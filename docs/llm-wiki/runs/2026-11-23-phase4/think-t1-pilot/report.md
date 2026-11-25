# Evaluation run 2026-11-25_02-21-35_think-off_vs_think-on

33 prompts from `C:\Unity_Projects\wt-p4\UnityProject\Assets\Evaluation\agent.json` (Village guard), asked in two arms interleaved: each prompt in A, then in B.

## Conditions

| | |
|---|---|
| Started (UTC) | 2026-11-25T02:21:35Z |
| Finished (UTC) | 2026-11-25T03:29:36Z |
| Framework | 0.4.0-preview.2 |
| Set commit | f699ed74d6afbe414fae5c914fcb9052e0fcb347 |
| Profile | VillageGuard |
| Graphics device | Null Device (128 MB) |
| Targets | An ExplicitTargetSource made from the set's declared targets, each also told to the model as an observation, "id: description." |
| State | Set on Agent.State, and told to the model as observations by a DescribeState of the caller's own; each answer records its lines. |

| Arm | Config | Provider | Model | Version | checkTarget | Warm-up | Resident after warm-up |
|---|---|---|---|---|---|---|---|
| think-off | p4-A | ollama | qwen3.5:4b | 0.40.14 | on | 0 | qwen3.5:4b 3481/3481 MB in VRAM |
| think-on | p4-B | ollama | qwen3.5:4b, thinking | 0.40.14 | on | 0 | qwen3.5:4b 3481/3481 MB in VRAM |

## Outcomes

| Outcome | think-off | think-on |
|---|---:|---:|
| Correct | 26 | 13 |
| WrongLegalAction | 6 | 1 |
| ContainedByGuard | 1 | 0 |
| RejectedWhenActionExpected | 0 | 0 |
| PipelineError | 0 | 19 |
| **Accuracy** | **78.8%** (26/33) | **39.4%** (13/33) |

## Noise check

Noise floor: 10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50. A difference smaller than it is no result.

| Scope | n | A | B | B − A | Floor | |
|---|---:|---:|---:|---:|---:|---|
| all | 33 | 78.8 | 39.4 | -39.4 | 12.3 | **beyond noise** |
| simple_command | 4 | 75.0 | 50.0 | -25.0 | 35.4 | within noise |
| command_with_target | 5 | 100.0 | 60.0 | -40.0 | 31.6 | **beyond noise** |
| impossible_request | 3 | 66.7 | 33.3 | -33.3 | 40.8 | within noise |
| conversation | 3 | 100.0 | 66.7 | -33.3 | 40.8 | within noise |
| situation | 9 | 55.6 | 22.2 | -33.3 | 23.6 | **beyond noise** |
| event | 9 | 88.9 | 33.3 | -55.6 | 23.6 | **beyond noise** |

## Field order

Whether each reply's fields came in the schema's order, action, then target, then statement. An arm with any out of order measures the order as well as the model.

| | think-off | think-on |
|---|---:|---:|
| In order | 33 | 14 |
| Out of order | 0 | 0 |
| Not checked | 0 | 19 |
| Warm-ups out of order | 0 | 0 |
| | order kept | order kept |

## Per category

| Category | Arm | n | Correct | WrongLegalAction | ContainedByGuard | RejectedWhenActionExpected | PipelineError |
|---|---|---:|---:|---:|---:|---:|---:|
| simple_command | think-off | 4 | 3 | 1 | 0 | 0 | 0 |
| simple_command | think-on | 4 | 2 | 0 | 0 | 0 | 2 |
| command_with_target | think-off | 5 | 5 | 0 | 0 | 0 | 0 |
| command_with_target | think-on | 5 | 3 | 0 | 0 | 0 | 2 |
| impossible_request | think-off | 3 | 2 | 0 | 1 | 0 | 0 |
| impossible_request | think-on | 3 | 1 | 0 | 0 | 0 | 2 |
| conversation | think-off | 3 | 3 | 0 | 0 | 0 | 0 |
| conversation | think-on | 3 | 2 | 0 | 0 | 0 | 1 |
| situation | think-off | 9 | 5 | 4 | 0 | 0 | 0 |
| situation | think-on | 9 | 2 | 1 | 0 | 0 | 6 |
| event | think-off | 9 | 8 | 1 | 0 | 0 | 0 |
| event | think-on | 9 | 3 | 0 | 0 | 0 | 6 |

## Answers that were not correct

| Arm | Prompt | Stimulus | Expected | Model said | Outcome | Guard and reason |
|---|---|---|---|---|---|---|
| think-on | simple_001 | Come with me. | follow_player/no_target | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-off | simple_015 | Don't stop following me. | none/no_target | follow_player/no_target | WrongLegalAction |  |
| think-on | simple_015 | Don't stop following me. | none/no_target | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | target_015 | Don't go to the gate, go to the market instead. | move_to/market | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | target_022 | Get up on the tower and watch the fields. | move_to/north_tower | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | impossible_001 | Attack Godzilla. | none/no_target | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | impossible_008 | Enter the locked cave without opening it. | none/no_target | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-off | impossible_015 | It's right behind you, attack the dragon! | none/no_target | attack_target/wolf | ContainedByGuard | TargetGroundingGuard (RewriteToNone): Only 0.491 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write "dragon…". |
| think-on | conversation_008 | You look tired. | none/no_target | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | situation_001 | Nothing has happened for a while. | none/no_target | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | situation_008 | The player is alone. | none/no_target | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-off | situation_015 | The animals at the market are restless. | attack_target/wolf | move_to/market | WrongLegalAction |  |
| think-on | situation_015 | The animals at the market are restless. | attack_target/wolf | none/no_target | WrongLegalAction |  |
| think-off | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | move_to/eastern_road | WrongLegalAction |  |
| think-on | situation_022 | The player is setting off alone at dusk. | follow_player/no_target | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-off | situation_029 | Smoke is drifting across the square. | none/no_target | move_to/well | WrongLegalAction |  |
| think-on | situation_036 | Children are playing on the docks again. | move_to/docks | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | situation_043 | It has been quiet at the gate all day. | none/no_target | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | situation_050 | The market lanterns have gone out. | move_to/market | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-off | situation_057 | The player is leaving through the gate. | follow_player/no_target | move_to/village_gate | WrongLegalAction |  |
| think-on | event_001 | An enemy was spotted near the bridge. | move_to/bridge | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | event_008 | The bridge has collapsed. | none/no_target | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | event_015 | The captain wants a guard by the water before the boats arrive. | move_to/bridge\|docks | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | event_022 | A scream came from the docks. | move_to/docks | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | event_029 | A hunter has killed a wolf. | none/no_target | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-on | event_036 | The boats came in early. | move_to/docks | UnreadableReply: Agent 'Village guard' could not decide. The reply was empty. The reply was: "" | PipelineError |  |
| think-off | event_043 | The player has been thrown to the ground. | attack_target/wolf | stop_following/no_target | WrongLegalAction |  |

## Reply checks

A second score, apart from the outcomes and the accuracy, which no flag changes: whether each statement repeats a few-shot line, is empty, is too long, or leaks an id, a field label, a JSON brace or an assistant's phrase.

| Check | think-off | think-on |
|---|---:|---:|
| Checked | 33 | 14 |
| Echo | 0 | 0 |
| of a stimulus | 0 | 0 |
| of a statement | 0 | 0 |
| Empty | 0 | 0 |
| TooLong (over 200 characters) | 0 | 0 |
| Leak | 0 | 0 |
| **Any flag** | **0** | **0** |

No statement was flagged.
