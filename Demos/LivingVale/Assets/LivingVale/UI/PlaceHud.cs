using LivingVale.Game;
using LivingVale.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace LivingVale.UI
{
    /// <summary>
    /// The notice area under the compass (G8a; G4b showed only the place name and a discovery line):
    /// the current place's name, then up to three queued notices, oldest on top: "Discovered:
    /// Reedbank +10 XP", quest receipts earned outside a conversation, and the model key's notice
    /// ("loading …"), which updates in place and stays until the model is ready. One area, so they
    /// stack rather than overlap. While a conversation is open the area moves left of the chat column.
    /// Built from code with uGUI like the other HUDs, without raycast targets.
    /// </summary>
    [DefaultExecutionOrder(400)]
    public sealed class PlaceHud : MonoBehaviour
    {
        private const float NoticeSeconds = 4f;
        private const string BetweenPlaces = "Briar Vale";
        private const string ModelKey = "model";

        [SerializeField] private ValeBootstrap bootstrap;

        private readonly NoticeQueue queue = new NoticeQueue();
        private RectTransform area;
        private Text placeName;
        private Text[] lines;
        private ValePlace shown;
        private bool built;
        private bool chatLayout;

        public void Configure(ValeBootstrap owner)
        {
            bootstrap = owner;
        }

        /// <summary>Adds a notice; a keyed one replaces the shown or waiting one with its key.</summary>
        public void Push(string text, string key = null, float seconds = NoticeSeconds)
        {
            queue.Push(text, seconds, Time.unscaledTime, key);
        }

        private void Start()
        {
            Build();
            bootstrap.Discovery.Discovered += OnDiscovered;
            bootstrap.Quests.Completed += OnReceipt;
            bootstrap.Routes.Completed += OnReceipt;
            if (bootstrap.Models != null)
            {
                bootstrap.Models.NoticeChanged += OnModelNotice;
                bootstrap.Models.Finished += OnModelNotice;
                OnModelNotice();
            }
        }

        private void OnDestroy()
        {
            if (bootstrap == null)
            {
                return;
            }

            if (bootstrap.Discovery != null)
            {
                bootstrap.Discovery.Discovered -= OnDiscovered;
            }

            if (bootstrap.Quests != null)
            {
                bootstrap.Quests.Completed -= OnReceipt;
                bootstrap.Routes.Completed -= OnReceipt;
            }

            if (bootstrap.Models != null)
            {
                bootstrap.Models.NoticeChanged -= OnModelNotice;
                bootstrap.Models.Finished -= OnModelNotice;
            }
        }

        private void OnDiscovered(ValePlace place)
        {
            Push($"Discovered: {place.DisplayName}   +{LivingVale.World.PlaceDiscovery.Xp} XP");
        }

        private void OnReceipt(QuestReceipt receipt)
        {
            // The conversation already shows its own receipts.
            if (receipt == null || bootstrap.Coordinator.IsOpen || string.IsNullOrEmpty(receipt.Line))
            {
                return;
            }

            Push(receipt.Line);
        }

        private void OnModelNotice()
        {
            if (this == null || !built)
            {
                return;
            }

            // Sticky while loading or failed; an empty notice (ready) clears it.
            Push(bootstrap.DriverNotice, ModelKey, 0f);
        }

        private void LateUpdate()
        {
            if (!built)
            {
                return;
            }

            ValePlace current = bootstrap.Discovery.Current;
            if (current != shown || placeName.text.Length == 0)
            {
                shown = current;
                placeName.text = current?.DisplayName ?? BetweenPlaces;
            }

            bool chatting = bootstrap.Coordinator.IsOpen;
            if (chatting != chatLayout)
            {
                chatLayout = chatting;
                // The pivot is the area's top centre, so the offset moves its centre.
                area.anchoredPosition = new Vector2(chatting ? HudLayout.NoticeChatOffset : 0f,
                    -HudLayout.NoticeTop);
                area.sizeDelta = new Vector2(chatting ? HudLayout.NoticeChatWidth : HudLayout.NoticeWidth, area.sizeDelta.y);
            }

            float now = Time.unscaledTime;
            queue.Tick(now);
            float y = -26f;
            for (int i = 0; i < lines.Length; i++)
            {
                Text line = lines[i];
                bool on = i < queue.Visible.Count;
                line.enabled = on;
                if (!on)
                {
                    continue;
                }

                Notice notice = queue.Visible[i];
                if (line.text != notice.Text)
                {
                    line.text = notice.Text;
                }

                // A long receipt or model name wraps; the next line moves down under it.
                float height = Mathf.Max(HudLayout.NoticeLine, line.preferredHeight);
                line.rectTransform.anchoredPosition = new Vector2(0f, y);
                line.rectTransform.sizeDelta = new Vector2(0f, height);
                y -= height + 2f;
                Color colour = line.color;
                colour.a = Mathf.Clamp01(notice.Left(now) / 0.6f);
                line.color = colour;
            }
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10; // Under the chat overlay.
            HudLayout.Scale(gameObject);

            area = new GameObject("Notice area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(transform, false);
            area.anchorMin = area.anchorMax = area.pivot = new Vector2(0.5f, 1f);
            area.anchoredPosition = new Vector2(0f, -HudLayout.NoticeTop);
            area.sizeDelta = new Vector2(HudLayout.NoticeWidth, 26f + 3f * HudLayout.NoticeLine);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeName = Label("Place name", font, 0f, 22, new Color(0.95f, 0.93f, 0.85f));
            lines = new Text[queue.MaxVisible];
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = Label($"Notice {i + 1}", font, -26f - i * HudLayout.NoticeLine, 16, new Color(1f, 0.85f, 0.45f));
                lines[i].enabled = false;
            }

            built = true;
        }

        private Text Label(string objectName, Font font, float y, int size, Color colour)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(area, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, size + 6f);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.UpperCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            return text;
        }
    }
}
