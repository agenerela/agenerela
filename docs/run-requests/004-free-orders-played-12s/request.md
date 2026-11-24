# 004: free orders, the played check at 12 s

Status: done (results.md)

**Why.** With free orders the rulers decide the next turn while the player watches the last one play back. With a
12 s playback (the lead's target), how long does End turn wait for a player who presses it the moment the playback
ends? The headline figure for the lead.

**What.** `GreyBoxStrategy.Realm.Editor.FreeOrdersLiveCheck.RunFromCommandLine -planSeconds 12 -turns 6`, rule mode
with free orders, a stand-in player who plans for 12 s and then gives a plain plan, four `qwen3.5:4b` rulers. Blocks
`p12s1`, `p12s2`, `p12s3` with `-strategySeed` 1, 2, 3. Smallest slice: `p12s1`.

**Machine.** The home server (RTX 4070 SUPER), from Unity on the lead's laptop, each block under its own clean lease,
the model warmed first. Never compared with the RTX 5080's figures.

**What to record.** From `turns.csv`: End turn's wait a turn, the share of it that was treaty terms, how many rulers
were still deciding at End turn, calls and model seconds a turn; a turn with failed calls (`errors` > 0) is left out.
Report: `python tools/live-runs/free-orders/report.py <out>`.
