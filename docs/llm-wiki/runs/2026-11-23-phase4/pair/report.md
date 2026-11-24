# Evaluation run 2026-11-24_04-18-32_flat_vs_paired

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T04:18:32Z |
| Finished (UTC) | 2026-11-24T04:21:23Z |
| Framework | 0.4.0-preview.2 |
| Set commit | ab2d30bf7b0c8074926f76b7acee6968dc1ef9b0 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| flat | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| paired | p4-B | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

## Outcomes

| Outcome | flat | paired |
|---|---:|---:|
| Correct | 142 | 142 |
| WrongLegalAction | 57 | 58 |
| ContainedByGuard | 3 | 3 |
| RejectedWhenActionExpected | 7 | 6 |
| PipelineError | 0 | 0 |
| **Accuracy** | **67.9%** (142/209) | **67.9%** (142/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 67.9 | 67.9 | +0.0 | 4.9 | within noise |
| simple_command | 24 | 79.2 | 79.2 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 85.7 | 85.7 | +0.0 | 12.0 | within noise |
| impossible_request | 15 | 93.3 | 93.3 | +0.0 | 18.3 | within noise |
| conversation | 17 | 76.5 | 76.5 | +0.0 | 17.1 | within noise |
| situation | 58 | 58.6 | 58.6 | +0.0 | 9.3 | within noise |
| event | 60 | 53.3 | 53.3 | +0.0 | 9.1 | within noise |

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
| simple_command | flat | 24 | 19 | 5 | 0 | 0 | 0 |
| simple_command | paired | 24 | 19 | 5 | 0 | 0 | 0 |
| command_with_target | flat | 35 | 30 | 4 | 1 | 0 | 0 |
| command_with_target | paired | 35 | 30 | 4 | 1 | 0 | 0 |
| impossible_request | flat | 15 | 14 | 0 | 1 | 0 | 0 |
| impossible_request | paired | 15 | 14 | 0 | 1 | 0 | 0 |
| conversation | flat | 17 | 13 | 4 | 0 | 0 | 0 |
| conversation | paired | 17 | 13 | 4 | 0 | 0 | 0 |
| situation | flat | 58 | 34 | 21 | 1 | 2 | 0 |
| situation | paired | 58 | 34 | 22 | 1 | 1 | 0 |
| event | flat | 60 | 32 | 23 | 0 | 5 | 0 |
| event | paired | 60 | 32 | 23 | 0 | 5 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| flat | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | simple_013 | You don't need to follow me anymore. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | simple_013 | You don't need to follow me anymore. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | simple_014 | That's far enough, I can manage from here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | simple_014 | That's far enough, I can manage from here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| paired | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| flat | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| paired | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| flat | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| paired | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| flat | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.009 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| paired | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.009 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| flat | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| paired | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| flat | target_031 | Right, back to the first place I said. | move_to/campfire | none/no_target | WrongLegalAction |  |
| paired | target_031 | Right, back to the first place I said. | move_to/campfire | none/no_target | WrongLegalAction |  |
| flat | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.44 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| paired | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.44 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| flat | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| paired | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| flat | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| paired | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| flat | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| paired | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| flat | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| paired | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| flat | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| paired | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| flat | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.556 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| paired | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.556 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| flat | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| flat | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| paired | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| flat | situation_019 | Shouting has broken out among the stalls. | move_to/market | none/no_target | WrongLegalAction |  |
| paired | situation_019 | Shouting has broken out among the stalls. | move_to/market | none/no_target | WrongLegalAction |  |
| flat | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| flat | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| paired | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| flat | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| paired | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| flat | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| paired | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| flat | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| paired | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| flat | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| paired | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| flat | situation_040 | The player is heading for the cliffs. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_040 | The player is heading for the cliffs. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| flat | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.415 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| paired | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.415 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| flat | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| paired | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| flat | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| paired | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| flat | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| paired | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| flat | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| paired | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| flat | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| paired | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| flat | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| paired | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/wolf | WrongLegalAction |  |
| flat | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| paired | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| flat | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| paired | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| flat | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.689 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| paired | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.689 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| flat | event_006 | A guard reported movement on the eastern road. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| paired | event_006 | A guard reported movement on the eastern road. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| flat | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| paired | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| flat | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| paired | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| flat | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| paired | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| flat | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| paired | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| flat | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.539 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| paired | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.539 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| flat | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| paired | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| flat | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| paired | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| flat | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| paired | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| flat | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| paired | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| flat | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | event_027 | The shelter door has jammed with people inside. | move_to/shelter | none/no_target | WrongLegalAction |  |
| paired | event_027 | The shelter door has jammed with people inside. | move_to/shelter | none/no_target | WrongLegalAction |  |
| flat | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| paired | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| flat | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| paired | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| flat | event_031 | A storm warning has been called. | move_to/well\|shelter | none/no_target | WrongLegalAction |  |
| paired | event_031 | A storm warning has been called. | move_to/well\|shelter | none/no_target | WrongLegalAction |  |
| flat | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| paired | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| flat | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | event_036 | The boats came in early. | move_to/docks | move_to/docks | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.692 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cr…". |
| paired | event_036 | The boats came in early. | move_to/docks | move_to/docks | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.692 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cr…". |
| flat | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.238 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| paired | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.238 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| flat | event_040 | The player whistles for you. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| paired | event_040 | The player whistles for you. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| flat | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.136 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| paired | event_044 | A fire has broken out in the market. | move_to/market | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.024 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "wolves…". |
| flat | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| paired | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| flat | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| paired | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| flat | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| paired | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| flat | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| paired | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| flat | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| paired | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | flat | paired |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 86 | 87 |
| of a stimulus | 0 | 0 |
| of a statement | 86 | 87 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **86** | **87** |

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
| flat | simple_006 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_006 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_007 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_007 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_008 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_009 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_009 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_010 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_011 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_012 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | simple_014 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | simple_014 | WrongLegalAction | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
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
| flat | simple_022 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | simple_022 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
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
| flat | target_008 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_008 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_011 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_012 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_012 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_013 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_016 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_016 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_019 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_027 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | target_027 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | target_028 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_028 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_032 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_032 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | target_035 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | target_035 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | impossible_015 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | impossible_015 | ContainedByGuard | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | conversation_011 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | conversation_011 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | conversation_015 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_004 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_004 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_018 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_018 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_022 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | situation_022 | WrongLegalAction | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | situation_024 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_024 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_027 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_027 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_030 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_034 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_036 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_036 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_049 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | situation_049 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| flat | situation_051 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_051 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_054 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | situation_055 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_055 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | situation_056 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | situation_056 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_001 | RejectedWhenActionExpected | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_001 | RejectedWhenActionExpected | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_002 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_003 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_004 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_005 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_006 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_006 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_010 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_014 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_014 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_018 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| paired | event_018 | Correct | Understood, I'll hold here. | Echo: stop_following's example statement "Understood, I'll hold here." |
| flat | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_020 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_022 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_022 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_023 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_023 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_025 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_028 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_028 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_033 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_033 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_036 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_036 | RejectedWhenActionExpected | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_037 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_037 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_038 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_038 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_039 | RejectedWhenActionExpected | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_039 | RejectedWhenActionExpected | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_042 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_043 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_043 | Correct | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_045 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_045 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_048 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_048 | WrongLegalAction | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_053 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| paired | event_053 | Correct | Heading over there now. | Echo: move_to's example statement "Heading over there now." |
| flat | event_057 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| paired | event_057 | WrongLegalAction | Stand back, I'll deal with it. | Echo: attack_target's example statement "Stand back, I'll deal with it." |
| flat | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
| paired | event_059 | Correct | Lead the way, I'm right behind you. | Echo: follow_player's example statement "Lead the way, I'm right behind you." |
