# Status

Where the build stands: what has merged, what is still to write, and the groundwork beside
it. The short version is in [`AGENTS.md`](../AGENTS.md); this page is the long one, kept out
of that file because every agent loads `AGENTS.md` into every session. The package's API
changes are also in its
[`CHANGELOG.md`](../UnityProject/Packages/com.agenerela.framework/CHANGELOG.md).

Update it when a pull request merges, in the same docs sweep that updates the build plan's
phase table.

## Phases

Phases 0 and 1 are complete: **Phase 1 (Core, Actions, Schema) closed on 11 October**, when
the five checks in #11 passed. **Phase 2 (Ollama provider, queue, memory) is underway**, due
29 October, and closes when the live checks in its gate (#94) pass. See the build plan's
phase table for what each phase means, and [`docs/phases/phase-2.html`](phases/phase-2.html)
for what each Phase 2 issue does.

## Merged so far

- The `ILLMProvider` contract and its supporting types (#12), with its follow-up (#67, via
  #68): token probabilities for the grounding guard (DR-016).
  `ProviderCapabilities.ReportsTokenProbabilities` is set only for probabilities taken before
  the schema's mask, and `ProviderResult.TokenProbabilities` carries them, one per token,
  with EditMode tests. No provider fills them yet.
- `TargetRegistry` (#5), with its follow-up (#29, via #55): `no_target` is reserved, and
  lookups return false rather than throw on a malformed id, so a provider's raw output can
  go straight in.
- `ActionDefinition` and its `ActionDefinitionAsset` wrapper (#4, via #35 and #37), with its
  follow-up (#49, via #54): the `Description` and `ExampleStimulus` tooltips and the
  `ValidateId` tests.
- Decision telemetry (#3, via #36): `DecisionOutcome` and `DecisionTelemetry`, with EditMode
  tests, and `DecisionResult`, which pairs a decision with its telemetry. Its follow-up (#43,
  via #60) added `DecisionResult`'s EditMode tests.
- The core decision types `AgentIdentity`, `AgentContext` and `AgentDecision` (#2, via #39),
  with input validation and EditMode tests. `AgentContext.Actions` followed with
  availability (#58, via #63).
- `AgentProfile` (#15, via #56) — the asset a developer creates first: an `AgentIdentity`,
  the `ActionDefinitionAsset`s the agent can ever use, and the idle few-shot example. It
  warns in the Inspector about an empty name, a missing entry or a duplicate action id, with
  EditMode tests.
- `ActionRegistry` and `IActionHandler` (#6, via #57) — each action id bound to the game code
  that runs it, listed in a stable order for the schema enum. `Register` rejects a null,
  duplicate, malformed or reserved id (`none`, which #8 adds to every agent); lookups return
  false rather than throw. Two adapters come with it: `DelegateActionHandler` for a pair of
  lambdas, and `ActionHandlerBehaviour`, the first file in `Runtime/Unity/`. EditMode tests
  throughout.
- The `[AgentAction]` code front door (#50, via #59) — an action defined by the method that
  runs it, with `[Example]` for its few-shot example and `[Available]` for state masking.
  `ActionRegistry.RegisterMethods` registers every such method on an object; where an asset
  shares the id, the asset's definition wins and the method still runs it (DR-011). One
  parameterized fixture runs the same assertions through every way an action is registered.
- Target discovery (#32, via #61, DR-014): `ITargetSource`, with `ExplicitTargetSource` for a
  hand-supplied set that needs no scene, and in `Runtime/Unity/` the `Targetable` component
  and `ProximityTargetSource`, which offers nearby `Targetable`s nearest first, capped, each
  registered as its `Transform`. What the cap drops is on its `Dropped`, which `Agent` copies
  into `DecisionTelemetry.TargetsDropped` (#81, below).
- State-derived availability (#7, via #63): `ActionAvailability.For(ctx)` returns the actions
  legal for one decision, in registry order. A target-requiring action is left out while the
  `TargetRegistry` is empty, and so is any action whose handler's `IsAvailable` says no.
  Recomputed on every call, never cached. The same pull request added `AgentContext.Actions`
  (#58), the registry it reads. EditMode tests throughout.
- The few-shot builder (#10, via #74): `FewShotBuilder.Build` assembles the example block
  from the available actions alone. Each action gets one example, its `{0}` filled with the
  first of its `PreferredExampleTargets` that is registered, or no example if none is. Then
  come the profile's idle example, answered with `none`, and a refusal example: a real
  `ExampleStimulus` filled with an unregistered placeholder (`Atlantis`). EditMode tests
  throughout. The "Pick up the Blacksmith" regression test registers the unsuitable target
  first, so it fails if examples rotate blindly through the registry (#75, via #83).
- `DecisionSchema` (#8, via #78): the provider-neutral shape of the answer, `action`, then
  `target`, then `statement`, all required. The action enum is the available actions in
  registry order with `none` added last; the target enum is `no_target`, then the registered
  ids, and the `target` field is left out while nothing is registered. `DecisionRequest.Schema`
  carries it unserialised, for each provider to serialise in its own dialect. One EditMode
  test per measured rule.
- `JsonSchemaSerializer` (#9, via #82): a `DecisionSchema` written as the JSON Schema Ollama
  and the cloud APIs accept: one string property per field in order, an `enum` for each
  field with allowed values, `required`, `propertyOrdering` always (Gemini needs it) and
  `additionalProperties: false`. A null, unnamed or duplicate field, or an empty, `""` or
  duplicate allowed value, throws rather than reaching a provider. Golden-file tests,
  including one through `DecisionSchema.Build`. Phase 2's Ollama provider is its first caller.
- `PromptBuilder` (#16, via #79): an agent's context made into the `DecisionRequest` — a
  system prompt of identity, goals and one fixed instruction line, the few-shot block as
  given, the observations verbatim, and the labelled stimulus as the final user turn.
  Deterministic, `\n` only, with golden-file tests.
- `Agent` (#17, via #80): `DecideAsync` builds the context, asks availability once, builds
  the schema, examples and request, calls the provider and reads the reply with Newtonsoft.
  An unreadable reply or a provider failure becomes a `PipelineError` result, never an
  exception; only an empty label and the caller's own cancellation throw. `Execute` re-checks
  the action and target and returns false on a refusal. Proven end to end against
  `FakeProvider`. Every async test awaits through `Fixtures.NotCancelled`, because the Test
  Framework counts a test whose Task ends cancelled as passed.
- The Phase 1 gate (#11, via #84): `Phase1GateTests`, the build plan's five checks in one
  file: state masking, `target` required with `no_target`, the field order, few-shot
  examples that use only sensible registered targets, and no `target` field while nothing is
  registered. Each runs through `Agent.DecideAsync` and `FakeProvider` and asserts on the
  recorded request and its serialized JSON, and each failure message names the measured rule
  that broke. A throwaway probe that switched each rule off failed exactly its own gate.
- `TargetsDropped` filled (#81, via #96), the first Phase 2 merge: `IReportsDroppedTargets`,
  an optional interface beside `ITargetSource`, which `ProximityTargetSource` implements.
  `Agent` copies each such source's `Dropped` into the decision's telemetry right after its
  `Collect`, sources in order; `Execute` resolves the targets again without touching a
  returned result. EditMode tests, including a capped proximity source end to end.
- Typed provider errors (#87, via #101): a provider throws `ProviderException` with a
  `ProviderErrorKind` (`Unreachable`, `ModelNotFound`, `Timeout`, `Rejected`, `ServerError`,
  `MalformedResponse`) and its `ProviderName`. Every `PipelineError` result now records why
  in `DecisionTelemetry.ErrorKind`, a `PipelineErrorKind` that maps each provider kind one to
  one, plus `RequestPreparation`, `UnreadableReply`, and `ProviderFailed` for any other
  exception or no result. `ErrorMessage` holds the warning `Agent` logged. EditMode tests,
  one per kind, through `Agent.DecideAsync`.
- `OllamaProvider` (#88, via #102), the first provider that calls a model: `POST /api/chat`
  over `HttpClient` with `JsonSchemaSerializer`'s output in `format`, greedy decoding,
  `think: false`, `num_ctx` 4096 and five top log-probabilities, at `http://127.0.0.1:11434`
  by default (`OllamaSettings`). The reply's text, token counts and token probabilities fill
  `ProviderResult`, and its model fills the new `ProviderResult.Model` and
  `DecisionTelemetry.Model`. Each failure throws its #87 `ProviderException` kind. 47 EditMode
  tests run with no Ollama installed, through an `HttpMessageHandler` seam; one live decision
  on `qwen3.5:2b` with Ollama 0.34.2 was checked by hand. WebGL players are not supported. Open
  for later issues: `DecisionTelemetry.LatencySeconds` still comes from `Agent`'s stopwatch,
  not the provider's (#89 starts it at the send), and the provider is not
  `IDisposable` (#92 creates and replaces providers).
- The request queue and `DecideOptions.Priority` (#89, via #103, DR-015): every provider call
  goes through an internal `RequestScheduler`, one per provider instance, found by the instance
  itself, so agents sharing a provider share its queue and agents on different providers never
  wait on each other. At most one request in flight (`MaxInFlight`, internal until #92 sets it),
  `PlayerFacing` (the default) before `Background`, first come, first served within a tier.
  Nothing already sent is aborted, and nothing is retried. A request cancelled while queued is
  never sent, and a failed one frees its slot. Telemetry adds `Priority`, `QueueWaitSeconds` and
  `QueuedAhead`, and `LatencySeconds` is timed from the send, so it stays the provider's time
  alone. Every provider call starts on the main thread, even when the slot was freed on a
  worker thread. 36 EditMode tests, all holding and releasing requests by hand rather than
  timing them.
- Short-term memory (#90, via #104, build plan §2.9): `IMemoryStrategy` (`Recall(ctx)`,
  `Remember(ctx, request, result)`) and its default, `RollingHistory(turns: 6, maxTokens: 256)`,
  on `Agent.Memory`, a new one for each agent and never one on the profile; null turns memory
  off. `DecisionRequest.History` now carries the newest exchanges that fit, oldest first, two
  entries each: the stimulus as it was sent, then the decision as the schema's JSON. Its remarks
  document that encoding, which `OllamaProvider` already sent. Tokens are estimated as
  characters over four, and the oldest exchange goes first, whole, under either cap. What is
  remembered is the decision `DecideAsync` returns, never the raw reply; a pipeline error or a
  cancelled call remembers nothing, and remembering happens on the main thread even when the
  provider answered on another. Telemetry adds `HistoryTurnsSent`, `HistoryTurnsDropped` (what
  this request's token cap left out) and `HistoryTurnsForgotten` (the rising total the turn cap
  let go of), filled through the optional `IReportsDroppedTurns`. 30 EditMode tests. The
  256-token default is a starting point for Phase 4 to measure.
- Newtonsoft as the core's one declared dependency (DR-009), and each demo game as its own
  Unity project (DR-010).
- `Demos/GreyBoxVillage` (#19, then via #112) — the evaluation agent's world as a playable
  grey-box village, whose guard is driven by `qwen3.5:2b` through `OllamaProvider`. The game
  decides when to ask (a player line, a trespass, a wolf, a quiet moment) and which actions are
  offered by who is asking, so chat never gets an unprompted `patrol`. A request history panel
  and a JSONL log show exactly what was sent and what came back. Only the public API is used.
- The first 54 evaluation prompts (#13, via #65), nine in each of six categories, in
  `UnityProject/Assets/Evaluation/prompts/`. Its follow-up (#66, via #76) added `agent.json`,
  the guard every prompt is measured against, so every `expectedTarget` is a registered id,
  and rewrote the observations as facts about the world. The folder's `README.md` documents
  the format and the category balance, and `findings.md` how annotator disagreements get
  resolved. Still open, in #85: the real annotators' names and their own answers on the 18
  doubly labelled prompts, and the next batch of prompts.
- `Repo hygiene` checks for the hard rules a script can check, including "only
  `Runtime/Unity/` touches a scene" (#38, #40), for a changed `.meta` GUID (#41), and for a
  binary file outside Git LFS. Every check is in `tools/ci/hygiene.py`, which also runs on a
  laptop and as a pre-commit hook. It still compiles no C#, so a green check does not prove
  `test` builds.

## Not written yet

All of it Phase 2: the gate, a live decision in a sandbox scene with `RequiresOllama`
PlayMode tests (#94), which can start now that #87, #88, #89 and #90 have all merged. Beside
it, and not on the gate's path: observation sources (#91), the provider config asset (#92)
and `AgentBehaviour` (#93).
**The framework can now call a model**, through `OllamaProvider` (#88), but no test does yet:
the first live decision under test is the gate's PlayMode test (#94). Phase 1 was pure C#
with EditMode tests and no Ollama, on purpose.

## Groundwork beside the phases

Open beside Phase 2: the evaluation set's real annotators and next batch of
prompts (#85), and the demo games, one sub-issue each under #47, starting with the
GreyBoxStrategy grey box (#20). Code two games share moves into `Demos/Shared/` once a second
game needs it (#14). The asset server those games draw on (`tools/comfyui/`) is set up on the
RTX 5080 machine. Its first run there found four faults, fixed in #71, and all three
workflows have made a test asset; #72 moved the image workflow to Z-Image Turbo's NVFP4
build, about twice as fast there.

What all of this is meant to become, screen by screen, is drawn in
[`docs/design/`](design/README.md).
