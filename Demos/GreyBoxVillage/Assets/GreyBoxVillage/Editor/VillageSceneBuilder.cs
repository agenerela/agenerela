using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela;
using Demos.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>Authoring utility. The saved scene and baked data work without this assembly.</summary>
    public static class VillageSceneBuilder
    {
        public const string Root = "Assets/GreyBoxVillage";
        public const string NavPath = Root + "/VillageNavMesh.villagenavmesh";
        private static Material ground, stone, dark, blue, gold, water, restricted, leaf, villagerGreen, wolfGrey;

        [MenuItem("Tools/Demos/GreyBoxVillage/Create Initial Scenes")]
        public static void CreateInitialScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoLauncher.VillagePath) != null)
                throw new InvalidOperationException("The village already exists. Use Rebuild Village or Rebake Navigation.");
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && active.isDirty)
                throw new InvalidOperationException("Save the untitled scene before creating the village.");
            LoadMaterials();

            // Unity cannot add scenes alongside an untitled scene. Replace only a clean one.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                string.IsNullOrEmpty(active.path) ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            BuildWorld();
            EditorSceneManager.SaveScene(scene, DemoLauncher.VillagePath);

            var launcher = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(launcher);
            CreateCamera();
            CreateLight();
            new GameObject("DemoLauncher").AddComponent<DemoLauncher>();
            EditorSceneManager.SaveScene(launcher, DemoLauncher.LauncherPath);
            EditorSceneManager.CloseScene(launcher, true);
            SceneManager.SetActiveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in new[] { DemoLauncher.LauncherPath, DemoLauncher.VillagePath })
                if (!scenes.Any(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Replaces everything in the open village scene with the world built here, keeping the
        /// scene asset and its GUID, and bakes navigation. The world is the evaluation agent's
        /// (UnityProject/Assets/Evaluation/agent.json): every one of its targets is a Targetable here.
        /// </summary>
        [MenuItem("Tools/Demos/GreyBoxVillage/Rebuild Village")]
        public static void RebuildVillage()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != DemoLauncher.VillagePath || Application.isPlaying)
                throw new InvalidOperationException("Open GreyBoxVillage in Edit mode first.");
            LoadMaterials();
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            BuildWorld();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Demos/GreyBoxVillage/Rebake Navigation")]
        public static void RebakeNavigation()
        {
            if (SceneManager.GetActiveScene().path != DemoLauncher.VillagePath || Application.isPlaying)
                throw new InvalidOperationException("Open GreyBoxVillage in Edit mode first.");
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            Bake(roots.Single(x => x.name == "BakedNavigation").GetComponent<VillageNavigation>(),
                roots.Single(x => x.name == "VillageGeometry").transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void LoadMaterials()
        {
            EnsureFolder(Root + "/Materials");
            ground = Material("Ground", new Color(0.43f, 0.46f, 0.43f));
            stone = Material("Stone", new Color(0.64f, 0.65f, 0.63f));
            dark = Material("DarkStone", new Color(0.27f, 0.30f, 0.32f));
            blue = Material("PlayerBlue", new Color(0.22f, 0.49f, 0.73f));
            gold = Material("GuardGold", new Color(0.75f, 0.58f, 0.25f));
            water = Material("River", new Color(0.24f, 0.38f, 0.43f));
            restricted = Material("Restricted", new Color(0.72f, 0.22f, 0.2f));
            leaf = Material("Leaf", new Color(0.2f, 0.36f, 0.22f));
            villagerGreen = Material("VillagerGreen", new Color(0.45f, 0.7f, 0.4f));
            wolfGrey = Material("WolfGrey", new Color(0.5f, 0.5f, 0.52f));
        }

        // ---- The world ---------------------------------------------------------------------------
        // x runs east, z north. The village sits inside its wall south of the river; the north bank
        // holds the tower, the lord's keep, the woods and the cave. Every Targetable root sits on
        // the walkable approach to its place, so move_to ends somewhere the guard can stand.

        private static void BuildWorld()
        {
            var geometry = new GameObject("VillageGeometry").transform;

            // Ground, river and banks. The river bed is solid, so nothing falls out of the world.
            Box("ground_south", geometry, new Vector3(0, -0.5f, -5), new Vector3(100, 1, 50), ground);
            Box("ground_north", geometry, new Vector3(0, -0.5f, 38), new Vector3(100, 1, 24), ground);
            Box("river_bed", geometry, new Vector3(0, -1.5f, 23), new Vector3(100, 1, 6), dark);
            var river = Box("river_water", geometry, new Vector3(0, -0.35f, 23), new Vector3(100, 0.1f, 6), water);
            UnityEngine.Object.DestroyImmediate(river.GetComponent<Collider>());
            Wall("river_bank", geometry, -50, -3, 19.85f, 0.8f, dark, alongX: true);
            Wall("river_bank", geometry, 3, 26, 19.85f, 0.8f, dark, alongX: true);
            Wall("river_bank", geometry, 34, 50, 19.85f, 0.8f, dark, alongX: true);
            Wall("river_bank", geometry, -50, -3, 26.15f, 0.8f, dark, alongX: true);
            Wall("river_bank", geometry, 3, 50, 26.15f, 0.8f, dark, alongX: true);

            // The outer boundary keeps the player on the ground.
            Wall("boundary", geometry, -30, 50, -50, 1.4f, dark, alongX: false);
            Wall("boundary", geometry, -30, 50, 50, 1.4f, dark, alongX: false);
            Wall("boundary", geometry, -50, 50, -30, 1.4f, dark, alongX: true);
            Wall("boundary", geometry, -50, 50, 50, 1.4f, dark, alongX: true);

            // The village wall, with the gate to the south and the eastern road through the east side.
            Wall("village_wall", geometry, -45, -3.5f, -15, 2, stone, alongX: true);
            Wall("village_wall", geometry, 3.5f, 45, -15, 2, stone, alongX: true);
            Wall("village_wall", geometry, -15, 19.7f, -45, 2, stone, alongX: false);
            Wall("village_wall", geometry, -15, -3, 45, 2, stone, alongX: false);
            Wall("village_wall", geometry, 3, 19.7f, 45, 2, stone, alongX: false);

            var gate = Place("village_gate", "The main gate in the village wall", geometry, new Vector3(0, 0, -15));
            Box("left_pier", gate, new Vector3(-3.8f, 1.8f, 0), new Vector3(1.6f, 3.6f, 1.5f), stone);
            Box("right_pier", gate, new Vector3(3.8f, 1.8f, 0), new Vector3(1.6f, 3.6f, 1.5f), stone);
            Box("lintel", gate, new Vector3(0, 4, 0), new Vector3(9.2f, 0.8f, 1.5f), dark);
            Label("village_gate", gate, new Vector3(0, 5.1f, 0));

            var well = Place("well", "The stone well in the village square", geometry, new Vector3(0, 0, -2.4f));
            Box("well_ring", well, new Vector3(0, 0.5f, 2.4f), new Vector3(2.4f, 1, 2.4f), stone);
            NoCollider(Box("well_water", well, new Vector3(0, 1.01f, 2.4f), new Vector3(1.6f, 0.02f, 1.6f), water));
            Label("well", well, new Vector3(0, 2.2f, 2.4f));

            var market = Place("market", "The open-air market where villagers trade", geometry, new Vector3(16, 0, -0.5f));
            foreach (float x in new[] { -4f, 0f, 4f })
            {
                Box("stall", market, new Vector3(x, 0.6f, 4), new Vector3(3, 1.2f, 2), dark);
                NoCollider(Box("awning", market, new Vector3(x, 2.4f, 4), new Vector3(3.2f, 0.15f, 2.4f), gold));
            }
            Label("market", market, new Vector3(0, 3.6f, 4));

            var campfire = Place("campfire", "The campfire in the guards' camp", geometry, new Vector3(14, 0, -8.2f));
            Box("fire", campfire, new Vector3(0, 0.25f, -1.8f), new Vector3(1, 0.5f, 1), gold);
            Box("log", campfire, new Vector3(0, 0.2f, -3.2f), new Vector3(2.2f, 0.4f, 0.5f), dark);
            Label("campfire", campfire, new Vector3(0, 1.8f, -1.8f));

            var bellPost = Landmark("alarm_bell", geometry, new Vector3(8.5f, 0, -10.5f));
            Box("bell_post", bellPost, new Vector3(0, 1.5f, 0), new Vector3(0.3f, 3, 0.3f), dark);
            var bell = Box("bell", bellPost, new Vector3(0, 2.9f, 0.45f), new Vector3(0.8f, 0.8f, 0.8f), gold);
            bell.isStatic = false;
            Label("alarm bell", bellPost, new Vector3(0, 4, 0));

            var dummy = Place("training_dummy", "A straw training dummy in the guards' camp", geometry, new Vector3(21, 0, -12.3f));
            Box("dummy_post", dummy, new Vector3(0, 1.3f, 1.3f), new Vector3(0.4f, 2.6f, 0.4f), dark);
            Box("dummy_body", dummy, new Vector3(0, 1.8f, 1.3f), new Vector3(1.2f, 1, 0.6f), gold);
            Box("dummy_arms", dummy, new Vector3(0, 2.1f, 1.3f), new Vector3(2.2f, 0.3f, 0.3f), dark);
            Label("training_dummy", dummy, new Vector3(0, 3.7f, 1.3f));

            var shelter = Place("shelter", "The shelter where villagers take cover", geometry, new Vector3(-18, 0, 10));
            Box("shelter_roof", shelter, new Vector3(0, 3, 1), new Vector3(8.4f, 0.4f, 6.4f), dark);
            Box("shelter_back", shelter, new Vector3(0, 1.5f, 4), new Vector3(8, 3, 0.4f), stone);
            Box("shelter_left", shelter, new Vector3(-4, 1.5f, 1), new Vector3(0.4f, 3, 6), stone);
            Box("shelter_right", shelter, new Vector3(4, 1.5f, 1), new Vector3(0.4f, 3, 6), stone);
            Label("shelter", shelter, new Vector3(0, 4.2f, 1));

            var road = Place("eastern_road", "The road leaving the village to the east", geometry, new Vector3(47, 0, 0));
            NoCollider(Box("road_surface", geometry, new Vector3(33, 0.01f, 0), new Vector3(34, 0.02f, 4), dark));
            Label("eastern_road", road, new Vector3(0, 2, 0));

            var bridge = Place("bridge", "The wooden bridge over the stream", geometry, new Vector3(0, 0, 23));
            Box("bridge_deck", bridge, new Vector3(0, -0.15f, 0), new Vector3(6, 0.3f, 7.6f), stone);
            foreach (float x in new[] { -2.9f, 2.9f })
                Box("bridge_rail", bridge, new Vector3(x, 0.6f, 0), new Vector3(0.25f, 1.2f, 7.6f), dark);
            Label("bridge", bridge, new Vector3(0, 2, 0));

            var docks = Place("docks", "The docks where supply boats unload", geometry, new Vector3(30, 0, 21.5f));
            Box("dock_deck", docks, new Vector3(0, -0.15f, 0.5f), new Vector3(8, 0.3f, 4), stone);
            foreach (float x in new[] { -3.9f, 3.9f })
                Box("dock_rail", docks, new Vector3(x, 0.6f, 0.5f), new Vector3(0.25f, 1.2f, 4), dark);
            Box("dock_rail", docks, new Vector3(0, 0.6f, 2.4f), new Vector3(8, 1.2f, 0.25f), dark);
            Box("crate", docks, new Vector3(2.4f, 0.5f, 1.4f), new Vector3(1, 1, 1), gold);
            Label("docks", docks, new Vector3(0, 2, 0.5f));

            var tower = Place("north_tower", "The tower on the north side of the village", geometry, new Vector3(-20, 0, 34.5f));
            Box("tower_block", tower, new Vector3(0, 3.5f, 3.5f), new Vector3(4, 7, 4), stone);
            Box("tower_cap", tower, new Vector3(0, 7.25f, 3.5f), new Vector3(4.7f, 0.5f, 4.7f), dark);
            Box("tower_door", tower, new Vector3(0, 1, 1.46f), new Vector3(1.3f, 2, 0.08f), dark);
            Label("north_tower", tower, new Vector3(0, 8.5f, 3.5f));

            foreach (var (x, z, w, d) in new[]
            {
                (-8f, 8f, 5f, 4f), (8f, 10f, 5f, 4f), (-32f, 6f, 6f, 5f),
                (31f, 9f, 5f, 5f), (-6f, -9f, 4f, 4f), (32f, -9f, 5f, 4f)
            })
                Box("house", geometry, new Vector3(x, 1.5f, z), new Vector3(w, 3, d), stone);

            // The woods, north-east of the river, where the wolf lives.
            foreach (var (x, z) in new[]
            {
                (30f, 31f), (35f, 29f), (41f, 31f), (46f, 34f), (29f, 37f), (34f, 41f), (42f, 38f),
                (47f, 42f), (31f, 46f), (38f, 47f), (44f, 47f), (27f, 43f)
            })
                Tree(geometry, new Vector3(x, 0, z));

            // The off-limits places: walls and buildings here; their trigger zones further down.
            Wall("armory_fence", geometry, -28, -14, -4, 1.6f, restricted, alongX: true);
            Wall("armory_fence", geometry, -28, -14, -12, 1.6f, restricted, alongX: true);
            Wall("armory_fence", geometry, -12, -4, -28, 1.6f, restricted, alongX: false);
            Wall("armory_fence", geometry, -12, -9.5f, -14, 1.6f, restricted, alongX: false);
            Wall("armory_fence", geometry, -6.5f, -4, -14, 1.6f, restricted, alongX: false);
            Box("armory_building", geometry, new Vector3(-25, 1.75f, -8), new Vector3(5, 3.5f, 7), stone);

            Wall("keep_wall", geometry, 11, 16.5f, 34, 2.5f, stone, alongX: true);
            Wall("keep_wall", geometry, 19.5f, 25, 34, 2.5f, stone, alongX: true);
            Wall("keep_wall", geometry, 11, 25, 46, 2.5f, stone, alongX: true);
            Wall("keep_wall", geometry, 34, 46, 11, 2.5f, stone, alongX: false);
            Wall("keep_wall", geometry, 34, 46, 25, 2.5f, stone, alongX: false);
            Box("keep_building", geometry, new Vector3(18, 3, 43.5f), new Vector3(8, 6, 4), stone);

            Box("cave_rock", geometry, new Vector3(-40, 2.5f, 47.5f), new Vector3(10, 5, 4), dark);
            Box("cave_rock", geometry, new Vector3(-45, 2, 43.5f), new Vector3(3, 4, 7), dark);
            Box("cave_rock", geometry, new Vector3(-35, 2, 43.5f), new Vector3(3, 4, 7), dark);
            Box("cave_door", geometry, new Vector3(-40, 1.5f, 45.3f), new Vector3(7, 3, 0.4f), restricted);

            // Points the guard, the villagers and the wolf walk between. Not targets.
            var square = new[] { well, market, Point(geometry, "square_a", 6, 4), Point(geometry, "square_b", -6, 2),
                Point(geometry, "square_c", 10, -4), Point(geometry, "square_d", -4, -6) };
            var woods = new[] { Point(geometry, "woods_a", 32, 33), Point(geometry, "woods_b", 39, 34),
                Point(geometry, "woods_c", 44, 44), Point(geometry, "woods_d", 34, 44), Point(geometry, "woods_e", 38, 39) };
            var prowl = new[] { Point(geometry, "prowl_north_bank", 6, 30), Point(geometry, "prowl_square", 3, 12),
                Point(geometry, "prowl_docks", 26, 16) };
            var den = Point(geometry, "wolf_den", 47, 47);

            var navigation = new GameObject("BakedNavigation").AddComponent<VillageNavigation>();
            Bake(navigation, geometry);

            // Trigger zones stay out of VillageGeometry, so the bake never treats them as walls.
            var zoneRoot = new GameObject("RestrictedZones").transform;
            var armory = Zone(zoneRoot, "armory", "armory", "The armory, where the village's weapons are kept",
                new Vector3(-18.3f, 1, -8), new Vector3(8.2f, 2, 7.6f), new Vector3(-12.6f, 0, -8));
            Zone(zoneRoot, "lords_keep", "lord's keep", "The lord's keep, where the lord lives",
                new Vector3(18, 1, 38.5f), new Vector3(13.2f, 2, 8.4f), new Vector3(18, 0, 32.6f));
            Zone(zoneRoot, "locked_cave", "mouth of the locked cave", "A cave whose entrance is locked",
                new Vector3(-40, 1, 42.6f), new Vector3(6.8f, 2, 5f), new Vector3(-40, 0, 38.6f));

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 0.05f, -18f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0, 0.9f, 0);
            controller.stepOffset = 0.25f;
            var movement = player.AddComponent<DemoPlayerController>();
            Capsule("player_body", player.transform, blue, 1f);
            Label("player", player.transform, new Vector3(0, 2.4f, 0));

            var guardObject = new GameObject("VillageGuard");
            guardObject.transform.position = new Vector3(-2.5f, 0, -13);
            var nav = guardObject.AddComponent<NavMeshAgent>();
            nav.radius = 0.4f;
            nav.height = 1.8f;
            nav.speed = 3.5f;
            nav.stoppingDistance = 0.8f;
            var guard = guardObject.AddComponent<VillageGuard>();
            Capsule("guard_body", guardObject.transform, gold, 1f);
            var guardCollider = guardObject.AddComponent<CapsuleCollider>();
            guardCollider.center = new Vector3(0, 0.9f, 0);
            guardCollider.height = 1.8f;
            guardCollider.radius = 0.4f;
            Label("guard", guardObject.transform, new Vector3(0, 2.4f, 0));
            guard.PatrolRoute = new[] { gate, campfire, market, bridge, well, shelter, armory.Entrance };

            var villagers = new List<Villager>();
            for (int i = 0; i < 4; i++)
            {
                var v = new GameObject("villager_" + (i + 1));
                v.transform.position = square[i + 2].position;
                var vnav = v.AddComponent<NavMeshAgent>();
                vnav.radius = 0.3f;
                vnav.height = 1.6f;
                vnav.speed = 2f;
                vnav.stoppingDistance = 0.4f;
                Capsule("villager_body", v.transform, villagerGreen, 0.85f);
                var villager = v.AddComponent<Villager>();
                villager.WanderPoints = square;
                villagers.Add(villager);
            }

            var alarm = bellPost.gameObject.AddComponent<VillageAlarm>();
            alarm.Bell = bell.GetComponent<Renderer>();
            alarm.Villagers = villagers.ToArray();
            alarm.Shelter = shelter;
            guard.Alarm = alarm;

            var wolfObject = new GameObject("wolf");
            wolfObject.transform.position = new Vector3(38, 0, 40);
            var wnav = wolfObject.AddComponent<NavMeshAgent>();
            wnav.radius = 0.35f;
            wnav.height = 1f;
            wnav.speed = 3f;
            wnav.stoppingDistance = 0.5f;
            NoCollider(Box("wolf_body", wolfObject.transform, new Vector3(0, 0.55f, 0), new Vector3(0.6f, 0.6f, 1.3f), wolfGrey)).isStatic = false;
            NoCollider(Box("wolf_head", wolfObject.transform, new Vector3(0, 0.8f, 0.8f), new Vector3(0.45f, 0.45f, 0.5f), wolfGrey)).isStatic = false;
            AddTargetable(wolfObject, "wolf", "A wolf that came out of the woods", "creature");
            var wolf = wolfObject.AddComponent<VillageWolf>();
            wolf.WanderPoints = woods;
            wolf.ProwlPoints = prowl;
            wolf.Den = den;
            Label("wolf", wolfObject.transform, new Vector3(0, 1.8f, 0));

            var camera = CreateCamera();
            camera.orthographic = false;
            camera.fieldOfView = 50;
            camera.farClipPlane = 250;
            var rig = camera.gameObject.AddComponent<DemoCameraRig>();
            rig.Player = player.transform;
            camera.transform.position = new Vector3(0, 14.8f, -29.6f);
            camera.transform.LookAt(new Vector3(0, 1, -18));
            CreateLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.7f, 0.7f, 0.7f);
            RenderSettings.skybox = null;

            var ui = new GameObject("CommandUI");
            var overlay = ui.AddComponent<DecisionDebugOverlay>();
            var input = ui.AddComponent<DemoCommandInput>();
            input.Player = movement;
            input.Overlay = overlay;
            // Set fields before enabling subscription callbacks.
            ui.SetActive(false);
            var commands = ui.AddComponent<VillageCommands>();
            commands.Input = input;
            commands.Overlay = overlay;
            commands.Player = player.transform;
            commands.Guard = guard;
            var senses = ui.AddComponent<GuardSenses>();
            senses.Commands = commands;
            senses.Guard = guard;
            senses.Player = player.transform;
            senses.Wolf = wolf;
            commands.Senses = senses;
            ui.AddComponent<DecisionHistoryPanel>().Commands = commands;
            ui.SetActive(true);
        }

        private static void Bake(VillageNavigation navigation, Transform geometry)
        {
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(geometry, ~0, NavMeshCollectGeometry.PhysicsColliders,
                0, new List<NavMeshBuildMarkup>(), sources);
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.4f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.25f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.1f;
            var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(new Vector3(0, 3, 10), new Vector3(104, 14, 84)), Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("Village NavMesh bake failed.");
            data.name = "VillageNavMesh";
            // NavMeshData .asset files force binary serialization even in Force Text projects.
            // Keep the editor-baked data as JSON; the importer creates the native asset in Library.
            System.IO.File.WriteAllText(NavPath, EditorJsonUtility.ToJson(data));
            UnityEngine.Object.DestroyImmediate(data);
            AssetDatabase.ImportAsset(NavPath, ImportAssetOptions.ForceSynchronousImport);
            navigation.BakedData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            if (navigation.BakedData == null) throw new InvalidOperationException("Baked navigation import failed.");
            EditorUtility.SetDirty(navigation);
            AssetDatabase.SaveAssets();
        }

        // A named place the guard can be sent to: a Targetable on the walkable approach.
        private static Transform Place(string id, string description, Transform parent, Vector3 position)
        {
            var place = Landmark(id, parent, position);
            AddTargetable(place.gameObject, id, description, "place");
            return place;
        }

        private static RestrictedZone Zone(Transform parent, string id, string displayName, string description,
            Vector3 centre, Vector3 size, Vector3 entrance)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var zone = go.AddComponent<RestrictedZone>();
            zone.Id = id;
            zone.DisplayName = displayName;
            AddTargetable(go, id, description + ". Off limits to the player", "restricted");
            zone.Entrance = Landmark(id + "_entrance", parent, entrance);
            var floor = NoCollider(Box(id + "_floor", go.transform, new Vector3(0, -0.98f, 0), new Vector3(size.x, 0.02f, size.z), restricted));
            floor.isStatic = false;
            Label(id + " / off limits", go.transform, new Vector3(0, 2.6f, 0));
            return zone;
        }

        // Targetable warns from OnValidate about its empty Id the moment it is added, before the
        // Id can be set, so the builder mutes the log for that one call.
        private static void AddTargetable(GameObject go, string id, string description, string category)
        {
            bool logging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            Targetable targetable;
            try { targetable = go.AddComponent<Targetable>(); }
            finally { Debug.unityLogger.logEnabled = logging; }
            targetable.Id = id;
            targetable.Description = description;
            targetable.Category = category;
        }

        private static Transform Point(Transform parent, string name, float x, float z) => Landmark(name, parent, new Vector3(x, 0, z));

        // A straight wall from a to b along one axis, at the given position on the other.
        private static void Wall(string name, Transform parent, float a, float b, float at, float height, Material material, bool alongX)
        {
            float mid = (a + b) / 2, length = Mathf.Abs(b - a);
            var position = alongX ? new Vector3(mid, height / 2, at) : new Vector3(at, height / 2, mid);
            var scale = alongX ? new Vector3(length, height, 0.6f) : new Vector3(0.6f, height, length);
            Box(name, parent, position, scale, material);
        }

        private static void Tree(Transform parent, Vector3 position)
        {
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "tree";
            trunk.transform.SetParent(parent, false);
            trunk.transform.localPosition = position + new Vector3(0, 1.5f, 0);
            trunk.transform.localScale = new Vector3(1.4f, 1.5f, 1.4f);
            trunk.GetComponent<Renderer>().sharedMaterial = leaf;
            trunk.isStatic = true;
        }

        private static GameObject NoCollider(GameObject go)
        {
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Transform Landmark(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.isStatic = true;
            return go;
        }

        private static void Capsule(string name, Transform parent, Material material, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0.9f * size, 0);
            go.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f) * size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        private static void Label(string text, Transform parent, Vector3 position)
        {
            var go = new GameObject("label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.rotation = Quaternion.Euler(45, 0, 0);
            var label = go.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.16f;
            label.color = Color.white;
            go.AddComponent<DemoBillboard>();
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.14f, 0.17f, 0.19f);
            camera.orthographic = true;
            camera.orthographicSize = 21;
            camera.farClipPlane = 150;
            go.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateLight()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        private static Material Material(string name, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");
            if (existing != null) return existing;
            var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(
                "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader"));
            material.name = name;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0);
            AssetDatabase.CreateAsset(material, Root + "/Materials/" + name + ".mat");
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
