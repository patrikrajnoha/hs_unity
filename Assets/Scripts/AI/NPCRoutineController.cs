using UnityEngine;
using UnityEngine.AI;

namespace HighStakes.AI
{
    /// <summary>Moves an NPC through connected NavMesh waypoints and drives locomotion animation.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NPCRoutineController : MonoBehaviour
    {
        private enum RoutineState { Disabled, Travelling, Idling }

        [Header("References")]
        [SerializeField] private WaypointManager waypointManager;
        [SerializeField] private Animator animator;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float movementSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float acceleration = 8f;
        [SerializeField, Min(0.05f)] private float stoppingDistance = 0.35f;
        [SerializeField, Min(0.01f)] private float arrivalTolerance = 0.15f;

        [Header("Idle")]
        [SerializeField, Min(0f)] private float minimumWaitTime = 3f;
        [SerializeField, Min(0f)] private float maximumWaitTime = 10f;
        [SerializeField] private bool startOnEnable = true;

        [Header("Animator")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string movingParameter = "IsMoving";
        [SerializeField, Min(0.01f)] private float animationDampTime = 0.15f;
        [SerializeField, Min(0f)] private float movingThreshold = 0.1f;

        private NavMeshAgent agent;
        private WaypointNode currentNode;
        private WaypointNode destinationNode;
        private RoutineState state;
        private float idleTimer;

        public WaypointNode CurrentNode => currentNode;
        public WaypointNode DestinationNode => destinationNode;
        public bool IsIdle => state == RoutineState.Idling;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (waypointManager == null) waypointManager = FindAnyObjectByType<WaypointManager>();
            ApplyAgentSettings();
        }

        private void OnEnable()
        {
            if (startOnEnable) BeginRoutine();
        }

        private void Update()
        {
            var wander = GetComponent<NPCWanderRoadOnly>();
            var police = GetComponent<PoliceBehavior>();
            var interaction = GetComponent<NpcInteraction>();
            if ((wander != null && wander.enabled) || (police != null && police.MovementOwnedByPolice) ||
                (interaction != null && (interaction.isInteracting || interaction.IsDialogueActive))) return;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            UpdateAnimator();
            if (state == RoutineState.Travelling) UpdateTravel();
            else if (state == RoutineState.Idling) UpdateIdle();
        }

        public void BeginRoutine()
        {
            if (!isActiveAndEnabled || agent == null || waypointManager == null) return;
            if (!agent.isOnNavMesh)
            {
                if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    state = RoutineState.Disabled;
                    return;
                }
                agent.Warp(hit.position);
            }
            currentNode = FindClosestNode();
            SelectNextDestination();
        }

        public void StopRoutine()
        {
            state = RoutineState.Disabled;
            if (agent != null && agent.isOnNavMesh) agent.ResetPath();
        }

        private void ApplyAgentSettings()
        {
            agent.speed = movementSpeed;
            agent.acceleration = acceleration;
            agent.stoppingDistance = stoppingDistance;
            agent.autoBraking = true;
        }

        private void UpdateTravel()
        {
            if (agent.pathPending) return;
            if (destinationNode == null || !agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                SelectNextDestination();
                return;
            }
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + arrivalTolerance)
            {
                currentNode = destinationNode;
                destinationNode = null;
                agent.ResetPath();
                idleTimer = Random.Range(Mathf.Min(minimumWaitTime, maximumWaitTime), Mathf.Max(minimumWaitTime, maximumWaitTime));
                state = RoutineState.Idling;
            }
        }

        private void UpdateIdle()
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f) SelectNextDestination();
        }

        private void SelectNextDestination()
        {
            WaypointNode next = null;
            if (currentNode != null && currentNode.ConnectedNodes.Count > 0)
            {
                int startIndex = Random.Range(0, currentNode.ConnectedNodes.Count);
                for (int i = 0; i < currentNode.ConnectedNodes.Count; i++)
                {
                    WaypointNode candidate = currentNode.ConnectedNodes[(startIndex + i) % currentNode.ConnectedNodes.Count];
                    if (candidate != null && candidate != currentNode) { next = candidate; break; }
                }
            }
            if (next == null) next = waypointManager.GetRandomNode(currentNode);
            if (next == null || !agent.SetDestination(next.transform.position))
            {
                state = RoutineState.Disabled;
                return;
            }
            agent.isStopped = false;
            destinationNode = next;
            state = RoutineState.Travelling;
        }

        private WaypointNode FindClosestNode()
        {
            WaypointNode closest = null;
            float closestDistance = float.PositiveInfinity;
            foreach (WaypointNode node in waypointManager.Nodes)
            {
                if (node == null) continue;
                float distance = (node.transform.position - transform.position).sqrMagnitude;
                if (distance < closestDistance) { closestDistance = distance; closest = node; }
            }
            return closest;
        }

        private void UpdateAnimator()
        {
            if (animator == null || agent == null) return;
            var driver = animator.GetComponent<NPCVisualAnimatorDriver>();
            if (driver != null && driver.enabled && driver.DrivesAnimator) return;
            float speed = agent.velocity.magnitude;
            if (HasAnimatorParameter(speedParameter, AnimatorControllerParameterType.Float))
                animator.SetFloat(speedParameter, speed, animationDampTime, Time.deltaTime);
            if (HasAnimatorParameter(movingParameter, AnimatorControllerParameterType.Bool))
                animator.SetBool(movingParameter, state == RoutineState.Travelling && speed > movingThreshold);
        }

        private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                return false;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == parameterName && parameters[i].type == parameterType)
                    return true;
            }

            return false;
        }

        private void OnValidate()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (agent != null) ApplyAgentSettings();
            if (minimumWaitTime < 0f) minimumWaitTime = 0f;
            if (maximumWaitTime < minimumWaitTime) maximumWaitTime = minimumWaitTime;
        }
    }
}
