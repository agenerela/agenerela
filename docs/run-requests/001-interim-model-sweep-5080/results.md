# 001 results: the 13-model sweep on the interim set, on the RTX 5080

Fill this in as the run goes. Leave a field as "—" rather than deleting it, so a reader can see
what was not recorded. The request is [`request.md`](request.md).

## Conditions

| | |
|---|---|
| Run by | <person or session> |
| Date and time | <start – end, local> |
| Way | A (all on the 5080) / B (5080's Ollama, Unity on the laptop) |
| Server | <IP:port>, Ollama <version from /api/version>, started with `OLLAMA_GO_TEMPLATE=1`, `OLLAMA_CONTEXT_LENGTH=16384`, `OLLAMA_NO_CLOUD=0` |
| GPU | RTX 5080 16 GB, driver <version>; `nvidia-smi` showed nothing else on the card: yes / no |
| ComfyUI stacks idle | yes / no |
| Unity | 6000.3.23f1, batch mode, on <machine>; `test` at <commit> |
| Set | `Evaluation/Interim/agent.json`, 389 prompts; profile `Evaluation/VillageGuard.asset` |
| Power | mains on <machine(s)> |
| Total time | — |

## Per model

One row per model, from `aggregate.py`'s output. "On" is the idle example on (the default score),
"off" is without it. The floor is 3.6 points at n = 389.

| Model | Path | Order | VRAM / size, MB | On | Off | Off − on, verdict | `stop_following` on / off (of 48) | Unasked on / off (of 156) | Missed on / off (of 233) | Replies flagged on / off | Median / p90 latency, s | Pipeline errors | Curated, laptop (on) |
|---|---|---|---|---:|---:|---|---|---|---|---|---|---|---:|
| `qwen3.5:2b` | | | | | | | | | | | | | 56/76 |
| `qwen3.5:4b` | | | | | | | | | | | | | 70/76 |
| `qwen3:4b-instruct-2507-q4_K_M` | | | | | | | | | | | | | 65/76 |
| `granite4.1:3b` | | | | | | | | | | | | | 62/76 |
| `nemotron-3-nano:4b` | | | | | | | | | | | | | 60/76 |
| `MiniCPM5-2B` Q4_K_M | | | | | | | | | | | | | 58/76 |
| `lfm3:1.7b` | | | | | | | | | | | | | 46/76 |
| `gemma4:e2b-it-qat` | | | | | | | | | | | | | 64/76 |
| `ministral-3:3b` | | | | | | | | | | | | | 65/76 |
| `phi4-mini:latest` | | | | | | | | | | | | | 57/76 |
| `llama3.2:3b` | | | | | | | | | | | | | 59/76 |
| `qwen3:8b` | | | | | | | | | | | | | 65/76 |
| `L3-8B-Stheno-v3.2` Q4_K_M | | | | | | | | | | | | | 60/76 |

## Against the baseline

Each model's "on" score against `qwen3.5:2b`'s "on" score, from this run only (`vs baseline` in
`aggregate.py`'s output). These are separate runs on one server, not `Sequential` pairs.

| Model | On | Against `qwen3.5:2b` | Categories beyond noise against the 2B |
|---|---:|---|---|

## What went wrong, and anything else worth knowing

- —

## Files

- Summary: `results-5080-interim.json` in this folder, or <where, if too big>
- Per-model reports and logs: <the sweep folder>
- `findings.md` entry: <its heading>
