using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Providers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // Few-shot examples as chat turns (#151): FewShotBuilder.BuildTurns, and how Agent chooses
    // between the turns and the block. How Ollama orders them is in OllamaProviderTests.
    public sealed class ExampleTurnsTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- FewShotBuilder.BuildTurns ---------------------------------------------------------------

        [Test]
        public void TheVillageGuardsExamplesBecomeFivePairsInTheBlocksOrder()
        {
            var targets = Registry("tower", "training_dummy");
            var actions = new List<ActionDefinition>
            {
                Action("follow_player", "Accompany me on my rounds.", false),
                Action("move_to", "Proceed to the {0}.", true, "tower", "bridge"),
                Action("attack_target", "Engage the {0}.", true, "training_dummy"),
            };

            var turns = FewShotBuilder.BuildTurns(actions, targets, new PromptOptions { IdleExampleStimulus = "You seem well rested today." }, null);

            Assert.That(turns, Is.EqualTo(new[]
            {
                "Player: \"Accompany me on my rounds.\"",
                "{\"action\":\"follow_player\",\"target\":\"no_target\",\"statement\":\"\"}",
                "Player: \"Proceed to the Tower.\"",
                "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"\"}",
                "Player: \"Engage the TrainingDummy.\"",
                "{\"action\":\"attack_target\",\"target\":\"training_dummy\",\"statement\":\"\"}",
                "Player: \"You seem well rested today.\"",
                "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}",
                "Player: \"Proceed to the Atlantis.\"",
                "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}",
            }), "Action examples, the idle example, then the refusal, its note left out: it is not a value the schema allows.");
        }

        [Test]
        public void EveryExampleOfTheBlockIsATurnAndStatementsCarryOver()
        {
            var targets = Registry("bread", "tower");
            var quote = Countered(Action("quote_purchase", "How much for the {0}?", true, "bread"), "That {0} smells good.");
            quote.ExampleStatement = "The {0} is three gold.";
            quote.CounterExampleStatement = "Baked it this morning.";
            var offer = Action("offer_quest", "Got any work for me?", false);
            offer.ExampleStatement = "Rats in the cellar, if you're brave.";
            var actions = new List<ActionDefinition> { quote, offer, Action("move_to", "Proceed to the {0}.", true, "tower") };
            var options = new PromptOptions
            {
                StimulusLabel = "Customer",
                IdleExampleStimulus = "Nothing has changed.",
                IdleExampleStatement = "Quiet as ever.",
            };

            var turns = FewShotBuilder.BuildTurns(actions, targets, options, null);
            string block = FewShotBuilder.Build(actions, targets, options);

            Assert.That(turns, Is.EqualTo(new[]
            {
                "Customer: \"How much for the Bread?\"",
                "{\"action\":\"quote_purchase\",\"target\":\"bread\",\"statement\":\"The Bread is three gold.\"}",
                "Customer: \"That Bread smells good.\"",
                "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"Baked it this morning.\"}",
                "Customer: \"Got any work for me?\"",
                "{\"action\":\"offer_quest\",\"target\":\"no_target\",\"statement\":\"Rats in the cellar, if you're brave.\"}",
                "Customer: \"Proceed to the Tower.\"",
                "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"\"}",
                "Customer: \"Nothing has changed.\"",
                "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"Quiet as ever.\"}",
                "Customer: \"How much for the Atlantis?\"",
                "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}",
            }), "Counter-examples, the idle example and the refusal are all there; a written statement carries over, an unwritten one is empty.");
            Assert.That(turns.Count / 2, Is.EqualTo(block.Split('\n').Length - 1), "One pair for each of the block's lines, header aside.");
        }

        [Test]
        public void StatementsOffAndCounterExamplesOffAreReadAsTheBlockReadsThem()
        {
            var targets = Registry("bread");
            var quote = Countered(Action("quote_purchase", "How much for the {0}?", true, "bread"), "That {0} smells good.");
            quote.ExampleStatement = "Three gold.";

            var turns = FewShotBuilder.BuildTurns(new List<ActionDefinition> { quote }, targets,
                new PromptOptions { ExampleStatements = false, CounterExamples = false }, null);

            Assert.That(turns, Is.EqualTo(new[]
            {
                "Player: \"How much for the Bread?\"",
                "{\"action\":\"quote_purchase\",\"target\":\"bread\",\"statement\":\"\"}",
                "Player: \"How much for the Atlantis?\"",
                "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}",
            }));
        }

        [Test]
        public void EveryAnswerIsValidJsonInFieldOrderEvenWithQuotesInIt()
        {
            var targets = Registry("gate");
            var shout = Action("shout_warning", "Shout \"halt\" at the {0}.", true, "gate");
            shout.ExampleStatement = "\"Halt!\" I cried, at the C:\\gate.";

            var turns = FewShotBuilder.BuildTurns(new List<ActionDefinition> { shout }, targets,
                new PromptOptions { IdleExampleStimulus = "The sign says \"closed\".", IdleExampleStatement = "So it \"is\"." }, null);

            Assert.That(turns.Count % 2, Is.Zero);
            for (int i = 1; i < turns.Count; i += 2)
            {
                var answer = JObject.Parse(turns[i]);
                Assert.That(answer.Properties().Select(p => p.Name), Is.EqualTo(new[] { "action", "target", "statement" }), turns[i]);
            }

            Assert.That((string)JObject.Parse(turns[1])["statement"], Is.EqualTo("\"Halt!\" I cried, at the C:\\gate."));
            Assert.That(turns[0], Is.EqualTo("Player: \"Shout \"halt\" at the Gate.\""),
                "The user turn is labelled as the request's own stimulus is, which quotes it without escaping.");
        }

        [Test]
        public void TheAnswersFollowTheSchemasFields()
        {
            var targets = Registry("tower");
            var actions = new List<ActionDefinition> { Action("move_to", "Proceed to the {0}.", true, "tower") };
            var noStatement = new DecisionSchema(new[]
            {
                new SchemaField { Name = "action", AllowedValues = new[] { "move_to", "none" }, Required = true },
                new SchemaField { Name = "target", AllowedValues = new[] { "no_target", "tower" }, Required = true },
            });

            var turns = FewShotBuilder.BuildTurns(actions, targets, null, noStatement);

            Assert.That(turns[1], Is.EqualTo("{\"action\":\"move_to\",\"target\":\"tower\"}"),
                "Written as memory writes a decision: only the fields the schema asks for.");
        }

        // ---- Agent ---------------------------------------------------------------------------------

        [Test]
        public async Task OffTheRequestIsWhatItWasAndCarriesNoTurns()
        {
            var provider = Sending(new FakeProvider(GoToTheTower));
            var agent = Fixtures.VillageGuard(provider);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            var request = provider.LastRequest;
            Assert.That(request.FewShotBlock, Does.StartWith("Examples of correct decisions:"));
            Assert.That(request.ExampleTurns, Is.Empty);
            Assert.That(result.Telemetry.ExamplesSentAsTurns, Is.False);
            Assert.That(new DecideOptions().ExamplesAsTurns, Is.False, "Off by default.");
            Assert.That(new PromptOptions().ExamplesAsTurns, Is.False);
        }

        [Test]
        public async Task OnTheExamplesMoveOutOfTheSystemSideIntoTurns()
        {
            var provider = Sending(new FakeProvider(GoToTheTower));
            var agent = Fixtures.VillageGuard(provider);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { ExamplesAsTurns = true }));

            var request = provider.LastRequest;
            Assert.That(request.FewShotBlock, Is.Null, "One form only: the system side has no example block.");
            Assert.That(request.ExampleTurns, Has.Count.EqualTo(10));
            Assert.That(request.ExampleTurns[0], Is.EqualTo("Player: \"Accompany me on my rounds.\""));
            Assert.That(request.ExampleTurns[9], Is.EqualTo("{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}"));
            Assert.That(request.History, Is.Empty, "Examples are not memory.");
            Assert.That(result.Telemetry.ExamplesSentAsTurns, Is.True);
            Assert.That(result.Telemetry.HistoryTurnsSent, Is.Zero);
        }

        [Test]
        public async Task AProviderThatDoesNotSendTurnsGetsTheBlock()
        {
            var provider = new FakeProvider(GoToTheTower);   // SendsExampleTurns not set
            var agent = Fixtures.VillageGuard(provider);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { ExamplesAsTurns = true }));

            Assert.That(provider.LastRequest.FewShotBlock, Does.StartWith("Examples of correct decisions:"),
                "A provider written before the field existed never loses its examples.");
            Assert.That(provider.LastRequest.ExampleTurns, Is.Empty);
            Assert.That(result.Telemetry.ExamplesSentAsTurns, Is.False, "Telemetry records what went, not what was asked.");
        }

        [Test]
        public async Task MemoryIsNeverMixedWithTheExamples()
        {
            var provider = Sending(new FakeProvider(GoToTheTower));
            var agent = Fixtures.VillageGuard(provider);
            agent.Memory = new RollingHistory();
            var options = new DecideOptions { ExamplesAsTurns = true };

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", options));
            await Fixtures.NotCancelled(agent.DecideAsync("Now the dummy.", options));

            var request = provider.LastRequest;
            Assert.That(request.History, Is.EqualTo(new[] { "Player: \"Go to the tower.\"", GoToTheTower }),
                "Only the agent's own exchange is remembered, never an example.");
            Assert.That(request.ExampleTurns, Has.Count.EqualTo(10));
        }

        [Test]
        public void ThePreviewShowsTheTurnsItWouldSend()
        {
            var agent = Fixtures.VillageGuard(Sending(new FakeProvider(GoToTheTower)));

            var on = agent.Preview("Hello.", new DecideOptions { ExamplesAsTurns = true });
            var off = agent.Preview("Hello.");

            Assert.That(on.Request.ExampleTurns, Has.Count.EqualTo(10));
            Assert.That(on.Request.FewShotBlock, Is.Null);
            Assert.That(off.Request.ExampleTurns, Is.Empty);
            Assert.That(off.Request.FewShotBlock, Is.Not.Null);
        }

        // ---- Helpers -------------------------------------------------------------------------------

        private static FakeProvider Sending(FakeProvider provider)
        {
            provider.Capabilities = new ProviderCapabilities
            {
                SupportsConstrainedDecoding = true,
                Dialect = SchemaDialect.JsonSchema,
                ReportsTokenProbabilities = true,
                SendsExampleTurns = true,
            };
            return provider;
        }

        private static TargetRegistry Registry(params string[] ids)
        {
            var registry = new TargetRegistry();
            foreach (string id in ids)
            {
                registry.Register(id, new object());
            }

            return registry;
        }

        private static ActionDefinition Action(string id, string stimulus, bool requiresTarget, params string[] preferredTargets)
            => new ActionDefinition
            {
                Id = id,
                ExampleStimulus = stimulus,
                RequiresTarget = requiresTarget,
                PreferredExampleTargets = preferredTargets,
            };

        private static ActionDefinition Countered(ActionDefinition action, string counterExample)
        {
            action.CounterExampleStimulus = counterExample;
            return action;
        }
    }
}
