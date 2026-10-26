using System;

namespace Agenerela
{
    /// <summary>
    /// What one guard made of a decision (build plan §2.5): let it through, turn it into
    /// <c>none</c>, or reject it. A guard returns one from <see cref="IDecisionGuard.Inspect"/>,
    /// made with <see cref="Pass"/>, <see cref="RewriteToNone"/> or <see cref="Reject"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pipeline stops at the first verdict that is not <see cref="GuardVerdictKind.Pass"/>, and
    /// both kinds that stop it leave the agent doing <c>none</c> with <c>no_target</c>. They differ
    /// in what went wrong, which the eval harness needs to tell apart. A rewrite is a contained
    /// refusal: the model chose something legal, and the guard judged it wrong, so the agent does
    /// nothing and <see cref="Agent.Execute"/> returns true. A reject means the provider broke the
    /// contract, such as an action that was never offered, so <see cref="Agent.Execute"/> returns
    /// false.
    /// </para>
    /// <para>
    /// <see cref="Guard"/> is filled in by the pipeline, not by the guard, so a verdict always
    /// names the guard that actually returned it.
    /// </para>
    /// </remarks>
    public readonly struct GuardVerdict
    {
        private GuardVerdict(GuardVerdictKind kind, string reason, string guard)
        {
            Kind = kind;
            Reason = reason;
            Guard = guard;
        }

        /// <summary>The decision may go on to the next guard. Also what <c>default</c> is.</summary>
        public static GuardVerdict Pass => default;

        /// <summary>
        /// The decision is legal, but wrong: the agent does <c>none</c> instead. A contained
        /// refusal, not an error.
        /// </summary>
        /// <param name="reason">Why, in words a developer reads in telemetry and logs.</param>
        /// <exception cref="ArgumentException"><paramref name="reason"/> is null, empty or whitespace.</exception>
        public static GuardVerdict RewriteToNone(string reason)
        {
            return new GuardVerdict(GuardVerdictKind.RewriteToNone, Required(reason), null);
        }

        /// <summary>
        /// The provider broke the contract: an action not offered, a target not registered, an
        /// action that needs a target answered without one. The agent does <c>none</c>, and
        /// <see cref="Agent.Execute"/> returns false.
        /// </summary>
        /// <param name="reason">Why, in words a developer reads in telemetry and logs.</param>
        /// <exception cref="ArgumentException"><paramref name="reason"/> is null, empty or whitespace.</exception>
        public static GuardVerdict Reject(string reason)
        {
            return new GuardVerdict(GuardVerdictKind.Reject, Required(reason), null);
        }

        /// <summary>Pass, rewrite to <c>none</c>, or reject.</summary>
        public GuardVerdictKind Kind { get; }

        /// <summary>Why the guard stopped the decision. Null for <see cref="Pass"/>.</summary>
        public string Reason { get; }

        /// <summary>
        /// The <see cref="IDecisionGuard.Name"/> of the guard that returned this verdict, as
        /// <see cref="DecisionTelemetry.GuardsFired"/> records it. Null until the pipeline has run
        /// it, and for <see cref="Pass"/>.
        /// </summary>
        public string Guard { get; }

        // The pipeline's stamp: the same verdict, naming the guard that returned it.
        internal GuardVerdict From(string guard)
        {
            return new GuardVerdict(Kind, Reason, guard);
        }

        private static string Required(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A guard that stops a decision says why.", nameof(reason));
            }

            return reason;
        }
    }

    // What a guard decided, recorded in DecisionTelemetry.Verdict. Serialized as its position,
    // like DecisionOutcome, so a new kind goes at the end.
    public enum GuardVerdictKind
    {
        // The decision goes on to the next guard
        Pass,

        // Legal but judged wrong: the agent does none instead, a contained refusal
        RewriteToNone,

        // The provider broke the contract: the agent does none, and Execute returns false
        Reject
    }
}
