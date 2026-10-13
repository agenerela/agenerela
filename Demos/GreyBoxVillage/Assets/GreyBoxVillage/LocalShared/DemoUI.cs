using UnityEngine;
using UnityEngine.UIElements;

namespace Demos.Shared
{
    /// <summary>Local UI Toolkit helpers; uses the project's Input System at runtime.</summary>
    public static class DemoUI
    {
        public static VisualElement Create(GameObject owner)
        {
            var document = owner.GetComponent<UIDocument>();
            if (document == null) document = owner.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("VillagePanelSettings");
            var root = document.rootVisualElement;
            root.Clear();
            root.focusable = true;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            return root;
        }

        public static VisualElement Box(VisualElement parent)
        {
            var box = new VisualElement();
            box.style.position = Position.Absolute;
            box.style.backgroundColor = new Color(0.06f, 0.08f, 0.09f, 0.94f);
            box.style.paddingLeft = box.style.paddingRight = 12;
            box.style.paddingTop = box.style.paddingBottom = 10;
            parent.Add(box);
            return box;
        }

        public static Label Label(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.color = Color.white;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 3;
            parent.Add(label);
            return label;
        }
    }
}
