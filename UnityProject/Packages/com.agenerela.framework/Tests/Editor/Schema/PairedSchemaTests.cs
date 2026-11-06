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
    // The paired schema (#190 B, DR-020): DecisionSchema.Pairs, its JSON form as a root anyOf,
    // SchemaLegalityGuard holding a paired request to it, and DecideOptions.PairTargetsWithActions.
    // Every await of DecideAsync goes through Fixtures.NotCancelled (see AgentTests).
    public sealed class PairedSchemaTests
    {
        // Three branches of a quest giver: complete_quest takes the quest, move_to anything, none
        // nothing. This exact string is the golden; compared with line endings normalised to \n.
        const string QuestGiverJson = @"{
  ""anyOf"": [
    {
      ""type"": ""object"",
      ""properties"": {
        ""action"": {
          ""type"": ""string"",
          ""description"": ""The one action to take now, from the allowed list."",
          ""enum"": [""move_to""]
        },
        ""target"": {
          ""type"": ""string"",
          ""description"": ""What the action applies to. Use no_target when none of the listed targets applies."",
          ""enum"": [""village_gate"", ""quest_wolf""]
        },
        ""statement"": {
          ""type"": ""string"",
          ""description"": ""What the agent says or announces. One or two short sentences, or empty.""
        }
      },
      ""required"": [""action"", ""target"", ""statement""],
      ""propertyOrdering"": [""action"", ""target"", ""statement""],
      ""additionalProperties"": false
    },
    {
      ""type"": ""object"",
      ""properties"": {
        ""action"": {
          ""type"": ""string"",
          ""description"": ""The one action to take now, from the allowed list."",
          ""enum"": [""complete_quest""]
        },
        ""target"": {
          ""type"": ""string"",
          ""description"": ""What the action applies to. Use no_target when none of the listed targets applies."",
          ""enum"": [""quest_wolf""]
        },
        ""statement"": {
          ""type"": ""string"",
          ""description"": ""What the agent says or announces. One or two short sentences, or empty.""
        }
      },
      ""required"": [""action"", ""target"", ""statement""],
      ""propertyOrdering"": [""action"", ""target"", ""statement""],
      ""additionalProperties"": false
    },
    {
      ""type"": ""object"",
      ""properties"": {
        ""action"": {
          ""type"": ""string"",
          ""description"": ""The one action to take now, from the allowed list."",
          ""enum"": [""none""]
        },
        ""target"": {
          ""type"": ""string"",
          ""description"": ""What the action applies to. Use no_target when none of the listed targets applies."",
          ""enum"": [""no_target""]
        },
        ""statement"": {
          ""type"": ""string"",
          ""description"": ""What the agent says or announces. One or two short sentences, or empty.""
        }
      },
      ""required"": [""action"", ""target"", ""statement""],
      ""propertyOrdering"": [""action"", ""target"", ""statement""],
      ""additionalProperties"": false
    }
  ]
}";

        private const string CompleteAtTheGate = "{\"action\":\"complete_quest\",\"target\":\"village_gate\",\"statement\":\"Done.\"}";
        private const string CompleteTheQuest = "{\"action\":\"complete_quest\",\"target\":\"quest_wolf\",\"statement\":\"Done.\"}";

        // ---- DecisionSchema.Build ------------------------------------------------------------

        [Test]
        public void ASchemaIsFlatUnlessAskedToPair()
        {
            var agent = QuestGiver(new FakeProvider());

            Assert.That(DecisionSchema.Build(Context(agent)).Pairs, Is.Null);
            Assert.That(DecisionSchema.Build(Context(agent), pairTargets: false).Pairs, Is.Null);
        }

        [Test]
        public void WithNoTargetsThereIsNothingToPair()
        {
            var agent = QuestGiver(new FakeProvider(), withTargets: false);

            var schema = DecisionSchema.Build(Context(agent), pairTargets: true);

            Assert.That(schema.Pairs, Is.Null);
            Assert.That(schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action", "statement" }));
        }

        [Test]
        public void EachActionIsPairedWithTheTargetsItTakes()
        {
            var agent = QuestGiver(new FakeProvider());
            agent.Actions.Register(new ActionDefinition { Id = "wave", Description = "Wave." }, new FakeHandler());
            agent.Actions.Register(new ActionDefinition { Id = "inspect", Description = "Look closely.", TargetCategories = new[] { "place" } }, new FakeHandler());
            agent.Targets.Register("odd_stone", new object());

            var schema = DecisionSchema.Build(Context(agent), pairTargets: true);

            Assert.That(Branches(schema), Is.EqualTo(new[]
            {
                "move_to -> village_gate, quest_wolf, odd_stone",
                "complete_quest -> quest_wolf",
                "wave -> no_target, village_gate, quest_wolf, odd_stone",
                "inspect -> no_target, village_gate",
                "none -> no_target",
            }));
        }

        [Test]
        public void APairedSchemaKeepsTheFlatFieldsAsTheyWere()
        {
            var agent = QuestGiver(new FakeProvider());

            var flat = DecisionSchema.Build(Context(agent));
            var paired = DecisionSchema.Build(Context(agent), pairTargets: true);

            Assert.That(paired.Fields.Count, Is.EqualTo(flat.Fields.Count));
            for (int i = 0; i < flat.Fields.Count; i++)
            {
                Assert.That(paired.Fields[i].Name, Is.EqualTo(flat.Fields[i].Name));
                Assert.That(paired.Fields[i].AllowedValues, Is.EqualTo(flat.Fields[i].AllowedValues));
            }
        }

        [Test]
        public void ActionsThatTakeTheSameTargetsShareOneBranch()
        {
            var agent = QuestGiver(new FakeProvider());
            agent.Actions.Register(new ActionDefinition { Id = "attack_target", Description = "Attack.", RequiresTarget = true }, new FakeHandler());
            agent.Actions.Register(new ActionDefinition { Id = "inspect", Description = "Look.", TargetCategories = new[] { "ghost" } }, new FakeHandler());

            var schema = DecisionSchema.Build(Context(agent), pairTargets: true);

            Assert.That(Branches(schema), Is.EqualTo(new[]
            {
                "move_to, attack_target -> village_gate, quest_wolf",
                "complete_quest -> quest_wolf",
                "inspect, none -> no_target",
            }));
        }

        [Test]
        public void AnActionThatNeedsATargetItCannotHaveIsLeftOut()
        {
            var agent = QuestGiver(new FakeProvider());
            agent.Targets.Unregister("quest_wolf");

            var flat = DecisionSchema.Build(Context(agent));
            var paired = DecisionSchema.Build(Context(agent), pairTargets: true);

            Assert.That(ActionEnum(flat), Is.EqualTo(new[] { "move_to", "complete_quest", "none" }));
            Assert.That(ActionEnum(paired), Is.EqualTo(new[] { "move_to", "none" }));
            Assert.That(Branches(paired), Is.EqualTo(new[] { "move_to -> village_gate", "none -> no_target" }));
        }

        [Test]
        public void ANullPairIsRefused()
        {
            Assert.Throws<ArgumentException>(() => new DecisionSchema(new[] { Statement() }, new ActionTargets[] { null }));
            Assert.Throws<ArgumentNullException>(() => new ActionTargets(null, new string[0]));
            Assert.Throws<ArgumentNullException>(() => new ActionTargets(new string[0], null));
        }

        [Test]
        public void ThePairsAreCopied()
        {
            var actions = new List<string> { "move_to" };
            var pairs = new List<ActionTargets> { new ActionTargets(actions, new[] { "village_gate" }) };

            var schema = new DecisionSchema(new[] { Statement() }, pairs);
            actions.Add("complete_quest");
            pairs.Clear();

            Assert.That(schema.Pairs, Has.Count.EqualTo(1));
            Assert.That(schema.Pairs[0].Actions, Is.EqualTo(new[] { "move_to" }));
        }

        // ---- JsonSchemaSerializer ------------------------------------------------------------

        [Test]
        public void APairedSchemaIsARootAnyOfOfCompleteObjects()
        {
            var agent = QuestGiver(new FakeProvider());

            string json = Serialize(DecisionSchema.Build(Context(agent), pairTargets: true));

            Assert.That(json, Is.EqualTo(QuestGiverJson.Replace("\r\n", "\n")));
        }

        [Test]
        public void AFlatSchemaIsSerializedAsBefore()
        {
            var agent = QuestGiver(new FakeProvider());

            string json = Serialize(DecisionSchema.Build(Context(agent)));

            Assert.That(json, Does.StartWith("{\n  \"type\": \"object\",\n  \"properties\": {"));
            Assert.That(json, Does.Not.Contain("anyOf"));
        }

        [Test]
        public void PairsThatNoGrammarCouldHonourAreRefused()
        {
            var cases = new Dictionary<string, ActionTargets[]>
            {
                ["empty list of pairs"] = new ActionTargets[0],
                ["two pairs"] = new[] { Pair("move_to", "village_gate"), Pair("move_to", "quest_wolf"), Pair("none", TargetRegistry.NoTarget) },
                ["in no pair"] = new[] { Pair("move_to", "village_gate") },
                ["are empty"] = new[] { Pair("move_to"), Pair("none", TargetRegistry.NoTarget) },
                ["null or empty"] = new[] { Pair("move_to", ""), Pair("none", TargetRegistry.NoTarget) },
                ["twice"] = new[] { Pair("move_to", "village_gate", "village_gate"), Pair("none", TargetRegistry.NoTarget) },
                ["does not offer"] = new[] { Pair("move_to", "tower"), Pair("none", TargetRegistry.NoTarget) },
            };

            foreach (var entry in cases)
            {
                var schema = new DecisionSchema(new[] { Action("move_to", "none"), Target(TargetRegistry.NoTarget, "village_gate", "quest_wolf"), Statement() }, entry.Value);

                var error = Assert.Throws<ArgumentException>(() => Serialize(schema), entry.Key);
                Assert.That(error.Message, Does.Contain(entry.Key), entry.Key);
            }
        }

        [Test]
        public void PairsNeedAnActionFieldAndATargetField()
        {
            var schema = new DecisionSchema(new[] { Action("move_to", "none"), Statement() }, new[] { Pair("move_to", "village_gate") });

            var error = Assert.Throws<ArgumentException>(() => Serialize(schema));

            Assert.That(error.Message, Does.Contain("needs an action field and a target field"));
        }

        // ---- SchemaLegalityGuard -------------------------------------------------------------

        [Test]
        public void APairThePairedSchemaDidNotOfferIsRejected()
        {
            var agent = QuestGiver(new FakeProvider());
            var ctx = Context(agent);
            var request = new DecisionRequest { Schema = DecisionSchema.Build(ctx, pairTargets: true) };

            var verdict = new SchemaLegalityGuard().Inspect(Input(ctx, request, "complete_quest", "village_gate"));

            Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(verdict.Reason, Does.Contain("not offered with this action"));
        }

        [Test]
        public void APairThePairedSchemaOfferedPasses()
        {
            var agent = QuestGiver(new FakeProvider());
            var ctx = Context(agent);
            var request = new DecisionRequest { Schema = DecisionSchema.Build(ctx, pairTargets: true) };

            Assert.That(new SchemaLegalityGuard().Inspect(Input(ctx, request, "complete_quest", "quest_wolf")).Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(new SchemaLegalityGuard().Inspect(Input(ctx, request, "move_to", "quest_wolf")).Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(new SchemaLegalityGuard().Inspect(Input(ctx, request, ActionRegistry.None, "village_gate")).Kind, Is.EqualTo(GuardVerdictKind.Pass),
                "none always passes, as it always has.");
        }

        [Test]
        public void AFlatRequestHoldsNoPairToAnything()
        {
            var agent = QuestGiver(new FakeProvider());
            var ctx = Context(agent);
            var request = new DecisionRequest { Schema = DecisionSchema.Build(ctx) };

            var verdict = new SchemaLegalityGuard().Inspect(Input(ctx, request, "complete_quest", "village_gate"));

            Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.Pass), "The category guard's to judge.");
        }

        // ---- DecideOptions.PairTargetsWithActions --------------------------------------------

        [Test]
        public async Task AnAgentSendsAFlatSchemaByDefault()
        {
            var provider = new FakeProvider(CompleteTheQuest);
            var agent = QuestGiver(provider);

            await Fixtures.NotCancelled(agent.DecideAsync("Here is the wolf's pelt."));

            Assert.That(provider.LastRequest.Schema.Pairs, Is.Null);
        }

        [Test]
        public async Task AnAgentAskedToPairSendsThePairedSchema()
        {
            var provider = new FakeProvider(CompleteTheQuest);
            var agent = QuestGiver(provider);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Here is the wolf's pelt.", new DecideOptions { PairTargetsWithActions = true }));

            Assert.That(Serialize(provider.LastRequest.Schema), Is.EqualTo(QuestGiverJson.Replace("\r\n", "\n")));
            Assert.That(result.Verdict, Is.Null);
            Assert.That(agent.Execute(result), Is.True);
        }

        [Test]
        public async Task PairingChangesNothingButTheSchema()
        {
            var flat = new FakeProvider(CompleteTheQuest);
            var paired = new FakeProvider(CompleteTheQuest);
            var options = new DecideOptions { ListActionsAndTargets = true };

            await Fixtures.NotCancelled(QuestGiver(flat).DecideAsync("Here is the wolf's pelt.", options));
            options.PairTargetsWithActions = true;
            await Fixtures.NotCancelled(QuestGiver(paired).DecideAsync("Here is the wolf's pelt.", options));

            Assert.That(paired.LastRequest.SystemPrompt, Is.EqualTo(flat.LastRequest.SystemPrompt));
            Assert.That(paired.LastRequest.FewShotBlock, Is.EqualTo(flat.LastRequest.FewShotBlock));
            Assert.That(paired.LastRequest.Stimulus, Is.EqualTo(flat.LastRequest.Stimulus));
        }

        [Test]
        public async Task AnActionThePairedSchemaLeavesOutHasNoExample()
        {
            var provider = new FakeProvider("{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}");
            var agent = QuestGiver(provider);
            agent.Targets.Unregister("quest_wolf");

            await Fixtures.NotCancelled(agent.DecideAsync("Anything for me?", new DecideOptions { PairTargetsWithActions = true }));
            Assert.That(provider.LastRequest.FewShotBlock, Does.Not.Contain("complete_quest"));

            await Fixtures.NotCancelled(agent.DecideAsync("Anything for me?"));
            Assert.That(provider.LastRequest.FewShotBlock, Does.Contain("complete_quest"), "Flat, the action and its example stay.");
        }

        [Test]
        public async Task AModelThatBreaksThePairingIsRejected()
        {
            var agent = QuestGiver(new FakeProvider(CompleteAtTheGate));
            LogAssert.Expect(LogType.Warning, new Regex("SchemaLegalityGuard rejected 'complete_quest' on 'village_gate'"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Here is the wolf's pelt.", new DecideOptions { PairTargetsWithActions = true }));

            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(agent.Execute(result), Is.False);
        }

        // ---- Helpers -------------------------------------------------------------------------

        // move_to takes any target; complete_quest takes a quest, with an example on it so the few
        // shot shows it; village_gate is a place and quest_wolf a quest.
        private static Agent QuestGiver(FakeProvider provider, bool withTargets = true)
        {
            var actions = new[]
            {
                new ActionDefinition { Id = "move_to", Description = "Walk to a place.", RequiresTarget = true },
                new ActionDefinition
                {
                    Id = "complete_quest", Description = "Take what the player brought.", RequiresTarget = true,
                    TargetCategories = new[] { "quest" },
                    // village_gate second, so a flat schema still shows the example once the quest is gone.
                    ExampleStimulus = "I finished {0}.", PreferredExampleTargets = new[] { "quest_wolf", "village_gate" },
                },
            };

            var agent = new Agent(new AgentIdentity { Name = "Odile", Role = "a merchant" }, actions, provider);
            agent.Bind("move_to", new FakeHandler());
            agent.Bind("complete_quest", new FakeHandler());
            if (withTargets)
            {
                agent.Targets.Register("village_gate", new object(), "place");
                agent.Targets.Register("quest_wolf", new object(), "quest");
            }

            return agent;
        }

        private static AgentContext Context(Agent agent)
        {
            return new AgentContext(agent.Identity, "", null, null, agent.Targets, agent.Actions);
        }

        private static GuardInput Input(AgentContext ctx, DecisionRequest request, string action, string target)
        {
            return new GuardInput(ctx, request, null, new AgentDecision(action, target, ""), null, null);
        }

        private static IEnumerable<string> Branches(DecisionSchema schema)
        {
            return schema.Pairs.Select(p => string.Join(", ", p.Actions) + " -> " + string.Join(", ", p.Targets));
        }

        private static IReadOnlyList<string> ActionEnum(DecisionSchema schema)
        {
            return schema.Fields.First(f => f.Name == DecisionSchema.ActionFieldName).AllowedValues;
        }

        private static ActionTargets Pair(string action, params string[] targets) => new ActionTargets(new[] { action }, targets);

        private static SchemaField Action(params string[] allowed) => new SchemaField
        {
            Name = DecisionSchema.ActionFieldName, Description = DecisionSchema.ActionDescription, AllowedValues = allowed, Required = true,
        };

        private static SchemaField Target(params string[] allowed) => new SchemaField
        {
            Name = DecisionSchema.TargetFieldName, Description = DecisionSchema.TargetDescription, AllowedValues = allowed, Required = true,
        };

        private static SchemaField Statement() => new SchemaField
        {
            Name = DecisionSchema.StatementFieldName, Description = DecisionSchema.StatementDescription, Required = true,
        };

        private static string Serialize(DecisionSchema schema) => new JsonSchemaSerializer().Serialize(schema);
    }
}
