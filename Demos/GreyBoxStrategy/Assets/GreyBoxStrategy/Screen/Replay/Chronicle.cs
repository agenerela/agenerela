using System;
using System.Collections.Generic;
using System.Linq;

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
                    line.Text = $"{N(e.Country)} recruits {e.Text.Replace("recruited ", "")}";
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
            return line;
        }

        /// <summary>An army's name as the chronicle says it: "maren_host" is "Maren's host".</summary>
        public static string Host(string armyId)
        {
            if (string.IsNullOrEmpty(armyId)) return "an army";
            string general = armyId.EndsWith("_host") ? armyId.Substring(0, armyId.Length - 5) : armyId;
            return char.ToUpperInvariant(general[0]) + general.Substring(1) + "'s host";
        }

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
}
