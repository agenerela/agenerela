using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>One thing a realm did in a turn, for the turn strip: a kind for its icon, the chronicle's words, its ruler's line.</summary>
    public sealed class TurnDeed
    {
        /// <summary>recruit, fortify, tax, war, peace, cede, demand, march, siege, hold, attack, army, refused, hidden.</summary>
        public string Kind;
        public string Text;
        /// <summary>What the ruler said with it, or null.</summary>
        public string Quote;
        /// <summary>The event it came from; its decision opens in the inspector. Null for a deed the fog hides.</summary>
        public ReplayEvent Event;
        /// <summary>A guard of the framework stopped or contained the decision behind it.</summary>
        public bool Guarded;
        /// <summary>For a guarded deed: which guard stopped what, and why, in plain words.</summary>
        public string GuardLine;
    }

    /// <summary>One realm's row of the strip: what it did this turn, grouped by kind for the icons.</summary>
    public sealed class TurnRow
    {
        public string Country;
        public int Turn;
        /// <summary>The realm you rule.</summary>
        public bool Mine;
        /// <summary>The persona that rules it ("the opportunist"), or null.</summary>
        public string Persona;
        public List<TurnDeed> Deeds = new List<TurnDeed>();
        /// <summary>The kinds in the order first done, with how many of each: the row's icons.</summary>
        public List<(string kind, int count)> Kinds => Deeds.GroupBy(d => d.Kind).Select(g => (g.Key, g.Count())).ToList();
        public bool Guarded => Deeds.Any(d => d.Guarded);
    }

    /// <summary>
    /// The turn strip's rows (#20, the lead's turn summary of 17 November): for each realm, what it did in a turn, from the
    /// turn's recorded events and their decisions only. The words are the chronicle's; whether a guard stopped a
    /// decision is the decision feed's record. Under fog a rival's court orders are news to every court, but an order to
    /// an army you cannot see is only counted, never told.
    /// </summary>
    public static class TurnSummary
    {
        /// <param name="countries">Every realm, in seat order: each gets a row, the viewer's first.</param>
        /// <param name="viewer">The realm you rule, or null while you watch (nothing is hidden).</param>
        /// <param name="armySeen">Under fog, whether one of a rival's army orders is in your sight; null: every one is.</param>
        public static List<TurnRow> For(int turn, IEnumerable<ReplayEvent> events, IEnumerable<string> countries, string viewer,
            Func<string, string> persona, Func<string, string> nameOf, Func<ReplayEvent, bool> armySeen = null)
        {
            var list = events.Where(e => e.Turn == turn).ToList();
            var rows = new List<TurnRow>();
            foreach (string c in countries)
            {
                var row = new TurnRow { Country = c, Turn = turn, Mine = c == viewer, Persona = persona?.Invoke(c) };
                foreach (var e in list.Where(e => e.Country == c))
                {
                    string kind = KindOf(e);
                    if (kind == null) continue;
                    bool hidden = viewer != null && c != viewer && e.Kind == ReplayEventKind.ArmyOrder && armySeen != null && !armySeen(e);
                    if (hidden) { row.Deeds.Add(new TurnDeed { Kind = "hidden", Text = "An order to an army beyond your sight" }); continue; }
                    var line = Chronicle.Line(e, nameOf);
                    var deed = new TurnDeed
                    {
                        Kind = kind, Event = e,
                        Text = line?.Text ?? e.Text,
                        Quote = string.IsNullOrWhiteSpace(e.Statement) ? null : e.Statement.Trim(),
                    };
                    var d = e.Decision;
                    if (d != null && (d.GuardsFired.Count > 0 || d.Verdict != null))
                    {
                        deed.Guarded = true;
                        deed.GuardLine = GuardLine(d);
                    }
                    row.Deeds.Add(deed);
                }
                rows.Add(row);
            }
            return rows.OrderBy(r => r.Mine ? 0 : 1).ToList();
        }

        /// <summary>The strip's kind for an event of a realm's own, or null for one that is not something it did.</summary>
        public static string KindOf(ReplayEvent e)
        {
            switch (e.Kind)
            {
                case ReplayEventKind.Recruit: return "recruit";
                case ReplayEventKind.Fort: return "fortify";
                case ReplayEventKind.Taxes: return "tax";
                case ReplayEventKind.WarDeclared: return "war";
                case ReplayEventKind.PeaceOffered: case ReplayEventKind.PeaceAccepted: case ReplayEventKind.PeaceMutual: return "peace";
                case ReplayEventKind.TermOffered:
                    return e.Term == Actions.CedeOccupied ? "cede" : e.Term == Actions.DemandOccupied ? "demand" : "peace";
                case ReplayEventKind.ArmyOrder: return e.Province != null ? "march" : "hold";
                case ReplayEventKind.Hold: return "hold";
                case ReplayEventKind.Capture: case ReplayEventKind.AttackFailed: return "attack";
                case ReplayEventKind.Siege: case ReplayEventKind.SiegeTaken: return "siege";
                case ReplayEventKind.ArmiesMerged: case ReplayEventKind.ArmySplit: return "army";
                case ReplayEventKind.Refused: return "refused";
                default: return null;
            }
        }

        /// <summary>
        /// "The legality guard stopped “attack orsk”: the target was not offered with this action. It held instead."
        /// The guard's name made plain, what the model wrote, the recorded reason; the framework's point, said once.
        /// </summary>
        public static string GuardLine(RecordedDecision d)
        {
            string guard = d.Verdict?.Guard ?? d.GuardsFired.FirstOrDefault() ?? "a guard";
            string plain = guard == "SchemaLegalityGuard" ? "The legality guard" : guard == "TargetGroundingGuard" ? "The grounding guard" : "The guard " + guard;
            string wrote = d.ModelAction == null ? null
                : d.ModelAction + (string.IsNullOrEmpty(d.ModelTarget) || d.ModelTarget == RecordedDecision.NoTarget ? "" : " " + d.ModelTarget);
            string reason = d.Verdict?.Reason;
            return plain + " stopped " + (wrote != null ? $"“{wrote}”" : "the model's answer") + (string.IsNullOrWhiteSpace(reason) ? "" : ": " + reason.Trim().TrimEnd('.')) + ". It held instead.";
        }
    }
}
