using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HighStakes.City
{
    // The board owns only its own movement lock; dialogue keeps its separate lock.
    public sealed class WarehouseGuideUI : MonoBehaviour
    {
        private static WarehouseGuideUI active;
        public static bool IsOpen => active != null && active.hud != null && active.hud.IsOpen;
        private SafehouseInventoryHud hud;
        private Inventory inventory;
        private GameObject guide;
        private TextMeshProUGUI hint, title, controls, selected, helpLabel, closeLabel, guideTitle, guideBody;
        private bool guideRequested;

        public void Initialize(SafehouseInventoryHud owner, Inventory items, Canvas canvas, GameObject panel)
        {
            active = this;
            hud = owner;
            inventory = items;
            var root = panel.transform;
            title = Label(root, new Vector2(24, -12), new Vector2(450, 32), 24);
            controls = Label(root, new Vector2(24, -51), new Vector2(450, 34), 14);
            controls.textWrappingMode = TextWrappingModes.Normal;
            selected = Label(root, new Vector2(500, -88), new Vector2(275, 125), 17);
            selected.textWrappingMode = TextWrappingModes.Normal;
            helpLabel = Button(root, new Vector2(500, -12), new Vector2(140, 32), ToggleGuide);
            closeLabel = Button(root, new Vector2(650, -12), new Vector2(126, 32), () => hud.SetInventoryOpen(false));
            var hintObject = new GameObject("Inventory Shortcut", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintObject.transform.SetParent(canvas.transform, false);
            var hintRect = hintObject.GetComponent<RectTransform>();
            hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(.5f, 0);
            hintRect.anchoredPosition = new Vector2(0, 6);
            hintRect.sizeDelta = new Vector2(800, 28);
            hint = hintObject.GetComponent<TextMeshProUGUI>();
            hint.fontSize = 17;
            hint.alignment = TextAlignmentOptions.Center;
            hint.raycastTarget = false;
            var shadow = hintObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(1, -1);

            guide = new GameObject("Warehouse Guide", typeof(RectTransform), typeof(Image));
            guide.transform.SetParent(canvas.transform, false);
            var rect = guide.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 264);
            rect.sizeDelta = new Vector2(800, 510);
            guide.GetComponent<Image>().color = new Color32(12, 22, 34, 250);
            guideTitle = Label(guide.transform, new Vector2(24, -18), new Vector2(752, 38), 26);
            guideBody = Label(guide.transform, new Vector2(24, -70), new Vector2(752, 420), 20);
            guideBody.textWrappingMode = TextWrappingModes.Normal;
            guideBody.enableAutoSizing = true;
            guideBody.fontSizeMin = 15;
            guideBody.fontSizeMax = 20;
            guideBody.lineSpacing = 2;
            guide.SetActive(false);
            LocalizationManager.LanguageChanged += RefreshLanguage;
            RefreshLanguage();
        }

        private void RefreshLanguage()
        {
            title.text = LocalizationManager.Get("ui.warehouse.inventory_title");
            controls.text = LocalizationManager.Get("ui.warehouse.controls");
            helpLabel.text = LocalizationManager.Get("ui.warehouse.help");
            closeLabel.text = LocalizationManager.Get("ui.warehouse.close");
            guideTitle.text = LocalizationManager.Get("ui.warehouse.guide_title");
            guideBody.text = LocalizationManager.Get("ui.warehouse.guide_body");
        }

        public void ToggleGuide()
        {
            guideRequested = !guideRequested;
            if (guideRequested) hud.SetInventoryOpen(true);
            guide.SetActive(guideRequested && hud.IsOpen);
        }

        private void Update()
        {
            if (hud == null || inventory == null) return;
            bool inWarehouse = CityInterior.RoomAt(transform.position).Contains("Warehouse / Production");
            if (Time.timeScale > 0f && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame &&
                (hud.IsOpen || inWarehouse) && (GetComponent<PlayerController>() == null ||
                    !GetComponent<PlayerController>().IsDialogueInputLocked || hud.IsOpen)) ToggleGuide();
            guide.SetActive(guideRequested && hud.IsOpen);
            if (!hud.IsOpen) guideRequested = false;
            hint.gameObject.SetActive(!hud.IsOpen);
            hint.text = LocalizationManager.Get(inWarehouse ? "ui.warehouse.shortcuts" : "ui.warehouse.inventory_shortcut");
            Item item = inventory.GetSelectedItem();
            selected.text = item == null ? LocalizationManager.Get("ui.warehouse.empty") :
                LocalizationManager.GetItemName(item) + "\n\n" + LocalizationManager.Get("ui.warehouse." + ItemHint(item));
        }

        private static string ItemHint(Item item)
        {
            if (ProductItemFactory.IsSeed(item)) return "use_seed";
            if (item.name == "WateringCan") return "use_can";
            if (item.name == "Pruner") return "use_pruner";
            if (item.name == "Rake") return "use_rake";
            if (item.name == "PackEmpty") return "use_bag";
            if (ProductItemFactory.IsPackagedProduct(item)) return "use_package";
            if (ProductItemFactory.IsRawProduct(item)) return "use_crop";
            return "use_after_close";
        }

        private void OnDestroy()
        {
            LocalizationManager.LanguageChanged -= RefreshLanguage;
            if (active == this) active = null;
        }

        private static TextMeshProUGUI Label(Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = new Color32(225, 239, 238, 255);
            text.raycastTarget = false;
            return text;
        }

        private static TextMeshProUGUI Button(Transform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Board Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color32(35, 76, 77, 255);
            go.GetComponent<Button>().onClick.AddListener(action);
            var text = Label(go.transform, Vector2.zero, size, 16);
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }
    }
}
