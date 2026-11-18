using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Choosing several armies at once (#20, the lead's controls of 16 and 17 November): a left-drag on the map draws a
    /// box and chooses every army of yours inside it, Shift with a click adds or removes one, and a right click on a
    /// province orders all of them there. The left panel shows the group: each army a row, and their soldiers in all.
    /// What a box, a click or a right click does is <see cref="ScreenModels"/>; this file draws it and applies it.
    /// </summary>
    public sealed partial class ScreenUI
    {
        // The armies chosen, in the order they were chosen; SelectedArmy is the first, for the council's one-army view.
        private readonly List<string> chosenArmies = new List<string>();

        /// <summary>The armies chosen on the map, the first being <see cref="SelectedArmy"/>.</summary>
        public IReadOnlyList<string> ChosenArmies => chosenArmies;

        public bool IsChosen(string id) => id != null && chosenArmies.Contains(id);

        /// <summary>Chooses these armies, replacing the others; none clears the choice.</summary>
        public void ChooseArmies(IEnumerable<string> ids)
        {
            chosenArmies.Clear();
            foreach (var id in ids ?? Enumerable.Empty<string>()) if (id != null && !chosenArmies.Contains(id)) chosenArmies.Add(id);
        }

        /// <summary>Redraws what shows the choice: the outliner's rows (the pieces redraw each frame).</summary>
        public void RefreshChoice() { if (lastState != null) RenderOutliner(lastState); }

        // ---- the box ----

        private VisualElement boxMark;

        /// <summary>Draws the selection box between two points, in the root's coordinates.</summary>
        public void ShowBox(Vector2 a, Vector2 b)
        {
            if (boxMark == null)
            {
                boxMark = new VisualElement { pickingMode = PickingMode.Ignore };
                boxMark.AddToClassList("select-box");
                // Its look is here rather than in the stylesheet: a gilt outline and a light fill.
                boxMark.style.position = Position.Absolute;
                boxMark.style.borderTopWidth = boxMark.style.borderBottomWidth = boxMark.style.borderLeftWidth = boxMark.style.borderRightWidth = 1.5f;
                var gilt = new Color32(226, 194, 122, 230);
                boxMark.style.borderTopColor = boxMark.style.borderBottomColor = boxMark.style.borderLeftColor = boxMark.style.borderRightColor = (Color)gilt;
                boxMark.style.backgroundColor = new Color(0.89f, 0.76f, 0.48f, 0.12f);
                fx.Add(boxMark);
            }
            boxMark.style.left = Mathf.Min(a.x, b.x); boxMark.style.top = Mathf.Min(a.y, b.y);
            boxMark.style.width = Mathf.Abs(a.x - b.x); boxMark.style.height = Mathf.Abs(a.y - b.y);
            boxMark.style.display = DisplayStyle.Flex;
        }

        public void HideBox() { if (boxMark != null) boxMark.style.display = DisplayStyle.None; }

        /// <summary>Where each army's piece stands on the screen (its foot), in the root's coordinates, with its owner.</summary>
        public List<(string id, string owner, float x, float y)> PiecePoints()
        {
            var list = new List<(string, string, float, float)>();
            if (hostState == null) return list;
            foreach (var a in hostState.Armies)
            {
                if (!hosts.TryGetValue(a.Id, out var piece) || piece.resolvedStyle.display == DisplayStyle.None) continue;
                var r = Root.WorldToLocal(piece.worldBound);
                if (r.width <= 0) continue;
                list.Add((a.Id, a.Owner, r.center.x, r.yMax));
            }
            return list;
        }

        // ---- orders for the group ----

        /// <summary>
        /// A right click on a province with armies chosen: each that can march there is ordered to, in the outliner's
        /// order; each that cannot is named with why. The choice is cleared once at least one is ordered.
        /// </summary>
        public (List<string> ordered, List<(string id, string why)> refused) OrderChosenTo(int province)
        {
            var ordered = new List<string>(); var refused = new List<(string, string)>();
            if (OrdersRequest == null || province < 0 || chosenArmies.Count == 0) return (ordered, refused);
            string p = ProvinceIds[province];
            var w = OrdersRequest.World;
            var order = w.ArmiesOf(You).Select(a => a.Id).Where(chosenArmies.Contains).ToList();
            foreach (var id in order)
            {
                var m = WritLegality.ArmyMoves(w, You, id).FirstOrDefault(x => x.Action == Actions.March && x.Target == p);
                if (m == null) { refused.Add((id, Words.Destination(WritLegality.WhyNotArmy(w, You, id, new Move(Actions.March, p))) ?? "not offered")); continue; }
                Plan.Armies[id] = m;
                ordered.Add(id);
            }
            if (ordered.Count > 0) { chosenArmies.Clear(); PlanChanged?.Invoke(); ShowOrders(ordersStatus?.text); }
            return (ordered, refused);
        }

        /// <summary>The notice after a group order: who could not go, and why. Null when every one went.</summary>
        public static string GroupNotice(string province, IReadOnlyList<(string id, string why)> refused)
        {
            if (refused.Count == 0) return null;
            return $"{province}: " + string.Join("; ", refused.Select(r => $"{Chronicle.Host(r.id)} {r.why}"));
        }

        // ---- merge and split (the build session's rules, RuleSession.CanMerge/Merge/CanSplit/Split) ----

        /// <summary>Whether these armies can merge now: null, or why not. Set by the director; null outside your council.</summary>
        public Func<IReadOnlyList<string>, string> CanMergeArmies;
        /// <summary>Whether this army can split so many soldiers off now: null, or why not.</summary>
        public Func<string, int, string> CanSplitArmy;
        /// <summary>The Merge button or M: merges the chosen armies.</summary>
        public Action MergePressed;
        /// <summary>The split's confirm button: splits this many soldiers off the chosen army.</summary>
        public Action<int> SplitPressed;

        private bool splitOpen;
        private SliderInt splitSlider;

        /// <summary>The Merge button for the chosen armies: enabled when the rules allow it, the reason in its tooltip when not.</summary>
        private VisualElement MergeRow()
        {
            string why = CanMergeArmies == null ? "only in your council" : CanMergeArmies(chosenArmies);
            var b = new Button(() => MergePressed?.Invoke()) { text = "Merge (M)" };
            b.AddToClassList("menu-button"); b.name = "merge-armies";
            b.tooltip = why == null ? "Merge them into one army under the senior general, at once and for no order" : "Cannot merge: " + why;
            b.SetEnabled(why == null);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 10, paddingRight = 10, paddingTop = 6 } };
            row.Add(b);
            if (why != null) { var l = new Label(why); l.AddToClassList("why-no"); l.style.marginLeft = 8; l.style.alignSelf = Align.Center; l.style.flexShrink = 1; row.Add(l); }
            return row;
        }

        /// <summary>
        /// Under one chosen army of yours in the left panel: Split…, and when open a slider (soldiers to detach, half by
        /// default; each type goes in proportion, as the rules split it) and its confirm button, disabled with the reason
        /// in its tooltip when the rules say no. S opens and closes it.
        /// </summary>
        public void AddSplitControl(string id, ReplayState s)
        {
            var a = s?.Army(id);
            if (a == null || CanSplitArmy == null || !SideOpen) return;
            var body = side.Q<ScrollView>();
            if (body == null) return;
            var box = new VisualElement { name = "split-control", style = { paddingLeft = 10, paddingRight = 10, paddingTop = 8, paddingBottom = 8 } };
            var toggle = new Button(() => { splitOpen = !splitOpen; AddSplitControlAgain(id, s); }) { text = splitOpen ? "Split ▾" : "Split… (S)" };
            toggle.AddToClassList("menu-button"); toggle.AddToClassList("quiet");
            box.Add(toggle);
            if (splitOpen && a.Soldiers >= 2)
            {
                int start = Math.Max(1, a.Soldiers / 2);
                splitSlider = new SliderInt("Soldiers to detach", 1, a.Soldiers - 1) { value = start };
                splitSlider.style.marginTop = 6;
                var go = new Button { text = "" }; go.AddToClassList("menu-button"); go.name = "split-army";
                void Refresh(int n)
                {
                    string why = CanSplitArmy(id, n);
                    go.text = $"Split off {n}, keep {a.Soldiers - n}";
                    go.tooltip = why == null ? "A new army stands here with its own general; it holds" : "Cannot split: " + why;
                    go.SetEnabled(why == null);
                }
                splitSlider.RegisterValueChangedCallback(e => Refresh(e.newValue));
                go.clicked += () => SplitPressed?.Invoke(splitSlider.value);
                Refresh(start);
                box.Add(splitSlider); box.Add(go);
            }
            else if (splitOpen) box.Add(new Label("An army of one soldier cannot split.") { name = "split-none" });
            body.Add(box);
        }

        private void AddSplitControlAgain(string id, ReplayState s)
        {
            side.Q("split-control")?.RemoveFromHierarchy();
            AddSplitControl(id, s);
        }

        /// <summary>S: opens or closes the split under the chosen army.</summary>
        public void ToggleSplit(ReplayState s)
        {
            if (chosenArmies.Count != 1) return;
            splitOpen = !splitOpen;
            AddSplitControlAgain(chosenArmies[0], s);
        }

        /// <summary>For the CLI and tests: the split slider's value, or -1 when it is closed.</summary>
        public int SplitValue => splitOpen && splitSlider != null && splitSlider.parent != null ? splitSlider.value : -1;

        // ---- the group view ----

        /// <summary>The left panel with several armies chosen: each a row (a click opens it alone), and their soldiers in all.</summary>
        public void ShowGroup(ReplayState s)
        {
            var armies = chosenArmies.Select(id => s?.Army(id)).Where(a => a != null).ToList();
            if (armies.Count == 0) { CloseSide(); return; }
            var body = SideHead("ARMIES");
            int total = armies.Sum(a => a.Soldiers);
            body.Add(KitHeader(CountryLook.Of(You), $"{armies.Count} armies", $"{total} soldiers in all · right-click a province to march them there"));
            foreach (var a in armies)
            {
                string doing = a.Marching ? $"to {Cap(a.Destination)}" : $"at {Cap(a.Location)}";
                var row = KitRow(new ScreenModels.Row { Key = "army-" + a.Id, Label = Chronicle.Host(a.Id), Value = a.Soldiers.ToString(), Tip = $"{Chronicle.Host(a.Id)}, {doing}. Click: this army alone." },
                    IconKind.Army, new Color32(196, 204, 214, 255));
                string id = a.Id;
                row.RegisterCallback<ClickEvent>(_ => HostPressed?.Invoke(id));
                body.Add(row);
            }
            body.Add(MergeRow());
            var hint = new Label("Shift-click a piece to add or remove it. Esc or a click on the sea clears the choice.");
            hint.AddToClassList("src"); hint.style.paddingLeft = 10; hint.style.paddingRight = 10; hint.style.paddingTop = 6;
            body.Add(hint);
        }
    }
}
