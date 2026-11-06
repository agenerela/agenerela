using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// Optional plumbing for asking one agent one thing at a time (#190, Living Vale #8). It keeps
    /// at most one turn waiting, ends that turn at a deadline the caller sets even when the
    /// provider ignores cancellation, and refuses at <see cref="Execute(DecisionResult, out ActionResult)"/>
    /// any result whose turn is no longer current, so a reply the game has moved past never runs
    /// and never enters memory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing in the framework needs it, and it decides nothing about when or why the agent is
    /// asked (DR-008): the game calls <see cref="Start"/> at the moment it chooses, with the
    /// stimulus and <see cref="DecideOptions"/> it would give <see cref="Agent.DecideAsync"/>.
    /// It is named for its commonest use, a chat with a character, and suits any agent asked one
    /// thing at a time where only the latest answer counts.
    /// </para>
    /// <para>
    /// A turn's result is current from when it answers until the next turn starts or
    /// <see cref="Cancel"/> ends it, and only a current result runs. When a turn stops being
    /// current, its answer is discarded from the agent's memory (<see cref="Agent.Discard"/>), so
    /// nothing remembers it afterwards, <see cref="Agent.Commit"/> included. That needs
    /// <see cref="Agenerela.MemoryCommit.OnExecute"/>, and <see cref="Start"/> throws for an agent
    /// with memory under <see cref="Agenerela.MemoryCommit.OnDecide"/>, which would have remembered
    /// the answer before the conversation saw it.
    /// </para>
    /// <para>
    /// A result is told current by reference: the conversation knows the <see cref="DecisionResult"/>
    /// its current turn produced. It never reads or sets <see cref="DecideOptions.Correlation"/>,
    /// which stays the caller's own, for handlers to read as before. <see cref="Agent.Execute(DecisionResult)"/>
    /// called directly knows nothing of turns and would run a stale result's handler, though it
    /// would not remember it: execute through the conversation.
    /// </para>
    /// <para>
    /// Plain C# with no scene. Call it from the main thread, as the agent is; the deadline and the
    /// end of a turn come back to it through Unity's synchronisation context.
    /// </para>
    /// <para>
    /// <b>Extension point:</b> what to do with a stimulus that arrives while a turn waits is the
    /// caller's choice between <see cref="Start"/>, which refuses it, and <see cref="Replace"/>,
    /// which ends the waiting turn. A per-agent policy for events, such as queueing with a cap
    /// (GreyBoxVillage's G5 in #190), would sit beside these two.
    /// </para>
    /// </remarks>
    public sealed class Conversation
    {
        private readonly Func<string, DecideOptions, CancellationToken, Awaitable<DecisionResult>> decide;
        private TimeSpan timeout = System.Threading.Timeout.InfiniteTimeSpan;
        private int turns;

        // The result Execute is carrying out, while its handler runs; null otherwise.
        private DecisionResult executing;

        /// <summary>A conversation with <paramref name="agent"/>, which may also be asked outside it.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="agent"/> is null.</exception>
        public Conversation(Agent agent)
            : this(agent, agent == null ? null : new Func<string, DecideOptions, CancellationToken, Awaitable<DecisionResult>>(agent.DecideAsync))
        {
        }

        // The tests' seam: a decide of their own, for an answer DecideAsync cannot be made to give.
        internal Conversation(Agent agent, Func<string, DecideOptions, CancellationToken, Awaitable<DecisionResult>> decide)
        {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            this.decide = decide ?? throw new ArgumentNullException(nameof(decide));
        }

        /// <summary>The agent every turn asks.</summary>
        public Agent Agent { get; }

        /// <summary>
        /// How long a turn may wait for its answer, the provider's queue included, before it ends
        /// as <see cref="TurnStatus.TimedOut"/>, counted from when <see cref="Start"/> returns.
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>, the default, never times out:
        /// how long a player waits is the game's to decide. Read when a turn starts, so changing it
        /// changes only the turns after.
        /// </summary>
        /// <remarks>
        /// The turn ends at the deadline whether or not the provider honours the cancellation it is
        /// sent then. A provider that ignores it keeps its place in the queue until it answers, so
        /// the next turn waits behind it, but its answer is dropped.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Neither infinite nor positive, or longer than <c>int.MaxValue</c> milliseconds, about 24 days.
        /// </exception>
        public TimeSpan Timeout
        {
            get => timeout;
            set
            {
                if (value != System.Threading.Timeout.InfiniteTimeSpan &&
                    (value <= TimeSpan.Zero || value.TotalMilliseconds > int.MaxValue))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "Timeout is positive and at most int.MaxValue milliseconds, or Timeout.InfiniteTimeSpan for none.");
                }

                timeout = value;
            }
        }

        /// <summary>
        /// The turn waiting for its answer, or the answered turn whose result may still run. Null
        /// when neither: before the first turn, and after one ends unanswered or is cancelled.
        /// </summary>
        public ConversationTurn Current { get; private set; }

        /// <summary>Whether a turn is waiting for its answer, in which case <see cref="Start"/> returns null.</summary>
        public bool IsWaiting => Current != null && Current.Status == TurnStatus.Waiting;

        /// <summary>
        /// Starts a turn: asks the agent, through <see cref="Agent.DecideAsync"/> with
        /// <paramref name="stimulus"/> and <paramref name="options"/>, and returns the turn at once,
        /// for the caller to await its <see cref="ConversationTurn.Completion"/>. Null, with nothing
        /// asked and nothing changed, while another turn is waiting: one at a time, never queued.
        /// </summary>
        /// <param name="stimulus">What the agent is asked, as <see cref="Agent.DecideAsync"/> takes it.</param>
        /// <param name="options">The call's options, as <see cref="Agent.DecideAsync"/> takes them, its <see cref="DecideOptions.Correlation"/> included.</param>
        /// <param name="ct">
        /// The caller's own token. Cancelling it while the turn waits ends the turn as
        /// <see cref="TurnStatus.Cancelled"/>; once the turn is answered, it changes nothing.
        /// </param>
        /// <remarks>
        /// Starting a turn makes the answered turn before it stale: its result is discarded from
        /// memory, and <see cref="Execute(DecisionResult)"/> refuses it. Settle that result first,
        /// with <see cref="Execute(DecisionResult)"/> or <see cref="Agent.Commit"/>, if it should be
        /// remembered. The new turn is current before the agent runs any of the game's code, so a
        /// turn started from inside that code, from an observation source say, comes after this one.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The agent has a <see cref="Agent.Memory"/> and its <see cref="Agent.MemoryCommit"/> is not
        /// <see cref="Agenerela.MemoryCommit.OnExecute"/>. Nothing is asked.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// What <see cref="Agent.DecideAsync"/> throws at the call, such as for an empty
        /// <see cref="DecideOptions.StimulusLabel"/>. Nothing changes: the turn before stays as it was.
        /// </exception>
        public ConversationTurn Start(string stimulus, DecideOptions options = null, CancellationToken ct = default)
        {
            return Begin(stimulus, options, ct, replace: false);
        }

        /// <summary>
        /// Starts a turn as <see cref="Start"/> does, ending the waiting turn first, if there is one,
        /// as <see cref="TurnStatus.Replaced"/>: its request is cancelled, and whatever it answers is
        /// dropped. For a caller that wants the latest stimulus to win. Never null.
        /// </summary>
        /// <param name="stimulus">What the agent is asked, as <see cref="Agent.DecideAsync"/> takes it.</param>
        /// <param name="options">The call's options, as <see cref="Agent.DecideAsync"/> takes them.</param>
        /// <param name="ct">The caller's own token, as for <see cref="Start"/>.</param>
        /// <exception cref="InvalidOperationException">As for <see cref="Start"/>. Nothing is ended.</exception>
        /// <exception cref="ArgumentException">As for <see cref="Start"/>. Nothing is ended.</exception>
        public ConversationTurn Replace(string stimulus, DecideOptions options = null, CancellationToken ct = default)
        {
            return Begin(stimulus, options, ct, replace: true);
        }

        /// <summary>
        /// Ends the current turn: one still waiting ends as <see cref="TurnStatus.Cancelled"/>, and
        /// an answered one's result goes stale and is discarded from memory. For a game that moves
        /// on: the player walked away, another speaker took over. Returns whether there was a
        /// current turn.
        /// </summary>
        public bool Cancel()
        {
            var turn = Current;
            if (turn == null)
            {
                return false;
            }

            if (turn.Status == TurnStatus.Waiting)
            {
                End(turn, TurnStatus.Cancelled);
            }
            else
            {
                Retire(turn);
            }

            return true;
        }

        /// <summary>
        /// Whether <paramref name="result"/> is the answer of the current turn, the one result
        /// <see cref="Execute(DecisionResult)"/> runs. False for null, for a stale turn's answer, and
        /// for a result this conversation did not ask for.
        /// </summary>
        public bool IsCurrent(DecisionResult result)
        {
            var turn = Current;
            return result != null && turn != null && turn.Status == TurnStatus.Answered && ReferenceEquals(turn.Result, result);
        }

        /// <summary>
        /// Carries out the current turn's answer through <see cref="Agent.Execute(DecisionResult)"/>,
        /// and refuses any other result: one whose turn is no longer current, or one this
        /// conversation did not ask for. Returns whether the decision was carried out.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        public bool Execute(DecisionResult result)
        {
            return Execute(result, out _);
        }

        /// <summary>
        /// Carries out the current turn's answer as <see cref="Agent.Execute(DecisionResult, out ActionResult)"/>
        /// does, which says what that did, and refuses any other result.
        /// </summary>
        /// <param name="result">A result, normally the <see cref="ConversationTurn.Result"/> of the current turn.</param>
        /// <param name="outcome">
        /// What <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> reports for the current
        /// turn's answer. For any other result, <see cref="ExecutionStatus.Refused"/> with the reason,
        /// and no handler runs.
        /// </param>
        /// <returns><paramref name="outcome"/>'s <see cref="ActionResult.Succeeded"/>.</returns>
        /// <remarks>
        /// A refused result is not logged, since the game itself moved past it, but the refusal is
        /// written to its telemetry, in <see cref="DecisionTelemetry.Execution"/> and
        /// <see cref="DecisionTelemetry.ExecutionReason"/>, as an agent's own refusal is. The current
        /// turn's answer may be executed more than once, as an agent allows.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        public bool Execute(DecisionResult result, out ActionResult outcome)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!IsCurrent(result))
            {
                outcome = ActionResult.Refused(
                    "It is not the answer of the conversation's current turn: a newer turn started, Cancel ended it, " +
                    "it ended without an answer, or the conversation never asked for it.");
                result.Telemetry.Execution = outcome.Status;
                result.Telemetry.ExecutionReason = outcome.Reason;
                return false;
            }

            // The handler is the game's code, and may start the next turn from inside it. That turn
            // must not discard the answer being carried out, which Agent remembers only once the
            // handler returns; one that was not carried out goes the way of any stale answer.
            var outer = executing;
            executing = result;
            try
            {
                return Agent.Execute(result, out outcome);
            }
            finally
            {
                executing = outer;
                if (!IsCurrent(result) && !ReferenceEquals(result, executing))
                {
                    Agent.Discard(result);
                }
            }
        }

        private ConversationTurn Begin(string stimulus, DecideOptions options, CancellationToken ct, bool replace)
        {
            if (Agent.Memory != null && Agent.MemoryCommit != MemoryCommit.OnExecute)
            {
                string name = Agent.Identity?.Name;
                throw new InvalidOperationException(
                    $"{(string.IsNullOrWhiteSpace(name) ? "This agent" : $"Agent '{name}'")} remembers each decision as DecideAsync returns it " +
                    "(MemoryCommit.OnDecide), so a reply the conversation drops as stale would be remembered anyway. " +
                    "Set agent.MemoryCommit = MemoryCommit.OnExecute, or agent.Memory = null for an agent with no memory.");
            }

            var previous = Current;
            if (previous != null && previous.Status == TurnStatus.Waiting && !replace)
            {
                return null;
            }

            // The two mistakes DecideAsync throws for at the call, checked before anything changes,
            // so that a mistake costs the previous turn nothing.
            CheckOptions(options);

            var limit = timeout;
            var turn = new ConversationTurn(this, ++turns, ct);

            // Current before the agent is asked, so a turn started from inside the game's code the
            // agent runs while preparing the request comes after this one, and ends it.
            Current = turn;

            // The previous turn ends before the agent is asked too (Codex, round 1): preparing the
            // request runs the game's code, which must find the previous answer already discarded
            // from memory and the previous request already cancelled.
            if (previous != null)
            {
                if (previous.Status == TurnStatus.Waiting)
                {
                    End(previous, TurnStatus.Replaced);
                }
                else
                {
                    Retire(previous);
                }
            }

            Awaitable<DecisionResult> decision;
            try
            {
                decision = decide(stimulus, options, turn.Token);
            }
            catch
            {
                // A mistake DecideAsync found that CheckOptions did not, thrown before it runs any of
                // the game's code. The previous turn has ended all the same.
                if (Current == turn)
                {
                    Current = null;
                }

                if (turns == turn.Number)
                {
                    turns--;
                }

                turn.Abandon();
                throw;
            }

            // Every name below is this turn's local, never Current: the game's code the agent ran
            // above may already have started a newer turn.
            _ = Run(turn, decision);

            if (turn.Status == TurnStatus.Waiting && limit != System.Threading.Timeout.InfiniteTimeSpan)
            {
                _ = Deadline(turn, limit);
            }

            return turn;
        }

        // DecideAsync's own checks at the call, repeated so they throw before the previous turn ends.
        private static void CheckOptions(DecideOptions options)
        {
            if (options == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(options.StimulusLabel))
            {
                throw new ArgumentException(
                    "DecideOptions.StimulusLabel is empty. It labels the stimulus; the default is \"Player\".",
                    nameof(options));
            }

            if (options.MaxListedTargets < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxListedTargets,
                    "DecideOptions.MaxListedTargets cannot be negative. 0 lists no targets.");
            }
        }

        // The whole life of one turn's request, including watching a provider that answers after
        // the turn ended. Never throws: nothing awaits it.
        private async Task Run(ConversationTurn turn, Awaitable<DecisionResult> awaitable)
        {
            var decision = AsTask(awaitable);
            try
            {
                if (!decision.IsCompleted)
                {
                    // Raced against the turn's token, which ending the turn cancels and the caller's
                    // cancels too: a provider that ignores cancellation must not keep the turn waiting.
                    var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    using (turn.Token.Register(() => stopped.TrySetResult(true)))
                    {
                        await Task.WhenAny(decision, stopped.Task);
                    }
                }

                if (turn.Status == TurnStatus.Waiting)
                {
                    if (decision.Status == TaskStatus.RanToCompletion && !turn.Token.IsCancellationRequested)
                    {
                        turn.Answer(decision.Result);
                    }
                    else if (decision.IsFaulted && !(decision.Exception.GetBaseException() is OperationCanceledException))
                    {
                        End(turn, TurnStatus.Failed, decision.Exception.GetBaseException());
                    }
                    else
                    {
                        // Nothing here ended it, so the caller's token did: the request was cancelled,
                        // is still out, or answered just as the caller gave up on it.
                        End(turn, TurnStatus.Cancelled);
                    }
                }

                if (turn.Status != TurnStatus.Answered)
                {
                    // Whatever the agent does now is late. Waited for only to drop it.
                    DecisionResult late = null;
                    try
                    {
                        late = await decision;
                    }
                    catch (Exception)
                    {
                        // Cancelled, as a stopped request should be, or failed after the turn ended.
                    }

                    if (late != null)
                    {
                        Agent.Discard(late);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                if (decision.IsCompleted)
                {
                    turn.ReleaseToken();
                }
            }
        }

        // Ends the turn when its time is up, unless it ended first.
        private async Task Deadline(ConversationTurn turn, TimeSpan after)
        {
            try
            {
                await Task.Delay(after, turn.StartDeadline());
            }
            catch (OperationCanceledException)
            {
                return;
            }

            End(turn, TurnStatus.TimedOut);
        }

        // A waiting turn ends without an answer, once.
        private void End(ConversationTurn turn, TurnStatus status, Exception failure = null)
        {
            if (turn.Status != TurnStatus.Waiting)
            {
                return;
            }

            if (Current == turn)
            {
                Current = null;
            }

            turn.End(status, failure);
        }

        // An answered turn stops being current: its result goes stale, and out of memory's reach,
        // unless Execute is carrying it out right now, in which case Execute settles it after.
        private void Retire(ConversationTurn turn)
        {
            if (Current == turn)
            {
                Current = null;
            }

            if (turn.Result != null && !ReferenceEquals(turn.Result, executing))
            {
                Agent.Discard(turn.Result);
            }
        }

        private static async Task<DecisionResult> AsTask(Awaitable<DecisionResult> awaitable)
        {
            return await awaitable;
        }
    }
}
