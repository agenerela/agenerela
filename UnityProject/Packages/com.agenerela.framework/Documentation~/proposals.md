# Proposals — the agent offers, the player confirms

**This is optional.** Nothing in the framework needs it, and an agent works exactly as before
without it. `Proposals` is plumbing for a game where some of an agent's decisions are offers the
player accepts or turns down: a quest, a trade, a route, a deal between two countries. The agent
decides `offer_route`; the game shows a card; the route only starts if the player clicks
**Confirm**, and only if it still can (#190, Living Vale #16).

It is a small, plain C# helper beside the agent, in the spirit of [`Conversation`](conversations.md):

- **it holds a decision instead of carrying it out**, for the actions the game names;
- **confirm carries it out through `Agent.Execute`**, so the check at confirm is the agent's own
  re-check: legality, categories and the per-target check ([targets.md](targets.md#conditions-per-target)).
  A route that closed while the card was on screen is refused, with the check's reason;
- **a click on an old card is refused**: confirm takes the proposal object the card was made
  from, so a proposal that was superseded, declined or reset never runs, and neither does
  whatever happens to be pending now;
- **a reset leaves nothing held.**

**The game still decides everything about the moment** (DR-008): when the agent is asked, which
actions want a confirmation, and when a card goes away. Nothing reaches the model differently:
the agent decides exactly as it would without the helper. Delete it, and nothing in `Agent`
changes.

Every rule here runs as a test, in `Tests/Editor/Core/ProposalsTests.cs`.

## Setting one up

```csharp
var offers = new Proposals(kest, heldActions: new[] { "offer_route", "offer_exchange" })
{
    OnePending = true,                                       // one card at a time
    StillValid = p => (int)p.Correlation == talk.Generation, // the game's own rule, if it has one
};
offers.Changed += _ => RedrawCards(offers.Pending);
```

`talk` stands for the game's own conversation state; the framework has no generation of its own.
`HeldActions` is a set of action ids, ordinal, which can be changed at any time. `OnePending` is
off by default, so every proposal waits until it is settled; on, a new proposal supersedes the
ones waiting, as a new card replaces the old.

## A decision, held or carried out

```csharp
var result = await kest.DecideAsync(line, new DecideOptions { Correlation = talk.Generation });

var proposal = offers.Submit(result, out var done);
if (proposal != null)
    ShowCard(proposal, result.Decision.Statement);   // held: nothing has run
else
    Say(result.Decision.Statement);                  // carried out at once; done says what it did
```

`Submit` holds the decision when its action is in `HeldActions`, and otherwise carries it out at
once through `Agent.Execute`, returning `null` with the outcome in `done`. A pipeline error, a
rejected answer and `none` are never held. `Hold(result)` holds a decision whatever its action,
for a game that decides that itself; it throws for a result with nothing to propose.

A held decision is not checked again when it is held: it was legal when the agent made it, and
the agent checks it again at confirm.

## The card's buttons

```csharp
void OnConfirm(Proposal proposal)
{
    if (offers.Confirm(proposal, out var outcome))
        Toast("Route started.");
    else
        Toast(outcome.Reason);       // superseded, stale, or the agent's re-check said no
}

void OnDecline(Proposal proposal) => offers.Decline(proposal);
void OnConversationClosed()       => offers.DeclineAll();
```

`Confirm` asks, in order:

1. Is it still pending? A proposal superseded, declined, reset or already confirmed is refused.
2. Does the game's `StillValid` predicate, when set, say it still stands?
3. `Agent.Execute`: the action is registered and available, the target registered and in the
   action's categories, and the per-target check allows the pair now. A pair that went stale is
   refused with the check's reason, for example "'offer_route' cannot be done to 'route_ford'
   right now: the ford is flooded". Then the handler runs.

A proposal runs only while it is in `Pending`, checked again after `StillValid`, since the
predicate is the game's code and may decline it or reset the agent. It leaves `Pending` before its
handler runs, so no handler, however deeply confirmations nest, can confirm it twice.

| `proposal.Status` | Means |
|---|---|
| `Pending` | Held, waiting for the player |
| `Accepted` | Confirmed, and carried out |
| `Failed` | Confirmed, and the handler reported `ExecutionStatus.Failed`, or threw |
| `Refused` | Confirmed, and refused without running a handler: `StillValid` said no, or the agent's re-check did |
| `Declined` | `Decline` or `DeclineAll` |
| `Superseded` | A newer proposal replaced it, with `OnePending` on |
| `Reset` | The agent was reset while it waited |

The status changes once. `proposal.Outcome` is what `Agent.Execute` reported, null until then;
`proposal.Reason` says why one ended without being carried out. `Changed` is raised once per call
that changes the pending set, however many proposals it settled.

## Memory, telemetry and reset

**A proposal is remembered when it is held, once.** The agent did propose it, and the player saw
the offer, whatever they answer next. Under `MemoryCommit.OnDecide` it was remembered as
`DecideAsync` returned it; under `MemoryCommit.OnExecute`, `Hold` commits it (`agent.Commit`).
Either way, `Agent.Execute` at confirm finds it settled and does not remember it again, and
declining it does not forget it. That follows how `OnExecute` already works
([executing-and-memory.md](executing-and-memory.md)): a decision is settled once, by whichever of
`Execute`, `Commit` or `Discard` comes first.

What the player answered is not in memory; the game observes it if the agent should know
(an observation such as "The traveller declined the ford route."). A built-in observation source
for waiting proposals was considered and left out: its wording would be unmeasured, and it would
be one more switch that changes what the model reads.

**Telemetry tells an offer from an accepted one.** `DecisionTelemetry.Execution` stays null while
a proposal waits; the `DecisionStage.Executed` record in the decision feed comes only at confirm. A
refusal by the helper itself (superseded, already settled, `StillValid`) is written to the result's
telemetry as `Refused` with its reason, unless the result already has an `Execution` of its own,
from any proposal or a direct `agent.Execute`, which is never overwritten; it is not
logged or raised on the feed, as a conversation's refusals are not. A proposal declined,
superseded or reset is never executed and leaves its telemetry as it was.

**`Agent.Reset()` leaves nothing held.** The helper watches the agent's reset: every pending
proposal ends as `Reset` during the reset, and `Changed` is raised. A `Changed` handler that throws
then is logged, so it cannot fail the reset. No call to `DeclineAll` is needed. `Pending` is empty
when `Reset` returns: `Hold` throws if a `Changed` handler calls it during the reset. A decision made
before a reset must not be held after it either; `Hold` refuses one it can tell, a result held for
memory under `OnExecute`, but with no memory the helper cannot tell, so that is the caller's to
avoid.

## With a conversation

A held turn's result is safe from the conversation moving on: it was committed when held, so the
conversation discarding it as stale changes nothing. `Confirm` calls `Agent.Execute` directly, not
`Conversation.Execute`, so a card can be confirmed after the next line. If an offer should lapse
when the conversation moves on, say so in `StillValid`, for example
`p => chat.IsCurrent(p.Result)`, or call `DeclineAll` when the game's conversation closes.

## What it does not do

- **Decide which actions need a confirmation.** The game lists them.
- **Show anything.** The card, its wording and its buttons are the game's.
- **Save.** Proposals live as long as the helper; on loading a save, call `DeclineAll`.
- **Check at hold.** The agent's check at decide, and again at confirm, is the check.

## Replacing Living Vale's route cards

Living Vale's `Game/QuestRoutes.cs` built this by hand: `RouteProposal` with a conversation
generation, `TryPropose` replacing the earlier card, `Confirm` re-checking `RouteContract.Available`
and the generation, `Decline`, and `InvalidatePending` on open, close or load. With the helper, the
handler of `offer_route` does what `Confirm` applied; `RouteContract.Available` becomes the
action's `[AvailableFor]` check; the generation goes in `DecideOptions.Correlation` and
`StillValid`; `InvalidatePending` becomes `DeclineAll`; and `OnePending` replaces the hand-written
supersede.
