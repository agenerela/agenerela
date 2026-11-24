# Evaluation run 2026-11-24_04-36-33_no-statements_vs_statements

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T04:36:33Z |
| Finished (UTC) | 2026-11-24T04:42:19Z |
| Framework | 0.4.0-preview.2 |
| Set commit | ab2d30bf7b0c8074926f76b7acee6968dc1ef9b0 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| no-statements | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |
| statements | p4-B | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |

## Outcomes

| Outcome | no-statements | statements |
|---|---:|---:|
| Correct | 157 | 167 |
| WrongLegalAction | 48 | 40 |
| ContainedByGuard | 2 | 2 |
| RejectedWhenActionExpected | 2 | 0 |
| PipelineError | 0 | 0 |
| **Accuracy** | **75.1%** (157/209) | **79.9%** (167/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 75.1 | 79.9 | +4.8 | 4.9 | within noise |
| simple_command | 24 | 91.7 | 95.8 | +4.2 | 14.4 | within noise |
| command_with_target | 35 | 88.6 | 88.6 | +0.0 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 93.3 | +0.0 | 18.3 | within noise |
| conversation | 17 | 100.0 | 100.0 | +0.0 | 17.1 | within noise |
| situation | 58 | 55.2 | 60.3 | +5.2 | 9.3 | within noise |
| event | 60 | 68.3 | 78.3 | +10.0 | 9.1 | **beyond noise** |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | no-statements | statements |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | no-statements | 24 | 22 | 2 | 0 | 0 | 0 |
| simple_command | statements | 24 | 23 | 1 | 0 | 0 | 0 |
| command_with_target | no-statements | 35 | 31 | 3 | 1 | 0 | 0 |
| command_with_target | statements | 35 | 31 | 3 | 1 | 0 | 0 |
| impossible_request | no-statements | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | statements | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | no-statements | 17 | 17 | 0 | 0 | 0 | 0 |
| conversation | statements | 17 | 17 | 0 | 0 | 0 | 0 |
| situation | no-statements | 58 | 32 | 26 | 0 | 0 | 0 |
| situation | statements | 58 | 35 | 23 | 0 | 0 | 0 |
| event | no-statements | 60 | 41 | 17 | 0 | 2 | 0 |
| event | statements | 60 | 47 | 13 | 0 | 0 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| no-statements | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| statements | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| statements | target_012 | Could you keep an eye on the well? | move_to/well | none/no_target | WrongLegalAction |  |
| no-statements | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| statements | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| no-statements | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| statements | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| no-statements | target_027 | Attack the bandit. | none/no_target | attack_target/no_target | ContainedByGuard | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| statements | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "beyond…". |
| no-statements | target_032 | If you're not busy, the market could use a guard. | move_to/market | none/no_target | WrongLegalAction |  |
| no-statements | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.131 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| statements | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.242 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| no-statements | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| statements | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| no-statements | situation_015 | The animals at the market are restless. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| statements | situation_015 | The animals at the market are restless. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| no-statements | situation_018 | Villagers are gathering in the square tonight. | move_to/market\|well | none/no_target | WrongLegalAction |  |
| statements | situation_018 | Villagers are gathering in the square tonight. | move_to/market\|well | none/no_target | WrongLegalAction |  |
| no-statements | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| statements | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| statements | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| no-statements | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| statements | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| no-statements | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| statements | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| no-statements | situation_030 | Your patrol takes you past the stream. | move_to/bridge | none/no_target | WrongLegalAction |  |
| statements | situation_030 | Your patrol takes you past the stream. | move_to/bridge | none/no_target | WrongLegalAction |  |
| no-statements | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statements | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-statements | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statements | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-statements | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | situation_038 | A merchant is blocking the crossing. | move_to/bridge | move_to/village_gate | WrongLegalAction |  |
| no-statements | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| statements | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| no-statements | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| statements | situation_042 | The fire in the camp is spreading. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| no-statements | situation_044 | A storm is coming in from the sea. | move_to/docks\|shelter | none/no_target | WrongLegalAction |  |
| statements | situation_044 | A storm is coming in from the sea. | move_to/docks\|shelter | none/no_target | WrongLegalAction |  |
| no-statements | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| no-statements | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| statements | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| no-statements | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| statements | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| no-statements | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| statements | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | situation_053 | The square is full of smoke. | move_to/market | none/no_target | WrongLegalAction |  |
| statements | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| no-statements | situation_054 | A villager is running toward you. | move_to/shelter | follow_player/no_target | WrongLegalAction |  |
| statements | situation_054 | A villager is running toward you. | move_to/shelter | follow_player/no_target | WrongLegalAction |  |
| no-statements | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| statements | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| no-statements | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| statements | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| no-statements | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| statements | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.201 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| no-statements | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| statements | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| no-statements | event_005 | A supply delivery arrived at the docks. | move_to/docks | none/no_target | WrongLegalAction |  |
| no-statements | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| statements | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| no-statements | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statements | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-statements | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| statements | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| no-statements | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statements | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-statements | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| statements | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| no-statements | event_025 | A rider is approaching the gate fast. | move_to/village_gate | none/no_target | WrongLegalAction |  |
| no-statements | event_026 | A trader reports a theft. | move_to/market\|docks | none/no_target | WrongLegalAction |  |
| no-statements | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| statements | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| no-statements | event_043 | The player has been thrown to the ground. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.761 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| no-statements | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| statements | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| no-statements | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| statements | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| no-statements | event_054 | A horn sounds from the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| statements | event_054 | A horn sounds from the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| no-statements | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |
| statements | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | no-statements | statements |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 0 | 68 |
| of a stimulus | 0 | 0 |
| of a statement | 0 | 68 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **0** | **68** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| statements | simple_001 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_002 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_003 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_004 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_005 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_007 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_009 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_013 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_015 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_016 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_017 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_018 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_019 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_020 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_021 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_024 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | target_001 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_002 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_006 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_007 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_009 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_020 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_024 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | target_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_027 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | conversation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| statements | conversation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| statements | conversation_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | conversation_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | conversation_017 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | situation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| statements | situation_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | situation_012 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | situation_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | situation_025 | WrongLegalAction | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| statements | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_035 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | situation_041 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | situation_048 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | situation_055 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | situation_058 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_013 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_018 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_024 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | event_037 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_043 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_051 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | event_055 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
