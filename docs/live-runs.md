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
| #244's prompt options in GreyBoxStrategy: stage 2, the game's moves line against `+no-moves-line+moves-sentence`, both models, 30 games an arm; and the persona 4b table, `default` against `default+own-words`, 20 games | Sequential arms, one model resident, 5080 box | The 5080 lock, after the server's `OLLAMA_NUM_PARALLEL` change; both arms of each run on the same side of it | Whether the framework's moves sentence replaces the game's (attack share, provinces, illegal answers), and that `InYourOwnWords` does nothing worse on the 4B | #244 items 5 and 8 |
| Example statements, written vs `ExampleStatements = false` | A/B, interleaved, one model, on GreyBoxVillage's guard (`Assets/Evaluation/VillageGuard.asset`); laptop on mains power | Statements written on `VillageGuard.asset`: an `ExampleStatement` on each of its four actions and its `IdleExampleStatement`, none matching a prompt (`PromptSetChecks` refuses one that does) | Do statements stop the model echoing an example's stimulus, without it parroting the statement instead? **Arm A:** `DecideOptions.ExampleStatements = false`, which is today's prompt byte for byte. **Arm B:** the defaults, so the written statements are shown. Count accuracy **and** replies that repeat any example line, a stimulus or a statement. The runner counts accuracy, not echoes, so match each answer's `modelStatement` in `report.json` against every example line with `PromptSetChecks.MatchesExample`. It helps only if echoes fall and accuracy does not | #190 G14, #151 |
| `ObservationPlacement.WithStimulus` as the default, on a demo game | A/B, interleaved, both models, on the game's own agent | A demo that keeps its standing facts in an `IObservationSource` and passes only a moment's own facts with the call | The interim gain (+5.4 and +5.7) came with the standing lines in a source; with the whole block moved it fell 8 and 10 points. Does a real game's split keep the gain? If so, flip the default | #151 |
| The example-target fallback where targets have categories | A/B, interleaved, both models, interim set, 5080 box | Possibly nothing now: `EvalArm.Legality` (#151) gives an arm categories on copies of the set's targets and actions; check first that the fallback reads them | Without categories the fallback names "Strike the Bridge." and impossible requests fall about 30 points; does it hold up where an attack can only name a creature? Arms: `ExampleTargetFallback` off and on, with the preferred lists emptied in both | #190 LV #20, #151 |
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
| How the statement rules are introduced (`StatementRulesWording`), #244 item 8: curated and interim sets, both models, interleaved; the persona 2b table, 20 games, RTX 5080 | No correct-answer change beyond noise on either model or set. At the 2B table `InYourOwnWords` took rule copies from 15% to none (merchant 65% to 0), `HowItShouldRead` to 7% and cut battles and peace offers; on the evaluation set the 2B echoed more few-shot lines (8 to 24 flags). No default changed | `findings.md`, 11 November, "How a statement rule is introduced" |
| The legal moves as one observation line (`LegalTargets = ObservationLine`), #244 item 5: interim set with `EvalArm.Legality`, against the plain list and `PerTarget`, both models, interleaved, RTX 5080 | Worse: 2B −6.4 and −6.7, beyond noise, most breaks `none` turned into an action; 4B −3.6 (on the floor) and −3.3. Illegal pairs to 0 on the 4B, as the paired schema already does. No default changed | `findings.md`, 11 November, "The legal moves as an observation line" |
| GreyBoxStrategy's table of models (#20): five model seats, persona against plain, mixed 4B and 2B, persona 2B and a third model, 20 games a table, before and after #248, RTX 5080 | A persona explains 84% of the 4B's seat-to-seat aggression (placebo 3%) and makes the game livelier, beyond the game-to-game spread; the 2B is weaker, holds more and copies its statement rule (gap N9); gemma4:e2b-it-qat's personas show nearly as much (η² 0.71) in a slower game with the cleanest voice; 4B with personas recommended as the human's opponents | `findings.md`, 11 November, "GreyBoxStrategy: five models at one table" |
| The grounding guard with many legal targets (#244 item 1): 5 against 20 alternatives a token, interim and curated sets, both models, interleaved, RTX 5080; threshold, renormalised and legal-set readings scored offline on the same replies; GreyBoxStrategy 30 games an arm on `default` | 20 alternatives left every reply on all 465 prompts the same on either model, and the correct answers and impossible requests handled, though 4 contained wrong answers now pass (3 on the 2B, 1 on the 4B); no measurable latency; and cut the game's legal containments 72 → 19 (4B) and 94 → 58 (2B): now the default. A 0.6 threshold frees the rest (with 20: 3 and 3, attack share as with the guard off) but on the 2B's interim set lets 17 of 64 impossible requests guess against 15: a per-call setting, 0.8 stays; GreyBoxStrategy's `default` uses 0.6. Renormalising and legal-set scoring not shipped | `findings.md`, 11 November, "The grounding guard and many legal targets" |
| `LegalTargets = PerTarget` in place of GreyBoxStrategy's moves line, both models, 30 games an arm, RTX 5080, arms sequential as stage 2 | It does not hold: without the line the 4B attacks 15% of the time against 23% (17% with pairing), provinces within noise; the 2B 0.5 against 1.1. The paired schema took illegal answers 30 → 0 and 62 → 0 at no measured cost and is the demo's default. Grounding off with pairing: attack share 22% → 33% (gap N4) | `findings.md`, 10 November, "GreyBoxStrategy: the framework's legal-target listing does not replace the game's moves line" |
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
| Where a call's own observations go (`DecideOptions.ObservationPlacement`), interim and curated sets, guards on, RTX 5080 | With the standing lines in an observation source, `WithStimulus` against `AfterSources`: interim `qwen3.5:4b` 351/389 against 330, +5.4, and `qwen3.5:2b` 283 against 261, +5.7, both beyond noise, almost all on situations and events, calm scenes no worse; curated 76 within noise on both (68 against 69, 54 against 54). `BeforeSources` 328 against 330, dropped. With the whole block as the call's own, as the demos send it: 4B 299 against 330, −8.0, and 2B 221 against 261, −10.3, player lines −15.4 on both. A following-policy line on top: 346 against 351. Default stays `AfterSources`; the flip waits on the demos using observation sources | `findings.md`, 10 November, "Where a call's own facts go" |
| `PairTargetsWithActions` on vs off, interim set with the eval-only legality (`EvalArm.Legality`), guards on, RTX 5080 | No score change: `qwen3.5:4b` 319/389 against 319, `qwen3.5:2b` 291 against 287. Illegal pairs the model chose 6 → 0 and 20 → 0, no answer broken, no extra prompt tokens. The way the docs give to keep a model off illegal pairs | `findings.md`, 10 November, "Saying which targets each action may take" |
| The legal pairs in the prompt (`DecideOptions.LegalTargets`), interim and curated sets, both models, guards on, RTX 5080 | With the eval-only legality, `PerTarget` against the plain list: 4B 333 against 317, +4.1, and 2B 271 against 286, −3.9, both beyond noise; on the set as it is 4B +3.1, 2B −4.4 beyond. `PerAction` helped neither (2B −6.2 beyond) and was removed. Curated 76 within noise. Neither set restricts any pairs as it ships. Default stays `Off`; `PerTarget` is an opt-in, not for a 2B-class model | `findings.md`, 10 November, "Saying which targets each action may take" |
| The plain action list vs the runner defaults, interim set, side result of the run above | `qwen3.5:2b` 292/389 against 260, +8.2 beyond noise; `qwen3.5:4b` 323 against 329, −1.5. Input to the defaults pass; no default changed | `findings.md`, 10 November, "The plain action list over the defaults" |
| The 13-model sweep on the interim set, 389 prompts, on the RTX 5080 (Docker Ollama 0.40.14, over the network) | `qwen3.5:4b` first at 330/389 against the 2B's 263/389, +17.2 beyond noise; the idle example helps 7 models beyond noise and costs only `qwen3.5:2b` (+4.6 without it, `stop_following` 22/48 against 39/48) | `findings.md`, 9 November; [Request 001](run-requests/001-interim-model-sweep-5080/results.md) |
