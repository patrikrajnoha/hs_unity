using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


[System.Serializable]
public class ShopEntry
{
    public Item item;       // reference to the scriptable item
    public int price;       // price in store
    public int quantity;    // stock available
}


public class Shop : MonoBehaviour
{
    public ShopEntry[] shopItems;       // instead of Item[]
    public GameObject buttonPrefab;     // prefab with Button + TMP_Text + Image
    public Transform buttonParent;      // panel to hold buttons

    private PlayerStats playerStats;
    private bool shopUiInitialized;

    // Keep track of which entry has which text
    private Dictionary<ShopEntry, TMP_Text> entryTexts = new Dictionary<ShopEntry, TMP_Text>();

    private void Start()
    {
        BuildShopUI();
    }

    private void BuildShopUI()
    {
        if (shopUiInitialized)
            return;

        shopUiInitialized = true;
        playerStats = FindObjectOfType<PlayerStats>();

        var createdItems = new HashSet<Item>();
        foreach (ShopEntry entry in shopItems)
        {
            if (entry == null || entry.item == null || !createdItems.Add(entry.item))
                continue;

            GameObject btnObj = Instantiate(buttonPrefab, buttonParent);

            RectTransform buttonRect = btnObj.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 1f);
                buttonRect.anchorMax = new Vector2(0.5f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 1f);
                buttonRect.sizeDelta = new Vector2(240f, 70f);
                buttonRect.anchoredPosition = new Vector2(0f, -40f - (createdItems.Count - 1) * 80f);
            }

            // Set text
            TMP_Text text = btnObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = $"{entry.item.name}\n${entry.price} (x{entry.quantity})";
                entryTexts[entry] = text; // store reference
            }

            // Set image
            Image img = btnObj.transform.Find("Icon")?.GetComponent<Image>();
            if (img != null && entry.item.icon != null)
            {
                img.sprite = entry.item.icon;
                img.preserveAspect = true;
            }

            // Add listener
            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                ShopEntry capturedEntry = entry;
                btn.onClick.AddListener(() => OnItemClicked(capturedEntry));
            }
        }
    }


    private void OnItemClicked(ShopEntry entry)
    {
        if (entry.quantity <= 0)
        {
            GameLogger.LogWarning("economy", "purchase_failed_out_of_stock", "Out of stock: " + entry.item.name);
            Debug.Log("Out of stock: " + entry.item.name);
            return;
        }

        if (!playerStats.CanAfford(entry.price))
        {
            GameLogger.LogWarning("economy", "purchase_failed_funds", $"Cannot afford {entry.item.name} (${entry.price})");
            Debug.Log("Not enough money!");
            return;
        }

        // Pay for item
        playerStats.ChangeMoney(-entry.price);

        GameLogger.LogInfo("economy", "item_bought", $"Bought {entry.item.name} for ${entry.price} (remaining stock: {entry.quantity - 1})");
        Debug.Log($"Bought {entry.item.name} for ${entry.price}");

        // reduce stock
        entry.quantity--;

        // update UI text
        if (entryTexts.ContainsKey(entry))
        {
            entryTexts[entry].text = $"{entry.item.name}\n${entry.price} (x{entry.quantity})";
        }

        // spawn prefab in world
        if (entry.item.prefab != null)
        {
            Vector3 spawnPos = transform.position + transform.forward * 2f;
            GameObject go = Instantiate(entry.item.prefab, spawnPos, Quaternion.identity);
            var crntItem = go.GetComponent<PickableObject>();
            crntItem.spawned = true;
        }

    }
}
