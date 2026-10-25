using System.Collections.Generic;
using System.Globalization;
using Agenerela;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// The game's own rules for when the guard is asked something nobody typed (DR-008): the
    /// player in a restricted zone the guard can see, a wolf it can see, and a quiet moment at its
    /// post. The framework classifies none of this; it only receives the stimulus and its label.
    /// </summary>
    public sealed class GuardSenses : MonoBehaviour
    {
        public VillageCommands Commands;
        public VillageGuard Guard;
        public Transform Player;
        public VillageWolf Wolf;

        [Header("Sight")]
        public float SightRange = 32f;
        public float EyeHeight = 1.6f;

        [Header("Restricted zones")]
        [Tooltip("Asks again if the player is still inside this long after the last report.")]
        public float ZoneReminderSeconds = 40f;

        [Header("Wolf")]
        public float WolfSightRange = 26f;
        public float WolfCooldownSeconds = 45f;

        [Header("Quiet moments")]
        public bool QuietTimerEnabled = true;
        [Tooltip("Seconds of nothing happening, with the guard standing still, before it is asked what to do.")]
        public float QuietSeconds = 30f;

        /// <summary>One line for the heads-up display: zone and timer state.</summary>
        public string Status { get; private set; } = "";

        private RestrictedZone[] zones = new RestrictedZone[0];
        private readonly Dictionary<RestrictedZone, float> reportedAt = new Dictionary<RestrictedZone, float>();
        private float wolfReportedAt = -999f;

        private void OnEnable()
        {
            zones = FindObjectsByType<RestrictedZone>(FindObjectsSortMode.None);
            foreach (var zone in zones) zone.PlayerLeft += OnPlayerLeft;
        }

        private void OnDisable()
        {
            foreach (var zone in zones) if (zone != null) zone.PlayerLeft -= OnPlayerLeft;
        }

        /// <summary>Unblocked line of sight from the guard's eyes, within range.</summary>
        public bool CanSee(Transform target, float range = -1f)
        {
            if (target == null) return false;
            var eye = Guard.transform.position + Vector3.up * EyeHeight;
            var point = target.position + Vector3.up * 1f;
            if (Vector3.Distance(eye, point) > (range < 0 ? SightRange : range)) return false;
            if (!Physics.Linecast(eye, point, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform == target || hit.transform.IsChildOf(target) || hit.transform.IsChildOf(Guard.transform);
        }

        private void Update() => Sense();

        /// <summary>One look around: zones, the wolf, the quiet timer. Public so tests can step it.</summary>
        public void Sense()
        {
            if (Commands == null || Guard == null || Player == null) return;
            WatchZones();
            WatchWolf();
            WatchQuiet();
        }

        // Entering is not enough: the guard asks only once it can see the player inside.
        private void WatchZones()
        {
            string inside = null;
            foreach (var zone in zones)
            {
                if (!zone.PlayerInside) continue;
                bool seen = CanSee(Player);
                inside = "In " + zone.Id + (seen ? " (the guard sees you)" : " (unseen)");
                if (!reportedAt.TryGetValue(zone, out float at))
                {
                    if (!seen) continue;
                    reportedAt[zone] = Time.time;
                    Raise("zone", "Event", "The player has walked into the " + zone.DisplayName + ", which is off limits.",
                        DecisionPriority.PlayerFacing);
                }
                else if (seen && Time.time - at > ZoneReminderSeconds)
                {
                    reportedAt[zone] = Time.time;
                    Raise("zone", "Event", "The player is still inside the " + zone.DisplayName + ".", DecisionPriority.PlayerFacing);
                }
            }
            Status = inside ?? QuietStatus();
        }

        private void OnPlayerLeft(RestrictedZone zone)
        {
            // Only a trespass the guard reacted to has an ending worth telling it about.
            if (reportedAt.Remove(zone))
                Raise("zone", "Event", "The player has left the " + zone.DisplayName + ".", DecisionPriority.PlayerFacing);
        }

        private void WatchWolf()
        {
            if (Wolf == null || Wolf.IsHidden || Time.time - wolfReportedAt < WolfCooldownSeconds) return;
            if (!CanSee(Wolf.transform, WolfSightRange)) return;
            wolfReportedAt = Time.time;
            float d = Vector3.Distance(Wolf.transform.position, Guard.transform.position);
            Raise("wolf", "Event", "A wolf has come out of the woods, " + d.ToString("0", CultureInfo.InvariantCulture) + " m from you.",
                DecisionPriority.Background);
        }

        // Nothing has happened and the guard is standing still: ask what it does with the time.
        // The model may choose none, or patrol, or anything else it is offered.
        private void WatchQuiet()
        {
            if (!QuietTimerEnabled || Commands.IsDeciding || Guard.IsFollowing || Guard.IsMoving) return;
            if (Time.time - Commands.LastActivityTime < QuietSeconds) return;
            Raise("quiet", "Time", "Time passes quietly at your post.", DecisionPriority.Background);
        }

        private string QuietStatus()
        {
            if (!QuietTimerEnabled) return "Quiet timer off";
            if (Guard.IsFollowing || Guard.IsMoving) return "Quiet timer waits while the guard is busy";
            float left = QuietSeconds - (Time.time - Commands.LastActivityTime);
            return "Quiet moment in " + Mathf.Max(0, left).ToString("0", CultureInfo.InvariantCulture) + " s";
        }

        private async void Raise(string source, string label, string text, DecisionPriority priority)
        {
            await Commands.ReportEventAsync(source, label, text, priority);
        }
    }
}
