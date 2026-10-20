using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // OllamaProvider with no Ollama running (#88): a scripted HttpMessageHandler captures the body
    // it sends and answers in Ollama's shapes, as Ollama 0.34.2 gave them. The live round trip is
    // #94's PlayMode test. As in AgentTests, the Unity Test Framework counts a Task that ends
    // cancelled as passed, so every await that must not be cancelled goes through a helper that
    // fails on OperationCanceledException.
    public sealed class OllamaProviderTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        // #16's village guard request as Ollama receives it, with the values #88's request table
        // gives: the system side of #16's golden file, the stimulus as the last user message, #9's
        // schema in format, and greedy, thinking off, logprobs on.
        private const string GoldenBody = @"{
  ""model"": ""qwen3.5:2b"",
  ""messages"": [
    {
      ""role"": ""system"",
      ""content"": ""You are Village Guard, a guard at the town gate. Loyal, brief, a little suspicious of strangers.\nYour goals: keep the gate safe; help the player when asked.\nChoose exactly one action from the allowed list. Use no_target when none of the listed targets applies.\n\nExamples of correct decisions:\nPlayer: \""Accompany me on my rounds.\"" -> action: follow_player, target: no_target\nPlayer: \""Proceed to the Tower.\"" -> action: move_to, target: tower\nPlayer: \""Engage the TrainingDummy.\"" -> action: attack_target, target: training_dummy\nPlayer: \""You seem well rested today.\"" -> action: none, target: no_target\nPlayer: \""Proceed to the Atlantis.\"" -> action: none, target: no_target (not an available target)""
    },
    {
      ""role"": ""user"",
      ""content"": ""Player: \""Go to the tower.\""""
    }
  ],
  ""format"": {
    ""type"": ""object"",
    ""properties"": {
      ""action"": {
        ""type"": ""string"",
        ""description"": ""The one action to take now, from the allowed list."",
        ""enum"": [""follow_player"", ""move_to"", ""attack_target"", ""none""]
      },
      ""target"": {
        ""type"": ""string"",
        ""description"": ""What the action applies to. Use no_target when none of the listed targets applies."",
        ""enum"": [""no_target"", ""tower"", ""training_dummy""]
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
  ""stream"": false,
  ""think"": false,
  ""options"": {
    ""temperature"": 0.0,
    ""num_ctx"": 4096
  },
  ""logprobs"": true,
  ""top_logprobs"": 5
}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- The request ----------------------------------------------------------------------

        [Test]
        public async Task TheVillageGuardRequestIsTheGoldenBody()
        {
            var handler = ScriptedHandler.Replying(Envelope(GoToTheTower));
            var agent = Fixtures.VillageGuard(Provider(handler));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision, Is.Not.Null, result.Telemetry.ErrorMessage);
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            var sent = handler.Requests[0];
            Assert.That(sent.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(sent.Uri, Is.EqualTo(new Uri("http://127.0.0.1:11434/api/chat")));
            Assert.That(sent.ContentType, Is.EqualTo("application/json"));
            Assert.That(Compact(sent.Body), Is.EqualTo(Compact(GoldenBody)),
                "Every field, in order, with the values #88 tables. Whitespace aside, the body is exact.");
        }

        [Test]
        public void TheDefaultsAreTheMeasuredOnes()
        {
            var settings = new OllamaSettings();

            Assert.That(settings.Endpoint, Is.EqualTo("http://127.0.0.1:11434"),
                "Not localhost, which costs about 2 s a request on Windows (findings, 5 October).");
            Assert.That(settings.Model, Is.EqualTo("qwen3.5:2b"));
            Assert.That(settings.ContextSize, Is.EqualTo(4096));
            Assert.That(settings.Temperature, Is.EqualTo(0f), "Appendix A was greedy.");
            Assert.That(settings.TimeoutSeconds, Is.EqualTo(30f));
        }

        [Test]
        public async Task FormatIsTheSerializersOutputAsAnObject()
        {
            // A description that looks like a date: read back by a default Newtonsoft parser, it
            // would become a DateTime and be written out differently.
            var schema = new DecisionSchema(new[]
            {
                new SchemaField { Name = "action", Description = "2026-10-19T12:00:00Z", AllowedValues = new[] { "wave", "none" }, Required = true },
                new SchemaField { Name = "statement", Description = "Say \"hi\"\\ über.", Required = true },
            });
            var handler = ScriptedHandler.Replying(Envelope("{}"));

            await NotCancelled(Provider(handler).RequestAsync(Request(schema), CancellationToken.None));

            string serialized = new JsonSchemaSerializer().Serialize(schema);
            var format = Parse(handler.Requests[0].Body)["format"];
            Assert.That(format.Type, Is.EqualTo(JTokenType.Object), "An object, not a string holding one.");
            Assert.That(handler.Requests[0].Body, Does.Contain("\"format\":" + serialized),
                "Embedded exactly as #9 wrote it.");
            Assert.That(format.ToString(Formatting.None), Is.EqualTo(Compact(serialized)));
        }

        [Test]
        public async Task BlocksArriveByteForByte()
        {
            // Whitespace a provider might be tempted to tidy, quotes, a backslash, a tab, a CR and
            // characters outside ASCII, one of them outside the BMP.
            var request = Request();
            request.SystemPrompt = "  You are the guard.\t \n\n";
            request.FewShotBlock = "\r\nExamples:\n  - \"Go.\" -> move_to  ";
            request.Observations = "What you know right now:\n- the gate is \\open\\ é \U0001F409\n";
            request.Stimulus = "Player: \"  Go to the   tower. \"\n";
            var handler = ScriptedHandler.Replying(Envelope(GoToTheTower));

            await NotCancelled(Provider(handler).RequestAsync(request, CancellationToken.None));

            var messages = Messages(handler.Requests[0].Body);
            Assert.That(messages.Select(m => m.role), Is.EqualTo(new[] { "system", "user" }));
            Assert.That(messages[0].content, Is.EqualTo(
                request.SystemPrompt + "\n\n" + request.FewShotBlock + "\n\n" + request.Observations));
            Assert.That(messages[1].content, Is.EqualTo(request.Stimulus));
            Assert.That(handler.Requests[0].Bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "No byte-order mark.");
        }

        [Test]
        public async Task EmptyBlocksAreLeftOutAndNoSystemSideMeansNoSystemMessage()
        {
            var handler = ScriptedHandler.Replying(Envelope(GoToTheTower));
            var provider = Provider(handler);

            var someEmpty = Request();
            someEmpty.SystemPrompt = "Who.";
            someEmpty.FewShotBlock = "";
            someEmpty.Observations = "Known.";
            await NotCancelled(provider.RequestAsync(someEmpty, CancellationToken.None));

            var noneAtAll = Request();
            noneAtAll.SystemPrompt = null;
            noneAtAll.FewShotBlock = null;
            noneAtAll.Observations = "";
            await NotCancelled(provider.RequestAsync(noneAtAll, CancellationToken.None));

            Assert.That(Messages(handler.Requests[0].Body)[0].content, Is.EqualTo("Who.\n\nKnown."));
            Assert.That(Messages(handler.Requests[1].Body).Select(m => m.role), Is.EqualTo(new[] { "user" }));
        }

        [Test]
        public async Task HistoryGoesBetweenTheSystemSideAndTheStimulusAsUserThenAssistant()
        {
            // #90's proposal: each remembered exchange is the stimulus as it was sent, then the
            // decision as JSON.
            var request = Request();
            request.History = new List<string>
            {
                "Player: \"Follow me.\"",
                "{\"action\":\"follow_player\",\"target\":\"no_target\",\"statement\":\"Lead on.\"}",
                "Player: \"Stop.\"",
                "{\"action\":\"stop_following\",\"target\":\"no_target\",\"statement\":\"\"}",
            };
            var handler = ScriptedHandler.Replying(Envelope(GoToTheTower));

            await NotCancelled(Provider(handler).RequestAsync(request, CancellationToken.None));

            var messages = Messages(handler.Requests[0].Body);
            Assert.That(messages.Select(m => m.role),
                Is.EqualTo(new[] { "system", "user", "assistant", "user", "assistant", "user" }));
            Assert.That(messages.Skip(1).Take(4).Select(m => m.content), Is.EqualTo(request.History));
            Assert.That(messages.Last().content, Is.EqualTo(request.Stimulus));
        }

        [Test]
        public async Task SettingsAreCopiedWhenTheProviderIsBuilt()
        {
            var settings = new OllamaSettings { Endpoint = "http://10.0.0.5:8080/ollama/", Model = "qwen3.5:4b", ContextSize = 2048, Temperature = 0.5f };
            var handler = ScriptedHandler.Replying(Envelope(GoToTheTower));
            var provider = new OllamaProvider(settings, handler);
            settings.Model = "changed";
            settings.Endpoint = "http://changed";

            await NotCancelled(provider.RequestAsync(Request(), CancellationToken.None));

            var body = Parse(handler.Requests[0].Body);
            Assert.That(handler.Requests[0].Uri, Is.EqualTo(new Uri("http://10.0.0.5:8080/ollama/api/chat")));
            Assert.That((string)body["model"], Is.EqualTo("qwen3.5:4b"));
            Assert.That((int)body["options"]["num_ctx"], Is.EqualTo(2048));
            Assert.That((float)body["options"]["temperature"], Is.EqualTo(0.5f));
        }

        // ---- The reply ------------------------------------------------------------------------

        [Test]
        public async Task TheEnvelopeMapsOntoTheResult()
        {
            // Shaped like a real reply from qwen3.5:2b on Ollama 0.34.2, cut short.
            const string Content = "{\n  \"action\": \"greet\",\n  \"statement\": \"Привіт!\"\n}";
            var handler = ScriptedHandler.Replying(Envelope(Content, promptTokens: 26, completionTokens: 26, logprobs: @"[
                {""token"":""{"",""logprob"":-0.14383183419704437,""bytes"":[123],""top_logprobs"":[
                    {""token"":""{"",""logprob"":-0.14383183419704437,""bytes"":[123]},
                    {""token"":""```"",""logprob"":-2.7616336345672607,""bytes"":[96,96,96]}]},
                {""token"":""Пр"",""logprob"":-0.5,""bytes"":[208,159,209,128],""top_logprobs"":[]},
                {""token"":""}"",""logprob"":0,""bytes"":[125]}
            ]"));

            var result = await NotCancelled(Provider(handler).RequestAsync(Request(), CancellationToken.None));

            Assert.That(result.Text, Is.EqualTo(Content), "message.content, exactly as received.");
            Assert.That(result.PromptTokens, Is.EqualTo(26));
            Assert.That(result.CompletionTokens, Is.EqualTo(26));
            Assert.That(result.Model, Is.EqualTo("qwen3.5:2b"));
            Assert.That(result.LatencySeconds, Is.GreaterThanOrEqualTo(0f).And.LessThan(5f));

            var tokens = result.TokenProbabilities;
            Assert.That(tokens.Select(t => t.Text), Is.EqualTo(new[] { "{", "Пр", "}" }));
            Assert.That(tokens.Select(t => t.LogProbability), Is.EqualTo(new[] { (float)-0.14383183419704437, -0.5f, 0f }));
            Assert.That(tokens[0].Alternatives.Select(a => a.Text), Is.EqualTo(new[] { "{", "```" }));
            Assert.That(tokens[0].Alternatives.Select(a => a.LogProbability), Is.EqualTo(new[] { (float)-0.14383183419704437, (float)-2.7616336345672607 }));
            Assert.That(tokens[1].Alternatives, Is.Not.Null.And.Empty);
            Assert.That(tokens[2].Alternatives, Is.Not.Null.And.Empty, "No top_logprobs field reads as none, never null.");
        }

        [Test]
        public async Task ASplitCharacterIsMissingFromTheTokensButNotFromTheText()
        {
            // providers.md: a character split across two tokens is cut from both tokens' text, so
            // the joined tokens differ from message.content, which is still the text.
            const string Content = "{\"statement\":\"\U0001F409\"}";
            var handler = ScriptedHandler.Replying(Envelope(Content, logprobs: @"[
                {""token"":""{\"""",""logprob"":0,""top_logprobs"":[]},
                {""token"":""statement"",""logprob"":0,""top_logprobs"":[]},
                {""token"":""\"":\"""",""logprob"":0,""top_logprobs"":[]},
                {""token"":"""",""logprob"":-0.25,""bytes"":[],""top_logprobs"":[]},
                {""token"":"""",""logprob"":-0.5,""bytes"":[],""top_logprobs"":[]},
                {""token"":""\""}"",""logprob"":0,""top_logprobs"":[]}
            ]"));

            var result = await NotCancelled(Provider(handler).RequestAsync(Request(), CancellationToken.None));

            Assert.That(result.Text, Is.EqualTo(Content));
            Assert.That(result.TokenProbabilities, Has.Count.EqualTo(6), "One entry per token, the cut ones included.");
            Assert.That(result.TokenProbabilities[3].Text, Is.Empty);
            Assert.That(result.TokenProbabilities[4].LogProbability, Is.EqualTo(-0.5f));
            Assert.That(string.Concat(result.TokenProbabilities.Select(t => t.Text)), Is.EqualTo("{\"statement\":\"\"}"));
        }

        [Test]
        public async Task WhatOllamaLeavesOutReadsAsNotReported()
        {
            var handler = ScriptedHandler.Replying("{\"message\":{\"role\":\"assistant\",\"content\":\"\"},\"done\":true}");

            var result = await NotCancelled(Provider(handler, new OllamaSettings { Model = "qwen3.5:4b" })
                .RequestAsync(Request(), CancellationToken.None));

            Assert.That(result.Text, Is.Empty, "An empty reply is still a reply, for Agent to judge.");
            Assert.That(result.PromptTokens, Is.EqualTo(0));
            Assert.That(result.CompletionTokens, Is.EqualTo(0));
            Assert.That(result.TokenProbabilities, Is.Null, "Not reported, which the guard records as unable to run.");
            Assert.That(result.Model, Is.EqualTo("qwen3.5:4b"), "The model asked for, when the server does not say.");
        }

        [Test]
        public async Task TextThatLooksLikeADateStaysAsReceived()
        {
            const string Content = "  2026-10-19T12:00:00Z \n";
            var handler = ScriptedHandler.Replying(Envelope(Content));

            var result = await NotCancelled(Provider(handler).RequestAsync(Request(), CancellationToken.None));

            Assert.That(result.Text, Is.EqualTo(Content));
        }

        [Test]
        public async Task TheModelReachesTheTelemetry()
        {
            var handler = ScriptedHandler.Replying(Envelope(GoToTheTower, model: "qwen3.5:4b", promptTokens: 143, completionTokens: 21));
            var agent = Fixtures.VillageGuard(Provider(handler));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(result.Telemetry.Model, Is.EqualTo("qwen3.5:4b"), "The model the server says answered.");
            Assert.That(result.Telemetry.ProviderName, Is.EqualTo("ollama"));
            Assert.That(result.Telemetry.PromptTokens, Is.EqualTo(143));
            Assert.That(result.Telemetry.CompletionTokens, Is.EqualTo(21));
        }

        [Test]
        public async Task AProviderThatNamesNoModelLeavesTheTelemetrysEmpty()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(new DecisionTelemetry().Model, Is.Empty);
            Assert.That(result.Telemetry.Model, Is.Empty);
        }

        [Test]
        public async Task TheResultComesBackOnTheMainThread()
        {
            Assert.That(SynchronizationContext.Current, Is.Not.Null,
                "The test needs Unity's main-thread context to prove anything.");
            int main = Thread.CurrentThread.ManagedThreadId;
            var handler = ScriptedHandler.Replying(Envelope(GoToTheTower));
            handler.OnThreadPool = true;
            var agent = Fixtures.VillageGuard(Provider(handler));

            // Awaited directly, not through Fixtures.NotCancelled: awaiting a Task would resume
            // the test on the main thread whatever happened inside.
            DecisionResult result;
            try
            {
                result = await agent.DecideAsync("Go to the tower.");
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"The decision ended cancelled, though nothing cancelled it: {e.Message}");
                return;
            }

            int arrived = Thread.CurrentThread.ManagedThreadId;
            Assert.That(handler.AnsweredOnThread, Is.Not.EqualTo(main), "The reply must arrive off the main thread for this to test anything.");
            Assert.That(arrived, Is.EqualTo(main), "Agent and the game carry on where they called from.");
            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
        }

        [Test]
        public async Task TheProviderReadsTheReplyOnTheMainThread()
        {
            // The test above holds even with ConfigureAwait(false) in the provider: an Awaitable
            // brings the code awaiting it back to the main thread on its own (checked by adding
            // ConfigureAwait(false) and running both). This one watches the provider's own code
            // after its await: it reads the transport's exception message there, to name the
            // failure, and the exception records the thread that read it.
            int main = Thread.CurrentThread.ManagedThreadId;
            var refused = new ThreadRecordingException();
            var handler = ScriptedHandler.Throwing(refused);
            handler.OnThreadPool = true;

            var failure = await Fails(Provider(handler).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.Unreachable));
            Assert.That(handler.AnsweredOnThread, Is.Not.EqualTo(main), "The failure must arrive off the main thread for this to test anything.");
            Assert.That(refused.ReadOnThreads, Is.Not.Empty);
            Assert.That(refused.ReadOnThreads, Has.All.EqualTo(main), "No ConfigureAwait(false) on the path back to Agent.");
        }

        [Test]
        public void NameAndCapabilitiesAreProvidersMdsTable()
        {
            var provider = Provider(ScriptedHandler.Replying(Envelope(GoToTheTower)));
            var capabilities = provider.Capabilities;

            Assert.That(provider.Name, Is.EqualTo("ollama"));
            Assert.That(capabilities.SupportsConstrainedDecoding, Is.True);
            Assert.That(capabilities.NeedsExplicitPropertyOrdering, Is.False);
            Assert.That(capabilities.Dialect, Is.EqualTo(SchemaDialect.JsonSchema));
            Assert.That(capabilities.RateLimit, Is.Null);
            Assert.That(capabilities.RejectsEmptyEnumValues, Is.False);
            Assert.That(capabilities.ReportsTokenProbabilities, Is.True);
            Assert.That(provider.Capabilities, Is.SameAs(capabilities), "Constant for the provider's lifetime.");
        }

        // ---- Failures -------------------------------------------------------------------------

        [Test]
        public async Task NoServerIsUnreachableNamingTheEndpoint()
        {
            var handler = ScriptedHandler.Throwing(new HttpRequestException("An error occurred while sending the request",
                new WebException("Error: ConnectFailure (Connection refused)")));

            var failure = await Fails(Provider(handler).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.Unreachable));
            Assert.That(failure.ProviderName, Is.EqualTo("ollama"));
            Assert.That(failure.Message, Does.Contain("http://127.0.0.1:11434"));
            Assert.That(failure.Message, Does.Contain("Connection refused"));
            Assert.That(failure.InnerException, Is.InstanceOf<HttpRequestException>());
        }

        [Test]
        public async Task TheTransportsMessageIsCutAtItsFirstLineBreak()
        {
            // What Unity's Mono said for a stopped Ollama on Windows: a line break, then NULs.
            var handler = ScriptedHandler.Throwing(new HttpRequestException("An error occurred while sending the request",
                new WebException("No connection could be made because the target machine actively refused it.\n\0\0\0\0\0")));

            var failure = await Fails(Provider(handler).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Message, Does.EndWith("(No connection could be made because the target machine actively refused it.)"));
            Assert.That(failure.Message.IndexOfAny(new[] { '\0', '\n', '\r' }), Is.EqualTo(-1));
        }

        [Test]
        public async Task AClosedPortIsUnreachableThroughTheRealTransport()
        {
            // The one test that uses HttpClient's own handler: what it throws for a refused
            // connection is what decides the kind. A port just released is very likely closed.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var provider = new OllamaProvider(new OllamaSettings { Endpoint = $"http://127.0.0.1:{port}", TimeoutSeconds = 20f });

            var failure = await Fails(provider.RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.Unreachable), failure.Message);
            Assert.That(failure.Message, Does.Contain($"http://127.0.0.1:{port}"));
            Assert.That(failure.Message.IndexOf('\0'), Is.EqualTo(-1), "No NUL from the socket's message.");
        }

        [Test]
        public async Task AnAddressWithAPasswordIsNamedWithoutIt()
        {
            var handler = ScriptedHandler.Throwing(new HttpRequestException("refused"));
            var provider = Provider(handler, new OllamaSettings { Endpoint = "http://user:hunter2@127.0.0.1:11434" });

            var failure = await Fails(provider.RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Message, Does.Contain("http://127.0.0.1:11434"));
            Assert.That(failure.Message, Does.Not.Contain("hunter2"));
            Assert.That(failure.Message, Does.Not.Contain("user"));
        }

        [Test]
        public async Task AMissingModelIsModelNotFoundAndSaysWhatToPull()
        {
            // Ollama 0.34.2's exact reply for a model it does not have.
            var handler = ScriptedHandler.Replying("{\"error\":\"model 'nosuchmodel:1b' not found\"}", HttpStatusCode.NotFound);
            var provider = Provider(handler, new OllamaSettings { Model = "nosuchmodel:1b" });

            var failure = await Fails(provider.RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.ModelNotFound));
            Assert.That(failure.Message, Does.Contain("'nosuchmodel:1b'"));
            Assert.That(failure.Message, Does.Contain("ollama pull nosuchmodel:1b"));
        }

        [Test]
        public async Task A404FromSomethingElseIsRejected()
        {
            // What Ollama answers on a path it does not serve, as when the endpoint carries a
            // wrong prefix.
            var handler = ScriptedHandler.Replying("404 page not found", HttpStatusCode.NotFound, "text/plain");

            var failure = await Fails(Provider(handler).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.Rejected));
            Assert.That(failure.Message, Does.Contain("404"));
            Assert.That(failure.Message, Does.Contain("Is that address an Ollama server?"));
        }

        [TestCase(HttpStatusCode.BadRequest, "{\"error\":\"top_logprobs must be between 0 and 20\"}", ProviderErrorKind.Rejected, "400", "top_logprobs must be between 0 and 20")]
        [TestCase((HttpStatusCode)429, "{\"error\":\"too many requests\"}", ProviderErrorKind.Rejected, "429", "too many requests")]
        [TestCase(HttpStatusCode.InternalServerError, "{\"error\":\"model requires more system memory\"}", ProviderErrorKind.ServerError, "500", "model requires more system memory")]
        [TestCase(HttpStatusCode.ServiceUnavailable, "", ProviderErrorKind.ServerError, "503", "(nothing)")]
        public async Task OtherStatusesAreRejectedOrServerErrors(HttpStatusCode status, string body, ProviderErrorKind kind,
                                                                 string code, string said)
        {
            var handler = ScriptedHandler.Replying(body, status);

            var failure = await Fails(Provider(handler).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(kind));
            Assert.That(failure.Message, Does.Contain("HTTP " + code));
            Assert.That(failure.Message, Does.Contain(said));
        }

        [TestCase("not json at all")]
        [TestCase("{}")]
        [TestCase("{\"message\":{\"role\":\"assistant\"}}")]
        [TestCase("{\"message\":{\"content\":5}}")]
        [TestCase("{\"message\":\"text\"}")]
        [TestCase("[{\"message\":{\"content\":\"\"}}]")]
        [TestCase("{\"message\":{\"content\":\"a\"},\"done\":false}\n{\"message\":{\"content\":\"b\"},\"done\":true}")]
        [TestCase("{\"message\":{\"content\":\"\"},\"eval_count\":\"many\"}")]
        [TestCase("{\"message\":{\"content\":\"\"},\"eval_count\":2147483648}")]
        [TestCase("{\"message\":{\"content\":\"\"},\"prompt_eval_count\":99999999999999999999}")]
        [TestCase("{\"message\":{\"content\":\"\"},\"prompt_eval_count\":-1}")]
        [TestCase("{\"message\":{\"content\":\"\"},\"logprobs\":{}}")]
        [TestCase("{\"message\":{\"content\":\"\"},\"logprobs\":[{\"token\":\"a\"}]}")]
        [TestCase("{\"message\":{\"content\":\"\"},\"logprobs\":[{\"token\":\"a\",\"logprob\":0,\"top_logprobs\":5}]}")]
        public async Task AnEnvelopeThatCannotBeReadIsMalformed(string body)
        {
            var handler = ScriptedHandler.Replying(body);

            var failure = await Fails(Provider(handler).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.MalformedResponse), failure.Message);
            Assert.That(failure.Message, Does.Contain("http://127.0.0.1:11434"));
        }

        [Test]
        public async Task NoReplyWithinTheTimeoutIsTimeoutAndAbortsTheRequest()
        {
            var handler = ScriptedHandler.Hanging();
            var provider = Provider(handler, new OllamaSettings { TimeoutSeconds = 0.2f });

            var failure = await Fails(provider.RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(failure.Message, Does.Contain("0.2 s"));
            Assert.That(failure.Message, Does.Contain("OllamaSettings.TimeoutSeconds"));
            Assert.That(handler.Aborted, Is.True, "The request itself is stopped, not just abandoned.");
        }

        [Test]
        public async Task TheCallersCancellationAbortsTheRequestAndThrowsOperationCanceled()
        {
            var handler = ScriptedHandler.Hanging();
            var provider = Provider(handler);
            using (var cts = new CancellationTokenSource())
            {
                var pending = provider.RequestAsync(Request(), cts.Token);
                cts.Cancel();

                try
                {
                    await pending;
                    Assert.Fail("A cancelled request has no result.");
                }
                catch (OperationCanceledException e)
                {
                    Assert.That(e.CancellationToken, Is.EqualTo(cts.Token));
                }
            }

            Assert.That(handler.Aborted, Is.True, "Cancelling aborts the HTTP request.");
        }

        [Test]
        public async Task ATokenCancelledBeforeTheCallIsACancellationNotATimeout()
        {
            var handler = ScriptedHandler.Hanging();
            var provider = Provider(handler, new OllamaSettings { TimeoutSeconds = 0.05f });
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                try
                {
                    await provider.RequestAsync(Request(), cts.Token);
                    Assert.Fail("A cancelled request has no result.");
                }
                catch (OperationCanceledException)
                {
                    // The caller asked for it, so it is a cancellation, never a Timeout.
                }
            }
        }

        [Test]
        public async Task AgentRecordsTheKindAndPassesCancellationThrough()
        {
            var missing = ScriptedHandler.Replying("{\"error\":\"model 'qwen3.5:2b' not found\"}", HttpStatusCode.NotFound);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. The provider failed\\. ProviderException: .*ollama pull qwen3\\.5:2b"));

            var failed = await Fixtures.NotCancelled(Fixtures.VillageGuard(Provider(missing)).DecideAsync("Go to the tower."));

            Assert.That(failed.Decision, Is.Null);
            Assert.That(failed.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ModelNotFound));
            Assert.That(failed.Telemetry.Model, Is.Empty, "No reply, so no model said it answered.");

            var hanging = ScriptedHandler.Hanging();
            var agent = Fixtures.VillageGuard(Provider(hanging));
            using (var cts = new CancellationTokenSource())
            {
                var pending = agent.DecideAsync("Go to the tower.", null, cts.Token);
                cts.Cancel();

                try
                {
                    await pending;
                    Assert.Fail("A cancelled decision has no result.");
                }
                catch (OperationCanceledException)
                {
                }
            }

            Assert.That(hanging.Aborted, Is.True);
        }

        // ---- Arguments ------------------------------------------------------------------------

        [Test]
        public void BadSettingsAreRefusedWhenTheProviderIsBuilt()
        {
            Assert.Throws<ArgumentNullException>(() => new OllamaProvider(null));

            var bad = new List<OllamaSettings>
            {
                new OllamaSettings { Endpoint = null },
                new OllamaSettings { Endpoint = "" },
                new OllamaSettings { Endpoint = "localhost:11434" },
                new OllamaSettings { Endpoint = "ftp://127.0.0.1:11434" },
                new OllamaSettings { Endpoint = "not an address" },
                new OllamaSettings { Endpoint = "http://127.0.0.1:11434/?model=x" },
                new OllamaSettings { Endpoint = "http://127.0.0.1:11434#top" },
                new OllamaSettings { Model = " " },
                new OllamaSettings { Model = null },
                new OllamaSettings { ContextSize = 0 },
                new OllamaSettings { Temperature = -0.1f },
                new OllamaSettings { Temperature = float.NaN },
                new OllamaSettings { Temperature = float.PositiveInfinity },
                new OllamaSettings { TimeoutSeconds = 0f },
                new OllamaSettings { TimeoutSeconds = float.NaN },
                new OllamaSettings { TimeoutSeconds = 1e10f },
            };

            foreach (var settings in bad)
            {
                Assert.Throws<ArgumentException>(() => new OllamaProvider(settings),
                    $"Endpoint '{settings.Endpoint}', model '{settings.Model}', context {settings.ContextSize}, " +
                    $"temperature {settings.Temperature}, timeout {settings.TimeoutSeconds}");
            }
        }

        [Test]
        public void ARefusedEndpointIsNotEchoedWithItsPassword()
        {
            foreach (string endpoint in new[] { "http://user:hunter2@127.0.0.1:11434/?model=x", "ftp://user:hunter2@127.0.0.1:11434" })
            {
                var refused = Assert.Throws<ArgumentException>(() => new OllamaProvider(new OllamaSettings { Endpoint = endpoint }));

                Assert.That(refused.Message, Does.Not.Contain("hunter2"));
                Assert.That(refused.Message, Does.Contain("OllamaSettings.Endpoint"));
            }
        }

        [Test]
        public void ARequestThatCannotBeSentThrowsAtTheCall()
        {
            var handler = ScriptedHandler.Replying(Envelope(GoToTheTower));
            var provider = Provider(handler);
            var noSchema = Request();
            noSchema.Schema = null;
            var badSchema = Request(new DecisionSchema(new[] { new SchemaField { Name = "action", AllowedValues = new string[0] } }));

            Assert.Throws<ArgumentNullException>(() => provider.RequestAsync(null, CancellationToken.None));
            Assert.Throws<ArgumentException>(() => provider.RequestAsync(noSchema, CancellationToken.None));
            Assert.Throws<ArgumentException>(() => provider.RequestAsync(badSchema, CancellationToken.None));
            Assert.That(handler.Requests, Is.Empty, "Nothing is sent.");
        }

        // ---- Helpers --------------------------------------------------------------------------

        private static OllamaProvider Provider(ScriptedHandler handler, OllamaSettings settings = null)
        {
            return new OllamaProvider(settings ?? new OllamaSettings(), handler);
        }

        // A request with a small schema, for the tests that are not about the village guard.
        private static DecisionRequest Request(DecisionSchema schema = null)
        {
            return new DecisionRequest
            {
                SystemPrompt = "You are the guard.",
                Stimulus = "Player: \"Go to the tower.\"",
                Schema = schema ?? new DecisionSchema(new[]
                {
                    new SchemaField { Name = "action", Description = DecisionSchema.ActionDescription, AllowedValues = new[] { "move_to", "none" }, Required = true },
                    new SchemaField { Name = "statement", Description = DecisionSchema.StatementDescription, Required = true },
                }),
            };
        }

        // Ollama's non-streaming chat reply around content.
        private static string Envelope(string content, string model = "qwen3.5:2b", int promptTokens = 143,
                                       int completionTokens = 21, string logprobs = "[]")
        {
            return "{\"model\":" + JsonConvert.ToString(model)
                + ",\"created_at\":\"2026-10-20T03:43:53.2937267Z\""
                + ",\"message\":{\"role\":\"assistant\",\"content\":" + JsonConvert.ToString(content) + "}"
                + ",\"done\":true,\"done_reason\":\"stop\""
                + ",\"prompt_eval_count\":" + promptTokens
                + ",\"eval_count\":" + completionTokens
                + ",\"logprobs\":" + logprobs + "}";
        }

        private static JObject Parse(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                return JObject.Load(reader);
            }
        }

        // The same JSON with no insignificant whitespace, fields in the order written.
        private static string Compact(string json)
        {
            return Parse(json).ToString(Formatting.None);
        }

        private static List<(string role, string content)> Messages(string body)
        {
            return Parse(body)["messages"].Select(m => ((string)m["role"], (string)m["content"])).ToList();
        }

        private static async Task<T> NotCancelled<T>(Awaitable<T> pending)
        {
            try
            {
                return await pending;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"The request ended cancelled, though nothing cancelled it: {e.Message}");
                return default;
            }
        }

        private static async Task<ProviderException> Fails(Awaitable<ProviderResult> pending)
        {
            try
            {
                await pending;
            }
            catch (ProviderException e)
            {
                return e;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"The request ended cancelled instead of failing: {e.Message}");
            }

            Assert.Fail("The request succeeded instead of failing.");
            return null;
        }

        // Stands in for the network: records each request as it was sent and answers as scripted.
        private sealed class ScriptedHandler : HttpMessageHandler
        {
            private Func<CancellationToken, Task<HttpResponseMessage>> answer;

            public readonly List<Sent> Requests = new List<Sent>();

            // Answer from a thread-pool thread, as a real socket does.
            public bool OnThreadPool;

            public int AnsweredOnThread { get; private set; }

            // Whether the request's cancellation token fired while it was pending.
            public bool Aborted { get; private set; }

            public static ScriptedHandler Replying(string body, HttpStatusCode status = HttpStatusCode.OK,
                                                   string contentType = "application/json")
            {
                var handler = new ScriptedHandler();
                handler.answer = ct =>
                {
                    var response = new HttpResponseMessage(status) { Content = new StringContent(body, new UTF8Encoding(false)) };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType) { CharSet = "utf-8" };
                    return Task.FromResult(response);
                };
                return handler;
            }

            public static ScriptedHandler Throwing(Exception failure)
            {
                var handler = new ScriptedHandler();
                handler.answer = ct => Task.FromException<HttpResponseMessage>(failure);
                return handler;
            }

            // Never answers; ends cancelled when the request's token fires, as an aborted socket does.
            public static ScriptedHandler Hanging()
            {
                var handler = new ScriptedHandler();
                handler.answer = ct =>
                {
                    var never = new TaskCompletionSource<HttpResponseMessage>();
                    ct.Register(() =>
                    {
                        handler.Aborted = true;
                        never.TrySetCanceled(ct);
                    });
                    return never.Task;
                };
                return handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                // StringContent is already in memory, so this completes at once.
                byte[] bytes = request.Content.ReadAsByteArrayAsync().Result;
                Requests.Add(new Sent
                {
                    Method = request.Method,
                    Uri = request.RequestUri,
                    ContentType = request.Content.Headers.ContentType?.MediaType,
                    Bytes = bytes,
                    Body = Encoding.UTF8.GetString(bytes),
                });

                if (!OnThreadPool)
                {
                    AnsweredOnThread = Thread.CurrentThread.ManagedThreadId;
                    return answer(ct);
                }

                return Task.Run(() =>
                {
                    Thread.Sleep(20);
                    AnsweredOnThread = Thread.CurrentThread.ManagedThreadId;
                    return answer(ct);
                });
            }
        }

        // A refused connection that remembers which threads read its message.
        private sealed class ThreadRecordingException : HttpRequestException
        {
            public readonly List<int> ReadOnThreads = new List<int>();

            public override string Message
            {
                get
                {
                    lock (ReadOnThreads)
                    {
                        ReadOnThreads.Add(Thread.CurrentThread.ManagedThreadId);
                    }

                    return "Connection refused";
                }
            }
        }

        private sealed class Sent
        {
            public HttpMethod Method;
            public Uri Uri;
            public string ContentType;
            public byte[] Bytes;
            public string Body;
        }
    }
}
