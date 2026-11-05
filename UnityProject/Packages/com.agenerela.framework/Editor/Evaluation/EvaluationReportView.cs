using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Agenerela.Evaluation;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// Everything the Evaluation Report window shows about one run (#146), in screen 4's order and
    /// words: the conditions, the five outcomes per arm, the noise check, accuracy per category, the
    /// answers that were not correct, and a summary to paste into findings.md.
    /// </summary>
    /// <remarks>
    /// Built from the fields <see cref="EvalReport.ToJson"/> writes and nothing else, never from
    /// <see cref="EvalAnswer.Result"/>, so a report fresh from a run and the same report read back
    /// from <c>report.json</c> show the same thing.
    /// </remarks>
    internal sealed class EvaluationReportView
    {
        /// <summary>The five outcomes in screen 4's order.</summary>
        internal static readonly DecisionOutcome[] Outcomes =
        {
            DecisionOutcome.Correct, DecisionOutcome.WrongLegalAction, DecisionOutcome.ContainedByGuard,
            DecisionOutcome.RejectedWhenActionExpected, DecisionOutcome.PipelineError,
        };

        internal EvaluationReportView(EvalReport report)
        {
            Report = report ?? throw new ArgumentNullException(nameof(report));
            ArmA = ArmAt(0);
            ArmB = ArmAt(1);
        }

        internal EvalReport Report { get; }

        internal ArmView ArmA { get; }
        internal ArmView ArmB { get; }

        /// <summary>One arm: its name, a line on what it ran with, and its overall counts.</summary>
        internal sealed class ArmView
        {
            internal string Name;
            internal string Subtitle;
            internal OutcomeCounts Counts;
        }

        private ArmView ArmAt(int i)
        {
            var result = i < Report.Arms.Count ? Report.Arms[i] : null;
            var conditions = i < Report.Conditions.Arms.Count ? Report.Conditions.Arms[i] : null;
            string name = result?.Name ?? conditions?.Name ?? (i == 0 ? "A" : "B");
            string what = conditions == null ? "" : conditions.Model ?? conditions.ProviderConfig ?? conditions.Provider ?? "";
            string guard = conditions == null ? "" : conditions.CheckTarget ? "grounding guard on" : "grounding guard off";
            return new ArmView
            {
                Name = name,
                Subtitle = string.Join(" · ", new[] { what, guard }.Where(s => s.Length > 0)),
                Counts = result?.Overall ?? new OutcomeCounts(),
            };
        }

        // ---- ① The run ---------------------------------------------------------------------------

        /// <summary>The line beside the set-up: when, how many prompts, how long.</summary>
        internal string RunLine
        {
            get
            {
                var c = Report.Conditions;
                var parts = new List<string>();
                if (TryParse(c.StartedUtc, out var started))
                {
                    parts.Add("Run " + started.ToLocalTime().ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture));
                }

                parts.Add($"{c.Prompts} prompts × 2 arms");
                parts.Add(c.Order == EvalArmOrder.Interleaved ? "interleaved" : "one after the other");
                if (Report.Cancelled)
                {
                    parts.Add("cancelled");
                }

                string duration = Duration;
                if (duration != null)
                {
                    parts.Add(duration);
                }

                return string.Join(" · ", parts);
            }
        }

        /// <summary>How long the run took, such as <c>9 min 40 s</c>; null when it did not finish.</summary>
        internal string Duration
        {
            get
            {
                if (!TryParse(Report.Conditions.StartedUtc, out var started) || !TryParse(Report.Conditions.FinishedUtc, out var finished))
                {
                    return null;
                }

                var span = finished - started;
                return span.TotalMinutes >= 1
                    ? $"{(int)span.TotalMinutes} min {span.Seconds} s"
                    : $"{Math.Max(0, (int)span.TotalSeconds)} s";
            }
        }

        private static bool TryParse(string iso, out DateTime utc)
        {
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
        }

        /// <summary>
        /// What the window says above a cancelled run's report; null for a run that finished.
        /// </summary>
        internal string CancelledLine => !Report.Cancelled ? null
            : $"Cancelled after {Report.Answers.Count} of {Report.Conditions.Prompts * 2} answers. The set was not asked whole, so nothing here is an accuracy result.";

        /// <summary>Everything that did not hold as it should, as the run recorded it.</summary>
        internal IReadOnlyList<string> Warnings => Report.Conditions.Warnings;

        /// <summary>
        /// The conditions the numbers depend on, in plain words: the order, then per arm its model,
        /// version, warm-up and what was loaded in GPU memory, then the set, the profile and the machine.
        /// </summary>
        internal IReadOnlyList<string> ConditionLines()
        {
            var c = Report.Conditions;
            var lines = new List<string>
            {
                c.Order == EvalArmOrder.Interleaved
                    ? "Interleaved: each prompt was asked in arm A, then in arm B, so drift over the session hit both alike."
                    : "One after the other: all of arm A, its model unloaded, then all of arm B. Drift over the session was not shared between them, so repeat a close result.",
            };

            for (int i = 0; i < c.Arms.Count; i++)
            {
                var arm = c.Arms[i];
                string letter = i == 0 ? "A" : i == 1 ? "B" : (i + 1).ToString(CultureInfo.InvariantCulture);
                var said = new List<string> { arm.ProviderConfig ?? arm.Provider ?? "no config" };
                if (!string.IsNullOrEmpty(arm.Model)) said.Add(arm.Model);
                said.Add(arm.ProviderVersion == null || arm.ProviderVersion == "not checked"
                    ? "provider version not checked"
                    : $"{Capital(arm.Provider)} {arm.ProviderVersion}");
                said.Add(arm.CheckTarget ? "grounding guard on" : "grounding guard off");
                said.Add($"{arm.WarmUpRequests} warm-up request{(arm.WarmUpRequests == 1 ? "" : "s")}");
                lines.Add($"Arm {letter}, {arm.Name}: {string.Join(", ", said)}.");

                foreach (var reading in arm.Residency)
                {
                    lines.Add($"    {Capital(reading.When)}: {Resident(reading, arm.Model)}");
                }

                if (arm.Unloaded.HasValue)
                {
                    lines.Add(arm.Unloaded.Value ? "    Unloaded after the arm, before the next began." : "    Still loaded after it was asked to unload.");
                }
            }

            lines.Add(c.SetCommit == null
                ? $"Set: {c.SetPath} ({c.SetAgent}), not in git, so the exact prompts cannot be recovered later."
                : $"Set: {c.SetPath} ({c.SetAgent}), last changed in commit {Short(c.SetCommit)}"
                  + (c.SetUncommitted == true ? ", plus changes not yet committed, so the commit alone does not recover it." : "."));
            lines.Add($"Profile: {c.Profile}. Framework {c.FrameworkVersion}.");
            lines.Add($"Graphics device, as Unity reports it: {c.GraphicsDevice}, {c.GraphicsMemoryMB} MB. Not necessarily the one the model ran on.");
            if (!string.IsNullOrEmpty(c.State)) lines.Add("State: " + c.State);
            if (!string.IsNullOrEmpty(c.Targets)) lines.Add("Targets: " + c.Targets);
            return lines;
        }

        // One residency reading in words: who was loaded and how much of the arm's model was in VRAM.
        internal static string Resident(ResidencyReading reading, string model)
        {
            if (!reading.Checked)
            {
                return "not checked: this provider cannot say which models are loaded.";
            }

            if (reading.Models.Count == 0)
            {
                return "no model loaded.";
            }

            var parts = reading.Models.Select(m =>
            {
                string vram = m.FullyOnGpu
                    ? $"fully on the GPU, {Megabytes(m.SizeVram)} of {Megabytes(m.Size)} MB"
                    : $"only {Megabytes(m.SizeVram)} of {Megabytes(m.Size)} MB on the GPU, so its latencies are not comparable";
                return $"{m.Name} {vram}";
            }).ToList();

            bool others = model != null && reading.Models.Any(m => !EvaluationRunSetup.SameModel(m.Name, model));
            return string.Join("; ", parts) + (others ? ". More than one model was loaded (hard rule 3)." : ".");
        }

        // ---- ② Five outcomes --------------------------------------------------------------------

        /// <summary>One outcome's row: its share in each arm and the difference, in points.</summary>
        internal sealed class OutcomeRow
        {
            internal DecisionOutcome Outcome;
            internal string Name;
            internal string Meaning;
            internal int CountA, CountB;
            internal double ShareA, ShareB;

            /// <summary>B minus A, in points.</summary>
            internal double DifferencePoints;

            /// <summary>The noise check's verdict, on the correct row only: the check is on accuracy.</summary>
            internal string Noise;
        }

        /// <summary>Screen 4's name for an outcome.</summary>
        internal static string NameOf(DecisionOutcome outcome)
        {
            switch (outcome)
            {
                case DecisionOutcome.Correct: return "Correct";
                case DecisionOutcome.WrongLegalAction: return "Wrong but legal";
                case DecisionOutcome.ContainedByGuard: return "Contained by a guard";
                case DecisionOutcome.RejectedWhenActionExpected: return "Rejected when an action was expected";
                case DecisionOutcome.PipelineError: return "Pipeline error";
                default: return outcome.ToString();
            }
        }

        /// <summary>What an outcome means, in a few words; "" for correct.</summary>
        internal static string MeaningOf(DecisionOutcome outcome)
        {
            switch (outcome)
            {
                case DecisionOutcome.WrongLegalAction: return "the agent visibly misbehaves";
                case DecisionOutcome.ContainedByGuard: return "the safety layer working";
                case DecisionOutcome.RejectedWhenActionExpected: return "a good request refused";
                case DecisionOutcome.PipelineError: return "no decision; not a semantic result";
                default: return "";
            }
        }

        /// <summary>The five rows, correct first.</summary>
        internal IReadOnlyList<OutcomeRow> OutcomeRows()
        {
            var all = Overall;
            return Outcomes.Select(o =>
            {
                double a = Share(ArmA.Counts, o), b = Share(ArmB.Counts, o);
                return new OutcomeRow
                {
                    Outcome = o,
                    Name = NameOf(o),
                    Meaning = MeaningOf(o),
                    CountA = ArmA.Counts[o],
                    CountB = ArmB.Counts[o],
                    ShareA = a,
                    ShareB = b,
                    DifferencePoints = (b - a) * 100,
                    Noise = o == DecisionOutcome.Correct && all != null ? (all.WithinNoise ? "within noise" : "beyond noise") : null,
                };
            }).ToList();
        }

        /// <summary>The share of <paramref name="counts"/> that is <paramref name="outcome"/>, from 0 to 1.</summary>
        internal static double Share(OutcomeCounts counts, DecisionOutcome outcome)
        {
            return counts == null || counts.Total == 0 ? 0 : (double)counts[outcome] / counts.Total;
        }

        // ---- ③ The noise check ------------------------------------------------------------------

        /// <summary>The whole set's noise check; null when the report has none.</summary>
        internal NoiseCheck Overall => Report.Noise.FirstOrDefault(n => n.Scope == "all");

        /// <summary>The noise check in one sentence, so nobody reports an improvement that isn't real.</summary>
        internal string NoiseLine
        {
            get
            {
                var all = Overall;
                if (all == null)
                {
                    return Report.Cancelled
                        ? "No noise check: the run was cancelled before both arms had been asked the same prompts."
                        : "No noise check: the report has none.";
                }

                string difference = $"Arm B's accuracy minus arm A's is {Signed(all.DifferencePoints)} points";
                string floor = $"the noise floor at {all.N} prompts is {Points(all.NoiseFloorPoints)}";
                return all.WithinNoise
                    ? $"{difference}, and {floor}: a difference this small could be chance, so it is no result."
                    : $"{difference}, beyond {floor}: larger than the drift between identical runs.";
            }
        }

        // ---- ④ Per category ---------------------------------------------------------------------

        /// <summary>One category's accuracy in each arm.</summary>
        internal sealed class CategoryRow
        {
            internal string Id;
            internal int N;
            internal double AccuracyA, AccuracyB;
            internal double DifferencePoints;
            internal double NoiseFloorPoints;
            internal bool WithinNoise;
        }

        /// <summary>Each category in the set's order, with its own noise check.</summary>
        internal IReadOnlyList<CategoryRow> CategoryRows()
        {
            return Report.Noise.Where(n => n.Scope != "all").Select(n => new CategoryRow
            {
                Id = n.Scope,
                N = n.N,
                AccuracyA = n.AccuracyA,
                AccuracyB = n.AccuracyB,
                DifferencePoints = n.DifferencePoints,
                NoiseFloorPoints = n.NoiseFloorPoints,
                WithinNoise = n.WithinNoise,
            }).ToList();
        }

        // ---- ⑤ The failure list -----------------------------------------------------------------

        /// <summary>
        /// The answers in arm <paramref name="arm"/> that were not correct, in the order they were
        /// asked; only those of <paramref name="outcome"/> when it is given.
        /// </summary>
        internal IReadOnlyList<EvalAnswer> Failures(string arm, DecisionOutcome? outcome = null)
        {
            return Report.Answers
                .Where(a => a.Arm == arm && a.Outcome != DecisionOutcome.Correct && (outcome == null || a.Outcome == outcome))
                .ToList();
        }

        /// <summary>The prompt as the agent was asked it: <c>Player: "Walk with me to the bridge."</c></summary>
        internal static string Asked(EvalAnswer a) => $"{a.Label}: “{a.Stimulus}”";

        /// <summary>What was expected and what came back, on one line.</summary>
        internal static string ExpectedAndGot(EvalAnswer a)
        {
            string expected = $"expected {a.ExpectedAction} · {a.ExpectedTarget}";
            if (a.Outcome == DecisionOutcome.PipelineError || a.ModelAction == null)
            {
                return $"{expected} · no decision: {a.ErrorKind ?? "error"}";
            }

            string said = $"{a.ModelAction} · {a.ModelTarget}";
            return a.Guard == null
                ? $"{expected} · got {said}"
                : $"{expected} · model said {said}, stopped by {a.Guard} ({a.GuardVerdict})";
        }

        /// <summary>
        /// Everything one answer recorded, as label and value pairs, for the row's <b>Open</b>: what
        /// was asked, what was expected, what the model said, what the guards made of it.
        /// </summary>
        internal static IReadOnlyList<KeyValuePair<string, string>> Details(EvalAnswer a)
        {
            var rows = new List<KeyValuePair<string, string>>();
            void Add(string label, string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    rows.Add(new KeyValuePair<string, string>(label, value));
                }
            }

            Add("Prompt", $"{a.PromptId} in {a.Category}");
            Add("Asked", Asked(a));
            Add("State told to the model", a.StateLines == null || a.StateLines.Count == 0 ? null : string.Join(" ", a.StateLines));
            Add("State set on the agent", a.State == null || !a.State.HasValues ? null : a.State.ToString(Newtonsoft.Json.Formatting.None));
            Add("Grounding guard", a.CheckTarget ? "on for this prompt" : "off for this prompt");
            Add("Expected", $"{a.ExpectedAction} · {a.ExpectedTarget}");
            Add("Model said", a.ModelAction == null ? null : $"{a.ModelAction} · {a.ModelTarget}");
            Add("Its statement", a.ModelStatement);
            Add("After the guards", a.Action == null ? null : $"{a.Action} · {a.Target}");
            Add("Guard", a.Guard == null ? null : $"{a.Guard}, {a.GuardVerdict}: {a.GuardReason}");
            Add("Target grounding", a.TargetGrounding == null ? null
                : $"{a.TargetGrounding.Value.ToString("0.00", CultureInfo.InvariantCulture)} ({a.TargetGroundingStatus})");
            Add("Error", a.ErrorKind == null ? null : $"{a.ErrorKind}: {a.ErrorMessage}");
            Add("Outcome", NameOf(a.Outcome));
            Add("Model", a.Model == null ? null : $"{a.Model}, {a.LatencySeconds.ToString("0.00", CultureInfo.InvariantCulture)} s");
            return rows;
        }

        // ---- ⑥ Labelling -----------------------------------------------------------------------

        /// <summary>
        /// The labellers' agreement in the Prompt Set window's own words and counts (#134), so the
        /// two windows never disagree about the same label files.
        /// </summary>
        internal static string AgreementHeadline(AgreementReport agreement)
        {
            int twice = agreement.AnsweredByTwoOrMore;
            return twice == 0
                ? "No prompt has two answers yet."
                : $"{agreement.Unanimous} of {twice} prompts answered by two or more people got the same answer from all ({agreement.PercentAgreement:0}%).";
        }

        /// <summary>The line under the headline: the splits, and where they are settled.</summary>
        internal static string AgreementDetail(AgreementReport agreement)
        {
            if (agreement.AnsweredByTwoOrMore == 0)
            {
                return "A second person labels prompts blind in the Prompt Set window's Label tab.";
            }

            int splits = agreement.Splits.Count;
            return splits == 0
                ? "No prompt split its labellers."
                : $"{splits} prompt{(splits == 1 ? "" : "s")} split {(splits == 1 ? "its" : "their")} labellers: the Prompt Set window's Agreement tab lists and settles them.";
        }

        // ---- ⑥ Export ---------------------------------------------------------------------------

        /// <summary>
        /// A short Markdown summary to paste into findings.md: what ran, under what conditions, the
        /// five outcomes per arm, the noise check and any warning. <paramref name="reportPath"/> is
        /// where the full report is, or null.
        /// </summary>
        internal string FindingsSummary(string reportPath)
        {
            var c = Report.Conditions;
            var md = new StringBuilder();
            void Line(string text = "") => md.Append(text).Append('\n');

            if (Report.Cancelled)
            {
                Line($"**{CancelledLine}**");
                Line();
            }

            string set =string.IsNullOrEmpty(c.SetPath) ? "the set" : $"`{Path.GetFileName(Path.GetDirectoryName(c.SetPath))}/{Path.GetFileName(c.SetPath)}`";
            string commit = c.SetCommit == null ? ", not in git" : $" at {Short(c.SetCommit)}" + (c.SetUncommitted == true ? " plus uncommitted changes" : "");
            Line($"**A/B run {Report.Name}**: {c.Prompts} prompts from {set}{commit}, "
               + (c.Order == EvalArmOrder.Interleaved ? "interleaved" : "one arm after the other") + $", framework {c.FrameworkVersion}.");
            Line();
            for (int i = 0; i < c.Arms.Count && i < 2; i++)
            {
                var arm = c.Arms[i];
                string version = arm.ProviderVersion == null || arm.ProviderVersion == "not checked" ? "version not checked" : $"{Capital(arm.Provider)} {arm.ProviderVersion}";
                Line($"- Arm {(i == 0 ? "A" : "B")} `{arm.Name}`: {arm.ProviderConfig}, {arm.Model ?? "model not reported"}, {version}, "
                   + $"grounding guard {(arm.CheckTarget ? "on" : "off")}, {arm.WarmUpRequests} warm-up.");
            }

            Line();
            Line($"| Outcome | {ArmA.Name} | {ArmB.Name} |");
            Line("|---|---:|---:|");
            foreach (var row in OutcomeRows())
            {
                Line($"| {row.Name} | {row.CountA} ({Percent(row.ShareA)}) | {row.CountB} ({Percent(row.ShareB)}) |");
            }

            Line();
            Line(NoiseLine + $" Floor: {NoiseCheck.Formula}.");
            if (c.Warnings.Count > 0)
            {
                Line();
                Line("Warnings: " + string.Join(" ", c.Warnings));
            }

            if (!string.IsNullOrEmpty(reportPath))
            {
                Line();
                Line($"Full report: `{reportPath}`.");
            }

            return md.ToString();
        }

        // ---- Plumbing ---------------------------------------------------------------------------

        internal static string Percent(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

        internal static string Points(double points) => points.ToString("0.0", CultureInfo.InvariantCulture);

        internal static string Signed(double points) => (points > 0 ? "+" : points < 0 ? "−" : "±") + Math.Abs(points).ToString("0.0", CultureInfo.InvariantCulture);

        private static string Megabytes(long bytes) => (bytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture);

        private static string Short(string commit) => commit.Length > 7 ? commit.Substring(0, 7) : commit;

        private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
