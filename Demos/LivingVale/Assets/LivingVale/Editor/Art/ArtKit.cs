using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LivingVale.Editor
{
    /// <summary>Groups the models into preview rows.</summary>
    public enum ArtKitCategory
    {
        /// <summary>Village structures.</summary>
        Building,
        /// <summary>Small environmental objects.</summary>
        Prop,
        /// <summary>Intact and wrecked wagons.</summary>
        Wagon,
        /// <summary>Vegetation and rocks.</summary>
        Nature,
        /// <summary>Defensive walls and gates.</summary>
        Fortification,
        /// <summary>Campsite structures and objects.</summary>
        Camp,
        /// <summary>Humanoid bodies.</summary>
        Character,
        /// <summary>Handheld equipment.</summary>
        Gear,
        /// <summary>Non-humanoid creatures.</summary>
        Creature,
        /// <summary>Ground cover, cliffs and distant mountains (G7c).</summary>
        Landscape
    }

    /// <summary>Selects a palette-painted humanoid outfit.</summary>
    public enum ArtOutfit
    {
        /// <summary>The player's outfit.</summary>
        Player,
        /// <summary>A village resident's outfit.</summary>
        Villager,
        /// <summary>The healer's outfit.</summary>
        Healer,
        /// <summary>A raider's outfit.</summary>
        Raider,
        /// <summary>An Ashwatch guard's outfit.</summary>
        Guard
    }

    /// <summary>Describes an exported model and its expected import geometry.</summary>
    public readonly struct ArtKitEntry
    {
        /// <summary>The model's filename without its extension.</summary>
        public string Id { get; }
        /// <summary>The preview row containing this model.</summary>
        public ArtKitCategory Category { get; }
        /// <summary>Whether the importer should generate mesh colliders.</summary>
        public bool Collidable { get; }
        /// <summary>Whether the origin is at the base centre.</summary>
        public bool PivotAtBase { get; }
        /// <summary>The approximate height in metres, or longest axis for the sword.</summary>
        public float Height { get; }

        /// <summary>Creates a catalog entry.</summary>
        public ArtKitEntry(string id, ArtKitCategory category, bool collidable, bool pivotAtBase, float height)
        {
            Id = id;
            Category = category;
            Collidable = collidable;
            PivotAtBase = pivotAtBase;
            Height = height;
        }
    }

    /// <summary>Loads and places the Living Vale art kit while building scenes in the Editor.</summary>
    public static class ArtKit
    {
        /// <summary>The imported FBX model folder.</summary>
        public const string ModelFolder = "Assets/LivingVale/Art/Models";
        /// <summary>The palette texture folder.</summary>
        public const string TextureFolder = "Assets/LivingVale/Art/Textures";
        /// <summary>The shared material folder.</summary>
        public const string MaterialFolder = "Assets/LivingVale/Art/Materials";
        /// <summary>The palette PNG asset path.</summary>
        public const string PalettePath = TextureFolder + "/LV_Palette.png";
        /// <summary>The shared URP material asset path.</summary>
        public const string PaletteMaterialPath = MaterialFolder + "/LV_Palette.mat";

        private static readonly ArtKitEntry[] Catalog =
        {
            new ArtKitEntry("cottage_a", ArtKitCategory.Building, true, true, 5.75f),
            new ArtKitEntry("cottage_b", ArtKitCategory.Building, true, true, 5.13f),
            new ArtKitEntry("market_stall", ArtKitCategory.Building, true, true, 2.92f),
            new ArtKitEntry("well", ArtKitCategory.Building, true, true, 2.66f),
            new ArtKitEntry("fence_segment", ArtKitCategory.Prop, true, true, 1.1f),
            new ArtKitEntry("fence_post", ArtKitCategory.Prop, true, true, 1.2f),
            new ArtKitEntry("crate", ArtKitCategory.Prop, true, true, 0.7f),
            new ArtKitEntry("barrel", ArtKitCategory.Prop, true, true, 1.01f),
            new ArtKitEntry("sack", ArtKitCategory.Prop, false, true, 0.6f),
            new ArtKitEntry("signpost", ArtKitCategory.Prop, true, true, 2.35f),
            new ArtKitEntry("wagon", ArtKitCategory.Wagon, true, true, 1.53f),
            new ArtKitEntry("wagon_wrecked", ArtKitCategory.Wagon, true, true, 1.65f),
            new ArtKitEntry("tree_pine", ArtKitCategory.Nature, true, true, 7.0f),
            new ArtKitEntry("tree_broadleaf", ArtKitCategory.Nature, true, true, 6.18f),
            new ArtKitEntry("rock_a", ArtKitCategory.Nature, true, true, 1.41f),
            new ArtKitEntry("rock_b", ArtKitCategory.Nature, true, true, 0.72f),
            new ArtKitEntry("herb_plant", ArtKitCategory.Nature, false, true, 0.48f),
            new ArtKitEntry("ashwatch_gate", ArtKitCategory.Fortification, true, true, 7.0f),
            new ArtKitEntry("ashwatch_wall", ArtKitCategory.Fortification, true, true, 5.0f),
            new ArtKitEntry("ruin_wall", ArtKitCategory.Camp, true, true, 2.67f),
            new ArtKitEntry("camp_tent", ArtKitCategory.Camp, true, true, 1.95f),
            new ArtKitEntry("campfire", ArtKitCategory.Camp, false, true, 0.64f),
            new ArtKitEntry("humanoid_player", ArtKitCategory.Character, false, true, 1.81f),
            new ArtKitEntry("humanoid_villager", ArtKitCategory.Character, false, true, 1.81f),
            new ArtKitEntry("humanoid_healer", ArtKitCategory.Character, false, true, 1.81f),
            new ArtKitEntry("humanoid_raider", ArtKitCategory.Character, false, true, 1.86f),
            new ArtKitEntry("humanoid_guard", ArtKitCategory.Character, false, true, 1.85f),
            new ArtKitEntry("sword", ArtKitCategory.Gear, false, false, 1.0f),
            new ArtKitEntry("wolf", ArtKitCategory.Creature, false, true, 0.99f),
            new ArtKitEntry("humanoid_patient_seated", ArtKitCategory.Character, false, true, 1.39f),
            new ArtKitEntry("cobbles", ArtKitCategory.Landscape, false, true, 0.08f),
            new ArtKitEntry("cliff", ArtKitCategory.Landscape, false, true, 23.71f),
            new ArtKitEntry("mountain_a", ArtKitCategory.Landscape, false, true, 109.83f),
            new ArtKitEntry("mountain_b", ArtKitCategory.Landscape, false, true, 79.84f)
        };

        /// <summary>The immutable catalog, in placement order.</summary>
        public static IReadOnlyList<ArtKitEntry> Entries { get; } = Array.AsReadOnly(Catalog);

        /// <summary>The catalog ids, in placement order.</summary>
        public static IReadOnlyList<string> Ids { get; } = Array.AsReadOnly(Array.ConvertAll(Catalog, entry => entry.Id));

        /// <summary>Looks up a catalog id using an exact, case-sensitive match.</summary>
        public static bool TryGetEntry(string id, out ArtKitEntry entry)
        {
            foreach (ArtKitEntry candidate in Catalog)
            {
                if (string.Equals(candidate.Id, id, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = default;
            return false;
        }

        /// <summary>Returns the FBX asset path for an id.</summary>
        public static string ModelPath(string id) => $"{ModelFolder}/{id}.fbx";

        /// <summary>Loads a known model, returning null silently when it is unavailable.</summary>
        public static GameObject Load(string id)
        {
            return TryGetEntry(id, out _) ? AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(id)) : null;
        }

        /// <summary>Places a linked model prefab at a world position and yaw, or returns null silently.</summary>
        public static GameObject Place(string id, Vector3 position, float yRotation = 0f, Transform parent = null)
        {
            GameObject model = Load(id);
            if (model == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yRotation, 0f));
            instance.transform.SetParent(parent, worldPositionStays: true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            if (!Application.isBatchMode)
            {
                Undo.RegisterCreatedObjectUndo(instance, "Place art kit model");
            }

            return instance;
        }

        /// <summary>Returns the humanoid model id for an outfit.</summary>
        public static string HumanoidId(ArtOutfit outfit)
        {
            switch (outfit)
            {
                case ArtOutfit.Player: return "humanoid_player";
                case ArtOutfit.Villager: return "humanoid_villager";
                case ArtOutfit.Healer: return "humanoid_healer";
                case ArtOutfit.Raider: return "humanoid_raider";
                case ArtOutfit.Guard: return "humanoid_guard";
                default: throw new ArgumentOutOfRangeException(nameof(outfit), outfit, "Unknown outfit.");
            }
        }

        /// <summary>Replaces capsule visuals at their feet while preserving the capsule object and collider.</summary>
        public static GameObject SwapBody(GameObject capsuleBody, ArtOutfit outfit)
        {
            GameObject model = Load(HumanoidId(outfit));
            if (model == null)
            {
                return null;
            }

            Transform capsule = capsuleBody.transform;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, capsule.parent);
            instance.name = $"{capsuleBody.name} (humanoid)";
            instance.transform.localPosition = capsule.localPosition - Vector3.up * capsule.localScale.y;
            instance.transform.localRotation = capsule.localRotation;
            instance.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = capsuleBody.layer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }

            if (!Application.isBatchMode)
            {
                Undo.RegisterCreatedObjectUndo(instance, "Swap art kit body");
            }

            RemoveVisual(capsuleBody.GetComponent<MeshRenderer>());
            RemoveVisual(capsuleBody.GetComponent<MeshFilter>());
            return instance;
        }

        /// <summary>Loads the shared palette material, or returns null if setup has not run.</summary>
        public static Material PaletteMaterial => AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);

        /// <summary>Creates or updates and saves the shared matte URP Lit palette material.</summary>
        public static Material EnsurePaletteMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit shader was not found.");
            }

            Material material = PaletteMaterial;
            if (material == null)
            {
                EnsureFolder(MaterialFolder);
                material = new Material(shader) { name = "LV_Palette" };
                AssetDatabase.CreateAsset(material, PaletteMaterialPath);
            }

            material.shader = shader;
            Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            material.SetTexture("_BaseMap", palette);
            material.mainTexture = palette;
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>Sets up the palette material and reimports the complete model kit.</summary>
        [MenuItem("Living Vale/Art/Set Up Art Kit")]
        public static void SetUp()
        {
            try
            {
                EnsurePaletteMaterial();
                AssetDatabase.ImportAsset(ModelFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();
                int resolved = 0;
                foreach (string id in Ids)
                {
                    if (Load(id) != null)
                    {
                        resolved++;
                    }
                }

                Debug.Log($"[LivingVale] Art kit setup resolved {resolved}/{Ids.Count} catalog ids.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                else
                {
                    throw;
                }
            }
        }

        private static void RemoveVisual(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isBatchMode)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
            else
            {
                Undo.DestroyObjectImmediate(component);
            }
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
    }
}
