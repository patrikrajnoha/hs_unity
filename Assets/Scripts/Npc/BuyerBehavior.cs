using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


[System.Serializable]
public class WantedItem
{
    public string itemName;
    public int minAmount = 1;
    public int maxAmount = 5;
    public int basePrice = 100;
    public int addictiveness = 30;
    public int addictAmount = 30;
}
public enum BuyerPersonality
{
    Casual,
    Shady,
    Nervous
}



public class BuyerBehavior : MonoBehaviour, INpcBehavior
{
    public List<WantedItem> wantedItems = new List<WantedItem>();

    public int trust = 50; // affects willingness to negotiate
    //public int addictiveness = 30;
    public Mood mood = Mood.Neutral; // can affect acceptance
    public string wantedItem = "Weed"; // example
    public int wantedAmount;
    public int offerPrice; // default price per item

    private NpcInteraction npcInteraction;
    private Inventory playerInventory;
    private GameMaster gm;

    private PlayerStats currentPlayer;
    private bool counterOfferAccepted = false;
    private bool saleCompletedForInteraction = false;
    private bool saleFlowOpen;
    private Item saleItem;

    [Header("Addiction Settings")]
    public float addictionDecayRate = 1f; // per in-game hour
    private DayNightCycle dayNight;

    public float wantedUntil = 0f; // in-game time until he wants this item

    public GameObject player;
    private GameStageManager gsm;
    private PlayerUIManager puim;

    public bool IsSaleInProgress => saleFlowOpen &&
        npcInteraction != null &&
        npcInteraction.IsDialogueActive &&
        puim != null &&
        puim.selectedTradeItem != null &&
        puim.selectedTradeItem.isIllegal;

    public bool IsSellingTo(GameObject target)
    {
        return target != null && player == target && IsSaleInProgress;
    }

    private void OnEnable()
    {
        PoliceHeatRegistry.Register(this);
    }



    private void Awake()
    {
        npcInteraction = GetComponent<NpcInteraction>();

    }

    void Start()
    {
        gm = FindObjectOfType<GameMaster>();
        dayNight = FindObjectOfType<DayNightCycle>();
        gsm = FindObjectOfType<GameStageManager>();

    }

    void Update()
    {
        //DecayAddictiveness();
        
        

    }

    private float lastDecayTime = 0f;
    private void DecayAddictiveness()
    {
        if (dayNight == null) return;

        // Only decay once per in-game hour
        if (dayNight.currentTime - lastDecayTime >= 1f || dayNight.currentTime < lastDecayTime)
        {
            lastDecayTime = dayNight.currentTime;

            foreach (var item in wantedItems)
            {
                // decay towards original addictAmount
                if (item.addictiveness > item.addictAmount)
                {
                    item.addictiveness -= 1; // decay by 1 per hour, adjust as needed
                    if (item.addictiveness < item.addictAmount) item.addictiveness = item.addictAmount;
                }
            }
        }
    }

    public void Interact(PlayerStats playerStats)
    {
        player = npcInteraction.currentPlayer;
        playerInventory = player.GetComponent<Inventory>();
        puim = player.GetComponent<PlayerUIManager>();

        currentPlayer = playerStats; // store for later
        saleCompletedForInteraction = false;
        saleFlowOpen = false;
        GenerateRequest();
        npcInteraction.ShowConversationChoices(
            GetGreeting() + "\n" + GetHint(),
            NpcDialogue.Get("choice.talk_through_the_order"), OpenTrade,
            NpcDialogue.Get("choice.what_are_you_looking_for"), ShowRequestExplanation,
            NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("buyer.leave")));
    }

    private WantedItem currentRequest;

    private Coroutine requestTimerCoroutine;

    private void GenerateRequest()
    {
        if (wantedItems.Count == 0) return;

        // If still holding a request, don’t change it
        if (currentRequest != null) return;

        // Pick new request
        int totalWeight = 0;
        foreach (var item in wantedItems) totalWeight += item.addictiveness;

        int randomValue = Random.Range(0, totalWeight);
        int runningSum = 0;

        foreach (var item in wantedItems)
        {
            runningSum += item.addictiveness;
            if (randomValue < runningSum)
            {
                currentRequest = item;
                break;
            }
        }

        // Decide amount randomly
        wantedAmount = Random.Range(currentRequest.minAmount, currentRequest.maxAmount + 1);

        // Adjust offer price
        float modifier = 1f;
        switch (mood)
        {
            case Mood.Good: modifier = 1.1f; break;
            case Mood.Angry: modifier = 0.9f; break;
            case Mood.Sad: modifier = 0.95f; break;
        }

        offerPrice = Mathf.RoundToInt(currentRequest.basePrice * modifier * (trust / 100f));

        // Start/reset timer coroutine
        if (requestTimerCoroutine != null)
        {
            StopCoroutine(requestTimerCoroutine);
        }
        requestTimerCoroutine = StartCoroutine(RequestTimerCoroutine(300f)); // 300s = 5 minutes
    }

    private IEnumerator RequestTimerCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        currentRequest = null;
        requestTimerCoroutine = null;
    }


    private string GetHint()
    {
        if (currentRequest == null)
            return NpcDialogue.Get("buyer.request.generic");

        return NpcDialogue.Get("buyer.request.item", "item", currentRequest.itemName);
    }

    private string GetGreeting()
    {
        if (npcInteraction.ConversationCount <= 1)
            return NpcDialogue.Get("buyer.greeting.first");

        if (npcInteraction.LastChoiceId == "buyer.ask_price")
            return NpcDialogue.Get("buyer.greeting.price_followup");

        if (npcInteraction.LastChoiceId == "buyer.ask_request")
            return NpcDialogue.Get("buyer.greeting.request_followup");

        if (npcInteraction.RelationshipScore >= 2)
            return NpcDialogue.Get("buyer.greeting.trusted");

        if (npcInteraction.RelationshipScore <= -2)
            return NpcDialogue.Get("buyer.greeting.cold");

        return NpcDialogue.Get("buyer.greeting.return");
    }

    private void ShowRequestExplanation()
    {
        npcInteraction.RememberChoice("buyer.ask_request", 1);
        npcInteraction.ShowConversationChoices(
            NpcDialogue.Get("buyer.request.explain"),
            NpcDialogue.Get("choice.discuss_the_price"), ShowPriceTalk,
            NpcDialogue.Get("choice.show_me_the_order"), OpenTrade,
            NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("buyer.leave")));
    }

    private void ShowPriceTalk()
    {
        npcInteraction.RememberChoice("buyer.ask_price", 1);
        npcInteraction.ShowConversationChoices(
            NpcDialogue.Get("buyer.price.talk"),
            NpcDialogue.Get("choice.make_the_deal"), OpenTrade,
            NpcDialogue.Get("choice.not_today"), () => npcInteraction.CloseConversation(NpcDialogue.Get("buyer.leave")),
            null, null);
    }

    private void OpenTrade()
    {
        npcInteraction.RememberChoice("buyer.trade", 0);
        saleFlowOpen = true;
        ShowPopup(GetHint());
    }


    private void ShowPopup(string text)
    {
        if (npcInteraction != null)
        {
            npcInteraction.interactionMessage = text;

            npcInteraction.YesNo();
            puim.SetButtonLabels(NpcDialogue.Get("choice.make_offer"), NpcDialogue.Get("choice.walk_away"));
            npcInteraction.ItemImg();


            
            // hook buttons
            puim.buttonYes.onClick.RemoveAllListeners();
            puim.buttonYes.onClick.AddListener(OnSellAttempt);

            puim.buttonNo.onClick.RemoveAllListeners();
            puim.buttonNo.onClick.AddListener(OnDeclineSale);
        }
    }

    private void OnSellAttempt()
    {
        if (puim == null || playerInventory == null || currentRequest == null)
        {
            Debug.LogWarning("[Buyer] Sale attempt ignored because the interaction is not initialized.");
            return;
        }

        Item selected = puim.selectedTradeItem;


        if (selected == null || !ProductItemFactory.MatchesRequest(selected, currentRequest.itemName))
        {
            trust -= 5;
            npcInteraction.interactionMessage = NpcDialogue.Get("buyer.invalid_item", "item", currentRequest.itemName);
            CloseInteraction();
            return;
        }



        saleItem = selected;
        int available = playerInventory.GetItemAmount(selected);

        if (available <= 0)
        {
            trust -= 5;
            npcInteraction.interactionMessage = NpcDialogue.Get("buyer.not_enough_item", "item", currentRequest.itemName);
            CloseInteraction();
            return;
        }

        puim.lockPanel.SetActive(true); // lock item so player can’t change it now

        // Update the icon image in the UI
        if (puim.selectedItemImage != null)
        {
            puim.selectedItemImage.sprite = selected.icon; // use the ScriptableObject's sprite
            puim.selectedItemImage.enabled = true; // just in case it was hidden
        }

        // Update message text
        npcInteraction.interactionMessage = NpcDialogue.Get(
            "buyer.quantity_prompt",
            "item", currentRequest.itemName,
            "available", available.ToString(),
            "wantedAmount", wantedAmount.ToString());

        // Show input fields (if you later re-enable them)
        puim.inputQuantityGO.SetActive(true);
        puim.inputPriceGO.SetActive(true);

        // Rebind button actions
        puim.buttonYes.onClick.RemoveAllListeners();
        puim.buttonYes.onClick.AddListener(OnConfirmSale);

        puim.buttonNo.onClick.RemoveAllListeners();
        puim.buttonNo.onClick.AddListener(OnDeclineSale);
    }


    public void OnItemSelected(Item selected)
    {
        if (selected == null)
        {
            Debug.LogWarning("[Buyer] Ignoring an empty item selection.");
            return;
        }

        if (puim == null && npcInteraction != null && npcInteraction.currentPlayer != null)
            puim = npcInteraction.currentPlayer.GetComponent<PlayerUIManager>();

        if (puim == null)
        {
            Debug.LogWarning("[Buyer] Cannot select an item because PlayerUIManager is missing.");
            return;
        }

        Debug.Log($"[buyer] puim = {puim}");
        Debug.Log($"[buyer] npcInteraction = {npcInteraction}");
        Debug.Log($"[buyer] selected = {selected}, icon = {selected.icon}");

        Debug.Log("[buyer] selectedItemImage : " + puim.selectedItemImage);
        if (puim.selectedItemImage != null)
        {
            puim.selectedItemImage.sprite = selected.icon;
            puim.selectedItemImage.enabled = true;
        }

        Debug.Log("[buyer] selectedTradeItem : " + puim.selectedTradeItem);
        puim.selectedTradeItem = selected;
        if (npcInteraction != null)
            npcInteraction.interactionMessage = NpcDialogue.Get("buyer.selected_item", "item", selected.name);
    }



    private void OnConfirmSale()
    {
        if (puim == null || playerInventory == null || currentRequest == null)
        {
            Debug.LogWarning("[Buyer] Sale confirmation ignored because the interaction is not initialized.");
            return;
        }

        if (!TryGetSaleInput(out int sellAmount, out int sellPrice))
        {
            RejectInvalidSaleInput();
            return;
        }

        Item itemBeingSold = saleItem != null ? saleItem : puim.selectedTradeItem;
        int available = playerInventory.GetItemAmount(itemBeingSold);

        if (sellAmount > available)
        {
            trust -= 5;
            npcInteraction.interactionMessage = NpcDialogue.Get("buyer.not_enough_quantity", "item", currentRequest.itemName);
            CloseInteraction();
            return;
        }

        int basePrice = currentRequest.basePrice;
        int playerTotal = sellPrice * sellAmount;
        int baseTotal = basePrice * sellAmount;

        float trustFactor = 1f + (trust / 100f); // 1.0 to 2.0
        int maxCounterPrice = Mathf.RoundToInt(basePrice * trustFactor);

        // CASE 1: Player offers equal or lower than base -> Accept immediately
        if (sellPrice <= basePrice)
        {
            CompleteSale(sellAmount, playerTotal);
            counterOfferAccepted = false;
            return;
        }

        // CASE 2: Player overpriced
        if (!counterOfferAccepted)
        {
            // Determine logical counter-offer
            int counterPrice = Mathf.Clamp(
                Mathf.RoundToInt(sellPrice * 0.8f), // always lower than player's price
                basePrice,
                maxCounterPrice
            );

            if (counterPrice >= sellPrice)
            {
                counterPrice = sellPrice - 1; // safety clamp
            }

            int counterTotal = counterPrice * sellAmount;
            offerPrice = counterPrice;

            npcInteraction.interactionMessage = NpcDialogue.Get(
                "buyer.counter_offer",
                "counterTotal", counterTotal.ToString(),
                "sellAmount", sellAmount.ToString());
            counterOfferAccepted = true; // next Yes means accept buyer's counter

            // Change Yes/No button behavior for this stage
            puim.buttonYes.onClick.RemoveAllListeners();
            puim.buttonYes.onClick.AddListener(() =>
            {
                CompleteSale(sellAmount, counterTotal);
                counterOfferAccepted = false;
            });

            puim.buttonNo.onClick.RemoveAllListeners();
            puim.buttonNo.onClick.AddListener(OnCounterRefused);

            return;
        }

        // CASE 3: Player presses Yes after already having counter
        CompleteSale(sellAmount, offerPrice * sellAmount);
        counterOfferAccepted = false;
    }


    private void OnCounterRefused()
    {
        float chance = Random.value;

        float trustValueChance = trust / 100f;
        Debug.Log("trsut value chance : " + trustValueChance);

        if (chance < trustValueChance)
        {
            npcInteraction.interactionMessage = NpcDialogue.Get("buyer.counter_accepted");
            if (TryGetSaleInput(out int sellAmount, out int sellPrice))
                CompleteSale(sellAmount, sellPrice * sellAmount);
            else
                RejectInvalidSaleInput();
        }
        else
        {
            GameLogger.LogInfo("economy", "counter_offer_refused", "Buyer " + gameObject.name + " refused counter offer");
            npcInteraction.interactionMessage = NpcDialogue.Get("buyer.counter_refused");
            CloseInteraction();
        }

        counterOfferAccepted = false;
    }

    private bool TryGetSaleInput(out int sellAmount, out int sellPrice)
    {
        sellAmount = 0;
        sellPrice = 0;

        if (puim == null || puim.inputQuantityTxt == null || puim.inputPriceTxt == null)
            return false;

        bool validQuantity = int.TryParse(
            puim.inputQuantityTxt.text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out sellAmount);
        bool validPrice = int.TryParse(
            puim.inputPriceTxt.text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out sellPrice);

        if (!validQuantity || !validPrice || sellAmount <= 0 || sellPrice < 0)
            return false;

        // Keep the existing int-based transaction API safe from arithmetic overflow.
        return (long)sellAmount * sellPrice <= int.MaxValue;
    }

    private void RejectInvalidSaleInput()
    {
        if (npcInteraction != null)
            npcInteraction.interactionMessage = NpcDialogue.Get("buyer.invalid_sale_input");

        CloseInteraction();
    }


    private void OnDeclineSale()
    {
        GameLogger.LogInfo("economy", "sale_declined", "Player declined sale with buyer " + gameObject.name);
        npcInteraction.interactionMessage = NpcDialogue.Get("buyer.declined");
        CloseInteraction();
    }

    public bool IsRegularBuyer()
    {
        // Make sure currentRequest exists before accessing it
        if (currentRequest == null)
            return trust >= 80; // only use trust if no request

        return trust >= 80 || currentRequest.addictiveness >= 80;
    }

    // In CompleteSale
    private void CompleteSale(int amount, int totalPrice)
    {
        if (saleCompletedForInteraction || amount <= 0 || totalPrice < 0 || currentRequest == null ||
            currentPlayer == null || playerInventory == null)
        {
            Debug.LogWarning("[Buyer] Ignoring an invalid or duplicate sale completion.");
            return;
        }

        Item itemBeingSold = saleItem != null ? saleItem : puim.selectedTradeItem;
        int available = playerInventory.GetItemAmount(itemBeingSold);
        if (amount > available)
        {
            Debug.LogWarning("[Buyer] Sale completion ignored because the inventory changed.");
            CloseInteraction();
            return;
        }

        saleCompletedForInteraction = true;
        npcInteraction.interactionMessage = NpcDialogue.Get(
            "buyer.deal",
            "amount", amount.ToString(),
            "totalPrice", totalPrice.ToString());
        playerInventory.RemoveItem(itemBeingSold, amount);
        currentPlayer.ChangeMoney(totalPrice);
        if (itemBeingSold != null && itemBeingSold.isIllegal)
            GameMaster.ReportCompletedSale(currentPlayer.gameObject);
        trust += 5;

        // Increase addiction
        currentRequest.addictiveness += currentRequest.addictAmount;
        // Optional: clamp max addiction
        currentRequest.addictiveness = Mathf.Min(currentRequest.addictiveness, 100);

        // Resolve the manager at completion time as well as during Start. This
        // keeps a valid sale connected to stage progression if script startup
        // order left the cached reference unset.
        GameStageManager stageManager = gsm != null ? gsm : GameStageManager.Instance;
        if (stageManager != null)
            stageManager.RecordPlayerSale(amount, totalPrice);
        else
            Debug.LogError("[Buyer] Completed a sale but GameStageManager is unavailable; progression was not recorded.");

        CloseInteraction();
    }


    private void CloseInteraction()
    {
        saleFlowOpen = false;
        saleItem = null;

        if (npcInteraction != null)
            npcInteraction.EndDialogue();

        puim.yesButton.SetActive(false);
        puim.noButton.SetActive(false);
        //npcInteraction.isBtn = false;
        //npcInteraction.isInput = false;
        puim.inputQuantityGO.SetActive(false);
        puim.inputPriceGO.SetActive(false);
        puim.itemImgGo.SetActive(false);
        puim.popupImageBorder.SetActive(false);
        //npcInteraction.isMouse = false;
        player.GetComponent<PlayerController>().LockMouse();
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        puim.lockPanel.SetActive(false);
        counterOfferAccepted = false; // reset for next time
        puim.selectedTradeItem = null;  // clear memory
        if (puim.selectedItemImage != null)
        {
            puim.selectedItemImage.sprite = null; // remove sprite
            puim.selectedItemImage.enabled = false;
        }
    }

    private void OnDisable()
    {
        PoliceHeatRegistry.Unregister(this);
        saleFlowOpen = false;
    }




}
