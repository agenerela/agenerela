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
   *Update, 9 November 2026:* the idle example's with-vs-without has now run once, on the 76
   evaluation prompts and `qwen3.5:2b`: 56/76 with it, 59/76 without, and `stop_following` 2/7
   against 6/7 (findings, "The idle example on the profile's own line"). Overall that is
   within the noise floor (8.1 points), but `simple_command` moved 33 points, beyond its own floor.
   It is one model, and it does not settle whether the line should go. Counter-examples
   and the action/target list were run the same evening (both inside the noise floor too). The other
   rules in this list are still owed.
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

### Living Vale's quest routes on gemma4:e2b-it-qat: six of seven, then seven of seven on the merged branch; a statement can contradict its action (5 November 2026)

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
and without them (gap 18). Rows are in `.scratch/g4c/live/routes.jsonl` in the G4c worktree.

**Re-run the same evening, on the merged branch: seven of seven.** This was the same check with
the same seven lines and model, on `69-living-vale` at 7c2d34e. Two things had changed since the
first run, so it cannot say which one helped. One was G5f's `none` example (entry above). The
other was G4b's authored personas for Fen, Kest, Brann and Elra (`RegionNpcPersonas`), which
replaced the descriptor-built ones.
- The bluff now gets `none` / `no_target` and no card. Kest's line is "I do not care what you
  claim. If you have proof, show it to me". The unasked payment offer is gone.
- The other six picked the same action and target as before. Fen's line now carries the clue in
  her own words ("grey cloaks… toward Broken Watch"). Warm turns took 0.9 to 1.3 s, and Fen's cold
  load 19.6 s.
- Brann's statement still contradicts his action. He chose `grant_access` and said "Show me the
  permit." So the mismatch did not come from the missing persona. It stays open, and the receipt
  line is still what tells the player the truth.

This is one run of each, not an A/B, so it is no evidence that either change improves accuracy
(hard rule 2). The rows are in `.scratch/g4c/live/routes2.jsonl` in the G4c worktree.

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

### GreyBoxVillage's situations played live: a new action took the bell's place (5 November 2026)

#153's situations slice played its presenter script four times on the real model
(`VillageCapture.PresenterLiveFromCommandLine`): four agents on one queue answering the wolf,
a thief, a fire and night at the gate. Four runs of one scripted scene are anecdotes, not
measurements (hard rule 2). The tables are in the PR. Runs 1 to 3 found two game bugs and two
harness bugs, each fixed before the next run, so the runs did not all run the same code.

- **A wolf 14 m from Bram, with both `chase` and `sound_alarm` offered: `chase`, 4 of 4.** The
  new action took the bell's place, and so the chain to Captain Hale. Asked by the player, "Ring
  the bell, Bram! Warn the village!", he rang it once in two runs, and answered `warn_player`
  the other time, saying the bell was ringing when it was not.
- **The thief, 12 guard decisions with `chase` offered: `attack_target` 9, `chase` 2,
  `warn_player` 1.** `attack_target` had walked to where the thief had been, so it got away in
  run 2. It now runs after a running thief too.
- **Odile chose `call_guard` in 8 of 8 decisions** (four thieves, four fires), always naming Bram.
  **Every guard asked about the fire chose `fetch_water`, 12 of 12.** Wren chose `none` on
  reaching the gate (4 of 4), and `open_gate`, then `close_gate`, when the player asked (run 4).
- **The grounding guard fired twice.** In run 1 Wren was asked about a wolf already fleeing,
  which is not a target, and wanted to write "wolf". A fleeing wolf is no longer reported. In
  run 3, Hale's `send_guard` read 0.736 at the target (it wanted "arms…"), below 0.8.
- **Bram answered "Quiet night, Bram?" with "Fine day for it.", 4 of 4.** That is the stimulus
  of the `none` action's few-shot example, copied as the statement, at night. Not measured;
  Phase 4 is where to count how often an example's text comes back as the answer. Since then #218 (9 November) lets an example
  carry its own statement (`Statement` on `[Example]` and `[CounterExample]`, `IdleExampleStatement`,
  `DecideOptions.ExampleStatements`), so an example no longer has to show its stimulus's text as
  the reply. Written statements and their effect are unmeasured; the A/B is listed under "Waiting"
  in `docs/live-runs.md`.
- **Latency.** 65 warm decisions took 0.48–2.33 s, median 0.95 s. With three agents asked at
  once, the last waited up to 3.40 s in the queue. The first request of a fresh session took
  7.1 and 9.9 s (runs 1 and 3), while the model loaded.

**Conditions.**
- Branch `153-gbv-situations`, Unity 6000.3.23f1, Play mode in batch mode.
- `qwen3.5:2b` on Ollama 0.40.14, at `http://127.0.0.1:11434`. No other model was loaded
  (`/api/ps` was empty before the first run).
- RTX 4060 Laptop on AC power. Taken under the shared Ollama lock, with no Living Vale run.
- Quiet timers off, so every decision came from the script.

**What would change this.** A Phase 4 run of the wolf prompts with and without `chase` in the
offered set, on the same model. If the bell's share does not fall with `chase` offered, this was
noise.

### The framework's prompt features: no worse than Living Vale's own on gemma or qwen3.5:2b (5 November 2026)

**What changed.**

- **Treatment (framework arm):** #193's features, all off by default in the framework.
  - `DecideOptions.ListActionsAndTargets`, so the system prompt lists the offered actions with
    their descriptions and the offered targets with their `IDescribedTarget` line;
  - `DecideOptions.CounterExamples`, with a new `CounterExampleStimulus` on every Living Vale
    action, each a line near the action's subject that asks for nothing;
  - G5f's idle example, set as `Agent.IdleExampleStimulus` instead of per call.
- **What the framework arm still had:** the game's per-action rule lines, its status lines and the
  per-turn example rewriting (`RefreshExamples`).
- **Control (shipped arm):** the game's own prompt as shipped after G5f. The bindings write every
  action, description and target as observation lines (`PromptLines`), with the idle example
  passed per call.

**The set.** One run of `MaraTradeLiveCheck` and `QuestRouteLiveCheck` per model.

- **Trade:** G5b's five asked trades.
- **Talk:** G5f's 12 lines asking for nothing, plus the browsing line, so 13.
- **Quest routes:** G4c's seven route lines (Fen, Kest twice, Kest's claim without proof, Brann,
  Elra, Iven).
- **Wording:** the counter-examples are new, and none appears in the evaluation set or any
  measured line (checked by search; hard rule 4).
- **Staging:** each case was staged alone, with a fresh driver and game state.

**Conditions.**

- **Arms:** interleaved line by line on one model (measuring.md, item 4).
- **Models:** `gemma4:e2b-it-qat`, then `qwen3.5:2b`, one resident at a time, each warmed with
  three requests, under the Ollama lock.
- **Setup:** RTX 4060 Laptop, AC power, Ollama 0.40.14, temperature 0, 22:56–23:01. No CPU side
  load was recorded.

| Model | Arm | Asked trades (of 5) | Unasked offers (of 13) | Quest routes (of 7) | p50 |
|---|---|---|---|---|---|
| `gemma4:e2b-it-qat` | shipped (control) | 5 | 0 | 7 | 0.85 s |
| | **framework** | 5 | 0 | 7 | 0.80 s |
| `qwen3.5:2b` | shipped (control) | 4 (1 contained by the pair guard) | 0 | 4 (3 chose `none`) | 1.15 s |
| | **framework** | **5** | 0 | **6** (1 wrong but legal) | 1.20 s |

**Classified, `qwen3.5:2b`.**

- **The shipped arm's missed trade** is G5b's known iron-scrap mix-up: a purchase quote on a sale
  bundle, which the pair guard contained. The framework arm got it right.
- **The shipped arm's three route misses** chose `none` where an action was expected:
  - Fen's raid account;
  - Kest's testimony exchange;
  - Kest's payment exchange.
- **The framework arm's one route miss** was wrong but legal: on the testimony line it offered
  the payment exchange.
- **No pipeline errors** in either arm.

**How much to trust it.** It is small n and one run.

- **gemma:** the two arms are identical, so the run cannot tell them apart; "no worse" is all it
  shows.
- **qwen:** the framework arm got 3 more lines right out of 11 lines that asked for an action (5 trades, 6 routes), and lost none. That
  is suggestive, not significant.
- **What it does not show:** the arm changed three things together, so it cannot say which one
  helped.

**Kept:** the framework arm, now the game's only prompt. It was no worse on correct actions or
unasked offers, which was the condition for switching. The game's `PromptLines` listing is
deleted; its rule and status lines and `RefreshExamples` stay (framework gaps 20).

**What would change this.** The evaluation-set A/B owed by #151 showing the list or the
counter-examples costing command accuracy, or a repeat on a larger Living Vale set reversing the
qwen gain.

### Model lifecycle calls against a real Ollama: a warm-up is an empty `/api/generate` (5 November 2026)

**What was checked.** `OllamaProvider`'s new `IModelLifecycle` calls (#190 workstream D,
DR-019), once, through the framework's own code: the `RequiresOllama` PlayMode test
`ModelLifecycleLiveTests`. EditMode tests script the same shapes; this is the check that the
server answers as they assume. Not an accuracy or latency claim, and no control arm is needed for
one.

**Conditions.** Ollama 0.40.14 on the lead's laptop (RTX 4060 Laptop, 8 GB), on AC power,
`qwen3.5:2b`, nothing resident before or after (`/api/ps` empty), the shared Ollama lock held,
one run.

**Result.**
- `ListModelsAsync` (`GET /api/tags`) listed 11 models, each with a size, `qwen3.5:2b` among them.
- `UnloadAsync` (`keep_alive: 0` on `/api/generate`, then `/api/ps` polled) returned true both
  times, the first time for a model that was not loaded, so the unload neither loads nor fails.
- `WarmUpAsync` (`POST /api/generate` with only `model`): **7.08 s cold**, then **0.01 s** with
  the model loaded. So the empty generate does load the model, and a second warm-up costs nothing.
- A model that was never pulled answered 404 with Ollama's "not found" error, which the provider
  reports as `ModelNotFound`.

**What it means.** A game can warm the next model up before moving its agents onto it, and the
first decision then skips the load, about 7 s for this model on this card. The warm-up takes no
slot in the decision queue (DR-015, as amended), so it never delays a player's turn on the
provider the agents are still using.

**The warm-up must send the decisions' context size (same evening, after the Codex review).** The
run above sent no `num_ctx`, so Ollama loaded the model at its own default, which `/api/ps`
reports as **16384** on this machine. The decisions send `OllamaSettings.ContextSize`, 4096 by
default, so the first decision after such a warm-up loads the model again. The run above did
not see this, because it made no decision after its warm-up. With a provider set to `num_ctx`
8192, the first decision after the old warm-up took **7.46 s**. The warm-up now sends the
decisions' `num_ctx`, and the same check took **1.18 s**. The cold warm-up took 9.04 s before the
fix and 7.98 s after; there was one run each, under the same conditions, the old warm-up first.
Two more runs followed. The first, after the test was changed to time the same decision twice
(Codex review, F5), gave 1.12 s and then 0.84 s. The second, after the test was changed again
to read the loaded size from `/api/ps`, ran the old warm-up and the fix one after the other.
With the old warm-up, the model was loaded at 16384, and the first decision took 5.99 s. With
the fix, it was loaded at 8192, and the first decision took 0.99 s. A first decision near 1 s is
ordinary; Phase 2's warm decisions were under 0.6 s.

**What would change this.** An Ollama release where an empty generate stops loading the model, or
where `keep_alive: 0` on a model that is not loaded starts loading it, or where a request at a new
context size no longer reloads the model. Re-run `ModelLifecycleLiveTests` after an Ollama
upgrade, as for the schema probe.

### Ollama keeps a paired schema's replies to the pairs it offers, on both chat paths (6 November 2026)

A check of the paired schema DR-020 describes (#190 B), before it ships behind
`DecideOptions.PairTargetsWithActions`: does Ollama accept a root `anyOf` of complete objects as
`format`, and does the grammar it builds stop an action being sampled with a target of another kind?
It is a grammar check, **not an accuracy claim**: ten made-up stimuli per arm, one run each.

**Setup.** `tools/benchmarks/paired_schema_probe.py`. A merchant with `move_to` (any target),
`complete_quest` (the two quests only) and `none`; targets `village_gate` and `well` (places),
`quest_wolf` and `quest_lantern` (quests). The prompt is laid out as `OllamaProvider` lays one out,
with the action and target list. Ten stimuli tempt `complete_quest` on a place ("Wrap up the well
task for me."), a few are plain. Four schemas per stimulus: **flat** and **paired**, the shapes the
framework sends, and both again without `none` and `no_target` (**forced**), a shape the framework
never sends, so that the model has to act. Ollama 0.40.14, RTX 4060 Laptop 8 GB, on mains power,
`temperature` 0.7, `seed` 7, thinking off, one model resident at a time (`/api/ps` empty before
each). Raw replies: `.scratch/190b/paired_probe.jsonl` in the session's checkout.

| Model (chat path) | Flat | Paired | Flat, forced | Paired, forced | Key order `action`, `target`, `statement` |
|---|---:|---:|---:|---:|---:|
| `qwen3.5:2b` (rendered) | 10/10 | 10/10 | 7/10 | 10/10 | 40/40 |
| `qwen3:4b-instruct-2507-q4_K_M` (native) | 8/10 | 10/10 | 7/10 | 10/10 | 0/40 |

Each cell but the last counts replies whose pair the paired schema offers. Every request was
answered; Ollama refused no schema.

- **The grammar holds the pairing on both paths.** Every wrong pair, `complete_quest` on
  `village_gate` or `well`, came from a flat schema: 2 on the 4B with `none` available, 3 on each
  model forced. No paired reply had one.
- **What the model chose instead is not always right.** Forced away from the well, the 2B answered
  "Complete it at the well, I left everything there." with `quest_lantern`, and the 4B answered
  "Wrap up the well task for me." with `quest_wolf`. Pairing makes the wrong kind of target
  unsayable; it can still swap one quest for another, which is the grounding guard's job.
- **The field order is what it was.** The 4B, on Ollama's native path, wrote `action`,
  `statement`, `target` in all 40 replies, the alphabetical order the entry of 5 November found for
  the flat schema; each branch's keys are sorted the same way. The 2B kept the framework's order in
  all 40.
- **A side note on layout, not pursued.** A first version of the probe put the examples in the user
  turn rather than the system message. The 2B then answered `none` to all 40 flat and paired
  requests, over two runs (`paired_probe_layout1.jsonl`), so the prompt layout mattered more than
  the schema there.

**What it does not show.** Whether pairing helps or hurts the choices: that is #151's A/B on the
evaluation set, same model, same session. Our set declares no categories, so on it pairing changes
only the target lists of the actions that need a target (no `no_target`) and of `none`
(`no_target` alone).

**What would change this.** A paired reply that pairs an action with a target it does not take, on
any Ollama version; or an Ollama release that rejects a root `anyOf` in `format`. Re-run the probe
after an upgrade.

### `Agent.Reset` aborts a request Ollama is already working on, and the next decision is normal (9 November 2026)

**What was checked.** #190 G12's `Agent.Reset()` (merged as #219) against a real server, because
its unit tests use a fake provider only. Three things: the call in flight ends cancelled, Ollama
stops the request and frees its slot, and the next `DecideAsync` on the same agent returns a normal
decision. A live check, not an accuracy or latency claim, so it needs no control arm.

**Conditions.** Ollama 0.40.14 on the lead's laptop (RTX 4060 Laptop, 8 GB), on AC power,
`qwen3.5:2b` alone and fully in VRAM (`size_vram` 2254 of 2254 MB), the shared Ollama lock held, no
other Unity running. `origin/test` at 61a7744. A throwaway PlayMode test on `LiveGuard`, never
committed: warm-up, three ordinary decisions (656 ms mean), then three times over: start a
`DecideAsync`, call `Reset()` 263 ms later, await the call, then decide again on the same agent.

**Result: 3 of 3 on every point.**

| Repeat | The call | Ollama (server log) | Generated before the stop | The next decision |
|---|---|---|---:|---|
| 1 | `OperationCanceledException` 7 ms after `Reset()` | `[GIN] 500, 265 ms`, then `srv stop: cancel task` | 16 tokens | `move_to`/`tower`, 770 ms |
| 2 | the same, 1 ms after | `500, 262 ms`, `cancel task` | 15 tokens | `follow_player`, 869 ms |
| 3 | the same, 1 ms after | `500, 262 ms`, `cancel task` | 15 tokens | `none`, 875 ms |

- **The request was being generated when it was stopped**, not still queued: llama-server had
  written 15 or 16 tokens of the reply, and released the slot at once.
- **The slot was free straight away.** The next decision waited 0 ms in the queue, with nothing
  ahead of it, and took as long as an ordinary one, so nothing was left running.
- **Its memory was cleared too.** The cancelled request's prompt was 374 tokens, carrying the three
  earlier exchanges; the next one was 214.
- The message was "Agent 'Village Guard' was reset, which cancelled this decision."

**Worth knowing.** Ollama logs a chat its client dropped as HTTP 500, not 499. A request log that
counts 500s as server errors will count every reset as one.

**What would change this.** An Ollama release that keeps generating after the client drops the
connection, which would show as the next decision waiting in the queue. Raw output:
`.scratch/live-runs-1109/` in the session's checkout (`run1-*`, `ResetLiveCheck.cs`).

### `OLLAMA_GO_TEMPLATE=1` keeps the field order, for `Qwen3-4B-Instruct-2507` and `MiniCPM5-2B` (9 November 2026)

A live check of the fix the entry of 5 November suggested ("Why `qwen3:4b-instruct-2507` wrote
`statement` before `target`"). Not an accuracy claim.

**Setup.** The framework's own request, from the 5 November proxy capture (system prompt, schema
with `action`, `target`, `statement`, options, `logprobs`), with six stimuli per model. The order
the schema gives is not alphabetical. Ollama 0.40.14, RTX 4060 Laptop on AC power, one model
resident at a time, each unloaded after its six (`keep_alive: 0`): `qwen3:4b-instruct-2507-q4_K_M`
3032 of 3032 MB in VRAM, `MiniCPM5-2B` (`hf.co/openbmb/MiniCPM5-2B-GGUF:Q4_K_M`) 1564 of 1564 MB.
First on the Ollama app as it normally runs, then on `ollama serve` started by hand with
`OLLAMA_GO_TEMPLATE=1` and the two variables the app adds (`OLLAMA_CONTEXT_LENGTH=16384`,
`OLLAMA_NO_CLOUD=0`), then the app restored, with the same server config as before.

| Model | As the app runs (variable unset) | `OLLAMA_GO_TEMPLATE=1` |
|---|---|---|
| `qwen3:4b-instruct-2507-q4_K_M` | `selected=gguf_chat_template`, 0 of 6 in order | `selected=go_template`, 6 of 6 in order |
| `MiniCPM5-2B` | `selected=gguf_chat_template`, 0 of 6 in order | `selected=go_template`, 6 of 6 in order |

Every reply out of order was `action`, `statement`, `target`, the sorted order.

- **Only a variable that is set counts.** Every server log since 3 November prints
  `OLLAMA_GO_TEMPLATE:true`, the 5 November run included. In 0.40.14's source that is only the
  default printed when the variable is unset. `goTemplateEnvSet()` (`server/images.go`) skips the
  rule that prefers the GGUF template only when the variable is set.
- **`MiniCPM5-2B` confirms the 5 November prediction** that it would come back sorted. On the sorted
  path it twice wrote just the target's name as the statement (`"statement": "bridge"`) before the
  target. With the order kept, it wrote a line ("I'll be at the bridge.").
- **The variable changes two things, not one.** The Go template also formats the prompt in place
  of the GGUF one, and the 4B's replies changed wording between the two paths. So an A/B with the
  variable set compares another template as well as another order.

**What it means for the 8 GB shortlist.** `MiniCPM5-2B` can be measured in the framework's order
only with `OLLAMA_GO_TEMPLATE=1` set on the server, and its result is then a result on the Go
template. Models on `renderer_parser`, `qwen3.5:2b` among them, never take the GGUF path, set or
not.

**What would change this.** An Ollama release whose native path passes the schema on as sent, or
that changes when the GGUF template is preferred. Raw replies: `.scratch/live-runs-1109/run2-*`
in the session's checkout.

### Listing the actions and targets in the prompt: 58/76 against 56/76 on `qwen3.5:2b`, within noise (9 November 2026)

The first of #193's three switches through the runner (#145), for #151: does
`DecideOptions.ListActionsAndTargets` help a model that never reads the schema?

**Setup.** The evaluation set (`Assets/Evaluation/`, 76 prompts) and its profile `VillageGuard`,
`origin/test` at 61a7744. Two arms, interleaved, on one model:

- **A, control:** the defaults (`ListActionsAndTargets` off).
- **B:** `ListActionsAndTargets = true`, cap 12, so all 11 targets were listed. The actions came with
  their descriptions. The targets came by id only: the runner's `DeclaredTarget` does not implement
  `IDescribedTarget`, and in both arms each target's description already reached the model as an
  observation line.

Everything else was the runner's defaults (grounding guard on, temperature 0). State was told with
the 5 November wording ("You are following the player."). `qwen3.5:2b` on Ollama 0.40.14, RTX 4060
Laptop on AC power, 2254 of 2254 MB in VRAM before and after each arm's warm-up and at its end,
nothing else resident, no other Unity running, the Ollama lock held. 130 s for both arms, after a
12-prompt slice first (31 s). No warnings and no pipeline errors.

| Outcome | A, list off | B, list on |
|---|---:|---:|
| Correct | 56 | 58 |
| WrongLegalAction | 16 | 16 |
| ContainedByGuard | 1 | 0 |
| RejectedWhenActionExpected | 3 | 2 |
| **Accuracy** | **73.7%** | **76.3%** |

**+2.6 points, within noise** (floor 8.1 at n = 76). No category was beyond its floor:
command_with_target +9.1 (n 11), conversation +11.1 (9), event +5.3 (19), impossible_request −11.1
(9), simple_command +11.1 (9), situation −5.3 (19). Arm A matched the `qwen3.5:2b` arm of
5 November, 56 of 76, on the same Ollama version.

**The answers that moved.** 13 prompts changed answer: 6 became correct, 4 stopped being correct.

- Became correct with the list: "Go back to the shelter." (A `none`), "Stay where you are." (A
  `none`), "A scout spotted movement near the north tower." (A attacked the wolf, refused by the
  guard), "The bridge has collapsed." (A walked to the gate) and "You look tired." (A walked to the
  campfire).
- Stopped being correct: "Enter the locked cave without opening it." came back `none` with the
  target `locked_cave`, which the flat schema allows. "The player has reached the shelter." became
  `none`, said as "Fine day for it.", the idle example's line. "Villagers are gathering in the square
  tonight." became `none`. The `move_to`/`north_tower` that the guard stopped in A for "The
  watchtower is unattended." went through in B.
- Not moved at all: "Wait here.", "Do not move." and "Hold position." are `none` in both arms, as
  are "The player is heading off on their own.", "The player has gone inside to sleep." and "The
  square has gone quiet.". The list does not get the 2B to `stop_following` or `follow_player` on
  these.

**A side count, not scored.** Statements that were an example line word for word: 3 in A ("Strike the
Wolf." twice, "You can stay put now." once), 5 in B ("Strike the Wolf." three times, "Fine day for
it." twice). This is the echo G14 is about, and too few to say anything.

**Rule 4.** No example matches a prompt exactly; the runner checks that. One partial overlap holds
in both arms: `move_to`'s example "Go and stand by the {0}." and target_005 "Go stand at the north
tower." (noted on 5 November). Both arms answered target_005 correctly.

**What it means.** Nothing to change: listing the actions and targets is no measurable help to
`qwen3.5:2b` on this set, and it stays off. A larger set, or a model that reads the list better,
would need its own run; the 10 November entry "The plain action list over the defaults" ran it on the interim set: +8.2 on the 2B, -1.5 on the 4B.

**What would change this.** A repeat that moves outside the noise floor, on more prompts (#147) or
on another model. Raw report: `.scratch/live-runs-1109/run3a-report/` in the session's checkout.

### Counter-examples on the evaluation set: 56/76 with them and without, on `qwen3.5:2b` (9 November 2026)

The second of #193's switches through the runner, for #151: does an action's counter-example, a
line about its subject answered with `none`, cut actions nobody asked for?

**Setup.** `VillageGuard` has no counter-examples, so with it as it is the switch changes nothing.
With the lead's approval, one was written for each of its four actions, on in-memory copies for this
run only (the asset is unchanged). Both arms used that profile:

- **A, control:** `DecideOptions.CounterExamples = false`.
- **B:** the defaults, which show the counter-examples.

The lines. Each was checked word by word against all 76 stimuli and their observations. None
matches one, and the only content words they share are `well` and `wolf`, the targets the actions'
own examples already show:

| Action | Counter-example (`{0}` is filled as in the action's example) |
|---|---|
| `follow_player` | "My brother likes to wander behind the carts." |
| `stop_following` | "The old miller never budges from his chair." |
| `move_to` | "The {0} was built by my grandfather." (shown as "The Well …") |
| `attack_target` | "The {0} howled at sunset yesterday." (shown as "The Wolf …") |

Everything else as in the entry above: the evaluation set at 61a7744, interleaved, runner defaults,
the 5 November state wording, `qwen3.5:2b` on Ollama 0.40.14, 2254 of 2254 MB in VRAM before and
after each warm-up and at each arm's end, on AC power, nothing else resident, the lock held. 139 s.
No warnings and no pipeline errors. This run and the next one ran on `ollama serve`, started by hand
with the app's server configuration, so the app's updater could not install 0.40.15 mid-batch. The
control arm gave the same 76 answers as the entry above's control did on the app.

| Outcome | A, counter-examples off | B, on |
|---|---:|---:|
| Correct | 56 | 56 |
| WrongLegalAction | 16 | 15 |
| ContainedByGuard | 1 | 2 |
| RejectedWhenActionExpected | 3 | 3 |
| **Accuracy** | **73.7%** | **73.7%** |

**+0.0 points, within noise** (floor 8.1). Per category, B − A: command_with_target −9.1 (n 11),
conversation +11.1 (9), event +5.3 (19), situation −5.3 (19), impossible_request and
simple_command 0. All within noise.

**Unasked actions.** On the 30 prompts labelled `none`, the model chose an action, before any
guard, 4 times without the counter-examples and 3 times with them. On the 46 prompts that want an
action, it answered `none` 10 times without and 11 with. Living Vale's 6/13 → 0/13 (5 November) was
one idle-style line in its own game, not a counter-example per action, and does not carry over here.

**The four answers that moved.** Became correct: "You look tired." (A walked to the campfire) and
"The bridge has collapsed." (A walked to the gate), both `none` in B. Stopped being correct: "The
path ahead is empty." became `move_to`/`bridge`, which the guard contained, and "Approach the
eastern road." became `none` ("I will not follow you into the unknown.").

**What it means.** Counter-examples, as written here, make no measurable difference to
`qwen3.5:2b` on this set. They stay empty by default, which is what they are. A set with more
prompts that tempt an unasked action would test them more sharply than 30 `none` prompts do.

**What would change this.** A run outside the noise floor, with other lines, on more prompts or on
another model. Raw report: `.scratch/live-runs-1109/run3b-report/` in the session's checkout.

### The idle example for a code-built agent: 59/76 unset against 56/76 set, and the line costs `stop_following` (9 November 2026)

The third of #193's switches through the runner, for #151: does `Agent.IdleExampleStimulus` help an
agent built in code, which has no idle example unless one is set?

**Setup.** The runner always builds its agents from a profile, so a profile agent (`VillageGuard`)
stood in for a code-built one, and the idle example was set per call:

- **A:** `IdleExampleStimulus = ""`, no idle example, what a code-built agent sends today.
- **B:** `IdleExampleStimulus = AgentProfile.DefaultIdleExampleStimulus`, "Nothing has changed
  since last time.", the line a new profile starts with.

A preview of both prompts showed they differ by that one example line and nothing else. Everything
else as in the two entries above, on the same `ollama serve`: 0.40.14, 2254 of 2254 MB in VRAM
throughout, interleaved, 138 s, no warnings and no pipeline errors.

**Rule 4.** The default line partly repeats situation_001, "Nothing has happened for a while.",
which is labelled `none` too. So the result is given with and without that prompt. Both arms
answered it `none`.

| Outcome | A, no idle example | B, the default idle example |
|---|---:|---:|
| Correct | 59 | 56 |
| WrongLegalAction | 12 | 15 |
| ContainedByGuard | 2 | 1 |
| RejectedWhenActionExpected | 3 | 4 |
| **Accuracy** | **77.6%** | **73.7%** |
| Without situation_001 | 58/75, 77.3% | 55/75, 73.3% |

**Overall −3.9 points, within noise** (floor 8.1; without situation_001, −4.0, floor 8.2).
**simple_command −33.3, beyond noise** (88.9% → 55.6%, n = 9, floor 23.6). The other categories
were within noise: situation −10.5 (−11.1 without situation_001), conversation +11.1,
impossible_request +11.1, the rest 0.

**What the idle line did.** With it, the 2B answered `none` to commands it had obeyed without it:
"Wait here.", "Stay where you are.", "Do not move." and "The player has gone inside to sleep.", all
`stop_following`, and the night-watch situation, a `move_to`. Its statements still said it would
stay ("I will stand guard here until further notice."). It also fixed two `none` prompts: "You look
tired." (A walked to the campfire) and "Attack Godzilla." (A attacked the wolf, contained by the
guard). On the 30 `none` prompts it acted 5 times without the line and 3 times with it. On the 46
that want an action, it answered `none` 6 times without and 10 times with.

**It was also repeated.** With the line, 10 of the 76 statements were "Nothing has changed since
last time." word for word, against none without it. That is the echo #190 G14 is about.

**An observation across runs, not a result.** The control arms of the two entries above had the
profile's own idle example, "Fine day for it.", and scored 56/76 with simple_command at 55.6%, the
same as B here. At temperature 0 this machine gave identical answers on repeat (both controls
matched answer for answer). So the cost to `stop_following` may come from any idle `none` example,
whatever its text. The next entry runs #151's "idle example with vs without" A/B on the profile's
own line, and it does.

**What it means.** Nothing to switch on. The one difference beyond the noise floor is in favour of
the default that already holds, a code-built agent with no idle example. It is one category of 9
prompts, so it is worth repeating on #147's larger set before anyone removes the profile's idle
example.

**What would change this.** A repeat on more `stop_following` prompts, or on another model, where
the idle line no longer costs them. Raw report: `.scratch/live-runs-1109/run3c-report/` in the
session's checkout.

### The idle example on the profile's own line: 56/76 with it against 59/76 without, and `stop_following` 2/7 against 6/7 (9 November 2026)

#151's "idle example with vs without", run the same evening because the entry above suggested the
cost to `stop_following` came from any idle `none` example, whatever its text.

**Setup.** `VillageGuard` as it is, so the idle example is its own line, "Fine day for it.". Two arms,
interleaved, on one model:

- **A, control:** the defaults, which show the profile's line.
- **B:** `IdleExampleStimulus = ""`, which leaves it out.

A preview showed the two prompts differ by that one line. Otherwise as in the entries above: the
evaluation set at 61a7744, runner defaults, the 5 November state wording, `qwen3.5:2b` on Ollama
0.40.14 through a hand-started `ollama serve` with the app's server configuration, 2254 of 2254 MB in
VRAM throughout, on AC power, nothing else resident, no other Unity running, the lock held. 123 s,
no warnings and no pipeline errors. No example line matches a prompt; "Fine day for it." shares no
words with any.

| Outcome | A, "Fine day for it." | B, no idle example |
|---|---:|---:|
| Correct | 56 | 59 |
| WrongLegalAction | 16 | 12 |
| ContainedByGuard | 1 | 2 |
| RejectedWhenActionExpected | 3 | 3 |
| **Accuracy** | **73.7%** | **77.6%** |

**Overall +3.9 points without it, within noise** (floor 8.1). **simple_command +33.3, beyond noise**
(55.6% → 88.9%, n = 9, floor 23.6). The other categories: impossible_request −11.1, situation +5.3,
the rest 0, all within noise.

**`stop_following`: 2 of 7 right with the idle line, 6 of 7 without.** The five answers that moved
were all one of two kinds:

- Four commands went from `none` to `stop_following` without the line: "Wait here.", "Stay where you
  are.", "Do not move." and "The player has gone inside to sleep.". With it, the model said it would
  stay ("I will wait for your next command.") but chose `none`. "Hold position." is `none` in both.
- One went the other way: "Attack Godzilla." was `none` with the line and `attack_target`/`wolf`
  without it, which the grounding guard contained.

On the 30 `none` prompts the model acted 4 times with the line and 5 without (Godzilla). On the 46
that want an action, it answered `none` 10 times with the line and 6 without. "Fine day for it." was
never repeated as a statement. Other example lines were, 3 times with the line and once without.

**The same answers as the runs before.** At temperature 0, arm A gave the same 76 answers as the
controls of the `ListActionsAndTargets` and counter-example runs, and arm B the same 76 as the
code-built run's unset arm. So the default line, "Nothing has changed since last time.", and the
profile's line cost `stop_following` the same four prompts. The entry above's observation across
runs is now this A/B.

**What it means.** On `qwen3.5:2b`, an idle `none` example makes "stay here" commands come back as
`none`, by a margin beyond the noise floor in that category, while the whole set moves within noise.
It is the first result of the evening that points at a default, since every profile starts with an
idle example (`AgentProfile.DefaultIdleExampleStimulus`). Changing that is the lead's decision, in its
own pull request. The category has 9 prompts, so repeat it on #147's larger set, and on another
model, before deciding. Nothing here changes code. The sweep in the next entry ran this A/B on 13
models: only `qwen3.5:2b` pays this cost.

**What would change this.** A repeat on more `stop_following` prompts, or on another model, where the
idle line no longer turns them into `none`. Raw report: `.scratch/live-runs-1109/run3d-report/` in
the session's checkout.

### Thirteen models on the evaluation set: `qwen3.5:4b` first at 70/76, and the idle example hurts only `qwen3.5:2b` (9 November 2026)

A sweep of every model pulled for [`model-candidates.md`](model-candidates.md), on the lead's
request: each model's default score on the evaluation set, and the idle-example A/B of the two
entries above, run again on each. It covers #151's "`qwen3.5:2b` vs `qwen3.5:4b`" row and
`live-runs.md`'s "8 GB shortlist", with the limits below.

**Setup.**
- **The set:** the evaluation set at 61a7744, all 76 prompts, and `VillageGuard`. It is the only
  set there is. #147's 200+ prompts do not exist yet, so every per-category figure below rests on
  9 to 19 prompts.
- **The server:** one `ollama serve` for the whole sweep, Ollama 0.40.14, checked before each model.
  It ran with `OLLAMA_GO_TEMPLATE=1` and the app's two variables (`OLLAMA_CONTEXT_LENGTH=16384`,
  `OLLAMA_NO_CLOUD=0`), so no model took the key-sorting path (entry of 9 November above).
- **The machine:** the RTX 4060 Laptop, 8 GB, on AC power, checked before each model. No other Unity
  was running, the Ollama lock was held, and one model was resident at a time, each unloaded with
  `keep_alive: 0` before the next.
- **Before each model's runs:** six raw `/api/chat` requests in the framework's own shape
  (`think: false`, temperature 0, `num_ctx` 4096, `logprobs`). They checked the field order, any
  thinking, and `size` and `size_vram`.
- **Then two arms through the runner, interleaved:**
  - **On:** the defaults, with the profile's idle example "Fine day for it.". This is also the
    model's default score.
  - **Off:** `IdleExampleStimulus = ""`.
  - Both used the runner's defaults (grounding guard on, temperature 0) and the 5 November state
    wording.

**What the comparisons are.** The idle on/off pair is an interleaved A/B on one model. The ranking
between models compares each model's "on" arm with `qwen3.5:2b`'s, which ran first on the same server
that session. **These are separate runs on one server, not the runner's `Sequential` pairs.** The
same noise floor is applied to them (8.1 points at n = 76), but drift between runs is not shared.
At temperature 0 this machine repeated a run answer for answer all evening, and `qwen3.5:2b` gave the
same 76 answers here as in the two entries above.

**Every model passed the pre-checks.**
- 6 of 6 replies in the framework's field order on all 13.
- No thinking came back from any. With `think: false`, which `OllamaProvider` always sends, the
  replies had no `thinking` field and no `<think>` text, and used 24 to 40 tokens each, `qwen3:8b`
  included.
- Every model sat fully in VRAM (`size_vram` equal to `size`), the two 8B models included, so all
  the latencies are comparable.
- No warnings and no pipeline errors in any run. The whole sweep took 33 minutes.

**Default score, ranked**, each against `qwen3.5:2b`'s 56/76 (floor 8.1):

| Model | Chat path | VRAM, MB | Default (idle on) | Against the 2B | Median / p90 latency, s |
|---|---|---:|---:|---|---|
| `qwen3.5:4b` | renderer_parser | 2993 | **70** (92.1%) | +18.4, beyond noise | 1.00 / 1.09 |
| `qwen3:4b-instruct-2507-q4_K_M` | go_template | 3032 | 65 (85.5%) | +11.8, beyond noise | 0.62 / 0.67 |
| `ministral-3:3b` | parser | 2601 | 65 (85.5%) | +11.8, beyond noise | 0.53 / 0.59 |
| `qwen3:8b` | go_template | 5327 | 65 (85.5%) | +11.8, beyond noise | 0.96 / 1.05 |
| `gemma4:e2b-it-qat` | renderer_parser | 1578 | 64 (84.2%) | +10.5, beyond noise | 0.70 / 0.84 |
| `granite4.1:3b` | go_template | 2390 | 62 (81.6%) | +7.9, within noise | 0.46 / 0.52 |
| `nemotron-3-nano:4b` | renderer_parser | 2693 | 60 (78.9%) | +5.3, within noise | 0.68 / 0.77 |
| `L3-8B-Stheno-v3.2` Q4_K_M | go_template | 5035 | 60 (78.9%) | +5.3, within noise | 0.85 / 0.93 |
| `llama3.2:3b` | go_template | 2442 | 59 (77.6%) | +3.9, within noise | 0.46 / 0.55 |
| `MiniCPM5-2B` Q4_K_M | go_template | 1564 | 58 (76.3%) | +2.6, within noise | 0.36 / 0.42 |
| `phi4-mini` | go_template | 2956 | 57 (75.0%) | +1.3, within noise | 0.68 / 0.82 |
| **`qwen3.5:2b`** (the baseline) | renderer_parser | 2254 | 56 (73.7%) | | 0.68 / 0.77 |
| `lfm3:1.7b` | renderer_parser | 1114 | 46 (60.5%) | −13.2, beyond noise | 0.38 / 0.46 |

Latency is the runner's per-decision time on the "on" arm, warm, one model resident.
`gemma4:e2b-it-qat`'s VRAM is what `/api/ps` reported; its download is 4135 MB.

**Most of the gap is simple commands.**
- Every model but `phi4-mini`, `lfm3:1.7b` and the baseline scored 9/9 on simple_command, against
  the 2B's 5/9: +44.4, beyond noise.
- Only `qwen3.5:4b` was also beyond noise on situations (17/19 against 13/19) and events (15/19
  against 11/19).
- Two models fell beyond noise on situations: `MiniCPM5-2B` (7/19) and `L3-8B-Stheno` (8/19).
  `lfm3:1.7b` fell beyond noise on events (5/19) and situations (8/19).

**The idle example on and off**, per model (off − on, floor 8.1; with the line / without):

| Model | On | Off | Off − on | simple_command | `stop_following` | Unasked actions (of 30) | Missed requests (of 46) | Echoes |
|---|---:|---:|---|---|---|---|---|---|
| `qwen3.5:2b` | 56 | 59 | +3.9, within | 55.6 / **88.9, beyond** | **2/7 / 6/7** | 4 / 5 | 10 / 6 | 3 / 1 |
| `qwen3.5:4b` | 70 | 66 | −5.3, within | 100 / 100 | 6/7 / 6/7 | 1 / 7 | 2 / 0 | 0 / 0 |
| `qwen3:4b-instruct-2507` | 65 | 56 | **−11.8, beyond** | 100 / 100 | 6/7 / 6/7 | 5 / 15 | 1 / 0 | 0 / 0 |
| `ministral-3:3b` | 65 | 61 | −5.3, within | 100 / 100 | 7/7 / 7/7 | 4 / 7 | 0 / 0 | 0 / 0 |
| `qwen3:8b` | 65 | 61 | −5.3, within | 100 / 100 | 7/7 / 7/7 | 4 / 7 | 1 / 1 | 0 / 0 |
| `gemma4:e2b-it-qat` | 64 | 62 | −2.6, within | 100 / 100 | 5/7 / 7/7 | 3 / 6 | 5 / 2 | 0 / 0 |
| `granite4.1:3b` | 62 | 61 | −1.3, within | 100 / 100 | 6/7 / 7/7 | 1 / 2 | 11 / 10 | 0 / 0 |
| `nemotron-3-nano:4b` | 60 | 63 | +3.9, within | 100 / 100 | 7/7 / 7/7 | 2 / 5 | 8 / 1 | 1 / 0 |
| `L3-8B-Stheno-v3.2` | 60 | 61 | +1.3, within | 100 / 100 | 7/7 / 7/7 | 11 / 11 | 0 / 0 | 0 / 0 |
| `llama3.2:3b` | 59 | 51 | **−10.5, beyond** | 100 / 100 | 7/7 / 7/7 | 10 / 19 | 0 / 0 | 0 / 0 |
| `MiniCPM5-2B` | 58 | 56 | −2.6, within | 100 / 88.9 | 6/7 / 7/7 | 6 / 9 | 7 / 5 | 0 / 0 |
| `phi4-mini` | 57 | 56 | −1.3, within | 77.8 / 100 | 5/7 / 7/7 | 9 / 14 | 3 / 0 | 1 / 1 |
| `lfm3:1.7b` | 46 | 47 | +1.3, within | 77.8 / 77.8 | 3/7 / 3/7 | 5 / 3 | 2 / 6 | 0 / 0 |

"Unasked actions" counts an action chosen on a prompt labelled `none`, before any guard. "Missed
requests" counts `none` answered to a prompt that wants an action. "Echoes" are statements that
repeat an example line word for word.

**What the idle example does.** It is the line the framework uses to cut unasked actions, and on
11 of the 13 models taking it away added unasked actions: by 10 on `qwen3:4b-instruct-2507` and 9
on `llama3.2:3b`, both beyond noise overall. The cost to `stop_following` that the entries above
found is `qwen3.5:2b`'s alone: it is the only model whose simple commands fall beyond noise with
the line. On every other model the line costs nothing there, or a prompt or two. So the
previous entry's pointer at a default does not hold across models. What it does show is that the 2B
reads the idle line as a reason to answer `none`.

**Limits.**
- **The model ranking is not a set of A/B results of the runner's kind.** It compares separate
  runs on one server, at n = 76, on one set written for one agent.
- **The chat path differs between models**, and it is part of the comparison. Six models ran on
  Ollama's own renderer or parser. Seven ran on their Go template, because of `OLLAMA_GO_TEMPLATE=1`;
  two of those, `qwen3:4b-instruct-2507` and `MiniCPM5-2B`, would take the key-sorting GGUF path
  without it. The variable changes how the prompt is formatted (entry of 9 November above), so these
  scores hold for a server set up this way.
- `qwen3:4b-instruct-2507` scored 65/76 on 5 November too, with its keys sorted, and 65/76 here in
  order. That compares two sessions and two templates, so it is an observation only.
- One grounding threshold, 0.8, was used for every model. It was set on Qwen's tokenizer
  (`model-candidates.md`), and no model was calibrated.

**What it means.** On this set, five models beat `qwen3.5:2b` by more than the noise floor at its
own defaults, and `qwen3.5:4b` by the most, at about 1.5 times the latency. `ministral-3:3b` and
`qwen3:4b-instruct-2507` tie at 65/76 for less time than the 2B. Choosing a default model is the
lead's decision, and nothing in code changes here. A repeat on #147's larger set, with the arms as
the runner's `Sequential` pairs, is what would make a ranking a result.

**What would change this.** A repeat on more prompts, or with `Sequential` pairs, that reorders
the top five or closes their gap to the 2B. Another Ollama version, or a server without
`OLLAMA_GO_TEMPLATE=1`, changes the template seven models are served with. Raw data: `results.json`,
with every answer per model, in `.scratch/model-sweep-1109/` of the main checkout; reports and
logs in the session's checkout under the same folder name.

### The interim set on the laptop: `qwen3.5:2b` 265/389 with the idle example, 279/389 without, and the sweep moved to the RTX 5080 (9 November 2026)

The 13-model sweep of the entry above, started again on the interim set and stopped after its first
model. The lead asked for the rest to run on the RTX 5080. That run is
[run request 001](../run-requests/001-interim-model-sweep-5080/request.md). This entry records the
one model that finished on the laptop.

**The set.** The interim set: 389 prompts in `Assets/Evaluation/Interim/` (#223), measured against
the curated set's `VillageGuard`. **It is AI-written.** Claude wrote it and gave the first labels,
then Codex labelled it blind, and the two agreed on 391 of 392 (its README). So its prompts may be
less ambiguous than the curated set's 76, which the team wrote. **Its scores and
the curated set's are not directly comparable**, though both are given below. Its noise floor is
3.6 points overall and about 9 per category, against 8.1 and 20 to 30 at n = 76.

**Setup.** As in the entry above, apart from three things:

- `test` was at db16c03, which adds #224's reply checks to `EvalRunner`. That is the only framework
  change since the curated sweep, and its tests show accuracy unchanged.
- The probe was 2 requests, since the field order and thinking were established that evening.
- The throwaway test needed `[Timeout]`: the Test Framework stops a test at 180 s by default. The
  first attempt failed there and recorded nothing.

The run itself: one `ollama serve` 0.40.14 with `OLLAMA_GO_TEMPLATE=1` and the app's two
variables, RTX 4060 Laptop on AC power, the lock held, no other Unity. `qwen3.5:2b` was 2254 of
2254 MB in VRAM throughout. The idle on/off arms ran interleaved, 524 s of asking (9 min end to
end), with no warnings and no pipeline errors.

| | Idle example on (defaults) | Off | Off − on |
|---|---:|---:|---|
| **Correct** | **265/389 (68.1%)** | **279/389 (71.7%)** | +3.6, **beyond noise** (floor 3.59, on the line) |
| simple_command | 46/65 | 57/65 | +16.9, beyond noise |
| impossible_request | 41/64 | 47/64 | +9.4, beyond noise |
| command_with_target | 63/66 | 60/66 | −4.5, within |
| conversation | 44/64 | 44/64 | 0 |
| situation | 32/65 | 31/65 | −1.5, within |
| event | 39/65 | 40/65 | +1.5, within |
| `stop_following` | 21/48 | 39/48 | |
| Unasked actions, of 156 `none` prompts | 46 | 51 | |
| Missed requests, of 233 that want an action | 37 | 22 | |
| Reply checks flagged (#224), of 389 | 14, all echoes | 6, all echoes | |
| Median / p90 latency | 0.66 / 0.81 s | 0.65 / 0.80 s | |

The curated set gave the same model 56/76 (73.7%) with the line and 59/76 (77.6%) without, on the
same server that evening.

- **The idle line's cost to `stop_following` holds at five times the prompts**: 21/48 right with it,
  39/48 without. The curated set had 2/7 and 6/7.
- **On this set, the line also costs impossible requests** (+9.4 without it, beyond noise). Taking the line
  away added 5 unasked actions (46 to 51) and removed 15 missed requests (37 to 22).
- **Every reply flagged was an echo**, never an empty, too-long or leaking one. With the line, the
  model repeated "Strike the Wolf." 8 times, "You can stay put now." 5 times and "Fine day for it."
  once. Without it, 6 echoes, of the same two action examples.
- On the curated set, only `qwen3.5:2b` paid this cost (entry above). Whether the other models do on
  a larger set is what request 001 measures.

**Not a ranking**, since only one model finished here. `qwen3.5:4b` was stopped about ten minutes
into its run, at the lead's request, and recorded nothing.

**What would change this.** Request 001's run of the same model on the RTX 5080, as its baseline. A
different result there points at the machine, not the model. Raw data:
`docs/run-requests/001-interim-model-sweep-5080/laptop-interim-results.json`, and the full report
in the laptop session's checkout, `.scratch/model-sweep-interim/`.

### The RTX 5080's Docker Ollama: a 54 s first load after a pull, 1.9 s after that (9 November 2026)

**What.** The Docker server in `tools/ollama-server/` (Ollama 0.40.14, Linux build under WSL2,
RTX 5080 16 GB, driver 621.14), asked through its gateway from the same machine. `qwen3.5:2b`
straight after `ollama pull`: the first `/api/chat` took 54.3 s, the second 0.31 s. After an
unload, two cold loads by `/api/generate` with `num_ctx` 4096 took 1.9 s each. After both containers
were recreated (same volume, same machine, no reboot), the first load took 28.5 s by Ollama's log
and 29.2 s at the client. Resident at
2254 MB, all of it on the GPU. One run of each, no A/B: a setup note, not a latency result.

**Why it matters.** 54 s is past `OllamaSettings.TimeoutSeconds`' default of 30 s, so the first
decision after a pull or a restart can fail as `Timeout`. The first load is most likely Docker
reading the model from its volume for the first time; that was not separated from the CUDA
start-up, which the 1.9 s loads also include. Hence `ollama_remote.py warm <model>` before a run.

**What else differs from the Ollama app.** Its log shows `OLLAMA_VULKAN:true` and
`OLLAMA_FLASH_ATTENTION:false` as the image's defaults, and CUDA 13 libraries. Whether any of
it moves accuracy or latency against the app on the same card is unmeasured, which is why the two
are kept apart as separate machines (`environment.md`).

**What would change this.** Timing a load with the file already in the page cache against one
without, after a server restart.

### Policy lines as knowledge or as observations: 335/389 against 323/389, and neither helps situations (9 November 2026)

#190's Living Vale #14 added `AgentIdentity.Knowledge`, lines in the system prompt after the goals.
The design notes owed one A/B: a few policy lines given as knowledge, against the same lines given
as observations, which is how Living Vale sends its facts today. The question behind it: models
score far lower on situations and events than on player lines, because the right answer there is
the designer's policy ("a guard on watch duty goes to an empty tower"). Does stating a policy help,
and does it matter where it goes?

**The lines.** Five, written for this run by the agent that built the feature, who could see the
set's six category names (`simple_command` … `event`) and its targets, but not its prompts or
labels. They are a designer's guess, not tuned to the set, and the entry point refused any that
overlapped a prompt's stimulus (hard rule 4; none did):

1. When trouble is reported at a place you can reach, go there.
2. When a wolf or an armed stranger threatens someone nearby, attack it.
3. When people need to take cover, go to the shelter.
4. Small talk, questions and quiet moments need no action.
5. If you are asked for something you cannot do, or a place that is not here, take no action and say so.

None of them says when to follow the player or stop.

**Setup.** The interim set (389 prompts, `Assets/Evaluation/Interim/`, set commit a24dde9) with
`Assets/Evaluation/VillageGuard.asset`, `qwen3.5:4b`, the RTX 5080's Docker Ollama 0.40.14 over the
network (home), 2995 of 2995 MB in VRAM after each warm-up, nothing else resident, the laptop on
mains power, the 5080 lock held. Interleaved, runner defaults (guards on, the profile's idle
example), the 9 November state wording. Two A/B runs back to back, each 6 minutes, after a 24-prompt
slice (23 against 24, within noise):

- **Run 1.** A: the lines as `DecideOptions.Observations`, first in the observation block. B: the
  same lines as `EvalArm.Knowledge`, so under "What you know:" in the system prompt.
- **Run 2.** A: no lines at all, today's prompt. B: the lines as knowledge again.

| | A: as observations | B: as knowledge | A: no lines | B: as knowledge |
|---|---:|---:|---:|---:|
| | run 1 | run 1 | run 2 | run 2 |
| **All, 389** | 323 (83.0%) | 335 (86.1%) | 330 (84.8%) | 335 (86.1%) |
| Player lines, 259 | 235 (90.7%) | 244 (94.2%) | 239 (92.3%) | 244 (94.2%) |
| Situations and events, 130 | 88 (67.7%) | 91 (70.0%) | 91 (70.0%) | 91 (70.0%) |
| simple_command, 65 | 89.2% | 98.5% | 98.5% | 98.5% |
| command_with_target, 66 | 98.5% | 98.5% | 100.0% | 98.5% |
| impossible_request, 64 | 93.8% | 92.2% | 76.6% | 92.2% |
| conversation, 64 | 81.3% | 87.5% | 93.8% | 87.5% |
| situation, 65 | 60.0% | 58.5% | 61.5% | 58.5% |
| event, 65 | 75.4% | 81.5% | 78.5% | 81.5% |

"Player lines" are the four categories a player says; floors are 3.6 points at n = 389, 4.4 at 259,
6.2 at 130 and 8.8 at 65.

**Run 1, knowledge against observations: +3.1 overall, within noise** (floor 3.6). Beyond noise
only in simple_command, +9.2: as observations, 6 of the 65 commands to follow ("Come with me" and
the like) became `none`, as knowledge none did. The most likely reading is line 4, "Small talk …
need no action", sitting in the observation block right before the stimulus; that was not isolated.
Situations and events +2.3, within noise.

**Run 2, knowledge against no lines: +1.3 overall, within noise.** Beyond noise only in
impossible_request, +15.6, which is line 5's subject: without the lines, 15 of the 64 requests for a
place that is not here or a thing the guard cannot do went wrong (11 acted on, wrong-but-legal, and
4 contained by the guard), against 5 with them (4 and 1). Conversation −6.3, within noise. Situations and events: 91 and 91.

**The arms are deterministic.** The knowledge arm gave the same 389 answers, statements included,
in both runs, and the no-lines arm's 330 is what Request 001 measured for this model on this server
earlier the same day. So repeating an identical prompt adds no noise here; the floor above stands
for how far an unrelated change to the prompt moves the score, which this server has not measured.

**What it means.**

- **Overall, the point estimates favour knowledge, but within noise.** The same five lines scored
  7 answers below no lines as observations (323 against 330, across two runs, so read it with care)
  and 5 above as knowledge. No accuracy change is claimed: neither overall difference passes the
  floor. The one category beyond noise against observations, simple_command, is consistent with
  the observation block's position before the stimulus, not shown by it.
- **The lines helped impossible requests.** The five lines together moved impossible_request
  beyond noise; line 5 names that case and is the likely cause, but no run isolated it.
- **These lines do not close the gap on situations and events.** It stays at about 70% against
  92–94% on player lines in every arm. Most misses there are `follow_player`, `stop_following` and
  `attack_target` answered with `move_to` or `none`, cases no line covered, or covered too loosely
  ("someone nearby"). Whether lines written per action would, is the next run, and it needs lines
  written by someone who has not seen the labels. The 10 November entry "Situations and events: the deciding fact sat in the observations" sorts the 4B's misses by cause and tries moving that fact into the stimulus.
- Nothing becomes a default: both fields stay empty unless a developer writes them.

**What would change this.** Another model, other lines, or a situation set labelled against a
written policy. Raw reports: `.scratch/lv14/report-full/` and `report-full-none/` in the session's
checkout (`.claude/worktrees/silly-panini-9d970f`); the entry point is `.scratch/lv14/KnowledgeAbRun.cs.txt` there.

### Few-shot examples as chat turns: better for `qwen3.5:2b` at the noise floor, worse beyond it for `qwen3.5:4b` and `gemma4:e2b-it-qat`, and every statement goes empty (9 November 2026)

**What.** A switch built for this run and **not adopted**: `DecideOptions.ExamplesAsTurns` (#151)
sent the same few-shot examples, in the same order, as `user`/`assistant` turns, each answer the
JSON the schema asks for, instead of the block of lines in the system prompt. The examples went
after the system message and before the agent's memory; the refusal's "(not an available target)"
note was left out of its JSON answer; only a provider declaring `SendsExampleTurns` (Ollama) got
turns. **The code is parked**, never merged, on the branch `151-chat-turn-examples-parked`: the switch,
its 14 EditMode tests and its docs are commit `6ea8bf0`, and the run kit,
`tools/live-runs/example-turns/` (entry point, `run_one.sh`, README), is on the same branch, since it
needs that code. Check the branch out to re-run either run below.
A review that morning had replayed the curated set's 76 requests against `qwen3.5:2b` outside Unity
with the examples moved into turns and scored 63/76 against 56/76 (one model, one run, no guard: a
lead, not a result). This is the A/B that lead asked for.

**Setup.** The interim set, 389 prompts, against `VillageGuard`, through `EvalRunner` with the
parked branch's kit: arm `block` (the defaults) against arm `turns`
(`ExamplesAsTurns = true`), **interleaved, in one session per model, guards on**, three warm-up
requests each. The framework was the parked branch, off `test` at 17c5184. RTX 5080,
Docker Ollama 0.40.14, over the network from the lead's laptop on AC power, the 5080 lock held.
One model resident at a time, each fully in VRAM throughout and unloaded after its run (nothing
was resident before each). State as one observation line, targets as one observation each, as in
the sweep. A 20-prompt slice of `qwen3.5:2b` ran first and gave 19/20 against 17/20.

| Correct, of 389 | `block` | `turns` | `turns` − `block` |
|---|---:|---:|---|
| **`qwen3.5:2b`** | **263 (67.6%)** | **277 (71.2%)** | +3.6, **beyond noise by a hair** (floor 3.59) |
| **`qwen3.5:4b`** | **330 (84.8%)** | **291 (74.8%)** | −10.0, **beyond noise** |
| **`gemma4:e2b-it-qat`** | **290 (74.6%)** | **264 (67.9%)** | −6.7, **beyond noise** |

The two `block` scores of the Qwen models are request 001's two baselines on the same server to the
prompt (263 and 330), so the server and the set reproduced.

By category (player lines: simple_command, command_with_target, impossible_request, conversation;
policy cases: situation, event). Floors about 9 points per category, 4.4 for the 259 player lines,
6.2 for the 130 policy cases:

| | 2B block | 2B turns | 4B block | 4B turns | Gemma block | Gemma turns |
|---|---:|---:|---:|---:|---:|---:|
| Player lines, of 259 | 195 | 210 | 239 | 220 | 207 | 192 |
| Policy cases, of 130 | 68 | 67 | 91 | 71 | 83 | 72 |
| simple_command, of 65 | 47 | 50 | 64 | 59 | 53 | 41 (−18.5) |
| command_with_target, of 66 | 62 | 62 | 66 | 63 | 63 | 60 |
| impossible_request, of 64 | 41 | 42 | 49 | 44 | 41 | 49 (+12.5) |
| conversation, of 64 | 45 | 56 (+17.2) | 60 | 54 (−9.4) | 50 | 42 (−12.5) |
| situation, of 65 | 29 | 30 | 40 | 35 | 39 | 36 |
| event, of 65 | 39 | 37 | 51 | 36 (−23.1) | 44 | 36 (−12.3) |
| `stop_following`, of 48 | 22 | 28 | 44 | 31 | 29 | 10 |
| Unasked actions, of 156 `none` prompts | 50 | 38 | 23 | 46 | 32 | 16 |
| Missed requests, of 233 that want one | 36 | 43 | 12 | 13 | 35 | 74 |
| Replies flagged (#224) | 13, echoes | 361, **empty** | 0 | 378, **empty** | 0 | 389, **empty** |
| Median latency | 0.361 s | 0.147 s | 0.443 s | 0.210 s | 0.332 s | 0.149 s |
| Median prompt tokens | 366 | 419 | 366 | 419 | 368 | 426 |
| Median completion tokens | 35 | 14 | 37 | 15 | 31 | 16 |

Numbers in brackets are the runner's noise check where it says beyond noise.

- **The 2B's gain is conversation.** 12 of its 49 flips to right are conversation lines: 7 that
  the block arm answered with an action (`attack_target` on the wolf, `move_to`) and the turns arm
  rightly with `none`, and 5 where the turns arm chose the right action, `follow_player` or
  `stop_following`, where the block arm had chosen a wrong one. That is the shape of the morning's replay. It does not carry over to the policy cases,
  which lose wolf events to `none` or a wrong target.
- **The 4B loses everywhere**, events most: it doubles its unasked actions (23 to 46, 26 of its 59
  flips to wrong were `none` prompts answered `move_to`) and loses `stop_following` (44 to 31).
- **Gemma swings towards `none`**: missed requests double (35 to 74) and `stop_following` falls from
  29 to 10, which is why impossible requests rise and every other category falls.
- **Every statement goes empty.** No `VillageGuard` example has a written statement, so every
  example answer shows `"statement":""`, and all three models copy it: 361, 378 and 389 of 389
  replies are flagged `Empty`, against 13 echoes, 0 and 0 with the block. The block shows no
  statement at all, and there each model writes its own line. That is also why the turns arm is
  faster (half the completion tokens). Accuracy here scores only the action and target, so it does
  not count this, but a talking NPC would fall silent.
- **The cost in prompt tokens**: about 53 more per request (366 to 419), the JSON and role overhead
  of five example pairs.

**Second run: both arms with written statements.** The empty copies confound the first run, so the
tech lead asked for the A/B again with a statement on every example, the same in both arms. The
parked kit's `AGENERELA_EXAMPLE_STATEMENTS=1` writes them on in-memory copies of `VillageGuard` and its
four action assets (the asset itself is unchanged): `follow_player` "Right behind you.",
`stop_following` "I'll hold here, then.", `move_to` "Heading to the {0} now.", `attack_target`
"Steel out. The {0} won't trouble us long.", and the idle example "Aye, quiet so far.". None matches
a prompt: the run asserts `PromptSetChecks` finds no more example matches than with the bare profile.
Same server, set, arms and order, guards on, `qwen3.5:4b` then `qwen3.5:2b`, each alone and fully in
VRAM, the same evening (20:18 to 20:28).

| Correct, of 389 | `block` + statements | `turns` + statements | `turns` − `block` |
|---|---:|---:|---|
| **`qwen3.5:4b`** | **332 (85.3%)** | **290 (74.6%)** | −10.8, **beyond noise** |
| **`qwen3.5:2b`** | **274 (70.4%)** | **288 (74.0%)** | +3.6, **beyond noise by a hair** (floor 3.59) |

| | 4B block | 4B turns | 2B block | 2B turns |
|---|---:|---:|---:|---:|
| Player lines, of 259 | 241 | 223 | 201 | 215 |
| Policy cases, of 130 | 91 | 67 | 73 | 73 |
| conversation, of 64 | 59 | 54 | 44 | 59 (+23.4) |
| event, of 65 | 50 | 34 (−24.6) | 42 | 40 |
| Replies flagged `Empty` | 0 | 1 | 0 | 0 |
| Replies flagged `Echo` (a statement repeated word for word) | 255 | 52 | 253 | 85 |
| `stop_following`, of 48 | 44 | 35 | 20 | 30 |
| Unasked actions / missed requests | 20 / 15 | 49 / 12 | 32 / 59 | 34 / 42 |
| Median latency | 0.374 s | 0.272 s | 0.315 s | 0.200 s |
| Median prompt tokens | 410 | 447 | 410 | 447 |

- **The accuracy result does not move.** Written statements leave the 4B 10.8 points worse with
  turns, events worst (−24.6) and unasked actions more than doubled again (20 to 49), and the 2B
  3.6 points better, from conversation. So the first run's split is not the empty statements.
- **Statements stop the empty replies** in the turns arm (378 and 361 empty before, 1 and 0 now),
  **but the block arm then parrots them**: about two thirds of its replies are an example's statement
  word for word ("Aye, quiet so far." 64 times on the 4B, "Steel out. The Wolf won't trouble us
  long." 67 times on the 2B). The turns arm echoes far less (52 and 85). That is G14's echo moving
  from the stimulus to the statement, which the live-runs list already asks to measure.
- Statements cost 44 prompt tokens in the block and 28 in turns; the turns arm is still the
  longer prompt by 37.

**What it means.** The morning's +7 on 76 prompts was real for `qwen3.5:2b` in direction but about
half the size on 389 prompts, twice, and the same change costs `qwen3.5:4b` 10 to 11 points beyond
noise twice and `gemma4:e2b-it-qat` 6.7 once, with or without written statements. The switch was not
adopted into the package. Turns do make a model echo a written statement less, but no accuracy gain comes with it
outside the 2B.

**What would change this.**

- A model family on which turns win beyond noise on both player lines and policy cases.

Short of that: an answer that leaves `statement` out when none was written; the curated
76 in the Unity path, to compare with the morning's replay; a model above 4B. Raw data: the reports
and `tokens.json` files in the lead's main checkout, `.scratch/151-chat-turns/full/` and `stated/`,
with `summarize.py`, which prints these tables.

### Thirteen models on the interim set, on the RTX 5080: `qwen3.5:4b` first at 330/389, and the idle example helps seven models beyond noise, costing only `qwen3.5:2b` (9 November 2026)

[Run request 001](../run-requests/001-interim-model-sweep-5080/request.md): the 13-model sweep of
the curated-set entry above ("Thirteen models on the evaluation set"), on the interim set's 389
prompts, so per-category results rest on 64 to 66 prompts rather than 9 to 19.

**The set.** The interim set (#223), measured against the curated set's `VillageGuard`. **It is
AI-written.** Claude wrote it and gave the first labels, then Codex labelled it blind, and the two
agreed on 391 of 392. Its prompts may be less ambiguous than the team's 76, so **its scores and the
curated set's are not directly comparable**. Noise floor: 3.6 points overall, 8.7 to 8.8 per category.

**Setup.**
- **The machine: RTX 5080, Docker Ollama 0.40.14, over the network.** This is way B of the
  request. The model ran on the 5080's Docker server (`tools/ollama-server/`), reached through its
  gateway over the home network, and Unity ran in batch mode on the laptop (RTX 4060 Laptop, on AC
  power). The server pins `OLLAMA_GO_TEMPLATE=1`, `OLLAMA_CONTEXT_LENGTH=16384` and
  `OLLAMA_NO_CLOUD=0`. **It is a machine of its own for measuring**: its numbers are compared with
  each other here, never with the laptop's.
- **Latency includes the network hop** from the laptop to the 5080 and back.
- **The framework:** `test` at cd0c09c. Since the laptop's interim run (db16c03), the only framework
  change is #226's API key header in `OllamaProvider`; the prompt is the same.
- **The lock and the GPU:** the 5080's lock was held throughout, and `ollama_remote.py ps` showed
  nothing resident before the first model. One model was resident at a time: warmed, unloaded,
  run, then unloaded with `keep_alive: 0` before the next. The 12 models other than the 2B were
  pulled first, one at a time, and no download overlapped a timed run.
- **The kit:** `tools/live-runs/model-sweep/`, with a 2-request probe per model, then the two arms
  interleaved through the runner. **On** is the defaults, with the profile's idle example "Fine day
  for it."; **off** sets `IdleExampleStimulus = ""`. Both arms used the runner's defaults (grounding
  guard on, temperature 0).
- **The chat path** (the server log's "template selection" line) was not read, since that log is
  only on the 5080.
- **A proof run came first**, the 2B on the curated 76 through the gateway: 56/76 on, 58/76 off,
  `stop_following` 2/7 against 6/7.

**Every model passed the pre-checks.** 2 of 2 probe replies were in the framework's field order on
all 13, with no thinking. Every model sat fully in VRAM (`size_vram` equal to `size`, 1114 to
5327 MB). No arm had warnings or pipeline errors. Each model took 3 to 7 minutes for both arms; the
sweep, 2B baseline included, took 63 minutes.

**Default score, ranked**, each against `qwen3.5:2b`'s 263/389 on this server (floor 3.6; 8.7 to
8.8 per category). The last column is the curated set on the laptop, from the entry above: **two
sets on two machines**, for orientation only.

| Model | VRAM, MB | Default (idle on) | Against the 2B | Categories beyond noise against the 2B | Median / p90 latency, s | Curated, laptop |
|---|---:|---:|---|---|---|---:|
| `qwen3.5:4b` | 2995 | **330** (84.8%) | +17.2, beyond | simple +26.2, conversation +23.4, event +18.5, situation +16.9, impossible +12.5 | 0.51 / 0.56 | 70/76 |
| `qwen3:8b` | 5327 | 315 (81.0%) | +13.4, beyond | simple +26.2, conversation +25.0, event +12.3, situation +12.3 | 0.37 / 0.42 | 65/76 |
| `granite4.1:3b` | 2390 | 304 (78.1%) | +10.5, beyond | simple +23.1, conversation +21.9, impossible +9.4 | 0.24 / 0.27 | 62/76 |
| `nemotron-3-nano:4b` | 2693 | 304 (78.1%) | +10.5, beyond | simple +23.1, situation +20.0, conversation +12.5, event +10.8; impossible −9.4 | 0.35 / 0.38 | 60/76 |
| `qwen3:4b-instruct-2507-q4_K_M` | 3032 | 296 (76.1%) | +8.5, beyond | situation +20.0, simple +16.9, conversation +12.5, event +12.3; impossible −15.6 | 0.32 / 0.36 | 65/76 |
| `L3-8B-Stheno-v3.2` Q4_K_M | 5035 | 294 (75.6%) | +8.0, beyond | simple +24.6 | 0.32 / 0.37 | 60/76 |
| `gemma4:e2b-it-qat` | 1578 | 290 (74.6%) | +6.9, beyond | situation +15.4, simple +9.2 | 0.34 / 0.41 | 64/76 |
| `phi4-mini` | 2956 | 290 (74.6%) | +6.9, beyond | simple +23.1, impossible +18.8, conversation +9.4 | 0.31 / 0.35 | 57/76 |
| `ministral-3:3b` | 2601 | 289 (74.3%) | +6.7, beyond | simple +24.6 | 0.28 / 0.31 | 65/76 |
| `llama3.2:3b` | 2442 | 277 (71.2%) | +3.6, on the floor | simple +20.0; impossible −9.4 | 0.24 / 0.29 | 59/76 |
| **`qwen3.5:2b`** (the baseline) | 2254 | 263 (67.6%) | | | 0.37 / 0.42 | 56/76 |
| `MiniCPM5-2B` Q4_K_M | 1564 | 240 (61.7%) | −5.9, beyond | event −15.4, conversation −10.9, situation −9.2 | 0.21 / 0.23 | 58/76 |
| `lfm3:1.7b` | 1114 | 202 (51.9%) | −15.7, beyond | impossible −35.9, event −27.7, situation −20.0, conversation −10.9 | 0.23 / 0.28 | 46/76 |

Latency is the runner's per-decision time on the "on" arm, warm, one model resident, network hop
included. `command_with_target` is beyond noise for no model: every model scored 57 to 66 of 66 there.

**The idle example on and off**, per model (off − on, floor 3.6; with the line / without):

| Model | On | Off | Off − on | Categories beyond noise, off − on | `stop_following` (of 48) | Unasked actions (of 156) | Missed requests (of 233) | Replies flagged |
|---|---:|---:|---|---|---|---|---|---|
| `qwen3.5:2b` | 263 | 281 | **+4.6, beyond** | simple +15.4, impossible +9.4 | **22 / 39** | 50 / 48 | 36 / 22 | 13 / 7 |
| `qwen3.5:4b` | 330 | 292 | **−9.8, beyond** | conversation −18.8, impossible −12.5, event −12.3, simple −10.8 | 44 / 45 | 23 / 67 | 12 / 3 | 0 / 0 |
| `qwen3:8b` | 315 | 294 | **−5.4, beyond** | impossible −15.6 | 47 / 47 | 40 / 63 | 7 / 4 | 2 / 2 |
| `granite4.1:3b` | 304 | 311 | +1.8, within | — | 38 / 42 | 19 / 21 | 36 / 36 | 2 / 0 |
| `nemotron-3-nano:4b` | 304 | 278 | **−6.7, beyond** | impossible −12.5, situation −12.3, event −9.2 | 44 / 44 | 43 / 69 | 14 / 8 | 2 / 2 |
| `qwen3:4b-instruct-2507` | 296 | 268 | **−7.2, beyond** | conversation −15.6, impossible −10.9 | 38 / 40 | 66 / 94 | 3 / 0 | 0 / 0 |
| `L3-8B-Stheno-v3.2` | 294 | 279 | **−3.9, beyond** | conversation −14.1 | 46 / 47 | 65 / 82 | 3 / 1 | 1 / 0 |
| `gemma4:e2b-it-qat` | 290 | 297 | +1.8, within | simple +12.3 | 29 / 42 | 32 / 45 | 35 / 13 | 0 / 1 |
| `phi4-mini` | 290 | 255 | **−9.0, beyond** | conversation −25.0, impossible −12.5 | 48 / 47 | 36 / 74 | 4 / 1 | 2 / 3 |
| `ministral-3:3b` | 289 | 277 | −3.1, within | impossible −10.9 | 46 / 46 | 64 / 80 | 0 / 0 | 0 / 0 |
| `llama3.2:3b` | 277 | 235 | **−10.8, beyond** | conversation −23.4, impossible −17.2, event −10.8 | 44 / 45 | 78 / 120 | 4 / 2 | 1 / 1 |
| `MiniCPM5-2B` | 240 | 240 | 0 | — | 38 / 42 | 63 / 72 | 32 / 22 | 1 / 0 |
| `lfm3:1.7b` | 202 | 200 | −0.5, within | — | 30 / 30 | 86 / 65 | 3 / 19 | 4 / 3 |

"Unasked actions" counts an action chosen on a prompt labelled `none`, before any guard. "Missed
requests" counts `none` answered to a prompt that wants an action. "Replies flagged" are #224's
reply checks.

**What the idle example does, on five times the prompts.**
- **It helps seven models beyond noise, and costs one.** Taking the line away cost `llama3.2:3b`
  10.8 points, `qwen3.5:4b` 9.8 and `phi4-mini` 9.0. Only `qwen3.5:2b` gained, +4.6. The other five
  are within noise.
- **What it buys is fewer unasked actions.** Without it, they rose on 11 of 13 models: 23 to 67 on
  `qwen3.5:4b`, 36 to 74 on `phi4-mini`, 78 to 120 on `llama3.2:3b`. The losses are in
  conversation and impossible requests, the categories where `none` is right.
- **What it costs is `stop_following`, and on this set two models pay it.** `qwen3.5:2b` gets 22 of
  48 right with the line against 39 without, as on the curated set and on the laptop.
  `gemma4:e2b-it-qat` gets 29 against 42; the curated set's 7 prompts showed this only as 5/7
  against 7/7. Gemma's overall score is within noise either way, because without the line its
  unasked actions rose by 13.
- So the curated-set entry's reading holds and sharpens. The line is the framework's guard against
  unasked actions, and the 2B reads it as a reason to answer `none` to commands.

**Replies.** Every model but two had 0 to 3 replies flagged per arm, all echoes. `qwen3.5:2b` had
13 with the line: "You can stay put now." 6 times, "Strike the Wolf." 6 times and "Fine day for
it." once. `lfm3:1.7b` is the only model with flags other than echoes. With the line, 3 of its
replies were empty, and one ran to 953 characters, listing the action ids, `no_target` and a
`target:` label.

**Limits.**
- **The model ranking is not a set of A/B results of the runner's kind.** It compares separate
  runs on one server, not `EvalArmOrder.Sequential` pairs, at n = 389, on a set an AI wrote for one
  agent.
- **The chat path was not read.** Which models used their Go template is assumed from the curated
  sweep's laptop log, not seen here.
- One grounding threshold, 0.8, was used for every model. It was set on Qwen's tokenizer.
- `llama3.2:3b`'s +3.6 against the 2B sits exactly on the floor.

**What it means.** `qwen3.5:4b` leads on both sets: by 17 points over the 2B here, beyond noise in
five of six categories, at about 1.4 times the 2B's latency on this server. `qwen3:8b` is second
here and `granite4.1:3b` third, at the lowest latency of the top ten. `ministral-3:3b`, tied second
on the curated set, is ninth. Ten models beat the 2B by at least the floor; `MiniCPM5-2B` and
`lfm3:1.7b` fall below it. Choosing a default model, and whether the idle line should stay on for a
model like the 2B, are the lead's decisions; nothing in code changes here.

**What would change this.** #147's curated 200+ prompts, written by people, with the ranking run as
`Sequential` pairs. A different Ollama build, or a server without `OLLAMA_GO_TEMPLATE=1`, changes
several models' templates. Raw data: `docs/run-requests/001-interim-model-sweep-5080/results-5080-interim.json`
(per-prompt answers removed), and the full `results.json` with every answer, plus the per-model
reports, probes and logs, in the live-runs session's checkout, `.scratch/model-sweep-5080/interim/`.

### How a few-shot example names its target: spaced names are no worse on either model; the uncategorised fallback beats no example by 15 to 19 points but costs impossible requests 30 (9 November 2026)

#190's Living Vale #20 added two switches to `FewShotBuilder`, both off (the first was then adopted
and removed; see below). `SpacedExampleNames` calls
a target by its id's words, "north tower" rather than "NorthTower", and drops the article before
the refusal's missing target ("Go and stand by Atlantis." rather than "the Atlantis").
`ExampleTargetFallback` gives an action with no preferred target here an example naming the first
offered target it takes, skipping any the stimulus mentions. Two A/B runs per model.

**The shipped evaluation agent cannot measure either as it stands.** Its targeted actions prefer
`well` and `wolf`, one-word ids that are always offered. Rendering every prompt's few-shot block in
both arms through `Agent.Preview`, with no model called: with the fallback, 0 of 389 prompts sent a
different block, so that A/B would have compared identical prompts and was not run; with spaced
names, all 389 differed, but only in "Well"/"Wolf" becoming lowercase and the refusal's "the". So
each run changed the profile copy both its arms used, never the asset:

- **Spaced names:** `move_to`'s preferred target became `north_tower`, a two-word id. The arms send
  "Go and stand by the NorthTower." and "Go and stand by the north tower.", "Strike the Wolf." and
  "Strike the wolf.", and the two refusals.
- **Fallback:** `move_to`'s and `attack_target`'s preferred lists were emptied, as for a game whose
  targets change every turn. Off, the block has no targeted example at all; on, it names the first
  target in the set's order that the stimulus does not mention, usually `bridge` for both actions
  ("Go and stand by the Bridge.", "Strike the Bridge."), `village_gate` when the stimulus mentions
  the bridge. The set's targets have no categories, so nothing stops a bridge being attacked.

The entry point checked every rendered example line against every prompt's stimulus (hard rule 4);
none matched.

**Setup.** The interim set (389 prompts, `Assets/Evaluation/Interim/`, set commit a24dde9) with a
copy of `Assets/Evaluation/VillageGuard.asset`, the RTX 5080's Docker Ollama 0.40.14 over the
network (home), each model fully in VRAM after its warm-up, nothing else resident, the laptop on
mains power, the 5080 lock held. Interleaved, runner defaults (guards on, the profile's idle
example), the 9 November state wording. A 24-prompt slice first on `qwen3.5:4b` (24 against 24),
then the four runs back to back, 5 to 6½ minutes each: `qwen3.5:4b` spaced then fallback, then
`qwen3.5:2b` the same.

| | 4B glued | 4B spaced | 2B glued | 2B spaced |
|---|---:|---:|---:|---:|
| **All, 389** | 334 (85.9%) | 337 (86.6%) | 269 (69.2%) | 280 (72.0%) |
| Player lines, 259 | 241 (93.1%) | 240 (92.7%) | 198 (76.4%) | 210 (81.1%) |
| Situations and events, 130 | 93 (71.5%) | 97 (74.6%) | 71 (54.6%) | 70 (53.8%) |
| simple_command, 65 | 98.5% | 98.5% | 70.8% | 83.1% |
| command_with_target, 66 | 100.0% | 100.0% | 93.9% | 95.5% |
| impossible_request, 64 | 78.1% | 76.6% | 64.1% | 67.2% |
| conversation, 64 | 95.3% | 95.3% | 76.6% | 78.1% |
| situation, 65 | 58.5% | 64.6% | 50.8% | 49.2% |
| event, 65 | 84.6% | 84.6% | 58.5% | 58.5% |

| | 4B fallback off | 4B fallback on | 2B fallback off | 2B fallback on |
|---|---:|---:|---:|---:|
| **All, 389** | 243 (62.5%) | 317 (81.5%) | 214 (55.0%) | 273 (70.2%) |
| Player lines, 259 | 189 (73.0%) | 231 (89.2%) | 167 (64.5%) | 207 (79.9%) |
| Situations and events, 130 | 54 (41.5%) | 86 (66.2%) | 47 (36.2%) | 66 (50.8%) |
| simple_command, 65 | 100.0% | 98.5% | 83.1% | 81.5% |
| command_with_target, 66 | 12.1% | 100.0% | 9.1% | 93.9% |
| impossible_request, 64 | 96.9% | 64.1% | 96.9% | 67.2% |
| conversation, 64 | 84.4% | 93.8% | 70.3% | 76.6% |
| situation, 65 | 40.0% | 56.9% | 33.8% | 44.6% |
| event, 65 | 43.1% | 75.4% | 38.5% | 56.9% |

Floors are 3.6 points at n = 389, 4.4 at 259, 6.2 at 130 and 8.8 at 65.

**Spaced names: no worse on either model.** `qwen3.5:4b` +0.8 overall, within noise in every scope
and category (situation +6.2, within its 8.8). `qwen3.5:2b` +2.8 overall, within noise; beyond it
in its favour on player lines, +4.6 against a 4.4 floor, from simple_command, +12.3 (9 commands to
follow or stop fixed, 1 broken). Nothing went worse beyond noise. Reply checks: the 4B flagged no
echo in either arm, the 2B 9 glued and 11 spaced.

**The fallback: far better than no example, but it teaches the model that any target goes.** Off
against on, `qwen3.5:4b` +19.0 overall and `qwen3.5:2b` +15.2, both beyond noise, almost all from
command_with_target (8 to 66 and 6 to 62 of 66: with no targeted example, the models answered
commands to go somewhere with `follow_player` or `none`), plus situations and events (+24.6 and
+14.6). But impossible_request fell beyond noise on both, 62 to 41 and 62 to 43 of 64. Of the 4B's
23 wrong, 10 attacked a place and 7 walked to one: "Burn down the market." became `attack_target
market`, "Tear down the north tower." `attack_target north_tower`, "Sink the docks." `attack_target
docks`, after an example showing "Strike the Bridge." as a correct attack. Of the 2B's 21, 17 were
`attack_target`, 10 of them on the wolf, for requests that have nothing to do with it ("Find my lost
ring.", "Walk through the wall.", "Unlock the cave."), and 7 on a place. Off, with no targeted example, the models seldom took a targeted action at all,
which is why that arm's impossible requests scored 97%.

For scale only, not a measured comparison: Request 001 measured the unchanged agent on the same
server the same day at 330 (4B) and 263 (2B). The glued arms above, which differ from it only in
`move_to` naming the north tower instead of the well, scored 334 and 269; the fallback-on arms,
which name uncategorised fallback targets in place of the hand-picked ones, 317 and 273.

**What it means.**

- **Spaced names: adopted 9 November.** They read better and are no worse within noise on both
  models, the tech lead's rule for a default. The switch is gone: every example now calls a target
  by its id's words, and the refusal fits its article, as the B arms above did.
- **The fallback should stay off by default.** It only changes an agent that today sends no example
  for an action, and there it adds 15 to 19 points; but on an agent whose targets have no
  categories it pairs a verb with a target it does not suit, and impossible requests fall about 30
  points, beyond noise on the 4B. A game should turn it on only where its targeted actions declare
  `TargetCategories`, so an attack example names a creature and a trade example an item. That case
  is unmeasured: the interim set's targets carry no categories.
- **The shipped evaluation agent never sees either change**, so neither alters any number measured
  on it so far, apart from the two lowercase letters and the refusal's article.

**What would change this.** A set whose targets carry categories, to measure the fallback where it
is meant to be used; #147's curated prompts. Raw data: the four reports, the rendered blocks
(`lv20-render-*.txt`), the entry point (`Lv20AbRun.cs.txt`) and the test results, in the lead's checkout,
`.scratch/190-lv20/`.

### Situations and events: the deciding fact sat in the observations, and moving it into the stimulus gained 22 answers on `qwen3.5:4b` and 17 on `qwen3.5:2b` (10 November 2026)

For #151. On the interim set, player lines are near solved (`qwen3.5:4b` about 240 of 259), while the
130 `situation` and `event` prompts sit at about 91 to 97. Two questions: why the model gets those
wrong, and whether one small change to what is sent fixes a real share of it.

**Part 1, no model: the 4B's 39 misses, by cause.** Every wrong situation or event answer of
`qwen3.5:4b`'s control arm (guards on, 330/389, below), read against its prompt, observations, label
and the full request as sent (rendered through `Agent.Preview`). One cause per miss:

| Cause | Misses | Example (expected → chosen) |
|---|---:|---|
| `follow_player` expected; the model walked to where the player was going, or stayed | 9 | "The player sets off toward the locked cave on their own." → `move_to locked_cave` |
| A wolf on the scene; the model went to the place instead of attacking | 8 | "Screams are coming from the well." + "A wolf has cornered a child by the well" → `move_to well` |
| Passive: `none` where the fact that calls for action is only in the observations | 5 | "The lookout has fallen asleep." + "The north tower's lookout is snoring" → `none` |
| `stop_following` expected; the model chose `none` | 4 | "The player has stepped into the bathhouse." + "Guards may not enter the bathhouse" → `none` |
| The label is arguable, or another answer has the same effect | 4 | following, "The player is crossing the bridge, and you are right behind." → `follow_player`, expected `none` |
| Acted on a calm scene | 4 | "A lantern went out by the shelter." → `move_to shelter` |
| Right action, wrong target (the example's `well`) | 2 | "Something is splashing in the stream." + "A villager fell into the stream beside the bridge" → `move_to well` |
| Attacked the wolf the target list says is about, with none in the scene | 2 | "A brawl started outside the shelter." → `attack_target wolf` |
| Right answer, stopped by the grounding guard (0.74 against 0.8) | 1 | "A child fell off the railing." → `move_to bridge`, rewritten to `none` |

25 of the 39 are situations, 14 events. 11 are prompts whose own note says the target is named only
in the observations (25 such prompts in the set).

**How much of the gap is the labels.** 4 of 130 labels are arguable: twice the model's answer has the
same effect in the game (re-issuing `follow_player` while following; `stop_following` for a player
standing still), and twice it is a defensible other place (the shelter for a grass fire that spread
from the campfire; the gate rather than the road for riders seen far down the road). Counting those
as right, an honest ceiling for these 130 is about 126 (97%). A larger share is labelled against a
policy the prompt never states: the 13 follow and stop-following prompts expect a guard to follow,
unasked, a player who walks off alone, and to stop following at a door it may not pass. The goal line
says only "help the player", and the model never sees what an action does (no action description
reaches an Ollama model unless `ListActionsAndTargets` is on), so those 13 test whether the model
guesses the designer's policy. They are not wrong labels, but they are not an observation-reading
test either.

**What the request looks like.** For a situation, every few-shot line is a command relabelled
`Situation:` (`Situation: "Strike the wolf."`), and the one declarative line, "Fine day for it.", is
answered `none`. The prompt's own observations come last in the system prompt, after the state line
and the runner's eleven `id: description.` target lines; the user turn holds only the stimulus. In the
4B's misses the deciding fact (the wolf, the pinned trader, the snoring lookout) was almost always one
of those last lines.

**Part 2, three variants.** Picked to hit the biggest buckets, each a prompt-side change with no new
framework switch:

- **fold**, where the event text sits: for a non-`Player` stimulus, the prompt's own observation lines
  join the stimulus's quote and leave the observation block (`Situation: "A stall has collapsed on
  someone. A trader is pinned under a stall in the market."`). It changes the 118 situation and event
  prompts that have observations, and nothing else. Done by a provider config in scratch that rewrites
  the request's text before an ordinary `OllamaProvider` sends it; both arms went through it, the
  control unchanged. Aimed at the passive and place-not-threat buckets.
- **list**, the action descriptions: `ListActionsAndTargets = true`, an existing switch (measured
  before only on the curated 76, on the 2B), so the prompt says "follow_player: Walk behind the player
  wherever they go." and "stop_following: Stop following the player and stay where you are.". Aimed at
  the follow and stop-following buckets.
- **example**, one situation-shaped few-shot line written for this run, after reading the set: "A wolf
  is tearing at a woodcutter's coat outside the north tower." → `attack_target wolf`, before the `none`
  lines and labelled like the call. "Woodcutter", "tearing" and "coat" appear in no stimulus or
  observation of either set (hard rule 4). Aimed at the place-not-threat bucket.

**Setup.** The interim set (389 prompts, set commit a24dde9) with `Assets/Evaluation/VillageGuard.asset`
unchanged, `origin/test` at 59e11f3, the RTX 5080's Docker Ollama 0.40.14 over the network (home), the
laptop on mains power, the 5080 lock held, nothing else resident (`ps` before and after: `qwen3.5:4b`
3.1 GB, then `qwen3.5:2b` 2.4 GB, 100% on the GPU). Interleaved, runner defaults (guards on, the
profile's idle example), the 9 November state wording. A 24-prompt slice first on each model (fold, 24
against 24 on both), then the three 4B runs back to back, 6 to 6½ minutes each, then fold on the 2B.
The wrapped runs' reports say residency "not checked", because the runner reads it only from a plain
`OllamaProvider`; the `ps` readings stand in for it.

| | 4B control | 4B fold | 4B list | 4B example | 2B control | 2B fold |
|---|---:|---:|---:|---:|---:|---:|
| **All, 389** | 330 (84.8%) | **352 (90.5%)** | 323 (83.0%) | 325 (83.5%) | 261 (67.1%) | **278 (71.5%)** |
| Player lines, 259 | 239 | 239 | 226 | 235 | 197 | 197 |
| Situations and events, 130 | 91 (70.0%) | **113 (86.9%)** | 97 (74.6%) | 90 (69.2%) | 64 (49.2%) | **81 (62.3%)** |
| simple_command, 65 | 64 | 64 | 62 | 64 | 48 | 48 |
| command_with_target, 66 | 66 | 66 | 66 | 66 | 62 | 62 |
| impossible_request, 64 | 49 | 49 | 43 | 46 | 41 | 41 |
| conversation, 64 | 60 | 60 | 55 | 59 | 46 | 46 |
| situation, 65 | 40 | 51 | 48 | 38 | 26 | 39 |
| event, 65 | 51 | 62 | 49 | 52 | 38 | 42 |

The 4B control gave the same 389 answers in all three runs. Floors: 3.6 points at n = 389, 4.4 at
259, 6.2 at 130, 8.8 at 65.

- **fold, `qwen3.5:4b`: +5.7 overall, beyond noise; situations and events +16.9, beyond noise**
  (situation and event +16.9 each). 22 fixed, none broken; player lines identical. Of the 39 misses
  it fixed all 8 place-not-threat, 4 of 5 passive, all 4 calm-scene actions, 2 of 4 stop-following,
  the guard's false stop, one wrong target, one standing wolf and 1 of 9 follows.
- **fold, `qwen3.5:2b`: +4.4 overall, beyond noise; situations and events +13.1, beyond noise**
  (situation +20.0 beyond, event +6.2 within). 24 fixed, 7 broken, all of the over-acting kind: calm
  scenes whose place is now in the quote ("Fishermen are mending nets at the docks." → `move_to
  docks`). The 4B did not over-act on the same prompts.
- **list, 4B: −1.8 overall, within noise; situations +12.3, beyond noise, but player lines −5.0 and
  impossible_request −9.4, both beyond noise.** It fixed 4 of 9 follows and 2 of 4 stop-followings,
  and made the model act on lines that need nothing ("Keep your eyes open." → `follow_player`, "Burn
  down the market." → `attack_target market`).
- **example, 4B: −1.3 overall, within noise everywhere.** It fixed none of the eight place-not-threat
  misses it was written for.

**What is left after fold on the 4B: 17**: the 4 arguable labels, 10 follow and stop-following, and 3
others (one passive, one wrong target, one standing wolf). Against the ceiling of about 126, fold's
113 leaves 13, and 10 of those are the unstated following policy.

**An aside on the floor.** The 9 November control (#231, before #234 lowercased "Well" and "Wolf" and
dropped the refusal's "the") also scored 330, but 14 of its 389 answers differ from tonight's, 10 in
outcome, 5 each way. Two letters and an article moved 2.6% of answers with no net change: the first
measurement on this server of how far an unrelated change to the prompt moves single answers.

**What it means.**

- **The gap is mostly where the fact sits, not what the model knows.** Moving the prompt's own lines
  from the end of the observation block into the stimulus fixed 22 of the 4B's 39 misses and broke
  none, and fixed 24 of the 2B's 66 for 7 broken; beyond the floor on both models, overall and on
  situations and events. One run per model, on this set.
- **It is not yet a framework change, and no default changed.** On this set the fold changes how the
  runner (or a developer) splits a situation between the stimulus and the observations. Two readings
  fit the result and were not separated: the fact helps because it is in the user turn, or because it
  is no longer behind eleven boilerplate target lines. They lead to different changes (guidance to put
  a situation's facts in its stimulus, or the caller's own observations ahead of the target lines).
  The tech lead decides.
- **The set's "named only in the observations" prompts measure this very placement**, so a fold-style
  change raises the score partly by removing what those prompts were written to test. A game's
  developer chooses where facts go; the set should keep testing both.
- **Following needs the policy written down, not more examples.** Action descriptions moved half the
  follow cases but made the 4B act on player lines that need nothing; one more example did nothing. A
  goals or knowledge line about following ("follow the player when they leave alone; stop at a place
  you may not enter") is the obvious next run; #231's lines did not cover it.
- **The labels are not the problem.** About 4 of 130 are arguable; the ceiling stays near 97%.

**Not tried.** Observation order with the prompt's own lines first (separates the two readings above);
the stimulus label's wording for a non-spoken event; fold together with the action list; a
following-policy line; other models than the two; the 2B's misses were not sorted by cause. Raw data:
the reports, renders, cause table and entry point in the lead's checkout, `.scratch/151-policy-gap/`.
The next entry, "Where a call's own facts go", takes up the observation order and the
following-policy line.

### Where a call's own facts go: after the stimulus gains about 21 of 389 on both models, but only when the standing facts stay in the block (10 November 2026)

For #151, following the entry above. Its fold moved a situation's own lines into the stimulus by
reading the label, and left two readings open: the fact helps because it is in the user turn, or
because it is no longer behind eleven boilerplate target lines. This run turns it into a framework
option that never reads the label, `DecideOptions.ObservationPlacement`, and separates the two.

**What moves.** The framework already told two kinds of line apart: an agent's
`ObservationSources`, gathered on every call, and the call's own `DecideOptions.Observations`.
`Agent.Observe` puts the sources' lines first and the call's last. Before this change `EvalRunner`
passed everything as the call's own (the arm's lines, the state line, the 11 `id: description.`
target lines, then the prompt's own), so the fold's "prompt's own lines" had no name in the
framework. The runner now sends the standing lines through an observation source, so only a
prompt's own lines are the call's; with the default placement every one of the interim set's 389
requests is byte-identical to the entry above's control render. Three placements were built:

- **`AfterSources`**, the default: the call's lines close the "What you know right now:" block,
  as before.
- **`BeforeSources`**: the call's lines open that block, ahead of the standing lines.
- **`WithStimulus`**: the call's lines leave the block and follow the stimulus's closing quote in
  the user turn, each ended as a sentence: `Event: "The player lay down to sleep by the campfire."
  Other guards are watching the camp.` The quote stays the developer's text. The fold put the facts
  inside the quote.

Neither moved arm reads the label, so they also move the 8 player prompts that carry observations
(6 `command_with_target`, 2 `conversation`); 126 of the 389 requests change, against the fold's 118.

**Setup.** The interim set (389 prompts, `Assets/Evaluation/Interim/`, set commit a24dde9) and the
curated set (76, `Assets/Evaluation/`, same commit), with `Assets/Evaluation/VillageGuard.asset`
unchanged and the 9 November state wording. The RTX 5080's Docker Ollama over the network, the 5080
lock held for each run and released between them, `ps` showing nothing before each, the model 100%
in VRAM after every arm (`qwen3.5:4b` 3.1 GB, `qwen3.5:2b` 2.4 GB). Interleaved, runner defaults
(guards on, the profile's idle example), framework 0.4.0-preview.1 with this change. A 24-prompt
slice first (24 against 24), then each run back to back in one session, 5 to 6 minutes each for the
389 and about a minute for the 76. Floors: 3.6 points at n = 389, 4.4 at 259, 6.2 at 130, 8.8 at
65, 8.1 at 76, 11.5 at 38.

**Interim set, 389 prompts.**

| | 4B control | 4B `BeforeSources` | 4B control | 4B `WithStimulus` | 2B control | 2B `WithStimulus` |
|---|---:|---:|---:|---:|---:|---:|
| **All, 389** | 330 | 328 (−0.5) | 330 | **351 (+5.4)** | 261 | **283 (+5.7)** |
| Player lines, 259 | 239 | 239 | 239 | 239 | 197 | 199 |
| Situations and events, 130 | 91 | 89 (−1.5) | 91 | **112 (+16.2)** | 64 | **84 (+15.4)** |
| simple_command, 65 | 64 | 64 | 64 | 64 | 48 | 48 |
| command_with_target, 66 | 66 | 66 | 66 | 66 | 62 | 62 |
| impossible_request, 64 | 49 | 49 | 49 | 49 | 41 | 41 |
| conversation, 64 | 60 | 60 | 60 | 60 | 46 | 48 |
| situation, 65 | 40 | 39 | 40 | **51 (+16.9)** | 26 | **43 (+26.2)** |
| event, 65 | 51 | 50 | 51 | **61 (+15.4)** | 38 | 41 (+4.6) |

Bold is beyond the floor. The 4B control gave 330 in both runs, as it did in the entry above.

- **`BeforeSources` does nothing.** 7 fixed and 9 broken, all within noise. The order inside the
  block is not the lever; it was dropped from the option.
- **`WithStimulus`, `qwen3.5:4b`: 23 fixed, 2 broken**, all on situations and events. Against the
  entry above's causes, it fixed 7 of 8 place-not-threat, all 5 passive, all 4 calm-scene actions,
  2 of 4 stop-following, both wrong targets, the guard's false stop, one standing wolf and 1 of 9
  follows. The 2 it broke walked to the named place instead of the right one (`village_gate` for a
  road, `campfire` for the camp's smoke). That is the fold's 352 without reading the label.
- **`WithStimulus`, `qwen3.5:2b`: 28 fixed, 6 broken**, 2 of the fixes conversation lines; the
  situations move far more than the events.

**The calm scenes.** The fold broke 7 calm scenes on the 2B. Counting the 40 situations and events
whose expected action is `none`, against the 90 that call for one:

| | over-acted on 40 calm scenes | stayed still on 90 that call for action |
|---|---:|---:|
| 4B control / `WithStimulus` | 6 / **2** | 10 / **3** |
| 2B control / `WithStimulus` | 15 / 14 | 14 / 9 |
| 4B control / `BeforeSources` | 6 / 10 | 10 / 10 |

`WithStimulus` makes neither model over-act; it is the fold's facts inside the quote that did.

**Curated set, 76 prompts, control against `WithStimulus`.** `qwen3.5:4b` 69 against 68 (−1.3),
`qwen3.5:2b` 54 against 54; situations and events 31 against 30 and 23 against 23, every category
within noise (the floor is 11.5 on 38). Calm scenes: 4B 2 of 12 over-acted in both arms, 2B 3
against 4. This set's 4B control already gets 31 of its 38 situations and events; whatever the
interim set's gap was, it is not here to close.

**The whole block with the stimulus.** All three demo games pass every fact as the call's own
(`DecideOptions.Observations`), with no observation source, so `WithStimulus` would move their whole
block. An eval-only switch, `EvalArm.StandingLinesAsCallObservations`, sends the runner's standing
lines with the call the same way; with `AfterSources` its 389 requests are byte-identical to the
control render. Interim set, `AfterSources` against `WithStimulus`, both arms with the switch on:

| | 4B whole, after | 4B whole, with | 2B whole, after | 2B whole, with |
|---|---:|---:|---:|---:|
| **All, 389** | 330 | **299 (−8.0)** | 261 | **221 (−10.3)** |
| Player lines, 259 | 239 | **199 (−15.4)** | 197 | **157 (−15.4)** |
| Situations and events, 130 | 91 | **100 (+6.9)** | 64 | 64 |
| simple_command, 65 | 64 | **48** | 48 | **38** |
| command_with_target, 66 | 66 | **60** | 62 | 59 |
| impossible_request, 64 | 49 | **42** | 41 | **30** |
| conversation, 64 | 60 | **49** | 46 | **30** |
| situation, 65 | 40 | **49** | 26 | 31 |
| event, 65 | 51 | 51 | 38 | 33 |

A request then reads `Player: "Walk with me." You are not following the player. bridge: The wooden
bridge over the stream. village_gate: …`, the stimulus followed by twelve standing facts. The 4B
answered 13 follow commands, "Walk with me." among them, with `none`, and attacked the wolf for impossible
requests; the 2B over-acted on 34 of the 40 calm scenes (15 in its control) and walked to the gate
for follow commands.

**Step 4: one following-policy line, report only.** The 4B's 18 misses left after `WithStimulus`
include 9 follow and stop-following. One line written for this run, as a designer would, was added to
the profile's knowledge in memory (`EvalArm.Knowledge`, the asset untouched): "When the player sets
out without you, follow them; once they settle somewhere you cannot go with them, stop following."
No four-word run of it appears in any prompt file of either set (hard rule 4). Interim set, 4B,
`WithStimulus` against `WithStimulus` plus the line: 351 against 346 (−1.3, within noise);
situations +9.2 beyond noise (51 → 57), events 61 → 63, but simple_command −15.4 beyond noise
(64 → 54) and player lines −5.0 beyond noise. It fixed 7 of the 8 remaining follows and broke 10
commands to stay put ("Hold here." → `none`, "I'll go on alone from here." →
`follow_player`). Calm scenes 2 against 3. A following policy is the game's to write, and this one
costs as much as it fixes.

**What was and was not measured.** Measured: two models, two sets, one session, one run each, with
the standing lines in an observation source and the prompt's own one to three lines as the call's;
and, on the interim set, the whole block as the call's. Not measured: a real game's split between
sources and call lines; other models; other servers; a `WithStimulus` call with memory on, whose
history then carries the facts.

**What it means.**

- **The deciding fact helps in the user turn, not earlier in the block.** `WithStimulus` gains as
  much as the fold did, on both models, without reading the label; `BeforeSources` gains nothing.
- **It helps only for the facts of this moment.** Twelve standing lines after the stimulus cost
  15 points of player lines on both models. A developer who wants the gain keeps what holds on every
  call in an `IObservationSource` and passes a moment's own facts with the call.
- **The default stays `AfterSources`** (the tech lead's decision): every demo game, and probably most
  developers, pass every fact with the call today, and for them `WithStimulus` is the worst layout
  measured. Flipping it waits on the demos moving their standing facts into sources.

Raw data: reports, renders, the causes table and the entry point in this branch's
`.scratch/151-placement/` (gitignored, in the worktree that ran it).

### Living Vale's adoption of #190: no visible regression, after one fix to the route examples (10 November 2026)

**What changed.** Living Vale adopted the merged #190 features (branch `69-lv-adopt-190`). Two of
them change what the model reads: the personas' facts and speaking rules move from observation
lines and the personality into `AgentIdentity.Knowledge` and `StatementRules` (#231), and the
per-turn example rewriting gives way to fixed example lines with `INamedTarget` names and
`ExampleTargetFallback` (#234). The action and target list, counter-examples and the idle line were
adopted in G5g and are unchanged; the paired schema was not adopted.

**Conditions.** RTX 5080, Docker Ollama 0.40.14, over the network; one model resident at a time,
nothing else on the card (the shared lock held). Control is `test` at 2c27289, the arm is the branch,
each from its own worktree, with the game's own live checks: `MaraTradeLiveCheck` (5 asked trades, 1
plain line and the 12 non-trade lines, each line twice) and `QuestRouteLiveCheck` (the 7 G4c route
lines, each twice). Order per run: control gemma4, adopted gemma4, adopted 4B, control 4B.

**Result.** These sets are small: read them as "no visible regression", not as a measurement.

| Run | Arm | Model | Asked trades | Unasked offers | Route lines |
|---|---|---|---:|---:|---:|
| 1 (routes once) | control | gemma4:e2b-it-qat | 5/5 | 0/1 | 7/7 |
| 1 | adopted | gemma4:e2b-it-qat | 5/5 | 0/1 | **6/7** |
| 2 | control | gemma4:e2b-it-qat | 10/10 | 0/26 | 14/14 |
| 2 | adopted | gemma4:e2b-it-qat | 10/10 | 0/26 | **12/14** |
| 2 | control / adopted | qwen3.5:4b | 10/10 / 10/10 | 0/26 / 0/26 | 14/14 / 14/14 |
| 3, after the fix | control | gemma4:e2b-it-qat | 10/10 | 0/26 | 14/14 |
| 3, after the fix | adopted | gemma4:e2b-it-qat | 10/10 | 0/26 | 14/14 |
| 3, after the fix | control / adopted | qwen3.5:4b | 10/10 / 10/10 | 0/26 / 0/26 | 14/14 / 14/14 |

The one miss was one line, 3 of 3 times: Elra, "This is the medical parcel from the wagon, for your
garrison.", answered `none` ("It never arrived. The garrison is short of medicine."), where control
delivered. Two diagnostic arms on gemma4, the route lines three times each, both put it right
(21/21): the persona facts back in the observations, or the route actions' example targets named
as preferred. The cause is the fallback's mention check: the line says "parcel", so the fallback
skipped `delivery_parcel_elra`, Elra's only delivery, and her delivery example was left out. The
game now prefers each route action's own targets, which the framework names mentioned or not, as
`RefreshExamples` did; run 3 is that code. The idle line's effect survives the adoption: no unasked
offer in 52 non-trade lines per model.

**What would change it.** A larger, labelled set of the game's lines; the fallback's mention check
measured on targeted actions with categories (`docs/live-runs.md`, waiting), where a line naming the
target it wants is the normal case. Raw rows: the lead's checkout, `.scratch/lv-adopt-190/ab/` (not in git).

### GreyBoxStrategy: a model in a country's seat plays at a random player's level; the game-side moves line is what makes it attack, and the grounding guard contains legal attacks (10 November 2026)

**One game, small n.** These are game-side findings on a new game (#20, stage 2), not
evaluation-set results. Every arm is 30 games. Read the decision-level shares as solid, to about
±4 points on 340 to 540 decisions an arm, and the outcomes as weak.

**What was run.** `Demos/GreyBoxStrategy` played with a framework `Agent` in one seat against
four scripted countries (two expansionist, two cautious), seats rotating, seeds 1 to 30 for every
row. The control arm sends this turn's facts after the stimulus (`ObservationPlacement.WithStimulus`),
with the plain stimulus "Your move.". It also sends a game-side standing line naming each move's
legal targets ("Moves open to us: attack fenmark or westmarch; ..."), and leaves the grounding guard on.
Each treatment changes one of those four. Baselines: a scripted or random player in the same seat
on the same seeds. Conditions: RTX 5080, Docker Ollama 0.40.14, over the home network, one model
resident, temperature 0, `qwen3.5:4b` then `qwen3.5:2b` in one session (16:20 and 16:39 that day),
with `ListActionsAndTargets` on and `MaxListedTargets` 40. A 30-game pilot of the 4B the same
afternoon, before one game-side fix (a target named on a move that takes none is now dropped,
not refused), gave the same control numbers (1.8 provinces, 20% contained).

| Seat | Mean provinces at the end | Knocked out of 30 | Wins |
|---|---:|---:|---:|
| scripted expansionist | 4.3 | 19 | 8 |
| scripted cautious | 3.1 | 17 | 1 |
| random | 1.4 | 21 | 1 |
| `qwen3.5:4b`, control | 1.8 | 19 | 0 |
| `qwen3.5:2b`, control | 0.4 | 26 | 0 |

| Model, arm | Provinces | Contained by a guard | Hold | Attack |
|---|---:|---:|---:|---:|
| 4b control | 1.8 | 21% | 31% | 15% |
| 4b after-sources | 1.8 | 34% | 53% | 16% |
| 4b headline stimulus | 2.0 | 9% | 14% | 10% |
| 4b no moves line | 0.9 | 25% | 32% | 2% |
| 4b grounding off | 2.1 | 12% | 20% | 21% |
| 2b control | 0.4 | 50% | 91% | 1% |
| 2b after-sources | 0.6 | 66% | 93% | 1% |
| 2b headline stimulus | 0.7 | 34% | 79% | 1% |
| 2b no moves line | 0.4 | 45% | 87% | 0% |
| 2b grounding off | 0.3 | 33% | 65% | 8% |

Median latency was 0.55 to 0.60 s a decision on the 4B and 0.40 to 0.55 s on the 2B, with no
pipeline errors. A game costs the model 11 to 18 decisions.

**What it shows.**

- Neither model is a player yet. The 4B is near random (1.8 provinces against 1.4); the 2B is
  below it. The 4B's attacks are well chosen, winning 89% of those that landed against the
  scripts' 97%, but it lands 2.7 a game to their 13.
- The moves line is a game-side stand-in for listing legal pairs in the prompt (#216, stage 1's
  N1). It is the one arm that moves the 4B's play a long way. Without it, attacks fall from 15% to 2% of
  council moves.
- The grounding guard (`TargetGroundingGuard`, threshold 0.8) contains legal moves whenever
  several targets are legal, because a sensible model spreads its probability across them. In the
  4B's control arm it fired 63 times (47 on attacks); in the 2B's, 150 times. Switching it off
  cut the 4B's containment from 21% to 12%.
- After the stimulus beats after the sources on containment (21% against 34% on the 4B, 50%
  against 66% on the 2B), which agrees in direction with the evaluation-set result of the same
  day. The game's outcome did not move.
- The headline stimulus cuts holds, but turns them into recruits and tax rises, not attacks.
- The 4B copies its remembered statements over the facts. Once it called an army of 108
  "critically low at 10 soldiers" for 20 turns. That points at what `RollingHistory` replays (#216
  item 3).

**What would change this.** A run of 100 games or more per arm, and a second game state to check that the
grounding guard's misfires are about several legal targets and not about this prompt. Also an
arm with a guard threshold the call can set, which the framework does not offer (stage 2's gap
N4). The tables, method and gaps are in `Demos/GreyBoxStrategy/README.md`.

### Saying which targets each action may take: per target helps `qwen3.5:4b` by 4.1 points and costs `qwen3.5:2b` about 4, while the paired schema alone takes illegal pairs to 0 on both (10 November 2026)

For #151. Ollama never shows the schema to the model (10 October), so a game's target categories,
its paired schema (`PairTargetsWithActions`) and its per-target checks (`ITargetAvailability`) reach
a local model nowhere. The model can pick a pair the guards then refuse. This run asks whether
saying the legal pairs in the prompt helps. It is the new opt-in `DecideOptions.LegalTargets`,
whose pairs come from the paired schema's own rule and availability answer.

**Two renderings were tried**, against the plain `ListActionsAndTargets` list:

- **PerAction** ended each action's line with its targets: `- attack_target: Attack something.
  Targets: wolf.`
- **PerTarget** names, on each target's line, the actions that may take it: `- wolf (for
  attack_target)`.

Both renderings print `Target: no_target.` on the line of an action that may take no listed target.

**Neither set has restricted pairs as it ships.** Rendered with no model, 0 of the interim set's
389 prompts give any action a strict subset of the targets. The curated 76 uses the same targets,
so it has none either. On these sets the option can only show formatting and that it does no harm.
So the runner got an eval-only legality, `EvalArm.Legality`, applied to the arm's own copies. The
prompt set, its labels and the profile stay unchanged.

- **Categories:** the set's 10 places are "place", the wolf is "creature", and the player is
  "player". `move_to` takes places, `attack_target` creatures, and the two follow actions the player.
- **Extra targets:** six people that no action may take (villagers, lookout, child, farmer, trader,
  merchant), told as standing lines.
- **Cap:** the list's cap is 20.

The runner refuses a legality that would make any label's expected pair illegal, and with it all
389 prompts have restricted pairs. **This legality arm is the only evidence for restricted pairs.**
A number from a real game is still owed: GreyBoxStrategy with `PerTarget` in place of its game-side
moves line.

**Setup.**

- **Sets:** the interim set (389, `Assets/Evaluation/Interim/`) and the curated set (76), both at
  set commit a24dde9, with `Assets/Evaluation/VillageGuard.asset` unchanged.
- **Machine:** the RTX 5080's Docker Ollama over the network. The 5080 lock was held for each run
  and released between runs, and `ps` showed nothing resident before each run. The model was 100%
  in VRAM (`qwen3.5:4b` 3.1 GB, `qwen3.5:2b` 2.4 GB).
- **Runs:** interleaved, runner defaults (guards on, temperature 0), framework 0.4.0-preview.1 with
  this change. A 24-prompt slice came first, then 18 runs. The 4B half ran before a pause for the
  card, and the rest ran in one later session the same day.
- **Control:** the plain-list control arm drifted by 1 to 2 answers between runs. Each comparison
  below is against its own run's control.
- **Floors:** 3.6 points at n = 389, 8.8 at 65, 11.2 at 40 (calm scenes), 8.1 at 76.

**Interim set, survived (after guards), A against B.**

| Arms | `qwen3.5:4b` | Δ | `qwen3.5:2b` | Δ |
|---|---|---:|---|---:|
| Set as it is: list vs PerAction | 323 vs 321 | −0.5 | 292 vs 268 | **−6.2** |
| Set as it is: list vs PerTarget | 322 vs 334 | +3.1 | 291 vs 274 | **−4.4** |
| Restricted pairs: list vs PerAction | 316 vs 313 | −0.8 | 290 vs 284 | −1.5 |
| Restricted pairs: list vs PerTarget | 317 vs 333 | **+4.1** | 286 vs 271 | **−3.9** |
| Restricted pairs: list vs paired schema (no prompt change) | 319 vs 319 | 0.0 | 287 vs 291 | +1.0 |

Bold is beyond noise.

- **The 4B with PerTarget and restricted pairs:** impossible requests +10.9, events +9.2 and calm
  scenes +15, all beyond noise. It fixed 27 answers and broke 11.
- **The 2B with PerTarget:**
  - **Set as it is:** simple commands fell 20.0, 14 "stay here" lines going from `stop_following`
    to `none`. That is the action the idle example also cost it (9 November).
  - **Restricted pairs:** calm scenes fell 22.5, beyond noise, as it walked to whatever place the
    scene named ("The locked cave is silent." → `move_to locked_cave`).
- **PerAction** cost the 2B on simple commands and conversation, and helped neither model anywhere
  beyond noise.
- **The paired schema** fixed 4 answers on the 2B and broke none. On the 4B it fixed none and broke
  none.

**Illegal pairs the model chose under restricted pairs**, before any guard, list → B:

| Arm B | `qwen3.5:4b` | `qwen3.5:2b` |
|---|---|---|
| PerAction | 6 → 0 | 20 → 2 |
| PerTarget | 6 → 1 | 20 → 3 |
| Paired schema | 6 → 0 | 20 → 0 |

On the 2B, 15 of the list arm's 20 illegal pairs named a person. With restricted pairs,
`TargetCategoryGuard` fired 10 times in the list arm and 0 to 2 times in the others.

**Prompt length**, mean prompt tokens:

| | List | PerAction | PerTarget | Paired schema |
|---|---:|---:|---:|---:|
| Set as it is | 459 | 489 | 597 | — |
| Restricted pairs | 544 | 572 | 593 | 544 |

**Curated 76, as it is.** Within noise everywhere.

| Arm | `qwen3.5:4b` | `qwen3.5:2b` |
|---|---:|---:|
| List | 70 | 57 |
| PerAction | 68 | 57 |
| PerTarget | 70 | 57 |

**What was decided.** The default stays `Off`.

- **PerAction** was deleted. The enum stays, with `Off` and `PerTarget`, so another rendering can be
  added.
- **PerTarget** stays as a documented opt-in for larger models: do not use it with a 2B-class model.
- **To keep any model off illegal pairs,** the docs point to `PairTargetsWithActions`. It took them
  to 0 on both models, broke nothing and costs no tokens.

Removing PerAction left PerTarget's code path unchanged, so its requests are the ones measured here.
This also answers the live-runs row "`PairTargetsWithActions` on vs off": it changes no score
(0.0 and +1.0) and removes the illegal pairs.

Beside it, `DecisionTelemetry.TargetsUnlisted` now reports the targets the list's
`MaxListedTargets` cut, which was silent before.

**What would change this:**

- A real game with restricted pairs, to see whether the 4B's gain holds outside the legality arm.
- #147's curated set, should it come with categories.
- A rendering the 2B reads without losing `stop_following`.

### The plain action list over the defaults: +8.2 points on `qwen3.5:2b`, −1.5 on `qwen3.5:4b`, on the interim set (10 November 2026)

**Not this task's question:** a side result of the runs above, recorded as input to the defaults
pass. No default was changed for it.

Each model's first run set the runner defaults, which send no action and target list
(`ListActionsAndTargets` off), against the list on. The setup is the entry above's, on the interim
set as it is (no legality).

| Model | Defaults | List | Δ |
|---|---:|---:|---:|
| `qwen3.5:2b` | 260/389 | 292/389 | **+8.2** |
| `qwen3.5:4b` | 329/389 | 323/389 | −1.5 |

- **The 2B, beyond noise:** simple commands +10.8, impossible requests +9.4 and situations +16.9.
  The list fixed 49 answers and broke 17. It also cut `TargetGroundingGuard`'s refusals from 35 to
  14.
- **The 4B, both beyond noise:** the list cost impossible requests 9.4 points and gained
  situations 12.3, and the two nearly cancel.
- **Curated 76:** within noise on both (2B 54 against 57, 4B 69 against 70), as on 9 November
  (58 against 56).
- **Cost:** the list adds about 90 prompt tokens (368 → 459).

The 9 November situations entry measured the list at 323 against 330 on the 4B, consistent with
this. That entry had no 2B arm.

### GreyBoxStrategy: the framework's legal-target listing does not replace the game's moves line, and the paired schema takes illegal answers to none at no cost (10 November 2026)

**One game, small n.** Game-side findings (#20, #151, #244 item 5), 30 games an arm. Read
decision shares as fairly solid and outcomes as weak.

**What was run.** `Demos/GreyBoxStrategy` with a framework `Agent` in one seat against four scripted
countries, as stage 2 (the entry above): seeds 1 to 30 in every arm, seats rotating, arms one
after another in Unity batch mode, from 22:17 to 23:24. RTX 5080, Docker Ollama 0.40.14, over the
home network, one model resident, temperature 0, `qwen3.5:4b` then `qwen3.5:2b`. The seat's
per-target check (`DelegateActionHandler.IsAvailableFor`, built from the game's `Legality.WhyNot`)
is on in every arm. The control (A) is stage 2's: the game-side line `Moves open to us: attack
fenmark or westmarch; ...` among the standing facts, a flat schema, grounding on. B drops the line
for `DecideOptions.LegalTargets = PerTarget`; C adds `PairTargetsWithActions` to B; D drops the
line and lists nothing. Four more arms were added to explain them. The framework changed since
stage 2's runs (#245 among others), so A was run again here rather than taken from that entry.

| Model, arm | Provinces | Attack share | Attacks landed a game | Illegal answers | Contained (legal / legal attacks) | Prompt tokens |
|---|---:|---:|---:|---:|---:|---:|
| 4b A, the moves line | 2.0 | 23% | 2.8 | 30 | 86 (56 / 30) | 975 |
| 4b A, run again | 1.9 | 22% | 3.0 | 26 | 71 (45 / 30) | 966 |
| 4b B, `PerTarget`, no line | 1.0 | 15% | 1.6 | 17 | 29 (12 / 7) | 621 |
| 4b C, B paired | 1.6 | 17% | 1.9 | 0 | 17 (17 / 12) | 620 |
| 4b D, checks only | 1.1 | 2% | 0.1 | 81 | 98 (18 / 13) | 928 |
| 4b the line and `PerTarget` | 1.4 | 22% | 2.3 | 10 | 31 (21 / 11) | 660 |
| 4b the line, paired | 2.6 | 22% | 3.0 | 0 | 72 (72 / 39) | 972 |
| 4b the line, paired, grounding off | 2.5 | 33% | 3.8 | 0 | 0 | 965 |
| 4b C, grounding off | 1.8 | 19% | 2.1 | 0 | 0 | 620 |
| 2b A | 1.1 | 10% | 1.1 | 62 | 132 (70 / 23) | 956 |
| 2b C | 0.5 | 7% | 0.8 | 0 | 109 (109 / 44) | 619 |
| 2b the line, paired | 1.0 | 12% | 1.3 | 0 | 94 (94 / 28) | 953 |

No model won a game except one, in the 4B's line-paired-grounding-off arm. Illegal answers are
council answers before any guard that the game did not offer. Median latency was 0.59 to 0.65 s a
decision, with no pipeline errors. Arms compare seed by seed: the paired 95% band on mean provinces
is about ±1.1 to ±1.5 on the 4B and ±0.7 on the 2B, and no province difference above is beyond it.
Attack shares rest on about 480 council moves an arm, where 5 points is about two standard errors;
moves within a game are not independent, so 5 to 8 points is likely, not proven. A's rerun differed
by 0.03 provinces and 0.3 points.

**What it shows.**

- **`PerTarget` does not replace a game's own moves line.** In its place the 4B attacks less: 15%
  against 23% (z −3.0), and 17% with pairing (z −2.4). The gap is on turns 1 to 4, where A attacks
  in 57 of 120 council moves and B and C in 21. On the 2B, C ends with 0.5 provinces to A's 1.1,
  inside the band. The eval-set gain for `PerTarget` on the 4B (+4.1, the entry above this one) does
  not carry over into this game's attack behaviour.
- **What helps is a sentence among the facts, not the pairs as such.** The line with `PerTarget`
  beside it keeps the attack share at 22%, so the listing does not suppress attacks; it just does
  not prompt them. The listing shrinks the prompt by about 35% (975 → 620 tokens), listing the 5 to
  10 targets some move can take instead of all 29.
- **The paired schema is free here.** Illegal answers fall to 0 on both models (30 and 62 before),
  with attack share and provinces unchanged within noise. In B, 15 of the 17 illegal answers named
  a country at war with the seat as `attack`'s target; its list had that country `(for offer_peace)`.
- **The grounding guard's legal containments are the largest loss left (gap N4).** With pairing,
  every containment is on a legal answer: 72 in the 4B's line-paired arm, 39 of them attacks.
  Grounding off raises the attack share from 22% to 33% (z 3.8) and attacks landed from 3.0 to 3.8
  a game; provinces stay within noise.

**What was decided.** The demo keeps its moves line and turns the paired schema on by default
(`ModelSeatOptions.Default`). No framework default changes. A new framework gap goes in the demo's
`README.md` (N8): an option to send the legal moves as an observation line, worded by action, which
would need its own A/B on the evaluation set.

**What would change this.** 100 games an arm or more, to see the outcome effects that 30 cannot;
a second game, to tell this game's verbs from the general point; and an A/B of a framework-written
moves line (N8) against the game's own.

### The grounding guard and many legal targets: 20 alternatives a token cut its legal containments by 74% and 38% at no cost to correct answers or impossible requests; a lower threshold frees the rest but lets a small model guess (11 November 2026)

**The question.** In GreyBoxStrategy the grounding guard (`TargetGroundingGuard`, DR-016) contained
13% of `qwen3.5:4b`'s decisions and 21% of `qwen3.5:2b`'s, every one a legal answer once the schema
was paired (#244 item 1, the demo's gap N4; the entry of 10 November above). Why, and what fixes it
without letting a model act on a target it invented?

**Why it fires.** The guard's share already counts every listed target, not only the chosen one,
so a model torn between three legal provinces keeps that probability on the list. Scoring only the
targets the chosen action may legally take would count a subset, so it could only contain more.
What the guard cannot count is probability the provider did not report: Ollama returned the five
likeliest alternatives a token, and the guard counts the rest as off the list, as DR-016's probe
calibrated it. Of the 4B's 72 containments in 30 games, 39 had every reported alternative on the
list; for the 2B, 66 of 94. The rest had some reported mass on a string that continues no id.

**What was run.** Two settings, both new (`OllamaSettings.TokenAlternatives`, sent as
`top_logprobs`, and `DecideOptions.TargetGroundingThreshold`), and four scorings of the same
replies. RTX 5080, Docker Ollama 0.40.14 over the home network, one model resident, temperature 0,
`qwen3.5:4b` then `qwen3.5:2b`, the evaluation runs and the first ten game arms between 06:42 and
07:56, the combined game arm from 08:24 to 08:36.

- Evaluation sets: `EvalRunner`, interleaved, control (five alternatives, 0.8) against twenty
  alternatives at 0.8, on the interim 389 and the curated 76. Every reply was identical between
  the two arms on all 465 prompts on both models, so the replies were dumped with all twenty
  alternatives and every other variant scored on them offline, as DR-016's checks were: a port of
  `TargetGrounding.Read` that matched the guard's recorded share on every answer. The variants:
  top 5 or top 20; the share over the listed targets or over the chosen action's legal ones (the
  eval-only legality of #246: `move_to` takes places, `attack_target` the wolf); the share as is,
  or divided by the reported mass ("renormalised", the unreported rest counted as unknown); and
  thresholds from 0.8 to 0.3.
- GreyBoxStrategy: 30 games an arm, seeds 1 to 30, on the demo's `default` arm (the moves line and
  the paired schema), with every reply's alternatives dumped by a scratch wrapper. `default`
  reproduced #247's line-paired arm exactly (72 contained, 39 attacks, 2.6 provinces).

**Evaluation sets.** Correct · contained when the label was `none` · refused when an action was
expected · wrong · the guard's containments on right / wrong answers · impossible requests handled
(correct or contained, of 64) · impossible requests carried out on a guessed target:

| `qwen3.5:2b`, interim 389 | Correct | Contained | Refused | Wrong | Contained right / wrong | Impossible handled | Guessed |
|---|---:|---:|---:|---:|---:|---:|---:|
| Control: top 5, 0.8 | 261 | 11 | 27 | 90 | 9 / 27 | 49 | 15 |
| Top 20, 0.8 (the live arm) | 261 | 11 | 24 | 93 | 9 / 24 | 49 | 15 |
| Top 5, 0.6 | 268 | 8 | 16 | 97 | 2 / 20 | 47 | 17 |
| Top 5, 0.5 | 268 | 7 | 14 | 100 | 2 / 17 | 47 | 17 |
| Top 20, 0.6 | 268 | 8 | 16 | 97 | 2 / 20 | 47 | 17 |
| Renormalised, top 5, 0.8 | 263 | 8 | 22 | 96 | 7 / 21 | 47 | 17 |
| Renormalised, top 20, 0.8 | 262 | 8 | 23 | 96 | 8 / 21 | 47 | 17 |
| Legal targets only, top 5, 0.8 | 260 | 20 | 30 | 79 | 10 / 38 | 53 | 11 |
| Guard off | 270 | 0 | 2 | 117 | 0 / 0 | 41 | 23 |

On `qwen3.5:4b` the guard fires 10 times in 389. Control 330 · 4 · 6 · 49 (contained 1 right, 7
wrong; 53 impossible handled, 11 guessed); top 20 at 0.8 330 · 4 · 5 · 50, the same impossibles;
top 5 at 0.6 331, 53 handled; at 0.5 331, 52 handled, 12 guessed; renormalised 331, 53 handled;
legal targets 330, 54 handled, 10 guessed; off 331, 50 handled, 14 guessed. On the curated 76 no
variant changes an answer (4B 69; 2B 54 with 9 of 9 impossible handled; off 8 of 9). Every
difference is within the 3.6-point floor at n = 389. The sets hold few open choices: 5 of 389
prompts and 3 of 76 accept more than one target.

**Cost of twenty.** No measurable latency, interleaved in one session: median 0.47 → 0.43 s on the
4B, 0.38 → 0.37 s on the 2B. Each reply's token probabilities grow from about 5 KB to 17 KB as
JSON, in memory only: the decision log keeps the reply's text, and GreyBoxStrategy's log lines
stayed at about 5.4 KB.

**GreyBoxStrategy, 30 games an arm.** Provinces at the end · attack share of council moves (z
against `default`) · attacks landed a game · the guard's containments, all legal (of them attacks):

| Model, arm | Provinces | Attack share | Attacks landed | Contained (attacks) |
|---|---:|---:|---:|---:|
| 4b `default` | 2.6 | 22% | 3.0 | 72 (39) |
| 4b + 20 alternatives | 2.1 | 27% (z 1.9) | 3.2 | 19 (4) |
| 4b + threshold 0.6 | 2.5 | 31% (z 3.2) | 3.7 | 15 (12) |
| 4b + threshold 0.5 | 2.3 | 32% (z 3.6) | 3.7 | 3 (1) |
| 4b + 20 alternatives + 0.6 | 2.1 | 32% (z 3.5) | 3.6 | 3 (0) |
| 4b guard off | 2.5 | 33% (z 3.8) | 3.8 | 0 |
| 2b `default` | 1.0 | 12% | 1.3 | 94 (28) |
| 2b + 20 alternatives | 0.7 | 16% (z 1.7) | 1.7 | 58 (13) |
| 2b + threshold 0.6 | 0.9 | 18% (z 2.7) | 2.2 | 25 (10) |
| 2b + threshold 0.5 | 0.8 | 20% (z 3.3) | 2.3 | 8 (6) |
| 2b + 20 alternatives + 0.6 | 0.7 | 21% (z 3.7) | 2.3 | 3 (1) |
| 2b guard off | 0.5 | 21% (z 3.6) | 2.3 | 0 |

No province difference is beyond the paired 95% band (±0.9 to ±1.4 on the 4B, ±0.4 to ±0.7 on the
2B; the 2B's guard-off −0.47 sits just inside its ±0.49). No model won a game except one each in
the 4B's guard-off, two lower-threshold and combined arms.

**What the containments wanted.** Three, with the alternatives at the target's lowest token:

- `declare_war kesh`, share 0.789: `k` 0.365, `mer` 0.315, `kers` 0.072, `orsk` 0.058, `ors` 0.051,
  `mers` 0.025, then fragments (`or`, `ork`, `sk`, `ks`, `esk`, `gers`). A three-way legal spread
  with about 0.15 on strings that continue no id. Most of what top 20 still contains is this shape;
  renormalised it reads 0.92.
- `declare_war orsk`, share 0.263: `val` 0.634. The model wanted to declare war on its own country,
  `valen`, which is not a target, and the mask wrote `orsk`. The guard working.
- `declare_war orsk`, share 0.000: `il` 0.564, then `no` and `none`. It wanted `ilvar`, not on this
  call's list. The guard working.

**What I believe now.**

- Twenty alternatives fix the provider half of the problem, at no cost to correct answers or to
  impossible requests. Their one cost: four wrong answers the guard had contained at five now
  pass (3 on the 2B, 1 on the 4B, each where an action was expected: `int_situation_053`,
  `int_event_038`, `int_event_058` and `int_situation_050`). Adopted as the default.
- Renormalising is not a cheap stand-in for twenty. It is more permissive (2 containments against
  19 in the 4B's games) and cost the 2B the same two impossible requests as a 0.6 threshold. Not
  shipped.
- Scoring only the legal targets is the stricter direction: on the 2B it handled 53 impossible
  requests against 49, for one more right answer contained. It needs an action's legal set, which
  here only the eval-only legality supplied. Not shipped; a lead for #138.
- A lower threshold frees legal spreads and costs a small model impossible requests: at 0.6 the
  2B guessed on 17 of 64 against 15. The default stays 0.8, and the threshold is a per-call setting
  for a game with many legal targets a turn. GreyBoxStrategy's `default` arm uses 0.6 with twenty
  alternatives: 3 legal containments in 30 games on either model, and the attack share of the
  guard off.

**For #138: what remains.** Real off-list mass inside a legal spread, the `kers` case, is still
contained at 0.8 with twenty alternatives: 19 of the 4B's decisions and 58 of the 2B's in 30 games.
The guard cannot tell a fragment that continues no id from a near miss without a list of names.
Leads: a reading that ignores alternatives below some probability or length; or the legal-set
reading above, for a game that declares its legal sets.

**What would change this.** An evaluation set with many prompts that accept several targets, where
the threshold trade could be measured on right answers rather than in a game; a second game; and a
provider that reports its alternatives differently (LLMUnity reports at most 10).

### GreyBoxStrategy: five models at one table. A persona explains 84% of a 4B seat's aggression, and makes the game livelier; the 2B copies its statement rule (11 November 2026)

**The question.** Asked by #20. Can the same game and the same model, with a different persona,
play measurably differently? And what do five model seats at one table look like against the
all-script and all-random tables?

**What was run.** `TableRunner` in `Demos/GreyBoxStrategy/`. Every council is one
`DecisionRound`. Five personas use only `AgentIdentity` (`Personality`, three `Knowledge` lines,
`StatementRules`). Each table played 20 games, seeds 1 to 20, with the same seat and persona
rotation. The machine was the RTX 5080 box (Docker Ollama 0.40.14, over the home network), at
temperature 0. Each table ran with exactly one model resident, except the mixed table, which had
both qwens. The other models were unloaded before each table started (`resident.txt` per run).

There are two sets, never compared with each other:

- **Before #248** (grounding threshold 0.8, five alternatives a token), 07:59 to 09:08:
  persona 4b and plain 4b.
- **After #248** (the demo's `default`: threshold 0.6, 20 alternatives), 09:09 to 10:52: persona
  4b, plain 4b, mixed (three 4b seats and two 2b, with personas), persona 2b, and persona
  gemma4:e2b-it-qat.

All-script and all-random tables on the same seeds are reference points. Each number gets a paired,
seed-by-seed 95% interval. A difference counts as **beyond** only when that interval excludes zero.

**Results, after #248.**

| | plain 4b | persona 4b | mixed | persona 2b |
|---|---:|---:|---:|---:|
| η² of aggression by persona (p) | 0.030 (0.60), placebo slots | 0.841 (<0.001) | 0.432 (<0.001) | 0.295 (<0.001) |
| ended by domination | 20% | 60% | 75% | 0% |
| battles a turn | 1.23 | 1.46 | 1.15 | 0.72 |
| turns with no change | 39% | 26% | 35% | 64% |
| words a statement | 37.8 | 13.6 | 13.5 / 14.1 | 15.5 |
| repeated word for word | 3% | 5% | 6% (4b) / 48% (2b) | 54% |

- **Persona against plain, 4b, paired by seed.** These differences are beyond: battles a turn
  +0.24 (+0.13 to +0.35), provinces changing hands a turn +0.21, quiet turns −13 points, domination
  +40 points, peace offered +4.9 a game, and 3.7 turns shorter. Wars declared, lead changes and
  peace made are within. The warmonger attacks with 60% of its council moves and offers no peace.
  The builder attacks with 5% and holds 37% of its turns.
- **Before #248** the same contrast held: η² 0.524 against 0.036, and peace offered beyond. But the
  guard contained 23% of persona decisions, almost all of them legal attacks, and 95 to 100% of
  games ran to the turn limit. Persona 4b before and after: domination 5% → 60% and battles +0.51 a
  turn, both beyond. The guard fix moved this game more than the persona did.
- **4b against 2b at the mixed table, paired by seed.** The 2B places 3.45 against 2.70 and ends
  with 2.7 provinces against 6.5. It holds 35% of its turns against 10%, and aims at the weaker 44%
  of the time against 74%. All of these are beyond. Of the 20 wins, 19 went to the 4B, which held
  three of the five seats.
- **The 2B copies its statement rule.** `PromptBuilder` writes the rules as
  `Your statement: <rules>`. The merchant's rule opens "A trader's voice: profit, price or a
  deal." The 2B returned that whole line as its statement 373 times in 582 at its own table, and
  the warmonger's line 76 times in 594. The 4B did it twice in 2,318.
- **Seconds a decision**, single-model tables only: 4b with personas 0.46 s median and 0.55 s
  90th percentile; 4b plain 0.65 / 0.85 s, with statements three times as long; 2b 0.35 / 0.45 s.
  No timing is given from the mixed table, where two models shared one server.
- **Memory resident:** 3.1 GB (4b), 2.4 GB (2b), 1.7 GB (gemma4:e2b-it-qat), 5.5 GB for the
  mixed table. All five seats share one loaded model.

- **A third model, gemma4:e2b-it-qat, with personas** (10:35 to 10:52): η² of aggression 0.713
  (p < 0.001), so its personas show nearly as much as the 4B's. It plays a slower game: 15% of
  games end by domination, and 46% of turns are quiet. Its statements are the shortest (9.5
  words), none copies the statement rule, 4% are repeated word for word and 7% contradict the move.
  Its attack win rate is the lowest (61%). Decisions take 0.30 s median and 0.35 s at the 90th
  percentile.

**What was not measured.** No persona was tuned. The persona texts were written once and run
as they were. 20 games is small, and only the differences marked beyond are claimed. Whether the
statements read like people is the three game accounts in the demo README, one reader's judgement.

**What it changes.** Personas through the identity are enough to give a 4B seat a character, so
the human player's default opponents should be 4B seats with personas. The 2B is not fit for a
persona seat until gap N9 is fixed; that fix is a prompt change, owed an A/B run. Gaps N9 and N10
are in the demo README.

**What would change this.** A second game; personas written by someone else; and gap N9's A/B run,
which may change the 2B's voice numbers entirely.

### How a statement rule is introduced: "in your own words" ends the 2B's copying at the game table with no accuracy cost on either model, but the 2B echoes more few-shot lines on the evaluation set (11 November 2026)

**The question.** Asked by #244 item 8, demo gap N9. The persona 2B at GreyBoxStrategy's table
returned its persona's whole statement rule as its statement: 64% of the merchant's statements
(entry above, same day). Does the line that introduces the rules (`Your statement: <rules>`) invite
that? If it does, does another wording stop it without costing correct answers?

**What was run.** `PromptOptions.StatementRulesWording` / `DecideOptions.StatementRulesWording` has
three values. Only the opening of the system prompt's rules line changes:

| Value | The line |
|---|---|
| `YourStatement` (default, control) | `Your statement: <rules>` |
| `HowItShouldRead` | `How your statement should read: <rules>` |
| `InYourOwnWords` | `Write your statement in your own words, following these rules: <rules>` |

- **Evaluation sets.** `EvalRunner`, interleaved, guards on. On the curated 76 and the interim 389,
  each arm carried the same rules through the new `EvalArm.StatementRules`: "A village guard's
  voice: plain and watchful. One sentence, under 20 words. If the action has a target, name it."
  Control ran against each wording on `qwen3.5:2b` and `qwen3.5:4b`, one model resident (checked
  with `ps` before and after each run). Rendered without a model, the arms differ only in that one
  line (76 of 76 prompts).
- **GreyBoxStrategy.** The `personas` table on the 2B, 20 games, seeds 1 to 20, three arms one
  after another: `default`, `default+how-it-reads`, `default+own-words`. Each difference is paired
  by seed with a 95% interval. "Copies its statement rule" is a new `Scorecard` number: the
  statement holds the whole rule, ignoring case and punctuation. "A sentence of its rule" is any
  one sentence of four words or more.
- RTX 5080 box, Docker Ollama 0.40.14, over the network, temperature 0, 14:50 to 16:57. All of it
  ran before the server's `OLLAMA_NUM_PARALLEL` change that evening.

**Results, evaluation sets.** Correct answers, then statements that copy the rule (whole / a
sentence):

| Run | Control | `HowItShouldRead` | `InYourOwnWords` |
|---|---:|---:|---:|
| curated 76, 4B | 67, 0 / 0 | 64 (−3.9, floor 8.1), 0 / 0 | 66 (−1.3), 0 / 0 |
| curated 76, 2B | 56, 1 copy | 56 (0.0), 0 | 58 (+2.6), 0 |
| interim 389, 4B | 327, 5 / 8 | 325 (−0.5, floor 3.6), 0 / 0 | 330 (+0.8), 0 / 0 |
| interim 389, 2B | 255, 16 / 17 | 255 (0.0), 2 / 2 | 254 (−0.3), 0 / 0 |

Every change in correct answers is within noise, on both models and both sets. Neither model loses
answers to either wording. Both wordings all but end rule copying on the evaluation set.

**The cost, on the 2B only.** The runner flags replies that repeat a few-shot example's stimulus.
On the interim set the 2B's flags rise from 8 (control) to 16 with `HowItShouldRead` and 24 with
`InYourOwnWords`; on the curated set from 1 to 5 with `InYourOwnWords`. The echoed lines are the
examples' own stimuli: "Strike the wolf." 9 times, "You can stay put now." 4, "Go and stand by the
eastern road." 4, "Fine day for it." 2. Rule copies plus echoes come to 24 in control, 18 with
`HowItShouldRead` and 24 with `InYourOwnWords`. So on the evaluation set the 2B moves its copying
from the rule to the examples rather than stopping it. The 4B echoes nothing in any arm.

**Results, the 2B at GreyBoxStrategy's table** (paired by seed, 20 games):

| Number | Control | `HowItShouldRead` | `InYourOwnWords` |
|---|---:|---:|---:|
| copies its statement rule, all seats | 15% | 7% (−8, **beyond**) | 0% (−15, **beyond**) |
| copies a sentence of its rule | 16% | 13% (within) | 0% (−16, **beyond**) |
| the merchant's statements copying its rule | 65% | 35% (−30, **beyond**) | 0% (−65, **beyond**) |
| words a statement | 15.5 | 14.3 (**beyond**) | 12.2 (**beyond**) |
| repeated word for word | 54% | 57% (within) | 47% (within) |
| names its target | 29% | 36% (within) | 38% (within) |
| η² of aggression by persona | 0.295 | 0.370 | 0.308 |
| battles a turn | 0.72 | 0.62 (**beyond**) | 0.67 (within) |
| peace offered a game | 6.30 | 2.25 (**beyond**) | 5.50 (within) |
| provinces changing hands a turn | 0.53 | 0.48 (within) | 0.47 (−0.06, **beyond**) |

The persona still changes the play under both wordings (η² p < 0.001 in all three). Illegal
answers stay at 0%. `HowItShouldRead` changes the game itself: fewer battles, far fewer peace
offers, and the merchant holds 13 points more of its turns. `InYourOwnWords` changes one liveliness
number, slightly (−0.06 provinces changing hands a turn, the interval's edge at −0.01).

**The 4B at the table.** The same `personas` table on `qwen3.5:4b`, 20 games, seeds 1 to 20,
`default` against `default+own-words`, one after another under one lock, one model resident,
22:23 to 23:03, after the server's `OLLAMA_NUM_PARALLEL` change (both arms). It is the check that
nothing gets worse on the 4B. Paired by seed, 95%:

| Number | Control | `InYourOwnWords` | |
|---|---:|---:|---|
| copies its statement rule / a sentence of it | 0% / 0% | 0% / 0% | — |
| words a statement | 13.6 | 13.8 | within |
| names its target | 72% | 78% (+6) | **beyond**, better |
| repeated word for word | 5% | 10% (+5) | **beyond**, worse |
| contradicts its move | 19% | 15% | within |
| statements given | 98% | 99% | within |
| illegal answers | 0% | 0% | — |
| η² of aggression / hold / peace by persona | 0.84 / 0.60 / 0.72 | 0.83 / 0.68 / 0.62 | — |
| battles a turn | 1.47 | 1.36 | within (edge, +0.00) |
| provinces changing hands a turn | 1.15 | 1.06 | within |
| peace offered a game | 8.05 | 9.10 | within |

No pipeline errors. The 4B never copied its rule, so there was nothing to fix. Every liveliness
number is within its interval, and the persona still shapes the play as strongly. One voice number
gets worse beyond noise: statements repeated word for word, 5% to 10%, still well under the 2B's
47 to 54%. One gets better: naming the target, 72% to 78%.

**Reading.**
- The wording of the rules line is what invites the copy. Saying "in your own words" ends it at the
  game table, and costs no correct answers on either model or either set.
- The 2B pays for it on the evaluation set by echoing more few-shot lines. That set's prompts look
  much like its examples; the game's councils do not.
- `HowItShouldRead` does half the job and moves the 2B's play.
- On the 4B it changes nothing that matters: no copies to remove, the game as lively, the persona
  as strong; statements repeat a little more often (5% to 10%) and name their target a little more.
- No default changes. A developer whose small model reads its rules out can set `InYourOwnWords`,
  and should watch for few-shot echoes when it does.

### The legal moves as an observation line: it ends illegal pairs, but costs correct answers on both models, beyond noise on the 2B (11 November 2026)

**The question.** Asked by #244 item 5, demo gap N8. GreyBoxStrategy writes its own sentence of
legal moves among its facts, "Moves open to you: ...". It works better there than the framework's
`PerTarget` annotations (entry of 10 November). Can the framework write that sentence itself, from
the same availability checks, so a game need not?

**What was built.** `LegalTargetListing.ObservationLine`, beside `Off` and `PerTarget`. One
sentence, grouped by action, in schema order: `Moves open to you: move_to tree_a, tree_b or well;
chop tree_a.` It sits among the observations, after the sources' lines and before the call's own,
wherever `ObservationPlacement` puts those. That is where the game's line stood. It comes from
`DecisionSchema.PairsOf` and the targets cap, like `PerTarget`. With `ListActionsAndTargets` on, the
action list stays plain. The paired schema and the guards are unchanged. Rendered without a model,
an `ObservationLine` arm differs from control only by that one line (389 of 389), and the schemas
are identical.

**What was run.** The interim 389 through `EvalRunner` with the eval-only legality `EvalArm.Legality`
(places, creatures, people; #246), interleaved, guards on. Two runs a model: control (the plain
list) against `ObservationLine`, then `PerTarget` against `ObservationLine`. Same machine and
window as the entry above.

| Run | A | `ObservationLine` | Δ (floor 3.6) | Fixed / broke | Illegal pairs, A → line | Prompt tokens, A → line |
|---|---:|---:|---:|---:|---:|---:|
| 4B, control | 335 | 321 | −3.6, on the floor | 6 / 20 | 9 → 0 | 435 → 476 |
| 4B, `PerTarget` | 333 | 320 | −3.3, within | 18 / 31 | 0 → 0 | 593 → 476 |
| 2B, control | 258 | 233 | **−6.4, beyond** | 17 / 42 | 23 → 10 | 435 → 476 |
| 2B, `PerTarget` | 271 | 245 | **−6.7, beyond** | 17 / 43 | 3 → 9 | 593 → 476 |

By category, on the 2B against control: impossible requests −23.4, conversation −10.9 and
situations −9.2, all beyond; simple commands +6.2, within. Most of the breaks are `none` turning
into `attack_target wolf` or a `move_to`. Read as a list of things to do, the line pulls the 2B
toward acting. On the 4B against `PerTarget`, situations −15.4 and events −9.2 are beyond.

**GreyBoxStrategy stage 2, on `qwen3.5:4b` only.** The lead's rule is that long game runs use the
4B only, so **the moves sentence in the game was measured on the 4B alone**; the 2B's run was
dropped. One model seat against four scripts, 30 games an arm, seeds 1 to 30, seats rotating. Two
arms one after another under one lock, one model resident, RTX 5080 box, Docker Ollama 0.40.14,
19:48 to 19:59, both after the server's `OLLAMA_NUM_PARALLEL` change. `default` is the game's own
line (`Moves open to us: attack fenmark, stonereach or westmarch; fortify ashby or brindle.`).
`default+no-moves-line+moves-sentence` drops it for the framework's
(`Moves open to you: fortify ashby or brindle; attack stonereach, fenmark or westmarch.`, schema
order). Paired schema and the demo's grounding settings in both. Intervals are paired by seed, 95%.

| Number | The game's line | The framework's sentence | Difference |
|---|---:|---:|---:|
| provinces at the end | 2.1 | 0.7 | −1.4, [−2.8, +0.0], the edge |
| knocked out | 19 of 30 | 24 of 30 | +5, within |
| attack share, all council moves | 32% (150 / 511) | 27% (110 / 436) | −5, z about −1.7, within |
| attack share, turns 1 to 4 | 45% (55 / 122) | 40% (48 / 121) | −5, within |
| attacks landed a game | 3.6 | 2.6 | −1.0 |
| illegal answers | 0 | 0 | — |
| contained by a guard | 3 | 3 | — |
| wins | 1 | 0 | — |
| prompt tokens | 965 | 968 | — |
| median latency | 0.60 s | 0.64 s | — |

No pipeline errors. Fewer decisions in the second arm (436 against 511) because the seat is knocked
out sooner.

**Reading.**
- On the evaluation set the sentence is no better than the plain list and no better than
  `PerTarget`: beyond noise worse on the 2B, at or near the floor on the 4B.
- It takes the 4B's illegal pairs to 0, as `PerTarget` does, and costs fewer tokens than
  `PerTarget`.
- The paired schema alone already takes illegal pairs to 0 (entry of 10 November), so that is no
  reason to use it.
- In the game, on the 4B, it does most of what the game's own line does, unlike `PerTarget`. The
  attack share stays within noise of the game's line (27% against 32%), where `PerTarget` in the
  line's place fell to 15% and no line at all to 2% (entries of 10 November). It still ends with
  fewer provinces, 0.7 against 2.1, at the edge of the band, so it is not shown to be as good.
- Two differences in the text may matter and were not separated: the framework's sentence follows
  schema order, so `fortify` comes before `attack`, and it says "you" where the game says "us".
- No default changes. GreyBoxStrategy keeps its own line. `ObservationLine` stays an option for a
  game that has no such sentence of its own; on a 2B-class model the evaluation set says not to.

### Parallel requests on the RTX 5080: Ollama refuses them for `qwen3.5`, and on a model that takes them they change the answers (11 November 2026)

**What.** `OLLAMA_NUM_PARALLEL=4` on the Docker server in `tools/ollama-server/` (Ollama 0.40.14,
RTX 5080), tried so two laptop sessions could share the card. 60 stimuli spread over the interim
set's six categories, in `framework_request.json`'s request shape (greedy, `num_ctx` 4096, the
schema, logprobs), sent one at a time and then two or four at once from the 5080 itself, through
the gateway. One run of each, no A/B: a check of what the setting does, not an accuracy result.

| Model | Slots | At once | 60 requests | Per request (median) | Against one slot, one at a time |
|---|---|---|---|---|---|
| `qwen3.5:4b` | 1 (refused 4) | 1 | 25.4 s | 0.42 s | |
| `qwen3.5:4b` | 1 (refused 4) | 2 | 24.3 s | 0.80 s | 60/60 replies identical |
| `qwen3:4b-instruct-2507` | 1 | 1 | 16.9 s | 0.28 s | baseline |
| `qwen3:4b-instruct-2507` | 1 | 2 | 16.3 s | 0.54 s | 55/60 replies identical, 60/60 decisions |
| `qwen3:4b-instruct-2507` | 4 | 1 | 16.8 s | 0.28 s | 60/60 replies identical |
| `qwen3:4b-instruct-2507` | 4 | 2 | 12.7 s | 0.42 s | 47/60 replies identical, 58/60 decisions |
| `qwen3:4b-instruct-2507` | 4 | 4 | 10.9 s | 0.72 s | 47/60 replies identical, 58/60 decisions |

**`qwen3.5` takes no parallel requests on Ollama 0.40.14.** Every load of the 2B and the 4B logged
`model architecture does not currently support parallel requests architecture=qwen35` and started
llama.cpp with one slot (`-np 1`), so two clients take turns: no gain in throughput, and each
request's time doubles with the wait behind the other.

**On a model that takes them, parallel is faster and changes the answers.** With four slots,
`qwen3:4b-instruct` served 1.33 times as many requests a second two at a time and 1.55 times four
at a time, but 13 of 60 replies changed, and two decisions turned into `none`
(`int_event_007` from `move_to campfire`, `int_simple_033` from `follow_player`). First-token
log-probabilities moved by up to 2.4, which is what the grounding guard reads. Four slots with one
request at a time changed nothing, so it is batching requests together that moves them.

**Two clients at once move a reply even with one slot.** On `qwen3:4b-instruct`, two at once with
one slot, where they queue rather than batch, changed the wording of 5 of 60 replies and a
first-token log-probability by up to 2.2, with every decision the same. `qwen3.5:4b` did not move
in the same test. The cause, most likely prompt-cache reuse in a different order, was not
separated.

**So.** `OLLAMA_NUM_PARALLEL` stays unset on the Docker server. A measured run is the only client on
the server, which is what the 5080's lock already asks: a result from two sessions at once is not
comparable with one from a single session, in accuracy as well as latency. Request 001's sweep ran
before the setting was added, with one slot.

**What would change this.** An Ollama version that runs `qwen35` in parallel, and an A/B of a whole
set with and without batching on such a model, to see whether the changed replies cost accuracy
or only move it. Raw data: `.scratch/ollama-parallel/` in the lead's RTX 5080 checkout.

### What a remembered decision holds: leaving the statement out of memory makes the 4B's statements fit their moves worse, and fixes nothing it was meant to (11 November 2026)

**What.** `RollingHistory.Remembers` (#244 item 4, #216 item 3, PR #251) in GreyBoxStrategy's
`personas` table: five `qwen3.5:4b` seats with personas, seeds 1 to 20, seats and personas
rotating. Three arms, each the demo's `default` seat (moves line, paired schema, grounding 0.6):
**whole**, the control and today's history; **move**, `+memory-move`, each remembered decision
without its statement; **outcome**, `+memory-outcome`, without its statement and with the turn
report's line for the move (`"took fenmark from orsk (34 against 21); lost 6"`, attached by
`CountryAgent.Learn`). Arms interleaved in four blocks of five seeds, the order rotating block to
block. RTX 5080, Docker Ollama 0.40.14 over the home network, `OLLAMA_NUM_PARALLEL` unset, one model
resident, one client on the server, temperature 0. 18:22 to 19:31, 3,957 s of games. A difference is
called beyond noise only when `compare.py`'s 95% interval, paired by seed, excludes 0. **`qwen3.5:2b`
was not measured** (the lead: long game runs on the 4B only).

**Why.** Gap N7: in stage 2 the 4B repeated its own remembered sentences for turns on end, and #249's
table showed grievances that never happened ("they will bleed for taking our land back"). The guess
was that remembering the move, and what came of it, would stop both.

| Per game, mean (sd between games) | whole | move | outcome |
|---|---:|---:|---:|
| repeated word for word | 5% (4%) | 6% (3%), +0 within | 7% (2%), +1 within |
| claims land taken | 2% (1%) | 3% (2%), +1 within | 2% (1%), −0 within |
| invented grievance | 1% (1%) | 2% (2%), +0 within | 1% (1%), −0 within |
| names its target | 72% (8%) | 33% (5%), **−39, −44 to −35** | 43% (6%), **−29, −34 to −25** |
| contradicts its move | 19% (7%) | 42% (9%), **+23, +18 to +28** | 26% (9%), **+8, +3 to +13** |
| copies a sentence of its rule | 0% | 1%, **+1, +0 to +1** | 5% (3%), **+5, +4 to +7** |
| copies its whole statement rule | 0% | 0%, within | 1% (2%), **+1, +1 to +2** |
| words a statement | 13.6 | 12.2, **−1.4** | 11.2, **−2.4** |
| statements given | 98% | 99%, within | 98%, within |
| battles a turn | 1.47 | 1.27, **−0.20, −0.30 to −0.09** | 1.56, within |
| provinces changing hands a turn | 1.15 | 0.98, **−0.18, −0.27 to −0.08** | 1.20, within |
| refused at resolution | 6% | 4%, **−1, −3 to −0** | 7%, within |

Pooled over the 20 games: 2,318, 2,594 and 2,413 statements; 124, 147 and 159 repeated word for word
(5.3%, 5.7%, 6.6%); 57, 84 and 57 claims of land taken; 32, 46 and 31 invented grievances (1.4%, 1.8%,
1.3%). Every other liveliness and sense number (turns, endings, wars, peace offered and made, land
taken from a country, quiet turns, lead changes, eliminations, Gini, idle share, illegal answers,
containments, attack win rate, aiming at the weaker, answering an attack, accepting peace) is within
noise in both arms.

**The problems were already small under today's default.** With personas, the paired schema and the
moves line, 5% of the control's statements repeat an earlier one and 1.4% invent a grievance. Stage
2's runs of repetition came from a different seat (no persona, the flat schema). Neither number
moves in either arm.

**The remembered statements were doing a job.** Without them, statements name the move's target
half as often (72% to 33%) and name someone else more than twice as often (19% to 42%). The
persona's rule says "if the move has a target, name it"; the model's own earlier answers, each
naming its target, were the examples it followed. With them gone, the 4B also starts reading its
rule out as its statement (`"A trader's voice: profit, price or a deal."`), which #249 saw only on
the 2B (gap N9): 5% of statements copy a sentence of the rule under **outcome**. The outcome line
recovers part of the naming (43% against 33%), most likely because its text names the target
(`took fenmark ...`), and leaves the game as lively as the control. **Move** also makes the game a
little quieter (fewer battles and conquests a turn, beyond noise).

**So.** Neither setting becomes a default, for any game. `Whole` stays. The option stays in the
package, off: the outcome line is a sound way to give a game's results to memory, and a game
whose statements carry no rule, or a strategy that keeps one remembered statement for its voice,
may read this differently. GreyBoxStrategy keeps the whole decision.

**What would change this.** A run where the statement is remembered *and* the outcome is added
(a fourth setting, not built), which would keep the examples and add the truth; the 2B, which
copies more and may be helped or hurt more; and a grievance measure that also reads paraphrases.
Each remembered stimulus already carries the next turn's facts ("Our attack on ashby failed"), so
this game always remembered part of each outcome. Raw data: `.scratch/244-memory-outcomes/runs/full-4b/`
in the lead's laptop checkout (worktree `wonderful-kilby-ef674e`).

### A storyteller in GreyBoxStrategy: twice the kinds of event, games that diverge, no cost to the game; it leans against the leader, and the 4B leaves out half its titles (12 November 2026)

**What.** Stage S1 of the storyteller (`docs/demos/painted-realm-research.md`, section 1, design A),
in the demo only: one more `Agent` with no body, asked once a turn before upkeep from turn 3, whose
actions are eight effect templates and whose targets are the five countries, six groups and the held
provinces, with the paired schema and every fairness rule as per-target availability. The game sets
the strength, checks the story, applies the effect and tells the rulers the title (never the story).
It replaces the random event table. GreyBoxStrategy's `personas` table, five `qwen3.5:4b` seats,
seeds 1 to 20, seats and personas rotating, the demo's `default` seat. Four arms interleaved in four
blocks of five seeds, the order rotating: **control** (`default`, the random table), **story A**
(`default+storyteller`), **story B** (`+inspiration-1`: other inspiration words, nothing else), and
the **control again**. RTX 5080, Docker Ollama 0.40.14 over the home network, one model resident,
one client, temperature 0, 12 November 00:38 to 02:03 (5,048 s). A difference is beyond noise only
when `compare.py`'s 95% interval, paired by seed, excludes 0. The 2B was not run (game runs are 4B
only).

**Why.** The lead wants the model to create events with bonuses and minuses for countries, to make
each run unique. The research page asked for this A/B before any framework work.

**Before the run, three fixes from two smoke games and a probe** (the same model and server, not
measurements): under a three-event template cooldown the 4B cycled plague, bad harvest, unrest and
volunteers for 21 turns, three blows to each gift, so the cooldown became four events with no third
blow or gift in a row; unrest was undone by the same upkeep's recovery and now blocks it; and the 4B
wrote no title in any of 62 events under two wordings of the statement rule. Replaying five logged
requests with three wordings: no title in 5 of 5 without an example, in 5 of 5 with
`Format: <Title>: <story>`, a title in 3 of 5 with an example (`as in "Title Of The Event: what
happened to whom."`), which the arm uses.

**The control ran twice and played the same 20 games move for move** (0% of provinces differ at
turns 10 and 20), so on this server the seeds are exact and every difference is the arm's.

| Per game, mean (sd) | control | story A | story B |
|---|---:|---:|---:|
| events a game | 25.1 (8.6) | 23.6 (4.3), within | 22.1 (4.4), within |
| event value a game, gold, unsigned | 527 (205) | 690 (127), **+163, +50 to +277** | 683 (186), **+156, +27 to +286** |
| gifts share | 31% | 42%, **+11, +5 to +16** | 42%, **+10, +5 to +16** |
| distinct templates a game | 3.95 | 6.65, **+2.70, +2.32 to +3.08** | 6.20, **+2.25, +1.88 to +2.62** |
| distinct template-target pairs a game | 14.1 | 16.7, **+2.55, +0.50 to +4.60** | 15.7, within |
| net value Gini | 0.54 | 0.64, **+0.10, +0.05 to +0.15** | 0.63, **+0.09, +0.04 to +0.14** |
| value tracks standing (r) | −0.15 | −0.37, **−0.22, −0.37 to −0.06** | −0.33, **−0.18, −0.32 to −0.04** |
| hit rulers naming the event | 21% | 29%, **+8, +1 to +15** | 31%, **+9, +2 to +16** |
| peace offered a game | 8.05 | 11.25, **+3.20, +0.10 to +6.30** | 10.95, **+2.90, +0.06 to +5.74** |
| final map Gini | 0.54 | 0.53, within | 0.58, **+0.05, +0.01 to +0.08** |
| words a statement | 13.6 | 12.7, **−0.92, −1.21 to −0.63** | 12.7, **−0.93, −1.23 to −0.62** |
| idle share (all seats) | 10% | 13%, within | 15%, **+5, +0 to +9** |
| aims at the weaker | 63% | 66%, within | 69%, **+6, +1 to +11** |

Within noise in both storyteller arms: turns, endings, wars, peace made, battles and provinces
changing hands a turn, land taken from a country, quiet turns, lead changes, eliminations; illegal
answers, containments, refusals, attack win rate, answering an attack, accepting peace; statements
given, repeated word for word, names its target (72% against 66% and 66%), contradicts its move (19%
against 24%), claims of land taken; invented grievances fell slightly (beyond, under 1 point). η² of
aggression by persona: 0.84, 0.81, 0.82. Warmonger wins: 13, 12, 15 of 20.

*Event value* counts soldiers at 2 gold, stability at 20, a fort at 30. *Net value Gini* is the mean
absolute difference over twice the mean absolute value, so blows count. *Value tracks standing* is,
within a game, the correlation between what an event was worth to a country and its provinces then
against the living mean. *Hit rulers naming the event*: of model rulers hit, those whose statement
on that turn's council holds a word of four letters or more from the title, or a word of the kind
(harvest, plague, fever...); the table's events have no title, so the control is scored on the kind
alone, which favours it.

**The storyteller's own numbers** (story A / story B): 473 / 444 calls; no illegal answer; no
containment; `none` 0.4% / 0.7%; one `UnreadableReply` each after about 20 s; no event refused when
it landed; median 0.69 s a call on about 1,340 prompt tokens (the rulers' median stayed 0.45 s).
Stories replaced by the game's check 4.9% / 3.4% (16 / 10 for a digit, 7 / 5 for naming a country the
event did not reach). No title 45% / 44%. Titles not repeated within their game 59% / 65%; titles
that also appear in another game 72% / 77%. A story shares 18% / 16% of its two-word phrases with the
game's earlier stories. Targets: 340 / 293 countries, 111 / 120 groups (`the_leader` 56 / 61), 20 /
28 provinces. Template mix, story A: bad harvest 98, volunteers 92, unrest 91, plague 83, zeal 51,
windfall 36, stronghold 18, walls crumble 2.

**Divergence**, story A against story B, the same seeds: another owner in 22% (sd 13) of provinces at
turn 10 and 45% (sd 18) at turn 20; 19 and 20 of the 20 games differ. The control against its rerun:
0% and 0%.

**Fairness, by persona** (net event value over the 20 games): the builder, usually the weakest seat,
−400 under the table, +461 and +468 under the storyteller; the warmonger, who wins most games, −740
against −1,467 and −1,431.

**Two personas change, in both storyteller runs:** the builder holds 39% of its council moves under
the table and 61% and 71% under the storyteller (beyond), almost all on turns no event reached it
(65% and 78% against 42%); the opportunist offers peace in 15% against 27% and 37% (beyond). With
five personas and about twenty numbers each, single "beyond" results are expected by chance; these
two are each beyond in both runs. A likely reading for the builder: gifts fill a treasury its
persona prefers to keep.

**50 events labelled by one reader** (this session's author, not blind to the arm, model statements
before the check; `.scratch/storyteller-s1/labels.md`): the right sign 48, the right country 47
(none named 3, a wrong one 0), an invented consequence 3, a wrong magnitude 2, flat, reading like
the effect's own description, 6.

**So.** The storyteller does what the research page hoped on every measure from the logs: varied
within a game, divergent between runs of a seed, noticed by the rulers, with no measured cost to the
game's liveliness or the rulers' sense, at 0.7 s a turn. It is not neutral: it hits the leader and
helps the weak, and it changes how the builder and the opportunist play. Its titles are the weak part:
half missing, the rest often the inspiration words or a title copied from its own "told so far" line
("The Iron Hermit's Curse strikes again" nine times in one game). It stays an arm, off in `default`;
S2 starts with the titles.

**What would change this.** A blind read by people (the research page's fun test, not run); the
titles fixed and re-run; the 2B; a storyteller persona, or a storyteller on its own provider at a
higher temperature (gap 7). Raw data: `.scratch/storyteller-s1/runs/ab/` in the short-path worktree
`C:\Unity_Projects\wt-st` on the lead's laptop (smoke games, the title probe and the labels beside it).
