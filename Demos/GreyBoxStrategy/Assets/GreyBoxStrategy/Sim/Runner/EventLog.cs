using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GreyBoxStrategy
{
    /// <summary>
    /// Every event of a series, written out: <c>events.csv</c> one row an event, and <c>events.md</c>
    /// for a person to read, the storyteller's events with their titles, stories and story checks.
    /// </summary>
    public static class EventLog
    {
        public static string Csv(IEnumerable<GameRecord> games)
        {
            var sb = new StringBuilder("seed,turn,source,template,target,province,hit,values,total_value,title,story,story_verdict,statement,"
                                       + "action,model_title,options,choices,chosen_by,spared,chain,chain_stage,traits,history,effects\n");
            foreach (var g in games)
                foreach (var e in g.Reports.SelectMany(r => r.Events))
                    sb.AppendLine(string.Join(",", g.Seed, e.Turn, e.Source, e.Template, e.Target, e.Province, string.Join(" ", e.Hit),
                        string.Join(" ", e.Hit.Where(e.Value.ContainsKey).Select(h => $"{h}={e.Value[h]}")), e.TotalValue, Q(e.Title), Q(e.Story), Q(e.StoryVerdict), Q(e.Statement),
                        e.Action, Q(e.ModelTitle), string.Join(" ", e.Options), string.Join(" ", e.Choices.Select(kv => $"{kv.Key}={kv.Value}")),
                        string.Join(" ", e.ChosenBy.Select(kv => $"{kv.Key}={kv.Value}")), string.Join(" ", e.Spared), e.ChainId,
                        e.ChainStage == 0 ? "" : e.ChainStage.ToString(), Q(e.Traits), Q(e.HistoryRef),
                        Q(string.Join("; ", e.Hit.Where(e.Effect.ContainsKey).Select(h => $"{h}: {e.Effect[h]}")))));
            return sb.ToString();
        }

        /// <summary>Every chain of every game (S2): one row a chain, with its events by turn and template.</summary>
        public static string ChainsCsv(IEnumerable<GameRecord> games)
        {
            var sb = new StringBuilder("seed,chain,mode,skeleton,subject,title,status,end_reason,events,turns,templates\n");
            foreach (var g in games)
                foreach (var c in g.Game.Events.Chains)
                    sb.AppendLine(string.Join(",", g.Seed, c.Id, c.Mode, c.Skeleton, c.Subject, Q(c.Title), c.Status, Q(c.EndReason), c.Events.Count,
                        string.Join(" ", c.Events.Select(e => e.Turn)), string.Join(" ", c.Events.Select(e => e.Template))));
            return sb.ToString();
        }

        public static string Markdown(string table, IReadOnlyList<GameRecord> games)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Events: {table}");
            sb.AppendLine();
            foreach (var g in games)
            {
                var events = g.Reports.SelectMany(r => r.Events).ToList();
                var refused = g.Reports.Where(r => r.EventRefused != null).ToList();
                sb.AppendLine($"## Game {g.Seed}: {g.Result?.Winner} wins on turn {g.Result?.Turns}");
                sb.AppendLine();
                if (events.Count == 0) sb.AppendLine("No events.");
                foreach (var e in events) sb.AppendLine("- " + Line(e));
                foreach (var r in refused) sb.AppendLine($"- turn {r.Turn}: refused at upkeep, {r.EventRefused}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>One event in one line: turn, title, template and target, what it did to whom, the story and its check.</summary>
        public static string Line(EventRecord e)
        {
            string effects = string.Join(", ", e.Hit.Where(e.Value.ContainsKey).Select(h => e.Choices.ContainsKey(h)
                ? $"{h} {e.Effect[h]} ({e.ChosenBy[h]})" : $"{h} {(e.Value[h] >= 0 ? "+" : "-")}{e.Effect[h]}"));
            string where = e.Province != null ? $" in {e.Province}" : "";
            if (e.Source == EventRecord.FromTable) return $"turn {e.Turn}: {e.Template} ({e.Source}): {effects}";
            string tale = e.ChainId != null ? $" [{e.ChainId} #{e.ChainStage}]" : "";
            string line = $"turn {e.Turn}: **\"{e.Title}\"**{tale}, {e.Template} on {e.Target}{where}: {effects}. {e.Story} [{e.StoryVerdict}]";
            if (e.HistoryRef != null) line += $" (history: {e.HistoryRef})";
            if (e.StoryReplaced || e.StoryVerdict == "no title") line += $" (the model wrote: \"{e.Statement}\")";
            return line;
        }

        public static string Q(string s) => string.IsNullOrEmpty(s) ? "" : "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
    }
}
