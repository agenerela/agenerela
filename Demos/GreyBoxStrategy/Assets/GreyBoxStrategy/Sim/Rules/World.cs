using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>How two countries stand. Every pair starts at peace.</summary>
    public enum Relation
    {
        Peace,
        War,
        /// <summary>Peace that cannot be broken by a declaration until it runs out.</summary>
        Truce,
    }

    /// <summary>A country's numbers. Only <see cref="Game"/> changes them during play.</summary>
    public sealed class Country
    {
        public Country(CountryInfo info)
        {
            Info = info;
        }

        public CountryInfo Info { get; }
        public string Id => Info.Id;
        public string Name => Info.Name;
        public int Gold { get; set; }
        public int Army { get; set; }
        public int Stability { get; set; }
        public bool Alive { get; set; } = true;
        /// <summary>The turn it lost its last province, or 0 while alive.</summary>
        public int EliminatedOnTurn { get; set; }
        /// <summary>Whether it raised taxes this turn, which stops stability recovering next upkeep.</summary>
        public bool RaisedTaxes { get; set; }

        public override string ToString() => Id;
    }

    /// <summary>A province's owner and defences. Only <see cref="Game"/> changes them during play.</summary>
    public sealed class Province
    {
        public Province(ProvinceInfo info)
        {
            Info = info;
        }

        public ProvinceInfo Info { get; }
        public string Id => Info.Id;
        public string Name => Info.Name;
        /// <summary>A country id, or null while neutral.</summary>
        public string Owner { get; set; }
        public bool IsNeutral => Owner == null;
        public int Forts { get; set; }
        /// <summary>The defenders of a neutral province. Zero once a country holds it.</summary>
        public int Garrison { get; set; }
        /// <summary>A capital is the start province marked * on the map, whoever holds it now.</summary>
        public bool IsCapital => Info.IsCapital;
        /// <summary>
        /// The writs turn: the country occupying it, or null while its owner controls it. Occupation
        /// never changes the owner; only a treaty does (v2 5.1).
        /// </summary>
        public string Occupier { get; set; }
        /// <summary>Who controls it now: the occupier, or else the owner (null for neutral land).</summary>
        public string Controller => Occupier ?? Owner;

        public override string ToString() => Id;
    }

    /// <summary>
    /// The whole state of a game: the map, every country's numbers, every province's owner and
    /// every pair's relation. Plain C#, no scene and no UnityEngine (DR-011).
    /// </summary>
    public sealed class World
    {
        private readonly Dictionary<string, Country> countries;
        private readonly Dictionary<string, Province> provinces;
        private readonly Dictionary<string, Relation> relations = new Dictionary<string, Relation>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> truceLeft = new Dictionary<string, int>(StringComparer.Ordinal);

        public World(MapDefinition map, GameRules rules, int seed)
        {
            Map = map;
            Rules = rules;
            Seed = seed;
            CountryList = map.Countries.Select(c => new Country(c)
            {
                Gold = c.StartGold,
                Army = c.StartArmy,
                Stability = rules.StartStability,
            }).ToList();
            countries = CountryList.ToDictionary(c => c.Id, StringComparer.Ordinal);
            ProvinceList = map.Provinces.Select(p => new Province(p)
            {
                Owner = p.StartOwner,
                Forts = p.StartForts,
                Garrison = p.StartOwner == null
                    ? Dice.Int(seed, 0, p.Id, "garrison", rules.NeutralGarrisonMin, rules.NeutralGarrisonMax + 1)
                    : 0,
            }).ToList();
            provinces = ProvinceList.ToDictionary(p => p.Id, StringComparer.Ordinal);
        }

        public MapDefinition Map { get; }
        public GameRules Rules { get; }
        public int Seed { get; }
        /// <summary>
        /// Every province's terrain, landmark and resource (<see cref="TraitCatalog"/>), drawn from the
        /// seed on first use. Only the storyteller's S2 events read them; nothing else in the rules does.
        /// </summary>
        public IReadOnlyDictionary<string, ProvinceTraits> Traits =>
            traits ?? (traits = TraitCatalog.Assign(Map, Seed));
        private Dictionary<string, ProvinceTraits> traits;

        /// <summary>The turn being played, 1-based; 0 before the first upkeep.</summary>
        public int Turn { get; set; }

        /// <summary>Every country, alive or not, in seat order.</summary>
        public IReadOnlyList<Country> CountryList { get; }
        public IReadOnlyList<Province> ProvinceList { get; }
        public IEnumerable<Country> Alive => CountryList.Where(c => c.Alive);

        public Country Country(string id) => id != null && countries.TryGetValue(id, out var c) ? c : null;
        public Province Province(string id) => id != null && provinces.TryGetValue(id, out var p) ? p : null;
        public bool IsCountry(string id) => Country(id) != null;
        public bool IsProvince(string id) => Province(id) != null;

        public IEnumerable<Province> ProvincesOf(string countryId) => ProvinceList.Where(p => p.Owner == countryId);
        public int ProvinceCount(string countryId) => ProvinceList.Count(p => p.Owner == countryId);

        /// <summary>
        /// Gold a turn. In the writs turn an occupied province gives its owner nothing and its
        /// occupier half (v2 5.1); otherwise every province owned gives the same.
        /// </summary>
        public int Income(Country c)
        {
            if (Rules.Turn != TurnStyle.Writs) return ProvinceCount(c.Id) * Rules.IncomePerProvince;
            int own = ProvinceList.Count(p => p.Owner == c.Id && p.Occupier == null);
            int occupied = ProvinceList.Count(p => p.Occupier == c.Id);
            return own * Rules.IncomePerProvince + occupied * Rules.IncomePerProvince / 2;
        }

        // --- The writs turn (rules v2, R1) -----------------------------------------------------

        /// <summary>Every army on the map, in the order they were raised.</summary>
        public List<Army> Armies { get; } = new List<Army>();
        /// <summary>Ids of armies destroyed or disbanded, never given to a new army, so a log or a memory keeps one name for one army.</summary>
        public HashSet<string> RetiredArmyIds { get; } = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Each war's record of battles, by the pair's key (the two ids in order).</summary>
        public Dictionary<string, WarRecord> Wars { get; } = new Dictionary<string, WarRecord>(StringComparer.Ordinal);

        public Army FindArmy(string id) => id == null ? null : Armies.FirstOrDefault(a => a.Id == id);
        public IEnumerable<Army> ArmiesOf(string countryId) => Armies.Where(a => a.Owner == countryId);
        public IEnumerable<Army> ArmiesAt(string provinceId) => Armies.Where(a => a.Location == provinceId);

        /// <summary>
        /// Writs this country has this turn: <see cref="GameRules.BaseWrits"/> + 1 per <see cref="GameRules.ProvincesPerWrit"/>
        /// provinces owned, at most <see cref="GameRules.MaxWrits"/> (v2 1.1), unless <see cref="GameRules.WritsOverride"/>
        /// sets this country's own number.
        /// </summary>
        public int Writs(string countryId) =>
            countryId != null && Rules.WritsOverride != null && Rules.WritsOverride.TryGetValue(countryId, out int own) ? System.Math.Max(1, own)
            : System.Math.Min(Rules.MaxWrits, Rules.BaseWrits + ProvinceCount(countryId) / Rules.ProvincesPerWrit);

        /// <summary>The record of the war between two countries, made when first asked.</summary>
        public WarRecord War(string a, string b)
        {
            string key = PairKey(a, b);
            if (!Wars.TryGetValue(key, out var w)) Wars[key] = w = new WarRecord(key);
            return w;
        }

        public static string PairKey(string a, string b) => Key(a, b);
        public int Upkeep(Country c) => c.Army / Rules.SoldiersPerUpkeepGold;

        /// <summary>How many soldiers a recruit would raise now: the levy, cut to what the treasury pays for.</summary>
        /// <remarks>In the writs turn an occupied province raises no one: the levy counts the land the country still controls.</remarks>
        public int Levy(Country c) =>
            System.Math.Min(Rules.LevyBase + Rules.LevyPerProvince * (Rules.Turn == TurnStyle.Writs
                    ? ProvinceList.Count(p => p.Owner == c.Id && p.Occupier == null) : ProvinceCount(c.Id)),
                c.Gold / Rules.GoldPerSoldier);

        public IEnumerable<Province> Neighbours(Province p) => Map.Neighbours(p.Id).Select(Province);

        /// <summary>Whether the province borders one held by this country (and is not its own).</summary>
        public bool Touches(string countryId, Province p) =>
            p.Owner != countryId && Neighbours(p).Any(n => n.Owner == countryId);

        /// <summary>A province of this country that borders a province it does not hold.</summary>
        public bool IsBorder(Province p) => p.Owner != null && Neighbours(p).Any(n => n.Owner != p.Owner);

        /// <summary>Whether any province of one borders any province of the other.</summary>
        public bool Borders(string a, string b) =>
            ProvinceList.Any(p => p.Owner == a && Neighbours(p).Any(n => n.Owner == b));

        /// <summary>
        /// One letter per province for the writs turn's logs: the owner's initial, upper case on a
        /// capital, '.' for neutral; an occupied province shows the occupier's initial after a '/'.
        /// Not used by the one-move game, whose log is unchanged.
        /// </summary>
        public string ControlSignature() =>
            string.Join(",", ProvinceList.Select(p => p.Id + "=" + (p.Owner ?? "-") + (p.Occupier != null ? "/" + p.Occupier : "")));

        public Relation RelationBetween(string a, string b) =>
            relations.TryGetValue(Key(a, b), out var r) ? r : Relation.Peace;

        public int TruceTurnsLeft(string a, string b) =>
            truceLeft.TryGetValue(Key(a, b), out var t) ? t : 0;

        public bool AtWar(string a, string b) => RelationBetween(a, b) == Relation.War;

        public IEnumerable<Country> EnemiesOf(string id) => Alive.Where(c => c.Id != id && AtWar(id, c.Id));

        public void SetRelation(string a, string b, Relation relation, int truceTurns = 0)
        {
            string key = Key(a, b);
            relations[key] = relation;
            if (relation == Relation.Truce) truceLeft[key] = truceTurns;
            else truceLeft.Remove(key);
        }

        /// <summary>Counts every truce down one turn. Returns the pairs whose truce just ended.</summary>
        public List<(string a, string b)> TickTruces()
        {
            var ended = new List<(string, string)>();
            foreach (string key in truceLeft.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList())
            {
                int left = truceLeft[key] - 1;
                if (left <= 0)
                {
                    truceLeft.Remove(key);
                    relations[key] = Relation.Peace;
                    var parts = key.Split('|');
                    ended.Add((parts[0], parts[1]));
                }
                else
                {
                    truceLeft[key] = left;
                }
            }
            return ended;
        }

        /// <summary>
        /// One letter per province, row by row: the owner's initial, upper case on a capital, or '.'
        /// for neutral. For the logs; the screen will draw the same thing in colour.
        /// </summary>
        public string OwnerGrid()
        {
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < Map.Rows; r++)
            {
                for (int c = 0; c < Map.Columns; c++)
                {
                    var p = ProvinceList.FirstOrDefault(x => x.Info.Column == c && x.Info.Row == r);
                    char ch = p == null ? ' ' : p.Owner == null ? '.' : char.ToLowerInvariant(p.Owner[0]);
                    if (p != null && p.Owner != null && p.IsCapital) ch = char.ToUpperInvariant(ch);
                    sb.Append(ch);
                    if (c + 1 < Map.Columns) sb.Append(' ');
                }
                if (r + 1 < Map.Rows) sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Owner per province id, for logs and for comparing two games.</summary>
        public string OwnerSignature() =>
            string.Join(",", ProvinceList.Select(p => p.Id + "=" + (p.Owner ?? "-")));

        private static string Key(string a, string b) =>
            string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
    }
}
