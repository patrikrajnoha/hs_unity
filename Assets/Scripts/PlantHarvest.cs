using UnityEngine;

public class PlantHarvest : MonoBehaviour
{
    private bool fullyGrown = false;

    public void SetFullyGrown(bool value)
    {
        fullyGrown = value;
    }

    // Call this when player interacts (optional)
    public void EnableBuds()
    {
        if (!fullyGrown) return;

        // Enable all fruits (children with PickableObject)
        PickableObject[] buds = GetComponentsInChildren<PickableObject>(true);
        foreach (PickableObject bud in buds)
        {
            bud.isInRange = false; // reset
        }
    }
}
