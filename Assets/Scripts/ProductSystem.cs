using System.Collections.Generic;
using HighStakes.City;
using UnityEngine;

/// <summary>
/// Shared rules for crops, mixed products, quality and packaging. The project
/// creates its prototype items at runtime, so these helpers also cache derived
/// Item assets and keep inventory stacking stable.
/// </summary>
public static class ProductQualityRules
{
    public static float Multiplier(ProductQuality quality)
    {
        switch (quality)
        {
            case ProductQuality.Premium: return 1.35f;
            case ProductQuality.Superior: return 1.75f;
            default: return 1f;
        }
    }

    public static int GetBasePrice(Item item)
    {
        if (item == null)
            return 0;
        return Mathf.Max(0, Mathf.RoundToInt(item.basePrice * Multiplier(item.quality)));
    }

    public static string Label(ProductQuality quality)
    {
        switch (quality)
        {
            case ProductQuality.Premium: return "Premium";
            case ProductQuality.Superior: return "Superior";
            default: return "Standard";
        }
    }

    public static ProductQuality RollQuality(float careScore)
    {
        float roll = Random.value;
        float premiumChance = Mathf.Lerp(0.2f, 0.5f, Mathf.Clamp01(careScore));
        float superiorChance = Mathf.Lerp(0.04f, 0.16f, Mathf.Clamp01(careScore));

        if (roll < superiorChance)
            return ProductQuality.Superior;
        if (roll < superiorChance + premiumChance)
            return ProductQuality.Premium;
        return ProductQuality.Standard;
    }
}

public static class ProductItemFactory
{
    private static readonly Dictionary<string, Item> variants = new Dictionary<string, Item>();

    public static bool IsSeed(Item item)
    {
        return item != null && item.category == ProductCategory.Seed && item.harvestProduct != null;
    }

    public static bool IsRawProduct(Item item)
    {
        return item != null && item.category == ProductCategory.RawProduct && !item.isPackaged;
    }

    public static bool IsPackagedProduct(Item item)
    {
        return item != null && item.category == ProductCategory.PackagedProduct && item.isPackaged;
    }

    public static bool IsDrugProduct(Item item) => item != null && item.isIllegal &&
        (IsRawProduct(item) || IsPackagedProduct(item) || item.name == "Pack");

    public static int GetMarketPrice(Item item)
    {
        if (item == null)
            return 0;

        return Mathf.Max(0, Mathf.RoundToInt(item.basePrice * ProductQualityRules.Multiplier(item.quality)));
    }

    public static Item CreateQualityVariant(Item baseItem, ProductQuality quality)
    {
        if (baseItem == null || quality == ProductQuality.Standard && baseItem.quality == ProductQuality.Standard)
            return baseItem;

        string baseId = string.IsNullOrEmpty(baseItem.baseProductId) ? baseItem.name : baseItem.baseProductId;
        string key = baseId + "_quality_" + (int)quality;
        if (variants.TryGetValue(key, out Item cached) && cached != null)
            return cached;

        Item variant = ScriptableObject.CreateInstance<Item>();
        variant.name = key;
        variant.itemName = baseItem.itemName + " (" + ProductQualityRules.Label(quality) + ")";
        variant.icon = baseItem.icon;
        variant.prefab = baseItem.prefab;
        variant.isIllegal = baseItem.isIllegal;
        variant.basePrice = baseItem.basePrice;
        variant.category = baseItem.category;
        variant.quality = quality;
        variant.productId = baseItem.productId;
        variant.baseProductId = baseId;
        variant.effectName = baseItem.effectName;
        variant.effectValue = baseItem.effectValue;
        variant.mixCount = baseItem.mixCount;
        variant.isPackaged = baseItem.isPackaged;
        variant.harvestProduct = baseItem.harvestProduct;
        variants[key] = variant;
        return variant;
    }

    public static Item CreateMixedProduct(Item baseProduct, Item ingredient)
    {
        if (baseProduct == null || ingredient == null)
            return null;

        string baseId = string.IsNullOrEmpty(baseProduct.baseProductId)
            ? baseProduct.name
            : baseProduct.baseProductId;
        string effect = string.IsNullOrEmpty(ingredient.effectName) ? ingredient.itemName : ingredient.effectName;
        string effectId = string.IsNullOrEmpty(ingredient.productId) ? ingredient.name : ingredient.productId;
        ProductQuality quality = (ProductQuality)Mathf.Clamp(
            Mathf.Max((int)baseProduct.quality, (int)ingredient.quality), 0, 2);
        string key = baseProduct.name + "_mix_" + effectId + "_q" + (int)quality;

        if (variants.TryGetValue(key, out Item cached) && cached != null)
            return cached;

        Item mixed = ScriptableObject.CreateInstance<Item>();
        mixed.name = key;
        mixed.itemName = baseProduct.itemName + " + " + effect + " (" + ProductQualityRules.Label(quality) + ")";
        mixed.icon = baseProduct.icon;
        mixed.prefab = baseProduct.prefab;
        mixed.isIllegal = true;
        mixed.basePrice = Mathf.Max(1, ProductQualityRules.GetBasePrice(baseProduct) + ingredient.effectValue);
        mixed.category = ProductCategory.RawProduct;
        mixed.quality = quality;
        mixed.productId = baseId + "_mix_" + effectId;
        mixed.baseProductId = baseId;
        mixed.effectName = string.IsNullOrEmpty(baseProduct.effectName)
            ? effect
            : baseProduct.effectName + " + " + effect;
        mixed.effectValue = baseProduct.effectValue + ingredient.effectValue;
        mixed.mixCount = baseProduct.mixCount + 1;
        mixed.isPackaged = false;
        variants[key] = mixed;
        return mixed;
    }

    public static Item CreatePackagedProduct(Item rawProduct)
    {
        if (!IsRawProduct(rawProduct))
            return null;

        string key = rawProduct.name + "_packaged";
        if (variants.TryGetValue(key, out Item cached) && cached != null)
            return cached;

        Item packaged = ScriptableObject.CreateInstance<Item>();
        packaged.name = key;
        packaged.itemName = "Pack: " + rawProduct.itemName;
        packaged.icon = rawProduct.icon;
        packaged.prefab = rawProduct.prefab;
        packaged.isIllegal = true;
        packaged.basePrice = Mathf.Max(1, ProductQualityRules.GetBasePrice(rawProduct) + 3);
        packaged.category = ProductCategory.PackagedProduct;
        packaged.quality = rawProduct.quality;
        packaged.productId = rawProduct.productId + "_packaged";
        packaged.baseProductId = string.IsNullOrEmpty(rawProduct.baseProductId) ? rawProduct.name : rawProduct.baseProductId;
        packaged.effectName = rawProduct.effectName;
        packaged.effectValue = rawProduct.effectValue;
        packaged.mixCount = rawProduct.mixCount;
        packaged.isPackaged = true;
        variants[key] = packaged;
        return packaged;
    }

    public static bool MatchesRequest(Item item, string requestedName)
    {
        if (item == null || string.IsNullOrEmpty(requestedName))
            return false;

        if (item.name == requestedName || item.itemName == requestedName)
            return true;

        if (!string.IsNullOrEmpty(item.baseProductId) && item.baseProductId == requestedName)
            return true;

        return item.isPackaged && item.baseProductId == requestedName.Replace(" ", "");
    }

}

public sealed class PlantCropState : MonoBehaviour
{
    public Item seedItem;
    public Item outputProduct;
    [Range(0f, 1f)] public float careScore = 0.5f;
}

/// <summary>
/// A compact runtime mixing station. It uses the existing inventory and the
/// same generated safehouse world, so the feature is immediately playable in
/// the current prototype without requiring authored prefabs.
/// </summary>
public sealed class ProductMixingSystem : MonoBehaviour
{
    private Transform mixingStation;
    private Inventory inventory;
    private PlayerUIManager playerUi;
    private Item[] baseProducts;
    private Item[] ingredients;
    private Item selectedProduct;
    private bool open;
    private float previousTimeScale = 1f;
    private Rect windowRect = new Rect(0f, 0f, 470f, 520f);

    public void Initialize(Transform station, Item[] products, Item[] ingredientItems)
    {
        mixingStation = station;
        baseProducts = products;
        ingredients = ingredientItems;
        inventory = GetComponent<Inventory>();
        playerUi = GetComponent<PlayerUIManager>();
    }

    private void Update()
    {
        if (GameMaster.GameOverActive || mixingStation == null || inventory == null)
            return;

        if (open)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();
            return;
        }

        if (Vector3.Distance(transform.position, mixingStation.position) <= 3f)
        {
            if (playerUi != null)
                playerUi.ShowInteractionPrompt(KeyCode.E, "Open mixing station");

            if (Input.GetKeyDown(KeyCode.E))
                Open();
        }
    }

    private void Open()
    {
        open = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        windowRect = new Rect(Mathf.Max(12f, (Screen.width - 470f) * 0.5f),
            Mathf.Max(12f, (Screen.height - 520f) * 0.5f), 470f, 520f);
    }

    private void Close()
    {
        open = false;
        selectedProduct = null;
        Time.timeScale = previousTimeScale;
    }

    private void OnDisable()
    {
        if (open)
            Time.timeScale = previousTimeScale;
    }

    private void OnGUI()
    {
        if (!open)
            return;

        windowRect = GUI.Window(1001, windowRect, DrawWindow, "MIXING STATION");
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();
        GUILayout.Label("Combine an unpackaged product with an ingredient.");
        GUILayout.Label("Each mix adds an effect, value and sometimes quality.");
        GUILayout.Space(8f);

        GUILayout.Label("PRODUCT");
        if (baseProducts != null)
        {
            for (int i = 0; i < baseProducts.Length; i++)
            {
                Item product = baseProducts[i];
                if (!ProductItemFactory.IsRawProduct(product))
                    continue;

                int amount = inventory.GetItemAmount(product);
                if (amount > 0 && GUILayout.Button(product.itemName + "  x" + amount))
                    selectedProduct = product;
            }
        }

        // Also expose already mixed raw products so recipes can be chained.
        for (int i = 0; i < 12; i++)
        {
            Item item = inventory.GetItemAt(i);
            if (item == null || item.mixCount <= 0 || !ProductItemFactory.IsRawProduct(item))
                continue;
            if (GUILayout.Button(item.itemName + "  x" + inventory.GetItemAmount(item)))
                selectedProduct = item;
        }

        if (selectedProduct != null)
        {
            GUILayout.Space(8f);
            GUILayout.Label("SELECTED: " + selectedProduct.itemName);
            GUILayout.Label("Market value: $" + ProductItemFactory.GetMarketPrice(selectedProduct));
            GUILayout.Label("INGREDIENT");

            if (ingredients != null)
            {
                for (int i = 0; i < ingredients.Length; i++)
                {
                    Item ingredient = ingredients[i];
                    if (ingredient == null)
                        continue;

                    int amount = inventory.GetItemAmount(ingredient);
                    GUI.enabled = amount > 0;
                    if (GUILayout.Button("Mix with " + ingredient.itemName + "  x" + amount))
                        Mix(ingredient);
                    GUI.enabled = true;
                }
            }
        }
        else
        {
            GUILayout.Label("Select a harvested or already mixed product first.");
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("Press Escape to close.");
        if (GUILayout.Button("CLOSE"))
            Close();
        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void Mix(Item ingredient)
    {
        if (selectedProduct == null || ingredient == null || inventory.GetItemAmount(selectedProduct) <= 0 ||
            inventory.GetItemAmount(ingredient) <= 0)
            return;

        if (!inventory.RemoveItem(selectedProduct, 1) || !inventory.RemoveItem(ingredient, 1))
            return;

        Item result = ProductItemFactory.CreateMixedProduct(selectedProduct, ingredient);
        if (result == null || !inventory.AddItem(result))
            return;

        SafehouseInventoryHud hud = GetComponent<SafehouseInventoryHud>();
        if (hud != null)
            hud.ShowFeedback("MIXED: " + result.itemName);

        GameLogger.LogInfo("production", "product_mixed",
            "Mixed " + selectedProduct.itemName + " with " + ingredient.itemName + " -> " + result.itemName);
        selectedProduct = result;
    }
}
