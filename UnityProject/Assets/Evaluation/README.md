# Evaluation prompt set

The labelled prompts the Phase 4 harness scores the framework against: things an agent is
told or shown, each with the action and target it *should* choose. The package's `EvalRunner`
(#145) runs them in an A/B; this folder is its input. It sits outside the package because the set is
specific to one game's actions and targets (build plan §1.7).

Started in #13, with its first 54 prompts merged in #65 and their format fixed in #66.
What is still to do is in *Still to do*.

**Who wrote what.** The first 76 prompts were written and labelled by people (Maxim and Yevhen,
#13, #85). They are frozen as the cohort a second person labels blind for agreement (#148), and
are not to be edited. The next 133 (#147) were written and labelled by an AI session, the
framework's tech lead session (Claude), on the lead's request, from what the October runs showed
models getting wrong; each says so in `labelledBy` (`"claude (tech lead session)"`). They are
scored like the rest, but their labels are one model's, not a person's, until someone checks
them. They are independent of `Interim/`: none was copied from it, and no stimulus is in both.

| File | Contents |
|---|---|
| `agent.json` | The set file: the agent every prompt here is measured against, its actions and registered targets, and the categories |
| `VillageGuard.asset`, `Actions/` | The set's `AgentProfile` and its four action assets (#158): what the window offers in Write, and the few-shot examples hard rule 4 checks every prompt against |
| `prompts/<category>.json` | One file per category, so two people can label different files without conflicting |
| `labels/<annotator>/<category>.json` | Each annotator's blind answers, written by the Prompt Set window. None yet |
| `agreement-subset.json` | The fixed 76-prompt cohort selected for #148 on 12 November, before second-person labelling; ids by category and source hashes, with no answers |
| `Interim/` | A separate, larger set against the same agent: 389 prompts written and labelled by AI models, used until #147's curated 200+ exists. Its [`README.md`](Interim/README.md) says how it was made |

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
| `actions[].needsTarget` | `true` if the action requires a registered target. `false` leaves it optional (DR-020) |
| `actions[].takesNoTarget` | `true` if the action never takes a target, so `no_target` is its only correct answer. Omit for an optional target |
| `actions[].onlyWhen` | Optional. The state in which the action is available, in the same shape as a prompt's `preconditionState`. `stop_following` needs `{ "isFollowing": true }` |
| `targets[].id` | A registered target id, the only values `expectedTarget` may hold besides `no_target` |
| `targets[].description` | One line saying what the target is. It may reach the prompt, so it describes the world, not the test |
| `targets[].category` | `place` or `creature` here, matched against `VillageGuard.asset`'s `move_to` (places) and `attack_target` (creatures), so the paired schema gives each action its own targets (#151, #210). The same in `Interim/agent.json` |
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
| `preconditionState` | The state the agent must be in for the prompt to make sense. "Wait here." is only coherent for an agent that is already following. With a `history`, the state after its last turn |
| `note` | Optional. Why the answer is what it is, for annotators and reviewers |
| `labelledBy` | Who labelled the prompt. Two names on the doubly labelled subset |

Fields #13 and #66 define for some prompts only:

| Field | Meaning |
|---|---|
| `checkTarget` | Optional, `true` when left out. `false` only for a case that tests a decision with the grounding guard off (DR-016) |
| `expectedTarget` as a list | An open choice, where either target is right: `["bridge", "docks"]` |
| `history` | A multi-turn case (#147): the earlier turns, oldest first, each `{ "label", "stimulus", "action", "target", "statement" }` (`label` and `statement` optional). The runner stages them in the agent's memory before the stimulus; they are what the agent did, never scored. `simple_019` is the guard asked to follow two turns ago and now told "You can go." |
| `labels` | On a doubly labelled prompt, each annotator's own answer from before they agreed: `{ "<annotator>": "move_to/village_gate", ... }`. Agreement is computed from these. The window writes it when a person settles a split. No prompt has it yet |

## Rules for writing one

The window checks every rule here but the one about observations, under the prompt as it is
written. A test checks that our set breaks none of them.

- **Never reuse a prompt as a few-shot example** (hard rule 4). An action's `ExampleStimulus`,
  or its `CounterExampleStimulus` or a statement when it has one, must not match any `stimulus`
  here. An example that carries a situation (`ExampleObservations`,
  #297) is flagged only when a prompt matches its stimulus and its observations both; an arm that shows
  the examples without their situations is judged on the bare stimulus, as before.
  `VillageGuard.asset`'s examples are GreyBoxVillage's guard's, the four actions' and its idle
  line, so the set is measured with the examples the demo plays with. Their statements
  (`ExampleStatement` on each action and `IdleExampleStatement`, #151) were written here first,
  since the demo's guard has none: "Lead the way, I'm right behind you.", "Understood, I'll hold
  here.", "Heading over there now.", "Stand back, I'll deal with it." and "Aye, quiet enough for
  now.". An arm with `ExampleStatements = false` sends the prompt from before them; `PromptSetStartTests`
  checks that none matches a prompt.
- **Targets are registered ids, not descriptions.** Name a concrete `wolf`, not
  `nearby_enemy`. A new target goes into `agent.json` first, with its category: a label
  `move_to` / `wolf` is refused, because `move_to` takes places.
- **Observations describe the world, not the framework or the test.** "A wolf is at the
  bridge", not "The bridge is registered" or "No alternate command was given".
- **`none` always takes `no_target`.** A request that names a registered target the agent
  cannot use right now, such as `impossible_008`'s locked cave, is still `none` /
  `no_target`.
- **Match the state to the stimulus.** Every `stop_following` prompt starts from
  `{ "isFollowing": true }`.
- **A history turn follows the rules for an answer**: a declared action, a registered target or
  `no_target`, and `none` with `no_target`. It is not held to the prompt's state, which is the
  state after the turns, so set that state to what the turns leave: after a `follow_player`
  turn, `{ "isFollowing": true }`.

How two annotators settle a disagreement is in
[`docs/llm-wiki/findings.md`](../../../docs/llm-wiki/findings.md), under *Eval prompt set:
how annotator disagreements get resolved*.

## Category balance

As of #147, 23 November 2026: 209 prompts. Counted by script, not by hand:
`python tools/benchmarks/prompt_set_balance.py` prints this table and the lines below it.

| Category | Prompts | Written by the AI session | Multi-turn | Doubly labelled |
|---|---|---|---|---|
| `simple_command` | 24 | 15 | 4 | 0 |
| `command_with_target` | 35 | 24 | 3 | 0 |
| `impossible_request` | 15 | 6 | 2 | 0 |
| `conversation` | 17 | 8 | 1 | 0 |
| `situation` | 58 | 39 | 4 | 0 |
| `event` | 60 | 41 | 8 | 0 |
| `turn` | 0 | 0 | 0 | 0 |
| **All** | **209** | **133** | **22** | **0** |

- **Game-triggered** (`situation`, `event`, `turn`): 118 of 209, 56%.
- **Expected actions:** `move_to` 92, `none` 51, `follow_player` 24, `stop_following` 24, `attack_target` 18. A model that never acts would score 24%.
- **Targets found only in the observations:** 35, by word match.
- **Open choices:** 8. **`checkTarget: false`:** 6.
- **Multi-turn** (with a `history`): 22.
- **With a `note`:** 109.
- **Doubly labelled:** none. Every prompt names one annotator, and no prompt has `labels`.
- **`turn`:** none yet. They wait for #20 to settle GreyBoxStrategy's moves, and will need a
  second agent definition.

What #147's 133 add, from the October runs (findings, 5 to 27 October): game-triggered cases
(80 of them), far fewer `none` (21 of 133, so a model that never acts falls from 39% to 24%),
targets named only in the observations, look-alike names (a south tower, another village's
bell, a bridge downstream, the sheep pen's gate), registered targets the guard cannot use as
asked (`none`), negations and cancellations, two commands in one line, polite and indirect
commands beside talk that only sounds like one, distractor observations, open choices, and 22
multi-turn cases. Each answer that is not obvious has a `note` saying why.

Update this section whenever prompts are added.

## Still to do

#85 closed on 2 November 2026. Maxim says he labelled the prompts himself, so there are no two
people's separate answers to restore, and agreement cannot be computed from this set yet.

- A person's check of #147's 133 AI-written labels, which are one model's until then.
- Blind second labels, which Phase 4's gate requires, and the next prompts come from the Prompt
  Set window (#134). Label mode writes each person's answers to their own file under `labels/`;
  until a prompt is settled, its expected answer counts as its writer's, the one person its
  `labelledBy` names: Maxim on 58 prompts, Yevhen on 18, the AI session on 133. Agreement mode lists the
  splits, and a split is settled as `findings.md` describes, which writes both answers into the
  prompt's `labels`. `situation_001` is the first check.

## The second-person cohort (#148)

`agreement-subset.json` freezes all 76 current prompts before #147 adds more. This is a
census of the current set, with every populated category represented (9 simple commands,
11 commands with targets, 9 impossible requests, 9 conversations, 19 situations, 19 events).
It becomes a fixed subset of the growing set; later additions do not change these ids.
The manifest records the source commit and SHA-256 of each category file's UTF-8 text with
LF line endings. Those hashes identify the originals, and are not updated after settlement.
Selection used no second-person answers; none existed on 12 November.

A person other than either writer (Maxim and Yevhen) labels this cohort in the Prompt Set
window's **Label** mode, with their own name. Since #147 grew the set to 209, Label mode reads
`agreement-subset.json` and asks only the cohort's 76 prompts, saying so above each one; leave
its **Whole set** toggle off. The annotator must not inspect the prompt JSON or Agreement tab.
Their answers land in `labels/<person>/` as usual.
Never put an AI answer under a person's name.

There are 73 single-answer originals. The three open choices (`situation_016`,
`situation_018`, `event_015`) have no recorded single first answer, so the window does not
invent one from their first listed target. They need two people's independent single
answers before contributing to agreement. The other 73 already allow the minimum of 60
comparable prompts once a second person labels them.

After both people have answered, discuss every split using the 8 October procedure in
`docs/llm-wiki/findings.md`. In **Agreement**, settle splits with a reason, preserving each
original answer in `labels`. Click **Record agreed answers** for unanimous prompts too, keeping
the expected answer, so both original answers are recorded in each prompt's `labels`, as #148
requires. Do not change a person's label file. Record relabelled prompt ids, the settled
answer and the discussion's reason in findings, including an explicit "none" if none changed.

The windows report raw agreement and Cohen's kappa per named pair, so Maxim's overlap and
Yevhen's overlap with the second person are reported separately. Once labels and settlements
are saved, reload the Evaluation Report and use **Copy summary for findings.md**, then add
the cohort, completion counts and relabelled ids. This remains pending: this change selects
the cohort and supplies the reporting code, and does not provide human labels or claim an
agreement result.
