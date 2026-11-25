using UnityEngine;
using HighStakes.City;

// Resolve one pickup before any pickup/station processes its E key.
[DefaultExecutionOrder(-100)]
public class ObjectDetector : MonoBehaviour
{
    public float pickupRange = 3f;
    public PickableObject CurrentTarget { get; private set; }
    private Camera playerCamera;

    private void Start() { playerCamera = GetComponentInChildren<Camera>(); }
    private void Update() { RefreshTarget(); }

    public void RefreshTarget()
    {
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        PickableObject nearest = null;
        float best = float.MaxValue;
        bool blocked = GameMaster.GameOverActive || WarehouseGuideUI.IsOpen || Time.timeScale <= 0;
        var controller = GetComponent<PlayerController>();
        blocked |= controller != null && controller.IsDialogueInputLocked;
        foreach (var obj in FindObjectsByType<PickableObject>(FindObjectsSortMode.None))
        {
            obj.isInRange = obj.isLooking = false;
            if (blocked || obj.item == null || obj.CompareTag("pot")) continue;
            if (CityInteractionTargeting.TryScore(transform, playerCamera, obj.transform, pickupRange, out float score) && score < best)
            {
                best = score;
                nearest = obj;
            }
        }
        if (CurrentTarget != null && CurrentTarget != nearest) CurrentTarget.Highlight(false);
        CurrentTarget = nearest;
        if (nearest == null) return;
        nearest.isInRange = nearest.isLooking = true;
        nearest.Highlight(true);
    }

    private void OnDisable()
    {
        if (CurrentTarget != null)
        {
            CurrentTarget.isInRange = CurrentTarget.isLooking = false;
            CurrentTarget.Highlight(false);
        }
        CurrentTarget = null;
    }
}
