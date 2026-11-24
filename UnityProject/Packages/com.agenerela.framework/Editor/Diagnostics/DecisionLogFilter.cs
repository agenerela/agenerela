using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Editor.UI;

namespace Agenerela.Editor.Diagnostics
{
    /// <summary>
    /// The Decision Log's filters (#172 ②): which outcomes to show, which agent, and text to find in
    /// what the agents were asked. Plain data, so a test filters without a window.
    /// </summary>
    internal sealed class DecisionLogFilter
    {
        /// <summary>Every outcome, in the order the toolbar shows them.</summary>
        internal static readonly Outcome[] Outcomes =
            { Outcome.Executed, Outcome.NoAction, Outcome.Contained, Outcome.Error, Outcome.NotExecuted };

        /// <summary>The outcomes switched off. Empty shows every outcome.</summary>
        internal HashSet<Outcome> Hidden { get; } = new HashSet<Outcome>();

        /// <summary>The one agent shown, by its name in the list; null or empty for all agents.</summary>
        internal string Agent { get; set; }

        /// <summary>Text the label or stimulus must contain, ignoring case; null or blank for any.</summary>
        internal string Search { get; set; }

        /// <summary>Whether <paramref name="row"/> passes every filter.</summary>
        internal bool Matches(DecisionLogRow row) => !Hidden.Contains(row.Outcome) && MatchesAgentAndSearch(row);

        /// <summary>
        /// Whether <paramref name="row"/> passes the agent and search filters. The outcome counts are
        /// taken over these, as the Console counts its kinds over its search, so switching one
        /// outcome off does not zero its count.
        /// </summary>
        internal bool MatchesAgentAndSearch(DecisionLogRow row)
        {
            if (!string.IsNullOrEmpty(Agent) && row.Agent != Agent)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(Search))
            {
                return true;
            }

            var find = Search.Trim();
            return Contains(row.Line.Stimulus, find) || Contains(row.Line.Label, find);
        }

        /// <summary>The rows shown, newest first.</summary>
        internal List<DecisionLogRow> Apply(IReadOnlyList<DecisionLogRow> rows)
        {
            var shown = new List<DecisionLogRow>();
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (Matches(rows[i]))
                {
                    shown.Add(rows[i]);
                }
            }

            return shown;
        }

        /// <summary>How many rows of each outcome pass the agent and search filters.</summary>
        internal Dictionary<Outcome, int> Counts(IReadOnlyList<DecisionLogRow> rows)
        {
            var counts = Outcomes.ToDictionary(o => o, _ => 0);
            foreach (var row in rows)
            {
                if (MatchesAgentAndSearch(row))
                {
                    counts[row.Outcome]++;
                }
            }

            return counts;
        }

        /// <summary>Each agent in <paramref name="rows"/>, once, in the order of first appearance.</summary>
        internal static List<string> Agents(IReadOnlyList<DecisionLogRow> rows)
        {
            return rows.Select(r => r.Agent).Distinct().ToList();
        }

        private static bool Contains(string text, string find)
        {
            return text != null && text.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
