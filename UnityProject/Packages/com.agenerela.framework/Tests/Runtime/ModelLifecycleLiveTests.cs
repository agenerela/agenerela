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
                await missing.WarmUpAsync(CancellationToken.None);
                Assert.Fail("The warm-up succeeded for a model no machine has.");
            }
            catch (ProviderException e)
            {
                Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.ModelNotFound), e.Message);
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
