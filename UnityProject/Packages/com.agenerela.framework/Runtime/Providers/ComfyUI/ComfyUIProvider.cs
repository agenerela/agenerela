using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Agenerela.Providers.ComfyUI
{
    /// <summary>
    /// An <see cref="IImageProvider"/> that runs a workflow on a ComfyUI server (DR-025): posts it to
    /// <c>/prompt</c> with the request written in, waits on <c>/history</c>, and downloads the first
    /// picture it saved from <c>/view</c>. The same workflow files and the same parameter names as
    /// <c>tools/comfyui/comfy.py</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Isolated</b> in its own assembly, <c>Agenerela.ComfyUI</c>, which references only the core
    /// and Newtonsoft (hard rule 6): a game that wants it references that assembly.
    /// </para>
    /// <para>
    /// <b>The key</b> goes only in the <c>Authorization</c> header; the address in messages is its
    /// scheme, host and port, and any server text quoted has the key replaced (hard rule 1).
    /// </para>
    /// <para>
    /// <b>Failures</b> are results (<see cref="IImageProvider"/>): nothing answering is
    /// <see cref="ProviderErrorKind.Unreachable"/>; a workflow naming a model the server lacks is
    /// <see cref="ProviderErrorKind.ModelNotFound"/>; a refused key, route or workflow is
    /// <see cref="ProviderErrorKind.Rejected"/>; a busy card (HTTP 503), a gateway with no ComfyUI
    /// behind it (502) or a node that failed is <see cref="ProviderErrorKind.ServerError"/>; no
    /// picture within <see cref="ComfyUISettings.TimeoutSeconds"/> is
    /// <see cref="ProviderErrorKind.Timeout"/>; and a reply that is not what ComfyUI sends, or a
    /// download that is not a PNG, is <see cref="ProviderErrorKind.MalformedResponse"/>.
    /// </para>
    /// <para>
    /// A job that timed out or was cancelled is taken off the server's queue if it is still waiting
    /// there. One already running finishes on the server: interrupting it could stop another
    /// client's job on a shared card.
    /// </para>
    /// </remarks>
    public sealed class ComfyUIProvider : IImageProvider
    {
        private const int Longest = 300;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private static readonly string[] Named = { "prompt", "seed", "width", "height" };

        private readonly HttpClient client;
        private readonly Uri baseUri;
        private readonly string endpoint;
        private readonly string apiKey;
        private readonly JObject graph;
        private readonly JObject parameters;
        private readonly Dictionary<string, string> defaults;
        private readonly float timeoutSeconds;
        private readonly float pollSeconds;
        private readonly bool freeAfterEach;
        private readonly System.Random seeds = new System.Random();

        /// <summary>A provider for the server and workflow <paramref name="settings"/> name.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The URL is not an absolute http or https address without a query, the key has a space or
        /// a control character in it, the workflow is not JSON in one of the two forms, or a timing
        /// is out of range.
        /// </exception>
        public ComfyUIProvider(ComfyUISettings settings)
            : this(settings, new HttpClientHandler())
        {
        }

        // The seam EditMode tests use: a handler that answers as ComfyUI would.
        internal ComfyUIProvider(ComfyUISettings settings, HttpMessageHandler handler)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            string trimmed = (settings.Url ?? "").Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed + "/", UriKind.Absolute, out baseUri)
                || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
                || baseUri.Query.Length > 0 || baseUri.Fragment.Length > 0)
            {
                // The address itself is left out: it may carry a password (hard rule 1).
                throw new ArgumentException("ComfyUISettings.Url is not an http address with no query, such as http://127.0.0.1:8188.", nameof(settings));
            }

            endpoint = baseUri.Scheme + "://" + baseUri.Authority + baseUri.AbsolutePath.TrimEnd('/');

            apiKey = (settings.ApiKey ?? "").Trim();
            if (apiKey.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            {
                throw new ArgumentException("ComfyUISettings.ApiKey contains a space, a line break or another control character. A key is one word.", nameof(settings));
            }

            if (!(settings.TimeoutSeconds > 0f) || settings.TimeoutSeconds > int.MaxValue / 1000f)
            {
                throw new ArgumentException($"ComfyUISettings.TimeoutSeconds is {settings.TimeoutSeconds}. It must be above 0.", nameof(settings));
            }

            if (!(settings.PollSeconds >= 0f) || settings.PollSeconds > 60f)
            {
                throw new ArgumentException($"ComfyUISettings.PollSeconds is {settings.PollSeconds}. It must be from 0 to 60.", nameof(settings));
            }

            (graph, parameters) = ReadWorkflow(settings.WorkflowJson);
            defaults = new Dictionary<string, string>(settings.DefaultParameters ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            foreach (var pair in defaults)
            {
                // Checked now, so a mistyped default fails when the game starts, not at the first picture.
                Target(pair.Key);
            }

            timeoutSeconds = settings.TimeoutSeconds;
            pollSeconds = settings.PollSeconds;
            freeAfterEach = settings.FreeAfterEach;

            client = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.ExpectContinue = false;
            if (apiKey.Length > 0)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            Capabilities = new ImageCapabilities { SizeStep = 16, MaxSide = 2048, HonoursSeed = parameters.ContainsKey("seed") };
        }

        /// <summary><c>"comfyui"</c>.</summary>
        public string Name => "comfyui";

        /// <summary>Sides a multiple of 16 up to 2048; a seed honoured when the workflow has a <c>seed</c> param; no edits.</summary>
        public ImageCapabilities Capabilities { get; }

        /// <summary>The server's address as messages name it: scheme, host, port and path, nothing else.</summary>
        public string Endpoint => endpoint;

        /// <summary>
        /// The workflow graph this request would send, with every value written in: what
        /// <see cref="GenerateAsync"/> posts. For a check or a log; it contains no key.
        /// </summary>
        /// <exception cref="ArgumentException">As <see cref="GenerateAsync"/>.</exception>
        public JObject Graph(ImageRequest request, out long? seed)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.IsEdit || request.Mask != null)
            {
                // DR-028: no workflow here takes an input picture yet. One with a LoadImage param,
                // the picture sent through /upload/image, is how ComfyUI would edit.
                throw new ArgumentException(
                    "ComfyUIProvider does not edit pictures: the request has Images or a Mask, and the workflow it runs takes no " +
                    "input picture (ImageCapabilities.Edits is false). Send it with no Images for a new picture, or use a backend " +
                    "that edits, such as OpenAIImagesProvider on sd-server.", nameof(request));
            }

            string why = Capabilities.WhyNot(request);
            if (why != null)
            {
                throw new ArgumentException(why, nameof(request));
            }

            var built = (JObject)graph.DeepClone();
            seed = request.Seed;
            if (seed == null && parameters.ContainsKey("seed"))
            {
                // As comfy.py does: a fresh seed under 2^50, reported so the picture can be made again.
                seed = (long)(seeds.NextDouble() * (1L << 50));
            }

            var values = new List<(string Name, string Value)>();
            if (parameters.ContainsKey("prompt"))
            {
                values.Add(("prompt", request.Prompt ?? ""));
            }

            if (seed.HasValue && parameters.ContainsKey("seed"))
            {
                values.Add(("seed", seed.Value.ToString(CultureInfo.InvariantCulture)));
            }

            if (parameters.ContainsKey("width"))
            {
                values.Add(("width", request.Width.ToString(CultureInfo.InvariantCulture)));
            }

            if (parameters.ContainsKey("height"))
            {
                values.Add(("height", request.Height.ToString(CultureInfo.InvariantCulture)));
            }

            var own = request.Parameters ?? new Dictionary<string, string>();
            foreach (var pair in defaults.Where(d => !own.ContainsKey(d.Key)))
            {
                values.Add((pair.Key, pair.Value));
            }

            foreach (var pair in own)
            {
                if (Array.IndexOf(Named, pair.Key) >= 0)
                {
                    throw new ArgumentException($"The parameter '{pair.Key}' is set by the request's own field; set that instead.", nameof(request));
                }

                values.Add((pair.Key, pair.Value));
            }

            foreach (var (name, value) in values)
            {
                foreach (var (node, input) in Target(name))
                {
                    var inputs = (JObject)built[node]["inputs"];
                    inputs[input] = Coerce(name, value, inputs[input]);
                }
            }

            return built;
        }

        /// <summary>Runs the workflow for <paramref name="request"/> and downloads its picture.</summary>
        /// <exception cref="ArgumentException">
        /// The request is an edit, which ComfyUI does not do here (DR-028), the size is not a multiple
        /// of 16 or is over 2048, or a parameter names no param or
        /// <c>node.input</c> of the workflow, names an input wired to another node, or has a value
        /// that input cannot take.
        /// </exception>
        public Awaitable<ImageResult> GenerateAsync(ImageRequest request, CancellationToken ct)
        {
            // Built before anything is sent, so a request that cannot be sent fails at the call.
            var built = Graph(request, out long? seed);
            return Run(built, seed, ct);
        }

        /// <summary>
        /// Asks the server to unload its models and free the card's memory (<c>/free</c>), for a batch
        /// run with <see cref="ComfyUISettings.FreeAfterEach"/> off. True when the server took it.
        /// </summary>
        public async Awaitable<bool> FreeAsync(CancellationToken ct)
        {
            try
            {
                var (status, _) = await Send(HttpMethod.Post, "free", "{\"unload_models\":true,\"free_memory\":true}", ct, 30f);
                return status >= 200 && status <= 299;
            }
            catch (ComfyFailure)
            {
                return false;
            }
        }

        private async Awaitable<ImageResult> Run(JObject built, long? seed, CancellationToken ct)
        {
            var clock = Stopwatch.StartNew();
            using (var deadline = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                string promptId = null;
                try
                {
                    var body = new JObject { ["prompt"] = built, ["client_id"] = Guid.NewGuid().ToString("N") };
                    var (status, text) = await Send(HttpMethod.Post, "prompt", body.ToString(Formatting.None), linked.Token, null);
                    if (status < 200 || status > 299)
                    {
                        return Refusal(status, text, "/prompt", clock);
                    }

                    promptId = (string)Parse(text, "/prompt")["prompt_id"];
                    if (string.IsNullOrEmpty(promptId))
                    {
                        throw new ComfyFailure(ProviderErrorKind.MalformedResponse, $"ComfyUI at {endpoint} accepted the workflow but gave no prompt_id. It said: {Excerpt(text)}");
                    }

                    JObject entry = await WaitFor(promptId, linked.Token, clock);
                    string failed = ExecutionError(entry);
                    if (failed != null)
                    {
                        promptId = null; // finished: nothing to take off the queue
                        return Fail(ProviderErrorKind.ServerError, $"ComfyUI at {endpoint} ran the workflow, but {failed}", clock);
                    }

                    var item = FirstImage(entry);
                    promptId = null;
                    if (item == null)
                    {
                        return Fail(ProviderErrorKind.MalformedResponse, $"ComfyUI at {endpoint} finished the job but it saved no picture. Does the workflow end in SaveImage?", clock);
                    }

                    string query = "view?filename=" + Uri.EscapeDataString((string)item["filename"] ?? "")
                                   + "&subfolder=" + Uri.EscapeDataString((string)item["subfolder"] ?? "")
                                   + "&type=" + Uri.EscapeDataString((string)item["type"] ?? "output");
                    var (viewStatus, bytes) = await SendBytes(HttpMethod.Get, query, linked.Token);
                    if (viewStatus < 200 || viewStatus > 299)
                    {
                        return Refusal(viewStatus, Utf8.GetString(bytes ?? Array.Empty<byte>()), "/view", clock);
                    }

                    if (!ImageResult.IsPng(bytes))
                    {
                        return Fail(ProviderErrorKind.MalformedResponse, $"ComfyUI at {endpoint} sent a file that is not a PNG for {(string)item["filename"]}.", clock);
                    }

                    if (freeAfterEach)
                    {
                        await FreeAsync(ct);
                    }

                    return ImageResult.Made(bytes, Name, (float)clock.Elapsed.TotalSeconds, seed, GenerateSeconds(entry));
                }
                catch (ComfyFailure e)
                {
                    return Fail(e.Kind, e.Message, clock);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    await Withdraw(promptId);
                    throw new OperationCanceledException("The caller cancelled the picture.", ct);
                }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested)
                {
                    await Withdraw(promptId);
                    return Fail(ProviderErrorKind.Timeout,
                        $"ComfyUI at {endpoint} made no picture within {Seconds(timeoutSeconds)} s. The card may be busy with another job; " +
                        "raise ComfyUISettings.TimeoutSeconds if it keeps happening.", clock);
                }
            }
        }

        private async Awaitable<JObject> WaitFor(string promptId, CancellationToken ct, Stopwatch clock)
        {
            while (true)
            {
                var (status, text) = await Send(HttpMethod.Get, "history/" + Uri.EscapeDataString(promptId), null, ct, null);
                if (status < 200 || status > 299)
                {
                    var refusal = Refusal(status, text, "/history", clock);
                    throw new ComfyFailure(refusal.Error.Value, refusal.ErrorMessage);
                }

                // ComfyUI writes the job into its history once it has finished or failed.
                if (Parse(text, "/history")[promptId] is JObject entry)
                {
                    return entry;
                }

                if (pollSeconds > 0f)
                {
                    await Task.Delay(TimeSpan.FromSeconds(pollSeconds), ct);
                }
                else
                {
                    await Task.Yield();
                    ct.ThrowIfCancellationRequested();
                }
            }
        }

        // A job given up on is taken off the queue if it is still waiting; best effort, never thrown.
        private async Awaitable Withdraw(string promptId)
        {
            if (promptId == null)
            {
                return;
            }

            try
            {
                await Send(HttpMethod.Post, "queue", new JObject { ["delete"] = new JArray(promptId) }.ToString(Formatting.None), CancellationToken.None, 10f);
            }
            catch (Exception)
            {
                // A gateway may not expose /queue; the job then runs to its end on the server.
            }
        }

        private async Awaitable<(int Status, string Text)> Send(HttpMethod method, string path, string body, CancellationToken ct, float? ownTimeout)
        {
            var (status, bytes) = await SendBytes(method, path, ct, body, ownTimeout);
            return (status, Utf8.GetString(bytes ?? Array.Empty<byte>()));
        }

        private async Awaitable<(int Status, byte[] Bytes)> SendBytes(HttpMethod method, string path, CancellationToken ct, string body = null, float? ownTimeout = null)
        {
            using (var own = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, own.Token))
            using (var message = new HttpRequestMessage(method, new Uri(baseUri, path)))
            {
                if (ownTimeout.HasValue)
                {
                    own.CancelAfter(TimeSpan.FromSeconds(ownTimeout.Value));
                }

                if (body != null)
                {
                    message.Content = new StringContent(body, Utf8, "application/json");
                }

                try
                {
                    using (var response = await client.SendAsync(message, linked.Token))
                    {
                        return ((int)response.StatusCode, await response.Content.ReadAsByteArrayAsync());
                    }
                }
                catch (Exception e) when (ct.IsCancellationRequested)
                {
                    // An aborted request can surface as another exception; it is still a cancellation.
                    throw new OperationCanceledException("The request to ComfyUI was cancelled.", e, ct);
                }
                catch (Exception e) when (own.IsCancellationRequested)
                {
                    throw new ComfyFailure(ProviderErrorKind.Timeout, $"ComfyUI at {endpoint} did not answer /{path} in time.", e);
                }
                catch (Exception e) when (e is HttpRequestException || e is IOException || e is System.Net.WebException
                                          || e is System.Net.Sockets.SocketException)
                {
                    throw new ComfyFailure(ProviderErrorKind.Unreachable,
                        $"Nothing answered at {endpoint}. Is the server on, and is this machine on its network? ({Excerpt(FirstLine(e.GetBaseException().Message))})", e);
                }
            }
        }

        private ImageResult Refusal(int status, string text, string path, Stopwatch clock)
        {
            string said = Excerpt(text);
            switch (status)
            {
                case 400 when path == "/prompt":
                    string errors = PromptErrors(text, out bool missingModel);
                    return Fail(missingModel ? ProviderErrorKind.ModelNotFound : ProviderErrorKind.Rejected,
                        $"ComfyUI at {endpoint} refused the workflow{(missingModel ? ": it names a model the server does not have" : "")}. {errors}", clock);
                case 401:
                    return Fail(ProviderErrorKind.Rejected, $"The server at {endpoint} refused the key (HTTP 401). Check the key's environment variable holds this client's key.", clock);
                case 403:
                    return Fail(ProviderErrorKind.Rejected, $"The server at {endpoint} does not let this client reach {path} (HTTP 403).", clock);
                case 502:
                    return Fail(ProviderErrorKind.ServerError, $"The gateway at {endpoint} is up, but ComfyUI behind it is not answering (HTTP 502). It may be starting.", clock);
                case 503:
                    return Fail(ProviderErrorKind.ServerError, $"The server at {endpoint} is busy (HTTP 503): {said}", clock);
            }

            return Fail(status >= 500 ? ProviderErrorKind.ServerError : ProviderErrorKind.Rejected,
                $"ComfyUI at {endpoint} answered {path} with HTTP {status}: {said}", clock);
        }

        private ImageResult Fail(ProviderErrorKind kind, string message, Stopwatch clock)
        {
            return ImageResult.Failed(kind, Name, Redact(message), (float)clock.Elapsed.TotalSeconds);
        }

        private string Redact(string text)
        {
            return apiKey.Length == 0 || text == null ? text : text.Replace(apiKey, "<redacted>");
        }

        // ---- The workflow ------------------------------------------------------------------------

        private static (JObject Graph, JObject Params) ReadWorkflow(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("ComfyUISettings.WorkflowJson is empty. Give the workflow's JSON, such as tools/comfyui/workflows/image.json.", nameof(json));
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException e)
            {
                throw new ArgumentException($"ComfyUISettings.WorkflowJson is not one JSON object ({e.Message}). Export the workflow with Export (API).", nameof(json));
            }

            var graph = root["prompt"] as JObject ?? root;
            var prms = root["prompt"] is JObject ? root["params"] as JObject ?? new JObject() : new JObject();
            if (!graph.Properties().Any() || graph.Properties().Any(p => !(p.Value is JObject node) || !(node["inputs"] is JObject) || node["class_type"] == null))
            {
                throw new ArgumentException("ComfyUISettings.WorkflowJson is not a ComfyUI API graph: each node needs a class_type and inputs. Use Export (API), not Save.", nameof(json));
            }

            foreach (var p in prms.Properties())
            {
                if (!(p.Value is JObject spec) || !(spec["targets"] is JArray targets) || targets.Count == 0)
                {
                    throw new ArgumentException($"The workflow's param '{p.Name}' has no targets.", nameof(json));
                }

                foreach (var t in targets)
                {
                    if (!(t is JArray pair) || pair.Count != 2 || !(graph[(string)pair[0]]?["inputs"] is JObject inputs) || inputs[(string)pair[1]] == null)
                    {
                        throw new ArgumentException($"The workflow's param '{p.Name}' points at an input the graph does not have.", nameof(json));
                    }
                }
            }

            return (graph, prms);
        }

        // The inputs a parameter name writes: a declared param's targets, or one node.input.
        private List<(string Node, string Input)> Target(string name)
        {
            if (parameters[name] is JObject spec)
            {
                return spec["targets"].Select(t => ((string)t[0], (string)t[1])).ToList();
            }

            int dot = name?.IndexOf('.') ?? -1;
            if (dot > 0 && graph[name.Substring(0, dot)]?["inputs"] is JObject inputs && inputs[name.Substring(dot + 1)] != null)
            {
                var input = inputs[name.Substring(dot + 1)];
                if (input is JArray)
                {
                    throw new ArgumentException($"The parameter '{name}' is wired to another node in the workflow and cannot be set.");
                }

                return new List<(string, string)> { (name.Substring(0, dot), name.Substring(dot + 1)) };
            }

            string known = string.Join(", ", parameters.Properties().Select(p => p.Name));
            throw new ArgumentException($"The workflow has no parameter '{name}'. It takes: {(known.Length > 0 ? known : "node_id.input only")}.");
        }

        // A value as the type the workflow already holds there, as comfy.py does.
        private static JToken Coerce(string name, string raw, JToken current)
        {
            raw = raw ?? "";
            switch (current?.Type)
            {
                case JTokenType.Integer:
                    if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole))
                    {
                        return whole;
                    }

                    break;
                case JTokenType.Float:
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                    {
                        return number;
                    }

                    break;
                case JTokenType.Boolean:
                    if (bool.TryParse(raw, out bool flag))
                    {
                        return flag;
                    }

                    break;
                default:
                    return raw;
            }

            throw new ArgumentException($"The parameter '{name}' takes a {current.Type.ToString().ToLowerInvariant()}, not '{Excerpt(raw)}'.");
        }

        // ---- Reading replies --------------------------------------------------------------------

        private JObject Parse(string text, string path)
        {
            try
            {
                using (var reader = new JsonTextReader(new StringReader(text ?? "")) { DateParseHandling = DateParseHandling.None })
                {
                    if (JToken.ReadFrom(reader) is JObject o)
                    {
                        return o;
                    }
                }
            }
            catch (JsonException)
            {
            }

            throw new ComfyFailure(ProviderErrorKind.MalformedResponse, $"ComfyUI at {endpoint} answered {path} with something that is not a JSON object: {Excerpt(text)}");
        }

        private string PromptErrors(string text, out bool missingModel)
        {
            missingModel = false;
            JObject data;
            try
            {
                data = JObject.Parse(text ?? "");
            }
            catch (JsonException)
            {
                return Excerpt(text);
            }

            var lines = new List<string>();
            if (data["error"] is JObject error)
            {
                lines.Add(((string)error["message"] + " " + (string)error["details"]).Trim());
            }

            if (data["node_errors"] is JObject nodes)
            {
                foreach (var node in nodes.Properties())
                {
                    foreach (var e in node.Value["errors"] as JArray ?? new JArray())
                    {
                        string type = (string)e["type"] ?? "";
                        string details = (string)e["details"] ?? "";
                        if (type == "value_not_in_list" && System.Text.RegularExpressions.Regex.IsMatch(details, @"\.(safetensors|ckpt|pt|pth|bin|gguf)\b"))
                        {
                            missingModel = true;
                        }

                        lines.Add($"node {node.Name} ({(string)node.Value["class_type"]}): {(string)e["message"]} {details}".Trim());
                    }
                }
            }

            return Excerpt(lines.Count > 0 ? string.Join("; ", lines) : text);
        }

        private static string ExecutionError(JObject entry)
        {
            var status = entry["status"] as JObject;
            if (status == null || (string)status["status_str"] != "error")
            {
                return null;
            }

            foreach (var m in status["messages"] as JArray ?? new JArray())
            {
                string kind = (string)m[0];
                var data = m[1] as JObject ?? new JObject();
                if (kind == "execution_error")
                {
                    return $"node {(string)data["node_id"]} ({(string)data["node_type"]}) failed: {(string)data["exception_type"]}: {Excerpt(((string)data["exception_message"] ?? "").Trim())}";
                }

                if (kind == "execution_interrupted")
                {
                    return "the job was interrupted on the server.";
                }
            }

            return "the job failed without a message.";
        }

        private static JObject FirstImage(JObject entry)
        {
            foreach (var output in (entry["outputs"] as JObject ?? new JObject()).Properties())
            {
                foreach (var list in (output.Value as JObject ?? new JObject()).Properties())
                {
                    foreach (var item in list.Value as JArray ?? new JArray())
                    {
                        if (item is JObject o && (string)o["type"] == "output"
                            && ((string)o["filename"] ?? "").EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            return o;
                        }
                    }
                }
            }

            return null;
        }

        // From the job's own messages: execution_start to execution_success, in milliseconds.
        private static float? GenerateSeconds(JObject entry)
        {
            long? start = null, end = null;
            foreach (var m in entry["status"]?["messages"] as JArray ?? new JArray())
            {
                string kind = (string)m[0];
                var stamp = m[1]?["timestamp"];
                if (stamp == null || stamp.Type != JTokenType.Integer)
                {
                    continue;
                }

                if (kind == "execution_start")
                {
                    start = (long)stamp;
                }
                else if (kind == "execution_success")
                {
                    end = (long)stamp;
                }
            }

            return start.HasValue && end.HasValue && end >= start ? (end.Value - start.Value) / 1000f : (float?)null;
        }

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

        private static string Seconds(float seconds) => seconds.ToString("0.###", CultureInfo.InvariantCulture);

        // A failure found deep in a call, carried up to Run, which turns it into a result.
        private sealed class ComfyFailure : Exception
        {
            public ComfyFailure(ProviderErrorKind kind, string message, Exception inner = null)
                : base(message, inner)
            {
                Kind = kind;
            }

            public ProviderErrorKind Kind { get; }
        }
    }
}
