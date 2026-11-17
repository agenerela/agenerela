using System;

namespace LivingVale.Combat
{
    public enum EnemyState { Idle, Pursue, Telegraph, Attack, Recover, Stagger, Defeat }

    /// <summary>Distances in metres and durations in seconds; independent of a scene or clock.</summary>
    public sealed class EnemyBrainTuning
    {
        public float AggroRange = 9f;
        public float LeashRange = 18f;
        public float AttackRange = 1.8f;
        public float TelegraphSeconds = 0.6f;
        public float AttackSeconds = 0.35f;
        public float RecoverSeconds = 0.8f;
        public float MoveSpeed = 2.4f;
        public float LungeSpeed;
        public float HomeArrivalDistance = 0.25f;
        public float HitRange = 1.9f;
        public float HitHalfAngleDegrees = 60f;

        public static EnemyBrainTuning Raider() => new EnemyBrainTuning
        {
            AttackSeconds = CombatTuning.Raider().AttackDuration
        };

        public static EnemyBrainTuning Wolf() => new EnemyBrainTuning
        {
            AggroRange = 11f,
            LeashRange = 20f,
            AttackRange = 4f,
            TelegraphSeconds = 0.45f,
            AttackSeconds = CombatTuning.Wolf().AttackDuration,
            RecoverSeconds = 1f,
            MoveSpeed = 3.2f,
            LungeSpeed = 10f,
            HitRange = 1.3f,
            HitHalfAngleDegrees = 60f
        };
    }

    public readonly struct EnemyPerception
    {
        public EnemyPerception(float distanceToTarget, float targetDistanceFromHome,
            float selfDistanceFromHome, bool targetAvailable, bool staggered = false, bool defeated = false)
        {
            DistanceToTarget = distanceToTarget;
            TargetDistanceFromHome = targetDistanceFromHome;
            SelfDistanceFromHome = selfDistanceFromHome;
            TargetAvailable = targetAvailable;
            Staggered = staggered;
            Defeated = defeated;
        }

        public float DistanceToTarget { get; }
        public float TargetDistanceFromHome { get; }
        public float SelfDistanceFromHome { get; }
        public bool TargetAvailable { get; }
        public bool Staggered { get; }
        public bool Defeated { get; }
    }

    /// <summary>
    /// One transition per step keeps every wind-up, contact and recovery observable, even after
    /// a long frame. A committed wind-up and swing finish before ordinary disengagement.
    /// </summary>
    public sealed class EnemyBrain
    {
        private readonly EnemyBrainTuning tuning;

        public EnemyBrain(EnemyBrainTuning tuning)
        {
            this.tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        }

        public EnemyState State { get; private set; }
        public float StateEnteredAt { get; private set; }
        public bool IsEngaged => State == EnemyState.Pursue || State == EnemyState.Telegraph ||
            State == EnemyState.Attack || State == EnemyState.Recover || State == EnemyState.Stagger;
        public bool ReturningHome { get; private set; }

        public EnemyState Step(in EnemyPerception p, float now)
        {
            if (State == EnemyState.Defeat)
            {
                return State;
            }

            if (p.Defeated)
            {
                ReturningHome = false;
                return Enter(EnemyState.Defeat, now);
            }

            if (p.Staggered)
            {
                ReturningHome = false;
                return Enter(EnemyState.Stagger, now);
            }

            if (State == EnemyState.Stagger)
            {
                return Enter(EnemyState.Recover, now);
            }

            // Retreating, disappearing or crossing the leash cannot cancel a visible wind-up.
            if (State == EnemyState.Telegraph)
            {
                return now >= StateEnteredAt + tuning.TelegraphSeconds ? Enter(EnemyState.Attack, now) : State;
            }

            if (State == EnemyState.Attack)
            {
                return now >= StateEnteredAt + tuning.AttackSeconds ? Enter(EnemyState.Recover, now) : State;
            }

            if (ReturningHome)
            {
                if (p.SelfDistanceFromHome <= tuning.HomeArrivalDistance)
                {
                    ReturningHome = false;
                }

                return State;
            }

            if (!p.TargetAvailable || p.TargetDistanceFromHome > tuning.LeashRange ||
                p.SelfDistanceFromHome > tuning.LeashRange)
            {
                ReturningHome = p.SelfDistanceFromHome > tuning.HomeArrivalDistance;
                return Enter(EnemyState.Idle, now);
            }

            switch (State)
            {
                case EnemyState.Idle:
                    if (p.DistanceToTarget <= tuning.AggroRange)
                    {
                        return Enter(EnemyState.Pursue, now);
                    }
                    break;
                case EnemyState.Pursue:
                    if (p.DistanceToTarget <= tuning.AttackRange)
                    {
                        return Enter(EnemyState.Telegraph, now);
                    }
                    break;
                case EnemyState.Recover:
                    if (now >= StateEnteredAt + tuning.RecoverSeconds)
                    {
                        return Enter(EnemyState.Pursue, now);
                    }
                    break;
            }

            return State;
        }

        public void Reset(float now)
        {
            State = EnemyState.Idle;
            StateEnteredAt = now;
            ReturningHome = false;
        }

        private EnemyState Enter(EnemyState state, float now)
        {
            if (State != state)
            {
                State = state;
                StateEnteredAt = now;
            }

            return State;
        }
    }
}
