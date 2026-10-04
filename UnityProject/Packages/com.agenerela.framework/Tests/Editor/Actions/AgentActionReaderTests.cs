using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Agenerela.Tests
{
    /// <summary>How RegisterMethods reads [AgentAction], [Example] and [Available] methods (#50).</summary>
    public sealed class AgentActionReaderTests
    {
        private sealed class Tower { }

        private sealed class Villager { }

        private sealed class Bare
        {
            public int Waves;

            [AgentAction("wave", "Wave hello.")]
            public void Wave()
            {
                Waves++;
            }
        }

        private sealed class Ordered
        {
            [AgentAction("wait", "Stay where you are.")]
            public void Wait() { }

            [AgentAction("attack", "Strike a named thing.")]
            public void Attack() { }

            [AgentAction("move_to", "Walk to a named place.")]
            public void MoveTo() { }
        }

        private sealed class Scrambled
        {
            public AgentContext Context;
            public AgentDecision Decision;
            public object Target;
            public int Calls;

            // Deliberately out of the usual order: each parameter is filled by its type.
            [AgentAction("inspect", "Look closely at something.")]
            public void Inspect(AgentDecision decision, object target, AgentContext ctx)
            {
                Decision = decision;
                Target = target;
                Context = ctx;
                Calls++;
            }
        }

        private sealed class TypedTarget
        {
            public Tower Reached;

            // Private, as a method with no access modifier is in C#.
            [AgentAction("move_to", "Walk to a named place.", RequiresTarget = true)]
            private void MoveTo(Tower tower)
            {
                Reached = tower;
            }
        }

        private sealed class ValueTarget
        {
            public int Building = -1;

            [AgentAction("assign_worker", "Send a worker to a building.", RequiresTarget = true)]
            public void Assign(int building)
            {
                Building = building;
            }
        }

        private sealed class StaticAction
        {
            public static int Rings;

            [AgentAction("ring_bell", "Ring the alarm bell.")]
            public static void Ring()
            {
                Rings++;
            }
        }

        private sealed class Failing
        {
            [AgentAction("fail", "Always fails.")]
            public void Fail()
            {
                throw new InvalidTimeZoneException("thrown by the game");
            }
        }

        private sealed class Follower
        {
            [AgentAction("follow_player", "Walk with the person who asked.")]
            public void Follow() { }

            [AgentAction("stop_following", "Stop walking with them.")]
            public void Stop() { }

            [Available("follow_player")]
            public bool CanFollow(AgentContext ctx)
            {
                return !(bool)ctx.State["isFollowing"];
            }

            [Available("stop_following")]
            public bool CanStop(AgentContext ctx)
            {
                return (bool)ctx.State["isFollowing"];
            }
        }

        private sealed class SharedCheck
        {
            public bool Busy;

            [AgentAction("move_to", "Walk to a named place.")]
            public void MoveTo() { }

            [AgentAction("attack", "Strike a named thing.")]
            public void Attack() { }

            [AgentAction("wave", "Wave hello.")]
            public void Wave() { }

            [Available("move_to")]
            [Available("attack")]
            public bool NotBusy()
            {
                return !Busy;
            }
        }

        private class Npc
        {
            public string Ran;

            [AgentAction("wave", "Wave hello.")]
            private void Wave()
            {
                Ran = "npc wave";
            }

            [AgentAction("greet", "Say hello.")]
            public virtual void Greet()
            {
                Ran = "npc greet";
            }

            [AgentAction("rest", "Sit down for a while.")]
            public virtual void Rest()
            {
                Ran = "npc rest";
            }
        }

        private sealed class GateGuard : Npc
        {
            [AgentAction("patrol", "Walk the walls.")]
            public void Patrol()
            {
                Ran = "guard patrol";
            }

            // No attribute: still the base class's greet, now running this body.
            public override void Greet()
            {
                Ran = "guard greet";
            }

            // Declares its own: this attribute replaces the base class's.
            [AgentAction("rest", "Lean on the gate.")]
            public override void Rest()
            {
                Ran = "guard rest";
            }
        }

        // Owners RegisterMethods must refuse, one problem each.
        private sealed class ReturnsBool
        {
            [AgentAction("wave", "Wave hello.")]
            public bool Wave() => true;
        }

        private sealed class ReturnsEnumerator
        {
            [AgentAction("wave", "Wave hello.")]
            public IEnumerator Wave()
            {
                yield break;
            }
        }

        private sealed class TwoTargets
        {
            [AgentAction("give", "Hand an item to someone.")]
            public void Give(object item, object recipient) { }
        }

        private sealed class TwoContexts
        {
            [AgentAction("wave", "Wave hello.")]
            public void Wave(AgentContext first, AgentContext second) { }
        }

        private sealed class TwoDecisions
        {
            [AgentAction("wave", "Wave hello.")]
            public void Wave(AgentDecision first, AgentDecision second) { }
        }

        private sealed class RefParameter
        {
            [AgentAction("wave", "Wave hello.")]
            public void Wave(ref object target) { }
        }

        private sealed class GenericMethod
        {
            [AgentAction("wave", "Wave hello.")]
            public void Wave<T>(T target) { }
        }

        private sealed class DuplicateId
        {
            [AgentAction("wave", "Wave hello.")]
            public void First() { }

            [AgentAction("wave", "Wave again.")]
            public void Second() { }
        }

        private sealed class MalformedId
        {
            [AgentAction("Wave", "Wave hello.")]
            public void Wave() { }
        }

        private sealed class ReservedId
        {
            [AgentAction("none", "Do nothing.")]
            public void Idle() { }
        }

        private sealed class ExampleWithoutAction
        {
            [Example("Wave at the {0}.")]
            public void Wave() { }
        }

        private sealed class CheckReturnsVoid
        {
            [AgentAction("wave", "Wave hello.")]
            public void Wave() { }

            [Available("wave")]
            public void CanWave() { }
        }

        private sealed class CheckTakesTarget
        {
            [AgentAction("wave", "Wave hello.")]
            public void Wave() { }

            [Available("wave")]
            public bool CanWave(object target) => true;
        }

        private sealed class CheckForUnknownId
        {
            [AgentAction("wave", "Wave hello.")]
            public void Wave() { }

            [Available("wav")]
            public bool CanWave() => true;
        }

        private sealed class TwoChecks
        {
            [AgentAction("wave", "Wave hello.")]
            public void Wave() { }

            [Available("wave")]
            public bool First() => true;

            [Available("wave")]
            public bool Second() => true;
        }

        private sealed class ActionAndCheck
        {
            [AgentAction("wave", "Wave hello.")]
            [Available("wave")]
            public void Wave() { }
        }

        [Test]
        public void AnActionWithoutAnExampleHasAnEmptyOne()
        {
            var registry = new ActionRegistry();

            registry.RegisterMethods(new Bare());

            var definition = registry.Definitions.Single();
            Assert.That(definition.Id, Is.EqualTo("wave"));
            Assert.That(definition.Description, Is.EqualTo("Wave hello."));
            Assert.That(definition.RequiresTarget, Is.False);
            Assert.That(definition.ExampleStimulus, Is.Empty);
            Assert.That(definition.PreferredExampleTargets, Is.Empty);
        }

        [Test]
        public void ActionsAreRegisteredInDeclarationOrder()
        {
            var registry = new ActionRegistry();

            registry.RegisterMethods(new Ordered());

            Assert.That(Ids(registry), Is.EqualTo(new[] { "wait", "attack", "move_to" }),
                "The schema enum is built in registration order, so it must not depend on how " +
                "reflection happens to list the methods.");
        }

        [Test]
        public void AMethodWithNoParametersRuns()
        {
            var registry = new ActionRegistry();
            var owner = new Bare();
            registry.RegisterMethods(owner);

            Execute(registry, "wave", TargetRegistry.NoTarget, new TargetRegistry());

            Assert.That(owner.Waves, Is.EqualTo(1));
        }

        [Test]
        public void EachParameterIsFilledByItsType()
        {
            var registry = new ActionRegistry();
            var owner = new Scrambled();
            registry.RegisterMethods(owner);
            var tower = new Tower();
            var targets = new TargetRegistry();
            targets.Register("tower", tower);
            var ctx = Context(targets);
            var decision = new AgentDecision("inspect", "tower", "Let me see.");

            Handler(registry, "inspect").Execute(ctx, decision);

            Assert.That(owner.Context, Is.SameAs(ctx));
            Assert.That(owner.Decision, Is.SameAs(decision));
            Assert.That(owner.Target, Is.SameAs(tower));
        }

        [Test]
        public void NoTargetGivesTheTargetParameterNull()
        {
            var registry = new ActionRegistry();
            var owner = new Scrambled();
            registry.RegisterMethods(owner);
            var targets = new TargetRegistry();
            targets.Register("tower", new Tower());

            Execute(registry, "inspect", TargetRegistry.NoTarget, targets);

            Assert.That(owner.Calls, Is.EqualTo(1));
            Assert.That(owner.Target, Is.Null);
        }

        [Test]
        public void ATargetArrivesAsItsParametersType()
        {
            var registry = new ActionRegistry();
            var owner = new TypedTarget();
            registry.RegisterMethods(owner);
            var tower = new Tower();
            var targets = new TargetRegistry();
            targets.Register("tower", tower);

            Execute(registry, "move_to", "tower", targets);

            Assert.That(owner.Reached, Is.SameAs(tower));
        }

        [Test]
        public void ATargetRegisteredAsAnotherTypeIsAClearError()
        {
            var registry = new ActionRegistry();
            var owner = new TypedTarget();
            registry.RegisterMethods(owner);
            var targets = new TargetRegistry();
            targets.Register("tower", new Villager());

            var exception = Assert.Throws<InvalidOperationException>(
                () => Execute(registry, "move_to", "tower", targets));

            Assert.That(exception.Message, Does.Contain("TypedTarget.MoveTo"));
            Assert.That(exception.Message, Does.Contain("Tower"));
            Assert.That(exception.Message, Does.Contain("Villager"));
            Assert.That(owner.Reached, Is.Null);
        }

        [Test]
        public void AnUnregisteredTargetIsAClearError()
        {
            var registry = new ActionRegistry();
            registry.RegisterMethods(new TypedTarget());
            var targets = new TargetRegistry();
            targets.Register("tower", new Tower());

            var exception = Assert.Throws<InvalidOperationException>(
                () => Execute(registry, "move_to", "castle", targets));

            Assert.That(exception.Message, Does.Contain("'castle'"));
        }

        [Test]
        public void AValueTypeTargetArrivesAsRegistered()
        {
            var registry = new ActionRegistry();
            var owner = new ValueTarget();
            registry.RegisterMethods(owner);
            var targets = new TargetRegistry();
            targets.Register("forge", 3);

            Execute(registry, "assign_worker", "forge", targets);

            Assert.That(owner.Building, Is.EqualTo(3));
        }

        [Test]
        public void NoTargetForAValueTypeTargetIsAClearError()
        {
            // An int cannot be null, and passing 0 would silently name a building.
            var registry = new ActionRegistry();
            var owner = new ValueTarget();
            registry.RegisterMethods(owner);

            var exception = Assert.Throws<InvalidOperationException>(
                () => Execute(registry, "assign_worker", TargetRegistry.NoTarget, new TargetRegistry()));

            Assert.That(exception.Message, Does.Contain(TargetRegistry.NoTarget));
            Assert.That(owner.Building, Is.EqualTo(-1));
        }

        [Test]
        public void TheGameCodesOwnExceptionReachesTheCaller()
        {
            var registry = new ActionRegistry();
            registry.RegisterMethods(new Failing());

            var exception = Assert.Throws<InvalidTimeZoneException>(
                () => Execute(registry, "fail", TargetRegistry.NoTarget, new TargetRegistry()),
                "Reflection's TargetInvocationException would hide the developer's own exception.");

            Assert.That(exception.Message, Is.EqualTo("thrown by the game"));
        }

        [Test]
        public void AStaticMethodCanBeAnAction()
        {
            StaticAction.Rings = 0;
            var registry = new ActionRegistry();
            registry.RegisterMethods(new StaticAction());

            Execute(registry, "ring_bell", TargetRegistry.NoTarget, new TargetRegistry());

            Assert.That(StaticAction.Rings, Is.EqualTo(1));
        }

        [Test]
        public void AnActionWithoutACheckIsAlwaysAvailable()
        {
            var registry = new ActionRegistry();
            registry.RegisterMethods(new Bare());

            Assert.That(Handler(registry, "wave").IsAvailable(Context(new TargetRegistry())), Is.True);
        }

        [Test]
        public void ChecksDecideAvailabilityFromTheAgentsState()
        {
            // #7's worked example: an agent already following is not offered follow_player.
            var registry = new ActionRegistry();
            registry.RegisterMethods(new Follower());
            var following = Context(new TargetRegistry(), new Dictionary<string, object> { { "isFollowing", true } });
            var idle = Context(new TargetRegistry(), new Dictionary<string, object> { { "isFollowing", false } });

            Assert.That(Handler(registry, "follow_player").IsAvailable(following), Is.False);
            Assert.That(Handler(registry, "stop_following").IsAvailable(following), Is.True);
            Assert.That(Handler(registry, "follow_player").IsAvailable(idle), Is.True);
            Assert.That(Handler(registry, "stop_following").IsAvailable(idle), Is.False);
        }

        [Test]
        public void OneCheckCanGateSeveralActions()
        {
            var registry = new ActionRegistry();
            registry.RegisterMethods(new SharedCheck { Busy = true });
            var ctx = Context(new TargetRegistry());

            Assert.That(Handler(registry, "move_to").IsAvailable(ctx), Is.False);
            Assert.That(Handler(registry, "attack").IsAvailable(ctx), Is.False);
            Assert.That(Handler(registry, "wave").IsAvailable(ctx), Is.True);
        }

        [Test]
        public void ABaseClassesActionsComeFirstPrivateOnesIncluded()
        {
            var registry = new ActionRegistry();

            registry.RegisterMethods(new GateGuard());

            Assert.That(Ids(registry), Is.EqualTo(new[] { "wave", "greet", "patrol", "rest" }));
        }

        [TestCase("wave", "npc wave")]
        [TestCase("greet", "guard greet")]
        [TestCase("patrol", "guard patrol")]
        [TestCase("rest", "guard rest")]
        public void AnInheritedActionRunsTheMostDerivedBody(string actionId, string ran)
        {
            var registry = new ActionRegistry();
            var owner = new GateGuard();
            registry.RegisterMethods(owner);

            Execute(registry, actionId, TargetRegistry.NoTarget, new TargetRegistry());

            Assert.That(owner.Ran, Is.EqualTo(ran));
        }

        [Test]
        public void AnOverrideThatDeclaresTheActionReplacesTheBaseClassesAttribute()
        {
            var registry = new ActionRegistry();

            registry.RegisterMethods(new GateGuard());

            Assert.That(registry.TryGet("rest", out var rest, out _), Is.True);
            Assert.That(rest.Description, Is.EqualTo("Lean on the gate."));
        }

        [TestCase(typeof(ReturnsBool), "must return void")]
        [TestCase(typeof(ReturnsEnumerator), "must return void")]
        [TestCase(typeof(TwoTargets), "two targets")]
        [TestCase(typeof(TwoContexts), "two AgentContext")]
        [TestCase(typeof(TwoDecisions), "two AgentDecision")]
        [TestCase(typeof(RefParameter), "ref, out or in")]
        [TestCase(typeof(GenericMethod), "generic")]
        [TestCase(typeof(DuplicateId), "declared twice")]
        [TestCase(typeof(MalformedId), "uppercase")]
        [TestCase(typeof(ReservedId), "reserved")]
        [TestCase(typeof(ExampleWithoutAction), "no [AgentAction]")]
        [TestCase(typeof(CheckReturnsVoid), "must return bool")]
        [TestCase(typeof(CheckTakesTarget), "nothing or one AgentContext")]
        [TestCase(typeof(CheckForUnknownId), "no [AgentAction] with that id")]
        [TestCase(typeof(TwoChecks), "two [Available] checks")]
        [TestCase(typeof(ActionAndCheck), "both [AgentAction] and [Available]")]
        public void AMalformedOwnerIsRejectedAndNothingIsRegistered(Type ownerType, string problem)
        {
            var registry = new ActionRegistry();

            var exception = Assert.Throws<ArgumentException>(
                () => registry.RegisterMethods(Activator.CreateInstance(ownerType)));

            Assert.That(exception.Message, Does.Contain(problem));
            Assert.That(exception.ParamName, Is.EqualTo("owner"));
            Assert.That(registry.Definitions, Is.Empty);
        }

        [Test]
        public void ErrorsNameTheMethodsInvolved()
        {
            var registry = new ActionRegistry();

            var duplicate = Assert.Throws<ArgumentException>(() => registry.RegisterMethods(new DuplicateId()));
            var malformed = Assert.Throws<ArgumentException>(() => registry.RegisterMethods(new MalformedId()));

            Assert.That(duplicate.Message, Does.Contain("DuplicateId.First and DuplicateId.Second"));
            Assert.That(malformed.Message, Does.Contain("MalformedId.Wave"));
        }

        private static IActionHandler Handler(ActionRegistry registry, string actionId)
        {
            Assert.That(registry.TryGet(actionId, out _, out var handler), Is.True, $"'{actionId}' is not registered.");
            return handler;
        }

        private static void Execute(ActionRegistry registry, string actionId, string targetId, TargetRegistry targets)
        {
            Handler(registry, actionId).Execute(Context(targets), new AgentDecision(actionId, targetId, ""));
        }

        private static string[] Ids(ActionRegistry registry)
        {
            return registry.Definitions.Select(definition => definition.Id).ToArray();
        }

        private static AgentContext Context(TargetRegistry targets, IReadOnlyDictionary<string, object> state = null)
        {
            return new AgentContext(new AgentIdentity { Name = "Gate Guard" }, "", null, state, targets, new ActionRegistry());
        }
    }
}
