using System;
using System.Collections.Generic;
using LivingVale.Game;
using UnityEditor;
using UnityEngine;

namespace LivingVale.Editor.Region
{
    /// <summary>
    /// The grey-box toolkit every place builder shares: primitives, materials, invisible walls and
    /// scattered trees, all parented under one root per place so the hierarchy stays readable. Heights
    /// follow one rule: the open ground's top is at <see cref="GroundTop"/>, paths sit just above it, and
    /// a place's yard is at y = 0, where NPCs stand.
    /// </summary>
    internal static class RegionKit
    {
        public const string MaterialFolder = "Assets/LivingVale/Materials";

        /// <summary>The top of the 600 × 600 ground, a little below the village's y = 0 so they never z-fight.</summary>
        public const float GroundTop = -0.05f;

        /// <summary>The top of a path strip: above the ground, below a yard.</summary>
        public const float PathTop = -0.02f;

        public const float PathWidth = 4f;

        /// <summary>Ignore Raycast: the camera's obstruction cast and the NPC sight line skip it, the player still collides.</summary>
        public const int BlockerLayer = 2;

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        /// <summary>Forgets cached materials; call once at the start of a build.</summary>
        public static void Reset()
        {
            Cache.Clear();
        }

        /// <summary>An empty parent for one place's objects.</summary>
        public static Transform Root(string name, Transform parent = null)
        {
            var root = new GameObject(name).transform;
            if (parent != null)
            {
                root.SetParent(parent, false);
            }

            return root;
        }

        /// <summary>A primitive in world space under <paramref name="parent"/>, static so Unity batches it.</summary>
        public static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material, float yaw = 0f, bool collider = true)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
            {
                UnityEngine.Object.DestroyImmediate(primitive.GetComponent<Collider>());
            }

            GameObjectUtility.SetStaticEditorFlags(primitive, StaticEditorFlags.BatchingStatic);
            return primitive;
        }

        /// <summary>A box resting on y = <paramref name="baseY"/>: give its footprint and height, not its centre.</summary>
        public static GameObject Block(Transform parent, string name, float x, float z, float width, float depth,
            float height, Material material, float yaw = 0f, float baseY = 0f)
        {
            return Primitive(parent, name, PrimitiveType.Cube, new Vector3(x, baseY + height * 0.5f, z),
                new Vector3(width, height, depth), material, yaw);
        }

        /// <summary>A flat patch whose top is at y = 0: a yard, a field, a clearing floor.</summary>
        public static GameObject Yard(Transform parent, string name, float x, float z, float width, float depth,
            Material material, float yaw = 0f)
        {
            return Primitive(parent, name, PrimitiveType.Cube, new Vector3(x, -0.25f, z),
                new Vector3(width, 0.5f, depth), material, yaw);
        }

        /// <summary>An invisible wall the player cannot pass and the camera ignores (layer 2).</summary>
        public static GameObject Blocker(Transform parent, string name, Vector3 centre, Vector3 size, float yaw = 0f)
        {
            var blocker = new GameObject(name);
            blocker.layer = BlockerLayer;
            blocker.transform.SetParent(parent, false);
            blocker.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
            blocker.AddComponent<BoxCollider>().size = size;
            GameObjectUtility.SetStaticEditorFlags(blocker, StaticEditorFlags.BatchingStatic);
            return blocker;
        }

        /// <summary>A grey-box tree: a trunk with a collider and a crown without one.</summary>
        public static void Tree(Transform parent, string name, float x, float z, float height, Material trunk, Material crown)
        {
            GameObject stem = Primitive(parent, $"{name} trunk", PrimitiveType.Cylinder, new Vector3(x, height * 0.3f, z),
                new Vector3(0.5f, height * 0.3f, 0.5f), trunk);
            // G7b: a kit tree of about the same height; the trunk keeps its collider, the crown never had one.
            // The species and turn come from the position, so a rebuild plants the same tree.
            int hash = Mathf.Abs(Mathf.RoundToInt(x * 7.3f) * 31 + Mathf.RoundToInt(z * 3.7f));
            bool pine = hash % 3 != 0;
            if (ArtDressing.Dress(stem, pine ? "tree_pine" : "tree_broadleaf", new Vector3(x, 0f, z), hash % 360,
                    height / (pine ? 7f : 6.2f)) != null)
            {
                return;
            }

            Primitive(parent, $"{name} crown", PrimitiveType.Sphere, new Vector3(x, height * 0.75f, z),
                new Vector3(height * 0.45f, height * 0.55f, height * 0.45f), crown, collider: false);
        }

        /// <summary>
        /// Trees scattered in a ring around (x, z), fixed by <paramref name="seed"/> so a rebuild puts them
        /// in the same places. Points on or near a path, or inside <paramref name="inner"/>, are skipped.
        /// </summary>
        public static int TreeRing(Transform parent, string name, float x, float z, float inner, float outer, int count,
            int seed, Material trunk, Material crown)
        {
            var random = new System.Random(seed);
            int planted = 0;
            for (int i = 0; i < count; i++)
            {
                double angle = random.NextDouble() * Math.PI * 2.0;
                double radius = inner + random.NextDouble() * (outer - inner);
                float tx = x + (float)(Math.Cos(angle) * radius);
                float tz = z + (float)(Math.Sin(angle) * radius);
                float height = 6f + (float)random.NextDouble() * 4f;
                if (NearPath(tx, tz, PathWidth * 0.5f + 2.5f) || !InsideBoundary(tx, tz, 6f))
                {
                    continue;
                }

                Tree(parent, $"{name} {i}", tx, tz, height, trunk, crown);
                planted++;
            }

            return planted;
        }

        /// <summary>Whether (x, z) is within <paramref name="margin"/> of any path's centre line.</summary>
        public static bool NearPath(float x, float z, float margin)
        {
            foreach (var (from, to) in ValePlaces.Paths)
            {
                ValePlace a = ValePlaces.Get(from), b = ValePlaces.Get(to);
                if (DistanceToSegment(x, z, a.X, a.Z, b.X, b.Z) <= margin)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool InsideBoundary(float x, float z, float margin)
        {
            float limit = ValePlaces.GroundHalfExtent - margin;
            return Mathf.Abs(x) <= limit && Mathf.Abs(z) <= limit;
        }

        public static float DistanceToSegment(float px, float pz, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            float lengthSquared = dx * dx + dz * dz;
            float t = lengthSquared <= 0f ? 0f : Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / lengthSquared);
            float cx = ax + t * dx - px, cz = az + t * dz - pz;
            return Mathf.Sqrt(cx * cx + cz * cz);
        }

        /// <summary>A URP Lit material asset, created once and recoloured on every build.</summary>
        public static Material Material(string name, Color color)
        {
            if (Cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

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
            Cache[name] = material;
            return material;
        }

        // The region's shared palette, so every place reads as one valley.
        public static Material Grass => Material("RegionGrass", new Color(0.36f, 0.47f, 0.29f));
        public static Material Dirt => Material("RegionDirt", new Color(0.52f, 0.43f, 0.31f));
        public static Material Rock => Material("RegionRock", new Color(0.42f, 0.42f, 0.44f));
        public static Material Stone => Material("Stone", new Color(0.46f, 0.49f, 0.46f));
        public static Material Timber => Material("Timber", new Color(0.43f, 0.28f, 0.17f));
        public static Material Canvas => Material("RegionCanvas", new Color(0.72f, 0.66f, 0.52f));
        public static Material Water => Material("RegionWater", new Color(0.18f, 0.36f, 0.55f));
        public static Material Bark => Material("RegionBark", new Color(0.33f, 0.24f, 0.16f));
        public static Material Leaves => Material("RegionLeaves", new Color(0.2f, 0.36f, 0.2f));
        public static Material Crop => Material("RegionCrop", new Color(0.62f, 0.6f, 0.24f));
        public static Material Mountain => Material("RegionMountain", new Color(0.5f, 0.54f, 0.6f));
        public static Material Ember => Material("RegionEmber", new Color(0.9f, 0.45f, 0.15f));
    }
}
