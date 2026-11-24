using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// A demo video's timeline: one step a line, played in order over a replay by the screen's demo director
    /// (<c>Screen/Demo/DemoDirector.cs</c>). Demo only: nothing in the game reads it unless a demo is started.
    /// <code>
    /// # a comment
    /// load Assets/GreyBoxStrategy/Screen/Replays/seed-0123-live-4b.json
    /// turn 4
    /// until Capture valen
    /// click ".chronicle .lg:has(.tag.land) .lg-link"
    /// callout ".inspector .code-box" "The reply, in order" "Action, then target, then statement." side=left
    /// wait 3.5
    /// </code>
    /// A word with spaces goes in double quotes (<c>\"</c> inside them is a quote); a trailing <c>key=value</c> is an
    /// option. A target is a selector (<see cref="DemoSelector"/>) or a map anchor: <c>@province:stonereach</c>,
    /// <c>@country:merrow</c> (its capital).
    /// </summary>
    public sealed class DemoScript
    {
        public readonly List<DemoStep> Steps = new List<DemoStep>();

        /// <summary>Seconds the steps that take a known time add up to; the replay's own pace is not counted.</summary>
        public float KnownSeconds => Steps.Sum(s => s.KnownSeconds);

        /// <summary>Every verb, with its positional arguments (min, max) and the options it takes.</summary>
        public static readonly IReadOnlyDictionary<string, (int min, int max, string[] options, string usage)> Verbs =
            new Dictionary<string, (int, int, string[], string)>(StringComparer.Ordinal)
            {
                ["load"] = (1, 1, new string[0], "load <recording path, from the project folder>"),
                ["roll"] = (0, 0, new string[0], "roll: a capture starts here; the steps before it set the scene unrecorded"),
                ["ui-scale"] = (1, 1, new string[0], "ui-scale <x>: the screen's UI x times its automatic size, for this take only (0.75 to 2)"),
                ["speed"] = (1, 1, new string[0], "speed <0.25 to 32>"),
                ["turn"] = (1, 1, new string[0], "turn <n>: jump to turn n's start without animating the turns before"),
                ["play"] = (0, 0, new string[0], "play"),
                ["pause"] = (0, 0, new string[0], "pause"),
                ["until"] = (1, 2, new string[0], "until <event kind> [country]: play, and hold before the next such event"),
                ["after"] = (1, 2, new string[0], "after <event kind> [country]: play, and hold once the next such event has landed"),
                ["wait"] = (1, 1, new string[0], "wait <seconds>"),
                ["cursor"] = (1, 1, new[] { "time" }, "cursor <target> [time=s]: glide the cursor there"),
                ["hide-cursor"] = (0, 0, new[] { "time" }, "hide-cursor [time=s]"),
                ["click"] = (1, 1, new[] { "time" }, "click <target> [time=s]: glide there, press, and do what a click does"),
                ["callout"] = (3, 3, new[] { "side", "width" }, "callout <target> \"title\" \"line\" [side=left|right|above|below] [width=px]"),
                ["dim"] = (1, 1, new[] { "pad" }, "dim <target> [pad=px]: darken everything but it"),
                ["clear"] = (0, 0, new string[0], "clear: callouts and the dim fade away"),
                ["chapter"] = (1, 2, new string[0], "chapter \"title\" [\"line\"]: the label in the corner, until the next or clear-chapter"),
                ["clear-chapter"] = (0, 0, new string[0], "clear-chapter"),
                ["title"] = (1, 2, new[] { "time" }, "title \"title\" [\"line\"] [time=s]: a card in the middle, held, then gone"),
                ["map"] = (1, 1, new[] { "zoom", "time" }, "map <@province:id|@country:id|fit> [zoom=x] [time=s]: pan (and zoom) the map"),
                ["inspect"] = (2, 2, new string[0], "inspect <turn> <country>: open a decision directly (no cursor)"),
                ["tab"] = (1, 1, new string[0], "tab <name>: show an inspector tab directly (no cursor)"),
                ["dev"] = (1, 1, new string[0], "dev \"<dev menu command>\""),
                ["fade"] = (1, 1, new[] { "time" }, "fade in|out [time=s]: from or to black"),
            };

        /// <summary>The event kinds <c>until</c> and <c>after</c> take, as <see cref="ReplayEventKind"/> names them.</summary>
        public static bool IsEventKind(string s) => Enum.TryParse<ReplayEventKind>(s, false, out var k) && Enum.IsDefined(typeof(ReplayEventKind), k) && !char.IsDigit(s[0]);

        /// <summary>Parses a timeline; throws <see cref="DemoScriptException"/> naming the line of the first mistake.</summary>
        public static DemoScript Parse(string text)
        {
            var script = new DemoScript();
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Trim();
                if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal)) continue;
                int n = i + 1;
                List<(string text, bool quoted)> words;
                try { words = Words(raw); }
                catch (FormatException e) { throw new DemoScriptException(n, e.Message); }
                string verb = words[0].text;
                if (!Verbs.TryGetValue(verb, out var spec)) throw new DemoScriptException(n, $"\"{verb}\" is not a step. Steps: {string.Join(", ", Verbs.Keys)}");
                var step = new DemoStep { Verb = verb, Line = n };
                foreach (var (w, quoted) in words.Skip(1))
                {
                    // An option is an unquoted key=value with a plain key; anything quoted is an argument.
                    int eq = w.IndexOf('=');
                    bool option = !quoted && eq > 0 && w.Substring(0, eq).All(char.IsLetter);
                    if (!option) { step.Args.Add(w); continue; }
                    string key = w.Substring(0, eq);
                    if (!spec.options.Contains(key)) throw new DemoScriptException(n, $"{verb} takes no option \"{key}\". {spec.usage}");
                    step.Options[key] = w.Substring(eq + 1);
                }
                if (step.Args.Count < spec.min || step.Args.Count > spec.max) throw new DemoScriptException(n, $"{verb} takes {Count(spec.min, spec.max)}, not {step.Args.Count}. {spec.usage}");
                string why = Check(step);
                if (why != null) throw new DemoScriptException(n, why);
                script.Steps.Add(step);
            }
            if (script.Steps.Count == 0) throw new DemoScriptException(0, "The timeline has no steps.");
            if (script.Steps[0].Verb != "load") throw new DemoScriptException(script.Steps[0].Line, "A timeline starts by loading a recording (load <path>), so every take plays the same game.");
            return script;
        }

        private static string Count(int min, int max) => min == max ? (min == 1 ? "1 argument" : $"{min} arguments") : $"{min} to {max} arguments";

        /// <summary>What is wrong with a step's arguments, or null.</summary>
        private static string Check(DemoStep s)
        {
            foreach (var kv in s.Options)
            {
                if (kv.Key == "side") { if (!new[] { "left", "right", "above", "below" }.Contains(kv.Value)) return $"side is left, right, above or below, not \"{kv.Value}\"."; }
                else if (!TryNumber(kv.Value, out float v) || v < 0) return $"{kv.Key} is a number of zero or more, not \"{kv.Value}\".";
            }
            switch (s.Verb)
            {
                case "ui-scale": return TryNumber(s.Args[0], out float z) && z >= 0.75f && z <= 2 ? null : "ui-scale is 0.75 to 2.";
                case "speed": return TryNumber(s.Args[0], out float x) && x >= 0.25f && x <= 32 ? null : "speed is 0.25 to 32.";
                case "turn": return int.TryParse(s.Args[0], NumberStyles.None, CultureInfo.InvariantCulture, out int t) && t >= 1 ? null : "turn is a turn number, 1 or more.";
                case "inspect": return int.TryParse(s.Args[0], NumberStyles.None, CultureInfo.InvariantCulture, out int u) && u >= 1 ? null : "inspect <turn> <country>: the turn is a number.";
                case "wait": return TryNumber(s.Args[0], out float w) && w >= 0 ? null : "wait takes seconds, zero or more.";
                case "until":
                case "after": return IsEventKind(s.Args[0]) ? null : $"\"{s.Args[0]}\" is not an event kind. Kinds: {string.Join(", ", Enum.GetNames(typeof(ReplayEventKind)))}";
                case "fade": return s.Args[0] == "in" || s.Args[0] == "out" ? null : "fade in, or fade out.";
                case "map": return s.Args[0] == "fit" || s.Args[0].StartsWith("@province:", StringComparison.Ordinal) || s.Args[0].StartsWith("@country:", StringComparison.Ordinal) ? null : "map takes @province:<id>, @country:<id> or fit.";
                case "cursor":
                case "click":
                case "callout":
                case "dim": return CheckTarget(s.Args[0]);
            }
            return null;
        }

        private static string CheckTarget(string target)
        {
            if (target.StartsWith("@", StringComparison.Ordinal))
                return target.StartsWith("@province:", StringComparison.Ordinal) && target.Length > 10 || target.StartsWith("@country:", StringComparison.Ordinal) && target.Length > 9
                    ? null : $"\"{target}\": a map anchor is @province:<id> or @country:<id>.";
            try { DemoSelector.Parse(target); return null; }
            catch (FormatException e) { return e.Message; }
        }

        public static bool TryNumber(string s, out float v) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        /// <summary>A line's words: spaces separate them, double quotes keep one together (<c>\"</c> is a quote inside).</summary>
        public static List<(string text, bool quoted)> Words(string line)
        {
            var words = new List<(string, bool)>();
            var w = new StringBuilder();
            bool quoted = false, any = false, wasQuoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '\\' && i + 1 < line.Length && line[i + 1] == '"') { w.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else w.Append(c);
                }
                else if (c == '"') { quoted = true; any = true; wasQuoted = true; }
                else if (char.IsWhiteSpace(c)) { if (any) words.Add((w.ToString(), wasQuoted)); w.Clear(); any = false; wasQuoted = false; }
                else { w.Append(c); any = true; }
            }
            if (quoted) throw new FormatException("A quote is not closed.");
            if (any) words.Add((w.ToString(), wasQuoted));
            return words;
        }
    }

    /// <summary>One step of a <see cref="DemoScript"/>.</summary>
    public sealed class DemoStep
    {
        public string Verb;
        public readonly List<string> Args = new List<string>();
        public readonly Dictionary<string, string> Options = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>The line it was written on, for messages.</summary>
        public int Line;

        public string Arg(int i) => i < Args.Count ? Args[i] : null;

        public float Number(string option, float fallback) => Options.TryGetValue(option, out var s) && DemoScript.TryNumber(s, out float v) ? v : fallback;

        /// <summary>The time a step takes by itself, when its own words say: a wait, a title, a fade.</summary>
        public float KnownSeconds => Verb == "wait" && DemoScript.TryNumber(Args[0], out float w) ? w
            : Verb == "title" ? Number("time", DemoDefaults.TitleSeconds)
            : Verb == "fade" ? Number("time", DemoDefaults.FadeSeconds) : 0;

        public override string ToString() => $"{Line}: {Verb} {string.Join(" ", Args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a))}";
    }

    /// <summary>The timings a step takes when its line gives none.</summary>
    public static class DemoDefaults
    {
        public const float TitleSeconds = 3.2f, FadeSeconds = 0.6f, MapSeconds = 1.1f, PressSeconds = 0.32f, CalloutIn = 0.35f, DimIn = 0.4f;

        /// <summary>The cursor's glide: longer for a longer way, never rushed and never slow.</summary>
        public static float GlideSeconds(float distance) => Math.Max(0.55f, Math.Min(1.15f, 0.42f + distance / 1500f));

        /// <summary>Ease in and out (cubic), 0 to 1.</summary>
        public static float Ease(float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t < 0.5f ? 4 * t * t * t : 1 - (float)Math.Pow(-2 * t + 2, 3) / 2;
        }
    }

    public sealed class DemoScriptException : Exception
    {
        public readonly int Line;
        public DemoScriptException(int line, string message) : base(line > 0 ? $"Line {line}: {message}" : message) { Line = line; }
    }
}
