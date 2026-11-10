using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // What an agent's few-shot block shows beyond each action's own example (#190): the idle
    // example on an agent built in code (Living Vale #13), and each action's counter-example
    // (Living Vale #9). Both stay off until the A/B in #151; these pin how they are switched.
    public sealed class AgentExampleTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        // The end of every idle example's line, and of a counter-example's.
        private const string AnsweredWithNone = "-> action: none, target: no_target";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- The idle example on an agent built in code ----------------------------------------

        [Test]
        public async Task ACodeBuiltAgentHasNoIdleExampleUntilItIsGivenOne()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = CodeBuiltGuard(fake);
            Assert.That(agent.IdleExampleStimulus, Is.Null, "Off by default.");

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            string before = fake.LastRequest.FewShotBlock;
            agent.IdleExampleStimulus = AgentProfile.DefaultIdleExampleStimulus;
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            string given = fake.LastRequest.FewShotBlock;
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { IdleExampleStimulus = "The bell has not rung." }));
            string perCall = fake.LastRequest.FewShotBlock;
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { IdleExampleStimulus = "" }));
            string omitted = fake.LastRequest.FewShotBlock;

            Assert.That(IdleLines(before), Is.Empty, "A code-built agent has no profile to take one from.");
            Assert.That(IdleLines(given), Is.EqualTo(new[] { "Player: \"Nothing has changed since last time.\" " + AnsweredWithNone }),
                "The same idle example a new profile gives.");
            Assert.That(IdleLines(perCall), Is.EqualTo(new[] { "Player: \"The bell has not rung.\" " + AnsweredWithNone }),
                "A call's own still wins.");
            Assert.That(IdleLines(omitted), Is.Empty, "An empty one leaves it out of that call.");
        }

        [Test]
        public async Task TheAgentsIdleExampleReplacesItsProfilesAndACallsStillWins()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            string fromProfile = fake.LastRequest.FewShotBlock;
            agent.IdleExampleStimulus = "The bell has not rung.";
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            string agents = fake.LastRequest.FewShotBlock;
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { IdleExampleStimulus = "All is calm." }));
            string perCall = fake.LastRequest.FewShotBlock;
            agent.IdleExampleStimulus = "";
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            string omitted = fake.LastRequest.FewShotBlock;

            Assert.That(IdleLines(fromProfile), Is.EqualTo(new[] { "Player: \"You seem well rested today.\" " + AnsweredWithNone }),
                "Unset, a profile-built agent sends what it always did.");
            Assert.That(IdleLines(agents), Is.EqualTo(new[] { "Player: \"The bell has not rung.\" " + AnsweredWithNone }));
            Assert.That(IdleLines(perCall), Is.EqualTo(new[] { "Player: \"All is calm.\" " + AnsweredWithNone }));
            Assert.That(IdleLines(omitted), Is.Empty, "Empty leaves it out, the profile's included.");
        }

        // ---- Counter-examples ------------------------------------------------------------------

        [Test]
        public async Task ACounterExampleIsShownWhileItsActionIsOfferedUnlessTheCallLeavesItOut()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake, isFollowing: false);
            Definition(agent, "move_to").CounterExampleStimulus = "The {0} looks lovely at dusk.";
            Definition(agent, "stop_following").CounterExampleStimulus = "You walk too fast.";

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            var shown = fake.LastRequest.FewShotBlock.Split('\n').ToList();
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { CounterExamples = false }));
            string leftOut = fake.LastRequest.FewShotBlock;

            int moveTo = shown.IndexOf("Player: \"Proceed to the Tower.\" -> action: move_to, target: tower");
            Assert.That(moveTo, Is.GreaterThan(0));
            Assert.That(shown[moveTo + 1], Is.EqualTo("Player: \"The Tower looks lovely at dusk.\" " + AnsweredWithNone),
                "Right after the action's own example, its {0} filled the same way.");
            Assert.That(shown, Has.None.Contains("You walk too fast."),
                "stop_following is not offered while the guard is not following, so neither is its counter-example.");
            Assert.That(leftOut, Does.Not.Contain("lovely at dusk"));
            Assert.That(leftOut.Split('\n'), Has.Length.EqualTo(shown.Count - 1), "Off takes out that line and nothing else.");
            Assert.That(new DecideOptions().CounterExamples, Is.True, "On by default, which shows only what a developer wrote.");
        }

        // ---- Statements and the example's target (#190, G14 and Living Vale #20) -----------------

        [Test]
        public async Task AnIdleStatementNeverComesFromFurtherDownThanItsStimulus()
        {
            var fake = new FakeProvider(GoToTheTower);
            var profile = Fixtures.VillageGuardProfile();
            profile.IdleExampleStatement = "Rested and ready.";
            var agent = new Agent(profile, fake);
            agent.Bind("move_to", new FakeHandler());
            agent.Targets.Register("tower", new object());
            Assert.That(new Agent(Fixtures.VillageGuardProfile(), fake).IdleExampleStatement, Is.Null, "Off by default.");

            async Task<string> Idle(DecideOptions options = null)
            {
                await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", options));
                return IdleLines(fake.LastRequest.FewShotBlock).Single();
            }

            string profiles = await Idle();
            string callsStimulus = await Idle(new DecideOptions { IdleExampleStimulus = "The bell has not rung." });
            string callsBoth = await Idle(new DecideOptions { IdleExampleStimulus = "The bell has not rung.", IdleExampleStatement = "Not yet." });
            string callsStatement = await Idle(new DecideOptions { IdleExampleStatement = "All quiet here." });
            agent.IdleExampleStimulus = "All is calm.";
            string agentsStimulus = await Idle();
            agent.IdleExampleStimulus = null;
            agent.IdleExampleStatement = "Well rested, sir.";
            string agentsStatement = await Idle();

            Assert.That(profiles, Is.EqualTo("Player: \"You seem well rested today.\" -> action: none, target: no_target, statement: \"Rested and ready.\""));
            Assert.That(callsStimulus, Is.EqualTo("Player: \"The bell has not rung.\" " + AnsweredWithNone),
                "The profile's statement was written for the profile's line, not the call's.");
            Assert.That(callsBoth, Does.EndWith(", statement: \"Not yet.\""));
            Assert.That(callsStatement, Is.EqualTo("Player: \"You seem well rested today.\" -> action: none, target: no_target, statement: \"All quiet here.\""),
                "A call's statement may go on a stimulus from further down.");
            Assert.That(agentsStimulus, Is.EqualTo("Player: \"All is calm.\" " + AnsweredWithNone), "Nor on the agent's own line.");
            Assert.That(agentsStatement, Does.EndWith(", statement: \"Well rested, sir.\""));
        }

        [Test]
        public async Task TheCallsSwitchesReachTheExamples()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            Definition(agent, "move_to").ExampleStatement = "Heading for the {0}.";
            Definition(agent, "attack_target").PreferredExampleTargets = new[] { "scarecrow" };   // not registered

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            string defaults = fake.LastRequest.FewShotBlock;
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { ExampleStatements = false, ExampleTargetFallback = true, SpacedExampleNames = true }));
            string switched = fake.LastRequest.FewShotBlock;

            Assert.That(defaults, Does.Contain("\"Proceed to the Tower.\" -> action: move_to, target: tower, statement: \"Heading for the Tower.\""),
                "A written statement is shown by default, its {0} filled like the stimulus's.");
            Assert.That(defaults, Does.Not.Contain("action: attack_target"), "No fallback by default.");
            Assert.That(switched, Does.Not.Contain("statement:"), "ExampleStatements = false leaves them out.");
            Assert.That(switched, Does.Contain("\"Engage the training dummy.\" -> action: attack_target, target: training_dummy"),
                "ExampleTargetFallback = true names a target the action takes, skipping the tower the stimulus " +
                "mentions; SpacedExampleNames = true calls it by its id's words.");
            Assert.That(switched, Does.Contain("\"Proceed to the tower.\" -> action: move_to, target: tower"),
                "A registered preferred target is still used, mentioned or not.");
            Assert.That(switched, Does.Contain("\"Proceed to Atlantis.\" -> action: none"), "The spaced refusal drops the article.");
            Assert.That(defaults, Does.Contain("\"Proceed to the Atlantis.\" -> action: none"), "Not by default.");
            Assert.That(new DecideOptions().ExampleTargetFallback, Is.False);
            Assert.That(new DecideOptions().SpacedExampleNames, Is.False);
            Assert.That(new DecideOptions().ExampleStatements, Is.True, "On, which shows only what a developer wrote.");
        }

        private static ActionDefinition Definition(Agent agent, string id)
        {
            Assert.That(agent.Actions.TryGet(id, out var definition, out _), Is.True, $"'{id}' is not bound.");
            return definition;
        }

        // The village guard's identity and its move_to, built in code with no asset (DR-011).
        private static Agent CodeBuiltGuard(FakeProvider fake)
        {
            var moveTo = new ActionDefinition
            {
                Id = "move_to",
                Description = "Walk to a named place.",
                RequiresTarget = true,
                ExampleStimulus = "Proceed to the {0}.",
                PreferredExampleTargets = new[] { "tower" },
            };

            var agent = new Agent(Fixtures.VillageGuardIdentity(), new[] { moveTo }, fake);
            agent.Bind("move_to", new FakeHandler());
            agent.Targets.Register("tower", new object());
            return agent;
        }

        // The idle example's line, or none, with its statement when it has one. The refusal example
        // also answers none, but always ends with "(not an available target)". Only for an agent
        // with no counter-examples, whose lines answer none too.
        private static string[] IdleLines(string fewShotBlock)
        {
            return fewShotBlock.Split('\n')
                .Where(line => line.Contains(AnsweredWithNone) && !line.EndsWith("(not an available target)"))
                .ToArray();
        }
    }
}
