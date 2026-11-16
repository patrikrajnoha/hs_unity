using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlantSpawner : MonoBehaviour
{
    public GameObject plantPrefab;        // The plant prefab with 4 children stages
    public GameObject stageOnePrefab;
    public GameObject dryPrefab;
    private readonly Dictionary<GameObject, GameObject> dryPlants = new Dictionary<GameObject, GameObject>();
    public Color wetColor = new Color(0.4f, 0.3f, 0.2f); // darker brown
    public Color dryColor = new Color(0.8f, 0.7f, 0.5f); // lighter brown
    public Material dirtMat; // shared material for all dirt

    public float interactionDistance = 3f; // How close the player needs to be
    public float growthTime = 5f;         // Total growth duration
    [Min(1f)] public float wateringTimeLimit = 45f; // Time to water a planted seed

    private Camera playerCamera;

    private Item item;
    private PickableObject po;
    private PlayerUIManager puim;

    public GameObject seedPrefab;


    void Start()
    {
        playerCamera = this.GetComponentInChildren<Camera>();
        po = FindObjectOfType<PickableObject>();
        puim = this.GetComponent<PlayerUIManager>();
    }

    void Update()
    {
        if (!TryGetTargetPot(out GameObject pot, out _))
            return;

        PickableObject potState = pot.GetComponent<PickableObject>();
        if (potState == null)
            potState = pot.GetComponentInParent<PickableObject>();
        if (potState == null)
            return;

        // A prefab may put the "pot" tag on a visible child while the
        // gameplay state lives on the root. Always operate on that root
        // so spawn, plant, and watering state share one object.
        pot = potState.gameObject;

        Inventory inventory = GetComponent<Inventory>();
        if (inventory == null)
            return;

        Item selectedItem = inventory.GetSelectedItem();

        if (dryPlants.ContainsKey(pot))
        {
            if (selectedItem != null && selectedItem.name == "Rake")
            {
                puim.ShowInteractionPrompt(KeyCode.Q, "Clear pot");
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    if (dryPlants.TryGetValue(pot, out GameObject dryPlant) && dryPlant != null)
                        Destroy(dryPlant);
                    dryPlants.Remove(pot);
                }
            }

            return;
        }

        bool hasPlantOrPendingStage = HasPlantOrPendingStage(pot);

        if (HasPendingStage(pot))
        {
            PickableObject water = selectedItem != null && selectedItem.name == "WateringCan" && selectedItem.prefab != null
                ? selectedItem.prefab.GetComponent<PickableObject>()
                : null;

            if (selectedItem == null || selectedItem.name != "WateringCan")
            {
                puim.ShowPopup("Select WateringCan");
            }
            else if (!inventory.IsSelectedWateringCanFull())
            {
                puim.ShowPopup("WateringCan is empty");
            }
            else if (!potState.isWatered)
            {
                puim.ShowInteractionPrompt(KeyCode.C, "Water");
                if (Input.GetKeyDown(KeyCode.C) || (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame))
                    TryWater(pot, inventory, water);
            }
        }

        if (selectedItem != null && selectedItem.name == "Seed" && !hasPlantOrPendingStage)
        {
            puim.ShowInteractionPrompt(KeyCode.F, "Plant Seed");

            if (Input.GetKeyDown(KeyCode.F))
            {
                Transform spawnPoint = FindNamedChild(pot.transform, "SpawnPoint", "spawn");
                if (stageOnePrefab == null || spawnPoint == null)
                {
                    Debug.LogWarning("Planting requirements not met for pot: " + pot.name);
                    return;
                }

                GameObject stageOne = Instantiate(stageOnePrefab, spawnPoint.position, Quaternion.identity, pot.transform);
                if (stageOne == null || !inventory.RemoveItemByName("Seed", 1))
                {
                    if (stageOne != null)
                        Destroy(stageOne);
                    return;
                }

                // Keep the planted stage available until the player has time
                // to fetch and fill the watering can. The watering action is
                // completed explicitly with C and must not expire in transit.
                StartCoroutine(WaitForWater(stageOne, pot));
                FirebaseGameLogger.LogInfo("farming", "seed_planted", "Planted seed in " + pot.name);

                Transform dirt = FindNamedChild(pot.transform, "Soil", "dirt");
                if (dirt != null)
                {
                    foreach (Transform child in dirt)
                    {
                        MeshRenderer meshRend = child.GetComponent<MeshRenderer>();
                        if (meshRend != null)
                            meshRend.SetPropertyBlock(null);
                    }
                }

                potState.isWatered = false;
            }
        }
    }

    /// <summary>
    /// Performs the real watering action after PlantSpawner has validated the
    /// target and selected item. Both keyboard input and focused tests use
    /// this path, so water is consumed only for a valid planted crop.
    /// </summary>
    public bool TryWater(GameObject pot, PickableObject water)
    {
        if (pot == null || water == null || !water.isFull)
            return false;

        PickableObject potState = pot.GetComponent<PickableObject>() ?? pot.GetComponentInParent<PickableObject>();
        if (potState == null || potState.isWatered || !HasPendingStage(potState.gameObject))
            return false;

        potState.isWatered = true;
        water.isFull = false;
        ApplyWetDirt(potState);
        FirebaseGameLogger.LogInfo("farming", "plant_watered", "Watered " + potState.name);
        return true;
    }

    private bool TryWater(GameObject pot, Inventory inventory, PickableObject water)
    {
        if (pot == null || inventory == null || !inventory.IsSelectedWateringCanFull())
            return false;

        PickableObject potState = pot.GetComponent<PickableObject>() ?? pot.GetComponentInParent<PickableObject>();
        if (potState == null || potState.isWatered || !HasPendingStage(potState.gameObject))
            return false;

        potState.isWatered = true;
        inventory.SetSelectedWateringCanFull(false);
        if (water != null)
            water.isFull = false;

        ApplyWetDirt(potState);
        FirebaseGameLogger.LogInfo("farming", "plant_watered", "Watered " + potState.name);
        return true;
    }

    private void ApplyWetDirt(PickableObject potState)
    {
        Transform dirt = FindNamedChild(potState.transform, "Soil", "dirt");
        if (dirt == null)
            return;

        foreach (Transform child in dirt)
        {
            MeshRenderer meshRend = child.GetComponent<MeshRenderer>();
            if (meshRend == null)
                continue;

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            meshRend.GetPropertyBlock(props);
            props.SetColor("_BaseColor", wetColor);
            meshRend.SetPropertyBlock(props);
        }
    }

    private bool TryGetTargetPot(out GameObject pot, out float hitDistance)
    {
        pot = null;
        hitDistance = 0f;

        if (playerCamera == null)
            return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Resolve the gameplay state from the collider's hierarchy. This
            // supports visible child colliders and keeps the interaction tied
            // to the tagged pot root instead of a particular collider layout.
            PickableObject state = hit.collider.GetComponentInParent<PickableObject>();
            if (state == null || !state.CompareTag("pot"))
                continue;

            if (!IsPotInRange(state))
                continue;

            pot = state.gameObject;
            hitDistance = hit.distance;
            return true;
        }

        // The pot collider is intentionally small, so the camera ray can hit
        // the soil or the temporary plant instead of the pot itself. Resolve
        // the nearest pot as a fallback while the player is close and looking
        // towards it, so the interaction does not depend on pixel-perfect aim.
        GameObject[] pots = GameObject.FindGameObjectsWithTag("pot");
        GameObject nearestPot = null;
        float nearestDistance = float.MaxValue;

        foreach (GameObject candidate in pots)
        {
            if (candidate == null)
                continue;

            PickableObject state = candidate.GetComponent<PickableObject>() ?? candidate.GetComponentInParent<PickableObject>();
            if (state == null || !state.CompareTag("pot") || !IsPotInRange(state))
                continue;

            Vector3 direction = state.transform.position - playerCamera.transform.position;
            float distance = direction.magnitude;
            if (distance > interactionDistance + 1f)
                continue;

            direction.Normalize();
            if (Vector3.Dot(playerCamera.transform.forward, direction) < 0.55f)
                continue;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPot = state.gameObject;
            }
        }

        if (nearestPot != null)
        {
            pot = nearestPot;
            hitDistance = nearestDistance;
            return true;
        }

        return false;
    }

    private bool IsPotInRange(PickableObject potState)
    {
        if (potState == null)
            return false;

        Vector3 offset = potState.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    private bool HasPlantOrPendingStage(GameObject pot)
    {
        if (pot == null)
            return false;

        foreach (Transform child in pot.transform)
        {
            if (child.CompareTag("weed") || (stageOnePrefab != null && child.name.StartsWith(stageOnePrefab.name)))
                return true;
        }

        return false;
    }

    private bool HasPendingStage(GameObject pot)
    {
        if (pot == null || stageOnePrefab == null)
            return false;

        foreach (Transform child in pot.transform)
        {
            if (child.name.StartsWith(stageOnePrefab.name))
                return true;
        }

        return false;
    }

    private IEnumerator GrowPlant(GameObject plant)
    {
        int stages = plant.transform.childCount;
        float stageTime = growthTime / stages;

        for (int i = 0; i < stages; i++)
        {
            // Deactivate previous stage
            if (i > 0)
                plant.transform.GetChild(i - 1).gameObject.SetActive(false);

            // Activate current stage
            Transform currentStage = plant.transform.GetChild(i);
            currentStage.gameObject.SetActive(true);

            // If this is the last stage, start monitoring for children
            if (i == stages - 1)
                StartCoroutine(DestroyWhenEmpty(currentStage.gameObject, 5f)); // 15 sec delay

            if (i < stages - 1)
                yield return new WaitForSeconds(stageTime);
        }

        Debug.Log("Plant fully grown!");
        
    }

    // Coroutine to destroy plant when last stage has no more children
    private IEnumerator DestroyWhenEmpty(GameObject lastStage, float delay)
    {
        while (lastStage.transform.childCount > 0)
            yield return null; // wait until all children are gone

        yield return new WaitForSeconds(delay);

        // Drop seeds before destroying
        DropSeeds(lastStage.transform.position);

        // Get the pot (parent of the plant)
        Transform pot = lastStage.transform.parent.parent; // because lastStage -> plantPrefab -> pot

        if (pot != null)
        {
            PickableObject potState = pot.GetComponent<PickableObject>();
            if (potState != null)
                potState.isWatered = false;

            // Reset the dirt color
            Transform dirt = FindNamedChild(pot, "Soil", "dirt");
            if (dirt != null)
            {
                foreach (Transform child in dirt)
                {
                    MeshRenderer meshRend = child.GetComponent<MeshRenderer>();
                    if (meshRend != null)
                    {
                        meshRend.SetPropertyBlock(null); // removes the override entirely
                        potState.isWatered = false;
                    }
                }
            }
        }

        // Finally destroy the plant
        Destroy(lastStage.transform.parent.gameObject);
    }



    private void DropSeeds(Vector3 dropPosition)
    {
        if (seedPrefab == null)
        {
            Debug.LogWarning("Seed prefab not assigned!");
            return;
        }

        // Always drop one seed
        Instantiate(seedPrefab, dropPosition + Vector3.up * 0.3f, Quaternion.identity);

        // 10–20% chance for an extra seed
        float chance = Random.Range(0f, 1f);
        if (chance <= Random.Range(0.1f, 0.2f))
        {
            Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0.3f, Random.Range(-0.2f, 0.2f));
            Instantiate(seedPrefab, dropPosition + offset, Quaternion.identity);
            Debug.Log("Bonus seed dropped!");
        }
        else
        {
            Debug.Log("Dropped 1 seed.");
        }
    }



    private IEnumerator WaitForWater(GameObject stageOne, GameObject pot)
    {
        if (stageOne == null || pot == null)
            yield break;

        PickableObject potState = pot.GetComponent<PickableObject>();
        if (potState == null)
            potState = pot.GetComponentInParent<PickableObject>();
        Transform spawnPoint = FindNamedChild(pot.transform, "SpawnPoint", "spawn");
        if (potState == null || spawnPoint == null)
            yield break;

        float remaining = Mathf.Max(1f, wateringTimeLimit);

        while (pot != null && stageOne != null)
        {
            // The pot or its temporary plant can be removed while this coroutine waits.
            if (pot == null || stageOne == null || potState == null || spawnPoint == null)
            {
                if (puim != null)
                    puim.HideWateringCountdown();
                yield break;
            }

            // If it was watered, continue normally
            if (potState.isWatered)
            {
                if (puim != null)
                    puim.HideWateringCountdown();

                // Spawn the real plant and start growing
                GameObject plant = Instantiate(plantPrefab, spawnPoint.position, Quaternion.identity, pot.transform);

                // Disable all stages at start
                for (int i = 0; i < plant.transform.childCount; i++)
                    plant.transform.GetChild(i).gameObject.SetActive(false);

                StartCoroutine(GrowPlant(plant));

                // Remove stageOne and exit coroutine
                Destroy(stageOne);
                yield break;
            }

            if (puim != null)
                puim.ShowWateringCountdown(remaining);

            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                if (puim != null)
                    puim.HideWateringCountdown();

                Destroy(stageOne);
                if (dryPrefab != null)
                {
                    GameObject dryPlant = Instantiate(dryPrefab, spawnPoint.position, Quaternion.identity, pot.transform);
                    dryPlants[pot] = dryPlant;
                }

                yield break;
            }

            yield return null;
        }

        if (puim != null)
            puim.HideWateringCountdown();
    }

    private static Transform FindNamedChild(Transform parent, string preferredName, string legacyName)
    {
        if (parent == null)
            return null;

        Transform child = parent.Find(preferredName);
        return child != null ? child : parent.Find(legacyName);
    }


}
