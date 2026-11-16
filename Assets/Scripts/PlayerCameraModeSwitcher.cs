using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Toggles first-person and a GTA-style shoulder camera. The camera orbits in
/// world space, follows the player with a collision-aware spring distance and
/// keeps movement relative to the camera while in third person.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[AddComponentMenu("HighStakes/Camera/Player Camera Mode Switcher")]
public sealed class PlayerCameraModeSwitcher : MonoBehaviour
{
    public enum CameraMode
    {
        FirstPerson,
        ThirdPerson
    }

    [Header("Mode")]
    [SerializeField] private CameraMode startingMode = CameraMode.FirstPerson;
    [SerializeField] private Vector3 firstPersonPosition = new Vector3(0f, 0.564f, -0.04f);

    [Header("Third person camera")]
    [SerializeField, Min(1f)] private float thirdPersonDistance = 4.75f;
    [SerializeField] private float thirdPersonPivotHeight = 1.45f;
    [SerializeField] private float thirdPersonShoulderOffset = 0.55f;
    [SerializeField, Range(-10f, 45f)] private float thirdPersonStartingPitch = 12f;
    [SerializeField, Range(-30f, 0f)] private float thirdPersonMinPitch = -12f;
    [SerializeField, Range(0f, 80f)] private float thirdPersonMaxPitch = 45f;
    [SerializeField, Min(0.02f)] private float cameraCollisionRadius = 0.2f;

    private Camera playerCamera;
    private Transform playerVisual;
    private Renderer[] visualRenderers;
    private DialogueCameraController dialogueCameraController;
    private PlayerController playerController;
    private CameraMode currentMode;
    private float orbitYaw;
    private float orbitPitch;

    public CameraMode CurrentMode => currentMode;
    public bool IsThirdPerson => currentMode == CameraMode.ThirdPerson;

    private void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>(true);
        playerVisual = transform.Find("PlayerVisual");
        visualRenderers = playerVisual != null
            ? playerVisual.GetComponentsInChildren<Renderer>(true)
            : new Renderer[0];
        dialogueCameraController = GetComponent<DialogueCameraController>();
        playerController = GetComponent<PlayerController>();
        ApplyMode(startingMode);
    }

    private void Update()
    {
        if (playerController != null && playerController.IsDialogueInputLocked) return;
        if (dialogueCameraController != null && dialogueCameraController.IsInDialogue)
            return;

        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            ToggleMode();
    }

    private void LateUpdate()
    {
        if (IsThirdPerson && (dialogueCameraController == null || !dialogueCameraController.IsInDialogue))
            UpdateThirdPersonCamera();
    }

    public void ToggleMode()
    {
        ApplyMode(currentMode == CameraMode.FirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson);
    }

    public void SetMode(CameraMode mode)
    {
        ApplyMode(mode);
    }

    public void SetFirstPersonPosition(Vector3 localPosition)
    {
        firstPersonPosition = localPosition;
        if (!IsThirdPerson) ApplyMode(CameraMode.FirstPerson);
    }

    /// <summary>
    /// Receives the already-smoothed look delta from PlayerController.
    /// </summary>
    public void ApplyThirdPersonLook(Vector2 lookDelta)
    {
        if (!IsThirdPerson)
            return;

        orbitYaw += lookDelta.x;
        orbitPitch = Mathf.Clamp(orbitPitch - lookDelta.y, thirdPersonMinPitch, thirdPersonMaxPitch);
    }

    public Vector3 GetMovementDirection(Vector2 moveInput)
    {
        if (playerCamera == null)
            return new Vector3(moveInput.x, 0f, moveInput.y);

        Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(playerCamera.transform.right, Vector3.up).normalized;
        return Vector3.ClampMagnitude(right * moveInput.x + forward * moveInput.y, 1f);
    }

    private void ApplyMode(CameraMode mode)
    {
        currentMode = mode;
        if (playerCamera != null)
        {
            playerCamera.tag = "MainCamera";
            playerCamera.enabled = true;
        }

        bool showVisual = mode == CameraMode.ThirdPerson;
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] != null)
                visualRenderers[i].enabled = showVisual;
        }

        if (mode == CameraMode.FirstPerson)
        {
            if (playerCamera != null)
            {
                playerCamera.transform.localPosition = firstPersonPosition;
                playerCamera.transform.localRotation = Quaternion.identity;
            }

            if (playerController != null)
                playerController.SyncLookToCameraTransform();
        }
        else
        {
            orbitYaw = transform.eulerAngles.y;
            orbitPitch = thirdPersonStartingPitch;
            UpdateThirdPersonCamera();
        }
    }

    private void UpdateThirdPersonCamera()
    {
        if (playerCamera == null)
            return;

        Vector3 pivot = transform.position + Vector3.up * thirdPersonPivotHeight;
        Quaternion orbit = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        Vector3 offset = orbit * (Vector3.back * thirdPersonDistance + Vector3.right * thirdPersonShoulderOffset);
        float safeDistance = ResolveCameraDistance(pivot, offset.normalized, offset.magnitude);
        Vector3 desiredPosition = pivot + offset.normalized * safeDistance;
        playerCamera.transform.position = desiredPosition;
        Vector3 lookTarget = pivot + Vector3.up * 0.15f;
        playerCamera.transform.rotation = Quaternion.LookRotation(lookTarget - desiredPosition, Vector3.up);
    }

    private float ResolveCameraDistance(Vector3 pivot, Vector3 directionToCamera, float desiredDistance)
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            pivot,
            cameraCollisionRadius,
            directionToCamera,
            desiredDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float safeDistance = desiredDistance;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || collider.transform.IsChildOf(transform))
                continue;

            safeDistance = Mathf.Min(safeDistance, hits[i].distance - cameraCollisionRadius);
        }

        return Mathf.Clamp(safeDistance, 0.65f, desiredDistance);
    }
}
