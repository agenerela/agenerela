// Commented out until #2 merges (PR #39). DecisionResult wraps AgentDecision, which #2 adds,
// and without it the whole Agenerela assembly fails to compile (CS0246). The code is correct
// as written: once #39 lands, delete this comment and the two comment markers around it.

/*
using System;

namespace Agenerela
{
    public sealed class DecisionResult
    {
        public AgentDecision Decision { get; }
        public DecisionTelemetry Telemetry { get; }

        public DecisionResult(
            AgentDecision decision,
            DecisionTelemetry telemetry)
        {
            if (telemetry == null)
            {
                throw new ArgumentNullException(nameof(telemetry));
            }

            if (decision == null &&
                telemetry.Outcome != DecisionOutcome.PipelineError)
            {
                throw new ArgumentException(
                    "Decision can be null only when the outcome is PipelineError.",
                    nameof(decision));
            }

            Decision = decision;
            Telemetry = telemetry;
        }
    }
}
*/
