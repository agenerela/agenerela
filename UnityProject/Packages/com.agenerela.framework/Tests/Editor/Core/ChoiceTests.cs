using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // A choice among options given with the call (DR-022, #266 item 2, DecideOptions.Choices): the
    // call offers its choices instead of the agent's actions, with no target field; the prompt lists
    // them; an unavailable one is left out and recorded; Execute runs nothing and settles memory; the
    // legality guard holds the reply to what was offered; and a bad list throws at the call. Every
    // await of DecideAsync goes through Fixtures.NotCancelled.
    public sealed class ChoiceTests
    {
        private static string Reply(string action) =>
            $"{{\"action\":\"{action}\",\"statement\":\"We will pay them.\"}}";

        private static List<Choice> Dilemma() => new List<Choice>
        {
            new Choice("hire_them", "Pay 30 gold, and 14 soldiers join our army"),
            new Choice("send_them_away", "They leave, and nothing changes"),
            new Choice("seize_their_arms", "Take their weapons by force", Availability.No("we have no army at hand")),
        };

        private static DecideOptions Asking() => new DecideOptions { Choices = Dilemma() };

        // ---- The request ------------------------------------------------------------------------

        [Test]
        public void NullByDefault()
        {
            Assert.That(new DecideOptions().Choices, Is.Null);
        }

        [Test]
        public async Task TheSchemaOffersTheAvailableChoicesThenNoneAndNoTarget()
        {
            var fake = new FakeProvider(Reply("hire_them"));
            await Fixtures.NotCancelled(Ruler(fake, out _).DecideAsync("Mercenaries are at the gate.", Asking()));

            var schema = fake.LastRequest.Schema;
            Assert.That(schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action", "statement" }), "A choice takes no target.");
            Assert.That(schema.Fields[0].AllowedValues, Is.EqualTo(new[] { "hire_them", "send_them_away", ActionRegistry.None }),
                "The agent's own actions are not offered, and the unavailable choice is left out.");
            Assert.That(schema.Pairs, Is.Null);
        }

        [Test]
        public async Task ThePromptListsTheChoicesWithTheirWordsAndNothingOfTheAgentsActions()
        {
            var fake = new FakeProvider(Reply("hire_them"));
            await Fixtures.NotCancelled(Ruler(fake, out _).DecideAsync("Mercenaries are at the gate.", Asking()));

            string prompt = fake.LastRequest.SystemPrompt;
            Assert.That(prompt, Does.Contain(
                "Allowed actions:\n- hire_them: Pay 30 gold, and 14 soldiers join our army.\n- send_them_away: They leave, and nothing changes.\n- none: Take no action."),
                "Listed although ListActionsAndTargets is off.");
            Assert.That(prompt, Does.Not.Contain("seize_their_arms"));
            Assert.That(prompt, Does.Not.Contain("attack"));
            Assert.That(prompt, Does.Not.Contain("Targets:"));
            Assert.That(fake.LastRequest.FewShotBlock ?? "", Does.Not.Contain("attack"), "No example of the agent's own actions.");
        }

        [Test]
        public async Task AnOrdinaryCallIsWhatItWas()
        {
            var plain = new FakeProvider("{\"action\":\"attack\",\"target\":\"fenmark\",\"statement\":\"\"}");
            var agent = Ruler(plain, out _);

            await Fixtures.NotCancelled(agent.DecideAsync("Your move."));
            var first = plain.LastRequest;
            await Fixtures.NotCancelled(agent.DecideAsync("Mercenaries are at the gate.", Asking()));
            await Fixtures.NotCancelled(agent.DecideAsync("Your move."));

            Assert.That(plain.LastRequest.SystemPrompt, Is.EqualTo(first.SystemPrompt), "A choice call leaves nothing behind.");
            Assert.That(new JsonSchemaSerializer().Serialize(plain.LastRequest.Schema), Is.EqualTo(new JsonSchemaSerializer().Serialize(first.Schema)));
            Assert.That(first.Schema.Fields[0].AllowedValues, Does.Contain("attack"));
        }

        // ---- The decision and its record ---------------------------------------------------------

        [Test]
        public async Task TheChosenIdIsTheDecisionAndTelemetryAndTheLogRecordTheChoices()
        {
            var fake = new FakeProvider(Reply("hire_them"));
            var agent = Ruler(fake, out _);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Mercenaries are at the gate.", Asking()));

            Assert.That(result.Verdict, Is.Null);
            Assert.That(result.Decision.ActionId, Is.EqualTo("hire_them"));
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.Decision.Statement, Is.EqualTo("We will pay them."));
            Assert.That(result.Telemetry.ChoicesOffered, Is.EqualTo(new[] { "hire_them", "send_them_away" }));
            Assert.That(result.Telemetry.ChoicesUnavailable.Single().Choice, Is.EqualTo("seize_their_arms"));
            Assert.That(result.Telemetry.ChoicesUnavailable.Single().Reason, Is.EqualTo("we have no army at hand"));

            var line = DecisionLogLine.From(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1));
            Assert.That(line.ChoicesOffered, Is.EqualTo(new[] { "hire_them", "send_them_away" }));
            Assert.That(line.ChoicesUnavailable.Single().ToString(), Is.EqualTo("seize_their_arms: we have no army at hand"));
        }

        [Test]
        public async Task AnOrdinaryCallsLogLineHasNoChoices()
        {
            var fake = new FakeProvider("{\"action\":\"attack\",\"target\":\"fenmark\",\"statement\":\"\"}");
            var agent = Ruler(fake, out _);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your move."));

            Assert.That(result.Telemetry.ChoicesOffered, Is.Empty);
            var line = DecisionLogLine.From(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1));
            Assert.That(line.ChoicesOffered, Is.Null);
            Assert.That(line.ChoicesUnavailable, Is.Null);
        }

        [Test]
        public async Task ExecuteRunsNothingAndSettlesMemory()
        {
            var fake = new FakeProvider(Reply("hire_them"));
            var agent = Ruler(fake, out var attack);
            agent.Memory = new RollingHistory();
            agent.MemoryCommit = MemoryCommit.OnExecute;

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Mercenaries are at the gate.", Asking()));

            Assert.That(agent.Execute(result, out var outcome), Is.True);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Done));
            Assert.That(result.Telemetry.GuardsFired, Is.Empty, "No re-check refused it.");
            Assert.That(attack.Executions, Is.Empty);

            fake.Reply = "{\"action\":\"attack\",\"target\":\"fenmark\",\"statement\":\"\"}";
            await Fixtures.NotCancelled(agent.DecideAsync("Your move."));
            Assert.That(fake.LastRequest.History.Last(), Does.Contain("hire_them"), "Remembered once carried out.");
        }

        [Test]
        public async Task AnUnavailableChoiceInTheReplyIsRejected()
        {
            var fake = new FakeProvider(Reply("seize_their_arms"));
            LogAssert.Expect(LogType.Warning, new Regex("not offered"));

            var result = await Fixtures.NotCancelled(Ruler(fake, out _).DecideAsync("Mercenaries are at the gate.", Asking()));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("SchemaLegalityGuard"));
        }

        [Test]
        public async Task TheAgentsOwnActionInTheReplyIsRejected()
        {
            var fake = new FakeProvider("{\"action\":\"attack\",\"statement\":\"\"}");
            var agent = Ruler(fake, out var attack);
            LogAssert.Expect(LogType.Warning, new Regex("not registered"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Mercenaries are at the gate.", Asking()));

            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(agent.Execute(result), Is.False);
            Assert.That(attack.Executions, Is.Empty);
        }

        [Test]
        public async Task ADevelopersGuardSeesTheChoices()
        {
            var fake = new FakeProvider(Reply("send_them_away"));
            var agent = Ruler(fake, out _);
            var guard = new FakeGuard("Watcher");
            agent.Guards.Add(guard);

            await Fixtures.NotCancelled(agent.DecideAsync("Mercenaries are at the gate.", Asking()));

            var input = guard.Inputs.Single();
            Assert.That(input.Options.Choices.Select(c => c.Id), Does.Contain("send_them_away"));
            Assert.That(input.Decision.ActionId, Is.EqualTo("send_them_away"));
        }

        [Test]
        public async Task TheListIsCopiedWhenTheCallStarts()
        {
            var fake = new FakeProvider(Reply("hire_them")) { Hold = true };
            var agent = Ruler(fake, out _);
            var options = Asking();

            var call = agent.DecideAsync("Mercenaries are at the gate.", options);
            options.Choices.Clear();
            options.Choices.Add(new Choice("flee", "Run"));
            fake.Release();
            var result = await Fixtures.NotCancelled(call);

            Assert.That(result.Telemetry.ChoicesOffered, Is.EqualTo(new[] { "hire_them", "send_them_away" }));
        }

        // ---- Mistakes in the call -----------------------------------------------------------------

        [Test]
        public void ABadListThrowsAtTheCall()
        {
            var agent = Ruler(new FakeProvider(Reply("hire_them")), out _);

            void Throws(string because, params Choice[] choices)
            {
                var e = Assert.Throws<ArgumentException>(() => agent.DecideAsync("Choose.", new DecideOptions { Choices = choices.ToList() }), because);
                Assert.That(e.Message, Does.Contain("DecideOptions.Choices"), because);
            }

            Throws("empty");
            Throws("null", new Choice[] { null });
            Throws("uppercase id", new Choice("Hire", "Pay them"));
            Throws("whitespace id", new Choice("hire them", "Pay them"));
            Throws("none", new Choice(ActionRegistry.None, "Do nothing"));
            Throws("twice", new Choice("hire", "Pay them"), new Choice("hire", "Pay them again"));
            Throws("no description", new Choice("hire", " "));
            Assert.Throws<ArgumentNullException>(() => new Choice(null, "Pay them"));
            Assert.Throws<ArgumentNullException>(() => new Choice("hire", null));
        }

        // ---- Helpers ----------------------------------------------------------------------------

        // A ruler with an attack action and a province, none of which a choice call offers.
        private static Agent Ruler(FakeProvider fake, out FakeHandler attack)
        {
            var actions = new[]
            {
                new ActionDefinition
                {
                    Id = "attack", Description = "Attack a province.", RequiresTarget = true,
                    ExampleStimulus = "Take the {0}.", PreferredExampleTargets = new[] { "fenmark" },
                },
            };

            var agent = new Agent(new AgentIdentity { Name = "Kesh", Role = "the court of Kesh" }, actions, fake);
            attack = new FakeHandler();
            agent.Bind("attack", attack);
            agent.Targets.Register("fenmark", new object(), "province");
            return agent;
        }
    }
}
