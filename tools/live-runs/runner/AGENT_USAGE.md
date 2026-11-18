# Using the run queue: guide for an AI agent

For a Claude Code, Codex or other agent session **on the lead's laptop** (or any machine but the
RTX 5080 one) that wants a live run played, or needs the card for something else. Read
[`README.md`](README.md) first.

## Rules

1. **While the queue runs, the card is the runner's**, and the runner runs only while the lead's
   tray app is open on the RTX 5080 machine: `runq.py status` says when it last wrote. Before a live check in the editor, a run of
   your own, or a picture, model or sound from the asset server, pause the queue
   (`runq.py pause "<why>"`), wait until `runq.py status` says `paused`, take the laptop's lock as
   usual, and resume the queue when you are done (`runq.py resume`). The lead's decision,
   18 November.
2. **Ask before you push to `test`.** `pause`, `resume` and `front` show the commit and ask; an
   agent cannot answer that question, so ask the user in the chat and pass `--yes` only after a
   yes in their words.
3. **A request is never edited after it starts playing.** If it must change, it is a new request
   with the next number. The runner refuses a manifest played at another commit.
4. **Write the findings yourself.** The runner pushes raw results to `runs/<request id>`; it never
   opens a pull request or changes a default. `results.md` and the `findings.md` entry are yours,
   in a pull request into `test`, as [`docs/run-requests/README.md`](../../../docs/run-requests/README.md) says.

## Asking for a run

1. Write `docs/run-requests/NNN-<name>/request.md` as before, and beside it `run.json`
   ([the format](../../../docs/run-requests/README.md#runjson-a-request-the-runner-can-play)).
   Pin `commit` to a full sha that is on `test` (or pushed), and list the blocks in the order to
   play them, **arms interleaved** by seed, so a pause never leaves one arm ahead.
2. Check it: `python tools/live-runs/runner/runq.py check docs/run-requests/NNN-<name>`. It refuses
   a flag given twice: the game reads only the first copy, so a block's own `-strategyRules` must
   carry the whole value (`FreeOrders=true;CourtTurnFacts=true`), not add to `common_args`.
3. Push the request to `test` (a docs commit), then queue it: `runq.py front NNN-<name> "<why>"`,
   or edit `order` in `QUEUE.json` in the same commit.

The estimate matters: a block is killed at twice its `minutes`. Use the minutes a block of that
size took last time, plus five for the first block in a new worktree (Unity imports the project).

## Reading progress and results

```bash
python tools/live-runs/runner/runq.py status
```

```text
runner   running 007-free-orders-prompt-ab/f1, card in use; 3 min ago (from runs/007-free-orders-prompt-ab)
queue    running; 2 in order, 1 on hold
  1.    007-free-orders-prompt-ab  1/3 done, playing f1  (runs/007-free-orders-prompt-ab, 31 min ago)
  2.    008-free-orders-played-12s  not started
  hold  010-forts-siege-income  not started
```

Status comes back through git, so from the laptop it is as old as the last push: at most thirty
minutes while a request plays. The results are on the branch:

```bash
git fetch origin "+refs/heads/runs/*:refs/remotes/origin/runs/*"
git show origin/runs/007-free-orders-prompt-ab --stat
```

`docs/run-requests/<id>/results/` holds `manifest.json` (each block's status, minutes and tries,
the Ollama version and `size_vram`), `status.json`, the files `keep` names, `report-N.txt` for each
`report` command, and `LEFT_ON_MACHINE.md` for files too big to push.

When you write the finding, say **which kind of runner played it**: the manifest's `runner` field.
Answers from the runner compare with laptop-driven runs against the same server; **times do not**,
because a call from the machine itself crosses no network.

## Playing a request on the laptop

`runq.py run docs/run-requests/NNN-<name>` plays one request here, with the same manifest and rules:
it takes the laptop's lock `.scratch/ollama-5080-lock/` per block, and stops after the block in hand
if that folder has a `pause` file or the battery is under 25%. It refuses while the queue is
running (rule 1); `--ignore-queue` only with the lead's yes. Run the same command again to continue
a stopped request; results are in `.scratch/runner/results/<id>/`. It replaces the per-session
`run_blocks.sh` copies: don't write another.
