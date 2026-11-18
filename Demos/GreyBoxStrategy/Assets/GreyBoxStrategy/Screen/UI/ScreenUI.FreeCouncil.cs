using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The council with free orders on (<see cref="GameRules.FreeOrders"/>, the lead's turn of 17 November): no writs. The
    /// court is a list you build, as many orders as gold allows, each row with its cost and a mark to take it back; every
    /// army may be ordered; End turn says how many rulers are still deciding in the background. With the switch off the
    /// writs council (<see cref="ShowWritsOrders"/>) is drawn exactly as before.
    /// </summary>
    public sealed partial class ScreenUI
    {
        /// <summary>The model rulers still deciding this council, by country id (<c>RuleSession.RulersStillDeciding</c>).</summary>
        public Func<IReadOnlyCollection<string>> Deciding;

        /// <summary>Whether a first-turn hint was dismissed for good, by id; and dismissing one. Null: every hint shows.</summary>
        public Func<string, bool> HintSeen;
        public Action<string> DismissHint;

        /// <summary>The first first-turn hint: what free orders change, at the top of the council.</summary>
        public const string FreeOrdersHint = "free-orders";
        public const string FreeOrdersHintText = "No writs: give as many orders as your gold allows; right-click a province to march the armies you have selected.";

        // End turn was pressed while rulers still decide: the button says so until the turn resolves.
        private bool endWaiting;
        private CountryRequest endWaitingFor;

        /// <summary>Your court's orders this turn, as the panel builds them into <see cref="Plan"/>.</summary>
        public CourtList Court(CountryRequest req) => new CourtList(Plan.Courts, req.World.Country(You)?.Gold ?? 0,
            m => WritLegality.CourtOrderCost(req.World, You, m), given => WritLegality.CourtMovesAfter(req.World, You, given));

        private void ShowFreeOrders(string status)
        {
            var req = OrdersRequest;
            var w = req.World;
            if (endWaitingFor != req) { endWaiting = false; endWaitingFor = req; }
            marchLines?.MarkDirtyRepaint();
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
            // The first of the first-turn hints: one line, until you close it once.
            if (HintSeen?.Invoke(FreeOrdersHint) != true)
            {
                var hint = new VisualElement(); hint.AddToClassList("hint-line");
                var hl = new Label(FreeOrdersHintText); hl.AddToClassList("hint-text"); hint.Add(hl);
                var hx = new Button(() => { DismissHint?.Invoke(FreeOrdersHint); hint.RemoveFromHierarchy(); }) { text = "×", tooltip = "Got it: do not show this again" };
                hx.AddToClassList("tbtn"); hx.AddToClassList("hint-x"); hint.Add(hx);
                body.Add(hint);
            }
            if (showReport)
            {
                var f = new VisualElement(); f.AddToClassList("facts-box");
                f.Add(Mono(string.Join("\n", req.StandingFacts)));
                if (req.TurnFacts.Count > 0) f.Add(Mono("\nThis turn: " + string.Join(" ", req.TurnFacts)));
                body.Add(f);
            }
            if (PlanRefusals != null && PlanRefusals.Count > 0)
                body.Add(Note("Not carried out last turn: " + string.Join("; ", PlanRefusals.Select(r =>
                {
                    int at = r.IndexOf(": ", StringComparison.Ordinal);
                    return at > 0 ? $"{Chronicle.Host(r.Substring(0, at))}, {r.Substring(at + 2)}" : r;
                })) + ".", "bad"));
            if (Treasury(w) is string dry) body.Add(Note(dry, "bad"));
            AdvisorSection(body, req);

            // ---- the court: a list you build ----
            var court = Court(req);
            body.Add(SectionHead("THE COURT", "as many orders as your gold allows"));
            var list = new VisualElement(); list.AddToClassList("court-list");
            if (court.Orders.Count == 0) list.Add(Text("No court orders yet. Each button below adds one; the court gives them in this order.", "src"));
            for (int i = 0; i < court.Orders.Count; i++)
            {
                var m = court.Orders[i]; int at = i;
                var row = new VisualElement(); row.AddToClassList("court-row");
                var n = new Label($"{i + 1}."); n.AddToClassList("court-n"); row.Add(n);
                var what = new Label(Describe(m)) { tooltip = m.Detail }; what.AddToClassList("court-what"); row.Add(what);
                int cost = court.CostOf(m);
                if (cost > 0) { var c = new Label($"−{cost} gold"); c.AddToClassList("court-cost"); row.Add(c); }
                var x = new Button(() =>
                {
                    var dropped = court.RemoveAt(at);
                    lastCourtNote = dropped.Count == 0 ? null : "Taken out with it, no longer offered: " + string.Join(", ", dropped.Select(Describe)) + ".";
                    PlanChanged?.Invoke(); ShowOrders(status);
                }) { text = "×", tooltip = "Take this order back" };
                x.AddToClassList("tbtn"); x.AddToClassList("court-x"); row.Add(x);
                list.Add(row);
            }
            body.Add(list);
            if (lastCourtNote != null) { body.Add(Note(lastCourtNote, "bad")); lastCourtNote = null; }
            // The treasury line: gold now, what your orders hold back, what is left for more.
            var gold = new VisualElement { tooltip = "Gold your court's orders spend is held back as you add them, and paid when they land." }; gold.AddToClassList("court-gold");
            gold.Add(new Icon(IconKind.Gold, 13) { Tint = new Color32(232, 196, 92, 255) });
            gold.Add(new Label($"{court.Gold} gold"));
            gold.Add(new Label($"· {court.Committed} committed") { name = "court-committed" });
            var left = new Label($"· {court.Left} left"); left.AddToClassList(court.Left > 0 ? "good" : "bad"); gold.Add(left);
            body.Add(gold);
            // The buttons: each adds an order; one shut says why, in its tooltip and the status line.
            var buttons = new VisualElement(); buttons.AddToClassList("order-moves");
            var recruits = w.Rules.UnitTypes ? Actions.RecruitTypes : new[] { Actions.Recruit };
            foreach (string action in recruits.Concat(new[] { Actions.RaiseTaxes, Actions.Fortify, Actions.DeclareWar, Actions.OfferWhitePeace, Actions.DemandOccupied, Actions.CedeOccupied }))
            {
                var offered = court.Offered(action);
                string a = action;
                string why = offered.Count > 0 ? null : court.WhyShut(action) ?? WhyNotCourtAction(w, action) ?? "not offered this turn";
                var b = new Button(() =>
                {
                    if (why != null) { if (ordersStatus != null) ordersStatus.text = $"{CourtLabel(a)}: {why}"; return; }
                    if (offered.Count == 1 && (offered[0].Target == null || !Actions.TargetsCountry(a) && !Actions.TargetsProvince(a))) { court.Add(offered[0]); openCourt = null; PlanChanged?.Invoke(); ShowOrders(status); }
                    else { openCourt = openCourt == a ? null : a; ShowOrders(status); }
                }) { text = CourtLabel(action) };
                b.AddToClassList("order-move");
                if (Actions.RecruitsType(action) is UnitType rt)
                {
                    b.text = "";
                    b.AddToClassList("recruit-type");
                    b.Add(new Label(ArmyLook.RecruitName(action)) { pickingMode = PickingMode.Ignore });
                    var note = new Label(ArmyLook.RecruitNote(w.Rules, rt)) { pickingMode = PickingMode.Ignore }; note.AddToClassList("recruit-note"); b.Add(note);
                }
                if (why != null) { b.AddToClassList("off"); b.tooltip = "Not offered: " + why; }
                else b.tooltip = Actions.RecruitsType(action) is UnitType tt ? ArmyLook.RecruitTooltip(w.Rules, tt, w.Levy(w.Country(You), tt)) : Explain(action);
                if (openCourt == action) b.AddToClassList("on");
                buttons.Add(b);
            }
            body.Add(buttons);
            if (openCourt != null)
            {
                var chips = new VisualElement(); chips.AddToClassList("chips"); chips.style.marginTop = 6;
                foreach (var m in court.Offered(openCourt))
                {
                    string extra = Actions.IsTerm(m.Action) && lastState != null ? $" · war {lastState.WarScore(You, m.Target):+0;-0;0}" : "";
                    var mm = m;
                    var chip = new Button(() => { court.Add(mm); openCourt = null; PlanChanged?.Invoke(); ShowOrders(status); })
                        { text = (Actions.TargetsCountry(m.Action) ? CountryLook.NameOf(m.Target) : Cap(m.Target)) + extra, tooltip = $"{Describe(m)}: {m.Detail}" };
                    chip.AddToClassList("target-chip");
                    chips.Add(chip);
                }
                body.Add(chips);
            }

            // ---- the armies: every one may be ordered ----
            var all = w.ArmiesOf(You).Select(a => a.Id).ToList();
            var awaiting = WritLegality.Awaiting(w, You).Select(a => a.Id).ToList();
            body.Add(SectionHead("YOUR ARMIES", all.Count == 0 ? "" : "every army: click one, then right-click a lit province"));
            if (awaiting.Count > 0)
            {
                int open = awaiting.Count(a => !Plan.Armies.ContainsKey(a));
                var next = new Button(() => NextArmyPressed?.Invoke()) { text = open > 0 ? $"Next army awaiting orders ({open}) · N" : "Next army · N", tooltip = "Choose the next army awaiting orders, and bring the map to it" };
                next.AddToClassList("order-move"); next.AddToClassList("next-army");
                body.Add(next);
            }
            foreach (var army in w.ArmiesOf(You).OrderBy(a => awaiting.IndexOf(a.Id) < 0 ? 99 : awaiting.IndexOf(a.Id)))
                body.Add(ArmyRow(req, army, all, awaiting));
            if (all.Count == 0) body.Add(Text("No armies. Recruit at your capital to raise one.", "src"));
            if (SelectedArmy != null && all.Contains(SelectedArmy))
            {
                var moves = WritLegality.ArmyMoves(w, You, SelectedArmy);
                bool canMarch = moves.Any(m => m.Action == Actions.March);
                body.Add(Text(canMarch ? $"{Chronicle.Host(SelectedArmy)}: right-click a lit province to march there, or hold."
                                       : $"{Chronicle.Host(SelectedArmy)}: no march is offered from {Cap(w.FindArmy(SelectedArmy)?.Location)}; it can only hold.", "src"));
                var hold = new Button(() => { Plan.Armies[SelectedArmy] = moves.First(m => m.Action == Actions.None); PlanChanged?.Invoke(); ShowOrders(status); }) { text = "Hold", tooltip = "Hold where it stands; it keeps that order until something wakes it" };
                hold.AddToClassList("order-move"); hold.style.alignSelf = Align.FlexStart; hold.style.marginTop = 2;
                if (Plan.Armies.TryGetValue(SelectedArmy, out var given) && given.Action == Actions.None) hold.AddToClassList("pick");
                body.Add(hold);
            }

            // ---- End turn, and the rulers still deciding in the background ----
            var foot = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 8 } };
            var st = new Label(status ?? ""); st.AddToClassList("orders-status"); st.style.flexGrow = 1; st.style.flexShrink = 1; foot.Add(st);
            ordersStatus = st;
            var deciding = new VisualElement(); deciding.AddToClassList("deciding"); foot.Add(deciding);
            var end = new Button(() => { if ((Deciding?.Invoke()?.Count ?? 0) > 0) endWaiting = true; ShowDeciding(deciding, null); EndTurnPressed?.Invoke(); })
                { text = "End turn", tooltip = "Send your orders (Enter). The turn resolves once every ruler has decided; most already have." };
            end.AddToClassList("menu-button"); end.AddToClassList("end-turn"); foot.Add(end);
            foot.AddToClassList("orders-foot");
            orders.Add(foot);
            foot.style.paddingLeft = 10; foot.style.paddingRight = 10; foot.style.paddingBottom = 8; foot.style.flexShrink = 0;
            // The count follows the background deciding without redrawing the panel.
            ShowDeciding(deciding, end);
            foot.schedule.Execute(() => ShowDeciding(deciding, end)).Every(300);
            KeepLayout();
        }

        private string lastCourtNote;

        private void ShowDeciding(VisualElement into, Button end)
        {
            var who = Deciding?.Invoke() ?? Array.Empty<string>();
            string key = string.Join(",", who.OrderBy(x => x, StringComparer.Ordinal)) + "|" + endWaiting;
            if (into.userData as string == key) return;
            into.userData = key;
            into.Clear();
            into.style.display = who.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            into.tooltip = who.Count == 0 ? null : "Still deciding, on this turn's snapshot: " + string.Join(", ", who.Select(CountryLook.NameOf));
            foreach (string c in who.OrderBy(x => x, StringComparer.Ordinal)) into.Add(new Crest(CountryLook.Of(c), 16));
            if (who.Count > 0) into.Add(new Label(DecidingLine(who.Count)));
            if (end == null) return;
            bool waiting = endWaiting && who.Count > 0;
            end.text = waiting ? $"Waiting for {who.Count}…" : "End turn";
            end.EnableInClassList("waiting", waiting);
        }

        /// <summary>"2 rulers still deciding"; the foot's words.</summary>
        public static string DecidingLine(int n) => n == 1 ? "1 ruler still deciding" : $"{n} rulers still deciding";
    }
}
