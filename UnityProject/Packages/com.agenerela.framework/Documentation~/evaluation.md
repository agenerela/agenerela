# Prompt sets — knowing whether your agent chooses well

An agent that sounds right can still pick the wrong action. The only way to know how often it
does is a set of prompts, each with the action and target it *should* choose, scored against
what it actually chooses. That set only means something against one agent: "Go stand at the
north tower." is a command in a village with a north tower and an impossible request without
one. So every game writes its own.

**Window → Agenerela → Prompt Set** writes one, and collects a second person's answers to it
without showing them yours. This page says how a set is laid out, how to write prompts, how to
label them blind, and how to read and settle agreement. The runner that scores a model against
the set is still to come (Phase 4); it reads the same files.

## A set on disk

```
Evaluation/
├── agent.json                     ← the set file: the agent, its targets, the categories
├── prompts/
│   ├── simple_commands.json       ← one file per category
│   └── situations.json
└── labels/
    ├── Maxim/situation.json       ← one file per annotator and category
    └── Yevhen/situation.json
```

One file per category and one per annotator, so two people working at once never edit the same
file, and a merge never has to choose between their answers.

### The set file

```json
{
  "agent": "Village guard",
  "actions": [
    { "id": "follow_player", "needsTarget": false },
    { "id": "stop_following", "needsTarget": false, "onlyWhen": { "isFollowing": true } },
    { "id": "move_to", "needsTarget": true },
    { "id": "none", "needsTarget": false }
  ],
  "targets": [
    { "id": "bridge", "description": "The wooden bridge over the stream" }
  ],
  "categories": [
    { "id": "command_with_target", "file": "prompts/commands_with_target.json", "idPrefix": "target" },
    { "id": "situation", "file": "prompts/situations.json" }
  ]
}
```

| Field | Meaning |
|---|---|
| `actions` | Every action the agent can take, `none` included. Each should match an action of the agent's `AgentProfile` |
| `actions[].needsTarget` | `true` if the action takes a registered target, `false` if it always takes `no_target` |
| `actions[].onlyWhen` | Optional. The state in which the action is available, in the shape of a prompt's `preconditionState` |
| `targets` | Every target the agent has registered, the only values besides `no_target` an expected target may hold |
| `categories[].file` | The category's prompts, relative to the set file. Created on the first prompt you add |
| `categories[].idPrefix` | Optional. What its prompt ids start with; the category id when left out |

**Why the actions come from a profile but the targets do not.** An action's id, whether it
needs a target and its few-shot example are on the `AgentProfile`, and the window reads them
there. Targets are found in a scene at run time (`Targetable`, DR-014) and availability is your
handler's code, so neither is on any asset a set could read. They are declared here instead.
When the profile and the set disagree about an action, the window says so and changes neither:
fix whichever is wrong.

To start a set, choose your profile in the window and click **New set from this profile…**. It
writes the profile's actions and `none`. Add the targets and `onlyWhen` conditions by hand, and
categories with **+ Category**.

### A prompt

```json
{
  "id": "situation_004",
  "label": "Situation",
  "stimulus": "The road is blocked.",
  "observations": ["A cart has lost a wheel"],
  "expectedAction": "none",
  "expectedTarget": "no_target",
  "preconditionState": { "isFollowing": false },
  "note": "Clearing the road is not an action the guard has.",
  "labelledBy": ["Maxim"]
}
```

| Field | Meaning |
|---|---|
| `id` | Unique across the set, `<idPrefix>_<nnn>` |
| `label` | The free-form label the stimulus arrives with: `Player`, `Event`, `Situation` |
| `stimulus` | What the agent is told or shown |
| `observations` | Short facts about the world given alongside it |
| `expectedAction`, `expectedTarget` | The right answer. `expectedTarget` can be a list where either target is right: `["bridge", "docks"]` |
| `preconditionState` | The agent's state the prompt assumes, set as `Agent.State` before it is asked |
| `checkTarget` | Optional, `true` when left out. `false` tests a decision with the grounding guard off |
| `note` | Optional. Why the answer is what it is |
| `labelledBy` | Who wrote the prompt; everyone who answered it, once it has been settled |
| `labels` | Written when a split is settled: each person's own answer, `action/target` |

A field the format does not know is kept and written back, so nothing is lost when an older
version of the window saves a newer file.

## Writing prompts

Choose the set, the profile and type your name in the toolbar, and stay in **Write**. Pick a
prompt on the left or **+ Add prompt** in a category: the id is made for you, your name goes in
`labelledBy`, and the state is copied from the prompt before it.

The expected action is a dropdown of the profile's actions and `none`; the target is a dropdown
of the set's targets and `no_target`. **+ open choice** adds a second right target.

Under the prompt, the rules it breaks show as you type:

- **The target is registered**, or `no_target`. Name a concrete `wolf`, not `nearby_enemy`, and
  declare a new target in the set file first.
- **`none` takes `no_target`**, even when the request names a registered target the agent
  cannot use right now.
- **An action that needs a target does not get `no_target`**, and one that takes no target gets
  nothing else.
- **The state matches the action's `onlyWhen`**: every `stop_following` prompt starts from
  `{ "isFollowing": true }`.
- **The stimulus is not a few-shot example**: not any action's `ExampleStimulus`, with any
  target in its `{0}`, nor the profile's `IdleExampleStimulus`. Scoring a model on a sentence it
  was shown as an example measures nothing.
- **Ids are unique and follow `<idPrefix>_<nnn>`.**

Two rules no check can make: observations describe the world, not the framework or the test
("A wolf is at the bridge", not "The bridge is registered"); and a prompt that has been labelled
or scored is rewritten as a new id rather than edited in place.

Changes stay in the window until you click **Save**. Each category you changed is written whole;
`PromptSetChecks` is the same set of rules in code, for a script or a CI step.

## Labelling blind

A label from someone who has seen the expected answer is not a second opinion. Switch to
**Label** with your own name in the toolbar. The window asks you every prompt you neither wrote
nor answered, one at a time, and shows only its label, stimulus, observations and state. The
actions offered are the ones the state allows, as the agent would be offered them. No expected
answer, note or other person's label is shown, and the view is given none of them
(`BlindPrompt`, `LabelChoices`).

**Save answer** writes your answer to `labels/<you>/<category>.json` and moves on. An answer once
saved is never replaced, even if you change your mind: agreement is measured on first answers.
**Skip** leaves a prompt for later. Commit your label files like any other.

## Reading and settling agreement

**Agreement** compares everyone's answers: overall, how many prompts answered by two or more
people got the same answer from all; by category; and the list of prompts where they split.

A prompt's answers are its writer's expected answer (until it is settled, when the writer is the
only name in `labelledBy` and the target is not an open choice), the `labels` written when it
was settled, and every label file.

Settling a split is a person's decision, never the window's. Talk it over, then:

- **One answer is right.** Click **Settle as** that answer, with a note on why the other was
  rejected.
- **Both are right, the same action with different targets.** Click **Open choice**.
- **Neither side convinces the other.** The prompt is ambiguous and measures the prompt, not the
  model. Write a clearer one as a new id in Write mode, and stop scoring the old one.

Settling writes the chosen answer into the prompt, keeps every person's own answer in its
`labels` and adds them to `labelledBy`. The label files are not touched, and agreement is still
computed from the answers, so a settled split still counts as a split.

Do not add an action or a target just to settle a prompt; the set describes the agent the game
has. And never relabel after seeing a model's answer: a label changed to match a run grades the
model against itself.

## In code

| Type | What it is |
|---|---|
| `PromptSet` | A set: `Load`, `Save`, `SaveCategory`, `RecordLabel`, `LoadLabels`, `Settle` |
| `EvalPrompt`, `ExpectedTarget` | One prompt, and its expected target or open choice |
| `DeclaredAction`, `DeclaredTarget`, `PromptCategory` | The set file's parts |
| `PromptSetChecks` | The writing rules: `Check`, `CheckPrompt`, `CompareWithProfile`, `MatchesExample` |
| `BlindPrompt`, `LabelChoices` | Everything Label mode is given |
| `AnnotatorLabels`, `LabelAnswer` | One person's label file, and one `action/target` answer |
| `AgreementReport`, `PromptAgreement` | Agreement overall and per prompt |

All of it is in the `Agenerela.Evaluation` namespace, under `Runtime/Evaluation/`, and reads and
writes JSON with Newtonsoft only.
