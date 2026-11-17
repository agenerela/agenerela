using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// Fog of war in rule mode: what your country's court knows, which is what a model ruler is
    /// told on the same turn (<c>Facts.Standing</c>, the moves' target descriptions), no more.
    /// </summary>
    /// <remarks>
    /// <para><b>Known:</b> who holds every province (the map is public); each country's army and
    /// province count, and its relation to you (a model ruler is told both in its standing
    /// facts); your own country in full; garrisons and forts of every province that is yours or
    /// touches your land (the targets a model is told about); every move that lands, as it lands,
    /// because a war or a capture is seen by everyone.</para>
    /// <para><b>Hidden:</b> another country's treasury, income, upkeep and stability; the
    /// garrisons and forts of provinces beyond your borders; the harvests, bandits and unrest of
    /// other countries; and what a rival's court tells it (its standing facts, and why moves were
    /// not offered), which open in the inspector once the game is over.</para>
    /// <para><b>Open as they happen</b> (the lead, 12 November: "we should show what they do and
    /// what they say"): every rival's decision in the inspector, its answer, its statement, the
    /// moves it could choose from, the guards and the timing.</para>
    /// Watch mode has no viewer, and shows everything.
    /// </remarks>
    public sealed class Fog
    {
        private readonly RealmQueries queries;

        public Fog(string viewer, RealmQueries queries)
        {
            Viewer = viewer;
            this.queries = queries;
        }

        /// <summary>The country you rule; null in watch mode, where nothing is hidden.</summary>
        public string Viewer { get; }
        public bool On => Viewer != null;

        /// <summary>Whether its garrison and forts are known: it is yours or touches your land.</summary>
        public bool KnowsProvince(ReplayState s, int province)
        {
            if (!On) return true;
            if (s.Owners[province] == Viewer) return true;
            return queries.Neighbours(province).Any(q => s.Owners[q] == Viewer);
        }

        /// <summary>Whether a country's treasury, income, upkeep and stability are known: only your own.</summary>
        public bool KnowsTreasury(string country) => !On || country == Viewer;

        /// <summary>Whether a ruler's decision opens in the inspector: always, since what rulers do and say is public.</summary>
        public bool KnowsDecision(string country, bool gameOver) => true;

        /// <summary>Whether what a ruler's court told it can be read: your own, or any once the game is over. It holds the treasury.</summary>
        public bool KnowsReport(string country, bool gameOver) => !On || gameOver || country == Viewer
            // The storyteller and its scribe are no country and keep no secrets: what they read and were offered is the showcase.
            || country == Recording.StorytellerId || country == Recording.ScribeId;

        /// <summary>Whether an upkeep line is news to you: your own, and every revolt or truce, since land and relations are public.</summary>
        public bool KnowsUpkeep(ReplayEvent e) =>
            !On || e.Kind != ReplayEventKind.Upkeep || e.Country == null || e.Country == Viewer;

        /// <summary>The provinces whose garrison and forts you know.</summary>
        public HashSet<int> Known(ReplayState s) =>
            new HashSet<int>(Enumerable.Range(0, s.Owners.Length).Where(p => KnowsProvince(s, p)));
    }
}

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// What a country's court sees on the writs turn (rules v2, 3.8): its own and controlled provinces, every
    /// province with one of its armies, and the provinces bordering all of them. Rival armies beyond it are not shown.
    /// </summary>
    public static class WritsSight
    {
        public static System.Collections.Generic.HashSet<string> Of(ReplayState s, System.Collections.Generic.IReadOnlyList<string> provinceIds, string you,
            System.Func<string, System.Collections.Generic.IReadOnlyList<string>> neighbours)
        {
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            var core = new System.Collections.Generic.List<string>();
            for (int i = 0; i < provinceIds.Count; i++) if (s.Controller(i) == you || s.Owners[i] == you) core.Add(provinceIds[i]);
            foreach (var a in s.Armies) if (a.Owner == you) core.Add(a.Location);
            foreach (string p in core) { seen.Add(p); foreach (string n in neighbours(p)) seen.Add(n); }
            return seen;
        }
    }

    /// <summary>
    /// Whether a ruler says exactly what it said the last time it spoke. On the writs turn a country speaks
    /// several times a turn, and the measured rulers repeat lines word for word (35% against 5% in the one-move
    /// game); the screen shows a repeated line once, and the chronicle marks the repeat "said again".
    /// </summary>
    public sealed class RepeatFilter
    {
        private readonly System.Collections.Generic.Dictionary<string, string> last = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);

        public bool Repeats(string country, string line)
        {
            if (country == null || string.IsNullOrWhiteSpace(line)) return false;
            string norm = line.Trim();
            bool same = last.TryGetValue(country, out string before) && string.Equals(before, norm, System.StringComparison.OrdinalIgnoreCase);
            last[country] = norm;
            return same;
        }
    }
}
