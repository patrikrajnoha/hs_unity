using System.Collections;
using System.Collections.Generic;
using HighStakes.City;
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
        if (HighStakes.City.WarehouseGuideUI.IsOpen || Time.timeScale <= 0) return;
        var detector = GetComponent<ObjectDetector>();
        if (detector != null && detector.CurrentTarget != null) return;
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
                puim.ShowInteractionPrompt(KeyCode.E,
                    LocalizationManager.GetOrFallback("ui.action.clear_pot", "Clear pot"));
                if (Input.GetKeyDown(KeyCode.E))
                {
                    if (dryPlants.TryGetValue(pot, out GameObject dryPlant) && dryPlant != null)
                        Destroy(dryPlant);
                    dryPlants.Remove(pot);
                    GameLogger.LogInfo("farming", "pot_cleared", "Cleared dead plant from " + pot.name);
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
                puim.ShowPopup(LocalizationManager.GetOrFallback("ui.notice.select_watering_can", "Select WateringCan"));
            }
            else if (!inventory.IsSelectedWateringCanFull())
            {
                puim.ShowPopup(LocalizationManager.GetOrFallback("ui.notice.watering_can_empty", "WateringCan is empty"));
            }
            else if (!potState.isWatered)
            {
                puim.ShowInteractionPrompt(KeyCode.C,
                    LocalizationManager.GetOrFallback("ui.action.water", "Water"));
                if (Input.GetKeyDown(KeyCode.C) || (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame))
                    TryWater(pot, inventory, water);
            }
        }

        if (ProductItemFactory.IsSeed(selectedItem) && !hasPlantOrPendingStage)
        {
            puim.ShowInteractionPrompt(KeyCode.F,
                LocalizationManager.GetOrFallback("ui.action.plant_seed", "Plant Seed"));

            if (Input.GetKeyDown(KeyCode.F))
            {
                Transform spawnPoint = FindNamedChild(pot.transform, "SpawnPoint", "spawn");
                if (stageOnePrefab == null || spawnPoint == null)
                {
                    Debug.LogWarning("Planting requirements not met for pot: " + pot.name);
                    return;
                }

                GameObject stageOne = Instantiate(stageOnePrefab, spawnPoint.position, Quaternion.identity, pot.transform);
                if (stageOne == null || !inventory.RemoveItem(selectedItem, 1))
                {
                    if (stageOne != null)
                        Destroy(stageOne);
                    return;
                }

                PlantCropState cropState = stageOne.GetComponent<PlantCropState>();
                if (cropState == null)
                    cropState = stageOne.AddComponent<PlantCropState>();
                cropState.seedItem = selectedItem;
                cropState.outputProduct = selectedItem.harvestProduct;
                cropState.careScore = 0.35f;

                // Keep the planted stage available until the player has time
                // to fetch and fill the watering can. The watering action is
                // completed explicitly with C and must not expire in transit.
                StartCoroutine(WaitForWater(stageOne, pot));
                GameLogger.LogInfo("farming", "seed_planted", "Planted seed in " + pot.name);

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
        GameLogger.LogInfo("farming", "plant_watered", "Watered " + potState.name);
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
        GameLogger.LogInfo("farming", "plant_watered", "Watered " + potState.name);
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

        float best = float.MaxValue;
        foreach (var candidate in GameObject.FindGameObjectsWithTag("pot"))
        {
            if (CityInteractionTargeting.TryScore(transform, playerCamera, candidate.transform,
                    interactionDistance, out float score) && score < best)
            {
                best = score;
                pot = candidate;
            }
        }
        hitDistance = best;
        return pot != null;
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
        float stageTime = growthTime / Mathf.Max(1, stages - 1);
        PlantGrowthIndicator indicator = plant.GetComponent<PlantGrowthIndicator>();
        if (indicator == null)
            indicator = plant.AddComponent<PlantGrowthIndicator>();
        indicator.Initialize(stages);

        for (int i = 0; i < stages; i++)
        {
            // Deactivate previous stage
            if (i > 0)
                plant.transform.GetChild(i - 1).gameObject.SetActive(false);

            // Activate current stage
            Transform currentStage = plant.transform.GetChild(i);
            currentStage.gameObject.SetActive(true);
            indicator.SetProgress(i / (float)stages, i + 1);

            // If this is the last stage, start monitoring for children
            if (i == stages - 1)
                StartCoroutine(DestroyWhenEmpty(currentStage.gameObject, .5f, plant.GetComponent<PlantCropState>()));

            if (i < stages - 1)
            {
                float elapsed = 0f;
                while (elapsed < stageTime)
                {
                    elapsed += Time.deltaTime;
                    indicator.SetProgress((i + Mathf.Clamp01(elapsed / stageTime)) / stages, i + 1);
                    yield return null;
                }
            }
        }

        indicator.SetProgress(1f, stages);
        Debug.Log("Plant fully grown!");
        
    }

    // Coroutine to destroy plant when last stage has no more children
    private IEnumerator DestroyWhenEmpty(GameObject lastStage, float delay, PlantCropState cropState)
    {
        // Leaves and stems remain after picking the buds; they must not keep the pot occupied forever.
        while (lastStage != null && HasHarvestRemaining(lastStage))
            yield return null;
        if (lastStage == null) yield break;

        yield return new WaitForSeconds(delay);

        // Drop seeds before destroying
        var inventory = GetComponent<Inventory>();
        Item returnedSeed = cropState != null ? cropState.seedItem : null;
        if (GetComponent<CityInputBridge>() != null && inventory != null && returnedSeed != null)
            inventory.AddItem(returnedSeed);
        else
            DropSeeds(lastStage.transform.position, returnedSeed);

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



    public static bool HasHarvestRemaining(GameObject stage)
    {
        foreach (var pickup in stage.GetComponentsInChildren<PickableObject>())
            if (pickup.CompareTag("bud")) return true;
        return false;
    }

    private void DropSeeds(Vector3 dropPosition, Item seedItem)
    {
        if (seedPrefab == null)
        {
            Debug.LogWarning("Seed prefab not assigned!");
            return;
        }

        // Always drop one seed
        GameObject seed = Instantiate(seedPrefab, dropPosition + Vector3.up * 0.3f, Quaternion.identity);
        if (seedItem != null)
            seed.GetComponent<PickableObject>().item = seedItem;
        seed.GetComponent<PickableObject>()?.EnableWorldPhysics();

        // 10–20% chance for an extra seed
        float chance = Random.Range(0f, 1f);
        if (chance <= Random.Range(0.1f, 0.2f))
        {
            Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0.3f, Random.Range(-0.2f, 0.2f));
            GameObject bonusSeed = Instantiate(seedPrefab, dropPosition + offset, Quaternion.identity);
            if (seedItem != null)
                bonusSeed.GetComponent<PickableObject>().item = seedItem;
            bonusSeed.GetComponent<PickableObject>()?.EnableWorldPhysics();
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

                PlantCropState plantedState = stageOne.GetComponent<PlantCropState>();

                // Spawn the real plant and start growing. The selected seed
                // determines which product the mature buds produce.
                GameObject plant = Instantiate(plantPrefab, spawnPoint.position, Quaternion.identity, pot.transform);

                PlantCropState cropState = plant.GetComponent<PlantCropState>();
                if (cropState == null)
                    cropState = plant.AddComponent<PlantCropState>();
                cropState.seedItem = plantedState != null ? plantedState.seedItem : null;
                cropState.outputProduct = plantedState != null ? plantedState.outputProduct : null;
                cropState.careScore = plantedState != null
                    ? Mathf.Clamp01(plantedState.careScore + Mathf.Clamp01(remaining / Mathf.Max(1f, wateringTimeLimit)) * 0.65f)
                    : 0.5f;

                if (cropState.outputProduct != null)
                {
                    cropState.outputProduct = ProductItemFactory.CreateQualityVariant(
                        cropState.outputProduct, ProductQualityRules.RollQuality(cropState.careScore));
                    AssignHarvestProduct(plant, cropState.outputProduct);
                }

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

    private void AssignHarvestProduct(GameObject plant, Item product)
    {
        if (plant == null || product == null)
            return;

        PickableObject[] buds = plant.GetComponentsInChildren<PickableObject>(true);
        foreach (PickableObject bud in buds)
        {
            if (bud != null && bud.CompareTag("bud"))
                bud.item = product;
        }
    }

    private static Transform FindNamedChild(Transform parent, string preferredName, string legacyName)
    {
        if (parent == null)
            return null;

        Transform child = parent.Find(preferredName);
        return child != null ? child : parent.Find(legacyName);
    }


}
