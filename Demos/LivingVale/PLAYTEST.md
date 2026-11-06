# Living Vale — play-test checkpoint, 6 November 2026

**Milestone:** the first adventure is in the game. *The Missing Wagon* has all three recovery
routes and all three endings, there are two side quests, all eight NPCs talk through a local
model, and there is trade, combat, a map and compass, and saves. It is built on branch
`69-living-vale` from commit `aa09373`.

**What has been checked so far:**
- 579 EditMode tests pass (4 live-model tests skipped).
- Each route was checked live on gemma4 in the tests (7 of 7).
- The built game was played by the coordinator through chat, trade, combat and loot, Iven's quest
  offer, F6/F7, the map, and F9/F10 with NPC memory.

**Never played by hand from start to finish:** the full route from Iven to an ending. That is what
this checkpoint is for.

## Start

1. Ollama must be running with the default model: `ollama pull gemma4:e2b-it-qat`. Without it,
   the notice says the model did not load. **F6** switches to scripted replies, and **F7**
   reaches `qwen3.5:2b`.
2. Run `C:\Unity_Projects\wt-lv\Demos\LivingVale\Builds\Windows\LivingVale.exe`, or open
   `Demos/LivingVale` in Unity 6000.3.23f1, then `Assets/LivingVale/Scenes/Village.unity`, and
   press Play.
3. The first reply waits for the model to load, about 10 s, and the notice says "loading…".
   After that, replies take about a second.

## Controls

| Key | Does |
|---|---|
| WASD, Shift, mouse | Move, sprint, orbit the camera |
| **E** | Talk to the NPC in front of you (within 3 m), or use a quest object |
| Enter / Esc | Send a line / close the chat. Walking past 6 m also closes it |
| **I** | Inventory: gold, items, level, XP, quest state |
| **M** | Map: places found, paths, you, the quest goal, NPCs |
| Left mouse / right mouse / Space | Attack / block / dodge |
| **F9 / F10** | Save / load. NPC memory is saved too |
| **F6 / F7** | Switch between model and scripted replies / move to the next model Ollama has |
| F1–F5 | Scripted replies only: normal, slow, never answering, malformed, throwing |

The compass at the top points at the next goal, with its distance. The journal at the top right
says what to do next on its "Next:" line.

## A guided run (about 20–30 minutes)

Use your own words throughout; the NPCs are not looking for keywords.

1. **Briar Glen.** Talk to **Iven** (healer, by the porch with the patients). Ask about the
   missing medicine wagon. He offers *The Missing Wagon* on a card; **Accept** it.
2. **Mara** (the striped stall) sells potions, bread and herbs, and buys pelts, scrap and herbs.
   Ask in your own words. Only the card's button moves gold. Try one purchase.
3. **The Old Road** (south, about 150 m; follow the compass). Press **E** at the **wagon
   wreck** for a clue. Brann's **lost badge** is here too.
4. **Reedbank** (east, over the river). Talk to **Fen** about what they saw. Fen's account is the
   real proof for the investigation route.
5. **Broken Watch** (far south, past the Old Road). **Kest** holds the medical parcel. There are three ways to
   get it:
   - **Talk:** tell Kest what Fen saw. It only works after Fen has actually told you; just saying
     "I have proof" should get nothing.
   - **Pay** 20 gold, or have **Iven's herb favour** done first. Ask Iven for it, then pick the
     three marked herbs in Herb Hollow, west of the Glen, and bring them back.
   - **Fight** the three camp defenders, then take the parcel with **E**.
6. **The ending.** Deliver the parcel:
   - to **Iven** (the patients leave the porch);
   - or to **Elra** at Ashwatch, after **Brann** at the gate gives you a delivery permit (the gate
     opens);
   - or split it after both Iven and Elra agree, each on their own card.

   You should get the quest's XP once and level up.
7. **Side quests:** ask **Brann** at the Ashwatch gate about his badge, and bring it back from
   the Old Road. Iven's herbs are above.

## Worth trying on purpose

- Ask Mara for rumours or small talk. She should **not** offer a trade you didn't ask for.
- Ask an NPC something outside what they know, or try to make them break character.
- **F9**, then tell an NPC something new, then **F10**, and ask about it. They should remember only
  what was said before the save, and the chat panel should start empty.
- Press **F7** quickly several times, then talk. The first reply on a new model is slow while it
  loads, but nothing should time out from the key presses themselves.
- Walk away mid-reply, get hit mid-chat, and press Esc while waiting. A late reply must never
  appear or act.

## Known issues

- The world around the open map is barely dimmed, and the Glen's map labels crowd.
- The cobbles in the Glen read large and chequered.
- NPC routines can take them out of sight; use **M** to find them.
- Perk effects (Hardy, Skirmisher, Trusted Face) are recorded but not wired into combat or
  bargaining yet.
- **The XP budget is open:** five places pay 10 XP each on first visit, but the content list
  planned three (30). This is the lead's call.
- No sound yet. Sound waits for the asset server.

## Reporting

Note anything odd with what you typed and what happened. The coordinator session turns notes
into fixes, and framework gaps into entries in
[`docs/demos/living-vale-framework-gaps.md`](../../docs/demos/living-vale-framework-gaps.md).
