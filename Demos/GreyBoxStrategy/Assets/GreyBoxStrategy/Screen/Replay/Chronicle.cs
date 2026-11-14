using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GreyBoxStrategy.Replay
{
    /// <summary>The chronicle's filter chips: what kind of line an event makes.</summary>
    public enum ChronicleCategory
    {
        Land,
        War,
        Peace,
        Building,
        Economy,
        Refused,
        Guard,
        /// <summary>The storyteller's events, and the dev menu's changes.</summary>
        Story,
    }

    /// <summary>One line of the chronicle.</summary>
    public sealed class ChronicleLine
    {
        public ReplayEvent Event;
        public int Turn => Event.Turn;
        public string Country => Event.Country;
        public ChronicleCategory Category;
        /// <summary>The line the screen shows, written from what happened, never from the statement.</summary>
        public string Text;
        /// <summary>The ruler's words under it, or empty.</summary>
        public string Quote;
        /// <summary>The line without its country's name in front ("fortifies Glimmerford"), for a ruler's orders grouped under its name.</summary>
        public string Short;
    }

    /// <summary>One ruler's orders of one turn, under one heading, with what it said once.</summary>
    public sealed class ChronicleGroup
    {
        public int Turn;
        public string Country;
        /// <summary>Newest first, as the chronicle shows lines.</summary>
        public List<ChronicleLine> Lines = new List<ChronicleLine>();
        /// <summary>The orders in words, oldest first, the same order given twice said once with "twice".</summary>
        public List<(string text, ChronicleLine line)> Items = new List<(string, ChronicleLine)>();
        /// <summary>What the ruler said, each different line once, oldest first.</summary>
        public List<string> Quotes = new List<string>();
        /// <summary>A lone line: drawn as itself, not as a group.</summary>
        public bool Single => Lines.Count == 1;
    }

    /// <summary>
    /// Turns replay events into chronicle lines and filters them. Plain C#, so it is tested
    /// without a screen.
    /// </summary>
    public static class Chronicle
    {
        /// <summary>Upkeep's quiet lines (income only) are left out; everything else makes a line.</summary>
        public static ChronicleLine Line(ReplayEvent e, Func<string, string> name)
        {
            string N(string id) => id == null ? "the neutrals" : name(id);
            string P(string id) => id == null ? "" : char.ToUpperInvariant(id[0]) + id.Substring(1);
            var line = new ChronicleLine { Event = e, Quote = e.Statement ?? "" };
            switch (e.Kind)
            {
                case ReplayEventKind.Capture:
                    line.Category = ChronicleCategory.Land;
                    line.Text = $"{N(e.Country)} takes {P(e.Province)} from {N(e.Other)} ({e.Attack} against {e.Defence})";
                    break;
                case ReplayEventKind.Revolt:
                    line.Category = ChronicleCategory.Land;
                    line.Text = $"{P(e.Province)} revolts against {N(e.Country)} and breaks away";
                    break;
                case ReplayEventKind.AttackFailed:
                    line.Category = ChronicleCategory.War;
                    line.Text = $"{N(e.Country)}'s attack on {P(e.Province)} fails ({e.Attack} against {e.Defence})";
                    break;
                case ReplayEventKind.WarDeclared:
                    line.Category = ChronicleCategory.War;
                    line.Text = $"{N(e.Country)} declares war on {N(e.Other)}";
                    break;
                case ReplayEventKind.Eliminated:
                    line.Category = ChronicleCategory.War;
                    line.Text = $"{N(e.Country)} has lost its last province and is out of the game";
                    break;
                case ReplayEventKind.PeaceOffered:
                    line.Category = ChronicleCategory.Peace;
                    line.Text = $"{N(e.Country)} offers {N(e.Other)} peace";
                    break;
                case ReplayEventKind.PeaceAccepted:
                    line.Category = ChronicleCategory.Peace;
                    line.Text = $"{N(e.Country)} accepts {N(e.Other)}'s peace: a truce for 5 turns";
                    break;
                case ReplayEventKind.PeaceRefused:
                    line.Category = ChronicleCategory.Peace;
                    line.Text = $"{N(e.Country)} refuses {N(e.Other)}'s peace";
                    break;
                case ReplayEventKind.PeaceMutual:
                    line.Category = ChronicleCategory.Peace;
                    line.Text = $"{N(e.Country)} and {N(e.Other)} both offer peace: a truce";
                    break;
                case ReplayEventKind.TruceEnded:
                    line.Category = ChronicleCategory.Peace;
                    line.Text = $"The truce between {N(e.Country)} and {N(e.Other)} ends";
                    break;
                case ReplayEventKind.Fort:
                    line.Category = ChronicleCategory.Building;
                    line.Text = $"{N(e.Country)} fortifies {P(e.Province)}";
                    break;
                case ReplayEventKind.Recruit:
                    line.Category = ChronicleCategory.Economy;
                    line.Text = $"{N(e.Country)} {RecruitText(e.Text, P)}";
                    break;
                case ReplayEventKind.Taxes:
                    line.Category = ChronicleCategory.Economy;
                    line.Text = $"{N(e.Country)} raises taxes";
                    break;
                case ReplayEventKind.Upkeep:
                    line.Category = ChronicleCategory.Economy;
                    int colon = e.Text.IndexOf(':');
                    line.Text = colon > 0 ? $"{N(e.Country)}: {e.Text.Substring(colon + 1).Trim()}" : e.Text;
                    break;
                case ReplayEventKind.Story:
                {
                    // The chronicler's voice: the title, then who it struck and how, in the game's words; the story is the quote.
                    var r = e.Story;
                    var t = EventRules.Template(r.Template);
                    string who = r.Hit.Count == 0 ? "" : string.Join(", ", r.Hit.Select(N));
                    string what = t?.NewsVerb ?? r.Template.Replace('_', ' ');
                    line.Category = ChronicleCategory.Story;
                    line.Text = $"“{r.Title}”: {who} {what}{(r.Province != null ? " in " + P(r.Province) : "")}"
                                + (r.Choices.Count > 0 ? $"; chose {string.Join(", ", r.Choices.Select(kv => $"{N(kv.Key)} {kv.Value.Replace('_', ' ')}"))}" : "");
                    line.Quote = r.Story ?? "";
                    break;
                }
                case ReplayEventKind.ArmyOrder:
                    line.Category = ChronicleCategory.War;
                    line.Text = e.Province != null ? $"{N(e.Country)}: {Host(e.Army)} marches on {P(e.Province)}" : $"{N(e.Country)}: {Host(e.Army)} holds";
                    break;
                case ReplayEventKind.March:
                    return null; // the map shows it
                case ReplayEventKind.Battle:
                {
                    var b = e.Battle;
                    string where = b.Province != null ? P(b.Province) : $"the {P(b.Border?[0])}–{P(b.Border?[1])} border";
                    string them = b.Defender == null ? "the garrison" : b.DefenderKind == "militia" ? N(b.Defender) + "'s militia" : N(b.Defender);
                    line.Category = ChronicleCategory.War;
                    line.Text = $"Battle at {where}: {N(b.Attacker)} {b.AttackerSoldiers} against {them} {b.DefenderSoldiers}; "
                                + (b.AttackerWon ? $"{N(b.Attacker)} won" : $"{them} held") + (b.Routed ? ", the loser routed" : "")
                                + $" ({b.AttackerLost} and {b.DefenderLost} lost)";
                    break;
                }
                case ReplayEventKind.ArmyLost:
                    line.Category = ChronicleCategory.War;
                    line.Text = $"{Host(e.Army)} of {N(e.Country)} " + (e.Text.Contains("routed") ? "is routed" : e.Text.Contains("disbanded") ? "has nowhere to go and disbands" : "is destroyed");
                    break;
                case ReplayEventKind.Occupy:
                    line.Category = ChronicleCategory.Land;
                    line.Text = $"{N(e.Country)} occupies {P(e.Province)}, {N(e.Other)}'s";
                    break;
                case ReplayEventKind.Free:
                    line.Category = ChronicleCategory.Land;
                    line.Text = $"{N(e.Country)} frees {P(e.Province)} from {N(e.Other)}";
                    break;
                case ReplayEventKind.TakeNeutral:
                    line.Category = ChronicleCategory.Land;
                    line.Text = $"{N(e.Country)} takes {P(e.Province)}, neutral land" + (e.Attack > 0 ? $" ({e.Attack} against {e.Defence})" : "");
                    break;
                case ReplayEventKind.TermOffered:
                    line.Category = ChronicleCategory.Peace;
                    line.Text = $"{N(e.Country)} puts a term to {N(e.Other)}: {TermText(e.Term, e.Country, e.Other, N)}";
                    break;
                case ReplayEventKind.TermAccepted:
                    line.Category = ChronicleCategory.Peace;
                    line.Text = $"{N(e.Country)} accepts {N(e.Other)}'s term";
                    break;
                case ReplayEventKind.TermRefused:
                    line.Category = ChronicleCategory.Peace;
                    line.Text = $"{N(e.Country)} refuses {N(e.Other)}'s term";
                    break;
                case ReplayEventKind.Treaty:
                    line.Category = ChronicleCategory.Peace;
                    line.Text = $"{(e.Enforced ? "Enforced treaty" : "Treaty")} between {N(e.Country)} and {N(e.Other)}: {TermText(e.Term, e.Country, e.Other, N)}"
                                + (e.Ceded.Count > 0 ? $" ({string.Join(", ", e.Ceded.Select(P))})" : "") + "; a truce";
                    break;
                case ReplayEventKind.Broken:
                    line.Category = ChronicleCategory.War;
                    line.Text = $"{N(e.Country)}'s realm is broken: its occupiers keep what they hold";
                    break;
                case ReplayEventKind.Stalemate:
                    line.Category = ChronicleCategory.War;
                    line.Text = "No province has changed hands for many turns: a stalemate ends the game";
                    break;
                case ReplayEventKind.Dev:
                    line.Category = ChronicleCategory.Story;
                    line.Text = "Dev menu: " + e.Text;
                    break;
                case ReplayEventKind.Refused:
                    line.Category = ChronicleCategory.Refused;
                    line.Text = $"{N(e.Country)}'s move could not happen: {e.Text.Replace("refused: ", "")}";
                    break;
                case ReplayEventKind.Hold:
                    bool guarded = e.Decision != null && e.Decision.GuardsFired.Count > 0;
                    line.Category = guarded ? ChronicleCategory.Guard : ChronicleCategory.Economy;
                    line.Text = guarded ? $"{N(e.Country)} holds: a guard stopped its move" : $"{N(e.Country)} holds";
                    break;
                default:
                    return null;
            }
            string speaker = N(e.Country);
            line.Short = e.Country != null && line.Text.StartsWith(speaker + ": ", StringComparison.Ordinal) ? line.Text.Substring(speaker.Length + 2)
                       : e.Country != null && line.Text.StartsWith(speaker + " ", StringComparison.Ordinal) ? line.Text.Substring(speaker.Length + 1)
                       : line.Text;
            return line;
        }

        private static readonly Regex Raised = new Regex(@"^raised (\d+) soldiers at (\w+)(?: \((.*)\))?");
        private static readonly Regex JoinedArmy = new Regex(@"^(new )?(\w+?_host\d*) \+?(\d+)$");

        /// <summary>
        /// A recruit in words, without the ids the rules write: "raised 15 soldiers at orskhaven (fenna_host +15)" is
        /// "raises 15 soldiers at Orskhaven for Fenna's host"; the one-move game's "recruited 10 soldiers for 50 gold"
        /// is "recruits 10 soldiers for 50 gold".
        /// </summary>
        public static string RecruitText(string text, Func<string, string> province)
        {
            text = text ?? "";
            if (text.StartsWith("recruited ", StringComparison.Ordinal)) return "recruits " + text.Substring("recruited ".Length);
            var m = Raised.Match(text);
            if (!m.Success) return "recruits " + text;
            string at = $"raises {m.Groups[1].Value} soldiers at {province(m.Groups[2].Value)}";
            if (!m.Groups[3].Success) return at;
            var parts = m.Groups[3].Value.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => JoinedArmy.Match(p.Trim())).Where(x => x.Success).ToList();
            if (parts.Count == 1)
                return parts[0].Groups[1].Success ? $"{at}: a new army, {Host(parts[0].Groups[2].Value)}" : $"{at} for {Host(parts[0].Groups[2].Value)}";
            var said = parts.Select(x => x.Groups[1].Success ? $"a new army, {Host(x.Groups[2].Value)}, of {x.Groups[3].Value}" : $"{x.Groups[3].Value} for {Host(x.Groups[2].Value)}").ToList();
            return parts.Count == 0 ? at : $"{at}: " + string.Join(" and ", said);
        }

        // The rules number a general whose name is taken again: "tolly2_host" (and allow "tolly_host2").
        private static readonly Regex HostId = new Regex(@"^([A-Za-z_]+?)(\d*)_host(\d*)$");
        private static readonly string[] Ordinals = { "", "", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth" };

        /// <summary>An army's name as the chronicle says it: "maren_host" is "Maren's host", "maren2_host" "Maren's second host".</summary>
        public static string Host(string armyId)
        {
            if (string.IsNullOrEmpty(armyId)) return "an army";
            var m = HostId.Match(armyId);
            string general = m.Success ? m.Groups[1].Value : armyId;
            string name = char.ToUpperInvariant(general[0]) + general.Substring(1).Replace('_', ' ') + "'s";
            string number = m.Success ? m.Groups[2].Value + m.Groups[3].Value : "";
            if (number == "") return name + " host";
            int n = int.Parse(number);
            return n < Ordinals.Length && n >= 2 ? $"{name} {Ordinals[n]} host" : $"{name} host {n}";
        }

        /// <summary>The kinds that are a ruler's own orders: grouped by ruler and turn in the chronicle.</summary>
        public static bool IsOrder(ReplayEventKind k) =>
            k == ReplayEventKind.Recruit || k == ReplayEventKind.Taxes || k == ReplayEventKind.Fort || k == ReplayEventKind.Hold
            || k == ReplayEventKind.ArmyOrder || k == ReplayEventKind.WarDeclared || k == ReplayEventKind.TermOffered || k == ReplayEventKind.Refused;

        /// <summary>
        /// Lines as the chronicle draws them (newest first), with a ruler's orders of one turn gathered under one
        /// heading where the first of them stands: on the writs turn a ruler gives several orders a turn, and each
        /// would otherwise make its own line with the same words under it. Battles, land and the storyteller stay lines.
        /// </summary>
        public static List<ChronicleGroup> Group(IReadOnlyList<ChronicleLine> newestFirst, string saidAgain = null)
        {
            var groups = new List<ChronicleGroup>();
            var open = new Dictionary<(int, string), ChronicleGroup>();
            foreach (var l in newestFirst)
            {
                if (l.Country != null && IsOrder(l.Event.Kind))
                {
                    if (!open.TryGetValue((l.Turn, l.Country), out var g))
                    {
                        g = new ChronicleGroup { Turn = l.Turn, Country = l.Country };
                        open[(l.Turn, l.Country)] = g;
                        groups.Add(g);
                    }
                    g.Lines.Add(l);
                }
                else groups.Add(new ChronicleGroup { Turn = l.Turn, Country = l.Country, Lines = { l } });
            }
            foreach (var g in groups.Where(g => !g.Single))
            {
                var oldest = Enumerable.Reverse(g.Lines).ToList();
                foreach (var same in oldest.GroupBy(l => l.Short ?? l.Text))
                {
                    int n = same.Count();
                    g.Items.Add((n == 1 ? same.Key : n == 2 ? same.Key + ", twice" : $"{same.Key}, {n} times", same.First()));
                }
                foreach (var l in oldest)
                    if (!string.IsNullOrWhiteSpace(l.Quote) && l.Quote != saidAgain && !g.Quotes.Any(q => Same(q, l.Quote))) g.Quotes.Add(l.Quote);
            }
            return groups;
        }

        private static bool Same(string a, string b) => string.Equals(a?.Trim().TrimEnd('.', '!'), b?.Trim().TrimEnd('.', '!'), StringComparison.OrdinalIgnoreCase);

        /// <summary>A treaty's term in words, from the proposer's side.</summary>
        public static string TermText(string term, string from, string to, Func<string, string> name)
        {
            switch (term)
            {
                case Actions.OfferWhitePeace: return "white peace, all occupied land returned";
                case Actions.DemandOccupied: return $"{name(to)} cedes what {name(from)} occupies";
                case Actions.CedeOccupied: return $"{name(from)} cedes what {name(to)} occupies";
                default: return term?.Replace('_', ' ') ?? "a term";
            }
        }

        /// <summary>Newest first; only the countries and categories switched on.</summary>
        public static List<ChronicleLine> Filter(IEnumerable<ChronicleLine> lines, ISet<string> countries, ISet<ChronicleCategory> categories) =>
            lines.Where(l => l != null && (l.Country == null || countries.Contains(l.Country)) && categories.Contains(l.Category))
                .Reverse().ToList();
    }

    /// <summary>
    /// The rules' reasons and lines in the screen's words: proper names and capitals, no ids, no underscores.
    /// "highmoor belongs to valen, at peace with us" is "Highmoor is Valen's, and we are at peace".
    /// </summary>
    public sealed class Wording
    {
        private readonly Func<string, string> name;
        private readonly HashSet<string> countries, provinces;

        public Wording(IEnumerable<string> countryIds, IEnumerable<string> provinceIds, Func<string, string> countryName)
        {
            countries = new HashSet<string>(countryIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            provinces = new HashSet<string>(provinceIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            name = countryName;
        }

        private static readonly Regex Army = new Regex(@"\b\w+?_host\d*\b");
        private static readonly Regex Word = new Regex(@"\b[a-z][a-z_]*\b");
        private static readonly Regex AtPeace = new Regex(@"^(\w+) belongs to (\w+), at peace with us$");
        private static readonly Regex TooFar = new Regex(@"^(\w+) is more than (\d+) steps away$");
        private static readonly Regex Already = new Regex(@"^\w+ is already at (\w+)$");

        /// <summary>A province's name: its id with a capital.</summary>
        public static string Province(string id) => string.IsNullOrEmpty(id) ? "" : char.ToUpperInvariant(id[0]) + id.Substring(1);

        /// <summary>Ids to names (armies, countries, provinces), actions to words, and a capital first.</summary>
        public string Plain(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var m = AtPeace.Match(text);
            if (m.Success && provinces.Contains(m.Groups[1].Value)) text = $"{m.Groups[1].Value} is {m.Groups[2].Value}'s, and we are at peace";
            text = Army.Replace(text, x => Chronicle.Host(x.Value));
            text = Word.Replace(text, x =>
                countries.Contains(x.Value) ? name(x.Value) : provinces.Contains(x.Value) ? Province(x.Value) : x.Value.Replace('_', ' '));
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>Why an army may not march to a province, said of the province: "Valen's, and we are at peace".</summary>
        public string Destination(string why)
        {
            if (string.IsNullOrEmpty(why)) return why;
            var m = AtPeace.Match(why);
            if (m.Success) return $"{name(m.Groups[2].Value)}'s, and we are at peace";
            if ((m = TooFar.Match(why)).Success) return $"more than {m.Groups[2].Value} steps away";
            if (Already.IsMatch(why)) return "the army is already there";
            return Plain(why);
        }
    }
}
