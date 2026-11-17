using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace LivingVale.Unity
{
    /// <summary>
    /// G8a: a check of the HUD layout without the Editor. Started with <c>-lvHudShot &lt;folder&gt;</c>,
    /// the player starts on scripted replies (so it never loads a model), waits for the HUD, writes
    /// <c>hud-&lt;width&gt;.png</c>, opens the map, writes <c>map-&lt;width&gt;.png</c> and quits. Run it
    /// once per resolution with <c>-screen-width</c> and <c>-screen-height</c>. Without the argument
    /// it does nothing.
    /// </summary>
    public sealed class HudShot : MonoBehaviour
    {
        private const string Argument = "-lvHudShot";

        private Action openMap;
        private string folder;

        /// <summary>The folder to write to, or null when the player was not started for a shot.</summary>
        public static string RequestedFolder()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == Argument)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        public static void StartIfRequested(GameObject host, Action openMap)
        {
            string target = RequestedFolder();
            if (string.IsNullOrEmpty(target))
            {
                return;
            }

            var shot = host.AddComponent<HudShot>();
            shot.folder = target;
            shot.openMap = openMap;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(3f);
            yield return Capture("hud");
            openMap?.Invoke();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("map");
            Application.Quit();
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(folder, $"{name}-{Screen.width}.png");
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            // The back buffer's alpha is whatever the UI blended last; a PNG viewer would show the
            // scene through every panel. The screen is opaque, so the picture is too.
            Color32[] pixels = image.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].a = 255;
            }

            image.SetPixels32(pixels);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Destroy(image);
        }
    }
}
