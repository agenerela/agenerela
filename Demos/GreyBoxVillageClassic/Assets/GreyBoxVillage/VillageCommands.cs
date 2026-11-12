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
        public Transform Player;
        public AgentMind[] Minds = new AgentMind[0];
        public float SpeakingDistance = 6f;
        [Tooltip("Append every decision to Logs/agenerela-decisions-<time>.jsonl.")]
        public bool WriteLogFile = true;

        private DecisionRecorder recorder;
        private bool writeLog = true;
        private readonly List<AgentCard> cards = new List<AgentCard>();

        /// <summary>Every decision this session, from every agent. Made on first use.</summary>
        public DecisionRecorder Recorder =>
            recorder ??= new DecisionRecorder(WriteLogFile && writeLog ? DecisionRecorder.DefaultFilePath() : null);

        public bool IsDeciding => Minds.Any(m => m != null && m.IsDeciding);

        private void OnEnable() => Input.Submitted += OnSubmitted;
        private void OnDisable() => Input.Submitted -= OnSubmitted;

        /// <summary>
        /// Gives every agent this provider config, so fresh agents with empty memories, and starts a
        /// new recorder. Tests pass a config whose provider is a fake.
        /// </summary>
        public void UseProvider(ProviderConfig config, bool writeLogFile = false)
        {
            writeLog = writeLogFile;
            recorder = null;
            foreach (var mind in Minds) mind.UseProvider(config);
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
            if (string.IsNullOrWhiteSpace(text)) return;
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

        // ---- The board -----------------------------------------------------------------------

        private void Update()
        {
            if (Board == null) return;
            var listener = Nearest();
            while (cards.Count < Minds.Length) cards.Add(new AgentCard());
            for (int i = 0; i < Minds.Length; i++) Fill(cards[i], Minds[i], Minds[i] == listener);
            string speak = listener != null ? "Enter: speak to " + listener.Name
                : "Walk within " + SpeakingDistance.ToString("0", CultureInfo.InvariantCulture) + " m of someone to speak";
            Board.Show("GREYBOX VILLAGE", ProviderLabel + "   |   " + speak + "   |   H: request history", cards);
        }

        private void Fill(AgentCard card, AgentMind mind, bool listening)
        {
            float distance = Vector3.Distance(Player.position, mind.transform.position);
            card.Name = mind.Name;
            card.Role = mind.Role;
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
