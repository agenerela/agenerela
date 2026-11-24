# Evaluation run 2026-11-24_04-21-40_no-statements_vs_statements

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T04:21:40Z |
| Finished (UTC) | 2026-11-24T04:24:56Z |
| Framework | 0.4.0-preview.2 |
| Set commit | ab2d30bf7b0c8074926f76b7acee6968dc1ef9b0 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| no-statements | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| statements | p4-B | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

## Outcomes

| Outcome | no-statements | statements |
|---|---:|---:|
| Correct | 140 | 141 |
| WrongLegalAction | 50 | 58 |
| ContainedByGuard | 7 | 3 |
| RejectedWhenActionExpected | 12 | 7 |
| PipelineError | 0 | 0 |
| **Accuracy** | **67.0%** (140/209) | **67.5%** (141/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 67.0 | 67.5 | +0.5 | 4.9 | within noise |
| simple_command | 24 | 70.8 | 79.2 | +8.3 | 14.4 | within noise |
| command_with_target | 35 | 85.7 | 85.7 | +0.0 | 12.0 | within noise |
| impossible_request | 15 | 80.0 | 93.3 | +13.3 | 18.3 | within noise |
| conversation | 17 | 76.5 | 76.5 | +0.0 | 17.1 | within noise |
| situation | 58 | 58.6 | 56.9 | -1.7 | 9.3 | within noise |
| event | 60 | 56.7 | 53.3 | -3.3 | 9.1 | within noise |

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
| simple_command | no-statements | 24 | 17 | 7 | 0 | 0 | 0 |
| simple_command | statements | 24 | 19 | 5 | 0 | 0 | 0 |
| command_with_target | no-statements | 35 | 30 | 4 | 1 | 0 | 0 |
| command_with_target | statements | 35 | 30 | 4 | 1 | 0 | 0 |
| impossible_request | no-statements | 15 | 12 | 0 | 3 | 0 | 0 |
| impossible_request | statements | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | no-statements | 17 | 13 | 4 | 0 | 0 | 0 |
| conversation | statements | 17 | 13 | 4 | 0 | 0 | 0 |
| situation | no-statements | 58 | 34 | 19 | 1 | 4 | 0 |
| situation | statements | 58 | 33 | 22 | 1 | 2 | 0 |
| event | no-statements | 60 | 34 | 16 | 2 | 8 | 0 |
| event | statements | 60 | 32 | 23 | 0 | 5 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| no-statements | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | simple_013 | You don't need to follow me anymore. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | simple_013 | You don't need to follow me anymore. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | simple_014 | That's far enough, I can manage from here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | simple_014 | That's far enough, I can manage from here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| statements | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| no-statements | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | move_to/shelter | WrongLegalAction |  |
| statements | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| no-statements | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| statements | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| no-statements | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.006 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| statements | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.009 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| no-statements | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statements | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-statements | target_031 | Right, back to the first place I said. | move_to/campfire | none/no_target | WrongLegalAction |  |
| statements | target_031 | Right, back to the first place I said. | move_to/campfire | none/no_target | WrongLegalAction |  |
| no-statements | impossible_001 | Attack Godzilla. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.071 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "god…". |
| no-statements | impossible_013 | Fight the dragon on the north tower. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.23 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| no-statements | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.5 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| statements | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.44 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| no-statements | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| statements | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| no-statements | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| statements | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| no-statements | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| statements | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| statements | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| no-statements | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| statements | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| no-statements | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.555 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| statements | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.556 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| no-statements | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| statements | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| statements | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| statements | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/shelter | WrongLegalAction |  |
| no-statements | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| statements | situation_019 | Shouting has broken out among the stalls. | move_to/market | none/no_target | WrongLegalAction |  |
| no-statements | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| statements | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| no-statements | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| statements | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-statements | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| statements | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| no-statements | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statements | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-statements | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.756 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| no-statements | situation_036 | Children are playing on the docks again. | move_to/docks | move_to/shelter | WrongLegalAction |  |
| no-statements | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| statements | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| no-statements | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.568 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| statements | situation_040 | The player is heading for the cliffs. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.497 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| statements | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.427 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| no-statements | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| statements | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| no-statements | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| statements | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| no-statements | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| statements | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| no-statements | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| statements | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| no-statements | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| statements | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| no-statements | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| statements | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| no-statements | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| statements | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| no-statements | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| statements | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.599 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| statements | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.689 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| statements | event_006 | A guard reported movement on the eastern road. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| no-statements | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| statements | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| statements | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| no-statements | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.519 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "unknown…". |
| no-statements | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| statements | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| no-statements | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| statements | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| no-statements | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.567 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| statements | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.539 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| statements | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-statements | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| statements | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statements | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-statements | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| statements | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| no-statements | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| statements | event_027 | The shelter door has jammed with people inside. | move_to/shelter | none/no_target | WrongLegalAction |  |
| no-statements | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| statements | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| no-statements | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.327 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| no-statements | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| statements | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| statements | event_031 | A storm warning has been called. | move_to/well\|shelter | none/no_target | WrongLegalAction |  |
| no-statements | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.783 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| no-statements | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| statements | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| no-statements | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_036 | The boats came in early. | move_to/docks | move_to/docks | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.711 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cr…". |
| statements | event_036 | The boats came in early. | move_to/docks | move_to/docks | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.691 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cr…". |
| no-statements | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.253 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| statements | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.264 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| statements | event_040 | The player whistles for you. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.187 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| statements | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.132 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| no-statements | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| statements | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| no-statements | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| statements | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| no-statements | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statements | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| statements | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-statements | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.773 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "rid…". |
| statements | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | no-statements | statements |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 11 | 86 |
| of a stimulus | 11 | 0 |
| of a statement | 0 | 86 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **11** | **86** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| statements | simple_001 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_002 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_003 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_004 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_005 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_006 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_007 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_009 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_014 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_015 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_016 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_017 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_018 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_019 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_020 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_021 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| statements | simple_022 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | simple_024 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | target_001 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_002 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_006 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_007 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_008 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_012 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_016 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_027 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | target_028 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_032 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | target_035 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | impossible_015 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | conversation_011 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_004 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_018 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_020 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_024 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_027 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_036 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_049 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| statements | situation_051 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_055 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | situation_056 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-statements | event_001 | RejectedWhenActionExpected | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| statements | event_001 | RejectedWhenActionExpected | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-statements | event_002 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| statements | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | event_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | event_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | event_006 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-statements | event_007 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| statements | event_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-statements | event_013 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| statements | event_014 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-statements | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| statements | event_018 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| no-statements | event_020 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| statements | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_022 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | event_023 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | event_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-statements | event_028 | WrongLegalAction | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| statements | event_028 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_033 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | event_036 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-statements | event_037 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| statements | event_037 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_038 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | event_039 | RejectedWhenActionExpected | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-statements | event_041 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| statements | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| no-statements | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| statements | event_043 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_045 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | event_048 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| no-statements | event_052 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| statements | event_053 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| statements | event_057 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| statements | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
