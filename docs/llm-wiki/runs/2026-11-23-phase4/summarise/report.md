# Evaluation run 2026-11-24_04-15-17_templated_vs_summarised

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T04:15:17Z |
| Finished (UTC) | 2026-11-24T04:18:14Z |
| Framework | 0.4.0-preview.2 |
| Set commit | ab2d30bf7b0c8074926f76b7acee6968dc1ef9b0 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| templated | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| summarised | p4-B | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

## Outcomes

| Outcome | templated | summarised |
|---|---:|---:|
| Correct | 142 | 145 |
| WrongLegalAction | 57 | 54 |
| ContainedByGuard | 3 | 3 |
| RejectedWhenActionExpected | 7 | 7 |
| PipelineError | 0 | 0 |
| **Accuracy** | **67.9%** (142/209) | **69.4%** (145/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 67.9 | 69.4 | +1.4 | 4.9 | within noise |
| simple_command | 24 | 79.2 | 79.2 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 85.7 | 85.7 | +0.0 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 93.3 | +0.0 | 18.3 | within noise |
| conversation | 17 | 76.5 | 76.5 | +0.0 | 17.1 | within noise |
| situation | 58 | 58.6 | 56.9 | -1.7 | 9.3 | within noise |
| event | 60 | 53.3 | 60.0 | +6.7 | 9.1 | within noise |

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
| simple_command | templated | 24 | 19 | 5 | 0 | 0 | 0 |
| simple_command | summarised | 24 | 19 | 5 | 0 | 0 | 0 |
| command_with_target | templated | 35 | 30 | 4 | 1 | 0 | 0 |
| command_with_target | summarised | 35 | 30 | 4 | 1 | 0 | 0 |
| impossible_request | templated | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | summarised | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | templated | 17 | 13 | 4 | 0 | 0 | 0 |
| conversation | summarised | 17 | 13 | 4 | 0 | 0 | 0 |
| situation | templated | 58 | 34 | 21 | 1 | 2 | 0 |
| situation | summarised | 58 | 33 | 21 | 1 | 3 | 0 |
| event | templated | 60 | 32 | 23 | 0 | 5 | 0 |
| event | summarised | 60 | 36 | 20 | 0 | 4 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| templated | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | simple_013 | You don't need to follow me anymore. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | simple_013 | You don't need to follow me anymore. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | simple_014 | That's far enough, I can manage from here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | simple_014 | That's far enough, I can manage from here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| summarised | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| templated | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| summarised | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| templated | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| summarised | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| templated | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.009 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| summarised | target_027 | Attack the bandit. | none/no_target | attack_target/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bandid…". |
| templated | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| summarised | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| templated | target_031 | Right, back to the first place I said. | move_to/campfire | none/no_target | WrongLegalAction |  |
| summarised | target_031 | Right, back to the first place I said. | move_to/campfire | none/no_target | WrongLegalAction |  |
| templated | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.44 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| summarised | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.546 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| summarised | conversation_003 | That was a close one. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| templated | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| templated | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| summarised | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| templated | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| summarised | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| templated | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| summarised | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| summarised | situation_002 | The path ahead is empty. | none/no_target | move_to/bridge | WrongLegalAction |  |
| templated | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| summarised | situation_004 | The road is blocked. | none/no_target | move_to/locked_cave | WrongLegalAction |  |
| templated | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.556 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| summarised | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.738 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| templated | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| summarised | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| templated | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| summarised | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| summarised | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/shelter | WrongLegalAction |  |
| templated | situation_019 | Shouting has broken out among the stalls. | move_to/market | none/no_target | WrongLegalAction |  |
| summarised | situation_019 | Shouting has broken out among the stalls. | move_to/market | none/no_target | WrongLegalAction |  |
| templated | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| summarised | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| templated | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| summarised | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| templated | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| summarised | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| templated | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| summarised | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| templated | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| summarised | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| templated | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| summarised | situation_039 | Sheep are loose. | move_to/eastern_road | move_to/shelter | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "sheep…". |
| templated | situation_040 | The player is heading for the cliffs. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| summarised | situation_040 | The player is heading for the cliffs. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| templated | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.415 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| summarised | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.758 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| templated | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| summarised | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| templated | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| summarised | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| templated | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| summarised | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| templated | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| summarised | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| templated | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| summarised | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| templated | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| summarised | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| templated | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| summarised | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| templated | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| summarised | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| templated | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.689 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| templated | event_006 | A guard reported movement on the eastern road. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| templated | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| templated | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| templated | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| summarised | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| templated | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| summarised | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| templated | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.539 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| summarised | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.679 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| templated | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| summarised | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| templated | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| summarised | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| templated | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| summarised | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| templated | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| templated | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | event_027 | The shelter door has jammed with people inside. | move_to/shelter | none/no_target | WrongLegalAction |  |
| templated | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| summarised | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| templated | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| summarised | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| templated | event_031 | A storm warning has been called. | move_to/well\|shelter | none/no_target | WrongLegalAction |  |
| summarised | event_031 | A storm warning has been called. | move_to/well\|shelter | none/no_target | WrongLegalAction |  |
| templated | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| summarised | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| templated | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | event_036 | The boats came in early. | move_to/docks | move_to/docks | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.692 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cr…". |
| summarised | event_036 | The boats came in early. | move_to/docks | move_to/docks | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.743 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cr…". |
| templated | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.238 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| templated | event_040 | The player whistles for you. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| summarised | event_040 | The player whistles for you. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| templated | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.136 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| summarised | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.085 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| templated | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| summarised | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| templated | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| summarised | event_049 | The sheep pen gate has broken. | move_to/market | move_to/shelter | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "sheep…". |
| summarised | event_051 | The player shouts "Don't follow me!" and runs off. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | event_054 | A horn sounds from the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| templated | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| summarised | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| templated | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| summarised | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| templated | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| summarised | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| summarised | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | move_to/campfire | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | templated | summarised |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 86 | 81 |
| of a stimulus | 0 | 2 |
| of a statement | 86 | 79 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **86** | **81** |

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
| templated | simple_006 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_006 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_007 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_009 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | simple_014 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
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
| templated | simple_022 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | simple_022 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
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
| templated | target_008 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_008 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_012 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_016 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_016 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_027 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | target_028 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_028 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_032 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_032 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | target_035 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | target_035 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | impossible_015 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | impossible_015 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | conversation_001 | Correct | Aye, quiet enough for now. | Echo: the profile's idle example statement "Aye, quiet enough for now." |
| templated | conversation_011 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | conversation_011 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_002 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_004 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_018 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_018 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_020 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_022 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | situation_024 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_024 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_027 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_027 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_029 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_036 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_036 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_049 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | situation_049 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| templated | situation_051 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_051 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_055 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_055 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | situation_056 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | situation_056 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_001 | RejectedWhenActionExpected | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_001 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_006 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_007 | Correct | Strike the beast, I'll deal with it. | Echo: attack_target's example "Strike the {0}." |
| templated | event_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_014 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_014 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_018 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| summarised | event_018 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| templated | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_022 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_022 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_023 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_023 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_026 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_028 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_028 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_033 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_033 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_036 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_036 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_037 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_037 | Correct | Strike the beast, I'll take it down. | Echo: attack_target's example "Strike the {0}." |
| templated | event_038 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_038 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_039 | RejectedWhenActionExpected | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_043 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| templated | event_045 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_045 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_048 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_048 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_053 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| summarised | event_053 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_057 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_057 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| summarised | event_058 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| templated | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| summarised | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
