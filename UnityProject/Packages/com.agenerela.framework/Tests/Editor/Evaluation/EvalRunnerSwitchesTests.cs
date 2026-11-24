using System.Linq;
using System.Threading.Tasks;
using Agenerela.Evaluation;
using NUnit.Framework;
using static Agenerela.Tests.Evaluation.RunnerFixtures;

namespace Agenerela.Tests.Evaluation
{
    // What three of #151's runs need: an arm that summarises the targets (§2.8), categories the set's
    // own targets declare (#190 B, #210), and echoes told apart from parrots (#190 G14, #218). Every
    // run is awaited through Fixtures.NotCancelled, since the Test Framework counts a cancelled Task
    // as passed.
    public sealed class EvalRunnerSwitchesTests
    {
        private RunnerFixtures fixtures;

        [SetUp]
        public void SetUp()
        {
            fixtures = new RunnerFixtures();
        }

        [TearDown]
        public void TearDown()
        {
            fixtures.Dispose();
            Fixtures.DestroyCreated();
        }

        private static readonly string Bridge = Prompt("target_001", "Meet me over at the bridge.", "move_to", "bridge", false);

        private PromptSet PlacesAndCreatures()
        {
            var set = fixtures.Set(new[] { Bridge });
            set.Targets.Single(t => t.Id == "bridge").Category = "place";
            set.Targets.Single(t => t.Id == "docks").Category = "place";
            set.Targets.Single(t => t.Id == "wolf").Category = "creature";
            return set;
        }

        private static AgentProfile CategorisedProfile()
        {
            var profile = Fixtures.VillageGuardProfile();
            profile.Actions.Single(a => a.Action.Id == "move_to").Action.TargetCategories = new[] { "place" };
            profile.Actions.Single(a => a.Action.Id == "attack_target").Action.TargetCategories = new[] { "creature" };
            return profile;
        }

        // ---- Summarised targets (§2.8) ------------------------------------------------------------

        [Test]
        public async Task AnArmThatSummarisesTargetsSendsOneLinePerCategoryInPlaceOfOnePerTarget()
        {
            var a = new FakeProvider(Reply("move_to", "bridge"));
            var b = new FakeProvider(Reply("move_to", "bridge"));
            var armB = new EvalArm("summarised", fixtures.Config(b, "FakeB")) { SummariseTargets = true };
            var runner = new EvalRunner(PlacesAndCreatures(), CategorisedProfile(), new EvalArm("templated", fixtures.Config(a, "FakeA")), armB);

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            string templated = a.Requests.Last().Observations;
            Assert.That(templated, Does.Contain("- bridge: The wooden bridge.\n- docks: The docks.\n- wolf: A wolf."));
            string summarised = b.Requests.Last().Observations;
            Assert.That(summarised, Does.Contain("- isFollowing: false.\n- place: bridge (the wooden bridge) and docks (the docks).\n- creature: wolf (a wolf)."),
                "The summary takes the target lines' place, after the state's.");
            Assert.That(summarised, Does.Not.Contain("bridge: The wooden bridge"));
            Assert.That(b.Requests.Last().SystemPrompt, Is.EqualTo(a.Requests.Last().SystemPrompt), "Only the observations change.");
            Assert.That(report.Conditions.Arms.Select(c => c.SummariseTargets), Is.EqualTo(new[] { false, true }));
            Assert.That(report.Answers.Select(x => x.Outcome), Is.All.EqualTo(DecisionOutcome.Correct));
        }

        // ---- The set's own categories (#190 B, #210) ------------------------------------------------

        [Test]
        public async Task TheSetsCategoriesReachEveryArmSoPairingPairs()
        {
            var a = new FakeProvider(Reply("attack_target", "bridge"));
            var b = new FakeProvider(Reply("attack_target", "bridge"));
            var runner = new EvalRunner(PlacesAndCreatures(), CategorisedProfile(),
                new EvalArm("flat", fixtures.Config(a, "FakeA")),
                new EvalArm("paired", fixtures.Config(b, "FakeB"), new DecideOptions { PairTargetsWithActions = true }));

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            var pairs = b.Requests.Last().Schema.Pairs.Select(p => string.Join(", ", p.Actions) + " -> " + string.Join(", ", p.Targets)).ToList();
            Assert.That(pairs, Does.Contain("move_to -> bridge, docks"));
            Assert.That(pairs, Does.Contain("attack_target -> wolf"));
            Assert.That(a.Requests.Last().Schema.Pairs, Is.Null, "The flat arm's schema is not paired.");
            var flat = report.Answers.Single(x => x.Arm == "flat");
            Assert.That(flat.Guard, Is.EqualTo("TargetCategoryGuard"), "Its guard refuses the pair the categories forbid.");
        }

        [Test]
        public async Task AnArmsOwnLegalityStillOverridesTheSetsCategory()
        {
            var legality = new EvalLegality { Name = "bridge-as-creature" };
            legality.TargetCategories["bridge"] = "creature";
            var provider = new FakeProvider(Reply("move_to", "bridge"));
            var runner = new EvalRunner(PlacesAndCreatures(), CategorisedProfile(),
                new EvalArm("A", fixtures.Config(provider, "FakeA")), new EvalArm("B", fixtures.Config(provider, "FakeB")) { Legality = legality });

            var e = await EvalRunnerTests.Refused(runner);

            Assert.That(e.Message, Does.Contain("arm 'B'").And.Contain("move_to/bridge").And.Contain("illegal"));
        }

        [Test]
        public async Task ALabelTheSetsCategoriesMakeIllegalIsRefusedBeforeAnythingIsSent()
        {
            var set = PlacesAndCreatures();
            set.Targets.Single(t => t.Id == "bridge").Category = "creature";
            var provider = new FakeProvider(Reply("move_to", "bridge"));
            var runner = new EvalRunner(set, CategorisedProfile(),
                new EvalArm("A", fixtures.Config(provider, "FakeA")), new EvalArm("B", fixtures.Config(provider, "FakeB")));

            var e = await EvalRunnerTests.Refused(runner);

            Assert.That(e.Message, Does.Contain("target_001 expects move_to/bridge").And.Contain("takes place").And.Contain("in creature"));
            Assert.That(provider.Requests, Is.Empty);
            Assert.That(PromptSetChecks.CompareWithProfile(PlacesAndCreatures(), CategorisedProfile()), Is.Empty,
                "With the bridge a place the same label is legal.");
        }

        [Test]
        public void ATargetsCategoryIsReadAndWrittenWithTheSet()
        {
            var set = PlacesAndCreatures();
            set.Save();

            var again = PromptSet.Load(set.FilePath);

            Assert.That(again.Targets.Select(t => t.Category), Is.EqualTo(new[] { "place", "place", "creature" }));
            Assert.That(System.IO.File.ReadAllText(set.FilePath), Does.Contain("\"category\": \"creature\""));
            Assert.That(again.Targets[0].Extra, Is.Empty, "A known field, not an unknown one kept aside.");
        }
    }
}
