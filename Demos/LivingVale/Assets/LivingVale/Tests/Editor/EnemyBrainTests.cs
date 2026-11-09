using LivingVale.Combat;
using NUnit.Framework;

namespace LivingVale.Tests
{
    public sealed class EnemyBrainTests
    {
        // Steps land just past a deadline: float sums at the exact boundary round differently under Mono.
        private const float Past = 0.001f;

        private EnemyBrainTuning tuning;
        private EnemyBrain brain;

        [SetUp]
        public void SetUp()
        {
            tuning = EnemyBrainTuning.Raider();
            brain = new EnemyBrain(tuning);
            brain.Reset(10f);
        }

        [Test]
        public void Idle_AggroRequiresAnAvailableTargetWithinRange()
        {
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.IsEngaged, Is.False);
            Assert.That(brain.Step(See(tuning.AggroRange + 0.01f), 11f), Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.Step(See(2f, available: false), 12f), Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.Step(See(tuning.AggroRange), 13f), Is.EqualTo(EnemyState.Pursue));
            Assert.That(brain.IsEngaged, Is.True);
            Assert.That(brain.StateEnteredAt, Is.EqualTo(13f));
        }

        [Test]
        public void Pursue_EntersTelegraphOnlyWithinAttackRange()
        {
            brain.Step(See(5f), 10f);
            Assert.That(brain.Step(See(tuning.AttackRange + 0.01f), 11f), Is.EqualTo(EnemyState.Pursue));
            Assert.That(brain.StateEnteredAt, Is.EqualTo(10f), "staying in a state must not restart its clock");
            Assert.That(brain.Step(See(tuning.AttackRange), 12f), Is.EqualTo(EnemyState.Telegraph));
            Assert.That(brain.StateEnteredAt, Is.EqualTo(12f));
            Assert.That(brain.IsEngaged, Is.True);
        }

        [Test]
        public void Telegraph_AttacksOnlyWhenItsFullWindupHasElapsed()
        {
            EnterTelegraph();
            float deadline = brain.StateEnteredAt + tuning.TelegraphSeconds;
            Assert.That(brain.Step(See(1f), deadline - 0.001f), Is.EqualTo(EnemyState.Telegraph));
            Assert.That(brain.StateEnteredAt, Is.EqualTo(11f));
            Assert.That(brain.Step(See(1f), deadline), Is.EqualTo(EnemyState.Attack));
            Assert.That(brain.StateEnteredAt, Is.EqualTo(deadline));
        }

        [Test]
        public void Telegraph_StillAttacksWhenTheTargetStepsBack()
        {
            EnterTelegraph();
            float deadline = brain.StateEnteredAt + tuning.TelegraphSeconds;
            Assert.That(brain.Step(See(6f), deadline - 0.001f), Is.EqualTo(EnemyState.Telegraph));
            Assert.That(brain.Step(See(6f), deadline), Is.EqualTo(EnemyState.Attack));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CommittedWindupAndSwing_FinishBeforeGivingUp(bool unavailable)
        {
            EnterTelegraph();
            var lost = new EnemyPerception(30f, tuning.LeashRange + 1f, 5f, !unavailable);
            float attackAt = brain.StateEnteredAt + tuning.TelegraphSeconds;
            Assert.That(brain.Step(lost, attackAt - 0.001f), Is.EqualTo(EnemyState.Telegraph));
            Assert.That(brain.Step(lost, attackAt), Is.EqualTo(EnemyState.Attack));
            Assert.That(brain.Step(lost, attackAt + tuning.AttackSeconds - 0.001f), Is.EqualTo(EnemyState.Attack));
            Assert.That(brain.Step(lost, attackAt + tuning.AttackSeconds + Past), Is.EqualTo(EnemyState.Recover));
            Assert.That(brain.Step(lost, attackAt + tuning.AttackSeconds + 0.01f), Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.ReturningHome, Is.True);
            Assert.That(brain.IsEngaged, Is.False);
        }

        [Test]
        public void AttackAndRecovery_EachKeepTheirOwnDuration()
        {
            EnterTelegraph();
            float attackAt = 11f + tuning.TelegraphSeconds;
            brain.Step(See(1f), attackAt);
            float recoverAt = attackAt + tuning.AttackSeconds;
            Assert.That(brain.Step(See(1f), recoverAt - 0.001f), Is.EqualTo(EnemyState.Attack));
            Assert.That(brain.Step(See(1f), recoverAt), Is.EqualTo(EnemyState.Recover));
            Assert.That(brain.StateEnteredAt, Is.EqualTo(recoverAt));
            float pursueAt = recoverAt + tuning.RecoverSeconds;
            Assert.That(brain.Step(See(1f), pursueAt - 0.001f), Is.EqualTo(EnemyState.Recover));
            Assert.That(brain.Step(See(1f), pursueAt), Is.EqualTo(EnemyState.Pursue));
        }

        [Test]
        public void LongFrame_DoesNotSkipTheAttackOrItsRecovery()
        {
            EnterTelegraph();
            Assert.That(brain.Step(See(1f), 100f), Is.EqualTo(EnemyState.Attack));
            Assert.That(brain.StateEnteredAt, Is.EqualTo(100f));
            Assert.That(brain.Step(See(1f), 200f), Is.EqualTo(EnemyState.Recover));
            Assert.That(brain.StateEnteredAt, Is.EqualTo(200f));
            Assert.That(brain.Step(See(1f), 200.1f), Is.EqualTo(EnemyState.Recover));
        }

        [TestCase(EnemyState.Idle)]
        [TestCase(EnemyState.Pursue)]
        [TestCase(EnemyState.Telegraph)]
        [TestCase(EnemyState.Attack)]
        [TestCase(EnemyState.Recover)]
        public void Stagger_InterruptsEveryLivingStateAndEndsInRecovery(EnemyState interrupted)
        {
            EnterState(interrupted);
            var staggered = See(1f, staggered: true);
            Assert.That(brain.Step(staggered, 20f), Is.EqualTo(EnemyState.Stagger));
            Assert.That(brain.IsEngaged, Is.True);
            Assert.That(brain.Step(staggered, 21f), Is.EqualTo(EnemyState.Stagger));
            Assert.That(brain.StateEnteredAt, Is.EqualTo(20f));
            Assert.That(brain.Step(See(1f), 22f), Is.EqualTo(EnemyState.Recover));
            Assert.That(brain.StateEnteredAt, Is.EqualTo(22f));
            Assert.That(brain.Step(See(1f), 22f + tuning.RecoverSeconds - 0.001f), Is.EqualTo(EnemyState.Recover));
            Assert.That(brain.Step(See(1f), 22f + tuning.RecoverSeconds + Past), Is.EqualTo(EnemyState.Pursue));
        }

        [TestCase(EnemyState.Idle)]
        [TestCase(EnemyState.Pursue)]
        [TestCase(EnemyState.Telegraph)]
        [TestCase(EnemyState.Attack)]
        [TestCase(EnemyState.Recover)]
        [TestCase(EnemyState.Stagger)]
        public void Defeat_TakesPriorityAndIsTerminalUntilReset(EnemyState interrupted)
        {
            EnterState(interrupted);
            var defeated = See(1f, staggered: true, defeated: true);
            Assert.That(brain.Step(defeated, 20f), Is.EqualTo(EnemyState.Defeat));
            Assert.That(brain.IsEngaged, Is.False);
            Assert.That(brain.ReturningHome, Is.False);
            Assert.That(brain.Step(See(1f), 100f), Is.EqualTo(EnemyState.Defeat));
            Assert.That(brain.Step(See(1f, staggered: true), 101f), Is.EqualTo(EnemyState.Defeat));
            Assert.That(brain.StateEnteredAt, Is.EqualTo(20f));

            brain.Reset(102f);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.IsEngaged, Is.False);
            Assert.That(brain.StateEnteredAt, Is.EqualTo(102f));
            Assert.That(brain.Step(See(1f), 103f), Is.EqualTo(EnemyState.Pursue));
        }

        [Test]
        public void Leash_DisengagesAndDoesNotReaggroUntilHome()
        {
            brain.Step(See(5f), 10f);
            var leashed = new EnemyPerception(2f, tuning.LeashRange + 0.01f, 5f, true);
            Assert.That(brain.Step(leashed, 11f), Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.IsEngaged, Is.False);
            Assert.That(brain.ReturningHome, Is.True);

            var nearby = new EnemyPerception(1f, 3f, 3f, true);
            Assert.That(brain.Step(nearby, 12f), Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.ReturningHome, Is.True, "finish the retreat before accepting another fight");
            var home = new EnemyPerception(1f, 1f, tuning.HomeArrivalDistance, true);
            Assert.That(brain.Step(home, 13f), Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.ReturningHome, Is.False);
            Assert.That(brain.Step(home, 14f), Is.EqualTo(EnemyState.Pursue));
        }

        [Test]
        public void SelfOutsideLeash_AlsoReturnsHome()
        {
            brain.Step(See(5f), 10f);
            var outside = new EnemyPerception(2f, 17f, tuning.LeashRange + 1f, true);
            Assert.That(brain.Step(outside, 11f), Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.ReturningHome, Is.True);
            Assert.That(brain.IsEngaged, Is.False);
        }

        [TestCase(EnemyState.Pursue)]
        [TestCase(EnemyState.Recover)]
        public void UnavailableTarget_DisengagesAndWalksHome(EnemyState state)
        {
            EnterState(state);
            var missing = new EnemyPerception(float.PositiveInfinity, float.PositiveInfinity, 4f, false);
            Assert.That(brain.Step(missing, 20f), Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.ReturningHome, Is.True);
            Assert.That(brain.IsEngaged, Is.False);
        }

        [Test]
        public void UnavailableTargetAtHome_DoesNotStartARetreat()
        {
            brain.Step(See(5f), 10f);
            Assert.That(brain.Step(See(1f, available: false), 11f), Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.ReturningHome, Is.False);
        }

        [Test]
        public void Reset_ClearsARetreat()
        {
            brain.Step(See(5f), 10f);
            brain.Step(new EnemyPerception(20f, 25f, 5f, true), 11f);
            Assert.That(brain.ReturningHome, Is.True);
            brain.Reset(12f);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.ReturningHome, Is.False);
            Assert.That(brain.IsEngaged, Is.False);
            Assert.That(brain.StateEnteredAt, Is.EqualTo(12f));
        }

        [Test]
        public void Wolf_StartsItsLungeFromFurtherAwayThanRaider()
        {
            var wolfTuning = EnemyBrainTuning.Wolf();
            var wolf = new EnemyBrain(wolfTuning);
            wolf.Reset(10f);
            var target = See(3.5f);
            brain.Step(target, 10f);
            wolf.Step(target, 10f);
            Assert.That(brain.Step(target, 11f), Is.EqualTo(EnemyState.Pursue));
            Assert.That(wolf.Step(target, 11f), Is.EqualTo(EnemyState.Telegraph));
            Assert.That(wolf.Step(target, 11f + wolfTuning.TelegraphSeconds + Past), Is.EqualTo(EnemyState.Attack));
            Assert.That(wolfTuning.AttackRange, Is.GreaterThan(tuning.AttackRange));
            Assert.That(wolfTuning.LungeSpeed, Is.GreaterThan(0f));
            Assert.That(tuning.LungeSpeed, Is.Zero);
            Assert.That(wolfTuning.RecoverSeconds, Is.GreaterThan(tuning.RecoverSeconds));
        }

        [Test]
        public void AttackDurations_MatchTheCombatantsContactWindows()
        {
            Assert.That(tuning.AttackSeconds, Is.EqualTo(CombatTuning.Raider().AttackDuration));
            Assert.That(EnemyBrainTuning.Wolf().AttackSeconds, Is.EqualTo(CombatTuning.Wolf().AttackDuration));
        }

        private void EnterTelegraph()
        {
            Assert.That(brain.Step(See(1f), 10f), Is.EqualTo(EnemyState.Pursue));
            Assert.That(brain.Step(See(1f), 11f), Is.EqualTo(EnemyState.Telegraph));
        }

        private void EnterState(EnemyState state)
        {
            if (state == EnemyState.Idle)
            {
                return;
            }

            brain.Step(See(1f), 10f);
            if (state == EnemyState.Pursue)
            {
                return;
            }

            brain.Step(See(1f), 11f);
            if (state == EnemyState.Telegraph)
            {
                return;
            }

            float attackAt = 11f + tuning.TelegraphSeconds;
            brain.Step(See(1f), attackAt);
            if (state == EnemyState.Attack)
            {
                return;
            }

            brain.Step(See(1f), attackAt + tuning.AttackSeconds + Past);
            if (state == EnemyState.Stagger)
            {
                brain.Step(See(1f, staggered: true), 15f);
            }

            Assert.That(brain.State, Is.EqualTo(state), "test setup reached the state being interrupted");
        }

        private static EnemyPerception See(float distance, bool available = true,
            bool staggered = false, bool defeated = false) =>
            new EnemyPerception(distance, distance, 0f, available, staggered, defeated);
    }
}
