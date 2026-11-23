# The run queue

Live runs played on the RTX 5080 machine itself, beside Ollama, so the laptop only asks and reads.
A run is a list of blocks that can stop after any block and continue later; the runs owed wait in
a queue on `test` that anyone can pause, reorder and switch. [`DESIGN.md`](DESIGN.md) is the
design and why; this page is how to use it.

| You are | Read |
|---|---|
| The agent setting it up on the RTX 5080 machine | [`AGENT_SETUP.md`](AGENT_SETUP.md) |
| A session on the laptop asking for a run or reading one | [`AGENT_USAGE.md`](AGENT_USAGE.md) |
| A person | this page |

## On the RTX 5080 machine: the tray app

**Double-click `tools/live-runs/runner/tray.cmd`.** A coloured dot with an **A** appears in the
notification area (it may be under the ^ arrow), and the runner plays the queue for as long as the
tray is open. Close the tray and the runner stops: nothing ever plays without it.

| Dot | Means |
|---|---|
| green | playing a block (hover for which) |
| amber | paused, or waiting for the GPU |
| grey | idle: nothing owed |
| red | the runner stopped or keeps failing (see the log from the menu) |

Left-click for a one-line status; right-click for the menu:

| Menu item | Does |
|---|---|
| **Pause after this block** | finishes the block in hand, then waits |
| **Stop now (free the GPU)** | kills the block in hand and unloads the model within seconds; the block plays again from its start when you resume, so no result is spoiled |
| **Resume** | carries on with the next block |
| **Game mode: pause while I play** | on by default (below) |
| **Show the queue...** | a window with `runq.py status`, refreshed every minute, with buttons to pause, resume or reorder the shared queue (each asks before it pushes to `test`) |
| Show status, Open results folder, Open the runner log | |
| **Start with Windows** | a shortcut in your Startup folder, so the tray opens at logon; untick to remove it |
| **Quit** | stops the block in hand, frees the GPU, closes the tray |

**Game mode** stops the block in hand and pauses as soon as a game runs, and resumes three minutes
after it closes. A game is a program started from a game library folder (Steam's
`steamapps\common`, Epic Games, XboxGames, GOG, Riot, EA, Ubisoft), a program on your own list, or
any window covering the whole screen. Programs that do that but are not games (Claude, browsers,
editors, Remote Desktop, Wallpaper Engine) are never counted. When it gets one wrong, the menu shows
**Never count X as a game**. **Game mode settings...** opens `tray.json` in the work folder:

| Setting | Default |
|---|---|
| `game_mode` | `true` |
| `fullscreen_is_a_game` | `true` |
| `resume_after_minutes` | `3` |
| `game_folders`, `games`, `never_a_game` | the lists above; `games` is your own, by exe name such as `eldenring.exe` |
| `start_runner` | `true`: start the runner when the tray opens |

If you resume by hand while a game is running, game mode leaves that game alone until it closes.

**For scripts:** a file `tray-command` in the work folder holding one word, `pause`, `stop`,
`resume`, `queue` or `quit`, does what the menu does. `tray.ps1 -SelfTest` checks the control files
and game detection (with a fake game in a fake `steamapps\common`) without a screen.

## In one minute

1. **Ask for a run** by adding `docs/run-requests/NNN-<name>/run.json` beside its `request.md`
   ([the format](../../../docs/run-requests/README.md#runjson-a-request-the-runner-can-play)), and
   checking it: `python tools/live-runs/runner/runq.py check docs/run-requests/NNN-<name>`.
2. **Queue it**: put its id in `order` in [`docs/run-requests/QUEUE.json`](../../../docs/run-requests/QUEUE.json)
   and push both to `test`, or push the request and run `runq.py front NNN-<name>`.
3. **The runner plays it** on the RTX 5080 machine, one block at a time, and pushes what the
   request keeps to a branch `runs/NNN-<name>`: when it finishes, and every thirty minutes before.
4. **Read it**: `runq.py status` prints the queue and each request's progress. Someone then writes
   `results.md` and the `findings.md` entry from that branch, in a pull request into `test`.

## The commands

```text
runq.py serve                      on the RTX 5080 machine: play the queue (the tray app runs it for you)
runq.py run <request folder>       anywhere: play one request, resumable
runq.py status                     anywhere: the queue, each request's progress, whether the card is in use
runq.py pause "<why>" | resume     change QUEUE.json on test in a commit (asks before pushing)
runq.py front <request id>         move a request to the front of the order (asks before pushing)
runq.py check <request folder>     check a run.json without running it
```

`pause`, `resume` and `front` change `QUEUE.json` in a commit on `test` made in a throwaway
worktree, so your own checkout and branch are untouched. They show the commit and ask before
pushing; `--yes` skips the question. Python 3.8 or later, standard library only.

## What a block is, and what happens to it

A block is one Unity start: one arm, a first seed, a number of games. For each block the runner
waits for a quiet card (no other model resident, the asset server idle), starts Unity in batch
mode with the request's command, and watches it.

| Status | Means |
|---|---|
| `planned` | not played yet |
| `running` | playing now, or the runner stopped mid-block (it is set aside and planned again when the runner starts) |
| `done` | exited cleanly, wrote what `done_when` names, and its log shows no provider failure |
| `invalid: timeout` | still running at twice its estimate: killed, the model unloaded |
| `invalid: provider` | its log shows the model could not be asked |
| `invalid: exit <n>`, `invalid: no <file>` | Unity failed, or the expected output is missing |

An invalid block is tried once more, then left for a person. A done block never plays again unless
someone deletes its entry from `manifest.json`. A pause, local or in the queue, takes effect after
the block in hand, so it never changes a result.

## Where things are

| What | Where |
|---|---|
| Each request's manifest, logs and outputs | `<work>/results/<request id>/` on the machine that played it |
| Partial output of a block that was cut short | `<work>/results/<request id>/_aside/` |
| What the runner is doing | `<work>/status.json`, and in each `runs/*` branch |
| The worktree a request plays in | `<work>/<first 12 of its commit>/`, kept so the next block reuses its `Library` |
| Results that come back | branch `runs/<request id>`, folder `docs/run-requests/<request id>/results/` |

`<work>` is `AGENERELA_RUNNER_WORK`, else `--work`, else `.scratch/runner/` in the main checkout.
On the RTX 5080 machine it is `C:\agenerela-runs\work`. Files over 5 MB, or past 50 MB a request,
stay on the machine and are listed in `results/LEFT_ON_MACHINE.md`; raw logs there are kept 30 days.

## Tests

```bash
python -m unittest discover -s tools/live-runs/runner/tests
```

No Unity, GPU or network: the blocks are `tests/fake_block.py`. They prove resume after a hard
kill, the timeout kill and its one retry, a pause, Stop now, the tray closing, a switch, the queue
file's rules, the request validator and what goes on a results branch. The tray's own check is
`powershell -NoProfile -ExecutionPolicy Bypass -File tools/live-runs/runner/tray.ps1 -SelfTest`.
