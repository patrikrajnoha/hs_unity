using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class PlayerUIManager : MonoBehaviour
{
    [Header("UI Panels - Assign in Inspector")]
    public GameObject statsPanel;
    public GameObject popupPanel;
    public GameObject stealPanel;
    public GameObject stageProgressPanel;
    public GameObject wantedPan;
    public TextMeshProUGUI wantedCountdownText;

    [Header("Stats UI")]
    public TextMeshProUGUI statsText;

    [Header("Popup UI")]
    public TextMeshProUGUI popupText;
    public GameObject popupImageBorder;
    public Sprite popImgSprite;

    [Header("Interaction UI - Assign in Inspector")]
    public GameObject yesButton;
    public GameObject noButton;
    public GameObject dropButton;
    public Button buttonYes;
    public Button buttonNo;
    public Button buttonDrop;
    public GameObject inputPriceGO;
    public GameObject inputQuantityGO;
    public TMP_InputField inputPriceTxt;
    public TMP_InputField inputQuantityTxt;
    public Image selectedItemImage;
    public Item selectedTradeItem;
    public GameObject itemImgGo;
    public GameObject lockPanel;

    [Header("Steal UI")]
    public List<Image> stealSlots;
    public Sprite hiddenSlotSprite;
    public TextMeshProUGUI stealTimerText;
    public TextMeshProUGUI stealAttemptsText;

    [Header("Stage Progress UI")]
    public TMP_Text stageTxt;
    public TMP_Text questTxt;

    [Header("Watering UI")]
    public TMP_Text wateringCountdownText;

    private PlayerController player;
    private string currentPopupMessage;

    


    /*
    public void Initialize(PlayerController playerController)
    {
        player = playerController;
        HideAllOnStart();
    }
    */

    private void Start()
    {
        EnsureWateringCountdownText();
        HideAllOnStart();
        

    }
    void HideAllOnStart()
    {
        if (statsPanel != null) statsPanel.SetActive(false);
        if (popupPanel != null) popupPanel.SetActive(false);
        if (stealPanel != null) stealPanel.SetActive(false);
        if (wantedPan != null) wantedPan.SetActive(false);
        if (yesButton != null) yesButton.SetActive(false);
        if (noButton != null) noButton.SetActive(false);
        if (dropButton != null) dropButton.SetActive(false);
        if (lockPanel != null) lockPanel.SetActive(false);
        if (itemImgGo != null) itemImgGo.SetActive(false);
        if (inputPriceGO != null) inputPriceGO.SetActive(false);
        if (inputQuantityGO != null) inputQuantityGO.SetActive(false);
        if (popupImageBorder != null) popupImageBorder.SetActive(false);
        HideWateringCountdown();


    }

    void LateUpdate()
    {
        UpdatePopup();
    }

    void UpdatePopup()
    {
        if (!string.IsNullOrEmpty(currentPopupMessage))
        {
            if (popupText != null) popupText.text = currentPopupMessage;
            if (popupImageBorder != null) popupImageBorder.GetComponent<Image>().enabled = true;
            if (popupText != null) popupText.enabled = true;
        }
        else
        {
            if (popupImageBorder != null) popupImageBorder.GetComponent<Image>().enabled = false;
            if (popupText != null) popupText.enabled = false;
            if (popupPanel != null && popupPanel.activeSelf) popupPanel.SetActive(false);
        }
        currentPopupMessage = null;
    }

    public void ShowPopup(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        currentPopupMessage = message;
        if (popupPanel != null && !popupPanel.activeSelf)
            popupPanel.SetActive(true);
    }

    /// <summary>
    /// Shows a compact prompt using the key that the gameplay code actually
    /// listens for. Keeping the key at the call site prevents UI text from
    /// drifting away from the interaction binding.
    /// </summary>
    public void ShowInteractionPrompt(KeyCode key, string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return;

        ShowPopup("[" + key + "] " + action);
    }

    private void EnsureWateringCountdownText()
    {
        if (wateringCountdownText != null)
            return;

        Transform parent = stageProgressPanel != null ? stageProgressPanel.transform.parent : null;
        if (parent == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>(true);
            if (canvas != null)
                parent = canvas.transform;
        }

        if (parent == null)
            return;

        GameObject countdownObject = new GameObject("WateringCountdownText", typeof(RectTransform), typeof(TextMeshProUGUI));
        countdownObject.transform.SetParent(parent, false);
        countdownObject.transform.SetAsLastSibling();

        TextMeshProUGUI text = countdownObject.GetComponent<TextMeshProUGUI>();
        TMP_Text template = questTxt != null ? questTxt : stageTxt;
        if (template != null)
        {
            text.font = template.font;
            text.fontSize = Mathf.Max(24f, template.fontSize * 0.9f);
        }

        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        RectTransform rect = countdownObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.8f);
        rect.anchorMax = new Vector2(0.5f, 0.8f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(600f, 60f);

        wateringCountdownText = text;
    }

    public void ShowWateringCountdown(float seconds)
    {
        if (wateringCountdownText == null)
            return;

        wateringCountdownText.text = "Polievanie: " + Mathf.CeilToInt(Mathf.Max(0f, seconds)) + " s";
        wateringCountdownText.gameObject.SetActive(true);
    }

    public void HideWateringCountdown()
    {
        if (wateringCountdownText == null)
            return;

        wateringCountdownText.text = string.Empty;
        wateringCountdownText.gameObject.SetActive(false);
    }


    public void HidePopup()
    {
        if (popupImageBorder != null)
            popupImageBorder.SetActive(false);
        if (popupText != null) popupText.enabled = false;
        currentPopupMessage = null;
        if (popupPanel != null)
            popupPanel.SetActive(false);
    }

    public void ShowWantedPanel(bool show)
    {
        if (wantedPan != null)
            wantedPan.SetActive(show);
    }

    public void UpdateWantedCountdown(string text)
    {
        if (wantedCountdownText != null)
            wantedCountdownText.text = text;
    }

    public void ShowStatsPanel(bool show)
    {
        if (statsPanel != null) statsPanel.SetActive(show);
    }

    public void UpdateStatsText(string text)
    {
        if (statsText != null) statsText.text = text;
    }

    public void ShowYesNo()
    {
        if (yesButton != null) yesButton.SetActive(true);
        if (noButton != null) noButton.SetActive(true);
    }

    public void ShowCollectCancelDrop()
    {
        if (yesButton != null) yesButton.SetActive(true);
        if (noButton != null) noButton.SetActive(true);
        if (dropButton != null) dropButton.SetActive(true);
    }

    public void ShowRecruitClose()
    {
        if (yesButton != null) yesButton.SetActive(true);
        if (noButton != null) noButton.SetActive(true);
    }

    public void SetButtonLabels(string yes = "Yes", string no = "No", string drop = "Drop")
    {
        if (buttonYes != null)
        {
            var txt = buttonYes.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = yes;
        }
        if (buttonNo != null)
        {
            var txt = buttonNo.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = no;
        }
        if (buttonDrop != null)
        {
            var txt = buttonDrop.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = drop;
        }
    }

    public void ShowInputFields(bool price, bool quantity)
    {
        if (inputPriceGO != null) inputPriceGO.SetActive(price);
        if (inputQuantityGO != null) inputQuantityGO.SetActive(quantity);
    }

    public void ShowItemImage(bool show)
    {
        if (itemImgGo != null) itemImgGo.SetActive(show);
    }

    public void SetItemImage(Sprite sprite)
    {
        if (selectedItemImage != null)
        {
            selectedItemImage.sprite = sprite;
            selectedItemImage.enabled = sprite != null;
        }
    }

    public void ShowLockPanel(bool show)
    {
        if (lockPanel != null) lockPanel.SetActive(show);
    }

    public void CloseAllInteractionUI()
    {
        if (yesButton != null) yesButton.SetActive(false);
        if (noButton != null) noButton.SetActive(false);
        if (dropButton != null) dropButton.SetActive(false);
        ShowInputFields(false, false);
        ShowItemImage(false);
        ShowLockPanel(false);
        SetItemImage(null);
    }

    /// <summary>
    /// STEALING PICKPOCKET UI
    /// </summary>
    public void ShowStealUI()
    {
        stealPanel.SetActive(true);
    }

    public void HideStealUI()
    {
        stealPanel.SetActive(false);
    }

    public void UpdateStealTimer(float time)
    {
        stealTimerText.text = $"...{Mathf.Ceil(time)}s";
    }

    public void UpdateStealAttempts(int current, int max)
    {
        // This function is generic and works for both Searching (Attempts) and Collecting (Clicks) stages.
        stealAttemptsText.text = $"{current}/{max}";
        // You might want to change the word "Attempts" to "Clicks" in the Collecting stage inside NpcInteraction or PUM to be perfectly aligned with your old code, but this is functional.
    }

    public void SetSlotHidden(Image slot)
    {
        slot.sprite = hiddenSlotSprite;
    }

    public void SetSlotRevealed(Image slot, Sprite icon)
    {
        slot.sprite = icon;
    }
    public void SetSlotVisible(Image slot, Sprite icon = null)
    {
        // If we have a specific icon, show it
        if (icon != null)
            slot.sprite = icon;
        else
            slot.sprite = hiddenSlotSprite;

        slot.enabled = true; // make sure it�s visible
    }

    public void SetSlotInvisible(Image slot)
    {
        slot.enabled = false; // hides the visual but keeps the slot object active
    }


    public void ClearStealSlots()
    {
        foreach (var slot in stealSlots)
            SetSlotHidden(slot);
    }

}
