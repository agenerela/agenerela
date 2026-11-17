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
    /// A live game is played back from its decision log while it is still being written. These
    /// check that a game cut off mid-way builds the turns it has, and that the log's raw lines
    /// rebuild the same game the exported recording does.
    /// </summary>
    public class LiveReplayTests
    {
        private static string Json => File.ReadAllText(Path.Combine(Application.dataPath, "GreyBoxStrategy/Screen/Replays/seed-0004-personas-4b.json"));

        [Test]
        public void A_game_still_being_played_builds_the_turns_it_has()
        {
            var root = JObject.Parse(Json);
            var cut = new JArray(((JArray)root["decisions"]).Where(d => (int)d["turn"] <= 6));
            root["decisions"] = cut;
            var g = ReplayBuilder.Build(Recording.Parse(root.ToString()), partial: true);
            Assert.That(g.Turns.Count, Is.EqualTo(6));
            Assert.That(g.Result, Is.Null, "the game is not over");
            Assert.That(g.Turns.Last().Events, Is.Not.Empty);
        }

        [Test]
        public void An_empty_log_gives_the_starting_map()
        {
            var g = ReplayBuilder.Build(Recording.FromDecisionLog(new string[0], 77, new Dictionary<string, RecordedPlayer>()), partial: true);
            Assert.That(g.Turns, Is.Empty);
            Assert.That(g.Initial.Owners.Count(o => o == null), Is.EqualTo(10), "ten provinces start neutral");
        }

        [Test]
        public void The_decision_log_rebuilds_the_same_game_as_the_export()
        {
            // Turn the exported decisions back into the log's two lines per decision.
            var lines = new List<string>();
            foreach (JObject d in (JArray)JObject.Parse(Json)["decisions"])
            {
                var decided = (JObject)d.DeepClone();
                decided.Remove("turn"); decided.Remove("moment"); decided.Remove("execution");
                decided["stage"] = "Decided";
                lines.Add(decided.ToString(Newtonsoft.Json.Formatting.None));
                if (d["execution"] is JObject ex)
                {
                    var executed = new JObject { ["stage"] = "Executed", ["chainId"] = d["chainId"], ["agentId"] = d["agentId"], ["execution"] = ex };
                    lines.Add(executed.ToString(Newtonsoft.Json.Formatting.None));
                }
            }
            lines.Add("{\"chainId\":\"seed-4-turn-3-coun"); // a line cut off while being written
            var fromLog = ReplayBuilder.Build(Recording.FromDecisionLog(lines, 4, new Dictionary<string, RecordedPlayer>()));
            var fromExport = ReplayBuilder.Build(Recording.Parse(Json));
            Assert.That(fromLog.Result.Winner, Is.EqualTo(fromExport.Result.Winner));
            Assert.That(fromLog.Result.Turns, Is.EqualTo(fromExport.Result.Turns));
            Assert.That(fromLog.AllEvents.Select(e => e.Text), Is.EqualTo(fromExport.AllEvents.Select(e => e.Text)));
        }
    }
}
