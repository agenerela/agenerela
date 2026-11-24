# Phase 4's A/B runs on the home server, 23 November 2026

The reports of `EvalRunner` runs on the 209-prompt evaluation set (#147), played one after
another on the home server (RTX 4070 SUPER 12 GB, Docker Ollama 0.40.14 behind the gate at commit
`5728506a6d6b`, over the home network), each under its own clean lease. What they show is in
[`findings.md`](../../findings.md), 23 November: "The Phase 4 gate's live A/B", "#151's first
runs on the 209 set" and "#151's switch runs on the 209 set". The `-4b` folders ran `qwen3.5:4b` in both arms;
the rest `qwen3.5:2b`. The first five rows ran at test `1f12636`; every run below them at `60e435c` (#385).

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

`Phase4Run.cs.txt` is the entry point that ran them: one test, the run picked by
`AGENERELA_P4_RUN`, built on the model-sweep kit's `ModelSweepRun.cs.txt`
([`tools/live-runs/model-sweep/`](../../../../tools/live-runs/model-sweep/README.md)). Copy it into
a detached copy of the repository as
`UnityProject/Packages/com.agenerela.framework/Tests/Runtime/Phase4Run.cs`, and run it in batch
mode with `-runTests -testPlatform PlayMode -testFilter Agenerela.Tests.Phase4Run`, inside
`degen_lease.py run --clean`. Unload every model inside the lease before Unity starts: on
23 November a clean lease once still had another session's model resident, and the runner refused.
Never commit the copied file.

Only `gate/` keeps its `report.json` (about 425 KB); the others are in the run session's
`.scratch/phase4-runs/out/` on the lead's laptop.
