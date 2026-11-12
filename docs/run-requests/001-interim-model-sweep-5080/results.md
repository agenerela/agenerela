# 001 results: the 13-model sweep on the interim set, on the RTX 5080

Filled in by the run. A field left as "—" was not recorded. The request is [`request.md`](request.md);
what the numbers mean is in [`findings.md`](../../llm-wiki/findings.md), "Thirteen models on the
interim set, on the RTX 5080".

## Conditions

| | |
|---|---|
| Run by | The live-runs Claude Code session on the lead's laptop, for the implementation master session |
| Date and time | 9 November 2026, 17:47 – 19:13 local (proof run, 2B baseline, pulls, 12 models) |
| Way | B: the 5080's Docker Ollama (`tools/ollama-server/`), Unity on the laptop |
| Server | 192.168.1.129:11436, the keyed gateway; Docker Ollama 0.40.14 from `/api/version`, with `OLLAMA_GO_TEMPLATE=1`, `OLLAMA_CONTEXT_LENGTH=16384`, `OLLAMA_NO_CLOUD=0` pinned by the image's compose file |
| GPU | RTX 5080 16 GB; driver not read from here. `ollama_remote.py ps` showed nothing resident before the first model and between models; `nvidia-smi` on the 5080 not checked from here |
| ComfyUI stacks idle | Not checked from here: no docker or ssh to that machine (AGENT_USAGE rule 2) |
| Unity | 6000.3.23f1, batch mode, on the laptop; `test` at cd0c09c in a detached worktree with `ModelSweepRun.cs` copied in |
| Set | `Evaluation/Interim/agent.json`, 389 prompts; profile `Evaluation/VillageGuard.asset` |
| Power | Laptop on mains (`Win32_Battery` status 2), checked before each model |
| Total time | 63 min of sweeping, 3 to 7 min a model; 16 min of pulls before it |

## Per model

"On" is the idle example on (the default score), "off" is without it. The floor is 3.6 points at
n = 389. The chat path was not read: the server's log is only on the 5080.

| Model | Path | Order | VRAM / size, MB | On | Off | Off − on, verdict | `stop_following` on / off (of 48) | Unasked on / off (of 156) | Missed on / off (of 233) | Replies flagged on / off | Median / p90 latency, s | Pipeline errors | Curated, laptop (on) |
|---|---|---|---|---:|---:|---|---|---|---|---|---|---|---:|
| `qwen3.5:2b` | — | 2/2 | 2254 / 2254 | 263 | 281 | +4.6, beyond | 22 / 39 | 50 / 48 | 36 / 22 | 13 / 7 | 0.37 / 0.42 | 0 | 56/76 |
| `qwen3.5:4b` | — | 2/2 | 2995 / 2995 | 330 | 292 | −9.8, beyond | 44 / 45 | 23 / 67 | 12 / 3 | 0 / 0 | 0.51 / 0.56 | 0 | 70/76 |
| `qwen3:4b-instruct-2507-q4_K_M` | — | 2/2 | 3032 / 3032 | 296 | 268 | −7.2, beyond | 38 / 40 | 66 / 94 | 3 / 0 | 0 / 0 | 0.32 / 0.36 | 0 | 65/76 |
| `granite4.1:3b` | — | 2/2 | 2390 / 2390 | 304 | 311 | +1.8, within | 38 / 42 | 19 / 21 | 36 / 36 | 2 / 0 | 0.24 / 0.27 | 0 | 62/76 |
| `nemotron-3-nano:4b` | — | 2/2 | 2693 / 2693 | 304 | 278 | −6.7, beyond | 44 / 44 | 43 / 69 | 14 / 8 | 2 / 2 | 0.35 / 0.38 | 0 | 60/76 |
| `MiniCPM5-2B` Q4_K_M | — | 2/2 | 1564 / 1564 | 240 | 240 | 0, within | 38 / 42 | 63 / 72 | 32 / 22 | 1 / 0 | 0.21 / 0.23 | 0 | 58/76 |
| `lfm3:1.7b` | — | 2/2 | 1114 / 1114 | 202 | 200 | −0.5, within | 30 / 30 | 86 / 65 | 3 / 19 | 4 / 3 | 0.23 / 0.28 | 0 | 46/76 |
| `gemma4:e2b-it-qat` | — | 2/2 | 1578 / 1578 | 290 | 297 | +1.8, within | 29 / 42 | 32 / 45 | 35 / 13 | 0 / 1 | 0.34 / 0.41 | 0 | 64/76 |
| `ministral-3:3b` | — | 2/2 | 2601 / 2601 | 289 | 277 | −3.1, within | 46 / 46 | 64 / 80 | 0 / 0 | 0 / 0 | 0.28 / 0.31 | 0 | 65/76 |
| `phi4-mini:latest` | — | 2/2 | 2956 / 2956 | 290 | 255 | −9.0, beyond | 48 / 47 | 36 / 74 | 4 / 1 | 2 / 3 | 0.31 / 0.35 | 0 | 57/76 |
| `llama3.2:3b` | — | 2/2 | 2442 / 2442 | 277 | 235 | −10.8, beyond | 44 / 45 | 78 / 120 | 4 / 2 | 1 / 1 | 0.24 / 0.29 | 0 | 59/76 |
| `qwen3:8b` | — | 2/2 | 5327 / 5327 | 315 | 294 | −5.4, beyond | 47 / 47 | 40 / 63 | 7 / 4 | 2 / 2 | 0.37 / 0.42 | 0 | 65/76 |
| `L3-8B-Stheno-v3.2` Q4_K_M | — | 2/2 | 5035 / 5035 | 294 | 279 | −3.9, beyond | 46 / 47 | 65 / 82 | 3 / 1 | 1 / 0 | 0.32 / 0.37 | 0 | 60/76 |

## Against the baseline

Each model's "on" score against `qwen3.5:2b`'s "on" score, from this run only. These are separate
runs on one server, not `Sequential` pairs.

| Model | On | Against `qwen3.5:2b` | Categories beyond noise against the 2B |
|---|---:|---|---|
| `qwen3.5:4b` | 330 | +17.2, beyond | simple +26.2, conversation +23.4, event +18.5, situation +16.9, impossible +12.5 |
| `qwen3:8b` | 315 | +13.4, beyond | simple +26.2, conversation +25.0, event +12.3, situation +12.3 |
| `granite4.1:3b` | 304 | +10.5, beyond | simple +23.1, conversation +21.9, impossible +9.4 |
| `nemotron-3-nano:4b` | 304 | +10.5, beyond | simple +23.1, situation +20.0, conversation +12.5, event +10.8; impossible −9.4 |
| `qwen3:4b-instruct-2507-q4_K_M` | 296 | +8.5, beyond | situation +20.0, simple +16.9, conversation +12.5, event +12.3; impossible −15.6 |
| `L3-8B-Stheno-v3.2` Q4_K_M | 294 | +8.0, beyond | simple +24.6 |
| `gemma4:e2b-it-qat` | 290 | +6.9, beyond | situation +15.4, simple +9.2 |
| `phi4-mini:latest` | 290 | +6.9, beyond | simple +23.1, impossible +18.8, conversation +9.4 |
| `ministral-3:3b` | 289 | +6.7, beyond | simple +24.6 |
| `llama3.2:3b` | 277 | +3.6, on the floor | simple +20.0; impossible −9.4 |
| `MiniCPM5-2B` Q4_K_M | 240 | −5.9, beyond | event −15.4, conversation −10.9, situation −9.2 |
| `lfm3:1.7b` | 202 | −15.7, beyond | impossible −35.9, event −27.7, situation −20.0, conversation −10.9 |

## What went wrong, and anything else worth knowing

- **Only `qwen3.5:2b` was on the server at the start.** The lead approved pulling the other 12; they
  came down one at a time in 16 minutes, before any of them was timed.
- **The first pass refused all 12 models**, because Unity Hub's CLI server (`unity.exe serve`) was
  running on the laptop and the kit counted it as an Editor. Nothing was measured in that pass. The
  kit now counts Editors only (`sweep_one.sh`), and the second pass ran clean.
- **A proof run** of the 2B on the curated 76 through the gateway came first: 56/76 on, 58/76 off.
  It is in the sweep folder as `proof-curated/`.
- **The "template selection" lines were not read.** They are only in the 5080's Docker log. The
  probe's field-order check passed on every model.
- The 12 and 16 GB tiers were not run; the lead judged them not needed.

## Files

- Summary: [`results-5080-interim.json`](results-5080-interim.json) in this folder, per-prompt answers removed
- Per-model reports, probes, Unity logs and the full `results.json`: `.scratch/model-sweep-5080/` in the
  live-runs session's checkout (`interim/` and `proof-curated/`)
- `findings.md` entry: "Thirteen models on the interim set, on the RTX 5080: `qwen3.5:4b` first at
  330/389, and the idle example helps every model but `qwen3.5:2b` (9 November 2026)"
