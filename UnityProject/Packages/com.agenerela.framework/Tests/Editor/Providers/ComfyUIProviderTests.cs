using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using Agenerela.Providers.ComfyUI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // ComfyUIProvider with no server (DR-025): a fake handler answers /prompt, /history, /view,
    // /free and /queue as ComfyUI v0.39.1 does, behind a gateway that wants a key.
    public sealed class ComfyUIProviderTests
    {
        private const string Key = "k3y-0f-th3-t3st-cl13nt";

        // tools/comfyui/workflows/image.json, cut to the nodes the params touch.
        private const string Workflow = @"{
  ""description"": ""test"",
  ""params"": {
    ""prompt"": { ""targets"": [[""27"", ""text""]], ""required"": true },
    ""seed"": { ""targets"": [[""3"", ""seed""]], ""random"": true },
    ""width"": { ""targets"": [[""13"", ""width""]] },
    ""height"": { ""targets"": [[""13"", ""height""]] }
  },
  ""prompt"": {
    ""28"": { ""class_type"": ""UNETLoader"", ""inputs"": { ""unet_name"": ""z_image_turbo_nvfp4.safetensors"", ""weight_dtype"": ""default"" } },
    ""27"": { ""class_type"": ""CLIPTextEncode"", ""inputs"": { ""clip"": [""30"", 0], ""text"": """" } },
    ""13"": { ""class_type"": ""EmptySD3LatentImage"", ""inputs"": { ""width"": 1024, ""height"": 1024, ""batch_size"": 1 } },
    ""3"": { ""class_type"": ""KSampler"", ""inputs"": { ""model"": [""28"", 0], ""seed"": 0, ""steps"": 8, ""cfg"": 1.0 } },
    ""9"": { ""class_type"": ""SaveImage"", ""inputs"": { ""images"": [""8"", 0], ""filename_prefix"": ""agenerela/image"" } }
  }
}";

        private static ImageRequest Request(long? seed = 42)
        {
            return new ImageRequest { Prompt = "a painted king", Width = 512, Height = 640, Seed = seed };
        }

        private static ComfyUIProvider Provider(FakeComfy server, Action<ComfyUISettings> change = null)
        {
            var settings = new ComfyUISettings { Url = "http://192.168.1.4:8289/", ApiKey = Key, WorkflowJson = Workflow, PollSeconds = 0f, TimeoutSeconds = 5f };
            change?.Invoke(settings);
            return new ComfyUIProvider(settings, server);
        }

        // ---- A picture made -----------------------------------------------------------------------

        [Test]
        public async Task TheRequestIsWrittenIntoTheWorkflowAndThePictureComesBack()
        {
            var server = new FakeComfy { PendingPolls = 2 };
            var provider = Provider(server, s => s.DefaultParameters["28.unet_name"] = "z_image_turbo_int8_convrot.safetensors");
            var request = Request();
            request.Parameters["3.steps"] = "6";

            var result = await Fixtures.NotCancelled(provider.GenerateAsync(request, CancellationToken.None));

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Png, Is.EqualTo(FakeComfy.Png));
            Assert.That(result.Seed, Is.EqualTo(42));
            Assert.That(result.ProviderName, Is.EqualTo("comfyui"));
            Assert.That(result.GenerateSeconds, Is.EqualTo(12.5f).Within(0.001f), "From execution_start to execution_success.");

            var graph = (JObject)JObject.Parse(server.Sent("POST", "/prompt").Body)["prompt"];
            Assert.That((string)graph["27"]["inputs"]["text"], Is.EqualTo("a painted king"));
            Assert.That((long)graph["3"]["inputs"]["seed"], Is.EqualTo(42));
            Assert.That((long)graph["3"]["inputs"]["steps"], Is.EqualTo(6), "A node.input parameter, coerced to the workflow's integer.");
            Assert.That((long)graph["13"]["inputs"]["width"], Is.EqualTo(512));
            Assert.That((long)graph["13"]["inputs"]["height"], Is.EqualTo(640));
            Assert.That((string)graph["28"]["inputs"]["unet_name"], Is.EqualTo("z_image_turbo_int8_convrot.safetensors"), "A default parameter.");
            Assert.That(server.Requests.Count(r => r.Path.StartsWith("/history/p-1", StringComparison.Ordinal)), Is.EqualTo(3), "Polled until the job was in the history.");
            Assert.That(server.Requests.Last().Path, Is.EqualTo("/free"), "The card is given back after the picture.");
        }

        [Test]
        public async Task TheKeyGoesOnlyInTheAuthorizationHeader()
        {
            var server = new FakeComfy();

            await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(), CancellationToken.None));

            Assert.That(server.Requests, Has.Count.GreaterThan(2));
            Assert.That(server.Requests.Select(r => r.Authorization), Has.All.EqualTo("Bearer " + Key));
            Assert.That(server.Requests.Select(r => r.Uri.ToString() + r.Body), Has.None.Contains(Key));
        }

        [Test]
        public async Task WithNoSeedOneIsChosenAndReported()
        {
            var server = new FakeComfy();

            var result = await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(seed: null), CancellationToken.None));

            var graph = JObject.Parse(server.Sent("POST", "/prompt").Body)["prompt"];
            Assert.That(result.Seed, Is.Not.Null);
            Assert.That((long)graph["3"]["inputs"]["seed"], Is.EqualTo(result.Seed.Value));
        }

        [Test]
        public async Task ABatchLeavesTheModelsLoadedUntilFreed()
        {
            var server = new FakeComfy();
            var provider = Provider(server, s => s.FreeAfterEach = false);

            await Fixtures.NotCancelled(provider.GenerateAsync(Request(), CancellationToken.None));
            Assert.That(server.Requests.Select(r => r.Path), Has.None.EqualTo("/free"));

            Assert.That(await Fixtures.NotCancelled(provider.FreeAsync(CancellationToken.None)), Is.True);
            Assert.That(server.Requests.Last().Path, Is.EqualTo("/free"));
        }

        [Test]
        public async Task ABareApiGraphTakesNodeInputParameters()
        {
            var server = new FakeComfy();
            string bare = JObject.Parse(Workflow)["prompt"].ToString();
            var provider = Provider(server, s => s.WorkflowJson = bare);
            var request = Request();
            request.Parameters["27.text"] = "a bare prompt";

            var result = await Fixtures.NotCancelled(provider.GenerateAsync(request, CancellationToken.None));

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(provider.Capabilities.HonoursSeed, Is.False, "No seed param, so the seed is not written anywhere.");
            Assert.That((string)JObject.Parse(server.Sent("POST", "/prompt").Body)["prompt"]["27"]["inputs"]["text"], Is.EqualTo("a bare prompt"));
        }

        // ---- Requests that cannot be sent --------------------------------------------------------

        [Test]
        public void ARequestThatCannotBeSentFailsAtTheCallAndSendsNothing()
        {
            var server = new FakeComfy();
            var provider = Provider(server);
            var unknown = Request();
            unknown.Parameters["99.nothing"] = "1";
            var wired = Request();
            wired.Parameters["27.clip"] = "x";
            var typed = Request();
            typed.Parameters["3.steps"] = "eight";

            Assert.Throws<ArgumentException>(() => provider.GenerateAsync(unknown, CancellationToken.None));
            Assert.Throws<ArgumentException>(() => provider.GenerateAsync(wired, CancellationToken.None));
            Assert.Throws<ArgumentException>(() => provider.GenerateAsync(typed, CancellationToken.None));
            Assert.Throws<ArgumentException>(() => provider.GenerateAsync(new ImageRequest { Prompt = "x", Width = 500, Height = 512 }, CancellationToken.None));
            Assert.That(server.Requests, Is.Empty);
        }

        [Test]
        public void BadSettingsFailWhenTheProviderIsBuilt()
        {
            Assert.Throws<ArgumentException>(() => Provider(new FakeComfy(), s => s.WorkflowJson = ""));
            Assert.Throws<ArgumentException>(() => Provider(new FakeComfy(), s => s.WorkflowJson = "{\"prompt\": {\"1\": {}}}"));
            Assert.Throws<ArgumentException>(() => Provider(new FakeComfy(), s => s.Url = "ftp://host"));
            Assert.Throws<ArgumentException>(() => Provider(new FakeComfy(), s => s.ApiKey = "two words"));
            Assert.Throws<ArgumentException>(() => Provider(new FakeComfy(), s => s.DefaultParameters["nope"] = "1"));
        }

        // ---- Failures, as results ---------------------------------------------------------------

        [Test]
        public async Task AModelTheServerLacksIsModelNotFound()
        {
            var server = new FakeComfy
            {
                OnPrompt = () => (HttpStatusCode.BadRequest,
                    "{\"error\":{\"message\":\"Prompt outputs failed validation\"},\"node_errors\":{\"28\":{\"class_type\":\"UNETLoader\",\"errors\":[{\"type\":\"value_not_in_list\",\"message\":\"Value not in list\",\"details\":\"unet_name: 'z_image_turbo_nvfp4.safetensors' not in []\"}]}}}"),
            };

            var result = await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(), CancellationToken.None));

            Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.ModelNotFound));
            Assert.That(result.ErrorMessage, Does.Contain("node 28 (UNETLoader)"));
        }

        [Test]
        public async Task ARefusedKeyIsRejectedAndTheMessageNamesNoKey()
        {
            var server = new FakeComfy { OnPrompt = () => (HttpStatusCode.Unauthorized, "bad token " + Key) };

            var result = await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(), CancellationToken.None));

            Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.Rejected));
            Assert.That(result.ErrorMessage, Does.Contain("401").And.Not.Contain(Key));
        }

        [Test]
        public async Task ABusyCardIsAServerErrorWithTheServersWordsAndNoKey()
        {
            var server = new FakeComfy { OnPrompt = () => (HttpStatusCode.ServiceUnavailable, "the card is held by ollama for agenerela (" + Key + ")") };

            var result = await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(), CancellationToken.None));

            Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.ServerError));
            Assert.That(result.ErrorMessage, Does.Contain("held by ollama").And.Contain("<redacted>").And.Not.Contain(Key));
        }

        [Test]
        public async Task NothingAnsweringIsUnreachable()
        {
            var server = new FakeComfy { Refuse = true };

            var result = await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(), CancellationToken.None));

            Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.Unreachable));
            Assert.That(result.ErrorMessage, Does.Contain("http://192.168.1.4:8289"));
        }

        [Test]
        public async Task ANodeThatFailedIsAServerErrorNamingIt()
        {
            var server = new FakeComfy { History = id => "{\"" + id + "\":{\"status\":{\"status_str\":\"error\",\"messages\":[[\"execution_error\",{\"node_id\":\"3\",\"node_type\":\"KSampler\",\"exception_type\":\"torch.OutOfMemoryError\",\"exception_message\":\"out of memory\"}]]},\"outputs\":{}}}" };

            var result = await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(), CancellationToken.None));

            Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.ServerError));
            Assert.That(result.ErrorMessage, Does.Contain("node 3 (KSampler)").And.Contain("out of memory"));
        }

        [Test]
        public async Task ADownloadThatIsNotAPngIsMalformed()
        {
            var server = new FakeComfy { View = Encoding.UTF8.GetBytes("<html>not a picture</html>") };

            var result = await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(), CancellationToken.None));

            Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.MalformedResponse));
        }

        [Test]
        public async Task AJobThatNeverFinishesTimesOutAndIsTakenOffTheQueue()
        {
            var server = new FakeComfy { PendingPolls = int.MaxValue };
            var provider = Provider(server, s => { s.TimeoutSeconds = 0.3f; s.PollSeconds = 0.05f; });

            var result = await Fixtures.NotCancelled(provider.GenerateAsync(Request(), CancellationToken.None));

            Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(result.Seconds, Is.GreaterThanOrEqualTo(0.25f));
            var withdraw = server.Sent("POST", "/queue");
            Assert.That((string)JObject.Parse(withdraw.Body)["delete"][0], Is.EqualTo("p-1"));
        }

        [Test]
        public async Task TheCallersCancellationThrows()
        {
            var server = new FakeComfy { PendingPolls = int.MaxValue };
            var provider = Provider(server, s => s.PollSeconds = 0.05f);
            bool cancelled = false;
            using (var cts = new CancellationTokenSource())
            {
                var waiting = provider.GenerateAsync(Request(), cts.Token);
                cts.CancelAfter(150);
                try
                {
                    await waiting;
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
            }

            Assert.That(cancelled, Is.True);
            Assert.That(server.Requests.Select(r => r.Path), Has.Some.EqualTo("/queue"));
        }

        // ---- The fake server ---------------------------------------------------------------------

        private sealed class FakeComfy : HttpMessageHandler
        {
            public static readonly byte[] Png = ScriptedImageProvider.OnePixelPng;

            public readonly List<Sent> Requests = new List<Sent>();
            public int PendingPolls;
            public bool Refuse;
            public Func<(HttpStatusCode, string)> OnPrompt = () => (HttpStatusCode.OK, "{\"prompt_id\":\"p-1\",\"number\":1,\"node_errors\":{}}");
            public byte[] View = Png;

            public Func<string, string> History = id =>
                "{\"" + id + "\":{\"status\":{\"status_str\":\"success\",\"completed\":true,\"messages\":[" +
                "[\"execution_start\",{\"prompt_id\":\"" + id + "\",\"timestamp\":1700000000000}]," +
                "[\"execution_success\",{\"prompt_id\":\"" + id + "\",\"timestamp\":1700000012500}]]}," +
                "\"outputs\":{\"9\":{\"images\":[{\"filename\":\"agenerela/image_00001_.png\",\"subfolder\":\"\",\"type\":\"output\"}]}}}}";

            private int polls;

            public Sent Sent(string method, string path)
            {
                var found = Requests.FirstOrDefault(r => r.Method == method && r.Path == path);
                Assert.That(found, Is.Not.Null, $"Nothing was sent as {method} {path}. Sent: {string.Join(", ", Requests.Select(r => r.Method + " " + r.Path))}");
                return found;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync();
                var sent = new Sent
                {
                    Method = request.Method.Method,
                    Uri = request.RequestUri,
                    Path = request.RequestUri.AbsolutePath,
                    Authorization = request.Headers.Authorization?.ToString(),
                    Body = body,
                };
                Requests.Add(sent);
                ct.ThrowIfCancellationRequested();
                if (Refuse)
                {
                    throw new HttpRequestException("No connection could be made because the target machine actively refused it.");
                }

                if (sent.Path == "/prompt")
                {
                    var (status, text) = OnPrompt();
                    return Answer(status, text);
                }

                if (sent.Path.StartsWith("/history/", StringComparison.Ordinal))
                {
                    string id = sent.Path.Substring("/history/".Length);
                    return Answer(HttpStatusCode.OK, polls++ < PendingPolls ? "{}" : History(id));
                }

                if (sent.Path == "/view")
                {
                    Assert.That(request.RequestUri.Query, Does.Contain("filename=agenerela%2Fimage_00001_.png").And.Contain("type=output"));
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(View) };
                }

                return Answer(HttpStatusCode.OK, "");
            }

            private static HttpResponseMessage Answer(HttpStatusCode status, string text)
            {
                return new HttpResponseMessage(status) { Content = new StringContent(text, new UTF8Encoding(false), "application/json") };
            }
        }

        private sealed class Sent
        {
            public string Method;
            public Uri Uri;
            public string Path;
            public string Authorization;
            public string Body;
        }
    }
}
