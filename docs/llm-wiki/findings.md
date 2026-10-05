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

### Latency differs between Unity and standalone scripts — most likely `localhost`

The same model measured **~1.31 s** mean latency inside Unity (while URP rendered a
180-prop scene) but **~3.1–3.5 s** from standalone Python hitting the same Ollama server
on the same machine, with the model correctly unloaded and reloaded between runs. That is
backwards from expectation — Unity should be the *more* contended environment.

**Answer, 5 October 2026: the Python scripts paid about 2 s a request to reach Ollama.**
They called `http://localhost:11434`. On Windows `localhost` resolves to `::1` first, and
Ollama listens on `127.0.0.1:11434` only, so every request waited for the IPv6 attempt to
fail before falling back. Measured with the same tiny request (`qwen3.5:2b`, 5 tokens out),
six times each, alternating, from Python's `urllib`:

| Endpoint | Client time, mean | Ollama's own time, mean |
|---|---|---|
| `http://localhost:11434` | 2.26 s | 0.22 s |
| `http://127.0.0.1:11434` | 0.14 s | 0.12 s |

Conditions: Windows 11, Ollama 0.34.2, RTX 4060 Laptop on AC power, one model resident. The
~2.1 s offset matches the old gap. Unity was not re-measured, so this is the likely cause,
not a proven one. If `UnityWebRequest` resolves `localhost` the same way and Unity still
measures ~1.3 s, there is a second cause.

What changed: the probes in `tools/benchmarks/` now call `127.0.0.1`. `OllamaProvider`'s
default endpoint should be `127.0.0.1` too (build plan §2.4). Absolute latencies from the old
scripts are inflated by ~2 s; relative comparisons within one run are unaffected.

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

### Few-shot's +35 points is one small measurement of one design

The few-shot block was the largest single lever the prototype measured (build plan,
Appendix A). Where its numbers come from, per Feasibility Report II (27 August 2026):

- **+35.3 points** is the gap between two of five schema variants run straight against the
  model, outside Unity, on **17 prompts** (the variant study's cases, per
  `tools/benchmarks/gemini_compare.py`). Action-first alone scored 52.9% (9/17); action-first
  with few-shot examples scored 88.2% (15/17). That is six prompts, and at n=17 one prompt
  moves the score by 5.9 points. Few-shot also lifted the reasoning-first variant, from 35.3%
  (6/17) to 76.5% (13/17).
- **58.5% → 84.9%** is the controlled A/B: 53 labelled prompts per arm, `qwen3.5:2b` (Q4)
  on an RTX 4060 Laptop through Ollama, greedy decoding, one annotator. Its treatment arm
  changed four things at once (per-request schema, state masking, few-shot and action-first
  order), so it does not say how much of the gain is few-shot's.

Never measured: the rules inside the block. One example per available action with a target
the verb sensibly applies to, then an idle example and a negative example with `no_target`
(build plan §2.3, rule 4; #10). Blind rotation through the targets was dropped mid-run after
it produced "Pick up the Blacksmith", and was never scored. Nothing ran on a second model,
scene or action vocabulary.

So the builder's rules are starting rules. **Experiments owed in Phase 4**, each an A/B with
a control arm on the 200+ prompt set, and each recorded here:

1. Few-shot on vs off, to re-measure the lift at a sample size that can see less than 10
   points.
2. The same comparison on the 0.8B and 4B models of the report's model-size study: the lift
   may shrink or grow with model size.
3. The rules one at a time: sensible vs rotated example targets, with and without the
   idle example, with and without the negative example, the number of examples per action,
   and their order. If the idle example does not help, `AgentProfile.IdleExampleStimulus`
   (#15) can go.
4. Levers never tried: examples picked by similarity to the stimulus, and a hand-written
   block per profile (#10's extension points).

The design already allows all four. `DecisionRequest.FewShotBlock` is its own field, so an
arm can swap or drop the block while the rest of the prompt stays byte-identical. Give
`FewShotBuilder` (#10) a parameter when an experiment needs one, not before.

---

## New findings

The framework itself still measures nothing. Phase 1 is pure C# with no model in the loop,
settled by EditMode tests, and its first numbers arrive with Phase 2, when a real provider
answers a real request. Entries below that carry numbers come from the standalone probes in
`tools/benchmarks/`, which ask a model directly. Everything else comes from the build plan's
Appendix A, measured in the predecessor prototype.

### Stopping a swapped target: the model's own confidence beats the name check (5 October 2026)

**What changed.** `tools/benchmarks/target_swap_probe.py` runs 109 labelled prompts through
the framework's planned prompt and schema: `action`, then a `target` enum with `no_target`,
then `statement`, with a few-shot block holding one refusal. It also runs five variants of
that prompt, and `target_swap_score.py` applies the checks afterwards. There are two invented
scenes: a village guard with five targets, and a companion with five items. The categories:

- requests for things that are not there at all, such as "Attack Godzilla." (16)
- **near misses**, the same kind of thing as one that is there, such as a scarecrow beside the
  training dummy (30)
- requests by exact name (16) and by another name, such as "Hit the mannequin." (22)
- pronouns resolved from the previous turn (4)
- game-triggered stimuli (13) and chat (8)

**Conditions.** `qwen3.5:2b` (Q8_0) and `qwen3.5:4b` (Q4_K_M) through Ollama 0.34.2, on an
RTX 4060 Laptop on AC power, one model resident. Greedy decoding, `think: false`, `num_ctx`
4096, every arm in one session. One annotator, who also wrote the arms. No few-shot example
shares a phrasing or sentence frame with a test prompt.

**Results.** Correct of 109 · wrong actions carried out · good requests refused:

| Arm | `qwen3.5:2b` | `qwen3.5:4b` |
|---|---|---|
| Control: today's prompt and schema | 73 · 29 · 7 | 98 · 10 · 1 |
| + name check, on player requests | 83 · 4 · 22 | 89 · 3 · 17 |
| + yes/no verifier request | 86 · 5 · 18 | 103 · 3 · 3 |
| **+ confidence check: chosen target below 0.8 → `none`** | **95 · 6 · 8** | **103 · 5 · 1** |
| A one-line rule in the prompt instead | 88 · 8 · 13 | 101 · 3 · 5 |
| A near-miss refusal example instead | 78 · 22 · 9 | 97 · 10 · 2 |
| A field for the player's words, + a check on it | 88 · 6 · 15 | 100 · 1 · 8 |
| A free-text target, resolved in code | 92 · 7 · 10 | 98 · 7 · 3, and one broken answer |

**How the confidence check reads it.** It takes the probability the model gave the first
token of the target it chose. Ollama returns that probability from before the schema's mask,
so it shows what the model wanted to write, which was often "scarecrow".

- **Separation.** It separates wrong targeted answers from right ones with an AUROC of 0.94 on
  the 2B (27 wrong, 37 right) and 0.997 on the 4B (8 wrong, 41 right).
- **Threshold.** Any threshold from 0.5 to 0.9 scored 92–95 on the 2B and 101–104 on the 4B.
- **False refusals.** At 0.8 the check added one false refusal on the 2B ("Go to the water
  butt.") and none on the 4B. The name check added 15 and 16, nearly all paraphrases and
  pronouns.
- **Multiplying all tokens instead.** Multiplying every token's probability does about as
  well, but it punishes a right answer whenever the mask forces a split the model did not
  want. On the 2B, "Pass me the healing draught." came out as health_potion with p ≈ 0,
  because the model wanted "he|aling".
- **Cost.** Asking for logprobs changed no decision. It added 0.08 s a decision on the 2B
  (0.68 → 0.76 s) and 0.06 s on the 4B. The verifier's second request added 0.31–0.32 s to
  every decision it checked.

**What else it showed.**
- **Near misses are the real problem.** Today's prompt already refuses most unrelated
  requests (2B 12/16, 4B 16/16) but few near misses (2B 9/30, 4B 23/30).
- **What still gets through** at 0.8: near misses the model is sure of. Spear → sword, bucket
  → barrel, lantern → torch and Excalibur → sword, all at 0.83–0.90. Some of these labels are
  judgment calls, since a designer may happily accept the torch.
- **Wrong actions on the right target are out of reach of any target check.** On the 2B,
  "That's an interesting sword you have." still picks the sword up at 0.999. Appendix A gives
  that one to model scale, and the 4B gets it right.
- **A one-line rule does help.** It cut the 2B's wrong actions from 29 to 8, against the
  literature's finding that rules barely help ([arXiv 2510.22977](https://arxiv.org/abs/2510.22977)).
  But it refuses more good requests than the confidence check, and adds nothing on top of it.
- **The words-the-player-used field did not hold the player's words.** The model wrote the
  name it had already matched: "Hit the mannequin." became "the training dummy", and "Drink
  the mana potion." became "the health potion". A check on that field inherits the swap.
- **A free-text target once ran past the token limit** on the 4B ("Attack it!"), a failure an
  enum cannot have.
- **Hard rule 4 in action.** With the refusal example "Go to Atlantis.", the 2B refused 9 of
  the 10 village requests for things that are not there. Reworded to "Visit Atlantis.", so
  that no test prompt shares its frame, it refused 7. These are two sessions, the first on
  battery.

**Update, the same day: checking every decision.** I re-scored the same results, with no new
requests. The signal was the share of the model's probability that stayed on the target list
at the start of its target (the scorer's `legal` signal). Below 0.8 → `none`, applied to every
decision rather than only to player requests. The 2B scored 95 · 6 · 8 and the 4B 103 · 5 · 1,
the same totals as the chosen-target version.

- It added one false refusal on the 2B and none on the 4B. The refused request was "Go to the
  water butt.": only 0.03 of the probability stayed on the list, because the model wanted to
  write "water".
- It refused no good game-triggered decision on either model.
- On the 4B it caught a wrong one: "A merchant waves at you from the road." made the guard
  walk to the bridge, with 0.33 on the list.
- The chosen-target version, run on every decision, blocked one good game-triggered choice on
  the 2B: "Someone dropped a weapon near the road. Secure it.", at 0.73.

DR-016 adopted the on-list share, on by default; the second run below moved it to every token
of the target. Its blind spot here is a missing thing whose name
starts like a listed one: "Pick up the spear." keeps 0.99 on the list through the "s" of
`sword`. Not tested yet: an open choice between two equally good targets, since none of the 13
game prompts was one.

**What I believe now.** The model's own confidence in its target is the best check we have
against the swap. It is free on Ollama, needs no word lists, and keeps the paraphrases and
pronouns the name check refuses. The name check cost more good requests than it saved bad ones
on the 4B. DR-016 records the decision.

**What would change my mind.** Any of these:
- Phase 4's 200+ prompt set, written by other people, shows the separation falling well below
  0.9, or the right threshold differing by scene.
- The in-process provider reports probabilities from after the grammar rather than before it.
- Confident near misses turn out common. Here they were 2 of 30 on the 2B and 3 of 30 on the 4B.

**Limits.** Small categories, where one prompt moves a 16-prompt category by 6 points. Two
invented scenes, one model family, Ollama only. Literature behind the design question:
[forced choice without the gold label](https://arxiv.org/abs/2406.16203);
["none of the above" drops of 30–50% across 28 models](https://aclanthology.org/2025.findings-acl.1031/);
[grammar-constrained decoding distorting the distribution](https://arxiv.org/abs/2405.21047);
[KnowNo's "an option not listed here" and asking for help](https://arxiv.org/abs/2307.01928).

### Open choices and look-alike names: read the whole target (5 October 2026, second run)

**What changed.** The probe gained a third scene, a fort sentry whose targets come in
look-alike pairs: `north_gate` and `south_gate`, `east_tower` and `west_tower`, plus a shield
and a helmet. It brought 21 new prompts:

- eight **open choices**, where either of two targets is right ("Check one of the gates.")
- six **near misses whose names start like a listed target** ("Go to the north tower.")
- four exact requests, two unrelated ones and one chat line

The probe now records the model's probabilities for every token of the target, not only the
first. The scorer gained a `path` signal: at each token of the target, the share of the top-5
probability that could still lead to a listed target, keeping the lowest. Only the control
prompt and schema ran (arm A0), on all 130 prompts, so every check is scored on identical
answers. Same conditions as the first run, on AC power. The control matched the first run's
answers exactly on the 109 shared prompts.

**Results.** Correct of 130 · wrong actions carried out · good requests refused:

| Check | `qwen3.5:2b` | `qwen3.5:4b` |
|---|---|---|
| None (control) | 83 · 33 · 14 | 113 · 15 · 2 |
| Name check, on player requests | 97 · 4 · 29 | 109 · 3 · 18 |
| On-list share at the first token, every decision | 105 · 10 · 15 | 118 · 10 · 2 |
| **On-list share at every token, every decision** | **108 · 5 · 17** | **124 · 4 · 2** |

- **Look-alike near misses:** reading every token caught 6 of 6 on both models. The first token
  caught 2 on the 2B and 1 on the 4B. "Go to the south tower." walked the 4B to `south_gate`
  with 1.00 on the list at the first token and 0.23 across the whole target. "Pick up the
  spear." scored 0.00 on both models once the second token was read.
- **Open choices:** the 4B got 7 of 8 right. Its targets kept 0.98–1.00 on the list while its
  confidence in the one it picked fell to 0.42–0.52 on "Climb either watchtower and look out."
  A check on that confidence would have refused those good choices; the on-list share
  passed them. The 2B answered `none` to six of the eight by itself, before any guard. That is
  the model, not the guard.
- **Separation:** the every-token share separated wrong targeted answers from right ones with
  an AUROC of 0.93 on the 2B and 1.00 on the 4B. The first-token share scored 0.86 and 0.81 on
  this harder set.
- **Cost:** three good requests refused on the 2B, none on the 4B. Thresholds from 0.6 to 0.9
  scored 107–108 on the 2B and 123–125 on the 4B. The refused requests:
  - "Go to the water butt." The model wanted "water".
  - "Someone dropped a weapon near the road. Secure it." The model weighed "weapon", at 0.77.
  - "Pass me the healing draught." The model began "healing", and the schema completed it as
    `health_potion`, which was right.

**What I believe now.** Read the whole target. It fixes the look-alike blind spot and passes
open choices, and its only cost is a small model sometimes refusing a paraphrase. DR-016 adopts
it.

**What would change my mind.** A larger set, written by other people, where paraphrase
refusals on a small model outnumber the look-alike swaps it catches.

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
