using System.Collections.Generic;
using System.Globalization;
using Agenerela;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// The game's own rules for when a guard is asked something nobody typed (DR-008): the
    /// player in a restricted zone the guard can see, a wolf it can see, a thief it sees running,
    /// smoke from a fire it was not told about, and a quiet moment at its post. The framework
    /// classifies none of this; it only receives the stimulus and its label. Each guard has its own,
    /// so each notices with its own eyes and keeps its own timers.
    /// </summary>
    public sealed class GuardSenses : MonoBehaviour
    {
        public AgentMind Mind;
        public VillageGuard Guard;
        public Transform Player;
        public VillageWolf Wolf;
        public VillageThief Thief;
        public VillageFire Fire;

        [Header("Sight")]
        public float SightRange = 32f;
        public float EyeHeight = 1.6f;

        [Header("Restricted zones")]
        [Tooltip("Asks again if the player is still inside this long after the last report.")]
        public float ZoneReminderSeconds = 40f;

        [Header("Wolf")]
        public float WolfSightRange = 26f;
        public float WolfCooldownSeconds = 45f;

        [Header("Smoke")]
        [Tooltip("Smoke rises above the roofs, so it is seen from further off than a person.")]
        public float SmokeSightRange = 45f;

        [Header("Quiet moments")]
        public bool QuietTimerEnabled = true;
        [Tooltip("Seconds of nothing happening, with the guard standing still, before it is asked what to do.")]
        public float QuietSeconds = 30f;

        /// <summary>One line for the agent board: zone and timer state.</summary>
        public string Status { get; private set; } = "";

        private RestrictedZone[] zones = new RestrictedZone[0];
        private readonly Dictionary<RestrictedZone, float> reportedAt = new Dictionary<RestrictedZone, float>();
        private float wolfReportedAt = -999f;
        private int thiefReported, fireReported;

        private void OnEnable()
        {
            zones = FindObjectsByType<RestrictedZone>(FindObjectsSortMode.None);
            foreach (var zone in zones) zone.PlayerLeft += OnPlayerLeft;
        }

        private void OnDisable()
        {
            foreach (var zone in zones) if (zone != null) zone.PlayerLeft -= OnPlayerLeft;
        }

        /// <summary>
        /// Unblocked line of sight from the guard's eyes, within range. Ignore Raycast colliders, the
        /// shut gate's, block nothing: a guard sees through the bars.
        /// </summary>
        public bool CanSee(Transform target, float range = -1f)
        {
            if (target == null) return false;
            var eye = Guard.transform.position + Vector3.up * EyeHeight;
            var point = target.position + Vector3.up * 1f;
            if (Vector3.Distance(eye, point) > (range < 0 ? SightRange : range)) return false;
            if (!Physics.Linecast(eye, point, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform == target || hit.transform.IsChildOf(target) || hit.transform.IsChildOf(Guard.transform);
        }

        private void Update() => Sense();

        /// <summary>One look around: zones, the wolf, the thief, the smoke, the quiet timer. Public so tests can step it.</summary>
        public void Sense()
        {
            if (Mind == null || Guard == null || Player == null) return;
            WatchZones();
            WatchWolf();
            WatchThief();
            WatchFire();
            WatchQuiet();
        }

        /// <summary>Forgets every report, so the next look tells the guard afresh. Used between tests and by the presenter.</summary>
        public void ResetState()
        {
            reportedAt.Clear();
            wolfReportedAt = -999f;
            thiefReported = Thief != null ? Thief.Theft : 0;
            fireReported = Fire != null ? Fire.Number : 0;
        }

        /// <summary>Lets the next sighting of the wolf report at once, whatever its cooldown. The presenter's F2.</summary>
        public void ForgetWolf() => wolfReportedAt = -999f;

        /// <summary>
        /// Marks this fire as known, so the senses do not report it again: <see cref="VillageEvents"/>
        /// already told this guard when it started.
        /// </summary>
        public void KnowFire(int number) => fireReported = Mathf.Max(fireReported, number);

        // Entering is not enough: the guard asks only once it can see the player inside.
        private void WatchZones()
        {
            string inside = null;
            foreach (var zone in zones)
            {
                if (!zone.PlayerInside) continue;
                bool seen = CanSee(Player);
                inside = "you are in " + zone.Id + (seen ? ", seen" : ", unseen");
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
            // A wolf running back to its den after being driven off is no news.
            if (Wolf == null || Wolf.IsHidden || Wolf.IsFleeing || Time.time - wolfReportedAt < WolfCooldownSeconds) return;
            if (!CanSee(Wolf.transform, WolfSightRange)) return;
            wolfReportedAt = Time.time;
            float d = Vector3.Distance(Wolf.transform.position, Guard.transform.position);
            Raise("wolf", "Event", "A wolf has come out of the woods, " + d.ToString("0", CultureInfo.InvariantCulture) + " m from you.",
                DecisionPriority.Background);
        }

        // A thief running with stolen goods, once per theft, the first time this guard sees it.
        private void WatchThief()
        {
            if (Thief == null || !Thief.IsRunning || Thief.Theft == thiefReported || !CanSee(Thief.transform)) return;
            thiefReported = Thief.Theft;
            float d = Vector3.Distance(Thief.transform.position, Guard.transform.position);
            string ware = Thief.Loot != null ? Thief.Loot.Id : "goods";
            Raise("thief", "Event", "A thief is running for the eastern_road with Odile's " + ware + ", " +
                d.ToString("0", CultureInfo.InvariantCulture) + " m from you.", DecisionPriority.Background);
        }

        // Smoke from a fire this guard was not told about when it started: once per fire.
        private void WatchFire()
        {
            if (Fire == null || !Fire.IsBurning || Fire.Number == fireReported) return;
            if (!CanSee(Fire.Smoke != null ? Fire.Smoke : Fire.transform, SmokeSightRange)) return;
            fireReported = Fire.Number;
            Raise("fire", "Event", Fire.Stimulus, DecisionPriority.Background);
        }

        // Nothing has happened and the guard is standing still: ask what it does with the time.
        // The model may choose none, or patrol, or anything else it is offered.
        private void WatchQuiet()
        {
            if (!QuietTimerEnabled || Mind.IsDeciding || Mind.HasPending || Guard.IsBusy) return;
            if (Time.time - Mind.LastActivityTime < QuietSeconds) return;
            Raise("quiet", "Time", "Time passes quietly at your post.", DecisionPriority.Background);
        }

        private string QuietStatus()
        {
            if (!QuietTimerEnabled) return "quiet timer off";
            if (Guard.IsBusy) return "quiet timer waits while busy";
            float left = QuietSeconds - (Time.time - Mind.LastActivityTime);
            return "quiet moment in " + Mathf.Max(0, left).ToString("0", CultureInfo.InvariantCulture) + " s";
        }

        private async void Raise(string source, string label, string text, DecisionPriority priority)
        {
            await Mind.ReportEventAsync(source, label, text, priority);
        }
    }
}
