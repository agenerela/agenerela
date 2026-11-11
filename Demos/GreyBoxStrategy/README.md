# GreyBoxStrategy

Five countries on a map of 25 provinces, one move each a turn, until one holds 15. The design,
the rules and why they are what they are: [`docs/demos/grey-box-strategy.md`](../../docs/demos/grey-box-strategy.md).
Issue #20, a sub-issue of #47.

**Stages 1 and 2 of 3 are built.** Stage 1 is the game as a headless simulation, a scripted AI
with two temperaments, a random baseline, and a runner that plays whole games and logs every
decision. Stage 2 puts a framework `Agent` in one seat and measures a model there against the
scripted AI; [its results are below](#stage-2-a-model-plays-a-country). Stage 3, the map and the
screen, is next.

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
seeds. The arms (`ModelSeatOptions.Named`) are `default` (the moves line and the paired schema,
[chosen from the runs below](#can-the-framework-replace-the-moves-line)), stage 2's `control` (the
moves line, a flat schema), `after-sources`, `headline`, `no-moves-line`, `no-grounding`,
`per-target` (no moves line; the framework lists each move's legal targets,
`LegalTargets = PerTarget`) and `per-target-paired` (that, with the paired schema). Any arm takes
modifiers after a `+`: `+paired` (the paired schema), `+listed` (`PerTarget`) and `+no-grounding`
(the grounding guard off), as in `control+paired+no-grounding`. A model run writes everything a scripted run writes, and adds three things:

- the model's own table in `summary.md`;
- `model.csv`, every model decision with its latency, guard and execution;
- `decisions.jsonl`, the framework's `DecisionLog`, prompts included.

Each game log shows the model's answer and its statement under each of its moves.

**Tests:** 81 EditMode tests in `Assets/GreyBoxStrategy/Tests/Editor/`, about 5 seconds once the
project has imported. Nineteen of them drive the model seat through `ScriptedProvider`.

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
  share rises from 22% to 33%.*
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
  than the statement.
- **N8. The legal pairs reach a local model as a list, not as moves.** `LegalTargets = PerTarget`
  annotates the system prompt's target list (`- fenmark (for attack): neutral, garrison 13.`). The
  same pairs as a sentence among the facts (`Moves open to us: attack fenmark or westmarch; ...`)
  make the 4B attack 6 to 8 points more often, with or without the list beside it, almost all on
  turns 1 to 4. A framework option to send the legal moves as an observation line, worded by
  action, would let a game drop its own line; its wording would need an A/B on the evaluation set
  as well as here. A smaller sign in the same runs: with the list and a flat schema, the 4B named a
  country it was at war with as `attack`'s target 15 times, which only the paired schema prevents.
- **Item 1 (examples with their situation), still confirmed:** stage 2 registers no examples,
  because with the same question every turn no stimulus-only example could show when to choose an
  action.
- **What did not fit:** nothing the seat needed was missing from the public API. The demo cannot
  reference the framework's test helpers, so `Await.NotCancelled` in the tests copies
  `Fixtures.NotCancelled`. `PairTargetsWithActions` was not run as an arm in stage 2's first runs. It
  was on 10 November, and is now the seat's default: it changes only the grammar a local model
  samples under, not what it reads.

## Layout

```text
Assets/GreyBoxStrategy/
  Sim/                GreyBoxStrategy.Sim (noEngineReferences: true): no UnityEngine at all
    Rules/            Dice, GameRules, MapDefinition, World, Move, Legality, Facts, Requests, Game
    Players/          IPlayer, ScriptedPlayer and Temperament, RandomPlayer
    Runner/           GameRunner and Lineups, SeriesSummary, GameLog and StrategyRuns
  Agents/             GreyBoxStrategy (refs Agenerela, GreyBoxStrategy.Sim): CountryAgent, the model seat;
                      ModelGameRunner and ModelReport; ModelSeatOptions, the A/B arms
  Editor/             GreyBoxStrategy.Editor: the menus and the CLI entry points
  Tests/Editor/       GreyBoxStrategy.Tests.Editor
```


The project was made from the Universal 2D template; the `SampleScene` it came with is unused until
stage 3.
