using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests
{
    // Who asked and what (#190, G6): DecisionRequest's three internal fields, AgentName,
    // StimulusText and StimulusLabel. PromptBuilder sets them for the scripted provider to read.
    // They must never be sent to a model nor written out by a serializer: a provider must not
    // branch on who is asking (DR-008), and what reaches the model is measured.
    public sealed class RequestOriginTests
    {
        private static readonly string[] OriginFields = { "AgentName", "StimulusText", "StimulusLabel" };

        private const string Reply = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        [TearDown]
        public void TearDown()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public async Task PromptBuilderRecordsWhoAskedAndWhat()
        {
            var fake = new FakeProvider(Reply);
            var agent = Fixtures.VillageGuard(fake);

            await Fixtures.NotCancelled(agent.DecideAsync("Walk with me.", new DecideOptions { StimulusLabel = "Traveller" }));

            var sent = fake.LastRequest;
            Assert.That(sent.AgentName, Is.EqualTo("Village Guard"));
            Assert.That(sent.StimulusText, Is.EqualTo("Walk with me."), "As passed, without its label.");
            Assert.That(sent.StimulusLabel, Is.EqualTo("Traveller"));
        }

        [Test]
        public async Task ARefusalLineCarriesItsDecisionsOrigin()
        {
            var fake = new FakeProvider(Reply);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Stopper"));

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { AskForRefusalLine = true }));

            Assert.That(fake.Requests, Has.Count.EqualTo(2), "The decision, then its refusal line.");
            var decided = fake.Requests[0];
            var line = fake.Requests[1];
            Assert.That(line.Stimulus, Is.Not.EqualTo(decided.Stimulus), "The refusal line asks in its own words.");
            Assert.That(line.AgentName, Is.EqualTo(decided.AgentName).And.EqualTo("Village Guard"));
            Assert.That(line.StimulusText, Is.EqualTo(decided.StimulusText).And.EqualTo("Go to the tower."));
            Assert.That(line.StimulusLabel, Is.EqualTo(decided.StimulusLabel).And.EqualTo("Player"));
        }

        [Test]
        public async Task NoneOfThemReachesOllamasRequestBody()
        {
            // The values are in the prompt already, as the system prompt and the stimulus turn, so
            // the check is that the body does not change with them: the same request with the three
            // fields cleared must send exactly the same bytes.
            var handler = new CapturingHandler();
            var provider = new OllamaProvider(new OllamaSettings(), handler);
            var agent = Fixtures.VillageGuard(provider);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            var request = result.Request;
            Assert.That(request.AgentName, Is.Not.Null, "The fields must be set for this to test anything.");
            Assert.That(request.StimulusText, Is.Not.Null);
            Assert.That(request.StimulusLabel, Is.Not.Null);

            request.AgentName = null;
            request.StimulusText = null;
            request.StimulusLabel = null;
            await Fixtures.NotCancelled(provider.RequestAsync(request, CancellationToken.None));

            Assert.That(handler.Bodies, Has.Count.EqualTo(2));
            Assert.That(handler.Bodies[0], Is.EqualTo(handler.Bodies[1]), "The body must not depend on who asked.");
            var keys = KeysOf(JToken.Parse(handler.Bodies[0])).ToList();
            Assert.That(keys, Does.Contain("messages").And.Contain("format"), "The walk must find the body's keys, or this checks nothing.");
            Assert.That(keys.Where(IsOriginField), Is.Empty);
        }

        [Test]
        public void NewtonsoftNeverWritesThem()
        {
            var written = JToken.Parse(JsonConvert.SerializeObject(Request()));

            Assert.That(KeysOf(written), Does.Contain("SystemPrompt"), "The public fields must still be written, or this checks nothing.");
            Assert.That(KeysOf(written).Where(IsOriginField), Is.Empty);
        }

        [Test]
        public void JsonUtilityNeverWritesThem()
        {
            string written = JsonUtility.ToJson(Request());

            foreach (var field in OriginFields)
            {
                Assert.That(written, Does.Not.Contain(field));
            }
        }

        private static DecisionRequest Request()
        {
            return new DecisionRequest
            {
                SystemPrompt = "You are the guard.",
                Stimulus = "Player: \"Go to the tower.\"",
                AgentName = "Village Guard",
                StimulusText = "Go to the tower.",
                StimulusLabel = "Player",
            };
        }

        private static bool IsOriginField(string key)
        {
            return OriginFields.Any(f => string.Equals(f, key, StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(f, key.Replace("_", ""), StringComparison.OrdinalIgnoreCase));
        }

        // Every property name in the JSON, at any depth.
        private static IEnumerable<string> KeysOf(JToken token)
        {
            return ((JContainer)token).Descendants().OfType<JProperty>().Select(p => p.Name);
        }

        // Stands in for the network, as OllamaProviderTests' handler does: keeps each body and
        // answers with an Ollama chat reply around Reply.
        private sealed class CapturingHandler : HttpMessageHandler
        {
            public readonly List<string> Bodies = new List<string>();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Bodies.Add(Encoding.UTF8.GetString(request.Content.ReadAsByteArrayAsync().Result));
                string envelope = "{\"model\":\"qwen3.5:2b\",\"created_at\":\"2026-11-05T00:00:00Z\","
                                  + "\"message\":{\"role\":\"assistant\",\"content\":" + JsonConvert.ToString(Reply) + "},"
                                  + "\"done\":true,\"done_reason\":\"stop\",\"prompt_eval_count\":1,\"eval_count\":1,\"logprobs\":[]}";
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope, new UTF8Encoding(false)) };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
                return Task.FromResult(response);
            }
        }
    }
}
