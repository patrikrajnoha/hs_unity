using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space cinematic bars for local NPC dialogue.
///
/// The overlay uses a reference-resolution CanvasScaler, so the default
/// 138-unit bars render as approximately 138 pixels at 1920x1080 and scale
/// proportionally with other 16:9 resolutions. It never changes the camera
/// projection or the actual game resolution.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("HighStakes/UI/Dialogue Letterbox")]
public sealed class DialogueLetterbox : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    public static DialogueLetterbox Instance { get; private set; }

    [Header("Letterbox layout")]
    [Tooltip("Bar height in CanvasScaler reference units. 138 at 1920x1080 produces approximately a 2.39:1 active image area.")]
    [Min(0f)]
    [SerializeField] private float barHeightAtReferenceResolution = 138f;

    [Tooltip("The overlay's reference resolution. Keep this at 1920x1080 for the default 16:9 scaling behavior.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);

    [SerializeField] private Color barColor = new Color32(5, 10, 19, 238);

    [Header("Animation")]
    [Min(0f)]
    [SerializeField] private float animationDuration = 0.45f;

    [Tooltip("Easing applied to both the reveal and hide animations.")]
    [SerializeField] private AnimationCurve easing = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 1.8f),
        new Keyframe(1f, 1f, 1.8f, 0f));

    private Canvas canvas;
    private RectTransform topBar;
    private RectTransform bottomBar;
    private Coroutine animationRoutine;
    private bool barsVisible;
    private bool dialogueRequestActive;

    public bool IsVisible => barsVisible;

    /// <summary>
    /// Gets the shared overlay, creating it lazily so existing dialogue
    /// prefabs do not need a scene reference.
    /// </summary>
    public static DialogueLetterbox GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        DialogueLetterbox existing = FindFirstObjectByType<DialogueLetterbox>();
        if (existing != null)
            return existing;

        GameObject overlay = new GameObject("Dialogue Letterbox");
        return overlay.AddComponent<DialogueLetterbox>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    /// <summary>
    /// Animates the bars into view. Repeated calls while this dialogue request
    /// is active are ignored, so a second interaction cannot restart the
    /// animation or create another overlay.
    /// </summary>
    public void ShowLetterbox()
    {
        if (dialogueRequestActive)
            return;

        dialogueRequestActive = true;
        StartBarAnimation(true);
    }

    /// <summary>
    /// Animates the bars out of view. Calling it more than once is harmless.
    /// </summary>
    public void HideLetterbox()
    {
        if (!dialogueRequestActive)
            return;

        dialogueRequestActive = false;
        StartBarAnimation(false);
    }

    private void BuildOverlay()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = ResolveDialogueCamera();
        canvas.planeDistance = 0.4f;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        topBar = CreateBar("Top Bar", true);
        bottomBar = CreateBar("Bottom Bar", false);

        SetBarProgress(0f);
        canvas.enabled = false;
    }

    private Camera ResolveDialogueCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
            return mainCamera;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.isActiveAndEnabled)
                return camera;
        }

        return null;
    }

    private RectTransform CreateBar(string objectName, bool top)
    {
        GameObject barObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barObject.transform.SetParent(transform, false);

        RectTransform rect = barObject.GetComponent<RectTransform>();
        rect.anchorMin = top ? new Vector2(0f, 1f) : Vector2.zero;
        rect.anchorMax = top ? Vector2.one : new Vector2(1f, 0f);
        rect.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 0f);

        Image image = barObject.GetComponent<Image>();
        image.color = barColor;
        image.raycastTarget = false;
        return rect;
    }

    private void StartBarAnimation(bool show)
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        if (canvas != null)
            canvas.worldCamera = ResolveDialogueCamera();

        if (show)
            canvas.enabled = true;

        animationRoutine = StartCoroutine(AnimateBars(show));
    }

    private IEnumerator AnimateBars(bool show)
    {
        float start = GetBarProgress();
        float target = show ? 1f : 0f;
        float duration = Mathf.Max(0f, animationDuration);

        if (duration <= 0f)
        {
            SetBarProgress(target);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float easedTime = easing != null ? easing.Evaluate(normalizedTime) : Mathf.SmoothStep(0f, 1f, normalizedTime);
                SetBarProgress(Mathf.LerpUnclamped(start, target, easedTime));
                yield return null;
            }

            SetBarProgress(target);
        }

        animationRoutine = null;
        barsVisible = show;

        if (!show && !dialogueRequestActive)
            canvas.enabled = false;
    }

    private void SetBarProgress(float progress)
    {
        float height = Mathf.Max(0f, barHeightAtReferenceResolution) * Mathf.Clamp01(progress);
        topBar.sizeDelta = new Vector2(0f, height);
        bottomBar.sizeDelta = new Vector2(0f, height);
    }

    private float GetBarProgress()
    {
        if (barHeightAtReferenceResolution <= 0f)
            return 0f;

        return Mathf.Clamp01(topBar.sizeDelta.y / barHeightAtReferenceResolution);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
