using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // Per-target availability (#190 LV #19, #216 item 5, DR-020): ITargetAvailability, its two front
    // doors, the paired schema dropping rejected pairs, an action with no legal target masked in both
    // modes, TargetAvailabilityGuard, Execute's re-check, and the reasons in telemetry and the
    // preview. Every await of DecideAsync goes through Fixtures.NotCancelled.
    public sealed class TargetAvailabilityTests
    {
        private const string Claimed = "claimed by the miller";

        private static string Reply(string action, string target) =>
            $"{{\"action\":\"{action}\",\"target\":\"{target}\",\"statement\":\"\"}}";

        // ---- The schema ----------------------------------------------------------------------

        [Test]
        public void ThePairedSchemaLeavesOutEachPairTheCheckRejects()
        {
            var claimed = new HashSet<string> { "tree_b" };
            var agent = Woodcutter(new FakeProvider(), claimed);

            var schema = DecisionSchema.Build(Context(agent), pairTargets: true);

            Assert.That(Branches(schema), Is.EqualTo(new[]
            {
                "move_to -> tree_a, tree_b, well",
                "chop -> tree_a",
                "none -> no_target",
            }));
        }

        [Test]
        public void AnActionWithNoLegalTargetIsLeftOutFlatOrPaired()
        {
            var claimed = new HashSet<string> { "tree_a", "tree_b" };
            var agent = Woodcutter(new FakeProvider(), claimed);

            var flat = DecisionSchema.Build(Context(agent));
            var paired = DecisionSchema.Build(Context(agent), pairTargets: true);

            Assert.That(ActionEnum(flat), Is.EqualTo(new[] { "move_to", "none" }));
            Assert.That(ActionEnum(paired), Is.EqualTo(new[] { "move_to", "none" }));
            Assert.That(Branches(paired), Does.Not.Contain("chop -> "), "No branch with an empty target enum either.");
        }

        [Test]
        public void TheFlatTargetEnumStaysEveryRegisteredTarget()
        {
            var agent = Woodcutter(new FakeProvider(), new HashSet<string> { "tree_b" });

            var flat = DecisionSchema.Build(Context(agent));

            Assert.That(flat.Pairs, Is.Null);
            Assert.That(ActionEnum(flat), Is.EqualTo(new[] { "move_to", "chop", "none" }));
            Assert.That(TargetEnum(flat), Is.EqualTo(new[] { "no_target", "tree_a", "tree_b", "well" }));
        }

        [Test]
        public void ACheckThatAllowsEveryTargetChangesNothingTheModelIsSent()
        {
            // The sanity check over rendered schemas the lead asked for: masking per target only
            // masks what it rejects. A check that rejects nothing renders the same request, flat and
            // paired, as no check at all.
            foreach (bool pair in new[] { false, true })
            {
                var without = new FakeProvider(Reply("none", "no_target"));
                var with = new FakeProvider(Reply("none", "no_target"));
                var plain = Woodcutter(without, claimed: null);
                var checking = Woodcutter(with, new HashSet<string>());

                var options = new DecideOptions { PairTargetsWithActions = pair, ListActionsAndTargets = true };
                var a = plain.Preview("Cut something.", options).Request;
                var b = checking.Preview("Cut something.", options).Request;

                Assert.That(Json(b.Schema), Is.EqualTo(Json(a.Schema)), $"pair={pair}");
                Assert.That(b.SystemPrompt, Is.EqualTo(a.SystemPrompt), $"pair={pair}");
                Assert.That(b.FewShotBlock, Is.EqualTo(a.FewShotBlock), $"pair={pair}");
            }
        }

        [Test]
        public void AHandlerWithoutACheckIsNeverMaskedPerTarget()
        {
            // An action whose categories match no registered target keeps its flat-schema place
            // when its handler has no per-target check, exactly as before (#210 masks it only paired).
            var agent = new Agent(new AgentIdentity { Name = "Bram", Role = "a woodcutter" }, Array.Empty<ActionDefinition>(), new FakeProvider());
            agent.Actions.Register(
                new ActionDefinition { Id = "chop", Description = "Chop a tree.", RequiresTarget = true, TargetCategories = new[] { "tree" } },
                new DelegateActionHandler(_ => true, (_, __) => { }));
            agent.Targets.Register("well", new object(), "place");

            Assert.That(ActionEnum(DecisionSchema.Build(Context(agent))), Is.EqualTo(new[] { "chop", "none" }));
        }

        [Test]
        public void TheCheckIsAskedOnlyAboutTargetsInTheActionsCategories()
        {
            var asked = new List<string>();
            var agent = Woodcutter(new FakeProvider(), new HashSet<string>(), asked);

            DecisionSchema.Build(Context(agent), pairTargets: true);

            Assert.That(asked, Is.EqualTo(new[] { "tree_a", "tree_b" }), "Never the well, which is not a tree, and never no_target.");
        }

        // ---- The guard and Execute -----------------------------------------------------------

        [Test]
        public async Task TheGuardTurnsARejectedPairIntoNoneInAFlatSchema()
        {
            var agent = Woodcutter(new FakeProvider(Reply("chop", "tree_b")), new HashSet<string> { "tree_b" });

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Cut down the second tree."));

            Assert.That(result.ModelDecision.ActionId, Is.EqualTo("chop"));
            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.Verdict.HasValue, Is.True);
            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("TargetAvailabilityGuard"));
            Assert.That(result.Verdict.Value.Reason, Is.EqualTo($"'chop' cannot be done to 'tree_b' right now: {Claimed}"));
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "TargetAvailabilityGuard" }));
        }

        [Test]
        public async Task TheGuardPassesAnAllowedPair()
        {
            var agent = Woodcutter(new FakeProvider(Reply("chop", "tree_a")), new HashSet<string> { "tree_b" });

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Cut down the first tree."));

            Assert.That(result.Verdict.HasValue, Is.False);
            Assert.That(result.Decision.ActionId, Is.EqualTo("chop"));
            Assert.That(result.Decision.TargetId, Is.EqualTo("tree_a"));
        }

        [Test]
        public async Task UnderPairingARejectedPairTheGrammarLetThroughIsRejected()
        {
            var agent = Woodcutter(new FakeProvider(Reply("chop", "tree_b")), new HashSet<string> { "tree_b" });

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("paired them apart"));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Cut down the second tree.", new DecideOptions { PairTargetsWithActions = true }));

            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("SchemaLegalityGuard"));
        }

        [Test]
        public async Task ExecuteRefusesAPairThatWentStaleWithTheReason()
        {
            var claimed = new HashSet<string>();
            var handler = Chopper(claimed, out var chopped);
            var agent = Woodcutter(new FakeProvider(Reply("chop", "tree_a")), handler);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Cut down the first tree."));
            Assert.That(result.Decision.TargetId, Is.EqualTo("tree_a"));

            // Someone claims the tree while the model was thinking.
            claimed.Add("tree_a");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(Claimed));
            bool ran = agent.Execute(result, out var outcome);

            Assert.That(ran, Is.False);
            Assert.That(chopped, Is.Empty, "No handler ran.");
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(outcome.Reason, Is.EqualTo($"'chop' cannot be done to 'tree_a' right now: {Claimed}"));
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "TargetAvailabilityGuard@Execute" }));
            Assert.That(result.Telemetry.ExecutionReason, Is.EqualTo(outcome.Reason));
        }

        [Test]
        public async Task ExecuteNamesTheReasonWhenNoTargetIsLeftAtAll()
        {
            var claimed = new HashSet<string>();
            var handler = Chopper(claimed, out var chopped);
            var agent = Woodcutter(new FakeProvider(Reply("chop", "tree_a")), handler);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Cut down the first tree."));
            claimed.Add("tree_a");
            claimed.Add("tree_b");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(Claimed));
            agent.Execute(result, out var outcome);

            Assert.That(chopped, Is.Empty);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(outcome.Reason, Does.Contain("'tree_a': " + Claimed));
        }

        [Test]
        public async Task ExecuteCarriesOutAPairStillAllowed()
        {
            var claimed = new HashSet<string>();
            var handler = Chopper(claimed, out var chopped);
            var agent = Woodcutter(new FakeProvider(Reply("chop", "tree_a")), handler);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Cut down the first tree."));
            claimed.Add("tree_b");

            Assert.That(agent.Execute(result, out var outcome), Is.True);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Done));
            Assert.That(chopped, Is.EqualTo(new[] { "tree_a" }));
        }

        // ---- Telemetry and the preview -------------------------------------------------------

        [Test]
        public async Task TelemetryRecordsEachRejectedPairWithItsReason()
        {
            var agent = Woodcutter(new FakeProvider(Reply("none", "no_target")), new HashSet<string> { "tree_b" });

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Rest a while."));

            Assert.That(result.Telemetry.TargetsUnavailable.Select(p => p.ToString()), Is.EqualTo(new[] { $"chop -> tree_b: {Claimed}" }));
        }

        [Test]
        public async Task AnAgentWithNoCheckRecordsNoRejectedPairs()
        {
            var agent = Woodcutter(new FakeProvider(Reply("none", "no_target")), claimed: null);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Rest a while."));

            Assert.That(result.Telemetry.TargetsUnavailable, Is.Empty);
        }

        [Test]
        public void ThePreviewShowsTheRejectedPairsAndWhyAnActionIsLeftOut()
        {
            var agent = Woodcutter(new FakeProvider(), new HashSet<string> { "tree_a", "tree_b" });

            var preview = agent.Preview("Cut something.");

            Assert.That(preview.Error, Is.Null);
            Assert.That(preview.Available.Select(a => a.Id), Is.EqualTo(new[] { "move_to" }));
            Assert.That(preview.TargetsUnavailable.Select(p => p.ToString()), Is.EqualTo(new[]
            {
                $"chop -> tree_a: {Claimed}",
                $"chop -> tree_b: {Claimed}",
            }));
            var reason = preview.LeftOut.Single(left => left.Action.Id == "chop").Reason;
            Assert.That(reason, Is.EqualTo(
                $"It needs a target, and its per-target check allows none of those it takes: 'tree_a' ({Claimed}), 'tree_b' ({Claimed})."));
        }

        [Test]
        public void ThePreviewAsksEachCheckOncePerTarget()
        {
            var asked = new List<string>();
            var agent = Woodcutter(new FakeProvider(), new HashSet<string> { "tree_b" }, asked);

            agent.Preview("Cut something.", new DecideOptions { PairTargetsWithActions = true });

            Assert.That(asked, Is.EqualTo(new[] { "tree_a", "tree_b" }));
        }

        [Test]
        public async Task TheDecisionLogLineCarriesTheRejectedPairsOnlyWhenThereAreAny()
        {
            var plain = Woodcutter(new FakeProvider(Reply("none", "no_target")), claimed: null);
            var checking = Woodcutter(new FakeProvider(Reply("none", "no_target")), new HashSet<string> { "tree_b" });

            var none = await Fixtures.NotCancelled(plain.DecideAsync("Rest a while."));
            var some = await Fixtures.NotCancelled(checking.DecideAsync("Rest a while."));

            Assert.That(DecisionLogLine.From(Entry(plain, none)).TargetsUnavailable, Is.Null, "Left out, so an existing log line is unchanged.");
            Assert.That(DecisionLogLine.From(Entry(checking, some)).TargetsUnavailable.Select(p => p.ToString()),
                Is.EqualTo(new[] { $"chop -> tree_b: {Claimed}" }));
        }

        // ---- The front doors -----------------------------------------------------------------

        [Test]
        public void AnAvailableForMethodIsTheCheckOfItsAction()
        {
            var owner = new Woodpile();
            var agent = new Agent(new AgentIdentity { Name = "Bram", Role = "a woodcutter" }, Array.Empty<ActionDefinition>(), new FakeProvider());
            agent.Actions.RegisterMethods(owner);
            agent.Targets.Register("log_a", new object());
            agent.Targets.Register("log_b", new object());

            var preview = agent.Preview("Split some wood.", new DecideOptions { PairTargetsWithActions = true });
            var schema = preview.Request.Schema;

            Assert.That(Branches(schema), Is.EqualTo(new[]
            {
                "split -> log_a",
                "stack -> log_b",
                "none -> no_target",
            }));
            Assert.That(preview.TargetsUnavailable.Select(p => p.ToString()), Is.EqualTo(new[]
            {
                "split -> log_b: too wet",
                "stack -> log_a",
            }));
            Assert.That(owner.Contexts, Is.Not.Empty, "The check that takes a context was given one.");
        }

        [Test]
        public void AnAvailableForMethodOfTheWrongShapeIsRefused()
        {
            var registry = new ActionRegistry();

            var returns = Assert.Throws<ArgumentException>(() => registry.RegisterMethods(new ReturnsInt()));
            var takes = Assert.Throws<ArgumentException>(() => registry.RegisterMethods(new TakesNothing()));
            var typo = Assert.Throws<ArgumentException>(() => registry.RegisterMethods(new NamesNoAction()));
            var twice = Assert.Throws<ArgumentException>(() => registry.RegisterMethods(new TwoChecks()));
            var both = Assert.Throws<ArgumentException>(() => registry.RegisterMethods(new BothChecks()));

            Assert.That(returns.Message, Does.Contain("must return bool or Availability"));
            Assert.That(takes.Message, Does.Contain("takes the target's id as a string"));
            Assert.That(typo.Message, Does.Contain("has no [AgentAction] with that id"));
            Assert.That(twice.Message, Does.Contain("has two [AvailableFor] checks"));
            Assert.That(both.Message, Does.Contain("has both [Available] and [AvailableFor]"));
        }

        [Test]
        public void AnAvailabilityConvertsFromABoolAndKeepsItsReason()
        {
            Availability yes = true;
            Availability no = false;

            Assert.That(yes.IsAvailable, Is.True);
            Assert.That(yes, Is.EqualTo(Availability.Yes));
            Assert.That(default(Availability).IsAvailable, Is.True, "A forgotten answer never masks anything.");
            Assert.That(no.IsAvailable, Is.False);
            Assert.That(no.Reason, Is.Null);
            Assert.That(Availability.No("  ").Reason, Is.Null);
            Assert.That(Availability.No(Claimed).Reason, Is.EqualTo(Claimed));
        }

        // ---- Fixtures ------------------------------------------------------------------------

        // A woodcutter with move_to (anything) and chop (trees only), two trees and a well. Its
        // chop handler has a per-target check rejecting the claimed trees; with claimed null, no
        // check at all.
        private static Agent Woodcutter(FakeProvider provider, HashSet<string> claimed, List<string> asked = null)
        {
            var handler = new DelegateActionHandler(_ => true, (_, __) => { });
            if (claimed != null)
            {
                handler.IsAvailableFor = (ctx, id) =>
                {
                    asked?.Add(id);
                    return claimed.Contains(id) ? Availability.No(Claimed) : Availability.Yes;
                };
            }

            return Woodcutter(provider, handler);
        }

        private static Agent Woodcutter(FakeProvider provider, DelegateActionHandler chop)
        {
            var actions = new[]
            {
                new ActionDefinition { Id = "move_to", Description = "Walk somewhere.", RequiresTarget = true },
                new ActionDefinition { Id = "chop", Description = "Chop a tree.", RequiresTarget = true, TargetCategories = new[] { "tree" } },
            };

            var agent = new Agent(new AgentIdentity { Name = "Bram", Role = "a woodcutter" }, actions, provider);
            agent.Bind("move_to", new FakeHandler());
            agent.Bind("chop", chop);
            agent.Targets.Register("tree_a", new object(), "tree");
            agent.Targets.Register("tree_b", new object(), "tree");
            agent.Targets.Register("well", new object(), "place");
            return agent;
        }

        private static DelegateActionHandler Chopper(HashSet<string> claimed, out List<string> chopped)
        {
            var log = new List<string>();
            chopped = log;
            return new DelegateActionHandler(_ => true, (_, decision) => log.Add(decision.TargetId))
            {
                IsAvailableFor = (_, id) => claimed.Contains(id) ? Availability.No(Claimed) : Availability.Yes,
            };
        }

        private static DecisionFeedEntry Entry(Agent agent, DecisionResult result)
        {
            return new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1);
        }

        private static AgentContext Context(Agent agent)
        {
            return new AgentContext(agent.Identity, "", null, null, agent.Targets, agent.Actions);
        }

        private static IEnumerable<string> Branches(DecisionSchema schema)
        {
            return schema.Pairs.Select(p => string.Join(", ", p.Actions) + " -> " + string.Join(", ", p.Targets));
        }

        private static IReadOnlyList<string> ActionEnum(DecisionSchema schema)
        {
            return schema.Fields.First(f => f.Name == DecisionSchema.ActionFieldName).AllowedValues;
        }

        private static IReadOnlyList<string> TargetEnum(DecisionSchema schema)
        {
            return schema.Fields.First(f => f.Name == DecisionSchema.TargetFieldName).AllowedValues;
        }

        private static string Json(DecisionSchema schema)
        {
            return new JsonSchemaSerializer().Serialize(schema);
        }

        // [AvailableFor] in both shapes: split takes only dry logs, with a reason; stack only the
        // logs split ones are not, as a bool, given the context.
        private sealed class Woodpile
        {
            public List<AgentContext> Contexts { get; } = new List<AgentContext>();

            [AgentAction("split", "Split a log.", RequiresTarget = true)]
            private void Split(string log) { }

            [AgentAction("stack", "Stack a log.", RequiresTarget = true)]
            private void Stack(string log) { }

            [AvailableFor("split")]
            private Availability CanSplit(string id) => id == "log_b" ? Availability.No("too wet") : Availability.Yes;

            [AvailableFor("stack")]
            private bool CanStack(AgentContext ctx, string id)
            {
                Contexts.Add(ctx);
                return id != "log_a";
            }
        }

        private sealed class ReturnsInt
        {
            [AgentAction("split", "Split a log.", RequiresTarget = true)]
            private void Split() { }

            [AvailableFor("split")]
            private int CanSplit(string id) => 1;
        }

        private sealed class TakesNothing
        {
            [AgentAction("split", "Split a log.", RequiresTarget = true)]
            private void Split() { }

            [AvailableFor("split")]
            private bool CanSplit() => true;
        }

        private sealed class NamesNoAction
        {
            [AgentAction("split", "Split a log.", RequiresTarget = true)]
            private void Split() { }

            [AvailableFor("splt")]
            private bool CanSplit(string id) => true;
        }

        private sealed class TwoChecks
        {
            [AgentAction("split", "Split a log.", RequiresTarget = true)]
            private void Split() { }

            [AvailableFor("split")]
            private bool One(string id) => true;

            [AvailableFor("split")]
            private bool Two(string id) => true;
        }

        private sealed class BothChecks
        {
            [AgentAction("split", "Split a log.", RequiresTarget = true)]
            private void Split() { }

            [Available("split")]
            [AvailableFor("split")]
            private bool Check(string id) => true;
        }
    }
}
