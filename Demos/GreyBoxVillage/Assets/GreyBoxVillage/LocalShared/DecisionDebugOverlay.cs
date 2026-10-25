using System.Collections.Generic;
using UnityEngine;

namespace Demos.Shared
{
    /// <summary>Temporary implementation of the Show contract from #14. Framework-free: it takes strings.</summary>
    public sealed class DecisionDebugOverlay : MonoBehaviour
    {
        public string ActionId { get; private set; } = "none";
        public string TargetId { get; private set; } = "no_target";
        public string Message { get; private set; } = "Walk up to the guard, press Enter and say anything.";
        public string Stimulus { get; private set; } = "Player: (waiting)";
        public string Statement { get; private set; } = "";
        public string Details { get; private set; } = "";
        public string Provider { get; private set; } = "none";
        private float latency = -1;
        private string guards = "not run (no framework)";

        public void Show(string actionId, string targetId, float latencySeconds,
            IReadOnlyList<string> guardsFired)
        {
            ActionId = actionId;
            TargetId = targetId;
            latency = latencySeconds;
            guards = guardsFired == null ? "not run (no framework)" :
                guardsFired.Count == 0 ? "none" : string.Join(", ", guardsFired);
        }

        public void ShowPlaceholder(string stimulus, string actionId, string targetId, string message)
        {
            Stimulus = stimulus;
            Message = message;
            Statement = "";
            Details = "";
            Show(actionId, targetId, -1, null);
        }

        public void ShowNote(string message) => Message = message;

        public void ShowThinking(string stimulus, string provider)
        {
            Stimulus = stimulus;
            Provider = provider;
            Statement = "";
            Details = "";
            Message = "The guard is thinking...";
            Show("...", "...", -1, null);
        }

        public void ShowDecision(string stimulus, string actionId, string targetId, string statement,
            string details, float latencySeconds, IReadOnlyList<string> guardsFired, string provider)
        {
            Stimulus = stimulus;
            Statement = statement ?? "";
            Details = details ?? "";
            Provider = provider;
            Message = string.IsNullOrEmpty(statement) ? "" : "Guard: \"" + statement + "\"";
            Show(actionId, targetId, latencySeconds, guardsFired);
        }

        public string LatencyLabel => latency < 0 ? "N/A" : latency.ToString("0.000") + " s";
        public string GuardsLabel => guards;
    }
}
