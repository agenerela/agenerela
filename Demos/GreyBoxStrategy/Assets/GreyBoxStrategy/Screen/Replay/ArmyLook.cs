using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// R3 on screen, in words a player reads: an army's mix of pikes, riders and bows, the type it is mostly, its general
    /// and what the general's traits do, and what a recruit order raises. Every answer is null when the rules have no
    /// unit types or no generals, so the screen shows exactly what it showed before them.
    /// </summary>
    public static class ArmyLook
    {
        /// <summary>The type the army has most of (pikes before riders before bows on a tie), or null without types.</summary>
        public static UnitType? Largest(ArmyState a)
        {
            if (a == null || !a.HasMix) return null;
            var best = UnitType.Pikes;
            foreach (var t in Units.All) if (Count(a, t) > Count(a, best)) best = t;
            return best;
        }

        public static int Count(ArmyState a, UnitType t) => t == UnitType.Riders ? a.Riders : t == UnitType.Bows ? a.Bows : a.Pikes;

        /// <summary>"12 pikes, 8 riders": the exact mix, for your own armies; null without types.</summary>
        public static string Mix(ArmyState a) =>
            a == null || !a.HasMix ? null : string.Join(", ", Units.All.Where(t => Count(a, t) > 0).Select(t => $"{Count(a, t)} {Units.Name(t)}"));

        /// <summary>"mostly riders", or "mixed": what a rival's army looks like from your side (rules v2 3.8); null without types.</summary>
        public static string Mostly(ArmyState a) =>
            a == null || !a.HasMix ? null : Units.Mostly(new UnitMix { Pikes = a.Pikes, Riders = a.Riders, Bows = a.Bows });

        /// <summary>"Maren the Wary (+10% defending)": the general and what the traits do; null without generals.</summary>
        public static string Leader(ArmyState a) =>
            a?.General == null ? null : $"{ReplayBuilder.Title(a)} ({string.Join("; ", a.Traits.Select(Effect))})";

        /// <summary>What a trait does, in a few words.</summary>
        public static string Effect(string trait)
        {
            switch (trait)
            {
                case GeneralTraits.Bold: return "+10% attacking, +10% own losses";
                case GeneralTraits.Wary: return "+10% defending";
                case GeneralTraits.HorseBorn: return "riders +15%";
                case GeneralTraits.Bowmaster: return "bows +15%";
                case GeneralTraits.Swift: return "a march's first step a turn shorter";
                case GeneralTraits.Stubborn: return "never routed: retreats instead";
                case GeneralTraits.Inspiring: return "−20% own losses";
                case GeneralTraits.Siegewright: return "sieges a turn shorter";
                case GeneralTraits.Forager: return "its army eats half the stores";
                default: return trait;
            }
        }

        /// <summary>
        /// The lines a piece's tooltip adds under its usual line: the mix (yours) or what it mostly is (a rival's),
        /// and its general. Empty without types and generals.
        /// </summary>
        public static List<string> TooltipLines(ArmyState a, bool rival)
        {
            var lines = new List<string>();
            string mix = rival ? Mostly(a) : Mix(a);
            if (mix != null) lines.Add(rival ? Cap(mix) + "." : Cap(mix) + ".");
            string leader = Leader(a);
            if (leader != null) lines.Add("Led by " + leader + ".");
            // R3's supply: your own army's stores; a rival's are not yours to know.
            if (!rival && Stores(a) is string stores) lines.Add(stores);
            return lines;
        }

        /// <summary>"Stores: 3 of 4 turns." or the hungry warning; null without supply.</summary>
        public static string Stores(ArmyState a)
        {
            if (a == null || !a.HasStores) return null;
            if (a.Hungry) return "No stores: outside your own land it loses soldiers every turn, and it fights weaker. Bring it home to refill.";
            return $"Stores: {Turns(a.HalfStores)} of {Turns(a.MaxHalfStores)} turns; refilled in your own land.";
        }

        private static string Turns(int half) => half % 2 == 0 ? (half / 2).ToString() : $"{half / 2}½";

        /// <summary>"Siege 2 of 4": the tag on a besieged province; null when none is besieged there or the rules have no sieges.</summary>
        public static string SiegeTag(ReplayState s, int province)
        {
            if (s?.SiegedBy == null || province < 0 || province >= s.SiegedBy.Length || s.SiegedBy[province] == null) return null;
            int turns = s.SiegeTurns[province], needed = s.SiegeNeeded[province];
            return needed > 0 ? $"Siege {turns} of {needed}" : $"Siege, turn {turns}";
        }

        /// <summary>"Recruit pikes": a recruit order's name, or null for any other action.</summary>
        public static string RecruitName(string action) =>
            Actions.RecruitsType(action) is UnitType t ? "Recruit " + Units.Name(t) : null;

        /// <summary>"2 gold each · beat riders": what a recruit order costs and what its soldiers beat.</summary>
        public static string RecruitNote(GameRules rules, UnitType t) =>
            $"{rules.GoldPerSoldier * Units.CostFactor(rules, t)} gold each · beat {Units.Name(Units.Beats(t))}";

        /// <summary>The recruit order's tooltip: cost, how many now, what they beat and what beats them.</summary>
        public static string RecruitTooltip(GameRules rules, UnitType t, int now) =>
            $"Raise {Units.Name(t)} at your capital: {rules.GoldPerSoldier * Units.CostFactor(rules, t)} gold a soldier" +
            (t == UnitType.Riders ? ", and twice a pike's upkeep" : "") + $", {now} now. " +
            $"{Cap(Units.Name(t))} beat {Units.Name(Units.Beats(t))}; {Units.Name(Units.BeatenBy(t))} beat {Units.Name(t)}.";

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
