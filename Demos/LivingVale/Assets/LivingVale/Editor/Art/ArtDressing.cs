using UnityEditor;
using UnityEngine;

namespace LivingVale.Editor
{
    /// <summary>
    /// How the scene builders put the art kit on their grey-box primitives (G7b) without changing
    /// gameplay. The rule: <b>collision belongs to the primitive, looks belong to the kit.</b> Dressing a
    /// primitive keeps its collider exactly where it was and only hides its mesh, and the kit model's own
    /// colliders are switched off, so swapping art cannot move a wall, a talk range or a sight line.
    /// When a kit model is missing, every method leaves the primitive as it was and returns null.
    /// </summary>
    public static class ArtDressing
    {
        /// <summary>
        /// Shows kit model <paramref name="id"/> in place of <paramref name="primitive"/>'s mesh, under the
        /// same parent. The primitive keeps its collider (or is removed if it has none).
        /// </summary>
        public static GameObject Dress(GameObject primitive, string id, Vector3 position, float yaw, Vector3 scale)
        {
            GameObject model = Place(primitive != null ? primitive.transform.parent : null, id, position, yaw, scale,
                collide: false);
            if (model != null && primitive != null)
            {
                model.layer = primitive.layer;
                SetStatic(model, GameObjectUtility.GetStaticEditorFlags(primitive));
                HideLook(primitive);
            }

            return model;
        }

        /// <inheritdoc cref="Dress(GameObject, string, Vector3, float, Vector3)"/>
        public static GameObject Dress(GameObject primitive, string id, Vector3 position, float yaw = 0f, float scale = 1f)
        {
            return Dress(primitive, id, position, yaw, Vector3.one * scale);
        }

        /// <summary>
        /// Dresses several primitives with one model: the first keeps its collider like
        /// <see cref="Dress(GameObject, string, Vector3, float, Vector3)"/>, and so do the others.
        /// </summary>
        public static GameObject DressAll(GameObject[] primitives, string id, Vector3 position, float yaw, Vector3 scale)
        {
            GameObject model = Dress(primitives[0], id, position, yaw, scale);
            if (model != null)
            {
                for (int i = 1; i < primitives.Length; i++)
                {
                    HideLook(primitives[i]);
                }
            }

            return model;
        }

        /// <summary>
        /// Scenery that has no primitive behind it, static. <paramref name="collide"/> keeps the model's
        /// mesh collider; use it only away from paths, yards and NPCs.
        /// </summary>
        public static GameObject Decorate(Transform parent, string id, Vector3 position, float yaw = 0f, float scale = 1f,
            bool collide = false)
        {
            GameObject model = Place(parent, id, position, yaw, Vector3.one * scale, collide);
            if (model != null)
            {
                SetStatic(model, StaticEditorFlags.BatchingStatic);
            }

            return model;
        }

        /// <summary>
        /// A moving or animated part (a body, a held sword): placed under <paramref name="parent"/> in its
        /// local space, on <paramref name="layer"/>, with no collider and not static.
        /// </summary>
        public static GameObject Attach(Transform parent, string id, Vector3 localPosition, Quaternion localRotation,
            int layer, float scale = 1f)
        {
            GameObject model = ArtKit.Place(id, Vector3.zero, 0f, parent);
            if (model == null)
            {
                return null;
            }

            model.transform.localPosition = localPosition;
            model.transform.localRotation = localRotation;
            model.transform.localScale = Vector3.one * scale;
            DisableColliders(model);
            SetLayer(model, layer);
            return model;
        }

        /// <summary>Hides a primitive's mesh but keeps its collider; a primitive without one is removed.</summary>
        public static void HideLook(GameObject primitive)
        {
            if (primitive == null)
            {
                return;
            }

            if (primitive.GetComponent<Collider>() == null && primitive.transform.childCount == 0)
            {
                Object.DestroyImmediate(primitive);
                return;
            }

            Object.DestroyImmediate(primitive.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(primitive.GetComponent<MeshFilter>());
        }

        /// <summary>Sets the layer of an object and everything under it.</summary>
        public static void SetLayer(GameObject root, int layer)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        /// <summary>The humanoid outfit an NPC wears, by role (design §7): who they are should read at a glance.</summary>
        public static ArtOutfit OutfitFor(string npcId)
        {
            switch (npcId)
            {
                case "npc_iven": return ArtOutfit.Healer;
                case "npc_brann":
                case "npc_elra": return ArtOutfit.Guard;
                case "npc_kest": return ArtOutfit.Raider;
                default: return ArtOutfit.Villager;
            }
        }

        private static GameObject Place(Transform parent, string id, Vector3 position, float yaw, Vector3 scale, bool collide)
        {
            GameObject model = ArtKit.Place(id, position, yaw, parent);
            if (model == null)
            {
                return null;
            }

            model.transform.localScale = scale;
            if (!collide)
            {
                DisableColliders(model);
            }

            return model;
        }

        private static void DisableColliders(GameObject model)
        {
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private static void SetStatic(GameObject root, StaticEditorFlags flags)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
            }
        }
    }
}
