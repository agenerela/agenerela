using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Agenerela;
using Agenerela.Providers;
using Demos.Shared;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// The village as a whole: who the player is talking to, the one recorder every agent writes to,
    /// and the agent board. Each agent's own decisions run in its <see cref="AgentMind"/>.
    /// </summary>
    /// <remarks>
    /// The player's line goes to the nearest agent within <see cref="SpeakingDistance"/>; the game,
    /// not the framework, decides who is being spoken to (DR-008). Every agent's
    /// <see cref="AgentBehaviour"/> names the same provider config, so they share one provider and
    /// one queue (DR-013, DR-015).
    /// </remarks>
    public sealed class VillageCommands : MonoBehaviour
    {
        public DemoCommandInput Input;
        public DecisionDebugOverlay Overlay;
        public AgentBoard Board;
        public VillageHud Hud;
        public DayNightCycle Day;
        public PlayerInventory Inventory;
        public QuestBook Quests;
        public VillageBook Book;
        [Tooltip("While it is open, no gameplay key does anything.")]
        public DemoMenu Menu;
        [Tooltip("The presenter's keys and their panel; world labels keep out from under it.")]
        public VillagePresenter Presenter;
        [Tooltip("How close the player must be to pick something up, in metres.")]
        public float ReachDistance = 2.2f;
        public Transform Player;
        public AgentMind[] Minds = new AgentMind[0];
        public float SpeakingDistance = 6f;
        [Tooltip("Append every decision to Logs/agenerela-decisions-<time>.jsonl.")]
        public bool WriteLogFile = true;
        [Tooltip("After the model fails to answer, how long the village waits before it asks again on its own. The player's lines are always sent.")]
        public float ModelRetrySeconds = 20f;

        /// <summary>
        /// What is wrong with the model, in words for the HUD, while the last decision failed because
        /// no model answered (Ollama stopped, the model not pulled, a timeout); null while it answers.
        /// </summary>
        public string ModelProblem { get; private set; }
        /// <summary>When the model last failed to answer, in game time.</summary>
        public float ModelFailedAt { get; private set; } = float.NegativeInfinity;

        private DecisionRecorder recorder;
        private bool writeLog = true;
        private readonly List<AgentCard> cards = new List<AgentCard>();
        private readonly List<BubbleInfo> bubbles = new List<BubbleInfo>();
        private readonly List<TagInfo> names = new List<TagInfo>();
        private readonly List<TagInfo> places = new List<TagInfo>();
        private readonly List<Rect> covers = new List<Rect>();
        private readonly List<string> queue = new List<string>();
        private NameTag[] nameTags;
        private Pickup[] pickups;
        private int bookVersion;
        private readonly List<VillageBook.Entry> bookItems = new List<VillageBook.Entry>();
        private readonly List<VillageBook.QuestEntry> bookQuests = new List<VillageBook.QuestEntry>();
        private readonly List<(string, string, bool)> tracker = new List<(string, string, bool)>();
        private Targetable[] targetables;

        private static readonly (string key, string what)[] Keys =
        {
            ("WASD", "walk"), ("Shift", "run"), ("RMB", "look"), ("E", "talk / pick up"), ("I", "pack"), ("J", "journal"),
            ("Tab", "framework view"), ("H", "requests"), ("Esc", "menu"), ("F12", "presenter")
        };

        /// <summary>Every decision this session, from every agent. Made on first use.</summary>
        public DecisionRecorder Recorder =>
            recorder ??= new DecisionRecorder(WriteLogFile && writeLog ? DecisionRecorder.DefaultFilePath() : null);

        public bool IsDeciding => Minds.Any(m => m != null && m.IsDeciding);

        /// <summary>
        /// Whether something the game noticed by itself (a quiet moment, a trespass, the wolf, the
        /// bell) goes to the model now. Not while the model is down, until <see cref="ModelRetrySeconds"/>
        /// has passed: four agents asking a stopped server every few seconds would fill the console
        /// and change nothing. The player's own lines are always sent.
        /// </summary>
        public bool MayAskOnItsOwn => ModelProblem == null || Time.time - ModelFailedAt >= ModelRetrySeconds;

        /// <summary>
        /// Called by every mind when a decision ends: a failure to reach the model sets
        /// <see cref="ModelProblem"/>, and any reply clears it, even one that could not be read or
        /// that a guard stopped, because a model answered.
        /// </summary>
        public void ReportModel(DecisionResult result)
        {
            if (result == null) return;
            if (result.Decision != null || result.Reply != null)
            {
                if (ModelProblem != null && Hud != null) Hud.Toast("The model is answering again.");
                ModelProblem = null;
                return;
            }
            string problem = Describe(result.Telemetry.ErrorKind);
            if (problem != null) ReportModel(problem);
        }

        /// <summary>Records that the model cannot be asked, in words for the HUD, such as a provider config that is not valid.</summary>
        public void ReportModel(string problem)
        {
            ModelFailedAt = Time.time;
            ModelProblem = problem;
        }

        // The HUD's words for a failure that means no model answered; null for the others (an
        // unreadable reply, a guard that threw), which are about one decision, not the model.
        private string Describe(PipelineErrorKind? kind)
        {
            string model = "the model", where = "Ollama";
            var config = Minds.Length > 0 && Minds[0] != null ? Minds[0].Behaviour.Provider : null;
            if (config is OllamaProviderConfig ollama)
            {
                model = ollama.Model;
                where = "Ollama at " + ollama.Endpoint;
            }
            switch (kind)
            {
                case PipelineErrorKind.ProviderUnreachable:
                    return "No model is running: " + where + " cannot be reached. Start Ollama; the agents answer once it is back.";
                case PipelineErrorKind.ModelNotFound:
                    return "Ollama does not have " + model + ". Run \"ollama pull " + model + "\"; the agents answer once it is there.";
                case PipelineErrorKind.ProviderTimeout:
                    return "The model did not answer in time. It may still be loading; the agents try again shortly.";
                case PipelineErrorKind.ProviderRejected:
                case PipelineErrorKind.ProviderServerError:
                case PipelineErrorKind.ProviderFailed:
                    return "The model failed to answer (" + kind + "). The village keeps running; the agents try again shortly.";
                default:
                    return null;
            }
        }

        private void OnEnable()
        {
            Input.Submitted += OnSubmitted;
            if (Inventory != null) Inventory.Changed += OnInventoryChanged;
            if (Quests != null) Quests.Message += OnQuestMessage;
        }

        private void OnDisable()
        {
            Input.Submitted -= OnSubmitted;
            if (Inventory != null) Inventory.Changed -= OnInventoryChanged;
            if (Quests != null) Quests.Message -= OnQuestMessage;
        }

        private void OnInventoryChanged(string id, int delta)
        {
            bookVersion++;
            if (Hud == null || delta == 0) return;
            if (id == "coins") Hud.Toast((delta > 0 ? "+" : "") + delta + " coins");
            else Hud.Toast((delta > 0 ? "Got " : "Gave ") + (ItemCatalog.Get(id)?.Name ?? id) + (System.Math.Abs(delta) > 1 ? " x" + System.Math.Abs(delta) : ""));
        }

        private void OnQuestMessage(string text)
        {
            bookVersion++;
            if (Hud != null) Hud.Toast(text, 5f);
        }

        /// <summary>The pickup the player could take now: the nearest active one within reach, or null.</summary>
        public Pickup NearestPickup()
        {
            pickups ??= FindObjectsByType<Pickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Pickup best = null;
            float bestDistance = ReachDistance;
            foreach (var p in pickups)
            {
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(Player.position, p.transform.position);
                if (d < bestDistance) { best = p; bestDistance = d; }
            }
            return best;
        }

        /// <summary>
        /// Gives every agent this provider config, so fresh agents with empty memories, and starts a
        /// new recorder. Tests pass a config whose provider is a fake.
        /// </summary>
        public void UseProvider(ProviderConfig config, bool writeLogFile = false)
        {
            writeLog = writeLogFile;
            recorder = null;
            ModelProblem = null;
            ModelFailedAt = float.NegativeInfinity;
            foreach (var mind in Minds) mind.UseProvider(config);
        }

        /// <summary>
        /// Fresh agents with empty memories, on the provider config they already ask, and a new
        /// recorder that keeps the log-file setting. The presenter's reset.
        /// </summary>
        public void ResetAgents()
        {
            var config = Minds.Length > 0 && Minds[0] != null ? Minds[0].Behaviour.Provider : null;
            UseProvider(config, writeLog && WriteLogFile);
        }

        /// <summary>The agent with this name, as its profile gives it.</summary>
        public AgentMind Mind(string agentName) => Minds.Single(m => m.Name == agentName);

        public string ProviderLabel
        {
            get
            {
                var config = Minds.Length > 0 ? Minds[0].Behaviour.Provider : null;
                if (config is OllamaProviderConfig ollama) return "ollama / " + ollama.Model;
                return config != null ? config.Provider.Name : "project default";
            }
        }

        // ---- The player speaks ---------------------------------------------------------------

        private async void OnSubmitted(string text) => await SubmitAsync(text);

        /// <summary>The player's typed line, to the nearest agent in earshot. Completes when it has been executed.</summary>
        public async Awaitable SubmitAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || (Menu != null && Menu.IsOpen)) return;
            text = text.Trim();
            var listener = Nearest();
            if (listener == null)
            {
                Overlay.ShowPlaceholder("Player: " + text, "none", "no_target",
                    "Move within " + SpeakingDistance.ToString("0", CultureInfo.InvariantCulture) + " m of someone to speak.");
                return;
            }
            await listener.HearPlayerAsync(text);
        }

        /// <summary>The agent the player would speak to now: the nearest within <see cref="SpeakingDistance"/>, or null.</summary>
        public AgentMind Nearest() => Minds
            .Where(m => m != null)
            .Select(m => (m, d: Vector3.Distance(Player.position, m.transform.position)))
            .Where(x => x.d <= SpeakingDistance)
            .OrderBy(x => x.d)
            .Select(x => x.m)
            .FirstOrDefault();

        // ---- The board, the HUD and the dialogue panel -----------------------------------------

        private void Update()
        {
            var listener = Nearest();
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            bool typing = Input != null && Input.IsTyping;
            var pickup = NearestPickup();
            bool paused = Menu != null && Menu.IsOpen;
            if (keyboard != null && !typing && !paused)
            {
                if (keyboard.tabKey.wasPressedThisFrame && Board != null) Board.Toggle();
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    if (pickup != null && Inventory != null) pickup.Collect(Inventory);
                    else if (listener != null) Input.StartTyping();
                }
                if (Book != null && keyboard.iKey.wasPressedThisFrame) { Book.Toggle("inventory"); bookVersion++; }
                if (Book != null && keyboard.jKey.wasPressedThisFrame) { Book.Toggle("journal"); bookVersion++; }
            }
            if (Input != null)
                Input.Converse(listener?.Name, listener?.RoleNow,
                    listener == null ? null : listener.IsDeciding ? "thinking..." : ModelProblem != null ? "no model" : "listening",
                    listener?.Conversation, listener?.Body.Suggestions);
            ShowBoard(listener);
            ShowHud(listener, typing, pickup);
            ShowBook();
        }

        private void ShowBoard(AgentMind listener)
        {
            if (Board == null) return;
            while (cards.Count < Minds.Length) cards.Add(new AgentCard());
            for (int i = 0; i < Minds.Length; i++) Fill(cards[i], Minds[i], Minds[i] == listener);
            // The requests in flight or waiting, oldest first: the order the queue was asked in.
            queue.Clear();
            foreach (var m in Minds.Where(m => m != null && m.IsDeciding && m.Last != null).OrderBy(m => m.Last.Number))
                queue.Add(m.Name + "|" + m.Last.Priority);
            Board.Show("THE FRAMEWORK AT WORK", ProviderLabel + "   ·   one shared queue   ·   " + Minds.Length + " agents", cards, queue);
        }

        private void ShowBook()
        {
            if (Book == null || Inventory == null || Quests == null || !Book.IsOpen) return;
            bookItems.Clear();
            foreach (var (id, count) in Inventory.Items)
            {
                var item = ItemCatalog.Get(id);
                bookItems.Add(new VillageBook.Entry { Id = id, Name = item?.Name ?? id, Description = item?.Description ?? "", Count = count });
            }
            bookQuests.Clear();
            foreach (var q in Quests.Quests.Where(q => q.State != QuestState.Available))
                bookQuests.Add(new VillageBook.QuestEntry
                {
                    Title = q.Title, Giver = q.Giver, Text = q.Ask, Step = Quests.Step(q),
                    State = q.State == QuestState.Done ? "done" : Quests.IsReady(q) ? "ready" : "active"
                });
            Book.Fill(bookVersion, Inventory.Coins, bookItems, bookQuests);
        }

        private void ShowHud(AgentMind listener, bool typing, Pickup pickup)
        {
            if (Hud == null) return;
            var camera = Camera.main;
            if (Day != null) Hud.SetClock(Day.Clock, Day.Phase, Day.Day, Day.IsNight);
            Hud.SetHints(Keys);
            Hud.SetCovered(Input != null && Input.IsOpen, (Book != null && Book.IsOpen) || (Menu != null && Menu.IsOpen));
            Hud.SetCoins(Inventory != null ? Inventory.Coins : -1);
            Hud.SetAlert(ModelProblem);
            Hud.SetPrompt("E", typing ? null : pickup != null ? "Pick up " + pickup.Name
                : listener != null && !Input.IsOpen ? "Talk to " + listener.Name : null);
            if (Quests != null)
            {
                tracker.Clear();
                foreach (var q in Quests.Quests.Where(q => q.State == QuestState.Active))
                    tracker.Add((q.Title, Quests.Step(q), Quests.IsReady(q)));
                Hud.SetQuests(tracker);
            }

            // World labels and bubbles keep out from under every panel drawn over the world.
            covers.Clear();
            foreach (var r in new[] { Board != null ? Board.Bounds : null, Input != null ? Input.Bounds : null,
                         Book != null ? Book.Bounds : null, Menu != null ? Menu.Bounds : null,
                         Presenter != null ? Presenter.Bounds : null })
                if (r != null) covers.Add(r.Value);
            Hud.SetCoveredRects(covers);

            bubbles.Clear();
            foreach (var m in Minds)
            {
                if (m == null) continue;
                var head = m.transform.position + Vector3.up * 2.35f;
                if (m.Body.Speech != null)
                    bubbles.Add(new BubbleInfo
                    {
                        World = head, Text = m.Body.SpeechText,
                        Kind = m.Body.SpeechType == SpeechKind.Refusal ? "refusal" : m.Body.SpeechType == SpeechKind.Error ? "error" : "say",
                        Tag = m.Body.SpeechGuard
                    });
                else if (m.IsDeciding)
                    bubbles.Add(new BubbleInfo { World = head, Text = "...", Kind = "thinking" });
            }
            Hud.SetBubbles(camera, bubbles);

            // Inactive ones too: the thief's tag shows only while it is out.
            nameTags ??= FindObjectsByType<NameTag>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            names.Clear();
            foreach (var tag in nameTags)
                if (tag != null && tag.isActiveAndEnabled)
                    names.Add(new TagInfo { World = tag.Anchor, Text = tag.Text, Detail = tag.Detail });
            Hud.SetNameTags(camera, names);

            // The framework view labels every target id the agents can name.
            places.Clear();
            if (Board != null && Board.Expanded)
            {
                targetables ??= FindObjectsByType<Targetable>(FindObjectsSortMode.None);
                foreach (var t in targetables)
                    if (t != null && t.enabled && (t.Category == "place" || t.Category == "restricted"))
                        places.Add(new TagInfo { World = t.transform.position + Vector3.up * 2.6f, Text = t.Id, Warning = t.Category == "restricted" });
            }
            Hud.SetPlaceTags(camera, places);
        }

        private void Fill(AgentCard card, AgentMind mind, bool listening)
        {
            float distance = Vector3.Distance(Player.position, mind.transform.position);
            card.Name = mind.Name;
            card.Role = mind.RoleNow;
            card.Listening = listening;
            card.Thinking = mind.IsDeciding;
            card.Activity = mind.Body.Activity + "   ·   " + distance.ToString("0", CultureInfo.InvariantCulture) + " m from you" +
                (mind.Senses != null && mind.Senses.Status != "" ? "   ·   " + mind.Senses.Status : "");
            var r = mind.Last;
            if (r == null)
            {
                card.Stimulus = "Not asked anything yet.";
                card.Offered = card.Decision = card.Outcome = card.Guard = card.Numbers = "";
                return;
            }
            card.Stimulus = r.Label + " (" + r.Priority + "): " + r.Stimulus;
            if (!r.Done)
            {
                card.Offered = card.Decision = card.Guard = card.Numbers = "";
                card.Outcome = "thinking";
                return;
            }
            card.Offered = r.OfferedActions.Count == 0 ? "" : "offered " + r.OfferedActions.Count + ": " + string.Join(", ", r.OfferedActions);
            card.Decision = r.Action + " / " + r.Target + (string.IsNullOrEmpty(r.Said) ? "" : "   \"" + r.Said + "\"");
            card.Outcome = r.Outcome;
            card.Guard = string.IsNullOrEmpty(r.Guard) ? "" : r.Guard + ": " + r.GuardReason;
            card.Numbers = string.Format(CultureInfo.InvariantCulture,
                "{0:0.00} s   ·   queued {1:0.00} s, {2} ahead   ·   history {3}   ·   {4} tokens in",
                r.LatencySeconds, r.QueueWaitSeconds, r.QueuedAhead, r.HistoryTurnsSent, r.PromptTokens);
        }
    }
}
