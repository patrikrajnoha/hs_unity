using UnityEngine;

public class PickableObject : MonoBehaviour
{
    public Item item; // Assigned ScriptableObject
    [HideInInspector] public bool isInRange = false;
    [HideInInspector] public bool isLooking = false;

    private MeshRenderer[] meshRenderers;
    private Color[][] originalColors; // store colors for each material of each renderer
    private Rigidbody rb;
    public string requiredToolName; // just the name of the tool, e.g. "Pruner"
    public bool isWatered = false; // new state for watering cup    
    public bool isFull = false;
    public bool spawned = false;


    [HideInInspector]public bool hasRequiredToolSelected = true; // default true if no tool is needed


    void Start()
    {
        // Get all MeshRenderers in this object and its children
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        originalColors = new Color[meshRenderers.Length][];

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            originalColors[i] = new Color[meshRenderers[i].materials.Length];
            for (int j = 0; j < meshRenderers[i].materials.Length; j++)
            {
                originalColors[i][j] = meshRenderers[i].materials[j].color;
            }
        }

        rb = GetComponent<Rigidbody>();

        if (this.transform.parent != null)
        {
            if (this.tag == "bud")
            {
                rb.isKinematic = true;
                
            }
        }
        else
        {
            rb.isKinematic = false;
            
            requiredToolName = "";
        }

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {

            float distance = Vector3.Distance(player.transform.position, transform.position);

            // only allow the one actually in range to pick it up
            if (distance < 3.0f) // or whatever pickup range you use
            {
                Inventory inventory = player.GetComponent<Inventory>();

                if (spawned)
                {
                    if (inventory.AddItem(item))
                    {
                        Destroy(gameObject);
                        spawned = false;
                    }
                }

            }
        }



        

    }

    void Update()
    {
        if (GameMaster.GameOverActive)
            return;

        

        if (isFull && this.tag == "waterCup")
        {
            foreach (Transform child in transform)
            {
                if (child.CompareTag("wcwater"))
                {
                    child.gameObject.SetActive(true);
                }
            }
        }

        if (!isFull && this.tag == "waterCup")
        {
            foreach (Transform child in transform)
            {
                if (child.CompareTag("wcwater"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }



        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(player.transform.position, transform.position);

            // only allow the one actually in range to pick it up
            if (distance < 4.0f) // or whatever pickup range you use
            {
                Inventory inventory = player.GetComponent<Inventory>();

                bool hasRequiredToolSelected = true;
                if (requiredToolName != "")
                    hasRequiredToolSelected = inventory.HasSelectedItem(requiredToolName);

                // Pots are fixed gameplay targets. PlantSpawner owns their
                // F/C interactions; treating them as generic pickups would
                // surface a false E prompt and could destroy the pot.
                bool isFixedPot = CompareTag("pot");

                if (!isFixedPot && CompareTag("bud") && isInRange && isLooking && hasRequiredToolSelected)
                {
                    PlayerUIManager ui = player.GetComponent<PlayerUIManager>();
                    if (ui != null)
                        ui.ShowInteractionPrompt(KeyCode.E, "Harvest");
                }
                else if (!isFixedPot && isInRange && isLooking && hasRequiredToolSelected && item != null)
                {
                    PlayerUIManager ui = player.GetComponent<PlayerUIManager>();
                    if (ui != null)
                        ui.ShowInteractionPrompt(KeyCode.E, "Pick up " + item.itemName);
                }

                if (!isFixedPot && isInRange && isLooking && Input.GetKeyDown(KeyCode.E) && hasRequiredToolSelected)
                {
                    if (inventory != null && inventory.AddItem(item))
                    {
                        Destroy(gameObject);
                    }
                    else
                    {
                        Debug.Log($"{player.name} cannot pick up {item.itemName}");
                    }
                }
            }
        }



    }


    public void Highlight(bool on)
    {
        if (meshRenderers == null || originalColors == null) return;

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] == null || i >= originalColors.Length || originalColors[i] == null)
                continue;

            Material[] materials = meshRenderers[i].materials;
            int materialCount = Mathf.Min(materials.Length, originalColors[i].Length);
            for (int j = 0; j < materialCount; j++)
            {
                if (materials[j] == null)
                    continue;

                if (on)
                {
                    materials[j].color = Color.yellow;
                    // Optional: enable emission for glow effect
                    materials[j].EnableKeyword("_EMISSION");
                    materials[j].SetColor("_EmissionColor", Color.yellow * 0.5f);
                }
                else
                {
                    materials[j].color = originalColors[i][j];
                    materials[j].SetColor("_EmissionColor", Color.black);
                    materials[j].DisableKeyword("_EMISSION");
                }
            }
        }
    }



}
