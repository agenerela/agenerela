using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests
{
    public sealed class GbnfSerializerTests
    {
        private static string Serialize(DecisionSchema schema) => new GbnfSerializer().Serialize(schema);

        private static SchemaField Action(params string[] values) => new SchemaField
        {
            Name = DecisionSchema.ActionFieldName,
            AllowedValues = values,
            Required = true,
        };

        private static SchemaField Target(params string[] values) => new SchemaField
        {
            Name = DecisionSchema.TargetFieldName,
            AllowedValues = values,
            Required = true,
        };

        private static SchemaField Statement() => new SchemaField
        {
            Name = DecisionSchema.StatementFieldName,
            AllowedValues = null,
            Required = true,
        };

        private static DecisionSchema FollowingAgent() => new DecisionSchema(new[]
        {
            Action("stop_following", "move_to", "none"),
            Target(TargetRegistry.NoTarget, "bridge", "tower"),
            Statement(),
        });

        private static string Golden(string name)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var path = Path.Combine(projectRoot, "Packages", "com.agenerela.framework", "Tests", "Editor",
                "Schema", "Goldens", name);
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        [Test]
        public void FollowingAgentMatchesGolden()
        {
            Assert.That(Serialize(FollowingAgent()), Is.EqualTo(Golden("following-agent.gbnf")));
        }

        [Test]
        public void BuiltFollowingAgentMatchesGolden()
        {
            var context = FollowingAgentContext();

            Assert.That(Serialize(DecisionSchema.Build(context)), Is.EqualTo(Golden("following-agent.gbnf")));
        }

        [Test]
        public void NoTargetShapeMatchesGolden()
        {
            var schema = new DecisionSchema(new[] { Action("stop_following", "none"), Statement() });

            Assert.That(Serialize(schema), Is.EqualTo(Golden("no-target.gbnf")));
        }

        [Test]
        public void StatementOnlyShapeMatchesGolden()
        {
            Assert.That(Serialize(DecisionSchema.StatementOnly()), Is.EqualTo(Golden("statement-only.gbnf")));
        }

        [Test]
        public void PairedSchemaMatchesGolden()
        {
            var schema = new DecisionSchema(
                new[]
                {
                    Action("move_to", "complete_quest", "none"),
                    Target("village_gate", "quest_wolf", TargetRegistry.NoTarget),
                    Statement(),
                },
                new[]
                {
                    new ActionTargets(new[] { "move_to" }, new[] { "village_gate", "quest_wolf" }),
                    new ActionTargets(new[] { "complete_quest" }, new[] { "quest_wolf" }),
                    new ActionTargets(new[] { "none" }, new[] { TargetRegistry.NoTarget }),
                });

            Assert.That(Serialize(schema), Is.EqualTo(Golden("paired.gbnf")));
        }

        [Test]
        public void PromptBuilderRequestSchemaMatchesGolden()
        {
            var context = FollowingAgentContext();
            var request = PromptBuilder.Build(context, DecisionSchema.Build(context), null);

            Assert.That(Serialize(request.Schema), Is.EqualTo(Golden("prompt-builder.gbnf")));
        }

        [Test]
        public void GrammarEnumAcceptsAnAllowedActionAndRejectsAnUnlistedAction()
        {
            var grammar = Serialize(FollowingAgent());
            var actionRule = Rule(grammar, "value-0");
            var actionMatcher = EnumMatcher(actionRule);

            Assert.That(actionMatcher.IsMatch("\"move_to\""), Is.True);
            Assert.That(actionMatcher.IsMatch("\"hack\""), Is.False);
        }

        [Test]
        public void PairedBranchesRestrictActionsAndTargetsTogether()
        {
            var schema = new DecisionSchema(
                new[] { Action("move_to", "none"), Target("gate", TargetRegistry.NoTarget), Statement() },
                new[]
                {
                    new ActionTargets(new[] { "move_to" }, new[] { "gate" }),
                    new ActionTargets(new[] { "none" }, new[] { TargetRegistry.NoTarget }),
                });
            var grammar = Serialize(schema);

            Assert.That(EnumMatcher(Rule(grammar, "pair-0-value-0")).IsMatch("\"move_to\""), Is.True);
            Assert.That(EnumMatcher(Rule(grammar, "pair-0-value-0")).IsMatch("\"none\""), Is.False);
            Assert.That(EnumMatcher(Rule(grammar, "pair-0-value-1")).IsMatch("\"gate\""), Is.True);
            Assert.That(EnumMatcher(Rule(grammar, "pair-0-value-1")).IsMatch("\"no_target\""), Is.False);
        }

        [Test]
        public void PairedSchemaRejectsActionsMissingFromPairs()
        {
            var schema = new DecisionSchema(
                new[] { Action("move_to", "none"), Target("gate", TargetRegistry.NoTarget), Statement() },
                new[] { new ActionTargets(new[] { "move_to" }, new[] { "gate" }) });

            Assert.Throws<ArgumentException>(() => Serialize(schema));
        }

        [Test]
        public void RejectsAnEmptyEnum()
        {
            Assert.Throws<ArgumentException>(() => Serialize(new DecisionSchema(new[] { Action(), Statement() })));
        }

        [TestCase("")]
        [TestCase(null)]
        public void RejectsAnEmptyEnumValue(string value)
        {
            Assert.Throws<ArgumentException>(() => Serialize(new DecisionSchema(new[] { Action("none", value) })));
        }

        [Test]
        public void RejectsDuplicateEnumValues()
        {
            Assert.Throws<ArgumentException>(() => Serialize(new DecisionSchema(new[] { Action("none", "none") })));
        }

        [Test]
        public void RejectsDuplicateFieldNames()
        {
            Assert.Throws<ArgumentException>(() => Serialize(new DecisionSchema(new[] { Statement(), Statement() })));
        }

        [Test]
        public void PairedSchemaRejectsAnActionAssignedToMoreThanOnePair()
        {
            var schema = new DecisionSchema(
                new[] { Action("move_to", "none"), Target("gate", TargetRegistry.NoTarget), Statement() },
                new[]
                {
                    new ActionTargets(new[] { "move_to" }, new[] { "gate" }),
                    new ActionTargets(new[] { "move_to", "none" }, new[] { TargetRegistry.NoTarget }),
                });

            Assert.Throws<ArgumentException>(() => Serialize(schema));
        }

        [Test]
        public void PairedSchemaRejectsValuesOutsideTheFlatEnums()
        {
            var schema = new DecisionSchema(
                new[] { Action("move_to", "none"), Target("gate", TargetRegistry.NoTarget), Statement() },
                new[]
                {
                    new ActionTargets(new[] { "move_to" }, new[] { "other" }),
                    new ActionTargets(new[] { "none" }, new[] { TargetRegistry.NoTarget }),
                });

            Assert.Throws<ArgumentException>(() => Serialize(schema));
        }

        [Test]
        public void PairedSchemaRejectsMissingActionOrTargetFields()
        {
            var schema = new DecisionSchema(
                new[] { Action("none"), Statement() },
                new[] { new ActionTargets(new[] { "none" }, new[] { TargetRegistry.NoTarget }) });

            Assert.Throws<ArgumentException>(() => Serialize(schema));
        }

        [Test]
        public void PairedSchemaRejectsAnEmptyPairList()
        {
            var schema = new DecisionSchema(
                new[] { Action("none"), Target(TargetRegistry.NoTarget), Statement() },
                Array.Empty<ActionTargets>());

            Assert.Throws<ArgumentException>(() => Serialize(schema));
        }

        [Test]
        public void PairedSchemaRejectsEmptyPairValues()
        {
            var schema = new DecisionSchema(
                new[] { Action("none"), Target(TargetRegistry.NoTarget), Statement() },
                new[] { new ActionTargets(Array.Empty<string>(), new[] { TargetRegistry.NoTarget }) });

            Assert.Throws<ArgumentException>(() => Serialize(schema));
        }

        [Test]
        public void EnumIdsAreEscapedForJsonAndGbnf()
        {
            var schema = new DecisionSchema(new[]
            {
                Action("say\"hi", "café", "back\\slash", "none"),
                Statement(),
            });
            var grammar = Serialize(schema);

            Assert.That(grammar, Does.Contain("\\\""));
            Assert.That(grammar, Does.Contain("\\u00e9"));
            Assert.That(grammar, Does.Contain("\\\\"));
        }

        [Test]
        public void EnumIdsEscapeControlCharactersAndUnpairedSurrogates()
        {
            var schema = new DecisionSchema(new[]
            {
                Action("control\u0001character", "unpaired\ud800surrogate", "none"),
                Statement(),
            });
            var grammar = Serialize(schema);

            Assert.That(grammar, Does.Contain("\\\\u0001"));
            Assert.That(grammar, Does.Contain("\\\\ud800"));
        }

        private static string Rule(string grammar, string name) => grammar
            .Split('\n')
            .Single(line => line.StartsWith(name + " ::= ", StringComparison.Ordinal))
            .Substring(name.Length + 5);

        // This small test-only matcher reads literal alternatives from a generated enum rule,
        // then checks whether a JSON string token is one of those alternatives.
        private static Regex EnumMatcher(string rule)
        {
            var literals = Regex.Matches(rule, "\"(?:\\\\.|[^\"])*\"")
                .Cast<Match>()
                .Select(match => Regex.Escape(UnquoteGbnf(match.Value)));
            return new Regex("^(?:" + string.Join("|", literals) + ")$");
        }

        private static string UnquoteGbnf(string terminal) => terminal.Substring(1, terminal.Length - 2)
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\");

        private static AgentContext FollowingAgentContext()
        {
            var actions = new ActionRegistry();
            actions.Register(Definition("follow_player", false), Handler(ctx => !IsFollowing(ctx)));
            actions.Register(Definition("stop_following", false), Handler(IsFollowing));
            actions.Register(Definition("move_to", true), Handler(ctx => true));

            var targets = new TargetRegistry();
            targets.Register("bridge", new object());
            targets.Register("tower", new object());

            return new AgentContext(
                new AgentIdentity { Name = "Village Guard" },
                "Wait here.",
                Array.Empty<string>(),
                new Dictionary<string, object> { { "isFollowing", true } },
                targets,
                actions);
        }

        private static ActionDefinition Definition(string id, bool requiresTarget) => new ActionDefinition
        {
            Id = id,
            Description = $"Action {id}",
            RequiresTarget = requiresTarget,
        };

        private static IActionHandler Handler(Func<AgentContext, bool> isAvailable) =>
            new DelegateActionHandler(isAvailable, (context, decision) => { });

        private static bool IsFollowing(AgentContext context) => (bool)context.State["isFollowing"];
    }
}
