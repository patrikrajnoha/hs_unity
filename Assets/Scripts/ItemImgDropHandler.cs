using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemImgDropHandler : MonoBehaviour, IDropHandler
{
    private Image targetImage;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
    }

    public void OnDrop(PointerEventData eventData)
    {
        InventorySlotDragHandler source = eventData.pointerDrag == null
            ? null
            : eventData.pointerDrag.GetComponent<InventorySlotDragHandler>();

        if (source == null)
            return;

        Item item = source.GetDraggedItem();
        if (item == null)
            return;

        PlayerUIManager ui = GetComponentInParent<PlayerUIManager>();
        if (ui == null)
            return;

        ui.selectedItemImage = targetImage;
        ui.selectedTradeItem = item;

        if (targetImage != null)
        {
            targetImage.sprite = item.icon;
            targetImage.enabled = true;
        }

        NpcInteraction[] allNpcs = FindObjectsByType<NpcInteraction>(FindObjectsSortMode.None);
        foreach (NpcInteraction npc in allNpcs)
        {
            if (npc.currentNpcIner)
                npc.SetSelectedTradeItem(item);
        }
    }
}
