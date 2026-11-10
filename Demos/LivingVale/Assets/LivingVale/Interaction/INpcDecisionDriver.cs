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

        /// <summary>
        /// What a framework result is to the game: its decision, or why there is none. An unreadable
        /// reply is <see cref="NpcReplyFailure.Malformed"/>; every other pipeline error, the server
        /// being unreachable included, is <see cref="NpcReplyFailure.ProviderError"/>.
        /// </summary>
        public static NpcReply From(DecisionResult result)
        {
            if (result == null)
            {
                return Failed(NpcReplyFailure.ProviderError);
            }

            var telemetry = result.Telemetry;
            if (result.Decision == null || (telemetry != null && telemetry.Outcome == DecisionOutcome.PipelineError))
            {
                var kind = telemetry?.ErrorKind;
                bool unreadable = kind == PipelineErrorKind.UnreadableReply || kind == PipelineErrorKind.ProviderMalformedResponse;
                return Failed(unreadable ? NpcReplyFailure.Malformed : NpcReplyFailure.ProviderError);
            }

            return Decided(result.Decision, result);
        }
    }

    /// <summary>
    /// The coordinator's boundary to the NPCs' agents. Deciding and executing stay separate calls,
    /// as in the framework (hard rule 5): each NPC's <see cref="Conversation"/> (#209) asks its agent,
    /// keeps one turn waiting at a time, races the coordinator's timeout and refuses a stale answer,
    /// and the coordinator executes an answer only through it.
    /// </summary>
    public interface INpcDecisionDriver
    {
        /// <summary>Shown in the chat overlay, so scripted replies are never mistaken for a model's.</summary>
        string Label { get; }

        /// <summary>This NPC's conversation with its agent, made the first time it is asked for.</summary>
        Conversation ConversationFor(NpcDescriptor npc);

        /// <summary>What a turn sends with the player's line: the label, the observations, the turn itself as correlation.</summary>
        DecideOptions OptionsFor(NpcTurn turn);

        /// <summary>
        /// Carries out the conversation's current answer. False when the conversation refuses it as
        /// stale, or the agent's re-check or the game's handler refuses it; the coordinator then shows
        /// a game-authored line instead of the statement, so the NPC never claims an action that did
        /// not happen.
        /// </summary>
        bool Execute(NpcTurn turn, DecisionResult result);
    }
}
