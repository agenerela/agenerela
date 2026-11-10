using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>One province as the map defines it: where it sits and who holds it at the start.</summary>
    public sealed class ProvinceInfo
    {
        public ProvinceInfo(string id, string name, int column, int row, string startOwner, bool isCapital, int startForts)
        {
            Id = id;
            Name = name;
            Column = column;
            Row = row;
            StartOwner = startOwner;
            IsCapital = isCapital;
            StartForts = startForts;
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
        /// The standard map: a 5 × 5 board of 25 provinces. Four countries hold a corner each, Merrow
        /// holds the middle column's centre, and ten neutral provinces lie between them, so the
        /// first turns are a land grab and the map starts filling with colour from turn 1.
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
        /// Borders are the four orthogonal neighbours. * is a capital. The board is symmetric
        /// left to right and top to bottom, so no corner starts better than another.
        /// </remarks>
        public static MapDefinition Standard()
        {
            var countries = new[]
            {
                new CountryInfo("valen", "Valen", "Proud and quick to take offence; believes strength earns respect.", 40, 30),
                new CountryInfo("orsk", "Orsk", "A merchant republic; prefers coin to blood, never forgets a betrayal.", 40, 30),
                new CountryInfo("merrow", "Merrow", "Small and devout; defends what it has and trusts no one.", 40, 30),
                new CountryInfo("kesh", "Kesh", "Hill clans under one banner; fierce in war, quick to make peace when it pays.", 40, 30),
                new CountryInfo("ilvar", "Ilvar", "Ambitious and patient; strikes whoever is weakest.", 40, 30),
            };

            string[,] ids =
            {
                { "highmoor", "ashby", "stonereach", "corwick", "orskhaven" },
                { "brindle", "fenmark", "templegate", "saltmere", "redcliff" },
                { "westmarch", "harrow", "merrowdeep", "glimmerford", "eastwatch" },
                { "thornbury", "ravenholt", "pilgrimsrest", "duskvale", "ilmoor" },
                { "keshtar", "marrowfield", "southgate", "oakhollow", "ilvarion" },
            };
            string[,] names =
            {
                { "Highmoor", "Ashby", "Stonereach", "Corwick", "Orskhaven" },
                { "Brindle", "Fenmark", "Templegate", "Saltmere", "Redcliff" },
                { "Westmarch", "Harrow", "Merrowdeep", "Glimmerford", "Eastwatch" },
                { "Thornbury", "Ravenholt", "Pilgrim's Rest", "Duskvale", "Ilmoor" },
                { "Keshtar", "Marrowfield", "Southgate", "Oakhollow", "Ilvarion" },
            };
            var owners = new Dictionary<string, string>
            {
                ["highmoor"] = "valen", ["ashby"] = "valen", ["brindle"] = "valen",
                ["orskhaven"] = "orsk", ["corwick"] = "orsk", ["redcliff"] = "orsk",
                ["merrowdeep"] = "merrow", ["templegate"] = "merrow", ["pilgrimsrest"] = "merrow",
                ["keshtar"] = "kesh", ["thornbury"] = "kesh", ["marrowfield"] = "kesh",
                ["ilvarion"] = "ilvar", ["ilmoor"] = "ilvar", ["oakhollow"] = "ilvar",
            };
            var capitals = new HashSet<string> { "highmoor", "orskhaven", "merrowdeep", "keshtar", "ilvarion" };

            var provinces = new List<ProvinceInfo>();
            for (int r = 0; r < 5; r++)
            for (int c = 0; c < 5; c++)
            {
                string id = ids[r, c];
                owners.TryGetValue(id, out string owner);
                bool capital = capitals.Contains(id);
                provinces.Add(new ProvinceInfo(id, names[r, c], c, r, owner, capital, capital ? 1 : 0));
            }

            var borders = new List<(string, string)>();
            for (int r = 0; r < 5; r++)
            for (int c = 0; c < 5; c++)
            {
                if (c + 1 < 5) borders.Add((ids[r, c], ids[r, c + 1]));
                if (r + 1 < 5) borders.Add((ids[r, c], ids[r + 1, c]));
            }

            return new MapDefinition(countries, provinces, borders);
        }
    }
}
