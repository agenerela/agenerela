using System;
using Agenerela;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public sealed class DecisionResultTests
    {
        [Test]
        public void ConstructorStoresDecisionAndTelemetry()
        {
            var decision = new AgentDecision("move to", "player", "moving towards the payer");
            
            var telemetry = new DecisionTelemetry
            {
                Outcome = DecisionOutcome.Correct
            };

            var reuslt = new DecisionResult(decision, telemetry);
        }

    }
}

//Assert.Throws<ArgumentNullException>(() => new DecisionResult(decision, null));