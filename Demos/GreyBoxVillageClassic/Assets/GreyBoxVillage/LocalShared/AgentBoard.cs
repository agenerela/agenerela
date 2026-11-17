using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Demos.Shared
{
    /// <summary>
    /// One agent as the board shows it. Strings only, filled by the game each frame: the shared
    /// helpers never reference the framework, so #14 can move them as they are.
    /// </summary>
    public sealed class AgentCard
    {
        public string Name = "", Role = "", Activity = "";
        public string Stimulus = "", Offered = "", Decision = "", Guard = "", Numbers = "";
        /// <summary>executed, none, contained, refused, error or thinking; empty before the first decision.</summary>
        public string Outcome = "";
        public bool Thinking, Listening;
    }

    /// <summary>
    /// The always-on panel that makes the framework visible: one card per agent with what it was
    /// asked, what it was offered, what it chose, whether a guard stopped it, and what that cost.
    /// UI Toolkit, on a GameObject of its own, because <see cref="DemoUI.Create"/> owns its document.
    /// </summary>
    public sealed class AgentBoard : MonoBehaviour
    {
        private VisualElement root, column;
        private Label title, subtitle;
        private readonly List<CardView> views = new List<CardView>();

        /// <summary>The cards as last shown, for tests.</summary>
        public IReadOnlyList<AgentCard> Cards { get; private set; } = new List<AgentCard>();

        private sealed class CardView
        {
            public VisualElement Box;
            public Label Title, Activity, Stimulus, Offered, Decision, Chip, Guard, Numbers;
        }

        private void OnEnable()
        {
            root = DemoUI.Create(gameObject);
            column = new VisualElement();
            column.style.position = Position.Absolute;
            column.style.left = 16;
            column.style.top = 16;
            column.style.width = 430;
            column.pickingMode = PickingMode.Ignore;
            root.Add(column);

            var header = Panel(column);
            title = Text(header, "", 17, Color.white);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            subtitle = Text(header, "", 11, Muted);
            Text(header, "Blue: you   ·   Gold: Bram   ·   Silver: Wren   ·   Green: villagers   ·   Grey: wolf   ·   Red: off limits", 11, Muted);
            views.Clear();
        }

        private void OnDisable()
        {
            root?.Clear();
            views.Clear();
        }

        /// <summary>Updates the board. Cheap to call every frame: elements are made once per card.</summary>
        public void Show(string heading, string detail, IReadOnlyList<AgentCard> cards)
        {
            Cards = cards;
            if (column == null) return;
            title.text = heading;
            subtitle.text = detail;
            while (views.Count < cards.Count) views.Add(MakeCard());
            for (int i = 0; i < views.Count; i++)
            {
                var view = views[i];
                bool shown = i < cards.Count;
                view.Box.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (shown) Fill(view, cards[i]);
            }
        }

        private void Fill(CardView view, AgentCard card)
        {
            view.Title.text = card.Name + (string.IsNullOrEmpty(card.Role) ? "" : "  —  " + card.Role) + (card.Listening ? "   ◂ listening" : "");
            view.Activity.text = card.Activity;
            view.Stimulus.text = card.Stimulus;
            Set(view.Offered, card.Offered);
            Set(view.Decision, card.Decision);
            Set(view.Guard, card.Guard);
            Set(view.Numbers, card.Numbers);
            string outcome = card.Thinking ? "thinking" : card.Outcome;
            view.Chip.text = outcome.ToUpperInvariant();
            view.Chip.style.display = string.IsNullOrEmpty(outcome) ? DisplayStyle.None : DisplayStyle.Flex;
            var colour = ColourOf(outcome);
            view.Chip.style.backgroundColor = colour;
            view.Box.style.borderLeftColor = colour;
        }

        private CardView MakeCard()
        {
            var box = Panel(column);
            box.style.borderLeftWidth = 4;
            box.style.borderLeftColor = Muted;
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            box.Add(top);
            var view = new CardView { Box = box };
            view.Title = Text(top, "", 14, Color.white);
            view.Title.style.unityFontStyleAndWeight = FontStyle.Bold;
            view.Title.style.flexShrink = 1;
            view.Chip = Text(top, "", 10, new Color(0.08f, 0.09f, 0.09f));
            view.Chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            view.Chip.style.paddingLeft = view.Chip.style.paddingRight = 6;
            view.Chip.style.paddingTop = view.Chip.style.paddingBottom = 2;
            view.Chip.style.borderTopLeftRadius = view.Chip.style.borderTopRightRadius =
                view.Chip.style.borderBottomLeftRadius = view.Chip.style.borderBottomRightRadius = 8;
            view.Chip.style.alignSelf = Align.FlexStart;
            view.Activity = Text(box, "", 11, Muted);
            view.Stimulus = Text(box, "", 12, new Color(0.85f, 0.88f, 0.9f));
            view.Offered = Text(box, "", 11, Muted);
            view.Decision = Text(box, "", 13, new Color(0.96f, 0.93f, 0.82f));
            view.Guard = Text(box, "", 11, Amber);
            view.Numbers = Text(box, "", 10, Muted);
            return view;
        }

        private static readonly Color Muted = new Color(0.62f, 0.68f, 0.7f);
        private static readonly Color Amber = new Color(1f, 0.74f, 0.32f);

        private static Color ColourOf(string outcome) =>
            outcome == "executed" ? new Color(0.55f, 0.85f, 0.5f)
            : outcome == "none" ? new Color(0.7f, 0.74f, 0.76f)
            : outcome == "contained" ? Amber
            : outcome == "refused" || outcome == "error" ? new Color(0.95f, 0.45f, 0.4f)
            : outcome == "thinking" ? new Color(0.55f, 0.75f, 0.95f)
            : Muted;

        private static void Set(Label label, string text)
        {
            label.text = text ?? "";
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static VisualElement Panel(VisualElement parent)
        {
            var box = new VisualElement();
            box.pickingMode = PickingMode.Ignore;
            box.style.backgroundColor = new Color(0.06f, 0.08f, 0.09f, 0.9f);
            box.style.paddingLeft = box.style.paddingRight = 10;
            box.style.paddingTop = box.style.paddingBottom = 7;
            box.style.marginBottom = 6;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius =
                box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 4;
            parent.Add(box);
            return box;
        }

        private static Label Text(VisualElement parent, string text, int size, Color colour)
        {
            var label = DemoUI.Label(parent, text);
            label.pickingMode = PickingMode.Ignore;
            label.style.fontSize = size;
            label.style.color = colour;
            label.style.marginBottom = 1;
            return label;
        }
    }
}
