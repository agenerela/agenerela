using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Agenerela.Providers;
using Demos.Shared;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>
    /// The capture tool's second half: the village in Play mode with every UI Toolkit panel drawn
    /// over it, and the frame time of a fixed view. Both run headless.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A window-less editor has no screen for the UI to draw on, so the panels draw into a
    /// RenderTexture instead (<see cref="PanelSettings.targetTexture"/>), the main camera into
    /// another of the same size, and the two are laid over each other. The agents answer from a
    /// scripted provider (the framework's <see cref="ScriptedProviderConfig"/>), never a real model.
    /// </para>
    /// <para>
    /// <c>Unity -batchmode -projectPath Demos/GreyBoxVillage -executeMethod
    /// Demos.GreyBoxVillage.Editor.VillageCapture.CaptureUiFromCommandLine -captureOut DIR
    /// [-captureSizes 1280x720,1920x1080]</c>, and <c>MeasureFromCommandLine</c> for the frame time,
    /// both with no <c>-quit</c> and no <c>-nographics</c>: they enter Play mode, work through
    /// their steps, and exit the editor themselves, with code 1 if anything failed. The work is one
    /// coroutine across Play mode, so the editor is told to skip the domain reload while it runs, and
    /// is set back before it exits.
    /// </para>
    /// </remarks>
    public static partial class VillageCapture
    {
        private static IEnumerator running;
        private static bool exitWhenDone;
        private static int failures;
        private static bool savedOptionsEnabled;
        private static EnterPlayModeOptions savedOptions;

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static (int w, int h)[] Sizes() =>
            (Arg("-captureSizes") ?? "1280x720,1920x1080").Split(',')
            .Select(s => s.Split('x'))
            .Select(p => (int.Parse(p[0], CultureInfo.InvariantCulture), int.Parse(p[1], CultureInfo.InvariantCulture)))
            .ToArray();

        public static void CaptureUiFromCommandLine() =>
            Run(UiShots(Arg("-captureOut") ?? Path.GetFullPath("Logs/captures"), Sizes()), true);

        public static void MeasureFromCommandLine() =>
            Run(Measure(Arg("-captureOut") ?? Path.GetFullPath("Logs/captures")), true);

        [MenuItem("Tools/GreyBoxVillage/Capture the UI (Play mode)")]
        private static void CaptureUiFromMenu() =>
            Run(UiShots(Path.GetFullPath("Logs/captures"), new[] { (1280, 720), (1920, 1080) }), false);

        private static void Run(IEnumerator work, bool exit)
        {
            if (running != null) throw new InvalidOperationException("A capture is already running.");
            EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Single);
            savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            savedOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            failures = 0;
            exitWhenDone = exit;
            running = Wrapped(work);
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        // The work, then leaving Play mode and putting the editor's settings back.
        private static IEnumerator Wrapped(IEnumerator work)
        {
            while (!EditorApplication.isPlaying) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
            while (true)
            {
                bool more;
                try { more = work.MoveNext(); }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    failures++;
                    break;
                }
                if (!more) break;
                yield return work.Current;
            }
            (work as IDisposable)?.Dispose();
            EditorApplication.ExitPlaymode();
            while (EditorApplication.isPlaying) yield return null;
        }

        private static void Tick()
        {
            if (running.MoveNext()) return;
            EditorApplication.update -= Tick;
            running = null;
            EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
            EditorSettings.enterPlayModeOptions = savedOptions;
            Debug.Log("[VillageCapture] done, " + failures + " failure(s)");
            if (exitWhenDone) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        // Waits whole frames: the editor can tick more than once a frame.
        private static IEnumerable<object> Frames(int n)
        {
            int until = Time.frameCount + n;
            while (Time.frameCount < until) yield return null;
        }

        private static IEnumerable<object> Until(Func<bool> done, int maxFrames = 300)
        {
            int until = Time.frameCount + maxFrames;
            while (!done() && Time.frameCount < until) yield return null;
        }

        // ---- The UI views --------------------------------------------------------------------

        private static IEnumerator UiShots(string folder, (int w, int h)[] sizes)
        {
            Directory.CreateDirectory(folder);
            Application.targetFrameRate = 30;
            var commands = Object.FindFirstObjectByType<VillageCommands>();
            var config = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
            config.Scripted.Otherwise(ScriptedReply.Decide("none", "no_target", "Good day."));
            foreach (var mind in commands.Minds)
                if (mind.Senses != null) mind.Senses.enabled = false;
            var events = commands.GetComponent<VillageEvents>();
            if (events != null) events.enabled = false;
            var panel = Resources.Load<PanelSettings>("VillagePanelSettings");
            var camera = Camera.main;
            var saved = (panel.targetTexture, panel.clearColor, panel.colorClearValue);
            try
            {
                foreach (var (w, h) in sizes)
                {
                    var (world, ui) = Targets(camera, panel, w, h);
                    foreach (float hour in new[] { 11f, 22.5f })
                    {
                        string when = (hour < 18f ? "day" : "night") + "_" + w + "x" + h;
                        foreach (var x in Village(commands, config, hour, folder, when, world, ui)) yield return x;
                    }
                    // The presenter's situations (VillageCapture.Situations.cs).
                    foreach (var x in Situations(commands, config, folder, "_" + w + "x" + h, world, ui)) yield return x;
                    camera.targetTexture = null;
                    Release(world, ui, panel);
                }

                // The title screen, last: loading it ends the village.
                EditorSceneManager.LoadSceneInPlayMode(DemoLauncher.LauncherPath, new LoadSceneParameters(LoadSceneMode.Single));
                foreach (var x in Frames(10)) yield return x;
                foreach (var (w, h) in sizes)
                {
                    var titleCamera = Camera.main;
                    var (world, ui) = Targets(titleCamera, panel, w, h);
                    foreach (var x in Shoot(folder, "title_" + w + "x" + h, world, ui)) yield return x;
                    if (titleCamera != null) titleCamera.targetTexture = null;
                    Release(world, ui, panel);
                }
            }
            finally
            {
                (panel.targetTexture, panel.clearColor, panel.colorClearValue) = saved;
                Application.targetFrameRate = -1;
                Object.DestroyImmediate(config);
            }
        }

        private static (RenderTexture world, RenderTexture ui) Targets(Camera camera, PanelSettings panel, int w, int h)
        {
            var world = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            var ui = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            var previous = RenderTexture.active;
            RenderTexture.active = world;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
            if (camera != null) camera.targetTexture = world;
            panel.targetTexture = ui;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;
            return (world, ui);
        }

        private static void Release(RenderTexture world, RenderTexture ui, PanelSettings panel)
        {
            if (panel.targetTexture == ui) panel.targetTexture = null;
            world.Release();
            ui.Release();
            Object.DestroyImmediate(world);
            Object.DestroyImmediate(ui);
        }

        // One run through the village at one hour: each view the HUD, the dialogue, the bubbles, the
        // framework view, the book, the menu and the no-model warning give.
        private static IEnumerable<object> Village(VillageCommands commands, ScriptedProviderConfig config, float hour,
            string folder, string when, RenderTexture world, RenderTexture ui)
        {
            var scripted = config.Scripted;
            scripted.Otherwise(ScriptedReply.Decide("none", "no_target", "Good day."));
            commands.Day.Running = false;
            commands.Day.TimeOfDay = hour / 24f;
            commands.UseProvider(config);
            foreach (var mind in commands.Minds) mind.Body.ResetState();
            commands.Inventory.Reset(8);
            commands.Quests.Define();
            if (commands.Board.Expanded) commands.Board.Toggle();
            commands.Book.Show(false);
            commands.Menu.Close();

            IEnumerable<object> Snap(string name) => Shoot(folder, name + "_" + when, world, ui);
            IEnumerable<object> Ask(string agent, string line, ScriptedReply reply)
            {
                scripted.Otherwise(reply);
                var awaiter = commands.Mind(agent).HearPlayerAsync(line).GetAwaiter();
                return Until(() => awaiter.IsCompleted);
            }

            // Odile offers her quest: the dialogue panel, its chips, the tracker and the toast.
            foreach (var x in Stand(commands, "Odile", 3.2f)) yield return x;
            foreach (var x in Ask("Odile", "Any work for a traveller?",
                         ScriptedReply.Decide("offer_quest", "quest_moonpetals", "Bring me three moonpetals from the woods past the river. They glow after dark."))) yield return x;
            foreach (var x in Snap("dialogue")) yield return x;

            // She is asked for what the player cannot afford: a guard contains it, an amber bubble.
            foreach (var x in Ask("Odile", "I'll take the lantern.",
                         ScriptedReply.Decide("sell", "lantern", "Here you are, love."))) yield return x;
            foreach (var x in Snap("refusal")) yield return x;

            // Bram answers in his own words: a speech bubble.
            foreach (var x in Stand(commands, "Bram", 3.5f)) yield return x;
            foreach (var x in Ask("Bram", "Is the road east safe?",
                         ScriptedReply.Decide("none", "no_target", "Safe enough by day. Keep to the road and don't linger after dark."))) yield return x;
            foreach (var x in Snap("bubble")) yield return x;

            // Nobody in earshot: the HUD alone, with the quest tracker, on the road into the square.
            foreach (var mind in commands.Minds) mind.Body.Hush();
            foreach (var x in Place(commands, new Vector3(0.8f, 0, -5.5f), new Vector3(2f, 0, 12f))) yield return x;
            foreach (var x in Snap("hud")) yield return x;

            // The framework view.
            commands.Board.Toggle();
            foreach (var x in Snap("framework")) yield return x;
            commands.Board.Toggle();

            // The pack and the journal, with something in each.
            commands.Inventory.Add("moonpetal", 2);
            commands.Inventory.Add("bread");
            commands.Inventory.Add("old_lantern");
            var lantern = commands.Quests.Get("quest_lantern");
            commands.Quests.Offer(lantern);
            commands.Quests.Complete(lantern);
            commands.Quests.Offer(commands.Quests.Get("quest_wolf"));
            commands.Inventory.Add("torch");
            commands.Book.Open("inventory");
            foreach (var x in Snap("pack")) yield return x;
            commands.Book.Open("journal");
            foreach (var x in Snap("journal")) yield return x;
            commands.Book.Show(false);

            // The Esc menu, and its list of controls. (The request history, H, is IMGUI, which a
            // headless editor does not draw, so it is not captured.)
            commands.Menu.Open();
            foreach (var x in Snap("menu")) yield return x;
            commands.Menu.ShowControls(true);
            foreach (var x in Snap("controls")) yield return x;
            commands.Menu.ShowControls(false);
            commands.Menu.Close();

            // No model: the HUD says so, and so does the agent who was asked.
            foreach (var x in Stand(commands, "Bram", 3.5f)) yield return x;
            foreach (var x in Ask("Bram", "Open the gate, please.",
                         ScriptedReply.Fail(ProviderErrorKind.Unreachable, "The capture tool's provider is set to fail."))) yield return x;
            foreach (var x in Snap("nomodel")) yield return x;
            scripted.Otherwise(ScriptedReply.Decide("none", "no_target", "Good day."));
        }

        // Puts the player in front of an agent, facing it, with the camera behind the player.
        private static IEnumerable<object> Stand(VillageCommands commands, string agent, float distance)
        {
            var t = commands.Mind(agent).transform;
            return Place(commands, t.position + t.forward * distance, t.position);
        }

        private static IEnumerable<object> Place(VillageCommands commands, Vector3 at, Vector3 lookAt)
        {
            var player = commands.Player;
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.position = at;
            var look = lookAt - at;
            look.y = 0;
            if (look.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(look);
            if (controller != null) controller.enabled = true;
            var rig = Object.FindFirstObjectByType<DemoCameraRig>();
            if (rig != null)
            {
                rig.Yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg + 25f;
                rig.Pitch = 16f;
                rig.Distance = 6.5f;
            }
            int until = Time.frameCount + 40;
            while (Time.frameCount < until) yield return null;
        }

        // Lets the layout and the camera settle, then reads both layers and lays the UI over the world.
        private static IEnumerable<object> Shoot(string folder, string name, RenderTexture world, RenderTexture ui)
        {
            int until = Time.frameCount + 8;
            while (Time.frameCount < until) yield return null;
            var w = Read(world);
            var u = Read(ui);
            var a = w.GetPixels();
            var b = u.GetPixels();
            for (int i = 0; i < a.Length; i++)
            {
                // The UI is drawn with premultiplied alpha, so its colour already carries its coverage;
                // both layers are blended in linear space, as the GPU blends into an sRGB target.
                var top = b[i].linear;
                var under = a[i].linear;
                float k = 1f - b[i].a;
                a[i] = new Color(top.r + under.r * k, top.g + under.g * k, top.b + under.b * k, 1f).gamma;
            }
            w.SetPixels(a);
            w.Apply();
            string path = Path.Combine(folder, name + ".png");
            File.WriteAllBytes(path, w.EncodeToPNG());
            Debug.Log("[VillageCapture] " + path);
            Object.DestroyImmediate(w);
            Object.DestroyImmediate(u);
        }

        private static Texture2D Read(RenderTexture rt)
        {
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        // ---- Frame time ----------------------------------------------------------------------

        // The first three are slice 1's views, so the figures compare. Slice 3 adds the busiest view,
        // the square, in rain (the rain, the mist of its haze and the cloud dome) and at night (every
        // lamp and door light), and the woods.
        private static readonly (string name, Vector3 from, Vector3 to, float hour, WeatherKind weather)[] Views =
        {
            ("overview", new Vector3(0, 40, -64), new Vector3(0, 0, 8), 11f, WeatherKind.Clear),
            ("square", new Vector3(-10, 6, -10), new Vector3(8, 1.5f, 4), 11f, WeatherKind.Clear),
            ("north", new Vector3(-4, 8, 20), new Vector3(8, 3, 40), 11f, WeatherKind.Clear),
            ("square_rain", new Vector3(-10, 6, -10), new Vector3(8, 1.5f, 4), 11f, WeatherKind.Rain),
            ("square_night", new Vector3(-10, 6, -10), new Vector3(8, 1.5f, 4), 22.5f, WeatherKind.Clear),
            ("woods", new Vector3(24, 7, 26), new Vector3(40, 1, 42), 11f, WeatherKind.Clear),
        };

        // Holds the camera on fixed views at 1920x1080 with no frame cap, and logs the mean frame
        // time, the main thread's time and the draw calls of each.
        private static IEnumerator Measure(string folder)
        {
            Directory.CreateDirectory(folder);
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            var commands = Object.FindFirstObjectByType<VillageCommands>();
            var config = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
            config.Scripted.Otherwise(ScriptedReply.Decide("none", "no_target", "Good day."));
            commands.UseProvider(config);
            foreach (var mind in commands.Minds)
                if (mind.Senses != null) mind.Senses.enabled = false;
            var camera = Camera.main;
            var rig = camera.GetComponent<DemoCameraRig>();
            if (rig != null) rig.enabled = false;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = rt;
            commands.Day.Running = false;
            commands.Day.TimeOfDay = 11f / 24f;
            if (commands.Weather != null) commands.Weather.Changes = false;
            var lines = new List<string>();
            using (var mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 400))
            using (var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"))
            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"))
            using (var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"))
            {
                foreach (var (name, from, to, hour, weather) in Views)
                {
                    commands.Day.TimeOfDay = hour / 24f;
                    if (commands.Routine != null) commands.Routine.CatchUp();
                    if (commands.Weather != null)
                    {
                        commands.Weather.Set(weather);
                        commands.Weather.Snap();
                    }
                    camera.transform.position = from;
                    camera.transform.LookAt(to);
                    // Long enough for the rain to fill the air round the camera before timing it.
                    foreach (var x in Frames(weather == WeatherKind.Clear ? 60 : 400)) yield return x;
                    float total = 0;
                    const int n = 300;
                    long thread = 0;
                    for (int i = 0; i < n; i++)
                    {
                        int frame = Time.frameCount;
                        while (Time.frameCount == frame) yield return null;
                        total += Time.unscaledDeltaTime;
                        thread += mainThread.LastValue;
                    }
                    string line = string.Format(CultureInfo.InvariantCulture,
                        "{0}: frame {1:0.00} ms, main thread {2:0.00} ms, draw calls {3}, batches {4}, set-pass {5}, triangles {6}",
                        name, total / n * 1000f, thread / (double)n / 1e6, drawCalls.LastValue, batches.LastValue, setPass.LastValue, triangles.LastValue);
                    Debug.Log("[VillageCapture] " + line);
                    lines.Add(line);
                }
            }
            File.WriteAllLines(Path.Combine(folder, "frame-time.txt"), lines);
            camera.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(config);
        }
    }
}
