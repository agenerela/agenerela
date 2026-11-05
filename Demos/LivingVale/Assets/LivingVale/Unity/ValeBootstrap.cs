using System;
using LivingVale.Game;
using LivingVale.Interaction;
using LivingVale.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LivingVale.Unity
{
    [DefaultExecutionOrder(-1000)]
    public sealed class ValeBootstrap : MonoBehaviour
    {
        [SerializeField] private ThirdPersonPlayer player;
        [SerializeField] private ChatOverlay overlay;
        [SerializeField, Min(0.1f)] private float turnTimeout = 8f;

        private InputActionMap actions;
        private InputAction move;
        private InputAction sprint;
        private InputAction look;
        private InputAction talk;
        private InputAction close;
        private InputAction inventoryKey;
        private InputAction[] fixtures;
        private bool capturedTextFocus;
        private ConversationTradeLink tradeLink;
        private InventoryHud inventoryHud;
        private int blockedFrame = -1;

        public ConversationCoordinator Coordinator { get; private set; }
        public ScriptedNpcDecisionDriver Driver { get; private set; }

        /// <summary>The player's holdings. G2 hands loot in through it as an <see cref="ILootSink"/>.</summary>
        public PlayerInventory Inventory { get; private set; }
        public TradeService Trade { get; private set; }

        /// <summary>The executor every NPC driver shares; G5's driver registers <see cref="NpcActionExecutor.AvailableFor"/>.</summary>
        public NpcActionExecutor Executor { get; private set; }
        public ILootSink Loot => Inventory;
        public bool WorldInputBlocked => !Application.isFocused || capturedTextFocus ||
            blockedFrame == Time.frameCount || (overlay != null && overlay.InputHasFocus);
        public Vector2 Movement => WorldInputBlocked ? Vector2.zero : move.ReadValue<Vector2>();
        public bool Sprinting => !WorldInputBlocked && sprint.IsPressed();
        public Vector2 OrbitDelta => WorldInputBlocked || Coordinator.IsOpen ? Vector2.zero : look.ReadValue<Vector2>();
        public bool TalkPressed => !WorldInputBlocked && talk.WasPressedThisFrame();

        /// <summary>I toggles the inventory, except while typing.</summary>
        public bool InventoryPressed => !WorldInputBlocked && inventoryKey.WasPressedThisFrame();

        public void Configure(ThirdPersonPlayer scenePlayer, ChatOverlay sceneOverlay)
        {
            player = scenePlayer;
            overlay = sceneOverlay;
        }

        private void Awake()
        {
            GameEvents.ReportSubscriberError = Debug.LogException;
            var catalog = ItemCatalog.CreateDefault();
            Inventory = PlayerInventory.CreateStarting(catalog);

            // The coordinator needs the driver, the driver the executor, the executor the trade
            // service, and the trade service the conversation: the link is attached last.
            tradeLink = new ConversationTradeLink();
            Trade = new TradeService(Inventory, new[] { VendorStock.CreateMara(catalog) }, tradeLink);
            Executor = new NpcActionExecutor(Trade);
            Driver = new ScriptedNpcDecisionDriver(executor: Executor);
            Coordinator = new ConversationCoordinator(Driver, new ConversationSettings
            {
                TurnTimeout = TimeSpan.FromSeconds(Mathf.Max(0.1f, turnTimeout))
            });
            tradeLink.Attach(Coordinator, Trade);

            actions = new InputActionMap("Living Vale");
            move = actions.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            sprint = actions.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            sprint.AddBinding("<Keyboard>/rightShift");
            look = actions.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            talk = actions.AddAction("Talk", InputActionType.Button, "<Keyboard>/e");
            close = actions.AddAction("Close", InputActionType.Button, "<Keyboard>/escape");
            inventoryKey = actions.AddAction("Inventory", InputActionType.Button, "<Keyboard>/i");
            fixtures = new InputAction[5];
            for (int i = 0; i < fixtures.Length; i++)
            {
                fixtures[i] = actions.AddAction($"Fixture{i + 1}", InputActionType.Button, $"<Keyboard>/f{i + 1}");
            }

            overlay.Initialize(this);
            inventoryHud = overlay.gameObject.AddComponent<InventoryHud>();
            inventoryHud.Initialize(this);
            foreach (var speaker in FindObjectsByType<NpcSpeaker>(FindObjectsSortMode.None))
            {
                speaker.Bind(this, player.transform);
            }

            Coordinator.Changed += OnConversationChanged;
        }

        private void OnEnable()
        {
            actions.Enable();
            RefreshCursor();
        }

        private void Update()
        {
            // Capture before the EventSystem can deselect the field on Enter, Esc or a click.
            // Every gameplay component sees the same focus decision for the entire frame.
            capturedTextFocus = overlay.InputHasFocus;
            if (close.WasPressedThisFrame())
            {
                BlockWorldInputForFrame();
                Coordinator.Close(ConversationCloseReason.PlayerClosed);
            }
        }

        private void LateUpdate()
        {
            // UI selection has now been processed, including clicks into the input this frame.
            if (WorldInputBlocked)
            {
                return;
            }

            if (InventoryPressed)
            {
                inventoryHud.Toggle();
            }

            for (int i = 0; i < fixtures.Length; i++)
            {
                if (fixtures[i].WasPressedThisFrame())
                {
                    Driver.Fixture = (ScriptedFixture)i;
                    overlay.Render();
                }
            }
        }

        public void BlockWorldInputForFrame()
        {
            blockedFrame = Time.frameCount;
        }

        private void OnConversationChanged(ConversationEvent change)
        {
            if (change.Kind == ConversationEventKind.Opened || change.Kind == ConversationEventKind.Closed)
            {
                BlockWorldInputForFrame();
                RefreshCursor();
            }
        }

        private void RefreshCursor()
        {
            bool free = Coordinator.IsOpen || !Application.isFocused;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (Coordinator != null)
            {
                BlockWorldInputForFrame();
                RefreshCursor();
            }
        }

        private void OnDisable()
        {
            actions?.Disable();
            Coordinator?.Close(ConversationCloseReason.Interrupted);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (Coordinator != null)
            {
                Coordinator.Changed -= OnConversationChanged;
            }

            tradeLink?.Dispose();

            actions?.Dispose();
        }
    }
}
