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
            Assert.That(reuslt.Telemetry, Is.SmaeAs(telemetry)); // Check whether result.Telemetry is the exact same object telemetry
        }

        [Test]
        public void NullTelemetry()
        {
            var decision = new AgentDecision("move to", "player", "moving towards the payer");

            Asert.Throws<ArgumentException>(() => new DecisionResults(decision, null));
        }

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

            Assert.Throws<ArgumentException>( () => new DecisionResult(null, telemetry));
        }

        [Test]
        public void NullDecisionAndNullOutcome()
        {
            var telemetry = new DecisionTelemetry
            {
                Outcome = null
            };

            Assert.Throws<ArgumentException>( () => new DecisionResult(null, telemetry));
        }

        [Test]
        public void NullDecisionWIthPipelineErrorsIsAccepted()
        {
            var telemetry = new DecisionTelemetry
            {
                Outcome = DecisionOutcome.PipelineError
            };

            var result = new DecisionResult(null, telemetry);

            Assert.That(result.Decision, IsBoxed.Null);
            Assert.That(result.Telemetry, Is.SameAs(telemetry));
        }
    }
}
