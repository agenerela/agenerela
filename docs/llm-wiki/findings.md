# Findings

Measurements taken **in this repository**. Results carried over from the predecessor
prototype live in the build plan's **Appendix A** — they are not duplicated here, because
two copies of a number drift apart.

Every entry needs: what changed, control vs treatment with n, the conditions (model,
quantisation, hardware, other models resident), and what would change your mind. See
[README.md](README.md) for the rule.

---

## Inherited, unresolved — carry these forward

Open questions the prototype could not close. Whoever addresses one should record the
answer here.

### Latency differs between Unity and standalone scripts, unexplained

The same model measured **~1.31 s** mean latency inside Unity (while URP rendered a
180-prop scene) but **~3.1–3.5 s** from standalone Python hitting the same Ollama server
on the same machine, with the model correctly unloaded and reloaded between runs. That is
backwards from expectation — Unity should be the *more* contended environment.

Never root-caused. Until it is, treat *relative* comparisons within one environment as
valid and be sceptical of absolute latency numbers quoted across environments.

### Every accuracy number was measured through Ollama's HTTP API

Appendix A's results all came via Ollama. Once the in-process provider exists
(build plan Phase 6b), the same eval set must be re-run through it. Constrained decoding
goes through a different path there — GBNF supplied directly rather than a JSON Schema
compiled by Ollama — so the numbers do not automatically transfer. When telemetry's "schema
mode" field lands (see [New findings](#new-findings) below), record it alongside that re-run
so a regression can be told apart from decoding simply going unenforced.

### Statistical power

The prototype's eval set was n=53 with a single annotator. Differences under ~10 points
were not detectable. Building the 200+ prompt, multi-annotator set is Phase 4 and gates
the credibility of everything measured afterwards.

### Few-shot's +35 points is one measurement of one design

The generated few-shot block was the largest single lever the prototype measured: **+35.3
points** on its own, and part of the arm that went from 58.5% to 84.9% together with the
constrained schema (build plan, Appendix A). The combined arm ran at n=53 per arm on one 2B
model with greedy decoding; Appendix A gives no sample size for the isolated figure.

It measured the block as a whole. The rules inside it were never isolated: one example per
available action, with a target the verb sensibly applies to, then an idle example and a
negative example with `no_target` (build plan §2.3, rule 4; #10). The sensible-target rule
came from reading bad output ("Pick up the Blacksmith"), not from an A/B run. Treat these
as starting rules. Phase 4 should re-measure the block and try levers the prototype never
measured: how many examples, their order, examples picked by similarity to the stimulus, and
a hand-written block per profile (#10's extension points).

The design already leaves room for that. `DecisionRequest.FewShotBlock` is its own field, so
an A/B arm can swap or drop the block while the rest of the prompt stays byte-identical.
Give `FewShotBuilder` (#10) a parameter when an experiment needs one, not before.

---

## New findings

*Still nothing measured.* Phase 1 is pure C# with no model in the loop — its correctness is
settled by EditMode tests, not by measurement — so the first entry here arrives with Phase 2,
when a real provider answers a real request. Until then, every number about this project
comes from the build plan's Appendix A and was measured in the predecessor prototype, not
here.

### Telemetry now exists to catch Phase 2's numbers, with one field deliberately missing

`DecisionOutcome`, `DecisionTelemetry`, and `DecisionResult` merged (issue #3, PR #36):
latency, prompt/completion tokens, provider name, fired guards, and an outcome classification
(`Correct`, `WrongLegalAction`, `ContainedByGuard`, `RejectedWhenActionExpected`,
`PipelineError`). This is where Phase 2's first real numbers land — not a finding itself.

`DecisionResult` was briefly commented out on `test`: it wraps `AgentDecision`, which
issue #2 adds and which had not merged yet, so the package had stopped compiling. It came
back once #2 landed (PR #39); nothing else depended on it in the meantime, so nobody using
telemetry was blocked.

Left out on purpose: **schema mode** — whether the response was actually constrained, or the
model merely saw the schema as prompt text and could ignore it. `ProviderCapabilities`
gained a `SchemaDialect` enum (`JsonSchema` / `Gbnf`) in the same window, and it is the wrong
type to reach for here: a dialect is the *format* the schema is written in, not whether it
was *enforced* — a provider with `SupportsConstrainedDecoding == false` still reports one.
Schema mode needs a third state ("not enforced") derived from both fields, so it waits for a
real provider in #3's extension points rather than being bolted onto `SchemaDialect`.

Why it's worth carrying forward rather than shrugging off as a naming nit: enforcement was
half of the prototype's biggest measured gain — its constrained-schema arm, combined with
few-shot examples, went from 58.5% to 84.9% (build plan, Appendix A). Once schema mode lands
in telemetry, it should be the first thing cut against when a Phase 2 run looks off — a model
answering "correctly" without constrained decoding is a different (weaker, cheaper-to-break)
result than one that was actually constrained, and today nothing in the pipeline can tell the
two apart after the fact.
