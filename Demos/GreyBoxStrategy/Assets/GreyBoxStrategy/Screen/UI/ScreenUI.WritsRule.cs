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
            var body = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, style = { paddingLeft = 10, paddingRight = 10, paddingTop = 7, paddingBottom = 8, maxHeight = 560 } };
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
            if (req.Writs == GameRules.UnlimitedWrits)
            {
                // New game's "Your writs: unlimited": no pips to count, every army awaiting orders is asked.
                writs.Add(new Label($"no limit · {used} used") { style = { fontSize = 12, color = new Color(0.86f, 0.8f, 0.66f) } });
                writs.tooltip = "Your writs have no limit this game (New game, Your writs: unlimited): every army awaiting orders asks you, and your court may give a second order. The rulers keep the rules' count.";
            }
            else
            {
                for (int k = 0; k < req.Writs; k++) { var pip = new VisualElement(); pip.AddToClassList("writ-pip"); pip.EnableInClassList("on", k < used); writs.Add(pip); }
                writs.Add(new Label($"{used} of {req.Writs}") { style = { marginLeft = 6, fontSize = 12, color = new Color(0.86f, 0.8f, 0.66f) } });
                writs.tooltip = $"Writs a turn: {w.Rules.BaseWrits} + 1 for every {w.Rules.ProvincesPerWrit} provinces you own, at most {w.Rules.MaxWrits}. " +
                                "The court's order takes one; each army awaiting orders takes one, the most urgent first, until one is left for the court; unused writs are lost.";
            }
            body.Add(writs);

            // Your advisor's proposal for the turn, when it is on: suggestions only.
            AdvisorSection(body, req);

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
            if (withWrits.Count > 0)
            {
                // On a big world the army waiting may be anywhere: this finds it, as the N key does.
                int open = withWrits.Count(a => !Plan.Armies.ContainsKey(a));
                var next = new Button(() => NextArmyPressed?.Invoke())
                {
                    text = open > 0 ? $"Next army awaiting orders ({open}) · N" : "Next army · N",
                    tooltip = "Choose the next army that has a writ and no order yet, and bring the map to it",
                };
                next.AddToClassList("order-move"); next.AddToClassList("next-army");
                body.Add(next);
            }
            foreach (var army in w.ArmiesOf(You).OrderBy(a => awaiting.IndexOf(a.Id) < 0 ? 99 : awaiting.IndexOf(a.Id)))
                body.Add(ArmyRow(req, army, withWrits, awaiting));
            if (w.ArmiesOf(You).Count() == 0) body.Add(Text("No armies. Recruit at your capital to raise one.", "src"));

            if (SelectedArmy != null && withWrits.Contains(SelectedArmy))
            {
                var moves = WritLegality.ArmyMoves(w, You, SelectedArmy);
                bool canMarch = moves.Any(m => m.Action == Actions.March);
                var hint = Text(canMarch ? $"{Chronicle.Host(SelectedArmy)}: click a lit province to march there, or hold."
                                         : $"{Chronicle.Host(SelectedArmy)}: no march is offered from {Cap(w.FindArmy(SelectedArmy)?.Location)}; it can only hold.", "src"); body.Add(hint);
                var hold = new Button(() => { Plan.Armies[SelectedArmy] = moves.First(m => m.Action == Actions.None); PlanChanged?.Invoke(); ShowOrders(status); }) { text = "Hold", tooltip = "Hold where it stands; it waits for orders again when something wakes it" };
                hold.AddToClassList("order-move"); hold.style.alignSelf = Align.FlexStart; hold.style.marginTop = 2;
                if (Plan.Armies.TryGetValue(SelectedArmy, out var given) && given.Action == Actions.None) hold.AddToClassList("pick");
                body.Add(hold);
                // Why the nearest provinces it may not go to are not offered: three at most, the nearest first; any other, a click on the map says.
                var army = w.FindArmy(SelectedArmy);
                var steps = Steps(army?.Location);
                var not = ProvinceIds.Where(p => p != army?.Location && !moves.Any(m => m.Target == p) && steps.ContainsKey(p))
                    .OrderBy(p => steps[p]).ThenBy(p => p, StringComparer.Ordinal)
                    .Select(p => (p, why: WritLegality.WhyNotArmy(w, You, SelectedArmy, new Move(Actions.March, p))))
                    .Where(x => x.why != null).Take(3).ToList();
                if (not.Count > 0) body.Add(Text("Not offered, the nearest:", "src"));
                foreach (var (p, why) in not) body.Add(Text($"{Cap(p)}: {Words.Destination(why)}", "why-no"));
                if (not.Count > 0) body.Add(Text("Any other province: click it on the map to hear why.", "src"));
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
            // R3's unit types: three recruit orders, one a type, in place of one.
            var recruits = w.Rules.UnitTypes ? Actions.RecruitTypes : new[] { Actions.Recruit };
            foreach (string action in recruits.Concat(new[] { Actions.RaiseTaxes, Actions.Fortify, Actions.DeclareWar, Actions.OfferWhitePeace, Actions.DemandOccupied, Actions.CedeOccupied, Actions.None }))
            {
                var legal = req.Legal.Where(m => m.Action == action).ToList();
                string a = action;
                string why = legal.Count == 0 ? WhyNotCourtAction(w, action) ?? "Not offered this turn." : null;
                var b = new Button(() =>
                {
                    // Not offered: the button stays where it is, dim, and says why rather than going grey and silent.
                    if (why != null) { if (ordersStatus != null) ordersStatus.text = $"{CourtLabel(a)}: {why}"; return; }
                    if (legal.Count == 1 && (legal[0].Target == null || !Actions.TargetsCountry(a) && !Actions.TargetsProvince(a))) Set(second, legal[0], status);
                    else { openCourt = openCourt == a && openCourt2 == second ? null : a; openCourt2 = second; ShowOrders(status); }
                }) { text = CourtLabel(action) };
                b.AddToClassList("order-move");
                if (Actions.RecruitsType(action) is UnitType rt)
                {
                    // Its cost and what it beats, on the button itself, under its name.
                    b.text = "";
                    b.AddToClassList("recruit-type");
                    b.Add(new Label(ArmyLook.RecruitName(action)) { pickingMode = PickingMode.Ignore });
                    var note = new Label(ArmyLook.RecruitNote(w.Rules, rt)) { pickingMode = PickingMode.Ignore }; note.AddToClassList("recruit-note"); b.Add(note);
                }
                if (why != null) { b.AddToClassList("off"); b.tooltip = "Not offered: " + why; }
                else b.tooltip = Actions.RecruitsType(action) is UnitType tt ? ArmyLook.RecruitTooltip(w.Rules, tt, w.Levy(w.Country(You), tt)) : Explain(action);
                if (chosen != null && chosen.Action == action) b.AddToClassList("pick");
                else if (openCourt == action && openCourt2 == second) b.AddToClassList("on");
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
                if (why != null) return (target != null ? (Actions.TargetsCountry(action) ? CountryLook.NameOf(target) : Cap(target)) + ": " : "") + Words.Plain(why);
            }
            return null;
        }

        private VisualElement ArmyRow(CountryRequest req, Army army, List<string> withWrits, List<string> awaiting)
        {
            var row = new VisualElement(); row.AddToClassList("army-row");
            bool hasWrit = withWrits.Contains(army.Id), waits = awaiting.Contains(army.Id);
            if (army.Id == SelectedArmy) row.AddToClassList("pick");
            var head = new VisualElement { style = { width = 150 } };
            head.Add(new Label($"{Chronicle.Host(army.Id)} · {army.Soldiers}") { style = { fontSize = 12, unityFontStyleAndWeight = FontStyle.Bold } });
            // R3: its mix as three small bars, and its general; nothing more when the rules have neither.
            var st = hostState?.Army(army.Id);
            if (st != null && st.HasMix) head.Add(new MixBars(st));
            if (ArmyLook.Leader(st) is string leader) { var gl = new Label(ReplayBuilder.Title(st)) { tooltip = "Led by " + leader }; gl.AddToClassList("army-general"); head.Add(gl); }
            row.Add(head);
            string state;
            if (hasWrit)
            {
                var m = Plan.Armies.TryGetValue(army.Id, out var x) ? x : null;
                state = m == null ? $"at {Cap(army.Location)}, awaiting orders: it has a writ" : m.Action == Actions.March ? $"orders: march to {Cap(m.Target)}" : "orders: hold";
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

        /// <summary>The council's "next army" button: the director does what the N key does.</summary>
        public Action NextArmyPressed;

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
            if (m == null) return Words.Destination(WritLegality.WhyNotArmy(OrdersRequest.World, You, SelectedArmy, new Move(Actions.March, p))) ?? "not offered";
            Plan.Armies[SelectedArmy] = m;
            SelectedArmy = null;
            PlanChanged?.Invoke();
            ShowOrders(ordersStatus?.text);
            return null;
        }

        // Steps from a province to every other, over the map's borders: how near a refused destination is.
        private Dictionary<string, int> Steps(string from)
        {
            var d = new Dictionary<string, int>(StringComparer.Ordinal);
            if (from == null || NeighboursOf == null) return d;
            var q = new Queue<string>(); d[from] = 0; q.Enqueue(from);
            while (q.Count > 0)
            {
                string p = q.Dequeue();
                foreach (string n in NeighboursOf(p)) if (!d.ContainsKey(n)) { d[n] = d[p] + 1; q.Enqueue(n); }
            }
            return d;
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
