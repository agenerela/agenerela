# 001: the 13-model sweep on the interim set, on the RTX 5080

| | |
|---|---|
| **Status** | open |
| **Machine** | The RTX 5080 box (16 GB). Either everything runs there, or only its Ollama does and Unity runs on the laptop over the network (way B below) |
| **Asked by** | The lead, 9 November 2026, through the implementation master session |
| **Kit** | [`tools/live-runs/model-sweep/`](../../../tools/live-runs/model-sweep/README.md) |
| **Results go in** | [`results.md`](results.md), here, and a new `findings.md` entry |
| **Expected time** | About 1 to 1.5 hours on the 5080. Measured on the laptop: 9 min for `qwen3.5:2b`, about 2.2 h projected for all 13 |

## Why

On 9 November the laptop swept 13 models over the curated set's 76 prompts (findings, "Thirteen
models on the evaluation set"). At n = 76 the noise floor is about 8 points overall and 20 to 30 per
category, so a whole category can turn on a handful of prompts. The interim set has 389 prompts
(#223, in `test`), which puts the floor near 3.6 overall and 9 per category.

The laptop started this sweep and stopped after `qwen3.5:2b`, because the lead asked for it to run
on the faster card. That one model's laptop result is in
[`laptop-interim-results.json`](laptop-interim-results.json). Re-run it here as the baseline:
numbers from two machines are never mixed in one table.

## What to run

All 13 models, one at a time, `qwen3.5:2b` first:

| # | Model | Note |
|---|---|---|
| 1 | `qwen3.5:2b` | The baseline. Run it first |
| 2 | `qwen3.5:4b` | |
| 3 | `qwen3:4b-instruct-2507-q4_K_M` | Sorts its keys without `OLLAMA_GO_TEMPLATE=1` |
| 4 | `granite4.1:3b` | |
| 5 | `nemotron-3-nano:4b` | |
| 6 | `hf.co/openbmb/MiniCPM5-2B-GGUF:Q4_K_M` | Sorts its keys without `OLLAMA_GO_TEMPLATE=1` |
| 7 | `lfm3:1.7b` | |
| 8 | `gemma4:e2b-it-qat` | |
| 9 | `ministral-3:3b` | |
| 10 | `phi4-mini:latest` | |
| 11 | `llama3.2:3b` | |
| 12 | `qwen3:8b` | |
| 13 | `hf.co/QuantFactory/L3-8B-Stheno-v3.2-GGUF:Q4_K_M` | |

Each model goes through `sweep_one.sh`: a 2-request probe (field order and thinking were checked
for all 13 on 9 November), then the A/B. The `idle-profile` arm uses the defaults, with
`VillageGuard`'s idle example "Fine day for it.". The `idle-none` arm sets
`IdleExampleStimulus = ""`. The arms interleave. Set and profile:

- set: `UnityProject/Assets/Evaluation/Interim/agent.json`, 389 prompts (`SWEEP_SET=Evaluation/Interim/agent.json`)
- profile: `UnityProject/Assets/Evaluation/VillageGuard.asset`, the curated set's, one folder up
  (the kit's default)

## Before you start, on the 5080

The server is the Docker Ollama in [`tools/ollama-server/`](../../../tools/ollama-server/README.md),
set up on the 5080 on 9 November, not the Ollama app. It pins 0.40.14, the version of every result
so far, with the kit's settings (`OLLAMA_GO_TEMPLATE=1`, `OLLAMA_CONTEXT_LENGTH=16384`,
`OLLAMA_NO_CLOUD=0`), so the kit's step 1, starting `ollama serve` by hand, is not needed. It is a
machine of its own for measuring: compare its results with each other, never with the app's.

1. **The server up**, at the right version: `python tools/ollama-server/ollama_remote.py check`
   prints `ollama 0.40.14`.
2. **The models.** Pull all 13 on the 5080 (`docker exec agenerela-ollama ollama pull <name>`, about
   35 GB), or with `ollama_remote.py pull <name>` from the laptop, then check them with `check`.
   Warm each one before its run (`ollama_remote.py warm <name>`): a first load from disk took 54 s,
   past the framework's 30 s timeout.
3. **The GPU free.** That machine also runs the asset server and the lead's own ComfyUI. Both must
   be idle for the whole run, with no render queued. Don't stop, remove or change either
   container: just don't queue work, and check `nvidia-smi` shows nothing else on the card and
   `ollama_remote.py ps` shows no model. The machine is on mains power.

## Two ways to run it

**Way A: everything on the 5080.** Needs a clone with Git LFS, and Unity 6000.3.23f1
([`environment.md`](../../llm-wiki/environment.md)). Follow the kit's README from step 2, with
`OLLAMA_URL` unset: `sweep_one.sh` reads `http://127.0.0.1:11436` and the key from the 5080's
`tools/ollama-server/.env`, which `setup.ps1` wrote.

**Way B: the 5080's Ollama, Unity on the laptop.** Connect the laptop as
[`tools/ollama-server/README.md`, "Connect from the laptop"](../../../tools/ollama-server/README.md#connect-from-the-laptop)
says, check with `ollama_remote.py check` from the laptop, then follow the kit's README on the
laptop with `OLLAMA_URL` unset. The laptop's own Ollama should be idle; the laptop's lock is not
needed, since its GPU is not used.

Either way, write each model's "template selection" line into the sweep folder, as the kit's
step 4 says. It is in the server's log: on the 5080,
`docker logs agenerela-ollama 2>&1 | grep "template selection"`.

## What to record

Fill in [`results.md`](results.md). Then summarise with the kit's `aggregate.py`, comparing with
the laptop's curated sweep:

```bash
python tools/live-runs/model-sweep/aggregate.py "$SWEEP_OUT" \
  --compare docs/run-requests/001-interim-model-sweep-5080/laptop-curated-results.json \
  --describe "interim set, 389 prompts, test <commit>, RTX 5080, ollama serve 0.40.14 with OLLAMA_GO_TEMPLATE=1, way A or B"
```

Copy its `results.json` here as `results-5080-interim.json`. If it is over about 1 MB, keep it in
`.scratch/` instead and say where.

**The `findings.md` entry** is a new one, dated the day of the run, laid out like the 9 November
sweep's. It must say:

- the interim set is AI-written, by Claude, with Codex labelling it blind, and its two labellers
  agreed 391 of 392 times. So its prompts may be less ambiguous than the curated set's, and the two
  sets' scores are not directly comparable;
- the machine and the way (A or B), with a note that latency over the network includes the hop;
- that the model ranking compares separate runs on one server, not `EvalArmOrder.Sequential` pairs;
- each model's curated-set score (laptop) beside its interim score (5080), marked as two sets on
  two machines.

**Then:** set this file's Status to done, move the `live-runs.md` row to Done, and open one docs
pull request into `test`. Don't change any default in code. A model that would make a better
default, or an idle example that should change, is reported for the lead to decide.

## If something goes wrong

- **`NO REPORT` with "Timeout value of 180000 ms was exceeded"**: the entry point was copied without
  its `[Timeout]`. Re-copy `ModelSweepRun.cs.txt`.
- **A model not fully in VRAM** (`size_vram` below `size`): record it and run it anyway. Its latency
  is then not comparable, and the table says so.
- **Pipeline errors**, timeouts at the provider's 30 s: record how many. Re-run the model once. If
  they persist, note them and go on.
- **A model writes `statement` before `target`**: mark it "order not kept" in every table, run its
  arms, and leave it out of the ranking.
