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

- **Painted Realm's generated worlds look like land, not figures** (#20). Coasts have bays and
  headlands, inland seas are irregular, lobes are uneven, and no land meets the frame. Small scenery
  islands lie offshore. When a world's starts are unfair, the generator searches again on the same
  geometry before making a new one. Worlds now have rivers, roads with bridges, and a development
  value per province, all drawn on the contact sheet; no rule reads them yet. The generator reports its
  progress and stops at a time budget with the best world found. Every seed now makes a different
  world than before.
- **Painted Realm: a generated world drawn as a sea chart by code** (#20, the screen half of R2).
  New game → Map: Generated, or the dev menu's `generated` command. Scripted rulers play a world
  of any size and shape, and the screen replays it over a chart drawn at the screen's size: sea,
  coast, terrain glyphs, ridges and passes, settlements, ports, special places, a compass rose, and
  names placed to fit. The owner wash, the borders and the capture work as on the Classic board,
  with ten owner colours checked to stay apart. The Classic board and its painting are unchanged.
- **Painted Realm rules v2, slice R2: the map generator, headless** (#20). GreyBoxStrategy generates
  worlds from a seed: small to huge (30 to 150 provinces), five land shapes, terrain, coasts and
  ports, ridges with passes, regions, fair capitals and starting realms, goods, population, names from
  tables and special places. Each world is checked for connectivity, shape and fairness, and made again
  from the next sub-seed when it fails. A world comes out the same on every machine, and its geometry
  is plain data for the screen to draw. The world-maker agent, the Cartographer and model-written names
  plug in through checked hooks. Today's rules play on any generated world headless.
  `ArtSource/world_sheet.py` draws exported worlds on one sheet.
- **The RTX 5080 gateway passes `/v1/systemone`**, the route Ollama (0.35 and later) serves decision
  models on, and `ollama_remote.py systemone <model>` checks it. The lead applies the Caddyfile on the
  machine with the steps in `docs/llm-wiki/non-llm-deciders.md`, "What to change on the RTX 5080 for
  decision models". No other `/v1` route opens.
- **Decision models researched** (Gemini Decision 1, Jev and the open-weight ones in the Ollama
  library), with their question format measured on `qwen3.5:4b`: 272/389 and 62/76 against 330 and 70,
  3.2 times faster. Both named models are hosted only. The open ones ran on `/v1/systemone` once
  the lead opened it: `tev1:4b` 248/389, `strands-decider:2b` 231, `d1:3b` 228, all beyond noise below
  the 4B, refusing commands. No decision-model provider recommended. They lead the page below.
- **Non-generative deciders researched and measured against `qwen3.5:4b`**, on the RTX 5080 with the
  control re-run in the same session: a new wiki page, `docs/llm-wiki/non-llm-deciders.md`, with a
  survey and the results, an entry in `findings.md` and a row in `live-runs.md`. The probe is
  `tools/benchmarks/non_llm_decider_probe.py`; its reference and tuning examples, written new and
  checked against both sets, are in `non_llm_examples.json`.
  - The best engine, `bge-m3` embeddings with a trained head, scores 271/389 and 63/76 against the
    4B's 330 and 70, beyond noise on both sets.
  - A cascade that lets the engine answer only when it is confident kept the 4B's score within noise
    while answering 28% of interim decisions itself: the one idea left to A/B.
  - No reranker runs on Ollama 0.40.14.
  - Five small models pulled onto the server: three embedders and two reranker GGUFs. No framework
    code changed.

- **Painted Realm rules v2, slice R0: settings and the map as data** (#20). GreyBoxStrategy's map is
  now data (`WorldMap`, JSON, with terrain, coasts, ports and population no rule reads yet), and
  today's board is the Classic preset, loaded from it. `GameSetup` holds a new game's settings (seed,
  map, countries, victory mode, storyteller preset, advisor) with a saved form that refuses invalid
  settings with a message. Conquest and turn-limit victory modes join today's domination, which stays
  the default until R1. A test pins today's 200-game series byte for byte. `GreyBoxStrategy.Sim` now
  references Newtonsoft JSON.
- **Painted Realm rules v2: the design for the deeper game** (#20),
  `docs/demos/painted-realm-rules-v2.md`. Design only, no game code. It records the lead's
  34 answers of 12 November and his answers to its own eight questions, and settles, for build sessions to take one slice at a time:
  - **The turn:** writs (2 to 5 orders a turn), standing orders, and a model ruler asked only about
    the court and the armies awaiting orders. About 7 s of model time a turn for six `qwen3.5:4b`
    rulers, 14 s at worst; five ways compared.
  - **A seeded map generator** with fairness and connectivity checks, special places named and told
    by a model, and a sea chart drawn by code at any resolution. Today's board stays as the Classic
    preset.
  - **Armies:** three unit types with counters, generals with traits, sieges, supply as stores, fog
    of war, and a survey of what gives army play depth.
  - **The rest:** population, food, goods and buildings shown as development; agreements and opinions
    with reasons; conquest with no turn limit; the settings and presets; what each decision asks of
    the models and the framework; the A/B each slice owes.
  - **Conquest by treaty:** armies occupy land, shown as hatching, and a peace treaty transfers it.
    The treaty is a closed set of terms bounded by a war score, answered by the other ruler.
  - **Later stages:** orders without a cap (automation, groups, a decision budget), a world shaped
    by a model through the framework, and names and histories written by a model, each with its
    A/B. Research on making the unit roster our own.
  - **The build:** eleven slices with issue-ready checklists. The lead's answers to the first
    draft's eight questions are recorded beside his other 12 November answers.

- **Painted Realm: a dev menu** (#20). F1 opens it, in the editor and in development builds.
  - Play with stand-in rulers and no card. Jump, step, autoplay and set any speed.
  - Change gold, soldiers, stability, forts, owners, wars and eliminations, and fire any of the
    storyteller's event templates. Edits are recorded and replayed at the same turn.
  - Pretend the card is held, the server is down, a round times out, or a reply is unreadable,
    without touching the real lock.
  - Readouts, screenshots, save and load a recording, and window sizes.
  - Everything runs through `DevTools.Run("…")` too. A game it touches is marked DEV on screen
    and in its recording.

- **Painted Realm: a main menu, New game, Settings and a pause menu** (#20).
  - The game opens on a main menu: New game, Replay, Settings, Quit.
  - New game chooses watch or rule, your country, the model and the seed. It lists the map,
    persona mix, storyteller and advisor as coming.
  - Settings: display mode, resolution, UI scale, VSync, frame cap, playback speed and sound
    volumes. They are saved in `settings.json` and applied at once. A display change reverts
    unless kept.
  - Esc opens a pause menu in a game.
  - In rule mode, rivals' decisions and words open as they happen; only their court's report
    stays private until the game ends.
  - Every country's army stands on the map at its capital, with its size, in every map mode.

- **Two 4B-class candidates measured against `qwen3.5:4b`** (#252's first arms), on the RTX 5080
  with the control re-run in the same session. The candidates are `gemma4:e4b-it-qat` and
  `nemotron-3.5-bolt:e4b`. Results are in `findings.md` and `model-candidates.md`'s "Measured",
  with a row in `live-runs.md` and a candidates table and a fourth game account in the
  GreyBoxStrategy README.
  - Both pass the pre-checks: no thinking, field order kept, probabilities from before the mask.
  - Correct answers, interim and curated: 330 / 329 / 324 and 69 / 69 / 66 (control, Gemma,
    Bolt). All within noise.
  - Gemma E4B handles impossible requests worse (−18.8, beyond noise). At the persona table it
    plays the control's game in shorter lines.
  - Bolt E4B plays a passive game and repeats half its statements.
  - `/api/ps` under-reports Bolt: 950 MB, about 2.8 GB real.
  - No default changed.

- **GreyBoxStrategy: the storyteller's stage S2** (#20, demo only, no framework change). Each piece
  is a modifier on `+storyteller`, off in `default`:
  - `+choices`: six dilemmas whose options the struck ruler chooses, as one more decision (`Moment.Choice`);
    a person's seat answers through `Game.PendingChoices` and `Game.Choose`.
  - `+multi`: group events that leave out the members the fairness rules protect, each member with its
    own amount or choice.
  - `+chains-free` and `+chains-skeleton`: tales over several turns, held by the game (`Game.Chains`),
    written freely by the model or on four skeletons the game registers.
  - `+history`: a recent-happenings line, and a story check that replaces an invented battle.
  - `+traits`: a terrain, a landmark and a resource for every province, and three events that need one.
  - `+title-call`: the title from a second, short call.
  - `+preset-<name>`: five storyteller presets (`StorytellerSettings.Presets`); `+s2` is every piece but
    the chains.
  - The game deals each turn's kind (a tale, a dilemma or an ordinary event) by the preset's chances.
    Effects, dilemmas, skeletons and traits are data, with no province or country named.
  - `-strategyIndex` lets one block of a longer series seat everyone as the series would.
  - Measured against S1: findings, 12 November, "The storyteller's stage S2".
- **GreyBoxStrategy: a storyteller that invents the game's events** (#20, stage S1 of
  `docs/demos/painted-realm-research.md`, demo only, no framework change). One more `Agent` with no
  body, asked before each upkeep from turn 3, picks one of eight effect templates and a country,
  group or province, and writes the title and story; the game sets the strength, keeps it fair
  through availability (a budget, no gift to the leader or blow on the weak, cooldowns, no third
  blow or gift in a row), replaces a story that names the wrong country or holds a number, and tells
  the rulers the title. An arm, `+storyteller`, off in `default`; it replaces the random event table
  (`Game.EventTable`). `StrategyTableMenu.RunInterleavedFromCommandLine` runs several arms on the
  same seeds in blocks, every run writes `events.csv` and `events.md`, and the scorecard has an
  events section. Measured against the table on `qwen3.5:4b`: findings, 12 November, "A storyteller
  in GreyBoxStrategy".
- **GreyBoxStrategy, Painted Realm: rule mode** (#20). The start menu's third choice: you rule
  one country against four model rulers with personas.
  - The rulers decide at once, in one `DecisionRound`, while your order waits. The turn then
    plays back as in watch mode.
  - The orders panel offers exactly the moves `Legality` offers a model seat in your place. A
    move that is not offered says why. Province and rival panels have Order buttons, and a peace
    offer to you opens the envoy's card.
  - Fog of war is decided and documented: you know what a model ruler is told, no more.
  - The RTX 5080's lock is taken for each round of model calls only, and the model is unloaded
    after each.
  - Tests drive the four seats with a `ScriptedProvider`.
- **Painted Realm: three fixes to #260's live game.**
  - The lock now resolves to the main checkout's from a git worktree too.
  - The server check and the unload use `HttpClient`, as the framework's provider does. Before,
    `UnityWebRequest` refused the plain-HTTP gateway.
  - The start menu fits at 1366 × 768.

- **GreyBoxStrategy, Painted Realm: a start menu and "Play it"** (#20).
  - The Watch scene opens on a menu: replay the recorded game, or watch a live game (model and
    seed).
  - A live game checks first and says why it cannot start: either gateway variable missing, the
    RTX 5080's lock held (and by whom), the server unreachable, or the key refused.
  - It takes the lock while the model plays, unloads the model, and releases the lock at once. A
    game that stops mid-way says so on the map.
  - The three-turn stop is now enforced where the game asks its rulers, and two tests drive a
    whole live session with a `ScriptedProvider`.
  - The demo README opens with the steps.

- **GreyBoxStrategy, the screen follows a live game** (#20).
  - Five `qwen3.5:4b` persona seats play through `TableRunner`, and the screen reads the
    framework's `DecisionLog` as it grows: a partial replay build, and the log read while it is
    written.
  - A turn plays only once it has fully resolved.
  - Checked with one full live game on the RTX 5080, where Ilvar won on turn 24; no timing claims.
  - Also: open panels no longer cover the map-mode bar, and following a country centres its land
    in the free map area.
  - 3 new EditMode tests.

- **GreyBoxStrategy, the screen's panels and map modes** (#20, still on the replay).
  - **Province and country panels** on click: defence, who may attack a province and why not,
    and a country's persona, treasury, last facts, offered moves and decisions.
  - **Map modes:** Political, the main one, keeps capitals and forts; Armies; Forts; and Reach,
    which shows the followed country's legal attacks with win chances.
  - **A diplomacy matrix.**
  - **Following a country** turns the top bar into its full values, the groundwork for rule mode.
  - **The map** is a 4096 × 2304 master with open sea on every side, upscaled from codex-image's
    painting and re-baked; the sea beyond it no longer repeats visibly.
  - `RealmQueries`' reach is checked against every attack the recorded game offered. 3 new
    EditMode tests.

- **GreyBoxStrategy, stage 3, slice 2: the decision inspector** (#20). Any chronicle line or
  speech banner opens the decision behind it. It shows six sections, from the recorded telemetry
  alone: the model's raw answer; the game's offered moves with the choice marked, and those not
  offered with each reason; the facts sent; the guards with target grounding and the re-check when
  the move landed; memory; model, tokens and queue time. Also: the map now fills the screen edge to
  edge over a tile of its own sea, the scroller and buttons are styled, tooltips work in play, the
  top bar turns compact below 1600 px, and banners stay clear of the panels. The URP upgrade
  Unity asked for on first open is committed, so a fresh checkout opens without the modal dialog
  that blocked the Unity CLI. 4 new EditMode tests.

- **GreyBoxStrategy, stage 3, slice 1: watch mode plays back a recorded game** (#20, working
  title Painted Realm). `Screen/Watch.unity` replays seed 4 of the v248 persona 4b table with every
  ruler's own words and no model call. `ReplayBuilder` rebuilds the game from its recorded decisions,
  since the rules are deterministic per seed, and refuses a recording the rules no longer fit.
  The map is one quad and one shader over a sea-chart painting, with a province id map baked from
  the rule graph. Owners tint the painting's hue, borders between owners redraw themselves, and a
  capture plays as the design page times it: strike, fall, spread from the attacker's side,
  settle. A UI Toolkit screen adds the top bar, realm list, filtered chronicle, speech at the
  capital and time controls. 6 new EditMode tests. Assets and their licences are in the design
  page's asset list.

- **GreyBoxStrategy, stage 3's design: the painted map and the screen** (#20), for the lead to
  approve or redirect: [`docs/demos/grey-box-strategy-screen.md`](docs/demos/grey-box-strategy-screen.md)
  with eight mock-ups (three looks for the parchment map, the capture of a province step by step,
  rule and advise modes, a court scene). It specifies the colour change to the millisecond, the
  three modes, the Why card over `DecisionFeed`, a single-shader map built from the rule graph
  with a painted fallback, and an asset list. Round 2 follows the lead's choices (look C,
  "Painted Realm" for now, dense terrain, a portrait per persona with several faces): a clickable
  grand-strategy screen in HTML, `docs/demos/grey-box-strategy-screen/ui/index.html`, with
  watch, rule and advise modes, a decision inspector, two styles, and twelve screenshots. The
  lead's answers to 28 design questions are recorded, each marked as met by the mock-up or not.
  Docs only.

- **GreyBoxStrategy: what a remembered move holds, and invented grievances** (#244 item 4, #216
  item 3, gap N7). After each turn resolves, `CountryAgent.Learn` attaches every landed move's
  line from the resolution report (`"took fenmark from orsk (34 against 21); lost 6"`) to its
  remembered decision through the framework's new `Agent.SetOutcome`. The model-seat modifiers
  `+memory-move` and `+memory-outcome` set `RollingHistory.Remembers`. The default arm keeps the
  whole decision, so no existing arm changes. The scorecard's voice section gains **claims land
  taken** and **invented grievance**: a claim against a country that took none of our land
  earlier that game. 6 new EditMode tests. Measured on `qwen3.5:4b`, 20 games an arm: both settings
  made statements name their target far less often (72% to 33% and 43%) and contradict the move
  more, beyond noise, while repetition (5%) and invented grievances (1.4%) were already rare and
  did not move. The demo keeps the whole decision. `qwen3.5:2b` was not measured. See `findings.md`.
- **GreyBoxStrategy: arms for #244's prompt options, and a rule-copy count** (#244 items 5 and 8).
  - The model-seat modifiers `+no-moves-line`, `+moves-sentence` (`LegalTargets = ObservationLine`),
    `+how-it-reads` and `+own-words` (`StatementRulesWording`).
  - The `Scorecard`'s voice section counts statements that copy their persona's whole statement
    rule, or one sentence of it.
  - At the persona 2b table, `+own-words` took those copies from 15% to none; at the 4b table,
    which never copied, it changed nothing beyond noise but statements repeated word for word (5%
    to 10%). In stage 2, on the 4B only, the framework's moves sentence kept the attack share
    within noise of the game's line but ended with fewer provinces (0.7 against 2.1, the band's
    edge), so the game keeps its line. See the demo README's gaps N8 and N9.

- **GreyBoxStrategy: models at one table** (#20). Any seat can be a model, a script or random
  (`TableDefinition`, `Tables.Named`, `StrategyTableMenu`). Each council is one framework
  `DecisionRound`, executed in the game's own order. There are five personas built only from
  `AgentIdentity`, and a `Scorecard` scoring liveliness, sense, character, voice and strength from
  the logs, with all-script and all-random tables as reference points. On the RTX 5080, 20 games a
  table: a persona explains 84% of a 4B seat's aggression (placebo 3%) and makes the game livelier
  (domination 20% → 60%, quiet turns 39% → 25%). The 2B copies its statement rule into its
  statements (new gap N9). 16 new EditMode tests. See the demo README's "Models at one table" and
  `findings.md`, 11 November.
- **GreyBoxStrategy: the grounding guard plays legal moves again** (#244 item 1, gap N4). New arm
  modifiers `+threshold-<x>` and `+alternatives-<n>`, and the `default` arm now sets the grounding
  threshold to 0.6 on top of the framework's new 20 alternatives a token. On the RTX 5080, 30 games
  an arm, the guard's containments of legal moves fell from 72 to 3 (`qwen3.5:4b`) and 94 to 3
  (`qwen3.5:2b`), and the attack share rose from 22% to 32% and 12% to 21%, as with the guard off;
  provinces within noise. The arm measured as `default` before is `control+paired+alternatives-5`.
  The framework's side, and the evaluation-set measurement, is in the package's changelog and
  `findings.md` (11 November).

- **GreyBoxStrategy: can the framework replace the moves line?** (#20, #244 item 5, #151). The
  model seat gains arms for the framework's legal-target listing (`per-target`, `per-target-paired`)
  and modifiers on any arm (`+paired`, `+listed`, `+no-grounding`). Its report adds illegal answers,
  containments on legal answers and attacks, and attacks landed. On the RTX 5080, 30 games an arm,
  `LegalTargets = PerTarget` in place of the game's line cut the 4B's attack share from 23% to 15%
  (17% with pairing), so the line stays. The paired schema took illegal answers from 30 to 0 (4B)
  and 62 to 0 (2B) at no measured cost, and is now the seat's default (`ModelSeatOptions.Default`,
  the `default` arm); stage 2's `control` is unchanged. Grounding off with pairing raised the attack
  share to 33%, sizing gap N4. A new gap, N8, is in the demo's `README.md`. 82 EditMode tests.
- **GreyBoxStrategy, stage 2: a model plays a country** (#20). A framework `Agent` built in code
  with no scene plays one seat. Its eight moves are registered once. Availability comes from the
  game's rules, action-wide and per target (`ITargetAvailability`), with categories for provinces
  and countries. Standing facts come from an observation source, and this turn's facts go with the
  call. `Agent.Execute` runs as the re-check at the moment the game lands each move, through a new
  `IMoveGate` in the simulation. A game-side line names each move's legal targets, standing in for
  stage 1's gap N1. On the RTX 5080, 30 games per arm against four scripted countries: `qwen3.5:4b`
  ends with 1.8 provinces (random 1.4, scripted 3.1 to 4.3), `qwen3.5:2b` with 0.4, and neither
  wins a game. Without the moves line the 4B's attacks fall from 15% to 2% of its moves. The
  grounding guard contains legal attacks when several provinces are open. Five arms and the
  baselines are in the demo's `README.md` and `findings.md`. The menu and the CLI entry points run a
  model seat, and the logs show each model answer and statement. 77 EditMode tests, 15 of them on
  the model seat through `ScriptedProvider`.
- **GreyBoxStrategy, stage 1: the country game, headless** (#20). `Demos/GreyBoxStrategy/` is
  a new Unity project (Universal 2D, the framework by relative path, `com.unity.pipeline`). Its
  simulation assembly has no engine references at all: five countries on a 5 × 5 map of 25
  provinces, ten of them neutral, one move each a turn (recruit, fortify, raise taxes, declare
  war, attack a province, offer peace), envoys that accept or refuse peace before anything
  lands, every move re-checked at resolution so a stale one is refused, and dice keyed by
  seed, turn and purpose so a game replays exactly. A scripted AI in two temperaments wins 98 to
  99% of games against four random players and 27% and 24% per seat in a mixed line-up; random
  wins none of 200. A runner plays N games from the editor menu, the Unity CLI or
  `-executeMethod` and writes a summary, a CSV and a per-game log of every decision with the
  facts the player saw. Facts come in two kinds from the start, standing and this turn's, for
  `ObservationPlacement.WithStimulus`. 59 EditMode tests. The design moved on from the 9
  November draft (`docs/demos/grey-box-strategy.md` says what changed and why), and the demo's
  `README.md` answers #20's four questions and checks #216 against the game.
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
  problems, cutting about 9,000 lines to about 300. It runs without `OLLAMA_NUM_PARALLEL`, which
  Ollama ignores for `qwen3.5` and which changed replies on models it does batch (findings,
  11 November).
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

- **GreyBoxStrategy marks the moves that take no target** (#244 item 2): recruit, raise_taxes and
  the two envoy answers set `ActionDefinition.TakesNoTarget`, so the framework drops a target the
  model names for one and records it, and the seat's `TargetIgnored` reads the framework's
  telemetry. The seat's own drop stays as a second check.

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

- **Painted Realm: two offers of peace to you in one turn** were answered only once. The
  replay then stopped at that turn. Each envoy now waits for its own answer. A replay that
  cannot rebuild a turn that resolved now says so on screen.

- **Painted Realm: sharp on large screens, and never silent while the card is busy** (#20).
  - **Large screens.** The UI now grows with the screen: 1.33× at 1440 and 2× at 2160. Before,
    it stayed 1080p-sized below 2000 pixels tall.
    - In the editor, a notice says when the Game view draws the game at a fixed size such as
      1920 × 1080 and stretches it. That is what the lead saw.
  - **The RTX 5080 in rule mode.** A rule game now holds the card for the whole game, as the
    lead decided, and the top bar says so.
    - It waits with the holder and a clock while another session has the card.
    - It says when the server is slow, and stops a round with no answer in 150 s.
    - Menu returns to the start menu from any game.
    - The lock records the Unity that took it, and is let go, with the model unloaded, on every
      way out: the end, Menu, stopping Play mode, quitting, a script reload, or an error.

- **Painted Realm: fixes from the night's play-through** (#20).
  - A move that is not offered shows its reason on hover again: disabled buttons got no hover,
    so the reason never appeared.
  - Once your country is out, the orders panel says so.
  - A ruler's speech no longer sits under the open inspector.
  - The README's Play it section opens with "What to try".

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
