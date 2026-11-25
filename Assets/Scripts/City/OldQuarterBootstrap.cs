using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace HighStakes.City
{
    /// <summary>A deterministic, original modular district. No external meshes or LFS binaries required.</summary>
    public sealed class OldQuarterBootstrap : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private Transform geometry;
        private Material stone, asphalt, pavement, trim, glass, foliage, skin, navy, mint;
        private Font signFont;
        private Material signMaterial;
        private readonly Dictionary<string, CityActor> actors = new Dictionary<string, CityActor>();
        private readonly List<Bounds> mapSidewalks = new List<Bounds>();
        private readonly List<Bounds> mapBuildings = new List<Bounds>();
        private SafehouseFarmingSystem safehouseFarming;

        private void Awake()
        {
            if (FindAnyObjectByType<PauseMenuController>() == null)
                gameObject.AddComponent<PauseMenuController>();

            CreatePalette();
            ConfigureLighting();
            Bounds districtBounds = BuildDistrict();
            // Bake static colliders before spawning agents. Render meshes need Read/Write in builds;
            // collider collection also works in standalone players.
            Physics.SyncTransforms();
            var surface = geometry.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.12f;
            surface.BuildNavMesh();
            BuildPedestrianRoutes();

            if (GameMaster.Instance == null) new GameObject("GameMaster").AddComponent<GameMaster>();
            var player = CreatePlayer(new Vector3(-16, 0.3f, -7));
            AddPerson("mara", "Mara / Fixer", new Vector3(-16, 0.3f, -9), mint, "mara.idle", false);
            AddPerson("tomas", "Tomas / Barista", new Vector3(15, 0.3f, 19), stone, "tomas.idle", false);
            AddPerson("ada", "Ada / Dock worker", new Vector3(32, 0.3f, -9), trim, "ada.route", false);
            AddLocation("parcel", "Dock parcel", new Vector3(35, 0.3f, 9), new Vector3(0.7f, 0.6f, 0.7f));
            AddLocation("ledger", "Archive locker", new Vector3(35, 1.3f, -22), new Vector3(0.6f, 0.15f, 0.5f));
            // Keep the hold pad in front of the supply shelf, on the reachable central aisle.
            AddLocation("safehouse", "Safehouse", new Vector3(-17, 0.26f, -20), new Vector3(1.8f, 0.06f, 1.8f));
            AddLocation("fuse", "Workshop / Spare fuse", new Vector3(-20, 1.15f, -21), new Vector3(0.35f, 0.25f, 0.25f));
            AddLocation("wiring", "Workshop / Maintenance note", new Vector3(-14, 0.6f, -22), new Vector3(0.5f, 0.08f, 0.6f));
            AddLocation("switchboard", "Workshop / Switchboard", new Vector3(-13.65f, 1.1f, -19), new Vector3(0.25f, 0.7f, 0.9f));
            AddLocation("manifest", "Archive / Manifest", new Vector3(29, 1.15f, -20), new Vector3(0.5f, 0.08f, 0.6f));
            AddLocation("invoice", "Cafe / Invoice", new Vector3(19, 1.35f, 21.2f), new Vector3(0.5f, 0.08f, 0.6f));
            AddLocation("radio", "Radio house / Transmitter", new Vector3(-17, 1.45f, 21.4f), new Vector3(1.1f, 0.5f, 0.6f));
            AddLocation("extraction", "Final extraction", new Vector3(-20, 0.24f, -9), new Vector3(1.8f, 0.06f, 1.8f));
            AddPerson("officer", "Officer Novak", new Vector3(9, 0.3f, -18), navy, "officer.idle", true);
            for (int i = 0; i < 10; i++)
            {
                float x = i % 2 == 0 ? -8 : 8;
                AddPerson("local" + i, "Local resident", new Vector3(x, 0.3f, -32 + i * 6), i % 2 == 0 ? trim : mint, "local.rumor", true);
            }
            var hud = gameObject.AddComponent<CityHud>();
            hud.Build(districtBounds, mapSidewalks, mapBuildings);
            var director = gameObject.AddComponent<CityMissionDirector>();
            director.Initialize(player, actors, hud);
        }

        private void BuildPedestrianRoutes()
        {
            var routes = new GameObject("Pedestrian routes / crossings and shopfronts");
            routes.transform.SetParent(transform, false);
            var manager = routes.AddComponent<HighStakes.AI.WaypointManager>();
            var nodes = new List<HighStakes.AI.WaypointNode>();
            float[] stops = { -34, -22, -9, 9, 22, 34 };
            foreach (float stop in stops)
                foreach (float side in new[] { -8f, 8f })
                {
                    foreach (Vector3 point in new[] { new Vector3(side, 0.2f, stop), new Vector3(stop, 0.2f, side) })
                    {
                        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1f, NavMesh.AllAreas)) continue;
                        var go = new GameObject("Sidewalk / " + nodes.Count);
                        go.transform.SetParent(routes.transform, false); go.transform.position = hit.position;
                        nodes.Add(go.AddComponent<HighStakes.AI.WaypointNode>());
                    }
                }
            foreach (var from in nodes)
                foreach (var to in nodes)
                    if (from != to && Vector3.Distance(from.transform.position, to.transform.position) <= 20f &&
                        !NavMesh.Raycast(from.transform.position, to.transform.position, out _, NavMesh.AllAreas))
                        from.AddConnection(to);
            manager.Refresh();
        }

        private void CreatePalette()
        {
            stone = Material("Warm limestone", new Color32(183, 157, 126, 255));
            asphalt = Material("Asphalt", new Color32(40, 49, 57, 255));
            pavement = Material("Pavement", new Color32(120, 130, 130, 255));
            trim = Material("Terracotta", new Color32(164, 77, 56, 255));
            glass = Material("Lit windows", new Color32(245, 195, 109, 255), true);
            foliage = Material("Trees", new Color32(46, 98, 78, 255));
            skin = Material("Skin", new Color32(199, 148, 111, 255));
            navy = Material("Police navy", new Color32(28, 47, 79, 255));
            mint = Material("Jade", new Color32(46, 121, 112, 255));
            signFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            signMaterial = new Material(Resources.Load<Shader>("City/WorldSign")) { name = "Old Quarter signs" };
            materials.Add(signMaterial);
            RefreshSignTexture(signFont);
            Font.textureRebuilt += RefreshSignTexture;
        }

        private Material Material(string name, Color color, bool emissive = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material template = Resources.Load<Material>(emissive ? "City/DistrictGlow" : "City/DistrictBase");
            var material = template != null ? new Material(template) : new Material(shader);
            material.name = name; material.color = color;
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.7f);
            }
            materials.Add(material);
            return material;
        }

        private void ConfigureLighting()
        {
            RenderSettings.ambientLight = new Color(0.5f, 0.56f, 0.64f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color32(99, 118, 131, 255);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;
            var sun = new GameObject("Late afternoon sun").AddComponent<Light>();
            sun.transform.SetParent(transform);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.8f, 0.6f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38, -35, 0);
            RenderSettings.sun = sun;
            RenderSettings.skybox = CreateAtmosphereMaterial("City/OldQuarterSky", "Old Quarter afternoon sky");
        }

        private Material CreateAtmosphereMaterial(string shaderPath, string name)
        {
            var material = new Material(Resources.Load<Shader>(shaderPath)) { name = name };
            material.SetColor("_FogColor", RenderSettings.fogColor);
            material.SetVector("_SunDirection", -RenderSettings.sun.transform.forward);
            materials.Add(material);
            return material;
        }

        private Bounds BuildDistrict()
        {
            geometry = new GameObject("Modular District / Static Geometry").transform;
            geometry.SetParent(transform, false);
            var road = Box("Road foundation", geometry, new Vector3(0, -0.3f, 0), new Vector3(90, 0.6f, 90), asphalt);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var sidewalk = Box("Raised sidewalk block", geometry, new Vector3(x * 23, 0.08f, z * 23), new Vector3(34, 0.16f, 34), pavement);
                    mapSidewalks.Add(sidewalk.GetComponent<Renderer>().bounds);
                    Building(new Vector3(x * 17, 0.16f, z * 19), 10, 12, 8, x < 0 ? stone : trim, z < 0 ? "WORKSHOP" : "CAFE");
                    Building(new Vector3(x * 32, 0.16f, z * 19), 10, 12, 6, x < 0 ? mint : stone, "MARKET");
                    Building(new Vector3(x * 17, 0.16f, z * 34), 10, 10, 12, stone, "RESIDENCES");
                    Building(new Vector3(x * 32, 0.16f, z * 34), 10, 10, 7, trim, z < 0 ? "DOCK 04" : "ARCADE");
                }
            // Crossings join the four pedestrian blocks; the entire road is navigable.
            for (int i = -3; i <= 3; i++)
            {
                Box("Crossing east", geometry, new Vector3(i * 1.15f, 0.012f, 9), new Vector3(0.6f, 0.02f, 2.5f), stone, false);
                Box("Crossing west", geometry, new Vector3(i * 1.15f, 0.012f, -9), new Vector3(0.6f, 0.02f, 2.5f), stone, false);
                Box("Crossing north", geometry, new Vector3(9, 0.012f, i * 1.15f), new Vector3(2.5f, 0.02f, 0.6f), stone, false);
                Box("Crossing south", geometry, new Vector3(-9, 0.012f, i * 1.15f), new Vector3(2.5f, 0.02f, 0.6f), stone, false);
            }
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                Box("Lane marking", geometry, new Vector3(0, 0.01f, i * 11), new Vector3(0.12f, 0.02f, 3), stone, false);
                StreetLamp(new Vector3(-7, 0.16f, i * 11));
                Tree(new Vector3(7, 0.16f, i * 11));
                Bench(new Vector3(i * 11, 0.16f, 7));
            }
            Box("Boundary north", geometry, new Vector3(0, 1, 44), new Vector3(90, 2, 1), stone);
            Box("Boundary south", geometry, new Vector3(0, 1, -44), new Vector3(90, 2, 1), stone);
            Box("Boundary west", geometry, new Vector3(-44, 1, 0), new Vector3(1, 2, 90), stone);
            Box("Boundary east", geometry, new Vector3(44, 1, 0), new Vector3(1, 2, 90), stone);
            CreateBoundaryFog(road.transform.localScale);
            return road.GetComponent<Renderer>().bounds;
        }

        private void CreateBoundaryFog(Vector3 districtSize)
        {
            var material = CreateAtmosphereMaterial("City/BoundaryFog", "Old Quarter boundary fog");
            // The inward faces conceal the exterior and blend into the sky above the horizon.
            var fog = Box("District boundary fog", transform, new Vector3(0, 17, 0),
                new Vector3(districtSize.x, 36, districtSize.z), material, false);
            fog.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Building(Vector3 position, float width, float depth, float height, Material wall, string sign)
        {
            var module = new GameObject("Building / " + sign).transform;
            module.SetParent(geometry, false); module.localPosition = position;
            string room = null; int unlockLevel = 1;
            if (position.x == 17 && position.z == 19) room = "The Jade Room / Cafe";
            if (position.x == -17 && position.z == -19) { room = "Workshop / Safehouse"; unlockLevel = 1; }
            if (position.x == 32 && position.z == -19) { room = "Archive / Records"; unlockLevel = 3; }
            // A separate, always-open production building; authored mission positions stay unchanged.
            if (position.x == -32 && position.z == -19) room = "Warehouse / Production";
            if (position.x == -17 && position.z == 19) { room = "Radio House / Studio"; unlockLevel = 5; }
            if (room == null)
            {
                var modifier = module.gameObject.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true; modifier.area = 1;
                var shell = Box("Shell", module, Vector3.up * height * 0.5f, new Vector3(width, height, depth), wall);
                mapBuildings.Add(shell.GetComponent<Renderer>().bounds);
                Box("Door", module, new Vector3(0, 1.15f, -depth / 2 - 0.025f), new Vector3(1.2f, 2.3f, 0.05f), navy, false);
            }
            else
            {
                if (position.z < 0) module.localRotation = Quaternion.Euler(0, 180, 0);
                var interior = module.gameObject.AddComponent<CityInterior>();
                interior.roomName = room; interior.size = new Vector3(width - 0.8f, 3.8f, depth - 0.8f);
                var floor = Box("Interior floor", module, Vector3.up * 0.04f, new Vector3(width, 0.08f, depth), stone);
                mapBuildings.Add(floor.GetComponent<Renderer>().bounds);
                Box("Left wall", module, new Vector3(-width / 2 + 0.2f, 1.9f, 0), new Vector3(0.4f, 3.8f, depth), wall);
                Box("Right wall", module, new Vector3(width / 2 - 0.2f, 1.9f, 0), new Vector3(0.4f, 3.8f, depth), wall);
                Box("Back wall", module, new Vector3(0, 1.9f, depth / 2 - 0.2f), new Vector3(width, 3.8f, 0.4f), wall);
                float doorWidth = 2.4f;
                foreach (int side in new[] { -1, 1 })
                    Box("Entrance side", module, new Vector3(side * (width + doorWidth) / 4, 1.9f, -depth / 2 + 0.2f),
                        new Vector3((width - doorWidth) / 2, 3.8f, 0.4f), wall);
                Box("Door lintel", module, new Vector3(0, 3.25f, -depth / 2 + 0.2f), new Vector3(doorWidth, 1.1f, 0.4f), wall);
                var upper = Box("Ceiling and upper floors", module, Vector3.up * ((height + 3.8f) / 2), new Vector3(width, height - 3.8f, depth), wall);
                var upperModifier = upper.AddComponent<NavMeshModifier>(); upperModifier.overrideArea = true; upperModifier.area = 1;
                FurnishRoom(module, room);
                if (room == "Warehouse / Production")
                {
                    safehouseFarming = module.gameObject.AddComponent<SafehouseFarmingSystem>();
                    safehouseFarming.Build(module, stone, trim, glass, navy, mint, signFont, signMaterial);
                    CreateSign(module, LocalizationManager.GetOrFallback("ui.warehouse.production", "GROW / PACK / SELL"), new Vector3(0, 6.1f, -depth / 2 - 0.085f),
                        0.12f, 48, Color.white, signFont, signMaterial);
                }
                AddDoor(module, room, unlockLevel, depth);
                var lamp = new GameObject("Interior warm light").AddComponent<Light>();
                lamp.transform.SetParent(module, false); lamp.transform.localPosition = new Vector3(0, 3.2f, 0);
                lamp.type = LightType.Point; lamp.range = 11; lamp.intensity = 3.5f;
                lamp.color = new Color(1f, 0.86f, 0.66f);
            }
            Box("Cornice", module, Vector3.up * (height - 0.2f), new Vector3(width + 0.35f, 0.4f, depth + 0.35f), stone);
            Box("Shopfront canopy", module, new Vector3(0, 2.95f, -depth / 2 - 0.5f), new Vector3(width - 1, 0.18f, 1.5f), mint);
            for (int row = 0; row < Mathf.FloorToInt(height / 3); row++)
                for (int col = -1; col <= 1; col++)
                {
                    if (!(room != null && row == 0 && col == 0))
                        Box("Window south", module, new Vector3(col * 2.7f, 1.7f + row * 2.6f, -depth / 2 - 0.04f), new Vector3(1.2f, 1.3f, 0.06f), glass, false);
                    Box("Window north", module, new Vector3(col * 2.7f, 1.7f + row * 2.6f, depth / 2 + 0.04f), new Vector3(1.2f, 1.3f, 0.06f), glass, false);
                }
        }

        private void AddDoor(Transform room, string name, int level, float depth)
        {
            // Moving leaves stay OUTSIDE the static navigation collection. The doorway itself is baked.
            var go = new GameObject("Door / " + name);
            go.transform.SetParent(transform, false);
            go.transform.position = room.TransformPoint(new Vector3(0, 0, -depth / 2));
            go.transform.rotation = room.rotation;
            var actor = go.AddComponent<CityActor>();
            actor.id = "door_" + name.ToLowerInvariant().Replace(" / ", "_").Replace(" ", "_"); actor.displayName = "Door / " + name;
            actors.Add(actor.id, actor);
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(go.transform, false); hinge.localPosition = new Vector3(-1.1f, 0, 0);
            Box("Door leaf", hinge, new Vector3(1.1f, 1.3f, 0), new Vector3(2.15f, 2.6f, 0.12f), navy);
            Box("Handle", hinge, new Vector3(1.9f, 1.2f, -0.1f), new Vector3(0.08f, 0.12f, 0.08f), glass, false);
            var door = go.AddComponent<CityDoor>(); door.leaf = hinge; door.minimumLevel = level; door.roomName = name;
            CreateSign(go.transform, name.Split('/')[0].Trim().ToUpperInvariant(), new Vector3(0, 3.3f, -0.15f),
                0.085f, 48, Color.white, signFont, signMaterial);
        }

        private void FurnishRoom(Transform room, string name)
        {
            if (name.Contains("Production")) return; // Production supplies its own minimal equipment.
            // Physical furniture leaves a wide central route from every entrance.
            if (name.Contains("Cafe"))
            {
                Box("Serving counter", room, new Vector3(0, 0.6f, 2.2f), new Vector3(6.2f, 1.2f, 1.1f), mint);
                Box("Coffee machine", room, new Vector3(-1.6f, 1.55f, 2.2f), new Vector3(0.8f, 0.7f, 0.6f), navy);
                foreach (int side in new[] { -1, 1 })
                {
                    Box("Cafe table", room, new Vector3(side * 3.3f, 0.75f, -2), new Vector3(1.3f, 0.12f, 1.3f), trim);
                    Box("Chair", room, new Vector3(side * 3.3f, 0.4f, -3.3f), new Vector3(0.6f, 0.8f, 0.6f), mint);
                }
            }
            else if (name.Contains("Workshop"))
            {
                // Keep the centre line clear from the entrance to the back wall.
                // The right side becomes the practical work/rest zone; the
                // farming system uses the left side for the grow rack.
                Box("Workbench", room, new Vector3(2.75f, 0.45f, 2.45f), new Vector3(2.4f, 0.9f, 1.1f), trim);
                Box("Tool shelf", room, new Vector3(3.75f, 0.9f, 0.25f), new Vector3(0.85f, 1.8f, 0.65f), navy);
                Box("Safehouse sofa", room, new Vector3(2.65f, 0.45f, 4.25f), new Vector3(2.7f, 0.9f, 1), mint);
                if (name.Contains("Workshop")) Box("Fuse board casing", room, new Vector3(-4.35f, 1.2f, -0.7f), new Vector3(0.25f, 0.95f, 1.2f), navy);
            }
            else if (name.Contains("Archive"))
            {
                foreach (int side in new[] { -1, 1 })
                    foreach (float z in new[] { 0f, 3f })
                        Box("Storage crate", room, new Vector3(side * 3, 0.5f, z), new Vector3(1.8f, 1, 1.8f), trim);
                Box("Records desk", room, new Vector3(0, 0.45f, 4.5f), new Vector3(2.6f, 0.9f, 1), mint);
            }
            else
            {
                Box("Broadcast desk", room, new Vector3(0, 0.6f, 2.4f), new Vector3(4.2f, 1.2f, 1.3f), mint);
                Box("Equipment rack", room, new Vector3(-3.5f, 1.1f, 3), new Vector3(1.1f, 2.2f, 1), navy);
                Box("Studio couch", room, new Vector3(3, 0.45f, -1), new Vector3(1.2f, 0.9f, 3), trim);
                Box("Rug", room, new Vector3(0, 0.085f, -1), new Vector3(3.4f, 0.01f, 3.4f), mint, false);
            }
        }

        private void StreetLamp(Vector3 position)
        {
            Box("Lamp pole", geometry, position + Vector3.up * 2, new Vector3(0.15f, 4, 0.15f), navy);
            Box("Lantern", geometry, position + Vector3.up * 4, new Vector3(0.5f, 0.5f, 0.5f), glass, false);
            var lamp = new GameObject("Warm street light").AddComponent<Light>();
            lamp.transform.SetParent(geometry);
            lamp.transform.position = position + Vector3.up * 3.8f;
            lamp.type = LightType.Point; lamp.range = 9; lamp.intensity = 1.8f;
            lamp.color = new Color(1, 0.73f, 0.4f);
        }
        private void Tree(Vector3 position)
        {
            Box("Planter", geometry, position + Vector3.up * 0.25f, new Vector3(1.7f, 0.5f, 1.7f), stone);
            Box("Tree trunk", geometry, position + Vector3.up * 1.3f, new Vector3(0.35f, 2.6f, 0.35f), trim);
            Shape(PrimitiveType.Sphere, "Crown", geometry, position + Vector3.up * 3, new Vector3(2.8f, 2.6f, 2.8f), foliage, false);
        }
        private void Bench(Vector3 position)
        {
            Box("Bench seat", geometry, position + Vector3.up * 0.5f, new Vector3(2.1f, 0.18f, 0.65f), trim);
            Box("Bench back", geometry, position + new Vector3(0, 0.85f, 0.3f), new Vector3(2.1f, 0.65f, 0.12f), trim);
        }

        private PlayerController CreatePlayer(Vector3 position)
        {
            var go = new GameObject("Old Quarter Player");
            go.tag = "Player";
            go.transform.position = position;
            var collider = go.AddComponent<CharacterController>();
            collider.height = 1.8f; collider.center = Vector3.up * 0.9f; collider.radius = 0.3f; collider.stepOffset = 0.3f;
            Puppet(go.transform, navy, false);
            var camera = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.transform.SetParent(go.transform, false);
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.nearClipPlane = 0.08f; camera.farClipPlane = 180; camera.fieldOfView = 62;
            var player = go.AddComponent<PlayerController>();
            player.playerCamera = camera.transform;
            go.AddComponent<PlayerProductEffects>();
            // Keep the system cursor hidden for camera look, but show a small
            // in-game pointer at the centre of the player's view.
            go.AddComponent<PlayerAimReticle>();
            go.AddComponent<CityInputBridge>();
            var switcher = go.AddComponent<PlayerCameraModeSwitcher>();
            switcher.SetFirstPersonPosition(new Vector3(0, 1.65f, 0.05f));
            switcher.SetMode(PlayerCameraModeSwitcher.CameraMode.ThirdPerson);
            if (safehouseFarming != null)
                safehouseFarming.ConfigurePlayer(go);
            return player;
        }

        private void AddPerson(string id, string name, Vector3 position, Material clothing, string conversation, bool wander)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2, NavMesh.AllAreas)) position = hit.position;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, position.z < 0 ? 0 : 180, 0);
            bool police = id == "officer";
            Puppet(go.transform, clothing, police);
            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 0.9f; collider.height = 1.8f; collider.radius = 0.28f;
            var actor = go.AddComponent<CityActor>();
            actor.id = id; actor.displayName = name; actor.defaultDialogue = conversation; actor.IsPerson = true;
            actors.Add(id, actor);
            if (wander)
            {
                var agent = go.AddComponent<NavMeshAgent>();
                agent.radius = 0.35f; agent.height = 1.8f; agent.speed = 1.5f;
                if (police) go.AddComponent<PoliceBehavior>();
                go.AddComponent<NPCWanderRoadOnly>();
            }
        }

        private void AddLocation(string id, string name, Vector3 position, Vector3 scale)
        {
            var go = Box(name, transform, position + Vector3.up * scale.y * 0.5f, scale, (id == "safehouse" || id == "extraction") ? mint : glass, false);
            var actor = go.AddComponent<CityActor>();
            actor.id = id; actor.displayName = name;
            actors.Add(id, actor);
        }

        private void Puppet(Transform parent, Material clothing, bool police)
        {
            var visual = new GameObject("PlayerVisual").transform;
            visual.SetParent(parent, false);
            var puppet = visual.gameObject.AddComponent<StreetPuppet>();
            var body = new GameObject("Body").transform;
            body.SetParent(visual, false);
            puppet.body = body;
            Box("Jacket", body, new Vector3(0, 1.13f, 0), new Vector3(0.52f, 0.65f, 0.3f), clothing, false);
            Shape(PrimitiveType.Sphere, "Head", body, new Vector3(0, 1.68f, 0), new Vector3(0.34f, 0.4f, 0.34f), skin, false);
            Box("Hair or cap", body, new Vector3(0, 1.85f, -0.02f), new Vector3(0.36f, 0.12f, 0.35f), navy, false);
            Box("Nose", body, new Vector3(0, 1.68f, 0.19f), new Vector3(0.08f, 0.08f, 0.08f), skin, false);
            puppet.leftArm = Limb(body, "Left arm", new Vector3(-0.36f, 1.4f, 0), 0.56f, 0.17f, clothing);
            puppet.rightArm = Limb(body, "Right arm", new Vector3(0.36f, 1.4f, 0), 0.56f, 0.17f, clothing);
            puppet.leftLeg = Limb(body, "Left leg", new Vector3(-0.15f, 0.8f, 0), 0.76f, 0.2f, navy);
            puppet.rightLeg = Limb(body, "Right leg", new Vector3(0.15f, 0.8f, 0), 0.76f, 0.2f, navy);
            if (police)
            {
                Box("Badge", body, new Vector3(-0.13f, 1.3f, 0.161f), new Vector3(0.09f, 0.1f, 0.015f), glass, false);
                Box("Cap brim", body, new Vector3(0, 1.84f, 0.15f), new Vector3(0.4f, 0.04f, 0.2f), navy, false);
            }
        }
        private Transform Limb(Transform parent, string name, Vector3 position, float length, float width, Material material)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false); pivot.localPosition = position;
            Box(name + " mesh", pivot, Vector3.down * length * 0.5f, new Vector3(width, length, width), material, false);
            return pivot;
        }
        private GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = true)
            => Shape(PrimitiveType.Cube, name, parent, position, scale, material, solid);

        private GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
            {
                var collider = go.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
            }
            return go;
        }
        internal static void CreateSign(Transform parent, string text, Vector3 position, float size, int fontSize,
            Color color, Font font, Material material, float yaw = 0)
        {
            var go = new GameObject("Sign " + text, typeof(TextMesh));
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var sign = go.GetComponent<TextMesh>();
            sign.text = text; sign.characterSize = size; sign.fontSize = fontSize; sign.anchor = TextAnchor.MiddleCenter;
            sign.color = color;
            sign.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void RefreshSignTexture(Font font)
        {
            if (font == signFont)
                signMaterial.mainTexture = font.material.mainTexture;
        }

        private void OnDestroy()
        {
            Font.textureRebuilt -= RefreshSignTexture;
            foreach (var material in materials)
                if (material != null) Destroy(material);
        }
    }
}
