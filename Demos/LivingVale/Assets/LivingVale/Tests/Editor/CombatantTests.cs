using LivingVale.Combat;
using NUnit.Framework;
using UnityEngine;

namespace LivingVale.Tests
{
    public sealed class CombatantTests
    {
        private static Combatant Player() => new Combatant("player", CombatTuning.Player());
        private static Combatant Foe() => new Combatant("foe", CombatTuning.Player());

        [Test]
        public void StartsAtFullHealthAndStamina()
        {
            var player = Player();
            Assert.That(player.Health, Is.EqualTo(100f));
            Assert.That(player.Stamina, Is.EqualTo(100f));
        }

        [Test]
        public void LightAttackDealsTwentyDamage()
        {
            var attacker = Foe();
            var victim = Player();
            AttackSwing swing = attacker.TryStartAttack(0f);

            HitResult hit = victim.ReceiveHit(swing, 0.1f);

            Assert.That(hit.Outcome, Is.EqualTo(HitOutcome.Hit));
            Assert.That(hit.Damage, Is.EqualTo(20f));
            Assert.That(victim.Health, Is.EqualTo(80f));
        }

        [Test]
        public void LightAttackCostsTenStamina()
        {
            var attacker = Foe();
            Assert.That(attacker.TryStartAttack(0f), Is.Not.Null);
            Assert.That(attacker.Stamina, Is.EqualTo(90f));
        }

        [Test]
        public void SwingLandsOncePerVictim()
        {
            var attacker = Foe();
            var victim = Player();
            var bystander = new Combatant("bystander", CombatTuning.Player());
            AttackSwing swing = attacker.TryStartAttack(0f);

            HitResult first = victim.ReceiveHit(swing, 0.1f);
            HitResult second = victim.ReceiveHit(swing, 0.2f);
            HitResult other = bystander.ReceiveHit(swing, 0.2f);

            Assert.That(first.Landed, Is.True);
            Assert.That(second.Outcome, Is.EqualTo(HitOutcome.Ignored));
            Assert.That(second.IgnoredBecause, Is.EqualTo(IgnoreReason.AlreadyStruckBySwing));
            Assert.That(victim.Health, Is.EqualTo(80f));
            Assert.That(other.Landed, Is.True, "Each victim takes its own hit from the same swing.");
        }

        [Test]
        public void NextSwingCanHitTheSameVictimAgain()
        {
            var attacker = Foe();
            var victim = Player();
            victim.ReceiveHit(attacker.TryStartAttack(0f), 0.1f);
            attacker.Tick(1f, 0.016f);

            AttackSwing next = attacker.TryStartAttack(1f);
            HitResult hit = victim.ReceiveHit(next, 1.1f);

            Assert.That(hit.Landed, Is.True);
            Assert.That(victim.Health, Is.EqualTo(60f));
        }

        [Test]
        public void CannotAttackAgainDuringASwingOrWithoutStamina()
        {
            var attacker = Foe();
            Assert.That(attacker.TryStartAttack(0f), Is.Not.Null);
            Assert.That(attacker.TryStartAttack(0.1f), Is.Null, "A swing in progress refuses another.");

            var tired = Foe();
            float now = 0f;
            for (int i = 0; i < 10; i++, now += 1f)
            {
                Assert.That(tired.TryStartAttack(now), Is.Not.Null);
            }

            Assert.That(tired.Stamina, Is.EqualTo(0f).Within(0.001f));
            Assert.That(tired.TryStartAttack(now), Is.Null);
        }

        [Test]
        public void BlockReducesDamageAndCostsStamina()
        {
            var attacker = Foe();
            var victim = Player();
            Assert.That(victim.SetBlocking(true, 0f), Is.True);

            HitResult hit = victim.ReceiveHit(attacker.TryStartAttack(0f), 0.1f);

            Assert.That(hit.Outcome, Is.EqualTo(HitOutcome.Blocked));
            Assert.That(hit.Damage, Is.EqualTo(5f).Within(0.001f));
            Assert.That(victim.Health, Is.EqualTo(95f).Within(0.001f));
            Assert.That(victim.Stamina, Is.EqualTo(85f).Within(0.001f));
            Assert.That(hit.Staggered, Is.False, "A held block does not stagger.");
        }

        [Test]
        public void BlockingRefusesAnAttackInTheSameInstant()
        {
            var player = Player();
            player.SetBlocking(true, 0f);
            Assert.That(player.TryStartAttack(0f), Is.Null);
            Assert.That(player.Stamina, Is.EqualTo(100f), "A refused attack spends nothing.");
        }

        [Test]
        public void RaisingABlockCancelsTheSwingInProgress()
        {
            var attacker = Foe();
            var victim = Player();
            AttackSwing swing = attacker.TryStartAttack(0f);

            Assert.That(attacker.SetBlocking(true, 0.05f), Is.True);
            HitResult hit = victim.ReceiveHit(swing, 0.1f);

            Assert.That(attacker.IsAttacking(0.1f), Is.False);
            Assert.That(hit.IgnoredBecause, Is.EqualTo(IgnoreReason.SwingOver));
            Assert.That(victim.Health, Is.EqualTo(100f));
        }

        [Test]
        public void NoGuardGoesUpWithNoStamina()
        {
            var victim = Player();
            float now = 0f;
            for (int i = 0; i < 4; i++, now += 1f)
            {
                victim.TryDodge(now); // 4 × 25 stamina
            }

            Assert.That(victim.Stamina, Is.EqualTo(0f).Within(0.001f));
            victim.SetBlocking(true, now);
            Assert.That(victim.IsBlocking, Is.False, "No guard goes up with no stamina at all.");
        }

        [Test]
        public void GuardBreaksWhenAHitCostsMoreStaminaThanIsLeft()
        {
            var victim = Player();
            victim.TryDodge(0f);
            victim.TryDodge(1f);
            victim.TryDodge(2f);
            victim.TryStartAttack(3f); // 100 - 75 - 10 = 15
            victim.Tick(4f, 0f);
            victim.TryStartAttack(4f); // 5 left, below the block cost of 15
            victim.Tick(5f, 0f);
            Assert.That(victim.SetBlocking(true, 5f), Is.True);

            HitResult hit = victim.ReceiveHit(Foe().TryStartAttack(5f), 5.1f);

            Assert.That(hit.Outcome, Is.EqualTo(HitOutcome.GuardBroken));
            Assert.That(hit.Damage, Is.EqualTo(20f));
            Assert.That(hit.Staggered, Is.True);
            Assert.That(victim.IsBlocking, Is.False);
        }

        [Test]
        public void GuardBreakStaggersEvenBelowTheStaggerThreshold()
        {
            var victim = new Combatant("sturdy", new CombatTuning { MaxStamina = 5f, StaggerThreshold = 25f });
            Assert.That(victim.SetBlocking(true, 0f), Is.True);

            HitResult hit = victim.ReceiveHit(Foe().TryStartAttack(0f), 0.1f);

            Assert.That(hit.Outcome, Is.EqualTo(HitOutcome.GuardBroken));
            Assert.That(hit.Staggered, Is.True);
        }

        [Test]
        public void DodgeCostsTwentyFiveStamina()
        {
            var player = Player();
            Assert.That(player.TryDodge(0f), Is.True);
            Assert.That(player.Stamina, Is.EqualTo(75f));
        }

        [Test]
        public void DodgeIgnoresHitsInsideItsWindowOnly()
        {
            var victim = Player();
            victim.TryDodge(0f);
            float window = victim.Tuning.DodgeInvulnerability;

            HitResult inside = victim.ReceiveHit(Foe().TryStartAttack(0f), window * 0.5f);
            HitResult after = victim.ReceiveHit(Foe().TryStartAttack(window), window + 0.01f);

            Assert.That(inside.IgnoredBecause, Is.EqualTo(IgnoreReason.Invulnerable));
            Assert.That(after.Landed, Is.True);
            Assert.That(victim.Health, Is.EqualTo(80f));
        }

        [Test]
        public void DodgedSwingCannotLandWhenTheWindowCloses()
        {
            var victim = Player();
            AttackSwing swing = Foe().TryStartAttack(0f);
            victim.TryDodge(0f);

            victim.ReceiveHit(swing, 0.1f);
            HitResult late = victim.ReceiveHit(swing, victim.Tuning.DodgeInvulnerability + 0.05f);

            Assert.That(late.Landed, Is.False);
            Assert.That(victim.Health, Is.EqualTo(100f));
        }

        [Test]
        public void HitStaggersAndCancelsTheVictimsSwing()
        {
            var victim = Player();
            AttackSwing own = victim.TryStartAttack(0f);

            HitResult hit = victim.ReceiveHit(Foe().TryStartAttack(0f), 0.1f);

            Assert.That(hit.Staggered, Is.True);
            Assert.That(victim.IsStaggered(0.2f), Is.True);
            Assert.That(own.IsActive(0.2f), Is.False, "A stagger interrupts the victim's attack.");
            Assert.That(victim.TryStartAttack(0.2f), Is.Null);
            Assert.That(victim.IsStaggered(0.1f + victim.Tuning.StaggerDuration + 0.01f), Is.False);
        }

        [Test]
        public void StaminaRegeneratesAfterTheDelayButNotWhileBlocking()
        {
            var player = Player();
            player.TryDodge(0f);
            player.Tick(0.1f, 0.1f);
            Assert.That(player.Stamina, Is.EqualTo(75f), "No regeneration inside the delay.");

            player.Tick(2f, 1f);
            Assert.That(player.Stamina, Is.EqualTo(75f + player.Tuning.StaminaRegen).Within(0.001f));

            player.SetBlocking(true, 2f);
            player.Tick(3f, 1f);
            Assert.That(player.Stamina, Is.EqualTo(75f + player.Tuning.StaminaRegen).Within(0.001f));
        }

        [Test]
        public void HealthRegeneratesOnlyLongAfterTheLastHit()
        {
            var player = Player();
            player.ReceiveHit(Foe().TryStartAttack(0f), 0f);
            player.Tick(1f, 1f);
            Assert.That(player.Health, Is.EqualTo(80f));

            player.Tick(player.Tuning.HealthRegenDelay + 1f, 1f);
            Assert.That(player.Health, Is.EqualTo(80f + player.Tuning.HealthRegen).Within(0.001f));
        }

        [Test]
        public void DefeatIsRaisedOnceAndRestoreRevives()
        {
            var victim = new Combatant("weak", new CombatTuning { MaxHealth = 30f });
            int defeats = 0;
            victim.Defeated += _ => defeats++;

            victim.ReceiveHit(Foe().TryStartAttack(0f), 0f);
            HitResult fatal = victim.ReceiveHit(Foe().TryStartAttack(1f), 1f);
            HitResult afterwards = victim.ReceiveHit(Foe().TryStartAttack(2f), 2f);

            Assert.That(fatal.Defeated, Is.True);
            Assert.That(victim.Health, Is.EqualTo(0f));
            Assert.That(afterwards.IgnoredBecause, Is.EqualTo(IgnoreReason.AlreadyDefeated));
            Assert.That(defeats, Is.EqualTo(1));
            Assert.That(victim.TryStartAttack(3f), Is.Null);

            victim.Restore();
            Assert.That(victim.IsDefeated, Is.False);
            Assert.That(victim.Health, Is.EqualTo(30f));
            Assert.That(victim.TryStartAttack(3f), Is.Not.Null);
        }

        [Test]
        public void OwnSwingNeverHitsItsAttacker()
        {
            var attacker = Foe();
            HitResult hit = attacker.ReceiveHit(attacker.TryStartAttack(0f), 0.1f);
            Assert.That(hit.IgnoredBecause, Is.EqualTo(IgnoreReason.OwnSwing));
        }

        [Test]
        public void LootTableUsesTheStableItemIds()
        {
            LootDrop[] wolf = LootTable.For(EnemyArchetype.Wolf, "enemy_wolf_1", Vector3.zero);
            LootDrop[] raider = LootTable.For(EnemyArchetype.Raider, "enemy_raider_1", Vector3.zero);

            Assert.That(wolf[0].ItemId, Is.EqualTo("item_wolf_pelt"));
            Assert.That(raider[0].ItemId, Is.EqualTo("item_iron_scrap"));
            Assert.That(wolf[0].SourceId, Is.EqualTo("enemy_wolf_1"));
        }
    }
}
