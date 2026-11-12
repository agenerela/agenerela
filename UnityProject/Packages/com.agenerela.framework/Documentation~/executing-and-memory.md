# Acting on a decision — what it did, which call it was, and what the agent remembers

`DecideAsync` asks the model and returns a decision. `Execute` checks it again and runs the
game's code. This page covers what happens around that second call (DR-018): how a handler says
what it did, how a decision carries the caller's own context to the handler, how memory can wait
until the game has acted, how memory is saved with the game, and how to start an agent afresh.

All of it is opt-in. A handler, a call and a memory written before it work as they did, and
`agent.Execute(result)` still returns `true` or `false`.

Every rule here runs as a test, in `Tests/Editor/Core/ExecutionResultTests.cs`,
`Tests/Editor/Memory/MemoryCommitTests.cs`, `Tests/Editor/Memory/MemorySnapshotTests.cs`,
`Tests/Editor/Memory/RememberedDecisionTests.cs` and
`Tests/Editor/Core/AgentResetTests.cs`.

## What a handler did: `ActionResult`

`Execute` returning `true` means the handler ran. It cannot say that the game then refused: the
quote had gone stale, the purse was short. A handler that knows that returns an `ActionResult`:

```csharp
agent.Bind("quote_sale", DelegateActionHandler.WithResult(
    ctx => shop.IsOpen,
    (ctx, decision) =>
    {
        if (!shop.TryQuote(decision.TargetId, out var quote))
            return ActionResult.Failed("That stock is gone.");
        return ActionResult.Done(quote);
    }));

if (!agent.Execute(result, out var outcome))
    ui.Say(outcome.Reason);                       // "That stock is gone."
else if (outcome.TryGetPayload(out Quote quote))
    ui.ShowQuote(quote);
```

There are three ways to return one:

| Front door | How |
|---|---|
| A delegate | `DelegateActionHandler.WithResult(isAvailable, execute)`, whose `execute` returns an `ActionResult`. The constructor still takes a plain `Action` |
| An attributed method | An `[AgentAction]` method returns `ActionResult` instead of `void` |
| A class | Implement `IActionHandlerWithResult`, and keep `IActionHandler.Execute` as a one-line forward |

```csharp
public sealed class Shopkeeper : IActionHandlerWithResult
{
    public bool IsAvailable(AgentContext ctx) => shop.IsOpen;
    public void Execute(AgentContext ctx, AgentDecision decision) => ExecuteWithResult(ctx, decision);
    public ActionResult ExecuteWithResult(AgentContext ctx, AgentDecision decision) => …;
}
```

A scene component deriving from `ActionHandlerBehaviour` adds `IActionHandlerWithResult` the same
way, its `Execute` override forwarding to `ExecuteWithResult`.

`Execute` calls `ExecuteWithResult` when a handler has it, once, and never both methods. A
handler that reports nothing counts as `Done` whenever it returns, so nothing written before this
changes.

### The four statuses

| `outcome.Status` | Means | `Execute` returns |
|---|---|---|
| `Done` | The handler ran and did it; `Payload` is what it made, or null | `true` |
| `Failed` | The handler ran and could not; `Reason` says why, in its own words | `false` |
| `NoAction` | The decision was `none`, a guard's rewrite included: nothing to run | `true` |
| `Refused` | No handler ran: a pipeline error, a guard's `Reject`, or the re-check at `Execute` refused it; `Reason` says which | `false` |

When the per-target check is what refused, `outcome.AvailabilityReason` is that check's own
`Availability.No(...)` reason, word for word ("claimed by Brann"), for a line the game writes
itself; `Reason` is the agent's sentence for a log. It is null for every other refusal and status.

A handler makes only `Done` and `Failed`. `Failed` needs a reason. `default(ActionResult)` is
`Done` with no payload. A `Failed` is the game's own answer, so it is not logged; a `Refused` is
logged as it always was. A handler that throws still throws out of `Execute`, and records
nothing.

The status and the reason go into the result's telemetry, in `Telemetry.Execution` and
`Telemetry.ExecutionReason`, so the evaluation can tell "chose the right action" from "the
action worked". The payload does not: it is the game's object, and telemetry is plain data. Each
`Execute` of a result replaces the last one's.

## Which call a decision belongs to: `Correlation`

A handler sometimes needs to know which call it is acting for: the conversation a quote belongs
to, the turn number, a request id. Writing it into `Agent.State` before `Execute` works only
while one call is in flight, because every call on the agent shares that state. Put it on the
call instead:

```csharp
var result = await agent.DecideAsync(line, new DecideOptions { Correlation = conversation });
// …later, perhaps after another call has started:
agent.Execute(result);

// In the handler:
var conversation = (Conversation)ctx.Correlation;
```

The framework never reads it. It is kept, never copied, on `DecisionResult.Correlation`, pipeline
errors included, and on every `AgentContext` made for the decision: the ones the availability
checks, target and observation sources, guards and memory see while deciding, and the one
`Execute` hands the handler. It never reaches the model or the telemetry. It says which call this
is, never why the agent is asked, so it stays inside DR-008.

A guard reads it from `input.Context.Correlation`, which is how a game's guard could refuse a
decision that belongs to a conversation it has moved past.

The same goes for availability. An `IsAvailable` that depends on who asked, such as an alarm only
a player may raise, reads `ctx.Correlation`. It gets the call's own correlation while deciding,
and again in the re-check at `Execute`, whatever a later call has set meanwhile. Per-call facts
belong there rather than in `Agent.State`, which every call shares (GreyBoxVillage's G11). The
framework still decides nothing from it: the developer's own check does.

`Correlation` is the caller's alone. To say that one agent's decision led to another's, use
`RelayAsync` and `DecideOptions.Chain` instead, which the telemetry and the decision log record
([Chains of relayed decisions](logging-decisions.md#chains-of-relayed-decisions)).

## Memory that waits for the game: `MemoryCommit`

By default an agent remembers each decision as `DecideAsync` returns it. A game that may drop a
reply after it arrives (it timed out, the player walked away, another speaker took over) would
then have an agent that remembers saying something nobody heard. Set the agent to wait:

```csharp
agent.MemoryCommit = MemoryCommit.OnExecute;
```

Now a decision is held on its own result:

- `Execute` remembers it when it carries it out: `Done` or `NoAction`. Executing it again does
  not remember it twice.
- One that `Execute` refuses, or whose handler reports `Failed`, keeps waiting. The game may
  still `agent.Commit(result)`, say because the agent's line was shown anyway, or
  `agent.Discard(result)` so that nothing remembers it later.
- One the game simply drops is never remembered. It needs no clean-up: it is held on the
  result, not on the agent.

A game that asks an agent one thing at a time, such as a chat, can leave this bookkeeping to an
optional `Conversation` ([conversations.md](conversations.md)): it refuses a stale reply at
`Execute` and discards it from memory. A game where some decisions are offers the player
confirms can hold them in an optional `Proposals` ([proposals.md](proposals.md)), which remembers
a proposal once, when it is held, and carries it out through `Execute` on confirm. A game that
asks several agents at once and acts on their answers in an order of its own can use an optional
`DecisionRound` ([decision-rounds.md](decision-rounds.md)): it remembers a decision only when it
carries it out, and nothing from a round that is cancelled or a decision it never executes.

`Commit` and `Discard` return whether the decision was waiting. Only the agent that made a
decision can settle it; another agent's `Commit` or `Discard` throws, and its `Execute` may carry
the decision out without touching the first agent's memory.

What is remembered is exactly what `OnDecide` would have remembered, with the same context and
request, into the memory the decision recalled from. The difference is order: decisions are
remembered in the order they are settled. The mode is read when a decision starts, as
`Agent.Memory` is. It works with any `IMemoryStrategy`, since a strategy only sees `Remember`,
called later.

### One memory per `Agent`, not per character

Memory belongs to an `Agent` object. A game that runs two agents for one character, say a
scripted one and one the model drives, holds two memories, and each remembers only its own
decisions. To give them one, set both agents' `Agent.Memory` to the same `IMemoryStrategy`
instance. Under `MemoryCommit.OnExecute`, each agent still settles only the decisions it made.

## What a remembered decision holds: `Remembers` and outcomes

`RollingHistory` sends each past decision back to the model as the JSON the model gave, statement
included. That suits a conversation, where the agent's own words are the history. A turn-based game
may want the moves remembered instead, and what came of them: in GreyBoxStrategy a small model
copied its remembered statements for turns on end, and claimed land was taken that nobody took
(#244 item 4, #216 item 3). `RollingHistory.Remembers` chooses, at each recall:

| `RememberedDecision` | Each remembered decision is sent as |
|---|---|
| `Whole` (the default) | the decision as given, byte for byte as before: `{"action":"attack","target":"fenmark","statement":"Fenmark falls today."}` |
| `Move` | the decision without its statement: `{"action":"attack","target":"fenmark"}` |
| `MoveAndOutcome` | the move, then what came of it: `{"action":"attack","target":"fenmark","outcome":"took fenmark from orsk (34 against 21); lost 6"}` |

The stimulus of each exchange is sent unchanged in every setting, and the history keeps two
entries an exchange. The memory keeps everything whichever is set, so the setting can change at
any time and loses nothing; the token cap counts what is sent, so leaving statements out fits more
exchanges in the same budget.

The outcome is the game's own line, never a value from a list. Attach it through the agent, any
time after the decision, because it is often known only after the handler has run, or later in the
turn:

```csharp
var history = new RollingHistory { Remembers = RememberedDecision.MoveAndOutcome };
agent.Memory = history;
agent.MemoryCommit = MemoryCommit.OnExecute;

agent.Execute(result);                       // remembered now, before the battle resolves
// ...every move of the turn lands...
agent.SetOutcome(result, "took fenmark from orsk (34 against 21); lost 6");
```

`SetOutcome` replaces an outcome set before, and null or blank clears it. It returns whether a
memory holds the decision, or will once it is settled:

- A decision still held by `MemoryCommit.OnExecute` keeps the line and hands it to the memory when
  `Execute` or `Commit` remembers it. When `Execute` does not carry it out, the refusal's or
  failure's `ActionResult.Reason` becomes its outcome unless the game set one, so a decision
  committed anyway is remembered with why it failed. A later failure replaces that reason, a
  retry that succeeds drops it, and the game's own line always stays.
- One already remembered, under `OnDecide` or once settled, is amended in `Agent.Memory`.
- False for one discarded, dropped by `Reset`, forgotten by the turn cap, and for a memory that does
  not implement `IRemembersOutcomes`.

Under `Whole` and `Move` an outcome is kept and saved but never sent, so attaching outcomes changes
no prompt by default. Which setting helps is unmeasured: GreyBoxStrategy's A/B run on `qwen3.5:4b`
is owed (`docs/live-runs.md`), and the 2B will not be measured in it. No setting is per call, because what a memory
holds is the memory's to say, and a strategy of the Memory area's own decides it for itself.

## Saving memory with the game: `IExportsMemory`

A strategy that can be saved implements `IExportsMemory` beside `IMemoryStrategy`.
`RollingHistory`, the default, does. Its snapshot is plain data, public fields and lists, so it
goes straight into a save object for `JsonUtility` or Newtonsoft:

```csharp
[Serializable]
public sealed class SaveGame
{
    public int gold;
    public MemorySnapshot guardMemory;
}

// Saving:
if (guard.Memory is IExportsMemory saved) save.guardMemory = saved.Export();

// Loading:
if (guard.Memory is IExportsMemory loaded) loaded.Import(save.guardMemory);
```

A `MemorySnapshot` holds:

| Field | Holds |
|---|---|
| `Format`, `Version` | Which strategy wrote it, such as `RollingHistory.SnapshotFormat`, and its version |
| `Exchanges` | Every exchange remembered, oldest first, as `DecisionRequest.History` encodes it: the stimulus as sent, label included, then the whole decision as JSON, and its `Outcome` (null, or empty after `JsonUtility`, when none) |
| `Data` | Anything else the strategy keeps, in its own encoding; null or empty when nothing |

`RollingHistory.Export` holds every exchange it keeps, those the token cap leaves out of a
request included. `Import` replaces what it holds and keeps the newest `Turns` of the snapshot's
exchanges. It takes only a snapshot `RollingHistory` wrote: `RollingHistory.SnapshotFormat`, at a
version from 1 to `RollingHistory.SnapshotVersion`, which is 2 since outcomes are saved; a version 1
snapshot imports with no outcomes. An imported outcome is sent again, but `SetOutcome` cannot reach
it, since a save keeps no results. Another strategy's snapshot, a newer version,
or a missing exchange, stimulus or decision is refused with an `ArgumentException`, and the memory
keeps what it had. To move memory between strategies, copy the exchanges into a snapshot of the
target's format on purpose. `Dropped` and `Forgotten` are counts for telemetry, so they are not
saved.

To check a whole save before loading any of it, ask `CanImport` first. It changes nothing, and
gives the same reason `Import` would throw with:

```csharp
foreach (var saved in save.npcMemories)
    if (!history.CanImport(saved.memory, out string reason))
        return $"This save cannot be loaded: {reason}";
```

A decision still waiting under `MemoryCommit.OnExecute` is not in the memory yet, so it is not in
the snapshot either.

## Starting an agent afresh: `Reset`

After a scene event, a new stage or a debug key, a game may want an agent to start over: forget
what it remembers, and stop deciding whatever it was asked before. `Reset` does this without
destroying anything, so the agent keeps its identity, provider, actions, targets, sources, guards,
`State`, `Backlog` and `MemoryCommit`:

```csharp
guard.Agent.Reset();      // a plain Agent
behaviour.ResetAgent();   // the agent an AgentBehaviour built
```

It does three things (#190, G12):

- **Cancels this agent's decisions**, and only this agent's: other agents on the same provider
  keep their places. A call waiting in the agent's `Backlog` or in the provider's queue leaves it
  at once and is never sent. A call already with the provider is stopped the way a caller's own
  cancellation stops it, so it ends when the provider honours that; Ollama aborts the request at
  once. Until it ends, `IsDeciding` stays true. A call made after `Reset`, even one made from the
  `catch` of a call it cancelled, decides normally.
- **Drops the decisions held for `MemoryCommit.OnExecute`.** They are never remembered:
  `Commit` and `Discard` return `false` for them. `Execute` still carries one out if the game asks
  it to, with its usual re-check, and remembers nothing.
- **Clears memory**, through `IClearableMemory`, which `RollingHistory` implements. A strategy
  that does not implement it keeps what it holds, and a warning says so: implement the interface,
  or give `Agent.Memory` a new instance after the reset. A memory two agents share is cleared for
  both.

`AgentBehaviour`'s method is `ResetAgent`, not `Reset`, because Unity calls a component's `Reset`
in the editor. Before `Reset`, a game had to destroy the component and add it again, which built
a new agent and lost the guards, sources and state the game had added to the old one.

### Telling a reset from your own cancellation

A decision a reset cancels ends as any cancelled decision does: awaiting it throws
`OperationCanceledException`. **If your own token is not cancelled, the agent was reset.** The
exception's `CancellationToken` is then the agent's own, and when you cancelled the call
yourself it is your token, as it always was.

```csharp
try
{
    var result = await agent.DecideAsync(line, options, myToken);
    agent.Execute(result);
}
catch (OperationCanceledException) when (!myToken.IsCancellationRequested)
{
    // The agent was reset: this call belonged to the agent before it. Show nothing.
}
catch (OperationCanceledException)
{
    // You cancelled it yourself.
}
```

A game that already catches `OperationCanceledException` around `DecideAsync`, as GreyBoxVillage's
`AgentMind` does, needs no change to survive a reset. A `Conversation` turn whose decision a reset
cancels ends `Cancelled`.

To make this work, a provider is sent a token linked to both the caller's and the agent's reset,
never the caller's own token object. Cancelling either one cancels it.

## Extension points

- **Memory (#119).** A new strategy implements `IExportsMemory` to be saved, with what it keeps
  beyond exchanges in `Data`, and `IClearableMemory` to be cleared by `Agent.Reset`, facts or a
  summary included. `MemoryCommit` needs nothing from it. A strategy that keeps outcomes implements
  `IRemembersOutcomes`, and decides for itself how they reach the model.
- **Guards (#138).** A guard reads `Correlation` from its input's context. The re-check at
  `Execute` is the one place a rule is checked at the moment of acting; until a guard can run
  there too, such a rule belongs in the handler, which can now say why with `ActionResult.Failed`.
