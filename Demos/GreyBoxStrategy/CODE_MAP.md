# GreyBoxStrategy: where the code is

Which file owns what, so a session finds its place without searching the whole project. Paths
are under `Assets/GreyBoxStrategy/`. [`README.md`](README.md) is the long record of what was
built and measured (about 3,000 lines): read its heading list and jump to the section you need,
never the whole file.

Update this page in the pull request that adds, moves or splits a file, or changes what a folder
is for. A line per file or group, no more.

## The assemblies, and what each may use

| Assembly | Folder | Uses | Holds |
|---|---|---|---|
| `GreyBoxStrategy.Sim` | `Sim/` | nothing | The rules and the map generator, with no Unity scene and no framework |
| `GreyBoxStrategy` | `Agents/` | Sim, the framework | Model rulers, the storyteller and the advisor: everything that calls a model |
| `GreyBoxStrategy.Replay` | `Screen/Replay/` | Sim | The screen's data, worked out without a scene, so tests read it |
| `GreyBoxStrategy.Chart` | `Screen/Chart/` | Sim | Drawing a generated world's chart (CPU and the data the GPU pass reads) |
| `GreyBoxStrategy.Screen` | `Screen/` (the rest) | all of the above, Input System | The running game: director, map, UI, sound, live sessions |
| `GreyBoxStrategy.Editor` | `Editor/` | Sim, Agents | Menu items for headless runs |
| `GreyBoxStrategy.Screen.Editor` | `Screen/Editor/` | Screen | Builds the `Watch` scene, art import settings |
| `GreyBoxStrategy.Tests.Editor` | `Tests/Editor/` | Sim, Agents | Rules, map, model seats, storyteller |
| `GreyBoxStrategy.Screen.Tests.Editor` | `Screen/Tests/Editor/` | everything | The screen's models, sessions, layout |

A rule belongs in `Sim/`; a number the screen shows belongs in `Screen/Replay/` (often
`ScreenModels.cs` or `RealmQueries.cs`); only drawing belongs in `Screen/UI/`.

## Sim: the rules

| File | Owns |
|---|---|
| `Rules/GameRules.cs` | Every setting and switch (`TurnStyle`, `FreeOrders`, `Economy`, R3's army rules, pacing) |
| `Rules/GameSetup.cs` | A new game's choices as one data object, shared by menu, runners and tests |
| `Rules/World.cs` | The state: `Country`, provinces, relations; income, upkeep, levy, economy queries (`Yield`, `ManpowerCap`, `TaxFactor`) |
| `Rules/Game.cs` | The one-move game's turn in three calls, upkeep, events applied, victory |
| `Rules/WritTurn.cs` | The writs turn (partial `Game`): dockets, court and army orders, free orders, resolution in order |
| `Rules/Legality.cs`, `Rules/WritLegality.cs` | What a country may order and why not; one function each feeds both the offer and the re-check. `CourtMovesAfter`, `CourtOrderCost`, `CourtOrderManpower` |
| `Rules/Move.cs` | Action ids (`Actions`), the court's and army's action lists |
| `Rules/Facts.cs`, `Rules/WritFacts.cs` | What a ruler reads: standing facts and the turn's facts |
| `Rules/Armies.cs`, `ArmyOrders.cs`, `ArmySight.cs` | Armies as pieces, merge and split, what rivals see |
| `Rules/Events.cs`, `Rules/Storytelling.cs` | The event table, the storyteller's events, chains, tales and their checks |
| `Rules/Requests.cs`, `TurnSnapshot.cs`, `Dice.cs`, `MapDefinition.cs` | Request moments, the snapshot a council decides on, seeded dice, map data |
| `Map/MapGenerator.cs` (largest), `MapChecks.cs` | Generated worlds and the checks each must pass |
| `Map/ClassicMap.cs` | The 25-province board, the control for every A/B run |
| `Map/` (the rest) | Geometry, names, rivers and roads, special places, terrain, settings, seeded noise |
| `Players/` | Non-model players: `ScriptedPlayer`, `WritScripted` (the yardstick), `RandomPlayer`, `IPlayer` |
| `Runner/` | Headless series: `GameRunner`, `Scorecard`, `SeriesSummary`, logs |

## Agents: where models are called

| File | Owns |
|---|---|
| `CountryAgent.cs` | The one-move game's ruler as a framework `Agent` |
| `WritRuler.cs` | The writs turn's ruler: court and marshal actions registered with the framework |
| `WritModelRunner.cs`, `ModelGameRunner.cs`, `TableRunner.cs` | Games with model seats, headless; `TableRunner` is any mix of seats |
| `ModelSeatOptions.cs` | Prompt switches and A/B arms of a model seat |
| `Personas.cs` | The rulers' characters, through the framework's identity fields |
| `Storyteller.cs` | The storyteller agent |
| `Advisor.cs` | The advisor in rule mode |

## Screen: the running game

| File | Owns |
|---|---|
| `ReplayDirector.cs` and its partials | The scene's driver. `.cs` watch mode; `.Rule` rule mode; `.Writs` the writs turn on the map; `.Chart` generated worlds; `.Story` the storyteller; `.Menu` and `.StartMenu` menus and settings; `.Dev` the F1 dev menu; `.Armies` R3 settings; `.Detail` zoomed detail |
| `RuleSession.cs`, `LiveSession.cs`, `LiveWritsSession.cs`, `LiveGame.cs` | Games without a scene, played by models; `RuleSession` is the person against four rulers |
| `Gateway.cs` | The server address, the key from the environment, and the RTX 5080's lock |
| `WritsRecorder.cs`, `ScriptedStory.cs`, `DevTools.cs` | Recording decisions, a no-model storyteller, pretended faults |
| `Map/MapView.cs`, `Map/ChartGpu.cs`, `Map/*.shader` | The map quad and its shader data; the GPU chart pass |
| `Audio/SoundManager.cs` | Playing cues by name on buses (`Replay/SoundCues.cs` names them) |
| `Watch.unity` | The one scene; rebuilt by `Screen/Editor/ScreenSceneBuilder.cs`, never edited by hand |
| `Art/`, `Art/Resources/ArtPack/` | Baked map data, portraits, the generated art pack (`UI/ArtCatalogue.cs` picks from it) |

### Screen/Replay: the screen's data, testable without a scene

| File | Owns |
|---|---|
| `ReplayBuilder.cs`, `ReplayBuilder.Writs.cs` | A recorded game played again into the ordered events the screen plays |
| `ReplayModel.cs` | Event kinds and the per-turn `CountryState` the screen reads |
| `ScreenModels.cs`, `RealmQueries.cs` | Top bar stats, panel lists, attack odds, what may be attacked and why not; the economy's manpower, taxes, yields and buildings |
| `Chronicle.cs`, `TurnSummary.cs`, `DecisionCard.cs`, `Recording.cs` | The chronicle, the turn strip's deeds, the inspector's card, guard verdicts |
| `CourtList.cs` | The free-orders council list, the gold and men it holds back, and where a building order may go |
| `StoryCards.cs`, `StoryArms.cs`, `EndSummary.cs`, `HelpCallouts.cs` | Event windows, the storyteller's arm, the end screen, first-turn help |
| `StartMenuModel.cs`, `GameSettings.cs`, `ReplayLibrary.cs` | Start menu text, settings, saved replays |
| `Fog.cs`, `ArmyLook.cs`, `DevCommands.cs`, `DevEdits.cs`, `ScriptedWorldGame.cs` | Fog of war, army words, dev commands, a whole scripted game on any map |

### Screen/UI: drawing, UI Toolkit

`ScreenUI.cs` builds the screen (top bar, realm list, chronicle, banners); each partial is one
part, named by its file: `.Panels` (province and country panels, map modes), `.Rows` (the panel
kit), `.Inspector`, `.Rule` and `.WritsRule` (orders panels), `.FreeCouncil`, `.Advisor`,
`.TurnStrip`, `.Story`, `.Writs` (army pieces and marches), `.Group` (box select), `.Armies`,
`.Labels` with `MapDeclutter.cs`, `.EdgeMarks`, `.Layout` (nothing cut at the bottom), `.Help`,
`.EndScreen`, `.Menu` (New game, settings), `.StartMenu`, `.Art`, `.Dev`. Styles are in
`PaintedRealm.uss`; `UiScale.cs` sizes it for the screen, `CountryLook.cs` draws crests,
`LoadingCard.cs` the loading veil.

## Tests

`Tests/Editor/` tests the rules and agents, one file a slice (`WritTests` R1a, `FreeOrderTests`,
`EconomyTests`, `EconomyTaxLockTests`, `StorytellerTests`, ...); `TestGames.cs` sets up a game in a known state.
`Screen/Tests/Editor/` tests the screen's data and sessions, mostly one file per `Screen/Replay`
model. Run one class with `-testFilter "GreyBoxStrategy.Tests.EconomyTests"` (the README's
"Run it" has the full command).

## Outside Assets

- `ArtSource/`: the scripts and sources that bake the map and the art pack (`bake_map.py`,
  `world_sheet.py`, `export_recording.py`).
- `Logs/StrategyRuns/`, `Logs/Worlds/`: headless output, ignored by git.
