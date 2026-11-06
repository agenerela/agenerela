using System;
using LivingVale.Game;
using LivingVale.UI;
using LivingVale.Unity;
using UnityEditor;
using UnityEngine;

namespace LivingVale.Editor
{
    /// <summary>Builds the quest landmarks, pickups, camp defenders, visible endings and route HUDs.</summary>
    public static class QuestSceneBuilder
    {
        private const string MaterialFolder = "Assets/LivingVale/Materials";

        public static void Build(ValeBootstrap bootstrap, GameObject player)
        {
            // The village's and combat's shared materials are loaded as they are, never recoloured:
            // other builders and the art session own them. Only quest-specific ones are made here.
            Material timber = Load("Timber");
            Material stone = Load("Stone");
            Material steel = Load("Steel");
            Material herbs = MaterialFor("QuestHerbs", new Color(0.22f, 0.55f, 0.18f));
            Material cloth = MaterialFor("QuestPatientCloth", new Color(0.85f, 0.83f, 0.7f));
            Material red = Load("RaiderCloth");

            BuildWreck(bootstrap, timber, steel);
            BuildHerbs(bootstrap, herbs, timber);
            GameObject[] defenders = BuildCamp(bootstrap, timber, stone, red);
            GameObject[] patients = BuildPatients(cloth, timber);
            Vector3 gate = Centre(ValePlaces.Ashwatch) + new Vector3(0f, 0f, -12f);
            GameObject[] bars = BuildGate(gate, steel);
            var crates = new GameObject[2];
            for (int i = 0; i < crates.Length; i++)
            {
                crates[i] = Primitive($"Quest division supplies {i + 1}", PrimitiveType.Cube,
                    gate + new Vector3(3.2f, 0.45f, i * 1.2f), new Vector3(0.9f, 0.9f, 0.9f), timber);
                crates[i].SetActive(false);
            }

            new GameObject("Quest world view").AddComponent<QuestWorldView>()
                .Configure(bootstrap, patients, bars, crates, defenders);
            ChatOverlay overlay = UnityEngine.Object.FindFirstObjectByType<ChatOverlay>();
            player.AddComponent<QuestWorldInteraction>()
                .Configure(bootstrap, overlay, player.GetComponent<NpcInteraction>());
            new GameObject("Quest route card", typeof(RectTransform)).AddComponent<QuestRouteCard>()
                .Configure(bootstrap);
            new GameObject("Quest journal HUD", typeof(RectTransform)).AddComponent<QuestJournalHud>()
                .Configure(bootstrap);
        }

        private static void BuildWreck(ValeBootstrap bootstrap, Material timber, Material steel)
        {
            Vector3 road = Centre(ValePlaces.OldRoad);
            Transform wreck = Pickup("Quest wagon wreck", road, bootstrap, QuestRoutes.PickupWreck);
            GameObject bed = Piece("Quest tilted wagon bed", wreck, PrimitiveType.Cube,
                new Vector3(0f, 0.8f, 0f), new Vector3(3.2f, 0.4f, 2f), timber, keepCollider: true);
            bed.transform.localRotation = Quaternion.Euler(8f, 12f, 14f);
            GameObject wheel = Piece("Quest upright wagon wheel", wreck, PrimitiveType.Cylinder,
                new Vector3(-1f, 0.65f, -1.1f), new Vector3(1.25f, 0.12f, 1.25f), timber);
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Piece("Quest fallen wagon wheel", wreck, PrimitiveType.Cylinder,
                new Vector3(1.6f, 0.12f, 1.4f), new Vector3(1.25f, 0.1f, 1.25f), timber);

            Transform badge = Pickup("Quest dented badge", road + new Vector3(2.5f, 0f, 0f),
                bootstrap, QuestRoutes.PickupBadge);
            Piece("Quest steel badge", badge, PrimitiveType.Cylinder,
                new Vector3(0f, 0.05f, 0f), new Vector3(0.32f, 0.025f, 0.32f), steel);
        }

        private static void BuildHerbs(ValeBootstrap bootstrap, Material green, Material timber)
        {
            Vector3 hollow = Centre(ValePlaces.HerbHollow);
            string[] ids = { QuestRoutes.PickupHerb1, QuestRoutes.PickupHerb2, QuestRoutes.PickupHerb3 };
            Vector3[] offsets =
            {
                new Vector3(-3f, 0f, -1.75f),
                new Vector3(3f, 0f, -1.75f),
                new Vector3(0f, 0f, 3.5f)
            };
            for (int i = 0; i < ids.Length; i++)
            {
                Transform spot = Pickup($"Quest herb spot {i + 1}", hollow + offsets[i], bootstrap, ids[i]);
                for (int leaf = 0; leaf < 3; leaf++)
                {
                    float angle = leaf * Mathf.PI * 2f / 3f;
                    Piece($"Quest herb cluster {leaf + 1}", spot, PrimitiveType.Capsule,
                        new Vector3(Mathf.Cos(angle) * 0.23f, 0.27f, Mathf.Sin(angle) * 0.23f),
                        new Vector3(0.32f, 0.27f, 0.32f), green);
                }

                Piece("Quest herb marker post", spot, PrimitiveType.Cube,
                    new Vector3(0f, 0.5f, 0.65f), new Vector3(0.12f, 1f, 0.12f), timber);
            }
        }

        private static GameObject[] BuildCamp(ValeBootstrap bootstrap, Material timber, Material stone, Material red)
        {
            Vector3 watch = Centre(ValePlaces.BrokenWatch);
            Transform parcel = Pickup("Quest medical parcel", watch, bootstrap, QuestRoutes.PickupParcel);
            Piece("Quest parcel crate", parcel, PrimitiveType.Cube,
                new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 0.7f), timber);
            Piece("Quest parcel cross across", parcel, PrimitiveType.Cube,
                new Vector3(0f, 0.815f, 0f), new Vector3(0.65f, 0.025f, 0.15f), red);
            Piece("Quest parcel cross along", parcel, PrimitiveType.Cube,
                new Vector3(0f, 0.815f, 0f), new Vector3(0.15f, 0.025f, 0.5f), red);

            Primitive("Quest Broken Watch west ruin", PrimitiveType.Cube, watch + new Vector3(-8f, 1f, 1f),
                new Vector3(0.8f, 2f, 5f), stone, keepCollider: true);
            Primitive("Quest Broken Watch north ruin", PrimitiveType.Cube, watch + new Vector3(1f, 0.8f, 8f),
                new Vector3(5f, 1.6f, 0.8f), stone, keepCollider: true);
            GameObject fallen = Primitive("Quest Broken Watch fallen wall", PrimitiveType.Cube,
                watch + new Vector3(7f, 0.4f, -3f), new Vector3(2.5f, 0.8f, 1.2f), stone, keepCollider: true);
            fallen.transform.rotation = Quaternion.Euler(0f, 25f, 8f);

            Vector3[] offsets =
            {
                new Vector3(-5f, 0f, 0f),
                new Vector3(3f, 0f, 4f),
                new Vector3(3f, 0f, -4f)
            };
            var defenders = new GameObject[QuestRoutes.DefenderIds.Count];
            for (int i = 0; i < defenders.Length; i++)
            {
                defenders[i] = CombatSceneBuilder.SpawnRaider(QuestRoutes.DefenderIds[i], watch + offsets[i]);
                defenders[i].name = $"Quest camp defender {i + 1}";
            }

            return defenders;
        }

        private static GameObject[] BuildPatients(Material cloth, Material timber)
        {
            var patients = new GameObject[3];
            for (int i = 0; i < patients.Length; i++)
            {
                Vector3 feet = new Vector3(4f + i, 0f, 5f);
                Primitive($"Quest patient bench {i + 1}", PrimitiveType.Cube, feet + new Vector3(0f, 0.45f, 0f),
                    new Vector3(0.85f, 0.15f, 0.7f), timber);
                for (int leg = 0; leg < 2; leg++)
                {
                    Primitive($"Quest bench {i + 1} leg {leg + 1}", PrimitiveType.Cube,
                        feet + new Vector3(-0.3f + leg * 0.6f, 0.2f, 0f), new Vector3(0.12f, 0.4f, 0.5f), timber);
                }

                patients[i] = Primitive($"Quest porch patient {i + 1}", PrimitiveType.Capsule,
                    feet + new Vector3(0f, 1.05f, 0f), new Vector3(0.5f, 0.55f, 0.5f), cloth);
            }

            return patients;
        }

        private static GameObject[] BuildGate(Vector3 centre, Material steel)
        {
            var bars = new GameObject[9];
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = Primitive($"Quest gate bar {i + 1}", PrimitiveType.Cube,
                    centre + new Vector3(-1.9f + i * 0.475f, 1.6f, 0f),
                    new Vector3(0.2f, 3.2f, 0.25f), steel, keepCollider: true);
            }

            return bars;
        }

        private static Vector3 Centre(string placeId)
        {
            ValePlace place = ValePlaces.Get(placeId);
            return new Vector3(place.X, 0f, place.Z);
        }

        private static Transform Pickup(string name, Vector3 feet, ValeBootstrap bootstrap, string id)
        {
            var root = new GameObject(name);
            root.transform.position = feet;
            var visual = new GameObject($"{name} visual");
            visual.transform.SetParent(root.transform, false);
            root.AddComponent<QuestPickupPoint>().Configure(bootstrap, id, visual);
            return visual.transform;
        }

        private static GameObject Piece(string name, Transform parent, PrimitiveType type, Vector3 offset,
            Vector3 scale, Material material, bool keepCollider = false)
        {
            GameObject piece = Primitive(name, type, parent.position + offset, scale, material, keepCollider);
            piece.transform.SetParent(parent, true);
            return piece;
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
                throw new InvalidOperationException($"{name}.mat is missing; build the village and combat first.");
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
