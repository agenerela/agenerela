using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>The map modes. Political stays the main one: it keeps capitals, forts and names (decision 19).</summary>
    public enum MapMode { Political, Armies, Forts, Reach }

    /// <summary>
    /// The side panel (a province or a country, on click), the diplomacy view, the map-mode bar and
    /// the followed country's own top bar: the commands' homes the design page lays out.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private VisualElement side, diplomacy, modes, legend, followBar;
        private Label[] armyBadges;
        public RealmQueries Queries;
        public Action<MapMode> ModePressed;
        public Action<string> FollowPressed;
        public Action DiplomacyPressed;
        public Action<ReplayEvent> DecisionPressed;
        public Action<int> ProvincePressed;
        public MapMode Mode = MapMode.Political;
        public string Followed;

        private static readonly (MapMode mode, string label, IconKind icon, string tip)[] Modes =
        {
            (MapMode.Political, "Political", IconKind.Province, "Political (1): owners' colours, capitals, forts and names"),
            (MapMode.Armies, "Armies", IconKind.Army, "Armies (2): each country's colour as strong as its army"),
            (MapMode.Forts, "Forts", IconKind.Fort, "Forts (3): provinces by their forts, +15 defence each"),
            (MapMode.Reach, "Reach", IconKind.War, "Reach (4): who can attack what. The followed country's legal attacks, hatched, with the share of rolls each wins"),
        };

        public void BuildPanels()
        {
            side = new VisualElement(); side.AddToClassList("panel"); side.AddToClassList("side"); Root.Add(side);
            diplomacy = new VisualElement(); diplomacy.AddToClassList("panel"); diplomacy.AddToClassList("diplomacy"); Root.Add(diplomacy);

            modes = new VisualElement(); modes.AddToClassList("modes");
            foreach (var m in Modes)
            {
                var b = new Button(() => ModePressed?.Invoke(m.mode)) { tooltip = m.tip }; b.AddToClassList("mode");
                b.userData = m.mode;
                b.Add(new Icon(m.icon, 12) { style = { marginRight = 5 } }); b.Add(new Label(m.label));
                modes.Add(b);
            }
            Root.Add(modes);
            legend = new VisualElement(); legend.AddToClassList("legend"); Root.Add(legend);

            followBar = new VisualElement(); followBar.AddToClassList("follow-bar");
            nations.parent.Insert(nations.parent.IndexOf(nations) + 1, followBar);

            // The showcase in the date block, one click away in every mode: the decisions the framework recorded.
            var insp = new Button(() => InspectPressed?.Invoke()) { tooltip = "The decisions: what each ruler was told, offered and chose, as the framework recorded it (I)" };
            insp.AddToClassList("tbtn"); insp.AddToClassList("insp-btn"); insp.Add(new Icon(IconKind.Eye, 13)); insp.Add(new Label("Decisions"));
            playButton.parent.Add(insp);
            var diplo = new Button(() => DiplomacyPressed?.Invoke()) { tooltip = "Diplomacy (D): who is at war, at truce or at peace with whom" };
            diplo.AddToClassList("tbtn"); diplo.Add(new Icon(IconKind.Peace, 12)); diplo.style.marginLeft = 8;
            playButton.parent.Add(diplo);

            armyBadges = new Label[ProvinceIds.Count];
            for (int i = 0; i < ProvinceIds.Count; i++)
            {
                armyBadges[i] = new Label { pickingMode = PickingMode.Ignore }; armyBadges[i].AddToClassList("army-badge");
                armyBadges[i].style.display = DisplayStyle.None; labels.Add(armyBadges[i]);
            }
        }

        // ---- the outliner (variant B, #20) ----

        private VisualElement outliner;
        private string filtersForYou;
        private ScrollView outlinerBody;

        /// <summary>
        /// Your armies (stores as loaves, what each is doing), the sieges you lay or suffer, your wars with their scores,
        /// and the ledgers behind a button. Watching, every siege and war.
        /// </summary>
        public void RenderOutliner(ReplayState s)
        {
            if (outlinerBody == null || s == null) return;
            // The feed's "Me" exists only once you rule: its filters are rebuilt when the fog comes on or goes.
            if (filtersForYou != You) { filtersForYou = You; RenderFilters(); }
            outlinerBody.Clear();
            string me = Fog.On ? You : Followed;
            var o = ScreenModels.OutlinerOf(s, me, ProvinceIds, Cap, TurnsLeft);
            if (me != null)
            {
                OutHead(IconKind.Army, "ARMIES", o.Armies.Count.ToString());
                foreach (var a in o.Armies)
                {
                    var row = new VisualElement(); row.AddToClassList("ol-row"); if (a.Hungry) row.AddToClassList("hungry");
                    row.tooltip = $"{a.Name}: {a.Soldiers} soldiers, {a.Doing}. Click: its panel.";
                    var top = new VisualElement(); top.AddToClassList("ol-top");
                    top.Add(new Icon(a.Besieging ? IconKind.Fort : a.Marching ? IconKind.Province : IconKind.Army, 14) { Tint = new Color32(196, 204, 214, 255) });
                    top.Add(new Label(a.Name) { name = "ol-name" });
                    if (a.Loaves > 0)
                    {
                        var loaves = new VisualElement(); loaves.AddToClassList("ol-loaves");
                        for (int k = 0; k < a.Loaves; k++)
                            loaves.Add(new Icon(IconKind.Loaf, 11) { Tint = k < a.Full ? new Color32(217, 168, 90, 255) : k < a.Full + a.Half ? new Color32(160, 124, 70, 255) : new Color32(70, 58, 40, 255) });
                        top.Add(loaves);
                    }
                    var n = new Label(a.Soldiers.ToString()); n.AddToClassList("ol-n"); top.Add(n);
                    row.Add(top);
                    var doing = new Label(a.Hungry ? "no stores · " + a.Doing : a.Doing); doing.AddToClassList("ol-sub"); if (a.Hungry) doing.AddToClassList("bad"); row.Add(doing);
                    string id = a.Id;
                    row.RegisterCallback<ClickEvent>(_ => HostPressed?.Invoke(id));
                    outlinerBody.Add(row);
                }
                if (o.Armies.Count == 0) OutNote("No armies: recruit at your capital.");
            }
            OutHead(IconKind.Fort, "SIEGES", o.Sieges.Count.ToString());
            foreach (var x in o.Sieges)
            {
                var row = new VisualElement(); row.AddToClassList("ol-row");
                var top = new VisualElement(); top.AddToClassList("ol-top");
                top.Add(new Crest(CountryLook.Of(x.Besieger), 14));
                top.Add(new Label($"{Cap(ProvinceIds[x.Province])}") { name = "ol-name" });
                var n = new Label(x.Tag ?? "siege"); n.AddToClassList("ol-n"); if (x.Side < 0) n.AddToClassList("bad"); else if (x.Side > 0) n.AddToClassList("good"); top.Add(n);
                row.Add(top);
                row.tooltip = $"{CountryLook.NameOf(x.Besieger)} besieges {Cap(ProvinceIds[x.Province])}, {(x.Holder == null ? "neutral" : CountryLook.NameOf(x.Holder) + "'s")}. Click: the province.";
                int at = x.Province;
                row.RegisterCallback<ClickEvent>(_ => ProvincePressed?.Invoke(at));
                outlinerBody.Add(row);
            }
            if (o.Sieges.Count == 0) OutNote("None.");
            OutHead(IconKind.Swords, "WARS", o.Wars.Count.ToString());
            foreach (var w in o.Wars)
            {
                var row = new VisualElement(); row.AddToClassList("ol-row");
                var top = new VisualElement(); top.AddToClassList("ol-top");
                var pair = w.Other.Split('|');
                foreach (var id in pair) top.Add(new Crest(CountryLook.Of(id), 14));
                top.Add(new Label(pair.Length == 2 ? $"{CountryLook.NameOf(pair[0])} – {CountryLook.NameOf(pair[1])}" : CountryLook.NameOf(w.Other)) { name = "ol-name" });
                var n = new Label($"{w.Score:+0;-0;0}"); n.AddToClassList("ol-n"); if (w.Score < 0) n.AddToClassList("bad"); else if (w.Score > 0) n.AddToClassList("good"); top.Add(n);
                row.Add(top);
                row.tooltip = $"War score from {(pair.Length == 2 ? CountryLook.NameOf(pair[0]) + "'s" : "your")} side: positive, winning. Click: {(pair.Length == 2 ? "the first realm" : "the realm")}.";
                string who = pair[0];
                row.RegisterCallback<ClickEvent>(_ => CountryPressed?.Invoke(who));
                outlinerBody.Add(row);
            }
            if (o.Wars.Count == 0) OutNote(me != null ? "At peace with every realm." : "No wars.");
            OutHead(IconKind.Scroll, "LEDGERS", "");
            var buttons = new VisualElement(); buttons.AddToClassList("ol-buttons");
            var ledger = new Button(ToggleLedger) { text = "Realms", tooltip = "Every realm's standing, wars, truces, armies, and the neutral land" }; ledger.AddToClassList("tbtn"); ledger.AddToClassList("ol-btn");
            var diplo = new Button(() => DiplomacyPressed?.Invoke()) { text = "Diplomacy", tooltip = "Who is at war, truce or peace with whom (D)" }; diplo.AddToClassList("tbtn"); diplo.AddToClassList("ol-btn");
            buttons.Add(ledger); buttons.Add(diplo);
            outlinerBody.Add(buttons);
        }

        private void OutHead(IconKind icon, string title, string note)
        {
            var h = new VisualElement(); h.AddToClassList("ol-head");
            h.Add(new Icon(icon, 12) { Tint = new Color32(226, 194, 122, 255) });
            h.Add(new Label(title) { style = { marginLeft = 6, flexGrow = 1 } });
            if (note.Length > 0) h.Add(new Label(note) { style = { color = (Color)new Color32(141, 127, 102, 255) } });
            outlinerBody.Add(h);
        }

        private void OutNote(string text) { var l = new Label(text); l.AddToClassList("ol-note"); outlinerBody.Add(l); }

        /// <summary>The realms' ledger opens on the left, where a province or an army would; the left is empty otherwise.</summary>
        public void ToggleLedger()
        {
            bool open = !Root.ClassListContains("ledger-open");
            if (open) CloseSide();
            Root.EnableInClassList("ledger-open", open);
        }

        /// <summary>Gilt corners on every panel, once each.</summary>
        public void Ornament()
        {
            Root.Query(className: "panel").ForEach(p =>
            {
                if (p.ClassListContains("ornate")) return;
                p.AddToClassList("ornate");
                foreach (var c in new[] { "tl", "tr", "bl", "br" })
                {
                    var e = new VisualElement { pickingMode = PickingMode.Ignore }; e.AddToClassList("corner"); e.AddToClassList(c);
                    p.Add(e);
                }
            });
        }

        // ---- map modes ----

        public void ShowMode(MapMode mode, ReplayState s, ISet<int> reach)
        {
            Mode = mode;
            foreach (var b in modes.Children()) b.EnableInClassList("on", (MapMode)b.userData == mode);
            legend.Clear();
            switch (mode)
            {
                case MapMode.Political:
                    foreach (var id in CountryIds) legend.Add(LegendItem((Color)CountryLook.Of(id).Colour, CountryLook.NameOf(id)));
                    legend.Add(LegendItem(new Color32(150, 140, 122, 255), "Neutral"));
                    break;
                case MapMode.Armies:
                    legend.Add(new Label("Colour as strong as the owner's army; each army stands at its capital"));
                    break;
                case MapMode.Forts:
                    legend.Add(new Label("No fort: pale · one fort: half · two forts: full, +15 defence each"));
                    break;
                case MapMode.Reach:
                    legend.Add(Followed == null
                        ? new Label("Follow a country (its panel, or its crest) to see what it can attack")
                        : new Label($"{CountryLook.NameOf(Followed)} can attack {reach?.Count ?? 0} province(s) · % = share of rolls it wins"));
                    break;
            }
            for (int i = 0; i < ProvinceIds.Count; i++)
            {
                // The badge shows the reach mode's odds; armies have their own pieces in every mode.
                bool odds = mode == MapMode.Reach && reach != null && reach.Contains(i) && Followed != null;
                armyBadges[i].style.display = odds ? DisplayStyle.Flex : DisplayStyle.None;
                if (odds)
                {
                    double p = Queries.WinChance(s, Followed, i);
                    armyBadges[i].text = $"{Math.Round(p * 100)}%";
                    armyBadges[i].style.backgroundColor = p > 0.6 ? new Color(0.25f, 0.4f, 0.2f) : p > 0.3 ? new Color(0.5f, 0.38f, 0.12f) : new Color(0.45f, 0.15f, 0.1f);
                }
            }
        }

        private static VisualElement LegendItem(Color c, string text)
        {
            var e = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 9 } };
            e.Add(new VisualElement { style = { width = 12, height = 9, backgroundColor = c, marginRight = 4 } });
            e.Add(new Label(text));
            return e;
        }

        private void PlaceBadges()
        {
            for (int i = 0; i < ProvinceIds.Count; i++)
            {
                if (armyBadges[i].style.display == DisplayStyle.None) continue;
                var p = ProvinceToPanel(i);
                armyBadges[i].style.left = p.x + (Queries.IsCapital(i) ? 30 : 0); armyBadges[i].style.top = p.y + (Queries.IsCapital(i) ? -24 : 14);
            }
        }

        // ---- the followed country's own bar: the groundwork for rule mode's ----

        public void ShowFollowBar(ReplayState s)
        {
            followBar.Clear();
            bool on = Followed != null;
            nations.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
            Root.EnableInClassList("following", on && Writs);
            followBar.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) return;
            var me = Followed; var c = s.Countries[me]; var look = CountryLook.Of(me);
            int n = s.ProvinceCount(me), net = c.Income - c.Upkeep;
            var head = new VisualElement(); head.AddToClassList("nation"); head.AddToClassList("me");
            head.tooltip = Fog.On ? $"You rule {look.Name}. Click: your panel. Your rivals are on the right." : $"Following {look.Name}. Click: its panel. The other rulers are on the right.";
            head.RegisterCallback<ClickEvent>(_ => CountryPressed?.Invoke(me));
            if (Writs)
            {
                // Variant B (#20): the realm's banner hangs below the bar, the ruler's portrait frame beside it.
                var banner = new RealmBanner(look) { tooltip = head.tooltip }; banner.RegisterCallback<ClickEvent>(_ => CountryPressed?.Invoke(me));
                followBar.Add(banner);
                followBar.Add(new PortraitSlot(58, 66, $"{look.Name}'s ruler: a portrait will hang here") { name = "my-portrait" });
                StatBar(s, me, c, look, n, net);
                RenderOutliner(s);
                return;
            }
            head.Add(new Crest(look, 30) { name = "my-crest" }); var nm = new Label(look.Name.ToUpperInvariant()); nm.AddToClassList("nation-name"); nm.AddToClassList("my-name"); head.Add(nm);
            followBar.Add(head);
            Segment("PROVINCES", $"{n} / {WinAt}", Conquest ? $"{n} of {ProvinceIds.Count} provinces. Conquest: the last realm standing wins." : $"{n} of {ProvinceIds.Count} provinces; {WinAt} wins. {Math.Max(0, WinAt - n)} to go.");
            int hostCount = s.Armies.Count(a => a.Owner == me);
            Segment("ARMY", Writs ? $"{c.Army} in {hostCount} {(hostCount == 1 ? "army" : "armies")}" : c.Army.ToString(), Writs
                ? $"{c.Army} soldiers in {hostCount} {(hostCount == 1 ? "army" : "armies")}. A province with no army defends with its militia."
                : $"{c.Army} soldiers. A province of ours defends with 70% of it.");
            Segment("TREASURY", $"{c.Gold}  {net:+0;-0} a turn", $"{c.Gold} gold, {net:+0;-0} a turn. Below zero, 15% of the army deserts.");
            Segment("INCOME", $"+{c.Income}", $"10 gold for each of {n} provinces.");
            Segment("UPKEEP", $"-{c.Upkeep}", $"1 gold per 2 soldiers: {c.Army} soldiers.");
            Segment("STABILITY", $"{c.Stability} of 5", "Rises 1 a turn without a tax rise. At 1, a province may revolt.");
            var wars = CountryIds.Where(o => o != me && s.Relation(me, o)?.Kind == "war").ToList();
            var truces = CountryIds.Where(o => o != me && s.Relation(me, o)?.Kind == "truce").ToList();
            Segment(Writs && wars.Count > 0 ? "WARS · WAR SCORE" : "WARS", wars.Count == 0 ? "none" : string.Join(", ", wars.Select(o => Writs ? $"{CountryLook.NameOf(o)} {s.WarScore(me, o):+0;-0;0}" : CountryLook.NameOf(o))),
                Writs ? "Whom it is at war with, and the war score from our side, -100 to +100: what we occupy of theirs against what they occupy of ours, battles won, their capital. A treaty's terms cost war score." : "Whom it is at war with.");
            Segment("TRUCES", truces.Count == 0 ? "none" : string.Join(", ", truces.Select(CountryLook.NameOf)), "Truces: no war may be declared until they end.");
            Segment("BUILDINGS", "—", "Room for province development (decision 12). Not in the game yet.").AddToClassList("later");
            Segment("TRADE", "—", "Room for trade and tribute (decision 9). Not in the game yet.").AddToClassList("later");
            var rivals = new VisualElement(); rivals.AddToClassList("rivals");
            foreach (var o in CountryIds.Where(o => o != me).OrderByDescending(o => s.ProvinceCount(o)))
            {
                var r = s.Relation(me, o)?.Kind;
                var chip = new VisualElement(); chip.AddToClassList("riv"); if (r != null) chip.AddToClassList(r);
                chip.tooltip = $"{CountryLook.NameOf(o)}: {s.ProvinceCount(o)} provinces, {(r == "war" ? "at war with " + look.Name : r == "truce" ? "at truce with " + look.Name : "at peace with " + look.Name)}. Click: its panel.";
                chip.RegisterCallback<ClickEvent>(_ => CountryPressed?.Invoke(o));
                chip.Add(new Crest(CountryLook.Of(o), 15)); chip.Add(new Label(s.ProvinceCount(o).ToString()) { style = { marginLeft = 3 } });
                rivals.Add(chip);
            }
            followBar.Add(rivals);
            if (Fog.On) return; // rule mode: the bar is your own country, for the whole game
            var stop = new Button(() => FollowPressed?.Invoke(null)) { text = "×", tooltip = "Stop following: every country again" }; stop.AddToClassList("tbtn"); stop.style.marginLeft = 4; followBar.Add(stop);
        }

        // The UI draft (#20): each number with its icon and colour, the breakdown in its tooltip, rivals on the right.
        private static readonly Dictionary<string, (IconKind icon, string cls, string caption)> StatLook = new Dictionary<string, (IconKind, string, string)>
        {
            ["gold"] = (IconKind.Gold, "st-gold", "gold"), ["army"] = (IconKind.Army, "st-army", "soldiers"), ["land"] = (IconKind.Province, "st-land", "provinces"),
            ["writ"] = (IconKind.Seal, "st-writ", "writs left"), ["stab"] = (IconKind.Scales, "st-stab", "stability"), ["war"] = (IconKind.Swords, "st-war", "wars"), ["truce"] = (IconKind.Truce, "st-truce", "truces"),
        };

        private void StatBar(ReplayState s, string me, CountryState c, CountryLook look, int n, int net)
        {
            (int, int)? writs = OrdersRequest != null && OrdersRequest.CountryId == me && OrdersRequest.Writs != GameRules.UnlimitedWrits ? (OrdersRequest.Writs, WritsUsed(OrdersRequest)) : ((int, int)?)null;
            foreach (var st in ScreenModels.TopBar(s, me, Queries?.Rules?.IncomePerProvince ?? 10, Conquest, WinAt, writs, CountryLook.NameOf, Cap))
            {
                var (icon, cls, caption) = StatLook[st.Key];
                var e = new VisualElement { tooltip = st.Tip }; e.AddToClassList("bstat"); e.AddToClassList(cls); if (st.Key == "gold") e.AddToClassList("first");
                var i = new Icon(icon, 30); i.AddToClassList("bstat-icon"); e.Add(i);
                var col = new VisualElement(); col.AddToClassList("bstat-col");
                var line = new VisualElement(); line.AddToClassList("bstat-line");
                var v = new Label(st.Value); v.AddToClassList("bstat-value"); line.Add(v);
                var u = new Label(st.Sub); u.AddToClassList("bstat-sub"); if (st.Tone > 0) u.AddToClassList("good"); else if (st.Tone < 0) u.AddToClassList("bad"); line.Add(u);
                col.Add(line);
                var cap = new Label(caption.ToUpperInvariant()); cap.AddToClassList("bstat-cap"); col.Add(cap);
                e.Add(col);
                if (st.Key == "stab")
                {
                    var pips = new VisualElement(); pips.AddToClassList("pips"); pips.style.flexDirection = FlexDirection.Row;
                    for (int k = 1; k <= 5; k++) { var pip = new VisualElement(); pip.AddToClassList("pip"); pip.EnableInClassList("on", k <= c.Stability); pips.Add(pip); }
                    e.Add(pips);
                }
                followBar.Add(e);
            }
            RivalCrests(s, me, look);
        }

        private void RivalCrests(ReplayState s, string me, CountryLook look)
        {
            var rivals = new VisualElement(); rivals.AddToClassList("rivals");
            foreach (var o in CountryIds.Where(o => o != me).OrderByDescending(o => s.ProvinceCount(o)))
            {
                var r = s.Relation(me, o)?.Kind;
                var chip = new VisualElement(); chip.AddToClassList("riv"); if (r != null) chip.AddToClassList(r);
                chip.tooltip = $"{CountryLook.NameOf(o)}: {s.ProvinceCount(o)} provinces, {(r == "war" ? $"at war with you, war score {s.WarScore(me, o):+0;-0;0}" : r == "truce" ? "at truce with you" : "at peace with you")}. Click: its panel.";
                chip.RegisterCallback<ClickEvent>(_ => CountryPressed?.Invoke(o));
                chip.Add(new Crest(CountryLook.Of(o), 26));
                if (r == "war") { var x = new Icon(IconKind.Swords, 10); x.AddToClassList("riv-war"); chip.Add(x); }
                chip.Add(new Label(s.ProvinceCount(o).ToString()) { style = { marginLeft = 3 } });
                rivals.Add(chip);
            }
            followBar.Add(rivals);
        }

        /// <summary>Opens the inspector on the newest decision you may read (the top bar's Decisions, or I).</summary>
        public Action InspectPressed;

        private VisualElement Segment(string label, string value, string tip)
        {
            var e = new VisualElement { tooltip = tip }; e.AddToClassList("seg2");
            var l = new Label(label); l.AddToClassList("seg2-label"); e.Add(l);
            var v = new Label(value); v.AddToClassList("seg2-value"); e.Add(v);
            followBar.Add(e);
            return e;
        }

        // ---- the side panel: a province or a country ----

        public void CloseSide() { side.RemoveFromClassList("on"); Root.RemoveFromClassList("side-open"); }
        public bool SideOpen => side.ClassListContains("on");
        /// <summary>The panel-space x where the free map area begins: right of the realm list, or of an open side panel.</summary>
        public float FreeLeft => SideOpen ? side.layout.xMax + 10 : (Root.ClassListContains("compact") ? 50 : 248);

        private VisualElement SideHead(string title)
        {
            side.Clear();
            var head = new VisualElement(); head.AddToClassList("panel-head");
            // The left panel holds one thing at a time: back to your realm, or close.
            var back = new Button(CloseSide) { text = "‹ Realm", tooltip = "Back to the realm (Esc)" }; back.AddToClassList("tbtn"); back.AddToClassList("side-back"); head.Add(back);
            var t = new Label(title); t.AddToClassList("panel-title"); head.Add(t);
            var close = new Button(CloseSide) { text = "×", tooltip = "Close (Esc)" }; close.AddToClassList("tbtn"); head.Add(close);
            side.Add(head);
            var body = new ScrollView(); body.style.flexGrow = 1; side.Add(body);
            Root.RemoveFromClassList("ledger-open");
            side.AddToClassList("on");
            Root.AddToClassList("side-open");
            return body;
        }

        /// <summary>
        /// An army in the left panel: who leads it, what it is made of, its stores, what it is doing and where. A rival's
        /// shows what your court sees of it, as its piece does.
        /// </summary>
        public void ShowArmy(string id, ReplayState s, IEnumerable<ReplayEvent> history)
        {
            var a = s?.Army(id);
            if (a == null) return;
            bool rival = Fog.On && a.Owner != You;
            var body = SideHead("ARMY");
            var hdr = new VisualElement(); hdr.AddToClassList("insp-head");
            hdr.Add(new Crest(CountryLook.Of(a.Owner), 30));
            var hb = new VisualElement { style = { marginLeft = 10 } };
            var name = new Label(Chronicle.Host(a.Id)); name.AddToClassList("insp-title"); hb.Add(name);
            var sub = new Label($"{CountryLook.NameOf(a.Owner)} · {(rival ? ArmySight.About(a.Soldiers) : a.Soldiers.ToString())} soldiers"); sub.AddToClassList("insp-sub"); hb.Add(sub);
            hdr.Add(hb); body.Add(hdr);

            var str = Sec(body, "STRENGTH", rival ? "what your court sees" : "");
            if (a.HasMix && !rival) { str.Add(new MixBars(a)); str.Add(KeyValue("Made of", ArmyLook.Mix(a))); }
            else if (a.HasMix) str.Add(KeyValue("Mostly", ArmyLook.Mostly(a) ?? "not known"));
            str.Add(KeyValue("Soldiers", rival ? ArmySight.About(a.Soldiers) : a.Soldiers.ToString()));
            if (ArmyLook.Leader(a) is string leader)
            {
                var gen = Sec(body, "GENERAL", "");
                var gt = new Label(ReplayBuilder.Title(a)); gt.AddToClassList("insp-title"); gt.style.fontSize = 14; gen.Add(gt);
                gen.Add(Note(leader));
            }
            if (!rival && a.HasStores)
            {
                var sup = Sec(body, "STORES", a.Hungry ? "none left" : "");
                sup.Add(Note(ArmyLook.Stores(a)));
            }
            var doing = Sec(body, "ORDERS", "");
            string what = a.Marching ? $"Marching to {Cap(a.Destination)}" + (TurnsLeft != null ? $", {TurnsLeft(a)} turn{(TurnsLeft(a) == 1 ? "" : "s")}" : "") + "."
                : a.Awaiting ? $"Awaiting orders at {Cap(a.Location)}: {string.Join(", ", a.Wake)}." : $"Holding at {Cap(a.Location)}.";
            doing.Add(Note(what));
            int at = IndexOf(a.Location);
            if (ArmyLook.SiegeTag(s, at) is string siege && s.SiegedBy[at] == a.Owner) doing.Add(KeyValue("Besieging", $"{Cap(a.Location)}: {siege}"));
            var where = new Label($"At {Cap(a.Location)}: click to open the province") { tooltip = "Open the province" }; where.AddToClassList("list-more");
            where.RegisterCallback<ClickEvent>(_ => { if (at >= 0) ProvincePressed?.Invoke(at); });
            doing.Add(where);
            var fought = history.Where(e => e.Kind == ReplayEventKind.Battle && e.Battle != null && (e.Battle.AttackerArmies.Contains(id) || e.Battle.DefenderArmies.Contains(id))).ToList();
            if (fought.Count > 0)
            {
                var bat = Sec(body, "BATTLES", fought.Count.ToString());
                foreach (var e in fought.AsEnumerable().Reverse().Take(8))
                {
                    var row = new VisualElement(); row.AddToClassList("row"); row.AddToClassList("wraps");
                    row.Add(new Icon(IconKind.Swords, 12) { Tint = (e.Battle.AttackerWon == e.Battle.AttackerArmies.Contains(id)) ? new Color32(150, 200, 120, 255) : new Color32(226, 110, 90, 255) });
                    row.Add(new Label($"Turn {e.Turn} · {Cap(e.Battle.Province ?? "the border")} · {(e.Battle.AttackerWon == e.Battle.AttackerArmies.Contains(id) ? "won" : "lost")}") { style = { marginLeft = 6 } });
                    var ev = e; row.tooltip = "Click: the battle report"; row.RegisterCallback<ClickEvent>(_ => ShowBattle(ev));
                    bat.Add(row);
                }
            }
        }

        public void ShowProvince(int p, ReplayState s, IEnumerable<ReplayEvent> history)
        {
            string id = ProvinceIds[p], owner = s.Owners[p];
            var body = SideHead(Cap(id).ToUpperInvariant());
            bool capital = Queries.IsCapital(p);
            var hdr = new VisualElement(); hdr.AddToClassList("insp-head");
            if (owner != null) hdr.Add(new Crest(CountryLook.Of(owner), 30));
            var hb = new VisualElement { style = { marginLeft = 10 } };
            var name = new Label(Cap(id)); name.AddToClassList("insp-title"); hb.Add(name);
            var sub = new Label(owner == null ? "Neutral land" : CountryLook.NameOf(owner) + (capital ? "'s capital" : "'s province")); sub.AddToClassList("insp-sub"); hb.Add(sub);
            hdr.Add(hb); body.Add(hdr);

            bool known = Fog.KnowsProvince(s, p);
            var hold = Sec(body, "HOLDING", known ? "" : "beyond your borders");
            hold.Add(KeyValue("Owner", owner == null ? "nobody" : CountryLook.NameOf(owner)));
            if (owner == null) hold.Add(KeyValue("Garrison", known ? s.Garrisons[p].ToString() : "not known"));
            else hold.Add(KeyValue("Defended by", $"70% of {s.Countries[owner].Army} soldiers"));
            hold.Add(KeyValue("Forts", known ? $"{s.Forts[p]} (+{s.Forts[p] * Queries.Rules.FortBonus})" : "not known"));
            if (capital) hold.Add(KeyValue("Capital", $"+{Queries.Rules.CapitalBonus}"));
            hold.Add(KeyValue("Defends with about", known ? Queries.Defence(s, p).ToString() : "not known"));
            if (!known) hold.Add(Note("Your court knows the garrisons and forts of your own provinces and of those touching your land."));
            hold.Add(KeyValue("Income", owner == null ? "none" : $"{Queries.Rules.IncomePerProvince} gold a turn"));
            var nb = new Label("Borders: " + string.Join(", ", Queries.Neighbours(p).Select(q => Cap(ProvinceIds[q])))); nb.AddToClassList("why-no"); hold.Add(nb);
            LandSection(body, id);


            if (Fog.On) ProvinceOrders(body, p);
            var moves = Fog.On ? new VisualElement() : Sec(body, "WHO CAN ATTACK IT", "now");
            // The realms that can, each with its odds; then those that cannot, one line for every realm too far away.
            var rivals = CountryIds.Where(c => c != owner && s.Countries[c].Alive).Select(c => (c, why: Queries.WhyNotAttack(s, c, p))).ToList();
            foreach (var (c, why) in rivals.Where(x => x.why == null))
                moves.Add(AttackRow(c, $"{CountryLook.NameOf(c)} · wins {Math.Round(Queries.WinChance(s, c, p) * 100)}% of rolls", false));
            foreach (var (c, why) in rivals.Where(x => x.why != null && !RealmQueries.IsFarReason(x.why)))
                moves.Add(AttackRow(c, $"{CountryLook.NameOf(c)} · {Lowered(Words.Plain(why)).TrimEnd('.')}", true));
            int far = rivals.Count(x => x.why != null && RealmQueries.IsFarReason(x.why));
            if (far > 0) moves.Add(Note(rivals.Count(x => x.why == null) + rivals.Count(x => x.why != null && !RealmQueries.IsFarReason(x.why)) == 0
                ? (far == 1 ? "No realm can: the only other one is not next to it." : $"No realm can: none of the {far} others is next to it.")
                : $"{far} {(far == 1 ? "other cannot" : "others cannot")}: not next to it."));
            if (rivals.Count == 0) moves.Add(Note("No realm is left to attack it."));

            var events = history.Where(e => e.ProvinceIndex == p).ToList();
            if (events.Count > 0)
            {
                var hist = Sec(body, "HISTORY", "");
                foreach (var e in events)
                {
                    var line = Chronicle.Line(e, CountryLook.NameOf);
                    if (line == null) continue;
                    var row = new VisualElement(); row.AddToClassList("row"); row.AddToClassList("wraps");
                    row.Add(new Crest(CountryLook.Of(e.Country), 15));
                    row.Add(new Label($"Turn {e.Turn} · {line.Text}") { style = { marginLeft = 6, whiteSpace = WhiteSpace.Normal, flexShrink = 1 } });
                    if (e.Decision != null && Fog.KnowsDecision(e.Decision.AgentId, GameOver)) { row.tooltip = "Click: the decision"; row.RegisterCallback<ClickEvent>(_ => DecisionPressed?.Invoke(e)); }
                    hist.Add(row);
                }
            }
        }

        private static string Lowered(string t) => string.IsNullOrEmpty(t) ? t : char.ToLowerInvariant(t[0]) + t.Substring(1);

        private static VisualElement AttackRow(string country, string text, bool cannot)
        {
            var row = new VisualElement(); row.AddToClassList("row"); row.AddToClassList("wraps");
            row.Add(new Crest(CountryLook.Of(country), 15));
            var t = new Label(text) { style = { whiteSpace = WhiteSpace.Normal, flexShrink = 1 } }; t.style.marginLeft = 6; if (cannot) t.AddToClassList("muted");
            row.Add(t);
            return row;
        }

        /// <summary>A generated world's map data, for the province panel's land; null on the Classic board.</summary>
        public WorldMap Land;

        // The land itself, as the map describes it: what the chart draws there, and a special place's story. Every
        // court knows it, as every ruler knows the map.
        private void LandSection(VisualElement body, string id)
        {
            var land = Land?.Provinces.FirstOrDefault(x => x.Id == id);
            if (land == null) return;
            var sec = Sec(body, "THE LAND", "from the map");
            sec.Add(KeyValue("Terrain", land.Terrain.ToString().ToLowerInvariant() + (land.Port ? ", a port" : land.Coastal ? ", on the coast" : "")));
            var region = Land.Regions.FirstOrDefault(r => r.Id == land.Region);
            if (region != null) sec.Add(KeyValue("Region", region.Name ?? region.Id));
            int tier = Chart.SettlementLook.TierOf(land);
            sec.Add(KeyValue("Settlement", Chart.SettlementLook.Name(tier)));
            sec.Add(KeyValue("Development", $"{Chart.ChartLayout.DevelopmentOf(land)} of {Development.Max}"));
            sec.Add(KeyValue("Population", land.Population.ToString()));
            if (!string.IsNullOrEmpty(land.Good)) sec.Add(KeyValue("Goods", Cap(land.Good)));
            sec.Add(Note("The bigger the settlement, the more of the land around it is farmed."));
            var special = Land.Specials?.FirstOrDefault(x => x.Province == id);
            if (special != null)
            {
                var sp = Sec(body, "A SPECIAL PLACE", (special.Template ?? "").Replace('-', ' ').Replace('_', ' '));
                var t = new Label(special.Name); t.AddToClassList("insp-title"); sp.Add(t);
                if (!string.IsNullOrEmpty(special.Story)) sp.Add(Note(special.Story));
            }
        }

        public void ShowCountry(string id, ReplayState s, IEnumerable<ReplayEvent> history, string personality, IReadOnlyList<string> knowledge)
        {
            var look = CountryLook.Of(id); var player = Players?.GetValueOrDefault(id);
            var body = SideHead(look.Name.ToUpperInvariant());
            var hdr = new VisualElement(); hdr.AddToClassList("insp-head");
            var face = new Image { image = portraits, uv = Faces.Uv(player?.Persona, look.Slot % 2), scaleMode = ScaleMode.ScaleAndCrop };
            face.AddToClassList("insp-face"); face.style.borderTopColor = face.style.borderBottomColor = face.style.borderLeftColor = face.style.borderRightColor = (Color)look.Colour;
            hdr.Add(face);
            var hb = new VisualElement { style = { marginLeft = 10, flexShrink = 1 } };
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(new Crest(look, 18)); var name = new Label(look.Name.ToUpperInvariant()); name.AddToClassList("insp-title"); row.Add(name);
            hb.Add(row);
            var sub = new Label($"The {player?.Persona ?? "ruler"} · {player?.Model}"); sub.AddToClassList("insp-sub"); hb.Add(sub);
            if (Fog.On) { hdr.Add(hb); body.Add(hdr); CountryBody(body, id, s, history, personality, knowledge); return; }
            var follow = new Button(() => FollowPressed?.Invoke(Followed == id ? null : id)) { text = Followed == id ? "Following" : "Follow", tooltip = "Follow: the top bar shows this country's full values, and the reach mode its attacks" };
            follow.AddToClassList("tbtn"); follow.style.width = StyleKeyword.Auto; follow.style.paddingLeft = 8; follow.style.paddingRight = 8; follow.style.marginTop = 4;
            if (Followed == id) follow.AddToClassList("on");
            hb.Add(follow);
            hdr.Add(hb); body.Add(hdr);
            CountryBody(body, id, s, history, personality, knowledge);
        }

        private void CountryBody(VisualElement body, string id, ReplayState s, IEnumerable<ReplayEvent> history, string personality, IReadOnlyList<string> knowledge)
        {
            var c = s.Countries[id];
            if (Fog.On && id != You) CountryOrders(body, id);
            if (id == You)
            {
                var you = Sec(body, "RULER", "you");
                you.Add(Note("You rule this country. Your council is offered the moves a model ruler would be offered here, by the same rules."));
            }
            else
            {
                var ruler = Sec(body, "RULER", "persona, not country");
                var p = new Label(personality ?? ""); p.AddToClassList("insp-sub"); ruler.Add(p);
                if (knowledge != null) foreach (var k in knowledge) { var kl = new Label("· " + k); kl.AddToClassList("why-no"); ruler.Add(kl); }
            }

            var tre = Sec(body, "TREASURY", "");
            if (Fog.KnowsTreasury(id))
            {
                tre.Add(KeyValue("Gold", c.Gold.ToString()));
                tre.Add(KeyValue("Income", "+" + c.Income));
                tre.Add(KeyValue("Upkeep (1 per 2 soldiers)", "-" + c.Upkeep));
            }
            tre.Add(KeyValue("Army", c.Army.ToString()));
            if (Fog.KnowsTreasury(id)) tre.Add(KeyValue("Stability", $"{c.Stability} of 5"));
            else tre.Add(Note("Its treasury, income and stability are not known to your court. A model ruler is told every rival's army and land, and nothing more."));

            if (id == You && Fog.On) return;

            var last = history.LastOrDefault(e => e.Decision != null && e.Decision.AgentId == id && !e.Decision.IsEnvoy);
            if (last != null && Fog.KnowsReport(id, GameOver))
            {
                var card = DecisionCard.From(last);
                var told = Sec(body, "WHAT IT READ LAST", $"turn {card.Turn}");
                var f = new VisualElement(); f.AddToClassList("facts-box"); f.Add(Mono(string.Join("\n", card.Standing))); told.Add(f);
                var off = Sec(body, "OFFERED THAT TURN", $"{card.Offered.Count} moves");
                var chips = new VisualElement(); chips.AddToClassList("chips");
                for (int i = 0; i < card.Offered.Count; i++) { var ch = new Label(card.Offered[i]); ch.AddToClassList("mchip"); if (i == card.Picked) ch.AddToClassList("pick"); chips.Add(ch); }
                off.Add(chips);
            }
            var decs = Sec(body, "DECISIONS", "click one");
            foreach (var e in history.Where(e => e.Decision != null && e.Decision.AgentId == id).Reverse().Take(12))
            {
                var r = new VisualElement(); r.AddToClassList("row"); r.tooltip = "Click: the decision";
                r.Add(new Icon(IconKind.Eye, 11));
                var d = e.Decision;
                r.Add(new Label($"t{d.Turn} · {d.Moment} · {d.Action}{(d.HasTarget ? " " + d.Target : "")}") { style = { marginLeft = 6 } });
                r.RegisterCallback<ClickEvent>(_ => DecisionPressed?.Invoke(e));
                decs.Add(r);
            }
        }

        private static VisualElement Sec(VisualElement body, string title, string note)
        {
            var s = new VisualElement(); s.AddToClassList("sec");
            var h = new VisualElement(); h.AddToClassList("sec-head");
            h.Add(new Label(title));
            if (!string.IsNullOrEmpty(note)) { var n = new Label(note); n.AddToClassList("sec-note"); h.Add(n); }
            s.Add(h); body.Add(s);
            return s;
        }

        // ---- diplomacy ----

        public bool DiplomacyOpen => diplomacy.ClassListContains("on");
        public void CloseDiplomacy() => diplomacy.RemoveFromClassList("on");

        public void ShowDiplomacy(ReplayState s, int turn, IEnumerable<ReplayEvent> envoysThisTurn)
        {
            diplomacy.Clear();
            var head = new VisualElement(); head.AddToClassList("panel-head");
            head.Add(new Icon(IconKind.Peace, 12) { style = { marginRight = 7 } });
            var t = new Label("DIPLOMACY"); t.AddToClassList("panel-title"); head.Add(t);
            var close = new Button(CloseDiplomacy) { text = "×", tooltip = "Close (Esc)" }; close.AddToClassList("tbtn"); head.Add(close);
            diplomacy.Add(head);
            var grid = new VisualElement { style = { paddingLeft = 10, paddingRight = 10, paddingTop = 8, paddingBottom = 8 } };
            var hr = new VisualElement(); hr.AddToClassList("mx-row"); hr.Add(new VisualElement { style = { width = 110 } });
            foreach (var b in CountryIds) { var cell = new VisualElement(); cell.AddToClassList("mx-head"); cell.Add(new Crest(CountryLook.Of(b), 16)); cell.Add(new Label(CountryLook.NameOf(b))); hr.Add(cell); }
            grid.Add(hr);
            foreach (var a in CountryIds)
            {
                var row = new VisualElement(); row.AddToClassList("mx-row");
                var rh = new VisualElement(); rh.AddToClassList("mx-rowhead"); rh.Add(new Crest(CountryLook.Of(a), 16)); rh.Add(new Label(CountryLook.NameOf(a)) { style = { marginLeft = 6 } }); row.Add(rh);
                foreach (var b in CountryIds)
                {
                    var cell = new VisualElement(); cell.AddToClassList("mx-cell");
                    if (a == b) { cell.AddToClassList("self"); row.Add(cell); continue; }
                    var (kind, since, left, border) = Queries.Between(s, a, b);
                    cell.AddToClassList(kind);
                    cell.Add(new Icon(kind == "war" ? IconKind.War : kind == "truce" ? IconKind.Truce : IconKind.Peace, 13));
                    cell.Add(new Label(kind == "war" ? (Writs ? $"war {s.WarScore(a, b):+0;-0;0}" : $"war · t{since}") : kind == "truce" ? $"truce · {left}" : "peace") { style = { fontSize = 10 } });
                    if (border) cell.Add(new Label("border") { style = { fontSize = 9, color = new Color(0.55f, 0.5f, 0.4f) } });
                    cell.tooltip = $"{CountryLook.NameOf(a)} and {CountryLook.NameOf(b)}: " + (kind == "war" ? $"at war since turn {since}" + (Writs ? $", war score {s.WarScore(a, b):+0;-0;0} from {CountryLook.NameOf(a)}'s side" : "") : kind == "truce" ? $"truce, {left} turns left" : "at peace")
                                   + (border ? ". They share a border." : ". No shared border: war cannot be declared.");
                    row.Add(cell);
                }
                grid.Add(row);
            }
            diplomacy.Add(grid);
            var env = new VisualElement(); env.AddToClassList("sec");
            env.Add(new Label("THIS TURN'S ENVOYS") { style = { fontSize = 10, color = new Color(0.55f, 0.5f, 0.4f), letterSpacing = 2 } });
            var list = envoysThisTurn.ToList();
            if (list.Count == 0) env.Add(Note($"None on turn {turn}. Envoys are answered before any move lands; an accepted offer is a 5-turn truce."));
            foreach (var e in list) env.Add(Note(Chronicle.Line(e, CountryLook.NameOf)?.Text ?? e.Text));
            diplomacy.Add(env);
            diplomacy.AddToClassList("on");
        }
    }
}
