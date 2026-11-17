using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela;
using Agenerela.Providers;
using Demos.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>Authoring utility. The saved scene and baked data work without this assembly.</summary>
    public static partial class VillageSceneBuilder
    {
        public const string Root = "Assets/GreyBoxVillage";
        public const string NavPath = Root + "/VillageNavMesh.villagenavmesh";
        public const string AgentsFolder = Root + "/Agents";
        public const string ProviderPath = AgentsFolder + "/VillageOllama.asset";
        private static Material ground, stone, dark, gold, silver, water, restricted, leaf;

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
            LoadArt();
            BuildWorld();
            RecordPlacedModels(scene);
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
            LoadArt();
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            BuildWorld();
            RecordPlacedModels(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Rebuild Village from the command line, for an agent or CI with the editor closed:
        /// <c>Unity -batchmode -projectPath Demos/GreyBoxVillage -executeMethod
        /// Demos.GreyBoxVillage.Editor.VillageSceneBuilder.RebuildFromCommandLine -quit</c>.
        /// </summary>
        public static void RebuildFromCommandLine()
        {
            EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Single);
            RebuildVillage();
        }

        /// <summary>
        /// Records every change the builder made to a placed model (where it stands, its turn and
        /// size, its static flags) as a prefab override. A change made by script to a prefab
        /// instance is saved only once recorded: without this, every model was saved at the
        /// origin and none of them batched.
        /// </summary>
        private static void RecordPlacedModels(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsPartOfPrefabInstance(t)) continue;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                    foreach (var r in t.GetComponents<Renderer>()) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
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
            gold = Material("GuardGold", new Color(0.75f, 0.58f, 0.25f));
            silver = Material("GuardSilver", new Color(0.62f, 0.68f, 0.76f));
            water = Material("River", new Color(0.24f, 0.38f, 0.43f));
            restricted = Material("Restricted", new Color(0.72f, 0.22f, 0.2f));
            leaf = Material("Leaf", new Color(0.2f, 0.36f, 0.22f));
        }

        // ---- The world ---------------------------------------------------------------------------
        // x runs east, z north. The village sits inside its wall south of the river; the north bank
        // holds the tower, the lord's keep, the woods and the cave. Every Targetable root sits on
        // the walkable approach to its place, so move_to ends somewhere the guard can stand.

        private static void BuildWorld()
        {
            walls.Clear();
            trees.Clear();
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

            var well = Place("well", "The stone well in the village square", geometry, new Vector3(0, 0, -2.4f));
            Box("well_ring", well, new Vector3(0, 0.5f, 2.4f), new Vector3(2.4f, 1, 2.4f), stone);
            NoCollider(Box("well_water", well, new Vector3(0, 1.01f, 2.4f), new Vector3(1.6f, 0.02f, 1.6f), water));

            var market = Place("market", "The open-air market where villagers trade", geometry, new Vector3(16, 0, -0.5f));
            foreach (float x in new[] { -4f, 0f, 4f })
            {
                Box("stall", market, new Vector3(x, 0.6f, 4), new Vector3(3, 1.2f, 2), dark);
                NoCollider(Box("awning", market, new Vector3(x, 2.4f, 4), new Vector3(3.2f, 0.15f, 2.4f), gold));
            }

            var campfire = Place("campfire", "The campfire in the guards' camp", geometry, new Vector3(14, 0, -8.2f));
            Box("fire", campfire, new Vector3(0, 0.25f, -1.8f), new Vector3(1, 0.5f, 1), gold);
            Box("log", campfire, new Vector3(0, 0.2f, -3.2f), new Vector3(2.2f, 0.4f, 0.5f), dark);

            var bellPost = Landmark("alarm_bell", geometry, new Vector3(8.5f, 0, -10.5f));
            Box("bell_post", bellPost, new Vector3(0, 1.5f, 0), new Vector3(0.3f, 3, 0.3f), dark);
            var bell = Box("bell", bellPost, new Vector3(0, 2.9f, 0.45f), new Vector3(0.8f, 0.8f, 0.8f), gold);
            bell.isStatic = false;

            var dummy = Place("training_dummy", "A straw training dummy in the guards' camp", geometry, new Vector3(21, 0, -12.3f));
            Box("dummy_post", dummy, new Vector3(0, 1.3f, 1.3f), new Vector3(0.4f, 2.6f, 0.4f), dark);
            Box("dummy_body", dummy, new Vector3(0, 1.8f, 1.3f), new Vector3(1.2f, 1, 0.6f), gold);
            Box("dummy_arms", dummy, new Vector3(0, 2.1f, 1.3f), new Vector3(2.2f, 0.3f, 0.3f), dark);

            var shelter = Place("shelter", "The shelter where villagers take cover", geometry, new Vector3(-18, 0, 10));
            Box("shelter_roof", shelter, new Vector3(0, 3, 1), new Vector3(8.4f, 0.4f, 6.4f), dark);
            Box("shelter_back", shelter, new Vector3(0, 1.5f, 4), new Vector3(8, 3, 0.4f), stone);
            Box("shelter_left", shelter, new Vector3(-4, 1.5f, 1), new Vector3(0.4f, 3, 6), stone);
            Box("shelter_right", shelter, new Vector3(4, 1.5f, 1), new Vector3(0.4f, 3, 6), stone);

            var road = Place("eastern_road", "The road leaving the village to the east", geometry, new Vector3(47, 0, 0));
            NoCollider(Box("road_surface", geometry, new Vector3(33, 0.01f, 0), new Vector3(34, 0.02f, 4), dark));

            var bridge = Place("bridge", "The wooden bridge over the stream", geometry, new Vector3(0, 0, 23));
            Box("bridge_deck", bridge, new Vector3(0, -0.15f, 0), new Vector3(6, 0.3f, 7.6f), stone);
            foreach (float x in new[] { -2.9f, 2.9f })
                Box("bridge_rail", bridge, new Vector3(x, 0.6f, 0), new Vector3(0.25f, 1.2f, 7.6f), dark);

            var docks = Place("docks", "The docks where supply boats unload", geometry, new Vector3(30, 0, 21.5f));
            Box("dock_deck", docks, new Vector3(0, -0.15f, 0.5f), new Vector3(8, 0.3f, 4), stone);
            foreach (float x in new[] { -3.9f, 3.9f })
                Box("dock_rail", docks, new Vector3(x, 0.6f, 0.5f), new Vector3(0.25f, 1.2f, 4), dark);
            Box("dock_rail", docks, new Vector3(0, 0.6f, 2.4f), new Vector3(8, 1.2f, 0.25f), dark);
            Box("crate", docks, new Vector3(2.4f, 0.5f, 1.4f), new Vector3(1, 1, 1), gold);

            var tower = Place("north_tower", "The tower on the north side of the village", geometry, new Vector3(-20, 0, 34.5f));
            Box("tower_block", tower, new Vector3(0, 3.5f, 3.5f), new Vector3(4, 7, 4), stone);
            Box("tower_cap", tower, new Vector3(0, 7.25f, 3.5f), new Vector3(4.7f, 0.5f, 4.7f), dark);
            Box("tower_door", tower, new Vector3(0, 1, 1.46f), new Vector3(1.3f, 2, 0.08f), dark);

            foreach (var (x, z, w, d, _, _) in Houses)
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
            var inventory = player.AddComponent<PlayerInventory>();
            inventory.Coins = 8;
            Figure(player, "char_player");

            // The scripted crowd. Each villager is a person a guard can name, and so one the
            // never_attack_villagers guard protects. Each follows the routine's day: the well at
            // dawn, its work (the fields outside the gate, or the market), the square at midday,
            // the campfire at dusk and home after dark, where the light by its door comes on.
            var routine = new GameObject("VillageRoutine").AddComponent<VillageRoutine>();
            var homes = new Dictionary<string, (Vector3 door, Vector3 inward)>
            {
                // Just outside each cottage's front door (build_kit.py: a and b face south, d west, e north).
                ["tomas"] = (new Vector3(-6f, 0, -6.0f), Vector3.back),
                ["ada"] = (new Vector3(8f, 0, 7.0f), Vector3.forward),
                ["pell"] = (new Vector3(-8f, 0, 5.0f), Vector3.forward),
                ["mira"] = (new Vector3(27.5f, 0, 9f), Vector3.right),
            };
            var jobs = new Dictionary<string, string>
                { ["tomas"] = VillageRoutine.Fields, ["ada"] = VillageRoutine.Market, ["pell"] = VillageRoutine.Fields, ["mira"] = VillageRoutine.Market };
            var homeRoot = new GameObject("Homes").transform;
            routine.Spots = new[]
            {
                Spot(geometry, VillageRoutine.Well, "drawing water at the well", (-2.2f, -1.2f), (2.2f, -1.0f), (0.6f, -3.6f)),
                Spot(geometry, VillageRoutine.Market, "at the market", (11.5f, 1.0f), (15f, 0.6f), (19.5f, 1.2f), (17.5f, -2.2f)),
                Spot(geometry, VillageRoutine.Fields, "working the fields outside the gate", (-8.5f, -20.5f), (-10f, -25f), (8.5f, -21f), (10f, -25.5f)),
                Spot(geometry, VillageRoutine.Square, "in the square", (6f, 4f), (-6f, 2f), (10f, -4f), (-4f, -6f), (-2.4f, 1.4f)),
                Spot(geometry, VillageRoutine.Campfire, "at the campfire", (16.2f, -9.6f), (15.6f, -11.9f), (12.2f, -11.6f)),
                new VillageRoutine.Spot { Id = VillageRoutine.Home, Words = "at home", Points = new Transform[0] },
            };
            var villagers = new List<Villager>();
            var names = new[] { "tomas", "ada", "pell", "mira" };
            for (int i = 0; i < names.Length; i++)
            {
                var v = new GameObject("villager_" + names[i]);
                v.transform.position = square[i + 2].position;
                var vnav = v.AddComponent<NavMeshAgent>();
                vnav.radius = 0.3f;
                vnav.height = 1.6f;
                vnav.speed = 2f;
                vnav.stoppingDistance = 0.4f;
                Figure(v, "char_villager_" + names[i]);
                Tag(v, char.ToUpperInvariant(names[i][0]) + names[i].Substring(1), "villager");
                AddTargetable(v, names[i], char.ToUpperInvariant(names[i][0]) + names[i].Substring(1) + ", a villager", "person");
                var villager = v.AddComponent<Villager>();
                villager.Routine = routine;
                villager.Job = jobs[names[i]];
                var (door, inward) = homes[names[i]];
                villager.Home = Landmark("home_" + names[i], homeRoot, door);
                villager.HomeLight = PointLight(homeRoot, door + inward * 0.6f + Vector3.up * 2.2f, new Color(1f, 0.72f, 0.4f), 1.5f, 5.5f);
                villager.HomeLight.name = "home_light_" + names[i];
                villager.HomeLight.enabled = false;
                villagers.Add(villager);
            }
            routine.Villagers = villagers.ToArray();
            routine.Spots[routine.Spots.Length - 1].Points = villagers.Select(v => v.Home).ToArray();

            var alarm = bellPost.gameObject.AddComponent<VillageAlarm>();
            alarm.Bell = bell.GetComponent<Renderer>();
            alarm.Villagers = villagers.ToArray();
            alarm.Shelter = shelter;

            var wolfObject = new GameObject("wolf");
            wolfObject.transform.position = new Vector3(38, 0, 40);
            var wnav = wolfObject.AddComponent<NavMeshAgent>();
            wnav.radius = 0.35f;
            wnav.height = 1f;
            wnav.speed = 3f;
            wnav.stoppingDistance = 0.5f;
            Figure(wolfObject, "wolf");
            Tag(wolfObject, "Wolf", "", 1.5f);
            AddTargetable(wolfObject, "wolf", "A wolf that came out of the woods", "creature");
            var wolf = wolfObject.AddComponent<VillageWolf>();
            wolf.WanderPoints = woods;
            wolf.ProwlPoints = prowl;
            wolf.Den = den;

            // The thief (F3): out of the world until the presenter sends it, so it is saved
            // inactive. It slips in by the fruit stall, reaches over Odile's counter and runs for
            // the far end of the eastern road.
            var thiefObject = new GameObject("thief");
            thiefObject.transform.position = new Vector3(23f, 0, 2.2f);
            var tnav = thiefObject.AddComponent<NavMeshAgent>();
            tnav.radius = 0.3f;
            tnav.height = 1.7f;
            tnav.speed = 2.6f;
            tnav.stoppingDistance = 0.3f;
            Figure(thiefObject, "char_thief");
            Tag(thiefObject, "Thief", "stranger");
            AddTargetable(thiefObject, "thief", "A hooded thief running off with stolen goods", "stranger");
            var thief = thiefObject.AddComponent<VillageThief>();
            thief.Entry = Point(geometry, "thief_entry", 23f, 2.2f);
            thief.Counter = Point(geometry, "thief_counter", 16.2f, 1.7f);
            thief.Escape = Point(geometry, "thief_escape", 48.5f, 0f);

            // The fire (F4), at the bread stall beside Odile's. Its flames and smoke are dressed
            // with the art, and saved switched off.
            var fireObject = new GameObject("stall_fire");
            fireObject.transform.position = new Vector3(12f, 0, 3.5f);
            var fire = fireObject.AddComponent<VillageFire>();
            fire.Well = well;
            fire.ThrowSpot = Point(geometry, "fire_throw_spot", 12f, 1.6f);
            fire.Smoke = Landmark("fire_smoke", fireObject.transform, new Vector3(0, 6f, 0));

            // The gate's doors, shut at night by the watch. Not in VillageGeometry, so the bake
            // never sees them: shut, their obstacle carves the running NavMesh, and their blocker
            // stops the player. It is on Ignore Raycast, which a guard's sight leaves out.
            var gateObject = new GameObject("VillageGate");
            gateObject.transform.position = gate.position;
            gateObject.layer = 2;
            var villageGate = gateObject.AddComponent<VillageGate>();
            var blocker = gateObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, 1.8f, 0);
            blocker.size = new Vector3(6.4f, 3.6f, 0.5f);
            blocker.enabled = false;
            var obstacle = gateObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0, 1.8f, 0);
            obstacle.size = new Vector3(6.4f, 3.6f, 0.8f);
            obstacle.carving = true;
            obstacle.enabled = false;
            villageGate.Blocker = blocker;
            villageGate.Obstacle = obstacle;
            // Just inside the gate, so a visitor outside it is within speaking distance (6 m).
            var watchPost = Landmark("night_watch_post", geometry, new Vector3(1.8f, 0, -13.4f));
            var offWatch = Landmark("off_watch_post", geometry, new Vector3(12.4f, 0, -8.4f));

            // The agents. Both guards ask the same provider config, so they share one provider and
            // one queue (DR-013, DR-015).
            var provider = ProviderConfig();
            var bram = Guard("Bram", new AgentIdentity
            {
                Name = "Bram",
                Role = "the guard of the village gate",
                Personality = "Brief and dutiful. Speaks in one short sentence.",
                Goals = "Keep the village safe, keep people out of places that are off limits, and help the player."
            }, "Bram, the guard at the village gate", new Vector3(-2.5f, 0, -13), gold, gate,
                new[] { gate, campfire, market, bridge, well, shelter, armory.Entrance }, 30f, provider, alarm, wolf, player.transform);
            var wren = Guard("Wren", new AgentIdentity
            {
                Name = "Wren",
                Role = "the guard of the bridge",
                Personality = "Watchful and dry. Speaks in one short sentence.",
                Goals = "Keep the bridge and the river safe, back up the other guard, and help the player."
            }, "Wren, the guard on the bridge", new Vector3(2, 0, 17), silver, bridge,
                new[] { bridge, docks, market, well }, 37f, provider, alarm, wolf, player.transform);
            foreach (var mind in new[] { bram, wren })
            {
                var body = (VillageGuard)mind.Body;
                body.Fire = fire;
                mind.Senses.Thief = thief;
                mind.Senses.Fire = fire;
            }

            var odile = Townsfolk<VillageMerchant>("Odile", new AgentIdentity
            {
                Name = "Odile",
                Role = "the merchant at the market",
                Personality = "Warm, shrewd and talkative. Calls people love. Speaks in one or two short sentences.",
                Goals = "Sell her goods at a fair price, keep her stall safe, and find someone to gather moonpetals for her."
            }, "Odile, the merchant at the market", new Vector3(16, 0, 5.4f), 180f, "char_odile", provider,
                // No people by proximity: the guards she can call come from GuardTargetSource.
                new[] { "item", "place" });
            var stock = new List<Targetable>();
            var counter = new Vector3(16, 1.1f, 3.3f);
            string[] wares = { "bread", "apple", "rope", "torch", "lantern" };
            for (int i = 0; i < wares.Length; i++)
            {
                var ware = new GameObject("stock_" + wares[i]);
                ware.transform.position = counter + new Vector3(-1.1f + i * 0.55f, 0, 0);
                Model("item_" + wares[i], ware.transform, Vector3.zero, 15f * i, 0.8f, isStatic: false);
                var item = ItemCatalog.Get(wares[i]);
                AddTargetable(ware, wares[i], item.Name + " on Odile's counter, " + item.Price + (item.Price == 1 ? " coin" : " coins"), "item");
                stock.Add(ware.GetComponent<Targetable>());
            }
            var merchant = (VillageMerchant)odile.Body;
            merchant.Stock = stock.ToArray();
            merchant.Thief = thief;
            merchant.Fire = fire;
            thief.Merchant = merchant;
            var hale = Townsfolk<VillageCaptain>("Hale", new AgentIdentity
            {
                Name = "Hale",
                Role = "the captain of the guard",
                Personality = "Gruff, fair and brief. Gives orders in one short sentence.",
                Goals = "Keep the village safe through his guards, see the wolf dealt with, and reward those who help."
            }, "Captain Hale, before the lord's keep", new Vector3(18, 0, 30.4f), 180f, "char_hale", provider,
                new[] { "place", "person", "restricted" });

            // Things to find: moonpetals in the woods, Wren's lantern at the docks, and the pelt the
            // wolf leaves when a guard drives it off during the wolf quest.
            var finds = new GameObject("Pickups").transform;
            foreach (var p in new[] { new Vector3(33, 0, 36.5f), new Vector3(41.5f, 0, 42), new Vector3(-9, 0, 31.5f), new Vector3(-27, 0, 29.5f) })
                Pickup(finds, "moonpetal", p);
            Pickup(finds, "old_lantern", P("docks") + new Vector3(3.2f, 0, -0.6f));
            var pelt = Pickup(finds, "pelt", Vector3.zero);
            pelt.gameObject.SetActive(false);

            var camera = CreateCamera();
            camera.orthographic = false;
            camera.fieldOfView = 50;
            camera.farClipPlane = 250;
            var rig = camera.gameObject.AddComponent<DemoCameraRig>();
            rig.Player = player.transform;
            camera.transform.position = new Vector3(0, 14.8f, -29.6f);
            camera.transform.LookAt(new Vector3(0, 1, -18));
            var sun = CreateLight();
            var day = Dress(camera, sun, alarm);
            // Only Wren fetches water, so only she has a bucket.
            DressSituations(villageGate, fire, new[] { (VillageGuard)wren.Body });

            // The night watch is Wren's alone, so only she is ever offered the gate (NightWatch).
            var watch = wren.gameObject.AddComponent<NightWatch>();
            watch.Guard = (VillageGuard)wren.Body;
            watch.Mind = wren;
            watch.Gate = villageGate;
            watch.Day = day;
            watch.WatchPost = watchPost;
            watch.Relieved = (VillageGuard)bram.Body;
            watch.RelievedPost = offWatch;
            // So are chase and fetch_water: Bram, the evaluation agent, keeps the actions he had.
            wren.gameObject.AddComponent<Responder>().Guard = (VillageGuard)wren.Body;

            var boardObject = new GameObject("AgentBoard");
            var board = boardObject.AddComponent<AgentBoard>();

            var hud = new GameObject("HUD").AddComponent<VillageHud>();
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
            commands.Board = board;
            commands.Hud = hud;
            commands.Day = day;
            routine.Day = day;
            commands.Routine = routine;
            var weather = DressWeather(camera, day);
            commands.Weather = weather;
            foreach (var mind in new[] { bram, wren }) mind.Senses.Weather = weather;
            commands.Player = player.transform;
            commands.Minds = new[] { bram, wren, odile, hale };
            commands.Inventory = inventory;
            var quests = ui.AddComponent<QuestBook>();
            quests.Inventory = inventory;
            commands.Quests = quests;
            commands.Book = new GameObject("Book").AddComponent<VillageBook>();
            wolf.Pelt = pelt;
            wolf.Quests = quests;
            foreach (var mind in commands.Minds) mind.Village = commands;
            var events = ui.AddComponent<VillageEvents>();
            events.Village = commands;
            events.Alarm = alarm;
            thief.Village = commands;
            fire.Village = commands;
            fire.Events = events;
            ui.AddComponent<DecisionHistoryPanel>().Commands = commands;
            // Its own GameObject: DemoUI.Create owns one UIDocument per GameObject, and clears it.
            var menu = new GameObject("Menu").AddComponent<DemoMenu>();
            commands.Menu = menu;
            menu.Input = input;
            menu.Board = board;
            menu.Day = day;
            menu.Book = commands.Book;

            // The presenter's keys, F1 to F5, and their panel (F12).
            var presenter = new GameObject("Presenter").AddComponent<VillagePresenter>();
            presenter.Village = commands;
            presenter.Wolf = wolf;
            presenter.Thief = thief;
            presenter.Fire = fire;
            presenter.Watch = watch;
            presenter.Alarm = alarm;
            presenter.Weather = weather;
            presenter.Routine = routine;
            // Up the road outside the gate, within the gate guard's sight and the bell's 15 m, and
            // clear of the player's line from the start.
            presenter.WolfFrom = Point(geometry, "wolf_from", 6f, -24.5f);
            presenter.WolfTo = Point(geometry, "wolf_to", 3f, -19.5f);
            commands.Presenter = presenter;
            ui.SetActive(true);
            thiefObject.SetActive(false);
        }

        // A guard: its body, the AgentBehaviour that builds its agent from its profile, its mind and
        // its senses, and a Targetable so the other agents can name it.
        private static AgentMind Guard(string name, AgentIdentity identity, string description, Vector3 position,
            Material material, Transform post, Transform[] route, float quietSeconds, ProviderConfig provider,
            VillageAlarm alarm, VillageWolf wolf, Transform player)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var nav = go.AddComponent<NavMeshAgent>();
            nav.radius = 0.4f;
            nav.height = 1.8f;
            nav.speed = 3.5f;
            nav.stoppingDistance = 0.8f;
            var guard = go.AddComponent<VillageGuard>();
            guard.Post = post;
            guard.PatrolRoute = route;
            guard.Alarm = alarm;
            Figure(go, "char_" + name.ToLowerInvariant());
            Tag(go, name, identity.Role.Replace("the ", ""));
            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0, 0.9f, 0);
            collider.height = 1.8f;
            collider.radius = 0.4f;
            AddTargetable(go, name.ToLowerInvariant(), description, "person");

            var behaviour = go.AddComponent<AgentBehaviour>();
            behaviour.Provider = provider;
            behaviour.FindTargetsNearby = true;
            // Every place in the village, the evaluation set's included, fits under the cap.
            behaviour.Targets = new ProximityTargetSource { Radius = 90f, Cap = 24, Categories = GuardCategories };
            // The game writes its own observations, ids and distances included, which already pass
            // the agent's eight-line budget, so a proximity source would add nothing (README).
            behaviour.NoticeNearby = false;
            behaviour.Profile = Profile(name, identity);

            var mind = go.AddComponent<AgentMind>();
            mind.Body = guard;
            var senses = go.AddComponent<GuardSenses>();
            senses.Mind = mind;
            senses.Guard = guard;
            senses.Player = player;
            senses.Wolf = wolf;
            senses.QuietSeconds = quietSeconds;
            mind.Senses = senses;
            return mind;
        }

        // "stranger" is the thief, offered only while it is out.
        private static readonly string[] GuardCategories = { "place", "person", "creature", "restricted", "stranger" };

        // An agent who is not a guard: the merchant and the captain. Same AgentBehaviour and shared
        // provider config; a body of its own; no guard senses.
        private static AgentMind Townsfolk<T>(string name, AgentIdentity identity, string description, Vector3 position,
            float yaw, string model, ProviderConfig provider, string[] categories) where T : VillageBody
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var nav = go.AddComponent<NavMeshAgent>();
            nav.radius = 0.4f;
            nav.height = 1.8f;
            nav.speed = 3f;
            var body = go.AddComponent<T>();
            Figure(go, model);
            Tag(go, name, identity.Role.Replace("the ", ""));
            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0, 0.9f, 0);
            collider.height = 1.8f;
            collider.radius = 0.4f;
            AddTargetable(go, name.ToLowerInvariant(), description, "person");
            var behaviour = go.AddComponent<AgentBehaviour>();
            behaviour.Provider = provider;
            behaviour.FindTargetsNearby = true;
            behaviour.Targets = new ProximityTargetSource { Radius = 90f, Cap = 24, Categories = categories };
            behaviour.NoticeNearby = false;
            behaviour.Profile = Profile(name, identity);
            var mind = go.AddComponent<AgentMind>();
            mind.Body = body;
            return mind;
        }

        private static Pickup Pickup(Transform parent, string item, Vector3 position)
        {
            var go = new GameObject("pickup_" + item);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var pickup = go.AddComponent<Pickup>();
            pickup.ItemId = item;
            pickup.Visual = Model("item_" + (item == "old_lantern" ? "lantern" : item), go.transform, Vector3.zero, isStatic: false);
            if (item == "moonpetal")
            {
                var glow = new GameObject("glow").AddComponent<Light>();
                glow.transform.SetParent(go.transform, false);
                glow.transform.localPosition = Vector3.up * 0.6f;
                glow.type = LightType.Point;
                glow.color = new Color(0.6f, 0.95f, 1f);
                glow.range = 3f;
                glow.intensity = 1.2f;
            }
            return pickup;
        }

        // Kept between rebuilds, so a reference to the asset never breaks and a local change survives.
        private static AgentProfile Profile(string name, AgentIdentity identity)
        {
            EnsureFolder(AgentsFolder);
            string path = AgentsFolder + "/" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<AgentProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<AgentProfile>();
            profile.Identity = identity;
            profile.IdleExampleStimulus = "Fine day for it.";
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static ProviderConfig ProviderConfig()
        {
            EnsureFolder(AgentsFolder);
            var config = AssetDatabase.LoadAssetAtPath<OllamaProviderConfig>(ProviderPath);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<OllamaProviderConfig>();
            config.Endpoint = "http://127.0.0.1:11434";
            config.Model = "qwen3.5:2b";
            AssetDatabase.CreateAsset(config, ProviderPath);
            return config;
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

        // A place of the villagers' routine and its points. Not a target: only where they stand.
        private static VillageRoutine.Spot Spot(Transform parent, string id, string words, params (float x, float z)[] points) =>
            new VillageRoutine.Spot
            {
                Id = id,
                Words = words,
                Points = points.Select((p, i) => Point(parent, "routine_" + id + "_" + i, p.x, p.z)).ToArray()
            };

        // A straight wall from a to b along one axis, at the given position on the other.
        private static void Wall(string name, Transform parent, float a, float b, float at, float height, Material material, bool alongX)
        {
            walls.Add((name, a, b, at, height, alongX));
            float mid = (a + b) / 2, length = Mathf.Abs(b - a);
            var position = alongX ? new Vector3(mid, height / 2, at) : new Vector3(at, height / 2, mid);
            var scale = alongX ? new Vector3(length, height, 0.6f) : new Vector3(0.6f, height, length);
            Box(name, parent, position, scale, material);
        }

        private static void Tree(Transform parent, Vector3 position)
        {
            trees.Add(position);
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

        private static void Tag(GameObject go, string text, string detail, float height = 2.15f)
        {
            var tag = go.AddComponent<NameTag>();
            tag.Text = text;
            tag.Detail = detail;
            tag.Height = height;
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

        private static Light CreateLight()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            return light;
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
