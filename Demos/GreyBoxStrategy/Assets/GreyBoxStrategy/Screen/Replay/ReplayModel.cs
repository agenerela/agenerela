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
        /// <summary>A change made through the dev menu, at the start of the turn: the game is a dev game.</summary>
        Dev,
        /// <summary>The storyteller's event, landed at upkeep: <see cref="ReplayEvent.Story"/> has all of it.</summary>
        Story,

        // The writs turn (rules v2, R1): armies as pieces, occupation, treaties.
        /// <summary>An army's order from its marshal: march or hold. <see cref="ReplayEvent.Army"/> names it.</summary>
        ArmyOrder,
        /// <summary>Every army's steps this turn, at the same moment: <see cref="ReplayEvent.Routes"/>.</summary>
        March,
        /// <summary>A battle in a province or on a border: <see cref="ReplayEvent.Battle"/> has every term.</summary>
        Battle,
        /// <summary>An army destroyed, routed, or disbanded with nowhere to go.</summary>
        ArmyLost,
        /// <summary>An enemy province occupied: its owner stays, the occupier's hatching goes over it.</summary>
        Occupy,
        /// <summary>An owner's army freed its own occupied province.</summary>
        Free,
        /// <summary>Neutral land taken by entering it: ownership changes at once, as a capture.</summary>
        TakeNeutral,
        /// <summary>A treaty term put to the other side (<see cref="ReplayEvent.Term"/>).</summary>
        TermOffered,
        /// <summary>The other side's answer to a term: accepted or refused.</summary>
        TermAccepted,
        TermRefused,
        /// <summary>A treaty landed: occupied land changes owner or returns (<see cref="ReplayEvent.Ceded"/>, <see cref="ReplayEvent.Returned"/>).</summary>
        Treaty,
        /// <summary>A realm broken: every province occupied, no army left; its occupiers keep what they hold.</summary>
        Broken,
        /// <summary>No province changed hands for the stalemate's turns: the game ends.</summary>
        Stalemate,
    }

    /// <summary>An army at one moment, as the map draws its piece.</summary>
    public sealed class ArmyState
    {
        public string Id;
        public string Owner;
        public string Location;
        public int Soldiers;
        /// <summary>Where it marches, or null while it holds.</summary>
        public string Destination;
        /// <summary>The provinces still to enter, the next first.</summary>
        public List<string> Path = new List<string>();
        /// <summary>Above 1: on rough ground, it waits on the border before entering <see cref="Path"/>'s first.</summary>
        public int StepTurnsLeft;
        /// <summary>Why it waits for orders; empty while it carries one out.</summary>
        public List<string> Wake = new List<string>();
        public bool Awaiting => Wake.Count > 0;
        public bool Marching => Destination != null;
        public ArmyState Clone() => new ArmyState
        {
            Id = Id, Owner = Owner, Location = Location, Soldiers = Soldiers, Destination = Destination,
            Path = Path.ToList(), StepTurnsLeft = StepTurnsLeft, Wake = Wake.ToList(),
        };
    }

    /// <summary>One army's movement in a turn's <see cref="ReplayEventKind.March"/>: the provinces it entered, in order.</summary>
    public sealed class ArmyRoute
    {
        public string Army;
        public string Owner;
        public string From;
        /// <summary>The provinces entered, in order; empty when it waited on the border or met the enemy on it.</summary>
        public List<string> Steps = new List<string>();
        /// <summary>Set when it went towards this province but stopped on the border: a rough step, or a battle there.</summary>
        public string Towards;
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
        /// <summary>The writs turn: who occupies each province, by index; null where nobody does. Never null itself.</summary>
        public string[] Occupiers;
        /// <summary>The writs turn: every army on the map.</summary>
        public List<ArmyState> Armies = new List<ArmyState>();
        /// <summary>The writs turn: each war's score from the first country's side, keyed "a|b" in country order.</summary>
        public Dictionary<string, int> WarScores = new Dictionary<string, int>(StringComparer.Ordinal);
        public Dictionary<string, CountryState> Countries = new Dictionary<string, CountryState>(StringComparer.Ordinal);
        public List<RelationState> Relations = new List<RelationState>();

        public ReplayState Clone() => new ReplayState
        {
            Owners = (string[])Owners.Clone(),
            Forts = (int[])Forts.Clone(),
            Garrisons = (int[])Garrisons.Clone(),
            Occupiers = Occupiers == null ? new string[Owners.Length] : (string[])Occupiers.Clone(),
            Armies = Armies.Select(a => a.Clone()).ToList(),
            WarScores = new Dictionary<string, int>(WarScores, StringComparer.Ordinal),
            Countries = Countries.ToDictionary(kv => kv.Key, kv => kv.Value.Clone(), StringComparer.Ordinal),
            Relations = Relations.Select(r => new RelationState { A = r.A, B = r.B, Kind = r.Kind, TruceTurnsLeft = r.TruceTurnsLeft, Since = r.Since }).ToList(),
        };

        public int ProvinceCount(string country) => Owners.Count(o => o == country);

        /// <summary>Who holds a province now: its occupier, or else its owner.</summary>
        public string Controller(int province) => Occupiers?[province] ?? Owners[province];

        public ArmyState Army(string id) => Armies.FirstOrDefault(a => a.Id == id);

        /// <summary>The war score between two countries from <paramref name="us"/>'s side; 0 when not at war.</summary>
        public int WarScore(string us, string them)
        {
            if (WarScores.TryGetValue(us + "|" + them, out int s)) return s;
            return WarScores.TryGetValue(them + "|" + us, out s) ? -s : 0;
        }

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

        public bool ChangesMap => Kind == ReplayEventKind.Capture || Kind == ReplayEventKind.Revolt || Kind == ReplayEventKind.TakeNeutral
                                  || Kind == ReplayEventKind.Treaty || Kind == ReplayEventKind.Broken;

        /// <summary>The army concerned: an order's, a lost one's.</summary>
        public string Army;
        /// <summary>A <see cref="ReplayEventKind.March"/>: every army that moved or waited on a border.</summary>
        public List<ArmyRoute> Routes = new List<ArmyRoute>();
        /// <summary>A <see cref="ReplayEventKind.Battle"/>: every term of it, from the rules.</summary>
        public BattleRecord Battle;
        /// <summary>A treaty's term (<c>demand_occupied</c>, <c>cede_occupied</c>, <c>offer_white_peace</c>), offered or landed.</summary>
        public string Term;
        /// <summary>Whether a term landed without being asked, at a war score of 90 or more.</summary>
        public bool Enforced;
        /// <summary>A treaty: the provinces whose owner changed, and those returned to their owner (the hatching lifts).</summary>
        public List<string> Ceded = new List<string>();
        public List<string> Returned = new List<string>();

        /// <summary>A storyteller event: title, story, who it hit and each one's effect, the choices, its tale.</summary>
        public EventRecord Story;
        /// <summary>The scribe's decision that titled it (<c>+title-call</c>), or null. <see cref="Decision"/> is the storyteller's.</summary>
        public RecordedDecision TitleDecision;
        /// <summary>Each ruler's answer to a dilemma, by country: what it chose and said.</summary>
        public Dictionary<string, RecordedDecision> ChoiceDecisions = new Dictionary<string, RecordedDecision>(StringComparer.Ordinal);
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
        /// <summary>The writs turn: armies whose piece the events left away from where the rules put them; 0 when the replay tells it exactly.</summary>
        public int ArmyDrift;
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
        /// <summary>The map the game was played on: the Classic board, or a generated world (its <c>Source</c> carries the geometry).</summary>
        public MapDefinition Map;
        public GameRules Rules;
        /// <summary>The writs turn (rules v2, R1): armies as pieces, occupation, treaties.</summary>
        public bool Writs;
        public Dictionary<string, RecordedPlayer> Players;
        public ReplayState Initial;
        public List<ReplayTurn> Turns = new List<ReplayTurn>();
        public GameResult Result;
        /// <summary>True when the dev menu changed the game, or played it with stand-in rulers: never a measured game.</summary>
        public bool Dev;
        /// <summary>The storyteller's rules, or null for a game without one.</summary>
        public StoryRules Story;
        /// <summary>The game's tales as they stood when the build ended; follow one to a turn with its events' <c>Turn</c>.</summary>
        public List<Chain> Chains = new List<Chain>();
        /// <summary>For a partial build: why it stopped where it did, or null when it reached the end of what was recorded.</summary>
        public string StoppedBecause;
        public IEnumerable<ReplayEvent> AllEvents => Turns.SelectMany(t => t.Events);
        public int IndexOf(string provinceId)
        {
            for (int i = 0; i < ProvinceIds.Count; i++) if (ProvinceIds[i] == provinceId) return i;
            return -1;
        }
    }
}
