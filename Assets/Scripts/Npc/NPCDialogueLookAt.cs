using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class NPCDialogueLookAt : MonoBehaviour
{
    [Header("Body rotation")]
    [SerializeField, Range(0f, 180f)] private float bodyYawLimit = 115f;
    [SerializeField] private float bodyTurnSpeed = 180f;

    [Header("Head and eye limits")]
    [SerializeField, Range(0f, 90f)] private float headYawLimit = 70f;
    [SerializeField, Range(0f, 90f)] private float headPitchUpLimit = 35f;
    [SerializeField, Range(0f, 90f)] private float headPitchDownLimit = 25f;

    [Header("Smoothing")]
    [SerializeField] private float blendInTime = 0.2f;
    [SerializeField] private float blendOutTime = 0.35f;
    [SerializeField] private float targetSmoothTime = 0.08f;

    private Animator animator;
    private NpcInteraction interaction;
    private Transform body;
    private Quaternion initialBodyRotation;
    private float lookBlend;
    private float targetBlend;
    private Vector3 smoothedTarget;
    private Vector3 targetVelocity;
    private bool hasTarget;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        interaction = GetComponentInParent<NpcInteraction>();
        body = interaction != null ? interaction.transform : transform.root;
        initialBodyRotation = body.rotation;

        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void Update()
    {
        if (interaction == null)
            interaction = GetComponentInParent<NpcInteraction>();

        Camera dialogueCamera = FindDialogueCamera();
        bool active = interaction != null && interaction.IsDialogueActive && dialogueCamera != null;
        targetBlend = active ? 1f : 0f;
        float blendSpeed = (targetBlend > lookBlend ? blendInTime : blendOutTime);
        lookBlend = Mathf.MoveTowards(lookBlend, targetBlend, Time.deltaTime / Mathf.Max(0.01f, blendSpeed));

        if (active)
        {
            Vector3 desiredTarget = dialogueCamera.transform.position;
            if (!hasTarget)
                smoothedTarget = desiredTarget;
            smoothedTarget = Vector3.SmoothDamp(smoothedTarget, desiredTarget, ref targetVelocity, targetSmoothTime);
            hasTarget = true;
            RotateBodyToward(dialogueCamera.transform.position);
        }
        else if (body != null && lookBlend <= 0.001f)
        {
            // Navigation owns body yaw outside dialogue. Restoring the spawn
            // rotation here fought the agent on every walking frame.
            hasTarget = false;
        }
    }

    private void RotateBodyToward(Vector3 targetPosition)
    {
        if (body == null || lookBlend <= 0.001f)
            return;

        Vector3 flatDirection = Vector3.ProjectOnPlane(targetPosition - body.position, Vector3.up);
        if (flatDirection.sqrMagnitude < 0.0001f)
            return;

        float signedYaw = Vector3.SignedAngle(body.forward, flatDirection.normalized, Vector3.up);
        float clampedYaw = Mathf.Clamp(signedYaw, -bodyYawLimit, bodyYawLimit);
        Quaternion desired = Quaternion.AngleAxis(clampedYaw, Vector3.up) * body.rotation;
        body.rotation = Quaternion.RotateTowards(body.rotation, desired, bodyTurnSpeed * Time.deltaTime * lookBlend);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || !animator.isHuman || !hasTarget)
            return;

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
            return;

        Vector3 toTarget = smoothedTarget - head.position;
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Vector3 localDirection = body != null ? body.InverseTransformDirection(toTarget.normalized) : toTarget.normalized;
        float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(localDirection.y, new Vector2(localDirection.x, localDirection.z).magnitude) * Mathf.Rad2Deg;
        yaw = Mathf.Clamp(yaw, -headYawLimit, headYawLimit);
        pitch = Mathf.Clamp(pitch, -headPitchUpLimit, headPitchDownLimit);

        Vector3 clampedDirection = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
        Vector3 clampedTarget = head.position + (body != null ? body.TransformDirection(clampedDirection) : clampedDirection) * Mathf.Max(0.5f, toTarget.magnitude);

        animator.SetLookAtPosition(clampedTarget);
        animator.SetLookAtWeight(lookBlend, 0.12f * lookBlend, 0.55f * lookBlend, 0.9f * lookBlend, 0.35f * lookBlend);
    }

    private Camera FindDialogueCamera()
    {
        if (interaction != null && interaction.currentPlayer != null)
        {
            Camera playerCamera = interaction.currentPlayer.GetComponentInChildren<Camera>(true);
            if (playerCamera != null && playerCamera.enabled)
                return playerCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
            return mainCamera;

        // DialogueCameraController may keep the active camera at scene root and
        // the project camera is currently untagged, so do not rely on Camera.main.
        foreach (Camera camera in Camera.allCameras)
        {
            if (camera != null && camera.isActiveAndEnabled)
                return camera;
        }

        return null;
    }
}
