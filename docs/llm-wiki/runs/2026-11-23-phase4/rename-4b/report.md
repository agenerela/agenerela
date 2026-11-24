# Evaluation run 2026-11-24_02-13-20_statement_vs_dialogue

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T02:13:20Z |
| Finished (UTC) | 2026-11-24T02:17:28Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| statement | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |
| dialogue | p4-B | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |

Arm dialogue named the free-text field `dialogue`.

## Outcomes

| Outcome | statement | dialogue |
|---|---:|---:|
| Correct | 173 | 171 |
| WrongLegalAction | 32 | 33 |
| ContainedByGuard | 3 | 3 |
| RejectedWhenActionExpected | 1 | 2 |
| PipelineError | 0 | 0 |
| **Accuracy** | **82.8%** (173/209) | **81.8%** (171/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 82.8 | 81.8 | -1.0 | 4.9 | within noise |
| simple_command | 24 | 95.8 | 95.8 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 88.6 | 88.6 | +0.0 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 93.3 | +0.0 | 18.3 | within noise |
| conversation | 17 | 100.0 | 100.0 | +0.0 | 17.1 | within noise |
| situation | 58 | 67.2 | 67.2 | +0.0 | 9.3 | within noise |
| event | 60 | 81.7 | 78.3 | -3.3 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | statement | dialogue |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | statement | 24 | 23 | 1 | 0 | 0 | 0 |
| simple_command | dialogue | 24 | 23 | 1 | 0 | 0 | 0 |
| command_with_target | statement | 35 | 31 | 2 | 2 | 0 | 0 |
| command_with_target | dialogue | 35 | 31 | 2 | 2 | 0 | 0 |
| impossible_request | statement | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | dialogue | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | statement | 17 | 17 | 0 | 0 | 0 | 0 |
| conversation | dialogue | 17 | 17 | 0 | 0 | 0 | 0 |
| situation | statement | 58 | 39 | 18 | 0 | 1 | 0 |
| situation | dialogue | 58 | 39 | 18 | 0 | 1 | 0 |
| event | statement | 60 | 49 | 11 | 0 | 0 | 0 |
| event | dialogue | 60 | 47 | 12 | 0 | 1 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| statement | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| dialogue | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| statement | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.338 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| dialogue | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.338 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| statement | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| dialogue | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| statement | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| dialogue | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| statement | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bandid…". |
| dialogue | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bandid…". |
| statement | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.132 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| dialogue | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.137 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| statement | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| dialogue | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| statement | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| dialogue | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| statement | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| dialogue | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| statement | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| dialogue | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| statement | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| dialogue | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| statement | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| dialogue | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| statement | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| dialogue | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| statement | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.711 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| dialogue | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.711 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| statement | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| dialogue | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statement | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| dialogue | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| statement | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| dialogue | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| statement | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| dialogue | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statement | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| dialogue | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| statement | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| dialogue | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| statement | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| dialogue | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| statement | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| dialogue | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| statement | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| dialogue | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| statement | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| dialogue | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| statement | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| dialogue | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| statement | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| dialogue | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| statement | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| dialogue | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| statement | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| dialogue | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| statement | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| dialogue | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| statement | event_019 | A villager waves from across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| dialogue | event_019 | A villager waves from across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| statement | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| dialogue | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statement | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| dialogue | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| statement | event_028 | The player has started running toward the woods. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| dialogue | event_028 | The player has started running toward the woods. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| statement | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| dialogue | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| dialogue | event_043 | The player has been thrown to the ground. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.778 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| dialogue | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| statement | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| dialogue | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| statement | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |
| dialogue | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | statement | dialogue |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 0 | 0 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **0** | **0** |

No statement was flagged.
