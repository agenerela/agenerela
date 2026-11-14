using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// What a country sees of rival armies (rules v2 3.8, the lead's answer 12: "presence and rough size when it
    /// borders your land; nothing deeper in"), as rules data for <see cref="GameRules.Fog"/>. The screen has its
    /// own fog; this is what the rulers read.
    /// </summary>
    /// <remarks>
    /// A country sees its own land and every province bordering it, and every province one of its armies stands
    /// in and their borders. There a rival army shows its position, its general and its size rounded to 10;
    /// in forest only that an army is there; its mix of types only when one of our armies with riders stands in
    /// or beside that province. Always known: each rival's total soldiers, rounded to 10, and how many armies it
    /// has. Allies and watchtowers are not built.
    /// </remarks>
    public static class Fog
    {
        /// <summary>The provinces <paramref name="countryId"/> sees.</summary>
        public static HashSet<string> Seen(World w, string countryId)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var near = w.ProvinceList.Where(p => p.Controller == countryId).Select(p => p.Id)
                .Concat(w.ArmiesOf(countryId).Select(a => a.Location));
            foreach (string id in near)
            {
                seen.Add(id);
                foreach (string n in w.Map.Neighbours(id)) seen.Add(n);
            }
            return seen;
        }

        public static bool Sees(World w, string countryId, string provinceId) => Seen(w, countryId).Contains(provinceId);

        /// <summary>"about 40", rounded to the nearest 10; "a few" under 5.</summary>
        public static string About(int soldiers) =>
            soldiers < 5 ? "a few" : $"about {(int)Math.Round(soldiers / 10.0, MidpointRounding.AwayFromZero) * 10}";

        /// <summary>Whether one of our armies with riders stands in or beside the province: then we see a rival army's mix there.</summary>
        public static bool Scouts(World w, string countryId, string provinceId) =>
            w.Rules.UnitTypes && w.ArmiesOf(countryId).Any(a => a.Riders > 0 && (a.Location == provinceId || w.Map.AreNeighbours(a.Location, provinceId)));

        /// <summary>
        /// A rival army as <paramref name="countryId"/> sees it, or null when it stands where we do not see:
        /// "orsk's caspar_host, about 40, led by Caspar the Bold, at corwick"; in forest "an army of orsk's at
        /// corwick"; with scouting, its mix.
        /// </summary>
        public static string Sighting(World w, string countryId, Army a, ISet<string> seen = null)
        {
            seen = seen ?? Seen(w, countryId);
            if (a.Owner == countryId || !seen.Contains(a.Location)) return null;
            var p = w.Province(a.Location);
            if (p.Info.Terrain == Terrain.Forest) return $"an army of {a.Owner}'s at {a.Location}";
            string line = $"{a.Owner}'s {a.Id}, {About(a.Soldiers)}";
            if (w.Rules.Generals && a.General != null) line += $", led by {a.General.Title}";
            if (Scouts(w, countryId, a.Location)) line += $" ({Units.Mix(a)})";
            return line + $", at {a.Location}";
        }

        /// <summary>The rival armies we see, nearest our land first, at most <paramref name="max"/>.</summary>
        public static List<string> Sightings(World w, string countryId, int max)
        {
            var seen = Seen(w, countryId);
            var ours = new HashSet<string>(w.ProvinceList.Where(p => p.Controller == countryId).Select(p => p.Id), StringComparer.Ordinal);
            return w.Armies.Where(a => a.Owner != countryId && seen.Contains(a.Location))
                .OrderByDescending(a => w.AtWar(countryId, a.Owner))
                .ThenByDescending(a => ours.Contains(a.Location) ? 2 : w.Map.Neighbours(a.Location).Any(ours.Contains) ? 1 : 0)
                .ThenByDescending(a => a.Soldiers).ThenBy(a => a.Id, StringComparer.Ordinal)
                .Take(max).Select(a => Sighting(w, countryId, a, seen)).ToList();
        }

        /// <summary>A province's rival armies as a march order's detail: "enemy armies about 40", "an enemy army" in forest, null when unseen or none.</summary>
        public static string ArmiesThere(World w, string countryId, string provinceId)
        {
            var theirs = w.ArmiesAt(provinceId).Where(a => a.Owner != countryId).ToList();
            if (theirs.Count == 0 || !Sees(w, countryId, provinceId)) return null;
            if (w.Province(provinceId).Info.Terrain == Terrain.Forest) return "an enemy army";
            return "enemy armies " + About(theirs.Sum(a => a.Soldiers));
        }
    }
}
