using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Rule mode on the writs turn (rules v2, 1.1 and 3.9): your writs counted, your court's orders with the
    /// reason for those not offered, your armies with which gets a writ this turn and which waits for want
    /// of one, an army chosen on the map with its legal destinations lit, a treaty term put to you as a card
    /// with the war score, and fog of war for your country.
    /// </summary>
    public sealed partial class ScreenUI
    {
        /// <summary>Your orders for this writs turn, as the panel builds them; sent with End turn.</summary>
        public RuleSession.WritsPlan Plan = new RuleSession.WritsPlan();
        /// <summary>The army the map's clicks order, or null.</summary>
        public string SelectedArmy;
        /// <summary>The panel changed what is selected or planned: the director redraws the map's highlights.</summary>
        public Action PlanChanged;
        /// <summary>Which provinces border which: for fog of war and the panel.</summary>
        public Func<string, IReadOnlyList<string>> NeighboursOf;

        private string openCourt;
        private bool openCourt2;

        /// <summary>
        /// The armies of yours that get a writ this turn, in the order the rules ask them: awaiting orders,
        /// the most urgent first, until all but one writ is used (one is kept for the court).
        /// </summary>
        public List<string> ArmiesWithWrits(CountryRequest court)
        {
            var w = court.World;
            return WritLegality.Awaiting(w, court.CountryId).Take(Math.Max(0, court.Writs - 1)).Select(a => a.Id).ToList();
        }

        /// <summary>Whether a second court order is asked: the first is not "none" and a writ is left after the armies.</summary>
        public bool SecondCourt(CountryRequest court) =>
            Plan.Court != null && Plan.Court.Action != Actions.None && 1 + ArmiesWithWrits(court).Count < court.Writs;

        /// <summary>Writs your plan spends: the court's (unless none), one an army asked, the second court's.</summary>
        public int WritsUsed(CountryRequest court)
        {
            bool court1 = Plan.Court != null && Plan.Court.Action != Actions.None;
            int armies = ArmiesWithWrits(court).Count;
            bool court2 = SecondCourt(court) && Plan.Court2 != null && Plan.Court2.Action != Actions.None;
            return (court1 ? 1 : 0) + armies + (court2 ? 1 : 0);
        }

        private void ShowWritsOrders(string status)
        {
            var req = OrdersRequest;
            var w = req.World;
            orders.Clear();
            var head = new VisualElement(); head.AddToClassList("panel-head");
            head.Add(new Crest(CountryLook.Of(You), 16) { style = { marginRight = 6 } });
            var t = new Label("YOUR COUNCIL"); t.AddToClassList("panel-title"); head.Add(t);
            var rep = new Button(() => { showReport = !showReport; ShowOrders(status); }) { text = "Report", tooltip = "What your court tells you this turn: the same lines a model ruler reads" };
            rep.AddToClassList("tbtn"); rep.style.width = StyleKeyword.Auto; rep.style.paddingLeft = 7; rep.style.paddingRight = 7;
            if (showReport) rep.AddToClassList("on");
            head.Add(rep);
            orders.Add(head);
            var body = new ScrollView { style = { paddingLeft = 10, paddingRight = 10, paddingTop = 7, paddingBottom = 8, maxHeight = 560 } };
            orders.Add(body);
            if (showReport)
            {
                var f = new VisualElement(); f.AddToClassList("facts-box");
                f.Add(Mono(string.Join("\n", req.StandingFacts)));
                if (req.TurnFacts.Count > 0) f.Add(Mono("\nThis turn: " + string.Join(" ", req.TurnFacts)));
                body.Add(f);
            }

            // The writs: one pip a writ, filled as the plan spends them.
            int used = WritsUsed(req);
            var writs = new VisualElement(); writs.AddToClassList("writs-row");
            writs.Add(new Label("WRITS") { style = { fontSize = 10, letterSpacing = 2, color = new Color(0.55f, 0.5f, 0.4f), width = 52 } });
            for (int k = 0; k < req.Writs; k++) { var pip = new VisualElement(); pip.AddToClassList("writ-pip"); pip.EnableInClassList("on", k < used); writs.Add(pip); }
            writs.Add(new Label($"{used} of {req.Writs}") { style = { marginLeft = 6, fontSize = 12, color = new Color(0.86f, 0.8f, 0.66f) } });
            writs.tooltip = $"Writs a turn: {w.Rules.BaseWrits} + 1 for every {w.Rules.ProvincesPerWrit} provinces you own, at most {w.Rules.MaxWrits}. " +
                            "The court's order takes one; each army awaiting orders takes one, the most urgent first, until one is left for the court; unused writs are lost.";
            body.Add(writs);

            // The court: its order, then a second one if a writ is left.
            body.Add(SectionHead("THE COURT", "one order, a writ"));
            body.Add(CourtRow(req, false, status));
            if (SecondCourt(req))
            {
                body.Add(SectionHead("THE COURT AGAIN", "a writ is left after the armies"));
                body.Add(CourtRow(req, true, status));
            }

            // The armies: which gets a writ, which waits for want of one, which carries out its order.
            var withWrits = ArmiesWithWrits(req);
            var awaiting = WritLegality.Awaiting(w, You).Select(a => a.Id).ToList();
            body.Add(SectionHead("YOUR ARMIES", withWrits.Count < awaiting.Count ? $"{awaiting.Count - withWrits.Count} wait: no writ left" : "click one, then a lit province"));
            foreach (var army in w.ArmiesOf(You).OrderBy(a => awaiting.IndexOf(a.Id) < 0 ? 99 : awaiting.IndexOf(a.Id)))
                body.Add(ArmyRow(req, army, withWrits, awaiting));
            if (w.ArmiesOf(You).Count() == 0) body.Add(Text("No armies. Recruit at your capital to raise one.", "src"));

            if (SelectedArmy != null && withWrits.Contains(SelectedArmy))
            {
                var moves = WritLegality.ArmyMoves(w, You, SelectedArmy);
                var hint = Text($"{Chronicle.Host(SelectedArmy)}: click a lit province to march there, or hold.", "src"); body.Add(hint);
                var hold = new Button(() => { Plan.Armies[SelectedArmy] = moves.First(m => m.Action == Actions.None); PlanChanged?.Invoke(); ShowOrders(status); }) { text = "Hold", tooltip = "Hold where it stands; it waits for orders again when something wakes it" };
                hold.AddToClassList("tbtn"); hold.style.width = StyleKeyword.Auto; hold.style.paddingLeft = 8; hold.style.paddingRight = 8; body.Add(hold);
                // Why the rest of the map is not offered, the nearest first.
                var not = ProvinceIds.Where(p => !moves.Any(m => m.Target == p))
                    .Select(p => (p, why: WritLegality.WhyNotArmy(w, You, SelectedArmy, new Move(Actions.March, p))))
                    .Where(x => x.why != null && !x.why.Contains("more than")).Take(5).ToList();
                foreach (var (p, why) in not) body.Add(Text($"{Cap(p)}: {why}", "why-no"));
            }

            var foot = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 8 } };
            var st = new Label(status ?? ""); st.AddToClassList("orders-status"); st.style.flexGrow = 1; st.style.flexShrink = 1; foot.Add(st);
            ordersStatus = st;
            var end = new Button(() => EndTurnPressed?.Invoke()) { text = "End turn", tooltip = "Send your orders (Enter). Every ruler's orders then land in the rules' order, and every army steps at once." };
            end.AddToClassList("menu-button"); end.AddToClassList("end-turn"); foot.Add(end);
            orders.Add(foot);
            foot.style.paddingLeft = 10; foot.style.paddingRight = 10; foot.style.paddingBottom = 8;
        }

        private VisualElement CourtRow(CountryRequest req, bool second, string status)
        {
            var box = new VisualElement();
            var chosen = second ? Plan.Court2 : Plan.Court;
            var line = new VisualElement(); line.AddToClassList("order-line");
            line.Add(new Label("ORDER") { style = { fontSize = 10, letterSpacing = 2, color = new Color(0.55f, 0.5f, 0.4f), width = 52 } });
            var c = new Label(Describe(chosen ?? Move.Hold)); c.AddToClassList("order-chosen"); line.Add(c);
            box.Add(line);
            var row = new VisualElement(); row.AddToClassList("order-moves");
            var w = req.World;
            foreach (string action in new[] { Actions.Recruit, Actions.RaiseTaxes, Actions.Fortify, Actions.DeclareWar, Actions.OfferWhitePeace, Actions.DemandOccupied, Actions.CedeOccupied, Actions.None })
            {
                var legal = req.Legal.Where(m => m.Action == action).ToList();
                string a = action;
                var b = new Button(() =>
                {
                    if (legal.Count == 1 && (legal[0].Target == null || !Actions.TargetsCountry(a) && !Actions.TargetsProvince(a))) Set(second, legal[0], status);
                    else { openCourt = openCourt == a && openCourt2 == second ? null : a; openCourt2 = second; ShowOrders(status); }
                }) { text = CourtLabel(action) };
                b.AddToClassList("move-btn");
                if (legal.Count == 0) { b.SetEnabled(false); b.tooltip = WhyNotCourtAction(w, action) ?? "Not offered this turn."; }
                else b.tooltip = Explain(action);
                if (chosen != null && chosen.Action == action) b.AddToClassList("pick");
                row.Add(b);
            }
            box.Add(row);
            if (openCourt != null && openCourt2 == second)
            {
                var chips = new VisualElement(); chips.AddToClassList("chips"); chips.style.marginTop = 6;
                foreach (var m in req.Legal.Where(m => m.Action == openCourt))
                {
                    string extra = Actions.IsTerm(m.Action) && lastState != null ? $" · war {lastState.WarScore(You, m.Target):+0;-0;0}" : "";
                    var chip = new Button(() => Set(second, m, status)) { text = (Actions.TargetsCountry(m.Action) ? CountryLook.NameOf(m.Target) : Cap(m.Target)) + extra, tooltip = $"{Describe(m)}: {m.Detail}" };
                    chip.AddToClassList("target-chip"); if (m.Equals(chosen)) chip.AddToClassList("pick");
                    chips.Add(chip);
                }
                box.Add(chips);
                if (Actions.IsTerm(openCourt))
                    box.Add(Text("A term is offered only when your war score pays for it. The other side answers this turn; at a score of 90 a demand cannot be refused.", "src"));
            }
            return box;
        }

        private void Set(bool second, Move m, string status)
        {
            if (second) Plan.Court2 = m; else Plan.Court = m;
            openCourt = null;
            PlanChanged?.Invoke();
            ShowOrders(status);
        }

        private static string CourtLabel(string action)
        {
            switch (action)
            {
                case Actions.OfferWhitePeace: return "White peace";
                case Actions.DemandOccupied: return "Demand land";
                case Actions.CedeOccupied: return "Cede land";
                case Actions.None: return "None";
                default: return Cap(action.Replace('_', ' '));
            }
        }

        // Why no move of this kind is offered: the first reason the rules give for any target.
        private string WhyNotCourtAction(World w, string action)
        {
            IEnumerable<string> targets = Actions.TargetsCountry(action) ? CountryIds.Where(c => c != You) : Actions.TargetsProvince(action) ? ProvinceIds : new string[] { null };
            foreach (string target in targets)
            {
                string why = WritLegality.WhyNotCourt(w, You, new Move(action, target));
                if (why != null) return (target != null ? (Actions.TargetsCountry(action) ? CountryLook.NameOf(target) : Cap(target)) + ": " : "") + why;
            }
            return null;
        }

        private VisualElement ArmyRow(CountryRequest req, Army army, List<string> withWrits, List<string> awaiting)
        {
            var row = new VisualElement(); row.AddToClassList("army-row");
            bool hasWrit = withWrits.Contains(army.Id), waits = awaiting.Contains(army.Id);
            if (army.Id == SelectedArmy) row.AddToClassList("pick");
            row.Add(new Label($"{Chronicle.Host(army.Id)} · {army.Soldiers}") { style = { width = 150, fontSize = 12, unityFontStyleAndWeight = FontStyle.Bold } });
            string state;
            if (hasWrit)
            {
                var m = Plan.Armies.TryGetValue(army.Id, out var x) ? x : null;
                state = m == null ? $"at {Cap(army.Location)}, awaiting: a writ for it" : m.Action == Actions.March ? $"orders: march to {Cap(m.Target)}" : "orders: hold";
                row.AddToClassList(m == null ? "needs" : "ordered");
            }
            else if (waits) { state = $"at {Cap(army.Location)}, awaiting, but no writ left: it holds"; row.AddToClassList("no-writ"); }
            else if (army.Marching) state = $"marching to {Cap(army.Destination)}" + (TurnsLeft != null && hostState?.Army(army.Id) is ArmyState s ? $", {TurnsLeft(s)} turns" : "");
            else state = $"holding at {Cap(army.Location)}";
            var l = new Label(state) { style = { fontSize = 12, flexShrink = 1, whiteSpace = WhiteSpace.Normal } }; row.Add(l);
            row.tooltip = (waits ? "Awaiting orders: " + string.Join(", ", army.Wake) + ". " : "") +
                          (hasWrit ? "Click, then click a lit province on the map." : waits ? "The rules ask armies the most urgent first, one writ each, keeping one for the court." : "Carrying out its order: it costs no writ. It awaits orders again when it arrives, fights, is blocked, or four turns pass.");
            string id = army.Id;
            row.RegisterCallback<ClickEvent>(_ => { SelectedArmy = hasWrit ? (SelectedArmy == id ? null : id) : null; PlanChanged?.Invoke(); ShowOrders(ordersStatus?.text); HostPressed?.Invoke(id); });
            return row;
        }

        /// <summary>What the panel shows of the plan, so the director redraws it only when it changed.</summary>
        public string WritsKey() => Plan.Court + "|" + Plan.Court2 + "|" + string.Join(",", Plan.Armies.Select(kv => kv.Key + ":" + kv.Value));

        /// <summary>The next of your armies that gets a writ and has no order yet (the N key), or the first again.</summary>
        public string NextArmy()
        {
            if (OrdersRequest == null) return null;
            var list = ArmiesWithWrits(OrdersRequest);
            if (list.Count == 0) return null;
            var open = list.Where(a => !Plan.Armies.ContainsKey(a)).ToList();
            var pool = open.Count > 0 ? open : list;
            int k = SelectedArmy == null ? -1 : pool.IndexOf(SelectedArmy);
            SelectedArmy = pool[(k + 1) % pool.Count];
            PlanChanged?.Invoke();
            ShowOrders(ordersStatus?.text);
            return SelectedArmy;
        }

        /// <summary>A province clicked while an army is chosen: its march, or why not.</summary>
        public string OrderArmyTo(int province)
        {
            if (OrdersRequest == null || SelectedArmy == null || province < 0) return null;
            string p = ProvinceIds[province];
            var m = WritLegality.ArmyMoves(OrdersRequest.World, You, SelectedArmy).FirstOrDefault(x => x.Action == Actions.March && x.Target == p);
            if (m == null) return WritLegality.WhyNotArmy(OrdersRequest.World, You, SelectedArmy, new Move(Actions.March, p)) ?? "not offered";
            Plan.Armies[SelectedArmy] = m;
            SelectedArmy = null;
            PlanChanged?.Invoke();
            ShowOrders(ordersStatus?.text);
            return null;
        }

        /// <summary>The chosen army's legal destinations, by province index, for the map's highlight.</summary>
        public HashSet<int> Destinations()
        {
            var set = new HashSet<int>();
            if (OrdersRequest == null || SelectedArmy == null) return set;
            foreach (var m in WritLegality.ArmyMoves(OrdersRequest.World, You, SelectedArmy))
                if (m.Action == Actions.March) { int i = IndexOf(m.Target); if (i >= 0) set.Add(i); }
            return set;
        }

        // ---- a treaty term put to you ----

        /// <summary>A treaty term another ruler puts to you: the term in words, the war score from your side, Accept or Refuse.</summary>
        public void ShowTreaty(CountryRequest offer, string statement, Action accept, Action refuse)
        {
            HideOffer();
            string from = offer.OfferedBy;
            var look = CountryLook.Of(from); var player = Players?.GetValueOrDefault(from);
            offerCard = new VisualElement(); offerCard.AddToClassList("panel"); offerCard.AddToClassList("offer");
            offerCard.style.borderTopColor = (Color)look.Colour; offerCard.style.borderTopWidth = 3;
            var head = new VisualElement(); head.AddToClassList("panel-head");
            head.Add(new Icon(IconKind.Peace, 12) { style = { marginRight = 7 } });
            var t = new Label($"{look.Name.ToUpperInvariant()} PROPOSES A TREATY"); t.AddToClassList("panel-title"); head.Add(t);
            offerCard.Add(head);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 12, paddingRight = 12, paddingTop = 10 } };
            var face = new Image { image = portraits, uv = Faces.Uv(player?.Persona, look.Slot % 2), scaleMode = ScaleMode.ScaleAndCrop };
            face.AddToClassList("insp-face"); face.style.borderTopColor = face.style.borderBottomColor = face.style.borderLeftColor = face.style.borderRightColor = (Color)look.Colour;
            row.Add(face);
            var col = new VisualElement { style = { marginLeft = 12, flexShrink = 1 } };
            var who = new Label($"{look.Name} · the {player?.Persona ?? "ruler"}"); who.AddToClassList("banner-who"); col.Add(who);
            if (!string.IsNullOrWhiteSpace(statement)) { var l = new Label($"“{statement}”"); l.AddToClassList("banner-line"); col.Add(l); }
            string term = offer.TurnFacts.Count > 0 ? offer.TurnFacts[0] : "a treaty";
            var terms = new Label(Named(term)); terms.AddToClassList("why-no"); terms.style.marginTop = 6; col.Add(terms);
            int score = WarScore.Of(offer.World, You, from);
            var sc = new Label($"War score from your side: {score:+0;-0;0}. At {offer.World.Rules.EnforcedScore} a demand lands without being asked; refusing keeps the war going.");
            sc.AddToClassList("src"); sc.style.whiteSpace = WhiteSpace.Normal; col.Add(sc);
            row.Add(col);
            offerCard.Add(row);
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, paddingLeft = 12, paddingRight = 12, paddingBottom = 12, paddingTop = 6 } };
            var refuseB = new Button(() => { HideOffer(); refuse(); }) { text = "Refuse", tooltip = "Fight on" }; refuseB.AddToClassList("menu-button"); refuseB.AddToClassList("quiet"); buttons.Add(refuseB);
            var acceptB = new Button(() => { HideOffer(); accept(); }) { text = "Accept", tooltip = "The treaty lands this turn, and a truce begins" }; acceptB.AddToClassList("menu-button"); acceptB.style.marginLeft = 8; buttons.Add(acceptB);
            offerCard.Add(buttons);
            Root.Add(offerCard);
        }

        // ---- fog of war on the writs turn (v2 3.8) ----

        /// <summary>
        /// The provinces your court sees: its own and those it controls, every province with one of its armies,
        /// and the provinces bordering all of them. Null when there is no fog (watch mode, or the game is over).
        /// </summary>
        public HashSet<string> Sight(ReplayState s)
        {
            if (!Fog.On || GameOver || NeighboursOf == null) return null;
            return WritsSight.Of(s, ProvinceIds, You, NeighboursOf);
        }

        // ---- a ruler repeating itself ----

        private readonly RepeatFilter saidFilter = new RepeatFilter(), quotes = new RepeatFilter();

        /// <summary>Whether this ruler's banner would repeat its last one word for word (see <see cref="RepeatFilter"/>).</summary>
        public bool Repeats(string country, string line) => saidFilter.Repeats(country, line);

        /// <summary>The chronicle's line for a repeated statement: the line itself once, then "said again".</summary>
        public const string SaidAgain = "(said again)";
    }
}
