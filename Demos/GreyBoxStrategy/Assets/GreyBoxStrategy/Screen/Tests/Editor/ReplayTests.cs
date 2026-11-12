using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreyBoxStrategy.Replay;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The screen plays back a recorded model game. These check that the playback is that game:
    /// rebuilt from its decisions alone, it ends as the record says, and its events move the map
    /// exactly as the rules did.
    /// </summary>
    public class ReplayTests
    {
        private const string RecordingPath = "GreyBoxStrategy/Screen/Replays/seed-0004-personas-4b.json";
        private static string Json => File.ReadAllText(Path.Combine(Application.dataPath, RecordingPath));
        private static ReplayGame Build() => ReplayBuilder.Build(Recording.Parse(Json));

        [Test]
        public void Seed_4_rebuilt_from_its_decisions_ends_as_the_table_recorded_it()
        {
            // game-0004.md of the v248 persona 4b table: kesh wins by domination on turn 17.
            var g = Build();
            Assert.That(g.Result.Winner, Is.EqualTo("kesh"));
            Assert.That(g.Result.Ending, Is.EqualTo(Ending.Domination));
            Assert.That(g.Result.Turns, Is.EqualTo(17));
            Assert.That(g.Turns.Count, Is.EqualTo(17));
        }

        [Test]
        public void Every_turn_changes_as_many_provinces_as_the_game_counted()
        {
            foreach (var t in Build().Turns)
                Assert.That(t.Events.Count(e => e.ChangesMap), Is.EqualTo(t.TerritoryChanges), $"turn {t.Turn}");
        }

        [Test]
        public void A_capture_moves_exactly_one_province_to_the_attacker()
        {
            var g = Build();
            int captures = 0;
            foreach (var t in g.Turns)
            {
                var before = t.Council;
                foreach (var e in t.Events.Where(x => x.Kind != ReplayEventKind.Upkeep && x.Kind != ReplayEventKind.Revolt && x.Kind != ReplayEventKind.TruceEnded))
                {
                    var changed = Enumerable.Range(0, before.Owners.Length).Where(i => before.Owners[i] != e.After.Owners[i]).ToList();
                    if (e.Kind == ReplayEventKind.Capture)
                    {
                        captures++;
                        Assert.That(changed, Is.EqualTo(new[] { e.ProvinceIndex }), $"turn {t.Turn}: {e.Text}");
                        Assert.That(e.After.Owners[e.ProvinceIndex], Is.EqualTo(e.Country));
                    }
                    else Assert.That(changed, Is.Empty, $"turn {t.Turn}: {e.Kind} {e.Text}");
                    before = e.After;
                }
            }
            Assert.That(captures, Is.GreaterThan(10));
        }

        [Test]
        public void Statements_come_from_the_decision_that_made_the_move()
        {
            var first = Build().Turns[0].Events.First(e => e.Country == "valen" && e.Kind == ReplayEventKind.Fort);
            Assert.That(first.Province, Is.EqualTo("ashby"));
            Assert.That(first.Statement, Is.EqualTo("We will fortify ashby to strengthen our border against the neutral garrisons while preserving our gold."));
            Assert.That(first.Decision.ModelName, Is.EqualTo("qwen3.5:4b"));
        }

        [Test]
        public void A_recording_that_does_not_fit_the_rules_is_refused_not_shown()
        {
            // Drop one council decision: the game asks for it, and the replay must say so rather
            // than invent a move.
            var root = JObject.Parse(Json);
            var decisions = (JArray)root["decisions"];
            decisions.First(d => (int)d["turn"] == 3 && (string)d["moment"] == "council").Remove();
            Assert.Throws<InvalidOperationException>(() => ReplayBuilder.Build(Recording.Parse(root.ToString())));
        }

        [Test]
        public void The_chronicle_filters_by_country_and_kind_newest_first()
        {
            var g = Build();
            var lines = g.AllEvents.Select(e => Chronicle.Line(e, id => char.ToUpperInvariant(id[0]) + id.Substring(1))).Where(l => l != null).ToList();
            var all = new HashSet<ChronicleCategory>((ChronicleCategory[])Enum.GetValues(typeof(ChronicleCategory)));
            var kesh = Chronicle.Filter(lines, new HashSet<string> { "kesh" }, new HashSet<ChronicleCategory> { ChronicleCategory.Land });
            Assert.That(kesh, Is.Not.Empty);
            Assert.That(kesh.All(l => l.Country == "kesh" && l.Category == ChronicleCategory.Land));
            Assert.That(kesh.First().Turn, Is.GreaterThanOrEqualTo(kesh.Last().Turn));
            Assert.That(kesh.First().Text, Does.StartWith("Kesh takes "));
            var none = Chronicle.Filter(lines, new HashSet<string>(), all);
            Assert.That(none.All(l => l.Country == null));
        }
    }
}
