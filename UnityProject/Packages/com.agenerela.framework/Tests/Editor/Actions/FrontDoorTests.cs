using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Agenerela.Tests
{
    /// <summary>The ways an action can reach an <see cref="ActionRegistry"/> (DR-011).</summary>
    public enum FrontDoor
    {
        /// <summary>An ActionDefinitionAsset, bound to a handler the developer writes.</summary>
        Asset,

        /// <summary>An [AgentAction] method, which is its own handler.</summary>
        Attribute,

        /// <summary>An [AgentAction] method whose definition an asset with the same id replaces.</summary>
        AttributeWithAsset,

        /// <summary>An ActionDefinition built in code, bound to a DelegateActionHandler.</summary>
        Code
    }

    /// <summary>
    /// DR-011's shared fixture: the same assertions run against one action defined through each front
    /// door, so nothing downstream (schema, few-shot, guards, telemetry) can tell which was used. A new
    /// front door adds a <see cref="FrontDoor"/> value and a case to the two switches at the bottom, and
    /// must pass everything here unchanged.
    /// </summary>
    [TestFixture(FrontDoor.Asset)]
    [TestFixture(FrontDoor.Attribute)]
    [TestFixture(FrontDoor.AttributeWithAsset)]
    [TestFixture(FrontDoor.Code)]
    public sealed class FrontDoorTests
    {
        private readonly FrontDoor door;
        private readonly List<Object> created = new List<Object>();

        public FrontDoorTests(FrontDoor door)
        {
            this.door = door;
        }

        [TearDown]
        public void DestroyCreatedAssets()
        {
            foreach (var asset in created)
            {
                Object.DestroyImmediate(asset);
            }

            created.Clear();
        }

        // The game code every door wires move_to to. Through the attribute door this class is also
        // the definition; the other doors describe the same action as data.
        private sealed class Guard
        {
            public bool Busy;
            public object MovedTo;

            [AgentAction("move_to", "Walk to a named place.", RequiresTarget = true)]
            [Example("Head over to the {0}.", "tower", "bridge")]
            public void MoveTo(AgentContext ctx, object target)
            {
                MovedTo = target;
            }

            [Available("move_to")]
            public bool CanMove()
            {
                return !Busy;
            }
        }

        // An attribute's id is fixed when it compiles, so each id the doors must refuse has a class.
        private sealed class UppercaseId
        {
            [AgentAction("Move_To", "Walk to a named place.")]
            public void MoveTo() { }
        }

        private sealed class SpacedId
        {
            [AgentAction("move to", "Walk to a named place.")]
            public void MoveTo() { }
        }

        private sealed class ReservedId
        {
            [AgentAction(ActionRegistry.None, "Do nothing.")]
            public void Idle() { }
        }

        [Test]
        public void RegistersOneDefinitionWithTheDeclaredFields()
        {
            var registry = new ActionRegistry();

            Register(registry, new Guard());

            Assert.That(registry.Definitions, Has.Count.EqualTo(1));
            var definition = registry.Definitions[0];
            Assert.That(definition.Id, Is.EqualTo("move_to"));
            Assert.That(definition.Description, Is.EqualTo("Walk to a named place."));
            Assert.That(definition.RequiresTarget, Is.True);
            Assert.That(definition.ExampleStimulus, Is.EqualTo("Head over to the {0}."));
            Assert.That(definition.PreferredExampleTargets, Is.EqualTo(new[] { "tower", "bridge" }));
        }

        [Test]
        public void TryGetAndHandlerForReturnTheRegisteredPair()
        {
            var registry = new ActionRegistry();
            Register(registry, new Guard());

            Assert.That(registry.TryGet("move_to", out var definition, out var handler), Is.True);
            Assert.That(definition, Is.SameAs(registry.Definitions[0]));
            Assert.That(handler, Is.Not.Null);
            Assert.That(registry.HandlerFor(definition), Is.SameAs(handler));
        }

        [Test]
        public void IsAvailableFollowsTheGamesState()
        {
            var registry = new ActionRegistry();
            var guard = new Guard();
            Register(registry, guard);
            var handler = registry.HandlerFor(registry.Definitions[0]);
            var ctx = Context(new TargetRegistry());

            Assert.That(handler.IsAvailable(ctx), Is.True);

            guard.Busy = true;

            Assert.That(handler.IsAvailable(ctx), Is.False,
                "State masking asks IsAvailable, so every front door must pass on the game's answer: " +
                "an action that cannot apply is left out of the schema, not forbidden in prose.");
        }

        [Test]
        public void ExecuteRunsTheGameCodeOnTheChosenTarget()
        {
            var registry = new ActionRegistry();
            var guard = new Guard();
            Register(registry, guard);
            var tower = new object();
            var targets = new TargetRegistry();
            targets.Register("tower", tower);
            targets.Register("bridge", new object());

            registry.HandlerFor(registry.Definitions[0])
                .Execute(Context(targets), new AgentDecision("move_to", "tower", "On my way."));

            Assert.That(guard.MovedTo, Is.SameAs(tower));
        }

        [Test]
        public void RegisteringTheSameIdTwiceIsRejected()
        {
            var registry = new ActionRegistry();
            Register(registry, new Guard());
            var first = registry.Definitions[0];

            var exception = Assert.Throws<InvalidOperationException>(() => Register(registry, new Guard()));

            Assert.That(exception.Message, Does.Contain("already registered"));
            Assert.That(registry.Definitions, Is.EqualTo(new[] { first }));
        }

        [TestCase("Move_To")]
        [TestCase("move to")]
        public void AMalformedIdIsRejected(string id)
        {
            var registry = new ActionRegistry();

            Assert.Throws<ArgumentException>(() => RegisterId(registry, id));

            Assert.That(registry.Definitions, Is.Empty);
        }

        [Test]
        public void TheReservedNoneIsRejected()
        {
            var registry = new ActionRegistry();

            var exception = Assert.Throws<ArgumentException>(() => RegisterId(registry, ActionRegistry.None));

            Assert.That(exception.Message, Does.Contain("reserved"));
            Assert.That(registry.Definitions, Is.Empty);
        }

        // move_to, run by the guard, through this fixture's door.
        private void Register(ActionRegistry registry, Guard guard)
        {
            switch (door)
            {
                case FrontDoor.Asset:
                    registry.Register(NewAsset(MoveTo("move_to")).Action, HandWritten(guard));
                    break;
                case FrontDoor.Attribute:
                    registry.RegisterMethods(guard);
                    break;
                case FrontDoor.AttributeWithAsset:
                    registry.RegisterMethods(guard, new[] { NewAsset(MoveTo("move_to")) });
                    break;
                case FrontDoor.Code:
                    registry.Register(MoveTo("move_to"), HandWritten(guard));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(door), door, "No case for this front door.");
            }
        }

        // An action with an id every door must refuse, through this fixture's door.
        private void RegisterId(ActionRegistry registry, string id)
        {
            switch (door)
            {
                case FrontDoor.Asset:
                    registry.Register(NewAsset(MoveTo(id)).Action, HandWritten(new Guard()));
                    break;
                case FrontDoor.Attribute:
                    registry.RegisterMethods(OwnerWithId(id));
                    break;
                case FrontDoor.AttributeWithAsset:
                    registry.RegisterMethods(OwnerWithId(id), new[] { NewAsset(MoveTo(id)) });
                    break;
                case FrontDoor.Code:
                    registry.Register(MoveTo(id), HandWritten(new Guard()));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(door), door, "No case for this front door.");
            }
        }

        private static object OwnerWithId(string id)
        {
            switch (id)
            {
                case "Move_To":
                    return new UppercaseId();
                case "move to":
                    return new SpacedId();
                case ActionRegistry.None:
                    return new ReservedId();
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, "No class above declares this id.");
            }
        }

        // What the guard's attributes say, as data.
        private static ActionDefinition MoveTo(string id)
        {
            return new ActionDefinition
            {
                Id = id,
                Description = "Walk to a named place.",
                RequiresTarget = true,
                ExampleStimulus = "Head over to the {0}.",
                PreferredExampleTargets = new[] { "tower", "bridge" }
            };
        }

        // The handler a developer writes when the action's definition is not its own method.
        private static IActionHandler HandWritten(Guard guard)
        {
            return new DelegateActionHandler(
                ctx => guard.CanMove(),
                (ctx, decision) => guard.MoveTo(ctx, ctx.Targets.TryGet(decision.TargetId, out var target) ? target : null));
        }

        private ActionDefinitionAsset NewAsset(ActionDefinition action)
        {
            var asset = ScriptableObject.CreateInstance<ActionDefinitionAsset>();
            asset.Action = action;
            created.Add(asset);
            return asset;
        }

        private static AgentContext Context(TargetRegistry targets)
        {
            return new AgentContext(new AgentIdentity { Name = "Gate Guard" }, "", null, null, targets);
        }
    }
}
