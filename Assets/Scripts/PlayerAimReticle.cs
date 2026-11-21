using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates a small screen-centred aiming pointer for both camera modes.
/// It uses a dedicated screen overlay Canvas so the dot stays fixed to the
/// display centre while the gameplay camera orbits in third person.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("HighStakes/UI/Player Aim Reticle")]
public sealed class PlayerAimReticle : MonoBehaviour
{
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField, Min(1f)] private float size = 8f;
    [SerializeField, Min(0f)] private float outlineSize = 2f;

    private DialogueCameraController dialogueCameraController;
    private PlayerCameraModeSwitcher cameraModeSwitcher;
    private PauseMenuController pauseMenuController;
    private GameObject reticleCanvasObject;
    private Canvas reticleCanvas;
    private Texture2D reticleTexture;
    private Sprite reticleSprite;

    private void Awake()
    {
        size = Mathf.Max(size, 8f);
        outlineSize = Mathf.Max(outlineSize, 2f);
        dialogueCameraController = GetComponent<DialogueCameraController>();
        cameraModeSwitcher = GetComponent<PlayerCameraModeSwitcher>();
        pauseMenuController = FindAnyObjectByType<PauseMenuController>();
        CreateReticleCanvas();
    }

    private void LateUpdate()
    {
        if (reticleCanvas == null)
            return;

        if (pauseMenuController == null)
            pauseMenuController = FindAnyObjectByType<PauseMenuController>();

        // The feature bootstrap can add this component before it adds the
        // camera mode switcher. Resolve it lazily so third-person mode is
        // recognised even when both components are created at runtime.
        if (cameraModeSwitcher == null)
            cameraModeSwitcher = GetComponent<PlayerCameraModeSwitcher>();

        bool inDialogue = dialogueCameraController != null && dialogueCameraController.IsInDialogue;
        bool isThirdPerson = cameraModeSwitcher != null && cameraModeSwitcher.IsThirdPerson;
        bool isPaused = pauseMenuController != null && pauseMenuController.IsPaused;
        reticleCanvas.enabled = !inDialogue && !isPaused && !isThirdPerson;
    }

    private void OnDestroy()
    {
        if (reticleSprite != null)
            Destroy(reticleSprite);
        if (reticleTexture != null)
            Destroy(reticleTexture);
    }

    private void CreateReticleCanvas()
    {
        reticleCanvasObject = new GameObject("PlayerReticleCanvas", typeof(RectTransform), typeof(Canvas));
        reticleCanvasObject.transform.SetParent(transform, false);
        reticleCanvas = reticleCanvasObject.GetComponent<Canvas>();
        // Do not bind the reticle to the gameplay camera. The camera is an
        // orbiting child of the player in third person, and a camera-space
        // canvas that is also parented to the player can inherit that motion
        // and make the pointer drift or render at the wrong scale.
        reticleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        reticleCanvas.overrideSorting = true;
        reticleCanvas.sortingOrder = 30000;
        reticleCanvas.pixelPerfect = true;

        CanvasScaler scaler = reticleCanvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        reticleTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "PlayerReticleTexture",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point
        };
        reticleTexture.SetPixel(0, 0, Color.white);
        reticleTexture.Apply();
        reticleSprite = Sprite.Create(reticleTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        reticleSprite.name = "PlayerReticleSprite";
        reticleSprite.hideFlags = HideFlags.HideAndDontSave;

        Image outline = CreateReticleImage("PlayerReticleOutline", size + outlineSize * 2f, new Color(0f, 0f, 0f, color.a * 0.85f));
        outline.transform.SetParent(reticleCanvasObject.transform, false);
        Image dot = CreateReticleImage("PlayerReticleDot", size, color);
        dot.transform.SetParent(reticleCanvasObject.transform, false);
    }

    private Image CreateReticleImage(string objectName, float imageSize, Color imageColor)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        Image image = imageObject.GetComponent<Image>();
        image.sprite = reticleSprite;
        image.color = imageColor;
        image.raycastTarget = false;

        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(imageSize, imageSize);
        return image;
    }
}
