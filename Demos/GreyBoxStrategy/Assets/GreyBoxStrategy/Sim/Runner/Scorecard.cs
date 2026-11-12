using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GreyBoxStrategy
{
    /// <summary>
    /// What one model seat said and asked for on one decision, for the scorecard's sense and voice
    /// sections. Scripts and random players have none: those sections then say so.
    /// </summary>
    public sealed class SeatDecisionInfo
    {
        public int Seed;
        public int Turn;
        public string Country;
        public Moment Moment;
        /// <summary>Whether the model answered at all (no pipeline error).</summary>
        public bool Answered;
        /// <summary>The model's own answer, before any guard.</summary>
        public string AskedAction;
        public string AskedTarget;
        /// <summary>Whether that answer was a move the game offered.</summary>
        public bool AskedLegal;
        /// <summary>Whether a guard turned it into none.</summary>
        public bool Contained;
        public string Statement;
        /// <summary>For an envoy's answer, the country that offered peace; null otherwise.</summary>
        public string OfferedBy;
        /// <summary>The statement rules the seat's persona gave it, or null: what "copies its statement rule" compares with.</summary>
        public string StatementRules;
    }

    /// <summary>One call to a game's storyteller, for the scorecard's events section.</summary>
    public sealed class StorytellerCallInfo
    {
        public int Turn;
        /// <summary>Whether the model answered at all.</summary>
        public bool Answered;
        public string AskedAction;
        /// <summary>Whether its own answer, before any guard, was an event the game offered, or none.</summary>
        public bool AskedLegal;
        public bool Contained;
        public bool Error;
        /// <summary>Whether the decision, after the guards, was no event.</summary>
        public bool None;
        /// <summary>Whether an event was queued for the upkeep.</summary>
        public bool Queued;
        public float LatencySeconds;
    }

    /// <summary>One played game, with who sat where: a label per country (the player: a model or a script kind) and a character (a persona, or a placebo slot).</summary>
    public sealed class ScoredGame
    {
        public ScoredGame(GameRecord record, IReadOnlyDictionary<string, string> labels,
            IReadOnlyDictionary<string, string> characters, IReadOnlyList<SeatDecisionInfo> decisions = null,
            IReadOnlyList<StorytellerCallInfo> storyteller = null)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            Labels = labels ?? record.Kinds;
            Characters = characters ?? Labels;
            Decisions = decisions ?? Array.Empty<SeatDecisionInfo>();
            Storyteller = storyteller ?? Array.Empty<StorytellerCallInfo>();
        }

        /// <summary>The storyteller's calls, or none when the game had no storyteller.</summary>
        public IReadOnlyList<StorytellerCallInfo> Storyteller { get; }

        public GameRecord Record { get; }
        public IReadOnlyDictionary<string, string> Labels { get; }
        public IReadOnlyDictionary<string, string> Characters { get; }
        public IReadOnlyList<SeatDecisionInfo> Decisions { get; }
    }

    /// <summary>One number of a scorecard.</summary>
    public sealed class ScoreRow
    {
        public string Section;
        public string Group;
        public string Metric;
        public double Value;
    }

    /// <summary>
    /// How a table of players feels to watch and to play against, from its games alone, with no
    /// reference to who won against whom outside the table. Five sections: liveliness of the games,
    /// sense of each player's moves, character (does a persona change the play), voice (the
    /// statements), and strength (placement by seat). Every number's one-line reason is in
    /// <see cref="Definitions"/>, and the demo README prints them.
    /// </summary>
    /// <remarks>
    /// Plain C#, no Unity, so a scripted table and a model table are scored by the same code.
    /// Moves are judged on the state they were chosen on (<see cref="Game.Snapshots"/>), never on
    /// the world after the turn.
    /// </remarks>
    public sealed class Scorecard
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Seat-games with fewer council moves than this are left out of the character test: a country out on turn 3 has no character to measure.</summary>
        public const int MinMovesForCharacter = 5;

        /// <summary>Shuffles in the character test's permutation test.</summary>
        public const int Permutations = 2000;

        /// <summary>The scorecard's numbers and why each is there, in print order.</summary>
        public static readonly IReadOnlyList<(string section, string metric, string why)> Definitions = new[]
        {
            ("liveliness", "turns", "How long a game lasts: a game that ends on turn 15 and one that drags to 30 feel different."),
            ("liveliness", "ended by domination", "Share of games someone won outright, rather than the clock ending them: a game that goes somewhere."),
            ("liveliness", "wars declared a game", "Wars are the story; a table that never declares one is a table of farmers."),
            ("liveliness", "peace offered / made a game", "Diplomacy is the other half of the story: offers show intent, truces show agreement."),
            ("liveliness", "battles a turn", "How much fighting a spectator sees each turn."),
            ("liveliness", "provinces changing hands a turn", "Whether the map moves: battles that change nothing are noise."),
            ("liveliness", "provinces taken from a country a game", "Land taken from a rival, not from the neutrals: the conquests that make enemies."),
            ("liveliness", "turns with no change", "Share of turns where the map stood still: dead air."),
            ("liveliness", "lead changes a game", "How often the leader (unique most provinces) changed: a race rather than a procession."),
            ("liveliness", "eliminations a game", "Countries knocked out: stakes."),
            ("liveliness", "final map Gini", "How uneven the final map is, 0 even to 0.8 one country holding all: an ending with a clear shape."),
            ("sense", "idle share", "Council moves that were none, as the game received them: a player that holds is not playing."),
            ("sense", "illegal answers", "Model answers, before any guard, that the game did not offer (models only)."),
            ("sense", "contained by a guard", "Model decisions a guard turned into none: the framework, not the player, chose to hold (models only)."),
            ("sense", "refused at resolution", "Council moves the game refused when they came to land, made stale earlier in the turn."),
            ("sense", "attack win rate", "Of attacks that reached a battle, those won: picking fights it can win."),
            ("sense", "aims at the weaker", "Of attacks on a country's province and declarations of war, those against a country with a smaller army than ours."),
            ("sense", "answers an attack", "Of attacks or war declarations against it, those answered within two turns by striking back, fortifying or offering the aggressor peace."),
            ("sense", "accepts peace when losing", "Of peace offers from a country with a larger army, those accepted."),
            ("sense", "accepts peace when winning", "Of peace offers from a country with a smaller or equal army, those accepted; a sensible player accepts less often here than when losing."),
            ("character", "aggression", "Attacks and declarations of war, as a share of council moves: the warmonger's number."),
            ("character", "move mix", "Share of each council move: what the player spends its turns on."),
            ("character", "eta2 aggression / hold / peace", "Share of the seat-to-seat variance in each move share that the character explains (0 to 1), with a permutation p: does the persona change the play beyond game-to-game noise?"),
            ("voice", "statements given", "Share of model answers with a statement: a ruler who speaks."),
            ("voice", "words a statement", "Length: a proclamation, not an essay."),
            ("voice", "repeated word for word", "Statements identical to one the same country said earlier that game: the illusion breaks on the second time."),
            ("voice", "copies its statement rule", "Of statements from a seat with statement rules, those that hold the whole rule word for word: the instruction read out as the proclamation (gap N9)."),
            ("voice", "copies a sentence of its rule", "Of the same statements, those that hold any one sentence of the rule of four words or more word for word, the whole rule included."),
            ("voice", "names its target", "Of targeted moves, statements that name the target: the words fit the deed."),
            ("voice", "contradicts its move", "Of targeted moves whose statement names a country or province, those that do not name the target: the words name someone else."),
            ("voice", "claims land taken", "Statements saying someone took, seized or stole our land (ClaimsLandTaken): a grievance, true or not."),
            ("voice", "invented grievance", "Statements claiming our land was taken by someone who took none of it earlier in the game: the countries named, else the move's counterpart, else anyone (gap N7)."),
            ("events", "events a game", "Events that happened, the random table's or the storyteller's: the two arms should be of comparable volume."),
            ("events", "event value a game", "What the events moved, in gold (soldiers at 2, stability at 20, a fort at 30), summed without sign: their weight in the game."),
            ("events", "gifts share", "Events worth something to those hit, rather than costing them."),
            ("events", "distinct template-target pairs a game", "How many different things happened to different targets in one game: variety within a game."),
            ("events", "distinct templates a game", "How many of the kinds of event a game saw."),
            ("events", "distinct titles", "Of the storyteller's titles in a game, those not repeated in that game."),
            ("events", "story phrases shared", "Of a story's two-word phrases, those an earlier story of the same game already had (pairs of two common words left out): the stories repeating themselves."),
            ("events", "titles shared across seeds", "Of the storyteller's titles, those that also appear in another game of the table: the stories repeating between games."),
            ("events", "stories replaced", "Of the storyteller's events, those whose story the game replaced: it named a country the event did not reach, held a number or ran long."),
            ("events", "titles missing", "Of the storyteller's events, those whose statement had no title before a colon, whatever else the story check did."),
            ("events", "net value Gini", "How unevenly events' net worth fell on the five countries in a game, 0 even; Gini's mean difference over the mean absolute value, so blows count as well as gifts."),
            ("events", "value tracks standing", "Correlation, within a game, between what an event was worth to a country and its provinces then against the mean: below 0 helps the weak, above 0 crowns the strong."),
            ("events", "hit rulers naming it", "Of model rulers hit by an event who made a statement on that turn's council, those whose statement names it (a word of its title or of its kind): the event reached the story."),
            ("events", "storyteller calls", "Times the storyteller was asked."),
            ("events", "storyteller chose none", "Of its calls, those that made nothing happen, after the guards."),
            ("events", "storyteller illegal answers", "Of its answers, before any guard, those that were no event it was offered."),
            ("events", "storyteller contained", "Of its calls, those a guard turned into none."),
            ("events", "storyteller pipeline errors", "Calls with no decision at all."),
            ("events", "events refused at upkeep", "Events the storyteller chose that the game's re-check refused when they came to land."),
            ("events", "storyteller median latency", "Seconds a storyteller call took, median."),
            ("strength", "placement", "Mean rank by provinces at the end, 1 best, ties shared: strength between seats at the same table."),
            ("strength", "wins", "Games won by this seat kind."),
            ("strength", "provinces at the end", "Mean provinces held at the end."),
            ("strength", "knocked out", "Seats out of the game before the end."),
        };

        /// <param name="characterTest">Whether to run the character section's permutation test; off for a one-game card, where it means nothing.</param>
        public Scorecard(string table, IReadOnlyList<ScoredGame> games, string note = null, bool characterTest = true)
        {
            Table = table;
            Games = games ?? throw new ArgumentNullException(nameof(games));
            Note = note;
            this.characterTest = characterTest;
            Compute();
        }

        private readonly bool characterTest;

        /// <summary>
        /// Every number again, game by game (<c>table,seed,section,group,metric,value</c>), so two
        /// tables on the same seeds can be compared seed by seed against how much one table varies
        /// between its own games.
        /// </summary>
        public string PerGameCsv()
        {
            var sb = new StringBuilder("table,seed,section,group,metric,value\n");
            foreach (var g in Games)
                foreach (var r in new Scorecard(Table, new[] { g }, characterTest: false).Rows)
                    sb.AppendLine(string.Join(",", Q(Table), g.Record.Seed, r.Section, Q(r.Group), Q(r.Metric),
                        double.IsNaN(r.Value) ? "" : r.Value.ToString("R", Inv)));
            return sb.ToString();
        }

        public string Table { get; }
        public string Note { get; }
        public IReadOnlyList<ScoredGame> Games { get; }
        public List<ScoreRow> Rows { get; } = new List<ScoreRow>();

        /// <summary>The value of one row, or NaN.</summary>
        public double Get(string section, string group, string metric) =>
            Rows.FirstOrDefault(r => r.Section == section && r.Group == group && r.Metric == metric)?.Value ?? double.NaN;

        // --- The council moves, as the game received them ------------------------------------

        /// <summary>One council move of one country, with the snapshot it was chosen on.</summary>
        private sealed class CouncilMove
        {
            public ScoredGame Game;
            public int Turn;
            public string Country;
            public Move Choice;
            public MoveOutcome Outcome;
            public TurnSnapshot Snapshot;
        }

        private static IEnumerable<CouncilMove> CouncilMoves(ScoredGame g)
        {
            var snaps = g.Record.Game.Snapshots.ToDictionary(s => s.Turn);
            foreach (var report in g.Record.Reports)
            {
                var seen = new HashSet<CountryRequest>();
                foreach (var o in report.Outcomes)
                {
                    if (o.Request == null || o.Request.Moment != Moment.Council || !seen.Add(o.Request)) continue;
                    yield return new CouncilMove
                    {
                        Game = g, Turn = report.Turn, Country = o.Request.CountryId, Choice = o.Choice, Outcome = o,
                        Snapshot = snaps.TryGetValue(report.Turn, out var s) ? s : null,
                    };
                }
            }
        }

        private void Add(string section, string group, string metric, double value) =>
            Rows.Add(new ScoreRow { Section = section, Group = group, Metric = metric, Value = value });

        private void Compute()
        {
            int n = Games.Count;
            if (n == 0) return;
            var moves = Games.SelectMany(CouncilMoves).ToList();
            Liveliness(n, moves);
            // Sense and strength by player, then by character where that is something else (a
            // persona, or a placebo slot), so a persona's sense can be read beside its model's.
            var keys = new List<Func<ScoredGame, string, string>> { (g, c) => g.Labels[c] };
            if (Games.Any(g => g.Labels.Any(kv => g.Characters[kv.Key] != kv.Value))) keys.Add((g, c) => g.Characters[c]);
            foreach (var key in keys)
            {
                Sense(moves, key);
                Strength(key);
            }
            Character(moves);
            Voice();
            EventsSection();
        }

        // --- Liveliness ----------------------------------------------------------------------

        private void Liveliness(int n, List<CouncilMove> moves)
        {
            const string S = "liveliness", G = "table";
            // Turns played, which is the result's turns for a finished game; a game still running
            // (a test's few turns) counts what it has played and has no ending.
            double turns = Games.Sum(g => (double)g.Record.Reports.Count);
            Add(S, G, "games", n);
            Add(S, G, "turns", turns / n);
            Add(S, G, "ended by domination", Games.Count(g => g.Record.Result?.Ending == Ending.Domination) / (double)n);
            Add(S, G, "ended last standing", Games.Count(g => g.Record.Result?.Ending == Ending.LastStanding) / (double)n);
            Add(S, G, "ended at the turn limit", Games.Count(g => g.Record.Result?.Ending == Ending.TurnLimit) / (double)n);
            var landed = moves.Where(m => !m.Outcome.Refused).ToList();
            Add(S, G, "wars declared a game", landed.Count(m => m.Choice.Action == Actions.DeclareWar) / (double)n);
            Add(S, G, "peace offered a game", Games.Sum(g => g.Record.Reports.Sum(r => r.PeaceOffers)) / (double)n);
            Add(S, G, "peace made a game", Games.Sum(g => g.Record.Reports.Sum(r => r.PeaceAccepted)) / (double)n);
            Add(S, G, "battles a turn", Games.Sum(g => g.Record.Reports.Sum(r => r.Battles)) / turns);
            Add(S, G, "provinces changing hands a turn", Games.Sum(g => g.Record.Reports.Sum(r => r.TerritoryChanges)) / turns);
            Add(S, G, "provinces taken from a country a game", landed.Count(m => m.Choice.Action == Actions.Attack && TookFromCountry(m.Outcome)) / (double)n);
            Add(S, G, "turns with no change", Games.Sum(g => g.Record.Reports.Count(r => r.TerritoryChanges == 0)) / turns);
            Add(S, G, "lead changes a game", Games.Sum(g => LeadChanges(g.Record)) / (double)n);
            Add(S, G, "eliminations a game", Games.Sum(g => g.Record.Game.World.CountryList.Count(c => !c.Alive)) / (double)n);
            Add(S, G, "final map Gini", Games.Average(g => Gini(FinalProvinces(g.Record).Values.Select(v => (double)v).ToList())));
        }

        private static IReadOnlyDictionary<string, int> FinalProvinces(GameRecord r) =>
            r.Result?.Provinces ?? (IReadOnlyDictionary<string, int>)(r.Reports.Count > 0 ? r.Reports[r.Reports.Count - 1].ProvinceCounts : new Dictionary<string, int>());

        private static bool Took(MoveOutcome o) => o.Text != null && o.Text.StartsWith("took ", StringComparison.Ordinal);
        private static bool TookFromCountry(MoveOutcome o) => Took(o) && !o.Text.Contains("from the neutrals");

        /// <summary>Changes of the unique leader in provinces, turn by turn; a tie keeps the last leader.</summary>
        public static int LeadChanges(GameRecord record)
        {
            string leader = null;
            int changes = 0;
            foreach (var r in record.Reports)
            {
                int max = r.ProvinceCounts.Values.DefaultIfEmpty(0).Max();
                var top = r.ProvinceCounts.Where(kv => kv.Value == max).Select(kv => kv.Key).ToList();
                if (top.Count != 1) continue;
                if (leader != null && top[0] != leader) changes++;
                leader = top[0];
            }
            return changes;
        }

        /// <summary>The Gini coefficient of non-negative values: 0 all equal, (n−1)/n one holds all.</summary>
        public static double Gini(IReadOnlyList<double> xs)
        {
            double sum = xs.Sum();
            if (xs.Count == 0 || sum <= 0) return 0;
            double diff = 0;
            foreach (double a in xs)
                foreach (double b in xs)
                    diff += Math.Abs(a - b);
            return diff / (2.0 * xs.Count * sum);
        }

        // --- Sense ---------------------------------------------------------------------------

        private void Sense(List<CouncilMove> moves, Func<ScoredGame, string, string> key)
        {
            const string S = "sense";
            foreach (var group in moves.GroupBy(m => key(m.Game, m.Country)).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                string label = group.Key;
                var mine = group.ToList();
                Add(S, label, "council moves", mine.Count);
                Add(S, label, "idle share", Share(mine.Count(m => m.Choice.Action == Actions.None), mine.Count));
                var nonHold = mine.Where(m => m.Choice.Action != Actions.None).ToList();
                Add(S, label, "refused at resolution", Share(nonHold.Count(m => m.Outcome.Refused), nonHold.Count));
                var battles = mine.Where(m => m.Choice.Action == Actions.Attack && !m.Outcome.Refused).ToList();
                Add(S, label, "attacks landed", battles.Count);
                Add(S, label, "attack win rate", Share(battles.Count(m => Took(m.Outcome)), battles.Count));

                // Against a country: an attack on a province a country held, or a declaration of war.
                var aimed = mine.Where(m => m.Snapshot != null && (m.Choice.Action == Actions.DeclareWar
                    || (m.Choice.Action == Actions.Attack && m.Snapshot.OwnerOf(m.Choice.Target) != null))).ToList();
                Add(S, label, "moves against a country", aimed.Count);
                Add(S, label, "aims at the weaker", Share(aimed.Count(m =>
                {
                    string enemy = m.Choice.Action == Actions.DeclareWar ? m.Choice.Target : m.Snapshot.OwnerOf(m.Choice.Target);
                    return m.Snapshot.ArmyOf(enemy) < m.Snapshot.ArmyOf(m.Country);
                }), aimed.Count));
            }

            // Model-only: answers before the guards.
            foreach (var group in Games.SelectMany(g => g.Decisions.Select(d => (g, d))).GroupBy(x => key(x.g, x.d.Country)))
            {
                var council = group.Where(x => x.d.Moment == Moment.Council && x.d.Answered).ToList();
                Add(S, group.Key, "illegal answers", Share(council.Count(x => !x.d.AskedLegal), council.Count));
                Add(S, group.Key, "contained by a guard", Share(group.Count(x => x.d.Contained), group.Count()));
                Add(S, group.Key, "pipeline errors", group.Count(x => !x.d.Answered));
            }

            Retaliation(moves, key);
            PeaceSense(key);
        }

        private void Retaliation(List<CouncilMove> moves, Func<ScoredGame, string, string> key)
        {
            var byGameTurnCountry = moves.ToDictionary(m => (m.Game, m.Turn, m.Country));
            var answered = new Dictionary<string, (int events, int answered)>(StringComparer.Ordinal);
            foreach (var g in Games)
            {
                var events = new HashSet<(string victim, string aggressor, int turn)>();
                foreach (var m in moves.Where(m => m.Game == g && !m.Outcome.Refused && m.Snapshot != null))
                {
                    if (m.Choice.Action == Actions.DeclareWar) events.Add((m.Choice.Target, m.Country, m.Turn));
                    else if (m.Choice.Action == Actions.Attack && m.Snapshot.OwnerOf(m.Choice.Target) is string owner && owner != m.Country)
                        events.Add((owner, m.Country, m.Turn));
                }
                foreach (var e in events)
                {
                    var window = new[] { e.turn + 1, e.turn + 2 }
                        .Select(t => byGameTurnCountry.TryGetValue((g, t, e.victim), out var mv) ? mv : null)
                        .Where(mv => mv != null).ToList();
                    if (window.Count == 0) continue; // out of the game, or the game ended: no chance to answer
                    bool did = window.Any(mv =>
                        mv.Choice.Action == Actions.Fortify
                        || (mv.Choice.Action == Actions.OfferPeace && mv.Choice.Target == e.aggressor)
                        || (mv.Choice.Action == Actions.Attack && mv.Snapshot?.OwnerOf(mv.Choice.Target) == e.aggressor));
                    string label = key(g, e.victim);
                    answered.TryGetValue(label, out var c);
                    answered[label] = (c.events + 1, c.answered + (did ? 1 : 0));
                }
            }
            foreach (var kv in answered)
            {
                Add("sense", kv.Key, "attacks suffered", kv.Value.events);
                Add("sense", kv.Key, "answers an attack", Share(kv.Value.answered, kv.Value.events));
            }
        }

        private void PeaceSense(Func<ScoredGame, string, string> key)
        {
            var tally = new Dictionary<string, int[]>(StringComparer.Ordinal); // losing offers, accepted; winning offers, accepted
            foreach (var g in Games)
            {
                var snaps = g.Record.Game.Snapshots.ToDictionary(s => s.Turn);
                foreach (var report in g.Record.Reports)
                foreach (var o in report.Outcomes.Where(o => o.Request?.Moment == Moment.Envoy && o.Text != "the offer no longer stood"))
                {
                    if (!snaps.TryGetValue(report.Turn, out var s)) continue;
                    string me = o.Request.CountryId;
                    bool losing = s.ArmyOf(o.Request.OfferedBy) > s.ArmyOf(me);
                    bool accepted = o.Applied.Action == Actions.AcceptPeace;
                    string label = key(g, me);
                    if (!tally.TryGetValue(label, out var t)) tally[label] = t = new int[4];
                    t[losing ? 0 : 2]++;
                    if (accepted) t[losing ? 1 : 3]++;
                }
            }
            foreach (var kv in tally)
            {
                Add("sense", kv.Key, "peace offers received", kv.Value[0] + kv.Value[2]);
                Add("sense", kv.Key, "accepts peace when losing", Share(kv.Value[1], kv.Value[0]));
                Add("sense", kv.Key, "accepts peace when winning", Share(kv.Value[3], kv.Value[2]));
            }
        }

        // --- Character -----------------------------------------------------------------------

        private sealed class SeatGame
        {
            public ScoredGame Game;
            public string Character;
            public double Aggression;
            public double Hold;
            public double Peace;
        }

        private void Character(List<CouncilMove> moves)
        {
            const string S = "character";
            foreach (var group in moves.GroupBy(m => m.Game.Characters[m.Country]).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var mine = group.ToList();
                foreach (string a in Actions.Council)
                    Add(S, group.Key, a, Share(mine.Count(m => m.Choice.Action == a), mine.Count));
                Add(S, group.Key, "aggression", Share(mine.Count(m => IsAggressive(m.Choice)), mine.Count));
            }

            var seatGames = moves.GroupBy(m => (m.Game, m.Country))
                .Where(g => g.Count() >= MinMovesForCharacter)
                .Select(g => new SeatGame
                {
                    Game = g.Key.Game,
                    Character = g.Key.Game.Characters[g.Key.Country],
                    Aggression = g.Count(m => IsAggressive(m.Choice)) / (double)g.Count(),
                    Hold = g.Count(m => m.Choice.Action == Actions.None) / (double)g.Count(),
                    Peace = g.Count(m => m.Choice.Action == Actions.OfferPeace) / (double)g.Count(),
                }).ToList();
            Add(S, "table", "seat-games measured", seatGames.Count);
            foreach (var group in seatGames.GroupBy(s => s.Character))
                Add(S, group.Key, "aggression sd between games", Sd(group.Select(s => s.Aggression).ToList()));
            if (!characterTest) return;
            Test(seatGames, s => s.Aggression, "aggression");
            Test(seatGames, s => s.Hold, "hold");
            Test(seatGames, s => s.Peace, "peace");
        }

        private static bool IsAggressive(Move m) => m.Action == Actions.Attack || m.Action == Actions.DeclareWar;

        /// <summary>
        /// Eta squared of a per-seat-game share by character, and a permutation p: characters
        /// shuffled among the seats of each game, so every shuffle keeps a game's own luck.
        /// </summary>
        private void Test(List<SeatGame> seatGames, Func<SeatGame, double> value, string name)
        {
            if (seatGames.Count < 4 || seatGames.Select(s => s.Character).Distinct().Count() < 2) return;
            var values = seatGames.Select(value).ToList();
            var labels = seatGames.Select(s => s.Character).ToList();
            double observed = Eta2(values, labels);
            var rng = new System.Random(20);
            var byGame = Enumerable.Range(0, seatGames.Count).GroupBy(i => seatGames[i].Game).Select(g => g.ToList()).ToList();
            int atLeast = 0;
            var shuffled = new List<string>(labels);
            for (int k = 0; k < Permutations; k++)
            {
                foreach (var idx in byGame)
                {
                    var pool = idx.Select(i => labels[i]).ToList();
                    for (int i = pool.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        (pool[i], pool[j]) = (pool[j], pool[i]);
                    }
                    for (int i = 0; i < idx.Count; i++) shuffled[idx[i]] = pool[i];
                }
                if (Eta2(values, shuffled) >= observed - 1e-12) atLeast++;
            }
            Add("character", "table", $"eta2 {name}", observed);
            Add("character", "table", $"p {name}", (atLeast + 1) / (double)(Permutations + 1));
        }

        /// <summary>Between-group sum of squares over the total: the share of variance the labels explain.</summary>
        public static double Eta2(IReadOnlyList<double> values, IReadOnlyList<string> labels)
        {
            double mean = values.Average();
            double total = values.Sum(v => (v - mean) * (v - mean));
            if (total <= 0) return 0;
            double between = 0;
            foreach (var g in Enumerable.Range(0, values.Count).GroupBy(i => labels[i]))
            {
                double m = g.Average(i => values[i]);
                between += g.Count() * (m - mean) * (m - mean);
            }
            return between / total;
        }

        // --- Voice ---------------------------------------------------------------------------

        private void Voice()
        {
            const string S = "voice";
            var all = Games.SelectMany(g => g.Decisions.Where(d => d.Answered).Select(d => (g, d))).ToList();
            if (all.Count == 0) return;
            foreach (var group in all.GroupBy(x => x.g.Characters[x.d.Country]).Concat(all.GroupBy(x => "all: " + x.g.Labels[x.d.Country]))
                         .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var said = group.Where(x => !string.IsNullOrWhiteSpace(x.d.Statement)).ToList();
                Add(S, group.Key, "answers", group.Count());
                Add(S, group.Key, "statements given", Share(said.Count, group.Count()));
                Add(S, group.Key, "words a statement", said.Count == 0 ? 0 : said.Average(x => Words(x.d.Statement)));
                int repeated = 0;
                foreach (var seat in said.GroupBy(x => (x.g, x.d.Country)))
                {
                    var before = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var x in seat.OrderBy(x => x.d.Turn).ThenBy(x => x.d.Moment))
                        if (!before.Add(Normal(x.d.Statement))) repeated++;
                }
                Add(S, group.Key, "repeated word for word", Share(repeated, said.Count));

                var ruled = said.Where(x => !string.IsNullOrWhiteSpace(x.d.StatementRules)).ToList();
                if (ruled.Count > 0)
                {
                    Add(S, group.Key, "statements with a rule", ruled.Count);
                    Add(S, group.Key, "copies its statement rule", Share(ruled.Count(x => CopiesRule(x.d.Statement, x.d.StatementRules)), ruled.Count));
                    Add(S, group.Key, "copies a sentence of its rule", Share(ruled.Count(x => CopiesRuleSentence(x.d.Statement, x.d.StatementRules)), ruled.Count));
                }

                var targeted = said.Where(x => Takes(x.d.AskedAction) && !string.IsNullOrEmpty(x.d.AskedTarget) && x.d.AskedTarget != "no_target").ToList();
                int named = 0, naming = 0, contradicts = 0;
                foreach (var x in targeted)
                {
                    var ids = Named(x.d.Statement, x.g.Record.Game.World);
                    if (ids.Contains(x.d.AskedTarget)) named++;
                    if (ids.Count == 0) continue;
                    naming++;
                    if (!ids.Contains(x.d.AskedTarget)) contradicts++;
                }
                Add(S, group.Key, "targeted statements", targeted.Count);
                Add(S, group.Key, "names its target", Share(named, targeted.Count));
                Add(S, group.Key, "contradicts its move", Share(contradicts, naming));

                int claims = 0, invented = 0;
                foreach (var x in said)
                {
                    if (!ClaimsLandTaken(x.d.Statement)) continue;
                    claims++;
                    if (!GrievanceHappened(x.g, x.d)) invented++;
                }
                Add(S, group.Key, "claims land taken", Share(claims, said.Count));
                Add(S, group.Key, "invented grievance", Share(invented, said.Count));
            }
        }

        // A taking verb in the past, or as an accusation ("for taking", "is taking"), in the same
        // sentence as our land or "from us". Not "we will take": a threat or a plan is no grievance.
        private static readonly Regex Taking = new Regex(
            @"\b(took|taken|seized|stole|stolen|grabbed|captured|conquered|robbed|for (taking|seizing|stealing|grabbing)|(?<!\bwe )(is|are|was|were|keeps?|kept) (taking|seizing|stealing|grabbing))\b",
            RegexOptions.IgnoreCase);
        private static readonly Regex OurLand = new Regex(
            @"\b(our|my) (\w+ )?(land|lands|province|provinces|territory|territories|soil|border|borders|home|homes|homeland|fields|village|villages|town|towns)\b|\bfrom us\b",
            RegexOptions.IgnoreCase);

        /// <summary>
        /// Whether a statement says someone took our land: one sentence with a taking verb in the past
        /// or as an accusation (<c>took</c>, <c>stole</c>, <c>for taking</c>, <c>is seizing</c>) and our land
        /// (<c>our land</c>, <c>my provinces</c>, <c>from us</c>). Mechanical, so approximate: it reads
        /// "Valen has taken our land" and "peace from those who stole our land", and not "we will take
        /// their land".
        /// </summary>
        public static bool ClaimsLandTaken(string statement)
        {
            if (string.IsNullOrWhiteSpace(statement)) return false;
            foreach (string sentence in Regex.Split(statement, @"(?<=[.!?;])\s+"))
                if (Taking.IsMatch(sentence) && OurLand.IsMatch(sentence)) return true;
            return false;
        }

        /// <summary>
        /// Whether a land grievance in <paramref name="d"/>'s statement could be true: someone it
        /// accuses took a province from <paramref name="d"/>'s country on an earlier turn of the game.
        /// The accused are the countries the statement names; with none, the move's counterpart (the
        /// country targeted, the owner of the province targeted, or the envoy's offerer); with none of
        /// those either, anyone.
        /// </summary>
        public static bool GrievanceHappened(ScoredGame g, SeatDecisionInfo d)
        {
            var world = g.Record.Game.World;
            var countries = new HashSet<string>(world.CountryList.Select(c => c.Id), StringComparer.Ordinal);
            var takers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var report in g.Record.Reports.Where(r => r.Turn < d.Turn))
                foreach (var o in report.Outcomes)
                {
                    var m = o.Request == null || o.Refused ? null : TookFrom.Match(o.Text ?? "");
                    if (m != null && m.Success && m.Groups[2].Value == d.Country) takers.Add(o.Request.CountryId);
                }

            var accused = new HashSet<string>(Named(d.Statement, world).Where(countries.Contains), StringComparer.Ordinal);
            accused.Remove(d.Country);
            if (accused.Count == 0)
            {
                string counterpart = d.Moment == Moment.Envoy ? d.OfferedBy
                    : d.AskedTarget != null && countries.Contains(d.AskedTarget) ? d.AskedTarget
                    : g.Record.Game.Snapshots.FirstOrDefault(s => s.Turn == d.Turn)?.OwnerOf(d.AskedTarget);
                if (counterpart != null && counterpart != d.Country) accused.Add(counterpart);
            }
            return accused.Count == 0 ? takers.Count > 0 : accused.Overlaps(takers);
        }

        // Game.Battle's line for a won attack: "took fenmark from orsk (34 against 21); lost 6".
        private static readonly Regex TookFrom = new Regex(@"^took (\S+) from (\S+) \(");

        private static bool Takes(string action) => action != null && (Actions.TargetsProvince(action) || Actions.TargetsCountry(action));

        public static int Words(string s) => string.IsNullOrWhiteSpace(s) ? 0 : s.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

        public static string Normal(string s) => Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");

        // Lower case, every run of characters that are not letters or digits one space, so the
        // model's own punctuation does not hide a copy.
        private static string Bare(string s) => Regex.Replace(s.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();

        /// <summary>Whether the statement holds the whole rule word for word, ignoring case and punctuation.</summary>
        public static bool CopiesRule(string statement, string rules)
        {
            if (string.IsNullOrWhiteSpace(statement) || string.IsNullOrWhiteSpace(rules)) return false;
            string rule = Bare(rules);
            return rule.Length > 0 && (" " + Bare(statement) + " ").Contains(" " + rule + " ");
        }

        /// <summary>Whether the statement holds any sentence of the rule of four words or more word for word, ignoring case and punctuation.</summary>
        public static bool CopiesRuleSentence(string statement, string rules)
        {
            if (string.IsNullOrWhiteSpace(statement) || string.IsNullOrWhiteSpace(rules)) return false;
            string said = " " + Bare(statement) + " ";
            foreach (string sentence in Regex.Split(rules, @"(?<=[.!?:])\s+"))
            {
                string words = Bare(sentence);
                if (Words(sentence) >= 4 && said.Contains(" " + words + " ")) return true;
            }

            return CopiesRule(statement, rules);
        }

        /// <summary>The country and province ids a statement names, as whole words, ignoring case.</summary>
        public static HashSet<string> Named(string statement, World world)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(statement)) return found;
            foreach (string id in world.CountryList.Select(c => c.Id).Concat(world.ProvinceList.Select(p => p.Id)))
                if (Regex.IsMatch(statement, @"\b" + Regex.Escape(id) + @"\b", RegexOptions.IgnoreCase)) found.Add(id);
            return found;
        }

        // --- Events --------------------------------------------------------------------------

        private static readonly HashSet<string> Common = new HashSet<string>(StringComparer.Ordinal)
        {
            "the", "a", "an", "of", "and", "in", "on", "at", "to", "from", "with", "for", "over", "under", "its", "their", "his", "her",
            "our", "by", "as", "is", "was", "were", "it", "that", "this", "into", "upon", "all", "who", "when", "while", "has", "had",
        };

        private static List<string> Tokens(string s) =>
            string.IsNullOrEmpty(s) ? new List<string>() : Regex.Matches(s.ToLowerInvariant(), @"[\p{L}']+").Cast<Match>().Select(m => m.Value.Trim('\'')).Where(t => t.Length > 0).ToList();

        private static HashSet<string> Bigrams(string s)
        {
            var t = Tokens(s);
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i + 1 < t.Count; i++)
                if (!(Common.Contains(t[i]) && Common.Contains(t[i + 1]))) set.Add(t[i] + " " + t[i + 1]);
            return set;
        }

        /// <summary>Whether a ruler's statement names an event: a word of its title of four letters or more (not a common word, not a place), or a word of its kind.</summary>
        public static bool NamesEvent(string statement, EventRecord e, World world)
        {
            if (string.IsNullOrWhiteSpace(statement)) return false;
            var said = new HashSet<string>(Tokens(statement), StringComparer.Ordinal);
            var places = new HashSet<string>(world.CountryList.Select(c => c.Id).Concat(world.ProvinceList.Select(p => p.Id)), StringComparer.Ordinal);
            var words = Tokens(e.Title).Where(w => w.Length >= 4 && !Common.Contains(w) && !places.Contains(w)).Concat(EventRules.KeywordsFor(e.Template));
            return words.Any(said.Contains);
        }

        /// <summary>The Gini coefficient of signed values: the mean absolute difference over twice the mean absolute value; NaN when all are 0.</summary>
        public static double SignedGini(IReadOnlyList<double> xs)
        {
            if (xs.Count == 0) return double.NaN;
            double meanAbs = xs.Average(x => Math.Abs(x));
            if (meanAbs == 0) return double.NaN;
            double sum = 0;
            foreach (double a in xs)
                foreach (double b in xs) sum += Math.Abs(a - b);
            return sum / (2.0 * xs.Count * xs.Count * meanAbs);
        }

        private static double Pearson(IReadOnlyList<(double x, double y)> pts)
        {
            if (pts.Count < 3) return double.NaN;
            double mx = pts.Average(p => p.x), my = pts.Average(p => p.y);
            double sxy = pts.Sum(p => (p.x - mx) * (p.y - my));
            double sxx = pts.Sum(p => (p.x - mx) * (p.x - mx)), syy = pts.Sum(p => (p.y - my) * (p.y - my));
            return sxx == 0 || syy == 0 ? double.NaN : sxy / Math.Sqrt(sxx * syy);
        }

        private void EventsSection()
        {
            const string S = "events", G = "table";
            int n = Games.Count;
            var perGame = Games.Select(g => (g, ev: g.Record.Reports.SelectMany(r => r.Events).ToList())).ToList();
            var all = perGame.SelectMany(x => x.ev).ToList();
            Add(S, G, "events a game", all.Count / (double)n);
            Add(S, G, "event value a game", perGame.Average(x => (double)x.ev.Sum(e => e.Value.Values.Sum(v => Math.Abs(v)))));
            Add(S, G, "gifts share", Share(all.Count(e => e.TotalValue > 0), all.Count));
            Add(S, G, "distinct template-target pairs a game", perGame.Average(x => (double)x.ev.Select(e => e.Template + "|" + e.Target).Distinct().Count()));
            Add(S, G, "distinct templates a game", perGame.Average(x => (double)x.ev.Select(e => e.Template).Distinct().Count()));

            var told = perGame.Select(x => (x.g, ev: x.ev.Where(e => e.Source == EventRecord.FromStoryteller).ToList())).ToList();
            int toldCount = told.Sum(x => x.ev.Count);
            if (toldCount > 0 || Games.Any(g => g.Storyteller.Count > 0))
            {
                int distinct = told.Sum(x => x.ev.Select(e => Normal(e.Title ?? "")).Distinct().Count());
                Add(S, G, "distinct titles", Share(distinct, toldCount));
                int stories = 0;
                double shared = 0;
                foreach (var x in told)
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var e in x.ev)
                    {
                        var b = Bigrams(e.Story);
                        if (seen.Count > 0 && b.Count > 0)
                        {
                            stories++;
                            shared += b.Count(seen.Contains) / (double)b.Count;
                        }
                        seen.UnionWith(b);
                    }
                }
                Add(S, G, "story phrases shared", stories == 0 ? double.NaN : shared / stories);
                if (n >= 2)
                {
                    var bySeed = told.SelectMany(x => x.ev.Select(e => (seed: x.g.Record.Seed, title: Normal(e.Title ?? "")))).ToList();
                    var games = bySeed.GroupBy(t => t.title).ToDictionary(t => t.Key, t => t.Select(z => z.seed).Distinct().Count());
                    Add(S, G, "titles shared across seeds", Share(bySeed.Count(t => games[t.title] > 1), bySeed.Count));
                }
                Add(S, G, "stories replaced", Share(told.Sum(x => x.ev.Count(e => e.StoryReplaced)), toldCount));
                Add(S, G, "titles missing", Share(told.Sum(x => x.ev.Count(e => EventRules.SplitTitle(e.Statement).title == null)), toldCount));
            }

            // Fairness: how unevenly the net worth fell, and whether it followed standing.
            var ginis = new List<double>();
            var tracks = new List<double>();
            foreach (var x in perGame)
            {
                var world = x.g.Record.Game.World;
                var net = world.CountryList.Select(c => (double)x.ev.Where(e => e.Value.ContainsKey(c.Id)).Sum(e => e.Value[c.Id])).ToList();
                double gi = SignedGini(net);
                if (!double.IsNaN(gi)) ginis.Add(gi);
                var pts = new List<(double, double)>();
                foreach (var e in x.ev)
                {
                    if (e.ProvincesAtTime.Count == 0) continue;
                    double mean = e.ProvincesAtTime.Values.Average();
                    foreach (string h in e.Hit)
                        if (e.ProvincesAtTime.TryGetValue(h, out int held)) pts.Add((e.Value[h], held - mean));
                }
                double r = Pearson(pts);
                if (!double.IsNaN(r)) tracks.Add(r);
            }
            Add(S, G, "net value Gini", ginis.Count == 0 ? double.NaN : ginis.Average());
            Add(S, G, "value tracks standing", tracks.Count == 0 ? double.NaN : tracks.Average());

            // Reaction: a hit model ruler's statement on that turn's council names the event.
            int hitSeats = 0, naming = 0;
            foreach (var x in perGame)
            {
                if (x.g.Decisions.Count == 0) continue;
                var world = x.g.Record.Game.World;
                foreach (var e in x.ev)
                    foreach (string h in e.Hit)
                    {
                        var d = x.g.Decisions.FirstOrDefault(s => s.Country == h && s.Moment == Moment.Council && s.Turn == e.Turn && s.Answered);
                        if (d == null || string.IsNullOrWhiteSpace(d.Statement)) continue;
                        hitSeats++;
                        if (NamesEvent(d.Statement, e, world)) naming++;
                    }
            }
            if (hitSeats > 0) Add(S, G, "hit rulers naming it", Share(naming, hitSeats));

            var calls = Games.SelectMany(g => g.Storyteller).ToList();
            if (calls.Count > 0)
            {
                var answered = calls.Where(c => c.Answered).ToList();
                Add(S, G, "storyteller calls", calls.Count);
                Add(S, G, "storyteller chose none", Share(calls.Count(c => c.None), calls.Count));
                Add(S, G, "storyteller illegal answers", Share(answered.Count(c => !c.AskedLegal), answered.Count));
                Add(S, G, "storyteller contained", Share(calls.Count(c => c.Contained), calls.Count));
                Add(S, G, "storyteller pipeline errors", calls.Count(c => c.Error));
                Add(S, G, "events refused at upkeep", Games.Sum(g => g.Record.Reports.Count(r => r.EventRefused != null)));
                var lat = answered.Select(c => (double)c.LatencySeconds).OrderBy(v => v).ToList();
                Add(S, G, "storyteller median latency", lat.Count == 0 ? double.NaN : lat[lat.Count / 2]);
            }
        }

        // --- Strength ------------------------------------------------------------------------

        private void Strength(Func<ScoredGame, string, string> key)
        {
            const string S = "strength";
            var seats = new List<(string label, double place, bool won, int provinces, bool out_)>();
            foreach (var g in Games.Where(g => g.Record.Result != null))
            {
                var result = g.Record.Result;
                var world = g.Record.Game.World;
                foreach (var c in world.CountryList)
                {
                    int mine = result.Provinces[c.Id];
                    int better = result.Provinces.Values.Count(v => v > mine);
                    int equal = result.Provinces.Values.Count(v => v == mine);
                    double place = better + (equal + 1) / 2.0;
                    seats.Add((key(g, c.Id), place, result.Winner == c.Id, mine, !c.Alive));
                }
            }
            foreach (var group in seats.GroupBy(s => s.label).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                Add(S, group.Key, "seats", group.Count());
                Add(S, group.Key, "placement", group.Average(s => s.place));
                Add(S, group.Key, "wins", group.Count(s => s.won));
                Add(S, group.Key, "provinces at the end", group.Average(s => (double)s.provinces));
                Add(S, group.Key, "knocked out", group.Count(s => s.out_));
            }
        }

        // --- Output --------------------------------------------------------------------------

        private static double Share(int part, int whole) => whole == 0 ? double.NaN : part / (double)whole;

        private static double Sd(IReadOnlyList<double> xs)
        {
            if (xs.Count < 2) return double.NaN;
            double m = xs.Average();
            return Math.Sqrt(xs.Sum(x => (x - m) * (x - m)) / (xs.Count - 1));
        }

        private static readonly HashSet<string> Counts = new HashSet<string>
        {
            "games", "council moves", "attacks landed", "moves against a country", "attacks suffered", "peace offers received",
            "pipeline errors", "seat-games measured", "answers", "targeted statements", "statements with a rule", "seats", "wins", "knocked out",
            "storyteller calls", "storyteller pipeline errors", "events refused at upkeep",
        };

        private static readonly HashSet<string> Plain = new HashSet<string>
        {
            "turns", "wars declared a game", "peace offered a game", "peace made a game", "battles a turn",
            "provinces changing hands a turn", "provinces taken from a country a game", "lead changes a game", "eliminations a game",
            "final map Gini", "words a statement", "placement", "provinces at the end",
            "events a game", "event value a game", "distinct template-target pairs a game", "distinct templates a game", "net value Gini",
            "value tracks standing", "storyteller median latency",
        };

        public static string Format(string metric, double v)
        {
            if (double.IsNaN(v)) return "–";
            if (Counts.Contains(metric)) return v.ToString("0", Inv);
            if (metric.StartsWith("eta2", StringComparison.Ordinal)) return v.ToString("0.000", Inv);
            if (metric.StartsWith("p ", StringComparison.Ordinal)) return v.ToString("0.000", Inv);
            if (metric.EndsWith(" sd between games", StringComparison.Ordinal)) return v.ToString("0.00", Inv);
            if (Plain.Contains(metric)) return v.ToString("0.00", Inv);
            return (100 * v).ToString("0", Inv) + "%";
        }

        public string ToCsv()
        {
            var sb = new StringBuilder("table,section,group,metric,value\n");
            foreach (var r in Rows)
                sb.AppendLine(string.Join(",", Q(Table), r.Section, Q(r.Group), Q(r.Metric), double.IsNaN(r.Value) ? "" : r.Value.ToString("R", Inv)));
            return sb.ToString();
        }

        private static string Q(string s) => s != null && (s.Contains(",") || s.Contains("\"")) ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Scorecard: {Table}");
            sb.AppendLine();
            if (Note != null)
            {
                sb.AppendLine(Note);
                sb.AppendLine();
            }
            foreach (string section in new[] { "liveliness", "sense", "character", "voice", "events", "strength" })
            {
                var rows = Rows.Where(r => r.Section == section).ToList();
                sb.AppendLine($"## {char.ToUpperInvariant(section[0])}{section.Substring(1)}");
                sb.AppendLine();
                if (rows.Count == 0)
                {
                    sb.AppendLine("Nothing to score: no seat here makes statements.");
                    sb.AppendLine();
                    continue;
                }
                var groups = rows.Select(r => r.Group).Distinct().ToList();
                var metrics = rows.Select(r => r.Metric).Distinct().ToList();
                sb.AppendLine("| Metric | " + string.Join(" | ", groups) + " |");
                sb.AppendLine("|---|" + string.Concat(groups.Select(_ => "---:|")));
                foreach (string m in metrics)
                    sb.AppendLine($"| {m} | " + string.Join(" | ", groups.Select(g =>
                    {
                        var r = rows.FirstOrDefault(x => x.Group == g && x.Metric == m);
                        return r == null ? "" : Format(m, r.Value);
                    })) + " |");
                sb.AppendLine();
            }
            sb.AppendLine("## What each number is for");
            sb.AppendLine();
            foreach (var d in Definitions) sb.AppendLine($"- **{d.metric}** ({d.section}): {d.why}");
            return sb.ToString();
        }
    }
}
