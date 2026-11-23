# Painted Realm: the UI rework, a first draft (15 November)

The lead, after playing: "the main points we should adjust now are UI and controls … make sure it matches EU4 or EU5
or victoria. The left panel is kind of strange, we are missing icons and colors on top and your like flag on the
left. The chronicle on the right can be refactored maybe too and we can make councill feel even more better."

He asked to see the layout before it is built out. This is variant A, built; variant B, which goes further, was shown as pictures, then chosen on 16 November and built (#340, #343, #348), and is in [`painted-realm-ui-variant-b.md`](painted-realm-ui-variant-b.md). The frames of both are in
[`painted-realm-ui/`](painted-realm-ui/). This draft is real code running in the game; the frames are
taken from it. It matches the grand-strategy games in structure and density and copies none of their art. Each
section below says what moved and why, and the questions at the end were put to him before he chose B on 16 November.

## The four places on the screen

| Place | For | Opens with |
|---|---|---|
| Top bar | Your realm in numbers, the time, the controls | Always there |
| Left panel | One thing in detail: your realm, a province, an army, a rival | A click on the map, an army, a crest; "‹ Realm" or Esc goes back |
| Map | Where things are and what moves | Always there |
| Right column | The chronicle; the council in rule mode beside it | Always there; the council while you give orders |

The inspector, which shows each framework decision, opens over the map. To reach it, click a chronicle line, click
**Decisions** in the top bar, or press **I**. It is the showcase, so it is never more than one click away.

## Top bar

**Before:** small text segments: "PROVINCES 3 / 25", "ARMY 30 · 1", "TREASURY 55 +15". There was no icon, no colour,
and only a small crest.

**Now:**
- Your shield, larger, then your realm's name.
- Each number with an icon and a colour:
  - treasury, gold, "560 −15 a turn";
  - soldiers, steel, "90 · 1 army";
  - provinces, green, "3 of 60";
  - writs left, seal red, "3 of 3 writs left";
  - stability, with its five pips;
  - wars, red;
  - truces, blue.
- Each number's tooltip breaks it down:
  - the treasury into income per province and upkeep per soldier;
  - soldiers army by army, with where each stands and where it marches;
  - provinces by name;
  - writs by the rule that sets them;
  - each war's score from your side.
- On the right:
  - the rivals' crests, with their province counts and a red sword when at war;
  - **Decisions**, which opens the inspector.
- The **Buildings** and **Trade** placeholders are off the bar. They were greyed "not in the game yet" segments, and
  are better in a roadmap than on the player's screen.

Frames: `u1-council-topbar-1080.png`, `-4k`.

## Left panel

**Before:** the realm ledger stayed on the left, and a province or country opened as a second panel beside it, covering
the map. An army had no panel of its own.

**Now:** the left panel shows one thing at a time:
- **Realm** (the default): standing, wars with their scores, truces, armies, and neutral land.
- **Province**: owner, defence, forts, the land, and your orders there. It opens when you click a province.
- **Army**: its crest and name, strength and mix, general and traits, stores, orders, besieging, and battles fought
  (each opens its report). It opens when you click an army, your own or a rival's you can see. A rival's shows only
  what your court sees, such as a size rounded to 10, as its piece does.
- **Rival**: its ruler, persona, what you know of it, and its decisions. It opens when you click a crest.

**"‹ Realm"** in the panel's head, or Esc, goes back. The map keeps all the room to the right.

Frames:
- `u2-left-province`
- `u3-left-army`
- `u5-left-rival`
- `u7-realm-chronicle`, the realm view

## Chronicle

**Now:**
- Each entry carries an icon for its kind beside the speaker's crest:
  - land (a flag);
  - war (crossed swords);
  - peace (a seal);
  - gold;
  - building (a hammer);
  - refused (a struck circle);
  - guard (a shield);
  - story (a quill).
- The filter chips carry the same icons.
- A ruler's orders still group under its name with what it said.

**Next, if he agrees:**
- a "Me" filter on by default in rule mode, so your own events are not buried;
- the rulers' words folded under each entry rather than toggled for all.

Frame: `u7-realm-chronicle`.

## Council

The council is kept as he likes it. In this draft only:
- the arrows (below);
- the top bar's writs-left stat, which counts the writs as the council does.

Polish proposed for after his answer:
- the writs as seal icons;
- a header with your shield;
- each army row's state as an icon (awaiting, marching, besieging, hungry) before its text.

## Marches as arrows

The planned arrow was right; the arrow under way was weak on its owner's own land (Kesh's blue over blue). It now has:
- a brighter body;
- a heavy ink edge;
- a light spine down its middle.

It holds on any colour. Turns tags of marches that end together stack instead of overlapping.

Frame: `u8-arrows-own-land`.

## Controls

| Input | Does |
|---|---|
| Left click on a province | Opens it in the left panel; with an army chosen, orders its march there (as before) |
| Left click on an army | Opens it in the left panel; your army with a writ is also chosen for orders |
| **Right click** on a province (new) | With an army chosen, orders its march there, as the grand-strategy games do |
| Right or middle drag, left drag on the map | Pans |
| Wheel | Zooms at the pointer; over a panel it scrolls the panel |
| Hover | Tooltips on every number, piece, crest and button |
| Space | Pause or play |
| N | Next army awaiting orders (rule mode); a step otherwise |
| Enter | End turn |
| 1, 2, 3, 4 | Map modes: political, armies, forts, reach |
| D | Diplomacy |
| **I** (new) | Decisions: the inspector |
| Esc | Closes what is on top, then goes back, then the pause menu |
| F1 | Dev menu (editor and development builds) |

One inconsistency is left for him to decide: a left click on a province both opens it and, with an army chosen,
orders a march. With right-click orders, a left click could keep to selecting, as in the games he named.

## Icons

They are drawn by code in one flat-ink style tinted per use, which reads well at 11 to 17 pixels on the dark chart:
- swords, quill, seal, hammer, scales, shield, struck circle and hourglass (new);
- flag, coin, fort, eye, scroll and truce (existing).

The codex-image skill is kept for what code cannot draw well, such as realm banners or larger emblems, if he wants
them.

## Questions for the lead

1. Left click on a province with an army chosen: keep ordering the march, or only select, leaving orders to the right
   click?
2. The top bar's numbers: is the set right? Prestige-like scores do not exist in the rules; the realm's standing (its
   rank) could be added.
3. The left panel: is one panel at a time right, or should the realm ledger stay visible under a province or army?
4. The chronicle: a "Me" filter by default in rule mode?
5. Icons: code-drawn as here, or generated images in one style for the larger ones (banners, the top bar's shield)?
