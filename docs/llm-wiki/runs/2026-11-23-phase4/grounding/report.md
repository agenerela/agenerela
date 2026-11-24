# Evaluation run 2026-11-24_00-00-54_ground-0.8_vs_ground-0.6

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T00:00:54Z |
| Finished (UTC) | 2026-11-24T00:04:35Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| ground-0.8 | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| ground-0.6 | p4-B | ollama | qwen3.5:2b | 0.40.14 | on, threshold 0.6 | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

## Outcomes

| Outcome | ground-0.8 | ground-0.6 |
|---|---:|---:|
| Correct | 138 | 142 |
| WrongLegalAction | 52 | 57 |
| ContainedByGuard | 5 | 2 |
| RejectedWhenActionExpected | 14 | 8 |
| PipelineError | 0 | 0 |
| **Accuracy** | **66.0%** (138/209) | **67.9%** (142/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 66.0 | 67.9 | +1.9 | 4.9 | within noise |
| simple_command | 24 | 79.2 | 79.2 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 80.0 | 80.0 | +0.0 | 12.0 | within noise |
| impossible_request | 15 | 86.7 | 86.7 | +0.0 | 18.3 | within noise |
| conversation | 17 | 76.5 | 76.5 | +0.0 | 17.1 | within noise |
| situation | 58 | 51.7 | 56.9 | +5.2 | 9.3 | within noise |
| event | 60 | 58.3 | 60.0 | +1.7 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | ground-0.8 | ground-0.6 |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | ground-0.8 | 24 | 19 | 5 | 0 | 0 | 0 |
| simple_command | ground-0.6 | 24 | 19 | 5 | 0 | 0 | 0 |
| command_with_target | ground-0.8 | 35 | 28 | 6 | 1 | 0 | 0 |
| command_with_target | ground-0.6 | 35 | 28 | 6 | 1 | 0 | 0 |
| impossible_request | ground-0.8 | 15 | 13 | 1 | 1 | 0 | 0 |
| impossible_request | ground-0.6 | 15 | 13 | 2 | 0 | 0 | 0 |
| conversation | ground-0.8 | 17 | 13 | 4 | 0 | 0 | 0 |
| conversation | ground-0.6 | 17 | 13 | 4 | 0 | 0 | 0 |
| situation | ground-0.8 | 58 | 30 | 21 | 1 | 6 | 0 |
| situation | ground-0.6 | 58 | 33 | 23 | 0 | 2 | 0 |
| event | ground-0.8 | 60 | 35 | 15 | 2 | 8 | 0 |
| event | ground-0.6 | 60 | 36 | 17 | 1 | 6 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| ground-0.8 | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| ground-0.6 | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| ground-0.8 | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| ground-0.6 | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| ground-0.8 | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| ground-0.6 | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| ground-0.8 | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| ground-0.6 | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| ground-0.8 | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.015 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| ground-0.6 | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.015 of the model's probability at the target stayed on the target list (threshold 0.6); it wanted to write "band…". |
| ground-0.8 | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | attack_target/wolf | WrongLegalAction |  |
| ground-0.6 | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | attack_target/wolf | WrongLegalAction |  |
| ground-0.8 | target_031 | Right, back to the first place I said. | move_to/campfire | move_to/village_gate | WrongLegalAction |  |
| ground-0.6 | target_031 | Right, back to the first place I said. | move_to/campfire | move_to/village_gate | WrongLegalAction |  |
| ground-0.8 | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| ground-0.6 | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| ground-0.8 | impossible_012 | Open the locked cave for me. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| ground-0.6 | impossible_012 | Open the locked cave for me. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| ground-0.8 | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.653 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| ground-0.6 | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | WrongLegalAction |  |
| ground-0.8 | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| ground-0.6 | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| ground-0.8 | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.6 | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.8 | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| ground-0.6 | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| ground-0.8 | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.6 | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.8 | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.6 | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.8 | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.683 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| ground-0.6 | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| ground-0.8 | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| ground-0.6 | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| ground-0.6 | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| ground-0.8 | situation_021 | Something is moving among the sheep. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.778 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| ground-0.8 | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| ground-0.6 | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| ground-0.8 | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| ground-0.6 | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| ground-0.6 | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| ground-0.6 | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| ground-0.8 | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| ground-0.6 | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.794 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| ground-0.8 | situation_036 | Children are playing on the docks again. | move_to/docks | move_to/shelter | WrongLegalAction |  |
| ground-0.6 | situation_036 | Children are playing on the docks again. | move_to/docks | move_to/shelter | WrongLegalAction |  |
| ground-0.8 | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| ground-0.6 | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| ground-0.8 | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.687 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| ground-0.6 | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.8 | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.39 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| ground-0.6 | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.39 of the model's probability at the target stayed on the target list (threshold 0.6); it wanted to write "fire…". |
| ground-0.8 | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| ground-0.6 | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| ground-0.8 | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| ground-0.6 | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| ground-0.8 | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| ground-0.6 | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| ground-0.8 | situation_051 | Shouting from the guards' camp. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| ground-0.8 | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.6 | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.8 | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| ground-0.6 | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| ground-0.8 | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| ground-0.6 | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.6); it wanted to write "villag…". |
| ground-0.8 | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| ground-0.6 | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| ground-0.8 | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.609 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| ground-0.6 | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | WrongLegalAction |  |
| ground-0.8 | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| ground-0.6 | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| ground-0.8 | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| ground-0.6 | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.447 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "unknown…". |
| ground-0.6 | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.447 of the model's probability at the target stayed on the target list (threshold 0.6); it wanted to write "unknown…". |
| ground-0.8 | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| ground-0.6 | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| ground-0.8 | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| ground-0.6 | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| ground-0.8 | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.507 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| ground-0.6 | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.507 of the model's probability at the target stayed on the target list (threshold 0.6); it wanted to write "bar…". |
| ground-0.8 | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| ground-0.6 | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| ground-0.6 | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| ground-0.8 | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| ground-0.6 | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.6); it wanted to write "villag…". |
| ground-0.8 | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| ground-0.6 | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| ground-0.8 | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| ground-0.6 | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.6); it wanted to write "hunter…". |
| ground-0.8 | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| ground-0.6 | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| ground-0.8 | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| ground-0.8 | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| ground-0.6 | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| ground-0.8 | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| ground-0.6 | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.202 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| ground-0.6 | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.202 of the model's probability at the target stayed on the target list (threshold 0.6); it wanted to write "band…". |
| ground-0.8 | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.214 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| ground-0.6 | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.214 of the model's probability at the target stayed on the target list (threshold 0.6); it wanted to write "fire…". |
| ground-0.8 | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.6 | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| ground-0.8 | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.6 | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| ground-0.8 | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| ground-0.6 | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| ground-0.8 | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.741 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "rid…". |
| ground-0.6 | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | ground-0.8 | ground-0.6 |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 4 | 4 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **4** | **4** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| ground-0.8 | event_013 | Correct | Strike the wolf to help the player up. | Echo: attack_target's example "Strike the {0}." |
| ground-0.6 | event_013 | Correct | Strike the wolf to help the player up. | Echo: attack_target's example "Strike the {0}." |
| ground-0.8 | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| ground-0.6 | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| ground-0.8 | event_037 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| ground-0.6 | event_037 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| ground-0.8 | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| ground-0.6 | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
