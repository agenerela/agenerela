using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Agenerela;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Demos.GreyBoxVillage.Lab
{
    /// <summary>The run's conditions, as run.json writes them.</summary>
    [Serializable]
    public sealed class LabRunInfo
    {
        public string started = "";
        public string finished = "";
        public string frameworkVersion = "";
        public string unityVersion = "";
        public string gpu = "";
        public string server = "";
        public string ollamaVersion = "";
        public string order = "";
        public int repeats;
        public int seed;
        public float timeScale;
        public int scenarios;
        public string scenariosCommit = "";
        public bool scenariosUncommitted;
        public List<LabArmInfo> arms = new List<LabArmInfo>();
        public List<LabModelInfo> models = new List<LabModelInfo>();
        public List<LabResidency> residency = new List<LabResidency>();

        public static void Fill(LabRunInfo info, LabBench.Settings settings)
        {
            info.started = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            info.frameworkVersion = AgenerelaInfo.Version;
            info.unityVersion = Application.unityVersion;
            info.gpu = SystemInfo.graphicsDeviceName + ", " + SystemInfo.graphicsMemorySize + " MB";
            info.server = settings.Server == null ? "scripted (no model)"
                : settings.Server.Name == "5080" ? "5080: RTX 5080, Docker Ollama, over the network"
                : "local: this machine's Ollama at 127.0.0.1";
            info.order = settings.Interleaved ? "interleaved" : "sequential";
            info.repeats = settings.Repeats;
            info.seed = settings.Seed;
            info.timeScale = settings.TimeScale;
            info.scenarios = settings.Scenarios.Count;
            string folder = Path.GetFullPath(LabFiles.Folder);
            info.scenariosCommit = Git(folder, "log -1 --format=%H -- .") ?? "";
            info.scenariosUncommitted = !string.IsNullOrEmpty(Git(folder, "status --porcelain -- ."));
            foreach (var arm in settings.Arms)
                info.arms.Add(new LabArmInfo
                {
                    name = arm.Name, variant = arm.Variant.name, description = arm.Variant.description, model = arm.Model ?? "scripted",
                    contextSize = arm.ContextSize,
                    temperature = arm.Config is Agenerela.Providers.OllamaProviderConfig o ? o.Temperature : 0,
                    variantJson = JsonUtility.ToJson(arm.Variant),
                });
        }

        private static string Git(string folder, string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", "-C \"" + folder + "\" " + arguments)
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using (var git = Process.Start(start))
                {
                    string output = git.StandardOutput.ReadToEnd();
                    return git.WaitForExit(5000) && git.ExitCode == 0 ? output.Trim() : null;
                }
            }
            catch (Exception) { return null; }
        }
    }

    [Serializable]
    public sealed class LabArmInfo
    {
        public string name = "", variant = "", description = "", model = "";
        public int contextSize;
        public float temperature;
        public string variantJson = "";
    }

    [Serializable]
    public sealed class LabModelInfo
    {
        public string name = "", digest = "", family = "", parameterSize = "", quantization = "";
    }

    [Serializable]
    public sealed class LabResidency
    {
        public string when = "", arm = "";
        public List<LabResident> models = new List<LabResident>();
    }

    [Serializable]
    public sealed class LabResident
    {
        public string name = "";
        public long size, sizeVram;
        public int contextLength;
    }

    /// <summary>Writes a run's report: run.json, scores.jsonl, report.md and statements.md (decisions.jsonl is the framework's DecisionLog).</summary>
    public static class LabReport
    {
        public static void Write(LabBench.Settings settings, LabBench.Run run)
        {
            Directory.CreateDirectory(settings.Out);
            File.WriteAllText(Path.Combine(settings.Out, "run.json"), JsonUtility.ToJson(run.Info, true));
            File.WriteAllLines(Path.Combine(settings.Out, "scores.jsonl"), run.Results.Select(r => JsonUtility.ToJson(r)));
            File.WriteAllText(Path.Combine(settings.Out, "report.md"), Markdown(settings, run));
            File.WriteAllText(Path.Combine(settings.Out, "statements.md"), Statements(settings, run));
            Debug.Log("[LabBench] report written to " + settings.Out);
        }

        private static string Pct(int part, int whole) => whole == 0 ? "-" : (100.0 * part / whole).ToString("0", CultureInfo.InvariantCulture) + "%";
        private static string N(double v, string format = "0.00") => double.IsNaN(v) ? "-" : v.ToString(format, CultureInfo.InvariantCulture);
        private static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "");

        public static string Markdown(LabBench.Settings settings, LabBench.Run run)
        {
            var info = run.Info;
            var turns = new Dictionary<string, LabTurn>();
            foreach (var s in settings.Scenarios)
                for (int i = 0; i < s.turns.Count; i++) turns[s.id + "#" + (i + 1)] = s.turns[i];
            var summaries = settings.Arms.Select(a => LabArmSummary.Of(a.Name, run.Results, turns)).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("# GreyBoxVillage bench run");
            sb.AppendLine();
            sb.AppendLine("**One run is an anecdote until it is an A/B run with enough turns (hard rule 2).** A difference smaller than the noise floor says nothing.");
            sb.AppendLine();
            sb.AppendLine("## Conditions");
            sb.AppendLine();
            sb.AppendLine("- Started " + info.started + ", finished " + info.finished + "; framework " + info.frameworkVersion + ", Unity " + info.unityVersion + ".");
            sb.AppendLine("- Server: " + info.server + (string.IsNullOrEmpty(info.ollamaVersion) ? "" : ", Ollama " + info.ollamaVersion) + ". GPU seen by Unity: " + info.gpu + ".");
            sb.AppendLine("- " + info.scenarios + " scenarios, " + info.repeats + " repeat(s), " + info.order + ", seed " + info.seed + ", time scale " + N(info.timeScale, "0.#") +
                          ". Scenarios at " + (string.IsNullOrEmpty(info.scenariosCommit) ? "an unknown commit" : info.scenariosCommit.Substring(0, Math.Min(10, info.scenariosCommit.Length))) +
                          (info.scenariosUncommitted ? ", with uncommitted changes" : "") + ".");
            if (!settings.Interleaved) sb.AppendLine("- The arms ran one after the other, so drift over the session is not shared between them.");
            else if (settings.Arms.Select(a => a.Model).Distinct().Count() > 1)
                sb.AppendLine("- Interleaved over two models: each switch unloaded the other model and warmed this one up before anything was timed (hard rule 3).");
            foreach (var m in info.models)
                sb.AppendLine("- Model `" + m.name + "`: " + m.parameterSize + " " + m.family + ", " + m.quantization + ", digest " + (m.digest.Length > 12 ? m.digest.Substring(0, 12) : m.digest) + ".");
            foreach (var a in info.arms)
                sb.AppendLine("- Arm **" + a.name + "**: variant `" + a.variant + "`" + (string.IsNullOrEmpty(a.description) ? "" : " (" + a.description + ")") + ", " + a.model +
                              ", num_ctx " + a.contextSize + ", temperature " + N(a.temperature, "0.##") + ".");
            sb.AppendLine();

            sb.AppendLine("## Per arm");
            sb.AppendLine();
            sb.AppendLine("Accuracy is the model's own answer against the scenario's acceptable pairs, whatever a guard then made of it. Primers (a memory probe's first turn) are asked but never scored.");
            sb.AppendLine();
            sb.AppendLine("| Arm | Scored | Exact | Exact or weaker | Action alone | `none` when right | Acted on small talk | Bait caught (model + guard) | Must-not asked / ran | Errors | p50 | p95 | Queue | Tokens in / out |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var s in summaries)
                sb.AppendLine("| " + s.Arm + " | " + s.Scored + " | " + s.Exact + " (" + Pct(s.Exact, s.Scored) + ") | " + Pct(s.Exact + s.Weaker, s.Scored) + " | " + Pct(s.ActionRight, s.Scored) +
                              " | " + s.NoneAnswered + "/" + s.NoneRight + " | " + s.ChatterActed + "/" + s.Chatter + " | " + (s.BaitByModel + s.BaitByGuard) + "/" + s.Bait + " (" + s.BaitByModel + " + " + s.BaitByGuard + ")" +
                              " | " + s.MustNotAsked + " / " + s.MustNotRan + " | " + s.Errors + " | " + N(s.P50) + " s | " + N(s.P95) + " s | " + N(s.MeanQueue) + " s | " + N(s.MeanPromptTokens, "0") + " / " + N(s.MeanCompletionTokens, "0") + " |");
            sb.AppendLine();
            foreach (var s in summaries.Where(x => x.PrimersMissed > 0))
                sb.AppendLine("- " + s.Arm + ": " + s.PrimersMissed + " scored turn(s) came after a primer that was not answered acceptably; on the rest, " + s.ExactAfterPrimer + "/" + s.ScoredAfterPrimer + " exact.");

            sb.AppendLine();
            sb.AppendLine("### By kind and by agent");
            sb.AppendLine();
            var groups = new List<(string name, Func<LabTurnResult, string> key)> { ("kind", r => r.kind), ("agent", r => r.agent), ("difficulty", r => r.difficulty) };
            foreach (var (name, key) in groups)
            {
                var values = run.Results.Where(r => !r.primer).Select(key).Distinct().OrderBy(v => v).ToList();
                sb.AppendLine("| " + name + " | " + string.Join(" | ", settings.Arms.Select(a => a.Name)) + " |");
                sb.AppendLine("|---|" + string.Concat(settings.Arms.Select(_ => "---|")));
                foreach (var v in values)
                    sb.AppendLine("| " + v + " | " + string.Join(" | ", settings.Arms.Select(a =>
                    {
                        var rs = run.Results.Where(r => r.arm == a.Name && !r.primer && key(r) == v).ToList();
                        return rs.Count(r => r.Exact) + "/" + rs.Count;
                    })) + " |");
                sb.AppendLine();
            }

            sb.AppendLine("### Time per decision");
            sb.AppendLine();
            foreach (var a in settings.Arms)
            {
                var rs = run.Results.Where(r => r.arm == a.Name && r.latencySeconds > 0).ToList();
                if (rs.Count == 0) continue;
                double wall = rs.Average(r => r.wallSeconds);
                sb.AppendLine("- " + a.Name + ": " + rs.Count + " decisions, model latency mean " + N(rs.Average(r => r.latencySeconds)) + " s, p50 " + N(LabScore.Percentile(rs.Select(r => r.latencySeconds), 0.5)) +
                              " s, p95 " + N(LabScore.Percentile(rs.Select(r => r.latencySeconds), 0.95)) + " s, slowest " + N(rs.Max(r => r.latencySeconds)) + " s; asked to answered, mean " + N(wall) + " s.");
            }
            int all = run.Results.Count;
            if (all > 0 && DateTime.TryParse(info.started, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var s0)
                && DateTime.TryParse(info.finished, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var s1))
            {
                double seconds = (s1 - s0).TotalSeconds;
                sb.AppendLine("- The whole run: " + N(seconds, "0") + " s for " + all + " asked turns, " + N(seconds / all, "0.0") + " s a turn with resets, setups, warm-ups and model switches; about " +
                              N(3600 * all / Math.Max(1, seconds), "0") + " turns an hour like these.");
            }
            sb.AppendLine();

            sb.AppendLine("### VRAM");
            sb.AppendLine();
            // The same reading after every swap of an interleaved run is said once, with how often.
            var readings = info.residency.Select(reading => reading.when + (string.IsNullOrEmpty(reading.arm) ? "" : " (" + reading.arm + ")") + ": " +
                (reading.models.Count == 0 ? "nothing resident" : string.Join("; ", reading.models.Select(m => m.name + " " + Mb(m.size) + " MB, " + Mb(m.sizeVram) + " MB in VRAM, context " + m.contextLength))));
            foreach (var g in readings.GroupBy(r => r))
                sb.AppendLine("- " + g.Key + (g.Count() > 1 ? ", " + g.Count() + " times" : "") + ".");
            sb.AppendLine();

            sb.AppendLine("## Per pair of arms");
            sb.AppendLine();
            sb.AppendLine("Exact accuracy, turn by turn over the turns both arms scored. p is the exact two-sided McNemar (sign) test on the turns only one arm got right; the noise floor is the framework's estimate at this n, " + Agenerela.Evaluation.NoiseCheck.Formula + ".");
            sb.AppendLine();
            sb.AppendLine("| A | B | Paired | Both right | Both wrong | Only A | Only B | B - A | Noise floor | p | Reads as |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            var pairs = new List<LabPairComparison>();
            for (int i = 0; i < settings.Arms.Count; i++)
                for (int j = i + 1; j < settings.Arms.Count; j++)
                    pairs.Add(LabPairComparison.Of(settings.Arms[i].Name, settings.Arms[j].Name, run.Results));
            foreach (var c in pairs)
            {
                bool self = c.A.Split('~')[0] == c.B.Split('~')[0];
                string reads = self ? "a variant against itself: the run's own noise"
                    : Math.Abs(c.DifferencePoints) < c.NoiseFloorPoints ? "within noise: no result"
                    : c.P >= 0.05 ? "beyond the floor, but p ≥ 0.05: not shown"
                    : "beyond noise";
                sb.AppendLine("| " + c.A + " | " + c.B + " | " + c.Paired + " | " + c.BothRight + " | " + c.BothWrong + " | " + c.OnlyA + " | " + c.OnlyB + " | " +
                              N(c.DifferencePoints, "+0;-0;0") + " pts | " + N(c.NoiseFloorPoints, "0") + " pts | " + N(c.P, "0.000") + " | " + reads + " |");
            }
            sb.AppendLine();
            foreach (var c in pairs.Where(p => p.A.Split('~')[0] == p.B.Split('~')[0]))
            {
                var ra = run.Results.Where(r => r.arm == c.A && !r.primer).ToDictionary(r => r.Key);
                var rb = run.Results.Where(r => r.arm == c.B && !r.primer).ToDictionary(r => r.Key);
                int changed = ra.Keys.Intersect(rb.Keys).Count(k => ra[k].modelAction != rb[k].modelAction || ra[k].modelTarget != rb[k].modelTarget);
                sb.AppendLine("- **Noise (hard rule 2):** " + c.A + " and " + c.B + " are one variant run twice. They chose a different action or target on " + changed + " of " + c.Paired +
                              " turns, and differed in exactness on " + (c.OnlyA + c.OnlyB) + ". " + (changed == 0 ? "At this temperature the run agreed with itself." : "That is the floor any difference between variants must clear."));
            }
            foreach (var c in pairs.Where(p => p.Flips.Count > 0))
                sb.AppendLine("- " + c.A + " vs " + c.B + ", the turns that differ: " + string.Join("; ", c.Flips.Take(30)) + (c.Flips.Count > 30 ? "; ..." : "") + ".");
            sb.AppendLine();

            if (run.Leaks.Count > 0 || run.Notes.Count > 0)
            {
                sb.AppendLine("## Notes");
                sb.AppendLine();
                foreach (var leak in run.Leaks) sb.AppendLine("- **State left behind:** " + leak);
                foreach (var note in run.Notes) sb.AppendLine("- " + note);
                sb.AppendLine();
            }

            sb.AppendLine("## Every turn");
            sb.AppendLine();
            sb.AppendLine("| Scenario | Turn | Arm | Asked | Accept | Chose | Outcome | Verdict | Latency |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var r in run.Results.OrderBy(r => r.scenario, StringComparer.Ordinal).ThenBy(r => r.turn).ThenBy(r => r.repeat).ThenBy(r => r.arm, StringComparer.Ordinal))
            {
                turns.TryGetValue(r.scenario + "#" + r.turn, out var t);
                sb.AppendLine("| " + r.scenario + (r.repeat > 1 ? " r" + r.repeat : "") + " | " + r.turn + (r.primer ? " (primer)" : "") + " | " + r.arm + " | " + Cell(r.label + ": " + r.stimulus) +
                              " | " + Cell(t == null ? "" : string.Join(", ", t.accept)) + " | `" + r.modelAction + " " + r.modelTarget + "` | " + r.outcome + (string.IsNullOrEmpty(r.guard) ? "" : " (" + r.guard + ")") +
                              " | " + r.verdict + (r.mustNotAsked ? ", MUST-NOT" : "") + (r.caughtBy != "" ? ", caught by " + r.caughtBy : "") + " | " + N(r.latencySeconds) + " s |");
            }
            return sb.ToString();
        }

        private static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0", CultureInfo.InvariantCulture);

        /// <summary>Each scenario's lines, every arm's side by side, for a person to judge voice, context and invention.</summary>
        public static string Statements(LabBench.Settings settings, LabBench.Run run)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Statements, side by side");
            sb.AppendLine();
            sb.AppendLine("What each arm's agent said (the model's statement; after a guard, the refusal line it said instead is in brackets). Judge voice, context and invention; a statement is never scored.");
            sb.AppendLine();
            foreach (var s in settings.Scenarios)
            {
                var rows = run.Results.Where(r => r.scenario == s.id).ToList();
                if (rows.Count == 0) continue;
                sb.AppendLine("## " + s.id + " (" + s.agent + ", " + s.kind + ")");
                sb.AppendLine();
                sb.AppendLine(s.purpose);
                sb.AppendLine();
                sb.AppendLine("| Turn | Asked | " + string.Join(" | ", settings.Arms.Select(a => a.Name)) + " |");
                sb.AppendLine("|---|---|" + string.Concat(settings.Arms.Select(_ => "---|")));
                foreach (var g in rows.GroupBy(r => (r.turn, r.repeat)).OrderBy(g => g.Key.turn).ThenBy(g => g.Key.repeat))
                {
                    var first = g.First();
                    sb.AppendLine("| " + g.Key.turn + (g.Key.repeat > 1 ? " r" + g.Key.repeat : "") + " | " + Cell(first.label + ": " + first.stimulus) + " | " + string.Join(" | ", settings.Arms.Select(a =>
                    {
                        var r = g.FirstOrDefault(x => x.arm == a.Name);
                        if (r == null) return "";
                        string line = Cell(r.statement);
                        if (r.outcome == "contained" || r.outcome == "refused") line += " [" + (string.IsNullOrEmpty(r.said) ? "said nothing" : Cell(r.said)) + "]";
                        return "`" + r.modelAction + " " + r.modelTarget + "` " + line;
                    })) + " |");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
