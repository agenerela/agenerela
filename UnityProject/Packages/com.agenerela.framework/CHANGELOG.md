# Changelog

All notable changes to this package are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

While the version stays `0.x`, the public API may change in any release.

## [Unreleased]

### Changed

- **The default Ollama model is `qwen3.5:4b`**, not `qwen3.5:2b`: `new OllamaSettings()` and a new
  `OllamaProviderConfig` asset name it. On the 389-prompt interim set it chose right 330 times
  against 263, for about 0.08 s more a decision on an RTX 5080 and about 0.7 GB more memory
  (findings, 9 November). Pull it with `ollama pull qwen3.5:4b`. An existing config asset keeps the
  model it was saved with; code that relied on the default now asks the 4B, so name `qwen3.5:2b`
  in `OllamaSettings.Model` to keep the old one.

### Added

- **Standing knowledge and statement rules on an identity** (#190, Living Vale #14):
  `AgentIdentity.Knowledge`, a list of lines, and `AgentIdentity.StatementRules`, one string, both
  serialized, so an `AgentProfile` carries them and a code-built agent gets them through the
  identity it is given. `PromptBuilder` renders them in the system prompt after the goals, as
  `What you know:` with one `- ` line each, then `Your statement: <rules>.`; blank lines are skipped
  and every line break becomes a space, so neither can add a heading or a line of its own.
  Knowledge is not an observation and never counts against `Agent.MaxObservations`. Both empty, the
  default and what every existing asset loads as, sends exactly the prompt sent before them.
  `EvalRunner` can measure them: `EvalArm.Knowledge` adds lines to the profile's knowledge for one
  arm's agents without changing the asset, and each arm's conditions record it as `knowledge`.
- **A decision log file** (#190, G7): `DecisionLog.Start(path)` appends every decision feed
  entry to a JSON-lines file, one `DecisionLogLine` per line with `"version": 1`: the agent, the
  stimulus and label, what the game got, the model's own answer when a guard changed it, the
  verdict, what `Execute` did, the provider, model, latency, waits, tokens, a pipeline error's kind
  and the raw reply. The prompt is written only with `IncludePrompt` on. Lines are written off the
  main thread; a failed write is reported once and never reaches the game. The file rolls over
  once past `MaxFileBytes` (16 MiB). `DecisionLogReader.Read(path)` parses a file back, skipping a
  partial last line. See `Documentation~/logging-decisions.md`.
- **A key for an Ollama server that wants one**: `OllamaSettings.ApiKey`, empty by default, is
  sent as `Authorization: Bearer <key>` on every request, decisions and lifecycle calls alike, so
  a project can use an Ollama served from another machine behind a gateway, such as the RTX 5080's
  in this repository's `tools/ollama-server/`. A key with a space or a control character inside is
  refused when the provider is built. `OllamaProviderConfig.ApiKeyVariable` (**API Key Variable**)
  names the environment variable to read it from when the provider is built, so the asset never
  holds the key; a named variable that is not set fails then, naming it. An HTTP 401 is still
  `Rejected`, and its message now says whether a key was sent. No message names the key.
- **Reply checks in `EvalRunner`** (#145): every answer's statement is checked for four mechanical
  flags, `ReplyFlag.Echo` (it is a few-shot line, by `PromptSetChecks.MatchesExample`: any action's
  example or counter-example stimulus or statement, the profile's idle example and its statement,
  or the arm's own), `Empty` (blank when the schema asked for one), `TooLong` (over the new
  `EvalArm.MaxStatementLength`, `EvalArm.DefaultMaxStatementLength` = 200 characters, unmeasured)
  and `Leak` (an action or target id that is not a plain word, such as `move_to` or `no_target`; a
  schema field used as a label, `action:`; a JSON brace; or one of `ReplyChecks.AssistantPhrases`).
  Each answer records `EvalAnswer.ReplyChecked` and `ReplyFlags`, a `ReplyFinding` per flag with
  what it found; each arm `EvalArmResult.Replies`, a `ReplyCounts` per flag and of answers with any
  flag; each arm's conditions `MaxStatementLength`; and `report.md` ends with a **Reply checks**
  section listing every flagged answer. `EvalReport.FlaggedReplies` lists them in code and
  `ReplyChecks.Check` runs the checks on any statement. A second score only: no flag changes an
  outcome, `Telemetry.Outcome` or the accuracy, and a report written before them reads back with
  nothing checked. No prompt-set format change.
- **Inspection seams** (#166): `DecisionFeed.Recorded`, raised once for each result `DecideAsync`
  returns, pipeline errors and guard-stopped decisions included, and once for each `Execute` that
  returns, with a `DecisionFeedEntry` carrying the `Agent`, the `DecisionResult`, its
  `DecisionStage`, the `Execution` for `Executed`, the UTC time and a sequence number. Cancelled
  calls, a reset's included, are never in it. A subscriber that throws is logged; with none, raising
  costs a null check. **`Agent.Preview(stimulus, options)`** returns an `AgentPreview`: the request
  `DecideAsync` would send next, built by the same method, with the actions offered and left out,
  the targets and observations per source with what each dropped, and the history. It never calls
  the provider or remembers anything. **`AgentBehaviour.Find(agent)`** and **`HasAgent`** map an
  agent back to its component without building one. The package's Editor assembly now sees the
  runtime's internals. `IMemoryStrategy.Recall`, `ITargetSource.Collect` and
  `IObservationSource.Observe` now say a preview calls them with no decision after it, so they
  should change nothing.
- **Statements in the few-shot examples** (#190 G14): an example can show what the agent says,
  as `-> action: X, target: Y, statement: "..."`, the reply's own field order. Write one in
  `ActionDefinition.ExampleStatement` and `CounterExampleStatement` (or `Statement = "..."` on
  `[Example]` and `[CounterExample]`), its `{0}` filled with the stimulus's target, and for the idle
  example in `AgentProfile.IdleExampleStatement`, `Agent.IdleExampleStatement` or
  `DecideOptions.IdleExampleStatement`. The idle statement follows the stimulus's precedence but
  never comes from further down than the stimulus did, so a profile's statement is never attached
  to a line a call or an agent supplied. On, but empty: every statement is empty by default, so
  nothing changes until one is written. `DecideOptions.ExampleStatements = false` (and
  `PromptOptions.ExampleStatements`) leaves them all out, for an A/B's control arm. The refusal
  of a target that is not here never has one. A quote or backslash inside an example's stimulus
  or statement is now escaped as JSON escapes it, so it cannot end the quoted text early; no
  example the repository ships contains either. Unmeasured: the A/B is owed in #151 and written up
  in `docs/live-runs.md`.
- **`FewShotBuilder.Build(available, targets, PromptOptions)`**, the overload `Agent` now uses, so
  every per-call example setting comes from one object. The old overload is unchanged.
- `PromptSetChecks` and `EvalRunner` (an arm's own idle statement) refuse a prompt that matches an
  example's statement, as they do one that matches its stimulus (hard rule 4). `EvalArmConditions`
  records `idleExampleStatement` and `exampleStatements`.
- `Documentation~/prompt.md`: a section on statements.
- **`Agent.Reset()`** (#190, G12): starts an agent afresh without destroying it. It cancels this
  agent's decisions, and no other agent's on the same provider: one waiting in its `Backlog` or in
  the provider's queue is never sent, and one with the provider is stopped through its token. It
  drops the decisions held for `MemoryCommit.OnExecute`, so `Commit` and `Discard` return false for
  them and `Execute` carries them out without remembering them. And it clears memory. A cancelled
  caller sees a plain `OperationCanceledException` whose token is the agent's own, so a caller whose
  own token is not cancelled knows the agent was reset. **`AgentBehaviour.ResetAgent()`** does the
  same for the agent the component built; it is not called `Reset` because Unity calls that in the
  editor.
- **`IClearableMemory`** (#190, G12): an optional interface beside `IMemoryStrategy`, with
  `Clear()`, which `Agent.Reset` calls. `RollingHistory` implements it, setting `Dropped` and
  `Forgotten` back to 0 as well. A strategy without it keeps its memory on a reset, with a warning.
- `Documentation~/executing-and-memory.md`: starting an agent afresh, and telling a reset from your
  own cancellation.

- **Target categories** (#190 B, DR-020): `TargetRegistry.Register(id, target, category)`, beside
  the two-argument overload, and `TargetRegistry.CategoryOf(id)`, null for a target with no
  category and never throwing. `ExplicitTargetSource.Add(id, target, category)` does the same for a
  hand-made set, and `ProximityTargetSource` now registers each `Targetable` in its
  `Targetable.Category`. The model never reads a category.
- **Which targets an action takes** (#190 B, GreyBoxVillage G2, Living Vale #4):
  `ActionDefinition.TargetCategories`, `[AgentAction(TargetCategories = new[] { ... })]`,
  `ActionDefinition.AcceptsCategory(category)` and `DeclaresTargetCategories`. Empty, the default,
  takes any target. **`TargetCategoryGuard`**, a third built-in guard, runs in `DecideAsync` after
  `TargetGroundingGuard` and before `Agent.Guards`, and turns an action paired with a target of
  another category, or of none, into `none` (`RewriteToNone`); `Agent.Execute` runs it again after
  `SchemaLegalityGuard` and refuses a mismatch. It passes every action that names no category, so
  nothing changes until a developer declares one, and the request is unchanged either way.
- **`Agent.TargetCategories`** (#190 B, G3): the only target categories an agent is offered,
  whichever source finds them, applied after every source on each decision and at `Execute`'s
  re-check. Empty, the default, offers every target; set, a target in another category or in none
  is left out.
- **`TargetRegistry.Offer(id, target, category)`** (#190 B, G13): registers a target unless the
  same object is already registered under the id, and returns whether it added it; another object
  under the id throws. It is the merge rule for target sources below.
- **`ActionRegistry.RegisterMethods(owner, assets, only)`** (#190 B, G10): registers only the
  listed action ids of one owner; null registers all of them, an empty list none. Every method is
  still checked.
- **`AgentBehaviour.OnlyActions`** (#190 B, G10): the action ids the agent registers, of its
  GameObject's `[AgentAction]` methods and its `Handlers` rows. Empty, the default, registers every
  one. The Inspector warns about an id nothing provides and a `Handlers` row it leaves out.
- **`AgentBehaviour.TargetCategories`** (#190 B, G3): copied to `Agent.TargetCategories`, and to the
  proximity source's `Categories` when that names none, so the source's cap counts only what can
  be offered.
- `Documentation~/targets.md`: categories, the category guard, an agent's categories, the merge
  rule and an agent's own actions.
- **The paired schema** (#190 B, DR-020): `DecideOptions.PairTargetsWithActions`, off by default.
  On, the schema offers each action with only the targets it takes, its categories' or every
  target, with `no_target` only when it does not require one, and `none` with `no_target` alone; an
  action that requires a target and takes none registered is not offered, nor its example shown.
  The prompt is unchanged. Unmeasured: it stays off until the A/B in #151.
- **`DecisionSchema.Pairs`**, a list of **`ActionTargets`** (`Actions`, `Targets`), null for a flat
  schema; `DecisionSchema.Build(ctx, pairTargets)` and a `DecisionSchema(fields, pairs)` constructor.
  `JsonSchemaSerializer` writes a schema with pairs as a root `anyOf` of complete objects, and
  refuses pairs no grammar could honour; a schema without pairs is written exactly as before.
  `SchemaLegalityGuard` rejects a reply whose action and target a paired request did not offer
  together.
- `EvalArmConditions.PairTargetsWithActions`, so `report.json` says which arm paired its targets.
- A test that an arm pairing targets over our evaluation set changes nothing but the schema.
- A test that our evaluation set's schema, as `EvalRunner` sends it with every option off, is
  byte-identical to the shape every measurement so far was scored against.
- **`Conversation`, an optional helper for asking one agent one thing at a time** (#190,
  Living Vale #8). `new Conversation(agent)`; `Start(stimulus, options, ct)` asks the agent and
  returns a `ConversationTurn` at once, or null while another turn is waiting;
  `Replace(...)` ends the waiting turn as `Replaced` first. `Timeout`, infinite by default, ends
  a turn as `TimedOut` on time even when the provider ignores cancellation, and the caller's
  token ends it as `Cancelled`. `Cancel()` ends the current turn. Only the current turn's answer
  runs: `Execute(result)` and `Execute(result, out ActionResult)` refuse any other with
  `ExecutionStatus.Refused`, recorded in its telemetry, and a turn's answer is discarded from
  memory when the turn stops being current. `Start` throws for an agent with memory unless
  `MemoryCommit` is `OnExecute`. It tells a stale result by reference and never touches
  `DecideOptions.Correlation`. `ConversationTurn` has `Number`, `Status` (`TurnStatus`:
  `Waiting`, `Answered`, `TimedOut`, `Cancelled`, `Replaced`, `Failed`), `Result`,
  `IsCurrent` and `Completion`, a `Task<TurnStatus>` that never ends cancelled. The game still
  decides when a turn starts (DR-008). `Documentation~/conversations.md` describes it.

- **What executing a decision did** (#190 C, DR-018): `ActionResult`, a value type made by
  `ActionResult.Done(payload)` and `ActionResult.Failed(reason, payload)`, with `Status`
  (`ExecutionStatus`: `Done`, `Failed`, `NoAction`, `Refused`), `Reason`, `Payload`,
  `Succeeded`, `HandlerRan` and `TryGetPayload<T>`. A handler returns one through
  `IActionHandlerWithResult`, an optional interface beside `IActionHandler`; through
  `DelegateActionHandler.WithResult`; or from an `[AgentAction]` method that returns
  `ActionResult` instead of `void`. The new overload `Agent.Execute(result, out ActionResult)`
  hands it to the caller, with `NoAction` for `none` and `Refused`, with the reason, whenever no
  handler ran. `Execute(result)` is unchanged except that a handler reporting `Failed` makes it
  false; a handler that reports nothing is `Done` whenever it returns. `DecisionTelemetry` gains
  `Execution` and `ExecutionReason`, written by every `Execute`.
- **`DecideOptions.Correlation`** (#190 C): the caller's own object for a call, which the
  framework never reads, carried on `DecisionResult.Correlation`, pipeline errors included, and
  on `AgentContext.Correlation` of every context made for the decision, the one `Execute` hands the
  handler included. `AgentContext` gains a constructor that takes it.
- **`Agent.MemoryCommit`** (#190 C): `MemoryCommit.OnDecide`, the default and the behaviour so
  far, or `MemoryCommit.OnExecute`, under which a decision is held on its result until
  `Execute` carries it out or the game calls `Agent.Commit(result)` or `Agent.Discard(result)`,
  so a reply dropped as stale is never remembered.
- **Saving memory** (#190 C): `IExportsMemory`, an optional interface beside `IMemoryStrategy`,
  with `Export()` and `Import(MemorySnapshot)`. `MemorySnapshot` (`Format`, `Version`,
  `Exchanges` of `MemoryExchange`, `Data`) is public fields and lists, for `JsonUtility` and
  Newtonsoft. `RollingHistory` implements it, as `RollingHistory.SnapshotFormat` version 1.
  `RollingHistory.Import` takes only that format at a version from 1 to `SnapshotVersion`, and
  throws an `ArgumentException` for another format, a newer version or a null exchange, leaving
  the memory as it was (#190, Living Vale #22). `RollingHistory.CanImport(snapshot, out reason)`
  gives the same answer without importing, so a game can check a whole save first.
- `Documentation~/executing-and-memory.md`: handler results, correlation, memory that waits, and
  saving memory.
- **The actions and targets, listed in the prompt** (#190 A, Living Vale #1, GreyBoxVillage G9):
  `DecideOptions.ListActionsAndTargets`, off by default, and `PromptOptions.ListActionsAndTargets`
  beside it. On, the system prompt gains, after the instruction line, "Allowed actions:" with every
  action the schema offers as `- id: Description.`, `none` last, and "Targets:" with the schema's
  targets, without `no_target`, as `- id: Description.` or `- id`. A provider that never reads the
  schema, such as Ollama, otherwise shows a local model no action list at all. `MaxListedTargets`
  (12 by default, `PromptOptions.DefaultMaxListedTargets`) caps the target lines, the first in
  schema order; every action is always listed, and a negative cap throws at the call. Unmeasured:
  it stays off until the A/B in #151.
- **`IDescribedTarget`**: a target object implements its `Description` to be listed with it. Any
  other target, a scene target's `Transform` included, is listed by id.
- **Counter-examples** (#190 A, Living Vale #9): `ActionDefinition.CounterExampleStimulus` and, for
  the code front door, `[CounterExample("...")]`. `FewShotBuilder.Build` shows one right after its
  action's example, answered `action: none, target: no_target`, while the action is offered, its
  `{0}` filled as the example's is; with `{0}` and no registered preferred target, it is left out.
  Empty by default, so nothing changes until a developer writes one. `DecideOptions.CounterExamples`
  (on by default) and `FewShotBuilder.Build`'s new `counterExamples` parameter leave them all out,
  for an A/B's control arm. `PromptSetChecks` flags a prompt that matches one (hard rule 4).
- **The idle example on an agent built in code** (#190 A, Living Vale #13): `Agent.IdleExampleStimulus`,
  null by default. Set, it replaces the profile's for that agent, and a call's
  `DecideOptions.IdleExampleStimulus` still wins; empty leaves it out. `AgentProfile.DefaultIdleExampleStimulus`
  is the text a new profile starts with, so a code-built agent can have the same one. Off by
  default, because whether an idle example helps is still unmeasured.
- `EvalArmConditions` records each arm's `listActionsAndTargets`, `maxListedTargets` and
  `counterExamples` in `report.json`.
- `Documentation~/prompt.md`: what the model reads, and the three switches above.

- **The A/B runner** (#145): `EvalRunner` asks every prompt of a `PromptSet` through a fresh
  `Agent` per prompt in two `EvalArm`s, each a name, a `ProviderConfig` and the `DecideOptions`
  its calls start from, and returns an `EvalReport`. `EvalArmOrder.Interleaved` (the default)
  asks each prompt in A, then in B; `Sequential` runs all of A, unloads its Ollama model and
  waits until it has left memory, then runs all of B, for arms on two models. Before the run it
  refuses a set that breaks a `PromptSetChecks` rule or disagrees with the profile, an Ollama
  server holding a model other than the arms', and interleaved arms on two models, with an
  `InvalidOperationException` saying why; it records Ollama's version, sends `WarmUpRequests`
  (3) throwaway decisions per arm that never reach the report, and records `size` and
  `size_vram` from `/api/ps`, warning when the model is not fully on the GPU. Each prompt's
  `preconditionState` goes into `Agent.State`, every profile action is bound to a do-nothing
  handler available exactly when the state holds its `onlyWhen`, the set's targets come from an
  `ExplicitTargetSource`, and the state and targets are also told to the model as observations,
  the state through `DescribeState` (`PlainStateLines` by default). `EvalRunner.Classify` sorts
  an answer into the five `DecisionOutcome`s, any guard verdict counting as the guard firing,
  and the outcome is written to the decision's `Telemetry.Outcome`.
- **The report.** `EvalReport` holds `Conditions` (`EvalConditions`, `EvalArmConditions`,
  `ResidencyReading`, `ResidentModel`: dates, framework version, the set's last commit, the
  graphics device, how targets and state reached the model, and per arm its config, provider,
  model, version, options and residency), `Arms` (`EvalArmResult`, `OutcomeCounts` overall and
  per category), `Noise` (`NoiseCheck`: a difference smaller than `10 × √(50 / n)` points is
  within noise) and every `EvalAnswer`, with the model's own decision and the guard's reason.
  `Save(folder)` writes `report.json` and a Markdown `report.md` into a folder named for the run;
  `FromJson` reads one back.
- **A run's progress and a cancelled run's report** (#146). `EvalRunner.Progress` is told, on
  the main thread, each `EvalPhase` (`Checking`, `WarmUp`, `Asking`, `Unloading`) and, as an
  `EvalProgress`, the arm and the answers so far out of the run's total. A cancelled run now
  throws `EvalCancelledException`, an `OperationCanceledException`, so every existing catch still
  catches it, whose `Partial` is the report as far as it got: `EvalReport.Cancelled` true, every
  answer given, and a noise check only where both arms were asked the same prompts. `report.md`
  opens with "Cancelled after n of m answers"; a finished report's JSON is unchanged.
- **Window → Agenerela → Evaluation Report** (#146, screen 4): runs an A/B from the Editor
  without blocking it, and reads the report in six numbered parts: the run set-up
  with its conditions in plain words, the five outcomes per arm, the noise check, accuracy per
  category, the answers that were not correct (each opening its whole record), and the
  labellers' agreement, counted as the Prompt Set window counts it, with a summary to copy into
  findings.md. Each finished run is saved to `Logs/Evaluation/`, and **Open report…** reads any
  saved `report.json` back, showing the same as when it ran. Until it can run it shows the steps
  to start and why **Run A/B** is greyed out; **Use the project's set, profile and arms** fills
  them from the project's `agent.json`, the profile beside it and Project Settings. Each arm has
  a name and a grounding-guard switch, one config twice starting as `control` (off) against
  `guards-on`; arms on two Ollama models always run one after the other. Its choices are
  remembered per project in `EditorPrefs`. A run shows its progress, and a cancelled run's
  partial report is saved and shown, marked cancelled.
- **The evaluation arms on Project Settings → Agenerela** (DR-013): `AgenerelaSettings` gains
  `EvaluationArmA` and `EvaluationArmB`, which agents never read.
- `Documentation~/evaluation.md`: running an A/B and reading its report.

- **`DecisionResult.Request` and `DecisionResult.Reply`** (#142): the `DecisionRequest` a decision
  sent and the provider's `ProviderResult` for it, so a game can tell its agents' exchanges apart
  when several share one provider's queue, without wrapping the provider. Both are read-only and
  set on every result whose request was sent, pipeline errors included: a reply that could not be
  read, or a guard that threw, keeps the reply. `Reply` is null when the provider failed or
  returned nothing, and both are null when preparing the request failed. A guard-stopped decision
  keeps the model's original reply, never the refusal line's. A cancelled call still has no
  result. The public constructor sets neither.

- **Prompt sets** (#134, DR-017): the labelled prompts an agent is measured against, as public
  types in `Agenerela.Evaluation`. `PromptSet` is the set file (`Agent`, `Actions`, `Targets`,
  `Categories`) and its prompts, with `Load`, `Save`, `SaveCategory`, `CreateFor(profile, path)`,
  `NextId`, `TryFind` and `FindAction`. `EvalPrompt` holds a prompt's fields under their JSON
  names, `ExpectedTarget` one target or an open choice, and `DeclaredAction` (with
  `IsAvailableIn(state)`), `DeclaredTarget` and `PromptCategory` the set file's parts. A field
  the format does not know is kept in `Extra` and written back. Files are read and written with
  Newtonsoft only, indented, with LF line ends. `ValidateCategoryId` and `ValidateCategory` keep
  a category's id a plain name and its file inside the set's folder, so neither can redirect a
  write onto the set file, a label file or anywhere else; `Load` refuses a set that breaks
  them, or has a list that is null, with an `InvalidDataException`.
- **The writing rules in code.** `PromptSetChecks.Check` and `CheckPrompt` return a
  `PromptProblem` for each `PromptRule` a prompt breaks: an unregistered target, `none` without
  `no_target`, a target-needing action with `no_target` or a targetless one with a target, a
  state that does not match the action's `onlyWhen`, a stimulus that is a few-shot example
  (hard rule 4, `MatchesExample`), an id that is malformed or used twice, an undeclared action, a
  missing field. `CompareWithProfile` lists where an `AgentProfile` and the set disagree.
- **Blind labels and agreement.** `PromptSet.RecordLabel` writes one annotator's answer, a
  `LabelAnswer` (`action/target`), to `labels/<annotator>/<category>.json`, an `AnnotatorLabels`,
  and never replaces one. `BlindPrompt.Queue` gives the prompts an annotator still has to label
  with nothing that shows an answer, and `LabelChoices` the actions their state allows.
  `AgreementReport.Compute` counts per prompt (`PromptAgreement`: `Answered`, `Agreeing`,
  `IsSplit`) and overall, and lists the splits; `PromptSet.Settle` writes a person's decision and
  keeps everyone's answer in the prompt's `labels`.
- **Window → Agenerela → Prompt Set** (`PromptSetWindow`): Write mode edits prompts with the
  expected answer picked from dropdowns and the rules shown inline; Label mode asks one
  annotator blind; Agreement mode shows the counts and settles a split when a person clicks.
- **A Prompt Set window that explains itself** (#158). Until a set and a profile are chosen, the
  tabs are greyed out with the reason, and the window shows three numbered steps: choose the
  set, choose the profile, type your name. **Open the project's evaluation set** opens the
  `agent.json` under `Assets/` (`Assets/Evaluation/` first) and the `AgentProfile` beside it in
  one click; **New set from this profile…** stays as the second way in. A line under the tabs
  says what the current one is for, every toolbar field has a tooltip, **You** is now **Your
  name**, and Label mode shows "12 of 40 left to label" and says why once nothing is left. The
  set, the profile and the name are remembered in `EditorPrefs`, keyed per project: a name or set
  chosen in #134's window is asked for once more. agenerela's own set now has its profile,
  `Assets/Evaluation/VillageGuard.asset`, so the few-shot check runs on it.
- `Documentation~/evaluation.md`: the set's layout, writing prompts, labelling blind, and reading
  and settling agreement.

- **Contained refusals** (#109, build plan §2.5). A decision a guard stopped now has an empty
  statement, where it kept the model's; the model's words stay on `ModelDecision`, and memory holds
  what was actually said. `Agent.RefusalStatement`, a
  `Func<GuardVerdict, AgentContext, string>`, gives the developer's line when a guard stops a
  decision. `DecideOptions.AskForRefusalLine`, off by default, asks the model for one in a second,
  short request through the same queue at the call's priority, made only when a guard fired and no
  developer line was given. `DecisionTelemetry` records it apart from the decision:
  `RefusalLineSeconds`, `RefusalLinePromptTokens`, `RefusalLineCompletionTokens`, and
  `RefusalLineError` when it fails, which leaves the decision standing. The request's wording is
  unmeasured. `Documentation~/guards.md` has the rules.
- **`AgentBehaviour`** (#93): an agent on a GameObject, with no manager object. Drop it on the
  guard and assign a `Profile`. It builds the `Agent` with its own `Provider` or the project
  default, registers the `[AgentAction]` methods of every component on the GameObject, binds the
  rows of `Handlers`, and finds targets and observations around this transform: `Targets` and
  `Observations` are a `ProximityTargetSource` and a `ProximityObservationSource`, each with an
  `Origin` that defaults to this transform and switched by `FindTargetsNearby` and `NoticeNearby`,
  both on by default. `DecideAsync(stimulus, options)` passes `destroyCancellationToken`, so a
  destroyed guard's decision ends cancelled and the provider stops its request. The agent is built
  in `Awake` or by the first read of `Agent`, whichever comes first, and a bad setup throws
  `InvalidOperationException` saying why. The Inspector warns about a missing profile, an action
  with no handler, and a row of `Handlers` that cannot be bound, through a public `Validate`.
- `Documentation~/agents-in-a-scene.md`: setting one up, each field, when it is built, and what
  destroying the GameObject does.
- **Developer guards** (#108, build plan §2.5 item 3). `Agent.Guards` is now public: a game's own
  `IDecisionGuard`s, empty by default, run after the built-in ones in list order inside
  `DecideAsync`, and their verdicts show in `GuardsFired`, `Verdict` and `GuardReason` like a
  built-in's. `Execute` does not run them again. `DelegateGuard` makes one from a name and a
  `Func<GuardInput, GuardVerdict>`.
- `PipelineErrorKind.GuardFailed`, at the end of the enum: a guard that throws, or a null entry in
  `Agent.Guards`, makes the decision a pipeline error naming the guard, rather than a refusal.
- `Documentation~/guards.md`: what each built-in guard checks, the three verdicts, a guard
  compared with availability, and two worked examples, a per-threat cooldown and the name check
  DR-016 dropped, both run as tests.
- **The scripted provider** (#190, G6): `ScriptedProvider`, an `ILLMProvider` with no model
  behind it, for tests, headless captures and presenting a game with the model off. Replies are
  scripted with `When(agent, stimulus, replies…)`, matched by `AgentIdentity.Name` and the exact
  stimulus without its label, a null agent or stimulus matching any, with `Otherwise(…)` for
  everything else and a `Callback` given a `ScriptedRequest` (the agent, the stimulus and its
  label, the schema's `OfferedActions` and `OfferedTargets`, the request) for rules. Several
  replies for one key answer in order, the last repeating; a request nothing answers fails with
  `ProviderErrorKind.Rejected`, naming the agent and stimulus. A `ScriptedReply` is `Decide`,
  `Statement`, `Raw`, `Fail(kind)` or `Never()`, with `After(seconds)` for a wall-clock latency
  that telemetry records, and `WithTokens` for the grounding guard. Refusal lines take
  `RefusalLine` or the callback, never a key's next reply. `Hold` and `Release()` keep replies
  back for queue tests. `Name` and every reply's `Model` are always `"scripted"`, so telemetry
  never names a model. `ScriptedProviderConfig` (**Create → Agenerela → Scripted Provider**) makes
  one an asset whose `Replies` entries a scene can select, with `Scripted` for scripting it
  further in code.
- `Documentation~/providers.md`: the scripted provider, as an asset, testing with it, and how a
  decision log will replay through it (G7).
- **`Agent.Id`** (#190, Living Vale #21): an optional stable id the game chooses, such as
  `"npc_mara"`, null by default and never sent to a model. Two agents that share a name, or are
  built from one profile and so share one `AgentIdentity`, can be told apart by it.
  `ScriptedProvider` matches a script by the asking agent's id before its name, within each stage,
  and reports it in `ScriptedRequest.AgentId`. `When(agent, …)` and a config entry's **Agent** take
  either, and keying by name works as before when no id is set.

- **Provider health** (#190 G1, DR-015 as amended). `ProviderHealth.For(provider)`, one per
  provider instance like its queue, says whether the provider is answering: `IsDown`, `Reason`
  (a `PipelineErrorKind`), `Message`, `DownSinceUtc`, `RetryAtUtc`, `ConsecutiveFailures`, and a
  `Changed` event, raised on the main thread. `Agent` reports every request: any reply marks it
  up, a provider failure down, with a retry time 5 s after the first failure, doubling to 60 s
  (`FirstRetrySeconds`, `LongestRetrySeconds`, each at most a day). `Reset()` marks it up by hand. **`BackOff`**, off
  by default: when on, a background request whose turn comes while the provider is down, before
  the retry time, is not sent; player-facing requests always are.
- `PipelineErrorKindExtensions.IsProviderFault(kind)`: true for a failure that is the provider's,
  false for one decision's own (preparation, an unreadable reply, a guard, the backlog).
- **Model lifecycle** (#190 LV #6, DR-019). `IModelLifecycle`, an optional interface a provider
  implements to manage its server's models: `ListModelsAsync` (a list of `ModelInfo`: `Name`,
  `SizeBytes`), `WarmUpAsync` and `UnloadAsync` for the provider's own model. None goes through
  the decision queue, and failures throw `ProviderException`. `OllamaProvider` implements it with
  `/api/tags`, an `/api/generate` with no prompt at the decisions' `num_ctx`, and `keep_alive: 0`
  that waits until `/api/ps` no longer lists the model.
- **`Agent.Provider` can be set** (DR-019), to switch an agent to another backend or model at
  runtime. It keeps its memory, actions, targets, sources, guards and state; a decision under way
  finishes on the provider it started with, its refusal line included.
- **`Agent.Backlog`** (#190 G5, DR-015 as amended), null by default. A `DecisionBacklog` makes the
  agent decide one call at a time; calls made meanwhile wait, player-facing first, and are
  prepared when their turn comes. Presets `DecisionBacklog.LatestWins`, `DropOldest(n)` and
  `QueueWithCap(n)`, or `new DecisionBacklog(capacity, BacklogOverflow)`. `Agent.IsDeciding` and
  `Agent.Waiting` say what it is doing; `DecisionTelemetry.BacklogWaitSeconds` records how long a
  call waited for its turn.
- `PipelineErrorKind.ProviderDown`, `Superseded` and `BacklogFull`, at the end of the enum: a call
  held back by `ProviderHealth.BackOff`, dropped from a backlog for a newer one, or refused by a
  full one. Each ends with no decision before anything is sent, and logs nothing.
- `Documentation~/providers.md`: provider health, model lifecycle, switching an agent's provider,
  and the backlog.

### Changed

- **A provider is sent a token linked to the caller's** (#190, G12), so that `Agent.Reset` can stop
  a request already with it. It was the caller's own token object. Cancelling the caller's token
  cancels it as before, and a caller still sees its own token on the `OperationCanceledException`.
  Only code that compared the provider's token with the caller's notices.

- **The Evaluation Report window is built in UI Toolkit on the #167 kit** (#179): the same
  window as #146 built, now a tree from `EvaluationReportWindow.uxml` and `.uss`, with the kit's
  section titles, chips for category ids, problem boxes for what the project or the set lacks,
  and its primary button. The five outcomes' fills come from the kit's variables, so both editor
  skins read. Two changes the lead approved with the redrawn screen 4: **Run set-up** folds to one
  line, with **Run A/B** on it, while a report is shown; and on first open the one primary
  button is the **Start here** card's. It opens at 780 × 900 rather than Unity's small default,
  at the lead's request; its minimum stays 560 px. EditMode tests now build the window's tree and
  check what it shows, and that nothing runs past the window at its 560 px minimum.

- **The same target from two sources is one target** (#190 B, G13, DR-020). `ExplicitTargetSource`
  and `ProximityTargetSource` skip an id an earlier source, or `Agent.Targets`, already registered
  for the same object, which they used to refuse; the first registration, its category included,
  is kept, and a Targetable skipped this way still counts toward the proximity cap. Another object
  under the id is still an error. `ITargetSource`'s contract says so, and `TargetRegistry.Register`
  is unchanged: it throws for any id already present.

### Fixed

- **Play mode without a domain reload** (#190, G8): `AgenerelaSettings` forgets the settings it
  found, and a test's override, as Play mode starts, so Enter Play Mode Options with the domain
  reload off no longer carries them into the next session. A new EditMode test fails on any static
  field in the framework's runtime assembly that nothing resets; the three that need no reset
  (`ProviderConfig`'s provider names and built configs, and the queues) say why in it.

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
