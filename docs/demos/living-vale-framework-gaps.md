# Living Vale — gaps in the framework

**Status:** living list, started 5 November 2026. Every Living Vale session adds to it the
day it works around something. It records what building a real game on Agenerela showed
the framework is missing: what the game needed, how it worked around it, and what a framework
feature would change. **None of this is decided.** Each entry becomes an issue, or a decision
record, only when the lead takes it up. Measured claims link to
[findings](../llm-wiki/findings.md).

The game uses the framework's public API only (`Demos/README.md`), so every workaround
below lives in `Demos/LivingVale/Assets/LivingVale/`. That is what makes this list
trustworthy: each entry names code a framework feature would delete.

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
| 9 | The model makes offers nobody asked for | Medium (game feel) | Research |
| 10 | Grounding guard rejects some correct choices on some models | Medium | Research |
| 11 | Memory cannot be saved and restored with the game | Medium | Small |
| 12 | Decisions carry no parameters (quantity, price) | Low for now | Large |
| 13 | An identity has no slot for what the agent knows, or for how it must speak | Medium | Small |
| 14 | Where an agent is and what it is doing has no standard shape | Low to medium | Small |

## Entries

### 1. Actions and targets are invisible to a local model unless the developer writes them into the prompt

- **Needed:** for Mara to choose `quote_purchase` on `stock_mara_bread_one`, the model has to
  know the action exists, what it does, which targets it takes, and what each target is (item,
  price). Ollama only uses the schema to constrain sampling; a local model never reads it
  (AGENTS.md, findings of 10 October).
- **Workaround:** `Agenerela/TradeActionBinding.cs` and `QuestActionBinding.cs` write every
  available action, its description and each target with its price into observation lines on
  every turn (`PromptLines`). `RefreshExamples` rewrites each action's few-shot example every
  turn so it points at a target that exists now.
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

- **Needed:** a handler must know which conversation (generation) a decision belongs to, so a
  quote is tied to that conversation alone.
- **Workaround:** the driver writes the generation into `Agent.State["livingvale.generation"]`
  just before `Execute`. That is shared mutable state on the agent.
- **Proposal:** `Execute(result, context)`, or a caller-supplied correlation object on
  `DecideOptions` that flows through to the handler's context.
- **Payoff:** removes a fragile global-ish channel, and makes concurrent agents safe.
- **Size and risk:** small. Related to #17's execution-time context.

### 6. Model lifecycle is the developer's problem

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

- **Needed:** "Any rumours about the wagon?" should not produce a potion quote card.
- **Observed:** qwen3.5:2b and gemma4:e2b-it-qat both open unasked quotes, in the G5b and G5c
  live checks and the 5 November play-check.
- **Workaround:** being worked on in G5f. This entry is updated with whatever is measured to
  work.
- **Proposal:** to be decided after G5f. Possibly an action-level "only when asked" description
  pattern, or a guard. Note DR-008: the framework must not classify the player's input.
- **Research area:** guards, and evaluation.

### 10. The grounding guard rejects some correct choices on some models

- **Observed:** `TargetGroundingGuard` rewrote correct trade choices to `none` for
  `nemotron-3-nano:4b` (2 of 6) and Stheno 8B (1 of 6) (findings, 5 November: "Non-Qwen small
  models").
- **Proposal:** look at its confidence threshold per model, or by token-probability quality. A
  research item, not a code change yet.
- **Research area:** guards (#138).

### 11. Memory cannot be saved and restored with the game

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

### 13. An identity has no slot for what the agent knows, or for how it must speak

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

### 14. Where an agent is and what it is doing has no standard shape

- **Needed:** NPCs now walk routines (G4b), so "you are at your workshop" is true at one stop and
  not at the next; later, an NPC should know it is mid-task, or that the traveler found it far
  from its post.
- **Workaround:** none yet: each persona's first fact states where the NPC usually is, and the
  routine never leaves that place, so the line stays true. `World/NpcRoutine.cs` knows the current
  stop but nothing reports it to the model.
- **Proposal:** not a core change: `IObservationSource` already covers it, and the framework
  must not assume a scene or a speaking character (DR-008, DR-011). Possibly a small sample source
  in `Runtime/Unity/` that names the registered zone a Transform is in, so a developer has a
  pattern to copy.
- **Payoff:** low to medium: it saves a few lines, but makes "the model was told where it is" a
  habit rather than a bug found in play.
- **Size and risk:** small. A sample, not an API.
- **Research area:** observations.
