# GreyBoxVillage

Standalone Unity **6000.3.23f1**, Universal 3D (URP) project for
[issue #19](https://github.com/agenerela/agenerela/issues/19).
The scene was transferred from the framework dev project after DR-010 moved demo games
into separate projects. Scene, material and script GUIDs were preserved.

## Open and run

Open this folder in Unity Hub. Let Package Manager resolve dependencies and scripts compile.
Open `Assets/GreyBoxVillage/GreyBoxVillage.unity`, then enter Play mode.

- Move with WASD or arrow keys. The camera tracks the player.
- Within 5 m of the gold guard, press Enter, type a command and press Enter again.
- Escape leaves text entry. Movement is suspended while typing.
- Recognized placeholder shortcuts: `Go to the tower`, `Go to the bridge`, `Follow me`,
  and `Attack the training dummy`. Capitalization and trailing periods are ignored.
- The overlay reports the action and target, with provider `none`, latency `N/A`, and
  guards `not run`. Unknown text reports `none / no_target`; this is not an AI decision.
- The local `DemoLauncher` scene is a start screen for this game only. It does not load
  other game projects. Both scenes are registered in Build Settings; the village is first.

## Dependencies and MCP

The framework is loaded from
`file:../../../UnityProject/Packages/com.agenerela.framework` in `Packages/manifest.json`.
The village runtime currently references only its local `Demos.Shared` helpers and uses
no framework API; adding a package dependency does not wire up an AI agent.
The Input System and AI Navigation packages match the framework dev project's versions.

MCP for Unity is pinned to the same git revision as the framework dev project. After
import, open its MCP window, select local HTTP at `http://127.0.0.1:8080`, start the server
if needed, and connect. The existing client uses the `/mcp` endpoint on that server.
Keep only this game open while working on the village.

`Assets/GreyBoxVillage/LocalShared/` contains the provisional player, camera, input,
overlay and start screen. Keep them here until a second game needs them, then extract a
local package under `Demos/Shared/` per the repository's demo rules.

## When the game would call DecideAsync

For this scene the real list has **one moment**: the player submits nonempty text while
within 5 m of the guard. The placeholder command handler occupies that seam today.
In Phase 2, pass the original text as the stimulus and `Player` as `StimulusLabel`, await
the result, and call the framework's separate validated `Execute` method.

There is no idle timer, perception trigger, combat trigger, or day/night cycle in this
scene. Those do not generate requests. Adding them later is a game-side decision.

At submission, `VillageCommands.Observations()` supplies these literal sentences (the
distance is measured at the moment of submission):

```text
It is daytime.
The gate is open.
You are standing at the gate.
You are not following the player.
The player is 3.2 m away.
The tower and bridge can be reached on foot.
The training_dummy is in the training yard.
```

The guard remains stationary, so these facts are appropriate for the placeholder scene.
Once actions actually change state, derive the corresponding observations from that state.

## Actions and targets

The IDs follow [issue #18](https://github.com/agenerela/agenerela/issues/18). No action
assets or profiles have been invented before the framework types exist.

| Future action asset / ID | Target | Current handler | Phase 2 behavior |
|---|---|---|---|
| `follow_player` | `no_target` | `VillageGuard.Follow(player)` logs what it would do | Follow the player's position; unavailable while already following |
| `move_to` | `tower` or `bridge` | `VillageGuard.MoveTo(target)` logs the destination | Set the NavMeshAgent destination to the registered approach point |
| `attack_target` | `training_dummy` | `VillageGuard.Attack(target)` logs the intended attack | Approach the dummy and invoke game-side combat; no combat exists yet |
| Framework-owned `none` | `no_target` | No action | Leave the guard's current behavior unchanged |

The named landmark roots are the registration points. `tower` is at its door approach,
`bridge` is on its deck, and `training_dummy` is in front of the dummy. Geometry is under
`VillageGeometry`; the guard has a NavMeshAgent and a separate capsule collider.

Compared with [#2](https://github.com/agenerela/agenerela/issues/2) and
[#17](https://github.com/agenerela/agenerela/issues/17), this scene needs no additional
framework API: stimulus, label and observations carry the request; target registration
maps IDs to transforms; game-owned state controls availability; Bind/Execute call the
game handlers. Following state belongs in `State` for code and in an observation for the
model. The existing lexical shortcuts are to be replaced, not reused as framework guards.

## Navigation and verification

The scene uses editor-baked `VillageNavMesh.villagenavmesh`, stored as JSON because Unity
forces native NavMeshData `.asset` files to binary even under Force Text. The included
importer creates the native navigation asset in Unity's generated cache. Rebuild it after
geometry changes with `Tools > Demos > GreyBoxVillage > Rebake Navigation`. There is no
runtime bake and no binary artwork in this project.

Verified in this project on Unity 6000.3.23f1:

- All 12 EditMode tests passed, including a test that enters Play mode and types, submits
  and cancels through the UI Toolkit command field.
- The guard is on the imported NavMesh and has complete paths to all three targets.
- The player controller traverses the gate and bridge in both directions.
- `Go to the tower` reports `move_to / tower` without moving the guard.
- Scene wiring, build registration, unknown and negated input, speaking distance, and
  framework assembly isolation are covered by the tests.

Command input and buttons use UI Toolkit with the project's Input System. The static
heading uses IMGUI only to render text. No gameplay compilation errors were reported;
the editor reported an unrelated Visual Studio messaging UDP-port warning.
