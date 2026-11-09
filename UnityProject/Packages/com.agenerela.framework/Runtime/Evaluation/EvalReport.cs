using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// What an <see cref="EvalRunner"/> run found: its conditions, each arm's five outcomes overall
    /// and per category, the noise check, and every answer. <see cref="Save"/> writes it as
    /// <c>report.json</c> and a Markdown summary, <c>report.md</c>, in a folder named for the run.
    /// </summary>
    /// <remarks>
    /// Never one percentage (build plan §4, item 5): the counts are per outcome, because a wrong
    /// legal action, a contained one and a refused good request each need a different fix.
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EvalReport
    {
        /// <summary>The file names <see cref="Save"/> writes.</summary>
        public const string JsonFile = "report.json", MarkdownFile = "report.md";

        /// <summary>The run's name, which its folder is named for: when it started, then the arms.</summary>
        [JsonProperty("name")] public string Name;

        /// <summary>
        /// True when the run was cancelled before it finished: the report holds only the answers
        /// given until then, and is no accuracy result. Written to JSON only when true.
        /// </summary>
        [JsonProperty("cancelled", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool Cancelled;

        [JsonProperty("conditions")] public EvalConditions Conditions = new EvalConditions();

        /// <summary>Arm A's result, then arm B's.</summary>
        [JsonProperty("arms")] public List<EvalArmResult> Arms = new List<EvalArmResult>();

        /// <summary>The whole set's noise check first, then each category's.</summary>
        [JsonProperty("noise")] public List<NoiseCheck> Noise = new List<NoiseCheck>();

        /// <summary>Every answer, both arms', in the order they were asked. The warm-up is never here.</summary>
        [JsonProperty("answers")] public List<EvalAnswer> Answers = new List<EvalAnswer>();

        /// <summary>Every answer that was not <see cref="DecisionOutcome.Correct"/>.</summary>
        public IEnumerable<EvalAnswer> NotCorrect => Answers.Where(a => a.Outcome != DecisionOutcome.Correct);

        /// <summary>Every answer whose statement a reply check flagged, whatever its outcome (<see cref="ReplyChecks"/>).</summary>
        public IEnumerable<EvalAnswer> FlaggedReplies => Answers.Where(a => a.ReplyFlags != null && a.ReplyFlags.Count > 0);

        /// <summary>The report as indented JSON with LF line ends.</summary>
        public string ToJson() => PromptSetJson.Write(this);

        /// <summary>A report read back from <see cref="ToJson"/>'s text.</summary>
        /// <exception cref="InvalidDataException">The text is not a report.</exception>
        public static EvalReport FromJson(string json)
        {
            try
            {
                return PromptSetJson.Read<EvalReport>(json) ?? throw new InvalidDataException("The report is empty.");
            }
            catch (JsonException e)
            {
                throw new InvalidDataException($"Not an evaluation report: {e.Message}", e);
            }
        }

        /// <summary>
        /// Writes <see cref="JsonFile"/> and <see cref="MarkdownFile"/> into a new folder named
        /// <see cref="Name"/> inside <paramref name="parentFolder"/>, creating both folders. A folder
        /// of that name already there gets <c>-2</c>, <c>-3</c>... rather than being written over.
        /// </summary>
        /// <returns>The folder written.</returns>
        public string Save(string parentFolder)
        {
            if (string.IsNullOrWhiteSpace(parentFolder))
            {
                throw new ArgumentException("Say where the report goes.", nameof(parentFolder));
            }

            string name = Regex.Replace(string.IsNullOrWhiteSpace(Name) ? "run" : Name, @"[^A-Za-z0-9._-]+", "-");
            string folder = Path.Combine(Path.GetFullPath(parentFolder), name);
            for (int i = 2; Directory.Exists(folder); i++)
            {
                folder = Path.Combine(Path.GetFullPath(parentFolder), $"{name}-{i}");
            }

            Directory.CreateDirectory(folder);
            PromptSetJson.WriteFile(Path.Combine(folder, JsonFile), this);
            File.WriteAllText(Path.Combine(folder, MarkdownFile), ToMarkdown(), new UTF8Encoding(false));
            return folder;
        }

        private static readonly ReplyFlag[] Flags = { ReplyFlag.Echo, ReplyFlag.Empty, ReplyFlag.TooLong, ReplyFlag.Leak };

        private static readonly DecisionOutcome[] Outcomes =
        {
            DecisionOutcome.Correct, DecisionOutcome.WrongLegalAction, DecisionOutcome.ContainedByGuard,
            DecisionOutcome.RejectedWhenActionExpected, DecisionOutcome.PipelineError,
        };

        /// <summary>
        /// The Markdown summary: conditions, outcomes, noise check, categories, every answer that was
        /// not correct, then the reply checks' counts and every answer they flagged.
        /// </summary>
        public string ToMarkdown()
        {
            var md = new StringBuilder();
            var c = Conditions;
            void Line(string text = "") => md.Append(text).Append('\n');

            Line($"# Evaluation run {Name}");
            Line();
            if (Cancelled)
            {
                Line($"**Cancelled after {Answers.Count} of {c.Prompts * 2} answers.** The set was not asked whole, so nothing here is an "
                   + "accuracy result" + (Noise.Count == 0 ? ", and there is no noise check: the arms were not asked the same prompts." : "."));
                Line();
            }

            Line($"{c.Prompts} prompts from `{c.SetPath}` ({c.SetAgent}), asked in two arms "
               + (c.Order == EvalArmOrder.Interleaved
                   ? "interleaved: each prompt in A, then in B."
                   : "one after the other: all of A, the model unloaded, then all of B. Drift over the session is not shared between them."));
            Line();

            if (c.Warnings.Count > 0)
            {
                Line("## Warnings");
                Line();
                foreach (string warning in c.Warnings)
                {
                    Line($"- **{Cell(warning)}**");
                }

                Line();
            }

            Line("## Conditions");
            Line();
            Line("| | |");
            Line("|---|---|");
            Line($"| Started (UTC) | {c.StartedUtc} |");
            Line($"| Finished (UTC) | {c.FinishedUtc} |");
            Line($"| Framework | {c.FrameworkVersion} |");
            Line($"| Set commit | {(c.SetCommit == null ? "not in git" : c.SetCommit + (c.SetUncommitted == true ? " **plus uncommitted changes**" : ""))} |");
            Line($"| Profile | {Cell(c.Profile)} |");
            Line($"| Graphics device | {Cell(c.GraphicsDevice)} ({c.GraphicsMemoryMB} MB) |");
            Line($"| Targets | {Cell(c.Targets)} |");
            Line($"| State | {Cell(c.State)} |");
            Line();

            Line("| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |");
            Line("|---|---|---|---|---|---|---|---|");
            foreach (var arm in c.Arms)
            {
                var reading = arm.Residency.FirstOrDefault(r => r.When == EvalRunner.AfterWarmUp);
                string resident = reading == null || !reading.Checked
                    ? "not checked"
                    : string.Join(", ", reading.Models.Select(m => $"{m.Name} {Megabytes(m.SizeVram)}/{Megabytes(m.Size)} MB in VRAM"));
                Line($"| {Cell(arm.Name)} | {Cell(arm.ProviderConfig)} | {Cell(arm.Provider)} | {Cell(arm.Model)} | {Cell(arm.ProviderVersion)} "
                   + $"| {(arm.CheckTarget ? "on" : "off")} | {arm.WarmUpRequests} | {Cell(resident)} |");
            }

            Line();
            Line("## Outcomes");
            Line();
            Line("| Outcome | " + string.Join(" | ", Arms.Select(a => Cell(a.Name))) + " |");
            Line("|---|" + string.Concat(Arms.Select(_ => "---:|")));
            foreach (var outcome in Outcomes)
            {
                Line($"| {outcome} | " + string.Join(" | ", Arms.Select(a => a.Overall[outcome].ToString(CultureInfo.InvariantCulture))) + " |");
            }

            Line("| **Accuracy** | " + string.Join(" | ", Arms.Select(a => $"**{Percent(a.Overall.Accuracy)}** ({a.Overall.Correct}/{a.Overall.Total})")) + " |");
            Line();

            Line("## Noise check");
            Line();
            Line($"Noise floor: {NoiseCheck.Formula}. A difference smaller than it is no result.");
            Line();
            Line("| Scope | n | A | B | B − A | Floor | |");
            Line("|---|---:|---:|---:|---:|---:|---|");
            foreach (var check in Noise)
            {
                Line($"| {Cell(check.Scope)} | {check.N} | {Points(check.AccuracyA)} | {Points(check.AccuracyB)} | {Signed(check.DifferencePoints)} "
                   + $"| {Points(check.NoiseFloorPoints)} | {(check.WithinNoise ? "within noise" : "**beyond noise**")} |");
            }

            Line();
            Line("## Per category");
            Line();
            Line("| Category | Arm | n | " + string.Join(" | ", Outcomes) + " |");
            Line("|---|---|---:|" + string.Concat(Outcomes.Select(_ => "---:|")));
            // The noise check's order is the set's.
            var categories = Noise.Count > 1 ? Noise.Skip(1).Select(n => n.Scope)
                : Arms.Count > 0 ? Arms[0].CategoryIds : Enumerable.Empty<string>();
            foreach (string category in categories)
            {
                foreach (var arm in Arms)
                {
                    var counts = arm.Category(category);
                    if (counts != null)
                    {
                        Line($"| {Cell(category)} | {Cell(arm.Name)} | {counts.Total} | "
                           + string.Join(" | ", Outcomes.Select(o => counts[o].ToString(CultureInfo.InvariantCulture))) + " |");
                    }
                }
            }

            Line();
            Line("## Answers that were not correct");
            Line();
            var wrong = NotCorrect.ToList();
            if (wrong.Count == 0)
            {
                Line("None.");
            }
            else
            {
                Line("| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |");
                Line("|---|---|---|---|---|---|---|");
                foreach (var a in wrong)
                {
                    string said = a.ModelAction == null ? $"{a.ErrorKind}: {a.ErrorMessage}" : $"{a.ModelAction}/{a.ModelTarget}";
                    string guard = a.Guard == null ? "" : $"{a.Guard} ({a.GuardVerdict}): {a.GuardReason}";
                    Line($"| {Cell(a.Arm)} | {Cell(a.PromptId)} | {Cell(a.Stimulus)} | {Cell(a.ExpectedAction)}/{Cell(a.ExpectedTarget)} "
                       + $"| {Cell(said)} | {a.Outcome} | {Cell(guard)} |");
                }
            }

            Replies(Line);
            return md.ToString();
        }

        // The second score: what the reply checks found in the statements, apart from the outcomes.
        private void Replies(Action<string> line)
        {
            line("");
            line("## Reply checks");
            line("");
            if (Answers.All(a => !a.ReplyChecked))
            {
                line("No statement was checked: the run had no decisions, or the report was written before reply checks existed.");
                return;
            }

            line("A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a "
               + "few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.");
            line("");
            line("| Check | " + string.Join(" | ", Arms.Select(a => Cell(a.Name))) + " |");
            line("|---|" + string.Concat(Arms.Select(_ => "---:|")));
            line("| Checked | " + string.Join(" | ", Arms.Select(a => a.Replies.Checked.ToString(CultureInfo.InvariantCulture))) + " |");
            foreach (var flag in Flags)
            {
                string name = flag.ToString();
                if (flag == ReplyFlag.TooLong)
                {
                    var limits = Conditions.Arms.Select(a => a.MaxStatementLength).Distinct().ToList();
                    name += limits.Count == 1 ? $" (over {limits[0]} characters)" : " (over each arm's limit)";
                }

                line($"| {name} | " + string.Join(" | ", Arms.Select(a => a.Replies[flag].ToString(CultureInfo.InvariantCulture))) + " |");
            }

            line("| **Any flag** | " + string.Join(" | ", Arms.Select(a => $"**{a.Replies.Flagged}**")) + " |");
            line("");

            var flagged = FlaggedReplies.ToList();
            if (flagged.Count == 0)
            {
                line("No statement was flagged.");
                return;
            }

            line("| Arm | Prompt | Outcome | Statement | Flags |");
            line("|---|---|---|---|---|");
            foreach (var a in flagged)
            {
                string said = string.IsNullOrWhiteSpace(a.ModelStatement) ? "*(empty)*" : Cell(a.ModelStatement);
                line($"| {Cell(a.Arm)} | {Cell(a.PromptId)} | {a.Outcome} | {said} "
                   + $"| {Cell(string.Join("; ", a.ReplyFlags.Where(f => f != null)))} |");
            }
        }

        // A table cell: no line breaks, and a | that does not end the cell.
        private static string Cell(string text) =>
            (text ?? "").Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|");

        private static string Percent(double share) => (share * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        private static string Points(double points) => points.ToString("0.0", CultureInfo.InvariantCulture);

        private static string Signed(double points) => (points >= 0 ? "+" : "") + Points(points);

        private static string Megabytes(long bytes) => (bytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture);
    }
}
