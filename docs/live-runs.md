# Live runs to do

Checks that need a real model on a real GPU, so they can't run in CI or on a laptop on battery.
This is a working list: add a row when something new needs a live check, and move it to **Done**
with a link to its `findings.md` entry once it has run. Kept since 9 November 2026, at the lead's
request.

**Before any run:**
- Read [`llm-wiki/measuring.md`](llm-wiki/measuring.md).
- Take the shared Ollama lock: `.scratch/ollama-lock/README.md` on the lead's laptop.
- Make sure only the model under test is loaded, and the machine is on mains power.
- Record the Ollama version and `size_vram` with the result.

**Two machines:**
- **Laptop:** the lead's RTX 4060 Laptop, 8 GB, plugged in.
- **5080 box:** the RTX 5080 machine, 16 GB, that also runs the asset server. It covers the 12 and
  16 GB tiers in [`llm-wiki/model-candidates.md`](llm-wiki/model-candidates.md).

An **A/B run** compares two arms on the evaluation set through the runner (#145), in one session
(hard rule 2). The formal list of A/B runs owed is #151. This page adds the order to run them
in, the machine, and the live checks that aren't A/B runs.

## Ready to run now

| Run | Kind | Machine | What it checks | From |
|---|---|---|---|---|
| Idle example with vs without | A/B | Laptop | Does the `none` example earn its tokens at all? On the profile's own line, "Fine day for it.": the code-built run on 9 November saw the default line cost `stop_following` commands (findings, 9 November) | #151 |
| `qwen3.5:2b` vs `qwen3.5:4b` | A/B, sequential arms | Laptop | The prototype's headline, on the framework | #151 |
| The 8 GB shortlist against `qwen3.5:2b` | A/B, sequential arms, one model each | Laptop | Which small model to use by default. **First check each model keeps the field order**: models on Ollama's GGUF-template path get their keys sorted (findings, 5 November). Today that's `MiniCPM5-2B`, which keeps the order only with `OLLAMA_GO_TEMPLATE=1` set on the server, and then on the Go template (findings, 9 November) | `model-candidates.md`, #151 |
| The 12 and 16 GB tiers | A/B, sequential arms | 5080 box | A "high-end GPU" option, such as Granite 4.1 8B, Qwen3.5-9B or Gemma 4 12B, against the 2B | `model-candidates.md` |

## Waiting for something first

| Run | Kind | Waiting for | What it checks | From |
|---|---|---|---|---|
| Example statements, written vs `ExampleStatements = false` | A/B, interleaved, one model, on GreyBoxVillage's guard (`Assets/Evaluation/VillageGuard.asset`); laptop on mains power | Statements written on `VillageGuard.asset`: an `ExampleStatement` on each of its four actions and its `IdleExampleStatement`, none matching a prompt (`PromptSetChecks` refuses one that does) | Do statements stop the model echoing an example's stimulus, without it parroting the statement instead? **Arm A:** `DecideOptions.ExampleStatements = false`, which is today's prompt byte for byte. **Arm B:** the defaults, so the written statements are shown. Count accuracy **and** replies that repeat any example line, a stimulus or a statement. The runner counts accuracy, not echoes, so match each answer's `modelStatement` in `report.json` against every example line with `PromptSetChecks.MatchesExample`. It helps only if echoes fall and accuracy does not | #190 G14, #151 |
| `PairTargetsWithActions` on vs off | A/B | Categories on the evaluation set's targets and profile actions | Does pairing each action with its own targets help? | #210, #151 |
| The example-target fallback | A/B | LV #20, paused | Does filling `{0}` from a registered target help, or does it leak the answer when it picks the target the player just named? | #190 LV #20 |
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
