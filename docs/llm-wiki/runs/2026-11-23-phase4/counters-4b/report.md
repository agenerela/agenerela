# Evaluation run 2026-11-24_04-42-36_no-counters_vs_counters

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T04:42:36Z |
| Finished (UTC) | 2026-11-24T04:47:07Z |
| Framework | 0.4.0-preview.2 |
| Set commit | ab2d30bf7b0c8074926f76b7acee6968dc1ef9b0 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| no-counters | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |
| counters | p4-B | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |

## Outcomes

| Outcome | no-counters | counters |
|---|---:|---:|
| Correct | 174 | 164 |
| WrongLegalAction | 31 | 42 |
| ContainedByGuard | 3 | 3 |
| RejectedWhenActionExpected | 1 | 0 |
| PipelineError | 0 | 0 |
| **Accuracy** | **83.3%** (174/209) | **78.5%** (164/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 83.3 | 78.5 | -4.8 | 4.9 | within noise |
| simple_command | 24 | 95.8 | 95.8 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 88.6 | 82.9 | -5.7 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 93.3 | +0.0 | 18.3 | within noise |
| conversation | 17 | 100.0 | 100.0 | +0.0 | 17.1 | within noise |
| situation | 58 | 65.5 | 60.3 | -5.2 | 9.3 | within noise |
| event | 60 | 85.0 | 76.7 | -8.3 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | no-counters | counters |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | no-counters | 24 | 23 | 1 | 0 | 0 | 0 |
| simple_command | counters | 24 | 23 | 1 | 0 | 0 | 0 |
| command_with_target | no-counters | 35 | 31 | 2 | 2 | 0 | 0 |
| command_with_target | counters | 35 | 29 | 4 | 2 | 0 | 0 |
| impossible_request | no-counters | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | counters | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | no-counters | 17 | 17 | 0 | 0 | 0 | 0 |
| conversation | counters | 17 | 17 | 0 | 0 | 0 | 0 |
| situation | no-counters | 58 | 38 | 19 | 0 | 1 | 0 |
| situation | counters | 58 | 35 | 23 | 0 | 0 | 0 |
| event | no-counters | 60 | 51 | 9 | 0 | 0 | 0 |
| event | counters | 60 | 46 | 14 | 0 | 0 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| no-counters | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| counters | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| counters | target_012 | Could you keep an eye on the well? | move_to/well | none/no_target | WrongLegalAction |  |
| no-counters | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.317 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| counters | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.175 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| no-counters | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| counters | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| no-counters | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| counters | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| no-counters | target_027 | Attack the bandit. | none/no_target | attack_target/no_target | ContainedByGuard | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| counters | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "beyond…". |
| counters | target_032 | If you're not busy, the market could use a guard. | move_to/market | none/no_target | WrongLegalAction |  |
| no-counters | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.224 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| counters | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| no-counters | situation_002 | The path ahead is empty. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| no-counters | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| counters | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| counters | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| no-counters | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| counters | situation_015 | The animals at the market are restless. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| counters | situation_018 | Villagers are gathering in the square tonight. | move_to/market\|well | none/no_target | WrongLegalAction |  |
| no-counters | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| counters | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-counters | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| counters | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| no-counters | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| counters | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| counters | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| no-counters | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| counters | situation_030 | Your patrol takes you past the stream. | move_to/bridge | none/no_target | WrongLegalAction |  |
| counters | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-counters | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.591 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| no-counters | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| counters | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| counters | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| counters | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| no-counters | situation_041 | A wolf and a stray dog are near the market. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| no-counters | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| no-counters | situation_044 | A storm is coming in from the sea. | move_to/docks\|shelter | none/no_target | WrongLegalAction |  |
| counters | situation_044 | A storm is coming in from the sea. | move_to/docks\|shelter | none/no_target | WrongLegalAction |  |
| no-counters | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| counters | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| counters | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| no-counters | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| counters | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-counters | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| counters | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| counters | situation_054 | A villager is running toward you. | move_to/shelter | follow_player/no_target | WrongLegalAction |  |
| no-counters | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| counters | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| no-counters | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| counters | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| no-counters | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| counters | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-counters | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| counters | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| counters | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| counters | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-counters | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| counters | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| no-counters | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| counters | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| no-counters | event_019 | A villager waves from across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| no-counters | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| counters | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-counters | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| counters | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| counters | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| no-counters | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| counters | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| counters | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | event_052 | A wolf and a bear were seen near the docks. | attack_target/wolf | move_to/docks | WrongLegalAction |  |
| counters | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| counters | event_054 | A horn sounds from the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| no-counters | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |
| counters | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | no-counters | counters |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 78 | 74 |
| of a stimulus | 0 | 0 |
| of a statement | 78 | 74 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **78** | **74** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| no-counters | simple_001 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_001 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_002 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_002 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_003 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_003 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_004 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_004 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_005 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_005 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_007 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_007 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_009 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_009 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_013 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_013 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_015 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_015 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_016 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_016 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_017 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_017 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_018 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_018 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_019 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_019 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_020 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_020 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_021 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_021 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_024 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_024 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | target_001 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_001 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_002 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_002 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_006 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_006 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_007 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_007 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_009 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_012 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_014 | ContainedByGuard | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_017 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | target_017 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_020 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_020 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_024 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | target_027 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_033 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_035 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | impossible_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | impossible_007 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | conversation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| counters | conversation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | conversation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| counters | conversation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | conversation_007 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | conversation_009 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | conversation_010 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | conversation_011 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | conversation_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | conversation_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | conversation_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | conversation_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | conversation_017 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | conversation_017 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | situation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| counters | situation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | situation_003 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| counters | situation_003 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | situation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| counters | situation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | situation_008 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | situation_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | situation_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | situation_012 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | situation_012 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | situation_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | situation_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | situation_025 | WrongLegalAction | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| counters | situation_025 | WrongLegalAction | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_035 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | situation_035 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | situation_037 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | situation_041 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | situation_048 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | situation_048 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | situation_053 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_055 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | situation_055 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | situation_056 | WrongLegalAction | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | situation_058 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | situation_058 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_004 | WrongLegalAction | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | event_007 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_007 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_013 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_013 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_019 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_024 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | event_024 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | event_028 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | event_037 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_041 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_043 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_051 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | event_051 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | event_055 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | event_055 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | event_058 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
