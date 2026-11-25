using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The world's name and backstory at the head of the chronicle (#20, world lore): one line in the feed's own style,
    /// the name and the story's start, that a click opens in place, as a chronicle entry opens.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private VisualElement worldRow;

        /// <summary>Shows the world's name and backstory above the chronicle, or removes them when both are empty.</summary>
        public void SetWorld(string name, string backstory)
        {
            worldRow?.RemoveFromHierarchy();
            worldRow = null;
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(backstory)) return;
            var row = new VisualElement { name = "world-lore" };
            row.AddToClassList("lg"); row.AddToClassList("lg-tale"); row.AddToClassList("world-lore");
            row.tooltip = backstory;
            row.RegisterCallback<ClickEvent>(_ => row.ToggleInClassList("open"));
            var kind = new Icon(IconKind.Scroll, 14) { Tint = new Color32(236, 200, 120, 255) }; kind.AddToClassList("lg-kind"); row.Add(kind);
            var body = new VisualElement(); body.AddToClassList("lg-body");
            var title = new Label(string.IsNullOrWhiteSpace(name) ? "This world" : name); title.AddToClassList("world-lore-name"); body.Add(title);
            if (!string.IsNullOrWhiteSpace(backstory)) { var t = new Label(backstory); t.AddToClassList("lg-text"); body.Add(t); }
            row.Add(body);
            worldRow = row;
            var chronicle = filters.parent;
            chronicle?.Insert(chronicle.IndexOf(filters), row);
        }
    }
}
