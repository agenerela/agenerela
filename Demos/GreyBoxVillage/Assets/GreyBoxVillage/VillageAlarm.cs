using System;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// The bell in the guards' camp. While it rings, every villager runs to the shelter, and
    /// <see cref="VillageEvents"/> tells every agent in earshot.
    /// </summary>
    public sealed class VillageAlarm : MonoBehaviour
    {
        public Renderer Bell;
        public Villager[] Villagers;
        public Transform Shelter;
        public float RingSeconds = 30f;

        public bool IsSounding => Time.time < stopAt;

        /// <summary>Raised when the bell starts ringing, with whoever rang it; null when no agent did.</summary>
        public event Action<VillageBody> Rang;

        private float stopAt = -1f;
        private Color restColor;
        private bool wasSounding;

        public void Ring(VillageBody ringer = null)
        {
            stopAt = Time.time + RingSeconds;
            foreach (var villager in Villagers)
                if (villager != null) villager.TakeCover(Shelter);
            Debug.Log("[GreyBoxVillage] The alarm bell is ringing.", this);
            Rang?.Invoke(ringer);
        }

        /// <summary>Stops the bell at once. Used between tests.</summary>
        public void Silence() => stopAt = -1f;

        private void Start()
        {
            if (Bell != null) restColor = Bell.material.color;
        }

        private void Update()
        {
            bool sounding = IsSounding;
            if (Bell != null && (sounding || wasSounding))
                Bell.material.color = sounding && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? Color.red : restColor;
            if (wasSounding && !sounding)
                foreach (var villager in Villagers)
                    if (villager != null) villager.AllClear();
            wasSounding = sounding;
        }
    }
}
