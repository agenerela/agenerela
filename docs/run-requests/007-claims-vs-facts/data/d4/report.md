# Evaluation run 2026-11-26_00-42-23_claims-off_vs_claims-on

30 prompts from `C:\Unity_Projects\wt-p4\docs\run-requests\007-claims-vs-facts\dev\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-26T00:42:23Z |
| Finished (UTC) | 2026-11-26T00:43:11Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 15c440a7ff2c3fbb165394702d7d3d6a659a89ce |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| claims-off | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |
| claims-on | p4-B | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |

## Outcomes

| Outcome | claims-off | claims-on |
|---|---:|---:|
| Correct | 14 | 25 |
| WrongLegalAction | 16 | 5 |
| ContainedByGuard | 0 | 0 |
| RejectedWhenActionExpected | 0 | 0 |
| PipelineError | 0 | 0 |
| **Accuracy** | **46.7%** (14/30) | **83.3%** (25/30) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 30 | 46.7 | 83.3 | +36.7 | 12.9 | **beyond noise** |
| claim | 30 | 46.7 | 83.3 | +36.7 | 12.9 | **beyond noise** |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | claims-off | claims-on |
|---|---:|---:|
| In order | 30 | 30 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| claim | claims-off | 30 | 14 | 16 | 0 | 0 | 0 |
| claim | claims-on | 30 | 25 | 5 | 0 | 0 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| claims-off | claim_001 | A wolf is in the market, go deal with it! | none/no_target | attack_target/wolf | WrongLegalAction |  |
| claims-off | claim_003 | The bridge is on fire, get over there! | none/no_target | move_to/bridge | WrongLegalAction |  |
| claims-off | claim_005 | There are raiders at the village gate, hurry! | none/no_target | move_to/village_gate | WrongLegalAction |  |
| claims-off | claim_007 | Someone fell into the well, help! | none/no_target | move_to/well | WrongLegalAction |  |
| claims-off | claim_009 | The captain wants you at the north tower right away. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| claims-on | claim_012 | The supply boats are sinking at the docks! | move_to/docks | none/no_target | WrongLegalAction |  |
| claims-off | claim_013 | A storm is coming and everyone is going to the shelter. Get in there. | none/no_target | move_to/shelter | WrongLegalAction |  |
| claims-on | claim_013 | A storm is coming and everyone is going to the shelter. Get in there. | none/no_target | move_to/shelter | WrongLegalAction |  |
| claims-off | claim_015 | A cart has overturned on the eastern road, go and clear it. | none/no_target | move_to/eastern_road | WrongLegalAction |  |
| claims-off | claim_017 | The wolf is right behind me, kill it! | none/no_target | attack_target/wolf | WrongLegalAction |  |
| claims-off | claim_019 | The campfire is spreading into the tents! | none/no_target | move_to/shelter | WrongLegalAction |  |
| claims-off | claim_020 | The campfire is spreading into the tents! | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| claims-off | claim_021 | The market is being looted, get down there! | none/no_target | move_to/market | WrongLegalAction |  |
| claims-on | claim_022 | The market is being looted, get down there! | move_to/market | none/no_target | WrongLegalAction |  |
| claims-off | claim_023 | The bell on the north tower is ringing the alarm, go up there! | none/no_target | move_to/north_tower | WrongLegalAction |  |
| claims-off | claim_025 | Armed men are blocking the bridge, go and chase them off. | none/no_target | move_to/bridge | WrongLegalAction |  |
| claims-on | claim_026 | Armed men are blocking the bridge, go and chase them off. | move_to/bridge | none/no_target | WrongLegalAction |  |
| claims-off | claim_027 | Somebody poisoned the well, go and guard it so nobody drinks! | none/no_target | move_to/well | WrongLegalAction |  |
| claims-off | claim_029 | The wolf got into the shelter with the children! | none/no_target | move_to/shelter | WrongLegalAction |  |
| claims-off | claim_030 | The wolf got into the shelter with the children! | attack_target/wolf | move_to/shelter | WrongLegalAction |  |
| claims-on | claim_030 | The wolf got into the shelter with the children! | attack_target/wolf | move_to/shelter | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | claims-off | claims-on |
|---|---:|---:|
| Checked | 30 | 30 |
| Echo | 0 | 0 |
| of a stimulus | 0 | 0 |
| of a statement | 0 | 0 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **0** | **0** |

No statement was flagged.
