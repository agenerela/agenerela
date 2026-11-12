# GreyBoxStrategy

Five countries on a map of 25 provinces, one move each a turn, until one holds 15. The design,
the rules and why they are what they are: [`docs/demos/grey-box-strategy.md`](../../docs/demos/grey-box-strategy.md).
Issue #20, a sub-issue of #47.

**Stages 1 and 2 of 3 are built.** Stage 1 is the game as a headless simulation, a scripted AI
with two temperaments, a random baseline, and a runner that plays whole games and logs every
decision. Stage 2 puts a framework `Agent` in one seat and measures a model there against the
scripted AI; [its results are below](#stage-2-a-model-plays-a-country). Any seat can now be a
model, a script or random, with personas; [the table of models](#models-at-one-table) scores them.
Stage 3, the map and the screen, has begun: [watch mode plays back a recorded game](#the-screen-watch-mode-playing-back-a-recorded-game).

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
`+how-it-reads` and `+own-words` (`StatementRulesWording`). A model run writes everything a scripted run writes, and adds three things:

- the model's own table in `summary.md`;
- `model.csv`, every model decision with its latency, guard and execution;
- `decisions.jsonl`, the framework's `DecisionLog`, prompts included.

Each game log shows the model's answer and its statement under each of its moves.

**Tests:** 99 EditMode tests in `Assets/GreyBoxStrategy/Tests/Editor/`, about 5 seconds once the
project has imported. Twenty of them drive the model seat through `ScriptedProvider`, and
sixteen in `TableTests` the table, the personas and the scorecard.

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

`Scorecard` computes five sections from the game logs and decisions alone, never from what a
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
  line"). Stage 2 here, the game's line against the framework's, is still owed, on the 4B only.
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
  there the 2B repeated more few-shot lines (findings, "How a statement rule is introduced"). The
  4B table is still owed. No default changed.
- **N10. A decision round's staleness check comes after the game's own.** `RoundEntry.Stale`
  never fires here, because the game refuses a move that an earlier one broke before it asks the
  seat. Two smaller misfits: a round holds an agent once (a second peace offer needs a second
  round), and two models at one table are two provider queues against one server.
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

Slice 3 plays a live game.

## Layout

```text
Assets/GreyBoxStrategy/
  Sim/                GreyBoxStrategy.Sim (noEngineReferences: true): no UnityEngine at all
    Rules/            Dice, GameRules, MapDefinition, World, Move, Legality, Facts, Requests, Game,
                      TurnSnapshot
    Players/          IPlayer, ScriptedPlayer and Temperament, RandomPlayer
    Runner/           GameRunner and Lineups, SeriesSummary, GameLog and StrategyRuns, Scorecard
  Agents/             GreyBoxStrategy (refs Agenerela, GreyBoxStrategy.Sim): CountryAgent, the model seat;
                      ModelGameRunner (one DecisionRound a council) and ModelReport; ModelSeatOptions,
                      the A/B arms; Personas; TableRunner, the table of models
  Editor/             GreyBoxStrategy.Editor: the menus and the CLI entry points
  Tests/Editor/       GreyBoxStrategy.Tests.Editor
  Screen/             GreyBoxStrategy.Screen: the screen (namespace GreyBoxStrategy.Realm)
    Replay/           GreyBoxStrategy.Replay (noEngineReferences): Recording, ReplayBuilder, Chronicle
    Map/              PaintedMap.shader, MapView
    UI/               ScreenUI, CountryLook (crests and icons drawn by code), PaintedRealm.uss/.tss
    Art/              the painting, the faces, the baked province maps
    Replays/          recorded games to play back
    Editor/           the art import settings, ScreenSceneBuilder
    Tests/Editor/     GreyBoxStrategy.Screen.Tests.Editor
ArtSource/            outside Assets: the art's sources and the scripts that bake and export
```


The project was made from the Universal 2D template; the `SampleScene` it came with is unused. The
screen's scene is `Screen/Watch.unity`.
