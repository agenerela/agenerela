# Decision rounds — several agents, one snapshot, your order

**This is optional.** Nothing in the framework needs it, and an agent works exactly as before
without it. `DecisionRound` is plumbing for a game that asks several agents at once and then acts
on all their answers: a turn of a strategy game, a colony's dawn, a bridge crew answering one
order (#216, item 2; DR-021).

It is a small, plain C# helper beside the agents, in the spirit of [`Conversation`](conversations.md):

- **everyone decides on one snapshot.** `DecisionRound.DecideAsync` asks every agent through the
  normal queue and returns only when all have answered, with nothing executed;
- **you give the order afterwards**, having read the results, and may give it in phases;
- **every decision is re-checked as it runs**, by `Agent.Execute`, so a move an earlier one made
  illegal is refused, and the round reports it as stale;
- **cancelling drops the whole round**, and nothing from it enters memory.

**The game still decides everything about the moment** (DR-008): when a round happens, who is in
it, what each is asked, and the order. Nothing reaches the model differently: each agent decides
exactly as it would with `DecideAsync`. Delete it, and nothing in `Agent` changes.

Every rule here runs as a test, in `Tests/Editor/Core/DecisionRoundTests.cs`.

## A round

```csharp
var round = await DecisionRound.DecideAsync(
    countries.Select(c => new RoundCall(c.Agent, "Your move.", new DecideOptions
    {
        StimulusLabel = "Council",
        Observations = c.TurnFacts,
        Priority = DecisionPriority.Background,
    })),
    id: $"turn-{turn}-council",
    ct: turnCancelled);

// Peace first, then home moves, then wars, then attacks, seats rotating: an order read from
// what was chosen, so it can only be given now.
foreach (var phase in new[] { "offer_peace", "recruit", "raise_taxes", "fortify", "declare_war", "attack" })
    foreach (var entry in round.Execute(Rotated(round.Waiting).Where(e => e.ActionId == phase).Select(e => e.Agent)))
        Report(entry);   // entry.Outcome, entry.Stale

round.End();             // whatever is left, a hold or a pipeline error, is never acted on
```

`RoundCall` takes what `DecideAsync` would: the agent, the stimulus and the `DecideOptions`, which
are copied. Each agent is in a round at most once. No calls at all make an empty round, decided at
once, so a turn in which nobody is offered peace needs no special case. The calls are made in the order given, so among
equal priorities that is the queue's order; it says nothing about the order of execution.

## Executing, in phases

`Execute(order)` runs the named agents' decisions in that order, each through
`Agent.Execute(result, out outcome)`, and returns their entries in the same order. Call it once
with everyone, or once per phase: each call runs only the agents it names, and nobody is asked
again. An agent you leave out stays in `round.Waiting`.

The whole order is checked before anything runs, so a mistake in it executes nothing: an agent
not in the round, or named twice, throws `ArgumentException`; one already executed, failed or
skipped throws `InvalidOperationException`. So does calling `Execute` or `End` from inside a
handler the round is running: return, then run the next phase.

Each `RoundEntry` holds:

| Member | What it is |
|---|---|
| `Agent`, `Stimulus`, `Index` | Whose entry, what it was asked, its place in the calls |
| `Result` | What `DecideAsync` returned: a decision, or a pipeline error, which is this agent's alone |
| `Decision`, `ActionId` | The decision and its action, or null; `ActionId` is what an order is usually built from |
| `Status` | `Decided` (waiting), `Executed`, `Failed` (the call threw: no result), `Threw` (its handler threw), `Skipped` (`End` ended the round first) |
| `Outcome` | What `Execute` reported, once executed: `Done`, `Failed` by the handler, `NoAction`, or `Refused` with the reason |
| `Stale` | Whether `Execute` refused a decision `DecideAsync` had accepted, because something since, usually an earlier move in the round, made it illegal |
| `Error` | Why there is no result (`Failed`), or what the handler threw (`Threw`) |

A refusal is stale only when the decision was a legal move to begin with. A pipeline error, or a
reply a guard rejected inside `DecideAsync`, is refused too, as `Execute` always refuses them, but
was never a move that could go stale.

### Writing the re-check

The stale case is only as good as the rules `Execute` checks again. It re-runs the legality check
(registered action and target, availability), the category guard and the per-target check
(`ITargetAvailability`, `[AvailableFor]`; [targets.md](targets.md#conditions-per-target)). So a
rule another agent's move can break, such as "the province is still neutral" or "we are still at
war with them", belongs in `[Available]`, `IsAvailable` or a per-target check, read from the game's
state as it is now. A rule only the handler knows can still refuse, through
`ActionResult.Failed`, but that is a failure, not a stale move.

## Memory

An agent with a `Memory` must use `MemoryCommit.OnExecute`; `DecideAsync` throws
`InvalidOperationException` before asking anyone otherwise, because under `OnDecide` an answer
would be remembered before the round could drop it. An agent with no memory needs nothing.

So a decision is remembered exactly when it is carried out: `Executed` with `Done` or `NoAction`.
One refused as stale, one whose handler failed, and one never executed are not. `End()` marks every
waiting entry `Skipped` and discards its decision (`Agent.Discard`), so a later `Commit` or
`Execute` of it remembers nothing either. Not calling `End` is safe too: a decision never executed
is never remembered unless the game commits it.

## Cancellation and failures

Cancelling the round's token while any agent is still deciding cancels every request still out,
discards every answer already in, and ends `DecideAsync` with `OperationCanceledException`. It ends
at once, even when a provider ignores cancellation; that provider's answer is discarded when it
comes. Once the round is returned, the token changes nothing.

One agent's trouble is its own:

- a **pipeline error** (an unreachable server, an unreadable reply) is that agent's `Result`, as it
  would be from `DecideAsync`; the others decide normally. Executing it is refused, not stale;
- a **call that throws**, as one cancelled by `Agent.Reset` during the round does, leaves that
  entry `Failed` with the exception in `Error`, and the round still returns;
- a **handler that throws** stops that `Execute` call and passes the exception on, as
  `Agent.Execute` does: its entry is `Threw`, those before it keep their outcomes, those after it
  are still waiting.

## In the log

Each decision is an ordinary `DecideAsync` call, so `DecisionLog` and `DecisionFeed` record it as
any other, and each execution as an `Executed` record. A round's decisions share one chain
([logging-decisions.md](logging-decisions.md#chains-of-relayed-decisions)): each call whose options
carry no `Chain` is given `DecisionChain.Start(round.Id)`, so its log line has the round's id as
`chainId`, at hop 0, and a decision relayed from it with `RelayAsync` stays in that chain. Pass
`id` to name the round (`"turn-7-council"`); without it, a new id is made. A call with a chain of
its own keeps it. `DecideOptions.Correlation` is never read or set: it stays yours, for handlers to
read as before.

## What it does not do

- **It never decides when a round happens**, or classifies why the agents are asked (DR-008).
- **It does not order anything for you.** There is no comparer or phase list: the order is a list
  of agents you build from the results.
- **It does not merge rounds.** Decisions that depend on other decisions, such as an envoy asked
  after a council offered peace, are a second round, decided before either is executed; the two
  rounds' entries can then be executed interleaved, in whatever order the game's rules say.
- **No deadline of its own.** A round that must end in time takes a token the game cancels with a
  timer.

**Extension point:** a broadcast, one order to a crew whose answers are merged (#216, F8), would be
a mode beside this one, once a demo needs it.
