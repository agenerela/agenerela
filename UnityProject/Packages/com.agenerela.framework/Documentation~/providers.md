# Providers — how three very different backends fit behind one interface

`ILLMProvider` exists so that "which model runs this" is a configuration choice rather than
a framework edit. This note explains how each planned backend satisfies it, what a *second*
cloud vendor would need, and which parts of the contract are deliberately still open.

One backend is implemented: `OllamaProvider` (#88), the development backend. Phase 1 defined
the contract, and `Agent` (#17) is tested against a fake provider; Phase 6 writes the cloud
providers, Phase 6b the in-process one. Beside them ships `ScriptedProvider` (#190), which has
no model behind it at all: for tests, headless captures and presenting a game with the model
off ([below](#the-scripted-provider--no-model-at-all)).

## The interface

```csharp
public interface ILLMProvider
{
    string Name { get; }
    ProviderCapabilities Capabilities { get; }
    Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct);
}
```

A provider takes a request `PromptBuilder` (#16) has assembled, sends it, and returns text.
It does not parse the reply, does not know what an action is, and does not decide whether
the answer was legal. How it composes the request's fields into messages is stated once, in
the remarks on `DecisionRequest`.
`Agent` (#17) parses `ProviderResult.Text` and builds `DecisionTelemetry` (#3) from the rest:
the provider's `Name`, the model the reply reports (`ProviderResult.Model`, copied into
`DecisionTelemetry.Model`), its token counts, and a latency the agent measures itself around
the request.

Two rules sit above every implementation:

- **A provider is untrusted.** `Capabilities` reports what a backend claims; the validation
  pipeline (Phase 3) runs over every decision regardless. A capability flag must never
  switch a guard off. The grounding guard reads the token probabilities in the result, not
  the flag that promises them, and records that it could not run when a reply has none. A
  plain-chat backend with no constrained decoding is a legal
  provider — it is just likelier to return something unparseable, which the pipeline
  classifies rather than crashes on.
- **A provider never rewrites the prompt.** The blocks in `DecisionRequest` arrive rendered
  and go out unchanged. Prompt wording is measured; two of the three regressions caught by
  the prototype's A/B harness were wording changes, and a provider quietly improving a
  block would be invisible in the numbers. The one part that arrives unserialised is
  `DecisionRequest.Schema`, the provider-neutral `DecisionSchema` (#8): the provider runs the
  serializer for its `Dialect` over it, and treats it as required, since a request without a
  schema is not a decision request. Not every schema has an `action`: the request for a refusal
  line (#109, [guards.md](guards.md)) has only `statement`, and its `Stimulus` carries no label.
  A provider sends it like any other.

## The three backends

| | Ollama | Cloud (Gemini) | In-process (LLMUnity) |
|---|---|---|---|
| Job | Development | Optional / comparison | Shipping |
| Constrained decoding | yes | yes | yes (GBNF) |
| Explicit ordering | no | **yes** | no |
| Dialect | `JsonSchema` | `JsonSchema` | **`Gbnf`** |
| Rate limit | none | **~15/min, 1000/day** | none |
| Rejects empty enum values | no | **yes** | no |
| Reports token probabilities | yes | by model | not yet known |

Bold marks where one backend differs from the other two, and most of it falls in the middle
column — which is the point. Gemini is the vendor whose quirks the prototype actually hit, so
it is the worked example and doubles as the conformance reference for the next vendor.

The last row is `ReportsTokenProbabilities`, which the grounding guard depends on (build plan
§2.5, DR-016). A provider sets it only if every reply carries the probabilities taken
**before** the schema's mask. After the mask they always sit on the target list, so the guard
would pass everything.

A provider that leaves it false gets the fallback instead (#107). While
`DecideOptions.CheckTarget` is on, `Agent` adds one line to the system prompt, after the
instruction line, naming the schema's target ids: "Only these things are here: tower,
training_dummy. If you are asked to act on anything else, choose action none and target
no_target, and say it is not here. Never act on a different thing instead."
`DecisionTelemetry.TargetRuleSent` records it. The rule measured below the guard in DR-016,
and this wording of it is unmeasured. The flag is read before the request, so a provider that
sets it and then returns no probabilities gets neither the rule nor the guard.

### Ollama — development

An HTTP server on the developer's machine. Call it at `http://127.0.0.1:11434`, not
`localhost`: on Windows `localhost` tries IPv6 first, Ollama listens on IPv4 only, and the
prototype's probes waited about 2 s a request for the failed attempt (build plan §2.4). The
schema goes in the request's `format` field as JSON
Schema and Ollama compiles it to a sampling grammar internally, so property order is
inferred from the schema and needs no separate statement. No quota, no key, no budget.

`Capabilities`: constrained decoding yes, explicit ordering no, dialect `JsonSchema`,
`RateLimit` null, rejects empty enum values no, reports token probabilities yes.

**What was built (#88).** `new Agent(profile, new OllamaProvider(new OllamaSettings()))`
decides against `qwen3.5:2b` at `http://127.0.0.1:11434`. `OllamaSettings` is a plain class
with `Endpoint`, `Model`, `ContextSize` (4096), `Temperature` (0) and `TimeoutSeconds` (30);
the provider copies it when built. An `OllamaProviderConfig` asset holds the same fields, and
is how most games choose one: see [Provider config assets](#provider-config-assets) below.

Each decision is one `POST {Endpoint}/api/chat`, sending what the prototype sent wherever a
measurement in Appendix A depends on it:

| Field | Value |
|---|---|
| `model` | `OllamaSettings.Model` |
| `messages` | One `system` message, the non-empty system-side blocks joined by a blank line (left out if there are none); then `History`; then `Stimulus` as the last `user` message |
| `format` | `JsonSchemaSerializer`'s output (#9), embedded as a JSON object exactly as written |
| `stream`, `think` | `false`, `false` |
| `options` | `temperature` from the settings, `num_ctx` from the settings |
| `logprobs`, `top_logprobs` | `true`, `5` |

`History` is sent in the encoding `DecisionRequest.History` documents (#90): entries alternate
`user` and `assistant`, starting with `user`, each exchange being the stimulus as it was sent
and the decision as JSON.

From the reply, `message.content` becomes `Text` exactly as received, `prompt_eval_count` and
`eval_count` the token counts (0 when Ollama leaves them out), `logprobs` the token
probabilities (null when it sends none), and `model` the new `ProviderResult.Model`.
`LatencySeconds` is wall-clock time from sending to the reply.

Failures throw `ProviderException`:

| What happened | Kind |
|---|---|
| Connection refused, or no server at that address | `Unreachable`, naming the endpoint |
| HTTP 404 with an `error` saying the model was not found, as 0.34.2 answers: `{"error":"model 'x' not found"}` | `ModelNotFound`, with the `ollama pull` command |
| No reply within `TimeoutSeconds`, and the caller did not cancel | `Timeout` |
| Any other 4xx, including a 404 without that `error`, which is not Ollama's chat endpoint | `Rejected`, with the status code and Ollama's `error` |
| Any 5xx | `ServerError` |
| A 2xx whose body is not one JSON object with a `message.content` string, or whose counts or `logprobs` are the wrong shape | `MalformedResponse` |

The caller's cancellation aborts the HTTP request and throws `OperationCanceledException`,
even if the timeout has also run out. An address with a user name or password in it is named
in messages without them.

**Transport.** `System.Net.Http.HttpClient`, one per provider, behind an
`HttpMessageHandler` the EditMode tests replace to capture the body and script Ollama's
replies, so they run with no server. It needs no package, so `package.json`, CI and DR-009
are unchanged. Call `RequestAsync` from the main thread: nothing on the way back uses
`ConfigureAwait(false)`, so the reply is read there and `Agent` carries on there.

**WebGL is not supported.** `HttpClient` has no sockets in a WebGL player, so the provider
fails there. Ollama is the development backend and cannot ship in a game anyway; in the Editor
it works whatever the build target.

**Token probabilities.** Send `"logprobs": true, "top_logprobs": 5` on every request. Five
was enough in the probe, and Ollama refuses more than 20. They come from before the mask
(checked on 0.34.2) and cost about 0.07 s a decision. Each entry of the response's `logprobs`
maps onto one `TokenProbability`: `token` to `Text`, `logprob` to `LogProbability`,
`top_logprobs` to `Alternatives`. A character split across two tokens is missing from both
tokens' text, so on a reply with such a character the joined texts differ from
`message.content`. Ollama's `bytes` field does not recover it, because Ollama rebuilds it
from the already cut text. `Text` is still `message.content`, as received. When such a
character comes before the end of the target, the grounding guard records the reply as
`Unreadable` and passes it, rather than guess where the target is.

Ollama cannot ship in a game — the player would have to install and run a separate server —
which is why it is the development backend and not the answer.

### Cloud APIs — optional, and plural by design

The goal is not "support Gemini" but "support cloud APIs generally". Gemini is first because
it is what the prototype measured against.

Gemini takes JSON Schema, and differs from Ollama in four ways that are all visible in
`ProviderCapabilities`:

- **It rejects `""` as an enum value** with HTTP 400. This is half of why the `no_target`
  sentinel exists — the other half being that it gives the model a way to *say* "what you
  asked for isn't here". The framework never emits an empty enum value for any provider, so
  `RejectsEmptyEnumValues` changes nothing at runtime; it exists so a serializer can assert
  its own output and the Phase 6 conformance suite has something to check.
- **It needs `propertyOrdering` stated explicitly** rather than inferred. Field order —
  `action`, then `target`, then `statement` — is worth +11.7 accuracy points on its own, so
  a dialect that will not infer it must be told.
- **It is rate-limited**, and a key is involved. Keys go in headers, never URLs, and are
  redacted from error output (hard rule 1).
- **It reports token probabilities on some models only.** A Gemini provider sets
  `ReportsTokenProbabilities` from the model it was configured with, and only once someone
  has checked that Gemini's numbers come from before its schema constraint. Until then, the
  prompt carries the one-line fallback rule instead of the guard (build plan §2.5).

Budget caps and throttling are enforced in code, not convention — in a shared cloud-provider
base class rather than per vendor, since the difference between vendors is the numbers.

### In-process (LLMUnity) — shipping

The model runs inside the game process; the player installs nothing. This is the provider
that makes "local-first" true rather than aspirational, and it is the one the framework
actually ships behind.

The interesting difference is the dialect. llama.cpp — and so LLMUnity — wants **GBNF
directly** and will not convert a JSON Schema for you. So this provider reads
`Capabilities.Dialect == Gbnf` and runs a different serializer over the same
provider-neutral `DecisionSchema`, emitting the same constraints in grammar form: the masked
action enum, the target enum with its `no_target` sentinel, and the field ordering. Budget
that as real work in Phase 6b — roughly a day plus tests — not as a free adapter.

Keeping `DecisionSchema` provider-neutral is exactly what makes this a second *serializer*
rather than a second schema *system*. Do not let grammar syntax leak back into the model.

**Token probabilities are not known yet.** LLMUnity's `LLMClient` has an `nProbs` setting,
0 to 10 in its Inspector, not yet tried. Whether llama.cpp reports the probabilities before
or after its grammar is unclear, and the grounding guard needs before. So
`ReportsTokenProbabilities` stays false, and the prompt carries the one-line fallback rule,
until Phase 6b checks it. This is the provider the framework ships behind, so it is the check
that matters most (build plan §2.5, DR-016).

LLMUnity is not a hard dependency: it is distributed through OpenUPM, which would force
every developer to add a scoped registry even if they only ever use Ollama. It ships behind
a version define or as a separate package, pinned to an exact version.

**A diagnostic worth keeping:** when in-process results look wrong, run the identical
decision through Ollama. Same model, same schema, different transport — if they disagree,
the bug is in the GBNF serializer or the provider wrapper, not in the framework.

## Provider config assets

A provider can be built in code, as above, but most games choose one once, as an asset
(DR-013, #92). **Create → Agenerela → Ollama Provider** makes an `OllamaProviderConfig`, named
`OllamaLocal` by default, holding `OllamaSettings`' fields with the same defaults, plus
`MaxInFlight`. **Project Settings → Agenerela** names the project default, saved in
`Assets/Agenerela/Resources/AgenerelaSettings.asset`, which the page creates the first time a
default is set. Several configs can coexist, such as `OllamaLocal` and `GeminiFlash`; an A/B
run needs two alive at once, and the same page names them as **Arm A** and **Arm B**
(`AgenerelaSettings.EvaluationArmA` and `EvaluationArmB`), which agents never read
([evaluation.md](evaluation.md#running-an-ab)).

```csharp
// The agent's own config if it has one, otherwise the project default.
var agent = new Agent(profile, ProviderConfig.Resolve(ownConfig).Provider);

// The project default alone.
var guard = new Agent(profile, ProviderConfig.Resolve().Provider);
```

- **One provider per config.** `ProviderConfig.Provider` builds the provider the first time it
  is read and returns that same instance after, so every agent on one config shares one
  provider, and so one queue (#89). `MaxInFlight` (default 1) is that queue's limit.
- **Resolution** is the agent's own config, then the project default. `AgentBehaviour`'s
  **Provider** field is that own config ([agents-in-a-scene.md](agents-in-a-scene.md)). A destroyed
  config counts as none. With neither, `Resolve` throws `InvalidOperationException`: "Create one with Create →
  Agenerela → Ollama Provider, and set it in Project Settings → Agenerela". DR-013's middle
  level, a scene override, is left out until a demo needs one.
- **A built player finds the default** with `Resources.Load`, with no scene and no
  `AssetDatabase`, and the build carries the config it names.
- **Telemetry** records the config's asset name in `DecisionTelemetry.ProviderConfig`, however
  the provider reached the agent; it is empty for a provider built in code.
- **Edits.** Outside Play mode, an Inspector edit drops the built provider, and the next read
  builds one with the new values. During Play, a new `MaxInFlight` reaches the live queue at
  once, and every other field waits for the next Play: rebuilding mid-Play would split one
  config's agents across two queues. The provider is dropped as Play mode starts and as it ends.
- **An invalid config** (an empty model, `MaxInFlight` 0) throws `InvalidOperationException`
  naming the config and the setting when its provider is first read.
- **Play mode without a domain reload** (Enter Play Mode Options) keeps every static field as
  the last session left it. The framework resets its own as Play mode starts:
  `AgenerelaSettings` forgets the settings asset it found, and the built providers are dropped
  as above. Each static that needs no reset says why in `Tests/Editor/DomainReloadTests.cs`,
  which fails on any new static field nothing resets (#190, G8).

### Adding a provider config

A config for another backend is one class:

1. Derive from `ProviderConfig`, sealed, with
   `[CreateAssetMenu(menuName = "Agenerela/<Backend> Provider", fileName = "<Name>")]`.
2. Add the backend's settings as public serialized fields, each with a `[Tooltip]`, defaulting to
   the provider's own defaults (`OllamaProviderConfig` reads them from a `new OllamaSettings()`).
3. Override `CreateProvider()` to return a **new** `ILLMProvider` built from those fields, and
   throw `ArgumentException` for a setting out of range; `Provider` reports it with the config's
   name.

The base does the rest: it builds the provider once, applies `MaxInFlight` to its queue,
records the config's name for telemetry, and drops the provider after edits as above. A
subclass that needs `OnValidate` overrides it and calls `base.OnValidate()`.

**Never serialize an API key** (hard rule 1): a config asset is committed, and ships inside the
build. Serialize where the key is read from, such as an environment variable's name, and read
the key in `CreateProvider()`. Nor may a key appear in an exception message.

## The scripted provider — no model at all

`ScriptedProvider` answers each request with a reply the developer scripted, after a latency of
their choosing (#190, G6). It is for tests, headless captures, and presenting a game with the
model off. Every game built on the framework had written its own before it existed.

```csharp
var scripted = new ScriptedProvider()
    .When("Bram", "Follow me.", ScriptedReply.Decide("follow_player", statement: "Right behind you."))
    .When("Wren", "Follow me.", ScriptedReply.Decide("none", statement: "I stay at the well.").After(1.2f))
    .When(null, "Thief!", ScriptedReply.Decide("chase", "thief"))   // any agent
    .Otherwise(ScriptedReply.Decide("none", statement: "Hm."));       // anything else

var bram = new Agent(bramProfile, scripted);
```

**It never passes for a model.** Its `Name` is always `"scripted"`, and so is every reply's
`ProviderResult.Model`, so `DecisionTelemetry.ProviderName` and `Model`, an evaluation report and
any HUD say "scripted" wherever they would name the model, even when a reply replays a model's
text word for word. A `ScriptedReply` has no model and no token counts to set.

**Which reply answers.** The agent is matched by `AgentIdentity.Name`, and the stimulus by the
exact text passed to `DecideAsync`, without its label, both case-sensitive. A decision request
takes the first of:

1. the replies scripted for this agent and this stimulus, `When(agent, stimulus, …)`;
2. those for this stimulus from any agent, `When(null, stimulus, …)`;
3. those for this agent with any stimulus, `When(agent, null, …)`;
4. `Callback`, when it returns a reply rather than null;
5. `Otherwise(…)`, which is `When(null, null, …)`.

A request with no agent name or an empty stimulus skips the stages that need one, so the callback
still comes before `Otherwise`. Several replies for one key answer in order, and the last keeps answering. Scripting a key again
replaces it and starts it over; `Clear()` forgets every key. A request nothing answers fails with
a `ProviderException` of `Rejected` that names the agent and the stimulus, so a test that asks
something it did not script finds out at once.

**The callback** is for rules rather than a table, such as a game's keyword lines. It is given a
`ScriptedRequest`: the agent, the stimulus and its label, `OfferedActions` and `OfferedTargets`
from the schema (so a rule can answer only with what the agent may do now), and the
`DecisionRequest` itself. Returning null leaves the request to `Otherwise`.

**Replies.** `ScriptedReply.Decide(action, target, statement)` writes the schema's JSON object,
fields in their order, the statement escaped. Nothing checks that the action is one the agent
offers: an illegal reply shows a guard stopping it. `Raw(text)` sends text exactly, a reply
that does not parse included. `Fail(kind)` fails as a provider does: `Unreachable` for a
stopped server, `Timeout` for a slow one. `Never()` stays pending until the request is
cancelled, for testing a game's own timeout. `After(seconds)` adds a latency to any of them, and
`WithTokens(…)` gives a reply token probabilities for the grounding guard to read.

**Latency** is wall-clock time, waited before the reply arrives, so `DecisionTelemetry.LatencySeconds`
records it as it would a model's. It arrives on the main thread. Cancelling the request ends the
wait at once. A reply with no latency answers within `RequestAsync`, so
`await agent.DecideAsync(…)` completes in the call.

**A refusal line** (`DecideOptions.AskForRefusalLine`, [guards.md](guards.md)) takes no keyed
reply, so it never uses up a key's next one: `Callback` answers it (`ScriptedRequest.IsRefusalLine`
is true), then `RefusalLine`, usually a `ScriptedReply.Statement(…)`. With neither, it fails and
the decision stands without a line.

**Capabilities.** By default it claims only what a script can honestly do: no constrained
decoding and no token probabilities, so with `CheckTarget` on the prompt carries the target rule,
as for any provider without them (#107). Pass Ollama's capabilities to
`new ScriptedProvider(capabilities)` to receive exactly Ollama's prompt.

### As an asset

**Create → Agenerela → Scripted Provider** makes a `ScriptedProviderConfig`, which a scene, an
`AgentBehaviour` or Project Settings → Agenerela selects like any provider config. Each entry
of **Replies** answers an agent and a stimulus with one decision and a latency; a blank agent
answers every agent and a blank stimulus every stimulus, so an entry with both blank is the
fallback, and a new asset has one that does nothing and says nothing. Entries for the same agent
and stimulus answer in list order. **Refusal Line** answers refusal-line requests. Anything an
entry cannot say, such as a failure or a callback, goes through `config.Scripted`, the
`ScriptedProvider` the config built:

```csharp
var config = (ScriptedProviderConfig)settings.DefaultProvider;
config.Scripted.Otherwise(ScriptedReply.Fail(ProviderErrorKind.Unreachable));   // "Ollama is down"
```

Inspector edits reach it as for any config: outside Play mode at once, during Play at the next
Play. Code scripting `config.Scripted` reaches it at once.

### Testing with the scripted provider

- **Script exactly what the test asks**, with `When` for each agent and stimulus, and no
  `Otherwise`: anything else fails with the agent and stimulus named, rather than answering
  quietly.
- **Every agent on one provider shares its queue**, as on a real config. To test a request that
  stays with the provider while something else happens, set `Hold`; each request then waits,
  its reply chosen, until `Release()` lets the oldest go on. `HeldCount`, `RequestCount` and
  `LastRequest` say what reached it.
- **Failures and timeouts** are replies: `Fail(ProviderErrorKind.Unreachable)` for a stopped
  server, `Never()` for a hung one, `.After(seconds)` for a slow one.
- **A test agent's name is its key.** Two agents with the same `AgentIdentity.Name` share their
  scripts; rename one in the test, as the framework's own tests do.

### Replaying a decision log

Not built yet: it comes with the runtime decision log (#190, G7). A log line holds an agent, a
stimulus, the reply's text and its latency, which is exactly a `When` entry, so a replay reads
the log and scripts `When(agent, stimulus, ScriptedReply.Raw(text).After(latency), …)` with each
pair's lines in log order. Nothing in the provider has to change for it, and the replay says
"scripted" in telemetry like every other scripted reply.

## The question that proves the interface works

*What would OpenAI or Anthropic need that is not already in the table?* If the answer
requires changing `ILLMProvider`, the interface is not done.

Working through both — vendor behaviour as of writing, to be confirmed when Phase 6
implements them:

**What they need that is already covered.** Both take a JSON-Schema-shaped description of
the answer, so `Dialect` stays `JsonSchema`. Both preserve the property order they are
given, so `NeedsExplicitPropertyOrdering` stays false. Both constrain generation, so the
guard pipeline is a second line of defence rather than the only one. Both are rate-limited
and need a key in a header. None of that touches the interface.

**What they need that is not.** One real gap: **both throttle on tokens per minute, not
only requests per minute.** A long few-shot block can exhaust a token budget while the
request count is still comfortably inside its limit, so a scheduler pacing purely on
requests will hit 429s it did not predict. Anthropic splits it further, into input and
output tokens per minute.

That is a field on `RateLimit` — a supporting type — not a change to `ILLMProvider`. Which
is the answer the question was asking for: the interface absorbed a vendor it was not
designed against. It is left unadded until Phase 6, because adding a throttling dimension
nothing enforces yet would be a guess about how the Phase 6 scheduler wants to read it.

**Two smaller ones, both implementation-level.** OpenAI's strict structured-output mode
requires every object to carry `additionalProperties: false` and to list every property in
`required` — a constraint on what the JSON Schema serializer emits, satisfiable inside the
provider or as a serializer option. Anthropic commonly reaches structured output through
tool use rather than a response-format field, so the schema is placed differently in the
request body; it also requires `max_tokens` on every call. Both live entirely inside a
provider class.

**Where the interface is coarser than reality.** `SupportsConstrainedDecoding` is a boolean,
but real backends span native grammars, tool-call coercion, and prompt-only "please reply
in JSON". The boolean is enough today because the guard pipeline runs identically in every
case; if a scheduler ever needs to prefer one mode over another, that becomes an enum on
`ProviderCapabilities` — again a supporting type, not the interface.

## Cancellation and failure

**Cancellation is part of the contract**, not a nicety. The agent a decision was asked for
can die, the scene can unload, the player can walk away mid-generation. Implementations pass
the token down to the transport — abort the HTTP request, stop the in-process generation —
and let `OperationCanceledException` propagate. Do not return a partial or empty
`ProviderResult` on cancel: a cancelled decision has no result, and one returned silently
would be scored as a wrong answer.

**Failures throw; results mean a reply arrived.** Server down, model not pulled, HTTP 429,
malformed envelope — all exceptions, so nothing downstream has to tell "no result" from
"empty result". Throw `ProviderException` (#87), whose `Kind` says why — `Unreachable`,
`ModelNotFound`, `Timeout`, `Rejected`, `ServerError` or `MalformedResponse` — so a missing
model surfaces as a diagnosable error rather than a leaked transport exception. Any other
exception still counts as a failure, with no reason given. API keys must never appear in an
exception message.

**What the agent does with each.** `Agent.DecideAsync` (#17) turns a thrown failure into a
result with no decision and the outcome `PipelineError`, logging the exception's type and
message as a warning, so a game's decision loop keeps running and the evaluation harness can
count it. A reply that arrived but cannot be read, empty or not one JSON object, is the same
outcome. Either way the result carries the request sent in `DecisionResult.Request`, and a
reply that arrived in `DecisionResult.Reply`, so a game can show why it could not be read (#142);
a request that could not be prepared was never sent, and leaves both null. A cancellation
propagates only when the caller's own token was cancelled; an
`OperationCanceledException` the caller did not ask for, such as a transport's own timeout, is
a failure like any other.

## Provider health

Whether a provider is answering is something every game ends up showing ("the model is not
running"), and GreyBoxVillage and Living Vale both built it for themselves. `ProviderHealth`
does it once, per provider instance, like the queue (#190 G1, DR-015 as amended):

```csharp
var health = ProviderHealth.For(agent.Provider);
health.Changed += h => hud.ModelProblem = h.IsDown ? h.Message : null;
health.BackOff = true;   // optional: hold background requests while it is down
```

- **What moves it.** `Agent` reports every request it sends. Any reply marks the provider up,
  even one that cannot be read, because a model answered. A provider failure marks it down:
  `Reason` is the `PipelineErrorKind`, `Message` the provider's own words, `DownSinceUtc` when
  the run of failures began, `ConsecutiveFailures` how many in a row. A cancelled request reports
  nothing, and neither do the model lifecycle calls below.
- **The retry time.** `RetryAtUtc` is 5 s after the first failure, doubling with each one after
  it, up to 60 s (`FirstRetrySeconds`, `LongestRetrySeconds`). It is set whether or not the
  back-off is on, so a HUD can show it.
- **`BackOff`, off by default.** When on, a background request whose turn in the queue comes
  while the provider is down, before the retry time, is not sent. Its result has no decision, with
  `ErrorKind` `ProviderDown`, and nothing is logged: the failure that put the provider down
  logged already. The first background request after the retry time is sent and finds out. A
  player-facing request is always sent, because someone is waiting for its answer or its error.
  It is off by default because one timeout would otherwise cost the next few seconds of
  background work, such as a country's turn in a strategy game.
- **`Changed`** is raised after any report that changes the state: going down, failing again
  while down (a new reason or retry time), coming back. `Agent` raises it on the main thread,
  so a handler may touch the scene; a handler that throws is logged and the decision goes on.
  `Reset()` marks the provider up by hand, such as when the player says the server is back.
- **`IsProviderFault(kind)`**, an extension on `PipelineErrorKind`, tells a game which failures
  to show as "the model is not answering" and which belong to one decision. True for no
  server, no model, a timeout, a refusal, a server error, an unreadable envelope, any other
  provider failure, and `ProviderDown`. False for a request that could not be prepared, a reply
  that cannot be read, a guard that threw, and a call the backlog dropped or refused.

The outcome is recorded before the request's slot in the queue is freed, so the request that
slot goes to is judged by it, even when the provider answered on another thread.

## Model lifecycle

A game that offers a choice of model has to list what the server has, load the chosen one,
and unload the one left behind. A provider that can do this implements `IModelLifecycle`
(DR-018); `OllamaProvider` does. A provider that does not has nothing to manage, so a game
asks first:

```csharp
if (provider is IModelLifecycle models)
{
    var installed = await models.ListModelsAsync(ct);   // ModelInfo: Name, SizeBytes
    await models.WarmUpAsync(ct);                        // loads this provider's model now
    bool unloaded = await models.UnloadAsync(ct);        // out of memory now
}
```

| Call | Ollama | Returns |
|---|---|---|
| `ListModelsAsync` | `GET /api/tags` | The pulled models, named with their tags (`qwen3.5` lists as `qwen3.5:latest`), sizes on disk in bytes |
| `WarmUpAsync` | `POST /api/generate` with only `model`, which loads it and generates nothing | When the model is in memory |
| `UnloadAsync` | `POST /api/generate` with `keep_alive: 0`, then `GET /api/ps` every 0.25 s | True once the model is no longer listed, false if it still is after 15 s |

- **None of them is a decision**, so none goes through the decision queue: a warm-up does only
  what the first decision on that model would have to do anyway, so it never holds a slot a
  decision is waiting for. Living Vale's warm-ups were decisions, and the player's turns timed
  out behind them (DR-015, as amended).
- **Failures throw `ProviderException`**, as `RequestAsync` does: `Unreachable` when nothing
  answers, `ModelNotFound` with the `ollama pull` command when the server lacks the model,
  `Timeout` after `TimeoutSeconds`, and `MalformedResponse` for a reply that is not Ollama's.
  They do not report to `ProviderHealth`, which follows decisions.
- **Cancelling** the token aborts the request and throws `OperationCanceledException`. That is
  how a warm-up a newer one replaces is stopped: the framework keeps no list of warm-ups.
- **Each call acts on the provider's own model.** Its settings are copied when it is built, so
  a provider never changes model, and its telemetry and its queue never mix two.

Phase 5's provider inspector (#173) fills its model dropdown from `ListModelsAsync`. Its
connection check is its own.

## Switching an agent's provider

`Agent.Provider` can be set (DR-018). The next decision goes to the new provider, and the agent
keeps everything else: its memory, actions, targets, sources, guards and state. A decision
already under way finishes on the provider it started with, its refusal line included. To move
a village of NPCs to another Ollama model:

```csharp
var next = new OllamaProvider(new OllamaSettings { Model = "gemma4:e2b-it-qat" });
await next.WarmUpAsync(ct);                       // before anyone has to wait for it
foreach (var npc in agents) npc.Provider = next;  // one instance, so one queue for all of them
await ((IModelLifecycle)previous).UnloadAsync(ct);
```

The new provider has its own queue and its own health, shared by every agent given that same
instance. A request already queued on the old one is sent there. For a scene agent, set
`AgentBehaviour.Agent.Provider`; its **Provider** field is read once, when the agent is built.

## Busy agents: the backlog

By default an agent decides every call at once, side by side: two perception events in a row
send two requests, and the second is prepared before the first is remembered. A game that wants
one decision per agent at a time sets `Agent.Backlog` (#190 G5, DR-015 as amended):

```csharp
guard.Backlog = DecisionBacklog.LatestWins;      // one may wait; a newer call takes its place
guard.Backlog = DecisionBacklog.DropOldest(3);   // three may wait; the oldest makes way
guard.Backlog = DecisionBacklog.QueueWithCap(3); // three may wait; the newest is refused
guard.Backlog = new DecisionBacklog(0, BacklogOverflow.RefuseNewest); // none while it decides
```

- **One at a time.** A call made while the agent decides waits in the agent, not in the
  provider's queue, player-facing before background and first come, first served within each.
  It is prepared only when its turn comes, so it sees the targets and state of that moment and
  the memory of the decision before it.
- **When it is full**, `DropOldest` drops the oldest waiting call that is not more urgent than the
  new one, a background one before any player-facing one; a background call never pushes out a
  player-facing one, and is refused instead. `RefuseNewest` refuses the new call. A dropped call
  ends with `ErrorKind` `Superseded`, a refused one with `BacklogFull`; both have no decision,
  were never sent, and log nothing.
- **The decision under way is never dropped.** Its own token stops it, while it waits in the
  provider's queue or once it is sent, and the next call's turn comes at once. A cancelled
  waiting call leaves the backlog.
- **What it records.** `Agent.IsDeciding` is true while a decision is under way, `Agent.Waiting`
  counts the calls behind it, and `DecisionTelemetry.BacklogWaitSeconds` is how long a call
  waited for its turn, apart from `QueueWaitSeconds`, its wait in the provider's queue.
- **It counts only calls made while it is set**, and is read when each call is made. It never
  asks why the agent was called (DR-008): what it keeps or drops follows only the order of the
  calls and their `Priority`.

## Still open

- **Token-based rate limits**, above. Phase 6.

## Extension points — where the next things go

Each of these is a new method, a new field on a supporting type, or a new class. None of
them is a change to `ILLMProvider`, which is the test this design has to keep passing.

- **Streaming** (`IAsyncEnumerable` of tokens) for showing a statement as it arrives. Not
  needed for decisions — add a second method when a demo wants it.
- **Batching** several agents' requests into one call. Strategy-demo scale, Phase 6+.
- **Token budgets and cost tracking** per session, enforced in the cloud provider base
  class. Phase 6.
- **A `keep_alive` setting** for how long Ollama keeps a model after the last request. Every
  request leaves Ollama's default of five minutes; `UnloadAsync` is the way to free it sooner.
  The A/B runner's reads of `/api/version` and `/api/ps` stay internal until a game needs them.
- **`ProviderHealth` from lifecycle calls.** A warm-up that succeeds could mark a provider up.
  Left out so health follows decisions alone; add it if a game warms up to recover.
- **A setting for a model that rejects `think`**, when one is adopted. `OllamaProvider` always
  sends `think: false`.
- **A provider conformance suite** every vendor must pass. Phase 6 deliverable, shaped from
  the fake provider in #17 (`Tests/Editor/Fakes/FakeProvider.cs`).
- **A scene override** between an agent's own config and the project default, DR-013's middle
  level: a field `ProviderConfig.Resolve` reads second. Add it when a demo needs one.
- **A session budget and rate limits** per config, beside `MaxInFlight`, applied to the same
  queue. Phase 6, with the first cloud config.
