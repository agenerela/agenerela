using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Editor.Evaluation;
using Agenerela.Editor.UI;
using Agenerela.Evaluation;
using Agenerela.Tests.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Agenerela.Tests.Evaluation.RunnerFixtures;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// The Evaluation Report window's tree (#179): built from its UXML on the #167 kit and read
    /// directly, with no window, so what #146 could only check by hand is a test. The one test that
    /// needs layout opens a window, and is ignored in a run with no graphics device.
    /// </summary>
    public sealed class EvaluationReportWindowTests
    {
        private RunnerFixtures fixtures;
        private EvaluationReportUi ui;

        private static readonly string Bridge = Prompt("target_001", "Meet me over at the bridge.", "move_to", "bridge", false);

        [SetUp]
        public void SetUp()
        {
            fixtures = new RunnerFixtures();
            ui = new EvaluationReportUi(EvaluationReportUi.Load());
        }

        [TearDown]
        public void TearDown()
        {
            EditorUiFixtures.CloseAll();
            fixtures.Dispose();
            Fixtures.DestroyCreated();
        }

        // ---- Run A/B, greyed with its reason -------------------------------------------------------

        [Test]
        public void RunIsGreyedOutWithItsReasonUntilTheSetUpIsComplete()
        {
            ui.Show(new EvaluationReportUi.State { WhyNotRunnable = "Choose a prompt set first (step 1)." });

            var run = Find<Button>(EvaluationReportUi.RunName);
            Assert.That(run.enabledSelf, Is.False);
            Assert.That(Find<Label>(EvaluationReportUi.RunReasonName).text, Is.EqualTo("Choose a prompt set first (step 1)."));

            ui.Show(new EvaluationReportUi.State { Setup = Ready(), WhyNotRunnable = null });

            Assert.That(run.enabledSelf, Is.True);
            Assert.That(Find<Label>(EvaluationReportUi.RunReasonName).text, Does.StartWith("Ready."));
        }

        [Test]
        public void TheStartCardShowsUntilItCanRunAndNamesWhatTheProjectLacks()
        {
            ui.Show(new EvaluationReportUi.State { WhyNotRunnable = "Choose a prompt set first (step 1).", ArmsInSettings = false });

            Assert.That(Shown(EvaluationReportUi.StartName), Is.True);
            Assert.That(Shown(EvaluationReportUi.UseDefaultsName), Is.False, "The card's button is the one primary; the toolbar's would be a second.");
            Assert.That(Find<Button>(EvaluationReportUi.StartDefaultsName).ClassListContains(AgenerelaButtons.PrimaryClass), Is.True);
            Assert.That(Find(EvaluationReportUi.StepsName).childCount, Is.EqualTo(4));
            var problem = Find<ProblemBox>("arms-problem");
            Assert.That(problem.Message, Does.Contain("Project Settings → Agenerela"));
            Assert.That(problem.HasFix, Is.True);

            ui.Show(new EvaluationReportUi.State { Setup = Ready(), WhyNotRunnable = null });

            Assert.That(Shown(EvaluationReportUi.StartName), Is.False);
            Assert.That(Shown(EvaluationReportUi.UseDefaultsName), Is.True);
        }

        [Test]
        public void ASetTheRunnerWouldRefuseListsItsProblemsInAProblemBox()
        {
            var setup = Ready();
            setup.Set = fixtures.Set(new[] { Prompt("target_001", "Meet me at the castle.", "move_to", "castle", false) });

            ui.Show(new EvaluationReportUi.State { Setup = setup, WhyNotRunnable = setup.WhyNotRunnable() });

            Assert.That(Find<ProblemBox>("set-problem").Message, Does.Contain("castle"));
            Assert.That(Find<Button>(EvaluationReportUi.RunName).enabledSelf, Is.False);
        }

        // ---- B · A run --------------------------------------------------------------------------

        [Test]
        public void ARunShowsItsProgressAndCancelInPlaceOfRun()
        {
            ui.Show(new EvaluationReportUi.State { Setup = Ready(), Running = true, Seconds = 80 });
            ui.ShowProgress(new EvalProgress(EvalPhase.Asking, "guards-on", 37, 152), 80);

            Assert.That(Shown(EvaluationReportUi.ProgressRowName), Is.True);
            Assert.That(Shown(EvaluationReportUi.RunName), Is.False);
            var bar = Find<ProgressBar>(EvaluationReportUi.ProgressName);
            Assert.That(bar.title, Is.EqualTo("Asking · guards-on · 37 of 152 answers"));
            Assert.That(bar.value, Is.EqualTo(37f / 152).Within(1e-5));
            Assert.That(Find<Label>("elapsed").text, Does.StartWith("1 min 20 s so far"));
        }

        // ---- C, D · A report ----------------------------------------------------------------------

        [Test]
        public void ASavedReportShowsAllSixParts()
        {
            var view = new EvaluationReportView(Saved(Report()));

            ui.Show(new EvaluationReportUi.State { Setup = Ready(), View = view, Agreement = Agreement(), ShownPath = "" });

            Assert.That(Shown(EvaluationReportUi.ReportName), Is.True);
            Assert.That(Shown(EvaluationReportUi.NoReportName), Is.False);
            // ① folded to one line, Run A/B on it.
            Assert.That(Shown(EvaluationReportUi.SetupFieldsName), Is.False);
            Assert.That(Find<Label>(EvaluationReportUi.SetupSummaryName).text, Does.Contain("control vs guards-on"));
            Assert.That(Find<Button>(EvaluationReportUi.RunName).parent.name, Is.EqualTo("setup-header-run"));
            // ② a bar per arm and a row per outcome.
            Assert.That(Find(EvaluationReportUi.OutcomesName).Q("bar-a"), Is.Not.Null);
            Assert.That(Find(EvaluationReportUi.OutcomesName).Q("bar-b"), Is.Not.Null);
            Assert.That(Find("outcome-table").childCount, Is.EqualTo(6), "A heading and the five outcomes.");
            Assert.That(Find("outcome-Correct").Children().Last(), Is.InstanceOf<Label>());
            Assert.That(((Label)Find("outcome-Correct").Children().Last()).text, Does.EndWith(view.OutcomeRows()[0].Noise));
            // ③ the noise check's sentence.
            Assert.That(Find<Label>("noise-line").text, Is.EqualTo(view.NoiseLine));
            // ④ a row per category, its id a chip.
            Assert.That(Find("category-situation").Q<Chip>().Id, Is.EqualTo("situation"));
            // ⑤ arm B's answers that were not correct.
            Assert.That(Find(EvaluationReportUi.FailureListName).childCount, Is.EqualTo(2));
            // ⑥ agreement in the Prompt Set window's words.
            Assert.That(Find<Label>(EvaluationReportUi.AgreementHeadlineName).text, Is.EqualTo(EvaluationReportView.AgreementHeadline(Agreement())));
        }

        [Test]
        public void TheFailureFilterAndArmNarrowTheList()
        {
            var state = new EvaluationReportUi.State { Setup = Ready(), View = new EvaluationReportView(Saved(Report())) };
            ui.Show(state);
            Assert.That(Ids(), Is.EqualTo(new[] { "failure-target_001", "failure-situation_001" }));

            ui.FailureOutcome = DecisionOutcome.PipelineError;
            ui.Show(state);
            Assert.That(Ids(), Is.EqualTo(new[] { "failure-situation_001" }));
            Assert.That(Find<SectionTitle>("failures-heading").text, Does.Contain("1 PROMPT"));

            ui.FailureOutcome = null;
            ui.FailureArm = 0;
            ui.Show(state);
            Assert.That(Ids(), Is.EqualTo(new[] { "failure-situation_001" }), "Arm A got target_001 right.");

            IEnumerable<string> Ids() => Find(EvaluationReportUi.FailureListName).Children().Select(c => c.name);
        }

        [Test]
        public void ACancelledReportSaysSoAboveEverything()
        {
            var report = Report();
            report.Cancelled = true;
            var view = new EvaluationReportView(Saved(report));

            ui.Show(new EvaluationReportUi.State { Setup = Ready(), View = view });

            Assert.That(Find<ProblemBox>("cancelled-problem").Message, Is.EqualTo(view.CancelledLine));

            ui.Show(new EvaluationReportUi.State { Setup = Ready(), View = new EvaluationReportView(Saved(Report())) });

            Assert.That(Find(EvaluationReportUi.CancelledName).childCount, Is.Zero);
        }

        [Test]
        public void SetUpOpensAgainWhenTheReportGoes()
        {
            var state = new EvaluationReportUi.State { Setup = Ready(), View = new EvaluationReportView(Saved(Report())) };
            ui.Show(state);
            Assert.That(ui.SetupOpen, Is.False);

            ui.Show(new EvaluationReportUi.State { Setup = Ready() });

            Assert.That(ui.SetupOpen, Is.True);
            Assert.That(Shown(EvaluationReportUi.SetupFieldsName), Is.True);
            Assert.That(Find<Button>(EvaluationReportUi.RunName).parent.name, Is.EqualTo("run-row"));
        }

        // ---- At the minimum width ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator AtItsMinimumWidthNoButtonOrSectionRunsPastTheWindow()
        {
            ui.Show(new EvaluationReportUi.State { Setup = Ready(), View = new EvaluationReportView(Saved(Report())), Agreement = Agreement() });
            using (var tree = EditorUiFixtures.Host(ui.Root, new Vector2(560, 900)))
            {
                yield return EditorUiFixtures.Until(() => ui.Root.layout.width > 0, "the window to lay out");

                float right = ui.Root.worldBound.xMax + 0.5f;
                var past = ui.Root.Query<VisualElement>()
                    .Where(e => (e is Button || e.ClassListContains("er-section") || e.ClassListContains("er-table-row") || e is ProblemBox)
                                && e.resolvedStyle.display == DisplayStyle.Flex && e.worldBound.width > 0 && e.worldBound.xMax > right)
                    .ToList();

                Assert.That(past.Select(e => $"{e.name} ({e.GetType().Name}) ends at {e.worldBound.xMax:0} of {right:0}"), Is.Empty);
            }
        }

        // ---- Fixtures ---------------------------------------------------------------------------

        private VisualElement Find(string name) => ui.Root.Find<VisualElement>(name);

        private T Find<T>(string name) where T : VisualElement => ui.Root.Find<T>(name);

        private bool Shown(string name) => Find(name).style.display != DisplayStyle.None;

        // A set, a profile and one config in both arms: ready to run.
        private EvaluationRunSetup Ready()
        {
            var setup = new EvaluationRunSetup { Set = fixtures.Set(new[] { Bridge }), Profile = Fixtures.VillageGuardProfile() };
            var config = fixtures.Config(new FakeProvider(), "OllamaLocal");
            setup.ChangeConfig(0, config);
            setup.ChangeConfig(1, config);
            return setup;
        }

        private static EvalReport Saved(EvalReport report) => EvalReport.FromJson(report.ToJson());

        // Two prompts in two categories; arm A gets the command right and the situation wrong, arm B
        // has the command contained and loses the situation to a timeout.
        private static EvalReport Report()
        {
            var report = new EvalReport
            {
                Name = "2026-11-06_10-00-00_control_vs_guards-on",
                Conditions = new EvalConditions
                {
                    StartedUtc = "2026-11-06T10:00:00Z", FinishedUtc = "2026-11-06T10:01:30Z", FrameworkVersion = "0.4.0",
                    SetPath = "C:/Games/Village/Assets/Evaluation/agent.json", SetAgent = "Village guard", Prompts = 2, Profile = "VillageGuard",
                },
            };
            foreach (var (name, check) in new[] { ("control", false), ("guards-on", true) })
            {
                report.Conditions.Arms.Add(new EvalArmConditions { Name = name, ProviderConfig = "OllamaLocal", Provider = "ollama", Model = "qwen3.5:2b", CheckTarget = check });
            }

            var a = new EvalArmResult { Name = "control" };
            var b = new EvalArmResult { Name = "guards-on" };
            a.Add("command_with_target", DecisionOutcome.Correct);
            a.Add("situation", DecisionOutcome.WrongLegalAction);
            b.Add("command_with_target", DecisionOutcome.ContainedByGuard);
            b.Add("situation", DecisionOutcome.PipelineError);
            report.Arms.Add(a);
            report.Arms.Add(b);
            report.Noise.Add(NoiseCheck.Of("all", a.Overall, b.Overall));
            report.Noise.Add(NoiseCheck.Of("command_with_target", a.Category("command_with_target"), b.Category("command_with_target")));
            report.Noise.Add(NoiseCheck.Of("situation", a.Category("situation"), b.Category("situation")));
            report.Answers.Add(Answer("control", "target_001", "command_with_target", DecisionOutcome.Correct));
            report.Answers.Add(Answer("guards-on", "target_001", "command_with_target", DecisionOutcome.ContainedByGuard));
            report.Answers.Add(Answer("control", "situation_001", "situation", DecisionOutcome.WrongLegalAction));
            report.Answers.Add(Answer("guards-on", "situation_001", "situation", DecisionOutcome.PipelineError));
            return report;
        }

        private static EvalAnswer Answer(string arm, string id, string category, DecisionOutcome outcome)
        {
            return new EvalAnswer
            {
                Arm = arm, PromptId = id, Category = category, Label = "Player", Stimulus = "Meet me over at the bridge.",
                ExpectedAction = "move_to", ExpectedTarget = "bridge", Outcome = outcome,
                ModelAction = outcome == DecisionOutcome.PipelineError ? null : "move_to",
                ModelTarget = outcome == DecisionOutcome.PipelineError ? null : "docks",
                ErrorKind = outcome == DecisionOutcome.PipelineError ? "Timeout" : null,
            };
        }

        private AgreementReport Agreement()
        {
            var set = fixtures.Set(new[] { Bridge });
            return AgreementReport.Compute(set, set.LoadLabels());
        }
    }
}
