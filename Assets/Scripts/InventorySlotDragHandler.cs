using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class InventorySlotDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    private Inventory inventory;
    private int slotIndex;
    private Image slotImage;
    private Image dragGhost;
    private Color originalColor;

    public void Initialize(Inventory owner, int index)
    {
        inventory = owner;
        slotIndex = index;
        slotImage = GetComponent<Image>();
    }

    public Item GetDraggedItem()
    {
        return inventory != null && inventory.HasItemAt(slotIndex)
            ? inventory.GetItemAt(slotIndex)
            : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inventory == null || !inventory.HasItemAt(slotIndex) || slotImage == null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        originalColor = slotImage.color;
        Color faded = originalColor;
        faded.a *= 0.35f;
        slotImage.color = faded;
        slotImage.raycastTarget = false;

        GameObject ghostObject = new GameObject("InventoryDragGhost", typeof(RectTransform), typeof(Image));
        dragGhost = ghostObject.GetComponent<Image>();
        dragGhost.sprite = slotImage.sprite;
        dragGhost.preserveAspect = true;
        dragGhost.raycastTarget = false;
        dragGhost.color = new Color(1f, 1f, 1f, 0.85f);

        RectTransform ghostRect = ghostObject.GetComponent<RectTransform>();
        RectTransform slotRect = slotImage.rectTransform;
        ghostRect.SetParent(canvas.rootCanvas.transform, false);
        ghostRect.sizeDelta = slotRect.rect.size;
        ghostRect.SetAsLastSibling();

        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
            UpdateGhostPosition(eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        InventorySlotDragHandler source = eventData.pointerDrag == null
            ? null
            : eventData.pointerDrag.GetComponent<InventorySlotDragHandler>();

        if (source != null && source != this && source.inventory == inventory)
            inventory.MoveItem(source.slotIndex, slotIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (slotImage != null)
        {
            slotImage.color = originalColor;
            slotImage.raycastTarget = true;
        }

        if (dragGhost != null)
            Destroy(dragGhost.gameObject);

        dragGhost = null;
    }

    private void UpdateGhostPosition(PointerEventData eventData)
    {
        RectTransform ghostRect = dragGhost.rectTransform;
        RectTransform canvasRect = ghostRect.parent as RectTransform;
        if (canvasRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            ghostRect.anchoredPosition = localPoint;
        }
    }
}
