# Simpler demos, and what the framework still lacks

**Status:** a proposal, written 9 November 2026. Nothing on this page is decided. It
suggests which demos to build after the two 3D ones, and lists the framework pieces those
demos would need, checked against the code on `test` that day.

## Why simpler

GreyBoxVillage and Living Vale both work, but most of their time went into 3D:
terrain, cameras, animation, navigation, generated art. None of that shows the framework.
The demos below follow four rules, so the time goes into the agents instead:

1. **2D or UI only.** No models, no terrain, no LFS art. Coloured boxes and text.
2. **The game's rules are plain C#,** tested in EditMode with seeded dice. A whole game
   then runs headless on `ScriptedProvider`, so it can be checked without anyone playing it.
3. **One slice is one pull request,** and a slice takes one person a week or two.
4. **Each demo proves something no other demo does.** If two would prove the same thing,
   build one.

## The lineup

| Demo | Agents | What only it proves | Suggested order |
|---|---|---|---|
| **GreyBoxStrategy** (#20), [design](grey-box-strategy.md) | 5 countries | An agent with no body; agent to agent; stale moves within a turn; the eval set's `turn` category | **Now** |
| **Hearth**, a colony, as GreyBox2D's genre (#44) | 8 to 12 settlers | Many agents in one queue; the Unity adapter in 2D; knowledge shared by a group | After GreyBoxStrategy's stage 2 |
| **Risk race, top-down 2D** (#53) | 4 racers | A deadline: the world does not wait for the answer | Any time; its grey box needs no framework |
| **Bridge crew** (optional) | 4 officers | One line heard by several agents, most of whom must answer `none` | Only if a slot opens |

### Hearth: a colony (GreyBox2D, #44)

#44 is waiting for a genre. This is a suggestion for it.

**The game.** Eight to twelve settlers live on a 2D grid of tiles: trees, fields, a well,
a storehouse and a wall. Each settler has hunger, tiredness and a trade. At dawn, and when
something happens (*"raiders seen to the north"*, *"the well is dry"*), each settler picks
a job: `chop`, `farm`, `fetch_water`, `haul`, `guard`, `rest`, or `none`. Ordinary game
code walks them there tile by tile. The colony survives 30 days or it doesn't. The player
is the reeve: they set one priority a day, which each settler hears as a stimulus.

**What it proves.** Twelve agents asking at dawn is the queue at the scale the build plan
worries about (§2.6, about 21 s for 30 agents). It tests `Background` priority, backlogs
(`DecisionBacklog.LatestWins` per settler) and the scheduler. Settlers are 2D sprites
with `AgentBehaviour` and `Targetable`, so the Unity adapter is proven in a second
dimension. Knowledge belongs to the colony as a whole: everyone should know the well is dry.

**Why it's buildable.** Grid movement, no physics, no pathfinding beyond a breadth-first
search on a small grid. Sprites are flat colours.

### Risk race, top-down 2D (#53)

Same idea as [`risk-race.md`](risk-race.md): racers choose a safe or a risky route at
forks, a few seconds ahead, while they keep driving. Drawn from above, as dots on a loop of
line segments, it keeps everything that page says the demo proves (deadlines, stale
decisions at the fork, four agents asking at once). It drops the 3D track, the cameras and
the cars. **#53 records "3D decided".** This page suggests reopening that, for the reason
this page exists.

### Bridge crew (optional)

A starship bridge as a single text screen. The captain types one order, such as *"Shields
up and get us out of here"*. Four officers hear it: helm, weapons, engineering and the
medic. Each decides with its own action set, so most of them should answer `none`. It is
the cheapest demo here, with no map at all. It proves the thing every multi-agent game
needs and nobody has shown yet: one stimulus, several listeners, and only the right ones act.

## What the framework still lacks

Each entry names the demo that needs it, what exists today, and what is missing.
**Off until A/B'd** marks anything that changes what reaches the model. Those ship behind
an option that is off, as #190's rules say, and become the default only after a run with a
control arm (hard rule 2). Entries already in #190 are only linked here.

### Memory

**F1. Remember what came of a decision, not only the decision.** *Strategy, Colony.*
Today `RollingHistory` stores the stimulus and the decision as JSON. `Execute` now returns
an `ActionResult` (#195), but nothing carries it into memory. So a country remembers that
it attacked Orsk, and never that it lost 18 soldiers doing it. The proposal: an option on
`RollingHistory` (or a decorator strategy) that appends the result's `Reason`, or a short
line the handler returns, to the remembered exchange. **Off until A/B'd.**

**F4. A ledger: keyed facts that expire.** *Strategy, Colony, Living Vale (#190 LV #14).*
`IMemoryStrategy` can only return exchange pairs. A fact such as *"orsk broke a truce with
us on turn 6"* or *"the well is dry"* is not an exchange. Every game ends up rebuilding the
same thing as an observation source. The proposal: a stock `FactLedger` that is both an
`IObservationSource` and `IExportsMemory`, with `Set(key, line, expiresAfter)` and
`Remove(key)`, and lines rendered in a stable order within a budget. One instance can be
shared by several agents. That is the colony's group knowledge, and it does not break
memory's "never shared" rule, because it is an observation source. Existing sources already
work this way, so it adds a convenience, not a new path to the model. **Built** as `FactLedger` and `FactLedgerView` in `Runtime/Observations/` (#295, 14 November).

**F5. A summary of older memory.** *Colony; any agent that lives long.* A settler lives
30 days, but `RollingHistory` keeps 6 exchanges. The proposal: a strategy that folds
exchanges as they age out into a summary line, built by game code or by a background model
call refreshed on events, never once per decision (§2.8). **Off until A/B'd.** This belongs
to the memory research area (#119).

### Targets and actions

**F2. Examples that carry their situation.** *Strategy, Colony, Risk race: every
game-triggered agent.* An example today is a stimulus and an answer (`ExampleAttribute`),
with no observations. When the game asks the same question every time (*"Choose this turn's
move"*), the situation lives only in the observations, so no example can show *when* to
choose an action. GreyBoxStrategy works around it by putting a headline in the stimulus.
The proposal: let an example carry one or two observation lines, rendered the way a real
call's are. **Off until A/B'd**, and the examples must still never be evaluation prompts
(hard rule 4). This is likely the biggest accuracy lever for game-triggered agents. It
pairs with #190's G14, which found that examples' stimuli get echoed back as answers.

**F3. Availability per target.** *Strategy, Colony (#190 LV #19).* `[Available]` answers
for an action as a whole. *"You may attack any country, but not one you have a truce with"*
can only be said through categories, as GreyBoxStrategy does. *"Chop any tree no one else
has claimed"* can only be said by re-registering the targets before every decision. The
proposal: an optional `IsAvailableFor(actionId, targetId, ctx)` that feeds the paired schema
(#210), the category guard's verdicts and `Execute`'s re-check. The paired schema is the
part that already exists, so this extends it rather than adding a new mechanism.

**F6. Line of sight in 2D.** *Colony.* `ProximityTargetSource` finds targets by distance
over every `Targetable` in the scene, which already works in 2D. Its line-of-sight check is
a 3D `Physics.RaycastAll` (`TargetableQuery.CanSee`), which never hits a 2D collider. The
proposal: use `Physics2D` when the origin has a `Collider2D` or a `Rigidbody2D`, or a
`LineOfSight` setting of None, 3D or 2D. It is small, testable and changes no prompt.

### Several agents at once

**F7. A decision round.** *Strategy (every turn), Colony (every dawn), Bridge crew.* "Ask
these agents on one snapshot, wait for all of them, then execute in an order I give,
re-checking each" is written by hand in every turn-based game. The proposal: an optional
helper, `DecisionRound`, in the spirit of #209's conversation helper. You give it the
agents, a stimulus per agent and an order. It returns each agent's result and receipt,
including the moves `Execute` refused as stale. Optional, because the framework never
decides *when* to ask (DR-008).

**F8. Broadcast, for when several agents hear one line.** *Bridge crew, GreyBoxVillage's
captain.* This is the same round with one stimulus, plus a rule for when two listeners both
act. The game supplies the rule. It may be a mode of F7 rather than its own type.

**F9. Relay with a chain id.** *Strategy's envoys.* Already #190 G4. GreyBoxStrategy
would be its second user, which is a good reason to build it before the demo hand-rolls one.

**F10. Fair turns in the background queue.** *Colony.* Today `RequestScheduler` serves
each priority tier first come, first served. One settler who is asked often can push the
others back. The proposal: round-robin per agent within the `Background` tier, as an option
on the scheduler. It changes no prompt, only the order, and the queue's existing tests show
where it fits.

### Time

**F11. A deadline and a fallback.** *Risk race.* `DecideOptions` has no deadline. The game
cancels a token itself and decides on its own what "too late" means. The scheduler has no
time limit for requests still waiting in the queue either. The proposal: a
`DecideOptions.Deadline`, a fallback decision returned when it passes (marked late in
telemetry, never remembered), and a queued request dropped once its deadline is gone, so
the model never spends time on an answer nobody can use.

### Tools

**F12. Replaying a whole game.** *Strategy and Colony A/B runs.* This needs #190 G7 (a
runtime decision log) and LV #21 (a stable `AgentId` in `ScriptedProvider`). With both,
plus seeded dice, a 20-turn game played by one model can be replayed exactly, and compared
turn by turn with another model on the same luck.

### In order of payoff

| # | Piece | Needed by | Changes the prompt? |
|---|---|---|---|
| F2 | Examples with their situation | every game-triggered agent | yes, off until A/B'd |
| F7 | Decision round | Strategy, Colony, Bridge crew | no |
| F1 | Outcome in memory | Strategy, Colony | yes, off until A/B'd |
| F4 | Fact ledger | Strategy, Colony, Living Vale | no new path (an observation source) |
| F3 | Availability per target | Strategy, Colony, Living Vale | through the paired schema |
| F11 | Deadline and fallback | Risk race | no |
| F6 | Line of sight in 2D | Colony | no |
| F10 | Fair background turns | Colony | no |
| F9, F12 | Relay; replay | Strategy | no (already in #190) |
| F5, F8 | Summary memory; broadcast | Colony; Bridge crew | F5 yes |

Each demo can ship with a hand-written workaround first, as Living Vale did. Its log of
what it had to build for itself becomes the evidence for the framework piece, exactly as
#190 came from the first two demos.
