# Evaluation run 2026-11-24_04-28-32_templated_vs_summarised

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T04:28:32Z |
| Finished (UTC) | 2026-11-24T04:32:22Z |
| Framework | 0.4.0-preview.2 |
| Set commit | ab2d30bf7b0c8074926f76b7acee6968dc1ef9b0 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| templated | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |
| summarised | p4-B | ollama | qwen3.5:4b | 0.40.14 | on | 3 | qwen3.5:4b 2995/2995 MB in VRAM |

## Outcomes

| Outcome | templated | summarised |
|---|---:|---:|
| Correct | 164 | 162 |
| WrongLegalAction | 42 | 45 |
| ContainedByGuard | 3 | 2 |
| RejectedWhenActionExpected | 0 | 0 |
| PipelineError | 0 | 0 |
| **Accuracy** | **78.5%** (164/209) | **77.5%** (162/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 78.5 | 77.5 | -1.0 | 4.9 | within noise |
| simple_command | 24 | 95.8 | 95.8 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 82.9 | 85.7 | +2.9 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 93.3 | +0.0 | 18.3 | within noise |
| conversation | 17 | 100.0 | 100.0 | +0.0 | 17.1 | within noise |
| situation | 58 | 60.3 | 56.9 | -3.4 | 9.3 | within noise |
| event | 60 | 76.7 | 75.0 | -1.7 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | templated | summarised |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | templated | 24 | 23 | 1 | 0 | 0 | 0 |
| simple_command | summarised | 24 | 23 | 1 | 0 | 0 | 0 |
| command_with_target | templated | 35 | 29 | 4 | 2 | 0 | 0 |
| command_with_target | summarised | 35 | 30 | 4 | 1 | 0 | 0 |
| impossible_request | templated | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | summarised | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | templated | 17 | 17 | 0 | 0 | 0 | 0 |
| conversation | summarised | 17 | 17 | 0 | 0 | 0 | 0 |
| situation | templated | 58 | 35 | 23 | 0 | 0 | 0 |
| situation | summarised | 58 | 33 | 25 | 0 | 0 | 0 |
| event | templated | 60 | 46 | 14 | 0 | 0 | 0 |
| event | summarised | 60 | 45 | 15 | 0 | 0 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| templated | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| summarised | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| templated | target_012 | Could you keep an eye on the well? | move_to/well | none/no_target | WrongLegalAction |  |
| summarised | target_012 | Could you keep an eye on the well? | move_to/well | none/no_target | WrongLegalAction |  |
| templated | target_014 | Meet me at the south tower. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.175 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "south…". |
| templated | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| summarised | target_020 | Head down toward the water. | move_to/bridge\|docks | move_to/well | WrongLegalAction |  |
| templated | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| summarised | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| templated | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "beyond…". |
| summarised | target_027 | Attack the bandit. | none/no_target | attack_target/no_target | ContainedByGuard | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| templated | target_032 | If you're not busy, the market could use a guard. | move_to/market | none/no_target | WrongLegalAction |  |
| summarised | target_032 | If you're not busy, the market could use a guard. | move_to/market | none/no_target | WrongLegalAction |  |
| templated | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| summarised | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.244 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| templated | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| templated | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| summarised | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| templated | situation_015 | The animals at the market are restless. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| summarised | situation_015 | The animals at the market are restless. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| summarised | situation_016 | Night has fallen and no one is watching the ways into the village. | move_to/eastern_road\|village_gate | none/no_target | WrongLegalAction |  |
| templated | situation_018 | Villagers are gathering in the square tonight. | move_to/market\|well | none/no_target | WrongLegalAction |  |
| summarised | situation_018 | Villagers are gathering in the square tonight. | move_to/market\|well | none/no_target | WrongLegalAction |  |
| summarised | situation_020 | A child has wandered toward the stream. | move_to/bridge | move_to/well | WrongLegalAction |  |
| templated | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| summarised | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| templated | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| summarised | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| templated | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| summarised | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| summarised | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| templated | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| summarised | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| templated | situation_030 | Your patrol takes you past the stream. | move_to/bridge | none/no_target | WrongLegalAction |  |
| summarised | situation_030 | Your patrol takes you past the stream. | move_to/bridge | none/no_target | WrongLegalAction |  |
| templated | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| summarised | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| templated | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| summarised | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| templated | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| summarised | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| templated | situation_044 | A storm is coming in from the sea. | move_to/docks\|shelter | none/no_target | WrongLegalAction |  |
| summarised | situation_044 | A storm is coming in from the sea. | move_to/docks\|shelter | none/no_target | WrongLegalAction |  |
| summarised | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| templated | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| summarised | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| templated | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| summarised | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| templated | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| summarised | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| templated | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| summarised | situation_053 | The square is full of smoke. | move_to/market | move_to/well | WrongLegalAction |  |
| templated | situation_054 | A villager is running toward you. | move_to/shelter | follow_player/no_target | WrongLegalAction |  |
| templated | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| summarised | situation_055 | The player has stopped to chat at the well. | none/no_target | stop_following/no_target | WrongLegalAction |  |
| templated | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| summarised | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| templated | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| summarised | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| templated | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| summarised | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| summarised | event_005 | A supply delivery arrived at the docks. | move_to/docks | none/no_target | WrongLegalAction |  |
| templated | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| templated | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| summarised | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| templated | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| summarised | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| templated | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| summarised | event_018 | The player has reached the shelter. | stop_following/no_target | move_to/village_gate | WrongLegalAction |  |
| templated | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| summarised | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| templated | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| summarised | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| summarised | event_033 | A child fell into the stream by the bridge. | move_to/bridge | move_to/well | WrongLegalAction |  |
| templated | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| summarised | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| summarised | event_043 | The player has been thrown to the ground. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| templated | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| summarised | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| templated | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| summarised | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| templated | event_054 | A horn sounds from the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| summarised | event_054 | A horn sounds from the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| templated | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | templated | summarised |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 74 | 69 |
| of a stimulus | 0 | 0 |
| of a statement | 74 | 69 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **74** | **69** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| templated | simple_001 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_001 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_002 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_002 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_003 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_003 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_004 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_004 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_005 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_005 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_007 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_007 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_009 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_009 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_013 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_013 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_015 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_015 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_016 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_016 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_017 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_017 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_018 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_018 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_019 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_019 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_020 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_020 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_021 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_021 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | simple_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_024 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_024 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | target_001 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_001 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_002 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_002 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_006 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_006 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_007 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_007 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_008 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_009 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_009 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_017 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | target_017 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_020 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_020 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_027 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_033 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_035 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | conversation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| summarised | conversation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| templated | conversation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| summarised | conversation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| templated | conversation_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | conversation_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | conversation_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | conversation_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | conversation_017 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | conversation_017 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | situation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| summarised | situation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| templated | situation_003 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| templated | situation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| summarised | situation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| templated | situation_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | situation_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | situation_012 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | situation_012 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | situation_021 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | situation_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | situation_023 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | situation_025 | WrongLegalAction | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| templated | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_035 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | situation_035 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | situation_041 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | situation_041 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | situation_048 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | situation_048 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | situation_053 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_055 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | situation_055 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | situation_058 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | situation_058 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_007 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_007 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_013 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_013 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_024 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | event_024 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | event_028 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | event_037 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_037 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_041 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_043 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_051 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | event_051 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | event_055 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | event_055 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | event_058 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
