using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The writs turn on the map (rules v2, 3.9 and 5.1): every army as a piece, always shown, in every
    /// map mode; its march as a dashed ink line with the turns it has left; a battle's clash and its report
    /// card; a pennant on every occupied province; the seal a treaty stamps.
    /// </summary>
    public sealed partial class ScreenUI
    {
        /// <summary>True for a writs game: armies are pieces in the rules, so the map shows each one.</summary>
        public bool Writs;
        /// <summary>For a march line's label: the turns an army needs for the rest of its path.</summary>
        public Func<ArmyState, int> TurnsLeft;
        /// <summary>A piece clicked: the army, for the side panel.</summary>
        public Action<string> HostPressed;

        private readonly Dictionary<string, VisualElement> hosts = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        private readonly Dictionary<string, (int from, int to, float t)> hostMotion = new Dictionary<string, (int, int, float)>(StringComparer.Ordinal);
        private readonly Dictionary<int, VisualElement> pennants = new Dictionary<int, VisualElement>();
        private MarchLines marchLines;
        /// <summary>The plates and pennants, under the province names in the same layer, so a crowd of armies never hides a name.</summary>
        private VisualElement hostLayer;
        private ReplayState hostState;

        /// <summary>The pieces for a state: one per army, fanned out where several stand together.</summary>
        private void ShowHosts(ReplayState s)
        {
            hostState = s;
            if (hostLayer == null)
            {
                hostLayer = new VisualElement { pickingMode = PickingMode.Ignore, name = "hosts" };
                hostLayer.style.position = Position.Absolute; hostLayer.style.left = 0; hostLayer.style.top = 0; hostLayer.style.right = 0; hostLayer.style.bottom = 0;
                labels.Insert(0, hostLayer);
            }
            foreach (var kv in armyPieces) kv.Value.style.display = DisplayStyle.None;
            var alive = new HashSet<string>(s.Armies.Select(a => a.Id), StringComparer.Ordinal);
            foreach (var id in hosts.Keys.Where(k => !alive.Contains(k)).ToList()) { hosts[id].RemoveFromHierarchy(); hosts.Remove(id); }
            foreach (var a in s.Armies)
            {
                if (!hosts.TryGetValue(a.Id, out var piece))
                {
                    piece = new VisualElement { pickingMode = PickingMode.Position }; piece.AddToClassList("host");
                    piece.Add(new Crest(CountryLook.Of(a.Owner), 15));
                    var n = new Label(); n.AddToClassList("host-n"); piece.Add(n);
                    var g = new Label(Initial(a.Id)); g.AddToClassList("host-g"); piece.Add(g);
                    var seal = new VisualElement { pickingMode = PickingMode.Ignore }; seal.AddToClassList("host-wait"); piece.Add(seal);
                    string id = a.Id;
                    piece.RegisterCallback<ClickEvent>(_ => HostPressed?.Invoke(id));
                    hostLayer.Add(piece);
                    hosts[a.Id] = piece;
                }
                piece.style.backgroundColor = (Color)Darker(CountryLook.Of(a.Owner).Colour);
                piece.Q<Label>(className: "host-n").text = a.Soldiers.ToString();
                piece.Q(className: "host-wait").style.display = ShowAwaiting && a.Awaiting ? DisplayStyle.Flex : DisplayStyle.None;
                piece.tooltip = HostLine(a) + "\nClick: its army.";
            }
            if (marchLines == null) { marchLines = new MarchLines { Owner = this }; fx.Insert(0, marchLines); }
            marchLines.MarkDirtyRepaint();
            ShowPennants(s);
        }

        /// <summary>The open-seal mark on armies awaiting orders: rule mode's, on the person's own armies.</summary>
        public bool ShowAwaiting;

        /// <summary>An army in one line: "Maren's host (Orsk), 40 soldiers at Corwick, marching to Ashby (2 turns)".</summary>
        public string HostLine(ArmyState a)
        {
            string order = a.Marching ? $", marching to {Cap(a.Destination)}" + (TurnsLeft != null ? $" ({TurnsLeft(a)} turns)" : "") : ", holding";
            string waits = a.Awaiting ? $"\nAwaiting orders: {string.Join(", ", a.Wake)}." : "";
            string border = a.StepTurnsLeft > 1 && a.Path.Count > 0 ? $"\nOn the border with {Cap(a.Path[0])}: rough ground takes two turns." : "";
            return $"{Chronicle.Host(a.Id)} ({CountryLook.NameOf(a.Owner)}), {a.Soldiers} soldiers at {Cap(a.Location)}{order}.{border}{waits}";
        }

        private static string Initial(string id) => string.IsNullOrEmpty(id) ? "?" : char.ToUpperInvariant(id[0]).ToString();

        /// <summary>Moves a piece between two provinces (indices) during an animation; t 0 to 1. Cleared by <see cref="StopHostMotion"/>.</summary>
        public void MoveHost(string id, int from, int to, float t) => hostMotion[id] = (from, to, t);
        public void StopHostMotion() => hostMotion.Clear();

        /// <summary>Where a piece stands, in panel space: its province, or between two while it moves or waits on a border.</summary>
        public Vector2 HostPoint(ArmyState a)
        {
            if (hostMotion.TryGetValue(a.Id, out var m)) return Vector2.Lerp(ProvinceToPanel(m.from), ProvinceToPanel(m.to), m.t) + new Vector2(0, -20);
            int at = IndexOf(a.Location);
            if (at < 0) return new Vector2(-1000, -1000);
            var here = hostState.Armies.Where(x => x.Location == a.Location && !hostMotion.ContainsKey(x.Id)).OrderBy(x => x.Owner, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
            int k = Math.Max(0, here.IndexOf(a)), n = Math.Max(1, here.Count);
            var p = ProvinceToPanel(at);
            if (a.StepTurnsLeft > 1 && a.Marching && a.Path.Count > 0 && IndexOf(a.Path[0]) >= 0) p = Vector2.Lerp(p, ProvinceToPanel(IndexOf(a.Path[0])), 0.45f);
            // Several armies in one province stand side by side, two rows when there are many, so each reads.
            int perRow = n <= 3 ? n : (n + 1) / 2;
            int row = k / perRow, col = k % perRow, inRow = Math.Min(perRow, n - row * perRow);
            float dx = (col - (inRow - 1) / 2f) * 58;
            return p + new Vector2(dx, -24 - row * 24);
        }

        private int IndexOf(string province)
        {
            if (province == null) return -1;
            for (int i = 0; i < ProvinceIds.Count; i++) if (ProvinceIds[i] == province) return i;
            return -1;
        }

        private void PlaceHosts()
        {
            if (hostState == null) return;
            foreach (var a in hostState.Armies)
            {
                if (!hosts.TryGetValue(a.Id, out var piece)) continue;
                var p = HostPoint(a);
                piece.style.left = p.x;
                piece.style.top = p.y;
            }
            foreach (var kv in pennants)
            {
                var p = ProvinceToPanel(kv.Key);
                kv.Value.style.left = p.x + 10;
                kv.Value.style.top = p.y - 6;
            }
            marchLines?.MarkDirtyRepaint();
            marchLines?.PlaceLabels();
        }

        // ---- pennants on occupied land ----

        private void ShowPennants(ReplayState s)
        {
            for (int i = 0; i < s.Occupiers.Length; i++)
            {
                string occ = s.Occupiers[i];
                pennants.TryGetValue(i, out var flag);
                if (occ == null) { if (flag != null) { flag.RemoveFromHierarchy(); pennants.Remove(i); } continue; }
                if (flag == null)
                {
                    flag = new Pennant { pickingMode = PickingMode.Position };
                    hostLayer.Add(flag);
                    pennants[i] = flag;
                }
                ((Pennant)flag).Colour = CountryLook.Of(occ).Colour;
                flag.tooltip = $"{Cap(ProvinceIds[i])}: {CountryLook.NameOf(s.Owners[i])}'s, occupied by {CountryLook.NameOf(occ)}. It stays {CountryLook.NameOf(s.Owners[i])}'s until a treaty says otherwise.";
                flag.MarkDirtyRepaint();
            }
        }

        private sealed class Pennant : VisualElement
        {
            public Color Colour;
            public Pennant()
            {
                AddToClassList("pennant");
                generateVisualContent += ctx =>
                {
                    var p = ctx.painter2D;
                    p.strokeColor = new Color32(40, 28, 16, 255); p.lineWidth = 1.6f;
                    p.BeginPath(); p.MoveTo(new Vector2(2, 20)); p.LineTo(new Vector2(2, 0)); p.Stroke();
                    p.fillColor = Colour; p.strokeColor = new Color32(30, 20, 10, 255); p.lineWidth = 1;
                    p.BeginPath(); p.MoveTo(new Vector2(2.5f, 1)); p.LineTo(new Vector2(16, 5)); p.LineTo(new Vector2(2.5f, 10)); p.ClosePath(); p.Fill(); p.Stroke();
                };
            }
        }

        // ---- march lines ----

        private sealed class MarchLines : VisualElement
        {
            public ScreenUI Owner;
            private readonly List<Label> turnLabels = new List<Label>();

            public MarchLines()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
                generateVisualContent += Draw;
            }

            private List<(ArmyState army, List<Vector2> pts)> Lines()
            {
                var lines = new List<(ArmyState, List<Vector2>)>();
                var s = Owner?.hostState;
                if (s == null) return lines;
                foreach (var a in s.Armies.Where(a => a.Marching && a.Path.Count > 0))
                {
                    var pts = new List<Vector2> { Owner.HostPoint(a) + new Vector2(0, 14) };
                    foreach (string step in a.Path)
                    {
                        int i = Owner.IndexOf(step);
                        if (i >= 0) pts.Add(Owner.ProvinceToPanel(i));
                    }
                    if (pts.Count >= 2) lines.Add((a, pts));
                }
                return lines;
            }

            /// <summary>The turns-left labels, outside drawing: a label cannot change while the tree renders.</summary>
            public void PlaceLabels()
            {
                int used = 0;
                if (Owner.TurnsLeft != null)
                    foreach (var (a, pts) in Lines())
                    {
                        if (used >= turnLabels.Count) { var l = new Label(); l.AddToClassList("march-turns"); Add(l); turnLabels.Add(l); }
                        var label = turnLabels[used++];
                        int t = Owner.TurnsLeft(a);
                        label.text = t == 1 ? "1 turn" : $"{t} turns";
                        label.style.display = DisplayStyle.Flex;
                        var end = pts[pts.Count - 1];
                        label.style.left = end.x + 7; label.style.top = end.y - 8;
                    }
                Hide(used);
            }

            private void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                foreach (var (a, pts) in Lines())
                {
                    var c = (Color)CountryLook.Of(a.Owner).Colour;
                    p.lineCap = LineCap.Round;
                    // A dark casing under a dash in the owner's colour, as ink over the wash.
                    Dashed(p, pts, new Color(0.08f, 0.05f, 0.02f, 0.85f), 4.2f);
                    Dashed(p, pts, Color.Lerp(c, Color.white, 0.15f), 2.2f);
                    var end = pts[pts.Count - 1];
                    p.fillColor = c; p.strokeColor = new Color(0.08f, 0.05f, 0.02f, 1); p.lineWidth = 1.4f;
                    p.BeginPath(); p.Arc(end, 4.5f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); p.Stroke();
                }
            }

            private void Hide(int from) { for (int i = from; i < turnLabels.Count; i++) turnLabels[i].style.display = DisplayStyle.None; }

            private static void Dashed(Painter2D p, List<Vector2> pts, Color colour, float width)
            {
                p.strokeColor = colour; p.lineWidth = width;
                const float dash = 7, gap = 5;
                float carry = 0; bool on = true;
                for (int k = 1; k < pts.Count; k++)
                {
                    Vector2 a = pts[k - 1], b = pts[k];
                    float len = Vector2.Distance(a, b), at = 0;
                    var dir = len > 0 ? (b - a) / len : Vector2.zero;
                    while (at < len)
                    {
                        float seg = Mathf.Min((on ? dash : gap) - carry, len - at);
                        if (on) { p.BeginPath(); p.MoveTo(a + dir * at); p.LineTo(a + dir * (at + seg)); p.Stroke(); }
                        at += seg; carry += seg;
                        if (carry >= (on ? dash : gap) - 0.001f) { carry = 0; on = !on; }
                    }
                }
            }
        }

        // ---- a battle: the clash where it happens ----

        private sealed class Clash : VisualElement
        {
            public Func<Vector2> At;
            public float Puff, Swords;
            public Clash()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
                generateVisualContent += ctx =>
                {
                    if (At == null) return;
                    var c = At() + new Vector2(0, 8);
                    var p = ctx.painter2D;
                    if (Puff > 0)
                    {
                        // A puff of smoke: a few rings opening and fading.
                        for (int k = 0; k < 5; k++)
                        {
                            float ang = k * 1.2566f + Puff * 0.6f, r = 8 + 22 * Puff;
                            p.fillColor = new Color(0.85f, 0.8f, 0.7f, 0.5f * (1 - Puff));
                            p.BeginPath(); p.Arc(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r * 0.6f, 6 + 10 * Puff, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                        }
                    }
                    if (Swords > 0)
                    {
                        float s = 0.6f + 0.4f * Swords;
                        p.fillColor = new Color32(27, 19, 11, (byte)(255 * Swords));
                        p.strokeColor = new Color32(210, 173, 98, (byte)(255 * Swords)); p.lineWidth = 1.5f;
                        p.BeginPath(); p.Arc(c, 15 * s, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); p.Stroke();
                        p.strokeColor = new Color32(243, 226, 184, (byte)(255 * Swords)); p.lineWidth = 3; p.lineCap = LineCap.Round;
                        p.BeginPath(); p.MoveTo(c + new Vector2(-8, -8) * s); p.LineTo(c + new Vector2(8, 8) * s); p.Stroke();
                        p.BeginPath(); p.MoveTo(c + new Vector2(8, -8) * s); p.LineTo(c + new Vector2(-8, 8) * s); p.Stroke();
                    }
                };
            }
        }

        /// <summary>A clash at a province, or on the border between two: returns a handle for <see cref="SetClash"/> and <see cref="HideClash"/>.</summary>
        public object ShowClash(int province, int other = -1)
        {
            var c = new Clash { At = () => other >= 0 ? Vector2.Lerp(ProvinceToPanel(province), ProvinceToPanel(other), 0.5f) : ProvinceToPanel(province) };
            fx.Add(c);
            return c;
        }

        public void SetClash(object handle, float swords, float puff)
        {
            if (!(handle is Clash c)) return;
            c.Swords = swords; c.Puff = puff; c.MarkDirtyRepaint();
        }

        public void HideClash(object handle) => (handle as Clash)?.RemoveFromHierarchy();

        // ---- the battle report card ----

        private VisualElement battleCard;

        /// <summary>
        /// The battle report (v2 3.4): both sides, their armies and soldiers, each term of the result as a bar,
        /// the dice last; who lost what and where the loser went; and the rules' own sentence on why.
        /// </summary>
        public void ShowBattle(ReplayEvent e)
        {
            CloseBattle();
            var b = e.Battle;
            if (b == null) return;
            string where = b.Province != null ? Cap(b.Province) : $"the {Cap(b.Border?[0])}–{Cap(b.Border?[1])} border";
            string them = b.Defender == null ? "The garrison" : CountryLook.NameOf(b.Defender) + (b.DefenderKind == "militia" ? "'s militia" : "");
            battleCard = new VisualElement(); battleCard.AddToClassList("panel"); battleCard.AddToClassList("event-card"); battleCard.AddToClassList("battle-card");
            var head = new VisualElement(); head.AddToClassList("panel-head");
            var t = new Label($"BATTLE REPORT · TURN {b.Turn}"); t.AddToClassList("panel-title"); head.Add(t);
            var close = new Button(CloseBattle) { text = "×", tooltip = "Close (Esc)" }; close.AddToClassList("tbtn"); head.Add(close);
            battleCard.Add(head);
            var body = new ScrollView(); body.AddToClassList("event-body"); battleCard.Add(body);

            var title = new Label($"The battle at {where}"); title.AddToClassList("event-title"); body.Add(title);
            var verdict = new Label(Named(b.Why)); verdict.AddToClassList("event-story"); body.Add(verdict);

            var sides = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            sides.Add(Side(b.Attacker, CountryLook.NameOf(b.Attacker), "attacking", b.AttackerArmies, b.AttackerSoldiers, 0, b.AttackerLost, b.AttackerWon, b));
            sides.Add(Side(b.Defender, them, b.DefenderKind == "army" ? "defending" : b.DefenderKind == "militia" ? "its militia" : "neutral garrison", b.DefenderArmies, b.DefenderSoldiers, b.Militia, b.DefenderLost, !b.AttackerWon, b));
            body.Add(sides);

            body.Add(SectionHead("WHAT DECIDED IT", "strength, side by side"));
            double max = Math.Max(1, Math.Max(b.AttackStrength, b.DefenceStrength));
            body.Add(Bar("Soldiers", b.AttackerSoldiers, b.DefenderSoldiers, max, b, plain: true));
            if (b.FortTerm > 0) body.Add(Bar(b.Forts == 1 ? "1 fort" : $"{b.Forts} forts", 0, b.FortTerm, max, b));
            if (b.CapitalTerm > 0) body.Add(Bar("The capital", 0, b.CapitalTerm, max, b));
            body.Add(Bar($"The dice ×{b.AttackRoll:0.00} and ×{b.DefenceRoll:0.00}", b.AttackerSoldiers * (b.AttackRoll - 1), b.DefenderSoldiers * (b.DefenceRoll - 1), max, b));
            body.Add(Bar("Strength", b.AttackStrength, b.DefenceStrength, max, b, plain: true, total: true));
            if (b.Terrain != null) body.Add(Text($"Fought on {b.Terrain}: terrain does not change a battle until unit types and generals come (R3).", "src"));

            var after = new List<string>();
            foreach (var kv in b.Retreats) after.Add($"{Chronicle.Host(kv.Key)} fell back to {Cap(kv.Value)}");
            if (b.Routed) after.Add(b.Retreats.Count == 0 ? "The loser was routed: destroyed, with nowhere to go or beaten by more than 2.5 to 1" : "Some of the loser's armies were routed");
            if (after.Count > 0) body.Add(Text(string.Join(". ", after) + ".", "event-effect"));
            Root.Add(battleCard);
        }

        public void CloseBattle() { battleCard?.RemoveFromHierarchy(); battleCard = null; }
        public bool BattleOpen => battleCard != null;

        private VisualElement Side(string country, string name, string role, List<string> armies, int soldiers, int militia, int lost, bool won, BattleRecord b)
        {
            var col = new VisualElement { style = { flexGrow = 1, flexBasis = 0, marginRight = 8 } };
            var row = new VisualElement(); row.AddToClassList("event-row");
            if (country != null) row.Add(new Crest(CountryLook.Of(country), 22));
            var n = new Label(name); n.AddToClassList("event-name"); row.Add(n);
            col.Add(row);
            col.Add(Text($"{role}{(won ? " · won" : " · lost")}", won ? "event-chose" : "event-effect"));
            string who = armies.Count > 0 ? string.Join(", ", armies.Select(Chronicle.Host)) : "";
            if (militia > 0) who += (who.Length > 0 ? " and " : "") + $"{(country == null ? "a garrison" : "the militia")} of {militia}";
            col.Add(Text($"{soldiers} soldiers{(who.Length > 0 ? ": " + who : "")}", "src"));
            col.Add(Text($"Lost {lost}", "event-effect"));
            return col;
        }

        // One term as two bars, the attacker's growing left of the middle, the defender's right.
        /// <summary>A sentence of the rules' with country ids as names: "valen won" reads "Valen won".</summary>
        private string Named(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (var id in CountryIds) text = System.Text.RegularExpressions.Regex.Replace(text, $@"\b{id}\b", CountryLook.NameOf(id));
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private static VisualElement Bar(string label, double attack, double defence, double max, BattleRecord b, bool plain = false, bool total = false)
        {
            var row = new VisualElement(); row.AddToClassList("battle-bar");
            var l = new Label(label); l.AddToClassList("battle-bar-label"); row.Add(l);
            var track = new VisualElement(); track.AddToClassList("battle-track");
            var left = new VisualElement(); left.AddToClassList("battle-half"); left.style.justifyContent = Justify.FlexEnd; left.style.flexDirection = FlexDirection.Row;
            var right = new VisualElement(); right.AddToClassList("battle-half"); right.style.flexDirection = FlexDirection.Row;
            var a = new VisualElement(); a.AddToClassList("battle-fill"); a.style.width = Length.Percent((float)(100 * Math.Max(0, attack) / max));
            a.style.backgroundColor = (Color)CountryLook.Of(b.Attacker).Colour;
            var d = new VisualElement(); d.AddToClassList("battle-fill"); d.style.width = Length.Percent((float)(100 * Math.Max(0, defence) / max));
            d.style.backgroundColor = b.Defender != null ? (Color)CountryLook.Of(b.Defender).Colour : new Color(0.6f, 0.56f, 0.48f);
            left.Add(a); right.Add(d);
            track.Add(left); track.Add(right);
            row.Add(track);
            var v = new Label(plain ? $"{attack:0} : {defence:0}" : $"{Signed(attack)} : {Signed(defence)}"); v.AddToClassList("battle-bar-value"); row.Add(v);
            if (total) row.AddToClassList("total");
            return row;
        }

        private static string Signed(double v) => Math.Abs(v) < 0.5 ? "0" : v > 0 ? $"+{v:0}" : $"{v:0}";

        // ---- the treaty's seal ----

        /// <summary>A wax seal stamping on a province for a moment as a treaty lands: grows in, holds, fades.</summary>
        public VisualElement StampSeal(int province, string country)
        {
            var seal = new VisualElement { pickingMode = PickingMode.Ignore }; seal.AddToClassList("seal");
            seal.Add(new Crest(CountryLook.Of(country), 20));
            var p = ProvinceToPanel(province);
            seal.style.left = p.x; seal.style.top = p.y;
            fx.Add(seal);
            seal.schedule.Execute(() => seal.AddToClassList("on")).StartingIn(16);
            seal.schedule.Execute(() => seal.RemoveFromClassList("on")).StartingIn(1100);
            seal.schedule.Execute(() => seal.RemoveFromHierarchy()).StartingIn(1500);
            return seal;
        }
    }
}
