using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Local-player-only conversation camera for the existing first-person camera.
/// The camera is temporarily detached from the player while this component is
/// the owner, then restored to its original parent/local state on exit.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("HighStakes/Camera/Dialogue Camera Controller")]
public sealed class DialogueCameraController : MonoBehaviour
{
    private enum CameraState
    {
        Gameplay,
        Entering,
        Dialogue,
        Exiting
    }

    [Header("Camera references")]
    [Tooltip("Optional. If empty, PlayerController.playerCamera is used.")]
    [SerializeField] private Camera playerCamera;

    [Header("Local player visibility")]
    [Tooltip("Legacy explicit renderer reference. During dialogue all local MeshRenderer and SkinnedMeshRenderer components under this Player are hidden; colliders and gameplay components remain enabled.")]
    [SerializeField] private Renderer localPlayerBodyRenderer;

    [Header("Dialogue framing")]
    [Tooltip("Base camera distance for the Fallout-style frame. The final distance scales with the NPC renderer height.")]
    [Min(0.5f)] public float dialogueDistance = 2.3f;
    [Tooltip("Aim offset from the resolved head/face target. Keep this near eye level; body visibility comes from distance, not a lowered aim point.")]
    public float targetVerticalOffset = 0f;
    [Tooltip("Local-right composition offset. Zero keeps the NPC centrally framed.")]
    public float horizontalFramingOffset = 0f;
    [Tooltip("Keep close to gameplay FOV; physical distance does the framing work.")]
    [Min(0.05f)] public float dialogueFov = 60f;
    [Tooltip("World-space NPC height represented by dialogueDistance before adaptive scaling.")]
    [Min(0.1f)] public float referenceNpcHeight = 2.1f;

    [Header("Transition")]
    [Min(0f)] public float transitionDuration = 0.45f;
    [Min(0f)] public float returnDuration = 0.55f;

    [Header("Collision")]
    public bool useCollisionProtection = true;
    public LayerMask collisionMask = ~0;
    [Min(0f)] public float collisionRadius = 0.12f;
    [Min(0f)] public float collisionPadding = 0.18f;

    [Header("Follow")]
    [Min(0.01f)] public float followSmoothTime = 0.08f;
    [Min(0.01f)] public float rotationSharpness = 14f;

    private PlayerController playerController;
    private Transform dialogueNpc;
    private Transform gameplayParent;
    private CameraState state;

    private Vector3 gameplayLocalPosition;
    private Quaternion gameplayLocalRotation;
    private Vector3 gameplayWorldPosition;
    private Quaternion gameplayWorldRotation;
    private float gameplayFov;
    private bool gameplayStateSaved;
    private readonly List<Renderer> localDialogueRenderers = new List<Renderer>();
    private readonly List<bool> localDialogueRendererStates = new List<bool>();
    private bool localVisualVisibilityApplied;
    private bool gameplayRestorePending;

    private Vector3 transitionStartPosition;
    private Quaternion transitionStartRotation;
    private float transitionStartFov;
    private float transitionElapsed;
    private float transitionTime;

    private Vector3 followVelocity;
    private float fovVelocity;

    public bool IsInDialogue => state != CameraState.Gameplay;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        if (playerCamera == null && playerController != null && playerController.playerCamera != null)
            playerCamera = playerController.playerCamera.GetComponent<Camera>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        state = CameraState.Gameplay;
    }

    private void OnEnable()
    {
        if (gameplayRestorePending && gameplayStateSaved)
        {
            gameplayRestorePending = false;
            RestoreGameplayState();
        }
    }

    private void LateUpdate()
    {
        if (playerCamera == null)
            return;

        switch (state)
        {
            case CameraState.Entering:
                UpdateEntering();
                break;
            case CameraState.Dialogue:
                UpdateDialogue();
                break;
            case CameraState.Exiting:
                UpdateExiting();
                break;
        }
    }

    public void EnterDialogue(Transform npc)
    {
        if (!IsLocallyOwned() || npc == null || playerCamera == null)
            return;

        dialogueNpc = npc;

        if (!gameplayStateSaved)
        {
            SaveGameplayState();
            playerCamera.transform.SetParent(null, true);
        }

        if (playerController != null)
            playerController.SetDialogueInputLocked(true);

        Vector3 targetPosition;
        Quaternion targetRotation;
        if (!TryCalculateDialogueFrame(out targetPosition, out targetRotation))
        {
            RestoreGameplayState();
            return;
        }

        ApplyLocalPlayerVisibility();
        StartTransition(targetPosition, targetRotation, dialogueFov, transitionDuration, CameraState.Entering);
    }

    public void ExitDialogue()
    {
        if (!gameplayStateSaved || state == CameraState.Gameplay)
            return;

        Vector3 targetPosition;
        Quaternion targetRotation;
        GetGameplayWorldPose(out targetPosition, out targetRotation);
        StartTransition(targetPosition, targetRotation, gameplayFov, returnDuration, CameraState.Exiting);
    }

    private void SaveGameplayState()
    {
        gameplayStateSaved = true;
        gameplayParent = playerCamera.transform.parent;
        gameplayLocalPosition = playerCamera.transform.localPosition;
        gameplayLocalRotation = playerCamera.transform.localRotation;
        gameplayWorldPosition = playerCamera.transform.position;
        gameplayWorldRotation = playerCamera.transform.rotation;
        gameplayFov = playerCamera.fieldOfView;
    }

    private void StartTransition(Vector3 targetPosition, Quaternion targetRotation, float targetFov,
        float duration, CameraState targetState)
    {
        transitionStartPosition = playerCamera.transform.position;
        transitionStartRotation = playerCamera.transform.rotation;
        transitionStartFov = playerCamera.fieldOfView;
        transitionElapsed = 0f;
        transitionTime = Mathf.Max(0.01f, duration);
        followVelocity = Vector3.zero;
        fovVelocity = 0f;

        playerCamera.transform.SetParent(null, true);
        state = targetState;

        if (targetState == CameraState.Entering)
        {
            // The end pose is recalculated while entering so moving NPCs do not
            // leave the camera aimed at an obsolete position.
            SetPose(transitionStartPosition, transitionStartRotation, transitionStartFov);
        }
    }

    private void UpdateEntering()
    {
        if (!HasValidDialogueTarget())
        {
            ExitDialogue();
            return;
        }

        Vector3 targetPosition;
        Quaternion targetRotation;
        if (!TryCalculateDialogueFrame(out targetPosition, out targetRotation))
        {
            ExitDialogue();
            return;
        }

        transitionElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(transitionElapsed / transitionTime);
        t = Mathf.SmoothStep(0f, 1f, t);
        SetPose(
            Vector3.Lerp(transitionStartPosition, targetPosition, t),
            Quaternion.Slerp(transitionStartRotation, targetRotation, t),
            Mathf.Lerp(transitionStartFov, dialogueFov, t));

        if (t >= 1f)
        {
            state = CameraState.Dialogue;
            followVelocity = Vector3.zero;
            fovVelocity = 0f;
        }
    }

    private void UpdateDialogue()
    {
        if (!HasValidDialogueTarget())
        {
            ExitDialogue();
            return;
        }

        Vector3 targetPosition;
        Quaternion targetRotation;
        if (!TryCalculateDialogueFrame(out targetPosition, out targetRotation))
        {
            ExitDialogue();
            return;
        }

        playerCamera.transform.position = Vector3.SmoothDamp(
            playerCamera.transform.position,
            targetPosition,
            ref followVelocity,
            followSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation,
            targetRotation,
            1f - Mathf.Exp(-rotationSharpness * Time.unscaledDeltaTime));
        playerCamera.fieldOfView = Mathf.SmoothDamp(
            playerCamera.fieldOfView,
            dialogueFov,
            ref fovVelocity,
            followSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
    }

    private void UpdateExiting()
    {
        Vector3 targetPosition;
        Quaternion targetRotation;
        GetGameplayWorldPose(out targetPosition, out targetRotation);

        transitionElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(transitionElapsed / transitionTime);
        t = Mathf.SmoothStep(0f, 1f, t);
        SetPose(
            Vector3.Lerp(transitionStartPosition, targetPosition, t),
            Quaternion.Slerp(transitionStartRotation, targetRotation, t),
            Mathf.Lerp(transitionStartFov, gameplayFov, t));

        if (t >= 1f)
            RestoreGameplayState();
    }

    private void RestoreGameplayState()
    {
        RestoreLocalPlayerVisibility();

        if (playerCamera != null)
        {
            // The exit blend already ends at the current gameplay-parent pose.
            // Restore parent and local values as one logical handoff instead of
            // writing world pose, reparenting with world preservation, and then
            // writing local pose again. That redundant sequence can expose a
            // one-frame world/local mismatch before gameplay look resumes.
            playerCamera.transform.SetParent(gameplayParent, false);
            if (gameplayParent != null)
            {
                playerCamera.transform.localPosition = gameplayLocalPosition;
                playerCamera.transform.localRotation = gameplayLocalRotation;
            }
            else
            {
                playerCamera.transform.position = this.gameplayWorldPosition;
                playerCamera.transform.rotation = this.gameplayWorldRotation;
            }

            playerCamera.fieldOfView = gameplayFov;

            // PlayerController must inherit the camera's final gameplay pitch
            // before it is allowed to process look input again.
            if (playerController != null)
                playerController.SyncLookToCameraTransform();
        }

        if (playerController != null)
            playerController.SetDialogueInputLocked(false);

        dialogueNpc = null;
        gameplayStateSaved = false;
        state = CameraState.Gameplay;
        followVelocity = Vector3.zero;
        fovVelocity = 0f;
    }

    private void GetGameplayWorldPose(out Vector3 position, out Quaternion rotation)
    {
        if (gameplayParent != null)
        {
            position = gameplayParent.TransformPoint(gameplayLocalPosition);
            rotation = gameplayParent.rotation * gameplayLocalRotation;
        }
        else
        {
            position = gameplayWorldPosition;
            rotation = gameplayWorldRotation;
        }
    }

    private bool TryCalculateDialogueFrame(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        Vector3 target = ResolveTargetPosition(dialogueNpc);
        target += Vector3.up * targetVerticalOffset;
        Vector3 fromTargetToCamera = gameplayWorldPosition - target;
        if (fromTargetToCamera.sqrMagnitude < 0.01f)
            fromTargetToCamera = -dialogueNpc.forward;
        fromTargetToCamera.Normalize();

        Vector3 targetRight = dialogueNpc.right;
        Vector3 desiredPosition = target + fromTargetToCamera * GetAdaptiveDialogueDistance(dialogueNpc);
        desiredPosition += targetRight * horizontalFramingOffset;

        if (useCollisionProtection)
            desiredPosition = ResolveCollision(target, desiredPosition);

        Vector3 lookDirection = target - desiredPosition;
        if (lookDirection.sqrMagnitude < 0.0001f)
            return false;

        position = desiredPosition;
        rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        return true;
    }

    private float GetAdaptiveDialogueDistance(Transform npc)
    {
        Renderer[] renderers = npc.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(npc.position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        if (!hasBounds || bounds.size.y <= 0.01f)
            return Mathf.Max(0.5f, dialogueDistance);

        // Scale physical distance with the complete renderer height. This keeps
        // differently-sized NPCs at a similar on-screen size while preserving
        // the eye-level aim target used by the dialogue shot.
        float heightMultiplier = Mathf.Clamp(
            bounds.size.y / Mathf.Max(0.1f, referenceNpcHeight),
            0.8f,
            1.5f);
        return Mathf.Max(0.5f, dialogueDistance * heightMultiplier);
    }

    private Vector3 ResolveCollision(Vector3 target, Vector3 desiredPosition)
    {
        Vector3 toCamera = desiredPosition - target;
        float distance = toCamera.magnitude;
        if (distance <= 0.01f)
            return desiredPosition;

        Vector3 direction = toCamera / distance;
        Vector3 origin = target + direction * 0.08f;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            collisionRadius,
            direction,
            distance,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = distance;
        bool blocked = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || IsDialogueNpcCollider(hitCollider.transform) || IsLocalPlayerCollider(hitCollider.transform))
                continue;

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                blocked = true;
            }
        }

        if (!blocked)
            return desiredPosition;

        return origin + direction * Mathf.Max(0.05f, nearestDistance - collisionPadding);
    }

    private bool IsDialogueNpcCollider(Transform candidate)
    {
        return dialogueNpc != null && (candidate == dialogueNpc || candidate.IsChildOf(dialogueNpc));
    }

    private bool IsLocalPlayerCollider(Transform candidate)
    {
        // The cast runs from the NPC toward the camera. At close interaction
        // distances it naturally intersects the local player's capsule, which
        // is not an environmental obstruction and must not pull the camera
        // toward the NPC into an extreme close-up.
        return candidate == transform || candidate.IsChildOf(transform);
    }

    private bool HasValidDialogueTarget()
    {
        return dialogueNpc != null && dialogueNpc.gameObject.activeInHierarchy;
    }

    private Vector3 ResolveTargetPosition(Transform npc)
    {
        DialogueCameraTarget explicitTarget = npc.GetComponentInChildren<DialogueCameraTarget>(true);
        if (explicitTarget != null && explicitTarget.ResolvedTarget != null)
            return explicitTarget.ResolvedTarget.position + Vector3.up * explicitTarget.verticalOffset;

        Transform head = FindHeadBone(npc);
        if (head != null)
            return head.position;

        Renderer[] renderers = npc.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(npc.position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        if (hasBounds)
        {
            // No reliable head bone is present on the current NPC meshes. Use
            // a normalized upper-body anchor instead of the bounds top, which
            // keeps head/shoulders/upper torso framed across different heights.
            float upperBodyAnchor = Mathf.Lerp(bounds.min.y, bounds.max.y, 0.68f);
            return new Vector3(bounds.center.x, upperBodyAnchor, bounds.center.z);
        }

        return npc.position + Vector3.up * 1.6f;
    }

    private Transform FindHeadBone(Transform root)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            string name = children[i].name.ToLowerInvariant();
            if (name == "head" || name.EndsWith("head") || name.Contains("head"))
                return children[i];
        }

        return null;
    }

    private void SetPose(Vector3 position, Quaternion rotation, float fov)
    {
        playerCamera.transform.position = position;
        playerCamera.transform.rotation = rotation;
        playerCamera.fieldOfView = fov;
    }

    private bool IsLocallyOwned()
    {
        NetworkPlayer networkPlayer = GetComponent<NetworkPlayer>();
        return networkPlayer == null || networkPlayer.isLocalPlayer;
    }

    private void ApplyLocalPlayerVisibility()
    {
        if (localVisualVisibilityApplied || !IsLocallyOwned())
            return;

        localDialogueRenderers.Clear();
        localDialogueRendererStates.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null ||
                (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)))
                continue;

            localDialogueRenderers.Add(renderer);
            localDialogueRendererStates.Add(renderer.enabled);
        }

        // Keep compatibility with an explicitly assigned legacy renderer even
        // if it is outside the current hierarchy.
        if (localPlayerBodyRenderer != null && !localDialogueRenderers.Contains(localPlayerBodyRenderer))
        {
            localDialogueRenderers.Add(localPlayerBodyRenderer);
            localDialogueRendererStates.Add(localPlayerBodyRenderer.enabled);
        }

        for (int i = 0; i < localDialogueRenderers.Count; i++)
            localDialogueRenderers[i].enabled = false;

        // Renderer-only and local-only: Player, CharacterController,
        // colliders, Rigidbody, NetworkPlayer and gameplay scripts stay enabled.
        localVisualVisibilityApplied = true;
    }

    private void RestoreLocalPlayerVisibility()
    {
        if (!localVisualVisibilityApplied)
            return;

        for (int i = 0; i < localDialogueRenderers.Count; i++)
        {
            Renderer renderer = localDialogueRenderers[i];
            if (renderer != null)
                renderer.enabled = localDialogueRendererStates[i];
        }

        localDialogueRenderers.Clear();
        localDialogueRendererStates.Clear();
        localVisualVisibilityApplied = false;
    }

    private void OnDisable()
    {
        // Disabling this component does not disable the Player hierarchy, so
        // the camera can be restored immediately and safely. A hierarchy
        // disable is different: Unity may reject reparenting while the Player
        // is being deactivated, so defer that restore until OnEnable.
        if (gameplayStateSaved && gameObject.activeInHierarchy)
            RestoreGameplayState();
        else if (gameplayStateSaved)
        {
            // The Player hierarchy is currently being disabled. Reparenting a
            // child during that operation is invalid; finish on OnEnable.
            if (playerController != null)
                playerController.SetDialogueInputLocked(false);
            gameplayRestorePending = true;
        }
    }

    private void OnDestroy()
    {
        RestoreLocalPlayerVisibility();

        if (gameplayStateSaved && playerController != null)
            playerController.SetDialogueInputLocked(false);
    }

}
