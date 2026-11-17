using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The turn rework, slice 1: a game played with <see cref="GameRules.FreeOrders"/> by the scripted rulers is
    /// recorded and the replay rebuilds it, each realm's several decisions in the order the game asked them.
    /// </summary>
    public class FreeOrderReplayTests
    {
        [Test]
        public void A_free_orders_game_replays_with_every_realms_decisions_in_order()
        {
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            rules.FreeOrders = true;
            var recording = ScriptedWorldGame.Play(setup, map, rules);
            var g = ReplayBuilder.Build(recording, map, rules);
            Assert.That(g.StoppedBecause, Is.Null);
            Assert.That(g.Turns.Count, Is.GreaterThan(5));
            Assert.That(g.Turns.Sum(t => t.ArmyDrift), Is.EqualTo(0), "the replay's armies stand where the game's do");
            var courts = recording.Decisions.Where(d => d.Moment == "council").GroupBy(d => (d.Turn, d.AgentId)).ToList();
            Assert.That(courts.Any(c => c.Count() > 1), Is.True, "some realm gave more than one court order in a turn");
        }
    }
}
