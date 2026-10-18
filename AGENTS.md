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
- A model served through Ollama never reads the response schema: it only constrains
  sampling, so field and action descriptions in it reach no local model (findings,
  10 October 2026). Anything a local model must read goes in the prompt. Re-run
  `tools/benchmarks/schema_visibility_probe.py` after an Ollama upgrade.

## Repo layout

| Path | Contents |
|---|---|
| `UnityProject/Packages/com.agenerela.framework/` | The package — everything shippable |
| `UnityProject/Assets/Evaluation/` | The labelled prompt set, its first 54 prompts so far (#13), and `agent.json`, the agent they are measured against; its `README.md` documents the format. The benchmark harness that runs it is Phase 4 |
| `Demos/<Game>/` | Demo games — one full Unity project each, loading the package by relative path (DR-010) |
| `docs/` | Build plan, llm-wiki, screen designs and course materials — [`docs/README.md`](docs/README.md) maps them |
| `tools/benchmarks/` | Standalone Python probes, no Unity required |
| `tools/ci/` | `hygiene.py`, every check CI runs, runnable on your machine and as a pre-commit hook; `release.py`, the mechanical steps of a release |
| `tools/comfyui/` | The asset server: ComfyUI in Docker on the lead's RTX 5080 machine, making 3D models, sounds and images for the demo games, and `comfy.py`, which agents call it with |

## Working conventions

**Branches.** `test` is the default and integration branch; `main` is protected and
restricted. Branch off `test`, open pull requests **into `test`**. Never target `main` —
pushes there are restricted to the repository owner.

**Releases** are tags on `main`, one minor version per closed phase, published by the
`Release` workflow; [`docs/RELEASING.md`](docs/RELEASING.md) is the process. Never change
the version in `package.json` or `AgenerelaInfo.Version` by hand:
`tools/ci/release.py prepare` moves both and the changelog together, and CI fails the two
disagreeing. Never push a tag unless the repository owner asked for that release.

**Commits.** Explain *why*, not just *what*. No attribution trailers (rule 7).

**Only `Runtime/Unity/` touches a scene.** Everywhere else in the framework's `Runtime/`,
no `GameObject`, `Component`, `MonoBehaviour`, `Transform`, scene query or physics call, so
an agent can exist with no scene at all — a country in GreyBoxStrategy has no Transform.
`UnityEngine` itself is fine (`Awaitable`, `ScriptableObject`, `[Tooltip]`), and `UnityEditor`
in `Runtime/` must sit inside `#if UNITY_EDITOR`. CI enforces both (DR-011, build plan §1.2).

**Demo games are separate Unity projects** under `Demos/`, one per game (DR-010). Read
[`Demos/README.md`](Demos/README.md) before creating or changing one. A game loads the
framework through a relative `file:` path in its `Packages/manifest.json` — never a
version or git URL — and uses only the framework's public API. Framework work happens in
`UnityProject/`.

**Generated assets come from the asset server** in [`tools/comfyui/`](tools/comfyui/README.md).
Before asking it for anything, read
[`tools/comfyui/AGENT_USAGE.md`](tools/comfyui/AGENT_USAGE.md): connecting a machine, the
three workflows, judging what comes back, and what to do when a run fails. In short: ask with
`python tools/comfyui/comfy.py run <workflow>` (`list` shows the workflows), which reads
`COMFY_URL` and `COMFY_API_TOKEN` from `tools/comfyui/.env`. For a 3D model, make and look at
the image first, then run `mesh` on it; below 50,000 faces, add `--set decimation=qem`, or
the mesh can break into shards. Look at every model before reporting it:
`python tools/comfyui/preview_glb.py <the .glb>` renders it from six angles with Blender. The
`comfyui-assets` skill, in [`.agents/skills/`](.agents/skills/comfyui-assets/SKILL.md) for
Codex with a pointer in `.claude/skills/` for Claude Code, runs this whole procedure. Never
open or print `.env`, and never put the token in a URL or a chat. Setting the server up is
its own runbook,
[`tools/comfyui/AGENT_SETUP.md`](tools/comfyui/AGENT_SETUP.md): that machine also runs the
user's own ComfyUI, so touch only this stack's containers, image and volume. Never run a
`docker ... prune`, and never stop, remove or reconfigure another container, or restart WSL
or Docker Desktop, without the user's say-so. Generated files are drafts until a person
puts them in a game.

**Before opening a pull request:**
- Unity console clean — no new errors or warnings
- EditMode tests pass (`Window → General → Test Runner`)
- No secrets, no generated files (`Library/`, `Logs/`, `*.csproj`)
- Every new file and folder in a Unity project or package has its `.meta` committed with
  it. Unity only creates one when it sees the file, so if you made files in an IDE or on
  GitHub, open that project in Unity before committing
- If the framework's public API changed, every game project under `Demos/` still
  compiles. CI cannot check this yet; open each one
- Any accuracy claim has a control arm and is recorded in `docs/llm-wiki/findings.md`

**Never merge a pull request with a failing CI check.** `Repo hygiene` catches missing
`.meta` files, a committed `.meta` whose GUID changed in place or when its asset moved,
broken JSON and committed generated files in every Unity project, plus a binary file that
bypassed Git LFS, a `.gitignore` rule that ignores tracked files, a project on the wrong
Unity version or a game not loading the framework from this repo; a red check is a blocker,
not a warning. Run the same checks before committing with `python tools/ci/hygiene.py`,
which reads what is staged; every check, and why it exists, is in that file. When it
reports a missing or changed `.meta` for an asset that already existed, restore the
committed one (`git checkout origin/test -- <path>.meta`; for a moved asset, its message
gives the two commands). Never commit the copy Unity regenerates: its GUID is new, so every
reference to the asset stays broken.

**Reviewing a pull request** follows [`REVIEWING.md`](REVIEWING.md): the checks in order,
the four finding labels, and the summary template. A green check is not a review, because
CI never compiles C# or runs a test. An agent that cannot open Unity says so in the review
rather than implying the code builds. The `review-pull-request` skill, in
[`.agents/skills/`](.agents/skills/review-pull-request/SKILL.md), is the procedure an agent
follows to meet it.

**Do not** commit `Library/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.slnx`, or `.env`.
They are gitignored; if one appears in `git status`, something is wrong — investigate
rather than force-adding.

**Scratch work goes in `.scratch/<topic>/`** at the repository root: experiment scripts,
renders, downloaded files, logs, drafts. Git ignores the folder, so it stays with the
checkout where the user can find it, and never in a tracked folder or a system temp
directory. Give each topic a short `README.md` saying what is there. Anything worth keeping
moves into the repository proper.

**Unity version is pinned** to 6000.3.23f1 by each project's
`ProjectSettings/ProjectVersion.txt`, and CI fails if any project differs. Do not upgrade
it; opening a project in a newer patch rewrites that file for everyone.

## Current state

**Phase 1 (Core, Actions, Schema) is complete**, closed on 11 October when the five checks
in #11 passed. **Phase 2 (Ollama provider, queue, memory) is underway**, due 19 October, and
closes when the live checks in its gate (#94) pass. **Nothing in the framework calls a model
yet**: the first live decision is Phase 2's, through an Ollama provider that sends
`JsonSchemaSerializer`'s output.

What has merged, what is still to write and the groundwork beside the phases are in
[`docs/STATUS.md`](docs/STATUS.md); the package's API changes are in its
[`CHANGELOG.md`](UnityProject/Packages/com.agenerela.framework/CHANGELOG.md). Read the one
you need. Neither is loaded into every session, which is why they are not here. When a
pull request merges, update `docs/STATUS.md`, and this section only when a phase opens or
closes.

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

Skills follow the same rule. Each lives once, in `.agents/skills/<name>/`, where Codex reads
it; Claude Code reads `.claude/skills/`, so each skill there is a pointer `SKILL.md` with the
same name and description that sends Claude Code to the one in `.agents/skills/`. Use plain
files, not symlinks: git checks symlinks out as text on many Windows machines.
