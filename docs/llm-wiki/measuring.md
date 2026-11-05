# Measuring: read this before you write down a number

The checklist for any accuracy or latency figure that goes into [`findings.md`](findings.md),
a pull request, a slide or a report. It exists because the prototype's baseline arm alone
moved 11 points between three identical runs, and because several measurements here were
quietly wrong for a reason nobody saw at the time.

[Hard rule 2](../../AGENTS.md) (no accuracy claim without an A/B run) and hard rule 3 (one
model resident when timing) are the short form. This page is the long one.

- [The seven rules](#the-seven-rules)
- [`localhost` and VRAM](#localhost-and-vram)
- [What has been learned since](#what-has-been-learned-since)
- [Before you run](#before-you-run)

## The seven rules

These are build plan §4, copied as they are. The plan's heading for them says to copy them
into the wiki verbatim, so this page is where they live now.

Before writing down any accuracy number:
1. **Control arm.** Baseline vs treatment on the identical prompt set, same model, same
   machine, same session. Single before/after runs are anecdotes — the prototype's
   baseline arm alone drifted 52.8%–64.2% across three otherwise-identical runs (n=53).
   Treat ~10 points as the noise floor at that n; grow n before trusting smaller deltas.
2. **One model resident.** `ollama stop` everything else before timing. Two models on an
   8GB card share compute and flatten every latency number toward the same value
   (observed directly).
3. **No few-shot/eval overlap**, even partial phrasings.
4. **Precondition state staged per case.** "Wait here" is only coherent for an agent
   currently following.
5. **Classify outcomes**, never a single percentage: correct / wrong-but-legal (the bad
   one) / contained-by-guard (the *safety layer working*) / rejected-when-expected /
   pipeline-error. A whitelist rejection scored as "failure" makes your own safety system
   look broken.
6. **Warm the model** (2–3 throwaway requests) so cold-load doesn't pollute the first arm.
7. CI: EditMode tests always run; PlayMode integration tests behind a `RequiresOllama`
   category so the suite is green on machines without a model.

## `localhost` and VRAM

Both notes are build plan §4, copied as they are.

A measurement issue, most likely explained on 5 October 2026: the prototype measured ~1.3s
mean latency in-Unity but ~3.5s from standalone Python scripts, same model/machine. The
scripts called `localhost`, which on Windows tries IPv6 first while Ollama listens on IPv4
only, and each request waited about 2.1 s for the failed attempt ([findings](findings.md)).
Use `127.0.0.1` in probes and as the provider's default endpoint. Unity was not re-measured,
so until it is, trust *relative* comparisons within one environment over absolute latency
claims across environments.

**Not yet scheduled: VRAM monitoring.** Nothing watches GPU memory while a local model
runs. The one figure we have, a 2B model plus a minimal scene at 4.2 GB of 8 GB
(Appendix A), was a single reading, and item 2 above is checked by hand with `ollama ps`.
That matters because running out of VRAM does not fail: the part of the model that does not
fit runs from system memory or on the CPU, and every latency number after that is quietly
wrong. When someone picks this up, record first and build a dashboard later, if ever:

- **During measurement.** The benchmark scripts and the Phase 4 harness log, per arm, which
  models are resident and how much of each sits in VRAM — Ollama's `/api/ps` reports `size`
  and `size_vram` for each model, and `ollama ps` shows the CPU/GPU split — and flag a run
  where a second model is loaded or the model is not fully on the GPU. On NVIDIA hardware
  `nvidia-smi` gives the whole card's usage, renderer included.
- **In a game.** Once the in-process provider ships (Phase 6b), the model shares the card
  with the renderer, so a developer needs to see the headroom. Telemetry or the Decision
  Log window (§2.7) is the natural place for it.

Appendix A is at the end of [the build plan](../FRAMEWORK_BUILD_PLAN.md).

## What has been learned since

Four things, each from the work after §4 was written. Where a source is in
[`findings.md`](findings.md), the entry is named. Where it is not, this page says so.

**1. Ollama updates itself, so record its version with every result.** The lead's laptop went
from Ollama 0.34.2 to 0.40.14 between two runs on 3 November 2026, with nobody choosing to
upgrade. Two things changed on that jump: the schema still never reaches the model and the
probabilities are still from before the mask, but a forced target now comes back in small
pieces (`t`, `rain`, `ing`…) where 0.34.2 gave it as one token. A number from before the
update and one from after were taken on different software. Source: findings, "The Phase 3
gate passed live" (3 November 2026), under "Checks after the Ollama upgrade". After any
upgrade, re-run `python tools/benchmarks/schema_visibility_probe.py`
([environment.md](environment.md#local-models--ollama)). Read the version from
`curl http://127.0.0.1:11434/api/version`.

**2. A GPU that drops out leaves Ollama on the CPU, and nothing fails.** On 3 November 2026,
before the Phase 3 gate's live run, the RTX 4060 showed Windows error code 43 and Ollama ran
on the CPU. Every latency taken then would have looked like a slow model. A restart fixed it.
Check two things before a run, and again if latency jumps:

- `size_vram` from `curl http://127.0.0.1:11434/api/ps`. It should be most or all of `size`;
  0 means the model is not on the GPU at all.
- The server log's "inference compute" line, which says which device Ollama chose when it
  started. On Windows the log is `%LOCALAPPDATA%\Ollama\server.log`.

`findings.md` does not yet have an entry for the code 43 episode; it comes from the lead's
account of that run. The one earlier mention of the symptom, `size_vram` of 0 on a laptop
whose GPU was off when Ollama started, is in [environment.md](environment.md#local-models--ollama)
(seen once, not investigated).

**3. Say how the set reached the prompt: targets as observations, state as a line.** The
Phase 3 suite passed each prompt's targets to the agent as observations, one line per target
with its id and a short description, because the prompt builder lists no targets and Ollama
never shows the schema. It also told the model the agent's state with one observation line
("You are following the player."), because `Agent.State` never reaches the prompt either.
Neither is a neutral default: DR-016's numbers came from a prompt that listed every target,
so the two are not directly comparable. A result is only comparable with another that fed the
model the same way, so write down how the targets and the state were given. Source: findings,
"The Phase 3 gate passed live" (3 November 2026), under "How the targets reached the prompt".

**4. When the two arms are two models, run them one after the other.** Rule 2 says two models
on a card flatten latency. A comparison of two models therefore cannot interleave its arms:
running all of arm A, unloading the model (`{"model": "<name>", "keep_alive": 0, "messages":
[]}` posted to `/api/chat`), and then running all of arm B keeps one model resident at a time.
The cost is that drift over the session no longer hits both arms alike, so say in the result
that the arms ran sequentially, and repeat a close result. Two arms on one model (guards on
against off) interleave instead. This follows from rule 2 and the note in
[environment.md](environment.md#local-models--ollama), and is how the runner in #145 is
specified. It has no findings entry of its own yet.

## Before you run

The A/B runner is [#145](https://github.com/agenerela/agenerela/issues/145), being built in
Phase 4. The middle column says what that issue specifies the runner will check for you. It
is not a promise that the code is merged: until it is, and for any provider it does not
cover, every row is yours to check by hand. The runner reports "not checked" for a provider
other than Ollama.

| Check | Runner (#145) | By hand |
|---|---|---|
| A control arm on the same set, model, machine and session (rule 1) | Runs both arms, interleaved on one model | Decide what the control is; a result without one is an anecdote |
| Enough prompts for the difference you claim (rule 1) | Marks a difference smaller than the noise floor "within noise" | Grow n; the floor is about 10 points at n≈50 |
| Only one model resident (rule 2) | Reads `/api/ps` and refuses to start if another is loaded (Ollama) | `ollama stop <other>`; note that `ollama` hangs in Git Bash on the lead's laptop, so use the HTTP API there |
| The model is on the GPU | Records `size` and `size_vram`, flags a model not fully on the GPU | Look at the server log's "inference compute" line; check `nvidia-smi` if latency is odd |
| Provider version recorded | Records Ollama's `/api/version` | For any other provider, write it down yourself |
| No few-shot and evaluation overlap (rule 3) | Not checked | Compare your examples with the prompt set; partial phrasings count |
| Preconditions staged per case (rule 4) | Stages `preconditionState`, binds every declared action, and tells the model the state as an observation | Check the set's labels use the right preconditions |
| Outcomes classified, not one percentage (rule 5) | Sorts every answer into the five outcomes | Read the wrong answers, not only the counts |
| Warm-up (rule 6) | 2 to 3 throwaway requests per arm, never in the report | Warm up yourself for a probe or script |
| Arms of two models run one after the other | The sequential option unloads between arms | Choose it, and say so in the result |
| How targets and state reached the prompt | Records the choice in the report | Compare only with results fed the same way |
| `127.0.0.1`, not `localhost` | Not checked | Use `http://127.0.0.1:11434` everywhere |
| Plugged in (a laptop) | Not checked | A battery run is not comparable with an AC one |
| Nothing else heavy on the machine | Not checked | Another session's Unity batch tests ran on the CPU during the 3 November run, so its latencies are "not a latency measurement" (findings) |
| Ollama's version unchanged since the run you compare with | Records it, does not compare | Compare the versions; if it changed, re-run the control |
| The result written down | Writes the report | Put it in `findings.md` the same day, negative results too |

Rule 7 is not a before-you-run check: the live tests carry the `RequiresOllama` category, so
without a model they are ignored, not failed.
