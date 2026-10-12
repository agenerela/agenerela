# Evaluation prompt set

The labelled prompts the Phase 4 harness will score the framework against: things an agent
is told or shown, each with the action and target it *should* choose. The harness itself is
not written yet; this folder is its input. It sits outside the package because the set is
specific to one game's actions and targets (build plan §1.7).

Started in #13, with its first 54 prompts merged in #65 and their format fixed in #66.
What is still to do is #85.

| File | Contents |
|---|---|
| `agent.json` | The agent every prompt here is measured against: its actions and registered targets |
| `prompts/<category>.json` | One file per category, so two people can label different files without conflicting |

## The agent: `agent.json`

A prompt only means something against a particular agent. "Go stand at the north tower." is
a command in a village with a north tower, and an impossible request without one. Every
prompt in `prompts/` assumes the agent in `agent.json`, the GreyBoxVillage guard extended
with the targets the prompts name.

| Field | Meaning |
|---|---|
| `agent` | Display name |
| `actions[].id` | An action the agent can take. `none` is the idle action the schema adds to every agent |
| `actions[].needsTarget` | `true` if the action takes a registered target, `false` if it always takes `no_target` |
| `actions[].onlyWhen` | Optional. The state in which the action is available, in the same shape as a prompt's `preconditionState`. `stop_following` needs `{ "isFollowing": true }` |
| `targets[].id` | A registered target id, the only values `expectedTarget` may hold besides `no_target` |
| `targets[].description` | One line saying what the target is. It may reach the prompt, so it describes the world, not the test |
| `targetRules` | Checks a harness should run on the set before scoring it: every expected target registered, and `none` always paired with `no_target` |

The demo scene has a target the set does not use, and calls one target by another name.
GreyBoxVillage registers `tower` and `training_dummy`, and this set has `north_tower` and no
`training_dummy`. One side needs renaming before results can be checked in the demo.

## A prompt

Each category file holds `{ "category": "<category>", "prompts": [ ... ] }`, and each prompt
has these fields:

| Field | Meaning |
|---|---|
| `id` | Unique across the set, `<category>_<nnn>` |
| `label` | The free-form label the stimulus arrives with: `Player`, `Event`, `Situation`, and later `Report` |
| `stimulus` | What the agent is told or shown |
| `observations` | Short facts about the world the agent is given alongside it. Empty for most player commands |
| `expectedAction` | The action id the agent should choose |
| `expectedTarget` | A registered target id, or `no_target` |
| `preconditionState` | The state the agent must be in for the prompt to make sense. "Wait here." is only coherent for an agent that is already following |
| `note` | Optional. Why the answer is what it is, for annotators and reviewers |
| `labelledBy` | Who labelled the prompt. Two names on the doubly labelled subset |

Fields #13 and #66 define that no prompt uses yet:

| Field | Meaning |
|---|---|
| `checkTarget` | Optional, `true` when left out. `false` only for a case that tests a decision with the grounding guard off (DR-016) |
| `expectedTarget` as a list | An open choice, where either target is right: `["north_gate", "south_gate"]` |
| `labels` | On a doubly labelled prompt, each annotator's own answer from before they agreed: `{ "<annotator>": "move_to/village_gate", ... }`. Agreement is computed from these |

## Rules for writing one

- **Never reuse a prompt as a few-shot example** (hard rule 4). An action's `ExampleStimulus`
  must not match any `stimulus` here.
- **Targets are registered ids, not descriptions.** Name a concrete `wolf`, not
  `nearby_enemy`. A new target goes into `agent.json` first.
- **Observations describe the world, not the framework or the test.** "A wolf is at the
  bridge", not "The bridge is registered" or "No alternate command was given".
- **`none` always takes `no_target`.** A request that names a registered target the agent
  cannot use right now, such as `impossible_008`'s locked cave, is still `none` /
  `no_target`.
- **Match the state to the stimulus.** Every `stop_following` prompt starts from
  `{ "isFollowing": true }`.

How two annotators settle a disagreement is in
[`docs/llm-wiki/findings.md`](../../../docs/llm-wiki/findings.md), under *Eval prompt set:
how annotator disagreements get resolved*.

## Category balance

As of #66's first pass, 8 October 2026: 54 prompts.

| Category | Prompts | Doubly labelled |
|---|---|---|
| `simple_command` | 9 | 3 |
| `command_with_target` | 9 | 3 |
| `impossible_request` | 9 | 3 |
| `conversation` | 9 | 3 |
| `situation` | 9 | 3 |
| `event` | 9 | 3 |
| `turn` | 0 | 0 |

- **Game-triggered** (`situation`, `event`, `turn`): 18 of 54, a third. #13 wants at least
  half of the finished set.
- **Expected actions:** `none` 28, `move_to` 15, `stop_following` 5, `follow_player` 4,
  `attack_target` 2. A model that never acts would already score 52%.
- **Targets found only in the observations:** 1 (`event_002`).
- **Open choices:** 0. **`checkTarget: false`:** 0.
- **`turn`:** none yet. They wait for #20 to settle GreyBoxStrategy's moves, and will need a
  second agent definition.

Update this section whenever prompts are added.

## Still to do (#85)

- `labelledBy` still holds the placeholder names `alice` and `bob` on all 54 prompts. Replace
  them with the real annotators, and give the 18 doubly labelled prompts their `labels`.
- The next batch: more game-triggered cases, situations where acting is right, targets found
  only in the observations, and open choices.
