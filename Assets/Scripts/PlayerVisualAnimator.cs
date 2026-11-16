using UnityEngine;

/// <summary>
/// Drives the imported humanoid Animator from the existing Player root.
/// Movement, networking, input and collision remain owned by PlayerController
/// and the other existing gameplay components.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerVisualAnimator : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float maxMoveSpeed = 9f;

    private Animator animator;
    private Transform playerRoot;
    private CharacterController characterController;
    private Vector3 lastRootPosition;
    private float airborneTime;
    private System.Collections.Generic.Dictionary<int, AnimatorControllerParameterType> parameterTypes =
        new System.Collections.Generic.Dictionary<int, AnimatorControllerParameterType>();

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
        PlayerController owner = GetComponentInParent<PlayerController>();
        playerRoot = owner != null ? owner.transform : transform.parent;
        if (animator != null) animator.applyRootMotion = false;
        characterController = playerRoot != null ? playerRoot.GetComponent<CharacterController>() : null;
        lastRootPosition = playerRoot != null ? playerRoot.position : transform.position;
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (var parameter in animator.parameters) parameterTypes[parameter.nameHash] = parameter.type;
    }

    private void LateUpdate()
    {
        if (animator == null || playerRoot == null)
            return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 rootDelta = (playerRoot.position - lastRootPosition) / deltaTime;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(rootDelta, Vector3.up);
        float speed = planarVelocity.magnitude;
        if (rootDelta.sqrMagnitude > 900f) speed = 0f; // ignore spawn/teleport displacement
        float motionSpeed = 1f; // Blend thresholds use world speed; analog input must not slow the clip twice.
        speed = Mathf.Min(speed, maxMoveSpeed * 1.5f);

        bool grounded = characterController == null || characterController.isGrounded;
        float verticalVelocity = characterController != null ? characterController.velocity.y : rootDelta.y;

        SetFloatIfPresent(SpeedHash, speed);
        SetFloatIfPresent(MotionSpeedHash, motionSpeed);
        SetBoolIfPresent(GroundedHash, grounded);
        airborneTime = grounded ? 0f : airborneTime + Time.deltaTime;
        SetBoolIfPresent(JumpHash, !grounded && verticalVelocity > 0.1f);
        SetBoolIfPresent(FreeFallHash, !grounded && airborneTime > 0.15f && verticalVelocity <= 0.1f);

        lastRootPosition = playerRoot.position;

    }

    private void SetFloatIfPresent(int parameterHash, float value)
    {
        if (HasParameter(parameterHash, AnimatorControllerParameterType.Float))
            animator.SetFloat(parameterHash, value, 0.12f, Time.deltaTime);
    }

    private void SetBoolIfPresent(int parameterHash, bool value)
    {
        if (HasParameter(parameterHash, AnimatorControllerParameterType.Bool))
            animator.SetBool(parameterHash, value);
    }

    // The imported jump clip contains an OnLand animation event. Keeping a
    // no-op receiver here prevents Unity from logging a missing receiver while
    // leaving landing behavior owned by the existing PlayerController.
    public void OnLand()
    {
        SetBoolIfPresent(JumpHash, false);
    }

    // The imported locomotion clips also contain OnFootstep events. Footstep
    // audio is not part of this visual replacement, but the receiver keeps the
    // animation clips from producing missing-event errors.
    public void OnFootstep()
    {
    }

    private bool HasParameter(int parameterHash, AnimatorControllerParameterType type)
    {
        return parameterTypes.TryGetValue(parameterHash, out var actual) && actual == type;
    }
}
