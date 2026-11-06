using LivingVale.Game;
using LivingVale.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace LivingVale.UI
{
    /// <summary>
    /// The current place's name at the top centre, and a short "Discovered: Reedbank" notice with its XP
    /// when <see cref="LivingVale.World.PlaceDiscovery"/> records a first visit. Built from code with
    /// uGUI like the other HUDs, without raycast targets, so it never takes a click from the chat.
    /// </summary>
    [DefaultExecutionOrder(400)]
    public sealed class PlaceHud : MonoBehaviour
    {
        private const float NoticeSeconds = 4f;
        private const string BetweenPlaces = "Briar Vale";

        [SerializeField] private ValeBootstrap bootstrap;

        private Text placeName;
        private Text notice;
        private float noticeUntil;
        private ValePlace shown;
        private bool built;

        public void Configure(ValeBootstrap owner)
        {
            bootstrap = owner;
        }

        private void Start()
        {
            Build();
            bootstrap.Discovery.Discovered += OnDiscovered;
        }

        private void OnDestroy()
        {
            if (bootstrap != null && bootstrap.Discovery != null)
            {
                bootstrap.Discovery.Discovered -= OnDiscovered;
            }
        }

        private void OnDiscovered(ValePlace place)
        {
            notice.text = $"Discovered: {place.DisplayName}   +{LivingVale.World.PlaceDiscovery.Xp} XP";
            noticeUntil = Time.unscaledTime + NoticeSeconds;
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

            float left = noticeUntil - Time.unscaledTime;
            notice.enabled = left > 0f;
            if (notice.enabled)
            {
                Color colour = notice.color;
                colour.a = Mathf.Clamp01(left / 0.6f);
                notice.color = colour;
            }
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10; // Under the chat overlay.
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeName = Label("Place name", font, new Vector2(0f, -14f), 22, new Color(0.95f, 0.93f, 0.85f));
            notice = Label("Discovery notice", font, new Vector2(0f, -44f), 18, new Color(1f, 0.85f, 0.45f));
            notice.enabled = false;
            built = true;
        }

        private Text Label(string objectName, Font font, Vector2 position, int size, Color colour)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(520f, 30f);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            text.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            return text;
        }
    }
}
