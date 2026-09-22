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