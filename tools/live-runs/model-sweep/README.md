# Model sweep kit

Runs a list of Ollama models through `EvalRunner` on one prompt set, one model at a time. Each model
gets two things:

1. **A probe**, `probe.py`: a few of the framework's own `/api/chat` requests, recording the reply's
   field order, any thinking, and `size` and `size_vram`.
2. **An A/B**, two arms interleaved:
   - `idle-profile`, the defaults, which is also the model's default score;
   - `idle-none`, with `DecideOptions.IdleExampleStimulus = ""`.

This is the sweep from 9 November 2026 (findings, "Thirteen models on the evaluation set"), packaged
so any machine can repeat it.

| File | What it is |
|---|---|
| `ModelSweepRun.cs.txt` | The Unity entry point. Not a committed test: copy it into a detached copy (step 2 below) |
| `framework_request.json` | The request `OllamaProvider` sends for the evaluation set, for the probe |
| `probe.py` | The probe |
| `sweep_one.sh` | One model, start to end: checks, probe, Unity run, unload. It stops before running if the server is the wrong Ollama version, if this machine is on battery, if Unity is already running, or if a model is already loaded |
| `aggregate.py` | Summarises a sweep folder into `results.json` and prints one line per model |

## Running a sweep

Do this once per sweep, then run step 4 once per model. Read
[`docs/llm-wiki/measuring.md`](../../../docs/llm-wiki/measuring.md) first.

**1. The Ollama server.** Start it with the settings every result so far used:

- `OLLAMA_GO_TEMPLATE=1`, so no model has its keys sorted (findings, 9 November);
- `OLLAMA_CONTEXT_LENGTH=16384` and `OLLAMA_NO_CLOUD=0`, the two the Ollama app adds.

Quit the Ollama app first (only Ollama's own processes), then in PowerShell:

```powershell
$env:OLLAMA_GO_TEMPLATE = '1'; $env:OLLAMA_CONTEXT_LENGTH = '16384'; $env:OLLAMA_NO_CLOUD = '0'
& "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe" serve 2>&1 | Tee-Object -FilePath "$env:USERPROFILE\ollama-sweep-serve.log"
```

The app's updater cannot run while it is closed, so the version holds for the whole sweep. For a
server on another machine, read
[`environment.md`, "Ollama on another machine"](../../../docs/llm-wiki/environment.md#ollama-on-another-machine-over-the-local-network)
first.

**2. A detached copy of the repository**, on a short path, with the entry point copied in:

```bash
git -C <your checkout> fetch origin
git -C <your checkout> worktree add --detach C:/Unity_Projects/wt-sweep origin/test
cp tools/live-runs/model-sweep/ModelSweepRun.cs.txt \
   C:/Unity_Projects/wt-sweep/UnityProject/Packages/com.agenerela.framework/Tests/Runtime/ModelSweepRun.cs
```

A short path, because `Library/PackageCache` passes Windows' 260-character limit inside a deep
folder. Use editor 6000.3.23f1 only. The first run imports for 3 to 4 minutes; that is not a hang.
Never commit the copied file. Remove the worktree when the sweep is done
(`git worktree remove --force C:/Unity_Projects/wt-sweep`).

**3. The settings**, in Git Bash:

```bash
export SWEEP_OUT=<a folder for this sweep, such as your checkout's .scratch/model-sweep-<date>>
export UNITY_PROJECT=C:/Unity_Projects/wt-sweep/UnityProject
export SWEEP_SET=Evaluation/Interim/agent.json      # or Evaluation/agent.json, the curated 76
export OLLAMA_URL=http://127.0.0.1:11434            # or http://<the server's LAN IP>:11434
export PROBE_REQUESTS=2                             # 6 for a model whose order nobody has checked
```

**4. Each model**, `qwen3.5:2b` first, because it is the baseline the others are compared with:

```bash
bash tools/live-runs/model-sweep/sweep_one.sh qwen3.5:2b
```

Then copy the model's "template selection" line from the server's log into
`$SWEEP_OUT/template-<model>.txt`, written as `selected=renderer_parser`. In `<model>`, every `/`
and `:` becomes `_`. That line says which chat path the model took. The script can't read it
itself when the server is another machine.

**5. The summary.**

```bash
python tools/live-runs/model-sweep/aggregate.py "$SWEEP_OUT" --compare <an earlier results.json> \
  --describe "<set, commit, server, machine>"
```

## Reading a model's line

| Field | Meaning |
|---|---|
| `order` | Probe replies in the framework's order `action`, `target`, `statement`. Anything below all of them: that model's scores are not comparable; mark it "order not kept" |
| `on` / `off` | Correct answers with the idle example and without it. `on` is the default score |
| `stop_following` | Right answers on prompts that want it. `qwen3.5:2b` loses these to the idle example (findings, 9 November) |
| `unasked` / `missed` | An action on a prompt labelled `none` / `none` on a prompt that wants an action |
| `replies flagged` | `EvalRunner`'s reply checks (#224): echoed examples, empty, too long, leaked ids |
| `vs baseline` | This model's `on` against the baseline's `on`, with the noise floor. These are separate runs on one server, not `EvalArmOrder.Sequential` pairs: say so when quoting them |

## Known pitfalls

- **The Test Framework ends a test at 180 s** unless it has a `[Timeout]`. `ModelSweepRun` has 4 hours.
  The curated set fits in 180 s; the interim set does not.
- **The lock.** On the lead's laptop, take `.scratch/ollama-lock/` first, and on its own. Never
  restart Ollama in the same step as checking the lock: on 9 November that cut off another
  session's run.
- **Battery.** The script reads `Win32_Battery` on the machine running Unity. A server elsewhere
  must be on mains too; check it there.
