using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>
    /// Runs guards in order over one decision, and records the verdict that stopped it (build plan
    /// §2.5). <see cref="Agent"/> runs it inside <see cref="Agent.DecideAsync"/> with every guard,
    /// and inside <see cref="Agent.Execute"/> with <see cref="SchemaLegalityGuard"/> alone.
    /// </summary>
    /// <remarks>
    /// The first verdict that is not <see cref="GuardVerdictKind.Pass"/> stops the pipeline: once
    /// the decision is <c>none</c>, every later guard would pass it anyway. So a run fires at most
    /// one guard. A guard that throws is not caught here; #108 decides what that means.
    /// </remarks>
    internal static class GuardPipeline
    {
        /// <summary>
        /// Inspects <paramref name="input"/> with each of <paramref name="guards"/>, in order, until
        /// one does not pass it. That guard's name, followed by <paramref name="suffix"/>, is added to
        /// <see cref="DecisionTelemetry.GuardsFired"/>, and its verdict and reason are recorded in
        /// <see cref="DecisionTelemetry.Verdict"/> and <see cref="DecisionTelemetry.GuardReason"/>.
        /// </summary>
        /// <returns>
        /// That verdict, its <see cref="GuardVerdict.Guard"/> set to the name recorded, or
        /// <see cref="GuardVerdict.Pass"/> when every guard passed, recording nothing.
        /// </returns>
        public static GuardVerdict Run(IEnumerable<IDecisionGuard> guards, GuardInput input, string suffix = "")
        {
            foreach (var guard in guards)
            {
                var verdict = guard.Inspect(input);
                if (verdict.Kind == GuardVerdictKind.Pass)
                {
                    continue;
                }

                string name = NameOf(guard) + suffix;
                var telemetry = input.Telemetry;
                telemetry.GuardsFired.Add(name);
                telemetry.Verdict = verdict.Kind;
                telemetry.GuardReason = verdict.Reason;
                return verdict.From(name);
            }

            return GuardVerdict.Pass;
        }

        // A guard with no name is still told apart in telemetry, by its type.
        private static string NameOf(IDecisionGuard guard)
        {
            return string.IsNullOrWhiteSpace(guard.Name) ? guard.GetType().Name : guard.Name;
        }
    }
}
