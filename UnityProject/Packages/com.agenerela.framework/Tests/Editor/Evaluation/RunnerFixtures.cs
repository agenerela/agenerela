using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Evaluation;
using Agenerela.Providers;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// What the runner tests (#145) share: a small set on disk for <see cref="Fixtures.VillageGuardProfile"/>,
    /// provider configs around a provider the test made, and a stand-in Ollama server. Dispose
    /// deletes the set's folder and the configs.
    /// </summary>
    /// <remarks>
    /// No stimulus here may be an evaluation prompt or a few-shot example (hard rule 4), and none
    /// may be one of the runner's warm-up stimuli, which would make it skip them.
    /// </remarks>
    public sealed class RunnerFixtures : IDisposable
    {
        public const string Model2B = "qwen3.5:2b", Model4B = "qwen3.5:4b";

        private readonly List<ScriptableObject> configs = new List<ScriptableObject>();

        public RunnerFixtures()
        {
            Folder = Path.Combine(Path.GetTempPath(), "agenerela-runner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Folder, "prompts"));
            File.WriteAllText(Path.Combine(Folder, "agent.json"), EvalFixtures.SetJson);
        }

        public string Folder { get; }

        /// <summary>
        /// Writes the set's two categories and loads it. <paramref name="commands"/> are
        /// <c>command_with_target</c>'s prompts (<c>target_nnn</c>), <paramref name="situations"/>
        /// <c>situation</c>'s; each a prompt object as <see cref="Prompt"/> writes it.
        /// </summary>
        public PromptSet Set(IEnumerable<string> commands, IEnumerable<string> situations = null)
        {
            File.WriteAllText(Path.Combine(Folder, "prompts", "commands.json"),
                "{ \"category\": \"command_with_target\", \"prompts\": [" + string.Join(",", commands) + "] }");
            File.WriteAllText(Path.Combine(Folder, "prompts", "situations.json"),
                "{ \"category\": \"situation\", \"prompts\": [" + string.Join(",", situations ?? Enumerable.Empty<string>()) + "] }");
            return PromptSet.Load(Path.Combine(Folder, "agent.json"));
        }

        /// <summary>One prompt's JSON. <paramref name="target"/> may be a JSON list for an open choice.</summary>
        public static string Prompt(string id, string stimulus, string action, string target, bool following,
                                    string label = "Player", string observations = "[]", string extra = "")
        {
            string expected = target.StartsWith("[", StringComparison.Ordinal) ? target : $"\"{target}\"";
            return $"{{ \"id\": \"{id}\", \"label\": \"{label}\", \"stimulus\": \"{stimulus}\", \"observations\": {observations}, "
                 + $"\"expectedAction\": \"{action}\", \"expectedTarget\": {expected}, "
                 + $"\"preconditionState\": {{ \"isFollowing\": {(following ? "true" : "false")} }}, \"labelledBy\": [\"Maxim\"]{extra} }}";
        }

        /// <summary>A reply naming <paramref name="action"/> and <paramref name="target"/>, as the model writes one.</summary>
        public static string Reply(string action, string target, string statement = "")
        {
            return new JObject { ["action"] = action, ["target"] = target, ["statement"] = statement }.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>A provider config whose provider is <paramref name="provider"/>, named <paramref name="name"/>.</summary>
        public ProviderConfig Config(ILLMProvider provider, string name)
        {
            var config = ScriptableObject.CreateInstance<GivenProviderConfig>();
            config.name = name;
            config.Instance = provider;
            configs.Add(config);
            return config;
        }

        /// <summary>A provider config for an <see cref="OllamaProvider"/> on <paramref name="model"/> that talks to <paramref name="server"/>.</summary>
        public ProviderConfig Ollama(FakeOllama server, string model, string name, string endpoint = "http://127.0.0.1:11434")
        {
            var provider = new OllamaProvider(new OllamaSettings { Endpoint = endpoint, Model = model }, server);
            return Config(provider, name);
        }

        public void Dispose()
        {
            foreach (var config in configs)
            {
                UnityEngine.Object.DestroyImmediate(config);
            }

            configs.Clear();
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, true);
            }
        }

        // A config that hands out the provider the test built, so the test can script and inspect it.
        private sealed class GivenProviderConfig : ProviderConfig
        {
            public ILLMProvider Instance;

            protected override ILLMProvider CreateProvider() => Instance;
        }
    }

    /// <summary>
    /// Stands in for an Ollama server: <c>/api/version</c>, <c>/api/ps</c>, <c>/api/chat</c> and
    /// the unloading <c>/api/generate</c>. A chat loads its model, as Ollama does; every request is
    /// logged in order, so a test can see what was asked when.
    /// </summary>
    public sealed class FakeOllama : HttpMessageHandler
    {
        public string Version = "0.40.14";

        /// <summary>What every chat answers.</summary>
        public string Content = RunnerFixtures.Reply("none", "no_target");

        /// <summary>The models in memory, in the order loaded.</summary>
        public readonly List<string> Loaded = new List<string>();

        /// <summary>Each model's size and the part of it in VRAM, as <c>/api/ps</c> reports them.</summary>
        public long Size = 3_400_000_000, SizeVram = 3_400_000_000;

        /// <summary>When true, an unload request is answered but the model stays loaded.</summary>
        public bool IgnoresUnload;

        /// <summary>Each request: <c>GET /api/ps</c>, <c>POST /api/chat qwen3.5:2b</c>, <c>POST /api/generate qwen3.5:2b keep_alive=0</c>.</summary>
        public readonly List<string> Log = new List<string>();

        /// <summary>The most models that were ever loaded at once.</summary>
        public int MostLoaded { get; private set; }

        public IEnumerable<string> Chats => Log.Where(l => l.StartsWith("POST /api/chat", StringComparison.Ordinal));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri.AbsolutePath;
            var body = request.Content == null ? null : JObject.Parse(request.Content.ReadAsStringAsync().Result);
            string model = (string)body?["model"];

            string answer;
            switch (path)
            {
                case "/api/version":
                    Log.Add("GET /api/version");
                    answer = new JObject { ["version"] = Version }.ToString();
                    break;
                case "/api/ps":
                    Log.Add("GET /api/ps");
                    answer = new JObject
                    {
                        ["models"] = new JArray(Loaded.Select(m => new JObject
                        {
                            ["name"] = m, ["model"] = m, ["size"] = Size, ["size_vram"] = SizeVram,
                        })),
                    }.ToString();
                    break;
                case "/api/generate":
                    Log.Add($"POST /api/generate {model} keep_alive={body?["keep_alive"]}");
                    if (!IgnoresUnload && (int?)body?["keep_alive"] == 0)
                    {
                        Loaded.Remove(model);
                    }

                    answer = new JObject { ["model"] = model, ["done"] = true, ["done_reason"] = "unload" }.ToString();
                    break;
                case "/api/chat":
                    Log.Add($"POST /api/chat {model}");
                    if (!Loaded.Contains(model))
                    {
                        Loaded.Add(model);
                    }

                    MostLoaded = Math.Max(MostLoaded, Loaded.Count);
                    answer = new JObject
                    {
                        ["model"] = model,
                        ["message"] = new JObject { ["role"] = "assistant", ["content"] = Content },
                        ["done"] = true,
                        ["prompt_eval_count"] = 120,
                        ["eval_count"] = 12,
                    }.ToString();
                    break;
                default:
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("404 page not found") });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(answer, new UTF8Encoding(false), "application/json"),
            });
        }
    }
}
