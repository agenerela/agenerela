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

### Added

- **The risk management plan** in `docs/course/2026-10-29-risk-management/`: an Excel
  register of 14 risks, two per member, with a risk matrix and a sprint review log, reviewed
  once per sprint. XLSX now goes through Git LFS.

## [0.4.0-preview.1] - 2026-10-29

### Framework

- A preview of 0.4.0, cut before Phase 3 closes and lightly tested. Phase 3 (Validation pipeline) is under way, due 9 November. The guard pipeline and
  `SchemaLegalityGuard` (#106, via #115) are in, and so is target grounding (#105, via #124,
  DR-016): `TargetGroundingGuard` reads the model's token probabilities at the target, and turns
  a decision into `none` when the model wanted something not on the list ("Attack the
  scarecrow." beside a training dummy). A live hand check on Ollama 0.34.2 read 0.051 there,
  recorded in `docs/llm-wiki/findings.md`. Its fallback is in too (#107, via #130): when a
  provider reports no token probabilities, the system prompt carries one line naming the
  targets that are here, an unmeasured wording owed a Phase 4 A/B. Developer guards (#108),
  contained refusals (#109) and the gate (#110) are not in yet.
  See the package's [`[0.4.0-preview.1]`](UnityProject/Packages/com.agenerela.framework/CHANGELOG.md#040-preview1---2026-10-29).
- From Phase 2's leftovers, observation sources are in (#91, via #123): `IObservationSource`,
  the extension point for the Observations research area (#122), with one built-in source,
  `ProximityObservationSource`. So is the provider config asset (#92, via #129, DR-013): the
  model is chosen once as an asset (**Create → Agenerela → Ollama Provider**), with a project
  default under **Project Settings → Agenerela**, and `ProviderConfig` is where other
  providers will plug in. `AgentBehaviour` (#93) is not in yet.

### Added

- **Preview releases and release notes written for people.** `tools/ci/release.py` accepts a
  pre-release version such as `0.4.0-preview.1`, which the release workflow publishes as a
  GitHub pre-release, so the "latest release" badge stays on the last full one. A release's
  notes are `docs/releases/<tag>.md` when it exists, in plain terms, followed by the install
  line and a link to the changelog; the changelog itself stays the detailed record.
- **The repository is MIT-licensed** (DR-007), in `LICENSE.md` at the root, settled once the
  team confirmed it owns the work. What the asset server generates is not covered: the models'
  own terms apply, which `Demos/README.md` sums up, and a game that commits a generated asset
  lists it in a `GENERATED.md`. The README says how to install the package from its git URL.
- **The evaluation set's second batch** (part of #85, via #118): 76 prompts, 38 of them
  game-triggered. It adds situations where acting is right, more targets found only in the
  observations, three open choices with a list `expectedTarget`, and three `checkTarget: false`
  cases; `none` falls from 52% to 39% of the answers. Every target is one GreyBoxVillage has.
  Real annotators and their own answers on the doubly labelled subset are still to do.
- **Branding in `tools/branding/`.** The logo as 3D-printable pins, a keychain and a sign
  (STL, in one- and two-colour versions), 4K stills and a 4K animation with sound, each
  rebuilt in Blender by the scripts beside it. STL, 3MF and WebP now go through Git LFS.
- **A `changelog` hygiene check, as a reminder.** A pull request that changes anything
  outside `docs/` and Markdown but neither changelog gets a warning, never a failure, unless
  its description says why with a line `No changelog: <reason>`. The pre-commit hook skips
  it. The pull request template asks for the entry and for the wiki pages the change
  touches, and the reviewer checks both.

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
