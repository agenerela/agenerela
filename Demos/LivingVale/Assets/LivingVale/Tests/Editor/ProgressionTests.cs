using System.Collections.Generic;
using LivingVale.Game;
using NUnit.Framework;

namespace LivingVale.Tests
{
    /// <summary>G4's XP thresholds, perk choices and once-per-event awards.</summary>
    public sealed class ProgressionTests
    {
        [TestCase(0, 1)]
        [TestCase(99, 1)]
        [TestCase(100, 2)]
        [TestCase(249, 2)]
        [TestCase(250, 3)]
        [TestCase(10000, 3)]
        public void LevelsFollowTheCumulativeThresholds(int xp, int level)
        {
            Assert.AreEqual(level, Progression.LevelFor(xp));
        }

        [Test]
        public void AnEventIdAwardsXpOnlyOnce()
        {
            var progression = new Progression();
            Assert.AreEqual(ProgressionRejection.None, progression.TryAward("quest_x", 60));
            Assert.AreEqual(ProgressionRejection.DuplicateEvent, progression.TryAward("quest_x", 60));
            Assert.AreEqual(60, progression.Xp);
            Assert.AreEqual(ProgressionRejection.InvalidAmount, progression.TryAward("quest_y", 0));
            Assert.AreEqual(ProgressionRejection.InvalidAmount, progression.TryAward("quest_z", -5));
            Assert.IsFalse(progression.HasAwarded("quest_y"), "A refused award does not use up its ID.");
            Assert.AreEqual(ProgressionRejection.None, progression.TryAward("quest_y", 1));
        }

        [Test]
        public void EachLevelUpGrantsOnePerkChoice()
        {
            var progression = new Progression();
            var levels = new List<int>();
            progression.LeveledUp += levels.Add;
            Assert.AreEqual(ProgressionRejection.NoPerkChoice, progression.TryChoosePerk(Perk.Hardy));

            progression.TryAward("a", 120);
            Assert.AreEqual(new[] { 2 }, levels.ToArray());
            Assert.AreEqual(1, progression.PendingPerkChoices);
            Assert.AreEqual(Progression.HealthPerLevel, progression.MaxHealthBonus);
            Assert.AreEqual(ProgressionRejection.None, progression.TryChoosePerk(Perk.TrustedFace));
            Assert.AreEqual(ProgressionRejection.NoPerkChoice, progression.TryChoosePerk(Perk.Hardy));

            progression.TryAward("b", 130);
            Assert.AreEqual(new[] { 2, 3 }, levels.ToArray());
            Assert.AreEqual(ProgressionRejection.AlreadyChosen, progression.TryChoosePerk(Perk.TrustedFace));
            Assert.AreEqual(ProgressionRejection.None, progression.TryChoosePerk(Perk.Skirmisher));
            Assert.AreEqual(0, progression.PendingPerkChoices);
            Assert.AreEqual(new[] { Perk.TrustedFace, Perk.Skirmisher }, progression.Perks);
            Assert.AreEqual(0, progression.XpToNextLevel, "Level 3 is the cap.");
        }

        [Test]
        public void OneAwardCanCrossBothThresholds()
        {
            var progression = new Progression();
            var levels = new List<int>();
            progression.LeveledUp += levels.Add;
            progression.TryAward("big", 300);
            Assert.AreEqual(new[] { 2, 3 }, levels.ToArray());
            Assert.AreEqual(2, progression.PendingPerkChoices);
        }

        [Test]
        public void SavedStatesAreCheckedForConsistency()
        {
            Assert.IsTrue(Progression.IsValidState(120, new[] { Perk.Hardy }, new[] { "e" }));
            Assert.IsFalse(Progression.IsValidState(-1, new Perk[0], new string[0]));
            Assert.IsFalse(Progression.IsValidState(50, new[] { Perk.Hardy }, new string[0]), "A perk needs a level-up.");
            Assert.IsFalse(Progression.IsValidState(300, new[] { Perk.Hardy, Perk.Hardy }, new string[0]));
            Assert.IsFalse(Progression.IsValidState(300, new[] { (Perk)9 }, new string[0]));
            Assert.IsFalse(Progression.IsValidState(0, new Perk[0], new[] { " " }));
        }
    }
}
