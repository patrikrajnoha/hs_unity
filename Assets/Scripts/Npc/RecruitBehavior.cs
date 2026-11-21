using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
//using UnityEngine.UIElements;
using UnityEngine.UI;
//using UnityEngine.UIElements;

public class RecruitBehavior : MonoBehaviour, INpcBehavior
{
    [System.Serializable]
    public class RecruitInventorySlot
    {
        public Item item;
        public int quantity;

        public RecruitInventorySlot(Item item, int quantity)
        {
            this.item = item;
            this.quantity = quantity;
        }
    }

    public List<RecruitInventorySlot> recruitInventory = new List<RecruitInventorySlot>();
    public int money = 0;
    public int trust = 50;
    private float nextAutoSellCheck = 0f;
    private float sellCheckInterval = 5f;
    private float sellRange = 3f;

    [HideInInspector]
    public GameObject player;

    private NpcInteraction npcInteraction;
    private GameMaster gm;
    private PlayerStats playerStats;
    private Inventory playerInventory;
    private PlayerUIManager puim;
    private GameStageManager gsm;



    void Awake()
    {

    }

    void Start()
    {
        npcInteraction = GetComponent<NpcInteraction>();
        gm = FindObjectOfType<GameMaster>();
        gsm = FindObjectOfType<GameStageManager>();


    }

    void Update()
    {
        if (GameStageManager.Instance == null)
            return;

        if (GameStageManager.Instance.currentStage == GameStage.Starter)
            return;

        if (Time.time >= nextAutoSellCheck)
        {
            TryAutoSell();
            nextAutoSellCheck = Time.time + sellCheckInterval;
        }

    }

    // Player gives an item to recruit
    public void GiveItemToRecruit(Item item, int amount = 1)
    {
        if (item == null || amount <= 0) return;

        var existing = recruitInventory.Find(i => i.item == item);
        if (existing != null)
        {
            existing.quantity += amount;
        }
        else
        {
            recruitInventory.Add(new RecruitInventorySlot(item, amount));
        }

        Debug.Log($"[Recruit] Given {amount}x {item.itemName}");
    }

    // Player takes everything back
    public void TakeAllItems(PlayerStats playerStats)
    {
        foreach (var slot in recruitInventory)
        {
            for (int i = 0; i < slot.quantity; i++)
                playerInventory.AddItem(slot.item);
        }

        int collectedMoney = money;
        playerStats.ChangeMoney(collectedMoney);
        if (gsm != null)
            gsm.RecordEarnings(collectedMoney);
        money = 0;
        recruitInventory.Clear();
        npcInteraction.interactionMessage = NpcDialogue.Get("recruit.take_all");
    }

    // Recruit automatically sells to nearby BuyerBehavior NPCs
    public void TryAutoSell()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, sellRange);
        foreach (var hit in hits)
        {
            BuyerBehavior buyer = hit.GetComponent<BuyerBehavior>();
            if (buyer != null)
            {
                SellRandomItemToBuyer(buyer);
                break; // only sell to one buyer at a time
            }
        }
    }

    private void SellRandomItemToBuyer(BuyerBehavior buyer)
    {
        if (recruitInventory.Count == 0)
        {
            Debug.Log("[Recruit] Nothing to sell right now.");
            return;
        }

        // pick random item from inventory
        int randomIndex = Random.Range(0, recruitInventory.Count);
        var slot = recruitInventory[randomIndex];

        if (slot == null || slot.item == null || slot.quantity <= 0)
            return;

        // random amount from 1 to quantity
        int sellAmount = Random.Range(1, slot.quantity + 1);
        int total = sellAmount * ProductItemFactory.GetMarketPrice(slot.item);

        // update values
        slot.quantity -= sellAmount;
        money += total;

        Debug.Log($"[Recruit] Sold {sellAmount}x {slot.item.itemName} for ${total}");

        // remove item if depleted
        if (slot.quantity <= 0)
            recruitInventory.RemoveAt(randomIndex);

        StartCoroutine(DealRoutine());
    }


    private IEnumerator DealRoutine()
    {
        Debug.Log("deal animation...");
        // pick random duration between 30 and 60 sec
        float interactionTime = Random.Range(5f, 7f);

        /*
        // start animation
        if (animator != null)
        {
            animator.SetBool(animationName, true);
            // or: animator.Play(animationName);
        }
        */

        // wait for that duration
        yield return new WaitForSeconds(interactionTime);
        /*
        // stop animation / release interaction
        if (animator != null)
        {
            animator.SetBool(animationName, false);
        }
        */

        Debug.Log("deal happened.. hopefully..");
        //gm.crimeHappened = true;
        NpcInteraction npc = GetComponent<NpcInteraction>();
        npc.isInteracting = false;
        npc.gameObject.GetComponent<NPCWanderRoadOnly>().ResumeMovement();

    }


    // Player interaction
    public void Interact(PlayerStats playerStats)
    {
        player = npcInteraction.currentPlayer;
        playerInventory = player.GetComponent<Inventory>();
        puim = player.GetComponent<PlayerUIManager>();

        npcInteraction.ShowConversationChoices(
            GetGreeting(),
            NpcDialogue.Get("choice.show_me_the_inventory"), () => OpenInventoryMenu(playerStats),
            NpcDialogue.Get("choice.how_s_business_going"), ShowStatus,
            NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("recruit.leave")));
    }

    private string GetGreeting()
    {
        if (npcInteraction.ConversationCount <= 1)
            return NpcDialogue.Get("recruit.greeting.first");

        if (npcInteraction.LastChoiceId == "recruit.status")
            return NpcDialogue.Get("recruit.greeting.status_followup");

        if (npcInteraction.RelationshipScore >= 2)
            return NpcDialogue.Get("recruit.greeting.trusted");

        if (npcInteraction.RelationshipScore <= -2)
            return NpcDialogue.Get("recruit.greeting.cold");

        return NpcDialogue.Get("recruit.greeting.return");
    }

    private void ShowStatus()
    {
        string statusKey = money > 0 || recruitInventory.Count > 0
            ? "recruit.status.good"
            : "recruit.status.low";

        npcInteraction.RememberChoice("recruit.status", 1);
        npcInteraction.ShowConversationChoices(
            NpcDialogue.Get(statusKey),
            NpcDialogue.Get("choice.show_me_the_numbers"), () => OpenInventoryMenu(player != null ? player.GetComponent<PlayerStats>() : null),
            NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("recruit.leave")),
            null, null);
    }

    private void OpenInventoryMenu(PlayerStats playerStats)
    {
        if (playerStats == null)
            return;

        string msg = NpcDialogue.Get("recruit.inventory_header", "money", money.ToString());
        foreach (var slot in recruitInventory)
        {
            msg += NpcDialogue.Get(
                "recruit.inventory_item",
                "item", slot.item.itemName,
                "amount", slot.quantity.ToString());
        }

        npcInteraction.CollectCancelDrop();
        npcInteraction.ItemImg();

        npcInteraction.interactionMessage = msg;
        npcInteraction.recruitInter = true;

        puim.buttonYes.onClick.RemoveAllListeners();
        puim.buttonYes.onClick.AddListener(() => TakeAllItems(playerStats));


        puim.buttonDrop.onClick.RemoveAllListeners();
        puim.buttonDrop.onClick.AddListener(() => OnSellAttempt()); //  NEW ACTION


        puim.buttonNo.onClick.RemoveAllListeners();
        puim.buttonNo.onClick.AddListener(CloseInteraction);

    }

    private void OnSellAttempt()
    {
        Item selected = puim.selectedTradeItem;

        if (selected == null)
        {
            npcInteraction.interactionMessage = NpcDialogue.Get("choice.please_select_an_item_first");
            return;
        }

        if (!selected.isIllegal)
        {
            npcInteraction.interactionMessage = NpcDialogue.Get("recruit.cannot_take");
            return;
        }

        int available = playerInventory.GetItemAmount(selected.name);

        if (available <= 0)
        {
            npcInteraction.interactionMessage = NpcDialogue.Get("recruit.not_enough_item", "item", selected.name);
            CloseInteraction();
            return;
        }

        puim.lockPanel.SetActive(true); // lock item so player can’t change it now

        /*
        // Update the icon image in the UI
        if (gm.selectedItemImage != null)
        {
            gm.selectedItemImage.sprite = selected.icon; // use the ScriptableObject's sprite
            gm.selectedItemImage.enabled = true; // just in case it was hidden
        }
        */

        // Update message text
        npcInteraction.interactionMessage = NpcDialogue.Get(
            "recruit.quantity_prompt",
            "item", selected.name,
            "available", available.ToString());

        // Show input fields (if you later re-enable them)
        //npcInteraction.isInput = true;
        puim.inputQuantityGO.SetActive(true);

        // Rebind button actions
        puim.buttonDrop.onClick.RemoveAllListeners();
        puim.buttonDrop.onClick.AddListener(OnConfirmDrop);

    }

    public void OnItemSelected(Item selected)
    {
        if (selected == null)
        {
            Debug.LogWarning("[Recruit] Ignoring an empty item selection.");
            return;
        }

        if (puim == null)
        {
            Debug.LogWarning("[Recruit] Cannot select an item because PlayerUIManager is missing.");
            return;
        }

        if (puim.selectedItemImage != null)
        {
            puim.selectedItemImage.sprite = selected.icon;
            puim.selectedItemImage.enabled = true;
        }

        puim.selectedTradeItem = selected;
        if (npcInteraction != null)
            npcInteraction.interactionMessage = NpcDialogue.Get("recruit.selected_item", "item", selected.name);
    }

    private void OnConfirmDrop()
    {
        Item selected = puim.selectedTradeItem;
        if (selected == null)
        {
            npcInteraction.interactionMessage = NpcDialogue.Get("choice.please_select_an_item_first");
            return;
        }

        if (puim.inputQuantityTxt == null ||
            !int.TryParse(
                puim.inputQuantityTxt.text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int dropAmount) ||
            dropAmount <= 0)
        {
            npcInteraction.interactionMessage = NpcDialogue.Get("recruit.invalid_quantity");
            return;
        }

        int available = playerInventory.GetItemAmount(selected.name);
        if (dropAmount > available)
        {
            npcInteraction.interactionMessage = NpcDialogue.Get("recruit.not_enough_quantity", "item", selected.name);
            return;
        }
        GiveItemToRecruit(selected, dropAmount);

        CompleteDrop(dropAmount);

    }

    private void CompleteDrop(int amount)
    {
        Item selected = puim.selectedTradeItem;

        npcInteraction.interactionMessage = NpcDialogue.Get("recruit.accept_item", "amount", amount.ToString());
        playerInventory.RemoveItemByName(selected.name, amount);
        trust += 5;

        if (gm.isVisible)
        {
            GameMaster.Instance.ReportCrime(player.gameObject);
        }


        CloseInteraction();
    }

    private void CloseInteraction()
    {
        if (npcInteraction != null)
            npcInteraction.EndDialogue();

        npcInteraction.interactionMessage = NpcDialogue.Get("recruit.goodbye");

        puim.dropButton.SetActive(false);
        puim.noButton.SetActive(false);
        puim.yesButton.SetActive(false);
        puim.itemImgGo.SetActive(false);
        puim.selectedTradeItem = null;  // clear memory
        puim.popupImageBorder.SetActive(false);
        if (puim.selectedItemImage != null)
        {
            puim.selectedItemImage.sprite = null; // remove sprite
            puim.selectedItemImage.enabled = false;
        }

        player.GetComponent<PlayerController>().LockMouse();
        npcInteraction.recruitInter = false;
        puim.inputQuantityGO.SetActive(false);

        puim.lockPanel.SetActive(false);
    }

}
