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
    /// The framework view. Collapsed, a strip of one pill per agent with its last outcome, so the
    /// framework is always visible without covering the village. Expanded (Tab), the full board: per
    /// agent what it was asked, what it was offered, what it chose, whether a guard stopped it and
    /// what that cost, above a lane of the requests waiting for the model.
    /// </summary>
    public sealed class AgentBoard : MonoBehaviour
    {
        private VisualElement root, strip, board, cardsColumn, lane, laneItems;
        private Label title, subtitle;
        private readonly List<CardView> views = new List<CardView>();
        private readonly List<Label> pills = new List<Label>();

        /// <summary>The cards as last shown, for tests.</summary>
        public IReadOnlyList<AgentCard> Cards { get; private set; } = new List<AgentCard>();

        /// <summary>Whether the full board is open.</summary>
        public bool Expanded { get; private set; }

        private sealed class CardView
        {
            public VisualElement Box;
            public Label Title, Activity, Stimulus, Offered, Decision, Chip, Guard, Numbers;
        }

        private void OnEnable()
        {
            root = DemoUI.Create(gameObject, 5);

            strip = new VisualElement { name = "AgentStrip", pickingMode = PickingMode.Ignore };
            // On a soft panel, like the key hints, so its heading reads over bright ground.
            strip.AddToClassList("vui-panel");
            strip.AddToClassList("vui-panel--soft");
            strip.style.paddingTop = strip.style.paddingBottom = 6;
            strip.style.paddingLeft = strip.style.paddingRight = 8;
            strip.style.position = Position.Absolute;
            strip.style.left = 14;
            // Below the HUD's clock and purse.
            strip.style.top = 104;
            root.Add(strip);
            var stripHead = DemoUI.Label(strip, "AGENTS   ·   Tab: framework view", "vui-muted");
            stripHead.style.marginBottom = 3;

            board = DemoUI.Panel(root);
            board.name = "FrameworkView";
            board.style.position = Position.Absolute;
            board.style.left = 14;
            board.style.top = 104;
            board.style.bottom = 54;
            board.style.width = 420;
            // Opaque, so no world label shows through it.
            board.style.backgroundColor = new Color(0.1f, 0.08f, 0.06f, 1f);
            board.pickingMode = PickingMode.Ignore;
            title = DemoUI.Label(board, "", "vui-title");
            subtitle = DemoUI.Label(board, "", "vui-subtitle");
            lane = DemoUI.Row(board);
            lane.style.marginTop = 6;
            lane.style.marginBottom = 6;
            DemoUI.Label(lane, "Waiting for the model:", "vui-muted").style.marginRight = 6;
            laneItems = DemoUI.Row(lane);
            laneItems.style.flexWrap = Wrap.Wrap;
            laneItems.style.flexShrink = 1;
            var scroll = new ScrollView(ScrollViewMode.Vertical) { pickingMode = PickingMode.Ignore };
            scroll.style.flexGrow = 1;
            board.Add(scroll);
            cardsColumn = scroll.contentContainer;
            DemoUI.Label(board, "Tab closes   ·   H shows each request in full", "vui-muted").style.marginTop = 4;
            views.Clear();
            pills.Clear();
            Apply();
        }

        private void OnDisable()
        {
            root?.Clear();
            views.Clear();
            pills.Clear();
        }

        /// <summary>Where the board sits over the world, in panel pixels: the framework view when open, the agent list when not.</summary>
        public Rect? Bounds => board == null ? (Rect?)null : Expanded ? board.worldBound : strip.worldBound;

        public void Toggle()
        {
            Expanded = !Expanded;
            Apply();
        }

        private void Apply()
        {
            if (board == null) return;
            DemoUI.Show(board, Expanded);
            DemoUI.Show(strip, !Expanded);
        }

        /// <summary>Updates the board. Cheap to call every frame: elements are made once per card.</summary>
        /// <param name="queue">The requests waiting for the model, oldest first, as "name|priority".</param>
        public void Show(string heading, string detail, IReadOnlyList<AgentCard> cards, IReadOnlyList<string> queue = null)
        {
            Cards = cards;
            if (board == null) return;
            title.text = heading;
            subtitle.text = detail;

            while (pills.Count < cards.Count)
            {
                var pill = DemoUI.Label(strip, "", "vui-chip");
                pill.style.alignSelf = Align.FlexStart;
                pill.style.marginBottom = 3;
                pills.Add(pill);
            }
            for (int i = 0; i < pills.Count; i++)
            {
                bool shown = i < cards.Count;
                DemoUI.Show(pills[i], shown);
                if (!shown) continue;
                var c = cards[i];
                string outcome = c.Thinking ? "thinking" : c.Outcome;
                pills[i].text = c.Name + (string.IsNullOrEmpty(outcome) ? "" : "  ·  " + outcome) + (c.Listening ? "  ·  listening" : "");
                SetOutcome(pills[i], outcome);
            }

            if (!Expanded) return;
            while (views.Count < cards.Count) views.Add(MakeCard());
            for (int i = 0; i < views.Count; i++)
            {
                var view = views[i];
                bool shown = i < cards.Count;
                view.Box.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (shown) Fill(view, cards[i]);
            }

            laneItems.Clear();
            if (queue == null || queue.Count == 0) DemoUI.Label(laneItems, "nothing", "vui-muted");
            else
                foreach (var item in queue)
                {
                    var parts = item.Split('|');
                    var chip = DemoUI.Label(laneItems, parts[0], "vui-chip");
                    chip.style.backgroundColor = parts.Length > 1 && parts[1] == "PlayerFacing"
                        ? new Color(0.27f, 0.51f, 0.78f) : new Color(0.42f, 0.36f, 0.5f);
                }
        }

        private void Fill(CardView view, AgentCard card)
        {
            view.Title.text = card.Name + (string.IsNullOrEmpty(card.Role) ? "" : "  —  " + card.Role) + (card.Listening ? "   (listening)" : "");
            view.Activity.text = card.Activity;
            view.Stimulus.text = card.Stimulus;
            Set(view.Offered, card.Offered);
            Set(view.Decision, card.Decision);
            Set(view.Guard, card.Guard);
            Set(view.Numbers, card.Numbers);
            string outcome = card.Thinking ? "thinking" : card.Outcome;
            view.Chip.text = outcome.ToUpperInvariant();
            view.Chip.style.display = string.IsNullOrEmpty(outcome) ? DisplayStyle.None : DisplayStyle.Flex;
            SetOutcome(view.Chip, outcome);
            view.Box.style.borderLeftColor = ColourOf(outcome);
        }

        private static readonly string[] Outcomes = { "executed", "none", "contained", "refused", "error", "thinking" };

        private static void SetOutcome(VisualElement element, string outcome)
        {
            foreach (var o in Outcomes) element.EnableInClassList("outcome-" + o, o == outcome);
        }

        private CardView MakeCard()
        {
            var box = DemoUI.Panel(cardsColumn, "vui-panel--soft");
            box.pickingMode = PickingMode.Ignore;
            box.style.marginBottom = 6;
            box.style.borderLeftWidth = 4;
            var top = DemoUI.Row(box);
            top.style.justifyContent = Justify.SpaceBetween;
            var view = new CardView { Box = box };
            view.Title = DemoUI.Label(top, "", "vui-title");
            view.Title.style.fontSize = 13;
            view.Title.style.flexShrink = 1;
            view.Title.style.whiteSpace = WhiteSpace.Normal;
            view.Chip = DemoUI.Label(top, "", "vui-chip");
            view.Chip.style.flexShrink = 0;
            view.Chip.style.marginLeft = 6;
            view.Chip.style.alignSelf = Align.FlexStart;
            view.Activity = DemoUI.Label(box, "", "vui-muted");
            view.Stimulus = DemoUI.Label(box, "", "vui-text");
            view.Offered = DemoUI.Label(box, "", "vui-muted");
            view.Decision = DemoUI.Label(box, "", "vui-text");
            view.Decision.style.color = new Color(0.97f, 0.9f, 0.7f);
            view.Guard = DemoUI.Label(box, "", "vui-text");
            view.Guard.style.color = new Color(1f, 0.74f, 0.32f);
            view.Numbers = DemoUI.Label(box, "", "vui-muted");
            view.Numbers.style.fontSize = 10;
            return view;
        }

        private static Color ColourOf(string outcome) =>
            outcome == "executed" ? new Color(0.33f, 0.59f, 0.33f)
            : outcome == "none" ? new Color(0.43f, 0.49f, 0.57f)
            : outcome == "contained" ? new Color(0.84f, 0.59f, 0.17f)
            : outcome == "refused" ? new Color(0.77f, 0.32f, 0.24f)
            : outcome == "error" ? new Color(0.67f, 0.17f, 0.17f)
            : outcome == "thinking" ? new Color(0.27f, 0.51f, 0.78f)
            : new Color(0.5f, 0.45f, 0.38f);

        private static void Set(Label label, string text)
        {
            label.text = text ?? "";
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
