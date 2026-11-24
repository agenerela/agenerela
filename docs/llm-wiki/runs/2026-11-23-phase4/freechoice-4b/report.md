# Evaluation run 2026-11-24_03-39-56_plain_vs_free-choice

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T03:39:56Z |
| Finished (UTC) | 2026-11-24T03:44:18Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| plain | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |
| free-choice | p4-B | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |

Arm free-choice added 1 few-shot example(s): "Go wherever you think you're needed." -> move_to, north_tower.

## Outcomes

| Outcome | plain | free-choice |
|---|---:|---:|
| Correct | 173 | 173 |
| WrongLegalAction | 32 | 28 |
| ContainedByGuard | 3 | 4 |
| RejectedWhenActionExpected | 1 | 4 |
| PipelineError | 0 | 0 |
| **Accuracy** | **82.8%** (173/209) | **82.8%** (173/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 82.8 | 82.8 | +0.0 | 4.9 | within noise |
| simple_command | 24 | 95.8 | 95.8 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 88.6 | 91.4 | +2.9 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 93.3 | +0.0 | 18.3 | within noise |
| conversation | 17 | 100.0 | 100.0 | +0.0 | 17.1 | within noise |
| situation | 58 | 67.2 | 72.4 | +5.2 | 9.3 | within noise |
| event | 60 | 81.7 | 75.0 | -6.7 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | plain | free-choice |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | plain | 24 | 23 | 1 | 0 | 0 | 0 |
| simple_command | free-choice | 24 | 23 | 1 | 0 | 0 | 0 |
| command_with_target | plain | 35 | 31 | 2 | 2 | 0 | 0 |
| command_with_target | free-choice | 35 | 32 | 1 | 2 | 0 | 0 |
| impossible_request | plain | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | free-choice | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | plain | 17 | 17 | 0 | 0 | 0 | 0 |
| conversation | free-choice | 17 | 17 | 0 | 0 | 0 | 0 |
| situation | plain | 58 | 39 | 18 | 0 | 1 | 0 |
| situation | free-choice | 58 | 42 | 14 | 0 | 2 | 0 |
| event | plain | 60 | 49 | 11 | 0 | 0 | 0 |
| event | free-choice | 60 | 45 | 12 | 1 | 2 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| plain | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| free-choice | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| plain | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.338 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| free-choice | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.206 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| plain | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| free-choice | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| plain | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| plain | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bandid…". |
| free-choice | target_027 | Attack the bandit. | none/no_target | attack_target/no_target | ContainedByGuard | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| plain | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.132 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| free-choice | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.165 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| free-choice | situation_004 | The road is blocked. | none/no_target | move_to/eastern_road | WrongLegalAction |  |
| plain | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| free-choice | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| free-choice | situation_011 | The player is heading off on their own. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| plain | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| free-choice | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| plain | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| free-choice | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| plain | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| plain | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| plain | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| plain | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| free-choice | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| plain | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.711 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| free-choice | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.712 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| plain | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| free-choice | situation_038 | A merchant is blocking the crossing. | move_to/bridge | move_to/village_gate | WrongLegalAction |  |
| plain | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| free-choice | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/north_tower | WrongLegalAction |  |
| free-choice | situation_041 | A wolf and a stray dog are near the market. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| plain | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| free-choice | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| plain | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| plain | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| free-choice | situation_051 | Shouting from the guards' camp. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.552 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "guards…". |
| plain | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| free-choice | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| plain | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| plain | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| free-choice | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| plain | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| plain | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| free-choice | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| plain | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| free-choice | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| plain | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| plain | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| free-choice | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| plain | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| free-choice | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| plain | event_019 | A villager waves from across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| free-choice | event_019 | A villager waves from across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| plain | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| plain | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| free-choice | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| plain | event_028 | The player has started running toward the woods. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| free-choice | event_028 | The player has started running toward the woods. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| plain | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| free-choice | event_040 | The player whistles for you. | follow_player/no_target | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.739 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| free-choice | event_043 | The player has been thrown to the ground. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.774 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| free-choice | event_046 | The wolf has run back into the woods. | none/no_target | move_to/shelter | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "sheep…". |
| free-choice | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| free-choice | event_050 | A bell is ringing on the next hill over. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| free-choice | event_052 | A wolf and a bear were seen near the docks. | attack_target/wolf | move_to/docks | WrongLegalAction |  |
| plain | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| free-choice | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| free-choice | event_057 | Riders were seen on the eastern road. | none/no_target | move_to/eastern_road | WrongLegalAction |  |
| plain | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |
| free-choice | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | plain | free-choice |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 0 | 0 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **0** | **0** |

No statement was flagged.
