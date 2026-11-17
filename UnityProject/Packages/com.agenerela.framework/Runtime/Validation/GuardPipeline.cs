using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>
    /// Runs guards in order over one decision, and records the verdict that stopped it (build plan
    /// §2.5). <see cref="Agent"/> runs it inside <see cref="Agent.DecideAsync"/> with every guard,
    /// and inside <see cref="Agent.Execute"/> with <see cref="SchemaLegalityGuard"/> alone.
    /// </summary>
    /// <remarks>
    /// The first verdict that stops the decision, a rewrite or a reject, stops the pipeline: once
    /// the decision is <c>none</c>, every later guard would pass it anyway, so a run stops at most
    /// once. A <see cref="GuardVerdictKind.ReplaceStatement"/> does not stop it: the guards after it
    /// judge the same move with the new statement (#266 item 4). A guard that throws is not caught here: <see cref="Agent"/> turns it into a
    /// pipeline error of kind <see cref="PipelineErrorKind.GuardFailed"/>.
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
            return Run(guards, input, suffix, out _);
        }

        /// <summary>
        /// As the other <c>Run</c>, and with <paramref name="decision"/> the decision as the guards
        /// left it: the input's, with the statement the last <see cref="GuardVerdictKind.ReplaceStatement"/>
        /// gave, if any. Each replacement is recorded in <see cref="DecisionTelemetry.GuardsFired"/>
        /// and <see cref="DecisionTelemetry.StatementsReplaced"/>; on a call that asked for no
        /// statement it is passed over and recorded nowhere (#266).
        /// </summary>
        public static GuardVerdict Run(IEnumerable<IDecisionGuard> guards, GuardInput input, string suffix, out AgentDecision decision)
        {
            var current = input;
            decision = input.Decision;
            foreach (var guard in guards)
            {
                var verdict = guard.Inspect(current);
                if (verdict.Kind == GuardVerdictKind.Pass)
                {
                    continue;
                }

                string name = NameOf(guard) + suffix;
                var telemetry = input.Telemetry;

                if (verdict.Kind == GuardVerdictKind.ReplaceStatement)
                {
                    if (!current.Options.AskForStatement)
                    {
                        continue;
                    }

                    telemetry.GuardsFired.Add(name);
                    telemetry.StatementsReplaced.Add(new ReplacedStatement(name, verdict.Reason, decision.Statement, verdict.Statement));
                    decision = new AgentDecision(decision.ActionId, decision.TargetId, verdict.Statement);
                    current = current.With(decision);
                    continue;
                }
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
