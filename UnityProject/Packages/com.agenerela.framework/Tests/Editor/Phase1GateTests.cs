using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    /// <summary>
    /// The five checks that close Phase 1 (build plan §3, #11). If these pass, the milestone is
    /// genuinely done. Each failure message names the measured rule that broke.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every gate runs the whole pipeline, <see cref="Agent.DecideAsync"/> against
    /// <see cref="FakeProvider"/>, and asserts on the request the provider received: first its
    /// <see cref="DecisionSchema"/>, then that schema as <see cref="JsonSchemaSerializer"/> writes
    /// it, which is what Ollama will be sent from Phase 2. The unit tests of #7, #8, #9 and #10
    /// cover each part on its own; these prove each rule survives the parts joined. The overlap is
    /// deliberate, so that one file a reviewer can read in two minutes proves the phase.
    /// </para>
    /// <para>
    /// Ollama uses the schema only to constrain sampling and never shows it to the model (findings,
    /// 10 October 2026). The rules below hold because they shape what can be sampled and what the
    /// examples demonstrate, not because the model reads them.
    /// </para>
    /// </remarks>
    public sealed class Phase1GateTests
    {
        private const string Idle = "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}";

        // One few-shot line, as FewShotBuilder writes it: <label>: "<stimulus>" -> action: <id>, target: <id>
        private static readonly Regex ExampleLine =
            new Regex("^(?<label>.+?): \"(?<stimulus>.*)\" -> action: (?<action>[^,]+), target: (?<target>\\S+)");

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public async Task Gate1_StateMaskingRemovesRedundantActions()
        {
            var fake = new FakeProvider(Idle);
            var following = Fixtures.VillageGuard(fake, isFollowing: true);

            await Fixtures.NotCancelled(following.DecideAsync("Keep close."));
            var request = fake.LastRequest;

            Assert.That(Allowed(request.Schema, DecisionSchema.ActionFieldName), Does.Not.Contain("follow_player"),
                "State masking regressed: an agent already following was offered follow_player. Availability "
              + "decides whether an action is in the enum at all, so a redundant command is unexpressible "
              + "rather than forbidden in prose (build plan §2.2 rule 1). The constrained schema, with the few-shot "
              + "block, took accuracy from 58.5% to 84.9% over the prose-rules baseline, and wrong-but-legal "
              + "answers from 20.8% to 3.8% (Appendix A).");
            Assert.That(JsonEnum(request.Schema, DecisionSchema.ActionFieldName), Does.Not.Contain("follow_player"),
                "State masking was lost in serialization: the JSON Schema Ollama is sent still allows "
              + "follow_player, so the sampler can produce it whatever DecisionSchema says (#9).");
            Assert.That(Examples(request.FewShotBlock).Select(e => e.Action), Does.Not.Contain("follow_player"),
                "The few-shot block demonstrates follow_player, which this agent is not offered. Examples come "
              + "from the same availability answer as the enum (#7, #10), or they teach an answer the schema forbids.");

            // The mask is derived from state on every decision, not a fixed omission: the same guard,
            // not following, is offered follow_player and not stop_following.
            var idle = Fixtures.VillageGuard(fake, isFollowing: false);
            await Fixtures.NotCancelled(idle.DecideAsync("Keep close."));

            Assert.That(Allowed(fake.LastRequest.Schema, DecisionSchema.ActionFieldName),
                Does.Contain("follow_player").And.Not.Contain("stop_following"),
                "Availability must be recomputed from the agent's state on every decision (#7). An idle guard "
              + "has to be offered follow_player, and not stop_following.");
        }

        [Test]
        public async Task Gate2_TargetIsRequiredWithNoTargetSentinel()
        {
            var fake = new FakeProvider("{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"There is no well here.\"}");
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the well."));
            var target = Field(fake.LastRequest.Schema, DecisionSchema.TargetFieldName);

            Assert.That(target, Is.Not.Null,
                "The target field is missing while targets are registered (#8).");
            Assert.That(target.Required, Is.True,
                "'target' must be required. Left optional, a 4B model omitted it even when the command named "
              + "one: target naming went 0/5 to 5/5 when it was made required with no_target (Appendix A, DR-004).");
            Assert.That(target.AllowedValues, Does.Contain(TargetRegistry.NoTarget),
                "'target' must offer the no_target sentinel. It is how the model says that what it was asked "
              + "for is not here, instead of substituting the nearest listed target (build plan §2.3 rule 2).");
            Assert.That(target.AllowedValues, Has.None.Null.And.None.EqualTo(""),
                "'target' must never offer \"\" or null as its empty value: Gemini rejects an empty enum "
              + "string with HTTP 400 (Appendix A). The empty value is no_target.");

            var json = Json(fake.LastRequest.Schema);
            Assert.That(json["required"].Values<string>(), Does.Contain(DecisionSchema.TargetFieldName),
                "The JSON Schema Ollama is sent does not list 'target' as required, so the sampler may "
              + "leave it out: the 0/5 failure of Appendix A (#9).");
            Assert.That(JsonEnum(fake.LastRequest.Schema, DecisionSchema.TargetFieldName),
                Does.Contain(TargetRegistry.NoTarget).And.None.EqualTo(""),
                "The JSON Schema's target enum must carry no_target and never \"\" (#9).");

            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget),
                "An answer of no_target must come back from the pipeline as no_target.");

            // Required end to end: a reply that leaves the target out is not read as "no target".
            fake.Reply = "{\"action\":\"move_to\",\"statement\":\"On my way.\"}";
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. .*no 'target'"));

            var missing = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(missing.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError),
                "A reply with no 'target', when the schema asked for one, must be a pipeline error. Reading it "
              + "as no_target would hide exactly the omission that requiring the field fixed (Appendix A).");
        }

        [Test]
        public async Task Gate3_FieldOrderIsActionTargetStatement()
        {
            var fake = new FakeProvider(Idle);
            var agent = Fixtures.VillageGuard(fake);

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            var schema = fake.LastRequest.Schema;
            var expected = new[] { "action", "target", "statement" };

            Assert.That(schema.Fields.Select(f => f.Name), Is.EqualTo(expected),
                "Field order regressed. 'action' must be generated before 'target', and both before "
              + "'statement': with the free text first, the model writes a reply and then picks whichever "
              + "action agrees with it, and commands collapse to 'none'. Measured at +11.7 accuracy points "
              + "(build plan §2.3 rule 1, Appendix A). No reasoning field may go before 'action' either: it "
              + "scored 35.3%, below the 41.2% baseline.");

            var json = Json(schema);
            Assert.That(((JObject)json["properties"]).Properties().Select(p => p.Name), Is.EqualTo(expected),
                "The JSON Schema Ollama is sent lists its properties out of order. A provider that takes the "
              + "order from the schema then generates the free text first, which undoes the +11.7 points (#9).");
            Assert.That(json["propertyOrdering"].Values<string>(), Is.EqualTo(expected),
                "propertyOrdering must state action, target, statement: Gemini orders fields by it rather than "
              + "by the properties' order (build plan §2.3 rule 1, #9).");
        }

        [Test]
        public async Task Gate4_FewShotUsesOnlySensibleTargets()
        {
            var fake = new FakeProvider(Idle);
            var agent = Fixtures.VillageGuard(fake);

            // Registered so that blind rotation through the registry would hand move_to the training
            // dummy and attack_target the well: the bug that once demonstrated "Pick up the Blacksmith"
            // (#75). In the fixture's own order, rotation happens to pick the right targets.
            agent.Targets.Unregister("tower");
            agent.Targets.Unregister("training_dummy");
            agent.Targets.Register("training_dummy", new object());
            agent.Targets.Register("well", new object());
            agent.Targets.Register("tower", new object());

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            var request = fake.LastRequest;
            var offeredActions = Allowed(request.Schema, DecisionSchema.ActionFieldName);
            var offeredTargets = Allowed(request.Schema, DecisionSchema.TargetFieldName);
            var examples = Examples(request.FewShotBlock);

            Assert.That(examples.Select(e => e.Action).Where(a => a != ActionRegistry.None),
                Is.EqualTo(offeredActions.Where(a => a != ActionRegistry.None)),
                "The few-shot block must match the actions offered: one example per available action, in the "
              + "enum's order, and none for an action the agent is not offered. Generated examples were the "
              + "largest single lever measured, +35.3 points (Appendix A, #10).");

            foreach (var example in examples)
            {
                Assert.That(offeredActions, Does.Contain(example.Action),
                    $"Example \"{example.Stimulus}\" answers '{example.Action}', which the schema does not offer.");

                if (example.Target == TargetRegistry.NoTarget)
                {
                    continue;
                }

                Assert.That(offeredTargets, Does.Contain(example.Target),
                    $"Example \"{example.Stimulus}\" names '{example.Target}', which is not a registered target.");

                Assert.That(agent.Actions.TryGet(example.Action, out var definition, out _), Is.True);
                Assert.That(definition.PreferredExampleTargets, Does.Contain(example.Target),
                    $"Example \"{example.Stimulus}\" pairs '{example.Action}' with '{example.Target}', which is "
                  + "not one of that action's PreferredExampleTargets. Blind rotation through the registry once "
                  + "demonstrated \"Pick up the Blacksmith\"; an example must use a target its verb sensibly "
                  + "applies to (build plan §2.3 rule 4, #10, #75).");
            }
        }

        [Test]
        public async Task Gate5_EmptyTargetRegistryRemovesTargetField()
        {
            var fake = new FakeProvider("{\"action\":\"none\",\"statement\":\"\"}");
            var agent = Fixtures.VillageGuard(fake);
            agent.Targets.Unregister("tower");
            agent.Targets.Unregister("training_dummy");

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            var schema = fake.LastRequest.Schema;

            // Which fields, in any order: Gate 3 owns the order.
            Assert.That(schema.Fields.Select(f => f.Name), Is.EquivalentTo(new[] { "action", "statement" }),
                "With no target registered, the 'target' field must be absent entirely, not left offering "
              + "no_target alone (build plan §3, Phase 1). There is nothing to point at, and every action that "
              + "needs a target is already masked (#7, #8).");
            Assert.That(Allowed(schema, DecisionSchema.ActionFieldName),
                Does.Not.Contain("move_to").And.Not.Contain("attack_target"),
                "An action that needs a target cannot be offered while no target is registered (#7): the model "
              + "could choose it, and nothing could carry it out.");

            var json = Json(schema);
            Assert.That(((JObject)json["properties"]).ContainsKey(DecisionSchema.TargetFieldName), Is.False,
                "The JSON Schema Ollama is sent still has a 'target' property with no targets registered (#9).");
            Assert.That(json["required"].Values<string>(), Does.Not.Contain(DecisionSchema.TargetFieldName),
                "The JSON Schema still requires 'target' with no targets registered, so no answer can satisfy it (#9).");
            Assert.That(json["propertyOrdering"].Values<string>(), Does.Not.Contain(DecisionSchema.TargetFieldName),
                "The JSON Schema's propertyOrdering still names 'target' with no targets registered (#9).");

            Assert.That(result.Decision, Is.Not.Null,
                "A reply with no 'target', to a schema that asked for none, must still be read as a decision.");
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
        }

        private static SchemaField Field(DecisionSchema schema, string name)
        {
            return schema.Fields.SingleOrDefault(f => f.Name == name);
        }

        private static IReadOnlyList<string> Allowed(DecisionSchema schema, string name)
        {
            var field = Field(schema, name);
            Assert.That(field, Is.Not.Null, $"The schema has no '{name}' field.");
            return field.AllowedValues;
        }

        private static JObject Json(DecisionSchema schema)
        {
            return JObject.Parse(new JsonSchemaSerializer().Serialize(schema));
        }

        private static IEnumerable<string> JsonEnum(DecisionSchema schema, string name)
        {
            return Json(schema)["properties"][name]["enum"].Values<string>();
        }

        // Every example line of the block, header excluded. A line that does not parse fails the test,
        // so a change to the line format cannot make these gates pass by matching nothing.
        private static List<(string Stimulus, string Action, string Target)> Examples(string block)
        {
            var lines = block.Split('\n');
            Assert.That(lines[0], Is.EqualTo("Examples of correct decisions:"));

            var examples = new List<(string Stimulus, string Action, string Target)>();
            foreach (var line in lines.Skip(1))
            {
                var match = ExampleLine.Match(line);
                Assert.That(match.Success, Is.True, $"Could not read the few-shot line: {line}");
                examples.Add((match.Groups["stimulus"].Value, match.Groups["action"].Value, match.Groups["target"].Value));
            }

            Assert.That(examples, Is.Not.Empty, "The few-shot block has no examples.");
            return examples;
        }
    }
}
