# Evaluation run 2026-11-24_01-53-24_history_vs_no-history

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T01:53:24Z |
| Finished (UTC) | 2026-11-24T01:56:54Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| history | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| no-history | p4-B | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

Arm no-history ignored each prompt's history: every prompt was asked alone.

## Outcomes

| Outcome | history | no-history |
|---|---:|---:|
| Correct | 138 | 134 |
| WrongLegalAction | 52 | 55 |
| ContainedByGuard | 5 | 5 |
| RejectedWhenActionExpected | 14 | 15 |
| PipelineError | 0 | 0 |
| **Accuracy** | **66.0%** (138/209) | **64.1%** (134/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 66.0 | 64.1 | -1.9 | 4.9 | within noise |
| simple_command | 24 | 79.2 | 70.8 | -8.3 | 14.4 | within noise |
| command_with_target | 35 | 80.0 | 82.9 | +2.9 | 12.0 | within noise |
| impossible_request | 15 | 86.7 | 80.0 | -6.7 | 18.3 | within noise |
| conversation | 17 | 76.5 | 76.5 | +0.0 | 17.1 | within noise |
| situation | 58 | 51.7 | 50.0 | -1.7 | 9.3 | within noise |
| event | 60 | 58.3 | 56.7 | -1.7 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | history | no-history |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | history | 24 | 19 | 5 | 0 | 0 | 0 |
| simple_command | no-history | 24 | 17 | 7 | 0 | 0 | 0 |
| command_with_target | history | 35 | 28 | 6 | 1 | 0 | 0 |
| command_with_target | no-history | 35 | 29 | 5 | 1 | 0 | 0 |
| impossible_request | history | 15 | 13 | 1 | 1 | 0 | 0 |
| impossible_request | no-history | 15 | 12 | 2 | 1 | 0 | 0 |
| conversation | history | 17 | 13 | 4 | 0 | 0 | 0 |
| conversation | no-history | 17 | 13 | 4 | 0 | 0 | 0 |
| situation | history | 58 | 30 | 21 | 1 | 6 | 0 |
| situation | no-history | 58 | 29 | 21 | 1 | 7 | 0 |
| event | history | 60 | 35 | 15 | 2 | 8 | 0 |
| event | no-history | 60 | 34 | 16 | 2 | 8 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| history | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-history | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| history | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-history | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| history | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-history | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| history | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-history | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| history | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| no-history | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| no-history | simple_019 | You can go. | stop_following/no_target | move_to/bridge | WrongLegalAction |  |
| no-history | simple_020 | Thanks, that's all for now. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| history | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| no-history | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| history | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| no-history | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| history | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| no-history | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| history | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.015 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| no-history | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.015 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| history | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | attack_target/wolf | WrongLegalAction |  |
| history | target_031 | Right, back to the first place I said. | move_to/campfire | move_to/village_gate | WrongLegalAction |  |
| no-history | target_031 | Right, back to the first place I said. | move_to/campfire | none/no_target | WrongLegalAction |  |
| history | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| no-history | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| history | impossible_012 | Open the locked cave for me. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| no-history | impossible_012 | Open the locked cave for me. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| no-history | impossible_014 | I said open the cave! | none/no_target | attack_target/wolf | WrongLegalAction |  |
| history | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.653 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| no-history | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.203 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| history | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| no-history | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| history | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| no-history | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| history | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| no-history | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| history | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| no-history | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| history | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| no-history | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| history | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.683 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| no-history | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.683 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| history | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-history | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| history | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-history | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| history | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| no-history | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| history | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| no-history | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| history | situation_021 | Something is moving among the sheep. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.778 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| no-history | situation_021 | Something is moving among the sheep. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.778 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| history | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| no-history | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| history | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-history | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| history | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| no-history | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| history | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-history | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| history | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| no-history | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| history | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-history | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| history | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.794 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| no-history | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.794 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| history | situation_036 | Children are playing on the docks again. | move_to/docks | move_to/shelter | WrongLegalAction |  |
| no-history | situation_036 | Children are playing on the docks again. | move_to/docks | move_to/shelter | WrongLegalAction |  |
| history | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-history | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| history | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| no-history | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| history | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.687 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| no-history | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.687 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| history | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.39 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| no-history | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.39 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| history | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| no-history | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| history | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| no-history | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.646 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "boat…". |
| no-history | situation_049 | The player is back and walking off again. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| history | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| no-history | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| history | situation_051 | Shouting from the guards' camp. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| no-history | situation_051 | Shouting from the guards' camp. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| history | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| no-history | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| history | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| no-history | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| history | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| no-history | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| history | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| no-history | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| history | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-history | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| history | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.609 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| no-history | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.609 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| history | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| no-history | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| history | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| no-history | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| history | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.447 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "unknown…". |
| no-history | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.447 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "unknown…". |
| history | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| no-history | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| history | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| no-history | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| history | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.507 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| no-history | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.507 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| history | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| no-history | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| history | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| no-history | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| history | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-history | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| history | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| no-history | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| history | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| no-history | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| history | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| no-history | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| history | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| no-history | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| history | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| no-history | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| history | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| no-history | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| history | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| no-history | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| history | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-history | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| history | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| no-history | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| history | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.202 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| no-history | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.202 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| history | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.214 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| no-history | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.211 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| history | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| no-history | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| history | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| no-history | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| history | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| no-history | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| no-history | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| history | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.741 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "rid…". |
| no-history | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.424 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "rid…". |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | history | no-history |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 4 | 4 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **4** | **4** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| history | event_013 | Correct | Strike the wolf to help the player up. | Echo: attack_target's example "Strike the {0}." |
| no-history | event_013 | Correct | Strike the wolf to help the player up. | Echo: attack_target's example "Strike the {0}." |
| history | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| no-history | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| history | event_037 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| no-history | event_037 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| history | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| no-history | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
