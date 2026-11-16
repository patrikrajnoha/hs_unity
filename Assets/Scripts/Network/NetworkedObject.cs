using UnityEngine;

public class NetworkedObject : MonoBehaviour
{
    [Header("Network Settings")]
    public string objectID;
    public bool autoGenerateID = true;
    public bool isActive = true;

    [Header("Sync Settings")]
    public bool syncPosition = true;
    public bool syncRotation = true;
    public bool syncOnStart = true;

    void Start()
    {
        // Auto-generate unique ID
        if (autoGenerateID || string.IsNullOrEmpty(objectID))
        {
            objectID = $"{gameObject.name}_{transform.position.x}_{transform.position.z}";
        }

        isActive = gameObject.activeSelf;

        // If we're the host and sync on start, sync this object
        if (syncOnStart && NetworkManager.Instance != null && NetworkManager.Instance.IsHost())
        {
            Invoke(nameof(SyncToClients), 0.5f);
        }
    }

    void SyncToClients()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost())
        {
            NetworkManager.Instance.SyncObjectState(this);
        }
    }

    // Call this when the object's state changes (picked up, destroyed, moved, etc.)
    public void OnStateChanged()
    {
        isActive = gameObject.activeSelf;

        // Only host sends updates
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost())
        {
            NetworkManager.Instance.SyncObjectState(this);
        }
    }

    // For continuous sync (like moving objects)
    void Update()
    {
        if (syncPosition && NetworkManager.Instance != null && NetworkManager.Instance.IsHost())
        {
            // Only sync if moved significantly
            if (Vector3.Distance(transform.position, lastPosition) > 0.1f)
            {
                lastPosition = transform.position;
                SyncToClients();
            }
        }
    }

    private Vector3 lastPosition;
}