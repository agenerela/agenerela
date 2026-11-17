using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // The two things DR-023 owed after its first step (#266 item 2). OrderLost: a reply whose fields
    // came in another order than the schema's is flagged in telemetry and the decision log, and
    // still read. SupportsPairedSchema: an agent with a declared parameter on a provider that cannot
    // take a paired schema is refused at setup, never at its first decision. Every await of
    // DecideAsync goes through Fixtures.NotCancelled.
    public sealed class ParameterProviderTests
    {
        private static ActionParameter Strength() => new ActionParameter { Name = "strength", Values = new[] { "minor", "major" } };

        private static ActionDefinition Blight(bool withParameter = true) => new ActionDefinition
        {
            Id = "blight", Description = "A country falls sick.", RequiresTarget = true,
            Parameters = withParameter ? new[] { Strength() } : null,
        };

        private static Agent Storyteller(ILLMProvider provider, bool withParameter = true)
        {
            var agent = new Agent(new AgentIdentity { Name = "The Teller" }, new[] { Blight(withParameter) }, provider);
            agent.Bind("blight", new FakeHandler());
            agent.Targets.Register("orsk", new object());
            return agent;
        }

        private static FakeProvider Unpaired(string reply = "") => new FakeProvider(reply)
        {
            Capabilities = new ProviderCapabilities { SupportsConstrainedDecoding = true, SupportsPairedSchema = false },
        };

        private static string Json(DecisionLogLine line) => Newtonsoft.Json.JsonConvert.SerializeObject(line, DecisionLogFormat.Settings);

        // ---- OrderLost --------------------------------------------------------------------------

        [TestCase("{\"action\":\"blight\",\"target\":\"orsk\",\"strength\":\"major\",\"statement\":\"Sour wells.\"}", false, TestName = "OrderHeld")]
        [TestCase("{\"action\":\"blight\",\"statement\":\"Sour wells.\",\"strength\":\"major\",\"target\":\"orsk\"}", true, TestName = "OrderLostWhenTheKeysAreSorted")]
        [TestCase("{\"action\":\"blight\",\"target\":\"orsk\",\"statement\":\"Sour wells.\",\"strength\":\"major\"}", true, TestName = "OrderLostWhenAParameterFollowsTheStatement")]
        [TestCase("{\"target\":\"orsk\",\"action\":\"blight\",\"strength\":\"major\",\"statement\":\"Sour wells.\"}", true, TestName = "OrderLostWhenTheTargetComesFirst")]
        [TestCase("{\"action\":\"blight\",\"note\":\"x\",\"target\":\"orsk\",\"strength\":\"major\",\"statement\":\"\"}", false, TestName = "OrderHeldAroundAFieldNobodyAskedFor")]
        public async Task AReplyOutOfTheSchemasOrderIsFlaggedAndStillRead(string reply, bool lost)
        {
            var fake = new FakeProvider(reply);
            var agent = Storyteller(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(result.Verdict, Is.Null, "Flagged, never refused: the values are legal whatever their order.");
            Assert.That(result.Decision.ActionId, Is.EqualTo("blight"));
            Assert.That(result.Decision.Arguments["strength"], Is.EqualTo("major"));
            Assert.That(result.Telemetry.OrderLost, Is.EqualTo(lost));

            var line = DecisionLogLine.From(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1));
            Assert.That(line.OrderLost, Is.EqualTo(lost ? true : (bool?)null));
            Assert.That(Json(line).Contains("\"orderLost\":true"), Is.EqualTo(lost), "Left out of the line when the order held.");
        }

        [Test]
        public async Task AnAgentWithNoParameterIsFlaggedForStatementBeforeTarget()
        {
            // The 5 November case: a native-path model wrote action, statement, target.
            var fake = new FakeProvider("{\"action\":\"blight\",\"statement\":\"Sour wells.\",\"target\":\"orsk\"}");
            var result = await Fixtures.NotCancelled(Storyteller(fake, withParameter: false).DecideAsync("Tell this turn's event."));

            Assert.That(result.Decision.TargetId, Is.EqualTo("orsk"));
            Assert.That(result.Telemetry.OrderLost, Is.True);

            fake.Reply = "{\"action\":\"blight\",\"target\":\"orsk\",\"statement\":\"Sour wells.\"}";
            var held = await Fixtures.NotCancelled(Storyteller(fake, withParameter: false).DecideAsync("Tell this turn's event."));
            Assert.That(held.Telemetry.OrderLost, Is.False);
        }

        [Test]
        public async Task AnUnreadableReplyIsNotFlagged()
        {
            var fake = new FakeProvider("{\"statement\":\"Sour wells.\",\"target\":\"orsk\"}");

            LogAssert.ignoreFailingMessages = true;
            var result = await Fixtures.NotCancelled(Storyteller(fake).DecideAsync("Tell this turn's event."));
            LogAssert.ignoreFailingMessages = false;

            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.UnreadableReply));
            Assert.That(result.Telemetry.OrderLost, Is.False);

            // The parser itself says so, not only the agent that never asks it.
            Assert.That(DecisionParser.TryParse(fake.Reply, fake.LastRequest.Schema, out _, out _, out bool lost), Is.False);
            Assert.That(lost, Is.False);
        }

        // ---- SupportsPairedSchema ---------------------------------------------------------------

        [Test]
        public void TrueByDefault()
        {
            Assert.That(new ProviderCapabilities().SupportsPairedSchema, Is.True);
            Assert.That(new OllamaProvider(new OllamaSettings()).Capabilities.SupportsPairedSchema, Is.True);
        }

        [Test]
        public void AnAgentWithADeclaredParameterIsRefusedAtConstruction()
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                new Agent(new AgentIdentity { Name = "The Teller" }, new[] { Blight() }, Unpaired()));

            Assert.That(error.Message, Is.EqualTo(
                "Agent 'The Teller': the action 'blight' declares a parameter, which is sent only in a paired schema, " +
                "and the provider 'fake' says it cannot take one (ProviderCapabilities.SupportsPairedSchema). " +
                "Give the agent a provider that can, or remove the parameter."));
        }

        [Test]
        public void AnAgentBuiltFromAProfileIsRefusedAtConstruction()
        {
            var profile = ScriptableObject.CreateInstance<AgentProfile>();
            var asset = ScriptableObject.CreateInstance<ActionDefinitionAsset>();
            try
            {
                profile.Identity = new AgentIdentity { Name = "The Teller" };
                asset.Action = Blight();
                profile.Actions = new List<ActionDefinitionAsset> { asset };

                Assert.That(() => new Agent(profile, Unpaired()),
                    Throws.InvalidOperationException.With.Message.Contains("declares a parameter, which is sent only in a paired schema"));
                Assert.That(() => new Agent(profile, new FakeProvider()), Throws.Nothing);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void RegisteringAParameterActionLaterIsRefusedAndLeavesTheRegistryAsItWas()
        {
            var agent = new Agent(new AgentIdentity { Name = "The Teller" }, new ActionDefinition[0], Unpaired());

            Assert.That(() => agent.Actions.Register(Blight(), new FakeHandler()),
                Throws.InvalidOperationException.With.Message.Contains("the provider 'fake' says it cannot take one"));
            Assert.That(agent.Actions.Definitions, Is.Empty);
            Assert.That(() => agent.Actions.Register(Blight(withParameter: false), new FakeHandler()), Throws.Nothing,
                "An action with no parameter needs no paired schema.");
        }

        [Test]
        public void SettingAProviderThatCannotPairIsRefusedAndTheOldOneStays()
        {
            var able = new FakeProvider();
            var agent = Storyteller(able);

            Assert.That(() => agent.Provider = Unpaired(),
                Throws.InvalidOperationException.With.Message.Contains("declares a parameter"));
            Assert.That(agent.Provider, Is.SameAs(able));

            var plain = Storyteller(able, withParameter: false);
            var unpaired = Unpaired();
            plain.Provider = unpaired;
            Assert.That(plain.Provider, Is.SameAs(unpaired), "An agent with no parameter may use it.");
        }

        [Test]
        public async Task ACallThatAsksForPairingOnSuchAProviderSendsNothing()
        {
            var unpaired = Unpaired("{\"action\":\"blight\",\"target\":\"orsk\",\"statement\":\"\"}");
            var agent = Storyteller(unpaired, withParameter: false);

            LogAssert.ignoreFailingMessages = true;
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event.", new DecideOptions { PairTargetsWithActions = true }));
            LogAssert.ignoreFailingMessages = false;

            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.RequestPreparation));
            Assert.That(result.Telemetry.ErrorMessage, Does.Contain("says it cannot take a paired schema"));
            Assert.That(unpaired.Requests, Is.Empty);

            // Without the option the same agent decides as it always did.
            var plain = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));
            Assert.That(plain.Decision.ActionId, Is.EqualTo("blight"));
            Assert.That(unpaired.Requests.Single().Schema.Pairs, Is.Null);
        }
    }
}
