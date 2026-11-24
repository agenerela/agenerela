using System;
using System.Collections.Generic;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>The screen's UI elements as <see cref="DemoSelector"/> reads them.</summary>
    public sealed class DemoTree : IDemoTree<VisualElement>
    {
        /// <summary>Tests replace it: elements built outside a panel have no layout.</summary>
        public Func<VisualElement, bool> VisibleTest;

        public static readonly DemoTree Shown = new DemoTree();

        public string Name(VisualElement n) => string.IsNullOrEmpty(n.name) ? null : n.name;
        public bool HasClass(VisualElement n, string cls) => n.ClassListContains(cls);
        public string Text(VisualElement n) => (n as TextElement)?.text;
        public IEnumerable<VisualElement> Children(VisualElement n) => n.hierarchy.Children();

        public bool Visible(VisualElement n)
        {
            if (VisibleTest != null) return VisibleTest(n);
            if (n.name == "demo-overlay") return false; // never the demo's own drawing
            var s = n.resolvedStyle;
            if (s.display == DisplayStyle.None || s.visibility == Visibility.Hidden || s.opacity < 0.02f) return false;
            var r = n.layout;
            return !float.IsNaN(r.width) && !float.IsNaN(r.height) && (r.width > 0.5f && r.height > 0.5f || n.childCount > 0);
        }

        /// <summary>The first shown match of a selector under the panel's root, or null.</summary>
        public static VisualElement Find(VisualElement root, string selector) => DemoSelector.Parse(selector).First(root, Shown);

        /// <summary>Where an element is on the panel, clipped to the scroll view it sits in; null when it is scrolled out of sight.</summary>
        public static Rect? OnPanel(VisualElement e)
        {
            if (e?.panel == null) return null;
            var r = e.worldBound;
            if (float.IsNaN(r.width) || r.width <= 0) return null;
            for (var p = e.parent; p != null; p = p.parent)
            {
                if (!(p is ScrollView v)) continue;
                var vp = v.contentViewport.worldBound;
                float x0 = Mathf.Max(r.xMin, vp.xMin), y0 = Mathf.Max(r.yMin, vp.yMin), x1 = Mathf.Min(r.xMax, vp.xMax), y1 = Mathf.Min(r.yMax, vp.yMax);
                if (x1 - x0 < 2 || y1 - y0 < 2) return null;
                r = Rect.MinMaxRect(x0, y0, x1, y1);
            }
            return r;
        }

        /// <summary>Scrolls every scroll view an element sits in so it is in sight; true when one moved.</summary>
        public static bool BringIntoView(VisualElement e)
        {
            bool moved = false;
            for (var p = e?.parent; p != null; p = p.parent)
            {
                if (!(p is ScrollView v)) continue;
                var vp = v.contentViewport.worldBound; var r = e.worldBound;
                if (r.yMin >= vp.yMin && r.yMax <= vp.yMax) continue;
                v.ScrollTo(e);
                moved = true;
            }
            return moved;
        }

        /// <summary>
        /// Does what a click on the element does: an inspector tab shows its page, a button presses, anything else gets
        /// the click event a pointer would send, which bubbles to the row that listens.
        /// </summary>
        public static void Click(VisualElement e)
        {
            if (e.userData is Action show) { show(); return; }
            if (e is Button)
            {
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = e; e.SendEvent(submit); }
                return;
            }
            using (var click = ClickEvent.GetPooled()) { click.target = e; e.SendEvent(click); }
        }
    }
}
