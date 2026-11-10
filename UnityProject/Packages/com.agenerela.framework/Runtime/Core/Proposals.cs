using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// Optional plumbing for "the agent proposes, the player confirms" (#190, Living Vale #16): it
    /// holds a <see cref="DecisionResult"/> instead of carrying it out, and carries it out through
    /// <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> only when the player confirms, so
    /// the check at confirm is the agent's own re-check, legality, categories and per-target
    /// availability (<see cref="ITargetAvailability"/>) included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing in the framework needs it, and it decides nothing about when or why the agent is
    /// asked (DR-008), nor which actions want a confirmation: the game lists them in
    /// <see cref="HeldActions"/>, or calls <see cref="Hold"/> itself. It changes nothing in
    /// <see cref="Agent"/>, <see cref="ActionResult"/> or <see cref="ExecutionStatus"/>, and nothing
    /// that reaches the model: the agent decides exactly as it would without it. Delete it, and an
    /// agent works as before.
    /// </para>
    /// <para>
    /// <b>Memory.</b> A held proposal is remembered when it is held, once: the agent did propose it,
    /// and the player saw the offer, whatever they answer. Under <see cref="Agenerela.MemoryCommit.OnDecide"/>
    /// it was already remembered as <see cref="Agent.DecideAsync"/> returned it; under
    /// <see cref="Agenerela.MemoryCommit.OnExecute"/>, <see cref="Hold"/> commits it
    /// (<see cref="Agent.Commit"/>). Either way <see cref="Agent.Execute(DecisionResult)"/> at confirm
    /// finds it settled and does not remember it again, and declining it does not forget it.
    /// </para>
    /// <para>
    /// <b>Telemetry.</b> <see cref="DecisionTelemetry.Execution"/> stays null while a proposal waits,
    /// so the decision feed already tells an offer from an accepted one: the
    /// <see cref="DecisionStage.Executed"/> record comes only at confirm. A proposal declined,
    /// superseded or reset is never executed and leaves its telemetry as it was.
    /// </para>
    /// <para>
    /// <b>Reset.</b> <see cref="Agent.Reset"/> leaves nothing held: every pending proposal ends as
    /// <see cref="ProposalStatus.Reset"/> during the reset, and <see cref="Changed"/> is raised. A
    /// <see cref="Changed"/> handler that throws then is logged, so it cannot fail the reset.
    /// </para>
    /// <para>
    /// Plain C# with no scene. Call it from the main thread, as the agent is.
    /// </para>
    /// <para>
    /// <b>Extension point:</b> whether a proposal still stands for reasons only the game knows, the
    /// conversation it was made in say, is <see cref="StillValid"/>. Whether the action can still be
    /// done to its target belongs in the handler's <see cref="ITargetAvailability"/>, which the agent
    /// asks again at confirm.
    /// </para>
    /// </remarks>
    public sealed class Proposals
    {
        private readonly List<Proposal> pending = new List<Proposal>();
        private int count;

        // The agent's reset token this helper watches; a new one after each reset, from the next Hold.
        private CancellationToken watched;
        private CancellationTokenRegistration watch;
        private bool watching;

        // The proposal Confirm is carrying out, while its handler runs; null otherwise.
        private Proposal confirming;

        /// <summary>A helper for <paramref name="agent"/>'s proposals.</summary>
        /// <param name="agent">The agent whose decisions are held.</param>
        /// <param name="heldActions">The action ids <see cref="Submit(DecisionResult)"/> holds rather than carries out at once. Null or empty for none.</param>
        /// <exception cref="ArgumentNullException"><paramref name="agent"/> is null.</exception>
        public Proposals(Agent agent, IEnumerable<string> heldActions = null)
        {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            HeldActions = heldActions == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(heldActions, StringComparer.Ordinal);
        }

        /// <summary>The agent whose decisions are held and carried out.</summary>
        public Agent Agent { get; }

        /// <summary>
        /// The action ids <see cref="Submit(DecisionResult)"/> holds for the player. Any other decision
        /// it carries out at once. Ordinal, case-sensitive, as action ids are.
        /// </summary>
        public ISet<string> HeldActions { get; }

        /// <summary>
        /// When true, at most one proposal waits: holding a new one supersedes those pending
        /// (<see cref="ProposalStatus.Superseded"/>), as a new card replaces the old. False, the
        /// default, keeps every proposal until it is settled. Read at each <see cref="Hold"/>.
        /// </summary>
        public bool OnePending { get; set; }

        /// <summary>
        /// The game's own say on whether a proposal still stands, asked at
        /// <see cref="Confirm(Proposal, out ActionResult)"/> before the agent's re-check: false
        /// refuses it, as <see cref="ProposalStatus.Refused"/>. For example, that it was made in the
        /// conversation still open, read from <see cref="Proposal.Correlation"/>. Null, the default,
        /// asks nothing.
        /// </summary>
        /// <remarks>Never asked about the action and target themselves: the agent's re-check does that.</remarks>
        public Func<Proposal, bool> StillValid { get; set; }

        /// <summary>
        /// Raised after the set of pending proposals changes: one held, confirmed, declined,
        /// superseded or reset. Once per call, however many proposals it settled.
        /// </summary>
        public event Action<Proposals> Changed;

        /// <summary>The proposals waiting for the player, oldest first. A copy: settling one while reading it is safe.</summary>
        public IReadOnlyList<Proposal> Pending => new List<Proposal>(pending).AsReadOnly();

        /// <summary>The newest proposal waiting, or null. With <see cref="OnePending"/>, the only one.</summary>
        public Proposal Latest => pending.Count == 0 ? null : pending[pending.Count - 1];

        /// <summary>
        /// Holds <paramref name="result"/> when its action is in <see cref="HeldActions"/> and returns
        /// the proposal; otherwise carries it out at once through <see cref="Agent.Execute(DecisionResult)"/>
        /// and returns null.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        public Proposal Submit(DecisionResult result)
        {
            return Submit(result, out _);
        }

        /// <summary>
        /// As <see cref="Submit(DecisionResult)"/>, and says what carrying it out did.
        /// </summary>
        /// <param name="result">The decision, as <see cref="Agent.DecideAsync"/> returned it.</param>
        /// <param name="outcome">
        /// What <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> reported when the
        /// decision was carried out at once; null when it was held. A pipeline error, a rejected
        /// answer and <c>none</c> are never held, so they are carried out, which refuses or does
        /// nothing as <see cref="Agent.Execute(DecisionResult)"/> always does.
        /// </param>
        /// <returns>The proposal when held, or null.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        public Proposal Submit(DecisionResult result, out ActionResult? outcome)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (Holdable(result) && HeldActions.Contains(result.Decision.ActionId))
            {
                outcome = null;
                return Hold(result);
            }

            Agent.Execute(result, out var done);
            outcome = done;
            return null;
        }

        /// <summary>
        /// Holds <paramref name="result"/> for the player, whatever its action, and returns the
        /// proposal. Nothing runs. Under <see cref="Agenerela.MemoryCommit.OnExecute"/> the decision is
        /// remembered now (see the class remarks). With <see cref="OnePending"/>, the proposals already
        /// waiting are superseded. Holding a result already pending here returns its proposal and
        /// changes nothing.
        /// </summary>
        /// <remarks>
        /// The decision is not checked again here: it was legal when the agent made it, and the agent
        /// checks it again at <see cref="Confirm(Proposal, out ActionResult)"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="result"/> has nothing to propose: a pipeline error, or a decision of
        /// <c>none</c>, a rejected answer's included. Or it was decided by another agent with memory
        /// held for it (<see cref="Agent.Commit"/> throws). Nothing is held.
        /// </exception>
        public Proposal Hold(DecisionResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!Holdable(result))
            {
                throw new ArgumentException(
                    "There is nothing to propose: the result is a pipeline error, or its decision is none.",
                    nameof(result));
            }

            foreach (var waiting in pending)
            {
                if (ReferenceEquals(waiting.Result, result))
                {
                    return waiting;
                }
            }

            // First, so a result of another agent's throws before anything changes. False, harmlessly,
            // for one already remembered (OnDecide, or settled before) and for an agent with no memory.
            Agent.Commit(result);

            Watch();

            var proposal = new Proposal(this, result, ++count);
            if (OnePending)
            {
                foreach (var old in pending)
                {
                    End(old, ProposalStatus.Superseded, $"Proposal #{proposal.Number} replaced it.");
                }

                pending.Clear();
            }

            pending.Add(proposal);
            RaiseChanged();
            return proposal;
        }

        /// <summary>
        /// Confirms <paramref name="proposal"/>: carries it out through <see cref="Agent.Execute(DecisionResult)"/>
        /// if it is still pending and <see cref="StillValid"/> allows it. Returns whether it was carried out.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="proposal"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="proposal"/> belongs to another <see cref="Proposals"/>.</exception>
        public bool Confirm(Proposal proposal)
        {
            return Confirm(proposal, out _);
        }

        /// <summary>
        /// Confirms <paramref name="proposal"/> as <see cref="Confirm(Proposal)"/> does, and says what
        /// that did.
        /// </summary>
        /// <param name="proposal">The proposal the player confirmed: the very object the card was made from.</param>
        /// <param name="outcome">
        /// What <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> reported, its re-check's
        /// refusal with the reason included. For a proposal no longer pending, or one
        /// <see cref="StillValid"/> refuses, <see cref="ExecutionStatus.Refused"/> with the reason, and
        /// no handler runs.
        /// </param>
        /// <returns><paramref name="outcome"/>'s <see cref="ActionResult.Succeeded"/>.</returns>
        /// <remarks>
        /// <para>
        /// The proposal leaves <see cref="Pending"/> before its handler runs, so the handler, which is
        /// the game's code, sees it gone and cannot confirm it twice. Afterwards its
        /// <see cref="Proposal.Status"/> is <see cref="ProposalStatus.Accepted"/>,
        /// <see cref="ProposalStatus.Failed"/> or <see cref="ProposalStatus.Refused"/>, from the
        /// outcome. A handler that throws leaves it <see cref="ProposalStatus.Failed"/> with no
        /// outcome, and the exception propagates.
        /// </para>
        /// <para>
        /// A refusal here, rather than by the agent, is written to the result's telemetry as the
        /// agent's own is, unless the decision was already carried out; it is not logged or raised on
        /// the decision feed, since the game itself moved past it, as <see cref="Conversation"/>'s
        /// refusals are not.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="proposal"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="proposal"/> belongs to another <see cref="Proposals"/>.</exception>
        public bool Confirm(Proposal proposal, out ActionResult outcome)
        {
            CheckOwn(proposal);

            if (!proposal.IsPending || ReferenceEquals(proposal, confirming))
            {
                outcome = ActionResult.Refused(ReferenceEquals(proposal, confirming)
                    ? "It is being confirmed already."
                    : $"It is no longer pending: {proposal.Status}. {proposal.Reason}".TrimEnd());
                if (proposal.Outcome == null && !ReferenceEquals(proposal, confirming))
                {
                    WriteRefusal(proposal.Result, outcome);
                }

                return false;
            }

            if (StillValid != null && !StillValid(proposal))
            {
                outcome = ActionResult.Refused("The game's StillValid check says it no longer stands.");
                pending.Remove(proposal);
                End(proposal, ProposalStatus.Refused, outcome.Reason);
                WriteRefusal(proposal.Result, outcome);
                RaiseChanged();
                return false;
            }

            pending.Remove(proposal);
            var outer = confirming;
            confirming = proposal;
            try
            {
                Agent.Execute(proposal.Result, out outcome);
            }
            catch
            {
                End(proposal, ProposalStatus.Failed, "The handler threw.");
                RaiseChanged();
                throw;
            }
            finally
            {
                confirming = outer;
            }

            proposal.Outcome = outcome;
            switch (outcome.Status)
            {
                case ExecutionStatus.Done:
                case ExecutionStatus.NoAction:
                    proposal.Status = ProposalStatus.Accepted;
                    break;
                case ExecutionStatus.Failed:
                    End(proposal, ProposalStatus.Failed, outcome.Reason);
                    break;
                default:
                    End(proposal, ProposalStatus.Refused, outcome.Reason);
                    break;
            }

            RaiseChanged();
            return outcome.Succeeded;
        }

        /// <summary>
        /// Declines <paramref name="proposal"/>: it ends as <see cref="ProposalStatus.Declined"/> and
        /// never runs. Returns whether it was pending.
        /// </summary>
        /// <param name="proposal">The proposal the player declined.</param>
        /// <param name="reason">Why, for <see cref="Proposal.Reason"/>; null for "Declined.".</param>
        /// <exception cref="ArgumentNullException"><paramref name="proposal"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="proposal"/> belongs to another <see cref="Proposals"/>.</exception>
        public bool Decline(Proposal proposal, string reason = null)
        {
            CheckOwn(proposal);
            if (!pending.Remove(proposal))
            {
                return false;
            }

            End(proposal, ProposalStatus.Declined, reason ?? "Declined.");
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Declines every pending proposal, as when the conversation they were made in closes or a
        /// save loads. Returns how many there were. <see cref="Changed"/> is raised once, if any.
        /// </summary>
        /// <param name="reason">Why, for each one's <see cref="Proposal.Reason"/>; null for "Declined.".</param>
        public int DeclineAll(string reason = null)
        {
            return EndAll(ProposalStatus.Declined, reason ?? "Declined.");
        }

        private static bool Holdable(DecisionResult result)
        {
            return result.Decision != null &&
                   result.Telemetry.Outcome != DecisionOutcome.PipelineError &&
                   result.Decision.ActionId != ActionRegistry.None;
        }

        private void CheckOwn(Proposal proposal)
        {
            if (proposal == null)
            {
                throw new ArgumentNullException(nameof(proposal));
            }

            if (!ReferenceEquals(proposal.Owner, this))
            {
                throw new ArgumentException("This proposal was held by another Proposals.", nameof(proposal));
            }
        }

        private int EndAll(ProposalStatus status, string reason)
        {
            if (pending.Count == 0)
            {
                return 0;
            }

            var ended = pending.ToArray();
            pending.Clear();
            foreach (var proposal in ended)
            {
                End(proposal, status, reason);
            }

            RaiseChanged();
            return ended.Length;
        }

        private static void End(Proposal proposal, ProposalStatus status, string reason)
        {
            proposal.Status = status;
            proposal.Reason = reason;
        }

        private static void WriteRefusal(DecisionResult result, ActionResult outcome)
        {
            result.Telemetry.Execution = outcome.Status;
            result.Telemetry.ExecutionReason = outcome.Reason;
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this);
        }

        // Watches the agent's current reset token. A reset cancels it and the agent moves to a new
        // one, which the next Hold watches; nothing is held in between.
        private void Watch()
        {
            var token = Agent.ResetToken;
            if (watching && token == watched)
            {
                return;
            }

            watch.Dispose();
            watched = token;
            watch = token.Register(OnReset);
            watching = true;
        }

        // Inside Agent.Reset, which must not fail for the game's Changed handler.
        private void OnReset()
        {
            watching = false;
            try
            {
                EndAll(ProposalStatus.Reset, "The agent was reset.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
