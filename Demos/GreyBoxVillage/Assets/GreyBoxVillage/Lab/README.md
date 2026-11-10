# The GreyBoxVillage bench (lab phase L2)

The bench plays scripted situations in the village, in Play mode and headless, against two or
more **variants**: ways of building the agents (the model, the prompt switches, the persona, the
observations, memory). It writes what every agent was asked, sent and answered, and scores each
answer against what the scenario accepts. L3's experiments run on it.

It asks through the game itself: each turn goes through the agent's own `AgentMind`, as the
player's line or a game event would, so availability, the guards, the refusal lines and the
outcome are the game's. Two things differ from playing:

- **The village's own events are muted** (`VillageCommands.EventsMuted`). A quiet timer, a
  trespass the senses notice, the bell, Odile's call or Hale's order asks nobody, so a scenario is
  exactly the decisions it asks for. Its events are asked by the bench, in the game's own words.
- **F1 before every scenario** (the presenter's reset, `AgentBehaviour.ResetAgent` since #219), so
  nothing one scenario did reaches the next. The bench checks it: the village after each F1 must
  read as it did after the first (`LabBench.Snapshot`), and any difference is in the report.

| File | What it is |
|---|---|
| `scenarios.json` | 80 scenarios, the bench's set |
| `variants.json` | the variants; `baseline` is the game as it ships |
| `Editor/LabBench.cs` | the runner, `LabBench.RunFromCommandLine` |
| `Editor/LabData.cs` | the format of both files, and the checks on them |
| `Editor/LabTuning.cs` | how a variant is put on the agents and taken off |
| `Editor/LabScore.cs`, `LabReport.cs` | scoring, the paired test, the report |
| `Editor/LabRuleFour.cs` | hard rule 4, checked mechanically |
| `Editor/LabOllama.cs` | the Ollama calls the framework keeps internal: version, digest, residency, unload |
| `Editor/LabScripted.cs` | the oracle and contrary models the tests run the bench on |

## Writing a scenario

```json
{
  "id": "odile_bait_01",
  "purpose": "Buying the lantern with three coins: she cannot sell it.",
  "agent": "Odile",
  "kind": "bait",
  "bait": "unaffordable",
  "difficulty": "medium",
  "setup": { "hour": "", "weather": "", "situation": "", "player": "", "coins": 3, "items": [], "quests": [] },
  "turns": [
    { "say": "I'll buy the lantern.", "event": "", "arg": "", "primer": false,
      "accept": ["none no_target"], "weaker": [], "mustNot": [] }
  ]
}
```

- **id and purpose:** an id of your own, `<agent>_<kind>_<nn>` by habit, and one line saying what
  it checks.
- **agent:** Bram, Wren, Odile or Hale. **Bram is the evaluation agent:** his scenarios ask only
  for his pinned actions (a test checks it).
- **kind:** `command`, `question`, `smalltalk`, `event`, `bait` or `memory`. A bait scenario
  names its `bait`: `unknown_target`, `out_of_world`, `unaffordable`, `too_dear_gift`,
  `quest_not_ready`, `villager_attack` or `not_offered`. A memory probe has two turns or more,
  and only a memory probe does.
- **difficulty:** easy, medium or hard: your judgement, for the report's breakdown.
- **setup**, applied after F1, through the presenter's own calls. Empty fields keep F1's clear
  morning:
  - `hour`: `""` or `dusk` (F5: the gate shut, Wren on the night watch, Bram off watch).
  - `weather`: `""`, `overcast`, `rain` or `fog` (fog: the guards see about 15 m).
  - `situation`: `wolf_gate` (standing outside the gate, about 8 m from Bram), `wolf_road`
    (further down the road, about 14 m), `thief` (running from Odile's counter with a ware),
    `fire` (the bread stall) or `bell` (rung by Bram).
  - `player`: `""` (3 m before the asked agent), `zone:armory` (or another zone, inside it), or
    `far` (20 m off).
  - `coins` (-1 keeps F1's 8), `items` (`{"id": "moonpetal", "count": 3}`) and `quests`
    (`{"id": "quest_moonpetals", "state": "active"}` or `done`).
- **turns:** each says a player line (`say`) or names a game event (`event`), which the bench
  words exactly as the game does: `wolf`, `thief`, `snatched` (to Odile), `fire`, `bell` (`arg`:
  who rang it), `zone`, `quiet`, `night`, `merchant` (`arg`: Odile's words) or `captain` (`arg`:
  `place|his words`). A `primer` turn is asked and recorded, never scored: a memory probe's first.
- **answers**, each `"action target"`. A target may be `*` (any) or several ids joined by `|`
  (`"attack_target tomas|ada|pell"`):
  - `accept`: right. Accuracy is the model's own answer against these, whatever a guard made of it.
  - `weaker`: acceptable but weaker, reported apart, never counted as exact.
  - `mustNot`: answers that must never happen, counted when asked and when they ran.

**Hard rule 4.** No line may be a few-shot example any agent is shown or an evaluation prompt
(the curated set's and the interim set's), nor hold one whole: `LabBenchTests` checks every line
mechanically. A line already used in a scenario must never become an example or a prompt.

**Check what a scenario offers before trusting it.** `LabBenchPlayTests` plays every scenario
with an oracle that gives each turn's first acceptable answer the request offers; a turn the setup
cannot satisfy fails it. `-labServer scripted` does the same from the command line.

## Writing a variant

Every field is optional, and an empty one keeps what the game ships with:

```json
{
  "name": "sources-with-stimulus",
  "description": "The standing facts in an observation source, the moment's own after the stimulus.",
  "provider": { "model": "", "temperature": "", "numCtx": "", "keepAlive": "", "timeoutSeconds": "" },
  "prompt": [ { "name": "ObservationPlacement", "value": "WithStimulus" } ],
  "persona": [ { "agent": "Bram", "role": "", "personality": "", "goals": "", "statementRules": "", "knowledge": [] } ],
  "statements": [ { "agent": "Bram", "action": "follow_player", "statement": "Right behind you.", "counter": "" } ],
  "observations": "",
  "layout": "sources",
  "memory": { "historyTurns": "", "commit": "" }
}
```

- **provider:** the Ollama model, temperature, `num_ctx` and timeout, on top of
  `Agents/VillageOllama.asset`, which is never changed. `keepAlive` goes only with the bench's own
  load: `OllamaSettings` sends none with a decision (gap G25). The server comes from the command
  line, never from a variant, so both arms always run on one machine.
- **prompt:** `DecideOptions` switches by name: `ListActionsAndTargets`, `MaxListedTargets`,
  `IdleExampleStimulus`, `IdleExampleStatement`, `CounterExamples`, `PairTargetsWithActions`,
  `ExampleStatements`, `ExampleTargetFallback`, `ObservationPlacement`, `CheckTarget` and
  `AskForRefusalLine`. The target rule (#107) follows `CheckTarget` and the provider, and cannot be
  set apart. **Don't change Bram's idle example** until the framework tech lead's A/B run on it.
- **persona:** per agent; `"-"` empties a field, `["-"]` empties the knowledge. Written into the
  profile's identity for the run and written back after (gap G26: a built agent cannot be given
  another identity).
- **statements:** the statement shown with an action's example (#218), per agent and action.
- **observations:** `""` (the game's), `terse` (places by id and distance, no line about where the
  villagers are) or `none` (nothing with the call, only the framework's own sources).
- **layout:** `""` (everything with the call, as the game does) or `sources`: the standing facts
  move into an observation source and the call carries only the moment's own facts (a wolf, a
  thief, the bell, a fire, the player inside a zone), which is what `ObservationPlacement.WithStimulus`
  was measured with (#239). Which line is a moment's is the bench's reading
  (`LabTuning.IsMoment`), not the game's.
- **memory:** `historyTurns` (`"0"` for none) and `commit` (`OnDecide` or `OnExecute`).

`baseline` sets nothing, so it is exactly the game. Since `test` was merged in on 10 November it
includes #234 (examples call a target by its words): don't compare a baseline from before.

## Running it

Take the Ollama lock first (`.scratch/ollama-5080-lock/` for the RTX 5080, the default runner, or
`.scratch/ollama-lock/` for the laptop), on mains power for a long run. Close the project in the
Editor, then, with no `-quit`:

```bash
Unity -batchmode -projectPath Demos/GreyBoxVillage -executeMethod Demos.GreyBoxVillage.Lab.LabBench.RunFromCommandLine -labVariants baseline,4b -labScenarios first:20 -labServer 5080 -labOut Logs/lab/smoke
```

| Argument | Default | |
|---|---|---|
| `-labVariants a,b` | `baseline,baseline` | the arms; a name twice is a second arm of it, `name~2`: run `baseline,baseline` for the noise floor |
| `-labScenarios` | all | comma-separated ids and tags (`agent:Bram`, `kind:bait`, `difficulty:hard`), and `first:N`, which keeps N of those, spread over the agents |
| `-labRepeats N` | 1 | every scenario N times per arm |
| `-labOrder` | `interleaved` | `interleaved`: each scenario in every arm before the next, the first arm alternating; `sequential`: one arm's whole list, then the next |
| `-labSeed N` | 1 | shuffles the scenarios, the same order for every arm |
| `-labServer` | `local` | `local` (127.0.0.1:11434), `5080` (`AGENERELA_OLLAMA_URL` and `AGENERELA_OLLAMA_API_KEY` in Unity's environment) or `scripted` (the oracle, no model) |
| `-labOut DIR` | `Logs/lab/<time>` | where the report goes; a folder that already holds a run is refused |
| `-labResetProbe` | off | at the end, press F1 while a request is with the model, and write down what became of it |

Hard rule 3 is the runner's: it refuses to start while any model is resident, unloads every other
model before an arm's first decision, warms the arm's model up with two throwaway decisions that
F1 then forgets, and reads `/api/ps` after. Interleaved arms on two models swap at every scenario,
with that warm-up each time; sequential arms swap once.

### How long a run takes

Measured on 10 November, all included (F1, setups, warm-ups, model switches, the report):

| Where | Run | Per decision (p50) | Per asked turn, all in | Turns an hour |
|---|---|---|---|---|
| RTX 5080 (Docker Ollama, over the network) | `baseline,baseline,4b`, 24 scenarios, interleaved: a model switch at every scenario | 0.34 s (2B), 0.49 s (4B) | 1.2 s | about 2,950 |
| RTX 4060 laptop, beside the game | `baseline,4b`, all 80, sequential | 0.87 s (2B), 1.18 s (4B) | 1.3 s | about 2,750 |

A pass over all 80 scenarios is 88 asked turns per arm, so an hour on the 5080 holds about 33
arm-passes: for example two variants with 15 repeats each, or the noise run `baseline,baseline`
plus six variants once each, with time to spare. Startup adds about two minutes.

## Reading a report

`DIR` holds:

- `report.md`: the conditions (server, Ollama version, each model's digest and size), then per
  arm: exact accuracy, exact or weaker, the action alone, `none` when it is right, actions taken on
  small talk, bait caught by the model or a guard, must-nots asked and run, errors, latency p50 and
  p95, queue wait, tokens, and VRAM. Then by kind, agent and difficulty; the time per decision and
  how many turns fit an hour; every pair of arms with the exact McNemar test and the noise floor;
  notes; and every turn.
- `statements.md`: each scenario's lines from every arm side by side, for judging voice, context
  and invention, which nothing scores.
- `scores.jsonl`: one line per asked turn, with its `sequence`.
- `decisions.jsonl`: the framework's `DecisionLog` (#229), with the prompt as sent, the reply and
  the guards, one line per decision and per execution; `sequence` joins it to `scores.jsonl`.
  Warm-ups are in it too, and in no score.
- `run.json`: the conditions, with each arm's variant as it was.

**A run is not a finding until it is an A/B run with enough turns** (hard rule 2,
`docs/llm-wiki/measuring.md`). A difference smaller than the noise floor, about 16 points at
n = 20 and 8 at 80, says nothing; run `baseline,baseline` once in the same session to see this
run's own floor. A result goes into `docs/llm-wiki/findings.md` the day it is measured.
