# Painted Realm: the lead's direction, 17 November 2026

The lead played the game on 16 November and answered 56 questions the next morning. This page
records his answers and the plan that follows from them. Where a session and this page disagree,
this page wins until the lead says otherwise. His own words are quoted; everything else is the
tech lead's reading.

The game is `Demos/GreyBoxStrategy/`. The measured description of today's turn is in
[`painted-realm-turn-flow.md`](painted-realm-turn-flow.md) once that page merges (#351); it was
written before these answers, so its recommendation is superseded here.

## What matters most

- **This week: "The turn feels free and has things to do."**
- **Who the demo is for first: developers judging the framework.**
- **What it must show.** "For me what matters the most is to be sure we showcase that personas do
  different things depending on the situations, like thats an actual showcase of using LLMs rather
  than scripts and behavior trees."
- A game lasts 30 to 45 minutes when played. About 60 provinces on a generated world. The painted
  map is fine for now.

## The turn

| Question | Answer |
|---|---|
| The writs limit | Gone. No action budget: every army and every province may be acted on each turn |
| Human and model realms | The same rules |
| Order of play | Everyone orders on the same snapshot and the turn resolves together. One phase, not two |
| Waiting after End turn | Up to 5 seconds. "We can allow them to sync when we plan, that way we wont be waiting on them" |
| Playback | About 10 to 15 seconds, everything at once on the map (today about 37 s) |
| Rulers' lines | Only when a ruler acts or something big happens, not every ruler every turn |
| Camera | Stays where the player is; jumps for big events such as a war declared, where the ruler's line is also spoken |
| Turn summary | A small strip, always visible, one row a realm, a click replays it |
| A guard stopping a decision | A clear shield mark and a line: "it is the framework's point" |
| The inspector | Opens only on a click |

## Economy

Light: gold, a few buildings, upkeep.

- Buildings are bought at once with gold, no build queue: **fort, market, barracks, farm**.
- A second resource, **manpower**, limits recruiting.
- Provinces differ by terrain (plains farm, hills mine, coast trade).
- Taxes are a standing level: low, normal, high.
- No trade between realms now.

## Armies

- Orders wanted first, after march, merge and split: **fortify, disband, reinforce**.
- Movement points, simple: one or two provinces a turn by terrain.
- The odds before a battle as a rough word: likely win, even, likely loss.
- Generals stay a small bonus and a name. No experience. The cap of 100 soldiers stays. No navies.
- Automation: "we can just like have a button to give it to general and general will decide what
  is better to do, but we should still be able to give it direct orders".

## Controls and screen

- **Right click orders the march; left click selects.** (This reverses the answer of 15 November.)
- Edge scrolling when the mouse touches the screen border.
- The lead's screen: a 16:10 laptop panel, 3K or 4K, at about 150% scaling. Design and check at
  1920×1200, 1707×1067 and 2560×1600 as well as 1920×1080 and 1366×768.
- Map modes on the bar: Political, Armies, Forts.
- Hints on the first turn; no tutorial yet. No difficulty setting yet.
- Saving and continuing a game in the middle is needed.

## Storyteller

- Every event stops the turn until dismissed, for now: "I want to check them and see each one".
- Events on the player's realm offer a choice of two or three options.
- How often events come, and how unfair the storyteller may be (a little, and it says so), are
  settings.
- A short epilogue written by the model at the end, under the highlights.

## Art, sound and voice

- The generated art pack (#353): "super nice I really like it". Painted, late-medieval, muted with
  gilt stays the style. A fixed pack by default, pictures made live as an option.
- Flags: keep the code-drawn crests, "leave the possibility to craft the flag with code predefined
  stuff and also allow to generate emblems".
- Music: a quiet ambient loop.
- Voice: off by default for rulers' lines; a click on a phrase in the chronicle speaks it. On by
  default, with a setting, for the storyteller's stories, events and large events.
- A downloaded neural voice when present, Windows voices as the fallback. The lead will install a
  speech model on the RTX 5080 machine if asked what to install.
- The title on the start screen is "Painted Realm" for now.

## Diplomacy

Talking to a ruler in the player's own words: "Yes, this is the showcase."

## How the work runs

"3 on opus 5.5 medium or one on fable 5.2 (for really hard and important work or a research)":
at most three sessions at once on Opus 5.5 at medium effort, or one on Fable 5.2.

## The turn rework, in slices

Each slice is a pull request or two into `test`, behind `GameRules` switches while it is built,
and on by default once its tests and one live check pass: the lead has decided these rules, so a
live run records what a turn costs, it does not decide whether the rule stays. Findings measured
on the writs turn are not comparable with runs after slice 1; every later entry says which turn
it ran on.

1. **Free orders, and rivals that decide while you plan.**
   - No writs for anyone. In a council you may give any number of court orders that gold and
     manpower allow, and order every army.
   - A model court plays the same turn through several decisions on one snapshot: court orders
     one at a time until it answers `none` (a cap of three a turn to start), then one decision for
     each of its armies that has no standing order (marching, besieging and fortified armies keep
     theirs and are not asked).
   - Those decisions run in the background from the moment the council opens, so End turn waits
     only for what is left, with a visible count ("2 rulers still deciding"); the target is no
     more than 5 seconds.
   - Measured in one live run on `qwen3.5:4b`: legal answers, decisions and model seconds a turn
     early, mid and late game, and how long End turn waited.
2. **Economy.** Manpower; the four buildings bought with gold; terrain yields; the tax level as a
   standing setting. Model courts are offered the same purchases.
3. **Armies.** Movement points by terrain, the odds word, fortify, disband, reinforce, and
   "give it to the general": an army handed to its general is decided by a model agent with the
   general's own character, until the player orders it directly again.
4. **Telling the turn.** Playback of 10 to 15 seconds with everything moving at once, the summary
   strip, action marks on the map, the camera jumping only for big events, the guard's shield.
5. **Later, in this order:** save and continue; event choices; free-text diplomacy; first-turn
   hints; the epilogue.

Every invented number above (the cap of three court orders, the 5 second target as a hard rule)
is a starting sketch to be tuned by play.
