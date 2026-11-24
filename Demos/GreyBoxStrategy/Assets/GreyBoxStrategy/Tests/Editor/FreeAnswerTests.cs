using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>
    /// The turn rework, slice 1, second part: <see cref="Game.Answer"/>, one realm's answer at a time, so each realm
    /// decides at its own pace while the person plans. The same game played through it in any interleaving ends where
    /// rounds through <see cref="Game.Submit"/> end.
    /// </summary>
    public sealed class FreeAnswerTests
    {
        private static GameRules Rules()
        {
            var r = ArmyRulesOff(Calm());
            r.Turn = TurnStyle.Writs;
            r.Victory = VictoryMode.Conquest;
            r.MaxTurns = 30;
            r.StalemateTurns = 20;
            r.FreeOrders = true;
            return r;
        }

        // A deterministic ruler: its answer depends only on what it is asked, never on when.
        private static Move Pick(CountryRequest r) =>
            r.Moment == Moment.Envoy ? new Move(Actions.RefusePeace)
            : r.Legal.OrderBy(m => (m.ToString() + r.CountryId + r.Turn + r.Ordered.Count).GetHashCode() & 0x7fffffff).First();

        private static string Play(bool byAnswer, int seed)
        {
            var g = new Game(seed, MapDefinition.Standard(), Rules());
            var rng = new Random(seed);
            var grids = new List<string>();
            while (!g.IsOver)
            {
                var requests = g.BeginTurn().ToList();
                if (byAnswer)
                {
                    // Realms answer in a random interleaving, each answer giving that realm's next request.
                    while (requests.Count > 0)
                    {
                        var r = requests[rng.Next(requests.Count)];
                        requests.Remove(r);
                        Assert.That(g.CouncilDone, Is.False);
                        if (g.Answer(new CountryAnswer(r, Pick(r))) is CountryRequest next)
                        {
                            Assert.That(next.CountryId, Is.EqualTo(r.CountryId), "the next request is the same realm's");
                            requests.Add(next);
                        }
                    }
                    Assert.That(g.CouncilDone, Is.True, "every realm done: the treaty answers are next");
                    var envoys = g.TreatyRequests;
                    if (envoys.Count > 0) g.Submit(envoys.Select(e => new CountryAnswer(e, Pick(e))).ToList());
                }
                else
                {
                    IReadOnlyList<CountryRequest> round = requests;
                    while (round.Count > 0) round = g.Submit(round.Select(x => new CountryAnswer(x, Pick(x))).ToList());
                }
                grids.Add(g.Resolve().OwnerGrid + "|" + string.Join(",", g.World.Armies.OrderBy(a => a.Id).Select(a => a.Id + a.Location + a.Soldiers)));
            }
            return string.Join("\n", grids);
        }

        [Test]
        public void AnsweringOneRealmAtATime_InAnyOrder_EndsWhereRoundsEnd()
        {
            foreach (int seed in new[] { 2, 5 })
                Assert.That(Play(byAnswer: true, seed), Is.EqualTo(Play(byAnswer: false, seed)), $"seed {seed}");
        }

        [Test]
        public void ARealmGoesOnWithoutWaitingForTheOthers()
        {
            var g = new Game(1, MapDefinition.Standard(), Rules());
            g.BeginTurn();
            g.World.Country("valen").Gold = 1000;
            var first = g.OpenRequests.First(r => r.CountryId == "valen");
            var second = g.Answer(new CountryAnswer(first, first.Legal.First(m => m.Action == Actions.Recruit)));
            Assert.That(second, Is.Not.Null);
            Assert.That(second.Moment, Is.EqualTo(Moment.Council), "valen's next court order, while the others have not answered");
            Assert.That(g.OpenRequests.Count(r => r.CountryId == "valen"), Is.EqualTo(1), "one open request a realm");
            Assert.That(g.OpenRequests.Count, Is.EqualTo(g.World.Alive.Count()));
            Assert.Throws<ArgumentException>(() => g.Answer(new CountryAnswer(first, Move.Hold)), "a request is answered once");
        }

        [Test]
        public void Answer_IsFreeOrdersOnly()
        {
            var r = Rules();
            r.FreeOrders = false;
            var g = new Game(1, MapDefinition.Standard(), r);
            var requests = g.BeginTurn();
            Assert.Throws<InvalidOperationException>(() => g.Answer(new CountryAnswer(requests[0], Move.Hold)));
        }
    }
}
