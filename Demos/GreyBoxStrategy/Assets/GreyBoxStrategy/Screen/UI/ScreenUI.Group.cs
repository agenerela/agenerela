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
            var hint = new Label("Shift-click a piece to add or remove it. Esc or a click on the sea clears the choice.");
            hint.AddToClassList("src"); hint.style.paddingLeft = 10; hint.style.paddingRight = 10; hint.style.paddingTop = 6;
            body.Add(hint);
        }
    }
}
