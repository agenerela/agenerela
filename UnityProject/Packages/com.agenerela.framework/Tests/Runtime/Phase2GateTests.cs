using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Agenerela.Tests
{
    /// <summary>
    /// The four checks that close Phase 2 (build plan §3, #94), each run against a real model
    /// served by a local Ollama. If these pass, the framework calls a model end to end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every test is in the <c>RequiresOllama</c> category, so a machine without a model can
    /// exclude it, and is ignored, with the reason, when nothing answers at the endpoint (build
    /// plan §4, item 7). The EditMode suite proves each rule with a fake provider; these prove the
    /// live path: <see cref="Providers.OllamaProvider"/>, the queue (#89) and memory (#90) joined.
    /// </para>
    /// <para>
    /// No test checks whether the model chose <i>correctly</i>. Accuracy is Phase 4's, measured
    /// with a control arm (hard rule 2). Each timed decision prints a <c>LATENCY</c> line, which
    /// the findings entry for this gate is built from.
    /// </para>
    /// </remarks>
    [Category(LiveOllama.Category)]
    public sealed class Phase2GateTests
    {
        private const double RoundTripBudgetSeconds = 5.0;

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
        public async Task Gate1_RoundTripIsUnderFiveSecondsAfterWarmUp()
        {
            await LiveOllama.WarmUp();
            var provider = new RecordingProvider(LiveOllama.Provider());
            var guard = LiveGuard.Build(provider);

            var clock = Stopwatch.StartNew();
            var result = await LiveOllama.NotCancelled(
                guard.DecideAsync("Head over to the tower and keep watch.", LiveGuard.Options()));
            clock.Stop();
            Report(nameof(Gate1_RoundTripIsUnderFiveSecondsAfterWarmUp), clock, result);

            AssertDecided(result);
            Assert.That(Allowed(provider.LastRequest, DecisionSchema.ActionFieldName), Does.Contain(result.Decision.ActionId),
                "The model answered with an action the schema did not offer. Ollama constrains sampling to the "
              + "schema it is sent in format (#88), so this means the schema was not sent, or not honoured.");
            Assert.That(Allowed(provider.LastRequest, DecisionSchema.TargetFieldName), Does.Contain(result.Decision.TargetId),
                "The model answered with a target the schema did not offer.");
            Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(RoundTripBudgetSeconds),
                $"A warm decision took {clock.Elapsed.TotalSeconds:F2} s wall-clock, over the Phase 2 budget of "
              + $"{RoundTripBudgetSeconds} s (build plan §3). Check that only one model is resident (ollama ps), "
              + "and that the endpoint is 127.0.0.1, not localhost.");
        }

        [Test]
        public async Task Gate2_TelemetryIsFilledIn()
        {
            await LiveOllama.WarmUp();
            var guard = LiveGuard.Build(LiveOllama.Provider());

            // Background, not the default, so the priority recorded is shown to be the one declared.
            var clock = Stopwatch.StartNew();
            var result = await LiveOllama.NotCancelled(
                guard.DecideAsync("Someone is shouting near the training dummy.", LiveGuard.Options(DecisionPriority.Background)));
            clock.Stop();
            Report(nameof(Gate2_TelemetryIsFilledIn), clock, result);

            AssertDecided(result);
            var t = result.Telemetry;
            Assert.That(t.ProviderName, Is.EqualTo("ollama"), "ProviderName is the provider's Name.");
            Assert.That(t.Model, Does.StartWith(LiveOllama.Model.Split(':')[0]),
                "Model is the model that answered, as Ollama reported it (#88).");
            Assert.That(t.FrameworkVersion, Is.EqualTo(AgenerelaInfo.Version), "FrameworkVersion is AgenerelaInfo.Version.");
            Assert.That(t.LatencySeconds, Is.GreaterThan(0f).And.LessThanOrEqualTo((float)clock.Elapsed.TotalSeconds),
                "LatencySeconds times the provider's request, which happens inside the call that was timed.");
            Assert.That(t.PromptTokens, Is.GreaterThan(0), "PromptTokens comes from Ollama's prompt_eval_count.");
            Assert.That(t.CompletionTokens, Is.GreaterThan(0), "CompletionTokens comes from Ollama's eval_count.");
            Assert.That(t.Priority, Is.EqualTo(DecisionPriority.Background), "Priority is the one the call declared (DR-015).");
            Assert.That(t.QueueWaitSeconds, Is.GreaterThanOrEqualTo(0f).And.LessThan((float)clock.Elapsed.TotalSeconds),
                "QueueWaitSeconds runs from joining the queue to being sent, inside the call that was timed (#89).");
            Assert.That(t.HistoryTurnsSent, Is.EqualTo(0), "A new agent has no exchanges to send (#90).");
        }

        [Test]
        public async Task Gate3_MissingModelIsATypedError()
        {
            var guard = LiveGuard.Build(LiveOllama.Provider(LiveOllama.MissingModel));

            DecisionResult result = null;
            try
            {
                result = await LiveOllama.NotCancelled(guard.DecideAsync("Follow me.", LiveGuard.Options()));
            }
            catch (Exception e) when (!(e is AssertionException))
            {
                Assert.Fail($"A missing model leaked an exception to the caller (#87): {e.GetType().Name}: {e.Message}");
            }

            Assert.That(result.Decision, Is.Null, "A model that is not pulled cannot decide.");
            Assert.That(result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ModelNotFound),
                $"Ollama's 404 for a model it lacks must map to ModelNotFound (#87, #88). It said: {result.Telemetry.ErrorMessage}");
            Assert.That(result.Telemetry.ErrorMessage, Does.Contain(LiveOllama.MissingModel),
                "The message names the model, so the developer knows what to pull.");
        }

        [Test]
        public async Task Gate4_HistoryReachesTheSecondRequest()
        {
            await LiveOllama.WarmUp();
            var provider = new RecordingProvider(LiveOllama.Provider());
            var guard = LiveGuard.Build(provider);

            var clock = Stopwatch.StartNew();
            var first = await LiveOllama.NotCancelled(guard.DecideAsync("Follow me.", LiveGuard.Options()));
            clock.Stop();
            Report(nameof(Gate4_HistoryReachesTheSecondRequest) + ".first", clock, first);
            AssertDecided(first);
            Assert.That(first.Telemetry.HistoryTurnsSent, Is.EqualTo(0), "Nothing was remembered before the first decision.");

            // Carried out, so the agent is in the state the second line assumes (build plan §4, item 4).
            // Whether the model chose follow_player is not this test's concern.
            guard.Execute(first);

            clock.Restart();
            var second = await LiveOllama.NotCancelled(guard.DecideAsync("Now wait here.", LiveGuard.Options()));
            clock.Stop();
            Report(nameof(Gate4_HistoryReachesTheSecondRequest) + ".second", clock, second);
            AssertDecided(second);

            Assert.That(second.Telemetry.HistoryTurnsSent, Is.EqualTo(1),
                "The second decision must send the first exchange as history (#90).");
            var history = provider.LastRequest.History;
            Assert.That(history, Has.Count.EqualTo(2), "One exchange is two entries: the stimulus, then the decision.");
            Assert.That(history[0], Does.Contain("Follow me."), "The first entry is the earlier stimulus, label included.");
            Assert.That(history[1], Does.Contain(first.Decision.ActionId), "The second entry is the decision the agent made.");
        }

        private static void AssertDecided(DecisionResult result)
        {
            Assert.That(result.Telemetry.Outcome, Is.Not.EqualTo(DecisionOutcome.PipelineError),
                $"The live decision failed: {result.Telemetry.ErrorKind}: {result.Telemetry.ErrorMessage}");
            Assert.That(result.Decision, Is.Not.Null, "A result that is not a pipeline error carries a decision.");
        }

        private static IReadOnlyList<string> Allowed(Providers.DecisionRequest request, string field)
        {
            var match = request.Schema.Fields.FirstOrDefault(f => f.Name == field);
            Assert.That(match, Is.Not.Null, $"The schema has no '{field}' field.");
            return match.AllowedValues;
        }

        // One line per timed decision, read back from the test results for the findings entry.
        private static void Report(string test, Stopwatch clock, DecisionResult result)
        {
            var t = result.Telemetry;
            TestContext.Out.WriteLine(
                $"LATENCY test={test} wall={clock.Elapsed.TotalSeconds:F3} provider={t.LatencySeconds:F3} "
              + $"queue={t.QueueWaitSeconds:F3} prompt_tokens={t.PromptTokens} completion_tokens={t.CompletionTokens} "
              + $"model={t.Model} action={result.Decision?.ActionId} target={result.Decision?.TargetId}");
        }
    }
}
