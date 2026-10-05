# Standalone benchmark probes

Python scripts that talk to a model directly, **without Unity**. Standard library only —
no `pip install` needed.

## Why these exist

Iterating on a schema or prompt idea inside Unity means entering Play Mode, waiting for a
domain reload, and running 50+ requests. These scripts do the same experiment in seconds.

**The workflow is: find a fix here, port it into C#, then re-validate inside Unity.** A
result from these scripts is *provisional* until confirmed in-engine.

**Talk to `127.0.0.1`, never `localhost`.** On Windows `localhost` resolves to IPv6 first,
Ollama listens on IPv4 only, and every request waited about 2 s for the failed attempt. That
is most likely why the prototype's scripts measured ~3.5 s a decision against ~1.3 s in Unity
(`docs/llm-wiki/findings.md`).

When porting: `compare_2b_4b.py` and `gemini_compare.py` name the free-text field
`dialogue`, as the prototype did. The framework calls it `statement` (DR-008); the rename
itself is unmeasured, and scheduled as an early Phase 4 A/B.

## `compare_2b_4b.py`

Runs a focused 20-prompt test in four categories — simple commands, commands with a
target, impossible requests, and plain conversation — against any Ollama models you name.

```bash
python tools/benchmarks/compare_2b_4b.py qwen3.5:4b qwen3.5:2b
```

Prints a plain-English "what went wrong" list per model, and a side-by-side table when you
pass exactly two.

**Do not remove the `unload_all()` call.** It runs `ollama stop` on every known model
before each timed run. Two models resident on one GPU share compute and flatten every
latency measurement toward the same number — this produced a misleading result once
already.

## `gemini_compare.py`

A separate 17-case set — the one behind Appendix A's cloud-versus-local row — against a
cloud API. Reads `GEMINI_API_KEY` and `GEMINI_MODEL` from `.env` at the repo root
(gitignored — copy `.env.example`).

```bash
python tools/benchmarks/gemini_compare.py
```

**Do not remove the guards.** `MAX_REQUESTS = 20` is a hard budget cap and `SPACING = 4.3`
keeps requests under the free tier's ~15/minute limit. The daily allowance is ~1000
requests, shared across everyone testing with that key.

The key is read from `.env` and sent in an HTTP header, never a URL, and is redacted from
any error output. Keep it that way.

## `target_swap_probe.py` and `target_swap_score.py`

Measures ways to stop a model swapping a missing target for a legal one: "Attack Godzilla"
makes the guard attack the training dummy. There are 109 labelled prompts in two invented
scenes, a village guard and a companion with items. Six arms ask the model: today's prompt
and schema, the same without logprobs (to time them), a near-miss refusal example, a
one-line rule, a field for the words the player used, and a free-text target. The scorer
then applies checks afterwards: the name check, a yes/no verifier, and a confidence check on
the probability the model gave its chosen target, read from Ollama's `logprobs`.

```bash
python tools/benchmarks/target_swap_probe.py qwen3.5:2b qwen3.5:4b
python tools/benchmarks/target_swap_score.py -v
```

The probe appends to `target_swap_results.jsonl` in the current directory, so run it from a
scratch folder, not the repository. The scorer reads the same file. A full run took about
12 minutes for the 2B and 18 for the 4B on an RTX 4060 laptop on AC power. The results of
5 October 2026, and what they led to, are in `docs/llm-wiki/findings.md` and DR-016.

## Before trusting any number these print

- Was there a **control arm**? A single before/after run is an anecdote.
- Was **exactly one model** resident during timing? Check `ollama ps`.
- Do any few-shot examples **overlap the test prompts**? That invalidates the result.
- Is n large enough? At n≈50 the observed noise floor was about 10 points.

The full checklist is in `docs/FRAMEWORK_BUILD_PLAN.md` §4.
