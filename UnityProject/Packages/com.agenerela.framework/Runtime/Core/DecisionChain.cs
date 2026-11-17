using System;

namespace Agenerela
{
    /// <summary>
    /// Which chain of relayed decisions a call belongs to (#190, G4): an id every decision in the
    /// chain shares, how many relays from its start this one is, and the <see cref="Agent.Id"/> of
    /// the agent whose decision was relayed to it. Set on <see cref="DecideOptions.Chain"/>, usually
    /// by <see cref="DecisionRelay.RelayAsync"/>, and recorded in the decision's telemetry, so a
    /// decision log groups "thief → Odile → Wren → chase" as one chain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Immutable. The framework only carries it: it never reaches the prompt or the provider, so a
    /// relayed decision is asked exactly what the developer passed, and it says nothing about why the
    /// agent is asked (DR-008). A call with no chain, the default, records none.
    /// </para>
    /// <para>
    /// A chain starts at the first relay, unless the game starts it earlier: pass
    /// <see cref="Start"/> on the call that begins it, such as Odile's when she sees the thief, and
    /// that decision is in the chain too, at hop 0.
    /// </para>
    /// <para>
    /// A <see cref="DecisionRound"/> is one chain too: every call it makes without a chain of its own
    /// starts one with the round's <see cref="DecisionRound.Id"/>, so a turn's decisions share an id,
    /// each at hop 0, and a decision relayed from one of them continues it (#216, item 2).
    /// </para>
    /// </remarks>
    public sealed class DecisionChain
    {
        /// <summary>
        /// A chain whose first decision is this call, at hop 0 with no source.
        /// </summary>
        /// <param name="id">The chain's id; null or empty makes a new one, unique to this chain.</param>
        public static DecisionChain Start(string id = null)
        {
            return new DecisionChain(string.IsNullOrEmpty(id) ? NewId() : id, 0, null);
        }

        /// <summary>A chain at a hop of the caller's choosing, as a game restoring a saved chain would make it.</summary>
        /// <param name="id">The chain's id. Any non-empty string.</param>
        /// <param name="hop">Relays from the chain's start: 0 for its first decision.</param>
        /// <param name="sourceAgentId">The <see cref="Agent.Id"/> of the agent whose decision was relayed, or null.</param>
        /// <exception cref="ArgumentException"><paramref name="id"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="hop"/> is negative.</exception>
        public DecisionChain(string id, int hop, string sourceAgentId)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("A decision chain needs an id.", nameof(id));
            }

            if (hop < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hop), hop, "A chain's hop count cannot be negative.");
            }

            Id = id;
            Hop = hop;
            SourceAgentId = sourceAgentId;
        }

        /// <summary>The id every decision in the chain shares.</summary>
        public string Id { get; }

        /// <summary>How many relays from the chain's start this decision is: 0 for its first, 1 for the first relay.</summary>
        public int Hop { get; }

        /// <summary>
        /// The <see cref="Agent.Id"/> of the agent whose decision was relayed to this one; null at the
        /// chain's start, and when that agent has no id.
        /// </summary>
        public string SourceAgentId { get; }

        /// <summary>The next hop of this chain: the same id, one hop further, from <paramref name="sourceAgentId"/>.</summary>
        public DecisionChain Next(string sourceAgentId)
        {
            return new DecisionChain(Id, Hop + 1, sourceAgentId);
        }

        /// <summary>The chain a decision was made in, read from its telemetry; null for a decision in none.</summary>
        public static DecisionChain Of(DecisionResult result)
        {
            var telemetry = result?.Telemetry;
            return telemetry == null || string.IsNullOrEmpty(telemetry.ChainId)
                ? null
                : new DecisionChain(telemetry.ChainId, Math.Max(0, telemetry.ChainHop), Empty(telemetry.ChainSource));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return SourceAgentId == null ? $"{Id}#{Hop}" : $"{Id}#{Hop} from {SourceAgentId}";
        }

        internal static string NewId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private static string Empty(string text)
        {
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }
}
