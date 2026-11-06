using LivingVale.Game;
using LivingVale.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace LivingVale.UI
{
    /// <summary>Keeps route progress visible, with the guide's next goal under it (G8a). Receipts go to the notice area.</summary>
    public sealed class QuestJournalHud : MonoBehaviour
    {
        [SerializeField] private ValeBootstrap bootstrap;

        private Font font;
        private RectTransform panel;
        private Text journal;
        private Text goalLine;

        public void Configure(ValeBootstrap owner)
        {
            bootstrap = owner;
        }

        private void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // G8a: the right column's top slot (HudLayout). Hidden while chatting, when the quest and
            // trade cards use the right-hand side; receipts go to the notice area under the compass.
            panel = Rect("Quest journal", transform, new Vector2(1f, 1f),
                new Vector2(-14f, -HudLayout.JournalTop), new Vector2(HudLayout.JournalWidth, 120f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.04f, 0.06f, 0.08f, 0.84f);
            background.raycastTarget = false;
            journal = Label("Journal", panel, new Vector2(10f, -8f), new Vector2(HudLayout.JournalWidth - 20f, 104f), Color.white);
            goalLine = Label("Next goal", panel, new Vector2(10f, -8f), new Vector2(HudLayout.JournalWidth - 20f, 40f),
                new Color(1f, 0.82f, 0.3f));

            bootstrap.Quests.Changed += Render;
            bootstrap.Routes.Changed += Render;
            bootstrap.Inventory.Changed += Render;
            Render();
        }

        private void Render()
        {
            journal.text = string.Join("\n", bootstrap.Routes.JournalLines());
            QuestGoal goal = QuestGuide.For(bootstrap.Routes);
            goalLine.text = goal != null ? "Next: " + goal.Instruction + "." : "";
            // Wrapped leads and delivery choices can add lines; grow enough to keep them readable.
            float textWidth = HudLayout.JournalWidth - 20f;
            float journalHeight = journal.preferredHeight;
            float goalHeight = goal != null ? goalLine.preferredHeight + 4f : 0f;
            float height = Mathf.Max(120f, journalHeight + goalHeight + 16f);
            panel.sizeDelta = new Vector2(HudLayout.JournalWidth, height);
            journal.rectTransform.sizeDelta = new Vector2(textWidth, journalHeight + 2f);
            goalLine.rectTransform.anchoredPosition = new Vector2(10f, -12f - journalHeight);
            goalLine.rectTransform.sizeDelta = new Vector2(textWidth, goalHeight);
        }

        private void LateUpdate()
        {
            panel.gameObject.SetActive(!bootstrap.Coordinator.IsOpen);
        }

        private static RectTransform Rect(string objectName, Transform parent, Vector2 anchor,
            Vector2 position, Vector2 size)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Text Label(string objectName, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(objectName, parent, new Vector2(0f, 1f), position, size);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 15;
            label.color = color;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private void OnDestroy()
        {
            if (bootstrap != null && bootstrap.Routes != null)
            {
                bootstrap.Quests.Changed -= Render;
                bootstrap.Routes.Changed -= Render;
                bootstrap.Inventory.Changed -= Render;
            }
        }
    }
}
