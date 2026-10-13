using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Agenerela.Tests
{
    public class ActionRegistryTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void DestroyCreatedAssets()
        {
            foreach (var asset in created)
            {
                Object.DestroyImmediate(asset);
            }

            created.Clear();
        }

        private sealed class FakeHandler : IActionHandler
        {
            public bool IsAvailable(AgentContext ctx)
            {
                return true;
            }

            public void Execute(AgentContext ctx, AgentDecision decision)
            {
            }
        }

        private sealed class Walker
        {
            public int Moves;

            [AgentAction("move_to", "Walk to a named place.", RequiresTarget = true)]
            public void MoveTo()
            {
                Moves++;
            }

            [AgentAction("wait", "Stay where you are.")]
            public void Wait()
            {
            }
        }

        private static ActionDefinition Definition(string id)
        {
            return new ActionDefinition
            {
                Id = id,
                Description = $"Action {id}"
            };
        }

        [Test]
        public void RegisterThenTryGetReturnsSameDefinitionAndHandler()
        {
            var registry = new ActionRegistry();
            var definition = Definition("build");
            var handler = new FakeHandler();

            registry.Register(definition, handler);

            Assert.That(registry.TryGet("build", out var foundDefinition, out var foundHandler), Is.True);
            Assert.That(foundDefinition, Is.SameAs(definition));
            Assert.That(foundHandler, Is.SameAs(handler));
        }

        [Test]
        public void DefinitionsPreserveInsertionOrder()
        {
            var registry = new ActionRegistry();
            var build = Definition("build");
            var move = Definition("move_to");
            var gather = Definition("gather");

            registry.Register(build, new FakeHandler());
            registry.Register(move, new FakeHandler());
            registry.Register(gather, new FakeHandler());

            Assert.That(registry.Definitions, Is.EqualTo(new[] { build, move, gather }));
        }

        [Test]
        public void DefinitionsIsALiveReadOnlyView()
        {
            var registry = new ActionRegistry();
            var definitions = registry.Definitions;

            registry.Register(Definition("build"), new FakeHandler());

            Assert.That(definitions, Has.Count.EqualTo(1));
            Assert.That(((ICollection<ActionDefinition>)definitions).IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => ((ICollection<ActionDefinition>)definitions).Clear());
        }

        [Test]
        public void TryGetReturnsFalseAndNullsForUnknownId()
        {
            var registry = new ActionRegistry();
            registry.Register(Definition("build"), new FakeHandler());

            Assert.That(registry.TryGet("attack", out var definition, out var handler), Is.False);
            Assert.That(definition, Is.Null);
            Assert.That(handler, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("Build")]
        [TestCase("move to")]
        public void TryGetReturnsFalseAndNullsForMalformedIds(string id)
        {
            var registry = new ActionRegistry();
            registry.Register(Definition("build"), new FakeHandler());

            Assert.That(registry.TryGet(id, out var definition, out var handler), Is.False);
            Assert.That(definition, Is.Null);
            Assert.That(handler, Is.Null);
        }

        [Test]
        public void RegisterRejectsDuplicateIdsAndLeavesRegistryUnchanged()
        {
            var registry = new ActionRegistry();
            var originalDefinition = Definition("build");
            var originalHandler = new FakeHandler();

            registry.Register(originalDefinition, originalHandler);

            var exception = Assert.Throws<InvalidOperationException>(
                () => registry.Register(Definition("build"), new FakeHandler()));

            Assert.That(exception.Message, Does.Contain("already registered"));
            Assert.That(registry.Definitions, Is.EqualTo(new[] { originalDefinition }));
            Assert.That(registry.TryGet("build", out var foundDefinition, out var foundHandler), Is.True);
            Assert.That(foundDefinition, Is.SameAs(originalDefinition));
            Assert.That(foundHandler, Is.SameAs(originalHandler));
        }

        [Test]
        public void RegisterRejectsNullDefinition()
        {
            var registry = new ActionRegistry();

            var exception = Assert.Throws<ArgumentNullException>(
                () => registry.Register(null, new FakeHandler()));

            Assert.That(exception.ParamName, Is.EqualTo("definition"));
            Assert.That(registry.Definitions, Is.Empty);
        }

        [Test]
        public void RegisterRejectsNullHandler()
        {
            var registry = new ActionRegistry();

            var exception = Assert.Throws<ArgumentNullException>(
                () => registry.Register(Definition("build"), null));

            Assert.That(exception.ParamName, Is.EqualTo("handler"));
            Assert.That(registry.Definitions, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("Build")]
        [TestCase("move to")]
        public void RegisterRejectsMalformedIds(string id)
        {
            var registry = new ActionRegistry();

            var exception = Assert.Throws<ArgumentException>(
                () => registry.Register(Definition(id), new FakeHandler()));

            Assert.That(exception.Message, Is.Not.Empty);
            Assert.That(registry.Definitions, Is.Empty);
        }

        [Test]
        public void RegisterRejectsReservedNoneId()
        {
            var registry = new ActionRegistry();

            var exception = Assert.Throws<ArgumentException>(
                () => registry.Register(Definition(ActionRegistry.None), new FakeHandler()));

            Assert.That(exception.Message, Does.Contain("reserved"));
            Assert.That(registry.Definitions, Is.Empty);
        }

        [Test]
        public void HandlerForReturnsRegisteredHandler()
        {
            var registry = new ActionRegistry();
            var definition = Definition("build");
            var handler = new FakeHandler();

            registry.Register(definition, handler);

            Assert.That(registry.HandlerFor(definition), Is.SameAs(handler));
        }

        [Test]
        public void HandlerForRejectsNullDefinition()
        {
            var registry = new ActionRegistry();

            var exception = Assert.Throws<ArgumentNullException>(() => registry.HandlerFor(null));

            Assert.That(exception.ParamName, Is.EqualTo("definition"));
        }

        [Test]
        public void HandlerForUnregisteredDefinitionThrowsClearError()
        {
            var registry = new ActionRegistry();

            var exception = Assert.Throws<KeyNotFoundException>(
                () => registry.HandlerFor(Definition("attack")));

            Assert.That(exception.Message, Does.Contain("attack"));
            Assert.That(exception.Message, Does.Contain("Register"));
        }

        [Test]
        public void DelegateActionHandlerCallsDelegates()
        {
            var decision = new AgentDecision("build", "house_site", "");
            var availableCalled = false;
            AgentDecision executedDecision = null;
            var handler = new DelegateActionHandler(
                ctx =>
                {
                    availableCalled = true;
                    return true;
                },
                (ctx, d) => executedDecision = d);

            Assert.That(handler.IsAvailable(null), Is.True);
            handler.Execute(null, decision);

            Assert.That(availableCalled, Is.True);
            Assert.That(executedDecision, Is.SameAs(decision));
        }

        [Test]
        public void DelegateActionHandlerRejectsNullDelegates()
        {
            Assert.Throws<ArgumentNullException>(
                () => new DelegateActionHandler(null, (ctx, decision) => { }));
            Assert.Throws<ArgumentNullException>(
                () => new DelegateActionHandler(ctx => true, null));
        }

        [Test]
        public void ActionHandlerBehaviourIsMonoBehaviourActionHandler()
        {
            Assert.That(typeof(ActionHandlerBehaviour).IsAbstract, Is.True);
            Assert.That(typeof(IActionHandler).IsAssignableFrom(typeof(ActionHandlerBehaviour)), Is.True);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(ActionHandlerBehaviour)), Is.True);
        }

        [Test]
        public void RegisterMethodsRegistersEveryAttributedMethod()
        {
            var registry = new ActionRegistry();

            registry.RegisterMethods(new Walker());

            Assert.That(registry.Definitions, Has.Count.EqualTo(2));
            Assert.That(registry.Definitions[0].Id, Is.EqualTo("move_to"));
            Assert.That(registry.Definitions[1].Id, Is.EqualTo("wait"));
        }

        [Test]
        public void RegisterMethodsLetsAnAssetWinTheDefinition()
        {
            var registry = new ActionRegistry();
            var walker = new Walker();
            var asset = NewAsset("move_to", "Go to the place they name.");

            registry.RegisterMethods(walker, new[] { asset });

            Assert.That(registry.TryGet("move_to", out var definition, out var handler), Is.True);
            Assert.That(definition, Is.SameAs(asset.Action),
                "Where an asset and an [AgentAction] define the same id, the asset wins (DR-011): its " +
                "wording is what Phase 4 varies between arms without a recompile.");
            Assert.That(registry.Definitions[0], Is.SameAs(asset.Action), "The action keeps its method's place.");

            handler.Execute(null, new AgentDecision("move_to", TargetRegistry.NoTarget, ""));

            Assert.That(walker.Moves, Is.EqualTo(1), "The method still runs the action the asset words.");
        }

        [Test]
        public void RegisterMethodsLeavesAssetsForOtherIdsToRegister()
        {
            var registry = new ActionRegistry();

            registry.RegisterMethods(new Walker(), new[] { NewAsset("attack", "Strike a named thing.") });

            Assert.That(registry.Definitions, Has.Count.EqualTo(2));
            Assert.That(registry.TryGet("attack", out _, out _), Is.False);
        }

        [Test]
        public void RegisterMethodsSkipsNullAssets()
        {
            var registry = new ActionRegistry();

            registry.RegisterMethods(new Walker(), new ActionDefinitionAsset[] { null });

            Assert.That(registry.Definitions, Has.Count.EqualTo(2));
        }

        [Test]
        public void RegisterMethodsRejectsTwoAssetsForOneMethod()
        {
            var registry = new ActionRegistry();
            var assets = new[] { NewAsset("move_to", "Walk to a named place."), NewAsset("move_to", "Go there.") };

            var exception = Assert.Throws<ArgumentException>(() => registry.RegisterMethods(new Walker(), assets));

            Assert.That(exception.ParamName, Is.EqualTo("assets"));
            Assert.That(exception.Message, Does.Contain("move_to"));
            Assert.That(registry.Definitions, Is.Empty);
        }

        [Test]
        public void RegisterMethodsRejectsAnIdAlreadyRegisteredAndStoresNothing()
        {
            var registry = new ActionRegistry();
            var wait = Definition("wait");
            registry.Register(wait, new FakeHandler());

            var exception = Assert.Throws<InvalidOperationException>(() => registry.RegisterMethods(new Walker()));

            Assert.That(exception.Message, Does.Contain("wait"));
            Assert.That(registry.Definitions, Is.EqualTo(new[] { wait }),
                "move_to must not be stored when the same call rejects wait.");
        }

        [Test]
        public void RegisterRejectsAnIdAMethodRegistered()
        {
            // The asset wins the definition, not a second handler: each action has one handler.
            var registry = new ActionRegistry();
            registry.RegisterMethods(new Walker());

            Assert.Throws<InvalidOperationException>(() => registry.Register(Definition("move_to"), new FakeHandler()));
            Assert.That(registry.Definitions, Has.Count.EqualTo(2));
        }

        [Test]
        public void RegisterMethodsRejectsNullOwner()
        {
            var registry = new ActionRegistry();

            var exception = Assert.Throws<ArgumentNullException>(() => registry.RegisterMethods(null));

            Assert.That(exception.ParamName, Is.EqualTo("owner"));
        }

        [Test]
        public void RegisterMethodsRejectsAnOwnerWithNoActions()
        {
            var registry = new ActionRegistry();

            var exception = Assert.Throws<ArgumentException>(() => registry.RegisterMethods(new object()));

            Assert.That(exception.Message, Does.Contain("no [AgentAction] methods"));
            Assert.That(registry.Definitions, Is.Empty);
        }

        private ActionDefinitionAsset NewAsset(string id, string description)
        {
            var asset = ScriptableObject.CreateInstance<ActionDefinitionAsset>();
            asset.name = id;
            asset.Action = new ActionDefinition { Id = id, Description = description };
            created.Add(asset);
            return asset;
        }
    }
}
