# Painted Realm — research: a storyteller, voices, generated countries

**Status:** a proposal, written 11 November 2026. **Nothing on this page is decided.** One part
has since been built and measured: stage S1 of the storyteller ([below](#a-staged-recommendation),
12 November). Everything else on the page is still unmeasured by us. It answers three of the lead's design answers of 11 November that
needed research before design, for the strategy demo (working title Painted Realm, today
[`Demos/GreyBoxStrategy/`](../../Demos/GreyBoxStrategy/README.md), #20, design in
[`grey-box-strategy.md`](grey-box-strategy.md)). Where this page says what the repo has
measured, it links [`findings.md`](../llm-wiki/findings.md) or the demo's `README.md`. Every
other number is someone else's, linked, and says whose. The demo may become a commercial game
later, so each section ends with the licences.

| Idea | Recommendation, in one line | Framework? |
|---|---|---|
| [1. A storyteller that creates events](#1-a-storyteller-that-creates-events) | One more agent with no body. It chooses an effect template and who it hits from a closed list the game registers, and writes only the title and the story. The game decides when, how strong, and what is fair | Not at first. A documented pattern after the demo's A/B, then up to nine small gaps (the list at the end of the section) |
| [2. Local text-to-speech](#2-local-text-to-speech) | Kokoro-82M in the game's process, one voice blend per persona; Qwen3-TTS 0.6B as the fallback | No new API: `DecisionFeed` already hands a game every executed statement. A docs recipe at most |
| [3. Generated country identity](#3-generated-country-identity) | Seeded tables for everything the rules or the prompt depend on; the model writes only names and flavour; flags drawn procedurally; portraits from a pool pre-generated on the asset server | No |

## 1. A storyteller that creates events

> *"I want to allow model to create events which have like bonuses or minuses to 1 or more
> contries … a tools for the model that is going to be a master teller in the game."* — the lead,
> 11 November

### In plain terms

A sixth agent, the storyteller, is asked once a turn (or whenever the game decides) to make
something happen. It picks **what kind of thing happens** and **to whom** from a list the game
registers, and writes **a title and a sentence or two of story**. The game, not the model,
decides how strong it is, checks it is fair, applies it, and tells the rulers in its own words.
So *"a golden meteor falls on Orsk"* is the model's; *"+40 gold to orsk"* is the game's.

This is the framework's own rule applied to a new kind of agent: the model chooses from what the
developer registered, and the game runs the code (hard rule 5). The storyteller is an `Agent`
with no body, like a country (DR-011), whose targets are the countries.

### What the framework does today, read from the code on `test`

| Piece | What it does now | What it means for a storyteller |
|---|---|---|
| `AgentDecision` | Exactly three fields: `ActionId`, `TargetId`, `Statement` | One effect, one target, one free text. No magnitude, no second target, no title field |
| `DecisionSchema` | Fields `action`, `target`, `statement`, in that order. Its remarks say a field added later, *"such as an action parameter, goes after `statement`"* | Typed arguments are anticipated in a comment, not built |
| Targets | One flat list per call (`Agent.Targets`, `TargetSources`); each may carry a category (DR-020) and a description (`IDescribedTarget`) | A group of countries can be registered as one target |
| Availability | `IsAvailable` per action, `ITargetAvailability` / `[AvailableFor]` per target, both with a reason; `PairTargetsWithActions` holds the grammar to legal pairs | Fairness rules known before asking (cooldowns, budgets) hide options rather than forbid them in prose, the settled rule |
| Guards | `IDecisionGuard` / `DelegateGuard` see the chosen action, target, statement, the prompt and the token probabilities; a verdict is `Pass`, `RewriteToNone` or `Reject` | A guard can read the story. It cannot keep the effect and replace only the story |
| Handlers | `DelegateActionHandler`; `Agent.Execute` re-checks availability before running it and returns an `ActionResult` | The handler is game code that applies the template |
| `DecisionRound` | Several agents on one snapshot, executed in the game's order (DR-021) | The storyteller can sit outside the council: it is asked at upkeep, before the rulers |
| Observations | `IObservationSource` for standing facts; `DecideOptions.Observations` per call, placed by `ObservationPlacement.WithStimulus` (#239) | An event reaches a ruler as one of *this turn's* facts, which is where a deciding fact helps most (findings, 10 November, "Situations and events": +22 of 389 on `qwen3.5:4b`) |
| Memory | `RollingHistory` replays each past decision's JSON, statement included | The demo's gap N7: the 4B copied its own sentences for turns on end. A storyteller fed its own stories back would likely repeat them |
| Provider | `OllamaSettings.Temperature`, default 0 (greedy), set per provider | At temperature 0 the same prompt gives the same event. Variety has to come from the prompt, or from a second provider with a higher temperature (two providers are two queues against one server: the demo's gap N10) |

Measured facts that bear on it:

- **A local model never reads the schema** (findings, 10 October). Whatever the storyteller must
  know about a template, its strength or its rules goes in the prompt.
- **Small models choose well from a closed list.** With the paired schema, illegal answers went
  to 0 in GreyBoxStrategy (#247) and on the evaluation set (findings, 6 and 10 November).
- **Field order matters.** Free text before the action made the prototype rationalise its reply
  and collapse to "no action" (Appendix A, +11.7 points for action first); a reasoning field before
  the action was the worst of five variants. Ollama's native chat path also sorts a schema's keys
  alphabetically for some models (findings, 5 November), so a schema with more fields does not
  always reach the sampler in the order it was written.
- **Models invent facts in statements.** At the persona table, rulers declared grievances that
  never happened (*"Valen's peace is a lie; they will bleed for taking our land back"*, when Valen
  had taken nothing) and about a fifth of statements named someone other than the target (demo
  `README.md`, "Three games"). A storyteller's story will do the same: it must not be allowed to
  carry mechanics.
- **Personas change play measurably on the 4B** (η² 0.84 for aggression; the 2B 0.30). A
  storyteller persona would plausibly change its stories as well. Unmeasured.

### What it chooses, and what it writes

An event has six parts. The split this page proposes:

| Part | Who decides | How |
|---|---|---|
| When an event happens | **the game** | Its own pacing rule (DR-008, DR-015: triggering stays the developer's) |
| What kind of effect | the model | An action from the templates the game offers now |
| Which countries | the model | A target: one country, or a group the game registered |
| How strong, how long | **the game** in the first version | Scaled to the target's size (below); a model choice only in design C |
| Title and story | the model | Free text, in the statement |
| The numbers players and rulers read | **the game** | The game writes the mechanical sentence; the model's text never contains it |

### Four designs

#### A. Effect templates as actions, countries and groups as targets

The game registers 8 to 12 effect templates as ordinary actions, each with a handler that applies
it, plus `none` (no event this turn, which a storyteller should be allowed):

| Action | Takes | What the game applies |
|---|---|---|
| `windfall` | a country or a group | + gold, scaled to income |
| `blight` | a country or a group | − income for 2 turns |
| `plague` | a country | − a share of the army |
| `zeal` | a country or a group | + 1 stability |
| `unrest` | a country | − 1 stability |
| `volunteers` | a country | + soldiers |
| `walls_crumble` | a province | − 1 fort |
| `stronghold` | a province | + garrison or + 1 fort |
| `omen` | anyone | nothing: flavour only, a seed for later |

Targets are the five countries **and a few groups registered as targets of their own**, each an
`IDescribedTarget` with a category: `everyone`, `the_north` (valen, orsk), `the_south`,
`the_leader`, `the_weakest`, and the ten pairs if the game wants them (*"orsk_and_kesh"*). Five
countries, three to five groups and the 25 provinces stay under the demo's raised
`MaxListedTargets` of 40. Categories (`country`, `group`, `province`) say which template takes
which, so the paired schema keeps `plague the_north` out if the game wants plague on one country
only.

**Strength** is the game's. A template's effect scales with the target (RimWorld's storytellers
scale threats to the colony's wealth for the same reason, see "What others do"), so a windfall
for a one-province country is not the same gold as for a twelve-province one. If the lead wants
the model to choose strength, two or three templates get a `minor` and a `major` id
(`windfall_major`), which is Living Vale's bundled-target workaround (its gap 12) applied to
actions.

**Title and story** go in the statement. The storyteller's `StatementRules` ask for *"A short
title, a colon, then one or two sentences."*; the game splits at the first colon and falls back
to the template's own name.

- **What the framework lacks:** nothing to build it. Groups as targets and the colon split are
  workarounds a later API could replace (gaps 1 and 3).
- **Guards:** fairness rules known before asking are availability (next section). On the answer,
  one `DelegateGuard` for the story: it names no country outside the target (the guards page's
  name-check example, here easy, since there are five names), contains no digits (numbers are the
  game's), and stays under a length.
- **Hard rule 5:** the model picks a template and a target; the handler is game code; `Execute`
  re-checks first.
- **A small model:** this is the shape the repo has measured as reliable: a closed choice first,
  then free text that describes the choice it already made (the field order rule works *for* the
  story here). The likely failures, by analogy with the rulers: one favourite template every turn
  (the 2B idled 35% of turns, the 4B 10%), and a story that adds consequences the effect does not
  have.

#### B. Two steps: the story first, then a second call maps it to effects

1. The **storyteller** is asked with themes as actions (`fortune`, `disaster`, `faith`,
   `nature`, `intrigue`, `wonder`) and countries and groups as targets. Its statement is the
   story, three sentences at most, with nothing to keep it to the game's effects.
2. A **referee**, a second agent on the same model with a dry prompt, is asked with that story as
   its stimulus and offered the effect templates of design A. It maps the story to one template
   and target, or to `none`.

- **For it:** the story is not anchored on a template, so it can be stranger. The referee's call
  is the framework's most measured shape: free text in, a closed choice out, exactly as a player
  line is. A story that no template matches is an "impossible request", which the Phase 3 suite
  contained 6 of 6 times on the 2B with the guards on (findings, 3 November). `DecisionChain` links
  the two calls in the log.
- **Against it:** two calls (about 1 s on the RTX 5080 at the 4B's 0.46 s a decision, at upkeep,
  where nobody waits). The story can promise what nothing applies (*"the king of Kesh dies"*):
  the player reads it, and the game does nothing. The referee choosing `none` throws the story away
  or leaves it as flavour. The story must be shown only after the referee has decided, beside the
  game's line.
- **Why it is not the free-text-first problem:** Appendix A's collapse happened within one reply,
  where the free text chose the action. Here the story is the stimulus of a separate call. What it
  risks instead is a mismatch between story and effect, which has to be measured.
- **What the framework lacks:** nothing to build it. A documented two-agent recipe would help.

#### C. A new framework capability: an action with typed arguments

`ActionDefinition` would declare parameters, each a closed set or a bounded text:

```text
blight:   targets  1 to 2 countries      (an array of target ids)
          strength minor | major          (an enum)
          turns    1 | 3 | 5              (an enum)
          title    at most 40 characters  (a string)
```

The schema would carry the parameters as fields after the target, one shape per action in the
paired schema, and `AgentDecision` would gain an `Arguments` map.

- **What the framework lacks:** all of it: `ActionDefinition` parameters, schema fields per action,
  the parser, `AgentDecision.Arguments`, prompt lines describing them (a local model never reads
  the schema), the few-shot block showing them, the decision log and telemetry, and the guards.
  `TargetGroundingGuard` scores one target field; an array of targets needs it per item. Whether
  Ollama's grammar honours an array's `minItems`, `maxItems` and unique items for an enum would
  have to be probed, as `paired_schema_probe.py` probed `anyOf`.
- **Order is unmeasured.** The schema's own remark puts a parameter after the statement. For an
  event the story should follow the strength it describes, so the parameters would want to go
  before it. Both orders, and the sorted-keys path, need a probe before anything else.
- **Guards** become simpler to write (each argument is typed) and the fairness budget can read the
  strength directly.
- **A small model** makes four or five choices in one reply instead of two. Nothing in the repo
  measures that, and Appendix A's history is that every added field cost accuracy until shown
  otherwise.
- **Payoff beyond this game:** Living Vale's gap 12 (*"three potions"*, a counter-offer price)
  wants the same thing; its entry says to keep the bundle workaround *"until there is evidence"*.
  A storyteller that clearly needs strength and two targets would be that evidence.

#### D. The game deals, the model picks and tells

The game's director rolls three candidate events each turn from its own tables (template,
target, strength, already fair), registers them as described targets (*"card_2: plague on kesh,
−6 soldiers"*), and the storyteller has one action, `tell`, whose target is the card it picks and
whose statement is the story. It is RimWorld's split, the director decides what is possible and
the storyteller only chooses, with a model in the chooser's seat.

- **For it:** fairness is entirely the game's; the model's job is the one it does best.
- **Against it:** it is the least "the model creates". The lead asked for the opposite. It is the
  right **fallback** if A turns out unfair or repetitive, and a useful middle arm.

#### Side by side

| | A. Templates | B. Two steps | C. Typed arguments | D. Cards |
|---|---|---|---|---|
| Builds on today's API | yes | yes | no, a schema change | yes |
| Model decides who and what | yes | the referee does | yes, and strength | picks among three |
| Strength | the game, or variant ids | the game | the model, typed | the game |
| Several countries | group targets | group targets | an array | the game's card |
| Story freedom | after the choice | most | after or before the choice | after the choice |
| Calls an event | 1 | 2 | 1 | 1 |
| Biggest risk | one template every turn | story promises what nothing applies | fields the model was never measured on | not the lead's ask |
| Graduates to the framework as | a pattern | a recipe | an API | a pattern |

### Keeping it fair: what the game checks

Most rules are known before the model is asked, so they are availability, and the illegal options
are absent from the schema (the settled rule):

- **A budget per country.** Each template has a value in gold (a windfall +40, a plague −30 worth
  of soldiers). A country whose net event value over the last six turns is above +X cannot be
  offered a bonus, below −X cannot be offered a penalty (`[AvailableFor]`, with the reason).
- **No kicking a country out.** No penalty on a country with one province; no template takes a
  capital or a province with its last soldiers; no event ends a country.
- **No crowning the leader.** No bonus for a country at 12 or more provinces. Whether the
  storyteller should instead help the weakest is a persona question (RimWorld's three storytellers
  differ exactly there).
- **Cooldowns.** A template not twice within four events; a target not twice in a row (the guards
  page's cooldown example, as availability).
- **Opening turns.** None before turn 3, so the first expansion is the rulers'.

On the answer, a guard checks the story (names, digits, length). Its trouble: a guard can only
pass, contain or reject the whole decision, so a good effect with a bad story is lost (gap 4).
Until then the handler can replace a bad story with the template's own line and log it.

**Seeded dice keep it measurable.** Every roll in the game is a hash of *(seed, turn, who,
purpose)* (`Dice`), so a storyteller's own rolls, under their own purpose, leave every battle's
dice as they were. That makes "the same game with and without a storyteller" a fair pair.

### How events become facts the rulers react to

The game writes every line a ruler reads; the model's title is quoted, never its numbers:

| Who | Where | Line |
|---|---|---|
| The country hit | this turn's facts, priority 1 | `"The Golden Star": a meteor of gold fell in our hills. +40 gold.` |
| Everyone else | this turn's facts, priority 3 | `News: orsk gained 40 gold ("The Golden Star").` |
| The country, while it lasts | a standing fact | `Blight in our fields: income −10 for 2 more turns.` |
| The player | the event log, read aloud by a narrator (section 2) | the title and the whole story |

- **Should a ruler read the story itself?** A ruler that reads *"a meteor of gold"* has
  something to talk about, and something to copy. The 4B copied its own memory (N7); the 2B
  copied its statement rule (N9). Title only against title and story is an A/B arm.
- **Room.** A country gets at most three turn facts, most important first. An event competes with
  war; at priority 1 it is above a harvest and below losing land.
- **Lasting effects** want a standing fact that counts itself down, which is #216's fact ledger
  (F4, *"not needed yet"* on 10 November). Events are the first thing in this game that need it,
  and the ledger's design, one instance shared by several agents, fits "everyone hears the news"
  (gap 5).

### Keeping 30 turns from repeating

1. **Availability does the hard part.** Cooldowns on templates and targets are deterministic and
   cost the model nothing.
2. **A list of what it already told, not its replies.** One standing fact,
   `Told so far: The Golden Star (orsk, windfall, turn 4); The Grey Cough (kesh, plague, turn 7) …`,
   the last five, instead of `RollingHistory` replaying whole stories to be copied (gap 6).
3. **A seeded inspiration line.** The game draws two words from a curated list per call, from the
   game seed and the turn, so it is reproducible and A/B-fair: `Inspiration: lanterns, a drowned
   bell.` Injected randomness of this kind is a common answer to LLM outputs collapsing onto a
   few favourites (see "What others do").
4. **Temperature.** The rulers run at 0. A storyteller may want more, on its own provider (gap 7).
   Raising it changes the model's choices too, so it is an arm, not a default.
5. **Threads.** A template can open one: an `omen` (*"a comet seen over Merrow"*) makes `comet_falls`
   available on Merrow two to four turns later, and only then. Chains like this are the backbone of
   the grand-strategy event systems described below, and they are all game state.
6. **A storyteller persona.** A calm chronicler, a trickster, a stern god. Plausibly the cheapest
   source of variety between games, since personas measurably changed the rulers' play.

### What others do

Ideas and pitfalls, described, not copied. Sources are marked as wikis, forums or press where
they are not primary.

- **RimWorld's storytellers** (described by the fan-run
  [RimWorld Wiki](https://rimworldwiki.com/wiki/AI_Storytellers), and its
  [raid points page](https://rimworldwiki.com/wiki/Raid_points)). The storyteller decides *when*
  and *what kind*; the size of a threat is computed from the colony's wealth and people, times
  difficulty, times an "adaptation" factor that falls after a loss. The three storytellers differ
  in pacing: one ramps up with rests, one leaves long calm gaps, one is random. **For us:** strength
  is the game's, scaled to the target; the storyteller's persona is its pacing and taste.
- **Left 4 Dead's director** ([Valve's GDC 2009 slides](https://cdn.fastly.steamstatic.com/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf)).
  It tracks each survivor's intensity and cycles build-up, peak and relax, holding back when
  intensity is high. **For us:** a per-country tension figure (land lost, wars) could make the
  game's pacing rule skip a country that is already being hammered.
- **Paradox's event systems** (Crusader Kings, Europa Universalis, Stellaris; described in modding
  forums and the fan-run [Stellaris](https://stellaris.paradoxwikis.com/Event_modding) and
  [EU4](https://eu4.paradoxwikis.com/Event_modding) modding wikis). Events fire from triggers and a
  "mean time to happen", are weighted, carry flags so they fire once, and schedule follow-ups.
  Effects are scripted templates. **For us:** that is design A, with the model in the writer's and
  picker's seat, and it is where threads and once-only flags come from.
- **Caves of Qud's procedural history**
  ([Grinblat and Bucklew, 2017](https://pcgworkshop.com/archive/grinblat2017subverting.pdf)) makes
  the events first and invents a reason afterwards. **For us:** that is exactly the order of design
  A: the effect is chosen, then explained.
- **Drama managers and experience management.** Façade's drama manager chooses the next beat to
  follow a designer's tension curve
  ([Mateas and Stern, 2003](https://users.soe.ucsc.edu/~michaelm/tenurereview/publications/mateas-tidse2003.pdf));
  [Riedl and Bulitko's survey](https://faculty.cc.gatech.edu/~riedl/pubs/aimag.pdf) frames the job
  as coherence against player agency. Kreminski's story sifting
  ([Felt, AIIDE 2021](https://doi.org/10.1609/aiide.v17i1.18903)) finds patterns in an event log,
  which is one way a later stage could call back to old events (*"the second plague in Kesh"*).
- **LLMs as game masters.** FIREBALL ([Zhu and others, 2023](https://arxiv.org/abs/2305.01528)),
  from real D&D play, found a model does better when given the true game state and asked for
  commands the game executes. That is our split. CALYPSO
  ([2023](https://arxiv.org/abs/2308.07540)) keeps the LLM as an assistant to a human DM. AI
  Dungeon's own help pages
  ([why the AI forgets](https://help.aidungeon.io/faq/why-does-the-ai-forget-or-mix-things-up))
  show the pitfall of keeping the world's state in the model's text: keep it in code.
- **Outputs collapse onto favourites.** Stories written with AI help are more alike as a set
  ([Doshi and Hauser, Science Advances 2024](https://www.science.org/doi/10.1126/sciadv.adn5290)),
  and many LLMs answer far more alike than people do
  ([arXiv 2501.19361](https://arxiv.org/html/2501.19361v1)). Two remedies that need no training:
  prepending unrelated random words raised diversity, with a 4B model among those tested
  ([arXiv 2601.18053](https://arxiv.org/abs/2601.18053), January 2026), which is the inspiration
  line; and asking for several candidates with probabilities and sampling one in code
  ([Verbalized Sampling, arXiv 2510.01171](https://arxiv.org/html/2510.01171v1)), which the paper
  shows helps larger models more.
- **Shipped games.** *Fall of an Empire*, a solo-developed survival strategy game, says on its
  [Steam page](https://store.steampowered.com/app/1830290) that a small local LLM generates its
  storylines and events, and ships its larger event model as a free download of its own. It is the
  closest precedent found, and shows the store disclosure in practice. *Suck Up!*
  ([Steam](https://store.steampowered.com/app/2726370)) runs its characters on a cloud model paid
  for with in-game tokens: the recurring cost a local model avoids. Ubisoft's NEO NPC
  ([Ubisoft news](https://news.ubisoft.com/en-us/article/5qXdxhshJBXoanFZApdG3L)) is a prototype in
  which writers set each character's frame and guardrails, the same division as ours.

**The pitfalls, in short:** state kept in the model's text drifts; a model left alone picks its
favourites; a story can promise what the game cannot do; and "random" is not "interesting" without
pacing. Every system above that works keeps the numbers and the pacing in code.

### A staged recommendation

**Stage S1, the smallest version worth building (demo only, no framework change).** *Built and
measured on 12 November: see "What S1 found" below.*
Design A with the game's strength. One storyteller agent on the same `qwen3.5:4b` (game runs are
4B only), asked at upkeep from turn 3, one event a turn at most, `none` allowed. Eight templates,
five countries and four groups as targets, the paired schema, the fairness rules above as
availability, one story guard, the facts table above, `Told so far` and the inspiration line. It
**replaces** the per-country random table (harvests, bandits, unrest), so the game's volume of
events stays comparable.

**The A/B.** Control: today's random event table. Arm: the storyteller. Same seeds, same personas,
20 games a table, paired by seed with the run kit's `compare.py`, as the table of models was.
What "interesting and unique" could be measured as, from logs alone where possible:

| Question | Measure | From |
|---|---|---|
| Is it varied within a game? | distinct template and target pairs per game; distinct titles; the share of two-word phrases a story shares with earlier stories in the same game | the event log |
| Is it varied between games? | titles shared across seeds; the template mix per game against the control's | the event log |
| Do games diverge? | two storyteller runs of one seed with different inspiration draws: the share of provinces with a different owner at turn 10 and 20 (the control's dice are identical, so its own divergence is 0) | game logs |
| Do rulers react? | the share of hit rulers whose next statement names the event; the move mix in the turn after a penalty against the control's turns after a bad harvest; the scorecard's liveliness section | decisions, scorecard |
| Is it fair? | net event value per country and its Gini; whether value tracks standing; wins by seat against the control | game logs |
| Does the story match the effect? | a person labels 50 events blind: right sign, right country, invents a consequence | a sample |
| Is it fun? | the lead and team read two event logs of one seed side by side, not told which is which, and pick one | a sample |
| Does it behave? | illegal answers, guard containments, stories replaced, latency | telemetry |

**What S1 found** (12 November; the demo `README.md`'s "Storyteller" and `findings.md`, "A storyteller
in GreyBoxStrategy"). Built as planned, with eight templates (`windfall`, `bad_harvest`, `plague`,
`volunteers`, `zeal`, `unrest`, `stronghold`, `walls_crumble`), six groups and the provinces as
targets, the fairness rules as availability, the story check in the game, the "told so far" and
inspiration lines, the title only in the rulers' facts. Against the random table, `qwen3.5:4b`,
personas, 20 games, two storyteller runs:

- **No illegal answer, no containment, 0.69 s a call;** liveliness, sense and voice within noise
  except more peace offered (8.1 to 11.3 and 11.0 a game) and slightly shorter statements.
- **Varied within a game:** 6.7 and 6.2 kinds of event a game against the table's 4, more weight
  (690 against 527 gold a game), more gifts (42% against 31%), and 29% and 31% of hit rulers
  naming the event against 21%.
- **Games diverge:** two runs differing only in the inspiration words had another owner in 22% of
  provinces at turn 10 and 45% at turn 20; the control against itself, 0%.
- **Fair, as a rubber band:** value against standing r −0.37 and −0.33 (the table's −0.15): it hits
  the leader and props up the weak. The leader still wins as often.
- **The failures the page predicted, and one it did not.** The first live game cycled four
  templates under the three-event cooldown, three blows to every gift; a four-event cooldown and no
  third blow or gift in a row fixed it. Stories match their effects well (one reader, 50 events:
  the right sign in 48, the right country in 47, an invented consequence in 3). The titles are the
  weak part: the 4B wrote none in 62 of 62 smoke events until the rule had an example, and still
  leaves out half; titles repeat across games (72% and 77% of them), mostly the inspiration words.
- **Two personas play differently, in both runs:** the builder holds far more (39% to 61% and 71%),
  the opportunist offers peace twice as often.
- **Gaps hit:** 1, 3 (now with evidence), 4, 6, 8 and 9; not 2, 5 or 7. Two new: the moves-line
  workaround again for a second agent, and a reply that runs away.

**Stage S2.** First, from what S1 found: titles (the "told so far" line by template and target
only, never the fallback titles; inspiration words kept out of titles, or many more of them) and
the builder's holding (an arm with the title kept out of the rulers' facts would separate the
fact from the gold). Then the arms planned on S1: design B against A (story and match, from the labelled sample and the
blind pick); inspiration on and off; title only against title and story in the rulers' facts; a
storyteller persona. Design D only if A is unfair or flat.

**Stage S3.** Threads and lasting effects (with the fact ledger), more templates, and then the
graduation decision.

**What would graduate into the framework.** First a documentation page, *a game master agent*:
an agent with no body whose targets are other agents and groups, availability as the fairness
budget, the game's own words for the facts it broadcasts, and the A/B above as the way to check
it. APIs only where the demo wrote code a second game would also write, from the gap list below.
The lead called this *"one of the gold points of the framework"*: the documented pattern, backed
by measurements, is what makes it one. Design C is the only part that changes what the model is
asked for, so it needs a decision record and its own A/B before anything else.

### Stage S2: design (12 November)

What the lead asked for after reading S1's events: *"too generic … we should also create a complex
ones with chain of events and also the ones that require a decision from the player or persona …
some events present the interesting story of something and also like depends on the province."*
Built in the demo only, on today's rules, with no framework change. Every piece is a modifier on
`+storyteller`, off in `default`, because each changes what a model reads. Nothing names a province
or a country: effects, dilemmas, skeletons, traits and presets are data the game registers, so the
same code runs on `realm-rules-v2`'s generated maps.

**1. Events with choices: how one action, one target and one statement make a dilemma.** Three
designs:

| Design | How | For | Against |
|---|---|---|---|
| **Dilemma templates (chosen)** | The game registers dilemmas, each with two or three options and each option's effect (`sellswords`: hire them for gold, or send them away). The storyteller picks a dilemma as its action and a target, and writes the story; the game writes the option lines from its own data and states the amounts. | One call, the paired schema still holds the grammar, every option is applicable by construction, fairness is availability | The option lines are the game's, not the model's |
| A second call writes the options | A first call picks the event; a second writes two or three option lines, each mapped to an effect by a list of effects | Options in the model's voice | One more call, and free-text options must be mapped back to effects: a parser the model can break |
| Typed parameters (#266 item 2) | One action `dilemma` with typed fields: option ids from a closed list, a line per option | One call, model-written options, still closed | Needs the framework's typed parameters, which do not exist |

A ruler answers a dilemma as one more decision: a `Choice` moment, the options as its actions (each
dilemma's option ids, registered on the seat and available only on that choice), the story and
what each option does among its facts. It is asked between turns, on the state the event is told
on, in a round of its own; the chosen effect lands at the next upkeep with the event. A scripted
seat takes the dilemma's default (the option that changes least), a random seat any option, and a
person picks on the screen through `Game.PendingChoices` and `Game.Choose`; an unanswered choice
takes the default. *What typed parameters would have made simpler:* the storyteller could have
written its own option lines and chosen the strength in the same call; today both are the game's.

**2. Events that strike many (`+multi`).** A group event reaches the members the fairness rules
allow, and is open when at least two are allowed (one for `the_leader` and `the_weakest`); S1 opened
a group only when every member was. Each member gets its own amount (a windfall is a turn of *its*
income), and a group dilemma gives each member its own choice of the same story. Who was spared is
recorded.

**3. Chains, both ways, for the comparison the lead agreed to.** The game holds every chain: an id
(`tale_1`), its subject (a country, a province or a group), the events told in it with what each
ruler chose, and a status (open, completed, abandoned). An open chain is a **target** of category
`tale`, described with its title and what happened so far, so continuing a chain is an ordinary
decision: an effect template on a tale strikes the tale's subject.
- **(a) Free (`+chains-free`):** any recent event of the storyteller can be continued with any
  effect its subject may take now, and `end_tale` closes a chain with an epilogue (no effect). The
  model decides what follows and when it ends.
- **(b) Skeletons (`+chains-skeleton`):** the game registers four skeletons (a pretender, a deep
  vein, a fever, a long winter), each a list of stages, each stage one or two templates or a
  dilemma, the next stage depending on what was chosen. `begin_<skeleton>` starts one; when its next
  stage is due, that stage is the action open on the tale. The model writes every stage's story.
- **Rules for both:** at most two open chains; a chain not continued for three turns, or whose
  subject is out, is abandoned; at most six events in a chain, and only one chain a game may pass
  three (the long ones are rare); a chain is "started" once it has two events.

**4. Game-aware stories (`+history`).** A `Recent happenings` line: captures, failed attacks on a
country, wars declared, truces, eliminations, revolts, a change of leader and rulers' choices from
the last three turns, from the game's reports. The story check then allows a country or province
that line names, and catches **invented history** mechanically: a sentence with a verb of capture or
battle (*took, captured, seized, conquered, fell, sacked, defeated, won …*) that names a province
nobody has taken or fought over yet, or two countries that never fought, has its story replaced
(`replaced: invents …`). It cannot check a claim with no name in it (*"after the great battle"*), a
motive, a count of the dead, or what an earlier story said.

**5. Province traits (`+traits`).** Every province gets a terrain, a landmark and a resource, drawn
from lists by the game's seed (any map, any size). They are in the province's description, and three
templates need one: `rich_vein` (a resource that is mined), `flood` (a river valley, a marsh or a
coast) and `pilgrims` (a holy landmark).

**6. Titles (`+title-call`).** The statement is the story alone, and a second, short call to a
scribe agent asks for the title of that story: two to five words, nothing else. With it, the "told
so far" line names template and target only (S1's owed fix), so there is no title to copy.

**7. Presets.** `StorytellerSettings`, plain data: how hard it leans (none, mild as in S1, strong),
how often it acts, how often a dilemma and a new chain are open, story-only events on or off.
Named presets `even_hand` (the measured one), `underdog`, `cruel_fate`, `quiet_years`, `saga`;
`+preset-<name>` picks one. A menu can list `StorytellerSettings.Presets`.

**8. Story-only events.** `omen`, no effect, neither gift nor blow, rare (cooldown), when the preset
allows.

**What the screen should show** (it is another session's; everything is plain C# in `Sim/`):
`Game.PendingChoices` (each an `EventChoice`: the country, the title, the story, the options with
the game's own effect line, the default) and `Game.Choose(country, option)` for a person's seat; an
`EventRecord`'s `Choices` (what each country chose), `ChainId` and `ChainStage`; `Game.Chains` (each
a `Chain`: title, subject, events, status) to follow a chain; `World.Traits` for a province's card.

**The measurement.** `personas` table, `qwen3.5:4b`, seeds 1 to 20, arms interleaved in blocks of
five: S1 (`default+storyteller`) as the control; S1 with the title call alone; and S2 whole
(`+s2`, every piece above) with free chains, and with skeletons.

**What S2 found** (12 November; the demo `README.md`'s "What S2 did" and `findings.md`, "The
storyteller's stage S2"). Built as designed above, plus one change the smoke games forced: offered
dilemmas and tales on 40% and 30% of turns, the 4B chose one dilemma in 28 events and began no tale, so
the game deals each turn's kind. Against S1, `qwen3.5:4b`, personas, 20 games an arm:

- **The title call fixes the titles** (missing 47% to 0%, distinct within a game 60% to 92 to 96%), at
  0.37 s an event, with nothing else in the game changed.
- **Twice the variety** (kinds of event 6.65 to 10.55 and 11.30), and the war unchanged: battles,
  conquests, wars, peace and endings within noise, rulers' sense, voice and character too.
- **Choices in character where the options tempt:** a pretender paid off by the builder and the merchant
  15 of 15 times, defied by the warmonger and the proud ruler 11 of 11. Elsewhere the active option wins.
- **Tales:** free, 2.2 a game and longer (3.6 events), recurring motifs; skeletons, 1.1 a game (2.3),
  each turning on a choice. Both leave most unfinished (8 of 44 and 4 of 22 completed).
- **Game-aware stories are rare** (5 to 6% name a real happening; 4 invented battles replaced), though
  the happenings line is read on nearly every call.
- **Costs:** a milder rubber band (value against standing −0.37 to −0.25 and −0.21), hit rulers naming
  the event 29% to 22%, 0.5 s more a turn.
- **Gaps:** 1, 2 (the main cost), 3, 4, 5, 6, 8 and 9 hit; two new, N13 (an optional action the model
  never takes, so the game must deal frequency) and N14 (a follow-up call for one decision).

### Framework gaps for a storyteller, ready to become an issue

Written as #244 is. **Rules for every item:** one pull request per item or small group, into
`test`; anything that changes what the model reads or how a reply is judged ships off and gets a
row in #151 before it becomes a default (hard rule 2); no input taxonomy (DR-008), no scene outside
`Runtime/Unity/` (DR-011), no new dependency (DR-009); every new test is seen failing once. None of
these is needed for stage S1.

**1. One target per decision, an event for several countries.**
*Why.* `AgentDecision` has one `TargetId`. An event on "the north" or "orsk and kesh" has to be a
group registered as a target of its own.
- [ ] Document groups as targets (a category, a description listing the members) in `targets.md`
- [ ] Revisit as a target array only with item 2, if the demo's groups prove clumsy

**2. No closed choice beside action and target (strength, duration, quantity).**
*Why.* Strength is either the game's or bundled into action ids (`windfall_major`). Living Vale's
gap 12 (quantities, prices) is the same need from another game.
- [ ] A probe first, no framework code: does Ollama's grammar keep an enum field, and an array of
  target ids with `minItems`/`maxItems`, on both chat paths, and in which order (findings,
  5 November, sorted keys)
- [ ] A design and a decision record: parameters on `ActionDefinition`, where they sit relative to
  the statement, how the prompt describes them
- [ ] Off by default; an A/B on the evaluation set and in the demo before any default

**3. One free-text field (title and story).**
*Why.* An event wants a title and a story; the demo splits the statement at a colon.
- [ ] Document the split as a recipe; a second text field only if a second game needs one

**4. A guard cannot keep the move and replace only the statement.**
*Why.* A verdict is `Pass`, `RewriteToNone` or `Reject`. A fair effect with a story that names the
wrong country is lost, or the handler patches it outside the pipeline, so telemetry calls it clean.
The rulers' invented grievances (demo `README.md`) would use the same verdict.
- [ ] A verdict that keeps the action and target and drops or replaces the statement, recorded in
  `GuardsFired` and the decision log
- [ ] Tests, and a line in `guards.md` on when to prefer it to containment

**5. A fact many agents read, in their own words, that expires (#216 item 4, the fact ledger).**
*Why.* An event is one fact five rulers hear: *ours* for one, *news* for four, and a standing line
for a lasting effect that counts down. Today the game writes each country's facts by hand.
- [ ] Take up #216 item 4 with this as its first user; keys, expiry in the game's units, one
  instance in several agents' `ObservationSources`

**6. Remembering what an agent did, as a list, not as replies (#216 item 3, N7).**
*Why.* A storyteller needs "what I already told" without its stories replayed to copy.
- [ ] Fold into #244 item 4's option for what a remembered decision holds (move only, move and
  outcome, all): a storyteller is a second user for "move only"

**7. Sampling per agent.**
*Why.* `OllamaSettings.Temperature` is per provider, default 0. A storyteller that wants 0.8 beside
rulers at 0 needs a second provider, which is a second queue against the same server (N10).
- [ ] Look at a per-call or per-agent temperature (and seed) on `DecideOptions`, sent as Ollama's
  `options`; changes the reply, so off and A/B'd

**8. Fairness and cooldowns are written by every game.**
*Why.* Budgets per target and cooldowns per action and target are availability code a storyteller,
the bell in GreyBoxVillage and any director will each write.
- [ ] Document the recipe in `guards.md` beside the cooldown example; a helper only if two games
  write the same one

**9. No measure of variety.**
*Why.* The evaluation harness scores right and wrong, and `ReplyChecks` flags an echo, an empty or
a too-long reply. Nothing counts how alike a run's statements are, which is what "unique each run"
would be judged by. GreyBoxStrategy's scorecard counts word-for-word repeats only.
- [ ] Build the measures in the demo's scorecard first (distinct pairs, shared phrases, divergence)
- [ ] Move the ones a second game uses into `Evaluation/`

### Licences

The storyteller adds no model and no asset: it runs on the rulers' `qwen3.5:4b`, whose licence
is to be read off the model card and recorded before any release (section 3), and its stories are
text the game generates at play time. Two things to know for a commercial release: Steam asks a
game to disclose content generated live by AI and the guardrails against illegal content (section
3's licences), and a story is filtered by nothing but the game's guards, so the story guard is
also that guardrail.

## 2. Local text-to-speech

> *"maybe there is a local AI that we can also use to do TTS or what do we do ?"* — the lead,
> 11 November

### In plain terms

Yes. Small open voice models now run on a gaming PC fast enough to speak a ruler's one-line
statement in well under a second, beside the 3 GB language model, and several have licences a
commercial game can ship. You do not "train" a voice: you pick a preset, mix two presets, or (in
some models) describe the voice in words. The best fit for this game is **Kokoro-82M**: tiny,
Apache-2.0, 54 preset voices that blend, and it can run inside Unity itself.

All figures below are the vendors' own or come from community projects, as marked; none was
measured by us. Quality ranks are from the Artificial Analysis
[Speech Arena](https://artificialanalysis.ai/text-to-speech/leaderboard), a crowd-sourced
leaderboard ranked by blind votes, read on 11 November 2026 (the page shows no snapshot date).

### The options worth considering

| Model | Size | Arena rank (Elo) | Speed (whose figure) | Voices | Weights licence; can a commercial game ship it? | From Unity |
|---|---|---|---|---|---|---|
| **[Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M)** v1.0 | 82M parameters; ONNX about 325 MB, about 90 MB int8; well under 1 GB of VRAM | #66 (1061) | 35–100× real time on an RTX 4060 Ti, first audio about 300 ms on GPU, about 3.5 s on an old i7 CPU ([Kokoro-FastAPI](https://github.com/remsky/Kokoro-FastAPI)'s figures) | 54 presets in 8 languages; presets blend by ratio (`af_bella(2)+af_heart(1)`); no emotion tags | Apache-2.0; **yes**. Trained on permissive data plus two CC-BY sets, which need a credit | In-process: Unity's own [Inference Engine sample](https://github.com/Unity-Technologies/sentis-samples/blob/main/TextToSpeechSample/README.md), or [KokoroSharp](https://github.com/Lyrcaxis/KokoroSharp) (MIT, ONNX Runtime); or a local server |
| **[Qwen3-TTS](https://github.com/QwenLM/Qwen3-TTS)** 0.6B / 1.7B (January 2026) | about 4 GB of VRAM for 0.6B (a blog's figure) | #93 (928) | first packet "as low as 97 ms" (vendor); on an RTX 4090 through NVIDIA's game plugin, first audio 164 ms ([NVIDIA's docs](https://docs.nvidia.com/ace-for-games/qwen-tts/1.0/nvigi-developer-pack.html)) | 9 presets; **a voice written as a description** (1.7B only); cloning from 3 s of audio | Apache-2.0; **yes**. The NVIDIA plugin's own licence was not found stated | NVIDIA's in-game plugin (a C API), or a Python server |
| [Phonon 31M](https://huggingface.co/gradium/phonon-31m-en) (October 2026) | 31M; 64.5 MB | #41 (1113) | "starts speaking in 7 ms on a laptop CPU" (vendor) | about 100 presets; cloning only in the vendor's paid studio | Apache-2.0, English only; **yes**. Its 40M sibling is CC-BY-NC: do not mix them up | Its own native runtime; no ONNX or Unity route found. A month old |
| [Pocket TTS](https://huggingface.co/kyutai/pocket-tts) (Kyutai) | 100M | not listed | about 6× real time on 2 CPU cores (vendor) | presets and cloning | CC-BY-4.0, gated download with prohibited uses; **probably**, with a credit | [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) has C# bindings |
| [Chatterbox](https://github.com/resemble-ai/chatterbox) Turbo / Multilingual | 350M / 500M | #85 (1021) | Nano about 3× real time on CPU (vendor) | cloning, `[laugh]`-style tags | MIT, **but** every clip carries a watermark, and the README says it was retired as research in August 2026 | official ONNX |
| [Piper](https://github.com/OHF-Voice/piper1-gpl) | 15–65 MB a voice | not listed | very fast on CPU | one voice per file | Engine GPL-3.0; most English voices derive from research-only data ([the lessac card](https://huggingface.co/rhasspy/piper-voices/blob/v1.0.0/en/en_US/lessac/high/MODEL_CARD)); **only as a separate program, with each voice's licence checked** | sherpa-onnx |
| Windows' built-in voices | part of Windows | not listed | instant | the system's few voices | ships with Windows; **yes** | a Windows speech API through a plugin |

**Ruled out for a commercial game:** XTTS-v2 (Coqui's non-commercial licence), F5-TTS and
Spark-TTS (CC-BY-NC weights), Fish Audio's S1-mini and S2 Pro (non-commercial or research
licences), and Microsoft's VibeVoice Realtime ("research purposes", with an
[audible AI disclaimer](https://huggingface.co/microsoft/VibeVoice-Realtime-0.5B)). Dia, Zonos,
Orpheus, Sesame CSM, Higgs Audio and the larger Kyutai and Parler models are mostly Apache or MIT,
but at 1 to 3B parameters and 6 to 10 GB of VRAM they do not fit beside the language model on an
8 GB laptop card.

### Two traps

- **eSpeak-ng is GPL-3.0.** Many engines (Piper, Zonos, sherpa-onnx's Kokoro and Piper models,
  KittenTTS) turn text into sounds with it. Linking it into the game, as a DLL in the same process
  or statically, would put the game under the GPL; running it as a separate program is commonly
  argued to be fine, but that is a question for a lawyer, not this page. **Kokoro needs no eSpeak
  for English:** its own phonemiser, [Misaki](https://github.com/hexgrad/misaki), is Apache-2.0,
  and both Unity's sample and KokoroSharp use a C# port of it. eSpeak is only Misaki's optional
  fallback for unknown words, which the game can leave out.
- **Cloning real people.** A voice is protected as a likeness in many places, and model licences
  forbid cloning without consent. Presets, blends of presets and voices written as a description
  avoid it; so does a hired voice actor whose contract covers synthetic use.

### Recommendation

**First try: Kokoro-82M, in Unity's process, English, no eSpeak.**

1. Start from Unity's Inference Engine sample and check the model imports on Unity 6000.3.23f1:
   earlier Inference Engine versions lacked operators it needs, and a
   [forum thread](https://discussions.unity.com/t/importing-kokoro-82m-tts-onnx-model-current-unsupported-features-and-future-support-timeline/1589028)
   still disputes it. If it fails, KokoroSharp on ONNX Runtime (a native plugin the game
   carries; the framework never references it, so hard rule 6 is untouched).
2. To hear the voices before any Unity work, run Kokoro-FastAPI, which speaks an OpenAI-style
   endpoint, on a developer machine.
3. Speak a statement only after its move lands (below), on the GPU if it fits, otherwise on the
   CPU: one sentence on CPU may take a few seconds on an older machine, which is acceptable for a
   spectated turn and worth measuring.

**Fallback: Qwen3-TTS 0.6B**, through NVIDIA's game plugin or a local Python server: Apache,
streaming, and a voice written as a description suits a ruler. About 4 GB of VRAM is tight beside
the 4B on an 8 GB card, and it ranks below Kokoro in the arena. The built-in Windows voices are
the last resort that always works. **Worth watching:** Phonon 31M, if it gets an ONNX or C route.

### A voice per persona

| Persona | Kokoro voice (to be chosen by ear) | Speed |
|---|---|---|
| warmonger | a deep male preset, blended with a rougher one | 1.05 |
| builder | a calm, warm preset | 0.95 |
| opportunist | a light, quick preset | 1.1 |
| merchant | a bright, friendly preset | 1.0 |
| proud | a slow, formal preset | 0.9 |
| the storyteller (section 1) | a narrator's preset, different from all five | 0.9 |

The voice follows the **persona**, which rotates across seats, not the country, so a listener
hears the same character in every game. With generated countries (section 3), the generator
picks a blend and a speed per ruler from a list the team has listened to.

### What would have to be measured

- **Latency** to the first sound, for one 13 to 15 word statement (the persona 4B's length), on
  the RTX 5080 and on the RTX 4060 laptop, with the 4B resident: GPU and CPU.
- **Whether it costs the decisions anything.** A decision's latency with and without speech
  running beside it, paired by seed (hard rule 3's spirit: two workloads on one card share it).
- **Names.** Generated or invented names (*merrowdeep*, *ilvarion*) are where a phonemiser
  guesses. A list of every place and country name, listened to once, with a pronunciation
  dictionary for the ones it gets wrong.
- **Whether people can tell the rulers apart.** Five voices, ten clips, the team names the
  persona.

### Does any of it belong in the framework?

**No new API.** `DecisionFeed.Recorded` (#166) already raises every decision, and a second time
with `DecisionStage` executed when `Agent.Execute` carries it out. A game that subscribes, takes
the executed entries with a statement, and hands them to its voice already has the hook the brief
asks about, with no dependency in the core. Speaking at execution rather than at decision means a
move refused or made stale is never spoken. A short recipe in `logging-decisions.md`, *speaking
statements aloud*, is worth writing once the demo has done it. A voice *provider* interface in the
framework would only be worth proposing if a second game needs the same plumbing, and even then
it would live behind a version define like any provider (hard rule 6).

### Licences

Kokoro's weights are Apache-2.0; the credits for its two CC-BY training sets go in the game's
credits. Misaki is Apache-2.0, KokoroSharp MIT, ONNX Runtime MIT. Unity's Inference Engine is a
Unity package under Unity's licence. Speech generated at play time is live-generated content for
Steam's AI disclosure (section 3's licences).

## 3. Generated country identity

> *"Lets have some kind of generator of each, this way we can also see what are the generated
> apsects of the country and we can try to use qwen image model to generate them a flag or even
> their persona … whether it should be a part of the framework"* — the lead, 11 November

### In plain terms

At the start of each game, each of the five countries gets a name, colours, a flag, a capital, a
ruler with a name, a voice and a portrait, and a line or two of history, and the screen shows
them. Most of it should come from **tables rolled from the game's seed**, because the rules, the
prompt and the measurements depend on it. The model can write the parts that are only flavour.
Flags are better **drawn by code** than painted by a diffusion model. Portraits can come from the
asset server, but **made before the game ships**, not while it runs.

### What a country is, and who makes each part

| Part | Made by | Why |
|---|---|---|
| Colours (two or three) | a table of palettes the team picked for the map's style, five drawn without repeats | The map's centrepiece is colour; five countries must stay distinguishable, also for colour-blind players |
| Persona (how the ruler plays) | **the five measured personas**, assigned as today | The persona is what the character test measured (η² 0.84 on the 4B). A generated persona changes what the model reads, every game differently, and needs its own A/B before it replaces them |
| Traits shown to the player (*"seafaring, devout"*) | a table, drawn per country; a trait may add a `Knowledge` line only once measured | Same reason |
| Country, capital, ruler and province names | the model, from a seed, checked by the game; or a syllable table | Names are flavour, but they become target ids (below) |
| Motto and two lines of history | the model | Flavour only; never read back to a ruler as fact |
| Flag | code, from the colours and a seeded design | Below |
| Portrait | a pre-generated pool, chosen by traits | Below |
| Voice | a blend and speed per persona (section 2) | |

**Names are the one risky part.** A province's name is the id a ruler chooses as a target, and
the grounding guard reads the model's token probabilities on it (DR-016). Today's ids are
ordinary English-looking words (*fenmark*, *westmarch*). Odd generated names split into more
tokens and may sit closer to each other, which is exactly where target choice and grounding were
measured to be fragile (findings, 5 October, "Open choices and look-alike names"). So: generated
names are checked by the game (unique, distinct first letters within a neighbourhood, no real
country, a block list, a valid id), and **an A/B of generated names against today's** comes before
they are the default. Keeping ids stable and generating only the display name is the safe
version, at the cost of the model reading names the player does not see.

**How the text is asked for.** The framework's decision is action, target, statement, so it is
not a general JSON generator, and should not become one for this. Two ways that fit:

1. **Tables only.** Syllable tables for names, lists for the rest. Free, instant, deterministic;
   the names sound generated.
2. **A chronicler agent at world creation**, on the rulers' model: for each country, the game
   rolls the tables first, then asks for names and a motto with the rolled traits as facts, with
   one action (`name_country`) and the statement as `Name: … Capital: … Motto: …`. The game parses
   and checks it, and falls back to the tables. The same agent can write the two lines of
   history. It is section 1's storyteller in another moment, which is a reason to build it after
   the storyteller.

### Flags: drawn, not painted

Diffusion models garble flags' symmetry, edges and charges; a flag is flat, symbolic and small.
Code does it better and deterministically from the seed, at any size, with no GPU. The design
space is the heraldic one: a field divided (per pale, per bend, quarterly), an ordinary (a cross,
a chevron, a band), and a charge (a beast, a tower, a star), in the country's colours.

- [Armoria](https://github.com/Azgaar/Armoria), the emblem generator inside Azgaar's Fantasy Map
  Generator, does exactly this. **Its code is MIT and its simple charges CC0, but its detailed
  charges come from WappenWiki under CC BY-NC 3.0, non-commercial**: a commercial game would have
  to leave those out or redraw them. Its grammar is a good reference; a small C# generator of our
  own, with a dozen charges the team draws or commissions, avoids the question.
- The painted, sea-chart look the lead wants for stage 3 can be a texture and a shader over the
  drawn flag (paper grain, ink edges, a little wobble), so flags match the map without being
  painted one by one.

### Portraits and crests from the asset server

What [`tools/comfyui/`](../../tools/comfyui/README.md) documents:

- **The image model is Z-Image Turbo**, Comfy-Org's NVFP4 build, through the `image` workflow:
  a 1024 × 1024 PNG from a prompt, about 7 s cold and 2.5 s with the model loaded on the RTX 5080.
  Its text encoder is `qwen_3_4b`, a Qwen3 model, which is likely what "the qwen image model"
  refers to. Alibaba's separate, larger [Qwen-Image](https://github.com/QwenLM/Qwen-Image)
  (Apache-2.0, 20B, the best open model for lettering, with edit versions that keep a character
  consistent) is not installed there.
- **Licence:** Z-Image Turbo is Apache-2.0
  ([model card](https://huggingface.co/Tongyi-MAI/Z-Image-Turbo)), and the README says so.
- **Consistency is the weak point.** `AGENT_USAGE.md` records that asked for four views of one
  object on one sheet, Z-Image drew the front three times and skipped panels, none of six usable.
  A set of five portraits in one style is an easier ask (five pictures, one style, no matching
  geometry), and the usual tools are, cheapest first: a fixed style prefix with only the subject
  changing, fixed seeds for reproducibility, a style LoRA trained on 15 to 30 approved images, and
  an edit model working from one approved master
  ([ComfyUI's style transfer handbook](https://blog.comfy.org/p/the-complete-style-transfer-handbook)).
  Only the first two work on the server as it is.

**At game start, or before?** Before.

- The asset server is the team's machine, not the player's. A shipped game cannot call it.
- On a player's GPU the model would ship with the game (about 4.5 GB for the NVFP4 build, which
  runs natively only on RTX 50-series cards; larger builds otherwise), and it does not fit beside
  the 3 GB language model on an 8 GB card: the game would unload the ruler model, generate, and
  load it again. One user reports 15 to 20 s an image on an RTX 4060, which makes five portraits
  a loading screen of a minute or two (an estimate, unmeasured).
- **So: a pool.** The team generates, say, 40 portraits in one style on the asset server, looks at
  every one, keeps the good ones, tags them by traits (age, temperament, culture), and the game's
  generator picks five by the rolled traits. To the player it is "generated"; to the game it is
  content a person approved, as `Demos/README.md` asks of generated files.
- Event illustrations, if wanted, follow the same rule: a pool per template, not one per event.

### What would have to be measured

- Generated names against today's ids, in the table of models (placement, attack win rate,
  illegal answers, grounding containments), paired by seed.
- A generated persona, if ever, against the five, by the character test.
- For the pool: how many of N portraits a person keeps, and whether five drawn at random look like
  one set (the team, blind, picks the odd one out).

### Does any of it belong in the framework?

**No.** It is game content made before the rulers are asked. The framework already takes a
code-built `AgentIdentity` (name, role, personality, goals, knowledge) and code-registered
targets, which is everything a generator hands it; `CountryAgent` builds its agents this way. The
only framework-shaped lesson is a documentation one: a game that generates names per game changes
what the model reads per game, so its A/B runs must generate from the same seeds in both arms.

### Licences

- **Z-Image Turbo:** Apache-2.0. **Qwen3** text encoders: Apache-2.0.
- **Copyright in what it makes.** The US Copyright Office's
  [2023 guidance](https://www.copyright.gov/ai/ai_policy_guidance.pdf) and its
  [January 2025 report on copyrightability](https://www.copyright.gov/ai/Copyright-and-Artificial-Intelligence-Part-2-Copyrightability-Report.pdf)
  say prompts alone do not make a person the author, while human selection, arrangement and
  editing can be protected. Law-firm commentary reports that the Supreme Court declined the
  *Thaler* appeal in March 2026 ([Holland & Knight](https://www.hklaw.com/en/insights/publications/2026/03/the-final-word-supreme-court-refuses-to-hear-case-on-ai-authorship)).
  In practice: a raw generated portrait is probably not ours to protect, and others could reuse
  it; flags drawn by our code, hand-edited art and the curated set are on firmer ground. This is
  not legal advice.
- **Steam's AI disclosure** ([Steamworks content survey](https://partner.steamgames.com/doc/gettingstarted/contentsurvey)):
  a game discloses AI-generated content players consume. **Pre-generated** content (a portrait
  pool) is reviewed like any other; **live-generated** content (the storyteller's events, the
  rulers' statements, spoken lines, names made at game start) must also describe its guardrails
  against illegal output, and players can report it in the overlay. Development tools are not the
  focus since Valve narrowed the form in January 2026, as reported by
  [PC Gamer](https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/).
- **Every generated file a game commits** goes in its `GENERATED.md`
  ([`Demos/README.md`](../../Demos/README.md#licences-of-generated-assets)).
- **The language model.** The rulers, the storyteller and the chronicler run on `qwen3.5:4b`. Its
  licence should be read off the model card and recorded in the wiki before a release, as Phase 6b
  asks of a shipped model file.
