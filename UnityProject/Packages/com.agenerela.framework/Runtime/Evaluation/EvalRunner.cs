using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Agenerela.Providers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// The A/B runner (#145): asks every prompt of a <see cref="PromptSet"/> through the real
    /// <see cref="Agent"/> in two arms, in one session, sorts every answer into one of the five
    /// outcomes and returns an <see cref="EvalReport"/>. Every accuracy claim after Phase 4 comes
    /// from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Before the run</b> (build plan §4): the set is refused if it breaks a writing rule
    /// (<see cref="PromptSetChecks.Check"/>) or disagrees with the profile
    /// (<see cref="PromptSetChecks.CompareWithProfile"/>). For an Ollama arm the runner records the
    /// server's version, refuses to start while a model other than the arm's is resident (hard
    /// rule 3), sends <see cref="WarmUpRequests"/> throwaway decisions whose stimuli are no
    /// prompt's, and records how much of the model is in GPU memory, warning when not all of it
    /// is. For any other provider the report says "not checked". Interleaved arms on two different
    /// Ollama models are refused: they would both be resident.
    /// </para>
    /// <para>
    /// <b>Each prompt</b> gets a fresh agent per arm, built from the profile with no memory, so no
    /// prompt sees another. Every action the profile has is bound to a handler that does nothing
    /// and is available exactly when the prompt's state matches the set's <c>onlyWhen</c>; the
    /// state goes into <see cref="Agent.State"/>. The set's targets come from an
    /// <see cref="ExplicitTargetSource"/> made from its declaration. The model reads neither the
    /// state nor the schema, so both are also told to it as observations, in this order: the
    /// arm's own lines, the state as <see cref="DescribeState"/> words it, one <c>id: description.</c>
    /// line per target, then the prompt's own observations. The prompt's own are the call's
    /// <see cref="DecideOptions.Observations"/>; the rest, the same on every prompt of the arm, come
    /// from an observation source of the agent's, so an arm's
    /// <see cref="DecideOptions.ObservationPlacement"/> moves the prompt's lines alone (#151),
    /// unless the arm sets <see cref="EvalArm.StandingLinesAsCallObservations"/>, which makes every
    /// line the call's.
    /// </para>
    /// <para>
    /// <b>The outcome</b> of each answer is <see cref="Classify"/>'s and is written to the
    /// decision's <see cref="DecisionTelemetry.Outcome"/>. Nothing is executed: the runner scores
    /// what the agent chose, not what it said.
    /// </para>
    /// <para>
    /// <b>What it said</b> is a second score: <see cref="ReplyChecks"/> flags each answer's statement
    /// that repeats a few-shot line, is empty, is longer than the arm's
    /// <see cref="EvalArm.MaxStatementLength"/>, or leaks the machinery, into
    /// <see cref="EvalAnswer.ReplyFlags"/> and <see cref="EvalArmResult.Replies"/>. No flag changes
    /// an outcome or the accuracy.
    /// </para>
    /// <para>
    /// Run it from the main thread. <see cref="Progress"/> says how far it has come. Cancelling
    /// stops it between or during decisions and throws an <see cref="EvalCancelledException"/>,
    /// an <see cref="OperationCanceledException"/> carrying the report as far as it got.
    /// </para>
    /// </remarks>
    public sealed class EvalRunner
    {
        // What each residency reading is called in the report.
        internal const string BeforeWarmUp = "before warm-up", AfterWarmUp = "after warm-up", AtEnd = "after the arm's last prompt";

        internal const string NotChecked = "not checked";

        // Throwaway stimuli, in the order they are tried; one a prompt of the set uses is skipped.
        private static readonly string[] WarmUpStimuli =
        {
            "Good evening.", "Is the road quiet tonight?", "Stay sharp.", "Lovely weather for it.", "Anything to report?",
        };

        /// <param name="set">The prompts and the targets and conditions they are asked with.</param>
        /// <param name="profile">The agent the set is measured against.</param>
        /// <param name="armA">The first arm, run first on each prompt.</param>
        /// <param name="armB">The second arm.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">The two arms have the same name.</exception>
        public EvalRunner(PromptSet set, AgentProfile profile, EvalArm armA, EvalArm armB)
        {
            Set = set ?? throw new ArgumentNullException(nameof(set));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            ArmA = armA ?? throw new ArgumentNullException(nameof(armA));
            ArmB = armB ?? throw new ArgumentNullException(nameof(armB));
            if (string.Equals(armA.Name, armB.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Both arms are named '{armA.Name}'; the report tells them apart by name.", nameof(armB));
            }

            Profile = profile;
        }

        public PromptSet Set { get; }
        public AgentProfile Profile { get; }
        public EvalArm ArmA { get; }
        public EvalArm ArmB { get; }

        /// <summary>Interleaved by default; sequential for two arms on two models.</summary>
        public EvalArmOrder Order = EvalArmOrder.Interleaved;

        /// <summary>
        /// The throwaway decisions sent per arm before its first prompt, 3 by default (build plan
        /// §4, item 6: two or three). They are never counted or reported, only how many there were.
        /// </summary>
        public int WarmUpRequests = 3;

        /// <summary>
        /// How a prompt's <c>preconditionState</c> is told to the model, one observation per line:
        /// <see cref="Agent.State"/> never reaches the prompt. <see cref="PlainStateLines"/> by
        /// default; set it to word the state the way the game does, such as "You are following the
        /// player." Null tells the model nothing about the state. Every answer records the lines.
        /// </summary>
        public Func<JObject, IEnumerable<string>> DescribeState = PlainStateLines;

        /// <summary>
        /// Told how far the run has come, on the main thread: <see cref="EvalPhase.Checking"/> once
        /// for both servers and again before each arm, <see cref="EvalPhase.WarmUp"/> before each
        /// arm's warm-up, <see cref="EvalPhase.Asking"/> after every answer, and
        /// <see cref="EvalPhase.Unloading"/> before a sequential run unloads arm A's model. Null by
        /// default. An exception it throws stops the run.
        /// </summary>
        public Action<EvalProgress> Progress;

        // How long a sequential run waits for arm A's model to leave memory; shorter in tests.
        internal float UnloadWaitSeconds = 15f;

        /// <summary>One line per state value, as the set writes it: <c>isFollowing: true.</c></summary>
        public static IEnumerable<string> PlainStateLines(JObject state)
        {
            if (state == null)
            {
                yield break;
            }

            foreach (var property in state.Properties())
            {
                yield return $"{property.Name}: {property.Value.ToString(Formatting.None)}.";
            }
        }

        /// <summary>
        /// The outcome of one answer to <paramref name="prompt"/> (build plan §4, item 5):
        /// <list type="bullet">
        ///   <item><description><see cref="DecisionOutcome.PipelineError"/>: there is no decision.</description></item>
        ///   <item><description>
        ///   A guard stopped the model's answer in <see cref="Agent.DecideAsync"/>, whatever its
        ///   verdict: <see cref="DecisionOutcome.ContainedByGuard"/> when the label was <c>none</c>,
        ///   otherwise <see cref="DecisionOutcome.RejectedWhenActionExpected"/>.
        ///   </description></item>
        ///   <item><description>
        ///   No guard did: <see cref="DecisionOutcome.Correct"/> when the action is the label's and
        ///   the target one it accepts (any target of an open choice), otherwise
        ///   <see cref="DecisionOutcome.WrongLegalAction"/>, <c>none</c> for an action included.
        ///   </description></item>
        /// </list>
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static DecisionOutcome Classify(EvalPrompt prompt, DecisionResult result)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            if (result == null) throw new ArgumentNullException(nameof(result));

            if (result.Decision == null || result.Telemetry.Outcome == DecisionOutcome.PipelineError)
            {
                return DecisionOutcome.PipelineError;
            }

            if (result.Verdict.HasValue)
            {
                return prompt.ExpectedAction == ActionRegistry.None
                    ? DecisionOutcome.ContainedByGuard
                    : DecisionOutcome.RejectedWhenActionExpected;
            }

            var decision = result.Decision;
            bool target = prompt.ExpectedTarget != null && prompt.ExpectedTarget.Accepts(decision.TargetId);
            return decision.ActionId == prompt.ExpectedAction && target
                ? DecisionOutcome.Correct
                : DecisionOutcome.WrongLegalAction;
        }

        /// <summary>Runs both arms over every prompt and returns the report. Nothing is written to disk.</summary>
        /// <exception cref="InvalidOperationException">
        /// The run cannot start or go on: the set breaks a rule or disagrees with the profile, has no
        /// prompts, another model is resident, interleaved arms are on two models, no warm-up decision
        /// was a decision, or the model could not be unloaded between sequential arms. The message
        /// says which.
        /// </exception>
        /// <exception cref="ProviderException">An Ollama server could not be read.</exception>
        /// <exception cref="EvalCancelledException"><paramref name="ct"/> was cancelled once the run had begun: an
        /// <see cref="OperationCanceledException"/> whose <see cref="EvalCancelledException.Partial"/> is the report so far.</exception>
        public async Awaitable<EvalReport> RunAsync(CancellationToken ct = default)
        {
            if (WarmUpRequests < 0)
            {
                throw new InvalidOperationException($"WarmUpRequests is {WarmUpRequests}; it cannot be negative.");
            }

            var prompts = Prompts();
            RefuseABrokenSet(prompts);

            var a = new ArmRun(ArmA, ProviderOf(ArmA), new ReplyChecks.Checker(Set, Profile, ArmA));
            var b = new ArmRun(ArmB, ProviderOf(ArmB), new ReplyChecks.Checker(Set, Profile, ArmB));
            if (Order == EvalArmOrder.Interleaved && a.Ollama != null && b.Ollama != null
                && !OllamaProvider.SameModel(a.Ollama.Model, b.Ollama.Model))
            {
                throw new InvalidOperationException(
                    $"Interleaved arms must be on one model, and arm '{a.Arm.Name}' is on {a.Ollama.Model} while '{b.Arm.Name}' is on "
                  + $"{b.Ollama.Model}: both would be resident (hard rule 3). Set Order to EvalArmOrder.Sequential.");
            }

            var report = new EvalReport();
            var started = DateTime.UtcNow;
            report.Name = System.Text.RegularExpressions.Regex.Replace(
                started.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + "_" + ArmA.Name + "_vs_" + ArmB.Name,
                @"[^A-Za-z0-9._-]+", "-");
            report.Conditions = Conditions(started, prompts.Count);
            report.Conditions.Arms.Add(a.Conditions);
            report.Conditions.Arms.Add(b.Conditions);
            report.Arms.Add(a.Result);
            report.Arms.Add(b.Result);

            try
            {
                await RunArms(report, a, b, prompts, ct);
            }
            catch (OperationCanceledException e) when (ct.IsCancellationRequested && !(e is EvalCancelledException))
            {
                // The report as far as it got, marked so nobody mistakes it for a result.
                report.Cancelled = true;
                Finish(report, a, b);
                throw new EvalCancelledException(report, e, ct);
            }

            Finish(report, a, b);
            return report;
        }

        private async Awaitable RunArms(EvalReport report, ArmRun a, ArmRun b, List<(string Category, EvalPrompt Prompt)> prompts, CancellationToken ct)
        {
            int total = prompts.Count * 2;

            // Both servers' state before anything is sent. Only arm A's model may be resident on
            // its server, since A runs first; on a server of B's own, only B's.
            ReportProgress(EvalPhase.Checking, null, 0, total);
            await RefuseStrangers(a, a, ct);
            await RefuseStrangers(b, b.Ollama != null && a.Ollama != null && b.Ollama.Endpoint == a.Ollama.Endpoint ? a : b, ct);

            if (Order == EvalArmOrder.Interleaved)
            {
                await Prepare(a, prompts, 0, total, ct);
                await Prepare(b, prompts, 0, total, ct);
                foreach (var (category, prompt) in prompts)
                {
                    report.Answers.Add(await Ask(a, category, prompt, ct));
                    ReportProgress(EvalPhase.Asking, a.Arm.Name, report.Answers.Count, total);
                    report.Answers.Add(await Ask(b, category, prompt, ct));
                    ReportProgress(EvalPhase.Asking, b.Arm.Name, report.Answers.Count, total);
                }

                await Read(a, AtEnd, ct);
                await Read(b, AtEnd, ct);
            }
            else
            {
                await Prepare(a, prompts, 0, total, ct);
                foreach (var (category, prompt) in prompts)
                {
                    report.Answers.Add(await Ask(a, category, prompt, ct));
                    ReportProgress(EvalPhase.Asking, a.Arm.Name, report.Answers.Count, total);
                }

                await Read(a, AtEnd, ct);
                ReportProgress(EvalPhase.Unloading, a.Arm.Name, report.Answers.Count, total);
                await Unload(a, UnloadWaitSeconds, ct);

                await Prepare(b, prompts, report.Answers.Count, total, ct);
                foreach (var (category, prompt) in prompts)
                {
                    report.Answers.Add(await Ask(b, category, prompt, ct));
                    ReportProgress(EvalPhase.Asking, b.Arm.Name, report.Answers.Count, total);
                }

                await Read(b, AtEnd, ct);
            }
        }

        // The warnings, the noise check and the finish time, for a whole run or a cancelled one.
        private void Finish(EvalReport report, ArmRun a, ArmRun b)
        {
            foreach (var run in new[] { a, b })
            {
                Warn(report.Conditions, run);
            }

            // A cancelled run compares only where both arms were asked the same prompts: in an
            // interleaved run, when the cancel fell between two prompts.
            if (a.Result.Overall.Total > 0 && a.Result.Overall.Total == b.Result.Overall.Total)
            {
                report.Noise.Add(NoiseCheck.Of("all", a.Result.Overall, b.Result.Overall));
                // In the set's order; a category with no prompts has nothing to compare.
                foreach (var category in Set.Categories.Where(c => a.Result.Category(c.Id) != null))
                {
                    var countsA = a.Result.Category(category.Id);
                    var countsB = b.Result.Category(category.Id);
                    if (countsB != null && countsA.Total == countsB.Total)
                    {
                        report.Noise.Add(NoiseCheck.Of(category.Id, countsA, countsB));
                    }
                }
            }

            report.Conditions.FinishedUtc = Iso(DateTime.UtcNow);
        }

        private void ReportProgress(EvalPhase phase, string arm, int answered, int total)
        {
            Progress?.Invoke(new EvalProgress(phase, arm, answered, total));
        }

        // ---- Before the run --------------------------------------------------------------------

        private List<(string Category, EvalPrompt Prompt)> Prompts()
        {
            return Set.Categories.SelectMany(c => c.Prompts.Select(p => (c.Id, p))).ToList();
        }

        private void RefuseABrokenSet(List<(string Category, EvalPrompt Prompt)> prompts)
        {
            var problems = PromptSetChecks.Check(Set, Profile).Select(p => $"{p.PromptId}: {p.Message}")
                .Concat(PromptSetChecks.CompareWithProfile(Set, Profile))
                .Concat(IdleExampleProblems(ArmA, prompts))
                .Concat(IdleExampleProblems(ArmB, prompts))
                .Concat(LegalityProblems(ArmA, prompts))
                .Concat(LegalityProblems(ArmB, prompts))
                .ToList();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The set cannot be run until it is fixed ({problems.Count} problem{(problems.Count == 1 ? "" : "s")}): "
                  + string.Join("; ", problems.Take(10)) + (problems.Count > 10 ? "; ..." : ""));
            }

            if (prompts.Count == 0)
            {
                throw new InvalidOperationException("The set has no prompts.");
            }
        }

        // An arm's own legality (#151) is honest only if every label stays a legal answer under it, and
        // its extra targets are new ids no label names: otherwise the arm would score the model
        // against an answer it was told it may not give.
        private IEnumerable<string> LegalityProblems(EvalArm arm, List<(string Category, EvalPrompt Prompt)> prompts)
        {
            var legality = arm.Legality;
            if (legality == null)
            {
                yield break;
            }

            var setIds = new HashSet<string>(Set.Targets.Select(t => t.Id), StringComparer.Ordinal);
            var extraIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var extra in legality.ExtraTargets)
            {
                if (string.IsNullOrWhiteSpace(extra.Id) || setIds.Contains(extra.Id) || !extraIds.Add(extra.Id))
                {
                    yield return $"arm '{arm.Name}': extra target '{extra.Id}' is blank, the set's own, or listed twice.";
                }
            }

            foreach (string id in legality.TargetCategories.Keys)
            {
                if (!setIds.Contains(id))
                {
                    yield return $"arm '{arm.Name}': its legality gives a category to '{id}', which is not one of the set's targets.";
                }
            }

            foreach (var (_, prompt) in prompts)
            {
                if (prompt.ExpectedTarget == null || prompt.ExpectedAction == ActionRegistry.None)
                {
                    continue;
                }

                // The categories Build gives the action: the arm's own, or else the profile's.
                if (!legality.ActionCategories.TryGetValue(prompt.ExpectedAction, out var categories))
                {
                    categories = Profile.Actions
                        .Select(asset => asset != null ? asset.Action : null)
                        .FirstOrDefault(action => action != null && action.Id == prompt.ExpectedAction)?.TargetCategories;
                }

                var rule = new ActionDefinition { Id = prompt.ExpectedAction, TargetCategories = categories };
                foreach (string id in prompt.ExpectedTarget.Ids)
                {
                    if (extraIds.Contains(id))
                    {
                        yield return $"{prompt.Id}: arm '{arm.Name}' registers its expected target '{id}' as an extra target.";
                    }
                    else if (categories != null && id != TargetRegistry.NoTarget &&
                             !rule.AcceptsCategory(legality.TargetCategories.TryGetValue(id, out var category) ? category : null))
                    {
                        yield return $"{prompt.Id}: arm '{arm.Name}' makes its expected answer {prompt.ExpectedAction}/{id} illegal.";
                    }
                }
            }
        }

        // An arm's own idle example, and its statement (#190, G14), replace the profile's, which
        // PromptSetChecks has checked, so they are checked here: scoring a prompt the model was
        // shown in an example measures nothing (hard rule 4).
        private IEnumerable<string> IdleExampleProblems(EvalArm arm, List<(string Category, EvalPrompt Prompt)> prompts)
        {
            var texts = new[]
            {
                (Text: arm.Options.IdleExampleStimulus, What: "idle example"),
                (Text: arm.Options.IdleExampleStatement, What: "idle example's statement"),
            };

            foreach (var (text, what) in texts)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                foreach (var (_, prompt) in prompts)
                {
                    if (PromptSetChecks.MatchesExample(prompt.Stimulus, text))
                    {
                        yield return $"{prompt.Id}: the stimulus is arm '{arm.Name}''s {what} \"{text}\": never score on a few-shot example (hard rule 4)";
                    }
                }
            }
        }

        private static ILLMProvider ProviderOf(EvalArm arm)
        {
            // Read once per run, on the main thread, as ProviderConfig asks.
            return arm.Config.Provider;
        }

        private EvalConditions Conditions(DateTime started, int prompts)
        {
            string commit = null;
            bool? uncommitted = null;
            if (Set.Folder != null)
            {
                commit = Git(Set.Folder, "log -1 --format=%H -- .");
                if (!string.IsNullOrEmpty(commit))
                {
                    string status = Git(Set.Folder, "status --porcelain -- .");
                    uncommitted = status == null ? (bool?)null : status.Length > 0;
                }
                else
                {
                    commit = null;
                }
            }

            return new EvalConditions
            {
                StartedUtc = Iso(started),
                FrameworkVersion = AgenerelaInfo.Version,
                Order = Order,
                SetPath = Set.FilePath,
                SetAgent = Set.Agent,
                Prompts = prompts,
                SetCommit = commit,
                SetUncommitted = uncommitted,
                Profile = Profile.name,
                GraphicsDevice = SystemInfo.graphicsDeviceName,
                GraphicsMemoryMB = SystemInfo.graphicsMemorySize,
                Targets = "An ExplicitTargetSource made from the set's declared targets, each also told to the model as an "
                        + "observation, \"id: description.\"",
                State = DescribeState == null
                    ? "Set on Agent.State only; not told to the model (DescribeState is null)."
                    : DescribeState == (Func<JObject, IEnumerable<string>>)PlainStateLines
                        ? "Set on Agent.State, and told to the model as observations, one \"key: value.\" line per value (PlainStateLines)."
                        : "Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines.",
            };
        }

        // Any model resident on run's server that is not first's, the arm that runs first there.
        private static async Awaitable RefuseStrangers(ArmRun run, ArmRun first, CancellationToken ct)
        {
            if (run.Ollama == null)
            {
                return;
            }

            var strangers = (await run.Ollama.ReadResidentAsync(ct))
                .Where(m => !OllamaProvider.SameModel(m.Name, first.Ollama.Model))
                .Select(m => m.Name)
                .ToList();
            if (strangers.Count > 0)
            {
                throw ResidentRefusal(first, strangers);
            }
        }

        private static InvalidOperationException ResidentRefusal(ArmRun run, List<string> others)
        {
            return new InvalidOperationException(
                $"Hard rule 3: Ollama at {run.Ollama.Endpoint} has {string.Join(", ", others)} loaded besides {run.Ollama.Model}, "
              + "arm '" + run.Arm.Name + "''s model. Two models on one card share compute and flatten every latency. "
              + $"Unload {(others.Count == 1 ? "it" : "them")} first (`ollama stop <model>`), then run again.");
        }

        // The version, the residency check, the warm-up and the reading after it.
        private async Awaitable Prepare(ArmRun run, List<(string Category, EvalPrompt Prompt)> prompts, int answered, int total, CancellationToken ct)
        {
            ReportProgress(EvalPhase.Checking, run.Arm.Name, answered, total);
            run.Conditions.WarmUpRequests = WarmUpRequests;
            if (run.Ollama != null)
            {
                run.Conditions.ProviderVersion = await run.Ollama.ReadVersionAsync(ct);
                var before = await Read(run, BeforeWarmUp, ct);
                var others = before.Models.Where(m => !OllamaProvider.SameModel(m.Name, run.Ollama.Model)).Select(m => m.Name).ToList();
                if (others.Count > 0)
                {
                    throw ResidentRefusal(run, others);
                }
            }
            else
            {
                run.Conditions.ProviderVersion = NotChecked;
                run.Conditions.Residency.Add(new ResidencyReading
                {
                    When = BeforeWarmUp,
                    Checked = false,
                    Note = $"{NotChecked}: the {run.Provider.Name} provider cannot say which models are resident.",
                });
            }

            ReportProgress(EvalPhase.WarmUp, run.Arm.Name, answered, total);
            await WarmUp(run, prompts, ct);
            await Read(run, AfterWarmUp, ct);
        }

        private async Awaitable WarmUp(ArmRun run, List<(string Category, EvalPrompt Prompt)> prompts, CancellationToken ct)
        {
            var stimuli = WarmUpStimuli
                .Where(s => prompts.All(p => !PromptSetChecks.MatchesExample(p.Prompt.Stimulus, s)))
                .ToList();
            DecisionResult last = null;
            bool decided = false;
            for (int i = 0; i < WarmUpRequests; i++)
            {
                if (stimuli.Count == 0)
                {
                    throw new InvalidOperationException("Every warm-up stimulus is also a prompt of the set; none could be sent.");
                }

                // Shaped like a prompt's request, the arm's and the targets' lines included, but with
                // no state: a DescribeState of the caller's may expect values a set always has.
                var options = run.Arm.Options.Copy();
                options.StimulusLabel = "Player";
                var agent = Build(run, new JObject(), Standing(run, new List<string>()), new List<string>(), options);
                last = await agent.DecideAsync(stimuli[i % stimuli.Count], options, ct);
                decided |= last.Decision != null;
            }

            if (WarmUpRequests > 0 && !decided)
            {
                throw new InvalidOperationException(
                    $"Arm '{run.Arm.Name}': no warm-up request produced a decision, so every prompt would fail the same way. "
                  + $"{last.Telemetry.ErrorKind}: {last.Telemetry.ErrorMessage}");
            }
        }

        private static async Awaitable<ResidencyReading> Read(ArmRun run, string when, CancellationToken ct)
        {
            var reading = new ResidencyReading { When = when, Checked = run.Ollama != null };
            if (run.Ollama == null)
            {
                reading.Note = NotChecked;
            }
            else
            {
                foreach (var (name, size, vram) in await run.Ollama.ReadResidentAsync(ct))
                {
                    reading.Models.Add(new ResidentModel { Name = name, Size = size, SizeVram = vram });
                }
            }

            run.Conditions.Residency.Add(reading);
            return reading;
        }

        private static async Awaitable Unload(ArmRun run, float waitSeconds, CancellationToken ct)
        {
            if (run.Ollama == null)
            {
                return;
            }

            run.Conditions.Unloaded = await run.Ollama.UnloadAsync(ct, waitSeconds);
            if (run.Conditions.Unloaded != true)
            {
                throw new InvalidOperationException(
                    $"Arm '{run.Arm.Name}''s model {run.Ollama.Model} was still resident after it was asked to unload, "
                  + "so arm B would run beside it (hard rule 3).");
            }
        }

        // What did not hold as it should, for the report's warnings.
        private static void Warn(EvalConditions conditions, ArmRun run)
        {
            if (run.Ollama == null)
            {
                conditions.Warnings.Add($"Arm '{run.Arm.Name}': which models were resident was {NotChecked} ({run.Provider.Name} provider).");
                return;
            }

            foreach (var reading in run.Conditions.Residency.Where(r => r.When != BeforeWarmUp))
            {
                var model = reading.Find(run.Ollama.Model);
                if (model == null)
                {
                    reading.Note = $"{run.Ollama.Model} was not resident.";
                    conditions.Warnings.Add($"Arm '{run.Arm.Name}': {run.Ollama.Model} was not resident {reading.When}.");
                }
                else if (!model.FullyOnGpu)
                {
                    reading.Note = $"{model.Name} was not fully on the GPU.";
                    conditions.Warnings.Add(
                        $"Arm '{run.Arm.Name}': {model.Name} was not fully on the GPU {reading.When}, {model.SizeVram} of {model.Size} bytes "
                      + "in VRAM, so its latencies are not comparable.");
                }

                var others = reading.Models.Where(m => !OllamaProvider.SameModel(m.Name, run.Ollama.Model)).Select(m => m.Name).ToList();
                if (others.Count > 0 && reading.When == AtEnd)
                {
                    conditions.Warnings.Add($"Arm '{run.Arm.Name}': {string.Join(", ", others)} became resident during the run (hard rule 3).");
                }
            }
        }

        // ---- Each prompt ----------------------------------------------------------------------

        private async Awaitable<EvalAnswer> Ask(ArmRun run, string category, EvalPrompt prompt, CancellationToken ct)
        {
            var state = prompt.PreconditionState ?? new JObject();
            var stateLines = DescribeState == null
                ? new List<string>()
                : (DescribeState(state) ?? Enumerable.Empty<string>()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            var options = run.Arm.Options.Copy();
            options.StimulusLabel = prompt.Label;
            options.CheckTarget = run.Arm.Options.CheckTarget && prompt.ChecksTarget;
            var agent = Build(run, state, Standing(run, stateLines), prompt.Observations ?? new List<string>(), options);

            var result = await agent.DecideAsync(prompt.Stimulus, options, ct);
            var outcome = Classify(prompt, result);
            result.Telemetry.Outcome = outcome;
            run.Result.Add(category, outcome);
            run.Answered(result.Telemetry.Model);

            // The reply checks, a second score: they read the statement and change nothing above.
            var replyFlags = new List<ReplyFinding>();
            bool replyChecked = outcome != DecisionOutcome.PipelineError && result.ModelDecision != null;
            if (replyChecked)
            {
                replyFlags = run.Replies.Check(result.ModelDecision.Statement, AsksForStatement(result));
                run.Result.Replies.Add(replyFlags);
            }

            var t = result.Telemetry;
            return new EvalAnswer
            {
                Arm = run.Arm.Name,
                PromptId = prompt.Id,
                Category = category,
                Label = prompt.Label,
                Stimulus = prompt.Stimulus,
                State = (JObject)state.DeepClone(),
                StateLines = stateLines,
                CheckTarget = options.CheckTarget,
                ExpectedAction = prompt.ExpectedAction,
                ExpectedTarget = prompt.ExpectedTarget?.ToString(),
                Outcome = outcome,
                ModelAction = result.ModelDecision?.ActionId,
                ModelTarget = result.ModelDecision?.TargetId,
                ModelStatement = result.ModelDecision?.Statement,
                Action = result.Decision?.ActionId,
                Target = result.Decision?.TargetId,
                Guard = result.Verdict?.Guard,
                GuardVerdict = result.Verdict?.Kind.ToString(),
                GuardReason = result.Verdict?.Reason,
                TargetGrounding = t.TargetGrounding,
                TargetGroundingStatus = t.TargetGroundingStatus?.ToString(),
                ErrorKind = t.ErrorKind?.ToString(),
                ErrorMessage = t.ErrorMessage,
                Model = t.Model,
                LatencySeconds = t.LatencySeconds,
                ReplyChecked = replyChecked,
                ReplyFlags = replyFlags,
                Result = result,
            };
        }

        // Whether the request's schema had a statement field; a decision's always has, so a result
        // without its request counts as asking.
        private static bool AsksForStatement(DecisionResult result)
        {
            var fields = result.Request?.Schema?.Fields;
            return fields == null || fields.Any(f => f != null && f.Name == DecisionSchema.StatementFieldName);
        }

        // The lines every prompt of the arm is told whatever it asks: the arm's own, the state's,
        // then one per target. The prompt's own lines are the call's own observations, after these.
        private List<string> Standing(ArmRun run, List<string> stateLines)
        {
            var observations = new List<string>(run.Arm.Options.Observations ?? new List<string>());
            observations.AddRange(stateLines);
            observations.AddRange(Set.Targets.Select(TargetLine));
            foreach (var extra in run.Arm.Legality?.ExtraTargets ?? new List<EvalExtraTarget>())
            {
                observations.Add(TargetLine(new DeclaredTarget { Id = extra.Id, Description = extra.Description }));
            }

            return observations;
        }

        // A fresh agent for one prompt in one arm: the profile's actions, each available exactly
        // when the state matches its onlyWhen, the set's targets, the state, and no memory. Sets
        // the call's own observations on options: the prompt's own lines. The standing lines reach
        // the agent through an observation source, so the agent tells them apart from the call's
        // own (#151), unless the arm sends them with the call, ahead of the prompt's. The cap is
        // raised to fit every line, so none is dropped. With AfterSources the request is the one
        // the runner sent when every line was the call's own: the standing lines first, then the
        // prompt's.
        private Agent Build(ArmRun run, JObject state, List<string> standing, IList<string> own, DecideOptions options)
        {
            var agent = new Agent(Profile, IdentityOf(run.Arm), run.Provider) { Memory = null };
            if (run.Arm.StandingLinesAsCallObservations)
            {
                var all = new List<string>(standing);
                all.AddRange(own);
                options.Observations = all;
            }
            else
            {
                options.Observations = new List<string>(own);
                agent.ObservationSources.Add(new StandingLines(standing));
            }

            agent.MaxObservations = standing.Count + own.Count;
            foreach (var asset in Profile.Actions)
            {
                var action = asset != null ? asset.Action : null;
                if (action == null || action.Id == ActionRegistry.None)
                {
                    continue;
                }

                // CompareWithProfile has made sure the set declares it.
                var declared = Set.FindAction(action.Id);
                var handler = new DelegateActionHandler(ctx => declared.IsAvailableIn(ToJson(ctx.State)), (ctx, d) => { });
                var legality = run.Arm.Legality;
                if (legality != null && legality.ActionCategories.TryGetValue(action.Id, out var categories))
                {
                    // A copy with the arm's categories, so the shared profile asset never changes.
                    agent.Actions.Register(WithCategories(action, categories), handler);
                }
                else
                {
                    agent.Bind(action.Id, handler);
                }
            }

            var targets = new ExplicitTargetSource();
            var targetCategories = run.Arm.Legality?.TargetCategories;
            foreach (var target in Set.Targets)
            {
                string category = null;
                targetCategories?.TryGetValue(target.Id, out category);
                targets.Add(target.Id, target, category);
            }

            foreach (var extra in run.Arm.Legality?.ExtraTargets ?? new List<EvalExtraTarget>())
            {
                targets.Add(extra.Id, new DeclaredTarget { Id = extra.Id, Description = extra.Description }, extra.Category);
            }

            agent.TargetSources.Add(targets);
            foreach (var property in state.Properties())
            {
                agent.State[property.Name] = property.Value is JValue value ? value.Value : property.Value.DeepClone();
            }

            return agent;
        }

        // The profile's definition with an arm's own target categories (#151): every field copied, so
        // its examples and description read as the profile's do.
        private static ActionDefinition WithCategories(ActionDefinition action, string[] categories)
        {
            return new ActionDefinition
            {
                Id = action.Id,
                Description = action.Description,
                RequiresTarget = action.RequiresTarget,
                TakesNoTarget = action.TakesNoTarget,
                ExampleStimulus = action.ExampleStimulus,
                PreferredExampleTargets = action.PreferredExampleTargets == null ? null : (string[])action.PreferredExampleTargets.Clone(),
                ExampleStatement = action.ExampleStatement,
                CounterExampleStimulus = action.CounterExampleStimulus,
                CounterExampleStatement = action.CounterExampleStatement,
                TargetCategories = categories == null ? null : (string[])categories.Clone(),
            };
        }

        // The profile's identity, or for an arm with knowledge of its own a copy of it with that
        // knowledge after the profile's, so the shared asset is never changed (#190, Living Vale #14).
        private AgentIdentity IdentityOf(EvalArm arm)
        {
            var identity = Profile.Identity;
            if (arm.Knowledge.Count == 0)
            {
                return identity;
            }

            var knowledge = new List<string>(identity.Knowledge ?? new List<string>());
            knowledge.AddRange(arm.Knowledge);
            return new AgentIdentity
            {
                Name = identity.Name,
                Role = identity.Role,
                Personality = identity.Personality,
                Goals = identity.Goals,
                Knowledge = knowledge,
                StatementRules = identity.StatementRules,
            };
        }

        private static JObject ToJson(IReadOnlyDictionary<string, object> state)
        {
            var json = new JObject();
            foreach (var entry in state)
            {
                json[entry.Key] = entry.Value is JToken token ? token.DeepClone()
                    : entry.Value == null ? JValue.CreateNull()
                    : JToken.FromObject(entry.Value);
            }

            return json;
        }

        private static string TargetLine(DeclaredTarget target)
        {
            if (string.IsNullOrWhiteSpace(target.Description))
            {
                return target.Id + ".";
            }

            string description = target.Description.Trim();
            return $"{target.Id}: {description}{(".!?".IndexOf(description[description.Length - 1]) >= 0 ? "" : ".")}";
        }

        // ---- Plumbing -------------------------------------------------------------------------

        private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // git's answer, trimmed, or null when there is no git, no repository, or it took too long.
        private static string Git(string folder, string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", $"-C \"{folder}\" {arguments}")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using (var git = Process.Start(start))
                {
                    if (git == null)
                    {
                        return null;
                    }

                    // Both pipes drained as they come, so the deadline below holds even while git
                    // writes nothing, and a long error cannot fill a pipe and stall it.
                    var output = new System.Text.StringBuilder();
                    git.OutputDataReceived += (_, line) =>
                    {
                        if (line.Data != null)
                        {
                            lock (output)
                            {
                                output.Append(line.Data).Append('\n');
                            }
                        }
                    };
                    git.ErrorDataReceived += (_, __) => { };
                    git.BeginOutputReadLine();
                    git.BeginErrorReadLine();
                    if (!git.WaitForExit(5000))
                    {
                        try
                        {
                            git.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                            // It exited after all.
                        }

                        return null;
                    }

                    // The overload without a timeout returns once the redirected output is read.
                    git.WaitForExit();
                    if (git.ExitCode != 0)
                    {
                        return null;
                    }

                    lock (output)
                    {
                        return output.ToString().Trim();
                    }
                }
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception || e is InvalidOperationException
                                      || e is PlatformNotSupportedException || e is IOException)
            {
                return null;
            }
        }

        // The standing lines of one prompt, handed to its agent as an observation source.
        private sealed class StandingLines : IObservationSource
        {
            private readonly List<string> lines;

            public StandingLines(List<string> lines)
            {
                this.lines = lines;
            }

            public IEnumerable<string> Observe(AgentContext ctx) => lines;
        }

        // One arm while it runs: its provider, what Ollama can say about it, and what it has found.
        private sealed class ArmRun
        {
            public readonly EvalArm Arm;
            public readonly ILLMProvider Provider;
            public readonly OllamaProvider Ollama;
            public readonly EvalArmConditions Conditions;
            public readonly EvalArmResult Result;
            public readonly ReplyChecks.Checker Replies;

            public ArmRun(EvalArm arm, ILLMProvider provider, ReplyChecks.Checker replies)
            {
                Arm = arm;
                Provider = provider;
                Replies = replies;
                Ollama = provider as OllamaProvider;
                Result = new EvalArmResult { Name = arm.Name };
                Conditions = new EvalArmConditions
                {
                    Name = arm.Name,
                    ProviderConfig = arm.Config.name,
                    Provider = provider.Name,
                    Model = Ollama?.Model,
                    CheckTarget = arm.Options.CheckTarget,
                    AskForRefusalLine = arm.Options.AskForRefusalLine,
                    IdleExampleStimulus = arm.Options.IdleExampleStimulus,
                    ListActionsAndTargets = arm.Options.ListActionsAndTargets,
                    MaxListedTargets = arm.Options.MaxListedTargets,
                    CounterExamples = arm.Options.CounterExamples,
                    IdleExampleStatement = arm.Options.IdleExampleStatement,
                    ExampleStatements = arm.Options.ExampleStatements,
                    ExampleTargetFallback = arm.Options.ExampleTargetFallback,
                    PairTargetsWithActions = arm.Options.PairTargetsWithActions,
                    LegalTargets = arm.Options.LegalTargets.ToString(),
                    Legality = arm.Legality?.Copy(),
                    ObservationPlacement = arm.Options.ObservationPlacement.ToString(),
                    StandingLinesAsCallObservations = arm.StandingLinesAsCallObservations,
                    Priority = arm.Options.Priority.ToString(),
                    Observations = new List<string>(arm.Options.Observations ?? new List<string>()),
                    Knowledge = new List<string>(arm.Knowledge),
                    MaxStatementLength = arm.MaxStatementLength,
                };
            }

            public void Answered(string model)
            {
                if (!string.IsNullOrEmpty(model) && !Conditions.ModelsAnswered.Contains(model))
                {
                    Conditions.ModelsAnswered.Add(model);
                }

                if (string.IsNullOrEmpty(Conditions.Model) && !string.IsNullOrEmpty(model))
                {
                    Conditions.Model = model;
                }
            }
        }
    }
}
