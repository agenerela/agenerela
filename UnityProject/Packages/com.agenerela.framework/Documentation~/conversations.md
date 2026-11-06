# Conversations — one turn at a time, and only the latest answer runs

**This is optional.** Nothing in the framework needs it, and an agent works exactly as before
without it. `Conversation` is plumbing for a game that asks one agent one thing at a time, such
as a chat with an NPC, and wants three things it is easy to get subtly wrong (#190, Living
Vale #8):

- **one turn waiting at a time**, so a second line does not race the first;
- **a timeout the game sets**, which ends the turn on time even when the provider ignores
  cancellation;
- **a stale answer refused**: a reply that arrives after the game moved on (a timeout, a cancel,
  a newer line, another speaker) never runs its handler and never enters the agent's memory.

**The game still decides the moment** (DR-008). A conversation never starts a turn by itself,
never decides when or why the agent is asked, and knows nothing of speakers, chat windows or
range: the game calls `Start` when it chooses, with the same stimulus and `DecideOptions` it
would give `DecideAsync`. It is sugar over `DecideAsync`, `Execute` and `MemoryCommit.OnExecute`,
as DR-015's ticker is over `Priority`: delete it, and nothing in `Agent` changes.

Every rule here runs as a test, in `Tests/Editor/Core/ConversationTests.cs`.

## Setting one up

```csharp
mara.MemoryCommit = MemoryCommit.OnExecute;    // required while the agent has memory
var chat = new Conversation(mara) { Timeout = TimeSpan.FromSeconds(8) };
```

A stale answer can only be kept out of memory if memory waits for the game
([executing-and-memory.md](executing-and-memory.md)). Under the default, `OnDecide`, the agent
remembers an answer before the conversation sees it, so `Start` throws for an agent that has
memory and is not set to `OnExecute`. An agent with `Memory = null` needs neither.

`Timeout` is `Timeout.InfiniteTimeSpan` by default, so a turn waits until it is answered or
cancelled: how long a player waits is the game's choice, not the framework's.

## A turn

```csharp
async void OnPlayerLine(string line)
{
    var turn = chat.Start(line, new DecideOptions { Observations = WhatMaraSees() });
    if (turn == null)
        return;                                   // still waiting on the last line: nothing sent

    ShowWaiting();
    var status = await turn.Completion;

    if (status == TurnStatus.TimedOut)
        Say("Mara did not answer in time.");
    else if (status == TurnStatus.Answered && turn.IsCurrent)
    {
        if (chat.Execute(turn.Result, out var outcome))
            Say(turn.Result.Decision.Statement);
        else
            Say(outcome.Reason);                  // the handler's own reason, or a refusal
    }
    // Cancelled or Replaced: the game did that itself, and already knows.
}

void OnPlayerWalkedAway() => chat.Cancel();
```

`Start` returns the turn at once. Its `Completion` completes when the wait ends, at the deadline
at the latest, and never ends cancelled: a cancelled turn completes with `TurnStatus.Cancelled`.
`Start` returns `null`, sending nothing, while another turn is waiting: one at a time, never
queued.

| `turn.Status` | Means |
|---|---|
| `Waiting` | Asked, no answer yet |
| `Answered` | The agent answered while the turn was waiting. `turn.Result` holds it, which may be a pipeline error |
| `TimedOut` | `Timeout` passed first |
| `Cancelled` | The caller's own token was cancelled, or `Cancel()` ended it |
| `Replaced` | `Replace` started another turn while it waited |
| `Failed` | `DecideAsync` threw something other than a cancellation, which only a bug does. `Completion` throws it |

The status changes once. Whether the turn's result may still run is `turn.IsCurrent`, a separate
question: an answered turn stays `Answered` after a newer turn starts, and its result goes stale.

## `Start` or `Replace`

What to do with a line that arrives while a turn waits is the caller's choice, per call:

- `chat.Start(...)` refuses it and returns `null`. The waiting turn carries on.
- `chat.Replace(...)` ends the waiting turn as `Replaced`, cancelling its request, and starts the
  new one. For a caller that wants the latest stimulus to win. It never returns `null`.

## Stale answers

A turn's result is **current** from when it is answered until the next `Start` or `Replace`, or
`Cancel()`. `chat.Execute(result)` runs only the current turn's result, through
`agent.Execute`, and refuses everything else: a stale turn's answer, or a result the
conversation never asked for. A refusal returns `false`, with `ExecutionStatus.Refused` and the
reason in the outcome and in the result's telemetry (`Execution`, `ExecutionReason`), and runs no
handler. It is not logged, since the game itself moved past it.

When a turn stops being current, its answer is **discarded** from memory (`agent.Discard`), so
nothing remembers it afterwards, `agent.Commit` included. If an answer should be remembered,
settle it before starting the next turn: `chat.Execute` remembers it when it runs, and
`agent.Commit` remembers one whose handler failed but whose line was shown anyway. An answer that
arrives after its turn ended is never handed out at all, and is discarded the same way.

A handler may start the next turn from inside `chat.Execute`, since it is the game's own code. The
answer being carried out is still remembered when its handler succeeds; one whose handler fails
goes stale with its turn, as any other.

`agent.Execute` called directly knows nothing of turns: on a stale result it would still run the
handler, though not remember it. Execute through the conversation.

**The conversation tells a stale result by reference.** It knows which `DecisionResult` its
current turn produced, so it never reads or sets `DecideOptions.Correlation`. That stays the
caller's own, and a handler reads it from `ctx.Correlation` as before
([executing-and-memory.md](executing-and-memory.md)).

## The timeout

The deadline counts from when `Start` returns, and covers the provider's queue as well as the
model. At the deadline the turn ends as `TimedOut` and its request is cancelled, whether or not
the provider honours that: the conversation races the request against its own deadline, rather
than waiting for the provider to give up. A provider that ignores cancellation keeps its place in
the queue until it answers, so the next turn waits behind it, but that late answer is dropped.

`Timeout` is read when a turn starts, so changing it changes only the turns after. It must be
positive and at most `int.MaxValue` milliseconds, about 24 days, or `Timeout.InfiniteTimeSpan`.

The caller's own `CancellationToken`, passed to `Start` or `Replace`, ends a waiting turn as
`Cancelled` in the same way, raced rather than waited for. Once the turn is answered, cancelling
it changes nothing; `Cancel()` is what makes an answer stale.

## Order and threads

A new turn is current before the agent runs any of the game's code for it, so a turn started from
inside that code, from an observation source say, comes after it and ends it. Each turn's
cancellation and deadline are its own: one turn ending never ends another.

Call a conversation from the main thread, as an agent is. The deadline and the end of a turn come
back to the main thread through Unity's synchronisation context, and code awaiting
`turn.Completion` never runs inside the conversation's own bookkeeping.

A mistake in the call, such as an empty `StimulusLabel`, throws from `Start` or `Replace` as it
does from `DecideAsync`, and changes nothing: the turn before stays as it was.

## What it does not do

- **Decide when to ask.** It has no opening, closing, speakers or range. The game ends a turn
  with `Cancel()` when its own rules say so: the player walked away, another NPC took over.
- **Keep the lines said.** The chat log is the game's, and the agent's memory is the agent's.
- **Queue.** A line that arrives while a turn waits is refused or replaces it. A per-agent policy
  for events, such as queueing with a cap, would sit beside `Start` and `Replace`
  (GreyBoxVillage's G5 in #190).
