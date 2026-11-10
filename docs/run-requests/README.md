# Run requests

**A run that one machine should do and another session will read.** Each request is a folder
here with two files:

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

## Requests

| # | Request | Machine | Status |
|---|---|---|---|
| [001](001-interim-model-sweep-5080/request.md) | The 13-model sweep on the interim set (389 prompts), on the RTX 5080 | RTX 5080 box, or its Ollama over the network from the laptop | open |
