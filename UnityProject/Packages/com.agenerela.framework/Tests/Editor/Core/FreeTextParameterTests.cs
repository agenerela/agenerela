using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // Free-text parameters (DR-030): a parameter with a MaxLength and no values is a short text the model
    // writes itself, such as a name. The schema caps it with maxLength, the prompt's action list says so, the
    // example shows its ExampleValue quoted, the reply's text reaches AgentDecision.Arguments and the handler,
    // and a longer one is refused by SchemaLegalityGuard. A closed parameter is unchanged (ParameterTests).
    // Every await of DecideAsync goes through Fixtures.NotCancelled.
    public sealed class FreeTextParameterTests
    {
        private static ActionParameter WorldName(string example = null) => new ActionParameter
        {
            Name = "world_name", Description = "the world's name", MaxLength = 24, ExampleValue = example,
        };

        // A chronicler with one action that takes no target: it names a world, and its statement is the backstory.
        private static Agent Chronicler(FakeProvider fake, out System.Collections.Generic.List<AgentDecision> ran, ActionParameter name = null)
        {
            var found = new ActionDefinition
            {
                Id = "found_world", Description = "Name the new world and tell its story.", TakesNoTarget = true,
                ExampleStimulus = "A new world rises from the sea.", ExampleStatement = "The isles rose in one night.",
                Parameters = new[] { name ?? WorldName("Ashen Reach") },
            };
            var agent = new Agent(new AgentIdentity { Name = "The Chronicler", Role = "the one who names new worlds" }, new[] { found }, fake);
            var executed = new System.Collections.Generic.List<AgentDecision>();
            ran = executed;
            agent.Bind("found_world", new DelegateActionHandler(_ => true, (ctx, d) => executed.Add(d)));
            return agent;
        }

        private const string Founded = "{\"action\":\"found_world\",\"world_name\":\"The Salt Kingdoms\",\"statement\":\"Five crowns rose where the sea fell back.\"}";

        // ---- What a parameter may be ----------------------------------------------------------

        [Test]
        public void AFreeTextListsNoValuesAndKeepsItsDefaultAndExampleWithinItsLength()
        {
            Assert.That(ActionParameter.Validate(WorldName("Ashen Reach"), out string problem), Is.True, problem);

            var both = WorldName();
            both.Values = new[] { "a" };
            Assert.That(ActionParameter.Validate(both, out problem), Is.False);
            Assert.That(problem, Does.Contain("either values or a MaxLength"));

            Assert.That(ActionParameter.Validate(new ActionParameter { Name = "world_name", MaxLength = -1 }, out problem), Is.False);
            Assert.That(ActionParameter.Validate(WorldName(new string('a', 25)), out problem), Is.False);
            Assert.That(ActionParameter.Validate(new ActionParameter { Name = "world_name", MaxLength = 4, Default = "Long one" }, out problem), Is.False);
            Assert.That(ActionParameter.Validate(new ActionParameter { Name = "world_name" }, out problem), Is.False,
                "No values and no MaxLength is still a closed choice with nothing to choose.");
        }

        [Test]
        public void AllowsTakesAnyTextUpToTheLengthWithLineBreaksButNoOtherControlCharacter()
        {
            var p = WorldName();
            Assert.That(p.IsFreeText, Is.True);
            Assert.That(p.Allows("The Salt Kingdoms"), Is.True);
            Assert.That(p.Allows(""), Is.True, "Empty is a text; whether it will do is the game's to say.");
            Assert.That(p.Allows(new string('a', 24)), Is.True);
            Assert.That(p.Allows(new string('a', 25)), Is.False);
            Assert.That(p.Allows("two\nlines"), Is.True);
            Assert.That(p.Allows("bell\a"), Is.False);
            Assert.That(p.Allows(null), Is.False);
            Assert.That(p.IsNumber, Is.False);
        }

        // ---- The request ------------------------------------------------------------------------

        [Test]
        public async Task TheSchemaCapsItWithMaxLengthBeforeTheStatementAndListsNoEnum()
        {
            var fake = new FakeProvider(Founded);
            await Fixtures.NotCancelled(Chronicler(fake, out _).DecideAsync("Name this world."));

            var branch = fake.LastRequest.Schema.Pairs.Single(p => p.Actions.Contains("found_world"));
            var field = branch.Parameters.Single();
            Assert.That((field.Name, field.AllowedValues, field.MaxLength), Is.EqualTo(("world_name", (System.Collections.Generic.IReadOnlyList<string>)null, 24)));

            string json = new JsonSchemaSerializer().Serialize(fake.LastRequest.Schema);
            Assert.That(json, Does.Contain(
                "        \"world_name\": {\n" +
                "          \"type\": \"string\",\n" +
                "          \"description\": \"the world's name\",\n" +
                "          \"maxLength\": 24\n" +
                "        },\n"));
            Assert.That(json, Does.Contain("\"propertyOrdering\": [\"action\", \"world_name\", \"statement\"]"),
                "After the action, before the free-text field, as a closed parameter is.");
        }

        [Test]
        public async Task ThePromptSaysFreeTextAtMostNCharactersAndTheExampleQuotesItsValue()
        {
            var fake = new FakeProvider(Founded);
            await Fixtures.NotCancelled(Chronicler(fake, out _).DecideAsync("Name this world."));

            Assert.That(fake.LastRequest.SystemPrompt, Does.Contain(
                "- found_world: Name the new world and tell its story. world_name (the world's name): free text, at most 24 characters."));
            Assert.That(fake.LastRequest.FewShotBlock, Does.Contain("world_name: \"Ashen Reach\", statement: \"The isles rose in one night.\""));
        }

        [Test]
        public async Task WithNoExampleValueTheExampleLeavesTheTextOut()
        {
            var fake = new FakeProvider(Founded);
            await Fixtures.NotCancelled(Chronicler(fake, out _, WorldName()).DecideAsync("Name this world."));
            Assert.That(fake.LastRequest.FewShotBlock, Does.Not.Contain("world_name"));
        }

        // ---- The reply --------------------------------------------------------------------------

        [Test]
        public async Task TheTextReachesTheDecisionTheHandlerAndTheLog()
        {
            var fake = new FakeProvider(Founded);
            var agent = Chronicler(fake, out var ran);
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Name this world."));

            Assert.That(result.Verdict, Is.Null);
            Assert.That(result.Decision.Arguments["world_name"], Is.EqualTo("The Salt Kingdoms"));
            Assert.That(result.Decision.Statement, Is.EqualTo("Five crowns rose where the sea fell back."));
            Assert.That(agent.Execute(result), Is.True);
            Assert.That(ran.Single().Arguments["world_name"], Is.EqualTo("The Salt Kingdoms"));

            var line = DecisionLogLine.From(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, System.DateTime.UtcNow, 1));
            Assert.That(line.Decision.Arguments["world_name"], Is.EqualTo("The Salt Kingdoms"));
        }

        [Test]
        public async Task ATextLongerThanItsCapIsRejectedAndNothingRuns()
        {
            // A provider that never reads the schema, as a fake does, can send one; the guard holds the cap anyway.
            var fake = new FakeProvider("{\"action\":\"found_world\",\"world_name\":\"" + new string('a', 25) + "\",\"statement\":\"\"}");
            var agent = Chronicler(fake, out var ran);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SchemaLegalityGuard rejected 'found_world'"));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Name this world."));

            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(result.Verdict.Value.Reason, Is.EqualTo("'world_name' is longer than 24 characters or holds a control character."));
            Assert.That(agent.Execute(result), Is.False);
            Assert.That(ran, Is.Empty);
        }

        // ---- Declared on a method ---------------------------------------------------------------

        private sealed class Founder
        {
            public string Named;

            [AgentAction("found_world", "Name the new world.", TakesNoTarget = true)]
            [AgentParameter("world_name", "the world's name", MaxLength = 24)]
            public void Found([Argument("world_name")] string name) => Named = name;
        }

        [Test]
        public async Task AMethodDeclaresAFreeTextWithMaxLengthAndReceivesIt()
        {
            var fake = new FakeProvider(Founded);
            var agent = new Agent(new AgentIdentity { Name = "The Chronicler" }, new ActionDefinition[0], fake);
            var founder = new Founder();
            agent.Actions.RegisterMethods(founder);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Name this world."));

            Assert.That(result.Verdict, Is.Null);
            Assert.That(agent.Execute(result), Is.True);
            Assert.That(founder.Named, Is.EqualTo("The Salt Kingdoms"));
        }
    }
}
