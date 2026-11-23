using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// What a merge or a split did, for the chronicle and the replay: the armies as they stand after it.
    /// </summary>
    public sealed class ArmyOrderRecord
    {
        public const string MergeKind = "merge", SplitKind = "split";

        public int Turn;
        /// <summary><see cref="MergeKind"/> or <see cref="SplitKind"/>.</summary>
        public string Kind;
        public string Country;
        public string Province;
        /// <summary>A merge: the army that stays, the senior general's. A split: the army split.</summary>
        public string Army;
        /// <summary>A merge: the armies that joined <see cref="Army"/> and are gone. A split: the new army alone.</summary>
        public List<string> Others = new List<string>();
        /// <summary>A split: the soldiers the new army took.</summary>
        public int Soldiers;
        /// <summary>A merge: each dismissed general's title, "Tolly the Bold"; empty with generals off or none to dismiss.</summary>
        public List<string> Dismissed = new List<string>();
        /// <summary>The line the chronicle tells.</summary>
        public string Text;
        /// <summary>The armies the order left standing, as they stand after it: the merged army, or both halves of a split.</summary>
        public List<ArmySnapshot> After = new List<ArmySnapshot>();

        public bool IsMerge => Kind == MergeKind;
    }

    /// <summary>
    /// Merging and splitting armies (#20, 16 November): orders of the human player's council, never offered to a model
    /// court, so nothing a model reads changes. Neither costs a writ. These functions hold the rules on a
    /// <see cref="World"/>; <see cref="Game.Merge"/> and <see cref="Game.Split"/> give them in the council.
    /// </summary>
    /// <remarks>
    /// A merge: two or more armies of one realm in one province, none marching, become the largest of them (ties: the
    /// lower id). Soldiers and each type add up, at most <see cref="GameRules.ArmyMax"/>; the half-stores are the
    /// soldier-weighted average, rounded down; the senior general leads (<see cref="Keeper"/>) and the others are dismissed. A siege's
    /// progress belongs to the province, so it carries on.
    /// A split: an army not marching gives 1 to size − 1 soldiers to a new army in the same province, each type in
    /// proportion (the remainder to the largest fractions, as losses are taken), the same stores level, and a general of
    /// its own, named as a newly raised army's is.
    /// </remarks>
    public static class ArmyOrders
    {
        /// <summary>Why these armies cannot merge now, or null when they can.</summary>
        public static string WhyNotMerge(World w, string countryId, IReadOnlyList<string> armyIds)
        {
            if (w.Rules.Turn != TurnStyle.Writs) return "armies merge only on the writs turn";
            var ids = (armyIds ?? new string[0]).Where(x => x != null).Distinct(StringComparer.Ordinal).ToList();
            if (ids.Count < 2) return "a merge needs two armies or more";
            var armies = new List<Army>();
            foreach (string id in ids)
            {
                var a = w.FindArmy(id);
                if (a == null || a.Owner != countryId) return $"{id} is not one of {countryId}'s armies";
                if (a.Marching) return $"{id} is marching";
                armies.Add(a);
            }
            if (armies.Select(a => a.Location).Distinct(StringComparer.Ordinal).Count() > 1) return "the armies do not stand in one province";
            int total = armies.Sum(a => a.Soldiers);
            if (total > w.Rules.ArmyMax) return $"together they are {total} soldiers, more than an army's {w.Rules.ArmyMax}";
            return null;
        }

        /// <summary>Why this army cannot give <paramref name="soldiers"/> to a new army now, or null when it can.</summary>
        public static string WhyNotSplit(World w, string countryId, string armyId, int soldiers)
        {
            if (w.Rules.Turn != TurnStyle.Writs) return "armies split only on the writs turn";
            var a = w.FindArmy(armyId);
            if (a == null || a.Owner != countryId) return $"{armyId} is not one of {countryId}'s armies";
            if (a.Marching) return $"{armyId} is marching";
            if (a.Soldiers < 2) return $"{armyId} has too few soldiers to split";
            if (soldiers < 1 || soldiers > a.Soldiers - 1) return $"a split takes 1 to {a.Soldiers - 1} of {armyId}'s {a.Soldiers} soldiers";
            return null;
        }

        /// <summary>
        /// The army a merge keeps: the one whose general has held command longest (<see cref="General.CommandSince"/>; an
        /// army with no general comes after any with one), then the larger, then the lower id. So a general split off
        /// and merged back never outranks the one who led before.
        /// </summary>
        public static Army Keeper(IEnumerable<Army> armies) =>
            armies.OrderBy(a => a.General == null ? 1 : 0).ThenBy(a => a.General?.CommandSince ?? 0)
                .ThenByDescending(a => a.Soldiers).ThenBy(a => a.Id, StringComparer.Ordinal).First();

        /// <summary>Merges the armies; <see cref="WhyNotMerge"/> must be null. Returns what happened.</summary>
        public static ArmyOrderRecord Merge(World w, string countryId, IReadOnlyList<string> armyIds)
        {
            string why = WhyNotMerge(w, countryId, armyIds);
            if (why != null) throw new InvalidOperationException(why);
            var armies = armyIds.Distinct(StringComparer.Ordinal).Select(w.FindArmy).ToList();
            var keep = Keeper(armies);
            var others = armies.Where(a => a != keep).OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
            var r = w.Rules;

            int total = armies.Sum(a => a.Soldiers);
            // Soldier-weighted, in whole half-turns, rounded down.
            long weighted = armies.Sum(a => (long)a.HalfStores * a.Soldiers);
            int halfStores = total > 0 ? (int)(weighted / total) : keep.HalfStores;
            foreach (var o in others)
            {
                foreach (var t in Units.All) keep.Add(t, o.Of(t));
                foreach (string reason in o.Wake) if (!keep.Wake.Contains(reason)) keep.Wake.Add(reason);
            }
            keep.HalfStores = halfStores;

            var record = new ArmyOrderRecord { Turn = w.Turn, Kind = ArmyOrderRecord.MergeKind, Country = countryId, Province = keep.Location, Army = keep.Id };
            foreach (var o in others)
            {
                record.Others.Add(o.Id);
                if (r.Generals && o.General != null) record.Dismissed.Add(o.General.Title);
                w.Armies.Remove(o);
                w.RetiredArmyIds.Add(o.Id);
            }
            string lead = r.Generals && keep.General != null ? $"; {keep.General.Title} leads" : "";
            string dismissed = record.Dismissed.Count == 0 ? ""
                : $"; {Join(record.Dismissed)} {(record.Dismissed.Count == 1 ? "is" : "are")} dismissed";
            record.Text = $"{countryId} merged {Join(others.Select(o => o.Id))} into {keep.Id} at {keep.Location}: {keep.Soldiers} soldiers{lead}{dismissed}.";
            record.After.Add(new ArmySnapshot(keep));
            return record;
        }

        /// <summary>Splits <paramref name="soldiers"/> off into a new army; <see cref="WhyNotSplit"/> must be null. Returns what happened.</summary>
        public static ArmyOrderRecord Split(World w, string countryId, string armyId, int soldiers)
        {
            string why = WhyNotSplit(w, countryId, armyId, soldiers);
            if (why != null) throw new InvalidOperationException(why);
            var a = w.FindArmy(armyId);
            var take = Share(a, soldiers);

            var fresh = new Army(ArmyNames.Next(w, countryId), countryId, a.Location, 0, w.Turn);
            // It holds where it stands, waiting for nothing: ordering it costs a writ, as recalling any holding army does.
            fresh.Wake.Clear();
            foreach (var t in Units.All) fresh.Add(t, take[(int)t]);
            fresh.HalfStores = a.HalfStores;
            if (w.Rules.Generals) fresh.General = GeneralTraits.For(w, fresh);

            // The original keeps the rest, type by type.
            var rest = new UnitMix { Pikes = a.Pikes - take[0], Riders = a.Riders - take[1], Bows = a.Bows - take[2] };
            a.Soldiers = 0;
            foreach (var t in Units.All) a.Add(t, rest.Of(t));
            w.Armies.Add(fresh);

            return new ArmyOrderRecord
            {
                Turn = w.Turn, Kind = ArmyOrderRecord.SplitKind, Country = countryId, Province = a.Location, Army = a.Id,
                Others = { fresh.Id }, Soldiers = soldiers,
                Text = $"{countryId} split {soldiers} soldiers from {a.Id} at {a.Location} as {fresh.Id}"
                       + (fresh.General != null ? $", led by {fresh.General.Title}" : "") + $"; {a.Id} keeps {a.Soldiers}.",
                After = { new ArmySnapshot(a), new ArmySnapshot(fresh) },
            };
        }

        /// <summary>
        /// How many of each type (pikes, riders, bows) <paramref name="soldiers"/> taken from the army are: each type in
        /// proportion, the remainder to the largest fractions, pikes first on ties.
        /// </summary>
        public static int[] Share(Army a, int soldiers)
        {
            var have = new[] { a.Pikes, a.Riders, a.Bows };
            int total = have.Sum();
            var now = new int[3];
            var frac = new double[3];
            for (int i = 0; i < 3; i++)
            {
                double exact = (double)soldiers * have[i] / total;
                now[i] = (int)Math.Floor(exact);
                frac[i] = exact - now[i];
            }
            int left = soldiers - now.Sum();
            foreach (int i in Enumerable.Range(0, 3).Where(i => now[i] < have[i]).OrderByDescending(i => frac[i]).ThenBy(i => i).Take(left)) now[i]++;
            return now;
        }

        private static string Join(IEnumerable<string> items)
        {
            var list = items.ToList();
            return list.Count <= 1 ? string.Join("", list) : string.Join(", ", list.Take(list.Count - 1)) + " and " + list.Last();
        }
    }
}
