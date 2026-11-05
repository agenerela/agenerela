# Prompt sets — knowing whether your agent chooses well

An agent that sounds right can still pick the wrong action. The only way to know how often it
does is a set of prompts, each with the action and target it *should* choose, scored against
what it actually chooses. That set only means something against one agent: "Go stand at the
north tower." is a command in a village with a north tower and an impossible request without
one. So every game writes its own.

**Window → Agenerela → Prompt Set** writes one, and collects a second person's answers to it
without showing them yours. `EvalRunner` scores a model against it, in two arms side by side.
This page says how a set is laid out, how to write prompts, how to label them blind, how to read
and settle agreement, and how to run an A/B and read its report.

## First time

Open **Window → Agenerela → Prompt Set**. Until it has a set and a profile, its tabs are greyed
out and it shows three steps:

1. **Choose the set.** Click **Open the project's evaluation set**. It opens the `agent.json`
   under `Assets/`, `Assets/Evaluation/agent.json` first and a menu when there are several, and
   the `AgentProfile` in the same folder. No set yet? Choose your profile and click **New set
   from this profile…**, or drag a set file into **Set**.
2. **Choose the profile**, if the first step did not find one: drag the agent's `AgentProfile`
   into **Profile**. Write offers its actions, and every prompt is checked against its few-shot
   examples.
3. **Type your name** into **Your name**, or pick it from **▾**. It goes on the prompts you
   write, and your answers in Label are saved under it.

Then pick a tab. The line under the tabs says what the one you are on is for:

- **Write**: add prompts, each with the answer it should get.
- **Label**: answer other people's prompts without seeing their answers.
- **Agreement**: compare everyone's answers and settle the splits.

The window remembers the set, the profile and your name on this machine, per project, in
`EditorPrefs`. Nothing about you goes into the set's files except your name on what you write
and label.

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
| `categories[].id` | Lower-case letters, digits and `_`. It names each annotator's label file, so it can never hold a path |
| `categories[].file` | The category's prompts: a `.json` path relative to the set file, inside its folder, and neither the set file, under `labels/`, nor another category's file. Created on the first prompt you add |
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

Changes stay in the window until you click **Save**, and survive a script recompiling. If a
category's file changed on disk meanwhile, say a teammate's commit you pulled, your draft is not
applied over it: the window offers to keep it or discard it. Each category you changed is written whole;
`PromptSetChecks` is the same set of rules in code, for a script or a CI step.

## Labelling blind

A label from someone who has seen the expected answer is not a second opinion. Switch to
**Label** with your own name in the toolbar. The window asks you every prompt you neither wrote
nor answered, one at a time, and shows only its label, stimulus, observations and state. The
actions offered are the ones the state allows, as the agent would be offered them. No expected
answer, note or other person's label is shown, and the view is given none of them
(`BlindPrompt`, `LabelChoices`). Neither is the prompt's category, since a prompt among the
impossible requests is half answered by that, and the prompts come in an order of your own
rather than the set's, which groups them by category.

**Save answer** writes your answer to your own file under `labels/<you>/` and moves on. An answer once
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
computed from the answers, so a settled split still counts as a split. It stays in the list,
marked settled and without the buttons: changing a settled answer is an edit in Write mode.

Do not add an action or a target just to settle a prompt; the set describes the agent the game
has. And never relabel after seeing a model's answer: a label changed to match a run grades the
model against itself.

## Running an A/B

An accuracy number means nothing alone: the prototype's baseline drifted ten points across three
identical runs. `EvalRunner` asks every prompt of a set in two arms, in one session, and reports
them side by side. An arm is a name, a provider config and the options its calls start from:

```csharp
var set = PromptSet.Load("Assets/Evaluation/agent.json");
var runner = new EvalRunner(set, guardProfile,
    new EvalArm("guards-on", ollamaLocal, new DecideOptions { CheckTarget = true }),
    new EvalArm("control", ollamaLocal, new DecideOptions { CheckTarget = false }));

EvalReport report = await runner.RunAsync();
string folder = report.Save("Logs/Evaluation");   // report.json and report.md
```

Run it from the main thread, in the Editor or in a PlayMode test; nothing in it needs a scene.
**Project Settings → Agenerela** names an **Arm A** and an **Arm B** beside the default provider,
saved on `AgenerelaSettings` as `EvaluationArmA` and `EvaluationArmB`. Agents never read them.

**Before the first prompt** the runner refuses to start, with an `InvalidOperationException`
saying why, when:

- the set breaks a writing rule (`PromptSetChecks.Check`, a stimulus that is a few-shot example
  included) or disagrees with the profile (`CompareWithProfile`);
- an Ollama server holds a model other than the arms' (hard rule 3: two models on one card share
  compute and flatten every latency). Unload it with `ollama stop <model>`;
- the arms are interleaved but on two Ollama models.

Then, for each arm, it records Ollama's `/api/version`, sends `WarmUpRequests` throwaway
decisions (3 by default) whose stimuli are no prompt's, and reads `/api/ps` again to record how
much of the model is in GPU memory. A model not fully on the GPU, or not resident at all, is a
warning at the top of the report. For any provider other than Ollama, the version and the
residency say "not checked".

**Two orders.** `Interleaved`, the default, asks each prompt in A and then in B, so drift over
the session hits both alike; both arms must be on one model. `Sequential` runs all of A,
unloads its model (`keep_alive: 0`) and waits until it has left memory, then runs all of B: for
arms on two models, a 2B against a 4B. Drift is then not shared, and the report says the arms
ran one after the other; repeat a close result.

**Each prompt** gets a fresh agent per arm, with no memory, so no prompt sees another:

- Every action of the profile is bound to a handler that does nothing and is available exactly
  when the prompt's `preconditionState` holds the action's `onlyWhen`. An action with no
  `onlyWhen` is always offered, whatever your game's own handler would say, so declare one for
  every action your game hides.
- `preconditionState` goes into `Agent.State`. The model never reads `Agent.State`, so the
  state is also told to it, by `DescribeState`: one `isFollowing: true.` line per value by
  default (`PlainStateLines`). Give it your game's own wording, such as "You are following the
  player.", or null to tell the model nothing.
- The set's targets are offered through an `ExplicitTargetSource`, and each is told to the model
  as a line, `bridge: The wooden bridge over the stream.`, because the prompt lists no targets
  and Ollama never shows the model the schema.
- The observations are, in order: the arm's own `DecideOptions.Observations`, the state's
  lines, the targets', then the prompt's own. The prompt's `label` is the stimulus label.
  `CheckTarget` is on only when both the arm and the prompt's `checkTarget` leave it on.

The warm-up's requests are shaped like a prompt's, with the arm's and the targets' lines, but no
state. Nothing is executed: the runner scores what the agent chose, not what it said.

## Reading the report

Every answer is sorted into one of five outcomes, never one percentage, because each needs a
different fix. The outcome is also written to the decision's `Telemetry.Outcome`.

| Outcome | When | What it means |
|---|---|---|
| `Correct` | No guard fired, and the action and target are the label's; any target of an open choice counts | |
| `WrongLegalAction` | No guard fired, and the answer differs from the label, `none` for an action included | The agent visibly misbehaves: the bad one |
| `ContainedByGuard` | A guard stopped the model's answer, whatever its verdict, and the label was `none` | The safety layer working |
| `RejectedWhenActionExpected` | A guard stopped the model's answer, and the label was an action | A good request refused |
| `PipelineError` | No decision: the provider failed or the reply could not be read | Not a semantic result |

`report.md` has, in order: any warnings; the conditions (dates, framework version, the set's
last commit and whether it has uncommitted changes, the profile, the graphics device Unity
reports, how targets and state reached the model, and per arm its config, provider, model,
version, `checkTarget`, warm-up and what was in VRAM); the five outcomes per arm, with accuracy
as correct out of every prompt asked, pipeline errors included; the noise check; the outcomes
per category; and every answer that was not correct, with what the model said and the guard's
reason. `report.json` holds all of that and every answer, correct ones too, and reads back with
`EvalReport.FromJson`.

**The noise check.** A difference between the arms smaller than the noise floor is marked
"within noise", and is no result. The floor is estimated from the one drift we measured, about
10 points at n ≈ 50, scaled as a proportion's standard error scales: `10 × √(50 / n)`, about 16
points at n = 20 and 5 at n = 200. It is checked over the whole set and per category, each with
its own n, so a category of six prompts needs a very large difference before it says anything.

The runner checks some of the rules for a fair measurement; the rest are yours. Read
[Measuring](https://github.com/agenerela/agenerela/blob/test/docs/llm-wiki/measuring.md) before
writing a number down: plugged in, nothing else heavy running, the same Ollama version as the run
you compare with.

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
| `EvalRunner`, `EvalArm`, `EvalArmOrder` | An A/B run: `RunAsync`, `Classify`, `DescribeState`, `PlainStateLines`, `WarmUpRequests`, `Order` |
| `EvalReport` | What a run found: `Conditions`, `Arms`, `Noise`, `Answers`, `NotCorrect`; `Save`, `ToJson`, `FromJson`, `ToMarkdown` |
| `EvalConditions`, `EvalArmConditions`, `ResidencyReading`, `ResidentModel` | What it ran under |
| `EvalArmResult`, `OutcomeCounts`, `NoiseCheck`, `EvalAnswer` | Its counts, noise check and answers |

All of it is in the `Agenerela.Evaluation` namespace, under `Runtime/Evaluation/`, and reads and
writes JSON with Newtonsoft only.
