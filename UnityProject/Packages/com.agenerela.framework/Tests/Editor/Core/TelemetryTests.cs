using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public class TelemetryTests 
    {
        [Test]
        public void OutcomeHasExactlyTheFiveClassesInOrder()
        {
            // Phase 4 reports key on these names, and a serialized enum is its position, so
            // adding, removing or reordering one has to be a deliberate change.
            Assert.That(Enum.GetNames(typeof(DecisionOutcome)), Is.EqualTo(new[]
            {
                "Correct", "WrongLegalAction", "ContainedByGuard",
                "RejectedWhenActionExpected", "PipelineError",
            }));
        }

        [Test]
        public void PipelineErrorKindHasItsTenKindsInOrder()
        {
            // Serialized as positions too, so a new kind goes at the end (#87).
            Assert.That(Enum.GetNames(typeof(PipelineErrorKind)), Is.EqualTo(new[]
            {
                "RequestPreparation", "ProviderUnreachable", "ModelNotFound", "ProviderTimeout",
                "ProviderRejected", "ProviderServerError", "ProviderMalformedResponse",
                "ProviderFailed", "UnreadableReply", "GuardFailed",
            }));
        }

        [Test]
        public void NewTelemetryIsReadyToFill()
        {
            var t = new DecisionTelemetry();
            Assert.That(t.Outcome, Is.Null, "Only the eval harness knows the right answer.");
            Assert.That(t.GuardsFired, Is.Not.Null.And.Empty);
            Assert.That(t.TargetsDropped, Is.Not.Null.And.Empty);
            Assert.That(t.ErrorKind, Is.Null, "No error until a decision fails.");
            Assert.That(t.ErrorMessage, Is.Null);
        }

        [Test]
        public void SurvivesANewtonsoftRoundTrip()
        {
            var t = new DecisionTelemetry { PromptTokens = 143, Outcome = DecisionOutcome.ContainedByGuard };
            t.GuardsFired.Add("SchemaLegalityGuard");
            t.GuardsFired.Add("TargetGroundingGuard");

            var back = JsonConvert.DeserializeObject<DecisionTelemetry>(JsonConvert.SerializeObject(t));

            Assert.That(back.Outcome, Is.EqualTo(DecisionOutcome.ContainedByGuard));
            Assert.That(back.GuardsFired, Is.EqualTo(new[] { "SchemaLegalityGuard", "TargetGroundingGuard" }));
            Assert.That(back.PromptTokens, Is.EqualTo(143));
            Assert.That(back.ErrorKind, Is.Null);
            Assert.That(back.ErrorMessage, Is.Null);
        }

        [Test]
        public void ThePipelineErrorSurvivesANewtonsoftRoundTrip()
        {
            const string Message = "Agent 'Village Guard' could not decide. The provider failed. "
                + "ProviderException: Model 'qwen3.5:2b' is not installed. Run `ollama pull qwen3.5:2b`.";
            var t = new DecisionTelemetry
            {
                Outcome = DecisionOutcome.PipelineError,
                ErrorKind = PipelineErrorKind.ModelNotFound,
                ErrorMessage = Message,
            };

            var back = JsonConvert.DeserializeObject<DecisionTelemetry>(JsonConvert.SerializeObject(t));

            Assert.That(back.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(back.ErrorKind, Is.EqualTo(PipelineErrorKind.ModelNotFound));
            Assert.That(back.ErrorMessage, Is.EqualTo(Message));
        }
    }
}