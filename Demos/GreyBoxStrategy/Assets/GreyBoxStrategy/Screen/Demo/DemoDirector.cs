using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Plays a demo video's timeline (<see cref="DemoScript"/>) over a replay: the cursor glides to elements found by
    /// selector and clicks them for real, callouts point at what the framework did, the map pans. Demo only: it
    /// exists only once started, from the dev menu's <c>demo</c> command or the <c>-demo &lt;timeline&gt;</c> command-line
    /// flag, and every take of a timeline is the same, since the replay calls no model and every motion runs on game time.
    /// </summary>
    public sealed class DemoDirector : MonoBehaviour
    {
        /// <summary>The timelines, by name: <c>sample</c> is <c>Timelines/sample.demo.txt</c> in this folder.</summary>
        public const string TimelineFolder = "Assets/GreyBoxStrategy/Screen/Demo/Timelines";

        public static DemoDirector Active { get; private set; }
        /// <summary>Raised once when a timeline ends, with null or what went wrong; the capture stops on it.</summary>
        public static event Action<string> Finished;
        /// <summary>Raised once, at the timeline's <c>roll</c> step (or its first step when it has none): a capture starts here.</summary>
        public static event Action Rolling;
        public bool Rolled { get; private set; }

        public bool Done { get; private set; }
        public string Error { get; private set; }
        public string Status => Done ? (Error ?? "done") : step == null ? "starting" : $"line {step.Line}: {step.Verb} ({clock:0.0} s)";
        public float Clock => clock;

        private ReplayDirector screen;
        private DemoScript script;
        private DemoOverlay overlay;
        private DemoStep step;
        private float clock;

        /// <summary>The project folder, where a timeline's paths start.</summary>
        public static string ProjectFolder => Path.GetDirectoryName(Application.dataPath);

        /// <summary>A timeline's file: a name in <see cref="TimelineFolder"/>, or a path.</summary>
        public static string TimelinePath(string nameOrPath)
        {
            if (nameOrPath.Contains("/") || nameOrPath.Contains("\\") || nameOrPath.EndsWith(".txt", StringComparison.Ordinal))
                return Path.IsPathRooted(nameOrPath) ? nameOrPath : Path.Combine(ProjectFolder, nameOrPath);
            return Path.Combine(ProjectFolder, TimelineFolder, nameOrPath + ".demo.txt");
        }

        /// <summary>Starts a timeline on the screen. Returns what it will play, or why not.</summary>
        public static string Begin(ReplayDirector on, string timeline)
        {
            if (on == null) return "No screen to play on.";
            if (Active != null && !Active.Done) return "A demo is playing: " + Active.Status;
            string path = TimelinePath(timeline);
            if (!File.Exists(path)) return "No timeline at " + path;
            DemoScript s;
            try { s = DemoScript.Parse(File.ReadAllText(path)); }
            catch (DemoScriptException e) { return Path.GetFileName(path) + ", " + e.Message; }
            if (Active != null) Destroy(Active);
            var d = on.gameObject.AddComponent<DemoDirector>();
            d.screen = on; d.script = s;
            Active = d;
            return $"playing {Path.GetFileName(path)}: {s.Steps.Count} steps, {s.KnownSeconds:0.#} s of waits and titles";
        }

        /// <summary>The <c>-demo &lt;timeline&gt;</c> flag: starts it once the screen is up.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void FromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-demo");
            if (i < 0 || i + 1 >= args.Length) return;
            string timeline = args[i + 1];
            var host = new GameObject("demo-start").AddComponent<DemoStarter>();
            host.Timeline = timeline;
        }

        private sealed class DemoStarter : MonoBehaviour
        {
            public string Timeline;
            private IEnumerator Start()
            {
                while (DevTools.Director == null || DevTools.Director.Ui == null) yield return null;
                yield return null;
                Debug.Log("[demo] " + Begin(DevTools.Director, Timeline));
                Destroy(gameObject);
            }
        }

        private IEnumerator Start()
        {
            while (screen.UiRoot == null || screen.Ui == null) yield return null;
            overlay = new DemoOverlay(screen.UiRoot);
            // A timeline that fades in starts black, so a capture's first frame is not the start menu.
            var firstFade = script.Steps.FirstOrDefault(x => x.Verb == "fade");
            if (firstFade != null && firstFade.Arg(0) == "in") overlay.Black();
            bool hasRoll = script.Steps.Any(x => x.Verb == "roll");
            if (!hasRoll) Roll();
            foreach (var s in script.Steps)
            {
                step = s;
                IEnumerator run;
                try { run = Run(s); }
                catch (Exception e) { Fail(e); yield break; }
                if (run == null) continue; // a step that takes no time
                // A step's own coroutine, with any error it throws named by its line.
                while (true)
                {
                    bool more;
                    try { more = run.MoveNext(); }
                    catch (Exception e) { Fail(e); yield break; }
                    if (!more) break;
                    yield return run.Current;
                }
            }
            Done = true;
            Debug.Log($"[demo] done at {clock:0.00} s");
            Finished?.Invoke(null);
        }

        private void Roll()
        {
            if (Rolled) return;
            Rolled = true;
            clock = 0;
            Debug.Log("[demo] rolling");
            Rolling?.Invoke();
        }

        private void Fail(Exception e)
        {
            Error = $"line {step?.Line}: {step?.Verb}: {e.Message}";
            Done = true;
            Debug.LogError("[demo] " + Error);
            Finished?.Invoke(Error);
        }

        private void Update()
        {
            clock += Time.deltaTime;
            overlay?.Tick(Time.deltaTime);
        }

        private void OnDestroy() { overlay?.Root.RemoveFromHierarchy(); if (Active == this) Active = null; }

        // ---- the steps ----

        private IEnumerator Run(DemoStep s)
        {
            switch (s.Verb)
            {
                case "load": return Load(s.Arg(0));
                case "roll": Roll(); return null;
                case "ui-scale": screen.DemoUiScale(Num(s.Arg(0))); return null;
                case "speed": screen.SetSpeed(Num(s.Arg(0))); return null;
                case "turn": return ToTurn(int.Parse(s.Arg(0), CultureInfo.InvariantCulture));
                case "play": screen.DemoPlay(); return null;
                case "pause": screen.Pause(); return null;
                case "until": return Until(s, before: true);
                case "after": return Until(s, before: false);
                case "wait": return Wait(Num(s.Arg(0)));
                case "cursor": return Glide(Point(s.Arg(0)), s.Options.ContainsKey("time") ? s.Number("time", 0) : (float?)null);
                case "hide-cursor": overlay.CursorGoal = 0; return Wait(s.Number("time", 0));
                case "click": return Click(s);
                case "callout":
                {
                    var target = Target(s.Arg(0));
                    if (target() == null) throw new InvalidOperationException($"nothing on screen matches {s.Arg(0)}");
                    overlay.AddCallout(target, s.Arg(1), s.Arg(2), s.Options.TryGetValue("side", out var side) ? side : null, s.Number("width", 380));
                    return null;
                }
                case "dim":
                {
                    var target = Target(s.Arg(0));
                    if (target() == null) throw new InvalidOperationException($"nothing on screen matches {s.Arg(0)}");
                    overlay.Dim(target, s.Number("pad", 10));
                    return null;
                }
                case "clear": overlay.Clear(); return null;
                case "chapter": overlay.Chapter(s.Arg(0), s.Arg(1)); return null;
                case "clear-chapter": overlay.ClearChapter(); return null;
                case "title": return Title(s);
                case "map": return MapStep(s);
                case "inspect":
                {
                    string r = screen.DemoInspect(int.Parse(s.Arg(0), CultureInfo.InvariantCulture), s.Arg(1));
                    if (r.StartsWith("no decision", StringComparison.Ordinal)) throw new InvalidOperationException(r);
                    return null;
                }
                case "tab": if (!screen.Ui.ShowInspectorTab(s.Arg(0))) throw new InvalidOperationException($"the inspector has no tab \"{s.Arg(0)}\""); return null;
                case "dev": Debug.Log("[demo] dev " + s.Arg(0) + ": " + DevTools.Run(s.Arg(0))); return null;
                case "fade": overlay.Fade(s.Arg(0) == "out", s.Number("time", DemoDefaults.FadeSeconds)); return Wait(s.Number("time", DemoDefaults.FadeSeconds));
            }
            throw new InvalidOperationException("no such step: " + s.Verb);
        }

        private static float Num(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) yield return null;
        }

        private IEnumerator Load(string path)
        {
            string full = Path.IsPathRooted(path) ? path : Path.Combine(ProjectFolder, path);
            if (!File.Exists(full)) throw new FileNotFoundException("no recording at " + full);
            // The start menu builds its screen first.
            while (screen.Ui == null) yield return null;
            string r = screen.LoadForDemo(Recording.Parse(File.ReadAllText(full)));
            if (r.StartsWith("A game", StringComparison.Ordinal)) throw new InvalidOperationException(r + " Start the demo from the start menu.");
            Debug.Log("[demo] " + r);
            yield return null;
        }

        private IEnumerator ToTurn(int turn)
        {
            screen.SkipToTurn(turn);
            while (screen.CurrentTurn < turn && !screen.Ui.GameOver) yield return null;
            if (screen.CurrentTurn < turn) throw new InvalidOperationException($"the game ended before turn {turn}");
        }

        private IEnumerator Until(DemoStep s, bool before)
        {
            var kind = (ReplayEventKind)Enum.Parse(typeof(ReplayEventKind), s.Arg(0));
            string country = s.Arg(1);
            Func<ReplayEvent, bool> match = e => e.Kind == kind && (country == null || e.Country == country);
            if (before) screen.DemoHoldBefore = match; else screen.DemoHoldAfter = match;
            screen.DemoPlay();
            // Held once the hook has fired and playback waits at the next event (or the game is over).
            while ((before ? screen.DemoHoldBefore : screen.DemoHoldAfter) != null || !screen.AtGate && !screen.Ui.GameOver)
            {
                if (screen.Ui.GameOver) throw new InvalidOperationException($"the game ended before {(before ? "a" : "the end of a")} {kind}{(country != null ? " by " + country : "")}");
                yield return null;
            }
        }

        private IEnumerator Title(DemoStep s)
        {
            overlay.Title(s.Arg(0), s.Arg(1));
            yield return Wait(s.Number("time", DemoDefaults.TitleSeconds));
            overlay.HideTitle();
            while (overlay.TitleShown) yield return null;
        }

        private IEnumerator MapStep(DemoStep s)
        {
            string a = s.Arg(0);
            int province = a == "fit" ? -1 : a.StartsWith("@country:", StringComparison.Ordinal) ? screen.CapitalOf(a.Substring(9)) : screen.ProvinceIndexOf(a.Substring(10));
            if (a != "fit" && province < 0) throw new InvalidOperationException("no such place: " + a);
            yield return screen.DemoPan(province, s.Number("zoom", 1), s.Number("time", DemoDefaults.MapSeconds));
        }

        // ---- the cursor ----

        private IEnumerator Glide(Func<Vector2> to, float? seconds = null)
        {
            Vector2 goal = to();
            Vector2 from = overlay.CursorAt;
            // The first move fades the cursor in, coming from where it rests.
            overlay.CursorGoal = 1;
            float dist = Vector2.Distance(from, goal);
            float time = seconds ?? DemoDefaults.GlideSeconds(dist);
            if (dist < 1) { yield return Wait(Mathf.Min(time, 0.2f)); yield break; }
            // A slight arc, as a hand moves: a sideways bow of 6% of the way.
            var normal = new Vector2(-(goal - from).y, (goal - from).x).normalized * dist * 0.06f;
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                goal = to(); // the element may still be settling
                float e = DemoDefaults.Ease(t / time);
                overlay.CursorAt = Vector2.Lerp(from, goal, e) + normal * Mathf.Sin(Mathf.PI * e);
                yield return null;
            }
            overlay.CursorAt = to();
        }

        private IEnumerator Click(DemoStep s)
        {
            string target = s.Arg(0);
            VisualElement element = null;
            if (!target.StartsWith("@", StringComparison.Ordinal))
            {
                element = DemoTree.Find(screen.UiRoot, target) ?? throw new InvalidOperationException($"nothing on screen matches {target}");
                if (DemoTree.BringIntoView(element)) yield return Wait(0.15f);
            }
            yield return Glide(Point(target), s.Options.ContainsKey("time") ? s.Number("time", 0) : (float?)null);
            // Press: the cursor dips, a ripple spreads, the click lands halfway down.
            float press = DemoDefaults.PressSeconds;
            overlay.Ripple(overlay.CursorAt);
            bool clicked = false;
            for (float t = 0; t < press; t += Time.deltaTime)
            {
                float k = t / press;
                overlay.CursorPress = k < 0.35f ? k / 0.35f : 1 - (k - 0.35f) / 0.65f;
                if (!clicked && k >= 0.35f) { clicked = true; DoClick(target, element); }
                yield return null;
            }
            if (!clicked) DoClick(target, element);
            overlay.CursorPress = 0;
        }

        private void DoClick(string target, VisualElement element)
        {
            SoundManager.Play(SoundCues.PanelOpen);
            if (element != null) { DemoTree.Click(element); return; }
            if (target.StartsWith("@province:", StringComparison.Ordinal)) screen.OpenProvince(screen.ProvinceIndexOf(target.Substring(10)));
            else screen.OpenCountry(target.Substring(9));
        }

        // ---- targets ----

        /// <summary>Where a target is on the panel, each time it is asked (it may move); null when it is not on screen.</summary>
        private Func<Rect?> Target(string target)
        {
            if (target.StartsWith("@", StringComparison.Ordinal))
            {
                int p = target.StartsWith("@country:", StringComparison.Ordinal) ? screen.CapitalOf(target.Substring(9)) : screen.ProvinceIndexOf(target.Substring(10));
                if (p < 0) throw new InvalidOperationException("no such place: " + target);
                return () => { var c = screen.ProvinceOnPanel(p); return new Rect(c.x - 26, c.y - 18, 52, 36); };
            }
            var sel = DemoSelector.Parse(target);
            VisualElement found = null;
            return () =>
            {
                // Found once and followed; found again if it was rebuilt (the chronicle redraws on every line).
                if (found == null || found.panel == null) found = sel.First(screen.UiRoot, DemoTree.Shown);
                return found == null ? null : DemoTree.OnPanel(found);
            };
        }

        /// <summary>The point the cursor goes to on a target: a little left of its middle, so the arrow does not hide its words' start.</summary>
        private Func<Vector2> Point(string target)
        {
            var t = Target(target);
            if (t() == null) throw new InvalidOperationException($"nothing on screen matches {target}");
            Vector2 last = Vector2.zero;
            return () =>
            {
                var r = t();
                if (r.HasValue) last = new Vector2(r.Value.x + Mathf.Min(r.Value.width * 0.5f, 60 + r.Value.width * 0.15f), r.Value.y + r.Value.height * 0.55f);
                return last;
            };
        }
    }
}
