# Providers — how three very different backends fit behind one interface

`ILLMProvider` exists so that "which model runs this" is a configuration choice rather than
a framework edit. This note explains how each planned backend satisfies it, what a *second*
cloud vendor would need, and which parts of the contract are deliberately still open.

Nothing here is implemented yet. Phase 1 defines the contract; Phase 2 writes the Ollama
provider, Phase 6 the cloud ones, Phase 6b the in-process one.

## The interface

```csharp
public interface ILLMProvider
{
    string Name { get; }
    ProviderCapabilities Capabilities { get; }
    Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct);
}
```

A provider takes an assembled request, sends it, and returns text. It does not parse the
reply, does not know what an action is, and does not decide whether the answer was legal.
`Agent` (#17) parses `ProviderResult.Text` and builds `DecisionTelemetry` (#3) from the rest.

Two rules sit above every implementation:

- **A provider is untrusted.** `Capabilities` reports what a backend claims; the validation
  pipeline (Phase 3) runs over every decision regardless. A capability flag must never
  switch a guard off. A plain-chat backend with no constrained decoding is a legal
  provider — it is just likelier to return something unparseable, which the pipeline
  classifies rather than crashes on.
- **A provider never rewrites the prompt.** The blocks in `DecisionRequest` arrive rendered
  and go out unchanged. Prompt wording is measured; two of the three regressions caught by
  the prototype's A/B harness were wording changes, and a provider quietly improving a
  block would be invisible in the numbers.

## The three backends

| | Ollama | Cloud (Gemini) | In-process (LLMUnity) |
|---|---|---|---|
| Job | Development | Optional / comparison | Shipping |
| Constrained decoding | yes | yes | yes (GBNF) |
| Explicit ordering | no | **yes** | no |
| Dialect | `JsonSchema` | `JsonSchema` | **`Gbnf`** |
| Rate limit | none | **~15/min, 1000/day** | none |
| Rejects empty enum values | no | **yes** | no |

Read as `ProviderCapabilities`, the middle column is the only one that is not all defaults —
which is the point. Gemini is the vendor whose quirks the prototype actually hit, so it is
the worked example and doubles as the conformance reference for the next vendor.

### Ollama — development

An HTTP server on `localhost`. The schema goes in the request's `format` field as JSON
Schema and Ollama compiles it to a sampling grammar internally, so property order is
inferred from the schema and needs no separate statement. No quota, no key, no budget.

`Capabilities`: constrained decoding yes, explicit ordering no, dialect `JsonSchema`,
`RateLimit` null, rejects empty enum values no.

Notes carried over from the prototype's client, for whoever writes this in Phase 2: keep it
non-blocking, send `think:false` for Qwen-family reasoning modes, make `num_ctx`
configurable, and read latency and token counts out of the response envelope rather than
timing from outside where you can.

Ollama cannot ship in a game — the player would have to install and run a separate server —
which is why it is the development backend and not the answer.

### Cloud APIs — optional, and plural by design

The goal is not "support Gemini" but "support cloud APIs generally". Gemini is first because
it is what the prototype measured against.

Gemini takes JSON Schema, and differs from Ollama in three ways that are all visible in
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

LLMUnity is not a hard dependency: it is distributed through OpenUPM, which would force
every developer to add a scoped registry even if they only ever use Ollama. It ships behind
a version define or as a separate package, pinned to an exact version.

**A diagnostic worth keeping:** when in-process results look wrong, run the identical
decision through Ollama. Same model, same schema, different transport — if they disagree,
the bug is in the GBNF serializer or the provider wrapper, not in the framework.

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
"empty result". Phase 2 adds the typed provider exception, so a missing model surfaces as a
diagnosable error rather than a leaked transport exception; until then, throwing is still
the contract. API keys must never appear in an exception message.

## Still open

- **`DecisionRequest.Schema`** is not on the type yet — `DecisionSchema` (#8) does not exist.
  The field is marked with a `TODO(#8)` and lands with that issue. Treat it as required once
  it does: a request without a schema is not a decision request.
- **How a turn in `DecisionRequest.History` encodes its speaker** is decided in Phase 2,
  alongside the memory strategy that will fill it. The list is empty until then, so no
  provider can be wrong about it yet — but do not invent an encoding and read it back.
- **Token-based rate limits**, above. Phase 6.

## Extension points — where the next things go

Each of these is a new method, a new field on a supporting type, or a new class. None of
them is a change to `ILLMProvider`, which is the test this design has to keep passing.

- **Streaming** (`IAsyncEnumerable` of tokens) for showing a statement as it arrives. Not
  needed for decisions — add a second method when a demo wants it.
- **Batching** several agents' requests into one call. Strategy-demo scale, Phase 6+.
- **Token budgets and cost tracking** per session, enforced in the cloud provider base
  class. Phase 6.
- **Health and warm-up** (`IsReady`, is the model loaded?). Phase 2, for the Ollama
  provider's missing-model case.
- **A provider conformance suite** every vendor must pass. Phase 6 deliverable, shaped from
  the fake provider in #17.
