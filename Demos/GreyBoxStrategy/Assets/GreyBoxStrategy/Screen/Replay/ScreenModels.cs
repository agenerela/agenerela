using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// The screen's numbers and lists, worked out from a state and nothing else, so tests can check them without a
    /// scene: the top bar's stats with their breakdowns, the outliner's rows, and which chronicle lines a feed filter
    /// keeps (the UI rework, variant B, #20).
    /// </summary>
    public static class ScreenModels
    {
        /// <summary>One number on the top bar: what it shows, a word under it, whether that word is good or bad, its tooltip.</summary>
        public sealed class Stat
        {
            public string Key, Value, Sub, Tip;
            /// <summary>+1 good, -1 bad, 0 neutral: the colour of <see cref="Sub"/>.</summary>
            public int Tone;
        }

        /// <param name="writs">This turn's writs and how many the plan spends, or null outside your council.</param>
        public static List<Stat> TopBar(ReplayState s, string me, int incomePerProvince, bool conquest, int winAt, (int all, int used)? writs,
            Func<string, string> nameOf, Func<string, string> provinceName)
        {
            var c = s.Countries[me];
            int n = s.ProvinceCount(me), net = c.Income - c.Upkeep;
            var mine = s.Armies.Where(a => a.Owner == me).ToList();
            var list = new List<Stat>
            {
                new Stat
                {
                    Key = "gold", Value = c.Gold.ToString(), Sub = $"{net:+0;-0;0} a turn", Tone = Math.Sign(net),
                    Tip = $"Treasury: {c.Gold} gold.\nIncome +{c.Income}: {n} {(n == 1 ? "province" : "provinces")} at {incomePerProvince} each.\n" +
                          $"Upkeep -{c.Upkeep}: {c.Army} soldiers, 1 gold for every 2.\nNet {net:+0;-0;0} a turn. Below zero, 15% of the army deserts.",
                },
                new Stat
                {
                    Key = "army", Value = c.Army.ToString(), Sub = $"{mine.Count} {(mine.Count == 1 ? "army" : "armies")}",
                    Tip = $"Soldiers: {c.Army}.\n" + (mine.Count == 0 ? "No armies: your land defends with its militia."
                        : string.Join("\n", mine.Select(a => $"{Chronicle.Host(a.Id)}: {a.Soldiers} at {provinceName(a.Location)}" + (a.Marching ? $", marching to {provinceName(a.Destination)}" : "")))),
                },
            };
            var held = Enumerable.Range(0, s.Owners.Length).Where(i => s.Owners[i] == me).ToList();
            list.Add(new Stat
            {
                Key = "land", Value = n.ToString(), Sub = $"of {(conquest ? s.Owners.Length : winAt)}",
                Tip = $"Provinces: {n}. " + (conquest ? "Conquest: the last realm standing wins." : $"{winAt} wins; {Math.Max(0, winAt - n)} to go."),
            });
            if (writs is (int all, int used))
                list.Add(new Stat
                {
                    Key = "writ", Value = Math.Max(0, all - used).ToString(), Sub = $"of {all} writs", Tone = all - used <= 0 ? -1 : 0,
                    Tip = $"Writs this turn: {all}, {Math.Max(0, all - used)} left. The court's order takes one; each army you order, or that awaits orders, takes one, keeping one for the court. Unused writs are lost.",
                });
            list.Add(new Stat { Key = "stab", Value = c.Stability.ToString(), Sub = "of 5", Tone = c.Stability <= 1 ? -1 : 0, Tip = "Stability: rises 1 a turn without a tax rise. At 1, a province may revolt." });
            var wars = s.Relations.Where(r => r.Kind == "war" && (r.A == me || r.B == me)).Select(r => r.A == me ? r.B : r.A).ToList();
            list.Add(new Stat
            {
                Key = "war", Value = wars.Count.ToString(), Sub = wars.Count == 1 ? "war" : "wars", Tone = wars.Count > 0 ? -1 : 0,
                Tip = wars.Count == 0 ? "At war with nobody." : "War score from your side, -100 to +100: positive, you are winning.\n" + string.Join("\n", wars.Select(o => $"{nameOf(o)}: {s.WarScore(me, o):+0;-0;0}")),
            });
            var truces = s.Relations.Where(r => r.Kind == "truce" && (r.A == me || r.B == me)).ToList();
            list.Add(new Stat
            {
                Key = "truce", Value = truces.Count.ToString(), Sub = truces.Count == 1 ? "truce" : "truces",
                Tip = truces.Count == 0 ? "No truces." : "No war may be declared on them until each ends.\n" + string.Join("\n", truces.Select(r => $"{nameOf(r.A == me ? r.B : r.A)}: {r.TruceTurnsLeft} turns left")),
            });
            return list;
        }

        /// <summary>One army in the outliner: its name, soldiers, stores as loaves, and what it is doing in a few words.</summary>
        public sealed class ArmyRow
        {
            public string Id, Name, Doing;
            public int Soldiers;
            /// <summary>Whole and half loaves of stores, out of <see cref="Loaves"/>; all zero without supply.</summary>
            public int Full, Half, Loaves;
            public bool Hungry, Besieging, Marching;
        }

        public sealed class SiegeRow
        {
            public int Province;
            public string Besieger, Holder, Tag;
            /// <summary>+1 your siege, -1 a siege of your land, 0 neither.</summary>
            public int Side;
        }

        public sealed class WarRow
        {
            public string Other;
            public int Score;
        }

        public sealed class Outliner
        {
            public List<ArmyRow> Armies = new List<ArmyRow>();
            public List<SiegeRow> Sieges = new List<SiegeRow>();
            public List<WarRow> Wars = new List<WarRow>();
        }

        /// <summary>
        /// The outliner for <paramref name="me"/>: its armies, the most pressing first (hungry, then awaiting orders, then
        /// by size); every siege it lays or suffers; its wars, the sharpest first. With no realm (watch mode), every
        /// siege and every war, each from the first-named realm's side.
        /// </summary>
        public static Outliner OutlinerOf(ReplayState s, string me, IReadOnlyList<string> provinceIds, Func<string, string> provinceName, Func<ArmyState, int> turnsLeft = null)
        {
            var o = new Outliner();
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < provinceIds.Count; i++) index[provinceIds[i]] = i;
            if (me != null)
                foreach (var a in s.Armies.Where(a => a.Owner == me)
                             .OrderByDescending(a => a.Hungry).ThenByDescending(a => a.Awaiting).ThenByDescending(a => a.Soldiers).ThenBy(a => a.Id, StringComparer.Ordinal))
                {
                    int at = a.Location != null && index.TryGetValue(a.Location, out int x) ? x : -1;
                    bool besieging = at >= 0 && s.SiegedBy != null && s.SiegedBy[at] == a.Owner;
                    o.Armies.Add(new ArmyRow
                    {
                        Id = a.Id, Name = Chronicle.Host(a.Id), Soldiers = a.Soldiers,
                        Loaves = a.HasStores ? a.MaxHalfStores / 2 : 0, Full = a.HasStores ? a.HalfStores / 2 : 0, Half = a.HasStores ? a.HalfStores % 2 : 0,
                        Hungry = a.Hungry, Besieging = besieging, Marching = a.Marching,
                        Doing = a.Marching ? $"to {provinceName(a.Destination)}" + (turnsLeft != null ? $", {turnsLeft(a)} turn{(turnsLeft(a) == 1 ? "" : "s")}" : "")
                              : besieging ? $"besieging {provinceName(a.Location)}"
                              : a.Awaiting ? $"at {provinceName(a.Location)}, awaiting orders"
                              : $"holding at {provinceName(a.Location)}",
                    });
                }
            if (s.SiegedBy != null)
                for (int i = 0; i < s.SiegedBy.Length; i++)
                {
                    if (s.SiegedBy[i] == null) continue;
                    string holder = s.Controller(i);
                    int side = s.SiegedBy[i] == me ? 1 : holder == me ? -1 : 0;
                    if (me != null && side == 0) continue;
                    o.Sieges.Add(new SiegeRow { Province = i, Besieger = s.SiegedBy[i], Holder = holder, Tag = ArmyLook.SiegeTag(s, i), Side = side });
                }
            foreach (var r in s.Relations.Where(r => r.Kind == "war"))
            {
                if (me != null && r.A != me && r.B != me) continue;
                string a = me == null || r.A == me ? r.A : r.B, b = a == r.A ? r.B : r.A;
                o.Wars.Add(new WarRow { Other = me == null ? a + "|" + b : b, Score = s.WarScore(a, b) });
            }
            o.Wars = o.Wars.OrderByDescending(w => Math.Abs(w.Score)).ThenBy(w => w.Other, StringComparer.Ordinal).ToList();
            return o;
        }

        /// <summary>The chronicle feed's filters.</summary>
        public enum Feed { All, Me, War, Land }

        /// <summary>Whether a feed filter keeps a line. "Me" is what you did and what was done to you.</summary>
        public static bool Keeps(ChronicleLine l, Feed f, string me) => l != null && f switch
        {
            Feed.Me => me != null && (l.Country == me || l.Event?.Other == me),
            Feed.War => l.Category == ChronicleCategory.War || l.Category == ChronicleCategory.Refused,
            Feed.Land => l.Category == ChronicleCategory.Land || l.Category == ChronicleCategory.Peace,
            _ => true,
        };
    }
}
