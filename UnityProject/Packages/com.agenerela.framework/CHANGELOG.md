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
  so the guards can inspect them. `AgentContext` rejects a null identity or target registry,
  and turns a null stimulus, observations or state into an empty value. EditMode tests cover
  construction, the rejected ids, a null identity, and null observations, state and
  statement. `AgentContext.Actions` is still to come (#58).
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

### Changed
- The package depends on `com.unity.nuget.newtonsoft-json` 3.2.2, which Package Manager
  installs automatically. The runtime and editor assemblies reference `Newtonsoft.Json.dll`
  explicitly and no other precompiled DLL (DR-009).

## [0.1.0] - 2026-09-03

### Added
- Package skeleton: runtime, editor and editor-test assembly definitions.
- `AgenerelaInfo` with package name and version constants.
- Smoke tests asserting the assemblies resolve and are visible to the test runner.
