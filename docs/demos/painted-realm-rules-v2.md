# Painted Realm (GreyBoxStrategy), rules v2: the deeper game

**Status:** a design, 12 November 2026 (#20), being built slice by slice: **R0 is built** (12 November,
[below](#r0-settings-and-the-map-as-data)), and **R2's headless generator** ([below](#r2-the-map-generator-headless-and-a-drawn-chart-spike)). It is written so that build
sessions can each take one slice ([the build](#10-the-build-as-slices)) without inventing rules.
The game as it is today, with its measurements, is in the demo's
[`README.md`](../../Demos/GreyBoxStrategy/README.md) and in
[`grey-box-strategy.md`](grey-box-strategy.md). The screen is designed in
[`grey-box-strategy-screen.md`](grey-box-strategy-screen.md), and the storyteller, voices and
generated identity in [`painted-realm-research.md`](painted-realm-research.md). The folder keeps
its name (the lead, 12 November: "Lets not rename the folder for now").

**Revised the same day** with the lead's answers to [this page's eight questions](#11-the-leads-answers-to-this-pages-questions).
Conquest is now occupation followed by a peace treaty ([5.1](#51-occupation-war-score-and-treaties)).
Three later slices are planned: orders without a cap ([1.6](#16-the-route-to-no-cap)), a world
shaped by a model through the framework ([2.8](#28-a-world-shaped-through-the-framework)), and
names and histories written by a model ([2.9](#29-names-and-histories-written-by-the-model)). There
is also research on making the units our own ([3.11](#311-making-the-roster-our-own-research)).

**How to read it.** The lead's words are quoted where they decide something. His 34 answers of
12 November are written out in full [at the end](#the-leads-answers-12-november-2026). **Every
choice of this page's own is marked _Mine_**, so the lead can overturn it without hunting for it.
Numbers marked *to tune* are starting values for the simulation, which sets the final ones by
200-game runs, as stage 1 set every number in `GameRules`. Nothing here copies another game's
text, names, numbers or art. Where an idea is common to the genre, [the sources](#sources) say
where it was read.

## In plain terms

Today a country makes one move a turn and has one army number. In v2 a country is a realm with
**several armies standing on the map**, each led by a general, made of three kinds of soldier,
marching one province at a time over terrain that slows them and starves them. Forts must be
besieged. Provinces have people, food, a trade good and buildings that show on the map as how
developed the land looks. An army that takes a province **occupies** it, and the land becomes
the conqueror's only when a **peace treaty** cedes it. Rulers make deals with each other and hold
opinions of each other with reasons you can read. The map is **generated from a seed** at any size, and a model adds special
places with their own stories.

A turn works the way the lead asked: "in kind of the way that Civilization does it, like where you
can do multiple actions per turn". Each ruler gets a few **writs** a turn. The court spends one on
the realm's business, and each army that is waiting for orders spends one. Armies that already
have an order (a march, a siege) carry it out on their own. The default game is conquest with no
turn limit.

The hard constraint is the model. One `qwen3.5:4b` serves every ruler, one request at a time, at
about half a second a decision. Six rulers making one decision each is 3 seconds a turn; six rulers
each commanding four armies one by one would be 15 seconds or more. The design keeps a turn of six
rulers at **about 7 seconds of model time on average and 14 at worst** before treaties, and every decision a
closed list of about a dozen moves, the shape the 4B is measured to handle. Treaty answers in
wartime add about 0.7 s on average and up to 5.5 s at worst, so the totals are 7.3 to 8.7 s on
average and 19.3 s at worst ([1.2](#12-how-a-model-ruler-takes-such-a-turn-five-ways)).

## Contents

1. [The turn](#1-the-turn)
2. [The map generator, and how the map is drawn](#2-the-map-generator-and-how-the-map-is-drawn)
3. [Armies](#3-armies)
4. [Economy and buildings](#4-economy-and-buildings)
5. [Diplomacy and the realm](#5-diplomacy-and-the-realm)
6. [Winning and length](#6-winning-and-length)
7. [Settings and presets](#7-settings-and-presets)
8. [What each mechanic asks of the models and the framework](#8-what-each-mechanic-asks-of-the-models-and-the-framework)
9. [How it will be measured](#9-how-it-will-be-measured)
10. [The build as slices](#10-the-build-as-slices), with [issue-ready checklists](#issue-ready-checklists)
11. [The lead's answers to this page's questions](#11-the-leads-answers-to-this-pages-questions)

## What the design has to respect

These are measured (`docs/llm-wiki/findings.md`, and the demo `README.md`), not preferences:

| Fact | What it forces here |
|---|---|
| `qwen3.5:4b` takes 0.46 s median, 0.55 s at the 90th percentile, a decision with a persona and a 13-word statement, on the RTX 5080; plain, with 38-word statements, 0.65 s | Model time grows with the number of decisions and the length of what is written, so the turn caps decisions and keeps most statements short |
| One loaded model serves every ruler; the server answers one request at a time | Decisions add up; they never run side by side |
| The 4B chooses well from a closed list of action and target pairs with the paired schema: 0 illegal answers | Every new decision is a closed list. No free numbers, no free targets |
| It needs the legal moves **as a line among its facts** (`Moves open to us: ...`); the framework's own listing did not replace that line | Every decision kind gets its own moves line, written by the game |
| It reads about 800 to 1,400 prompt tokens today | Facts stay local: neighbours, not the whole world |
| A persona explains 84% of a ruler's aggression (η² 0.84) | The personas stay as they are; new decisions must leave room for them to show |
| Statements invent grievances about land never taken | Opinions with **true reasons** go into the facts ([section 5](#5-diplomacy-and-the-realm)), which also tests whether that helps |
| Long target lists are where the grounding guard needed 20 alternatives and a 0.6 threshold | At most about 12 targets per decision |
| A model served through Ollama never reads the schema | Anything the model must know is in the prompt |
| A decision is one action, one target and one statement. No typed parameters (#266 item 2, #190 LV #12) | Quantities, unit types and peace terms are spelled into action ids or fixed by the rules. [Section 8](#8-what-each-mechanic-asks-of-the-models-and-the-framework) flags every place this bites |
| One storyteller call per about 460 ran away for 20 s (N12) | With about 15 decisions a turn, that is one stall every 30 turns. A reply cap (#244 item 11) moves from nice to needed |

What stays from stages 1 and 2: dice seeded per purpose (a roll is a hash of game seed, turn, who
and purpose), every rule as one `WhyNot` function used by the offered list and the re-check, all
countries deciding on one snapshot, and `Sim/` with no UnityEngine at all. The generator is
`Sim/` code too.

## 1. The turn

### 1.1 What a country does in a turn

**Writs.** A country has a number of **writs** each turn: sealed orders its court can send.
Each order costs one writ. _Mine:_ the name is ours, the idea of a shared pool of orders is the
genre's (Old World's "orders", [sources](#sources)).

- **Writs a turn** = 2 + 1 for every 8 provinces held, at most 5 (*to tune*). A starting realm of 3 or 4
  provinces has 2; a realm of 24 has 5. Unused writs are lost.
- **Why a cap at all.** It bounds model time (below), and it makes attention a resource: a big realm
  cannot order every army every turn, so standing orders matter.

**What costs what:**

| Thing | Costs | Notes |
|---|---|---|
| A **court order**: recruit, build, raise taxes, declare war, offer peace, propose a deal, break a deal | 1 writ, and gold where the rule says | One court order a turn always comes first; a second only if writs are left after the armies |
| An **army order**: march, hold, merge, split | 1 writ | Only for an army **awaiting orders** (below) |
| An army **carrying out a standing order**: marching on, besieging on, holding | free | It costs movement, stores and upkeep, as an army always does |
| **Answering** an envoy, a proposal or an ally's call to arms | free | The answer is asked of the ruler as a separate decision, as envoys are today |
| The **statement** | free | One proclamation a turn, on the first court order ([1.4](#14-statements)) |
| Upkeep, food, income, sieges ticking, attrition | free, automatic | Applied by the rules |

**Standing orders.** An order lasts until it is done or broken, as a unit's long move does in
Civilization:

| Order | Lasts until |
|---|---|
| `march` to a province (one or many steps away; the path is the rules' shortest one) | it arrives, or the path is blocked, or it fights a battle |
| marching into a walled hostile province | becomes a siege, which lasts until the walls fall or the army leaves or loses |
| `hold` | something wakes it (below), or 4 turns pass |

**An army is awaiting orders** when it has no order, or one of these **wake conditions** fired
since its last order (_Mine_):

1. it arrived, or its path became blocked;
2. it fought a battle (won, lost or held);
3. a siege it led ended;
4. a hostile army entered a province bordering it;
5. its country declared war, was declared war on, or made peace;
6. its stores fell to 1 or 0 ([supply](#36-supply));
7. it was recruited this turn, or merged or split;
8. 4 turns passed since its last order.

An army awaiting orders that gets no writ **holds**, and keeps waiting. The screen marks it.

**When the turn resolves.** Every country decides on the same snapshot, then the turn resolves at
once, in this order (_Mine_, extending today's peace, home moves, wars, attacks):

```text
1 Upkeep       truces and deals count down; income, food and upkeep; stores refill or run down;
               attrition; events land; revolts
2 Council      each ruler: court order, then orders for armies awaiting them, then a second court
               order if writs remain                                  (all on one snapshot)
3 Answers      envoys, proposals and calls to arms are answered       (same snapshot)
4 Resolve      a. diplomacy: peace, deals, alliances, declarations and breaches
               b. home: recruits appear, building work starts or finishes, taxes
               c. movement: every marching army takes its step at the same moment
               d. battles: wherever hostile armies now share a province, or crossed one border
                  in opposite directions
               e. sieges tick; an undefended enemy province entered by a hostile army is occupied,
                  neutral land entered is taken (5.1)
5 Check        eliminations, victory
```

- **Moves land at the same moment** (_Mine_). Today countries take turns within each phase in
  rotating seat order, which was fine for one move each. With armies, a sequential order would let
  the first seat's army step out of a province before the second seat's army arrives. Stepping
  everyone at once is fairer and needs no seat order for movement. Diplomacy and home moves keep the
  rotating seat order, as today.
- **A crossing on one border.** Two hostile armies that march into each other's provinces across
  the same border meet on it (_Mine_). Neither province gets terrain's defence bonus. The winner
  completes its step, and the loser stays in the province it was leaving. This never needs a seat
  order.
- **Every order is re-checked when it lands**, with the same `WhyNot` that offered it, as today. A
  march through a province that became hostile this turn stops before it.

**In rule mode** a person gets the same turn: the court's orders in the orders panel, a "next army
awaiting orders" key, each army's order from its panel, writs shown in the top bar, and End turn.
The screen page already kept a short queue of orders above End turn for this. **A person is held
to the same cap** (the lead: "Yeah lets have the same cap but to be honest I would eventually wont
to have no cap of orders. We can have smth like civ has."). "Unlimited writs for the player"
stays an easy difficulty setting, now a rule: `GameRules.WritsOverride` gives one country its own count, or
`UnlimitedWrits`. [1.6](#16-the-route-to-no-cap) designs the route to no cap.

### 1.2 How a model ruler takes such a turn: five ways

Numbers for **six rulers** (the default medium map) on the 4B. Two decision costs are used:

- **t_c = 0.46 s**: a court decision with a persona statement, as measured (median).
- **t_o = 0.30 to 0.46 s**: an army order. The low end is an estimate, **unmeasured**: an army
  order writes no statement or a few words, and, when one ruler's orders run back to back, the
  prompt's long shared beginning may be reused by Ollama rather than read again (the last
  request's prefix is kept per slot). The high end assumes neither helps. [R1](#r1-the-turn-of-writs-and-armies-as-pieces)
  owes a probe.
- **Armies awaiting orders**: 1.0 a ruler a turn on the 25-province map and 1.4 on the medium map
  are estimates. The simulation counts them exactly with scripted players before any model run.

| Way | Decisions a ruler a turn | Model time a turn, 6 rulers (average / worst) | Prompt a decision | Risks |
|---|---|---|---|---|
| **A. One plan decision; code commands the armies.** The ruler picks an aim (`attack` a province, `defend` one, `build`, `recruit`...) and a scripted marshal moves the armies toward it | 1 | 2.8 s / 3.3 s | ~1,100 | The persona never touches an army: what the lead called "too dull" stays dull for the model's part. The demo stops showing the framework commanding pieces |
| **B. Every army and the court, every turn** | 1 + armies (2.5 average at medium) | 7.3 to 9.7 s / 16.6 s with 5 armies each | ~1,100 | Grows without a bound as realms grow. Most decisions are "keep marching", which the 4B's high hold share turns into idling |
| **C. A fixed pool of K = 3 orders, for any piece** (closest to Old World) | 3 | 8.3 to 10 s / 10 s | 1,800 to 2,400: every army's every move on one list | One decision must name an army **and** a destination: a typed parameter, or compound target ids (`maren_host>fenmark`) that the grounding guard has never been measured on. The longest lists measured anywhere |
| **D. A plan decision, then two orders narrowed by it** | 1 + 2 | 6.4 to 8.3 s / 8.3 s | ~1,100 | The plan is an extra decision whose only effect is to filter lists; a wrong plan starves the orders. A fixed two orders whether or not anything needs one |
| **E. Orders when needed, under a writ cap** (recommended) | 1 court + armies awaiting orders (cap: writs − 1) + sometimes a second court order | **6.6 to 8.0 s / 13.8 s** (16.5 s at the 90th percentile latency); with treaty answers 7.3 to 8.7 s / 19.3 s | court ~1,500, army ~1,100 | Armies past the cap hold and wait; the 4B's tendency to hold may leave armies idle. A probe and the sim must show standing orders carry the game |

How E's numbers are made: average 6 × (t_c + 1.4 t_o + 0.3 t_c) + 0.5 s of answers = 6.6 to 8.0 s;
worst, every ruler at 5 writs, 6 × 5 × 0.46 = 13.8 s. For **eight rulers** (the large map) multiply
by 4/3: 8.8 to 10.7 s average, 18.4 s worst. For **four rulers** (the small map) by 2/3: 4.4 to 5.3 s.

**Treaties add answers** ([5.1](#51-occupation-war-score-and-treaties)).
- **Proposing** a treaty is a court order, so it is already counted.
- **Answering** one is a separate decision (_Mine_, estimate). In wartime about one ruler in four
  answers a proposal each turn, so 6 × 0.25 × 0.46 ≈ **+0.7 s** on average: E becomes **7.3 to
  8.7 s**.
- **The worst case** is every ruler answering two proposals, the most a ruler answers in a turn
  (5.1): 6 × 2 × 0.46 = +5.5 s, so **19.3 s**.
- An enforced treaty asks nothing.

**Recommendation: E.** _Mine._ Why:

- **It is the Civilization feel the lead asked for**: pieces with long orders carry them out, and
  the ruler is asked about the ones that need it. Several orders a turn, without asking about every
  piece every turn.
- **Every decision is about one thing**, so it stays one action and one target from a list of about
  a dozen: `march` with a province, for **the army named in the question**. No typed parameter, no
  compound id. The army is carried in `DecideOptions.Correlation`, the pattern the envoy call
  already uses, so per-target availability can read which army is asking.
- **It is bounded.** The writ cap fixes the worst case; the average is about 2.5 times today's turn.
- **It hides behind the screen.** The resolution is instant and the screen plays a turn back, so
  the next council already runs while the last turn is shown (the live game does this today). A
  turn with marches and a battle plays for about 4 to 8 s at 1× (_Mine_, estimate), so most of E's
  model time is never seen. In rule mode the model rulers decide while the person gives orders,
  which takes longer than 8 s.

### 1.3 The order of decisions within a turn

For each ruler, in this order (_Mine_):

1. **The court order** (always asked; `none` allowed). It carries the turn's proclamation.
2. **Each army awaiting orders**, most urgent first: a hostile army beside it, then a siege it leads
   or that holds it, then stores at 1 or 0, then the rest by size. One writ each, until writs − 1 are
   used (one is always kept for the court).
3. **A second court order**, if a writ is left and the first was not `none`.

Each decision after the first reads one more line, `Ordered this turn: recruit at highmoor; maren_host
to march to corwick.`, so the ruler does not repeat itself. Nothing lands until the turn resolves,
so a ruler's orders cannot see each other's results, and no ruler sees another's.

**One ruler at a time.** All of one ruler's decisions are sent one after another before the next
ruler's (_Mine_). Rulers decide on one snapshot, so the order between rulers changes nothing in the
game, but back-to-back requests that share a long prompt beginning are where the server's prefix
reuse can help. The framework's `DecisionRound` holds an agent once (N10), so this is several
rounds, one per writ slot, or a short chain per ruler written by the game; [section 8](#8-what-each-mechanic-asks-of-the-models-and-the-framework)
records it.

**Two agents per country: the court and the marshal** (_Mine_). Both carry the same identity and
persona; the court is offered realm moves, the marshal army orders. Keeping them apart:

- keeps each prompt's list of actions short and fixed, so the prompt's beginning is the same for
  every army order of that ruler;
- gives each its own memory, so three army orders a turn do not push the court's last decisions out
  of a six-exchange history (a gap noted in section 8);
- costs nothing on the server: an agent is a prompt, and both use the one loaded model.

### 1.4 Statements

- **The court's first order carries the turn's proclamation**, under the persona's statement rules,
  as today. It is what the chronicle and the speech banner show.
- **Army orders** get a rule of their own: _Mine_, "a few words, or nothing" (a general's line,
  shown small by the piece). Writing less is most of t_o's saving. An arm with no statement at all
  is measured beside it.
- **Answers** keep today's envoy statements.

### 1.5 What a ruler reads, literally

The court, turn 14 on a medium map (_Mine_, the wording to be checked against the 4B):

```text
Turn 14. We hold 9 of 60 provinces; conquest wins. Writs: 3 this turn.
Treasury: 84 gold; income 52, upkeep 31 a turn. Food: +4. Stability: 4 of 5.
Our armies: maren_host 52 at fenmark, marching to corwick; tolly_host 30 at highmoor, awaiting orders.
orsk: at war with us; about 90 soldiers in 2 armies, 8 provinces; hostile (-45): took ashby from us, at war.
merrow: at peace; about 40 soldiers, 6 provinces; wary (-10): borders us.
kesh: truce for 3 more turns; about 70 soldiers, 11 provinces.
5 neutral provinces border us.
Moves open to us: recruit pikes, riders or bows at highmoor; build walls at fenmark or ashby; build a farm at brindle; raise taxes; offer peace to orsk; declare war on merrow.
```

The marshal, for one army (the stimulus is `Orders for tolly_host.`):

```text
tolly_host: 30 soldiers (20 pikes, 10 bows), led by Tolly the Wary; at highmoor (hills, ours); stores 4 of 4.
Seen: orsk's army of about 40 at corwick, 2 provinces away. stonereach (neutral, garrison 12) borders highmoor.
Ordered this turn: recruit pikes at highmoor.
Orders open to tolly_host: march to stonereach, ashby, corwick or orskhaven; merge with maren_host; hold.
```

Only what the country can see is written ([fog of war](#38-fog-of-war)).

### 1.6 The route to no cap

The lead: "Yeah lets have the same cap but to be honest I would eventually wont to have no cap of
orders. We can have smth like civ has." So writs are the first version (R1). Slice
[R8](#r8-orders-without-a-cap) removes the cap.

**Without a cap,** as in Civilization:
- every army may take an order every turn;
- the court may give as many orders as the treasury pays for;
- what limits a realm is what it has: gold, soldiers, armies, stores, and the turns a march takes.

The person gets exactly that: no writs, the "next army" key, End turn.

**What it does to a model ruler's turn.** Each decision stays the same size: it is still about one
army or one court matter, so prompts stay at about 1,100 and 1,500 tokens. The **count** grows with
the realm. Every army awaiting orders is now asked, and the court is asked again until it answers
`none` or cannot pay. Estimates for six rulers on the medium map (_Mine_; t_o as in 1.2):

| Stage of the game | Armies a ruler | Asked a turn (awaiting) | Court orders | Model time a turn, 6 rulers |
|---|---:|---:|---:|---|
| early | 2 | 1.0 | 1.5 | 6.0 to 6.9 s |
| middle | 5 | 2.5 | 2 | 10.0 to 12.4 s |
| late, two large realms of 10 armies, four middle ones | 10 / 5 | 5 / 2.5 | 3 / 2 | about 14 s |
| late, all six large | 10 | 5 | 3 | 17 to 22 s |
| for comparison: every army asked every turn, all six large | 10 | 10 | 3 | about 36 s |

**Keeping it affordable**, each measured in R8:

1. **Standing orders and wake conditions** (from R1) already keep most armies quiet most turns.
2. **Automation an army can be left on.** Three marshal orders that run themselves, using the
   scripted players' own tactics for one army:
   - **`guard`** (a province): stays, returns after a battle, wakes only if it loses or the province
     falls;
   - **`campaign`** (a country at war): marches on that country's nearest provinces it can occupy,
     besieges, falls back when hungry; wakes on a rout, on peace, or every 8 turns;
   - **`watch_border`** (no target): moves toward the realm's nearest threat.

   An army on automation is asked nothing until it wakes. This is the biggest saving, and it is
   also a showcase: a model handing a piece to code with one decision.
3. **Groups.** Armies in one province or bordering ones, with the same order, form a group with one
   decision for all of them. A `follow` order (target: another of our armies) moves with it.
4. **A decision budget a ruler a turn,** hidden from the rules: at most 8. Armies past it carry on
   with their order or their automation. It is counted in decisions, never in seconds, so a seed
   plays the same on every machine. The person has none.
5. **The "ordered this turn" line** lists only the last five orders, so later decisions do not grow.

With automation and the budget, the late game is estimated at 10 to 12 s a turn for six rulers. R8
measures it ([9.3](#93-the-ab-each-slice-owes)).

## 2. The map generator, and how the map is drawn

The lead: "we need to start with the map generator already, to allow any form and size of it. WE
can also use LLM model to design it maybe, in text way." And: "there should be also some special
provinces which LLM may add up to the world. Like sepcial lakes, special mines, special old
castles".

### 2.1 Who makes what

| Part | Made by | Why |
|---|---|---|
| Land shape, provinces, borders, adjacency, terrain, coasts, ports, capitals, starting realms, trade goods, population | **seeded code** | The rules, the balance and every measurement depend on them; a seed must give the same world on every machine |
| Province names | **seeded code** first: syllable tables per region, checked. **The model later** ([2.9](#29-names-and-histories-written-by-the-model), slice R10) | Names are target ids, and odd names were measured fragile for target choice (findings, 5 October); so the model's names come with checks and an A/B |
| Which provinces may be special, and which effect each may carry | **seeded code**: slots and a closed list of effect templates | Strength stays the game's, as with the storyteller |
| Which template each special place carries, its name and its story | **the model**, one decision per special place | Flavour and variety; a closed choice the 4B handles |
| The world's name and a one-line legend | **the model** (optional) | Flavour only, never read back to a ruler as fact |
| Country names, colours, flags, rulers | as the research page's section 3 says: tables and code, the model for flavour; names and histories by the model in R10 | Already designed there |
| Which world among fair candidates, great features, each country's character and where it starts | **the model, later** ([2.8](#28-a-world-shaped-through-the-framework), slice R9), choosing among options the code made fair | The lead wants maps generated "maybe using the framework" |

"In text way", read precisely: **the model writes text and chooses from closed lists; it never
writes geometry or numbers.** _Mine._ A model asked for a map in text would give a description
that code must still turn into adjacency and balance, and it would not be the same on two runs.

### 2.2 Settings the generator takes

| Setting | Values | Default |
|---|---|---|
| Seed | any integer | random, shown, re-usable |
| Size | small ~30 provinces, medium ~60, large ~100, huge ~150 | medium |
| Countries | 3 to 10; the size sets a default (4, 6, 8, 10) | 6 |
| Land shape | `continent`, `two_lands` (two lobes joined by a narrow neck), `inland_sea` (a ring of land around a sea), `peninsula` (land open to one edge), `long_coast` (a long land along a sea) | `continent` |
| Neutral land | share of provinces neutral at the start, 30% to 70% | 55% (today's board is 40%) |
| Special places | none, few (1 per 15 provinces), many (1 per 8) | few |
| Starting realm | 2 to 5 provinces each | 3 |

All land is one connected whole in v2, because there are no fleets (the lead accepted "ports and
sea trade now, fleets later"). Islands are drawn as scenery only.

### 2.3 The steps, from seed to world

Every step draws its randomness from the seed and the step's name, as the dice do, so changing one
step's rule does not reshuffle the others. Floating-point noise is avoided: _Mine_, integer hash
noise and fixed-point maths, so a seed's world is the same on every machine (a test pins a hash).

1. **Land.** A height field on a grid (about 400 × 225 cells for medium; grows with size): several
   octaves of value noise plus a falloff that makes the chosen shape (radial for `continent`, two
   centres and a saddle for `two_lands`, a ring for `inland_sea`, a ramp for `peninsula` and
   `long_coast`). Land is the cells above a level picked so the land covers the share the size
   needs. Small detached land and lakes below a size are filled or drained; only the largest land
   stays playable.
2. **Province seeds.** Points on land by Poisson-disc sampling at the spacing the province count
   needs, moved twice toward their cells' centres (Lloyd relaxation) so provinces are even.
3. **Provinces.** Each land cell joins its nearest seed by a distance bent with low-frequency noise,
   so borders wander like hand-drawn ones. A province split into pieces keeps its largest piece and
   gives the rest to neighbours. Then **checks**: every province's area between 0.5 and 1.8 times
   the mean, no province thinner than a set width, and none touching another only at a corner.
4. **Adjacency** comes from the cells: two provinces border when they share at least 3 cell edges
   (so a corner contact is never a border). The rule graph is derived from the drawing's own
   source, so the two cannot disagree; the screen page's rule ("built from the rule graph and
   checked against it") holds by construction.
5. **Ridges.** One to three ridge lines across the land, placed by the seed, cut the borders they
   cross, except at one or two **passes** each. They make fronts and chokepoints. A cut is refused
   if it would split the land graph.
6. **Terrain**, per province, from height and a moisture noise: the highest become **mountains**, then
   **hills**, the wettest low ones **marsh**, wet ones **forest**, the rest **plains**. Quotas keep a
   world readable (*to tune*): plains 35%, forest 25%, hills 18%, marsh 10%, mountains 12%, by ranking
   provinces rather than by thresholds, so every seed has every terrain.
7. **Coasts and ports.** A province touching the sea is coastal. Ports go on coastal provinces with a
   long enough coast, about one in three of them, spread out.
8. **Capitals and starting realms.** Capitals are chosen by farthest-point sampling on the province
   graph: no capital on mountains or marsh, none on the land's edge, every pair at least 4 steps
   apart on medium (3 on small). Each country then takes the next nearest provinces from its capital
   until it has the starting size, never one that borders another country's start.
9. **Trade goods and population** ([section 4](#4-economy-and-buildings)), from terrain tables. The
   scarce goods (horses, iron) are placed so that no starting realm holds more than one, and at least
   one of each lies in neutral land between realms, which gives a reason to expand and to trade.
10. **Neutral garrisons**, 8 to 16 as today, plus 4 per terrain step of defence ([3.2](#32-terrain)).
11. **Special places**: slots, then the model ([2.5](#25-special-places)).
12. **Names** for provinces and regions from syllable tables, one table per region (a region is a
    cluster of about eight provinces), so a region's names sound alike. Checked: unique, a valid id
    (lowercase letters only, 4 to 11 letters), no two neighbours starting with the same three letters,
    not a real country, not on a block list.
13. **Fairness checks** (below); on failure, the next sub-seed, up to 50 tries, then the best one
    found, with its scores logged.

### 2.4 How a generated map is checked

**Connectivity** (a failure is a bug, not a retry): the land graph is connected, every capital
reaches every other, every province has at least 2 borders except at most one in twenty, no border
crosses a ridge except at a pass.

**Fairness** (_Mine_, every number *to tune*). Each capital's **reach** is the provinces nearer to it, in
steps on the graph, than to any other capital. For every country:

| Measure | Allowed spread between the best and worst country |
|---|---|
| Provinces in its reach | at most 1.3 to 1 |
| Value of its reach: food + gold + 1 per trade good, from the same tables the rules use | at most 1.25 to 1 |
| Neutral provinces it borders at the start | at most 2 to 1, and never 0 |
| Rival capitals within 6 steps | at most 2 apart |
| Defensible borders (hills, mountains, marsh, ridges on its reach's edge) | reported, not checked |

**Balance by play**, for presets and for a seed people will share: the same 200-game run of
scripted players as stage 1, on that map. No starting position may win more than twice its fair
share, nor less than half of it. On the 25-province board 200 games take about 3 seconds; on a
medium map with armies it is estimated at about 20 seconds, so it is a tool ("check this map")
and the presets' certificate, not a step of every new game.

### 2.5 Special places

**Code picks the slots.** About one province in fifteen (the "few" setting), never a capital,
never in a starting realm, at least 4 steps apart, at most one per region. Each slot has the
templates its terrain allows.

**The templates** (_Mine_, names ours; the numbers are the game's and *to tune*):

| Template | Terrain | Effect while held |
|---|---|---|
| `old_castle` | any but marsh | walls level 2 from the start; +10% defence; a neutral garrison of 30 |
| `silver_mine` | hills, mountains | +8 gold a turn |
| `iron_deep` | hills, mountains | provides iron; recruits there cost 25% less |
| `sacred_lake` | plains, forest, hills | +1 stability for its holder, at most once |
| `healing_spring` | any | armies there refill all stores and lose no men to attrition |
| `horse_plains` | plains | provides horses; riders recruited there cost half |
| `great_wood` | forest | an army there is invisible to rivals; +15% defence |
| `old_battlefield` | plains, hills | the first battle fought there each war gives the winner's general a trait |
| `crown_seat` | any | the Crownhold victory's province ([section 6](#6-winning-and-length)); +1 writ to its holder |

**The model's part.** A *world-maker* agent, the storyteller's kind (no body, DR-011), is asked once
per slot at world creation: action = a template the slot allows, target = the slot's province (so
the paired schema keeps it to fitting pairs), statement = `Name: story` (*"The Drowned Abbey: a
lake over a sunken chapel whose bells still ring in winter."*), checked by the storyteller's story
check (no digit, no other place's name, at most 40 words), with the template's own line as the
fallback. Its facts: the slot's terrain and neighbours, the region, and the places already made.
For a medium map that is about 4 calls, 2 to 3 seconds before the first turn, shown as "the
chronicler is drawing the map". With the model off, the templates are rolled and named from
tables.

**Province traits** (the lead accepted "two or three per province" for events): rolled by code,
from a list per terrain (`pilgrim_road`, `border_feud`, `old_mine`, `river_ford`, `fever_marsh`...),
with no effect of their own. They are what the storyteller's events can attach to
([R5](#r5-events-with-choices-and-chains)).

**The world's legend** (optional): one more call, action = the land shape the code already chose
(so the model cannot change it), statement = `World name: one sentence`. Shown on the menu and
the chronicle's first page.

### 2.6 How the map is drawn, by code

The lead: "for images its better to use code by you as much as possible and use codex image gen
when it is clearer a best way to do that." Today's map is one codex-image painting, upscaled, with
a province id map baked from it. A generated world has no painting, so it is drawn.

**One source, any resolution.** The generator's output is vector: each province a polygon (from
the cells, simplified, its edges subdivided with seeded midpoint noise, the "noisy edges" technique,
[sources](#sources)), the coast, ridges and rivers (R2's generator now makes rivers, see its follow-up below). At load the screen **rasterises the
province id map at the size the display needs** (2048 wide at 1080 p, 4096 at 4K) on the CPU, so it
is sharp at any size. The existing shader's inputs stay as they are: the id map, the edge and
distance fields baked from it, and the state texture, which grows from today's fixed 32 columns
(`MapView.StateColumns`) to one column per province.

**The sea-chart look, layer by layer** (bottom to top; _Mine_):

| Layer | How |
|---|---|
| Paper | one tileable paper-grain texture and a low-frequency stain noise; **image generation is clearly best here** (one tile, codex-image or the asset server) |
| Sea | the dark chart tone, **coast ripples** as three to five offset contour lines from the coast's distance field, fading outward; **rhumb lines** from two or three compass roses placed in open sea, 16 or 32 lines each, thin ink, clipped at the coast; one compass rose drawn by code |
| Land base | a paper-light tone, darker toward the coast, with a hill-shade from the height field (soft, low contrast) |
| Terrain | **ink glyphs placed by Poisson sampling inside each province**, kept clear of its border and its label: mountains as hatched peaks with a shaded side, hills as low arcs, forest as clusters of small round trees, marsh as dashes with tufts, plains as sparse field strips. Glyphs are drawn by code into an atlas at device scale at start-up, in a few variants each. If the drawn glyphs look sterile next to the painting, a **stamp sheet from codex-image** (a sheet of trees, peaks, tufts in ink) is the fallback: the one place image generation may be best for terrain |
| Ridges | a chain of mountain glyphs along each ridge, the passes left open with a small pass mark |
| Wash | the owner's colour as today: a hue-and-saturation replace, darker toward the border (the screen page's lesson), so the terrain reads through it |
| Borders | from the id map, as today: a line between two owners heavier than one inside a realm; a coast line heaviest |
| Occupation | the occupier's colour as inked diagonal hatching over the owner's wash, and a pennant on the settlement ([5.1](#51-occupation-war-score-and-treaties)) |
| Development | the settlement glyph and its surroundings ([4.6](#46-how-development-shows-on-the-map)) |
| Armies, sieges, arrows | pieces above everything ([3.9](#39-armies-on-the-map)) |
| Labels | below |

**Labels.**
- **A province's name** sits at the point of its polygon farthest from its border (the pole of
  inaccessibility, from the distance field), sized to fit the largest circle there.
- **A realm's name** is set in widely spaced capitals along its realm's longest axis, recomputed
  when a capture settles.
- **Region names** in italics, shown only when zoomed out.
- **Collisions** are resolved greedily, larger labels first. A label that cannot fit hides at that
  zoom.
- **Levels of detail:** province names hide below a zoom, realm names below another.
- All text is UI-side text at device scale, never baked into a texture, so 4K stays sharp.

**Size.** On the huge map 150 provinces and about 9,000 terrain glyphs as instanced quads are well
within a 2D URP scene. The CPU raster at 4096 × 2304 is about 9.4 million pixels. The HTML mock-up
composed 1.6 million in about 40 ms, so this is well under a second once, at load.

**The classic board stays.** _Mine:_ today's 25-province world, with its painting, becomes the
**Classic** preset, with terrain assigned by hand to match the painting. Every measurement so far
was taken on it, and it stays the control for every A/B in [section 9](#9-how-it-will-be-measured).

### 2.8 A world shaped through the framework

The lead: "Ok, but as I said, I want to end up with the possiblity to generate maps and maybe using
the framework". So a later stage of the generator (slice [R9](#r9-the-model-shapes-the-world)) lets
a model act as **the world's designer**, through the framework's own decision shape: one action,
one target and one statement, the paired schema and per-target availability.

**The rule: code proposes, the model picks, code checks** (_Mine_).
- Every option the model sees has already passed the connectivity and fairness checks, or is
  re-checked the moment it is applied and refused if it fails.
- The model never writes a coordinate or a number.

| Step | Action | Target | Statement | What code does |
|---|---|---|---|---|
| 1. The idea | a land shape (`continent`, `two_lands`, `inland_sea`, `peninsula`, `long_coast`) | a climate (`cold_north`, `temperate`, `warm_south`, `dry_inland`, `many_rivers`) | the world's name and legend | Generates three candidate worlds for that idea from three sub-seeds. All pass the checks |
| 2. The world | `choose_world` | one of the three, each described in a line ("a long inland sea; two passes through the spine; marsh in the north") | why | Keeps it |
| 3. Great features, up to three | `great_lake`, `spine_range`, `old_road`, `river_delta`, `wasteland` | a region | `Name: story` | Applies the feature: a range cuts borders but keeps the land connected; a road makes steps cost one turn; a lake removes a province. It re-runs every check, and refuses a feature that fails |
| 4. Each country | a character (`seafarers`, `highlanders`, `river_folk`, `forest_folk`, `horse_lords`, `merchant_league`, `old_kingdom`) | a start site among the fair candidate sites code found. Availability is the fit rule: seafarers need a coastal site, highlanders hills | `Name: one line of history` | Places the realm. The character sets its own unit ([3.11](#311-making-the-roster-our-own-research)), its naming style, and which goods its land leans to, within the fairness spread |
| 5. Special places | as in [2.5](#25-special-places) | a slot | `Name: story` | as there |
| 6. Regions | a naming style | a region | the region's name | names ([2.9](#29-names-and-histories-written-by-the-model)) |

**What stays seeded code:** geometry, adjacency, terrain quotas, the candidate sites, every number,
and every check, including the balance run that certifies a preset.

**The character is not the persona.** The persona (how the ruler plays) stays the five measured
ones. A character's effect on what a ruler reads, such as a `Knowledge` line ("our riders are the
best in the land"), changes the prompt, so it waits for its own A/B.

**What it showcases.** A framework agent with no body designing a world:
- per-target availability as the fit rules;
- the paired schema keeping every pick legal;
- the story check guarding its text;
- the `DecisionLog` recording each choice with what was offered.

The menu gets **How this world was made**, which opens those decisions in the inspector. _Mine:_ the
world-maker gets its own identity, *the Cartographer*.

**Cost.** About 1 + 1 + 3 + 6 + 4 + 8 = 23 decisions, about 12 s on the 4B, plus about 3 s to
generate the three candidates. It runs on the loading screen with the names ([2.9](#29-names-and-histories-written-by-the-model)).

### 2.9 Names and histories written by the model

The lead: "Lets have model generate place names and history of provinces and countries eventually.
Like it would add a lot of colors to the game where you have new names each time, its like we
discussed with the country names". Tables are the first version (R2). Slice
[R10](#r10-names-and-histories-by-the-model) adds the model.

**What it writes, and how it is asked** (_Mine_). Each request is one decision, with the statement
carrying the text:

| What | Decisions | Action | Target | Statement |
|---|---|---|---|---|
| A region's name and its provinces' names | 1 a region (about 8 on medium) | a naming style (`harsh_north`, `soft_river`, `old_imperial`, `hill_clan`, `marsh_folk`, `coastal_trade`, `old_forest`, `open_steppe`) | the region | `Region: Name1, Name2, ...` in the order of a numbered list in its facts ("1 coastal plains with a port; 2 hills by the pass; ...") |
| A country's name, its capital's name and its history | 1 a country | its character (2.8), or the rolled one | the country | `Name; Capital: two sentences` |
| A one-sentence history for each province | 1 a region | `write_histories` | the region | `1: ...; 2: ...`, at most 120 words |

**Checks.** A name that fails falls back to the region's table name; a history that fails, to none.

- **Form:**
  - letters only after folding accents;
  - 4 to 11 letters, one word;
  - the id is the name in lowercase, and the display name is the same word capitalised, so what a
    person reads and what a model names are one word.
- **Unique:**
  - across every id (provinces, countries, regions, armies, generals);
  - no id a prefix of another;
  - none within one letter of an existing one;
  - no two bordering provinces sharing their first three letters.
- **Not reserved:** no action id, `none`, `no_target`, group id (`everyone`, `the_leader`...), or word
  the facts use (`army`, `peace`, `war`).
- **Safe to show:**
  - nothing on a block list of real countries, capitals and well-known places;
  - nothing on an unsuitable-word list (slurs, profanity, in several languages), matched as
    substrings.
- **Histories:**
  - at most 40 words each, with no digits;
  - they name only places and countries that exist;
  - the storyteller's story check applies.

**Exact references.** Names are fixed at creation, saved in the recording, and never change in a
game. The rulers' and the storyteller's facts use the ids. Histories are **never read by a ruler**,
being flavour in the province and country panels. The storyteller reads a province's history line
when an event strikes it, so it can refer to it, as the lead asked of events.

**When.** At game start, after the geometry and the world choices, on a loading screen.
- **What it shows:** "The chronicler is naming the world", a line a region, the names appearing on
  the drawn map as they arrive.
- **Budget on the RTX 5080:**
  - 8 region names × about 1.2 s (longer statements);
  - 6 countries × 0.8 s;
  - 8 history decisions × about 2 s;
  - **about 30 s** in all, or **about 45 s with the world choices of 2.8**.
- **Hard limit, 60 s:** what is left comes from the tables, and the game starts.
- **The lock and the fallback:** the lock is taken for it (a rule game holds it anyway). With no
  server, everything comes from tables.

**Determinism and the A/B.** At temperature 0 the same seed's facts give the same names on the
same server and model (the control re-run of 12 November reproduced 20 games move for move). Even
so, names are **cached per seed** in a names file. Every arm of an A/B then reads the very same
names, and two arms differ only in what they are meant to differ in.

### 2.10 Risks and fallbacks

| Risk | Fallback |
|---|---|
| The drawn chart looks sterile next to the painting the lead liked (look C) | A spike first ([R2](#r2-the-map-generator-headless-and-a-drawn-chart-spike)), judged by the lead from captured frames beside today's painting. Then a codex-image stamp sheet for terrain. Last, an image-to-image pass over the drawn map is **not** proposed: it would not keep borders where the rules have them |
| An unfair world | the fairness checks and retries; the presets carry a play-tested certificate |
| Slivers, pinches, corner contacts | the area, width and corner checks in step 3, before adjacency |
| Names that tokenise badly or look alike | tables, the checks in step 12, and an A/B before model-written province names are ever used |
| A seed that differs between machines | integer noise, fixed-point maths, and a test that pins one seed's map hash |
| The model's special places are dull or wrong | closed templates; the story check; table names as fallback |
| Generation is slow on huge | measured in R2's tests (target: under 2 s for 150 provinces on the laptop) |
| Labels collide on small screens | levels of detail; checked at 1366 × 768 and 4K, as decision 28 asks |

## 3. Armies

The lead: "the map should show armies at all times and I think armies should become very detailed
in ters of mechanics and usage, not its too dull." Accepted on 12 November: several armies, each led
by a general; three unit types with a simple counter system; one province a turn, slowed by terrain
(with animations); battles in one turn with a report; forts that turn battles into sieges; supply;
generals with traits; fog of war with totals visible.

### 3.1 An army

| Field | Values |
|---|---|
| id | the general's name as an id plus `_host` (`maren_host`). It stays when the general changes, so memory and the chronicle keep one name |
| owner, location | a country, a province |
| soldiers by type | pikes, riders, bows (whole numbers) |
| general | a name and one or two traits ([3.5](#35-generals)) |
| order and progress | march (destination, path, turns left on this step), hold, or siege |
| stores | 0 to 4 turns of food ([3.6](#36-supply)) |

- **How many.** As many as a country can pay for, as the lead accepted. The **upkeep** of 1 gold per
  2 soldiers stays, and riders cost double. _Mine:_ an army holds at most 100 soldiers (140 under a
  `marshal` general), which keeps one stack from deciding everything without a supply rule that
  counts stacks.
- **Recruiting.** A court order, `recruit_pikes`, `recruit_riders` or `recruit_bows`, at a province
  with barracks or at the capital. It raises one company of 10 (two with barracks level 2, three at
  level 3), cut to what the treasury pays. The company joins an army of ours in that province, or
  forms a new one with a new general.
- **Provinces without an army** defend with a militia of 5 plus 10 per walls level (*to tune*).
  Today's rule, where every province defends with 70% of the whole army, goes, since armies are now
  in places.

### 3.2 Terrain

Five types, each with what it does (_Mine_, every number *to tune*):

| Terrain | Turns to enter | Defender | Riders | Supply | Food | Notes |
|---|---:|---|---|---|---:|---|
| plains | 1 | — | +20% | rich | 3 | the most people and the most food |
| forest | 1 | +10% | −20% | rich | 2 | an army in forest shows only as "an army" to rivals, no size |
| hills | 2 | +15% | −20% | poor | 2 | bows +10% when defending |
| marsh | 2 | +10% | −30% | poor | 1 | +5% attrition outside our land |
| mountains | 2 | +25% | −30% | poor | 1 | mines; never a capital |

"Turns to enter" is how many turns a step into that province takes; the piece is drawn on the
border meanwhile. Coastal and port provinces take their land terrain's values.

### 3.3 Unit types and the counter system

Three types (_Mine_, the names): **pikes**, **riders** and **bows**. Pikes counter riders, riders
counter bows, and bows counter pikes. Each type has a base strength per soldier: pikes 1.0, riders 1.4,
bows 0.9 (*to tune*). Riders cost 4 gold a soldier (2 with horses), the others 2.

**The counter rule**, simple enough to explain in one line of a report. For each type t on a side,
its multiplier is

```text
m(t) = 1 + 0.5 × (enemy's share of the type t counters) − 0.5 × (enemy's share of the type that counters t)
```

So pikes facing an all-rider army fight at 1.5, and riders facing all pikes at 0.5. A side's
strength is the sum over its types of soldiers × base × m(t). The battle report names the largest
counter effect: *"our pikes broke their riders (+18%)"*.

### 3.4 Battles

A battle happens wherever hostile armies end the movement step in one province, or cross one
border going opposite ways ([1.1](#11-what-a-country-does-in-a-turn)). Allied armies in one province
fight as one side. A neutral province's garrison, and a militia, fight as pikes.

**Strength** of a side = Σ (soldiers × base × counter) × terrain × general × support × hunger × roll:

| Factor | Value |
|---|---|
| terrain | the defender's bonus from [3.2](#32-terrain); riders' modifier per type |
| walls | +15% a level for the defender, if it is besieged there and sallies out ([3.7](#37-forts-and-sieges)) |
| general | its traits ([3.5](#35-generals)) |
| support | +10% for each other army of the same side in a province bordering the battle, at most +20%. _Mine_, after Civilization's support bonus; priority 2 below |
| hunger | −15% for a side whose stores are 0 |
| roll | 0.85 to 1.15, seeded per battle and side, as today |

The higher strength wins. **Casualties** keep today's shape: each side loses 30% of the other
side's strength in soldiers, the winner only 60% of that. Losses come from each type in proportion.
**Pursuit:** the loser loses another 10% of its soldiers for every 20% of riders in the winner's
army, at most 30% (_Mine_). **Retreat:** the loser falls back one step to the province it came
from, or else to the nearest province it may enter. An army that has nowhere to go, or lost by more
than 2.5 to 1, is **routed**: destroyed, and its general captured or killed. A province with no
defender left is **occupied** at the end of the turn, unless it is walled. Neutral land is taken
outright ([5.1](#51-occupation-war-score-and-treaties)).

**The report**, written by code from the factors, the three largest first (_Mine_ wording). The ruler
reads it as one of this turn's facts:

```text
We won at corwick against orsk: our 52 against their 40. Our pikes broke their riders (+18%); the hills
favoured them (+15%); Maren the Wary held firm (+10%). We lost 9; they lost 21 and fell back to orskhaven.
```

The screen shows the same report as a card at the battle, with both sides' mix, each factor as a
bar, the roll last.

### 3.5 Generals

Every army has one. A new army gets a new general from the region's name table with one trait,
rolled; a general who wins three battles gains a second. A general dies or is captured when the
army is routed, and with a 5% chance in any lost battle; the army then gets a new one.

Ten traits (_Mine_, names and numbers ours, *to tune*):

| Trait | Effect |
|---|---|
| bold | +10% strength when attacking; +10% own casualties |
| wary | +10% strength when holding or defending |
| siegewright | sieges take one turn less |
| forager | stores last twice as long |
| horse-born | riders +15% |
| bowmaster | bows +15% |
| swift | the first step of every march takes one turn less (never below 1) |
| stubborn | never routed; retreats instead |
| inspiring | −20% own casualties |
| marshal | commands up to 140 soldiers |

The general's name and trait are in the marshal's facts, so a persona can play to them, and in the
report, so the chronicle can tell them.

### 3.6 Supply

The lead: "Ok, but we need to make sure we think how to do that." Two workable models:

**Model A, what the land carries.** Each province can feed a number of soldiers: 20 + 10 per farm
level, plains +20, forest +10, hills 0, marsh and mountains −10; twice that in our own or an ally's
land. Every army in a province shares it. Soldiers beyond it lose 5% of the excess a turn. This is
the grand-strategy convention of a supply limit (EU4's, [sources](#sources)).

**Model B, stores and lines.** Each army carries **stores**, 0 to 4 turns of food.
- **Supplied land:** our own provinces, an ally's, and a country's that gave us passage. There an
  army refills 2 stores a turn, up to 4, and loses nothing.
- **Elsewhere,** it eats 1 store a turn in rich land (plains, forest) and 2 in poor land (hills,
  marsh, mountains).
- **At 0 stores,** it loses 10% of its soldiers a turn (at least 2) and fights at −15%.
- A `forager` general halves the eating; a `healing_spring` refills everything; marsh adds 5%.
- A realm in famine ([4.2](#42-food)) refills nothing.

| | A: what the land carries | B: stores and lines |
|---|---|---|
| One line a model reads | "fenmark feeds 40; we have 52 there; 12 will starve" | "stores 2 of 4" |
| Far from home | does not matter by itself; only the land's richness does | is the whole point: every turn abroad costs |
| Poor land | yes | yes (eats twice as fast) |
| Long sieges abroad | cost only if the land is poor | bleed after 2 to 4 turns, so relief and timing matter |
| Stacks of armies | punished (they share the land) | not punished; the 100-soldier cap does that |
| Shown on the map | a feed number per province, which the map has no room for | pips under each piece |
| Planning | the ruler must know every province's feed number | count turns: "4 turns of stores, 2 a turn in the hills" |

**Pick: B**, with the army cap (_Mine_). It answers both halves of the lead's accepted default ("far
from home or in poor land") with one number per army that the model can read, the player can see,
and the battle report can mention. A's per-province number would cost a line per province in the
prompt and a label per province on the map. The sim A/Bs B against no supply at all before it
ships ([R3](#r3-armies-in-depth)).

### 3.7 Forts and sieges

- **Walls** are a building with three levels ([4.4](#44-buildings)). Today's forts become walls
  level 1 (a capital starts with walls 1, as today with one fort).
- **An undefended walled province cannot be walked into.** An army whose march enters a hostile
  walled province **besieges** it instead: it stands in the province, the province still belongs to
  its owner, and produces no income or food for it.
- **A siege takes** 2 turns per walls level (*to tune*): −1 with a `siegewright` general, −1 if the
  besiegers outnumber the garrison (10 a level) three to one, never below 1. Each turn of a siege
  also has a 10% seeded chance of an early breach.
- **When it ends** the walls drop one level and the province is occupied.
- **A siege is broken** when the besieging army leaves or loses a battle there. The owner's army
  that marches in fights it, with the walls' bonus if the garrison sallies.
- **A garrison** is only the walls' own 10 a level. An army inside walled land of its own just defends
  with the walls' bonus.
- **A fort holds the road** (priority 2): an enemy army that enters a province bordering an
  unbesieged hostile walled province must stop there, and can only besiege or go back. This is the
  genre's zone of control for forts (EU4, 2015 and 2016, [sources](#sources)), in its simplest
  form: it makes walls shape where wars are fought.

### 3.8 Fog of war

The lead (12 November): "I see the future of the game to be more like EU5 or EU4 where you have
multiple armies and you see like the total but there is a fog of war for actual units". Accepted:
"presence and rough size when it borders your land; nothing deeper in".

A country **sees** (_Mine_, the exact rule):

- its own provinces and every province bordering them;
- every province with one of its armies, and their borders;
- one ring more around a `watchtower` ([4.4](#44-buildings));
- what its allies see.

In what it sees:
- **a rival army** shows its position, its general, and its **size rounded to the nearest 10**
  ("about 40");
- an army in **forest** shows as "an army";
- the **mix of types** shows only when one of our armies with riders is in or beside that province
  (scouting).

Beyond that, nothing about armies. Always visible, as the lead wants: each rival's **total
soldiers, rounded to 10, and how many armies** it has, plus owners and capitals (the map's colours
are political knowledge). Walls and buildings beyond what a country sees show as last seen, with
the turn.

**The models read only what their country sees,** and so do the scripted players (_Mine_). This
keeps an A/B between them fair. Watch mode shows everything; rule mode shows the person's country's
view; the inspector, after the game, shows what each ruler saw.

### 3.9 Armies on the map

Accepted: "the map should show armies at all times"; "lets make sure they have animations too".

- **A piece:** a small standard in the owner's colour with the crest; a plate with the size (or
  "~40" in fog); the general's initial. Several armies in one province fan out around the
  settlement glyph.
- **A march:** a dashed ink line along the path to the destination, with the turn count. The piece
  slides from border to border in about 0.6 s a step at 1×, and stops on the border during a
  two-turn step.
- **A battle:** a clash glyph where it happens, a puff, the loser's piece sliding back; the report
  card on click.
- **A siege:** a ring of tents around the walled settlement, with one pip per turn left.
- **Stores:** four pips under the piece, red when 1 or 0.
- **Awaiting orders** (rule mode, own armies): a small open-seal mark; the key to cycle them is N.

### 3.10 What gives army play depth: a survey, and what to add

Read across grand-strategy and 4X games ([sources](#sources)); ideas and conventions only.

| Idea | Where the genre does it | Depth it adds | In this design |
|---|---|---|---|
| Terrain that changes movement and battle | everywhere (CK3's crossings, EU's terrain, Civ's tiles) | where to fight matters | **in** ([3.2](#32-terrain)) |
| A counter system between unit kinds | Civ's unit classes, EU's infantry, cavalry and artillery | what to recruit depends on the enemy | **in** ([3.3](#33-unit-types-and-the-counter-system)) |
| Commanders with traits | CK3's commanders, Old World's characters, Civ VII's commanders | armies have a face; a persona plays to them | **in** ([3.5](#35-generals)) |
| Supply and attrition | EU's supply limit, HOI4's supply hubs | far wars cost; timing matters | **in**, model B ([3.6](#36-supply)) |
| Forts and sieges over turns | EU's forts | walls shape fronts | **in** ([3.7](#37-forts-and-sieges)) |
| Retreat, rout and pursuit | CK3's aftermath, EU's morale | a won battle is worth chasing; riders have a job | **in** ([3.4](#34-battles)) |
| Fog of war with known totals | EU | scouting matters; surprise | **in** ([3.8](#38-fog-of-war)) |

**What I would add next, in priority order** (_Mine_):

1. **Forts hold the road** (zone of control for walls). Cheap, and it makes walls shape where wars
   go. The rule is in 3.7.
2. **Support from bordering armies** (+10% each, at most +20%), after Civilization's support. It
   rewards moving armies together, and the battle report can say it.
3. **Reinforcing in our own land.** An army in supplied land below its cap gains up to 5 soldiers a
   turn for 2 gold each, automatically. Fewer `recruit` orders for the model.
4. **Plunder**, a marshal order in an enemy province: gold for us, −1 population there, −15 opinion.
   It gives warmongers something to do and ties armies to the economy.
5. **Forced march**: one step a turn faster, for 1 store and a writ. Old World's idea of spending
   more to move further.
6. **Veterans**: an army that wins three battles fights at +10%. Simple, and visible as a star.
7. **Mercenaries**: a company hired for gold at once, anywhere we hold, upkeep doubled. A merchant
   persona's tool.

**What I would leave out** (_Mine_):

- **Fleets and landings** (the lead: fleets later).
- **Combat width and frontage**: opaque to a model and to a watcher.
- **A technology tree for units**: a second game.
- **Manpower pools**: gold and upkeep already bound armies, as stage 1 measured.
- **Tactical battle maps.**
- **Seasons and weather**: later, possibly as storyteller events.
- **Morale as a separate meter**: hunger and rout carry it.
- **Unit upgrades**.

### 3.11 Making the roster our own: research

The lead: "Lets have it like this for now but please do a reasearch on how we can make it more
interesting and unique". Pikes, riders and bows stay the base ([sources](#sources)).

**What makes a roster interesting**, as read across the genre:

- **Units that say who you are.**
  - Humankind gives every culture one emblematic unit beside the shared ones.
  - Civilization does the same per civilization.
  - A small difference with a strong identity is remembered more than a long list.
- **Units that answer the map.** When a unit needs a resource (Civilization's horses and iron for
  some units), land and trade gain a military reason.
- **Counters you can see coming.** Rock, paper, scissors is only a choice when the enemy's mix can
  be scouted, and switching takes time. Fog of war (3.8) and recruiting by companies do both.
- **Growth and risk.** Battle Brothers grows each soldier through the fights it survives, and lets
  injuries stay. Veterans make an army worth keeping alive.
- **A choice before the fight.** Stances and formations, or a commander's character, let the same
  army fight differently.
- **Few kinds, many modifiers.** Depth from combination reads better than many unit kinds,
  especially for a model reading a list.

**Three directions that would make ours its own** (_Mine_):

| Direction | What it is | What it asks of a model ruler | What it asks of the screen |
|---|---|---|---|
| **1. Levies of the land** | Each special place and some trade goods give a **local variant** of a base type, recruitable only there: `horse_plains` swift riders (the first step of a march one turn less); `iron_deep` iron pikes (+20% strength, +50% cost); `great_wood` wood bows (+25% in forest, size hidden as in forest); `old_castle` castle guards (+25% defending walls); salt marsh fen skirmishers (no marsh penalty) | One more recruit action for each variant, open only at the province that grants it (per-target availability), named in the moves line. No new decision kind. Facts gain the variant in the army's line | A small origin glyph on the company in the army panel; the province panel says what it levies; the variant's mark on the piece |
| **2. The realm's own unit** | From the country's character (2.8), one base type replaced by its own: highland pikes (no hills penalty), horse lords' mounted bows (count as riders and as bows in the counter rule), forest folk's hidden bows, the merchant league's hired companies (raised anywhere it holds, double upkeep) | The recruit action is renamed (`recruit_highland_pikes`). A `Knowledge` line saying what it is good at would change the prompt, so it needs an A/B | A unit glyph drawn in the crest's colours; a realm card at the start showing the unit |
| **3. Stances a general takes** | A marshal order with no target that lasts: `hold_line` (pikes +20% defending; marches cost a turn more), `skirmish` (bows +20%; the army falls back rather than be routed), `charge` (riders +25% attacking; +20% own casualties) | Three more marshal actions, a writ each. Personas would show in them (a warmonger charges). Risk: more army decisions, and the 4B repeating one stance | A stance glyph on the standard; the battle report names it |

Veterancy (priority 6 in 3.10) combines with any of them: a company that survives three battles
gets a name from the battle ("the Corwick pikes") and +10%.

**Pick: levies of the land first** (_Mine_).
- It ties armies to what the generator, the special places and the goods already create, so
  conquest and trade deals gain a military reason ("take the horse plains").
- It adds no decision kind and no prompt section, only actions offered where they are legal, which
  is per-target availability doing the framework's showcase work.
- **The order:** levies go in R6 with the goods; the realm's own unit follows the characters of 2.8
  in R9; stances only if R1's probe shows army orders are cheap.

## 4. Economy and buildings

Accepted: buildings (farm, market, barracks, walls, one special per terrain; three levels) "and as I
said I want it to be visible on the map"; food and one trade good per province; population per
province; trade as negotiated deals. From 11 November: buildings "one of the big parts of the game",
and the economy "will definetely need a reasearch but we should also make sure models will
understand how to use it". So every number below is one a ruler reads in a short line. All values
*to tune*, all _Mine_.

### 4.1 Population

Each province has a population of 1 to 12 (thousands, shown as a number).

- **At the start:** plains 4, forest 3, hills 3, marsh 2, mountains 2; +1 coastal; +2 capital.
- **Growth.** A province whose own food covers its people with one to spare gains a growth point a
  turn, and a person at 5 points, up to its cap. The cap is 4 + 2 per farm level, +2 on plains.
- **Loss.** −1 when conquered, −1 when plundered.
- **What it does:**
  - it is gold ([4.3](#43-gold));
  - it limits building ([4.4](#44-buildings)): a province's building levels may not exceed its
    population;
  - it draws the settlement ([4.6](#46-how-development-shows-on-the-map)).

### 4.2 Food

- **Produced:** each province makes its terrain's food ([3.2](#32-terrain)) + 2 per farm level +1
  if its good is grain.
- **Eaten:** half a unit per person, rounded up, plus 1 per 20 soldiers in supplied land.
- **The realm's balance** is one number: `Food: +4`.
- **Below zero is famine:** −1 stability a turn, no stores refill, the hungriest province loses a
  person.

### 4.3 Gold

- **Income per province** = population + 3 per market level, +5 at the capital, +2 for a luxury good
  (wine, cloth) with a market.
- **Upkeep** as today: 1 gold per 2 soldiers, riders double.
- **Taxes** as today. The treasury running dry still makes soldiers desert.

### 4.4 Buildings

A court order, `build_<kind>`, with a province of ours as the target. It builds the next level.

- **Cost:** 30, 60 and 90 gold for levels 1 to 3. **Time:** 1, 2 and 3 turns, during which the
  province shows scaffolding.
- **Building rule:** the sum of a province's building levels may not exceed its population.

| Building | Per level |
|---|---|
| farm | +2 food; +2 population cap |
| market | +3 gold |
| barracks | recruits here; +1 company per order; −10% recruit cost |
| walls | a siege takes 2 turns more; garrison +10; +15% defence |
| **special**, by terrain: | |
| plains: granary | +1 food; armies here refill all stores |
| forest: lumber camp | +2 gold; −10% building cost in the realm (once per realm) |
| hills: watchtower | sight one ring further ([3.8](#38-fog-of-war)); +5% defence |
| marsh: dikes | +2 food; no extra attrition in this province |
| mountains: mine | +4 gold; +4 more on iron or silver |
| coast with a port: harbour | +2 gold; sea trade: +1 gold for each other harbour within a trade deal |

The special takes the build order's name `build_special`, so the action list stays short. Which
building it is, the target's terrain decides.

**Offered in the moves line:** at most two provinces for each building kind, the two where it pays
most by the game's own count, so the line stays short (*"build walls at fenmark or ashby; build a farm
at brindle"*). [Section 8](#8-what-each-mechanic-asks-of-the-models-and-the-framework) records this as
a choice made for the model.

### 4.5 Trade goods and trade deals

- **Each province has one good**, rolled from its terrain:
  - grain (plains), horses (plains), timber (forest), cloth (forest, plains);
  - iron and stone (hills, mountains), salt (marsh, coast), wine (hills, plains).
- **A realm has a good** if it holds a province making it, or has a **trade deal** with a country
  that has it. Having one is all or nothing, never a quantity, so the model reads a list
  (`Goods: grain, timber, horses.`).

| Good | Effect on a realm that has it |
|---|---|
| grain | +10% food |
| horses | riders cost half |
| timber | buildings cost 20% less |
| iron | pikes cost 25% less; walls 20% less |
| stone | walls cost 30% less |
| salt | stores hold 5 instead of 4 |
| wine | stability recovers +1 faster after a tax rise |
| cloth | +10% gold |

**A trade deal** (court order `propose_trade`, a country): each side gets the other's goods for as
long as it lasts, and +5 gold a turn. It lasts until one breaks it, at least 10 turns. No terms to
choose, so no typed parameter is needed.

### 4.6 How development shows on the map

The lead (11 November): "how some provinces are like better developed, like in victoria 3 kind of";
and (12 November) "visible on the map". **Development** = population + building levels; it picks
the settlement's tier, and each building adds a mark (_Mine_, all drawn by code):

| What | Drawn as |
|---|---|
| development 1 to 4 / 5 to 9 / 10 to 15 / 16 and more | a hamlet dot; a village (three roofs); a town (roofs and a spire); a city (a dense cluster with two spires) |
| farm 1 to 3 | one to three bands of field strips around the settlement, in the land's light tone |
| market | a pennant per level on the settlement |
| barracks | crossed spears beside it |
| walls 1 to 3 | a ring around the settlement with two, four or six towers |
| special | its glyph: a granary dome, a sawn log, a tower, a dike line, a pick, a pier with an anchor |
| roads | a dotted ink line between the settlements of two bordering provinces of one realm whose development is 8 or more each |
| building under way | scaffolding lines on the settlement |

A **development** map mode (decision 19's family) washes provinces by development, light to dark.

## 5. Diplomacy and the realm

Accepted: peace, truce, alliance, tribute, trade deal, right of passage; opinions, visible, with
reasons ("Yes, and yes"); free-text negotiation, one ruler at a time first; breaking a deal costs
reputation, "maybe we can add more things as stability, legitimacy or more. If you think we need it".

### 5.1 Occupation, war score and treaties

The lead: "I would better want a peace treaty way. Like in EU4 where you conquer but you also need
to do a treatty for it to become yours". This replaces the first draft's taking a province by
entering it. The idea is the genre's; the rules, names and numbers here are ours (_Mine_, every
number *to tune*).

**Occupation.**
- **How it happens:**
  - an army at war with a province's owner ends its step there, with no defending army, and the
    province is not walled;
  - or its siege ends.

  The province is then **occupied**: its owner stays, and its **controller** is the occupier.
- **Neutral land has nobody to sign a treaty,** so beating its garrison takes it outright, as today.
  The opening land grab still colours the map.
- **An occupied province:**
  - gives its owner no income and no food;
  - gives the occupier half its income;
  - counts as supplied land for the occupier's armies.
- **Freeing it:** an army of the owner, or of an ally at war with the occupier, enters it with no
  occupying army there. A walled province must be besieged again, at its walls' current level.
- **Each war is between two countries.** An ally who joins is at war on its own account, and its
  occupations count in its own war.

**War score**, for each pair at war, from −100 to +100, from one side's view:

| Part | Points |
|---|---|
| A province's value | 1 + its population ÷ 3 (rounded down), +2 for a capital |
| Occupation | 100 × (value of their provinces we occupy − value of ours they occupy) ÷ the value of their whole realm |
| Battles | +4 for each battle won against them in this war, −4 for each lost, at most ±20 |
| Their capital | +10 while we occupy it |

**The terms**, each a court order with one target and a cost in war score. A term is offered only
when the proposer's score pays for it (availability), so the score bounds what may be asked, and
the model never sees a term it cannot ask:

| Action | Target | The treaty | Cost |
|---|---|---|---|
| `offer_white_peace` | the country | all occupied land returns | 0, either side |
| `demand_occupied` | the country | every province of theirs we occupy becomes ours; ours they occupy return | the occupation points of what is ceded |
| `demand_province` | a province of theirs that borders land we own or occupy | that province becomes ours; everything else returns | its value share × 100; × 1.5 if we do not occupy it |
| `demand_tribute` | the country | they pay 20% of their income to us for 10 turns; everything returns | 25 |
| `cede_occupied` | the country | (the losing side's offer) every province of ours they occupy becomes theirs | none |
| `offer_province` | a province of ours | that province becomes theirs; everything else returns | none |
| `offer_tribute` | the country | we pay 20% of our income for 10 turns; everything returns | none |

Every treaty ends that war and starts a 5-turn truce.

**Rounds.**
- **Proposing:** a side may propose one term a war a turn, for a writ.
- **Answering:** the other side answers, `accept` or `refuse`, as a separate free decision on the
  same snapshot. Its facts are the term, the war score, and both sides' armies as it sees them.
- **Both propose in one turn:** both proposals are answered. If both are accepted, the one from the
  side with the higher score lands.
- **A refused term** may be put again the next turn.
- **How long it takes:** a war ends after one to a few turns of bargaining, which is what several
  rounds look like here.
- **A ruler answers at most two proposals a turn;** more wait.

**Enforced terms.** At a war score of 90 or more, a demand cannot be refused. It lands without the
other side being asked; the log records it as enforced.

**When a war ends without a treaty**, and when a ruler is wiped out:

- **A war ends only by a treaty,** white peace included, **or when one side is broken.** Occupation
  never decays into ownership.
- **A realm is broken** when every province it owns is occupied and it has no army left. Every
  occupier then takes what it occupies, at once, and the country is out.
- **An occupier knocked out of the game:** its occupations return to their owners.
- **Peace with one of two enemies** settles only that pair's occupations.
- **A stuck war is offered the status quo.** If a war's score has moved less than 10 points and no
  occupation has changed for 10 turns, both sides are offered `status_quo`, free: each keeps what it
  occupies. It lands if both accept. This replaces the white-peace offer of the first draft, so a
  long war still moves the map.

**Under conquest with no turn limit,** stalemates end through:
- enforced terms at 90;
- broken realms;
- the status-quo offer;
- war weariness;
- fear of the leader;
- the storyteller's lean ([6.1](#61-conquest-the-default)).

**What the rulers read.** One more part on each line of a country at war with us, plus a terms line:

```text
orsk: at war with us, war score +34 (we occupy ashby and corwick; won 2 battles); about 90 soldiers in 2 armies; hostile (-45).
orsk occupies brindle.
Terms we may put to orsk: demand all we occupy (ashby, corwick); demand ashby; demand tribute; offer white peace.
```

That is about 80 more tokens a court prompt in wartime.

**What the eye sees** (_Mine_). Colour changing is the game's centrepiece, so occupation and
ownership must never look alike:

- **Owned land:** the owner's wash, as now.
- **Occupied land:** the owner's wash stays, and the occupier's colour is laid over it as **inked
  diagonal hatching**:
  - the lines are about 6 px apart at 1×, in the occupier's colour with a dark ink edge;
  - a small pennant of the occupier is planted on the settlement;
  - when a province is occupied, the hatching is drawn stroke by stroke from the side the army came
    from, in about 0.8 s;
  - it is a pattern, not only a colour, so it reads for colour-blind players too.
- **Under siege:** the ring of tents (3.9), and no hatching until it falls.
- **A treaty transferring land:**
  1. the treaty card: both crests, the term, the seal (court scenes later);
  2. for each ceded province, the hatching thickens and flows together into the new owner's solid
     wash, spreading from the new owner's border, with today's capture spread and wet edge;
  3. a wax-seal glyph stamps on the province for a second;
  4. the borders redraw.

  This is the colouring moment, now earned by a treaty.
- **Land returned:** the hatching lifts, fading from the edges inward, in 0.6 s.
- **A war map mode:** each war's score as a bar between the two crests; occupied provinces outlined
  in the occupier's colour.
- **The legend** in rule mode: solid is held, hatched is occupied.

**With typed parameters** (#266 item 2) one decision could carry a whole treaty: a list of
provinces, a tribute with its size and length, in any mix. Today a treaty is **one term**, so a
war ends with one term, and a mix (a province and tribute) is not possible. Nothing here depends on
that feature.

### 5.2 Agreements

Each is proposed by a court order and answered by the other ruler as a separate decision, accept or
refuse, as peace is today (_Mine_, the exact rules; numbers *to tune*):

| Agreement | Proposed by | Terms, fixed by the rules | Ends |
|---|---|---|---|
| **peace treaty** | one of the terms in [5.1](#51-occupation-war-score-and-treaties) (at war) | the term; then a truce for 5 turns | — |
| **truce** | follows a treaty | no war between the two | after 5 turns, or broken |
| **tribute** | a treaty term, `demand_tribute` or `offer_tribute` (5.1) | the payer gives 20% of its income each turn to the other for 10 turns | after 10 turns, or broken |
| **alliance** | `propose_alliance` (at peace) | defensive: when someone declares war on one, the other is asked to join ([5.3](#54-calls-to-arms)) | broken |
| **trade deal** | `propose_trade` (at peace) | goods shared both ways, +5 gold each a turn ([4.5](#45-trade-goods-and-trade-deals)) | broken, at least 10 turns in |
| **right of passage** | `ask_passage` (at peace) | our armies may enter and are supplied in their land | after 10 turns, or broken |

- **Free-text negotiation** (accepted, decision 10) uses the conversation panel the screen page
  planned. Whatever the talk agrees on lands as one of the proposals above, checked by the rules.

### 5.3 Opinions with visible reasons

Every ruler holds an opinion of every other from −100 to +100. It is the sum of reasons, each with a
value and a decay:

| Reason | Value | Decay |
|---|---|---|
| at war with us | −25 | while it lasts |
| occupies a province of ours | −5 each, at most −20 | while it lasts |
| took a province of ours by treaty | −10 each, at most −40 | 1 a turn |
| broke a deal with us | −30 | 2 a turn |
| broke a deal with anyone (its word) | −10 | 1 a turn |
| borders us | −5 | while it lasts |
| holds a third or more of the land | −1 for every 2% it holds above a third | while it lasts |
| alliance with us | +25 | while it lasts |
| trade deal with us | +10 | while it lasts |
| at war with our enemy | +10 | while it lasts |
| paid us tribute / accepted our peace | +10 | 1 a turn |

The rulers read the two largest reasons for each rival, in the game's words: `orsk: ... hostile
(-45): took ashby from us, at war.` The labels by value are friendly (30 and up), warm (10 to 29),
neutral, wary (−10 to −29), hostile (−30 and below). The screen's country panel lists every reason.

**What it is for:**
- **For the scripted players,** whose answers and choices of target use opinion thresholds.
- **For the measurement.** Today's statements invent grievances about land never taken. With true
  reasons among its facts, does the model invent fewer? That is [R7](#r7-diplomacy-and-the-realm)'s
  A/B, and it is measurable with the scorecard's invented-grievance count (#244 item 4).

### 5.4 Calls to arms

When a country declares war on one of our allies, we are asked, as a separate free decision,
`join_war` or `stay_out`.
- **Joining** puts us at war with the attacker.
- **Staying out** ends the alliance, with −30 opinion and the "broke a deal" reason.

### 5.5 Breaking a deal

A court order, `break_<agreement>`, with the other country as the target. Declaring war during a
truce is `break_truce`.

- **The other side's opinion** takes the reason "broke a deal with us".
- **Everyone's opinion** takes "broke a deal with anyone".
- **Stability** −1.

### 5.6 Does a realm value earn its place?

**Stability stays; nothing is added** (_Mine_).
- **Stability** already does real work: taxes cost it, it drives revolts, and the 4B reads it.
- **War weariness** folds into it: −1 every 6 turns a country is at war with anyone (*to tune*).
- **Reputation** is the "broke a deal with anyone" reason, so it lives in opinions, not in a meter.
- **Legitimacy** would be a second meter doing stability's job. Each meter is a line in every
  prompt and a number in the top bar, and the 4B already reads ten lines.

If the sim shows realms never wobbling in long games, the first thing to add is **overextension**:
stability falls when a realm holds more provinces than 8 per writ, which feeds revolts back into a
long game ([section 6](#6-winning-and-length)).

## 6. Winning and length

The lead: "Lets actually make the default mode to be like conquer evereything and allow infinite
turns."

### 6.1 Conquest, the default

- **The rule.** The game ends when one country is the last with any province. The lead accepted
  "last ruler standing" ("Ok").
- **There is no turn limit.** A person can stop at any time.
- **Headless runs** for measurement stop at a cap, 150 turns by default, and record the game as
  unfinished.

**Pacing.** On the 25-province board a province changes hands 1.5 times a turn with scripts and 1.1
with persona 4B rulers. A medium map needs the winner to take about 50 provinces. Rough guesses
(_Mine_), for the sim to measure:
- **About 60 to 100 turns** for a medium game.
- At about 7 s of model time a turn, mostly hidden behind playback, **about 12 to 20 minutes of
  model time**, and 45 to 90 minutes to watch at 1×.
- That is longer than the 30 minutes of 11 November's decision 1, which said it "should depend on
  the settings and the map sizes". The small map is the 30-minute game.

**Stalemates.** A long game needs the leader to be checked and stuck wars to end. The rules above
already carry four pressures:
1. **Fear of the leader:** the opinion reason for a country holding over a third pushes everyone
   else toward alliances against it.
2. **War weariness** costs stability.
3. **Revolts at low stability** free provinces.
4. **The storyteller** leans on the leader: S1 measured value against standing at r −0.37.

Treaties add three more ([5.1](#51-occupation-war-score-and-treaties)): **enforced terms** at a
war score of 90, **broken realms** taken whole by their occupiers, and the **status-quo offer** in a
stuck war. And one more, _Mine_:

- **Stalemate is measured, not hidden.** A game with no province changing owner or occupier for 20
  turns is counted as a stalemate in the scorecard.

### 6.2 Other modes, as settings

| Mode | Wins | Notes |
|---|---|---|
| **Conquest** (default) | the last country standing | no turn limit |
| **Domination** | holding a share of all provinces, 60% by default | today's rule (15 of 25) generalised |
| **Turn limit** | the most provinces after N turns, development breaking a tie | today's turn-30 rule |
| **Crownhold** | holding the `crown_seat` for 10 turns in a row | the lead's "king of the hel" idea (11 November, decision 5) |

### 6.3 What it means for the scorecard

Per-game numbers mean little when games differ by 60 turns. _Mine:_
- **Rates per turn** stay as today.
- **Turn windows** are added: turns 1 to 20, 21 to 50, 51 and on.
- **New measures:** the turn of the first elimination, turns to the end, unfinished games and
  stalemates.

For A/B runs that must finish in hours, **the A/B arms run with a turn cap** (40 turns for an
armies slice) on the same seeds, as stage 2 ran with 30.

## 7. Settings and presets

Accepted (32): model, persona mix, map size and storyteller chosen at the start. The lead (4): "We
should make sure we have a settings and support multiple resoltuions and UI scales and etc. Would be
nice to start with menu and the settings already". The menu becomes **New game**, then a settings
screen with these tabs (_Mine_), each with a preset row on top and a "custom" state once anything
is changed:

| Tab | Settings | Default |
|---|---|---|
| **World** | seed; size; countries; land shape; neutral land; special places (none, few, many); the model draws special places (on/off); or the **Classic** board | medium, 6, continent, 55%, few, on |
| **Rulers** | model preset, for all or per seat; persona mix: *distinct* (a different persona each, as the table measured), *chosen per seat*, *random with repeats*, *all one persona*; who rules each seat: model, script (expansionist, cautious), random, or **you** | `qwen3.5:4b`, the `default` arm; distinct |
| **Storyteller** | preset; then events per turn, lean toward the weak (0 to 1), events with choices (on/off), chains (off, rare, often), story-only events (on/off) | *Chronicler* |
| **Victory** | mode and its value; the turn cap for a headless run | conquest |
| **Advisor** | on/off ("enabled by default which you can disable in game or turn on in game"); its model preset | on, the rulers' model |
| **Difficulty** | preset: Easy, Normal, Hard, Custom; each sets the person's starting realm size and gold, whether the person is held to the writ cap, and the rivals' persona mix | Normal |
| **Display** | resolution, window mode, UI scale (auto: 1× at 1080, 1.33× at 1440, 2× at 2160, as today, or set), style Gilt or Engraved, animation speed, fog in watch mode (off) | auto |

**Storyteller presets** (accepted: "Lets have it as a settings with different presets you can tweak
before the game"; _Mine_, the values):

| Preset | Events a turn | Lean toward the weak | Choices | Chains |
|---|---|---|---|---|
| Off | 0, and the random event table | — | — | — |
| **Chronicler** (default) | 1 for the world | mild (0.3: S1's budget as built) | on | rare |
| Kingmaker | 1 | strong (0.8: gifts only to the bottom half, blows only to the leader) | on | rare |
| Saga | 1, plus a chain always running | mild | on | often |
| Chaos | 2 | none (0) | on | often |

"Lean toward the weak" scales S1's fairness budget: at 0 the budget is symmetric; at 1 a country in
the top half gets no gifts and one in the bottom half no blows.

**Model presets** (11 November, decision 17: "it can be like model presets because for some models
you may want them to work differently"). A preset is a name with a model, a `ModelSeatOptions` arm,
and a note shown in the menu. It is a file, so a new one needs no code. Shipped:
- `qwen3.5:4b` (default arm);
- `gemma4:e4b-it-qat` (the same arm; measured close on 12 November);
- `qwen3.5:2b` with `+own-words`, marked "weaker, faster".

**Saved with the game.** Every setting goes into the recording, so a replay rebuilds the same world
and the same game, as `ReplayBuilder` does today.

## 8. What each mechanic asks of the models and the framework

For every new kind of decision, what it offers, what it reads, its likely prompt, and the gaps it
hits (#216, #244, #266). Prompt sizes are estimates (_Mine_) from today's 970 tokens with 8 standing
lines and the moves line; every slice measures them.

| Decision | Asked | Actions (targets) | Reads | Prompt | Gaps |
|---|---|---|---|---|---|
| **Court order** | once or twice a ruler a turn | `recruit_pikes`, `recruit_riders`, `recruit_bows` (province); `build_farm`, `build_market`, `build_barracks`, `build_walls`, `build_special` (province); `raise_taxes`; `declare_war`, `break_truce`, `offer_peace`, `demand_tribute`, `offer_tribute`, `propose_alliance`, `propose_trade`, `ask_passage`, `break_alliance`, `break_trade` (country); `none` | 9 to 11 standing lines ([1.5](#15-what-a-ruler-reads-literally)), up to 3 turn facts, orders given, the moves line | ~1,400 with 4 remembered exchanges | **Typed parameters:** unit type and building kind spelled into action ids; peace terms as separate actions. Count by action is ~20, of which about 8 are open in a turn. The moves line grows: kept to two provinces a building kind and to provinces with barracks for recruits |
| **Army order** | per army awaiting orders, up to writs − 1 | `march` (province: its borders, plus at most 6 farther ones the game picks: enemy and neutral border provinces in reach, our own threatened ones, the nearest capital at war), `merge` (army: ours, same or bordering province), `split` (none: halves the army, a new general), `hold`; later `plunder`, `forced_march` | the army's line, what it sees nearby, orders given, the moves line | ~1,100 | **The army is not a target**; it is in the stimulus and `DecideOptions.Correlation`, which per-target availability reads (as `EnvoyOffer` today). A destination many steps away is one target and the path is the rules'. **N10**, a round holds an agent once: one round per writ slot or a game-side chain |
| **Treaty proposal** | a court order | the seven terms of [5.1](#51-occupation-war-score-and-treaties) (country or province), offered only when the war score pays | the war's line and the terms line | ~1,500 in wartime | **Typed parameters:** one term a treaty; a list of provinces or a tribute's size needs #266 item 2 |
| **Answers**: treaty, alliance, trade, passage, call to arms | per proposal received, at most two a turn | `accept` / `refuse`; `join_war` / `stay_out` | the offer first, the war score, then two facts | ~900 | One round per offer, as today (N10) |
| **Automation** (R8) | a marshal order | `guard` (province), `campaign` (country), `watch_border`, `follow` (army) | as an army order | ~1,100 | none new; the code runs the army afterwards |
| **World design** (R9) | at creation | land shape (climate); `choose_world` (candidate); features (region); character (start site) | the candidates' lines, sites, regions | ~800 | none new; the paired schema and per-target availability are the fit rules |
| **Names and histories** (R10) | at creation | naming style (region); character (country); `write_histories` (region) | the region's numbered list of provinces | ~800, with statements of 60 to 160 tokens | **#266 item 3:** one text field carries a list; **#244 item 11:** a reply cap matters with long statements |
| **Choice event** (R5) | per affected ruler; several for an event that "affects all" | `choose_first`, `choose_second`, `choose_third` (none) | the event's title and story, each option's line in the game's words | ~1,100 | **#266 item 2**: the storyteller cannot write options with their own effects in one decision. Workaround: *dilemma templates* whose options are the game's (below) |
| **Storyteller: event** | once a turn | today's 8 templates, plus R5's dilemma templates and chain steps (country, group, province) | today's facts, plus a digest of what happened (battles, conquests, broken deals) and running chains | ~1,500 | #266 items 3 (title), 4 (replace the story only), 5 (a lasting fact, for chains), 6 (what was told), 9 (variety); N11, N12 |
| **World-maker: special place** | at creation, per slot | the templates the slot allows (province) | the slot's terrain, region, neighbours; places made so far | ~700 | #266 item 3 (name and story split at a colon) |
| **Advisor** (rule mode) | when the person asks, or once a turn | today's advise mode: the ruler's own decision for the person's country, refused in character | the person's country's facts | ~1,400 | none new |

**What the framework would need, in one list:**

1. **Typed parameters** (#266 item 2, #190 LV #12). *Unit type and building kind as action ids* is a
   workaround that works: about 8 actions open in a turn. **A treaty is one term**: a province or
   tribute, never a mix or a size, until a decision can carry a list. **Trade deals with chosen
   goods** are left out because of it. **Choice events written by the model** need it.
2. **Several decisions per agent per turn** (N10). `DecisionRound` holds an agent once. Today that is
   a second envoy round; in v2 it is every turn, for every ruler. A *chain* helper is worth
   considering: one agent, several decisions in order, each with a line for what was ordered before,
   and nothing committed until the caller executes.
3. **Memory per thread.** Three army orders a turn push the court's last turns out of a six-exchange
   history. The design avoids it with two agents per country. A game wanting one agent would need
   memory keyed by thread (one history per army). Recorded, not asked for.
4. **A fact ledger** (#216 item 4, #266 item 5) for opinions' reasons, chains, "last seen" walls, and
   lasting events. The game can compute each without one.
5. **A reply cap** (#244 item 11, N12): needed now. With about 15 decisions a turn, a 20 s runaway in
   460 calls stalls a game about once every 30 turns.
6. **The moves-line workaround** (N1, N8, N11) for every decision kind. The game writes each line;
   `LegalTargets = ObservationLine` measured worse.
7. **Per-call target sets.** The prompt's target list must hold only the targets this decision can use
   (about 25), not all 60 to 150 provinces. A per-call `ITargetSource` does that today. `MaxListedTargets`'
   silent cut (N2) bites if it is forgotten.
8. **Prefix reuse.** Not a framework gap, but a fact to measure: whether back-to-back requests that
   share their beginning are cheaper on this server.

## 9. How it will be measured

Hard rule 2 holds: no accuracy or play claim without an A/B on the same model, machine, prompt set
and session. Every slice measures in the **simulation first** (scripted players, seconds, no model),
then with the model.

### 9.1 What carries over from the scorecard

All five sections carry over:
- **liveliness:** wars, peace, battles, provinces changing hands, quiet turns, lead changes,
  eliminations, Gini;
- **sense:** idle share, illegal answers, containments, refusals at resolution, attack win rate,
  aiming at the weaker, answering an attack, accepting peace;
- **character:** η² of aggression, with aggression redefined as the share of a ruler's orders that are
  marches into hostile land, declarations or breaches;
- **voice:** the court's statements, with all of today's checks;
- **strength.**

Per game becomes per turn, plus the turn windows of [6.3](#63-what-it-means-for-the-scorecard).

### 9.2 What is added

| Section | Measure |
|---|---|
| **Pace** | model time a turn (median, 90th percentile); decisions a turn by kind; tokens a decision by kind; queue time; the share of model time hidden behind playback |
| **Orders** | writs used out of writs held; orders by kind; armies awaiting orders that got none; **wasted orders**: refused when they landed, a march that never arrives, a build that cannot pay, `none` with an army awaiting and a writ spare |
| **Armies** | battles a turn; battle win rate against the odds before the battle (does it pick battles it should win?); losses to attrition as a share of all losses; sieges begun and finished; routs; army count and size over time |
| **War and treaties** | occupations a turn and provinces transferred by treaty a turn (both replace "provinces changing hands"); treaties by term; proposals refused; enforced treaties; status-quo offers made and accepted; broken realms; turns a war lasts; whether a ruler asks for what its score allows |
| **Automation** (R8) | armies on automation; decisions saved; what automated armies achieved against ordered ones |
| **World and names** (R9, R10) | options offered and chosen; features refused by the checks; names and histories falling back, by check; time at the start |
| **Counters** | whether recruits answer the mix of enemy armies the country has seen (the share of recruits that counter the largest type seen) |
| **Economy** (R6) | buildings by kind against the scripted "where it pays" choice; buildings that cannot be used (a barracks never recruited from); famine turns; development Gini |
| **Diplomacy** (R7) | deals made, kept and broken; calls answered; opinion against action (do rulers attack those they dislike?); **invented grievances** with and without opinion reasons |
| **Map** (R2) | the fairness scores of every generated map; wins by starting position in the 200-game balance run; generation time |
| **Length** | turns to the end; unfinished games; stalemates |

### 9.3 The A/B each slice owes

| Slice | Control | Arm | First in the sim | Then with the model (`qwen3.5:4b`, RTX 5080, behind the lock) |
|---|---|---|---|---|
| R1 | the one-move game on Classic | writs and armies on Classic | scripts beat random; length, battles, armies awaiting orders a turn | **a latency probe** (court and army orders, with and without a statement, back to back and interleaved), then the persona table, 20 games, 40-turn cap; sense, character, pace |
| R2 | Classic | generated medium maps, 20 seeds | the balance run per seed; fairness against wins | none needed for the map itself; world-maker calls checked like the storyteller's |
| R3 | R1's rules | each of types, generals, sieges, supply, fog on, one at a time, then all | each feature on against off, 200 games; supply A against B against none | all on against R1, persona table, 20 games; plus fog on against off for the models (does fog change sense?) |
| R4 | Classic with R3's rules | medium generated maps | length and stalemates | persona table on a fixed set of 10 seeds, 2 games each, 60-turn cap; pace at 6 rulers |
| R5 | the storyteller as built (S1) | choices; chains as skeletons; chains written by the model (the lead: "if you insist we can do both and compare and test") | — | 20 games an arm; the events section plus the blind pick of two logs by the team |
| R6 | R4 | economy and buildings | builds against the scripted choice | persona table; economy measures; does the builder persona build more? (η² of building) |
| R7 | R6 | diplomacy; and opinion reasons in facts on against off | deals and their effect on length | persona table; invented grievances with reasons against without |
| R8 | the writ cap | no cap with automation and the budget of 8; and no cap without automation | length, battles, armies idle | persona table, 20 games, 40-turn cap and a 100-turn slice of 5 games: model time a turn (median, 90th percentile) by turn window, decisions a turn, automation share, sense and character |
| R9 | the seeded generator alone | the world chosen and shaped by the model | the balance run on both arms' worlds | the same 20 seeds: fairness scores, features refused, game length and stalemates; a blind pick of two worlds by the team |
| R10 | table names | model names and histories, generated once per seed and cached | — | the same 20 seeds and geometry in both arms, only the names differing: illegal answers, grounding containments, statements naming their target, statements naming places that do not exist, attack share and sense; fallbacks by check; time at the start; a blind read of two maps by the team |

**Cost.** A 40-turn game of six rulers at about 7 s of model time a turn is about 5 minutes; a
20-game arm about 1.6 hours. Hence turn caps, two arms per session at most, and the sim doing
everything a script can.

## 10. The build as slices

The lead's order: armies with the new mechanics, then the bigger map, then events with choices and
chains, then buildings and economy, then diplomacy ("Ok"). Three slices he wants "eventually" come
after: no cap on orders (R8), the world shaped by the model (R9), and names and histories by the
model (R10). R9 and R10 touch only creation and may run any time after R4. Adjusted where something
must come first (_Mine_):

- **R0, settings and the map as data, comes first:** every later slice reads a map with terrain and
  a settings object. The lead asked to "start with menu and the settings already".
- **The turn of writs comes with the first armies (R1):** armies without it would be ordered one move a
  turn.
- **Occupation and the two simplest treaties come with R1 too** (white peace, and ceding what is
  occupied): with armies occupying land, nothing else would ever transfer it. The rest of the terms,
  tribute and single provinces, come with diplomacy in R7.
- **The generator's headless part (R2) moves up, beside R1,** because the lead asked to "start with
  the map generator already". It touches different files from R1, so the two can run at once. The
  drawn chart starts there as a spike the lead judges, while the full renderer waits for R4, "the
  bigger map" in his order.

| Slice | What it is | Size (_Mine_) | Playable after it |
|---|---|---|---|
| **R0** | settings, victory modes, the map as data with terrain, the Classic preset | M | today's game exactly, with a settings screen and conquest as default |
| **R1** | writs, the order cycle, armies as pieces, movement, battles, occupation, war score, white peace and ceding what is occupied, the court and marshal agents | L | the Classic board with armies, wars ending in treaties |
| **R2** | the generator headless, its checks, special-place slots, the world-maker; a drawn-chart spike | L (parallel with R1) | as after R1; generated worlds playable headless |
| **R3** | unit types, generals, sieges, supply, fog, the battle report | L | the Classic board, armies in depth |
| **R4** | generated worlds on screen: the drawn chart, labels, map settings, specials shown | L | any generated world, on screen |
| **R5** | events with choices and chains, province traits, multi-affected events | M to L | as R4, with a richer storyteller |
| **R6** | population, food, goods, buildings, development drawn, levies of the land | L | as R5, with an economy |
| **R7** | all treaty terms, enforced terms, status quo, broken realms; agreements, opinions, calls to arms, breaking deals | M to L | the full v2 game |
| **A** | the advisor, on by default and switchable in game | S to M (after R3) | rule mode with advice |
| **R8** | orders without a cap: automation, groups, the decision budget | M | the v2 game with no cap on orders |
| **R9** | the world shaped by the model: candidates, features, characters and start sites, the realm's own unit | M to L | worlds the model designed, with "How this world was made" |
| **R10** | names and histories by the model, with their checks, cache and loading screen | M | new names every game |

Sizes: **S** is one session's pull request; **M** about two; **L** three or more, and it may split
into several pull requests. Each slice ends with the scripted players updated (they are the
yardstick and the reference tables), its EditMode tests, the A/B it owes, a findings entry, its
changelog line and the demo `README.md` updated.

### R0. Settings and the map as data

- **Sim:**
  - `WorldMap` as data: provinces with terrain, coast, port, population and good slots (unused yet),
    adjacency, capitals, polygons when generated; loaded from JSON.
  - `MapDefinition` built from it for any number of provinces and countries.
  - The Classic preset with terrain set by hand to match the painting.
  - `GameSettings` (map, seats, victory mode and value, turn cap, storyteller preset, advisor),
    serialised.
  - The victory modes conquest, domination and turn limit. Terrain has no effect yet.
- **Agents:** seats, personas and model presets built from `GameSettings`; model presets as files.
- **Screen:**
  - The menu's **New game** and the settings screen with its tabs; the display settings
    (resolution, window, UI scale, style).
  - The state texture sized per province, not fixed at 32.
  - Settings stored in the recording.
- **Tests:**
  - The Classic preset in domination mode plays the 200-game mixed run **byte-identical** to
    today's `summary.md`.
  - Settings round-trip.
  - Conquest ends with one country left.
  - A recording made before R0 still replays.
- **Playable:** today's game, plus settings, conquest by default.

**Built, 12 November 2026** (the game's half; the screen's half is the screen session's). What
exists, and where it differs from the list above:
- **`WorldMap`** (`Sim/Map/WorldMap.cs`), the map as data, read and written as JSON by Newtonsoft
  (now referenced by `GreyBoxStrategy.Sim`, which keeps `noEngineReferences`). Provinces carry
  terrain, coast, port, population, a good, a region and a special place; no rule reads them yet.
  `MapDefinition.Standard()` is now `ClassicMap.Load().ToDefinition()`.
- **The Classic preset** (`ClassicMap`) is a JSON string in C#, because the simulation cannot load a
  Unity asset; it goes through the same reader as any map file. Terrain set by eye from the painting:
  10 plains, 7 forest, 4 hills, 3 mountains, 1 marsh. The board built in code is kept in the tests
  (`LegacyBoard`), and a test compares the two in every field.
- **`GameSetup`** (`Sim/Rules/GameSetup.cs`), not `GameSettings`: the screen already has a
  `GameSettings` for the display, and two types of one name in two namespaces the screen imports would
  not compile there. Seed, map, countries, victory mode with its share, turn limit and headless cap,
  storyteller preset and advisor (both data only). `FromJson` refuses unknown names, bad values and
  invalid settings with a message; `BuildMap`, `BuildRules` and `NewGame` start a game.
- **Victory modes:** `Domination` (today's rule, the default), `Conquest` (last country standing; a
  headless run stops at its cap with the new ending `Unfinished`) and `TurnLimit`. _Mine:_ **the
  default stays domination** until R1. On today's one-move rules conquest is the same game without
  its end, and the brief for this slice asked that today's game play exactly as it did.
- **Byte-identical, pinned:** the 200-game mixed series' `summary.md` and every turn's owner grid hash
  to the same values as on the commit before R0 (`SetupTests`). The first standing fact, the agent's
  goal line and its "one of five countries" read exactly as before on Classic; other victory modes and
  country counts get their own words.
- **Not in this slice:** model presets as files and seats or personas from settings (they touch
  `ModelSeatOptions`, which another session was changing); the menu, the settings screen, the state
  texture and settings stored in recordings are the screen's (`Screen/`). Recordings made before R0
  still replay: the recorded game's test passes unchanged.
- **What the page left inconsistent:** section 2.2 sets neutral land at 55% *and* starting realms of 3
  provinces; on a medium map of 60 provinces with 6 countries those give 70% neutral, not 55%. R2
  takes the realm size as the setting and reports the neutral share.

### R1. The turn of writs and armies as pieces

- **Sim:**
  - `Army` (one soldier type until R3; R3 part one adds three behind `GameRules.UnitTypes`, off), its orders and wake conditions.
  - Writs and the order cycle ([1.3](#13-the-order-of-decisions-within-a-turn)).
  - Simultaneous movement with terrain costs; border crossings.
  - Battles between armies with today's formula; retreat and rout; militia and neutral garrisons.
  - Occupation, and neutral land taken by entering; recruit at the capital.
  - War score; the terms `offer_white_peace`, `demand_occupied` and `cede_occupied`, and their
    answers; broken realms.
  - The resolution phases ([1.1](#11-what-a-country-does-in-a-turn)).
  - Scripted players with armies, and random.
  - New log lines.
- **Agents:**
  - The court and marshal agents per country with their actions, availability by `WhyNot`, the army
    in `Correlation`, and two moves lines.
  - Decisions chained per ruler; memory 4 each; the army statement rule.
- **Screen:**
  - Pieces always shown, march lines, the step animation, battle clash and a plain report card.
  - Occupation hatching and its drawing-in; the treaty's flow from hatch to wash, the seal, and
    hatching lifting from returned land.
  - Rule mode: writs in the top bar, awaiting marks, the army cycle key, orders queued above End turn.
  - `ReplayBuilder` rebuilds armies.
- **Tests:**
  - Each terrain's cost; two armies stepping at once; a border crossing.
  - Occupation changes the controller and never the owner; a treaty transfers exactly its term;
    white peace returns everything; a broken realm's land goes to its occupiers.
  - Battles are deterministic per seed; the writ cap holds.
  - Every wake condition fires once.
  - Every order is re-checked when it lands.
  - Whole games terminate; scripted beats random.
  - A fake model plays exactly the scripted game through the framework, as stage 2 checked.
- **Measured:** [9.3](#93-the-ab-each-slice-owes), the latency probe first.
- **Playable:** watch, live and rule on Classic, with armies; wars end in treaties.

**Split in three, 12 November** (proposed to the tech lead with R2's follow-up):
- **R1a, the rules, headless: built.**
- **R1b, built** (both parts, 12 November; see below): the court and marshal agents, and the prompt fixes R2 found: the standing facts, the length
  of the moves line, and the game's length.
- **R1c, measured** (12 and 13 November): the live measurement on `qwen3.5:4b`. 20 writs games on Classic
  against 20 one-move games: 7.1 s of model time a turn (estimate 7 to 9 s), no game finished in 40 turns,
  armies used, treaties made, the persona still showing, statements repeated word for word 35% against 5%.
  Five uncapped games end by stalemate after about 190 turns; five on a medium world did not finish in 40.
  The run found army orders for targets the list left out, now contained (`WritRuler.NotOffered`). Numbers:
  [`findings.md`](../llm-wiki/findings.md), 13 November.
- **The statement repeats, measured** (13 and 14 November), 15 games an arm: showing a ruler its own last four
  lines (`+own-lines`) cut repeats from 36% to 22% with legality, model time and persona unchanged, and is the
  demo's default now. One voice a turn (`+one-voice`) made them worse (45%) and halved the treaties. Asking a
  decision without its statement field came to the framework just after (#266, `AskForStatement`). Numbers: `findings.md`, 13–14 November.
- **A country's own writ count** (14 November): `GameRules.WritsOverride`, read by `World.Writs`, for the
  person's unlimited writs on screen.

**Why Classic stalls, and three candidate changes** (13 November, from R1c's five uncapped games; _for the
tech lead to pick, nothing built_). The games lasted 165 to 220 turns. Four of the five ended in a
stalemate (12 turns with no province changing hands), and in 83% of turns no province changed hands at all.
Three things in the rules add up to this:

- **Land changes owner only by treaty, and most treaties undo the war.** Occupation never moves
  ownership. The 88 treaties were mostly white peace, which returns every occupied province and starts a
  3-turn truce. Courts offered white peace 806 times and envoys accepted 109. A demand for occupied land is
  enforced only at a war score of 90, which happened 9 times in 964 game-turns. A conqueror can hold half a
  neighbour for 20 turns, then lose it all to one accepted white peace.
- **A growing realm cannot move its armies.** Writs top out at 5, one kept for the court. After turn 60,
  rulers with 5 or more armies averaged 7.5 armies with 5.8 waiting, and ordered 2.7 a turn. The leader's
  armies pile up: kesh had 18 armies at turn 183 in game 22.
- **A last province holds out.** A realm breaks only when every province is occupied and it has no army
  left. In game 22, kesh (22 provinces, 412 soldiers) could not finish orsk, which kept one province and an
  army of 7. The stalemate rule then ended the game with kesh short of a win.

Candidates, each with its cost:

1. **Occupation held becomes ownership.** A province occupied for N turns in a row (say 8) passes to the
   occupier, with no treaty. Treaties remain the faster way. _Cost:_ a small rules change and one fact line
   ("held 5 of 8 turns"). The scripted pace tests need new pinned numbers. It snowballs more and makes
   white peace matter less. It adds no model time.
2. **Waiting armies keep a standing order.** An army left without a writ carries on with its last order:
   it keeps marching to its target, or holds. A cheaper alternative is to grow the writ cap with the army
   count (5 → base + armies ÷ 2). _Cost:_ standing orders are R3 work (wake conditions already exist) and add
   no model time. Growing the cap costs about 0.5 s a decision, so about +2 s a turn for a leader with 7
   armies, and the turn waits on the largest realm.
3. **End the last stand.** A realm breaks when every province it owns is occupied, whatever army it has
   left elsewhere. The stalemate counter also stops while some country occupies another's land. _Cost:_
   two small rules changes and one new test each. A cornered player loses a little sooner. Alone it does not
   speed the middle game.

_Mine:_ 1 and 3 together. Together they would end most Classic games in well under 100 turns at no model
cost, and leave 2 for R3, where standing orders belong. That reading is unmeasured: the scripted series
would show it in minutes before any live run.

**Built, both off** (14 November): `GameRules.OccupationBecomesOwnership` (candidate 1: a province occupied that
many turns in a row passes to the occupier; 0, the default, never) and `GameRules.BreakWhenAllOccupied`
(candidate 3, the break rule only; the stalemate counter is unchanged). With candidate 1 on, the rulers' facts
show each occupation's count ("corwick (held 3 of 8 turns)"). **Scripted, they change nothing that matters:**
100 Classic games of the `mixed` lineup take 77 turns off, 74 to 76 with either or both on. Scripted rulers
press a war to the end and do not stall; when they are made to make peace as readily as the models do, they
settle within 8 turns, so no occupation lasts long enough. Only model rulers stall, so which candidate helps can
only be read from a live run: for example 5 uncapped games with both on, on R1c's uncapped seeds 21 to 25.
Numbers: [`findings.md`](../llm-wiki/findings.md), 14 November.

The screen's half is the screen session's.

**The screen's half, first part: built** (13 November). Watch and replay of a writs game by stand-in
rulers: the pieces, the march lines and the step animation, the battle clash and its report card, the
occupation hatching and its drawing-in, the treaty's flow from hatch to wash under a seal, hatching
lifting from freed land, war scores, and `ReplayBuilder` rebuilding armies. The GreyBoxStrategy README's
"The writs turn on screen" has the detail. Rule mode on the writs turn (writs in the top bar, the
awaiting marks, the army cycle key, orders queued above End turn) came in the third part, below.

**R4 on screen, first part: built** (14 November). Generated worlds in all three ways of playing:
- New game routes every combination (`NewGameChoice.Route`);
- a rule game opens on your realm;
- the camera zooms and pans on any size;
- pieces scale with the spacing of provinces on screen;
- realm lists and the chronicle fold;
- frame time is measured on a huge world.

**R4 on screen, second part: built** (14 November).
- The chart stays sharp at any zoom: provinces come from the cell grid per screen pixel, not the id map.
- Collisions are settled among pieces, tags and every kind of name, and tested at three zooms.
- Realm names are drawn.
- The province panel shows settlement tiers and special places. The tiers themselves were already drawn by
  development; changing them during play waits for R6's buildings.

**R4 on screen, third part: built** (14 November).
- Detail up close: the visible window is drawn again at screen scale by the chart's own code.
- Farms and settlements grow with development.
- Rule mode on big maps: a next-army jump and markers at the screen's edge.
- The province panel speaks to a player.

Still open in R4:
- local facts and per-call target sets (Sim and Agents);
- the id map's adjacency test at 2048 and 4096.

**The screen's half, third part: built** (13 November). Rule mode on the writs turn:
- writs in the top bar and the council panel;
- the awaiting marks, and the armies left without a writ;
- the army cycle key (N);
- orders as one plan a turn, answered round by round;
- treaty terms as cards with the war score;
- fog of war for your country.

The storyteller now plays the writs turn on screen. Unlimited writs for the person alone needed a
per-country writs setting in the rules; it is `GameRules.WritsOverride` now (14 November), read by `World.Writs`.

**The screen's half, second part: built** (13 November). Model rulers play the writs turn live on the
screen, a court and a marshal for every country (`WritRuler`), with each decision recorded with its
army and opening in the inspector with its prompt. The GreyBoxStrategy README's "model rulers, live"
has the detail.

**What R1a built, and what it decided** (_Mine_ unless said):
- **The writs turn is a setting, not a replacement.** `GameRules.Turn` (`OneMove`, the default, or
  `Writs`) keeps today's game byte for byte: the pinned 200-game series and the recorded replay pass
  unchanged. `GameSetup.Writs` sets conquest, a 150-turn headless cap, and a 20-turn stalemate ending (R1b: 12 turns).
- **The stalemate ends a game.** Section 6.1 asks only that it be counted. A headless run needs an end,
  so 20 turns (R1b: 12) without a change of owner or controller ends the game as `Stalemate`, the leader named
  and nobody winning.
- **A border crossing:** the winner steps through, and the loser never crosses. Since the winner now
  stands in the province the loser was leaving, the loser falls back from it, with no second battle.
  Section 1.1's "the loser stays in the province it was leaving" cannot hold with the winner there.
- **Sieges, stores, unit types and generals' traits wait for R3** (unit types were built in R3's first part, 14 November, behind `GameRules.UnitTypes`, off). A walled province is fought for
  through its militia (5 + 10 a fort, plus the capital bonus; R1b: 3 + 6 a fort) rather than besieged. Wake conditions 3
  and 6 (a siege ends, stores run low) wait with them.
- **Recruiting** keeps today's levy (5 + 5 a province, paid at 2 gold a soldier). It appears at the
  capital, joining an army standing there up to 100, the rest forming new armies. Companies of 10 by
  type come with R3.
- **The status-quo offer, the other treaty terms and the war-weariness parts of 5.1 wait for R7**, as
  the page planned.
- **What the scripted players needed.** Without the random events, two scripted neighbours armed in
  step forever, and neither outnumbered the other enough to declare war. War is now weighed against
  the share of a rival's army that defends a province (`DefenceShare`, 0.7, as the one-move script
  does). The expansionist weighs war before arming, the cautious after.
- **Measured by simulation:** 0.95 armies awaiting orders a ruler a turn on Classic, against the
  page's estimate of 1.0. A medium generated world rarely ends within 150 turns (19 of 20 games
  unfinished), so the game's length needs tuning before R1c's live runs.

**R1b's first part: the pace, 12 November.** The tech lead asked for a game a person finishes in
about an hour. Scripted games before and after, same seeds:

| Scripted games (mixed line-up) | Turns, mean / median / 90th | Ended | Countries out at turns | Treaties a game |
|---|---|---|---|---:|
| Classic, before (40 games) | 123 / 121 / 150 | 23 one left, 8 stalemate, 9 at the 150 cap | 49, ... | 30 |
| **Classic, after** (40 games) | **78 / 80 / 106** | 39 one left, 1 stalemate | 18, 34, 57, 78 | 8 |
| Medium, before (20 games, 150 cap) | 150 | 1 one left, 19 at the cap | | 41 |
| **Medium, after** (20 games, 150 cap) | **141 / 150 / 150** | 10 one left, 10 at the cap | 22, 36, 55, 102, 132 | 12 |
| Medium, before, no cap (12 games) | 226 / 217 / 292 | all one left | 46, 63, 106, 164, 215 | |
| **Medium, after, no cap** (12 games) | **178 / 152 / 213** | 10 one left, 1 stalemate, 1 still going at turn 500 | 23, 31, 55, 102, 148 | |
| For comparison, after, domination at 75% | Classic 63 / 58; medium 144 / 118 | | | |

The levers that worked:
- **Armies:** two steps a turn over open ground; three writs; a weaker militia (3 + 6 a fort).
- **Wars:** wars fought out rather than settled small. The scripted rulers' new thresholds; demands
  enforced at 80; truces of 3 turns.
- **The game's end:** a 12-turn stalemate.

The levers that did not:
- earlier demands, since every treaty starts a truce;
- two wars at once;
- a realm breaking when its capital falls.

**The numbers in the design sections above are the first ones** (writs 2 + 1 for every
8 provinces, a 5-turn truce, enforced terms at 90, a 20-turn stalemate). R1b's defaults for the writs turn
are in the levers above: three writs, 3-turn truces, demands enforced at 80, a 12-turn stalemate.

**Not yet "an hour" for a medium world:** about 180 turns on average (median 152). The medium rows "after" were measured on #277's new worlds, the rows "before" on R2's; Classic is the same board in both. Its last war between two large realms is
where the time goes. Section 6.1's pressures for that (fear of the leader, war weariness, the
status-quo offer) belong to R7.

_For the lead:_ a 75% domination win gives 144 turns on medium (median 118) and 63 on Classic, if a shorter default is
wanted before R7. **Decided 13 November:** "Both, conquest default" (answer 9 in [11](#11-the-leads-answers-to-this-pages-questions)).

**Three writs** raise R1c's model time: decisions a ruler a turn rise from about 1.9 to 2.3 on Classic.
R1c measured 2.9 decisions a ruler a turn (14.4 a turn for five) and 7.1 s of model time a turn.

### R2. The map generator, headless, and a drawn-chart spike

- **Sim:**
  - `Sim/Map/Generator`: the steps in [2.3](#23-the-steps-from-seed-to-world), the checks in
    [2.4](#24-how-a-generated-map-is-checked), special-place slots and templates (effects still off
    until R3 and R6 use them), names.
  - The size, shape and country presets.
  - A `check this map` balance run.
- **Agents:** the world-maker agent, with its fallback tables.
- **Screen (spike):**
  - One scene that rasterises a generated medium map with sea, coast ripples, rhumb lines, terrain
    glyphs, washes and labels.
  - Captured at 1080 p and 4K through the Unity CLI, beside today's painting, for the lead to judge.
- **Tests:**
  - A seed's world hash is pinned.
  - 500 seeds per size pass every connectivity check.
  - Adjacency from cells equals the graph; there are no corner borders.
  - Fairness spreads stay within their limits.
  - Names are unique and valid.
  - Generation time per size.
- **Playable:** as after R1; any generated world plays headless.

**The headless generator is built, 12 November 2026** (before R1, so it plays today's rules). It lives in
`Sim/Map/`: `MapGenerator`, `MapChecks`, `MapGeometry`, `MapNames`, `SpecialTemplates`. The demo README
says how to use it. Measured on 100 seeds of every size and shape: every world passed every check (the
table is in the R2 pull request). Not built yet:
- the world-maker agent: `ISpecialPlaceMaker` is its hook, filled from tables today;
- the legend call;
- neutral garrisons by terrain, which waits for terrain's effects in R3;
- the 200-game balance certificate per preset. The runner plays a series on one generated world
  (`GameSetup.MapSeed`), which is its first half;
- the drawn-chart spike, which is the screen's. **Built the same day:** the demo README's
  [drawn-chart spike](../../Demos/GreyBoxStrategy/README.md#the-drawn-chart-spike-12-november) section
  says how it is drawn, what it costs and what today's rules make look wrong. Its pull request
  compares it with the painting. The paper texture was built in the second slice (13 November; see the demo README's "second slice"). Realm names were drawn in R4's second part and detail up close in its third (14 November), in the R4 paragraphs above.
  Not built yet: levels of detail beyond provinces and regions. Occupation hatching was built with the writs turn on screen (13 November, below).

What the design got wrong or left open, and what was decided (_Mine_ unless the lead says otherwise):
- **Capital spacing.** Section 2.3 asked for 4 steps on medium. With six countries on 60 provinces each
  reach is about ten provinces, and neighbouring capitals stand about 3.5 steps apart. At 4, compact
  worlds failed nearly every try. Now: 4 when each country has 20 or more provinces to itself,
  otherwise 3.
- **Rival capitals within 6 steps.** With eight or ten countries, some realm must sit inland, with six
  or seven rivals near against three on the coast. No placement met a spread of 2. Now the spread
  allowed is 2 up to six countries, then half the countries less one (3 for eight, 4 for ten).
- **Reach and ties.** "Nearer to it than to any other" left provinces at equal distance to nobody. On
  large maps those ties were a fifth of the land, so a capital's reach swung by a third with where its
  ties fell. A tied province now counts as shared equally among the capitals tied for it.
- **Terrain from height made a continent's middle all mountains.** The land's height carries the shape's
  falloff. Since capitals may not stand on mountains, they were pushed to a ring, and reach could not
  balance. Terrain now ranks a relief noise of its own, plus the ridges.
- **Corner contacts.** Repairing them one by one made new ones elsewhere. What works: smooth the borders
  first, then grow each short contact into a real border, and let the cell join a side only when that
  makes no new short contact. Where neither works, on the coast, the contact cells become water.
- **Neutral land and starting realms** fix the same number and disagree (section 2.2). The realm size is
  the setting. The neutral share follows: 60% small, 70% medium, 76% large and 80% huge with realms of
  3, against the page's 55%. The lead may want realms of 4 or 5 on the larger sizes.
- **Determinism.** A first version took its annealing odds from `Math.Exp`, whose last bit can differ
  between runtimes. It now uses only + − × ÷. Pinned world hashes measured under .NET match under
  Unity's Mono.
- **Speed.** One try at huge takes about 0.25 s under .NET and 0.7 s in the Unity editor. Retries make
  the whole: in the editor, 0.3 to 11 s for the exported huge worlds (16 tries at worst). The page's
  target of 2 s is met per try but not for every seed. Fewer retries would need a fairer first try, or
  fairness re-tried on the same geometry.
- **Shapes that read as figures** (the tech lead's review of the first sheet, 12 November). An ellipse, an
  oval lake, two round lobes, a land cut flat by the frame. Now each shape's proportions vary by seed, and
  two warps bend it before it is read: a broad one about 15% of the chart, and a fine one about 5.5%
  that cuts bays and pushes out headlands. The land fades out before the frame, so a peninsula or a long
  coast no longer runs off the chart's edge: section 2.2's "open to one edge" became a broad land
  narrowing to a tip. Land detached from the main land stays as scenery islands. It must stand two cells
  of sea from the main land and three from the frame, and be no bigger than a province. It is never
  played, as section 2.2 says. Every seed's world changed, and the pinned hashes were re-pinned.
- **Rivers, roads and development** (the lead, on seeing the drawn chart: farms, buildings and
  generated roads "would look fantastic").
  - **Rivers** drain from high land to water by a priority flood over cell corners, so none ends in a
    pit, and a river may form a province border exactly.
  - **Roads** follow the cheapest paths on the rules graph: plains are cheap, ridge cuts are never used,
    and a river costs a crossing, recorded as a bridge. Highways form a tree over the capitals; tracks
    join the ports and the most populous provinces.
  - **Development** is 0 to 4, from population, capital and port, in one function (`Development.Of`)
    that R6's buildings will feed.
  - Checks: every river ends in water or another river; the roads join every capital; every road step
    is between neighbours and bridges any river on that border.
  - **No rule reads any of them.** What they could mean later (_Mine_, to design when a slice wants
    them): a march along a road takes no extra turn on rough ground; a river border is a defensive line,
    like a ridge but crossable; a bridge is a chokepoint an army can hold; development is what R6's
    economy grows and what plunder lowers.
- **Progress and a time budget**, for a menu that must never hang. The generator reports after every
  try, and after a budget returns the best world found with its problems. A world cut short by the
  clock is the only one that may differ between machines.
- **The huge worlds that failed reach** after 50 tries. Up to eight more capital searches now run on
  the same geometry before a world is remade. A world that the first four searches made fair comes out
  unchanged.
- **Lloyd relaxation** runs three times, not two, and provinces under 0.7 of the mean area take a layer
  of cells from larger neighbours. Both cut the "area" failures.

### R3. Armies in depth

- **Sim:**
  - The three types and the counter rule; generals and traits.
  - Walls and sieges, including "forts hold the road"; supply model B and the army cap.
  - Fog of war for facts and scripted players; support and pursuit.
  - The battle report's factors.
  - Each on its own rules flag, so the sim can A/B them one by one.
- **Agents:**
  - The `recruit_*` actions; the army's line with mix, general and stores.
  - Seen armies written through fog; the report as a turn fact.
- **Screen:**
  - The army panel with mix, general and stores pips; siege tents and pips.
  - The full battle report card; fog in rule mode.
  - The inspector shows what a ruler saw.
- **Tests:**
  - Counter values; trait effects.
  - Sieges: their length, a breach, a broken siege.
  - Stores in rich and poor land; famine.
  - Fog: exactly the provinces the rule lists; totals rounded; forest hides size.
  - The report's factors sum to the result.
- **Playable:** Classic with deep armies.

### R4. Generated worlds on screen

- **Screen:**
  - The renderer from the spike, finished: every layer of [2.6](#26-how-the-map-is-drawn-by-code),
    levels of detail.
  - Province, realm and region labels.
  - The development layer's settlement tiers (from population; buildings come in R6).
  - Special places shown with their stories in the province panel.
  - The camera framing any size.
  - Map settings on the menu.
- **Sim and Agents:** facts kept local on big maps (neighbours and the two largest rivals only); the
  per-call target sets.
- **Tests:**
  - No label collides at three zoom levels.
  - The rasterised id map's adjacency equals the graph at 2048 and 4096.
  - Frame time on the huge map.
  - Every size preset plays a whole headless game.
- **Playable:** any generated world, any size, all three modes; Classic as a preset.

### R5. Events with choices and chains

**Part of this is built already, as the storyteller's stage S2** (12 November, in the demo's
[`README.md`](../../Demos/GreyBoxStrategy/README.md#stage-s2-dilemmas-events-on-many-tales-game-aware-stories-12-november)
and the research page's [stage S2](painted-realm-research.md#stage-s2-design-12-november)): dilemmas
(`+choices`), group events (`+multi`), tales continued by the model (`+chains-free`) or planned by the game
(`+chains-skeleton`), the digest of what happened (`+history`) and province traits (`+traits`). They are
arms of the storyteller, off by default, on today's board. The checklist below is not ticked: it describes the slice, and the README's S2
section does not mention the held event card in rule mode.

The lead: "we can have generic ones but we should also create a complex ones with chain of events
and also the ones that require a decision from the player or persona. Like the EU5 events ... also
like depends on the province"; "maybe we can even make multi affected. Like one events affect all";
chains "even bigger ones but they should be rare"; "the storyteller sees what happens in game and its
events can be soetimes influenced by that or reference that"; and about who writes chains, "I would
like to have model write them ... but if you insist we can do both and compare and test".

- **Sim:**
  - **Dilemma templates** (_Mine_): an event whose two or three options are the game's own, each an
    effect from a closed list, such as `refugees` (take them in: +1 population, −2 food for 3 turns;
    turn them away: −10 opinion with their land; conscript: +10 pikes, −1 stability).
  - **Chains** as small state machines of 3 to 5 steps (rare longer ones up to 8). A chain's step
    can depend on how the last choice went.
  - Province traits as conditions for events.
  - Events on a group, each member choosing for itself.
  - The storyteller's digest of what happened.
- **Agents:**
  - The storyteller picks a dilemma template or a chain step and a target, and writes the title and
    the situation.
  - The affected ruler chooses with `choose_first`, `choose_second` or `choose_third`.
  - **The chain A/B:** skeleton chains (the game fixes the steps, the model writes them) against
    model-led chains (the model picks each next step from the allowed list, with what came before as
    facts).
- **Screen:**
  - The event card with options in rule mode, held for the person, as the lead asked of the card.
  - Chain progress in the chronicle.
- **Tests:**
  - Every option's effect; chain transitions; rarity.
  - Group events ask each member.
  - The story check on options.
- **Gaps exercised:** #266 items 2 to 6.
- **Playable:** as R4, with events that ask.

### R6. Economy and buildings

- **Sim:** population, food, gold, goods and trade deals' goods, buildings and their rules, famine;
  levies of the land ([3.11](#311-making-the-roster-our-own-research)).
- **Agents:** the `build_*` actions and their two-province moves lines; the realm's goods and food in
  the facts.
- **Screen:**
  - The development layer complete: field strips, pennants, walls rings, specials, roads,
    scaffolding.
  - The development map mode; the economy ledger in the top bar's right end (the screen page's home
    for it); building from the province panel.
- **Tests:**
  - Each building's effect; the building-levels rule; growth and famine.
  - Goods via a deal.
  - Development tiers match their thresholds.
- **Playable:** the economy on any map.

### R7. Diplomacy and the realm

- **Sim:**
  - The rest of the treaty terms (`demand_province`, `demand_tribute`, `offer_province`,
    `offer_tribute`); enforced terms at 90; the status-quo offer.
  - Agreements and their answers; calls to arms; breaking deals.
  - Opinions with reasons and decay; war weariness.
  - Stalemate counting.
- **Agents:** the diplomacy actions; the answers; the two largest reasons in each rival's line.
- **Screen:**
  - The diplomacy matrix gains alliances, tribute, deals and passage.
  - The country panel lists opinions with every reason.
  - Court scenes for agreements (the screen page's item).
- **Tests:**
  - Each agreement's terms and end; a refused call to arms ends the alliance.
  - Opinion decay; a breach's costs.
  - The status-quo offer fires after 10 stuck turns; a demand at 90 is never asked.
- **Playable:** the full v2 game.

### R8. Orders without a cap

- **Sim:**
  - The cap goes, behind a rules flag.
  - Automation orders (`guard`, `campaign`, `watch_border`) run by the scripted tactics for one army,
    with their wake rules; `follow` and groups.
  - The decision budget a ruler a turn.
- **Agents:** the marshal's automation actions; the court asked until `none` or out of gold, within
  the budget; the "ordered this turn" line capped at five.
- **Screen:**
  - No writs in the top bar; the next-army key cycles every army.
  - An automation glyph on a piece, and its target drawn faintly.
- **Tests:**
  - An automated army acts as the scripted player would with one army.
  - Each automation's wake rule; the budget holds and is deterministic.
  - With the flag off, R7's game is unchanged.
- **Playable:** the v2 game without a cap; writs as a setting.

### R9. The model shapes the world

- **Sim:**
  - Three candidate worlds per idea; feature templates and how each is applied and re-checked.
  - Candidate start sites within the fairness spread; characters and what each sets.
  - The realm's own unit ([3.11](#311-making-the-roster-our-own-research)).
- **Agents:** the Cartographer agent and its six steps ([2.8](#28-a-world-shaped-through-the-framework)),
  with seeded fallbacks for each.
- **Screen:**
  - The loading screen showing each choice.
  - **How this world was made** on the menu, opening the inspector.
  - The realm card at the start.
- **Tests:**
  - Every option offered passes the checks.
  - A feature that fails is refused and the world stays valid.
  - The fallbacks give a full world with the server off.
- **Playable:** model-designed worlds, or seeded ones as before.

### R10. Names and histories by the model

- **Sim:**
  - Name and history checks ([2.9](#29-names-and-histories-written-by-the-model)) with table
    fallbacks.
  - The names file cached per seed.
  - Names fixed in the recording.
- **Agents:** the naming decisions per region and per country, and the history decisions; the
  60-second budget; the storyteller reading a struck province's history.
- **Screen:** the loading screen with names appearing on the map; histories in the province and
  country panels.
- **Tests:**
  - Each check refuses its case: reserved word, duplicate, prefix, near duplicate, block list,
    unsuitable word, digit, unknown place.
  - The cache gives identical names.
  - A timeout falls back cleanly.
- **Playable:** new names and histories every game, or tables.

### Issue-ready checklists

One list per slice, ready to paste into a sub-issue of #20. Each slice's pull request also updates
the demo `README.md`, `docs/demos/grey-box-strategy.md` where the rules change, a root `CHANGELOG.md`
line, and `findings.md` for anything measured.

**R0, settings and the map as data**
- [x] `WorldMap` data and JSON loader; `MapDefinition` from it for any size
- [x] Classic preset with hand-set terrain matching the painting
- [x] `GameSettings` (built as `GameSetup`), serialised; stored in recordings: screen
- [x] Victory modes: conquest, domination (default until R1), turn limit; headless turn cap
- [ ] Model presets as files; seats and personas from settings
- [ ] New game menu and the settings screen with its seven tabs; display settings
- [ ] State texture sized per province
- [x] Test: Classic in domination mode is byte-identical to today's 200-game `summary.md`
- [x] Test: settings round-trip; conquest ends; old recordings replay

**R1, the turn of writs and armies as pieces**
- [x] `Army`, orders, standing orders and the wake conditions (six of eight; sieges and stores in R3)
- [x] Writs a turn, the order cycle, the second court order
- [x] Simultaneous movement with terrain turns; border crossings; re-check on landing
- [x] Battles between armies, retreat and rout, militia and neutral garrisons
- [x] Occupation and controller; neutral land taken by entering; freeing occupied land
- [x] War score; `offer_white_peace`, `demand_occupied`, `cede_occupied` and their answers; broken realms
- [x] Occupation hatching, the treaty flow from hatch to wash with the seal, hatching lifting on return (watch and replay, #284)
- [x] Scripted expansionist and cautious with armies; random player
- [x] Court and marshal agents, the army in `Correlation`, both moves lines, chained decisions, memory 4 (R1b, `WritRuler`); the army statement rule waits for its own A/B (R1c measured the baseline: 35% of statements repeated word for word)
- [ ] Pieces, march lines, step animation, battle card; rule mode's writs, cycle key and order queue
- [ ] `ReplayBuilder` rebuilds armies
- [x] Tests: costs, simultaneity, determinism, writ cap, every wake condition, occupation never changes
      the owner, a treaty transfers exactly its term, scripted beats random; [x] the fake model plays the
      scripted game (R1b)
- [x] Sim: armies awaiting orders a ruler a turn, game length, battles
- [x] Live: the latency probe; then the persona table, 20 games, 40-turn cap, against the one-move game (R1c, 13 November)

**R2, the generator headless and the chart spike**
- [x] Land shapes, seeds, provinces, adjacency, ridges and passes, terrain quotas, coasts, ports
- [x] Capitals, starting realms, goods, population, names and their checks; garrisons by terrain wait for R3
- [x] Connectivity and fairness checks; retries; the balance run tool: a series on one world, no certificate yet
- [x] Special-place slots and templates; fallback tables; [ ] the world-maker agent; [ ] the legend call
- [x] Integer noise; a pinned world hash per size
- [ ] Spike scene: a medium map drawn by code, captured at 1080 p and 4K beside the painting, for the lead
- [x] Tests: seeds per size pass (100 per size and shape by hand, a few in every run); cell adjacency equals the graph; no corner borders; generation time

**R3, armies in depth**
- [x] Three types, costs, the counter rule (`GameRules.UnitTypes`, off; scripted only, 14 November)
- [x] The model rulers' recruit orders by type and the mix in their facts (14 November; unmeasured live, so still off)
- [ ] Generals: names, ten traits, second trait, death and capture
- [ ] Walls and sieges, breach, broken siege, forts hold the road
- [ ] Supply model B, the army cap; sim A/B of B against A against none
- [ ] Fog of war for facts and scripted players; totals rounded; forest; scouting
- [ ] Support and pursuit; the report's factors
- [ ] Every feature behind a rules flag; sim A/B each against off
- [ ] Army panel, siege marks, stores pips, the full report card, fog in rule mode
- [ ] Live: all on against R1, persona table, 20 games; fog on against off

**R4, generated worlds on screen**
- [ ] Renderer: paper, sea, ripples, rhumb lines, land, terrain glyph atlas, ridges, wash, borders
- [ ] Labels: province, realm, region; collisions; levels of detail
- [ ] Settlement tiers from population; special places in the province panel
- [ ] Map settings on the menu; camera for any size
- [ ] Local facts and per-call target sets for big maps
- [ ] Tests: id map adjacency at 2048 and 4096; no label collision at three zooms; frame time on huge
- [ ] Live: 10 seeds × 2 games, 60-turn cap; pace at 6 rulers

**R5, events with choices and chains**
- [ ] Dilemma templates and their options; the choice decision for rulers
- [ ] Group events, each member choosing
- [ ] Chains, rare long ones; skeleton and model-led variants
- [ ] Province traits as event conditions; the storyteller's digest of what happened
- [ ] The held event card in rule mode; chains in the chronicle
- [ ] Live: choices on against S1; skeleton against model-led chains; blind pick of two logs

**R6, economy and buildings**
- [ ] Population, growth, cap; food and famine; gold
- [ ] Goods and their effects; goods through trade deals
- [ ] Buildings, levels, costs, time, the levels rule, the specials by terrain
- [ ] Levies of the land: a variant per special place and good, recruitable only there
- [ ] The `build_*` actions and their moves line (two provinces a kind)
- [ ] Development drawn: tiers, fields, pennants, walls, specials, roads, scaffolding; development map mode
- [ ] Live: persona table; builds against the scripted choice; η² of building

**R7, diplomacy and the realm**
- [ ] Treaty terms `demand_province`, `demand_tribute`, `offer_province`, `offer_tribute`; enforced terms at 90
- [ ] The status-quo offer in a stuck war
- [ ] Truce, alliance, trade deal, passage; their answers
- [ ] Calls to arms; breaking deals; reputation as a reason
- [ ] Opinions with reasons and decay; labels; war weariness
- [ ] Stalemate counting
- [ ] Diplomacy matrix and country panel reasons; court scenes for agreements
- [ ] Live: opinion reasons in facts on against off (invented grievances); deals and length

**A, the advisor**
- [x] Advise mode with the person's country's facts: a `WritRuler` on the person's seat proposes the turn's
  plan, built 14 November (`Agents/Advisor.cs`, demo README "The advisor in rule mode"); "refuses in character"
  is not needed, as it only advises
- [x] On by default with model rulers; switchable in New game and in game (the pause menu, not the top bar)
- [ ] Its own model preset (today the session's model)

**R8, orders without a cap**
- [ ] The cap off behind a rules flag; writs kept as a setting
- [ ] `guard`, `campaign`, `watch_border` run by the scripted tactics; `follow`; groups
- [ ] The decision budget of 8, counted in decisions
- [ ] The court asked until `none` or out of gold; "ordered this turn" capped at five
- [ ] Screen: no writs, every army in the cycle, automation glyphs
- [ ] Live: writs against no cap with automation against no cap without, 40-turn and 100-turn runs, pace by turn window

**R9, the model shapes the world**
- [ ] Three candidates per idea, all checked; feature templates applied and re-checked
- [ ] Candidate start sites within the fairness spread; characters and their effects; the realm's own unit
- [ ] The Cartographer agent, its six steps, the fit rules as per-target availability, seeded fallbacks
- [ ] Loading screen, "How this world was made", the realm card
- [ ] Live: the same 20 seeds against the seeded generator; fairness, refusals, length; blind pick of two worlds

**R10, names and histories by the model**
- [ ] Naming per region and per country, histories per region; parsing the lists
- [ ] Every name and history check, with table fallbacks
- [ ] Names cached per seed and fixed in the recording; the 60-second budget
- [ ] The storyteller reads a struck province's history
- [ ] Loading screen with names arriving; histories in the panels
- [ ] Live: table names against cached model names on the same 20 seeds and geometry

## 11. The lead's answers to this page's questions

The first draft of this page asked eight questions; the lead answered them on 12 November, and the
page above now follows his answers. His words, and what each changed:

| # | The question | His answer | What it changed |
|---|---|---|---|
| 1 | Is a person held to the same cap on orders? | "Yeah lets have the same cap but to be honest I would eventually wont to have no cap of orders. We can have smth like civ has." | The same cap first; [1.6](#16-the-route-to-no-cap) and slice R8 design the route to no cap |
| 2 | Taking a province: at once, or by treaty? | "I would better want a peace treaty way. Like in EU4 where you conquer but you also need to do a treatty for it to become yours" | Overrode the draft's recommendation: occupation, war score and treaties ([5.1](#51-occupation-war-score-and-treaties)); the basic treaties in R1, the rest in R7 |
| 3 | The end of conquest: last ruler standing? | "Ok" | As drafted ([6.1](#61-conquest-the-default)) |
| 4 | A code-drawn sea chart for generated worlds, the painting kept as Classic? | "Ok" | As drafted ([2.6](#26-how-the-map-is-drawn-by-code)) |
| 5 | The default world: medium, about 60 provinces, 6 countries, a continent? | "Ok, but as I said, I want to end up with the possiblity to generate maps and maybe using the framework" | A stage where the model shapes the world through the framework ([2.8](#28-a-world-shaped-through-the-framework), slice R9) |
| 6 | Supply as stores, with a cap of 100 an army? | "Ok" | As drafted ([3.6](#36-supply)) |
| 7 | Province names from tables? | "Lets have model generate place names and history of provinces and countries eventually. Like it would add a lot of colors to the game where you have new names each time, its like we discussed with the country names" | Tables first; names and histories by the model ([2.9](#29-names-and-histories-written-by-the-model), slice R10) |
| 8 | Pikes, riders and bows? | "Lets have it like this for now but please do a reasearch on how we can make it more interesting and unique" | Kept; research and a pick in [3.11](#311-making-the-roster-our-own-research) |
| 9 | The writs game's win: conquest, or domination at 75% (asked with R1b, 13 November)? | "Both, conquest default" | As #282 built it: conquest the default, `domination_75` a named preset ([6.1](#61-conquest-the-default)) |

## The lead's answers, 12 November 2026

Asked by the framework tech lead session after the lead played all three modes. His words are
quoted; "Ok" means he accepted the default in brackets. The 11 November decisions are in
[`grey-box-strategy-screen.md`](grey-box-strategy-screen.md#the-leads-decisions-11-november-2026).
The last column says where this page uses each answer. His answers to this page's own eight questions, given later the same day, are in
[section 11](#11-the-leads-answers-to-this-pages-questions).

**After playing (same day):**

| Topic | His words | Here |
|---|---|---|
| Overall | "Overall the demo looks good but it is still to simple, I supose since its a prototype." | the page |
| Resolution | "its full hd and it hurts" (fixed in #268: the Game view was stretched; the UI now scales) | — |
| Map and armies | "I think the map is very small currently. The UI is kind of good for the prototype. I think we miss the features a lot also the map should show armies at all times and I think armies should become very detailed in ters of mechanics and usage, not its too dull." | 2, 3 |
| Feel | "the overall feelings are very good in terms of personas and stuff and speed. We need to develop it further I think." | 1.2 |
| Storyteller events | "I like all of them, they look very nice but they are too generic it seems. Like we can have generic ones but we should also create a complex ones with chain of events and also the ones that require a decision from the player or persona. Like the EU5 events. Would be also nice if some events present the interesting story of something and also like depends on the province and so." | R5 |
| Fog of war | "I see the future of the game to be more like EU5 or EU4 where you have multiple armies and you see like the total but there is a fog of war for actual units" | 3.8 |
| Inspector | "currently the inspector is mostly used to showcase. I think we should show what they do and what they say to the user." | R3's inspector line |
| The card while a person plays | "I think it should be hold". | R5's event card |
| The folder | "Lets not rename the folder for now." | status line |
| Images | "for images its better to use code by you as much as possible and use codex image gen when it is clearer a best way to do that." The RTX 5080 and the ComfyUI asset server may be used. | 2.6 |
| Mechanics | "Make sure the game has more mechanics." | the page |

**Map and scale:**

| # | Question [default] | His answer | Here |
|---|---|---|---|
| 1 | Provinces [about 60, 6 countries] | "I am thinking we need to start with the map generator already, to allow any form and size of it. WE can also use LLM model to design it maybe, in text way." | 2, R2 |
| 2 | Terrain types [five] | "Yes, there should be also some special provinces which LLM may add up to the world. Like sepcial lakes, special mines, special old castles" | 2.5, 3.2 |
| 3 | Sea [ports and sea trade now, fleets later] | Ok | 2.2, 4.4 |
| 4 | Screen | "We should make sure we have a settings and support multiple resoltuions and UI scales and etc. Would be nice to start with menu and the settings already" | 7, R0 |

**Armies:**

| # | Question [default] | His answer | Here |
|---|---|---|---|
| 5 | Number [as many as it can pay for, each led by a general] | "Ok. Maybe also do a research about it to see what can be done more too deepen mechanic." | 3.1, 3.10 |
| 6 | Unit types [three with a simple counter system] | Ok | 3.3 |
| 7 | Movement [one province a turn, terrain slows it] | "Ok, lets make sure they have animations too" | 3.2, 3.9 |
| 8 | Battles [one turn, with a battle report] | "One turn. Also, I want to make sure we have turns in kind of the way that Civilization does it, like where you can do multiple actions per turn" | 1, 3.4 |
| 9 | Sieges [forts turn battles into sieges over several turns] | Ok | 3.7 |
| 10 | Supply [an army far from home or in poor land loses men, simply] | "Ok, but we need to make sure we think how to do that." | 3.6 |
| 11 | Generals with traits [yes] | Ok | 3.5 |
| 12 | What you see of a rival army [presence and rough size when it borders your land; nothing deeper in] | Ok | 3.8 |

**Economy and buildings:**

| # | Question [default] | His answer | Here |
|---|---|---|---|
| 13 | Buildings [farm, market, barracks, walls, one special per terrain; three levels] | "Yes, and as I said I want it to be visible on the map" | 4.4, 4.6 |
| 14 | Resources [food and one trade good per province] | Ok | 4.2, 4.5 |
| 15 | Population per province [yes] | Ok | 4.1 |
| 16 | Trade as negotiated deals [yes] | Ok | 4.5, 5.2 |

**Diplomacy:**

| # | Question [default] | His answer | Here |
|---|---|---|---|
| 17 | Agreements [peace, truce, alliance, tribute, trade deal, right of passage] | Ok | 5.1, 5.2 |
| 18 | Opinions rulers hold of each other, visible, with reasons [yes] | "Yes, and yes" | 5.3 |
| 19 | Free-text negotiation [one ruler at a time first] | Ok | 5.2 |
| 20 | Breaking a deal costs reputation [yes] | "Ok, maybe we can add more things as stability, legitimacy or more. If you think we need it" | 5.5, 5.6 |

**Events and the storyteller:**

| # | Question [default] | His answer | Here |
|---|---|---|---|
| 21 | Events with choices [two or three options, the affected ruler picks] | "Yes, but maybe we can even make multi affected. Like one events affect all" | R5 |
| 22 | Chains [three to five linked events] | "I think we can allow even bigger ones but they should be rare. I also want to make sure that the storyteller sees what happens in game and its events can be soetimes influenced by that or reference that." | R5 |
| 23 | Who writes chains [skeletons the model fills; freer later] | "I would like to have model write them, because it seems to be it would be better but if you insist we can do both and compare and test" | R5's chain A/B |
| 24 | Province traits events can use [two or three per province] | Ok | 2.5, R5 |
| 25 | The storyteller's character [a wry chronicler] | Ok | — (as built) |
| 26 | Story-only events [occasionally] | Ok | 7 (storyteller tab) |
| 27 | Leaning toward the weak [mild, as a setting] | "Lets have it as a settings with different presets you can tweak before the game" | 7 |

**Playing and showing:**

| # | Question [default] | His answer | Here |
|---|---|---|---|
| 28 | Rivals' moves and speeches shown as they happen; private numbers hidden [yes] | Ok | 3.8 |
| 29 | An advisor model in rule mode [optional, off by default] | "Yes lets have it optional and enabled by default which you can disable in game or turn on in game" | 7, slice A |
| 30 | Saves and a written history [one autosave; a one-page chronicle] | "Lets have a special other story teller which will create an interactive story of what was happening using the log and events. We can add it later" | later; not in these slices |
| 31 | Game length [45 to 60 minutes, 40 turns] | "Lets actually make the default mode to be like conquer evereything and allow infinite turns" | 6 |
| 32 | Chosen at the start [model, persona mix, map size, storyteller on or off] | Ok | 7 |
| 33 | Order [armies with the new mechanics, then the bigger map, then events with choices and chains, then buildings and economy, then diplomacy] | Ok | 10 |
| 34 | Teammates in this demo [not yet] | Ok | — |

Rule-mode question 4 (a typed line for the player's own moves) was not answered; the default,
unnecessary, stands.

## Sources

Read on 12 November 2026 for ideas and conventions only; no text, names, numbers or art are copied.
Several are fan-written guides, used only for what a mechanic is, never for its numbers.

- Old World's shared pool of orders: Soren Johnson, [Designer Notes #1: Orders](https://designer-notes.com/?p=1679), and
  [a commands guide](https://culturedvultures.com/?p=159551)
- Civilization VI's combat, flanking, support and zone of control: [Civilization Wiki, Combat (Civ6)](https://civilization.fandom.com/wiki/Combat_(Civ6)),
  [Zone of Control (Civ6)](https://civilization.fandom.com/wiki/Zone_of_Control_(Civ6)),
  [CivFanatics, flanking and support](https://forums.civfanatics.com/resources/civ-vi-flanking-and-support.27610/);
  Civilization VII's commanders: [dev diary 5, combat](https://forums.civfanatics.com/threads/civilization-vii-dev-diary-5-combat.693621/)
- Europa Universalis IV's forts and zone of control: [development diary, 9 April 2015](https://forum.paradoxplaza.com/forum/developer-diary/eu4-development-diary-9th-of-april-2015.849816/)
  and [1 November 2016](https://forum.paradoxplaza.com/forum/threads/eu4-development-diary-1st-of-november-2016.979421/page-2);
  its supply-limit attrition: [developer diary 19](https://forum.paradoxplaza.com/forum/threads/europa-universalis-iv-developer-diary-19-war-has-never-been-so-much-fun.665825/)
- Europa Universalis V's supply, food and battles, from secondary guides: [Military & Logistics 101](https://europa-universalis-v-wiki.pages.dev/mechanics/military-logistics),
  [Pops, food and flanks](https://finalboss.io/war-in-europa-universalis-5-pops-food-and)
- Hearts of Iron IV's supply hubs: [dev diary, an early look at supply](https://admin-forum.paradoxplaza.com/forum/developer-diary/hoi4-dev-diary-early-look-at-at-supply-and-the-coming-year.1466076/page-3)
- Crusader Kings III's commanders and battle phases: [CK3 dev diary 3, war](https://admin-forum.paradoxplaza.com/forum/developer-diary/ck3-dev-diary-3-war.1279641/latest),
  [a combat guide](https://www.naguide.com/crusader-kings-iii-combat-guide/)
- Unit identity and growth: Humankind's emblematic units, [Humankind wiki, Culture](https://humankind.fandom.com/wiki/Culture);
  Battle Brothers' soldiers growing through risk, [a review](https://rpgamer.com/review/battle-brothers-review/)
- Map generation: Amit Patel, Red Blob Games, [Polygonal map generation](https://www.redblobgames.com/maps/polygon-map-generation/)
  (Voronoi provinces, noisy edges) and [a Voronoi maps tutorial](https://redblobgames.com/x/2022-voronoi-maps-tutorial);
  Azgaar's [Fantasy Map Generator](https://github.com/Azgaar/Fantasy-Map-Generator/wiki/Quick-Start-Tutorial) (grid, heightmap, states)
