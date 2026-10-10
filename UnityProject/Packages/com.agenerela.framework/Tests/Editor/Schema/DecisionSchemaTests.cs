using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public sealed class DecisionSchemaTests
    {
        private const string IsFollowingKey = "isFollowing";

        [Test]
        public void FollowingAgentGetsTheIssueExample()
        {
            // #8's example, and #9's golden input: follow_player is absent because the agent is
            // already following, and two targets are registered.
            var schema = DecisionSchema.Build(Context(isFollowing: true, "bridge", "tower"));

            Assert.That(Names(schema), Is.EqualTo(new[] { "action", "target", "statement" }));
            Assert.That(schema.Fields[0].AllowedValues, Is.EqualTo(new[] { "stop_following", "move_to", "none" }));
            Assert.That(schema.Fields[1].AllowedValues, Is.EqualTo(new[] { "no_target", "bridge", "tower" }));
            Assert.That(schema.Fields[2].AllowedValues, Is.Null);
            Assert.That(schema.Fields.All(field => field.Required), Is.True);
        }

        [Test]
        public void ActionComesBeforeStatement()
        {
            // +11.7 points depends on this: with the free text first, commands collapsed to
            // "no action" (build plan §2.3 rule 1).
            var withTargets = DecisionSchema.Build(Context(isFollowing: false, "tower"));
            var withoutTargets = DecisionSchema.Build(Context(isFollowing: false));

            Assert.That(Names(withTargets), Is.EqualTo(new[] { "action", "target", "statement" }));
            Assert.That(Names(withoutTargets), Is.EqualTo(new[] { "action", "statement" }));
        }

        [Test]
        public void TargetIsRequiredAndHasNoTargetSentinel()
        {
            // Never optional (a 4B model omitted it 0/5) and never "" (Gemini answers HTTP 400).
            var target = Field(DecisionSchema.Build(Context(isFollowing: false, "tower")), "target");

            Assert.That(target.Required, Is.True);
            Assert.That(target.AllowedValues, Does.Contain(TargetRegistry.NoTarget));
            Assert.That(target.AllowedValues[0], Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(target.AllowedValues, Has.None.Null);
            Assert.That(target.AllowedValues, Has.None.EqualTo(""));
        }

        [Test]
        public void EmptyTargetRegistryRemovesTargetField()
        {
            var schema = DecisionSchema.Build(Context(isFollowing: true));

            Assert.That(Names(schema), Is.EqualTo(new[] { "action", "statement" }),
                "With no targets the field is gone, not left with no_target alone.");
            Assert.That(schema.Fields[0].AllowedValues, Is.EqualTo(new[] { "stop_following", "none" }));
        }

        [Test]
        public void NoneIsAlwaysPresentAndAlwaysLast()
        {
            // §2.2 rule 3: listed last, idling reads as a fallback rather than a default.
            var nothingRegistered = ContextWith(new ActionRegistry(), isFollowing: false);
            var everythingMasked = ContextWith(Registry(("wait", false, false), ("look", false, false)), isFollowing: false);

            var contexts = new[]
            {
                Context(isFollowing: true, "bridge", "tower"),
                Context(isFollowing: false, "bridge", "tower"),
                Context(isFollowing: true),
                nothingRegistered,
                everythingMasked,
            };

            foreach (var ctx in contexts)
            {
                var actions = Field(DecisionSchema.Build(ctx), "action").AllowedValues;

                Assert.That(actions.Last(), Is.EqualTo(ActionRegistry.None));
                Assert.That(actions.Count(id => id == ActionRegistry.None), Is.EqualTo(1));
            }
        }

        [Test]
        public void ActionEnumFollowsAvailabilityOrderWithNoneLast()
        {
            // Registered out of alphabetical order, with the second one masked, so an enum that
            // was sorted, or built from every registered action, would fail.
            var ctx = ContextWith(
                Registry(("wave", false, true), ("guard", false, false), ("attack", false, true), ("build", false, true)),
                isFollowing: false);

            var expected = ActionAvailability.For(ctx).Select(definition => definition.Id).ToList();
            expected.Add(ActionRegistry.None);
            var actions = Field(DecisionSchema.Build(ctx), "action").AllowedValues;

            Assert.That(actions, Is.EqualTo(expected));
            Assert.That(actions, Is.EqualTo(new[] { "wave", "attack", "build", "none" }));
        }

        [Test]
        public void ActionEnumOffersOnlyTheActionsAvailableNow()
        {
            var following = Field(DecisionSchema.Build(Context(isFollowing: true, "tower")), "action");
            var idle = Field(DecisionSchema.Build(Context(isFollowing: false, "tower")), "action");
            var noTargets = Field(DecisionSchema.Build(Context(isFollowing: false)), "action");

            Assert.That(following.AllowedValues, Does.Not.Contain("follow_player"));
            Assert.That(idle.AllowedValues, Does.Not.Contain("stop_following"));
            Assert.That(noTargets.AllowedValues, Does.Not.Contain("move_to"),
                "An action needing a target cannot be offered when there is nothing to target.");
        }

        [Test]
        public void StatementIsRequiredAndFreeForm()
        {
            var statement = Field(DecisionSchema.Build(Context(isFollowing: false, "tower")), "statement");

            Assert.That(statement.Required, Is.True);
            Assert.That(statement.AllowedValues, Is.Null);
        }

        [Test]
        public void FieldsCarryTheFrameworkDescriptions()
        {
            var schema = DecisionSchema.Build(Context(isFollowing: false, "tower"));

            Assert.That(Field(schema, "action").Description, Is.EqualTo(DecisionSchema.ActionDescription));
            Assert.That(Field(schema, "target").Description, Is.EqualTo(DecisionSchema.TargetDescription));
            Assert.That(Field(schema, "statement").Description, Is.EqualTo(DecisionSchema.StatementDescription));
        }

        [Test]
        public void DescriptionsMatchTheSerializerGoldenFile()
        {
            // Copied from #9's expected output. These are the strings the model reads.
            Assert.That(DecisionSchema.ActionDescription,
                Is.EqualTo("The one action to take now, from the allowed list."));
            Assert.That(DecisionSchema.TargetDescription,
                Is.EqualTo("What the action applies to. Use no_target when none of the listed targets applies."));
            Assert.That(DecisionSchema.StatementDescription,
                Is.EqualTo("What the agent says or announces. One or two short sentences, or empty."));
            Assert.That(DecisionSchema.NoneDescription, Is.EqualTo("Take no action"));
        }

        [Test]
        public void DescriptionsNameNoPlayerOrCharacter()
        {
            // The agent may be a country reading a report (DR-008).
            var descriptions = new[]
            {
                DecisionSchema.ActionDescription,
                DecisionSchema.TargetDescription,
                DecisionSchema.StatementDescription,
                DecisionSchema.NoneDescription,
            };

            foreach (var description in descriptions)
            {
                Assert.That(description.ToLowerInvariant(), Does.Not.Contain("player"));
                Assert.That(description.ToLowerInvariant(), Does.Not.Contain("character"));
            }
        }

        [Test]
        public void ABuiltSchemaDoesNotFollowLaterRegistryChanges()
        {
            // TargetRegistry.Ids is a live view; the schema must be a snapshot of one decision.
            var ctx = Context(isFollowing: false, "tower");
            var schema = DecisionSchema.Build(ctx);

            ctx.Targets.Register("bridge", new object());

            Assert.That(Field(schema, "target").AllowedValues, Is.EqualTo(new[] { "no_target", "tower" }));
            Assert.That(Field(DecisionSchema.Build(ctx), "target").AllowedValues,
                Is.EqualTo(new[] { "no_target", "tower", "bridge" }));
        }

        [Test]
        public void ListsCannotBeChangedThroughTheSchema()
        {
            var schema = DecisionSchema.Build(Context(isFollowing: false, "tower"));

            Assert.Throws<NotSupportedException>(() => ((IList<SchemaField>)schema.Fields).Add(new SchemaField()));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)schema.Fields[0].AllowedValues).Add("fly"));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)schema.Fields[1].AllowedValues).Clear());
        }

        [Test]
        public void ConstructorKeepsTheFieldsGivenInOrder()
        {
            var statement = new SchemaField { Name = "statement" };
            var action = new SchemaField { Name = "action" };
            var given = new List<SchemaField> { statement, action };

            var schema = new DecisionSchema(given);
            given.Clear();

            Assert.That(schema.Fields, Is.EqualTo(new[] { statement, action }));
        }

        [Test]
        public void ConstructorRejectsNullFields()
        {
            Assert.Throws<ArgumentNullException>(() => new DecisionSchema(null));
            Assert.Throws<ArgumentException>(() => new DecisionSchema(new SchemaField[] { new SchemaField(), null }));
        }

        [Test]
        public void BuildRejectsNullContext()
        {
            Assert.Throws<ArgumentNullException>(() => DecisionSchema.Build(null));
        }

        // follow_player and stop_following masked by state, move_to needing a target: the
        // village guard's three actions from #8.
        private static AgentContext Context(bool isFollowing, params string[] targetIds)
        {
            var actions = new ActionRegistry();
            actions.Register(Definition("follow_player"), Handler(ctx => !IsFollowing(ctx)));
            actions.Register(Definition("stop_following"), Handler(IsFollowing));
            actions.Register(Definition("move_to", requiresTarget: true), Handler(ctx => true));

            return ContextWith(actions, isFollowing, targetIds);
        }

        private static AgentContext ContextWith(ActionRegistry actions, bool isFollowing, params string[] targetIds)
        {
            var targets = new TargetRegistry();
            foreach (var id in targetIds)
            {
                targets.Register(id, new object());
            }

            return new AgentContext(
                new AgentIdentity { Name = "Guard" },
                "What should you do now?",
                Array.Empty<string>(),
                new Dictionary<string, object> { { IsFollowingKey, isFollowing } },
                targets,
                actions);
        }

        private static ActionRegistry Registry(params (string Id, bool RequiresTarget, bool Available)[] entries)
        {
            var actions = new ActionRegistry();
            foreach (var (id, requiresTarget, available) in entries)
            {
                actions.Register(Definition(id, requiresTarget), Handler(ctx => available));
            }

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

        private static IActionHandler Handler(Func<AgentContext, bool> isAvailable)
        {
            return new DelegateActionHandler(isAvailable, (ctx, decision) => { });
        }

        private static bool IsFollowing(AgentContext ctx)
        {
            return ctx.State.TryGetValue(IsFollowingKey, out var value) &&
                value is bool isFollowing &&
                isFollowing;
        }

        private static SchemaField Field(DecisionSchema schema, string name)
        {
            return schema.Fields.Single(field => field.Name == name);
        }

        private static string[] Names(DecisionSchema schema)
        {
            return schema.Fields.Select(field => field.Name).ToArray();
        }
    }
}
