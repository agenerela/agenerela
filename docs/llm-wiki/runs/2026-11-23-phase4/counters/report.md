# Evaluation run 2026-11-24_04-25-13_no-counters_vs_counters

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T04:25:13Z |
| Finished (UTC) | 2026-11-24T04:28:15Z |
| Framework | 0.4.0-preview.2 |
| Set commit | ab2d30bf7b0c8074926f76b7acee6968dc1ef9b0 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| no-counters | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| counters | p4-B | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

## Outcomes

| Outcome | no-counters | counters |
|---|---:|---:|
| Correct | 140 | 142 |
| WrongLegalAction | 58 | 57 |
| ContainedByGuard | 4 | 3 |
| RejectedWhenActionExpected | 7 | 7 |
| PipelineError | 0 | 0 |
| **Accuracy** | **67.0%** (140/209) | **67.9%** (142/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 67.0 | 67.9 | +1.0 | 4.9 | within noise |
| simple_command | 24 | 87.5 | 79.2 | -8.3 | 14.4 | within noise |
| command_with_target | 35 | 85.7 | 85.7 | +0.0 | 12.0 | within noise |
| impossible_request | 15 | 86.7 | 93.3 | +6.7 | 18.3 | within noise |
| conversation | 17 | 82.4 | 76.5 | -5.9 | 17.1 | within noise |
| situation | 58 | 50.0 | 58.6 | +8.6 | 9.3 | within noise |
| event | 60 | 55.0 | 53.3 | -1.7 | 9.1 | within noise |

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
| simple_command | no-counters | 24 | 21 | 3 | 0 | 0 | 0 |
| simple_command | counters | 24 | 19 | 5 | 0 | 0 | 0 |
| command_with_target | no-counters | 35 | 30 | 4 | 1 | 0 | 0 |
| command_with_target | counters | 35 | 30 | 4 | 1 | 0 | 0 |
| impossible_request | no-counters | 15 | 13 | 0 | 2 | 0 | 0 |
| impossible_request | counters | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | no-counters | 17 | 14 | 3 | 0 | 0 | 0 |
| conversation | counters | 17 | 13 | 4 | 0 | 0 | 0 |
| situation | no-counters | 58 | 29 | 26 | 1 | 2 | 0 |
| situation | counters | 58 | 34 | 21 | 1 | 2 | 0 |
| event | no-counters | 60 | 33 | 22 | 0 | 5 | 0 |
| event | counters | 60 | 32 | 23 | 0 | 5 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| no-counters | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | simple_013 | You don't need to follow me anymore. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | simple_014 | That's far enough, I can manage from here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| counters | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| no-counters | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| counters | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| no-counters | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| counters | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| no-counters | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.016 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| counters | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.009 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| no-counters | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | attack_target/wolf | WrongLegalAction |  |
| counters | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-counters | target_031 | Right, back to the first place I said. | move_to/campfire | none/no_target | WrongLegalAction |  |
| counters | target_031 | Right, back to the first place I said. | move_to/campfire | none/no_target | WrongLegalAction |  |
| no-counters | impossible_001 | Attack Godzilla. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.088 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "god…". |
| no-counters | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.558 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| counters | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.44 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| counters | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| no-counters | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| counters | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| no-counters | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| counters | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-counters | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| counters | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| no-counters | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| counters | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| no-counters | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.742 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| counters | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.556 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| no-counters | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| counters | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-counters | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| counters | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| no-counters | situation_016 | Night has fallen and no one is watching the ways into the village. | move_to/eastern_road\|village_gate | none/no_target | WrongLegalAction |  |
| no-counters | situation_019 | Shouting has broken out among the stalls. | move_to/market | none/no_target | WrongLegalAction |  |
| counters | situation_019 | Shouting has broken out among the stalls. | move_to/market | none/no_target | WrongLegalAction |  |
| no-counters | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| counters | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-counters | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| no-counters | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| counters | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| no-counters | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| counters | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-counters | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| counters | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| no-counters | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| counters | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-counters | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | situation_038 | A merchant is blocking the crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| no-counters | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| counters | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| no-counters | situation_040 | The player is heading for the cliffs. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| counters | situation_040 | The player is heading for the cliffs. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-counters | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.481 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| counters | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.415 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| no-counters | situation_044 | A storm is coming in from the sea. | move_to/docks\|shelter | none/no_target | WrongLegalAction |  |
| no-counters | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| counters | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| no-counters | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| counters | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| no-counters | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| counters | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| no-counters | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| no-counters | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| counters | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| no-counters | situation_053 | The square is full of smoke. | move_to/market | none/no_target | WrongLegalAction |  |
| counters | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| no-counters | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| counters | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| no-counters | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| counters | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| no-counters | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| counters | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-counters | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.742 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| counters | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.689 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| no-counters | event_005 | A supply delivery arrived at the docks. | move_to/docks | none/no_target | WrongLegalAction |  |
| counters | event_006 | A guard reported movement on the eastern road. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| counters | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| no-counters | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| counters | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| no-counters | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| counters | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| no-counters | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| counters | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| no-counters | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.532 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| counters | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.539 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| counters | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-counters | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| counters | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-counters | event_018 | The player has reached the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| counters | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| counters | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| no-counters | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| no-counters | event_027 | The shelter door has jammed with people inside. | move_to/shelter | none/no_target | WrongLegalAction |  |
| counters | event_027 | The shelter door has jammed with people inside. | move_to/shelter | none/no_target | WrongLegalAction |  |
| no-counters | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| counters | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| no-counters | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| counters | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| no-counters | event_031 | A storm warning has been called. | move_to/well\|shelter | none/no_target | WrongLegalAction |  |
| counters | event_031 | A storm warning has been called. | move_to/well\|shelter | none/no_target | WrongLegalAction |  |
| no-counters | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| counters | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| no-counters | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| counters | event_036 | The boats came in early. | move_to/docks | move_to/docks | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.692 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cr…". |
| no-counters | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.281 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| counters | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.238 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| no-counters | event_040 | The player whistles for you. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| counters | event_040 | The player whistles for you. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-counters | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.159 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| counters | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.136 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| no-counters | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| counters | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| no-counters | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| counters | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| no-counters | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| counters | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-counters | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| counters | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-counters | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| counters | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | no-counters | counters |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 82 | 86 |
| of a stimulus | 0 | 0 |
| of a statement | 82 | 86 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **82** | **86** |

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
| no-counters | simple_006 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_006 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_007 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_007 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_009 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| no-counters | simple_013 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-counters | simple_014 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | simple_014 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
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
| no-counters | simple_022 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | simple_022 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
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
| no-counters | target_008 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_008 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_012 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_016 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_016 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_027 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | target_028 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_028 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_032 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_032 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | target_035 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | target_035 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | impossible_015 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | impossible_015 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | conversation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | conversation_010 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | conversation_011 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | conversation_011 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_004 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | situation_004 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_006 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | situation_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | situation_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_016 | WrongLegalAction | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | situation_018 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | situation_018 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_020 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | situation_022 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | situation_024 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_027 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | situation_027 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | situation_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_033 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_036 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | situation_036 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_045 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | situation_049 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | situation_049 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | situation_051 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | situation_055 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | situation_056 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_001 | RejectedWhenActionExpected | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_006 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_006 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_014 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_014 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_016 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_018 | WrongLegalAction | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| counters | event_018 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| counters | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_022 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_022 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_023 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_023 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_026 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_028 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_028 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_033 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_033 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_036 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_037 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_038 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_038 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_039 | RejectedWhenActionExpected | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_039 | RejectedWhenActionExpected | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_043 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_045 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_045 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_047 | WrongLegalAction | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| no-counters | event_048 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_048 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_053 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| counters | event_053 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_054 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-counters | event_057 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| counters | event_057 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-counters | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| counters | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
