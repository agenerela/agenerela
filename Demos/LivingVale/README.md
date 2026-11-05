# Living Vale

Standalone Unity **6000.3.23f1**, Universal 3D (URP) project for the
[Living Vale RPG demo](../../docs/demos/living-vale.md), tracked in
[#69](https://github.com/agenerela/agenerela/issues/69). It develops on its own branch,
`69-living-vale`, which merges `test` in when it needs framework changes.

## Play it

1. Open this folder in Unity Hub with the pinned editor.
2. Run **Living Vale → Build Village Scene** (or open `Assets/LivingVale/Scenes/Village.unity`
   if it is already built) and press Play. **Living Vale → Build Windows Player** writes
   `Builds/Windows/LivingVale.exe` (gitignored).

WASD moves, Shift sprints, the mouse orbits the camera, **E** talks to the NPC in front of you
(within 3 m), Esc closes the chat. Walking more than 6 m away also closes it. F1–F5 switch the
scripted replies between normal, slow, never answering, malformed and throwing, so the failure
paths can be tried by hand.

Replies come from the local model through Agenerela by default (Ollama at `127.0.0.1:11434`,
model set on `ValeBootstrap`); **F6** switches between the model and scripted replies.
With Ollama unreachable, a turn fails with a game-authored line and the game stays playable.

## Layout

| Folder | What | Scene? |
|---|---|---|
| `Assets/LivingVale/Interaction/` | Conversations: `ConversationCoordinator`, the `INpcDecisionDriver` boundary, `ScriptedNpcDecisionDriver` | No: plain C#, EditMode-tested |
| `Assets/LivingVale/Unity/` | Bootstrap, player, orbit camera, NPC speakers, focus and range | Yes |
| `Assets/LivingVale/UI/` | The chat overlay, built from code with uGUI | Yes |
| `Assets/LivingVale/Agenerela/` | Real NPC decisions: `AgenerelaNpcDecisionDriver` (one framework `Agent` per NPC, personas in `NpcPersona.cs`) and `SwitchableNpcDecisionDriver`, behind F6 | No: plain C#, EditMode-tested with a fake provider |
| `Assets/LivingVale/Editor/` | `VillageSceneBuilder` and `LivingValeBuild`, also runnable in batch mode | Editor only |
| `Assets/LivingVale/Tests/Editor/` | EditMode tests | — |

NPC replies use Agenerela's `Agent` by default; the overlay's badge says which driver is
selected, so a scripted reply is never mistaken for a model's. The scene is generated: change
`VillageSceneBuilder` and rebuild rather than editing `Village.unity` by hand.
