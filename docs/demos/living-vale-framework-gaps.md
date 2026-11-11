# Living Vale — gaps in the framework

**Status:** living list, started 5 November 2026; the merged features adopted on 10 November (below). Gathered with GreyBoxVillage's list in umbrella issue #190, where the master implementation session ticks what lands. Every Living Vale session adds to it the
day it works around something. It records what building a real game on Agenerela showed
the framework is missing: what the game needed, how it worked around it, and what a framework
feature would change. **None of this is decided.** Each entry becomes an issue, or a decision
record, only when the lead takes it up. Measured claims link to
[findings](../llm-wiki/findings.md).

The game uses the framework's public API only (`Demos/README.md`), so every workaround
below lives in `Demos/LivingVale/Assets/LivingVale/`. That is what makes this list
trustworthy: each entry names code a framework feature would delete.

## The adoption (10 November 2026)

Living Vale adopted every merged #190 feature in one pull request, branch `69-lv-adopt-190`, one
commit per feature. The game's own code (not its tests) went from 12,871 to 12,603 lines of code,
blank and comment lines not counted: **268 fewer**. Each entry's status below says what its
workaround became. In short:

| Gap | Framework feature | Workaround now |
|---|---|---|
| 1, 9, 13 | action and target list, counter-examples, idle example (#193) | adopted in G5g; the last per-turn plumbing, `RefreshExamples`, went with #234 (entry 20) |
| 2, 3, 5, 11 | execution results, commit on execute, correlation, memory export (#195) | adopted in G5h |
| 4 | target categories (#208) | deleted: the trade and quest pair guards |
| 6, 7 | model lifecycle, provider swap (#198) | deleted: `OllamaModelCatalog`, the unload loader, `WithProvider`/`WithOllamaModel`/`Detach`, the throwaway warm-up decision |
| 8 | `Conversation` (#209) | deleted: the coordinator's cancellation sources, deadline race and stale drop |
| 14 | identity knowledge and statement rules (#231) | deleted: facts as observations, rules appended to every personality |
| 16 | `Proposals` (#236) | deleted: `RouteProposal` and its bookkeeping, for route cards; the trade quote and Iven's main-quest offer keep their own cards |
| 17 | docs only (#236) | the game already had the shape; Fen's persona still states her clue (entry 17) |
| 19 | per-target availability (#235) | deleted: `RoutePairGuard`, `CheckPair`, the three `Offers` checks and handlers; the sources still filter (entry 19) |
| 20 | example target fallback, `INamedTarget` (#234) | deleted: the three `RefreshExamples`; the route actions prefer their own targets instead of the fallback (entry 20) |
| 21 | `Agent.Id` (#217) | deleted: `ScriptedAgentDriver`'s name-to-id map |
| 22 | `RollingHistory.CanImport` (#205) | deleted: the archive's own format check and throwaway import |
| 23 | one memory per agent (#205), provider swap (#198) | deleted: the second agent per NPC and `SwitchableNpcDecisionDriver` |
| G6 | `ScriptedProvider` (#194) | deleted: the scripted driver's own decide-and-execute path |
| G7 | `DecisionLog` (#229) | the game had no log of its own; it now writes one |
| 10, 12, 15, 18 | not built | unchanged |

**What did not fit**, each kept as game code and written down at its entry:

- **Entry 19.** A per-target check cannot keep a target out of the prompt's target list, which lists
  every registered target, check or no check. So the game's target sources still register only
  what the game offers now; registering every contract would show Kest "Release on Fen's account"
  before Fen has told anything. The check is kept for what it does add: masking an action with
  nothing to do, the re-check at `Execute` with its reason, and telemetry.
- **Entry 16.** `Proposals` belongs to one agent, but the game shows one card at a time across all
  NPCs, so the driver keeps one `Proposals` per NPC and adds what the card needs on top: the
  pending card across NPCs, finding a proposal's owner, declining every NPC's cards. It is
  about 40 lines a game with more than one proposing agent will write. *Since #244 item 6: a
  recipe in the framework's `Documentation~/proposals.md` ("One card across several agents"),
  no helper.*
- **Entry 16.** `Proposals.Hold` runs no re-check, so a decision that went stale between the
  decision and the hold still makes a card; the driver asks the executor first. *Fixed by #244
  item 6: `Hold` re-checks as `Agent.Execute` does and returns null for a stale decision; the
  driver's own check of the conversation and the contract stays the game's.*
- **Entry 16.** A confirm the agent refuses before the handler runs (the pair is no longer
  offered) produces no line of the game's: `ActionResult.Refused` is internal, and the outcome
  says only "refused". The card writes its own chat line. *Since #244 item 6:
  `ActionResult.AvailabilityReason` holds the per-target check's own words when that check
  refused, so the card can say why; other refusals still leave the line to the game.*
- **Entry 17.** Fen's persona knowledge still contains what she saw, now as identity knowledge
  instead of observations. The rule says a revealed fact is never standing knowledge, but the
  card target leaves once told, so without the lines Fen would no longer know what she saw.
  Content, for the game-content session.
- **Entry 20.** On the route actions the example-target fallback made gemma4 miss a delivery,
  3 of 3, in the same-session check on the RTX 5080: the fallback skips a target the player's line
  mentions, and a route line names its subject ("the parcel ... for your garrison"), so Elra's only
  delivery lost its example. The route actions now name their own targets as preferred examples,
  which the framework uses mentioned or not, as `RefreshExamples` did; the trade actions keep the
  fallback. Findings, 10 November, "Living Vale's adoption of #190".
- **Entry 8.** The conversation adoption saved no code: the coordinator's turn machinery went,
  but the driver now exposes each NPC's conversation and the turn's options, and the result
  mapping moved with it. What it bought is correctness the framework tests, not lines.

## The GreyBoxVillage log

GreyBoxVillage keeps its own list in `.scratch/gbv-master/framework-gaps.md` (scratch, so not in
git), G1–G9. Several entries overlap with this one:

| Here | There | Note |
|---|---|---|
| #1 | G9 | Same question. Living Vale's evidence settles it: verified in code, and the game had to add the lines |
| #4 | G2, G3 | Per-action target lists; G3 adds per-agent filtering |
| #7 | G5 | Queue behaviour while an agent is busy |
| #8 | G7 | Each game builds its own conversation and decision plumbing |

The rest are separate: G1 (provider health), G4 (agent-to-agent relay), G6 (scripted provider;
Living Vale wrote `ScriptedNpcDecisionDriver`), G8 (domain reload). When the lead turns either list
into issues, merge the overlapping entries.

## How to add an entry

Give each entry the next number and these parts:

- **Needed:** what the game needed, in one or two sentences.
- **Workaround:** the file and the trick.
- **Proposal:** the framework change, phrased as an option, not a decision.
- **Payoff:** what it would simplify or improve, for this game and for any developer.
- **Size and risk:** a rough guess, and which hard rule or settled design rule it touches.
- **Research area:** memory, targets, observations, evaluation or guards (build plan §6.6),
  when one owns it.

## Summary

| # | Gap | Payoff | Rough size |
|---|---|---|---|
| 1 | Actions and targets are invisible to a local model unless the developer writes them into the prompt | **Very high**: every action-taking game hits it | Medium |
| 2 | Execution returns only true/false; no receipt of what the game did | High | Medium |
| 3 | Memory records a decision before the game executes it | High | Small |
| 4 | One target list per agent; no action→target pairing | High | Medium |
| 5 | No per-call context reaches the handler | Medium | Small |
| 6 | Model lifecycle (list, warm up, unload, switch) is the developer's problem | Medium | Medium |
| 7 | Background requests in the shared queue delay player-facing turns | Medium | Medium |
| 8 | No built-in "this reply is stale" handling for real-time conversations | Medium | Medium |
| 9 | The model makes offers nobody asked for (fixed in the game by a negative example, G5f) | Medium (game feel) | Small |
| 10 | Grounding guard rejects some correct choices on some models | Medium | Research |
| 11 | Memory cannot be saved and restored with the game | Medium | Small |
| 12 | Decisions carry no parameters (quantity, price) | Low for now | Large |
| 13 | A code-built agent shows the model no `none` example unless the developer passes one | Medium | Small |
| 14 | An identity has no slot for what the agent knows, or for how it must speak | Medium | Small |
| 15 | Where an agent is and what it is doing has no standard shape | Low to medium | Small |
| 16 | No "propose, then the player confirms" step; every game builds its own cards | High | Medium |
| 17 | Knowledge an NPC holds cannot be tied to the action that reveals it | High (quest integrity) | Medium |
| 18 | The model cannot tell a player's claim from a game-verified fact | High | Small to medium |
| 19 | Per-target conditions live outside the framework; an absent action cannot say why | Medium | Medium |
| 20 | Few-shot examples name a target by its id, from a fixed list, never by its description | Medium | Small |
| 21 | `ScriptedProvider` matches the asking agent by display name, not a stable id | Low | Small |
| 22 | `RollingHistory.Import` accepts any snapshot format and version | Low | Small |
| 23 | Memory belongs to one agent, so two drivers for one NPC keep two | Low to medium | Small |

## Entries

### 1. Actions and targets are invisible to a local model unless the developer writes them into the prompt

**Status: adopted in the game (G5g).** The driver now sends `DecideOptions.ListActionsAndTargets`,
and `GameTargetOption` implements `IDescribedTarget`. In a same-session A/B (G5g, findings 5 November: "The framework's prompt features"), on 18 trade and talk lines and 7 quest-route lines per arm, the framework arm was as good on `gemma4:e2b-it-qat` (asked trades 5/5, unasked offers 0/13, routes 7/7 in both arms) and better on `qwen3.5:2b` (asked trades 5/5 against 4/5, routes 6/7 against 4/7, unasked offers 0/13 in both). The game's own action and
target lines (`PromptLines`) are deleted. What stays in the observations is game policy, one rule
line per action family (`RuleLines`), and game state (`StatusLines`), which the framework's list
does not carry. The per-turn example rewriting (`RefreshExamples`) went in the 10 November
adoption, with entry 20.

- **Needed:** for Mara to choose `quote_purchase` on `stock_mara_bread_one`, the model has to
  know the action exists, what it does, which targets it takes, and what each target is (item,
  price). Ollama only uses the schema to constrain sampling; a local model never reads it
  (AGENTS.md, findings of 10 October).
- **Workaround:** `Agenerela/TradeActionBinding.cs` and `QuestActionBinding.cs` write every
  available action, its description and each target with its price into observation lines on
  every turn (`PromptLines`). `RefreshExamples` rewrites each action's few-shot example every
  turn so it points at a target that exists now.
- **Verified in code (5 November):** `Core/PromptBuilder.cs`'s system prompt says "Choose exactly
  one action from the allowed list" but never lists the actions. It names target IDs only when
  `PromptOptions.TargetRule` is on (off by default), and never gives an action's or a target's
  description or display name. A local model learns the actions only from the few-shot examples
  and whatever the developer writes into observations.
- **Proposal:** a framework option that renders the available actions, their descriptions and
  each target's display name and description into the prompt, consistently and within budget,
  for providers that do not read the schema. Optionally, auto-generated few-shot examples from
  the live target list, kept apart from evaluation prompts (hard rule 4).
- **Payoff:** deletes about 150 lines of prompt plumbing per action family in this game, and makes
  action-taking work out of the box on local models for every developer. Today it silently
  does not, unless they know the schema rule.
- **Size and risk:** medium. It touches what the prompt contains, so it needs an A/B run on the
  evaluation set (hard rule 2). Field order stays action, target, statement.
- **Research area:** observations, and evaluation for the A/B.

### 2. Execution returns only true/false; no receipt of what the game did

**Status: adopted in the game (G5h).** Every Living Vale handler is an `IActionHandlerWithResult`
(`Agenerela/ExecutorActions.cs`), returning `Done` or `Failed` with the executor's `ActionOutcome`
as the payload. The driver reads `Agent.Execute(result, out ActionResult)` instead of each
binding's `LastOutcome`, which is gone, with `ClearOutcome`. The telemetry now records
`Execution` and `ExecutionReason`. What it does not cover: the trade and quest *receipts* still
come from `TradeService.Confirmed`, because they happen when the player confirms a card, after
`Execute` has returned. Since the 10 November adoption a route card's receipt does come back
through `Execute`: confirming the proposal (entry 16) runs the handler then, and its `ActionResult`
carries the receipt.

- **Needed:** after `Agent.Execute`, the game needs to know whether its own handler refused (a
  stale quote, not enough gold) and what it produced (the quote, the offer), to show the right
  line and to tell the model next turn what happened.
- **Workaround:** `Agent.Execute` returns true once the handler ran, so the game keeps its own
  outcome (`NpcActionExecutor`'s `ActionOutcome`, the bindings' `LastOutcome`) and checks it after
  `Execute`. Receipts reach the next turn as observation lines that the driver keeps
  (`lastReceipts`).
- **Proposal:** handlers return a result (success, reason, optional payload). `Execute` returns
  it, telemetry records it, and memory can include it ("you offered bread for 3 gold; the
  traveller bought it").
- **Payoff:** removes the parallel bookkeeping, and lets the evaluation separate "chose the right
  action" from "the action actually worked" (design §12, "execution receipts").
- **Size and risk:** medium, a public API change. Issue #69 already lists execution receipts.
- **Research area:** guards, or evaluation.

### 3. Memory records a decision before the game executes it

**Status: adopted in the game (G5h).** Each NPC's agent runs under `MemoryCommit.OnExecute`, and
`CommittedHistory` (81 lines) is deleted. A reply the coordinator drops as stale is never
executed, so it is never remembered. One the executor refuses (`Failed`) stays unsettled, and so
is not remembered either. The game needs neither `Commit` nor `Discard` today. The stale-reply,
cancel and refusal tests pass unchanged on the framework path.

- **Needed:** a reply dropped as stale (timeout, cancel, speaker switch) must not enter the
  NPC's memory, or the NPC "remembers" saying something the player never saw.
- **Workaround:** `Agenerela/CommittedHistory.cs` wraps `RollingHistory` and holds the newest
  decision until the driver commits it on execute.
- **Proposal:** a memory mode that commits on `Execute` (or on an explicit `Commit`/`Discard` of a
  `DecisionResult`), instead of on `DecideAsync`'s return.
- **Payoff:** removes a wrapper every real-time game would need. Memory then matches what
  actually happened.
- **Size and risk:** small. Default behaviour can stay as it is.
- **Research area:** memory.

### 4. One target list per agent; no action→target pairing

**Status: adopted (10 November).** Every target is registered in a category (stock, pack, quest,
fact, deal, handover, access) and each action declares its one, so `TargetCategoryGuard` does
what the trade and quest pair guards did, and both are deleted; the route guard went with entry 19.
**Not adopted:** the paired schema (`PairTargetsWithActions`, #210). It changes what the model is
sampled from, so it waits for its A/B (#151).

- **Needed:** `quote_purchase` takes stock entries; `quote_sale` takes sale bundles; `offer_quest`
  takes quest offers. One flat target list lets the model pair an action with another action's
  target.
- **Workaround:** a `DelegateGuard` pair guard in `TradeActionBinding` and `QuestActionBinding`
  rewrites a mismatched pair to `none`.
- **Proposal:** per-action target sources, so the schema constrains each action to its own targets
  (for example `oneOf` per action), with the legality guard checking the pair.
- **Payoff:** a whole class of wrong-but-legal answers becomes impossible to sample, not just
  caught afterwards. Fewer guards in every game.
- **Size and risk:** medium. It touches schema generation and Gemini's enum limits. Issue #69
  lists action-target compatibility.
- **Research area:** targets.

### 5. No per-call context reaches the handler

**Status: adopted in the game (G5h).** The driver passes the `NpcTurn` as `DecideOptions.Correlation`,
and the handlers read its generation from `AgentContext.Correlation`. `Agent.State["livingvale.generation"]`
and `TradeActionBinding.GenerationKey` are gone. A test checks the state stays empty.

- **Needed:** a handler must know which conversation (generation) a decision belongs to, so a
  quote is tied to that conversation alone.
- **Workaround:** the driver writes the generation into `Agent.State["livingvale.generation"]`
  just before `Execute`. That is shared mutable state on the agent.
- **Proposal:** `Execute(result, context)`, or a caller-supplied correlation object on
  `DecideOptions` that flows through to the handler's context.
- **Payoff:** removes a fragile global-ish channel, and makes concurrent agents safe.
- **Size and risk:** small. Related to #17's execution-time context.

### 6. Model lifecycle is the developer's problem

**Status: adopted (10 November).** F7 now gives every NPC's agent a new provider
(`AgenerelaNpcDecisionDriver.UseProvider`, `Agent.Provider`), so agents, memories and receipts stay
put, and `ModelCycle` warms up and unloads through the provider's `IModelLifecycle`; the selector
lists the server's models with `ListModelsAsync`. Deleted: `OllamaModelCatalog` and `IModelLoader`
(the game's own `/api/tags` and `keep_alive: 0` calls), `WithProvider`, `WithOllamaModel` and
`Detach`, and the throwaway warm-up decision. `ModelSelector` (the ordered list and skipping) and
`ModelCycle`'s debounce stay: they are the game's UX. F6 is a provider switch too (entry 23).

- **Needed:** list the installed models, warm one up, unload the one left behind, and switch an
  NPC's model at runtime without losing its memory.
- **Workaround:**
  - `Agenerela/ModelSelector.cs` (`OllamaModelCatalog` reads `/api/tags`), `ModelCycle.cs` (with a
    loader that sends `keep_alive: 0`).
  - `AgenerelaNpcDecisionDriver.WithProvider`, which rebuilds every agent and carries memories
    over.
  - The warm-up is a throwaway `Agent` with no memory.
- **Proposal:** on the provider, `ListModelsAsync`, `WarmUpAsync`, `UnloadAsync`. On the agent,
  a provider that can be swapped (or a provider config switch) that keeps memory and registrations.
- **Payoff:** about 400 lines of game code become a few calls. Every game that offers a model
  choice, or loads on demand, needs this.
- **Size and risk:** medium. It is provider-specific: Ollama first, others no-op.

### 7. Background requests in the shared queue delay player-facing turns

**Status: adopted for warm-ups (10 November).** The warm-up is the provider's `WarmUpAsync`, which
takes no slot in the decision queue, so a player's turn never waits behind it, and a newer press
aborts it. `Agent.Backlog` is not used: a chat asks one NPC one thing at a time (entry 8), so there
is no queue of the agent's own to manage.

- **Needed:** a player's turn must not wait behind warm-ups or other background work.
- **Workaround:** G5d debounces F7, cancels a superseded warm-up and unloads the old model. The
  bug it fixed: rapid presses queued about nine warm-ups, each up to 30 s, and the player's turns
  timed out behind them (play-check, 5 November).
- **Proposal:** a way to cancel or supersede queued background requests, and preemption, or a
  separate lane, so a player-facing request jumps a queued background one. The request in
  flight still finishes. Also keep warm-ups out of the decision queue.
- **Payoff:** responsiveness under load, which matters as soon as several NPCs think in the
  background (routines, perception).
- **Size and risk:** medium. DR-015 (priority tiers) is the starting point.

### 8. No built-in "this reply is stale" handling for real-time conversations

**Status: adopted (10 November).** Each NPC has a `Conversation` over its agent. The coordinator
starts each turn on it with its timeout, cancels it on close, switch or cancel, and executes the
answer only through it, so a late or stale answer never runs and never enters memory. Deleted: the
coordinator's cancellation source per turn, its deadline race and its stale-drop path. Kept, as game
policy: who is open, the open/close generation that quotes and cards are tied to, the chat log and
the events. It saved no lines (see "What did not fit"); the coordinator tests now run against the
real agents.

- **Needed:** one turn in flight per conversation, a timeout owned by the caller, and any reply
  that arrives after a cancel, close or switch dropped unexecuted.
- **Workaround:** the game's `Interaction/ConversationCoordinator` (generations, request IDs,
  its own deadline racing the backend). Codex review found two real races in the first version.
- **Proposal:** an optional conversation helper in the framework: a session object that issues
  `DecideAsync` calls, owns the timeout, and refuses to `Execute` a stale result.
- **Payoff:** every game with chat or barks needs this, and it is easy to get subtly wrong.
- **Size and risk:** medium. It must stay optional (DR-008: the framework never classifies when
  or why an agent is asked).

### 9. The model makes offers nobody asked for

**Status: adopted in the game (G5g).** Every Living Vale action now has a
`CounterExampleStimulus`, new wording, and the driver sends `DecideOptions.CounterExamples`. In a same-session A/B (G5g, findings 5 November: "The framework's prompt features"), on 18 trade and talk lines and 7 quest-route lines per arm, the framework arm was as good on `gemma4:e2b-it-qat` (asked trades 5/5, unasked offers 0/13, routes 7/7 in both arms) and better on `qwen3.5:2b` (asked trades 5/5 against 4/5, routes 6/7 against 4/7, unasked offers 0/13 in both).
The arm also kept G5f's idle example, moved to `Agent.IdleExampleStimulus` (entry 13), so the A/B
measures the two together, not the counter-examples alone.

- **Needed:** "Any rumours about the wagon?" should not produce a potion quote card.
- **Observed:** qwen3.5:2b and gemma4:e2b-it-qat both open unasked quotes, in the G5b and G5c
  live checks and the 5 November play-check.
- **Workaround (G5f, measured):** a **negative few-shot example**. `AgenerelaNpcDecisionDriver`
  passes `DecideOptions.IdleExampleStimulus = "Good to see a friendly face again."`, which the
  framework's few-shot block shows answered with `action: none`.
  - **Result:** in an A/B with the same session and interleaved arms, unasked offers on 13 lines
    asking for nothing fell from 6/13 to 0/13 on `gemma4:e2b-it-qat` and from 10/13 to 0/13 on
    `qwen3.5:2b`.
  - **Asked trades:** unchanged at 5/5 and 4/5 (findings, 5 November: "Unasked offers").
  - **The stricter rule line** was also tested. It worked on gemma (0/13) but left
    `qwen3.5:2b` at 1/13, and made it put a target on nine of its `none` answers.
  - **Not tried:** a guard. Telling an asked line from an unasked one means classifying the
    player's words, which DR-008 keeps out of the framework. A game could still add one as its
    own `DelegateGuard`, because that is the game's choice, but it would be a brittle keyword
    match where one example line was enough.
- **Proposal:** two small framework options, neither of which classifies input.
  - Give a code-built `Agent` the idle example a profile already has (entry 13), so every agent
    shows `none` once.
  - Let an `ActionDefinition` carry a counter-example: a line shown answered with `none`
    "although it mentions this action's subject". `FewShotBuilder` would add it beside the
    action's own example.
  - Either should be A/B-measured on the evaluation set before it becomes a default, because a
    `none` example could also make a model miss real commands (Appendix A).
- **Research area:** evaluation, with guards for the DR-008 boundary.

### 10. The grounding guard rejects some correct choices on some models

- **Observed:** `TargetGroundingGuard` rewrote correct trade choices to `none` for
  `nemotron-3-nano:4b` (2 of 6) and Stheno 8B (1 of 6) (findings, 5 November: "Non-Qwen small
  models").
- **Proposal:** look at its confidence threshold per model, or by token-probability quality. A
  research item, not a code change yet.
- **Research area:** guards (#138).

### 11. Memory cannot be saved and restored with the game

**Status: adopted in the game (G5h).**

- **The save:** `SaveSnapshot` is version 2, with `npcMemories`. Each entry is one NPC's
  `RollingHistory.Export()` under its stable id, as plain game data (`SavedNpcMemory`).
  `Agenerela/NpcMemoryArchive.cs` fills it, and `SaveGame` calls it through the game's own
  `INpcMemoryArchive`, so `Game/` reads no framework type.
- **Atomic load:** memories are validated on the detached data, by importing each into a
  throwaway history, before anything changes, and restored last.
- **Old saves:** a version 1 save still loads, with every NPC's memory cleared. An NPC the save
  does not mention is cleared too.
- **What did not fit:** `RollingHistory.Import` reads any strategy's exchanges and ignores
  `Format` and `Version` (by design), so the archive checks them itself. That is entry 22.

- **Needed:** after F10 (load), NPCs should remember the conversations that led to the saved
  state, or explicitly forget them.
- **Workaround:** G4a's save snapshot holds game state only. NPC memory carries over into the
  loaded game, so an NPC can remember a conversation from after the save.
- **Proposal:** `IMemoryStrategy` export and import (a plain-data snapshot), so a game can save it
  beside its own state.
- **Payoff:** consistent NPCs across save and load; also needed for any persistent world.
- **Size and risk:** small. Issue #69 lists memory persistence.
- **Research area:** memory.

### 12. Decisions carry no parameters

- **Needed:** "three potions" or a counter-offer price.
- **Workaround:** bundle targets (`stock_mara_potion_one`), exactly as design §9 planned.
- **Proposal:** none yet. Parameters would change the decision schema, which is measured and
  settled (AGENTS.md). Keep the bundle pattern until there is evidence.

### 13. A code-built agent shows the model no `none` example

**Status: adopted in the game (G5g).** The driver sets `Agent.IdleExampleStimulus` to G5f's line
instead of passing `DecideOptions.IdleExampleStimulus` on every call. Same text, so G5f's
measurement carries over; the G5g A/B ran with it in both arms' places.

- **Needed:** a demonstration that not every line is a request, which G5f found cuts unasked
  actions sharply on two small models (entry 9).
- **Observed:** an agent built from an `AgentProfile` gets the profile's `IdleExampleStimulus`
  ("Nothing has changed since last time."). An agent built in code, as every Living Vale NPC is,
  gets none unless each call passes `DecideOptions.IdleExampleStimulus`. Its few-shot block then
  demonstrates only actions, and the model sees `none` nowhere.
- **Workaround:** `AgenerelaNpcDecisionDriver.IdleExampleStimulus`, sent on every call.
- **Proposal:** an optional idle example on the code-only `Agent` constructor or as a property,
  matching what a profile gives. Or, at least, a line in the docs that a code-built agent has none.
- **Payoff:** every code-built agent, the GreyBoxStrategy countries included, would get the same
  `none` demonstration a profile-built one does.
- **Size and risk:** small, and additive. Changing what a code-built agent sends by default would
  move measured prompts, so it needs an A/B run before it becomes the default (hard rule 2).
- **Research area:** evaluation.

### 14. An identity has no slot for what the agent knows, or for how it must speak

**Status: adopted (10 November).** `NpcPersona.ToIdentity()` sets `AgentIdentity.Knowledge` to the
persona's facts and `StatementRules` to the house rules, which the framework renders once in the
system prompt. The facts no longer go out as observations, so they leave `Agent.MaxObservations`
to the turn's own lines, and no personality carries the rules any more. This changes what the
model reads; the framework's A/B (findings, 9 November) found it no worse.

- **Needed:** eight NPCs, each with a personality, goals, the house rules for the free-text field
  ("one to three short sentences, stay in Briar Vale, never claim to have done what you only talked
  about") and four or five facts of their own (design §7: an NPC knows its own role and local
  facts, nothing more).
- **Workaround:** `Agenerela/NpcPersona.cs` and `RegionNpcPersonas.cs` (G4b) keep personas in code.
  The speaking rules are appended to every `Personality` string, because `AgentIdentity` has only
  `Name`, `Role`, `Personality` and `Goals`. The facts go out as observation lines on every turn,
  prepended by `AgenerelaNpcDecisionDriver.ObservationsFor`, so they share `Agent.MaxObservations`
  with the turn's real observations (quest stage, quote, receipt) and are re-sent as if they were
  news. Adding one NPC touches four tables keyed by its ID: `World/RegionNpcs.cs`, the persona,
  `Interaction/RegionNpcScripts.cs` and the scene builder.
- **Proposal:** two optional `AgentIdentity` fields, carried by `AgentProfile` too: `Knowledge`
  (standing facts, rendered into the system prompt once, apart from observations) and
  `SpeakingRules` (or a profile-wide style block shared by many agents). Alternatively, a stock
  `FixedObservationSource` that returns the same lines every decision, so at least the plumbing is
  the framework's.
- **Payoff:** personas become data in an `AgentProfile` asset a designer can edit, the
  observation budget is left to what changes, and every developer with more than one NPC stops
  concatenating style rules into a personality string.
- **Size and risk:** small. Where the facts sit in the prompt may move accuracy (Appendix A has
  examples of order effects), so the rendering needs an A/B run before it becomes the default
  (hard rule 2).
- **Research area:** observations.

### 15. Where an agent is and what it is doing has no standard shape

- **Needed:** NPCs now walk routines (G4b), so "you are at your workshop" is true at one stop and
  not at the next; later, an NPC should know it is mid-task, or that the traveler found it far
  from its post.
- **Workaround:** none yet: each persona's first fact states where the NPC usually is, and the
  routine never leaves that place, so the line stays true. `World/NpcRoutine.cs` knows the current
  stop but nothing reports it to the model.
- **G8a, the same gap from the HUDs side:** the compass, the map and the name tags need every
  speaking agents position now, and there is no list of a scenes agents to ask. `Unity/NavigationTargets.cs`
  finds every `NpcSpeaker` once at start and reads its Transform each frame, and works out each NPCs
  home place from the games own `RegionNpcs` table. A small `Runtime/Unity/` registry of live agents
  with their Transform (opt-in, a component on the agents GameObject) would serve both the
  observation source above and any marker or tag UI, without the core knowing about scenes.
- **Proposal:** not a core change: `IObservationSource` already covers it, and the framework
  must not assume a scene or a speaking character (DR-008, DR-011). Possibly a small sample source
  in `Runtime/Unity/` that names the registered zone a Transform is in, so a developer has a
  pattern to copy.
- **Payoff:** low to medium: it saves a few lines, but makes "the model was told where it is" a
  habit rather than a bug found in play.
- **Size and risk:** small. A sample, not an API.
- **Research area:** observations.
### 16. No "propose, then the player confirms" step; every game builds its own cards

**Status:** in the framework with #236, as an optional helper rather than the agent primitive proposed below: `Proposals` holds a `DecisionResult` and on confirm carries it out through `Agent.Execute`, whose re-check (per-target availability included) is the confirm-time check; `Confirm` takes the `Proposal` by reference, with `Decline`, `DeclineAll`, `OnePending`, `StillValid` and `Changed`, and `Agent.Reset` ends every pending one. Memory records it once, at hold; telemetry's `Execution` stays null until confirm (`Documentation~/proposals.md`).

**Adopted for route cards (10 November).** Each NPC's agent has a `Proposals` with `OnePending`.
The driver holds a route card's decision instead of carrying it out; the card confirms or declines
that very `Proposal`; confirming runs `Agent.Execute`, then the executor's new `QuestRoutes.TryApply`,
which checks the conversation and the contract and applies it. A conversation's end and a load
decline every card. Deleted: `RouteProposal`, its state, the card replacement in `TryPropose`,
`Confirm`'s and `Decline`'s bookkeeping, `InvalidatePending`, and `ActionOutcome.Proposal`. Not
moved: `TradeQuote` (it has expiry and stock revisions of its own) and Iven's main-quest
`QuestOffer`. Three seams are listed under "What did not fit".

- **Needed:** most of G4c's actions must not take effect when the NPC chooses them. Kest's
  release, a division agreement, handing over the parcel and accepting a side quest all go on a
  card first, and only the player's button carries them out, after every condition is checked
  again.
- **Workaround:** a third copy of the same machinery. `Game/QuestRoutes.cs` has `RouteProposal`,
  next to G3's `TradeQuote` and G4a's `QuestOffer`. Each records the conversation generation it was
  made in, is invalidated by `ConversationTradeLink` when a conversation opens or closes, and
  re-checks its conditions on `Confirm`. Each card in `UI/` rebuilds its buttons for every new
  proposal, so a click started on the old card cannot confirm the new one. `Agent.Execute` reports
  only that a handler ran, so the drivers read `LastOutcome.Proposal` to learn that a card exists.
- **Proposal:** an optional framework primitive for a pending effect. A handler returns a
  proposal (ID, payload, a re-check delegate) instead of acting. `Agent.Confirm(id)` or
  `Agent.Decline(id)` resolves it, re-running the check. The framework invalidates it when the
  agent's context moves on, and memory records "proposed" and "confirmed" as different events.
- **Payoff:** about 200 lines per card type here, and the confirm-time re-check becomes a
  framework guarantee. Memory and telemetry could separate "the NPC offered" from "the player
  accepted", which evaluation needs.
- **Size and risk:** medium, a new public API that builds on #2 (receipts). It touches no
  settled schema rule.
- **Research area:** guards.

### 17. Knowledge an NPC holds cannot be tied to the action that reveals it

**Status:** the game already had this shape, and keeps it. One leftover: Fen's persona knowledge
(entry 14) still states what she saw, so she can still say it under `none`. Taking the lines out
would make her forget it once told, because the card target leaves; that is a content call.

**Framework:** settled in the framework's docs with #236, with no new API: the game's `KnowledgeCard` shape is the rule. A fact that counts only when revealed is a target of the revealing action, in its own category, never a standing `Knowledge` line, and the action's per-target check (#235) removes it once told (`Documentation~/targets.md`, "Facts told through an action", and `RevealedKnowledgeTests`).

- **Needed:** Fen's clue counts only once she tells it (design §3). The game has to know the fact
  was told, not just that her reply mentioned it.
- **Workaround:** the fact is a `KnowledgeCard` target of `share_information` in
  `Game/QuestRoutes.cs`. Executing it records the clue, and once recorded the action disappears.
  Fen's persona could still put the whole fact in her `statement` under `none`, and the game
  would never know. Nothing in the framework links a persona's knowledge lines to an action.
- **Proposal:** knowledge entries with IDs on the agent's identity, each optionally marked as
  "revealed only through action X". The prompt then tells the model to share it through that
  action, and an optional guard flags a `none` reply whose statement reveals it.
- **Payoff:** quest-critical information becomes a first-class, testable event in any game: a
  detective's clue, a password, a secret.
- **Size and risk:** medium. The guard part is research: it needs a way to detect paraphrase.
- **Research area:** observations, and guards for the detection.

### 18. The model cannot tell a player's claim from a game-verified fact

- **Needed:** "I have proof, Fen saw you" must not persuade Kest when Fen never told the
  player anything. The schema already omits the testimony deal, but the model can still *say* it
  is persuaded.
- **Workaround:** `Agenerela/RouteActionBinding.cs` `StatusLines` writes per-NPC lines from game
  state into the observations, for example "No witness account of your raid has reached the
  traveler. Claims of proof are only words." The stimulus label `Player:` is all that marks the
  player's own line.
- **Proposal:** a separate observation channel for game-verified facts (`DecideOptions.Facts`),
  rendered apart from the stimulus and from free observations, with one framework-owned
  instruction that facts outrank anything the player claims. It belongs with the
  prompt-rendering work in #1.
- **Payoff:** every game with persuasion, deception or evidence needs it. Without it, each game
  writes its own trust prose and nobody measures it.
- **Size and risk:** small in code, but it changes the prompt, so it needs an A/B run (hard
  rule 2). The settled field order is untouched.
- **Research area:** observations, with evaluation for the A/B.

### 19. Per-target conditions live outside the framework; an absent action cannot say why

**Status:** in the framework with #235 (DR-020 amended): `ITargetAvailability`, `[AvailableFor]` and `DelegateActionHandler.IsAvailableFor`; the paired schema drops a rejected pair, an action with no legal target is left out, `TargetAvailabilityGuard` and `Execute` hold a decision to it, and the reasons go to telemetry and `Agent.Preview`, never to the prompt (the proposed prompt line was dropped).

**Adopted, with one limit (10 November).** One `ExecutorHandler` serves every action, and its
per-target check allows a target only while the executor offers it. Deleted: `RoutePairGuard` and
`CheckPair`, and the three bindings' handlers with their `IsAvailable` and `Offers` loops. **Did not
fit:** the sources still register only what game state allows now. The prompt's target list is every
registered target whatever the check says, so registering every contract and letting the check
filter would put "Release on Fen's account" in Kest's prompt before Fen has told anything. So the
check answers yes for every registered target, and is kept for masking an action with no target,
the re-check at `Execute` with its reason, and telemetry. The framework would need the listing to
leave out a target the check rejects for every action.

- **Needed:** Elra takes the parcel only once Brann has granted the permit. Kest's favour deal
  needs the herb quest done. Division needs two recorded agreements. Each condition belongs to a
  target, not to an action.
- **Workaround:** each `RouteContract` in `Game/QuestRoutes.cs` carries its own `Available`
  predicate. `NpcActionExecutor.AvailableFor` filters with it every turn, and `Confirm` checks it
  again. The framework's `IActionHandler.IsAvailable` is per action only. When a condition is
  false the target is absent, which is the settled rule. The model is then never told *why*, so
  Elra cannot say "get a permit from Brann" unless the game also writes that into a status line.
- **Proposal:** targets that carry an availability predicate, which the framework checks while
  building the schema and again at `Execute`. Optionally, an "unavailable because …" line per
  absent target, for the prompt only and never for the schema.
- **Payoff:** removes the filtering and re-checking code from every binding, and lets NPCs explain
  refusals consistently.
- **Size and risk:** medium. The prompt line runs against "illegal options are absent, not
  forbidden in prose", so it needs evidence before it is adopted. Builds on #4.
- **Research area:** targets.

### 20. Few-shot examples name a target by its id, from a fixed list, never by its description

**Status: adopted (10 November).** Each action has one fixed `ExampleStimulus` ("I'll take {0},
please.", "Would you buy my {0}?", and the quest and route lines), the driver sends
`ExampleTargetFallback`, which suits the game since every action declares its category, and
`GameTargetOption` implements `INamedTarget` with the words `RefreshExamples` used ("healing potion
×1"). The three `RefreshExamples` are deleted. One change the model sees: the fallback never names a
target the player's line mentions, so "Do you have a healing potion?" now gets the bread example
instead of the potion, which was the answer. **Except on the route actions:** there the skip cost
gemma4 Elra's delivery, 3 of 3, so each route action prefers its own targets
(`QuestRoutes.TargetIds`), which the framework names whether or not the line mentions them. With
that, the same-session check shows no visible regression on either model (findings, 10 November).

- **Needed:** an example per available action that names a target the player could ask for
  now, as they would say it: "I'll take healing potion ×1, please."
- **Observed (G5g):** `FewShotBuilder` picks the first registered id from the action's
  `PreferredExampleTargets` and fills `{0}` with a display name made from the id ("Stock Mara
  Potion One"). Living Vale's targets change every turn (stock runs out, the player's pack
  changes, quest stages move), so a fixed list is either stale or has to be rewritten per turn.
  The framework's `IDescribedTarget` line is used by the list (#193), but not by the examples.
- **Workaround:** `RefreshExamples` in each binding rewrites `ExampleStimulus` and
  `PreferredExampleTargets` before every decision. It is still in the game after G5g.
- **Proposal:** let an example fall back to the first registered target its action can take
  when `PreferredExampleTargets` is empty. Fill `{0}` from an optional short name on
  `IDescribedTarget` (or a sibling `INamedTarget`) when the target has one.
- **Payoff:** deletes the last per-turn prompt plumbing in this game's bindings.
- **Size and risk:** small. It changes example text, so it needs the evaluation A/B (#151).
- **Research area:** evaluation.

### 21. `ScriptedProvider` matches the asking agent by display name, not a stable id

**Status: adopted (10 November).** Each NPC's agent has `Agent.Id = npc.Id` (#217), and the script
reads `ScriptedRequest.AgentId`; the name-to-id map is deleted.

- **Observed (G5g):** `ScriptedProvider` keys replies, and names the asker in `ScriptedRequest.Agent`,
  by `AgentIdentity.Name` ("Mara"). Living Vale keys NPCs by stable id (`npc_mara`), so
  `ScriptedAgentDriver` keeps a name-to-id map, filled as each turn starts. Two NPCs with the
  same display name would be indistinguishable to a script.
- **Proposal:** carry an optional stable agent id in the request (`ScriptedRequest.AgentId`), or
  let `When` take one.
- **Size and risk:** small and additive.
- **Research area:** evaluation (headless captures and replay).

### 22. `RollingHistory.Import` accepts any snapshot format and version

**Status: adopted (10 November).** `NpcMemoryArchive.Validate` asks `RollingHistory.CanImport`
(#205) instead of checking format and version itself and importing into a throwaway history, and an
NPC the save does not mention is cleared with `Clear()` (#219).

- **Observed (G5h):** `Import` reads the exchanges of any strategy's snapshot and ignores
  `MemorySnapshot.Format` and `Version`, by design (DR-018), and throws only on a null exchange.
  A save written by a future strategy, or a newer `RollingHistory` version, would load silently.
- **Workaround:** `NpcMemoryArchive.Validate` refuses any format other than
  `RollingHistory.SnapshotFormat`, and any version above `SnapshotVersion`.
- **Proposal:** a strict option (`Import(snapshot, strict: true)`), or a `CanImport(snapshot, out
  reason)` a game can call first. That would also give the validation a game needs before an
  atomic load, without a throwaway history.
- **Size and risk:** small and additive.
- **Research area:** memory (#119).

### 23. Memory belongs to one agent, so two drivers for one NPC keep two

**Status: adopted (10 November).** The scripted replies are a provider of the same agents as the
model's (F6 is `NpcProviderSwitch`, setting `Agent.Provider`), so each NPC has one agent and one
memory whichever answers, and the save holds all of it. Deleted: the second agent per NPC inside
`ScriptedAgentDriver`, `SwitchableNpcDecisionDriver` with its reply routing, and the scripted
driver's own decide-and-execute path (G6).

- **Observed (G5h):** since G5g the scripted replies go through their own `Agent` per NPC
  (`ScriptedAgentDriver`), with its own `RollingHistory`. An NPC the player talks to with
  scripted replies and then, after F6, with the model has two memories. Only the model driver's
  is saved.
- **Workaround:** none needed for the demo; the scripted path is a development fixture.
- **Proposal:** none for the framework yet. A game can pass the same `IMemoryStrategy` instance to
  both agents, which `Agent.Memory` allows. Living Vale may do that if the scripted path ever
  ships.
- **Research area:** memory.
