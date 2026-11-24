using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// A card on a dark veil while a generated world is made and drawn: what is happening and a bar.
    /// Its own UI document, so it shows before the screen's is built, and goes when the world is ready.
    /// </summary>
    public sealed class LoadingCard
    {
        private GameObject host;
        private Label line;
        private VisualElement fill;

        public static LoadingCard Show(ThemeStyleSheet theme, StyleSheet style, string title)
        {
            var card = new LoadingCard { host = new GameObject("Loading") };
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = UiScale.For(Screen.width, Screen.height);
            panel.sortingOrder = 100;
            var doc = card.host.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var root = doc.rootVisualElement;
            if (style != null) root.styleSheets.Add(style);
            ReplayDirector.ApplyFonts(root);
            var veil = new VisualElement(); veil.AddToClassList("menu-veil"); veil.style.backgroundColor = new Color(0.04f, 0.03f, 0.02f, 0.92f);
            var box = new VisualElement(); box.AddToClassList("panel"); box.AddToClassList("loading-card");
            var t = new Label(title); t.AddToClassList("menu-head"); box.Add(t);
            card.line = new Label("..."); card.line.AddToClassList("menu-sub"); box.Add(card.line);
            var bar = new VisualElement(); bar.AddToClassList("loading-bar");
            card.fill = new VisualElement(); card.fill.AddToClassList("loading-fill"); bar.Add(card.fill);
            box.Add(bar);
            veil.Add(box);
            root.Add(veil);
            return card;
        }

        public void Set(string text, float progress)
        {
            if (line == null) return;
            line.text = text;
            fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100);
        }

        public void Close()
        {
            if (host != null) Object.Destroy(host);
            host = null; line = null;
        }
    }
}
