using System;
using System.Collections.Generic;
using UnityEngine;
using GreyBoxStrategy.Replay;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Markers at the edge of the map area pointing to what a ruler should not lose on a big world: the
    /// turn's battles and their own capital, when those lie off screen. A click brings the camera there.
    /// </summary>
    public sealed partial class ScreenUI
    {
        /// <summary>Something worth pointing to: a province, what it is ("battle" or "capital"), and its words.</summary>
        public sealed class EdgeMark
        {
            public string Kind, Text;
            public int Province;
            public Color Colour;
            public ReplayEvent Event;
        }

        /// <summary>A marker clicked: the director pans there (and opens a battle's report).</summary>
        public Action<EdgeMark> EdgeMarkPressed;

        private readonly List<(VisualElement root, Arrow arrow, Label text)> edgeMarks = new List<(VisualElement, Arrow, Label)>();
        private readonly List<EdgeMark> edgeShown = new List<EdgeMark>();

        /// <summary>
        /// Places a marker for each item lying outside the free map area (between the panels, above the council),
        /// on that area's edge, its arrow pointing at the place. Items on screen get none. Null clears them all.
        /// </summary>
        public void PlaceEdgeMarks(IReadOnlyList<EdgeMark> items)
        {
            var area = FreeMapArea();
            int used = 0;
            var taken = new List<Rect>();
            if (items != null && ProvinceToPanel != null && area.width > 80 && area.height > 80)
                foreach (var it in items)
                {
                    var p = ProvinceToPanel(it.Province);
                    if (area.Contains(p)) continue;
                    var at = EdgePoint(area, p, out float angle);
                    if (used >= edgeMarks.Count) edgeMarks.Add(NewEdgeMark());
                    var (root, arrow, text) = edgeMarks[used];
                    // The whole marker inside the area, by its size at the last layout, never half under a panel.
                    float mw = root.resolvedStyle.width, mh = root.resolvedStyle.height;
                    if (!float.IsNaN(mw) && mw > 1 && area.width > mw) at.x = Mathf.Clamp(at.x, area.xMin + mw / 2, area.xMax - mw / 2);
                    if (!float.IsNaN(mh) && mh > 1 && area.height > mh) at.y = Mathf.Clamp(at.y, area.yMin + mh / 2, area.yMax - mh / 2);
                    if (orders != null && orders.resolvedStyle.display != DisplayStyle.None && orders.worldBound.Contains(at))
                        at.y = orders.worldBound.yMin - 16;
                    // Clear of the map modes and their key, wherever the screen put them (top in rule mode, bottom when watching).
                    foreach (var bar in new[] { legend, modes })
                    {
                        if (bar == null || bar.resolvedStyle.display == DisplayStyle.None || float.IsNaN(mh)) continue;
                        var r = bar.worldBound;
                        if (!r.Overlaps(new Rect(at.x - mw / 2, at.y - mh / 2, mw, mh))) continue;
                        at.y = r.center.y < Root.layout.height / 2 ? r.yMax + mh / 2 + 6 : r.yMin - mh / 2 - 6;
                    }
                    // Two markers on the same stretch of edge stack along it rather than cover each other.
                    if (!float.IsNaN(mw) && mw > 1)
                    {
                        bool side = Mathf.Abs(at.x - area.xMin - mw / 2) < 2 || Mathf.Abs(at.x - area.xMax + mw / 2) < 2;
                        for (int tries = 0; tries < 8; tries++)
                        {
                            var box = new Rect(at.x - mw / 2, at.y - mh / 2, mw, mh);
                            if (!taken.Exists(r => r.Overlaps(box))) break;
                            if (side) at.y += mh + 4; else at.x += mw + 4;
                        }
                        taken.Add(new Rect(at.x - mw / 2, at.y - mh / 2, mw, mh));
                    }
                    if (used < edgeShown.Count) edgeShown[used] = it; else edgeShown.Add(it);
                    root.style.display = DisplayStyle.Flex;
                    root.style.left = at.x; root.style.top = at.y;
                    root.EnableInClassList("battle", it.Kind == "battle");
                    arrow.Colour = it.Colour; arrow.style.rotate = new Rotate(angle); arrow.MarkDirtyRepaint();
                    text.text = it.Text;
                    root.tooltip = it.Text + ". Click: go there.";
                    used++;
                }
            for (int i = used; i < edgeMarks.Count; i++) edgeMarks[i].root.style.display = DisplayStyle.None;
            EdgeMarksShown = used;
        }

        /// <summary>For the Unity CLI: how many edge markers show.</summary>
        public int EdgeMarksShown { get; private set; }

        /// <summary>The map area left free by the panels, in panel pixels.</summary>
        public Rect FreeMapArea()
        {
            var r = Root.layout;
            if (float.IsNaN(r.width) || r.width < 1) return Rect.zero;
            float left = FreeLeft, right = r.width - (InspectorOpen ? 536 : 356), top = 58, bottom = r.height - 54;
            return Rect.MinMaxRect(left + 24, top + 24, right - 24, bottom - 24);
        }

        /// <summary>Where the line from the area's centre to <paramref name="p"/> leaves the area, and its angle in degrees (0 points right).</summary>
        public static Vector2 EdgePoint(Rect area, Vector2 p, out float angle)
        {
            var c = area.center; var d = p - c;
            angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float tx = Mathf.Abs(d.x) < 1e-3f ? float.MaxValue : (area.width / 2) / Mathf.Abs(d.x);
            float ty = Mathf.Abs(d.y) < 1e-3f ? float.MaxValue : (area.height / 2) / Mathf.Abs(d.y);
            return c + d * Mathf.Min(1, Mathf.Min(tx, ty));
        }

        private (VisualElement, Arrow, Label) NewEdgeMark()
        {
            var root = new VisualElement { pickingMode = PickingMode.Position }; root.AddToClassList("edge-mark");
            var arrow = new Arrow(); arrow.AddToClassList("edge-arrow"); root.Add(arrow);
            var text = new Label { pickingMode = PickingMode.Ignore }; text.AddToClassList("edge-text"); root.Add(text);
            int k = edgeMarks.Count;
            root.RegisterCallback<ClickEvent>(_ => { if (k < edgeShown.Count) EdgeMarkPressed?.Invoke(edgeShown[k]); });
            fx.Add(root);
            return (root, arrow, text);
        }

        private sealed class Arrow : VisualElement
        {
            public Color Colour = Color.white;
            public Arrow()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += ctx =>
                {
                    var p = ctx.painter2D; float w = contentRect.width, h = contentRect.height;
                    p.fillColor = Colour; p.strokeColor = new Color(0.08f, 0.05f, 0.02f, 1); p.lineWidth = 1.4f;
                    p.BeginPath(); p.MoveTo(new Vector2(w, h / 2)); p.LineTo(new Vector2(1, 1)); p.LineTo(new Vector2(w * 0.3f, h / 2)); p.LineTo(new Vector2(1, h - 1)); p.ClosePath();
                    p.Fill(); p.Stroke();
                };
            }
        }
    }
}
