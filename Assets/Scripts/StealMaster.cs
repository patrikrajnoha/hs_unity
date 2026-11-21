using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StealMaster : MonoBehaviour
{
    public static StealMaster Instance { get; private set; }

    [Header("NPC Carry Item Pool")]
    [Tooltip("Items NPCs without inventories can randomly carry")]
    public List<Item> npcPossibleCarryItems = new List<Item>();

    [Header("UI Sprites")]
    [Tooltip("Sprite to show when revealing an empty slot")]
    public Sprite emptySlotSprite;

    // === Runtime data ===
    public GameObject currentNpc;
    public GameObject currentPlayer;
    private PlayerUIManager puim;
    private Inventory playerInventory;

    private Dictionary<Image, Item> slotItemMap = new Dictionary<Image, Item>();
    private List<Image> revealedSlots = new List<Image>();
    private Dictionary<Image, Coroutine> hideCoroutines = new Dictionary<Image, Coroutine>();
    // NPC memory for fake inventory
    private Dictionary<GameObject, List<InStockItem>> npcFakeInventories = new Dictionary<GameObject, List<InStockItem>>();
    private Dictionary<GameObject, float> npcInventoryTimestamps = new Dictionary<GameObject, float>();

    private const float FAKE_INVENTORY_LIFETIME = 120f; // 2 minutes

    private List<InStockItem> currentStealSource = new List<InStockItem>();

    private int currentAttempts;
    private int maxAttempts = 3;
    private int collectClicksRemaining;
    private int totalItemsFoundInSearching; // Track how many unique items we found

    private enum StealStage { None, Searching, Collecting }
    private StealStage currentStage = StealStage.None;
    private float stageTimer;
    private const float SEARCH_TIME = 10f;
    private const float COLLECT_TIME = 5f;

    private void Awake()
    {
        Instance = this;
    }

    // Called from NpcInteraction
    public void StartSteal(GameObject npc, GameObject player)
    {
        if (npc == null || player == null) return;

        currentNpc = npc;
        currentPlayer = player;

        puim = player.GetComponent<PlayerUIManager>();
        playerInventory = player.GetComponent<Inventory>();

        if (puim == null || playerInventory == null)
        {
            Debug.LogWarning("StealMaster: Missing Player UI or Inventory.");
            return;
        }

        // Stop NPC movement if possible
        NPCWanderRoadOnly wander = npc.GetComponent<NPCWanderRoadOnly>();
        if (wander != null) wander.StopAgent();

        player.GetComponent<PlayerController>().UnlockMouse();

        currentAttempts = maxAttempts;
        revealedSlots.Clear();
        slotItemMap.Clear();
        hideCoroutines.Clear();

        puim.ClearStealSlots();
        puim.ShowStealUI();

        // NEW — show popup when steal starts
        currentNpc.GetComponent<NpcInteraction>().interactionMessage = NpcDialogue.Get("steal.started");

        SetupStealSlots(npc);
        StartSearchingStage();

    }

    // === Core setup ===
    private void SetupStealSlots(GameObject npc)
    {
        INpcInventoryProvider invProvider = npc.GetComponent<INpcInventoryProvider>();

        if (invProvider != null)
        {
            currentStealSource = invProvider.GetInventory();
        }
        else
        {
            // If NPC has no real inventory, use memory or generate a new one
            if (npcFakeInventories.ContainsKey(npc))
            {
                // Check if their fake inventory expired
                if (Time.time - npcInventoryTimestamps[npc] > FAKE_INVENTORY_LIFETIME)
                {
                    npcFakeInventories[npc] = GenerateFakeInventory();
                    npcInventoryTimestamps[npc] = Time.time;
                }
            }
            else
            {
                npcFakeInventories[npc] = GenerateFakeInventory();
                npcInventoryTimestamps[npc] = Time.time;
            }

            currentStealSource = npcFakeInventories[npc];
        }


        slotItemMap.Clear();
        revealedSlots.Clear();

        List<int> available = new List<int>();
        for (int i = 0; i < puim.stealSlots.Count; i++)
            available.Add(i);

        //  ensure unique item types across slots
        HashSet<Item> usedItems = new HashSet<Item>();

        foreach (var stockItem in currentStealSource)
        {
            if (available.Count == 0) break;
            if (usedItems.Contains(stockItem.item)) continue;

            int index = Random.Range(0, available.Count);
            Image slot = puim.stealSlots[index];
            available.RemoveAt(index);

            usedItems.Add(stockItem.item);
            slotItemMap[slot] = stockItem.item;

            Button btn = slot.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnSlotClicked(slot));
        }


        foreach (var slot in puim.stealSlots)
        {
            Button btn = slot.GetComponent<Button>();
            if (btn.onClick.GetPersistentEventCount() == 0)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnSlotClicked(slot));
            }

            puim.SetSlotHidden(slot);
        }
    }

    private List<InStockItem> GenerateFakeInventory()
    {
        List<Item> pool = npcPossibleCarryItems;
        List<InStockItem> fakeInv = new List<InStockItem>();

        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("StealMaster: npcPossibleCarryItems is empty!");
            return fakeInv;
        }

        //  Detect if current NPC is a suspect
        bool isSuspect = currentNpc != null && currentNpc.GetComponent<Suspect>() != null;

        // Decide how many items
        int count = Random.Range(2, Mathf.Min(6, pool.Count + 1));

        // Create temp pools for easier filtering
        List<Item> illegalItems = pool.FindAll(i => i.isIllegal);
        List<Item> legalItems = pool.FindAll(i => !i.isIllegal);

        // Bias parameters
        float illegalChance = isSuspect ? 0.65f : 0.15f;  // 65% vs 15% per slot

        Debug.Log($"[StealMaster] Generating inventory for {(isSuspect ? "SUSPECT" : "normal NPC")} (illegalChance={illegalChance * 100}%)");

        for (int i = 0; i < count; i++)
        {
            Item chosenItem;

            // Decide whether to pick illegal or legal
            if (Random.value < illegalChance && illegalItems.Count > 0)
            {
                chosenItem = illegalItems[Random.Range(0, illegalItems.Count)];
            }
            else
            {
                chosenItem = legalItems.Count > 0
                    ? legalItems[Random.Range(0, legalItems.Count)]
                    : illegalItems[Random.Range(0, illegalItems.Count)]; // fallback
            }

            int amount = Random.Range(1, 3);
            fakeInv.Add(new InStockItem(chosenItem, amount, null));

            Debug.Log($"[StealMaster] Rolled {(chosenItem.isIllegal ? "ILLEGAL" : "legal")} item: {chosenItem.name} (x{amount})");
        }

        return fakeInv;
    }



    // === Stage control ===
    private void StartSearchingStage()
    {
        currentStage = StealStage.Searching;
        stageTimer = SEARCH_TIME;
        UpdateStealUI();
    }

    //private int totalItemsFoundInSearching; // Track how many unique items we found

    private void TransitionToCollecting()
    {
        StopAllCoroutines();
        hideCoroutines.Clear();

        // Hide ALL slots again for a "fresh" collecting round
        foreach (var slot in puim.stealSlots)
        {
            puim.SetSlotHidden(slot);
        }

        if (revealedSlots.Count > 0)
        {
            currentStage = StealStage.Collecting;
            stageTimer = COLLECT_TIME;

            // CHANGED: Set collect attempts to match number of found items
            totalItemsFoundInSearching = revealedSlots.Count; // Store the count
            collectClicksRemaining = revealedSlots.Count;

            Debug.Log($"Collecting stage! You have {COLLECT_TIME}s and {collectClicksRemaining} attempts to collect items.");
            puim.ShowPopup(NpcDialogue.Get("steal.collect_hint"));
            UpdateStealUI();
        }
        else
        {
            GameMaster.Instance.CallPolice(currentPlayer, currentNpc.transform);
            Debug.LogWarning("Nothing found! NPC noticed you searching!");
            puim.ShowPopup(NpcDialogue.Get("steal.found_nothing"));
            CloseStealUI();
        }
    }


    private void OnStageTimeout()
    {
        if (currentStage == StealStage.Searching)
            TransitionToCollecting();
        else if (currentStage == StealStage.Collecting)
        {
            Debug.LogWarning("Time's up! NPC noticed you!");
            CloseStealUI();
        }
    }

    private void Update()
    {
        if (GameMaster.GameOverActive)
            return;

        if (currentStage == StealStage.None) return;

        stageTimer -= Time.deltaTime;
        UpdateStealUI();

        if (stageTimer <= 0)
            OnStageTimeout();
    }

    // === Slot click logic ===
    private void OnSlotClicked(Image slot)
    {
        if (currentStage == StealStage.Searching)
            HandleSearchingStage(slot);
        else if (currentStage == StealStage.Collecting)
            HandleCollectingStage(slot);
    }

    private void HandleSearchingStage(Image slot)
    {
        if (currentAttempts <= 0)
            return;

        currentAttempts--;
        bool hasItem = slotItemMap.ContainsKey(slot);

        if (hasItem && !revealedSlots.Contains(slot))
        {
            Item foundItem = slotItemMap[slot];
            slot.sprite = foundItem.icon;
            revealedSlots.Add(slot);
            Debug.Log("Found: " + foundItem.itemName);

            if (hideCoroutines.ContainsKey(slot))
                StopCoroutine(hideCoroutines[slot]);

            Coroutine hideCoroutine = StartCoroutine(HideRevealedSlot(slot));
            hideCoroutines[slot] = hideCoroutine;
        }
        else
        {
            // CHANGED: Show empty sprite when clicking empty slot
            Debug.Log("Nothing here.");
            if (emptySlotSprite != null)
            {
                slot.sprite = emptySlotSprite;
                StartCoroutine(HideRevealedSlot(slot));
            }
        }

        UpdateStealUI();

        if (currentAttempts <= 0)
            StartCoroutine(DelayedTransitionToCollecting());
    }

    private IEnumerator HideRevealedSlot(Image slot)
    {
        float hideDelay = Random.Range(0.5f, 1.5f);
        yield return new WaitForSeconds(hideDelay);

        if (currentStage == StealStage.Searching)
            puim.SetSlotHidden(slot);

        if (hideCoroutines.ContainsKey(slot))
            hideCoroutines.Remove(slot);
    }

    private IEnumerator DelayedTransitionToCollecting()
    {
        yield return new WaitForSeconds(1f);
        if (currentStage == StealStage.Searching)
            TransitionToCollecting();
    }

    private void HandleCollectingStage(Image slot)
    {
        // CHANGED: Always decrease attempts on every click
        if (collectClicksRemaining <= 0) return;
        collectClicksRemaining--;

        // If this slot was one of the ones we found during searching
        bool wasFound = revealedSlots.Contains(slot);

        if (wasFound)
        {
            Item stolenItem = slotItemMap[slot];
            InStockItem stockEntry = currentStealSource.Find(x => x.item == stolenItem);

            if (stockEntry != null)
            {
                for (int i = 0; i < stockEntry.amount; i++)
                {
                    if (!playerInventory.AddItem(stolenItem))
                    {
                        Debug.Log("Inventory full, could not take all.");
                        break;
                    }
                }

                Debug.Log("Collected: " + stockEntry.itemName);
                // reveal briefly like in searching, then hide again
                slot.sprite = stolenItem.icon;
                StartCoroutine(TemporarilyRevealSlot(slot));

                // remove from found list so it acts as collected now
                revealedSlots.Remove(slot);
            }
            else
            {
                // should not happen, but treat as empty
                puim.ShowPopup(LocalizationManager.GetOrFallback("ui.notice.nothing_here", "Nothing here..."));
                // CHANGED: Show empty sprite
                if (emptySlotSprite != null)
                    slot.sprite = emptySlotSprite;
                StartCoroutine(TemporarilyRevealSlot(slot));
            }
        }
        else
        {
            // slot was never found => empty
            Debug.Log("Clicked empty slot during collecting.");
                puim.ShowPopup(LocalizationManager.GetOrFallback("ui.notice.nothing_here", "Nothing here..."));
            // CHANGED: Show empty sprite
            if (emptySlotSprite != null)
                slot.sprite = emptySlotSprite;
            StartCoroutine(TemporarilyRevealSlot(slot));
        }

        UpdateStealUI();

        // End conditions:
        // - out of collect attempts
        // - or we've collected all previously-found slots
        if (collectClicksRemaining <= 0)
        {
            Debug.Log("Collect attempts exhausted.");
            CloseStealUI();
        }
        else if (revealedSlots.Count == 0)
        {
            Debug.Log("Collected everything that was found.");
            CloseStealUI();
        }
    }


    private IEnumerator TemporarilyRevealSlot(Image slot)
    {
        Debug.Log("[temporarilyRevealSlot()");
        float revealTime = 0.9f;
        puim.SetSlotVisible(slot);
        yield return new WaitForSeconds(revealTime);

        puim.SetSlotHidden(slot);
    }



    // === UI helpers ===
    private void UpdateStealUI()
    {
        puim.UpdateStealTimer(stageTimer);

        if (currentStage == StealStage.Searching)
            puim.UpdateStealAttempts(currentAttempts, maxAttempts);
        else if (currentStage == StealStage.Collecting)
            // CHANGED: Use stored count instead of calculating from revealedSlots
            puim.UpdateStealAttempts(collectClicksRemaining, totalItemsFoundInSearching);
    }

    public void CloseStealUI()
    {
        StopAllCoroutines();
        hideCoroutines.Clear();

        puim.HideStealUI();
        currentStage = StealStage.None;
        revealedSlots.Clear();
        slotItemMap.Clear();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        currentPlayer.GetComponent<PlayerController>().LockMouse();

        NPCWanderRoadOnly wander = currentNpc.GetComponent<NPCWanderRoadOnly>();
        if (wander != null) wander.ResumeMovement();

        currentNpc = null;
        currentPlayer = null;
    }


    public string GetNpcStealHint(GameObject npc)
    {
        if (npc == null) return "";

        // If they have a real inventory:
        INpcInventoryProvider invProvider = npc.GetComponent<INpcInventoryProvider>();
        List<InStockItem> inventory = null;

        if (invProvider != null)
        {
            inventory = invProvider.GetInventory();
        }
        else if (npcFakeInventories.ContainsKey(npc))
        {
            inventory = npcFakeInventories[npc];
        }

        if (inventory == null || inventory.Count == 0)
            return NpcDialogue.Get("steal.empty_hint");

        int totalItems = 0;
        foreach (var i in inventory)
            totalItems += i.amount;

        return NpcDialogue.Get(
            "steal.inventory_hint",
            "itemTypes", inventory.Count.ToString(),
            "totalItems", totalItems.ToString());
    }


}
