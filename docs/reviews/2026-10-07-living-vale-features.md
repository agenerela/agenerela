# Living Vale as a real game — feature suggestions for the game and the framework

**Status.** Suggestions, 7 October 2026. A companion to the
[design review](2026-10-07-design-review.md), written for the case the user set: Living
Vale is meant to be an actual playable game, not a small testing demo. Nothing here is
decided. Where a suggestion changes what the model reads, it is marked **A/B**, because
hard rule 2 applies to it.

The Living Vale plan is on `codex/rpg-demo-plan`:
[`living-vale.md`](https://github.com/agenerela/agenerela/blob/codex/rpg-demo-plan/docs/demos/living-vale.md)
and
[`living-vale-design.md`](https://github.com/agenerela/agenerela/blob/codex/rpg-demo-plan/docs/demos/living-vale-design.md).
This document assumes both, and does not repeat what they already cover (combat, inventory,
levels, authored quests, save/load, accessibility, the chat-driven trade loop).

## What changes when it is a game rather than a demo

A demo has to show that a decision is correct. A game has to be *trusted* by someone who
did not read the build plan, for twenty minutes, while the model is slow and sometimes
wrong. That moves the hard problems:

| In a demo | In a game |
|---|---|
| Latency is a number on an overlay | Latency is a dead-eyed NPC for two seconds, every turn |
| A guard's refusal is a safety win | A refusal is "the game ignored me", unless the NPC explains it in character |
| The player knows what to type | The player does not know typing is a mechanic, or what the NPC can do |
| A wrong statement is a data point | A wrong statement ("as an AI model...", a modern idiom, a price that is not the price) breaks the fiction |
| Ollama is installed | Nothing is installed; the model ships with the game or downloads on first run |
| One playtester, one session | Saves, reloads, a second playthrough, an NPC who should remember you |
| The Decision Log is the record | Playtests are the record, and the best source of new evaluation prompts |

Every suggestion below answers one of those rows.

---

## Part A — Game features

Each entry: what it is, why a playable game needs it, what it needs from the framework,
when (v1 is the first complete adventure, v1.5 the first polish pass, v2 after), and a
rough size (S under a day, M a few days, L a week or more).

### A1. Conversation feel

**A1.1 Streamed replies, action first.** Show the NPC's line as it arrives, word by word,
and start the NPC's reaction (turning, a gesture, a nod toward the stall) the moment the
action and target are known, before the statement finishes. The schema's field order makes
this possible: `action` and `target` are complete before the first statement token, so the
game can validate and commit the action while the sentence is still being written. The
player then waits for a sentence to finish, which feels like a person talking, rather than
for a reply to begin, which feels like a freeze. *Needs:* framework B1. *When:* v1. *Size:*
M on the game side once B1 exists.

**A1.2 A thinking state that is acting, not loading.** Between the player's line and the
first token: the NPC turns to face the player, holds eye contact, an "..." sits over the
head, and a subtle idle (scratching a chin, setting down a tool) plays. One to three
seconds of this reads as a person considering an answer. A spinner reads as a bug. Budget
the animation to the measured latency and never show a progress bar. *Needs:* nothing.
*When:* v1. *Size:* S.

**A1.3 Suggested lines, as hints, not a menu.** Three short chips under the input ("What do
you sell?", "Where is the healer?", "Tell me about the wagon"), one per thing this NPC can
actually do right now, computed from the NPC's available actions. They teach the player
that typing anything works, they rescue a player with a controller, and they give the model
easy wins early. They must not become a dialogue tree: the chips change with state, and
typing is always primary. The chip wording must never be reused as a few-shot example or an
evaluation prompt (hard rule 4). *Needs:* B5 (available actions readable before a
decision). *When:* v1. *Size:* S.

**A1.4 Interrupt and resubmit.** The player sends a second line while the first is pending
(very common: a typo, or "wait, no"). The pending request is cancelled and the new one
replaces it; the NPC's "..." continues without a glitch. Never queue two turns per
conversation and never execute the first one after the second arrived. *Needs:* B4.
*When:* v1. *Size:* S once B4 exists.

**A1.5 Leaving mid-sentence.** Walking out of range or pressing Escape while a reply is
pending cancels it, and the NPC says a scripted farewell ("Off you go, then.") so the exit
is acknowledged. The model's late reply is discarded with status `Cancelled`, and nothing
executes. *Needs:* framework cancellation status (review F2). *When:* v1. *Size:* S.

**A1.6 The player has a name, and NPCs use it.** Chosen at the start, passed as an
observation ("You are speaking with Ada, a traveler you met this morning"). Cheap, and the
first thing that makes a model-driven NPC feel like it noticed you. *Needs:* nothing.
*When:* v1. *Size:* S.

### A2. Trust and legibility

**A2.1 In-character refusals with a reason.** When a guard rewrites a decision to `none`,
or a handler refuses (stale quote, not enough gold), the NPC's line must say so in
character and in a way that points at the real cause: "I don't know any Godzilla, friend."
"That offer's gone; ask me again." "You haven't the coin." A table of reason-to-line
templates per NPC role, with the guard reason and the handler reason as keys. Optionally,
for the rare case, a second short model request that writes the refusal given the reason
(the build plan already allows this). *Needs:* review F3 (execution result with reason), a
guard reason on the result (planned in Phase 3). *When:* v1. *Size:* M.

**A2.2 A journal that records what happened, not what was said.** Every executed action,
every committed trade, every quest state change, with the NPC and time. It is the receipt
ledger shown to the player. If an NPC's prose promised something the ledger does not show,
the player can see the difference, and the game is honest about it. Pair it with the
per-NPC transcript (A2.3). *Needs:* review F3. *When:* v1. *Size:* M.

**A2.3 A readable transcript per NPC, and an "NPC remembers" indicator.** Open the journal
on an NPC and see the last exchanges, which is also exactly what the model remembers. When
memory is cleared (a reload without export, v1) the transcript says "Mara does not recall
your last conversation", so the player is never surprised by a forgotten promise. *Needs:*
memory export (review F6) to avoid the clearing. *When:* v1 with clearing, v1.5 with
persistence. *Size:* S.

**A2.4 Visible services on approach.** Above or beside the NPC name: small icons for
trades, knows the area, gives work, grants access. They are derived from the NPC's
registered actions, so they cannot drift from what the NPC can do. This is pillar four
("improvisation has visible boundaries") made literal. *Needs:* B5. *When:* v1. *Size:* S.

**A2.5 Claims are not facts.** The player will lie ("the steward sent me", "I already paid").
The design already says a claim does not unlock anything. Go one step further and make
claims a first-class game object: an NPC can repeat a claim *as a claim* ("A traveler told
me the steward sent them") to another NPC through the witness system, and a claim can be
confirmed or contradicted by a recorded event, with a reputation effect either way. This
turns the prompt-injection problem into a mechanic, and it costs no model request: the
ledger is deterministic; only the phrasing is the model's. *Needs:* nothing in the
framework. *When:* v1.5. *Size:* M.

**A2.6 Prices and terms always come from the card.** The design has this rule. The UI
should enforce it visually: when a statement mentions a number that differs from the card,
dim the sentence's number and keep the card's, so a playtester never acts on the prose.
Log every such contradiction with the decision id (it is the most useful statement-quality
metric the game can collect). *Needs:* B7. *When:* v1. *Size:* S.

### A3. A world that reacts

**A3.1 Ambient barks at background priority.** When the player approaches with a visible
state (bloodied, carrying a wolf pelt, wearing the badge), the NPC may say one line without
being spoken to: a background-priority decision with only `none` available, whose statement
is the bark. One per approach, cooldown per NPC, cancelled if the player starts a
conversation. It is cheap, it uses DR-015's priority tier for the first time, and it makes
the valley feel like it sees you. *Needs:* `DecideOptions.Priority` (planned), B4.
*When:* v1.5. *Size:* S.

**A3.2 Gossip with provenance.** The design's witness system propagates facts. Add
provenance to each propagated fact ("Fen saw you at the wreck", "Brann told me you returned
his badge") and let NPCs cite it. The model gets the fact as an observation line that
already includes the source, so it cannot invent one. *Needs:* nothing. *When:* v1.5.
*Size:* M.

**A3.3 A notice board as the generated-quest surface.** Generated contracts (G6a) appear as
notices with their exact objective and reward, which the player reads before any NPC
improvises a premise around them. It keeps "what the quest is" authoritative and visible,
and gives the model a place to add colour without owning the numbers. *Needs:* nothing.
*When:* v2 with G6a. *Size:* M.

**A3.4 Epilogue from the ledger.** At the end of the first adventure, a summary screen
built deterministically from the journal (what you did, who you helped, what changed),
with an optional model-written paragraph that is labelled as generated and never
contradicts the list above it. *Needs:* nothing. *When:* v1.5. *Size:* S.

### A4. Playing without a model, and playing with a bad one

**A4.1 A scripted fallback that is a worse game, not a broken one.** Every NPC has a small
keyword-matched set of lines and the same action handlers, used when the provider is
unavailable, when the player turns "AI conversation" off in settings, and when a decision
fails three times in a row. It is clearly labelled in the UI ("Scripted"). It is also the
control arm for any claim that the model makes the game better: run playtesters through
both. *Needs:* a `ScriptedProvider` in the runtime (review F18), so the fallback uses the
same pipeline. *When:* v1. *Size:* M.

**A4.2 Provider status in the pause menu and the HUD.** Model name, where it runs, loaded or
not, last latency, VRAM headroom. A player who sees "model loading, 12 s" waits; one who
sees a silent NPC quits. *Needs:* B8. *When:* v1. *Size:* S.

**A4.3 Model choice in settings.** 2B for low-VRAM machines, 4B where it fits, chosen at
first launch from the detected VRAM and changeable later, with the accuracy and latency
trade-off stated in plain words. *Needs:* B8, Phase 6b. *When:* v1.5 on Ollama, v2 in
process. *Size:* S.

**A4.4 Retry and recovery UI.** On a provider failure: a one-line in-character stall
("Give me a moment.") and a retry; after the limit, the scripted fallback for that turn,
labelled. The world never mutates on a failed turn. *Needs:* review F2 statuses. *When:*
v1. *Size:* S.

### A5. Shipping

**A5.1 First-run model install.** A built player cannot assume Ollama. Until Phase 6b
lands, the Windows build ships with an installer step that checks for Ollama, offers to
install it, pulls the chosen model, and shows the model's licence and download size before
doing so. After Phase 6b, the same screen downloads a `.gguf` into the game's data folder,
verifies its hash, and records the licence shown. Offline mode must still start the game
in scripted fallback. *Needs:* B8, B9. *When:* v1 (Ollama), v2 (in process). *Size:* M.

**A5.2 A performance budget with inference resident.** 1080p60 target with the model loaded,
measured, with a 30 FPS preset that drops shadows and foliage before it drops anything the
conversation needs. Frame-time percentiles and VRAM recorded in the wiki per preset.
*Needs:* nothing. *When:* v1.5. *Size:* M.

**A5.3 Controller support is a chat problem, not an input problem.** The design defers
controllers. The reason to bring them forward is A1.3: with chips and an on-screen keyboard
as a last resort, a controller player can play most of the game. Decide early whether chips
are enough; if they are, the game is far more presentable on a couch at review.
*Needs:* A1.3. *When:* v1.5. *Size:* M.

### A6. Playtesting as the evaluation instrument

**A6.1 Opt-in decision recording in the player.** With consent on the first run, every
decision (request, reply, result, execution outcome, the player's line) is written to a
local JSONL file the tester can send back. It is the bug report for "the guard just stood
there", and it is the raw material for the next hundred evaluation prompts, labelled by what
the tester actually wanted. *Needs:* B6. *When:* v1. *Size:* S once B6 exists.

**A6.2 A tester console.** F2 opens a console with: set quest state, give gold, teleport,
force the next reply (switch the agent to `ScriptedProvider` for one turn), replay a
recorded decision, and dump the current prompt. Half of this is the framework's Decision
Log window made available in a build. *Needs:* B6, review F18. *When:* v1. *Size:* M.

**A6.3 Scenario tests that play the game.** Multi-turn scripts (state setup, a sequence of
player lines, the expected action after each, state mutations between turns) that run in
EditMode against `ScriptedProvider` for invariants, and in the Phase 4 harness against a
model for accuracy. The design's §14 table is the first scenario list. *Needs:* B10.
*When:* v1. *Size:* M.

---

## Part B — Framework features the game needs

Each entry: what, why, which demos benefit beyond Living Vale, the phase it belongs to,
and whether it is an A/B. Items already in the review's backlog are referenced by their
finding number rather than repeated.

### B1. Streaming with early commit — Phase 2b, **A/B** on the UI only

`ILLMProvider` gains a streaming method (`IAsyncEnumerable<string>` or an `IProgress<string>`
on `RequestAsync`), and `Agent.DecideAsync` gains an `OnStatementToken` callback. Because
fields are ordered, the agent parses the prefix as it arrives: once `action` and `target`
are complete, the legality guard and the grounding guard can run on them (the grounding
guard needs probabilities only for the target's tokens, which have arrived), and the game is
told the decision before the statement is finished. The statement then streams to the UI.
If a guard rewrites to `none` the stream is stopped and the game shows its refusal line
(A2.1). The accuracy rules are untouched; only when the game learns the answer changes.
*Benefits:* every talking NPC in every demo. *Risk:* the "early commit" path must be
tested against a reply that is well formed up to the target and malformed afterward; the
decision should still stand, with the statement empty and a telemetry note.

### B2. Game-authored memory notes — Phase 2c

`IMemoryStrategy.Note(string fact)` alongside turns: "The player bought a healing potion for
10 gold" written by the handler after a committed trade, in the same window as the
exchanges, oldest first. This is how an NPC remembers what *happened* rather than what was
said, which is memory rule 1 applied to the game's own events. With review F6's export and
import, notes persist with the save. *Benefits:* the strategy demo (turn reports as notes),
the race (what happened at the last fork).

### B3. A relevance-selected observation source — Phase 2, **A/B**

A real NPC knows dozens of facts; the observation cap and the measured prompt size allow a
handful per request. `RetrievalObservationSource` holds an NPC's fact lines and selects the
top k for this stimulus by a deterministic lexical score (token overlap, no model, no
dependency), so the same stimulus and state always produce the same lines and an eval run
is reproducible. The design's "role-specific facts" and "knowledge cards" are what it
holds. Whether retrieval beats a hand-picked fixed set is a Phase 4 A/B with a control arm.
*Benefits:* the strategy demo most of all, where a country's state is large.

### B4. A decision session: one pending request per conversation, replace-on-resubmit, cancel on leave — Phase 2b

A small `DecisionSession` type (in `Runtime/Core/`, needing no scene) that owns one agent's
pending request: `Submit(stimulus, options)` cancels the previous request if still pending,
carries a generation number, and raises `Decided` only for the latest generation. `Cancel()`
is what leaving range calls. Living Vale's "interaction coordinator", the race's gate logic
and the village's input box all want exactly this; shipping it once stops three copies.
Pairs with review F4 (deadlines) and F2 (statuses).

### B5. Read the offered set before deciding — Phase 1 (#17)

`Agent.Preview()` returning the actions and targets that *would* be offered right now
(`ActionAvailability.For` plus the collected targets), without a model request. The game
needs it for suggestion chips (A1.3), service icons (A2.4), and the "what the model is
offered" panel on the Agent Behaviour inspector that screen 2 already draws. It is a
read-only view of what `DecideAsync` computes anyway.

### B6. A decision sink — Phase 2

`IDecisionSink.Record(DecisionRequest, DecisionResult, ExecutionResult)` on the agent, with
a shipped `JsonlFileSink`. The Decision Log window subscribes to the same interface. It is
how a built player collects playtest data (A6.1), and the recorded file is the import format
for new evaluation prompts, so playtests feed Phase 4 without retyping.

### B7. A statement guard stage — Phase 3

The guard pipeline inspects `action` and `target`. A game that shows model prose to players
also needs a stage over `statement`: maximum length, strip code fences and JSON that leaked,
reject meta-talk patterns ("as an AI", "as a language model"), a developer-supplied banned
list, and optionally a check that any number in the statement matches a number the game
passed in observations (A2.6). A failing statement is replaced by the game's line, never
shown, and the decision stands: the action was right, the sentence was not. Telemetry
records which rule fired. Small models produce all of these failures; the game cannot ship
without something here.

### B8. Provider readiness and resources — Phase 2a

`ILLMProvider` gains `WarmUpAsync` and `IsReady`, and the Ollama config gains `keep_alive`,
so a game loads the model behind its loading screen and keeps it resident. A `Resources`
report (model size, VRAM used, VRAM free where the backend reports it, from Ollama's
`/api/ps`) feeds the settings menu (A4.2, A4.3) and the build plan's unscheduled VRAM
monitoring item.

### B9. Prefix stability for prompt caching — Phase 2, **A/B**

Ollama and llama.cpp reuse the computed prefix of a prompt when it is byte-identical to the
previous request. The request layout (system, few-shot, observations, history, stimulus) is
already in the right order for that, but the few-shot block is built from *available*
actions, so it changes whenever availability does, and the cache misses. Measure two arms
on the Phase 4 set: few-shot from available actions (as specified) versus from all
registered actions with the schema still masked. If accuracy is within noise, the stable
block wins on latency for every NPC with changing state, which is all of them. If it is
not, the measured rule stays. Either answer is worth having before Phase 6b, where every
millisecond is the player's.

### B10. Multi-turn scenarios in the evaluation format — Phase 4 prep (#66)

The review's F20 asks for `targets`, `history` and `acceptable` per case. A game needs one
more level: a *scenario* is an ordered list of cases sharing state, where each case may
mutate state after its expected action executes. The harness runs it as one session with
memory on. Living Vale's §14 scenario families are the first scenarios; the village guard's
"Follow me" then "Wait here" is the smallest.

### B11. Decide, then verbalise — an experiment, Phase 4 or later, **A/B**

The scaffolding makes a 2B model decide well; nothing makes it *write* well. A two-stage
option: the constrained decision request as today, with `statement` allowed to be empty,
then a second, unconstrained, streamed request to the same or a larger model that writes the
line given the chosen action, the target and the persona. It costs a request, so it is off
by default and only for player-facing conversation. It is the one place a bigger model's
prose could be bought without touching the decision's accuracy or its guards. Measure prose
quality (a human rating) and latency against single-stage before adopting it anywhere.

### B12. Composite and filtered target sources — Phase 2

`CompositeTargetSource(params ITargetSource[])` and `FilteredTargetSource(source, predicate)`
in `Runtime/Actions/`, so the per-NPC catalog windows the design describes (§11) are
configuration rather than code, and the race's "open routes of the fork ahead" is a filter
over an explicit set. Small, and it removes a reason to write `ITargetSource` by hand.

### B13. Per-profile voice examples — Phase 5, **A/B**

Two or three sample lines on `AgentProfile` that show *how* the agent talks, separate from
the stimulus-and-answer few-shot examples that teach the format. Small models drift into
modern idiom; a voice sample is the cheapest known fix. It changes the prompt, so it needs
the control arm, and it must obey hard rule 4.

### B14. A prompt budget with a loud warning — Phase 2

A per-profile token budget (default from the measured 143-token arm, scaled), checked
against the provider's reported prompt tokens, with a warning in the console and a telemetry
flag when a request exceeds it. The cap on observations (§2.8) and on history (§2.9) are
pieces of this; one budget makes "why is this NPC slow" answerable.

---

## Part C — A revised first slice for a playable game

The design's G1 to G4 build a whole RPG before a model is connected. For a game whose
selling point is its conversations, the slice that matters first is the conversation loop
in front of a model, with just enough game around it to make the loop mean something:

| Slice | Contents | Framework needed |
|---|---|---|
| **S1. One street** | Briar Glen's market block, movement, camera, focus, chat overlay, thinking state (A1.2), player name (A1.6) | `Agent` with `ScriptedProvider` (review F18) |
| **S2. One trader** | Mara: four stock entries, one quote, inline confirmation, receipt, journal (A2.2), refusal lines (A2.1), suggestion chips (A1.3) | B5, review F3 |
| **S3. One model** | Replace `ScriptedProvider` with `OllamaProvider`; streaming (A1.1); interrupt (A1.4); leave (A1.5); provider status (A4.2); recording (A6.1) | Phase 2a, B1, B4, B6, B8 |
| **S4. One quest, two ways** | Iven's offer, Fen's clue, Kest's negotiation or fight, the three dispositions; memory notes (B2); scenario tests (A6.3) | Phase 2c, B2, B10 |
| **S5. The rest of the RPG** | Inventory UI, levels and perks, side quests, routines, save/load with memory export, reputation and gossip (A3.2), barks (A3.1) | review F6, Phase 3 guards, B7 |
| **S6. Ship** | Installer (A5.1), settings (A4.3), performance presets (A5.2), scripted fallback (A4.1), tester console (A6.2), assets from the pilot | Phase 6b for the in-process build |

S1 to S3 are the demo. S4 onward is the game. The order keeps every week of game work
exercising the framework, and it means the first thing a playtester sees is the thing the
project is about.

---

## Part D — Experiments this game makes possible

Each is an A/B with a control arm on the Phase 4 instrument, recorded in `findings.md`.

1. **Few-shot from available versus all registered actions** (B9): accuracy against cache
   hit rate and latency.
2. **Retrieved versus fixed observations** (B3): accuracy and prompt size on the NPC with the
   most facts.
3. **Voice examples on versus off** (B13): human-rated prose quality, and whether accuracy
   moves.
4. **Decide-then-verbalise versus single stage** (B11): prose quality, latency, and whether
   the second request ever contradicts the first.
5. **Model-driven versus scripted conversation** (A4.1): playtester ratings of the same
   adventure, which is the only experiment that measures whether the framework makes the
   game better rather than the model more accurate.
6. **Per-action target enums** (review F8): accuracy on the trade and quest actions, where
   wrong pairings are most likely.
7. **History window size** (build plan §2.9): accuracy on the multi-turn scenarios of B10.

---

## Cross-reference: game feature → framework dependency

| Game feature | Framework item | Phase |
|---|---|---|
| A1.1 streamed replies | B1 | 2b |
| A1.3 suggestion chips, A2.4 service icons | B5 | 1 (#17) |
| A1.4 interrupt, A1.5 leave, A3.1 barks | B4, review F2, F4 | 2b |
| A2.1 refusal lines | review F3, Phase 3 guard reasons | 1, 3 |
| A2.2 journal | review F3 | 1 |
| A2.3 transcript persistence | review F6 | 2c |
| A2.6 price contradictions | B7 | 3 |
| A4.1 scripted fallback, A6.2 tester console | review F18 | 1 |
| A4.2, A4.3, A5.1 provider status, model choice, installer | B8, Phase 6b | 2a, 6b |
| A6.1 recording | B6 | 2 |
| A6.3 scenario tests | B10 | 4 prep |
| NPC facts at scale | B3, B14 | 2 |
| Memory of events | B2 | 2c |
