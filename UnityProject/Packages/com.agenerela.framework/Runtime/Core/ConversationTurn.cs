using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// One turn of a <see cref="Conversation"/>: one call to <see cref="Agent.DecideAsync"/>, from
    /// <see cref="Conversation.Start"/> or <see cref="Conversation.Replace"/> until it is answered or
    /// ends without an answer.
    /// </summary>
    /// <remarks>
    /// <see cref="Status"/> says how the wait ended, and <see cref="IsCurrent"/> whether the turn's
    /// result may still run. They are apart because an answered turn stays
    /// <see cref="TurnStatus.Answered"/> after a newer turn starts, while its result goes stale.
    /// </remarks>
    public sealed class ConversationTurn
    {
        // Continuations run asynchronously, so code awaiting a turn never runs inside the
        // conversation's own bookkeeping, where a turn started from it would find a half-ended one.
        private readonly TaskCompletionSource<TurnStatus> ended =
            new TaskCompletionSource<TurnStatus>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly Conversation conversation;

        // The token the agent decides with: linked to the caller's, and cancelled when the turn
        // ends without an answer. Disposed only once the agent has finished with it.
        private readonly CancellationTokenSource decision;

        // Stops the deadline's delay when the turn ends first. Null when the turn has none.
        private CancellationTokenSource deadline;

        internal ConversationTurn(Conversation conversation, int number, CancellationToken ct)
        {
            this.conversation = conversation;
            Number = number;
            decision = ct.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(ct) : new CancellationTokenSource();
            Token = decision.Token;
        }

        /// <summary>Which turn of its conversation this is, counting from 1.</summary>
        public int Number { get; }

        /// <summary>
        /// <see cref="TurnStatus.Waiting"/> until the agent answers or the turn ends without an
        /// answer; it changes once, and never afterwards.
        /// </summary>
        public TurnStatus Status { get; private set; }

        /// <summary>
        /// The agent's answer, a pipeline error included, when <see cref="Status"/> is
        /// <see cref="TurnStatus.Answered"/>; null otherwise. Kept after it goes stale, for a log, but
        /// <see cref="Conversation.Execute(DecisionResult)"/> refuses it once <see cref="IsCurrent"/>
        /// is false.
        /// </summary>
        public DecisionResult Result { get; private set; }

        /// <summary>
        /// Whether this is its conversation's <see cref="Conversation.Current"/> turn: still waiting,
        /// or answered with no newer turn started and no <see cref="Conversation.Cancel"/> since.
        /// </summary>
        public bool IsCurrent => conversation.Current == this;

        /// <summary>
        /// Completes with <see cref="Status"/> once it is no longer <see cref="TurnStatus.Waiting"/>,
        /// at the deadline at the latest, whether or not the provider has let go of the request.
        /// It never ends cancelled: a cancelled turn completes with <see cref="TurnStatus.Cancelled"/>.
        /// It throws only for <see cref="TurnStatus.Failed"/>.
        /// </summary>
        public Task<TurnStatus> Completion => ended.Task;

        // The agent's token for this turn, kept apart from the source so it can be read after the
        // source is disposed.
        internal CancellationToken Token { get; }

        internal CancellationToken StartDeadline()
        {
            deadline = new CancellationTokenSource();
            return deadline.Token;
        }

        internal void Answer(DecisionResult result)
        {
            Result = result;
            Status = TurnStatus.Answered;
            StopDeadline();
            ended.TrySetResult(TurnStatus.Answered);
        }

        // Ends a waiting turn without an answer. The agent's token last, once the turn is ended:
        // cancelling it runs the provider's and the agent's callbacks right here, and a provider
        // that gives up at once must find the turn already over. A callback that throws is the
        // provider's bug, logged rather than thrown out of Replace or Cancel, which by then have
        // done their work.
        internal void End(TurnStatus status, Exception failure)
        {
            Status = status;
            StopDeadline();

            if (failure != null)
            {
                ended.TrySetException(failure);
            }
            else
            {
                ended.TrySetResult(status);
            }

            try
            {
                decision.Cancel();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // A turn that never reached the agent, because the call itself was a mistake.
        internal void Abandon()
        {
            Status = TurnStatus.Cancelled;
            decision.Dispose();
            ended.TrySetResult(TurnStatus.Cancelled);
        }

        // Once the agent has returned or thrown: nothing holds the token any more.
        internal void ReleaseToken()
        {
            decision.Dispose();
        }

        private void StopDeadline()
        {
            if (deadline == null)
            {
                return;
            }

            deadline.Cancel();
            deadline.Dispose();
            deadline = null;
        }

        /// <summary>The turn's number and status: <c>Turn 3: Answered</c>.</summary>
        public override string ToString()
        {
            return $"Turn {Number}: {Status}";
        }
    }

    /// <summary>How a <see cref="ConversationTurn"/>'s wait ended, in <see cref="ConversationTurn.Status"/>.</summary>
    public enum TurnStatus
    {
        /// <summary>Asked, with no answer yet.</summary>
        Waiting,

        /// <summary>
        /// The agent answered while the turn was waiting. <see cref="ConversationTurn.Result"/> holds
        /// the answer, which may be a pipeline error: the turn ended, the decision did not succeed.
        /// </summary>
        Answered,

        /// <summary><see cref="Conversation.Timeout"/> passed first. Whatever the agent answers later is dropped.</summary>
        TimedOut,

        /// <summary>The caller's token was cancelled, <see cref="Conversation.Cancel"/> ended it, or <see cref="Agent.Reset"/> cancelled its decision.</summary>
        Cancelled,

        /// <summary><see cref="Conversation.Replace"/> started another turn while this one waited.</summary>
        Replaced,

        /// <summary>
        /// <see cref="Agent.DecideAsync"/> threw something other than a cancellation, which it does
        /// only for a bug, since a failure of the provider or the game's code is a pipeline error.
        /// <see cref="ConversationTurn.Completion"/> throws it.
        /// </summary>
        Failed,
    }
}
