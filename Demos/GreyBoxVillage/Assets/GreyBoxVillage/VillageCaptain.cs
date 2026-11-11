using System.Collections.Generic;
using System.Linq;
using Agenerela;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// Captain Hale's body, before the lord's keep. He gives the wolf quest and sends a guard where he
    /// is told to: <c>send_guard</c> picks the nearest guard who is not busy, and that guard is then
    /// asked with the captain's own words, labelled Captain. One agent's decision becomes another's
    /// stimulus, through the game alone (DR-008).
    /// </summary>
    public sealed class VillageCaptain : VillageBody
    {
        private string activity;
        private Transform order;

        public override string Activity => activity ??= "standing watch before the lord's keep";
        public override bool IsBusy => false;

        private static readonly IReadOnlyList<string> SuggestedLines = System.Array.AsReadOnly(new[]
        { "Any work for me, captain?", "Send a guard to the market.", "I have the wolf's pelt.", "What's the news?" });

        public override IReadOnlyList<string> Suggestions => SuggestedLines;

        public override void ResetState()
        {
            activity = null;
            order = null;
        }

        [AgentAction("send_guard", "Send one of your guards to a place.", RequiresTarget = true, TargetCategories = new[] { "place", "restricted" })]
        [Example("There's trouble down at the {0}, send someone.", "docks", "well")]
        private void SendGuard(Transform place)
        {
            order = place;
            activity = "giving orders";
            Report("Sending a guard to the " + IdOf(place) + ".");
        }

        /// <summary>Passes an order on, with his words, to the nearest guard who is free.</summary>
        public override void AfterAction(string actionId, string statement)
        {
            if (actionId != "send_guard" || order == null) return;
            var place = order;
            order = null;
            var commands = FindFirstObjectByType<VillageCommands>();
            var guard = commands == null ? null : commands.Minds
                .Where(m => m != null && m.Body is VillageGuard g && !g.IsBusy && !m.IsDeciding)
                .OrderBy(m => Vector3.Distance(m.transform.position, place.position))
                .FirstOrDefault();
            if (guard == null) return;
            _ = guard.ReportEventAsync("captain", "Captain", OrderStimulus(IdOf(place), statement), DecisionPriority.Background);
        }

        /// <summary>What the guard Hale sends is told: the place, and his own words.</summary>
        public static string OrderStimulus(string placeId, string statement) =>
            "Captain Hale orders you to the " + placeId + "." +
            (string.IsNullOrWhiteSpace(statement) ? "" : " He says: \"" + statement.Trim() + "\"");
    }
}
