# Evaluation run 2026-11-23_23-52-42_idle-profile_vs_idle-none

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-23T23:52:42Z |
| Finished (UTC) | 2026-11-23T23:56:29Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| idle-profile | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| idle-none | p4-B | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

## Outcomes

| Outcome | idle-profile | idle-none |
|---|---:|---:|
| Correct | 138 | 149 |
| WrongLegalAction | 52 | 40 |
| ContainedByGuard | 5 | 8 |
| RejectedWhenActionExpected | 14 | 12 |
| PipelineError | 0 | 0 |
| **Accuracy** | **66.0%** (138/209) | **71.3%** (149/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 66.0 | 71.3 | +5.3 | 4.9 | **beyond noise** |
| simple_command | 24 | 79.2 | 95.8 | +16.7 | 14.4 | **beyond noise** |
| command_with_target | 35 | 80.0 | 77.1 | -2.9 | 12.0 | within noise |
| impossible_request | 15 | 86.7 | 86.7 | +0.0 | 18.3 | within noise |
| conversation | 17 | 76.5 | 76.5 | +0.0 | 17.1 | within noise |
| situation | 58 | 51.7 | 60.3 | +8.6 | 9.3 | within noise |
| event | 60 | 58.3 | 63.3 | +5.0 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | idle-profile | idle-none |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | idle-profile | 24 | 19 | 5 | 0 | 0 | 0 |
| simple_command | idle-none | 24 | 23 | 1 | 0 | 0 | 0 |
| command_with_target | idle-profile | 35 | 28 | 6 | 1 | 0 | 0 |
| command_with_target | idle-none | 35 | 27 | 7 | 1 | 0 | 0 |
| impossible_request | idle-profile | 15 | 13 | 1 | 1 | 0 | 0 |
| impossible_request | idle-none | 15 | 13 | 0 | 2 | 0 | 0 |
| conversation | idle-profile | 17 | 13 | 4 | 0 | 0 | 0 |
| conversation | idle-none | 17 | 13 | 4 | 0 | 0 | 0 |
| situation | idle-profile | 58 | 30 | 21 | 1 | 6 | 0 |
| situation | idle-none | 58 | 35 | 16 | 2 | 5 | 0 |
| event | idle-profile | 60 | 35 | 15 | 2 | 8 | 0 |
| event | idle-none | 60 | 38 | 12 | 3 | 7 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| idle-profile | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| idle-none | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| idle-profile | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| idle-none | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| idle-profile | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| idle-none | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| idle-profile | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| idle-none | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| idle-none | target_026 | Let's stay clear of the cave; head for the eastern road instead. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| idle-profile | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.015 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| idle-none | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.011 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| idle-profile | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | attack_target/wolf | WrongLegalAction |  |
| idle-none | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | attack_target/wolf | WrongLegalAction |  |
| idle-profile | target_031 | Right, back to the first place I said. | move_to/campfire | move_to/village_gate | WrongLegalAction |  |
| idle-none | target_031 | Right, back to the first place I said. | move_to/campfire | move_to/well | WrongLegalAction |  |
| idle-profile | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| idle-none | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| idle-none | impossible_001 | Attack Godzilla. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.06 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "god…". |
| idle-profile | impossible_012 | Open the locked cave for me. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| idle-profile | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.653 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| idle-none | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.682 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| idle-none | conversation_006 | The weather is changing. | none/no_target | move_to/shelter | WrongLegalAction |  |
| idle-profile | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| idle-none | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| idle-profile | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| idle-none | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| idle-profile | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| idle-profile | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| idle-none | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| idle-none | situation_002 | The path ahead is empty. | none/no_target | move_to/bridge | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.768 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "nothing…". |
| idle-profile | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| idle-none | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| idle-profile | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.683 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| idle-none | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.631 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| idle-profile | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| idle-none | situation_011 | The player is heading off on their own. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-profile | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| idle-profile | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| idle-none | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| idle-profile | situation_021 | Something is moving among the sheep. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.778 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| idle-profile | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-none | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-profile | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| idle-none | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| idle-profile | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| idle-profile | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| idle-none | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| idle-profile | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| idle-none | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| idle-profile | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.794 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| idle-none | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| idle-profile | situation_036 | Children are playing on the docks again. | move_to/docks | move_to/shelter | WrongLegalAction |  |
| idle-none | situation_036 | Children are playing on the docks again. | move_to/docks | move_to/shelter | WrongLegalAction |  |
| idle-profile | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| idle-none | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| idle-profile | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.687 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| idle-none | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.717 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| idle-profile | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.39 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| idle-none | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.466 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| idle-profile | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| idle-none | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| idle-profile | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| idle-none | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| idle-profile | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| idle-none | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| idle-profile | situation_051 | Shouting from the guards' camp. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| idle-none | situation_051 | Shouting from the guards' camp. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.778 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| idle-profile | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| idle-none | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| idle-profile | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| idle-none | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| idle-profile | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| idle-none | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| idle-profile | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| idle-none | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| idle-profile | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| idle-none | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.609 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| idle-none | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.545 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| idle-profile | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-none | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| idle-profile | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| idle-profile | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.447 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "unknown…". |
| idle-none | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.475 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "unknown…". |
| idle-profile | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| idle-none | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| idle-profile | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| idle-none | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| idle-profile | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.507 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| idle-none | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.536 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| idle-profile | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| idle-none | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| idle-none | event_019 | A villager waves from across the square. | none/no_target | move_to/village_gate | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villager…". |
| idle-profile | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| idle-none | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| idle-profile | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| idle-none | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| idle-profile | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| idle-profile | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| idle-none | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| idle-profile | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.231 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| idle-none | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.234 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| idle-profile | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| idle-none | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| idle-profile | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| idle-none | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.8 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| idle-profile | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| idle-none | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| idle-profile | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| idle-none | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| idle-profile | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.202 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| idle-none | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.159 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| idle-profile | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.214 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| idle-none | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.225 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| idle-profile | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| idle-profile | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| idle-none | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| idle-profile | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| idle-none | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| idle-none | event_050 | A bell is ringing on the next hill over. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.8 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hill…". |
| idle-profile | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.741 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "rid…". |
| idle-none | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | idle-profile | idle-none |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 4 | 2 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **4** | **2** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| idle-profile | event_013 | Correct | Strike the wolf to help the player up. | Echo: attack_target's example "Strike the {0}." |
| idle-profile | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| idle-none | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| idle-profile | event_037 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| idle-profile | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| idle-none | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
