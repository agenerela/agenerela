# Live runs to do

Checks that need a real model on a real GPU, so they can't run in CI or on a laptop on battery.
This is a working list: add a row when something new needs a live check, and move it to **Done**
with a link to its `findings.md` entry once it has run. Kept since 9 November 2026, at the lead's
request.

**Before any run:**
- Read [`llm-wiki/measuring.md`](llm-wiki/measuring.md).
- Take the lock for the machine you run on, from the lead's laptop: `.scratch/ollama-5080-lock/`
  for the 5080 box, `.scratch/ollama-lock/` for the laptop's own Ollama. Each folder's README has
  the commands.
- Make sure only the model under test is loaded, and the machine is on mains power.
- Record the Ollama version and `size_vram` with the result.

**Two machines:**
- **Laptop:** the lead's RTX 4060 Laptop, 8 GB, plugged in.
- **5080 box:** the RTX 5080 machine, 16 GB, that also runs the asset server. It covers the 12 and
  16 GB tiers in [`llm-wiki/model-candidates.md`](llm-wiki/model-candidates.md).

Since 9 November the **5080 box is the default** for runs started from the laptop, through
[`tools/ollama-server/`](../tools/ollama-server/AGENT_USAGE.md). The laptop's own Ollama is used when
the 5080 is unreachable, or when the lead asks for it.

**A run for another machine, or for later, is written as a request** in
[`run-requests/`](run-requests/README.md): what to run, step by step, and a results file to fill
in. Its row here links it.

An **A/B run** compares two arms on the evaluation set through the runner (#145), in one session
(hard rule 2). The formal list of A/B runs owed is #151. This page adds the order to run them
in, the machine, and the live checks that aren't A/B runs.

## Ready to run now

| Run | Kind | Machine | What it checks | From |
|---|---|---|---|---|
| The 12 and 16 GB tiers | A/B, sequential arms | 5080 box | A "high-end GPU" option, such as Granite 4.1 8B, Qwen3.5-9B or Gemma 4 12B, against the 2B | `model-candidates.md`. Not run with request 001: the lead judged it not needed (9 November) |

## Waiting for something first

| Run | Kind | Waiting for | What it checks | From |
|---|---|---|---|---|
| Example statements, written vs `ExampleStatements = false` | A/B, interleaved, one model, on GreyBoxVillage's guard (`Assets/Evaluation/VillageGuard.asset`); laptop on mains power | Statements written on `VillageGuard.asset`: an `ExampleStatement` on each of its four actions and its `IdleExampleStatement`, none matching a prompt (`PromptSetChecks` refuses one that does) | Do statements stop the model echoing an example's stimulus, without it parroting the statement instead? **Arm A:** `DecideOptions.ExampleStatements = false`, which is today's prompt byte for byte. **Arm B:** the defaults, so the written statements are shown. Count accuracy **and** replies that repeat any example line, a stimulus or a statement. The runner counts accuracy, not echoes, so match each answer's `modelStatement` in `report.json` against every example line with `PromptSetChecks.MatchesExample`. It helps only if echoes fall and accuracy does not | #190 G14, #151 |
| `PairTargetsWithActions` on vs off | A/B | Categories on the evaluation set's targets and profile actions | Does pairing each action with its own targets help? | #210, #151 |
| The example-target fallback where targets have categories | A/B, interleaved, both models, interim set, 5080 box | Categories on the evaluation set's targets and profile actions, as for `PairTargetsWithActions` | Without categories the fallback names "Strike the Bridge." and impossible requests fall about 30 points; does it hold up where an attack can only name a creature? Arms: `ExampleTargetFallback` off and on, with the preferred lists emptied in both | #190 LV #20, #151 |
| Grounding threshold, calibrated | A/B over thresholds | A per-arm threshold setting, and #147's 200+ prompts | The 0.8 came from 130 prompts one person wrote | #151, DR-016 |
| A few-shot example of a free choice | A/B | #147's open-choice prompts | The 2B answered `none` to 6 of 8 open choices | #151 |
| History on vs off, and window sizes | A/B | #147's multi-turn cases | Does memory help or hurt a 2B? | #151 |
| `statement` vs `dialogue` | A/B | A per-call field name | Did the rename change accuracy? | #151, DR-008 |
| The one-line target rule's wording | A/B | Running Ollama without probabilities | Is #107's wording any good? | #151 |
| Templated vs summarised observations | A/B | A summarising observation source | Which form a 2B reads better | #151 |
| The Phase 4 gate's live A/B | A/B over the whole set | #147 and #148 | Guards on against guards off, on 200+ prompts | #150 |

## Done

| Run | Result | Where |
|---|---|---|
| Phase 3 gate, 20 prompts on `qwen3.5:2b` | 19/20 guards on, 18/20 off | `findings.md`, 3 November |
| `qwen3.5:2b` vs `Qwen3-4B-Instruct-2507`, first sequential run | 56/76 against 65/76, not a clean model result, since the 4B's field order was sorted | `findings.md`, 5 November |
| #198's model list, warm-up and unload | Passed | `findings.md`, 6 November |
| #210's paired-schema grammar check | No wrong pair from the paired schema. A grammar check, not accuracy | `findings.md`, 6 November |
| `Agent.Reset` with a request already at Ollama, `qwen3.5:2b` | 3 of 3: the call ended cancelled, Ollama cancelled the request mid-reply, and the next decision was normal with no queue wait | `findings.md`, 9 November |
| Whether Ollama keeps the field order | Set explicitly, `OLLAMA_GO_TEMPLATE=1` moved `Qwen3-4B-Instruct-2507` and `MiniCPM5-2B` onto `go_template`: 6 of 6 in order each, against 0 of 6 unset. It also changes the chat template | `findings.md`, 9 November |
| `ListActionsAndTargets` on vs off, `qwen3.5:2b` | 58/76 on against 56/76 off, within noise | `findings.md`, 9 November |
| Counter-examples on vs off, `qwen3.5:2b`, four lines written for the run | 56/76 both, within noise | `findings.md`, 9 November |
| Code-built idle example, set vs unset, `qwen3.5:2b` (a profile agent standing in) | 59/76 unset against 56/76 set, within noise overall; simple_command −33.3 beyond noise, `stop_following` commands lost to `none` | `findings.md`, 9 November |
| Idle example with vs without, `qwen3.5:2b`, the profile's own line | 56/76 with "Fine day for it." against 59/76 without, within noise overall; simple_command +33.3 without it, beyond noise, `stop_following` 2/7 with the line against 6/7 | `findings.md`, 9 November |
| `qwen3.5:2b` vs `qwen3.5:4b`, in the model sweep | 70/76 for the 4B against 56/76, +18.4, beyond noise. Separate runs on one server, not `Sequential` pairs | `findings.md`, 9 November |
| Policy lines as `AgentIdentity.Knowledge` vs as observations, and vs none, `qwen3.5:4b`, interim set, 5080 box | 335/389 as knowledge against 323/389 as observations (+3.1) and 330/389 with no lines (+1.3), both within noise; beyond noise only simple_command +9.2 against observations and impossible_request +15.6 against none. Situations and events about 70% in every arm | `findings.md`, 9 November, "Policy lines as knowledge or as observations" |
| The 8 GB shortlist and every other pulled model against `qwen3.5:2b`, 13 models | All kept the field order on `ollama serve` with `OLLAMA_GO_TEMPLATE=1`, none thought, all fit in VRAM. Five beat the 2B beyond noise: `qwen3.5:4b` 70, `qwen3:4b-instruct-2507`, `ministral-3:3b` and `qwen3:8b` 65, `gemma4:e2b-it-qat` 64; `lfm3:1.7b` fell below it at 46. Separate runs on one server, at n = 76 | `findings.md`, 9 November |
| Few-shot examples as chat turns vs the block (`ExamplesAsTurns`), interim set, guards on, RTX 5080 | `qwen3.5:2b` 277/389 turns against 263 block, +3.6, on the floor; `qwen3.5:4b` 291 against 330, −10.0, and `gemma4:e2b-it-qat` 264 against 290, −6.7, both beyond noise. 361 to 389 of 389 turns replies had an empty statement, copied from the examples. Again with written statements in both arms: 4B 290 against 332, −10.8, 2B 288 against 274, +3.6. Not adopted; the code is parked on `151-chat-turn-examples-parked` | `findings.md`, 9 November |
| Spaced example names (`SpacedExampleNames`) vs glued, interim set, guards on, RTX 5080, `move_to` naming `north_tower` in both arms | `qwen3.5:4b` 337/389 against 334, +0.8, and `qwen3.5:2b` 280 against 269, +2.8, both within noise; the 2B's player lines +4.6 beyond noise in its favour (simple_command +12.3). Nothing worse beyond noise. Adopted 9 November: the switch is gone and the spaced wording is what every agent sends | `findings.md`, 9 November, "How a few-shot example names its target" |
| The example-target fallback (`ExampleTargetFallback`) on vs off, interim set, RTX 5080, preferred lists emptied in both arms | `qwen3.5:4b` 317/389 against 243, +19.0, and `qwen3.5:2b` 273 against 214, +15.2, beyond noise, from command_with_target and situations and events; impossible_request −32.8 and −29.7, beyond noise, as "Strike the Bridge." taught attacking places. On the shipped evaluation agent the arms send identical prompts, so it was not run there | `findings.md`, 9 November, "How a few-shot example names its target" |
| Why situations and events go wrong, and three prompt-side variants, interim set, guards on, RTX 5080 | The 4B's 39 misses sorted by cause: 9 follow, 8 went to the place instead of the wolf, 5 passive, 4 stop-following, 4 arguable labels, 9 other. Folding a situation's own observations into its stimulus: `qwen3.5:4b` 352/389 against 330, +5.7, and `qwen3.5:2b` 278 against 261, +4.4, both beyond noise, all from situations and events. `ListActionsAndTargets` 323 (player lines −5.0 beyond noise) and one extra situation example 325, both within noise overall. No default changed | `findings.md`, 10 November, "Situations and events: the deciding fact sat in the observations" |
| The 13-model sweep on the interim set, 389 prompts, on the RTX 5080 (Docker Ollama 0.40.14, over the network) | `qwen3.5:4b` first at 330/389 against the 2B's 263/389, +17.2 beyond noise; the idle example helps 7 models beyond noise and costs only `qwen3.5:2b` (+4.6 without it, `stop_following` 22/48 against 39/48) | `findings.md`, 9 November; [Request 001](run-requests/001-interim-model-sweep-5080/results.md) |
