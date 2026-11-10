# Live-run kits

Scripts that a run request in [`docs/run-requests/`](../../docs/run-requests/README.md) points
to, so that any machine can repeat a run exactly as it was first done. Each kit has its own folder.
Python is standard library only, like `tools/benchmarks/`; the shell scripts run in Git Bash.

| Kit | What it runs | First used |
|---|---|---|
| [`model-sweep/`](model-sweep/README.md) | Each model in a list through `EvalRunner`, with the idle example on and off, plus a probe of field order, thinking and VRAM | 9 November 2026, on the lead's laptop (findings, "Thirteen models on the evaluation set") |
| [`example-turns/`](example-turns/README.md) | One model through `EvalRunner`, the few-shot examples as the system prompt's block against the same examples as chat turns (#151), and every answer's prompt tokens | 9 November 2026, on the RTX 5080 (findings, "Few-shot examples as chat turns") |
