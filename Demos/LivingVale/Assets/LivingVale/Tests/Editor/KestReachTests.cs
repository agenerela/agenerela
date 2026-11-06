using LivingVale.Combat;
using LivingVale.Editor;
using LivingVale.Game;
using LivingVale.World;
using NUnit.Framework;

namespace LivingVale.Tests
{
    /// <summary>
    /// The negotiation and investigation routes start by talking to Kest, and chat refuses to open
    /// while an enemy is engaged. So the player must be able to stand in talk range of Kest without
    /// waking the camp's defenders (#206 review: Kest stood 3 m from them).
    /// </summary>
    public sealed class KestReachTests
    {
        private const float TalkRange = 3f;

        [Test]
        public void EveryKestStop_IsOutOfTheDefendersReach_FromTalkRange()
        {
            var watch = ValePlaces.Get(ValePlaces.BrokenWatch);
            float aggro = System.Math.Max(EnemyBrainTuning.Raider().AggroRange, EnemyBrainTuning.Wolf().AggroRange);
            float needed = aggro + TalkRange + 1f;

            foreach (var stop in RegionNpcs.Get(RegionNpcs.KestId).Stops)
            {
                foreach (var offset in QuestSceneBuilder.DefenderOffsets)
                {
                    float dx = stop.X - (watch.X + offset.x);
                    float dz = stop.Z - (watch.Z + offset.z);
                    float distance = (float)System.Math.Sqrt(dx * dx + dz * dz);
                    Assert.That(distance, Is.GreaterThanOrEqualTo(needed),
                        $"Kest's stop ({stop.X}, {stop.Z}) is {distance:0.0} m from a defender; talking there wakes the camp.");
                }
            }
        }
    }
}
