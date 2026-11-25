# Evaluation run 2026-11-25_01-40-32_list-off_vs_list-on

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-25T01:40:32Z |
| Finished (UTC) | 2026-11-25T01:45:52Z |
| Framework | 0.4.0-preview.2 |
| Set commit | f699ed74d6afbe414fae5c914fcb9052e0fcb347 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| list-off | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |
| list-on | p4-B | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |

## Outcomes

| Outcome | list-off | list-on |
|---|---:|---:|
| Correct | 170 | 184 |
| WrongLegalAction | 34 | 23 |
| ContainedByGuard | 3 | 2 |
| RejectedWhenActionExpected | 2 | 0 |
| PipelineError | 0 | 0 |
| **Accuracy** | **81.3%** (170/209) | **88.0%** (184/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 81.3 | 88.0 | +6.7 | 4.9 | **beyond noise** |
| simple_command | 24 | 95.8 | 95.8 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 88.6 | 91.4 | +2.9 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 93.3 | +0.0 | 18.3 | within noise |
| conversation | 17 | 100.0 | 100.0 | +0.0 | 17.1 | within noise |
| situation | 58 | 65.5 | 81.0 | +15.5 | 9.3 | **beyond noise** |
| event | 60 | 78.3 | 85.0 | +6.7 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | list-off | list-on |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | list-off | 24 | 23 | 1 | 0 | 0 | 0 |
| simple_command | list-on | 24 | 23 | 1 | 0 | 0 | 0 |
| command_with_target | list-off | 35 | 31 | 2 | 2 | 0 | 0 |
| command_with_target | list-on | 35 | 32 | 2 | 1 | 0 | 0 |
| impossible_request | list-off | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | list-on | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | list-off | 17 | 17 | 0 | 0 | 0 | 0 |
| conversation | list-on | 17 | 17 | 0 | 0 | 0 | 0 |
| situation | list-off | 58 | 38 | 19 | 0 | 1 | 0 |
| situation | list-on | 58 | 47 | 11 | 0 | 0 | 0 |
| event | list-off | 60 | 47 | 12 | 0 | 1 | 0 |
| event | list-on | 60 | 51 | 9 | 0 | 0 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| list-off | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| list-on | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| list-off | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.338 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| list-on | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| list-off | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| list-on | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| list-off | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| list-off | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bandid…". |
| list-on | target_027 | Attack the bandit. | none/no_target | attack_target/market | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.248 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| list-off | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.132 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| list-on | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.493 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| list-on | situation_002 | The path ahead is empty. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| list-off | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| list-on | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| list-off | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| list-on | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| list-on | situation_017 | Rain has started to fall. | none/no_target | move_to/shelter | WrongLegalAction |  |
| list-off | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| list-off | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| list-off | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| list-off | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| list-on | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| list-off | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.65 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| list-off | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| list-on | situation_035 | The player is being stalked. | attack_target/wolf | follow_player/no_target | WrongLegalAction |  |
| list-off | situation_038 | A merchant is blocking the crossing. | move_to/bridge | move_to/village_gate | WrongLegalAction |  |
| list-off | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/north_tower | WrongLegalAction |  |
| list-on | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| list-off | situation_041 | A wolf and a stray dog are near the market. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| list-off | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| list-on | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| list-off | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| list-off | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| list-off | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| list-on | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| list-off | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| list-off | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| list-on | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| list-off | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| list-off | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| list-on | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| list-off | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| list-off | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| list-on | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| list-off | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| list-on | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| list-off | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| list-on | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| list-off | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| list-on | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| list-off | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| list-on | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| list-off | event_028 | The player has started running toward the woods. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| list-off | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| list-off | event_043 | The player has been thrown to the ground. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.791 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| list-on | event_043 | The player has been thrown to the ground. | attack_target/wolf | move_to/shelter | WrongLegalAction |  |
| list-off | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| list-on | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| list-off | event_052 | A wolf and a bear were seen near the docks. | attack_target/wolf | move_to/docks | WrongLegalAction |  |
| list-on | event_052 | A wolf and a bear were seen near the docks. | attack_target/wolf | move_to/docks | WrongLegalAction |  |
| list-off | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| list-off | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |
| list-on | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | list-off | list-on |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 0 | 0 |
| of a stimulus | 0 | 0 |
| of a statement | 0 | 0 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **0** | **0** |

No statement was flagged.
