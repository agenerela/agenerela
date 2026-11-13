using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace GreyBoxStrategy
{
    /// <summary>One country as a map file writes it.</summary>
    public sealed class CountryData
    {
        public string Id;
        public string Name;
        /// <summary>Sent to a model ruler as its personality; the rules never read it.</summary>
        public string Personality;
        public int StartGold = 40;
        public int StartArmy = 30;
    }

    /// <summary>One province as a map file writes it.</summary>
    public sealed class ProvinceData
    {
        public string Id;
        public string Name;
        /// <summary>Layout for the logs' owner grid: column and row of the province on a coarse grid.</summary>
        public int Column;
        public int Row;
        /// <summary>A country id, or null for neutral land.</summary>
        public string Owner;
        public bool Capital;
        public int Forts;
        public Terrain Terrain;
        public bool Coastal;
        public bool Port;
        public int Population;
        public string Good;
        public string Region;
        /// <summary>A special place's template id, or null (details in <see cref="WorldMap.Specials"/>).</summary>
        public string Special;
    }

    /// <summary>A special place on the map: which template it carries, its name and its line of story.</summary>
    public sealed class SpecialPlace
    {
        public string Province;
        public string Template;
        public string Name;
        public string Story;
        /// <summary>Who chose it: <c>table</c> today, <c>model</c> once the Cartographer (R9) or the world-maker does.</summary>
        public string MadeBy;
    }

    /// <summary>What a generated world was made from, so the same world can be made again.</summary>
    public sealed class GenerationInfo
    {
        public int Seed;
        public int Countries;
        public MapSettings Settings;
        /// <summary>Which try passed (0 is the first): the world is made from the sub-seed of that try.</summary>
        public int Attempt;
        /// <summary>The share of provinces neutral at the start, in percent.</summary>
        public int NeutralPercent;
        /// <summary>Each region's naming style, by region id.</summary>
        public Dictionary<string, string> Styles = new Dictionary<string, string>();
    }

    /// <summary>A named cluster of about eight provinces whose names sound alike (v2 section 2.3, step 12).</summary>
    public sealed class RegionData
    {
        public string Id;
        public string Name;
        public List<string> Provinces = new List<string>();
    }

    /// <summary>
    /// A map as data: countries, provinces with their terrain and coast, borders, and, for a
    /// generated world, its regions, special places and geometry. Saved and loaded as JSON
    /// (<see cref="MapJson"/>); <see cref="ToDefinition"/> turns it into the rules' map.
    /// </summary>
    /// <remarks>
    /// Rules v2 slice R0: the map stopped being built in code. The Classic board is one of these
    /// (<see cref="ClassicMap"/>), and the generator (R2) writes the same shape.
    /// </remarks>
    public sealed class WorldMap
    {
        public const int CurrentFormat = 1;

        public int Format = CurrentFormat;
        /// <summary>The preset's name (<c>Classic</c>) or the generated world's.</summary>
        public string Name;
        /// <summary>In seat order.</summary>
        public List<CountryData> Countries = new List<CountryData>();
        public List<ProvinceData> Provinces = new List<ProvinceData>();
        /// <summary>Each border once, as a pair of province ids.</summary>
        public List<string[]> Borders = new List<string[]>();
        public List<RegionData> Regions = new List<RegionData>();
        public List<SpecialPlace> Specials = new List<SpecialPlace>();
        /// <summary>How a generated world was made; null for a preset.</summary>
        public GenerationInfo Generated;
        /// <summary>The shapes a screen draws from; null for a board drawn by hand, such as Classic.</summary>
        public MapGeometry Geometry;

        /// <summary>Why this map cannot be played, or null. Checks ids, owners, capitals and borders.</summary>
        public string WhyInvalid()
        {
            if (Format != CurrentFormat) return $"map format {Format} is not {CurrentFormat}";
            if (Countries == null || Countries.Count < 2) return "a map needs at least two countries";
            if (Provinces == null || Provinces.Count < Countries.Count) return "a map needs at least one province per country";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in Countries)
            {
                if (string.IsNullOrEmpty(c.Id)) return "a country has no id";
                if (!ids.Add(c.Id)) return $"the id {c.Id} is used twice";
            }
            foreach (var p in Provinces)
            {
                if (string.IsNullOrEmpty(p.Id)) return "a province has no id";
                if (!ids.Add(p.Id)) return $"the id {p.Id} is used twice";
                if (p.Owner != null && !Countries.Any(c => c.Id == p.Owner)) return $"{p.Id} is owned by unknown country {p.Owner}";
                if (p.Capital && p.Owner == null) return $"capital {p.Id} has no owner";
            }
            foreach (var c in Countries)
            {
                int capitals = Provinces.Count(p => p.Capital && p.Owner == c.Id);
                if (capitals != 1) return $"{c.Id} has {capitals} capitals, not one";
            }
            var provinceIds = new HashSet<string>(Provinces.Select(p => p.Id), StringComparer.Ordinal);
            foreach (var b in Borders ?? new List<string[]>())
            {
                if (b == null || b.Length != 2) return "a border is not a pair of province ids";
                if (!provinceIds.Contains(b[0]) || !provinceIds.Contains(b[1])) return $"border {b[0]}-{b[1]} names an unknown province";
                if (b[0] == b[1]) return $"border {b[0]}-{b[1]} joins a province to itself";
            }
            return null;
        }

        /// <summary>The rules' map. Throws <see cref="ArgumentException"/> with the reason on an invalid map.</summary>
        public MapDefinition ToDefinition()
        {
            string why = WhyInvalid();
            if (why != null) throw new ArgumentException($"The map '{Name}' cannot be played: {why}.");
            var countries = Countries.Select(c => new CountryInfo(c.Id, c.Name, c.Personality, c.StartGold, c.StartArmy)).ToList();
            var provinces = Provinces.Select(p => new ProvinceInfo(p.Id, p.Name, p.Column, p.Row, p.Owner, p.Capital, p.Forts,
                p.Terrain, p.Coastal, p.Port, p.Population, p.Good, p.Region, p.Special)).ToList();
            var borders = Borders.Select(b => (b[0], b[1]));
            return new MapDefinition(countries, provinces, borders) { Source = this };
        }

        public string ToJson(bool indented = true) => MapJson.Write(this, indented);

        /// <summary>Reads a map. Throws <see cref="FormatException"/> with the reason when it cannot.</summary>
        public static WorldMap FromJson(string json)
        {
            var map = MapJson.Read<WorldMap>(json, "map");
            string why = map.WhyInvalid();
            if (why != null) throw new FormatException($"The map cannot be played: {why}.");
            return map;
        }
    }

    /// <summary>The JSON form every map and setup file shares: camelCase names, snake_case enums, unknown names refused.</summary>
    public static class MapJson
    {
        private static JsonSerializerSettings Settings(bool strict) => new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
            {
                NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy(),
            },
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter(new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()) },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = strict ? MissingMemberHandling.Error : MissingMemberHandling.Ignore,
            Culture = System.Globalization.CultureInfo.InvariantCulture,
        };

        public static string Write(object value, bool indented) =>
            JsonConvert.SerializeObject(value, indented ? Formatting.Indented : Formatting.None, Settings(false));

        /// <summary>Reads a value, refusing unknown names and bad values with a message that says what was wrong.</summary>
        public static T Read<T>(string json, string what) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException($"The {what} is empty.");
            try
            {
                var value = JsonConvert.DeserializeObject<T>(json, Settings(true));
                if (value == null) throw new FormatException($"The {what} is empty.");
                return value;
            }
            catch (JsonException e)
            {
                throw new FormatException($"The {what} cannot be read: {e.Message}", e);
            }
        }
    }
}
