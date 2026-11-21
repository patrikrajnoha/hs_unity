using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Applies the current HighStakes UI language to the existing scene hierarchy.
/// Gameplay scripts keep owning behaviour; this class only normalises layout,
/// colours, text hierarchy and button feedback at runtime.
/// </summary>
public static class UiModernizer
{
    private static readonly Color Ink = new Color32(8, 13, 25, 255);
    private static readonly Color Panel = new Color32(16, 25, 43, 238);
    private static readonly Color PanelRaised = new Color32(23, 37, 62, 248);
    private static readonly Color Accent = new Color32(96, 213, 232, 255);
    private static readonly Color AccentWarm = new Color32(255, 194, 92, 255);
    private static readonly Color Text = new Color32(244, 247, 255, 255);
    private static readonly Color Muted = new Color32(157, 173, 198, 255);
    private static readonly Color Danger = new Color32(239, 103, 120, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyScene(scene);
    }

    public static void ApplyScene(Scene scene)
    {
        if (scene.name == "OldQuarter") return; // This district owns a separate responsive HUD.
        if (Application.isPlaying)
            UiPreviewController.RestoreRuntimeVisibility();

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.gameObject.scene != scene)
                continue;

            NormalizeCanvas(canvas);

            if (scene.name == "Menu")
                ApplyMenu(canvas.transform);
            else if (scene.name == "Main")
                ApplyGameplay(canvas.transform);
        }

        if (!Application.isPlaying)
            UiPreviewController.EnsureSceneSetup(scene);
    }

    private static void NormalizeCanvas(Canvas canvas)
    {
        canvas.transform.localScale = Vector3.one;
        canvas.pixelPerfect = false;

        if (canvas.name == "GameplayCanvas")
        {
            Camera gameplayCamera = ResolveUiCamera();
            if (gameplayCamera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = gameplayCamera;
                canvas.planeDistance = 0.45f;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 5;
            }
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.52f;

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private static Camera ResolveUiCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
            return mainCamera;

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.isActiveAndEnabled)
                return camera;
        }

        return null;
    }

    private static void ApplyMenu(Transform canvas)
    {
        // The runtime-generated menu owns its complete visual hierarchy.
        // Do not add the legacy MenuBrand/backdrop on top of it.
        if (canvas != null && canvas.name == "Main Menu Canvas")
            return;

        GameObject mainPanel = FindNamed("MenuWindow");
        if (mainPanel != null)
        {
            StylePanel(mainPanel, PanelRaised, Accent);
            SetRect(mainPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(840f, 650f));
            CreateMenuBackdrop(canvas);
            CreateMenuSectionLabels(mainPanel.transform);
            ConfigureMenuLayout(mainPanel.transform);
        }

        StyleText(FindNamed("SteamStatusText"), 21f, Text, TextAlignmentOptions.Left);
        StyleText(FindNamed("Label"), 22f, Text, TextAlignmentOptions.Center);
        StyleInput(FindNamed("SteamIdInput"), "STEAM ID / FRIEND CODE");
        StyleButtonsInCanvas(canvas);
        StyleButton(FindNamed("InviteButton"), "INVITE PLAYER", Accent);
        StyleButton(FindNamed("PlayButton"), "START RUN", AccentWarm);

        if (FindNamed("MenuBrand") == null)
            CreateMenuBrand(canvas);
    }

    private static void ConfigureMenuLayout(Transform panel)
    {
        // The menu is intentionally a two-column card: connection status on the
        // left and the actions the player can take on the right. Using anchors
        // keeps the card stable on ultrawide and 16:9 displays alike.
        SetRect(FindNamed("SteamStatusText"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(185f, 28f), new Vector2(315f, 250f));
        SetRect(FindNamed("SteamIdInput"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(-210f, 118f), new Vector2(330f, 60f));
        SetRect(FindNamed("InviteButton"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(-210f, 20f), new Vector2(330f, 58f));
        SetRect(FindNamed("PlayButton"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(-210f, -70f), new Vector2(330f, 64f));
    }

    private static void CreateMenuBackdrop(Transform canvas)
    {
        if (FindNamed("MenuBackdrop") != null)
            return;

        GameObject backdrop = CreateUiObject("MenuBackdrop", canvas);
        SetFullScreen(backdrop.GetComponent<RectTransform>());

        Image image = backdrop.AddComponent<Image>();
        image.color = new Color(Ink.r, Ink.g, Ink.b, 0.94f);
        image.raycastTarget = false;
        backdrop.transform.SetAsFirstSibling();
    }

    private static void CreateMenuSectionLabels(Transform panel)
    {
        if (FindNamed("MenuNetworkLabel") == null)
        {
            GameObject statusLabel = CreateTextObject("MenuNetworkLabel", panel, "NETWORK STATUS", 14f,
                TextAlignmentOptions.Left);
            Anchor(statusLabel.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(42f, -124f), new Vector2(300f, 28f));
            TMP_Text text = statusLabel.GetComponent<TMP_Text>();
            text.color = Accent;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 2f;
        }

        if (FindNamed("MenuConnectionLabel") == null)
        {
            GameObject connectionLabel = CreateTextObject("MenuConnectionLabel", panel, "MULTIPLAYER ACCESS", 14f,
                TextAlignmentOptions.Left);
            Anchor(connectionLabel.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-375f, -124f), new Vector2(330f, 28f));
            TMP_Text text = connectionLabel.GetComponent<TMP_Text>();
            text.color = Accent;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 2f;
        }

        if (FindNamed("MenuHint") == null)
        {
            GameObject hint = CreateTextObject("MenuHint", panel,
                "Enter a friend code to invite a player.\nLocal mode is always available.", 14f,
                TextAlignmentOptions.Left);
            Anchor(hint.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-210f, -170f), new Vector2(330f, 50f));
            TMP_Text text = hint.GetComponent<TMP_Text>();
            text.color = Muted;
            text.lineSpacing = 2f;
        }
    }

    private static void ApplyGameplay(Transform canvas)
    {
        StylePanel(FindNamed("QuestWindow"), Panel, Accent);
        StylePanel(FindNamed("StatsWindow"), PanelRaised, Accent);
        StylePanel(FindNamed("DialogWindow"), new Color32(10, 18, 32, 248), AccentWarm);
        StylePanel(FindNamed("WantedWindow"), new Color(0.24f, 0.07f, 0.12f, 0.94f), Danger);
        StylePanel(FindNamed("StealingWindow"), PanelRaised, AccentWarm);
        StylePanel(FindNamed("InventoryWindow"), new Color(0.04f, 0.07f, 0.13f, 0.94f), Accent);
        StylePanel(FindNamed("StageWindow"), Panel, Accent);
        StylePanel(FindNamed("StatsWindow"), PanelRaised, Accent);
        StylePanel(FindNamed("AttemptPanel"), PanelRaised, AccentWarm);
        StylePanel(FindNamed("TimerPanel"), PanelRaised, AccentWarm);
        StylePanel(FindNamed("ItemPanel"), PanelRaised, Accent);
        StylePanel(FindNamed("SteamPanel"), PanelRaised, Accent);
        StylePanel(FindNamed("LockPanel"), new Color(0.14f, 0.08f, 0.10f, 0.96f), Danger);
        StylePanel(FindNamed("PauseWindow"), new Color(0.02f, 0.03f, 0.07f, 0.82f), Accent);
        StylePanel(FindNamed("PauseMenuCard"), PanelRaised, Accent);

        StyleText(FindNamed("StageText"), 18f, Accent, TextAlignmentOptions.Left);
        StyleText(FindNamed("QuestText"), 21f, Text, TextAlignmentOptions.Left);
        StyleText(FindNamed("Label"), 24f, Text, TextAlignmentOptions.TopLeft);
        StyleText(FindNamed("WantedTimerText"), 21f, AccentWarm, TextAlignmentOptions.Center);
        StyleText(FindNamed("AttemptText"), 18f, Text, TextAlignmentOptions.Center);
        StyleText(FindNamed("AmountText"), 18f, Text, TextAlignmentOptions.Center);
        StyleText(FindNamed("TimerText"), 16f, Muted, TextAlignmentOptions.Center);
        StyleText(FindNamed("InventorySelectedLabel"), 14f, AccentWarm, TextAlignmentOptions.Left);
        StyleText(FindNamed("StatusText"), 18f, Text, TextAlignmentOptions.Center);
        StyleText(FindNamed("FpsCounter"), 14f, Muted, TextAlignmentOptions.Right);

        StyleInput(FindNamed("PriceInput"), "SET PRICE");
        StyleInput(FindNamed("QuantityInput"), "QUANTITY");

        StyleButtonsInCanvas(canvas);
        StyleButton(FindNamed("ConfirmButton"), "CONFIRM", Accent);
        StyleButton(FindNamed("CancelButton"), "CANCEL", Muted);
        StyleButton(FindNamed("DropButton"), "DROP", AccentWarm);
        StyleButton(FindNamed("ResumeButton"), "RESUME GAME", Accent);
        StyleButton(FindNamed("MainMenuButton"), "MAIN MENU", Muted);
        StyleButton(FindNamed("QuitButton"), "QUIT TO DESKTOP", Danger);

        ConfigureGameplayLayout();
        ConfigureDialogueLayout();

        if (FindNamed("ModernHudHeader") == null)
            CreateHudHeader(canvas);
    }

    private static void ConfigureGameplayLayout()
    {
        // Keep the main gameplay windows anchored to the screen edges instead of
        // stretching them from the old full-screen offsets.
        SetRect(FindNamed("QuestWindow"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(440f, 250f));
        SetRect(FindNamed("DialogWindow"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(760f, 136f));
        SetRect(FindNamed("InventoryWindow"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(760f, 136f));
        ArrangeInventorySlots();
        SetRect(FindNamed("WantedWindow"), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-24f, -294f), new Vector2(300f, 82f));
        SetRect(FindNamed("StatsWindow"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(24f, -94f), new Vector2(320f, 360f));
        SetRect(FindNamed("StageWindow"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-24f, 20f), new Vector2(440f, 250f));
        SetRect(FindNamed("StealingWindow"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 420f));

        GameObject stageTextObject = FindNamed("StageText");
        SetRect(stageTextObject, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(-48f, 50f));

        GameObject questTextObject = FindNamed("QuestText");
        SetStretch(questTextObject, new Vector2(24f, 24f), new Vector2(-24f, -24f));

        TMP_Text questText = questTextObject != null ? questTextObject.GetComponent<TMP_Text>() : null;
        if (questText != null)
        {
            questText.margin = new Vector4(4f, 4f, 4f, 4f);
            questText.overflowMode = TextOverflowModes.Ellipsis;
        }

        SetRect(FindNamed("ConfirmButton"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(-240f, -86f), new Vector2(220f, 56f));
        SetRect(FindNamed("CancelButton"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(240f, -86f), new Vector2(220f, 56f));
        SetRect(FindNamed("DropButton"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -86f), new Vector2(220f, 56f));

        PlayerUIManager playerUi = Object.FindFirstObjectByType<PlayerUIManager>(FindObjectsInactive.Include);
        if (playerUi != null && playerUi.popupText != null)
        {
            SetStretch(playerUi.popupText.gameObject, new Vector2(24f, 62f), new Vector2(-24f, -20f));
            StyleText(playerUi.popupText.gameObject, 24f, Text, TextAlignmentOptions.Center);
            playerUi.popupText.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    private static void ConfigureDialogueLayout()
    {
        GameObject dialogWindow = FindNamed("DialogWindow");
        if (dialogWindow == null)
            return;

        // Keep the conversation card above the bottom letterbox bar. The old
        // 136-unit card placed its buttons below the card and clipped them.
        SetRect(dialogWindow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(1000f, 300f));
        dialogWindow.transform.SetAsLastSibling();

        GameObject dialogLabel = dialogWindow.transform.Find("Label") != null
            ? dialogWindow.transform.Find("Label").gameObject
            : null;
        SetRect(dialogLabel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(920f, 126f));

        TMP_Text labelText = dialogLabel != null ? dialogLabel.GetComponent<TMP_Text>() : null;
        if (labelText != null)
        {
            labelText.margin = new Vector4(24f, 16f, 24f, 8f);
            labelText.textWrappingMode = TextWrappingModes.Normal;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            EnsureTextShadow(dialogLabel, new Vector2(2f, -2f));
        }

        SetRect(FindNamed("ConfirmButton"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(-340f, 28f), new Vector2(300f, 68f));
        SetRect(FindNamed("DropButton"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(300f, 68f));
        SetRect(FindNamed("CancelButton"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(340f, 28f), new Vector2(300f, 68f));

        StyleButton(FindNamed("ConfirmButton"), null, Accent);
        StyleButton(FindNamed("DropButton"), null, AccentWarm);
        StyleButton(FindNamed("CancelButton"), null, Muted);
    }

    private static void EnsureTextShadow(GameObject textObject, Vector2 distance)
    {
        if (textObject == null)
            return;

        Shadow shadow = textObject.GetComponent<Shadow>();
        if (shadow == null)
            shadow = textObject.AddComponent<Shadow>();

        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private static void ArrangeInventorySlots()
    {
        GameObject inventoryObject = FindNamed("InventoryWindow");
        if (inventoryObject == null)
            return;

        List<RectTransform> slotParents = new List<RectTransform>();
        for (int i = 0; i < inventoryObject.transform.childCount; i++)
        {
            Transform child = inventoryObject.transform.GetChild(i);
            if (child.name == "LockPanel")
                continue;

            RectTransform childRect = child as RectTransform;
            if (childRect != null)
                slotParents.Add(childRect);
        }

        float spacing = 88f;
        float startX = -(slotParents.Count - 1) * spacing * 0.5f;
        for (int i = 0; i < slotParents.Count; i++)
        {
            RectTransform slot = slotParents[i];
            slot.anchorMin = new Vector2(0.5f, 0.5f);
            slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = new Vector2(startX + i * spacing, 26f);
            slot.sizeDelta = new Vector2(82f, 82f);
        }
    }

    private static void StylePanel(GameObject panelObject, Color colour, Color outlineColour)
    {
        if (panelObject == null)
            return;

        Image image = panelObject.GetComponent<Image>();
        if (image == null)
            image = panelObject.AddComponent<Image>();

        image.color = colour;
        image.raycastTarget = true;

        Outline outline = panelObject.GetComponent<Outline>();
        if (outline == null)
            outline = panelObject.AddComponent<Outline>();

        outline.effectColor = new Color(outlineColour.r, outlineColour.g, outlineColour.b, 0.28f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        Shadow shadow = panelObject.GetComponent<Shadow>();
        if (shadow == null)
            shadow = panelObject.AddComponent<Shadow>();

        shadow.effectColor = new Color(0f, 0f, 0f, 0.34f);
        shadow.effectDistance = new Vector2(0f, -8f);
        shadow.useGraphicAlpha = true;

        EnsurePanelAccent(panelObject, outlineColour);
    }

    private static void EnsurePanelAccent(GameObject panelObject, Color colour)
    {
        Transform existing = panelObject.transform.Find("ModernPanelAccent");
        if (existing != null)
            return;

        GameObject accent = CreateUiObject("ModernPanelAccent", panelObject.transform);
        RectTransform rect = accent.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(14f, -4f);
        rect.offsetMax = new Vector2(-14f, -1f);

        Image image = accent.AddComponent<Image>();
        image.color = new Color(colour.r, colour.g, colour.b, 0.85f);
        image.raycastTarget = false;
    }

    private static void StyleButton(GameObject buttonObject, string label, Color accent)
    {
        if (buttonObject == null)
            return;

        Button button = buttonObject.GetComponent<Button>();
        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
            image = buttonObject.AddComponent<Image>();

        // Buttons use a procedural colour treatment instead of the legacy
        // image sprites. This keeps the visual language consistent and makes
        // hover/pressed states work at every resolution.
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = new Color(accent.r, accent.g, accent.b, 0.92f);
        image.raycastTarget = true;

        if (button != null)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            ColorBlock colours = button.colors;
            colours.normalColor = image.color;
            colours.highlightedColor = Color.Lerp(image.color, Color.white, 0.18f);
            colours.pressedColor = Color.Lerp(image.color, Ink, 0.24f);
            colours.selectedColor = colours.highlightedColor;
            colours.disabledColor = new Color(0.35f, 0.39f, 0.46f, 0.45f);
            colours.fadeDuration = 0.12f;
            button.colors = colours;
        }

        Outline outline = buttonObject.GetComponent<Outline>();
        if (outline == null)
            outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.12f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        Shadow shadow = buttonObject.GetComponent<Shadow>();
        if (shadow == null)
            shadow = buttonObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.32f);
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.useGraphicAlpha = true;

        TMP_Text text = buttonObject.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
            return;

        if (!string.IsNullOrWhiteSpace(label))
            text.text = label;
        text.color = Ink;
        text.fontSize = 20f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 13f;
        text.fontSizeMax = 22f;
        text.raycastTarget = false;
    }

    private static void StyleButtonsInCanvas(Transform canvas)
    {
        if (canvas == null)
            return;

        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            Color accent = Accent;
            string buttonName = button.gameObject.name.ToLowerInvariant();

            if (buttonName.Contains("quit"))
                accent = Danger;
            else if (buttonName.Contains("drop") || buttonName.Contains("invite"))
                accent = AccentWarm;
            else if (buttonName.Contains("no") || buttonName.Contains("cancel") || buttonName.Contains("mainmenu"))
                accent = Muted;

            StyleButton(button.gameObject, null, accent);
        }
    }

    private static void StyleText(GameObject textObject, float size, Color colour, TextAlignmentOptions alignment)
    {
        if (textObject == null)
            return;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (text == null)
            text = textObject.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
            return;

        text.color = colour;
        text.fontSize = size;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(12f, size * 0.68f);
        text.fontSizeMax = size;
        text.lineSpacing = 4f;
    }

    private static void StyleInput(GameObject inputObject, string placeholder)
    {
        if (inputObject == null)
            return;

        Image image = inputObject.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.04f, 0.08f, 0.15f, 0.96f);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        if (input == null)
            return;

        if (input.placeholder is TMP_Text placeholderText)
        {
            placeholderText.text = placeholder;
            placeholderText.color = Muted;
            placeholderText.fontSize = 17f;
        }

        if (input.textComponent != null)
        {
            input.textComponent.color = Text;
            input.textComponent.fontSize = 18f;
        }
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject objectRoot = new GameObject(name, typeof(RectTransform));
        objectRoot.transform.SetParent(parent, false);
        return objectRoot;
    }

    private static void SetFullScreen(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void CreateMenuBrand(Transform canvas)
    {
        GameObject root = CreateTextObject("MenuBrand", canvas, "HIGHSTAKES", 54f, TextAlignmentOptions.Center);
        RectTransform rect = root.GetComponent<RectTransform>();
        Anchor(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(900f, 90f));
        TMP_Text title = root.GetComponent<TMP_Text>();
        title.color = Accent;
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 6f;

        GameObject subtitle = CreateTextObject("MenuSubtitle", root.transform, "RUN THE CITY  •  BUILD YOUR EMPIRE", 16f, TextAlignmentOptions.Center);
        RectTransform subtitleRect = subtitle.GetComponent<RectTransform>();
        Anchor(subtitleRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -26f), new Vector2(900f, 30f));
        subtitle.GetComponent<TMP_Text>().color = Muted;
    }

    private static void CreateHudHeader(Transform canvas)
    {
        GameObject root = CreateTextObject("ModernHudHeader", canvas, "HIGHSTAKES", 22f, TextAlignmentOptions.Left);
        RectTransform rect = root.GetComponent<RectTransform>();
        Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -28f), new Vector2(350f, 40f));
        TMP_Text title = root.GetComponent<TMP_Text>();
        title.color = Accent;
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 2f;
        title.raycastTarget = false;

        GameObject subtitle = CreateTextObject("ModernHudSubtitle", root.transform,
            LocalizationManager.GetOrFallback("ui.hud.live_operation", "LIVE OPERATION  •  ESC TO PAUSE"),
            12f, TextAlignmentOptions.Left);
        RectTransform subtitleRect = subtitle.GetComponent<RectTransform>();
        Anchor(subtitleRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, -21f), new Vector2(350f, 24f));
        subtitle.GetComponent<TMP_Text>().color = Muted;
        subtitle.GetComponent<TMP_Text>().raycastTarget = false;
    }

    private static GameObject CreateTextObject(string name, Transform parent, string value, float size, TextAlignmentOptions alignment)
    {
        GameObject objectRoot = new GameObject(name, typeof(RectTransform));
        objectRoot.transform.SetParent(parent, false);
        TMP_Text text = objectRoot.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Text;
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(10f, size * 0.6f);
        text.fontSizeMax = size;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        return objectRoot;
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetRect(GameObject objectRoot, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        if (objectRoot == null)
            return;

        RectTransform rect = objectRoot.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetStretch(GameObject objectRoot, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (objectRoot == null)
            return;

        RectTransform rect = objectRoot.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static GameObject FindNamed(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform item in transforms)
        {
            if (item.name == objectName)
                return item.gameObject;
        }
        return null;
    }
}
