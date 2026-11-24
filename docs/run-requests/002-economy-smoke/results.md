# 002: results

**Played** by the run queue on the RTX 5080 machine (`unity-editor-batch on the RTX 5080 machine
(DraganoiderPC2), gateway on 127.0.0.1`), at commit `e56f973`, on 18 November 2026: `qwen3.5:4b`,
Ollama 0.40.14, `size_vram` 3,140,495,604 (all of it on the GPU). Raw files are on the branch
`runs/002-economy-smoke`; the two `decisions.jsonl` (12 and 16 MB) stay on the runner for 30 days.

**The queue.** Both blocks finished at the first try: `x1` 10.4 minutes, `e1` 10.9. A request sent
from the laptop played end to end with no one at either machine.

| | x1, free orders | e1, free orders + economy |
|---|---:|---:|
| Decisions (council requests) | 1,007 (456) | 1,208 (448) |
| Illegal answers, not offered, errors | 0, 0, 0 | 0, 0, 0 |
| Calls a turn | 25.2 | 30.2 |
| Model seconds a turn (on the machine, no network) | 12.9 | 15.3 |
| Mean council prompt tokens (max) | 1,371 (1,725) | 1,480 (1,934) |
| Refused at resolution | 24 | 55 |

**What the courts did with the economy (e1, 448 council answers):**
- **Taxes: 79 changes,** almost all high and normal in turn: every realm set high at turn 1, and
  most went back and forth every turn or two all game (Kesh 23 changes, Merrow and Orsk 17). Six
  answers chose low.
- **Buildings: 2 in 40 turns,** a barracks (Valen, turn 17) and a farm (Orsk, turn 38). No
  market, the merchant included.
- Recruits about the same as without the economy (135 against 140).

**Read with care.** One seed, one game an arm: an anecdote, not a finding about the model
(hard rule 2). What it does show is a rule problem: a tax change is free and offered every turn,
so the courts toggle it; and the building orders, as worded and priced, are almost never chosen.
