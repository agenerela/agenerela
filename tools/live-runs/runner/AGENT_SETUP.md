# Setting up the runner on the RTX 5080 machine: guide for an AI agent

For a Claude Code, Codex or other agent session **on the RTX 5080 machine itself**. It covers what
the machine needs, the checks, starting `runq.py serve`, updating it, and what never to touch. Read
[`AGENTS.md`](../../../AGENTS.md), [`tools/ollama-server/README.md`](../../ollama-server/README.md)
and [`DESIGN.md`](DESIGN.md) first.

## Rules

1. **No keys anywhere.** The runner reads the Ollama key from `tools/ollama-server/.env` itself
   and hands it to the Unity process's environment only. Never open, print or log a `.env`, and
   never put a key on a command line, in a commit, a status file or a chat.
2. **Only this stack.** The machine also runs the asset server and the lead's own ComfyUI. The
   runner talks to Ollama through its gateway and to the asset server through `comfy.py`, never
   through `docker`. Never stop, restart or prune a container, WSL or Docker Desktop.
3. **Push only `runs/*`.** The runner pushes branches named `runs/<request id>` and nothing else:
   never `test`, `main` or a tag. A person merges findings in a pull request.
4. **Install nothing system-wide without the lead's yes**, and that includes a scheduled task or a
   service. The runner runs from the tray app, by the lead's choice.
5. **One Unity process and one model at a time** (hard rule 3). `serve` does that by itself; do
   not run a second `serve`, or a `run`, beside it.

## What the machine needs (the lead does these once)

| Need | Check |
|---|---|
| Unity 6000.3.23f1 through the Hub, the lead signed in (batch mode needs the licence) | `"C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" -version` |
| Git and Git LFS, the lead's GitHub login able to push to `agenerela/agenerela` | `git lfs version`, `gh auth status` |
| Python 3.8 or later | `python --version` |
| A checkout at a short path, `C:\agenerela-runs\repo`, on `test` | `git -C C:\agenerela-runs\repo status -sb` |
| A work folder, `C:\agenerela-runs\work`, and `AGENERELA_RUNNER_WORK` set to it for the lead's user | `echo %AGENERELA_RUNNER_WORK%` |
| The Ollama stack up, its `.env` in place | `python tools/ollama-server/ollama_remote.py check` |
| The asset server's `.env` in place (so the runner sees its queue) | `python tools/comfyui/comfy.py check` |
| Never sleep on mains power | `powercfg /query SCHEME_CURRENT SUB_SLEEP STANDBYIDLE` shows AC 0 |

Short paths matter: Unity fails on paths past 260 characters, and a worktree's `Library` is deep.
Unity's own path is found from each project's `ProjectVersion.txt`; set `AGENERELA_UNITY` only if
it is installed somewhere other than the Hub's default folder.

## The checks, in order

Run them from `C:\agenerela-runs\repo`:

```bash
python -m unittest discover -s tools/live-runs/runner/tests        # 26 tests, no GPU, about 20 s
powershell -NoProfile -ExecutionPolicy Bypass -File tools/live-runs/runner/tray.ps1 -SelfTest
python tools/live-runs/runner/runq.py status                       # the queue, read from origin/test
python tools/ollama-server/ollama_remote.py ps                     # "none" while nobody plays
```

Then one scripted block, which needs no model (DESIGN.md, build step 2): write a `run.json` with
`"model": ""`, `-strategyTable scripted` in `common_args` and a `block_args` without
`-strategyModel`, check it, and play it with `runq.py run <folder> --on-server`. The first block in
a new worktree spends several minutes importing; give it a generous estimate.

## Running the queue

**The lead runs it from the tray app**: `tools/live-runs/runner/tray.cmd` (README.md, "On the RTX
5080 machine: the tray app"). The tray starts `runq.py serve`, shows what it does, and stops it when
the tray closes; game mode frees the GPU while the lead plays. The lead decided on this on
18 November rather than a background service: the runner runs only while the lead can see it.
Never register a scheduled task or service for it; the tray's own "Start with Windows" is the
lead's switch.

`runq.py serve` alone, in a terminal, is for checks. It prints one line a block and writes
`C:\agenerela-runs\work\status.json`. It never exits on its own; an error is written to the status
file and it tries again a minute later. Never run it beside the tray: one runner at a time.

**The card is shared.** Before each block the runner waits until Ollama holds no other model, the
asset server's queue is empty (it then frees the asset server's models), and the card's memory
beyond Ollama's is under `AGENERELA_RUNNER_MAX_OTHER_MB` (6000 by default), which is how it notices
the lead's own ComfyUI. The status file says what it waits for.

**Pausing at the machine:** the tray's menu, or a file `C:\agenerela-runs\work\pause` (its `why:`
line is shown in the status). The runner stops after the block in hand and waits; delete the file
to continue. A file `stop` beside it kills the block in hand at once (it is set aside and planned
again) and pauses; a script writes `stop` to `tray-command` instead when the tray is open. The queue's own pause is
`runq.py pause "<why>"`, which anyone can do from anywhere.

**Updating the runner's code:** Quit the tray (it stops the block in hand and frees the GPU),
`git pull` in `C:\agenerela-runs\repo`, start the tray again.
A request always plays its own pinned commit, so updating the runner never changes what a block
plays. Stopping `serve` mid-block is safe too: the block is set aside and planned again.

## Housekeeping

- **Raw logs and outputs are kept 30 days** in `work\results\` (the lead's decision, 18 November);
  what `keep` names is on the `runs/*` branches for good.
- **Worktrees** under `work\<sha>\` stay while a request at that commit is owed. Remove one with
  `git -C C:\agenerela-runs\repo worktree remove --force <path>` once nothing in the queue pins it.
- **Results branches** under `work\branches\` are the runner's own worktrees of `runs/*`; leave them.

## When something fails

| Symptom | Meaning | Do |
|---|---|---|
| `waiting: another client has X loaded` | a laptop session is using the card | nothing: it waits. If it lasts hours, tell the lead |
| `waiting: N MB of the card is used by something other than Ollama` | the lead's ComfyUI (or a game) holds the card | wait; never stop the other program |
| `invalid: timeout` twice on one block | the block hangs, or its estimate is far too small | leave it; say so to whoever asked, with its log |
| `invalid: provider` | the model could not be asked | `ollama_remote.py check`; report, don't loop |
| `Unity ... is not at ...` | Unity is not installed where the Hub puts it | ask the lead, or set `AGENERELA_UNITY` |
| `could not push results` | the GitHub login lapsed, or the network is down | the results stay in `work\results\`; ask the lead to sign in again |
| `the request pins X but its manifest was played at Y` | a request was edited after it started | the request must be a new one; tell whoever edited it |
