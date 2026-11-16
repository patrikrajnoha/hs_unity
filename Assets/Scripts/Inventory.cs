using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class InventorySlot
{
    public Item item;
    public int quantity;
    public bool wateringCanIsFull;

    public InventorySlot(Item item, int quantity = 1)
    {
        this.item = item;
        this.quantity = quantity;
    }
}

public class Inventory : MonoBehaviour
{
    public Image[] borderPanels;

    public Image[] inventorySlots; // UI slot icons
    public KeyCode useKey = KeyCode.Mouse0;
    public float spawnDistance = 2f;

    private int inventoryLimit = 8;
    private InventorySlot[] inventoryItems;
    private int currentItemCount = 0;
    private int selectedIndex = 0;

    public GameObject shopUI;
    private GameMaster gm;

    public Sprite normalBorderSprite;
    public Sprite selectedBorderSprite;

    private TextMeshProUGUI[] quantityLabels;
    private Image[] selectionBackdrops;
    private TextMeshProUGUI[] selectionMarkers;
    private TextMeshProUGUI selectedItemLabel;
    private static Sprite runtimeWhiteSprite;

    private static readonly Color SelectionGlow = new Color32(255, 194, 92, 255);
    private static readonly Color SelectionFill = new Color(1f, 0.68f, 0.18f, 0.24f);
    private static readonly Color SlotFill = new Color(0.04f, 0.08f, 0.15f, 0.3f);
    private static readonly Color QuantityFill = new Color32(8, 13, 25, 245);


    void Start()
    {
        gm = FindObjectOfType<GameMaster>();

        inventoryItems = new InventorySlot[inventoryLimit];
        EnsureSlotVisuals();
        ClearInventoryUI();

        /*
        borderPanels = new Image[inventorySlots.Length];
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            // Find the border child by tag
            Transform borderChild = inventorySlots[i].transform.Find("itemPan"); // if you name it literally "borderItem"
            if (borderChild != null)
                borderPanels[i] = borderChild.GetComponent<Image>();
        }
        */

        // Hook buttons for each slot
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null) continue;

            // Empty slots must remain raycastable so they can also receive a drop.
            inventorySlots[i].enabled = true;
            inventorySlots[i].raycastTarget = true;

            int index = i; // capture loop variable
            Button btn = inventorySlots[i].GetComponentInParent<Button>();
            if (btn != null)
            {
                btn.enabled = true;
                btn.onClick.AddListener(() => OnSlotClicked(index));
            }

            InventorySlotDragHandler dragHandler = inventorySlots[i].GetComponent<InventorySlotDragHandler>();
            if (dragHandler == null)
                dragHandler = inventorySlots[i].gameObject.AddComponent<InventorySlotDragHandler>();

            dragHandler.Initialize(this, index);
        }

        UpdateSlotColors();
    }

    void Update()
    {
        if (GameMaster.GameOverActive)
            return;

        HandleSelection();
        HandleUse();
        HandleNumberKeys();
    }

    private void OnSlotClicked(int index)
    {
        if (index < currentItemCount && inventoryItems[index] != null)
        {
            selectedIndex = index;
            UpdateSlotColors();

            Item item = inventoryItems[index].item;

            NpcInteraction[] allNpcs = FindObjectsOfType<NpcInteraction>();
            foreach (var npc in allNpcs)
            {
                if (npc.currentNpcIner)
                {
                    npc.SetSelectedTradeItem(item); //  call it on that NPC
                    //break; // stop looping once you found it
                }
            }


        }
    }

    void ClearInventoryUI()
    {
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null) continue;

            inventorySlots[i].sprite = null;
            inventorySlots[i].enabled = true;
            inventorySlots[i].raycastTarget = true;

            SetQuantityLabel(i, string.Empty, false);
        }
    }

    public bool AddItem(Item item)
    {
        if (item == null) return false;

        // 1. Check if item already exists -> stack
        for (int i = 0; i < currentItemCount; i++)
        {
            if (item.name != "WateringCan" && inventoryItems[i] != null && inventoryItems[i].item == item)
            {
                inventoryItems[i].quantity++;
                UpdateSlotUI(i);
                FirebaseGameLogger.LogInfo("inventory", "item_added", "Stacked " + item.itemName + " (x" + inventoryItems[i].quantity + ")");
                return true;
            }
        }

        // 2. If new item, check space
        if (currentItemCount >= inventoryLimit)
        {
            FirebaseGameLogger.LogWarning("inventory", "inventory_full", "Could not add " + item.itemName);
            return false;
        }

        inventoryItems[currentItemCount] = new InventorySlot(item, 1);
        UpdateSlotUI(currentItemCount);
        currentItemCount++;
        FirebaseGameLogger.LogInfo("inventory", "item_added", "Added " + item.itemName + " (x1)");
        return true;
    }


    void UpdateSlotUI(int index)
    {
        var slot = inventoryItems[index];
        if (slot != null && slot.item != null)
        {
            inventorySlots[index].sprite = slot.item.icon;
            inventorySlots[index].enabled = true;

            // Keep the count visible even when it is one. This makes every
            // occupied slot immediately readable instead of relying on a
            // scene-authored TMP child being present.
            SetQuantityLabel(index, "x" + slot.quantity, true);
        }
        else
        {
            if (inventorySlots[index] == null) return;

            inventorySlots[index].sprite = null;
            inventorySlots[index].enabled = true;
            inventorySlots[index].raycastTarget = true;
            SetQuantityLabel(index, string.Empty, false);
        }
    }

    void HandleSelection()
    {
        if (currentItemCount <= 0)
            return;

        // mouseScrollDelta gives one clean event per wheel tick. Fall back to
        // the legacy axis for projects using the old input manager.
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f))
            scroll = Input.GetAxisRaw("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        int direction = scroll > 0f ? -1 : 1;
        selectedIndex = (selectedIndex + direction + currentItemCount) % currentItemCount;
        UpdateSlotColors();
    }

    void HandleNumberKeys()
    {
        for (int i = 0; i < inventoryLimit; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (i < currentItemCount)
                {
                    selectedIndex = i;
                    UpdateSlotColors();
                }
            }
        }
    }

    void UpdateSlotColors()
    {
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            bool isSelected = i == selectedIndex && HasItemAt(i);

            if (borderPanels != null && i < borderPanels.Length && borderPanels[i] != null)
            {
                borderPanels[i].sprite = (i == selectedIndex)
                    ? selectedBorderSprite
                    : normalBorderSprite;
            }

            if (selectionBackdrops != null && i < selectionBackdrops.Length && selectionBackdrops[i] != null)
            {
                selectionBackdrops[i].color = isSelected ? SelectionFill : SlotFill;
                Outline outline = selectionBackdrops[i].GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = isSelected
                        ? new Color(SelectionGlow.r, SelectionGlow.g, SelectionGlow.b, 0.98f)
                        : new Color(SelectionGlow.r, SelectionGlow.g, SelectionGlow.b, 0.18f);
                    outline.effectDistance = isSelected ? new Vector2(3f, -3f) : new Vector2(1f, -1f);
                }
            }

            if (selectionMarkers != null && i < selectionMarkers.Length && selectionMarkers[i] != null)
                selectionMarkers[i].gameObject.SetActive(isSelected);
        }

        UpdateSelectedItemLabel();
    }

    private void EnsureSlotVisuals()
    {
        if (inventorySlots == null)
            return;

        quantityLabels = new TextMeshProUGUI[inventorySlots.Length];
        selectionBackdrops = new Image[inventorySlots.Length];
        selectionMarkers = new TextMeshProUGUI[inventorySlots.Length];

        Transform inventoryRoot = null;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null)
                continue;

            Transform slotRoot = inventorySlots[i].transform.parent != null
                ? inventorySlots[i].transform.parent
                : inventorySlots[i].transform;

            if (inventoryRoot == null)
                inventoryRoot = slotRoot.parent;

            selectionBackdrops[i] = EnsureSelectionBackdrop(slotRoot);
            quantityLabels[i] = EnsureQuantityBadge(slotRoot);
            selectionMarkers[i] = EnsureSelectionMarker(slotRoot);
        }

        if (inventoryRoot != null)
            selectedItemLabel = EnsureSelectedItemLabel(inventoryRoot);
    }

    private static Image EnsureSelectionBackdrop(Transform slotRoot)
    {
        Transform existing = slotRoot.Find("InventorySelectionGlow");
        GameObject glow = existing != null
            ? existing.gameObject
            : new GameObject("InventorySelectionGlow", typeof(RectTransform), typeof(Image), typeof(Outline));

        if (existing == null)
            glow.transform.SetParent(slotRoot, false);

        glow.transform.SetAsFirstSibling();
        Image image = glow.GetComponent<Image>();
        image.sprite = GetRuntimeWhiteSprite();
        image.raycastTarget = false;

        RectTransform rect = glow.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-6f, -6f);
        rect.offsetMax = new Vector2(6f, 6f);

        Outline outline = glow.GetComponent<Outline>();
        outline.useGraphicAlpha = true;
        return image;
    }

    private static TextMeshProUGUI EnsureQuantityBadge(Transform slotRoot)
    {
        Transform existing = slotRoot.Find("InventoryQuantityBadge");
        GameObject badge = existing != null
            ? existing.gameObject
            : new GameObject("InventoryQuantityBadge", typeof(RectTransform), typeof(Image));

        if (existing == null)
            badge.transform.SetParent(slotRoot, false);

        Image background = badge.GetComponent<Image>();
        background.sprite = GetRuntimeWhiteSprite();
        background.color = QuantityFill;
        background.raycastTarget = false;

        RectTransform badgeRect = badge.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 0f);
        badgeRect.anchorMax = new Vector2(1f, 0f);
        badgeRect.pivot = new Vector2(1f, 0f);
        badgeRect.anchoredPosition = new Vector2(-3f, 3f);
        badgeRect.sizeDelta = new Vector2(36f, 25f);

        Outline badgeOutline = badge.GetComponent<Outline>();
        if (badgeOutline == null)
            badgeOutline = badge.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        badgeOutline.effectDistance = new Vector2(1f, -1f);
        badgeOutline.useGraphicAlpha = true;

        Transform textTransform = badge.transform.Find("Text");
        GameObject textObject = textTransform != null
            ? textTransform.gameObject
            : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        if (textTransform == null)
            textObject.transform.SetParent(badge.transform, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 16f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return text;
    }

    private static TextMeshProUGUI EnsureSelectionMarker(Transform slotRoot)
    {
        Transform existing = slotRoot.Find("InventorySelectedMarker");
        GameObject marker = existing != null
            ? existing.gameObject
            : new GameObject("InventorySelectedMarker", typeof(RectTransform), typeof(TextMeshProUGUI));

        if (existing == null)
            marker.transform.SetParent(slotRoot, false);

        TextMeshProUGUI text = marker.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 11f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = SelectionGlow;
        text.text = "SELECTED";
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        RectTransform rect = marker.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 3f);
        rect.sizeDelta = new Vector2(110f, 20f);

        Shadow shadow = marker.GetComponent<Shadow>();
        if (shadow == null)
            shadow = marker.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;

        marker.SetActive(false);
        return text;
    }

    private static TextMeshProUGUI EnsureSelectedItemLabel(Transform inventoryRoot)
    {
        Transform existing = inventoryRoot.Find("InventorySelectedLabel");
        GameObject label = existing != null
            ? existing.gameObject
            : new GameObject("InventorySelectedLabel", typeof(RectTransform), typeof(TextMeshProUGUI));

        if (existing == null)
            label.transform.SetParent(inventoryRoot, false);

        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 14f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Left;
        text.color = SelectionGlow;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -8f);
        rect.sizeDelta = new Vector2(320f, 22f);

        Shadow shadow = label.GetComponent<Shadow>();
        if (shadow == null)
            shadow = label.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;
        return text;
    }

    private void SetQuantityLabel(int index, string value, bool visible)
    {
        if (quantityLabels == null || index < 0 || index >= quantityLabels.Length || quantityLabels[index] == null)
            return;

        quantityLabels[index].text = value;
        quantityLabels[index].transform.parent.gameObject.SetActive(visible);
    }

    private void UpdateSelectedItemLabel()
    {
        if (selectedItemLabel == null)
            return;

        Item selectedItem = GetSelectedItem();
        if (selectedItem == null)
        {
            selectedItemLabel.text = "SELECT ITEM";
            selectedItemLabel.color = new Color32(157, 173, 198, 255);
            return;
        }

        string itemName = string.IsNullOrWhiteSpace(selectedItem.itemName)
            ? selectedItem.name
            : selectedItem.itemName;
        selectedItemLabel.text = "SELECTED  •  " + itemName;
        selectedItemLabel.color = SelectionGlow;
    }

    private static Sprite GetRuntimeWhiteSprite()
    {
        if (runtimeWhiteSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            runtimeWhiteSprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        return runtimeWhiteSprite;
    }



    void HandleUse()
    {
        /*
        if (gm.mouseVis)
        {
            return;
        }
        */

        /*
        if (shopUI.activeInHierarchy)
            return; // block dropping when shop is open
        */

        if (Input.GetKeyDown(useKey) && currentItemCount > 0 && selectedIndex < currentItemCount)
        {
            UseItem(selectedIndex);
        }
    }

    void UseItem(int index)
    {
        if (index < 0 || index >= currentItemCount) return; // prevent out of range
        if (inventoryItems[index] == null) return;

        Item item = inventoryItems[index].item;
        if (item != null && item.prefab != null)
        {
            Vector3 spawnPos = transform.position + transform.forward * spawnDistance;
            Instantiate(item.prefab, spawnPos, Quaternion.identity);
        }

        FirebaseGameLogger.LogInfo("inventory", "item_used", "Used " + (item != null ? item.itemName : "unknown item"));
        RemoveItem(index);
    }

    void RemoveItem(int index)
    {
        if (inventoryItems[index] == null) return;

        inventoryItems[index].quantity--;

        if (inventoryItems[index].quantity > 0)
        {
            UpdateSlotUI(index);
            return;
        }

        // Shift items left if stack is empty
        for (int i = index; i < currentItemCount - 1; i++)
        {
            inventoryItems[i] = inventoryItems[i + 1];
            UpdateSlotUI(i);
        }

        inventoryItems[currentItemCount - 1] = null;
        inventorySlots[currentItemCount - 1].sprite = null;
        inventorySlots[currentItemCount - 1].enabled = true;
        inventorySlots[currentItemCount - 1].raycastTarget = true;

        SetQuantityLabel(currentItemCount - 1, string.Empty, false);

        currentItemCount--;
        selectedIndex = currentItemCount > 0 ? Mathf.Clamp(selectedIndex, 0, currentItemCount - 1) : 0;
        UpdateSlotColors();
    }

    public bool HasItemByName(string toolName)
    {
        for (int i = 0; i < currentItemCount; i++)
        {
            if (inventoryItems[i] != null && inventoryItems[i].item != null &&
                inventoryItems[i].item.name == toolName)
                return true;
        }
        return false;
    }

    public void HasIllegal()
    {
        gm.hasIllegal = false;
        for (int i = 0; i < currentItemCount; i++)
        {
            if (inventoryItems[i] != null && inventoryItems[i].item != null && inventoryItems[i].item.isIllegal)
            {
                gm.hasIllegal = true;
            }

        }

    }

    public void RemoveAllIllegal()
    {
        for (int i = currentItemCount - 1; i >= 0; i--)
        {
            var slot = inventoryItems[i];
            if (slot != null && slot.item != null && slot.item.isIllegal)
            {
                //Debug.Log($"[inv] Removing ALL of illegal item: {slot.item.name} x{slot.quantity}");

                // Remove the entire stack at once
                inventoryItems[i] = null;

                // Shift everything left after removal
                for (int j = i; j < currentItemCount - 1; j++)
                {
                    inventoryItems[j] = inventoryItems[j + 1];
                    UpdateSlotUI(j);
                }

                inventoryItems[currentItemCount - 1] = null;
                inventorySlots[currentItemCount - 1].sprite = null;
                inventorySlots[currentItemCount - 1].enabled = true;
                inventorySlots[currentItemCount - 1].raycastTarget = true;

                SetQuantityLabel(currentItemCount - 1, string.Empty, false);

                currentItemCount--;
            }
        }

        selectedIndex = currentItemCount > 0 ? Mathf.Clamp(selectedIndex, 0, currentItemCount - 1) : 0;
        UpdateSlotColors();
        gm.hasIllegal = false;
    }



    public bool HasSelectedItem(string itemName)
    {
        if (selectedIndex < 0 || selectedIndex >= currentItemCount) return false;
        if (inventoryItems[selectedIndex] == null) return false;

        return inventoryItems[selectedIndex].item.name == itemName;
    }

    public Item GetSelectedItem()
    {
        if (selectedIndex >= 0 && selectedIndex < currentItemCount)
            return inventoryItems[selectedIndex]?.item;
        return null;
    }

    public bool IsSelectedWateringCanFull()
    {
        if (selectedIndex < 0 || selectedIndex >= currentItemCount)
            return false;

        InventorySlot slot = inventoryItems[selectedIndex];
        return slot != null && slot.item != null && slot.item.name == "WateringCan" && slot.wateringCanIsFull;
    }

    public bool SetSelectedWateringCanFull(bool isFull)
    {
        if (selectedIndex < 0 || selectedIndex >= currentItemCount)
            return false;

        InventorySlot slot = inventoryItems[selectedIndex];
        if (slot == null || slot.item == null || slot.item.name != "WateringCan")
            return false;

        slot.wateringCanIsFull = isFull;
        return true;
    }

    public bool HasItemAt(int index)
    {
        return index >= 0 && index < currentItemCount &&
               inventoryItems[index] != null && inventoryItems[index].item != null;
    }

    public Item GetItemAt(int index)
    {
        return HasItemAt(index) ? inventoryItems[index].item : null;
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (!HasItemAt(fromIndex) || currentItemCount <= 1) return;

        toIndex = Mathf.Clamp(toIndex, 0, currentItemCount - 1);
        if (fromIndex == toIndex) return;

        InventorySlot movedSlot = inventoryItems[fromIndex];

        if (fromIndex < toIndex)
        {
            for (int i = fromIndex; i < toIndex; i++)
                inventoryItems[i] = inventoryItems[i + 1];
        }
        else
        {
            for (int i = fromIndex; i > toIndex; i--)
                inventoryItems[i] = inventoryItems[i - 1];
        }

        inventoryItems[toIndex] = movedSlot;

        for (int i = 0; i < currentItemCount; i++)
            UpdateSlotUI(i);

        if (selectedIndex == fromIndex)
            selectedIndex = toIndex;
        else if (fromIndex < toIndex && selectedIndex > fromIndex && selectedIndex <= toIndex)
            selectedIndex--;
        else if (fromIndex > toIndex && selectedIndex >= toIndex && selectedIndex < fromIndex)
            selectedIndex++;

        UpdateSlotColors();
        FirebaseGameLogger.LogDebug("inventory", "items_reordered", "Moved slot " + fromIndex + " to " + toIndex);
    }

    public bool RemoveItemByName(string itemName, int amount = 1)
    {
        for (int i = 0; i < currentItemCount; i++)
        {
            if (inventoryItems[i] != null && inventoryItems[i].item.name == itemName)
            {
                inventoryItems[i].quantity -= amount;

                if (inventoryItems[i].quantity <= 0)
                {
                    // remove the slot
                    for (int j = i; j < currentItemCount - 1; j++)
                    {
                        inventoryItems[j] = inventoryItems[j + 1];
                        UpdateSlotUI(j);
                    }

                    inventoryItems[currentItemCount - 1] = null;
                    inventorySlots[currentItemCount - 1].sprite = null;
                    inventorySlots[currentItemCount - 1].enabled = true;
                    inventorySlots[currentItemCount - 1].raycastTarget = true;

                    SetQuantityLabel(currentItemCount - 1, string.Empty, false);

                    currentItemCount--;
                    selectedIndex = currentItemCount > 0 ? Mathf.Clamp(selectedIndex, 0, currentItemCount - 1) : 0;
                    UpdateSlotColors();
                }
                else
                {
                    UpdateSlotUI(i);
                }

                FirebaseGameLogger.LogInfo("inventory", "item_removed", "Removed " + itemName + " (x" + amount + ")");
                return true; // removed successfully
            }
        }
        return false; // not found
    }


    public int GetItemAmount(string itemName)
    {
        int total = 0;
        for (int i = 0; i < currentItemCount; i++)
        {
            if (inventoryItems[i] != null && inventoryItems[i].item != null &&
                inventoryItems[i].item.name == itemName)
            {
                total += inventoryItems[i].quantity;
            }
        }
        return total;
    }


}
