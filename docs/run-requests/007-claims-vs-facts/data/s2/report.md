# Evaluation run 2026-11-26_00-45-05_claims-off_vs_claims-on

209 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-26T00:45:05Z |
| Finished (UTC) | 2026-11-26T00:48:30Z |
| Framework | 0.4.0-preview.2 |
| Set commit | f699ed74d6afbe414fae5c914fcb9052e0fcb347 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| claims-off | p4-A | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |
| claims-on | p4-B | ollama | qwen3.5:2b | 0.40.14 | on | 3 | qwen3.5:2b 2254/2254 MB in VRAM |

## Outcomes

| Outcome | claims-off | claims-on |
|---|---:|---:|
| Correct | 149 | 76 |
| WrongLegalAction | 51 | 130 |
| ContainedByGuard | 3 | 0 |
| RejectedWhenActionExpected | 6 | 3 |
| PipelineError | 0 | 0 |
| **Accuracy** | **71.3%** (149/209) | **36.4%** (76/209) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 209 | 71.3 | 36.4 | -34.9 | 4.9 | **beyond noise** |
| simple_command | 24 | 83.3 | 41.7 | -41.7 | 14.4 | **beyond noise** |
| command_with_target | 35 | 85.7 | 22.9 | -62.9 | 12.0 | **beyond noise** |
| impossible_request | 15 | 80.0 | 100.0 | +20.0 | 18.3 | **beyond noise** |
| conversation | 17 | 88.2 | 82.4 | -5.9 | 17.1 | within noise |
| situation | 58 | 62.1 | 29.3 | -32.8 | 9.3 | **beyond noise** |
| event | 60 | 60.0 | 20.0 | -40.0 | 9.1 | **beyond noise** |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | claims-off | claims-on |
|---|---:|---:|
| In order | 209 | 209 |
| Out of order | 0 | 0 |
| Not checked | 0 | 0 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | claims-off | 24 | 20 | 4 | 0 | 0 | 0 |
| simple_command | claims-on | 24 | 10 | 14 | 0 | 0 | 0 |
| command_with_target | claims-off | 35 | 30 | 4 | 1 | 0 | 0 |
| command_with_target | claims-on | 35 | 8 | 27 | 0 | 0 | 0 |
| impossible_request | claims-off | 15 | 12 | 1 | 2 | 0 | 0 |
| impossible_request | claims-on | 15 | 15 | 0 | 0 | 0 | 0 |
| conversation | claims-off | 17 | 15 | 2 | 0 | 0 | 0 |
| conversation | claims-on | 17 | 14 | 3 | 0 | 0 | 0 |
| situation | claims-off | 58 | 36 | 19 | 0 | 3 | 0 |
| situation | claims-on | 58 | 17 | 40 | 0 | 1 | 0 |
| event | claims-off | 60 | 36 | 21 | 0 | 3 | 0 |
| event | claims-on | 60 | 12 | 46 | 0 | 2 | 0 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| claims-on | simple_001 | Come with me. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-off | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | simple_003 | Wait here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | simple_004 | Stay where you are. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | simple_005 | Follow me. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-off | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | simple_007 | Do not move. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | simple_008 | Stay with me. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-off | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | simple_009 | Hold position. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | simple_012 | Stick by my side until we get home. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-on | simple_014 | That's far enough, I can manage from here. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-off | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| claims-on | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| claims-on | simple_019 | You can go. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | simple_020 | Thanks, that's all for now. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | simple_021 | Never mind, I'll go in alone. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | simple_023 | Hang back, I want to talk to the trader alone. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | target_001 | Head to the bridge. | move_to/bridge | none/no_target | WrongLegalAction |  |
| claims-on | target_002 | Go to the village gate. | move_to/village_gate | none/no_target | WrongLegalAction |  |
| claims-on | target_003 | Walk toward the campfire. | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-on | target_004 | Move beside the well. | move_to/well | none/no_target | WrongLegalAction |  |
| claims-on | target_005 | Go stand at the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-on | target_007 | Take me to the docks. | move_to/docks | none/no_target | WrongLegalAction |  |
| claims-on | target_008 | Go back to the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| claims-on | target_009 | Approach the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-on | target_010 | Walk over to the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-on | target_011 | Head over to the well in the square. | move_to/well | none/no_target | WrongLegalAction |  |
| claims-off | target_012 | Could you keep an eye on the well? | move_to/well | none/no_target | WrongLegalAction |  |
| claims-on | target_012 | Could you keep an eye on the well? | move_to/well | none/no_target | WrongLegalAction |  |
| claims-on | target_013 | Would you mind heading over to the docks? | move_to/docks | follow_player/no_target | WrongLegalAction |  |
| claims-on | target_015 | Don't go to the gate, go to the market instead. | move_to/market | none/no_target | WrongLegalAction |  |
| claims-on | target_019 | Go where the smoke is. | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-on | target_020 | Head down toward the water. | move_to/bridge\|docks | none/no_target | WrongLegalAction |  |
| claims-off | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-on | target_021 | Go and cover whichever way in nobody is watching. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-on | target_022 | Get up on the tower and watch the fields. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-off | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| claims-on | target_023 | Wait for me at the stream crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| claims-on | target_024 | Go to the gate. No, wait, stay with me. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-on | target_025 | Hurry to the shelter! | move_to/shelter | none/no_target | WrongLegalAction |  |
| claims-on | target_026 | Let's stay clear of the cave; head for the eastern road instead. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-off | target_027 | Attack the bandit. | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.094 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "band…". |
| claims-on | target_029 | Forget the wolf, get back up the tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-on | target_030 | The well, please. Quickly. | move_to/well | none/no_target | WrongLegalAction |  |
| claims-on | target_031 | Right, back to the first place I said. | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-off | target_032 | If you're not busy, the market could use a guard. | move_to/market | follow_player/no_target | WrongLegalAction |  |
| claims-on | target_032 | If you're not busy, the market could use a guard. | move_to/market | none/no_target | WrongLegalAction |  |
| claims-on | target_033 | Run to the gate and shut it before the wolves get here. | move_to/village_gate | none/no_target | WrongLegalAction |  |
| claims-on | target_035 | Could you go and check the shelter is ready? | move_to/shelter | none/no_target | WrongLegalAction |  |
| claims-off | impossible_012 | Open the locked cave for me. | none/no_target | none/locked_cave | WrongLegalAction |  |
| claims-off | impossible_014 | I said open the cave! | none/no_target | attack_target/locked_cave | ContainedByGuard | TargetCategoryGuard (RewriteToNone): 'attack_target' takes only targets in the category 'creature', and 'locked_cave' is in the category 'place'. |
| claims-off | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.771 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| claims-off | conversation_011 | The bridge looked lovely in the morning light. | none/no_target | move_to/bridge | WrongLegalAction |  |
| claims-on | conversation_013 | Someone ought to check on the well, it's been making odd noises. | move_to/well | none/no_target | WrongLegalAction |  |
| claims-on | conversation_014 | You've walked with me long enough, friend. Go and rest. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | conversation_015 | I could use a hand down at the docks, if you're not busy. | move_to/docks | follow_player/no_target | WrongLegalAction |  |
| claims-off | conversation_017 | It's getting late and the road home is dark. Walk me back? | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| claims-off | situation_004 | The road is blocked. | none/no_target | move_to/bridge | WrongLegalAction |  |
| claims-off | situation_009 | The watchtower is unattended. | none/no_target | move_to/north_tower | WrongLegalAction |  |
| claims-on | situation_010 | Nobody is keeping watch from the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-off | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-on | situation_011 | The player is heading off on their own. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-off | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | situation_012 | The player has gone inside to sleep. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | situation_013 | Something is wrong down by the water. | move_to/docks | none/no_target | WrongLegalAction |  |
| claims-off | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| claims-on | situation_014 | The square has gone quiet. | move_to/well | none/no_target | WrongLegalAction |  |
| claims-on | situation_015 | The animals at the market are restless. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-on | situation_016 | Night has fallen and no one is watching the ways into the village. | move_to/eastern_road\|village_gate | none/no_target | WrongLegalAction |  |
| claims-off | situation_018 | Villagers are gathering in the square tonight. | move_to/market\|well | none/no_target | WrongLegalAction |  |
| claims-on | situation_018 | Villagers are gathering in the square tonight. | move_to/market\|well | none/no_target | WrongLegalAction |  |
| claims-off | situation_019 | Shouting has broken out among the stalls. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.267 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "Two…". |
| claims-on | situation_019 | Shouting has broken out among the stalls. | move_to/market | none/no_target | WrongLegalAction |  |
| claims-on | situation_020 | A child has wandered toward the stream. | move_to/bridge | none/no_target | WrongLegalAction |  |
| claims-on | situation_021 | Something is moving among the sheep. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-off | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/bridge | WrongLegalAction |  |
| claims-on | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-off | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | situation_023 | The player has sat down by the fire for the night. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | situation_024 | A boat is coming in. | move_to/docks | none/no_target | WrongLegalAction |  |
| claims-on | situation_025 | The wind is picking up fast. | move_to/shelter | none/no_target | WrongLegalAction |  |
| claims-on | situation_026 | The tower lantern is dark. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-on | situation_027 | Two places need a guard at once. | move_to/market\|bridge | none/no_target | WrongLegalAction |  |
| claims-off | situation_030 | Your patrol takes you past the stream. | move_to/bridge | follow_player/no_target | WrongLegalAction |  |
| claims-on | situation_030 | Your patrol takes you past the stream. | move_to/bridge | none/no_target | WrongLegalAction |  |
| claims-off | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-on | situation_031 | It is your turn on watch. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-on | situation_033 | The road east is quiet, but the tower is unmanned. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-on | situation_034 | Villagers are crowding around the well. | move_to/well | none/no_target | WrongLegalAction |  |
| claims-on | situation_035 | The player is being stalked. | attack_target/wolf | follow_player/no_target | WrongLegalAction |  |
| claims-on | situation_036 | Children are playing on the docks again. | move_to/docks | none/no_target | WrongLegalAction |  |
| claims-off | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | situation_037 | The player is safe inside the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | situation_038 | A merchant is blocking the crossing. | move_to/bridge | none/no_target | WrongLegalAction |  |
| claims-off | situation_039 | Sheep are loose. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| claims-on | situation_039 | Sheep are loose. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-off | situation_040 | The player is heading for the cliffs. | follow_player/no_target | move_to/locked_cave | WrongLegalAction |  |
| claims-on | situation_040 | The player is heading for the cliffs. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-off | situation_042 | The fire in the camp is spreading. | move_to/campfire | attack_target/campfire | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.556 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| claims-on | situation_042 | The fire in the camp is spreading. | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-on | situation_044 | A storm is coming in from the sea. | move_to/docks\|shelter | none/no_target | WrongLegalAction |  |
| claims-on | situation_045 | Something is wrong at the edge of the village. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-off | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | move_to/docks | WrongLegalAction |  |
| claims-on | situation_047 | The player has stepped aboard a boat. | stop_following/no_target | follow_player/no_target | WrongLegalAction |  |
| claims-on | situation_048 | A wolf is on the bridge with you. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-on | situation_049 | The player is back and walking off again. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-off | situation_050 | The market lanterns have gone out. | move_to/market | move_to/shelter | WrongLegalAction |  |
| claims-on | situation_050 | The market lanterns have gone out. | move_to/market | none/no_target | WrongLegalAction |  |
| claims-off | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-on | situation_051 | Shouting from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-off | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | attack_target/wolf | WrongLegalAction |  |
| claims-on | situation_052 | The player is crossing the stream alone at night. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-off | situation_053 | The square is full of smoke. | move_to/market | move_to/shelter | WrongLegalAction |  |
| claims-on | situation_053 | The square is full of smoke. | move_to/market | none/no_target | WrongLegalAction |  |
| claims-off | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "village_road…". |
| claims-on | situation_054 | A villager is running toward you. | move_to/shelter | attack_target/village_gate | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "villager…". |
| claims-on | situation_056 | The docks are unwatched tonight. | move_to/docks | none/no_target | WrongLegalAction |  |
| claims-off | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| claims-on | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| claims-on | situation_058 | A wolf is drinking from the well's trough. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-off | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/wolf | WrongLegalAction |  |
| claims-on | event_001 | An enemy was spotted near the bridge. | move_to/bridge | attack_target/bridge | RejectedWhenActionExpected | TargetCategoryGuard (RewriteToNone): 'attack_target' takes only targets in the category 'creature', and 'bridge' is in the category 'place'. |
| claims-on | event_002 | The player has been attacked. | attack_target/wolf | attack_target/wolf | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.767 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "player…". |
| claims-on | event_003 | A villager called for help at the gate. | move_to/village_gate | none/no_target | WrongLegalAction |  |
| claims-off | event_004 | The campfire has gone out. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| claims-on | event_004 | The campfire has gone out. | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-on | event_005 | A supply delivery arrived at the docks. | move_to/docks | none/no_target | WrongLegalAction |  |
| claims-on | event_006 | A guard reported movement on the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-off | event_009 | An ally is waiting at the shelter. | move_to/shelter | follow_player/no_target | WrongLegalAction |  |
| claims-on | event_009 | An ally is waiting at the shelter. | move_to/shelter | none/no_target | WrongLegalAction |  |
| claims-on | event_010 | A scout spotted movement near the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-off | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | attack_target/wolf | WrongLegalAction |  |
| claims-on | event_011 | A villager is trapped inside the locked cave. | move_to/locked_cave | none/no_target | WrongLegalAction |  |
| claims-off | event_012 | A fire has broken out near the market. | move_to/market | move_to/shelter | WrongLegalAction |  |
| claims-on | event_012 | A fire has broken out near the market. | move_to/market | none/no_target | WrongLegalAction |  |
| claims-on | event_013 | The player has been knocked down. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-off | event_014 | A cart has overturned outside the village. | move_to/eastern_road | move_to/bridge | WrongLegalAction |  |
| claims-on | event_014 | A cart has overturned outside the village. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-on | event_016 | Lights were seen in the north tower. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-off | event_017 | The player is leaving through the east wall. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| claims-on | event_017 | The player is leaving through the east wall. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-off | event_018 | The player has reached the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | event_018 | The player has reached the shelter. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-off | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-on | event_021 | The tower bell is ringing. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-on | event_022 | A scream came from the docks. | move_to/docks | none/no_target | WrongLegalAction |  |
| claims-on | event_023 | A messenger says the eastern road is flooded. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-off | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | event_024 | The player said goodbye and went into the inn. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | event_025 | A rider is approaching the gate fast. | move_to/village_gate | none/no_target | WrongLegalAction |  |
| claims-on | event_026 | A trader reports a theft. | move_to/market\|docks | none/no_target | WrongLegalAction |  |
| claims-off | event_027 | The shelter door has jammed with people inside. | move_to/shelter | attack_target/shelter | RejectedWhenActionExpected | TargetCategoryGuard (RewriteToNone): 'attack_target' takes only targets in the category 'creature', and 'shelter' is in the category 'place'. |
| claims-on | event_027 | The shelter door has jammed with people inside. | move_to/shelter | none/no_target | WrongLegalAction |  |
| claims-off | event_029 | A hunter has killed a wolf. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| claims-off | event_030 | The guards at the gate are calling for help. | move_to/village_gate | attack_target/wolf | WrongLegalAction |  |
| claims-on | event_030 | The guards at the gate are calling for help. | move_to/village_gate | none/no_target | WrongLegalAction |  |
| claims-on | event_031 | A storm warning has been called. | move_to/well\|shelter | none/no_target | WrongLegalAction |  |
| claims-off | event_032 | The campfire has been kicked over. | move_to/campfire | move_to/shelter | WrongLegalAction |  |
| claims-on | event_032 | The campfire has been kicked over. | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-on | event_033 | A child fell into the stream by the bridge. | move_to/bridge | none/no_target | WrongLegalAction |  |
| claims-off | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | attack_target/locked_cave | RejectedWhenActionExpected | TargetCategoryGuard (RewriteToNone): 'attack_target' takes only targets in the category 'creature', and 'locked_cave' is in the category 'place'. |
| claims-on | event_034 | Someone has broken into the locked cave. | move_to/locked_cave | none/no_target | WrongLegalAction |  |
| claims-off | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | event_035 | The player has reached home safely. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | event_036 | The boats came in early. | move_to/docks | none/no_target | WrongLegalAction |  |
| claims-on | event_037 | The player is under attack on the bridge. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-on | event_038 | A villager shouts that the tower is on fire. | move_to/north_tower | none/no_target | WrongLegalAction |  |
| claims-off | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | attack_target/wolf | WrongLegalAction |  |
| claims-on | event_039 | Bandits were spotted on the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-on | event_040 | The player whistles for you. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-on | event_041 | A wolf is drinking from the stream under the bridge. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-on | event_043 | The player has been thrown to the ground. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-off | event_044 | A fire has broken out in the market. | move_to/market | attack_target/market | RejectedWhenActionExpected | TargetGroundingGuard (RewriteToNone): Only 0.787 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "fire…". |
| claims-on | event_044 | A fire has broken out in the market. | move_to/market | none/no_target | WrongLegalAction |  |
| claims-on | event_045 | Shouting from the village gate. | move_to/village_gate | none/no_target | WrongLegalAction |  |
| claims-off | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | event_047 | The player has climbed the tower to watch the stars. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-off | event_048 | A traveller says a bridge downstream has washed away. | none/no_target | move_to/bridge | WrongLegalAction |  |
| claims-off | event_049 | The sheep pen gate has broken. | move_to/market | move_to/village_gate | WrongLegalAction |  |
| claims-on | event_049 | The sheep pen gate has broken. | move_to/market | none/no_target | WrongLegalAction |  |
| claims-on | event_051 | The player shouts "Don't follow me!" and runs off. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | event_052 | A wolf and a bear were seen near the docks. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-off | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-on | event_053 | Smoke is rising from the guards' camp. | move_to/campfire | none/no_target | WrongLegalAction |  |
| claims-on | event_054 | A horn sounds from the eastern road. | move_to/eastern_road | none/no_target | WrongLegalAction |  |
| claims-off | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | event_055 | The player waves you away. | stop_following/no_target | none/no_target | WrongLegalAction |  |
| claims-on | event_056 | The storm has passed and the player is setting out. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-off | event_057 | Riders were seen on the eastern road. | none/no_target | attack_target/wolf | WrongLegalAction |  |
| claims-on | event_058 | A villager reports a wolf by the campfire. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| claims-on | event_059 | The player beckons you to follow them out of the square. | follow_player/no_target | none/no_target | WrongLegalAction |  |
| claims-on | event_060 | A cart has tipped over on the bridge. | move_to/bridge | none/no_target | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | claims-off | claims-on |
|---|---:|---:|
| Checked | 209 | 209 |
| Echo | 11 | 0 |
| of a stimulus | 11 | 0 |
| of a statement | 0 | 0 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **11** | **0** |

| Arm | Prompt | Outcome | Statement | Flags |
|---|---|---|---|---|
| claims-off | simple_017 | Correct | You can stay put now. | Echo: stop_following's example "You can stay put now." |
| claims-off | conversation_001 | Correct | Fine day for it. | Echo: the profile's idle example "Fine day for it." |
| claims-off | event_002 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| claims-off | event_007 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| claims-off | event_013 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| claims-off | event_018 | WrongLegalAction | Fine day for it. | Echo: the profile's idle example "Fine day for it." |
| claims-off | event_020 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| claims-off | event_037 | Correct | Strike the wolf to save the player. | Echo: attack_target's example "Strike the {0}." |
| claims-off | event_041 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
| claims-off | event_043 | Correct | Strike the wolf! | Echo: attack_target's example "Strike the {0}." |
| claims-off | event_052 | Correct | Strike the wolf. | Echo: attack_target's example "Strike the {0}." |
