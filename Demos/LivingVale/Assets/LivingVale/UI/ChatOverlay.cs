using LivingVale.Game;
using LivingVale.Interaction;
using LivingVale.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LivingVale.UI
{
    public sealed class ChatOverlay : MonoBehaviour
    {
        private ValeBootstrap bootstrap;
        private Font font;
        private GameObject panel;
        private Text header;
        private Text badge;
        private Text prompt;
        private GameObject waiting;
        private InputField input;
        private Button send;
        private ScrollRect scroll;
        private RectTransform history;
        private GameObject tradeCard;
        private Text tradeSummary;
        private Text tradeGold;
        private Text tradeWarning;
        private Button tradeConfirm;
        private Button tradeDecline;
        private string confirmedQuoteId;
        private RectTransform tradeOffer;
        private string buttonsQuoteId;
        private string displayedNpcId;
        private bool scrollToBottom;
        private bool focusRequested;

        public bool InputHasFocus => input != null && input.gameObject.activeInHierarchy &&
            (input.isFocused || (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == input.gameObject));

        public void Initialize(ValeBootstrap owner)
        {
            bootstrap = owner;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildInterface();
            bootstrap.Coordinator.Changed += OnChanged;
            bootstrap.Trade.QuotesChanged += RenderTradeCard;
            bootstrap.Inventory.Changed += RenderTradeCard;
            Render();
        }

        public void SetFocusPrompt(string text)
        {
            prompt.text = text;
        }

        private void BuildInterface()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                var events = new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            RectTransform help = Box("Controls", transform, new Vector2(0f, 1f),
                new Vector2(14f, -14f), new Vector2(580f, 102f), new Color(0.04f, 0.06f, 0.08f, 0.88f));
            Label("Help", help,
                "WASD move · Shift sprint · Mouse orbit · E talk · Esc close · I inventory\n" +
                "Left mouse attack · Right mouse block · Space dodge · F6 model/scripted\n" +
                "Scripted: F1 Normal · F2 Slow · F3 NeverAnswers · F4 Malformed · F5 Throws\n" +
                "Typing captures gameplay keys; Return to world releases them.",
                new Vector2(10f, -8f), new Vector2(560f, 86f), 15, Color.white);
            badge = Label("Driver badge", transform, "", new Vector2(14f, -124f),
                new Vector2(560f, 28f), 18, new Color(1f, 0.86f, 0.35f));

            RectTransform promptRect = Box("Talk prompt", transform, new Vector2(0.5f, 0f),
                new Vector2(0f, 22f), new Vector2(320f, 38f), Color.clear);
            prompt = Label("Prompt", promptRect, "", new Vector2(0f, 0f),
                new Vector2(320f, 38f), 22, Color.white);
            prompt.alignment = TextAnchor.MiddleCenter;

            RectTransform chat = Box("Conversation", transform, new Vector2(1f, 0f),
                new Vector2(-18f, 70f), new Vector2(560f, 410f), new Color(0.045f, 0.06f, 0.08f, 0.96f));
            panel = chat.gameObject;
            header = Label("Speaker", chat, "", new Vector2(16f, -12f),
                new Vector2(420f, 30f), 21, Color.white);
            ButtonAt("Close", chat, new Vector2(468f, -10f), new Vector2(76f, 32f),
                () => bootstrap.Coordinator.Close(ConversationCloseReason.PlayerClosed));

            RectTransform viewport = Box("History viewport", chat, new Vector2(0f, 1f),
                new Vector2(16f, -50f), new Vector2(528f, 238f), new Color(0.02f, 0.03f, 0.04f, 0.6f));
            viewport.gameObject.AddComponent<RectMask2D>();
            history = Rect("History", viewport);
            history.anchorMin = new Vector2(0f, 1f);
            history.anchorMax = Vector2.one;
            history.pivot = new Vector2(0.5f, 1f);
            history.sizeDelta = Vector2.zero;
            var layout = history.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            history.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = history;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            RectTransform pending = Rect("Pending reply", chat);
            Place(pending, new Vector2(0f, 1f), new Vector2(16f, -294f), new Vector2(528f, 32f));
            waiting = pending.gameObject;
            Label("Waiting", pending, "Waiting for a reply…", Vector2.zero, new Vector2(380f, 32f),
                17, new Color(1f, 0.86f, 0.35f));
            ButtonAt("Cancel", pending, new Vector2(432f, 0f), new Vector2(96f, 30f),
                () => bootstrap.Coordinator.CancelPending());

            RectTransform field = Box("Chat input", chat, new Vector2(0f, 1f),
                new Vector2(16f, -334f), new Vector2(420f, 34f), new Color(0.16f, 0.2f, 0.24f));
            Text value = Label("Text", field, "", new Vector2(8f, -5f), new Vector2(404f, 24f), 17, Color.white);
            Text placeholder = Label("Placeholder", field, "Type a message…", new Vector2(8f, -5f),
                new Vector2(404f, 24f), 17, new Color(0.65f, 0.7f, 0.75f));
            input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = field.GetComponent<Image>();
            input.textComponent = value;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = bootstrap.Coordinator.Settings.MaxLineLength;
            input.customCaretColor = true;
            input.caretColor = Color.white;
            input.onSubmit.AddListener(_ => Submit());
            send = ButtonAt("Send", chat, new Vector2(448f, -334f), new Vector2(96f, 34f), Submit);
            ButtonAt("Return to world", chat, new Vector2(16f, -376f), new Vector2(160f, 26f), ReleaseInput);
            Label("Footer", chat, "Esc closes · Enter sends · Scroll to read history", new Vector2(188f, -378f),
                new Vector2(356f, 26f), 14, new Color(0.75f, 0.8f, 0.85f));

            RectTransform offer = Box("Trade card", transform, new Vector2(1f, 0f),
                new Vector2(-18f, 488f), new Vector2(560f, 96f), new Color(0.045f, 0.06f, 0.08f, 0.96f));
            tradeCard = offer.gameObject;
            tradeOffer = offer;
            tradeSummary = Label("Offer", offer, "", new Vector2(16f, -8f),
                new Vector2(528f, 26f), 17, Color.white);
            tradeGold = Label("Your gold", offer, "", new Vector2(16f, -34f),
                new Vector2(180f, 22f), 16, Color.white);
            tradeWarning = Label("Affordability", offer, "", new Vector2(204f, -34f),
                new Vector2(340f, 22f), 16, new Color(1f, 0.82f, 0.45f));
            tradeCard.SetActive(false);
        }

        public void Render()
        {
            var coordinator = bootstrap.Coordinator;
            badge.text = coordinator.DriverLabel;
            panel.SetActive(coordinator.IsOpen);
            RenderTradeCard();
            if (!coordinator.IsOpen)
            {
                ReleaseInput();
                displayedNpcId = null;
                input.SetTextWithoutNotify("");
                return;
            }

            var npc = coordinator.ActiveNpc;
            bool switched = displayedNpcId != npc.Id;
            displayedNpcId = npc.Id;
            header.text = $"{npc.DisplayName} — {npc.Role}";
            waiting.SetActive(coordinator.IsAwaitingReply);
            send.interactable = !coordinator.IsAwaitingReply;
            input.characterLimit = coordinator.Settings.MaxLineLength;

            foreach (Transform child in history)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            foreach (ChatLine line in coordinator.HistoryFor(npc.Id))
            {
                string name = line.Speaker == ChatSpeaker.Player ? "You" : npc.DisplayName;
                Color color = line.Speaker == ChatSpeaker.Player
                    ? new Color(0.65f, 0.85f, 1f) : new Color(0.92f, 0.92f, 0.86f);
                string text = $"{name} — {line.Text}";
                if (line.Speaker == ChatSpeaker.System)
                {
                    text = $"[System] {line.Text}";
                    color = new Color(1f, 0.82f, 0.45f);
                }

                Label("Line", history, text, Vector2.zero, new Vector2(508f, 0f), 18, color);
            }

            scrollToBottom = true;
            if (switched)
            {
                input.SetTextWithoutNotify("");
                FocusInput();
            }
        }

        private void OnChanged(ConversationEvent change)
        {
            Render();
        }

        private void RenderTradeCard()
        {
            var coordinator = bootstrap.Coordinator;
            TradeQuote quote = bootstrap.Trade.ActiveQuote;
            bool visible = quote != null && coordinator.IsOpen && coordinator.ActiveNpc != null &&
                quote.NpcId == coordinator.ActiveNpc.Id && quote.ConversationGeneration == coordinator.Generation;
            tradeCard.SetActive(visible);
            if (!visible)
            {
                return;
            }

            string item = bootstrap.Inventory.Catalog.Get(quote.ItemId).DisplayName;
            bool sale = quote.Direction == TradeDirection.Sale;
            tradeSummary.text = sale
                ? $"Sell {item} ×{quote.Quantity} — you receive {quote.Total} gold"
                : $"{item} ×{quote.Quantity} — {quote.Total} gold";
            tradeGold.text = $"Your gold: {bootstrap.Inventory.Gold}";
            bool affordable = sale || bootstrap.Inventory.Gold >= quote.Total;
            tradeWarning.text = affordable ? "" : "Not enough gold";
            if (buttonsQuoteId != quote.Id)
            {
                BuildTradeButtons(quote.Id);
            }

            tradeConfirm.GetComponentInChildren<Text>().text = sale
                ? $"Sell for {quote.Total} gold" : $"Buy for {quote.Total} gold";
            tradeConfirm.interactable = affordable && confirmedQuoteId != quote.Id;
        }

        // New buttons for every quote, never relabelled ones: uGUI fires onClick on release, so a
        // press that began on the old card would otherwise confirm the offer that replaced it.
        private void BuildTradeButtons(string quoteId)
        {
            if (tradeConfirm != null)
            {
                tradeConfirm.gameObject.SetActive(false);
                Destroy(tradeConfirm.gameObject);
                tradeDecline.gameObject.SetActive(false);
                Destroy(tradeDecline.gameObject);
            }

            buttonsQuoteId = quoteId;
            Button confirm = null;
            confirm = ButtonAt("Confirm", tradeOffer, new Vector2(16f, -60f), new Vector2(240f, 28f), () =>
            {
                bootstrap.BlockWorldInputForFrame();
                if (confirmedQuoteId == quoteId)
                {
                    return;
                }

                confirmedQuoteId = quoteId;
                confirm.interactable = false;
                // Inventory and receipt events can render synchronously inside Confirm. Keep this
                // quote disabled across those renders, even when the service refuses the purchase.
                bootstrap.Trade.Confirm(quoteId);
            });
            tradeConfirm = confirm;
            tradeDecline = ButtonAt("Decline", tradeOffer, new Vector2(268f, -60f), new Vector2(100f, 28f), () =>
            {
                bootstrap.BlockWorldInputForFrame();
                bootstrap.Trade.Decline(quoteId);
            });
        }

        private void LateUpdate()
        {
            if (focusRequested && panel.activeSelf)
            {
                focusRequested = false;
                EventSystem.current.SetSelectedGameObject(input.gameObject);
                input.ActivateInputField();
            }

            if (scrollToBottom && panel.activeSelf)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0f;
                scrollToBottom = false;
            }
        }

        private void Submit()
        {
            bootstrap.BlockWorldInputForFrame();
            if (bootstrap.Coordinator.Submit(input.text))
            {
                input.SetTextWithoutNotify("");
            }

            FocusInput();
        }

        private void FocusInput()
        {
            bootstrap.BlockWorldInputForFrame();
            // InputField deactivates itself after invoking onSubmit. Refocus after that callback.
            focusRequested = true;
        }

        private void ReleaseInput()
        {
            bootstrap.BlockWorldInputForFrame();
            focusRequested = false;
            input.DeactivateInputField();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private RectTransform Rect(string objectName, Transform parent)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private RectTransform Box(string objectName, Transform parent, Vector2 anchor,
            Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(objectName, parent);
            Place(rect, anchor, position, size);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private Text Label(string objectName, Transform parent, string text, Vector2 position,
            Vector2 size, int fontSize, Color color)
        {
            RectTransform rect = Rect(objectName, parent);
            Place(rect, new Vector2(0f, 1f), position, size);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.text = text;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private Button ButtonAt(string caption, Transform parent, Vector2 position,
            Vector2 size, UnityEngine.Events.UnityAction clicked)
        {
            RectTransform rect = Box(caption, parent, new Vector2(0f, 1f), position, size,
                new Color(0.22f, 0.3f, 0.35f));
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(clicked);
            Text label = Label("Label", rect, caption, Vector2.zero, size, 16, Color.white);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private void OnDestroy()
        {
            if (bootstrap != null && bootstrap.Coordinator != null)
            {
                bootstrap.Coordinator.Changed -= OnChanged;
            }

            if (bootstrap != null && bootstrap.Trade != null)
            {
                bootstrap.Trade.QuotesChanged -= RenderTradeCard;
            }

            if (bootstrap != null && bootstrap.Inventory != null)
            {
                bootstrap.Inventory.Changed -= RenderTradeCard;
            }
        }
    }
}
