# Painted Realm (GreyBoxStrategy), stage 3: the painted map and the screen

**Status:** a proposal, in its second round, 11 November 2026 (#20). No game code yet. **Painted
Realm** is the working title the lead chose; the folder and the issue keep the old name until
the name is final. The rules, the board and stages 1 and 2 are in [`grey-box-strategy.md`](grey-box-strategy.md)
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

## Round 2: the lead's choices, and a screen you can click

The lead looked at round 1 and decided on 11 November:

| Question | His answer |
|---|---|
| Look | **C, the dark sea-chart** |
| Name | **Painted Realm**, for now. The folder and the issue are not renamed yet |
| Terrain on the land | **A lot**, as in look B, in look C's palette |
| Portraits | **Belong to the persona**, "but we will need to have variants": several faces for each persona |
| Capture time (question 4), court scenes for war (question 5) | Still open; see [the questions](#open-questions-for-the-lead) |
| Later | **Text-to-speech for the rulers' statements**: recorded here as a later feature, not designed yet |

He also judged round 1's interface. It "seems very mobile like and tbh not very attractive",
and "the mockup is not very detailed and UI is just like a useless image so its hard to say
anything." He likes the interfaces of PC grand-strategy games: "We obviously wont have as much
features as those games, but I anticipate us having not a small amount of features."

So round 2 is **a working HTML page**, not a picture. It is laid out as a desktop grand-strategy
screen, in an original design, with real content. The map, the panels, the three modes and the
decision inspector all respond to clicks.

### Open it

Open [`grey-box-strategy-screen/ui/index.html`](grey-box-strategy-screen/ui/index.html) in
Chrome or Edge. It needs nothing installed. When the machine is online, its fonts come from
Google Fonts; offline, it falls back to system serifs. A local server works too:
`python -m http.server 8765 --directory docs/demos/grey-box-strategy-screen/ui`.

The **Mock-up** strip at the top centre switches the mode (Watch, Rule, Advise) and the style
(Gilt, Engraved). Its **i** says what is real and what is scripted. **No model runs in the
page.** The game logic is real, ported to JavaScript from `GameRules` and `Legality`: the moves
offered, the reasons a move is not, battle strength, losses and upkeep. Turn 12 is invented to
fit those rules. Three statements are quoted from the persona 4b table's seed 4 and marked
`*`. The other statements and every telemetry number are written for the mock-up.

The address takes a state after `#`, which is how the screenshots were taken:
`#mode=rule&order=attack`, `#insp=kesh-12`, `#style=engraved&country=orsk&mm=armies`, or
`#still=600&zoom=2.4`, a frozen frame of a capture. `#selftest=1` plays turn 12 in all three
modes and writes what happened into the page. Run in headless Edge, it finished all three with
no script error.

### Screenshots

At 1920 × 1080 and 1366 × 768, in `grey-box-strategy-screen/ui-shots/`:

| # | State |
|---|---|
| [01](grey-box-strategy-screen/ui-shots/01-watch.jpg) | **Watch**, gilt: the whole screen, with Valen's envoy speaking at Highmoor |
| [02](grey-box-strategy-screen/ui-shots/02-capture.jpg), [02b](grey-box-strategy-screen/ui-shots/02b-capture-close.jpg) | **The capture**: Kesh takes Harrow with the map dimmed around it; close up, indigo bleeding in from Kesh's side across bare chart |
| [03](grey-box-strategy-screen/ui-shots/03-inspector.jpg) | **The decision inspector** on Kesh's attack |
| [04](grey-box-strategy-screen/ui-shots/04-guard-engraved.jpg) | **Engraved style**: a decision a guard contained, and a province panel |
| [05](grey-box-strategy-screen/ui-shots/05-rule.jpg) | **Rule** as Valen: the orders, and the attack targets ringed with the share of rolls each attack wins |
| [06](grey-box-strategy-screen/ui-shots/06-advise.jpg) | **Advise** Kesh: the counsel, the reply over the map, and the inspector with the counsel in the request |
| [07](grey-box-strategy-screen/ui-shots/07-diplomacy.jpg) | **Diplomacy**, and a neutral province's panel |
| [08](grey-box-strategy-screen/ui-shots/08-engraved-country.jpg) | **Engraved**, the armies map mode and a country panel: the persona, both faces, the standing facts, the moves offered |
| [09](grey-box-strategy-screen/ui-shots/09-small-watch.jpg), [10](grey-box-strategy-screen/ui-shots/10-small-rule.jpg), [11](grey-box-strategy-screen/ui-shots/11-small-refused.jpg) | **1366 × 768**: the reach map mode after the turn, rule mode with a province open, a move refused at resolution |

![Watch mode, gilt](grey-box-strategy-screen/ui-shots/01-watch.jpg)

![The decision inspector](grey-box-strategy-screen/ui-shots/03-inspector.jpg)

### The screen

The map fills the screen. Everything else floats over it, small and dense, with a tooltip on
every number and icon.

| Region | What it shows | What it does |
|---|---|---|
| **Top bar** | Watch mode: each country's crest and name, then provinces, army, gold and net income a turn, stability as five pips, and wars as a count. On the right: the turn and phase, play and pause, step, speed, diplomacy, the chronicle | A country's tooltip gives the arithmetic (income 10 a province, upkeep 1 per 2 soldiers); a click opens its panel. The leader comes first; in rule and advise modes the bar is your own country in full, with the rivals in a compact strip ([the top bar by mode](#where-commands-live-as-the-game-grows)) |
| **Realm** (left) | The standing against 15, wars and the turn each began, truces and their turns left, armies as bars, neutral land and its garrisons | A click on a line finds it on the map. It collapses to a strip, and starts collapsed below 1600 px |
| **Map** | 25 provinces over painted terrain, the capitals with towers, forts as small towers, every province named | Drag to pan, wheel to zoom. Hovering a province shows its owner, forts and defence, and whether the focused country may attack it, and why not. A click opens its panel |
| **Map modes** (bottom left, keys 1 to 5) | **Political**; **Armies** (colour strength by army size, counts at the capitals); **Forts**; **Reach** (who can attack what: a chosen country's legal attacks, hatched, each with the share of dice rolls it wins); **Terrain** (the painting alone) | A legend sits above them |
| **Province panel** | Owner, defence and what makes it up, income, borders, the line a model reads when this province is a target, the focused country's moves here or why not, and the province's history | In rule mode each move it offers has an **Order** button |
| **Country panel** | The ruler: the persona, its description from `Personas.cs`, its two faces (pick one), the model and provider config. Then the standing facts it reads every turn, word for word; the treasury; the moves it is offered and those it is not, with reasons; and its decisions | A decision opens in the inspector |
| **Diplomacy** (key D) | A 5 × 5 matrix: war since a turn, truce with turns left, or peace, and whether the two share a border | Each cell's tooltip says what either side may do |
| **Chronicle** (right, key C) | A compact ledger by turn: a crest, the line the game writes, a tag (land, guard, refused), and the ruler's words under it | Filters by country and by kind. A click finds the place on the map and opens the decision. The quote-mark button hides the rulers' words |
| **Speech** | A small banner at the speaker's capital: the persona's face, the country's colour, the name and persona, the line | It stays on the capital through pan and zoom. A click opens the decision |
| **Decision inspector** | See below | Opens from a chronicle line, a banner, a country panel or advise mode |

### The decision inspector, the showcase

Six numbered sections. Under each, in small code type, are the names of the framework fields it
shows:

1. **What the model answered**: the raw JSON in its field order (action, target, statement).
   For a contained decision, it also says that the agent ran `none` and showed no statement.
2. **The moves it was offered**: each offered move as a chip, with its choice outlined, then
   what was **not** offered and the reason for each (`TargetsUnavailable`,
   `ITargetAvailability`). The page computes them with its port of `Legality.WhyNot`, on the
   state the ruler saw.
3. **What it was told**: the standing facts (`IObservationSource`), then the stimulus and this
   turn's facts (`ObservationPlacement.WithStimulus`), with the counsel in advise mode, and
   anything dropped.
4. **Guards**: `SchemaLegalityGuard`; `TargetGroundingGuard`, with the grounding share on a bar
   against the 60% threshold and, when it fires, its reason word for word; developer guards; and
   `Agent.Execute`'s re-check when the move landed: Done, or Refused with the reason ("we are not
   at war with ilvar").
5. **Memory**: exchanges sent, held but left out, and forgotten.
6. **Model and time**: model, provider config, persona and face, tokens in and out, and a bar
   that splits the queue wait (behind the other council calls on one model) from the model's own
   time.

Its footer says every value comes from `DecisionFeed.Recorded`, and that the game adds only the
layout.

### The three modes, as the page plays them

- **Watch**: Play runs turn 12 as [a turn on screen](#a-turn-on-screen) describes:
  - Valen's envoy and Kesh's refusal are spoken at the capitals.
  - The attacks land in seat order. Ilvar takes Duskvale, Kesh takes Harrow, and Orsk takes
    Ilmoor, each with the capture animation.
  - Merrow's attack on Duskvale is then refused, because Ilvar now holds it. The arrow breaks,
    and the chronicle strikes the line through.

  Step lands one move at a time. The speed is 1×, 2× or 4×.
- **Rule**:
  - Pick a country. The orders panel lists only the moves the rules offer it, and a disabled
    move's tooltip says why.
  - Attack and Fortify ring the legal provinces on the map. Attack also switches to the reach
    mode, which shows each target's odds.
  - End turn resolves your order with the four model rulers' moves. Each move is re-checked as it
    lands, so yours can make theirs stale.
  - If you rule Kesh, Valen's envoy waits for your Accept or Refuse.
- **Advise**:
  - Write a line of counsel to Kesh's ruler and send it. The page shows the ruler's reply over
    the map and opens the inspector, with your counsel in the request as a fact of this turn.
  - The replies are scripted from keywords.
  - Asking for war on Orsk shows the rule that matters. `declare_war orsk` was not offered,
    because Orsk does not border Kesh: **counsel cannot make an illegal move**.
  - End turn plays the advised move.

### Where commands live as the game grows

The lead approved round 2 ("Yes, build on it") and asked where the buttons will go once the game
has more in it. The rule: **every command has one home, chosen by what it acts on**. A new
feature takes the home its object already has, so nothing is bolted on later.

| What it acts on | Its home | What lives there now | What goes there later |
|---|---|---|---|
| **The game as a whole** | **The top bar's right end**: small icon buttons, each with a key | Time controls (play, step, speed), the turn, diplomacy (D), the chronicle (C) | Ledgers that open a full panel: the economy, trade and research, buildings, and events (each with a key; the event list is the game master's work, decision 11). Settings and saving go behind one menu button at the far right |
| **Your own country** (rule and advise) | **The top bar's left**: your values, each opening its panel on click | Provinces, army, treasury, income, upkeep, stability, wars, truces | Buildings and trade fill the two places already kept for them. A value that needs a decision (a deficit, an unhappy province) gets a small alert mark that opens the panel where it is fixed |
| **Every country** | **The realm list**, left; a click opens the **country panel** | Standing, wars, truces, armies, neutral land | Alliances and tribute under wars and truces. A rival's panel gets a **diplomacy row**: declare war, offer peace, propose an alliance, demand tribute, and **talk** (decision 10). Every move offered to you sits there, and every move not offered says why, as the orders panel does |
| **One province** | **The province panel**, opened by clicking the map | Holding, defence, the moves on it, history | **Build** (decision 12) and **move an army here** (decision 8). The map shows development as small marks on the province, not as buildings |
| **This turn's orders** (rule) | **The orders panel**, bottom centre, ending in **End turn** | The moves the rules offer you, then the order and End turn | Several orders a turn, if the rules allow them, as a short queue above End turn. Each order still comes from a panel; the orders panel only collects them |
| **Talking to a ruler** | **A conversation panel** that replaces the orders panel while it is open | Advise mode's counsel box | Negotiation with a rival in free text (decision 10) uses the same panel, with the rival's face and replies in character. The deal it reaches lands as ordinary moves the rules check |
| **Something that happened** | **The chronicle**, and the map where it happened | Lines, filters, click to locate, the inspector | Events from the game master appear here as lines, with a banner at the province they touch |
| **Why a ruler did something** | **The decision inspector**, from any line, banner or panel | The six sections | Unchanged: it is the framework's window, and grows only as the framework records more |
| **How you look at the map** | **The map-mode bar**, bottom left, keys 1 to 9 | Political, armies, forts, reach, terrain | Development, trade, alliances and fog of war (decision 13) become modes; the political mode keeps capitals, forts and armies on it (decision 19) |

**Keys.** Every panel and every map mode has one. Space plays and pauses. Enter ends the turn
in rule mode. Esc closes the top panel. The keys are listed in the about panel, and each
tooltip shows its key.

**Not used, on purpose:** floating toolbars, buttons painted on the map, and a radial menu. A
command lives in a panel's header or its rows, in the top bar, or in the orders panel.

**The top bar by mode.** Watch mode keeps the five countries side by side. You rule nobody, so
nobody comes first. In rule and advise modes the top bar is **your country in full**, as the
lead asked: provinces against 15, army, treasury with its change a turn, income, upkeep,
stability, wars and truces, plus two places kept for buildings and trade. The four rivals
shrink to a compact strip of crests on the right, each showing its province count and whether
you are at war or at truce with it. A click opens its panel, and the realm list keeps the full
comparison. In rule mode a rival's army will hide behind fog of war (decision 13), so the strip
does not show it.

### Two styles, one layout

**Gilt**: dark panels, gold hairlines, small capitals, the map as painted. **Engraved**: ivory
panels, dark-brown ink with red-ink accents, an engraver's typeface, and the map warmed and
lightened beneath. The layout and the map are the same in both, and the switch is instant.

### What building it taught, for the Unity shader

- **Tint the hue, not the paint.** Laying the country colour over the painting with plain alpha
  either buried the terrain or left the five colours too weak to tell apart; the page tried
  both. A hue-and-saturation replace (CSS `mix-blend-mode: color`) recolours the painting and
  keeps its light and shade, so the mountains and forests read through every country. The ink
  (borders, the pooled edge, hatching) goes on a second layer with normal blending. The Unity
  shader should do the same: a `color`-style blend for the wash, alpha for the ink.
- **The province map can be made from the rule graph by a script.** The page's 25 provinces
  are a 5 × 5 grid warped with noise: four vertical and four horizontal curves, none crossing
  another of its own kind, cut by the painted coastline. A last pass fixes the odd pixel where
  two diagonal provinces touch. Checked against the rules, the adjacency matched exactly. That
  proves the editor tool [the art approach](#the-art-approach-for-unity) proposes. The script is
  `.scratch/strategy-stage3/build_idmap.py` on the machine that made it; the province map
  itself is inside `index.html`.
- **Country borders redraw themselves**, from a distance field to the pixels whose neighbour
  has another owner. They are recomputed once a capture settles.
- **The CPU is fast enough here.** The page composes 1.6 million pixels on the CPU in about
  40 ms, so the GPU will not notice the work.

### Text-to-speech, later

The lead plans to give the rulers voices ([decision 26](#the-leads-decisions-11-november-2026),
under research). It is not designed here. Two notes for when it is:
the voice follows the persona, as the face does, and the banner's reading time (1.5 s plus
40 ms a character) becomes the line's spoken length.

### What round 2 changes in the rest of this page

- **Superseded:** the round-1 interface (the regions table below, and images 5 to 7, now
  deleted), the round-1 recommendation of look A, the name pick (Inkwash), and the four
  questions the lead answered.
- **Still the design:** how the colour change should feel and its timings, a turn on screen,
  what each mode is for, statements and the chronicle, court scenes, and the art approach, now
  with the tint lesson above.
- **Image 8**, the court scene, stays as the mood for court scenes, though it is drawn in look A.
- **Assets:** the map is one painting with dense terrain (`ui/map.jpg` is a draft). Portraits
  belong to the persona: three faces each with three expressions, by the lead's decisions 20 and
  21 below (`ui/portraits.jpg` holds ten drafts, two faces each), not five countries × three
  moods.

## The lead's decisions, 11 November 2026

The lead answered 28 design questions about the game as a whole, beyond this screen. His words are
quoted where he gave them; "ok" means he accepted the question's proposal. The last column says
whether round 2's mock-up already meets each decision. The mock-up is **not** redesigned for them
yet; the next round will be. Items 11, 22 and 26 are **under research** in a separate session.

| # | Topic | Decision | Mock-up |
|---|---|---|---|
| 1 | Session length | "Should depend on the settings and the map sizes. I think we can target the 30 minutes for now." | Not measured. 30 turns at 1× would be about 3 to 8 minutes of playback, plus the council's model time |
| 2 | Map size | "We should have different settings, lets focus on smaller that we have now." *Our reading, not his words: several sizes as a setting later; build the current 25-province size first.* | Meets: 25 provinces |
| 3 | Maps | "One hand made map and detailed one first, with interesting geo locations, for future we can create a generator or create more hand made ones" | Partly: one map, but a generated painting over a warped grid, not a hand-made map with designed places |
| 4 | Setting | An invented late-medieval world, no magic: ok | Meets |
| 5 | Winning | "the honest one would be to capture the most provinces and hold them, but we can have something like a king of the hel, where there is one like wonder in AoE which you need hold long enough to win maybe" | Partly: 15 provinces wins, as the rules say. A wonder to hold for several turns is not drawn |
| 6 | Tone | Slightly playful: ok, "we may tweak that later" | Meets, in the statements |
| 7 | Economy | "will definetely need a reasearch but we should also make sure models will understand how to use it" | No: the economy is still gold, income and upkeep |
| 8 | Armies | Armies as pieces on provinces: ok | No: armies are a number per country, shown only in the armies map mode |
| 9 | Diplomacy | Alliances and tribute: ok | No: war, truce and peace only |
| 10 | Negotiation | Free-text negotiation with another ruler, answered in character: "Yes, perfect" | No: advise mode talks to your own ruler, not to a rival |
| 11 | Events | Not a fixed deck. "Lets actually make this something dynamic that model will create, it will make each run unique. Like I want to allow model to create events which have like bonuses or minuses to 1 or more contries. I want our framework to support that kind of usage too, this can become one of the gold points of the framework. In simple words, we should have a tools for the model that is going to be a master teller in the game to create all kinds of flavorable situations that would make each run unique and interesting, for example (a golden meteor fall out of the sky)." **Under research.** | No: the fixed events of the rules (harvest, bandits, unrest) |
| 12 | Buildings | "I would like to have buildings to be one of the big parts of the game and I want them to be shown on the map. Not particualry the buildings but how some provinces are like better developed, like in victoria 3 kind of" | No: forts only |
| 13 | Fog of war | In rule mode; everything visible in watch: ok | No: rule mode shows everything |
| 14 | Mode order | Watch is the main mode, then rule: ok | Meets |
| 15 | Rule mode's turn | The others move at once, then the resolution is played back: "Ok, we will maybe change that" | Meets |
| 16 | Advise mode | The model ruler may refuse in character: ok | Meets: Kesh refuses peace with Valen in its own voice |
| 17 | Difficulty | By persona mix and starting land: ok, and "why not to add a model. It can be like model presets because for some models you may want them to work differently" | Partly: each seat shows its model; choosing models or presets is not drawn |
| 18 | View | Flat 2D with zoom and pan: ok | Meets |
| 19 | Map modes | "different map modes like in EU5 or victoria, but there should be the main one which gives you the most info still, like the political map that still shows fortress icons and etc." | Meets: five modes, and the political mode shows capitals, forts, names and garrisons |
| 20 | Expressions | Three expressions per portrait: ok, "maybe smth more eventually" | No: one each |
| 21 | Faces | Three faces per persona: ok | Partly: two each |
| 22 | Country identity | "Lets have some kind of generator of each, this way we can also see what are the generated apsects of the country and we can try to use qwen image model to generate them a flag or even their persona." Whether that is possible, and whether it belongs in the framework, is to be looked into. **Under research.** | No: five fixed countries |
| 23 | Animation | Ok, "more animation is better I think" | Partly: the capture, arrows, envoys and pulses |
| 24 | Inspector | On click, with a small thinking indicator: ok | Partly: the inspector opens on click; the only thinking indicator is the council's, in the top bar |
| 25 | Music | Candidates generated both without and with the asset server, and he picks. Never generate while tests run on the card | Not in scope |
| 26 | Text-to-speech | Wanted. Is there a local text-to-speech model that can be configured? **Under research.** | Not in scope |
| 27 | Audience | "Presentation first, but if it turns out very fun, I am thinking about actually developing it nicely and maybe make revenue with it in future" | Not in scope; see the licence note below |
| 28 | Platform | Windows, mouse and keyboard; 1920 × 1080 first, 1366 × 768 must work, and "make sure we support 4k too eventually" | Partly: checked at 1920 × 1080 and 1366 × 768, not at 4K |

**Met:** 2, 4, 6, 14, 15, 16, 18, 19. **Partly:** 3, 5, 17, 21, 23, 24, 28. **Not yet:** 7, 8,
9, 10, 12, 13, 20; and 11 and 22, under research. **Outside this mock-up:** 1, 25, 26
(under research) and 27.

**What these change in the rest of this page:**

- **Licences matter from now on (item 27).** Revenue is possible, so the licence of every
  generated or bundled asset, font and model has to be recorded where it is added, starting
  with this page's drafts. The fonts the page uses are open-licence (the SIL Open Font
  Licence). The images came from image models whose terms have to be checked before any of
  them ships.
- **Portraits (items 20, 21):** five personas × three faces × three expressions, 45 portraits,
  in place of round 1's 15. The asset list below is updated.
- **Text-to-speech (item 26)** is wanted; [its note](#text-to-speech-later) stands until the
  research reports.
- **Events (item 11)** would come from a model acting as a game master, not from the fixed
  deck in [the rules](grey-box-strategy.md#the-rules). The capture sequence and the chronicle
  would show them like any other event. Nothing is designed until the research reports.
- **The map (items 3, 8, 12, 13)** gains armies as pieces, visible development, and fog of war in
  rule mode. The province map keeps its rule: it is built from the rule graph and checked
  against it, whether the map is drawn by hand or generated.
- **Winning (item 5)** may gain a wonder to hold. That is a rules change for
  [`grey-box-strategy.md`](grey-box-strategy.md), measured in the simulation first, as every rule
  there was.
- **4K (item 28):** text and icons are drawn at device scale, and the map painting needs a
  4096-wide master, as [the asset list](#assets-with-rough-counts) already plans.

## The mock-ups (round 1)

Eight images were made with an image model on 11 November; images 5 to 7, the round-1
interface, are deleted (see round 2). **They are drafts, not art**: generated
images stay drafts until a person puts them in a game.

| # | Image | Shows |
|---|---|---|
| 1 | [`1-look-sepia-atlas.jpg`](grey-box-strategy-screen/1-look-sepia-atlas.jpg) | **Look A, warm sepia atlas page.** Watch mode, turn 12. Muted earth washes, iron-gall ink, wax seals |
| 2 | [`2-look-storybook.jpg`](grey-box-strategy-screen/2-look-storybook.jpg) | **Look B, bright storybook map.** The same moment with bright washes, painted trees, hills and a sea serpent |
| 3 | [`3-look-sea-chart.jpg`](grey-box-strategy-screen/3-look-sea-chart.jpg) | **Look C, dark aged sea-chart.** Jewel washes on dark vellum, gold-leaf country borders, brass and leather panels |
| 4 | [`4-capture-sequence.jpg`](grey-box-strategy-screen/4-capture-sequence.jpg) | **The centrepiece in four steps**, look A: strike, fall, spread, settle |
| 5–7 | *deleted* | The round-1 interface (the capture on the full screen, rule, advise), superseded by round 2's page |
| 8 | [`8-court-truce.jpg`](grey-box-strategy-screen/8-court-truce.jpg) | **A court scene** for a truce, over the dimmed map |

![Look A, the recommended look](grey-box-strategy-screen/1-look-sepia-atlas.jpg)

![The capture in four steps](grey-box-strategy-screen/4-capture-sequence.jpg)

**What the images get wrong**, so nobody builds it from them:

- **The geography is the image model's, not the board's.** The real board is the 5 × 5 graph in
  [`grey-box-strategy.md`](grey-box-strategy.md#the-map), where each province borders only its
  four orthogonal neighbours. The images draw more than 25 shapes in places, and diagonal
  provinces touch. [The art approach](#the-art-approach-for-unity) says how the real map keeps
  the rule graph exactly.
- Province counts in the images do not add up to the strip's counts.
- The line at Keshtar in images 1 to 3, "Harrow must bleed for daring to stand near our sacred
  soil", is real: kesh, the proud ruler, said it in the persona 4b table's seed 4. Every other
  line is written for the mock-ups.

*Superseded: the lead chose C.* Round 1 recommended **A, the sepia atlas**, with B's clarity in one respect, the labels and
panels. A is the lead's brief taken literally: paper, ink, watercolour. Its muted washes leave
room for the capture to be the brightest thing on screen (a fresh wash can be painted a little
more saturated, then dry to the muted tone). B reads fastest, but its painted terrain competes
with the colour for the eye, and its bright washes leave the capture nowhere brighter to go. C is
the most dramatic, but the dark sea and gold borders make five colours harder to tell apart, and
its leather panels are the most art to make. Images 4 and 8 are drawn in look A. Round 2 answers
the colour worry: a hue-only tint keeps five colours apart on the dark chart.

## The screen's regions (round 1, superseded)

*Replaced by [round 2's screen](#the-screen). Kept for the record.*

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

Five model rulers play; the player watches (round 2's screenshots 01 to 04). Pause, step one move, and three
speeds. Pausing also pauses the council, so a paused game asks no model. Every banner, chronicle
line and seal opens its Why card. A table is chosen before the game: the model for each seat and
whether personas are on, as `Tables.Named` already defines them, so a viewer can set the 4B
against the 2B and watch the difference.

**Done when** a viewer can watch a whole game at 1× without the screen waiting on the model, and
every capture plays the sequence above.

### 2. Rule

The player rules one country against four model rulers (screenshots 05 and 10). The side column becomes **your
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

The player is the counsellor of one model ruler (screenshot 06). Each turn, before the council, the
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
country's colour, with the ruler's round portrait at the left end (screenshots 01 and 06). Rules:

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
| | ruler portraits by persona: 5 personas × 3 faces × 3 expressions (decisions 20, 21) | 45 |
| **Court** | hall background | 1 |
| | rulers at the table, half-body, one pose per side for each country | 10 |
| | treaty scroll and table props | 1 to 2 |
| **UI** | parchment panel, ribbon banner, letter (counsel box) as 9-slices | 3 |
| | buttons in four states, the move icons (recruit, fortify, raise taxes, declare war, attack, offer peace, hold, accept, refuse) | 4 + 9 |
| | fonts, open licence | 2 |
| **Sound** (later) | brush stroke, drum, wet swish, seal thud, quill scratch, page turn, war horn, peace bell | about 10 |
| | ambient loop and one music loop | 2 |
| **Fallback only** | painted province washes, five colours | 125 |

About 120 pieces of art, most of them small, plus the fallback's 125 if it is needed. The
portraits and the court figures are the largest single cost. Each asset's licence is recorded
when it is added ([decision 27](#the-leads-decisions-11-november-2026)). The lead's decisions
add armies as pieces, development shown on the map, and music. They are not counted here until
they are designed.

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

**The lead chose Painted Realm** (11 November), for now. Round 1 picked Inkwash. The folder
and the issue are renamed only when the name is final.

## Open questions for the lead

Answered in round 1: the look (C), the name (Painted Realm, for now), terrain (a lot) and
portraits (by persona, with several faces each). Still open, each answerable in a word or by
pointing at a screenshot:

1. **Which style?** Gilt (screenshot 01) or Engraved (screenshot 08).
2. **A capture at 1× takes 2.4 s** (screenshots 02 and 02b). Right, slower, or faster?
3. **Court scenes for accepted peace only, or for declarations of war too?**
4. **Is the screen's density right?** More, the same, or less.
5. **Is the country colour's strength right** (screenshot 01)? Stronger, the same, or lighter.
6. **Is the decision inspector for every player, or behind a toggle for developers?**
   Recommended: every player. It is the showcase.
