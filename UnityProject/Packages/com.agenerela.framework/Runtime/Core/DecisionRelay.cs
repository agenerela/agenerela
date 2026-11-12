using System;
using System.Threading;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// Turns one agent's decision into another agent's stimulus, in one chain (#190, G4): Odile calls
    /// a guard, and the guard is asked with her words, and the decision log shows both decisions, and
    /// the guard's chase after them, under one <see cref="DecisionChain.Id"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optional, and only a convenience over <see cref="Agent.DecideAsync"/>: the game still decides
    /// when to relay, to whom and with what words, from a handler or right after
    /// <see cref="Agent.Execute(DecisionResult)"/> (DR-008). There is no scheduler and no fan-out;
    /// relay to two agents with two calls.
    /// </para>
    /// <para>
    /// What the receiving model reads is the stimulus and label the game passed, unchanged: the
    /// chain is carried beside the call, in <see cref="DecideOptions.Chain"/>, and never reaches the
    /// prompt.
    /// </para>
    /// </remarks>
    public static class DecisionRelay
    {
        /// <summary>The default most relays a chain may have, 8: enough for any story, few enough to stop two agents relaying to each other.</summary>
        public const int DefaultMaxHops = 8;

        /// <summary>
        /// Asks <paramref name="to"/> to decide on <paramref name="stimulus"/>, labelled
        /// <paramref name="label"/>, as the next hop of the chain <paramref name="decision"/> was made in,
        /// or of a new chain when it was made in none. Returns the receiving decision, which the
        /// caller executes as any other.
        /// </summary>
        /// <param name="from">The agent that made <paramref name="decision"/>; its <see cref="Agent.Id"/> is recorded as the hop's source.</param>
        /// <param name="decision">The decision being relayed, as <see cref="Agent.DecideAsync"/> returned it. Only its chain is read.</param>
        /// <param name="to">The agent asked. It may be <paramref name="from"/> itself.</param>
        /// <param name="stimulus">What <paramref name="to"/> is told, sent as it is.</param>
        /// <param name="label">How the stimulus is labelled, such as <c>Merchant</c>; it replaces the options' <see cref="DecideOptions.StimulusLabel"/>.</param>
        /// <param name="options">The rest of the call's options, copied; null uses the defaults. Its <see cref="DecideOptions.Chain"/> is replaced, and its <see cref="DecideOptions.Correlation"/> kept.</param>
        /// <param name="maxHops">The most relays the chain may have. A relay past it is refused, not sent.</param>
        /// <param name="ct">Cancels the receiving decision, as for <see cref="Agent.DecideAsync"/>.</param>
        /// <returns>
        /// The receiving decision. Past <paramref name="maxHops"/>, a result with no decision,
        /// <see cref="PipelineErrorKind.ChainTooLong"/>, which was never sent, is logged nowhere, and
        /// is raised on <see cref="DecisionFeed.Recorded"/> as <paramref name="to"/>'s, so the log
        /// shows where the chain stopped.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="from"/>, <paramref name="decision"/> or <paramref name="to"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="label"/> is empty, or the options are a mistake, as for <see cref="Agent.DecideAsync"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxHops"/> is below 1.</exception>
        public static async Awaitable<DecisionResult> RelayAsync(
            this Agent from, DecisionResult decision, Agent to, string stimulus, string label,
            DecideOptions options = null, int maxHops = DefaultMaxHops, CancellationToken ct = default)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (to == null) throw new ArgumentNullException(nameof(to));
            if (maxHops < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHops), maxHops, "A relay needs at least one hop.");
            }

            var call = options == null ? new DecideOptions() : options.Copy();
            call.StimulusLabel = label;
            var source = DecisionChain.Of(decision);
            call.Chain = source != null ? source.Next(from.Id) : new DecisionChain(DecisionChain.NewId(), 1, from.Id);

            if (call.Chain.Hop > maxHops)
            {
                return to.Refuse(call, PipelineErrorKind.ChainTooLong,
                    $"Not relayed: chain {call.Chain.Id} would reach hop {call.Chain.Hop}, past its cap of {maxHops}.");
            }

            return await to.DecideAsync(stimulus, call, ct);
        }
    }
}
