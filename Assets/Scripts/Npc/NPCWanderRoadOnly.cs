using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gives civilian NPCs a validated pedestrian route on the baked NavMesh.
/// The component keeps the ownership hooks used by police chase and dialogue
/// systems, while making normal movement feel more human: pedestrians choose
/// destinations ahead of themselves, vary walking speed, slow into targets and
/// pause before continuing.
/// </summary>
public class NPCWanderRoadOnly : MonoBehaviour
{
    [Header("Pedestrian Route")]
    [Min(4f)] public float wanderRadius = 22f;
    [Min(2f)] public float minWalkDistance = 7f;
    [Min(3f)] public float maxWalkDistance = 18f;
    [Min(0.1f)] public float minWaitTime = 1.2f;
    [Min(0.1f)] public float maxWaitTime = 3.8f;
    [Range(0f, 1f)] public float forwardBias = 0.72f;
    public string[] walkableTags = { "sideWalk", "crossWalk" };

    [Header("Human Motion")]
    [SerializeField, Min(0.1f)] private float minWalkSpeed = 1.15f;
    [SerializeField, Min(0.1f)] private float maxWalkSpeed = 1.75f;
    [SerializeField, Min(0f)] private float minStoppingDistance = 0.22f;
    [SerializeField, Min(0f)] private float maxStoppingDistance = 0.48f;
    [SerializeField, Min(1f)] private float minAngularSpeed = 150f;
    [SerializeField, Min(1f)] private float maxAngularSpeed = 220f;

    [Header("Navigation")]
    [SerializeField, Min(0.25f)] private float navMeshSearchRadius = 2f;
    [SerializeField, Range(4, 64)] private int destinationAttempts = 24;
    [SerializeField] private bool requireWalkableSurfaceTag;
    [SerializeField] private bool drawDebugPath;

    [HideInInspector] public NavMeshAgent agent;

    private HighStakes.AI.WaypointManager routeManager;
    private HighStakes.AI.WaypointNode routeNode, previousRouteNode;
    private int avoidancePriority;
    private float stuckTime;
    private Vector3 lastProgressPosition;
    private float nextAlignAttempt;
    private float waitTimer;
    private float waitDuration;
    private bool waiting;
    private Vector3 currentDestination;
    private NavMeshPath candidatePath;
    private NpcInteraction npcInt;
    private PoliceBehavior policeBehavior;
    private bool hasReportedMissingNavMesh;

    public bool canMove = true;

    /// <summary>True when the NPC currently owns a valid pedestrian path.</summary>
    public bool HasActivePath => agent != null && agent.enabled && agent.isOnNavMesh &&
        agent.hasPath && !agent.isStopped && agent.pathStatus == NavMeshPathStatus.PathComplete;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        avoidancePriority = Random.Range(30, 70);
        npcInt = GetComponent<NpcInteraction>();
        policeBehavior = GetComponent<PoliceBehavior>();
        candidatePath = new NavMeshPath();
    }

    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        policeBehavior = GetComponent<PoliceBehavior>();
        routeManager = FindAnyObjectByType<HighStakes.AI.WaypointManager>();
        lastProgressPosition = transform.position;
        ConfigureHumanMotion();
        canMove = true;
        TryAlignAgentToNavMesh();

        // Let the crowd settle into the scene instead of launching every NPC
        // on the same frame and in the same direction.
        BeginPause(Random.Range(0.15f, 1.2f));
    }

    private void Update()
    {
        if (agent == null || !agent.enabled)
            return;

        // Active dialogue owns movement; proximity only offers an interaction prompt.
        if (npcInt != null && (npcInt.isInteracting || npcInt.IsDialogueActive))
        {
            StopAgent();
            return;
        }

        // Police owns the agent during chase/investigation. Wander must not
        // overwrite the police destination or stop state.
        if ((npcInt != null && npcInt.chase) ||
            (policeBehavior != null && policeBehavior.MovementOwnedByPolice))
            return;

        if (!canMove)
        {
            StopAgent();
            return;
        }

        if (!agent.isOnNavMesh)
        {
            TryAlignAgentToNavMesh();
            return;
        }

        if (waiting)
        {
            StopAgent();
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitDuration)
            {
                waiting = false;
                ChooseNextDestination();
            }
            return;
        }

        if (agent.isStopped)
            agent.isStopped = false;

        bool reachedDestination = !agent.pathPending &&
            (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f);
        bool invalidPath = !agent.pathPending && agent.hasPath &&
            agent.pathStatus != NavMeshPathStatus.PathComplete;

        if (agent.hasPath && !agent.pathPending && !reachedDestination)
        {
            if ((transform.position - lastProgressPosition).sqrMagnitude > 0.09f)
            {
                stuckTime = 0f;
                lastProgressPosition = transform.position;
            }
            else stuckTime += Time.deltaTime;
        }
        if (reachedDestination || invalidPath || stuckTime > 3f)
        {
            stuckTime = 0f;
            lastProgressPosition = transform.position;
            BeginPause();
        }
    }

    private void ConfigureHumanMotion()
    {
        if (agent == null)
            return;

        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = avoidancePriority;
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.updateRotation = true;
        agent.speed = Random.Range(Mathf.Min(minWalkSpeed, maxWalkSpeed),
            Mathf.Max(minWalkSpeed, maxWalkSpeed));
        agent.acceleration = Random.Range(5.5f, 8.5f);
        agent.angularSpeed = Random.Range(Mathf.Min(minAngularSpeed, maxAngularSpeed),
            Mathf.Max(minAngularSpeed, maxAngularSpeed));
        agent.stoppingDistance = Random.Range(Mathf.Min(minStoppingDistance, maxStoppingDistance),
            Mathf.Max(minStoppingDistance, maxStoppingDistance));
    }

    private void BeginPause()
    {
        BeginPause(Random.Range(Mathf.Min(minWaitTime, maxWaitTime),
            Mathf.Max(minWaitTime, maxWaitTime)));
    }

    private void BeginPause(float duration)
    {
        waiting = true;
        waitTimer = 0f;
        waitDuration = Mathf.Max(0.05f, duration);
        StopAgent();
    }

    private void ChooseNextDestination()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (!TryFindPedestrianDestination(out Vector3 destination, out bool crosswalk))
        {
            // A short retry pause prevents a crowd from spinning or repeatedly
            // allocating paths when an NPC is temporarily boxed in.
            BeginPause(0.6f);
            return;
        }

        currentDestination = destination;
        ConfigureHumanMotion();
        agent.isStopped = false;

        if (!agent.SetDestination(currentDestination))
            BeginPause(0.6f);
    }

    private bool TryFindPedestrianDestination(out Vector3 result, out bool crosswalk)
    {
        result = transform.position;
        crosswalk = false;

        if (TryFindWaypointDestination(out result)) return true;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Random.insideUnitSphere;
            forward.y = 0f;
        }
        forward.Normalize();

        int areaMask = agent.areaMask == 0 ? NavMesh.AllAreas : agent.areaMask;
        int attempts = Mathf.Max(4, destinationAttempts);

        for (int i = 0; i < attempts; i++)
        {
            float distance = Random.Range(Mathf.Min(minWalkDistance, maxWalkDistance),
                Mathf.Max(minWalkDistance, maxWalkDistance));

            // Most pedestrians continue roughly forward, with occasional turns
            // to make the crowd look organic rather than grid-locked.
            float angle = Random.value < forwardBias
                ? Random.Range(-68f, 68f)
                : Random.Range(-175f, 175f);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * forward;
            Vector3 probe = transform.position + direction * distance;
            probe += new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));

            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit,
                    Mathf.Max(navMeshSearchRadius, 0.5f), areaMask))
                continue;

            if (Vector3.Distance(transform.position, hit.position) > wanderRadius)
                continue;

            if (!NavMesh.CalculatePath(transform.position, hit.position, areaMask, candidatePath) ||
                candidatePath.status != NavMeshPathStatus.PathComplete ||
                candidatePath.corners == null || candidatePath.corners.Length < 2)
                continue;

            bool hasSurfaceTag = HasWalkableSurfaceTag(hit.position, out bool hitCrosswalk);
            if (requireWalkableSurfaceTag && !hasSurfaceTag)
                continue;

            result = hit.position;
            crosswalk = hasSurfaceTag && hitCrosswalk;
            return true;
        }

        return false;
    }

    private bool TryFindWaypointDestination(out Vector3 result)
    {
        result = transform.position;
        if (routeManager == null || routeManager.Nodes.Count == 0) return false;
        if (routeNode == null)
        {
            float nearest = float.PositiveInfinity;
            foreach (var node in routeManager.Nodes)
            {
                if (node == null) continue;
                float distance = (node.transform.position - transform.position).sqrMagnitude;
                if (distance < nearest) { nearest = distance; routeNode = node; }
            }
        }
        if (routeNode == null) return false;
        if (Vector3.Distance(transform.position, routeNode.transform.position) > 2f)
            return TryRoutePath(routeNode.transform.position, out result);
        var connections = routeNode.ConnectedNodes;
        if (connections.Count == 0) return false;
        int first = Random.Range(0, connections.Count);
        for (int i = 0; i < connections.Count; i++)
        {
            var next = connections[(first + i) % connections.Count];
            if (next == null || (next == previousRouteNode && connections.Count > 1)) continue;
            if (!TryRoutePath(next.transform.position, out result)) continue;
            previousRouteNode = routeNode; routeNode = next;
            return true;
        }
        return false;
    }

    private bool TryRoutePath(Vector3 target, out Vector3 result)
    {
        result = transform.position;
        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 1.5f, agent.areaMask) ||
            !agent.CalculatePath(hit.position, candidatePath) || candidatePath.status != NavMeshPathStatus.PathComplete) return false;
        result = hit.position;
        return true;
    }

    private bool HasWalkableSurfaceTag(Vector3 position, out bool crosswalk)
    {
        crosswalk = false;
        if (walkableTags == null || walkableTags.Length == 0)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 2f,
            Vector3.down, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            foreach (string tag in walkableTags)
            {
                if (hit.collider != null && hit.collider.CompareTag(tag))
                {
                    crosswalk = tag == "crossWalk";
                    return true;
                }
            }
        }

        return false;
    }

    public void StopAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (agent.hasPath)
            agent.ResetPath();

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    public void ResumeMovement()
    {
        canMove = true;
        waiting = false;
        waitTimer = 0f;

        if (npcInt != null && (npcInt.isInteracting || npcInt.IsDialogueActive || npcInt.chase))
        {
            StopAgent();
            return;
        }

        if (agent == null || !agent.enabled)
            return;

        if (!agent.isOnNavMesh)
        {
            TryAlignAgentToNavMesh();
            return;
        }

        agent.isStopped = false;
        if (!agent.hasPath)
            ChooseNextDestination();
    }

    private bool TryAlignAgentToNavMesh()
    {
        if (agent == null || !agent.enabled || agent.isOnNavMesh)
            return agent != null && agent.isOnNavMesh;

        if (Time.time < nextAlignAttempt) return false;
        nextAlignAttempt = Time.time + 1f;
        float searchRadius = Mathf.Max(0.5f, navMeshSearchRadius);
        int areaMask = agent.areaMask == 0 ? NavMesh.AllAreas : agent.areaMask;
        Vector3 basePosition = transform.position - Vector3.up * agent.baseOffset;

        if (!NavMesh.SamplePosition(basePosition, out NavMeshHit hit, searchRadius, areaMask))
        {
            ReportMissingNavMesh();
            return false;
        }

        Vector3 alignedPosition = hit.position;
        if (Vector3.Distance(basePosition, hit.position) > searchRadius || !agent.Warp(alignedPosition))
        {
            ReportMissingNavMesh();
            return false;
        }

        hasReportedMissingNavMesh = false;
        return agent.isOnNavMesh;
    }

    private void ReportMissingNavMesh()
    {
        if (hasReportedMissingNavMesh)
            return;

        Debug.LogWarning($"{name} could not find a nearby pedestrian NavMesh position.");
        hasReportedMissingNavMesh = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugPath || agent == null || !agent.enabled || !agent.hasPath ||
            agent.path.corners == null || agent.path.corners.Length < 2)
            return;

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.8f);
        Vector3[] corners = agent.path.corners;
        for (int i = 0; i < corners.Length - 1; i++)
            Gizmos.DrawLine(corners[i] + Vector3.up * 0.08f, corners[i + 1] + Vector3.up * 0.08f);

        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
        Gizmos.DrawSphere(currentDestination + Vector3.up * 0.1f, 0.18f);
    }
}
