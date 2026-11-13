using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Everything on the screen except the map itself, in UI Toolkit: the top bar, the realm list,
    /// the chronicle, the labels that follow the map, the speech banner and the battle marks.
    /// It only shows; <see cref="ReplayDirector"/> decides what and when.
    /// </summary>
    public sealed partial class ScreenUI
    {
        public readonly VisualElement Root;
        private readonly VisualElement nations, realmBody, chronicleBody, labels, fx, filters;
        private readonly Label turnLabel, phaseLabel;
        private readonly Button playButton, stepButton, speedButton;
        private readonly Icon playIcon;
        private readonly Dictionary<string, Label> provinceCount = new Dictionary<string, Label>();
        private readonly List<ChronicleLine> lines = new List<ChronicleLine>();
        private readonly HashSet<string> countryFilter;
        private readonly HashSet<ChronicleCategory> categoryFilter = new HashSet<ChronicleCategory>((ChronicleCategory[])Enum.GetValues(typeof(ChronicleCategory)));
        private readonly Label[] provinceLabels;
        private readonly VisualElement[] fortMarks;
        private readonly Texture2D portraits;
        private bool showQuotes = true;

        public Func<int, Vector2> ProvinceToPanel;
        public Action PlayPressed, StepPressed, SpeedPressed;
        public Action<ChronicleLine> LinePressed;
        public Action<string> CountryPressed;
        public IReadOnlyList<string> CountryIds;
        public IReadOnlyList<string> ProvinceIds;
        public IReadOnlyDictionary<string, RecordedPlayer> Players;

        public ScreenUI(VisualElement root, StyleSheet style, Texture2D portraits, IReadOnlyList<string> countryIds, IReadOnlyList<string> provinceIds,
            IReadOnlyDictionary<string, RecordedPlayer> players, string title)
        {
            this.portraits = portraits;
            CountryIds = countryIds;
            ProvinceIds = provinceIds;
            provinceLabels = new Label[provinceIds.Count];
            fortMarks = new VisualElement[provinceIds.Count];
            Players = players;
            countryFilter = new HashSet<string>(countryIds);
            Root = root;
            // More than the Classic five: the top bar keeps each country's crest and numbers, its name in the tooltip.
            root.EnableInClassList("crowded", countryIds.Count > 5);
            root.styleSheets.Add(style);
            root.AddToClassList("root");
            root.pickingMode = PickingMode.Ignore;

            labels = new VisualElement { name = "labels", pickingMode = PickingMode.Ignore };
            labels.AddToClassList("labels");
            root.Add(labels);
            fx = new VisualElement { name = "fx", pickingMode = PickingMode.Ignore };
            fx.AddToClassList("labels");
            root.Add(fx);

            // ---- top bar ----
            var top = new VisualElement(); top.AddToClassList("topbar");
            var brand = new Label("PAINTED REALM"); brand.AddToClassList("brand");
            brand.tooltip = "Working title. Five rulers, each a framework agent, play for 25 provinces. " + title;
            top.Add(brand);
            nations = new VisualElement(); nations.AddToClassList("nations"); top.Add(nations);
            var time = new VisualElement(); time.AddToClassList("time");
            time.Add(BuildThinking());
            phaseLabel = new Label("Council"); phaseLabel.AddToClassList("phase"); time.Add(phaseLabel);
            turnLabel = new Label("Turn 1 of 30"); turnLabel.AddToClassList("turn"); time.Add(turnLabel);
            playButton = new Button(() => PlayPressed?.Invoke()) { tooltip = "Play or pause (Space)" }; playButton.AddToClassList("tbtn");
            playIcon = new Icon(IconKind.Pause, 12); playButton.Add(playIcon); time.Add(playButton);
            stepButton = new Button(() => StepPressed?.Invoke()) { tooltip = "Step: land the next move (N)" }; stepButton.AddToClassList("tbtn");
            stepButton.Add(new Icon(IconKind.Step, 12)); time.Add(stepButton);
            speedButton = new Button(() => SpeedPressed?.Invoke()) { tooltip = "Speed: 1×, 2×, 4× (+)" }; speedButton.AddToClassList("tbtn");
            speedButton.style.flexDirection = FlexDirection.Row; speedButton.style.width = 34;
            for (int i = 0; i < 3; i++) { var pip = new VisualElement(); pip.AddToClassList("speed-pip"); speedButton.Add(pip); }
            time.Add(speedButton);
            top.Add(time);
            root.Add(top);

            // ---- realm ----
            var realm = Panel("REALM", "realm");
            realmBody = new ScrollView(); realm.Add(realmBody);
            root.Add(realm);

            // ---- chronicle ----
            var chronicle = Panel("CHRONICLE", "chronicle");
            var quotes = new Button(() => { showQuotes = !showQuotes; RenderChronicle(null); }) { text = "“", tooltip = "Show or hide the rulers' words" };
            quotes.AddToClassList("tbtn");
            chronicle.Q(className: "panel-head").Add(quotes);
            filters = new VisualElement(); filters.AddToClassList("filters"); chronicle.Add(filters);
            chronicleBody = new ScrollView(); chronicleBody.style.flexGrow = 1; chronicle.Add(chronicleBody);
            root.Add(chronicle);
            RenderFilters();

            root.Add(BuildInspector());
            BuildTooltips();

            // ---- the map's labels ----
            for (int i = 0; i < provinceIds.Count; i++)
            {
                provinceLabels[i] = new Label(Cap(provinceIds[i])) { pickingMode = PickingMode.Ignore };
                provinceLabels[i].AddToClassList("plab");
                labels.Add(provinceLabels[i]);
                fortMarks[i] = new VisualElement { pickingMode = PickingMode.Ignore };
                fortMarks[i].AddToClassList("forts");
                labels.Add(fortMarks[i]);
            }
        }

        private static VisualElement Panel(string title, string cls)
        {
            var p = new VisualElement(); p.AddToClassList("panel"); p.AddToClassList(cls);
            var head = new VisualElement(); head.AddToClassList("panel-head");
            var t = new Label(title); t.AddToClassList("panel-title"); head.Add(t);
            p.Add(head);
            return p;
        }

        public static string Cap(string id) => string.IsNullOrEmpty(id) ? "" : char.ToUpperInvariant(id[0]) + id.Substring(1);

        // ---- top bar and realm ----

        /// <summary>Below 1600 px wide the top bar keeps crest, name, provinces, army and wars; the rest is in its tooltip.</summary>
        public void SetCompact(bool compact) => Root.EnableInClassList("compact", compact);

        /// <summary>The rules' numbers the screen shows: the Classic board's 15 of 25 in 30 turns, or a generated world's.</summary>
        public int WinAt = 15, MaxTurns = 30;

        public void SetTurn(int turn, string phase) { turnLabel.text = $"Turn {turn} of {MaxTurns}"; phaseLabel.text = phase; }

        public void SetPlaying(bool playing, float speed)
        {
            playButton.Remove(playButton[0]);
            playButton.Add(new Icon(playing ? IconKind.Pause : IconKind.Play, 12));
            int n = speed >= 4 ? 3 : speed >= 2 ? 2 : 1;
            for (int i = 0; i < 3; i++) speedButton[i].EnableInClassList("on", i < n);
        }

        public void ShowState(ReplayState s, string flipCountry = null)
        {
            lastState = s;
            nations.Clear();
            provinceCount.Clear();
            foreach (var id in CountryIds.OrderByDescending(id => s.ProvinceCount(id)))
            {
                var c = s.Countries[id];
                var look = CountryLook.Of(id);
                int n = s.ProvinceCount(id), net = c.Income - c.Upkeep;
                var wars = CountryIds.Where(o => o != id && s.Relation(id, o)?.Kind == "war").ToList();
                var chip = new VisualElement(); chip.AddToClassList("nation");
                if (!c.Alive) chip.AddToClassList("out");
                chip.tooltip = $"{look.Name} · the {Players?.GetValueOrDefault(id)?.Persona ?? "?"} · {Players?.GetValueOrDefault(id)?.Model}\n" +
                               $"Provinces {n} of {ProvinceIds.Count} ({WinAt} wins)\nArmy {c.Army}, upkeep {c.Upkeep} a turn\nTreasury {c.Gold} gold: income {c.Income}, upkeep {c.Upkeep}, net {net:+0;-0}\n" +
                               $"Stability {c.Stability} of 5\n" + (wars.Count > 0 ? "At war with " + string.Join(", ", wars.Select(CountryLook.NameOf)) : "At war with nobody");
                chip.RegisterCallback<ClickEvent>(_ => CountryPressed?.Invoke(id));
                chip.Add(new Crest(look));
                var name = new Label(look.Name.ToUpperInvariant()); name.AddToClassList("nation-name"); chip.Add(name);
                var count = Stat(chip, IconKind.Province, n.ToString());
                provinceCount[id] = count;
                if (id == flipCountry) Flip(count);
                Stat(chip, IconKind.Army, c.Army.ToString()).parent.AddToClassList("army-stat");
                var gold = Stat(chip, IconKind.Gold, c.Gold.ToString());
                gold.parent.AddToClassList("gold-stat");
                var delta = new Label($"{net:+0;-0}"); delta.AddToClassList("stat-delta"); delta.AddToClassList(net >= 0 ? "good" : "bad");
                gold.parent.Add(delta);
                var pips = new VisualElement(); pips.AddToClassList("pips"); pips.style.flexDirection = FlexDirection.Row; pips.style.marginRight = 7;
                for (int k = 1; k <= 5; k++) { var pip = new VisualElement(); pip.AddToClassList("pip"); pip.EnableInClassList("on", k <= c.Stability); pips.Add(pip); }
                chip.Add(pips);
                if (wars.Count > 0) { var w = Stat(chip, IconKind.War, wars.Count.ToString()); w.AddToClassList("bad"); }
                nations.Add(chip);
            }
            RenderRealm(s);
            ShowArmies(s);
            for (int i = 0; i < ProvinceIds.Count; i++)
            {
                fortMarks[i].Clear();
                if (!Fog.KnowsProvince(s, i)) continue;
                for (int k = 0; k < s.Forts[i]; k++) fortMarks[i].Add(new Icon(IconKind.Fort, 11) { Tint = new Color32(233, 217, 176, 255) });
            }
        }

        private static void Flip(VisualElement e)
        {
            e.AddToClassList("flip");
            e.schedule.Execute(() => e.RemoveFromClassList("flip")).StartingIn(300);
        }

        private static Label Stat(VisualElement parent, IconKind icon, string value)
        {
            var box = new VisualElement(); box.AddToClassList("stat");
            box.Add(new Icon(icon, 11));
            var v = new Label(value); v.AddToClassList("stat-value"); box.Add(v);
            parent.Add(box);
            return v;
        }

        private void RenderRealm(ReplayState s)
        {
            realmBody.Clear();
            var rank = CountryIds.OrderByDescending(id => s.ProvinceCount(id)).ToList();
            Section("STANDING", $"{WinAt} wins", rank.Select(id => BarRow(id, s.ProvinceCount(id), WinAt, $"{CountryLook.NameOf(id)}: {s.ProvinceCount(id)} provinces, {Math.Max(0, WinAt - s.ProvinceCount(id))} to win")));
            var wars = s.Relations.Where(r => r.Kind == "war").ToList();
            Section("WARS", wars.Count.ToString(), wars.Select(r => PairRow(r.A, r.B, IconKind.War, r.Since > 0 ? $"since t{r.Since}" : "", "bad")));
            var truces = s.Relations.Where(r => r.Kind == "truce").ToList();
            Section("TRUCES", truces.Count.ToString(), truces.Select(r => PairRow(r.A, r.B, IconKind.Truce, $"{r.TruceTurnsLeft} left", "warn")));
            int max = Math.Max(1, s.Countries.Values.Max(c => c.Army));
            Section("ARMIES", "", rank.Where(id => s.Countries[id].Alive).Select(id => BarRow(id, s.Countries[id].Army, max, $"{CountryLook.NameOf(id)}'s army: {s.Countries[id].Army} soldiers")));
            var neutral = Enumerable.Range(0, s.Owners.Length).Where(i => s.Owners[i] == null).ToList();
            Section("NEUTRAL LAND", neutral.Count.ToString(), neutral.Select(i =>
            {
                var row = new VisualElement(); row.AddToClassList("row");
                row.Add(new Label(Cap(ProvinceIds[i])));
                var g = new Label(Fog.KnowsProvince(s, i) ? $"garrison {s.Garrisons[i]}" : "garrison ?") { tooltip = Fog.KnowsProvince(s, i) ? null : "Beyond your borders: your court does not know its garrison" }; g.AddToClassList("muted"); g.style.marginLeft = StyleKeyword.Auto; row.Add(g);
                return row;
            }));
        }

        private void Section(string title, string note, IEnumerable<VisualElement> rows)
        {
            var head = new VisualElement(); head.AddToClassList("section-head");
            head.Add(new Label(title) { style = { flexGrow = 1 } });
            head.Add(new Label(note));
            realmBody.Add(head);
            var body = new VisualElement(); body.AddToClassList("section-body");
            int n = 0;
            foreach (var r in rows) { body.Add(r); n++; }
            if (n == 0) { var none = new Label("None"); none.AddToClassList("muted"); body.Add(none); }
            realmBody.Add(body);
        }

        private VisualElement BarRow(string id, int value, int max, string tip)
        {
            var row = new VisualElement { tooltip = tip }; row.AddToClassList("row");
            row.RegisterCallback<ClickEvent>(_ => CountryPressed?.Invoke(id));
            row.Add(new Crest(CountryLook.Of(id), 16));
            var name = new Label(CountryLook.NameOf(id)); name.AddToClassList("row-name"); row.Add(name);
            var bar = new VisualElement(); bar.AddToClassList("bar");
            var fill = new VisualElement(); fill.AddToClassList("bar-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(value / (float)max) * 100);
            fill.style.backgroundColor = (Color)CountryLook.Of(id).Colour;
            bar.Add(fill); row.Add(bar);
            var v = new Label(value.ToString()); v.AddToClassList("row-value"); row.Add(v);
            return row;
        }

        private VisualElement PairRow(string a, string b, IconKind icon, string note, string cls)
        {
            var row = new VisualElement(); row.AddToClassList("row");
            row.Add(new Crest(CountryLook.Of(a), 16));
            var i = new Icon(icon, 11); i.AddToClassList(cls); i.style.marginLeft = 3; i.style.marginRight = 3; row.Add(i);
            row.Add(new Crest(CountryLook.Of(b), 16));
            var t = new Label($"{CountryLook.NameOf(a)} – {CountryLook.NameOf(b)}"); t.style.marginLeft = 6; row.Add(t);
            var n = new Label(note); n.AddToClassList("muted"); n.style.marginLeft = StyleKeyword.Auto; row.Add(n);
            return row;
        }

        // ---- chronicle ----

        private void RenderFilters()
        {
            filters.Clear();
            foreach (var id in CountryIds)
            {
                var chip = new VisualElement { tooltip = "Show or hide " + CountryLook.NameOf(id) }; chip.AddToClassList("chip");
                chip.EnableInClassList("off", !countryFilter.Contains(id));
                chip.Add(new Crest(CountryLook.Of(id), 14));
                chip.RegisterCallback<ClickEvent>(_ => { if (!countryFilter.Remove(id)) countryFilter.Add(id); RenderFilters(); RenderChronicle(null); });
                filters.Add(chip);
            }
            foreach (ChronicleCategory c in Enum.GetValues(typeof(ChronicleCategory)))
            {
                if (c == ChronicleCategory.Story) continue; // room for the storyteller, not yet in the game
                var chip = new VisualElement(); chip.AddToClassList("chip");
                chip.EnableInClassList("off", !categoryFilter.Contains(c));
                chip.Add(new Label(c.ToString()));
                chip.RegisterCallback<ClickEvent>(_ => { if (!categoryFilter.Remove(c)) categoryFilter.Add(c); RenderFilters(); RenderChronicle(null); });
                filters.Add(chip);
            }
        }

        public void ClearChronicle() { lines.Clear(); RenderChronicle(null); }

        public void AddLine(ChronicleLine line)
        {
            if (line == null) return;
            lines.Add(line);
            RenderChronicle(line);
        }

        private void RenderChronicle(ChronicleLine fresh)
        {
            chronicleBody.Clear();
            int turn = -1;
            foreach (var l in Chronicle.Filter(lines, countryFilter, categoryFilter))
            {
                if (l.Turn != turn) { turn = l.Turn; var h = new Label($"TURN {turn}"); h.AddToClassList("lg-turn"); chronicleBody.Add(h); }
                var row = new VisualElement(); row.AddToClassList("lg");
                row.tooltip = l.Event.Decision != null ? "Click: find it on the map and open the decision" : "Click: find it on the map";
                row.RegisterCallback<ClickEvent>(_ => LinePressed?.Invoke(l));
                row.Add(new Crest(CountryLook.Of(l.Country), 16));
                var body = new VisualElement(); body.AddToClassList("lg-body");
                var t = new Label(l.Text); t.AddToClassList("lg-text"); body.Add(t);
                if (showQuotes && !string.IsNullOrEmpty(l.Quote)) { var q = new Label($"“{l.Quote}”"); q.AddToClassList("lg-quote"); body.Add(q); }
                row.Add(body);
                string tag = l.Category == ChronicleCategory.Land ? "land" : l.Category == ChronicleCategory.Refused ? "ref" : l.Category == ChronicleCategory.Guard ? "guard" : null;
                if (tag != null) { var tg = new Label(tag == "ref" ? "REFUSED" : tag.ToUpperInvariant()); tg.AddToClassList("tag"); tg.AddToClassList(tag); row.Add(tg); }
                if (l == fresh) { row.AddToClassList("new"); row.schedule.Execute(() => row.RemoveFromClassList("new")).StartingIn(50); }
                chronicleBody.Add(row);
            }
        }

        // ---- labels and speech that follow the map ----

        public void PlaceLabels(ReplayState s)
        {
            if (ProvinceToPanel == null) return;
            for (int i = 0; i < ProvinceIds.Count; i++)
            {
                var p = ProvinceToPanel(i);
                bool capital = CountryIds.Any(id => CapitalIndex(id) == i);
                var l = provinceLabels[i];
                l.EnableInClassList("clab", capital);
                l.EnableInClassList("plab", !capital);
                l.text = capital ? ProvinceIds[i].ToUpperInvariant() : Cap(ProvinceIds[i]);
                float size = ProvinceLabelSize?.Invoke(i) ?? -1;
                if (size >= 0)
                {
                    // A generated world: each name sized to the room in its province, hidden when it does not fit.
                    l.style.display = size > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                    if (size > 0) l.style.fontSize = size;
                    fortMarks[i].style.display = size > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                }
                l.style.left = p.x; l.style.top = p.y + (capital ? 4 : 0);
                fortMarks[i].style.left = p.x; fortMarks[i].style.top = p.y + (capital ? 24 : 10);
            }
            PlaceArmies();
            foreach (var b in banners) PlaceBanner(b);
            if (Queries != null) PlaceBadges();
            foreach (var m in marks) m.Place();
        }

        public static readonly Dictionary<string, string> Capitals = new Dictionary<string, string>
        {
            ["valen"] = "highmoor", ["orsk"] = "orskhaven", ["merrow"] = "merrowdeep", ["kesh"] = "keshtar", ["ilvar"] = "ilvarion",
        };

        public int CapitalIndex(string country) => Queries != null ? Queries.CapitalIndex(country) : Capitals.TryGetValue(country, out var p) ? ProvinceIds.ToList().IndexOf(p) : -1;

        /// <summary>A generated world: a province name's size in panel pixels, 0 to hide it. Null on the Classic board, whose names keep their style.</summary>
        public Func<int, float> ProvinceLabelSize;

        private readonly List<Label> regionLabels = new List<Label>();

        /// <summary>Region names, in spaced italic capitals, where the director placed them (panel pixels); an empty list hides them.</summary>
        public void PlaceRegionLabels(IReadOnlyList<(string text, Vector2 at, float size)> items)
        {
            while (regionLabels.Count < items.Count)
            {
                var l = new Label { pickingMode = PickingMode.Ignore };
                l.AddToClassList("rlab");
                labels.Insert(0, l);
                regionLabels.Add(l);
            }
            for (int i = 0; i < regionLabels.Count; i++)
            {
                var l = regionLabels[i];
                if (i >= items.Count) { l.style.display = DisplayStyle.None; continue; }
                l.style.display = DisplayStyle.Flex;
                l.text = items[i].text.ToUpperInvariant();
                l.style.fontSize = items[i].size;
                l.style.left = items[i].at.x; l.style.top = items[i].at.y;
            }
        }

        private sealed class Banner { public VisualElement Element; public int Province; }
        private readonly List<Banner> banners = new List<Banner>();

        /// <summary>The ruler speaks at the capital: the persona's face, the country's colour, the line.</summary>
        public VisualElement Say(string country, string line, int atProvince = -1, Action onClick = null)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            var look = CountryLook.Of(country);
            var player = Players?.GetValueOrDefault(country);
            var e = new VisualElement { pickingMode = onClick != null ? PickingMode.Position : PickingMode.Ignore, tooltip = onClick != null ? "Click: why this move" : null }; e.AddToClassList("banner");
            if (onClick != null) e.RegisterCallback<ClickEvent>(_ => onClick());
            e.style.borderLeftColor = (Color)look.Colour;
            var face = new Image { image = portraits, uv = Faces.Uv(player?.Persona, look.Slot % 2), scaleMode = ScaleMode.ScaleAndCrop };
            face.AddToClassList("face"); face.style.borderTopColor = face.style.borderBottomColor = face.style.borderLeftColor = face.style.borderRightColor = (Color)look.Colour;
            e.Add(face);
            var body = new VisualElement(); body.AddToClassList("banner-body");
            var who = new Label($"{look.Name.ToUpperInvariant()} · THE {(player?.Persona ?? "ruler").ToUpperInvariant()}"); who.AddToClassList("banner-who"); body.Add(who);
            var l = new Label($"“{line}”"); l.AddToClassList("banner-line"); body.Add(l);
            e.Add(body);
            fx.Add(e);
            var b = new Banner { Element = e, Province = atProvince >= 0 ? atProvince : CapitalIndex(country) };
            banners.Add(b);
            PlaceBanner(b);
            e.schedule.Execute(() => e.AddToClassList("on")).StartingIn(16);
            return e;
        }

        public void Unsay(VisualElement e)
        {
            if (e == null) return;
            e.RemoveFromClassList("on");
            banners.RemoveAll(b => b.Element == e);
            e.schedule.Execute(() => e.RemoveFromHierarchy()).StartingIn(260);
        }

        private void PlaceBanner(Banner b)
        {
            // Above the capital, but kept in the space between the realm list and the chronicle, so
            // no panel, the inspector included, ever hides a ruler's words.
            var p = ProvinceToPanel(b.Province);
            float half = Mathf.Max(60, b.Element.resolvedStyle.width) / 2, height = Mathf.Max(40, b.Element.resolvedStyle.height);
            float minX = 248 + half, maxX = Root.layout.width - (InspectorOpen ? 536 : 356) - half; // the inspector is wider than the chronicle
            b.Element.style.left = maxX > minX ? Mathf.Clamp(p.x, minX, maxX) : p.x;
            b.Element.style.top = Mathf.Max(50 + height, p.y - 26);
        }

        // ---- battle marks: the arrow, the swords and the score ----

        private readonly List<Mark> marks = new List<Mark>();

        private sealed class Mark : VisualElement
        {
            public int From, To; public Color Colour; public float Reveal; public bool Broken; public string Score; public bool Swords;
            public Func<int, Vector2> Map;
            private readonly Label score;
            public Mark()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
                generateVisualContent += Draw;
                score = new Label(); score.AddToClassList("badge"); score.style.display = DisplayStyle.None; Add(score);
            }
            public void Place()
            {
                MarkDirtyRepaint();
                var b = Map(To);
                score.text = Score;
                score.style.display = Swords && !string.IsNullOrEmpty(Score) ? DisplayStyle.Flex : DisplayStyle.None;
                score.style.left = b.x; score.style.top = b.y + 10;
            }
            private void Draw(MeshGenerationContext ctx)
            {
                if (Map == null) return;
                var a = Map(From); var b = Map(To);
                var mid = (a + b) / 2 + new Vector2(b.y - a.y, -(b.x - a.x)) * 0.18f;
                var p = ctx.painter2D;
                float limit = Broken ? Mathf.Min(Reveal, 0.65f) : Reveal;
                if (limit > 0)
                {
                    p.strokeColor = Colour; p.lineWidth = 7; p.lineCap = LineCap.Round;
                    p.BeginPath();
                    int steps = 24; Vector2 last = a;
                    p.MoveTo(a);
                    for (int i = 1; i <= steps * limit; i++)
                    {
                        float t = i / (float)steps;
                        last = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * mid + t * t * b;
                        p.LineTo(last);
                    }
                    p.Stroke();
                    if (!Broken && Reveal >= 0.99f)
                    {
                        var dir = (b - mid).normalized; var n = new Vector2(-dir.y, dir.x);
                        p.fillColor = Colour; p.BeginPath(); p.MoveTo(b + dir * 4); p.LineTo(b - dir * 16 + n * 9); p.LineTo(b - dir * 12); p.LineTo(b - dir * 16 - n * 9); p.ClosePath(); p.Fill();
                    }
                }
                if (Swords)
                {
                    var c = b + new Vector2(0, -26);
                    p.fillColor = Broken ? new Color32(90, 29, 20, 255) : new Color32(27, 19, 11, 255);
                    p.strokeColor = new Color32(210, 173, 98, 255); p.lineWidth = 1.5f;
                    p.BeginPath(); p.Arc(c, 15, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); p.Stroke();
                    p.strokeColor = new Color32(243, 226, 184, 255); p.lineWidth = 3; p.lineCap = LineCap.Round;
                    p.BeginPath(); p.MoveTo(c + new Vector2(-8, -8)); p.LineTo(c + new Vector2(8, 8)); p.Stroke();
                    p.BeginPath(); p.MoveTo(c + new Vector2(8, -8)); p.LineTo(c + new Vector2(-8, 8)); p.Stroke();
                }
            }
        }

        public object ShowArrow(int from, int to, Color colour)
        {
            var m = new Mark { From = from, To = to, Colour = colour, Map = ProvinceToPanel };
            fx.Add(m); marks.Add(m); m.Place();
            return m;
        }

        public void SetArrow(object handle, float reveal, bool broken, bool swords, string score)
        {
            if (!(handle is Mark m)) return;
            m.Reveal = reveal; m.Broken = broken; m.Swords = swords; m.Score = score; m.Place();
        }

        public void HideArrow(object handle)
        {
            if (!(handle is Mark m)) return;
            marks.Remove(m); m.RemoveFromHierarchy();
        }
    }
}
