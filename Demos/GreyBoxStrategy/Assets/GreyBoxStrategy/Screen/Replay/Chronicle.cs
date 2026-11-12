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
        /// <summary>Room for the storyteller's events (research, decision 11); nothing produces it yet.</summary>
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

        /// <summary>Newest first; only the countries and categories switched on.</summary>
        public static List<ChronicleLine> Filter(IEnumerable<ChronicleLine> lines, ISet<string> countries, ISet<ChronicleCategory> categories) =>
            lines.Where(l => l != null && (l.Country == null || countries.Contains(l.Country)) && categories.Contains(l.Category))
                .Reverse().ToList();
    }
}
