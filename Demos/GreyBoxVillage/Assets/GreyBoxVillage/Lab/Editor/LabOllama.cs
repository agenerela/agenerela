using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Demos.GreyBoxVillage.Lab
{
    /// <summary>Which Ollama the bench asks: the laptop's, or the RTX 5080's behind its gateway.</summary>
    public sealed class LabServer
    {
        public string Name;
        public string Endpoint;
        /// <summary>The environment variable holding the gateway's key; empty for none. The key itself is never stored or written.</summary>
        public string ApiKeyVariable = "";

        public static readonly LabServer Local = new LabServer { Name = "local", Endpoint = "http://127.0.0.1:11434" };

        /// <summary>The RTX 5080's gateway, from AGENERELA_OLLAMA_URL and AGENERELA_OLLAMA_API_KEY (tools/ollama-server/AGENT_USAGE.md).</summary>
        public static LabServer Remote()
        {
            string url = Environment.GetEnvironmentVariable("AGENERELA_OLLAMA_URL");
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("-labServer 5080 needs AGENERELA_OLLAMA_URL in Unity's environment (tools/ollama-server/AGENT_USAGE.md).");
            return new LabServer { Name = "5080", Endpoint = url.Trim().TrimEnd('/'), ApiKeyVariable = "AGENERELA_OLLAMA_API_KEY" };
        }

        public static LabServer Parse(string name) =>
            string.IsNullOrEmpty(name) || name == "local" ? Local
            : name == "5080" ? Remote()
            : throw new ArgumentException("-labServer is local or 5080, not " + name);
    }

    /// <summary>
    /// What the bench asks an Ollama server itself, beside the decisions the framework sends: the
    /// version, each model's digest and details, what is resident and how much of it is in VRAM,
    /// and loading or unloading a model (hard rule 3). The framework's own readers of these are
    /// internal to it (gap G25), so the bench calls the HTTP API, as tools/ollama-server does.
    /// </summary>
    public sealed class LabOllama : IDisposable
    {
        [Serializable] private sealed class VersionReply { public string version = ""; }
        [Serializable] private sealed class TagsReply { public List<TagModel> models = new List<TagModel>(); }
        [Serializable] private sealed class TagModel { public string name = ""; public string digest = ""; public long size; }
        [Serializable] private sealed class ShowReply { public ShowDetails details = new ShowDetails(); }
        [Serializable] public sealed class ShowDetails { public string family = ""; public string parameter_size = ""; public string quantization_level = ""; public string format = ""; }
        [Serializable] private sealed class PsReply { public List<Resident> models = new List<Resident>(); }

        /// <summary>One model in memory, as /api/ps lists it.</summary>
        [Serializable]
        public sealed class Resident
        {
            public string name = "";
            public long size;
            public long size_vram;
            public int context_length;
        }

        private readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        private readonly string endpoint;
        private readonly string key;

        public LabOllama(LabServer server)
        {
            endpoint = server.Endpoint.TrimEnd('/');
            key = string.IsNullOrEmpty(server.ApiKeyVariable) ? null : Environment.GetEnvironmentVariable(server.ApiKeyVariable);
            if (!string.IsNullOrEmpty(server.ApiKeyVariable) && string.IsNullOrEmpty(key))
                throw new InvalidOperationException(server.ApiKeyVariable + " is not set in Unity's environment.");
        }

        public void Dispose() => client.Dispose();

        public async Task<string> VersionAsync() => JsonUtility.FromJson<VersionReply>(await Get("/api/version")).version;

        /// <summary>Each pulled model's digest, from /api/tags (/api/show has none).</summary>
        public async Task<Dictionary<string, string>> DigestsAsync()
        {
            var tags = JsonUtility.FromJson<TagsReply>(await Get("/api/tags"));
            return tags.models.ToDictionary(m => m.name, m => m.digest);
        }

        public async Task<ShowDetails> ShowAsync(string model) =>
            JsonUtility.FromJson<ShowReply>(await Post("/api/show", "{\"model\":" + Json(model) + "}")).details;

        public async Task<List<Resident>> ResidentAsync() => JsonUtility.FromJson<PsReply>(await Get("/api/ps")).models ?? new List<Resident>();

        /// <summary>Unloads <paramref name="model"/> (keep_alive 0) and waits until /api/ps no longer lists it. False if it stays.</summary>
        public async Task<bool> UnloadAsync(string model, float waitSeconds = 20f)
        {
            await Post("/api/generate", "{\"model\":" + Json(model) + ",\"keep_alive\":0}");
            var clock = Stopwatch.StartNew();
            while (true)
            {
                if (!(await ResidentAsync()).Any(r => Same(r.name, model))) return true;
                if (clock.Elapsed.TotalSeconds > waitSeconds) return false;
                await Task.Delay(250);
            }
        }

        /// <summary>Unloads every resident model but <paramref name="keep"/>, and says which.</summary>
        public async Task<List<string>> UnloadOthersAsync(string keep)
        {
            var gone = new List<string>();
            foreach (var r in await ResidentAsync())
            {
                if (keep != null && Same(r.name, keep)) continue;
                if (!await UnloadAsync(r.name)) throw new InvalidOperationException(r.name + " stayed resident after keep_alive 0 (hard rule 3).");
                gone.Add(r.name);
            }
            return gone;
        }

        /// <summary>Loads <paramref name="model"/> for <paramref name="numCtx"/> tokens, as the provider's warm-up does, with the variant's keep_alive if it has one.</summary>
        public async Task<double> LoadAsync(string model, int numCtx, string keepAlive)
        {
            var clock = Stopwatch.StartNew();
            string alive = string.IsNullOrEmpty(keepAlive) ? "" : ",\"keep_alive\":" + (int.TryParse(keepAlive, out int n) ? n.ToString() : Json(keepAlive));
            await Post("/api/generate", "{\"model\":" + Json(model) + ",\"options\":{\"num_ctx\":" + numCtx + "}" + alive + "}");
            return clock.Elapsed.TotalSeconds;
        }

        public static bool Same(string a, string b)
        {
            string T(string x) => x == null ? "" : x.Trim().Contains(":") ? x.Trim() : x.Trim() + ":latest";
            return string.Equals(T(a), T(b), StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> Get(string path)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, endpoint + path)) return await Send(request);
        }

        private async Task<string> Post(string path, string body)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint + path) { Content = new StringContent(body, Encoding.UTF8, "application/json") })
                return await Send(request);
        }

        private async Task<string> Send(HttpRequestMessage request)
        {
            if (key != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
            using (var response = await client.SendAsync(request))
            {
                string text = await response.Content.ReadAsStringAsync();
                // The address is named without a user or password; the key is never in it.
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException("Ollama answered " + (int)response.StatusCode + " to " + request.Method + " " + request.RequestUri.AbsolutePath + ": " + Short(text));
                return text;
            }
        }

        private static string Short(string s) => s == null ? "" : s.Length > 200 ? s.Substring(0, 200) + "..." : s;

        private static string Json(string s) =>
            "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
