using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests
{
    /// <summary>
    /// <see cref="IModelLifecycle"/> against the local Ollama (DR-018, #190 workstream D): the
    /// shapes <c>OllamaLifecycleTests</c> scripts, checked once against the real server. Ignored
    /// when nothing answers, as every <c>RequiresOllama</c> test is.
    /// </summary>
    /// <remarks>
    /// It loads and unloads <see cref="LiveOllama.Model"/>, so run it alone, with no other model
    /// resident (hard rule 3), and leave the model unloaded after. The two timings it prints are
    /// what the PR reports; neither is asserted beyond "loaded is quicker than cold".
    /// </remarks>
    [Category(LiveOllama.Category)]
    public sealed class ModelLifecycleLiveTests
    {
        [OneTimeSetUp]
        public void ForgetTheLastRun()
        {
            LiveOllama.Reset();
        }

        [SetUp]
        public void RequireOllama()
        {
            LiveOllama.RequireReachable();
        }

        [Test]
        public async Task TheModelIsListedWarmsUpAndUnloads()
        {
            var provider = LiveOllama.Provider();
            IModelLifecycle models = provider;

            // Ollama lists every model with its tag.
            string listed = LiveOllama.Model.Contains(":") ? LiveOllama.Model : LiveOllama.Model + ":latest";
            var installed = await LiveOllama.NotCancelled(models.ListModelsAsync(CancellationToken.None));
            Assert.That(installed.Select(m => m.Name), Has.Member(listed),
                $"{LiveOllama.Model} is not pulled at {LiveOllama.Endpoint}; the server lists: "
              + string.Join(", ", installed.Select(m => m.Name)));
            Assert.That(installed.All(m => m.SizeBytes > 0), Is.True, "Every listed model has a size on disk.");

            // Unloaded first, so the warm-up is a cold load.
            Assert.That(await LiveOllama.NotCancelled(models.UnloadAsync(CancellationToken.None)), Is.True);

            var cold = Stopwatch.StartNew();
            await Done(models.WarmUpAsync(CancellationToken.None));
            cold.Stop();
            var warm = Stopwatch.StartNew();
            await Done(models.WarmUpAsync(CancellationToken.None));
            warm.Stop();
            UnityEngine.Debug.Log($"LIFECYCLE {LiveOllama.Model}: cold warm-up {cold.Elapsed.TotalSeconds:F2} s, "
                                + $"warm-up when loaded {warm.Elapsed.TotalSeconds:F2} s, {installed.Count} models listed");
            Assert.That(warm.Elapsed, Is.LessThan(cold.Elapsed), "The second warm-up found the model loaded.");

            Assert.That(await LiveOllama.NotCancelled(models.UnloadAsync(CancellationToken.None)), Is.True,
                "The server stopped listing the model within 15 s.");
        }

        [Test]
        public async Task WarmingUpAModelThatWasNeverPulledIsModelNotFound()
        {
            IModelLifecycle missing = LiveOllama.Provider(LiveOllama.MissingModel);

            try
            {
                // Through Done, so a cancellation fails the test instead of passing it (Codex review, F4).
                await Done(missing.WarmUpAsync(CancellationToken.None));
                Assert.Fail("The warm-up succeeded for a model no machine has.");
            }
            catch (ProviderException e)
            {
                Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.ModelNotFound), e.Message);
            }
        }

        [Test]
        public async Task AWarmUpLoadsTheModelAtTheContextSizeTheDecisionsUse()
        {
            // Not the server's default: Ollama reloads a model asked for at another context size, so
            // a warm-up at the default would leave the first decision to load it again (Codex review,
            // F1). Seen live on 5 November: 7.46 s for that decision without the fix, 1.18 s with it.
            // Judged by the context size the warm-up loaded, which /api/ps reports, not by timing: a
            // decision's time also holds prompt processing, which Ollama caches (Codex review, F5).
            var provider = new OllamaProvider(new OllamaSettings
            {
                Endpoint = LiveOllama.Endpoint,
                Model = LiveOllama.Model,
                ContextSize = 8192,
            });
            IModelLifecycle models = provider;
            Assert.That(await LiveOllama.NotCancelled(models.UnloadAsync(CancellationToken.None)), Is.True);

            var cold = Stopwatch.StartNew();
            await Done(models.WarmUpAsync(CancellationToken.None));
            cold.Stop();
            long? loaded = await LoadedContextLength();
            var first = await LiveOllama.NotCancelled(
                LiveGuard.Build(provider).DecideAsync("Head over to the tower and keep watch.", LiveGuard.Options()));
            UnityEngine.Debug.Log($"LIFECYCLE {LiveOllama.Model} at num_ctx 8192: loaded at {loaded?.ToString() ?? "?"}, "
                                + $"cold warm-up {cold.Elapsed.TotalSeconds:F2} s, first decision after it "
                                + $"{first.Telemetry.LatencySeconds:F2} s");
            Assert.That(await LiveOllama.NotCancelled(models.UnloadAsync(CancellationToken.None)), Is.True);

            Assert.That(first.Decision, Is.Not.Null, $"{first.Telemetry.ErrorKind}: {first.Telemetry.ErrorMessage}");
            if (loaded == null)
            {
                Assert.Ignore($"Ollama at {LiveOllama.Endpoint} does not report context_length in /api/ps, so which "
                            + "context size the warm-up loaded cannot be read. The timings above are the only evidence.");
            }

            Assert.That(loaded, Is.EqualTo(8192), "The warm-up loaded the model at another context size, so the first "
                                                + "decision has to load it again.");
        }

        // The context length /api/ps reports for LiveOllama.Model, or null when it reports none.
        private static async Task<long?> LoadedContextLength()
        {
            string listed = LiveOllama.Model.Contains(":") ? LiveOllama.Model : LiveOllama.Model + ":latest";
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
            {
                var ps = JObject.Parse(await client.GetStringAsync(LiveOllama.Endpoint.TrimEnd('/') + "/api/ps"));
                var entry = (ps["models"] as JArray)?.OfType<JObject>()
                    .FirstOrDefault(m => string.Equals((string)m["name"], listed, StringComparison.OrdinalIgnoreCase));
                Assert.That(entry, Is.Not.Null, $"{listed} is not resident right after its warm-up: {ps}");
                return entry["context_length"]?.Type == JTokenType.Integer ? (long)entry["context_length"] : (long?)null;
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
    }
}
