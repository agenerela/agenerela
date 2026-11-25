# 003: free orders, the court's prompt A/B

Status: done (results.md)

**Replaying it after 24 November.** `CourtTurnFacts` is on by default since then (the lead's call on this run), so
`FreeOrders=true` alone is no longer the control. To repeat the run, give the control blocks
`FreeOrders=true;CourtTurnFacts=false` and the n blocks `FreeOrders=true;CourtTurnFacts=false;CourtNoneWording=true`;
the t blocks are unchanged.

**Why.** Free orders (#20) let a court give as many orders a turn as its gold allows, one request at a time. Before
they can become the default, the tech lead wants to know whether telling a court's later request what its earlier
orders settled (`CourtTurnFacts`: "Taxes are raised this turn", the gold held back) or asking it for "another order, or
none" (`CourtNoneWording`) changes legal answers, court orders a turn, and calls and model seconds a turn. The lead
makes `CourtTurnFacts` the default if it is not worse on legal answers and seconds.

**What.** Painted Realm, the `personas` table, `qwen3.5:4b` only, the writs turn with free orders, Classic, capped at
40 turns, one game a block, arms interleaved by seed:

| Block | `-strategyRules` | Seed (series index) |
|---|---|---|
| c1, c2, c3 | `FreeOrders=true` | 1, 2, 3 (0, 1, 2) |
| t1, t2, t3 | `FreeOrders=true;CourtTurnFacts=true` | as above |
| n1, n2, n3 | `FreeOrders=true;CourtNoneWording=true` | as above |

Play order c1 t1 n1, c2 t2 n2, c3 t3 n3. Method `GreyBoxStrategy.Editor.StrategyTableMenu.RunInterleavedFromCommandLine`
with `-strategyTable personas -strategyModel qwen3.5:4b -strategyTurn writs -strategyCap 40 -strategyGames 1
-strategyBlock 1`. Smallest slice: c1, t1, n1.

**Machine.** The home server (RTX 4070 SUPER, 12 GB, behind its gate), from Unity on the lead's laptop, each block
under its own clean lease, the model warmed first. A third machine: never compare with the RTX 5080's figures.

**What to record.** Illegal answers; court orders a realm a turn; calls and model seconds a turn, each arm against
control on the same seed and turn; the gate's commit, the card and each block's lease. Report:
`python tools/live-runs/free-orders/report.py <out>`.
