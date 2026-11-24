# Evaluation run 2026-11-24_01-45-52_statement_vs_dialogue

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T01:45:52Z |
| Finished (UTC) | 2026-11-24T01:49:29Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| statement | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| dialogue | p4-B | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

Arm dialogue named the free-text field `dialogue`.

## Outcomes

| Outcome | statement | dialogue |
|---|---:|---:|
| Correct | 138 | 139 |
| WrongLegalAction | 52 | 52 |
| ContainedByGuard | 5 | 4 |
| RejectedWhenActionExpected | 14 | 14 |
| PipelineError | 0 | 0 |
| **Accuracy** | **66.0%** (138/209) | **66.5%** (139/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 66.0 | 66.5 | +0.5 | 4.9 | within noise |
| simple_command | 24 | 79.2 | 79.2 | +0.0 | 14.4 | within noise |
| command_with_target | 35 | 80.0 | 82.9 | +2.9 | 12.0 | within noise |
| impossible_request | 15 | 86.7 | 86.7 | +0.0 | 18.3 | within noise |
| conversation | 17 | 76.5 | 76.5 | +0.0 | 17.1 | within noise |
| situation | 58 | 51.7 | 51.7 | +0.0 | 9.3 | within noise |
| event | 60 | 58.3 | 58.3 | +0.0 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | statement | dialogue |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | statement | 24 | 19 | 5 | 0 | 0 | 0 |
| simple_command | dialogue | 24 | 19 | 5 | 0 | 0 | 0 |
| command_with_target | statement | 35 | 28 | 6 | 1 | 0 | 0 |
| command_with_target | dialogue | 35 | 29 | 5 | 1 | 0 | 0 |
| impossible_request | statement | 15 | 13 | 1 | 1 | 0 | 0 |
| impossible_request | dialogue | 15 | 13 | 1 | 1 | 0 | 0 |
| conversation | statement | 17 | 13 | 4 | 0 | 0 | 0 |
| conversation | dialogue | 17 | 13 | 4 | 0 | 0 | 0 |
| situation | statement | 58 | 30 | 21 | 1 | 6 | 0 |
| situation | dialogue | 58 | 30 | 21 | 1 | 6 | 0 |
| event | statement | 60 | 35 | 15 | 2 | 8 | 0 |
| event | dialogue | 60 | 35 | 16 | 1 | 8 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| statement | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| dialogue | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statement | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| dialogue | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statement | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| dialogue | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statement | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| dialogue | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statement | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| dialogue | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| statement | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| dialogue | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| statement | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| dialogue | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| statement | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| dialogue | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| statement | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.015 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| dialogue | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.015 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| statement | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | attack_target/wolf | WrongLegalAction |  |
| statement | target_031 | Right, back to the first place I said. | move_to/campfire | move_to/village_gate | WrongLegalAction |  |
| dialogue | target_031 | Right, back to the first place I said. | move_to/campfire | move_to/village_gate | WrongLegalAction |  |
| statement | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| dialogue | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| statement | impossible_012 | Open the locked cave for me. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| dialogue | impossible_012 | Open the locked cave for me. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| statement | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.653 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| dialogue | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.69 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| statement | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| dialogue | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| statement | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| dialogue | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| statement | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| dialogue | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| statement | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| dialogue | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| statement | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| dialogue | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| statement | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.683 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| dialogue | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.683 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| statement | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| dialogue | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| statement | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| dialogue | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statement | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| dialogue | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| statement | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| dialogue | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| statement | situation_021 | Something is moving among the sheep. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.778 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| dialogue | situation_021 | Something is moving among the sheep. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.778 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| statement | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| dialogue | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| statement | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| dialogue | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statement | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| dialogue | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| statement | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| dialogue | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statement | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| dialogue | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| statement | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| dialogue | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statement | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.794 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| dialogue | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.794 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| statement | situation_036 | Children are playing on the docks again. | move_to/docks | move_to/shelter | WrongLegalAction |  |
| dialogue | situation_036 | Children are playing on the docks again. | move_to/docks | move_to/shelter | WrongLegalAction |  |
| statement | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| dialogue | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statement | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| dialogue | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| statement | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.687 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| dialogue | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.687 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| statement | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.39 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| dialogue | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.39 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| statement | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| dialogue | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| statement | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| dialogue | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| statement | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| dialogue | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| statement | situation_051 | Shouting from the guards' camp. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| dialogue | situation_051 | Shouting from the guards' camp. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| statement | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| dialogue | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| statement | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| dialogue | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| statement | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| dialogue | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| statement | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| dialogue | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| statement | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| dialogue | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| statement | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.609 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| dialogue | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.609 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| statement | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| dialogue | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| statement | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| dialogue | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| statement | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.447 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "unknown…". |
| dialogue | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.447 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "unknown…". |
| statement | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| dialogue | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| statement | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| dialogue | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| statement | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.507 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| dialogue | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.507 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| statement | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| dialogue | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| statement | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| dialogue | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| statement | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| dialogue | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statement | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| dialogue | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| statement | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| dialogue | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| statement | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| dialogue | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| statement | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| dialogue | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| statement | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| dialogue | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| statement | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| dialogue | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| statement | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| dialogue | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| statement | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| dialogue | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statement | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| dialogue | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| statement | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.202 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| dialogue | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.202 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| statement | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.214 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| dialogue | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.189 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| statement | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| dialogue | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| statement | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| dialogue | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| statement | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| dialogue | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| statement | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.741 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "rid…". |
| dialogue | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | statement | dialogue |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 4 | 15 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **4** | **15** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| dialogue | conversation_001 | Correct | Fine day for it. | Echo: the profile's idle example "Fine day for it." |
| dialogue | situation_035 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| dialogue | situation_041 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| dialogue | situation_058 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| dialogue | event_001 | RejectedWhenActionExpected | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| dialogue | event_002 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| dialogue | event_007 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| statement | event_013 | Correct | Strike the wolf to help the player up. | Echo: attack_target's example "Strike the {0}." |
| dialogue | event_013 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| statement | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| dialogue | event_018 | Correct | Fine day for it. | Echo: the profile's idle example "Fine day for it." |
| dialogue | event_020 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| dialogue | event_028 | WrongLegalAction | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| dialogue | event_029 | ContainedByGuard | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| statement | event_037 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| dialogue | event_037 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| dialogue | event_041 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| statement | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| dialogue | event_052 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
