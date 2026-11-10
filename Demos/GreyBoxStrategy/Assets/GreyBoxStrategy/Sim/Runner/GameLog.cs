using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GreyBoxStrategy
{
    /// <summary>
    /// A game written out turn by turn for a person to read: every decision with the facts the
    /// player saw, what it was offered, what it chose and what came of it, then the map.
    /// </summary>
    public static class GameLog
    {
        public static string Write(GameRecord record)
        {
            var sb = new StringBuilder();
            var result = record.Result;
            var world = record.Game.World;
            sb.AppendLine($"# Game {record.Seed}: {result.Winner} ({record.WinnerKind}) wins, {SeriesSummary.Describe(result.Ending)}, turn {result.Turns}");
            sb.AppendLine();
            sb.AppendLine("| Country | Player | Provinces at the end | Out on turn |");
            sb.AppendLine("|---|---|---:|---:|");
            foreach (var c in world.CountryList)
                sb.AppendLine($"| {c.Id} | {record.Kinds[c.Id]} | {result.Provinces[c.Id]} | {(c.Alive ? "" : c.EliminatedOnTurn.ToString())} |");
            sb.AppendLine();
            sb.AppendLine("The map is printed after every turn: one letter per province, the owner's initial, upper case on a");
            sb.AppendLine("capital, `.` for neutral. Standing facts are what an observation source will return in stage 2;");
            sb.AppendLine("this turn's facts go with the call.");

            foreach (var report in record.Reports)
            {
                sb.AppendLine();
                sb.AppendLine($"## Turn {report.Turn}");
                if (report.Upkeep.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Upkeep:");
                    foreach (var line in report.Upkeep) sb.AppendLine("- " + line);
                }

                foreach (var o in report.Outcomes.OrderBy(o => o.Request.Moment).ThenBy(o => Seat(world, o.Request.CountryId)))
                {
                    var req = o.Request;
                    sb.AppendLine();
                    sb.AppendLine(req.Moment == Moment.Council
                        ? $"### {req.CountryId} ({record.Kinds[req.CountryId]}): council"
                        : $"### {req.CountryId} ({record.Kinds[req.CountryId]}): envoy from {req.OfferedBy}");
                    sb.AppendLine();
                    sb.AppendLine("Standing facts:");
                    foreach (var f in req.StandingFacts) sb.AppendLine("- " + f);
                    sb.AppendLine();
                    if (req.TurnFacts.Count == 0) sb.AppendLine("This turn: nothing new.");
                    else
                    {
                        sb.AppendLine("This turn:");
                        foreach (var f in req.TurnFacts) sb.AppendLine("- " + f);
                    }
                    sb.AppendLine();
                    sb.AppendLine($"Offered ({req.Legal.Count}): " + string.Join(" · ", req.Legal.Select(Show)));
                    sb.AppendLine();
                    sb.AppendLine($"**Chose `{o.Choice}`** → {o.Text}" + (o.Refused ? " **(refused)**" : ""));
                }

                if (report.Resolution.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("### What happened");
                    sb.AppendLine();
                    foreach (var line in report.Resolution) sb.AppendLine("- " + line);
                }
                sb.AppendLine();
                sb.AppendLine("```text");
                sb.AppendLine(report.OwnerGrid);
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("Provinces: " + string.Join(", ", report.ProvinceCounts.Select(kv => $"{kv.Key} {kv.Value}"))
                    + $". Changed hands this turn: {report.TerritoryChanges}.");
            }
            return sb.ToString();
        }

        private static string Show(Move m) => m.Detail == null ? $"`{m}`" : $"`{m}` ({m.Detail})";

        private static int Seat(World w, string id)
        {
            for (int i = 0; i < w.CountryList.Count; i++) if (w.CountryList[i].Id == id) return i;
            return int.MaxValue;
        }
    }

    /// <summary>
    /// Plays a series and writes it to a folder: <c>summary.md</c>, <c>games.csv</c>, and one
    /// <c>game-&lt;seed&gt;.md</c> per game. This is what the editor menu and the Unity CLI call.
    /// </summary>
    public static class StrategyRuns
    {
        public static SeriesSummary Run(string folder, string lineup, int games, int firstSeed = 1, bool writeGameLogs = true)
        {
            var records = GameRunner.PlaySeries(lineup, GameRunner.Seeds(games, firstSeed));
            var summary = new SeriesSummary(lineup, records);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "summary.md"), summary.ToMarkdown());
            File.WriteAllText(Path.Combine(folder, "games.csv"), summary.ToCsv());
            if (writeGameLogs)
                foreach (var r in records)
                    File.WriteAllText(Path.Combine(folder, $"game-{r.Seed:D4}.md"), GameLog.Write(r));
            return summary;
        }

        /// <summary>A fresh folder under <paramref name="root"/>, named for the time and the line-up.</summary>
        public static string NewFolder(string root, string lineup) =>
            Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + lineup);
    }
}
