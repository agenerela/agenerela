using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// Proposes names for a region's provinces. The tables (<see cref="TableNames"/>) do it today; the
    /// model does it in slice R10 (rules v2, section 2.9). Every proposal goes through
    /// <see cref="NameRules.WhyNot"/>, and one that fails falls back to the tables, so a maker can never
    /// break a world.
    /// </summary>
    public interface INameMaker
    {
        /// <summary>
        /// One proposed name per province of <paramref name="region"/>, in the order of
        /// <see cref="RegionData.Provinces"/>, or null to leave them all to the tables.
        /// </summary>
        IReadOnlyList<string> ProvinceNames(RegionData region, string style, WorldMap world);
    }

    /// <summary>The rules every id-name must pass (rules v2, section 2.9's checks).</summary>
    public static class NameRules
    {
        public const int MinLetters = 4;
        public const int MaxLetters = 11;

        /// <summary>Words a target id must never be: action ids, sentinels, group ids, words the facts use.</summary>
        public static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            "none", "notarget", "hold", "recruit", "fortify", "attack", "everyone", "theleader", "army", "armies",
            "peace", "war", "truce", "gold", "turn", "provinces", "province", "country", "countries", "realm",
            "plains", "forest", "hills", "marsh", "mountains", "grain", "horses", "timber", "cloth", "iron",
            "stone", "salt", "wine", "neutral", "capital", "region", "nobody", "self", "storyteller",
        };

        /// <summary>Real countries, capitals and well-known places: a generated world never borrows them.</summary>
        public static readonly HashSet<string> RealPlaces = new HashSet<string>(StringComparer.Ordinal)
        {
            "england", "scotland", "wales", "ireland", "france", "spain", "portugal", "italy", "germany", "austria",
            "poland", "russia", "ukraine", "sweden", "norway", "denmark", "finland", "iceland", "greece", "turkey",
            "egypt", "persia", "india", "china", "japan", "korea", "mongolia", "israel", "syria", "iraq", "iran",
            "canada", "mexico", "brazil", "chile", "peru", "texas", "london", "paris", "berlin", "rome", "madrid",
            "lisbon", "vienna", "moscow", "athens", "cairo", "tokyo", "delhi", "kyiv", "warsaw", "prague", "dublin",
            "oslo", "sparta", "troy", "carthage", "babylon", "jerusalem", "mecca", "venice", "florence", "naples",
            "milan", "geneva", "zurich", "boston", "york", "kent", "essex", "sussex", "cornwall", "devon", "dover",
            "inverness", "glasgow", "edinburgh", "oxford", "cambridge", "bristol", "hull", "bath", "durham",
            "gotham", "narnia", "gondor", "mordor", "rohan", "westeros", "tamriel", "skyrim", "hyrule", "azeroth",
        };

        /// <summary>Matched as substrings: a name containing one is refused.</summary>
        public static readonly string[] Unsuitable =
        {
            "fuck", "shit", "cunt", "piss", "cock", "dick", "twat", "wank", "slut", "whore", "nazi", "rape",
            "damn", "hell", "bitch", "bastard", "nigg", "fag", "kike", "spic", "chink", "tard", "anus", "penis",
            "vagina", "porn", "sex", "kill", "dead", "death",
        };

        /// <summary>
        /// Why <paramref name="id"/> cannot be a name here, or null. <paramref name="taken"/> are the ids
        /// already used; <paramref name="neighbours"/> the names of provinces bordering this one.
        /// </summary>
        public static string WhyNot(string id, ICollection<string> taken, IEnumerable<string> neighbours = null)
        {
            if (string.IsNullOrEmpty(id)) return "it is empty";
            foreach (char c in id)
                if (c < 'a' || c > 'z') return $"'{id}' has a character that is not a lowercase letter";
            if (id.Length < MinLetters || id.Length > MaxLetters) return $"'{id}' is not {MinLetters} to {MaxLetters} letters";
            if (Reserved.Contains(id)) return $"'{id}' is a reserved word";
            if (RealPlaces.Contains(id)) return $"'{id}' is a real place";
            foreach (string bad in Unsuitable)
                if (id.Contains(bad)) return $"'{id}' contains an unsuitable word";
            foreach (string other in taken)
            {
                if (other == id) return $"'{id}' is already used";
                if (other.StartsWith(id, StringComparison.Ordinal) || id.StartsWith(other, StringComparison.Ordinal))
                    return $"'{id}' and '{other}' are one a prefix of the other";
                if (WithinOne(id, other)) return $"'{id}' is one letter from '{other}'";
            }
            if (neighbours != null)
                foreach (string n in neighbours)
                    if (n != null && n.Length >= 3 && string.CompareOrdinal(n, 0, id, 0, 3) == 0)
                        return $"'{id}' and its neighbour '{n}' start with the same three letters";
            return null;
        }

        /// <summary>The display name of an id: the same word, capitalised (section 2.9).</summary>
        public static string Display(string id) => string.IsNullOrEmpty(id) ? id : char.ToUpperInvariant(id[0]) + id.Substring(1);

        // Levenshtein distance at most one.
        private static bool WithinOne(string a, string b)
        {
            int la = a.Length, lb = b.Length;
            if (Math.Abs(la - lb) > 1) return false;
            int i = 0, j = 0, edits = 0;
            while (i < la && j < lb)
            {
                if (a[i] == b[j]) { i++; j++; continue; }
                if (++edits > 1) return false;
                if (la > lb) i++;
                else if (lb > la) j++;
                else { i++; j++; }
            }
            edits += (la - i) + (lb - j);
            return edits <= 1;
        }
    }

    /// <summary>
    /// Names from syllable tables, one style a region, so a region's names sound alike (v2 section 2.3,
    /// step 12). The styles are the ones section 2.9 lets the model choose from.
    /// </summary>
    public sealed class TableNames : INameMaker
    {
        public static readonly string[] Styles =
        {
            "harsh_north", "soft_river", "old_imperial", "hill_clan", "marsh_folk", "coastal_trade", "old_forest", "open_steppe",
        };

        private static readonly Dictionary<string, (string[] start, string[] end)> Syllables = new Dictionary<string, (string[], string[])>
        {
            ["harsh_north"] = (new[] { "skar", "grim", "hald", "vor", "thrak", "kel", "brann", "jorv", "ulf", "dra", "hrot", "sva" },
                               new[] { "heim", "vik", "gard", "holt", "mark", "fell", "stad", "rik", "nes", "borg", "tun", "vald" }),
            ["soft_river"] = (new[] { "ella", "mira", "sel", "lun", "ava", "rhen", "ilo", "bel", "sava", "quel", "ondo", "lira" },
                              new[] { "wen", "mere", "dell", "ford", "lin", "ora", "nye", "vale", "ril", "ane", "sia", "meth" }),
            ["old_imperial"] = (new[] { "aure", "cass", "vel", "octa", "mar", "seren", "valer", "tib", "cor", "luc", "fla", "drus" },
                                new[] { "ium", "ia", "ona", "entum", "ara", "ica", "ola", "inus", "ano", "essa", "ova", "elum" }),
            ["hill_clan"] = (new[] { "dun", "glen", "craig", "bal", "invar", "kil", "tor", "ard", "ben", "car", "strath", "drum" },
                             new[] { "ach", "more", "lie", "dair", "rick", "neth", "wyn", "ross", "tarn", "ey", "ochy", "brae" }),
            ["marsh_folk"] = (new[] { "fen", "mud", "reed", "sal", "bog", "wil", "eel", "dim", "moss", "tarr", "slad", "rush" },
                              new[] { "wick", "ham", "lode", "sey", "by", "ditch", "mire", "ock", "wash", "holm", "fleet", "worth" }),
            ["coastal_trade"] = (new[] { "port", "sal", "cor", "tide", "mar", "bri", "sea", "ost", "gull", "har", "ancor", "pell" },
                                 new[] { "haven", "mouth", "quay", "strand", "bay", "wharf", "ness", "wick", "sund", "mere", "point", "hythe" }),
            ["old_forest"] = (new[] { "oak", "ash", "elm", "hollin", "bram", "fern", "yew", "thorn", "wold", "briar", "alder", "rowan" },
                              new[] { "wood", "hurst", "ley", "den", "shaw", "grove", "holt", "combe", "brook", "field", "glade", "stead" }),
            ["open_steppe"] = (new[] { "kar", "tash", "oyu", "bal", "sar", "ak", "kum", "tem", "ulan", "yar", "zhal", "orda" },
                               new[] { "kent", "tal", "dar", "bek", "sai", "ur", "an", "gol", "tepe", "ish", "yurt", "kala" }),
        };

        private static readonly string[] CountryStart =
        {
            "aldr", "bryn", "cael", "dorn", "esk", "fal", "garr", "hal", "ist", "jarn", "korv", "lom", "morr", "nar",
            "oth", "pell", "quor", "rav", "sev", "tor", "ulm", "verr", "wyr", "zan",
        };

        private static readonly string[] CountryEnd = { "ia", "or", "en", "ar", "is", "eth", "an", "ow", "und", "ane", "ost", "ir" };

        /// <summary>One line each, said to a model ruler as its country's personality (the Classic five first).</summary>
        public static readonly string[] Personalities =
        {
            "Proud and quick to take offence; believes strength earns respect.",
            "A merchant republic; prefers coin to blood, never forgets a betrayal.",
            "Small and devout; defends what it has and trusts no one.",
            "Hill clans under one banner; fierce in war, quick to make peace when it pays.",
            "Ambitious and patient; strikes whoever is weakest.",
            "A river kingdom of boatmen; trades first and fights only for its fords.",
            "Mountain lords who count every insult; slow to start a war and slower to end one.",
            "A young realm hungry for land; takes risks the old ones will not.",
            "Old and tired, proud of its past; wants peace but cannot bear to look weak.",
            "Riders of the open plains; strike fast and hate being cornered.",
        };

        private readonly int seed;

        public TableNames(int seed)
        {
            this.seed = seed;
        }

        /// <summary>The style a region speaks in, by its index.</summary>
        public string StyleFor(int regionIndex) => Styles[MapRandom.Int(seed, "region-style", regionIndex, Styles.Length)];

        public IReadOnlyList<string> ProvinceNames(RegionData region, string style, WorldMap world) => null;

        /// <summary>A valid name in <paramref name="style"/>, or null when none of the tables' combinations passes.</summary>
        public string Name(string style, string purpose, int index, ICollection<string> taken, IEnumerable<string> neighbours)
        {
            var (start, end) = Syllables.TryGetValue(style, out var s) ? s : Syllables[Styles[0]];
            var tried = neighbours?.ToList();
            int combos = start.Length * end.Length;
            int first = MapRandom.Int(seed, "name-" + purpose, index, combos);
            // A stride coprime with the table size visits every combination once.
            int stride = 7;
            while (Gcd(stride, combos) != 1) stride++;
            for (int k = 0; k < combos; k++)
            {
                int c = (first + k * stride) % combos;
                string name = start[c / end.Length] + end[c % end.Length];
                if (NameRules.WhyNot(name, taken, tried) == null) return name;
            }
            return null;
        }

        /// <summary>A country's id, from its own table.</summary>
        public string CountryName(int index, ICollection<string> taken)
        {
            int combos = CountryStart.Length * CountryEnd.Length;
            int first = MapRandom.Int(seed, "name-country", index, combos);
            for (int k = 0; k < combos; k++)
            {
                int c = (first + k * 11) % combos;
                string name = CountryStart[c / CountryEnd.Length] + CountryEnd[c % CountryEnd.Length];
                if (NameRules.WhyNot(name, taken) == null) return name;
            }
            return null;
        }

        private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
    }
}
