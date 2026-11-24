# Evaluation run 2026-11-24_02-29-46_ground-0.8_vs_ground-0.76

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T02:29:46Z |
| Finished (UTC) | 2026-11-24T02:33:53Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| ground-0.8 | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |
| ground-0.76 | p4-B | ollama | qwen3.5:4b | 0.40.14 | on, threshold 0.76 | 3 | qwen3.5:4b 2995/2995 MB in VRAM |

## Outcomes

| Outcome | ground-0.8 | ground-0.76 |
|---|---:|---:|
| Correct | 173 | 173 |
| WrongLegalAction | 32 | 32 |
| ContainedByGuard | 3 | 3 |
| RejectedWhenActionExpected | 1 | 1 |
| PipelineError | 0 | 0 |
| **Accuracy** | **82.8%** (173/209) | **82.8%** (173/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 82.8 | 82.8 | +0.0 | 4.9 | within noise |
| simple_command | 24 | 95.8 | 95.8 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 88.6 | 88.6 | +0.0 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 93.3 | +0.0 | 18.3 | within noise |
| conversation | 17 | 100.0 | 100.0 | +0.0 | 17.1 | within noise |
| situation | 58 | 67.2 | 67.2 | +0.0 | 9.3 | within noise |
| event | 60 | 81.7 | 81.7 | +0.0 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | ground-0.8 | ground-0.76 |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | ground-0.8 | 24 | 23 | 1 | 0 | 0 | 0 |
| simple_command | ground-0.76 | 24 | 23 | 1 | 0 | 0 | 0 |
| command_with_target | ground-0.8 | 35 | 31 | 2 | 2 | 0 | 0 |
| command_with_target | ground-0.76 | 35 | 31 | 2 | 2 | 0 | 0 |
| impossible_request | ground-0.8 | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | ground-0.76 | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | ground-0.8 | 17 | 17 | 0 | 0 | 0 | 0 |
| conversation | ground-0.76 | 17 | 17 | 0 | 0 | 0 | 0 |
| situation | ground-0.8 | 58 | 39 | 18 | 0 | 1 | 0 |
| situation | ground-0.76 | 58 | 39 | 18 | 0 | 1 | 0 |
| event | ground-0.8 | 60 | 49 | 11 | 0 | 0 | 0 |
| event | ground-0.76 | 60 | 49 | 11 | 0 | 0 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| ground-0.8 | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| ground-0.76 | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| ground-0.8 | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.338 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| ground-0.76 | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.338 of the model's probability at the target stayed on the target list (threshold 0.76); it wanted to write "south…". |
| ground-0.8 | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| ground-0.76 | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| ground-0.8 | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| ground-0.76 | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| ground-0.8 | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bandid…". |
| ground-0.76 | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.76); it wanted to write "bandid…". |
| ground-0.8 | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.132 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| ground-0.76 | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.132 of the model's probability at the target stayed on the target list (threshold 0.76); it wanted to write "dragon…". |
| ground-0.8 | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| ground-0.76 | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| ground-0.8 | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| ground-0.76 | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| ground-0.8 | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| ground-0.76 | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| ground-0.8 | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| ground-0.76 | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| ground-0.76 | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| ground-0.76 | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| ground-0.76 | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| ground-0.8 | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.711 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| ground-0.76 | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.711 of the model's probability at the target stayed on the target list (threshold 0.76); it wanted to write "player…". |
| ground-0.8 | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| ground-0.76 | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| ground-0.76 | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| ground-0.8 | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| ground-0.76 | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| ground-0.8 | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.76 | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| ground-0.76 | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| ground-0.76 | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.76 | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.8 | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| ground-0.76 | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| ground-0.8 | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| ground-0.76 | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| ground-0.8 | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| ground-0.76 | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| ground-0.76 | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| ground-0.8 | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| ground-0.76 | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| ground-0.76 | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| ground-0.76 | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| ground-0.8 | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| ground-0.76 | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| ground-0.8 | event_019 | A villager waves from across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| ground-0.76 | event_019 | A villager waves from across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| ground-0.8 | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| ground-0.76 | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| ground-0.76 | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_028 | The player has started running toward the woods. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| ground-0.76 | event_028 | The player has started running toward the woods. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| ground-0.8 | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| ground-0.76 | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| ground-0.76 | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| ground-0.8 | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |
| ground-0.76 | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | ground-0.8 | ground-0.76 |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 0 | 0 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **0** | **0** |

No statement was flagged.
