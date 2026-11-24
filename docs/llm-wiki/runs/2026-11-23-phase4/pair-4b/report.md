# Evaluation run 2026-11-24_04-32-39_flat_vs_paired

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T04:32:39Z |
| Finished (UTC) | 2026-11-24T04:36:15Z |
| Framework | 0.4.0-preview.2 |
| Set commit | ab2d30bf7b0c8074926f76b7acee6968dc1ef9b0 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| flat | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |
| paired | p4-B | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |

## Outcomes

| Outcome | flat | paired |
|---|---:|---:|
| Correct | 164 | 164 |
| WrongLegalAction | 42 | 42 |
| ContainedByGuard | 3 | 3 |
| RejectedWhenActionExpected | 0 | 0 |
| PipelineError | 0 | 0 |
| **Accuracy** | **78.5%** (164/209) | **78.5%** (164/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 78.5 | 78.5 | +0.0 | 4.9 | within noise |
| simple_command | 24 | 95.8 | 95.8 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 82.9 | 82.9 | +0.0 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 93.3 | +0.0 | 18.3 | within noise |
| conversation | 17 | 100.0 | 100.0 | +0.0 | 17.1 | within noise |
| situation | 58 | 60.3 | 60.3 | +0.0 | 9.3 | within noise |
| event | 60 | 76.7 | 76.7 | +0.0 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | flat | paired |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | flat | 24 | 23 | 1 | 0 | 0 | 0 |
| simple_command | paired | 24 | 23 | 1 | 0 | 0 | 0 |
| command_with_target | flat | 35 | 29 | 4 | 2 | 0 | 0 |
| command_with_target | paired | 35 | 29 | 4 | 2 | 0 | 0 |
| impossible_request | flat | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | paired | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | flat | 17 | 17 | 0 | 0 | 0 | 0 |
| conversation | paired | 17 | 17 | 0 | 0 | 0 | 0 |
| situation | flat | 58 | 35 | 23 | 0 | 0 | 0 |
| situation | paired | 58 | 35 | 23 | 0 | 0 | 0 |
| event | flat | 60 | 46 | 14 | 0 | 0 | 0 |
| event | paired | 60 | 46 | 14 | 0 | 0 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| flat | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| paired | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| flat | target_012 | Could you keep an eye on the well? | move_to/well | none/no_target | WrongLegalAction |  |
| paired | target_012 | Could you keep an eye on the well? | move_to/well | none/no_target | WrongLegalAction |  |
| flat | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.175 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| paired | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.175 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| flat | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| paired | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| flat | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| paired | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| flat | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "beyond…". |
| paired | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.001 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| flat | target_032 | If you're not busy, the market could use a guard. | move_to/market | none/no_target | WrongLegalAction |  |
| paired | target_032 | If you're not busy, the market could use a guard. | move_to/market | none/no_target | WrongLegalAction |  |
| flat | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| paired | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| flat | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| paired | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| flat | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| paired | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| flat | situation_015 | The animals at the market are restless. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| paired | situation_015 | The animals at the market are restless. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| flat | situation_018 | Villagers are gathering in the square tonight. | move_to/market\|well | none/no_target | WrongLegalAction |  |
| paired | situation_018 | Villagers are gathering in the square tonight. | move_to/market\|well | none/no_target | WrongLegalAction |  |
| flat | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| flat | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| paired | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| flat | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| paired | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| flat | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| paired | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| flat | situation_030 | Your patrol takes you past the stream. | move_to/bridge | none/no_target | WrongLegalAction |  |
| paired | situation_030 | Your patrol takes you past the stream. | move_to/bridge | none/no_target | WrongLegalAction |  |
| flat | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| paired | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| flat | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| paired | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| flat | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| paired | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| flat | situation_044 | A storm is coming in from the sea. | move_to/docks\|shelter | none/no_target | WrongLegalAction |  |
| paired | situation_044 | A storm is coming in from the sea. | move_to/docks\|shelter | none/no_target | WrongLegalAction |  |
| flat | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| paired | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| flat | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| paired | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| flat | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| flat | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| paired | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| flat | situation_054 | A villager is running toward you. | move_to/shelter | follow_player/no_target | WrongLegalAction |  |
| paired | situation_054 | A villager is running toward you. | move_to/shelter | follow_player/no_target | WrongLegalAction |  |
| flat | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| paired | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| flat | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| paired | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| flat | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| flat | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| paired | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| flat | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| paired | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| flat | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| paired | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| flat | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| paired | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| flat | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| paired | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| flat | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| paired | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| flat | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| paired | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| flat | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| paired | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| flat | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| paired | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| flat | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| paired | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| flat | event_054 | A horn sounds from the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| paired | event_054 | A horn sounds from the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| flat | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |
| paired | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | flat | paired |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 74 | 74 |
| of a stimulus | 0 | 0 |
| of a statement | 74 | 74 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **74** | **74** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| flat | simple_001 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_001 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_002 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_002 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_003 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_003 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_004 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_004 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_005 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_005 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_007 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_007 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_009 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_009 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_013 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_013 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_015 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_015 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_016 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_016 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_017 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_017 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_018 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_018 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_019 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_019 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_020 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_020 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_021 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_021 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_024 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_024 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | target_001 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_001 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_002 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_002 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_006 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_006 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_007 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_007 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_009 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_009 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_017 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | target_017 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_020 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_020 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_027 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | target_027 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_035 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_035 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | conversation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| paired | conversation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| flat | conversation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| paired | conversation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| flat | conversation_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | conversation_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | conversation_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | conversation_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | conversation_017 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | conversation_017 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | situation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| paired | situation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| flat | situation_003 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| paired | situation_003 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| flat | situation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| paired | situation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| flat | situation_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | situation_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | situation_012 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | situation_012 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | situation_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | situation_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | situation_025 | WrongLegalAction | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| paired | situation_025 | WrongLegalAction | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| flat | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_035 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | situation_035 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | situation_041 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | situation_041 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | situation_048 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | situation_048 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | situation_053 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_053 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_055 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | situation_055 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | situation_058 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | situation_058 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_007 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_007 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_013 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_013 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_024 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | event_024 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | event_037 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_037 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_041 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_041 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_043 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_043 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_051 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | event_051 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | event_055 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | event_055 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | event_058 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_058 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
