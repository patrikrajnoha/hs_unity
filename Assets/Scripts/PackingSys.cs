using System.Collections;
using System.Collections.Generic;
using HighStakes.City;
using UnityEngine;

public class PackingSys : MonoBehaviour
{

    private Item item;
    private PickableObject po;
    public string msg = "Press E to pack weed";
    public GameObject pack;
    public Transform packagingStation;
    public float stationDistance = 3f;
    private GameMaster gm;
    public bool isPacking;

    private PlayerUIManager puim;
    private Camera playerCamera;

    // Start is called before the first frame update
    void Start()
    {
        gm = FindObjectOfType<GameMaster>();
        puim = this.GetComponent<PlayerUIManager>();
        playerCamera = GetComponentInChildren<Camera>();

    }

    // Update is called once per frame
    void Update()
    {
        if (HighStakes.City.WarehouseGuideUI.IsOpen || Time.timeScale <= 0) return;
        var detector = GetComponent<ObjectDetector>();
        if (detector != null && detector.CurrentTarget != null) return;
        if (GameMaster.GameOverActive)
            return;

        Inventory inventory = this.GetComponent<Inventory>();
        if (inventory == null)
            return;
        Item selectedItem = inventory.GetSelectedItem();
        Item productToPack = FindProductToPack(inventory, selectedItem);

        if (productToPack != null)
        {
            if (packagingStation != null && !IsTargeted())
                return;

            if (inventory.GetItemAmount("PackEmpty") <= 0)
            {
                if (puim != null)
                    puim.ShowPopup(LocalizationManager.Get("ui.warehouse.need_bag"));
                return;
            }

            if (puim != null)
                puim.ShowInteractionPrompt(KeyCode.E,
                    LocalizationManager.Get("ui.warehouse.pack_action") + " " + LocalizationManager.GetItemName(productToPack));
            if (Input.GetKeyDown(KeyCode.E))
            {
                bool success = TryPack(inventory, productToPack);
                SafehouseInventoryHud hud = GetComponent<SafehouseInventoryHud>();
                if (hud != null) hud.ShowFeedback(LocalizationManager.Get(success
                    ? "ui.warehouse.packed_inventory" : "ui.warehouse.pack_failed"));
                if (!success) return;
                isPacking = true;
                GameLogger.LogInfo("production", "product_packaged", "Packaged product: " + productToPack.itemName);
                if (gm != null && !gm.isChasingSomeone && gm.criminal == null && gm.isVisible)
                {
                    Debug.Log("[ps] criminal is set to player cause he is packing");
                    gm.criminal = this.gameObject;
                    gm.crimeHappened = true;
                }
            }
        }
    }

    private Item FindProductToPack(Inventory inventory, Item selectedItem)
    {
        if (ProductItemFactory.IsRawProduct(selectedItem))
            return selectedItem;

        for (int i = 0; i < 12; i++)
        {
            Item item = inventory.GetItemAt(i);
            if (ProductItemFactory.IsRawProduct(item) && inventory.GetItemAmount(item) > 0)
                return item;
        }

        return null;
    }

    private bool IsTargeted()
    {
        if (packagingStation == null || playerCamera == null)
            return true;

        return CityInteractionTargeting.TryScore(transform, playerCamera, packagingStation, stationDistance, out _);
    }

    public bool TryPack(Inventory inventory, Item product)
    {
        if (inventory == null || !ProductItemFactory.IsRawProduct(product) ||
            inventory.GetItemAmount(product) < 1 || inventory.GetItemAmount("PackEmpty") < 1) return false;
        Item bag = null;
        for (int i = 0; i < 12; i++)
            if (inventory.GetItemAt(i) != null && inventory.GetItemAt(i).name == "PackEmpty") bag = inventory.GetItemAt(i);
        Item packaged = ProductItemFactory.CreatePackagedProduct(product);
        if (packaged == null || bag == null) return false;
        inventory.RemoveItem(product, 1);
        inventory.RemoveItem(bag, 1);
        if (inventory.AddItem(packaged)) return true;
        // Restore ingredients if the output cannot fit; no product is lost.
        inventory.AddItem(product);
        inventory.AddItem(bag);
        return false;
    }
}
