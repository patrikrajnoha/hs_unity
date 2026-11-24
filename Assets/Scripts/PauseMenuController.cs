using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [Header("Pause menu UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    private Button settingsButton;
    private Button newCampaignButton;
    private Button languageButton;
    private Button minimapToggleButton;
    private Button closeSettingsButton;
    private GameObject settingsPanel;
    private TMP_Text pauseTitleLabel;
    private TMP_Text resumeLabel;
    private TMP_Text mainMenuLabel;
    private TMP_Text quitLabel;
    private TMP_Text settingsButtonLabel;
    private TMP_Text settingsTitleLabel;
    private TMP_Text languageLabel;
    private TMP_Text minimapToggleLabel;
    private TMP_Text closeSettingsLabel;
    private TMP_Text campaignStatsLabel;
    private MinimapController minimapController;
    private HighStakes.City.CityHud cityHud;
    private HighStakes.City.CityMissionDirector cityMissionDirector;

    private bool isPaused;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        LocalizationManager.EnsureLoaded();
        if (pausePanel == null)
            BuildRuntimePauseUi();
        SetPaused(false);
    }

    private void Start()
    {
        pauseTitleLabel = FindLabel(pauseTitleLabel, "PauseTitle");
        resumeLabel = FindLabel(resumeLabel, resumeButton);
        mainMenuLabel = FindLabel(mainMenuLabel, mainMenuButton);
        quitLabel = FindLabel(quitLabel, quitButton);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        BuildSettingsUi();
        LocalizationManager.LanguageChanged += UpdateLocalizedUi;
        UpdateLocalizedUi();
    }

    private void Update()
    {
        if (GameMaster.GameOverActive)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (cityMissionDirector == null)
                cityMissionDirector = FindAnyObjectByType<HighStakes.City.CityMissionDirector>();

            if (cityMissionDirector != null && cityMissionDirector.HandleEscape())
                return;

            TogglePause();
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;

        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(ResumeGame);

        if (mainMenuButton != null)
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OpenSettings);
        if (newCampaignButton != null) newCampaignButton.onClick.RemoveAllListeners();
        if (languageButton != null)
            languageButton.onClick.RemoveListener(ToggleLanguage);
        if (minimapToggleButton != null)
            minimapToggleButton.onClick.RemoveListener(ToggleMinimap);
        if (closeSettingsButton != null)
            closeSettingsButton.onClick.RemoveListener(CloseSettings);

        LocalizationManager.LanguageChanged -= UpdateLocalizedUi;
    }

    public void TogglePause()
    {
        SetPaused(!isPaused);
    }

    public void ResumeGame()
    {
        GameLogger.LogInfo("gameplay", "pause_resumed", "Game resumed from pause menu");
        SetPaused(false);
    }

    public void ReturnToMainMenu()
    {
        GameLogger.LogInfo("gameplay", "return_to_main_menu", "Returning to Menu from pause menu");
        SetPaused(false);
        SceneManager.LoadScene("Menu");
    }

    public void QuitGame()
    {
        GameLogger.LogInfo("gameplay", "quit_game", "Quitting game from pause menu");
        SetPaused(false);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetPaused(bool paused)
    {
        if (paused == isPaused && pausePanel != null && pausePanel.activeSelf == paused)
            return;

        if (paused)
        {
            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            CloseSettings();
            Cursor.lockState = previousCursorLockState;
            Cursor.visible = previousCursorVisible;
        }

        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (paused)
            RefreshCampaignStats();
        if (pausePanel != null)
            pausePanel.SetActive(paused);
    }

    private void BuildSettingsUi()
    {
        if (pausePanel == null)
            return;

        Transform card = pausePanel.transform.Find("PauseMenuCard");
        if (card == null)
            return;

        RectTransform mainMenuRect = mainMenuButton != null ? mainMenuButton.GetComponent<RectTransform>() : null;
        RectTransform quitRect = quitButton != null ? quitButton.GetComponent<RectTransform>() : null;
        if (mainMenuRect != null)
            mainMenuRect.anchoredPosition = new Vector2(mainMenuRect.anchoredPosition.x, -5f);
        if (quitRect != null)
            quitRect.anchoredPosition = new Vector2(quitRect.anchoredPosition.x, -145f);

        settingsButton = CreateButton("SettingsButton", card, "", new Vector2(0f, -75f),
            new Color(0.376f, 0.835f, 0.91f, 0.92f));
        settingsButtonLabel = settingsButton.GetComponentInChildren<TMP_Text>(true);
        settingsButton.onClick.AddListener(OpenSettings);
        cityMissionDirector = FindAnyObjectByType<HighStakes.City.CityMissionDirector>();
        cityHud = FindAnyObjectByType<HighStakes.City.CityHud>();
        if (cityMissionDirector != null)
        {
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, 500f);
            GameObject stats = CreateLabel("CampaignStats", card, "", 17f, TextAlignmentOptions.Center);
            Anchor(stats.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -96f), new Vector2(390f, 30f));
            campaignStatsLabel = stats.GetComponent<TMP_Text>();
            campaignStatsLabel.color = new Color32(223, 207, 222, 255);
            newCampaignButton = CreateButton("NewCampaignButton", card, "", new Vector2(0f, -215f),
                new Color(0.38f, 0.45f, 0.56f, 0.92f));
            newCampaignButton.GetComponentInChildren<TMP_Text>(true).color = Color.white;
            newCampaignButton.onClick.AddListener(() => {
                var director = FindAnyObjectByType<HighStakes.City.CityMissionDirector>();
                if (director != null) director.NewCampaign();
            });
        }

        settingsPanel = CreateUiObject("SettingsPanel", pausePanel.transform);
        RectTransform settingsPanelRect = settingsPanel.GetComponent<RectTransform>();
        Stretch(settingsPanelRect);
        Image overlay = settingsPanel.AddComponent<Image>();
        overlay.color = new Color(0.01f, 0.02f, 0.05f, 0.78f);
        overlay.raycastTarget = true;

        GameObject settingsCard = CreateUiObject("SettingsCard", settingsPanel.transform);
        RectTransform settingsCardRect = settingsCard.GetComponent<RectTransform>();
        Anchor(settingsCardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(460f, 340f));
        Image cardImage = settingsCard.AddComponent<Image>();
        cardImage.color = new Color(0.09f, 0.145f, 0.243f, 0.98f);
        AddOutline(settingsCard, new Color(0.38f, 0.84f, 0.91f, 0.4f));

        GameObject title = CreateLabel("SettingsTitle", settingsCard.transform, "", 26f,
            TextAlignmentOptions.Center);
        Anchor(title.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -34f), new Vector2(400f, 42f));
        settingsTitleLabel = title.GetComponent<TMP_Text>();
        settingsTitleLabel.color = new Color(0.38f, 0.84f, 0.91f, 1f);

        minimapToggleButton = CreateButton("MinimapToggleButton", settingsCard.transform, "", new Vector2(0f, 44f),
            new Color(0.376f, 0.835f, 0.91f, 0.92f));
        minimapToggleLabel = minimapToggleButton.GetComponentInChildren<TMP_Text>(true);
        minimapToggleButton.onClick.AddListener(ToggleMinimap);

        languageButton = CreateButton("LanguageButton", settingsCard.transform, "", new Vector2(0f, -28f),
            new Color(0.376f, 0.835f, 0.91f, 0.92f));
        languageLabel = languageButton.GetComponentInChildren<TMP_Text>(true);
        languageButton.onClick.AddListener(ToggleLanguage);

        closeSettingsButton = CreateButton("CloseSettingsButton", settingsCard.transform, "", new Vector2(0f, -100f),
            new Color(0.38f, 0.45f, 0.56f, 0.92f));
        closeSettingsLabel = closeSettingsButton.GetComponentInChildren<TMP_Text>(true);
        closeSettingsButton.onClick.AddListener(CloseSettings);

        settingsPanel.SetActive(false);
        UpdateLocalizedUi();
        UpdateMinimapToggleLabel();
    }

    private void BuildRuntimePauseUi()
    {
        GameObject canvasObject = CreateUiObject("Pause Menu Canvas", null);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        pausePanel = CreateUiObject("PauseWindow", canvasObject.transform);
        RectTransform panelRect = pausePanel.GetComponent<RectTransform>();
        Stretch(panelRect);
        Image overlay = pausePanel.AddComponent<Image>();
        overlay.color = new Color(0.01f, 0.02f, 0.05f, 0.78f);
        overlay.raycastTarget = true;

        GameObject card = CreateUiObject("PauseMenuCard", pausePanel.transform);
        Anchor(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(440f, 400f));
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = new Color32(23, 37, 62, 250);
        AddOutline(card, new Color32(96, 213, 232, 180));

        GameObject title = CreateLabel("PauseTitle", card.transform, "", 30f,
            TextAlignmentOptions.Center);
        Anchor(title.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -36f), new Vector2(380f, 46f));
        pauseTitleLabel = title.GetComponent<TMP_Text>();
        pauseTitleLabel.color = new Color32(96, 213, 232, 255);

        resumeButton = CreateButton("ResumeButton", card.transform, "", new Vector2(0f, 90f),
            new Color32(96, 213, 232, 255));
        resumeLabel = resumeButton.GetComponentInChildren<TMP_Text>(true);
        mainMenuButton = CreateButton("MainMenuButton", card.transform, "", new Vector2(0f, 0f),
            new Color32(157, 173, 198, 255));
        mainMenuLabel = mainMenuButton.GetComponentInChildren<TMP_Text>(true);
        quitButton = CreateButton("QuitButton", card.transform, "", new Vector2(0f, -110f),
            new Color32(239, 103, 120, 255));
        quitLabel = quitButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void OpenSettings()
    {
        if (settingsPanel == null)
            return;

        UpdateMinimapToggleLabel();
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
    }

    private void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void ToggleMinimap()
    {
        if (cityHud != null)
        {
            cityHud.SetMapVisible(!cityHud.IsMapVisible);
        }
        else
        {
            if (minimapController == null)
                minimapController = FindAnyObjectByType<MinimapController>(FindObjectsInactive.Include);
            if (minimapController != null)
                minimapController.SetVisible(!minimapController.IsVisible);
        }
        UpdateMinimapToggleLabel();
    }

    private void ToggleLanguage()
    {
        LocalizationManager.ToggleLanguage();
    }

    private void UpdateLocalizedUi()
    {
        if (newCampaignButton != null)
            newCampaignButton.GetComponentInChildren<TMP_Text>(true).text = LocalizationManager.GetOrFallback("ui.city.new_campaign", "NEW CAMPAIGN");
        if (pauseTitleLabel != null)
            pauseTitleLabel.text = LocalizationManager.GetOrFallback("ui.pause.title", "PAUSED");
        if (resumeLabel != null)
            resumeLabel.text = LocalizationManager.GetOrFallback("ui.pause.resume", "RESUME GAME");
        if (mainMenuLabel != null)
            mainMenuLabel.text = LocalizationManager.GetOrFallback("ui.pause.main_menu", "MAIN MENU");
        if (quitLabel != null)
            quitLabel.text = LocalizationManager.GetOrFallback("ui.pause.quit", "QUIT TO DESKTOP");
        if (settingsButtonLabel != null)
            settingsButtonLabel.text = LocalizationManager.GetOrFallback("ui.settings.title", "SETTINGS");
        if (settingsTitleLabel != null)
            settingsTitleLabel.text = LocalizationManager.GetOrFallback("ui.settings.title", "SETTINGS");
        if (closeSettingsLabel != null)
            closeSettingsLabel.text = LocalizationManager.GetOrFallback("ui.settings.back", "BACK");
        if (languageLabel != null)
        {
            string languageName = LocalizationManager.CurrentLanguage == LocalizationManager.Slovak
                ? LocalizationManager.GetOrFallback("ui.language.slovak", "SLOVAK")
                : LocalizationManager.GetOrFallback("ui.language.english", "ENGLISH");
            languageLabel.text = LocalizationManager.GetOrFallback("ui.settings.language", "LANGUAGE") + ": " + languageName;
        }
        RefreshCampaignStats();
        UpdateMinimapToggleLabel();
    }

    private void RefreshCampaignStats()
    {
        if (campaignStatsLabel != null && cityMissionDirector != null && cityMissionDirector.Progress != null)
            campaignStatsLabel.text = HighStakes.City.CityHud.CampaignStats(cityMissionDirector.Progress);
    }

    private void UpdateMinimapToggleLabel()
    {
        if (minimapToggleLabel == null)
            return;

        if (cityHud == null && minimapController == null)
            minimapController = FindAnyObjectByType<MinimapController>(FindObjectsInactive.Include);

        bool visible = cityHud != null ? cityHud.IsMapVisible : minimapController == null || minimapController.IsVisible;
        string state = visible
            ? LocalizationManager.GetOrFallback("ui.minimap.on", "ON")
            : LocalizationManager.GetOrFallback("ui.minimap.off", "OFF");
        minimapToggleLabel.text = LocalizationManager.GetOrFallback("ui.settings.minimap", "MINIMAP") + ": " + state;
    }

    private static TMP_Text FindLabel(TMP_Text current, Button button)
    {
        return current != null ? current : button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private TMP_Text FindLabel(TMP_Text current, string objectName)
    {
        if (current != null || pausePanel == null)
            return current;
        Transform found = pausePanel.transform.Find(objectName);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Color colour)
    {
        GameObject root = CreateUiObject(name, parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(320f, 58f));

        Image image = root.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = true;
        AddOutline(root, new Color(1f, 1f, 1f, 0.14f));

        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = colour;
        colors.highlightedColor = Color.Lerp(colour, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(colour, new Color(0.08f, 0.12f, 0.2f, 1f), 0.25f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        GameObject textObject = CreateLabel(name + "Label", root.transform, label, 18f,
            TextAlignmentOptions.Center);
        Stretch(textObject.GetComponent<RectTransform>());
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.color = new Color(0.03f, 0.05f, 0.1f, 1f);
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        return button;
    }

    private static GameObject CreateLabel(string name, Transform parent, string value, float size,
        TextAlignmentOptions alignment)
    {
        GameObject root = CreateUiObject(name, parent);
        TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = size;
        text.color = Color.white;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        return root;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        return root;
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

    private static void AddOutline(GameObject root, Color colour)
    {
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = colour;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
    }
}
