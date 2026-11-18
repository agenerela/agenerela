# 002: economy smoke, the first request from the laptop

Status: open

**Why.** The run queue on the RTX 5080 machine (#376, #377) has played its own check (901). This is
the first request sent from another machine: it tests the queue end to end, and it is the smallest
slice of the economy's live check (E3), so its numbers are worth reading on their own.

**What.** Painted Realm's writs turn with free orders, five model rulers with personas on
`qwen3.5:4b`, the Classic map, seed 1, capped at 40 turns: once without the economy (`x1`, the
control) and once with it (`e1`). `run.json` beside this file is what the runner plays.

**What to record** (the tech lead, from the `runs/002-economy-smoke` branch):
- whether both blocks finished at the first try, their minutes, the Ollama version and `size_vram`;
- illegal answers in each block;
- in `e1`: which buildings each persona bought and when, and each tax change;
- calls and model seconds a turn, `x1` against `e1`.

One seed is an anecdote, not a finding (hard rule 2). A default changes only with the full E3 run.
