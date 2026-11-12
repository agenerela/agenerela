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

Five things, each from the work after §4 was written. Where a source is in
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
[environment.md](environment.md#local-models--ollama), and is what the runner's
`Sequential` order does. It first ran live on 5 November 2026, `qwen3.5:2b` against
`qwen3:4b-instruct-2507`: the unload worked and the second arm started with nothing resident.
The runner leaves the last arm's model loaded, so unload it yourself afterwards. At temperature 0
the swapped repeat gave identical answers, so a repeat on one machine checks the arm order, not
sampling noise. Source: findings, "First model A/B" (5 November 2026).

**5. A new model may not write the fields in the schema's order.** In the same run, Ollama
0.40.14 held `action`, `target`, `statement` for `qwen3.5:2b`, but `qwen3:4b-instruct-2507`
wrote `action`, `statement`, `target` on every request, whatever order the schema gave. Writing
the free text before the target breaks a settled rule (Appendix A), and the runner cannot see
it, because the report keeps parsed fields, not the raw text. Before a model's first A/B, read
the field order of a few raw responses (`/api/chat` with the framework's schema); if it differs,
the A/B compares two designs, not two models. Source: findings, "First model A/B"
(5 November 2026). The cause is in Ollama: a model served through its own GGUF chat template has
the schema's keys sorted on the way to llama.cpp. The server log says which way a model went:
a "template selection" line with `selected=gguf_chat_template` means the order is lost, and
`renderer_parser` or `go_template` means it is kept. Source: findings, "Why
`qwen3:4b-instruct-2507` wrote `statement` before `target`" (5 November 2026). Setting `OLLAMA_GO_TEMPLATE=1`
on the server sends a model that has a Go template down `go_template`, the 4B and `MiniCPM5-2B`
included. That restores the order, but the Go template then formats the prompt too, a second
difference to name in the result. The `OLLAMA_GO_TEMPLATE:true` every server log prints is only the
default, shown when the variable is unset. Source: findings, "`OLLAMA_GO_TEMPLATE=1` keeps the field
order" (9 November 2026).

## Before you run

The A/B runner is `EvalRunner` ([#145](https://github.com/agenerela/agenerela/issues/145),
merged 5 November 2026; [`evaluation.md`](../../UnityProject/Packages/com.agenerela.framework/Documentation~/evaluation.md)
documents it). The middle column says what it checks for you. For a provider other than
Ollama it reports "not checked", and for a probe or script that does not use it, every row is
yours to check by hand. Where the runner "refuses", it throws before sending anything and says
why.

| Check | Runner (#145) | By hand |
|---|---|---|
| A control arm on the same set, model, machine and session (rule 1) | Runs both arms, interleaved on one model | Decide what the control is; a result without one is an anecdote |
| Enough prompts for the difference you claim (rule 1) | Marks a difference smaller than the noise floor "within noise" (floor = 10 × √(50 / n) points, so 10 at n = 50), over the whole set and per category | Grow n; the floor is about 10 points at n≈50 |
| Only one model resident (rule 2) | Reads `/api/ps` on both servers and refuses to start if a model other than the arm's is loaded, and checks again before each arm's warm-up (Ollama) | `ollama stop <other>`; note that `ollama` hangs in Git Bash on the lead's laptop, so use the HTTP API there |
| The model is on the GPU | Records `size` and `size_vram` before the warm-up, after it and at the arm's end, and warns when the model is not fully on the GPU (a warning, not a refusal). The GPU name in the report is Unity's, not necessarily Ollama's device | Look at the server log's "inference compute" line; check `nvidia-smi` if latency is odd |
| Provider version recorded | Records Ollama's `/api/version` | For any other provider, write it down yourself |
| No few-shot and evaluation overlap (rule 3) | Refuses an example stimulus (the profile's examples, counter-examples and their statements, or an arm's own idle example and its statement) that is exactly a prompt. Partial phrasings are not checked | Compare your examples with the prompt set; partial phrasings count |
| Preconditions staged per case (rule 4) | Stages `preconditionState`, binds every declared action (offered exactly when its `onlyWhen` holds, and always when it has none), and tells the model the state as an observation | Check the set's labels use the right preconditions, and that `onlyWhen` is declared for every action the game restricts |
| Outcomes classified, not one percentage (rule 5) | Sorts every answer into the five outcomes. A guard stopping an answer counts as contained only when the label was `none`, else as rejected-when-expected. Pipeline errors stay in the accuracy denominator | Read the wrong answers, not only the counts (the Markdown report lists every answer that was not correct) |
| What the agent said, not only what it chose | Flags each answer's statement that repeats a few-shot line, is empty, is over the arm's `MaxStatementLength` (200 characters by default, unmeasured) or leaks an id, a field label, a JSON brace or an assistant's phrase, and counts them per arm beside the outcomes, never in them. A statement that contradicts its decision is not checked | Read the flagged answers; count contradictions ("I will stand guard here." with `none`) by hand until a judge model has been validated against people's labels |
| Warm-up (rule 6) | 3 throwaway requests per arm, never in the report; refuses if none of them produced a decision | Warm up yourself for a probe or script |
| Arms of two models run one after the other | `EvalArmOrder.Sequential` unloads the first arm's model (`keep_alive: 0`), waits until `/api/ps` drops it, and refuses if it stays. Interleaved arms on two Ollama models are refused | Choose it, and say so in the result |
| The model writes the fields in the schema's order | Not checked: the report keeps parsed fields, not the raw text | For a model's first A/B, read a few raw `/api/chat` responses (point 5 above) |
| How targets and state reached the prompt | Records the choice in the report, with the set's last commit and whether it has uncommitted changes | Compare only with results fed the same way |
| Which prompt switches each arm ran with | Records each arm's options: `CheckTarget`, `AskForRefusalLine`, the idle example, `ListActionsAndTargets` with its cap, `CounterExamples`, `PairTargetsWithActions`, `ExampleTargetFallback`, priority and the arm's own observations | Name the switches that differ between arms when you quote a result; `ListActionsAndTargets`, `CounterExamples`, `PairTargetsWithActions` and `ExampleTargetFallback` are off by default, as are `StatementRulesWording` (other than the default `YourStatement`) and `LegalTargetListing.ObservationLine`; where an A/B has run, it is in findings. Example target names are spaced in every arm (#234) |
| `127.0.0.1`, not `localhost` | Not checked | Use `http://127.0.0.1:11434` everywhere |
| Plugged in (a laptop) | Not checked | A battery run is not comparable with an AC one |
| Nothing else heavy on the machine | Not checked | Another session's Unity batch tests ran on the CPU during the 3 November run, so its latencies are "not a latency measurement" (findings) |
| Ollama's version unchanged since the run you compare with | Records it, does not compare | Compare the versions; if it changed, re-run the control |
| The result written down | Writes the report | Put it in `findings.md` the same day, negative results too |

Rule 7 is not a before-you-run check: the live tests carry the `RequiresOllama` category and are
ignored, not failed, when Ollama is unreachable. A reachable server still needs the configured
model pulled, or the run fails at warm-up.

**The runner's own live check is not a result.** On 5 November 2026 its `RequiresOllama` test
ran both arms, interleaved, on `qwen3.5:2b`, Ollama 0.40.14, RTX 4060 Laptop on mains power,
nothing else resident: the model was 2254 of 2254 MB in VRAM after each warm-up, no warnings, no
pipeline errors, 90 s. Both arms answered all 8 fixture prompts correctly, "within noise" at
n = 8 (floor 25 points). It shows the runner works against a real server. It says nothing
about accuracy (hard rule 2).
