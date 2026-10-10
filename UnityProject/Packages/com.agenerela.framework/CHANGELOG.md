# Changelog

All notable changes to this package are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

While the version stays `0.x`, the public API may change in any release.

## [Unreleased]

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
  left out are in `Dropped`, for `DecisionTelemetry.TargetsDropped`. A missing, malformed or
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
  header-only block for no actions.
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
