using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates and owns a local circular minimap. The map is a real top-down
/// RenderTexture view; the UI is generated once and follows screen resizing via
/// CanvasScaler anchors.
/// </summary>
public sealed class MinimapController : MonoBehaviour
{
    private const string VisibilityPreferenceKey = "HighStakes.MinimapVisible";

    [Header("Map")]
    [SerializeField, Min(8f)] private float worldViewSize = 36f;
    [SerializeField, Min(128)] private int renderResolution = 512;
    [SerializeField, Min(5f)] private float cameraHeight = 60f;
    [SerializeField] private LayerMask minimapLayers = ~0;
    [SerializeField] private bool rotateMapWithPlayer;

    [Header("UI")]
    [SerializeField, Min(120f)] private float minimapPixels = 300f;
    [SerializeField] private Vector2 screenOffset = new Vector2(-32f, 32f);

    private Camera minimapCamera;
    private RenderTexture renderTexture;
    private Canvas canvas;
    private RectTransform iconRoot;
    private RectTransform mapRoot;
    private readonly List<Image> policeIcons = new List<Image>();
    private Sprite circleSprite;
    private Sprite triangleSprite;
    private Sprite policeSprite;
    private Sprite terrainPatternSprite;
    private PoliceHeatController heatController;
    private bool initialized;
    private bool minimapVisible = true;

    public bool IsVisible => minimapVisible;

    private void Awake()
    {
        NetworkPlayer networkPlayer = GetComponent<NetworkPlayer>();
        if (networkPlayer != null && !networkPlayer.isLocalPlayer)
        {
            enabled = false;
            return;
        }

        heatController = GetComponent<PoliceHeatController>();
        if (PlayerPrefs.HasKey(VisibilityPreferenceKey))
            minimapVisible = PlayerPrefs.GetInt(VisibilityPreferenceKey, 1) != 0;
    }

    private void Start()
    {
        if (heatController == null)
            heatController = GetComponent<PoliceHeatController>();

        BuildMinimap();
        ApplyVisibility();
    }

    public void SetVisible(bool visible)
    {
        minimapVisible = visible;
        PlayerPrefs.SetInt(VisibilityPreferenceKey, visible ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (canvas != null)
            canvas.gameObject.SetActive(minimapVisible);
    }

    private void LateUpdate()
    {
        if (!initialized || minimapCamera == null)
            return;

        Vector3 position = transform.position;
        minimapCamera.transform.position = position + Vector3.up * cameraHeight;
        minimapCamera.transform.rotation = rotateMapWithPlayer
            ? Quaternion.Euler(90f, transform.eulerAngles.y, 0f)
            : Quaternion.Euler(90f, 0f, 0f);

        UpdatePlayerIcon();
        UpdatePoliceIcons();
    }

    private void BuildMinimap()
    {
        if (initialized)
            return;

        circleSprite = CreateCircleSprite(256, 0.48f, 0.03f);
        triangleSprite = CreateOutlinedTriangleSprite(64);
        policeSprite = CreatePoliceDotSprite(64);
        terrainPatternSprite = CreateTerrainPatternSprite(256);

        renderTexture = new RenderTexture(renderResolution, renderResolution, 16, RenderTextureFormat.ARGB32)
        {
            name = "HighStakes_Minimap_RT",
            useMipMap = false,
            autoGenerateMips = false,
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear
        };
        renderTexture.Create();

        GameObject cameraObject = new GameObject("HighStakes_MinimapCamera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        minimapCamera = cameraObject.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = worldViewSize * 0.5f;
        minimapCamera.nearClipPlane = 0.1f;
        minimapCamera.farClipPlane = cameraHeight + 100f;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.035f, 0.17f, 0.055f, 1f);
        minimapCamera.cullingMask = minimapLayers.value & ~(1 << 5);
        minimapCamera.targetTexture = renderTexture;
        minimapCamera.enabled = true;

        canvas = CreateCanvas();
        GameObject container = CreateUiObject("MinimapContainer", canvas.transform);
        RectTransform containerRect = container.GetComponent<RectTransform>();
        // Bottom-right anchored so the widget stays there at every resolution.
        containerRect.anchorMin = new Vector2(1f, 0f);
        containerRect.anchorMax = new Vector2(1f, 0f);
        containerRect.pivot = new Vector2(1f, 0f);
        containerRect.anchoredPosition = new Vector2(screenOffset.x, screenOffset.y);
        containerRect.sizeDelta = Vector2.one * minimapPixels;

        GameObject shadow = CreateUiObject("MinimapShadow", container.transform);
        Image shadowImage = shadow.AddComponent<Image>();
        shadowImage.sprite = circleSprite;
        shadowImage.color = new Color(0f, 0f, 0f, 0.42f);
        shadowImage.raycastTarget = false;
        RectTransform shadowRect = shadow.GetComponent<RectTransform>();
        Stretch(shadowRect, -5f);

        GameObject plate = CreateUiObject("MinimapPlate", container.transform);
        Image plateImage = plate.AddComponent<Image>();
        plateImage.sprite = circleSprite;
        plateImage.color = new Color(0.02f, 0.10f, 0.035f, 0.98f);
        plateImage.raycastTarget = false;
        Stretch(plate.GetComponent<RectTransform>(), 7f);

        GameObject mapMaskObject = CreateUiObject("CircularMapMask", container.transform);
        Image maskImage = mapMaskObject.AddComponent<Image>();
        maskImage.sprite = circleSprite;
        maskImage.color = Color.white;
        Mask mask = mapMaskObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        RectTransform mapMaskRect = mapMaskObject.GetComponent<RectTransform>();
        Stretch(mapMaskRect, 22f);

        GameObject mapObject = CreateUiObject("MapView", mapMaskObject.transform);
        RawImage rawImage = mapObject.AddComponent<RawImage>();
        rawImage.texture = renderTexture;
        rawImage.color = new Color(0.52f, 0.88f, 0.56f, 0.94f);
        rawImage.raycastTarget = false;
        Stretch(rawImage.rectTransform, 0f);
        mapRoot = rawImage.rectTransform;

        GameObject mapOverlay = CreateUiObject("MapPattern", mapMaskObject.transform);
        Image overlayImage = mapOverlay.AddComponent<Image>();
        overlayImage.sprite = terrainPatternSprite;
        overlayImage.color = new Color(0.08f, 0.32f, 0.10f, 0.48f);
        overlayImage.raycastTarget = false;
        Stretch(mapOverlay.GetComponent<RectTransform>(), 0f);

        GameObject iconLayer = CreateUiObject("MapIcons", mapMaskObject.transform);
        iconRoot = iconLayer.GetComponent<RectTransform>();
        Stretch(iconRoot, 0f);

        GameObject playerIcon = CreateUiObject("PlayerArrow", iconLayer.transform);
        Image playerImage = playerIcon.AddComponent<Image>();
        playerImage.sprite = triangleSprite;
        playerImage.color = Color.white;
        playerImage.raycastTarget = false;
        RectTransform playerRect = playerIcon.GetComponent<RectTransform>();
        playerRect.anchorMin = new Vector2(0.5f, 0.5f);
        playerRect.anchorMax = new Vector2(0.5f, 0.5f);
        playerRect.pivot = new Vector2(0.5f, 0.5f);
        playerRect.sizeDelta = Vector2.one * 18f;

        GameObject frameObject = CreateUiObject("MinimapMetalFrame", container.transform);
        MinimapFrameGraphic frameGraphic = frameObject.AddComponent<MinimapFrameGraphic>();
        frameGraphic.raycastTarget = false;
        Stretch(frameObject.GetComponent<RectTransform>(), 0f);

        PoliceHeatHUD heatHud = container.AddComponent<PoliceHeatHUD>();
        heatHud.Configure(heatController, frameGraphic);

        initialized = true;
    }

    private void UpdatePlayerIcon()
    {
        if (iconRoot == null || iconRoot.childCount == 0)
            return;

        RectTransform playerIcon = iconRoot.GetChild(0) as RectTransform;
        if (playerIcon == null)
            return;

        playerIcon.localRotation = Quaternion.Euler(0f, 0f, -transform.eulerAngles.y);
    }

    private void UpdatePoliceIcons()
    {
        int index = 0;
        foreach (PoliceBehavior police in PoliceHeatRegistry.ActivePolice)
        {
            if (police == null || police.gameObject == gameObject)
                continue;

            Image icon = GetPoliceIcon(index++);
            Vector3 delta = police.transform.position - transform.position;
            delta.y = 0f;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            float radius = worldViewSize * 0.5f;
            bool visible = distance <= radius;
            icon.enabled = visible;
            if (!visible)
                continue;

            float scale = minimapPixels - 48f;
            icon.rectTransform.anchoredPosition = new Vector2(delta.x / radius, delta.z / radius) * (scale * 0.5f);
            icon.rectTransform.localRotation = Quaternion.identity;
        }

        for (int i = index; i < policeIcons.Count; i++)
            policeIcons[i].enabled = false;
    }

    private Image GetPoliceIcon(int index)
    {
        while (policeIcons.Count <= index)
        {
            GameObject iconObject = CreateUiObject("PoliceIcon", iconRoot);
            Image image = iconObject.AddComponent<Image>();
            image.sprite = policeSprite;
            image.color = Color.white;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * 11f;
            policeIcons.Add(image);
        }

        return policeIcons[index];
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("HighStakesHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(null, false);
        // Keep the runtime HUD visible in the Hierarchy while playing so the
        // generated minimap can be inspected without turning it into a scene
        // asset or leaving generated objects behind after Play Mode.
        canvasObject.hideFlags = HideFlags.DontSave;
        Canvas result = canvasObject.GetComponent<Canvas>();
        result.renderMode = RenderMode.ScreenSpaceOverlay;
        result.sortingOrder = 2000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return result;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject value = new GameObject(name, typeof(RectTransform));
        value.transform.SetParent(parent, false);
        return value;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

    private static Sprite CreateCircleSprite(int size, float radius, float feather)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        FillTexture(texture, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(size - 1, size - 1) * 0.5f) / (size * 0.5f);
            return Mathf.Clamp01((radius + feather - d) / feather);
        });
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
    }

    private static Sprite CreateRingSprite(int size, float inner, float outer)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        FillTexture(texture, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(size - 1, size - 1) * 0.5f) / (size * 0.5f);
            float outerAlpha = Mathf.Clamp01((outer - d) * size * 0.08f);
            float innerAlpha = Mathf.Clamp01((d - inner) * size * 0.08f);
            return Mathf.Min(outerAlpha, innerAlpha);
        });
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
    }

    private static Sprite CreateOutlinedTriangleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size;
                float ny = (y + 0.5f) / size;
                bool outer = IsTrianglePixel(nx, ny, 0.44f);
                bool inner = IsTrianglePixel(nx, ny, 0.30f);
                pixels[y * size + x] = inner
                    ? Color.white
                    : outer ? new Color(0.015f, 0.02f, 0.015f, 1f) : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
    }

    private static bool IsTrianglePixel(float normalizedX, float normalizedY, float halfWidth)
    {
        if (normalizedY < 0.08f || normalizedY > 0.92f)
            return false;

        float width = Mathf.Lerp(0.02f, halfWidth, normalizedY);
        return Mathf.Abs(normalizedX - 0.5f) <= width;
    }

    private static Sprite CreatePoliceDotSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = (y + 0.5f) / size - 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                pixels[y * size + x] = distance < 0.27f
                    ? new Color(0.03f, 0.25f, 0.78f, 1f)
                    : distance < 0.36f
                        ? new Color(0.015f, 0.02f, 0.02f, 1f)
                        : distance < 0.50f
                            ? new Color(0.88f, 0.93f, 0.82f, 0.95f)
                            : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
    }

    private static Sprite CreateTerrainPatternSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool diagonal = ((x + y) / 18) % 2 == 0;
                bool block = ((x / 32) + (y / 32)) % 3 == 0;
                pixels[y * size + x] = diagonal
                    ? new Color(0.06f, 0.25f, 0.08f, block ? 0.32f : 0.18f)
                    : new Color(0.40f, 0.75f, 0.32f, 0.08f);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
    }

    private static void FillTexture(Texture2D texture, System.Func<int, int, float> alpha)
    {
        int size = texture.width;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float value = Mathf.Clamp01(alpha(x, y));
                pixels[y * size + x] = new Color(1f, 1f, 1f, value);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
    }

    private void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);

        if (minimapCamera != null)
            Destroy(minimapCamera.gameObject);

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
