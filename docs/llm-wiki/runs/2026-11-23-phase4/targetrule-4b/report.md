# Evaluation run 2026-11-24_02-08-15_probabilities_vs_target-rule

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T02:08:15Z |
| Finished (UTC) | 2026-11-24T02:13:02Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| probabilities | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |
| target-rule | p4-B | ollama | qwen3.5:4b | 0.40.14 | on, no probabilities | 3 | qwen3.5:4b 2995/2995 MB in VRAM |

## Outcomes

| Outcome | probabilities | target-rule |
|---|---:|---:|
| Correct | 171 | 169 |
| WrongLegalAction | 33 | 40 |
| ContainedByGuard | 3 | 0 |
| RejectedWhenActionExpected | 2 | 0 |
| PipelineError | 0 | 0 |
| **Accuracy** | **81.8%** (171/209) | **80.9%** (169/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 81.8 | 80.9 | -1.0 | 4.9 | within noise |
| simple_command | 24 | 95.8 | 91.7 | -4.2 | 14.4 | within noise |
| command_with_target | 35 | 88.6 | 97.1 | +8.6 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 100.0 | +6.7 | 18.3 | within noise |
| conversation | 17 | 100.0 | 100.0 | +0.0 | 17.1 | within noise |
| situation | 58 | 65.5 | 67.2 | +1.7 | 9.3 | within noise |
| event | 60 | 80.0 | 70.0 | -10.0 | 9.1 | **beyond noise** |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | probabilities | target-rule |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | probabilities | 24 | 23 | 1 | 0 | 0 | 0 |
| simple_command | target-rule | 24 | 22 | 2 | 0 | 0 | 0 |
| command_with_target | probabilities | 35 | 31 | 2 | 2 | 0 | 0 |
| command_with_target | target-rule | 35 | 34 | 1 | 0 | 0 | 0 |
| impossible_request | probabilities | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | target-rule | 15 | 15 | 0 | 0 | 0 | 0 |
| conversation | probabilities | 17 | 17 | 0 | 0 | 0 | 0 |
| conversation | target-rule | 17 | 17 | 0 | 0 | 0 | 0 |
| situation | probabilities | 58 | 38 | 19 | 0 | 1 | 0 |
| situation | target-rule | 58 | 39 | 19 | 0 | 0 | 0 |
| event | probabilities | 60 | 48 | 11 | 0 | 1 | 0 |
| event | target-rule | 60 | 42 | 18 | 0 | 0 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| probabilities | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| target-rule | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| target-rule | simple_021 | Never mind, I'll go in alone. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| probabilities | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.338 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| probabilities | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| probabilities | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| target-rule | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| probabilities | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bandid…". |
| probabilities | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.132 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| probabilities | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| target-rule | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| probabilities | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| target-rule | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| probabilities | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| target-rule | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| probabilities | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| target-rule | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| probabilities | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| target-rule | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| probabilities | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| target-rule | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| probabilities | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| target-rule | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| probabilities | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.729 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| probabilities | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| target-rule | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| probabilities | situation_038 | A merchant is blocking the crossing. | move_to/bridge | move_to/village_gate | WrongLegalAction |  |
| probabilities | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| target-rule | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| probabilities | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| target-rule | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| target-rule | situation_044 | A storm is coming in from the sea. | move_to/docks\|shelter | none/no_target | WrongLegalAction |  |
| probabilities | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| target-rule | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| probabilities | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| target-rule | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| probabilities | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| target-rule | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| probabilities | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| target-rule | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| probabilities | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| target-rule | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| probabilities | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| target-rule | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| probabilities | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| target-rule | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| probabilities | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| target-rule | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| probabilities | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| target-rule | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| target-rule | event_005 | A supply delivery arrived at the docks. | move_to/docks | none/no_target | WrongLegalAction |  |
| probabilities | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| target-rule | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| probabilities | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| target-rule | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| probabilities | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| target-rule | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| probabilities | event_019 | A villager waves from across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| probabilities | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| target-rule | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| probabilities | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| target-rule | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| probabilities | event_028 | The player has started running toward the woods. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| target-rule | event_028 | The player has started running toward the woods. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| target-rule | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | none/no_target | WrongLegalAction |  |
| target-rule | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| target-rule | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| target-rule | event_040 | The player whistles for you. | follow_player/no_target | move_to/campfire | WrongLegalAction |  |
| probabilities | event_043 | The player has been thrown to the ground. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.76 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| target-rule | event_043 | The player has been thrown to the ground. | attack_target/wolf | stop_following/no_target | WrongLegalAction |  |
| target-rule | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| target-rule | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| probabilities | event_052 | A wolf and a bear were seen near the docks. | attack_target/wolf | move_to/docks | WrongLegalAction |  |
| target-rule | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| target-rule | event_054 | A horn sounds from the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| probabilities | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| target-rule | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| probabilities | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | probabilities | target-rule |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 0 | 0 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 1 |
| **Any flag** | **0** | **1** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| target-rule | situation_004 | Correct | A fallen tree blocks the eastern_road. | Leak: the id eastern_road |
