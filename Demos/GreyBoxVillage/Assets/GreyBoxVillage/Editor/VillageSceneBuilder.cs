using System;
using System.Collections.Generic;
using System.Linq;
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
        private static Material ground, stone, dark, blue, gold, water;

        [MenuItem("Tools/Demos/GreyBoxVillage/Create Initial Scenes")]
        public static void CreateInitialScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoLauncher.VillagePath) != null)
                throw new InvalidOperationException("The village already exists. Edit the scene directly and use Rebake Navigation.");
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && active.isDirty)
                throw new InvalidOperationException("Save the untitled scene before creating the village.");
            EnsureFolder(Root + "/Materials");
            ground = Material("Ground", new Color(0.43f, 0.46f, 0.43f));
            stone = Material("Stone", new Color(0.64f, 0.65f, 0.63f));
            dark = Material("DarkStone", new Color(0.27f, 0.30f, 0.32f));
            blue = Material("PlayerBlue", new Color(0.22f, 0.49f, 0.73f));
            gold = Material("GuardGold", new Color(0.75f, 0.58f, 0.25f));
            water = Material("River", new Color(0.24f, 0.38f, 0.43f));

            // Unity cannot add scenes alongside an untitled scene. Replace only a clean one.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                string.IsNullOrEmpty(active.path) ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var geometry = new GameObject("VillageGeometry").transform;
            Box("ground_south", geometry, new Vector3(0, -0.5f, -4.5f), new Vector3(36, 1, 21), ground);
            Box("ground_north", geometry, new Vector3(0, -0.5f, 15), new Vector3(36, 1, 10), ground);
            var river = Box("river_visual_only", geometry, new Vector3(0, -0.8f, 8), new Vector3(36, 0.1f, 4), water);
            UnityEngine.Object.DestroyImmediate(river.GetComponent<Collider>());
            // Invisible banks prevent walking into the decorative channel; bridge stays open.
            foreach (float z in new[] { 5.95f, 10.05f })
                foreach (float x in new[] { -10.5f, 10.5f })
                    Box("river_bank", geometry, new Vector3(x, 0.25f, z), new Vector3(15, 0.5f, 0.3f), dark);

            var gate = Landmark("gate", geometry, new Vector3(0, 0, -6));
            Box("left_pier", gate, new Vector3(-3.8f, 1.8f, 0), new Vector3(1.6f, 3.6f, 1.5f), stone);
            Box("right_pier", gate, new Vector3(3.8f, 1.8f, 0), new Vector3(1.6f, 3.6f, 1.5f), stone);
            Box("lintel", gate, new Vector3(0, 4, 0), new Vector3(9.2f, 0.8f, 1.5f), dark);
            foreach (float x in new[] { -11.3f, 11.3f })
                Box("town_wall", geometry, new Vector3(x, 1, -6), new Vector3(13.4f, 2, 0.8f), stone);
            Label("gate / open", gate, new Vector3(0, 5.1f, 0));

            var tower = Landmark("tower", geometry, new Vector3(-10, 0, 11));
            Box("tower_block", tower, new Vector3(0, 3.5f, 3), new Vector3(4, 7, 4), stone);
            Box("tower_cap", tower, new Vector3(0, 7.25f, 3), new Vector3(4.7f, 0.5f, 4.7f), dark);
            Box("tower_door", tower, new Vector3(0, 1, 0.96f), new Vector3(1.3f, 2, 0.08f), dark);
            Label("tower", tower, new Vector3(0, 8.5f, 3));

            var bridge = Landmark("bridge", geometry, new Vector3(0, 0, 8));
            Box("bridge_deck", bridge, new Vector3(0, -0.15f, 0), new Vector3(6, 0.3f, 5), stone);
            foreach (float x in new[] { -2.8f, 2.8f })
                Box("bridge_rail", bridge, new Vector3(x, 0.6f, 0), new Vector3(0.25f, 1.2f, 4), dark);
            Label("bridge", bridge, new Vector3(0, 2, 0));

            var dummy = Landmark("training_dummy", geometry, new Vector3(11, 0, 0));
            Box("dummy_post", dummy, new Vector3(0, 1.3f, 1.3f), new Vector3(0.4f, 2.6f, 0.4f), dark);
            Box("dummy_body", dummy, new Vector3(0, 1.8f, 1.3f), new Vector3(1.2f, 1, 0.6f), gold);
            Box("dummy_arms", dummy, new Vector3(0, 2.1f, 1.3f), new Vector3(2.2f, 0.3f, 0.3f), dark);
            Label("training_dummy", dummy, new Vector3(0, 3.7f, 1.3f));

            Box("house_block_A", geometry, new Vector3(-11, 1.7f, 0), new Vector3(5, 3.4f, 5), stone);
            Box("house_block_B", geometry, new Vector3(11, 1.7f, 15), new Vector3(5, 3.4f, 5), stone);
            // The four boundary walls stop the player walking off the ground.
            foreach (float x in new[] { -18f, 18f })
                Box("boundary", geometry, new Vector3(x, 0.7f, 2.5f), new Vector3(0.4f, 1.4f, 35), dark);
            foreach (float z in new[] { -15f, 20f })
                Box("boundary", geometry, new Vector3(0, 0.7f, z), new Vector3(36, 1.4f, 0.4f), dark);

            var navigation = new GameObject("BakedNavigation").AddComponent<VillageNavigation>();
            Bake(navigation, geometry);

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 0.05f, -10);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0, 0.9f, 0);
            controller.stepOffset = 0.25f;
            var movement = player.AddComponent<DemoPlayerController>();
            Capsule("player_body", player.transform, blue);
            Label("player", player.transform, new Vector3(0, 2.4f, 0));

            var guardObject = new GameObject("VillageGuard");
            guardObject.transform.position = new Vector3(-2, 0, -7.5f);
            var nav = guardObject.AddComponent<NavMeshAgent>();
            nav.radius = 0.4f;
            nav.height = 1.8f;
            nav.speed = 3.5f;
            nav.stoppingDistance = 0.8f;
            var guard = guardObject.AddComponent<VillageGuard>();
            Capsule("guard_body", guardObject.transform, gold);
            var guardCollider = guardObject.AddComponent<CapsuleCollider>();
            guardCollider.center = new Vector3(0, 0.9f, 0);
            guardCollider.height = 1.8f;
            guardCollider.radius = 0.4f;
            Label("guard", guardObject.transform, new Vector3(0, 2.4f, 0));

            var camera = CreateCamera();
            var rig = camera.gameObject.AddComponent<DemoCameraRig>();
            rig.Player = player.transform;
            camera.transform.position = new Vector3(0, 24, -26);
            camera.transform.LookAt(new Vector3(0, 0, -2));
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
            commands.Tower = tower;
            commands.Bridge = bridge;
            commands.TrainingDummy = dummy;
            ui.SetActive(true);
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
            Selection.activeGameObject = geometry.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 0, 3), Quaternion.Euler(50, 0, 0), 32);
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
                new Bounds(new Vector3(0, 3, 2.5f), new Vector3(40, 14, 39)), Vector3.zero, Quaternion.identity);
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

        private static void Capsule(string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0.9f, 0);
            go.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
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
