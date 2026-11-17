using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // A target on an action that takes none (#244 item 2, GreyBoxStrategy's N5): with
    // ActionDefinition.TakesNoTarget, a flat schema's stray target is dropped before the guards and
    // recorded; a paired schema never offers one, and SchemaLegalityGuard rejects one that comes
    // anyway, as it does a decision Execute is handed. Every await of DecideAsync goes through
    // Fixtures.NotCancelled.
    public sealed class StrayTargetTests
    {
        private static string Reply(string action, string target) =>
            $"{{\"action\":\"{action}\",\"target\":\"{target}\",\"statement\":\"For the realm.\"}}";

        // ---- A flat schema: the target is dropped -------------------------------------------

        [Test]
        public async Task AStrayTargetIsDroppedBeforeTheGuardsAndTheHandlerAndRecorded()
        {
            var fake = new FakeProvider(Reply("recruit", "merrowdeep"));
            var agent = Country(fake, out var recruit, out _);
            var guard = new FakeGuard("Watcher");
            agent.Guards.Add(guard);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your move."));

            Assert.That(result.Decision.ActionId, Is.EqualTo("recruit"));
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.Decision.Statement, Is.EqualTo("For the realm."));
            Assert.That(result.ModelDecision.TargetId, Is.EqualTo("merrowdeep"), "What the model said stays readable.");
            Assert.That(result.Telemetry.TargetIgnored, Is.EqualTo("merrowdeep"));
            Assert.That(result.Verdict, Is.Null, "Dropped, not refused.");
            Assert.That(result.Telemetry.GuardsFired, Is.Empty);
            Assert.That(guard.Inputs.Single().Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget), "No guard sees the stray target.");

            Assert.That(agent.Execute(result), Is.True);
            Assert.That(recruit.Executions.Single().Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget), "Nor does the handler.");
        }

        [Test]
        public async Task NothingIsRecordedWhenTheModelNamesNoTarget()
        {
            var fake = new FakeProvider(Reply("recruit", TargetRegistry.NoTarget));
            var agent = Country(fake, out _, out _);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your move."));

            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.Telemetry.TargetIgnored, Is.Null);
            Assert.That(DecisionLogLine.From(Entry(agent, result)).TargetIgnored, Is.Null, "Left out, so an existing log line is unchanged.");
            Assert.That(DecisionLogLine.From(Entry(agent, result)).ModelDecision, Is.Null);
        }

        [Test]
        public async Task AnActionWithAnOptionalTargetKeepsIt()
        {
            var fake = new FakeProvider(Reply("pray", "merrowdeep"));
            var agent = Country(fake, out _, out var pray);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your move."));

            Assert.That(result.Decision.TargetId, Is.EqualTo("merrowdeep"), "Without TakesNoTarget nothing changes.");
            Assert.That(result.Telemetry.TargetIgnored, Is.Null);
            Assert.That(agent.Execute(result), Is.True);
            Assert.That(pray.Executions.Single().Decision.TargetId, Is.EqualTo("merrowdeep"));
        }

        [Test]
        public async Task TheDecisionLogLineCarriesTheDroppedTargetAndTheModelsAnswer()
        {
            var fake = new FakeProvider(Reply("recruit", "merrowdeep"));
            var agent = Country(fake, out _, out _);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your move."));
            var line = DecisionLogLine.From(Entry(agent, result));

            Assert.That(line.TargetIgnored, Is.EqualTo("merrowdeep"));
            Assert.That(line.Decision.Target, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(line.ModelDecision.Target, Is.EqualTo("merrowdeep"));

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(line);
            Assert.That(json, Does.Contain("\"targetIgnored\":\"merrowdeep\""));
        }

        // ---- A paired schema: never offered, rejected if it comes -----------------------------

        [Test]
        public async Task APairedSchemaOffersAnActionThatTakesNoTargetWithNoTargetAlone()
        {
            var fake = new FakeProvider(Reply("recruit", TargetRegistry.NoTarget));
            var agent = Country(fake, out _, out _);

            await Fixtures.NotCancelled(agent.DecideAsync("Your move.", new DecideOptions { PairTargetsWithActions = true }));

            var pairs = fake.LastRequest.Schema.Pairs;
            Assert.That(pairs.Single(p => p.Actions.Contains("recruit")).Targets, Is.EqualTo(new[] { TargetRegistry.NoTarget }));
            Assert.That(pairs.Single(p => p.Actions.Contains("pray")).Targets,
                Is.EqualTo(new[] { TargetRegistry.NoTarget, "merrowdeep", "fenmark" }), "An optional target is still any target.");
        }

        [Test]
        public async Task UnderAPairedSchemaAStrayTargetIsRejectedNotDropped()
        {
            var fake = new FakeProvider(Reply("recruit", "merrowdeep"));
            var agent = Country(fake, out var recruit, out _);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("takes no target"));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your move.", new DecideOptions { PairTargetsWithActions = true }));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.Reject), "The grammar forbade it: the provider broke the contract.");
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("SchemaLegalityGuard"));
            Assert.That(result.Verdict.Value.Reason, Is.EqualTo("the action takes no target, and the decision has one."));
            Assert.That(result.Telemetry.TargetIgnored, Is.Null);
            Assert.That(recruit.Executions, Is.Empty);
        }

        [Test]
        public void TheLegalityGuardRejectsADecisionWithATargetForAnActionThatTakesNone()
        {
            var agent = Country(new FakeProvider(), out _, out _);
            var ctx = new AgentContext(agent.Identity, "", null, null, agent.Targets, agent.Actions);

            var stray = Legality(ctx, "recruit", "merrowdeep");
            var clean = Legality(ctx, "recruit", TargetRegistry.NoTarget);

            Assert.That(stray.Kind, Is.EqualTo(GuardVerdictKind.Reject), "As Execute's re-check refuses a decision it is handed.");
            Assert.That(stray.Reason, Is.EqualTo("the action takes no target, and the decision has one."));
            Assert.That(clean.Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(Legality(ctx, "pray", "merrowdeep").Kind, Is.EqualTo(GuardVerdictKind.Pass));
        }

        // ---- Declaring it --------------------------------------------------------------------

        [Test]
        public void TakesNoTargetCannotBeCombinedWithRequiresTargetOrCategories()
        {
            var registry = new ActionRegistry();

            Assert.Throws<ArgumentException>(() => registry.Register(
                new ActionDefinition { Id = "a", Description = "A.", TakesNoTarget = true, RequiresTarget = true }, new FakeHandler()));
            Assert.Throws<ArgumentException>(() => registry.Register(
                new ActionDefinition { Id = "b", Description = "B.", TakesNoTarget = true, TargetCategories = new[] { "province" } }, new FakeHandler()));
            Assert.That(registry.Definitions, Is.Empty);

            registry.Register(new ActionDefinition { Id = "c", Description = "C.", TakesNoTarget = true, TargetCategories = new[] { " " } }, new FakeHandler());
            Assert.That(registry.Definitions.Single().Id, Is.EqualTo("c"), "A blank category names none.");
        }

        [Test]
        public void TheAttributeCarriesTakesNoTarget()
        {
            var registry = new ActionRegistry();

            registry.RegisterMethods(new Treasury());

            Assert.That(registry.Definitions.Single(d => d.Id == "raise_taxes").TakesNoTarget, Is.True);
            Assert.That(registry.Definitions.Single(d => d.Id == "pray").TakesNoTarget, Is.False);
        }

        private sealed class Treasury
        {
            [AgentAction("raise_taxes", "Take gold from our people.", TakesNoTarget = true)]
            public void RaiseTaxes()
            {
            }

            [AgentAction("pray", "Ask the gods for help.")]
            public void Pray()
            {
            }
        }

        // ---- Helpers -------------------------------------------------------------------------

        // A country whose recruit takes no target, and whose pray takes one optionally.
        private static Agent Country(FakeProvider fake, out FakeHandler recruit, out FakeHandler pray)
        {
            var actions = new[]
            {
                new ActionDefinition { Id = "recruit", Description = "Raise soldiers with gold.", TakesNoTarget = true },
                new ActionDefinition { Id = "pray", Description = "Ask the gods for help." },
            };

            var agent = new Agent(new AgentIdentity { Name = "Corwick", Role = "a country" }, actions, fake);
            recruit = new FakeHandler();
            pray = new FakeHandler();
            agent.Bind("recruit", recruit);
            agent.Bind("pray", pray);
            agent.Targets.Register("merrowdeep", new object(), "province");
            agent.Targets.Register("fenmark", new object(), "province");
            return agent;
        }

        private static GuardVerdict Legality(AgentContext ctx, string action, string target)
        {
            return new SchemaLegalityGuard().Inspect(new GuardInput(ctx, null, null, new AgentDecision(action, target, ""), null, null));
        }

        private static DecisionFeedEntry Entry(Agent agent, DecisionResult result)
        {
            return new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1);
        }
    }
}
