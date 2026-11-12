using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Rule mode's own pieces: the orders panel (bottom centre, ending in End turn), the moves on a
    /// province or a country panel, and the envoy card for a peace offer to you. What is legal, and
    /// why not, is always <see cref="Legality.WhyNot"/> on the council's own state.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private VisualElement orders, offerCard;
        private Label ordersStatus, card;
        private string openAction;
        private bool showReport;

        /// <summary>Fog of war: what you are shown in rule mode. Its viewer is null in watch mode.</summary>
        public Fog Fog = new Fog(null, null);
        /// <summary>Your council request while the game waits for your order, else null.</summary>
        public CountryRequest OrdersRequest;
        /// <summary>The order you have chosen this turn.</summary>
        public Move Chosen = Move.Hold;
        public Action<Move> OrderPressed;
        public Action EndTurnPressed;
        public Action<MapMode> HintMode;
        public bool GameOver;

        private string You => Fog.Viewer;

        /// <summary>Whether the orders panel shows the report (standing and turn facts); for the Unity CLI too.</summary>
        public bool ReportOpen { get => showReport; set => showReport = value; }

        public void SetRuleMode(string you)
        {
            Root.AddToClassList("rule");
            Followed = you;
        }

        // ---- the orders panel ----

        /// <summary>Redraws the orders panel. <paramref name="status"/> is the line under it: who the game waits on.</summary>
        public void ShowOrders(string status)
        {
            if (orders == null) { orders = new VisualElement(); orders.AddToClassList("panel"); orders.AddToClassList("orders"); Root.Add(orders); }
            orders.Clear();
            var req = OrdersRequest;
            var head = new VisualElement(); head.AddToClassList("panel-head");
            head.Add(new Crest(CountryLook.Of(You), 16) { style = { marginRight = 6 } });
            var t = new Label("YOUR COUNCIL"); t.AddToClassList("panel-title"); head.Add(t);
            if (req != null)
            {
                var rep = new Button(() => { showReport = !showReport; ShowOrders(status); }) { text = "Report", tooltip = "What your court tells you this turn: the same lines a model ruler reads" };
                rep.AddToClassList("tbtn"); rep.style.width = StyleKeyword.Auto; rep.style.paddingLeft = 7; rep.style.paddingRight = 7;
                if (showReport) rep.AddToClassList("on");
                head.Add(rep);
            }
            orders.Add(head);

            var body = new VisualElement { style = { paddingLeft = 10, paddingRight = 10, paddingTop = 7, paddingBottom = 8 } };
            orders.Add(body);
            if (req == null)
            {
                var wait = new Label(status ?? ""); wait.AddToClassList("orders-status"); body.Add(wait);
                ordersStatus = wait;
                return;
            }

            if (showReport)
            {
                var f = new VisualElement(); f.AddToClassList("facts-box");
                f.Add(Mono(string.Join("\n", req.StandingFacts)));
                if (req.TurnFacts.Count > 0) f.Add(Mono("\nThis turn: " + string.Join(" ", req.TurnFacts)));
                body.Add(f);
            }

            var order = new VisualElement(); order.AddToClassList("order-line");
            order.Add(new Label("ORDER") { style = { fontSize = 10, letterSpacing = 2, color = new Color(0.55f, 0.5f, 0.4f), width = 52 } });
            var chosen = new Label(Describe(Chosen)); chosen.AddToClassList("order-chosen"); order.Add(chosen);
            body.Add(order);

            var row = new VisualElement(); row.AddToClassList("order-moves");
            foreach (var action in new[] { Actions.Recruit, Actions.RaiseTaxes, Actions.Attack, Actions.Fortify, Actions.DeclareWar, Actions.OfferPeace, Actions.None })
                row.Add(ActionButton(req, action, status));
            body.Add(row);

            if (openAction != null)
            {
                var targets = req.Legal.Where(m => m.Action == openAction).ToList();
                var chips = new VisualElement(); chips.AddToClassList("chips"); chips.style.marginTop = 6;
                foreach (var m in targets)
                {
                    string odds = m.Action == Actions.Attack && Queries != null && lastState != null ? $" · {Math.Round(Queries.WinChance(lastState, You, ProvinceIds.ToList().IndexOf(m.Target)) * 100)}%" : "";
                    var c = new Button(() => OrderPressed?.Invoke(m)) { text = Cap(m.Target) + odds, tooltip = $"{Describe(m)}: {m.Detail}" };
                    c.AddToClassList("target-chip"); if (m.Equals(Chosen)) c.AddToClassList("pick");
                    chips.Add(c);
                }
                body.Add(chips);
                var hint = new Label(Actions.TargetsProvince(openAction) ? "Or click a province on the map: its panel has the same order." : "Or open a country from the realm list: its panel has the same order.");
                hint.AddToClassList("src"); body.Add(hint);
            }

            var foot = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 8 } };
            var st = new Label(status ?? ""); st.AddToClassList("orders-status"); st.style.flexGrow = 1; st.style.flexShrink = 1; foot.Add(st);
            ordersStatus = st;
            var end = new Button(() => EndTurnPressed?.Invoke()) { text = "End turn", tooltip = "Send your order (Enter). The four rulers' moves and yours then land in the rules' order." };
            end.AddToClassList("menu-button"); end.AddToClassList("end-turn"); foot.Add(end);
            body.Add(foot);
        }

        private Button ActionButton(CountryRequest req, string action, string status)
        {
            bool targeted = Actions.TargetsProvince(action) || Actions.TargetsCountry(action);
            var legal = req.Legal.Where(m => m.Action == action).ToList();
            string label = action == Actions.None ? "Hold" : action == Actions.RaiseTaxes ? "Raise taxes" : action == Actions.DeclareWar ? "Declare war" : action == Actions.OfferPeace ? "Offer peace" : Cap(action);
            if (targeted) label += $" ({legal.Count})";
            string why = legal.Count > 0 ? null : WhyNone(req, action);
            var b = new Button(() =>
            {
                if (why != null) return;
                if (!targeted) { OrderPressed?.Invoke(legal[0]); return; }
                openAction = openAction == action ? null : action;
                if (action == Actions.Attack && openAction != null) HintMode?.Invoke(MapMode.Reach);
                ShowOrders(status);
            })
            { text = label };
            b.AddToClassList("order-move");
            if (Chosen.Action == action) b.AddToClassList("pick");
            if (openAction == action) b.AddToClassList("on");
            // Not SetEnabled(false): a disabled element gets no hover, and its tooltip is the reason.
            b.EnableInClassList("off", why != null);
            b.tooltip = why != null ? $"Not offered: {why}" : targeted ? $"{string.Join(", ", legal.Select(m => Cap(m.Target)))}" : Explain(action);
            return b;
        }

        /// <summary>Why an action has no legal target: the rule's own reason for the nearest candidate.</summary>
        private static string WhyNone(CountryRequest req, string action)
        {
            var w = req.World;
            if (!Actions.TargetsProvince(action) && !Actions.TargetsCountry(action))
                return Legality.WhyNot(w, req.CountryId, new Move(action));
            IEnumerable<string> candidates = Actions.TargetsProvince(action)
                ? w.ProvinceList.Where(p => action == Actions.Fortify ? p.Owner == req.CountryId : p.Owner != req.CountryId && w.Touches(req.CountryId, p)).Select(p => p.Id)
                : w.Alive.Where(c => c.Id != req.CountryId).Select(c => c.Id);
            var reasons = candidates.Select(id => Legality.WhyNot(w, req.CountryId, new Move(action, id))).Where(r => r != null).Distinct().ToList();
            if (reasons.Count == 0) return action == Actions.Attack ? "no province is next to our land" : "no target";
            return reasons.Count == 1 ? reasons[0] : string.Join("; ", reasons.Take(2)) + (reasons.Count > 2 ? "; …" : "");
        }

        private static string Explain(string action)
        {
            switch (action)
            {
                case Actions.Recruit: return "Spend gold on soldiers: 2 gold each, as many as the treasury allows";
                case Actions.RaiseTaxes: return "Take gold now, at a cost of 1 stability";
                case Actions.None: return "Do nothing this turn";
                default: return action;
            }
        }

        public static string Describe(Move m)
        {
            if (m == null) return "Hold";
            switch (m.Action)
            {
                case Actions.None: return "Hold";
                case Actions.Recruit: return "Recruit";
                case Actions.RaiseTaxes: return "Raise taxes";
                case Actions.Attack: return "Attack " + Cap(m.Target);
                case Actions.Fortify: return "Fortify " + Cap(m.Target);
                case Actions.DeclareWar: return "Declare war on " + CountryLook.NameOf(m.Target);
                case Actions.OfferPeace: return "Offer peace to " + CountryLook.NameOf(m.Target);
                default: return m.ToString();
            }
        }

        /// <summary>Changes only the orders panel's status line: the clock ticks without rebuilding its buttons.</summary>
        public void SetOrdersStatus(string status)
        {
            if (ordersStatus != null) ordersStatus.text = status ?? "";
        }

        /// <summary>
        /// The RTX 5080 in the top bar: "held" while this game holds the card, "waiting" while it
        /// waits for it, null to hide it.
        /// </summary>
        public void SetCard(string state)
        {
            if (card == null)
            {
                card = new Label(); card.AddToClassList("card-chip");
                turnLabel.parent.Insert(0, card);
            }
            card.style.display = state == null ? DisplayStyle.None : DisplayStyle.Flex;
            card.EnableInClassList("waiting", state == "waiting");
            card.text = state == "held" ? "RTX 5080 · held" : "RTX 5080 · waiting";
            card.tooltip = state == "held"
                ? "This game holds the RTX 5080's lock until it ends or you leave (Menu). Other sessions wait for it."
                : "Another session holds the RTX 5080. The game takes it when it is free.";
        }

        /// <summary>A Menu button at the top bar's right end: back to the start menu, from any game.</summary>
        public void AddMenuButton(Action pressed)
        {
            var b = new Button(pressed) { text = "Menu", tooltip = "The pause menu (Esc): resume, settings, or back to the main menu." };
            b.AddToClassList("tbtn"); b.style.width = StyleKeyword.Auto; b.style.paddingLeft = 8; b.style.paddingRight = 8; b.style.marginLeft = 8;
            playButton.parent.Add(b);
        }

        public void HideOrders() { orders?.RemoveFromHierarchy(); orders = null; openAction = null; }

        // ---- moves on a panel: a province's or a rival's ----

        private ReplayState lastState;

        /// <summary>Your orders on this province: each move that names it, ordered on click, or why not.</summary>
        private void ProvinceOrders(VisualElement body, int p)
        {
            var req = OrdersRequest;
            var sec = Sec(body, "YOUR ORDERS HERE", req != null ? $"turn {req.Turn}" : "after the turn");
            if (req == null) { sec.Add(Note("Orders open when your council meets.")); return; }
            string id = ProvinceIds[p];
            foreach (var action in new[] { Actions.Attack, Actions.Fortify })
            {
                var m = new Move(action, id);
                string why = Legality.WhyNot(req.World, You, m);
                if (why == null)
                {
                    var legal = req.Legal.First(x => x.Equals(m));
                    string odds = action == Actions.Attack ? $" · wins {Math.Round(Queries.WinChance(lastState, You, p) * 100)}% of rolls" : "";
                    var b = new Button(() => OrderPressed?.Invoke(legal)) { text = $"Order: {Describe(m)}{odds}", tooltip = legal.Detail };
                    b.AddToClassList("order-move"); b.AddToClassList("wide"); if (legal.Equals(Chosen)) b.AddToClassList("pick");
                    sec.Add(b);
                }
                else sec.Add(Note($"{Describe(m)}: {why}"));
            }
            sec.Add(Src("Legality.WhyNot, the rule the models are offered moves by"));
        }

        /// <summary>Your orders towards a rival: war and peace, or why not.</summary>
        private void CountryOrders(VisualElement body, string country)
        {
            var req = OrdersRequest;
            var sec = Sec(body, "YOUR ORDERS", req != null ? $"turn {req.Turn}" : "after the turn");
            if (req == null) { sec.Add(Note("Orders open when your council meets.")); return; }
            foreach (var action in new[] { Actions.DeclareWar, Actions.OfferPeace })
            {
                var m = new Move(action, country);
                string why = Legality.WhyNot(req.World, You, m);
                if (why == null)
                {
                    var legal = req.Legal.First(x => x.Equals(m));
                    var b = new Button(() => OrderPressed?.Invoke(legal)) { text = "Order: " + Describe(m), tooltip = legal.Detail };
                    b.AddToClassList("order-move"); b.AddToClassList("wide"); if (legal.Equals(Chosen)) b.AddToClassList("pick");
                    sec.Add(b);
                }
                else sec.Add(Note($"{Describe(m)}: {why}"));
            }
            sec.Add(Src("Legality.WhyNot, the rule the models are offered moves by"));
        }

        // ---- the envoy card ----

        /// <summary>A peace offer to you, answered with Accept or Refuse.</summary>
        public void ShowOffer(string from, string statement, int truceTurns, Action accept, Action refuse)
        {
            HideOffer();
            var look = CountryLook.Of(from); var player = Players?.GetValueOrDefault(from);
            offerCard = new VisualElement(); offerCard.AddToClassList("panel"); offerCard.AddToClassList("offer");
            offerCard.style.borderTopColor = (Color)look.Colour; offerCard.style.borderTopWidth = 3;
            var head = new VisualElement(); head.AddToClassList("panel-head");
            head.Add(new Icon(IconKind.Peace, 12) { style = { marginRight = 7 } });
            var t = new Label($"{look.Name.ToUpperInvariant()}'S ENVOY"); t.AddToClassList("panel-title"); head.Add(t);
            offerCard.Add(head);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 12, paddingRight = 12, paddingTop = 10 } };
            var face = new Image { image = portraits, uv = Faces.Uv(player?.Persona, look.Slot % 2), scaleMode = ScaleMode.ScaleAndCrop };
            face.AddToClassList("insp-face"); face.style.borderTopColor = face.style.borderBottomColor = face.style.borderLeftColor = face.style.borderRightColor = (Color)look.Colour;
            row.Add(face);
            var col = new VisualElement { style = { marginLeft = 12, flexShrink = 1 } };
            var who = new Label($"{look.Name} · the {player?.Persona ?? "ruler"}"); who.AddToClassList("banner-who"); col.Add(who);
            if (!string.IsNullOrWhiteSpace(statement)) { var l = new Label($"“{statement}”"); l.AddToClassList("banner-line"); col.Add(l); }
            var terms = new Label($"{look.Name} offers peace: a truce of {truceTurns} turns. Neither side may declare war or attack the other until it ends.");
            terms.AddToClassList("why-no"); terms.style.marginTop = 6; col.Add(terms);
            row.Add(col);
            offerCard.Add(row);
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, paddingLeft = 12, paddingRight = 12, paddingBottom = 12, paddingTop = 6 } };
            var refuseB = new Button(() => { HideOffer(); refuse(); }) { text = "Refuse", tooltip = "Fight on" }; refuseB.AddToClassList("menu-button"); refuseB.AddToClassList("quiet"); buttons.Add(refuseB);
            var acceptB = new Button(() => { HideOffer(); accept(); }) { text = "Accept", tooltip = "A truce for " + truceTurns + " turns" }; acceptB.AddToClassList("menu-button"); acceptB.style.marginLeft = 8; buttons.Add(acceptB);
            offerCard.Add(buttons);
            Root.Add(offerCard);
        }

        public void HideOffer() { offerCard?.RemoveFromHierarchy(); offerCard = null; }
        public bool OfferOpen => offerCard != null;
    }
}
