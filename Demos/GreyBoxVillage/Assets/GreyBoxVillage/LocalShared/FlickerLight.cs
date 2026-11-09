using UnityEngine;

namespace Demos.Shared
{
    /// <summary>Makes a point light flicker like a flame, around the intensity it started with.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class FlickerLight : MonoBehaviour
    {
        [Range(0f, 1f)] public float Amount = 0.25f;
        public float Speed = 6f;
        [Tooltip("When set, the light is on only while this is true, and off otherwise.")]
        public bool OnlyAtNight;

        /// <summary>Set by the day cycle: how dark it is, 0 at noon to 1 at midnight.</summary>
        public float Darkness { get; set; }

        private Light lamp;
        private float baseIntensity, seed;

        private void Awake()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float flicker = 1f - Amount + Amount * 2f * Mathf.PerlinNoise(seed, Time.time * Speed);
            float visible = OnlyAtNight ? Mathf.Clamp01((Darkness - 0.35f) * 3f) : 1f;
            lamp.intensity = baseIntensity * flicker * visible;
            lamp.enabled = lamp.intensity > 0.01f;
        }
    }
}
