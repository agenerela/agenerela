# Live-run kits

Scripts that a run request in [`docs/run-requests/`](../../docs/run-requests/README.md) points
to, so that any machine can repeat a run exactly as it was first done. Each kit has its own folder.
Python is standard library only, like `tools/benchmarks/`; the shell scripts run in Git Bash.

| Kit | What it runs | First used |
|---|---|---|
| [`model-sweep/`](model-sweep/README.md) | Each model in a list through `EvalRunner`, with the idle example on and off, plus a probe of field order, thinking and VRAM | 9 November 2026, on the lead's laptop (findings, "Thirteen models on the evaluation set") |

## Planned: a run queue on the RTX 5080 machine

[`runner/DESIGN.md`](runner/DESIGN.md) is the design, not yet built, for playing live runs on the
RTX 5080 machine itself, with runs that can stop and continue and a queue that can be paused and
reordered. Read it before writing another `run_blocks.sh`.
