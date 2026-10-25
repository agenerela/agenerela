using System;
using System.Net.Http;
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
    /// tests unchanged. The default endpoint is <c>127.0.0.1</c>, never <c>localhost</c>, which on
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

        public static string Model => FromEnvironment("AGENERELA_OLLAMA_MODEL", "qwen3.5:2b");

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
            return new OllamaProvider(new OllamaSettings { Endpoint = Endpoint, Model = model ?? Model });
        }

        /// <summary>
        /// Ignores the calling test, with the reason, when nothing answers at <see cref="Endpoint"/>,
        /// so a machine without Ollama stays green (build plan §4, item 7). Asked once per run.
        /// </summary>
        public static void RequireReachable()
        {
            if (reachable == null)
            {
                reachable = Answers(Endpoint);
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

        // Ollama's /api/version, on a worker thread so the blocking wait cannot deadlock against
        // Unity's synchronization context.
        private static bool Answers(string endpoint)
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
                {
                    var response = Task.Run(() => client.GetAsync(endpoint.TrimEnd('/') + "/api/version")).Result;
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string FromEnvironment(string name, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
