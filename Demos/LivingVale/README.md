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
(within 3 m), Esc closes the chat, **I** opens the inventory. Ask Mara to buy a potion, bread or
herbs, or to sell her a wolf pelt, iron scrap or herbs: her answer puts an exact offer on a card
in the chat, and only its button moves gold and items. Walking more than 6 m away also closes it. F1–F5 switch the
scripted replies between normal, slow, never answering, malformed and throwing, so the failure
paths can be tried by hand.

Replies come from the local model through Agenerela by default (Ollama at `127.0.0.1:11434`,
model set on `ValeBootstrap`); **F6** switches between the model and scripted replies.
With Ollama unreachable, a turn fails with a game-authored line and the game stays playable.

**Combat (G2):** left mouse attacks, right mouse blocks, Space dodges. A raider and a wolf wait in
the meadow south of the village. Combat input is ignored while typing, while a chat is open and
while the pointer is over UI. Taking damage closes the open chat, and nobody will talk while an
enemy is fighting you. When you fall you respawn where you started, and the enemies that were
fighting you go home at full health; a defeated enemy stays down and drops its loot once
(`item_wolf_pelt`, `item_iron_scrap`) straight into your inventory, once per enemy; a defeat
costs a tenth of your gold, never items. `CombatInventoryBridge` joins the two.

## Layout

| Folder | What | Scene? |
|---|---|---|
| `Assets/LivingVale/Game/` | Game state: `ItemCatalog`, `PlayerInventory` (also the `ILootSink`), `VendorStock`, `TradeService` and its quotes and receipts, and `NpcActionExecutor`, which every driver executes through | No: plain C#, EditMode-tested |
| `Assets/LivingVale/Interaction/` | Conversations: `ConversationCoordinator`, the `INpcDecisionDriver` boundary, `ScriptedNpcDecisionDriver`, and `ConversationTradeLink`, which ties quotes to the open conversation | No: plain C#, EditMode-tested |
| `Assets/LivingVale/Combat/` | Combat: `Combatant` and `EnemyBrain` rules (plain C#, EditMode-tested), the player's and enemies' behaviours, `CombatDirector` (chat, loot and respawn hooks: `ILootSink`, `IDefeatPenalty`) and the HUD | Rules no; behaviours yes |
| `Assets/LivingVale/Unity/` | Bootstrap, player, orbit camera, NPC speakers, focus and range | Yes |
| `Assets/LivingVale/UI/` | The chat overlay with its inline trade card, and the inventory panel, built from code with uGUI | Yes |
| `Assets/LivingVale/Agenerela/` | Real NPC decisions: `AgenerelaNpcDecisionDriver` (one framework `Agent` per NPC, personas in `NpcPersona.cs`) and `SwitchableNpcDecisionDriver`, behind F6 | No: plain C#, EditMode-tested with a fake provider |
| `Assets/LivingVale/Editor/` | `VillageSceneBuilder` (which calls `CombatSceneBuilder`) and `LivingValeBuild`, also runnable in batch mode | Editor only |
| `Assets/LivingVale/Tests/Editor/` | EditMode tests | — |

NPC replies use Agenerela's `Agent` by default; the overlay's badge says which driver is
selected, so a scripted reply is never mistaken for a model's. The scene is generated: change
`VillageSceneBuilder` and rebuild rather than editing `Village.unity` by hand.

## Trading (G3)

A decision never moves gold or items. `quote_purchase` names a stock entry
(`stock_mara_potion_one`) and `quote_sale` a sale bundle (`sale_mara_wolf_pelt_one`); each target
is one exact item and quantity, because a decision has no quantity field. The executor turns the
pair into a `TradeQuote` tied to the conversation's generation and to the inventory's and stock's
revisions, and the card shows it. Confirming calls `TradeService.Confirm` directly, never the
model, and anything that changed since the quote (the conversation, the holdings, the stock, the
clock) refuses it: a new price needs a new card. `NpcActionExecutor.AvailableFor(npcId)` lists the
actions with at least one eligible target, which is what the G5 driver registers with its `Agent`.

Both drivers trade through that one executor. The `Agent` driver registers `quote_purchase` and
`quote_sale` through `TradeActionBinding`, asking `AvailableFor` again on every turn, so an action
with nothing to offer is absent from the schema. Because a local model never reads the schema,
the actions and their target ids are also written into the prompt as observations. A guard turns
a quote naming the other action's target into `none`, and `Agent.Execute` refuses a target that
went stale before the reply was executed. Each turn also tells the model about its offer still
waiting for the player and the receipt of the last trade. An NPC remembers a reply only once it has been executed
(`CommittedHistory`), so a reply dropped as late is never part of its memory.
