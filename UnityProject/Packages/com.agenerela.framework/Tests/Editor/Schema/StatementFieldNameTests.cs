using System.Linq;
using System.Threading.Tasks;
using Agenerela.Providers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // The free-text field's name per call (DR-008, #151): DecideOptions.StatementFieldName names it
    // dialogue for the A/B run of the rename, and everything that reads the field reads it under the
    // name the schema was sent with. The default stays statement, byte for byte. Every await of
    // DecideAsync goes through Fixtures.NotCancelled.
    public sealed class StatementFieldNameTests
    {
        private const string Dialogue = DecisionSchema.DialogueFieldName;
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"dialogue\":\"On my way.\"}";

        private static DecideOptions Named(string name = Dialogue) => new DecideOptions { StatementFieldName = name };

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public async Task TheDefaultIsStatement()
        {
            Assert.That(new DecideOptions().StatementFieldName, Is.EqualTo("statement"));

            var fake = new FakeProvider("{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}");
            var result = await Fixtures.NotCancelled(Fixtures.VillageGuard(fake).DecideAsync("Go to the tower."));

            Assert.That(result.Request.Schema.StatementName, Is.EqualTo("statement"));
            Assert.That(result.Request.Schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action", "target", "statement" }));
            Assert.That(result.Decision.Statement, Is.EqualTo("On my way."));
        }

        [Test]
        public async Task NamedDialogueTheSchemaAsksForItLastAndTheDecisionReadsIt()
        {
            var fake = new FakeProvider(GoToTheTower);

            var result = await Fixtures.NotCancelled(Fixtures.VillageGuard(fake).DecideAsync("Go to the tower.", Named()));

            var schema = result.Request.Schema;
            Assert.That(schema.StatementName, Is.EqualTo(Dialogue));
            Assert.That(schema.AsksForStatement, Is.True);
            Assert.That(schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action", "target", "dialogue" }), "The order stays action, target, free text.");

            var sent = JObject.Parse(new JsonSchemaSerializer().Serialize(schema));
            Assert.That(sent["propertyOrdering"].Values<string>(), Is.EqualTo(new[] { "action", "target", "dialogue" }));
            Assert.That(sent["required"].Values<string>(), Does.Contain("dialogue"));
            Assert.That(((JObject)sent["properties"]).Properties().Select(p => p.Name), Has.None.EqualTo("statement"));

            Assert.That(result.Decision, Is.Not.Null, result.Telemetry.ErrorMessage);
            Assert.That(result.Decision.Statement, Is.EqualTo("On my way."));
            Assert.That(result.Telemetry.OrderLost, Is.False);
        }

        [Test]
        public async Task AReplyUnderTheOtherNameIsNotReadAsTheStatement()
        {
            var dialogueReply = new FakeProvider(GoToTheTower);
            var statementReply = new FakeProvider("{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}");

            var asStatement = await Fixtures.NotCancelled(Fixtures.VillageGuard(dialogueReply).DecideAsync("Go to the tower."));
            var asDialogue = await Fixtures.NotCancelled(Fixtures.VillageGuard(statementReply).DecideAsync("Go to the tower.", Named()));

            Assert.That(asStatement.Decision.Statement, Is.Empty, "A dialogue key was read under the default name.");
            Assert.That(asDialogue.Decision.Statement, Is.Empty, "A statement key was read in a call that named the field dialogue.");
        }

        [Test]
        public async Task TheOrderCheckWorksUnderEitherName()
        {
            var sortedDialogue = new FakeProvider("{\"action\":\"move_to\",\"dialogue\":\"On my way.\",\"target\":\"tower\"}");
            var sortedStatement = new FakeProvider("{\"action\":\"move_to\",\"statement\":\"On my way.\",\"target\":\"tower\"}");

            var dialogue = await Fixtures.NotCancelled(Fixtures.VillageGuard(sortedDialogue).DecideAsync("Go to the tower.", Named()));
            var statement = await Fixtures.NotCancelled(Fixtures.VillageGuard(sortedStatement).DecideAsync("Go to the tower."));

            Assert.That(dialogue.Telemetry.OrderLost, Is.True, "dialogue before target is out of order (#180)");
            Assert.That(statement.Telemetry.OrderLost, Is.True);
            Assert.That(dialogue.Decision.Statement, Is.EqualTo("On my way."), "Still read, as any other reply.");
        }

        [Test]
        public async Task TheExamplesAndTheRulesLineUseTheName()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Identity.StatementRules = "One short sentence.";
            var options = Named();
            options.IdleExampleStatement = "All quiet.";

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", options));

            Assert.That(result.Request.FewShotBlock, Does.Contain("action: none, target: no_target, dialogue: \"All quiet.\""));
            Assert.That(result.Request.FewShotBlock, Does.Not.Contain("statement:"));
            Assert.That(result.Request.SystemPrompt, Does.Contain("Your dialogue: One short sentence."));

            options.StatementFieldName = null;
            var defaulted = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", options));
            Assert.That(defaulted.Request.FewShotBlock, Does.Contain("statement: \"All quiet.\""), "Null is the default name.");
            Assert.That(defaulted.Request.SystemPrompt, Does.Contain("Your statement: One short sentence."));
        }

        [Test]
        public async Task MemoryRemembersTheReplyUnderItsNameAndLeavesItOutForMove()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var memory = new RollingHistory(turns: 4);
            agent.Memory = memory;

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", Named()));
            var second = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", Named()));

            Assert.That(second.Request.History, Has.Some.EqualTo(GoToTheTower));

            memory.Remembers = RememberedDecision.Move;
            var third = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", Named()));
            Assert.That(third.Request.History, Has.Some.EqualTo("{\"action\":\"move_to\",\"target\":\"tower\"}"),
                "Move leaves the statement out under the name it was remembered with.");
        }

        [Test]
        public void ARefusalLinesRequestAndReplyUseTheDecisionsName()
        {
            var decided = new DecisionRequest { Schema = DecisionSchema.StatementOnly(Dialogue), SystemPrompt = "You are a guard." };

            Assert.That(decided.Schema.Fields.Single().Name, Is.EqualTo(Dialogue));
            Assert.That(DecisionParser.TryParseStatement("{\"dialogue\":\"Not here.\"}", Dialogue, out string line, out _), Is.True);
            Assert.That(line, Is.EqualTo("Not here."));
            Assert.That(DecisionParser.TryParseStatement("{\"statement\":\"Not here.\"}", Dialogue, out _, out string problem), Is.False);
            Assert.That(problem, Does.Contain("'dialogue'"));
        }

        [TestCase("Dialogue")]
        [TestCase("")]
        [TestCase("two words")]
        [TestCase("action")]
        [TestCase("target")]
        [TestCase("also_targets")]
        [TestCase("outcome")]
        public void ANameTheReplyCannotHoldThrowsAtTheCall(string name)
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));

            Assert.That(() => agent.DecideAsync("Go to the tower.", Named(name)),
                Throws.ArgumentException.With.Message.Contains("StatementFieldName"));
        }

        [Test]
        public async Task AParameterOfTheSameNameFailsTheCallBeforeAnythingIsSent()
        {
            var actions = new[]
            {
                new ActionDefinition
                {
                    Id = "proclaim", Description = "Make a proclamation.",
                    Parameters = new[] { new ActionParameter { Name = Dialogue, Values = new[] { "short", "long" } } },
                },
            };
            var fake = new FakeProvider("{\"action\":\"proclaim\",\"dialogue\":\"short\"}");
            var agent = new Agent(new AgentIdentity { Name = "The Herald" }, actions, fake);
            agent.Bind("proclaim", new FakeHandler());

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("declares as a parameter"));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Announce the harvest.", Named()));

            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.RequestPreparation));
            Assert.That(result.Telemetry.ErrorMessage, Does.Contain("declares as a parameter"));
            Assert.That(fake.Requests, Is.Empty);
        }
    }
}
