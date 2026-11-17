using System;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // A decision that asks for no statement (#266, DecideOptions.AskForStatement): the schema, flat,
    // without targets and paired, has no statement field; the prompt says nothing of one; the parsed
    // decision's statement is empty; a stopped decision gets no line; and telemetry and the decision
    // log say it was not asked. With the option on, the default, the request is what it always was.
    // Every await of DecideAsync goes through Fixtures.NotCancelled.
    public sealed class NoStatementTests
    {
        private const string Rules = "A marshal's voice: short orders.";

        private static string Reply(string action, string target) =>
            $"{{\"action\":\"{action}\",\"target\":\"{target}\",\"statement\":\"Forward, for the realm.\"}}";

        private static DecideOptions Silent() => new DecideOptions { AskForStatement = false };

        // ---- The default changes nothing ------------------------------------------------------

        [Test]
        public async Task OnByDefaultAndTheRequestIsTheSameAsAskingExplicitly()
        {
            Assert.That(new DecideOptions().AskForStatement, Is.True);

            var implicitly = new FakeProvider(Reply("march", "fenmark"));
            var explicitly = new FakeProvider(Reply("march", "fenmark"));
            await Fixtures.NotCancelled(Marshal(implicitly).DecideAsync("Your orders."));
            var result = await Fixtures.NotCancelled(Marshal(explicitly).DecideAsync("Your orders.", new DecideOptions { AskForStatement = true }));

            AssertSameRequest(implicitly.LastRequest, explicitly.LastRequest);
            Assert.That(implicitly.LastRequest.Schema.AsksForStatement, Is.True);
            Assert.That(result.Decision.Statement, Is.EqualTo("Forward, for the realm."));
            Assert.That(result.Telemetry.StatementAsked, Is.True);
            Assert.That(DecisionLogLine.From(Entry(result)).StatementAsked, Is.Null, "Left out, so an existing log line is unchanged.");
        }

        // ---- The schema ------------------------------------------------------------------------

        [Test]
        public async Task TheFlatSchemaHasActionAndTargetOnlyInThatOrder()
        {
            var fake = new FakeProvider(Reply("march", "fenmark"));
            await Fixtures.NotCancelled(Marshal(fake).DecideAsync("Your orders.", Silent()));

            var schema = fake.LastRequest.Schema;
            Assert.That(schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action", "target" }));
            Assert.That(schema.AsksForStatement, Is.False);

            string json = new JsonSchemaSerializer().Serialize(schema);
            Assert.That(json, Does.Not.Contain("statement"));
            Assert.That(json, Does.Contain("\"required\": [\"action\", \"target\"]"));
            Assert.That(json, Does.Contain("\"propertyOrdering\": [\"action\", \"target\"]"));
        }

        [Test]
        public async Task WithNoTargetRegisteredTheSchemaIsTheActionAlone()
        {
            var fake = new FakeProvider("{\"action\":\"rest\",\"statement\":\"Rest.\"}");
            var agent = Marshal(fake);
            agent.Targets.Unregister("fenmark");
            agent.Targets.Unregister("westmarch");

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your orders.", Silent()));

            Assert.That(fake.LastRequest.Schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action" }));
            Assert.That(result.Decision.ActionId, Is.EqualTo("rest"));
            Assert.That(result.Decision.Statement, Is.Empty);
        }

        [Test]
        public async Task EveryBranchOfThePairedSchemaLeavesTheStatementOut()
        {
            var fake = new FakeProvider(Reply("march", "fenmark"));
            await Fixtures.NotCancelled(Marshal(fake).DecideAsync("Your orders.",
                new DecideOptions { AskForStatement = false, PairTargetsWithActions = true }));

            var schema = fake.LastRequest.Schema;
            Assert.That(schema.Pairs, Is.Not.Null);
            Assert.That(schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action", "target" }));

            string json = new JsonSchemaSerializer().Serialize(schema);
            Assert.That(json, Does.Contain("\"anyOf\""));
            Assert.That(json, Does.Not.Contain("statement"));
        }

        [Test]
        public void ThePublicBuildLeavesOnlyTheStatementOut()
        {
            var agent = Marshal(new FakeProvider());
            var ctx = new AgentContext(agent.Identity, "", null, null, agent.Targets, agent.Actions);

            var asked = DecisionSchema.Build(ctx, pairTargets: true, askForStatement: true);
            var silent = DecisionSchema.Build(ctx, pairTargets: true, askForStatement: false);

            Assert.That(silent.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action", "target" }));
            Assert.That(asked.Fields.Take(2).Select(f => f.AllowedValues), Is.EqualTo(silent.Fields.Select(f => f.AllowedValues)));
            Assert.That(silent.Pairs.Select(p => p.Targets), Is.EqualTo(asked.Pairs.Select(p => p.Targets)), "The pairs are the same.");
            Assert.That(DecisionSchema.Build(ctx, pairTargets: false).AsksForStatement, Is.True, "The old overloads ask, as before.");
        }

        // ---- The prompt ------------------------------------------------------------------------

        [Test]
        public async Task ThePromptSaysNothingOfAStatement()
        {
            var asked = new FakeProvider(Reply("march", "fenmark"));
            var silent = new FakeProvider(Reply("march", "fenmark"));
            await Fixtures.NotCancelled(Marshal(asked).DecideAsync("Your orders."));
            await Fixtures.NotCancelled(Marshal(silent).DecideAsync("Your orders.", Silent()));

            Assert.That(asked.LastRequest.SystemPrompt, Does.Contain("Your statement: " + Rules), "The fixture has rules to leave out.");
            Assert.That(asked.LastRequest.FewShotBlock, Does.Contain(", statement: \"Onward.\""), "And example statements.");

            Assert.That(silent.LastRequest.SystemPrompt, Does.Not.Contain("statement"));
            Assert.That(silent.LastRequest.FewShotBlock, Does.Not.Contain("statement"));
            Assert.That(silent.LastRequest.SystemPrompt,
                Is.EqualTo(asked.LastRequest.SystemPrompt.Replace("\nYour statement: " + Rules, "")), "Only the rules line goes.");
            Assert.That(silent.LastRequest.FewShotBlock,
                Is.EqualTo(asked.LastRequest.FewShotBlock.Replace(", statement: \"Onward.\"", "")), "Only the example's statement goes.");
        }

        [Test]
        public async Task TheTargetRuleNoLongerAsksTheAgentToSaySomething()
        {
            var fake = new FakeProvider(Reply("march", "fenmark"));
            fake.Capabilities = new ProviderCapabilities { ReportsTokenProbabilities = false };

            var asked = Marshal(fake);
            await Fixtures.NotCancelled(asked.DecideAsync("Your orders."));
            string withStatement = fake.LastRequest.SystemPrompt;
            await Fixtures.NotCancelled(asked.DecideAsync("Your orders.", Silent()));
            string without = fake.LastRequest.SystemPrompt;

            Assert.That(withStatement, Does.Contain("choose action none and target no_target, and say it is not here. Never act"));
            Assert.That(without, Does.Contain(
                "Only these things are here: fenmark, westmarch. If you are asked to act on anything else, choose action none and target no_target. Never act on a different thing instead."));
        }

        // ---- The decision ----------------------------------------------------------------------

        [Test]
        public async Task AStatementSentAnywayIsDroppedAndTelemetryAndTheLogSaySo()
        {
            var fake = new FakeProvider(Reply("march", "fenmark"));
            var agent = Marshal(fake, out var march);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your orders.", Silent()));

            Assert.That(result.Telemetry.ErrorKind, Is.Null);
            Assert.That(result.Verdict, Is.Null);
            Assert.That(result.Decision.ActionId, Is.EqualTo("march"));
            Assert.That(result.Decision.TargetId, Is.EqualTo("fenmark"));
            Assert.That(result.Decision.Statement, Is.Empty, "The schema asked for none.");
            Assert.That(result.ModelDecision.Statement, Is.Empty);
            Assert.That(result.Telemetry.StatementAsked, Is.False);

            var line = DecisionLogLine.From(Entry(result));
            Assert.That(line.StatementAsked, Is.False);
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(line), Does.Contain("\"statementAsked\":false"));

            Assert.That(agent.Execute(result), Is.True);
            Assert.That(march.Executions.Single().Decision.Statement, Is.Empty);
        }

        [Test]
        public async Task AReplyWithoutAStatementIsRead()
        {
            var fake = new FakeProvider("{\"action\":\"march\",\"target\":\"westmarch\"}");

            var result = await Fixtures.NotCancelled(Marshal(fake).DecideAsync("Your orders.", Silent()));

            Assert.That(result.Telemetry.ErrorKind, Is.Null);
            Assert.That(result.Verdict, Is.Null);
            Assert.That(result.Decision.TargetId, Is.EqualTo("westmarch"));
        }

        [Test]
        public async Task MemoryRemembersTheReplyWithoutAStatement()
        {
            var fake = new FakeProvider(Reply("march", "fenmark"));
            var agent = Marshal(fake);
            agent.Memory = new RollingHistory();

            await Fixtures.NotCancelled(agent.DecideAsync("Your orders.", Silent()));
            await Fixtures.NotCancelled(agent.DecideAsync("Your orders.", Silent()));

            Assert.That(fake.LastRequest.History.Last(), Is.EqualTo("{\"action\":\"march\",\"target\":\"fenmark\"}"));
        }

        // ---- A stopped decision ----------------------------------------------------------------

        [Test]
        public async Task AStoppedDecisionAsksForNoLine()
        {
            var fake = new FakeProvider(Reply("march", "fenmark"));
            var agent = Marshal(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Judge"));
            bool asked = false;
            agent.RefusalStatement = (verdict, ctx) =>
            {
                asked = true;
                return "I will not.";
            };

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your orders.",
                new DecideOptions { AskForStatement = false, AskForRefusalLine = true }));

            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.Statement, Is.Empty);
            Assert.That(asked, Is.False, "RefusalStatement is not asked.");
            Assert.That(fake.Requests.Count, Is.EqualTo(1), "No refusal line is requested.");
        }

        [Test]
        public async Task AGuardReadsTheOptionToSkipTheStatement()
        {
            var fake = new FakeProvider(Reply("march", "fenmark"));
            var agent = Marshal(fake);
            var judge = new FakeGuard("StatementJudge", input => !input.Options.AskForStatement || input.Decision.Statement.Length > 0
                ? GuardVerdict.Pass
                : GuardVerdict.RewriteToNone("the statement is empty."));
            agent.Guards.Add(judge);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your orders.", Silent()));

            Assert.That(result.Verdict, Is.Null);
            Assert.That(judge.Inputs.Single().Options.AskForStatement, Is.False);
            Assert.That(judge.Inputs.Single().Decision.Statement, Is.Empty);
        }

        // ---- Helpers ---------------------------------------------------------------------------

        // A marshal with statement rules, an example with a statement, and two provinces.
        private static Agent Marshal(FakeProvider fake) => Marshal(fake, out _);

        private static Agent Marshal(FakeProvider fake, out FakeHandler march)
        {
            var actions = new[]
            {
                new ActionDefinition
                {
                    Id = "march", Description = "Send an army to a province.", RequiresTarget = true,
                    ExampleStimulus = "Take the {0}.", PreferredExampleTargets = new[] { "fenmark" }, ExampleStatement = "Onward.",
                },
                new ActionDefinition { Id = "rest", Description = "Let the army rest." },
            };

            var identity = new AgentIdentity { Name = "Marshal", Role = "a marshal", StatementRules = Rules };
            var agent = new Agent(identity, actions, fake);
            march = new FakeHandler();
            agent.Bind("march", march);
            agent.Bind("rest", new FakeHandler());
            agent.Targets.Register("fenmark", new object(), "province");
            agent.Targets.Register("westmarch", new object(), "province");
            return agent;
        }

        private static void AssertSameRequest(DecisionRequest a, DecisionRequest b)
        {
            var serializer = new JsonSchemaSerializer();
            Assert.That(b.SystemPrompt, Is.EqualTo(a.SystemPrompt));
            Assert.That(b.FewShotBlock, Is.EqualTo(a.FewShotBlock));
            Assert.That(b.Observations, Is.EqualTo(a.Observations));
            Assert.That(b.Stimulus, Is.EqualTo(a.Stimulus));
            Assert.That(serializer.Serialize(b.Schema), Is.EqualTo(serializer.Serialize(a.Schema)));
        }

        private static DecisionFeedEntry Entry(DecisionResult result)
        {
            return new DecisionFeedEntry(null, result, DecisionStage.Decided, null, DateTime.UtcNow, 1);
        }
    }
}
