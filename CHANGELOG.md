# Changelog

What changed in this repository, newest first. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and each section is named after the
framework release it shipped beside.

There are two changelogs, and nothing is written in both:

- **The framework's public API** is in the package's own
  [`CHANGELOG.md`](UnityProject/Packages/com.agenerela.framework/CHANGELOG.md). It ships with
  the package and becomes each release's notes, so it is written for the developers who
  install it.
- **Everything else** is here: the evaluation set, the demo games, CI and tools, the docs, and
  what each phase closed. Each section below starts with one line on the framework, linking
  its package section, so this file alone shows the whole history.

A pull request adds its own entry under `[Unreleased]`, in whichever file it belongs. One that
merged without one gets it in the next docs commit. What is still to do is in the build plan's
phase table and the open issues, not here.

## [Unreleased]

### Framework

- Phase 3 (Validation pipeline) is under way, due 9 November. The guard pipeline and
  `SchemaLegalityGuard` (#106, via #115) are in; target grounding (#105) and its fallback
  (#107), developer guards (#108), contained refusals (#109) and the gate (#110) are not.
  See the package's [`[Unreleased]`](UnityProject/Packages/com.agenerela.framework/CHANGELOG.md#unreleased).

### Added

- **A `changelog` hygiene check.** A pull request that changes anything outside `docs/` and
  Markdown must also change one of the two changelogs, or say why not with a line
  `No changelog: <reason>` in its description. On a laptop it only warns, and the pre-commit
  hook skips it. The pull request template asks for the entry and for the wiki pages the
  change touches.

### Changed

- This changelog replaces `docs/STATUS.md`. Its per-merge history duplicated the package
  changelog and had to be updated in a separate commit after every merge; what it recorded
  beyond the API is in the sections below.

## [0.3.0] - 2026-10-25

### Framework

- **Phase 2 (Ollama provider, queue, memory) closed** when the live checks in its gate (#94,
  via #111) passed: `OllamaProvider`, typed provider errors, the per-provider request queue
  and short-term memory. See the package's
  [`[0.3.0]`](UnityProject/Packages/com.agenerela.framework/CHANGELOG.md#030---2026-10-25).
  Still open from Phase 2, off its gate's path: observation sources (#91), the provider config
  asset (#92) and `AgentBehaviour` (#93), so an agent is still built in code.

### Added

- `UnityProject/Assets/Sandbox/`, a scene where a person can type to the guard and watch it
  decide (#94).
- `Demos/GreyBoxVillage` driven by `qwen3.5:2b` through `OllamaProvider` (#19, via #112). The
  game decides when to ask (a player line, a trespass, a wolf, a quiet moment) and offers
  actions by who is asking, so chat never gets an unprompted `patrol`. A request history panel
  and a JSONL log show what was sent and what came back. It uses only the public API, and is
  driven with the Unity CLI; Unity Version Control was dropped from it.
- The first in-Unity latency since the prototype, in `findings.md`: 0.23 to 0.56 s warm on
  `qwen3.5:2b`, an RTX 5080.

### Changed

- A newer push to a pull request cancels its hygiene run still in progress.

## [0.2.0] - 2026-10-18

### Framework

- **Phase 1 (Core, Actions, Schema) closed on 11 October**, when the five checks in #11 (via
  #84) passed: actions, targets, availability, few-shot examples, the decision schema and its
  JSON Schema serializer, prompts, `Agent`, telemetry and target discovery, all testable with
  no model running. See the package's
  [`[0.2.0]`](UnityProject/Packages/com.agenerela.framework/CHANGELOG.md#020---2026-10-18).
- Newtonsoft JSON became the core's one declared dependency (DR-009, #24), and MCP for Unity
  was added to `UnityProject/` for agents.

### Added

- **Tagged releases** of the framework package: `tools/ci/release.py`, the `Release` workflow
  and [`docs/RELEASING.md`](docs/RELEASING.md) (#97).
- **The evaluation set**: the first 54 prompts, nine in each of six categories (#13, via #65),
  then `agent.json`, the guard every prompt is measured against, with observations rewritten
  as facts about the world (#66, via #76 and #86). Its `README.md` documents the format. Still
  open, in #85: the real annotators and their answers on the 18 doubly labelled prompts, and
  the next batch of prompts.
- **Demo games as separate Unity projects** under `Demos/`, each loading the package by
  relative path (DR-010, #27), starting with the `GreyBoxVillage` grey box (#19). The rest are
  sub-issues of #47, starting with `GreyBoxStrategy` (#20); code two games share moves into
  `Demos/Shared/` once a second game needs it (#14).
- **The asset server** in `tools/comfyui/`: ComfyUI in Docker on the RTX 5080 machine, making
  images, 3D models and sounds for the demo games, and `comfy.py`, which agents call it with
  (#70). Its first run there found four faults, fixed in #71; #72 moved the image workflow to
  Z-Image Turbo's NVFP4 build, about twice as fast; #73 wrote down how agents use it.
- **Repo hygiene checks** for the hard rules a script can check, run on every Unity project
  (#27): missing `.meta` files, including folders' (#24); "only `Runtime/Unity/` touches a scene"
  (DR-011, #38, #40); a committed `.meta` whose GUID changed, and a `.gitignore` rule that
  hides tracked files (#41); a moved asset whose `.meta` was regenerated (#52). All of them
  moved into `tools/ci/hygiene.py` (#97), which also runs on a laptop and as a pre-commit
  hook, and added a check for a binary file outside Git LFS. CI still compiles no C#, so a
  green check does not prove `test` builds.
- [`REVIEWING.md`](REVIEWING.md), the review procedure with its summary template, and the
  issue templates for tasks, findings and bugs.
- **Measurements** in [`findings.md`](docs/llm-wiki/findings.md): the grounding guard reads
  the model's confidence rather than the player's words (5 October, DR-016), and Ollama never
  shows the response schema to the model (10 October), with the probes in
  `tools/benchmarks/`.
- **Design docs**: the sitemap and six wireframes in [`docs/design/`](docs/design/README.md),
  the RiskRace write-up in [`docs/demos/`](docs/demos/risk-race.md), the Phase 2 page in
  [`docs/phases/`](docs/phases/phase-2.html), and the course pages in
  [`docs/course/`](docs/course/README.md).

## [0.1.0] - 2026-09-03

### Framework

- **Phase 0 (skeleton)**: the package layout of build plan §1.2, its assemblies and
  `AgenerelaInfo`, with smoke tests. See the package's
  [`[0.1.0]`](UnityProject/Packages/com.agenerela.framework/CHANGELOG.md#010---2026-09-03).

### Added

- The repository: the empty Unity project on 6000.3.23f1 with URP, `.gitattributes` and
  `.gitignore` before anything else, `test` as the integration branch and `main` protected.
- The project docs: the build plan, `AGENTS.md`, the llm-wiki and the `.env.example` secrets
  template.
