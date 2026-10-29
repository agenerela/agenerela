# Changelog

All notable changes to this package are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

While the version stays `0.x`, the public API may change in any release.

## [Unreleased]

## [0.4.0-preview.1] - 2026-10-29

### Added

- **Provider config assets** (#92, DR-013). A provider is now chosen once, as an asset, rather
  than built in every agent's code. `ProviderConfig` is the abstract `ScriptableObject` every
  backend's config derives from: `Provider` builds its provider once and returns the same
  instance after, so every agent on one config shares one provider and one queue, and
  `MaxInFlight` (default 1) sets that queue's limit. `OllamaProviderConfig`, made with
  **Create → Agenerela → Ollama Provider**, holds `OllamaSettings`' fields with the same
  defaults, `127.0.0.1` included.
- **The project default.** **Project Settings → Agenerela** names the provider config every agent
  without one of its own uses, saved in `AgenerelaSettings`, which the page creates at
  `Assets/Agenerela/Resources/AgenerelaSettings.asset` and a built player loads with
  `Resources.Load`. `ProviderConfig.Resolve(own)` returns the agent's own config, else the
  default, else throws `InvalidOperationException` saying how to make one:
  `new Agent(profile, ProviderConfig.Resolve().Provider)`. A scene override, DR-013's middle
  level, is left out until a demo needs one.
- `DecisionTelemetry.ProviderConfig`: the name of the config whose provider answered, empty for a
  provider built in code.
- **The guard pipeline** (#106, build plan §2.5). Every decision `DecideAsync` parses now
  passes through ordered guards before it is returned or remembered. `IDecisionGuard` judges
  a `GuardInput` (the context, request, reply, decision, the call's options and the
  telemetry) and returns a `GuardVerdict`: `Pass`, `RewriteToNone(reason)` or
  `Reject(reason)`. The first verdict that is not `Pass` stops the pipeline, and the decision
  becomes `none` with `no_target`. A reject is logged as a warning, and a rewrite is not.
- **`SchemaLegalityGuard`**, the first built-in guard. It checks that the action is registered
  and was offered, and that the target is registered or `no_target`. It also adds a new rule:
  an action that needs a target got a real one. `move_to / no_target` used to run with no
  target, and is now rejected.
- `DecisionResult.ModelDecision` (what the model answered) and `DecisionResult.Verdict` (the
  guard's name, the kind and the reason), beside `Decision`, which is what the agent will do.
- `DecisionTelemetry.Verdict` and `DecisionTelemetry.GuardReason`. `GuardsFired` is now
  filled in.
- **Observation sources** (#91, DR-012, build plan §2.8). An agent can now notice its
  surroundings rather than only be told. `IObservationSource.Observe(AgentContext)` returns
  short lines. `Agent.ObservationSources`, an ordered list that is empty by default, is asked
  on every decision after the targets are resolved, and nothing is kept between decisions. The
  sources' lines are sent verbatim, before the call's own `DecideOptions.Observations`. While a
  source runs, its context holds the call's own lines, never another source's. A source that
  throws, or a null entry, makes the decision a `PipelineError` of kind `RequestPreparation`,
  as a target source does. `Execute` never asks the sources.
- **`Agent.MaxObservations`**, the cap in lines, 8 by default. The call's own observations are
  always sent whole and count against it, and the sources fill what is left, in order. Blank
  lines take no place. An agent with no sources sends exactly what it sent before.
- `DecisionTelemetry.ObservationsDropped`: the lines a source had but the request does not
  carry, in source order. For each source, it lists the lines the cap cut, then the lines that
  source's own cap left out, which a source reports through the optional
  `IReportsDroppedObservations`, the same shape as #81's `IReportsDroppedTargets`.
- **`ProximityObservationSource`**, in `Runtime/Unity/`. It returns the `Description` of each
  `Targetable` within a radius, nearest first, with its own `Cap` (8). A `Targetable` that is
  not currently targetable, or has an empty description, adds nothing. It takes the same
  options as `ProximityTargetSource` and shares its query, so given the same settings the two
  find the same objects.
- **`TargetGroundingGuard`**, the second built-in guard (#105, DR-016). It catches a model
  that wanted a target not on the list: "Attack the scarecrow." with only a training dummy
  around now comes back as `none`, not as an attack on the dummy. It reads the reply's token
  probabilities at the target and keeps the lowest share that could still lead to a listed
  target. Below 0.8, a decision whose action needs a target is rewritten to `none` with
  `no_target`, and the reason names the share and what the model wanted to write. It runs on
  every decision. A reply without token probabilities passes, recorded as not checked.
- `TargetGrounding.Read`, the reading on its own, as a pure function returning a
  `GroundingReading` (`Status`, `Share`, `LowestAt`, `Wanted`). It matches the probe's
  `target_swap_score.py` number for number on ASCII ids, which are all DR-016 measured. An id
  containing the Greek capital sigma or the Turkish "İ" can read differently, because the
  probe used Python's lower-casing.
- `DecideOptions.CheckTarget`, `true` by default. Set it to `false` to skip the grounding
  guard for one call.
- `DecisionTelemetry.TargetGrounding` (the share, or null when it was not read) and
  `DecisionTelemetry.TargetGroundingStatus` (a `GroundingStatus`: `Scored`,
  `NoProbabilities`, `NoTargetField`, `Unreadable` or `Off`; null when the decision never
  reached the guard).
- **The target rule**, the grounding guard's fallback for a provider that reports no token
  probabilities (#107, DR-016). When `DecideOptions.CheckTarget` is on and the provider's
  `Capabilities.ReportsTokenProbabilities` is false, the system prompt gains one line after
  the instruction line: "Only these things are here: tower, training_dummy. If you are asked
  to act on anything else, choose action none and target no_target, and say it is not here.
  Never act on a different thing instead." It names the schema's target ids in order, and is
  left out when the schema has no target field. The wording adapts the measured rule and is
  itself unmeasured; Phase 4 owes the A/B. With Ollama, which reports probabilities, the
  prompt is unchanged. A provider that claims probabilities and then returns none still gets
  neither the rule nor the guard.
- `PromptOptions.TargetRule`, off by default, which `Agent` sets, and
  `DecisionTelemetry.TargetRuleSent`, true exactly when the line was sent.
- **An MIT licence.** `LICENSE.md` ships in the package folder, so a git-URL install carries it,
  and `package.json` says `"license": "MIT"`. Keep the notice with the package when you ship a
  game; nothing else is asked.

### Changed

- `Execute`'s re-check is now `SchemaLegalityGuard`, run against the agent as it is when
  `Execute` is called. A refusal there adds `SchemaLegalityGuard@Execute` to `GuardsFired`, and
  sets `Verdict` and `GuardReason`. It still returns false and logs a warning saying why.
  `Execute` also returns false for a decision a guard rejected inside `DecideAsync`, without
  logging it a second time.
- An answer naming an action that was not offered, or a target that is not registered, is now
  rejected inside `DecideAsync`. Before, it came back as that decision and only `Execute`
  refused it. Memory remembers it as `none`.

## [0.3.0] - 2026-10-25

Phase 2: Ollama provider, queue, memory. The first release that calls a model: an agent
decides through `OllamaProvider` against a local Ollama, one request at a time per provider
with the player's question first, remembering its last few exchanges, and a failed request
comes back as a typed reason rather than an exception. The Phase 2 gate's `RequiresOllama`
PlayMode tests prove the live path (#94); a warm decision on `qwen3.5:2b` took 0.23 to 0.56 s
on the machine they ran on. In the repository, not the package, `Demos/GreyBoxVillage` is a
playable village whose guard this release drives.

**Early development: not ready for a game yet.** Major parts are still missing:

- **The validation guards** (Phase 3, #105 to #110). `Execute` already refuses an action that
  is not available and a target that is not registered, but nothing yet catches a legal
  answer that is wrong: a substituted target, an action that needs a target answered without
  one, or a low-confidence pick.
- **Any measured accuracy** (Phase 4). No number from this framework says how often it chooses
  right; the ones in the docs are the predecessor prototype's.
- **All editor UI** (Phase 5): agent setup, the New Agent wizard and the Decision Log window.
- **Setup without code**: the provider config asset (#92), `AgentBehaviour` (#93) and
  observation sources (#91). An agent is still built in code.
- **Any provider but Ollama** (Phases 6 and 6b), so Ollama must be running, and a built
  player cannot decide on its own.

The public API will change in the releases to come.

### Added
- `ProviderException` and `ProviderErrorKind` in `Agenerela.Providers` (#87) — what a provider
  throws when a request fails, with a `Kind` (`Unreachable`, `ModelNotFound`, `Timeout`,
  `Rejected`, `ServerError`, `MalformedResponse`) and the failing provider's `ProviderName`, so
  a model that was never pulled is told apart from a server that is not running. Its message
  says what to do next where the provider can tell, and never contains an API key.
- `PipelineErrorKind` in namespace `Agenerela`, and `DecisionTelemetry.ErrorKind` and
  `DecisionTelemetry.ErrorMessage` (#87). Every `PipelineError` result now says why it failed:
  `RequestPreparation`, `ProviderUnreachable`, `ModelNotFound`, `ProviderTimeout`,
  `ProviderRejected`, `ProviderServerError`, `ProviderMalformedResponse`, `ProviderFailed` or
  `UnreadableReply`, with the warning `Agent` logged as its message. Each `ProviderErrorKind`
  maps to its own kind; `ProviderFailed` means only an exception that is not a
  `ProviderException`, or a provider returning no result. Both fields are null on a decision,
  and round-trip through Newtonsoft.
- `OllamaProvider` and `OllamaSettings` in `Agenerela.Providers` (#88) — the first provider
  that calls a model: Ollama's `/api/chat` on the developer's machine, at
  `http://127.0.0.1:11434` by default, sending the schema in `format`, greedy decoding,
  `think: false`, `num_ctx` 4096 and `logprobs` with five alternatives, as every number in
  Appendix A was measured. It returns `message.content` exactly as received, the token
  counts, the token probabilities and the model that answered, and throws
  `ProviderException` with the kind that fits. Built on `System.Net.Http.HttpClient`, so it
  needs no new package; it does not work in a WebGL player.
- `ProviderResult.Model` and `DecisionTelemetry.Model` (#88): the model that produced a
  reply, which `Agent` copies from the one to the other. Empty in telemetry when the provider
  does not say.
- Short-term memory (#90, build plan §2.9): `IMemoryStrategy`, with `Recall(ctx)` and
  `Remember(ctx, request, result)`, and its default, `RollingHistory(turns: 6, maxTokens: 256)`,
  the last exchanges oldest first. Past either cap the oldest exchange goes first, a whole one at
  a time; tokens are estimated as characters over four. `Agent.Memory` is a new `RollingHistory`
  for each agent, never one on the profile, and null turns memory off. The agent recalls into
  `DecisionRequest.History` while it prepares a request and remembers the decision it returns,
  as parsed, never the raw reply; a pipeline error or a cancelled call remembers nothing. It
  remembers on the main thread, even when the provider answered on another.
- `DecisionTelemetry.HistoryTurnsSent`, `HistoryTurnsDropped` and `HistoryTurnsForgotten`
  (#90), and the optional `IReportsDroppedTurns` a memory strategy implements to fill the other
  two. Dropped is what this request's token cap left out of the exchanges still held, so above
  0 means the budget is binding; Forgotten is the rising total the turn cap let go of.
- `DecisionPriority` (`PlayerFacing`, `Background`) and `DecideOptions.Priority` in namespace
  `Agenerela` (#89, DR-015). Every `Agent` now sends through a queue in front of its provider
  instance, shared by every agent given that instance: one request in flight at a time,
  player-facing before background, first come, first served within each. Player-facing is the
  default, so nothing changes for a caller who never sets it. A request already sent is never
  aborted; one cancelled while queued leaves the queue at once and never reaches the provider;
  a failed request frees its place, and nothing is retried. Agents on different provider
  instances never wait on each other.
- `DecisionTelemetry.Priority`, `QueueWaitSeconds` (from joining the queue to being sent) and
  `QueuedAhead` (requests in flight or queued ahead of it when it joined) (#89).
- `RequiresOllama` PlayMode tests in a new `Agenerela.Tests.Runtime` assembly (#94): a live
  round trip under 5 s after warm-up, its telemetry filled in, a model that was never pulled
  reported as `ModelNotFound`, and history reaching the second request. They are ignored,
  with the reason, when Ollama is not reachable. `AGENERELA_OLLAMA_MODEL` and
  `AGENERELA_OLLAMA_URL` choose the model and endpoint.

### Changed
- `DecisionRequest.History` now documents its encoding (#90): two entries per exchange, the
  stimulus as it was sent and then the decision as the schema's JSON on one line.
  `OllamaProvider` already sent it this way, as `user` then `assistant` messages.
- `ILLMProvider.RequestAsync`'s remarks and `Documentation~/providers.md` now point at
  `ProviderException` (#87) as what a provider throws; providers.md's Ollama section says what
  was built (#88).
- `DecisionTelemetry.LatencySeconds` is timed from the moment the request is sent, so it stays
  the provider's time alone and a wait in the queue shows in `QueueWaitSeconds` instead (#89).

## [0.2.0] - 2026-10-18

Phase 1: Core, Actions, Schema. The whole decision pipeline, from registered actions and
targets through state masking, the schema, few-shot examples and the prompt to a validated
`AgentDecision`, proven end to end against a fake provider. No provider calls a model yet;
the first, for Ollama, is Phase 2's.

### Added
- `ActionDefinition` and `ActionDefinitionAsset` in namespace `Agenerela` — the
  developer-authored action vocabulary (#4). `ActionDefinition` is the plain serializable type
  the registry, schema and few-shot builders consume; `ActionDefinitionAsset` is the
  `ScriptableObject` a developer creates from **Create → Agenerela → Action** (DR-011).
  `ActionDefinition.ValidateId` rejects an empty id, uppercase (compared culture-invariantly)
  and any whitespace, and the asset warns about a malformed id while it is being edited.
  `ValidateId` has EditMode tests. The `Description` tooltip asks for one short when-to-use
  clause, since it is the text the model chooses by, and the `ExampleStimulus` tooltip says
  to use `{0}` for the target and never to reuse an evaluation prompt (#49).
- `TargetRegistry` in namespace `Agenerela` — what an agent is allowed to refer to, held as
  ids the schema can offer and the guards can check: `Register`, `Unregister`, `TryGet`,
  `Contains`, `Ids` and `Count`, with EditMode tests. `TargetRegistry.NoTarget`
  (`no_target`) is the schema's "no target" value and can never be registered. `Register`
  throws for a malformed, reserved or duplicate id and for a null target; `TryGet`,
  `Contains` and `Unregister` return false for any id that is not registered, malformed ones
  included, and never throw, so a provider's raw output is safe to pass in. `Ids` is a live
  read-only view in insertion order, which the schema enum is built from (#29).
- `ILLMProvider` and its supporting types (`ProviderCapabilities`, `SchemaDialect`,
  `RateLimit`, `DecisionRequest`, `ProviderResult`) in `Agenerela.Providers` — the contract
  every backend satisfies, defined before any of them is written so that Ollama, cloud APIs
  and in-process inference are implementations rather than framework edits. No
  implementation yet; `DecisionRequest.Schema` lands with `DecisionSchema`.
- `Documentation~/providers.md`: how each planned backend answers the contract, what a
  second cloud vendor would need, and which parts are deliberately still open.
- `DecisionOutcome`, `DecisionTelemetry` and `DecisionResult` in namespace `Agenerela` (#3) —
  the outcome classification a decision gets scored against (`Correct`, `WrongLegalAction`,
  `ContainedByGuard`, `RejectedWhenActionExpected`, `PipelineError`) and the record it is
  written into: latency, prompt/completion tokens, provider name and fired guards. EditMode
  tests cover the enum's five names and order, a freshly-constructed `DecisionTelemetry`, and
  a Newtonsoft round trip. `DecisionResult` pairs an `AgentDecision` with its telemetry, and
  allows a null decision only for a `PipelineError`. Its EditMode tests cover that rule for
  every outcome and for a null one, the objects stored as given, and a null telemetry (#43).
- `AgentIdentity`, `AgentContext` and `AgentDecision` in namespace `Agenerela` (#2) — who an
  agent is, what it knows at the moment of a decision, and what it chose. `AgentDecision`
  rejects a null or whitespace action or target id but accepts ids that are not registered,
  so the guards can inspect them. `AgentContext` carries the agent's `TargetRegistry` and its
  `ActionRegistry` (`Actions`, #58), rejects a null identity, target registry or action
  registry, and turns a null stimulus, observations or state into an empty value. EditMode
  tests cover construction, the rejected ids, a null identity, a null action registry, and
  null observations, state and statement.
- `AgentProfile` in namespace `Agenerela` (#15) — the `ScriptableObject` a developer creates
  from **Create → Agenerela → Agent Profile**, shared by every agent that uses it: an
  `AgentIdentity`, the `ActionDefinitionAsset`s the agent can ever use (held as references,
  so editing an action asset changes every profile that lists it, DR-011), and
  `IdleExampleStimulus`, the stimulus for the few-shot example whose answer is `none`
  (default `"Nothing has changed since last time."`; empty omits the example).
  `AgentProfile.Validate` reports an empty identity name, a null entry in `Actions` or the
  same action id listed twice, and the asset logs the problem as a warning while it is being
  edited. Blank action ids are left to the action asset's own warning. EditMode tests cover
  each case.
- `IActionHandler` and `ActionRegistry` in namespace `Agenerela` (#6) — the binding from each
  action to the game code that runs it. A handler answers `IsAvailable(ctx)`, whether the
  action is legal right now, and `Execute(ctx, decision)`, the game's own code, reached only
  after every guard has passed. `ActionRegistry.Register` pairs an `ActionDefinition` with its
  handler, and throws for a null definition or handler, a duplicate id, an id
  `ActionDefinition.ValidateId` rejects, and `ActionRegistry.None` (`none`), which is reserved
  for the idle action the schema adds to every agent. `TryGet` returns false for any id that
  is not registered, null and malformed ones included, and never throws; `HandlerFor` throws a
  `KeyNotFoundException` naming the id for a definition that was never registered.
  `Definitions` is a live read-only view in insertion order, which the schema enum is built
  from. The registry keeps the developer's own definition, so an id changed after
  registering is not picked up (build plan §2.2). Two adapters implement `IActionHandler` for
  a developer who would rather not write a class per action: `DelegateActionHandler`, from a
  pair of delegates, and `ActionHandlerBehaviour`, an abstract `MonoBehaviour` in
  `Runtime/Unity/` that the Inspector can reference (DR-011). EditMode tests cover each rule.
- The code front door in namespace `Agenerela` (#50, DR-011) — an action defined by a method in
  the game's own script, with no asset. `[AgentAction(id, description)]`, with `RequiresTarget`,
  makes a method an action and the code that runs it; `[Example(stimulus, preferredTargets)]`
  gives it a few-shot example; and `[Available(actionId)]` marks a method returning bool, taking
  nothing or an `AgentContext`, as its `IsAvailable`. Without one the action is always
  available. `ActionRegistry.RegisterMethods(owner, assets)` registers every `[AgentAction]`
  method on `owner`, private and inherited ones included, base class first and then in
  declaration order. Each parameter is filled by its type: an `AgentContext`, an
  `AgentDecision`, and at most one target, the object registered under the decision's target
  id, passed as registered and never converted, or null for `no_target`, which a value-type
  parameter such as an `int` refuses. Where one of `assets` shares a method's id, the asset's
  definition is registered and the method still runs it: the asset wins (DR-011). The owner's
  actions are registered all together or not at all: a malformed method, attribute or id throws
  an `ArgumentException` naming the method, and an id already registered throws an
  `InvalidOperationException`. The game's own exceptions reach the caller unwrapped. Methods
  reached only by reflection may be stripped from an IL2CPP player; keeping them is part of the
  Phase 6b player test. EditMode tests cover each rule, and `FrontDoorTests` runs the same
  assertions through every way an action can be registered.
- Target discovery in namespace `Agenerela` (#32, DR-014) — an agent's targets are found, not
  typed per agent. `ITargetSource.Collect(ctx, into)` adds a source's targets to a
  `TargetRegistry`; finding nothing leaves it empty, never null, and an id an earlier source
  already added is an `InvalidOperationException` naming it. `ExplicitTargetSource` is a set
  the developer supplies with `Add(id, target)` and `Remove(id)`, in the order added, needing
  no scene: what a country in GreyBoxStrategy uses. `Targetable`, in `Runtime/Unity/`, is the
  component that makes a scene object nameable: an explicit `Id`, never taken from the
  GameObject's name, a one-line `Description`, a `Category` and `IsCurrentlyTargetable`, so a
  sealed door stops being nameable without being destroyed. `Targetable.ValidateId` applies
  `ActionDefinition.ValidateId`'s rules and also rejects `no_target`, and the component warns
  in the Inspector about a missing or malformed id. `ProximityTargetSource`, also in
  `Runtime/Unity/`, offers every enabled `Targetable` within `Radius` of `Origin`, filtered by
  layer, category and optionally line of sight, and never one on the agent itself. It sorts
  nearest first, ties broken by id, and keeps at most `Cap` (8 by default); the ids the cap
  left out are in `Dropped`, which an `Agent` copies into `DecisionTelemetry.TargetsDropped`
  (#81). A missing, malformed or
  duplicate id in the resolved set throws an `InvalidOperationException` naming the
  GameObjects. Each `Targetable` is registered as its `Transform`, so an `[AgentAction]` method
  taking a `Transform` target needs no conversion. EditMode tests cover each rule, including
  the empty set.
- `ActionAvailability` in namespace `Agenerela` (#7) — state masking, built in.
  `ActionAvailability.For(ctx)` returns the actions in `ctx.Actions` that are legal for this
  decision, in the registry's order, which the schema enum is built from. An action with
  `RequiresTarget` is left out while `ctx.Targets` is empty, and any action whose handler's
  `IsAvailable(ctx)` returns false is left out, so an illegal option is absent from the
  schema rather than forbidden in the prompt. The list is built fresh on every call, never
  cached, because state and targets change between decisions. A null context throws an
  `ArgumentNullException`. EditMode tests cover an agent already following (no
  `follow_player`), an idle one (no `stop_following`), an empty target registry, and two
  contexts sharing one registry.
- Token probabilities in the provider contract, in `Agenerela.Providers` (#67, DR-016) — what
  the grounding guard will read. `ProviderCapabilities.ReportsTokenProbabilities`, false by
  default, says every reply carries them, taken before the schema's mask; after the mask they
  always sit on the target list and the guard would pass everything.
  `ProviderResult.TokenProbabilities` holds one `TokenProbability` per generated token of
  `Text`, in order:
  its text, its log probability, and the likeliest `TokenAlternative`s at that position,
  likeliest first. It is null when the provider does not report them, and `Text` stays as
  received beside it. The joined texts equal `Text` except where a character was split across
  two tokens. No provider fills them yet. EditMode tests cover the defaults, and a hand-built
  result whose tokens join back into `Text` and whose alternatives keep their order through a
  Newtonsoft round trip. `Documentation~/providers.md` adds the flag to the capability table.
- `FewShotBuilder` in namespace `Agenerela` (#10) — the block of example exchanges sent with
  every request, built from the developer's own actions, so no example sentence is
  hardcoded and the same code serves a talking guard and a silent colony.
  `FewShotBuilder.Build(available, targets, stimulusLabel, idleExampleStimulus)` opens with
  `Examples of correct decisions:`, then gives one line per available action, in the order
  given: `<label>: "<stimulus>" -> action: <id>, target: <target>`. An action that takes no
  target answers `no_target`. One that does fills its `ExampleStimulus`'s `{0}` with the first
  of its `PreferredExampleTargets` that is registered, written as a display name
  (`training_dummy` becomes `TrainingDummy`). An action with an empty `ExampleStimulus`, or
  with no registered preferred target, gets no example rather than an invented one. Two
  closing examples follow. The idle example answers `idleExampleStimulus` with `none`, and is
  omitted when that is empty. The refusal example fills the first target-requiring stimulus
  containing `{0}` with `Atlantis` (`Avalon` if a game registers `atlantis`) and answers
  `none`, `no_target (not an available target)`; it is omitted when no available action has
  such a stimulus. `stimulusLabel` defaults to `Player`. EditMode tests cover #10's guard and
  colony blocks line for line, each omitted example, the placeholder fallback, and the
  header-only block for no actions. The "Pick up the Blacksmith" regression test registers
  the unsuitable target first, so it fails if examples rotate through the registry (#75).
- `DecisionSchema` and `SchemaField` in namespace `Agenerela` (#8) — the shape of the answer
  one decision asks for, in the framework's own provider-neutral form: ordered `Fields`, each
  a `SchemaField` with a `Name`, a `Description`, `AllowedValues` (null for free text) and
  `Required`. No JSON or grammar syntax lives in either type; each dialect's serializer
  translates it. `DecisionSchema.Build(ctx)` calls `ActionAvailability.For(ctx)` itself and
  returns three required fields in the measured order: `action`, whose values are the
  available actions in registry order followed by `ActionRegistry.None`, injected by the
  framework and always last; `target`, whose values are `TargetRegistry.NoTarget` followed by
  the registered ids, and which is left out entirely while no target is registered; and
  `statement`, free-form and possibly empty. The schema copies what it reads, so a target
  registered afterwards does not change it, and its lists are read-only. The three field
  descriptions are constants on `DecisionSchema` (`ActionDescription`, `TargetDescription`,
  `StatementDescription`), worded with no "player" and no "character", beside the field-name
  constants and `NoneDescription` (`Take no action`). A public constructor takes a fixed field
  list for serializer tests, and checks only for nulls. A null context throws an
  `ArgumentNullException`. EditMode tests cover #8's following-agent example, the field
  order, the `target` field with and without targets, `none` present once and last in every
  shape, the action enum following `ActionAvailability.For`'s order, the description strings,
  and the snapshot; a new `ActionAvailability` test pins that `For` keeps registration order.
- `PromptBuilder` and `PromptOptions` in namespace `Agenerela` (#16) — the text the model
  reads, assembled into a `DecisionRequest` with every field filled.
  `PromptBuilder.Build(ctx, schema, fewShotBlock, history, options)` derives `SystemPrompt`
  from `ctx.Identity` alone: `You are <name>, <role>. <personality>`, then
  `Your goals: <goals>` when there are goals, then one fixed instruction line, `Choose exactly
  one action from the allowed list. Use no_target when none of the listed targets applies.`
  No action list and no prose rule for anything the schema enforces. An empty name, role or
  personality is left out of the first sentence, the identity fields are trimmed, and a
  framed sentence gets a full stop only when the developer's text does not already end one.
  `FewShotBlock` is the block passed in, unchanged, so an A/B run can swap it alone; the
  caller builds it with `FewShotBuilder.Build`. `Observations` is `What you know right now:`
  followed by one `- ` line per observation, verbatim, with blank entries skipped, and is null
  when there are none. `History` is a copy of the turns passed in, never null. `Stimulus` is
  `<label>: "<stimulus>"`, the label from `PromptOptions.StimulusLabel` (default `Player`,
  any free string, rejected only when empty), and `Schema` is the schema passed in.
  `PromptOptions.IdleExampleStimulus` is carried for the caller to pass to `FewShotBuilder`.
  Output uses `\n` on every platform and is byte-identical for the same input. A null context
  or schema throws an `ArgumentNullException`. EditMode tests cover #16's golden file for the
  village guard, with and without observations, both as fields and composed the way
  `DecisionRequest`'s remarks tell a provider to; a non-default label reaching both the
  examples and the stimulus; observations verbatim and absent; determinism; the goals line
  and each identity part left out when empty; a country reading a report with no "player" or
  "character" in anything the framework wrote; and the history copy.
- `Agent` and `DecideOptions` in namespace `Agenerela` (#17) — the class that joins every other
  Phase 1 part, proven end to end against a fake provider. `Agent` is plain C#, not a
  `MonoBehaviour`, so a faction owns one with no GameObject. `new Agent(profile, provider)`
  takes its identity and idle example from an `AgentProfile` (the identity object is kept,
  the idle example read on every call);
  `new Agent(identity, actions, provider)` builds one in code with no asset, and has no idle
  example unless a call passes one. `Bind(actionId, handler)` registers the action as the
  profile defines it, and throws an `ArgumentException` for an id the profile does not list or
  lists twice; an action nobody binds is never offered. `Actions` is the agent's
  `ActionRegistry`, `Targets` the targets registered by hand, `TargetSources` the ordered
  `ITargetSource`s asked on every decision after them (DR-014), and `State` the game's own
  facts, copied into `AgentContext.State` for the handlers and never sent to the model.
  `DecideAsync(stimulus, options, ct)` builds the context, the schema from the actions
  available now, the examples and the request, sends it, and reads the reply into a
  `DecisionResult` whose telemetry holds the provider's name, the framework version, the
  request's latency on a monotonic clock, and the token counts the provider reported. History
  is empty until memory arrives (Phase 2). A target source that fails or offers an id twice, a
  provider that throws, and a reply that is empty, is not one JSON object, or lacks `action`
  or a `target` the schema asked for, give a result with no decision and the outcome
  `PipelineError`, the reason logged as a warning, never an exception. An empty
  `StimulusLabel` throws an `ArgumentException` at the call, before anything is sent, and a
  cancelled token an `OperationCanceledException` when the result is awaited. The reply is
  read with Newtonsoft by the schema's field names, and a reply naming an action or target the
  request did not offer is still a decision. `Execute(result)` checks it again against the
  agent as it is now: the action registered and available, the target registered or
  `no_target`, both recomputed. It runs the bound handler and returns true, returns true for
  `none` without running anything, and returns false, running nothing, for a pipeline error
  and for a decision it refuses, logging why. `DecideOptions` is the per-call bag:
  `StimulusLabel` (default `Player`), `IdleExampleStimulus` (null uses the profile's, empty
  leaves the example out) and `Observations`. EditMode tests run every decision through
  `FakeProvider`, which answers with a scripted reply, fails, or holds its reply, and records
  every request, and `FakeHandler`, which records executions; `Fixtures` holds the shared
  village guard, whose identity #16's golden test now reads too. They cover #17's composition
  test, #16's golden request produced by the agent, a reply becoming a decision with its
  telemetry, each unreadable reply, a failing provider, cancellation, a non-default label and
  two observations reaching the request, state never reaching it, target sources resolved per
  decision, and each refusal.
- `JsonSchemaSerializer` in namespace `Agenerela` (#9) — translates a `DecisionSchema` (#8)
  into the JSON Schema Ollama and the cloud APIs accept. One string property per field in the
  order given, an `enum` for each field with allowed values, `required`, `propertyOrdering`
  (always: Gemini needs the order stated and other consumers ignore it) and
  `additionalProperties: false`. Output is indented with `\n` line endings on every platform.
  A field that is null, unnamed or named twice, and allowed values that are empty or contain
  a null, `""` or a duplicate, throw an `ArgumentException`, so no provider ever sends an
  empty enum string to Gemini. EditMode tests compare the following-agent example against the
  golden output in #9, both built by hand and as `DecisionSchema.Build` makes it from the
  agent's actions, state and targets, and cover a parser round trip, field and enum order, the
  shape without a target field, and each rejection.
- `Phase1GateTests` (#11) — the five checks that close Phase 1, in one file: state masking,
  `target` required with `no_target`, the field order `action`, `target`, `statement`, few-shot
  examples that use only registered targets their verbs sensibly apply to, and no `target`
  field while no target is registered. Each runs `Agent.DecideAsync` against `FakeProvider`
  and asserts on the request it recorded, both its `DecisionSchema` and that schema as
  `JsonSchemaSerializer` writes it, and each failure message names the measured rule that broke.
- `IReportsDroppedTargets` in namespace `Agenerela` (#81) — an optional interface beside
  `ITargetSource` for a source that can say which targets it found but did not offer: one
  member, `Dropped`, the ids the last `Collect` left out, in the source's own order, empty and
  never null when it dropped nothing. `ProximityTargetSource` implements it. On the decision
  path, `Agent` copies each such source's `Dropped` into `DecisionTelemetry.TargetsDropped`
  right after that source's `Collect`, sources in order, so a target list that lost the thing
  the player named can be told apart from a model that ignored a listed target. `Execute`
  resolves the targets again without touching telemetry, so a returned result keeps the drops
  of its own decision. A source that does not implement the interface adds nothing. EditMode
  tests cover the order across several sources, a source that reports nothing, `Execute`
  leaving a returned result alone, and a capped `ProximityTargetSource` end to end through
  `DecideAsync`.

### Changed
- `DecisionRequest.Schema` — the request's `DecisionSchema`, now on the type. It arrives
  unserialised, each provider serialising it in its own dialect, and is required (#8).
- `DecisionTelemetry.TargetsDropped` — the target ids a source found but left out because of
  its cap, so a thin-looking target list can be diagnosed (#32).
- The package depends on `com.unity.nuget.newtonsoft-json` 3.2.2, which Package Manager
  installs automatically. The runtime and editor assemblies reference `Newtonsoft.Json.dll`
  explicitly and no other precompiled DLL (DR-009).

## [0.1.0] - 2026-09-03

### Added
- Package skeleton: runtime, editor and editor-test assembly definitions.
- `AgenerelaInfo` with package name and version constants.
- Smoke tests asserting the assemblies resolve and are visible to the test runner.
