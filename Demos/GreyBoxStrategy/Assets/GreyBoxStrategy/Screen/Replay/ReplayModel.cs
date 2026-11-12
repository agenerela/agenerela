using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>What one event on the screen is: the chronicle files it, the map animates it.</summary>
    public enum ReplayEventKind
    {
        /// <summary>An upkeep line that changes nothing on the map: a harvest, bandits, a bankruptcy.</summary>
        Upkeep,
        /// <summary>A province broke away from its owner and is neutral again.</summary>
        Revolt,
        /// <summary>A truce ended this turn.</summary>
        TruceEnded,
        /// <summary>A country offered another peace.</summary>
        PeaceOffered,
        /// <summary>An envoy's answer: accepted, so a truce begins.</summary>
        PeaceAccepted,
        /// <summary>An envoy's answer: refused.</summary>
        PeaceRefused,
        /// <summary>Two countries offered each other peace in one turn: a truce without asking.</summary>
        PeaceMutual,
        Recruit,
        Taxes,
        Fort,
        WarDeclared,
        /// <summary>An attack that took the province: the colour changes.</summary>
        Capture,
        AttackFailed,
        /// <summary>A move the re-check refused at resolution, with the reason.</summary>
        Refused,
        Hold,
        /// <summary>A country lost its last province.</summary>
        Eliminated,
    }

    /// <summary>A country's numbers at one moment.</summary>
    public sealed class CountryState
    {
        public string Id;
        public int Gold;
        public int Army;
        public int Stability;
        public bool Alive;
        public int Provinces;
        public int Income;
        public int Upkeep;
        public CountryState Clone() => (CountryState)MemberwiseClone();
    }

    /// <summary>A relation between two countries at one moment.</summary>
    public sealed class RelationState
    {
        public string A;
        public string B;
        /// <summary>"war" or "truce"; pairs at peace are absent.</summary>
        public string Kind;
        public int TruceTurnsLeft;
        /// <summary>The turn a war began, as far as the replay saw it; 0 if unknown.</summary>
        public int Since;
    }

    /// <summary>The map and the countries at one moment: what the screen draws.</summary>
    public sealed class ReplayState
    {
        /// <summary>Owner per province index (row × columns + column); null for neutral.</summary>
        public string[] Owners;
        public int[] Forts;
        public int[] Garrisons;
        public Dictionary<string, CountryState> Countries = new Dictionary<string, CountryState>(StringComparer.Ordinal);
        public List<RelationState> Relations = new List<RelationState>();

        public ReplayState Clone() => new ReplayState
        {
            Owners = (string[])Owners.Clone(),
            Forts = (int[])Forts.Clone(),
            Garrisons = (int[])Garrisons.Clone(),
            Countries = Countries.ToDictionary(kv => kv.Key, kv => kv.Value.Clone(), StringComparer.Ordinal),
            Relations = Relations.Select(r => new RelationState { A = r.A, B = r.B, Kind = r.Kind, TruceTurnsLeft = r.TruceTurnsLeft, Since = r.Since }).ToList(),
        };

        public int ProvinceCount(string country) => Owners.Count(o => o == country);

        public RelationState Relation(string a, string b) =>
            Relations.FirstOrDefault(r => (r.A == a && r.B == b) || (r.A == b && r.B == a));
    }

    /// <summary>One thing that happens on the screen, in the order the game made it happen.</summary>
    public sealed class ReplayEvent
    {
        public int Turn;
        public ReplayEventKind Kind;
        /// <summary>Whose move or whose event this is.</summary>
        public string Country;
        /// <summary>The province it concerns, or null.</summary>
        public string Province;
        public int ProvinceIndex = -1;
        /// <summary>The other country it concerns: a war's target, a captured province's last owner, an envoy's partner.</summary>
        public string Other;
        public int Attack;
        public int Defence;
        /// <summary>The game's own line, word for word.</summary>
        public string Text;
        /// <summary>The ruler's words with this move, or empty.</summary>
        public string Statement = "";
        /// <summary>The decision behind it, or null for an upkeep event.</summary>
        public RecordedDecision Decision;
        /// <summary>The state once this event has happened.</summary>
        public ReplayState After;

        public bool ChangesMap => Kind == ReplayEventKind.Capture || Kind == ReplayEventKind.Revolt;
    }

    /// <summary>One turn: the state it opened with, and what happened in it.</summary>
    public sealed class ReplayTurn
    {
        public int Turn;
        /// <summary>Before upkeep: the last turn's end.</summary>
        public ReplayState Start;
        /// <summary>After upkeep, when the council decided.</summary>
        public ReplayState Council;
        public List<ReplayEvent> Events = new List<ReplayEvent>();
        /// <summary>The game's own count of provinces that changed hands this turn, revolts included.</summary>
        public int TerritoryChanges;
        public ReplayState End => Events.Count > 0 ? Events[Events.Count - 1].After : Council;
    }

    /// <summary>A whole recorded game, ready to play back.</summary>
    public sealed class ReplayGame
    {
        public int Seed;
        public string Title;
        public IReadOnlyList<string> CountryIds;
        public IReadOnlyList<string> ProvinceIds;
        public int Columns;
        public Dictionary<string, RecordedPlayer> Players;
        public ReplayState Initial;
        public List<ReplayTurn> Turns = new List<ReplayTurn>();
        public GameResult Result;
        public IEnumerable<ReplayEvent> AllEvents => Turns.SelectMany(t => t.Events);
        public int IndexOf(string provinceId)
        {
            for (int i = 0; i < ProvinceIds.Count; i++) if (ProvinceIds[i] == provinceId) return i;
            return -1;
        }
    }
}
