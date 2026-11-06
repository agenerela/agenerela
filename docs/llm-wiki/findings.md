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
The 25 October gate used `127.0.0.1` only, so `localhost` from Unity is still untested. A
2.1 s delay on an unreachable `127.0.0.1` (19 October) is a different thing; see that entry.

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

Phase 1 is pure C# with no model in the loop, settled by EditMode tests. The framework's
first repeated measurement is the Phase 2 gate's in-Unity latency (25 October 2026), taken
when a real provider answered a real request; the entries after the first `OllamaProvider`
call (19 October) are what it showed. Entries are in date order. Earlier entries that carry numbers come
from the standalone probes in `tools/benchmarks/`, which ask a model directly. Everything else
comes from the build plan's Appendix A, measured in the predecessor prototype.

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

### What a provider can get from Ollama's and LLMUnity's token probabilities (7 October 2026)

Facts the grounding guard (DR-016) and the providers depend on, found while writing the
contract in #67 (PR #68). Only the first row was measured; the rest come from reading
Ollama's source and LLMUnity's Inspector, and are labelled as that.

| Fact | Basis |
|---|---|
| Ollama's `logprobs` are taken **before** the schema's mask, so they show what the model wanted to write | Measured on 0.34.2, the 5 October entry above; cost 0.06–0.08 s a decision |
| `top_logprobs` is capped at **20**: Ollama refuses a larger number. The probe sent 5, and `OllamaProvider` sends 5 | Read in Ollama's `server/routes.go` (PR #68). Not tried against a live server |
| **A character split across two tokens is missing from both tokens' text.** llama.cpp's server cuts the unfinished bytes off, and Ollama rebuilds its `bytes` field from the text it already cut, so `bytes` does not recover it | Read in Ollama's source (PR #68). The unit test uses a fixture, not a live reply |
| LLMUnity's `LLMClient.nProbs` exists, **0 to 10** in its Inspector, so the in-process provider can keep at most ten alternatives | Read from the Inspector. Not tried, and whether llama.cpp reports before or after its grammar is still unknown (`providers.md`) |

What follows from the split-character row: the token texts only join back into the reply
when no such character is in it, so anything mapping tokens to positions in `Text` checks the
join first. ASCII replies are unaffected, but target ids may contain non-ASCII letters, so
the Phase 3 guard needs the check. `ProviderResult.TokenProbabilities` carries that rule.

**What would change this.** A different Ollama version: the cap, the split-character
handling and the "before the mask" behaviour were all checked on 0.34.2 only.

### Sword asset quality: improve component inputs before raising settings (8 October 2026)

This is an asset-pipeline finding, not a language-model accuracy measurement.
[Full experiment report](asset-quality-2026-10-08.md) and
[measurements and hashes](asset-quality-2026-10-08.json).

**Conditions.** Sixteen sequential jobs on the same RTX 5080 server, ComfyUI 0.39.1,
seed `49081001`, one gothic sword design. Each group keeps its PNGs fixed; controls
are repeated in this session. Studio renders use Blender 5.2.2 LTS, identical camera
rules and lighting, 768 px views and 48 Cycles samples. Six-angle exports were inspected.

**Observed.** With full-sword references, 4K versus 2K normal baking, 24 versus 12 texture
steps, a smaller cage, QEM, lighter smoothing and different crease angles did not
convincingly recover the smeared engraving. New close-up hilt references plus separate
component generation preserved silver vines, leather wraps and the solid-looking onyx
inset much better. Approximate effective grip width increased from 31 to 110 input pixels.
The single-view full-sword recipe bent the blade in the side view; multiview kept it straight.

**Costs.** Hilt textures at 2K versus 4K looked close at the comparison size and reduced
the GLB from 26.98 to 12.57 MB (about 53%). Reducing its geometry from 49,986 to 19,934
triangles with QEM kept it intact but reduced the GLB only to 25.53 MB at fixed 4K textures.
4K normal baking increased the full-sword GLB about 36% and hilt about 18%, without a
convincing visible detail gain in these views. Client durations are recorded, but caching
and transfer make them unsuitable for a cold-inference speed claim.

**Practical choice.** For this ornate thin prop, generate the detailed hilt separately
and author the straight, beveled blade locally. The revised draft is in the gitignored
scratch folder, with an editable Blender file. Select texture size by intended viewing
distance; increasing atlas size cannot reconstruct lost source detail.

**Limits.** One design, qualitative review, no blinded perceptual scores. The close-up
recipe changes framing, source detail, scope and triangle allocation together; the
single-view arm changes workflows as well as conditioning. Fixed-seed controls produced
slightly different meshes, so subtle differences are not established wins. These results
do not establish a universal best setting or test the other two swords independently.

### Eval prompt set: how annotator disagreements get resolved (8 October 2026)

A procedure, not a measurement. #13 asks for it, and the Phase 4 agreement number depends on
it: once a disagreement is resolved by overwriting it, agreement can no longer be computed.
The prompt format is in
[`UnityProject/Assets/Evaluation/README.md`](../../UnityProject/Assets/Evaluation/README.md).

1. **Label alone.** On the doubly labelled subset, each annotator answers every prompt
   without seeing the other's answer, against `agent.json` and the prompt's
   `preconditionState`.
2. **Record both answers before talking.** Each one goes into the prompt's `labels`, keyed by
   annotator, as `action/target`. They are never edited afterwards, even when the person who
   gave one changes their mind. Percent agreement, and Cohen's kappa once the overlap is large
   enough, are computed from `labels` alone (Phase 4).
3. **Then settle it, in one of three ways:**
   - **One answer is right.** It becomes `expectedAction` / `expectedTarget`, and `note`
     says why the other was rejected, such as "closing the gate is not a registered action".
   - **Both are right, with the same action and different targets.** Make it an open choice:
     `expectedTarget` as a list of both.
   - **Neither side can convince the other.** The prompt is ambiguous, so it measures the
     prompt rather than the model. Rewrite the stimulus or observations so one answer is
     right, as a new prompt id, and drop the old one from scoring. Its `labels` stay in the
     agreement count.
4. **Don't add an action or target just to settle a prompt.** `agent.json` describes the
   agent the game has. If the agent cannot do what the stimulus asks, the answer is `none` /
   `no_target`.
5. **Never relabel after seeing a model's answer.** A label changed to match a run's output
   grades the model against itself. A label found wrong later is fixed in its own commit,
   before any run that is reported, and the commit message says why.

**What would change my mind.** If agreement on the overlap stays low after ambiguous prompts
are rewritten, the problem is the agent definition or the category, not the prompts, and
step 3's third case would be hiding it.

### Asset server: face count barely changes the time; NVFP4 is steadier than int8 (8 October 2026)

An asset-pipeline result, not a language-model accuracy one. The tables and commands are in
[`tools/comfyui/README.md`](../../tools/comfyui/README.md) ("Why NVFP4", "Text to 3D model"),
which stays the one place that holds them in full. Source: PRs #71 and #72, on the RTX 5080
machine. The sword trials that followed are in
[`asset-quality-2026-10-08.md`](asset-quality-2026-10-08.md).

- **Image, NVFP4 against int8 (Z-Image Turbo).** Same prompts and seeds, 24 images each, models
  loaded: int8 took 2.8–12.2 s (median about 4.9 s), NVFP4 2.3–2.7 s (median 2.5 s). int8 was
  erratic, about 3 s in one block and 5–12 s in the next two, and PR #72 did not find out why.
  Lettering spelled right: int8 14 of 14, NVFP4 13 of 14. "Equally good to the eye" for props,
  scenes and a portrait is one person's judgement, not a measurement.
- **Mesh, the face count hardly changes the time.** The default run (47,825 triangles) took
  175 s and the `faces=20000 decimation=qem` run (18,267 triangles) took 171 s. One run each,
  so an anecdote; a 4 s gap on a three-minute job is small.
- **Mesh, the textures are most of the file.** `texture_size=1024` takes the base colour and
  metallic-roughness maps from about 3.5 MB to 1.2 MB each; the normal map stays at 2048 px.
- **Mesh decimation.** The template's `midpoint` broke the mesh into shards at 20,000 and 5,000
  faces and `qem` did not. Tested on one object (README).

**What would change this.** A different GPU: NVFP4 runs in hardware only on RTX 50-series
cards. A second workload on the card would also move every time here.

### Ollama never shows the response schema to the model (10 October 2026)

**What was checked.** Is the text inside the JSON Schema sent as Ollama's `format` read by
the model as part of its prompt, or used only to build the sampling grammar? That text
includes the field descriptions and anything written into them. This came up while building
#8. The prototype put each action's description into the schema's `action` description,
and the issue for #8 replaced that with one fixed sentence. If the model reads the schema,
that would have been an unmeasured change.

**How.** `tools/benchmarks/schema_visibility_probe.py` sends one chat four times: a
one-line system prompt, then `Player: "Go to the tower."`. The four runs: no `format`; the
decision schema with a one-sentence `action` description; the same with ~300 extra words in
that description; and the one-sentence schema again. Temperature 0, thinking off, 40 tokens
out.

| Request | `prompt_eval_count` |
|---|---|
| No `format` | 37 |
| Schema, one-sentence description | 37 |
| Schema, ~300-word description | 37 |
| Schema, one-sentence description, repeated | 37 |

Both schema variants also gave byte-identical replies. Without `format`, the model wrote
free prose. Conditions: Windows 11, Ollama 0.34.2, `qwen3.5:2b` the only model loaded and
on the GPU, RTX 4060 Laptop on AC power. Run twice, the second time from the committed
script, with the same result. A token count is deterministic, so one request per run
settles it.

**What it means.**

- Ollama uses the schema only to constrain sampling. **Nothing written in it reaches the
  model**: not the three field descriptions #8 defines, and not an action description
  placed there. The GBNF path (Phase 6b) carries no descriptions at all. Cloud APIs that
  take a response schema, such as Gemini, are expected to show it to the model, but that was
  not measured here. So one `DecisionSchema` may tell a cloud model more than a local one.
- **The prototype was the same.** Its action descriptions sat only in the `format` schema,
  so Appendix A's Ollama numbers were measured with the model reading action ids, the system
  prompt and the few-shot block, and never the descriptions. The 84.9% arm is therefore the
  configuration #8 builds, not a different one. The prototype's Gemini comparison is the
  one run where descriptions may have been read.
- **As built in #8 and #16, `ActionDefinition.Description` reaches no local model.** The
  schema is invisible and the system prompt carries no action list.
- **The description-symmetry rule (build plan §2.2) rests on weaker evidence than it
  states.** It credits a regression partly to a long, emphatic `none` description. The
  prototype's own comment says that description acted together with an "only act when
  directly asked" system prompt. The description cannot have reached the model through
  Ollama, so the prompt wording is the likelier cause. Keeping the rule costs nothing; its
  evidence should not be cited as a measurement of descriptions.
- #16's fixed line `Use no_target when none of the listed targets applies.` repeats the
  `target` field's description. For a local model it is the only place that sentence is
  read, so it is not a duplicate there.

**Owed.** Whether action descriptions should be in the prompt is a Phase 4 A/B. The control
is action ids only, as measured. The treatment is one `id: description` line per available
action in the system side of the request.

**What would change this.** An Ollama version or model template that puts the schema into
the prompt. Re-run the probe after any Ollama upgrade; it takes seconds.

### The Phase 1 gate was shown able to fail, one break at a time (12 October 2026)

PR #84 added the five definition-of-done checks (#11). A check that has never failed proves
nothing, so a throwaway probe (not committed) switched each measured rule off in the runtime
code, one at a time, and ran all five gates under each break. Each break failed exactly its own
gate and no other: `IsAvailable` ignored → Gate 1; `target` optional with no `no_target` →
Gate 2; `statement` moved first → Gate 3; examples rotating through the registry → Gate 4;
`target` kept with no targets → Gate 5. With no break all five pass.

Each failure message cites only what Appendix A measured. Gate 1 cites the constrained schema
together with the few-shot block (58.5% → 84.9%), because state masking was never measured
alone; Gate 5 cites the build plan and #7/#8 rather than a number. At that point 354 of 354
EditMode tests passed. The method suits any later gate: break the rule on purpose before
trusting the check.

### `OllamaProvider`'s live check: what a real server showed that tests could not (19 October 2026)

PR #102 (merged 20 October, #88). Each number is one reading by hand, through `Agent` and
`OllamaProvider` in batch mode, so they are anecdotes (hard rule 2). Conditions: Ollama 0.34.2,
`qwen3.5:2b`, the lead's laptop on AC power, no other model resident. The Phase 2 gate's
measurement (25 October, below) is the one with a repeat count.

- **One decision on the laptop.** The first request took 9.4 s, mostly loading the model; the
  second took 0.59 s, with 193 prompt and 33 completion tokens. n = 1, and Appendix A's ~1.3 s
  had Unity rendering a 180-prop scene, so the two do not compare. It is the only
  through-the-framework number from the laptop so far; the gate's 0.427 s is another machine.
- **Ollama stopped: `ProviderUnreachable` came back after 2.1 s.** The PR says Windows retries
  a refused loopback connection; that explanation was not tested. The endpoint was
  `127.0.0.1`, so this is **not** the `localhost` delay of 5 October, though it is the same
  size. A 2 s unreachable result is not proof that the address was `localhost`.
- **Mono's "connection refused" message ends in a line break and a run of NUL characters** on
  Windows. They reached the warning log and telemetry, which is how the live run found them;
  the provider now cuts the transport's message at its first line break.
- **How Ollama 0.34.2 answers a missing model and a wrong path.** A model it lacks gives
  `404` with `{"error":"model 'nosuchmodel:1b' not found"}`; a path it does not serve gives a
  plain-text `404 page not found`. The provider tells them apart so a wrong endpoint does not
  send the developer to `ollama pull`. Other versions were not checked.
- **"The reply comes back on the main thread" cannot be tested by checking the main thread.**
  A mutation run in PR #102 showed that a test asserting the *result* arrives there still
  passes with `ConfigureAwait(false)` added, because Unity's `Awaitable` resumes its awaiter on
  the main thread anyway. Only a test that watches the provider's own continuation failed the
  mutant (thread 16 instead of 1).
- **DR-016's case, live, once.** For "Attack the scarecrow." the first token of the target was
  `"sc"` (−0.060) against `"training"` (−4.0), and the 42 token texts joined to exactly `Text`.
  One prompt: a demonstration of the mechanism, not a rate.

### First in-Unity latency: a warm decision through the framework in under 0.6 s (25 October 2026)

**What was measured.** The Phase 2 gate's `RequiresOllama` PlayMode tests (#94) time each live
decision two ways: wall-clock around `Agent.DecideAsync` (prompt build, queue, request,
parse and the hop back to the main thread), and `DecisionTelemetry.LatencySeconds` (the
provider's request alone, from the send). It is the first latency taken inside Unity since
the prototype, and the first through this framework.

Five batch-mode runs of the four tests, four timed decisions each, n = 20:

| | Mean | Median | Min | Max | SD |
|---|---|---|---|---|---|
| Wall-clock around `DecideAsync` | 0.427 s | 0.463 s | 0.225 s | 0.560 s | 0.120 s |
| `LatencySeconds` (provider only) | 0.427 s | 0.463 s | 0.225 s | 0.560 s | 0.119 s |
| `QueueWaitSeconds` | 0.000 s | | | | |

Per decision: 211 to 248 prompt tokens, 24 to 42 completion tokens. The longest, 0.52 to
0.56 s, wrote the most tokens (42); the shortest, 0.23 to 0.25 s, the fewest (24). Greedy
decoding made every run give the same four decisions and token counts, so the spread is
between the four requests, not between runs: each request varied by 0.05 s or less across
the five runs.

**Conditions.** Windows 11, Unity 6000.3.23f1 PlayMode tests in the Editor in batch mode,
Ollama 0.33.0, `qwen3.5:2b` (2.4 GB resident, 100% GPU, `num_ctx` 4096, temperature 0,
`think: false`), NVIDIA RTX 5080 16 GB desktop GPU, driver 617.14. Endpoint
`http://127.0.0.1:11434`. One model resident, checked with `ollama ps` after every run.
Warm: three throwaway requests before the first timed one, and the model stayed loaded
between runs. Two other Unity Editors and ordinary desktop apps were open; no ComfyUI
container was running. A cold first run took 6.1 s for the warm-up and its first test
together, so warming matters (build plan §4, item 6).

**What it means.**

- **The Phase 2 budget of 5 s holds with a wide margin on this machine.** The slowest
  decision was 0.56 s.
- **Almost all of the time is inside the provider's request.** Wall-clock and
  `LatencySeconds` agree to the logged millisecond, so what happens outside the provider
  call (building the prompt and schema, the queue, the parse and the hop back to the main
  thread) took about a millisecond or less. That is not all of the framework's own work:
  `LatencySeconds` starts before `OllamaProvider` serialises the request body, so that cost
  sits inside the provider's time and is not isolated here.
- **Not comparable with Appendix A's ~1.3 s.** That was an RTX 4060 Laptop with URP
  rendering a 180-prop scene; this is a desktop RTX 5080 with no scene rendering. Only a
  run on the same machine compares (§4). The inherited `localhost` question above stays
  open: this run used `127.0.0.1` only, so it has no `localhost` arm.
- **No accuracy claim.** The tests check that a decision arrives, not whether it is right.
  Accuracy is Phase 4's, with a control arm (hard rule 2).

**Reproduce.** With Ollama running and `qwen3.5:2b` pulled, run the PlayMode category
`RequiresOllama` from the Test Runner, or in batch mode with
`-runTests -testPlatform PlayMode -testCategory RequiresOllama`. Each timed decision prints a
`LATENCY` line into the results. `AGENERELA_OLLAMA_MODEL` and `AGENERELA_OLLAMA_URL` choose
another model or endpoint.

**What would change this.** A slower GPU or a laptop on battery, a second model resident, a
scene rendering beside the model, or a longer prompt (more targets, more history). Re-run on
the lead's laptop for the number Appendix A can be compared with.


### GreyBoxVillage played live: availability fixed what wording could not (25 October 2026)

Single runs by hand, `qwen3.5:2b`, Ollama 0.33.0, warm, each decision about 0.4 s. Anecdotes,
not measurements (hard rule 2). The full list is under "Played live" in
[`Demos/GreyBoxVillage/README.md`](../../Demos/GreyBoxVillage/README.md) (PRs #111 and #112).

- **Before the availability rules**, "Hello" drew `patrol`, and a wolf 26 m away drew
  `sound_alarm`, once with the statement "The wolf is too far away to be a threat": the action
  contradicting the words. The README's reading is that the model took the greeting for
  `patrol`'s old example of a calm moment; that reading is a guess.
- **With the rules**, the game derives availability from who is asking (player, zone, wolf,
  quiet timer) and from the wolf's distance. The same lines then drew `none` or
  `attack_target / wolf`, and `patrol` was not offered for a greeting. This is the settled
  rule that illegal options are absent from the schema, seen live. Since Ollama never shows
  action descriptions (10 October), availability is the lever a developer has, and wording is not.
- **Left for Phases 3 and 4 on purpose:** a repeated statement copied word for word from the
  history, wrong-but-legal picks on free chat, and "Walk with me" drawing `follow_player` in
  one session and `patrol` as a cold first request in another. None is measured; the Phase 4
  runner is where to count them.

### The grounding guard's probabilities are still from before the mask on Ollama 0.34.2 (27 October 2026)

The check #105 asked for before shipping `TargetGroundingGuard`. If Ollama reported token
probabilities from after the schema's mask, they would always sit on the target list, and the
guard would pass everything. Single runs by hand, greedy, `qwen3.5:2b`, Ollama 0.34.2, RTX 4060
Laptop on AC power, no other model resident, warm except the first request. These are
anecdotes that check the plumbing, not measurements of the guard (hard rule 2).

**Through the probe**, with its A0 village prompt, read by `target_info` and the `path` signal
of `target_swap_score.py` (`.scratch` script, not committed):

| Request | Model answered | Reading | DR-016's probe, 5 October |
|---|---|---|---|
| "Attack the scarecrow." | `attack_target / training_dummy` | **0.051**: first-token top "sc" at 0.92 | 0.05 |
| "Attack the training dummy." | `attack_target / training_dummy` | 0.998 | 0.998 |
| "Pick up the spear." | `pick_up_item / sword` | 0.0005, at the token after "s" | — |
| "Attack the dragon." | `attack_target / training_dummy` | 0.61 | — |

The probabilities are still from before the mask. A reading of 0.05 is possible only if the
model's own "sc" survived into the reported alternatives.

**Through the framework**, `OllamaProvider` and `Agent.DecideAsync` with the Phase 2 gate's
village guard (`LiveGuard`), in a PlayMode test run once and not committed:

| Request | Targets in the prompt | Model answered | Reading | Agent did |
|---|---|---|---|---|
| "Attack the scarecrow." | none | `attack_target / training_dummy` | 0.024 | `none`, wanted "sc…" |
| "Attack the scarecrow." | one observation per target id, as GreyBoxVillage does | `attack_target / training_dummy` | 0.111 | `none`, wanted "sc…" |
| "Attack the training dummy." | the same | `attack_target / training_dummy` | 0.997 | attacked the dummy |
| "Attack the dragon on the hill." | the same | `none / no_target` | 0.96 (on `no_target`) | `none` |

So the C# reading works on a live reply, and the substitution the 25 October demo showed
("Attack the dragon on the hill" became an attack on the dummy, on Ollama 0.33.0) is the case it
refuses. This time the 2B answered the dragon with `none` by itself, so that line shows
nothing about the guard. The two scarecrow rows differ in their prompts, not because of the
guard, and are one run each.

**What would change this.** A reading near 1 for the scarecrow on a later Ollama would mean
the probabilities moved after the mask, and the guard would pass everything. Repeat this check
after an Ollama upgrade, as for `schema_visibility_probe.py`. Whether the guard turns away
more good requests when the prompt lists no targets (#105, "Known gap") is #110's live run and
Phase 4's to measure, not this one's.

### The Phase 3 gate passed live: 19/20 with the guards on, 18/20 without (3 November 2026)

#110's live check: the prototype's 20-prompt suite (`tools/benchmarks/compare_2b_4b.py`, prompts
word for word), run through the framework's own pipeline by `Phase3LiveSuiteTests` in PlayMode.
Each prompt was decided twice in a row, first with the guards on and then with
`CheckTarget = false` as the control, on one warm model in one session. Greedy decoding, no
memory, so no prompt saw another. The gate's bar is 19/20 or better with the guards on.

**Result: the gate passed.**

| Group | Guards on | Control (`CheckTarget = false`) |
|---|---|---|
| 1. Simple commands | 5/5 | 5/5 |
| 2. Commands with a target | 5/5 | 5/5 |
| 3. Impossible requests | **6/6** | 5/6 |
| 4. Chat | 3/4 | 3/4 |
| **Total** | **19/20** | **18/20** |
| Wrong actions carried out | 1 | 2 |
| Good requests refused | 0 | 0 |
| Grounding guard rewrites | 1 | — |

- **"Pick up Excalibur."** The model answered `pick_up_item / sword` in both arms. With the
  guards on, the reading was 0.717, below 0.8, so the decision was rewritten to `none`. In the
  control it picked up the sword. This was the only rewrite.
- **"That's an interesting sword you have."** Picked up the sword in both arms, at a reading of
  0.999. This is Appendix A's residual, a wrong action on the right target, which no target
  check can catch.
- No good request was refused. The lowest reading on a command the model got right was 0.77,
  on "Stay close." (`follow_player`). The guard acts only on actions that need a target, so it
  passed.
- The other five impossible requests the model answered with `none` by itself in both arms,
  with readings between 0.82 and 0.99 on `no_target`.

**At n = 20 this says the gate passed, not how much the guard helps** (hard rule 2). The arms
differ by one prompt, well inside the noise floor. That measurement is Phase 4's, at 200+
prompts.

**Excalibur differs from DR-016's probe.** There the 2B took Excalibur for the sword with
confidence, and §2.5 lists it as a near miss that passes. Here it read 0.717 and was caught.
The prompt differs (Marcus, five targets given as observations, new examples), and so does the
Ollama version. It is one reading, not a change to that limit.

**How the targets reached the prompt.** `PromptBuilder` lists no targets, and Ollama never
shows the schema. So each decision carried them as observations, one line per target with its
id and a short description (`bridge: the stone bridge over the river.`), the way GreyBoxVillage
does. It also carried one line saying whether the guard is following the player, which the
prototype put in its system prompt. DR-016's numbers came from a prompt that listed every
target as well, so the two are not directly comparable.

**The agent.** "Marcus, a village guard", with the prototype's five targets as ids (`bridge`,
`tower`, `blacksmith`, `training_dummy`, `sword`), its five actions plus `none`, and
`already_following` set for each prompt. The few-shot examples are new. The prototype's own
share the frame "<verb> the <target>." with the suite, which hard rule 4 forbids. A test checks
the rendered block against all 20 prompts, and the examples are written down in the test file.

**Checks after the Ollama upgrade (0.34.2 → 0.40.14), made first:**

- `schema_visibility_probe.py`: `prompt_eval_count` was 37 in all four arms. **The schema still
  never reaches the model.**
- One "Attack the scarecrow." request with `format`, `logprobs` and `top_logprobs`. The
  alternatives at the target's first token were `sc` -0.13, `Sc` -2.52 and `The` -4.54. The
  schema forced `t` at -11.6. **The probabilities are still from before the mask.** One change:
  the forced target now comes back in small pieces (`t`, `rain`, `ing`, `_d`, `u`, `mm`, `y`),
  where 0.34.2 gave "training" as one token (27 October). The guard reads every token, and the
  suite's readings on right targets (0.997–0.999) show it still reads them correctly.

**Conditions.**
- `test` at 727558c, Unity 6000.3.23f1, PlayMode in batch mode.
- `qwen3.5:2b` (Q8_0) on Ollama 0.40.14, at `http://127.0.0.1:11434`.
- RTX 4060 Laptop 8 GB on AC power, `qwen3.5:2b` the only model loaded (the test's `/api/ps`
  check).
- Warmed up before the first scored prompt.
- Another session's Unity batch tests were running on the CPU at the same time. Per-decision
  latency was 0.75–1.09 s. That is not a latency measurement.

**What would change this.** A guards-on run below 19/20 on the same suite and model. Or a
later Ollama whose probabilities are from after the mask: the scarecrow check would then read
near 1, and the guard would pass everything.

### Living Vale's NPCs on four small models: qwen3.5:4b read best, at qwen3.5:2b's speed (5 November 2026)

**A small qualitative comparison, not an accuracy claim** (hard rule 2): n = 12 player lines per
model, no labels and no control arm. It answers "which local model should Living Vale's NPCs
talk with by default", and nothing about action selection, since every NPC had only `none`.

**What ran.** Living Vale's G5 driver, `AgenerelaNpcDecisionDriver`, headless in Unity batch
mode (`NpcModelComparison`, an explicit `RequiresOllama` EditMode test): one framework `Agent`
per NPC, the player's line as the stimulus, five or six persona lines per NPC as observations,
the framework's `RollingHistory` as memory, no targets (`no_target` only), no state line. Twelve
lines written for this run, none from `UnityProject/Assets/Evaluation/` (hard rule 4): six to
Mara, then six to Iven, in order, so the two follow-ups had a previous turn. Each NPC had
two knowledge questions, a greeting, a follow-up, an off-topic or unsupported request and an
attempt to break character. Raw output, one JSON line per turn, is in `.scratch/lv-g5/compare/`
on the lead's laptop. Prompts were about 400–480 tokens. Mara's first persona line then said the traveler was someone she had
not met before; it was dropped after the run, because it stayed true on every later visit.

**Conditions.** RTX 4060 Laptop GPU (8 GB), on AC power, Ollama 0.40.14, temperature 0, `think`
off, context 4096. The four models ran one after the other, with every other model unloaded
first and three throwaway warm-up requests (measuring.md, item 4). `/api/ps` showed one model,
fully on the GPU (`size_vram` = `size`), before each one. Not checked: other sessions on this
machine may have been running Unity batch jobs, and a Codex review was running (mostly remote).
Latency is the framework's `LatencySeconds`, the provider's request alone.

| Model (Ollama tag, quant, VRAM) | p50 | p95 | Pipeline errors / malformed | Guards fired | In character | Uses its knowledge | Follow-ups |
|---|---|---|---|---|---|---|---|
| `qwen3.5:2b` (Q8_0, 2.4 GB) | 0.92 s | 1.33 s | 0 / 0 | 0 | 12/12, but wordy and repetitive | Partly: invented stock (dried meats, iron ore) | Wrong sum ("20 gold each, so 40"); Iven said "not sure where it started" and then said where |
| `qwen3.5:4b` (Q4_K_M, 3.1 GB) | 0.89 s | 1.67 s | 0 / 0 | 0 | 12/12, concise | Yes; one odd line (Iven had "never seen a castle outside Briar Vale") | Both right: "twenty gold for the pair"; Ashwatch, two days, the Old Road |
| `granite4.1:3b` (2.5 GB) | 0.60 s | 0.85 s | 0 / 0 | 0 | 12/12, very terse ("Yes, it is.") | Yes | Both right |
| `lfm3:1.7b` (1.2 GB) | 0.76 s | 1.06 s | 0 / 0 | 0 | 11/12: Iven's greeting answered as the player ("I'm already here, Iven.") | Yes | Both right |

What a player would notice. **granite4.1:3b** wrote `Iven:` at the start of all six of Iven's
replies, so the chat would show the name twice, and it dodged the break-character line ("I'm
not sure what you mean"). **qwen3.5:2b** ended four of Mara's six replies by sending the
traveler to Sella or Iven, whatever was asked. No model broke character on the two attempts,
no model claimed an action, and none came near the game's 8-second turn timeout (slowest
turn 1.89 s). With only `none` registered, the guards had nothing to contain.

**Recommendation: `qwen3.5:4b` for Living Vale's NPCs**, on the evidence above: the fewest
slips in the twelve (one odd line, no wrong fact) and the most natural replies, at the same latency as `qwen3.5:2b` here. The cost is
0.8 GB more VRAM beside the game's renderer, unmeasured with the game running. `qwen3.5:2b`
stays the framework's default; this is a choice for one game's chat, not for action selection.
`granite4.1:3b` is the fallback where latency or memory matters most, if a prompt line or a
trim of a leading `Name:` fixes its prefix.

**What would change this.** A larger set (n ≥ 50) with judged labels, scored blind, in which
`qwen3.5:4b` is no better than `qwen3.5:2b`; latency with the game rendering beside it; or a
repeat of this run that disagrees, since the arms ran sequentially and n is 12.

### Mara's trade actions live: the right quote 5/6 on qwen3.5:4b, 4/6 on qwen3.5:2b (5 November 2026)

**A small live check, not an accuracy claim** (hard rule 2): six labelled lines per model and no
control arm. It shows that G5b's trade binding works against a real model, and which mistakes
show up first.

**What ran.** `MaraTradeLiveCheck`, an explicit `RequiresOllama` EditMode test that sends each
line through the real `AgenerelaNpcDecisionDriver` and executes the result through G3's
`NpcActionExecutor`. Each case was staged on its own: a fresh driver and game state, no memory,
and the player carrying one wolf pelt and one iron scrap, so both `quote_purchase` (three stock
entries) and `quote_sale` (two bundles) were offered. Targets reached the prompt as observation
lines, one per id with its price, plus one line per action and one rule line. There was no state
line. The few-shot block had one example per action, on its first target ("I'll take healing
potion ×1, please." and "Would you buy my wolf pelt ×1?"). None of the six lines repeats that
wording, but lines 1 and 4 ask for the same targets as the examples, which favours them. The
lines were five buy or sell requests and one browsing line labelled `none`.

**Conditions.** As in the entry above: RTX 4060 Laptop, AC power, Ollama 0.40.14, temperature 0,
the two models run one after the other under the shared Ollama lock, each warmed with three
throwaway requests and resident alone, fully on the GPU. A Codex review was running at the same
time.

| Outcome (measuring.md, rule 5) | `qwen3.5:4b` | `qwen3.5:2b` |
|---|---|---|
| Correct action and target | 5 | 4 |
| Wrong but legal | 1 | 1 |
| Contained by a guard | 0 | 1 |
| Rejected when expected | 0 | 0 |
| Pipeline error | 0 | 0 |

- **Wrong but legal**: on the browsing line ("Just looking around. How is business today?") both
  models made an offer, a potion and bread. Nothing moves until the player confirms the card, but
  an unasked offer is the visible mistake. A `none` example in the few-shot block
  (`IdleExampleStimulus`) is the obvious candidate fix. It would need an A/B run.
- **Contained**: `qwen3.5:2b` asked to buy the player's iron scrap with `quote_purchase` instead
  of `quote_sale`. The pair guard rewrote it to `none`, the player saw the refusal line, and no
  quote was made.
- **Statements**: every price named in a statement matched the target's own price. One
  `qwen3.5:2b` line about buying the pelt read as if Mara were selling it.
- **Latency**: p50 1.8 s on `qwen3.5:4b` and 1.2 s on `qwen3.5:2b` (n = 6), longer than the chat-only
  entry above because the prompt is longer. One `qwen3.5:4b` turn took 14.9 s, which is over the
  game's 8 s turn timeout. It happened once, was not investigated, and may have come from the
  load beside it.

**What would change this.** A larger labelled trade set with a control arm. Repeating the 14.9 s
turn would make the turn timeout a real risk for `qwen3.5:4b`.

### Non-Qwen small models for Living Vale's NPCs: gemma4:e2b wrote best, qwen3.5:2b near the bottom (5 November 2026)

**A small comparison with one judge, not an accuracy claim** (hard rule 2). Each model got 24
lines, there was one blind judge, and there are no human labels. `qwen3.5:2b` is the control arm.
It ran in the same session, on the same machine, with the same lines.

**What ran.** For each model, the real `AgenerelaNpcDecisionDriver` in Unity batch mode was sent:

- the 12 conversation lines from the first entry today (`NpcModelComparison`);
- 6 new writing lines that need voice more than facts (`NpcModelComparison.RunTheWritingLines`):
  the night the wagon left, the news of its wreck, how Iven will tell his patients, a market
  rumour and who told it, and a market day described. None repeats a line above, a few-shot
  example or the evaluation set (hard rule 4);
- the 6 trade lines from G5b (`MaraTradeLiveCheck`).

Each model had one turn of each kind per line, so n = 24, at temperature 0. Raw output is in
`.scratch/lv-g5/g5c/` on the lead's laptop.

**Conditions.**

- **Hardware and software:** RTX 4060 Laptop (8 GB), on AC power, Ollama 0.40.14, under the
  shared Ollama lock.
- **Arms:** run one after the other, 16:28–16:37. Every other model was unloaded first, and each
  model was warmed with three throwaway requests. `/api/ps` showed it alone and fully on the GPU.
- **Not checked:** two model pulls (Stheno, gemma) were downloading during the first six arms,
  and a Codex review was running.
- **CPU side load:** another session's Unity EditMode run took the CPU from 16:29:08 to 16:29:49,
  overlapping the `granite4.1:3b` arm and the start of `nemotron-3-nano:4b`'s. Both arms were run
  again at 16:46 with nothing beside them. The p50s were the same (0.59 s and 0.55 s), granite's
  p95 went from 0.89 to 0.84 s and nemotron's from 1.28 to 1.09 s, and the trade outcomes were
  identical. The table keeps the first run, whose replies were the ones judged.
- **Gemma's size:** `gemma4:e2b-it-qat` showed 1.66 GB resident, although its file is 4.3 GB.
  The later check below explains the gap.
- **Not run:** `gemma4:e4b` (6.1 GB or more) and `ministral-3:8b` (6.0 GB) leave too little room
  for the game. `llama3.1:8b` was dropped because pulls were slow; the 8B slot went to the Stheno
  fine-tune.

**Writing quality, judged blind.** Codex (gpt-6.1-sol, high effort, read-only) scored each of the
162 chat and writing replies 1–5 on four things: voice, staying within the NPC's knowledge
(given to it as the persona's fact list), prose, and coherence with the previous turn. The replies
were shuffled per line under letters that changed per line, and unblinded afterwards. The trade
lines were not judged.

| Model | Resident VRAM | p50 | p95 | > 8 s | Errors | Trade (6) | Voice | Knowledge | Prose | Coherence | Chat (12) | Writing (6) | **Overall** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `gemma4:e2b-it-qat` | 1.66 GB* | 0.70 s | 4.14 s | 0 | 0 | 5 correct, 1 wrong-legal | 4.11 | 4.22 | 4.17 | 4.17 | 4.31 | 3.88 | **4.17** |
| `L3-8B-Stheno-v3.2` Q4_K_M | 5.28 GB | 1.49 s | 2.20 s | 0 | 0 | 5 correct, 1 rejected-when-expected | 3.72 | 3.72 | 3.33 | 4.56 | 3.94 | 3.62 | 3.83 |
| `nemotron-3-nano:4b` | 2.82 GB | 0.55 s | 1.28 s | 0 | 0 | 3 correct, 3 stopped by a guard | 3.61 | 4.50 | 3.28 | 3.89 | 3.96 | 3.54 | 3.82 |
| `llama3.2:3b` | 2.56 GB | 0.96 s | 1.38 s | 0 | 0 | 4, 1 contained, 1 wrong-legal | 3.78 | 3.33 | 3.61 | 4.28 | 3.77 | 3.71 | 3.75 |
| `granite4.1:3b` | 2.51 GB | 0.59 s | 0.89 s | 0 | 0 | 6 correct | 3.22 | 4.11 | 3.50 | 4.11 | 3.67 | 3.88 | 3.74 |
| `lfm3:1.7b` | 1.17 GB | 0.55 s | 0.89 s | 0 | 0 | 5, 1 wrong-legal | 3.28 | 3.50 | 3.22 | 3.61 | 3.56 | 3.08 | 3.40 |
| `MiniCPM5-2B` Q4_K_M | 1.64 GB | 0.64 s | 0.84 s | 0 | 0 | 4, 1 contained, 1 wrong-legal | 3.00 | 3.56 | 2.78 | 2.83 | 2.73 | 3.67 | 3.04 |
| **`qwen3.5:2b` (control)** | 2.36 GB | 0.95 s | 1.35 s | 0 | 0 | 4, 1 contained, 1 wrong-legal | 3.06 | 3.17 | 3.06 | 2.83 | 3.15 | 2.79 | 3.03 |
| `ministral-3:3b` | 2.73 GB | 1.20 s | 1.50 s | 0 | 0 | 5, 1 wrong-legal | 3.06 | 1.61 | 3.11 | 3.61 | 2.81 | 2.92 | 2.85 |

Latency is the framework's `LatencySeconds` over all 24 turns. No turn of any model reached the
game's 8 s turn timeout, and no turn was a pipeline error or malformed. \*As reported by
`/api/ps`; see Conditions.

What the numbers do not show:

- **The grounding guard stopped correct trade choices.** Every "rejected when expected" and
  "stopped by a guard" above is `TargetGroundingGuard`: two of `nemotron-3-nano:4b`'s three, and
  `Stheno`'s one, rewrote a correct quote to `none`. The guard's confidence check was set on
  Qwen's token probabilities (findings, 5 October). That it is too strict for other models'
  probabilities is a hypothesis for the Guards research area. It is not measured here.
- **The browsing line still draws an unasked offer** from most models, as in G5b.
- **Gemma is short and safe, and once wrong about what it knows.** Iven answered "the night the
  wagon left" with "I do not know anything about a wagon leaving a castle" and named someone who
  does not exist. The judge still scored its knowledge high overall.
- **Stheno reads richest**, and is best at following on from the previous turn (coherence 4.56).
  It is the heaviest by far (5.28 GB beside the game) and the slowest, and its licence is
  CC BY-NC 4.0, non-commercial only.
- **`ministral-3:3b` invents the most** (knowledge 1.61).

**Recommendation: try `gemma4:e2b-it-qat` as Living Vale's default.** The lead adopted it as
the game's default on 5 November (G5e). `qwen3.5:2b` stays the framework's default and is
second in F7's list.
On this evidence it beat the `qwen3.5:2b` control by more than a point on every judged axis (4.17
against 3.03 overall), matched the best trade result, and stayed fast, at under 2 GB as Ollama
reports it. Before switching:

- repeat the run, because it is one judge, n = 18 judged replies per model, and sequential arms;
- check gemma's real memory use with the game rendering;
- check its four slower trade turns, about 4 s each.

`qwen3.5:4b`, recommended in the first entry today, was not in this run, so the two cannot be
ranked against each other here. Every model in the table, and `qwen3.5:4b`, can be picked in the
game with F7.

**What would change this.** A second judge or human ratings that disagree; a repeat in which
gemma's lead falls inside the run-to-run spread; or gemma failing to fit beside the game's
renderer.

**Gemma's memory, checked the same evening.** The GPU was empty (0 MiB) when `gemma4:e2b-it-qat`
was loaded alone under the lock with a 4096-token context.

- **What `/api/ps` reports:** `size` and `size_vram` were both 1.66 GB.
- **What the card shows:** `nvidia-smi` showed **2.78 GB** used, all of it Ollama's `llama-server`.
- **Where the 4.3 GB file goes,** from the server log:
  - 1.34 GB of model weights on the GPU, all 36 layers offloaded;
  - **2.15 GB in pinned system memory** (`CUDA_Host`): gemma's per-layer embeddings, which
    llama.cpp keeps off the card by design;
  - its vision and audio projectors (`gemma4v`, `gemma4a`), loaded although the game sends
    neither;
  - KV cache and compute buffers.
- **Cold load:** 10.3 s, which the startup notice and the warm-up cover.
- **Measured once, nothing else running, not repeated.**

So `/api/ps` undercounts gemma's GPU use by about 1.1 GB, and gemma also takes about 2 GB of
system RAM. It still fits: 2.8 GB beside the game leaves room on the 8 GB card. During the
coordinator's play-check, the game, gemma and a leftover `llama3.2:3b` together used 5.5 GB, and
G5d's unload on switching now removes that leftover.

### Unasked offers: one negative example cut them to none on both models (5 November 2026)

**What changed.** The treatment is a **negative few-shot example**. `AgenerelaNpcDecisionDriver` passes
`DecideOptions.IdleExampleStimulus`, so the few-shot block shows one line answered with `none`
alongside the trade examples. The control is G5b's prompt, which has no such line. A second
treatment was a stricter rule line ("Make an offer … only when the traveler asks to buy something
from you or to sell you something. For greetings, questions, rumours and small talk, choose none
and just answer.").

**The set.**

- **Lines asking for nothing:** 12 new lines to Mara (rumours, small talk, directions, feelings,
  a question about her stall that is not a purchase), plus G5b's browsing line, so 13 in all.
- **Asked trades:** G5b's five buy and sell lines.
- **Overlap:** none repeats an evaluation prompt or any example in the prompt (hard rule 4). The
  first negative example, "Fine weather for the market, isn't it?", was dropped after run 1 because
  "The weather is changing." is an evaluation prompt. Run 2 measured the line now shipped, "Good to
  see a friendly face again."

**Conditions.**

- **Staging:** each case was staged alone, with a fresh driver and trade state, and the player
  carrying a pelt and iron scrap, so both trade actions were offered.
- **Arms:** interleaved line by line on one model (measuring.md, item 4).
- **Models:** `gemma4:e2b-it-qat`, then `qwen3.5:2b`, one resident at a time, warmed with three
  throwaway requests, under the Ollama lock.
- **Setup:** RTX 4060 Laptop, AC power, Ollama 0.40.14, temperature 0, `MaraTradeLiveCheck` with
  `LV_ARMS`. Run 1 was 17:27–17:33, run 2 17:34–17:39. No CPU side load was recorded in the
  window.

| Model | Arm | Unasked offers shown (of 13) | Asked trades correct (of 5) | p50 |
|---|---|---|---|---|
| `gemma4:e2b-it-qat` | control (run 1 / run 2) | **7 / 6** | 5 / 5 | 0.85 / 0.95 s |
| | negative example, weather (run 1) | 0 | 5 | 0.85 s |
| | **negative example, shipped (run 2)** | **0** | 5 | 0.90 s |
| | stricter rule (run 1) | 0 | 5 | 0.85 s |
| `qwen3.5:2b` | control (run 1 / run 2) | **10 / 10** | 4 / 4 | 1.13 / 1.15 s |
| | negative example, weather (run 1) | 0 | 4 | 1.10 s |
| | **negative example, shipped (run 2)** | **0** | 4 | 1.12 s |
| | stricter rule (run 1) | 1 | 4 | 1.15 s |

**Classified.**

- **Asked trades:** `qwen3.5:2b`'s one miss is the same in every arm. It quoted a *purchase* of
  the player's iron scrap, and the pair guard contained it (contained by a guard, not
  wrong-but-legal).
- **Unasked offers:** every one shown in a control arm was a legal quote on a line that asked for
  none (wrong but legal). In its control arms `qwen3.5:2b` chose an offer on all 13 lines; guards
  contained 3 of them.
- **The rule arm on `qwen3.5:2b`:** it left an offer on 1 line, and gave 9 of its `none` answers
  a target id. That is harmless, since `none` executes nothing, but it is a sign the model was
  confused.
- **Pipeline errors:** none.

**How much to trust it.**

- **Size and repetition:** n = 13 per arm, one set of lines, and two runs. The control arms
  repeated closely: 7 then 6 on gemma (so temperature 0 is not fully deterministic here), and 10
  then 10 on qwen.
- **The gaps:** 6/13 against 0/13 and 10/13 against 0/13 are far outside that spread. Each line
  ran in both arms, so the test is paired. In run 2 the example improved 6 lines and worsened none
  on gemma, and improved 10 and worsened none on qwen. An exact McNemar test gives two-sided
  p = 0.031 and p = 0.002.
- **Why it is not an accuracy claim:** the lines are hand-written, the classification is by the
  expected label alone, and the negative example has been measured only on Living Vale's trade
  prompt.
- **Not measured:** the example reaches every NPC, Iven and the quest NPCs included, beside their
  own action examples. How they behave with it was not measured here.

**Kept:** the negative example, now the driver's default (`DefaultIdleExample`). The rule line
stays as it was, because it did no better on gemma, did worse on `qwen3.5:2b`, and changes
wording measured elsewhere.

**What would change this.** A larger set on which the example makes a model miss asked trades,
or an evaluation-set A/B showing a `none` example costs command accuracy. Appendix A warns that
plausible prompt changes have hurt before.

### Living Vale's quest routes on gemma4:e2b-it-qat: the right action six times in seven, and two kinds of slip (5 November 2026)

**A smoke check, not an accuracy claim.** It was one run of seven lines, with no labels and no
control arm (hard rule 2). It asks whether G4c's new route actions are reachable from a local
model that sees them only through the prompt lines `RouteActionBinding` writes.

**What ran.** `QuestRouteLiveCheck` in Unity batch mode, through the `Agent` driver, against a
local Ollama 0.40.14 with `gemma4:e2b-it-qat`, taken under the shared lock and nothing else
loaded. Each line was staged on fresh game state at the quest stage it needs. Fen, Kest, Brann
and Elra had no authored persona yet, so each was built from its descriptor, and what they knew
came only from the target descriptions and status lines.

**What it showed.**
- Six of seven picked the expected action and target. These were Fen's `share_information`
  (13.7 s, a cold load), Kest's testimony and payment deals, Brann's permit, and both deliveries.
  The warm turns took 1.0 to 1.7 s.
- The bluff ("I have proof. A witness watched your people…", with no clue recorded): the
  testimony deal was absent from the schema, so it could not be chosen. Kest picked
  `exchange_kest_payment` instead, an unrequested offer of the kind framework gap 9 describes. His
  line refused the proof. Nothing was unlocked, and the card still needed the player's 20 gold.
- Brann chose `grant_access` correctly, but said "You must first earn a delivery permit". The
  statement contradicted the action, and nothing checks that the two agree.

**What it means.** The prompt-line workaround (framework gap 1) is enough to reach every new
action on this model. The two slips are what the game's cards and receipts are for: a wrong
offer still needs the player's button, and the receipt line says what really happened. Whether
the status lines ("Claims of proof are only words") help is unmeasured. That needs an A/B run with
and without them (gap 18). The run predates G5f's `none` example (entry above), which may well remove the bluff's unasked offer. Rows are in `.scratch/g4c/live/routes.jsonl` in the G4c worktree.

### First model A/B: `qwen3:4b-instruct-2507` 65/76, `qwen3.5:2b` 56/76, but the 4B wrote its fields in another order (5 November 2026)

The first of #151's runs through the A/B runner (#145), and the first live run of its
`Sequential` order. The control arm was `qwen3.5:2b`, the treatment `qwen3:4b-instruct-2507-q4_K_M`,
the first pick on the 8 GB shortlist in [`model-candidates.md`](model-candidates.md). Both arms
had the guards on. The set was our 76 prompts (`UnityProject/Assets/Evaluation/`, last changed in
87d1e4d) against the `VillageGuard` profile. All of the 2B's arm ran, then its model was unloaded,
then all of the 4B's.

**Result: the runner marks the difference beyond noise, but it is not a clean comparison of two
models.** Ollama kept the schema's field order (`action`, `target`, `statement`) for the 2B but not
for the 4B, which wrote `statement` before `target` on every request (below). The arms differ in
the model *and* in the field order, so this says nothing about which model chooses better (hard
rule 2).

| Outcome | `qwen3.5:2b` | `qwen3:4b-instruct-2507` |
|---|---:|---:|
| Correct | 56 | 65 |
| Wrong legal action | 16 | 10 |
| Contained by a guard | 1 | 0 |
| Rejected when an action was expected | 3 | 1 |
| Pipeline error | 0 | 0 |
| **Accuracy** | **73.7%** | **85.5%** |

| Category (n) | `qwen3.5:2b` | `qwen3:4b-instruct-2507` | 4B − 2B, points (floor) |
|---|---|---|---|
| Simple commands (9) | 5 correct, 4 wrong | 9 correct | +44.4, **beyond noise** (23.6) |
| Commands with a target (11) | 10 correct, 1 wrong | 11 correct | +9.1, within noise (21.3) |
| Impossible requests (9) | 9 correct | 9 correct | 0, within noise (23.6) |
| Conversation (9) | 8 correct, 1 wrong | 8 correct, 1 wrong | 0, within noise (23.6) |
| Situations (19) | 13 correct, 5 wrong, 1 contained | 15 correct, 4 wrong | +10.5, within noise (16.2) |
| Events (19) | 11 correct, 5 wrong, 3 rejected | 13 correct, 5 wrong, 1 rejected | +10.5, within noise (16.2) |
| **All (76)** | **56** | **65** | **+11.8, beyond noise (8.1)** |

**The 4B wrote `statement` before `target`, on every request.** A pass-through proxy logged the
raw responses of a repeat run: all 76 of the 4B's scored responses came back as `action`,
`statement`, `target`, and all 76 of the 2B's as `action`, `target`, `statement` (each arm's 3
warm-ups, not part of the run, were in the same order as its scored responses). The
request was the same for both, with `required` and `propertyOrdering` in that order and
`additionalProperties: false`. Two probes outside the framework (`.scratch/151-first-ab/`)
narrowed it down. Given a schema in the order `statement`, `target`, `action`, the 2B followed
it on all 3 stimuli, so Ollama does hold the order for the 2B. The 4B still wrote `action`,
`statement`, `target`, which is alphabetical. So on Ollama 0.40.14 the schema's order is lost for
this model, perhaps because its keys are sorted somewhere on its path; that was not investigated.
This matters because the field order is a settled rule (Appendix A, DR-008): free text written
before the target is the order measured to make the model rationalise its own reply. The 4B's arm
therefore did not run the framework's design, and the framework cannot tell: the report holds
parsed fields, not the raw text.

**The 4B's probabilities are from before the mask too, and the guard caught nothing on it.** The
scarecrow check (3 November entry) on the 4B: at the target's first token it wanted `sc` at
−0.00, and the schema forced `t` at −26.5 (`tower`). On the 2B it wanted `sc` at −0.01 and was
forced to `br` at −15.0. So the readings mean the same on both models, but they behave
differently. On the 4B, 32 of the 34 readings on a registered target were 1.000, wrong answers
included, so the grounding guard had little to act on:

- 2B: the guard stopped 4 answers, all of them wrong. "The watchtower is unattended."
  (`move_to/north_tower`, 0.592) was contained. Three events whose answer had the wrong action or
  target count as rejected, because their label was an action: "An enemy was spotted near the
  bridge." (`attack_target/wolf`, 0.583, the model wanted "enemy"), "A scout spotted movement near
  the north tower." (`attack_target/wolf`, 0.53, wanted "unknown") and "A cart has overturned
  outside the village." (`move_to/bridge`, 0.43, wanted "bar…"). Its lowest reading on a right
  answer with a target was 0.885.
- 4B: one stop, and it was a right answer refused. "A cart has overturned outside the village."
  was `move_to/eastern_road` as labelled, at 0.368: it wanted "cart". "The watchtower is
  unattended." went through to `move_to/north_tower` at 1.000, where the 2B's was contained.
  "An enemy was spotted near the bridge." went through to `attack_target/wolf` at 0.863.

**Answers worth reading.**

- **The 2B answered `none` to all four "stay" commands while following** ("Wait here.", "Stay
  where you are.", "Do not move.", "Hold position."), with lines like "I will wait for you to make
  your move." It understood and still chose `none`. That is the whole simple-command gap; the 4B
  got all four.
- **The 4B acts on situations that need no action.** "Good morning." → `follow_player`
  ("Good morning. Following you."). "Rain has started to fall." → `move_to/village_gate`. "A
  villager waves from across the square." → `move_to/well`. "The road is blocked." →
  `move_to/eastern_road` (the 2B said `move_to/bridge`).
- **The 4B missed a state change.** "The player has reached the shelter.", while following, was
  `move_to/shelter`, not `stop_following`.
- **Both models attack the wolf on enemy reports.** "An enemy was spotted near the bridge." (both)
  and "Lights were seen in the north tower." (4B) were answered with `attack_target/wolf`, the
  only enemy the agent knows of, where the label is `move_to`.
- **"The animals at the market are restless."** The 4B answered `move_to/wolf`: a legal pair,
  since `move_to` takes any target, but the wrong action.

**Sequential mode worked against the real server.** After the 2B's last prompt the runner
unloaded it (`keep_alive: 0`) and recorded `unloaded: true`, and the 4B's reading before its
warm-up showed nothing resident. Each arm's model was the only one listed after its warm-up and
at its end, fully in VRAM: 2254 of 2254 MB for the 2B, 3032 of 3032 MB for the 4B. No warnings,
no pipeline errors, 137 s for the run. One gap: the runner does not unload the last arm's model,
so the 4B stayed resident (Ollama's default five minutes) until it was unloaded by hand.

**The repeat.** With the arms swapped (4B first), and once more through the proxy, all 152
answers were identical to the first run: same action, target, statement and reading. At
temperature 0 the run is deterministic on this machine, so the repeat shows that the arm order
did not matter here. It does not shrink the noise floor, which stands for drift between sessions
and prompt sets, not for sampling.

**What the noise check allows.** +11.8 points overall against a floor of 8.1, and +44.4 on simple
commands against 23.6, are marked beyond noise. Every other category is within noise. With the
field order differing between the arms, the difference cannot be put down to the model. The A/B
that can is a rerun with the 4B writing the fields in the framework's order.

**Latency, the first run only.** Median 0.80 s for the 2B and 0.72 s for the 4B; 90th
percentile 0.93 s and 0.81 s. Another session's Unity EditMode batch run loaded the CPU from
"about 11:29 to 11:31" local time by its own account. A sampler (one reading about every 6 s)
first saw its process at 11:31:12, during the swapped run. If it did start at 11:29, it overlapped
the last minute of the 4B's arm (about 11:28:50–11:29:51); the 4B's median was 0.718 s over its
first 38 prompts and 0.728 s over its last 38. These latencies are the 76 scored prompts per arm
only; the runner never reports its warm-ups. In the proxy run's raw responses, again the 76
scored prompts per model only, prompts were 354–405 tokens on the 2B and 333–383 on the 4B,
answers 27–44 and 25–41 tokens, and the 4B's lines were shorter (median 28 characters against
46.5). No reasoning came back from either model. One sequential run on one laptop: not a latency
claim.

Warm-ups, for information only, not part of the run: each arm's first 3 requests, told apart from
the scored prompts both by position and by their stimuli ("Good evening.", "Is the road quiet
tonight?", "Stay sharp."). Prompts 345–348 tokens on the 2B and 325–328 on the 4B, answers 25–36
and 31–37. The 2B answered `none` to all three. The 4B acted on two: "Is the road quiet tonight?"
→ `move_to/eastern_road` and "Stay sharp." → `follow_player`, which fits its over-acting above.

**Conditions.**
- `test` at 3f0a87c (framework 0.4.0-preview.1), Unity 6000.3.23f1, PlayMode in batch mode, from
  a throwaway test whose text is in `.scratch/151-first-ab/README.md` on the lead's laptop.
- Ollama 0.40.14 at `http://127.0.0.1:11434`, on CUDA on the RTX 4060 Laptop 8 GB (the server
  log's "inference compute" line). The report's "Null Device" is Unity's `-nographics` device,
  not Ollama's.
- `qwen3.5:2b` Q8_0 (digest 324d162b…) and `qwen3:4b-instruct-2507-q4_K_M` Q4_K_M (0edcdef3…).
  Temperature 0, 4096 context, `think: false`, logprobs on.
- Mains power. Nothing else resident before either arm, and the Ollama lock held for the run.
- Three warm-up requests per arm. Targets as observations (`id: description.`). The state as one
  observation, "You are following the player." or "You are not following the player." (the
  Phase 3 wording), and on `Agent.State`. Idle example "Fine day for it.". `CheckTarget` on.
- The arms ran sequentially, the 2B first.
- Few-shot overlap, checked by hand as `measuring.md` asks: one prompt, `target_005` "Go stand
  at the north tower.", is a partial phrasing of `move_to`'s example "Go and stand by the {0}."
  (rule 3). Both arms answered it correctly, so it does not move the difference, but the set
  should replace it.

**What would change this.** A 4B arm whose raw responses keep `action`, `target`, `statement`,
on the same set: if it still scores above the 2B beyond noise, that is a model result. Or a later
Ollama that holds the order for the 4B.

### Why `qwen3:4b-instruct-2507` wrote `statement` before `target`: Ollama's native chat path sorts the schema's keys (5 November 2026)

A follow-up to the entry above, from the raw replies already captured, Ollama's and llama.cpp's
source, and Ollama's own server log. No new request was sent to a model. It is a finding about
how the response schema reaches the sampler, not an accuracy claim, and it says nothing about
which model chooses better.

**Result: the order is lost in Ollama, not in our schema and not in the model.** Ollama 0.40.14
has two ways to serve a chat. Through the one `qwen3:4b-instruct-2507` takes, Ollama reads our
schema into a Go map and writes it out again, and Go writes a map's keys in sorted order. So
llama.cpp is handed `properties` as `action`, `statement`, `target`, alphabetical, and builds its
grammar in that order. `qwen3.5:2b` takes the other way, where the schema is passed on as the
bytes we sent.

**1. Which replies kept the order.** From the proxy capture of the first A/B's third run (raw
`/api/chat` replies, scored prompts only):

| Model | Scored replies | `action`, `target`, `statement` | Out of order |
|---|---:|---:|---:|
| `qwen3.5:2b` | 76 | 76 | 0 |
| `qwen3:4b-instruct-2507` | 76 | 0 | 76, all `action`, `statement`, `target` |

The 3 warm-ups per model, not part of the run, had the same orders. With the schema's properties
reversed (`statement`, `target`, `action`; `order_probe_reversed.log`), the 2B followed the
reversed order on all 3 stimuli and the 4B still wrote `action`, `statement`, `target`.

**2. Where Ollama 0.40.14 loses it.** Read in the source at the v0.40.14 tag (commit 9a3e817,
2 November 2026):

- `chatModeForModel` (`server/routes.go`) sends a chat down Ollama's own rendered path when the
  model's manifest names a `renderer` or `parser`, uses harmony, or is served through its Go
  template. Otherwise the chat goes down the native path, which hands the model's own GGUF chat
  template to llama-server. `/api/generate` makes the same choice.
- On the rendered path, `llm/llama_server.go` passes `format` on to llama-server's
  `/completion` unchanged, as `json_schema` (a `json.RawMessage`), so the key order survives.
- On the native path, `llamaServerChatResponseFormat` (`llm/llama_server.go`) unmarshals the
  schema into `map[string]any` and puts it in an OpenAI-style `response_format`. Go's
  `encoding/json` writes map keys sorted ([`encoding/json` Marshal](https://pkg.go.dev/encoding/json#Marshal):
  "The map keys are sorted"), so `properties` comes out alphabetical. `required` is an array and
  keeps its order, but that does not help (next point).
- llama.cpp b11963 (28 October 2026, the version in Ollama's `LLAMA_CPP_VERSION`) builds an object
  rule in `common/json-schema-to-grammar.cpp`, `_build_object_rule`, by walking `properties` in
  the order given and putting each required one in the grammar in that order. `required` is read
  as an unordered set, for membership only. `propertyOrdering` appears nowhere in that file, so it
  is ignored.

Ollama's own server log says which way each model went, in its "template selection" line. During
the run, `qwen3.5:2b` logged `selected=renderer_parser` (its manifest has `renderer: qwen3.5` and
`parser: qwen3.5`). `qwen3:4b-instruct-2507` logged `selected=gguf_chat_template`. Its manifest has
neither a renderer nor a parser, only a Go template, and Ollama prefers the GGUF template when it
advertises more capabilities than the Go one (`shouldPreferChatTemplate` in `server/images.go`;
here `tools thinking completion` against `completion tools`).

**Not new in 0.40.14.** v0.34.2 (15 September 2026) has the same `llamaServerChatResponseFormat`,
the same two paths and the same rule for preferring the GGUF template. None of the release notes
from v0.34.0 to v0.40.14 mentions key order in `format`. What we cannot say is how 0.34.2 served
this 4B: it was pulled on 4 November, already on 0.40.14.

**Unverified.** That the native path sorted *this* request is read from the code, not seen: the
server log does not print the schema or grammar that reached llama-server. The rest of the chain
was observed: the path in the log, and the alphabetical order in the replies even when the schema
was reversed. Not read at all: Ollama's OpenAI-compatible `/v1/chat/completions` endpoint.

**3. Is it our schema?** `JsonSchemaSerializer` emits, as the capture shows, an object with
`properties` in the order `action`, `target`, `statement`, each a string with a description and,
for the first two, an `enum`; `required` in the same order; `propertyOrdering` in the same order;
and `additionalProperties: false`. That is correct, and the rendered path honours it. No object
schema can survive the native path, because a JSON object's keys have no order by definition and
the path treats the schema as a map. The only shapes that would survive are field names that sort
into the right order, which would mean renaming fields and touches DR-008, or a reply that is an
array instead of an object (`prefixItems`, which llama.cpp supports), which changes the reply's
format. Neither is proposed here.

**4. The two results run on 0.40.14.**

- **The first A/B (65/76 against 56/76).** The 2B arm ran in the framework's order: 76 of 76
  in the capture, which repeated the scored run answer for answer. The 4B arm ran with
  `statement` before `target` on all 76. That is why the entry above makes no model claim.
- **The Phase 3 gate run (19/20, 3 November).** It ran only `qwen3.5:2b`, and the 3 November
  server log shows `selected=renderer_parser` for every load that day. That is the path that keeps
  the order, and the 2B followed the schema's order in every capture today. Its raw replies were
  not captured, so "in order" is inferred, not counted, but nothing points the other way.

**The other models, from the "template selection" lines in the server logs** (most from the
4 November smoke test of the candidates). `granite4.1:3b` (`go_template`), and
`nemotron-3-nano:4b`, `lfm3:1.7b` and `qwen3.5:4b` (`renderer_parser`), take the path that keeps
the order. **`MiniCPM5-2B`
(`hf.co/openbmb/MiniCPM5-2B-GGUF:Q4_K_M`) logged `gguf_chat_template`**, so by the same code it
should come back alphabetical too. That is a prediction, not a count.

**A possible link to the guard, unverified.** On the 4B, 32 of 34 grounding readings were 1.000
(entry above). With `statement` written first, the line usually names the place ("Go to the
well.") before `target` is sampled, so the target may be close to certain by then. That would
blind the grounding guard on any model served this way. It needs an A/B with the order restored
to check.

**What a fix could look like.** Nothing in the framework is changed here.

- **Detect it (no measurement needed).** `OllamaProvider` reads the raw reply's key order and
  records whether it was `action`, `target`, `statement`. The runner counts it per arm and
  warns, or refuses, when the warm-ups already come back out of order. Today nothing can see it,
  because the report keeps only parsed fields.
- **Restore it (each needs a live check first).** Upstream, the native path could pass the schema
  on as raw bytes instead of a map, which would be a report to Ollama. On the lead's machine,
  setting `OLLAMA_GO_TEMPLATE=1` should send a model that has a Go template, the 4B included, down
  the rendered path. But that also changes which chat template formats the prompt, a second
  variable for an A/B.

**What would change this.** A server log or llama-server request showing the 4B's schema in our
order, which would mean another step reorders it. Or an Ollama release whose native path keeps
the schema as sent.
