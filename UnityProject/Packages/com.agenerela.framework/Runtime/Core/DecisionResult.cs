using System;

namespace Agenerela
{
    /// <summary>
    /// What <see cref="Agent.DecideAsync"/> returns: the decision the agent will act on, what the
    /// model itself answered, the guard verdict between the two, and the telemetry of the request.
    /// </summary>
    public sealed class DecisionResult
    {
        /// <summary>
        /// What the agent will do, after the guards. <c>none</c> with <c>no_target</c> when a guard
        /// rewrote or rejected the model's answer. Null only for a pipeline error.
        /// </summary>
        public AgentDecision Decision { get; }

        /// <summary>
        /// What the model said, as parsed, before any guard. The same object as
        /// <see cref="Decision"/> when every guard passed; null for a pipeline error.
        /// </summary>
        public AgentDecision ModelDecision { get; }

        /// <summary>
        /// The verdict that stopped the model's answer inside <see cref="Agent.DecideAsync"/>: the
        /// guard's name, the kind and the reason. Null when every guard passed, and for a pipeline
        /// error. Never changed afterwards: a refusal by <see cref="Agent.Execute"/>'s re-check is
        /// recorded in <see cref="Telemetry"/> instead.
        /// </summary>
        public GuardVerdict? Verdict { get; }

        /// <summary>The request's telemetry, guard verdicts included.</summary>
        public DecisionTelemetry Telemetry { get; }

        /// <summary>
        /// A result no guard stopped: <see cref="ModelDecision"/> is <paramref name="decision"/>
        /// and <see cref="Verdict"/> is null.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="telemetry"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="decision"/> is null and the outcome is not a pipeline error.
        /// </exception>
        public DecisionResult(
            AgentDecision decision,
            DecisionTelemetry telemetry)
            : this(decision, telemetry, decision, null)
        {
        }

        // A result a guard stopped, or none did when verdict is null. The guards' own rule, that a
        // stopped decision becomes none, is Agent's; this only keeps the pair consistent.
        internal DecisionResult(
            AgentDecision decision,
            DecisionTelemetry telemetry,
            AgentDecision modelDecision,
            GuardVerdict? verdict)
        {
            if (telemetry == null)
            {
                throw new ArgumentNullException(nameof(telemetry));  // If telemetry == null --> record 'telemetry' into the exception
            }

            if (decision == null &&
                telemetry.Outcome != DecisionOutcome.PipelineError)
            {
                throw new ArgumentException(
                    "Decision can be null only when the outcome is PipelineError.",  // decision can equal to null only if there is pipline error
                    nameof(decision));
            }

            if (verdict.HasValue && (verdict.Value.Kind == GuardVerdictKind.Pass || decision == null || modelDecision == null))
            {
                throw new ArgumentException(
                    "A verdict is recorded only for a decision a guard stopped.",
                    nameof(verdict));
            }

            Decision = decision;
            ModelDecision = modelDecision;
            Verdict = verdict;
            Telemetry = telemetry;
        }
    }
}
