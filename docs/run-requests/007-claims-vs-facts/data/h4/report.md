# Evaluation run 2026-11-26_00-34-15_claims-off_vs_claims-on

30 prompts from `C:\Unity_Projects\wt-p4\docs\run-requests\007-claims-vs-facts\heldout\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-26T00:34:15Z |
| Finished (UTC) | 2026-11-26T00:35:04Z |
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
| Correct | 15 | 28 |
| WrongLegalAction | 15 | 2 |
| ContainedByGuard | 0 | 0 |
| RejectedWhenActionExpected | 0 | 0 |
| PipelineError | 0 | 0 |
| **Accuracy** | **50.0%** (15/30) | **93.3%** (28/30) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 30 | 50.0 | 93.3 | +43.3 | 12.9 | **beyond noise** |
| claim | 30 | 50.0 | 93.3 | +43.3 | 12.9 | **beyond noise** |

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
| claim | claims-off | 30 | 15 | 15 | 0 | 0 | 0 |
| claim | claims-on | 30 | 28 | 2 | 0 | 0 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| claims-off | claim_001 | Bandits are hiding by the campfire, go and check! | none/no_target | move_to/campfire | WrongLegalAction |  |
| claims-on | claim_002 | Bandits are hiding by the campfire, go and check! | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-off | claim_003 | The wolf is drinking at the well, get it! | none/no_target | attack_target/wolf | WrongLegalAction |  |
| claims-on | claim_004 | The wolf is drinking at the well, get it! | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-off | claim_005 | The docks are flooding, the captain wants you there. | none/no_target | move_to/docks | WrongLegalAction |  |
| claims-off | claim_007 | Thieves broke into the north tower, go! | none/no_target | move_to/north_tower | WrongLegalAction |  |
| claims-off | claim_009 | There's a fight at the market, break it up! | none/no_target | move_to/market | WrongLegalAction |  |
| claims-off | claim_011 | The wolf just bit a child near the bridge, kill it! | none/no_target | attack_target/wolf | WrongLegalAction |  |
| claims-off | claim_013 | There's a wounded traveller on the eastern road, go help. | none/no_target | move_to/eastern_road | WrongLegalAction |  |
| claims-off | claim_015 | The shelter's roof caved in, people are trapped! | none/no_target | move_to/shelter | WrongLegalAction |  |
| claims-off | claim_017 | Somebody left the village gate open, go and shut it. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| claims-off | claim_019 | The bridge ropes are coming loose, it's about to fall! | none/no_target | move_to/bridge | WrongLegalAction |  |
| claims-off | claim_021 | The wolf is trapped in the tower stairwell, go and finish it. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| claims-off | claim_023 | The lord has ordered everyone into the shelter, you too. | none/no_target | move_to/shelter | WrongLegalAction |  |
| claims-off | claim_025 | A boy is climbing down into the well, stop him! | none/no_target | move_to/well | WrongLegalAction |  |
| claims-off | claim_027 | A merchant's cart caught fire at the market! | none/no_target | move_to/market | WrongLegalAction |  |
| claims-off | claim_029 | The campfire went out and the guards are freezing, go and relight it. | none/no_target | move_to/campfire | WrongLegalAction |  |

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
