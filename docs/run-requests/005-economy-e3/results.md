# 005: results

**Played** on 23 November 2026, 16:09 to 17:44 local, from Unity in batch mode on the lead's laptop (a detached
worktree at test `9ab0e1b`, after #378 and #386), against the home server: NVIDIA GeForce RTX 4070 SUPER 12 GB,
Docker Ollama 0.40.14 behind the gate (commit `5728506a6d6b` for every block), over the home network.
`qwen3.5:4b` only, warmed before each block. Each block held its own clean lease and the laptop lock
`.scratch/degen-agenerela-lock`, with nobody waiting: x1 #12196, e1 #13098, x2 #14045, e2 #15219, x3 #16328,
e3 #17479. Blocks took 13 to 17 minutes; none was voided or replayed. Played with the free-orders runs session's
runner (`run.sh`, `block.sh`), as the request says.

**Raw data** in the lead's main checkout, `.scratch/e3-economy-2026-11-23/data/` (`summary.md`, `model.csv`,
`games.csv`, `decisions.jsonl` a block; `report.txt` is `tools/live-runs/economy/report.py`'s output).

| | x: free orders | e: free orders + economy |
|---|---:|---:|
| Decisions (court requests) | 3,215 (1,230) | 3,091 (1,165) |
| Illegal answers | 2 (0.1%), 1 of them a court's | 0 |
| Court prompt tokens, mean (max) | 1,403 (1,769) | 1,539 (2,021) |
| Court requests over the 1,600 limit | 103 of 1,230 (8%) | 411 of 1,165 (35%) |
| Calls a turn (max) | 26.8 (47) | 25.8 (42) |
| Model seconds a turn (max) | 23.8 (195.2) | 20.9 (32.1) |
| e against x, same seed and turn (120 turns) | | calls −1.03, model s −2.85 |
| Battles a game, seeds 1 / 2 / 3 | 75 / 81 / 98 | 117 / 109 / 82 |
| Provinces changing hands a turn, seeds 1 / 2 / 3 | 0.5 / 0.8 / 0.6 | 1.9 / 1.5 / 0.9 |
| Peace made a game, seeds 1 / 2 / 3 | 19 / 15 / 16 | 4 / 18 / 19 |

The x arm's worst turn, 195 s, is x1's turn 21, where two court requests took about 90 s each. Those two account
for about 1.5 s of the 2.85 s a turn by which the economy arm is faster. The rest is within what one seed's game
plays differently. No block warned or failed.

**What the courts built and taxed (e, 3 games, every persona on 3 seats):**

| Persona | Barracks | Farms | Markets | Tax changes (to high, to normal) | First purchase, turns |
|---|---:|---:|---:|---|---|
| merchant | 3 | 3 | 12 | 13 (7, 6) | 1, 3, 4 |
| warmonger | 8 | 3 | 0 | 6 (4, 2) | 3, 4, 5 |
| opportunist | 7 | 1 | 0 | 7 (5, 2) | 3, 4 |
| proud | 9 | 6 | 0 | 13 (7, 6) | 8, 21, 36 |
| builder | 4 | 2 | 1 | 10 (6, 4) | 13, 15; none in e3 |

- **The courts play the economy legally:** 0 illegal answers in 3,091 decisions.
- **They build, and by persona:** 59 buildings in three games. The merchant buys 12 of the 13 markets, starting on
  turns 1 to 4; the warmonger and the opportunist buy barracks early; the proud buys late (turns 8, 21, 36). The
  builder buys least and latest, and nothing at all in e3, the reverse of its name.
- **Taxes are set high at turn 1 by almost every court and changed about 3 times a seat after**, never to low:
  49 changes over 15 seats and 40 turns. With the lock of #378, none flips every turn or two.
- **The prompt grows past its limit.** A court request with the economy is 136 tokens longer on average, and 35% of
  them pass the 1,600 limit (max 2,021), against 8% without.
- **More fighting:** battles 103 against 85 a game, and land changing hands about twice as often. Three seeds an
  arm; read it as a pattern to check, not a result.

**Read with care.** Three games an arm, one map, one model, games capped at 40 turns with none finished: an
anecdote about the model (hard rule 2), and a description of what the rules let it do. Never compared with
request 002, which ran on the RTX 5080.

**What it suggests, for the lead to decide:** the prompt limit needs a look before the economy goes on by default
(35% of court requests over 1,600 tokens); the builder persona's wording does not make it build; six seeds would
settle the per-persona picture and the battle count.
