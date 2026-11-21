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
                    puim.ShowPopup("Need an empty bag");
                return;
            }

            if (puim != null)
                puim.ShowInteractionPrompt(KeyCode.E,
                    "Pack " + productToPack.itemName);
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (!inventory.RemoveItem(productToPack, 1) ||
                    !inventory.RemoveItemByName("PackEmpty", 1))
                    return;

                Vector3 spawnPos = transform.position + transform.forward * 2f;
                GameObject go = Instantiate(pack, spawnPos, Quaternion.identity);
                go.SetActive(true);
                var pck = go.GetComponent<PickableObject>();
                if (pck != null)
                {
                    Item packagedProduct = ProductItemFactory.CreatePackagedProduct(productToPack);
                    pck.item = packagedProduct;
                    go.name = packagedProduct != null ? packagedProduct.name : "Packaged Product";
                    pck.EnableWorldPhysics();
                    pck.spawned = false;
                }
                isPacking = true;
                GameLogger.LogInfo("production", "product_packaged", "Packaged product: " + productToPack.itemName);
                SafehouseInventoryHud hud = GetComponent<SafehouseInventoryHud>();
                if (hud != null)
                    hud.ShowFeedback("PACKAGED: " + productToPack.itemName);
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

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, stationDistance + 1f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
            if (hit.transform == packagingStation || hit.transform.IsChildOf(packagingStation))
                return true;

        Vector3 direction = (packagingStation.position - playerCamera.transform.position).normalized;
        return Vector3.Dot(playerCamera.transform.forward, direction) >= 0.8f &&
            Vector3.Distance(transform.position, packagingStation.position) <= stationDistance;
    }
}
