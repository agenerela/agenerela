using System;
using System.IO;
using LivingVale.UI;
using LivingVale.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivingVale.Editor
{
    public static class VillageSceneBuilder
    {
        public const string ScenePath = "Assets/LivingVale/Scenes/Village.unity";
        private const string MaterialFolder = "Assets/LivingVale/Materials";
        private const string TemplateScene = "Assets/Scenes/SampleScene.unity";
        private const string BuilderScript = "Assets/LivingVale/Editor/VillageSceneBuilder.cs";

        [MenuItem("Living Vale/Build Village Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder("Assets/LivingVale/Scenes");
            EnsureFolder(MaterialFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material stone = MaterialFor("Stone", new Color(0.46f, 0.49f, 0.46f));
            Material timber = MaterialFor("Timber", new Color(0.43f, 0.28f, 0.17f));
            Material blue = MaterialFor("ValeBlue", new Color(0.2f, 0.48f, 0.7f));

            // G7c: packed earth, not grey stone, so the Glen reads as a village yard on the grass; the
            // square around the well and the stall is cobbled (looks only, 8 cm high, no colliders).
            Material earth = MaterialFor("GlenEarth", new Color(0.47f, 0.4f, 0.3f));
            Primitive("Village ground", PrimitiveType.Cube, new Vector3(0f, -0.5f, 0f),
                new Vector3(40f, 1f, 36f), earth);
            for (int tile = 0; tile < 4; tile++)
            {
                ArtDressing.Decorate(null, "cobbles", new Vector3(tile % 2 == 0 ? -5f : 5f, 0f, tile < 2 ? -1f : 9f),
                    tile * 90f);
            }
            House("West cottage", new Vector3(-10f, 0f, 6f), stone, timber);
            House("North cottage", new Vector3(0f, 0f, 12f), stone, timber);
            House("East cottage", new Vector3(11f, 0f, 8f), stone, timber);

            // G7b: each group of primitives keeps its colliders and shows a kit model instead (ArtDressing).
            var stall = new GameObject[4];
            stall[0] = Primitive("Stall counter", PrimitiveType.Cube, new Vector3(-5f, 0.55f, 3f),
                new Vector3(3.6f, 1.1f, 0.8f), timber);
            stall[1] = Primitive("Stall canopy", PrimitiveType.Cube, new Vector3(-5f, 2.8f, 3.5f),
                new Vector3(4f, 0.2f, 2.2f), blue);
            for (int i = 0; i < 2; i++)
            {
                stall[2 + i] = Primitive($"Stall post {i}", PrimitiveType.Cube, new Vector3(-6.7f + i * 3.4f, 1.4f, 4f),
                    new Vector3(0.15f, 2.8f, 0.15f), timber);
            }

            // The stall's front faces the square (south); Mara stands behind it.
            ArtDressing.DressAll(stall, "market_stall", new Vector3(-5f, 0f, 3f), 180f, Vector3.one);

            GameObject wellBase = Primitive("Well base", PrimitiveType.Cylinder, new Vector3(0f, 0.35f, 4f),
                new Vector3(1.6f, 0.35f, 1.6f), stone);
            GameObject wellWater = Primitive("Well water", PrimitiveType.Cylinder, new Vector3(0f, 0.71f, 4f),
                new Vector3(1.2f, 0.02f, 1.2f), blue);
            ArtDressing.DressAll(new[] { wellBase, wellWater }, "well", new Vector3(0f, 0f, 4f), 0f, Vector3.one);
            for (int i = 0; i < 3; i++)
            {
                Primitive($"Uneven paving {i}", PrimitiveType.Cube, new Vector3(2f + i * 1.5f, 0.06f + i * 0.06f, -2f),
                    new Vector3(1.4f, 0.12f + i * 0.12f, 2f), stone);
            }

            GameObject rail = Primitive("Fence rail", PrimitiveType.Cube, new Vector3(7f, 1f, -5f),
                new Vector3(5f, 0.2f, 0.2f), timber);
            for (int i = 0; i < 3; i++)
            {
                GameObject post = Primitive($"Fence post {i}", PrimitiveType.Cube, new Vector3(4.5f + i * 2.5f, 0.7f, -5f),
                    new Vector3(0.2f, 1.4f, 0.2f), timber);
                // A fence_segment is 2.5 m of rail with its post at its -X end; a fence_post closes the run.
                bool last = i == 2;
                if (ArtDressing.Dress(post, last ? "fence_post" : "fence_segment",
                        new Vector3(last ? 9.5f : 5.75f + i * 2.5f, 0f, -5f)) != null && i == 0)
                {
                    ArtDressing.HideLook(rail);
                }
            }

            // Market clutter by the stall: looks only, no colliders, off the player's way to Mara.
            ArtDressing.Decorate(null, "crate", new Vector3(-7.7f, 0f, 2.3f), 12f);
            ArtDressing.Decorate(null, "barrel", new Vector3(-7.8f, 0f, 3.5f));
            ArtDressing.Decorate(null, "sack", new Vector3(-2.5f, 0f, 2.6f), 40f);
            ArtDressing.Decorate(null, "barrel", new Vector3(13.2f, 0f, 5.2f), 20f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.72f);

            var bootstrap = new GameObject("Living Vale bootstrap").AddComponent<ValeBootstrap>();
            var overlay = new GameObject("Chat overlay", typeof(RectTransform)).AddComponent<ChatOverlay>();
            var playerObject = new GameObject("Player");
            playerObject.layer = 2; // Ignore Raycast keeps the player's body out of camera and sight queries.
            playerObject.transform.position = new Vector3(-5f, 0.1f, -2f);
            var controller = playerObject.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = Vector3.up;
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 50f;
            var body = Primitive("Player body", PrimitiveType.Capsule, Vector3.up,
                new Vector3(0.65f, 1f, 0.65f), blue);
            body.transform.SetParent(playerObject.transform, false);
            body.layer = 2;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            // G7b: the player's look is the kit humanoid; combat's flash and stagger act on it.
            GameObject playerLook = ArtKit.SwapBody(body, ArtOutfit.Player) ?? body;
            var player = playerObject.AddComponent<ThirdPersonPlayer>();

            var cameraObject = new GameObject("Orbit camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f; // G4b: the region's far side and the mountains beyond it.
            camera.fieldOfView = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.38f, 0.5f, 0.6f);
            cameraObject.transform.SetPositionAndRotation(new Vector3(-5f, 3.1f, -6.2f), Quaternion.Euler(20f, 0f, 0f));
            cameraObject.AddComponent<OrbitCamera>().Configure(bootstrap, playerObject.transform);
            player.Configure(bootstrap, cameraObject.transform);
            playerObject.AddComponent<NpcInteraction>().Configure(bootstrap, overlay);
            bootstrap.Configure(player, overlay);
            CombatSceneBuilder.Build(bootstrap, playerObject, playerLook); // G2: player combat, the meadow encounter, HUD.
            QuestSceneBuilder.Build(bootstrap, playerObject); // G4c: quest pickups, camp defenders, endings, route card and journal.

            Speaker("npc_mara", "Mara", "General trader", new Vector3(-5f, 0f, 4.5f), timber);
            // Between 6 and 9 m apart permits an explicit switch before leaving the old speaker's range.
            Speaker("npc_iven", "Iven", "Healer", new Vector3(3f, 0f, 3f), blue);
            Region.RegionBuilder.Build(bootstrap); // G4b: the valley, its five other places and six NPCs.
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Could not save {ScenePath}.");
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            RemoveUnusedTemplateScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"[LivingVale] Built {ScenePath}. Mara and Iven are more than 6 m apart.");
        }

        private static void House(string name, Vector3 feet, Material stone, Material timber)
        {
            var parts = new[]
            {
                Primitive(name, PrimitiveType.Cube, feet + Vector3.up * 1.8f, new Vector3(5f, 3.6f, 4f), stone),
                Primitive($"{name} roof", PrimitiveType.Cube, feet + Vector3.up * 3.8f, new Vector3(5.6f, 0.4f, 4.6f), timber),
                Primitive($"{name} door", PrimitiveType.Cube, feet + new Vector3(0f, 1.1f, -2.05f),
                    new Vector3(1.1f, 2.2f, 0.12f), timber)
            };
            // cottage_a's walls are 5 x 4 like the block; its door faces south like the primitive's.
            ArtDressing.DressAll(parts, "cottage_a", feet, 180f, Vector3.one);
        }

        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetPositionAndRotation(position, Quaternion.identity);
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            return primitive;
        }

        private static void Speaker(string id, string name, string role, Vector3 position, Material material)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            root.AddComponent<NpcSpeaker>().Configure(id, name, role);
            var capsule = Primitive($"{name} body", PrimitiveType.Capsule, Vector3.up,
                new Vector3(0.8f, 1f, 0.8f), material);
            capsule.transform.SetParent(root.transform, false);
            ArtKit.SwapBody(capsule, ArtDressing.OutfitFor(id)); // keeps the capsule's collider for sight lines
        }

        private static Material MaterialFor(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException("URP Lit shader was not found.");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void RemoveUnusedTemplateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplateScene) == null)
            {
                return;
            }

            // Ask Unity about imported dependencies, then check serialized GUID references too.
            // A scene reference can be stored in an asset Unity does not list as a dependency.
            string guid = AssetDatabase.AssetPathToGUID(TemplateScene);
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path == TemplateScene || path == BuilderScript ||
                    AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                {
                    if (dependency == TemplateScene)
                    {
                        Debug.Log($"[LivingVale] Kept {TemplateScene}, referenced by {path}.");
                        return;
                    }
                }

                // Streaming avoids loading large imported files into memory.
                using (var reader = new StreamReader(path))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains(guid) || line.Contains(TemplateScene))
                        {
                            Debug.Log($"[LivingVale] Kept {TemplateScene}, referenced by {path}.");
                            return;
                        }
                    }
                }
            }

            if (!AssetDatabase.DeleteAsset(TemplateScene))
            {
                throw new InvalidOperationException($"Could not delete unused {TemplateScene}.");
            }

            Debug.Log($"[LivingVale] Removed unused {TemplateScene} and its meta through AssetDatabase.");
        }
    }
}
