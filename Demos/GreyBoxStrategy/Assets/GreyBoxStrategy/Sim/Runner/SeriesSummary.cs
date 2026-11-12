using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace GreyBoxStrategy
{
    /// <summary>The numbers a series of games comes to, and the tables that show them.</summary>
    public sealed class SeriesSummary
    {
        public sealed class KindRow
        {
            public string Kind;
            public int Seats;
            public int Wins;
            public double WinRate => Seats == 0 ? 0 : (double)Wins / Seats;
            public double MeanFinalProvinces;
            public int Eliminated;
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public SeriesSummary(string lineup, IReadOnlyList<GameRecord> games)
        {
            Lineup = lineup;
            Games = games;
            var countries = games.Count == 0 ? new List<string>() : games[0].Game.World.CountryList.Select(c => c.Id).ToList();

            Kinds = games.SelectMany(g => g.Kinds.Select(kv => (game: g, country: kv.Key, kind: kv.Value)))
                .GroupBy(x => x.kind)
                .Select(grp => new KindRow
                {
                    Kind = grp.Key,
                    Seats = grp.Count(),
                    Wins = grp.Count(x => x.game.Result.Winner == x.country),
                    MeanFinalProvinces = grp.Average(x => (double)x.game.Result.Provinces[x.country]),
                    Eliminated = grp.Count(x => !x.game.Game.World.Country(x.country).Alive),
                })
                .OrderByDescending(r => r.WinRate).ThenBy(r => r.Kind, StringComparer.Ordinal)
                .ToList();

            WinsByCountry = countries.ToDictionary(c => c, c => games.Count(g => g.Result.Winner == c));
            Endings = Enum.GetValues(typeof(Ending)).Cast<Ending>().ToDictionary(e => e, e => games.Count(g => g.Result.Ending == e));
            var lengths = games.Select(g => g.Result.Turns).OrderBy(t => t).ToList();
            MeanTurns = lengths.Count == 0 ? 0 : lengths.Average();
            MedianTurns = lengths.Count == 0 ? 0 : lengths.Count % 2 == 1 ? lengths[lengths.Count / 2] : (lengths[lengths.Count / 2 - 1] + lengths[lengths.Count / 2]) / 2.0;
            MinTurns = lengths.Count == 0 ? 0 : lengths[0];
            MaxTurns = lengths.Count == 0 ? 0 : lengths[lengths.Count - 1];

            var reports = games.SelectMany(g => g.Reports).ToList();
            TotalTurns = reports.Count;
            TerritoryChangesPerTurn = reports.Count == 0 ? 0 : reports.Average(r => (double)r.TerritoryChanges);
            TurnsWithNoChange = reports.Count == 0 ? 0 : reports.Count(r => r.TerritoryChanges == 0) / (double)reports.Count;
            BattlesPerGame = games.Count == 0 ? 0 : reports.Sum(r => r.Battles) / (double)games.Count;
            StaleRefusals = reports.Sum(r => r.StaleRefusals);
            PeaceOffers = reports.Sum(r => r.PeaceOffers);
            PeaceAccepted = reports.Sum(r => r.PeaceAccepted);
            ChangesByTurn = reports.GroupBy(r => r.Turn).OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => (games: g.Count(), mean: g.Average(r => (double)r.TerritoryChanges)));
        }

        public string Lineup { get; }
        public IReadOnlyList<GameRecord> Games { get; }
        public List<KindRow> Kinds { get; }
        public Dictionary<string, int> WinsByCountry { get; }
        public Dictionary<Ending, int> Endings { get; }
        public double MeanTurns { get; }
        public double MedianTurns { get; }
        public int MinTurns { get; }
        public int MaxTurns { get; }
        public int TotalTurns { get; }
        public double TerritoryChangesPerTurn { get; }
        public double TurnsWithNoChange { get; }
        public double BattlesPerGame { get; }
        public int StaleRefusals { get; }
        public int PeaceOffers { get; }
        public int PeaceAccepted { get; }
        /// <summary>Per turn number: how many games were still running, and their mean territory changes.</summary>
        public Dictionary<int, (int games, double mean)> ChangesByTurn { get; }

        public KindRow Kind(string kind) => Kinds.FirstOrDefault(k => k.Kind == kind);

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# GreyBoxStrategy series: {Lineup}, {Games.Count} games");
            sb.AppendLine();
            if (Games.Count > 0)
                sb.AppendLine($"Seeds {Games.Min(g => g.Seed)} to {Games.Max(g => g.Seed)}. Seats rotate every game, so each kind plays every country equally often.");
            sb.AppendLine();
            sb.AppendLine("| Player | Seats | Wins | Win rate | Mean provinces at the end | Knocked out |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|");
            foreach (var k in Kinds)
                sb.AppendLine($"| {k.Kind} | {k.Seats} | {k.Wins} | {Pct(k.WinRate)} | {F(k.MeanFinalProvinces)} | {k.Eliminated} |");
            sb.AppendLine();
            sb.AppendLine("| Ending | Games |");
            sb.AppendLine("|---|---:|");
            // Unfinished (a conquest game at its cap) shows only when it happened, so a series of
            // today's game prints exactly as it did before rules v2 added it.
            foreach (var e in Endings.Where(e => e.Key != Ending.Unfinished || e.Value > 0)) sb.AppendLine($"| {Describe(e.Key)} | {e.Value} |");
            sb.AppendLine();
            sb.AppendLine("| Measure | Value |");
            sb.AppendLine("|---|---:|");
            sb.AppendLine($"| Game length, turns (mean / median / min / max) | {F(MeanTurns)} / {F(MedianTurns)} / {MinTurns} / {MaxTurns} |");
            sb.AppendLine($"| Provinces changing hands per turn | {F(TerritoryChangesPerTurn)} |");
            sb.AppendLine($"| Turns where no province changed hands | {Pct(TurnsWithNoChange)} |");
            sb.AppendLine($"| Battles per game | {F(BattlesPerGame)} |");
            sb.AppendLine($"| Peace offered / made | {PeaceOffers} / {PeaceAccepted} |");
            sb.AppendLine($"| Moves refused as stale at resolution | {StaleRefusals} |");
            sb.AppendLine();
            sb.AppendLine("| Wins by country | " + string.Join(" | ", WinsByCountry.Keys) + " |");
            sb.AppendLine("|---|" + string.Concat(WinsByCountry.Keys.Select(_ => "---:|")));
            sb.AppendLine("| | " + string.Join(" | ", WinsByCountry.Values) + " |");
            sb.AppendLine();
            sb.AppendLine("Provinces changing hands, by turn (mean over the games still running):");
            sb.AppendLine();
            var buckets = ChangesByTurn.GroupBy(kv => (kv.Key - 1) / 5).OrderBy(g => g.Key).ToList();
            sb.AppendLine("| Turns | " + string.Join(" | ", buckets.Select(b => $"{b.Key * 5 + 1}-{b.Key * 5 + 5}")) + " |");
            sb.AppendLine("|---|" + string.Concat(buckets.Select(_ => "---:|")));
            sb.AppendLine("| Changes per turn | " + string.Join(" | ", buckets.Select(b =>
                F(b.Sum(kv => kv.Value.mean * kv.Value.games) / Math.Max(1, b.Sum(kv => kv.Value.games))))) + " |");
            sb.AppendLine("| Games still running | " + string.Join(" | ", buckets.Select(b => b.Last().Value.games.ToString(Inv))) + " |");
            return sb.ToString();
        }

        /// <summary>One row per game, for a spreadsheet.</summary>
        public string ToCsv()
        {
            var countries = Games.Count == 0 ? new List<string>() : Games[0].Game.World.CountryList.Select(c => c.Id).ToList();
            var sb = new StringBuilder();
            sb.Append("seed,winner,winner_kind,ending,turns,territory_changes,battles,stale_refusals,peace_offers,peace_made");
            foreach (var c in countries) sb.Append($",{c}_kind,{c}_provinces");
            sb.AppendLine();
            foreach (var g in Games)
            {
                sb.Append(string.Join(",", g.Seed, g.Result.Winner, g.WinnerKind, g.Result.Ending, g.Result.Turns,
                    g.Reports.Sum(r => r.TerritoryChanges), g.Reports.Sum(r => r.Battles), g.Reports.Sum(r => r.StaleRefusals),
                    g.Reports.Sum(r => r.PeaceOffers), g.Reports.Sum(r => r.PeaceAccepted)));
                foreach (var c in countries) sb.Append($",{g.Kinds[c]},{g.Result.Provinces[c]}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static string Describe(Ending e)
        {
            switch (e)
            {
                case Ending.Domination: return "domination (15 provinces)";
                case Ending.LastStanding: return "last country standing";
                case Ending.Unfinished: return "unfinished at the turn cap";
                default: return "turn limit, most provinces";
            }
        }

        private static string F(double v) => v.ToString("0.0", Inv);
        private static string Pct(double v) => (v * 100).ToString("0", Inv) + "%";
    }
}
