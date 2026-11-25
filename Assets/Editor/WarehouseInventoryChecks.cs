using System;
using System.Collections.Generic;
using TMPro;
using HighStakes.City;
using UnityEngine;
using UnityEngine.EventSystems;

public static class WarehouseInventoryChecks
{
    public static void Check()
    {
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        var inventory = player.GetComponent<Inventory>();
        var hud = player.GetComponent<SafehouseInventoryHud>();
        var guide = player.GetComponent<WarehouseGuideUI>();
        Require(inventory.inventorySlots.Length == 12, "Not all 12 inventory slots are visible");
        Require(hud != null && guide != null, "Warehouse help was not installed");
        var items = new Item[12];
        string language = LocalizationManager.CurrentLanguage;
        bool hadLanguagePreference = PlayerPrefs.HasKey("HighStakes.Language");
        try
        {
            for (int i = 0; i < 12; i++)
            {
                items[i] = ScriptableObject.CreateInstance<Item>();
                items[i].name = "InventoryCheck" + i;
                items[i].itemName = items[i].name;
                Require(inventory.AddItem(items[i]), "Could not fill slot " + i);
            }
            hud.SetInventoryOpen(true, true);
            Require(inventory.IsBoardOpen && WarehouseGuideUI.IsOpen && player.IsDialogueInputLocked,
                "Open inventory did not block world controls");
            Require(Cursor.lockState == CursorLockMode.None, "Board cursor is locked");
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            var lastIcon = inventory.inventorySlots[11];
            var hitPointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, lastIcon.rectTransform.position) };
            EventSystem.current.RaycastAll(hitPointer, hits);
            Require(hits.Count > 0 && hits[0].gameObject == lastIcon.gameObject, "Last inventory slot cannot receive mouse input");
            var source = inventory.inventorySlots[0].GetComponent<InventorySlotDragHandler>();
            var destination = inventory.inventorySlots[11].GetComponent<InventorySlotDragHandler>();
            var pointer = new PointerEventData(EventSystem.current) { pointerDrag = source.gameObject };
            source.OnBeginDrag(pointer);
            destination.OnDrop(pointer);
            source.OnEndDrag(pointer);
            Require(inventory.GetItemAt(11) == items[0] && inventory.GetItemAt(0) == items[1] &&
                inventory.GetSelectedItem() == items[0], "Drag lost an item or changed selected identity");
            Require(inventory.GetItemAmount(items[0]) == 1, "Dragging dropped or consumed an item");
            guide.ToggleGuide();
            var guidePanel = GameObject.Find("Warehouse Guide");
            Require(guidePanel != null, "Guide did not open");
            foreach (string locale in new[] { "sk", "en" })
            {
                LocalizationManager.SetLanguage(locale);
                Canvas.ForceUpdateCanvases();
                foreach (var text in guidePanel.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    text.ForceMeshUpdate();
                    Require(!text.isTextOverflowing, "Guide text overflows in " + locale);
                }
            }
            Require(hud.CloseForEscape() && !hud.IsOpen && !inventory.IsBoardOpen && !player.IsDialogueInputLocked,
                "Escape did not release inventory controls");
            // Closing the board must preserve another system's input lock.
            player.SetDialogueInputLocked(true);
            hud.SetInventoryOpen(true, true);
            hud.SetInventoryOpen(false, true);
            Require(player.IsDialogueInputLocked, "Closing inventory released the dialogue lock");
            player.SetDialogueInputLocked(false);
            Require(LocalizationManager.Get("ui.warehouse.guide_body").Contains("45"), "Warehouse guide translation missing");
            Debug.Log("WAREHOUSE INVENTORY PASS: 12 slots, real drag/drop, selection, cursor, guide, Escape and separate dialogue lock.");
        }
        finally
        {
            hud.SetInventoryOpen(false, true);
            player.SetDialogueInputLocked(false);
            LocalizationManager.SetLanguage(language);
            if (!hadLanguagePreference) PlayerPrefs.DeleteKey("HighStakes.Language");
            PlayerPrefs.Save();
            foreach (var item in items)
                if (item != null) { inventory.RemoveItem(item, inventory.GetItemAmount(item)); UnityEngine.Object.Destroy(item); }
        }
    }
    public static void CheckSimplifiedProduction()
    {
        var farm = UnityEngine.Object.FindAnyObjectByType<SafehouseFarmingSystem>();
        Require(Array.FindAll(farm.GetComponentsInChildren<PickableObject>(), p => p.CompareTag("pot")).Length == 2,
            "Warehouse must contain exactly two pots");
        var supplies = Array.FindAll(farm.GetComponentsInChildren<PickableObject>(), p => p.name.StartsWith("Supply /"));
        Require(supplies.Length == 5, "Warehouse has extra or missing supplies");
        foreach (var supply in supplies)
            Require(supply.GetComponent<Rigidbody>().isKinematic, "Supply can fall behind furniture");
        Require(farm.MixingStation == null && farm.GetComponentInChildren<LampSwitch>() == null,
            "Unused mixing/switch interactions remain");
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        var inventory = player.GetComponent<Inventory>();
        var packing = player.GetComponent<PackingSys>();
        Item package = ProductItemFactory.CreatePackagedProduct(farm.GreenBudItem);
        try
        {
            inventory.AddItem(farm.GreenBudItem);
            Require(!packing.TryPack(inventory, farm.GreenBudItem) && inventory.GetItemAmount(farm.GreenBudItem) == 1,
                "Packing without a bag lost the crop");
            inventory.AddItem(farm.PackEmptyItem);
            Require(packing.TryPack(inventory, farm.GreenBudItem) && inventory.GetItemAmount(package) == 1 &&
                inventory.GetItemAmount(farm.GreenBudItem) == 0 && inventory.GetItemAmount(farm.PackEmptyItem) == 0,
                "Packing did not deliver directly to inventory");
        }
        finally
        {
            inventory.RemoveItem(farm.GreenBudItem, inventory.GetItemAmount(farm.GreenBudItem));
            inventory.RemoveItem(farm.PackEmptyItem, inventory.GetItemAmount(farm.PackEmptyItem));
            inventory.RemoveItem(package, inventory.GetItemAmount(package));
        }
        var stage = new GameObject("Harvest regression stage");
        var decoration = new GameObject("Stem and leaves");
        decoration.transform.SetParent(stage.transform);
        var bud = new GameObject("Harvest bud", typeof(PickableObject));
        bud.tag = "bud";
        bud.transform.SetParent(stage.transform);
        Require(PlantSpawner.HasHarvestRemaining(stage), "Harvest not detected");
        UnityEngine.Object.DestroyImmediate(bud);
        var routine = (System.Collections.IEnumerator)typeof(PlantSpawner).GetMethod("DestroyWhenEmpty",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(
                player.GetComponent<PlantSpawner>(), new object[] { stage, .5f, null });
        Require(routine.MoveNext() && routine.Current is WaitForSeconds, "Decorative leaves prevent clearing the harvested pot");
        UnityEngine.Object.DestroyImmediate(stage);
        var probe = new GameObject("Interaction probe");
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            probe.transform.position = new Vector3(1000, 0, 1000);
            var cameraObject = new GameObject("Probe camera", typeof(Camera));
            cameraObject.transform.SetParent(probe.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.35f, 0);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            target.transform.position = probe.transform.position + new Vector3(0, 1.35f, 2);
            target.transform.localScale = Vector3.one * .25f;
            var pickup = target.AddComponent<PickableObject>();
            pickup.item = farm.SeedItem;
            var detector = probe.AddComponent<ObjectDetector>();
            wall.SetActive(false);
            Physics.SyncTransforms();
            detector.RefreshTarget();
            Require(detector.CurrentTarget == pickup, "First-person pickup focus failed");
            camera.transform.localPosition = new Vector3(.5f, 2, -4);
            camera.transform.LookAt(target.transform);
            detector.RefreshTarget();
            Require(detector.CurrentTarget == pickup, "Third-person pickup uses camera distance instead of player range");
            // Check the actual display positions, not only a synthetic target.
            foreach (var supply in supplies)
            {
                Vector3 approach = supply.transform.position - farm.transform.forward * 1.8f;
                approach.y = .15f;
                Require(UnityEngine.AI.NavMesh.SamplePosition(approach, out var floor, .6f, UnityEngine.AI.NavMesh.AllAreas),
                    "No reachable approach to " + supply.name);
                probe.transform.position = floor.position;
                camera.transform.localPosition = new Vector3(0, 1.35f, 0);
                camera.transform.LookAt(supply.GetComponent<Collider>().bounds.center);
                Physics.SyncTransforms();
                detector.RefreshTarget();
                Require(detector.CurrentTarget == supply, "Display cannot be selected from the aisle: " + supply.name);
            }
            probe.transform.position = new Vector3(1000, 0, 1000);
            camera.transform.localPosition = new Vector3(0, 1.35f, 0);
            camera.transform.LookAt(target.transform);
            wall.transform.position = probe.transform.position + new Vector3(0, 1.35f, 1);
            wall.transform.localScale = new Vector3(3, 3, .2f);
            wall.SetActive(true);
            Physics.SyncTransforms();
            detector.RefreshTarget();
            Require(detector.CurrentTarget == null, "Pickup focus goes through a wall");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probe);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(wall);
        }
        Debug.Log("SIMPLE WAREHOUSE PASS: two pots, five stable supplies, direct packing, first/third-person focus and wall occlusion.");
    }

    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
