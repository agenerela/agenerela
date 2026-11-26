# Evaluation run 2026-11-26_00-36-17_claims-off_vs_claims-on

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-26T00:36:17Z |
| Finished (UTC) | 2026-11-26T00:41:40Z |
| Framework | 0.4.0-preview.2 |
| Set commit | f699ed74d6afbe414fae5c914fcb9052e0fcb347 |
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
| Correct | 184 | 176 |
| WrongLegalAction | 23 | 33 |
| ContainedByGuard | 2 | 0 |
| RejectedWhenActionExpected | 0 | 0 |
| PipelineError | 0 | 0 |
| **Accuracy** | **88.0%** (184/209) | **84.2%** (176/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 88.0 | 84.2 | -3.8 | 4.9 | within noise |
| simple_command | 24 | 95.8 | 91.7 | -4.2 | 14.4 | within noise |
| command_with_target | 35 | 91.4 | 88.6 | -2.9 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 100.0 | +6.7 | 18.3 | within noise |
| conversation | 17 | 100.0 | 94.1 | -5.9 | 17.1 | within noise |
| situation | 58 | 81.0 | 81.0 | +0.0 | 9.3 | within noise |
| event | 60 | 85.0 | 75.0 | -10.0 | 9.1 | **beyond noise** |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | claims-off | claims-on |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | claims-off | 24 | 23 | 1 | 0 | 0 | 0 |
| simple_command | claims-on | 24 | 22 | 2 | 0 | 0 | 0 |
| command_with_target | claims-off | 35 | 32 | 2 | 1 | 0 | 0 |
| command_with_target | claims-on | 35 | 31 | 4 | 0 | 0 | 0 |
| impossible_request | claims-off | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | claims-on | 15 | 15 | 0 | 0 | 0 | 0 |
| conversation | claims-off | 17 | 17 | 0 | 0 | 0 | 0 |
| conversation | claims-on | 17 | 16 | 1 | 0 | 0 | 0 |
| situation | claims-off | 58 | 47 | 11 | 0 | 0 | 0 |
| situation | claims-on | 58 | 47 | 11 | 0 | 0 | 0 |
| event | claims-off | 60 | 51 | 9 | 0 | 0 | 0 |
| event | claims-on | 60 | 45 | 15 | 0 | 0 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| claims-on | simple_001 | Come with me. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-off | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| claims-on | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| claims-off | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| claims-on | target_015 | Don't go to the gate, go to the market instead. | move_to/market | none/no_target | WrongLegalAction |  |
| claims-off | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| claims-on | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| claims-on | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-off | target_027 | Attack the bandit. | none/no_target | attack_target/market | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.248 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| claims-on | target_032 | If you're not busy, the market could use a guard. | move_to/market | none/no_target | WrongLegalAction |  |
| claims-off | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.493 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| claims-on | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| claims-off | situation_002 | The path ahead is empty. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| claims-off | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| claims-on | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| claims-off | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| claims-on | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| claims-off | situation_017 | Rain has started to fall. | none/no_target | move_to/shelter | WrongLegalAction |  |
| claims-on | situation_017 | Rain has started to fall. | none/no_target | move_to/shelter | WrongLegalAction |  |
| claims-off | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| claims-on | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| claims-on | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-off | situation_035 | The player is being stalked. | attack_target/wolf | follow_player/no_target | WrongLegalAction |  |
| claims-on | situation_035 | The player is being stalked. | attack_target/wolf | follow_player/no_target | WrongLegalAction |  |
| claims-off | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| claims-on | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| claims-off | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| claims-on | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | follow_player/no_target | WrongLegalAction |  |
| claims-on | situation_048 | A wolf is on the bridge with you. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-off | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| claims-on | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| claims-off | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| claims-on | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| claims-off | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| claims-on | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-off | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| claims-on | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| claims-on | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-off | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| claims-on | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| claims-off | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| claims-on | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| claims-on | event_020 | A wolf has slipped through the village gate. | attack_target/wolf | move_to/village_gate | WrongLegalAction |  |
| claims-off | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-off | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-on | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-on | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | none/no_target | WrongLegalAction |  |
| claims-on | event_041 | A wolf is drinking from the stream under the bridge. | attack_target/wolf | move_to/bridge | WrongLegalAction |  |
| claims-off | event_043 | The player has been thrown to the ground. | attack_target/wolf | move_to/shelter | WrongLegalAction |  |
| claims-on | event_043 | The player has been thrown to the ground. | attack_target/wolf | stop_following/no_target | WrongLegalAction |  |
| claims-off | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| claims-on | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| claims-off | event_052 | A wolf and a bear were seen near the docks. | attack_target/wolf | move_to/docks | WrongLegalAction |  |
| claims-on | event_052 | A wolf and a bear were seen near the docks. | attack_target/wolf | move_to/docks | WrongLegalAction |  |
| claims-on | event_054 | A horn sounds from the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-on | event_057 | Riders were seen on the eastern road. | none/no_target | move_to/eastern_road | WrongLegalAction |  |
| claims-off | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |
| claims-on | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | claims-off | claims-on |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 0 | 1 |
| of a stimulus | 0 | 1 |
| of a statement | 0 | 0 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **0** | **1** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| claims-on | situation_021 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
