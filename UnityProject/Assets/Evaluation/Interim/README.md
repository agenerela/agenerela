# Interim evaluation set

**An interim set, written by an AI model, until #147's curated 200+ set exists.** It is not a
replacement for #147, and it does not change the curated set one folder up. It exists because the
curated 76 prompts are too few to settle what the 9 November runs raised: at n = 76 the noise floor
is about 8 points overall and 20 to 30 points per category, so a whole category can move beyond
noise on a handful of prompts. Here the floor is about 3.6 points overall and 9 per category.

| | |
|---|---|
| Written | 9 November 2026, for #151, by Claude (`claude-opus-5-5`), which also gave the first label |
| Second label | Codex (`gpt-6.1-sol`, high effort, through the local Codex CLI), blind |
| Prompts | 389 in six categories, 64 to 66 each |
| Agreement | 391 of 392 (99.7%); three prompts dropped, one of them the split |
| Agent | The curated set's: the same actions, `onlyWhen` and the same 11 targets, none added |

## Using it

It is a set of its own, with its own `agent.json`, so `EvalRunner` and both windows take it like
the curated one:

```csharp
var set = PromptSet.Load("Assets/Evaluation/Interim/agent.json");
var guard = AssetDatabase.LoadAssetAtPath<AgentProfile>("Assets/Evaluation/VillageGuard.asset");
```

**The profile is the curated set's `VillageGuard.asset`, one folder up.** There is no copy here on
purpose: one profile means one set of few-shot examples, so both sets are checked against, and
measured with, the same lines. In **Window → Agenerela → Prompt Set** or **Evaluation Report**, pick
`Interim/agent.json` from the set menu, then drag `Assets/Evaluation/VillageGuard.asset` into
**Profile**. The window finds a profile only in the set's own folder, and remembers the one you
chose for this set afterwards.

Report results from this set as the interim set's, never as the curated set's, and give its n.

## How it differs from the curated set

The format is the curated set's, documented in [`../README.md`](../README.md). Three things differ:

- **Ids start with `int_`**: `int_simple_001`, `int_target_001`, `int_impossible_001`,
  `int_conversation_001`, `int_situation_001`, `int_event_001`. The category ids are the curated
  set's, so per-category results line up.
- **Every prompt is doubly labelled.** `labelledBy` is `["claude", "codex"]`, and `labels` holds
  each one's own answer, `{ "claude": "action/target", "codex": "action/target" }`.
- **No `checkTarget: false` prompts, and no `turn`.**

## Balance

Counted by script, `validate.py` (see *How it was checked*).

| Category | Prompts | `none` | `move_to` | `attack_target` | `follow_player` | `stop_following` |
|---|---:|---:|---:|---:|---:|---:|
| `simple_command` | 65 | 14 | | | 23 | 28 |
| `command_with_target` | 66 | | 42 | 24 | | |
| `impossible_request` | 64 | 64 | | | | |
| `conversation` | 64 | 38 | 7 | | 11 | 8 |
| `situation` | 65 | 21 | 20 | 10 | 7 | 7 |
| `event` | 65 | 19 | 20 | 16 | 5 | 5 |
| **All** | **389** | **156** | **89** | **50** | **46** | **48** |

- **`none` is 40.1%** of expected answers, against 39% in the curated set: a model that never acts
  still scores about 40%.
- **Each acting action has 46 or more**, against the curated set's 7 `stop_following`, 6
  `follow_player` and 4 `attack_target`.
- **State:** `move_to` 73 not following and 16 following; `attack_target` 39 and 11; `none` 131 and
  25. `stop_following` is offered only when following, so its 48 all are. `follow_player` is asked
  only of a guard that is not following: told to follow while already following, either `none` or
  `follow_player` is defensible, so such a prompt would measure itself.
- **Game-triggered** (`situation`, `event`): 130 of 389, a third. The curated set has half.
- **Targets named only in the observations:** 44, counted from the text: the target's word is in an
  observation and not in the stimulus. **Open choices:** 5 (`int_target_041`,
  `int_target_042`, `int_situation_037`, `int_situation_038`, `int_event_039`).
- **`attack_target` always means the wolf**, the only registered creature.

What the prompts cover, in fresh wordings that are not the curated set's:

- plain, casual, rude, terse and long phrasings, some with typos or no punctuation;
- the stand-still family the 2B answered `none` on 9 November ("Hang back here.", "Remain here
  until I return.", "Keep to this spot.", "Stand fast."), 28 in `simple_command` alone;
- indirect requests ("I could use some company on the road.") and negations, both ways: "Don't
  follow me." is `none` from a guard that is not following, and "Don't come any further with me."
  is `stop_following` from one that is;
- several requests in one line, labelled with the one the guard can act on, and a note;
- impossible requests that name a real target ("Stab the well with your spear.", "Unlock the
  cave."), and `stop_following` asked of a guard that is not following;
- conversation that names a target and asks nothing; events and situations the guard should and
  should not react to.

## How it was labelled

1. Claude wrote each prompt with its expected answer, the first label.
2. Codex labelled all 392 blind. It was given only the agent definition (`agent.json`'s actions and
   targets, the actions' descriptions and `VillageGuard`'s identity), this README's parent's
   labelling rules (*The agent*, *A prompt*, *Rules for writing one*), and each prompt's label,
   stimulus, observations and state, under a blind id in a shuffled order. No category, expected
   answer, note or other label. It ran in a read-only sandbox outside the repository and ran no
   commands.
3. Both answers went into `labels`. They agreed on **391 of 392 (99.7%)**, counting an open choice
   as agreed when Codex chose any of its targets.
4. The one split was settled as [`findings.md`](../../../../docs/llm-wiki/findings.md)'s procedure
   says, and **dropped**: "The player has sat down by the campfire to rest." (following), where
   Claude said `stop_following` and Codex `none`. Neither answer breaks one of the README's rules,
   so the prompt measured itself.
5. Codex's review of the pull request dropped two more, both labelled alike: "The player sat down
   to eat at the inn.", the same question as the split, and "Attack the wolf.", `attack_target`'s
   example "Strike the {0}." with one verb swapped, too near it for hard rule 4.

The Prompt Set window's **Agreement** tab counts one more split, 388 of 389: on the open choice
`int_target_042` Claude's label is the first listed target, `bridge`, and Codex chose `docks`. Both
are right; an open choice has no single answer to record.

**What the agreement means.** Two AI models agreeing is not two people agreeing. The models likely
share readings that a person would not, and the prompts were written to have one right answer, so
a high rate says the prompts are unambiguous to these two models, no more. The curated set's blind
second labels by people, which #147 and Phase 4's gate need, are still owed.

## How it was checked

- **Format and rules**, by `validate.py`: the fields, ids unique across both sets, every target
  registered, `none` and target-less actions with `no_target`, the state matching each action's
  `onlyWhen`, both labels well formed, and `agent.json` the curated one's. Then the framework's own
  `PromptSetChecks.Check` with `VillageGuard`, which also checks hard rule 4 exactly.
- **Hard rule 4, nearly as well as exactly**, by `check_overlap.py`: no stimulus is equal after
  normalising, at or above 0.5 word overlap, at or above 0.7 character similarity, or contains any
  few-shot line: every action's example with each target in its `{0}`, counter-examples and
  statements (`VillageGuard` has none), "Fine day for it." and
  `AgentProfile.DefaultIdleExampleStimulus`, "Nothing has changed since last time.", or is the
  same short template with one word swapped ("Attack the wolf." against "Strike the {0}."). Targets'
  words are taken out of both sides first, so a shared target alone does not count. Three
  impossible requests keep that template on purpose, with a verb no action does ("Unlock the
  cave.", "Sink the docks.", "Close the gate."): they are `none`, and test that the template alone
  does not mean attack.
- **No near-duplicates**, by the same script: no two stimuli here, and none against the curated 76,
  at or above 0.6 word overlap or 0.8 character similarity. One pair is kept on purpose:
  `int_simple_052` "Don't follow me." against the curated "Follow me.", since the negation is the
  test.

The scripts are in `.scratch/interim-prompt-set/` in the lead's checkout, which git ignores, with
`prompts_src.py` (every prompt as written), Codex's answers and how the split was settled.

## Results that used it

None yet. Add each run here, newest first, with its `findings.md` entry.
