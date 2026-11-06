using System;
using UnityEngine;

namespace LivingVale.Combat
{
    /// <summary>
    /// The scene side of a <see cref="Combatant"/>: ticks its regeneration, flashes its renderers
    /// on a hit and shakes the visual while staggered. Player and enemies both carry one.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CombatBody : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private string combatantId = "combatant";
        [SerializeField] private EnemyArchetype tuningPreset = EnemyArchetype.Raider;
        [SerializeField] private bool isPlayer;
        [SerializeField] private Transform visual;
        [SerializeField] private Renderer[] flashRenderers = Array.Empty<Renderer>();
        [SerializeField, Min(0f)] private float flashSeconds = 0.12f;
        [SerializeField, Min(0f)] private float overheadHeight = 2.4f;

        // A warm red, not white: the art kit's palette material is already white, so a flash toward
        // white would not show on it (G7b).
        private static readonly Color FlashTint = new Color(1f, 0.45f, 0.4f);

        private MaterialPropertyBlock block;
        private Color[] baseColors = Array.Empty<Color>();
        private float flashUntil = float.NegativeInfinity;
        private Vector3 visualRest;

        public Combatant Combatant { get; private set; }
        public bool IsPlayer => isPlayer;
        public Transform Visual => visual;

        /// <summary>Where an overhead health bar sits, in world space.</summary>
        public Vector3 OverheadPoint => transform.position + Vector3.up * overheadHeight;

        public void Configure(string id, bool player, EnemyArchetype preset, Transform bodyVisual,
            Renderer[] renderers, float barHeight)
        {
            combatantId = id;
            isPlayer = player;
            tuningPreset = preset;
            visual = bodyVisual;
            flashRenderers = renderers ?? Array.Empty<Renderer>();
            overheadHeight = barHeight;
        }

        private void Awake()
        {
            CombatTuning tuning = isPlayer ? CombatTuning.Player()
                : tuningPreset == EnemyArchetype.Wolf ? CombatTuning.Wolf() : CombatTuning.Raider();
            Combatant = new Combatant(combatantId, tuning);
            Combatant.Damaged += OnDamaged;
            block = new MaterialPropertyBlock();
            baseColors = new Color[flashRenderers.Length];
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                Material material = flashRenderers[i] != null ? flashRenderers[i].sharedMaterial : null;
                baseColors[i] = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
            }

            if (visual != null)
            {
                visualRest = visual.localPosition;
            }
        }

        private void Update()
        {
            float now = Time.time;
            Combatant.Tick(now, Time.deltaTime);
            float flash = Mathf.Clamp01((flashUntil - now) / Mathf.Max(0.0001f, flashSeconds));
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                if (flashRenderers[i] == null)
                {
                    continue;
                }

                flashRenderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(baseColors[i], FlashTint, flash));
                flashRenderers[i].SetPropertyBlock(block);
            }

            if (visual != null)
            {
                // A small shake reads as stagger without an animation rig.
                visual.localPosition = Combatant.IsStaggered(now) && !Combatant.IsDefeated
                    ? visualRest + new Vector3(Mathf.Sin(now * 70f) * 0.06f, 0f, Mathf.Cos(now * 55f) * 0.04f)
                    : visualRest;
            }
        }

        private void OnDamaged(Combatant _, HitResult hit)
        {
            if (hit.Damage > 0f)
            {
                flashUntil = Time.time + flashSeconds;
            }
        }

        private void OnDestroy()
        {
            if (Combatant != null)
            {
                Combatant.Damaged -= OnDamaged;
            }
        }
    }

    /// <summary>Melee reach as plain geometry on the ground plane, shared by the player and enemies.</summary>
    public static class MeleeReach
    {
        public static bool InArc(Vector3 attackerPosition, Vector3 attackerForward, Vector3 victimPosition,
            float range, float halfAngleDegrees)
        {
            Vector3 offset = victimPosition - attackerPosition;
            offset.y = 0f;
            if (offset.sqrMagnitude > range * range)
            {
                return false;
            }

            if (offset.sqrMagnitude < 0.01f)
            {
                return true;
            }

            Vector3 forward = attackerForward;
            forward.y = 0f;
            return Vector3.Angle(forward, offset) <= halfAngleDegrees;
        }
    }
}
