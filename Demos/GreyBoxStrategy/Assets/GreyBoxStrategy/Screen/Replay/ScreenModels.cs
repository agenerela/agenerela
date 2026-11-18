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

        /// <summary>
        /// One row of a panel (variant B, slice 2): a label, a value, and either a bar (<see cref="Max"/> &gt; 0, filled to
        /// <see cref="Amount"/>) or pips (<see cref="Pips"/> &gt; 0, <see cref="Amount"/> of them lit). Sentences go in the tip.
        /// </summary>
        public sealed class Row
        {
            public string Key, Label, Value, Tip;
            public int Amount, Max, Pips, Half;
            public int Tone;
        }

        /// <summary>
        /// An army's rows: soldiers, then each type as a bar (yours; a rival's shows only what it mostly is, as its piece
        /// does), its stores as loaves (yours only), its general's traits with what each does, and what it is doing.
        /// </summary>
        public static List<Row> ArmyRows(ArmyState a, bool rival, string doing)
        {
            var rows = new List<Row>();
            rows.Add(new Row { Key = "soldiers", Label = "Soldiers", Value = rival ? ArmySight.About(a.Soldiers) : a.Soldiers.ToString(), Tip = rival ? "What your court sees: a size rounded to ten." : $"{a.Soldiers} soldiers." });
            if (a.HasMix)
            {
                if (rival) rows.Add(new Row { Key = "mostly", Label = "Seen as", Value = ArmyLook.Mostly(a) ?? "not known", Tip = "Your court sees what a rival army mostly is, not its count of each type." });
                else
                    foreach (var t in Units.All)
                    {
                        int n = ArmyLook.Count(a, t);
                        rows.Add(new Row { Key = "type-" + Units.Name(t), Label = char.ToUpperInvariant(Units.Name(t)[0]) + Units.Name(t).Substring(1), Value = n.ToString(), Amount = n, Max = Math.Max(1, a.Soldiers), Tip = $"{n} {Units.Name(t)} of {a.Soldiers}." });
                    }
            }
            if (!rival && a.HasStores)
                rows.Add(new Row
                {
                    Key = "stores", Label = "Stores", Value = a.Hungry ? "none" : $"{a.HalfStores / 2}{(a.HalfStores % 2 == 1 ? "½" : "")} turns",
                    Pips = a.MaxHalfStores / 2, Amount = a.HalfStores / 2, Half = a.HalfStores % 2, Tone = a.Hungry ? -1 : 0, Tip = ArmyLook.Stores(a),
                });
            if (a.General != null)
                foreach (var t in a.Traits)
                    rows.Add(new Row { Key = "trait-" + t, Label = GeneralTraits.Epithet(t), Value = ArmyLook.Effect(t), Tip = $"{a.General} the {GeneralTraits.Epithet(t)}: {ArmyLook.Effect(t)}." });
            rows.Add(new Row { Key = "doing", Label = "Orders", Value = doing, Tip = a.Awaiting ? "Awaiting orders: " + string.Join(", ", a.Wake) + "." : null });
            return rows;
        }

        /// <summary>
        /// A province's holding rows: owner, who defends it and with about how many, its forts as pips, the capital's
        /// bonus, its income. What your court does not know beyond your borders says so.
        /// </summary>
        public static List<Row> ProvinceRows(ReplayState s, int p, RealmQueries q, bool known, Func<string, string> nameOf)
        {
            var rows = new List<Row>();
            string owner = s.Owners[p], holder = s.Controller(p);
            rows.Add(new Row { Key = "owner", Label = "Owner", Value = owner == null ? "nobody" : nameOf(owner) });
            if (holder != owner) rows.Add(new Row { Key = "occupier", Label = "Occupied by", Value = nameOf(holder), Tone = -1, Tip = "The land stays its owner's until a treaty hands it over." });
            if (owner == null) rows.Add(new Row { Key = "garrison", Label = "Garrison", Value = known ? s.Garrisons[p].ToString() : "not known", Tip = known ? null : "Beyond your borders: your court does not know its garrison." });
            rows.Add(new Row { Key = "defence", Label = "Defends with about", Value = known ? q.Defence(s, p).ToString() : "not known" });
            const int fortCap = 5;
            rows.Add(new Row
            {
                Key = "forts", Label = "Forts", Value = known ? $"{s.Forts[p]} (+{s.Forts[p] * q.Rules.FortBonus})" : "not known",
                Pips = known ? Math.Max(fortCap, s.Forts[p]) : 0, Amount = known ? s.Forts[p] : 0, Tip = known ? $"Each fort adds {q.Rules.FortBonus} to its defence." : "Beyond your borders: your court does not know its forts.",
            });
            if (q.IsCapital(p)) rows.Add(new Row { Key = "capital", Label = "Capital", Value = $"+{q.Rules.CapitalBonus}", Tip = "A capital defends harder." });
            rows.Add(new Row { Key = "income", Label = "Income", Value = owner == null ? "none" : $"+{q.Rules.IncomePerProvince} a turn" });
            if (s.SiegedBy?[p] != null) rows.Add(new Row { Key = "siege", Label = "Besieged by", Value = $"{nameOf(s.SiegedBy[p])} · {ArmyLook.SiegeTag(s, p)}", Tone = -1 });
            return rows;
        }

        /// <summary>
        /// A realm's rows: land, soldiers and, only if your court knows them, its treasury, income, upkeep and
        /// stability; its relation to you, and the war score from your side when at war.
        /// </summary>
        public static List<Row> RealmRows(ReplayState s, string id, string you, bool knowsTreasury, Func<string, string> nameOf)
        {
            var c = s.Countries[id];
            var rows = new List<Row>
            {
                new Row { Key = "land", Label = "Provinces", Value = s.ProvinceCount(id).ToString() },
                new Row { Key = "army", Label = "Soldiers", Value = c.Army.ToString() },
            };
            if (knowsTreasury)
            {
                int net = c.Income - c.Upkeep;
                rows.Add(new Row { Key = "gold", Label = "Treasury", Value = $"{c.Gold} ({net:+0;-0;0} a turn)", Tone = Math.Sign(net), Tip = $"Income +{c.Income}, upkeep -{c.Upkeep}." });
                rows.Add(new Row { Key = "stab", Label = "Stability", Value = $"{c.Stability} of 5", Pips = 5, Amount = c.Stability });
            }
            else rows.Add(new Row { Key = "gold", Label = "Treasury", Value = "not known", Tip = "Its treasury, income and stability are not known to your court." });
            if (you != null && id != you)
            {
                var r = s.Relation(you, id);
                string kind = r?.Kind ?? "peace";
                rows.Add(new Row { Key = "relation", Label = "With you", Value = kind == "truce" ? $"truce, {r.TruceTurnsLeft} turns left" : kind, Tone = kind == "war" ? -1 : 0 });
                if (kind == "war") rows.Add(new Row { Key = "score", Label = "War score", Value = $"{s.WarScore(you, id):+0;-0;0}", Tone = Math.Sign(s.WarScore(you, id)), Tip = "From your side, -100 to +100: positive, you are winning." });
            }
            return rows;
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

        /// <summary>What a left click on the map does.</summary>
        public enum MapClick { Nothing, CloseInspector, OrderMarch, OpenProvince, ClearSelection }

        /// <summary>
        /// A left click (not a drag) on the screen: it selects, and never orders (the lead, 17 November: right click
        /// orders the march, left click selects). A click on a panel is the panel's. With the inspector open, a click on
        /// the map outside it only closes it, as Esc does. Otherwise a province opens, and the sea clears the choice.
        /// </summary>
        public static MapClick OnMapClick(bool overPanel, bool inspectorOpen, int province)
        {
            if (overPanel) return MapClick.Nothing;
            if (inspectorOpen) return MapClick.CloseInspector;
            return province < 0 ? MapClick.ClearSelection : MapClick.OpenProvince;
        }

        /// <summary>A right click (not a drag) on the map: with any of your armies chosen, it orders them to the province.</summary>
        public static MapClick OnRightClick(bool overPanel, int province, int armiesChosen) =>
            !overPanel && province >= 0 && armiesChosen > 0 ? MapClick.OrderMarch : MapClick.Nothing;

        /// <summary>A press and release closer than this many pixels is a click; farther, a drag (the box, or a pan).</summary>
        public const float DragPixels = 6f;

        public static bool IsDrag(float dx, float dy) => dx * dx + dy * dy >= DragPixels * DragPixels;

        /// <summary>
        /// The armies a box selects: each of yours whose piece stands inside it, in the order given (the outliner's).
        /// With <paramref name="add"/> (Shift held) they join the armies already chosen; otherwise they replace them.
        /// </summary>
        public static List<string> BoxSelect(IEnumerable<(string id, string owner, float x, float y)> pieces, string you, Box box,
            IEnumerable<string> chosen, bool add)
        {
            var inside = pieces.Where(p => p.owner == you && p.x >= box.xMin && p.x <= box.xMax && p.y >= box.yMin && p.y <= box.yMax)
                .Select(p => p.id).ToList();
            if (!add) return inside;
            var all = chosen.ToList();
            foreach (var id in inside) if (!all.Contains(id)) all.Add(id);
            return all;
        }

        /// <summary>A click on one of your army's pieces: alone it chooses that army; with Shift it adds or removes it.</summary>
        public static List<string> ClickArmy(string id, IEnumerable<string> chosen, bool shift)
        {
            var all = chosen.ToList();
            if (!shift) return all.Count == 1 && all[0] == id ? new List<string>() : new List<string> { id };
            if (!all.Remove(id)) all.Add(id);
            return all;
        }

        /// <summary>
        /// Edge scrolling: the pan direction, -1, 0 or 1 on each axis (y up), while the pointer touches the window's border
        /// within <paramref name="margin"/> pixels. Never when it is off, the window has no focus, or the pointer is over a
        /// panel or outside the window.
        /// </summary>
        public static (int x, int y) EdgeScroll(float px, float py, float width, float height, bool on, bool focused, bool overPanel, float margin = 4)
        {
            if (!on || !focused || overPanel || px < 0 || py < 0 || px > width || py > height) return (0, 0);
            int x = px <= margin ? -1 : px >= width - margin ? 1 : 0;
            int y = py <= margin ? -1 : py >= height - margin ? 1 : 0;
            return (x, y);
        }

        /// <summary>A panel's or a banner's box on the screen, in the root's coordinates (this assembly has no UnityEngine).</summary>
        public readonly struct Box
        {
            public readonly float xMin, yMin, xMax, yMax;
            public Box(float x, float y, float width, float height) { xMin = x; yMin = y; xMax = x + width; yMax = y + height; }
            public bool Overlaps(Box o) => xMax > o.xMin && xMin < o.xMax && yMax > o.yMin && yMin < o.yMax;
        }

        /// <summary>
        /// Where a ruler's words go: the centre x and the bottom line of the banner, which hangs above that line. They sit
        /// above the capital at <paramref name="px"/>, <paramref name="py"/> and never under a panel or off the screen:
        /// right of the side panel when it is open; above the chronicle and the council when they would fall under
        /// them; beside the inspector on the nearer side with room, or below it when neither side has room (a small
        /// screen); always inside the screen. 248 px on the left and 356 on the right are the old realm list and
        /// chronicle, still kept clear.
        /// </summary>
        public static (float x, float bottom) PlaceWords(float px, float py, float half, float height, float screenW, float screenH,
            Box? side, IEnumerable<Box> below, Box? inspector, IEnumerable<Box> above = null)
        {
            float minX = 248 + half, maxX = screenW - 356 - half;
            float x = maxX > minX ? Math.Min(Math.Max(px, minX), maxX) : px;
            float bottom = Math.Max(50 + height, py - 26);
            bool Covers(Box r, float pad) => x + half > r.xMin - pad && x - half < r.xMax + pad && bottom > r.yMin - pad && bottom - height < r.yMax;
            // The map's mode row and its legend, top left: words for a capital in the north drop below them.
            foreach (var r in above ?? Enumerable.Empty<Box>())
                if (Covers(r, 8)) bottom = r.yMax + 8 + height;
            if (side is Box s && Covers(s, 8)) x = s.xMax + 8 + half;
            foreach (var r in below)
                if (Covers(r, 8)) bottom = r.yMin - 8;
            if (inspector is Box w && Covers(w, 0))
            {
                float left = w.xMin - 8 - half, right = w.xMax + 8 + half;
                bool leftFits = left >= minX, rightFits = right <= maxX;
                if (leftFits && (!rightFits || x - left <= right - x)) x = left;
                else if (rightFits) x = right;
                else bottom = w.yMax + 8 + height;
            }
            float lowest = Math.Max(50 + height, screenH - 8);
            return (x, Math.Min(Math.Max(bottom, 50 + height), lowest));
        }

        /// <summary>The map's zoom for one notch of the wheel: 20% of the current size, so the full range takes about twelve.</summary>
        public const float ZoomStep = 1.2f;

        /// <summary>
        /// A frame's wheel reading in notches. The Input System reports a notch as 1 by default (its uniform scroll
        /// setting) and as 120 on Windows with the platform's own range: a reading of 10 or more is taken as the
        /// latter. A trackpad's small readings give fractions of a notch; a frame is clamped to three notches.
        /// </summary>
        public static float WheelNotches(float scrollY)
        {
            if (Math.Abs(scrollY) < 1e-4f) return 0;
            float n = Math.Abs(scrollY) >= 10 ? scrollY / 120f : scrollY;
            return Math.Max(-3f, Math.Min(3f, n));
        }

        /// <summary>The camera size the wheel aims for: up (positive) zooms in. Multiplicative, so each notch feels the same.</summary>
        public static float ZoomTarget(float from, float notches, float min, float max) =>
            Math.Max(min, Math.Min(max, from * (float)Math.Pow(ZoomStep, -notches)));

        /// <summary>One frame of easing toward the target, in log space: about 95% of the way in a tenth of a second.</summary>
        public static float EaseZoom(float current, float target, float dt)
        {
            if (current <= 0 || target <= 0) return target;
            float k = 1f - (float)Math.Exp(-Math.Max(0f, dt) / 0.033f);
            return (float)Math.Exp(Math.Log(current) + (Math.Log(target) - Math.Log(current)) * k);
        }
    }
}
