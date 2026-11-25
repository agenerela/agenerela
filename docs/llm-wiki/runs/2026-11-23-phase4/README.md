# Phase 4's A/B runs on the home server, 23 November 2026

The reports of `EvalRunner` runs on the 209-prompt evaluation set (#147), played one after
another on the home server (RTX 4070 SUPER 12 GB, Docker Ollama 0.40.14 behind the gate at commit
`5728506a6d6b`, over the home network), each under its own clean lease. What they show is in
[`findings.md`](../../findings.md), 23 November: "The Phase 4 gate's live A/B", "#151's first
runs on the 209 set", "#151's switch runs on the 209 set" and "#151 after #384". The `-4b` folders ran `qwen3.5:4b` in both arms;
the rest `qwen3.5:2b`. The first five rows ran at test `1f12636`; every run below them at `60e435c` (#385), except the four rows after it, at `4485e83` (#384) on set `ab2d30b`, whose controls are not comparable with the rest, `list-4b-timing/`, at `6056863` on 24 November, and `think-t1-pilot/`, at `0666d68` the same day.

| Folder | Run | Lease | Arms |
|---|---|---|---|
| [`gate/`](gate/report.md) | #150's gate A/B (with `report.json`, every answer) | #10061 | `guards-on` (defaults) against `guards-off` (`CheckTarget = false`) |
| [`idle/`](idle/report.md) | Idle example, #151 | #10488 | `idle-profile` (defaults) against `idle-none` (`IdleExampleStimulus = ""`) |
| [`list/`](list/report.md) | `ListActionsAndTargets`, #151 | #10915 | `list-off` (defaults) against `list-on` |
| [`grounding/`](grounding/report.md) | Grounding threshold, #151 | #11342 | `ground-0.8` (defaults) against `ground-0.6` |
| [`models/`](models/report.md) | `qwen3.5:2b` against `qwen3.5:4b`, #151, sequential arms | #11769 | `qwen3.5-2b` against `qwen3.5-4b` |
| [`rename/`](rename/report.md), [`rename-4b/`](rename-4b/report.md) | The free-text field's name, #151, DR-008 | #18526, #21088 | `statement` (defaults) against `dialogue` (`StatementFieldName`) |
| [`targetrule/`](targetrule/report.md), [`targetrule-4b/`](targetrule-4b/report.md) | The one-line target rule, #151, #107 | #18953, #20661 | `probabilities` (defaults) against `target-rule` (`TokenProbabilities = false` on the config) |
| [`history/`](history/report.md), [`history-4b/`](history-4b/report.md) | History on against off, #151 | #19380, #21515 | `history` (defaults) against `no-history` (`EvalArm.IgnoreHistory`) |
| [`grounding-076/`](grounding-076/report.md), [`grounding-076-4b/`](grounding-076-4b/report.md) | Grounding threshold 0.8 against 0.76 (tech lead) | #21942, #22369 | `ground-0.8` (defaults) against `ground-0.76` |
| [`idle-4b/`](idle-4b/report.md), [`list-4b/`](list-4b/report.md) | The idle example and `ListActionsAndTargets` again, on `qwen3.5:4b` | #19807, #20234 | as `idle/` and `list/` |
| [`list-4b-repeat/`](list-4b-repeat/report.md) | `ListActionsAndTargets` again on `qwen3.5:4b`, a repeat of `list-4b/` | #27545 | as `list/` |
| [`freechoice/`](freechoice/report.md), [`freechoice-4b/`](freechoice-4b/report.md) | One free-choice example (tech lead's wording), #151 | #27972, #28399 | `plain` (defaults) against `free-choice` (`AddedExamples`) |
| [`summarise/`](summarise/report.md), [`summarise-4b/`](summarise-4b/report.md) | Templated against summarised targets, #151, §2.8 | #31165, #32873 | `templated` (defaults) against `summarised` (`EvalArm.SummariseTargets`) |
| [`pair/`](pair/report.md), [`pair-4b/`](pair-4b/report.md) | The paired schema, #151 | #31592, #33300 | `flat` (defaults) against `paired` (`PairTargetsWithActions`) |
| [`statements/`](statements/report.md), [`statements-4b/`](statements-4b/report.md) | Example statements, #151 | #32019, #33727 | `no-statements` (`ExampleStatements = false`) against `statements` (defaults) |
| [`counters/`](counters/report.md), [`counters-4b/`](counters-4b/report.md) | Counter-examples, #151 | #32446, #34154 | `no-counters` (`CounterExamples = false`) against `counters` (defaults) |
| [`list-4b-timing/`](list-4b-timing/report.md) | `ListActionsAndTargets` timed on `qwen3.5:4b`, 24 November, with `decisions.csv` (each decision's prompt and completion tokens and latency) | #73451 (`agenerela-runs`) | as `list/` |
| [`think-t1-pilot/`](think-t1-pilot/report.md) | Native thinking off against on (T1 pilot, DR-029), `qwen3.5:4b`, 33 prompts (slice 0), both arms at `num_ctx` 16384, 24 November, with `report.json` and `decisions.csv` | #73923 (`agenerela-runs`) | `think-off` against `think-on` (`OllamaProviderConfig.Think`) |

`Phase4Run.cs.txt` is the entry point that ran them: one test, the run picked by
`AGENERELA_P4_RUN`, built on the model-sweep kit's `ModelSweepRun.cs.txt`
([`tools/live-runs/model-sweep/`](../../../../tools/live-runs/model-sweep/README.md)). Copy it into
a detached copy of the repository as
`UnityProject/Packages/com.agenerela.framework/Tests/Runtime/Phase4Run.cs`, and run it in batch
mode with `-runTests -testPlatform PlayMode -testFilter Agenerela.Tests.Phase4Run`, inside
`degen_lease.py run --clean`. Unload every model inside the lease before Unity starts: on
23 November a clean lease once still had another session's model resident, and the runner refused.
Never commit the copied file.

Only `gate/` and `think-t1-pilot/` keep their `report.json` (and `list-4b-timing/` and `think-t1-pilot/` their `decisions.csv`) (about 425 KB); the others are in the run session's
`.scratch/phase4-runs/out/` on the lead's laptop.
