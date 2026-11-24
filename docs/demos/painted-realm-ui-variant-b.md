# Painted Realm: the UI rework, variant B (15 November)

Variant A, in [`painted-realm-ui-draft.md`](painted-realm-ui-draft.md), is built and running: the old screen with icons,
colours, a shield and a left panel that holds one thing. The tech lead's read was that it still looks like the screen
the lead called strange, with icons added. Variant B goes as far as the lead asked: "completely refactor it and make
sure it matches EU4 or EU5 or victoria".

**Status, 16 November: B was chosen and is built**, in three slices (#340, #343, #348); the build is described in
[`grey-box-strategy-screen.md`](grey-box-strategy-screen.md#the-ui-rework-variant-b-16-november-2026) and the demo README.
What follows is the proposal as it was shown, before the lead chose.

B was shown as pictures only. Each one is drawn over a real frame of the game, a medium generated world with all four army rules
on, at turn 8. None of it was built in Unity then. The lead was to choose A, B or a mix, and only that would be built.

It matches those games in structure and density and copies none of their art. The emblems are drawn by code in one
style for the dark sea chart. Portrait slots are framed placeholders; generated portraits and banners come later.

The frames are in [`painted-realm-ui/`](painted-realm-ui/), `b-*.jpg` beside `a-*.jpg`.

## What differs from A

| | A (built) | B (pictures) |
|---|---|---|
| **Top bar** | A 42 px strip; your shield at 30 px; icons at 17 px, numbers at 15 px | 74 px high. Your realm's banner hangs below the bar, with a ruler's portrait slot beside it. Coloured two-tone icons at 34 px, numbers at 25 px, a caption under each. A framed date block with pause, speed, Decisions and Menu |
| **Your realm's overview** | The left panel's default: standing, wars, truces, armies, neutral land as lists | An **outliner** at the top right: your armies with their stores as loaves, your sieges and your wars. The ledgers of realms, armies and land sit behind three buttons. The left side of the map is free until you choose something |
| **Left panel** | Text rows and sentences in a flat black box | A framed panel with corner ornaments and a header band with the crest and a portrait slot. Tabs with icons. Rows built from an icon, a label, a bar or pips, and a number on the right. Sentences move to tooltips: four loaves, not "Stores: 4 of 4 turns; refilled in your own land." |
| **Army** | Strength, general, stores and orders as text | The mix as three bars (pikes, riders, bows), the general with a face slot and the trait's effect, stores as loaves, and orders with a "New orders · a writ" button |
| **Rival** | The ruler's persona text and its decisions | The same, with a portrait in the header. Its words appear as a speech bubble on the map by its land, not only in the chronicle |
| **Chronicle** | A full-height column, a fifth of the screen, three-line entries | A narrow feed at the bottom left, one line each, with the speaker's crest and an icon for the kind. An entry opens in place to show what was said and a link to its decision. Filters: All, Me, War, Land. Full text and tales sit behind Tales |
| **Council** | Kept as it is | Kept, framed like the other panels. Your writs show as three seals in its header, an army's stores as loaves in its row, and End turn is larger |
| **Inspector** | Opens over the map from a chronicle line, Decisions or I | A framed window over the map, with tabs for its five parts: Answer, Offered, Told, Guards and Timing. It opens from Decisions, I, a chronicle entry, a rival's decision or a speech bubble |

## Frames

- `b-top.jpg`: the top bar, the outliner, the chronicle feed, the council, and a speech bubble on the map.
- `b-province.jpg`: Haldfell in the left panel, with tabs for Holding, Land, Orders and History.
- `b-army.jpg`: Edda's host, with its mix in bars, the general, stores as loaves, and orders.
- `b-rival.jpg`: Othow, with its portrait slot, what your court knows, its ruler, its decisions, and its words on the
  map.
- `b-chronicle.jpg`: the feed with one entry opened in place.
- `b-inspector.jpg`: the inspector as a framed window with tabs.

## What B costs to build, roughly

- **Top bar, outliner and the panel frame:** a new layout and styles, reusing today's data. About a day.
- **Rows built from icons and bars:** a row kit (icon, label, bar or pips, value, tooltip) used by every panel. About a
  day.
- **The chronicle feed:** a new compact row and one entry opening in place. Half a day.
- **Speech bubbles linked to the inspector:** the bubbles exist; the link is small.
- **Portraits, banners and the frame's ornaments:** placeholders now. Generated art later, through the codex-image skill
  in one style, as a separate stage.

## Questions for the lead

1. A, B, or a mix, and which parts?
2. The outliner at the top right with your realm's overview, and the left side empty until you choose something: yes?
3. The chronicle as a narrow one-line feed at the bottom left: yes, or keep a full column?
4. Portrait slots now as empty frames, with the art later: yes?
