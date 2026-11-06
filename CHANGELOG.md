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

- **Phase 3 (Validation pipeline) closed** on 3 November when its gate (#110) passed: the
  guard pipeline and `SchemaLegalityGuard` (#106), target grounding (#105), its one-line
  fallback (#107), developer guards (#108) and contained refusals (#109, via #141), which keep
  an agent from saying what it will not do. See the package's `[Unreleased]` section.

### Added

- **An editor UI kit** (#167) in the package's `Editor/UI/`, for the Phase 5 screens to share.
  It is internal to `Agenerela.Editor`, so it adds no public API.
  - `Agenerela.uss` holds colour, spacing and type variables (`--ag-*`) built on Unity's own
    theme colours, so they suit both editor skins.
  - `Chip` shows an action or target id in monospace, struck through with a dashed outline when
    it is not offered.
  - `OutcomeIcon` shows Executed, No action, Contained or Error with Unity's built-in icons.
  - `ProblemBox` shows a message with an optional fix button.
  - `SectionTitle` heads a section in small, spaced, muted capitals, such as "EXPECTED ANSWER".
  - `AgenerelaButtons` adds two styles to Unity's plain button: a filled primary button for
    the one action a screen is for (**Save answer**), and a dashed one for an open choice
    (**Open choice**).
  - `EditorUiFixtures` in `Tests/Editor/UI/` builds a hosted element, an inspector or a window
    in an EditMode test, and finds its parts by name. **Window → Agenerela → Tests → UI Kit
    Gallery** shows every piece on one page, for checking both skins by hand. It is in the test
    assembly, so it does not ship in the package.
    A test that opens a window needs a graphics device, so under `-nographics` it is reported as
    ignored rather than failed.
  - USS has no muted text colour that reaches an editor window, no dashed border and no
    `text-transform`. The kit sets the colour itself, draws dashed outlines in
    `DashedOutline`, and upper-cases titles in code. A test catches a variable that silently
    fails to resolve.
  - After its review (#181): setting a chip's `text` sets its id, so the strike always follows
    `Offered`; the light-skin class is checked again whenever an element's styles resolve,
    not only when it is built; and `DashedOutline.Attach` attaches once, however often it is
    called.
- **GreyBoxVillage has two guards and an agent board** (#143), the first slice of the demo's
  upgrade.
  - Bram at the gate and Wren at the bridge are each an `AgentBehaviour` with a profile asset.
    Both ask one `OllamaProviderConfig`, so they share one queue.
  - The player speaks to the nearest guard. The bell is heard by every agent except the one who
    rang it.
  - A game guard, `never_attack_villagers`, contains an attack on the village's people, and each
    guard says its own refusal line.
  - The board shows each agent's stimulus, offered actions, decision, the guard that stopped it,
    and its queue wait. The request history can be filtered by agent.
  - The recorder reads `DecisionResult.Request` and `Reply` (#142) instead of wrapping the
    provider.

- **The evaluation set opens in the Prompt Set window** (#134). `agent.json` now lists its six
  categories, each with its file and the prefix its ids use (`command_with_target`'s are
  `target_001`, ...), so the window and the Phase 4 runner find every prompt. No prompt changed:
  a test loads and saves all 76 and compares them. The files are now in the layout the window
  writes, which a test keeps them in, so the first save from the window shows only what it
  changed rather than reflowing every line. Blind second labels go to
  `Assets/Evaluation/labels/<annotator>/<category>.json`; the README says how.
- **Screen 8, the Prompt Set window**, in `docs/design/`: the wireframe drawn for #134 before the
  window was built, with its notes.
- **Living Vale, a third-person RPG demo built on Agenerela** (#69), in `Demos/LivingVale/`: every
  NPC is talked to in typed chat, and each reply is a framework decision that the game validates and
  executes. It uses only the framework's public API and loads it by relative path.
  - **The first adventure:** *The Missing Wagon*, with three recovery routes (fight the camp, pay
    Kest or do Iven's herb favour, or bring Fen's account), three endings with visible changes in
    the valley, and two side quests (herbs, Brann's badge). Every deal and hand-over is a card in the
    chat, and only its button acts. Claims in chat record nothing.
  - **Eight NPCs**, each a framework `Agent` on a local model (default `gemma4:e2b-it-qat`; F7 moves
    to the next model Ollama has, loading only the one the player stops on). F6 switches to scripted
    replies, which also decide through `Agent`, with `ScriptedProvider`.
  - **Game systems:** trade with exact quote cards and all-or-nothing transfers; combat with a raider
    and a wolf, loot and respawn; quests, XP, levels and perks; saves with F9/F10 that include what
    each NPC remembers. A reply that arrives after the player cancelled, closed, switched or loaded is
    never applied or remembered.
  - **The valley:** six places on a 600 m map, routines, discovery, a compass with the quest goal,
    NPC name tags and a map on M. The art is low-poly, generated by our own Blender scripts
    (`Demos/LivingVale/ART.md`).
  - **On the framework:** it uses #193's prompt switches, #194's scripted provider and #195's
    results, correlation and commit-on-execute memory in place of its own workarounds, after
    same-session A/B runs where the prompt changed (findings, 5–6 November).
    `docs/demos/living-vale-framework-gaps.md` lists the 23 gaps the game found. #190 collects
    them for the framework.
  - 581 EditMode tests. How to play, and the known issues: `Demos/LivingVale/PLAYTEST.md`.
- **The risk management plan** in `docs/course/2026-10-29-risk-management/`: an Excel
  register of 14 risks, two per member, with a risk matrix and a sprint review log, reviewed
  once per sprint. XLSX now goes through Git LFS.
- **The Phase 3 gate's checks** (#110). `Phase3GateTests` proves each guard rule in EditMode
  with no model: a substitution is rewritten to `none`, an unavailable action and a
  target-needing action with `no_target` are rejected, verdicts reach telemetry, a reading
  below 0.8 is rewritten while a split between two listed targets passes, `CheckTarget = false`
  skips the guard for one call, and a provider without token probabilities is recorded as such.
  `Phase3LiveSuiteTests` runs the prototype's 20-prompt suite live on a 2B (`RequiresOllama`),
  guards on against a `CheckTarget = false` control arm in one session; its bar is 19/20 or
  better with the guards on. **It passed on 3 November**: 19/20 with the guards on, 18/20 in
  the control, on `qwen3.5:2b` and Ollama 0.40.14, recorded in `findings.md`. At n = 20 that
  says the gate passed, not by how much the guard helps.

### Fixed

- **Release notes no longer break mid-sentence.** GitHub shows every newline in a release's
  notes as a line break, so notes copied from wrapped Markdown came out as ragged paragraphs
  stopping halfway across the page, as 0.2.0, 0.3.0 and 0.4.0-preview.1 did (the last was
  fixed by hand). `tools/ci/release.py notes` now joins wrapped lines, leaving code, headings,
  HTML, tables and alert markers alone.
- **The evaluation set names its real annotator on `target_001` to `target_009`.** They held
  #13's placeholder names, `alice` and `bob`; Maxim labelled them. No prompt is doubly labelled
  now, so Phase 4's second labels are still to come, from the Prompt Set window (#134).
- **GreyBoxVillage reads the guard pipeline's verdict.** Since #106 a guard that stops an answer
  returns it as `none`, so the demo logged refusals as `none` and four of its tests failed. It now
  takes the outcome from the verdict and `Execute`: `refused` for a rejected answer, `contained`
  for one the grounding guard rewrote, with the model's answer, the guard and its reason on the
  overlay and in the history. A new test covers `contained`.

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
