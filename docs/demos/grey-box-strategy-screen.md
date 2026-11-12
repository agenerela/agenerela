# GreyBoxStrategy, stage 3: the painted map and the screen

**Status:** a proposal for the lead to approve or redirect, 11 November 2026 (#20). No game code
yet. The rules, the board and stages 1 and 2 are in [`grey-box-strategy.md`](grey-box-strategy.md)
and in the demo's [`README.md`](../../Demos/GreyBoxStrategy/README.md). This page designs the
screen: what it shows, how the map's colour changes, the three modes, the art approach for
Unity, the assets, a new name, and six questions for the lead.

> **What the lead decided on 11 November**, which this page builds on: a painted parchment map
> (hand-drawn old-world look, inked borders, paper texture, country colours washed over the
> provinces like watercolour); all three modes in the order watch, rule, advise; rulers'
> statements as speech over the map at the capital, with a portrait and the country's colour,
> plus a chronicle at the side; court scenes later, for agreements such as peace; a new name at
> stage 3. And the goal: "make sure that the framework showcases itself perfectly there", with
> "the 'coloring' of the map" as the part that should feel best.

## The mock-ups

Eight images, made with an image model on 11 November. **They are drafts, not art**: generated
images stay drafts until a person puts them in a game.

| # | Image | Shows |
|---|---|---|
| 1 | [`1-look-sepia-atlas.jpg`](grey-box-strategy-screen/1-look-sepia-atlas.jpg) | **Look A, warm sepia atlas page.** Watch mode, turn 12. Muted earth washes, iron-gall ink, wax seals |
| 2 | [`2-look-storybook.jpg`](grey-box-strategy-screen/2-look-storybook.jpg) | **Look B, bright storybook map.** The same moment with bright washes, painted trees, hills and a sea serpent |
| 3 | [`3-look-sea-chart.jpg`](grey-box-strategy-screen/3-look-sea-chart.jpg) | **Look C, dark aged sea-chart.** Jewel washes on dark vellum, gold-leaf country borders, brass and leather panels |
| 4 | [`4-capture-sequence.jpg`](grey-box-strategy-screen/4-capture-sequence.jpg) | **The centrepiece in four steps**, look A: strike, fall, spread, settle |
| 5 | [`5-capture-moment.jpg`](grey-box-strategy-screen/5-capture-moment.jpg) | **The centrepiece on the full screen**, look A: the map dimmed around one province, the colour mid-spread, the score ticking, the chronicle's new line |
| 6 | [`6-mode-rule.jpg`](grey-box-strategy-screen/6-mode-rule.jpg) | **Rule mode**, look A: the player's council, legal targets ringed on the map, a move that is not offered and why |
| 7 | [`7-mode-advise.jpg`](grey-box-strategy-screen/7-mode-advise.jpg) | **Advise mode**, look A: the player's counsel, the ruler's answer, and why it made its move |
| 8 | [`8-court-truce.jpg`](grey-box-strategy-screen/8-court-truce.jpg) | **A court scene** for a truce, over the dimmed map |

![Look A, the recommended look](grey-box-strategy-screen/1-look-sepia-atlas.jpg)

![The capture in four steps](grey-box-strategy-screen/4-capture-sequence.jpg)

**What the images get wrong**, so nobody builds it from them:

- **The geography is the image model's, not the board's.** The real board is the 5 × 5 graph in
  [`grey-box-strategy.md`](grey-box-strategy.md#the-map), where each province borders only its
  four orthogonal neighbours. The images draw more than 25 shapes in places, and diagonal
  provinces touch. [The art approach](#the-art-approach-for-unity) says how the real map keeps
  the rule graph exactly.
- Province counts in the images do not add up to the strip's counts, and the counts in image 6
  (turn 9) are not a real game.
- Image 7's "Advice heard: yes" is not something the framework can know. The real card shows
  the counsel as it was sent in the request (see [advise](#3-advise)).
- Image 7 drops the capital names, and its arrow points at Merrowdeep while the card says
  Templegate.
- The line at Keshtar in images 1 to 3, "Harrow must bleed for daring to stand near our sacred
  soil", is real: kesh, the proud ruler, said it in the persona 4b table's seed 4. Every other
  line is written for the mock-ups.

**Recommended look: A, the sepia atlas**, with B's clarity in one respect, the labels and
panels. A is the lead's brief taken literally: paper, ink, watercolour. Its muted washes leave
room for the capture to be the brightest thing on screen (a fresh wash can be painted a little
more saturated, then dry to the muted tone). B reads fastest, but its painted terrain competes
with the colour for the eye, and its bright washes leave the capture nowhere brighter to go. C is
the most dramatic, but the dark sea and gold borders make five colours harder to tell apart, and
its leather panels are the most art to make. Images 4 to 8 are drawn in look A.

## The screen's regions

Drawn in image 1. 16:9, laid out for 1920 × 1080, and it must stay legible at 1280 × 720.

| Region | Where | What it shows |
|---|---|---|
| **Top strip** | full width, 7% of the height | `Turn 12 of 30`; one wax seal a country, in seat order, with its name and province count, leader first; `15 to win`. A country that is out keeps its seal, cracked and grey. In rule and advise mode the player's country comes first, with a crown or a quill |
| **Map** | left, about 70% of the width | The board, framed by sea. Capitals named, forts as small tower glyphs (one or two), battles as crossed swords for the turn they happen. Neutral land bare paper with light hatching and its garrison on hover |
| **Speech banners** | on the map, at a capital | A ruler's statement on a ribbon in the country's colour, the portrait at its left end. One at a time at 1×; see [statements](#statements-over-the-map-and-in-the-chronicle) |
| **Side column** | right, about 25% of the width | Watch: the **chronicle**. Rule: **your council**, with the chronicle as a tab. Advise: **why this move**, with the chronicle as a tab |
| **Controls** | bottom left, over the sea | Watch: `Watching`, pause, step, `1x` `2x` `4x`. Rule: `Ruling Merrow`. Advise: the counsel box, across the bottom of the map |
| **Why card** | over the side column, on demand | The decision behind any banner, chronicle line or seal: see [what the framework shows](#what-the-framework-shows-off-on-this-screen) |

Hovering a province shows a small parchment label: its name, owner, forts and, for neutral land,
the garrison. Clicking a country's seal opens its sheet: treasury, army, stability, relations,
persona and model. Those are the standing facts the country itself reads, word for word, so a
viewer can see exactly what the ruler saw.

## A turn on screen

The simulation resolves a turn at once; the screen **plays the turn back from the game's
events**. That separation buys the one thing that hides the model's latency: while the screen
animates turn *N*, the council for turn *N + 1* is already being asked. A council is five calls
through one queue, about 2.3 s on the RTX 5080 with personas on qwen3.5:4b (0.46 s median a
decision), and several seconds on a laptop; a turn's playback at 1× is 6 to 15 s, so at
1× the viewer never waits. At 4×, the strip shows five quills scratching on the seals until the
council is in.

The beats of one turn, in the game's own order:

1. **Upkeep** (0.6 s): income ticks onto each seal; an event shows as a small stamp on that
   country's capital (a sheaf for a good harvest, a cracked sheaf for a poor one, a dagger for
   bandits); a revolt plays the capture in reverse (below).
2. **Council** (only if still waiting): quills on the seals, each stopping as its answer comes in.
3. **Envoys**: an accepted peace opens a court scene; a refused one is a short envoy card (a
   sealed letter torn in two, the two lines said, 2 s).
4. **Resolve**: each move lands in turn, and **the banner and the move land together**: the
   ruler speaks, then the map answers. Peace first, then home moves (a levy, a fort being inked
   in, a tax coin), then declarations of war (a torn ribbon between the two seals, the border
   between them redrawn as a thicker, darker line), then attacks.
5. **Check**: an elimination cracks a seal; a win gets [its own ending](#the-ending).

A move refused at resolution (stale moves, about six a game in the scripted runs) is shown, not
hidden: the arrow starts, stops at the border, and dissolves, and the chronicle line is struck
through with the reason ("Merrow took Duskvale first"). It is the clearest picture on the screen
that the game, not the model, decides what happens (hard rule 5).

## How the colour change should feel

This is the centrepiece, and image 4 is its storyboard. **The eye follows one path**: the
attacker's capital, where the banner rises, then the arrow, the province, the score on the strip,
and the chronicle's new line. Everything else on the map steps back while it plays.

Times are at 1×; 2× halves them, 4× quarters them. One capture takes 2.4 s.

| Step | Time | What happens | How |
|---|---|---|---|
| **0 Focus** | −0.3 to 0 s | The rest of the map dims (saturation −30%, value −10%), the target and its neighbours do not | ease in-out, held to the end of step 4 |
| **1 Strike** | 0 to 0.4 s | An ink arrow in the attacker's colour draws itself from the centre of the attacker's bordering province to the target's centre; at 0.3 s crossed swords stamp in the target with a small ink spatter; the battle's numbers appear small beside them (`91 vs 14`, from the game log) | stroke reveal along a spline, ease-out; stamp scales 1.3 → 1.0 |
| **2 Fall** | 0.4 to 0.8 s | The old wash lifts: the loser's colour breaks into dry specks and fades toward paper; the ink border between attacker and target fades out | a noise-threshold dissolve on the old colour's alpha, 1 → 0.1; border alpha 1 → 0 |
| **3 Spread** | 0.8 to 2.0 s | **The new colour bleeds in from the shared border** like wet paint on damp paper: a soft front with a darker wet edge and small cauliflower blooms, sweeping across the province | a front at distance *d* from the shared border, *d* rising 0 → 1 with ease-out-cubic; the front perturbed by a bloom noise; a wet-edge band 8 to 12 px darker by 25% |
| **4 Settle** | 2.0 to 2.4 s | The wet edge dries into the pooled-at-the-border look every province has; the new frontier is inked in with a stroke; a seal stamps in the province; on the strip, the winner's count flips up with a small pop and the loser's down; the chronicle's line slides in on top with a soft glow | wet-edge darkening blends into the edge-pool term; border stroke reveal along the new frontier, 0.3 s; the count flips at 2.1 s |
| **5 Release** | 2.4 to 2.7 s | The dim lifts | ease in-out |

**Variants:**

- **Neutral land** skips step 2: the province is already paper, so the spread starts at 0.4 s and
  a capture takes 2.0 s. These are most captures in the first five turns, when the map fills.
- **A failed attack** stops after step 1: the arrow cracks at the border, the swords stamp, and
  the province gives a small shake (2 px, 0.2 s). The loser's colour never moves.
- **A revolt** runs the capture backwards: the colour drains from the province toward its
  borders, hatching is inked in, and the border to its old owner is drawn.
- **A capital falling** adds a beat: the tower glyph cracks and the country's seal on the strip
  dims.
- **Several captures in one turn** at 1× play one after another; at 2× and 4× the next starts
  at the previous one's step 4, so a big turn still reads as one wave of colour.
- **Sound**, when there is sound: a brush stroke on the arrow, a dull drum on the swords, a wet
  swish on the spread, a wax-seal thud on the settle.

**What makes it feel good**, in priority order: the colour moves *from the attacker's side*, so
it reads as conquest rather than a recolour; the front has a wet edge, so it reads as paint; the
dim keeps the eye on it; the score and the chronicle answer in the same second, so the viewer
never wonders who now leads.

### The ending

When a country reaches 15 provinces, every one of its provinces re-runs a short spread (0.6 s,
staggered by distance from its capital, all within 2 s), the other colours dim to 50%, the
winner's ruler speaks a last banner (a final call with the stimulus "You have won"), and the
chronicle turns into a one-page summary of the game: turns, wars, peace made, provinces each
country held, with every decision still a click away. At turn 30 without a winner, the same, for
the country holding most.

## The three modes

Built in this order. Each mode reuses the last one's screen and changes only the side column,
the controls, and what waits for the player.

### 1. Watch

Five model rulers play; the player watches (images 1 to 5). Pause, step one move, and three
speeds. Pausing also pauses the council, so a paused game asks no model. Every banner, chronicle
line and seal opens its Why card. A table is chosen before the game: the model for each seat and
whether personas are on, as `Tables.Named` already defines them, so a viewer can set the 4B
against the 2B and watch the difference.

**Done when** a viewer can watch a whole game at 1× without the screen waiting on the model, and
every capture plays the sequence above.

### 2. Rule

The player rules one country against four model rulers (image 6). The side column becomes **your
council**: the country's figures, then the moves it is offered, which are **the same list the
models are offered**, from the same `Legality.WhyNot`. A move that takes a target rings the
legal targets on the map with a dashed gold line; hovering one says what it would be ("Attack
Harrow, garrison about 12"). Moves that are not offered are listed faded with their reason (the
rule's own reason, as `[AvailableFor]` gives it the models). The player's seat is an ordinary
`IPlayer`, not a framework agent, and the turn waits only for it.

Rival rulers' banners still appear, and statements that name the player's country are kept in
the chronicle with a small mark. A peace offer to the player opens the envoy card with
**Accept** and **Refuse**; a truce the player accepts opens the court scene.

The player sees what a model ruler sees, no more: the same standing facts and this turn's facts.
That is a fairness line the demo can say out loud.

**Done when** a person can play a whole game as any country and the moves offered match what
the models were offered on the same state.

### 3. Advise

The player is the counsellor of one model ruler (image 7). Each turn, before the council, the
player may write one line of counsel in a box across the bottom of the map; leaving it empty is
fine. **The counsel goes into that ruler's call** as a fact of this turn, after the stimulus
(`ObservationPlacement.WithStimulus`, #239), marked as the counsellor's. The ruler then decides
as always and answers with its statement, which is shown as a reply to the player.

The side column is **why this move**: the decision's Why card, open by default, with the
counsel quoted as it was sent. This is the mode that shows the framework's promise most sharply:
**counsel cannot make an illegal move**. "Give all your land to Orsk" can only steer the ruler
among the moves it is offered.

Advise needs one stage 2 style measurement before it is a feature: whether counsel changes the
4B's moves at all, compared with no counsel on the same seeds (hard rule 2). If it barely does,
the mode still works as theatre, but the Why card must not claim influence it cannot show.

**Done when** counsel reaches the call, the reply reads as a reply, and the A/B run is in
`findings.md`.

## Statements over the map and in the chronicle

**Over the map.** A statement rises as a ribbon banner from the speaker's capital, in the
country's colour, with the ruler's round portrait at the left end (images 1, 5 and 6). Rules:

- One banner at a time at 1×, shown as its move lands: 1.5 s plus 40 ms a character (about 4 s
  for a 15-word persona line), skippable with a click. At 2× and 4× a banner shrinks to the
  portrait and the first few words, and the whole line goes only to the chronicle.
- The banner sits above the capital, or below it, whichever keeps it on the map; it never
  covers the province the move targets, so the colour change is never under a banner.
- The portrait has three moods, chosen from the move, not from the text: calm (home moves,
  peace), angry (war, attack), triumphant (a capture that landed).
- A hold with a statement gets a banner at half size. A statement that is empty gets none.

**In the chronicle.** Each line is written **by the game, from what happened**, and the ruler's
words sit under it as a quote:

> **Kesh takes Harrow from Valen** · turn 12 · Kesh's seal
> *"The hills remember. Harrow is ours."*

So a statement that contradicts its move (18% of persona 4B statements, from the scorecard)
reads honestly: the viewer sees what was done and what was said, side by side. Newest first,
grouped by turn, one click to the Why card. A filter shows one country.

**Court scenes** (image 8) fit at the envoy step, when an offer of peace is **accepted**, or
when two countries offer each other peace in one turn. Peace is made 1.8 to 2.1 times a game at the
model tables with personas, so a court scene can always play at 1× and 2× and be skipped at 4×. The scene is
a painted hall, the two rulers at a table with a treaty between them, and the two real
statements: the offering ruler's and the accepting ruler's. The title names the treaty after a
province on the border they share ("Truce of Harrow"). It is the most visible proof that one
agent's decision is another agent's moment. Declarations of war could get the same scene later;
that is a question below.

## What the framework shows off on this screen

The Why card is the framework's window, and every fact on it comes from what the framework
already records for each decision (`DecisionFeed.Recorded`, `DecisionResult`,
`DecisionTelemetry`), so the demo invents no number. Opened from a banner, a chronicle line or a
seal:

| On the card | From | What a viewer learns |
|---|---|---|
| **The move, its target, the statement**, in that order | the decision | The response's field order, action first (a settled design rule) |
| **The moves it was offered**, and those it was not, each with its reason | the schema's action set, `TargetsUnavailable` with each reason (`ITargetAvailability`) | Illegal options are absent, not forbidden in prose: "Declare war on Orsk: not offered, truce, 3 turns left" |
| **What it was told**: its standing facts and this turn's facts, and what was cut | the request, `ObservationsDropped` | The ruler decides on the same few lines the player can read |
| **Guards**: passed, or which guard stopped it and why, word for word; how sure the model was of the target | `GuardsFired`, `GuardReason`, `TargetGrounding` | A guard catching a model that named a province it cannot attack |
| **What happened when it landed**: done, or refused at resolution and why | `Execution`, `ExecutionReason` | "Refused: Merrow took Duskvale first": stale decisions re-checked (stage 2's point 3) |
| **The council**: the round it belongs to, and which other decisions answered it | `ChainId`, `ChainSource`, the round's entries | Five rulers asked at once on one snapshot; an envoy as a relayed decision |
| **Memory**: turns remembered and sent | `HistoryTurnsSent`, `HistoryTurnsDropped` | A ruler that remembers who betrayed it, and how much it can remember |
| **Model, persona, time**: the model, the provider config, latency and time in the queue | `Model`, `ProviderConfig`, `LatencySeconds`, `QueueWaitSeconds` | Five rulers on one loaded model; two models at one table |

Two more things on the screen show the framework without a card: the **seal sheet's persona
line**, which lets a viewer see that the same model plays five different rulers, and an optional
**table overlay** (one key) that puts each seat's model and persona on the strip.

The card is the game's own UI reading the feed, not the editor's Decision Log window (screen 3 in
[`docs/design/`](../design/README.md), not built yet). A button on the card can open the run's
`decisions.jsonl` line for anyone who wants the raw record.

## The art approach for Unity

2D, in the existing URP project (the 2D packages are already in its manifest). The map is **one
quad drawn by one shader**, not 25 sprites, so colour, borders and the capture animation are all
data the shader reads.

**How provinces are represented.**

- **A province ID map**: a texture (2048 × 1152 for the map area) where each pixel's red channel
  is its province's index, 0 to 24, and 255 is sea. It is the single source of the map's shape.
- **Made from the rule graph, so the drawing cannot disagree with the rules.** An editor tool
  takes `MapDefinition`'s 5 × 5 grid, displaces each cell edge with low-frequency noise for
  hand-drawn, organic borders, and places a **painted landmark at each of the 16 interior grid
  corners** (a mountain, a lake, a wood): where four provinces meet, the landmark keeps the
  diagonal pairs from touching, which is the mistake every mock-up makes. The tool checks the
  resulting adjacency against the rules and refuses a map that differs.
- **Baked from the ID map** by the same tool: a distance-to-border field for every province (for
  the pooled-edge darkening) and, for each of the 40 shared borders in both directions, a
  distance-from-that-border field (for the spread front), packed into one atlas.
- **A per-province state texture**, 25 × 4 texels, written each frame from C#: current colour,
  previous colour, capture progress, the direction it is coming from, the dim factor, and
  highlight flags. Animation is only these numbers changing.

**What the shader composes**, bottom to top: the painted parchment; the wash (the owner's colour,
alpha shaped by a granulation noise and a paper-grain texture, darker toward each province's
border); during a capture, the spread front and its wet edge; ink borders, computed from the ID
map so that **a border between two owners is drawn heavier than one inside a country**, which
means country borders redraw themselves when land changes hands; and the dim.

**Painted, generated and shaded:**

| Kind | What |
|---|---|
| **Painted** (a person draws or finishes it) | the parchment base and sea, the coast, the 16 corner landmarks, capital and fort glyphs, the compass, the five seals' emblems, the UI frames |
| **Generated, then curated by a person** | ruler portraits and their moods, the court hall and the court figures, concept passes for the parchment and the landmarks; images from the asset server once the RTX 5080 is free, as `tools/comfyui/` describes |
| **Shaded** (no art at all) | every wash, every border, the capture, the dim, the wet edge, the seals' glow, the hover outlines |
| **Text** | province names and all UI text, from two open-licence serif fonts (IM Fell English for titles, Cormorant Garamond for body), so nothing is baked into art |

The UI is runtime UI Toolkit with parchment and ribbon 9-slice frames; banners are world-space
elements placed from each capital's position in the ID map.

**The main risk: the wash looks like a flat fill with noise on it**, which is exactly the
"simple" look the lead does not want. Watercolour is hard to fake with a shader, and the whole
demo's impression rides on it. It is tested first, before any other part of stage 3 (a one-scene
spike: the map, the five washes, one capture), and judged by the lead from a captured frame.

**The fallback** if the spike does not convince: **paint the washes instead of shading them.**
For every province, a painted wash sprite in each of the five colours (125 sprites, plus their
masks), cut from five full-map watercolour paintings, one per colour, made over the same
ID map. The capture then cross-fades the two sprites through an animated dissolve mask that
still sweeps from the shared border. It loses the procedural wet edge and costs more art, but
real paint looks like real paint. The shader's borders, dim and highlights stay as they are.

## Assets, with rough counts

| Group | Asset | Count |
|---|---|---|
| **Map** | parchment base with sea and coast, painted at 4096 × 2304 | 1 |
| | province ID map and its baked fields (generated by the editor tool) | 1 + 1 atlas |
| | corner landmarks (mountains, lakes, woods), variants | 6 to 8, placed 16 times |
| | capital glyphs, one per country | 5 |
| | fort glyph, one and two forts | 2 |
| | battle swords, revolt and event stamps (harvest, poor harvest, bandits, unrest) | 6 |
| | ink arrow brush texture, ink spatter | 2 |
| | watercolour textures: granulation, bloom noise, paper grain | 3 |
| | compass rose, frame ornaments | 2 to 4 |
| **Countries** | colours, each with a fresh and a dried tone | 5 |
| | wax seal base and five emblems (mountain, lion, sun, trident, branch, as in the mock-ups) | 1 + 5 |
| | ruler portraits, round, three moods each | 15 |
| **Court** | hall background | 1 |
| | rulers at the table, half-body, one pose per side for each country | 10 |
| | treaty scroll and table props | 1 to 2 |
| **UI** | parchment panel, ribbon banner, letter (counsel box) as 9-slices | 3 |
| | buttons in four states, the move icons (recruit, fortify, raise taxes, declare war, attack, offer peace, hold, accept, refuse) | 4 + 9 |
| | fonts, open licence | 2 |
| **Sound** (later) | brush stroke, drum, wet swish, seal thud, quill scratch, page turn, war horn, peace bell | about 10 |
| | ambient loop and one music loop | 2 |
| **Fallback only** | painted province washes, five colours | 125 |

About 90 pieces of art, most of them small, plus the fallback's 125 if it is needed. The
portraits and the court figures are the largest single cost.

## Building it

A suggested order of work for stage 3, each step with something to look at:

1. **The spike**: the ID-map tool, the map shader, five washes, one capture at 1×. Frame captures
   through the Unity CLI for the lead. Decides between the shaded wash and the fallback.
2. **Watch mode**: playback from the game's events, the strip, banners, the chronicle, the
   controls, the council running one turn ahead.
3. **The Why card** over `DecisionFeed`.
4. **The court scene** for accepted peace.
5. **Rule mode.**
6. **Advise mode**, after its A/B run.
7. **The art pass** from the asset server, the rename, the sound.

The simulation and the model seats stay as they are; the screen is a new assembly reading
them, so the headless runs and the tables keep working beside it.

## The name

The demo will not look grey-box, so it gets a name of its own. Five proposals:

| Name | Why |
|---|---|
| **Inkwash** | Names the centrepiece: the map washed in ink and colour. One word, easy to say, unused by the other demos |
| **The Painted Realm** | Says the look and that it is a world of rulers; reads well as a title card |
| **Atlas of Kings** | The map is an atlas page, and the players are kings; good for a game whose viewer reads a chronicle |
| **Border & Brush** | Territory and paint in three words; playful, and a little harder to say |
| **Five Thrones** | Says five rulers at one table, the framework's agents. (Not "Five Crowns", which is an existing card game) |

**Pick: Inkwash.** It names what the lead wants to feel best, it fits look A, it is short enough
for a folder (`Demos/Inkwash/`) and a menu (`Inkwash → Play 200 games`), and it does not tie the
game to kings if a republic like Orsk is ever the hero. Renaming the folder and the issue
follows the lead's choice, not this page.

## Open questions for the lead

1. **Which look?** A (sepia atlas), B (storybook) or C (sea-chart). Recommended: A.
2. **Which name?** One of the five, or another. Recommended: Inkwash.
3. **Terrain on the land?** None (A), a little (only the corner landmarks), or a lot (B).
   Recommended: a little.
4. **A capture at 1× takes 2.4 s.** Right, slower, or faster?
5. **Court scenes for accepted peace only, or for declarations of war too?**
6. **Does a portrait belong to the country or to the persona?** Personas rotate across seats, so
   "proud Kesh" in one game is "merchant Kesh" in the next. Recommended: the persona, so the
   face matches the voice.
