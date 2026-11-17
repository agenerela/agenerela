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
        [Tooltip("Swings while the bell rings; the bell hangs from it.")]
        public Transform BellPivot;
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

        private Quaternion restRotation;

        private void Start()
        {
            if (Bell != null) restColor = Bell.material.color;
            if (BellPivot != null) restRotation = BellPivot.localRotation;
        }

        private void Update() => Tick();

        /// <summary>
        /// One frame of the bell: it swings and flashes while it rings, and the villagers are told
        /// the all-clear on the first frame after it stops. Public so the EditMode tests can step it.
        /// </summary>
        public void Tick()
        {
            bool sounding = IsSounding;
            // Only in Play mode: Start has not taken the bell's rest pose in Edit mode, and
            // renderer.material there would leak a copy into the scene.
            if (Application.isPlaying)
            {
                if (Bell != null && (sounding || wasSounding))
                    Bell.material.color = sounding && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? new Color(1f, 0.45f, 0.4f) : restColor;
                if (BellPivot != null)
                    BellPivot.localRotation = restRotation * Quaternion.Euler(sounding ? Mathf.Sin(Time.time * 7f) * 28f : 0f, 0, 0);
            }
            if (wasSounding && !sounding)
                foreach (var villager in Villagers)
                    if (villager != null) villager.AllClear();
            wasSounding = sounding;
        }
    }
}
