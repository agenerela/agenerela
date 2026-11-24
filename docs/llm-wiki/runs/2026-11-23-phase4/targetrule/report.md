# Evaluation run 2026-11-24_01-49-46_probabilities_vs_target-rule

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-24T01:49:46Z |
| Finished (UTC) | 2026-11-24T01:53:07Z |
| Framework | 0.4.0-preview.2 |
| Set commit | 1a940f45d280fd1b0f204b27cdb07c17871bbf02 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| probabilities | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| target-rule | p4-B | ollama | qwen3.5:2b | 0.40.14 | on, no probabilities | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

## Outcomes

| Outcome | probabilities | target-rule |
|---|---:|---:|
| Correct | 139 | 147 |
| WrongLegalAction | 52 | 62 |
| ContainedByGuard | 5 | 0 |
| RejectedWhenActionExpected | 13 | 0 |
| PipelineError | 0 | 0 |
| **Accuracy** | **66.5%** (139/209) | **70.3%** (147/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 66.5 | 70.3 | +3.8 | 4.9 | within noise |
| simple_command | 24 | 79.2 | 91.7 | +12.5 | 14.4 | within noise |
| command_with_target | 35 | 80.0 | 82.9 | +2.9 | 12.0 | within noise |
| impossible_request | 15 | 86.7 | 100.0 | +13.3 | 18.3 | within noise |
| conversation | 17 | 76.5 | 82.4 | +5.9 | 17.1 | within noise |
| situation | 58 | 55.2 | 56.9 | +1.7 | 9.3 | within noise |
| event | 60 | 56.7 | 56.7 | +0.0 | 9.1 | within noise |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | probabilities | target-rule |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | probabilities | 24 | 19 | 5 | 0 | 0 | 0 |
| simple_command | target-rule | 24 | 22 | 2 | 0 | 0 | 0 |
| command_with_target | probabilities | 35 | 28 | 6 | 1 | 0 | 0 |
| command_with_target | target-rule | 35 | 29 | 6 | 0 | 0 | 0 |
| impossible_request | probabilities | 15 | 13 | 1 | 1 | 0 | 0 |
| impossible_request | target-rule | 15 | 15 | 0 | 0 | 0 | 0 |
| conversation | probabilities | 17 | 13 | 4 | 0 | 0 | 0 |
| conversation | target-rule | 17 | 14 | 3 | 0 | 0 | 0 |
| situation | probabilities | 58 | 32 | 19 | 1 | 6 | 0 |
| situation | target-rule | 58 | 33 | 25 | 0 | 0 | 0 |
| event | probabilities | 60 | 34 | 17 | 2 | 7 | 0 |
| event | target-rule | 60 | 34 | 26 | 0 | 0 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| probabilities | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| probabilities | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| probabilities | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| target-rule | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| probabilities | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| probabilities | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| target-rule | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| probabilities | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| probabilities | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| target-rule | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| probabilities | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| target-rule | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| probabilities | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.015 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| target-rule | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| probabilities | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | attack_target/wolf | WrongLegalAction |  |
| target-rule | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | attack_target/wolf | WrongLegalAction |  |
| probabilities | target_031 | Right, back to the first place I said. | move_to/campfire | move_to/village_gate | WrongLegalAction |  |
| target-rule | target_031 | Right, back to the first place I said. | move_to/campfire | none/no_target | WrongLegalAction |  |
| probabilities | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| target-rule | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| probabilities | impossible_012 | Open the locked cave for me. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| probabilities | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.653 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| probabilities | conversation_008 | You look tired. | none/no_target | move_to/campfire | WrongLegalAction |  |
| probabilities | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| target-rule | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| probabilities | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| target-rule | conversation_012 | I'm heading home through the woods, and I'd hate to go alone. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| probabilities | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| target-rule | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| probabilities | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| probabilities | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.637 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "tower…". |
| target-rule | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| probabilities | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| target-rule | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| probabilities | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| target-rule | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| target-rule | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| target-rule | situation_016 | Night has fallen and no one is watching the ways into the village. | move_to/eastern_road\|village_gate | none/no_target | WrongLegalAction |  |
| probabilities | situation_019 | Shouting has broken out among the stalls. | move_to/market | move_to/shelter | WrongLegalAction |  |
| target-rule | situation_019 | Shouting has broken out among the stalls. | move_to/market | attack_target/market | WrongLegalAction |  |
| probabilities | situation_021 | Something is moving among the sheep. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| probabilities | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| target-rule | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| probabilities | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| target-rule | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| probabilities | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| target-rule | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| probabilities | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| probabilities | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/shelter | WrongLegalAction |  |
| probabilities | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| target-rule | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| probabilities | situation_032 | The player is standing at the gate, waving you over. | move_to/village_gate | move_to/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| target-rule | situation_036 | Children are playing on the docks again. | move_to/docks | attack_target/wolf | WrongLegalAction |  |
| probabilities | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| target-rule | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| target-rule | situation_038 | A merchant is blocking the crossing. | move_to/bridge | attack_target/wolf | WrongLegalAction |  |
| probabilities | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| target-rule | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| probabilities | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.686 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "cl…". |
| target-rule | situation_040 | The player is heading for the cliffs. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| probabilities | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.438 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| target-rule | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/wolf | WrongLegalAction |  |
| probabilities | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| target-rule | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | move_to/village_gate | WrongLegalAction |  |
| probabilities | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| target-rule | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| probabilities | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| target-rule | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| probabilities | situation_051 | Shouting from the guards' camp. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.796 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "A…". |
| target-rule | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| probabilities | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| target-rule | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| probabilities | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| target-rule | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| probabilities | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villag…". |
| target-rule | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/wolf | WrongLegalAction |  |
| probabilities | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| target-rule | situation_055 | The player has stopped to chat at the well. | none/no_target | move_to/well | WrongLegalAction |  |
| probabilities | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| target-rule | situation_057 | The player is leaving through the gate. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| probabilities | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.55 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "enemy…". |
| target-rule | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | WrongLegalAction |  |
| probabilities | event_006 | A guard reported movement on the eastern road. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| probabilities | event_008 | The bridge has collapsed. | none/no_target | move_to/village_gate | WrongLegalAction |  |
| probabilities | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| probabilities | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.465 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "unknown…". |
| probabilities | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| target-rule | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| probabilities | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| target-rule | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| probabilities | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.514 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "bar…". |
| target-rule | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/docks | WrongLegalAction |  |
| probabilities | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| target-rule | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| probabilities | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| target-rule | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| probabilities | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| target-rule | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| probabilities | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/no_target | RejectedWhenActionExpected | SchemaLegalityGuard (Reject): the action needs a target, and the decision has none. |
| target-rule | event_025 | A rider is approaching the gate fast. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| probabilities | event_027 | The shelter door has jammed with people inside. | move_to/shelter | none/no_target | WrongLegalAction |  |
| target-rule | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/village_gate | WrongLegalAction |  |
| probabilities | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| target-rule | event_028 | The player has started running toward the woods. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| probabilities | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.25 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "hunter…". |
| target-rule | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| probabilities | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| target-rule | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| target-rule | event_031 | A storm warning has been called. | move_to/well\|shelter | none/no_target | WrongLegalAction |  |
| probabilities | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.798 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "em…". |
| probabilities | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| target-rule | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| probabilities | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| target-rule | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| probabilities | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| target-rule | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| probabilities | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.189 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| target-rule | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| target-rule | event_040 | The player whistles for you. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| probabilities | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.226 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| target-rule | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | WrongLegalAction |  |
| probabilities | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| probabilities | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| target-rule | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| probabilities | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| target-rule | event_049 | The sheep pen gate has broken. | move_to/market | none/no_target | WrongLegalAction |  |
| target-rule | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| target-rule | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| target-rule | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| probabilities | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.724 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "rid…". |
| target-rule | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| target-rule | event_059 | The player beckons you to follow them out of the square. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | probabilities | target-rule |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 4 | 12 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **4** | **12** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| target-rule | situation_041 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| target-rule | event_001 | WrongLegalAction | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| target-rule | event_002 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| target-rule | event_007 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| probabilities | event_013 | Correct | Strike the wolf to help the player up. | Echo: attack_target's example "Strike the {0}." |
| target-rule | event_013 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| probabilities | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| target-rule | event_018 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| target-rule | event_020 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| target-rule | event_028 | WrongLegalAction | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| probabilities | event_037 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| target-rule | event_037 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| target-rule | event_041 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| probabilities | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| target-rule | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| target-rule | event_052 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
