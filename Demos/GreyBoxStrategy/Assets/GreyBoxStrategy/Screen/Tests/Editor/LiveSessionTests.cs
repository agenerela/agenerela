using System;
using System.IO;
using System.Threading.Tasks;
using Agenerela.Providers;
using GreyBoxStrategy.Realm;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// A live game with the model replaced by a ScriptedProvider: five seats that always hold. It
    /// covers the three-turn stop, which the first run on the RTX 5080 missed (the model played on
    /// to turn 21 while the screen counted turns played back).
    /// </summary>
    public class LiveSessionTests
    {
        private string folder;

        [SetUp] public void SetUp() => folder = Path.Combine(Application.temporaryCachePath, "live-session-tests-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { try { Directory.Delete(folder, true); } catch (IOException) { } }

        private LiveSession Session(int stopAfter, ScriptedProvider provider) =>
            new LiveSession("qwen3.5:4b", "default", 11, stopAfter, Path.Combine(folder, "decisions.jsonl"), (m, w) => provider);

        [Test]
        public async Task A_short_check_stops_the_model_after_its_turns()
        {
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide("none", statement: "We wait."));
            var s = Session(3, provider);
            await s.RunAsync();
            Assert.That(s.Error, Is.Null);
            Assert.That(s.Stopped, Is.True, "stopped, not finished");
            Assert.That(s.Result, Is.Null);
            var g = s.Replay();
            Assert.That(g.Turns.Count, Is.EqualTo(3), "turns 1 to 3 played and logged, turn 4 never asked");
            Assert.That(provider.RequestCount, Is.EqualTo(3 * 5), "five rulers a turn, three turns");
        }

        [Test]
        public async Task Without_a_limit_the_game_plays_to_its_end()
        {
            var s = Session(0, new ScriptedProvider().Otherwise(ScriptedReply.Decide("none")));
            await s.RunAsync();
            Assert.That(s.Error, Is.Null);
            Assert.That(s.Stopped, Is.False);
            Assert.That(s.Result, Is.Not.Null);
            Assert.That(s.Replay().Turns.Count, Is.EqualTo(30), "five rulers that always hold reach the turn limit");
            Assert.That(s.Players["kesh"].Model, Is.EqualTo("qwen3.5:4b"));
            Assert.That(s.Players["kesh"].Persona, Is.Not.Null.And.Not.Empty);
        }
    }
}
