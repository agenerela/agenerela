using System;
using System.IO;
using System.Linq;
using Demos.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>
    /// Renders the village from fixed viewpoints at chosen hours into PNGs, with no window: the way
    /// to look at an art change without opening the editor.
    /// <c>Unity -batchmode -projectPath Demos/GreyBoxVillage -executeMethod
    /// Demos.GreyBoxVillage.Editor.VillageCapture.CaptureFromCommandLine -captureOut DIR [-captureRebuild]
    /// [-captureOnly name,name] -quit</c>. Needs a GPU, so leave out <c>-nographics</c>.
    /// </summary>
    public static class VillageCapture
    {
        private readonly struct Shot
        {
            public readonly string Name;
            public readonly Vector3 From, To;
            public readonly float Hour, Fov;

            public Shot(string name, Vector3 from, Vector3 to, float hour, float fov = 50f)
            {
                Name = name;
                From = from;
                To = to;
                Hour = hour;
                Fov = fov;
            }
        }

        private static readonly Shot[] Shots =
        {
            new Shot("overview_day", new Vector3(0, 40, -64), new Vector3(0, 0, 8), 10f),
            new Shot("gate_day", new Vector3(7, 4, -27), new Vector3(0, 2.5f, -14), 9f),
            new Shot("square_day", new Vector3(-10, 6, -10), new Vector3(8, 1.5f, 4), 11f),
            new Shot("market_day", new Vector3(11, 3.2f, -8), new Vector3(17, 1.6f, 3.5f), 13f),
            new Shot("river_day", new Vector3(-15, 6.5f, 13), new Vector3(8, 0, 24), 15f),
            new Shot("north_day", new Vector3(-4, 8, 20), new Vector3(8, 3, 40), 10f),
            new Shot("bram_close", new Vector3(-0.6f, 2.0f, -9.6f), new Vector3(-2.5f, 1.2f, -13), 11f, 40f),
            new Shot("camp_day", new Vector3(5, 4.5f, -2), new Vector3(15, 1, -11), 16f),
            new Shot("overview_dusk", new Vector3(0, 40, -64), new Vector3(0, 0, 8), 18.2f),
            new Shot("square_night", new Vector3(-10, 6, -10), new Vector3(8, 1.5f, 4), 22.5f),
            new Shot("gate_night", new Vector3(7, 4, -27), new Vector3(0, 2.5f, -14), 23f),
        };

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name)
            {
                int i = Array.IndexOf(args, name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }
            string folder = Arg("-captureOut") ?? Path.GetFullPath("Logs/captures");
            if (args.Contains("-captureRebuild")) VillageSceneBuilder.RebuildFromCommandLine();
            else EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Single);
            var only = Arg("-captureOnly")?.Split(',');
            Capture(folder, only);
        }

        public static void Capture(string folder, string[] only = null)
        {
            Directory.CreateDirectory(folder);
            var cycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
            var main = Camera.main;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            var go = new GameObject("capture camera");
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(main);
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camera.targetTexture = rt;
            var read = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            try
            {
                foreach (var shot in Shots)
                {
                    if (only != null && !only.Contains(shot.Name)) continue;
                    if (cycle != null)
                    {
                        cycle.TimeOfDay = shot.Hour / 24f;
                        cycle.Apply();
                        foreach (var lamp in UnityEngine.Object.FindObjectsByType<FlickerLight>(FindObjectsSortMode.None))
                            lamp.GetComponent<Light>().enabled = !lamp.OnlyAtNight || cycle.Darkness > 0.45f;
                    }
                    foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                        ps.Simulate(5f, true, true);
                    camera.transform.position = shot.From;
                    camera.transform.LookAt(shot.To);
                    camera.fieldOfView = shot.Fov;
                    camera.Render();
                    RenderTexture.active = rt;
                    read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    read.Apply();
                    RenderTexture.active = null;
                    string path = Path.Combine(folder, shot.Name + ".png");
                    File.WriteAllBytes(path, read.EncodeToPNG());
                    Debug.Log("[VillageCapture] " + path);
                }
            }
            finally
            {
                if (cycle != null)
                {
                    cycle.TimeOfDay = 0.36f;
                    cycle.Apply();
                }
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(read);
                rt.Release();
            }
        }
    }
}
