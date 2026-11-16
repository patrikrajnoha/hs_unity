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
    private Button minimapToggleButton;
    private Button closeSettingsButton;
    private GameObject settingsPanel;
    private TMP_Text minimapToggleLabel;
    private MinimapController minimapController;

    private bool isPaused;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;

    private void Awake()
    {
        SetPaused(false);
    }

    private void Start()
    {
        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        BuildSettingsUi();
    }

    private void Update()
    {
        if (GameMaster.GameOverActive)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
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
        if (minimapToggleButton != null)
            minimapToggleButton.onClick.RemoveListener(ToggleMinimap);
        if (closeSettingsButton != null)
            closeSettingsButton.onClick.RemoveListener(CloseSettings);
    }

    public void TogglePause()
    {
        SetPaused(!isPaused);
    }

    public void ResumeGame()
    {
        SetPaused(false);
    }

    public void ReturnToMainMenu()
    {
        SetPaused(false);
        SceneManager.LoadScene("Menu");
    }

    public void QuitGame()
    {
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

        settingsButton = CreateButton("SettingsButton", card, "SETTINGS", new Vector2(0f, -75f),
            new Color(0.376f, 0.835f, 0.91f, 0.92f));
        settingsButton.onClick.AddListener(OpenSettings);

        settingsPanel = CreateUiObject("SettingsPanel", pausePanel.transform);
        RectTransform settingsPanelRect = settingsPanel.GetComponent<RectTransform>();
        Stretch(settingsPanelRect);
        Image overlay = settingsPanel.AddComponent<Image>();
        overlay.color = new Color(0.01f, 0.02f, 0.05f, 0.78f);
        overlay.raycastTarget = true;

        GameObject settingsCard = CreateUiObject("SettingsCard", settingsPanel.transform);
        RectTransform settingsCardRect = settingsCard.GetComponent<RectTransform>();
        Anchor(settingsCardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(460f, 300f));
        Image cardImage = settingsCard.AddComponent<Image>();
        cardImage.color = new Color(0.09f, 0.145f, 0.243f, 0.98f);
        AddOutline(settingsCard, new Color(0.38f, 0.84f, 0.91f, 0.4f));

        GameObject title = CreateLabel("SettingsTitle", settingsCard.transform, "SETTINGS", 26f,
            TextAlignmentOptions.Center);
        Anchor(title.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -34f), new Vector2(400f, 42f));
        title.GetComponent<TMP_Text>().color = new Color(0.38f, 0.84f, 0.91f, 1f);

        minimapToggleButton = CreateButton("MinimapToggleButton", settingsCard.transform, "", new Vector2(0f, 18f),
            new Color(0.376f, 0.835f, 0.91f, 0.92f));
        minimapToggleLabel = minimapToggleButton.GetComponentInChildren<TMP_Text>(true);
        minimapToggleButton.onClick.AddListener(ToggleMinimap);

        closeSettingsButton = CreateButton("CloseSettingsButton", settingsCard.transform, "BACK", new Vector2(0f, -72f),
            new Color(0.38f, 0.45f, 0.56f, 0.92f));
        closeSettingsButton.onClick.AddListener(CloseSettings);

        settingsPanel.SetActive(false);
        UpdateMinimapToggleLabel();
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
        if (minimapController == null)
            minimapController = FindAnyObjectByType<MinimapController>(FindObjectsInactive.Include);

        if (minimapController == null)
            return;

        minimapController.SetVisible(!minimapController.IsVisible);
        UpdateMinimapToggleLabel();
    }

    private void UpdateMinimapToggleLabel()
    {
        if (minimapToggleLabel == null)
            return;

        if (minimapController == null)
            minimapController = FindAnyObjectByType<MinimapController>(FindObjectsInactive.Include);

        bool visible = minimapController == null || minimapController.IsVisible;
        minimapToggleLabel.text = "MINIMAP: " + (visible ? "ON" : "OFF");
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
