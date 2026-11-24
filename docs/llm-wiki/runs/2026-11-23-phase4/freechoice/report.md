# Evaluation run 2026-11-24_03-36-18_plain_vs_free-choice

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T03:36:18Z |
| Finished (UTC) | 2026-11-24T03:39:40Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| plain | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| free-choice | p4-B | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

Arm free-choice added 1 few-shot example(s): "Go wherever you think you're needed." -> move_to, north_tower.

## Outcomes

| Outcome | plain | free-choice |
|---|---:|---:|
| Correct | 138 | 143 |
| WrongLegalAction | 52 | 50 |
| ContainedByGuard | 5 | 7 |
| RejectedWhenActionExpected | 14 | 9 |
| PipelineError | 0 | 0 |
| **Accuracy** | **66.0%** (138/209) | **68.4%** (143/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 66.0 | 68.4 | +2.4 | 4.9 | within noise |
| simple_command | 24 | 79.2 | 66.7 | -12.5 | 14.4 | within noise |
| command_with_target | 35 | 80.0 | 88.6 | +8.6 | 12.0 | within noise |
| impossible_request | 15 | 86.7 | 80.0 | -6.7 | 18.3 | within noise |
| conversation | 17 | 76.5 | 64.7 | -11.8 | 17.1 | within noise |
| situation | 58 | 51.7 | 63.8 | +12.1 | 9.3 | **beyond noise** |
| event | 60 | 58.3 | 60.0 | +1.7 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | plain | free-choice |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | plain | 24 | 19 | 5 | 0 | 0 | 0 |
| simple_command | free-choice | 24 | 16 | 8 | 0 | 0 | 0 |
| command_with_target | plain | 35 | 28 | 6 | 1 | 0 | 0 |
| command_with_target | free-choice | 35 | 31 | 2 | 2 | 0 | 0 |
| impossible_request | plain | 15 | 13 | 1 | 1 | 0 | 0 |
| impossible_request | free-choice | 15 | 12 | 0 | 3 | 0 | 0 |
| conversation | plain | 17 | 13 | 4 | 0 | 0 | 0 |
| conversation | free-choice | 17 | 11 | 6 | 0 | 0 | 0 |
| situation | plain | 58 | 30 | 21 | 1 | 6 | 0 |
| situation | free-choice | 58 | 37 | 17 | 1 | 3 | 0 |
| event | plain | 60 | 35 | 15 | 2 | 8 | 0 |
| event | free-choice | 60 | 36 | 17 | 1 | 6 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| plain | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| free-choice | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| free-choice | simple_019 | You can go. | stop_following/no_target | move_to/market | WrongLegalAction |  |
| free-choice | simple_020 | Thanks, that's all for now. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | simple_022 | Alright, let's keep moving. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| plain | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| free-choice | target_014 | Meet me at the south tower. | none/no_target | move_to/no_target | ContainedByGuard | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| plain | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| free-choice | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| plain | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| plain | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.015 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| free-choice | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.007 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| plain | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | attack_target/wolf | WrongLegalAction |  |
| plain | target_031 | Right, back to the first place I said. | move_to/campfire | move_to/village_gate | WrongLegalAction |  |
| free-choice | target_031 | Right, back to the first place I said. | move_to/campfire | move_to/village_gate | WrongLegalAction |  |
| plain | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| free-choice | impossible_001 | Attack Godzilla. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.087 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "god…". |
| plain | impossible_012 | Open the locked cave for me. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| free-choice | impossible_013 | Fight the dragon on the north tower. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.112 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| plain | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.653 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| free-choice | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.655 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| free-choice | conversation_006 | The weather is changing. | none/no_target | move_to/shelter | WrongLegalAction |  |
| plain | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| free-choice | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| plain | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| free-choice | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| plain | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| free-choice | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| free-choice | conversation_014 | You've walked with me long enough, friend. Go and rest. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| free-choice | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| free-choice | situation_002 | The path ahead is empty. | none/no_target | move_to/bridge | WrongLegalAction |  |
| plain | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| free-choice | situation_004 | The road is blocked. | none/no_target | move_to/eastern_road | WrongLegalAction |  |
| plain | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.683 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| free-choice | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.656 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| plain | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| free-choice | situation_011 | The player is heading off on their own. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| plain | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| plain | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| plain | situation_021 | Something is moving among the sheep. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.778 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| plain | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| free-choice | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| plain | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| free-choice | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| plain | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| plain | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| free-choice | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| plain | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| free-choice | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| plain | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.794 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| plain | situation_036 | Children are playing on the docks again. | move_to/docks | move_to/shelter | WrongLegalAction |  |
| plain | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| free-choice | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| plain | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.687 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| free-choice | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.536 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| plain | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.39 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| free-choice | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.494 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| plain | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| free-choice | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| plain | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| free-choice | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| plain | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| free-choice | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| plain | situation_051 | Shouting from the guards' camp. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| plain | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| free-choice | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| plain | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| free-choice | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| plain | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| free-choice | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| plain | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| plain | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| free-choice | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| plain | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.609 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| free-choice | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.567 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| plain | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| free-choice | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| plain | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| plain | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.447 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "unknown…". |
| plain | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| free-choice | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| plain | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| free-choice | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| plain | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.507 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| free-choice | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.633 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| plain | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| free-choice | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| plain | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| plain | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| free-choice | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.042 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "r…". |
| plain | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| plain | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| free-choice | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| plain | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| free-choice | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.296 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| plain | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| free-choice | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| plain | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| free-choice | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.779 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| plain | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| free-choice | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| plain | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| free-choice | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| plain | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.202 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| free-choice | event_040 | The player whistles for you. | follow_player/no_target | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| plain | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.214 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| free-choice | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.215 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| plain | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| plain | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| free-choice | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| plain | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| free-choice | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| free-choice | event_050 | A bell is ringing on the next hill over. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| free-choice | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| free-choice | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| plain | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.741 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "rid…". |
| free-choice | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | plain | free-choice |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 4 | 3 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **4** | **3** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| free-choice | event_002 | Correct | Strike the beast! | Echo: attack_target's example "Strike the {0}." |
| plain | event_013 | Correct | Strike the wolf to help the player up. | Echo: attack_target's example "Strike the {0}." |
| free-choice | event_013 | Correct | Strike the beast! | Echo: attack_target's example "Strike the {0}." |
| plain | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| plain | event_037 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| free-choice | event_037 | Correct | Strike the beast! | Echo: attack_target's example "Strike the {0}." |
| plain | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
