using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using NUnit.Framework;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>
    /// <c>+event-facts</c>: the storyteller's events in the framework's fact ledger (#295), worded per
    /// ruler, read for <see cref="StoryRules.EventFactTurns"/> turns, and no longer among the turn's facts.
    /// </summary>
    public sealed class StorytellerEventFactsTests
    {
        private const string Golden = "The Golden Star: A meteor of gold fell in the hills of valen.";

        private static Game AtTurnTwo(ModelSeatOptions options)
        {
            var g = NewGame(1);
            g.EventTable = false;
            g.Story = options.StoryRules();
            PlayTurn(g);
            PlayTurn(g);
            return g;
        }

        private sealed class Table
        {
            public Game Game;
            public ScriptedProvider Valen, Orsk;
            public Dictionary<string, CountryAgent> Seats;
            public Dictionary<string, IPlayer> Players;

            public string Saw(ScriptedProvider p) => p.LastRequest.Request.Stimulus + "\n" + p.LastRequest.Request.Observations;
        }

        private static Table NewTable(string arm)
        {
            var options = ModelSeatOptions.Named(arm);
            var t = new Table { Game = AtTurnTwo(options) };
            t.Valen = new ScriptedProvider().Otherwise(ScriptedReply.Decide(ActionRegistry.None, TargetRegistry.NoTarget, "We count our gold."));
            t.Orsk = new ScriptedProvider().Otherwise(ScriptedReply.Decide(ActionRegistry.None, TargetRegistry.NoTarget, "We watch."));
            t.Seats = new Dictionary<string, CountryAgent>
            {
                ["valen"] = new CountryAgent(t.Game.World, "valen", t.Valen, options),
                ["orsk"] = new CountryAgent(t.Game.World, "orsk", t.Orsk, options),
            };
            foreach (var kv in t.Seats) t.Game.Gates[kv.Key] = kv.Value;
            t.Players = t.Game.World.CountryList.Where(c => !t.Seats.ContainsKey(c.Id)).ToDictionary(c => c.Id, c => (IPlayer)new HoldingPlayer());
            return t;
        }

        private static async Task Turn(Table t, Storyteller teller = null) =>
            await Await.NotCancelled(ModelGameRunner.PlayTurnAsync(t.Game, t.Players, t.Seats, default, teller));

        [Test]
        public void TheSwitch_Parses_AndIsOffInTheMenusArm()
        {
            Assert.That(ModelSeatOptions.Named("default+storyteller+event-facts").StoryRules().EventFacts, Is.True);
            Assert.That(ModelSeatOptions.Named("default+storyteller+s2+chains-skeleton").StoryRules().EventFacts, Is.False, "S2's measured arm is unchanged");
        }

        [Test]
        public async Task AnEvent_IsReadForThreeTurns_WordedForEachRuler_AndOnlyOnce()
        {
            var t = NewTable("default+storyteller+event-facts");
            var teller = new Storyteller(t.Game, new ScriptedProvider().Otherwise(ScriptedReply.Decide(EventRules.Windfall, "valen", Golden)), ModelSeatOptions.Named("default+storyteller+event-facts"));
            await Turn(t, teller);
            int turn = t.Game.Events.Events.Single().Turn;
            string ours = $"Turn {turn}: Event \"The Golden Star\": a windfall of 30 gold reached our treasury.";
            string news = $"Turn {turn}: News: valen received a windfall of gold (\"The Golden Star\").";

            for (int i = 0; i < StoryRules.EventFactTurns; i++)
            {
                if (i > 0) await Turn(t);
                Assert.That(t.Saw(t.Valen), Does.Contain(ours), $"valen reads its own line, turn {i}");
                Assert.That(t.Saw(t.Valen), Does.Not.Contain("News: valen"), "and not the news about itself");
                Assert.That(t.Saw(t.Orsk), Does.Contain(news), $"orsk reads the news, turn {i}");
                Assert.That(t.Saw(t.Valen).Split('\n').Count(l => l.Contains("The Golden Star")), Is.EqualTo(1), "once, not also among the turn's facts");
            }
            await Turn(t);
            Assert.That(t.Saw(t.Valen), Does.Not.Contain("The Golden Star"), "expired after three turns");
            Assert.That(t.Saw(t.Orsk), Does.Not.Contain("The Golden Star"));
        }

        [Test]
        public async Task WithTheSwitchOff_TheEventIsATurnFactAsBefore()
        {
            var t = NewTable("default+storyteller");
            var teller = new Storyteller(t.Game, new ScriptedProvider().Otherwise(ScriptedReply.Decide(EventRules.Windfall, "valen", Golden)));
            await Turn(t, teller);
            Assert.That(EventFacts.Of(t.Game.World), Is.Null, "no ledger");
            Assert.That(t.Saw(t.Valen), Does.Contain("Event \"The Golden Star\": a windfall of 30 gold reached our treasury."));
            await Turn(t);
            Assert.That(t.Saw(t.Valen), Does.Not.Contain("The Golden Star"), "read on one turn only");
        }
    }
}
