using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
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
            // a warm-up at the default would leave this decision to load it again (Codex review, F1).
            // Seen live on 5 November: 7.46 s for this decision without the fix, 1.18 s with it.
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
            // The same decision twice, each on a fresh agent so no memory lengthens the second. Their
            // inference costs the same, so what the first takes beyond the second is a reload, however
            // fast the server infers (Codex review, F5).
            const string Stimulus = "Head over to the tower and keep watch.";
            var first = await LiveOllama.NotCancelled(LiveGuard.Build(provider).DecideAsync(Stimulus, LiveGuard.Options()));
            var second = await LiveOllama.NotCancelled(LiveGuard.Build(provider).DecideAsync(Stimulus, LiveGuard.Options()));
            double extra = first.Telemetry.LatencySeconds - second.Telemetry.LatencySeconds;
            UnityEngine.Debug.Log($"LIFECYCLE {LiveOllama.Model} at num_ctx 8192: cold warm-up {cold.Elapsed.TotalSeconds:F2} s, "
                                + $"first decision after it {first.Telemetry.LatencySeconds:F2} s, the same again "
                                + $"{second.Telemetry.LatencySeconds:F2} s");

            Assert.That(first.Decision, Is.Not.Null, $"{first.Telemetry.ErrorKind}: {first.Telemetry.ErrorMessage}");
            Assert.That(second.Decision, Is.Not.Null, $"{second.Telemetry.ErrorKind}: {second.Telemetry.ErrorMessage}");
            Assert.That(extra, Is.LessThan(cold.Elapsed.TotalSeconds / 2),
                "The first decision after the warm-up took about a load longer than the same decision again: "
              + "the model was loaded again.");
            Assert.That(await LiveOllama.NotCancelled(models.UnloadAsync(CancellationToken.None)), Is.True);
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
