using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // OllamaProvider's model lifecycle (DR-019, #190 workstream D) with no Ollama running: a scripted
    // server answers /api/tags, /api/generate and /api/ps in the shapes Ollama 0.40.14 gives. The
    // live check against a real server is done by hand once (the PR says so). Every await that must
    // not end cancelled goes through a helper that fails on OperationCanceledException.
    public sealed class OllamaLifecycleTests
    {
        private const string Chat = "{\"model\":\"qwen3.5:4b\",\"message\":{\"role\":\"assistant\",\"content\":"
            + "\"{\\\"action\\\":\\\"move_to\\\",\\\"target\\\":\\\"tower\\\",\\\"statement\\\":\\\"On my way.\\\"}\"},\"done\":true}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public void OllamaManagesItsModels()
        {
            Assert.That(Provider(new Server()), Is.InstanceOf<IModelLifecycle>());
        }

        // ---- ListModelsAsync ------------------------------------------------------------------

        [Test]
        public async Task ListModelsReadsTheInstalledModelsFromApiTags()
        {
            var server = new Server();
            server.Answer("/api/tags", "{\"models\":[{\"name\":\"qwen3.5:4b\",\"model\":\"qwen3.5:4b\",\"size\":2741000000},"
                                     + "{\"name\":\"gemma4:e2b-it-qat\",\"size\":4100000000},{\"name\":\"tiny:latest\"}]}");

            var models = await Done(Provider(server).ListModelsAsync(CancellationToken.None));

            Assert.That(server.Log, Is.EqualTo(new[] { "GET /api/tags" }));
            Assert.That(models, Has.Count.EqualTo(3));
            Assert.That(models[0].Name, Is.EqualTo("qwen3.5:4b"));
            Assert.That(models[0].SizeBytes, Is.EqualTo(2741000000L));
            Assert.That(models[1].Name, Is.EqualTo("gemma4:e2b-it-qat"));
            Assert.That(models[2].SizeBytes, Is.Zero, "No size given reads as 0.");
            Assert.That(models[2].ToString(), Is.EqualTo("tiny:latest"));
        }

        [TestCase("{\"models\":[]}")]
        [TestCase("{\"models\":null}")]
        [TestCase("{}")]
        public async Task AServerWithNoModelsListsNone(string body)
        {
            var server = new Server();
            server.Answer("/api/tags", body);

            var models = await Done(Provider(server).ListModelsAsync(CancellationToken.None));

            Assert.That(models, Is.Empty);
        }

        [TestCase("{\"models\":\"qwen3.5:4b\"}", "models is not a list")]
        [TestCase("{\"models\":[{\"size\":12}]}", "entry has no name")]
        [TestCase("{\"models\":[{\"name\":\" \"}]}", "entry has no name")]
        [TestCase("not json", "is not one JSON object")]
        public async Task AListThatIsNotOllamasIsAMalformedResponse(string body, string why)
        {
            var server = new Server();
            server.Answer("/api/tags", body);

            var failure = await Fails(Provider(server).ListModelsAsync(CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.MalformedResponse));
            Assert.That(failure.Message, Does.Contain(why));
        }

        [Test]
        public async Task ListingFromAServerThatIsNotRunningIsUnreachable()
        {
            var server = new Server { Refused = true };

            var failure = await Fails(Provider(server).ListModelsAsync(CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.Unreachable));
            Assert.That(failure.Message, Does.Contain("Nothing answered at http://127.0.0.1:11434."));
        }

        // ---- WarmUpAsync ----------------------------------------------------------------------

        [Test]
        public async Task AWarmUpAsksApiGenerateForTheProvidersModelWithNoPromptAtItsContextSize()
        {
            var server = new Server();
            server.Answer("/api/generate", "{\"model\":\"qwen3.5:4b\",\"response\":\"\",\"done\":true,\"done_reason\":\"load\"}");
            var provider = new OllamaProvider(new OllamaSettings { ContextSize = 8192 }, server);

            await Done(provider.WarmUpAsync(CancellationToken.None));

            Assert.That(server.Log, Is.EqualTo(new[] { "POST /api/generate" }));
            var expected = new JObject { ["model"] = "qwen3.5:4b", ["options"] = new JObject { ["num_ctx"] = 8192 } };
            Assert.That(JToken.DeepEquals(JObject.Parse(server.Bodies[0]), expected), Is.True,
                "No prompt, so Ollama loads the model and generates nothing; the decisions' num_ctx, so the first "
              + $"decision does not load it again at another size (Codex review, F1). Sent: {server.Bodies[0]}");
        }

        [Test]
        public async Task WarmingUpAModelTheServerHasNotPulledIsModelNotFound()
        {
            var server = new Server();
            server.Answer("/api/generate", "{\"error\":\"model \\\"qwen3.5:4b\\\" not found, try pulling it first\"}", HttpStatusCode.NotFound);

            var failure = await Fails(Provider(server).WarmUpAsync(CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.ModelNotFound));
            Assert.That(failure.Message, Does.Contain("Run `ollama pull qwen3.5:4b`."));
        }

        [Test]
        public async Task AWarmUpThatIsNotOneJsonObjectIsAMalformedResponse()
        {
            var server = new Server();
            server.Answer("/api/generate", "loaded");

            var failure = await Fails(Provider(server).WarmUpAsync(CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.MalformedResponse));
        }

        [Test]
        public async Task CancellingAWarmUpAbortsItsRequest()
        {
            // How a warm-up a newer one replaces is stopped (DR-019): the caller's token.
            var server = new Server();
            server.Hang("/api/generate");

            using (var cts = new CancellationTokenSource())
            {
                var warming = Provider(server).WarmUpAsync(cts.Token);
                cts.Cancel();
                try
                {
                    await warming;
                    Assert.Fail("A cancelled warm-up ends cancelled.");
                }
                catch (OperationCanceledException)
                {
                }
            }

            Assert.That(server.Aborted, Is.True, "The request itself was aborted.");
        }

        [Test]
        public async Task AWarmUpNeverWaitsForTheDecisionQueue()
        {
            // Living Vale's bug (gaps list, #7): warm-ups sent as decisions queued up, and the
            // player's turns timed out behind them. A warm-up is not a decision, so it holds no
            // slot and waits for none.
            var server = new Server();
            server.Hang("/api/chat");
            server.Answer("/api/generate", "{\"model\":\"qwen3.5:4b\",\"done\":true,\"done_reason\":\"load\"}");
            var provider = Provider(server);
            var agent = Fixtures.VillageGuard(provider);
            var deciding = agent.DecideAsync("Go to the tower.");
            var queued = agent.DecideAsync("Go to the tower.", new DecideOptions { Priority = DecisionPriority.Background });
            Assert.That(RequestScheduler.For(provider).InFlight, Is.EqualTo(1), "Set-up: one decision holds the only slot.");
            Assert.That(RequestScheduler.For(provider).Queued, Is.EqualTo(1));

            var warming = provider.WarmUpAsync(CancellationToken.None);
            for (int i = 0; i < 100 && !warming.GetAwaiter().IsCompleted; i++)
            {
                await Task.Yield();
            }

            bool warmedBesideTheDecision = warming.GetAwaiter().IsCompleted;
            var log = server.Log.ToArray();
            int inFlight = RequestScheduler.For(provider).InFlight, waiting = RequestScheduler.For(provider).Queued;

            // Everything finishes before the verdict, so a failure here never leaves a request hanging.
            server.Answer("/api/chat", Chat);
            server.Release();
            await Fixtures.NotCancelled(deciding);
            await Fixtures.NotCancelled(queued);
            await Done(warming);

            Assert.That(warmedBesideTheDecision, Is.True, "It did not wait for the decision in flight.");
            Assert.That(log, Is.EqualTo(new[] { "POST /api/chat", "POST /api/generate" }), "Sent beside the decision in flight.");
            Assert.That(inFlight, Is.EqualTo(1), "It never took a slot.");
            Assert.That(waiting, Is.EqualTo(1), "Nor joined the queue.");
        }

        [Test]
        public async Task ALifecycleCallReportsNothingToTheProvidersHealth()
        {
            // Health follows decisions (ProviderHealth): a warm-up that fails is the caller's to read.
            var server = new Server { Refused = true };
            var provider = Provider(server);

            await Fails(provider.WarmUpAsync(CancellationToken.None));

            Assert.That(ProviderHealth.For(provider).IsDown, Is.False);
        }

        // ---- UnloadAsync ----------------------------------------------------------------------

        [Test]
        public async Task AnUnloadSendsKeepAliveZeroAndWaitsUntilApiPsNoLongerListsTheModel()
        {
            var server = new Server();
            server.Answer("/api/generate", "{\"model\":\"qwen3.5:4b\",\"done\":true,\"done_reason\":\"unload\"}");
            int reads = 0;
            server.Answer("/api/ps", () => ++reads < 3
                ? "{\"models\":[{\"name\":\"qwen3.5:4b\",\"size\":1,\"size_vram\":1}]}"
                : "{\"models\":[]}");

            bool unloaded = await Done(Provider(server).UnloadAsync(CancellationToken.None));

            Assert.That(unloaded, Is.True);
            Assert.That(server.Log, Is.EqualTo(new[] { "POST /api/generate", "GET /api/ps", "GET /api/ps", "GET /api/ps" }),
                "It waited until the model left /api/ps.");
            Assert.That(JToken.DeepEquals(JObject.Parse(server.Bodies[0]), new JObject { ["model"] = "qwen3.5:4b", ["keep_alive"] = 0 }), Is.True,
                $"Sent: {server.Bodies[0]}");
        }

        [Test]
        public async Task UnloadingAModelTheServerHasNotPulledIsModelNotFound()
        {
            var server = new Server();
            server.Answer("/api/generate", "{\"error\":\"model 'qwen3.5:4b' not found\"}", HttpStatusCode.NotFound);

            var failure = await Fails(Provider(server).UnloadAsync(CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.ModelNotFound));
        }

        // ---- Helpers --------------------------------------------------------------------------

        private static OllamaProvider Provider(Server server)
        {
            return new OllamaProvider(new OllamaSettings(), server);
        }

        private static async Task<T> Done<T>(Awaitable<T> pending)
        {
            try
            {
                return await pending;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"It ended cancelled, though nothing cancelled it: {e.Message}");
                return default;
            }
        }

        private static async Task Done(Awaitable pending)
        {
            try
            {
                await pending;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"It ended cancelled, though nothing cancelled it: {e.Message}");
            }
        }

        private static async Task<ProviderException> Fails<T>(Awaitable<T> pending)
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
                Assert.Fail($"It ended cancelled instead of failing: {e.Message}");
            }

            Assert.Fail("It succeeded instead of failing.");
            return null;
        }

        private static async Task<ProviderException> Fails(Awaitable pending)
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
                Assert.Fail($"It ended cancelled instead of failing: {e.Message}");
            }

            Assert.Fail("It succeeded instead of failing.");
            return null;
        }

        // An Ollama server by path: each answers as scripted, logs "METHOD /path", and keeps the body.
        private sealed class Server : HttpMessageHandler
        {
            private readonly Dictionary<string, Func<(HttpStatusCode, string)>> answers = new Dictionary<string, Func<(HttpStatusCode, string)>>();
            private readonly HashSet<string> hanging = new HashSet<string>();
            private readonly List<(TaskCompletionSource<HttpResponseMessage> Pending, string Path)> held =
                new List<(TaskCompletionSource<HttpResponseMessage>, string)>();

            public readonly List<string> Log = new List<string>();
            public readonly List<string> Bodies = new List<string>();

            // Every request fails as a refused connection does.
            public bool Refused;

            // Whether a hanging request's token fired.
            public bool Aborted { get; private set; }

            public void Answer(string path, string body, HttpStatusCode status = HttpStatusCode.OK)
            {
                hanging.Remove(path);
                answers[path] = () => (status, body);
            }

            public void Answer(string path, Func<string> body)
            {
                hanging.Remove(path);
                answers[path] = () => (HttpStatusCode.OK, body());
            }

            // Requests to this path wait until Release, or until their token fires.
            public void Hang(string path)
            {
                hanging.Add(path);
            }

            // Answers every held request as its path is scripted now.
            public void Release()
            {
                var pending = held.ToArray();
                held.Clear();
                foreach (var (source, path) in pending)
                {
                    source.TrySetResult(Respond(path));
                }
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                string path = request.RequestUri.AbsolutePath;
                Log.Add($"{request.Method} {path}");
                if (request.Content != null)
                {
                    Bodies.Add(request.Content.ReadAsStringAsync().Result);
                }

                if (Refused)
                {
                    return Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused"));
                }

                if (hanging.Contains(path))
                {
                    var pending = new TaskCompletionSource<HttpResponseMessage>();
                    held.Add((pending, path));
                    ct.Register(() =>
                    {
                        Aborted = true;
                        pending.TrySetCanceled(ct);
                    });
                    return pending.Task;
                }

                return Task.FromResult(Respond(path));
            }

            private HttpResponseMessage Respond(string path)
            {
                if (!answers.TryGetValue(path, out var answer))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("404 page not found") };
                }

                var (status, body) = answer();
                return new HttpResponseMessage(status) { Content = new StringContent(body, new UTF8Encoding(false), "application/json") };
            }
        }
    }
}
