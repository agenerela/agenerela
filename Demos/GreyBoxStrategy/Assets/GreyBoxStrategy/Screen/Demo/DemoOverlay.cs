using System;
using System.Collections.Generic;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// What a demo draws over the screen, in UI Toolkit on the screen's own panel so it scales with it: the cursor
    /// and its press, callouts with leader lines, the dim around one element, the chapter label, a title card and a
    /// fade. It only draws; <see cref="DemoDirector"/> decides what and when. Every motion runs on game time
    /// (<see cref="Tick"/>), so a capture at a fixed frame rate is the same every take.
    /// </summary>
    public sealed class DemoOverlay
    {
        // The gilt palette of PaintedRealm.uss.
        public static readonly Color Gilt = new Color32(210, 173, 98, 255);
        public static readonly Color GiltSoft = new Color32(201, 164, 90, 140);
        public static readonly Color Ink = new Color32(18, 15, 12, 255);
        public static readonly Color Parchment = new Color32(250, 244, 230, 255);

        public readonly VisualElement Root;
        private readonly Painted under, over;
        private readonly VisualElement boxes, chapter, titleCard, fade;
        private readonly Label chapterTitle, chapterLine, titleText, titleLine;
        private readonly List<Callout> callouts = new List<Callout>();

        // dim
        private Func<Rect?> dimTarget;
        private float dimPad = 10, dimAlpha, dimGoal;
        private Rect dimRect;
        private bool dimRectSet;

        // cursor, in panel units; its tip is the point
        public Vector2 CursorAt = new Vector2(1180, 640);
        public float CursorAlpha, CursorGoal;
        public float CursorPress; // 0 up, 1 fully pressed
        private readonly List<(Vector2 at, float t)> ripples = new List<(Vector2, float)>();

        // chapter, title, fade: 0 to 1 toward a goal
        private float chapterAlpha, chapterGoal, titleAlpha, titleGoal, fadeAlpha, fadeGoal, fadeSpeed = 1 / DemoDefaults.FadeSeconds;

        private sealed class Callout
        {
            public Func<Rect?> Target;
            public VisualElement Box;
            public string Side;
            public float Width;
            public float Alpha, Goal = 1, Grow;
            public DemoLayout.Placement Place;
            public bool Placed;
            public Rect LastTarget;
        }

        public DemoOverlay(VisualElement screenRoot)
        {
            Root = new VisualElement { name = "demo-overlay", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("demo-overlay");
            under = new Painted(DrawUnder) { name = "demo-under" };
            boxes = new VisualElement { name = "demo-boxes", pickingMode = PickingMode.Ignore }; boxes.AddToClassList("demo-fill");
            over = new Painted(DrawOver) { name = "demo-over" };

            chapter = new VisualElement { pickingMode = PickingMode.Ignore }; chapter.AddToClassList("demo-chapter");
            chapterTitle = new Label(); chapterTitle.AddToClassList("demo-chapter-title"); chapter.Add(chapterTitle);
            chapterLine = new Label(); chapterLine.AddToClassList("demo-chapter-line"); chapter.Add(chapterLine);

            titleCard = new VisualElement { pickingMode = PickingMode.Ignore }; titleCard.AddToClassList("demo-title-veil");
            var card = new VisualElement { pickingMode = PickingMode.Ignore }; card.AddToClassList("demo-title-card");
            titleText = new Label(); titleText.AddToClassList("demo-title"); card.Add(titleText);
            var rule = new VisualElement(); rule.AddToClassList("demo-title-rule"); card.Add(rule);
            titleLine = new Label(); titleLine.AddToClassList("demo-title-line"); card.Add(titleLine);
            titleCard.Add(card);

            fade = new VisualElement { pickingMode = PickingMode.Ignore }; fade.AddToClassList("demo-fade");

            var chapterRow = new VisualElement { pickingMode = PickingMode.Ignore }; chapterRow.AddToClassList("demo-chapter-row"); chapterRow.Add(chapter);
            Root.Add(under); Root.Add(boxes); Root.Add(chapterRow); Root.Add(titleCard); Root.Add(over); Root.Add(fade);
            chapter.style.opacity = 0; titleCard.style.opacity = 0; fade.style.opacity = 0;
            screenRoot.Add(Root);
        }

        /// <summary>The panel's size in panel units.</summary>
        public Rect PanelRect => Root.parent?.layout ?? new Rect(0, 0, 1920, 1080);

        // ---- what the director calls ----

        public void AddCallout(Func<Rect?> target, string title, string line, string side, float width)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore }; box.AddToClassList("demo-callout");
            box.style.width = width;
            var t = new Label(title); t.AddToClassList("demo-callout-title"); box.Add(t);
            if (!string.IsNullOrEmpty(line)) { var l = new Label(line); l.AddToClassList("demo-callout-line"); box.Add(l); }
            box.style.opacity = 0;
            boxes.Add(box);
            callouts.Add(new Callout { Target = target, Box = box, Side = side, Width = width });
        }

        public void Dim(Func<Rect?> target, float pad) { dimTarget = target; dimPad = pad; dimGoal = 1; dimRectSet = false; }

        /// <summary>Callouts and the dim fade away.</summary>
        public void Clear()
        {
            foreach (var c in callouts) c.Goal = 0;
            dimGoal = 0;
        }

        public void Chapter(string title, string line)
        {
            chapterTitle.text = title;
            chapterLine.text = line ?? "";
            chapterLine.style.display = string.IsNullOrEmpty(line) ? DisplayStyle.None : DisplayStyle.Flex;
            chapterAlpha = 0; chapterGoal = 1;
        }

        public void ClearChapter() => chapterGoal = 0;

        public void Title(string title, string line)
        {
            titleText.text = title;
            titleLine.text = line ?? "";
            titleLine.style.display = string.IsNullOrEmpty(line) ? DisplayStyle.None : DisplayStyle.Flex;
            titleGoal = 1;
        }

        public void HideTitle() => titleGoal = 0;
        public bool TitleShown => titleAlpha > 0.001f;

        public void Fade(bool toBlack, float seconds) { fadeGoal = toBlack ? 1 : 0; fadeSpeed = 1 / Mathf.Max(0.01f, seconds); }

        /// <summary>Black at once: the state a <c>fade in</c> starts from.</summary>
        public void Black() { fadeAlpha = fadeGoal = 1; fade.style.opacity = 1; }

        public void Ripple(Vector2 at) => ripples.Add((at, 0));

        // ---- each frame ----

        public void Tick(float dt)
        {
            if (Root.parent != null && Root.parent.IndexOf(Root) != Root.parent.childCount - 1) Root.BringToFront(); // a window brought to the front never covers the demo
            CursorAlpha = Toward(CursorAlpha, CursorGoal, dt / 0.25f);
            dimAlpha = Toward(dimAlpha, dimGoal, dt / DemoDefaults.DimIn);
            if (dimTarget != null)
            {
                var r = dimTarget();
                if (r.HasValue)
                {
                    var want = Grow(r.Value, dimPad);
                    // The hole eases to where its element is, so a panel opening under it does not make it jump.
                    dimRect = dimRectSet ? LerpRect(dimRect, want, Mathf.Clamp01(dt * 12)) : want;
                    dimRectSet = true;
                }
            }
            if (dimGoal == 0 && dimAlpha == 0) dimTarget = null;

            chapterAlpha = Toward(chapterAlpha, chapterGoal, dt / 0.45f);
            titleAlpha = Toward(titleAlpha, titleGoal, dt / 0.5f);
            fadeAlpha = Toward(fadeAlpha, fadeGoal, dt * fadeSpeed);
            chapter.style.opacity = DemoDefaults.Ease(chapterAlpha);
            chapter.style.translate = new Translate(0, (1 - DemoDefaults.Ease(chapterAlpha)) * 10);
            titleCard.style.opacity = DemoDefaults.Ease(titleAlpha);
            titleCard.style.display = titleAlpha > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            fade.style.opacity = fadeAlpha;

            var screen = PanelRect;
            var avoid = new List<DemoRect>();
            for (int i = callouts.Count - 1; i >= 0; i--)
            {
                var c = callouts[i];
                c.Alpha = Toward(c.Alpha, c.Goal, dt / (c.Goal > 0 ? DemoDefaults.CalloutIn : 0.28f));
                c.Grow = Toward(c.Grow, c.Goal, dt / 0.55f);
                if (c.Goal == 0 && c.Alpha == 0) { c.Box.RemoveFromHierarchy(); callouts.RemoveAt(i); continue; }
            }
            foreach (var c in callouts)
            {
                var t = c.Target();
                float h = c.Box.layout.height;
                if (t.HasValue && !float.IsNaN(h) && h > 0)
                {
                    c.LastTarget = t.Value;
                    var target = new DemoRect(t.Value.x, t.Value.y, t.Value.width, t.Value.height);
                    // Placed once, then kept: a callout that hops sides as the screen settles reads as a glitch.
                    if (!c.Placed) { c.Place = DemoLayout.Place(target, c.Width, h, new DemoRect(0, 0, screen.width, screen.height), c.Side, avoid); c.Placed = true; c.Side = c.Place.Side; }
                    else c.Place = DemoLayout.Place(target, c.Width, h, new DemoRect(0, 0, screen.width, screen.height), c.Side);
                    avoid.Add(c.Place.Box.Grow(12));
                    float e = DemoDefaults.Ease(c.Alpha);
                    var slide = SlideFrom(c.Place.Side) * (1 - e) * 14;
                    c.Box.style.left = c.Place.Box.X + slide.x;
                    c.Box.style.top = c.Place.Box.Y + slide.y;
                    c.Box.style.opacity = e;
                }
                else c.Box.style.opacity = 0; // laid out first, then shown
            }

            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                var r = ripples[i];
                r.t += dt / 0.55f;
                if (r.t >= 1) ripples.RemoveAt(i); else ripples[i] = r;
            }
            under.MarkDirtyRepaint();
            over.MarkDirtyRepaint();
        }

        private static Vector2 SlideFrom(string side) => side == "left" ? new Vector2(-1, 0) : side == "right" ? new Vector2(1, 0) : side == "above" ? new Vector2(0, -1) : new Vector2(0, 1);
        private static float Toward(float v, float goal, float step) => goal > v ? Mathf.Min(goal, v + step) : Mathf.Max(goal, v - step);
        private static Rect Grow(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + 2 * by, r.height + 2 * by);
        private static Rect LerpRect(Rect a, Rect b, float t) => new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));

        // ---- drawing ----

        /// <summary>Below the callout boxes: the dim, the boxes' shadows, the rings around targets and the leader lines.</summary>
        private void DrawUnder(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            var screen = PanelRect;
            if (dimAlpha > 0 && dimRectSet)
            {
                float a = DemoDefaults.Ease(dimAlpha);
                p.fillColor = new Color(0.03f, 0.02f, 0.01f, 0.64f * a);
                p.BeginPath();
                p.MoveTo(new Vector2(-4, -4)); p.LineTo(new Vector2(screen.width + 4, -4)); p.LineTo(new Vector2(screen.width + 4, screen.height + 4)); p.LineTo(new Vector2(-4, screen.height + 4)); p.ClosePath();
                RoundRect(p, dimRect, 8);
                p.Fill(FillRule.OddEven);
            }
            foreach (var c in callouts)
            {
                if (!c.Placed || c.Box.resolvedStyle.opacity <= 0) continue;
                float a = DemoDefaults.Ease(c.Alpha);
                var b = new Rect(c.Box.resolvedStyle.left, c.Box.resolvedStyle.top, c.Box.layout.width, c.Box.layout.height);
                Shadow(p, b, 7, a);
                // the ring around what it points at
                var ring = Grow(c.LastTarget, 5);
                p.strokeColor = new Color(Gilt.r, Gilt.g, Gilt.b, 0.95f * a);
                p.lineWidth = 2.2f;
                p.BeginPath(); RoundRect(p, ring, 6); p.Stroke();
                p.strokeColor = new Color(Gilt.r, Gilt.g, Gilt.b, 0.22f * a);
                p.lineWidth = 7f;
                p.BeginPath(); RoundRect(p, ring, 6); p.Stroke();
                // the leader line, drawn out from the callout as it appears
                var from = new Vector2(c.Place.FromX, c.Place.FromY) + SlideFrom(c.Place.Side) * (1 - a) * 14;
                var to = new Vector2(c.Place.ToX, c.Place.ToY) + (new Vector2(c.Place.ToX, c.Place.ToY) - from).normalized * -5;
                var end = Vector2.Lerp(from, to, DemoDefaults.Ease(c.Grow));
                p.strokeColor = new Color(0, 0, 0, 0.35f * a); p.lineWidth = 4.5f;
                p.BeginPath(); p.MoveTo(from + new Vector2(0, 2)); p.LineTo(end + new Vector2(0, 2)); p.Stroke();
                p.strokeColor = new Color(Gilt.r, Gilt.g, Gilt.b, a); p.lineWidth = 2f;
                p.BeginPath(); p.MoveTo(from); p.LineTo(end); p.Stroke();
                p.fillColor = new Color(Gilt.r, Gilt.g, Gilt.b, a);
                p.BeginPath(); p.Arc(from, 3.2f, 0, 360); p.Fill();
                if (c.Grow > 0.95f) { p.BeginPath(); p.Arc(end, 4.2f, 0, 360); p.Fill(); }
            }
        }

        /// <summary>Above everything but the fade: the press ripples and the cursor.</summary>
        private void DrawOver(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            foreach (var (at, t) in ripples)
            {
                float e = 1 - Mathf.Pow(1 - t, 3);
                p.strokeColor = new Color(Gilt.r, Gilt.g, Gilt.b, (1 - t) * 0.95f);
                p.lineWidth = 2.6f * (1 - t) + 0.6f;
                p.BeginPath(); p.Arc(at, 6 + 26 * e, 0, 360); p.Stroke();
                p.fillColor = new Color(1, 0.95f, 0.8f, (1 - t) * 0.35f);
                p.BeginPath(); p.Arc(at, 4 + 12 * e, 0, 360); p.Fill();
            }
            if (CursorAlpha <= 0) return;
            float alpha = DemoDefaults.Ease(CursorAlpha);
            float size = 1.18f * (1 - 0.12f * CursorPress);
            // A soft shadow: the arrow, larger and fainter, a few times, offset down and right.
            for (int i = 3; i >= 1; i--)
            {
                p.fillColor = new Color(0, 0, 0, 0.075f * alpha);
                Arrow(p, CursorAt + new Vector2(1.5f + i * 0.9f, 2.5f + i * 1.3f), size * (1 + i * 0.05f));
                p.Fill();
            }
            p.fillColor = new Color(Parchment.r, Parchment.g, Parchment.b, alpha);
            Arrow(p, CursorAt, size); p.Fill();
            p.strokeColor = new Color(Ink.r, Ink.g, Ink.b, alpha); p.lineWidth = 1.5f; p.lineJoin = LineJoin.Round;
            Arrow(p, CursorAt, size); p.Stroke();
        }

        /// <summary>A pointer arrow, its tip at <paramref name="tip"/>, about 22 panel units tall at size 1.</summary>
        private static void Arrow(Painter2D p, Vector2 tip, float s)
        {
            Vector2 P(float x, float y) => tip + new Vector2(x, y) * s;
            p.BeginPath();
            p.MoveTo(P(0, 0));
            p.LineTo(P(0, 18.5f));
            p.LineTo(P(4.4f, 14.6f));
            p.LineTo(P(7.6f, 21.6f));
            p.LineTo(P(10.6f, 20.3f));
            p.LineTo(P(7.5f, 13.4f));
            p.LineTo(P(13.2f, 13.2f));
            p.ClosePath();
        }

        private static void Shadow(Painter2D p, Rect r, float depth, float alpha)
        {
            for (int i = 1; i <= 5; i++)
            {
                p.fillColor = new Color(0, 0, 0, 0.07f * alpha);
                p.BeginPath(); RoundRect(p, new Rect(r.x - i * 1.4f, r.y - i * 1.4f + depth * 0.6f, r.width + i * 2.8f, r.height + i * 2.8f), 4 + i * 1.5f); p.Fill();
            }
        }

        private static void RoundRect(Painter2D p, Rect r, float radius)
        {
            float k = Mathf.Min(radius, r.width / 2, r.height / 2);
            p.MoveTo(new Vector2(r.xMin + k, r.yMin));
            p.LineTo(new Vector2(r.xMax - k, r.yMin)); p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMin + k), k);
            p.LineTo(new Vector2(r.xMax, r.yMax - k)); p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMax - k, r.yMax), k);
            p.LineTo(new Vector2(r.xMin + k, r.yMax)); p.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMax - k), k);
            p.LineTo(new Vector2(r.xMin, r.yMin + k)); p.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMin + k, r.yMin), k);
            p.ClosePath();
        }

        /// <summary>A full-panel layer drawn with Painter2D.</summary>
        private sealed class Painted : VisualElement
        {
            public Painted(Action<MeshGenerationContext> draw)
            {
                pickingMode = PickingMode.Ignore;
                AddToClassList("demo-fill");
                generateVisualContent += draw;
            }
        }
    }
}
