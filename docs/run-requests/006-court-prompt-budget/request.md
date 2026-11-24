# 006: the court's prompt budget, trimmed under 1,600 against 2,048 untrimmed

Status: open

**Why.** With the economy on, 35% of Painted Realm's court requests passed 1,600 tokens (mean 1,539, max 2,021) in
request 005, against 8% without it. Nothing cut them: 1,600 was only `report.py`'s yardstick, and Ollama's
`num_ctx` is 4,096. The framework now has a prompt budget (`PromptBudget`, #151) that measures every request and,
only when the developer lists them, trims in a given order. This run asks whether trimming the court's prompt under
1,600 changes how the courts play, and what it saves in tokens and seconds, against the same prompt untrimmed.

**What.** Painted Realm, the `personas` table, `qwen3.5:4b` only, the writs turn with free orders and the economy,
Classic, capped at 40 turns, one game a block, interleaved by seed. Both arms are GreyBoxStrategy's `default` arm
with a court budget; only the court's own requests (label `Court`) carry it:

| Arm | `-strategyArms` | What the court's budget does |
|---|---|---|
| a: trimmed | `default+budget-1600+trim` | While a court request's estimate is over 1,600 tokens: unlist the target lines no court order may take (`PromptTrim.UntakeableTargets`), then drop remembered exchanges, oldest first (`PromptTrim.History`). Each trim is recorded |
| b: untrimmed (control) | `default+budget-2048` | Measures against 2,048 and records it; trims nothing, so its requests are byte for byte today's |

Offline, on request 005's 1,165 economy court requests, the two trims would leave 1 of them over 1,600 (max about
1,610), and a mean near 1,300; about 40% of requests would be trimmed at all.

| Block | Arm | Seed (series index) |
|---|---|---|
| a1, b1 ... a6, b6 | a, b | 1 to 6 (0 to 5) |

Play order a1 b1 a2 b2 ... a6 b6. Six seeds an arm: personas rotate with period 5, so every persona sits at least six
times an arm; request 005's battles a game ran 82 to 117 over three seeds, so six a side tells a difference of about
20 battles from noise. Twelve blocks, about 3 hours at request 005's 13 to 17 minutes a block.

Method `GreyBoxStrategy.Editor.StrategyTableMenu.RunInterleavedFromCommandLine` with `-strategyTable personas
-strategyModel qwen3.5:4b -strategyTurn writs -strategyCap 40 -strategyGames 1 -strategyBlock 1
-strategyRules FreeOrders=true;Economy=true` (both in **one** `-strategyRules`), `-strategyArms` as above with
`-strategyArmNames a` or `b`, and `-strategySeed`/`-strategyIndex` as in the table. With the free-orders runner kit
(`run.sh`, `block.sh`, see request 005), a spec is:

    C="-strategyTurn writs -strategyCap 40 -strategyRules FreeOrders=true;Economy=true"
    "a1|1|0|watch|$C -strategyArms default+budget-1600+trim" "b1|1|0|watch|$C -strategyArms default+budget-2048" ...

If the kit's `watch` already passes `-strategyArms default`, replace that argument rather than adding a second.

**Code.** test at or after the merge of the framework pull request that adds `PromptBudget` and GreyBoxStrategy's
`+budget-<n>` and `+trim` modifiers (branch `prompt-budget`). Check out a fresh short-path worktree and import it
once outside the lease.

**Machine.** The home server (RTX 4070 SUPER), each block under its own clean lease and the laptop lock
`.scratch/degen-agenerela-lock`, `qwen3.5:4b` warmed first, nothing else on the card. Mains power. Record the
network path per block (LAN or VPN): compare a and b within a pair only if it changes mid-series. Never compared
with request 002 (RTX 5080) or 005's numbers.

**What to record.** Keep `summary.md`, `model.csv`, `games.csv` and `decisions.jsonl` a block, with the prompt in the
log as 005 had it. Run `python tools/live-runs/economy/report.py <out> --control b --limit 1600` and keep its output.
The gate's commit, the card, each block's lease and its start and end. Then send the data folder's path to the
requesting session (prompt-budget), which writes `results.md` and the findings entry from it:

- illegal answers, all requests and court requests, per arm;
- court prompt tokens (Ollama's `prompt_eval_count`) mean and max, how many over 1,600, and in arm a how many were
  trimmed, by which trim (`promptTrims` in `decisions.jsonl`) and how many stayed over (`overBudget`);
- buildings bought by persona and tax changes (report.py), battles a game, provinces each country holds at the end;
- calls and model seconds a turn, a against b on the same seed and turn;
- the estimate against the count: `promptTokensEstimated` against `promptTokens` on every court request.

**Rules.** Control arm b, same machine and session (hard rule 2); one model resident (rule 3); no default changes:
whether the court keeps a budget, and which, is the tech lead's call after this run.
