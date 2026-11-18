# A run queue on the RTX 5080 machine: design

**Status:** design accepted by the lead, 18 November 2026; written by the framework tech lead at
his request. **Built** on 18 November (steps 1, 3 and 4 below, and step 2's two checks): how to use
it is [`README.md`](README.md); what differs from this page is in "As built" at the end. It is written for the agent that will build it **on the RTX 5080 machine**, who has
not seen the conversations behind it. Read [`AGENTS.md`](../../../AGENTS.md) first, then
[`tools/ollama-server/README.md`](../../ollama-server/README.md), then this page.

The lead's words: "Maybe we can change the ollama container on rtx 5080 to save progress and have
it as like runs we can pause switch and etc.?", then "Lets do the headless runner on the 5080
machine and also lets design the updates for the Resumable runs and a run queue."

## The problem

A live run today is driven from the lead's laptop: a shell script there starts Unity in batch
mode, Unity plays a game, and every model call goes over the network to Ollama on the RTX 5080
machine. The Ollama container keeps nothing between requests, so a run's progress lives only in
that script on the laptop.

What that has cost:

- **A sleeping laptop stops the run.** On 17 November a played block stalled when the laptop slept
  and woke off its network; the script's timeouts never fired, and the card's lock was held for
  19 hours with nothing running.
- **A run cannot be paused or reordered** except by killing it. A stopped run starts again from
  its first block unless its script happens to skip finished ones.
- **The laptop is busy while a run plays**: one Unity process and a few gigabytes for hours, on a
  31 GB machine that also runs the sessions' editors, and on battery when the lead is out.
- **The run scripts are copies.** Each kit under a session's `.scratch/` has its own
  `run_blocks.sh` with the lock, the pause check and the unload written out again.

## What to build, in three parts

1. **A runner on the RTX 5080 machine** that plays the games there, beside Ollama, so the laptop
   only asks and reads.
2. **Resumable runs:** a run is a list of blocks with a status each, and can stop after any block
   and continue later without redoing finished ones.
3. **A queue:** the runs that are owed, in order, which can be paused, reordered and switched.

All three live in one kit, `tools/live-runs/runner/`, in Python with the standard library only
(as `tools/benchmarks/` and the other kits), so the same code runs a queue on the RTX 5080 machine
and a single run on a laptop.

### Decisions already made

| Question | Decision | Why |
|---|---|---|
| Where is run state kept? | In files written by the runner, never in the Ollama container | The container only answers requests. Changing it would not save a run, and its image is pinned so results stay comparable |
| How does the runner play a game? | Unity 6000.3.23f1 in batch mode on the RTX 5080 machine's own Windows, the same command the kits use today | No change to the game or to what a model is sent, so earlier findings stay comparable. A Linux player in a container would need the table runner moved out of Editor code first; that is a later option, below |
| How do requests reach the machine, and results come back? | Through git: a request is a folder on `test`; results are pushed to a branch of their own | No new network service, everything is reviewable, and the machine needs no inbound access beyond what it has |
| Who may the runner push to? | Only branches named `runs/<request>`. Never `test`, never `main`, never a tag | Findings and defaults are written and merged by the people and sessions that asked |
| What is one block? | One Unity start: one arm, a first seed, a number of games | It is the smallest piece that can be redone, and what the kits already use |

## Part 1: the runner on the RTX 5080 machine

### What the machine needs (the lead does these, once)

- **Unity 6000.3.23f1** installed through the Hub, with the lead signed in so batch mode has a
  licence. No build modules are needed.
- **Git and Git LFS**, with the lead's GitHub login able to push to `agenerela/agenerela`.
- **Python 3.8 or later.**
- **A checkout** at a short path, for example `C:\agenerela-runs\repo`, and a work folder
  `C:\agenerela-runs\work` (short paths matter: Unity fails on long ones).
- **The Ollama stack running** as [`tools/ollama-server/README.md`](../../ollama-server/README.md)
  describes, with its `.env` already there.
- The machine set **never to sleep** while plugged in.

The agent building this installs nothing system-wide without the lead's yes, touches only this
stack's containers (never the lead's own ComfyUI or any other container), never runs a
`docker ... prune`, and never opens, prints or logs a `.env` or a key.

### How it runs a block

For each block the runner:

1. Reads the request (below) and makes sure a git worktree exists at the request's commit, under
   `work\<short sha>\`. A second request at another commit gets its own worktree, so switching
   between runs is switching folders, and each keeps its imported `Library`.
2. Checks the card is quiet: `python tools/ollama-server/ollama_remote.py ps` says `none` or only
   the request's model, and the asset server's queue is empty (`python tools/comfyui/comfy.py
   check`, then `free` so its models are not resident). If the lead's own ComfyUI is using the
   card, it waits and says so in its status.
3. Sets the provider's address to the gateway on this machine, `http://127.0.0.1:11436`, and the
   key from `tools/ollama-server/.env` into the environment of the Unity process only. The key
   never appears on a command line, in a log or in a status file.
4. Starts Unity with the request's command, for example:

   ```text
   Unity.exe -batchmode -nographics -projectPath <worktree>\Demos\GreyBoxStrategy
     -executeMethod GreyBoxStrategy.Editor.StrategyTableMenu.RunInterleavedFromCommandLine
     -strategyTable personas -strategyModel qwen3.5:4b -strategyArms <arm> -strategyArmNames <block>
     -strategyGames <n> -strategySeed <seed> -strategyBlock <n> -strategyIndex <index>
     -strategyOut <results folder> <extra args> -logFile <results folder>\<block>.log
   ```

5. Watches it. A block has an estimate in minutes; **at twice the estimate the runner kills that
   Unity process, unloads the model and marks the block `invalid: timeout`**. A block whose log
   shows provider failures is marked `invalid: provider`. An invalid block is retried once, then
   left for a person.
6. On a clean exit, checks the block's expected output exists (the request names it, for example
   `<block>\summary.md`), marks the block `done`, unloads the model (`ollama_remote.py unload
   --all`) and writes the manifest.

One Unity process at a time. One model resident at a time (hard rule 3).

### How the laptop's sessions and the runner share the card

The laptop's sessions take turns with a lock folder on the laptop
(`.scratch/ollama-5080-lock/`), which the RTX 5080 machine cannot see. So:

- **The runner publishes whether it is using the card** in `status.json` (below), and a laptop
  session checks it before taking the laptop's lock: `python tools/live-runs/runner/runq.py status`.
- **The runner never starts a block while another machine has a model loaded**: step 2 above sees
  it in `ps` and waits.
- **While the queue is running, the card is the runner's.** A laptop session that needs the card
  (a live check in the editor, a picture or a sound from the asset server) pauses the queue first
  and resumes it after. A pause takes effect after the block in hand.

### Later option: a container instead of the Windows editor

A Linux player of the game in a container would need no Unity licence on the machine and could run
several games at once on the CPU. It needs the table runner
(`GreyBoxStrategy.Editor.StrategyTableMenu`, Editor code today) reachable from a player build, and
Linux build support on a machine that builds it. Not in this work. If it is ever done, a request's
`runner` field says which kind ran, and results from the two kinds are not compared on time.

## Part 2: resumable runs

### The request

A request is a folder on `test`, as [`docs/run-requests/README.md`](../../../docs/run-requests/README.md)
already says, with one more file the runner reads:

`docs/run-requests/NNN-<name>/run.json`

```json
{
  "id": "007-free-orders-prompt-ab",
  "question": "Do the court's turn facts or the none wording change legal answers and calls a turn under free orders?",
  "commit": "<full sha on test the run must play>",
  "project": "Demos/GreyBoxStrategy",
  "method": "GreyBoxStrategy.Editor.StrategyTableMenu.RunInterleavedFromCommandLine",
  "model": "qwen3.5:4b",
  "common_args": "-strategyTable personas -strategyTurn writs -strategyCap 40",
  "blocks": [
    { "name": "c1", "arm": "default", "seed": 1, "index": 0, "games": 1, "extra": "-strategyRules FreeOrders=true",                       "minutes": 6 },
    { "name": "f1", "arm": "default", "seed": 1, "index": 0, "games": 1, "extra": "-strategyRules FreeOrders=true;CourtTurnFacts=true",   "minutes": 6 },
    { "name": "w1", "arm": "default", "seed": 1, "index": 0, "games": 1, "extra": "-strategyRules FreeOrders=true;CourtNoneWording=true", "minutes": 6 }
  ],
  "done_when": "{block}/summary.md",
  "smallest_slice": ["c1", "f1", "w1"],
  "report": ["python Demos/GreyBoxStrategy/Tools/report.py {out}"],
  "keep": ["**/summary.md", "**/scorecard.json", "**/*.recording.json"],
  "notes": "Arms are interleaved by seed, so a pause never leaves one arm ahead of another."
}
```

Rules for a request:

- **The commit is pinned.** The runner plays exactly that commit. A request is never edited to
  match what was run; if it must change, it is a new request.
- **Blocks are listed in the order to play them, arms interleaved**, so stopping after any block
  leaves the arms as balanced as they can be.
- **`smallest_slice` runs first** and the runner reports it before going on, as the laptop's
  sessions do today.
- **`keep`** says which outputs come back through git. Everything else stays on the machine.

### The manifest

Beside the results, the runner keeps `manifest.json`, rewritten after every change by writing a
temporary file and renaming it, so a crash never leaves half a file:

```json
{
  "id": "007-free-orders-prompt-ab",
  "commit": "…",
  "runner": "unity-editor-batch on RTX5080",
  "ollama": { "version": "0.40.14", "model": "qwen3.5:4b", "size_vram": 3300000000 },
  "blocks": [
    { "name": "c1", "status": "done",    "started": "…", "ended": "…", "minutes": 5.4, "tries": 1 },
    { "name": "f1", "status": "running", "started": "…", "pid": 1234 },
    { "name": "w1", "status": "planned" }
  ]
}
```

A block's status is one of `planned`, `running`, `done`, `invalid: <why>`, `skipped: <why>`.

Resuming is reading the manifest and starting at the first block that is not `done`:

- A block found `running` when the runner starts is a block the last run did not finish: its
  process is killed if it is still there, its partial output is moved aside, and it goes back to
  `planned`.
- A `done` block is never run again unless a person deletes its entry.
- The request's commit must match the manifest's. If it does not, the runner refuses and says so.

### The same script on a laptop

`runq.py run docs/run-requests/NNN-<name>` plays one request on whatever machine it is on, with
the same manifest and the same rules, taking the laptop's lock folder per block and checking the
laptop's `pause` file and its battery between blocks (under 25%: stop after the block in hand).
The sessions' own `run_blocks.sh` copies are then retired in favour of it.

## Part 3: the queue

### The queue file

`docs/run-requests/QUEUE.json`, on `test`:

```json
{
  "paused": false,
  "pause_reason": "",
  "order": ["007-free-orders-prompt-ab", "008-free-orders-played-12s", "009-economy-live-check"],
  "hold": ["010-forts-siege-income"]
}
```

- **`order`** is what runs next, first to last. The runner finishes the request in hand before
  taking the next, unless the order changes.
- **Switching:** moving another request to the front of `order` makes the runner switch to it
  after the block in hand. The request it left keeps its manifest and continues when its turn
  comes again.
- **`paused: true`** stops the runner after the block in hand; it unloads the model and waits.
- **`hold`** lists requests that exist but must not run yet (waiting on a merge, or on the lead).

Anyone who may push docs to `test` changes the queue with a commit: the lead, or a session on the
laptop. The commit message says why. That makes every pause and reorder visible in the history.

### The runner's loop

```text
forever:
  git fetch; read QUEUE.json and every request's run.json at origin/test
  if paused, or order is empty:        write status; wait a minute; continue
  pick the first request in order that has a block not done
  play its next block (Part 1)
  after the block: write the manifest and the status; push results if the request finished
                   or thirty minutes have passed since the last push
```

A local file on the machine, `work\pause`, pauses it too, for the lead sitting at that machine.

### Status and results

- **`status.json`**, rewritten after every block and every minute while waiting: the request and
  block in hand, how many blocks are done of how many, the estimate of what is left, the last
  error, whether the card is in use by the runner, the time. No key, no address.
- **Results come back on a branch `runs/<request id>`**, cut from the request's commit, holding
  `docs/run-requests/<id>/results/` with the manifest, `status.json`, the files `keep` names and
  the outputs of `report`. The runner commits there and pushes; it never opens a pull request.
  Files over 5 MB each, or 50 MB a request in total, stay on the machine and are listed in
  `results/LEFT_ON_MACHINE.md` with their sizes.
- **Reading it from the laptop:** `python tools/live-runs/runner/runq.py status` fetches
  `runs/*` and prints one line a request. A session then writes `results.md` and the
  `findings.md` entry from the branch, in a pull request into `test`, as run requests already ask.

A later step, if polling git proves too slow: serve `status.json` read-only through the existing
gateway (Caddy on port 11436, the same bearer key). Not in the first version.

## The commands

```text
runq.py serve                      on the RTX 5080 machine: the loop above
runq.py run <request folder>       anywhere: play one request, resumable
runq.py status                     anywhere: the queue, each request's progress, whether the card is in use
runq.py pause "<why>" | resume     change QUEUE.json in a commit (asks before pushing)
runq.py front <request id>         move a request to the front of the order (asks before pushing)
runq.py check <request folder>     validate a run.json without running: fields, the commit exists, estimates add up
```

## What must stay true (measuring)

From [`docs/llm-wiki/measuring.md`](../../../docs/llm-wiki/measuring.md) and the hard rules:

- **Both arms of a comparison run in the same request, on the same machine, interleaved.** The
  queue never splits a request's arms across machines.
- **The Ollama image, its settings and the model are recorded in the manifest** with `size_vram`.
- **Answers from the runner are comparable with laptop-driven runs** against the same server:
  the model, the server and the game are the same. **Times are not**: a call from the machine
  itself has no network between it and the gateway. A finding says which kind of runner played it.
- **A pause never changes a result.** It happens between blocks, and a block is one whole game or
  set of games.
- **No default changes.** The runner reports; a person decides.

## Files to add

```text
tools/live-runs/runner/
  runq.py              the commands above
  runner/…             modules: request, manifest, queue, card (ps, unload, comfy check), unity (start, watch, kill), gitio
  README.md            for a person: what it is, how to ask for a run
  AGENT_SETUP.md       for the agent on the RTX 5080 machine: setting it up, the checks, what never to touch
  AGENT_USAGE.md       for a session on a laptop: writing a request, the queue, reading results
  tests/               standard-library unittest: manifest resume, queue order and switch, timeout, the request validator
docs/run-requests/QUEUE.json
docs/run-requests/README.md      a section on run.json and the queue
tools/ollama-server/AGENT_USAGE.md   rule 4 gains "check runq.py status first"
```

## Build it in this order

Each step is a pull request into `test` from a branch named `runner-<step>`, with its tests, and
ends with something a person can run.

1. **`runq.py check` and `run`, with the manifest.** Prove resume with a fake block command (a
   script that sleeps and writes a file): stop it mid-run, start again, and only the unfinished
   blocks play. Prove the timeout kill. No Unity, no GPU.
2. **A real request on the RTX 5080 machine, by hand:** `runq.py run` on a one-block request with
   the game's scripted players (no model), then a one-pair model request (two blocks, a few
   minutes). Record seconds a turn beside a laptop-driven run of the same seed.
3. **The queue and `serve`:** `QUEUE.json`, pause, resume, front, the results branch, `status`.
   Prove a pause and a switch on fake blocks, then on the one-pair request.
4. **`AGENT_SETUP.md`, `AGENT_USAGE.md`, the README and the changes to the two existing docs.**
5. **Move the first owed runs onto it** (the sessions on the laptop write their `run.json`).

## Done looks like

- The lead closes the laptop with three requests in the queue; in the morning their results are on
  three `runs/…` branches, and nothing waited on the laptop.
- `runq.py pause "playing a game"` from the laptop stops the queue after the block in hand, and
  `resume` continues it at the next block, with no block played twice.
- A request moved to the front runs next, and the one it interrupted finishes later from where it
  stopped.
- A block that hangs is killed at twice its estimate, the model is unloaded, and the queue goes on.
- No key appears in any commit, log, manifest or status file.

## The lead's answers (18 November 2026)

The lead accepted all four proposed answers ("Your question answers are okay regarding the rtx 5080
runner, I will start the changes there"):

1. **Unity may be installed and signed in on the RTX 5080 machine.**
2. **The runner may push to `runs/*` branches** with the lead's GitHub login on that machine, and
   to those branches only.
3. **While the queue runs, asset generation and live editor checks pause the queue first.**
4. **Raw logs on the machine are kept 30 days**; everything `keep` names stays on the results
   branches for good.

## As built (18 November 2026)

Built on the RTX 5080 machine in one pull request rather than one a step: steps 3 and 4 build on
step 1's modules, and the tests for all three run in one suite. What differs from the page above:

- **A flag goes in once.** The game's `Arg()` returns the first copy of a repeated flag, so the first
  example above, with `-strategyRules` in both `common_args` and `extra`, would have played every arm
  as `FreeOrders` alone. The example is corrected, and `runq.py check` refuses a request that repeats
  a flag, including one the runner sets itself (`-strategyModel`, `-strategyOut`, ...).
- **Three more request fields:** `block_args` (the per-block template; the default is the command in
  Part 1), `command` (a whole command instead of `project` and `method`, used by the tests), and
  `invalid_if_log` (the log text that makes a block `invalid: provider`; the default is the
  framework's `ProviderFailed`). `model` may be empty, for scripted players: such a request neither
  waits for the card nor takes it.
- **The lead's own ComfyUI is noticed by memory**, since it is another container the runner must not
  inspect: the card's memory in use beyond what Ollama reports must be under
  `AGENERELA_RUNNER_MAX_OTHER_MB` (6000 by default; the desktop alone uses about 3,000).
- **`runq.py run` refuses while the queue is running** (`--ignore-queue` overrides), so a laptop run
  never races the runner. It takes `--on-server` on the RTX 5080 machine, `--push` to push its results
  as `serve` does, and `--checkout` to play in a checkout already at the commit.
- **An interrupted try is not a try.** A block cut short by a crash, a reboot or Ctrl+C goes back to
  `planned` with its tries unchanged and a note in `interrupted`; only timeouts, failures and missing
  output count towards the one retry.
- **`pause`, `resume` and `front` commit in a throwaway worktree** at `origin/test`, so the caller's
  checkout and branch are untouched, and retry once if `test` moved. `--yes` skips the question, for
  an agent that has the user's yes in the chat.
- **`serve` does not update its own code**; AGENT_SETUP.md says how to pause, pull and restart.

**Step 2, by hand:** a scripted block and a model pair (the 16 November `n1` and `l1` at 42e0ec8)
both played through `runq.py run --on-server`; the pair's results are on `runs/901-runner-model-pair-check`
(findings, 18 November, "The run queue's first blocks"). The laptop-driven pair of the same seed on
the same day is still owed. **Step 3 on the real queue**, a pause and a switch through `QUEUE.json`
on `test`, needs a request on `test` and is owed; the same paths are proven on fake blocks by the tests.
**Step 5**, moving the first owed runs onto it, is for the sessions that own them.
