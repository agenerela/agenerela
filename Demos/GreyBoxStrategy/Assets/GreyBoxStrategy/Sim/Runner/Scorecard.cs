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
    }

    /// <summary>One played game, with who sat where: a label per country (the player: a model or a script kind) and a character (a persona, or a placebo slot).</summary>
    public sealed class ScoredGame
    {
        public ScoredGame(GameRecord record, IReadOnlyDictionary<string, string> labels,
            IReadOnlyDictionary<string, string> characters, IReadOnlyList<SeatDecisionInfo> decisions = null)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            Labels = labels ?? record.Kinds;
            Characters = characters ?? Labels;
            Decisions = decisions ?? Array.Empty<SeatDecisionInfo>();
        }

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
            ("voice", "names its target", "Of targeted moves, statements that name the target: the words fit the deed."),
            ("voice", "contradicts its move", "Of targeted moves whose statement names a country or province, those that do not name the target: the words name someone else."),
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
            }
        }

        private static bool Takes(string action) => action != null && (Actions.TargetsProvince(action) || Actions.TargetsCountry(action));

        public static int Words(string s) => string.IsNullOrWhiteSpace(s) ? 0 : s.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

        public static string Normal(string s) => Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");

        /// <summary>The country and province ids a statement names, as whole words, ignoring case.</summary>
        public static HashSet<string> Named(string statement, World world)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(statement)) return found;
            foreach (string id in world.CountryList.Select(c => c.Id).Concat(world.ProvinceList.Select(p => p.Id)))
                if (Regex.IsMatch(statement, @"\b" + Regex.Escape(id) + @"\b", RegexOptions.IgnoreCase)) found.Add(id);
            return found;
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
            "pipeline errors", "seat-games measured", "answers", "targeted statements", "seats", "wins", "knocked out",
        };

        private static readonly HashSet<string> Plain = new HashSet<string>
        {
            "turns", "wars declared a game", "peace offered a game", "peace made a game", "battles a turn",
            "provinces changing hands a turn", "provinces taken from a country a game", "lead changes a game", "eliminations a game",
            "final map Gini", "words a statement", "placement", "provinces at the end",
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
            foreach (string section in new[] { "liveliness", "sense", "character", "voice", "strength" })
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
