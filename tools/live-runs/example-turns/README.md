# Example-turns A/B kit

Runs one Ollama model through `EvalRunner` with two arms interleaved, guards on (#151):

- `block`, the defaults: the few-shot examples as the system prompt's block of lines;
- `turns`, `DecideOptions.ExamplesAsTurns = true`: the same examples as `user`/`assistant` chat turns
  ([`prompt.md`](../../../UnityProject/Packages/com.agenerela.framework/Documentation~/prompt.md#examples-as-chat-turns)).

It is the [model sweep](../model-sweep/README.md) with other arms, and is run the same way, one model
at a time. The first run, on 9 November 2026, is in findings, "Few-shot examples as chat turns".

| File | What it is |
|---|---|
| `ExampleTurnsRun.cs.txt` | The Unity entry point. Not a committed test: copy it into a detached copy as `UnityProject/Packages/com.agenerela.framework/Tests/Runtime/ExampleTurnsRun.cs`. Besides the report it writes `tokens.json`, every answer's prompt and completion tokens and the form its examples went in, which the report does not carry |
| `run_one.sh` | One model, start to end: the same checks as `sweep_one.sh` (Ollama version, mains power, no Unity running, nothing resident), the Unity run, then unload |

## Running it

Read [`docs/llm-wiki/measuring.md`](../../../docs/llm-wiki/measuring.md) first, and take the lock for
the machine (`.scratch/ollama-5080-lock/` for the RTX 5080).

1. A detached copy on a short path, with the entry point copied in, as the sweep's step 2 says.
2. The settings, in Git Bash:

   ```bash
   export RUN_OUT=<a folder for this run, such as your checkout's .scratch/example-turns-<date>>
   export UNITY_PROJECT=C:/Unity_Projects/wt-turns/UnityProject
   export SWEEP_SET=Evaluation/Interim/agent.json
   # From a worktree, which has no tools/ollama-server/.env of its own:
   export OLLAMA_ENV_FILE=<your main checkout>/tools/ollama-server/.env
   ```

3. Each model, `qwen3.5:2b` first. Run a slice of about 20 prompts before the whole set: a set file
   with the first few prompts of each category, written beside `Interim/`.

   ```bash
   bash tools/live-runs/example-turns/run_one.sh qwen3.5:2b
   ```

The report lands in `$RUN_OUT/report-<model>/`, `tokens.json` beside `report.json`.
