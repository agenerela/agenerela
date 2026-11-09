using System;

namespace LivingVale.Combat
{
    /// <summary>
    /// Numbers for one combatant, from the tuning table in living-vale-design.md §4.
    /// Plain data so the rules can be tested without a scene.
    /// </summary>
    [Serializable]
    public sealed class CombatTuning
    {
        public float MaxHealth = 100f;
        public float MaxStamina = 100f;

        /// <summary>Stamina per second, once <see cref="StaminaRegenDelay"/> has passed since the last spend.</summary>
        public float StaminaRegen = 22f;
        public float StaminaRegenDelay = 0.7f;

        /// <summary>Health per second, once <see cref="HealthRegenDelay"/> has passed since the last hit taken.</summary>
        public float HealthRegen = 3f;
        public float HealthRegenDelay = 6f;

        public float AttackDamage = 20f;
        public float AttackStaminaCost = 10f;

        /// <summary>A swing lasts this long; the attacker cannot start another, block or dodge during it.</summary>
        public float AttackDuration = 0.45f;

        public float DodgeStaminaCost = 25f;
        public float DodgeDuration = 0.45f;

        /// <summary>Hits that land within this time from the start of a dodge are ignored.</summary>
        public float DodgeInvulnerability = 0.3f;

        /// <summary>Fraction of damage a block removes.</summary>
        public float BlockDamageReduction = 0.75f;

        /// <summary>Stamina a block spends for each hit it absorbs. Without it the guard breaks.</summary>
        public float BlockStaminaCost = 15f;

        /// <summary>Hits that deal at least this much damage stagger. Zero makes every unblocked hit stagger.</summary>
        public float StaggerThreshold = 1f;
        public float StaggerDuration = 0.5f;

        public static CombatTuning Player() => new CombatTuning();

        public static CombatTuning Raider() => new CombatTuning
        {
            MaxHealth = 60f,
            AttackDamage = 14f,
            AttackDuration = 0.35f,
            HealthRegen = 0f,
            StaggerDuration = 0.6f
        };

        public static CombatTuning Wolf() => new CombatTuning
        {
            MaxHealth = 40f,
            AttackDamage = 10f,
            AttackDuration = 0.35f,
            HealthRegen = 0f,
            StaggerDuration = 0.45f
        };
    }
}
