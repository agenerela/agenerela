using System.Collections.Generic;
using UnityEngine;

namespace Demos.Shared
{
    /// <summary>Temporary implementation of the Show contract from #14.</summary>
    public sealed class DecisionDebugOverlay : MonoBehaviour
    {
        public string ActionId { get; private set; } = "none";
        public string TargetId { get; private set; } = "no_target";
        public string Message { get; private set; } = "Walk up to the guard, press Enter and type a command.";
        public string Stimulus { get; private set; } = "(waiting)";
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
            Show(actionId, targetId, -1, null);
        }

        public string LatencyLabel => latency < 0 ? "N/A" : latency.ToString("0.000") + " s";
        public string GuardsLabel => guards;
    }
}
