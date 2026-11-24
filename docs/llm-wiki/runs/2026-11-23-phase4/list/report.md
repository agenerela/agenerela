# Evaluation run 2026-11-23_23-56-46_list-off_vs_list-on

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-23T23:56:46Z |
| Finished (UTC) | 2026-11-24T00:00:36Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| list-off | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| list-on | p4-B | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

## Outcomes

| Outcome | list-off | list-on |
|---|---:|---:|
| Correct | 139 | 149 |
| WrongLegalAction | 52 | 54 |
| ContainedByGuard | 5 | 2 |
| RejectedWhenActionExpected | 13 | 4 |
| PipelineError | 0 | 0 |
| **Accuracy** | **66.5%** (139/209) | **71.3%** (149/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 66.5 | 71.3 | +4.8 | 4.9 | within noise |
| simple_command | 24 | 79.2 | 83.3 | +4.2 | 14.4 | within noise |
| command_with_target | 35 | 80.0 | 85.7 | +5.7 | 12.0 | within noise |
| impossible_request | 15 | 86.7 | 80.0 | -6.7 | 18.3 | within noise |
| conversation | 17 | 76.5 | 88.2 | +11.8 | 17.1 | within noise |
| situation | 58 | 53.4 | 62.1 | +8.6 | 9.3 | within noise |
| event | 60 | 58.3 | 60.0 | +1.7 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | list-off | list-on |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | list-off | 24 | 19 | 5 | 0 | 0 | 0 |
| simple_command | list-on | 24 | 20 | 4 | 0 | 0 | 0 |
| command_with_target | list-off | 35 | 28 | 6 | 1 | 0 | 0 |
| command_with_target | list-on | 35 | 30 | 4 | 1 | 0 | 0 |
| impossible_request | list-off | 15 | 13 | 1 | 1 | 0 | 0 |
| impossible_request | list-on | 15 | 12 | 2 | 1 | 0 | 0 |
| conversation | list-off | 17 | 13 | 4 | 0 | 0 | 0 |
| conversation | list-on | 17 | 15 | 2 | 0 | 0 | 0 |
| situation | list-off | 58 | 31 | 21 | 1 | 5 | 0 |
| situation | list-on | 58 | 36 | 19 | 0 | 3 | 0 |
| event | list-off | 60 | 35 | 15 | 2 | 8 | 0 |
| event | list-on | 60 | 36 | 23 | 0 | 1 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| list-off | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-on | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-on | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-on | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| list-on | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| list-off | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| list-on | target_012 | Could you keep an eye on the well? | move_to/well | none/no_target | WrongLegalAction |  |
| list-off | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| list-on | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| list-off | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| list-on | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| list-off | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.015 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| list-on | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.094 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| list-off | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | attack_target/wolf | WrongLegalAction |  |
| list-off | target_031 | Right, back to the first place I said. | move_to/campfire | move_to/village_gate | WrongLegalAction |  |
| list-on | target_032 | If you're not busy, the market could use a guard. | move_to/market | follow_player/no_target | WrongLegalAction |  |
| list-off | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| list-off | impossible_012 | Open the locked cave for me. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| list-on | impossible_012 | Open the locked cave for me. | none/no_target | none/locked_cave | WrongLegalAction |  |
| list-on | impossible_014 | I said open the cave! | none/no_target | attack_target/locked_cave | WrongLegalAction |  |
| list-off | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.653 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| list-on | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.771 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| list-off | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| list-off | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| list-on | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| list-off | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| list-off | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| list-on | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| list-off | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| list-on | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| list-off | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.655 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| list-on | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| list-off | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| list-on | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| list-off | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-on | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| list-on | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| list-on | situation_018 | Villagers are gathering in the square tonight. | move_to/market\|well | none/no_target | WrongLegalAction |  |
| list-off | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| list-on | situation_019 | Shouting has broken out among the stalls. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.267 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "Two…". |
| list-off | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| list-on | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| list-off | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-on | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| list-off | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| list-off | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| list-on | situation_030 | Your patrol takes you past the stream. | move_to/bridge | follow_player/no_target | WrongLegalAction |  |
| list-off | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| list-on | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| list-off | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.779 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| list-off | situation_036 | Children are playing on the docks again. | move_to/docks | move_to/shelter | WrongLegalAction |  |
| list-off | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-on | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| list-on | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| list-off | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.686 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| list-on | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| list-off | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.405 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| list-on | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.556 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| list-off | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| list-off | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| list-on | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| list-off | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| list-on | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| list-off | situation_051 | Shouting from the guards' camp. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.794 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| list-on | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| list-off | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| list-on | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| list-off | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| list-on | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| list-off | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| list-on | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "village_road…". |
| list-off | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| list-off | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| list-on | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| list-off | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.586 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| list-on | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | WrongLegalAction |  |
| list-on | event_004 | The campfire has gone out. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| list-off | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| list-off | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| list-on | event_009 | An ally is waiting at the shelter. | move_to/shelter | follow_player/no_target | WrongLegalAction |  |
| list-off | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.482 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "unknown…". |
| list-off | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| list-on | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| list-off | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| list-on | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| list-off | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.507 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| list-on | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| list-off | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| list-on | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| list-on | event_018 | The player has reached the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| list-on | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| list-off | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-on | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| list-off | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| list-on | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/shelter | WrongLegalAction |  |
| list-off | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| list-off | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.234 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| list-on | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| list-off | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| list-on | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| list-off | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.792 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| list-on | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| list-off | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| list-on | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/locked_cave | WrongLegalAction |  |
| list-off | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-on | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| list-off | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.219 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| list-on | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| list-off | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.212 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| list-on | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.787 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| list-off | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-on | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| list-on | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| list-off | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| list-on | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| list-on | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| list-on | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| list-off | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.793 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "rid…". |
| list-on | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | list-off | list-on |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 5 | 11 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **5** | **11** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| list-on | simple_017 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| list-on | conversation_001 | Correct | Fine day for it. | Echo: the profile's idle example "Fine day for it." |
| list-off | event_002 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| list-on | event_002 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| list-on | event_007 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| list-off | event_013 | Correct | Strike the wolf to help the player up. | Echo: attack_target's example "Strike the {0}." |
| list-on | event_013 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| list-off | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| list-on | event_018 | WrongLegalAction | Fine day for it. | Echo: the profile's idle example "Fine day for it." |
| list-on | event_020 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| list-off | event_037 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| list-on | event_037 | Correct | Strike the wolf to save the player. | Echo: attack_target's example "Strike the {0}." |
| list-on | event_041 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| list-off | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| list-on | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| list-on | event_052 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
