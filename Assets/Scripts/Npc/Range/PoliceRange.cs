using UnityEngine;
using System.Collections.Generic;

public class PoliceRange : MonoBehaviour
{
    private List<GameObject> suspectsInRange = new List<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Suspect>() != null)
        {
            suspectsInRange.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<Suspect>() != null)
        {
            suspectsInRange.Remove(other.gameObject);
        }
    }

    public bool IsInRange(GameObject target)
    {
        return suspectsInRange.Contains(target);
    }

    public List<GameObject> SuspectsInRange => suspectsInRange;


}
