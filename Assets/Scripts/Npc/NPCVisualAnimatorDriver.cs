using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class NPCVisualAnimatorDriver : MonoBehaviour
{
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private float dampTime = 0.1f;
    [SerializeField] private float walkThreshold = 0.1f;
    [SerializeField] private float runThreshold = 3.0f;

    private Animator animator;
    private NavMeshAgent agent;
    private bool hasSpeedParameter;
    private bool hasMovingParameter;
    public bool DrivesAnimator => hasSpeedParameter || hasMovingParameter;
    private Vector3 previousPosition;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponentInParent<NavMeshAgent>();
        animator.applyRootMotion = false;
        previousPosition = transform.position;
        if (animator.runtimeAnimatorController != null)
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == speedParameter && parameter.type == AnimatorControllerParameterType.Float)
                    hasSpeedParameter = true;
                if (parameter.name == "IsMoving" && parameter.type == AnimatorControllerParameterType.Bool)
                    hasMovingParameter = true;
            }
    }

    private void LateUpdate()
    {
        if (animator == null) return;

        float speed = 0f;
        if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped)
            speed = agent.velocity.magnitude;

        // Actual displacement also supports remote/non-agent characters.
        if (agent == null)
            speed = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
        previousPosition = transform.position;
        if (hasMovingParameter) animator.SetBool("IsMoving", speed > 0.08f);
        if (hasSpeedParameter) animator.SetFloat(speedParameter, Mathf.Min(speed, 12f), dampTime, Time.deltaTime);
    }

    public float WalkThreshold => walkThreshold;
    public float RunThreshold => runThreshold;
}