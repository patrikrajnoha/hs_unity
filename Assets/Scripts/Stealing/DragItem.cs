using UnityEngine;
using UnityEngine.EventSystems;

public class DragItem : MonoBehaviour, IDragHandler
{
    public RectTransform itemRect;

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            itemRect.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out pos
        );
        itemRect.anchoredPosition = pos;
    }


    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Noise"))
            //suspicion += 15;
            Debug.Log("u ran into noisy object.. ");


        if (col.CompareTag("ExitZone"))
            //Success();
            Debug.Log("succees yeey u pulled : " + this.gameObject.name);
    }



}
