using System;
using System.Collections.Generic;
using LivingVale.Agenerela;
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
        [Tooltip("The NPC reply driver selected when the game starts. F6 switches drivers.")]
        [SerializeField] private NpcDriverChoice driver = NpcDriverChoice.Agenerela;
        [Tooltip("The local Ollama model used by the Agenerela NPC driver.")]
        [SerializeField] private string ollamaModel = "gemma4:e2b-it-qat";
        [Tooltip("The models F7 cycles through, in order, skipping any Ollama does not have. Compared on 5 November 2026 (llm-wiki findings).")]
        [SerializeField] private List<string> ollamaModels = new List<string>(ModelCycleDefaults.Models);
        [Tooltip("The Ollama server: its endpoint, and the environment variable holding its key. Empty uses a local Ollama at 127.0.0.1:11434. Its model is ignored: the model above and F7 choose it.")]
        [SerializeField] private global::Agenerela.Providers.OllamaProviderConfig ollamaServer;

        private InputActionMap actions;
        private InputAction move;
        private InputAction sprint;
        private InputAction look;
        private InputAction talk;
        private InputAction close;
        private InputAction inventoryKey;
        private InputAction mapKey;
        private InputAction[] fixtures;
        private InputAction switchDriver;
        private InputAction saveKey;
        private InputAction loadKey;
        private InputAction switchModel;
        private readonly System.Threading.CancellationTokenSource lifetime = new System.Threading.CancellationTokenSource();
        private bool capturedTextFocus;
        private ConversationTradeLink tradeLink;
        private InventoryHud inventoryHud;
        private NavigationTargets navigation;
        private MapOverlay map;
        private int blockedFrame = -1;

        public ConversationCoordinator Coordinator { get; private set; }
        public ScriptedNpcDecisionDriver Driver { get; private set; }
        /// <summary>Every speaking NPC's agent, answering from the model or, after F6, from the script.</summary>
        public AgenerelaNpcDecisionDriver ModelDriver { get; private set; }

        /// <summary>F6: the script or the model, as the provider of the same agents.</summary>
        public NpcProviderSwitch Drivers { get; private set; }

        /// <summary>F7: the next installed model for the Agenerela driver.</summary>
        public ModelCycle Models { get; private set; }

        /// <summary>The model key's last game-authored line, shown beside the driver badge.</summary>
        public string DriverNotice => Models?.Notice ?? "";

        /// <summary>The player's holdings. G2 hands loot in through it as an <see cref="ILootSink"/>.</summary>
        public PlayerInventory Inventory { get; private set; }
        public TradeService Trade { get; private set; }

        /// <summary>XP, levels and perks (G4).</summary>
        public Progression Progression { get; private set; }

        /// <summary>First visits to the region's places, paid in XP through <see cref="Progression"/> (G4b).</summary>
        public LivingVale.World.PlaceDiscovery Discovery { get; private set; }

        /// <summary>The main and side quests (G4). Iven's <c>offer_quest</c> goes through it via the executor.</summary>
        public QuestLog Quests { get; private set; }

        /// <summary>G4c: the recovery routes, endings, side quests and world pickups, over <see cref="Quests"/>.</summary>
        public QuestRoutes Routes { get; private set; }

        /// <summary>F9 saves and F10 loads through it, under <see cref="Application.persistentDataPath"/>.</summary>
        public SaveGame Saves { get; private set; }

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

        /// <summary>M toggles the map (G8a), except while typing.</summary>
        public bool MapPressed => !WorldInputBlocked && mapKey.WasPressedThisFrame();

        /// <summary>Where the quest goal and the NPCs are now, for the compass, the map and the name tags (G8a).</summary>
        public NavigationTargets Navigation => navigation;

        public void Configure(ThirdPersonPlayer scenePlayer, ChatOverlay sceneOverlay)
        {
            player = scenePlayer;
            overlay = sceneOverlay;
        }

        private void Awake()
        {
            GameEvents.ReportSubscriberError = Debug.LogException;
            if (HudShot.RequestedFolder() != null)
            {
                driver = NpcDriverChoice.Scripted; // A layout check never loads a model.
            }
            var catalog = ItemCatalog.CreateDefault();
            Inventory = PlayerInventory.CreateStarting(catalog);

            // The coordinator needs the driver, the driver the executor, the executor the trade
            // service, and the trade service the conversation: the link is attached last.
            tradeLink = new ConversationTradeLink();
            Trade = new TradeService(Inventory, new[] { VendorStock.CreateMara(catalog) }, tradeLink);
            Progression = new Progression();
            Discovery = new LivingVale.World.PlaceDiscovery(Progression);
            Quests = new QuestLog(Inventory, Progression, tradeLink);
            Saves = new SaveGame(Inventory, Trade, Quests, Progression, SaveGame.DefaultDirectory);
            Routes = new QuestRoutes(Quests, tradeLink);
            Executor = new NpcActionExecutor(Trade, Quests, Routes);
            Driver = new ScriptedNpcDecisionDriver(executor: Executor);
            ModelDriver = AgenerelaNpcDecisionDriver.ForOllama(ollamaModel, Executor, server: ollamaServer);
            // The scripted replies are a provider of the same agents (#194, #198): one pipeline and
            // one memory per NPC, whichever answers (LV #23).
            Drivers = new NpcProviderSwitch(ModelDriver, new ScriptedReplies(Driver), driver);
            Saves.Routes = Routes;

            Coordinator = new ConversationCoordinator(ModelDriver, new ConversationSettings
            {
                TurnTimeout = TimeSpan.FromSeconds(Mathf.Max(0.1f, turnTimeout))
            });
            tradeLink.Attach(Coordinator, Trade);
            tradeLink.AttachQuests(Quests);
            tradeLink.AttachRoutes(Routes);

            // The route cards are the NPCs' proposals (#236): they end with the conversation and with a load.
            tradeLink.EndPendingCards = () => ModelDriver.DeclineProposals("The conversation moved on.");
            Saves.EndPendingCards = () => ModelDriver.DeclineProposals("A save was loaded.");
            // The server's own model list, warm-up and unload (#198), through the provider in use.
            Models = new ModelCycle(Coordinator, Drivers,
                new ModelSelector(ollamaModels, Drivers.ModelLabel, new ProviderModelCatalog(() => Drivers.ModelProvider)),
                model => AgenerelaNpcDecisionDriver.OllamaFor(model, ollamaServer));
            Models.Finished += OnModelCycled;
            Models.NoticeChanged += OnModelCycled;

            // NPC memories save and load with the game (G5h); F7 keeps the same driver and agents.
            Saves.Memories = new NpcMemoryArchive(() => ModelDriver);

            // The starting model loads like any other, so the notice says "loading …", and says
            // so when it does not load (not installed, server down) while F6 and F7 still work.
            if (driver == NpcDriverChoice.Agenerela)
            {
                Models.LoadCurrent(lifetime.Token);
            }

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
            mapKey = actions.AddAction("Map", InputActionType.Button, "<Keyboard>/m");
            fixtures = new InputAction[5];
            for (int i = 0; i < fixtures.Length; i++)
            {
                fixtures[i] = actions.AddAction($"Fixture{i + 1}", InputActionType.Button, $"<Keyboard>/f{i + 1}");
            }
            switchDriver = actions.AddAction("SwitchDriver", InputActionType.Button, "<Keyboard>/f6");
            saveKey = actions.AddAction("Save", InputActionType.Button, "<Keyboard>/f9");
            loadKey = actions.AddAction("Load", InputActionType.Button, "<Keyboard>/f10");
            switchModel = actions.AddAction("SwitchModel", InputActionType.Button, "<Keyboard>/f7");

            overlay.Initialize(this);
            inventoryHud = overlay.gameObject.AddComponent<InventoryHud>();
            inventoryHud.Initialize(this);
            foreach (var speaker in FindObjectsByType<NpcSpeaker>(FindObjectsSortMode.None))
            {
                speaker.Bind(this, player.transform);
            }

            Coordinator.Changed += OnConversationChanged;
        }

        // G8a: the compass, the name tags and the map are built here at runtime, after every scene
        // object has woken, so the scene builders need no change for them.
        private void Start()
        {
            navigation = new NavigationTargets(this);
            Transform feet = player.transform;
            new GameObject("Compass HUD", typeof(RectTransform)).AddComponent<CompassHud>().Initialize(this, feet, navigation);
            new GameObject("NPC name tags", typeof(RectTransform)).AddComponent<NpcNameTags>().Initialize(feet, navigation);
            map = new GameObject("Map", typeof(RectTransform)).AddComponent<MapOverlay>();
            map.Initialize(this, feet, navigation);
            HudShot.StartIfRequested(gameObject, map.Toggle);
        }

        private void OnEnable()
        {
            actions.Enable();
            RefreshCursor();
        }

        private CombatInventoryBridge combatBridge;
        private LivingVale.Combat.CombatDirector bridgedDirector;

        // Checked every frame rather than once at Start, so a director enabled later (or a new
        // one) also gets the bridge instead of logging loot and charging nothing.
        private void BridgeCombatToInventory()
        {
            var combat = LivingVale.Combat.CombatDirector.Active;
            if (combat == null || combat == bridgedDirector)
            {
                return;
            }

            combatBridge ??= new CombatInventoryBridge(Inventory);
            combat.LootSink = combatBridge;
            combat.DefeatPenalty = combatBridge;
            bridgedDirector = combat;
        }

        private void Update()
        {
            BridgeCombatToInventory();
            Vector3 feet = player.transform.position;
            Discovery.Visit(feet.x, feet.z);

            // Capture before the EventSystem can deselect the field on Enter, Esc or a click.
            // Every gameplay component sees the same focus decision for the entire frame.
            capturedTextFocus = overlay.InputHasFocus;
            if (close.WasPressedThisFrame())
            {
                BlockWorldInputForFrame();
                Coordinator.Close(ConversationCloseReason.PlayerClosed);
                if (map != null)
                {
                    map.Hide();
                }
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

            if (MapPressed && map != null)
            {
                map.Toggle();
            }

            for (int i = 0; i < fixtures.Length; i++)
            {
                if (fixtures[i].WasPressedThisFrame())
                {
                    Driver.Fixture = (ScriptedFixture)i;
                    overlay.Render();
                }
            }

            if (switchDriver.WasPressedThisFrame())
            {
                Drivers.Next();
                if (Drivers.Choice == NpcDriverChoice.Agenerela)
                {
                    Models.LoadCurrent(lifetime.Token);
                }

                overlay.Render();
            }

            if (saveKey.WasPressedThisFrame())
            {
                SaveNow();
            }

            if (loadKey.WasPressedThisFrame())
            {
                LoadNow();
            }

            if (switchModel.WasPressedThisFrame())
            {
                CycleModel();
            }
        }

        /// <summary>Writes the save. Nothing in flight is saved: no request, quote or offer.</summary>
        public bool SaveNow()
        {
            bool saved = Saves.TrySave(out string error);
            if (saved)
            {
                Debug.Log($"[LivingVale] Saved to {Saves.SavePath}.");
            }
            else
            {
                Debug.LogWarning($"[LivingVale] Save failed: {error}");
            }

            return saved;
        }

        /// <summary>
        /// Loads the save. The open conversation closes first, which cancels its request and moves
        /// the generation on, so a reply still on its way can never execute against the loaded state.
        /// A failed load leaves the game as it was.
        /// </summary>
        public bool LoadNow()
        {
            Coordinator.Close(ConversationCloseReason.Interrupted);
            bool loaded = Saves.TryLoad(out string error);
            if (loaded)
            {
                // NPC memory now stands at the save; the chat history on screen must not show
                // lines said after it (G5h saves memory, the shown history is not saved).
                Coordinator.ClearHistories();
                // The load may have reversed the last trades, so neither driver may report them.
                ModelDriver?.ForgetReceipts();
                Debug.Log($"[LivingVale] Loaded {Saves.SavePath}.");
            }
            else
            {
                Debug.LogWarning($"[LivingVale] Load failed, nothing changed: {error}");
            }

            overlay.Render();
            return loaded;
        }

        private async void CycleModel()
        {
            try
            {
                await Models.NextAsync(lifetime.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                // The scene went away mid-press; nothing to show.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnModelCycled()
        {
            if (this == null || overlay == null)
            {
                return;
            }

            overlay.Render();
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
            lifetime.Cancel();
            if (Models != null)
            {
                Models.Finished -= OnModelCycled;
                Models.NoticeChanged -= OnModelCycled;
            }

            if (Coordinator != null)
            {
                Coordinator.Changed -= OnConversationChanged;
            }

            tradeLink?.Dispose();
            navigation?.Dispose();

            actions?.Dispose();
        }
    }
}
