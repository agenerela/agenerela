# Scorecard: personas: qwen3.5:4b (default)



1 games, seeds 1 to 1, interleaved in blocks of 1 with x1, seats and personas rotating. Wall time 537 s.

## Liveliness

| Metric | table |
|---|---:|
| games | 1 |
| turns | 40.00 |
| ended by domination | 0% |
| ended last standing | 0% |
| ended at the turn limit | 0% |
| wars declared a game | 4.00 |
| peace offered a game | 0.00 |
| peace made a game | 17.00 |
| battles a turn | 3.13 |
| provinces changing hands a turn | 0.80 |
| provinces taken from a country a game | 0.00 |
| turns with no change | 60% |
| lead changes a game | 1.00 |
| eliminations a game | 0.00 |
| final map Gini | 0.10 |

## Sense

| Metric | qwen3.5:4b | builder | merchant | opportunist | proud | warmonger |
|---|---:|---:|---:|---:|---:|---:|
| council moves | 200 | 40 | 40 | 40 | 40 | 40 |
| idle share | 21% | 55% | 13% | 0% | 13% | 23% |
| refused at resolution | 1% | 0% | 0% | 0% | 3% | 0% |
| attacks landed | 0 | 0 | 0 | 0 | 0 | 0 |
| attack win rate | – | – | – | – | – | – |
| moves against a country | 5 | 0 | 1 | 1 | 2 | 1 |
| aims at the weaker | 60% | – | 0% | 100% | 50% | 100% |
| illegal answers | 0% | 0% | 0% | 0% | 0% | 0% |
| contained by a guard | 1% | 0% | 0% | 0% | 0% | 2% |
| pipeline errors | 0 | 0 | 0 | 0 | 0 | 0 |
| attacks suffered | 4 | 2 |  |  | 1 | 1 |
| answers an attack | 0% | 0% |  |  | 0% | 0% |
| peace offers received | 91 | 11 | 6 | 11 | 26 | 37 |
| accepts peace when losing | 15% | 50% | 100% | 44% | 0% | 0% |
| accepts peace when winning | 29% | 33% | 100% | 100% | 0% | 0% |

## Character

| Metric | builder | merchant | opportunist | proud | warmonger | table |
|---|---:|---:|---:|---:|---:|---:|
| recruit | 0% | 0% | 0% | 0% | 0% |  |
| fortify | 3% | 3% | 0% | 0% | 0% |  |
| raise_taxes | 13% | 10% | 8% | 0% | 13% |  |
| declare_war | 0% | 3% | 3% | 5% | 3% |  |
| attack | 0% | 0% | 0% | 0% | 0% |  |
| offer_peace | 0% | 0% | 0% | 0% | 0% |  |
| none | 55% | 13% | 0% | 13% | 23% |  |
| aggression | 0% | 3% | 3% | 5% | 3% |  |
| seat-games measured |  |  |  |  |  | 5 |
| aggression sd between games | – | – | – | – | – |  |
| eta2 aggression |  |  |  |  |  | 1.000 |
| p aggression |  |  |  |  |  | 1.000 |
| eta2 hold |  |  |  |  |  | 1.000 |
| p hold |  |  |  |  |  | 1.000 |
| eta2 peace |  |  |  |  |  | 0.000 |
| p peace |  |  |  |  |  | 1.000 |

## Voice

| Metric | all: qwen3.5:4b | builder | merchant | opportunist | proud | warmonger |
|---|---:|---:|---:|---:|---:|---:|
| answers | 1007 | 131 | 220 | 242 | 195 | 219 |
| statements given | 99% | 100% | 100% | 100% | 100% | 98% |
| words a statement | 13.64 | 13.47 | 14.10 | 13.31 | 14.11 | 13.24 |
| repeated word for word | 22% | 12% | 26% | 22% | 16% | 30% |
| statements with a rule | 1001 | 131 | 220 | 241 | 195 | 214 |
| copies its statement rule | 0% | 0% | 0% | 0% | 0% | 0% |
| copies a sentence of its rule | 0% | 0% | 0% | 0% | 0% | 0% |
| targeted statements | 544 | 33 | 148 | 169 | 93 | 101 |
| names its target | 73% | 100% | 84% | 49% | 77% | 84% |
| contradicts its move | 26% | 0% | 14% | 51% | 22% | 12% |
| claims land taken | 1% | 0% | 0% | 0% | 3% | 0% |
| invented grievance | 1% | 0% | 0% | 0% | 3% | 0% |

## Events

| Metric | table |
|---|---:|
| events a game | 47.00 |
| event value a game | 884.00 |
| gifts share | 28% |
| distinct template-target pairs a game | 17.00 |
| distinct templates a game | 4.00 |
| net value Gini | 0.59 |
| value tracks standing | -0.02 |
| hit rulers naming it | 9% |

## Strength

| Metric | qwen3.5:4b | builder | merchant | opportunist | proud | warmonger |
|---|---:|---:|---:|---:|---:|---:|
| seats | 5 | 1 | 1 | 1 | 1 | 1 |
| placement | 3.00 | 4.00 | 1.50 | 1.50 | 4.00 | 4.00 |
| wins | 1 | 0 | 0 | 1 | 0 | 0 |
| provinces at the end | 4.80 | 4.00 | 6.00 | 6.00 | 4.00 | 4.00 |
| knocked out | 0 | 0 | 0 | 0 | 0 | 0 |

## What each number is for

- **turns** (liveliness): How long a game lasts: a game that ends on turn 15 and one that drags to 30 feel different.
- **ended by domination** (liveliness): Share of games someone won outright, rather than the clock ending them: a game that goes somewhere.
- **wars declared a game** (liveliness): Wars are the story; a table that never declares one is a table of farmers.
- **peace offered / made a game** (liveliness): Diplomacy is the other half of the story: offers show intent, truces show agreement.
- **battles a turn** (liveliness): How much fighting a spectator sees each turn.
- **provinces changing hands a turn** (liveliness): Whether the map moves: battles that change nothing are noise.
- **provinces taken from a country a game** (liveliness): Land taken from a rival, not from the neutrals: the conquests that make enemies.
- **turns with no change** (liveliness): Share of turns where the map stood still: dead air.
- **lead changes a game** (liveliness): How often the leader (unique most provinces) changed: a race rather than a procession.
- **eliminations a game** (liveliness): Countries knocked out: stakes.
- **final map Gini** (liveliness): How uneven the final map is, 0 even to 0.8 one country holding all: an ending with a clear shape.
- **idle share** (sense): Council moves that were none, as the game received them: a player that holds is not playing.
- **illegal answers** (sense): Model answers, before any guard, that the game did not offer (models only).
- **contained by a guard** (sense): Model decisions a guard turned into none: the framework, not the player, chose to hold (models only).
- **refused at resolution** (sense): Council moves the game refused when they came to land, made stale earlier in the turn.
- **attack win rate** (sense): Of attacks that reached a battle, those won: picking fights it can win.
- **aims at the weaker** (sense): Of attacks on a country's province and declarations of war, those against a country with a smaller army than ours.
- **answers an attack** (sense): Of attacks or war declarations against it, those answered within two turns by striking back, fortifying or offering the aggressor peace.
- **accepts peace when losing** (sense): Of peace offers from a country with a larger army, those accepted.
- **accepts peace when winning** (sense): Of peace offers from a country with a smaller or equal army, those accepted; a sensible player accepts less often here than when losing.
- **aggression** (character): Attacks and declarations of war, as a share of council moves: the warmonger's number.
- **move mix** (character): Share of each council move: what the player spends its turns on.
- **eta2 aggression / hold / peace** (character): Share of the seat-to-seat variance in each move share that the character explains (0 to 1), with a permutation p: does the persona change the play beyond game-to-game noise?
- **statements given** (voice): Share of model answers with a statement: a ruler who speaks.
- **words a statement** (voice): Length: a proclamation, not an essay.
- **repeated word for word** (voice): Statements identical to one the same country said earlier that game: the illusion breaks on the second time.
- **copies its statement rule** (voice): Of statements from a seat with statement rules, those that hold the whole rule word for word: the instruction read out as the proclamation (gap N9).
- **copies a sentence of its rule** (voice): Of the same statements, those that hold any one sentence of the rule of four words or more word for word, the whole rule included.
- **names its target** (voice): Of targeted moves, statements that name the target: the words fit the deed.
- **contradicts its move** (voice): Of targeted moves whose statement names a country or province, those that do not name the target: the words name someone else.
- **claims land taken** (voice): Statements saying someone took, seized or stole our land (ClaimsLandTaken): a grievance, true or not.
- **invented grievance** (voice): Statements claiming our land was taken by someone who took none of it earlier in the game: the countries named, else the move's counterpart, else anyone (gap N7).
- **events a game** (events): Events that happened, the random table's or the storyteller's: the two arms should be of comparable volume.
- **event value a game** (events): What the events moved, in gold (soldiers at 2, stability at 20, a fort at 30), summed without sign: their weight in the game.
- **gifts share** (events): Events worth something to those hit, rather than costing them.
- **distinct template-target pairs a game** (events): How many different things happened to different targets in one game: variety within a game.
- **distinct templates a game** (events): How many of the kinds of event a game saw.
- **distinct titles** (events): Of the storyteller's titles in a game, those not repeated in that game.
- **story phrases shared** (events): Of a story's two-word phrases, those an earlier story of the same game already had (pairs of two common words left out): the stories repeating themselves.
- **titles shared across seeds** (events): Of the storyteller's titles, those that also appear in another game of the table: the stories repeating between games.
- **stories replaced** (events): Of the storyteller's events, those whose story the game replaced: it named a country the event did not reach, held a number or ran long.
- **titles missing** (events): Of the storyteller's events, those whose statement had no title before a colon, whatever else the story check did.
- **net value Gini** (events): How unevenly events' net worth fell on the five countries in a game, 0 even; Gini's mean difference over the mean absolute value, so blows count as well as gifts.
- **value tracks standing** (events): Correlation, within a game, between what an event was worth to a country and its provinces then against the mean: below 0 helps the weak, above 0 crowns the strong.
- **hit rulers naming it** (events): Of model rulers hit by an event who made a statement on that turn's council, those whose statement names it (a word of its title or of its kind): the event reached the story.
- **storyteller calls** (events): Times the storyteller was asked.
- **storyteller chose none** (events): Of its calls, those that made nothing happen, after the guards.
- **storyteller illegal answers** (events): Of its answers, before any guard, those that were no event it was offered.
- **storyteller contained** (events): Of its calls, those a guard turned into none.
- **storyteller pipeline errors** (events): Calls with no decision at all.
- **events refused at upkeep** (events): Events the storyteller chose that the game's re-check refused when they came to land.
- **storyteller median latency** (events): Seconds a storyteller call took, median.
- **choice events share** (events): Of the storyteller's events, the dilemmas: a ruler chose (S2).
- **choices a game** (events): Rulers' choices a game, one per country a dilemma reached.
- **choices left to the default** (events): Of those choices, the ones nobody answered, or answered with none: the game took the default.
- **events on two or more** (events): Of the storyteller's events, those that reached two countries or more.
- **story-only events share** (events): Of the storyteller's events, those with no effect: an omen, a tale's end.
- **trait events share** (events): Of the storyteller's events, those that need a province's trait: a rich vein, a flood, pilgrims.
- **chains started a game** (events): Chains that reached two events.
- **chains completed a game** (events): Chains told to their end, closed with end_tale, or run to their full length.
- **chains abandoned a game** (events): Chains nobody continued for three turns, or whose subject fell.
- **chain length** (events): Events in a started chain, mean.
- **long chains a game** (events): Chains of four events or more.
- **stories naming a real happening** (events): Of the storyteller's stories, those whose claim of a fight matched the game, or that named someone from the recent happenings (with +history).
- **stories inventing history** (events): Of the storyteller's stories, those claiming a capture or battle the game never had (before the story was replaced).
- **event seconds a turn** (events): Seconds spent a turn on events: the storyteller's call, its title call, and the rulers' choices.
- **placement** (strength): Mean rank by provinces at the end, 1 best, ties shared: strength between seats at the same table.
- **wins** (strength): Games won by this seat kind.
- **provinces at the end** (strength): Mean provinces held at the end.
- **knocked out** (strength): Seats out of the game before the end.
