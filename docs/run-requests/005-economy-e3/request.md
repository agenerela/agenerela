# 005: the economy's live check, E3

Status: open, for the Phase 4 runs session on the home server (tech lead, 23 November 2026)

**Why.** With `Economy` on (manpower, terrain yields, tax levels, market, barracks, farm), do model courts play it
legally, what does each persona buy over a game and when (the merchant's markets against the warmonger's barracks,
who went to high taxes and when), and what does it cost in prompt tokens, calls and seconds a turn, against free
orders without it? Request 002 (one seed, the RTX 5080) found taxes toggled and almost nothing built; #378 locked a
tax change for some turns since.

**What.** Painted Realm, the `personas` table, `qwen3.5:4b` only, the writs turn with free orders, Classic, capped at
40 turns, one game a block, interleaved by seed:

| Block | `-strategyRules` | Seed (series index) |
|---|---|---|
| x1, x2, x3 | `FreeOrders=true` | 1, 2, 3 (0, 1, 2) |
| e1, e2, e3 | `FreeOrders=true;Economy=true` | as above |

Play order x1 e1, x2 e2, x3 e3. Both settings go in one `-strategyRules` argument (a second replaces the first).
Method `GreyBoxStrategy.Editor.StrategyTableMenu.RunInterleavedFromCommandLine` with `-strategyTable personas
-strategyModel qwen3.5:4b -strategyTurn writs -strategyCap 40 -strategyGames 1 -strategyBlock 1`, on test after
#378. Personas rotate with period 5, so three seeds put each persona on three seats; six seeds would sharpen the
per-persona picture. Smallest slice: x1, e1. About 12 to 16 minutes a block on the home server.

**Machine.** The home server (RTX 4070 SUPER), each block under its own clean lease and the laptop lock
`.scratch/degen-agenerela-lock`, the model warmed first. Never compared with request 002's RTX 5080 numbers.

**What to record.** `python tools/live-runs/economy/report.py <out>`: illegal answers; prompt tokens a court request,
mean and max, against the 1,600 limit; calls and model seconds a turn, e against x; per persona and game, each building
bought (kind, province, turn) and each tax change (level, turn). The gate's commit, the card and each block's lease.
Keep `summary.md`, `model.csv`, `games.csv` and `decisions.jsonl` a block.
