# GreyBoxVillage

Standalone Unity **6000.3.23f1**, Universal 3D (URP) project for
[issue #19](https://github.com/agenerela/agenerela/issues/19): village agents driven by a local
model through the framework, in the world the evaluation set is written for. In game it is
**Ashford**, a walled village of four model-driven agents: Bram at the gate and Wren at the
bridge (#143), Odile the merchant and Captain Hale (#153). It has low-poly art, a day and night
cycle, quests and an inventory, and the framework view (Tab) shows every decision being made.
The villagers keep a daily routine and the weather turns, and both reach the agents as
observations ([A day in Ashford](#a-day-in-ashford)). Presenter keys stage four situations that
chain one agent's decision into another's: the wolf, a thief, a fire and night at the gate. A
five-minute script for showing it is at the end
([Presenting it](#presenting-it-a-five-minute-script)).

## Open and run

Start Ollama with `qwen3.5:2b` pulled (`ollama pull qwen3.5:2b`). Open this folder in Unity
Hub, or with `unity open Demos/GreyBoxVillage`. Open `Assets/GreyBoxVillage/GreyBoxVillage.unity`
and enter Play mode.

Or start from `LocalShared/DemoLauncher.unity`, the title screen.

| Key | Does |
|---|---|
| WASD or arrows (left stick) | Walk, relative to the camera; **Shift** runs |
| Right mouse drag (right stick), wheel | Orbit the camera, zoom |
| **E** or **Enter** | Pick up what is within reach, or talk to whoever is within 6 m |
| Enter / Esc while typing | Send the line / go back to walking |
| **I**, **J** | Your pack, your quest journal |
| **Tab** | The framework view: every agent's last request, the queue, and the target id over every place |
| **H** | The request history: every decision, with exactly what was sent and what came back |
| **Esc** | The menu: the framework view, how fast the day runs, the controls, restart, the title |
| **F1** to **F6** | Presenter keys: reset the village, the wolf, the thief, a fire, jump to dusk, change the weather ([The situations](#the-situations)) |
| **F12** | Show or hide the presenter panel, which lists those keys |

- **Talking:** the dialogue panel opens when someone is in earshot. Type anything, or pick a
  suggestion; the nearest agent hears it. Their reply shows above their head, amber when a game
  guard stopped what they chose.
- The first request after Ollama starts can time out while the model loads (30 s); the overlay
  shows the typed `ProviderTimeout`. Say it again.
- **With no model,** because Ollama is stopped or the model is not pulled, the village stays
  playable. A red banner at the top of the HUD says what is wrong and what to do, and the agent
  you asked says "No answer" in a red bubble rather than thinking forever. Until the model
  answers again, the agents stop asking it on their own (a quiet moment, a trespass, the wolf, the
  bell) for 20 s at a time (`VillageCommands.ModelRetrySeconds`), so the console gets one
  warning, not one per agent every few seconds. Your own lines are always sent, and the first
  answer clears the banner.
- **Play mode leaves no trace.** The day and night cycle writes only to copies of the palette and
  the sky that it makes when Play starts, so pressing Play changes no file. The game keeps no
  static state that a play session changes (a test checks every static field), so it plays the
  same with Enter Play Mode Options set to skip the domain reload.
- The model and endpoint are in `Assets/GreyBoxVillage/Agents/VillageOllama.asset`, the one
  provider config both guards ask.

## The world

The map is the evaluation agent's world (`UnityProject/Assets/Evaluation/agent.json`): every one
of its eleven targets is a `Targetable` here, so a prompt from the set can be played and a
surprise in play can become a prompt. The village sits inside its wall south of a river; the
north bank holds the tower, the lord's keep, the woods and a locked cave.

| Target id | Where |
|---|---|
| `village_gate` | The gate in the south wall, where the guard starts |
| `well`, `market` | The village square and its stalls |
| `campfire`, `training_dummy` | The guards' camp by the gate, with the alarm bell |
| `shelter` | Where villagers run when the alarm rings |
| `eastern_road` | The road out through the east wall |
| `bridge`, `docks` | The only crossing, and the docks on the south bank |
| `north_tower` | Across the river |
| `wolf` | Roams the woods and sometimes prowls toward the village |
| `bram`, `wren`, `odile`, `hale` | The four agents, so each can name the others |
| `tomas`, `ada`, `pell`, `mira` | The villagers, who keep a daily routine ([A day in Ashford](#a-day-in-ashford)) |
| `thief` | A hooded thief, category `stranger`, out only after **F3** |
| `armory`, `lords_keep`, `locked_cave` | **Off limits** to the player: red zones |

`training_dummy`, `armory`, `lords_keep`, the people (category `person`) and Odile's wares
(category `item`: `bread`, `apple`, `rope`, `torch`, `lantern`) are the demo's own; the rest are
the evaluation world's, with their ids unchanged. An agent never names itself. The guards'
target sources keep to places, people, creatures, restricted zones and the thief while it is out,
so Odile's wares never reach Bram's schema, and every evaluation target stays under his cap of 24 (a test checks). The scene is generated by
`Tools > Demos > GreyBoxVillage > Rebuild Village` (`Editor/VillageSceneBuilder.cs`), which
replaces the scene's contents in place, keeping its GUID, and bakes navigation.

## The look

Everything you see is built by scripts, so it can be rebuilt and changed in code review:

- **`ArtSource/build_kit.py`** builds the low-poly kit in Blender (5.x, headless: `blender
  --background --factory-startup --python Demos/GreyBoxVillage/ArtSource/build_kit.py`, about
  3 s). That is 77 FBX models, plus `Art/Palette.png` and `Art/PaletteEmission.png`: every face is
  UV-mapped to one swatch of one palette, so the whole village is one material.
  - The models: buildings, props and trees, the characters, the wolf, the items, the terrain and
    the water. The gate's doors are their own model, `gate_door`, hung in the scene so the night
    watch can shut them, and `bucket` is what a guard carries to a fire.
  - An FBX export stamps the time it was made, so a full run rewrites every file in a few bytes.
    `KIT_ONLY=gate_house,gate_door blender ...` writes only the models named.
  - Characters are separate parts at their joints. `SegmentedFigure` animates their walk, idle
    and talking with no rig.
- **`ArtSource/render_icons.py`** renders the inventory icons (`Resources/Icons`) and the
  dialogue portraits (`Resources/Portraits`) from the same models, lit from the viewer's side by
  a key and a fill light, with the sun as a rim, so no face is in its own shadow.
- **`Editor/VillageArtImporter.cs`** imports the kit with the palette material already on every
  model (the water gets its own), so a model placed in the scene overrides nothing.
- **`ArtSource/preview_kit.py`** renders contact sheets of the kit for review.
- **The scene builder** (`Editor/VillageSceneBuilder.Art.cs`) keeps every grey box exactly
  where it was, as an invisible collider. The NavMesh is baked from those boxes, so every path,
  place and line of sight the tests check is unchanged, and the models are visual-only children
  of `VillageArt`.
  - It also adds the sky, fog, URP post-processing, water (`Art/Shaders/VillageWater.shader`),
    lamps, campfire and torches, chimney smoke and fireflies. Fire, smoke and fireflies are
    prefabs under `Art/Effects`, written once per rebuild, so the scene stores only where each
    one stands.
  - The grass, flowers, bushes, rocks, reeds and the trees of the woods, 880 copies of 15
    models, are not GameObjects. A **`ScatterField`** stores where each copy stands, its turn and
    its size, and draws them with GPU instancing, one call per mesh per 120 m cell, culled by each
    cell's bounds. They have no colliders, like every model. The scene file went from 4.96 MB to
    about 1.2 MB. The bushes, flowers and grass inside the walls were never placed before: the
    builder's check for buildings in the way always found the ground.
  - **`DayNightCycle`** turns all of that through a day: ten minutes by default, changeable in
    the menu. Windows and lamps glow at night.
- **`Editor/VillageCapture.cs`** renders fixed views to PNGs with no window: `Unity -batchmode
  -projectPath Demos/GreyBoxVillage -executeMethod
  Demos.GreyBoxVillage.Editor.VillageCapture.CaptureFromCommandLine -captureOut <dir>
  [-captureRebuild] -quit`, without `-nographics`.
  - **With the UI**, `...VillageCapture.CaptureUiFromCommandLine -captureOut <dir>
    [-captureSizes 1280x720,1920x1080]`, with no `-quit`: it enters Play mode, and captures the
    HUD, the dialogue panel, a speech bubble, an amber refusal, the framework view, the pack, the
    journal, the menu and its controls, and the no-model warning, by day and at night, then the
    title screen. The request history (H) is IMGUI, which a window-less editor does not draw, so
    it is not captured. The
    panels draw into a RenderTexture (`PanelSettings.targetTexture`), laid over the camera's. The
    agents answer from a scripted provider, never a model. `Tools > GreyBoxVillage > Capture the
    UI` does the same from the editor, into `Logs/captures`.
  - `...VillageCapture.MeasureFromCommandLine -captureOut <dir>`, also with no `-quit`, holds the
    camera on three fixed views at 1920x1080 and writes the frame time and draw calls of each to
    `frame-time.txt`.
  - `...VillageCapture.TitleBackdropFromCommandLine -quit` renders the title screen's backdrop,
    `Resources/TitleBackdrop.jpg`, the village at dusk.
  - `...VillageCapture.CaptureSituationsFromCommandLine -captureOut <dir>`, with no `-quit`, plays
    the presenter keys in Play mode, with the agents answering from the presenter script's replies,
    and captures the wolf at the gate, the thief chased and caught, the fire and its end, and the
    shut gate at night, opened. `CaptureUiFromCommandLine` takes them too, after the UI views.
  - `...VillageCapture.CaptureExploreFromCommandLine -captureOut <dir>`, with no `-quit`, captures
    the villagers' routine at four hours (the morning's work, midday, dusk and night) and the
    village in each weather, in Play mode.
  - `...VillageCapture.PresenterLiveFromCommandLine -captureOut <dir>`, with no `-quit`, plays the
    same script with the real model and writes `live-run.md`, every decision with its guard and
    latency, and `live-run.jsonl`. Take the shared Ollama lock first.

No asset in this game comes from the asset server, so there is no `GENERATED.md` yet.

## A day in Ashford

### The villagers' routine

Tomas, Ada, Pell and Mira are scripted villagers, not agents: each a `Villager`, with the day in
`VillageRoutine`. Through `DayNightCycle`'s day (ten minutes by default) each goes:

| Hours | Where |
|---|---|
| 05:45 to 07:00 | The well |
| 07:00 to 11:00 | Work: Tomas and Pell to the fields outside the gate, Ada and Mira to the market |
| 11:00 to 14:00 | The square |
| 14:00 to 17:00 | Work again |
| 17:00 to 19:30 | The campfire in the guards' camp |
| 19:30 to 05:45 | Home: each walks to its cottage door and goes in, and the lamp by its door comes on |

- **The bell** sends them running for the shelter, from indoors too. When it stops, each goes back
  to where the hour has it.
- **F1** puts them back where Play started, and they set off for the morning's work. **F5** puts
  them straight at the campfire, as dusk has them, so the field workers are not left outside the
  gate it shuts.
- **Indoors** a villager is out of sight but still a person the guards can name, so Bram's targets
  never change with the hour (a test checks).
- **Every agent is told who is where,** in one line of its observations, read from where the
  villagers stand: "In the village: the market is busy (ada, mira), the square is empty, tomas and
  pell are working the fields outside the gate." At night: "In the village: everyone is at home for
  the night, and the streets are empty." While the bell rings: "In the village: the villagers are
  taking cover in the shelter."
- **The agents keep their posts,** as before: only Wren's night watch and Bram's evening at the
  campfire move them.

### Weather

`VillageWeather` turns through clear, overcast, rain and fog: by itself every five game hours,
about every two minutes at the normal day length, and on **F6**. The look blends in over a few
seconds: rain falls around the camera, wet ground and roofs darken, mist drifts low in fog, and the
sky, the sun and the fog colour grey over. Like the day and night cycle, it writes only to the
runtime copies of the palette and the sky, so a rainy session changes no file.

It changes what the agents notice, and nothing else:

| Weather | How far a guard sees | Every agent is told |
|---|---|---|
| Clear | 32 m, 26 m for the wolf, 45 m for smoke | Nothing |
| Overcast | The same | "The sky is overcast." |
| Rain | 22 m at most | "It is raining; you can see about 22 m." |
| Fog | 15 m at most | "Fog lies over the village; you can see about 15 m." |

- **The limit is in `GuardSenses.CanSee`,** so it reaches everything a guard watches for: the wolf
  is seen later, a thief lost sooner, and a trespass in fog may go unseen. A wolf 20 m off is
  reported in clear weather and in rain, and in fog only once it comes within 15 m.
- **The weather is never an availability input.** It offers no action and takes none away: in fog a
  guard is offered what it would be offered in sunshine for what it can see. A test checks that for
  Bram, whoever asks.
- **Clear weather adds nothing to the prompt,** so a clear day's requests read as the earlier live
  runs' did, apart from the village's line.
- **Only the running clock changes it,** never a jump in time, so F5 to dusk keeps the weather, and
  F1 brings back clear skies.

## The agents

Each guard is a GameObject with an `AgentBehaviour`, its profile (`Agents/Bram.asset`,
`Agents/Wren.asset`) and the `VillageOllama` provider config. Both ask that one config, so they
share one provider and one queue (DR-013, DR-015): while one guard's request is with the model,
the other's waits, and a player-facing request goes before a background one. Beside it sit the
guard's body (`VillageGuard`, the actions), its mind (`AgentMind`, what it is asked and what
comes of it) and its senses (`GuardSenses`). `VillageCommands` holds what the village shares: who
the player is talking to, the recorder and the board.

| Agent | Post | Patrol | Quiet timer |
|---|---|---|---|
| Bram, gold | `village_gate` | gate, camp, market, bridge, well, shelter, armory | 30 s |
| Wren, silver | `bridge` | bridge, docks, market, well | 37 s, so the two are not asked together |

At night the posts change: Wren keeps the night watch inside the gate, and Bram goes off watch to
the campfire ([Night at the gate](#night-at-the-gate)).

Odile (`VillageMerchant`) stands behind her stall at the market, and Captain Hale
(`VillageCaptain`) before the lord's keep. They have the same `AgentBehaviour` and provider
config, so all four agents share one queue, but no guard senses: they are asked when the player
speaks to them, and when they hear the bell.

## The market, the captain and the quests

**Odile** sells what lies on her counter, each ware a `Targetable` of category `item`. A sold ware
leaves the counter, and so leaves her schema, until it is restocked 90 s later.

| Action | Target | Offered when |
|---|---|---|
| `sell` | a ware | the player asks and something is on the counter |
| `give` | a ware | the same |
| `call_guard` | a guard (`GuardTargetSource`) | a thief runs off with a ware, or a fire burns ([The thief](#the-thief-f3)) |

**Captain Hale** has `send_guard` (target: a place). The nearest guard who is free is then asked
with his own words, labelled `Captain`. One agent's decision becomes another's stimulus, through
the game alone (DR-008).

**Quests.** Three agents give quests, through actions every body has (`VillageBody`):

| Action | Target | Offered when |
|---|---|---|
| `offer_quest` | a quest | the player asks and this agent has a quest left to give |
| `complete_quest` | a quest | the player asks and holds everything for one of this agent's quests |

The quests reach the model as targets through `QuestTargetSource`, a custom `ITargetSource`. A
finished quest leaves the schema. Each giver's observations say where the player stands on it.

| Quest | From | Bring | Reward |
|---|---|---|---|
| `quest_moonpetals` | Odile | 3 moonpetals, which glow in the woods north of the river | 15 coins and a rope |
| `quest_wolf` | Hale | The wolf's pelt. It drops the first time a guard drives the wolf off while the quest is on | 25 coins |
| `quest_lantern` | Wren | Her lantern, lost at the docks | 10 coins |

The player starts with 8 coins (`PlayerInventory`). Things lying in the world are `Pickup`s,
taken with E.

An `AgentBehaviour` builds its agent once, so `AgentMind.UseProvider` replaces the component with
one carrying a new config. That is how the tests give both guards a fake provider.

## When the game asks a guard

The framework never decides when an agent is asked or why (DR-008); this game does, in each
guard's own `GuardSenses` and in `VillageEvents`, and labels each stimulus itself. Each guard runs
one decision at a time: a player line sent while it is deciding is turned away, and an event
waits its turn. The other guard is not held up.

| Moment | Label | Priority | Stimulus |
|---|---|---|---|
| The player says something within 6 m of the nearest guard | `Player` | PlayerFacing | The typed line, verbatim |
| The guard **sees** the player inside a restricted zone | `Event` | PlayerFacing | "The player has walked into the armory, which is off limits." Again after 40 s if still inside |
| The player leaves a zone the guard reacted to | `Event` | PlayerFacing | "The player has left the armory." |
| The guard sees the wolf within 26 m | `Event` | Background | "A wolf has come out of the woods, 21 m from you." At most every 45 s |
| The guard's quiet timer runs out with nothing happening and the guard standing still | `Time` | Background | "Time passes quietly at your post." |
| The bell starts ringing, for every agent within 90 m but the one who rang it | `Event` | Background | "The alarm bell is ringing. Bram rang it." |
| A thief takes a ware from Odile's counter: Odile | `Event` | Background | "A hooded stranger has snatched the bread from your counter and is running for the eastern_road!" |
| A guard sees the thief running, once per theft | `Event` | Background | "A thief is running for the eastern_road with Odile's bread, 20 m from you." |
| Odile calls a guard (`call_guard`): that guard | `Merchant` | Background | "Odile shouts for you from the market: a thief has taken the bread from her counter and is running for the eastern_road. She says: "..."" |
| A fire starts: every guard who sees its smoke, and every agent within 10 m | `Event` | Background | "Smoke is pouring from the bread stall at the market. The stall is on fire." A guard who comes into sight of the smoke later is told then |
| Wren reaches the gate at nightfall | `Event` | Background | "You have reached the village gate and taken the night watch. The gate is shut for the night." |

A zone is a trigger volume (`RestrictedZone`). Entering it is not enough: the guard is asked
only once it can see the player, by a line of sight from its eyes, so a trespass behind the
armory fence can go unnoticed. The zone also checks each physics step that the player still
overlaps it, because Unity sends no `OnTriggerExit` for a collider that is disabled or
teleported out. The quiet timer is how the model shows what it does unprompted: it may choose
`none`, or `patrol`, which nothing else is offered for.

Each guard sees with its own eyes and keeps its own timers, so a trespass Bram cannot see may
still reach Wren. `VillageEvents` is for what everyone in range notices: the bell, and the smoke of a fire.

The observations sent with every request come from the scene and the guard's state: day or night,
the weather when it is not clear, what it is doing, whether it follows the player, how far the
player is and whether it can see them, which zone the player is in, the alarm, the wolf, who is
where in the village, and every place it can name, as `id, distance: description`. A model served by Ollama never reads the schema, so the ids have to be in the
prompt to be chosen deliberately. They already pass the agent's eight-line observation budget, so
the guards' `AgentBehaviour`s leave **Notice Nearby** off: a proximity source would add nothing.

## Actions

`AgentBehaviour` registers the `[AgentAction]` methods on `VillageGuard` (DR-011), and its
proximity target source (90 m, cap 24) registers the places and the people. The cap leaves room
for every evaluation target, which a test checks. The first
four are the evaluation agent's; the rest are this demo's. No example is an evaluation prompt
(hard rule 4).

| Action | Target | What it does | Offered when |
|---|---|---|---|
| `follow_player` | none | Walks after the player, stopping 2.5 m short | the player speaks, and the guard is not following |
| `stop_following` | none | Stops following and holds position | the player speaks, and the guard is following |
| `move_to` | a place | Walks there | always |
| `attack_target` | any | Walks there and strikes; the wolf flees to its den for 60 s, and a running thief is run after and caught | always |
| `warn_player` | none | Stops and faces the player; the statement is the warning | anything but a quiet moment |
| `block_path` | a zone | Stands in the zone's entrance | always |
| `sound_alarm` | none | Rings the bell for 30 s; villagers run to the shelter | the bell is silent, and the player speaks or the guard sees a wolf within 15 m |
| `patrol` | none | Walks the guard's loop (above) | a quiet moment, with the guard not following and not on the night watch |
| `none` | none | Nothing | always |

**Wren has four more,** on components only she carries, so they are never Bram's. Bram is the
evaluation agent, and #153 keeps his actions; a test checks that whatever happens he is offered
nothing outside the table above.

| Action | Component | Target | What it does | Offered when |
|---|---|---|---|---|
| `chase` | `Responder` | the thief or the wolf | Runs after it at 7 m/s; a caught thief drops what it took, the wolf flees | a running thief or the wolf is in sight, but not a wolf already running for its den |
| `fetch_water` | `Responder` | none | Fills a bucket at the well and throws it on the fire, again and again until it is out | a fire burns |
| `open_gate` | `NightWatch` | none | Opens the village gate | she has the night watch, the gate is shut, and the player or the captain asks |
| `close_gate` | `NightWatch` | none | Shuts it | she has the night watch and the gate is open |

Who is asking decides most of that column. A model served by Ollama never reads the schema, so
an action's description never reaches it; what it sees is the ids, one example each, the
observations and the history. Played live, `qwen3.5:2b` answered "Hello" with `patrol`, the
greeting perhaps read as `patrol`'s old example of a calm moment, and a wolf 26 m away with `sound_alarm`.
Wording cannot fix that on Ollama; availability can, because an action that is not offered
cannot be picked. So before each decision `AgentMind` puts the stimulus's source
(`player`, `zone`, `wolf`, `bell`, `quiet`) and the distance of a wolf the guard can see in the agent's
`State`, which the `[Available]` checks on `VillageGuard` read:

- `patrol` fills time when nothing is happening, so only the quiet timer offers it, and anything
  else that asks breaks a patrol off first (`VillageGuard.Attend`). The guard stops to talk, then
  the quiet timer can send it back on its rounds.
- Only the player starts or ends an escort; a wolf, a trespass, the bell or a quiet moment asks
  for neither. Every other action but `sound_alarm` ends one anyway.
- The bell is for a wolf close by (`VillageGuard.AlarmWolfDistance`, 15 m), or for the player,
  who can report a raid or a fire the game does not model.
- A quiet moment has nothing to warn the player about.

What the game cannot know it leaves to the model: "Hello sir" from a following player still
offers `stop_following`, because telling a greeting from "you can go" means reading the text,
which the game does not do (DR-008).

The statement appears above the guard's head. Every answer passes the framework's guards before
it runs, then the game's own:

- **`target_fits_action`**, on every agent: an action aimed at the wrong kind of target, such as
  a quest offered "at the market", is rewritten to `none`. The schema has one target list for all
  actions, so only a guard can pair them. Line: "I don't follow you."
- **`quest_not_ready`**, on every agent: `complete_quest` for a quest whose items the player no
  longer holds (they were dropped while the model thought). Line: "You haven't got it all yet.
  Come back when you do."
- **`cannot_afford`**, on Odile: `sell` of a ware that costs more than the player has. Line:
  "That costs more than you've got, love."
- **`no_free_gifts`**, on Odile: `give` of anything worth more than 2 coins. Line: "I can't give
  that away. An apple, maybe."
- **`never_attack_villagers`**, a `DelegateGuard` on each guard. `attack_target` at a `person`
  (a villager or the other guard) is rewritten to `none`. It is a guard rather than availability
  because it depends on the target the model picks: `attack_target` stays offered for the wolf
  and the training dummy.

`refused` means a guard rejected the answer, inside `DecideAsync` (an action that was not offered,
a target that is not registered) or in `Execute`'s re-check (something stopped being available
while the model thought). `contained` means a guard judged a legal answer wrong, and the guard
does nothing. The agent then says the line `VillageGuard.RefusalLine` gives that guard, through
`Agent.RefusalStatement`, in amber and tagged "stopped by" the guard:

| Guard | Line |
|---|---|
| `never_attack_villagers` | "I won't raise my sword against our own." |
| `TargetGroundingGuard` (a target the model wanted that is not here) | "There's no such thing here." |
| `SchemaLegalityGuard` (the provider broke the contract) | nothing |

The overlay, the board and the history show the model's own answer, the guard that stopped it,
its reason and the line said instead.

## The situations

Four situations make one agent's decision another's stimulus, through the game alone (DR-008).
Each is staged by a presenter key, and each leaves an agent asked within a few seconds. The keys
work while the framework view is open, so the audience can watch every decision, but not while
the player is typing (Esc first) or behind the menu. **F12** shows the panel that lists them, in
the bottom right corner; world labels keep out from under it.

| Key | What it does |
|---|---|
| **F1** | Resets the village: a clear morning, everyone back where Play started (the villagers set off for the morning's work), the bell silent, the thief gone, the fire out, the gate open, the pack and quests as new, and fresh agents with empty memories on the same provider config. A decision still with the model when F1 is pressed is dropped when it comes back |
| **F2** | The wolf comes up the road outside the south gate, 14 m from Bram, close enough for the bell |
| **F3** | A hooded thief slips up to Odile's counter, takes the first ware and runs for the eastern road |
| **F4** | The bread stall, beside Odile's, catches fire |
| **F5** | Jumps to dusk, just as night falls: the gate shuts and the watch changes hands, and the villagers are at the campfire |
| **F6** | The next weather: clear, overcast, rain, fog, and round again. It asks nobody; the next request of every agent says it, and in rain or fog how far the agent can see ([Weather](#weather)) |

### The wolf (F2)

Bram sees the wolf within 15 m, so `sound_alarm` is offered. The bell reaches Wren, Odile and Hale,
who has `send_guard`: the nearest guard who is free is then ordered to the place he names, labelled
`Captain`. Bram can drive the wolf off with `attack_target`, and Wren, if she sees it, with `chase`.
Bram used to be offered `chase` beside the bell too, and played live he chose it over the bell in
four runs of four, so the chain to Hale never began on its own; with his own actions only, it may
again, but that has not been played live yet.

### The thief (F3)

The thief (`VillageThief`, a scripted NPC with the kit's `char_thief`, not an agent) is a
`Targetable` of category `stranger`, saved inactive, so no agent can name or see it until it is
out. Once it has taken a ware:

- **Odile is asked,** and is offered **`call_guard`**, whose target is a guard. The guards reach her
  schema through `GuardTargetSource`, a custom `ITargetSource`; her proximity source offers wares
  and places, not people, so no villager is among her targets. Her call becomes that guard's
  stimulus, with her own words, labelled `Merchant`, the same way Hale's `send_guard` works.
  `call_guard` is offered while a thief runs or a fire burns, and `target_fits_action` contains a
  call at anything but a guard. For a thief she may call either guard; for a fire alone, only one
  who can carry water (Wren), so the call never goes to a guard with nothing to do about it.
- **A guard who sees it running** is asked, once per theft. Wren is offered **`chase`**; Bram goes
  after it with **`attack_target`**, which runs after a running thief: played live, the model picked
  `attack_target` as often as `chase`, and a guard that walked to where the thief had been let it
  get away. A called guard who cannot see the thief yet can go to the eastern road, and is asked
  again when it sees it. The thief runs at 2.5 m/s and a guard after it at 7 m/s.
- **Caught,** the thief drops the ware, which goes back on the counter at once. If it reaches the
  far end of the road it is gone, and the ware with it until the stall restocks it, 90 s later.

### A fire (F4)

Every guard who can see the smoke above the stall, and every agent within 10 m (Odile), is asked.
**`fetch_water`** is Wren's, offered only while the fire burns. She runs to the well, fills a bucket,
carries it to the stall and throws it, and goes back for more, until three buckets have put the fire
out. Odile can `call_guard` for the fire, and is offered Wren alone. Bram is told of the fire, and
can go there or warn the player, but carrying water is not one of his actions.

### Night at the gate (F5)

`DayNightCycle.NightChanged` shuts the gate. The gate's doors (`VillageGate`) are two `gate_door`
models; shut, a blocker stops the player and a `NavMeshObstacle` carves the running NavMesh.
The blocker sits on the Ignore Raycast layer, which a guard's sight leaves out, so the watch still
sees the wolf or a visitor through the bars.

- **The gate actions go to the night watch, not to Bram.** At nightfall Wren hurries to the gate and
  takes the watch (`NightWatch`, a component on Wren alone), and Bram goes off watch to the
  campfire, so he is not the one the player finds at the gate. Wren is asked when she gets there.
- Only Wren has **`open_gate`** and **`close_gate`**, available from the gate's state while she has
  the watch: `close_gate` while it is open, `open_gate` while it is shut and the player or the
  captain asks. A quiet moment never opens it, and the watch does not patrol.
- **Bram's action set never changes, day or night.** He is the evaluation agent. One test checks
  that whoever asks him (the player, a zone, the wolf, a quiet moment) he is offered the same
  actions at night as by day; another, that with the wolf close, a thief running and the stall on
  fire, by day and by night, whoever asks, he is offered nothing outside his own actions and
  registers no new one. Only his observations change: every agent is told "It is night." instead
  of "It is daytime.". Wren's card and name tag say "the night watch" while she has it.
- At dawn the gate opens, Wren goes back to the bridge and Bram to the gate.

### The bell: no `bell_cooldown` guard

#153 asked for a `bell_cooldown` guard: "the same threat cannot ring the bell twice within 30 s".
Availability already enforces it, so there is no guard: a guard that can never fire is not worth
adding. The bell rings for 30 s (`VillageAlarm.RingSeconds`), and `sound_alarm` is not offered while
it rings. Two guards asked at once, both offered the bell before it rang, ring it once: the answer
that comes back second is refused by `Execute`'s re-check (`SchemaLegalityGuard@Execute`). A test
holds both answers to check exactly that, and that the bell rings at least 30 s.

## The framework view

The top left always shows a strip with each agent's last outcome. **Tab** opens the whole board,
labels every place with the target id the agents name it by (restricted places in red), and
shows the lane of requests waiting for the model, blue for player-facing and purple for
background. One card per agent shows:

- its name and role, what it is doing, how far you are, and its quiet timer or zone status
- the last stimulus, with its label and priority
- the actions it was offered
- what it chose, and what it said
- an outcome chip: executed, none, contained, refused, error, or thinking while it waits
- the guard that stopped it, and its reason
- its latency, queue wait and requests ahead, history turns sent, and prompt tokens

The board itself (`LocalShared/AgentBoard.cs`) takes strings only, so it stays framework-free
for #14; `VillageCommands` fills it each frame.

## Request history and the decision log

`DecisionRecorder` keeps every decision from every agent, with the request each agent sent and
the reply it got, read from `DecisionResult.Request` and `Reply` (#142). It no longer wraps the
provider: with two agents on one queue, a wrapper cannot tell whose request it was handed. Press
**H** in Play mode: the list shows each decision, newest first, for every agent or one;
selecting one shows the result and telemetry, the raw reply, the offered actions and targets,
the system prompt, the few-shot examples, the history, the observations, the stimulus and the
JSON schema. Every finished decision is also appended, as one JSON line, to
`Logs/agenerela-decisions-<time>.jsonl` (gitignored; a new file each Play session).

It is a game-side prototype of the framework's Decision Log window (build plan §2.7, Phase 5).

## Dependencies and tools

The framework is loaded from `file:../../../UnityProject/Packages/com.agenerela.framework`. The
village assembly references `Agenerela` and uses only its public API; the `Demos.Shared` helpers
in `Assets/GreyBoxVillage/LocalShared/` stay framework-free until #14 moves them out.

The Unity CLI drives the editor through `com.unity.pipeline`: `unity status` shows the connected
editor, `unity command script eval '<C#>'` runs code in it, Play mode included, and
`unity test Demos/GreyBoxVillage --mode EditMode` runs the tests in batch mode while the project
is closed. MCP for Unity is still installed, pinned to the framework dev project's revision.
Unity Version Control (`com.unity.collab-proxy`) is not: this repository uses git, and its
progress dialog blocked the editor after every file change.

## Navigation and verification

The NavMesh is baked in the editor and stored as JSON (`VillageNavMesh.villagenavmesh`) because
Unity forces native NavMeshData assets to binary; the importer makes the native asset in the
cache. `Rebuild Village` bakes it; `Rebake Navigation` re-bakes after hand edits.

The EditMode tests run with no Ollama, through a fake provider config that all four agents share.
They cover:

- Odile sells only what is on her counter, and a sold ware leaves her schema.
- `cannot_afford` and `no_free_gifts` contain what they should and pass the rest.
- A quest is offered, then completed only when ready, and leaves the schema when done.
- `quest_not_ready` catches items lost while the model thought.
- `target_fits_action` contains a quest offered at a place.
- The captain's order becomes the nearer free guard's `Captain` stimulus.
- The wolf drops its pelt only during its quest.
- A pickup within reach goes into the pack.
- The guards are never offered the stall's wares.
- Every new agent's few-shot examples stay clear of the evaluation prompts.
- Playing through a night, in Play mode, changes no saved asset: the materials, the volume
  profiles, the agents' profiles, the provider config and the panel settings.
- Every static field in the game's two assemblies is reset by a `SubsystemRegistration` method
  before each play session. There are none today, so the test is a guard for the next one.
- The default volume profile holds no component whose script is missing.
- With no model (unreachable, or the model not pulled), the village says so on the HUD and in a
  red bubble, stops asking on its own, still sends the player's lines, and clears it all at the
  first answer. Any reply ends it, even one that cannot be read, and an unreadable reply on
  its own is not taken for a missing model.
- No static table holds objects that can change: the item catalog and the suggestion lines are
  read-only.
- A `ScatterField` follows its transform when it or a parent moves.
- The scatter is instanced, no placed model overrides the palette, and the scene stays under
  1.5 MB.
- The situations (`VillageSituationTests`): a thief robs Odile, who is offered `call_guard` only
  while there is trouble, aimed only at a guard, and her call reaches the guard she named with her
  words, and for a fire only a guard who can carry water; `chase` is offered only while a thief
  or the wolf is in sight; a caught thief's ware goes back on the counter, and one that gets away
  takes it; nightfall shuts the gate and hands the watch to Wren, who alone opens and shuts it, and
  never on a quiet moment; Bram is offered the same actions by night as by day, whoever asks, and
  nothing outside his own actions whatever happens; the watch sees through the shut gate; whoever
  sees the smoke is asked, `fetch_water` is offered only while the fire burns, and three buckets put
  it out; two guards answering the bell at once ring it once; a reset drops a decision still with
  the model; the new actions' few-shot examples are not evaluation prompts; and the scene is wired
  as the situations need it, the thief saved inactive.
- The presenter script, end to end in Play mode (`VillagePresenterScriptTests`): every stage of
  [the script](#presenting-it-a-five-minute-script) below, with everything walking, running and
  burning, against the scripted replies in `Editor/PresenterScript.cs`. Each key must leave an
  agent asked within 6 s, the keys work with the framework view open but not while the player
  types, and no decision in the run is refused or fails. After F5 the villagers are at the
  campfire; F6 goes round to fog, which Wren's next request carries, and F1 brings clear skies back.
  It takes about 15 s.
- The day and the weather (`VillageExploreTests`): each villager goes where its schedule says at
  every hour, goes indoors after dark with the lamp by its door lit, still a target, and comes out
  in the morning; the bell sends it to the shelter, the hour cannot move it while the bell rings,
  and it goes back to its day when the bell stops; every agent is told who is where, at work, at
  midday, at night and under the bell; every place of the routine and every door is on the
  NavMesh. Fog shortens a guard's sight, so a wolf reported 20 m off in clear weather and in rain is
  not reported in fog until it is close, and the player 20 m off is out of sight; every agent is
  told the weather and how far it can see, and clear weather adds nothing; the weather never adds
  or takes away any of Bram's actions or targets, whoever asks; it changes by itself a few times a
  day, through all four, and F6 goes round them; slice 3 adds no target to the village, and Bram is
  offered the same targets at every hour in every weather; and the routine and the weather are in
  the scene and wired, the rain saved not falling.

And from before:
each action executes on the guard; every evaluation target is offered and named in the observations; who is asking (the
player, a zone, a wolf, a quiet moment) decides which actions are offered, as the table above
says; a quiet moment can start a patrol and anything else breaks it off; only the player ends an
escort; the bell is offered for a wolf 5 m away and not for one 20 m away; following and the
alarm change which actions are offered; the schema guard refuses an action that was not offered
and an unregistered target, and the grounding guard contains a target the model wanted but was
not offered; a guard will not attack the village's people and says its own line, while the wolf is still
fair game; history reaches the second request; each guard remembers only its own exchanges; the
two guards share one queue, and each record keeps its own agent's request when priorities mix;
an event reaches only the agents that hear it, and the bell reaches Wren but not Bram, who rang
it; the player speaks to the nearer guard; no few-shot example is an evaluation prompt (hard
rule 4), with every action's examples, `stop_following` and `patrol` included; an event is
labelled and waits for a running decision, and still runs when that decision fails, but not once
its guard is destroyed; a quiet moment asks in the background; the recorder keeps the request and
reply and writes one JSON line; the NavMesh reaches every place and zone entrance from both posts, over the
bridge; both guards in the saved scene ask the same provider config; one test enters Play mode
and types through the UI.

Played live on 25 October 2026, with Bram the only guard, `qwen3.5:2b` on Ollama 0.33.0, warm, each decision in about
0.4 s. These are single runs, not measurements (hard rule 2):

- A quiet moment, unprompted: `patrol`, "I will keep watch over this village while it remains calm."
- Trespassing in the armory, once the following guard saw it: `block_path / armory`; leaving:
  `patrol`.
- "Drive that wolf off, now!": `attack_target / wolf`, and the wolf fled.
- A wolf sighting twice drew `sound_alarm` with the statement "The wolf is too far away to be a
  threat", the action contradicting the words, and the second time word for word the first
  answer from the history: something for Phase 4 to measure.
- "Bandits are climbing over the east wall" drew `warn_player` while the bell was already ringing,
  so `sound_alarm` was not on offer.
- "Walk with me for a while" drew `follow_player` in one session and `patrol` as the first, cold
  request of another.

Those runs predate the availability rules above. Checked again the same day with the rules in
place, through `VillageCommands` in an EditMode run (so the guard never walked), same model, warm,
one run of each line:

- "Hello" twice, "hello", "Hello sir" (once following, once not) and "Hello guard, what can you
  see ?" all drew `none`; `patrol` was not offered for any of them.
- Every quiet moment drew `patrol`.
- "Walk with me for a while" drew `follow_player`; "Can ou go with me ? I saw a wolf" drew
  `warn_player`.
- A wolf seen 20 m away drew `attack_target / wolf`, with the bell not on offer; at 5 m, with the
  bell offered, `attack_target / wolf` again. The statement was the same word for word each time,
  the history repeat noted above.

## Presenting it: a five-minute script

Before the audience arrives: start Ollama with `qwen3.5:2b`, open `GreyBoxVillage.unity` and press
Play. Say one line to Bram, so the model is loaded (the first request after a cold start can take
several seconds). Press **Tab** to open the framework view and leave it open: the board on the left
shows each agent's stimulus, what it was offered, what it chose and which guard stopped it, and
the lane shows requests waiting for the one model. **F12** shows the presenter panel. If the model
stops answering, a red banner says so and the village stays playable (press **F1** once it is back).

| Time | Where you stand | Say or press | What the audience sees |
|---|---|---|---|
| 0:00 | Anywhere | **F1** | Morning, everyone at their posts, the board empty |
| 0:15 | Walk up to Bram at the south gate and press **E** | "Hello. Anything to report?" | Bram's card: `Player`, PlayerFacing, the seven actions he is offered, and his answer above his head |
| 0:45 | Beside Bram | "Walk with me for a bit." | `follow_player`, and he follows you. On the next line his card offers `stop_following` in its place |
| 1:05 | A few steps on | "Thanks, you can stay here now." | `stop_following`. Only the player starts or ends an escort |
| 1:20 | Inside the gate, by the camp, facing the road (press **Esc** to stop typing first) | **F2** | The wolf comes up the road. Bram's card: `Event`, "A wolf has come out of the woods, 14 m from you", `sound_alarm` offered because it is within 15 m. If he rings it, the villagers run for the shelter, three bell requests queue in the lane, and Hale sends a guard to the gate with a `Captain` order. He may drive the wolf off instead (`attack_target`): then walk up to him and say "Ring the bell, Bram! Warn the village!", since the player can always ask for the bell |
| 2:10 | **F1**, then walk to the market, south of Odile's stall | **F3** | The thief takes the bread. Odile's card: `call_guard` offered, Wren called with her words; Wren's card shows a `Merchant` stimulus. A guard who sees it running goes after it (Wren's `chase`, offered only now, or Bram's `attack_target`); caught, the bread goes back on the counter |
| 3:10 | **F1**, then stand between the well and the market | **F4** | The bread stall burns. Whoever sees the smoke is asked. Wren is offered `fetch_water`, only now, and runs between the well and the stall with buckets; three put it out. Odile calls her too, the only guard she is offered for a fire. Bram's card offers nothing new |
| 4:00 | **F1**, then walk out through the south gate and turn round | **F5** | Dusk. The gate shuts, Wren hurries to it and takes the watch (her card now says "the night watch at the gate"), Bram goes off to the campfire. Wren's card: `open_gate` is not offered, because nobody has asked |
| 4:20 | Outside the shut gate, facing Wren | "Wren, it's me. Let me in, please!" | Wren is offered `open_gate`, only now and only her; the doors swing open |
| 4:35 | Walk in | "Thank you. Shut it behind me." | `close_gate`. Then walk to Bram at the campfire and ask "Quiet night, Bram?": his card offers exactly what it offered by day. He is the evaluation agent: neither night, nor the wolf, the thief or the fire, gives him an action he did not have |
| 4:50 | Anywhere, the board open | **F6** three times, to fog (optional) | The clock says fog, and the next card of whoever you speak to shows "Fog lies over the village; you can see about 15 m." in its observations. The weather never changes what an agent is offered, only what it sees: a wolf 20 m off would go unreported. **F1** clears it |
| 5:00 | | | End on the board: every decision, and why each one could or could not be made |

What to say while it plays: the model never moves anyone. It picks from the actions it is offered,
and the game decides what to offer from its own state (who is asking, what is in sight, what is
burning, whose watch it is). The guards (`target_fits_action`, `never_attack_villagers`, the
schema guard's re-check at `Execute`) catch what availability cannot.

The same script runs headless against scripted replies as a test (`VillagePresenterScriptTests`),
and once with the real model through `PresenterLiveFromCommandLine`, which writes every decision
down.
