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
- **Living Vale G4b: the region and six more NPCs** (#69), on `69-living-vale`: the grey-box
  valley around the village, built by one builder per place under `Editor/Region/` and called from
  `VillageSceneBuilder.Build`: a 600 × 600 ground, flat paths along `ValePlaces.Paths`, the Old
  Road bend with an empty wreck site, Reedbank (workshop, fields, and the bridge over the river
  that is the only way to Ashwatch), Ashwatch's forecourt and closed gate before a walled keep,
  Broken Watch's ruined camp, Herb Hollow's clearing and standing stone, boundary cliffs and
  distant mountains. Sella, Toma, Fen, Brann, Elra and Kest each have a persona, scripted lines
  and a two- or three-stop routine that holds while their chat is open; all of them only talk for
  now. Entering a place for the first time pays 10 XP once (`discover_<place id>`, saved with the
  progression) with a HUD notice, and the HUD names the current place. Framework gaps 14 and 15.
- **Living Vale G4c: the main quest's routes and endings, and both side quests** (#69), on
  `69-living-vale`. The parcel at Broken Watch can be recovered three ways: by defeating three
  camp defenders, by paying Kest or having done Iven's herb favour, or with Fen's witness clue,
  which counts only once Fen tells it through `share_information`. It can then be delivered to
  Iven, to Elra with Brann's permit, or divided once both have agreed. There are new NPC actions
  (`share_information`, `offer_exchange`, `resolve_delivery`, `grant_access`), and both side
  quests can be offered and handed in. **E** uses world pickups, and each ending changes the scene.
  Every deal goes on a card. All of it runs through `QuestRoutes` and the shared executor, from
  both drivers. Framework gaps 16–19 are recorded.
- **Living Vale G7b: the art kit is in the game** (#69), on `69-living-vale`: the Village scene's
  builders dress their primitives with the kit through `ArtDressing`, which keeps each primitive's
  collider and switches the model's off, so walls, talk ranges and sight lines stay where they were.
  The Glen's cottages, stall, well and fences, Reedbank's buildings, Ashwatch's walls and gatehouse,
  Broken Watch's ruins, tents and fire, the Old Road wreck, every tree and boulder, the quest herbs,
  seeded trees and rocks along the paths and cliffs, and signposts where paths leave places. The
  player, all eight NPCs (outfit by role) and the raiders wear the humanoid, the wolf is the kit's
  wolf, and the player and raiders hold the kit sword. `VillageSceneShots` renders the built scene
  from eight viewpoints for review.
- **Living Vale G7a: an art pilot kit** (#69), on `69-living-vale`: 29 low-poly, flat-shaded
  models (cottages, stall, well, fences, props, the intact and the wrecked wagon, trees, rocks,
  feverleaf, Ashwatch's gate and wall, Broken Watch's ruin, tent and campfire, one humanoid in
  five outfits, a sword and a wolf), all generated by our own Blender scripts in
  `Demos/LivingVale/Art~/blender/` and sharing one palette texture and one URP material. An
  `AssetPostprocessor` fixes their import, `ArtKit` places them and swaps a capsule body for the
  humanoid with a fallback when a model is missing, and an Art Preview scene shows them all.
  The scene builders do not use it yet; `Demos/LivingVale/ART.md` covers rebuilding and style.
- **Living Vale G4, part 1: quests, progression and saves** (#69), on `69-living-vale`: plain C#
  in `Game/` with EditMode tests. `QuestLog` holds *The Missing Wagon* (states Undiscovered →
  Offered → Active → ParcelRecovered → Resolved, early discovery skipping ahead, clues kept apart,
  three authored dispositions with the division needing both recorded agreements) and the side
  quests `quest_herbs` and `quest_badge`; every transition checks the current state, and every
  reward applies once per event ID. `Progression` gives levels at 100 and 250 XP and one perk per
  level-up. Iven offers the quest with `offer_quest` through the shared executor, from both
  drivers, on a card whose Accept button alone makes it Active. `SaveGame` writes a versioned
  snapshot atomically (keeping the previous save), validates before restoring, and never saves a
  quote or offer; F9 saves and F10 loads.
- **Living Vale G5f: NPCs stop making trade offers nobody asked for** (#69). The Agenerela
  driver now shows the model one negative example by default: a line answered with `none`. In an
  A/B on 13 lines asking for nothing, unasked offers fell from 6/13 to 0/13 on
  `gemma4:e2b-it-qat` and from 10/13 to 0/13 on `qwen3.5:2b`, with the asked trades unchanged
  (`findings.md`, 5 November). The help line fits 1280×720. Framework gap 9 is updated, and gap
  13 (a code-built agent has no `none` example) is new.
- **Living Vale G5e: `gemma4:e2b-it-qat` is the game's default model** (#69), adopted by the
  lead on 5 November after the blind writing comparison. `qwen3.5:2b` is second in F7's list. The
  starting model now loads through the same settle as F7, so the notice says "loading …" and, if
  it is not installed, tells the player to press F7 or F6. Its real memory use is in `findings.md`:
  2.8 GB on the card, against the 1.66 GB `/api/ps` reports.
- **Living Vale G5d: F7 loads only the model the player stops on** (#69): pressing through the
  list no longer queues a warm-up per model. A model is loaded once the key has rested for a
  second, the one before it is unloaded so two never share the 8 GB card beside the game, and a
  superseded warm-up is cancelled out of the queue. The notice says "loading …" until the model
  answers, and the help line fits 1280×720.
- **Living Vale G5c: pick the NPCs' model in the game** (#69): **F7** moves the `Agent` driver to
  the next model in `ValeBootstrap`'s list that Ollama has, and skips any it lacks with a
  game-authored notice beside the badge. It closes the open chat first, and keeps the executor,
  the personas and what each NPC remembers. A comparison of non-Qwen small models against
  `qwen3.5:2b`, with blind writing scores, is in `findings.md` (5 November).
- **Living Vale G5b: Mara trades through the model** (#69): the `Agent` driver registers G3's
  `quote_purchase` and `quote_sale` with targets from `NpcActionExecutor.AvailableFor`, asked again
  each turn, so an action with nothing to offer is absent from the schema. Both drivers now
  execute through the one executor. The actions and target ids are written into the prompt, a
  guard contains a quote that names the other action's target, and an NPC remembers a reply only
  once it is executed, so a reply dropped as late is not in its memory. A six-line live check is in
  `findings.md` (5 November).
- **Living Vale, milestone G5 "connect real decisions", for conversation** (#69), on
  `69-living-vale`: `AgenerelaNpcDecisionDriver` makes each speaking NPC a framework `Agent`,
  built in code from its persona (Mara and Iven so far), with the player's line as the stimulus,
  the NPC's own knowledge as observations and the framework's rolling memory per NPC. Only
  `none` is registered; a per-NPC callback is where trade actions will be registered. The model
  answers by default; F6 (or `ValeBootstrap`'s Inspector) switches to the scripted replies, and
  with Ollama down a turn fails with the game's own line. A small headless comparison of four
  local models for the NPCs is in `findings.md` (5 November).
- **Living Vale, milestone G1 "Walk and talk"** (#69), on the game's own branch
  `69-living-vale`: the URP project in `Demos/LivingVale/`, a third-person player and orbit
  camera, a grey-box village built by `VillageSceneBuilder`, two NPCs (Mara and Iven) with focus
  and a chat overlay, and a conversation layer that keeps one turn in flight, times out, and
  never applies a reply that arrives after the player cancelled, closed or switched speaker.
  NPC replies come from a scripted driver, labelled as such, behind the same boundary the real
  `Agent` will use at G5. 26 EditMode tests; the Windows player was play-checked.
- **Living Vale's combat and inventory are joined** (#69): a defeated enemy's loot goes into the
  player's inventory once (`CombatInventoryBridge`, keyed by enemy and item, so a repeat is
  refused), and a defeat costs a tenth of the player's gold, at least one, never items. The help
  panel now lists the combat keys and F6.
- **Living Vale, milestone G2 "Fight and collect", the combat half** (#69), on
  `69-living-vale`: attack, block and dodge with health and stamina, as plain-C# rules
  (`Combatant`: 20 damage, 10 stamina, one hit per swing per victim, a dodge window, a block
  that stops the swing) and a raider and a lunging wolf, each a small state machine
  (`EnemyBrain`), in a meadow south of the village. Taking damage closes the chat and an
  engaged enemy keeps it from opening; a defeat respawns the player in the village and resets
  the fight. Loot (`item_wolf_pelt`, `item_iron_scrap`) and the defeat penalty go through
  `ILootSink` and `IDefeatPenalty`, logged and free until G3's inventory takes them. 74
  EditMode tests in all. Not play-checked yet.
- **Living Vale, milestone G3 "Trade through chat"** (#69), on `69-living-vale`: the game-state
  core in `Assets/LivingVale/Game/` (design §5's twelve items with stable IDs, a 16-slot
  inventory with equipment, quick-use and protected quest storage, 40 starting gold, Mara's
  finite stock) and chat trading (design §9). Mara's `quote_purchase` and `quote_sale`
  decisions only make an exact `TradeQuote`; an inline card shows it, and its button calls
  `TradeService.Confirm`, which re-checks conversation, expiry, revisions, gold, room and stock
  and moves everything together or nothing. Replayed, expired, stale or unaffordable
  confirmations change nothing, and closing, switching or leaving range invalidates the quote.
  Both NPC drivers share one game-side `NpcActionExecutor`, which also lists each NPC's
  available actions and targets for G5 to register. **I** opens a read-only inventory panel.
  Loot enters through `ILootSink`, idempotent per event ID, for G2.
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
