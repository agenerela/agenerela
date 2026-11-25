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

namespace Agenerela.Providers.OpenAIImages
{
    /// <summary>
    /// An <see cref="IImageProvider"/> for any server that speaks OpenAI's images API (DR-028): a new
    /// picture through <c>/images/generations</c>, an edit through <c>/images/edits</c>. It works with
    /// stable-diffusion.cpp's <c>sd-server</c>, LocalAI (new pictures only) and OpenAI itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Isolated</b> in its own assembly, <c>Agenerela.OpenAIImages</c>, which references only the
    /// core and Newtonsoft (hard rule 6): a game that wants it references that assembly.
    /// </para>
    /// <para>
    /// <b>What is sent.</b> A new picture is JSON: <c>prompt</c>, <c>size</c> as <c>WxH</c>,
    /// <c>n</c> = 1, <c>output_format</c> = <c>png</c>, <c>model</c> when set, and each request
    /// parameter as a field of its own name. An edit is multipart: the same fields, the pictures as
    /// <c>image</c> (one) or <c>image[]</c> (several), and <c>mask</c>. The picture comes back from
    /// <c>data[0].b64_json</c>; a server that answers with a URL instead is told to send base64.
    /// </para>
    /// <para>
    /// <b>Seeds and steps.</b> OpenAI's API has neither. With
    /// <see cref="OpenAIImagesSettings.SdCppExtraArgs"/> on, the seed, <c>strength</c>,
    /// <c>negative_prompt</c> and every parameter named <c>extra.</c>&lt;path&gt; (such as
    /// <c>extra.sample_params.sample_steps</c>) go into the <c>&lt;sd_cpp_extra_args&gt;</c> block
    /// that <c>sd-server</c> reads from the end of the prompt, and the seed is honoured.
    /// </para>
    /// <para>
    /// <b>Failures</b> are results (<see cref="IImageProvider"/>): nothing answering is
    /// <see cref="ProviderErrorKind.Unreachable"/>; a model the server lacks is
    /// <see cref="ProviderErrorKind.ModelNotFound"/>; a refused key, route or request (a content
    /// refusal included) is <see cref="ProviderErrorKind.Rejected"/>; a server still busy (HTTP 429 or
    /// 503) after <see cref="OpenAIImagesSettings.Retry"/> is
    /// <see cref="ProviderErrorKind.RateLimited"/>; another 5xx is
    /// <see cref="ProviderErrorKind.ServerError"/>; no answer in
    /// <see cref="OpenAIImagesSettings.TimeoutSeconds"/> is <see cref="ProviderErrorKind.Timeout"/>;
    /// and a reply with no PNG in it is <see cref="ProviderErrorKind.MalformedResponse"/>.
    /// </para>
    /// <para>
    /// <b>The key</b> goes only in the <c>Authorization</c> header, messages name the address as its
    /// scheme, host, port and path, and server text quoted has the key replaced (hard rule 1).
    /// </para>
    /// </remarks>
    public sealed class OpenAIImagesProvider : IImageProvider
    {
        private const int Longest = 300;
        private const string ExtraPrefix = "extra.";
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private static readonly string[] Reserved = { "prompt", "size", "n", "model", "image", "image[]", "mask", "output_format" };

        private readonly HttpClient client;
        private readonly Uri baseUri;
        private readonly string endpoint;
        private readonly string apiKey;
        private readonly string model;
        private readonly bool extraArgs;
        private readonly Dictionary<string, string> defaults;
        private readonly float timeoutSeconds;
        private readonly RetryPolicy retry;
        private readonly System.Random random = new System.Random();

        /// <summary>A provider for the server <paramref name="settings"/> names.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The URL is not an absolute http or https address without a query, the key has a space or a
        /// control character in it, a default parameter cannot be sent, or a number is out of range.
        /// </exception>
        public OpenAIImagesProvider(OpenAIImagesSettings settings)
            : this(settings, new HttpClientHandler())
        {
        }

        // The seam EditMode tests use: a handler that answers as the server would.
        internal OpenAIImagesProvider(OpenAIImagesSettings settings, HttpMessageHandler handler)
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
                || baseUri.Query.Length > 0 || baseUri.Fragment.Length > 0 || baseUri.UserInfo.Length > 0)
            {
                // The address itself is left out: it may carry a password (hard rule 1).
                throw new ArgumentException("OpenAIImagesSettings.Url is not an http address with no query or password, " +
                                            "such as http://127.0.0.1:1234/v1.", nameof(settings));
            }

            endpoint = baseUri.Scheme + "://" + baseUri.Authority + baseUri.AbsolutePath.TrimEnd('/');

            apiKey = (settings.ApiKey ?? "").Trim();
            if (apiKey.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            {
                throw new ArgumentException("OpenAIImagesSettings.ApiKey contains a space, a line break or another control character. A key is one word.", nameof(settings));
            }

            if (!(settings.TimeoutSeconds > 0f) || settings.TimeoutSeconds > int.MaxValue / 1000f)
            {
                throw new ArgumentException($"OpenAIImagesSettings.TimeoutSeconds is {settings.TimeoutSeconds}. It must be above 0.", nameof(settings));
            }

            retry = (settings.Retry ?? new RetryPolicy()).Copy();
            if (retry.MaxAttempts < 1 || !(retry.MaxWaitSeconds >= 0f) || !(retry.DeadlineSeconds >= 0f) || retry.DeadlineSeconds > int.MaxValue / 1000f
                || !(retry.BackoffSeconds >= 0f) || !(retry.MaxBackoffSeconds >= 0f)
                || float.IsInfinity(retry.MaxWaitSeconds) || float.IsInfinity(retry.BackoffSeconds) || float.IsInfinity(retry.MaxBackoffSeconds))
            {
                throw new ArgumentException("OpenAIImagesSettings.Retry is out of range: MaxAttempts must be 1 or more, and every number of " +
                                            "seconds 0 or more and finite.", nameof(settings));
            }

            if (settings.SizeStep < 1 || settings.MaxSide < settings.SizeStep || settings.MaxImages < 1)
            {
                throw new ArgumentException($"OpenAIImagesSettings: SizeStep {settings.SizeStep}, MaxSide {settings.MaxSide} and MaxImages " +
                                            $"{settings.MaxImages} must each be 1 or more, with MaxSide at least SizeStep.", nameof(settings));
            }

            model = (settings.Model ?? "").Trim();
            extraArgs = settings.SdCppExtraArgs;
            timeoutSeconds = settings.TimeoutSeconds;
            defaults = new Dictionary<string, string>(settings.DefaultParameters ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            foreach (var pair in defaults)
            {
                // Checked now, so a mistyped default fails when the game starts, not at the first picture.
                CheckName(pair.Key);
            }

            client = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.ExpectContinue = false;
            if (apiKey.Length > 0)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            Capabilities = new ImageCapabilities
            {
                SizeStep = settings.SizeStep,
                MaxSide = settings.MaxSide,
                HonoursSeed = extraArgs,
                Edits = settings.Edits,
                Masks = settings.Edits && settings.Masks,
                MaxImages = settings.MaxImages,
            };
        }

        /// <summary><c>"openai-images"</c>.</summary>
        public string Name => "openai-images";

        /// <summary>As the settings say; a seed is honoured only with <see cref="OpenAIImagesSettings.SdCppExtraArgs"/> on.</summary>
        public ImageCapabilities Capabilities { get; }

        /// <summary>The server's address as messages name it: scheme, host, port and path, nothing else.</summary>
        public string Endpoint => endpoint;

        // The pause between attempts. A seam for EditMode tests, which record the pauses asked for
        // rather than wait them out.
        internal Func<TimeSpan, CancellationToken, Task> Pause = (wait, ct) => Task.Delay(wait, ct);

        /// <summary>Makes the picture, or the edit, for <paramref name="request"/>.</summary>
        /// <exception cref="ArgumentException">
        /// What <see cref="ImageCapabilities.WhyNot"/> reports, a parameter that names a field the
        /// provider writes itself (<c>prompt</c>, <c>size</c>, <c>model</c> and the like), or an
        /// <c>extra.</c> parameter with <see cref="OpenAIImagesSettings.SdCppExtraArgs"/> off.
        /// </exception>
        public Awaitable<ImageResult> GenerateAsync(ImageRequest request, CancellationToken ct)
        {
            // Built before anything is sent, so a request that cannot be sent fails at the call.
            var built = Build(request);
            return Run(built, ct);
        }

        /// <summary>
        /// What <see cref="GenerateAsync"/> would send for <paramref name="request"/>: the route, the
        /// prompt as written (the extra-args block included), the size, the seed and every other
        /// field. For a check or a log; it contains no key and no picture bytes.
        /// </summary>
        /// <exception cref="ArgumentException">As <see cref="GenerateAsync"/>.</exception>
        public OpenAIImagesCall Describe(ImageRequest request)
        {
            var built = Build(request);
            return new OpenAIImagesCall(built.Route, built.Prompt, built.Size, built.Seed, built.Fields, built.Images.Count, built.Mask != null);
        }

        // ---- Building the request ------------------------------------------------------------------

        private sealed class Built
        {
            public string Route;
            public string Prompt;
            public string Size;
            public long? Seed;
            public List<KeyValuePair<string, JToken>> Fields = new List<KeyValuePair<string, JToken>>();
            public List<byte[]> Images = new List<byte[]>();
            public byte[] Mask;
        }

        private Built Build(ImageRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string why = Capabilities.WhyNot(request);
            if (why != null)
            {
                throw new ArgumentException(why, nameof(request));
            }

            var built = new Built
            {
                Route = request.IsEdit ? "images/edits" : "images/generations",
                Size = request.Width.ToString(CultureInfo.InvariantCulture) + "x" + request.Height.ToString(CultureInfo.InvariantCulture),
                Images = request.IsEdit ? request.Images.ToList() : new List<byte[]>(),
                Mask = request.Mask,
            };

            var own = request.Parameters ?? new Dictionary<string, string>();
            var all = defaults.Where(d => !own.ContainsKey(d.Key)).Concat(own).OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
            var extra = new JObject();
            foreach (var pair in all)
            {
                CheckName(pair.Key);
                if (extraArgs && (pair.Key.StartsWith(ExtraPrefix, StringComparison.Ordinal) || pair.Key == "strength" || pair.Key == "negative_prompt"))
                {
                    string path = pair.Key.StartsWith(ExtraPrefix, StringComparison.Ordinal) ? pair.Key.Substring(ExtraPrefix.Length) : pair.Key;
                    Place(extra, path, Coerce(pair.Value), pair.Key);
                }
                else
                {
                    built.Fields.Add(new KeyValuePair<string, JToken>(pair.Key, Coerce(pair.Value)));
                }
            }

            built.Prompt = request.Prompt ?? "";
            if (extraArgs)
            {
                // As comfy.py does: a fresh seed under 2^50 when none is given, reported so the picture can be made again.
                built.Seed = request.Seed ?? (long)(random.NextDouble() * (1L << 50));
                extra["seed"] = built.Seed.Value;
                built.Prompt = built.Prompt + " <sd_cpp_extra_args>" + extra.ToString(Formatting.None) + "</sd_cpp_extra_args>";
            }

            return built;
        }

        // A parameter's name must be one the provider does not write itself, and extra. needs the block.
        private void CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A parameter's name is empty.");
            }

            if (Reserved.Contains(name))
            {
                throw new ArgumentException($"The parameter '{name}' is a field OpenAIImagesProvider writes itself: set it through ImageRequest " +
                                            "(Prompt, Width and Height, Images, Mask) or OpenAIImagesSettings.Model instead.");
            }

            if (name.StartsWith(ExtraPrefix, StringComparison.Ordinal))
            {
                if (!extraArgs)
                {
                    throw new ArgumentException($"The parameter '{name}' goes into sd-server's <sd_cpp_extra_args> block, which " +
                                                "OpenAIImagesSettings.SdCppExtraArgs being off leaves out. Turn it on for sd-server, or drop the parameter.");
                }

                string path = name.Substring(ExtraPrefix.Length);
                if (path.Length == 0 || path.Split('.').Any(p => p.Length == 0) || path == "seed")
                {
                    throw new ArgumentException($"The parameter '{name}' is not extra.<path>, such as extra.sample_params.sample_steps. " +
                                                "The seed is ImageRequest.Seed.");
                }
            }
        }

        // Writes value at a dotted path, making the objects on the way.
        private static void Place(JObject root, string path, JToken value, string name)
        {
            string[] parts = path.Split('.');
            JObject at = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (at[parts[i]] == null)
                {
                    at[parts[i]] = new JObject();
                }

                at = at[parts[i]] as JObject ?? throw new ArgumentException($"The parameter '{name}' writes inside '{parts[i]}', which another parameter set to a value.");
            }

            at[parts[parts.Length - 1]] = value;
        }

        // An open string as JSON: true and false, an integer, a number, or the string itself.
        private static JToken Coerce(string raw)
        {
            raw = raw ?? "";
            if (raw == "true" || raw == "false")
            {
                return raw == "true";
            }

            if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole))
            {
                return whole;
            }

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && !double.IsNaN(number) && !double.IsInfinity(number))
            {
                return number;
            }

            return raw;
        }

        private HttpContent Content(Built built)
        {
            if (built.Images.Count == 0)
            {
                var body = new JObject
                {
                    ["prompt"] = built.Prompt,
                    ["n"] = 1,
                    ["size"] = built.Size,
                    ["output_format"] = "png",
                };
                if (model.Length > 0)
                {
                    body["model"] = model;
                }

                foreach (var field in built.Fields)
                {
                    body[field.Key] = field.Value;
                }

                return new StringContent(body.ToString(Formatting.None), Utf8, "application/json");
            }

            var form = new MultipartFormDataContent();
            void Text(string name, string value) => form.Add(new StringContent(value, Utf8), Quoted(name));
            void Png(string name, byte[] bytes, string file)
            {
                var part = new ByteArrayContent(bytes);
                part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                form.Add(part, Quoted(name), Quoted(file));
            }

            Text("prompt", built.Prompt);
            Text("n", "1");
            Text("size", built.Size);
            Text("output_format", "png");
            if (model.Length > 0)
            {
                Text("model", model);
            }

            string imageField = built.Images.Count == 1 ? "image" : "image[]";
            for (int i = 0; i < built.Images.Count; i++)
            {
                Png(imageField, built.Images[i], "image-" + i.ToString(CultureInfo.InvariantCulture) + ".png");
            }

            if (built.Mask != null)
            {
                Png("mask", built.Mask, "mask.png");
            }

            foreach (var field in built.Fields)
            {
                Text(field.Key, field.Value.Type == JTokenType.String ? (string)field.Value : field.Value.ToString(Formatting.None));
            }

            return form;
        }

        // Multipart names in quotes, as browsers and OpenAI's own clients send them.
        private static string Quoted(string name) => "\"" + name.Replace("\"", "") + "\"";

        // ---- Sending it ----------------------------------------------------------------------------

        private async Awaitable<ImageResult> Run(Built built, CancellationToken ct)
        {
            var whole = Stopwatch.StartNew();
            try
            {
                string text = await Exchange(built, ct, whole);
                byte[] png = ReadPicture(text);
                return ImageResult.Made(png, Name, (float)whole.Elapsed.TotalSeconds, built.Seed);
            }
            catch (Failure f)
            {
                return ImageResult.Failed(f.Kind, Name, Redact(f.Message), (float)whole.Elapsed.TotalSeconds);
            }
        }

        // One request, sent again under the retry policy while the server answers busy (DR-026).
        private async Awaitable<string> Exchange(Built built, CancellationToken ct, Stopwatch whole)
        {
            int sends = 0;
            float paused = 0f;
            while (true)
            {
                float before = (float)whole.Elapsed.TotalSeconds;
                float attemptTimeout = timeoutSeconds;
                bool deadlineCuts = false;
                if (retry.DeadlineSeconds > 0f && retry.DeadlineSeconds - before < attemptTimeout)
                {
                    attemptTimeout = Math.Max(retry.DeadlineSeconds - before, 0.001f);
                    deadlineCuts = true;
                }

                var (status, text, retryAfter) = await Attempt(built, attemptTimeout, deadlineCuts, ct);
                sends++;
                if (status >= 200 && status <= 299)
                {
                    return text;
                }

                if (!RetryPolicy.IsBusy(status))
                {
                    throw Refusal(status, text, built.Route);
                }

                float wait = retryAfter ?? retry.BackoffFor(sends, NextUnit());
                string stop = null;
                if (sends >= retry.MaxAttempts)
                {
                    stop = retry.MaxAttempts == 1
                        ? "OpenAIImagesSettings.Retry.MaxAttempts is 1, so it was not sent again."
                        : $"It was sent {sends} times, Retry.MaxAttempts.";
                }
                else if (paused + wait > retry.MaxWaitSeconds)
                {
                    stop = $"The next pause, {Seconds(wait)} s, would pass Retry.MaxWaitSeconds, {Seconds(retry.MaxWaitSeconds)} s.";
                }
                else if (retry.DeadlineSeconds > 0f && whole.Elapsed.TotalSeconds + wait >= retry.DeadlineSeconds)
                {
                    stop = $"The next pause, {Seconds(wait)} s, would end past Retry.DeadlineSeconds, {Seconds(retry.DeadlineSeconds)} s.";
                }

                if (stop != null)
                {
                    string asked = retryAfter.HasValue ? $", asking to wait {Seconds(retryAfter.Value)} s" : "";
                    throw new Failure(ProviderErrorKind.RateLimited,
                        $"The images server at {endpoint} is busy at {built.Route}: HTTP {status}{asked}. {stop} " +
                        "Raise OpenAIImagesSettings.Retry's limits to wait longer.");
                }

                try
                {
                    await Pause(TimeSpan.FromSeconds(wait), ct);
                }
                catch (Exception e) when (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException("The caller cancelled the image request while it waited to send it again.", e, ct);
                }

                paused += wait;
            }
        }

        // One send, with its own timeout. Any status comes back; only the transport's failures throw.
        private async Awaitable<(int Status, string Text, float? RetryAfter)> Attempt(Built built, float seconds, bool deadlineCuts, CancellationToken ct)
        {
            using (var timeout = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token))
            using (var message = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, built.Route)))
            {
                // A fresh body each attempt: a sent HttpContent cannot be sent again.
                message.Content = Content(built);
                timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
                try
                {
                    using (var response = await client.SendAsync(message, linked.Token))
                    {
                        int status = (int)response.StatusCode;
                        string text = Utf8.GetString(await response.Content.ReadAsByteArrayAsync());
                        float? retryAfter = RetryPolicy.IsBusy(status) ? RetryAfter(response) : null;
                        return (status, text, retryAfter);
                    }
                }
                catch (Exception e) when (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException($"The caller cancelled the request to {built.Route}.", e, ct);
                }
                catch (Exception e) when (timeout.IsCancellationRequested)
                {
                    throw new Failure(ProviderErrorKind.Timeout, deadlineCuts
                        ? $"The images server at {endpoint} sent no picture before Retry.DeadlineSeconds, {Seconds(retry.DeadlineSeconds)} s, ran out."
                        : $"The images server at {endpoint} sent no picture within {Seconds(timeoutSeconds)} s. A model loading for the " +
                          "first time takes longest; raise OpenAIImagesSettings.TimeoutSeconds if it keeps happening.", e);
                }
                catch (Exception e) when (e is HttpRequestException || e is IOException || e is System.Net.WebException
                                          || e is System.Net.Sockets.SocketException)
                {
                    throw new Failure(ProviderErrorKind.Unreachable,
                        $"Nothing answered at {endpoint}. Is the images server (sd-server, LocalAI) running there? " +
                        $"({Excerpt(FirstLine(e.GetBaseException().Message))})", e);
                }
            }
        }

        private static float? RetryAfter(HttpResponseMessage response)
        {
            var now = DateTimeOffset.UtcNow;
            float? parsed = RetryPolicy.RetryAfterSeconds(response.Headers.RetryAfter, now);
            if (parsed.HasValue)
            {
                return parsed;
            }

            return response.Headers.TryGetValues("Retry-After", out var values)
                ? RetryPolicy.RetryAfterSeconds(string.Join(",", values), now)
                : null;
        }

        private double NextUnit()
        {
            lock (random)
            {
                return random.NextDouble();
            }
        }

        // ---- Reading the answer --------------------------------------------------------------------

        private Failure Refusal(int status, string text, string route)
        {
            ServerError(text, out string said, out string code);
            string quoted = said.Length > 0 ? $" It said: {Excerpt(said)}" : "";
            if (code == "model_not_found" || (status == 404 && said.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0 && model.Length > 0))
            {
                return new Failure(ProviderErrorKind.ModelNotFound,
                    $"The images server at {endpoint} has no model '{model}' (HTTP {status}).{quoted} Check OpenAIImagesSettings.Model.");
            }

            if (status == 401 || status == 403)
            {
                return new Failure(ProviderErrorKind.Rejected,
                    $"The images server at {endpoint} refused the key (HTTP {status}).{quoted} Check the key's variable.");
            }

            if (status == 404)
            {
                return new Failure(ProviderErrorKind.Rejected,
                    $"The images server at {endpoint} has no route {route} (HTTP 404).{quoted} OpenAIImagesSettings.Url is the API's base, " +
                    "up to and including /v1; a server that does not edit (LocalAI) answers 404 to an edit, so set Edits off for it.");
            }

            if (status >= 500)
            {
                return new Failure(ProviderErrorKind.ServerError, $"The images server at {endpoint} failed at {route} (HTTP {status}).{quoted}");
            }

            return new Failure(ProviderErrorKind.Rejected, $"The images server at {endpoint} refused the request to {route} (HTTP {status}).{quoted}");
        }

        // OpenAI's {"error":{"message","code"}}, a bare {"error":"..."}, or the text itself.
        private static void ServerError(string text, out string message, out string code)
        {
            message = FirstLine(text ?? "");
            code = "";
            try
            {
                var root = JToken.Parse(text ?? "") as JObject;
                var error = root?["error"];
                if (error is JObject e)
                {
                    message = (string)e["message"] ?? message;
                    code = (string)e["code"] ?? "";
                }
                else if (error != null && error.Type == JTokenType.String)
                {
                    message = (string)error;
                }
                else if (root?["message"] != null)
                {
                    message = (string)root["message"];
                }
            }
            catch (JsonException)
            {
                // Not JSON: the first line stands.
            }
        }

        private byte[] ReadPicture(string text)
        {
            JObject root;
            try
            {
                root = JToken.Parse(text ?? "") as JObject;
            }
            catch (JsonException e)
            {
                throw new Failure(ProviderErrorKind.MalformedResponse, $"The images server at {endpoint} answered with text that is not JSON: {Excerpt(FirstLine(text))}", e);
            }

            var first = (root?["data"] as JArray)?.FirstOrDefault() as JObject;
            if (first == null)
            {
                throw new Failure(ProviderErrorKind.MalformedResponse, $"The images server at {endpoint} answered with no data[0]: {Excerpt(FirstLine(text))}");
            }

            string b64 = (string)first["b64_json"];
            if (string.IsNullOrEmpty(b64))
            {
                string why = first["url"] != null
                    ? "It sent a URL instead of the picture. Ask for base64: add the default parameter response_format = b64_json (LocalAI and DALL-E want it)."
                    : "It sent no b64_json.";
                throw new Failure(ProviderErrorKind.MalformedResponse, $"The images server at {endpoint} answered with no picture. {why}");
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(b64);
            }
            catch (FormatException e)
            {
                throw new Failure(ProviderErrorKind.MalformedResponse, $"The images server at {endpoint} sent b64_json that is not base64.", e);
            }

            if (!ImageResult.IsPng(bytes))
            {
                throw new Failure(ProviderErrorKind.MalformedResponse,
                    $"The images server at {endpoint} sent a picture that is not a PNG ({bytes.Length} bytes). It was asked for output_format png.");
            }

            return bytes;
        }

        private string Redact(string text)
        {
            text = text ?? "";
            return apiKey.Length > 0 ? text.Replace(apiKey, "[key]") : text;
        }

        private static string FirstLine(string text)
        {
            text = (text ?? "").Trim();
            int end = text.IndexOfAny(new[] { '\r', '\n' });
            return end < 0 ? text : text.Substring(0, end);
        }

        private static string Excerpt(string text)
        {
            text = text ?? "";
            return text.Length <= Longest ? text : text.Substring(0, Longest) + "…";
        }

        private static string Seconds(float seconds) => seconds.ToString("0.###", CultureInfo.InvariantCulture);

        private sealed class Failure : Exception
        {
            public Failure(ProviderErrorKind kind, string message, Exception inner = null)
                : base(message, inner)
            {
                Kind = kind;
            }

            public ProviderErrorKind Kind { get; }
        }
    }

    /// <summary>What <see cref="OpenAIImagesProvider.Describe"/> says a request would send. No key and no picture bytes.</summary>
    public sealed class OpenAIImagesCall
    {
        internal OpenAIImagesCall(string route, string prompt, string size, long? seed, IEnumerable<KeyValuePair<string, JToken>> fields, int images, bool mask)
        {
            Route = route;
            Prompt = prompt;
            Size = size;
            Seed = seed;
            Fields = fields.ToDictionary(f => f.Key, f => f.Value.Type == JTokenType.String ? (string)f.Value : f.Value.ToString(Formatting.None), StringComparer.Ordinal);
            Images = images;
            HasMask = mask;
        }

        /// <summary><c>images/generations</c> or <c>images/edits</c>.</summary>
        public string Route { get; }

        /// <summary>The prompt as sent, the <c>&lt;sd_cpp_extra_args&gt;</c> block included.</summary>
        public string Prompt { get; }

        /// <summary>The size as sent, such as <c>768x896</c>.</summary>
        public string Size { get; }

        /// <summary>The seed sent in the extra-args block, or null when none is sent.</summary>
        public long? Seed { get; }

        /// <summary>Every other field, by name, as JSON text for a number or a flag.</summary>
        public IReadOnlyDictionary<string, string> Fields { get; }

        /// <summary>How many input pictures are sent.</summary>
        public int Images { get; }

        /// <summary>Whether a mask is sent.</summary>
        public bool HasMask { get; }
    }
}
