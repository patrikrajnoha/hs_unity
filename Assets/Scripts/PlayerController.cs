using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    [SerializeField] float runSpeed = 9f;
    private bool isRunning;
    public float gravity = -9.81f;
    public float jumpHeight = 2f;

    private CharacterController controller;
    private PlayerCameraModeSwitcher cameraModeSwitcher;
    private HighStakes.City.PlayerProductEffects productEffects;
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isGrounded;

    [Header("Look Settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 0.03f;
    private Vector2 lookInput;
    private float xRotation = 0f;
    private bool dialogueInputLocked;
    Vector2 currentMouseDelta;
    Vector2 currentMouseDeltaVelocity;
    public float mouseSmoothTime = 0.05f; // smaller = snappier

    private bool inventoryInputLocked;
    public bool IsDialogueInputLocked => dialogueInputLocked || inventoryInputLocked;

    public void SetInventoryInputLocked(bool locked)
    {
        inventoryInputLocked = locked;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        currentMouseDelta = Vector2.zero;
        currentMouseDeltaVelocity = Vector2.zero;
        isRunning = false;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cameraModeSwitcher = GetComponent<PlayerCameraModeSwitcher>();
    }

    private void Start()
    {
        cameraModeSwitcher = GetComponent<PlayerCameraModeSwitcher>();
        productEffects = GetComponent<HighStakes.City.PlayerProductEffects>();
        var input = GetComponent<PlayerInput>();
        if (input != null && input.actions != null)
            input.SwitchCurrentActionMap("Gameplay");

        if (!IsDialogueInputLocked) LockMouse();
    }

    public void ResetMotion() { velocity = Vector3.zero; }

    public void LockMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        mouseSensitivity = 0.03f;
    }
    public void UnlockMouse()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        mouseSensitivity = 0f;
    }

    public void StopMove()
    {
        moveSpeed = 0f;
        runSpeed = 0f;
        jumpHeight = 0f;
    }

    public void ResumeMove()
    {
        moveSpeed = 5f;
        runSpeed = 9f;
        jumpHeight = 2f;
    }

    /// <summary>
    /// Temporarily blocks gameplay movement and look while a local dialogue
    /// camera owns the presentation. UI input remains enabled.
    /// </summary>
    public void SetDialogueInputLocked(bool locked)
    {
        dialogueInputLocked = locked;
        if (locked)
        {
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            isRunning = false;
        }
    }

    /// <summary>
    /// Aligns the internal first-person pitch with the camera transform before
    /// another system hands camera ownership back to gameplay.
    /// </summary>
    public void SyncLookToCameraTransform()
    {
        if (playerCamera == null)
            return;

        float pitch = playerCamera.localEulerAngles.x;
        if (pitch > 180f)
            pitch -= 360f;

        xRotation = Mathf.Clamp(pitch, -90f, 90f);
        lookInput = Vector2.zero;
        currentMouseDelta = Vector2.zero;
        currentMouseDeltaVelocity = Vector2.zero;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = IsDialogueInputLocked ? Vector2.zero : context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsDialogueInputLocked && context.performed && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isGrounded = false; // prevent instant reset
        }
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        if (IsDialogueInputLocked)
        {
            isRunning = false;
            return;
        }

        if (context.started) isRunning = true;
        if (context.canceled) isRunning = false;
    }


    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = IsDialogueInputLocked ? Vector2.zero : context.ReadValue<Vector2>();
    }


    private void Update()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0) velocity.y = -2f;
        Vector3 horizontal = Vector3.zero;
        if (!IsDialogueInputLocked)
        {
            float currentSpeed = isRunning ? runSpeed : moveSpeed;
            if (productEffects != null)
                currentSpeed *= isRunning ? productEffects.SprintSpeedMultiplier : productEffects.WalkSpeedMultiplier;
            Vector3 move = cameraModeSwitcher != null && cameraModeSwitcher.IsThirdPerson
                ? cameraModeSwitcher.GetMovementDirection(moveInput)
                : transform.right * moveInput.x + transform.forward * moveInput.y;
            move = Vector3.ClampMagnitude(move, 1f);
            horizontal = move * currentSpeed;
            if (cameraModeSwitcher != null && cameraModeSwitcher.IsThirdPerson && move.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 12f * Time.deltaTime);
            }
        }
        // One collision step preserves the full velocity for animation. Gravity
        // remains active during conversations so jumping cannot freeze in midair.
        velocity.y += gravity * Time.deltaTime;
        controller.Move((horizontal + Vector3.up * velocity.y) * Time.deltaTime);
    }

    void LateUpdate()
    {
        HandleLook();
    }


    void HandleLook()
    {
        if (IsDialogueInputLocked)
            return;

        Vector2 targetMouseDelta = lookInput * mouseSensitivity;
        currentMouseDelta = Vector2.SmoothDamp(currentMouseDelta, targetMouseDelta, ref currentMouseDeltaVelocity, mouseSmoothTime);

        if (cameraModeSwitcher != null && cameraModeSwitcher.IsThirdPerson)
        {
            cameraModeSwitcher.ApplyThirdPersonLook(currentMouseDelta);
            return;
        }

        xRotation -= currentMouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * currentMouseDelta.x);
    }


}
