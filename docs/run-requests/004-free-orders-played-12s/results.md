# 004: results

**Played** on 23 November 2026, in the same session, worktree and conditions as request 003 (test `c544c13`, the
home server's RTX 4070 SUPER, gate `5728506a6d6b`, `qwen3.5:4b` warmed first). Clean leases: p12s1 #3, p12s2 #5814,
p12s3 #9970, each taken with nobody waiting and released at the block's end; about 2 minutes a block. No turn had a
failed call. Raw data: `.scratch/free-orders-runs-2026-11-23/data/p12s*/turns.csv` in the lead's main checkout.

| End turn waited, s | Turn 1 | 2 | 3 | 4 | 5 | 6 | Mean |
|---|---:|---:|---:|---:|---:|---:|---:|
| p12s1 | 18.49 | 0.00 | 1.56 | 0.00 | 0.91 | 2.22 | 3.86 |
| p12s2 | 1.95 | 0.00 | 0.76 | 0.82 | 0.10 | 2.50 | 1.02 |
| p12s3 | 3.90 | 0.00 | 0.81 | 0.00 | 1.80 | 0.00 | 1.08 |

- **All 18 turns: 1.99 s on average, 18.49 s at most.** Turns 2 to 6: 0.76 s on average, 2.50 s at most.
- **Turn 1 is the wait.** The rulers start deciding only when the game opens, so turn 1 has no playback to hide
  behind: three or four of the four rulers were still deciding at End turn. p12s1's turn 1 took 30.4 model seconds,
  the first block of the session.
- **What is left after turn 1 is mostly treaty terms** (2.5 to 3.3 s of each game's total), asked once the player has
  pressed End turn.
- Calls a turn 13.7 to 14.3; model seconds a turn 11.3 to 14.5.

**Read with care.** Three seeds of six turns, one machine; a measure of this machine's wait, not of the model.
