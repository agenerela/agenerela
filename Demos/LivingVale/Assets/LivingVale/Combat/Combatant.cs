using System;
using System.Collections.Generic;

namespace LivingVale.Combat
{
    public enum HitOutcome
    {
        /// <summary>Nothing happened; <see cref="HitResult.IgnoredBecause"/> says why.</summary>
        Ignored,
        Hit,
        Blocked,
        /// <summary>The victim was blocking without the stamina to absorb the hit: full damage and a stagger.</summary>
        GuardBroken
    }

    public enum IgnoreReason
    {
        None,
        AlreadyStruckBySwing,
        Invulnerable,
        AlreadyDefeated,
        OwnSwing,
        SwingOver
    }

    public readonly struct HitResult
    {
        public HitOutcome Outcome { get; }
        public IgnoreReason IgnoredBecause { get; }
        public float Damage { get; }
        public bool Staggered { get; }
        public bool Defeated { get; }
        public string AttackerId { get; }

        public HitResult(HitOutcome outcome, IgnoreReason ignoredBecause, float damage, bool staggered,
            bool defeated, string attackerId)
        {
            Outcome = outcome;
            IgnoredBecause = ignoredBecause;
            Damage = damage;
            Staggered = staggered;
            Defeated = defeated;
            AttackerId = attackerId;
        }

        public bool Landed => Outcome != HitOutcome.Ignored;

        public override string ToString() =>
            Landed ? $"{Outcome} {Damage:0.#}{(Staggered ? " stagger" : "")}{(Defeated ? " defeat" : "")}"
                : $"Ignored ({IgnoredBecause})";
    }

    /// <summary>
    /// One attack. It remembers whom it struck, so a blade that overlaps a victim for several
    /// frames still lands at most one hit per swing per victim.
    /// </summary>
    public sealed class AttackSwing
    {
        private readonly HashSet<Combatant> struck = new HashSet<Combatant>();

        internal AttackSwing(long id, Combatant attacker, float startedAt, float endsAt, float damage)
        {
            Id = id;
            Attacker = attacker;
            StartedAt = startedAt;
            EndsAt = endsAt;
            Damage = damage;
        }

        public long Id { get; }
        public Combatant Attacker { get; }
        public float StartedAt { get; }
        public float EndsAt { get; private set; }
        public float Damage { get; }
        public bool Cancelled { get; private set; }

        public bool IsActive(float now) => !Cancelled && now < EndsAt;

        /// <summary>0 at the start of the swing, 1 at its end; for visuals and hit windows.</summary>
        public float Progress(float now) => EndsAt <= StartedAt ? 1f
            : Math.Min(1f, Math.Max(0f, (now - StartedAt) / (EndsAt - StartedAt)));

        public bool HasStruck(Combatant victim) => struck.Contains(victim);

        internal bool MarkStruck(Combatant victim) => struck.Add(victim);

        internal void Cancel(float now)
        {
            Cancelled = true;
            EndsAt = Math.Min(EndsAt, now);
        }
    }

    /// <summary>
    /// Health, stamina and the action rules for one fighter, player or enemy. Plain C#: every
    /// method takes the current time in seconds, so tests drive it without a scene or a clock.
    /// Combat is deterministic game code; no model is asked about hit timing (design §4).
    /// </summary>
    public sealed class Combatant
    {
        private static long nextSwingId;

        private float lastStaminaSpend = float.NegativeInfinity;
        private float lastHitTaken = float.NegativeInfinity;
        private float dodgeStartedAt = float.NegativeInfinity;
        private float staggeredUntil = float.NegativeInfinity;

        public Combatant(string id, CombatTuning tuning)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Health = tuning.MaxHealth;
            Stamina = tuning.MaxStamina;
        }

        public string Id { get; }
        public CombatTuning Tuning { get; }
        public float Health { get; private set; }
        public float Stamina { get; private set; }
        public float MaxHealth => Tuning.MaxHealth;
        public float MaxStamina => Tuning.MaxStamina;
        public bool IsDefeated { get; private set; }
        public bool IsBlocking { get; private set; }

        /// <summary>The swing in progress, or null. A finished or cancelled swing is not returned.</summary>
        public AttackSwing ActiveSwing { get; private set; }

        /// <summary>Raised for every hit that landed, after health and state changed.</summary>
        public event Action<Combatant, HitResult> Damaged;

        /// <summary>Raised once when health reaches zero.</summary>
        public event Action<Combatant> Defeated;

        public bool IsAttacking(float now) => ActiveSwing != null && ActiveSwing.IsActive(now);
        public bool IsDodging(float now) => now - dodgeStartedAt < Tuning.DodgeDuration;
        public bool IsInvulnerable(float now) => now - dodgeStartedAt < Tuning.DodgeInvulnerability;
        public bool IsStaggered(float now) => now < staggeredUntil;

        /// <summary>Free to start an attack, a block or a dodge, ignoring stamina.</summary>
        public bool CanAct(float now) => !IsDefeated && !IsStaggered(now) && !IsAttacking(now) && !IsDodging(now);

        /// <summary>Regenerates stamina and health. Call once per frame.</summary>
        public void Tick(float now, float deltaTime)
        {
            if (ActiveSwing != null && !ActiveSwing.IsActive(now))
            {
                ActiveSwing = null;
            }

            if (IsDefeated || deltaTime <= 0f)
            {
                return;
            }

            // Holding a block keeps stamina from refilling, which is the block's standing cost.
            if (!IsBlocking && now - lastStaminaSpend >= Tuning.StaminaRegenDelay)
            {
                Stamina = Math.Min(MaxStamina, Stamina + Tuning.StaminaRegen * deltaTime);
            }

            if (now - lastHitTaken >= Tuning.HealthRegenDelay)
            {
                Health = Math.Min(MaxHealth, Health + Tuning.HealthRegen * deltaTime);
            }
        }

        /// <summary>Starts a light attack, or returns null when it is refused (blocking, busy, tired, defeated).</summary>
        public AttackSwing TryStartAttack(float now)
        {
            if (IsBlocking || !CanAct(now) || Stamina < Tuning.AttackStaminaCost)
            {
                return null;
            }

            Spend(Tuning.AttackStaminaCost, now);
            ActiveSwing = new AttackSwing(++nextSwingId, this, now, now + Tuning.AttackDuration, Tuning.AttackDamage);
            return ActiveSwing;
        }

        /// <summary>
        /// Raises or lowers the guard. Raising it cancels a swing in progress, so a fighter is
        /// never attacking and blocking in the same instant. Returns whether the guard is up.
        /// </summary>
        public bool SetBlocking(bool wanted, float now)
        {
            if (!wanted)
            {
                IsBlocking = false;
                return false;
            }

            if (IsBlocking)
            {
                return true;
            }

            if (IsDefeated || IsStaggered(now) || IsDodging(now) || Stamina <= 0f)
            {
                return false;
            }

            CancelSwing(now);
            IsBlocking = true;
            return true;
        }

        /// <summary>Starts a dodge with its invulnerability window, or returns false when refused.</summary>
        public bool TryDodge(float now)
        {
            if (IsDefeated || IsStaggered(now) || IsAttacking(now) || IsDodging(now) ||
                Stamina < Tuning.DodgeStaminaCost)
            {
                return false;
            }

            IsBlocking = false;
            Spend(Tuning.DodgeStaminaCost, now);
            dodgeStartedAt = now;
            return true;
        }

        /// <summary>Applies <paramref name="swing"/> to this fighter, once per swing.</summary>
        public HitResult ReceiveHit(AttackSwing swing, float now)
        {
            if (swing == null)
            {
                throw new ArgumentNullException(nameof(swing));
            }

            string attacker = swing.Attacker.Id;
            if (ReferenceEquals(swing.Attacker, this))
            {
                return Ignored(IgnoreReason.OwnSwing, attacker);
            }

            if (IsDefeated)
            {
                return Ignored(IgnoreReason.AlreadyDefeated, attacker);
            }

            if (!swing.IsActive(now))
            {
                return Ignored(IgnoreReason.SwingOver, attacker);
            }

            if (swing.HasStruck(this))
            {
                return Ignored(IgnoreReason.AlreadyStruckBySwing, attacker);
            }

            // A dodged swing still counts as struck: it cannot land when the window closes mid-swing.
            swing.MarkStruck(this);
            if (IsInvulnerable(now))
            {
                return Ignored(IgnoreReason.Invulnerable, attacker);
            }

            HitOutcome outcome = HitOutcome.Hit;
            float damage = swing.Damage;
            if (IsBlocking)
            {
                if (Stamina >= Tuning.BlockStaminaCost)
                {
                    outcome = HitOutcome.Blocked;
                    damage *= 1f - Tuning.BlockDamageReduction;
                    Spend(Tuning.BlockStaminaCost, now);
                }
                else
                {
                    outcome = HitOutcome.GuardBroken;
                    Stamina = 0f;
                    lastStaminaSpend = now;
                    IsBlocking = false;
                }
            }

            lastHitTaken = now;
            Health = Math.Max(0f, Health - damage);
            bool staggered = outcome == HitOutcome.GuardBroken ||
                (outcome == HitOutcome.Hit && damage >= Tuning.StaggerThreshold);
            if (staggered)
            {
                staggeredUntil = now + Tuning.StaggerDuration;
                IsBlocking = false;
                CancelSwing(now);
            }

            bool defeated = Health <= 0f;
            if (defeated)
            {
                IsDefeated = true;
                IsBlocking = false;
                CancelSwing(now);
            }

            var result = new HitResult(outcome, IgnoreReason.None, damage, staggered, defeated, attacker);
            Damaged?.Invoke(this, result);
            if (defeated)
            {
                Defeated?.Invoke(this);
            }

            return result;
        }

        /// <summary>Full health and stamina, no swing, guard, dodge or stagger. For respawns and encounter resets.</summary>
        public void Restore()
        {
            Health = MaxHealth;
            Stamina = MaxStamina;
            IsDefeated = false;
            IsBlocking = false;
            ActiveSwing = null;
            lastStaminaSpend = float.NegativeInfinity;
            lastHitTaken = float.NegativeInfinity;
            dodgeStartedAt = float.NegativeInfinity;
            staggeredUntil = float.NegativeInfinity;
        }

        public void CancelSwing(float now)
        {
            ActiveSwing?.Cancel(now);
            ActiveSwing = null;
        }

        private void Spend(float amount, float now)
        {
            Stamina = Math.Max(0f, Stamina - amount);
            lastStaminaSpend = now;
        }

        private static HitResult Ignored(IgnoreReason reason, string attacker) =>
            new HitResult(HitOutcome.Ignored, reason, 0f, false, false, attacker);
    }
}
