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
                    lines.Links.Add((from, new Vector2(Mathf.Clamp(from.x, a.xMin, a.xMax), Mathf.Clamp(from.y, a.yMin, a.yMax)), a));
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
            public readonly List<(Vector2 from, Vector2 to, Rect target)> Links = new List<(Vector2, Vector2, Rect)>();
            public HelpLines()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
                generateVisualContent += ctx =>
                {
                    var p = ctx.painter2D;
                    foreach (var (from, to, target) in Links)
                    {
                        p.strokeColor = new Color(0.93f, 0.8f, 0.5f, 0.9f); p.lineWidth = 1.6f;
                        p.BeginPath(); p.MoveTo(from); p.LineTo(to); p.Stroke();
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
