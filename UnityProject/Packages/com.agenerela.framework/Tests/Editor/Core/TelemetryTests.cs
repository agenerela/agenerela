using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public class TelemetryTests 
    {
        [Test]=
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
        public void NewTelemetryIsReadyToFill()
        {
            var t = new DecisionTelemetry();
            Assert.That(t.Outcome, Is.Null, "Only the eval harness knows the right answer.");
            Assert.That(t.GuardsFired, Is.Not.Null.And.Empty);
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
        }
    }
}