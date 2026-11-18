# Live-run kits

Scripts that a run request in [`docs/run-requests/`](../../docs/run-requests/README.md) points
to, so that any machine can repeat a run exactly as it was first done. Each kit has its own folder.
Python is standard library only, like `tools/benchmarks/`; the shell scripts run in Git Bash.

| Kit | What it runs | First used |
|---|---|---|
| [`runner/`](runner/README.md) | Any request with a `run.json`: on the RTX 5080 machine from the queue (`runq.py serve`), or one request anywhere (`runq.py run`) | 18 November 2026, on the RTX 5080 machine |
| [`model-sweep/`](model-sweep/README.md) | Each model in a list through `EvalRunner`, with the idle example on and off, plus a probe of field order, thinking and VRAM | 9 November 2026, on the lead's laptop (findings, "Thirteen models on the evaluation set") |

## The run queue on the RTX 5080 machine

[`runner/`](runner/README.md) plays live runs on the RTX 5080 machine itself, block by block, so a
run can stop and continue, in a queue on `test` that can be paused and reordered
([`runner/DESIGN.md`](runner/DESIGN.md) is why). A request gives it a `run.json`
([the format](../../docs/run-requests/README.md#runjson-a-request-the-runner-can-play)).
`runner/runq.py run` plays one request on a laptop the same way: use it instead of writing another
`run_blocks.sh`.
