using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Agenerela;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public sealed class DecisionResultTests
    {
        [Test]
        public void StoresDecisionAndTelemetry()
        {
            // AgentDecision() calls the AgentDecison constructor 
            // --> Creates and initialize new AgentDecison Object 
            // --> Stores the object in var decison 
            var decision = new AgentDecision("move to", "player", "moving towards the payer");
            
            // Creates new DecisionTelemetry Object; Outcome is part of it. Stores it in var telemetry
            var telemetry = new DecisionTelemetry
            {
                Outcome = DecisionOutcome.Correct
            }; 

            // DecisionResult() calls 
            var reuslt = new DecisionResult(decision, telemetry);
            
            Assert.That(reuslt.Decision, Is.SameAs(decision));  // Check whether result.Decision is the exact same object decision
            Assert.That(reuslt.Telemetry, Is.SameAs(telemetry)); // Check whether result.Telemetry is the exact same object telemetry
        }

        [Test] // This test checks what happens when telemetry is null
        public void NullTelemetry()
        {
            var decision = new AgentDecision("move to", "player", "moving towards the payer");

            // A valid decision is provided, but telemetry is null,
            // so the constructor throws an ArgumentNullException.
            Assert.Throws<ArgumentException>(() => new DecisionResult(decision, null)); 
        }


        // One test method with four different input values; ALL NON-pipelin
        [Test]
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
        // telemetry is null
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
        public void NullDecisionWIthPipelineErrorsIsAccepted()
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
