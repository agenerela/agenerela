# 006: results

**Played** on 24 November 2026, 14:08 to 17:39 local, from Unity in batch mode on the lead's laptop, on mains and the
home network (LAN) for every block. Detached worktree at test `ac71b5a` (after #392, which adds `PromptBudget` and
the `+budget-<n>` and `+trim` modifiers), imported once outside the lease per block. Against the home server:
NVIDIA GeForce RTX 4070 SUPER 12 GB, Docker Ollama 0.40.14 behind the gate (commit `5728506a6d6b` for every block).
`qwen3.5:4b` only, warmed before each block. Played in the request's order, a1 b1 ... a6 b6. Each block held its own
clean lease on the runs session's own client `agenerela-runs`, plus the laptop lock `.scratch/degen-agenerela-lock`:

| Block | Lease | Local time | Block | Lease | Local time |
|---|---|---|---|---|---|
| a1 | #60580 | 14:08–14:25 | b1 | #61612 | 14:25–14:39 |
| a2 | #62559 | 14:39–14:57 | b2 | #63868 | 14:57–15:13 |
| a3 | #64977 | 15:13–15:27 | b3 | #65945 | 15:27–15:42 |
| a4 | #66992 | 15:42–15:56 | b4 | #67986 | 15:56–16:11 |
| a5 | #69071 | 16:11–16:33 | b5 | #70210 | 16:33–16:56 |
| a6 | #71331 | 16:56–17:13 | b6 | #72527 | 17:13–17:39 |

- **Lease checks:** `degen_status.py` checked each lease before Unity started and after it ended. All 24 checks
  found the block's own lease.
- **Voids:** none were voided or replayed.
- **Other traffic:** one other agenerela request waited behind a5's lease. The other key's daily request count
  rose by 4 during a4, 7 during b4 (after its midnight-UTC reset), 7 during a5 and 1 during b5, and not at all
  during the other eight blocks. Those requests are small next to a block's roughly 1,000 decisions, but they are
  noted, not ruled out.

**Code since.** #405 (merged later, at `73f4c22`) turns Painted Realm's `CourtTurnFacts` on by default, and #404
turns `ListActionsAndTargets` on by default. Both arms here are from before either. A run of these arms on a newer
head has CourtTurnFacts on, which adds lines to every court prompt, so its token counts are not comparable with
these.

**Raw data** is in the runs session's worktree, `.scratch/r006/data-006/`:
- per block, `summary.md`, `model.csv`, `games.csv` and `decisions.jsonl` (a2's log rolled over at 16 MB into
  `decisions.1.jsonl`; read both);
- `report.txt`, `tools/live-runs/economy/report.py --control b --limit 1600`;
- `report-control-a.txt`, the same with `--control a`, for arm b's personas;
- `an.txt`, the trims, the estimate and the games, from `../an.py`.

| | a: trimmed under 1,600 | b: untrimmed (control) |
|---|---:|---:|
| Decisions (court requests) | 6,573 (2,463) | 6,195 (2,257) |
| Illegal answers, all / court | 0 / 0 | 0 / 0 |
| Court prompt tokens (`prompt_eval_count`), mean (max) | **1,339 (1,568)** | 1,547 (2,058) |
| Court requests over 1,600 | **0** | 834 (37%) |
| Court requests trimmed | 1,195 (49%): every one unlisted untakeable targets, 229 also dropped one remembered exchange | none (3 over its 2,048 budget, recorded only) |
| Court requests still over budget after trimming (`overBudget`) | 0 | – |
| Seconds a court request, mean | 0.83 | 0.82 |
| Calls a turn (max) | 27.4 (56) | 25.8 (42) |
| Model seconds a turn (max) | 22.0 (43.1) | 20.6 (33.1) |
| a against b, same seed and turn (240 turns) | calls +1.57, model s +1.39 | |
| …per-seed spread, 95% over 6 seeds | calls [−2.3, +5.5], model s [−1.5, +4.3]: within | |
| Battles a game, seeds 1–6 | 113 / 94 / 81 / 92 / 126 / 114 (mean 103.3) | 117 / 109 / 82 / 88 / 83 / 116 (mean 99.2) |
| …a − b paired | +4.2, 95% [−16.9, +25.2]: within | |
| Peace made a game, mean | 13.7 | 10.7 (a − b +3.0, 95% [−1.8, +7.8]: within) |
| Provinces changing hands a game, mean | 48.3 | 48.3 |
| Winner's persona, seeds 1–6 | opportunist, merchant, merchant, merchant, merchant, warmonger | warmonger, proud, merchant, merchant, merchant, warmonger |

**Buildings bought and tax changes, by persona (6 games, 6 seats each):**

| Persona | a: barracks / farms / markets | b: barracks / farms / markets | a: tax changes | b: tax changes | Provinces at the end, a / b |
|---|---|---|---:|---:|---|
| merchant | 27 / 26 / 26 | 19 / 20 / 32 | 32 | 26 | 8.8 / 8.5 |
| builder | 10 / 13 / 1 | 7 / 6 / 2 | 20 | 15 | 1.7 / 2.3 |
| opportunist | 15 / 4 / 1 | 9 / 5 / 0 | 28 | 13 | 4.8 / 4.0 |
| proud | 11 / 2 / 0 | 14 / 6 / 0 | 22 | 19 | 4.3 / 5.5 |
| warmonger | 13 / 2 / 1 | 14 / 12 / 0 | 18 | 13 | 4.3 / 3.8 |
| all | 152 | 146 | 120 | 86 | |

**The estimate against the count**, on every court request: `promptTokensEstimated` runs above Ollama's
`prompt_eval_count`.

| Court requests | Mean over the count | Range over the count |
|---|---:|---|
| b, untrimmed | +64 | −20 to +145 |
| a, untrimmed | +50 | |
| a, trimmed | +143 | |

The estimate never fell short by more than 20 tokens, so the trims act early rather than late. The overshoot is
largest on trimmed requests, the ones whose estimate was near the budget. The estimate is of the request as sent,
and `overBudget` follows the provider's count once it has one (`Agent.JudgeBudget`). So a few trimmed requests with
an estimate up to 1,638 still read `overBudget: false`, their count being under 1,600.

- **Trimming holds the budget and costs no legality:** every court request stays under 1,600 (max 1,568), about
  210 tokens fewer on average (−13%), with 0 illegal answers in both arms. Unlisting untakeable targets did most of
  it; a remembered exchange was dropped on 229 requests.
- **It saves no time on this card:** a court request takes 0.83 s against 0.82 s. Prompt evaluation of 200 tokens
  is a few milliseconds on a 4070 SUPER; the time is in generation. Model seconds a turn are higher in a (+1.4 s)
  because a's games made more calls a turn, within the spread between seeds.
- **Play, within the noise everywhere:** battles +4 a game, peace +3, provinces changing hands equal. Buildings
  152 against 146. The merchant still buys most markets and wins four games in a, three in b. Tax changes are 120
  against 86, the largest gap. The opportunist accounts for 15 of the 34. Read it as a
  pattern to check, not a result.

**Read with care.** Six games an arm, one map, one model, capped at 40 turns with none finished: an anecdote about
the model (hard rule 2). Never compared with requests 002 (RTX 5080) or 005.

**What it suggests, for the tech lead to decide.** Give the court `budget-1600+trim` when the economy is on. It
keeps every request under 1,600 with nothing measured lost. It buys headroom under `num_ctx` 4,096 as the court
prompt grows, CourtTurnFacts (#405) already adding to it. It is not a speed change, so it should not be sold as
one. No default was changed here.
