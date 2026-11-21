using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class NpcInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [System.NonSerialized]
    public string interactionMessage;

    private GameObject player;
    private PlayerStats playerStats;
    private PlayerUIManager puim;

    public INpcBehavior npcBehavior;

    private GameObject popupPan;
    private GameMaster gm;

    public bool isClose;
    public bool isInteracting;
    public bool chase;

    public Camera playerCamera;

    public GameObject npcGo;
    public bool deal = false;

    public bool recruitInter = false;

    public GameObject currentPlayer = null;
    public bool currentNpcIner = false;

    private DialogueCameraController dialogueCameraController;
    private DialogueLetterbox dialogueLetterbox;
    private bool dialogueStarted;

    [Header("Conversation memory")]
    [SerializeField] private int conversationCount;
    [SerializeField, Range(-100, 100)] private int relationshipScore;
    [SerializeField] private string lastChoiceId;

    public int ConversationCount => conversationCount;
    public int RelationshipScore => relationshipScore;
    public int Relationship => relationshipScore;
    public string LastChoiceId => lastChoiceId;
    public bool HasMet => conversationCount > 0;
    public bool IsDialogueActive => dialogueStarted;




    private void Start()
    {
        npcBehavior = GetComponent<INpcBehavior>();
        gm = FindObjectOfType<GameMaster>();

        isClose = false;
        isInteracting = false;
        chase = false;
        npcGo = this.gameObject;
        interactionMessage = NpcDialogue.Get("interaction.prompt");
    }

    private void Update()
    {
        if (GameMaster.GameOverActive)
            return;

        player = currentPlayer;
        playerStats = player != null ? player.GetComponent<PlayerStats>() : null;
        puim = player != null ? player.GetComponent<PlayerUIManager>() : null;

        // Once a dialogue starts, player movement is locked and the player can
        // no longer leave the interaction trigger to invoke OnTriggerExit.
        // Allow the dialogue to be closed explicitly instead.
        if (dialogueStarted)
        {
            // Dialogue behaviors update interactionMessage directly as they
            // advance through their branches. Keep the existing popup panel
            // fed while dialogue is active; otherwise the early return below
            // bypasses the normal proximity-prompt rendering path and leaves
            // the dialogue text/buttons hidden behind an inactive PopupPan.
            if (puim != null && !string.IsNullOrWhiteSpace(interactionMessage))
                puim.ShowPopup(interactionMessage);

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
            {
                EndDialogue();

                if (puim != null)
                {
                    puim.HidePopup();
                    puim.CloseAllInteractionUI();
                }

                if (player != null)
                {
                    PlayerController playerController = player.GetComponent<PlayerController>();
                    if (playerController != null)
                        playerController.LockMouse();
                }
            }

            return;
        }

        if (isClose && puim != null)
        {
            puim.ShowPopup(interactionMessage);

            if (Input.GetKeyDown(KeyCode.E))
            {
                RefreshBehavior();
                if (npcBehavior != null && playerStats != null)
                {
                    BeginDialogue();
                    npcBehavior.Interact(playerStats);
                }
                else if (npcBehavior == null)
                {
                    GameLogger.LogError("npc", "missing_behavior", $"NPC {gameObject.name} has no INpcBehavior attached");
                }
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                GameLogger.LogInfo("gameplay", "pickpocket_started", $"Started pickpocket attempt on {gameObject.name}");
                StealMaster.Instance.StartSteal(this.gameObject, player);
            }
        }

    }

   

    public void OnChangeBehavior()
    {
        Invoke("OnChange", 3f);
    }

    public void OnChange()
    {
        RefreshBehavior();
        if (npcBehavior != null && playerStats != null)
        {
            BeginDialogue();
            npcBehavior.Interact(playerStats);
        }
    }

    public void BeginDialogue()
    {
        if (dialogueStarted || currentPlayer == null)
            return;

        dialogueStarted = true;
        conversationCount++;
        GameLogger.LogInfo("npc", "dialogue_started", $"Started dialogue with {gameObject.name} (conversation count: {conversationCount})");
        dialogueCameraController = currentPlayer.GetComponent<DialogueCameraController>();
        if (dialogueCameraController != null)
            dialogueCameraController.EnterDialogue(transform);

        dialogueLetterbox = DialogueLetterbox.GetOrCreate();
        dialogueLetterbox.ShowLetterbox();
    }

    public void EndDialogue()
    {
        if (dialogueStarted)
            GameLogger.LogDebug("npc", "dialogue_ended", $"Ended dialogue with {gameObject.name}");

        dialogueStarted = false;
        if (dialogueCameraController != null)
            dialogueCameraController.ExitDialogue();
        dialogueCameraController = null;

        if (dialogueLetterbox != null)
            dialogueLetterbox.HideLetterbox();
        dialogueLetterbox = null;
    }

    /// <summary>
    /// Presents up to three meaningful conversation choices using the existing
    /// interaction buttons. Trade and inventory screens continue to use their
    /// legacy helpers below.
    /// </summary>
    public void ShowConversationChoices(
        string message,
        string firstLabel, UnityAction firstAction,
        string secondLabel, UnityAction secondAction,
        string thirdLabel, UnityAction thirdAction)
    {
        if (currentPlayer == null)
            return;

        if (puim == null)
            puim = currentPlayer.GetComponent<PlayerUIManager>();

        interactionMessage = message;
        if (puim == null)
            return;

        puim.CloseAllInteractionUI();
        ConfigureChoiceButton(puim.yesButton, puim.buttonYes, firstLabel, firstAction);
        ConfigureChoiceButton(puim.noButton, puim.buttonNo, secondLabel, secondAction);
        ConfigureChoiceButton(puim.dropButton, puim.buttonDrop, thirdLabel, thirdAction);
        puim.SetButtonLabels(firstLabel, secondLabel, thirdLabel);

        PlayerController controller = currentPlayer.GetComponent<PlayerController>();
        if (controller != null)
            controller.UnlockMouse();
    }

    public void RememberChoice(string choiceId, int relationshipDelta = 0)
    {
        lastChoiceId = choiceId;
        relationshipScore = Mathf.Clamp(relationshipScore + relationshipDelta, -100, 100);
    }

    public void CloseConversation(string finalMessage = null)
    {
        if (!string.IsNullOrEmpty(finalMessage))
            interactionMessage = finalMessage;

        EndDialogue();
        if (puim != null)
            puim.CloseAllInteractionUI();

        if (currentPlayer != null)
        {
            PlayerController controller = currentPlayer.GetComponent<PlayerController>();
            if (controller != null)
                controller.LockMouse();
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private static void ConfigureChoiceButton(
        GameObject buttonObject, Button button, string label, UnityAction action)
    {
        if (buttonObject != null)
            buttonObject.SetActive(!string.IsNullOrEmpty(label) && action != null);

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        if (!string.IsNullOrEmpty(label) && action != null)
            button.onClick.AddListener(action);
    }

    private void OnDisable()
    {
        EndDialogue();
    }

    private void OnDestroy()
    {
        EndDialogue();
    }

    public void RefreshBehavior()
    {
        npcBehavior = GetComponent<INpcBehavior>();
    }

    public void SetSelectedTradeItem(Item item)
    {
        puim.selectedItemImage = puim.itemImgGo.GetComponent<Image>();
        Debug.Log("[npcInter] SetSelectedTradeItem -> inside function");
        puim.selectedTradeItem = item;

        BuyerBehavior buyer = this.gameObject.GetComponent<BuyerBehavior>();
        if (buyer != null)
        {
            buyer.OnItemSelected(item);

        }

        RecruitBehavior recruit = this.gameObject.GetComponent<RecruitBehavior>();
        if (recruit != null)
        {
            recruit.OnItemSelected(item);
        }

        if (puim.selectedItemImage != null)
            puim.selectedItemImage.sprite = item.icon;
    }

    public void YesNo()
    {
        puim.yesButton.SetActive(true);
        puim.buttonYes.GetComponentInChildren<TextMeshProUGUI>().text =
            LocalizationManager.GetOrFallback("ui.choice.yes", "Yes");
        puim.noButton.SetActive(true);
        puim.buttonNo.GetComponentInChildren<TextMeshProUGUI>().text =
            LocalizationManager.GetOrFallback("ui.choice.no", "No");
        GameObject activePlayer = currentPlayer != null ? currentPlayer : player;
        if (activePlayer != null)
            activePlayer.GetComponent<PlayerController>()?.UnlockMouse();
    }

    public void CollectCancelDrop()
    {
        puim.yesButton.SetActive(true);
        puim.buttonYes.GetComponentInChildren<TextMeshProUGUI>().text =
            LocalizationManager.GetOrFallback("ui.choice.collect", "Collect");
        puim.noButton.SetActive(true);
        puim.buttonNo.GetComponentInChildren<TextMeshProUGUI>().text =
            LocalizationManager.GetOrFallback("ui.choice.close", "Close");
        puim.dropButton.SetActive(true);
        puim.buttonDrop.GetComponentInChildren<TextMeshProUGUI>().text =
            LocalizationManager.GetOrFallback("ui.choice.drop_item", "Drop Item");
        GameObject activePlayer = currentPlayer != null ? currentPlayer : player;
        if (activePlayer != null)
            activePlayer.GetComponent<PlayerController>()?.UnlockMouse();
    }

    public void RecruitClose()
    {
        puim.yesButton.SetActive(true);
        puim.buttonYes.GetComponentInChildren<TextMeshProUGUI>().text =
            LocalizationManager.GetOrFallback("ui.choice.recruit", "Recruit");
        puim.noButton.SetActive(true);
        puim.buttonNo.GetComponentInChildren<TextMeshProUGUI>().text =
            LocalizationManager.GetOrFallback("ui.choice.close", "Close");
        GameObject activePlayer = currentPlayer != null ? currentPlayer : player;
        if (activePlayer != null)
            activePlayer.GetComponent<PlayerController>()?.UnlockMouse();
    }

    public void ItemImg()
    {
        puim.popupImageBorder.SetActive(true);
        puim.itemImgGo.SetActive(true);
        
    }
}
