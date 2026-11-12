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
    /// other countries; and the model rulers' own decisions (what they read, what they were
    /// offered, the guards), which open in the inspector once the game is over.</para>
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

        /// <summary>Whether a ruler's decision can be opened in the inspector: your own, or any once the game is over.</summary>
        public bool KnowsDecision(string country, bool gameOver) => !On || gameOver || country == Viewer;

        /// <summary>Whether an upkeep line is news to you: your own, and every revolt or truce, since land and relations are public.</summary>
        public bool KnowsUpkeep(ReplayEvent e) =>
            !On || e.Kind != ReplayEventKind.Upkeep || e.Country == null || e.Country == Viewer;

        /// <summary>The provinces whose garrison and forts you know.</summary>
        public HashSet<int> Known(ReplayState s) =>
            new HashSet<int>(Enumerable.Range(0, s.Owners.Length).Where(p => KnowsProvince(s, p)));
    }
}
