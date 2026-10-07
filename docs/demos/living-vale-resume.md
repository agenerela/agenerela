# Living Vale — resume planning on another machine

**Status:** design draft complete; game implementation has not started.
**Branch:** `codex/rpg-demo-plan`, based on `test`.
**Tracker:** [#69](https://github.com/agenerela/agenerela/issues/69), a real sub-issue of
[Demo games #47](https://github.com/agenerela/agenerela/issues/47).

## Get the saved work

In a clone of `agenerela/agenerela`, fetch the remote and check out the planning branch:

```sh
git fetch origin
git switch --track origin/codex/rpg-demo-plan
```

If a local branch already exists, switch to it and inspect its state before updating.
Use a separate checkout/worktree if another session owns the current working directory.
There is no need to open Unity just to read or revise these plans.

## Read these files

- [Build roadmap](living-vale.md): game milestones and definitions of done.
- [Gameplay design](living-vale-design.md): world, NPCs, quests, RPG systems, chat/trade,
  cancellation, persistence and testing.
- [Asset pipeline](living-vale-assets.md): planned ComfyUI/Docker model and sound workflow.
- [Suggested features](living-vale-suggested-features.md): proposals for the playable game,
  none decided, with an alternative first slice that meets the model early.
- [Framework plan](../FRAMEWORK_BUILD_PLAN.md): architecture and DR-017, the agreed RPG direction.
- [Demo conventions](../../Demos/README.md): project creation and package-reference rules.

## Confirmed choices

This is a separate RPG demo from CompanionRPG (#45). Living Vale is a working name.
Grounded medieval fantasy; third-person single-player; compact open region;
20–30 minute first adventure; combat, inventory, quests and leveling.
Chat with every speaking NPC, with trading inside chat.
Broad roleplay; authored main quests and improvised dialogue/favors; generated quests
as an optional experiment. Friendly NPC combat has consequences and essential quest
characters are protected. Windows only, no deadline. Art style is selected by a future
ComfyUI/Unity pilot.

## Next work

The user may revise the design or request implementation. When implementation is requested,
close G0 before starting G1. Open choices are the final title, exact hardware,
rig/animation sources, art style, magic scope and generated-quest experiment depth.
No Unity project, generated assets, model benchmark or game code has been created.

## Integration constraints

Keep the existing action/target/statement schema. Typed game-owned contracts/quotes own
quantities, prices, objectives and rewards; the model never directly changes the scene.
Inline exact-offer confirmation commits atomic trades. Session/request/revision checks
reject stale responses. Distinguish selected decisions from successful execution receipts.

Use DR-016 grounding, not the superseded lexical check. The empty-target documentation
inconsistency is flagged for #8; do not silently change the required target/no_target rule.
Restore canonical game facts without private framework-memory access.
GreyBoxStrategy remains required and no other demo is canceled.

## Shared-machine and repository rules

Unity is pinned to 6000.3.23f1. Verify the project path/Unity instance before MCP edits.
One session owns a scene/prefab; coordinate GPU, model and Docker use with other sessions.
Framework work stays in UnityProject; demos use public API and relative file references.
Preserve meta GUIDs, use LFS for final binary assets, and target PRs into test.
Do not merge with failing CI, or claim compilation/accuracy without appropriate evidence.

Documentation links, fence balance and diff whitespace were checked. No Unity compilation,
tests, asset generation or model accuracy measurement was performed for this design work.
