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

## Layout

| Folder | What | Scene? |
|---|---|---|
| `Assets/LivingVale/Game/` | Game state: `ItemCatalog`, `PlayerInventory` (also the `ILootSink`), `VendorStock`, `TradeService` and its quotes and receipts, and `NpcActionExecutor`, which every driver executes through | No: plain C#, EditMode-tested |
| `Assets/LivingVale/Interaction/` | Conversations: `ConversationCoordinator`, the `INpcDecisionDriver` boundary, `ScriptedNpcDecisionDriver`, and `ConversationTradeLink`, which ties quotes to the open conversation | No: plain C#, EditMode-tested |
| `Assets/LivingVale/Unity/` | Bootstrap, player, orbit camera, NPC speakers, focus and range | Yes |
| `Assets/LivingVale/UI/` | The chat overlay with its inline trade card, and the inventory panel, built from code with uGUI | Yes |
| `Assets/LivingVale/Editor/` | `VillageSceneBuilder` and `LivingValeBuild`, also runnable in batch mode | Editor only |
| `Assets/LivingVale/Tests/Editor/` | EditMode tests | — |

NPC replies are **scripted** until milestone G5 swaps in a driver over the framework's
`Agent`; the overlay's badge says which driver is answering, so a scripted reply is never
mistaken for a model's. The scene is generated: change `VillageSceneBuilder` and rebuild rather
than editing `Village.unity` by hand.

## Trading (G3)

A decision never moves gold or items. `quote_purchase` names a stock entry
(`stock_mara_potion_one`) and `quote_sale` a sale bundle (`sale_mara_wolf_pelt_one`); each target
is one exact item and quantity, because a decision has no quantity field. The executor turns the
pair into a `TradeQuote` tied to the conversation's generation and to the inventory's and stock's
revisions, and the card shows it. Confirming calls `TradeService.Confirm` directly, never the
model, and anything that changed since the quote (the conversation, the holdings, the stock, the
clock) refuses it: a new price needs a new card. `NpcActionExecutor.AvailableFor(npcId)` lists the
actions with at least one eligible target, which is what the G5 driver registers with its `Agent`.
