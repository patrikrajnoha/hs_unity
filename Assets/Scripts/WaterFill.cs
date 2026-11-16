using UnityEngine;
using UnityEngine.InputSystem;

public class WaterFill : MonoBehaviour
{
    public float fillDistance;
    public string msg = "Press E to fill with water";
    public string msg2 = "Already full";

    private Transform player;
    private Item item;
    private PickableObject po;

    private Camera playerCamera;
    private PlayerUIManager puim;

    void Start()
    {
        //player = GameObject.FindGameObjectWithTag("Player").transform;
        

    }

    void Update()
    {
        if (GameMaster.GameOverActive)
            return;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            if (player == null)
                continue;

            Vector3 horizontalOffset = player.transform.position - transform.position;
            horizontalOffset.y = 0f;
            float distance = horizontalOffset.magnitude;

            if (distance > fillDistance)
                continue;

            playerCamera = player.GetComponentInChildren<Camera>();
            Inventory inventory = player.GetComponent<Inventory>();
            puim = player.GetComponent<PlayerUIManager>();
            if (playerCamera == null || inventory == null || puim == null)
                continue;

            if (!IsTargeted(playerCamera))
                continue;

            Item selectedItem = inventory.GetSelectedItem();
            if (selectedItem == null || selectedItem.name != "WateringCan")
                continue;

            PickableObject water = selectedItem.prefab != null
                ? selectedItem.prefab.GetComponent<PickableObject>()
                : null;

            if (inventory.IsSelectedWateringCanFull())
            {
                puim.ShowPopup(msg2);
                continue;
            }

            puim.ShowInteractionPrompt(KeyCode.E, "Fill WateringCan");
            if (Input.GetKeyDown(KeyCode.E) ||
                (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame))
            {
                inventory.SetSelectedWateringCanFull(true);
                if (water != null)
                    water.isFull = true;
            }
        }
    }

    private bool IsTargeted(Camera camera)
    {
        Ray ray = new Ray(camera.transform.position, camera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, fillDistance + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform) || transform.IsChildOf(hitTransform))
                return true;
        }

        // Some interactable fixtures (including the scene's water shelf) have
        // no usable collider on the visual mesh. Keep the interaction usable
        // by falling back to the same distance + facing check used before the
        // raycast was introduced.
        Vector3 directionToSource = (transform.position - camera.transform.position).normalized;
        return Vector3.Dot(camera.transform.forward, directionToSource) >= 0.85f;
    }
}
