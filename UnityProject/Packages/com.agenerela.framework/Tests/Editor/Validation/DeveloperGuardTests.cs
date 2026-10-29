using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // A game's own guards (#108): Agent.Guards, DelegateGuard, a guard that throws, and the two
    // worked examples of Documentation~/guards.md, so the docs cannot rot. Every await of
    // DecideAsync goes through Fixtures.NotCancelled (see AgentTests).
    public sealed class DeveloperGuardTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- DelegateGuard ------------------------------------------------------------------

        [Test]
        public void ADelegateGuardNeedsANameAndAFunction()
        {
            Func<GuardInput, GuardVerdict> pass = _ => GuardVerdict.Pass;

            foreach (string blank in new[] { null, "", "  " })
            {
                Assert.Throws<ArgumentException>(() => new DelegateGuard(blank, pass));
            }

            Assert.Throws<ArgumentNullException>(() => new DelegateGuard("rule", null));
            Assert.That(new DelegateGuard("rule", pass).Name, Is.EqualTo("rule"));
        }

        [Test]
        public void ADelegateGuardAnswersWithItsFunction()
        {
            GuardInput seen = null;
            var guard = new DelegateGuard("rule", input =>
            {
                seen = input;
                return GuardVerdict.RewriteToNone("because.");
            });
            var agent = Fixtures.VillageGuard(new FakeProvider());
            var input = new GuardInput(Context(agent, ""), null, null, new AgentDecision("move_to", "tower", ""), null, null);

            var verdict = guard.Inspect(input);

            Assert.That(seen, Is.SameAs(input));
            Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(verdict.Reason, Is.EqualTo("because."));
        }

        // ---- Agent.Guards -------------------------------------------------------------------

        [Test]
        public void AnAgentStartsWithNoGuardsOfItsOwn()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider());

            Assert.That(agent.Guards, Is.Empty, "The built-in guards are never in the list.");
        }

        [Test]
        public async Task DeveloperGuardsRunInListOrderAndStopAtTheFirstThatDoesNotPass()
        {
            var ran = new List<string>();
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Guards.Add(new DelegateGuard("first", _ => { ran.Add("first"); return GuardVerdict.Pass; }));
            agent.Guards.Add(new DelegateGuard("second", _ => { ran.Add("second"); return GuardVerdict.RewriteToNone("second says no."); }));
            agent.Guards.Add(new DelegateGuard("third", _ => { ran.Add("third"); return GuardVerdict.Pass; }));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(ran, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "second" }));
        }

        [Test]
        public async Task ABuiltInGuardThatStopsTheDecisionSkipsTheDevelopersGuards()
        {
            var ran = new List<string>();
            var agent = Fixtures.VillageGuard(new FakeProvider("{\"action\":\"dance\",\"target\":\"no_target\",\"statement\":\"\"}"));
            agent.Guards.Add(new DelegateGuard("mine", _ => { ran.Add("mine"); return GuardVerdict.Pass; }));
            LogAssert.Expect(LogType.Warning, new Regex("SchemaLegalityGuard rejected 'dance'"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Dance for me."));

            Assert.That(ran, Is.Empty);
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "SchemaLegalityGuard" }));
        }

        [Test]
        public async Task ADevelopersRewriteShowsExactlyLikeABuiltIns()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Guards.Add(new DelegateGuard("tower_locked", _ => GuardVerdict.RewriteToNone("the tower is locked at night.")));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.ModelDecision.ActionId, Is.EqualTo("move_to"));
            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("tower_locked"));
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "tower_locked" }));
            Assert.That(result.Telemetry.Verdict, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(result.Telemetry.GuardReason, Is.EqualTo("the tower is locked at night."));
            Assert.That(result.Telemetry.Outcome, Is.Null, "A refusal is a decision, not a pipeline error.");

            Assert.That(agent.Execute(result), Is.True, "A contained refusal: the agent does none.");
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task ADevelopersRejectShowsExactlyLikeABuiltIns()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Guards.Add(new DelegateGuard("strict", _ => GuardVerdict.Reject("the provider broke the game's contract.")));
            LogAssert.Expect(LogType.Warning,
                "Agent 'Village Guard' will do nothing: strict rejected 'move_to' on 'tower': the provider broke the game's contract.");

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("strict"));
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "strict" }));
            Assert.That(result.Telemetry.Verdict, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(result.Telemetry.GuardReason, Is.EqualTo("the provider broke the game's contract."));

            Assert.That(agent.Execute(result), Is.False);
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task ExecuteDoesNotRunTheDevelopersGuardsAgain()
        {
            int calls = 0;
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Guards.Add(new DelegateGuard("counted", _ => { calls++; return GuardVerdict.Pass; }));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            Assert.That(agent.Execute(result), Is.True);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Has.Count.EqualTo(1));
        }

        // ---- A guard that fails -------------------------------------------------------------

        [Test]
        public async Task AGuardThatThrowsIsAPipelineErrorNamingIt()
        {
            var ran = new List<string>();
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Guards.Add(new DelegateGuard("fine", _ => GuardVerdict.Pass));
            agent.Guards.Add(new DelegateGuard("broken", _ => throw new InvalidOperationException("the wolf is gone.")));
            agent.Guards.Add(new DelegateGuard("later", _ => { ran.Add("later"); return GuardVerdict.Pass; }));
            const string Expected = "Agent 'Village Guard' could not decide. The guard 'broken' (Guards[1]) failed. "
                                    + "InvalidOperationException: the wolf is gone.";
            LogAssert.Expect(LogType.Warning, Expected);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision, Is.Null);
            Assert.That(result.Verdict, Is.Null, "Not a refusal.");
            Assert.That(result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.GuardFailed));
            Assert.That(result.Telemetry.ErrorMessage, Is.EqualTo(Expected));
            Assert.That(result.Telemetry.GuardsFired, Is.Empty);
            Assert.That(ran, Is.Empty, "Nothing runs after a guard that failed.");

            Assert.That(agent.Execute(result), Is.False);
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Is.Empty);
            Assert.That(((RollingHistory)agent.Memory).Recall(Context(agent, "")), Is.Empty, "A pipeline error remembers nothing.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task ANullGuardIsAPipelineErrorNamingItsPlace()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Guards.Add(new DelegateGuard("fine", _ => GuardVerdict.Pass));
            agent.Guards.Add(null);
            LogAssert.Expect(LogType.Warning, new Regex(@"The guard Guards\[1\] failed\. InvalidOperationException: Guards\[1\] is null\.$"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision, Is.Null);
            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.GuardFailed));
        }

        [Test]
        public async Task AGuardWhoseNameThrowsIsNamedByItsType()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Guards.Add(new NamelessGuard());
            LogAssert.Expect(LogType.Warning, new Regex(@"The guard 'NamelessGuard' \(Guards\[0\]\) failed\. NotImplementedException"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.GuardFailed),
                "Its rewrite needs its name, which throws: the developer's code failed.");
        }

        // ---- Worked example 1: a cooldown, as guards.md writes it ---------------------------

        [Test]
        public async Task TheBellCooldownRefusesTheBellForAThreatItRangForRecently()
        {
            float now = 100f;
            var rangAt = new Dictionary<string, float> { ["wolf"] = 85f };
            var fake = new FakeProvider(Alarm("wolf"));
            var agent = Watchman(fake);
            agent.Guards.Add(BellCooldown(rangAt, () => now));

            var again = await Fixtures.NotCancelled(agent.DecideAsync("A wolf at the fence."));
            Assert.That(again.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(again.Telemetry.GuardsFired, Is.EqualTo(new[] { "bell_cooldown" }));
            Assert.That(again.Telemetry.GuardReason, Is.EqualTo("the bell rang for wolf less than 30 seconds ago."));

            fake.Reply = Alarm("bandit");
            var other = await Fixtures.NotCancelled(agent.DecideAsync("A bandit on the road."));
            Assert.That(other.Decision.ActionId, Is.EqualTo("sound_alarm"), "Another threat rings the bell.");

            now = 116f;
            fake.Reply = Alarm("wolf");
            var later = await Fixtures.NotCancelled(agent.DecideAsync("The wolf again."));
            Assert.That(later.Decision.ActionId, Is.EqualTo("sound_alarm"), "Past the cooldown, it rings again.");
            Assert.That(later.Telemetry.GuardsFired, Is.Empty);
        }

        // ---- Worked example 2: the name check DR-016 dropped --------------------------------

        [TestCase("Attack the training dummy.", "Player", true)]
        [TestCase("Hit the mannequin.", "Player", false)]
        [TestCase("Something moves by the dummy.", "Report", true)]
        public async Task TheNameCheckPassesOnlyANamedTarget(string stimulus, string label, bool passes)
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(
                "{\"action\":\"attack_target\",\"target\":\"training_dummy\",\"statement\":\"Have at you!\"}"));
            agent.Guards.Add(NameCheck());

            var result = await Fixtures.NotCancelled(agent.DecideAsync(stimulus, new DecideOptions { StimulusLabel = label }));

            if (passes)
            {
                Assert.That(result.Decision.ActionId, Is.EqualTo("attack_target"));
                Assert.That(result.Telemetry.GuardsFired, Is.Empty);
            }
            else
            {
                // The cost DR-016 measured: a paraphrase of a good request is refused.
                Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
                Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "name_check" }));
                Assert.That(result.Telemetry.GuardReason, Is.EqualTo("the player never named 'training dummy'."));
            }
        }

        [Test]
        public async Task TheNameCheckPassesADecisionWithNoTarget()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(
                "{\"action\":\"follow_player\",\"target\":\"no_target\",\"statement\":\"Lead on.\"}"));
            agent.Guards.Add(NameCheck());

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Come with me."));

            Assert.That(result.Decision.ActionId, Is.EqualTo("follow_player"));
        }

        // ---- The examples, as guards.md shows them ------------------------------------------

        // guards.md reads Time.time; the test passes its own clock.
        private static DelegateGuard BellCooldown(Dictionary<string, float> rangAt, Func<float> now)
        {
            return new DelegateGuard("bell_cooldown", input =>
            {
                var d = input.Decision;
                return d.ActionId == "sound_alarm"
                       && rangAt.TryGetValue(d.TargetId, out float at)
                       && now() - at < 30f
                    ? GuardVerdict.RewriteToNone($"the bell rang for {d.TargetId} less than 30 seconds ago.")
                    : GuardVerdict.Pass;
            });
        }

        private static DelegateGuard NameCheck()
        {
            return new DelegateGuard("name_check", input =>
            {
                var d = input.Decision;
                if (input.Options.StimulusLabel != "Player" || d.TargetId == TargetRegistry.NoTarget)
                {
                    return GuardVerdict.Pass;
                }

                string name = d.TargetId.Replace('_', ' ');
                return input.Context.Stimulus.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0
                    ? GuardVerdict.Pass
                    : GuardVerdict.RewriteToNone($"the player never named '{name}'.");
            });
        }

        // ---- Helpers ------------------------------------------------------------------------

        // The village guard with a bell to ring at a threat, and two threats in sight.
        private static Agent Watchman(FakeProvider fake)
        {
            var agent = Fixtures.VillageGuard(fake);
            agent.Actions.Register(new ActionDefinition
            {
                Id = "sound_alarm",
                Description = "Ring the bell at a threat.",
                RequiresTarget = true,
                ExampleStimulus = "Raise the alarm about the {0}.",
            }, new FakeHandler());
            agent.Targets.Register("wolf", new object());
            agent.Targets.Register("bandit", new object());
            return agent;
        }

        private static string Alarm(string target)
        {
            return $"{{\"action\":\"sound_alarm\",\"target\":\"{target}\",\"statement\":\"To arms!\"}}";
        }

        private static AgentContext Context(Agent agent, string stimulus)
        {
            return new AgentContext(agent.Identity, stimulus, null, null, agent.Targets, agent.Actions);
        }

        // A guard whose Name throws, and which rewrites everything, so the pipeline must read it.
        private sealed class NamelessGuard : IDecisionGuard
        {
            public string Name => throw new NotImplementedException("no name yet.");

            public GuardVerdict Inspect(GuardInput input)
            {
                return GuardVerdict.RewriteToNone("everything.");
            }
        }
    }
}
