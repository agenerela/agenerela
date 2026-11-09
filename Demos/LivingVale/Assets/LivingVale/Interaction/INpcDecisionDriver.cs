using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;

namespace LivingVale.Interaction
{
    /// <summary>One player turn sent to a driver.</summary>
    public sealed class NpcTurn
    {
        /// <summary>Unique per coordinator; a reply is applied only while it is still the pending request.</summary>
        public long RequestId { get; }
        public NpcDescriptor Npc { get; }
        public string PlayerLine { get; }

        /// <summary>The conversation before this turn, oldest first. Excludes <see cref="PlayerLine"/>.</summary>
        public IReadOnlyList<ChatLine> History { get; }

        /// <summary>
        /// The conversation generation the turn started in (<see cref="ConversationCoordinator.Generation"/>).
        /// Game handlers pass it on, so a quote made from this turn belongs to this conversation only.
        /// Zero when the turn was made without one, as G1 code did.
        /// </summary>
        public long Generation { get; }

        public NpcTurn(long requestId, NpcDescriptor npc, string playerLine, IReadOnlyList<ChatLine> history)
            : this(requestId, npc, playerLine, history, 0)
        {
        }

        public NpcTurn(long requestId, NpcDescriptor npc, string playerLine, IReadOnlyList<ChatLine> history, long generation)
        {
            RequestId = requestId;
            Npc = npc;
            PlayerLine = playerLine;
            History = history;
            Generation = generation;
        }
    }

    /// <summary>Why a turn produced no decision.</summary>
    public enum NpcReplyFailure
    {
        None,

        /// <summary>The reply could not be read as a decision.</summary>
        Malformed,

        /// <summary>The backend failed: unreachable, errored, refused.</summary>
        ProviderError
    }

    /// <summary>
    /// What a driver returns: a decision, or the reason there is none. A timeout or a cancellation
    /// is not a reply: the coordinator owns both and ends the turn itself.
    /// </summary>
    public sealed class NpcReply
    {
        /// <summary>The decision, in the framework's own type; null when <see cref="Failure"/> is set.</summary>
        public AgentDecision Decision { get; }
        public NpcReplyFailure Failure { get; }

        /// <summary>Driver-specific payload <see cref="INpcDecisionDriver.Execute"/> needs, such as a framework <c>DecisionResult</c>.</summary>
        public object DriverState { get; }

        private NpcReply(AgentDecision decision, NpcReplyFailure failure, object driverState)
        {
            Decision = decision;
            Failure = failure;
            DriverState = driverState;
        }

        public static NpcReply Decided(AgentDecision decision, object driverState = null) =>
            new NpcReply(decision ?? throw new System.ArgumentNullException(nameof(decision)), NpcReplyFailure.None, driverState);

        public static NpcReply Failed(NpcReplyFailure failure) =>
            new NpcReply(null, failure == NpcReplyFailure.None ? NpcReplyFailure.ProviderError : failure, null);
    }

    /// <summary>
    /// The game's boundary to whatever decides what an NPC does. Deciding and executing are
    /// separate calls, as in the framework (hard rule 5): the coordinator executes a decision only
    /// after checking it still belongs to the open conversation.
    /// </summary>
    /// <remarks>
    /// <see cref="ScriptedNpcDecisionDriver"/> is the G1–G4 development fixture. The G5 driver
    /// will wrap <c>Agent.DecideAsync</c> and <c>Agent.Execute</c>, keeping the
    /// <c>DecisionResult</c> in <see cref="NpcReply.DriverState"/>.
    /// </remarks>
    public interface INpcDecisionDriver
    {
        /// <summary>Shown in the chat overlay, so scripted replies are never mistaken for a model's.</summary>
        string Label { get; }

        /// <summary>
        /// Decides this turn. Honour <paramref name="ct"/>: the coordinator cancels it on timeout,
        /// close, switch or explicit cancel, and ignores whatever arrives afterwards anyway.
        /// </summary>
        Task<NpcReply> DecideAsync(NpcTurn turn, CancellationToken ct);

        /// <summary>
        /// Carries out a decision that is still current. Returns false when the driver's own
        /// re-check refuses it; the coordinator then shows a game-authored line instead of the
        /// statement, so the NPC never claims an action that did not happen.
        /// </summary>
        bool Execute(NpcTurn turn, NpcReply reply);
    }
}
