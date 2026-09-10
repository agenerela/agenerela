# Agenerela — instructions for AI coding agents

Unity framework that lets a language model drive game agents — NPCs, companions,
factions, colonies — by choosing from actions the developer has explicitly registered.
Senior design project, CSUN COMP 490 / 491.

> **This file is the single source of truth for every AI agent working in this
> repository** — Codex, Claude Code, Cursor, Copilot, Gemini CLI, Aider, and others.
> Agent-specific files (`CLAUDE.md`, `.github/copilot-instructions.md`) are thin pointers
> to this one. **Put rules here, not in those.**
>
> Humans should read it too — it is the shortest description of how this project works.

## Read this first

**Before doing any work, read [`docs/FRAMEWORK_BUILD_PLAN.md`](docs/FRAMEWORK_BUILD_PLAN.md).**

It is the plan of record: architecture, build phases with definitions of done, testing
standards, and decision records explaining *why* things are the way they are. **Appendix A
carries measured results from the predecessor prototype** — several intuitive-sounding
changes were tried there and made accuracy *worse*. The appendix says which, so they are
not repeated.

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
6. **The framework core's only third-party dependency is Newtonsoft JSON**
   (`com.unity.nuget.newtonsoft-json`), declared in the package's `package.json` so it
   installs with the framework. Adding any other needs a decision record (DR-009). Core
   asmdefs set `overrideReferences` and list their DLLs explicitly, so a DLL that merely
   exists in the project cannot leak in. Providers are the exception, and must be
   isolated behind a version define or a separate package.
7. **No attribution trailers in commits or pull requests.** No `Co-Authored-By`, no
   "Generated with &lt;tool&gt;". The history should read as the team's work; authorship is
   already recorded by the committer field.

## Settled design rules (measured — do not relitigate without new evidence)

- Response field order is `action`, then `target`, then the free-text field (`statement`;
  the prototype called it `dialogue`). Emitting the free text first makes the model
  rationalise its own reply and collapses commands to "no action". The rule is about
  order; the rename is unmeasured and is an early Phase 4 A/B run (DR-008).
- The framework never assumes a speaking character, and never classifies *when* or *why*
  an agent is asked. Inputs are open: a free-form stimulus, a free-form label, a list of
  observations. The developer's own systems decide the moment (DR-008).
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

## Working conventions

**Branches.** `test` is the default and integration branch; `main` is protected and
restricted. Branch off `test`, open pull requests **into `test`**. Never target `main` —
pushes there are restricted to the repository owner.

**Commits.** Explain *why*, not just *what*. No attribution trailers (rule 7).

**Before opening a pull request:**
- Unity console clean — no new errors or warnings
- EditMode tests pass (`Window → General → Test Runner`)
- No secrets, no generated files (`Library/`, `Logs/`, `*.csproj`)
- Every new file and folder under `UnityProject/` has its `.meta` committed with it.
  Unity only creates one when it sees the file, so if you made files in an IDE or on
  GitHub, open the project in Unity before committing
- Any accuracy claim has a control arm and is recorded in `docs/llm-wiki/findings.md`

**Never merge a pull request with a failing CI check.** `Repo hygiene` catches missing
`.meta` files, broken JSON and committed generated files; a red check is a blocker, not a
warning.

**Do not** commit `Library/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.slnx`, or `.env`.
They are gitignored; if one appears in `git status`, something is wrong — investigate
rather than force-adding.

**Unity version is pinned** to 6000.3.23f1 by `ProjectSettings/ProjectVersion.txt`.
Do not upgrade it; opening the project in a newer patch rewrites that file for everyone.

## Current state

Phase 0 complete: repo skeleton, empty Unity project, package with runtime/editor/test
assemblies and passing smoke tests. **No framework code exists yet** — Phase 1 (Core,
Actions, Schema) is next. See the build plan's phase table.

## Agent-specific configuration

Behavioural rules belong in this file. Only tool-specific *configuration* belongs in the
files below.

| Agent | Entry point | Notes |
|---|---|---|
| Claude Code | [`CLAUDE.md`](CLAUDE.md) → imports this file | Settings in [`.claude/`](.claude/README.md) |
| Codex, Cursor, Copilot, Gemini CLI, Aider, Zed, Windsurf, Devin | **this file**, natively | AGENTS.md is an open standard stewarded by the Linux Foundation's Agentic AI Foundation |

If you add an agent that reads a different filename, create a **pointer** to this file
rather than a copy. Duplicated rule files drift apart, and the drift is invisible until
two agents behave differently for reasons nobody can explain.
