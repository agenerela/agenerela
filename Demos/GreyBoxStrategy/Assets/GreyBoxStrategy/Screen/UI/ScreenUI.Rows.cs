using System;
using System.Collections.Generic;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Variant B's panel kit (#20, slice 2): a framed header with crest and portrait, tabs with icons, and rows built from
    /// an icon, a label, a bar or pips, and a value; the sentence that explains a row is its tooltip.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private static readonly Color RowInk = new Color32(214, 196, 158, 255);
        private static readonly Color Good = new Color32(150, 200, 120, 255), Bad = new Color32(226, 110, 90, 255);

        /// <summary>A row of a panel from a model row: icon, label, then a bar or pips if it has them, and its value.</summary>
        private static VisualElement KitRow(ScreenModels.Row r, IconKind icon, Color? tint = null, IconKind pip = IconKind.Fort, Color? fill = null)
        {
            var row = new VisualElement { tooltip = r.Tip }; row.AddToClassList("krow");
            row.Add(new Icon(icon, 15) { Tint = tint ?? RowInk });
            var label = new Label(r.Label); label.AddToClassList("krow-label"); row.Add(label);
            if (r.Max > 0)
            {
                var bar = new VisualElement(); bar.AddToClassList("krow-bar");
                var f = new VisualElement(); f.AddToClassList("krow-fill");
                f.style.width = Length.Percent(100f * Mathf.Clamp01((float)r.Amount / r.Max));
                if (fill is Color c) f.style.backgroundColor = c;
                bar.Add(f); row.Add(bar);
            }
            else if (r.Pips > 0)
            {
                var pips = new VisualElement(); pips.AddToClassList("krow-pips");
                for (int k = 0; k < r.Pips; k++)
                {
                    bool on = k < r.Amount, half = !on && k < r.Amount + r.Half;
                    pips.Add(new Icon(pip, 13) { Tint = on ? (fill ?? (Color)new Color32(226, 194, 122, 255)) : half ? new Color32(160, 124, 70, 255) : new Color32(70, 58, 40, 255) });
                }
                row.Add(pips);
            }
            var v = new Label(r.Value); v.AddToClassList("krow-value");
            if (r.Tone > 0) v.AddToClassList("good"); else if (r.Tone < 0) v.AddToClassList("bad");
            row.Add(v);
            return row;
        }

        /// <summary>The panel's header band: a large crest, a portrait (or its empty frame), a title and a line under it.</summary>
        private static VisualElement KitHeader(CountryLook look, string title, string sub, VisualElement portrait = null)
        {
            var h = new VisualElement(); h.AddToClassList("khead");
            if (look != null) h.Add(new Crest(look, 40));
            if (portrait != null) { portrait.AddToClassList("khead-portrait"); h.Add(portrait); }
            var col = new VisualElement(); col.AddToClassList("khead-text");
            var t = new Label(title); t.AddToClassList("khead-title"); col.Add(t);
            if (!string.IsNullOrEmpty(sub)) { var s = new Label(sub); s.AddToClassList("khead-sub"); col.Add(s); }
            h.Add(col);
            return h;
        }

        // The tab each kind of panel was last left on, so reopening a province opens the tab you used.
        private readonly Dictionary<string, int> lastTab = new Dictionary<string, int>();

        /// <summary>
        /// Tabs with icons over a body: a click shows that tab's contents, built when it is shown. <paramref name="key"/>
        /// remembers the tab per kind of panel.
        /// </summary>
        private VisualElement KitTabs(VisualElement into, string key, params (IconKind icon, string name, Action<VisualElement> build)[] tabs)
        {
            var strip = new VisualElement(); strip.AddToClassList("ktabs");
            var page = new VisualElement(); page.AddToClassList("kpage");
            var buttons = new List<VisualElement>();
            void Show(int i)
            {
                lastTab[key] = i;
                for (int k = 0; k < buttons.Count; k++) buttons[k].EnableInClassList("on", k == i);
                page.Clear();
                tabs[i].build(page);
            }
            for (int i = 0; i < tabs.Length; i++)
            {
                int k = i;
                var b = new VisualElement { tooltip = tabs[i].name }; b.AddToClassList("ktab");
                b.Add(new Icon(tabs[i].icon, 13)); b.Add(new Label(tabs[i].name));
                b.RegisterCallback<ClickEvent>(_ => Show(k));
                b.userData = (Action)(() => Show(k)); // what a click runs, for the CLI and tests
                buttons.Add(b); strip.Add(b);
            }
            into.Add(strip); into.Add(page);
            Show(lastTab.TryGetValue(key, out int last) && last < tabs.Length ? last : 0);
            return page;
        }

        /// <summary>For tests and the CLI: the tab names of the panel open on the left, and the one shown.</summary>
        public (List<string> tabs, string on) SideTabs()
        {
            var names = new List<string>(); string on = null;
            side.Query(className: "ktab").ForEach(t => { var l = t.Q<Label>(); names.Add(l?.text); if (t.ClassListContains("on")) on = l?.text; });
            return (names, on);
        }

        /// <summary>For the CLI: shows a tab of the panel open on the left, by name.</summary>
        public bool ShowSideTab(string name)
        {
            VisualElement hit = null;
            side.Query(className: "ktab").ForEach(t => { if (t.Q<Label>()?.text == name) hit = t; });
            if (!(hit?.userData is Action show)) return false;
            show();
            return true;
        }
    }
}
