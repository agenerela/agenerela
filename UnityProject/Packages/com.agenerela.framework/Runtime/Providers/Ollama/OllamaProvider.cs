using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// The development backend: a model served by Ollama on the developer's own machine, asked
    /// through its <c>/api/chat</c> endpoint. <c>new Agent(profile, new OllamaProvider(new
    /// OllamaSettings()))</c> decides against <c>qwen3.5:4b</c> at <c>http://127.0.0.1:11434</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It sends what the prototype sent wherever a measurement depends on it, because every
    /// number in the build plan's Appendix A was measured through Ollama that way: greedy
    /// decoding, thinking off, the schema in <c>format</c>, and <c>127.0.0.1</c> rather than
    /// <c>localhost</c>. The schema goes in <c>format</c> only, where Ollama compiles it into the
    /// sampling grammar; the model never reads it (findings, 10 October).
    /// </para>
    /// <para>
    /// The transport is <see cref="HttpClient"/>, one per provider, so the package needs nothing
    /// beyond Newtonsoft (DR-009). That rules out WebGL, where <see cref="HttpClient"/> has no
    /// sockets. Call <see cref="RequestAsync"/> from the main thread: the reply is read back on
    /// it, so <c>Agent</c> carries on there.
    /// </para>
    /// <para>
    /// A server that wants a key, such as the RTX 5080 behind <c>tools/ollama-server/</c>'s gateway,
    /// gets <see cref="OllamaSettings.ApiKey"/> as an <c>Authorization: Bearer</c> header on every
    /// request. An HTTP 401 is <see cref="ProviderErrorKind.Rejected"/>, and its message says
    /// whether a key was sent; no message names the key.
    /// </para>
    /// <para>
    /// Failures throw <see cref="ProviderException"/>: <see cref="ProviderErrorKind.Unreachable"/>
    /// when nothing answers, <see cref="ProviderErrorKind.ModelNotFound"/> when the server lacks
    /// the model, <see cref="ProviderErrorKind.Timeout"/> after
    /// <see cref="OllamaSettings.TimeoutSeconds"/>, <see cref="ProviderErrorKind.Rejected"/> for
    /// any other HTTP 4xx, <see cref="ProviderErrorKind.ServerError"/> for a 5xx, and
    /// <see cref="ProviderErrorKind.MalformedResponse"/> for a reply that is not Ollama's chat
    /// envelope.
    /// </para>
    /// <para>
    /// It manages its server's models too (<see cref="IModelLifecycle"/>, DR-019): the models
    /// installed, from <c>/api/tags</c>; a warm-up that loads its model, an <c>/api/generate</c>
    /// with no prompt and the context size its decisions send; and an unload, <c>keep_alive: 0</c>, which waits until
    /// <c>/api/ps</c> no longer lists the model. Each is one short request, with the same
    /// <see cref="OllamaSettings.TimeoutSeconds"/> and the same failures.
    /// </para>
    /// </remarks>
    public sealed class OllamaProvider : ILLMProvider, IModelLifecycle
    {
        // How long UnloadAsync waits for /api/ps to stop listing the model.
        private const float UnloadWaitSeconds = 15f;

        // The longest server text a message quotes: enough to see what went wrong.
        private const int Longest = 200;

        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        private readonly HttpClient client;
        private readonly Uri chatUri;
        private readonly string endpoint;
        private readonly string model;
        private readonly int contextSize;
        private readonly float temperature;
        private readonly int tokenAlternatives;
        private readonly float timeoutSeconds;
        private readonly bool sendsKey;
        private readonly JsonSchemaSerializer serializer = new JsonSchemaSerializer();

        /// <summary>A provider asking the server and model <paramref name="settings"/> name.</summary>
        /// <param name="settings">Copied now: changing them later changes nothing for this provider.</param>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The endpoint is not an absolute http or https address without a query, the model is empty, or the
        /// context size, temperature, timeout or token alternatives is out of range.
        /// </exception>
        public OllamaProvider(OllamaSettings settings)
            : this(settings, new HttpClientHandler())
        {
        }

        // The seam EditMode tests use: a handler that captures the request and scripts the reply,
        // so no Ollama has to be running.
        internal OllamaProvider(OllamaSettings settings, HttpMessageHandler handler)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            string trimmed = (settings.Endpoint ?? "").Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed + "/api/chat", UriKind.Absolute, out chatUri)
                || (chatUri.Scheme != Uri.UriSchemeHttp && chatUri.Scheme != Uri.UriSchemeHttps)
                || chatUri.Query.Length > 0 || chatUri.Fragment.Length > 0)
            {
                // The address itself is left out: it may carry a password (hard rule 1).
                throw new ArgumentException(
                    "OllamaSettings.Endpoint is not an http address with no query, such as http://127.0.0.1:11434.",
                    nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(settings.Model))
            {
                throw new ArgumentException("OllamaSettings.Model is empty. Name a model, such as qwen3.5:4b.", nameof(settings));
            }

            if (settings.ContextSize <= 0)
            {
                throw new ArgumentException($"OllamaSettings.ContextSize is {settings.ContextSize}. It must be above 0.", nameof(settings));
            }

            if (!(settings.Temperature >= 0f) || float.IsInfinity(settings.Temperature))
            {
                throw new ArgumentException($"OllamaSettings.Temperature is {settings.Temperature}. It must be 0 or above.", nameof(settings));
            }

            // CancelAfter takes whole milliseconds as an int, so about 24 days is the most it can wait.
            if (!(settings.TimeoutSeconds > 0f) || settings.TimeoutSeconds > int.MaxValue / 1000f)
            {
                throw new ArgumentException(
                    $"OllamaSettings.TimeoutSeconds is {settings.TimeoutSeconds}. It must be above 0.", nameof(settings));
            }

            // The key goes into a header, so a line break in it could add a header of its own.
            // The key itself is never named (hard rule 1).
            string apiKey = (settings.ApiKey ?? "").Trim();
            foreach (char c in apiKey)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    throw new ArgumentException(
                        "OllamaSettings.ApiKey contains a space, a line break or another control character. " +
                        "A key is one word, such as the 64 hex characters tools/ollama-server/setup.ps1 writes.",
                        nameof(settings));
                }
            }

            // Named in messages without any user name or password the address carried, since
            // messages are logged and kept in telemetry (hard rule 1).
            // Authority is the host and port alone.
            string path = chatUri.AbsolutePath;
            endpoint = chatUri.Scheme + "://" + chatUri.Authority + path.Substring(0, path.Length - "/api/chat".Length);
            if (settings.TokenAlternatives < 1 || settings.TokenAlternatives > OllamaSettings.MaxTokenAlternatives)
            {
                throw new ArgumentException(
                    $"OllamaSettings.TokenAlternatives is {settings.TokenAlternatives}. It must be from 1 to {OllamaSettings.MaxTokenAlternatives}, "
                  + "the most Ollama accepts; the default is 5.", nameof(settings));
            }

            model = settings.Model.Trim();
            contextSize = settings.ContextSize;
            temperature = settings.Temperature;
            tokenAlternatives = settings.TokenAlternatives;
            timeoutSeconds = settings.TimeoutSeconds;

            // The provider's own timeout runs on a token, so it can be told apart from the caller's
            // cancellation; the client's would throw the same exception for both.
            client = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

            // Without this, a POST can wait for a "100 Continue" before sending its body.
            client.DefaultRequestHeaders.ExpectContinue = false;

            // On every request, the decision and the lifecycle calls alike.
            sendsKey = apiKey.Length > 0;
            if (sendsKey)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            Capabilities = new ProviderCapabilities
            {
                SupportsConstrainedDecoding = true,
                NeedsExplicitPropertyOrdering = false,
                Dialect = SchemaDialect.JsonSchema,
                RateLimit = null,
                RejectsEmptyEnumValues = false,
                ReportsTokenProbabilities = true,
            };
        }

        /// <summary><c>"ollama"</c>.</summary>
        public string Name => "ollama";

        /// <summary>
        /// Constrained decoding, no explicit property ordering, the <c>JsonSchema</c> dialect, no
        /// rate limit, empty enum values not rejected, and token probabilities reported, taken
        /// before the schema's mask (checked on Ollama 0.34.2).
        /// </summary>
        public ProviderCapabilities Capabilities { get; }

        /// <summary>
        /// Sends <paramref name="req"/> to <c>/api/chat</c> and returns the model's reply, its
        /// token counts, its token probabilities and the model that answered.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The messages are composed as <see cref="DecisionRequest"/> fixes them: one
        /// <c>system</c> message holding the non-empty ones of <see cref="DecisionRequest.SystemPrompt"/>,
        /// <see cref="DecisionRequest.FewShotBlock"/> and <see cref="DecisionRequest.Observations"/>
        /// joined by a blank line, left out if all three are empty; then
        /// <see cref="DecisionRequest.History"/>, its entries alternating <c>user</c> and
        /// <c>assistant</c> from a <c>user</c> one, each exchange being the stimulus as it was sent
        /// and the decision as JSON, as <see cref="DecisionRequest.History"/> encodes it (#90); then
        /// <see cref="DecisionRequest.Stimulus"/> as the last <c>user</c> message. Nothing is
        /// rewritten, reordered or trimmed.
        /// </para>
        /// <para>
        /// The caller's cancellation aborts the HTTP request and throws
        /// <see cref="OperationCanceledException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="req"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The request has no schema, or its schema cannot be serialized
        /// (<see cref="JsonSchemaSerializer.Serialize"/>). Thrown by the call, before anything is sent.
        /// </exception>
        public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
        {
            if (req == null)
            {
                throw new ArgumentNullException(nameof(req));
            }

            if (req.Schema == null)
            {
                throw new ArgumentException("The request has no schema. A request without one is not a decision request.", nameof(req));
            }

            // Built before anything is sent, so a request that cannot be sent fails at the call.
            string body = Body(req);
            return Send(body, ct);
        }

        private async Awaitable<ProviderResult> Send(string body, CancellationToken ct)
        {
            using (var timeout = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token))
            using (var message = new HttpRequestMessage(HttpMethod.Post, chatUri))
            {
                message.Content = new StringContent(body, Utf8, "application/json");
                timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                var clock = Stopwatch.StartNew();
                int status;
                string text;

                // No ConfigureAwait(false) on these awaits: each resumes on the caller's context,
                // the main thread, so the result reaches Agent there.
                try
                {
                    using (var response = await client.SendAsync(message, linked.Token))
                    {
                        status = (int)response.StatusCode;
                        text = Utf8.GetString(await response.Content.ReadAsByteArrayAsync());
                    }
                }
                catch (Exception e) when (ct.IsCancellationRequested)
                {
                    // An aborted request can surface as some other exception; it is still the
                    // caller's cancellation.
                    throw new OperationCanceledException("The caller cancelled the request to Ollama.", e, ct);
                }
                catch (Exception e) when (timeout.IsCancellationRequested)
                {
                    throw new ProviderException(ProviderErrorKind.Timeout, Name,
                        $"Ollama at {endpoint} sent no reply within {Seconds(timeoutSeconds)} s. A model loading " +
                        "for the first time can take that long; raise OllamaSettings.TimeoutSeconds if it keeps happening.",
                        e);
                }
                catch (Exception e) when (e is HttpRequestException || e is IOException || e is System.Net.WebException
                                          || e is System.Net.Sockets.SocketException)
                {
                    throw new ProviderException(ProviderErrorKind.Unreachable, Name,
                        $"Nothing answered at {endpoint}. Is Ollama running there? Start the Ollama app or `ollama serve`. " +
                        $"({Excerpt(FirstLine(e.GetBaseException().Message))})",
                        e);
                }

                float latency = (float)clock.Elapsed.TotalSeconds;

                if (status < 200 || status > 299)
                {
                    throw Refusal(status, text);
                }

                return Read(text, latency);
            }
        }

        // ---- Model lifecycle (DR-019) -----------------------------------------------------------

        /// <summary>
        /// The models the server has pulled, from <c>/api/tags</c>, with their sizes on disk. Ollama
        /// names each with its tag, so <c>qwen3.5</c> is listed as <c>qwen3.5:latest</c>.
        /// </summary>
        /// <exception cref="ProviderException">
        /// Nothing answered (<see cref="ProviderErrorKind.Unreachable"/>), no reply in time, an HTTP
        /// error, or a reply that is not a list of models (<see cref="ProviderErrorKind.MalformedResponse"/>).
        /// </exception>
        public async Awaitable<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct)
        {
            var root = await GetObject("/api/tags", ct);
            var installed = new List<ModelInfo>();
            if (root["models"] == null || root["models"].Type == JTokenType.Null)
            {
                return installed;
            }

            if (!(root["models"] is JArray models))
            {
                throw Malformed("its /api/tags models is not a list", root.ToString(Formatting.None));
            }

            foreach (var entry in models)
            {
                if (!(entry is JObject fields) || !(fields["name"] is JValue name) || name.Type != JTokenType.String
                    || string.IsNullOrWhiteSpace((string)name))
                {
                    throw Malformed("an /api/tags entry has no name", root.ToString(Formatting.None));
                }

                installed.Add(new ModelInfo((string)name, Bytes(fields["size"])));
            }

            return installed;
        }

        /// <summary>
        /// Loads this provider's model now: an <c>/api/generate</c> naming it with no prompt, which
        /// Ollama answers once the model is in memory. A model already loaded answers at once.
        /// </summary>
        /// <remarks>
        /// It sends the same <c>num_ctx</c> as every decision (<see cref="OllamaSettings.ContextSize"/>).
        /// Ollama loads a model for one context size, and reloads it when a request asks for another,
        /// so a warm-up at the server's default would leave the first decision to load it again.
        /// </remarks>
        /// <exception cref="ProviderException">
        /// <see cref="ProviderErrorKind.ModelNotFound"/> when the server has not pulled it, and the
        /// failures <see cref="RequestAsync"/> has, within the same timeout.
        /// </exception>
        public async Awaitable WarmUpAsync(CancellationToken ct)
        {
            string body = new JObject
            {
                ["model"] = model,
                ["options"] = new JObject { ["num_ctx"] = contextSize },
            }.ToString(Formatting.None);
            string text = await Call(HttpMethod.Post, "/api/generate", body, ct);
            try
            {
                ParseObject(text);
            }
            catch (JsonException e)
            {
                throw Malformed($"its /api/generate is not one JSON object ({e.Message})", text, e);
            }
        }

        /// <summary>
        /// Unloads this provider's model now (<c>keep_alive: 0</c>), then waits until
        /// <c>/api/ps</c> no longer lists it, at most 15 s. False when it is still listed after that.
        /// </summary>
        /// <exception cref="ProviderException">
        /// <see cref="ProviderErrorKind.ModelNotFound"/> when the server has not pulled it, and the
        /// failures <see cref="RequestAsync"/> has.
        /// </exception>
        public Awaitable<bool> UnloadAsync(CancellationToken ct)
        {
            return UnloadAsync(ct, UnloadWaitSeconds);
        }

        // ---- For the evaluation runner (#145) --------------------------------------------------
        // Internal, so Runtime/Evaluation knows no Ollama address: it asks the provider, and the
        // provider knows its server. Each failure throws a ProviderException, as RequestAsync does.

        /// <summary>The model this provider asks for, as its settings name it.</summary>
        internal string Model => model;

        // What top_logprobs is sent as, for the evaluation report's conditions (#244).
        internal int TokenAlternatives => tokenAlternatives;

        /// <summary>The server's address, without any user name or password it carried.</summary>
        internal string Endpoint => endpoint;

        /// <summary>The server's own version, from <c>/api/version</c>, such as <c>0.40.14</c>.</summary>
        internal async Awaitable<string> ReadVersionAsync(CancellationToken ct)
        {
            var root = await GetObject("/api/version", ct);
            return root["version"] is JValue version && version.Type == JTokenType.String
                ? (string)version
                : throw Malformed("its /api/version has no version text", root.ToString(Formatting.None));
        }

        /// <summary>
        /// Every model the server holds in memory now, from <c>/api/ps</c>: its name, its whole size
        /// and how much of that is in GPU memory, both in bytes. Hard rule 3 is checked against it.
        /// </summary>
        internal async Awaitable<List<(string Name, long Size, long SizeVram)>> ReadResidentAsync(CancellationToken ct)
        {
            var root = await GetObject("/api/ps", ct);
            var resident = new List<(string, long, long)>();
            if (root["models"] == null || root["models"].Type == JTokenType.Null)
            {
                return resident;
            }

            if (!(root["models"] is JArray models))
            {
                throw Malformed("its /api/ps models is not a list", root.ToString(Formatting.None));
            }

            foreach (var entry in models)
            {
                if (!(entry is JObject fields) || !(fields["name"] is JValue name) || name.Type != JTokenType.String)
                {
                    throw Malformed("an /api/ps entry has no name", root.ToString(Formatting.None));
                }

                resident.Add(((string)name, Bytes(fields["size"]), Bytes(fields["size_vram"])));
            }

            return resident;
        }

        /// <summary>
        /// Asks the server to unload this provider's model now (<c>keep_alive: 0</c>), then waits
        /// until <c>/api/ps</c> no longer lists it, at most <paramref name="waitSeconds"/>. Returns
        /// false when it is still listed after that. What the public <see cref="UnloadAsync(CancellationToken)"/> calls.
        /// </summary>
        internal async Awaitable<bool> UnloadAsync(CancellationToken ct, float waitSeconds)
        {
            string body = new JObject { ["model"] = model, ["keep_alive"] = 0 }.ToString(Formatting.None);
            await Call(HttpMethod.Post, "/api/generate", body, ct);

            var clock = Stopwatch.StartNew();
            while (true)
            {
                bool listed = false;
                foreach (var resident in await ReadResidentAsync(ct))
                {
                    listed |= SameModel(resident.Name, model);
                }

                if (!listed)
                {
                    return true;
                }

                if (clock.Elapsed.TotalSeconds >= waitSeconds)
                {
                    return false;
                }

                await System.Threading.Tasks.Task.Delay(250, ct);
            }
        }

        /// <summary>
        /// True when two model names are one model as Ollama lists it: <c>qwen3.5</c> is
        /// <c>qwen3.5:latest</c>. Case is ignored, as Ollama's own names are lower case.
        /// </summary>
        internal static bool SameModel(string a, string b)
        {
            string Tagged(string n) => n == null ? "" : (n.Trim().IndexOf(':') < 0 ? n.Trim() + ":latest" : n.Trim());
            return string.Equals(Tagged(a), Tagged(b), StringComparison.OrdinalIgnoreCase);
        }

        private async Awaitable<JObject> GetObject(string path, CancellationToken ct)
        {
            string text = await Call(HttpMethod.Get, path, null, ct);
            try
            {
                return ParseObject(text);
            }
            catch (JsonException e)
            {
                throw Malformed($"its {path} is not one JSON object ({e.Message})", text, e);
            }
        }

        // One short request to another endpoint than /api/chat, failing as Send does.
        private async Awaitable<string> Call(HttpMethod method, string path, string body, CancellationToken ct)
        {
            // Beside /api/chat, so a server under a path prefix keeps it.
            string chat = chatUri.AbsoluteUri;
            var uri = new Uri(chat.Substring(0, chat.Length - "api/chat".Length) + path.TrimStart('/'));
            using (var timeout = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token))
            using (var message = new HttpRequestMessage(method, uri))
            {
                if (body != null)
                {
                    message.Content = new StringContent(body, Utf8, "application/json");
                }

                timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                int status;
                string text;
                try
                {
                    using (var response = await client.SendAsync(message, linked.Token))
                    {
                        status = (int)response.StatusCode;
                        text = Utf8.GetString(await response.Content.ReadAsByteArrayAsync());
                    }
                }
                catch (Exception e) when (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException($"The caller cancelled the request to Ollama's {path}.", e, ct);
                }
                catch (Exception e) when (timeout.IsCancellationRequested)
                {
                    throw new ProviderException(ProviderErrorKind.Timeout, Name,
                        $"Ollama at {endpoint} sent no reply to {path} within {Seconds(timeoutSeconds)} s.", e);
                }
                catch (Exception e) when (e is HttpRequestException || e is IOException || e is System.Net.WebException
                                          || e is System.Net.Sockets.SocketException)
                {
                    throw new ProviderException(ProviderErrorKind.Unreachable, Name,
                        $"Nothing answered at {endpoint}. Is Ollama running there? " +
                        $"({Excerpt(FirstLine(e.GetBaseException().Message))})", e);
                }

                if (status < 200 || status > 299)
                {
                    throw Refusal(status, text);
                }

                return text;
            }
        }

        // A byte count /api/ps may leave out, which then reads as 0.
        private static long Bytes(JToken token)
        {
            return token != null && token.Type == JTokenType.Integer ? Math.Max(0L, (long)token) : 0L;
        }

        // The request body, its fields in the order the issue tables them (#88).
        private string Body(DecisionRequest req)
        {
            string format = serializer.Serialize(req.Schema);

            var output = new StringWriter(CultureInfo.InvariantCulture);
            using (var json = new JsonTextWriter(output) { Formatting = Formatting.None })
            {
                json.WriteStartObject();
                json.WritePropertyName("model");
                json.WriteValue(model);

                json.WritePropertyName("messages");
                json.WriteStartArray();

                string system = SystemSide(req);
                if (system.Length > 0)
                {
                    Message(json, "system", system);
                }

                var history = req.History ?? Array.Empty<string>();
                for (int i = 0; i < history.Count; i++)
                {
                    Message(json, i % 2 == 0 ? "user" : "assistant", history[i]);
                }

                Message(json, "user", req.Stimulus);
                json.WriteEndArray();

                // An object, not a string: Ollama compiles it into the sampling grammar. Written
                // as the serializer produced it, so the grammar is exactly #9's output.
                json.WritePropertyName("format");
                json.WriteRawValue(format);

                json.WritePropertyName("stream");
                json.WriteValue(false);

                // Qwen-family reasoning adds seconds and no accuracy here.
                json.WritePropertyName("think");
                json.WriteValue(false);

                json.WritePropertyName("options");
                json.WriteStartObject();
                json.WritePropertyName("temperature");
                json.WriteValue(req.Temperature ?? temperature);
                json.WritePropertyName("num_ctx");
                json.WriteValue(contextSize);

                // Only when the call set them, so a call that did not sends what it always sent
                // (#266 item 7, #244 item 11).
                if (req.Seed.HasValue)
                {
                    json.WritePropertyName("seed");
                    json.WriteValue(req.Seed.Value);
                }

                if (req.MaxReplyTokens.HasValue)
                {
                    json.WritePropertyName("num_predict");
                    json.WriteValue(req.MaxReplyTokens.Value);
                }

                json.WriteEndObject();

                // For the grounding guard (DR-016).
                json.WritePropertyName("logprobs");
                json.WriteValue(true);
                json.WritePropertyName("top_logprobs");
                json.WriteValue(tokenAlternatives);

                json.WriteEndObject();
            }

            return output.ToString();
        }

        // DecisionRequest's rule: the non-empty system-side blocks, in order, joined by a blank line.
        private static string SystemSide(DecisionRequest req)
        {
            var blocks = new List<string>(3);
            foreach (string block in new[] { req.SystemPrompt, req.FewShotBlock, req.Observations })
            {
                if (!string.IsNullOrEmpty(block))
                {
                    blocks.Add(block);
                }
            }

            return string.Join("\n\n", blocks);
        }

        private static void Message(JsonWriter json, string role, string content)
        {
            json.WriteStartObject();
            json.WritePropertyName("role");
            json.WriteValue(role);
            json.WritePropertyName("content");
            json.WriteValue(content ?? "");
            json.WriteEndObject();
        }

        // Any reply outside 2xx. Ollama puts its reason in an "error" field.
        private ProviderException Refusal(int status, string text)
        {
            string error = ErrorField(text);
            string said = Excerpt(error ?? text);

            // Ollama 0.34.2 answers a model it lacks with 404 and {"error":"model 'x' not found"}.
            // A 404 with no such field is a server that is not Ollama, or the wrong address.
            if (status == 404 && error != null && error.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new ProviderException(ProviderErrorKind.ModelNotFound, Name,
                    $"Ollama at {endpoint} does not have the model '{model}'. Run `ollama pull {model}`. Ollama said: {said}");
            }

            if (status >= 500)
            {
                return new ProviderException(ProviderErrorKind.ServerError, Name,
                    $"Ollama at {endpoint} failed with HTTP {status}: {said}");
            }

            // 401 is the gateway in tools/ollama-server/ wanting a key; Ollama itself never sends it.
            string hint = status == 404 ? " Is that address an Ollama server?"
                : status == 401 && sendsKey ? " The server refused this provider's key: OllamaSettings.ApiKey (an " +
                                              "OllamaProviderConfig's API Key Variable) must hold the one the server expects."
                : status == 401 ? " The server wants a key: set OllamaSettings.ApiKey, or an OllamaProviderConfig's API Key Variable."
                : "";
            return new ProviderException(ProviderErrorKind.Rejected, Name,
                $"Ollama at {endpoint} refused the request with HTTP {status}: {said}{hint}");
        }

        // Ollama's chat envelope, read onto a result. Text is message.content exactly as received.
        private ProviderResult Read(string text, float latency)
        {
            JObject root;
            try
            {
                root = ParseObject(text);
            }
            catch (JsonException e)
            {
                throw Malformed($"it is not one JSON object ({e.Message})", text, e);
            }

            if (!(root["message"] is JObject message) || !(message["content"] is JValue content)
                || content.Type != JTokenType.String)
            {
                throw Malformed("it has no message.content text", text);
            }

            // The model the server says answered; the one asked for if it does not say.
            string answered = root["model"] is JValue name && name.Type == JTokenType.String ? (string)name : model;

            return new ProviderResult
            {
                Text = (string)content,
                LatencySeconds = latency,
                PromptTokens = Count(root, "prompt_eval_count", text),
                CompletionTokens = Count(root, "eval_count", text),
                TokenProbabilities = Probabilities(root["logprobs"], text),
                Model = answered,
            };
        }

        // A count Ollama may leave out, which then reads as 0.
        private int Count(JObject root, string field, string text)
        {
            var token = root[field];
            if (token == null || token.Type == JTokenType.Null)
            {
                return 0;
            }

            if (token.Type != JTokenType.Integer)
            {
                throw Malformed($"its {field} is not a whole number", text);
            }

            long count;
            try
            {
                count = (long)token;
            }
            catch (OverflowException)
            {
                count = -1;
            }

            if (count < 0 || count > int.MaxValue)
            {
                throw Malformed($"its {field} is negative or too large to be a token count", text);
            }

            return (int)count;
        }

        // Each logprobs entry onto one TokenProbability: token to Text, logprob to LogProbability,
        // top_logprobs to Alternatives (providers.md). No logprobs field means none reported, null.
        private List<TokenProbability> Probabilities(JToken logprobs, string text)
        {
            if (logprobs == null || logprobs.Type == JTokenType.Null)
            {
                return null;
            }

            if (!(logprobs is JArray entries))
            {
                throw Malformed("its logprobs is not a list", text);
            }

            var tokens = new List<TokenProbability>(entries.Count);
            foreach (var entry in entries)
            {
                var token = new TokenProbability();
                ReadToken(entry, text, out token.Text, out token.LogProbability);

                var top = entry["top_logprobs"];
                if (top != null && top.Type != JTokenType.Null)
                {
                    if (!(top is JArray alternatives))
                    {
                        throw Malformed("a logprobs entry's top_logprobs is not a list", text);
                    }

                    var list = new List<TokenAlternative>(alternatives.Count);
                    foreach (var alternative in alternatives)
                    {
                        var read = new TokenAlternative();
                        ReadToken(alternative, text, out read.Text, out read.LogProbability);
                        list.Add(read);
                    }

                    token.Alternatives = list;
                }

                tokens.Add(token);
            }

            return tokens;
        }

        private void ReadToken(JToken entry, string text, out string token, out float logProbability)
        {
            if (!(entry is JObject fields)
                || !(fields["token"] is JValue tokenValue) || tokenValue.Type != JTokenType.String
                || !(fields["logprob"] is JValue logprob)
                || (logprob.Type != JTokenType.Float && logprob.Type != JTokenType.Integer))
            {
                throw Malformed("a logprobs entry lacks its token or logprob", text);
            }

            token = (string)tokenValue;
            logProbability = (float)(double)logprob;
        }

        private ProviderException Malformed(string why, string text, Exception inner = null)
        {
            return new ProviderException(ProviderErrorKind.MalformedResponse, Name,
                $"Ollama at {endpoint} answered, but {why}. The reply began: {Excerpt(text)}", inner);
        }

        // One JSON object and nothing after it. Strings stay as written: by default Newtonsoft
        // turns a string that looks like a date into a DateTime, and a reply must arrive exactly.
        private static JObject ParseObject(string text)
        {
            using (var reader = new JsonTextReader(new StringReader(text)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;

                var root = JObject.Load(reader);
                if (reader.Read())
                {
                    throw new JsonReaderException("There is more after the object.");
                }

                return root;
            }
        }

        // The "error" text of an Ollama refusal, or null when the body has none.
        private static string ErrorField(string text)
        {
            try
            {
                return ParseObject(text)["error"] is JValue error && error.Type == JTokenType.String ? (string)error : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // The transport's own words up to the first line break. On Windows, Mono's socket message
        // ends in a line break and a run of NUL characters, which would reach the log and telemetry.
        private static string FirstLine(string text)
        {
            int end = text == null ? -1 : text.IndexOfAny(new[] { '\r', '\n', '\0' });
            return end < 0 ? text : text.Substring(0, end);
        }

        private static string Excerpt(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "(nothing)";
            }

            text = text.Trim();
            return text.Length <= Longest ? text : text.Substring(0, Longest) + "...";
        }

        private static string Seconds(float seconds)
        {
            return seconds.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
