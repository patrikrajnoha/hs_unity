using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HighStakes.City
{
    /// <summary>
    /// Builds the safehouse farming corner from the same primitive palette as
    /// OldQuarterBootstrap and wires the existing farming/inventory scripts to
    /// the generated Old Quarter player.
    /// </summary>
    public sealed class SafehouseFarmingSystem : MonoBehaviour
    {
        public Item SeedItem { get; private set; }
        public Item WateringCanItem { get; private set; }
        public Item PrunerItem { get; private set; }
        public Item RakeItem { get; private set; }
        public Item PackEmptyItem { get; private set; }
        public Item GreenBudItem { get; private set; }
        public Item PackItem { get; private set; }
        public Item MushroomSeedItem { get; private set; }
        public Item CocaSeedItem { get; private set; }
        public Item MushroomItem { get; private set; }
        public Item CocaLeafItem { get; private set; }
        public Item CukeItem { get; private set; }
        public Item EnergyDrinkItem { get; private set; }
        public Item ParacetamolItem { get; private set; }
        public Item FocusTonicItem { get; private set; }

        public GameObject PlantPrefab { get; private set; }
        public GameObject StageOnePrefab { get; private set; }
        public GameObject DryPrefab { get; private set; }
        public GameObject SeedPrefab { get; private set; }
        public GameObject PackPrefab { get; private set; }
        public GameObject IngredientPrefab { get; private set; }
        public Transform PackagingStation { get; private set; }
        public Transform MixingStation { get; private set; }

        private Transform farmRoot;
        private Transform templatesRoot;
        private Material stone;
        private Material trim;
        private Material glass;
        private Material navy;
        private Material mint;
        private Material soil;
        private Material leaf;
        private Material water;
        private bool built;

        public void Build(Transform room, Material stoneMaterial, Material trimMaterial,
            Material glassMaterial, Material navyMaterial, Material mintMaterial)
        {
            if (built)
                return;

            built = true;
            stone = stoneMaterial;
            trim = trimMaterial;
            glass = glassMaterial;
            navy = navyMaterial;
            mint = mintMaterial;
            soil = MakeMaterial("Safehouse soil", new Color32(92, 57, 42, 255));
            leaf = MakeMaterial("Safehouse leaves", new Color32(55, 142, 91, 255));
            water = MakeMaterial("Safehouse water", new Color32(42, 154, 245, 255));

            farmRoot = new GameObject("Safehouse Farming Corner").transform;
            farmRoot.SetParent(room, false);
            templatesRoot = new GameObject("Safehouse Farming Runtime Templates").transform;
            templatesRoot.SetParent(room, false);
            templatesRoot.localPosition = new Vector3(0f, -1000f, 0f);

            CreateItemsAndTemplates();
            BuildEquipment(room);
        }

        public void ConfigurePlayer(GameObject player)
        {
            if (!built || player == null)
                return;

            EnsureEventSystem();
            FarmingUiReferences ui = BuildInventoryUi();

            var playerUi = player.AddComponent<PlayerUIManager>();
            playerUi.popupPanel = ui.promptPanel;
            playerUi.popupImageBorder = ui.promptPanel;
            playerUi.popupText = ui.promptText;
            playerUi.wateringCountdownText = ui.wateringCountdown;

            var inventory = player.AddComponent<Inventory>();
            inventory.inventorySlots = ui.slots;

            player.AddComponent<ObjectDetector>();

            var plantSpawner = player.AddComponent<PlantSpawner>();
            plantSpawner.plantPrefab = PlantPrefab;
            plantSpawner.stageOnePrefab = StageOnePrefab;
            plantSpawner.dryPrefab = DryPrefab;
            plantSpawner.seedPrefab = SeedPrefab;
            plantSpawner.growthTime = 18f;
            plantSpawner.wateringTimeLimit = 45f;
            plantSpawner.interactionDistance = 3f;

            var packing = player.AddComponent<PackingSys>();
            packing.pack = PackPrefab;
            packing.packagingStation = PackagingStation;
            packing.stationDistance = 3f;

            var mixing = player.AddComponent<ProductMixingSystem>();
            mixing.Initialize(MixingStation,
                new[] { GreenBudItem, MushroomItem, CocaLeafItem },
                new[] { CukeItem, EnergyDrinkItem, ParacetamolItem, FocusTonicItem });

            var hud = player.AddComponent<SafehouseInventoryHud>();
            hud.Initialize(inventory, ui.waterStatus, ui.selectedStatus, ui.feedback, ui.canvas,
                ui.inventoryPanel);
        }

        private void CreateItemsAndTemplates()
        {
            GreenBudItem = CreateProductItem("GreenBud", "Green Bud", new Color32(85, 180, 104, 255), 12);
            MushroomItem = CreateProductItem("Mushroom", "Mushrooms", new Color32(200, 132, 176, 255), 18);
            CocaLeafItem = CreateProductItem("CocaLeaf", "Coca Leaves", new Color32(90, 190, 120, 255), 24);

            SeedItem = CreateSeedItem("Seed", "Green Bud Seed", new Color32(230, 190, 94, 255), GreenBudItem);
            MushroomSeedItem = CreateSeedItem("MushroomSpore", "Mushroom Spores", new Color32(205, 145, 210, 255), MushroomItem);
            CocaSeedItem = CreateSeedItem("CocaSeed", "Coca Seeds", new Color32(150, 210, 110, 255), CocaLeafItem);
            WateringCanItem = CreateItem("WateringCan", "Watering Can", new Color32(84, 160, 184, 255), false);
            PrunerItem = CreateItem("Pruner", "Pruning Shears", new Color32(221, 113, 89, 255), false);
            RakeItem = CreateItem("Rake", "Rake", new Color32(191, 135, 80, 255), false);
            PackEmptyItem = CreateItem("PackEmpty", "Empty Bag", new Color32(199, 165, 113, 255), false);
            PackItem = CreateItem("Pack", "Packed Product", new Color32(183, 113, 67, 255), true);

            CukeItem = CreateIngredientItem("Cuke", "Cuke", new Color32(110, 205, 120, 255), "Energized", 8, ProductQuality.Standard);
            EnergyDrinkItem = CreateIngredientItem("EnergyDrink", "Energy Drink", new Color32(235, 105, 75, 255), "Athletic", 12, ProductQuality.Premium);
            ParacetamolItem = CreateIngredientItem("Paracetamol", "Paracetamol", new Color32(220, 220, 236, 255), "Soothed", 10, ProductQuality.Standard);
            FocusTonicItem = CreateIngredientItem("FocusTonic", "Focus Tonic", new Color32(125, 140, 230, 255), "Focused", 16, ProductQuality.Premium);

            SeedPrefab = CreatePickupTemplate("Seed", SeedItem, PrimitiveType.Sphere, glass, "Untagged", "", new Vector3(0.18f, 0.12f, 0.18f));
            SeedItem.prefab = SeedPrefab;
            MushroomSeedItem.prefab = SeedPrefab;
            CocaSeedItem.prefab = SeedPrefab;
            WateringCanItem.prefab = CreateWateringCanTemplate();
            PrunerItem.prefab = CreatePrunerTemplate();
            RakeItem.prefab = CreateRakeTemplate();
            PackEmptyItem.prefab = CreatePickupTemplate("PackEmpty", PackEmptyItem, PrimitiveType.Cube, stone, "Untagged", "", new Vector3(0.45f, 0.08f, 0.32f));
            GreenBudItem.prefab = CreateBudTemplate("GreenBud", GreenBudItem);
            MushroomItem.prefab = GreenBudItem.prefab;
            CocaLeafItem.prefab = GreenBudItem.prefab;
            PackPrefab = CreatePickupTemplate("Pack", PackItem, PrimitiveType.Cube, trim, "pack", "", new Vector3(0.35f, 0.5f, 0.18f));
            PackItem.prefab = PackPrefab;
            IngredientPrefab = CreatePickupTemplate("Ingredient", CukeItem, PrimitiveType.Capsule, glass, "Untagged", "", new Vector3(0.2f, 0.3f, 0.2f));
            CukeItem.prefab = IngredientPrefab;
            EnergyDrinkItem.prefab = IngredientPrefab;
            ParacetamolItem.prefab = IngredientPrefab;
            FocusTonicItem.prefab = IngredientPrefab;

            StageOnePrefab = CreateStageOneTemplate();
            PlantPrefab = CreatePlantTemplate();
            DryPrefab = CreateDryTemplate();
        }

        private void BuildEquipment(Transform room)
        {
            // Layout zones inside the room:
            // left wall = growing, front-left = water and supplies,
            // front-right = packing, back-right = workshop and rest.
            // The centre remains open as a clear route from the door.
            CreateShelf(new Vector3(-3.25f, 0.85f, 1.25f), new Vector3(1.0f, 1.7f, 4.2f));

            float[] potDepths = { -0.35f, 0.95f, 2.25f, 3.55f };
            for (int i = 0; i < potDepths.Length; i++)
                CreatePot(i + 1, new Vector3(-2.15f, 0.55f, potDepths[i]));

            CreateGrowLight("Grow Light A", new Vector3(-2.1f, 3.1f, 0.25f));
            CreateGrowLight("Grow Light B", new Vector3(-2.1f, 3.1f, 2.8f));

            CreateWaterSource(new Vector3(-4.15f, 0.55f, -3.35f));
            MixingStation = CreateMixingWorkbench(new Vector3(2.75f, 0.55f, -1.65f));
            PackagingStation = CreatePackagingWorkbench(new Vector3(2.75f, 0.55f, -3.55f));

            // The back wall is around local Z 3.4. Keep the supply shelf
            // centred on that wall instead of placing it beyond the room,
            // where the old position clipped through the corner.
            CreateSupplyShelf(new Vector3(0f, 0.95f, 3.0f));
            SpawnPickup(SeedItem, new Vector3(-0.5f, 1.45f, 2.72f), "Supply / Seeds");
            SpawnPickup(MushroomSeedItem, new Vector3(-0.05f, 1.45f, 2.72f), "Supply / Mushroom Spores");
            SpawnPickup(CocaSeedItem, new Vector3(0.4f, 1.45f, 2.72f), "Supply / Coca Seeds");
            SpawnPickup(PackEmptyItem, new Vector3(0.5f, 1.45f, 2.72f), "Supply / Empty Bag", true);
            SpawnPickup(CukeItem, new Vector3(-0.5f, 1.05f, 2.72f), "Mixing / Cuke");
            SpawnPickup(EnergyDrinkItem, new Vector3(0f, 1.05f, 2.72f), "Mixing / Energy Drink");
            SpawnPickup(ParacetamolItem, new Vector3(0.5f, 1.05f, 2.72f), "Mixing / Paracetamol");
            SpawnPickup(FocusTonicItem, new Vector3(1f, 1.05f, 2.72f), "Mixing / Focus Tonic");
            SpawnPickup(WateringCanItem, new Vector3(-2.95f, 0.1f, -3.35f), "Supply / Watering Can");
            SpawnPickup(PrunerItem, new Vector3(2.15f, 1.65f, 2.45f), "Workbench / Pruning Shears");
            SpawnPickup(RakeItem, new Vector3(3.15f, 1.3f, 2.45f), "Workbench / Rake");

            Label(room, LocalizationManager.GetOrFallback("ui.safehouse.grow_room", "SAFEHOUSE GROW ROOM"),
                new Vector3(-2.45f, 2.55f, 0.25f), 0.075f, mint.color);
            Label(room, LocalizationManager.GetOrFallback("ui.safehouse.packing", "PACKING"),
                new Vector3(2.75f, 1.75f, -4.35f), 0.075f, glass.color);
            Label(room, "MIXING LAB", new Vector3(2.75f, 1.75f, -2.45f), 0.075f, mint.color);
        }

        private Transform CreateMixingWorkbench(Vector3 localPosition)
        {
            var workbench = new GameObject("Mixing Station").transform;
            workbench.SetParent(farmRoot, false);
            workbench.localPosition = localPosition;
            Box("Mixing top", workbench, new Vector3(0f, 0.72f, 0f), new Vector3(2.2f, 0.22f, 0.95f), navy);
            Box("Mixing leg", workbench, new Vector3(-0.8f, 0.32f, 0f), new Vector3(0.18f, 0.62f, 0.72f), trim);
            Box("Mixing leg", workbench, new Vector3(0.8f, 0.32f, 0f), new Vector3(0.18f, 0.62f, 0.72f), trim);
            for (int i = -1; i <= 1; i++)
                AddVisual(workbench, PrimitiveType.Cylinder, "Mixing beaker", new Vector3(i * 0.42f, 0.97f, 0f),
                    new Vector3(0.16f, 0.2f, 0.16f), glass);
            var collider = workbench.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.72f, 0f);
            collider.size = new Vector3(2.4f, 1.45f, 1.1f);
            return workbench;
        }

        private void CreateShelf(Vector3 localPosition, Vector3 size)
        {
            var shelf = new GameObject("Farming Shelf").transform;
            shelf.SetParent(farmRoot, false);
            shelf.localPosition = localPosition;
            Box("Shelf upright", shelf, new Vector3(0f, 0f, 0f), new Vector3(0.18f, size.y, size.z), navy);
            Box("Shelf top", shelf, new Vector3(-0.25f, size.y * 0.5f, 0f), new Vector3(0.65f, 0.16f, size.z), trim);
            Box("Shelf middle", shelf, new Vector3(-0.25f, 0.25f, 0f), new Vector3(0.65f, 0.12f, size.z), trim);
        }

        private GameObject CreatePot(int index, Vector3 localPosition)
        {
            var pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pot.name = "Farming Pot " + index;
            pot.tag = "pot";
            pot.transform.SetParent(farmRoot, false);
            pot.transform.localPosition = localPosition;
            pot.transform.localScale = new Vector3(0.8f, 0.35f, 0.8f);
            pot.GetComponent<Renderer>().sharedMaterial = trim;

            var body = pot.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var state = pot.AddComponent<PickableObject>();
            state.item = null;

            var soilTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            soilTop.name = "Soil";
            soilTop.tag = "dirt";
            soilTop.transform.SetParent(pot.transform, false);
            // The pot has a scaled parent, so lift the soil slightly above
            // the solid pot cap and just inside the rim; otherwise the soil
            // cylinder is hidden inside the pot mesh.
            soilTop.transform.localPosition = new Vector3(0f, 1.02f, 0f);
            soilTop.transform.localScale = new Vector3(0.88f, 0.045f, 0.88f);
            soilTop.GetComponent<Renderer>().sharedMaterial = soil;
            RemoveCollider(soilTop);

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Pot Rim";
            rim.transform.SetParent(pot.transform, false);
            rim.transform.localPosition = new Vector3(0f, 0.96f, 0f);
            rim.transform.localScale = new Vector3(1.15f, 0.1f, 1.15f);
            rim.GetComponent<Renderer>().sharedMaterial = trim;
            RemoveCollider(rim);

            var spawn = new GameObject("SpawnPoint");
            spawn.tag = "SpawnPoint";
            spawn.transform.SetParent(pot.transform, false);
            spawn.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            return pot;
        }

        private void CreateGrowLight(string name, Vector3 localPosition)
        {
            var fixture = new GameObject(name);
            fixture.transform.SetParent(farmRoot, false);
            fixture.transform.localPosition = localPosition;
            var light = fixture.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 2.4f;
            light.color = new Color(1f, 0.72f, 0.42f);
            light.enabled = true;
            var switcher = fixture.AddComponent<LampSwitch>();
            switcher.switchDistance = 3f;
            Box("Warm grow panel", fixture.transform, Vector3.zero, new Vector3(2.6f, 0.12f, 0.55f), glass, false);
            Box("Switch", fixture.transform, new Vector3(0f, -1.65f, 0f), new Vector3(0.18f, 0.28f, 0.08f), mint);
        }

        private void CreateWaterSource(Vector3 localPosition)
        {
            var source = new GameObject("Safehouse Water Source");
            source.transform.SetParent(farmRoot, false);
            source.transform.localPosition = localPosition;
            // Wall-mounted sink: the back plate sits against the left wall,
            // while the basin projects into the room instead of floating in it.
            Box("Sink Wall Mount", source.transform, new Vector3(-0.36f, 0.95f, 0f),
                new Vector3(0.14f, 1.9f, 1.55f), navy);
            Box("Sink Basin", source.transform, new Vector3(0.02f, 0.55f, 0f),
                new Vector3(0.78f, 0.28f, 1.45f), stone);
            Box("Sink Interior", source.transform, new Vector3(0.13f, 0.71f, 0f),
                new Vector3(0.5f, 0.035f, 1.1f), glass, false);
            Box("Faucet Stem", source.transform, new Vector3(-0.22f, 1.2f, 0f),
                new Vector3(0.12f, 1.2f, 0.12f), navy);
            Box("Faucet Spout", source.transform, new Vector3(0.12f, 1.65f, 0f),
                new Vector3(0.62f, 0.12f, 0.12f), navy);
            Box("Drain", source.transform, new Vector3(0.18f, 0.75f, 0f),
                new Vector3(0.12f, 0.03f, 0.12f), navy, false);

            var stream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stream.name = "Water Flow Visual";
            stream.transform.SetParent(source.transform, false);
            stream.transform.localPosition = new Vector3(0.43f, 1.02f, 0f);
            stream.transform.localScale = new Vector3(0.075f, 0.48f, 0.075f);
            stream.GetComponent<Renderer>().sharedMaterial = water;
            RemoveCollider(stream);

            var particleObject = new GameObject("Water Flow Particles");
            particleObject.transform.SetParent(source.transform, false);
            particleObject.transform.localPosition = new Vector3(0.43f, 1.48f, 0f);
            var particles = particleObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.42f;
            main.startSpeed = 0.45f;
            main.startSize = 0.055f;
            main.startColor = new Color(0.6f, 0.9f, 1f, 0.9f);
            main.gravityModifier = 0.9f;
            var emission = particles.emission;
            emission.rateOverTime = 28f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4f;
            shape.radius = 0.025f;
            particles.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;

            var bowl = source.AddComponent<BoxCollider>();
            bowl.center = new Vector3(0.02f, 0.72f, 0f);
            bowl.size = new Vector3(0.95f, 1.5f, 1.65f);
            var fill = source.AddComponent<WaterFill>();
            fill.fillDistance = 2.4f;
            fill.ConfigureFlow(stream, particles);
        }

        private Transform CreatePackagingWorkbench(Vector3 localPosition)
        {
            var workbench = new GameObject("Packaging Workbench").transform;
            workbench.SetParent(farmRoot, false);
            workbench.localPosition = localPosition;
            Box("Workbench top", workbench, new Vector3(0f, 0.72f, 0f), new Vector3(2.2f, 0.22f, 0.95f), trim);
            Box("Workbench leg", workbench, new Vector3(-0.8f, 0.32f, 0f), new Vector3(0.18f, 0.62f, 0.72f), navy);
            Box("Workbench leg", workbench, new Vector3(0.8f, 0.32f, 0f), new Vector3(0.18f, 0.62f, 0.72f), navy);
            Box("Packing scale", workbench, new Vector3(-0.48f, 0.93f, 0f), new Vector3(0.36f, 0.15f, 0.34f), mint);
            Box("Packing tray", workbench, new Vector3(0.46f, 0.92f, 0f), new Vector3(0.58f, 0.09f, 0.45f), stone);
            var collider = workbench.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.72f, 0f);
            collider.size = new Vector3(2.4f, 1.45f, 1.1f);
            return workbench;
        }

        private void CreateSupplyShelf(Vector3 localPosition)
        {
            var shelf = new GameObject("Supply Shelf").transform;
            shelf.SetParent(farmRoot, false);
            shelf.localPosition = localPosition;
            // This shelf is mounted on the back wall and faces into the room.
            // Its long axis is X, so it cannot cut through the grow rack on
            // the left wall or block the central walkway.
            Box("Supply back", shelf, new Vector3(0f, 0.9f, 0f), new Vector3(2.2f, 1.8f, 0.12f), navy);
            Box("Supply shelf", shelf, new Vector3(0f, 0.25f, -0.03f), new Vector3(2.2f, 0.14f, 0.6f), trim);
            Box("Supply shelf", shelf, new Vector3(0f, 1.25f, -0.03f), new Vector3(2.2f, 0.14f, 0.6f), trim);
        }

        private void SpawnPickup(Item item, Vector3 localPosition, string objectName, bool respawns = false)
        {
            if (item == null || item.prefab == null)
                return;

            GameObject pickup = Instantiate(item.prefab, farmRoot);
            pickup.name = objectName;
            pickup.transform.localPosition = localPosition;
            pickup.transform.localRotation = Quaternion.identity;
            pickup.SetActive(true);

            PickableObject pickable = pickup.GetComponent<PickableObject>();
            if (pickable != null)
            {
                pickable.item = item;
                pickable.EnableWorldPhysics();
            }

            if (respawns)
            {
                var point = pickup.AddComponent<PackEmptyRespawnPoint>();
                point.respawnDelay = 25f;
                point.ConfigureAsWorldSpawnPoint(pickup.transform.position, pickup.transform.rotation);
            }
        }

        private GameObject CreatePlantTemplate()
        {
            var plant = new GameObject("Plant");
            plant.tag = "weed";
            plant.transform.SetParent(templatesRoot, false);
            plant.transform.localPosition = new Vector3(0f, 0f, 0f);

            CreatePlantStage(plant.transform, "Growth Stage 1", 0.45f, 0.28f);
            CreatePlantStage(plant.transform, "Growth Stage 2", 0.75f, 0.52f);
            CreatePlantStage(plant.transform, "Growth Stage 3", 1.05f, 0.75f);
            Transform mature = CreatePlantStage(plant.transform, "Growth Stage 4", 1.4f, 1.0f);

            GameObject budA = Instantiate(GreenBudItem.prefab, mature);
            budA.name = "Mature Bud A";
            budA.transform.localPosition = new Vector3(-0.28f, 1.1f, 0.05f);
            GameObject budB = Instantiate(GreenBudItem.prefab, mature);
            budB.name = "Mature Bud B";
            budB.transform.localPosition = new Vector3(0.28f, 0.95f, -0.04f);
            return plant;
        }

        private Transform CreatePlantStage(Transform plant, string name, float height, float spread)
        {
            var stage = new GameObject(name).transform;
            stage.SetParent(plant, false);
            stage.localPosition = Vector3.zero;

            float stemRadius = Mathf.Lerp(0.055f, 0.095f, Mathf.Clamp01(height / 1.4f));
            AddVisual(stage, PrimitiveType.Cylinder, "Stem", new Vector3(0f, height * 0.45f, 0f),
                new Vector3(stemRadius, height * 0.45f, stemRadius), leaf);

            int leafRows = height < 0.6f ? 1 : height < 0.95f ? 2 : height < 1.25f ? 3 : 4;
            int leafletCount = height < 0.6f ? 3 : height < 0.95f ? 5 : 7;
            for (int row = 0; row < leafRows; row++)
            {
                float t = (row + 1f) / (leafRows + 1f);
                float y = height * Mathf.Lerp(0.22f, 0.78f, t);
                float reach = spread * Mathf.Lerp(0.42f, 0.9f, t);
                for (int leafIndex = 0; leafIndex < leafletCount; leafIndex++)
                {
                    float angle = leafletCount == 1
                        ? 0f
                        : Mathf.Lerp(-68f, 68f, leafIndex / (float)(leafletCount - 1));
                    angle += row % 2 == 0 ? 0f : 12f;
                    float radians = angle * Mathf.Deg2Rad;
                    Vector3 direction = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
                    Vector3 leafPosition = new Vector3(direction.x * reach * 0.34f,
                        y + Mathf.Sin(radians * 2f) * height * 0.025f,
                        direction.z * reach * 0.22f);
                    float leafLength = reach * (0.58f + 0.12f * Mathf.Abs(direction.x));
                    AddLeaf(stage, "Leaf row " + row + " / " + leafIndex, leafPosition,
                        new Vector3(leafLength, 0.055f, Mathf.Max(0.08f, reach * 0.17f)),
                        Quaternion.Euler(0f, -angle, angle > 0f ? 16f : -16f));
                }
            }

            AddLeaf(stage, "Top leaf", new Vector3(0f, height * 0.88f, 0f),
                new Vector3(spread * 0.72f, 0.065f, spread * 0.2f), Quaternion.identity);
            return stage;
        }

        private GameObject CreateStageOneTemplate()
        {
            var stage = new GameObject("StageOne");
            stage.tag = "weed";
            stage.transform.SetParent(templatesRoot, false);
            AddVisual(stage.transform, PrimitiveType.Cylinder, "New shoot stem", new Vector3(0f, 0.12f, 0f),
                new Vector3(0.045f, 0.12f, 0.045f), leaf);
            AddLeaf(stage.transform, "New shoot leaf L", new Vector3(-0.07f, 0.22f, 0f),
                new Vector3(0.16f, 0.05f, 0.08f), Quaternion.Euler(0f, 0f, -25f));
            AddLeaf(stage.transform, "New shoot leaf R", new Vector3(0.07f, 0.25f, 0f),
                new Vector3(0.16f, 0.05f, 0.08f), Quaternion.Euler(0f, 0f, 25f));
            return stage;
        }

        private GameObject CreateDryTemplate()
        {
            var dry = new GameObject("Dry Plant");
            dry.tag = "weed";
            dry.transform.SetParent(templatesRoot, false);
            AddVisual(dry.transform, PrimitiveType.Cylinder, "Dry stem", new Vector3(0f, 0.45f, 0f),
                new Vector3(0.08f, 0.45f, 0.08f), soil);
            AddVisual(dry.transform, PrimitiveType.Sphere, "Dry leaf", new Vector3(0.2f, 0.6f, 0f),
                new Vector3(0.35f, 0.08f, 0.12f), soil);
            AddVisual(dry.transform, PrimitiveType.Sphere, "Dry leaf", new Vector3(-0.18f, 0.35f, 0f),
                new Vector3(0.3f, 0.07f, 0.1f), soil);
            return dry;
        }

        private GameObject CreateBudTemplate(string name, Item item)
        {
            var bud = CreatePickupTemplate(name, item, PrimitiveType.Capsule, leaf, "bud", "Pruner",
                new Vector3(0.26f, 0.42f, 0.26f));
            AddLeaf(bud.transform, "Bud leaf L", new Vector3(-0.16f, -0.08f, 0f),
                new Vector3(0.22f, 0.05f, 0.1f), Quaternion.Euler(0f, 0f, -32f));
            AddLeaf(bud.transform, "Bud leaf R", new Vector3(0.16f, -0.02f, 0.01f),
                new Vector3(0.22f, 0.05f, 0.1f), Quaternion.Euler(0f, 0f, 32f));
            AddVisual(bud.transform, PrimitiveType.Sphere, "Bud highlight", new Vector3(0.08f, 0.08f, -0.08f),
                new Vector3(0.08f, 0.12f, 0.06f), mint);
            return bud;
        }

        private GameObject CreateWateringCanTemplate()
        {
            var can = CreatePickupModelRoot("WateringCan", WateringCanItem, "waterCup", "",
                new Vector3(1.65f, 1.45f, 2.15f), new Vector3(0f, 0.67f, 0f));
            AddVisual(can.transform, PrimitiveType.Cylinder, "Can Body", new Vector3(0f, 0.42f, 0f),
                new Vector3(0.78f, 0.42f, 0.78f), glass);
            AddVisual(can.transform, PrimitiveType.Cylinder, "Can Rim", new Vector3(0f, 0.86f, 0f),
                new Vector3(0.68f, 0.07f, 0.68f), glass);
            AddVisual(can.transform, PrimitiveType.Cylinder, "Can Lid", new Vector3(0f, 0.94f, 0f),
                new Vector3(0.3f, 0.05f, 0.3f), navy);

            AddVisual(can.transform, PrimitiveType.Cube, "Handle Left", new Vector3(-0.48f, 0.86f, 0.14f),
                new Vector3(0.12f, 0.72f, 0.12f), navy);
            AddVisual(can.transform, PrimitiveType.Cube, "Handle Right", new Vector3(0.48f, 0.86f, 0.14f),
                new Vector3(0.12f, 0.72f, 0.12f), navy);
            AddVisual(can.transform, PrimitiveType.Cube, "Handle Top", new Vector3(0f, 1.22f, 0.14f),
                new Vector3(1.08f, 0.12f, 0.12f), navy);

            var spout = AddVisual(can.transform, PrimitiveType.Cylinder, "Spout", new Vector3(0f, 0.58f, -0.7f),
                new Vector3(0.14f, 0.48f, 0.14f), glass);
            spout.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var nozzle = AddVisual(can.transform, PrimitiveType.Cylinder, "Spout Nozzle", new Vector3(0f, 0.58f, -1.2f),
                new Vector3(0.22f, 0.2f, 0.22f), glass);
            nozzle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var waterLevel = AddVisual(can.transform, PrimitiveType.Cylinder, "Water", new Vector3(0f, 0.8f, 0f),
                new Vector3(0.58f, 0.04f, 0.58f), water);
            waterLevel.tag = "wcwater";
            waterLevel.SetActive(false);
            return can;
        }

        private GameObject CreatePrunerTemplate()
        {
            var pruner = CreatePickupModelRoot("Pruner", PrunerItem, "Untagged", "",
                new Vector3(1.35f, 1.15f, 0.55f), new Vector3(0f, 0.1f, 0f));
            var leftHandle = AddVisual(pruner.transform, PrimitiveType.Cylinder, "Left Handle",
                new Vector3(-0.25f, -0.02f, 0f), new Vector3(0.14f, 0.38f, 0.14f), trim);
            leftHandle.transform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            var rightHandle = AddVisual(pruner.transform, PrimitiveType.Cylinder, "Right Handle",
                new Vector3(0.25f, -0.02f, 0f), new Vector3(0.14f, 0.38f, 0.14f), trim);
            rightHandle.transform.localRotation = Quaternion.Euler(0f, 0f, 24f);

            var leftBlade = AddVisual(pruner.transform, PrimitiveType.Cube, "Left Blade",
                new Vector3(-0.14f, 0.38f, 0f), new Vector3(0.1f, 0.52f, 0.06f), stone);
            leftBlade.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            var rightBlade = AddVisual(pruner.transform, PrimitiveType.Cube, "Right Blade",
                new Vector3(0.14f, 0.38f, 0f), new Vector3(0.1f, 0.52f, 0.06f), stone);
            rightBlade.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            AddVisual(pruner.transform, PrimitiveType.Sphere, "Pruner Pivot",
                new Vector3(0f, 0.18f, -0.04f), new Vector3(0.2f, 0.2f, 0.2f), navy);
            return pruner;
        }

        private GameObject CreateRakeTemplate()
        {
            var rake = CreatePickupModelRoot("Rake", RakeItem, "Untagged", "",
                new Vector3(1.65f, 1.85f, 0.5f), new Vector3(0f, 0.7f, 0f));
            AddVisual(rake.transform, PrimitiveType.Cylinder, "Rake Shaft", new Vector3(0f, 0.78f, 0f),
                new Vector3(0.08f, 0.78f, 0.08f), navy);
            AddVisual(rake.transform, PrimitiveType.Cylinder, "Rake Grip", new Vector3(0f, 1.62f, 0f),
                new Vector3(0.14f, 0.12f, 0.14f), trim);
            AddVisual(rake.transform, PrimitiveType.Cube, "Rake Head", new Vector3(0f, 0.12f, 0f),
                new Vector3(1.25f, 0.12f, 0.16f), trim);
            for (int i = -2; i <= 2; i++)
                AddVisual(rake.transform, PrimitiveType.Cube, "Rake Tooth " + (i + 3),
                    new Vector3(i * 0.22f, -0.15f, 0f), new Vector3(0.08f, 0.36f, 0.1f), glass);
            return rake;
        }

        private GameObject CreatePickupTemplate(string name, Item item, PrimitiveType primitive,
            Material material, string tag, string requiredTool, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.tag = tag;
            go.transform.SetParent(templatesRoot, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;

            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var pickable = go.AddComponent<PickableObject>();
            pickable.item = item;
            pickable.requiredToolName = requiredTool;
            return go;
        }

        private GameObject CreatePickupModelRoot(string name, Item item, string tag,
            string requiredTool, Vector3 colliderSize, Vector3 colliderCenter = default)
        {
            var go = new GameObject(name);
            go.tag = string.IsNullOrEmpty(tag) ? "Untagged" : tag;
            go.transform.SetParent(templatesRoot, false);
            go.transform.localPosition = Vector3.zero;

            var collider = go.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            collider.center = colliderCenter;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var pickable = go.AddComponent<PickableObject>();
            pickable.item = item;
            pickable.requiredToolName = requiredTool;
            return go;
        }

        private Item CreateItem(string id, string displayName, Color color, bool illegal)
        {
            var item = ScriptableObject.CreateInstance<Item>();
            item.name = id;
            item.itemName = displayName;
            item.icon = CreateIcon(color, id);
            item.isIllegal = illegal;
            item.basePrice = illegal ? 10 : 0;
            return item;
        }

        private Item CreateProductItem(string id, string displayName, Color color, int price)
        {
            Item item = CreateItem(id, displayName, color, true);
            item.category = ProductCategory.RawProduct;
            item.productId = id;
            item.baseProductId = id;
            item.basePrice = price;
            return item;
        }

        private Item CreateSeedItem(string id, string displayName, Color color, Item harvestProduct)
        {
            Item item = CreateItem(id, displayName, color, false);
            item.category = ProductCategory.Seed;
            item.productId = id;
            item.harvestProduct = harvestProduct;
            return item;
        }

        private Item CreateIngredientItem(string id, string displayName, Color color,
            string effect, int value, ProductQuality quality)
        {
            Item item = CreateItem(id, displayName, color, false);
            item.category = ProductCategory.Ingredient;
            item.productId = id;
            item.effectName = effect;
            item.effectValue = value;
            item.quality = quality;
            return item;
        }

        private Sprite CreateIcon(Color color, string itemId)
        {
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Runtime " + itemId + " icon";
            texture.filterMode = FilterMode.Point;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool border = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                    texture.SetPixel(x, y, border
                        ? new Color(0.02f, 0.04f, 0.07f)
                        : new Color(0.05f, 0.09f, 0.13f));
                }

            Color body = itemId == "WateringCan" ? new Color32(245, 195, 109, 255) : color;
            Color metal = itemId == "Rake" ? new Color32(28, 47, 79, 255) : new Color32(190, 198, 188, 255);
            Color dark = new Color32(28, 47, 79, 255);

            switch (itemId)
            {
                case "WateringCan":
                    FillRect(texture, 10, 13, 22, 18, body);
                    FillRect(texture, 13, 29, 16, 5, body);
                    FillRect(texture, 15, 31, 12, 3, dark);
                    DrawLine(texture, 11, 31, 11, 41, dark, 3);
                    DrawLine(texture, 31, 31, 31, 41, dark, 3);
                    DrawLine(texture, 11, 41, 31, 41, dark, 3);
                    DrawLine(texture, 31, 22, 42, 27, body, 4);
                    DrawLine(texture, 42, 27, 45, 31, body, 2);
                    FillCircle(texture, 21, 35, 2, dark);
                    break;

                case "Pruner":
                    DrawLine(texture, 22, 22, 13, 9, body, 4);
                    DrawLine(texture, 26, 22, 35, 9, body, 4);
                    DrawLine(texture, 22, 25, 12, 40, metal, 3);
                    DrawLine(texture, 26, 25, 36, 40, metal, 3);
                    FillCircle(texture, 24, 23, 3, dark);
                    break;

                case "Rake":
                    DrawLine(texture, 24, 8, 24, 38, dark, 3);
                    FillCircle(texture, 24, 7, 3, body);
                    DrawLine(texture, 9, 38, 39, 38, body, 3);
                    for (int i = 0; i < 6; i++)
                        DrawLine(texture, 11 + i * 5, 37, 11 + i * 5, 29, metal, 2);
                    break;

                case "Seed":
                    FillCircle(texture, 24, 24, 9, body);
                    FillCircle(texture, 20, 29, 2, Color.Lerp(body, Color.white, 0.45f));
                    break;

                case "PackEmpty":
                    FillRect(texture, 12, 12, 24, 25, body);
                    FillRect(texture, 15, 34, 18, 5, Color.Lerp(body, Color.white, 0.2f));
                    DrawLine(texture, 15, 39, 18, 44, dark, 3);
                    DrawLine(texture, 33, 39, 30, 44, dark, 3);
                    break;

                case "Pack":
                    FillRect(texture, 10, 12, 28, 25, body);
                    FillRect(texture, 21, 12, 5, 25, dark);
                    FillRect(texture, 10, 28, 28, 4, Color.Lerp(body, Color.white, 0.25f));
                    break;

                case "GreenBud":
                    DrawLine(texture, 24, 10, 24, 28, dark, 3);
                    FillCircle(texture, 19, 28, 7, body);
                    FillCircle(texture, 29, 28, 7, body);
                    FillCircle(texture, 24, 35, 8, Color.Lerp(body, Color.white, 0.15f));
                    break;

                default:
                    FillCircle(texture, 24, 24, 10, body);
                    break;
            }

            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "Runtime " + itemId + " Icon";
            return sprite;
        }

        private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            for (int px = x; px < x + width; px++)
                for (int py = y; py < y + height; py++)
                    if (px >= 0 && py >= 0 && px < texture.width && py < texture.height)
                        texture.SetPixel(px, py, color);
        }

        private static void FillCircle(Texture2D texture, int centerX, int centerY, int radius, Color color)
        {
            int radiusSquared = radius * radius;
            for (int x = centerX - radius; x <= centerX + radius; x++)
                for (int y = centerY - radius; y <= centerY + radius; y++)
                    if ((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= radiusSquared &&
                        x >= 0 && y >= 0 && x < texture.width && y < texture.height)
                        texture.SetPixel(x, y, color);
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                FillCircle(texture, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)),
                    Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), Mathf.Max(1, thickness / 2), color);
            }
        }

        private FarmingUiReferences BuildInventoryUi()
        {
            var canvasObject = new GameObject("Safehouse Farming HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = canvasObject.transform as RectTransform;
            var inventoryPanel = UiPanel("Inventory Panel", root, new Vector2(0.5f, 0f), new Vector2(0f, 12f),
                new Vector2(470f, 70f), new Color32(12, 22, 34, 235));
            var inventoryCanvasGroup = inventoryPanel.gameObject.AddComponent<CanvasGroup>();
            inventoryCanvasGroup.alpha = 0f;
            inventoryCanvasGroup.interactable = false;
            inventoryCanvasGroup.blocksRaycasts = false;
            UiText(inventoryPanel, "ITEMS  [1-8]", 9, new Color32(160, 181, 183, 255),
                new Vector2(12f, -8f), new Vector2(180f, 14f));

            var slots = new Image[8];
            for (int i = 0; i < slots.Length; i++)
            {
                var buttonObject = new GameObject("Inventory Slot " + (i + 1), typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(inventoryPanel, false);
                var buttonRect = buttonObject.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(0f, 0f);
                buttonRect.anchorMax = new Vector2(0f, 0f);
                buttonRect.pivot = new Vector2(0f, 0f);
                buttonRect.anchoredPosition = new Vector2(12f + i * 54f, 8f);
                buttonRect.sizeDelta = new Vector2(46f, 46f);
                var buttonImage = buttonObject.GetComponent<Image>();
                buttonImage.color = new Color32(30, 52, 63, 220);
                var colors = buttonObject.GetComponent<Button>().colors;
                colors.highlightedColor = new Color32(74, 131, 121, 255);
                colors.pressedColor = new Color32(119, 233, 196, 255);
                buttonObject.GetComponent<Button>().colors = colors;

                var iconObject = new GameObject("Item Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(buttonObject.transform, false);
                var iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(5f, 5f);
                iconRect.offsetMax = new Vector2(-5f, -5f);
                var icon = iconObject.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = true;
                slots[i] = icon;
            }

            var promptPanel = UiPanel("Farming Prompt", root, new Vector2(0.5f, 0f), new Vector2(0f, 158f),
                new Vector2(520f, 46f), new Color32(12, 22, 34, 235));
            var promptText = UiText(promptPanel, "", 18, Color.white, new Vector2(16f, -8f),
                new Vector2(488f, 30f));
            promptText.alignment = TextAlignmentOptions.Center;

            var feedback = UiText(root, "", 13, new Color32(255, 194, 92, 255),
                new Vector2(-320f, 220f), new Vector2(640f, 24f), new Vector2(0.5f, 0.5f));
            feedback.alignment = TextAlignmentOptions.Center;
            var wateringCountdown = UiText(root, "", 16, Color.white, new Vector2(-300f, 196f),
                new Vector2(600f, 26f), new Vector2(0.5f, 0.5f));
            wateringCountdown.alignment = TextAlignmentOptions.Center;

            promptPanel.gameObject.SetActive(false);
            return new FarmingUiReferences
            {
                canvas = canvas,
                slots = slots,
                promptPanel = promptPanel.gameObject,
                promptText = promptText,
                waterStatus = null,
                selectedStatus = null,
                feedback = feedback,
                wateringCountdown = wateringCountdown,
                inventoryPanel = inventoryPanel.gameObject
            };
        }

        private static RectTransform UiPanel(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        private static TextMeshProUGUI UiText(Transform parent, string value, int fontSize, Color color,
            Vector2 position, Vector2 size, Vector2? anchor = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Vector2 actualAnchor = anchor ?? new Vector2(0f, 1f);
            rect.anchorMin = rect.anchorMax = actualAnchor;
            rect.pivot = new Vector2(actualAnchor.x == 0.5f ? 0.5f : 0f, actualAnchor.y == 0f ? 0f : 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = color;
            text.text = value;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            new GameObject("Safehouse Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private Material MakeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            return material;
        }

        private GameObject AddVisual(Transform parent, PrimitiveType type, string name, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(go);
            return go;
        }

        private GameObject AddLeaf(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Quaternion localRotation)
        {
            var leafBlade = AddVisual(parent, PrimitiveType.Capsule, name, localPosition, localScale, leaf);
            leafBlade.transform.localRotation = localRotation;
            return leafBlade;
        }

        private GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
                RemoveCollider(go);
            return go;
        }

        private void Label(Transform parent, string text, Vector3 localPosition, float size, Color color)
        {
            var go = new GameObject("Sign " + text, typeof(TextMesh));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var label = go.GetComponent<TextMesh>();
            label.text = text;
            label.characterSize = size;
            label.fontSize = 42;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = color;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        }

        private static void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        private sealed class FarmingUiReferences
        {
            public Canvas canvas;
            public Image[] slots;
            public GameObject promptPanel;
            public TextMeshProUGUI promptText;
            public TextMeshProUGUI waterStatus;
            public TextMeshProUGUI selectedStatus;
            public TextMeshProUGUI feedback;
            public TextMeshProUGUI wateringCountdown;
            public GameObject inventoryPanel;
        }
    }

    public sealed class SafehouseInventoryHud : MonoBehaviour
    {
        private const float InventoryAnimationDuration = 0.22f;
        private Inventory inventory;
        private TextMeshProUGUI waterStatus;
        private TextMeshProUGUI selectedStatus;
        private TextMeshProUGUI feedback;
        private Canvas inventoryCanvas;
        private RectTransform inventoryPanel;
        private CanvasGroup inventoryGroup;
        private Vector2 inventoryOpenPosition;
        private Vector2 inventoryClosedPosition;
        private float inventoryAnimation;
        private bool inventoryTargetOpen;
        private float feedbackUntil;

        public void Initialize(Inventory owner, TextMeshProUGUI waterText, TextMeshProUGUI selectedText,
            TextMeshProUGUI feedbackText, Canvas canvas, GameObject panelObject)
        {
            inventory = owner;
            waterStatus = waterText;
            selectedStatus = selectedText;
            feedback = feedbackText;
            inventoryCanvas = canvas;
            inventoryPanel = panelObject != null ? panelObject.GetComponent<RectTransform>() : null;
            inventoryGroup = panelObject != null ? panelObject.GetComponent<CanvasGroup>() : null;
            if (inventoryPanel != null)
            {
                inventoryOpenPosition = inventoryPanel.anchoredPosition;
                inventoryClosedPosition = inventoryOpenPosition + Vector2.down * (inventoryPanel.rect.height + 24f);
                SetInventoryOpen(false, true);
            }
        }

        public void ShowFeedback(string message, float duration = 2.5f)
        {
            if (feedback == null)
                return;
            feedback.text = message;
            feedbackUntil = Time.unscaledTime + duration;
        }

        private void Update()
        {
            if (inventory == null)
                return;

            bool hideInventory = false;
            DialogueCameraController dialogueCamera = GetComponent<DialogueCameraController>();
            if (dialogueCamera != null && dialogueCamera.IsInDialogue)
                hideInventory = true;

            foreach (NpcInteraction npc in FindObjectsByType<NpcInteraction>(FindObjectsSortMode.None))
            {
                if (npc != null && npc.IsDialogueActive && npc.currentPlayer == gameObject)
                {
                    hideInventory = true;
                    break;
                }
            }

            HighStakes.City.CityMissionDirector cityMission =
                FindAnyObjectByType<HighStakes.City.CityMissionDirector>();
            if (cityMission != null &&
                (cityMission.IsConversationActive || cityMission.IsBetweenLevels || cityMission.HasEnded))
                hideInventory = true;

            if (hideInventory)
            {
                SetInventoryOpen(false, true);
                if (inventoryCanvas != null)
                    inventoryCanvas.enabled = false;
                return;
            }

            if (inventoryCanvas != null)
                inventoryCanvas.enabled = true;

            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                SetInventoryOpen(!inventoryTargetOpen);

            UpdateInventoryAnimation();

            Item selected = inventory.GetSelectedItem();
            if (selectedStatus != null)
                selectedStatus.text = selected == null
                    ? LocalizationManager.GetOrFallback("ui.inventory.empty", "EMPTY")
                    : LocalizationManager.GetItemName(selected).ToUpperInvariant();

            if (waterStatus != null)
            {
                bool selectedCan = selected != null && selected.name == "WateringCan";
                string wateringCan = LocalizationManager.GetOrFallback("ui.inventory.watering_can", "WATERING CAN");
                string waterState = inventory.IsSelectedWateringCanFull()
                    ? LocalizationManager.GetOrFallback("ui.inventory.full", "FULL")
                    : LocalizationManager.GetOrFallback("ui.inventory.empty", "EMPTY");
                waterStatus.text = selectedCan
                    ? wateringCan + ": " + waterState
                    : wateringCan + ": " + LocalizationManager.GetOrFallback("ui.inventory.select_can", "select can");
                waterStatus.color = selectedCan && inventory.IsSelectedWateringCanFull()
                    ? new Color32(119, 233, 196, 255)
                    : new Color32(160, 181, 183, 255);
            }

            if (feedback != null && feedbackUntil > 0f && Time.unscaledTime >= feedbackUntil)
            {
                feedback.text = string.Empty;
                feedbackUntil = 0f;
            }
        }

        private void SetInventoryOpen(bool open, bool instant = false)
        {
            inventoryTargetOpen = open;
            if (instant)
            {
                inventoryAnimation = open ? 1f : 0f;
                ApplyInventoryAnimation();
            }
        }

        private void UpdateInventoryAnimation()
        {
            if (inventoryPanel == null)
                return;

            float target = inventoryTargetOpen ? 1f : 0f;
            inventoryAnimation = Mathf.MoveTowards(inventoryAnimation, target,
                Time.unscaledDeltaTime / InventoryAnimationDuration);
            ApplyInventoryAnimation();
        }

        private void ApplyInventoryAnimation()
        {
            if (inventoryPanel == null)
                return;

            float eased = inventoryAnimation * inventoryAnimation * (3f - 2f * inventoryAnimation);
            inventoryPanel.anchoredPosition = Vector2.Lerp(inventoryClosedPosition, inventoryOpenPosition, eased);
            if (inventoryGroup != null)
            {
                inventoryGroup.alpha = eased;
                inventoryGroup.interactable = inventoryTargetOpen && inventoryAnimation >= 0.98f;
                inventoryGroup.blocksRaycasts = inventoryGroup.interactable;
            }
        }
    }

    /// <summary>World-space growth readout that follows a planted crop.</summary>
    public sealed class PlantGrowthIndicator : MonoBehaviour
    {
        private TextMesh label;
        private int totalStages;

        public void Initialize(int stages)
        {
            totalStages = Mathf.Max(1, stages);
            if (label != null)
                return;

            var labelObject = new GameObject("Growth Progress", typeof(TextMesh));
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = Vector3.up * 2.35f;
            label = labelObject.GetComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 44;
            label.characterSize = 0.045f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color32(255, 194, 92, 255);
            SetProgress(0f, 1);
        }

        public void SetProgress(float normalizedProgress, int stage)
        {
            if (label == null)
                return;

            float progress = Mathf.Clamp01(normalizedProgress);
            int percent = Mathf.RoundToInt(progress * 100f);
            int filled = Mathf.RoundToInt(progress * 10f);
            label.text = $"{LocalizationManager.GetOrFallback("ui.growth", "GROWTH")} {percent}%\n" +
                $"{LocalizationManager.GetOrFallback("ui.stage", "STAGE")} {Mathf.Clamp(stage, 1, totalStages)}/{totalStages}  " +
                $"[{new string('#', filled)}{new string('-', 10 - filled)}]";
            label.color = progress >= 1f
                ? new Color32(119, 233, 196, 255)
                : progress >= 0.5f
                    ? new Color32(160, 220, 120, 255)
                    : new Color32(255, 194, 92, 255);
        }

        private void LateUpdate()
        {
            if (label == null)
                return;

            Camera camera = Camera.main;
            if (camera == null)
                camera = FindAnyObjectByType<Camera>();
            if (camera != null)
                label.transform.rotation = Quaternion.LookRotation(
                    camera.transform.position - label.transform.position, Vector3.up);
        }
    }
}
