# GreyBoxStrategy

Five countries on a map of 25 provinces, one move each a turn, until one holds 15. The design,
the rules and why they are what they are: [`docs/demos/grey-box-strategy.md`](../../docs/demos/grey-box-strategy.md).
Issue #20, a sub-issue of #47.
A deeper version of the rules, [`docs/demos/painted-realm-rules-v2.md`](../../docs/demos/painted-realm-rules-v2.md), is being built slice by slice. Its first turn, the writs turn with armies as pieces, exists headless as a setting beside the one-move game, which stays the default ([R1a](#rules-v2-being-built)).

**Stages 1 and 2 of 3 are built.** Stage 1 is the game as a headless simulation, a scripted AI
with two temperaments, a random baseline, and a runner that plays whole games and logs every
decision. Stage 2 puts a framework `Agent` in one seat and measures a model there against the
scripted AI; [its results are below](#stage-2-a-model-plays-a-country). Any seat can now be a
model, a script or random, with personas; [the table of models](#models-at-one-table) scores them.
Stage 3, the map and the screen, has begun: [watch mode plays back a recorded game](#the-screen-watch-mode-playing-back-a-recorded-game).
A [storyteller](#storyteller) that invents the game's events is built as an arm, `+storyteller`, and measured against the random event table.
[Rules v2](../../docs/demos/painted-realm-rules-v2.md), the deeper game, is being built a slice at a time; [what exists so far](#rules-v2-being-built).

## Play it

**Painted Realm** (the working title) is this project's screen: five countries, each ruled by a
language model through the framework, fighting over 25 provinces on a painted sea-chart.

**What to try:**
- **Replay:** let turn 1 play, then click any chronicle line or speech banner. The inspector
  shows what that ruler read, the moves it was offered, and what the guards did.
- **Watch live:** watch the "council deciding" dots in the top bar while the model thinks. Press
  4 for the reach map, then open a country and Follow it to see what it can attack and its odds.
- **Rule:** take Kesh. Open Report to read what a model would be told. Click Southgate and order
  the attack, then press End turn. Declare war on a neighbour, and an envoy may come with peace.
  Click a rival's chronicle line: what it did and said opens at once, and its court's report
  stays private until the game ends.
- **Storyteller:** New game → Storyteller: Saga, then Rule. When the chronicler's banner
  appears over the map, click it to open the event. Its "storyteller's decision" button shows
  the template it picked and the targets it was not offered, with the reasons. Open **Tales** in
  the chronicle. With no server, the dev menu's **Rule kesh, saga** plays the same game with
  stand-ins, and **Dilemma on you** puts the choice card in front of you.
- **Settings:** try the UI scale steps, then Esc in a game for the pause menu.

1. Open `Demos/GreyBoxStrategy` in Unity 6000.3.23f1, then open
   `Assets/GreyBoxStrategy/Screen/Watch.unity`.
   - **Set the Game view to your screen first:** in its resolution menu choose **Free Aspect**,
     set **Scale** to **1x**, and untick **Low Resolution Aspect Ratios**.
   - A fixed size such as 1920 × 1080 is drawn at that size and stretched to fill a larger
     window, so it looks soft. The game says so across the top of the map when it happens.
   - The screen's text and panels grow with the screen: 1× at 1080 pixels tall, 1.33× at 1440,
     2× at 2160.
   - Then press **Play**.
2. The main menu is the first screen: **New game**, **Replay**, **Settings**, **Quit**.
   - **New game:**
     - Choose **Watch** (five model rulers with personas play) or **Rule** (you rule one country,
       chosen by its crest, against four).
     - Choose the model (`qwen3.5:4b` by default) and the seed (Roll picks one), then **Start**.
     - **Map:** Classic (the painting) or **Generated** (a chart drawn by code), with a size and a
       land shape. A generated world is a first look: the rules' scripted rulers play it at once
       and you watch the replay. It plays in watch mode only, with no model and no server
       ([the drawn chart](#the-drawn-chart-spike-12-november)).
     - **Storyteller:** Off (the random event table, as every measured game), or one of its
       presets: Even hand, Underdog, Cruel fate, Quiet years, Saga
       ([what it shows](#the-storyteller-on-screen-13-november)).
     - The persona mix and advisor are listed as coming.
   - **Replay:** seed 4 of the persona table, five `qwen3.5:4b` rulers. Kesh wins by domination
     on turn 17. Every line is the rulers' own. It needs no model and no server.
   - **Settings:**
     - Display mode, resolution, UI scale (automatic or fixed steps), VSync, frame cap, the
       default playback speed, and sound volumes (no sound plays yet).
     - Each applies at once and is saved in `settings.json` in the game's persistent data
       folder.
     - A change of display mode or resolution reverts after 12 s unless you keep it.
     - In the editor, the Game view decides the size, and the page says so.
3. In the game:
   - Space plays and pauses, N steps one move, and + changes the speed.
   - Keys 1 to 4 switch the map modes, and D opens diplomacy.
   - **Esc** closes the top panel. With none open it opens the **pause menu**: Resume, Settings,
     or Main menu. Menu (top right) opens the same pause menu.
   - Every country's army stands at its capital with its size, in every map mode.
   - Click a province, a crest or a chronicle line to open it. A chronicle line or a speech banner
     opens the decision behind it.
   - Right-drag pans the map, and the wheel zooms.
   - **Main menu** in the pause menu ends the game and lets the RTX 5080 go.
4. When you rule a country:
   - The orders panel, bottom centre, lists every move the rules offer you. These are the moves
     a model ruler would be offered in your place. A move that is not offered says why in its
     tooltip.
   - A move with a target lists its targets. A province or a rival's panel also has Order
     buttons. **End turn** (or Enter) sends your order.
   - The four rulers decide while you choose. The turn then plays back, and the panel opens
     again for your next order.
   - A peace offer to you opens the envoy's card: **Accept** or **Refuse**.
   - **Report** shows what your court tells you, the same lines a model ruler reads.
   - **Fog of war:** you see what your court knows. Other countries' treasuries and stability
     are hidden, and so are garrisons and forts beyond your borders. What rivals do and say is
     shown as it happens: their decisions open in the inspector during the game. What their
     courts tell them (their treasuries) opens when the game is over. The design page has the
     full rule.

**What a live game or a rule game needs:**
- **Two environment variables, in the environment Unity was started from:**
  `AGENERELA_OLLAMA_URL` (the server) and `AGENERELA_OLLAMA_API_KEY` (its key).
  [`tools/ollama-server/README.md`](../../tools/ollama-server/README.md#unity-on-the-laptop) says
  how to set them for your Windows user. Restart Unity **and Unity Hub** after setting them.
- **The RTX 5080, behind its lock.** The server is the card shared by every live run. A live game
  takes `.scratch/ollama-5080-lock/held` while the model plays, unloads the model, and releases
  the lock as soon as the model is done (playback carries on from the log). A full game holds the
  card for a few minutes.
- **A rule game holds the card for the whole game,** from before the first council until it
  ends or you leave with Menu (the lead's decision, 12 November). The top bar shows
  **RTX 5080 · held** meanwhile. The lock is let go, and the model unloaded, on every way out:
  the end, Menu, stopping Play mode, quitting, a script reload, or an error.
  - **While another session holds the card,** the game waits and shows who holds it, since when,
    and how long you have waited.
  - **When the server is slow** (another client using it without the lock), the status line says
    so after 20 s.
  - **A round with no answer in 150 s** stops the game with a message.
- **The game never sits silent.** A live game's council clock runs in the top bar, and a notice
  appears after 30 s.
- **If it cannot start, the menu says why rather than wait:** a variable is not set, the lock is
  held (and by whom), the server cannot be reached, or the key is refused. A game that stops
  mid-way says so across the top of the map.

## Dev menu

**F1** opens the dev menu in the editor and in development builds. It is not in a release build,
and the pause menu says so where it is available. It is a plain, dense tool, not part of the
look. Every button runs one command line, and the same lines run from code:

```csharp
GreyBoxStrategy.Realm.DevTools.Run("scripted-rule kesh 7");   // returns what happened, as text
GreyBoxStrategy.Realm.DevTools.Run("help");                   // every command
```

A session drives it through the Unity CLI:

```bash
unity command script eval -- --code 'return GreyBoxStrategy.Realm.DevTools.Run("turn 12");'
```

| Command | What it does |
|---|---|
| `scripted-rule <country> [seed]` | Rule a country against four stand-in rulers: a `ScriptedProvider` picks a move the rules offer, through the real agents. No server, no card |
| `scripted-watch [seed]` | Watch five stand-in rulers. No server, no card |
| `generated <small\|medium\|large\|huge> [shape] [seed] [countries]` | Watch scripted rulers play a generated world, drawn as a chart. Shapes: `continent`, `two_lands`, `inland_sea`, `peninsula`, `long_coast` |
| `turn <n>` · `step` · `play` · `pause` · `speed <x>` | Jump to a turn without animating the turns before it, land one event, or play and pause. Speed is 0.25 to 32 |
| `autoplay on\|off` | Play to the end at 8× or faster. In rule mode it also holds every turn and refuses every envoy, and takes the default of every dilemma |
| `gold\|army\|stability <country> <+n\|-n\|=n>` | Change a country's numbers, within the rules' ranges |
| `forts <province> <+n\|-n\|=n>` · `owner <province> <country\|neutral>` | Change a province |
| `war\|peace <country> <country>` · `eliminate <country>` | Change relations, or take a country out of the game |
| `event <template> <target> [Title: story]` · `events` | Fire any event template at the next upkeep: the table's, the storyteller's, a dilemma (`event sellswords kesh`), or a planned tale's beginning (`event begin_pretender kesh`). In a storyteller game it takes that turn's place, and the dev menu deals the turn the kind its event needs. `events` lists them all, read from `EventRules.AllTemplates`. The rules allow none before turn 3 |
| `scripted-rule <country> [seed] [preset]` · `scripted-watch [seed] [preset]` | The stand-in games above, with a stand-in storyteller on a preset (`saga`, `even_hand`...) that picks legal events, leaning toward dilemmas and tales |
| `story-open` · `story-inspect [title]` · `tales` · `choose <option>` | Open the last storyteller event, its decision (or its title call's) in the inspector, the tales panel; answer the choice card |
| `fog off\|on` | Reveal everything in rule mode, or restore what your court knows |
| `fault lock <holder…>` · `fault lock off` | Pretend another session holds the RTX 5080. The real lock directory is never read, written or taken while this is on |
| `fault server on\|off` | Pretend the server cannot be reached |
| `fault timeout on\|off` | Requests hang until cancelled, and a rule round times out after 8 s instead of 150 s |
| `fault unreadable on\|off` | Every reply is text no parser can read. The model is never asked |
| `faults off` | Clear every pretended fault |
| `readouts` | Frames per second, UI scale, resolution, the last reply's latency and tokens, the last played decision's queue wait, requests in flight, and who holds the card |
| `screenshot [name]` · `save-recording [name]` | Save to `Logs/Dev/`. A saved recording replays with `load-recording <path>` from the main menu |
| `size <w>x<h>` | 1366x768, 1920x1080, 2560x1440 or 3840x2160, or 16:10 1920x1200 or 3840x2400 (the lead's display): the Game view in the editor, the window in a build |
| `menu` | Show or hide the dev menu |

**State edits work in rule games, at the start of the next turn.** The screen rebuilds every turn
from the recording, so an edit is recorded and applied at the same point in the game and its
replay. It shows in the chronicle as a "Dev menu:" line.
- A recorded replay cannot change.
- A watch-live game runs on the table runner, which has no hook between turns. Start
  `scripted-rule` to try edits without the card.

**A game the dev menu touched is a dev game.** That covers an edit, stand-in rulers, or a
pretended fault. The top bar marks it with a red **DEV**, and its saved recording carries
`"dev": true` and the edits. The table runner and the scorecard never go through the screen, so
they never meet the dev menu.

## Run it

**From the editor:** open this project in Unity 6000.3.23f1, then **GreyBoxStrategy → Play 200
games (mixed line-up)**. 200 games take about 3 seconds. The folder opens when it is done:
`Logs/StrategyRuns/<time>-<line-up>/` in this project, which git ignores, holding

- `summary.md`: wins per player kind, how games ended, game length, territory changes per turn;
- `games.csv`: one row per game;
- `game-<seed>.md`: one game turn by turn, every decision with the facts the player saw, the
  moves it was offered, what it chose and what came of it, and the map after each turn.

**From the Unity CLI**, with the editor open on this project:

```bash
unity command script eval -- --code 'return GreyBoxStrategy.Editor.StrategyRunMenu.Run("mixed", 200, 1);' --timeout 60000
```

**In batch mode**, with the project closed (this one takes `-quit`; only test runs must not):

```bash
Unity.exe -batchmode -nographics -projectPath Demos/GreyBoxStrategy -quit -executeMethod GreyBoxStrategy.Editor.StrategyRunMenu.RunFromCommandLine -strategyLineup mixed -strategyGames 200 -strategySeed 1 -logFile run.log
```

Line-ups (`Lineups.Named`): `mixed` (two expansionists, two cautious, one random), `expansionist-vs-random`,
`cautious-vs-random`, `scripted` (no random seat) and `random`. Stage 2's baselines are
`seat-expansionist`, `seat-cautious` and `seat-random`: that kind in one seat against two
expansionists and two cautious players. Seats rotate every game, so over any five games each kind
plays every country once.

**A model in the seat** (stage 2): the same four opponents, a framework agent in the fifth seat,
through Ollama's HTTP API. The address is `AGENERELA_OLLAMA_URL` (default `http://127.0.0.1:11434`).
The key, for a server behind the RTX 5080's gateway, is `AGENERELA_OLLAMA_API_KEY`. Both are read
from the environment and sent in headers only. From the menu, **GreyBoxStrategy → Model seat: play
5 games**. From the Unity CLI, with the editor open (the run goes on in the background and logs
its progress to the console):

```bash
unity command script eval -- --code 'return GreyBoxStrategy.Editor.StrategyModelRunMenu.Start("model", "qwen3.5:4b", "default", 10, 1);'
```

In batch mode, with no `-quit` (the editor exits when the run ends):

```bash
Unity.exe -batchmode -nographics -projectPath Demos/GreyBoxStrategy -executeMethod GreyBoxStrategy.Editor.StrategyModelRunMenu.RunFromCommandLine -strategySeat model -strategyModel qwen3.5:4b -strategyArm default -strategyGames 30 -strategySeed 1 -logFile run.log
```

`-strategySeat` also takes `expansionist`, `cautious` or `random`, for a baseline on the same
seeds. The arms (`ModelSeatOptions.Named`) are `default` (the moves line, the paired schema and
the grounding threshold 0.6, [chosen from the runs below](#can-the-framework-replace-the-moves-line)
and [in gap N4's](#the-grounding-guard-with-many-legal-targets-gap-n4-11-november)), stage 2's `control` (the
moves line, a flat schema), `after-sources`, `headline`, `no-moves-line`, `no-grounding`,
`per-target` (no moves line; the framework lists each move's legal targets,
`LegalTargets = PerTarget`) and `per-target-paired` (that, with the paired schema). Any arm takes
modifiers after a `+`: `+paired` (the paired schema), `+listed` (`PerTarget`), `+no-grounding`
(the grounding guard off), `+threshold-<x>` (its threshold) and `+alternatives-<n>` (how many
alternatives the provider reports a token), as in `control+paired+no-grounding`. Four more change
the prompt for gaps N8 and N9: `+no-moves-line` (the game's moves line off), `+moves-sentence` (the
framework writes the moves as one line among the facts, `LegalTargets = ObservationLine`), and
`+how-it-reads` and `+own-words` (`StatementRulesWording`). `+storyteller` puts the
[storyteller](#storyteller) in place of the random event table (`+inspiration-<n>`, `+no-inspiration`). A model run writes everything a scripted run writes, and adds three things:

- the model's own table in `summary.md`;
- `model.csv`, every model decision with its latency, guard and execution;
- `decisions.jsonl`, the framework's `DecisionLog`, prompts included.

Each game log shows the model's answer and its statement under each of its moves.

**Tests:** 351 EditMode tests in all, counted on 13 November. 284 are the game's, in
`Assets/GreyBoxStrategy/Tests/Editor/`: 280 run every time, about a minute once the project has
imported (the map generator's take most of it), and four long ones are run by hand. Of the game's,
twenty drive the model seat through `ScriptedProvider`, sixteen in `TableTests` the table, the
personas and the scorecard, and fifteen in `StorytellerTests` the storyteller. 67 are the screen's,
in `Screen/Tests/Editor/`.

## Rules v2, being built

[`docs/demos/painted-realm-rules-v2.md`](../../docs/demos/painted-realm-rules-v2.md) is the plan, in
slices R0 to R10. Built so far:

**R0, settings and the map as data** (12 November). Today's game plays exactly as before; what
changed is where it comes from.

- **The map is data.** `WorldMap` (`Sim/Map/`) holds countries, provinces and borders, and v2's
  province fields that no rule reads yet: terrain (plains, forest, hills, marsh, mountains), coast,
  port, population, good, region, special place. It reads and writes JSON
  (`WorldMap.FromJson`, `ToJson`), refusing a map it cannot play with the reason.
  `MapDefinition.Standard()` is the **Classic** preset (`ClassicMap`), today's 25 provinces with
  terrain set by hand from the painting.
- **The settings are data.** `GameSetup` (`Sim/Rules/`) is what the menu, the runners and the tests
  share: seed, map, number of countries, victory mode and its values, the storyteller preset and the
  advisor switch (both data only so far). Its saved form:

  ```json
  { "version": 1, "turn": "one_move", "seed": 4, "map": "classic", "countries": 5, "victory": "domination",
    "dominationPercent": 60, "turnLimit": 30, "turnCap": 150, "storyteller": "off", "advisor": true }
  ```

  `GameSetup.FromJson` refuses unknown names and invalid values with a message ("the Classic board has
  five countries, not 6"); `NewGame()` starts the game; `GameRunner.Play(setup, lineup)` plays one
  headless.
- **Three victory modes.** `domination` is today's (15 of 25, or the most after turn 30) and stays the
  default until armies arrive in R1. `conquest`: the last country standing wins; a headless run stops
  at `turnCap` and records the game as `Unfinished`. `turn_limit`: the most provinces after
  `turnLimit` turns. A ruler's first fact and goal line say the mode's goal; on domination they read
  word for word as before.
- **Proof it is the same game:** `SetupTests` pins the hash of the 200-game mixed series' `summary.md`
  and of every turn's owner grid, as measured on the commit before R0, and compares the Classic data
  with the board as it was built in code (`LegacyBoard`), field by field.

**R2, the map generator, headless** (12 November). Worlds of any size are generated from a seed,
checked, and played by today's rules. Nothing draws them on screen yet (R4); a script draws them on a
sheet.

```csharp
var made = MapGenerator.Generate(seed: 7, countries: 6, new MapSettings { Size = MapSize.Medium, Shape = LandShape.InlandSea });
// made.Passed, made.Map (a WorldMap with Geometry), made.Problems, made.Attempts, made.Fairness
var game = new GameSetup { Seed = 7, Map = "generated", Countries = 6, Generator = new MapSettings() }.NewGame();
```

- **Settings** (`MapSettings`): size (small ~30 provinces, medium ~60, large ~100, huge ~150, with 4, 6,
  8 and 10 countries by default), land shape (`continent`, `two_lands`, `inland_sea`, `peninsula`,
  `long_coast`), starting realm (2 to 5 provinces) and special places (none, few, many). The same seed
  and settings make the same world on every machine: integer noise and hashes throughout, and a test
  pins one world per size, measured under .NET and matched under Unity's Mono.
- **The steps** (`Sim/Map/MapGenerator.cs`), each drawing its randomness from the seed and its own name:
  1. land from a height field shaped by the land shape, whose proportions vary by seed. Two warps,
     one broad and one fine, give the coasts bays and headlands and keep the inland sea from being an
     oval. The land fades out before the frame, so no coast runs along it. Small land clear of the main
     land stays as scenery islands;
  2. Poisson-disc seeds relaxed three times;
  3. provinces by nearest seed after a low-frequency warp, so borders wander, with small provinces
     grown, borders smoothed, and pieces, corners and short contacts repaired;
  4. adjacency from the cells (three shared cell edges make a border);
  5. ridges that cut borders except at passes;
  6. terrain by quota from its own relief and moisture noise;
  7. coasts and ports;
  8. regions of about eight provinces;
  9. goods and population;
  10. capitals placed by a seeded search for fair starts, with starting realms that never touch;
  11. names from syllable tables, a style a region;
  12. special places.
- **The checks** (`MapChecks`) judge any world, generated or not, and say why one fails:
  - connectivity: connected, capitals reach each other, every province has borders, no ridge crossed
    except at a pass, adjacency equal to the cells;
  - shape: one piece, area within 0.5 to 1.8 times the mean, a minimum width, no corner or short contacts;
  - fairness: reach, its value, neutral land bordering each start, rival capitals near, equal realms
    that do not touch, capitals off mountains and marsh and apart, scarce goods;
  - content: names, special places.

  A world that fails is made again from the next sub-seed, up to 50 times. When none passes, the best
  is returned with its problems, and `GameSetup` refuses one whose rules graph is wrong.
- **Special places**: an old castle, a silver mine, a sacred lake and six more, on slots the code picks
  (never a capital or starting realm, four steps apart, one a region), each with a name and a line of
  story from tables. `ISpecialPlaceMaker` is where the world-maker agent and the Cartographer (R9) plug
  in, and `INameMaker` is where the model's names (R10) do. Whatever they return is checked, and falls
  back to the tables.
- **Geometry for drawing** (`MapGeometry`, in the world's JSON): the cell grid, each province's outline
  loops, centre, label point and label radius, every border and coast as polylines with its kind (land,
  ridge, pass, coast, shore), the ridge lines, and the scenery islands' outlines (cell value -4).
  Also:
  - **rivers**, from high land to the sea or an inland sea, along cell corners, each with its rank, its
    mouth, the provinces it touches and the borders it forms;
  - **roads**: highways joining every capital and tracks to the ports and the most populous provinces,
    crossing ridges only at passes, each bridge recorded;
  - each province's **development**, 0 to 4, from `Development.Of` (population, capital, port; R6's
    buildings will feed it).

  No rule reads rivers, roads or development yet.
- **Progress and a time budget.** `MapGenerator.Generate(..., progress, budget)` reports after every try
  (tries so far, the best world's problems and reach spread, a line to show). After the budget it
  returns the best world found, with its problems and `TimedOut`, so a menu shows a loading state and
  never hangs.
- **A sheet of worlds**: **GreyBoxStrategy → Export 20 generated worlds** (or
  `MapExportMenu.ExportFromCommandLine` in batch mode) writes one world per size and shape with a
  `summary.md`. `python ArtSource/world_sheet.py <folder> sheet.png` draws them.
- **Today's rules on a generated world**: **GreyBoxStrategy → Play 200 games on a generated world**, or
  `StrategyRunMenu.RunFromCommandLine` with `-strategyMap generated -strategySize medium
  -strategyShape continent -strategyMapSeed 1`. One world, every game its own dice. The rules ran on
  every size without a change.

  **Where they assume today's board** (none fixed here; each waits for its slice):
  - **The game's length:** 30 turns and 60% to win suit 25 provinces. On 60 or more, every scripted game
    ran to the turn limit (R1's pacing, or conquest).
  - **The standing facts** list every rival, one line each: 12 lines with ten countries, over the budget
    of 8 the tests hold Classic to. The page's "facts stay local" is a prompt change and owes an A/B.
  - **The moves on offer:** up to 41 moves and 20 attack targets a turn, against the page's limit of
    about 12 targets.
  - **The labels:** `SeriesSummary` names the ending "domination (15 provinces)" whatever the share.
  - **The line-ups** have five kinds, so with six or more countries a kind sits twice, and "every kind
    at every country once in five games" no longer holds.
  - **The owner grid** in the logs places provinces on a coarse grid by their centres. It shows every
    province once, but sparsely.

### The drawn-chart spike (12 November)

The screen half of R2: a generated world drawn by code in the sea-chart style, for the lead to
judge against the painting. Classic is unchanged: its painting, its baked province map and its
look.

**See it:** New game → Map: Generated, or the dev menu's **Generated world** buttons
(`generated medium continent 7`). The world is made from the seed, and a whole game is played on it
at once by the rules' own scripted rulers (`ScriptedWorldGame`, Sim's `mixed` line-up), then
replayed on the screen like any recording. The model seats' runner plays the Classic board only, and
it lives in `Agents/`, which the rules sessions own. It is a dev game: the top bar says **DEV**.

**How it is drawn** (`Screen/Chart/`, plain C#, no Unity, so the tests and a .NET harness draw it
too):
- `ChartRaster` turns the cell grid into pixels at any width. Each pixel looks up the cells around a
  point moved a little by noise and takes the province with the most weight. Corners round off and
  borders wander like inked ones, and a neck one cell wide survives.
- `ChartPainter` draws the layers, all sized in grid cells, so a chart for 1366 and one for 3840 are
  the same drawing:
  - the sea, with engraved wave marks, ripples along the coast, rhumb lines from two points and a
    compass rose;
  - the coast line, and a cliff face under every coast that faces south;
  - land toned by terrain, with a soft relief and paper grain;
  - terrain as ink glyphs placed by Poisson rejection, clear of names and towns: conifers and round
    trees, peaks with a lit and a shaded side, hills, marsh reeds, field strips;
  - ridges as a chain of peaks, open at their passes with a pass mark;
  - a settlement for each province by population (a hamlet, a village, a town; a capital walled);
  - an anchor at each port, and a gilt seal for each special place, with its own mark.
- `ChartImage` makes the chart and the shader's inputs from one raster. The **chart** is a texture
  1.25 times the screen's width (2048 to 6144). The **province map** is half that width, with 16-bit
  ids in red and green, so any number of provinces fits. Its **edge field** is as Classic's bake.
- The map shader (`PaintedMap`) reads 16-bit ids when `_IdWide` is on. The state texture has a
  column per province (at least 32), so the owner wash, the borders between owners, the focus and
  the capture animation work as on Classic.
- `ChartPalette` gives ten owner colours. The first five are Classic's. The wash keeps the chart's
  lightness and takes only hue and saturation, so the palette is checked as the map shows it: both
  colours washed over a chart tone, then their CIE distance. The closest pair is 14 apart; Classic's
  five alone are 15. A test holds it at 12 or more.
- `ChartLabels` places names as screen text, never baked, so they stay sharp:
  - a province's name at its label point (the cell farthest from its border), sized to the room
    there, hidden when it does not fit or would collide with a larger one;
  - below about 4 screen pixels a cell, region names in spaced italic capitals instead.

**What it costs** (medium world, seed 7; the editor's Mono runtime unless said):

| Screen | Chart | Province map | Time to draw |
|---|---|---|---|
| 1366 × 768 | 2048 × 1153 | 1024 × 576 | 1.4 s |
| 1920 × 1080 | 2400 × 1351 | 1200 × 675 | about 2 s (estimated; not timed after the last speed-up) |
| 3840 × 2400 | 4800 × 2702 | 2400 × 1351 | 7.3 s (under .NET: 1.7 s) |

About 4.4 s of the 7.3 s is one pass over every pixel, which runs about 15 times slower under the
editor's Mono than under .NET. Memory for the chart at 4800 wide is about 70 MB with mipmaps. A
huge world's generator takes about 12 s in the editor before any of this (R2's own number).

**What looks wrong because of today's rules** (noted, not fixed):
- The game is 30 turns with 60% to win. That is 36 of 60 provinces on medium and 90 of 150 on
  huge, which no one reaches, so every game ends on the turn limit. "Turn 8 of 30" and "90 wins"
  say so in the top bar.
- With one move a turn, a huge map changes slowly. After 8 turns most of it is still neutral
  hatching.
- Scripted rulers say nothing, so the chronicle has no speech.

**R1a, the writs turn and armies as pieces, headless** (12 November). Rules v2's turn, as a setting
beside today's: `GameSetup.Writs(seed)` (or `"turn": "writs"`) plays it, conquest with no turn limit.
The default stays the one-move game, so every pinned series and recording is unchanged. Nothing asks a
model yet (R1b); the scripted players and the random baseline play it.

- **A turn.** Each ruler has writs: 2, plus 1 for every 8 provinces it owns, at most 5 (R1b, #281: three writs, not two; the R1b paragraph below gives the rest of the retune). The court is
  asked first. Then come the armies that are waiting for orders, the most urgent first (an enemy army
  beside them, then the largest), while writs are left beyond one kept for the court. Last, a second
  court order if a writ is left. Rounds of requests all see one snapshot:
  ```csharp
  var requests = game.BeginTurn();
  while (requests.Count > 0) requests = game.Submit(answers);   // court, armies, second court, treaty answers
  game.Resolve();
  ```
  Each request carries the ruler's earlier orders this turn (`Ordered`), and an army order its army
  (`ArmyId`, which a model seat will send as the correlation).
- **Armies** (`Sim/Rules/Armies.cs`) stand in provinces, named for their general (`maren_host`). An
  order stands until it is done:
  - `march` takes the shortest path, one step a turn, hills, marsh and mountains taking two (R1b: two steps a turn over plains and forest where it meets nothing to fight);
  - `hold` lasts four turns.

  An army waits for orders when one of the wake conditions built here fires:
  - it was raised or reinforced;
  - it arrived, or its path was blocked;
  - it fought;
  - an enemy army came near;
  - war or peace was made;
  - four turns passed.

  The other two conditions, a siege ending and stores running low, wait for R3. `Country.Army` stays the
  total, so upkeep, desertion and the storyteller's events land on the armies unchanged.
- **The landing order** (`Sim/Rules/WritTurn.cs`):
  1. treaties, then declarations of war;
  2. recruits (at the capital), taxes, forts;
  3. army orders, then every army steps at once;
  4. battles, then neutral land taken and enemy land occupied.

  Every order is re-checked as it lands, with the function that offered it (`WritLegality`). Two enemy
  armies crossing one border meet on it. The winner steps through; the loser never crosses, and falls
  back from the province it was leaving.
- **Battles** keep today's formula: soldiers × a roll, the holder adding forts and the capital bonus,
  30% casualties, 60% for the winner. A province with no army defends with a militia of 5 plus 10 a fort.
  A loser falls back where it came from, or else to a bordering province it holds. One beaten 2.5 to 1,
  or with nowhere to go, is routed.
- **Occupation and treaties** (v2 5.1).
  - Beating an enemy province's defence occupies it. The owner stays, and the occupier draws half its
    income. Neutral land is taken outright.
  - The war score counts the value occupied each way, battles (±4 each, at most ±20) and capitals.
  - The court may put three terms to an enemy:
    - `offer_white_peace`: everything returns;
    - `demand_occupied`: what we occupy becomes ours; this needs the war score to pay for it, and is
      enforced without asking at 90 (R1b: 80);
    - `cede_occupied`: what they occupy becomes theirs.

    The other side answers on the same snapshot, at most two a turn. A treaty ends the war in a 5-turn
    truce (R1b: 3 turns).
  - A realm with every province occupied and no army is broken: its occupiers keep what they hold.
  - A game where nothing changes hands for 20 turns ends as a stalemate (R1b: 12 turns).

**Simulated** (50 games a line-up on Classic, 20 on a medium generated world, scripted and random
players, 12 November; no model):

| Map, line-up | Turns | One left / stalemate / at the 150 cap | Battles a game | Treaties a game | Decisions a ruler a turn (army orders) |
|---|---:|---|---:|---:|---|
| Classic, mixed | 122 | 29 / 9 / 12 | 141 | 30 | 1.94 (49%) |
| Classic, scripted | 107 | 44 / 3 / 3 | 138 | 17 | 1.86 (49%) |
| Classic, expansionist against 4 random | 123 | 10 / 22 / 18; the expansionist won 47 | 107 | 56 | 2.16 |
| Medium, mixed | 150 | 1 / 0 / 19 | 287 | 43 | 2.45 (61%) |

On Classic a ruler is asked about 0.95 armies a turn, close to the page's estimate of 1.0. **A medium
world rarely finishes within 150 turns**, so the game's length is the first thing R1's numbers have to
tune.

**R1b, first part: the writs turn's pace, battle reports, armies in snapshots** (12 November).

- **Pace.** The writs turn played too long: 123 turns on Classic, and a medium world never finished
  within 150. Measured with scripted players on the same seeds before and after:

| Scripted games (mixed line-up) | Turns, mean / median / 90th | Ended | Countries out at turns | Treaties a game |
|---|---|---|---|---:|
| Classic, before (40 games) | 123 / 121 / 150 | 23 one left, 8 stalemate, 9 at the 150 cap | 49, ... | 30 |
| **Classic, after** (40 games) | **78 / 80 / 106** | 39 one left, 1 stalemate | 18, 34, 57, 78 | 8 |
| Medium, before (20 games, 150 cap) | 150 | 1 one left, 19 at the cap | | 41 |
| **Medium, after** (20 games, 150 cap) | **141 / 150 / 150** | 10 one left, 10 at the cap | 22, 36, 55, 102, 132 | 12 |
| Medium, before, no cap (12 games) | 226 / 217 / 292 | all one left | 46, 63, 106, 164, 215 | |
| **Medium, after, no cap** (12 games) | **178 / 152 / 213** | 10 one left, 1 stalemate, 1 still going at turn 500 | 23, 31, 55, 102, 148 | |
| For comparison, after, domination at 75% | Classic 63 / 58; medium 144 / 118 | | | |

  What changed, every value a default of the writs turn only (today's game is untouched):
  - three writs, not two;
  - a march takes two steps a turn over open ground (plains, forest) where it meets nothing to fight,
    and may be ordered four steps away;
  - the militia of land without an army is 3 + 6 a fort (was 5 + 10);
  - a 12-turn stalemate (was 20) and 3-turn truces after a treaty (was 5);
  - demands are enforced from a war score of 80 (was 90);
  - the scripted rulers fight wars out instead of trading small treaties: they demand at 80 (cautious
    70), cede at −80 (−70), and offer white peace only when losing badly (`Temperament`'s new treaty
    fields);
  - an occupied province no longer adds to its owner's levy, as it already gave no income.

  Things that did not help: earlier demands (more truces, longer games); two wars at once; a realm that
  breaks when its capital falls (`GameRules.BreakWhenCapitalFalls`, off; realms keep an army to the end).
  The medium rows "after" were measured on #277's new worlds, those "before" on R2's. The last war of a
  medium world, between two large realms, is still long. Section 6.1's pressures
  (fear of the leader, war weariness, the status-quo offer) are meant to end it, and come with R7.
- **Battle reports.** `TurnReport.BattleRecords` lists every battle:
  - the sides, armies and soldiers, and the militia or garrison;
  - terrain, forts, and the fort and capital terms;
  - both rolls and strengths, the losses, the winner, a rout, and each retreat;
  - `Why`, one sentence of the three terms that weighed most ("kesh won: 51 against 9; 1 fort (+15) for
    the defence; the capital (+8) for the defence.").
- **Snapshots.** `TurnSnapshot.Armies` and `Occupiers` hold every army and occupation as the council
  saw them, so a replay or the inspector can show any past turn.

## The first real run

`mixed`, 200 games, seeds 1 to 200, standard rules, run in batch mode on 10 November 2026. The
same rules compiled outside Unity under .NET 9 give a byte-identical `summary.md`.

| Player | Seats | Wins | Win rate | Mean provinces at the end | Knocked out |
|---|---:|---:|---:|---:|---:|
| expansionist | 400 | 106 | 27% | 5.7 | 181 |
| cautious | 400 | 94 | 24% | 6.4 | 141 |
| random | 200 | 0 | 0% | 0.8 | 149 |

| Measure | Value |
|---|---:|
| Ended by domination (15 provinces) / at the turn limit | 191 / 9 |
| Game length, turns (mean / median / min / max) | 21.7 / 21.0 / 15 / 30 |
| Provinces changing hands per turn | 1.5 |
| Turns where no province changed hands | 11% |
| Battles per game | 34.7 |
| Peace offered / made | 2171 / 275 |
| Moves refused as stale at resolution | 1232 (6.2 a game) |

| Turns | 1-5 | 6-10 | 11-15 | 16-20 | 21-25 | 26-30 |
|---|---:|---:|---:|---:|---:|---:|
| Provinces changing hands per turn | 2.2 | 1.6 | 1.3 | 1.2 | 1.1 | 1.0 |
| Games still running | 200 | 200 | 200 | 137 | 42 | 9 |

One scripted player against four random players, 200 games each: the expansionist wins 197
(99%), the cautious one 196 (98%). Wins by country in the mixed run: valen 43, orsk 55, merrow
27, kesh 36, ilvar 39; the middle is the hardest seat.

## The four questions from #20

### 1. When the game calls `DecideAsync` for a country

| Moment | When | `StimulusLabel` | Offered | Priority |
|---|---|---|---|---|
| **Council** | once per living country per turn, all on the same snapshot | `Council` | the moves its state allows, from `recruit`, `fortify`, `raise_taxes`, `declare_war`, `attack`, `offer_peace`, and `none` | `Background` while spectating, `PlayerFacing` while the player waits |
| **Envoy** | when another country offers it peace, after every council move is in and before any lands | `Envoy` | `accept_peace`, `refuse_peace` (`none` counts as refusing) | the same |

`Game` makes this the caller's choice by playing a turn in three calls: `BeginTurn()` returns
one council request per living country, `SubmitCouncil(answers)` returns one envoy request per
offer, `Resolve(envoyAnswers)` lands the turn. Nothing changes between the first and the last,
so stage 2 can ask all five agents at once and await them. The game, not the framework, decides
these moments (DR-008).

### 2. The observations, literally

A request carries the two kinds of fact separately (`CountryRequest.StandingFacts` and
`TurnFacts`), because `ObservationPlacement.WithStimulus` (#239) helps only when the standing
picture sits in an `IObservationSource` and only the one to three facts about this moment go
with the call. Every line names other countries and provinces by the id a move's target uses.

**Standing facts**, from an `IObservationSource` in stage 2, at most eight lines. Valen on turn
1, then Kesh on turn 8 of game 1 in the run below:

```text
Turn 1 of 30. We hold 3 of 25 provinces; 15 wins.
Treasury: 55 gold; income 30 and upkeep 15 a turn.
Army: 30 soldiers. Stability: 4 of 5.
orsk: at peace with us; army 30, 3 provinces.
merrow: at peace with us; army 30, 3 provinces.
kesh: at peace with us; army 30, 3 provinces.
ilvar: at peace with us; army 30, 3 provinces.
3 neutral provinces border us.
```

```text
Turn 8 of 30. We hold 6 of 25 provinces; 15 wins.
Treasury: 5 gold; income 60 and upkeep 56 a turn.
Army: 112 soldiers. Stability: 5 of 5.
valen: at peace with us; army 73, 6 provinces; borders us.
orsk: at peace with us; army 92, 5 provinces.
merrow: at peace with us; army 11, 7 provinces; borders us.
ilvar: truce with us for 4 more turns; army 19, 1 province.
```

Its one fact this turn was `We accepted peace with ilvar: a truce for 5 turns.` It was offered
`raise_taxes`, `declare_war merrow`, `declare_war valen` and `none`, and declared war on merrow.

**This turn's facts**, with the call, at most three, the most important first. Every sentence
the game writes:

| Priority | Sentence |
|---|---|
| 0 | `orsk took fenmark from us; we lost 12 soldiers.` |
| 0 | `orsk declared war on us.` |
| 0 | `ashby revolted and broke away from us.` |
| 1 | `We took corwick from orsk; we lost 9 soldiers.` |
| 1 | `Our attack on fenmark failed; we lost 7 soldiers.` |
| 1 | `We held redcliff against valen; we lost 5 soldiers.` |
| 1 | `orsk refused our offer of peace.` |
| 1 | `The treasury ran dry: 6 soldiers deserted.` |
| 2 | `We took westmarch from the neutrals; we lost 3 soldiers.` |
| 2 | `orsk accepted our peace: a truce for 5 turns.` / `We accepted peace with orsk: a truce for 5 turns.` |
| 2 | `Our attack on corwick could not happen: we are not at war with orsk.` (a stale move) |
| 2 | `A good harvest brought 25 gold.` / `A poor harvest halved our income this turn.` / `Bandits killed 5 of our soldiers.` / `Unrest in the provinces cost us 1 stability.` |
| 3 | `Our truce with kesh has ended.` |

An envoy call puts the offer first: `orsk offers us peace: a truce for 5 turns.`, then up to two
of the country's own facts.

The stimulus itself is stage 2's first design question. The draft put a headline in it
(*"Turn 7 of 20. orsk took Fenmark from us."*) because an example can carry only a stimulus
(#216 item 1). With the facts split, the obvious first arm is a plain stimulus, *"Your move."*,
with this turn's facts after it; the headline is the other arm.

### 3. The moves, their targets, and what each does to the state

| Action | Target | Offered when (`[Available]` / `[AvailableFor]` in stage 2) | Handler |
|---|---|---|---|
| `recruit` | none | the treasury pays for 5 soldiers | levy 5 + 5 per province, 2 gold each, cut to the treasury |
| `fortify` | one of our provinces | 30 gold; under 2 forts there; it borders a province we don't hold | +1 fort |
| `raise_taxes` | none | stability ≥ 2 | +30 gold, −1 stability, no recovery next upkeep |
| `declare_war` | a country | at peace (not truce); it borders us; army ≥ 20 | war, both ways |
| `attack` | a province | borders ours; army ≥ 10; neutral, or held by a country we are at war with | a battle; the winner takes the province |
| `offer_peace` | a country | at war with it | an envoy; the target answers this turn |
| `none` | none | always | hold |
| `accept_peace`, `refuse_peace` | none | only in an envoy call | a 5-turn truce, or nothing |

Targets: the five country ids and the 25 province ids. Every rule is one function,
`Legality.WhyNot`, returning a reason or nothing; the legal list and the re-check at resolution
both call it. A target's current state is in `Move.Detail` (`neutral, garrison 12`,
`orsk's, 1 fort, a capital`, `at war, army 38, 6 provinces`), which stage 2 returns as the
target's `IDescribedTarget.Description`.

### 4. The statement

A one-line proclamation for the event log beside the move (*"Valen will not be insulted twice.
We march on Orsk."*). It may be empty, and then the log shows the move alone. It is never parsed
for meaning: the move is the action field. Stage 1 has no statements, since scripts don't talk.

## Stage 2: a model plays a country

`Assets/GreyBoxStrategy/Agents/`, the game's own `GreyBoxStrategy` assembly (it references
`Agenerela` and `GreyBoxStrategy.Sim`), puts a framework `Agent` in one seat.

**How the seat works.** `CountryAgent` builds an `Agent` in code, with no asset and no scene
(DR-011). Its id is the country's id, and its identity comes from `CountryInfo`. It registers the
eight moves once, as `ActionDefinition`s with `DelegateActionHandler`s:

- Action-wide availability (the handler's `isAvailable`) is `Legality.WhyNot` for the untargeted
  moves, and "some target is legal" for the targeted ones. Council moves are offered only on a
  council call, and `accept_peace` and `refuse_peace` only on an envoy call, told apart by an
  `EnvoyOffer` in `DecideOptions.Correlation` (#195's pattern).
- Per-target availability (`DelegateActionHandler.IsAvailableFor`, which is `ITargetAvailability`)
  is `Legality.WhyNot` per target, with its reason.
- Target categories: provinces are `ours`, `neutral` or `foreign`; countries are `at_war`,
  `at_peace` or `truce`. `attack` takes `neutral` and `foreign`, `fortify` takes `ours`,
  `declare_war` takes `at_peace`, and `offer_peace` takes `at_war`.
- Targets come from a `ITargetSource` that registers all 25 provinces and every other living
  country. Each is an `IDescribedTarget` whose line is stage 1's `Move.Detail`.
- Standing facts come from an `IObservationSource`. This turn's facts go with the call in
  `DecideOptions.Observations`, placed by `ObservationPlacement`.
- `RollingHistory(6)` with `MemoryCommit.OnExecute`. `ListActionsAndTargets` is on, and
  `MaxListedTargets` is raised to 40 (gap N2). By default the schema is paired
  (`DecideOptions.PairTargetsWithActions`), so the grammar keeps the model to legal move and target
  pairs; stage 2's runs below used a flat schema (the `control` arm). `MaxObservations` is 12 (8 standing lines, the
  moves line and 3 turn facts).
- A `DecisionLog` with prompts per run (`decisions.jsonl`).

**`Agent.Execute` is the re-check when a move lands.** The handlers change nothing; the game
lands every move. `Game` now asks the seat for each move about to land through `IMoveGate`, and
the seat calls `Agent.Execute`. So the framework's re-check runs at that moment, memory keeps only
what landed, and a move the game refused first (made stale earlier in the turn) is
`Agent.Discard`ed at the end of the turn. A test plays whole games with a fake model that answers
as the scripted expansionist would. Through the framework, the script plays exactly the same
game, move for move, as it does on its own in that seat.

**The N1 workaround, exactly.** A standing line after the others, built by `Facts.MovesLine`
from the legal list, naming every target each targeted move can take now. It is left out of an
envoy call, and out of a council call with no targeted move open. For example:

```text
Moves open to us: attack fenmark or westmarch; fortify ashby, brindle or stonereach; declare war on merrow or orsk.
```

### Results: the model against the scripted AI

RTX 5080, Docker Ollama 0.40.14, over the home network, one model resident, temperature 0, on
10 November 2026. One model seat against two scripted expansionists and two scripted cautious
players, with seats rotating, seeds 1 to 30 for every row. Each arm is 30 games. The baselines put a
scripted or random player in the same seat on the same seeds. They need no model, and they were
played by the same code outside Unity, which matches Unity's output exactly.

| Seat | Wins | Mean provinces at the end (sd) | Knocked out |
|---|---:|---:|---:|
| scripted expansionist | 8 (27%) | 4.3 | 19 |
| scripted cautious | 1 (3%) | 3.1 | 17 |
| random | 1 (3%) | 1.4 | 21 |
| `qwen3.5:4b`, control | 0 | 1.8 (2.6) | 19 |
| `qwen3.5:2b`, control | 0 | 0.4 (1.0) | 26 |

Over 200 seeds the same baselines are: expansionist 21% wins and 4.5 provinces, cautious 19% and
5.2, random 1% and 0.9. The cautious seat's 3% on seeds 1 to 30 against 19% on 200 is a measure of how
noisy 30 games are.

| Model | Arm | Mean provinces (sd) | Knocked out | Decisions a game | Median latency | Contained by a guard | Hold | Attack |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| 4b | **control** (WithStimulus, "Your move.", moves line, grounding on) | 1.8 (2.6) | 19 | 16.9 | 0.60 s | 21% | 31% | 15% |
| 4b | after-sources | 1.8 (2.6) | 19 | 17.4 | 0.60 s | 34% | 53% | 16% |
| 4b | headline stimulus | 2.0 (2.3) | 13 | 18.0 | 0.55 s | 9% | 14% | 10% |
| 4b | no moves line | 0.9 (1.3) | 20 | 15.6 | 0.55 s | 25% | 32% | 2% |
| 4b | no grounding guard | 2.1 (3.2) | 20 | 17.2 | 0.60 s | 12% | 20% | 21% |
| 2b | **control** | 0.4 (1.0) | 26 | 11.3 | 0.54 s | 50% | 91% | 1% |
| 2b | after-sources | 0.6 (1.3) | 24 | 12.7 | 0.40 s | 66% | 93% | 1% |
| 2b | headline stimulus | 0.7 (1.1) | 21 | 14.9 | 0.49 s | 34% | 79% | 1% |
| 2b | no moves line | 0.4 (1.0) | 26 | 11.9 | 0.50 s | 45% | 87% | 0% |
| 2b | no grounding guard | 0.3 (0.9) | 26 | 12.4 | 0.55 s | 33% | 65% | 8% |

Hold and attack are shares of council moves as the game received them, after the guards. No
decision ended in a pipeline error, and none was refused by `Agent.Execute` when it landed.
A game costs the model 11 to 18 decisions, which is about 10 s on the 4B. The whole run took
45 minutes for both models.

**What 30 games can show.** Win rate cannot separate anything here: with no model win in 300
games, a difference would need to be about 25 points to show. Mean provinces at the end has a
standard deviation of 1 to 3, so two arms 30 games apart must differ by about 1.5 provinces to
mean anything. Decision shares come from 340 to 540 decisions an arm, which is good to about
±4 points. The behaviour differences below are real; their effect on who wins is not shown.

**What it shows:**

- **Both models play at or below a random player's level.** The 4B ends with 1.8 provinces to
  random's 1.4 and the scripts' 3.1 to 4.3. The 2B ends with 0.4. Neither model won a game in any arm.
- **The moves line is what makes the 4B attack at all:** 15% of its council moves without
  the line become 2%, and its provinces fall from 1.8 to 0.9.
- **The grounding guard contains legal, sensible moves.** On turn 1 the 4B asked for
  `attack fenmark` (garrison 13, army 30) with 0.63 of its probability on that target. Three
  provinces were legal, so the rest of its probability was on the other two. Below 0.8 the guard
  turns it into `none`. Off, the 4B's containment falls from 21% to 12% and its attack share
  rises from 15% to 21%.
- **After the stimulus (`WithStimulus`) beats after the sources** on containment: 21% against 34% for
  the 4B, 50% against 66% for the 2B. Holds: 31% against 53%, 91% against 93%.
- **The headline stimulus cuts containment and holds** (4B 9% and 14%), but the moves it adds are
  recruits and tax rises, not attacks.

### What the model's play looks like

Read from 14 of the 4B's control games, move by move with its statements, and its decisions in
every arm:

- **Sensible:** on turns 1 to 3 it takes the weakest neutral provinces, citing their garrisons.
  Its attacks are well chosen: it won 89% of those that landed, against the scripts' 97%. It
  offers peace when two countries are at war with it, and accepts about a third of offers.
- **Silly: too few attacks.** It landed 2.7 attacks a game, against about 13 for each scripted
  seat. After the land grab it settles into recruit, taxes, recruit. Upkeep eats the levy, and
  soldiers desert. That is the model's fault: the facts give income, upkeep and the army every turn.
- **Silly: it repeats itself, and believes its own old words.** Seed 6 raised taxes three turns
  running with the same sentence about a poor harvest that happened only on the first. Seed 9
  held for six turns with one statement word for word. Seed 7 recruited and raised taxes for
  20 turns, saying "my army is critically low at 10 soldiers". The facts said 88, and later
  108. The memory replays its last exchanges, statements included, and the model copies them over
  the facts. That is the prompt's fault as much as the model's (gap N7).
- **Silly: wars it cannot declare.** It asks for `declare_war` on a country it has a truce
  with, or does not border, again and again (seed 2: four turns on valen). The guards contain
  each one, and the turn is lost. The moves line names the legal targets. Without the line it
  happens twice as often, so the prompt carries part of the blame.
- **A target on a move that takes none** (`recruit merrowdeep`) came 87 times in the 4B's
  control and 217 times under the headline. The seat drops it (gap N5).
- **The 2B** mostly holds or asks to fortify. The grounding guard contains most of those, and
  it attacked twice in 30 games.

The full logs are not committed. A run writes them to `Logs/StrategyRuns/` in a few minutes.

### Can the framework replace the moves line?

**No, not yet. The moves line stays, and the schema is now paired by default.** Measured on
10 November 2026 (22:17 to 23:24) after #235 (per-target checks), #245 (`TakesNoTarget`) and #246
(`DecideOptions.LegalTargets`) landed, on the same machine and by the same method as stage 2: RTX
5080, Docker Ollama 0.40.14 over the home network, one model resident, temperature 0, 30 games an
arm, seeds 1 to 30, seats rotating, arms one after another in Unity batch mode. The seat's
per-target check (`DelegateActionHandler.IsAvailableFor`, from `Legality.WhyNot`) is on in every
arm; the arms change only what reaches the model.

| Model | Arm | Wins | Provinces (sd) | Attack share | Attacks landed (won) a game | Illegal answers | Contained (on legal answers / legal attacks) | Prompt tokens |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| 4b | **A** `control`: the moves line, flat schema | 0 | 2.0 (2.9) | 23% | 2.8 (2.4) | 30 (6%) | 86 (56 / 30) | 975 |
| 4b | A again (`control`, rerun) | 0 | 1.9 (3.1) | 22% | 3.0 (2.4) | 26 (5%) | 71 (45 / 30) | 966 |
| 4b | **B** `per-target`: no line, `LegalTargets = PerTarget` | 0 | 1.0 (1.5) | 15% | 1.6 (1.2) | 17 (4%) | 29 (12 / 7) | 621 |
| 4b | **C** `per-target-paired`: B plus `PairTargetsWithActions` | 0 | 1.6 (2.6) | 17% | 1.9 (1.5) | 0 | 17 (17 / 12) | 620 |
| 4b | **D** `no-moves-line`: the per-target checks alone | 0 | 1.1 (1.5) | 2% | 0.1 (0.1) | 81 (17%) | 98 (18 / 13) | 928 |
| 4b | `control+listed`: the line and `PerTarget` | 0 | 1.4 (2.5) | 22% | 2.3 (1.8) | 10 (2%) | 31 (21 / 11) | 660 |
| 4b | **`default`** (`control+paired`): the line, paired schema | 0 | 2.6 (3.2) | 22% | 3.0 (2.6) | 0 | 72 (72 / 39) | 972 |
| 4b | `control+paired+no-grounding` | 1 | 2.5 (3.7) | 33% | 3.8 (3.1) | 0 | 0 | 965 |
| 4b | `per-target-paired+no-grounding` | 0 | 1.8 (2.6) | 19% | 2.1 (1.6) | 0 | 0 | 620 |
| 2b | A `control` | 0 | 1.1 (1.7) | 10% | 1.1 (1.1) | 62 (14%) | 132 (70 / 23) | 956 |
| 2b | C `per-target-paired` | 0 | 0.5 (1.1) | 7% | 0.8 (0.4) | 0 | 109 (109 / 44) | 619 |
| 2b | `default` (`control+paired`) | 0 | 1.0 (1.6) | 12% | 1.3 (1.0) | 0 | 94 (94 / 28) | 953 |

Illegal answers are council answers, before any guard, that were not a move the game offered.
Contained is every decision a guard turned into `none`; in brackets, those whose answer was legal,
and of those the attacks. No arm had a pipeline error, and one decision in all of them (in D) was
refused by `Agent.Execute` when it landed. Median latency was 0.59 to 0.65 s a decision on the 4B
and 0.54 to 0.60 s on the 2B, over 450 to 550 decisions an arm.

**What 30 games can show.** Every arm plays the same 30 seeds, so arms compare seed by seed. The
paired 95% band on the difference in mean provinces is about ±1.1 to ±1.5 on the 4B and ±0.7 on
the 2B, and no difference in provinces in this table is beyond it. Decision shares are better
measured: on about 480 council moves an arm, an attack-share gap of 5 points is about two standard
errors, though moves within one game are not independent, so read 5 to 8 points as likely, not
proven. A's rerun moved provinces by 0.03 and attack share by 0.3 points: the server repeats
closely, though not move for move.

**What it shows:**

- **The framework's listing does not replace the moves line.** Without the line, with `PerTarget`
  (B), the 4B's attack share falls from 23% to 15% (z −3.0), and with the paired schema too (C) to
  17% (z −2.4). Provinces fall by 1.0 and 0.4, within noise. On the 2B, C holds 0.5 provinces to
  A's 1.1, just inside the band. The gap is on turns 1 to 4, the land grab: A attacks in 57 of 120
  council moves, B and C in 21. On turn 1 of seed 1, A's prompt ends its facts with
  `Moves open to us: attack fenmark, stonereach or westmarch; fortify ashby or brindle.` and the 4B
  attacks fenmark. B's prompt carries the same pairs as `- fenmark (for attack): neutral, garrison 13.`
  in the system prompt's target list, and it recruits.
- **The listing does not suppress attacks; the line drives them.** With both (`control+listed`) the
  attack share stays at 22%. What the line adds is a sentence among the facts, in the game's own
  verbs. The listing's lines cost nothing there, and are fewer: the prompt drops from 975 tokens
  to 620 to 660, since only the 5 to 10 targets some move can take are listed, not all 29.
- **The checks alone (D) do almost nothing for the model.** They keep unavailable moves off the
  schema's action enum, but the 4B attacks in 2% of council moves and lands 0.1 attacks a game, as
  stage 2's `no-moves-line` arm found.
- **The paired schema removes every illegal answer at no measured cost.** Illegal answers fall from
  30 to 0 on the 4B and 62 to 0 on the 2B, with attack share and provinces unchanged within noise
  (4B 2.6 against 2.0, 2B 1.0 against 1.1). In B, 15 of the 17 illegal answers were
  `attack <country>`, naming a country the seat was at war with rather than one of its provinces.
  B's list names that country `(for offer_peace)`, and only pairing stops the grammar sampling it.
- **Gap N4, sized.** With pairing on, every containment is on a legal answer. In `default` the
  grounding guard contains 72 decisions (13%), 39 of them attacks. Switching it off
  (`control+paired+no-grounding`) raises the attack share from 22% to 33% (z 3.8) and attacks
  landed from 3.0 to 3.8 a game, and gave the first model win in any run; provinces stay within
  noise (2.5 against 2.6). Without the line (C) the guard contains less, 17, and switching it off
  moves little.

**The decision.** No framework arm matched A, so the moves line stays: it is still the N1
workaround. The paired schema goes on by default (`ModelSeatOptions.Default`, the `default` arm),
since it took illegal answers to none on both models and cost nothing these 30 games can see.
Stage 2's `control` stays as it was measured. The grounding guard stays on: N4 is the framework's
to fix, and the demo shows the framework as it ships.

### The grounding guard with many legal targets (gap N4, 11 November)

The guard's share counts every listed target, so a spread over legal provinces was not the
problem in itself. What it could not count was probability Ollama never reported: five
alternatives a token, the rest read as off the list. The framework now asks for 20 by default
(`OllamaSettings.TokenAlternatives`) and lets a call set the threshold
(`DecideOptions.TargetGroundingThreshold`, #244). 30 games an arm on `default`, seeds 1 to 30,
RTX 5080, Docker Ollama 0.40.14, the full method and the evaluation-set side in `findings.md`
(11 November):

| Model, arm | Provinces | Attack share | Attacks landed a game | Legal answers contained (attacks) |
|---|---:|---:|---:|---:|
| 4b `default` as it was (5 alternatives, 0.8) | 2.6 | 22% | 3.0 | 72 (39) |
| 4b + 20 alternatives | 2.1 | 27% | 3.2 | 19 (4) |
| 4b + threshold 0.6 | 2.5 | 31% | 3.7 | 15 (12) |
| 4b + 20 alternatives + 0.6 | 2.1 | 32% (z 3.5) | 3.6 | 3 (0) |
| 4b guard off | 2.5 | 33% | 3.8 | 0 |
| 2b `default` as it was | 1.0 | 12% | 1.3 | 94 (28) |
| 2b + 20 alternatives | 0.7 | 16% | 1.7 | 58 (13) |
| 2b + threshold 0.6 | 0.9 | 18% | 2.2 | 25 (10) |
| 2b + 20 alternatives + 0.6 | 0.7 | 21% (z 3.7) | 2.3 | 3 (1) |
| 2b guard off | 0.5 | 21% | 2.3 | 0 |

No province difference is beyond the paired band. What 20 alternatives at 0.8 still contains is
real probability on strings that are no target beside a legal spread (`k` 0.365, `mer` 0.315,
`orsk` 0.11, `kers` 0.07 at "declare war"); the guard also caught the model wanting to declare
war on itself (`val` 0.634).

**The decision.** The framework's default is 20 alternatives; the demo's `default` arm adds the
threshold 0.6 (`ModelSeatOptions.Default`), which plays as the guard off does while keeping the
guard on. Every other arm keeps the framework's 0.8. The arm measured as `default` before this is
`control+paired+alternatives-5`.

## Models at one table

Measured on 11 November 2026, on the RTX 5080 box (Docker Ollama 0.40.14, over the network), 20 games a
table, seeds 1 to 20. Every table used the same seeds and the same seat rotation.

**Any seat can be a model, a script or random**, chosen per seat (`TableDefinition`, `Tables.Named`).
In this project, **all five model seats share one loaded model**. An agent is a prompt, not a model:
five `Agent`s with five identities send their calls to one `OllamaProvider` and one model in the
server's memory. The server reported **3.1 GB resident for qwen3.5:4b alone**, **2.4 GB for
qwen3.5:2b alone**, 1.7 GB for gemma4:e2b-it-qat, and **5.5 GB for the mixed table**, with both
qwens loaded. Only the mixed table had two models loaded. Every other table ran with exactly one,
unloaded before and after (`resident.txt` in each run).

### What the decision round bought, and what did not fit

Each turn's council is **one `DecisionRound`**. Every country is asked at once. The game then
executes the moves in its own order (peace, home moves, wars, attacks, rotating seats), each through
`RoundEntry`. Envoy answers get rounds of their own, one for each offer a country has to answer. A
round's id is `seed-<n>-turn-<t>-council` and becomes the `ChainId` in the telemetry.
`ModelGameRunner.PlayTurnAsync` does all this, and stage 2's single-seat arms run through it
unchanged.

- **Bought:** the game no longer asks five calls in a fixed order, so nobody's answer depends on who
  was asked first. A move that lands gets into memory through `MemoryCommit.OnExecute`. A move that
  never lands is not remembered. A council costs one `await`, not five.
- **Did not fit, 1:** `RoundEntry.Stale` never fires here. The game re-checks every move before it
  calls the seat's gate. A move that an earlier one made impossible (`merrow took duskvale first`)
  is refused by the game, and the round sees it only as Skipped. The round's own staleness check
  would need the game to ask it first. That is a sequencing question for the helper, not a bug.
- **Did not fit, 2:** a round holds an agent once. A country offered peace by two others in one turn
  needs a second envoy round.
- **Did not fit, 3:** two models are two providers, each with its own queue (`MaxInFlight` 1).
  The server still answers one request at a time, so the mixed table's latency includes waiting for
  the other model. Hence no timing from it.
- **Cost of swapping models:** none measurable on this box. Both qwens stay resident at 100% on the
  GPU, and alternating them cost nothing measurable in a probe before the tables ran.

### Personas

The five personas (`Personas.cs`) use only the framework's identity: `Personality`, three
`Knowledge` lines about how the ruler plays, and `StatementRules`. Their names are warmonger,
builder, opportunist, merchant and proud. A persona rotates across seats separately from the
country. Over 20 games, each persona plays every country and, at the mixed table, meets each model.

### The scorecard

`Scorecard` computes five sections (six with the [storyteller](#storyteller)'s events) from the game logs and decisions alone, never from what a
player says of itself. Every run writes `scorecard.md` (with a one-line reason for each number),
`scorecard.csv`, and `scorecard-games.csv`, one game a row.

- **Liveliness:** length, endings, wars, peace offered and made, battles, provinces changing
  hands, land taken from a country, quiet turns, lead changes, eliminations, and how uneven the
  final map is (Gini).
- **Sense:** idle share, illegal answers, guard containments, refusals at resolution, attack win
  rate, whether it aims at the weaker, answering an attack within two turns, and accepting peace
  when losing against when winning.
- **Character:** the move mix and aggression for each persona. **The character test** is η², the
  share of the seat-to-seat variance in a move share that the persona explains, with a
  permutation p (2,000 shuffles within each game). A table without personas is scored on placebo
  slots, so its η² shows what noise looks like.
- **Voice:** statements given, length, repeated word for word, names its target, contradicts its
  move, for seats with statement rules, the statements that copy the whole rule or one sentence
  of it (gap N9), claims land taken, and **invented grievances** (since #244 item 4): statements
  saying our land was taken by a country that took none of it earlier in the game. The accused are
  the countries the statement names, else the move's counterpart (the country targeted, the owner
  of the province targeted, the envoy's offerer), else anyone. The claim is matched mechanically
  (`Scorecard.ClaimsLandTaken`: a taking verb in the past or as an accusation, and our land or
  "from us", in one sentence), so it misses paraphrases; on #249's persona 4B logs it matched about
  2% of statements, all of them real claims.
- **Strength:** placement, wins, provinces at the end, knocked out.

The all-script and all-random tables are the **reference points**: what a lively game and a
random one look like. They are not opponents to beat.

**How to read the sample.** 20 games is small. `compare.py` in the run kit pairs two tables by
seed and gives a 95% interval on the difference. A difference is called **beyond** only when that
interval excludes zero, that is, when it is larger than what the same table varies between its own
games. Everything else is **within**, however large it looks.

### The tables

On the framework after #248, the `default` arm (grounding threshold 0.6, 20 alternatives a token):

| Measure | all scripts | all random | plain 4b | persona 4b | mixed 3×4b + 2×2b | persona 2b |
|---|---:|---:|---:|---:|---:|---:|
| turns | 22.9 | 30.0 | 28.8 | 25.1 | 25.4 | 30.0 |
| ended by domination | 95% | 0% | 20% | 60% | 75% | 0% |
| wars declared a game | 7.0 | 12.8 | 6.6 | 5.8 | 5.1 | 5.0 |
| peace offered / made a game | 11.8 / 1.1 | 16.0 / 8.9 | 3.2 / 1.5 | 8.1 / 1.8 | 7.1 / 2.1 | 6.3 / 2.1 |
| battles a turn | 1.62 | 0.89 | 1.23 | 1.46 | 1.15 | 0.72 |
| provinces changing hands a turn | 1.52 | 0.64 | 0.93 | 1.14 | 0.92 | 0.53 |
| turns with no change | 12% | 54% | 39% | 26% | 35% | 64% |
| lead changes a game | 2.6 | 1.5 | 1.5 | 1.5 | 1.4 | 0.9 |
| eliminations a game | 2.5 | 0.3 | 1.3 | 1.5 | 1.4 | 0.2 |
| final map Gini | 0.62 | 0.30 | 0.48 | 0.54 | 0.53 | 0.27 |
| idle share | 0% | 13% | 3% | 10% | 10% (4b), 34% (2b) | 33% |
| contained by a guard | – | – | 0% | 2% | 2%, 1% | 1% |
| aims at the weaker | 95–100% | 63% | 61% | 63% | 73%, 47% | 73% |
| answers an attack | 63–77% | 64% | 53% | 74% | 76%, 72% | 69% |
| η² aggression (p) | 0.009 (0.39) | – | 0.030 (0.60), placebo | **0.841 (<0.001)** | 0.432 (<0.001) | 0.295 (<0.001) |
| words a statement | – | – | 37.8 | 13.6 | 13.5 (4b), 14.1 (2b) | 15.5 |
| repeated word for word | – | – | 3% | 5% | 6%, 48% | 54% |
| names its target | – | – | 97% | 72% | 62%, 32% | 29% |
| contradicts its move | – | – | 2% | 18% | 28%, 23% | 18% |

Before #248, on the old default (threshold 0.8, five alternatives), a separate set. Persona 4b and
plain 4b only. The guard contained 23% and 17% of decisions, almost all of them legal attacks. Both
tables played a slow game: 95 to 100% ended at the turn limit, and half the turns were quiet.
Within that set, personas still raised η² aggression from 0.036 (p 0.51) to 0.524 (p < 0.001), and
peace offered from 2.1 to 7.75 a game (beyond).

**Seconds a decision**, from single-model tables only, with the other models unloaded: qwen3.5:4b
0.46 s median, 0.55 s 90th percentile, with personas; 0.65 / 0.85 s plain, whose statements are
three times longer. qwen3.5:2b with personas: 0.35 / 0.45 s; gemma4:e2b-it-qat 0.30 / 0.35 s.
A 20-game table took 16 to 30
minutes. A council is five calls in one queue, so a turn of five model seats takes about five
decisions' time.

**Two 4B-class candidates with personas** (12 November). Each ran its own table with the control
re-run beside it in the same session, on origin/test 469c900, with the `default` arm (findings, 11
and 12 November, "Two 4B-class candidates"). The re-run control reproduced the persona 4b column
above exactly. Each candidate is paired by seed with that control.

| Measure | persona 4b (control) | persona gemma4:e4b-it-qat | persona nemotron-3.5-bolt:e4b |
|---|---:|---:|---:|
| η² aggression (p) | 0.841 (<0.001) | 0.871 (<0.001) | 0.826 (<0.001) |
| ended by domination | 60% | 60% | 60% |
| battles a turn | 1.46 | 1.41 | 1.01, beyond |
| peace offered / made a game | 8.1 / 1.8 | 6.1 / 1.4 | 4.2 / 0.4, beyond |
| turns with no change | 26% | 28% | 38%, beyond |
| idle share | 10% | 10% | 39%, beyond |
| answers an attack | 74% | 59%, beyond | 65% |
| words a statement | 13.6 | 10.6, beyond | 8.3, beyond |
| repeated word for word | 5% | 7% | 55%, beyond |
| copies a sentence of its rule | 0% | 1% | 15%, beyond (merchant 70%) |
| names its target | 72% | 67% | 61% |
| contradicts its move | 18% | 18% (proud 73%) | 5%, beyond |
| invented grievance | 1% | 0%, beyond | 0%, beyond |
| seconds a decision, median / 90th | 0.49 / 0.55 | 0.44 / 0.49 | 0.35 / 0.40 |

Gemma E4B plays the control's game in shorter lines, and none of them invents a grievance. Its
proud ruler, though, says "My honor demands..." on almost every move. Bolt E4B plays a quieter
game: the builder holds 80% of its turns. Half its statements repeat an earlier one, and the
merchant's favourite is its own rule plus the sentinel, "A trader's voice: profit, price or a
deal. no_target".

### What the numbers say

- **Same game, same model, different persona, measurably different play: yes.** On qwen3.5:4b, the
  persona explains 84% of the seat-to-seat variance in aggression (p < 0.001). The placebo slots of
  the plain table explain 3% (p 0.60). The warmonger attacks with 60% of its moves and never
  offers peace. The builder attacks with 5% and holds 37% of its turns. The merchant accepts every
  peace offer, and the proud ruler almost none (0% when losing, 6% when winning).
- **Personas also make the 4B's game livelier**, compared with the same model plain, paired by
  seed, every one of these beyond: battles a turn +0.24, provinces changing hands a turn +0.21,
  quiet turns 38% → 25%, domination 20% → 60%, peace offered 3.2 → 8.1 a game, games 3.7 turns
  shorter. Wars declared, lead changes and peace made are within.
- **#248 changed the game more than the personas did.** Persona 4b before and after: battles +0.51
  a turn, land taken from countries 10.9 → 18.5 a game, domination 5% → 60%, all beyond.
- **The 2B plays a quieter, weaker game, and its persona shows less.** Placement 3.45 against the
  4B's 2.70 at the mixed table (beyond). Idle share 35% against 10%, and it aims at the weaker 44%
  against 74% (both beyond). η² aggression is 0.30 at its own table.
- **A third model, gemma4:e2b-it-qat (1.7 GB), with personas:** η² of aggression 0.713, nearly the
  4B's. It plays a slower game, though: 15% of games end by domination, 46% of turns are quiet,
  and its attack win rate is 61%. Its voice is the cleanest: 9.5 words a statement, no rule
  copied, 4% repeated, 7% contradicting the move. Decisions take 0.30 s median.
- **The voice is where the illusion breaks.** Statements are short with a persona (13 to 15 words)
  and run to 38 words without one: a plain model writes analyst's notes. The 2B repeats itself word
  for word in half its statements. Most of those repeats are the persona's own statement rule
  copied out ("A trader's voice: profit, price or a deal. One sentence, under 25 words. If the move
  has a target, name it."): 373 of the merchant's 582 statements at the 2B table, 76 of the
  warmonger's. That is gap N9. The 4B does it twice in 2,318. About a fifth of statements name
  someone other than the target.

### Three games

**Persona 4b, seed 4: kesh, the proud ruler, wins by domination on turn 17.** It opens on the
neutrals with lines that already sound like grievances ("Harrow must bleed for daring to stand
near our sacred soil"), then declares war on valen, the builder, with "Valen's peace is a lie; they
will bleed for taking our land back". Valen had taken nothing. Valen holds turn after turn
("We remain patient, fortifying ashby and saving gold"), offers peace on turn 7, and is refused
("Valen took our land; we will not accept their peace until they bleed for it"). By turn 10, kesh
attacks with 81 against 7. Meanwhile orsk, the opportunist, picks off ilvar, the warmonger, who is
fighting it alone ("Ilvar's capital is his pride; I strike while he fights on two fronts"), and
makes peace with merrow to do it ("A truce with the strong buys time to crush the weak"). It reads
like people playing: a slighted ruler snowballing, a schemer sizing up the weak, a warmonger
overreaching. The illusion breaks on a few lines. Kesh's first line ends with its own instruction
("...one sentence under twenty-five words"). Kesh is insulted by a neutral garrison. The builder
says "We fortify brindle" on a turn it held.

**Mixed table, seed 3: orsk, the 2B warmonger, wins at the turn limit, the 2B's only win in 20.**
The 4B's proud valen and opportunist kesh fight a long feud ("Kesh stole our land; we will not
accept their hollow peace until they pay!"). Kesh keeps offering truces ("I offer Valen a truce to
turn my focus to their weak provinces") until both offer peace on the same turn. Orsk recruits and
fortifies, then takes valen's provinces with 91 against 14 and 111 against 57. It has one phrase
("Attack stonereach to secure our border and crush the enemy's weak hold!") and reuses it for a
fort as well. The other 2B seat, ilvar the merchant, says one thing in all 33 of its turns, whatever
it does: its statement rule, word for word. The 4B seats read like rulers. The 2B seats
read like a template. Ilvar declaring war "in a trader's voice" breaks the illusion for good.

**Plain 4b, seed 4: orsk wins at the turn limit.** Without a persona, the same model plays a
sound and rather even game. Every country expands onto the neutrals, then wars start on turns 4
to 9. Merrow builds an army of 182 over four quiet turns before it declares war. Every statement is
a staff officer's memo, 30 to 70 words ("I have recruited another 10 soldiers, bringing my army to
172. My stability remains high at 5/5, and while I hold 9 provinces, I am still short of the 15
needed to win..."). Orsk declares war on valen with a statement calling exactly that "a strategic
error that will split our forces". The game is reasonable. What breaks the illusion is that nobody
in it has a voice: five copies of one advisor.

**Do they read like people playing?** With personas on the 4B, mostly yes. Feuds, refusals and
opportunism follow from the persona and the facts, and most lines fit the move. The things that
break it, most damaging first:

1. The 2B copying its statement rule (gap N9).
2. Grievances that never happened ("they will bleed for taking our land back").
3. A statement naming one target while the move takes another, or talking of a fort on a turn
   it held.
4. Repeated phrases.

**A fourth game: persona gemma4:e4b-it-qat, seed 4. orsk, the opportunist, wins at the turn limit.**
This is the same seed as the persona 4b account above, so the same map and rotation, but a
different game. Kesh, the proud ruler, wins nothing this time. The voices are distinct from the first
turn. Orsk sizes up the weak ("A weak neutral target offers a swift gain before the others
awaken"). Merrow talks like a trader ("A cheap province is a good bargain; let's acquire Fenmark",
"Glimmerford is a small prize; let's add it to our growing portfolio"). Ilvar talks like a
warmonger ("Duskvale falls to Ilvar; the map bleeds with our ambition") and refuses every truce
("Peace is a pause, not a surrender; we march on Merrow"). The game turns on ilvar fighting
everyone: kesh, orsk and merrow take six of its provinces in five turns. Orsk offers ilvar peace
"to turn my gaze toward a softer neighbor", is refused, and takes duskvale with 40 against 3. Valen
the builder holds and fortifies for eight turns, then declares war on kesh "to challenge their
dominance", fails at westmarch, loses brindle and highmoor, and spends the rest of the game recruiting "to replace our
losses". Kesh refuses peace from both, and this grievance is real ("We do not negotiate with those
who steal our soil", after orsk took four of its provinces). Orsk then takes keshtar, kesh's
capital ("Kesh's capital is weak; I strike at Keshtar"), and holds the most provinces at turn 30.

**What breaks the illusion is kesh's voice.** For its first nine turns it says one sentence, "My
honor demands that I crush those who dare to stand against Kesh.", over a raid on a neutral, a
recruit, a fort and a declaration of war alike. Only when valen attacks does it find new words
("Valen dares to challenge Kesh; they shall pay for their insolence"). The other four seats read as
well as the 4B's, in fewer words, and nobody invents a grievance. It is the persona 4b game's
weaknesses swapped: no false grievances, but one ruler stuck on a formula.

### Running a table

```bash
Unity.exe -batchmode -nographics -projectPath Demos/GreyBoxStrategy -executeMethod GreyBoxStrategy.Editor.StrategyTableMenu.RunFromCommandLine -strategyTable personas -strategyModel qwen3.5:4b -strategyArm default -strategyGames 20 -strategySeed 1 -logFile run.log
```

The tables (`Tables.Named`) are `models` (five model seats, no personas), `personas` (five model
seats with personas), `two-models` (`-strategyModel2`; seats A, B, A, B, A, with personas),
`model-vs-scripted`, `scripted` and `random`. A comma-separated seat list is also a table, such as
`model,expansionist,cautious,random,model`. `-strategyPersonas on|off` overrides the persona
setting. `-strategyArm` takes stage 2's arms and modifiers, among them `+memory-move` and
`+memory-outcome` for what a remembered move holds (gap N7). Each run writes a `ModelReport` for each
model in `summary.md`, the scorecard files, `games.csv`, `model.csv`, `decisions.jsonl` and the game
logs. From the editor: `StrategyTableMenu.Start(table, model, model2, arm, games, firstSeed,
personas)`.

## Storyteller

Stage S1 of the research page's storyteller ([`painted-realm-research.md`](../../docs/demos/painted-realm-research.md#1-a-storyteller-that-creates-events),
design A), built in the demo only, with no framework change. **An arm, off in `default`:** `+storyteller`.

**What it is.** One more framework `Agent`, `Storyteller`, with no body (DR-011). Before each turn's
upkeep from turn 3 the game asks it once, alone, on the state the last turn left: *"Turn 7. What
happens?"* It answers with one of the game's effect templates and a target, or `none`, and writes
the title and the story in its statement (*"The Golden Star: A meteor of gold fell in the hills of
valen."*). The game does everything else.

| Template | Takes | What the game applies |
|---|---|---|
| `windfall` | a country or a group | + gold, one turn's income, 20 to 60 |
| `bad_harvest` | a country or a group | its income halved this turn |
| `plague` | a country or a group | − 15% of the army, at least 3 |
| `volunteers` | a country | + soldiers, 5 + 3 a province, at most 30 |
| `zeal` | a country or a group | + 1 stability |
| `unrest` | a country | − 1 stability, and no recovery at that upkeep |
| `stronghold` | a province | + 1 fort there |
| `walls_crumble` | a province | − 1 fort there |

Targets are the living countries (category `country`), six groups (`group`: `everyone`,
`the_north` valen and orsk, `the_south` kesh and ilvar, `the_leader` and `the_weakest`, whoever alone
holds the most and the fewest provinces, `all_at_war`), and the provinces somebody holds
(`province`). Each is an `IDescribedTarget`; the schema is paired. `desertion` and a `discovery`
were left out: one is `plague` again, the other `windfall` again, and nothing lasting fits without
the fact ledger.

**Fair by availability.** Every rule is `EventRules.WhyNot`, the storyteller's per-target check
(`IsAvailableFor`) and the re-check when the event lands at upkeep:

- none before turn 3;
- a budget: a country whose events over the last 5 turns are worth +60 gold or more gets no gift,
  −60 or less no blow (soldiers count at 2 gold, stability at 20, a fort at 30);
- no gift to a country at 12 provinces or more, no blow on one with fewer than 3;
- a sickness only on an army of 20 or more, unrest only at stability 3 or more, no fort falls in a
  capital;
- cooldowns: a template not again within its last 4 events, a target not twice in a row, a
  country not struck twice in a row through one of its provinces, and no third blow (or gift) in a
  row;
- a group is open only when every member is.

Nothing in the list takes a province or a capital, so no event can end a country or win the game.

**The story check** is in the game (`EventRules.CheckStory`), because a framework guard can keep or
contain the whole decision but not replace the statement alone (gap 4). The statement splits at its
first colon into title and story. A story that names a country the event did not reach, or a
province of one, that holds a digit, or that runs past 70 words, is replaced by the template's own
line, and the effect stands. `StoryVerdict` says what happened.

**What the rulers read**, in the game's words, among this turn's facts:

| Who | Priority | Line |
|---|---|---|
| a country hit | 1 | `Event "The Golden Star": a windfall of 30 gold reached our treasury.` |
| everyone else | 3 | `News: valen received a windfall of gold ("The Golden Star").` |

They read the title, never the story (an S2 arm). The game log, `events.md` and `events.csv` keep
both.

**What the storyteller reads.** Its statement rule: *"A title of two to five words, a colon, then one
or two sentences telling what happened, as in "Title Of The Event: what happened to whom." Name only
the country or province it strikes. No numbers."* Its identity (*"The Chronicler"*, a wry chronicler of fortune and
calamity; goals: memorable and different, fair to those falling behind), the eight templates as its
actions with their effects, the targets, and standing facts: the turn, every country's standing,
`Events open now: windfall on kesh, merrow or the_weakest; ...` (the rulers' moves line, for the same
reason, N1), `Told so far:` its last five events by title, template, target and turn (not its
replies: no memory, gap 6), and `Inspiration, if you want it: a drowned bell, wolves.`, two of 64
images drawn from the seed and the turn (`+inspiration-<n>` draws others, `+no-inspiration` drops it).
Temperature stays 0, on the rulers' provider.

**With the arm on, the random event table is off** (`Game.EventTable`), so the storyteller's one
event a turn replaces the harvests, bandits and unrest rather than adding to them. `Game.QueueEvent`
throws if the table is on, and the table cannot be switched on with an event queued. Table events
are recorded in the same ledger (`TurnReport.Events`), so both arms are scored alike.

**Run it** as any table, with the arm: `-strategyArm default+storyteller` (stage 2's single-seat runs ignore it). Several arms on the same
seeds, interleaved in blocks, in one editor:

```bash
Unity.exe -batchmode -nographics -projectPath Demos/GreyBoxStrategy -executeMethod GreyBoxStrategy.Editor.StrategyTableMenu.RunInterleavedFromCommandLine -strategyTable personas -strategyModel qwen3.5:4b -strategyArms "default;default+storyteller" -strategyArmNames "control;storyteller" -strategyGames 20 -strategySeed 1 -strategyBlock 5 -strategyOut <folder> -logFile run.log
```

Each arm's folder gets the usual files plus `events.csv`, `events.md`, and with the storyteller
`storyteller.csv`. The scorecard has an **events** section.

### What it did: the storyteller against the random table (12 November)

`personas` table, five `qwen3.5:4b` seats with personas, 20 games an arm, seeds 1 to 20, RTX 5080,
Docker Ollama 0.40.14 over the home network, one model resident, temperature 0. Four arms
interleaved in blocks of five seeds in one editor (00:38 to 02:03): **control** (`default`, the random
table), **story A** (`default+storyteller`), **story B** (`+inspiration-1`, the same with other
inspiration words), and the control again. The full method and every number are in
[`findings.md`](../../docs/llm-wiki/findings.md), 12 November, "A storyteller in GreyBoxStrategy". A
difference is **beyond** when `compare.py`'s 95% interval, paired by seed, excludes 0.

**The control ran twice and played the same 20 games move for move**, so on this server the seeds are
exact: every difference below comes from the arm.

| Per game, mean | control | story A | story B |
|---|---:|---:|---:|
| events a game | 25.1 | 23.6, within | 22.1, within |
| what they moved, gold a game | 527 | 690, **beyond** | 683, **beyond** |
| gifts among them | 31% | 42%, **beyond** | 42%, **beyond** |
| kinds of event a game (of 8; the table has 4) | 3.95 | 6.65, **beyond** | 6.20, **beyond** |
| distinct kind-and-target pairs | 14.1 | 16.7, **beyond** | 15.7, within |
| net value Gini between the five | 0.54 | 0.64, **beyond** | 0.63, **beyond** |
| value tracks standing (r) | −0.15 | −0.37, **beyond** | −0.33, **beyond** |
| hit rulers whose statement names the event | 21% | 29%, **beyond** | 31%, **beyond** |
| peace offered a game | 8.05 | 11.25, **beyond** | 10.95, **beyond** |
| words a statement | 13.6 | 12.7, **beyond** | 12.7, **beyond** |

Every other liveliness number (turns, endings, wars, battles, land changing hands, quiet turns,
lead changes, eliminations) is within noise in both, and so is every sense and voice number of the
seats taken together, except in story B a higher idle share (10% to 15%) and more attacks on the
weaker (63% to 69%), both just beyond. Persona still explains the play: η² of aggression 0.84, 0.81
and 0.82. The warmonger wins 13, 12 and 15 of the 20.

**The storyteller itself:** 473 and 444 calls, no illegal answer (the paired schema), no containment,
none chosen in 0.4% and 0.7%, one unreadable reply per arm (a 20 s runaway), no event refused when it
landed, 0.69 s median a call on about 1,340 prompt tokens. Of its stories, 4.9% and 3.4% were
replaced (most for a number, a few for a wrong country); 47% and 44% came with no title; of the
titles in a game, 59% and 65% were not repeated in it; 72% and 77% of titles also appear in another
game; a story shares 18% and 16% of its two-word phrases with the game's earlier stories.

**Divergence.** The two storyteller runs, same seeds, other inspiration words: 22% of provinces have
another owner at turn 10 (sd 13), 45% at turn 20 (sd 18); 19 and 20 of 20 games differ. The control
against its rerun: 0% and 0%.

**What it shows:**

- **It is livelier where it was asked to be, and costs the game nothing measurable.** Twice the
  kinds of event, a third more weight, more gifts, and rulers who mention what hit them; the same
  battles, conquests, wars and endings.
- **It is a rubber band.** Its events lean against the leader (r −0.37; `the_leader` is its favourite
  group, 56 and 61 times) and help the weak: the builder, usually the weakest seat, got +461 and +468
  gold of events against −400 in the control, the warmonger −1,467 and −1,431 against −740. The
  leader still wins as often.
- **Two personas play differently, in both runs.** The builder holds far more (39% of its council
  moves to 61% and 71%), on the turns no event reached it: gifts let it sit on a full treasury. The
  opportunist offers peace twice as often (15% to 27% and 37%). Twenty games, five personas and some
  twenty numbers each make a few "beyond" results likely by chance; these two replicate.
- **Titles are the weak part.** Without an example in the rule, the 4B wrote no title in any of 62
  events (three smoke games, two wordings); with one, about half. When it does, it often takes the inspiration words
  ("A Drowned Bell", "A Broken Crown"), which is why titles repeat across games, or copies a title
  from the "told so far" line, which in seed 14 of story A made "The Iron Hermit's Curse strikes
  again" nine times. The fallback titles it reads there ("The Sickness") get copied too.
- **The stories mostly match their effects.** One reader (this session's author, not blind) labelled
  50 drawn at random: the right sign in 48, the right country in 47 (three name none, none names a
  wrong one), an invented consequence in 3 (unrest told as a famine; zeal with "the flames of war
  ignite in Valen's capital"), a wrong magnitude in 2 ("a thousand volunteers" for 20), and 6 flat,
  reading like the effect's own description ("the_leader's income is halved this turn.").

### One game: seed 4, with and without the storyteller

Without it (the control, the game [above](#three-games)): kesh, the proud ruler, wins by domination on
turn 17. With it, the same seats, the same personas, the same dice:

The storyteller opens on valen, the builder: *"A blight of gray mold has stricken the fields of
Valen, halving their income this turn"* (turn 3), then *"A Child King's Burden: The young ruler of
Valen, burdened by the memory of a bad harvest, finds his people grumbling in the streets"* (unrest,
turn 5). It remembers its own blight. Once kesh leads, it goes after the leader: *"A foul mist rises
from the marshes of fenmark and sweeps over keshtar, striking the leader with a sickness"* (turn 6),
*"A Blight Upon the Leader"* (turn 8), *"The Sickness of the Soil: Kesh's fields turn to dust, and its
people grumble at the leader's greed"* (turn 10). Kesh answers in character, raising taxes "to fund
our vengeance against those who stole our land", and fortifies fenmark "to withstand the coming
storm while our treasury bleeds". By turn 20 kesh and merrow hold 10 provinces each, and the
storyteller strikes whichever leads: *"A Locust Swarm: The great harvest of Merrow is swallowed by the
sky"* (turn 13), *"A White Stag: The leader merrow suffers a bad harvest"* (turn 18). Meanwhile it keeps
valen alive on one province: volunteers on turns 14, 19, 24 and 29, *"A golden meteor streaks across
the northern sky, raining sudden wealth upon Valen's single province"* on turn 16. Valen reads the
news and waits: *"We save our gold while Kesh's sickness and Merrow's famine weaken their resolve"*,
*"We hoard the windfall and wait for Orsk's unrest to turn into an army"*, and on turn 29, with the
storyteller's *"A Broken Crown"* on kesh the turn before, *"We attack Westmarch while Kesh's broken
crown weakens their resolve."* Ilvar, the warmonger, is out on turn 17. Nobody reaches 15: orsk, the
opportunist, wins at the turn limit with 7, level with merrow on provinces; kesh 6, valen back to 5.

It reads like a game with a narrator: a leader brought low by a run of bad luck, a small king kept
alive by miracles and talking about everyone else's misfortunes. What breaks it: the narrator is
monotonous in its targets (the leader, again), half its events carry the template's own title, and
two zeals and two unrests lost their stories to the number check ("stability drops to 3").

**The best event seen:** *"A Weeping Statue: The ancient stone figure in Merrow's capital, once silent,
now sheds tears of black ink that poison the well water."* (unrest on merrow, story A, seed 14, turn
11). **The worst:** *"A Silver Mine: the people of kesh dig up a fortune in gold."* (the same game,
turn 16), which is the story of a windfall, told for an unrest: kesh lost a point of stability while
its ruler read about a fortune.

### What the framework lacked, by the research page's numbers

- **1, one target per decision: hit, and the workaround held.** Six groups as targets of their own,
  with members in the description; 111 and 120 group events, `the_leader` most. No API wanted yet.
- **3, one free-text field: hit hard.** The 4B ignored "a short title, a colon, then..." in 62 of 62
  events under two wordings, and with an example in the rule still left out half the titles. A second text field (a
  title) is now the gap with evidence behind it.
- **4, a guard cannot keep the move and replace the statement: hit.** The story check runs in the
  game when the event lands; 23 and 15 replaced stories are invisible to the framework's telemetry,
  which calls those decisions clean.
- **6, remembering as a list: hit.** `Memory = null` and a "Told so far" line work, but the 4B copies
  titles from that line ("The Iron Hermit's Curse strikes again" nine times in one game), and the
  game's own fallback titles there get copied as the model's. A list of moves, not of words, would
  avoid both.
- **8, fairness and cooldowns written by every game: hit.** `EventRules.WhyNot` is the budget, the
  weak and the leader, four cooldowns and the gift-and-blow streak, all through `IsAvailableFor`, as
  the research page expected. The first live game showed why the streak rule is needed: under a
  three-event cooldown alone the 4B cycled plague, bad harvest, unrest, volunteers for 21 turns.
- **9, no measure of variety: hit.** Built in the demo's scorecard (the events section), as the page
  said to.
- **2 (strength), 5 (the fact ledger) and 7 (sampling per agent): not needed in S1.** Strength is the
  game's; no effect lasts past its upkeep; temperature stayed 0, and the inspiration line alone made
  45% of the map differ by turn 20.
- **New, N11: the same moves-line workaround for a second agent.** The storyteller needs an "Events
  open now" line among its facts for the same reason the rulers need theirs (N1, N8): the paired
  schema holds the grammar, but a local model reads only the prompt.
- **New, N12: a reply that runs away.** One storyteller call per arm (of about 460) took 20 s and came
  back unreadable; nothing caps a reply's length per call.

The screen's replay (`ReplayBuilder`) did not know storyteller events when S1 was built; it does now
([the storyteller on screen](#the-storyteller-on-screen-13-november)). It said: a storyteller game's forts and
armies would not rebuild. That is the screen's to add when it wants one.

### The storyteller on screen (13 November)

The storyteller plays on the screen, in watch live and rule games.

**Choosing it.** New game → **Storyteller**: Off, or one of its five presets. On, the arm is
`default+storyteller+s2+chains-skeleton+preset-<name>`:
- every stage S2 piece, with the title call on (the tech lead's decision, from S2's measurements:
  titles missing went from 47% to 0%);
- planned tales, which turn on what the rulers choose and so read as a story. Free tales ran longer
  but seldom ended.

The arm goes into the recording (`storyArm`), so a saved game replays under the same rules.

**How the screen plays it.** The storyteller and its scribe decide outside the rulers' rounds, so
their log lines have no chain id. `Recording.FromDecisionLog` takes the turn from the storyteller's
stimulus ("Turn 7. What happens?"), and the scribe's title from the line after it. The rulers' answers
to a dilemma are the `choices` round. `ReplayBuilder` then plays them through the game:
- before each turn's upkeep it queues the event the storyteller queued (the same `StoryEvent`);
- then each ruler's recorded choice (`Game.Choose`), an unanswered one taking the default.

The game rebuilds exactly: a test plays ten turns with a stand-in storyteller and compares every
event, choice and tale.

**On the map and in the chronicle:**
- **The chronicler's banner:** parchment, with its quill crest and no face. It stands where the event
  strikes: at the province, or at a country's capital when it strikes the realm. It shows the title,
  the story and who it struck, with what effect.
- **Its chronicle line** carries the quill crest and the title, then who it struck and what each
  chose. The story is the quote. The **Story** filter shows or hides these lines.
- **Clicking either opens the event card:**
  - the title, the story and the province's traits;
  - each country it struck, with its effect, what it chose (and whether you, the model or the
    default did), and what it said;
  - its tale;
  - the story check's verdict;
  - two buttons that open the storyteller's decision and the title call in the inspector. The
    inspector shows the templates offered, the targets left out with their reasons, the guards,
    and the reply.

**The choice card** (rule mode). When a dilemma reaches your country, the turn stops before its upkeep
behind a parchment card:
- the title and the story;
- every option, with what it costs or gives in the game's words and amounts, the default marked.

Nothing closes the card but a choice. Clicking an option selects it, and **Decide** takes it, so
one stray click never chooses. Esc opens the pause menu over the card, and leaving to the main menu
ends the game. The top bar says **Your choice**. The four rulers answer theirs at the same time.

**Tales** (the chronicle's **Tales** button) lists every tale from its first event to its end:
- each stage's turn, title and story, and what was chosen;
- whether the tale is begun, going on, told or left untold, and why.

Clicking a stage opens its event.

**Without a server:** the dev menu's **Storyteller** row.
- **Rule kesh, saga** and **Watch, saga** play with a stand-in storyteller (`ScriptedStory`). It
  picks legal pairs from the paired schema, leaning toward dilemmas and tales, and draws titles from
  a list.
- **Dilemma on you**, **Pretender tale** and **Omen** fire one at the next upkeep.

### Stage S2: dilemmas, events on many, tales, game-aware stories (12 November)

What the lead asked for after reading S1's events: events that ask a ruler to decide, events that strike
everyone, stories over several turns, stories that know what happened, and places with a character
of their own. The design, with the alternatives it chose between, is the research page's ["Stage S2:
design"](../../docs/demos/painted-realm-research.md#stage-s2-design-12-november). Built in the demo, with
no framework change. **Every piece is a modifier on `+storyteller`, off in `default`**, and with every
piece off the storyteller is S1's exactly (a test holds that):

| Modifier | What it adds |
|---|---|
| `+choices` | Six dilemmas (`sellswords`, `petitioners`, `relic`, `muster`, `engineers`, `fever_camps`), two or three options each, every option an effect the game applies |
| `+multi` | A group event reaches the members the fairness rules allow (open when two are) and records who was spared; a group dilemma gives each member its own choice |
| `+chains-free` | Any recent event is a tale the storyteller may continue (an effect on `tale_<n>` strikes its subject again) or close (`end_tale`) |
| `+chains-skeleton` | Four tales the game plans (`begin_pretender`, `begin_deep_vein`, `begin_fever`, `begin_long_winter`), each stage prescribed, the next one chosen by what the ruler chose |
| `+history` | A "Recent happenings" line from the game's reports, and the story check against invented battles |
| `+traits` | A terrain, a landmark and a resource for every province, in its description: terrain and coast from the map's own data (`WorldMap`, rules v2's R0), the landmark and resource from its special place and trade good when the map has them, else drawn from the seed; `rich_vein`, `flood` and `pilgrims` need one. The A/B ran before the merge, on drawn terrain |
| `+title-call` | The statement is the story alone; a second short call (the scribe) gives the title; "told so far" names template and target only |
| `+preset-<name>` | `even_hand`, `underdog`, `cruel_fate`, `quiet_years`, `saga` (`StorytellerSettings.Presets`) |
| `+s2` | Every piece but the chains, on `even_hand` |

**Effects are data.** S1's eight templates keep their code; every new one is `EffectPart`s (gold,
income, soldiers lost or gained, stability, a fort in the event's province) whose amounts the game
sets (`EventEffects.Plan`) and whose applicability it checks (`EventEffects.WhyNot`). Nothing names a
province or a country: dilemmas, skeletons, traits and presets carry over to a generated map.

**The game deals each turn.** In the first smoke games the 4B, offered dilemmas on 40% of turns and
tales on 30%, chose one dilemma in 28 events and began no tale. So each turn is dealt one kind from the
preset's chances (`EventRules.KindOfTurn`): a **tale's** turn (only beginnings and continuations of
tales are open), else a **dilemma's** (only dilemmas), else an **ordinary** one. A tale already open may
go on in any turn. On `even_hand`, 15% of turns are a tale's and 25% a dilemma's when one is open.

**A dilemma, step by step.** The storyteller picks `sellswords` on `valen` and writes the story. The
game plans each option for valen and asks it, between turns, in a round of its own (`Moment.Choice`):
the story and *"Our choice: hire_sellswords, hire them: pay 30 gold, 14 soldiers join our army;
send_sellswords_away, send them away: nothing changes."* among its facts, the options as its only
actions (registered on the seat with `+choices`, open only on that choice call). The answer lands at
the next upkeep with the event: *"Event "The Sellswords": we chose to hire them: pay 30 gold, 14
soldiers join our army."* A scripted seat takes the default, a random seat any option, an unanswered
one the default. A dilemma counts as a gift when every option gains, a blow when every option costs,
otherwise neither, and is open only when every option can apply.

**Tales.** The game holds each chain (`Chain`: subject, events, what was chosen, status). An open one
is a target of category `tale`, described with what happened so far (*"the tale "The Shadowed
Pretender" of valen: turn 17 pretender, valen chose defy_pretender; next: unrest"*). Rules for both
modes: at most two open; one not continued for three turns, or whose subject is out, is abandoned; at
most six events, and only one tale a game may reach four.

**The history check.** A sentence with a verb of capture or battle that names a province nobody has
fought over, or two countries that never fought, has its story replaced (`replaced: invented: ...`).
It cannot check a claim with no name in it, a motive, a count of the dead, or what an earlier story said.

**What the screen can use** (plain C#, `Sim/`): `Game.PendingChoices` (each `EventChoice`: country,
title, story, options with the game's effect line, default) and `Game.Choose(country, option)` for a
person's seat, to be called before the next `BeginTurn`; `EventRecord.Choices`, `ChosenBy`, `ChainId`
and `ChainStage`; `Game.Chains` to follow a tale; `World.Traits` for a province's card;
`StorytellerSettings.Presets` for the menu.

**Run it** as S1's A/B, one Unity run per arm and block (the lock is held for one arm's five games):

```bash
Unity.exe -batchmode -nographics -projectPath Demos/GreyBoxStrategy -executeMethod GreyBoxStrategy.Editor.StrategyTableMenu.RunInterleavedFromCommandLine -strategyTable personas -strategyModel qwen3.5:4b -strategyArms "default+storyteller+s2+chains-free" -strategyArmNames "s2-free" -strategyGames 5 -strategySeed 6 -strategyIndex 5 -strategyBlock 5 -strategyOut <folder> -logFile run.log
```

`-strategyIndex` is the series index of the first seed, so a block seats everyone as the whole series
would. With chains on, an arm's folder also gets `chains.csv`; with choices, `choices.csv`.

### What S2 did: against S1, 20 games an arm (12 November)

`personas` table, five `qwen3.5:4b` seats, RTX 5080 (Docker Ollama 0.40.14), seeds 1 to 20, four arms
interleaved in blocks of five seeds, one Unity run and one lock window per arm-block: **s1** (S1's
storyteller, `default+storyteller`, the control), **s1-title** (`+title-call` alone), **s2-free**
(`+s2+chains-free`) and **s2-skel** (`+s2+chains-skeleton`). Paired by seed with `compare.py`; **beyond**
when the 95% interval excludes 0. The control reproduced S1's published numbers. Method and every
number: [`findings.md`](../../docs/llm-wiki/findings.md), 12 November, "The storyteller's stage S2".

| Per game, mean | s1 | s1-title | s2-free | s2-skel |
|---|---:|---:|---:|---:|
| titles missing | 47% | 0%, **beyond** | 0%, **beyond** | 0%, **beyond** |
| titles distinct within a game | 60% | 96%, **beyond** | 92%, **beyond** | 95%, **beyond** |
| kinds of event a game | 6.65 | 6.95 | 10.55, **beyond** | 11.30, **beyond** |
| dilemmas among the events | – | – | 18% | 29% |
| story-only events | – | – | 13% | 12% |
| events needing a province's trait | – | – | 12% | 9% |
| tales started a game (two events or more) | – | – | 2.20 | 1.10, **beyond** against free |
| tale length | – | – | 3.63 | 2.25, **beyond** against free |
| tales completed / abandoned a game | – | – | 0.40 / 1.15 | 0.20 / 0.65 |
| stories naming a real happening / inventing one | – | – | 5% / 0.2% | 6% / 0.7% |
| gifts among the events | 42% | 42% | 36%, **beyond** | 29%, **beyond** |
| value tracks standing (r) | −0.37 | −0.37 | −0.25, **beyond** | −0.21, **beyond** |
| hit rulers whose statement names the event | 29% | 28% | 22%, **beyond** | 22%, **beyond** |
| event seconds a turn | 0.74 | 1.05, **beyond** | 1.23, **beyond** | 1.27, **beyond** |

**The game itself did not change.** Battles, land changing hands, wars, peace, lead changes,
eliminations and turns are within noise in every arm; only free tales show more games ended by
domination (60% to 80%, just beyond). The rulers' sense and voice are within noise, persona still
explains aggression (η² 0.81, 0.80, 0.85, 0.82, all p < 0.001), and no event was refused at upkeep.
Per persona, a few numbers move in each arm, as twenty numbers by five personas will by chance; the
builder holding more again (61% to 74%) shows only with the title call.

**What it shows:**

- **The title call fixes the titles,** at 0.35 to 0.39 s an event: every event has one, nearly all are
  different within a game, and fewer repeat across games (72% to 38 to 48%).
- **Choices read in character where the options are a real temptation.** A pretender was paid off by
  the builder and the merchant 15 times out of 15, and defied by the warmonger and the proud ruler 11
  times out of 11. On the other dilemmas the model takes the active option almost always (quarantine 62
  of 64, hire the sellswords 33 of 34, call the muster 44 of 48), whatever the persona: those options
  are simply better.
- **Free tales are longer and more numerous; skeleton tales are tighter.** Free: 44 tales, 15 of four
  events or more, motifs that recur across turns (a frozen bride, a bell, a weeping silence), and the
  model hammers one subject with its effects. Skeletons: 22 tales, each turning on the ruler's choice.
  Both leave most tales unfinished: the model seldom tells a due stage or closes a tale (8 of 44 and 4
  of 22 completed).
- **Stories rarely use what happened.** The happenings line is read on almost every call, but only 5 to
  6% of stories name a real capture, war or rival; the history check replaced 4 that invented one.
- **The rubber band loosens:** dilemmas count as neither gift nor blow, so fewer gifts go to the weak.
- **Rulers name the event less** (29% to 22%) in both S2 arms, but not with the title call alone. Not
  explained yet: one guess is that a ruler who answered a dilemma has already spoken about it in its
  choice call, and its council statement moves on; the measure counts council statements only.

**One reader's labels** (this session's author, not blind, 60 events drawn from the two S2 arms,
`.scratch/storyteller-s2/labels.md`): 50 stories fit their effect, 5 partly (a wrong size, an invented
consequence), 5 not (a wrong country or event, or replaced); 3 refer to a real happening, none invents
one; of 15 dilemmas, 9 stories set up the choice, 5 restate it flatly, 1 decides it for the ruler. Traits
are used whenever a province is the target. New defects: numbers in words pass the digit check
("stability slipping to four", "fifty men"), and a target id leaked into a story once ("the lords of
tale_8").

### One tale: "The Silent Flock" (free tales, seed 14)

Turn 23, an omen on the north: *"A strange silence falls over the North as the wind dies, and in the
monastery at highmoor, a flock of white birds lands on the river valley, their wings bearing symbols
that no saint has ever taught."* Turn 25, the storyteller continues it with a windfall for valen and
orsk: *"A sudden downpour washes the banks of Highmoor clean, revealing a forgotten silver mine that
glints ominously in the mud."* Turn 26, a bad harvest: *"The Silent Flock's flock starves as the frost
bites highmoor and ashby, leaving the north with nothing to feed its armies."* Turn 28, a dilemma, fever
in the camps of both. Valen, the builder: *"We pay the quarantine to save our soldiers and keep our
stability intact."* Orsk, the opportunist: *"I pay the quarantine to preserve my army before I strike
Highmoor."* Turn 30, the storyteller closes the tale with `end_tale`, following what they chose: *"The
fever's grip finally breaks as the quarantine lifts, leaving the north to breathe once more."*

A skeleton tale for contrast, seed 13, "The Grain Hoard": a pretender appears at orskhaven's crossroads
inn; orsk, the warmonger, defies him (*"Grain feeds the weak; I feed my soldiers with blood and
steel!"*); the people grumble at the rotting grain; the pretender's men rise *"demanding grain that was
hoarded by the old king"*, and orsk crushes them (*"Gold buys silence, but steel buys victory"*).

### Three events worth reading, and the worst

- **"The Ruler's Choice"** (skeletons, seed 13, turn 8, a muster for valen while orsk and kesh take
  its provinces): *"The ruler faces a grim choice: gather the few remaining men to face the encroaching
  armies of Orsk and Kesh, or leave the fields untended and risk starvation while the enemy advances."*
  Valen, the proud ruler: *"War demands steel not seed; we must face Kesh and Orsk with every man we have!"*
- **"The Armless Kesh"** (free tales, seed 12, turn 20, sellswords): *"A fortune-teller in keshtar
  whispers of silver that will not flow from the earth, and sellswords offer their swords to the
  gold-rich but armless kesh."* Kesh, the warmonger: *"Gold feeds the sword; I will buy men to reclaim my
  lost provinces!"*
- **"The Pilgrims' Prayer"** (skeletons, seed 19, turn 21, pilgrims at highmoor, whose landmark is a
  hermit's cave): *"A flock of weary pilgrims stumbled into the hermit's cave at highmoor, seeking solace
  from the war-torn lands, and the walls of valen's capital grew steadier with their prayers."*
- **The worst: "Fever Halts the March"** (skeletons, seed 19, turn 8). The storyteller chose the muster
  dilemma for kesh and told a fever: *"The fever breaks out in the camps of the leader, forcing a
  quarantine that halts the march."* Kesh, offered the men to arms or the harvest, answered the story
  it was told: *"Fever breaks but steel remains, we summon men to arms!"*

The events of all four arms can be read game by game, with every choice and every tale, in the page
`.scratch/storyteller-s2/page/chronicler-events.html` (built by `build.py` from the run's files).

### What the framework lacked in S2, by #266's numbers

- **1, one target per decision:** hit, the workaround held twice more: a tale in progress is a target of
  its own (`tale_3`), and an event on many is a group that leaves out the protected members.
- **2, no closed choice beside action and target:** hit, the main cost. The option lines are the game's,
  because one statement cannot carry a line per option; and every option of every dilemma is an action
  registered on every seat, unavailable except on its own choice call.
- **3, one free-text field:** hit, and the second call fixed it (titles missing 47% to 0%, 0.37 s an event).
- **4, keep the move and replace the statement:** hit again, by the history check, invisible to telemetry.
- **5, the fact ledger:** hit: tale state, the happenings line and a choice's facts are game code.
- **6, remembering as a list:** hit: "told so far" by template and target, and each tale's description.
- **8, fairness and cooldowns:** hit harder: a template's own cooldown, two open tales at most, one long
  tale a game, abandonment, dilemmas open only when every option can apply, the turn's kind dealt by chance.
- **9, variety:** the scorecard's events section grew chains, choices and history.
- **New, N13: an optional action the model never takes.** Offered but not dealt, dilemmas and tales were
  almost never chosen (1 dilemma and no tale in 28 events): frequency had to be the game's decision.
- **New, N14: a follow-up call for one decision.** The title is a second agent's decision on the first's
  story, chained by hand; telemetry shows two unrelated decisions.

## What the framework lacks for this game

Checked against #216 on 10 November 2026, from building stage 1, reading the code on `test`, and
stage 2's model runs.

| #216 item | Verdict | Why, for this game |
|---|---|---|
| 1. Examples that carry their situation (F2) | **Confirmed** | The council question is the same every turn; the situation is in the facts. An example of `recruit` cannot show "after a war was declared on us" without them. |
| 2. A decision round (F7) | **Confirmed, with a shape note** | `Game`'s three calls are this round, built by hand. The resolution order depends on what was chosen (peace, then home moves, then wars, then attacks, rotating seats), so the helper should decide everyone first and take the execution order afterwards, from the results, rather than an order fixed before the round. |
| 3. Remember what came of a decision (F1) | **Confirmed** | This turn's facts say what came of last turn's move; nothing remembers two turns back that an attack on fenmark failed. |
| 4. A fact ledger (F4) | **Not needed yet** | Standing facts are recomputed from the state every call, so a plain `IObservationSource` is enough. It would be needed for grudges ("orsk broke a truce with us on turn 6"), which these rules don't have: a truce cannot be broken. |
| 5. Availability per target (F3) | **Already in the code; boxes unticked** | `ITargetAvailability` and `[AvailableFor]` exist (#190 LV #19, DR-020). Four of the six council moves need it, so this game will be its heaviest user. |

New, not in #216:

- **N1. A local model may not learn which targets go with which action.** Ollama never reads the
  schema (findings, 10 October), so what a local model knows of legal pairs comes from the
  prompt. As far as I read `PromptBuilder.ChoicesBlock`, the list `ListActionsAndTargets` writes
  is the flat target enum, every registered target whatever per-target availability says, and a
  paired schema adds no prompt line. Here that is 30 ids, of which a country can use 6 to 15 on a
  given turn. The proposal: with per-target availability or pairing on, list each action's legal
  targets in the prompt (*"attack: fenmark, westmarch"*). It changes the prompt, so it is off
  until A/B'd.
- **N2. `MaxListedTargets` is 12; this game has 30 targets.** A setting, not a gap: stage 2 raises
  it per call. Worth a line in its documentation, though, since the cut is silent and falls on
  whatever comes last in the schema's order.
- **N3. Turning game states into evaluation prompts.** Stage 2 will want the `turn` category
  filled from real game states: a request's facts as observations, its legal moves, and the
  scripted AI's choice as a candidate label to review blind. Nothing exports that today. It
  belongs with the Prompt Set window (#134) or the evaluation harness, and it must keep hard
  rule 4 (no example reused as a prompt).

Found by stage 2, from the runs above:

- **N1, with evidence.** The game-side moves line is the workaround. Without it, the 4B's attack
  share falls from 15% to 2% and its provinces from 1.8 to 0.9 (one game, 30 seeds). It also
  repeats `declare_war` on countries it cannot declare war on, about twice as often.
  *Partly closed by #235, #245 and #246: the framework now knows each move's legal targets from the
  seat's per-target check, lists them in the prompt (`LegalTargets = PerTarget`) and holds the
  grammar to them (`PairTargetsWithActions`). The listing does not replace the line: without the
  line the 4B attacks 6 to 8 points less often
  ([the runs above](#can-the-framework-replace-the-moves-line)). The line stays, with the paired
  schema on by default. What is still missing is gap N8.*
- **N4. The grounding guard contains legal moves when several targets are legal.** It turns a
  decision into `none` when less than 0.8 of the model's probability at the target stayed on the
  chosen target (`TargetGroundingGuard.DefaultThreshold`, internal). With three provinces open to
  attack, a sensible model spreads its probability across them. The guard contained 63 of the
  4B's 508 decisions in the control arm, 47 of them attacks, and 150 of the 2B's 338. A game can only
  switch it off (`DecideOptions.CheckTarget`), not tune it. Two things would help: a threshold the
  call can set, or scoring the probability that stayed on any target the action could legally
  take. *Sized on 10 November with the paired schema on: every containment is then on a legal
  answer, 13% of the 4B's decisions and 39 attacks in 30 games; with the guard off, the attack
  share rises from 22% to 33%.* *Closed by #244 item 1 (11 November): 20 alternatives by default
  in the framework, a per-call threshold, and the demo's `default` arm at 0.6, which took the
  legal containments to 3 on both models ("The grounding guard with many legal targets").*
- **N5. A target on an action that takes none passes through.** `recruit merrowdeep` reaches the
  handler with `TargetId = merrowdeep`, and no guard or the parser flags it. The seat drops it, as
  any handler would, but the telemetry calls it a clean decision. The 4B did it 87 times in 30
  games in the control arm, and 217 times with the headline stimulus. *Closed by #244 item 2: the
  seat sets `ActionDefinition.TakesNoTarget` on recruit, raise_taxes and the two envoy answers, so
  the framework drops the target before the guards and records it in
  `DecisionTelemetry.TargetIgnored`; the seat's own drop stays as a second check.*
- **N6. An empty examples header.** With no examples registered, the few-shot block is the line
  `Examples of correct decisions:` with nothing under it, sent on every call. Unmeasured; a
  framework test would show whether it is intended.
- **N7. Memory replays statements, and a small model copies them over the facts.** `RollingHistory`
  keeps each decision's JSON, statement included. The 4B repeated its own sentences word for word
  for turns on end, once claiming an army of 10 against the facts' 108. This is #216 item 3's other
  half: what to remember of a decision. The move, and what came of it, would help this game more
  than the statement. **Now an option, off** (#244 item 4): `RollingHistory.Remembers` sends the
  move alone (`+memory-move`) or the move and the report's line for it (`+memory-outcome`), which
  `CountryAgent.Learn` attaches once the turn resolves. Note that each remembered stimulus already
  carries the next turn's facts ("Our attack on ashby failed"), so part of the outcome was always
  there. **Measured on `qwen3.5:4b`, and it does not help** (11 November, 20 games an arm, findings): with
  personas and the paired schema, repetition (5%) and invented grievances (1.4%) were already rare
  and did not move, while without the remembered statements the new ones named their target far
  less often (72% to 33% under `+memory-move`, 43% under `+memory-outcome`) and began copying the
  persona's rule. The seat keeps the whole decision. The 2B was not measured.
- **N8. The legal pairs reach a local model as a list, not as moves.** `LegalTargets = PerTarget`
  annotates the system prompt's target list (`- fenmark (for attack): neutral, garrison 13.`). The
  same pairs as a sentence among the facts (`Moves open to us: attack fenmark or westmarch; ...`)
  make the 4B attack 6 to 8 points more often, with or without the list beside it, almost all on
  turns 1 to 4. A framework option to send the legal moves as an observation line, worded by
  action, would let a game drop its own line; its wording would need an A/B on the evaluation set
  as well as here. A smaller sign in the same runs: with the list and a flat schema, the 4B named a
  country it was at war with as `attack`'s target 15 times, which only the paired schema prevents.
  *Built and measured on 11 November (#244 item 5):* `LegalTargets = ObservationLine` writes that
  sentence (`Moves open to you: ...`) where the game's line stood, `+moves-sentence` here. On the
  interim evaluation set it scored below both the plain list and `PerTarget`: −6.4 and −6.7 points
  on the 2B, beyond noise, and −3.6 and −3.3 on the 4B (findings, "The legal moves as an observation
  line"). In stage 2 here, on the 4B only (long game runs are 4B only), 30 games paired by seed,
  `+no-moves-line+moves-sentence` kept the attack share within noise of the game's line (27%
  against 32%), unlike `PerTarget`, but ended with fewer provinces, 0.7 against 2.1, at the edge of
  the band. The game keeps its own line; no default changed.
- **N9. A statement rule reads like a statement already given.** `PromptBuilder` writes
  `StatementRules` as `Your statement: <the rules>`. With a rule that opens with a noun phrase
  (`A trader's voice: profit, price or a deal. One sentence, under 25 words.`), qwen3.5:2b copies
  the whole line into its statement: 373 of the merchant's 582 statements at the persona 2b table,
  76 of the warmonger's, 2 of 2,318 on the 4B ([the table of models](#models-at-one-table)). A
  heading that cannot be read as an answer (`How your statement should read:`) would likely stop
  it. That changes the prompt, so it needs an A/B run. Rewording the personas would only hide it.
  *Measured on 11 November (#244 item 8):* the framework now has `StatementRulesWording`. At the
  persona 2b table, 20 games paired by seed, `+own-words` (`InYourOwnWords`) took the copies from
  15% of statements to none, the merchant's from 65%. `+how-it-reads` took them to 7%, and it also
  cut battles and peace offers. Neither wording cost correct answers on the evaluation sets, but
  there the 2B repeated more few-shot lines (findings, "How a statement rule is introduced"). At
  the persona 4b table, which never copied, `+own-words` left the game's liveliness and the
  persona's hold on the play within noise; statements repeated word for word rose from 5% to 10%
  and named their target more often (72% to 78%). No default changed.
- **N10. A decision round's staleness check comes after the game's own.** `RoundEntry.Stale`
  never fires here, because the game refuses a move that an earlier one broke before it asks the
  seat. Two smaller misfits: a round holds an agent once (a second peace offer needs a second
  round), and two models at one table are two provider queues against one server.
- **N11 and N12, from the storyteller** (12 November): a second agent that needs the moves-line
  workaround, and a reply that runs away. They are in
  [the storyteller's gap list](#what-the-framework-lacked-by-the-research-pages-numbers), beside the
  research page's gaps 1, 3, 4, 6, 8 and 9 that it hit.
- **Item 1 (examples with their situation), still confirmed:** stage 2 registers no examples,
  because with the same question every turn no stimulus-only example could show when to choose an
  action.
- **What did not fit:** nothing the seat needed was missing from the public API. The demo cannot
  reference the framework's test helpers, so `Await.NotCancelled` in the tests copies
  `Fixtures.NotCancelled`. `PairTargetsWithActions` was not run as an arm in stage 2's first runs. It
  was on 10 November, and is now the seat's default: it changes only the grammar a local model
  samples under, not what it reads.

## The screen: watch mode playing back a recorded game

Stage 3's first slice (working title **Painted Realm**; the design is
[`docs/demos/grey-box-strategy-screen.md`](../../docs/demos/grey-box-strategy-screen.md)). Open
`Assets/GreyBoxStrategy/Screen/Watch.unity` and press Play. It plays seed 4 of the v248 persona 4b
table, in which Kesh, the proud ruler, wins by domination on turn 17, with every ruler's own words.
**No model is called**:

- The rules are deterministic per seed, so `ReplayBuilder` rebuilds the very game from the
  recorded decisions (`Screen/Replays/*.json`), and refuses a recording that no longer fits the
  rules rather than show a different game.
- `ArtSource/export_recording.py` makes a recording from any table run's `decisions.jsonl`.

On the screen:

- **The map** is one quad and one shader (`Screen/Map/PaintedMap.shader`). A province id map, an
  edge field and a 32 × 4 state texture drive it, so owners and the capture are numbers changing.
  The id map is baked by `ArtSource/bake_map.py` from the painting and the rule graph, and checked
  against it.
- **The capture** plays as the design page times it: focus, strike, fall, spread from the
  attacker's border with a wet front, settle, release.
- **Around the map**, in UI Toolkit (`Screen/UI/`): the top bar, the realm list, the chronicle
  with filters by country and kind, the speech banner at the capital, and time controls (Space,
  N, +). Right or middle drag pans the map, and the wheel zooms.

`ScreenSceneBuilder` builds the scene, so it is never edited by hand. Run it from the menu
**GreyBoxStrategy → Build the watch-mode scene**, or from the Unity CLI:

```bash
unity command script eval -- --code 'return GreyBoxStrategy.Realm.Editor.ScreenSceneBuilder.Build();'
```

**The decision inspector** (slice 2) opens from any chronicle line or speech banner. It shows
the decision behind it in six numbered sections, from the recorded telemetry only:
- what the model answered (its raw reply);
- the moves the game offered, with the choice marked, and every move not offered with its reason;
- the standing and turn facts it was sent;
- the guards: schema legality, target grounding against its 60% threshold, and the re-check when
  the move landed;
- memory;
- model, tokens, and queue time against model time.

`DecisionCard` builds the inspector's content in plain C#. Esc closes it. The rest of the screen:
- The painting fades into a tile of its own open sea (`Art/sea-tile.jpg`), so the map fills
  any window edge to edge.
- Tooltips work in play, since runtime UI Toolkit shows none of its own.
- Below 1600 px the top bar turns compact.
- The council's thinking indicator shows at each turn's start.

**Panels, map modes and following** (still on the replay):
- **Clicking a province** opens its panel: owner, how it defends (garrison or 70% of the owner's army, forts, capital), income, borders, every country that may attack it with the share of rolls it wins or why it may not, and its history.
- **Clicking a country** (its crest or a realm row) opens its panel: the persona's description and knowledge from `Personas.cs`, the treasury, the facts it last read, the moves it was last offered, and its decisions.
- **Follow** in the country panel turns the top bar into that country's full values (provinces, army, treasury, income, upkeep, stability, wars, truces, places kept for buildings and trade), with the rivals compact on the right. This is the groundwork for rule mode's own bar.
- **Map modes** (keys 1 to 4): Political, the main one, keeps capitals, forts and names; Armies; Forts; Reach, which hatches the followed country's legal attacks with the share of rolls each wins and dims the rest.
- **Diplomacy** (D) is a 5 × 5 matrix of war, truce and peace, with borders and this turn's envoys.
- `RealmQueries` answers these from `GameRules` and the map, and a test checks its reach against every attack the game offered in the recorded game.

**The map** is a 4096 × 2304 master with open sea on every side (`ArtSource/sea-chart-map.png`). It is codex-image's 1672 × 941 painting upscaled, so its detail is still the 1672 px original's. The province maps are baked at 2048 × 1152, and `provinces.json` records the land's bounds, which the camera frames.

**A live game** (slice 3): the Watch object's `LiveGame` component, off by default, plays five
`qwen3.5:4b` persona seats on the default arm, through the game's own `TableRunner`. The
framework's `DecisionLog` writes every decision to `Logs/Live/`. The screen rebuilds the replay
from that log as it grows. It plays a turn once the next council has begun or the game is over,
so no turn is shown before every move of it has landed, and while it waits, the council's
thinking indicator shows.

The server comes from `AGENERELA_OLLAMA_URL` and `AGENERELA_OLLAMA_API_KEY` in Unity's
environment. Take `.scratch/ollama-5080-lock` first.

Checked on 11 November on the RTX 5080: a short game, then a full one, seed 102, which Ilvar won
by domination on turn 24. The screen followed both, guard containments included. There are no
timing claims from these runs. A short check's "stop after N turns" now counts the turns the
model has played; the first run counted played-back turns, so the model went on to 21. The fix
is covered by a review of the code, not yet by a live run.

## Layout

```text
Assets/GreyBoxStrategy/
  Sim/                GreyBoxStrategy.Sim (noEngineReferences: true): no UnityEngine at all
    Rules/            Dice, GameRules, MapDefinition, World, Move, Legality, Facts, Requests, Game,
                      TurnSnapshot, Events (the storyteller's templates, groups and fairness rules),
                      GameSetup (rules v2's settings); the writs turn: Armies (Army, WarRecord,
                      WarScore), WritLegality, WritTurn (Game's second half)
    Map/              WorldMap (the map as data, JSON), Terrain and TerrainTable, ClassicMap; the generator:
                      MapGenerator, MapSettings, MapChecks, MapGeometry, MapNames, SpecialTemplates, MapRandom
    Players/          IPlayer, ScriptedPlayer and Temperament, RandomPlayer, WritScripted (the writs turn)
    Runner/           GameRunner and Lineups, SeriesSummary, GameLog and StrategyRuns, Scorecard, EventLog
  Agents/             GreyBoxStrategy (refs Agenerela, GreyBoxStrategy.Sim): CountryAgent, the model seat;
                      ModelGameRunner (one DecisionRound a council) and ModelReport; ModelSeatOptions,
                      the A/B arms; Personas; TableRunner, the table of models; Storyteller
  Editor/             GreyBoxStrategy.Editor: the menus and the CLI entry points
  Tests/Editor/       GreyBoxStrategy.Tests.Editor
  Screen/             GreyBoxStrategy.Screen: the screen (namespace GreyBoxStrategy.Realm)
    Replay/           GreyBoxStrategy.Replay (noEngineReferences): Recording, ReplayBuilder, Chronicle
    Chart/            GreyBoxStrategy.Chart (noEngineReferences): a generated world drawn as a chart, by code
    Map/              PaintedMap.shader, MapView
    UI/               ScreenUI, CountryLook (crests and icons drawn by code), PaintedRealm.uss/.tss
    Art/              the painting, the faces, the baked province maps
    Replays/          recorded games to play back
    Editor/           the art import settings, ScreenSceneBuilder
    Tests/Editor/     GreyBoxStrategy.Screen.Tests.Editor
ArtSource/            outside Assets: the art's sources and the scripts that bake and export;
                      world_sheet.py draws exported generated worlds on one sheet
```


The project was made from the Universal 2D template; the `SampleScene` it came with is unused. The
screen's scene is `Screen/Watch.unity`.
