# Living Vale — suggested features for a playable game

**Status:** suggestions, 7 October 2026. None is decided, none is scheduled, and none
changes a confirmed direction in the [build plan](living-vale.md) or the
[game design](living-vale-design.md). They come from reading those two documents against
the framework as it stands, asking what a player who never read either will need. Where a
feature changes what the model reads, it is marked **A/B**: it needs a control arm before
it ships (hard rule 2). The framework-side reasoning behind the dependencies is in the
[design review](../reviews/2026-10-07-design-review.md)
and its
[feature companion](../reviews/2026-10-07-living-vale-features.md).

## What changes when it is a game rather than a demo

A demo shows that a decision is correct. A game has to be *trusted* by someone who did not
read the build plan, for twenty minutes, while the model is slow and sometimes wrong.

| In a demo | In a game |
|---|---|
| Latency is a number on an overlay | Latency is a dead-eyed NPC for two seconds, every turn |
| A guard's refusal is a safety win | A refusal is "the game ignored me", unless the NPC explains it in character |
| The player knows what to type | The player does not know typing is a mechanic, or what this NPC can do |
| A wrong statement is a data point | "As an AI model...", a modern idiom, or a price that is not the price breaks the fiction |
| Ollama is installed | Nothing is installed; the model ships with the game or downloads on first run |
| One tester, one session | Saves, reloads, a second playthrough, an NPC who should remember you |
| The Decision Log is the record | Playtests are the record, and the best source of new evaluation prompts |

Each suggestion below answers one of those rows. Format: what, why, what it needs from the
framework, when (**v1** the first complete adventure, **v1.5** the first polish pass,
**v2** after), size (**S** under a day, **M** a few days, **L** a week or more).

## 1. Conversation feel

**1.1 Streamed replies, action first.** Show the NPC's line word by word as it arrives,
and start the NPC's reaction (turning, a nod toward the stall) the moment the action and
target are known, before the statement finishes. The schema's field order makes this
possible: `action` and `target` are complete before the first statement token, so the game
can validate and commit the action while the sentence is still being written. The player
waits for a sentence to finish, which feels like a person talking, rather than for a reply
to begin, which feels like a freeze. *Needs:* a streaming provider call with early commit
(framework, Phase 2). *When:* v1. *Size:* M on the game side.

**1.2 A thinking state that is acting, not loading.** Between the player's line and the
first token: the NPC turns to face the player, an "..." sits over the head, and a small idle
plays (setting down a tool, scratching a chin). One to three seconds of this reads as a
person considering; a spinner reads as a bug. Never show a progress bar. *Needs:* nothing.
*When:* v1. *Size:* S.

**1.3 Suggested lines, as hints, not a menu.** Three short chips under the input ("What do
you sell?", "Where is the healer?", "Tell me about the wagon"), one per thing this NPC can
do right now, computed from the NPC's currently available actions. They teach that typing
anything works, they rescue a controller player, and they give the model easy wins early.
They must not become a dialogue tree: chips change with state and typing stays primary.
Chip wording is never reused as a few-shot example or an evaluation prompt (hard rule 4).
*Needs:* a way to read the offered actions without making a request (framework, #17).
*When:* v1. *Size:* S.

**1.4 Interrupt and resubmit.** The player sends a second line while the first is pending
(a typo, "wait, no"). The pending request is cancelled, the new one replaces it, and the
"..." continues without a glitch. Never queue two turns per conversation; never execute the
first after the second arrived. *Needs:* one-pending-request-per-conversation in the
framework, or the game's interaction coordinator (design §12) doing it. *When:* v1.
*Size:* S.

**1.5 Leaving mid-sentence.** Walking out of range or pressing Escape while a reply is
pending cancels it, and the NPC says a scripted farewell so the exit is acknowledged. The
late reply is discarded and nothing executes. *Needs:* a `Cancelled` status on the
decision rather than an exception. *When:* v1. *Size:* S.

**1.6 The player has a name, and NPCs use it.** Chosen at the start and passed as an
observation. The first thing that makes a model-driven NPC feel like it noticed you.
*Needs:* nothing. *When:* v1. *Size:* S.

## 2. Trust and legibility

**2.1 In-character refusals with the real reason.** When a guard rewrites a decision to
`none`, or a handler refuses (stale quote, not enough gold), the NPC's line says so in
character and points at the cause: "I don't know any Godzilla, friend." "That offer's gone;
ask me again." "You haven't the coin." A table of reason-to-line templates per NPC role,
keyed on the guard reason and the handler reason. For rare cases, an optional second short
model request writes the refusal from the reason, as the build plan already allows.
*Needs:* an execution result with a reason, and guard reasons on the result (framework
#17, Phase 3). *When:* v1. *Size:* M.

**2.2 A journal that records what happened, not what was said.** Every executed action,
committed trade and quest change, with the NPC and time: the receipt ledger shown to the
player. If an NPC's prose promised something the ledger does not show, the player can see
it, and the game is honest about it. *Needs:* an execution result from `Execute`. *When:*
v1. *Size:* M.

**2.3 A transcript per NPC, and a "does not recall" marker.** Open the journal on an NPC
and read the last exchanges, which is exactly what the model remembers. When memory is
cleared (a reload without export, v1) the transcript says so, so a forgotten promise never
surprises. *Needs:* memory export and import to avoid the clearing. *When:* v1 with
clearing, v1.5 with persistence. *Size:* S.

**2.4 Visible services on approach.** Beside the NPC name, small icons: trades, knows the
area, gives work, grants access. Derived from the NPC's registered actions, so they cannot
drift from what it can do. Pillar four ("improvisation has visible boundaries") made
literal. *Needs:* the same offered-set read as 1.3. *When:* v1. *Size:* S.

**2.5 Claims are not facts, and claims are a mechanic.** The player will lie ("the steward
sent me", "I already paid"). The design already says a claim unlocks nothing. Go one step
further and make claims a game object: an NPC can repeat a claim *as a claim* ("A traveler
told me the steward sent them") through the witness system, and a recorded event can
confirm or contradict it, with a reputation effect either way. This turns the
prompt-injection problem into gameplay at no model cost: the ledger is deterministic, only
the phrasing is the model's. *Needs:* nothing. *When:* v1.5. *Size:* M.

**2.6 Prices and terms always come from the card, visibly.** The design has the rule; the
UI should enforce it. When a statement mentions a number that differs from the card, dim
the sentence's number and keep the card's, so a tester never acts on prose. Log every such
contradiction with the decision id: it is the best statement-quality metric the game can
collect. *Needs:* a statement guard stage (framework, Phase 3) or a game-side check.
*When:* v1. *Size:* S.

## 3. A world that reacts

**3.1 Ambient barks at background priority.** When the player approaches with a visible
state (bloodied, carrying a wolf pelt, wearing the badge), the NPC may say one line unasked:
a background-priority decision with only `none` available, whose statement is the bark. One
per approach, a cooldown per NPC, cancelled if a conversation starts. Cheap, the first real
use of DR-015's priority tier, and it makes the valley feel like it sees you. *Needs:*
`DecideOptions.Priority` (planned). *When:* v1.5. *Size:* S.

**3.2 Gossip with provenance.** The witness system propagates facts; add the source to each
("Fen saw you at the wreck", "Brann told me you returned his badge") and let NPCs cite it.
The model receives the fact as an observation line that already contains the source, so it
cannot invent one. *Needs:* nothing. *When:* v1.5. *Size:* M.

**3.3 A notice board as the generated-quest surface.** Generated contracts (G6a) appear as
notices with their exact objective and reward, read before any NPC improvises a premise
around them. What the quest *is* stays authoritative and visible; the model adds colour
without owning numbers. *Needs:* nothing. *When:* v2 with G6a. *Size:* M.

**3.4 An epilogue from the ledger.** At the end of the first adventure, a summary screen
built from the journal (what you did, who you helped, what changed), with an optional
model-written paragraph labelled as generated that never contradicts the list above it.
*Needs:* nothing. *When:* v1.5. *Size:* S.

## 4. Playing without a model, and playing with a bad one

**4.1 A scripted fallback that is a worse game, not a broken one.** Every NPC has a small
keyword-matched set of lines over the same action handlers, used when the provider is
unavailable, when the player turns "AI conversation" off in settings, and after three
failed decisions in a row. It is labelled "Scripted" in the UI. It is also the control arm
for any claim that the model makes the game better: run testers through both. *Needs:* a
`ScriptedProvider` in the framework's runtime, so the fallback runs through the same
pipeline and the label is read from the provider's name. *When:* v1. *Size:* M.

**4.2 Provider status in the pause menu and the HUD.** Model name, where it runs, loaded or
not, last latency, VRAM headroom. A player who sees "model loading, 12 s" waits; one who
sees a silent NPC quits. *Needs:* provider readiness and resource reporting (framework,
Phase 2). *When:* v1. *Size:* S.

**4.3 Model choice in settings.** 2B for low-VRAM machines, 4B where it fits, picked at
first launch from detected VRAM and changeable later, with the accuracy and latency
trade-off in plain words. *Needs:* 4.2, and Phase 6b for the in-process build. *When:*
v1.5 on Ollama, v2 in process. *Size:* S.

**4.4 Retry and recovery.** On a provider failure: a one-line in-character stall ("Give me a
moment.") and a retry; past the limit, the scripted fallback for that turn, labelled. The
world never mutates on a failed turn. *Needs:* decision statuses for provider failure and
timeout. *When:* v1. *Size:* S.

## 5. Shipping

**5.1 First-run model install.** A built player cannot assume Ollama. Until Phase 6b, the
Windows build ships with a first-run step that checks for Ollama, offers to install it,
pulls the chosen model, and shows the model's licence and download size before doing so.
After Phase 6b the same screen downloads a `.gguf` into the game's data folder, verifies
its hash and records the licence shown. Offline, the game still starts in scripted
fallback. *Needs:* 4.2; Phase 6b later. *When:* v1 (Ollama), v2 (in process). *Size:* M.

**5.2 A performance budget with inference resident.** 1080p at 60 FPS with the model
loaded, measured, and a 30 FPS preset that drops shadows and foliage before anything the
conversation needs. Frame-time percentiles and VRAM recorded in the wiki per preset.
*Needs:* nothing. *When:* v1.5. *Size:* M.

**5.3 Controller support is a chat problem.** The design defers controllers. With chips
(1.3) and an on-screen keyboard as a last resort, a controller player can play most of the
game, which makes it far more presentable at review. Decide early whether chips are
enough. *Needs:* 1.3. *When:* v1.5. *Size:* M.

## 6. Playtesting as the evaluation instrument

**6.1 Opt-in decision recording in the player.** With consent on first run, every decision
(request, reply, result, execution outcome, the player's line) is written to a local JSONL
file the tester can send back. It is the bug report for "the guard just stood there", and
the raw material for the next hundred evaluation prompts, labelled by what the tester
actually wanted. *Needs:* a decision sink in the framework (Phase 2). *When:* v1. *Size:*
S.

**6.2 A tester console.** F2 opens: set quest state, give gold, teleport, force the next
reply (switch the agent to `ScriptedProvider` for one turn), replay a recorded decision,
dump the current prompt. Half of it is the Decision Log window made available in a build.
*Needs:* 6.1 and 4.1. *When:* v1. *Size:* M.

**6.3 Scenario tests that play the game.** Multi-turn scripts (state setup, a sequence of
player lines, the expected action after each, state changes between turns) that run in
EditMode against `ScriptedProvider` for invariants and in the Phase 4 harness against a
model for accuracy. The design's §14 table is the first scenario list. *Needs:* multi-turn
scenarios in the evaluation fixture format (#66). *When:* v1. *Size:* M.

## 7. A first slice that meets the model early

The build plan's G1 to G4 build the RPG before a model is connected. For a game whose
selling point is its conversations, the slice that matters first is the conversation loop
in front of a model, with just enough game around it to mean something. This is offered as
an alternative milestone order, not a replacement for the build plan's exit checks, which
still apply to each row.

| Slice | Contents | Framework needed |
|---|---|---|
| **S1. One street** | Briar Glen's market block, movement, camera, focus, chat overlay, thinking state (1.2), player name (1.6) | `Agent` with `ScriptedProvider` |
| **S2. One trader** | Mara: four stock entries, one quote, inline confirmation, receipt, journal (2.2), refusal lines (2.1), chips (1.3) | Offered-set read; execution result |
| **S3. One model** | `OllamaProvider` replaces `ScriptedProvider`; streaming (1.1); interrupt (1.4); leave (1.5); provider status (4.2); recording (6.1) | Phase 2 provider, streaming, statuses, sink |
| **S4. One quest, two ways** | Iven's offer, Fen's clue, Kest's negotiation or fight, the three dispositions; memory notes; scenario tests (6.3) | Memory, scenario fixtures |
| **S5. The rest of the RPG** | Inventory UI, levels and perks, side quests, routines, save/load with memory export, reputation and gossip (3.2), barks (3.1) | Memory export, Phase 3 guards, statement guard |
| **S6. Ship** | Installer (5.1), settings (4.3), performance presets (5.2), scripted fallback (4.1), tester console (6.2), assets from the pilot | Phase 6b for the in-process build |

S1 to S3 are the demo. S4 onward is the game. The order keeps every week of game work
exercising the framework, and the first thing a tester sees is the thing the project is
about.

## 8. Experiments this game makes possible

Each is an A/B with a control arm on the Phase 4 instrument, recorded in
[`findings.md`](../llm-wiki/findings.md) the day it is run.

1. **Model-driven versus scripted conversation** (4.1): tester ratings of the same
   adventure. The only experiment that measures whether the framework makes the *game*
   better rather than the model more accurate.
2. **Per-action target lists** (build plan §2.2's open question): accuracy on the trade and
   quest actions, where wrong pairings are most likely.
3. **History window size** (build plan §2.9): accuracy on the multi-turn scenarios of 6.3.
4. **Voice examples on versus off** (short per-profile samples of how an NPC talks): rated
   prose quality, and whether accuracy moves.
5. **Few-shot from available versus all registered actions**: accuracy against prompt-cache
   hits and latency, since a stable prefix is what Ollama and llama.cpp can reuse.

## Revision log

| Date | Change |
|---|---|
| 2026-10-07 | First draft, from the design review of the same date |
