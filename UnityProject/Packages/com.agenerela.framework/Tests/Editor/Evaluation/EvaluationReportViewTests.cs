using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agenerela.Editor.Evaluation;
using Agenerela.Evaluation;
using NUnit.Framework;
using static Agenerela.Tests.Evaluation.RunnerFixtures;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// What the Evaluation Report window shows about a run (#146, screen 4): its six parts, read
    /// from a report, the same whether the report is fresh from a run or read back from disk.
    /// </summary>
    public sealed class EvaluationReportViewTests
    {
        private RunnerFixtures fixtures;
        private string output;

        private static readonly string Bridge = Prompt("target_001", "Meet me over at the bridge.", "move_to", "bridge", false);
        private static readonly string Escort = Prompt("target_002", "Walk with me a while.", "follow_player", "no_target", false);
        private static readonly string Quiet = Prompt("situation_001", "The square is empty.", "none", "no_target", false, "Situation");

        [SetUp]
        public void SetUp()
        {
            fixtures = new RunnerFixtures();
            output = Path.Combine(Path.GetTempPath(), "agenerela-report-view-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            fixtures.Dispose();
            Fixtures.DestroyCreated();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }

        // ---- A saved report reopens identically ------------------------------------------------

        [Test]
        public async Task ASavedReportReopensShowingExactlyWhatTheRunShowed()
        {
            var setup = new EvaluationRunSetup { Set = fixtures.Set(new[] { Bridge, Escort }, new[] { Quiet }), Profile = Fixtures.VillageGuardProfile() };
            setup.A.Config = fixtures.Config(new FakeProvider(Reply("move_to", "bridge", "On my way.")), "FakeA");
            setup.B.Config = fixtures.Config(new FakeProvider(Reply("none", "no_target")), "FakeB");
            setup.NameArmsFromConfigs();
            var fresh = await Fixtures.NotCancelled(setup.Build().RunAsync());

            string folder = fresh.Save(output);
            var reopened = EvalReport.FromJson(File.ReadAllText(Path.Combine(folder, EvalReport.JsonFile)));

            Assert.That(fresh.Answers.All(a => a.Result != null), Is.True, "The fresh report carries each decision; the saved one cannot.");
            Assert.That(Everything(new EvaluationReportView(reopened)), Is.EqualTo(Everything(new EvaluationReportView(fresh))));
        }

        // Every text the window draws from a view, in its order.
        private static string Everything(EvaluationReportView view)
        {
            var text = new StringBuilder();
            text.AppendLine(view.Report.Name).AppendLine(view.RunLine).AppendLine(view.Duration);
            text.AppendLine(view.ArmA.Name + " / " + view.ArmA.Subtitle).AppendLine(view.ArmB.Name + " / " + view.ArmB.Subtitle);
            foreach (string line in view.Warnings.Concat(view.ConditionLines())) text.AppendLine(line);
            foreach (var row in view.OutcomeRows())
            {
                text.AppendLine($"{row.Name} {row.Meaning} {row.CountA} {row.CountB} {row.ShareA} {row.ShareB} {row.DifferencePoints} {row.Noise}");
            }

            text.AppendLine(view.NoiseLine);
            foreach (var row in view.CategoryRows())
            {
                text.AppendLine($"{row.Id} {row.N} {row.AccuracyA} {row.AccuracyB} {row.DifferencePoints} {row.NoiseFloorPoints} {row.WithinNoise}");
            }

            foreach (string arm in new[] { view.ArmA.Name, view.ArmB.Name })
            {
                foreach (var answer in view.Failures(arm))
                {
                    text.AppendLine(EvaluationReportView.Asked(answer)).AppendLine(EvaluationReportView.ExpectedAndGot(answer));
                    foreach (var detail in EvaluationReportView.Details(answer)) text.AppendLine(detail.Key + ": " + detail.Value);
                }
            }

            text.AppendLine(view.FindingsSummary("report.md"));
            return text.ToString();
        }

        [Test]
        public void ACancelledReportSaysSoAboveEverythingAndInItsSummary()
        {
            var report = Report(new[] { 1, 1, 0, 0, 0 }, new[] { 1, 0, 0, 0, 0 });
            report.Cancelled = true;
            report.Noise.Clear();
            report.Answers.AddRange(new[]
            {
                Answer("control", "target_001", DecisionOutcome.Correct), Answer("guards-on", "target_001", DecisionOutcome.Correct),
                Answer("control", "target_002", DecisionOutcome.WrongLegalAction),
            });
            report.Conditions.Prompts = 4;

            var view = new EvaluationReportView(report);

            Assert.That(view.CancelledLine, Does.StartWith("Cancelled after 3 of 8 answers").And.Contain("no").And.Contain("accuracy result"));
            Assert.That(view.RunLine, Does.Contain("cancelled"));
            Assert.That(view.NoiseLine, Does.Contain("cancelled before both arms"));
            Assert.That(view.FindingsSummary(null), Does.StartWith("**Cancelled after 3 of 8 answers"));
            Assert.That(new EvaluationReportView(Report(new[] { 1, 1, 0, 0, 0 }, new[] { 1, 0, 0, 0, 0 })).CancelledLine, Is.Null);
        }

        [Test]
        public void TheProgressBarSaysThePhaseTheArmAndTheAnswersSoFar()
        {
            Assert.That(EvaluationReportWindow.ProgressText(null), Is.EqualTo("Starting…"));
            Assert.That(EvaluationReportWindow.ProgressText(new EvalProgress(EvalPhase.Checking, null, 0, 152)), Is.EqualTo("Checking what is loaded · 0 of 152 answers"));
            Assert.That(EvaluationReportWindow.ProgressText(new EvalProgress(EvalPhase.WarmUp, "control", 0, 152)), Is.EqualTo("Warming up control · 0 of 152 answers"));
            Assert.That(EvaluationReportWindow.ProgressText(new EvalProgress(EvalPhase.Asking, "guards-on", 37, 152)), Is.EqualTo("Asking · guards-on · 37 of 152 answers"));
            Assert.That(EvaluationReportWindow.ProgressText(new EvalProgress(EvalPhase.Unloading, "control", 76, 152)), Is.EqualTo("Unloading control's model · 76 of 152 answers"));
        }

        // ---- ② Five outcomes --------------------------------------------------------------------

        [Test]
        public void TheFiveOutcomesAreEachArmsShareWithTheDifferenceInPoints()
        {
            var view = new EvaluationReportView(Report(new[] { 12, 8, 0, 0, 0 }, new[] { 17, 1, 2, 0, 0 }));

            var rows = view.OutcomeRows();

            Assert.That(rows.Select(r => r.Name), Is.EqualTo(new[]
            {
                "Correct", "Wrong but legal", "Contained by a guard", "Rejected when an action was expected", "Pipeline error",
            }));
            Assert.That(rows[0].ShareA, Is.EqualTo(0.6).Within(1e-9));
            Assert.That(rows[0].ShareB, Is.EqualTo(0.85).Within(1e-9));
            Assert.That(rows[0].DifferencePoints, Is.EqualTo(25).Within(1e-9));
            Assert.That(rows[1].DifferencePoints, Is.EqualTo(-35).Within(1e-9));
            Assert.That(rows[2].CountB, Is.EqualTo(2));
            Assert.That(rows[1].Meaning, Is.EqualTo("the agent visibly misbehaves"));
            Assert.That(rows[2].Meaning, Is.EqualTo("the safety layer working"));
        }

        [Test]
        public void OnlyTheCorrectRowCarriesTheNoiseVerdictBecauseTheCheckIsOnAccuracy()
        {
            var rows = new EvaluationReportView(Report(new[] { 12, 8, 0, 0, 0 }, new[] { 17, 1, 2, 0, 0 })).OutcomeRows();

            Assert.That(rows[0].Noise, Is.EqualTo("beyond noise"));
            Assert.That(rows.Skip(1).Select(r => r.Noise), Is.All.Null);
        }

        // ---- ③ The noise check ------------------------------------------------------------------

        [Test]
        public void ADifferenceSmallerThanTheFloorIsSaidToBeNoResult()
        {
            // 20 prompts: the floor is about 15.8 points, and 10 points is within it.
            var view = new EvaluationReportView(Report(new[] { 12, 8, 0, 0, 0 }, new[] { 14, 6, 0, 0, 0 }));

            Assert.That(view.Overall.WithinNoise, Is.True);
            Assert.That(view.NoiseLine, Does.Contain("+10.0 points").And.Contain("15.8").And.Contain("no result"));
            Assert.That(view.OutcomeRows()[0].Noise, Is.EqualTo("within noise"));
        }

        [Test]
        public void ADifferenceBeyondTheFloorIsSaidToBeLargerThanTheDrift()
        {
            var view = new EvaluationReportView(Report(new[] { 4, 16, 0, 0, 0 }, new[] { 18, 2, 0, 0, 0 }));

            Assert.That(view.NoiseLine, Does.Contain("+70.0 points").And.Contain("beyond").And.Not.Contain("no result"));
        }

        // ---- ④ Per category ---------------------------------------------------------------------

        [Test]
        public void EachCategoryHasItsOwnNInTheSetsOrderAndItsOwnNoiseCheck()
        {
            var report = Report(new[] { 12, 8, 0, 0, 0 }, new[] { 17, 1, 2, 0, 0 });
            report.Noise.Add(NoiseCheck.Of("situation", Counts(1, 5, 0, 0, 0), Counts(5, 0, 1, 0, 0)));
            report.Noise.Add(NoiseCheck.Of("command_with_target", Counts(11, 3, 0, 0, 0), Counts(12, 1, 1, 0, 0)));

            var rows = new EvaluationReportView(report).CategoryRows();

            Assert.That(rows.Select(r => r.Id), Is.EqualTo(new[] { "situation", "command_with_target" }), "The set's order, as the report keeps it; never \"all\".");
            Assert.That(rows[0].N, Is.EqualTo(6));
            Assert.That(rows[0].WithinNoise, Is.False);
            Assert.That(rows[1].N, Is.EqualTo(14));
            Assert.That(rows[1].WithinNoise, Is.True);
        }

        // ---- ⑤ The failure list -----------------------------------------------------------------

        [Test]
        public void TheFailureListHoldsOneArmsAnswersThatWereNotCorrect()
        {
            var report = Report(new[] { 1, 1, 0, 0, 0 }, new[] { 0, 0, 1, 0, 1 });
            report.Answers.AddRange(new[]
            {
                Answer("control", "target_001", DecisionOutcome.Correct),
                Answer("guards-on", "target_001", DecisionOutcome.ContainedByGuard),
                Answer("control", "situation_001", DecisionOutcome.WrongLegalAction),
                Answer("guards-on", "situation_001", DecisionOutcome.PipelineError),
            });
            var view = new EvaluationReportView(report);

            Assert.That(view.Failures("control").Select(a => a.PromptId), Is.EqualTo(new[] { "situation_001" }));
            Assert.That(view.Failures("guards-on").Select(a => a.PromptId), Is.EqualTo(new[] { "target_001", "situation_001" }));
            Assert.That(view.Failures("guards-on", DecisionOutcome.PipelineError).Select(a => a.PromptId), Is.EqualTo(new[] { "situation_001" }));
        }

        [Test]
        public void ARowSaysWhatWasExpectedWhatTheModelSaidAndWhichGuardStoppedIt()
        {
            var stopped = Answer("guards-on", "target_004", DecisionOutcome.RejectedWhenActionExpected);
            stopped.ModelAction = "move_to";
            stopped.ModelTarget = "docks";
            stopped.Guard = "TargetGrounding";
            stopped.GuardVerdict = "RewriteToNone";
            stopped.GuardReason = "docks was never named.";
            stopped.Action = "none";
            stopped.Target = "no_target";

            Assert.That(EvaluationReportView.Asked(stopped), Is.EqualTo("Player: \u201CMeet me over at the bridge.\u201D"));
            Assert.That(EvaluationReportView.ExpectedAndGot(stopped),
                Is.EqualTo("expected move_to · bridge · model said move_to · docks, stopped by TargetGrounding (RewriteToNone)"));

            var details = EvaluationReportView.Details(stopped).ToDictionary(d => d.Key, d => d.Value);
            Assert.That(details["Guard"], Is.EqualTo("TargetGrounding, RewriteToNone: docks was never named."));
            Assert.That(details["After the guards"], Is.EqualTo("none · no_target"));
            Assert.That(details["State told to the model"], Is.EqualTo("isFollowing: false."));
            Assert.That(details.ContainsKey("Error"), Is.False, "An empty field is left out, not shown blank.");
        }

        [Test]
        public void APipelineErrorRowSaysThereWasNoDecisionAndWhy()
        {
            var lost = Answer("control", "target_005", DecisionOutcome.PipelineError);
            lost.ErrorKind = "Timeout";
            lost.ErrorMessage = "No reply in 30 s.";

            Assert.That(EvaluationReportView.ExpectedAndGot(lost), Is.EqualTo("expected move_to · bridge · no decision: Timeout"));
            Assert.That(EvaluationReportView.Details(lost).Single(d => d.Key == "Error").Value, Is.EqualTo("Timeout: No reply in 30 s."));
        }

        // ---- ① The conditions in plain words ----------------------------------------------------

        [Test]
        public void AnArmsObservationPlacementIsSaidOnlyWhenItIsNotTheDefault()
        {
            // #151: a report from before the option, or an arm on the default, reads as it did.
            var arm = new EvalArmConditions();

            Assert.That(EvaluationReportView.PromptOptions(arm), Has.None.Contains("prompt's own observations"));
            arm.ObservationPlacement = "AfterSources";
            Assert.That(EvaluationReportView.PromptOptions(arm), Has.None.Contains("prompt's own observations"));
            arm.ObservationPlacement = "WithStimulus";
            Assert.That(EvaluationReportView.PromptOptions(arm), Has.Some.EqualTo("prompt's own observations after the stimulus"));

            // An arm that sent its standing lines with the call moved the whole block.
            arm.StandingLinesAsCallObservations = true;
            Assert.That(EvaluationReportView.PromptOptions(arm), Has.Some.EqualTo("every observation, standing lines included, after the stimulus"));
            arm.ObservationPlacement = "AfterSources";
            Assert.That(EvaluationReportView.PromptOptions(arm), Has.Some.EqualTo("standing lines sent as the call's own, at the end of the block as usual"));
        }

        [Test]
        public void AnArmsLegalPairsLineAndRulesWordingAreSaidOnlyWhenSet()
        {
            // #244 items 5 and 8: a report from before them, or an arm on the defaults, reads as it did.
            var arm = new EvalArmConditions();
            Assert.That(EvaluationReportView.PromptOptions(arm), Has.None.Contains("statement rules").And.None.Contains("may take"));

            arm.LegalTargets = "Off";
            arm.StatementRulesWording = "YourStatement";
            Assert.That(EvaluationReportView.PromptOptions(arm), Has.None.Contains("statement rules").And.None.Contains("may take"));

            arm.LegalTargets = "ObservationLine";
            arm.StatementRulesWording = "InYourOwnWords";
            arm.StatementRules = "Short.";
            Assert.That(EvaluationReportView.PromptOptions(arm), Has.Some.EqualTo("an observation line says which targets each action may take (ObservationLine)"));
            Assert.That(EvaluationReportView.PromptOptions(arm), Has.Some.EqualTo("statement rules introduced as InYourOwnWords"));
            Assert.That(EvaluationReportView.PromptOptions(arm), Has.Some.EqualTo("the arm's own statement rules \"Short.\""));

            arm.LegalTargets = "PerTarget";
            Assert.That(EvaluationReportView.PromptOptions(arm), Has.Some.EqualTo("the list says which targets each action may take (PerTarget)"));
        }

        [Test]
        public void TheConditionsSayTheOrderTheVersionAndHowMuchOfTheModelWasInVram()
        {
            var report = Report(new[] { 12, 8, 0, 0, 0 }, new[] { 17, 1, 2, 0, 0 });
            var arm = report.Conditions.Arms[0];
            arm.Residency.Add(Reading("before warm-up"));
            arm.Residency.Add(Reading("after warm-up", ("qwen3.5:2b", 2048L << 20, 2048L << 20)));
            report.Conditions.Arms[1].Residency.Add(Reading("after warm-up", ("qwen3.5:2b", 2048L << 20, 1024L << 20)));
            report.Conditions.Arms[1].ListActionsAndTargets = true;
            report.Conditions.Arms[1].MaxListedTargets = 12;
            report.Conditions.Arms[1].CounterExamples = true;
            report.Conditions.Arms[1].PairTargetsWithActions = true;

            var lines = new EvaluationReportView(report).ConditionLines();

            Assert.That(lines[0], Does.StartWith("Interleaved"));
            Assert.That(lines, Has.Some.EqualTo("Arm A, control: OllamaLocal, qwen3.5:2b, Ollama 0.40.14, grounding guard off, "
                                                + "system-prompt list of actions and targets off, no counter-examples, schema does not pair actions with targets, 3 warm-up requests."));
            Assert.That(lines, Has.Some.EqualTo("Arm B, guards-on: OllamaLocal, qwen3.5:2b, Ollama 0.40.14, grounding guard on, "
                                                + "system-prompt list of actions and targets on (at most 12 targets in it), counter-examples shown, schema pairs each action with its targets, 3 warm-up requests."));
            Assert.That(lines, Has.Some.EqualTo("    Before warm-up: no model loaded."));
            Assert.That(lines, Has.Some.EqualTo("    After warm-up: qwen3.5:2b fully on the GPU, 2048 of 2048 MB."));
            Assert.That(lines, Has.Some.Contains("only 1024 of 2048 MB on the GPU, so its latencies are not comparable"));
            Assert.That(lines, Has.Some.Contains("last changed in commit abcdef1."));
        }

        [Test]
        public void SequentialArmsASecondModelAndASetOutsideGitAreAllSaid()
        {
            var report = Report(new[] { 12, 8, 0, 0, 0 }, new[] { 17, 1, 2, 0, 0 });
            report.Conditions.Order = EvalArmOrder.Sequential;
            report.Conditions.SetCommit = null;
            report.Conditions.Arms[0].Unloaded = true;
            report.Conditions.Arms[0].Residency.Add(Reading("after warm-up", ("qwen3.5:2b", 100, 100), ("llama3.1:8b", 100, 100)));
            report.Conditions.Arms[1].ProviderVersion = "not checked";
            report.Conditions.Arms[1].Residency.Add(new ResidencyReading { When = "before warm-up", Checked = false });

            var lines = new EvaluationReportView(report).ConditionLines();

            Assert.That(lines[0], Does.StartWith("One after the other").And.Contain("repeat a close result"));
            Assert.That(lines, Has.Some.Contains("More than one model was loaded (hard rule 3)"));
            Assert.That(lines, Has.Some.Contains("Unloaded after the arm"));
            Assert.That(lines, Has.Some.Contains("provider version not checked"));
            Assert.That(lines, Has.Some.Contains("not checked: this provider cannot say"));
            Assert.That(lines, Has.Some.Contains("not in git"));
        }

        [Test]
        public void TheRunLineSaysHowManyPromptsAndHowLong()
        {
            var view = new EvaluationReportView(Report(new[] { 12, 8, 0, 0, 0 }, new[] { 17, 1, 2, 0, 0 }));

            Assert.That(view.Duration, Is.EqualTo("9 min 40 s"));
            Assert.That(view.RunLine, Does.Contain("20 prompts × 2 arms").And.Contain("interleaved").And.Contain("9 min 40 s"));
        }

        // ---- ⑥ Agreement and export -------------------------------------------------------------

        [Test]
        public void AgreementIsCountedAsThePromptSetWindowCountsIt()
        {
            using (var labelled = new EvalFixtures())
            {
                labelled.WriteLabels("Bo", "command_with_target",
                    "{ \"target_001\": \"move_to/bridge\", \"target_002\": \"move_to/wolf\", \"target_003\": \"move_to/docks\" }");
                labelled.WriteLabels("Bo", "situation", "{ \"situation_001\": \"none/no_target\" }");
                var set = labelled.Load();
                var agreement = AgreementReport.Compute(set, set.LoadLabels());

                // AgreementTests' own fixture: 4 prompts answered twice, 2 unanimous, 2 splits.
                Assert.That(EvaluationReportView.AgreementHeadline(agreement),
                    Is.EqualTo("2 of 4 prompts answered by two or more people got the same answer from all (50%)."));
                Assert.That(EvaluationReportView.AgreementDetail(agreement), Does.StartWith("2 prompts split"));
            }
        }

        [Test]
        public void WithNoSecondLabellerTheAgreementSaysSoAndWhereToLabel()
        {
            using (var labelled = new EvalFixtures())
            {
                File.WriteAllText(Path.Combine(labelled.Folder, "prompts", "situations.json"), "{ \"category\": \"situation\", \"prompts\": [] }");
                var set = labelled.Load();
                var agreement = AgreementReport.Compute(set, set.LoadLabels());

                Assert.That(EvaluationReportView.AgreementHeadline(agreement), Is.EqualTo("No prompt has two answers yet."));
                Assert.That(EvaluationReportView.AgreementDetail(agreement), Does.Contain("Label tab"));
            }
        }

        [Test]
        public void TheFindingsSummaryHasTheArmsTheFiveOutcomesTheNoiseCheckAndTheWarnings()
        {
            var report = Report(new[] { 12, 8, 0, 0, 0 }, new[] { 17, 1, 2, 0, 0 });
            report.Conditions.Warnings.Add("Arm 'guards-on': qwen3.5:2b was not fully on the GPU.");

            string summary = new EvaluationReportView(report).FindingsSummary("Logs/Evaluation/run/report.md");

            Assert.That(summary, Does.Contain("20 prompts from `Evaluation/agent.json` at abcdef1"));
            Assert.That(summary, Does.Contain("Arm A `control`: OllamaLocal, qwen3.5:2b, Ollama 0.40.14, grounding guard off, system-prompt list of actions and targets off, no counter-examples, schema does not pair actions with targets, 3 warm-up."));
            Assert.That(summary, Does.Contain("| Correct | 12 (60%) | 17 (85%) |"));
            Assert.That(summary, Does.Contain("| Contained by a guard | 0 (0%) | 2 (10%) |"));
            Assert.That(summary, Does.Contain("+25.0 points"));
            Assert.That(summary, Does.Contain(NoiseCheck.Formula));
            Assert.That(summary, Does.Contain("was not fully on the GPU"));
            Assert.That(summary, Does.Contain("`Logs/Evaluation/run/report.md`"));
        }

        // ---- Fixtures ---------------------------------------------------------------------------

        // A report as a run of n prompts would write it, counts given in screen 4's outcome order.
        private static EvalReport Report(int[] a, int[] b)
        {
            var countsA = Counts(a);
            var countsB = Counts(b);
            var report = new EvalReport
            {
                Name = "2026-11-05_14-20-00_control_vs_guards-on",
                Conditions = new EvalConditions
                {
                    StartedUtc = "2026-11-05T14:20:00Z",
                    FinishedUtc = "2026-11-05T14:29:40Z",
                    FrameworkVersion = "0.4.0",
                    Order = EvalArmOrder.Interleaved,
                    SetPath = "C:/Games/Village/Assets/Evaluation/agent.json",
                    SetAgent = "Village guard",
                    Prompts = countsA.Total,
                    SetCommit = "abcdef1234567890",
                    SetUncommitted = false,
                    Profile = "VillageGuard",
                    GraphicsDevice = "NVIDIA GeForce RTX 4060 Laptop GPU",
                    GraphicsMemoryMB = 7948,
                },
            };

            foreach (var (name, check) in new[] { ("control", false), ("guards-on", true) })
            {
                report.Conditions.Arms.Add(new EvalArmConditions
                {
                    Name = name, ProviderConfig = "OllamaLocal", Provider = "ollama", Model = "qwen3.5:2b",
                    ProviderVersion = "0.40.14", CheckTarget = check, WarmUpRequests = 3,
                });
            }

            report.Arms.Add(new EvalArmResult { Name = "control", Overall = countsA });
            report.Arms.Add(new EvalArmResult { Name = "guards-on", Overall = countsB });
            report.Noise.Add(NoiseCheck.Of("all", countsA, countsB));
            return report;
        }

        private static OutcomeCounts Counts(params int[] counts)
        {
            return new OutcomeCounts
            {
                Correct = counts[0], WrongLegalAction = counts[1], ContainedByGuard = counts[2],
                RejectedWhenActionExpected = counts[3], PipelineError = counts[4], Total = counts.Sum(),
            };
        }

        private static EvalAnswer Answer(string arm, string promptId, DecisionOutcome outcome)
        {
            return new EvalAnswer
            {
                Arm = arm, PromptId = promptId, Category = "command_with_target", Label = "Player",
                Stimulus = "Meet me over at the bridge.", StateLines = new List<string> { "isFollowing: false." },
                ExpectedAction = "move_to", ExpectedTarget = "bridge", Outcome = outcome,
            };
        }

        private static ResidencyReading Reading(string when, params (string Name, long Size, long Vram)[] models)
        {
            var reading = new ResidencyReading { When = when, Checked = true };
            reading.Models.AddRange(models.Select(m => new ResidentModel { Name = m.Name, Size = m.Size, SizeVram = m.Vram }));
            return reading;
        }
    }
}
