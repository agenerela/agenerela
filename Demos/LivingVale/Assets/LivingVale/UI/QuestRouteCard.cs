using LivingVale.Game;
using LivingVale.Interaction;
using LivingVale.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace LivingVale.UI
{
    /// <summary>Shows the current conversation's authored route contract and explicit consent buttons.</summary>
    public sealed class QuestRouteCard : MonoBehaviour
    {
        [SerializeField] private ValeBootstrap bootstrap;

        private Font font;
        private RectTransform card;
        private Text title;
        private Text terms;
        private Button confirm;
        private Button decline;
        private string buttonsProposalId;

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

            card = Box("Route contract", transform, new Vector2(1f, 0f),
                new Vector2(-18f, 488f), new Vector2(560f, 96f), new Color(0.06f, 0.12f, 0.14f, 0.98f));
            title = Label("Title", card, "", new Vector2(16f, -6f), new Vector2(528f, 22f), 18);
            terms = Label("Terms", card, "", new Vector2(16f, -28f), new Vector2(528f, 34f), 14);
            bootstrap.Routes.Changed += Render;
            bootstrap.Coordinator.Changed += OnConversationChanged;
            Render();
        }

        private void OnConversationChanged(ConversationEvent change)
        {
            Render();
        }

        private void Render()
        {
            RouteProposal proposal = bootstrap.Routes.ActiveProposal;
            var coordinator = bootstrap.Coordinator;
            bool visible = proposal != null && coordinator.IsOpen && coordinator.ActiveNpc != null &&
                coordinator.ActiveNpc.Id == proposal.NpcId && coordinator.Generation == proposal.ConversationGeneration;
            card.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            title.text = proposal.Contract.Title;
            terms.text = proposal.Contract.Terms;
            if (buttonsProposalId != proposal.Id)
            {
                BuildButtons(proposal.Id, proposal.Contract.ConfirmLabel);
            }
        }

        private void BuildButtons(string proposalId, string confirmLabel)
        {
            // uGUI clicks on release: a press on the previous proposal must never accept its replacement.
            if (confirm != null)
            {
                confirm.gameObject.SetActive(false);
                Destroy(confirm.gameObject);
                decline.gameObject.SetActive(false);
                Destroy(decline.gameObject);
            }

            buttonsProposalId = proposalId;
            Button accept = null;
            accept = ButtonAt(confirmLabel, card, new Vector2(16f, -64f), new Vector2(330f, 26f), () =>
            {
                bootstrap.BlockWorldInputForFrame();
                accept.interactable = false;
                bootstrap.Routes.Confirm(proposalId);
            });
            confirm = accept;
            decline = ButtonAt("Not now", card, new Vector2(358f, -64f), new Vector2(186f, 26f), () =>
            {
                bootstrap.BlockWorldInputForFrame();
                bootstrap.Routes.Decline(proposalId);
            });
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

        private static RectTransform Box(string objectName, Transform parent, Vector2 anchor,
            Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(objectName, parent, anchor, position, size);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private Text Label(string objectName, Transform parent, string text, Vector2 position,
            Vector2 size, int fontSize)
        {
            RectTransform rect = Rect(objectName, parent, new Vector2(0f, 1f), position, size);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.text = text;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private Button ButtonAt(string caption, Transform parent, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction clicked)
        {
            RectTransform rect = Box(caption, parent, new Vector2(0f, 1f), position, size,
                new Color(0.22f, 0.3f, 0.35f));
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(clicked);
            Text label = Label("Label", rect, caption, Vector2.zero, size, 16);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private void OnDestroy()
        {
            if (bootstrap != null && bootstrap.Routes != null)
            {
                bootstrap.Routes.Changed -= Render;
                bootstrap.Coordinator.Changed -= OnConversationChanged;
            }
        }
    }
}
