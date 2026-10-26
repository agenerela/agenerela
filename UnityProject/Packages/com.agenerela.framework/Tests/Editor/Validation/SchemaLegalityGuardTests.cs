using System;
using System.Collections.Generic;
using Agenerela.Providers;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // SchemaLegalityGuard on its own, over hand-built inputs: each rule, and the two meanings of
    // "available", what the request offered and what is legal now. Through Agent, in DecideAsync and
    // Execute, it is in GuardPipelineTests and AgentTests.
    public sealed class SchemaLegalityGuardTests
    {
        private readonly SchemaLegalityGuard guard = new SchemaLegalityGuard();

        [Test]
        public void ItsNameIsWhatTelemetryRecords()
        {
            Assert.That(guard.Name, Is.EqualTo("SchemaLegalityGuard"));
        }

        [TestCase("none", "no_target")]
        [TestCase("none", "tower")]
        [TestCase("none", "godzilla")]
        public void NoneAlwaysPasses(string action, string target)
        {
            var (ctx, _) = Guard(isFollowing: true);

            Assert.That(Inspect(ctx, action, target, request: null).Kind, Is.EqualTo(GuardVerdictKind.Pass));
        }

        [TestCase("move_to", "tower")]
        [TestCase("follow_player", "no_target")]
        [TestCase("follow_player", "tower")]
        public void ALegalDecisionPasses(string action, string target)
        {
            var (ctx, _) = Guard(isFollowing: false);

            Assert.That(Inspect(ctx, action, target, request: null).Kind, Is.EqualTo(GuardVerdictKind.Pass), "Re-checked against now.");
            Assert.That(Inspect(ctx, action, target, Offering(ctx)).Kind, Is.EqualTo(GuardVerdictKind.Pass), "Checked against the request.");
        }

        [TestCase("dance", "no_target", "the action is not registered.")]
        [TestCase("MOVE_TO", "tower", "the action is not registered.")]
        [TestCase("move_to", "godzilla", "the target is not registered.")]
        [TestCase("move_to", "Tower", "the target is not registered.")]
        [TestCase("follow_player", "godzilla", "the target is not registered.")]
        [TestCase("move_to", "no_target", "the action needs a target, and the decision has none.")]
        public void AnIllegalDecisionIsRejectedWithItsReason(string action, string target, string reason)
        {
            var (ctx, _) = Guard(isFollowing: false);

            foreach (var request in new[] { null, Offering(ctx) })
            {
                var verdict = Inspect(ctx, action, target, request);

                Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.Reject));
                Assert.That(verdict.Reason, Is.EqualTo(reason));
                Assert.That(verdict.Guard, Is.Null, "The pipeline names the guard, not the guard itself.");
            }
        }

        [Test]
        public void WithARequestAvailableMeansOfferedAndNoAvailabilityCheckRuns()
        {
            var (ctx, stop) = Guard(isFollowing: true);
            var offered = Offering(ctx);
            stop.AvailabilityChecks.Clear();

            Assert.That(Inspect(ctx, "stop_following", "no_target", offered).Kind, Is.EqualTo(GuardVerdictKind.Pass));

            var notOffered = Inspect(ctx, "follow_player", "no_target", offered);
            Assert.That(notOffered.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(notOffered.Reason, Is.EqualTo("the action was not offered: it was not available for this decision."));

            Assert.That(stop.AvailabilityChecks, Is.Empty,
                "The request's enum came from one availability answer; asking again could disagree with it.");
        }

        [Test]
        public void WithARequestAnActionAvailableNowButNotOfferedIsRejected()
        {
            // The answer the model was held to is the one it was shown, even if the world has
            // since changed so that the action would be legal.
            var (ctx, _) = Guard(isFollowing: true);
            var offered = Offering(ctx);
            var (now, _) = Guard(isFollowing: false);

            Assert.That(Inspect(now, "follow_player", "no_target", offered).Kind, Is.EqualTo(GuardVerdictKind.Reject));
        }

        [Test]
        public void WithoutARequestAvailabilityIsRecomputedEveryTime()
        {
            var (ctx, stop) = Guard(isFollowing: false);
            stop.AvailabilityChecks.Clear();

            var refused = Inspect(ctx, "stop_following", "no_target", request: null);
            Assert.That(refused.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(refused.Reason, Is.EqualTo("the action is not available right now."));

            Inspect(ctx, "stop_following", "no_target", request: null);
            Assert.That(stop.AvailabilityChecks, Has.Count.EqualTo(2), "Never cached.");
        }

        [Test]
        public void WithoutARequestATargetNeedingActionIsUnavailableWithNoTargetsAtAll()
        {
            // ActionAvailability's own rule: with nothing registered, move_to is never offered.
            // The target rule catches it first, since its target can only be no_target.
            var (ctx, _) = Guard(isFollowing: false);
            var empty = new AgentContext(ctx.Identity, "", null, ctx.State, new TargetRegistry(), ctx.Actions);

            var verdict = Inspect(empty, "move_to", "no_target", request: null);

            Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(verdict.Reason, Is.EqualTo("the action needs a target, and the decision has none."));
        }

        [Test]
        public void ARequestWithNoSchemaOfferedNothing()
        {
            var (ctx, _) = Guard(isFollowing: false);

            var verdict = Inspect(ctx, "follow_player", "no_target", new DecisionRequest());

            Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.Reject));
        }

        [Test]
        public void ANullInputThrows()
        {
            Assert.Throws<ArgumentNullException>(() => guard.Inspect(null));
        }

        // ---- Helpers ------------------------------------------------------------------------

        private GuardVerdict Inspect(AgentContext ctx, string action, string target, DecisionRequest request)
        {
            var input = new GuardInput(ctx, request, null, new AgentDecision(action, target, "Said."), null, null);
            return guard.Inspect(input);
        }

        // The village guard's own actions, as a context: follow_player available only while not
        // following, stop_following only while following, move_to needing a target. Returns the
        // stop_following handler, whose availability checks a test can count.
        private static (AgentContext Ctx, FakeHandler Stop) Guard(bool isFollowing, params string[] targets)
        {
            var actions = new ActionRegistry();
            var stop = new FakeHandler(IsFollowing);
            actions.Register(Definition("follow_player", false), new FakeHandler(ctx => !IsFollowing(ctx)));
            actions.Register(Definition("stop_following", false), stop);
            actions.Register(Definition("move_to", true), new FakeHandler());

            var registry = new TargetRegistry();
            foreach (string id in targets.Length > 0 ? targets : new[] { "tower", "training_dummy" })
            {
                registry.Register(id, new object());
            }

            var state = new Dictionary<string, object> { ["isFollowing"] = isFollowing };
            return (new AgentContext(new AgentIdentity { Name = "Guard" }, "", null, state, registry, actions), stop);
        }

        // A request whose schema offers what is available in ctx, as DecideAsync builds it.
        private static DecisionRequest Offering(AgentContext ctx)
        {
            return new DecisionRequest { Schema = DecisionSchema.Build(ctx) };
        }

        private static ActionDefinition Definition(string id, bool requiresTarget)
        {
            return new ActionDefinition
            {
                Id = id,
                Description = id,
                RequiresTarget = requiresTarget,
                ExampleStimulus = "Example for " + id + ".",
            };
        }

        private static bool IsFollowing(AgentContext ctx)
        {
            return (bool)ctx.State["isFollowing"];
        }
    }
}
