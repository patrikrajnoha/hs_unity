using UnityEngine;

[System.Serializable]
public enum ProductCategory
{
    Utility,
    Seed,
    RawProduct,
    Ingredient,
    PackagedProduct
}

[System.Serializable]
public enum ProductQuality
{
    Standard,
    Premium,
    Superior
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class Item : ScriptableObject
{
    public string itemName;
    public Sprite icon;        // For UI
    public GameObject prefab;  // For instantiating in the world
    public bool isIllegal;
    public int basePrice;

    // Product data is intentionally kept on Item so the existing inventory,
    // buyer and dealer systems can trade new products without a second item
    // database.
    public ProductCategory category = ProductCategory.Utility;
    public ProductQuality quality = ProductQuality.Standard;
    public string productId;
    public string baseProductId;
    public string effectName;
    public int effectValue;
    public int mixCount;
    public bool isPackaged;
    public Item harvestProduct;
}
