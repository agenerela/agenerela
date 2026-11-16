using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The first-turn help in rule mode: a veil over the screen with a few notes (<see cref="HelpCallouts"/>), each
    /// beside what it explains with an ink line to it, and "Got it". It opens by itself on a first rule game and from
    /// the pause menu at any time.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private VisualElement help;

        public bool HelpOpen => help != null;

        public void ShowHelp(IReadOnlyList<HelpCallout> notes, Action closed = null)
        {
            CloseHelp();
            help = new VisualElement { name = "help", pickingMode = PickingMode.Position };
            help.AddToClassList("help-veil");
            var lines = new HelpLines();
            help.Add(lines);
            Root.Add(help);
            var placed = new List<(VisualElement box, Func<Rect> anchor)>();
            foreach (var n in notes)
            {
                var anchor = HelpAnchor(n.Key);
                if (anchor == null) continue;
                var box = new VisualElement(); box.AddToClassList("help-note");
                var t = new Label(n.Title); t.AddToClassList("help-title"); box.Add(t);
                var x = new Label(n.Text); x.AddToClassList("help-text"); box.Add(x);
                help.Add(box);
                placed.Add((box, anchor));
            }
            var got = new Button(() => { CloseHelp(); closed?.Invoke(); }) { text = "Got it" };
            got.AddToClassList("menu-button"); got.AddToClassList("help-done");
            var foot = new VisualElement(); foot.AddToClassList("help-foot");
            var hint = new Label("This help is in the pause menu (Esc) whenever you want it again."); hint.AddToClassList("src"); foot.Add(hint);
            foot.Add(got);
            help.Add(foot);
            // Placed once laid out, and again a moment later (the council is rebuilt when the advice comes in): beside its
            // anchor, toward the middle, on screen, clear of the council, the panels and the other notes.
            void Place()
            {
                if (help == null) return;
                var area = Root.layout;
                var blocked = new List<Rect>();
                foreach (var e in new[] { orders, Root.Q(className: "chronicle"), foot })
                    if (e != null && e.resolvedStyle.display != DisplayStyle.None && e.worldBound.width > 0) blocked.Add(e.worldBound);
                lines.Links.Clear();
                foreach (var (box, anchor) in placed)
                {
                    var a = anchor();
                    float w = box.resolvedStyle.width, h = box.resolvedStyle.height;
                    if (float.IsNaN(w) || float.IsNaN(h) || a.width <= 0) { box.style.display = DisplayStyle.None; continue; }
                    box.style.display = DisplayStyle.Flex;
                    bool right = a.center.x < area.width / 2;
                    bool below = a.yMax < area.height * 0.3f;
                    var start = below ? new Vector2(a.center.x - w / 2, a.yMax + 18) : new Vector2(right ? a.xMax + 22 : a.xMin - w - 22, a.center.y - h / 2);
                    Vector2 Clamp(Vector2 v) => new Vector2(Mathf.Clamp(v.x, 8, area.width - w - 8), Mathf.Clamp(v.y, 52, area.height - h - 8));
                    bool Free(Vector2 v) { var r = new Rect(v, new Vector2(w, h)); return !blocked.Any(b => b.Overlaps(r)); }
                    var at = Clamp(start);
                    // Up first (the council's notes climb beside it), then down.
                    for (int k = 1; k <= 10 && !Free(at); k++)
                    {
                        var up = Clamp(start - new Vector2(0, k * (h + 8) / 2)); if (Free(up)) { at = up; break; }
                        var down = Clamp(start + new Vector2(0, k * (h + 8) / 2)); if (Free(down)) { at = down; break; }
                    }
                    blocked.Add(new Rect(at, new Vector2(w, h)));
                    box.style.left = at.x; box.style.top = at.y;
                    var from = new Vector2(Mathf.Clamp(a.center.x, at.x, at.x + w), Mathf.Clamp(a.center.y, at.y, at.y + h));
                    var to = new Vector2(Mathf.Clamp(from.x, a.xMin, a.xMax), Mathf.Clamp(from.y, a.yMin, a.yMax));
                    // A target deep inside the council (End turn, at its far corner): the line runs along the panel's
                    // edge and under it, never across what the panel says.
                    var council = orders != null && orders.resolvedStyle.display != DisplayStyle.None ? orders.worldBound : Rect.zero;
                    if (council.width > 0 && council.Overlaps(a) && a.xMin - council.xMin > 40 && from.x < council.xMin)
                    {
                        float under = Mathf.Min(council.yMax + 4, area.height - 2);
                        lines.Links.Add((new[] { from, new Vector2(council.xMin - 6, from.y), new Vector2(council.xMin - 6, under), new Vector2(a.center.x, under), new Vector2(a.center.x, a.yMax) }, a));
                    }
                    else if (council.width > 0 && council.Overlaps(a) && from.y < council.yMin && a.yMin - council.yMin > 40)
                    {
                        // A note above the council: across to the panel's right edge, down beside it, in to the target.
                        float side = Mathf.Min(council.xMax + 6, area.width - 2);
                        var top = new Vector2(Mathf.Clamp(council.xMax, at.x, at.x + w), at.y + h);
                        lines.Links.Add((new[] { top, new Vector2(top.x, (top.y + council.yMin) / 2), new Vector2(side, (top.y + council.yMin) / 2), new Vector2(side, a.center.y), new Vector2(a.xMax, a.center.y) }, a));
                    }
                    else lines.Links.Add((new[] { from, to }, a));
                }
                lines.MarkDirtyRepaint();
            }
            help.schedule.Execute(Place).StartingIn(60);
            help.schedule.Execute(Place).StartingIn(700);
        }

        public void CloseHelp() { help?.RemoveFromHierarchy(); help = null; }

        /// <summary>Where a note points, in panel pixels, or null when that part of the screen is not there.</summary>
        private Func<Rect> HelpAnchor(string key)
        {
            VisualElement Find(params string[] classes)
            {
                foreach (var c in classes)
                {
                    var e = Root.Query(className: c).Where(x => x.resolvedStyle.display != DisplayStyle.None && x.worldBound.width > 0).First();
                    if (e != null) return e;
                }
                return null;
            }
            Func<Rect> Live(params string[] classes) => () => Find(classes)?.worldBound ?? Rect.zero;
            switch (key)
            {
                case "writs": return Find("writs-row") == null ? null : Live("writs-row");
                case "army": return Find("next-army", "army-row") == null ? null : Live("next-army", "army-row");
                case "hatching": return Find("legend") == null ? null : Live("legend");
                case "advisor": return Find("advice-all", "advice-counsel") == null ? null : Live("advice-all", "advice-counsel");
                case "endturn": return Find("end-turn") == null ? null : Live("end-turn");
                case "edge":
                    // A marker if one shows, else the left edge of the map area, where one stands when something lies west.
                    return () => Find("edge-mark")?.worldBound ?? new Rect(FreeMapArea().xMin - 4, FreeMapArea().center.y - 14, 8, 28);
            }
            return null;
        }

        /// <summary>The ink lines from each note to what it explains, with a ring round the target.</summary>
        private sealed class HelpLines : VisualElement
        {
            public readonly List<(Vector2[] path, Rect target)> Links = new List<(Vector2[], Rect)>();
            public HelpLines()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
                generateVisualContent += ctx =>
                {
                    var p = ctx.painter2D;
                    foreach (var (path, target) in Links)
                    {
                        p.strokeColor = new Color(0.93f, 0.8f, 0.5f, 0.9f); p.lineWidth = 1.6f; p.lineJoin = LineJoin.Round;
                        p.BeginPath(); p.MoveTo(path[0]); for (int i = 1; i < path.Length; i++) p.LineTo(path[i]); p.Stroke();
                        p.lineWidth = 1.4f;
                        p.BeginPath();
                        var r = new Rect(target.xMin - 3, target.yMin - 3, target.width + 6, target.height + 6);
                        p.MoveTo(new Vector2(r.xMin, r.yMin)); p.LineTo(new Vector2(r.xMax, r.yMin)); p.LineTo(new Vector2(r.xMax, r.yMax)); p.LineTo(new Vector2(r.xMin, r.yMax)); p.ClosePath();
                        p.Stroke();
                    }
                };
            }
        }
    }
}
