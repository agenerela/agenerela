using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public class ActionAvailabilityTests
    {
        private const string IsFollowingKey = "isFollowing";

        [Test]
        public void FollowingAgentIsNotOfferedFollowAgain()
        {
            var ctx = Context(isFollowing: true);

            var available = ActionAvailability.For(ctx);

            Assert.That(Ids(available), Does.Not.Contain("follow_player"),
                "An agent already following must not be offered follow_player - the option " +
                "should be absent from the schema, not merely discouraged in the prompt.");
            Assert.That(Ids(available), Does.Contain("stop_following"));
        }

        [Test]
        public void IdleAgentIsNotOfferedStopFollowing()
        {
            var ctx = Context(isFollowing: false);

            var available = ActionAvailability.For(ctx);

            Assert.That(Ids(available), Does.Not.Contain("stop_following"),
                "An idle agent cannot stop following because it is not following yet.");
            Assert.That(Ids(available), Does.Contain("follow_player"));
        }

        [Test]
        public void EmptyTargetRegistryExcludesEveryTargetRequiringAction()
        {
            var ctx = Context(isFollowing: false, includeTarget: false);

            var available = ActionAvailability.For(ctx);

            Assert.That(available.Any(definition => definition.RequiresTarget), Is.False,
                "Actions that require a target should disappear when the context has no legal targets.");
            Assert.That(Ids(available), Does.Not.Contain("walk_to_target"));
            Assert.That(Ids(available), Does.Contain("follow_player"),
                "Non-target actions should still be offered when there are no targets.");
        }

        [Test]
        public void AvailabilityIsRecomputedForEachContext()
        {
            var actions = new ActionRegistry();
            actions.Register(
                Definition("context_sensitive_action"),
                new DelegateActionHandler(
                    IsFollowing,
                    (ctx, decision) => { }));

            var idleContext = ContextWith(actions, isFollowing: false, includeTarget: true);
            var followingContext = ContextWith(actions, isFollowing: true, includeTarget: true);

            Assert.That(Ids(ActionAvailability.For(idleContext)), Does.Not.Contain("context_sensitive_action"));
            Assert.That(Ids(ActionAvailability.For(followingContext)), Does.Contain("context_sensitive_action"));
        }

        [Test]
        public void ForRejectsNullContext()
        {
            Assert.Throws<ArgumentNullException>(() => ActionAvailability.For(null));
        }

        private static AgentContext Context(bool isFollowing, bool includeTarget = true)
        {
            return ContextWith(StandardActions(), isFollowing, includeTarget);
        }

        private static AgentContext ContextWith(ActionRegistry actions, bool isFollowing, bool includeTarget)
        {
            var targets = new TargetRegistry();
            if (includeTarget)
            {
                targets.Register("player", new object());
            }

            return new AgentContext(
                new AgentIdentity { Name = "Guide" },
                "What should you do now?",
                Array.Empty<string>(),
                new Dictionary<string, object>
                {
                    { IsFollowingKey, isFollowing }
                },
                targets,
                actions);
        }

        private static ActionRegistry StandardActions()
        {
            var actions = new ActionRegistry();

            actions.Register(
                Definition("follow_player"),
                new DelegateActionHandler(
                    ctx => !IsFollowing(ctx),
                    (ctx, decision) => { }));

            actions.Register(
                Definition("stop_following"),
                new DelegateActionHandler(
                    IsFollowing,
                    (ctx, decision) => { }));

            actions.Register(
                Definition("walk_to_target", requiresTarget: true),
                new DelegateActionHandler(
                    ctx => true,
                    (ctx, decision) => { }));

            return actions;
        }

        private static ActionDefinition Definition(string id, bool requiresTarget = false)
        {
            return new ActionDefinition
            {
                Id = id,
                Description = $"Action {id}",
                RequiresTarget = requiresTarget
            };
        }

        private static bool IsFollowing(AgentContext ctx)
        {
            return ctx.State.TryGetValue(IsFollowingKey, out var value) &&
                value is bool isFollowing &&
                isFollowing;
        }

        private static string[] Ids(IEnumerable<ActionDefinition> definitions)
        {
            return definitions.Select(definition => definition.Id).ToArray();
        }
    }
}
