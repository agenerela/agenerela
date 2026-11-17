using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests
{
    /// <summary>
    /// The local Ollama server the <c>RequiresOllama</c> tests run against (#94): where it is,
    /// which model to ask, whether it answers at all, and the warm-up that keeps a cold load out
    /// of the timed decision (build plan §4, item 6).
    /// </summary>
    /// <remarks>
    /// The endpoint and model come from <c>AGENERELA_OLLAMA_URL</c> and
    /// <c>AGENERELA_OLLAMA_MODEL</c>, so a machine with a different port or model runs the same
    /// tests unchanged. A server behind the gateway in <c>tools/ollama-server/</c> also needs
    /// <c>AGENERELA_OLLAMA_API_KEY</c>, its key, which is sent and never logged. The default endpoint is <c>127.0.0.1</c>, never <c>localhost</c>, which on
    /// Windows tries IPv6 first and adds about 2 s to every request (findings, 5 October 2026).
    /// </remarks>
    internal static class LiveOllama
    {
        public const string Category = "RequiresOllama";

        // A model name no machine has pulled, for the missing-model check.
        public const string MissingModel = "agenerela-missing-model:404";

        private static bool? reachable;
        private static bool warmed;

        public static string Endpoint => FromEnvironment("AGENERELA_OLLAMA_URL", "http://127.0.0.1:11434");

        // qwen3.5:2b on purpose, though OllamaSettings now defaults to the 4B: the Phase 2 and
        // Phase 3 gates were passed and recorded on the 2B (findings, 25 October and 3 November),
        // so the live tests keep measuring what those entries describe. Set the variable to try another.
        public static string Model => FromEnvironment("AGENERELA_OLLAMA_MODEL", "qwen3.5:2b");

        /// <summary>The environment variable holding the server's key, for a config to name.</summary>
        public const string ApiKeyVariable = "AGENERELA_OLLAMA_API_KEY";

        /// <summary>The server's key, or empty for a local Ollama, which wants none.</summary>
        public static string ApiKey => FromEnvironment(ApiKeyVariable, "");

        /// <summary>
        /// Forgets whether Ollama answered and whether the model was warmed. Called once per test
        /// run, because with domain reload disabled in Enter Play Mode Options these statics
        /// outlive the run, and a server started or stopped since would be judged by the last one.
        /// </summary>
        public static void Reset()
        {
            reachable = null;
            warmed = false;
        }

        /// <summary>An Ollama provider for <paramref name="model"/>, <see cref="Model"/> when null.</summary>
        public static OllamaProvider Provider(string model = null)
        {
            return new OllamaProvider(new OllamaSettings { Endpoint = Endpoint, Model = model ?? Model, ApiKey = ApiKey });
        }

        /// <summary>
        /// Ignores the calling test, with the reason, when nothing answers at <see cref="Endpoint"/>,
        /// so a machine without Ollama stays green (build plan §4, item 7). Asked once per run.
        /// </summary>
        public static void RequireReachable()
        {
            if (reachable == null)
            {
                int status = Answers(Endpoint);
                if (status == 401)
                {
                    // A server that answers but wants a key is there: skipping would hide the mistake.
                    Assert.Fail($"Ollama at {Endpoint} wants a key (HTTP 401). Set {ApiKeyVariable} to the one "
                              + "in the server's tools/ollama-server/.env.");
                }

                reachable = status >= 200 && status <= 299;
            }

            if (reachable == false)
            {
                Assert.Ignore($"Ollama is not reachable at {Endpoint}");
            }
        }

        /// <summary>
        /// Two or three throwaway decisions, once per run, on an agent of their own so no test's
        /// memory holds them. Fails when none of them is a decision, since every timed test after
        /// it would fail for the same reason, usually a model that was never pulled.
        /// </summary>
        public static async Task WarmUp()
        {
            if (warmed)
            {
                return;
            }

            var guard = LiveGuard.Build(Provider());
            string[] stimuli = { "Good evening.", "Is the road quiet tonight?", "Stay sharp." };
            DecisionResult last = null;
            bool decided = false;

            foreach (string stimulus in stimuli)
            {
                last = await NotCancelled(guard.DecideAsync(stimulus, LiveGuard.Options()));
                decided |= last.Decision != null;
            }

            Assert.That(decided, Is.True,
                $"Warming {Model} at {Endpoint} produced no decision: {last.Telemetry.ErrorKind}: "
              + $"{last.Telemetry.ErrorMessage}");
            warmed = true;
        }

        /// <summary>
        /// Awaits a decision and fails the test if it ends cancelled. The Unity Test Framework
        /// counts an async test whose Task ends cancelled as passed, skipping everything after the
        /// await, as the EditMode fixtures note.
        /// </summary>
        public static async Task<DecisionResult> NotCancelled(Awaitable<DecisionResult> decision)
        {
            try
            {
                return await decision;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"The decision ended cancelled, though nothing cancelled it: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Awaits anything else the framework returns as an <c>Awaitable</c>, such as an evaluation
        /// run (#145), and fails the test if it ends cancelled, for the same reason.
        /// </summary>
        public static async Task<T> NotCancelled<T>(Awaitable<T> awaitable)
        {
            try
            {
                return await awaitable;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"It ended cancelled, though nothing cancelled it: {e.Message}");
                return default;
            }
        }

        /// <summary>
        /// An <see cref="HttpClient"/> for a test's own call to the server, carrying
        /// <see cref="ApiKey"/> when there is one, so a server behind a gateway answers it too.
        /// </summary>
        public static HttpClient Client(TimeSpan timeout)
        {
            var client = new HttpClient { Timeout = timeout };
            if (ApiKey.Length > 0)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            }

            return client;
        }

        // The HTTP status of Ollama's /api/version, 0 when nothing answers, on a worker thread so
        // the blocking wait cannot deadlock against Unity's synchronization context.
        private static int Answers(string endpoint)
        {
            try
            {
                using (var client = Client(TimeSpan.FromSeconds(3)))
                {
                    var response = Task.Run(() => client.GetAsync(endpoint.TrimEnd('/') + "/api/version")).Result;
                    return (int)response.StatusCode;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static string FromEnvironment(string name, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
