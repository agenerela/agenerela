using System;
using System.IO;
using LivingVale.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LivingVale.Editor
{
    /// <summary>
    /// Photographs the built Village scene from fixed viewpoints (the Glen, the Old Road wreck, Reedbank,
    /// Ashwatch, Broken Watch and Herb Hollow) into the repository's <c>.scratch/lv-g7/scene-shots/</c>, so
    /// the art can be checked in the game without opening the Editor. Batch mode, but not
    /// <c>-nographics</c>: it renders with the scene's own lighting and fog. The scene is not saved.
    /// </summary>
    public static class VillageSceneShots
    {
        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("Living Vale/Art/Capture Village Scene Shots")]
        public static void Capture()
        {
            try
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                EditorSceneManager.OpenScene(VillageSceneBuilder.ScenePath, OpenSceneMode.Single);
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.scratch/lv-g7/scene-shots"));
                Directory.CreateDirectory(output);
                int shots = 0;
                // The first render after opening a scene runs before its ambient light is set up and
                // comes out tinted; render once and throw it away.
                var (_, firstEye, firstTarget) = Viewpoints()[0];
                string warmUp = Path.Combine(output, "warm-up.png");
                Render(firstEye, firstTarget, warmUp);
                File.Delete(warmUp);
                foreach (var (name, eye, target) in Viewpoints())
                {
                    Render(eye, target, Path.Combine(output, name + ".png"));
                    shots++;
                }

                Debug.Log($"[LivingVale] Captured {shots} scene shots in {output}.");
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

        private static (string name, Vector3 eye, Vector3 target)[] Viewpoints()
        {
            Vector3 At(string place) => new Vector3(ValePlaces.Get(place).X, 0f, ValePlaces.Get(place).Z);
            Vector3 road = At(ValePlaces.OldRoad), reed = At(ValePlaces.Reedbank), ash = At(ValePlaces.Ashwatch);
            Vector3 watch = At(ValePlaces.BrokenWatch), hollow = At(ValePlaces.HerbHollow);
            return new[]
            {
                ("glen", new Vector3(-13f, 7f, -15f), new Vector3(0f, 1.5f, 4f)),
                ("glen_player", new Vector3(-5.5f, 2.6f, -6f), new Vector3(-5f, 1.3f, 3f)),
                ("old_road_wreck", road + new Vector3(-9f, 4f, -10f), road + new Vector3(0f, 0.8f, 0f)),
                ("reedbank", reed + new Vector3(-28f, 9f, -32f), reed + new Vector3(-4f, 2f, -10f)),
                ("ashwatch", ash + new Vector3(-20f, 8f, -26f), ash + new Vector3(0f, 6f, 13f)),
                ("broken_watch", watch + new Vector3(18f, 9f, -22f), watch + new Vector3(0f, 1f, 0f)),
                ("herb_hollow", hollow + new Vector3(10f, 5f, -14f), hollow + new Vector3(0f, 0.5f, 0f)),
                ("vale_overview", new Vector3(-60f, 70f, -120f), new Vector3(60f, 0f, -90f))
            };
        }

        private static void Render(Vector3 eye, Vector3 target, string path)
        {
            var cameraObject = new GameObject("Scene shot camera") { hideFlags = HideFlags.HideAndDontSave };
            var target2D = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = 60f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 1000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.38f, 0.5f, 0.6f);
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
                camera.targetTexture = target2D;
                camera.Render();
                RenderTexture.active = target2D;
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(pixels);
                target2D.Release();
                UnityEngine.Object.DestroyImmediate(target2D);
            }
        }
    }
}
