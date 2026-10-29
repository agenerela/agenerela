using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // The guard pipeline (#106), through Agent.DecideAsync and FakeProvider: what a verdict does to
    // the result, the telemetry and the memory, the order guards run in, and Execute's re-check.
    // Every await of DecideAsync goes through Fixtures.NotCancelled (see AgentTests).
    public sealed class GuardPipelineTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- What SchemaLegalityGuard rejects inside DecideAsync -----------------------------

        [TestCase("stop_following", "no_target", "the action was not offered: it was not available for this decision.")]
        [TestCase("dance", "no_target", "the action is not registered.")]
        [TestCase("move_to", "godzilla", "the target is not registered.")]
        [TestCase("move_to", "no_target", "the action needs a target, and the decision has none.")]
        public async Task AnIllegalAnswerIsRejectedAndNothingRuns(string action, string target, string reason)
        {
            var fake = new FakeProvider(Reply(action, target, "Right away."));
            var agent = Fixtures.VillageGuard(fake);
            LogAssert.Expect(LogType.Warning,
                $"Agent 'Village Guard' will do nothing: SchemaLegalityGuard rejected '{action}' on '{target}': {reason}");

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Do something."));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.Decision.Statement, Is.EqualTo("Right away."), "The statement is #109's to change.");
            Assert.That(result.ModelDecision.ActionId, Is.EqualTo(action));
            Assert.That(result.ModelDecision.TargetId, Is.EqualTo(target));
            Assert.That(result.ModelDecision.Statement, Is.EqualTo("Right away."));

            Assert.That(result.Verdict.HasValue, Is.True);
            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("SchemaLegalityGuard"));
            Assert.That(result.Verdict.Value.Reason, Is.EqualTo(reason));

            var telemetry = result.Telemetry;
            Assert.That(telemetry.GuardsFired, Is.EqualTo(new[] { "SchemaLegalityGuard" }));
            Assert.That(telemetry.Verdict, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(telemetry.GuardReason, Is.EqualTo(reason));
            Assert.That(telemetry.Outcome, Is.Null, "A rejection is a decision, not a pipeline error; Phase 4 classifies it.");

            Assert.That(agent.Execute(result), Is.False, "A rejected answer is never carried out.");
            Assert.That(Executions(agent), Is.Empty);
            Assert.That(telemetry.GuardsFired, Has.Count.EqualTo(1), "Execute does not check a rejected answer again.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task ALegalAnswerPassesWithNothingRecorded()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(result.ModelDecision, Is.SameAs(result.Decision));
            Assert.That(result.Verdict, Is.Null);
            Assert.That(result.Telemetry.GuardsFired, Is.Empty);
            Assert.That(result.Telemetry.Verdict, Is.Null);
            Assert.That(result.Telemetry.GuardReason, Is.Null);

            Assert.That(agent.Execute(result), Is.True);
            Assert.That(result.Telemetry.GuardsFired, Is.Empty, "Execute's re-check passed it too.");
        }

        // ---- A rewrite ----------------------------------------------------------------------

        [Test]
        public async Task ARewriteBecomesNoneKeepsTheModelsAnswerAndIsNotAnError()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("NotThatTower", "the tower was the wrong one."));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.Decision.Statement, Is.EqualTo("On my way."));
            Assert.That(result.ModelDecision.ActionId, Is.EqualTo("move_to"));
            Assert.That(result.ModelDecision.TargetId, Is.EqualTo("tower"));

            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("NotThatTower"));
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "NotThatTower" }));
            Assert.That(result.Telemetry.Verdict, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(result.Telemetry.GuardReason, Is.EqualTo("the tower was the wrong one."));

            Assert.That(agent.Execute(result), Is.True, "A contained refusal: the agent does none, which succeeds.");
            Assert.That(Executions(agent), Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        // ---- Memory -------------------------------------------------------------------------

        [TestCase(false)]
        [TestCase(true)]
        public async Task MemoryRemembersTheGuardedDecisionNotTheModels(bool reject)
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(reject ? FakeGuard.Rejecting("Stopper") : FakeGuard.Rewriting("Stopper"));
            if (reject)
            {
                LogAssert.Expect(LogType.Warning, new Regex("Stopper rejected 'move_to' on 'tower'"));
            }

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            var history = ((RollingHistory)agent.Memory).Recall(Context(agent));
            Assert.That(history, Is.EqualTo(new[]
            {
                "Player: \"Go to the tower.\"",
                "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"On my way.\"}",
            }), "The agent did nothing, so the history says so.");

            agent.Guards.Clear();
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            Assert.That(fake.LastRequest.History[1], Does.Not.Contain("move_to"), "What the model reads next is the guarded decision.");
        }

        // ---- Order, and stopping ------------------------------------------------------------

        [Test]
        public async Task GuardsRunInOrderAndTheFirstThatDoesNotPassStopsThem()
        {
            var ran = new List<string>();
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var first = new FakeGuard("First", _ => { ran.Add("First"); return GuardVerdict.Pass; });
            var second = new FakeGuard("Second", _ => { ran.Add("Second"); return GuardVerdict.RewriteToNone("second says no."); });
            var third = new FakeGuard("Third", _ => { ran.Add("Third"); return GuardVerdict.Reject("never asked."); });
            agent.Guards.Add(first);
            agent.Guards.Add(second);
            agent.Guards.Add(third);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(ran, Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(third.Inputs, Is.Empty);
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "Second" }), "A guard that passes is not recorded.");
            Assert.That(second.Inputs[0].Decision, Is.SameAs(result.ModelDecision), "Each guard sees the parsed decision.");
        }

        [Test]
        public async Task TheBuiltInGuardRunsFirstAndARejectStopsTheRest()
        {
            var fake = new FakeProvider(Reply("dance", "no_target", ""));
            var agent = Fixtures.VillageGuard(fake);
            var after = FakeGuard.Rewriting("After");
            agent.Guards.Add(after);
            LogAssert.Expect(LogType.Warning, new Regex("SchemaLegalityGuard rejected 'dance'"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Dance for me."));

            Assert.That(after.Inputs, Is.Empty);
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "SchemaLegalityGuard" }));
        }

        [Test]
        public async Task AGuardWithNoNameIsRecordedByItsType()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting(" "));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "FakeGuard" }));
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("FakeGuard"));
        }

        // ---- What a guard is handed ---------------------------------------------------------

        [Test]
        public async Task AGuardIsHandedTheContextRequestReplyOptionsAndTelemetryOfItsDecision()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            var spy = new FakeGuard("Spy", input =>
            {
                input.Telemetry.GuardsFired.Add("Spy wrote here");
                return GuardVerdict.Pass;
            });
            agent.Guards.Add(spy);
            var options = new DecideOptions
            {
                StimulusLabel = "Report",
                Priority = DecisionPriority.Background,
                Observations = { "The gate is shut." },
            };

            var pending = agent.DecideAsync("Smoke over the tower.", options);
            options.StimulusLabel = "Changed";
            options.Observations.Add("Added after the call.");
            fake.Release();
            var result = await Fixtures.NotCancelled(pending);

            var input = spy.Inputs.Single();
            Assert.That(input.Context.Stimulus, Is.EqualTo("Smoke over the tower."));
            Assert.That(input.Context.Targets.Contains("tower"), Is.True);
            Assert.That(input.Request, Is.SameAs(fake.LastRequest));
            Assert.That(input.Reply.Text, Is.EqualTo(GoToTheTower));
            Assert.That(input.Decision, Is.SameAs(result.Decision));
            Assert.That(input.Telemetry, Is.SameAs(result.Telemetry));
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "Spy wrote here" }), "A guard may record its own numbers.");

            Assert.That(input.Options, Is.Not.SameAs(options), "The call's own copy.");
            Assert.That(input.Options.StimulusLabel, Is.EqualTo("Report"));
            Assert.That(input.Options.Priority, Is.EqualTo(DecisionPriority.Background));
            Assert.That(input.Options.Observations, Is.EqualTo(new[] { "The gate is shut." }), "Copied when the call was made.");
        }

        [Test]
        public async Task WithNoOptionsAGuardIsHandedTheDefaults()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var spy = new FakeGuard("Spy");
            agent.Guards.Add(spy);

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(spy.Inputs.Single().Options, Is.Not.Null);
            Assert.That(spy.Inputs.Single().Options.StimulusLabel, Is.EqualTo("Player"));
        }

        // ---- A pipeline error never reaches a guard -----------------------------------------

        [Test]
        public async Task AReplyThatCannotBeReadNeverReachesAGuard()
        {
            var fake = new FakeProvider("Sure, heading to the tower now.");
            var agent = Fixtures.VillageGuard(fake);
            var spy = new FakeGuard("Spy");
            agent.Guards.Add(spy);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision, Is.Null);
            Assert.That(result.ModelDecision, Is.Null);
            Assert.That(result.Verdict, Is.Null);
            Assert.That(spy.Inputs, Is.Empty);
            Assert.That(result.Telemetry.GuardsFired, Is.Empty);
        }

        [Test]
        public async Task AProviderThatFailsNeverReachesAGuard()
        {
            var fake = new FakeProvider(GoToTheTower);
            fake.FailWith(ProviderErrorKind.Unreachable);
            var agent = Fixtures.VillageGuard(fake);
            var spy = new FakeGuard("Spy");
            agent.Guards.Add(spy);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(spy.Inputs, Is.Empty);
        }

        // ---- Execute's re-check -------------------------------------------------------------

        [Test]
        public async Task ExecuteRefusesAnActionThatBecameUnavailableAndRecordsIt()
        {
            var fake = new FakeProvider(Reply("follow_player", "no_target", "Lead the way."));
            var agent = Fixtures.VillageGuard(fake);
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Keep me company."));
            Assert.That(result.Telemetry.GuardsFired, Is.Empty, "Legal when it was decided.");

            agent.State["isFollowing"] = true;
            LogAssert.Expect(LogType.Warning,
                "Agent 'Village Guard' did not execute 'follow_player' on 'no_target': the action is not available right now.");

            Assert.That(agent.Execute(result), Is.False);
            Assert.That(Executions(agent), Is.Empty);
            AssertRefusedAtExecute(result, "the action is not available right now.");
        }

        [Test]
        public async Task ExecuteRefusesATargetThatLeftAndRecordsIt()
        {
            var fake = new FakeProvider(Reply("move_to", "bridge", ""));
            var agent = Fixtures.VillageGuard(fake);
            var scouted = new ExplicitTargetSource().Add("bridge", new object());
            agent.TargetSources.Add(scouted);
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Cross over the stream."));
            Assert.That(result.Telemetry.GuardsFired, Is.Empty, "Legal when it was decided.");

            scouted.Remove("bridge");
            LogAssert.Expect(LogType.Warning, new Regex("did not execute 'move_to' on 'bridge': the target is not registered"));

            Assert.That(agent.Execute(result), Is.False);
            Assert.That(Executions(agent), Is.Empty);
            AssertRefusedAtExecute(result, "the target is not registered.");
        }

        [Test]
        public async Task ExecuteRunsOnlyTheLegalityGuardAgain()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var spy = new FakeGuard("Spy");
            agent.Guards.Add(spy);
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(agent.Execute(result), Is.True);
            Assert.That(spy.Inputs, Has.Count.EqualTo(1), "Only DecideAsync ran it.");
        }

        // ---- The verdict and input types ----------------------------------------------------

        [Test]
        public void AVerdictThatStopsADecisionSaysWhy()
        {
            Assert.That(GuardVerdict.Pass.Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(GuardVerdict.Pass.Reason, Is.Null);
            Assert.That(default(GuardVerdict).Kind, Is.EqualTo(GuardVerdictKind.Pass));

            Assert.That(GuardVerdict.RewriteToNone("why").Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(GuardVerdict.Reject("why").Reason, Is.EqualTo("why"));

            foreach (string missing in new[] { null, "", "  " })
            {
                Assert.Throws<ArgumentException>(() => GuardVerdict.RewriteToNone(missing));
                Assert.Throws<ArgumentException>(() => GuardVerdict.Reject(missing));
            }
        }

        [Test]
        public void AGuardInputNeedsAContextAndADecisionAndDefaultsTheRest()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider());
            var ctx = Context(agent);
            var decision = new AgentDecision("move_to", "tower", "");

            Assert.Throws<ArgumentNullException>(() => new GuardInput(null, null, null, decision, null, null));
            Assert.Throws<ArgumentNullException>(() => new GuardInput(ctx, null, null, null, null, null));

            var input = new GuardInput(ctx, null, null, decision, null, null);
            Assert.That(input.Options, Is.Not.Null);
            Assert.That(input.Telemetry, Is.Not.Null);
            Assert.That(input.Request, Is.Null);
            Assert.That(input.Reply, Is.Null);
        }

        // ---- Helpers ------------------------------------------------------------------------

        private static void AssertRefusedAtExecute(DecisionResult result, string reason)
        {
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "SchemaLegalityGuard@Execute" }));
            Assert.That(result.Telemetry.Verdict, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(result.Telemetry.GuardReason, Is.EqualTo(reason));
            Assert.That(result.Verdict, Is.Null, "The result's verdict is DecideAsync's, which passed it.");
            Assert.That(result.Decision, Is.SameAs(result.ModelDecision), "Execute refuses; it never rewrites.");
        }

        private static string Reply(string action, string target, string statement)
        {
            return $"{{\"action\":\"{action}\",\"target\":\"{target}\",\"statement\":\"{statement}\"}}";
        }

        private static AgentContext Context(Agent agent)
        {
            return new AgentContext(agent.Identity, "", null, null, agent.Targets, agent.Actions);
        }

        // Every execution of every FakeHandler bound to the agent.
        private static List<AgentDecision> Executions(Agent agent)
        {
            var all = new List<AgentDecision>();
            foreach (var definition in agent.Actions.Definitions)
            {
                all.AddRange(Fixtures.Handler(agent, definition.Id).Executions.Select(e => e.Decision));
            }

            return all;
        }
    }
}
