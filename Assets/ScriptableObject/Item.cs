using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class Item : ScriptableObject
{
    public string itemName;
    public Sprite icon;        // For UI
    public GameObject prefab;  // For instantiating in the world
    public bool isIllegal;
    public int basePrice; //  add this line
}
