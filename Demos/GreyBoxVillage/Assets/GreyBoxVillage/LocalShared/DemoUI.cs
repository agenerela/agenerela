using UnityEngine;
using UnityEngine.UIElements;

namespace Demos.Shared
{
    /// <summary>
    /// Local UI Toolkit helpers. Every document shares one panel and one theme
    /// (<c>Resources/VillageUI.uss</c>): dark wood panels, a gold edge, parchment text.
    /// </summary>
    public static class DemoUI
    {
        /// <summary>A document on <paramref name="owner"/> with the shared panel and theme, drawn above lower <paramref name="order"/>s.</summary>
        public static VisualElement Create(GameObject owner, int order = 0)
        {
            var document = owner.GetComponent<UIDocument>();
            if (document == null) document = owner.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("VillagePanelSettings");
            document.sortingOrder = order;
            var root = document.rootVisualElement;
            root.Clear();
            var theme = Resources.Load<StyleSheet>("VillageUI");
            if (theme != null && !root.styleSheets.Contains(theme)) root.styleSheets.Add(theme);
            root.focusable = true;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            return root;
        }

        public static VisualElement Panel(VisualElement parent, params string[] classes)
        {
            var box = new VisualElement();
            box.AddToClassList("vui-panel");
            foreach (var c in classes) box.AddToClassList(c);
            parent.Add(box);
            return box;
        }

        /// <summary>An absolutely placed panel, kept for callers that place their own.</summary>
        public static VisualElement Box(VisualElement parent)
        {
            var box = Panel(parent);
            box.style.position = Position.Absolute;
            return box;
        }

        public static Label Label(VisualElement parent, string text, string cls = "vui-text")
        {
            var label = new Label(text);
            label.AddToClassList(cls);
            label.pickingMode = PickingMode.Ignore;
            parent.Add(label);
            return label;
        }

        public static Button Button(VisualElement parent, string text, System.Action onClick, string name = null)
        {
            var button = new Button(onClick) { text = text };
            if (name != null) button.name = name;
            button.AddToClassList("vui-button");
            parent.Add(button);
            return button;
        }

        public static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.AddToClassList("vui-row");
            row.pickingMode = PickingMode.Ignore;
            parent.Add(row);
            return row;
        }

        /// <summary>A key cap and what it does, as in "[E] Talk to Bram".</summary>
        public static VisualElement KeyHint(VisualElement parent, string key, string what)
        {
            var row = Row(parent);
            row.style.marginRight = 12;
            Label(row, key, "vui-key");
            Label(row, what, "vui-muted");
            return row;
        }

        public static void Show(VisualElement element, bool shown) =>
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
