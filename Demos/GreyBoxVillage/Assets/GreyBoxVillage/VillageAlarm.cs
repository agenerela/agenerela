using UnityEngine;
using UnityEngine.AI;

namespace Demos.GreyBoxVillage
{
    /// <summary>The bell in the guards' camp. While it rings, every villager runs to the shelter.</summary>
    public sealed class VillageAlarm : MonoBehaviour
    {
        public Renderer Bell;
        public Villager[] Villagers;
        public Transform Shelter;
        public float RingSeconds = 30f;

        public bool IsSounding => Time.time < stopAt;

        private float stopAt = -1f;
        private Color restColor;
        private bool wasSounding;

        public void Ring()
        {
            stopAt = Time.time + RingSeconds;
            foreach (var villager in Villagers)
                if (villager != null) villager.TakeCover(Shelter);
            Debug.Log("[GreyBoxVillage] The alarm bell is ringing.", this);
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
