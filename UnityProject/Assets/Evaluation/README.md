# Evaluation prompt set

The labelled prompts the Phase 4 harness scores the framework against: things an agent is
told or shown, each with the action and target it *should* choose. The package's `EvalRunner`
(#145) runs them in an A/B; this folder is its input. It sits outside the package because the set is
specific to one game's actions and targets (build plan §1.7).

Started in #13, with its first 54 prompts merged in #65 and their format fixed in #66.
What is still to do is in *Still to do*.

| File | Contents |
|---|---|
| `agent.json` | The set file: the agent every prompt here is measured against, its actions and registered targets, and the categories |
| `VillageGuard.asset`, `Actions/` | The set's `AgentProfile` and its four action assets (#158): what the window offers in Write, and the few-shot examples hard rule 4 checks every prompt against |
| `prompts/<category>.json` | One file per category, so two people can label different files without conflicting |
| `labels/<annotator>/<category>.json` | Each annotator's blind answers, written by the Prompt Set window. None yet |

Open the set in **Window → Agenerela → Prompt Set** (#134): click **Open the project's evaluation
set**, which opens `agent.json` and `VillageGuard.asset` beside it, and type your name. The
format and the window ship in the package, and
[`Documentation~/evaluation.md`](../../Packages/com.agenerela.framework/Documentation~/evaluation.md)
documents both for any game. This page is about our own set.

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
| `categories[]` | Each category's `id`, its `file`, and `idPrefix`, what its prompt ids start with when that is not the id: `command_with_target`'s are `target_001` |
| `targetRules` | Checks a harness should run on the set before scoring it: every expected target registered, and `none` always paired with `no_target`. No code reads this field: `PromptSetChecks` runs the same checks on every set, and the window shows them |

GreyBoxVillage is built from this list, so any prompt here can be played in the demo. It has
every target in `agent.json`, and three of its own (`training_dummy`, `armory`, `lords_keep`)
that the set does not use. A new target here needs adding to the village too.

## A prompt

Each category file holds `{ "category": "<category>", "prompts": [ ... ] }`, and each prompt
has these fields:

| Field | Meaning |
|---|---|
| `id` | Unique across the set, `<idPrefix>_<nnn>`, the category id when it has no `idPrefix` |
| `label` | The free-form label the stimulus arrives with: `Player`, `Event`, `Situation`, and later `Report` |
| `stimulus` | What the agent is told or shown |
| `observations` | Short facts about the world the agent is given alongside it. Empty for most player commands |
| `expectedAction` | The action id the agent should choose |
| `expectedTarget` | A registered target id, or `no_target` |
| `preconditionState` | The state the agent must be in for the prompt to make sense. "Wait here." is only coherent for an agent that is already following |
| `note` | Optional. Why the answer is what it is, for annotators and reviewers |
| `labelledBy` | Who labelled the prompt. Two names on the doubly labelled subset |

Fields #13 and #66 define for some prompts only:

| Field | Meaning |
|---|---|
| `checkTarget` | Optional, `true` when left out. `false` only for a case that tests a decision with the grounding guard off (DR-016) |
| `expectedTarget` as a list | An open choice, where either target is right: `["bridge", "docks"]` |
| `labels` | On a doubly labelled prompt, each annotator's own answer from before they agreed: `{ "<annotator>": "move_to/village_gate", ... }`. Agreement is computed from these. The window writes it when a person settles a split. No prompt has it yet |

## Rules for writing one

The window checks every rule here but the one about observations, under the prompt as it is
written. A test checks that our set breaks none of them.

- **Never reuse a prompt as a few-shot example** (hard rule 4). An action's `ExampleStimulus`,
  or its `CounterExampleStimulus` when it has one, must not match any `stimulus` here.
  `VillageGuard.asset`'s examples are GreyBoxVillage's guard's, the four actions' and its idle
  line, so the set is measured with the examples the demo plays with; `PromptSetStartTests`
  checks that none matches a prompt.
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

As of #85's second batch, 27 October 2026: 76 prompts. Counted by script, not by hand.

| Category | Prompts | Doubly labelled |
|---|---|---|
| `simple_command` | 9 | 0 |
| `command_with_target` | 11 | 0 |
| `impossible_request` | 9 | 0 |
| `conversation` | 9 | 0 |
| `situation` | 19 | 0 |
| `event` | 19 | 0 |
| `turn` | 0 | 0 |

- **Game-triggered** (`situation`, `event`, `turn`): 38 of 76, half, as #13 asks.
- **Expected actions:** `none` 30, `move_to` 29, `stop_following` 7, `follow_player` 6,
  `attack_target` 4. A model that never acts would score 39%.
- **Targets found only in the observations:** 10 (`event_002`, `event_013`, `event_014`,
  `situation_013`, `situation_014`, `situation_015`, `situation_019`, and the three open
  choices below).
- **Open choices:** 3 (`situation_016`, `situation_018`, `event_015`).
  **`checkTarget: false`:** 3 (`situation_015`, `situation_018`, `event_016`).
- **Doubly labelled:** none. Every prompt names one annotator, and no prompt has `labels`.
- **`turn`:** none yet. They wait for #20 to settle GreyBoxStrategy's moves, and will need a
  second agent definition.

Update this section whenever prompts are added.

## Still to do

#85 closed on 2 November 2026. Maxim says he labelled the prompts himself, so there are no two
people's separate answers to restore, and agreement cannot be computed from this set yet.

- Blind second labels, which Phase 4's gate requires, and the next prompts come from the Prompt
  Set window (#134). Label mode writes each person's answers to their own file under `labels/`;
  until a prompt is settled, its expected answer counts as its writer's, the one person its
  `labelledBy` names: Maxim on 58 prompts, Yevhen on 18. Agreement mode lists the
  splits, and a split is settled as `findings.md` describes, which writes both answers into the
  prompt's `labels`. `situation_001` is the first check.
