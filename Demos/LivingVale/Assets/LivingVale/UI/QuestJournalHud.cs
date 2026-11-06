using LivingVale.Game;
using LivingVale.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace LivingVale.UI
{
    /// <summary>Keeps route progress visible and briefly shows quest receipts earned outside conversation.</summary>
    public sealed class QuestJournalHud : MonoBehaviour
    {
        [SerializeField] private ValeBootstrap bootstrap;

        private Font font;
        private RectTransform panel;
        private Text journal;
        private Text toast;
        private float toastUntil;

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

            // Below the driver notice (top, x 604–1024), and hidden while chatting, when the quest
            // and trade cards use the right-hand side and receipts are written into the chat.
            panel = Rect("Quest journal", transform, new Vector2(1f, 1f),
                new Vector2(-14f, -64f), new Vector2(420f, 120f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.04f, 0.06f, 0.08f, 0.84f);
            background.raycastTarget = false;
            journal = Label("Journal", panel, new Vector2(10f, -8f), new Vector2(400f, 104f), Color.white);
            toast = Label("Quest receipt", transform, new Vector2(-14f, -142f),
                new Vector2(420f, 60f), new Color(1f, 0.86f, 0.35f));
            toast.rectTransform.anchorMin = Vector2.one;
            toast.rectTransform.anchorMax = Vector2.one;
            toast.rectTransform.pivot = Vector2.one;
            toast.gameObject.SetActive(false);

            bootstrap.Quests.Changed += Render;
            bootstrap.Routes.Changed += Render;
            bootstrap.Inventory.Changed += Render;
            bootstrap.Quests.Completed += OnCompleted;
            bootstrap.Routes.Completed += OnCompleted;
            Render();
        }

        private void Render()
        {
            journal.text = string.Join("\n", bootstrap.Routes.JournalLines());
            // Wrapped leads and delivery choices can add lines; grow enough to keep them readable.
            float height = Mathf.Max(120f, journal.preferredHeight + 16f);
            panel.sizeDelta = new Vector2(420f, height);
            journal.rectTransform.sizeDelta = new Vector2(400f, height - 16f);
            toast.rectTransform.anchoredPosition = new Vector2(-14f, -72f - height);
        }

        private void OnCompleted(QuestReceipt receipt)
        {
            // Conversation already has the receipt; do not replay it after the player closes chat.
            toast.text = bootstrap.Coordinator.IsOpen ? "" : receipt.Line;
            toastUntil = Time.unscaledTime + 4f;
            RenderToast();
        }

        private void LateUpdate()
        {
            panel.gameObject.SetActive(!bootstrap.Coordinator.IsOpen);
            RenderToast();
        }

        private void RenderToast()
        {
            toast.gameObject.SetActive(!bootstrap.Coordinator.IsOpen && Time.unscaledTime < toastUntil &&
                !string.IsNullOrEmpty(toast.text));
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
                bootstrap.Quests.Completed -= OnCompleted;
                bootstrap.Routes.Completed -= OnCompleted;
            }
        }
    }
}
