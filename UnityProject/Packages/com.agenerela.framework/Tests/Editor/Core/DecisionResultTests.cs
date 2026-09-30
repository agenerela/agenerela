using System;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public sealed class DecisionResultTests
    {
        [Test]
        public void StoresDecisionAndTelemetry()
        {
            // AgentDecision() calls the AgentDecision constructor
            // --> Creates and initializes a new AgentDecision object
            // --> Stores the object in var decision
            var decision = new AgentDecision("move_to", "player", "moving towards the player");
            
            // Creates new DecisionTelemetry Object; Outcome is part of it. Stores it in var telemetry
            var telemetry = new DecisionTelemetry
            {
                Outcome = DecisionOutcome.Correct
            }; 

            // DecisionResult() calls the DecisionResult constructor with both objects
            var result = new DecisionResult(decision, telemetry);

            Assert.That(result.Decision, Is.SameAs(decision));  // Check whether result.Decision is the exact same object decision
            Assert.That(result.Telemetry, Is.SameAs(telemetry)); // Check whether result.Telemetry is the exact same object telemetry
        }

        [Test] // This test checks what happens when telemetry is null
        public void NullTelemetryIsRejected()
        {
            var decision = new AgentDecision("move_to", "player", "moving towards the player");

            // A valid decision is provided, but telemetry is null,
            // so the constructor throws an ArgumentNullException.
            Assert.Throws<ArgumentNullException>(() => new DecisionResult(decision, null));
        }


        // One test method with four different input values: every outcome except PipelineError
        [TestCase(DecisionOutcome.Correct)]
        [TestCase(DecisionOutcome.WrongLegalAction)]
        [TestCase(DecisionOutcome.ContainedByGuard)]
        [TestCase(DecisionOutcome.RejectedWhenActionExpected)]
        public void NullDecisionNonPipelineErrors(DecisionOutcome outcome)
        {
            var telemetry = new DecisionTelemetry
            {
                Outcome = outcome
            };

            // A null decision is provided, but valid telemetry
            Assert.Throws<ArgumentException>( () => new DecisionResult(null, telemetry));
        }


        // Checks what happens when:
        // decision is null
        // outcome is null, as telemetry holds at runtime before the eval harness fills it in
        [Test]
        public void NullDecisionAndNullOutcome()
        {
            var telemetry = new DecisionTelemetry
            {
                Outcome = null
            };

            Assert.Throws<ArgumentException>( () => new DecisionResult(null, telemetry));
        }

        // Checks the one situation where a null decision is allowed
        [Test]
        public void NullDecisionWithPipelineErrorIsAccepted()
        {
            var telemetry = new DecisionTelemetry
            {
                Outcome = DecisionOutcome.PipelineError
            };

            var result = new DecisionResult(null, telemetry);

            Assert.That(result.Decision, Is.Null);
            Assert.That(result.Telemetry, Is.SameAs(telemetry));
        }
    }
}
