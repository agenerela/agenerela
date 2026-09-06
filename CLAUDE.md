# Agenerela

Unity framework that lets a language model drive game agents — NPCs, companions,
factions, colonies — by choosing from actions the developer has explicitly registered.
Senior design project, CSUN COMP 490 / 491.

## Read this first

**Before doing any work in this repo, read [`docs/FRAMEWORK_BUILD_PLAN.md`](docs/FRAMEWORK_BUILD_PLAN.md).**

It is the plan of record: architecture, build phases with definitions of done, testing
standards, and decision records explaining *why* things are the way they are. **Appendix A
carries measured results from the predecessor prototype** — several intuitive-sounding
changes were tried there and made accuracy *worse*. The appendix says which, so they
aren't repeated.

New measurements go in [`docs/llm-wiki/findings.md`](docs/llm-wiki/findings.md) the day
they are taken.

## Hard rules

1. **Never commit secrets.** `.env` holds API keys and is gitignored; `.env.example` is
   the committed template. Keys go in HTTP headers, never URLs, and are redacted from
   error output.
2. **Never claim an accuracy change without an A/B run** — same model, same machine, same
   prompt set, same session. Single before/after runs are anecdotes. At n≈50 the observed
   noise floor was about 10 points.
3. **Unload other models before timing one** (`ollama stop <model>`). Two models resident
   on an 8GB card share compute and flatten every latency measurement.
4. **Few-shot examples must never reuse evaluation prompts.** Demonstrating a phrase and
   then scoring on that same phrase invalidates the result.
5. **The LLM never touches Unity directly.** It selects from a registered action set; the
   executor validates independently and runs the Unity code.
6. **The framework core has no third-party runtime dependencies.** Providers are the
   exception, and must be isolated behind a version define or a separate package.
7. **No attribution trailers in commits or pull requests.** No `Co-Authored-By: Claude`,
   no "Generated with Claude Code". Enforced by `attribution` in
   [`.claude/settings.json`](.claude/settings.json); do not reintroduce it by hand. The
   history should read as the team's work, and authorship is already recorded by the
   committer field.

## Claude Code configuration

Tool configuration lives in [`.claude/`](.claude/README.md) — see that README for what
belongs there. `CLAUDE.md` (this file) stays at the repository root, because that is the
location loaded automatically as project memory. Instructions here, configuration there.

## Settled design rules (measured — do not relitigate without new evidence)

- Response field order is `action`, then `target`, then `dialogue`. Emitting `dialogue`
  first makes the model rationalise its own chat and collapses commands to "no action".
- `target` is **required**, with a `no_target` sentinel value. Never optional, never `""`
  (Gemini rejects empty enum strings outright).
- **Do not** add a reasoning/chain-of-thought field before `action` — measured as the
  worst of five variants, below the unmodified baseline.
- Action availability is derived from agent state, so illegal options are absent from the
  schema rather than forbidden in prose.

## Repo layout

| Path | Contents |
|---|---|
| `UnityProject/Packages/com.agenerela.framework/` | The package — everything shippable |
| `UnityProject/Assets/Demos/` | Demo games, outside the package |
| `UnityProject/Assets/Evaluation/` | Benchmark harness and labelled prompt set |
| `docs/` | Build plan and llm-wiki |
| `tools/benchmarks/` | Standalone Python probes, no Unity required |

## Current state

Phase 0 complete: repo skeleton, empty Unity project, package with runtime/editor/test
assemblies and passing smoke tests. **No framework code exists yet** — Phase 1 (Core,
Actions, Schema) is next. See the build plan's phase table.
