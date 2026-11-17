using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>One province as the map defines it: where it sits and who holds it at the start.</summary>
    public sealed class ProvinceInfo
    {
        public ProvinceInfo(string id, string name, int column, int row, string startOwner, bool isCapital, int startForts)
            : this(id, name, column, row, startOwner, isCapital, startForts, Terrain.Plains, false, false, 0, null, null, null)
        {
        }

        public ProvinceInfo(string id, string name, int column, int row, string startOwner, bool isCapital, int startForts,
            Terrain terrain, bool isCoastal, bool hasPort, int population, string good, string region, string special)
        {
            Id = id;
            Name = name;
            Column = column;
            Row = row;
            StartOwner = startOwner;
            IsCapital = isCapital;
            StartForts = startForts;
            Terrain = terrain;
            IsCoastal = isCoastal;
            HasPort = hasPort;
            Population = population;
            Good = good;
            Region = region;
            Special = special;
        }

        /// <summary>The id a move names, unique across provinces and countries.</summary>
        public string Id { get; }
        public string Name { get; }
        /// <summary>Layout only, for the screen: the map's rules read <see cref="MapDefinition.Neighbours"/>.</summary>
        public int Column { get; }
        public int Row { get; }
        /// <summary>A country id, or null for a neutral province.</summary>
        public string StartOwner { get; }
        public bool IsCapital { get; }
        public int StartForts { get; }

        // --- Rules v2 (R0): carried by every map, read by no rule yet --------------------------
        /// <summary>The land, for the terrain rules R3 adds. Today's game never reads it.</summary>
        public Terrain Terrain { get; }
        /// <summary>Touches the sea or an inland sea.</summary>
        public bool IsCoastal { get; }
        public bool HasPort { get; }
        /// <summary>Population at the start (v2 section 4.1); 0 on a map that does not set it.</summary>
        public int Population { get; }
        /// <summary>The trade good it makes (v2 section 4.5), or null.</summary>
        public string Good { get; }
        /// <summary>The id of the region it belongs to, or null.</summary>
        public string Region { get; }
        /// <summary>The special place's template id (<c>old_castle</c>...), or null.</summary>
        public string Special { get; }
    }

    /// <summary>One country as the game starts it.</summary>
    public sealed class CountryInfo
    {
        public CountryInfo(string id, string name, string personality, int startGold, int startArmy)
        {
            Id = id;
            Name = name;
            Personality = personality;
            StartGold = startGold;
            StartArmy = startArmy;
        }

        public string Id { get; }
        public string Name { get; }
        /// <summary>Stage 2 sends this as the agent's personality. The simulation never reads it.</summary>
        public string Personality { get; }
        public int StartGold { get; }
        public int StartArmy { get; }
    }

    /// <summary>
    /// The map: provinces, which ones border which, and the countries that start on it. A graph,
    /// so the screen can colour each province by owner and draw its borders from the same data.
    /// </summary>
    public sealed class MapDefinition
    {
        private readonly Dictionary<string, ProvinceInfo> byId;
        private readonly Dictionary<string, string[]> neighbours;

        public MapDefinition(IReadOnlyList<CountryInfo> countries, IReadOnlyList<ProvinceInfo> provinces,
            IEnumerable<(string a, string b)> borders)
        {
            Countries = countries;
            Provinces = provinces;
            byId = provinces.ToDictionary(p => p.Id, StringComparer.Ordinal);

            var sets = provinces.ToDictionary(p => p.Id, _ => new SortedSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
            foreach (var (a, b) in borders)
            {
                if (!sets.ContainsKey(a) || !sets.ContainsKey(b) || a == b)
                    throw new ArgumentException($"Border {a}-{b} names an unknown province.");
                sets[a].Add(b);
                sets[b].Add(a);
            }
            neighbours = sets.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in countries)
                if (!ids.Add(c.Id)) throw new ArgumentException($"Duplicate id {c.Id}.");
            foreach (var p in provinces)
                if (!ids.Add(p.Id)) throw new ArgumentException($"Duplicate id {p.Id}.");
        }

        /// <summary>The data this map was built from (<see cref="WorldMap.ToDefinition"/>), or null when built in code.</summary>
        public WorldMap Source { get; internal set; }

        /// <summary>In seat order, which is also the order the log lists them.</summary>
        public IReadOnlyList<CountryInfo> Countries { get; }
        public IReadOnlyList<ProvinceInfo> Provinces { get; }
        public int Columns => Provinces.Max(p => p.Column) + 1;
        public int Rows => Provinces.Max(p => p.Row) + 1;

        public ProvinceInfo Province(string id) => byId.TryGetValue(id, out var p) ? p : null;

        /// <summary>The provinces bordering this one, in id order.</summary>
        public IReadOnlyList<string> Neighbours(string provinceId) =>
            neighbours.TryGetValue(provinceId, out var n) ? n : Array.Empty<string>();

        public bool AreNeighbours(string a, string b) => Neighbours(a).Contains(b);

        /// <summary>
        /// The standard map: the <b>Classic</b> preset, today's 25-province board, loaded from its data
        /// (<see cref="ClassicMap"/>). Four countries hold a corner each, Merrow holds the middle
        /// column's centre, and ten neutral provinces lie between them.
        /// </summary>
        /// <remarks>
        /// <code>
        ///          col 0        col 1         col 2         col 3        col 4
        /// row 0  highmoor*V   ashby V      stonereach .  corwick O    orskhaven*O
        /// row 1  brindle V    fenmark .    templegate M  saltmere .   redcliff O
        /// row 2  westmarch .  harrow .     merrowdeep*M  glimmerford . eastwatch .
        /// row 3  thornbury K  ravenholt .  pilgrimsrest M duskvale .  ilmoor I
        /// row 4  keshtar*K    marrowfield K southgate .  oakhollow I  ilvarion*I
        /// </code>
        /// Borders are the four orthogonal neighbours. * is a capital. Until R0 this board was built
        /// here in code; a test keeps that code and checks the data gives the same map in every field.
        /// </remarks>
        public static MapDefinition Standard() => ClassicMap.Load().ToDefinition();
    }
}
