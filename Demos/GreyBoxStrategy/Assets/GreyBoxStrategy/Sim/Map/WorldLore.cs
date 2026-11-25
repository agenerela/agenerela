using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GreyBoxStrategy
{
    /// <summary>
    /// What a loremaster proposed for a generated world, as it wrote it: untrusted text, checked by
    /// <see cref="LoreRules.Check"/> before anything uses it. Kept as written in the world's cache, so a check that
    /// changes later judges the same words again.
    /// </summary>
    public sealed class LoreProposal
    {
        public string WorldName;
        /// <summary>One per country, in seat order; null or empty for none.</summary>
        public List<string> RealmNames = new List<string>();
        /// <summary>One line about each country's ruler, in seat order.</summary>
        public List<string> RulerLines = new List<string>();
        public string Backstory;
    }

    /// <summary>
    /// A generated world's names and story after the checks: what the world uses. A piece the checks refused is
    /// absent, and the world keeps what its seeded tables gave it (null world name: the map's own; no rename: the
    /// table name; null backstory or line: none).
    /// </summary>
    public sealed class WorldLore
    {
        public string WorldName;
        public string Backstory;
        /// <summary>Old country id to its new one, for each realm whose name was taken.</summary>
        public readonly Dictionary<string, string> Renames = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>A line about each ruler, by the country's id after the renames.</summary>
        public readonly Dictionary<string, string> RulerLines = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>What was refused and why, one phrase each: "realm 2's name 'Tideflow': ...".</summary>
        public readonly List<string> Refused = new List<string>();

        /// <summary>Whether anything at all was kept.</summary>
        public bool Any => WorldName != null || Backstory != null || Renames.Count > 0 || RulerLines.Count > 0;

        /// <summary>"world name, 5 of 5 realms, 5 lines, backstory; 0 refused".</summary>
        public string Summary(int realms) =>
            $"{(WorldName != null ? "world name" : "no world name")}, {Renames.Count} of {realms} realms renamed, {RulerLines.Count} ruler lines, " +
            $"{(Backstory != null ? "backstory" : "no backstory")}; {Refused.Count} refused" + (Refused.Count > 0 ? ": " + string.Join("; ", Refused) : "");
    }

    /// <summary>
    /// The checks a loremaster's proposal passes before a world takes it (#20, world lore): code decides what is
    /// kept, the model only proposes. Each piece is judged alone, so one bad name costs only that name.
    /// </summary>
    public static class LoreRules
    {
        /// <summary>The longest world name kept, in characters.</summary>
        public const int WorldNameLength = 24;
        /// <summary>The longest realm name the schema lets the model write; one longer than <see cref="NameRules.MaxLetters"/> is refused, not cut.</summary>
        public const int RealmNameLength = 20;
        /// <summary>The longest line about a ruler kept.</summary>
        public const int RulerLineLength = 140;
        /// <summary>The longest backstory kept; a longer one is cut back to its last whole sentence.</summary>
        public const int BackstoryLength = 480;
        /// <summary>The shortest backstory kept.</summary>
        public const int BackstoryMinimum = 40;

        // Prose may say "death" and "slain": the words a story must never hold are fewer than an id's.
        private static readonly string[] Words =
        {
            "fuck", "shit", "cunt", "piss", "cock", "dick", "twat", "wank", "slut", "whore", "nazi", "rape", "bitch",
            "bastard", "fag", "kike", "spic", "chink", "tard", "anus", "penis", "vagina", "porn", "sex",
        };

        private static readonly string[] Stems = { "fuck", "shit", "cunt", "nigg", "whore", "bitch", "wank", "slut", "twat", "nazi", "porn", "rapist", "bastard" };

        /// <summary>
        /// Which world this is, as the generator made it: its countries' table ids and its province count, the same on
        /// every machine for a seed and generator. A lore written for one world is never given to another.
        /// </summary>
        public static string WorldKey(WorldMap world)
        {
            string ids = string.Join(",", world.Countries.Select(c => c.Id)) + "|" + world.Provinces.Count;
            uint h = 2166136261;
            foreach (char c in ids) h = (h ^ c) * 16777619;
            return h.ToString("x8");
        }

        /// <summary>
        /// The lore <paramref name="proposal"/> gives <paramref name="world"/>: each realm name, the world's name, each
        /// ruler's line and the backstory, checked one by one. A refused realm name that the prose used is replaced
        /// there by the name the realm keeps. Never throws for what the model wrote; null gives an empty lore.
        /// </summary>
        public static WorldLore Check(WorldMap world, LoreProposal proposal)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var lore = new WorldLore();
            if (proposal == null) return lore;

            // Every id the world already uses, old country ids included until their realm is renamed.
            var taken = world.Provinces.Select(p => p.Id).Concat(world.Regions.Select(r => r.Id)).Concat(world.Countries.Select(c => c.Id)).ToList();
            var finalNames = new List<string>();
            var swaps = new List<(string From, string To)>();
            for (int i = 0; i < world.Countries.Count; i++)
            {
                var country = world.Countries[i];
                string proposed = At(proposal.RealmNames, i);
                string id = RealmId(proposed, out string why);
                if (id != null)
                {
                    taken.Remove(country.Id);
                    why = id == country.Id ? null : NameRules.WhyNot(id, taken);
                    if (why != null) taken.Add(country.Id);
                }
                if (id != null && why == null)
                {
                    taken.Add(id);
                    if (id != country.Id) lore.Renames[country.Id] = id;
                    finalNames.Add(NameRules.Display(id));
                }
                else
                {
                    finalNames.Add(country.Name ?? NameRules.Display(country.Id));
                    if (!string.IsNullOrWhiteSpace(proposed))
                    {
                        lore.Refused.Add($"realm {i + 1}'s name '{Short(proposed)}': {why}");
                        string said = Regex.Replace(proposed.Trim().Trim('"', '\'', '.', ','), @"\s+", " ");
                        if (said.Length > 0 && said.All(ch => char.IsLetter(ch) || ch == ' ' || ch == '-' || ch == '\'')) swaps.Add((said, finalNames[i]));
                    }
                }
            }
            // A refused name another realm took stays in the prose: it is that realm's.
            swaps.RemoveAll(sw => finalNames.Any(n => string.Equals(n, sw.From, StringComparison.OrdinalIgnoreCase)));

            var placeNames = new HashSet<string>(world.Provinces.Select(p => p.Name).Concat(world.Regions.Select(r => r.Name)).Concat(finalNames)
                .Where(n => n != null).Select(n => n.ToLowerInvariant()));
            lore.WorldName = WorldName(proposal.WorldName, placeNames, out string worldWhy);
            if (lore.WorldName == null && !string.IsNullOrWhiteSpace(proposal.WorldName)) lore.Refused.Add($"the world's name '{Short(proposal.WorldName)}': {worldWhy}");

            for (int i = 0; i < world.Countries.Count; i++)
            {
                string line = Prose(Swap(At(proposal.RulerLines, i), swaps), RulerLineLength, out string why);
                string id = lore.Renames.TryGetValue(world.Countries[i].Id, out var to) ? to : world.Countries[i].Id;
                if (line != null) lore.RulerLines[id] = line;
                else if (!string.IsNullOrWhiteSpace(At(proposal.RulerLines, i))) lore.Refused.Add($"realm {i + 1}'s ruler line: {why}");
            }

            string story = Prose(Swap(proposal.Backstory, swaps), BackstoryLength, out string storyWhy);
            if (story != null && story.Length < BackstoryMinimum) { storyWhy = $"shorter than {BackstoryMinimum} characters"; story = null; }
            lore.Backstory = story;
            if (story == null && !string.IsNullOrWhiteSpace(proposal.Backstory)) lore.Refused.Add("the backstory: " + storyWhy);
            return lore;
        }

        /// <summary>
        /// Gives <paramref name="world"/> its lore: the renames everywhere an id is kept, the world's name, the backstory
        /// and each ruler's line. Before the rules' map is made from it (<see cref="WorldMap.ToDefinition"/>).
        /// </summary>
        public static void Apply(WorldMap world, WorldLore lore)
        {
            if (world == null || lore == null) return;
            world.Rename(lore.Renames);
            if (lore.WorldName != null) world.Name = lore.WorldName;
            world.Backstory = lore.Backstory;
            foreach (var c in world.Countries) c.RulerLine = lore.RulerLines.TryGetValue(c.Id, out var line) ? line : null;
        }

        /// <summary>The id a proposed realm name gives (<c>"Velmora"</c> to <c>velmora</c>), or null with why not.</summary>
        public static string RealmId(string proposed, out string why)
        {
            why = null;
            if (string.IsNullOrWhiteSpace(proposed)) { why = "it is empty"; return null; }
            string s = proposed.Trim().Trim('"', '\'', '.', ',');
            foreach (char c in s)
            {
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z')) { why = "it is not one word of plain letters"; return null; }
            }
            string id = s.ToLowerInvariant();
            if (id.Length < NameRules.MinLetters || id.Length > NameRules.MaxLetters) { why = $"it is not {NameRules.MinLetters} to {NameRules.MaxLetters} letters"; return null; }
            return id;
        }

        /// <summary>A proposed world name, cleaned (<c>"the salt kingdoms"</c> to <c>"The Salt Kingdoms"</c>), or null with why not.</summary>
        public static string WorldName(string proposed, ICollection<string> placeNames, out string why)
        {
            why = null;
            if (string.IsNullOrWhiteSpace(proposed)) { why = "it is empty"; return null; }
            string s = Regex.Replace(proposed.Trim().Trim('"', '\'', '.', ','), @"\s+", " ").Replace('’', '\'');
            if (s.Length < 3 || s.Length > WorldNameLength) { why = $"it is not 3 to {WorldNameLength} characters"; return null; }
            foreach (char c in s)
            {
                if (!char.IsLetter(c) && c != ' ' && c != '\'' && c != '-') { why = "it has a character that is not a letter, a space, an apostrophe or a hyphen"; return null; }
            }
            var words = s.Split(' ');
            if (words.Length > 4) { why = "it is more than four words"; return null; }
            foreach (string w in words)
            {
                string lower = w.ToLowerInvariant().Trim('\'', '-');
                if (NameRules.RealPlaces.Contains(lower)) { why = $"'{w}' is a real place"; return null; }
                if (Unsuitable(lower)) { why = "it has an unsuitable word"; return null; }
            }
            if (placeNames != null && placeNames.Contains(s.ToLowerInvariant())) { why = "a realm, region or province has that name"; return null; }
            return string.Join(" ", words.Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w.Substring(1)));
        }

        /// <summary>
        /// Prose cleaned for the screen and the image prompts: one line, straight quotes, only letters, digits, spaces
        /// and ordinary punctuation, at most <paramref name="max"/> characters and cut back to its last whole sentence,
        /// ending in one; or null with why not.
        /// </summary>
        public static string Prose(string text, int max, out string why)
        {
            why = null;
            if (string.IsNullOrWhiteSpace(text)) { why = "it is empty"; return null; }
            string s = text.Replace('‘', '\'').Replace('’', '\'').Replace('“', '"').Replace('”', '"')
                .Replace('–', '-').Replace('—', '-');
            s = Regex.Replace(s, @"\s+", " ").Trim();
            if (s.Length > 1 && s[0] == '"' && s[s.Length - 1] == '"') s = s.Substring(1, s.Length - 2).Trim();
            foreach (char c in s)
            {
                if (!char.IsLetterOrDigit(c) && " .,;:'\"-!?()".IndexOf(c) < 0) { why = $"it has the character '{c}'"; return null; }
            }
            if (s.Length > max || !EndsSentence(s))
            {
                // Cut back to the last whole sentence that fits: a reply cut short ends mid-word.
                int end = -1;
                for (int i = Math.Min(s.Length, max) - 1; i >= 0; i--)
                {
                    if (".!?".IndexOf(s[i]) >= 0 && (i + 1 == s.Length || s[i + 1] == ' ' || s[i + 1] == '"')) { end = i + 1; break; }
                }
                if (end <= 0) { why = s.Length > max ? $"no whole sentence in its first {max} characters" : "it is not a whole sentence"; return null; }
                if (end < s.Length && s[end] == '"' && s.Take(end).Count(c => c == '"') % 2 == 1) end++;
                s = s.Substring(0, end).Trim();
            }
            foreach (string w in Regex.Split(s.ToLowerInvariant(), @"[^a-z]+"))
            {
                if (w.Length > 0 && Unsuitable(w)) { why = "it has an unsuitable word"; return null; }
            }
            return s;
        }

        private static bool EndsSentence(string s)
        {
            string t = s.TrimEnd('"', '\'', ')');
            return t.Length > 0 && ".!?".IndexOf(t[t.Length - 1]) >= 0;
        }

        private static bool Unsuitable(string word)
        {
            if (Words.Contains(word)) return true;
            foreach (string stem in Stems)
                if (word.StartsWith(stem, StringComparison.Ordinal)) return true;
            return false;
        }

        // Each refused name the prose used, as a whole word in any case, replaced by the name the realm keeps.
        private static string Swap(string text, List<(string From, string To)> swaps)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (var (from, to) in swaps)
                text = Regex.Replace(text, @"\b" + Regex.Escape(from) + @"\b", to, RegexOptions.IgnoreCase);
            return text;
        }

        private static string At(List<string> list, int i) => list != null && i < list.Count ? list[i] : null;

        private static string Short(string s)
        {
            s = Regex.Replace(s ?? "", @"\s+", " ").Trim();
            return s.Length > 30 ? s.Substring(0, 30) + "..." : s;
        }
    }
}
