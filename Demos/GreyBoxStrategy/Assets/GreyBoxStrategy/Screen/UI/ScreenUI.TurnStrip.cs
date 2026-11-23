using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The turn strip (#20, the lead's turn summary of 17 November): after each resolution, one compact row a realm above the
    /// chronicle, with icons for what it did (a count when several), its persona, and a shield where a guard of the
    /// framework stopped one of its decisions. A click opens a card beside it: each decision as a line, "why" for the
    /// inspector, the guard's line in plain words. Everything comes from <see cref="TurnSummary"/>, which reads only what
    /// was recorded.
    /// </summary>
    public sealed partial class ScreenUI
    {
        /// <summary>A row of the strip was clicked: the realm and the turn. For "replay it on the map" (the screen session's).</summary>
        public Action<string, int> TurnRowPressed;

        private VisualElement turnStrip, turnStripBody, turnCard;

        /// <summary>The strip folded to its header, and folding it: saved with the settings. Null: always open.</summary>
        public Func<bool> StripFolded;
        public Action<bool> FoldStrip;
        private List<TurnRow> turnRows = new List<TurnRow>();
        private string openRow;

        /// <summary>For tests and the CLI: the strip's rows as they stand.</summary>
        public IReadOnlyList<TurnRow> TurnRows => turnRows;

        /// <summary>
        /// Whether your court sees an army order: yours always; a rival's when the army stands, or marches to, a province in
        /// your sight in <paramref name="state"/>; every one while you watch. The strip and the chronicle both ask it, so
        /// neither tells what the map's fog hides.
        /// </summary>
        public bool SeesArmyOrder(ReplayEvent e, ReplayState state)
        {
            if (e == null || e.Kind != ReplayEventKind.ArmyOrder || !Fog.On || e.Country == Fog.Viewer || state == null) return true;
            var sight = Sight(state);
            if (sight == null) return true;
            string at = state.Army(e.Army)?.Location;
            return (at != null && sight.Contains(at)) || (e.Province != null && sight.Contains(e.Province));
        }

        /// <summary>The strip for a turn just resolved: <paramref name="state"/> is the turn's end, for what your court sees.</summary>
        public void ShowTurnStrip(int turn, IEnumerable<ReplayEvent> events, ReplayState state)
        {
            bool Seen(ReplayEvent e) => SeesArmyOrder(e, state);
            turnRows = TurnSummary.For(turn, events, CountryIds.Where(c => state == null || !state.Countries.TryGetValue(c, out var cs) || cs.Alive), Fog.Viewer,
                c => Players != null && Players.TryGetValue(c, out var p) ? p.Persona : null, CountryLook.NameOf, Seen);
            if (turnStrip == null)
            {
                turnStrip = Panel("", "turn-strip");
                turnStrip.Q(className: "panel-head").RemoveFromHierarchy();
                turnStripBody = new ScrollView(); turnStripBody.AddToClassList("turn-strip-body"); turnStrip.Add(turnStripBody);
                Root.Add(turnStrip);
            }
            Root.AddToClassList("strip-on");
            if (openRow != null && turnCard != null) { var keep = openRow; CloseTurnCard(); openRow = keep; }
            turnStripBody.Clear();
            bool folded = StripFolded?.Invoke() == true;
            Root.EnableInClassList("strip-folded", folded);
            // The header folds the strip to one line, as the outliner's sections fold; the choice is kept in settings.
            var head = new VisualElement { tooltip = folded ? "Click: show what each realm did" : "Click: fold. Each realm's orders this turn; click a row for its decisions, and why." };
            head.AddToClassList("turn-strip-head");
            head.Add(new Label(folded ? "▸" : "▾") { style = { width = 12 } });
            head.Add(new Label($"TURN {turn} · WHAT EACH REALM DID"));
            int st = turn; var ev = events; var sstate = state;
            head.RegisterCallback<ClickEvent>(_ => { FoldStrip?.Invoke(!folded); if (!folded) CloseTurnCard(); ShowTurnStrip(st, ev, sstate); });
            turnStripBody.Add(head);
            if (!folded) foreach (var r in turnRows) turnStripBody.Add(TurnRowElement(r));
            KeepLayout();
            if (openRow != null) { var row = turnRows.FirstOrDefault(x => x.Country == openRow); if (row != null) ShowTurnCard(row); else openRow = null; }
        }

        public void HideTurnStrip() { turnStrip?.RemoveFromHierarchy(); turnStrip = null; Root.RemoveFromClassList("strip-on"); CloseTurnCard(); }

        private VisualElement TurnRowElement(TurnRow r)
        {
            var row = new VisualElement(); row.AddToClassList("turn-row"); row.EnableInClassList("mine", r.Mine); row.EnableInClassList("open", r.Country == openRow);
            row.Add(new Crest(CountryLook.Of(r.Country), 16));
            var name = new Label(r.Mine ? $"{CountryLook.NameOf(r.Country)} (you)" : CountryLook.NameOf(r.Country)); name.AddToClassList("turn-row-name"); row.Add(name);
            if (r.Persona != null) { var p = new Label("the " + r.Persona); p.AddToClassList("turn-row-persona"); row.Add(p); }
            var icons = new VisualElement(); icons.AddToClassList("turn-row-icons");
            if (r.Deeds.Count == 0) { var none = new Label("no orders"); none.AddToClassList("turn-row-none"); icons.Add(none); }
            foreach (var (kind, count) in r.Kinds)
            {
                var k = new VisualElement { tooltip = KindWords(kind, count) }; k.AddToClassList("turn-row-kind");
                k.Add(new Icon(KindIcon(kind), 16) { Tint = KindTint(kind) });
                if (count > 1) { var n = new Label(count.ToString()); n.AddToClassList("turn-row-count"); k.Add(n); }
                icons.Add(k);
            }
            row.Add(icons);
            if (r.Guarded)
            {
                var shield = new Icon(IconKind.Shield, 16) { Tint = new Color32(226, 194, 122, 255), tooltip = string.Join(" ", r.Deeds.Where(d => d.Guarded).Select(d => d.GuardLine)) };
                shield.AddToClassList("turn-row-shield"); row.Add(shield);
            }
            row.tooltip = "Click: its decisions this turn";
            string c = r.Country; int turn = r.Turn;
            row.RegisterCallback<ClickEvent>(_ =>
            {
                if (openRow == c) CloseTurnCard(); else { openRow = c; ShowTurnCard(turnRows.First(x => x.Country == c)); SoundManager.Play(SoundCues.PanelOpen); }
                foreach (var el in turnStripBody.Query(className: "turn-row").ToList()) el.EnableInClassList("open", el == row && openRow == c);
                TurnRowPressed?.Invoke(c, turn);
            });
            return row;
        }

        /// <summary>For the CLI and the tests: open a row's card as a click would.</summary>
        public bool OpenTurnRow(string country)
        {
            var r = turnRows.FirstOrDefault(x => x.Country == country);
            if (r == null) return false;
            openRow = country; ShowTurnCard(r); SoundManager.Play(SoundCues.PanelOpen);
            turnStripBody?.Query(className: "turn-row").ForEach(el => el.EnableInClassList("open", el.Q<Label>(className: "turn-row-name")?.text.StartsWith(CountryLook.NameOf(country)) == true));
            TurnRowPressed?.Invoke(country, r.Turn);
            return true;
        }

        private void ShowTurnCard(TurnRow r)
        {
            turnCard?.RemoveFromHierarchy();
            turnCard = new VisualElement(); turnCard.AddToClassList("panel"); turnCard.AddToClassList("turn-card");
            var head = new VisualElement(); head.AddToClassList("panel-head");
            head.Add(new Crest(CountryLook.Of(r.Country), 16) { style = { marginRight = 6 } });
            var t = new Label($"{CountryLook.NameOf(r.Country).ToUpperInvariant()} · TURN {r.Turn}"); t.AddToClassList("panel-title"); head.Add(t);
            var x = new Button(CloseTurnCard) { text = "×", tooltip = "Close" }; x.AddToClassList("tbtn"); head.Add(x);
            turnCard.Add(head);
            var body = new ScrollView(); body.AddToClassList("turn-card-body"); turnCard.Add(body);
            if (r.Persona != null) body.Add(Text($"Ruled by the {r.Persona}.", "src"));
            if (r.Deeds.Count == 0) body.Add(Text("It gave no orders this turn.", "src"));
            foreach (var d in r.Deeds)
            {
                var line = new VisualElement(); line.AddToClassList("tc-line");
                line.Add(new Icon(KindIcon(d.Kind), 13) { Tint = KindTint(d.Kind) });
                var col = new VisualElement(); col.AddToClassList("tc-col");
                col.Add(Text(d.Text, "tc-text"));
                if (d.Quote != null) { var q = new Label($"“{d.Quote}”"); q.AddToClassList("lg-quote"); col.Add(q); }
                if (d.Guarded)
                {
                    var g = new VisualElement(); g.AddToClassList("tc-guard");
                    g.Add(new Icon(IconKind.Shield, 13) { Tint = new Color32(226, 194, 122, 255) });
                    g.Add(Text(d.GuardLine + " This is the framework's point: the model chooses, the guards keep it to what the rules offer.", "tc-guard-text"));
                    col.Add(g);
                }
                line.Add(col);
                if (d.Event?.Decision != null)
                {
                    var ev = d.Event;
                    var why = new Button(() => DecisionPressed?.Invoke(ev)) { text = "Why?", tooltip = "The decision in the inspector: what it was told, offered and answered" };
                    why.AddToClassList("tbtn"); why.AddToClassList("tc-why"); line.Add(why);
                }
                body.Add(line);
            }
            Root.Add(turnCard);
            KeepLayout();
        }

        public void CloseTurnCard()
        {
            turnCard?.RemoveFromHierarchy(); turnCard = null; openRow = null;
            turnStripBody?.Query(className: "turn-row").ForEach(el => el.RemoveFromClassList("open"));
        }

        public static IconKind KindIcon(string kind) => kind switch
        {
            "recruit" => IconKind.Army, "fortify" => IconKind.Fort, "tax" => IconKind.Gold, "taxes" => IconKind.Taxes, "war" => IconKind.Swords,
            "market" => IconKind.Market, "barracks" => IconKind.Barracks, "farm" => IconKind.Farm,
            "peace" => IconKind.Peace, "cede" => IconKind.Province, "demand" => IconKind.Province, "march" => IconKind.Province,
            "siege" => IconKind.Fort, "hold" => IconKind.Hourglass, "attack" => IconKind.Swords, "army" => IconKind.Army,
            "refused" => IconKind.Refused, "hidden" => IconKind.Eye, _ => IconKind.Quill,
        };

        private static Color KindTint(string kind) => kind switch
        {
            "war" => new Color32(226, 110, 90, 255), "attack" => new Color32(226, 110, 90, 255), "refused" => new Color32(200, 120, 110, 255),
            "peace" => new Color32(150, 190, 225, 255), "tax" => new Color32(232, 196, 92, 255), "taxes" => new Color32(232, 196, 92, 255),
            "market" => BuildingTint(Building.Market), "barracks" => BuildingTint(Building.Barracks), "farm" => BuildingTint(Building.Farm), "march" => new Color32(150, 200, 120, 255),
            "cede" => new Color32(214, 160, 120, 255), "demand" => new Color32(214, 160, 120, 255), "siege" => new Color32(214, 180, 120, 255),
            "hidden" => new Color32(140, 128, 104, 255), "hold" => new Color32(170, 160, 140, 255),
            _ => new Color32(214, 196, 158, 255),
        };

        public static string KindWords(string kind, int n)
        {
            string w = kind switch
            {
                "recruit" => "recruited", "fortify" => "fortified", "tax" => "raised taxes", "taxes" => "set taxes",
                "market" => n == 1 ? "built a market" : "built markets", "barracks" => "built barracks", "farm" => n == 1 ? "built a farm" : "built farms", "war" => "declared war", "peace" => "sought peace",
                "cede" => "offered land", "demand" => "demanded land", "march" => n == 1 ? "an army marched" : "armies marched", "siege" => "a siege",
                "hold" => n == 1 ? "an order to hold" : "orders to hold", "attack" => "attacked", "army" => "merged or split armies",
                "refused" => "an order refused when it landed", "hidden" => n == 1 ? "an army out of your sight was ordered" : "armies out of your sight were ordered",
                _ => kind,
            };
            return n > 1 ? $"{w}, {n} orders" : w;
        }

        /// <summary>A building's colour, the same on the strip, in the province panel and in the council.</summary>
        public static Color BuildingTint(Building b) => b switch
        {
            Building.Market => new Color32(232, 196, 92, 255), Building.Barracks => new Color32(200, 140, 120, 255), _ => new Color32(160, 200, 110, 255),
        };

        public static IconKind BuildingIcon(Building b) => b switch
        {
            Building.Market => IconKind.Market, Building.Barracks => IconKind.Barracks, _ => IconKind.Farm,
        };
    }
}
