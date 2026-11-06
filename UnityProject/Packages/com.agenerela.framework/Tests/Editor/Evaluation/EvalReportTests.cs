using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Evaluation;
using NUnit.Framework;
using static Agenerela.Tests.Evaluation.RunnerFixtures;

namespace Agenerela.Tests.Evaluation
{
    // The report an A/B run writes (#145): counts per arm, category and outcome, the wrong answers,
    // the conditions, and the noise check, as JSON and as Markdown.
    public sealed class EvalReportTests
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
            output = Path.Combine(Path.GetTempPath(), "agenerela-reports-" + Guid.NewGuid().ToString("N"));
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

        [TestCase(50, 10.0)]
        [TestCase(200, 5.0)]
        [TestCase(20, 15.811)]
        public void TheNoiseFloorScalesFromTenPointsAtFifty(int n, double floor)
        {
            Assert.That(NoiseCheck.FloorAt(n), Is.EqualTo(floor).Within(0.001));
        }

        [Test]
        public async Task ADifferenceSmallerThanTheNoiseFloorIsWithinNoise()
        {
            var report = await Run(Reply("move_to", "bridge"), Reply("move_to", "bridge"));

            var all = report.Noise[0];
            Assert.That(all.Scope, Is.EqualTo("all"));
            Assert.That(all.DifferencePoints, Is.EqualTo(0));
            Assert.That(all.WithinNoise, Is.True);
            Assert.That(report.ToMarkdown(), Does.Contain("within noise"));
        }

        [Test]
        public async Task ADifferenceLargerThanTheNoiseFloorIsNot()
        {
            // A answers every prompt with none, B moves to the bridge: each is right once in three,
            // so overall they tie, within noise; in the situation category A is right and B wrong,
            // 100 points apart at n = 1, beyond its 70.7-point floor.
            var report = await Run(Reply("none", "no_target"), Reply("move_to", "bridge"));

            Assert.That(report.Noise[0].DifferencePoints, Is.EqualTo(0));
            Assert.That(report.Noise[0].WithinNoise, Is.True);
            var situation = report.Noise.Single(c => c.Scope == "situation");
            Assert.That(situation.N, Is.EqualTo(1));
            Assert.That(situation.DifferencePoints, Is.EqualTo(-100));
            Assert.That(situation.NoiseFloorPoints, Is.EqualTo(70.71).Within(0.01));
            Assert.That(situation.WithinNoise, Is.False);
            Assert.That(report.ToMarkdown(), Does.Contain("beyond noise"));
        }

        [Test]
        public async Task CountsArePerArmPerCategoryPerOutcome()
        {
            var report = await Run(Reply("none", "no_target"), Reply("move_to", "bridge"));

            var a = report.Arms[0];
            var b = report.Arms[1];
            Assert.That(a.Category("command_with_target").WrongLegalAction, Is.EqualTo(2));
            Assert.That(a.Category("situation").Correct, Is.EqualTo(1));
            Assert.That(b.Category("command_with_target").Correct, Is.EqualTo(1));
            Assert.That(b.Category("command_with_target").WrongLegalAction, Is.EqualTo(1));
            Assert.That(b.Category("situation").WrongLegalAction, Is.EqualTo(1));
            Assert.That(a.Overall.Total, Is.EqualTo(3));
            Assert.That(a.Overall.Accuracy, Is.EqualTo(1.0 / 3).Within(1e-9));
        }

        [Test]
        public async Task TheReportCarriesItsConditions()
        {
            var report = await Run(Reply("move_to", "bridge"), Reply("move_to", "bridge"));

            var c = report.Conditions;
            Assert.That(c.FrameworkVersion, Is.EqualTo(AgenerelaInfo.Version));
            Assert.That(DateTime.TryParse(c.StartedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _), Is.True, c.StartedUtc);
            Assert.That(c.FinishedUtc, Is.Not.Empty);
            Assert.That(c.SetPath, Is.EqualTo(Path.Combine(fixtures.Folder, "agent.json")));
            Assert.That(c.Prompts, Is.EqualTo(3));
            Assert.That(c.SetCommit, Is.Null, "A temporary folder is not in git.");
            Assert.That(c.Profile, Is.EqualTo("VillageGuard"));
            Assert.That(c.GraphicsDevice, Is.Not.Null);
            Assert.That(c.Targets, Does.Contain("ExplicitTargetSource"));
            Assert.That(c.State, Does.Contain("PlainStateLines"));
            Assert.That(c.Arms.Select(arm => arm.ProviderConfig), Is.EqualTo(new[] { "FakeA", "FakeB" }));
            Assert.That(c.Arms.Select(arm => arm.CheckTarget), Is.EqualTo(new[] { true, false }));
            Assert.That(c.Arms.Select(arm => arm.Provider), Is.All.EqualTo("fake"));
            Assert.That(c.Order, Is.EqualTo(EvalArmOrder.Interleaved));
        }

        [Test]
        public async Task EveryAnswerThatIsNotCorrectIsListedWithTheModelsDecisionAndTheGuardsReason()
        {
            var report = await Run(Reply("stop_following", "no_target"), Reply("move_to", "bridge"));

            var wrong = report.NotCorrect.ToList();
            Assert.That(wrong.Select(w => w.Arm + " " + w.PromptId), Is.EquivalentTo(new[]
            {
                "A target_001", "A target_002", "A situation_001", "B target_002", "B situation_001",
            }));
            var contained = wrong.Single(w => w.Arm == "A" && w.PromptId == "situation_001");
            Assert.That(contained.ModelAction, Is.EqualTo("stop_following"));
            Assert.That(contained.GuardReason, Is.Not.Empty);

            string md = report.ToMarkdown();
            Assert.That(md, Does.Contain("## Answers that were not correct"));
            Assert.That(md, Does.Contain("stop_following/no_target").And.Contain(contained.GuardReason));
            Assert.That(md, Does.Contain("Walk with me a while."));
        }

        [Test]
        public async Task SavingWritesJsonAndMarkdownInAFolderNamedForTheRun()
        {
            var report = await Run(Reply("move_to", "bridge"), Reply("none", "no_target"));

            string folder = report.Save(output);
            string again = report.Save(output);

            Assert.That(Path.GetFileName(folder), Is.EqualTo(report.Name));
            Assert.That(report.Name, Does.EndWith("_A_vs_B"));
            Assert.That(again, Is.Not.EqualTo(folder), "A second save never writes over the first.");
            Assert.That(File.ReadAllText(Path.Combine(folder, EvalReport.MarkdownFile)), Does.StartWith("# Evaluation run " + report.Name));

            var back = EvalReport.FromJson(File.ReadAllText(Path.Combine(folder, EvalReport.JsonFile)));
            Assert.That(back.Answers.Select(x => x.Outcome), Is.EqualTo(report.Answers.Select(x => x.Outcome)));
            Assert.That(back.Arms[1].Overall.Correct, Is.EqualTo(report.Arms[1].Overall.Correct));
            Assert.That(back.Conditions.Arms[0].ProviderConfig, Is.EqualTo("FakeA"));
            Assert.That(back.Noise.Select(x => x.WithinNoise), Is.EqualTo(report.Noise.Select(x => x.WithinNoise)));
            Assert.That(File.ReadAllText(Path.Combine(folder, EvalReport.JsonFile)), Does.Contain("\"outcome\": \"WrongLegalAction\""),
                "Outcomes are written by name.");
        }

        private Task<EvalReport> Run(string replyA, string replyB)
        {
            var set = fixtures.Set(new[] { Bridge, Escort }, new[] { Quiet });
            var runner = new EvalRunner(set, Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Config(new FakeProvider(replyA), "FakeA")),
                new EvalArm("B", fixtures.Config(new FakeProvider(replyB), "FakeB"), new DecideOptions { CheckTarget = false }));
            return Fixtures.NotCancelled(runner.RunAsync());
        }
    }
}
