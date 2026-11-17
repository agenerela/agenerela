# Developer guards: research and proposed next work

Research date: **4 November 2026**. Area: [#138](https://github.com/agenerela/agenerela/issues/138).
Owner: [@JaySlifer](https://github.com/JaySlifer).
Repository inspected: `test` at
[`78c46ed`](https://github.com/agenerela/agenerela/tree/78c46eddfc15b0c2f1d3e3fd890b87dfc45f2869).

This is a source review and design investigation covering all five starting ideas in #138.
It proposes work; it does not introduce a guard, change the pipeline, or report an accuracy
improvement. No model experiment, Unity test run, or runtime timing was performed for this
document. Existing tests were read, not rerun. Any future measurements, including failures,
belong in [findings.md](findings.md) with the conditions and control arm required by
[AGENTS.md](../../AGENTS.md).

## Recommendation

Start with a reusable **action/target compatibility guard**, using a game-supplied predicate
over plain data. The current demo already demonstrates this need with
`never_attack_villagers`. Follow it with a **per-action/target cooldown** that reads execution
history supplied by the game. Keep repetition an explicit game policy, reuse existing
visibility filtering before adding a line-of-sight guard, and retain strict wording as an
opt-in example rather than a default.

These are engineering recommendations, not experimentally established rankings. The owner
can choose a different first slice. No change is proposed to `SchemaLegalityGuard`,
`TargetGroundingGuard`, the grounding threshold, the schema shape, or guard ordering.

## What is already implemented

The issue's opening description is a historical baseline. In the inspected revision:

| Evidence | What it establishes |
|---|---|
| [`IDecisionGuard.cs`](../../UnityProject/Packages/com.agenerela.framework/Runtime/Validation/IDecisionGuard.cs), [`DelegateGuard.cs`](../../UnityProject/Packages/com.agenerela.framework/Runtime/Validation/DelegateGuard.cs), [`GuardInput.cs`](../../UnityProject/Packages/com.agenerela.framework/Runtime/Validation/GuardInput.cs) | The extension point and public test input already exist. The implemented signature is `Inspect(GuardInput)`, rather than the older illustrative signature in build plan §2.5. |
| [`GuardPipeline.cs`](../../UnityProject/Packages/com.agenerela.framework/Runtime/Validation/GuardPipeline.cs) and [`Agent.cs`](../../UnityProject/Packages/com.agenerela.framework/Runtime/Core/Agent.cs) | Built-ins run first; developer guards run in list order. The first non-pass stops evaluation. `Execute` runs legality alone. |
| [`guards.md`](../../UnityProject/Packages/com.agenerela.framework/Documentation~/guards.md) and [`DeveloperGuardTests.cs`](../../UnityProject/Packages/com.agenerela.framework/Tests/Editor/Validation/DeveloperGuardTests.cs) | Cooldown and strict-name checks are already documented examples with tests. These are not reusable named policy classes. |
| [`VillageGuard.Guards()`](../../Demos/GreyBoxVillage/Assets/GreyBoxVillage/VillageGuard.cs) and [`AgentMind.cs`](../../Demos/GreyBoxVillage/Assets/GreyBoxVillage/AgentMind.cs) | A demo now registers `never_attack_villagers`: attacks on category `person` become `none`, while attacks on other targets remain available. This supersedes “no demo game writes one” in #138. |
| [`TargetableQuery.cs`](../../UnityProject/Packages/com.agenerela.framework/Runtime/Unity/TargetableQuery.cs), [`ProximityTargetSource.cs`](../../UnityProject/Packages/com.agenerela.framework/Runtime/Unity/ProximityTargetSource.cs) | Optional line-of-sight filtering already exists during target discovery. A new visibility guard must justify what it adds. |

The existing demo is evidence that target-dependent rules fit the extension point. It is not
evidence that a reusable guard improves model accuracy, or that every execution is protected
against later world changes.

## Design constraints for every candidate

**Use the earliest suitable enforcement point.** A global cooldown belongs in
`IActionHandler.IsAvailable`, so the action is absent from the model's menu. A rule about the
chosen target belongs in a guard when availability cannot express it without hiding the
action from all other targets. Filtering targets is suitable when an object must not be
nameable at all; it is too broad when an agent may discuss a person but must not attack them.

**Use contained refusals for game rules.** Return `RewriteToNone(reason)` when a legal
choice violates game policy. Reserve `Reject` for broken contracts. Keep the reason useful
in telemetry and supply a player-facing line through `Agent.RefusalStatement` where needed.
`ModelDecision` preserves what the model chose; the final `Decision` holds the refusal.

**Keep a fair move whose line is wrong.** When the move is right and only the statement is
not (a story that invents a battle), return `ReplaceStatement(reason, statement)` rather than
containing the move or patching the line in the handler (#266 item 4). The move stands, later
guards still judge it, and `DecisionTelemetry.StatementsReplaced` and the decision log record the
replacement where every other verdict is recorded.

**Do not treat inspection as execution.** `DecideAsync` may be called without `Execute`, and
a later guard may stop a decision that an earlier guard passed. A stateful guard must not
start a cooldown or consume a repetition allowance just because it inspected a candidate.
The game should record the actual relevant gameplay event in its handler. `Execute` returns
true for a contained `none`, and handlers return `void`, so neither that boolean nor a
decision being remembered proves the intended gameplay effect happened.

**Check live invariants again where they matter.** Developer guards do not run again in
`Execute`. Global availability is checked again through legality, but target-dependent
visibility, compatibility, or cooldown may need a handler check. Two outstanding decisions
can both pass before either executes. Do the final check and state update together in the
game's execution path. Long-running actions may also need checks while they continue.

**Keep scene dependencies at the boundary.** Reusable policies over IDs, counters, and
game-supplied predicates can live in `Runtime/Validation/`. Implementations that read
`Transform`, `Component`, scene categories, or physics belong in `Runtime/Unity/` under
DR-011. A predicate does not make scene access suitable for core implementation code.
Do not add target categories to the core registry just to implement the first guard.

**Keep state and diagnostics explicit.** Scope history to the intended agent, define reset
and target-ID reuse behavior, bound retained history, and avoid hidden static state.
`DecisionTelemetry` has named fields, not a general custom-metric dictionary: existing
`GuardsFired`, `Verdict`, and `GuardReason` describe refusals; extra experiment records can
live beside the game's run log without changing the core telemetry API. Since evaluation
stops early, a guard that was never reached is not counted as passing.

## Candidate 1: action and target compatibility

**Question:** Can we prevent combinations such as `give` on `tower` without changing the
schema or teaching the framework a game's categories?

**Proposed design:** An opt-in guard receives a predicate describing allowed combinations.
The game resolves target IDs into its own tags, categories, diplomacy, or inventory data.
A scene adapter can inspect `Targetable.Category` in `Runtime/Unity/`; a non-scene strategy
agent supplies plain data. Inspect the original action and target, pass unrelated actions
and `none`, and rewrite a forbidden pair with a specific reason.

Choose and document behavior for missing metadata: for an action explicitly governed by an
allow-list, the recommended policy is refusal when compatibility cannot be established.
An action outside that policy passes. This distinguishes unknown compatibility from a rule
that never applied. `no_target` handling must follow the action's declared semantics, not
an assumption that every action needs a target.

**Existing proof of need:** GreyBoxVillage allows the model to choose `attack_target`, but
its developer guard refuses category `person`. This is the same policy shape as “give to
characters or props, never the tower.” The current callback is game code, not yet a shared
compatibility library. Build plan §2.2 explicitly leaves enforcement open.

**Limits:** This blocks known forbidden pairs; it does not prove that a permitted action
was intended. It cannot fix “interesting sword” being interpreted as a pickup. Dynamic
relationships, such as a target becoming an ally, need a final handler check.

**Acceptance cases:** permitted/forbidden pairs; same action with a different target;
unrelated action; `none`; allowed targetless action; missing metadata; changed category or
relationship before execution; non-scene target objects; refusal reason and handler not run.

## Candidate 2: cooldown

**Question:** Which repeated actions need post-decision enforcement?

**Proposed design:** Keep action-wide cooldowns in availability. For target-dependent
cooldowns, read the last successful gameplay event for `(agent, actionId, targetId)` from
game-owned history and compare it with an injected clock or turn index. Refuse while elapsed
time is less than the configured interval; pass at the boundary. Inspection reads history;
the handler records it after the event that counts as execution.

Validate configuration and define the meaning of zero, negative intervals, clock rollback,
new sessions, and target-ID reuse. Prefer separate clear time and turn policies over a
single ambiguous counter. Unity's double-precision time API is appropriate for long-running
sessions; the game must choose game time, real time, or turns explicitly. The existing bell
example uses game time and records each threat separately. [Unity time documentation](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/time/timeasdouble)

**Limits:** A cooldown can suppress legitimate urgent repetition. A successful handler call
may start a long-running action rather than complete it; the game decides which event
starts the interval. A guard alone does not reserve the action between inspection and use.

**Acceptance cases:** first use; just before/exactly at/after expiry; independent targets
and actions; independent agents; inspected but never executed; later guard refusal; failed
gameplay effect; two outstanding decisions; reset; bounded history; deterministic fake clock.

## Candidate 3: repetition

**Question:** When is repeating a pair evidence of a stuck agent rather than correct play?

**Proposed design:** An opt-in policy with a configured action set, maximum consecutive
executions of the same `(actionId, targetId)`, and explicit reset rules. Count actual game
events, rather than every model proposal or remembered exchange. Decide whether changing
targets, completing an objective, or receiving a new instruction resets a streak.
Do not count guard-generated `none` toward the streak: doing so can accidentally reset the
blocked pair and allow an endless act/refuse cycle.

**Limits:** Repeated attacks, resource gathering, and movement can be correct. Refusing a
loop does not choose a better action, guarantee progress, or detect alternating A/B loops.
A rolling-window detector is a separate policy with different tuning and false positives;
start with the simpler consecutive rule only if a demo exhibits that failure.

**Acceptance cases:** first K repetitions pass and K+1 refuses; action/target changes;
legitimate repeated task; new instruction; unexecuted/refused proposal; refusal does not
erase the streak; reset; independent agents; an alternating sequence documented as outside
the first policy's scope.

## Candidate 4: line of sight

**Question:** Is visibility a rule for naming a target, for a specific action, or for acting
at a particular instant?

**Proposed design:** Use `ProximityTargetSource`'s existing visibility filter when only
visible objects should be offered. Add an opt-in guard in `Runtime/Unity/` only when targets
remain nameable for other purposes, such as remembering a tower while refusing to shoot
through its wall. Scope it to actions that require visibility; do not assume `talk_about`
and `shoot` have the same rule. Reuse an agreed visibility policy across discovery, guard,
and handler where their semantics coincide, without expanding this research into a core
query refactor.

Define eye origin, aim point, range, obstruction layers, trigger policy, and self/target
collider handling. The current query sorts `RaycastAll` hits, skips the agent hierarchy,
and attributes hits by the collider's transform. Preserve these details if sharing its
behavior. Unity documents collider-origin and trigger/layer behavior; a ray test alone is
not field of view, lighting, stealth perception, or path reachability.
[Unity raycast documentation](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/physics/raycast)

**Limits:** A target may disappear or move behind a wall after inspection. A handler must
check visibility at the moment of an attack, and a continuous attack may need repeated
checks. Do not claim that the guard closes this timing gap by itself.

**Acceptance cases:** clear path; wall; own collider; target child collider and Rigidbody
parent; ignored trigger; configured blocker layer; zero-distance target; destroyed target;
action exempt from sight; obstruction introduced after decision; parity with discovery
where the same policy is intended. Physics cases need Unity scene tests.

## Candidate 5: strict wording

**Question:** Is literal wording part of the game's intended interaction contract?

**Proposed design:** Keep the existing name-check example opt-in, with caller-configured
scope. The example uses the free-form label `Player`; that is the game's convention, not
a framework input classification. A reusable version should receive the mapping from ID
to permitted names rather than assume underscores always encode display names. Decide
whether matching means literal phrases, complete words, or an exact command grammar.

**Known cost:** DR-016 already reports false refusals for paraphrases and pronouns and
records the decision to remove the name check from the built-ins. That is prior project
evidence, not a new result from this research. Simple substring matching can also accept
incidental mentions and does not understand negation: “do not attack the dummy” still names
the dummy. Literal presence is not proof of intent.

**Acceptance cases:** configured name; case variation; alias; punctuation and boundaries;
unrelated word containing the name; pronoun and paraphrase; negation; incidental mention;
non-player label; `none` and `no_target`. Mark semantic-intent failures as known limits
rather than implying a lexical rule solves them.

## External research and what transfers

The papers below supply context, not validation of these particular game guards:

| Primary source | Relevant observation | Implication here |
|---|---|---|
| Park et al., [Grammar-Aligned Decoding](https://arxiv.org/abs/2405.21047) | Constraining generation to a grammar can distort the model's output distribution; grammatical validity does not establish quality. | Keep syntax/registration checks distinct from game-policy checks. This does not justify changing the existing grounding signal or threshold. |
| Ahn et al., [Do As I Can, Not As I Say](https://arxiv.org/abs/2204.01691) | SayCan grounds language planning using available skills and their feasibility. | Supports separating what sounds plausible from what the environment permits. The proposed deterministic compatibility predicate is an analogy, not an implementation of SayCan. |
| Ren et al., [Robots That Ask For Help](https://arxiv.org/abs/2307.01928) | KnowNo uses calibrated uncertainty and human help in language-based planning. | Refusal or clarification can be a useful outcome. Its statistical guarantees do not transfer to this project's guards without its calibration method and assumptions. |

## Evaluation plan before claiming an improvement

First establish policy correctness with hand-built `GuardInput` instances, requiring no
model. Use fake clocks and game-owned execution history. Add integration tests with a fake
provider to check built-in ordering, first refusal, telemetry, refusal wording, and that a
blocked action does not run. Exercise the execution-time race in the handler as a separate
case. Demonstrate each new test can fail when its protected rule is disabled, following the
repository's testing convention. Run the existing EditMode suite in pinned Unity 6000.3.23f1;
use scene tests for physics policies. Documentation review alone cannot establish these gates.

Then compare **built-ins only** with **the same built-ins plus one candidate guard** using
the same model, machine, prompt set, and session. Hold prompt/schema/examples and grounding
settings fixed. Reset game and guard state identically; for history-dependent policies,
replay matched multi-turn scenarios, not unrelated isolated prompts. Alternate arm order
and repeat runs. Keep training examples out of evaluation prompts and freeze expected
acceptable actions/targets before scoring; have a second person label ambiguous cases.

Use balanced scenarios with legitimate repeats, invalid pairs, new targets, expiry
boundaries, and world changes. Separate fixed-decision replay, which isolates containment,
from live-model episodes, where refusals change subsequent history and therefore choices.
Do not mix those two results into one accuracy figure.

Report counts and denominators for correct useful behavior, wrong decisions contained,
wrong actions executed, valid choices wrongly refused, natural `none`, execution-time
blocks, and pipeline errors. Count a permitted-but-wrong choice as wrong even if the guard
passes it. Include task completion for sequential scenarios; blocking everything is not a
success. Record how often the candidate was actually reached after the built-ins.

Preserve per-case stimulus, expected choices, original/final decision, guard reason,
execution event and state timeline, plus run configuration and raw logs. Time guard checks
separately if reporting overhead: `LatencySeconds` measures the provider request and is
not a guard timer. Report paired outcome differences and uncertainty, not just a percentage.
No sample size or pass threshold is established here; choose those before the run based on
the effect worth detecting. The repository warns that small prompt sets have substantial
noise. Negative and inconclusive results belong in `findings.md` too.

## Proposed work slices and progress record

Each implementation slice should have its own issue or PR into `test`, linked to #138.
The following are proposed slices, not newly opened issues or completed implementation:

| Order | Slice | Evidence needed to finish |
|---|---|---|
| 1 | Plain-data compatibility guard and one demo integration | Explicit unknown-metadata policy; unit/integration cases; handler check for mutable rules; docs; unchanged behavior when omitted |
| 2 | Per-action/target cooldown with game-owned execution history | Injected clock/turn semantics; no state mutation on inspection; expiry/reset/race cases; demo example |
| 3 | Repetition investigation driven by a recorded demo failure | Definition of legitimate repeats; streak policy and reset semantics; paired episode evaluation |
| 4 | Visibility policy where discovery filtering is insufficient | Action scope; Unity placement; physics cases; execution-time check; justified reuse of existing query behavior |
| 5 | Strict wording only for a game that requests that contract | Explicit matching/scope rules; false-refusal cases; comparison with the existing example |

**Progress on 4 November:** reviewed the extension point, pipeline, execution re-check,
guard docs and tests, current village integration, and target visibility implementation;
checked primary literature and Unity API documentation; compared all five candidates;
identified implementation boundaries and an evaluation protocol. The initial recommendation
is compatibility first, with cooldown next. No new runtime behavior or measurement is claimed.

For subsequent issue updates, record the actual artifact link, what was completed, what
was learned, validation performed, unresolved limits, and the next concrete step. Link
measurements to their `findings.md` entry. Keep #138 open as the area tracker.
