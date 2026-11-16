using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum PolicePersonality
{
    Lawful,
    Aggressive,
    Lazy,
    Corrupt
}

public class PoliceBehavior : MonoBehaviour, INpcBehavior
{

    private GameMaster gm;
    private Inventory inv;
    private PoliceRange policeRange;
    private NPCWanderRoadOnly wanderScript;
    private NavMeshAgent agent;

    [Header("Police Personality")]
    public PolicePersonality personality = PolicePersonality.Lawful;

    [Header("Chase Settings")]
    public float chaseDuration = 120f; // seconds
    [SerializeField]
    public bool isChasing = false;
    public enum PursuitState { Patrol, Pursuing, Searching }
    public PursuitState State { get; private set; }
    public bool HasVisualContact { get; private set; }
    [SerializeField, Min(1f)] private float searchDuration = 12f;
    [SerializeField, Min(1f)] private float pursuitViewDistance = 32f;
    private Vector3 lastSeenPosition;
    private float lastSeenTime;
    private float nextRepath;
    private float pursuitStarted;
    private int patrolAreaMask;
    private float patrolSpeed;
    private float nextSearchPoint;
    private NavMeshPath pursuitPath;

    [Header("Sale Detection")]
    [SerializeField, Min(0.1f)] private float saleDetectionDistance = 15f;
    [SerializeField, Range(1f, 180f)] private float saleFieldOfView = 90f;
    [SerializeField] private Transform visionOrigin;
    [SerializeField] private LayerMask lineOfSightMask = ~0;


    private NpcInteraction npcInt;

    private PlayerStats currentPlayer;
    private int finePrice;

    public GameObject player;

    public bool isCalled = false;

    public bool MovementOwnedByPolice => isChasing || isCalled;

    private void OnEnable()
    {
        PoliceHeatRegistry.Register(this);
    }

    private void OnDisable()
    {
        PoliceHeatRegistry.Unregister(this);
    }

    void Start()
    {
        pursuitPath = new NavMeshPath();
        gm = FindAnyObjectByType<GameMaster>();

        policeRange = GetComponentInChildren<PoliceRange>();
        wanderScript = GetComponent<NPCWanderRoadOnly>();
        agent = GetComponent<NavMeshAgent>();
        npcInt = GetComponent<NpcInteraction>();
        patrolAreaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
        patrolSpeed = agent != null ? agent.speed : 1.5f;

    }

    void Update()
    {
        if (gm == null) gm = GameMaster.Instance;
        if (gm == null || GameMaster.GameOverActive) return;
        DetectSaleInProgress();
        if (!gm.crimeHappened || gm.criminal == null)
        {
            if (isChasing) StopChase();
            return;
        }
        Transform target = gm.criminal.transform;
        HasVisualContact = CanSeeTarget(target, isChasing ? pursuitViewDistance : saleDetectionDistance);
        if (!isChasing)
        {
            if (!HasVisualContact && !isCalled) return;
            StartChase(target);
        }
        if (HasVisualContact)
        {
            State = PursuitState.Pursuing;
            lastSeenPosition = target.position;
            lastSeenTime = Time.time;
        }
        else
        {
            State = PursuitState.Searching;
            if (Time.time - lastSeenTime > searchDuration || Time.time - pursuitStarted > chaseDuration)
            {
                GiveUpPursuit();
                return;
            }
        }
        if (npcInt != null && (npcInt.isInteracting || npcInt.IsDialogueActive)) return;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || Time.time < nextRepath) return;
        nextRepath = Time.time + 0.35f;
        agent.isStopped = false;
        agent.speed = HasVisualContact ? (personality == PolicePersonality.Lazy ? 4.5f : 6.2f) : 2.4f;
        Vector3 destination = lastSeenPosition;
        // Search the last known area, never the hidden player's live position.
        if (!HasVisualContact && !agent.pathPending && agent.remainingDistance < 1.5f && Time.time > nextSearchPoint)
        {
            Vector2 offset = Random.insideUnitCircle * 6f;
            destination += new Vector3(offset.x, 0f, offset.y);
            nextSearchPoint = Time.time + 2f;
        }
        else if (!HasVisualContact && agent.hasPath && agent.remainingDistance > 1.5f) return;
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, agent.areaMask) &&
            agent.CalculatePath(hit.position, pursuitPath) && pursuitPath.status == NavMeshPathStatus.PathComplete)
            agent.SetPath(pursuitPath);
    }

    public bool CanSeeTarget(Transform target, float maxDistance)
    {
        if (target == null) return false;
        Vector3 origin = visionOrigin != null ? visionOrigin.position : transform.position + Vector3.up * 1.5f;
        Vector3 point = target.position + Vector3.up;
        Vector3 direction = point - origin;
        if (direction.magnitude > maxDistance) return false;
        if (Vector3.Angle(transform.forward, direction) > (isChasing ? 75f : saleFieldOfView * 0.5f)) return false;
        // Ignore the officer's own colliders; the first other solid hit must be the target.
        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
            lineOfSightMask.value == 0 ? Physics.DefaultRaycastLayers : lineOfSightMask.value, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            return hit.transform == target || hit.transform.IsChildOf(target);
        }
        return true;
    }

    private void GiveUpPursuit()
    {
        GameObject suspect = gm != null ? gm.criminal : null;
        StopChase();
        foreach (PoliceBehavior officer in PoliceHeatRegistry.ActivePolice)
            if (officer != null && officer != this && officer.isChasing) return;
        PoliceHeatController heat = suspect != null ? suspect.GetComponent<PoliceHeatController>() : null;
        if (heat != null) heat.EndPursuit();
        if (gm != null) gm.ClearPoliceChase(suspect);
    }

    private void DetectSaleInProgress()
    {
        if (gm == null)
            return;

        foreach (PoliceHeatController heatController in PoliceHeatRegistry.ActivePlayers)
        {
            if (heatController == null || !heatController.IsSaleInProgress)
                continue;

            TryReportVisibleSale(heatController);
        }

        // Keeps compatibility with older scenes that assign PoliceBehavior.player
        // before the player has registered with the runtime feature.
        if (player != null && player.GetComponent<PoliceHeatController>() == null && gm.IsPlayerSelling(player))
        {
            PoliceHeatController fallback = player.AddComponent<PoliceHeatController>();
            TryReportVisibleSale(fallback);
        }
    }

    private void TryReportVisibleSale(PoliceHeatController target)
    {
        if (CanSeeTarget(target.transform, saleDetectionDistance))
            target.ReportPoliceObservation(this);
    }

    private void SetRangeScale(float x, float z)
    {
        if (policeRange == null) return;
        Vector3 scale = policeRange.transform.localScale;
        scale.x = x;
        scale.z = z;
        policeRange.transform.localScale = scale;
    }

    private void StartChase(Transform target)
    {
        if (isChasing || target == null) return;
        isChasing = true;
        State = PursuitState.Pursuing;
        lastSeenPosition = target.position;
        lastSeenTime = pursuitStarted = Time.time;
        nextRepath = 0f;
        isCalled = false;
        if (npcInt != null)
        {
            npcInt.isInteracting = false;
            npcInt.isClose = false;
            npcInt.chase = true;
        }
        gm.isChasingSomeone = true;
        if (agent != null) agent.areaMask = NavMesh.AllAreas;
        if (wanderScript != null) wanderScript.canMove = false;
    }

    private void StopChase()
    {
        isChasing = false;
        HasVisualContact = false;
        State = PursuitState.Patrol;
        isCalled = false;
        if (npcInt != null) npcInt.chase = false;
        SetRangeScale(15f, 10f);
        if (agent != null && agent.enabled)
        {
            agent.areaMask = patrolAreaMask;
            agent.speed = patrolSpeed;
            if (agent.isOnNavMesh) agent.ResetPath();
        }
        if (wanderScript != null) wanderScript.ResumeMovement();
    }

    public void Arrest()
    {
        if (gm != null && gm.arrest)
        {
            Debug.Log("u should be here ARREST so the agent is supposed to stop..");
            if (inv == null && gm.criminal != null)
            {
                player = gm.criminal;
                inv = player.GetComponent<Inventory>();
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;

            NpcInteraction npcInteraction = GetComponent<NpcInteraction>();
            if (npcInteraction != null) npcInteraction.interactionMessage = NpcDialogue.Get("police.arrest_demand");

            StartCoroutine(ArrestAnim());


            //finePrice = currentPlayer.money / 2;
            finePrice = 50;
            //Debug.Log("current money / 2 : " + finePrice);
            // taktiez by si mal zaplatit nejaku pokutu..
            //currentPlayer.money -= finePrice;




        }

    }

    public void Interact(PlayerStats playerStats)
    {
        if (npcInt == null) npcInt = GetComponent<NpcInteraction>();
        if (npcInt == null || playerStats == null) return;
        player = npcInt.currentPlayer;
        if (player == null) player = playerStats.gameObject;
        inv = player.GetComponent<Inventory>();
        currentPlayer = playerStats;
        if (inv != null) inv.HasIllegal();

        bool isCriminal = gm != null && gm.HeDoes();
        string opening = GetGreeting() + "\n" +
            (isCriminal ? GetRandomResponse("arrest") : DecideResponse(playerStats));

        npcInt.ShowConversationChoices(
            opening,
            isCriminal ? NpcDialogue.Get("choice.explain_the_situation") : NpcDialogue.Get("choice.what_is_this_about"), () =>
            {
                npcInt.RememberChoice("police.explain", 0);
                npcInt.ShowConversationChoices(
                    NpcDialogue.Get("police.explain"),
                    NpcDialogue.Get("choice.cooperate"), () =>
                    {
                        npcInt.RememberChoice("police.cooperate", 2);
                        npcInt.ShowConversationChoices(
                            NpcDialogue.Get("police.cooperate"),
                            NpcDialogue.Get("choice.leave"), () => npcInt.CloseConversation(NpcDialogue.Get("police.leave")),
                            null, null,
                            null, null);
                    },
                    NpcDialogue.Get("choice.stay_silent"), ShowChallengeResponse,
                    NpcDialogue.Get("choice.leave"), () => npcInt.CloseConversation(NpcDialogue.Get("police.leave")));
            },
            NpcDialogue.Get("choice.keep_your_distance"), ShowChallengeResponse,
            NpcDialogue.Get("choice.leave"), () => npcInt.CloseConversation(NpcDialogue.Get("police.leave")));

        Debug.Log($"Police ({personality}) opened a contextual conversation.");
    }

    private string GetGreeting()
    {
        if (npcInt.ConversationCount <= 1)
            return NpcDialogue.Get("police.greeting.first");

        if (npcInt.LastChoiceId == "police.cooperate")
            return NpcDialogue.Get("police.greeting.cooperative");

        if (npcInt.LastChoiceId == "police.challenge" ||
            npcInt.RelationshipScore <= -1)
            return NpcDialogue.Get("police.greeting.hostile");

        return NpcDialogue.Get("police.greeting.return");
    }

    private void ShowChallengeResponse()
    {
        npcInt.RememberChoice("police.challenge", -1);
        npcInt.ShowConversationChoices(
            NpcDialogue.Get("police.challenge"),
            NpcDialogue.Get("choice.explain_that"), () =>
            {
                npcInt.RememberChoice("police.explain_again", 0);
                npcInt.ShowConversationChoices(
                    NpcDialogue.Get("police.explain"),
                    NpcDialogue.Get("choice.cooperate"), () => npcInt.CloseConversation(NpcDialogue.Get("police.cooperate")),
                    NpcDialogue.Get("choice.leave"), () => npcInt.CloseConversation(NpcDialogue.Get("police.leave")),
                    null, null);
            },
            NpcDialogue.Get("choice.leave"), () => npcInt.CloseConversation(NpcDialogue.Get("police.leave")),
            null, null);
    }



    private string DecideResponse(PlayerStats player)
    {
        // Example checks — you can expand these based on your game logic
        bool isCriminal = gm != null && gm.HeDoes();
        bool isAggressive = player.currentMood == Mood.Angry;

        if (isCriminal)
        {
            return GetRandomResponse("arrest");
        }
        else if (isAggressive)
        {
            return GetRandomResponse("warn");
        }
        else
        {
            return GetRandomResponse("greet");
        }
    }

    private string GetRandomResponse(string type)
    {
        if (type == "greet" || type == "warn" || type == "arrest")
            return NpcDialogue.Get($"police.{type}");

        return NpcDialogue.Get("police.move_along");
    }

    // Optional patrol or chase methods
    public void Patrol() { /* implement patrol logic */ }
    public void ChasePlayer(Transform player) { /* implement chase logic */ }
    public void CallBackup() { /* implement backup spawn */ }


    private IEnumerator ArrestAnim()
    {
        Debug.Log("arrest animation...");
        // pick random duration between 30 and 60 sec
        float interactionTime = Random.Range(10f, 15f);

        /*
        // start animation
        if (animator != null)
        {
            animator.SetBool(animationName, true);
            // or: animator.Play(animationName);
        }
        */



        // wait for that duration
        yield return new WaitForSeconds(interactionTime);
        /*
        // stop animation / release interaction
        if (animator != null)
        {
            animator.SetBool(animationName, false);
        }
        */

        // Arrest can be triggered by the wanted-system chase without first
        // opening a police dialogue, so Interact() may not have initialized
        // inv. Resolve it from the criminal at the point the arrest finishes.
        if (inv == null && gm != null && gm.criminal != null)
            inv = gm.criminal.GetComponent<Inventory>();

        if (inv != null)
        {
            inv.HasIllegal();
            if (gm != null && gm.hasIllegal)
            {
                // tu by mala byt logika odpocitania veci z inventara
                inv.RemoveAllIllegal();
            }
        }


        StopChase();
        gm.arrest = false;
        if (gm.criminal != null)
        {
            gm.criminal.gameObject.GetComponent<PlayerController>().LockMouse();
            gm.criminal.gameObject.GetComponent<PlayerController>().ResumeMove();
        }

        SetRangeScale(15f, 10f);

        Debug.Log("deal happened.. hopefully..");
        gm.crimeHappened = false;
        gm.criminal = null;

    }

}
