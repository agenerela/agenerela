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

Replies come from the local model through Agenerela by default (Ollama at `127.0.0.1:11434`).
The model is set on `ValeBootstrap`, and since 5 November it defaults to `gemma4:e2b-it-qat`,
which wrote best in the blind comparison (llm-wiki findings). Pull it with
`ollama pull gemma4:e2b-it-qat`; without it the notice says it did not load, and F7 reaches
`qwen3.5:2b` and the rest of the list. **F6** switches between the model and scripted replies, and **F7**
moves the model to the next one in `ValeBootstrap`'s list that Ollama has, closing the open chat
first and keeping what each NPC remembers. A model Ollama lacks is skipped, and the badge says so.
With Ollama unreachable, a turn fails with a game-authored line and the game stays playable.

**Combat (G2):** left mouse attacks, right mouse blocks, Space dodges. A raider and a wolf wait in
the meadow south of the village. Combat input is ignored while typing, while a chat is open and
while the pointer is over UI. Taking damage closes the open chat, and nobody will talk while an
enemy is fighting you. When you fall you respawn where you started, and the enemies that were
fighting you go home at full health; a defeated enemy stays down and drops its loot once
(`item_wolf_pelt`, `item_iron_scrap`) straight into your inventory, once per enemy; a defeat
costs a tenth of your gold, never items. `CombatInventoryBridge` joins the two.

**Quests and saves (G4, part 1):** ask Iven about the wagon or the medicine and he offers *The
Missing Wagon* on a card; only its Accept button starts the quest. The inventory panel shows your
level, XP and the quest's state. **F9** saves and **F10** loads (closing any open chat first); the
save is `livingvale_save.json` under `Application.persistentDataPath`, and the save before it is
kept as `livingvale_save.bak.json`.

**The region (G4b):** paths lead out of the village to the Old Road (south), Herb Hollow (west),
and on to Reedbank, Ashwatch Castle and Broken Watch; the HUD names the place you are in, and the
first visit to each pays 10 XP once, saved with the rest. Sella stands at the inn in the Glen, Toma
and Fen at Reedbank, Brann and Elra at Ashwatch's gate, Kest at Broken Watch. They walk short
routines and stop to face you while you talk. They only talk for now; their quest actions come
with the quest routes. At walking pace each neighbour is 45 to 90 seconds away.

**Quest routes and endings (G4c):** **E** also uses quest objects when no NPC is in front of
you. At the Old Road you can investigate the wagon wreck (a clue and 10 XP) and take Brann's lost
badge, Herb Hollow has three marked herbs, and the medical parcel at Broken Watch can only be taken
once its three camp defenders are down. There are three ways to recover the parcel: fight for it,
pay Kest 20 gold or have Iven's herb favour done, or tell Kest what Fen saw, which works only after
Fen has actually told you. You then deliver it to Iven, or to Elra once Brann has given you a
delivery permit, or divide it after both have agreed on their own cards. Every deal and hand-over
shows a card in the chat, and only its button acts. When the quest is resolved, the patients leave
Iven's porch, the castle gate opens, or one patient leaves and supplies arrive at the gate. The
journal at the top right follows the quest. Fen, Kest, Brann and Elra stand in the region built
by G4b; until that is merged, their actions can be reached only from the tests.

## Layout

| Folder | What | Scene? |
|---|---|---|
| `Assets/LivingVale/Game/` | Game state: `ItemCatalog`, `PlayerInventory` (also the `ILootSink`), `VendorStock`, `TradeService` and its quotes and receipts, `QuestLog`, `Progression`, `SaveGame`, and `NpcActionExecutor`, which every driver executes through | No: plain C#, EditMode-tested |
| `Assets/LivingVale/Interaction/` | Conversations: `ConversationCoordinator`, the `INpcDecisionDriver` boundary, `ScriptedNpcDecisionDriver`, and `ConversationTradeLink`, which ties quotes to the open conversation | No: plain C#, EditMode-tested |
| `Assets/LivingVale/Combat/` | Combat: `Combatant` and `EnemyBrain` rules (plain C#, EditMode-tested), the player's and enemies' behaviours, `CombatDirector` (chat, loot and respawn hooks: `ILootSink`, `IDefeatPenalty`) and the HUD | Rules no; behaviours yes |
| `Assets/LivingVale/World/` | The region's rules: `NpcRoutine` (walk and idle), `PlaceDiscovery` (first visits, paid through `Progression`), and `RegionNpcs`, the six region NPCs' IDs, places, routines and colours | No: plain C#, EditMode-tested |
| `Assets/LivingVale/Unity/` | Bootstrap, player, orbit camera, NPC speakers, focus and range, and `NpcRoutineMover` | Yes |
| `Assets/LivingVale/UI/` | The chat overlay with its inline trade card, the inventory panel and the place HUD, built from code with uGUI | Yes |
| `Assets/LivingVale/Agenerela/` | Real NPC decisions: `AgenerelaNpcDecisionDriver` (one framework `Agent` per NPC, personas in `NpcPersona.cs` and `RegionNpcPersonas.cs`) and `SwitchableNpcDecisionDriver`, behind F6 | No: plain C#, EditMode-tested with a fake provider |
| `Assets/LivingVale/Editor/` | `VillageSceneBuilder` (which calls `CombatSceneBuilder` and `Region/RegionBuilder`, one builder per place) and `LivingValeBuild`, also runnable in batch mode | Editor only |
| `Assets/LivingVale/Tests/Editor/` | EditMode tests | — |

NPC replies use Agenerela's `Agent` by default; the overlay's badge says which driver is
selected, so a scripted reply is never mistaken for a model's. The scene is generated: change
`VillageSceneBuilder` and rebuild rather than editing `Village.unity` by hand.

## Art kit (G7a pilot)

A low-poly, flat-shaded kit of 29 models, every one generated by our own Blender scripts in
`Art~/blender/` and sharing one 64×64 palette texture and one URP material: two cottages, the
market stall, well, fences, crates, barrels, sacks, a signpost, the intact and the wrecked wagon,
two trees, two rocks, feverleaf, Ashwatch's gate and wall, Broken Watch's ruined wall, tent and
campfire, one humanoid body in five outfits, a sword and a wolf. The scene builders place them
through `ArtKit` (`Assets/LivingVale/Editor/Art/`) and keep their primitives as the fallback;
[`ART.md`](ART.md) has the rebuild command, the style rules, the triangle counts and the API.

![The kit, front three-quarter view](Art~/previews/contact_sheet.png)

![Characters: player, villager, healer, raider, guard, wolf and sword](Art~/previews/row_characters_front.png)

![Buildings: two cottages, the market stall and the well](Art~/previews/row_buildings_front.png)

![The same buildings imported into Unity, URP, in the Art Preview scene](Art~/previews/unity_buildings.png)

## Quests, progression and saves (G4)

`QuestLog` owns the main quest `quest_missing_wagon` and the side quests `quest_herbs` and
`quest_badge`. States move only through methods that check the current state (`AcceptOffer`,
`TryRecoverParcel`, `TryResolve`, `TryCompleteHerbs`, …); clues and agreements are recorded apart
from the state with `RecordClue`, and never advance it. Each step that pays goes through
`PlayerInventory.TryApplyQuestEvent` and `Progression.TryAward` with a fixed event ID, so a replay,
a double click or a reloaded save cannot pay twice. The parcel and badge live in protected quest
storage. `Progression` holds XP (levels at 100 and 250) and one perk per level-up (Hardy,
Skirmisher, Trusted Face); the perks' effects are not wired into combat or bargaining yet.

`offer_quest` is the first non-trade NPC action: `NpcActionExecutor` lists it for Iven while the
quest is Undiscovered or Offered, executing it moves Undiscovered to Offered and puts a
`QuestOffer` on the chat card, and the card's Accept calls `QuestLog.AcceptOffer`. The scripted
driver offers it on wagon, medicine, parcel or help lines; the `Agent` driver registers it through
`QuestActionBinding`, with its description, target and quest status in the observations.

`SaveGame` captures inventory, equipment, gold, vendor stock, quests and clues, XP and perks, and
both applied-event sets into a versioned `SaveSnapshot`, written to a temporary file and swapped in
with `File.Replace`. Loading validates the whole snapshot before restoring anything, so a corrupt
or future-version save changes nothing. Quotes, offers and requests are never saved; loading
invalidates the pending ones.

## Quest routes, endings and side quests (G4c)

`QuestRoutes` (in `Game/`) turns `QuestLog`'s steps into authored contracts that NPCs act on,
always through `NpcActionExecutor`, from either driver:

| Action | NPC → targets | Effect |
|---|---|---|
| `share_information` | Fen → `knowledge_fen_raid` | Records `clue_fen_testimony` and `clue_camp_located` at once |
| `offer_exchange` | Kest → `exchange_kest_payment` (20 gold), `exchange_kest_favour` (herb quest done), `exchange_kest_testimony` (Fen's clue recorded); Iven and Elra → `exchange_division_*` | A card. Confirming recovers the parcel (negotiation or investigation) or records that party's agreement |
| `resolve_delivery` | Iven → `delivery_parcel_iven`, `delivery_division_iven`, `delivery_herbs_iven`; Elra → `delivery_parcel_elra`, `delivery_division_elra` (both need the permit); Brann → `delivery_badge_brann` | A card. Confirming resolves the quest (healer, garrison, division) or hands a side quest in |
| `grant_access` | Brann → `access_delivery_permit`, once the parcel is recovered | Sets the permit at once |
| `offer_quest` | Iven → `offer_iven_herbs`; Brann → `offer_brann_badge` | A card; Accept starts the side quest |

A target is listed only while its conditions hold. "I have proof" therefore reaches Kest's
testimony deal only after Fen has told it, and `Confirm` checks every condition again. Claims in
chat record nothing. A route card belongs to the conversation it was made in, and opening,
closing or loading takes it away.
The `Agent` driver registers the four new actions through `RouteActionBinding`. That writes each
action, its targets and their terms into the prompt, plus status lines that say what the game has
verified (for Kest: whether a witness account exists). The side-quest offers join G4a's
`offer_quest`. The scripted driver's rules are in `ScriptedNpcDecisionDriver.Routes.cs`.

World pickups (`QuestPickupPoint`, used with E through `QuestWorldInteraction`) call
`QuestRoutes.TryPickUp`. Herbs and the badge are inventory event IDs (`pickup_herb_1`…), and the
wreck, the cleared camp and the permit are clue IDs, so saves carry all of them and nothing pays
twice. `QuestWorldView` reports defeated defenders (`enemy_camp_defender_1`–`3`) and switches the
ending's objects. `QuestSceneBuilder` places all of it, called in one line from the village build.

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
(`CommittedHistory`), so a reply dropped as late is never part of its memory. The few-shot block also shows one line
answered with `none` (`AgenerelaNpcDecisionDriver.DefaultIdleExample`). In G5f's A/B, that alone
stopped Mara making offers nobody asked for, on both models measured.
