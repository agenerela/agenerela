using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LivingVale.Editor
{
    /// <summary>Builds and photographs a labelled grid of the imported art kit.</summary>
    public static class ArtPreviewBuilder
    {
        /// <summary>The preview scene asset path.</summary>
        public const string ScenePath = "Assets/LivingVale/Scenes/ArtPreview.unity";

        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        /// <summary>Creates and saves the preview scene, with one row per catalog category.</summary>
        [MenuItem("Living Vale/Art/Build Art Preview Scene")]
        public static void Build()
        {
            try
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                BuildScene();
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

        /// <summary>Opens the preview and captures front, back and category-row PNGs in repository scratch space.</summary>
        [MenuItem("Living Vale/Art/Capture Art Preview")]
        public static void Capture()
        {
            try
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    // Calling the menu entry here would exit batch mode before capturing.
                    BuildScene();
                }

                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                CaptureScene(scene);
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

        private static void BuildScene()
        {
            Material palette = ArtKit.EnsurePaletteMaterial();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var grid = new GameObject("Art kit");
            Material missingMaterial = null;
            int resolved = 0;
            int rowIndex = 0;
            foreach (ArtKitCategory category in Enum.GetValues(typeof(ArtKitCategory)))
            {
                var row = new GameObject(category.ToString());
                row.transform.SetParent(grid.transform, false);
                row.transform.localPosition = new Vector3(0f, 0f, rowIndex * 14f);
                float cursor = 0f;
                foreach (ArtKitEntry entry in ArtKit.Entries)
                {
                    if (entry.Category != category)
                    {
                        continue;
                    }

                    // Models face +Z; turned half round they face the preview camera on the -Z side.
                    GameObject model = ArtKit.Place(entry.Id, row.transform.position, 180f, row.transform);
                    bool missing = model == null;
                    if (missing)
                    {
                        if (missingMaterial == null)
                        {
                            missingMaterial = new Material(palette.shader) { name = "Missing art", color = Color.red };
                        }

                        model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        model.name = entry.Id + " (missing)";
                        model.transform.SetParent(row.transform, false);
                        model.transform.localScale = Vector3.one * 0.5f;
                        model.transform.localPosition = Vector3.up * 0.25f;
                        model.GetComponent<Renderer>().sharedMaterial = missingMaterial;
                    }
                    else
                    {
                        resolved++;
                    }

                    Bounds bounds = RendererBounds(model);
                    Vector3 offset = Vector3.right * (cursor - bounds.min.x);
                    if (!entry.PivotAtBase)
                    {
                        offset.y = -bounds.min.y;
                    }

                    model.transform.position += offset;
                    bounds = RendererBounds(model);
                    AddLabel(row.transform, missing ? entry.Id + " (missing)" : entry.Id, bounds);
                    cursor = bounds.max.x + 1.5f;
                }

                rowIndex++;
            }

            Bounds gridBounds = RendererBounds(grid);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Preview ground";
            ground.transform.position = new Vector3(gridBounds.center.x, -0.5f, gridBounds.center.z);
            ground.transform.localScale = new Vector3(Mathf.Max(20f, gridBounds.size.x + 16f), 1f,
                Mathf.Max(20f, gridBounds.size.z + 16f));
            Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/LivingVale/Materials/Stone.mat");
            ground.GetComponent<Renderer>().sharedMaterial = stone != null ? stone : palette;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.72f);

            Camera camera = CreateCamera("Preview camera");
            Frame(camera, gridBounds, 0f, 50f);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Could not save {ScenePath}.");
            }

            Debug.Log($"[LivingVale] Built {ScenePath}, {resolved} models, {ArtKit.Ids.Count - resolved} missing, {rowIndex} rows.");
        }

        private static void AddLabel(Transform row, string text, Bounds modelBounds)
        {
            var label = new GameObject(text + " label").AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            label.transform.SetParent(row, false);
            label.transform.SetPositionAndRotation(
                new Vector3(modelBounds.center.x, 0.05f, modelBounds.min.z - 0.75f),
                Quaternion.Euler(90f, 0f, 0f));
            label.text = text;
            label.characterSize = Mathf.Clamp(modelBounds.size.x * 0.06f, 0.08f, 0.35f);
            label.fontSize = 64;
            label.anchor = TextAnchor.UpperCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
        }

        private static void CaptureScene(Scene scene)
        {
            GameObject grid = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Art kit")
                {
                    grid = root;
                    break;
                }
            }

            if (grid == null)
            {
                throw new InvalidOperationException("The preview has no Art kit grid. Rebuild the preview scene.");
            }

            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.scratch/lv-g7/unity-renders"));
            Directory.CreateDirectory(output);
            Camera camera = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            RenderTexture previous = RenderTexture.active;
            int captures = 0;
            try
            {
                camera = CreateCamera("Art capture camera");
                camera.gameObject.hideFlags = HideFlags.HideAndDontSave;
                camera.enabled = false;
                target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4
                };
                if (!target.Create())
                {
                    throw new InvalidOperationException("Could not create the preview render texture.");
                }

                pixels = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                Bounds gridBounds = RendererBounds(grid);
                Frame(camera, gridBounds, 0f, 50f);
                CapturePng(camera, pixels, Path.Combine(output, "overview.png"));
                captures++;
                foreach (ArtKitCategory category in Enum.GetValues(typeof(ArtKitCategory)))
                {
                    Transform row = grid.transform.Find(category.ToString());
                    if (row == null)
                    {
                        throw new InvalidOperationException($"The preview row {category} is missing. Rebuild the preview scene.");
                    }

                    Frame(camera, RendererBounds(row.gameObject), -30f, 20f);
                    string filename = "row_" + category.ToString().ToLowerInvariant() + ".png";
                    CapturePng(camera, pixels, Path.Combine(output, filename));
                    captures++;
                }

                Frame(camera, gridBounds, 180f, 50f);
                CapturePng(camera, pixels, Path.Combine(output, "back_overview.png"));
                captures++;
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null)
                {
                    camera.targetTexture = null;
                    UnityEngine.Object.DestroyImmediate(camera.gameObject);
                }

                if (pixels != null)
                {
                    UnityEngine.Object.DestroyImmediate(pixels);
                }

                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }

            Debug.Log($"[LivingVale] Captured {captures} art preview PNGs in {output}.");
        }

        private static Camera CreateCamera(string name)
        {
            var camera = new GameObject(name).AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.aspect = (float)CaptureWidth / CaptureHeight;
            camera.nearClipPlane = 0.1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.38f, 0.5f, 0.6f);
            camera.allowHDR = false;
            camera.allowMSAA = true;
            return camera;
        }

        private static void Frame(Camera camera, Bounds bounds, float azimuth, float elevation)
        {
            Quaternion rotation = Quaternion.Euler(elevation, azimuth, 0f);
            Quaternion inverse = Quaternion.Inverse(rotation);
            float verticalTangent = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float horizontalTangent = verticalTangent * camera.aspect;
            float distance = 1f;
            // Fit every corner in camera space, including the nearer edge of deep rows.
            Vector3 extents = bounds.extents + Vector3.one;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = inverse * Vector3.Scale(extents, new Vector3(x, y, z));
                        distance = Mathf.Max(distance, Mathf.Abs(corner.x) / horizontalTangent - corner.z);
                        distance = Mathf.Max(distance, Mathf.Abs(corner.y) / verticalTangent - corner.z);
                        distance = Mathf.Max(distance, camera.nearClipPlane + 0.1f - corner.z);
                    }
                }
            }

            camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
            camera.farClipPlane = distance + bounds.size.magnitude + 10f;
        }

        private static void CapturePng(Camera camera, Texture2D pixels, string path)
        {
            camera.Render();
            RenderTexture.active = camera.targetTexture;
            pixels.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0, false);
            pixels.Apply(false, false);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }

        private static Bounds RendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
