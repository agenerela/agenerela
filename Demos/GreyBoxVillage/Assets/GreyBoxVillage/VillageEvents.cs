using System.Collections.Generic;
using Agenerela;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>Something that happened in the village, and how an agent could notice it.</summary>
    public sealed class VillageEvent
    {
        public string Source = "event";
        public string Label = "Event";
        public string Text;
        public DecisionPriority Priority = DecisionPriority.Background;
        /// <summary>Where it happened, for hearing.</summary>
        public Vector3 Where;
        /// <summary>Agents within this distance of <see cref="Where"/> hear it; 0 means it makes no sound.</summary>
        public float HearingRadius;
        /// <summary>Agents that can see this are told; null means there is nothing to see.</summary>
        public Transform Seen;
        /// <summary>The body that caused it, which is never told about its own doing; null for none.</summary>
        public VillageBody Cause;
    }

    /// <summary>
    /// Tells each agent about an event only if it hears or sees it. The game decides who reacts
    /// (DR-008); the framework sees only the stimulus each agent is then given. One event can so
    /// become several decisions, which wait their turn in the provider's shared queue.
    /// </summary>
    /// <remarks>
    /// A guard's own senses watch what only it cares about (a trespass, the wolf, its quiet timer).
    /// This is for what everyone in range notices: the bell, and the smoke of a fire.
    ///
    /// <c>[ExecuteAlways]</c> so that <c>OnEnable</c> subscribes to the bell in Edit mode too, where the
    /// EditMode tests ring it; this component has no Update for it to run.
    /// </remarks>
    [ExecuteAlways]
    public sealed class VillageEvents : MonoBehaviour
    {
        public VillageCommands Village;
        public VillageAlarm Alarm;
        public float BellHearingRadius = 90f;

        private void OnEnable()
        {
            if (Alarm != null) Alarm.Rang += OnRang;
        }

        private void OnDisable()
        {
            if (Alarm != null) Alarm.Rang -= OnRang;
        }

        /// <summary>What everyone in earshot is told when the bell rings; <paramref name="ringer"/> null for nobody known.</summary>
        public static string BellStimulus(string ringer) =>
            "The alarm bell is ringing" + (!string.IsNullOrEmpty(ringer) ? ". " + ringer + " rang it." : ".");

        private void OnRang(VillageBody ringer)
        {
            var who = ringer != null ? ringer.GetComponent<AgentMind>() : null;
            Raise(new VillageEvent
            {
                Source = "bell",
                Text = BellStimulus(who != null ? who.Name : null),
                Where = Alarm.transform.position,
                HearingRadius = BellHearingRadius,
                Cause = ringer
            });
        }

        /// <summary>Asks every agent that hears or sees the event, and returns them. Their decisions run on.</summary>
        public List<AgentMind> Raise(VillageEvent e)
        {
            var told = new List<AgentMind>();
            foreach (var mind in Village.Minds)
            {
                if (mind == null || (e.Cause != null && mind.Body == e.Cause) || !Notices(mind, e)) continue;
                told.Add(mind);
                Tell(mind, e);
            }
            return told;
        }

        private static bool Notices(AgentMind mind, VillageEvent e) =>
            e.HearingRadius > 0 && Vector3.Distance(mind.transform.position, e.Where) <= e.HearingRadius ||
            e.Seen != null && mind.Senses != null && mind.Senses.CanSee(e.Seen);

        private static async void Tell(AgentMind mind, VillageEvent e) =>
            await mind.ReportEventAsync(e.Source, e.Label, e.Text, e.Priority);
    }
}
