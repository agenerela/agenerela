using System.Collections.Generic;
using LivingVale.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LivingVale.Tests
{
    /// <summary>Checks imported kit geometry, palette settings and the scene-builder placement API.</summary>
    public sealed class ArtKitTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        // Unity's NUnit 3.5 has no Assert.Multiple; the first failing check stops the test instead.
        private static void Multiple(System.Action checks) => checks();

        private static IEnumerable<string> ModelIds => ArtKit.Ids;

        private static IEnumerable<string> BasePivotIds
        {
            get
            {
                foreach (ArtKitEntry entry in ArtKit.Entries)
                {
                    if (entry.PivotAtBase)
                    {
                        yield return entry.Id;
                    }
                }
            }
        }

        /// <summary>Removes all scene objects created by a test, including partially configured placements.</summary>
        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        /// <summary>Reports all missing or empty model ids in one failure.</summary>
        [Test]
        public void EveryIdResolvesToAnImportedModelWithTriangles()
        {
            var missing = new List<string>();
            Assert.That(ArtKit.Ids, Is.Not.Empty);
            foreach (string id in ArtKit.Ids)
            {
                GameObject model = ArtKit.Load(id);
                bool hasTriangles = false;
                if (model != null && AssetImporter.GetAtPath(ArtKit.ModelPath(id)) is ModelImporter)
                {
                    foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter.sharedMesh != null && TriangleCount(filter.sharedMesh) > 0)
                        {
                            hasTriangles = true;
                            break;
                        }
                    }
                }

                if (!hasTriangles)
                {
                    missing.Add(id);
                }
            }

            Assert.That(missing, Is.Empty, "Missing or triangle-free models " + string.Join(", ", missing));
        }

        /// <summary>Checks catalog uniqueness and filename-safe identifiers.</summary>
        [Test]
        public void IdsAreUniqueAndUseLowercaseFilenameCharacters()
        {
            var ids = new HashSet<string>(System.StringComparer.Ordinal);
            Multiple(() =>
            {
                foreach (string id in ArtKit.Ids)
                {
                    Assert.That(id, Does.Match("^[a-z0-9_]+$"), id);
                    Assert.That(ids.Add(id), Is.True, "Duplicate id " + id);
                    Assert.That(ArtKit.TryGetEntry(id, out ArtKitEntry entry), Is.True, id);
                    Assert.That(entry.Id, Is.EqualTo(id));
                }
            });
        }

        /// <summary>Checks every material slot against the single shared palette material.</summary>
        [TestCaseSource(nameof(ModelIds))]
        public void EveryRendererUsesExactlyThePaletteMaterial(string id)
        {
            GameObject model = RequireModel(id);
            Material palette = ArtKit.PaletteMaterial;
            Assert.That(palette, Is.Not.Null, ArtKit.PaletteMaterialPath);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, id);
            Multiple(() =>
            {
                foreach (Renderer renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { palette }), id + "/" + renderer.name);
                }
            });
        }

        /// <summary>Checks that imports need no root rotation or scale compensation.</summary>
        [TestCaseSource(nameof(ModelIds))]
        public void ModelRootHasIdentityRotationAndUnitScale(string id)
        {
            Transform root = RequireModel(id).transform;
            Multiple(() =>
            {
                Assert.That(Quaternion.Angle(root.localRotation, Quaternion.identity), Is.LessThan(0.001f), id);
                Assert.That(Vector3.Distance(root.localScale, Vector3.one), Is.LessThan(0.0001f), id);
            });
        }

        /// <summary>
        /// The pivot is on the ground at the centre of the model's main body. Shafts, open doors and
        /// spilled debris may stick out to one side, so the bounds' centre may sit up to half the
        /// half-size away from it.
        /// </summary>
        [TestCaseSource(nameof(BasePivotIds))]
        public void PivotIsAtTheBaseCentre(string id)
        {
            Bounds bounds = MeshBounds(RequireModel(id));
            Multiple(() =>
            {
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.03f), id + " base");
                Assert.That(Mathf.Abs(bounds.center.x), Is.LessThanOrEqualTo(bounds.extents.x * 0.5f + 0.00001f), id + " x");
                Assert.That(Mathf.Abs(bounds.center.z), Is.LessThanOrEqualTo(bounds.extents.z * 0.5f + 0.00001f), id + " z");
            });
        }

        /// <summary>Checks the approximate catalog dimensions, including the grip-pivoted sword.</summary>
        [TestCaseSource(nameof(ModelIds))]
        public void HeightMatchesTheCatalogWithinTwentyPercent(string id)
        {
            Bounds bounds = MeshBounds(RequireModel(id));
            Assert.That(ArtKit.TryGetEntry(id, out ArtKitEntry entry), Is.True);
            float height = id == "sword" ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : bounds.size.y;
            Assert.That(height, Is.InRange(entry.Height * 0.8f, entry.Height * 1.2f), id);
        }

        /// <summary>Checks the triangle budget without requiring readable vertex or index buffers.</summary>
        [TestCaseSource(nameof(ModelIds))]
        public void TriangleBudgetIsRespected(string id)
        {
            GameObject model = RequireModel(id);
            Assert.That(ArtKit.TryGetEntry(id, out ArtKitEntry entry), Is.True);
            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(filters, Is.Not.Empty, id);
            ulong triangles = 0;
            foreach (MeshFilter filter in filters)
            {
                Assert.That(filter.sharedMesh, Is.Not.Null, id + "/" + filter.name);
                triangles += TriangleCount(filter.sharedMesh);
            }

            ulong budget = entry.Category == ArtKitCategory.Character ? 3000UL : 2000UL;
            Assert.That(triangles, Is.InRange(1UL, budget), id);
        }

        /// <summary>Checks that only collidable catalog entries acquire mesh colliders.</summary>
        [TestCaseSource(nameof(ModelIds))]
        public void MeshCollidersMatchTheCatalog(string id)
        {
            GameObject model = RequireModel(id);
            Assert.That(ArtKit.TryGetEntry(id, out ArtKitEntry entry), Is.True);
            Assert.That(model.GetComponentsInChildren<MeshCollider>(true).Length > 0, Is.EqualTo(entry.Collidable), id);
        }

        /// <summary>Checks the palette import settings needed for crisp, colour-correct UV cells.</summary>
        [Test]
        public void PaletteTextureHasPointFilteringAndUncompressedSrgbPixels()
        {
            var importer = AssetImporter.GetAtPath(ArtKit.PalettePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, ArtKit.PalettePath);
            Multiple(() =>
            {
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(importer.sRGBTexture, Is.True);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.None));
                Assert.That(importer.isReadable, Is.False);
            });
        }

        /// <summary>Checks silent fallback for an id outside the catalog.</summary>
        [Test]
        public void PlaceUnknownIdReturnsNullWithoutLogging()
        {
            GameObject instance = Track(ArtKit.Place("no_such_asset", Vector3.zero));
            Assert.That(instance, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>Checks world placement, parenting and the retained model-prefab link.</summary>
        [Test]
        public void PlacePreservesWorldPoseUnderAParentAndRemainsAPrefabInstance()
        {
            RequireModel("crate");
            GameObject parent = Track(new GameObject("Art placement parent"));
            parent.transform.SetPositionAndRotation(new Vector3(8f, 2f, -4f), Quaternion.Euler(0f, 31f, 0f));
            parent.transform.localScale = Vector3.one * 2f;
            Vector3 position = new Vector3(-3f, 0.4f, 6f);
            GameObject instance = Track(ArtKit.Place("crate", position, 73f, parent.transform));
            Assert.That(instance, Is.Not.Null);
            Multiple(() =>
            {
                Assert.That(instance.transform.parent, Is.EqualTo(parent.transform));
                Assert.That(Vector3.Distance(instance.transform.position, position), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(instance.transform.rotation, Quaternion.Euler(0f, 73f, 0f)), Is.LessThan(0.001f));
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(instance), Is.True);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(instance), Is.EqualTo(ArtKit.Load("crate")));
            });
        }

        /// <summary>Checks that humanoid replacement preserves collider identity, feet, pose and layer.</summary>
        [Test]
        public void SwapBodyKeepsTheCapsuleColliderAndPlacesTheHumanoidAtItsFeet()
        {
            RequireModel(ArtKit.HumanoidId(ArtOutfit.Player));
            GameObject parent = Track(new GameObject("Actor"));
            parent.transform.SetPositionAndRotation(new Vector3(4f, 0f, 3f), Quaternion.Euler(0f, 20f, 0f));
            GameObject capsule = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            capsule.name = "Actor body";
            capsule.transform.SetParent(parent.transform, false);
            capsule.transform.localPosition = Vector3.up;
            capsule.transform.localScale = new Vector3(0.65f, 1f, 0.65f);
            capsule.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
            capsule.layer = 2;
            Collider collider = capsule.GetComponent<Collider>();
            GameObject humanoid = Track(ArtKit.SwapBody(capsule, ArtOutfit.Player));
            Assert.That(humanoid, Is.Not.Null);
            Multiple(() =>
            {
                Assert.That(capsule, Is.Not.Null);
                Assert.That(capsule.GetComponent<Collider>(), Is.SameAs(collider));
                Assert.That(capsule.GetComponent<MeshRenderer>(), Is.Null);
                Assert.That(capsule.GetComponent<MeshFilter>(), Is.Null);
                Assert.That(capsule.transform.parent, Is.EqualTo(parent.transform));
                Assert.That(humanoid.transform.parent, Is.EqualTo(parent.transform));
                Assert.That(Vector3.Distance(humanoid.transform.localPosition, Vector3.zero), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(humanoid.transform.localRotation, capsule.transform.localRotation), Is.LessThan(0.001f));
                Assert.That(humanoid.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(humanoid.name, Is.EqualTo("Actor body (humanoid)"));
                Assert.That(humanoid.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(humanoid), Is.True);
                foreach (Transform child in humanoid.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(child.gameObject.layer, Is.EqualTo(2), child.name);
                }
            });
        }

        /// <summary>Checks the shared URP shader, base map and matte material properties.</summary>
        [Test]
        public void PaletteMaterialUsesUrpLitAndThePaletteBaseMap()
        {
            Material material = ArtKit.PaletteMaterial;
            Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtKit.PalettePath);
            Assert.That(material, Is.Not.Null, ArtKit.PaletteMaterialPath);
            Assert.That(palette, Is.Not.Null, ArtKit.PalettePath);
            Multiple(() =>
            {
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(material.GetTexture("_BaseMap"), Is.EqualTo(palette));
                Assert.That(material.mainTexture, Is.EqualTo(palette));
                Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white));
                Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(0f));
                Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(0f));
            });
        }

        /// <summary>Checks that import processing is limited to the two exact art folders.</summary>
        [TestCase("Assets/LivingVale/Art/Models/crate.fbx", true)]
        [TestCase("Assets/LivingVale/Art/Models/nested/crate.fbx", true)]
        [TestCase("Assets/LivingVale/Art/Textures/LV_Palette.png", true)]
        [TestCase("Assets/LivingVale/Art/ModelsExtra/crate.fbx", false)]
        [TestCase("Assets/LivingVale/Art/TexturesExtra/LV_Palette.png", false)]
        [TestCase("Assets/LivingVale/Art/Materials/LV_Palette.mat", false)]
        [TestCase("Assets/LivingVale/Art/models/crate.fbx", false)]
        [TestCase("Assets/LivingVale/Art/Models", false)]
        [TestCase(null, false)]
        public void PostprocessorHandlesOnlyItsAssetFolders(string path, bool expected)
        {
            Assert.That(ArtKitPostprocessor.Handles(path), Is.EqualTo(expected));
        }

        /// <summary>
        /// Models face +Z, Unity's forward, so a placed model with no rotation looks down the scene's
        /// +Z. Each case names a feature that sticks out at the front: a person's toes, the wolf's
        /// snout, a cottage's door step. Among the vertices in the given height band, the one
        /// furthest from the pivot along Z must be on the +Z side.
        /// </summary>
        [TestCase("humanoid_player", 0f, 0.05f)]
        [TestCase("humanoid_villager", 0f, 0.05f)]
        [TestCase("humanoid_healer", 0f, 0.05f)]
        [TestCase("humanoid_raider", 0f, 0.05f)]
        [TestCase("humanoid_guard", 0f, 0.05f)]
        [TestCase("wolf", 0.6f, 2f)]
        [TestCase("cottage_a", 0f, 0.3f)]
        [TestCase("cottage_b", 0f, 0.25f)]
        public void ModelFacesPositiveZ(string id, float minY, float maxY)
        {
            float furthest = 0f;
            foreach (MeshFilter filter in RequireModel(id).GetComponentsInChildren<MeshFilter>(true))
            {
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    if (vertex.y >= minY && vertex.y <= maxY && Mathf.Abs(vertex.z) > Mathf.Abs(furthest))
                    {
                        furthest = vertex.z;
                    }
                }
            }

            Assert.That(furthest, Is.GreaterThan(0f), id + " faces -Z");
        }

        private GameObject Track(GameObject instance)
        {
            created.Add(instance);
            return instance;
        }

        private static GameObject RequireModel(string id)
        {
            GameObject model = ArtKit.Load(id);
            Assert.That(model, Is.Not.Null, "Missing model " + ArtKit.ModelPath(id));
            return model;
        }

        private static ulong TriangleCount(Mesh mesh)
        {
            ulong triangles = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) == MeshTopology.Triangles)
                {
                    triangles += mesh.GetIndexCount(i) / 3;
                }
            }

            return triangles;
        }

        private static Bounds MeshBounds(GameObject model)
        {
            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(filters, Is.Not.Empty, model.name);
            Bounds combined = default;
            bool hasBounds = false;
            foreach (MeshFilter filter in filters)
            {
                Assert.That(filter.sharedMesh, Is.Not.Null, model.name + "/" + filter.name);
                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toRoot = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3(x, y, z));
                            Vector3 point = toRoot.MultiplyPoint3x4(corner);
                            if (!hasBounds)
                            {
                                combined = new Bounds(point, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                combined.Encapsulate(point);
                            }
                        }
                    }
                }
            }

            return combined;
        }
    }
}
