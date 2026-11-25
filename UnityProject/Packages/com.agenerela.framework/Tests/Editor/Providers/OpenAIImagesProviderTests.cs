using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using Agenerela.Providers.OpenAIImages;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // OpenAIImagesProvider with no server (DR-028): a fake handler answers /v1/images/generations and
    // /v1/images/edits as sd-server and OpenAI do, behind a key.
    public sealed class OpenAIImagesProviderTests
    {
        private const string Key = "k3y-0f-th3-1mag3-t3st";
        private static readonly byte[] Png = ScriptedImageProvider.OnePixelPng;
        private static readonly byte[] OtherPng = Png.Concat(new byte[] { 9, 9 }).ToArray();

        private static ImageRequest Request(long? seed = 42)
        {
            return new ImageRequest { Prompt = "a painted king", Width = 768, Height = 896, Seed = seed };
        }

        private static ImageRequest Edit(params byte[][] images)
        {
            var request = Request();
            request.Prompt = "give him a crown";
            request.Images.AddRange(images.Length == 0 ? new[] { Png } : images);
            return request;
        }

        private static OpenAIImagesProvider Provider(FakeServer server, Action<OpenAIImagesSettings> change = null)
        {
            var settings = new OpenAIImagesSettings { Url = "http://127.0.0.1:1234/v1/", ApiKey = Key, TimeoutSeconds = 5f };
            change?.Invoke(settings);
            var provider = new OpenAIImagesProvider(settings, server);
            provider.Pause = (wait, ct) =>
            {
                server.Pauses.Add(wait);
                return Task.CompletedTask;
            };
            return provider;
        }

        // ---- A new picture -------------------------------------------------------------------------

        [Test]
        public async Task ANewPictureIsPostedAsJsonAndThePngComesBack()
        {
            var server = new FakeServer();
            var provider = Provider(server, s => s.Model = "gpt-image-1");
            var request = Request();
            request.Parameters["quality"] = "high";

            var result = await Fixtures.NotCancelled(provider.GenerateAsync(request, CancellationToken.None));

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Png, Is.EqualTo(Png));
            Assert.That(result.ProviderName, Is.EqualTo("openai-images"));
            Assert.That(result.Seed, Is.Null, "With no extra-args block no seed is sent, so none is reported.");

            var sent = server.Requests.Single();
            Assert.That(sent.Path, Is.EqualTo("/v1/images/generations"));
            Assert.That(sent.ContentType, Is.EqualTo("application/json"));
            var body = JObject.Parse(sent.Body);
            Assert.That((string)body["prompt"], Is.EqualTo("a painted king"));
            Assert.That((string)body["size"], Is.EqualTo("768x896"));
            Assert.That((int)body["n"], Is.EqualTo(1));
            Assert.That((string)body["output_format"], Is.EqualTo("png"));
            Assert.That((string)body["model"], Is.EqualTo("gpt-image-1"));
            Assert.That((string)body["quality"], Is.EqualTo("high"), "A request parameter is a body field of its own name.");
            Assert.That(provider.Capabilities.HonoursSeed, Is.False);
        }

        [Test]
        public async Task TheKeyGoesOnlyInTheAuthorizationHeader()
        {
            var server = new FakeServer();

            await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(), CancellationToken.None));
            await Fixtures.NotCancelled(Provider(server).GenerateAsync(Edit(), CancellationToken.None));

            Assert.That(server.Requests.Select(r => r.Authorization), Has.All.EqualTo("Bearer " + Key));
            Assert.That(server.Requests.Select(r => r.Uri + r.Body), Has.None.Contains(Key));
            Assert.That(Provider(server).Endpoint, Is.EqualTo("http://127.0.0.1:1234/v1"));
        }

        [Test]
        public async Task NoModelAndNoKeyAreSentWhenNoneIsSet()
        {
            var server = new FakeServer();

            await Fixtures.NotCancelled(Provider(server, s => s.ApiKey = "").GenerateAsync(Request(), CancellationToken.None));

            Assert.That(server.Requests.Single().Authorization, Is.Null);
            Assert.That(JObject.Parse(server.Requests.Single().Body)["model"], Is.Null, "sd-server runs the model it was started with.");
        }

        // ---- sd-server's extra arguments -----------------------------------------------------------

        [Test]
        public async Task ForSdServerTheSeedStrengthAndExtrasGoInTheExtraArgsBlock()
        {
            var server = new FakeServer();
            var provider = Provider(server, s =>
            {
                s.SdCppExtraArgs = true;
                s.DefaultParameters["extra.sample_params.sample_steps"] = "8";
            });
            var request = Request();
            request.Parameters["strength"] = "0.6";
            request.Parameters["extra.sample_params.guidance.txt_cfg"] = "1";
            request.Parameters["negative_prompt"] = "text";

            var result = await Fixtures.NotCancelled(provider.GenerateAsync(request, CancellationToken.None));

            Assert.That(result.Seed, Is.EqualTo(42));
            Assert.That(provider.Capabilities.HonoursSeed, Is.True);
            string prompt = (string)JObject.Parse(server.Requests.Single().Body)["prompt"];
            var match = Regex.Match(prompt, "^a painted king <sd_cpp_extra_args>(.*)</sd_cpp_extra_args>$");
            Assert.That(match.Success, Is.True, prompt);
            var extra = JObject.Parse(match.Groups[1].Value);
            Assert.That((long)extra["seed"], Is.EqualTo(42));
            Assert.That((double)extra["strength"], Is.EqualTo(0.6).Within(1e-9));
            Assert.That((string)extra["negative_prompt"], Is.EqualTo("text"));
            Assert.That((long)extra["sample_params"]["sample_steps"], Is.EqualTo(8), "A default parameter, nested by its path.");
            Assert.That((long)extra["sample_params"]["guidance"]["txt_cfg"], Is.EqualTo(1));
            var body = JObject.Parse(server.Requests.Single().Body);
            Assert.That(body["strength"], Is.Null, "Not also sent as a body field.");
        }

        [Test]
        public async Task ForSdServerWithNoSeedOneIsChosenAndReported()
        {
            var server = new FakeServer();
            var provider = Provider(server, s => s.SdCppExtraArgs = true);

            var result = await Fixtures.NotCancelled(provider.GenerateAsync(Request(seed: null), CancellationToken.None));

            Assert.That(result.Seed, Is.Not.Null);
            Assert.That(provider.Describe(Request(seed: 5)).Seed, Is.EqualTo(5));
            Assert.That((string)JObject.Parse(server.Requests.Single().Body)["prompt"], Does.Contain("\"seed\":" + result.Seed.Value));
        }

        // ---- Edits ---------------------------------------------------------------------------------

        [Test]
        public async Task AnEditIsMultipartWithThePictureAndTheMask()
        {
            var server = new FakeServer();
            var request = Edit(Png);
            request.Mask = OtherPng;
            request.Parameters["quality"] = "low";

            var result = await Fixtures.NotCancelled(Provider(server).GenerateAsync(request, CancellationToken.None));

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            var sent = server.Requests.Single();
            Assert.That(sent.Path, Is.EqualTo("/v1/images/edits"));
            Assert.That(sent.ContentType, Is.EqualTo("multipart/form-data"));
            Assert.That(sent.Text("prompt"), Is.EqualTo("give him a crown"));
            Assert.That(sent.Text("size"), Is.EqualTo("768x896"));
            Assert.That(sent.Text("n"), Is.EqualTo("1"));
            Assert.That(sent.Text("quality"), Is.EqualTo("low"));
            Assert.That(sent.Files("image"), Is.EqualTo(new[] { Png }), "One picture is sent as image.");
            Assert.That(sent.Files("mask"), Is.EqualTo(new[] { OtherPng }));
            Assert.That(sent.Parts.Where(p => p.FileName != null).Select(p => p.ContentType), Has.All.EqualTo("image/png"));
        }

        [Test]
        public async Task SeveralPicturesAreSentAsImageArrayInOrder()
        {
            var server = new FakeServer();

            await Fixtures.NotCancelled(Provider(server).GenerateAsync(Edit(Png, OtherPng), CancellationToken.None));

            var sent = server.Requests.Single();
            Assert.That(sent.Files("image[]"), Is.EqualTo(new[] { Png, OtherPng }));
            Assert.That(sent.Files("image"), Is.Empty);
            Assert.That(sent.Files("mask"), Is.Empty);
        }

        [Test]
        public void AServerThatDoesNotEditRefusesAnEditAtTheCall()
        {
            var server = new FakeServer();
            var provider = Provider(server, s => s.Edits = false);

            Assert.That(provider.Capabilities.Edits, Is.False);
            Assert.That(provider.Capabilities.Masks, Is.False);
            Assert.Throws<ArgumentException>(() => provider.GenerateAsync(Edit(), CancellationToken.None));
            Assert.That(server.Requests, Is.Empty);
        }

        // ---- Requests that cannot be sent ----------------------------------------------------------

        [Test]
        public void AParameterThatCannotBeSentFailsAtTheCallAndSendsNothing()
        {
            var server = new FakeServer();
            var provider = Provider(server);

            var reserved = Request();
            reserved.Parameters["size"] = "1x1";
            var extra = Request();
            extra.Parameters["extra.sample_params.sample_steps"] = "8";
            var oddSize = Request();
            oddSize.Width = 770;

            Assert.That(Assert.Throws<ArgumentException>(() => provider.GenerateAsync(reserved, CancellationToken.None)).Message, Does.Contain("writes itself"));
            Assert.That(Assert.Throws<ArgumentException>(() => provider.GenerateAsync(extra, CancellationToken.None)).Message, Does.Contain("SdCppExtraArgs"));
            Assert.Throws<ArgumentException>(() => provider.GenerateAsync(oddSize, CancellationToken.None));
            Assert.That(server.Requests, Is.Empty);
        }

        [Test]
        public void BadSettingsFailWhenTheProviderIsMade()
        {
            var server = new FakeServer();

            Assert.Throws<ArgumentException>(() => Provider(server, s => s.Url = "not a url"));
            Assert.Throws<ArgumentException>(() => Provider(server, s => s.Url = "http://127.0.0.1:1234/v1?q=1"));
            Assert.Throws<ArgumentException>(() => Provider(server, s => s.ApiKey = "two words"));
            Assert.Throws<ArgumentException>(() => Provider(server, s => s.DefaultParameters["prompt"] = "x"));
            Assert.Throws<ArgumentException>(() => Provider(server, s => s.DefaultParameters["extra.sample_params.sample_steps"] = "8"));
            var e = Assert.Throws<ArgumentException>(() => Provider(server, s => s.Url = "http://user:" + Key + "@127.0.0.1:1234/v1"));
            Assert.That(e.Message, Does.Not.Contain(Key));
        }

        // ---- Failures are results ------------------------------------------------------------------

        [Test]
        public async Task ARefusedKeyIsRejectedAndTheKeyIsRedacted()
        {
            var server = new FakeServer { Answer = _ => (HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Incorrect API key provided: " + Key + "\",\"code\":\"invalid_api_key\"}}") };

            var result = await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(), CancellationToken.None));

            Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.Rejected));
            Assert.That(result.ErrorMessage, Does.Contain("refused the key").And.Contain("[key]"));
            Assert.That(result.ErrorMessage, Does.Not.Contain(Key));
        }

        [Test]
        public async Task EachStatusIsTheKindItMeans()
        {
            async Task<ProviderErrorKind?> KindFor(HttpStatusCode status, string body, Action<OpenAIImagesSettings> change = null)
            {
                var server = new FakeServer { Answer = _ => (status, body) };
                var result = await Fixtures.NotCancelled(Provider(server, change).GenerateAsync(Request(), CancellationToken.None));
                return result.Error;
            }

            Assert.That(await KindFor(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"The model 'nope' does not exist\",\"code\":\"model_not_found\"}}", s => s.Model = "nope"),
                Is.EqualTo(ProviderErrorKind.ModelNotFound));
            Assert.That(await KindFor(HttpStatusCode.NotFound, "404 Not Found"), Is.EqualTo(ProviderErrorKind.Rejected));
            Assert.That(await KindFor(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Your request was rejected by the safety system.\"}}"),
                Is.EqualTo(ProviderErrorKind.Rejected));
            Assert.That(await KindFor(HttpStatusCode.InternalServerError, "{\"error\":\"generation failed\"}"), Is.EqualTo(ProviderErrorKind.ServerError));
        }

        [Test]
        public async Task ABusyServerIsWaitedOutThenTheRequestIsSentAgain()
        {
            int calls = 0;
            var server = new FakeServer { Answer = _ => calls++ < 2 ? (HttpStatusCode.ServiceUnavailable, "busy") : (HttpStatusCode.OK, null) };
            server.RetryAfter = "2";
            var request = Edit();

            var result = await Fixtures.NotCancelled(Provider(server).GenerateAsync(request, CancellationToken.None));

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(server.Requests, Has.Count.EqualTo(3));
            Assert.That(server.Pauses, Is.EqualTo(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2) }), "Retry-After is followed.");
            Assert.That(server.Requests.Select(r => r.Files("image").Count), Has.All.EqualTo(1), "Each attempt carries the whole edit again.");
        }

        [Test]
        public async Task AServerStillBusyAfterThePolicyIsRateLimited()
        {
            var server = new FakeServer { Answer = _ => (HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"slow down\"}}") };

            var result = await Fixtures.NotCancelled(Provider(server, s => s.Retry = new RetryPolicy { MaxAttempts = 2, MaxWaitSeconds = 10f })
                .GenerateAsync(Request(), CancellationToken.None));

            Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.RateLimited));
            Assert.That(server.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task AReplyWithNoPngIsMalformed()
        {
            async Task<ImageResult> With(string body)
            {
                var server = new FakeServer { Answer = _ => (HttpStatusCode.OK, body) };
                return await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(), CancellationToken.None));
            }

            var url = await With("{\"data\":[{\"url\":\"http://127.0.0.1:1234/out.png\"}]}");
            Assert.That(url.Error, Is.EqualTo(ProviderErrorKind.MalformedResponse));
            Assert.That(url.ErrorMessage, Does.Contain("response_format"));

            var jpeg = await With("{\"data\":[{\"b64_json\":\"" + Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }) + "\"}]}");
            Assert.That(jpeg.Error, Is.EqualTo(ProviderErrorKind.MalformedResponse));
            Assert.That(jpeg.ErrorMessage, Does.Contain("not a PNG"));

            Assert.That((await With("<html>gateway</html>")).Error, Is.EqualTo(ProviderErrorKind.MalformedResponse));
            Assert.That((await With("{\"data\":[]}")).Error, Is.EqualTo(ProviderErrorKind.MalformedResponse));
            Assert.That((await With("{\"data\":[{\"b64_json\":\"%%%\"}]}")).Error, Is.EqualTo(ProviderErrorKind.MalformedResponse));
        }

        [Test]
        public async Task NothingAnsweringIsUnreachable()
        {
            var server = new FakeServer { Refuse = true };

            var result = await Fixtures.NotCancelled(Provider(server).GenerateAsync(Request(), CancellationToken.None));

            Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.Unreachable));
            Assert.That(result.ErrorMessage, Does.Contain("http://127.0.0.1:1234/v1"));
        }

        [Test]
        public async Task NoAnswerInTimeIsATimeout()
        {
            var server = new FakeServer { DelaySeconds = 5f };

            var result = await Fixtures.NotCancelled(Provider(server, s => s.TimeoutSeconds = 0.2f).GenerateAsync(Request(), CancellationToken.None));

            Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.Timeout));
        }

        [Test]
        public async Task TheCallersCancellationThrows()
        {
            var server = new FakeServer { DelaySeconds = 5f };
            bool cancelled = false;
            using (var cts = new CancellationTokenSource())
            {
                var waiting = Provider(server).GenerateAsync(Request(), cts.Token);
                cts.CancelAfter(100);
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
        }

        [Test]
        public void DescribeShowsWhatWouldBeSentWithNoKeyOrBytes()
        {
            var provider = Provider(new FakeServer(), s => s.SdCppExtraArgs = true);
            var request = Edit(Png, OtherPng);
            request.Mask = Png;
            request.Parameters["quality"] = "low";

            var call = provider.Describe(request);

            Assert.That(call.Route, Is.EqualTo("images/edits"));
            Assert.That(call.Size, Is.EqualTo("768x896"));
            Assert.That(call.Images, Is.EqualTo(2));
            Assert.That(call.HasMask, Is.True);
            Assert.That(call.Fields["quality"], Is.EqualTo("low"));
            Assert.That(call.Prompt, Does.StartWith("give him a crown <sd_cpp_extra_args>"));
            Assert.That(call.Prompt + string.Join(",", call.Fields.Values), Does.Not.Contain(Key));
        }

        // ---- The fake server -----------------------------------------------------------------------

        private sealed class FakeServer : HttpMessageHandler
        {
            public readonly List<Sent> Requests = new List<Sent>();
            public readonly List<TimeSpan> Pauses = new List<TimeSpan>();
            public bool Refuse;
            public float DelaySeconds;
            public string RetryAfter;

            // Status and body for each request; a null body is a picture.
            public Func<Sent, (HttpStatusCode, string)> Answer = _ => (HttpStatusCode.OK, null);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var sent = new Sent
                {
                    Uri = request.RequestUri,
                    Path = request.RequestUri.AbsolutePath,
                    Authorization = request.Headers.Authorization?.ToString(),
                    ContentType = request.Content?.Headers.ContentType?.MediaType,
                };
                if (request.Content is MultipartFormDataContent form)
                {
                    foreach (var part in form)
                    {
                        var disposition = part.Headers.ContentDisposition;
                        sent.Parts.Add(new Part
                        {
                            Name = disposition?.Name?.Trim('"'),
                            FileName = disposition?.FileName?.Trim('"'),
                            ContentType = part.Headers.ContentType?.MediaType,
                            Bytes = await part.ReadAsByteArrayAsync(),
                        });
                    }

                    sent.Body = string.Join("\n", sent.Parts.Where(p => p.FileName == null).Select(p => p.Name + "=" + Encoding.UTF8.GetString(p.Bytes)));
                }
                else
                {
                    sent.Body = request.Content == null ? "" : await request.Content.ReadAsStringAsync();
                }

                Requests.Add(sent);
                if (Refuse)
                {
                    throw new HttpRequestException("No connection could be made because the target machine actively refused it.");
                }

                if (DelaySeconds > 0f)
                {
                    await Task.Delay(TimeSpan.FromSeconds(DelaySeconds), ct);
                }

                ct.ThrowIfCancellationRequested();
                var (status, body) = Answer(sent);
                body = body ?? "{\"created\":1700000000,\"data\":[{\"b64_json\":\"" + Convert.ToBase64String(Png) + "\"}]}";
                var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                if (RetryAfter != null && ((int)status == 429 || (int)status == 503))
                {
                    response.Headers.TryAddWithoutValidation("Retry-After", RetryAfter);
                }

                return response;
            }
        }

        private sealed class Sent
        {
            public Uri Uri;
            public string Path;
            public string Authorization;
            public string ContentType;
            public string Body;
            public readonly List<Part> Parts = new List<Part>();

            public string Text(string name)
            {
                var part = Parts.SingleOrDefault(p => p.Name == name && p.FileName == null);
                Assert.That(part, Is.Not.Null, $"No text part {name}. Parts: {string.Join(", ", Parts.Select(p => p.Name))}");
                return Encoding.UTF8.GetString(part.Bytes);
            }

            public List<byte[]> Files(string name) => Parts.Where(p => p.Name == name && p.FileName != null).Select(p => p.Bytes).ToList();
        }

        private sealed class Part
        {
            public string Name;
            public string FileName;
            public string ContentType;
            public byte[] Bytes;
        }
    }
}
