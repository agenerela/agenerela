# Standalone benchmark probes

Python scripts that talk to a model directly, **without Unity**. Standard library only —
no `pip install` needed.

## Why these exist

Iterating on a schema or prompt idea inside Unity means entering Play Mode, waiting for a
domain reload, and running 50+ requests. These scripts do the same experiment in seconds.

**The workflow is: find a fix here, port it into C#, then re-validate inside Unity.** A
result from these scripts is *provisional* until confirmed in-engine — the two environments
have shown different absolute latency numbers for reasons not yet explained (see
`docs/llm-wiki/findings.md`).

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

The same 17-case set against a cloud API. Reads `GEMINI_API_KEY` and `GEMINI_MODEL` from
`.env` at the repo root (gitignored — copy `.env.example`).

```bash
python tools/benchmarks/gemini_compare.py
```

**Do not remove the guards.** `MAX_REQUESTS = 20` is a hard budget cap and `SPACING = 4.3`
keeps requests under the free tier's ~15/minute limit. The daily allowance is ~1000
requests, shared across everyone testing with that key.

The key is read from `.env` and sent in an HTTP header, never a URL, and is redacted from
any error output. Keep it that way.

## Before trusting any number these print

- Was there a **control arm**? A single before/after run is an anecdote.
- Was **exactly one model** resident during timing? Check `ollama ps`.
- Do any few-shot examples **overlap the test prompts**? That invalidates the result.
- Is n large enough? At n≈50 the observed noise floor was about 10 points.

The full checklist is in `docs/FRAMEWORK_BUILD_PLAN.md` §4.
