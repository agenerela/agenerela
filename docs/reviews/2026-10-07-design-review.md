# Design review — 7 October 2026

**What was reviewed.** `test` at `61c3a06` (the whole repository: build plan, decision
records, `AGENTS.md`, the framework package and its tests, `GreyBoxVillage`, the evaluation
prompt set, the benchmark probes, CI), plus the `codex/rpg-demo-plan` branch at `16a1e6a`,
which carries the Living Vale RPG plan and DR-017. Living Vale is read here as a use case:
what a real game needs from the framework, and where the current design bends or breaks
under it.

**What was not done.** Nothing was compiled and no test was run: this review was written
without Unity, as [`REVIEWING.md`](../../REVIEWING.md) asks a reviewer to say. Every code
finding below comes from reading the source, so "this throws" means "this is written to
throw", not "I saw it throw". No model was run and no accuracy number here is new.

**How to use this document.** §2 lists what is right and should survive any rework. §3 is
the findings, each labelled with the four labels from `REVIEWING.md` (**Blocking**,
**Should fix**, **Nit**, **Question**) and written as *where, what, why, fix*. §4 reads the
Living Vale plan against the framework. §5 is a prioritised feature backlog with the phase
each item belongs to. §6 is the list of small edits that can be made today. Findings are
numbered F1–F34 so an issue or a pull request can cite one.

---

## 1. Verdict

**The design is unusually sound for a project at this stage, and its weak points are
mostly things it already knows about and has not yet written down as decisions.** The
thesis (the scaffolding is the product, the model is a commodity) is measured rather than
asserted, the rules that came out of measurement are enforced structurally rather than in
prose, and the code that exists is careful. The risks are not in what has been built. They
are in three places:

1. **Two contradictions in settled rules** that the next two issues (#8, #17) will have to
   resolve on the fly if nobody resolves them first: whether `target` can ever disappear from
   the schema (F1), and what a decision that was cancelled or timed out is called (F2).
2. **The seam between deciding and executing is thinner than every demo needs.** Both game
   plans independently invented the same two things: an execution receipt, and a re-check
   against the world *now* rather than at decision time. That is the framework's job, not
   the game's (F3, F4, F5).
3. **Schedule.** Nine weeks remain in COMP 490; Phases 1 to 4 are owed by December; Phase 1
   has six open issues on its critical path and nothing has yet produced a decision. Meanwhile
   the demo list grew by two games and an asset server. The plan's own rule, that demos are
   the integration tests, is at risk of inverting into demos that need nothing from the
   framework for months (F28, §4.3).

| Area | State | Main point |
|---|---|---|
| Thesis and positioning | Strong | Measured, honestly bounded, defensible at review |
| Settled design rules | Strong, one contradiction | F1 must be decided before #8 merges |
| Core types (`Agent*`, `Decision*`) | Good, one gap | Telemetry has no cancelled/timed-out state; no execution result (F2, F3) |
| Actions and registries | Strong | Two front doors over one type, one parameterised fixture |
| Targets and discovery | Good, one behaviour wrong | Decision-time throw on a designer's typo (F7) |
| Provider contract | Strong | Designed against five backends, two of them not planned |
| Grounding guard (DR-016) | Novel and measured, one unverified dependency | Pre-mask probabilities on the shipping provider (F10) |
| Scheduling | Not designed | Deadlines are missing from the API both game plans need (F4) |
| Memory | Sketched well | Needs an execution hook and a save hook (F5, F6) |
| Evaluation | Instrument not built | Can start measuring now with a Python runner (F27) |
| Process and CI | Strong hygiene, no compiler | Reviewer's Unity is the only compiler (F29) |
| Docs | Excellent, heavy, drifting in places | README status and `.env.example` are stale (F31, F32) |
| Living Vale plan | Well aligned, over-scoped | Reorder milestones so the chat slice meets the framework first (§4) |

---

## 2. What is good, and should survive any rework

Keep these. Several are rarer than they look.

1. **Every settled rule has a number behind it, and the negative results are kept.** Field
   order (+11.7), few-shot (+35.3), the reasoning field that made things worse, the name
   check that refused more good requests than it saved. The `findings.md` entries say what
   would change the author's mind. That is the habit that keeps a research project honest,
   and it is already a habit here.
2. **Illegal options are unexpressible, not forbidden.** State masking of actions, the
   `no_target` sentinel, `none` injected last, a closed route absent from the enum. The
   framework makes the bad answer impossible to emit rather than asking the model nicely.
   This is the single idea that makes a 2B model usable, and the code keeps to it
   (`ActionAvailability.For`, `TargetRegistry`, `ActionRegistry.None`).
3. **The provider is untrusted and the executor validates independently** (hard rule 5,
   `ILLMProvider` remarks, `ProviderCapabilities` remarks). No capability flag can switch a
   guard off. The plan says it three times in three places, which is the right number.
4. **Scene independence is enforced by CI, not convention.** "Only `Runtime/Unity/` touches
   a scene" is what lets a country be an agent, and the hygiene workflow fails a pull request
   that breaks it. `ExplicitTargetSource` and the plain-class `Agent` are the proof it works.
5. **Two front doors over one plain type, with one parameterised fixture** (DR-011,
   `FrontDoorTests`). Nothing downstream can tell an asset-authored action from an attributed
   method, and every registration path runs the same assertions. The "asset wins the
   definition, the method keeps the handler" rule is exactly what a Phase 4 A/B needs.
6. **The provider contract was tested against vendors it was not designed for.**
   `Documentation~/providers.md` asks what OpenAI and Anthropic would need and finds one
   field on a supporting type. That is how you know an interface is done.
7. **DR-016 is a genuinely good idea, measured, with its limits stated.** Reading how much of
   the model's pre-mask probability stayed on the target list, at every token, costs 0.07 s,
   needs no word lists, passes paraphrases and pronouns, and catches look-alike swaps. The
   AUROC numbers and the three refused-good-requests are in the record. Nobody oversold it.
8. **Decision records with options, consequences and revisit triggers.** DR-009 to DR-016
   each weigh the alternatives and say what would reopen them. Reviewers can argue with a
   table instead of a memory.
9. **The repo-hygiene workflow is better than most professional Unity repos'.** GUID
   stability in place and across moves, `.gitignore` rules that would hide tracked files,
   the pinned Unity version across every project, the relative `file:` reference check, the
   attribution check. Each check documents the incident it exists for.
10. **The code is careful.** Culture-invariant id comparison with the Turkish-locale reason
    written down; ordinal string compares; deterministic ordering by metadata token;
    all-or-nothing registration; exceptions from game code rethrown with their own stack;
    `TryGet` never throws on a provider's raw output; `Awaitable` chosen on purpose. The test
    files read as specifications, and most test names would fail for the right reason.
11. **Demos are separate projects restricted to the public API** (DR-010). Three genres
    through one surface is the cheapest reusability proof available, and `Demos/README.md`
    makes adding one mechanical.
12. **Positioning is honest.** "Replaces the top-level behaviour-selection node, never
    behaviour trees"; the cost table that makes local-first an engineering choice rather than
    an apology; the VRAM figure stated as a constraint. The review panel will not catch the
    team overclaiming, because the team does not.
13. **`AGENTS.md` as the one rule file for every agent**, with `CLAUDE.md` a pointer. Most
    teams discover the drift problem after it has bitten them.

---

## 3. Findings

### 3.1 Contradictions in settled rules — decide before the next merge

#### F1 — `target` is "required, never optional" and also "removed when the registry is empty" — **Blocking** for #8

**Where.** [`AGENTS.md`](../../AGENTS.md) line 60 ("`target` is required ... Never optional");
[build plan](../FRAMEWORK_BUILD_PLAN.md) §3 Phase 1 row ("empty target-registry removes
`target` property entirely"); `Runtime/Actions/ITargetSource.cs` line 12 ("an empty registry
is what removes `target` from the schema"); issue #8 ("note `target` disappears entirely");
Living Vale design §16.7 flags the same thing.

**What.** Two settled rules disagree, and #8 is labelled `ready` with the disappearing
version in its definition of done.

**Why it matters.** `AgentDecision` refuses a null or whitespace target id
(`Runtime/Core/AgentDecision.cs` line 27), so a reply without a `target` field cannot become
a decision unless the parser synthesises `no_target`. That means two request shapes, two
parser paths, two golden files, and a guard that has to know which shape it is looking at.
The measured rule is "required with a sentinel"; the disappearing variant was never measured
and buys nothing, because a grammar enum with the single value `no_target` costs no tokens
and cannot be answered wrongly.

**Fix.** Keep `target` in every schema, always required, with its enum reduced to
`[no_target]` when the registry is empty. Update #8's definition of done, the Phase 1 row,
the `ITargetSource` remark, and gate 5 in #11 ("empty registry → the target enum is exactly
`[no_target]`"). Record it as a one-paragraph DR, since it changes a ready issue.

#### F2 — A cancelled or timed-out decision has no name — **Should fix** before #17

**Where.** `Runtime/Core/DecisionOutcome.cs`; `Runtime/Core/DecisionTelemetry.cs` line 16;
`Runtime/Core/DecisionResult.cs` line 20. Raised independently by the risk-race plan
(question 2) and Living Vale (§16.5).

**What.** `DecisionOutcome` is an evaluation classification (correct, wrong-legal,
contained, rejected, pipeline error) and is `null` at runtime. There is no runtime status at
all: a decision the game cancelled because the racer reached the fork, one that missed its
deadline, one whose provider failed, and one whose reply did not parse are either an
exception or the same `PipelineError`. The Decision Log will blame the provider for a
deadline the game set.

**Why it matters.** Two of three planned demos have deadlines. The eval harness needs to
exclude cancelled runs from accuracy rather than count them as errors.

**Fix.** Split the two concerns. Keep `DecisionOutcome` as the harness's label. Add a
runtime `DecisionStatus` on telemetry: `Decided`, `RewrittenToNone` (Phase 3), `Rejected`
(Phase 3), `Cancelled`, `TimedOut`, `ProviderFailed`, `Unparseable`. Let `DecisionResult`
carry a null decision for any status other than `Decided` and `RewrittenToNone`. Keep
cancellation an `OperationCanceledException` at the provider boundary as documented, and
have `Agent.DecideAsync` catch it and return the `Cancelled` result so a game never has to
try/catch around a decision.

### 3.2 The decide → execute seam — what both game plans had to invent

#### F3 — `Execute` returns nothing, so the game cannot know what happened — **Should fix** (design #17 this way)

**Where.** Issue #17 (`public void Execute(DecisionResult result)`); Living Vale §12
("`InteractionReceipt` ... complements framework decision telemetry; it does not pretend that
selecting an action proves its gameplay effect succeeded"); §16.3.

**What.** `Execute` is void. A handler can refuse (stale stock, departed NPC), throw, or run.
The game has no way to learn which without instrumenting every handler itself. Living Vale
wrote a whole receipt system to cover this; memory rule 1 ("remember what happened, not what
the model said") cannot be honoured either, because the framework does not know whether the
handler did anything.

**Fix.** `Execute` returns an `ExecutionResult`: `Ran`, `RefusedByRecheck(reason)`,
`RefusedByHandler(reason)`, `HandlerThrew(exception)`. Give `IActionHandler.Execute` a way to
refuse without throwing (return a bool or an `ExecutionResult`; a `bool` is enough for Phase
1). Memory's `Remember` takes the execution result, not only the decision. Telemetry records
it. The Decision Log's "the guard just stood there" then has a fourth answer: the handler
declined.

#### F4 — No deadline on a decision; the queue cannot order by one — **Should fix** before Phase 2's queue

**Where.** Build plan §2.6, DR-015; risk race (5 s to the fork, question 4); Living Vale §8
(8 s per chat turn).

**What.** `DecideOptions` is specified with `Priority` (player-facing or background) and
nothing about time. A game that needs an answer in five seconds has to cancel from outside,
and the queue cannot tell a request that can still be served from one that has already
missed its moment. DR-015's own revisit clause ("two tiers prove too coarse once ... four
agents a turn") is the symptom.

**Fix.** Add `DecideOptions.Deadline` (an absolute time, or `Timeout` as a span). The queue
serves the earliest deadline first within a priority tier, drops a request whose deadline has
passed before it is sent (status `TimedOut`, cost zero), and records queue wait separately
from generation time in telemetry. This answers DR-015's revisit question without a third
tier, and it is what the risk race needs on day one.

#### F5 — `Execute` cannot re-check against the world *now* unless the result carries its context — **Should fix** (design #17 this way)

**Where.** Issue #17 ("re-checks legality"); risk race question 1 ("the canyon can close
between the gate and the fork"); Living Vale §16.1.

**What.** `DecisionResult` holds a decision and telemetry, not the `AgentContext` the
decision was made under. `Execute(result)` therefore has to rebuild a context, and the plan
does not say whether targets are re-collected or the decision-time registry is reused. The
race needs *now*; the handler also needs the target object, which only the registry has.

**Fix.** `DecisionResult` carries the decision-time `AgentContext` (it is already a
snapshot). `Execute` re-collects targets through the agent's sources, re-runs
`ActionAvailability.For` on a fresh context, and checks the decision against the fresh one.
Handlers receive the fresh context. Document that an `ITargetSource` is therefore called
twice per decision, and that `Collect` must be cheap and side-effect free.

#### F6 — Memory has no save hook and no clear — **Should fix** before Phase 2 settles the interface

**Where.** Build plan §2.9 ("Revisit later: saving memory with the game ... wait until a demo
needs it"); Living Vale §11 and §13 ("If memory export/import is not public, clear rolling
history on load").

**What.** A demo now needs it. Living Vale's save/load design has to clear conversation
memory on load because it cannot export it, which means a reload forgets the last six turns.

**Fix.** Add `Clear()` and an `Export()`/`Import(string)` pair to `IMemoryStrategy` in Phase
2, with the default `RollingHistory` serialising its turns as plain JSON through Newtonsoft.
Two methods now cost less than a demo-owned workaround later.

### 3.3 Actions, targets, schema

#### F7 — A designer's typo on a `Targetable` throws at decision time — **Should fix**

**Where.** `Runtime/Unity/ProximityTargetSource.cs` lines 96 to 115 (malformed id, duplicate
id, id an earlier source added, all `InvalidOperationException`); the same for
`Runtime/Actions/ExplicitTargetSource.cs` line 49. DR-014 says "a duplicate id within one
agent's resolved set is a decision-time error **recorded in telemetry**".

**What.** The code throws where the decision record says record. In a built game, a
mis-typed id on one prop in range stops every agent near it from deciding at all, and the
exception surfaces in whatever game code awaited `DecideAsync`.

**Fix.** Keep the throw in the Editor (it is the right loud failure while authoring) and
drop-and-record in a player build: skip the offending `Targetable`, add its id to a new
`DecisionTelemetry.TargetsRejected` with the reason, log an error once per id. A static
`StrictTargets` switch, default true in the Editor and false in a player, keeps both
behaviours testable.

#### F8 — One flat target enum for every action — the first design question every demo past the village hits — **Should fix** (an early Phase 4 A/B)

**Where.** Build plan §2.2 "Revisit later — which targets an action accepts"; Living Vale
§9 and §10 (eleven actions, each on a different typed target); §16.2.

**What.** Today the model can pair `quote_purchase` with a `KnowledgeCard`, or `give` with
the tower. The plan knows this and defers it. Living Vale's eleven actions make it the
common case rather than an edge, and the game's answer (type-check in every handler) is the
prose-rule failure mode in a new shape: the bad pair is possible and caught late.

**Why it is feasible.** Every planned dialect can express "if `action` is X then `target`
is one of these": JSON Schema `oneOf` of objects with a `const` action and a per-action
`enum` (Ollama compiles through llama.cpp's schema-to-grammar converter, which handles
`oneOf`; Gemini and OpenAI strict mode accept `anyOf`), and GBNF expresses it as an
alternation of sequences. Field order is preserved inside each branch.

**Fix.** Add `ActionDefinition.TargetCategories` (string set; empty means any) now, since
`Targetable.Category` already exists and `ExplicitTargetSource.Add` can take a category.
Build the per-action enum in `DecisionSchema` behind a switch, default off. Make "flat enum
vs per-action enum" one of the first Phase 4 A/B runs; it changes the request shape, so it
needs the control arm. Until the A/B, a Phase 3 guard rejects the pair after the fact.

#### F9 — Parameters beyond `target`: name the idiom instead of leaving each game to invent it — **Should fix** (documentation, then a DR)

**Where.** Build plan §2.1 (`AgentDecision` is three fields); issue #8 extension points;
Living Vale §9 ("game-owned typed targets for catalog entries, sale bundles, quotes") and
§15's risk row "Too many parameters for current schema".

**What.** The framework has one parameter slot, `target`, and Living Vale discovered the
right way to use it: a target is any game-owned typed object, so a quantity or a price is
carried by *which* object was named (`stock_mara_potion_one`), never by a free field. That
is a good idiom. It is grammar-constrained, it keeps the model from inventing numbers, and
it needs no schema change. It is also undocumented, and its cost (the enum grows with every
bundle size) is exactly the prompt-size cost the plan warns about everywhere else.

**Fix.** Write it down in §2.2 as *the* way to pass a parameter: "a parameter is a target;
offer the few legal values as objects". State the budget: with `Cap` at 8 and a measured
143-token prompt, every bundle is an enum value. Pair it with F8, which is what makes typed
targets safe (a `quote_purchase` can only name stock entries). Record in a DR that a free
`quantity` field is deferred until a demo shows typed targets do not fit, and that adding
one is appended after `statement` and A/B-tested.

#### F10 — The grounding guard's shipping dependency is unverified: pre-mask probabilities from llama.cpp — **Question**, with a one-day spike attached

**Where.** DR-016 consequences; `ProviderCapabilities.ReportsTokenProbabilities` remarks;
`Documentation~/providers.md` ("Token probabilities are not known yet ... the check that
matters most").

**What.** The guard the thesis now rests on reads probabilities from *before* the grammar
mask. Ollama provides them. The provider the framework ships behind (LLMUnity over
llama.cpp) is "not yet tried", and the fallback (a one-line prompt rule) measured 88 against
95 of 109 on the 2B. If the shipping path cannot report pre-mask probabilities, the headline
accuracy claim does not transfer to a built player.

**Fix.** Do not wait for Phase 6b. llama.cpp's server exposes `n_probs` and a
`post_sampling_probs` switch (the default reports probabilities from the raw logits, before
the sampler chain the grammar lives in). Spend one day now: run LLMUnity's sample with
`nProbs` set and a grammar on, and check whether a token the grammar forbids still appears
in the top-n with its original probability. Record the answer in `findings.md` either way.
If it is no, DR-016 needs its fallback promoted to the plan of record for shipped games, and
the review presentation should say so.

#### F11 — An exception inside a handler's `IsAvailable` kills the decision — **Should fix** at #17

**Where.** `Runtime/Actions/ActionAvailability.cs` line 39 (`HandlerFor(definition).IsAvailable(ctx)`
with no guard around developer code); `MethodActionHandler.IsAvailable` rethrows the game's
exception by design.

**What.** `IsAvailable` is developer code and will throw (a destroyed `NavMeshAgent`, a
missing state key). Today that propagates out of `ActionAvailability.For`, so one bad
availability check stops the whole decision.

**Fix.** Decide the policy in #17 and test it: either the action is masked and the exception
recorded in telemetry (`GuardsFired`-style list, "availability check threw"), or the decision
is a `PipelineError`. Masking is safer for a shipped game; failing loudly is better in the
Editor. The same switch as F7 can choose.

#### F12 — A few-shot example may demonstrate a target that is not in this request's enum — **Question** for #10

**Where.** `ActionDefinition.PreferredExampleTargets`; `ExampleAttribute.PreferredTargets`;
build plan §2.3 rule 4; DR-014 (targets are collected per decision).

**What.** Preferred example targets are ids the developer wrote at authoring time. Under
discovery the registry changes every decision, so the preferred `tower` may be out of range
when the request is built. An example that answers with an id the enum does not contain
teaches the model a value the grammar will then forbid; the demonstration is wasted and the
model is nudged toward a near miss.

**Fix.** #10 should pick the first preferred target that is in the current registry, fall
back to any registered target the verb applies to (category match, once F8 lands), and omit
the example rather than demonstrate an absent id. Test the fallback and the omission.

#### F13 — `AgentContext.State` is a bag of `object` — **Nit**, with a suggestion

**Where.** `Runtime/Core/AgentContext.cs` line 16; issue #17 (`(bool)ctx.State["isFollowing"]`
in the sample test).

**What.** Handlers written through the asset front door cast out of a string-keyed
dictionary, and a missing key is a `KeyNotFoundException` at decision time. Attributed
methods never need it, since they read their owner's fields.

**Fix.** A `TryGet<T>(string key, out T value)` helper on the context, or let the agent carry
one developer-typed state object. Not Phase 1 work; worth a line in #17's extension points.

#### F14 — `ProximityTargetSource` scans the whole scene every decision — **Nit**

**Where.** `Runtime/Unity/ProximityTargetSource.cs` line 142 (`FindObjectsByType<Targetable>`),
with the comment "decisions are seconds apart, so the cost is paid rarely".

**What.** True for one guard. Thirty agents at six decisions a minute in a thousand-object
scene is three thousand full scans a minute, and F5 doubles it. The comment's assumption
should be a number in telemetry rather than a belief.

**Fix.** A static set of enabled `Targetable`s maintained in `OnEnable`/`OnDisable`, which
also makes the source testable without `FindObjectsByType`. Cheap, and it can wait until a
profile shows it matters.

#### F15 — No `link.xml`, so IL2CPP can strip what reflection and Newtonsoft need — **Should fix**, five minutes

**Where.** `Runtime/Actions/AgentActionReader.cs` line 15 (deferred to "the Phase 6b player
test"); DR-009 consequences; DR-011 consequences. No `link.xml` in the package.

**What.** Both DRs defer the stripping problem to the first player build. The fix is a
twelve-line file, and a player build is also what the risk race and Living Vale produce
early.

**Fix.** Add `link.xml` at the package root preserving the `Agenerela` assembly and
`Newtonsoft.Json`, and mark the telemetry and decision types `[Preserve]`. Costs nothing
until it saves a day.

### 3.4 Providers, scheduling, telemetry

#### F16 — Telemetry has no stated schema; every DR adds a field — **Should fix** (one doc page)

**Where.** `Runtime/Core/DecisionTelemetry.cs` holds latency, tokens, provider, version,
guards fired, targets dropped, outcome. The plan has promised, in separate places: the
resolved provider config (DR-013), schema mode (findings, #3), priority (DR-015), the
resolved target set (DR-014), the grounding score (DR-016), observations dropped (§2.8),
history turns sent and dropped (§2.9), queue wait (Living Vale §15), and now status (F2)
and execution result (F3).

**What.** Nothing lists them together, so #17 and Phase 2 will add them one at a time in
whatever shape is convenient, and the Decision Log window in Phase 5 will inherit the result.

**Fix.** One table in the build plan (§2.1 or a new §2.10): field, type, who fills it, which
phase, which DR. Then `DecisionTelemetry` grows against the table, and the eval report and
the log window are designed from it.

#### F17 — `Awaitable` is single-consumer; the design must not hand it to more than one listener — **Nit**, document it

**Where.** `Runtime/Providers/ILLMProvider.cs` line 76; DR-011 amendment (the `Awaitable`
choice); #17's planned `OnDecided` events.

**What.** A Unity `Awaitable` can be awaited once. The overlay in `GreyBoxVillage`, the
Decision Log, and the game code all want the same result. If `Agent` ever exposes a provider
awaitable, the second awaiter throws.

**Fix.** `Agent.DecideAsync` awaits the provider itself and publishes the `DecisionResult`
through events; callers never see the provider's awaitable. One sentence in #17.

#### F18 — A `ScriptedProvider` in the runtime, not only a test fake — **Should fix**, and it changes §4

**Where.** Issue #17 (`FakeProvider` in the test assembly); Living Vale §13
(`ScriptedNpcDecisionDriver` ... "never copy the framework's schema, prompt builder or
guards to make the placeholder look complete"); risk race `PlaceholderRacerBrain`;
`GreyBoxVillage`'s `VillageCommands` string matching.

**What.** Three demos have each written, or planned, a placeholder brain that bypasses the
framework entirely until Phase 2. That means months of game code that exercises no
registry, no schema, no availability masking and no `Execute` re-check, and a seam
(`INpcDecisionDriver`, `IRacerBrain`) that has to be replaced later.

**Fix.** Ship `ScriptedProvider : ILLMProvider` in `Runtime/Providers/`: given the request,
it returns a reply from a delegate or a table keyed on the stimulus, with an optional delay
and an optional failure mode, and it reports no constrained decoding and no probabilities.
A demo then calls the real `Agent.DecideAsync` from day one, every settled rule runs, and
Phase 2 is a provider swap rather than a brain swap. It also gives Phase 1 its end-to-end
test for free, and gives Living Vale's "scripted mode" a definition that cannot be mistaken
for model-driven behaviour: the overlay reads the provider's name.

#### F19 — `RateLimit` has no token dimension — **Nit**, already acknowledged

**Where.** `Runtime/Providers/ProviderCapabilities.cs`; `Documentation~/providers.md`
("one real gap"). Deferred to Phase 6 with a reason. Nothing to add beyond noting it is
tracked only in prose; a line in #3's extension points would keep it from being lost.

### 3.5 Evaluation

#### F20 — The prompt set's fixture format cannot express what the probes already found — **Should fix** in #66

**Where.** `UnityProject/Assets/Evaluation/prompts/*.json` (54 prompts, one vocabulary, every
precondition `isFollowing`, two labeller names); `findings.md` second run (eight open
choices, six look-alike near misses, pronouns resolved from history).

**What.** A case has `expectedAction` and `expectedTarget`, singular. The probes showed
three kinds of case the harness has to score and the format cannot hold: an open choice
where either of two targets is correct, a multi-turn reference that needs staged history,
and a case whose correctness depends on which targets were registered (the events file
expects `nearby_enemy`, which no precondition registers). The set also has one action
vocabulary, so nothing measures generalisation beyond the guard.

**Fix.** Per case: `targets` (the registry to stage), `history` (turns to stage), and
`acceptable` as a list of action/target pairs with `expected` as the preferred one. Add a
second vocabulary (the fort sentry from the probe is already written) so Phase 4 can report
per-scene. Label the labellers by handle, not placeholder names, since agreement is reported.

#### F21 — The README's headline number is the least controlled one — **Should fix**

**Where.** [`README.md`](../../README.md) line 22 ("the same model reached **95%**");
`findings.md` ("the few-shot +35 is one small measurement"; the 95% is a 20-prompt focused
suite in Appendix A).

**What.** The controlled numbers are 58.5 → 84.9 (n=53 per arm) and 83 → 108 of 130 with
the guard. The README leads with 95%, which comes from a 20-prompt suite of player commands.
The team is otherwise scrupulous; the front page is the exception.

**Fix.** Lead with the controlled A/B and the 130-prompt probe, and label the 95% as the
focused suite if it stays.

### 3.6 Process, schedule, repository

#### F22 — Phase 1's critical path is six issues deep and nothing has produced a decision — **Question** for the team

**Where.** Open issues #8, #9, #10, #16, #17, #11; the Phase 1 milestone; §6.5 of the plan
(Phases 0 to 4 in the fall).

**What.** `Agent` is blocked by everything, and it is 7 October. Phase 2 (provider, queue,
memory), Phase 3 (guards) and Phase 4 (harness, 200 prompts, second annotator) all follow.
The plan's December deliverable, "a working, measured framework", needs all four.

**Fix.** Three moves that shorten the path without cutting the deliverable:
- Merge #8 and #9 as one pull request if they have one owner's time, and #10 and #16 as
  another; the golden file in #16 is the test that proves all four together.
- Let `Agent` (#17) land against `ScriptedProvider` (F18) before #10 and #16 are polished,
  with the few-shot block empty. End-to-end first, then fill in.
- Start Phase 4 measurement now in Python (F27), so the instrument exists before the Unity
  harness does.

#### F23 — Phase 2 is three deliverables in one row — **Should fix** (split the row)

**Where.** Build plan §3, Phase 2: "OllamaProvider + queue + memory".

**What.** The provider is the critical path for every demo. The queue is needed by the race
and the strategy demo. Memory is needed by Living Vale and unmeasured. Bundling them means
the provider waits on memory's design questions (§2.9 rules 3 and 4).

**Fix.** Phase 2a: `OllamaProvider` and `AgentBehaviour`, with `GreyBoxVillage` making its
first real decision as the definition of done. Phase 2b: queue with priority and deadline
(F4). Phase 2c: memory. Each independently shippable, which is the plan's own standard.

#### F24 — GreyBoxStrategy is "never cut", labelled ready since 8 September, and unassigned — **Question**

**Where.** Issue #20; `Demos/README.md`; build plan §3 ("if the API can't express a non-NPC
agent you want to know in month two, not month eight").

**What.** The demo the plan calls the thesis demo has no owner, while two new demos (Living
Vale, risk race) have plans and assignees. Month two is now.

**Fix.** Assign it. Its grey box needs nothing from the framework and can use
`ScriptedProvider` (F18) and `ExplicitTargetSource` the day #17 lands, which makes it the
cheapest demo to start and the one that tests the most.

#### F25 — DR-017 was decided on a branch, by one person, and is recorded as a decision — **Question**

**Where.** `codex/rpg-demo-plan`: `docs/FRAMEWORK_BUILD_PLAN.md` DR-017 ("Direction decided
with the user"); `living-vale-resume.md`.

**What.** The plan's process is that a pull request into `test` gets a teammate's review,
and that a decision costing more than an hour to reverse gets a DR. DR-017 commits the team
to a full RPG (eight NPCs, combat, inventory, quests, levelling, save/load, an asset
pipeline) and lives only on a branch. It is written carefully and changes no settled rule,
but it is the largest scope decision in the repository and nobody else has ratified it.

**Fix.** Open the branch as a pull request into `test` with DR-017's status set to
"Proposed" until the team has read §4 of this review and agreed the milestone order. Say
in the DR who the six people are that it binds.

#### F26 — `Demos/Shared` is overdue and the village already has a second copy's worth — **Nit**

**Where.** `Demos/GreyBoxVillage/Assets/GreyBoxVillage/LocalShared/` (camera rig, command
input, debug overlay, launcher, player controller, UI helpers, all in namespace
`Demos.Shared`); issue #14 (blocked).

**What.** Six files are already written as shared code and namespaced as such. The second
game will copy them. The risk race plan and Living Vale both describe the same overlay.

**Fix.** Unblock #14 when the second grey box starts; promote the overlay first, since it is
the thing every demo's presentation shows.

#### F27 — Phase 4 can start measuring now, in Python, against the committed prompt files — **Should fix**

**Where.** `tools/benchmarks/target_swap_probe.py` (already renders the planned prompt and
schema and runs 130 prompts through Ollama); `UnityProject/Assets/Evaluation/prompts/`.

**What.** The eval JSON is provider-agnostic. The probe already does most of what the Phase 4
runner must do. Nothing stops a `eval_runner.py` that reads the prompt files and a scene
file (actions, targets, preconditions), builds the same prompt as #16's golden file, runs two
arms in one session, and writes the five-way classification. The in-Unity harness then has a
reference to match, and the team has numbers for the October presentation.

**Fix.** Write it with the same golden text as #16, read from one fixture both sides share,
so the Python and C# prompts cannot drift. Its first job: few-shot on versus off, and the
`dialogue` versus `statement` rename, both owed in `findings.md`.

#### F28 — The schedule risk in one sentence — **Question**

Nine weeks; Phases 1 to 4 owed; two new games planned whose first four milestones each need
nothing from the framework. The plan's rule is that demos are the integration tests. The
cheapest insurance is F18 (every demo runs through `Agent` from its first day) and F24
(start the demo that proves the thesis). The cut list the plan already names (polish first,
then `GreyBox2D`, never `GreyBoxStrategy`, never Phase 4) should be re-read with Living Vale
and the risk race added to it, in that order after `GreyBox2D`.

#### F29 — CI compiles nothing, and GameCI is deferred to Phase 3 — **Should fix** sooner

**Where.** `.github/workflows/repo-hygiene.yml` header; `REVIEWING.md` ("the reviewer's
Unity is the only compiler between a branch and `test`"); build plan §6.3.

**What.** Every pull request's build is checked by one teammate opening Unity. With six
people and a two-project repository (dev project plus one game so far), a merge that breaks
`test` is a matter of time, and the "a green check is not a review" warning is doing a
compiler's job.

**Fix.** Either GameCI with a personal licence activation file in a repository secret (an
afternoon, documented widely), or a self-hosted runner on the RTX 5080 machine that already
serves the asset server: it runs the EditMode tests headless with the command
`environment.md` already gives. Phase 2 at the latest, before the provider lands.

#### F30 — The licence is unresolved and the repository is public — **Question** with a date

**Where.** `README.md` ("all rights reserved ... published for review only"); DR-007;
`package.json` has no `license` field.

**What.** Phase 5's definition of done is a stranger building an agent in fifteen minutes.
A stranger cannot legally use the code. The IP check has been pending since September.

**Fix.** Put a date on it in the issue tracker and ask the advisor by that date. Add the
`license` field when it is known.

### 3.7 Documentation drift and small errors

#### F31 — `.env.example` still says `localhost` — **Should fix**, one line

**Where.** [`.env.example`](../../.env.example) line 17 (`OLLAMA_BASE_URL=http://localhost:11434`).
`findings.md`, `providers.md`, the probes and `AGENTS.md` all say `127.0.0.1` and explain
the 2 s cost. The template is the one file a new teammate copies.

#### F32 — The README status paragraph is stale — **Nit**

**Where.** `README.md`, the *Status* callout ("the agent, its action registry and the schema
do not" exist). `ActionRegistry`, `ActionAvailability`, the code front door and target
discovery have merged since. `AGENTS.md`'s "Current state" is accurate but duplicates
`CHANGELOG.md`.

**Fix.** Point the README at the changelog and keep one list. `AGENTS.md`'s list is worth
keeping because agents read it, but it should say it is a summary of the changelog.

#### F33 — The risk-race plan describes the guard DR-016 replaced — **Nit**

**Where.** [`docs/demos/risk-race.md`](../demos/risk-race.md) line 246 ("The guard checks
that the chosen target is named in the stimulus").

**What.** That is the lexical name check, dropped on 5 October. Under DR-016 the guard reads
the model's probability, so the question the paragraph asks (what the guard adds when the
game wrote the stimulus) has a different and better answer: it still catches a swap, and
the findings show it refused no good game-triggered decision.

#### F34 — Code nits, one line each

- `Runtime/Core/DecisionTelemetry.cs`: brace on the namespace line, "proviced",
  "orperations", comments where the rest of the package uses XML docs.
- `Runtime/Core/DecisionOutcome.cs`: no XML docs; the values are quoted by the eval report.
- `Runtime/Core/AgentContext.cs`: `Identity{ get; }` spacing, six times.
- `Runtime/Core/AgentDecision.cs`: "never can be null" → "never null".
- `package.json`: no `license`, `documentationUrl`, `changelogUrl`, or `samples` array
  (the plan's §1.8 asks for the last; harmless until Phase 7).
- `Demos/GreyBoxVillage/Packages/manifest.json`: the MCP for Unity git dependency is a
  development tool every teammate downloads when they open the game. It is pinned, which
  is right; consider whether it belongs in a committed manifest at all.
- `Demos/GreyBoxVillage/.../VillageCommands.cs`: uses both `OnGUI` and UI Toolkit; fine for
  a grey box, worth one UI system before it is copied to `Demos/Shared`.

---

## 4. Living Vale, read as a use case

The plan is on `codex/rpg-demo-plan`:
[`living-vale.md`](https://github.com/agenerela/agenerela/blob/codex/rpg-demo-plan/docs/demos/living-vale.md),
[`living-vale-design.md`](https://github.com/agenerela/agenerela/blob/codex/rpg-demo-plan/docs/demos/living-vale-design.md),
[`living-vale-assets.md`](https://github.com/agenerela/agenerela/blob/codex/rpg-demo-plan/docs/demos/living-vale-assets.md).

### 4.1 What it gets right about the framework

It is the most demanding use case written so far, and it respects every settled rule
without being told to:

- **Trade through chat without a new schema field.** Typed game-owned targets (stock
  entries, sale bundles, quotes, contracts) carry quantity and price; the model only picks
  which one. Confirmation is a game control, never a parsed "yes". Purchases are atomic and
  idempotent. This is the correct reading of hard rule 5 and the best worked example of the
  typed-target idiom (F9) in the repository.
- **Statements are never authoritative.** Prices in prose are ignored; a guard's rewrite is
  replaced by a game-authored line (§9, §12), which is exactly §2.5's instruction.
- **Stale decisions are rejected by generation and revision checks** (§12), which is the
  game-side half of F5.
- **It lists the framework questions it exposes** (§16) instead of working around them
  silently, and six of the seven are real: execution receipts (F3), cancellation outcome
  (F2), action-target pairing (F8), memory after a failed execution (F3, F5), memory
  persistence (F6), the empty-target contract (F1). The plan is a better bug report against
  the framework than most issues.
- **Scripted mode is labelled as a fixture** and must never be presented as model-driven.
  F18 makes that enforceable rather than a promise.
- **It separates deterministic invariants from model accuracy** in testing (§14) and
  refuses to assume the prototype's number transfers.

### 4.2 What it exposes that the framework should absorb

| Living Vale invents | Framework should provide | Finding |
|---|---|---|
| `InteractionReceipt` | `ExecutionResult` returned by `Execute` | F3 |
| Conversation generation and revision checks | `Execute` re-checks on a fresh context; result carries its context | F5 |
| Per-handler type and owner checks | Per-action target lists in the schema, categories on definitions | F8 |
| "Clear rolling history on load" | `IMemoryStrategy.Clear`, `Export`, `Import` | F6 |
| 8 s turn timeout cancelled from outside | `DecideOptions.Deadline`, `TimedOut` status | F2, F4 |
| Separate cancellation-reason log | Runtime `DecisionStatus` in telemetry | F2 |
| `ScriptedNpcDecisionDriver` | `ScriptedProvider` behind the real `Agent` | F18 |
| A target budget of 12 | A `Cap` that is configurable per source (it is) and reported (it is); the per-action enum is what keeps it small | F8, F9 |

### 4.3 Where the plan should change

1. **It is a full RPG, and four of its nine milestones need nothing from the framework.**
   G1 to G4 (movement, combat, inventory, loot, levelling, quests, save/load, scripted trade)
   is months of work before G5 connects a real decision. That inverts the plan's rule that
   demos are the integration tests, and it competes for the same six people as Phases 1 to 4.
   **Reorder:** G1 (walk and talk) → a cut-down G3 (one trader, two stock entries, one quote,
   confirmation, receipt) through `Agent` with `ScriptedProvider` → G5 as soon as
   `OllamaProvider` exists. Combat, inventory UI, levelling, side quests and save/load come
   after the conversation loop has been seen working against a model. The framework-relevant
   slice is three NPCs, trade, one quest with two resolutions.
2. **Conversation depends on memory more than any other demo**, and memory is Phase 2c and
   unmeasured. The design should say what a one-turn-memory version of the trade loop looks
   like, because that is what will exist at G5.
3. **Enum growth is the design's quiet risk.** One trader with four stock entries in three
   bundle sizes, two sale bundles, three knowledge cards, a pending quote and a quest offer is
   already past the default `Cap` of 8 and the plan's own budget of 12. Without F8 the model
   sees one list of eighteen ids for eleven actions. Build the per-NPC source windows the
   plan describes (§11) from the start, and treat F8's A/B as a prerequisite for G6.
4. **The generated-quest experiment (G6a) is the right thing to keep optional** and should
   stay behind the authored adventure. It is the one feature where "the model invents" is
   tempting, and the plan correctly keeps generation in game code.
5. **The asset pipeline doc is sound and separable.** Its exit gate (one prop, one modular
   piece, one animated outfit, one sound in a Windows build) is the right size for a pilot,
   and nothing in it blocks gameplay. Note the server in `tools/comfyui/` already selects
   models the asset doc treats as undecided (Z-Image, Pixal3D with TRELLIS.2 VAEs, Stable
   Audio 3); the two documents should agree before the pilot runs.
6. **DR-017 should be a pull request** with the team's review before it is a decision
   (F25).

### 4.4 The risk race, briefly

The plan is well targeted: it is the first demo with deadlines, concurrent requests and a
visible stale-decision re-check, and it needs the two framework features (F4, F5) that
Living Vale also needs. Its "placeholder brain" should be `ScriptedProvider` with a seeded
delay (F18), its guard paragraph is stale (F33), and its "four racers at the first fork"
question is answered by deadline ordering (F4) rather than a third priority tier. Its
seeded hazard dice are a good idea worth copying into the eval harness: a reproducible world
for an A/B.

---

## 5. Feature backlog, prioritised

Things the framework does not have and the use cases show it needs. Phase is where the
work belongs; "A/B" marks items that change what the model reads and therefore need a
control arm before they ship.

| Priority | Feature | Phase | Why | Depends on | A/B |
|---|---|---|---|---|---|
| 1 | Settle F1: `target` always present, enum `[no_target]` when empty | 1 (#8) | Two shapes otherwise | — | no |
| 2 | `ScriptedProvider` in `Runtime/Providers/` | 1 (#17) | Every demo through the real pipeline from day one; Phase 1's end-to-end test | #12 | no |
| 3 | `ExecutionResult` from `Execute`; handlers can refuse | 1 (#17) | Receipts, memory rule 1, the log window's fourth answer | — | no |
| 4 | `DecisionResult` carries its context; `Execute` re-checks on a fresh one | 1 (#17) | Stale-world safety both game plans need | #32 | no |
| 5 | Runtime `DecisionStatus` incl. `Cancelled`, `TimedOut` | 1 (#17, #3) | Deadlines; eval must exclude cancelled runs | — | no |
| 6 | Telemetry field table in the plan | 1 (doc) | Stops ad-hoc growth | — | no |
| 7 | `link.xml` and `[Preserve]` | 1 | Five minutes now, a day later | — | no |
| 8 | Python eval runner over the committed prompt files | 4-prep, now | Numbers before the Unity harness; owed A/Bs | #16 golden text | — |
| 9 | Pre-mask probability spike on LLMUnity | now | Whether DR-016 ships | — | no |
| 10 | `DecideOptions.Deadline`; EDF within tier; drop expired before send | 2b | Race, chat timeouts, DR-015's revisit | #17 | no |
| 11 | Soft target errors in player builds, `TargetsRejected` in telemetry | 2 | A typo must not stop a scene | #32 | no |
| 12 | `IMemoryStrategy.Clear/Export/Import` | 2c | Save/load | memory | no |
| 13 | `ActionDefinition.TargetCategories`; per-action target enum in `DecisionSchema` behind a switch | 3 (schema), 4 (A/B) | Action-target pairing; keeps typed-target enums small | #8, #9 | **yes** |
| 14 | Document the typed-target parameter idiom; DR deferring free fields | 1 (doc) | Living Vale found it; others will reinvent it | — | no |
| 15 | Fixture format: `targets`, `history`, `acceptable` list; second vocabulary | 4-prep (#66) | Open choices, multi-turn, per-scene accuracy | — | — |
| 16 | GameCI or a self-hosted EditMode runner | before Phase 2 ends | Nothing compiles in CI | — | no |
| 17 | `AgentContext.TryGet<T>` | 2 | Fewer casts in handlers | — | no |
| 18 | Static `Targetable` set instead of a scene scan | when profiled | Many agents, big scenes | — | no |
| 19 | `Demos/Shared` with the debug overlay first | when the second grey box starts (#14) | Already written as shared code | — | no |
| 20 | Streaming statement, batching, token-based rate limits, health check | 5, 6, 6, 2 | Already listed as extension points in `providers.md` | — | — |

---

## 6. Decision records to write

Short entries, since the plan already has the format. Each one closes a finding.

| Proposed | Decides | Finding |
|---|---|---|
| DR-018 | `target` is always present; its enum is `[no_target]` when the registry is empty | F1 |
| DR-019 | A decision has a runtime status separate from its evaluation outcome; cancellation is a result, not an exception, above the provider | F2 |
| DR-020 | `Execute` returns a result, re-checks against a fresh context, and memory records what ran | F3, F5 |
| DR-021 | A parameter is a target: typed game objects carry quantity and price; free fields are deferred and A/B-gated | F9 |
| DR-022 | Decisions carry a deadline; the queue orders by it within a tier | F4 |
| DR-023 | Target-source errors are loud in the Editor and recorded in a player | F7 |
| DR-017 | Stays as written, status *Proposed*, until ratified in a pull request | F25 |

---

## 7. Edits that can be made today

Each is under ten lines and needs no Unity.

- [ ] `.env.example` line 17: `localhost` → `127.0.0.1` (F31)
- [ ] `README.md` status paragraph: point at `CHANGELOG.md`; lead with the controlled
      numbers (F21, F32)
- [ ] `docs/demos/risk-race.md` line 246: rewrite the guard paragraph for DR-016 (F33)
- [ ] Issue #8: change the empty-registry item to "enum is exactly `[no_target]`"; issue #11
      gate 5 likewise (F1)
- [ ] Issue #17: add `ExecutionResult`, context on `DecisionResult`, status on telemetry,
      `ScriptedProvider`, and the `IsAvailable` exception policy to *What to build* (F2, F3,
      F5, F11, F18)
- [ ] Issue #10: the preferred-target fallback and omission rule (F12)
- [ ] Build plan §2.1: the telemetry field table (F16); §2.2: the typed-target idiom (F9);
      §3: split the Phase 2 row (F23)
- [ ] Package root: `link.xml` (F15)
- [ ] `DecisionTelemetry.cs`, `AgentContext.cs`, `AgentDecision.cs`: the nits in F34
- [ ] Assign #20 (F24); open `codex/rpg-demo-plan` as a pull request (F25)

---

## 8. How I reviewed

- Read in full: `AGENTS.md`, `README.md`, `CONTRIBUTING.md`, `REVIEWING.md`, the build plan
  and every decision record, `docs/llm-wiki/findings.md`, `docs/demos/risk-race.md`,
  `docs/design/README.md` and `screen-notes.md`, `Demos/README.md`, the hygiene workflow,
  the benchmark README, every `.cs` file in the package's `Runtime/`, the asmdefs,
  `package.json`, `CHANGELOG.md`, `Documentation~/providers.md`, the test file list with
  `FrontDoorTests`, `ActionAvailabilityTests` and the test names of the rest, every `.cs`
  file in `GreyBoxVillage`, its manifest, the six evaluation prompt files, and on the codex
  branch the three Living Vale documents, the resume note and DR-017.
- Read: open issues #8, #11, #16, #17 and the open-issue list.
- Not compiled, no tests run, no model run: no Unity in the review environment.
- No accuracy claim in this document is new; every number is quoted from the plan or
  `findings.md`.
