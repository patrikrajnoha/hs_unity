using UnityEngine;

public class ObjectDetector : MonoBehaviour
{
    public float pickupRange = 4f;
    private PickableObject closestObject;
    private Camera playerCamera;

    private void Start()
    {
        playerCamera = this.GetComponentInChildren<Camera>();
    }

    void Update()
    {
        if (GameMaster.GameOverActive)
            return;

        FindClosestObject();

        // Highlight the closest object
        if (closestObject != null)
        {
            closestObject.Highlight(true);
            closestObject.isLooking = true;
        }
    }

    void FindClosestObject()
    {
        PickableObject[] objects = FindObjectsOfType<PickableObject>();
        float closestDistance = Mathf.Infinity;
        PickableObject nearest = null;

        foreach (PickableObject obj in objects)
        {
            float distance = Vector3.Distance(transform.position, obj.transform.position);
            obj.isInRange = false;
            obj.isLooking = false; // reset

            // Check distance and if player is roughly looking at it
            Vector3 direction = (obj.transform.position - playerCamera.transform.position).normalized;
            float dot = Vector3.Dot(playerCamera.transform.forward, direction);

            if (distance < pickupRange && dot > 0.95f) // looking at object
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    nearest = obj;
                }
            }
        }

        // Remove highlight from previous object
        if (closestObject != null && closestObject != nearest)
            closestObject.Highlight(false);

        closestObject = nearest;

        if (closestObject != null)
            closestObject.isInRange = true;
    }
}
