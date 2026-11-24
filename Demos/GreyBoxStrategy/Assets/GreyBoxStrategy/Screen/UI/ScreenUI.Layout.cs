using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The layout rules no stylesheet line can say, so nothing on the screen is ever half shown at the window's
    /// bottom: a window over the map (the inspector, an event, a battle report, diplomacy, an offer) is never
    /// taller than the room below its own top edge, and its body scrolls inside it; and a list that scrolls
    /// fades at its last pixels while more lies below, so a row cut by the list's edge reads as "there is more",
    /// beside the scrollbar. The rest of the rule is in PaintedRealm.uss, under "Nothing is cut off at the bottom".
    /// </summary>
    public sealed partial class ScreenUI
    {
        /// <summary>The gap every panel and window keeps to the window's bottom edge, in layout units.</summary>
        public const float BottomMargin = 8;
        /// <summary>A window is never squeezed below this: its head and a few lines; below it the window's own body scrolls.</summary>
        public const float WindowFloor = 160;

        /// <summary>
        /// The tallest a window over the map may be: the screen's height below the window's top edge, less the
        /// bottom margin. USS has no "100% minus": a percentage of the screen counts from the screen's top, so a
        /// window that starts lower runs off the bottom of a small screen.
        /// </summary>
        public static float WindowMaxHeight(float screenHeight, float top) => Mathf.Max(WindowFloor, screenHeight - top - BottomMargin);

        /// <summary>Whether a scrolling list has rows below what it shows: its content, its viewport and how far it is scrolled.</summary>
        public static bool MoreBelow(float content, float viewport, float offset) => content - viewport - offset > 1f;

        /// <summary>
        /// Marks an element as a window over the map and keeps it inside the screen from then on: its tallest is set
        /// when it is laid out, and again whenever the screen changes size. Returns the element.
        /// </summary>
        public VisualElement Window(VisualElement e)
        {
            e.AddToClassList("window");
            e.RegisterCallback<GeometryChangedEvent>(_ => FitWindow(e));
            if (!fitsOnResize) { fitsOnResize = true; Root.RegisterCallback<GeometryChangedEvent>(_ => FitWindows()); }
            FitWindow(e);
            return e;
        }

        private bool fitsOnResize;

        private void FitWindows() => Root.Query(className: "window").ForEach(FitWindow);

        private void FitWindow(VisualElement e)
        {
            float screen = Root.layout.height, top = e.layout.y;
            if (float.IsNaN(screen) || float.IsNaN(top) || screen <= 0) return;
            float max = WindowMaxHeight(screen, top);
            // Set only when it changes: a style written from a geometry event lays the window out again.
            if (e.style.maxHeight.keyword == StyleKeyword.Null && Mathf.Abs(e.style.maxHeight.value.value - max) < 0.5f) return;
            e.style.maxHeight = max;
        }

        /// <summary>How many lines of a model's free text a panel shows before "more".</summary>
        public const int FoldLines = 3;

        /// <summary>
        /// The height that shows a text's first <paramref name="lines"/> lines whole, or null when the whole text is no
        /// taller than that and a "more" would only hide half a line.
        /// </summary>
        public static float? FoldHeight(float textHeight, float lineHeight, int lines = FoldLines)
        {
            if (lineHeight <= 0 || float.IsNaN(textHeight)) return null;
            float keep = lineHeight * lines;
            return textHeight > keep + lineHeight * 0.5f ? keep : (float?)null;
        }

        // The free texts opened with "more", by their text, so a council redrawn on every click keeps them open.
        private readonly HashSet<string> unfoldedTexts = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// A model's free text in a panel: its first lines whole, then a "more" link that opens the rest in place and
        /// "less" that folds it again. It never cuts a line in half and never pushes the panel's controls out of
        /// sight. Returns the box to add in the label's place.
        /// </summary>
        public VisualElement Fold(Label text, int lines = FoldLines)
        {
            var box = new VisualElement(); box.AddToClassList("fold");
            text.style.whiteSpace = WhiteSpace.Normal;
            var clip = new VisualElement(); clip.AddToClassList("fold-clip"); clip.Add(text); box.Add(clip);
            var more = new Label("more ›"); more.AddToClassList("list-more"); more.AddToClassList("fold-more"); more.style.display = DisplayStyle.None;
            box.Add(more);
            string key = text.text;
            void Apply()
            {
                float line = text.MeasureTextSize("Ag", 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).y;
                float? h = FoldHeight(text.layout.height, line, lines);
                bool open = unfoldedTexts.Contains(key);
                more.style.display = h == null ? DisplayStyle.None : DisplayStyle.Flex;
                more.text = open ? "less ‹" : "more ›";
                if (h == null || open) clip.style.maxHeight = StyleKeyword.None;
                else clip.style.maxHeight = h.Value + text.resolvedStyle.paddingTop + text.resolvedStyle.paddingBottom;
            }
            text.RegisterCallback<GeometryChangedEvent>(_ => Apply());
            more.RegisterCallback<ClickEvent>(e => { if (!unfoldedTexts.Remove(key)) unfoldedTexts.Add(key); Apply(); e.StopPropagation(); });
            return box;
        }

        // The windows over the map, by their stylesheet class: whichever part of the screen builds one, it is fitted.
        private static readonly string[] WindowClasses = { "inspector", "event-card", "diplomacy", "offer" };

        /// <summary>
        /// Applies both rules to whatever is on the screen now: every window over the map is fitted, every scrolling
        /// list has its shade. The screen runs it a few times a second, and at once when it opens a window, so a panel
        /// built anywhere in the screen's code is covered without having to remember to ask.
        /// </summary>
        public void KeepLayout()
        {
            foreach (string cls in WindowClasses)
                Root.Query(className: cls).ForEach(w => { if (!w.ClassListContains("window")) Window(w); });
            ShadeScrolls();
        }

        /// <summary>Every scrolling list gets its shade, once each.</summary>
        private void ShadeScrolls()
        {
            Root.Query<ScrollView>().ForEach(v =>
            {
                if (v.ClassListContains("shaded")) return;
                v.AddToClassList("shaded");
                v.contentViewport.Add(new ScrollShade(v));
            });
        }

        /// <summary>
        /// The fade over a scrolling list's last pixels while rows lie below them; gone at the list's end, where
        /// the last row stands whole above the list's bottom padding.
        /// </summary>
        private sealed class ScrollShade : VisualElement
        {
            private const float Tall = 22;
            private static readonly Color32 Ink = new Color32(20, 15, 10, 235), Clear = new Color32(20, 15, 10, 0);
            private readonly ScrollView view;

            public ScrollShade(ScrollView view)
            {
                this.view = view;
                pickingMode = PickingMode.Ignore;
                AddToClassList("scroll-shade");
                style.position = Position.Absolute; style.left = 0; style.right = 0; style.bottom = 0; style.height = Tall;
                generateVisualContent += Draw;
                view.verticalScroller.valueChanged += _ => Refresh();
                view.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
                view.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
                Refresh();
            }

            private void Refresh()
            {
                bool more = MoreBelow(view.contentContainer.layout.height, view.contentViewport.layout.height, view.scrollOffset.y);
                style.display = more ? DisplayStyle.Flex : DisplayStyle.None;
                // The content may have grown under it: the shade stays the viewport's last child, over the rows.
                if (more && parent != null && parent.IndexOf(this) != parent.childCount - 1) BringToFront();
            }

            private void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                if (r.width < 1 || r.height < 1) return;
                var mesh = ctx.Allocate(4, 6);
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMin, Vertex.nearZ), tint = Clear });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMin, Vertex.nearZ), tint = Clear });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMax, Vertex.nearZ), tint = Ink });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMax, Vertex.nearZ), tint = Ink });
                mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
                mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);
            }
        }
    }
}
