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
runq.py serve                      on the RTX 5080 machine: play the queue, forever
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
kill, the timeout kill and its one retry, a pause, a switch, the queue file's rules, the request
validator and what goes on a results branch.
