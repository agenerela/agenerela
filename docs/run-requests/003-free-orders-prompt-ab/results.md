# 003: results

**Played** on 23 November 2026 from Unity on the lead's laptop (batch mode, a worktree at test `c544c13`, after
#380), against the home server: NVIDIA GeForce RTX 4070 SUPER 12 GB, Docker Ollama behind the gate (gate commit
`5728506a6d6b` for every block), over the home network. `qwen3.5:4b`, 3.1 GB, 100% on the GPU, warmed before each
block (2.6 to 3.6 s). Each block held its own clean lease, taken with nobody waiting and released at its end:
c1 #94, t1 #1084, n1 #1864, c2 #2732, t2 #3906, n2 #4868, c3 #5901, t3 #8046, n3 #9040. Blocks took 11 to 16
minutes. The Ollama version was not read this session; the server guide records 0.40.14 (21 November).

**Raw data** in the lead's main checkout, `.scratch/free-orders-runs-2026-11-23/data/` (`model.csv`, `games.csv`,
`decisions.jsonl` a block; `report.txt` is the report's output).

**Replayed blocks.** Two attempts at t1 and one at t3 were voided and replayed, their data kept apart in `void/`:
t1 twice because the laptop slept (Modern Standby at 10:38, then the lid closed until 13:33) and the block outlived
its lease; t3 because another session using the same `agenerela` key took a lease 10 s before it, so t3 joined that
lease and shared the card. Sessions on that key now take a laptop-side lock, `.scratch/degen-agenerela-lock`, around
every lease.

| | Control (c) | `CourtTurnFacts` (t) | `CourtNoneWording` (n) |
|---|---:|---:|---:|
| Decisions (court requests) | 3,160 (1,207) | 2,722 (1,084) | 2,732 (1,090) |
| Illegal answers, not offered | 0, 0 | 0, 0 | 0, 0 |
| Court orders a realm a turn, seeds 1 / 2 / 3 | 1.07 / 1.48 / 1.38 | 0.96 / 0.86 / 1.10 | 1.05 / 0.88 / 0.93 |
| Calls a turn, seeds 1 / 2 / 3 | 21.1 / 29.2 / 28.7 | 19.4 / 23.9 / 24.8 | 21.6 / 23.6 / 23.1 |
| Model seconds a turn, seeds 1 / 2 / 3 | 16.4 / 23.7 / 23.7 | 16.4 / 19.6 / 21.6 | 17.5 / 18.8 / 18.2 |
| Model seconds a turn, all (against control, same seed and turn) | 21.2 | 19.2 (−2.0) | 18.2 (−3.1) |
| Seconds a call | 0.80 | 0.85 | 0.80 |
| Court prompt tokens, mean (max) | 1,407 (1,769) | 1,399 (1,766) | 1,389 (1,770) |
| A viewer's wait with a 12 s playback, mean | 9.3 s | 7.2 s | 6.2 s |

**What it shows.**
- **Legal answers: no difference.** Not one illegal or not-offered answer in 8,614 decisions, in any arm.
- **Both new prompts make a court stop sooner.** In all six pairs a court gave fewer orders a turn than the control
  on the same seed (control 1.31 a realm-turn, t 0.98, n 0.95). Realm-turns with three orders fell from 162 of 593 to
  102 of 600 (t) and 83 of 600 (n). Fewer orders mean fewer calls, which is where the 2 to 3 seconds a turn come from.
  Seconds a call did not fall (t is 0.05 s slower a call), and prompts are the same length.
- **Seed 1 alone shows no difference.** The fall is seeds 2 and 3, where the control gave the most orders.

**Read with care.** Three seeds, one game an arm each, and the games part after turn 1, so "the same turn" is a
different position after the first few turns. The direction is the same in all six pairs; the size is not measured.
These are not accuracy results: legal answers are 100% everywhere, so the question the lead set ("not worse on legal
answers and seconds") is answered by the table, and whether fewer orders is the better game is a design question.
