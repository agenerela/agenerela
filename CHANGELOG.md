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

- **In-game checks go through the Unity CLI.** Living Vale now has `com.unity.pipeline`
  0.9.1-exp.1, as GreyBoxVillage does, so `unity command` drives its editor: Play mode, a
  captured frame, the console and C# run in the game, without taking over the user's screen.
  `AGENTS.md` makes it the way agents check a running game, with computer use only for what it
  cannot do. `environment.md` has the loop as checked on Living Vale on 10 November, and
  `Demos/README.md` adds the package to every new game. Git ignores the package's per-machine
  `ProjectSettings/Packages/com.unity.pipeline/`. Living Vale's EditMode tests are unchanged:
  577 passed, 4 skipped.
- **An Ollama server on the RTX 5080 for other machines**, in `tools/ollama-server/`: Ollama
  0.40.14 in Docker, pinned by its image tag, with the model-sweep kit's settings, behind a Caddy
  gateway on port 11436 that wants a key on every request and passes only the routes the framework
  and the kits use. The laptop runs Unity, the live tests and the kits against the 5080's card,
  at home or over the VPN. `setup.ps1` prepares the server and writes the key; `ollama_remote.py`
  checks it, lists and warms models, unloads them and pulls new ones; `AGENT_USAGE.md` is the guide
  an agent on the laptop follows, and `AGENTS.md` points to it. The kit's `sweep_one.sh` and
  `probe.py` and its Unity entry point send the key, which `sweep_one.sh` reads from that folder's
  `.env` when `OLLAMA_URL` is unset, and `LiveOllama` reads `AGENERELA_OLLAMA_API_KEY` for the
  `RequiresOllama` tests, whose own calls to the server now send it too, and which a 401 fails
  rather than skips. All nine passed through the gateway from its LAN address on 9 November. `environment.md`'s network
  section, the sweep README and run request 001 now use it, in place of opening the Ollama app's
  port, which has no login. The framework's side is the package's new `OllamaSettings.ApiKey`.
  Its logs: the gateway writes one short line per request instead of its header dump, Docker
  rotates both containers' logs (5 files of 20 MB), the health check runs every 5 minutes instead
  of 30 s, and `logs.py` shows both containers as one timeline of requests, model loads, pulls and
  problems, cutting about 9,000 lines to about 300.
- **Run requests**, in `docs/run-requests/`: a run one machine should do, written down for another
  session or person to pick up. Each is a folder with a `request.md`, what to run step by step, and
  a `results.md` to fill in, with a status that goes open, claimed, done. Request 001 is the
  13-model sweep on the interim set, for the RTX 5080. Its kit, `tools/live-runs/model-sweep/`,
  is the 9 November sweep's scripts made portable: the server, project and set come from
  environment variables, and the Unity entry point is a file to copy in, not a committed test.
  `environment.md` gains a section on serving Ollama from another machine over the local network,
  so the laptop can use the 5080's card. It is untested until someone runs it.
- **An interim evaluation set of 389 prompts**, in `UnityProject/Assets/Evaluation/Interim/`
  (#151), for A/B runs until #147's curated 200+ set exists. It is measured against the curated
  set's agent, with the same actions and 11 targets, and is a set of its own with its own
  `agent.json` and `int_` ids. It was written and first labelled by Claude, then labelled blind by
  Codex: they agreed on 391 of 392. The one split was dropped, and so were two prompts Codex's
  review of the pull request found ambiguous or too near a few-shot example. `none` is 40% of the
  expected answers, and each acting action has 46 or more, where the curated set has 4 to 7. Its
  noise floor is about 3.6 points overall and 9 per category, against 8 and 20 to 30 at 76. It is
  AI-written and interim, not a replacement for #147; its `README.md` says how it was made and
  checked.
- **GreyBoxVillageClassic keeps the grey-box village from before v2**, in
  `Demos/GreyBoxVillageClassic/`: `Demos/GreyBoxVillage` as it stood at 396f266, the commit
  before #157, with its files and `.meta` GUIDs unchanged, as a Unity project of its own named
  "GreyBoxVillage Classic". It is kept for comparison and frozen, changing only to keep
  compiling against the framework.
- **GreyBoxVillage became Ashford, a village worth exploring** (#153).
  - **Art:** low-poly art built by a Blender script in `Demos/GreyBoxVillage/ArtSource/`, 77
    models on one palette. The grey boxes stay as invisible colliders, so the NavMesh and the
    tests are unchanged.
  - **Atmosphere:** sky, fog, post-processing, water, lamps, fire, smoke and a day and night
    cycle.
  - **Controls:** a third-person camera, running, and E to talk or pick up.
  - **UI Toolkit:** one theme for the HUD, dialogue panel, speech bubbles, framework view (Tab,
    with target ids over places and the request queue), pack (I), journal (J), menu and title
    screen.
  - **Two more agents:** Odile the merchant and Captain Hale, on the same provider config.
  - **Quests and an inventory:** quests reach the model as targets through a custom
    `ITargetSource`.
  - **New game guards:** `cannot_afford`, `no_free_gifts`, `quest_not_ready` and
    `target_fits_action`.
- **GreyBoxVillage is ready to present** (#153, polish slice).
  - **Play mode changes no file:** the day and night cycle drives copies of the palette and sky,
    and the default volume profile no longer names two scripts the project lacks.
  - **No static state** outlives a play session, so it plays the same with the domain reload off;
    a test checks every static field.
  - **With no model running**, a HUD banner and a red bubble say so, and the agents stop asking on
    their own until it answers.
  - **The scene file went from 4.96 MB to about 1.2 MB:** the palette comes with the FBX import,
    the nature scatter is drawn with GPU instancing (`ScatterField`), and the particle effects are
    prefabs. The bushes, flowers and grass inside the walls now appear: the builder's check for
    buildings in the way had always found the ground.
  - **The capture tool draws the UI** (`CaptureUiFromCommandLine`) and measures the frame time.
    The layout fixes it found:
    - the agent list clear of the purse, on a panel of its own, like the key hints;
    - toasts, at most three, stacked under the quest tracker, away from speech bubbles;
    - world labels kept out from under every panel and from each other;
    - bubbles above name tags;
    - a card title that wraps;
    - opaque book, menu, framework view and title panels.
  - **Portraits** lit from the viewer's side, so Odile's face reads, and a title screen backdrop.
- **GreyBoxVillage's situations, and a five-minute presenter script** (#153, situations slice).
  Each situation chains one agent's decision into another's, through the game alone.
  - **The thief:** it takes a ware from Odile's counter and runs for the eastern road. Odile can
    `call_guard`, whose target is a guard from a custom `ITargetSource`, and her call becomes that
    guard's `Merchant` stimulus. Wren can `chase` the thief, and Bram goes after it with
    `attack_target`; caught, it drops the ware back on the counter.
  - **Night at the gate:** nightfall shuts the gate. Wren takes the night watch and alone gets
    `open_gate` and `close_gate`, and Bram goes off watch. A test checks that Bram, the evaluation
    agent, is offered the same actions by night as by day.
  - **A fire** at the bread stall: whoever sees the smoke is asked, and Wren can `fetch_water`
    from the well until three buckets put it out.
  - **Bram keeps his actions:** `chase`, `fetch_water` and the gate's actions sit on components only
    Wren carries, and a test checks that whatever happens Bram is offered nothing new.
  - **No `bell_cooldown` guard:** availability already keeps the bell from ringing twice within
    30 s, and a test checks two guards answering at once ring it once.
  - **Presenter keys** F1 to F5 (reset, the wolf, the thief, a fire, dusk) and a panel (F12).
    They work with the framework view open, not while typing.
  - **The presenter script** in the README runs end to end as a Play-mode test against scripted
    replies, and once live through `PresenterLiveFromCommandLine`.
  - **Two kit models,** `gate_door` and `bucket`, and `KIT_ONLY` to rebuild only the models named.
  - **Fixes the live run found:** a guard attacking a running thief now runs after it, and a wolf
    running for its den is no longer reported. Agents are told when it is night.
- **GreyBoxVillage's day and its weather** (#153, explore slice, first part).
  - **A daily routine:** the four villagers go to the well at dawn, to work (the fields outside the
    gate, or the market), to the square at midday, to the campfire at dusk, and home after dark,
    where the lamp by each door comes on. The bell sends them to the shelter, and they go back to
    their day when it stops. F5 puts them at the campfire, inside the gate it shuts.
  - **Every agent is told who is where,** such as "In the village: the market is busy (ada, mira),
    the square is empty, tomas and pell are working the fields outside the gate."
  - **Weather:** clear, overcast, rain and fog, changing by itself a few times a day and on a new
    presenter key, **F6**. Rain falls round the camera, the ground darkens, mist drifts and the sky
    greys, all on the runtime copies of the palette and the sky.
  - **It changes what the agents notice:** rain and fog shorten a guard's sight to 22 m and 15 m,
    so the wolf is seen later and a thief lost sooner, and every agent is told the weather and how
    far it can see. It never changes what an agent is offered.
  - **Bram keeps his actions and targets:** tests check that no hour and no weather gives him or
    takes from him an action or a target, and that the slice adds no target to the village.
  - `CaptureExploreFromCommandLine` captures the routine at four hours and each weather.
- **GreyBoxVillage's reset, after #157's landing review** (#153). F1 now cancels what the old
  agents still have queued or with the model, so the next stage never waits behind an answer nobody
  will use, and it restarts the guards' quiet timers, so nobody is asked about a quiet moment the
  moment the village resets. F3 pressed while a thief runs puts its ware back first.
- **GreyBoxVillage's woods and its sound** (#153, explore slice, second part).
  - **The woods north of the river** are a walk worth taking: paths with signposts, a moonpetal
    glade that glows after dark, the wolf's den, a ruined shrine, a lookout over the river and a
    hermit's hut. Each place found is recorded in the journal ("Places found: 2 of 6").
  - **Oswin the hermit** says fixed lines in turn, on E. He is not an agent, so he adds nothing to
    the queue the four agents share.
  - **Nothing in the woods is a target:** tests check Bram is offered the same targets wherever the
    player walks.
  - **Sound** by time and place, and effects for the bell, footsteps, the gate, the bucket, the
    wolf and the fire, with a volume slider and a mute in the menu. The clips are
    not in yet: the asset server could not be reached, so the sound is wired and silent until they
    are added to `Audio/`.
  - **Five kit models:** `shrine_ruin`, `lookout`, `wolf_den`, `hermit_hut` and `char_hermit`, and the
    woods' paths on the terrain.
  - **Frame time** is measured in two more views, the square in rain and at night, and in the woods.
  - **Each effect plays on a voice of its own,** so two at once each sound from their own place,
    and a mute or a new volume reaches an effect already playing (#207's Codex review).
- **GreyBoxVillage on #190's framework features** (the framework lab, L1). The game deletes the
  workarounds the framework now replaces, and its tests hold every rule they held, each seen red
  with the feature's use broken:
  - **`ScriptedProvider`** (#194) for every test and capture; `FakeVillageProvider`,
    `FakeVillageConfig` and `CaptureProviderConfig` are gone.
  - **`DecideOptions.Correlation`** (#195) carries who is asking and what is in sight
    (`AgentMind.Moment`), in place of three keys written into `Agent.State`.
  - **`ProviderHealth`** (#198) says when the model is down; the first retry still waits 20 s, and
    now doubles with each failure.
  - **Target categories** (#208): each action declares what it takes and `TargetCategoryGuard`
    holds it, in place of the game's `target_fits_action`; each agent's categories are on its
    `AgentBehaviour`, in place of a list on each proximity source; `GuardTargetSource` offers
    through `TargetRegistry.Offer`.
  - **Only Actions** (#208) state Bram's action set; `Responder` is gone, and `chase` and
    `fetch_water` are on the guard's body. `NightWatch` stays, since it keeps the watch.
  - **The reset (F1)** copies every setting of an agent's component, so a field the framework adds
    is kept.
  - **Not adopted:** `Agent.Backlog`, since a waiting event must be prepared when its turn comes
    (framework gap G18). The prompt switches (#193) and the paired schema (#210) stay off, as the
    lab's experiment variables.
- **GreyBoxVillage's scenario bench** (the framework lab, L2), in
  `Demos/GreyBoxVillage/Assets/GreyBoxVillage/Lab/`, for comparing models and agent recipes in the
  game itself rather than on a list of prompts.
  - **80 scenarios** written for it, across Bram, Wren, Odile and Hale: 32 commands, 16 questions
    and small talk, 12 game events, 12 bait (an unknown target, an unaffordable buy, a quest not
    ready, a villager to attack, out-of-world asks) and 8 two-turn memory probes. Each sets the
    village up after F1 (hour, weather, the wolf, a thief, a fire, the bell, the pack, quests) and
    says which answers are right, weaker or must never happen. A test holds every line to hard
    rule 4 against both evaluation sets and every agent's few-shot lines.
  - **Variants** as data: the model, temperature and context, the prompt switches, a persona per
    agent (#231's knowledge and statement rules), example statements (#218), the observation style
    and layout (#239), and memory. `baseline` sets nothing, so it is the game.
  - **The runner,** `LabBench.RunFromCommandLine`, plays them headless in Play mode, interleaved or
    sequential, through each agent's own mind, unloading every other model and warming each up
    outside the measurement (hard rule 3). It writes the framework's `DecisionLog` (#229) with the
    prompt as sent, a score per turn, a report (accuracy, `none` when it is right, bait caught,
    must-nots, latency p50 and p95, tokens, VRAM, an exact McNemar test per pair against the noise
    floor) and the statements side by side.
  - **The game, for the bench:** F1 resets each agent with `AgentBehaviour.ResetAgent` (#219) in
    place of replacing its component; `AgentMind.OptionsFor`, `Tune` and `AskAsync`; and
    `VillageCommands.EventsMuted`, so a scenario is exactly the decisions it asks for. The game's
    event texts are built in one place each, so the bench sends the game's own words.
  - **Fixed:** the target grounding's refusal line answers only the player. Odile no longer says
    "I don't sell that." to a thief.
  - **The game stays on `qwen3.5:2b`:** on the laptop beside the running game, the 4B's median decision
    was 1.18 s against the 2B's 0.87 s, over the line of about 1 s the lab set for switching
    (findings, 10 November).
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
- **`tools/benchmarks/paired_schema_probe.py`** (#190 B, DR-020): checks that Ollama compiles the
  paired schema into a grammar that keeps every reply to a pair it offers, against the flat schema
  and both again with `none` removed. Its first run, on 6 November, is in `findings.md`: no paired
  reply held a wrong pair on either chat path. A grammar check, not an accuracy claim.
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


### Changed

- **Living Vale runs on the framework features its own gap list asked for** (#190, #69). One
  commit per feature replaces the game's workaround with the framework's API: the identity's
  knowledge and statement rules (#231), target categories (#208) and a per-target check (#235) in
  place of three pair guards, fixed examples with `INamedTarget` and `ExampleTargetFallback` in
  place of three `RefreshExamples` (#234), the provider's model list, warm-up and unload and a
  provider swap in place of the game's Ollama calls and driver rebuilds (#198), one agent per NPC
  for the script and the model (#194, LV #23), `Conversation` for the chat's turns (#209),
  `Proposals` for the route cards (#236), `Agent.Id` (#217), `CanImport` (#205) and the decision
  log (#229). The game's code is 268 lines shorter (blank and comment lines not counted). What did
  not fit, and why, is in `docs/demos/living-vale-framework-gaps.md`, "The adoption". The model
  now reads the personas' facts in the system prompt and slightly different examples. Living
  Vale's EditMode tests: 568 passed, 4 skipped, of 572. A same-session check on the RTX 5080
  (findings, 10 November) found one regression, on the route examples, fixed in the branch.

### Fixed

- **The model-sweep kit no longer takes Unity Hub's CLI server for an Editor.** `sweep_one.sh`
  refused every model while `Unity Hub\resources\unity.exe serve` was running, since it counted
  any process named Unity. It now counts Editors only. `OLLAMA_SERVER_ENV` points a copy of the kit
  in a worktree at another checkout's `tools/ollama-server/.env`. Found running request 001.
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
