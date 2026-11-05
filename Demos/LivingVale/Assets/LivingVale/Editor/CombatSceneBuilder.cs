using System;
using LivingVale.Combat;
using LivingVale.Unity;
using UnityEditor;
using UnityEngine;

namespace LivingVale.Editor
{
    /// <summary>
    /// Adds G2's combat to the generated village: the player's combat parts, a meadow south of
    /// the village with one raider and one wolf, the director that links combat to chat, loot and
    /// respawn, and the HUD. <see cref="VillageSceneBuilder.Build"/> calls it in one line, so the
    /// village builder stays almost untouched for the branches merging into it.
    /// </summary>
    public static class CombatSceneBuilder
    {
        private const string MaterialFolder = "Assets/LivingVale/Materials";

        // Enemy and player bodies sit on Ignore Raycast, like the player, so the orbit camera's
        // obstruction cast and the NPC sight line never treat a fighter as a wall.
        private const int FighterLayer = 2;

        public static void Build(ValeBootstrap bootstrap, GameObject player, GameObject playerBody)
        {
            Material stone = Load("Stone");
            Material timber = Load("Timber");
            Material steel = MaterialFor("Steel", new Color(0.72f, 0.75f, 0.78f));
            Material raiderCloth = MaterialFor("RaiderCloth", new Color(0.55f, 0.16f, 0.13f));
            Material wolfFur = MaterialFor("WolfFur", new Color(0.36f, 0.34f, 0.31f));

            // The village ground ends at z = -18; the meadow continues south of it.
            Primitive("South meadow", PrimitiveType.Cube, new Vector3(0f, -0.5f, -30f),
                new Vector3(40f, 1f, 24f), stone, keepCollider: true);
            Primitive("Meadow marker", PrimitiveType.Cube, new Vector3(-3f, 0.9f, -19.5f),
                new Vector3(0.25f, 1.8f, 0.25f), timber, keepCollider: true);
            Primitive("Meadow rock", PrimitiveType.Cube, new Vector3(7f, 0.4f, -31f),
                new Vector3(2.2f, 0.8f, 1.6f), stone, keepCollider: true);

            AddPlayerCombat(bootstrap, player, playerBody, steel);
            Raider(new Vector3(-1f, 0f, -27f), raiderCloth, steel);
            Wolf(new Vector3(4f, 0f, -29f), wolfFur);

            var respawn = new GameObject("Respawn point").transform;
            respawn.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            var director = new GameObject("Combat director").AddComponent<CombatDirector>();
            director.Configure(bootstrap, player.GetComponent<CombatBody>(), respawn);
            new GameObject("Combat HUD", typeof(RectTransform)).AddComponent<CombatHud>().Configure(director);
        }

        private static void AddPlayerCombat(ValeBootstrap bootstrap, GameObject player, GameObject playerBody,
            Material steel)
        {
            player.AddComponent<CombatBody>().Configure("player", true, EnemyArchetype.Raider, playerBody.transform,
                new[] { playerBody.GetComponent<Renderer>() }, 2.4f);
            var pivot = new GameObject("Blade pivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0.38f, 1.2f, 0.15f);
            GameObject blade = Primitive("Blade", PrimitiveType.Cube, Vector3.zero, new Vector3(0.07f, 0.04f, 1f), steel);
            blade.transform.SetParent(pivot, false);
            blade.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            blade.layer = FighterLayer;
            player.AddComponent<PlayerCombat>().Configure(bootstrap, pivot);
        }

        private static void Raider(Vector3 feet, Material cloth, Material steel)
        {
            GameObject root = FighterRoot("Raider", feet, 1.9f, 0.4f);
            Transform visual = Visual(root);
            GameObject body = Primitive("Raider body", PrimitiveType.Capsule, new Vector3(0f, 0.95f, 0f),
                new Vector3(0.75f, 0.95f, 0.75f), cloth);
            body.transform.SetParent(visual, false);
            GameObject blade = Primitive("Raider blade", PrimitiveType.Cube, new Vector3(0.45f, 1.1f, 0.45f),
                new Vector3(0.07f, 0.04f, 0.9f), steel);
            blade.transform.SetParent(visual, false);
            Finish(root, visual, EnemyArchetype.Raider, "enemy_raider_1", 2.4f,
                body.GetComponent<Renderer>(), blade.GetComponent<Renderer>());
        }

        private static void Wolf(Vector3 feet, Material fur)
        {
            GameObject root = FighterRoot("Wolf", feet, 0.9f, 0.45f);
            Transform visual = Visual(root);
            GameObject body = Primitive("Wolf body", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f),
                new Vector3(0.55f, 0.5f, 1.25f), fur);
            body.transform.SetParent(visual, false);
            GameObject head = Primitive("Wolf head", PrimitiveType.Cube, new Vector3(0f, 0.7f, 0.78f),
                new Vector3(0.4f, 0.35f, 0.45f), fur);
            head.transform.SetParent(visual, false);
            Finish(root, visual, EnemyArchetype.Wolf, "enemy_wolf_1", 1.4f,
                body.GetComponent<Renderer>(), head.GetComponent<Renderer>());
        }

        private static GameObject FighterRoot(string name, Vector3 feet, float height, float radius)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(feet + Vector3.up * 0.05f, Quaternion.identity);
            var controller = root.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = radius;
            controller.center = Vector3.up * (height * 0.5f);
            controller.stepOffset = Mathf.Min(0.3f, height * 0.3f);
            return root;
        }

        private static Transform Visual(GameObject root)
        {
            var visual = new GameObject($"{root.name} visual").transform;
            visual.SetParent(root.transform, false);
            return visual;
        }

        private static void Finish(GameObject root, Transform visual, EnemyArchetype archetype, string id,
            float barHeight, params Renderer[] renderers)
        {
            root.AddComponent<CombatBody>().Configure(id, false, archetype, visual, renderers, barHeight);
            root.AddComponent<EnemyController>().Configure(archetype, visual);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = FighterLayer;
            }
        }

        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material, bool keepCollider = false)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetPositionAndRotation(position, Quaternion.identity);
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                UnityEngine.Object.DestroyImmediate(primitive.GetComponent<Collider>());
            }

            return primitive;
        }

        private static Material Load(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
            if (material == null)
            {
                throw new InvalidOperationException($"{name}.mat is missing; build the village first.");
            }

            return material;
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
            material.SetFloat("_Smoothness", name == "Steel" ? 0.6f : 0f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
