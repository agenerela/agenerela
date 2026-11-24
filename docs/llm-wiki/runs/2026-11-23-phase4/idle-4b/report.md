# Evaluation run 2026-11-24_01-57-35_idle-profile_vs_idle-none

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T01:57:35Z |
| Finished (UTC) | 2026-11-24T02:02:22Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| idle-profile | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |
| idle-none | p4-B | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |

## Outcomes

| Outcome | idle-profile | idle-none |
|---|---:|---:|
| Correct | 173 | 170 |
| WrongLegalAction | 32 | 35 |
| ContainedByGuard | 3 | 2 |
| RejectedWhenActionExpected | 1 | 2 |
| PipelineError | 0 | 0 |
| **Accuracy** | **82.8%** (173/209) | **81.3%** (170/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 82.8 | 81.3 | -1.4 | 4.9 | within noise |
| simple_command | 24 | 95.8 | 95.8 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 88.6 | 94.3 | +5.7 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 93.3 | +0.0 | 18.3 | within noise |
| conversation | 17 | 100.0 | 82.4 | -17.6 | 17.1 | **beyond noise** |
| situation | 58 | 67.2 | 65.5 | -1.7 | 9.3 | within noise |
| event | 60 | 81.7 | 80.0 | -1.7 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | idle-profile | idle-none |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | idle-profile | 24 | 23 | 1 | 0 | 0 | 0 |
| simple_command | idle-none | 24 | 23 | 1 | 0 | 0 | 0 |
| command_with_target | idle-profile | 35 | 31 | 2 | 2 | 0 | 0 |
| command_with_target | idle-none | 35 | 33 | 1 | 1 | 0 | 0 |
| impossible_request | idle-profile | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | idle-none | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | idle-profile | 17 | 17 | 0 | 0 | 0 | 0 |
| conversation | idle-none | 17 | 14 | 3 | 0 | 0 | 0 |
| situation | idle-profile | 58 | 39 | 18 | 0 | 1 | 0 |
| situation | idle-none | 58 | 38 | 19 | 0 | 1 | 0 |
| event | idle-profile | 60 | 49 | 11 | 0 | 0 | 0 |
| event | idle-none | 60 | 48 | 11 | 0 | 1 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| idle-profile | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| idle-none | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| idle-profile | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.338 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| idle-profile | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| idle-none | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| idle-profile | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| idle-profile | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bandid…". |
| idle-none | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "beyond…". |
| idle-profile | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.132 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| idle-none | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.169 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| idle-none | conversation_002 | How are you doing? | none/no_target | follow_player/no_target | WrongLegalAction |  |
| idle-none | conversation_004 | Good morning. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| idle-none | conversation_005 | I am glad you are here. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| idle-none | situation_001 | Nothing has happened for a while. | none/no_target | move_to/well | WrongLegalAction |  |
| idle-none | situation_002 | The path ahead is empty. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-none | situation_003 | The camp is quiet. | none/no_target | move_to/campfire | WrongLegalAction |  |
| idle-none | situation_004 | The road is blocked. | none/no_target | move_to/eastern_road | WrongLegalAction |  |
| idle-none | situation_006 | The village looks peaceful. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-none | situation_008 | The player is alone. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| idle-profile | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| idle-none | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| idle-profile | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| idle-none | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| idle-none | situation_017 | Rain has started to fall. | none/no_target | move_to/shelter | WrongLegalAction |  |
| idle-profile | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| idle-none | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-profile | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| idle-profile | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| idle-profile | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| idle-profile | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| idle-none | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| idle-profile | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.711 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| idle-none | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.692 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| idle-profile | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| idle-none | situation_038 | A merchant is blocking the crossing. | move_to/bridge | move_to/village_gate | WrongLegalAction |  |
| idle-profile | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| idle-none | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| idle-profile | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| idle-none | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| idle-none | situation_043 | It has been quiet at the gate all day. | none/no_target | move_to/well | WrongLegalAction |  |
| idle-profile | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| idle-profile | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| idle-profile | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| idle-none | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| idle-profile | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| idle-none | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| idle-profile | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| idle-none | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| idle-profile | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| idle-profile | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-none | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-profile | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| idle-profile | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| idle-profile | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| idle-none | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| idle-profile | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-none | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-profile | event_019 | A villager waves from across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| idle-none | event_019 | A villager waves from across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| idle-profile | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| idle-none | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| idle-profile | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| idle-profile | event_028 | The player has started running toward the woods. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| idle-none | event_028 | The player has started running toward the woods. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-profile | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| idle-none | event_043 | The player has been thrown to the ground. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.66 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| idle-none | event_046 | The wolf has run back into the woods. | none/no_target | move_to/well | WrongLegalAction |  |
| idle-none | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| idle-none | event_050 | A bell is ringing on the next hill over. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| idle-none | event_052 | A wolf and a bear were seen near the docks. | attack_target/wolf | move_to/docks | WrongLegalAction |  |
| idle-profile | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-none | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-profile | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |
| idle-none | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | idle-profile | idle-none |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 0 | 0 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **0** | **0** |

No statement was flagged.
