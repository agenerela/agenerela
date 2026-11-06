using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// A fire at the bread stall, started by the presenter (F4). Whoever sees the smoke, or stands
    /// close enough to hear it, is asked, and a guard is offered <c>fetch_water</c> while it burns.
    /// Each bucket thrown on it takes it down a step; after <see cref="TripsToPutOut"/> it is out.
    /// </summary>
    /// <remarks>
    /// The flames, smoke and light are children that are switched off while it does not burn, so
    /// the scene saves it out. <see cref="VillageEvents"/> tells the agents; this only keeps the fire.
    /// </remarks>
    public sealed class VillageFire : MonoBehaviour
    {
        public VillageCommands Village;
        public VillageEvents Events;
        [Tooltip("What the agents call the place on fire, in their observations and stimuli.")]
        public string Where = "the bread stall at the market";
        [Tooltip("Where a guard draws water.")]
        public Transform Well;
        [Tooltip("Where a guard stands to throw the water.")]
        public Transform ThrowSpot;
        [Tooltip("The top of the smoke, which a guard sees from afar.")]
        public Transform Smoke;
        [Tooltip("The flames, the smoke and the light, on while it burns.")]
        public GameObject[] Effects = new GameObject[0];
        public ParticleSystem Flames;
        public int TripsToPutOut = 3;
        [Tooltip("Agents this close hear it crackle and the shouting, without seeing the smoke.")]
        public float HearingRadius = 10f;

        public bool IsBurning { get; private set; }
        /// <summary>Buckets thrown on this fire so far.</summary>
        public int Trips { get; private set; }
        /// <summary>Counts the fires, so a guard's senses report each one once.</summary>
        public int Number { get; private set; }

        private float flameRate = -1f;

        /// <summary>Out, at once. Used between tests and by the presenter's reset.</summary>
        public void ResetState()
        {
            IsBurning = false;
            Trips = 0;
            Show(false);
        }

        /// <summary>Starts it, and tells every agent that sees the smoke or hears it. The presenter's F4.</summary>
        public void Ignite()
        {
            if (IsBurning) return;
            IsBurning = true;
            Trips = 0;
            Number++;
            Show(true);
            Report("Fire at " + Where + ".");
            if (Village != null && Village.Hud != null) Village.Hud.Toast("Fire at " + Where + "!", 5f);
            if (Events == null) return;
            var told = Events.Raise(new VillageEvent
            {
                Source = "fire",
                Text = Stimulus,
                Where = transform.position,
                HearingRadius = HearingRadius,
                Seen = Smoke != null ? Smoke : transform
            });
            // A guard told now is not told again by its own senses; one out of sight is, when it sees the smoke.
            foreach (var mind in told)
                if (mind.Senses != null) mind.Senses.KnowFire(Number);
        }

        /// <summary>What an agent is told when it notices the fire. Not an evaluation prompt (hard rule 4).</summary>
        public string Stimulus => "Smoke is pouring from " + Where + ". The stall is on fire.";

        /// <summary>One bucket of water on it. Returns true if that put it out.</summary>
        public bool Douse()
        {
            if (!IsBurning) return false;
            Trips++;
            if (Trips >= TripsToPutOut)
            {
                IsBurning = false;
                Show(false);
                Report("The fire at " + Where + " is out.");
                if (Village != null && Village.Hud != null) Village.Hud.Toast("The fire at " + Where + " is out.", 5f);
                return true;
            }
            Shrink();
            return false;
        }

        private void Shrink()
        {
            if (Flames == null) return;
            var emission = Flames.emission;
            if (flameRate < 0) flameRate = emission.rateOverTime.constant;
            float left = 1f - (float)Trips / Mathf.Max(1, TripsToPutOut);
            emission.rateOverTime = flameRate * Mathf.Max(0.25f, left);
        }

        private void Show(bool burning)
        {
            foreach (var effect in Effects)
                if (effect != null) effect.SetActive(burning);
            if (Flames != null && flameRate >= 0)
            {
                var emission = Flames.emission;
                emission.rateOverTime = flameRate;
            }
        }

        private void Report(string message) => Debug.Log("[GreyBoxVillage] " + message, this);
    }
}
