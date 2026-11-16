using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the runtime Game Over overlay and owns its two scene-navigation
/// actions. The overlay is intentionally created at runtime so it does not
/// replace or rearrange the project's existing HUD/pause-menu hierarchy.
/// </summary>
public sealed class GameOverController : MonoBehaviour
{
    public const string GameOverMessage = "GAME OVER – You got caught!";

    [SerializeField] private string mainMenuSceneName = "Menu";

    private GameObject overlayCanvas;
    private bool isShown;

    public bool IsShown => isShown;

    public static GameOverController GetOrCreate()
    {
        GameOverController existing = Object.FindAnyObjectByType<GameOverController>();
        if (existing != null)
            return existing;

        GameObject controllerObject = new GameObject("GameOverController");
        return controllerObject.AddComponent<GameOverController>();
    }

    public void Show(GameObject player)
    {
        if (isShown)
            return;

        isShown = true;
        Time.timeScale = 0f;

        if (player != null)
        {
            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.SetDialogueInputLocked(true);
                controller.UnlockMouse();
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        EnsureEventSystem();
        BuildOverlay();
    }

    public void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObject.transform.SetParent(transform, false);
    }

    private void BuildOverlay()
    {
        if (overlayCanvas != null)
        {
            overlayCanvas.SetActive(true);
            return;
        }

        overlayCanvas = new GameObject("GameOverCanvas", typeof(RectTransform));
        overlayCanvas.transform.SetParent(transform, false);

        Canvas canvas = overlayCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        UnityEngine.UI.CanvasScaler scaler = overlayCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        overlayCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject panel = CreateUiObject("GameOverPanel", overlayCanvas.transform);
        SetFullScreen(panel.GetComponent<RectTransform>());
        UnityEngine.UI.Image panelImage = panel.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0.015f, 0.015f, 0.02f, 0.92f);

        GameObject card = CreateUiObject("GameOverCard", panel.transform);
        SetCentered(card.GetComponent<RectTransform>(), new Vector2(720f, 420f), new Vector2(0f, 0f));
        UnityEngine.UI.Image cardImage = card.AddComponent<UnityEngine.UI.Image>();
        cardImage.color = new Color(0.09f, 0.1f, 0.13f, 1f);

        GameObject title = CreateUiObject("GameOverText", card.transform);
        SetCentered(title.GetComponent<RectTransform>(), new Vector2(660f, 110f), new Vector2(0f, 105f));
        CreateText(title, GameOverMessage, 42f, TextAlignmentOptions.Center);

        GameObject restartButton = CreateButton(
            "RestartButton",
            card.transform,
            "Restart",
            new Vector2(0f, -35f),
            RestartCurrentScene);

        CreateButton(
            "MainMenuButton",
            card.transform,
            "Main Menu",
            new Vector2(0f, -145f),
            ReturnToMainMenu);

        UnityEngine.UI.Button restart = restartButton.GetComponent<UnityEngine.UI.Button>();
        if (restart != null)
            restart.Select();
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void SetFullScreen(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetCentered(RectTransform rectTransform, Vector2 size, Vector2 position)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = position;
    }

    private static TMPro.TextMeshProUGUI CreateText(
        GameObject parent,
        string message,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        TMPro.TextMeshProUGUI text = parent.AddComponent<TMPro.TextMeshProUGUI>();
        text.text = message;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        if (TMPro.TMP_Settings.defaultFontAsset != null)
            text.font = TMPro.TMP_Settings.defaultFontAsset;

        return text;
    }

    private static GameObject CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 position,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        SetCentered(buttonObject.GetComponent<RectTransform>(), new Vector2(360f, 78f), position);

        UnityEngine.UI.Image image = buttonObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.18f, 0.42f, 0.75f, 1f);

        UnityEngine.UI.Button button = buttonObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject labelObject = CreateUiObject("Label", buttonObject.transform);
        SetFullScreen(labelObject.GetComponent<RectTransform>());
        CreateText(labelObject, label, 28f, TextAlignmentOptions.Center);

        return buttonObject;
    }
}
