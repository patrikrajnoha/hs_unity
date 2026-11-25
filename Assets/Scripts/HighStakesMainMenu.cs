using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Self-contained main menu for the lightweight Menu scene.</summary>
public sealed class HighStakesMainMenu : MonoBehaviour
{
    private static readonly Color Ink = new Color32(8, 13, 25, 255);
    private static readonly Color Panel = new Color32(23, 37, 62, 248);
    private static readonly Color Accent = new Color32(96, 213, 232, 255);
    private static readonly Color Warm = new Color32(255, 194, 92, 255);
    private static readonly Color Muted = new Color32(157, 173, 198, 255);
    private static readonly Color Danger = new Color32(239, 103, 120, 255);

    private RectTransform startButtonRect;
    private GameObject settingsPanel;
    private TMP_Text titleLabel;
    private TMP_Text subtitleLabel;
    private TMP_Text statusLabel;
    private TMP_Text footerLabel;
    private TMP_Text settingsButtonLabel;
    private TMP_Text settingsTitleLabel;
    private TMP_Text languageLabel;
    private TMP_Text backLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Menu" || FindAnyObjectByType<HighStakesMainMenu>() != null)
            return;

        new GameObject("HighStakes Main Menu").AddComponent<HighStakesMainMenu>();
    }

    private void Awake()
    {
        LocalizationManager.EnsureLoaded();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Build();
    }

    private void OnEnable()
    {
        LocalizationManager.LanguageChanged += UpdateLocalizedUi;
    }

    private void OnDestroy()
    {
        LocalizationManager.LanguageChanged -= UpdateLocalizedUi;
    }

    private void Build()
    {
        EnsureEventSystem();

        if (Camera.allCameras.Length == 0)
        {
            GameObject cameraObject = new GameObject("Main Menu Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;
            camera.depth = -10f;
        }

        GameObject canvasObject = CreateUiObject("Main Menu Canvas", transform);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject backdrop = CreateUiObject("Main Menu Backdrop", canvasObject.transform);
        SetFullScreen(backdrop.GetComponent<RectTransform>());
        Image backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = new Color(Ink.r, Ink.g, Ink.b, 0.98f);
        backdropImage.raycastTarget = false;

        GameObject card = CreateUiObject("Main Menu Panel", canvasObject.transform);
        Anchor(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -10f), new Vector2(650f, 620f));
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = Panel;
        AddOutline(card, new Color(Accent.r, Accent.g, Accent.b, 0.55f));

        GameObject accent = CreateUiObject("Panel Accent", card.transform);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.offsetMin = new Vector2(24f, -5f);
        accentRect.offsetMax = new Vector2(-24f, -2f);
        accent.AddComponent<Image>().color = Accent;

        titleLabel = CreateLabel("Title", card.transform,
            LocalizationManager.GetOrFallback("ui.city.brand", "HIGHSTAKES"), 58f, TextAlignmentOptions.Center);
        Anchor(titleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -68f), new Vector2(580f, 80f));
        titleLabel.color = Accent;
        titleLabel.fontStyle = FontStyles.Bold;
        titleLabel.characterSpacing = 6f;

        subtitleLabel = CreateLabel("Subtitle", card.transform,
            LocalizationManager.GetOrFallback("ui.menu.subtitle", "OLD QUARTER  /  UNDERGROUND CONTRACTS"), 15f,
            TextAlignmentOptions.Center);
        Anchor(subtitleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -142f), new Vector2(580f, 30f));
        subtitleLabel.color = Muted;
        subtitleLabel.characterSpacing = 2f;

        statusLabel = CreateLabel("Status", card.transform,
            LocalizationManager.GetOrFallback("ui.menu.status", "LOCAL MODE READY\nA new run starts in the Old Quarter."), 16f,
            TextAlignmentOptions.Center);
        Anchor(statusLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 56f), new Vector2(500f, 68f));
        statusLabel.color = Muted;

        Button startButton = CreateButton("StartButton", card.transform,
            LocalizationManager.GetOrFallback("ui.menu.start", "START RUN"), new Vector2(0f, -54f),
            new Vector2(390f, 64f), Warm);
        startButtonRect = startButton.GetComponent<RectTransform>();
        startButton.onClick.AddListener(StartGame);

        Button quitButton = CreateButton("QuitButton", card.transform,
            LocalizationManager.GetOrFallback("ui.menu.quit", "QUIT TO DESKTOP"), new Vector2(0f, -140f),
            new Vector2(390f, 56f), Danger);
        quitButton.onClick.AddListener(QuitGame);

        Button settingsButton = CreateButton("SettingsButton", card.transform,
            LocalizationManager.GetOrFallback("ui.settings.title", "SETTINGS"), new Vector2(0f, -220f),
            new Vector2(390f, 56f), Muted);
        settingsButtonLabel = settingsButton.GetComponentInChildren<TMP_Text>(true);
        settingsButton.onClick.AddListener(OpenSettings);

        footerLabel = CreateLabel("Footer", card.transform,
            LocalizationManager.GetOrFallback("ui.menu.footer", "WASD MOVE  •  E INTERACT  •  ESC PAUSE"), 11f,
            TextAlignmentOptions.Center);
        Anchor(footerLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 22f), new Vector2(560f, 24f));
        footerLabel.color = Muted;

        BuildSettingsUi(canvasObject.transform);
        UpdateLocalizedUi();
    }

    private void BuildSettingsUi(Transform parent)
    {
        settingsPanel = CreateUiObject("Main Menu Settings Panel", parent);
        SetFullScreen(settingsPanel.GetComponent<RectTransform>());
        Image overlay = settingsPanel.AddComponent<Image>();
        overlay.color = new Color(Ink.r, Ink.g, Ink.b, 0.94f);
        overlay.raycastTarget = true;

        GameObject card = CreateUiObject("Main Menu Settings Card", settingsPanel.transform);
        Anchor(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(520f, 600f));
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = Panel;
        AddOutline(card, new Color(Accent.r, Accent.g, Accent.b, 0.55f));

        settingsTitleLabel = CreateLabel("SettingsTitle", card.transform,
            LocalizationManager.GetOrFallback("ui.settings.title", "SETTINGS"), 30f, TextAlignmentOptions.Center);
        Anchor(settingsTitleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -42f), new Vector2(460f, 48f));
        settingsTitleLabel.color = Accent;

        Button languageButton = CreateButton("LanguageButton", card.transform, "", new Vector2(0f, 25f),
            new Vector2(390f, 64f), Accent);
        languageLabel = languageButton.GetComponentInChildren<TMP_Text>(true);
        languageButton.onClick.AddListener(ToggleLanguage);
        Anchor(languageButton.GetComponent<RectTransform>(), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -115), new Vector2(390, 54));
        AudioSettingsPanel.Create(card.transform, -165);

        Button backButton = CreateButton("BackButton", card.transform,
            LocalizationManager.GetOrFallback("ui.settings.back", "BACK"), new Vector2(0f, -75f),
            new Vector2(390f, 56f), Muted);
        Anchor(backButton.GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 55), new Vector2(390, 50));
        backLabel = backButton.GetComponentInChildren<TMP_Text>(true);
        backButton.onClick.AddListener(CloseSettings);

        settingsPanel.SetActive(false);
    }

    private void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    private void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void ToggleLanguage()
    {
        LocalizationManager.ToggleLanguage();
    }

    private void UpdateLocalizedUi()
    {
        if (titleLabel != null)
            titleLabel.text = LocalizationManager.GetOrFallback("ui.city.brand", "HIGHSTAKES");
        if (subtitleLabel != null)
            subtitleLabel.text = LocalizationManager.GetOrFallback("ui.menu.subtitle", "OLD QUARTER  /  UNDERGROUND CONTRACTS");
        if (statusLabel != null)
            statusLabel.text = LocalizationManager.GetOrFallback("ui.menu.status", "LOCAL MODE READY\nA new run starts in the Old Quarter.");
        if (footerLabel != null)
            footerLabel.text = LocalizationManager.GetOrFallback("ui.menu.footer", "WASD MOVE  •  E INTERACT  •  ESC PAUSE");
        if (settingsButtonLabel != null)
            settingsButtonLabel.text = LocalizationManager.GetOrFallback("ui.settings.title", "SETTINGS");
        if (settingsTitleLabel != null)
            settingsTitleLabel.text = LocalizationManager.GetOrFallback("ui.settings.title", "SETTINGS");
        if (backLabel != null)
            backLabel.text = LocalizationManager.GetOrFallback("ui.settings.back", "BACK");
        if (languageLabel != null)
        {
            string languageName = LocalizationManager.CurrentLanguage == LocalizationManager.Slovak
                ? LocalizationManager.GetOrFallback("ui.language.slovak", "SLOVAK")
                : LocalizationManager.GetOrFallback("ui.language.english", "ENGLISH");
            languageLabel.text = LocalizationManager.GetOrFallback("ui.settings.language", "LANGUAGE") + ": " + languageName;
        }
    }

    private void Update()
    {
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseSettings();
            return;
        }
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
        {
            StartGame();
            return;
        }

        // Keep the menu clickable even when the editor's Game view did not forward
        // the pointer event to the UI module after a domain reload.
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && startButtonRect != null &&
            RectTransformUtility.RectangleContainsScreenPoint(startButtonRect,
                Mouse.current.position.ReadValue(), null))
        {
            StartGame();
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("Main Menu EventSystem", typeof(EventSystem));
        eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static void StartGame()
    {
        GameLogger.LogInfo("game", "start_campaign", "Main menu: Starting OldQuarter game");
        Time.timeScale = 1f;
        SceneManager.LoadScene("OldQuarter");
    }

    private static void QuitGame()
    {
        GameLogger.LogInfo("game", "quit_game", "Main menu: Quit selected");
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject objectRoot = new GameObject(name, typeof(RectTransform));
        objectRoot.transform.SetParent(parent, false);
        return objectRoot;
    }

    private static TMP_Text CreateLabel(string name, Transform parent, string value, float size,
        TextAlignmentOptions alignment)
    {
        GameObject objectRoot = CreateUiObject(name, parent);
        TextMeshProUGUI text = objectRoot.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(10f, size * 0.6f);
        text.fontSizeMax = size;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 position,
        Vector2 size, Color accent)
    {
        GameObject objectRoot = CreateUiObject(name, parent);
        RectTransform rect = objectRoot.GetComponent<RectTransform>();
        Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        Image image = objectRoot.AddComponent<Image>();
        image.color = accent;
        Button button = objectRoot.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => GameAudio.Play(AudioCue.Click));
        ColorBlock colors = button.colors;
        colors.normalColor = accent;
        colors.highlightedColor = Color.Lerp(accent, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(accent, Ink, 0.28f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        AddOutline(objectRoot, new Color(1f, 1f, 1f, 0.15f));

        TMP_Text text = CreateLabel(name + " Label", objectRoot.transform, label, 19f,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.color = Ink;
        text.fontStyle = FontStyles.Bold;
        return button;
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetFullScreen(RectTransform rect)
    {
        Stretch(rect);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void AddOutline(GameObject objectRoot, Color color)
    {
        Outline outline = objectRoot.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
    }
}
