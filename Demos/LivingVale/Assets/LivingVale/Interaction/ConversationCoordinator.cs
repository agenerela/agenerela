using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LivingVale.Interaction
{
    /// <summary>Why a conversation ended. The Unity side picks one; the coordinator only records it.</summary>
    public enum ConversationCloseReason
    {
        PlayerClosed,
        SwitchedSpeaker,
        LeftRange,
        Interrupted
    }

    /// <summary>Why a turn ended without the NPC's own line.</summary>
    public enum TurnFailure
    {
        None,
        Timeout,
        Malformed,
        ProviderError,

        /// <summary>The decision arrived, but the driver's re-check refused to execute it.</summary>
        Refused
    }

    public enum ConversationEventKind
    {
        Opened,
        Closed,
        PlayerSaid,

        /// <summary>A turn was sent; show "waiting for a reply".</summary>
        Waiting,
        NpcReplied,
        TurnFailed,

        /// <summary>The player cancelled the pending turn.</summary>
        TurnCancelled,

        /// <summary>The backend finished (answered, failed or cancelled) a turn that had already ended; nothing was applied. Diagnostic only.</summary>
        StaleReplyDropped,

        /// <summary>Game code added a system line, such as a trade receipt (<see cref="ConversationCoordinator.AddSystemLine"/>).</summary>
        SystemLine
    }

    /// <summary>One change to the conversation. Re-render from the coordinator's state; the event only says what changed.</summary>
    public readonly struct ConversationEvent
    {
        public ConversationEventKind Kind { get; }
        public string NpcId { get; }
        public long RequestId { get; }
        public TurnFailure Failure { get; }
        public ConversationCloseReason CloseReason { get; }

        public ConversationEvent(ConversationEventKind kind, string npcId, long requestId = 0,
            TurnFailure failure = TurnFailure.None, ConversationCloseReason closeReason = ConversationCloseReason.PlayerClosed)
        {
            Kind = kind;
            NpcId = npcId;
            RequestId = requestId;
            Failure = failure;
            CloseReason = closeReason;
        }

        public override string ToString() => $"{Kind} {NpcId} #{RequestId} {Failure}";
    }

    /// <summary>Tuning for <see cref="ConversationCoordinator"/>. Values are G1 proposals, to be measured later.</summary>
    public sealed class ConversationSettings
    {
        /// <summary>Total time a turn may take, queue included (design §8 proposes eight seconds).</summary>
        public TimeSpan TurnTimeout = TimeSpan.FromSeconds(8);

        /// <summary>Longest player line accepted. The chat input should enforce the same limit.</summary>
        public int MaxLineLength = 280;

        /// <summary>How many earlier lines a driver receives.</summary>
        public int MaxHistoryForDriver = 20;
    }

    /// <summary>
    /// Owns who the player is talking to and the one turn in flight. A reply is applied only if it
    /// answers the pending request of the conversation that is still open: closing, switching
    /// speaker or cancelling ends the turn, and anything that arrives for it afterwards is dropped,
    /// because cancelling a backend does not guarantee it stops (design §12).
    /// </summary>
    /// <remarks>
    /// Plain C#, no scene: the Unity side opens and closes conversations (focus, range, damage)
    /// and renders <see cref="Changed"/>. Call it from the main thread; awaits resume on the
    /// caller's synchronisation context, which in Unity is the main thread.
    /// </remarks>
    public sealed class ConversationCoordinator
    {
        private readonly INpcDecisionDriver driver;
        private readonly ConversationSettings settings;
        private readonly Dictionary<string, List<ChatLine>> histories = new Dictionary<string, List<ChatLine>>();

        private long nextRequestId;
        private NpcTurn pendingTurn;
        private CancellationTokenSource pendingCts;

        public ConversationCoordinator(INpcDecisionDriver driver, ConversationSettings settings = null)
        {
            this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
            this.settings = settings ?? new ConversationSettings();
        }

        public event Action<ConversationEvent> Changed;

        /// <summary>The driver's label, for the overlay ("Scripted replies").</summary>
        public string DriverLabel => driver.Label;

        public ConversationSettings Settings => settings;

        /// <summary>The NPC the player is talking to, or null.</summary>
        public NpcDescriptor ActiveNpc { get; private set; }

        public bool IsOpen => ActiveNpc != null;

        public bool IsAwaitingReply => pendingTurn != null;

        /// <summary>Goes up on every open and close. A turn belongs to the generation it started in.</summary>
        public long Generation { get; private set; }

        /// <summary>
        /// The most recently started turn's whole life, for tests and tools. It ends when the
        /// backend does, which can be after the turn itself ended (timeout, cancel, close), so
        /// waiting on it can take as long as a backend that ignores cancellation.
        /// </summary>
        public Task LastTurn { get; private set; } = Task.CompletedTask;

        /// <summary>This NPC's conversation so far, oldest first. Kept across closes for the session.</summary>
        public IReadOnlyList<ChatLine> HistoryFor(string npcId)
        {
            return npcId != null && histories.TryGetValue(npcId, out var lines) ? lines : (IReadOnlyList<ChatLine>)Array.Empty<ChatLine>();
        }

        /// <summary>
        /// Forgets every NPC's shown history, for a loaded save: the lines on screen must not
        /// contradict what the NPCs remember, which the save restores to an earlier point.
        /// </summary>
        /// <exception cref="InvalidOperationException">A conversation is open; close it first.</exception>
        public void ClearHistories()
        {
            if (ActiveNpc != null)
            {
                throw new InvalidOperationException("Close the conversation before clearing histories.");
            }

            histories.Clear();
        }

        /// <summary>
        /// Starts talking to <paramref name="npc"/>. Opening the NPC already open does nothing;
        /// opening another closes the current one first, ending its turn.
        /// </summary>
        public void Open(NpcDescriptor npc)
        {
            if (npc == null)
            {
                throw new ArgumentNullException(nameof(npc));
            }

            if (ActiveNpc != null)
            {
                if (ActiveNpc.Id == npc.Id)
                {
                    return;
                }

                Close(ConversationCloseReason.SwitchedSpeaker);
            }

            ActiveNpc = npc;
            Generation++;
            Raise(new ConversationEvent(ConversationEventKind.Opened, npc.Id));
        }

        /// <summary>Ends the conversation, and its pending turn with it. Does nothing when none is open.</summary>
        public void Close(ConversationCloseReason reason)
        {
            if (ActiveNpc == null)
            {
                return;
            }

            string npcId = ActiveNpc.Id;
            AbandonPending();
            ActiveNpc = null;
            Generation++;
            Raise(new ConversationEvent(ConversationEventKind.Closed, npcId, closeReason: reason));
        }

        /// <summary>
        /// Sends the player's line. False, with nothing sent, when no conversation is open, a turn is
        /// already pending (one at a time, never queued), or the line is blank or too long.
        /// </summary>
        public bool Submit(string text)
        {
            if (ActiveNpc == null || pendingTurn != null || text == null)
            {
                return false;
            }

            text = text.Trim();
            if (text.Length == 0 || text.Length > settings.MaxLineLength)
            {
                return false;
            }

            var npc = ActiveNpc;
            var history = Lines(npc.Id);
            int from = Math.Max(0, history.Count - settings.MaxHistoryForDriver);
            var turn = new NpcTurn(++nextRequestId, npc, text, history.GetRange(from, history.Count - from).AsReadOnly(), Generation);

            history.Add(new ChatLine(ChatSpeaker.Player, text));

            // Locals, not the fields: a subscriber to the events below may cancel, close or even
            // submit again, which replaces the fields with another turn's.
            var cts = new CancellationTokenSource();
            long generation = Generation;
            pendingTurn = turn;
            pendingCts = cts;

            Raise(new ConversationEvent(ConversationEventKind.PlayerSaid, npc.Id, turn.RequestId));
            Raise(new ConversationEvent(ConversationEventKind.Waiting, npc.Id, turn.RequestId));

            if (!IsCurrent(turn, generation))
            {
                // Abandoned by a subscriber before it reached the backend.
                cts.Dispose();
                return true;
            }

            // The deadline starts when the backend is asked, after the subscribers ran.
            cts.CancelAfter(settings.TurnTimeout);
            LastTurn = RunTurn(turn, generation, cts);
            return true;
        }

        /// <summary>
        /// Adds a game-authored line to the open conversation, such as a trade receipt, so the
        /// player sees it and the next turn's history says what happened. False when none is open.
        /// </summary>
        public bool AddSystemLine(string text)
        {
            if (ActiveNpc == null || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            Lines(ActiveNpc.Id).Add(new ChatLine(ChatSpeaker.System, text.Trim()));
            Raise(new ConversationEvent(ConversationEventKind.SystemLine, ActiveNpc.Id));
            return true;
        }

        /// <summary>Stops waiting for the pending reply. The conversation stays open for another try.</summary>
        public void CancelPending()
        {
            if (pendingTurn == null)
            {
                return;
            }

            var turn = pendingTurn;
            AbandonPending();
            Lines(turn.Npc.Id).Add(new ChatLine(ChatSpeaker.System, "You stop waiting for an answer."));
            Raise(new ConversationEvent(ConversationEventKind.TurnCancelled, turn.Npc.Id, turn.RequestId));
        }

        // The whole life of one turn, including watching a backend that answers after the
        // coordinator stopped waiting for it; LastTurn is this task.
        private async Task RunTurn(NpcTurn turn, long generation, CancellationTokenSource cts)
        {
            Task<NpcReply> decide;
            try
            {
                decide = driver.DecideAsync(turn, cts.Token) ?? Task.FromResult<NpcReply>(null);
            }
            catch (Exception e)
            {
                decide = Task.FromException<NpcReply>(e);
            }

            try
            {
                // Race the backend against the token, which the timeout and every abandonment
                // cancel: a backend that ignores cancellation must not keep the turn open.
                var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cts.Token.Register(() => stopped.TrySetResult(true)))
                {
                    await Task.WhenAny(decide, stopped.Task);
                }

                if (!decide.IsCompleted)
                {
                    if (IsCurrent(turn, generation))
                    {
                        // Nothing abandoned it, so the timeout fired.
                        ClearPending();
                        Fail(turn, TurnFailure.Timeout);
                    }

                    // Whatever the backend does now is late, and only observed.
                    try
                    {
                        await decide;
                    }
                    catch (Exception)
                    {
                        // Cancelled or failed after the turn ended: nothing to report to the player.
                    }

                    Raise(new ConversationEvent(ConversationEventKind.StaleReplyDropped, turn.Npc.Id, turn.RequestId));
                    return;
                }

                NpcReply reply = null;
                bool threw = false;
                try
                {
                    reply = await decide;
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    // Timeout or abandonment; told apart below.
                }
                catch (Exception e)
                {
                    threw = true;
                    Debug.LogWarning($"[LivingVale] {driver.Label} failed on turn {turn.RequestId} for {turn.Npc.Id}: {e}");
                }

                if (!IsCurrent(turn, generation))
                {
                    // Cancelled, closed or switched while it was out. Never applied.
                    Raise(new ConversationEvent(ConversationEventKind.StaleReplyDropped, turn.Npc.Id, turn.RequestId));
                    return;
                }

                // Still current, so nothing abandoned it: a cancelled token means the timeout fired,
                // and a reply that ignored the token and landed with it is late all the same.
                bool timedOut = cts.IsCancellationRequested;
                ClearPending();

                TurnFailure failure;
                if (timedOut)
                {
                    failure = TurnFailure.Timeout;
                }
                else if (threw)
                {
                    failure = TurnFailure.ProviderError;
                }
                else if (reply == null || (reply.Failure == NpcReplyFailure.None && reply.Decision == null))
                {
                    failure = TurnFailure.Malformed;
                }
                else if (reply.Failure != NpcReplyFailure.None)
                {
                    failure = reply.Failure == NpcReplyFailure.Malformed ? TurnFailure.Malformed : TurnFailure.ProviderError;
                }
                else
                {
                    failure = Apply(turn, reply);
                }

                if (failure == TurnFailure.None)
                {
                    Raise(new ConversationEvent(ConversationEventKind.NpcReplied, turn.Npc.Id, turn.RequestId));
                    return;
                }

                Fail(turn, failure);
            }
            finally
            {
                // Only once the backend is done with its token.
                if (decide.IsCompleted)
                {
                    cts.Dispose();
                }
            }
        }

        private void Fail(NpcTurn turn, TurnFailure failure)
        {
            Lines(turn.Npc.Id).Add(FailureLine(turn.Npc, failure));
            Raise(new ConversationEvent(ConversationEventKind.TurnFailed, turn.Npc.Id, turn.RequestId, failure));
        }

        private TurnFailure Apply(NpcTurn turn, NpcReply reply)
        {
            bool executed;
            try
            {
                executed = driver.Execute(turn, reply);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LivingVale] {driver.Label} could not execute turn {turn.RequestId} for {turn.Npc.Id}: {e}");
                executed = false;
            }

            if (!executed)
            {
                return TurnFailure.Refused;
            }

            string statement = reply.Decision.Statement;
            Lines(turn.Npc.Id).Add(new ChatLine(ChatSpeaker.Npc, string.IsNullOrWhiteSpace(statement) ? "…" : statement.Trim()));
            return TurnFailure.None;
        }

        // Game-authored lines, never the model's statement: on a refusal the statement may
        // describe the very action that was refused.
        private static ChatLine FailureLine(NpcDescriptor npc, TurnFailure failure)
        {
            switch (failure)
            {
                case TurnFailure.Timeout:
                    return new ChatLine(ChatSpeaker.System, $"{npc.DisplayName} did not answer in time. Try again.");
                case TurnFailure.Refused:
                    return new ChatLine(ChatSpeaker.Npc, "I can't do that right now.");
                default:
                    return new ChatLine(ChatSpeaker.System, $"{npc.DisplayName} seems distracted. Try again.");
            }
        }

        private bool IsCurrent(NpcTurn turn, long generation)
        {
            return pendingTurn == turn && Generation == generation && ActiveNpc != null && ActiveNpc.Id == turn.Npc.Id;
        }

        private void AbandonPending()
        {
            var cts = pendingCts;
            ClearPending();
            // After clearing, so a driver that completes synchronously on cancel sees a stale turn.
            // RunTurn disposes it once the backend has let go of the token.
            cts?.Cancel();
        }

        private void ClearPending()
        {
            pendingTurn = null;
            pendingCts = null;
        }

        private List<ChatLine> Lines(string npcId)
        {
            if (!histories.TryGetValue(npcId, out var lines))
            {
                lines = new List<ChatLine>();
                histories[npcId] = lines;
            }

            return lines;
        }

        private void Raise(ConversationEvent e)
        {
            var handler = Changed;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(e);
            }
            catch (Exception ex)
            {
                // A broken view must not wedge the conversation state.
                Debug.LogException(ex);
            }
        }
    }
}
