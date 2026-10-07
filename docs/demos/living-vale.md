# Living Vale — RPG demo build plan

**Status:** editable design draft, 7 October 2026. No game project or implementation
exists. This is a separate RPG demo; *Living Vale* is a working title and folder proposal.

**Development tracker:** [#69](https://github.com/agenerela/agenerela/issues/69), a
sub-issue of [Demo games #47](https://github.com/agenerela/agenerela/issues/47).

**Confirmed direction:** medieval fantasy with grounded characters; single-player,
third-person 3D; a compact open region; a 20–30 minute first adventure; combat,
inventory, quests and leveling; chat with every speaking NPC; broad roleplay and
improvisation; trading through the chat interface without a separate dialogue tree
or shop screen. Also confirmed: authored main quests with improvised dialogue/favors and
some generated-quest experiments; friendly-NPC combat with protected essential characters;
Windows only; no fixed deadline. Art style will be chosen through a ComfyUI/Unity pilot.
Content, balance and technical choices below are proposals.

Read the [detailed game design](living-vale-design.md) for systems and scenarios, and
the [asset pipeline](living-vale-assets.md) for the planned ComfyUI/Docker workflow.
The [framework build plan](../FRAMEWORK_BUILD_PLAN.md) and
[demo conventions](../../Demos/README.md) remain authoritative.

## The game in one paragraph

A traveler reaches a valley where a missing supply wagon has divided two villages
and the castle garrison. The player talks to residents in ordinary language, buys
supplies, follows conflicting leads, fights on the road, and chooses how to return
the recovered medicine. Conversations can produce real trades, information, small
favors and access to places. Characters improvise their responses and react to
what the player actually did. The first adventure ends with a visible change in
the valley and a level-up; the region remains explorable.

## Scope that makes the first build achievable

| Area | First complete adventure | Expansion after that build works |
|---|---|---|
| World | Two small villages, one castle gate/courtyard, forest road, ruined camp; roughly 600 × 600 m as a starting layout | Castle interior, cave, more routes and settlements |
| Characters | Eight named speaking NPCs; shared rigs with distinct outfits and profiles | More townsfolk, a recruitable companion, deeper routines |
| Combat | One melee weapon family, block, dodge, two enemy behaviors | Bow, spells, additional weapons and enemies |
| Inventory | 16 slots, equipment, consumables, gold, protected quest items; 12 item definitions | Crafting, equipment variety, larger catalogs |
| Quests | One main quest with combat and negotiated resolutions; two short side quests | More quest arcs and constrained generated favors |
| Progression | Levels 1–3 and one perk choice per level-up | More levels and specializations |
| Conversation | Chat, contextual facts, trade quotes, bounded haggling, quest offers, directions, roleplay | More social actions and long-term memory |
| Presentation | Coherent art chosen by an asset pilot, readable animation, essential effects and ambient audio | Voice, weather, cinematic polish |

The adventure duration is a playtest target, not an estimated development duration.
“Open world” means freely choosing routes and activities within this region. Density,
reactivity and a complete loop matter more than additional square kilometers.

All speaking NPCs must be addressable, including guards and the human camp leader
when not actively attacking. They have different abilities: a farmer may chat and
give directions but cannot sell the blacksmith's stock.

## Build sequence

These are game milestones, separate from the framework's numbered phases. Complete
one playable increment at a time; turn each row into small implementation tasks
when work starts. No delivery dates are promised before deadline and capacity are known.

| Milestone | Concrete work | Exit check | Framework dependency |
|---|---|---|---|
| **G0 — Lock the first slice** | Resolve pending questions; assign project identity and scene ownership; freeze eight NPCs, 12 items and quest state diagram | Reviewed content list, session route and acceptance checklist; scope has explicit cuts | None |
| **G1 — Walk and talk** | Create pinned Unity project; third-person movement/camera; one village block; NPC focus; chat overlay; delayed scripted brain | Built Windows player can approach two NPCs, submit chat, cancel, and switch speakers without cross-talk | None for decisions; package reference only |
| **G2 — Fight and collect** | Attack/block/dodge, health/stamina, two enemy behaviors, inventory/equipment, loot, respawn | Player defeats one encounter, equips loot and survives a defeat without losing a quest item | None |
| **G3 — Trade through chat** | Catalog, quotes, sale offers, inline confirmation, stock/gold/capacity checks; scripted negotiation | Buy and sell in chat; replaying a confirmation cannot duplicate gold/items; expired or unaffordable quotes do nothing | Existing registries can be exercised with fake decisions |
| **G4 — Finish an adventure** | Full region, eight NPCs, main quest branches, two side quests, XP/perks, reputation, basic routines and save/load | A tester completes combat and negotiation routes in separate saves, levels up, reloads and sees consistent results | None; scripted brain remains clearly labeled |
| **G5 — Connect real decisions** | Replace the conversation adapter with public Agent API; profiles, sources, registered handlers, cancellation and telemetry | At least three NPC roles select and execute real actions; greeting, unavailable trade, absent target and stale request cases are contained | Framework Phases 1–3; exact public signatures verified then |
| **G6 — Validate improvisation** | Add role-specific context and flexible supported favors; run held-out multi-turn scenarios; tune UX and context budgets | Full adventure works in model mode; factual contradictions and unintended actions are logged separately; game state remains valid | Framework Phase 4 instrument; no bypass of evaluation work |
| **G6a — Generated quest experiment** | Opt-in deliver/gather/recover contract generator over real content; solvability/reward budget checks, seed/version persistence | Five generated instances pass completion, abandonment and reload checks; authored quest remains unaffected | G6; templates/contracts are game-owned |
| **G7 — Replace and polish assets** | Pilot generated props/audio; replace grey box incrementally; animation, lighting, accessibility and performance pass | Entire route readable; generated assets pass import/visual checks; performance measured with runtime model resident | G6 validated; ComfyUI optional |
| **G8 — Package the showcase** | Resettable presentation saves, debug overlay, player build, instructions, clean-machine check | Showcase route works from fresh save; provider failures recover; distributable build's runtime requirements are explicit | Phase 6b required for self-contained local inference |

G1–G4 provide a real playable grey box while the framework is unfinished. Scripted
mode is a development fixture and must never be presented as model-driven behavior.
G5 can happen after Phase 3; G6 precedes major polish so game work does not displace
the framework's evaluation instrument (DR-006).

An Ollama-dependent development build is acceptable for internal demonstrations.
A build advertised as self-contained must meet Phase 6b: real decisions in the
player executable without Ollama or a network. Text-only play is the baseline.

## First implementation tasks

Close G0, then implement G1 alone, following the [Unity project checklist](../../Demos/README.md).

1. Create `Demos/LivingVale/` with Unity **6000.3.23f1**, proposed Universal 3D
   pipeline, and the framework's relative `file:` reference.
2. Create the game's runtime and Editor test assemblies, a bootstrap scene and a
   tiny village test scene. Let Unity generate each file/folder's `.meta`.
3. Add movement, camera collision, one interaction key, speaker focus and chat input.
4. Add two NPCs with stable IDs and scripted conversation responses; inject normal,
   delayed, timeout and malformed responses through the same adapter boundary.
5. Verify input focus: typing does not swing a weapon, move the player, or switch NPCs.
6. Build and manually run the Windows player. Stop at the G1 exit check before adding
   inventories, the second village or generated assets.

The future implementation agent must read this plan and its linked design, inspect
the current public framework API, and target the correct Unity instance before
editing. The class names in the design are proposals, not existing APIs.

## Acceptance checks for the complete first adventure

| Check | Evidence required |
|---|---|
| Playability | Fresh player build completes a 20–30 minute route; exploration remains possible afterward |
| RPG systems | Movement, readable combat, inventory, equip/use, quest progress, trade and at least one level-up work together |
| Everyone is approachable | Every named speaking NPC has chat, role knowledge and a coherent unavailable-state response |
| Conversation has consequences | A purchase, a negotiated or combat quest resolution, and one social change produce visible deterministic effects |
| Improvisation | A tester uses novel wording and follows up across turns; supported requests have actual effects, unsupported requests are explained |
| Transaction integrity | No negative balances, duplication, partial transfers, repeated rewards or stale-session effects in failure tests |
| Persistence | Completed quest rewards, stocks, XP, hostility/reputation and player state survive a reload |
| Failure handling | Timeout, cancellation, speaker departure and provider failure cause no world mutation and leave a usable interface |
| Performance | Frame times, request latency and VRAM measured on named hardware with runtime inference active |
| Framework evidence | Held-out scenarios, execution receipts and telemetry; any accuracy comparison has a same-session control arm |

Use meaningful EditMode tests for inventory transactions, quest transitions, rewards,
XP and stale/canceled requests. Use PlayMode/player checks for input, navigation,
combat, scene references and actual model integration. Do not infer compilation from
a green hygiene check.

## Worktree and shared-machine protocol

Use a separate managed worktree from `test` and a `codex/` branch for demo work
when other sessions may edit the repository. Worktrees isolate tracked source and
project files; they share the Git repository and the machine's GPU, Docker services
and editor endpoints.

Before opening Unity, verify the absolute worktree project path and editor version.
One session owns a scene or prefab at a time. Target Unity MCP by verified project
path/instance, never merely “the active editor.” Do not open, close, reconnect or
modify another session's editor. Coordinate GPU use before unloading anyone's model,
restarting a shared container or measuring inference. New worktrees have their own
imports and `Library/`; import only the required game and fetch its LFS assets.

Keep framework changes in `UnityProject/` on a separately scoped task, and verify
all demos when its public API changes. PRs target **`test`**. Commit only explicit
game/docs paths, preserve existing meta GUIDs, and require clean console, passing
EditMode tests and green hygiene before merge.

## Pending decisions and cut order

Still open: final title;
scale of the generated-quest experiment; visual style after the pilot; exact hardware
and available asset/rig sources. This is separate from `CompanionRPG` (#45). Windows-only support, protected essential characters
and no fixed deadline are confirmed. The design contains recommended
defaults and marks them as provisional. No other demo is canceled by this proposal;
`GreyBoxStrategy` remains necessary to prove agents can exist without a scene.

If time is tight, cut voice, weather, castle interiors, crafting and extra content
first. Shrink village geometry and cosmetic NPC routines next. Preserve chat-driven
trade, one coherent quest with alternatives, basic combat/inventory/leveling, save/load
and execution validation. Further cuts that remove confirmed requirements need a
scope discussion.

## Revision log

| Date | Change |
|---|---|
| 2026-10-07 | Initial draft from user direction; separates pre-framework gameplay, live integration, validation and asset production |
| 2026-10-07 | Confirmed separate demo; refined recovery/access/XP rules; created development tracker #69 under #47 |
