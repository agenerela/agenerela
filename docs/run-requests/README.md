# Run requests

**A run that one machine should do and another session will read.** Each request is a folder
here with two files, and a third when the run queue on the RTX 5080 machine plays it
([below](#runjson-a-request-the-runner-can-play)):

| File | Who writes it | What is in it |
|---|---|---|
| `request.md` | Whoever asks: a session, or the lead | What to run, on which machine, step by step, and what to record. Precise enough that a person or an agent who never saw the conversation can do it |
| `results.md` | Whoever runs it | What happened: the conditions, the numbers, anything that went wrong. Written in the template the request gives |

Raw data that is small enough, such as a `results.json`, goes in the same folder. Anything bigger
stays in the runner's `.scratch/`, and `results.md` says where.

[`../live-runs.md`](../live-runs.md) is still the list of every live check owed. A request is
for the rows that need instructions handed to another machine, or that will be run later by
someone else. Its row in `live-runs.md` links the request.

## The life of a request

1. **Open.** Someone adds `NNN-<short-name>/request.md` with `Status: open`, and a row in
   `live-runs.md` that links it. The number is the next free one, three digits.
2. **Claimed.** Whoever starts it sets `Status: claimed by <machine or session>, <date>` and pushes
   that change, so a second machine doesn't start the same run.
3. **Done.** The runner fills in `results.md`, adds the `findings.md` entry the request asks for,
   sets `Status: done`, moves the `live-runs.md` row to Done, and opens one docs pull request
   into `test` with all of it.
4. **Dropped**, when it is no longer wanted: `Status: dropped`, with one line saying why. The
   folder stays, so the history says it was considered.

A request is never edited to match what was run instead. If the run had to change, `results.md`
says what changed and why.

## Rules every request carries

These are the repository's hard rules and [`../llm-wiki/measuring.md`](../llm-wiki/measuring.md),
restated because a request is often read on its own:

- **Control arm.** No accuracy claim without one: the same set, machine and session (hard rule 2).
- **One model resident at a time** on the GPU, the others unloaded (hard rule 3).
- **Mains power**, and nothing else heavy on the machine or its GPU: no Unity tests, no ComfyUI
  renders, no game.
- **Record the Ollama version and `size_vram`** with every result.
- **`127.0.0.1` or an IP address, never `localhost`.**
- **No default changes in code** as part of a run. A result that argues for one is reported, and
  the lead decides in a separate pull request.
- **The lock.** On the lead's laptop, take the lock first, and on its own, before touching Ollama:
  `.scratch/ollama-5080-lock/` for the 5080 through its gateway, `.scratch/ollama-lock/` for the
  laptop's own. A session on any other machine sees neither, so make sure nobody else is using its GPU.

## run.json: a request the runner can play

The run queue ([`tools/live-runs/runner/`](../../tools/live-runs/runner/README.md)) plays a request
that has a `run.json` beside its `request.md`, on the RTX 5080 machine, block by block, and pushes
its results to a branch `runs/<id>`. [`QUEUE.json`](QUEUE.json) here says which requests it plays,
in what order, and whether it is paused. `runq.py check <folder>` checks a `run.json`.

```json
{
  "id": "007-free-orders-prompt-ab",
  "question": "Do the court's turn facts or the none wording change legal answers and calls a turn under free orders?",
  "commit": "<full sha the run must play>",
  "project": "Demos/GreyBoxStrategy",
  "method": "GreyBoxStrategy.Editor.StrategyTableMenu.RunInterleavedFromCommandLine",
  "model": "qwen3.5:4b",
  "common_args": "-strategyTable personas -strategyTurn writs -strategyCap 40",
  "blocks": [
    { "name": "c1", "arm": "default", "seed": 1, "index": 0, "games": 1, "extra": "-strategyRules FreeOrders=true", "minutes": 6 },
    { "name": "f1", "arm": "default", "seed": 1, "index": 0, "games": 1, "extra": "-strategyRules FreeOrders=true;CourtTurnFacts=true", "minutes": 6 }
  ],
  "done_when": "{block}/summary.md",
  "smallest_slice": ["c1", "f1"],
  "report": [],
  "keep": ["**/summary.md", "**/scorecard.json", "**/*.recording.json"],
  "notes": "Arms are interleaved by seed, so a pause never leaves one arm ahead of another."
}
```

| Field | Means |
|---|---|
| `id` | the folder's name |
| `question` | what the run answers, in one sentence |
| `commit` | the full sha the run plays, pinned: the request is never edited to match what ran |
| `project`, `method` | the Unity project and the `-executeMethod` each block calls |
| `model` | the model the blocks ask; empty for a request that needs no model (scripted players), which then neither waits for the card nor takes it |
| `common_args` | arguments every block gets |
| `block_args` | optional: the per-block arguments, a template. The default suits GreyBoxStrategy: `-strategyModel {model} -strategyArms {arm} -strategyArmNames {name} -strategyGames {games} -strategySeed {seed} -strategyBlock {games} -strategyIndex {index} -strategyOut {out}` |
| `blocks` | in the order to play them, arms interleaved. Each is one Unity start: `name`, `arm`, `seed`, `index`, `games`, `extra` (more arguments) and `minutes`, its estimate; a block still running at twice that is killed |
| `done_when` | the file a finished block writes, relative to the results folder |
| `smallest_slice` | blocks played first and reported before the rest |
| `report` | commands run in the request's worktree when it finishes; `{out}` is the results folder. Their output goes back as `report-N.txt` |
| `keep` | globs of the outputs that go back on the results branch; everything else stays on the machine |
| `invalid_if_log` | optional: text in a block's log that makes it `invalid: provider`. The default catches the framework's provider failures |
| `command` | instead of `project` and `method`: a whole command as a list, for a block that is not a Unity start (the kit's tests use it) |

**A flag goes in once.** The game reads only the first copy of a repeated flag, so a block's own
setting would be lost silently. Put a per-block `-strategyRules` whole in `extra`
(`FreeOrders=true;CourtTurnFacts=true`) and leave it out of `common_args`; `check` refuses it
otherwise.

**The queue** is `QUEUE.json`: `order` plays first to last, `hold` waits, `paused` stops the runner
after the block in hand. Change it with `runq.py pause`, `resume` and `front` from any machine,
each a commit on `test` whose message says why. Moving another request to the front switches the
runner to it after the block in hand; the one it left continues when its turn comes again.

## Requests

| # | Request | Machine | Status |
|---|---|---|---|
| [001](001-interim-model-sweep-5080/request.md) | The 13-model sweep on the interim set (389 prompts), on the RTX 5080 | RTX 5080 box, or its Ollama over the network from the laptop | done |
