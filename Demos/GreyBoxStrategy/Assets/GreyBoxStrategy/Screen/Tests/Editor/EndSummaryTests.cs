using System.IO;
using System.Linq;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>The end screen's summary, on a whole writs game and on the recorded Classic game.</summary>
    public class EndSummaryTests
    {
        private static ReplayGame writs, classic;

        [OneTimeSetUp]
        public void Make()
        {
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            writs = ReplayBuilder.Build(Recording.Parse(ScriptedWorldGame.Play(setup, map, rules).ToJson()), map, rules);
            classic = ReplayBuilder.Build(Recording.Parse(File.ReadAllText(
                Path.Combine(Application.dataPath, "GreyBoxStrategy/Screen/Replays/seed-0004-personas-4b.json"))));
        }

        private static ReplayGame[] Games => new[] { writs, classic };

        [Test]
        public void The_land_chart_runs_from_the_start_to_the_result()
        {
            foreach (var g in Games)
            {
                var s = EndSummary.From(g, id => id);
                Assert.That(g.Result, Is.Not.Null);
                foreach (var c in g.CountryIds)
                {
                    var held = s.Held[c];
                    Assert.That(held.Count, Is.EqualTo(g.Turns.Count + 1), $"{c}: the start and every turn");
                    Assert.That(held[0], Is.EqualTo(g.Initial.Owners.Count(o => o == c)), $"{c} at the start");
                    Assert.That(held[held.Count - 1], Is.EqualTo(g.Result.Provinces.TryGetValue(c, out int n) ? n : 0), $"{c} at the end is the result's count");
                }
                Assert.That(s.Winner, Is.EqualTo(g.Result.Winner));
                Assert.That(s.Rulers[0].Country, Is.EqualTo(g.Result.Winner), "the winner first");
                Assert.That(s.How, Is.Not.Empty);
            }
        }

        [Test]
        public void The_timeline_keeps_every_fall_and_is_short_and_in_order()
        {
            foreach (var g in Games)
            {
                var s = EndSummary.From(g, id => id);
                var falls = g.AllEvents.Where(e => e.Kind == ReplayEventKind.Eliminated).ToList();
                foreach (var e in falls) Assert.That(s.Timeline.Any(m => m.Kind == "fall" && m.Country == e.Country && m.Turn == e.Turn), Is.True, $"{e.Country}'s fall, as the rules call it");
                // A realm that ends with nothing, and had land, fell: in the timeline and on its ruler.
                foreach (var c in g.CountryIds.Where(c => c != g.Result.Winner && s.Held[c][0] > 0 && s.Held[c].Last() == 0))
                {
                    Assert.That(s.Timeline.Count(m => m.Kind == "fall" && m.Country == c), Is.EqualTo(1), $"{c} falls once");
                    Assert.That(s.Rulers.Single(r => r.Country == c).Fell, Is.GreaterThan(0), $"{c}'s ruler fell");
                }
                Assert.That(s.Timeline.Count, Is.LessThanOrEqualTo(EndSummary.TimelineMax));
                Assert.That(s.Timeline.Select(m => m.Turn), Is.Ordered, "earliest first");
                var pairs = s.Timeline.Where(m => m.Kind == "peace").Select(m => m.Turn + ":" + string.Join("|", new[] { m.Country, m.Other }.OrderBy(x => x))).ToList();
                Assert.That(pairs, Is.Unique, "one line for a pair's peace on a turn");
                foreach (var r in s.Rulers.Where(r => r.Fell > 0))
                    Assert.That(falls.Any(e => e.Country == r.Country && e.Turn == r.Fell)
                                || (s.Held[r.Country][r.Fell] == 0 && s.Held[r.Country].Skip(r.Fell).All(n => n == 0) && s.Held[r.Country][r.Fell - 1] > 0), Is.True,
                        $"{r.Country} fell on turn {r.Fell}: the rules said so, or it held nothing from then on");
            }
            Assert.That(EndSummary.From(writs, id => id).Timeline.Count, Is.GreaterThan(0), "a whole writs game has wars and treaties to show");
        }

        [Test]
        public void Each_ruler_speaks_in_its_own_last_lines()
        {
            foreach (var g in Games)
            {
                var s = EndSummary.From(g, id => id);
                Assert.That(s.Rulers.Select(r => r.Country), Is.EquivalentTo(g.CountryIds));
                foreach (var r in s.Rulers)
                {
                    var own = g.AllEvents.Where(e => e.Country == r.Country && e.Decision?.Statement != null).Select(e => e.Decision.Statement.Trim()).Where(x => x.Length > 0).ToList();
                    Assert.That(r.Lines.Count, Is.LessThanOrEqualTo(2));
                    Assert.That(r.Lines, Is.Unique);
                    Assert.That(r.Lines.All(own.Contains), Is.True, $"{r.Country} says only its own lines");
                    if (own.Count > 0) Assert.That(r.Lines.Last(), Is.EqualTo(own.Last()), $"{r.Country}: its last line is its last word");
                    Assert.That(r.Persona, Is.EqualTo(g.Players?[r.Country]?.Persona));
                }
            }
        }
    }
}
