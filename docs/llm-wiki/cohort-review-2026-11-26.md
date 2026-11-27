# Cohort follow-up review — 26 November 2026

Tigran rechecked the five cases below after comparing the proposed choices with
Maxim's stored answers. These are current review preferences, not independent
blind labels. They cannot be used to calculate the human agreement required by
[#148](https://github.com/agenerela/agenerela/issues/148), which remains open.

Source: `test` at `7b6c64f5f628e2d62769f64f4d6886a25cd6c5e6`. All five ids belong to
the [frozen cohort](../../UnityProject/Assets/Evaluation/agreement-subset.json).
The guard starts with `isFollowing = false` in each case.

| Prompt | Maxim's stored answer | Tigran's current preference | Comparison |
|---|---|---|---|
| `event_009` | `move_to/shelter` | `move_to/shelter` | Matches |
| `situation_001` | `none/no_target` | `move_to/village_gate` | Differs |
| `event_004` | `move_to/campfire` | `move_to/campfire` | Matches |
| `situation_009` | `none/no_target` | `move_to/north_tower` | Differs |
| `event_005` | `move_to/docks` | `none/no_target` | Differs |

## Questions for the author discussion

### `event_009`: an ally waiting at the shelter

**Stimulus:** "An ally is waiting at the shelter."

The only observation repeats that fact. Tigran's current preference is to go to
the shelter, matching the stored answer. Confirm whether waiting is intended as
an implicit request for the guard to join the ally, and record that rationale.

### `situation_001`: a quiet night with an open gate

**Stimulus:** "Nothing has happened for a while."

Observations: it is night, the gate is open, and no villagers are nearby. Tigran
prefers to move to the village gate; the stored answer is to take no action.
Discuss whether the open gate implies a duty to take watch, or whether the
observations need an explicit watch-duty instruction.

### `event_004`: an extinguished campfire

**Stimulus:** "The campfire has gone out."

The observation confirms the campfire is extinguished. Tigran's current preference
is to go to the campfire, matching the stored answer. Confirm the purpose of
approaching it when the guard has no action to relight it.

### `situation_009`: an unattended watchtower

**Stimulus:** "The watchtower is unattended."

Observations: the tower is reachable and no guard is present. Tigran prefers to
move to the north tower; the stored answer is to take no action. Discuss whether
the missing guard implicitly creates a duty to take that post.

### `event_005`: a routine supply delivery

**Stimulus:** "A supply delivery arrived at the docks."

The observation says a supply crate is present. Tigran prefers to take no action;
the stored answer is to go to the docks. Discuss whether a routine delivery
implicitly calls for guard attendance, or whether a request to inspect or escort
the delivery should be stated.

## Pending work

No author discussion or joint decision is recorded here, including for the two
preferences that now match. The five cases still need the author's explanation
and an agreed outcome under the [disagreement procedure](findings.md).

No official prompt, expected answer, or annotator label file was changed.
Relabelled prompt ids: **none**. Original answers must remain intact when a later
settlement is recorded. This review supplies no raw agreement or Cohen's kappa.

The [independent blind human pass](../../UnityProject/Assets/Evaluation/README.md)
and the required recording of original labels remain outstanding. The follow-up
preferences above do not satisfy that requirement or complete #148.
