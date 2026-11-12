using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>The dev menu's non-visual parts: its edits, its command line, its pretended faults.</summary>
    public class DevTests
    {
        private string folder;

        [SetUp] public void SetUp() { folder = Path.Combine(Application.temporaryCachePath, "dev-tests-" + Guid.NewGuid().ToString("N")); DevFaults.Clear(); }
        [TearDown] public void TearDown() { DevFaults.Clear(); try { Directory.Delete(folder, true); } catch (IOException) { } }

        [Test]
        public void Edits_change_the_world_within_its_rules()
        {
            var g = new Game(11, MapDefinition.Standard());
            var w = g.World;
            int gold = w.Country("kesh").Gold;
            Assert.That(DevEdits.Apply(g, "gold kesh +100"), Is.Null);
            Assert.That(w.Country("kesh").Gold, Is.EqualTo(gold + 100));
            Assert.That(DevEdits.Apply(g, "stability kesh =9"), Is.Null);
            Assert.That(w.Country("kesh").Stability, Is.EqualTo(w.Rules.MaxStability), "clamped to the rules' range");
            Assert.That(DevEdits.Apply(g, "army kesh -999"), Is.Null);
            Assert.That(w.Country("kesh").Army, Is.EqualTo(0));
            Assert.That(DevEdits.Apply(g, "owner harrow kesh"), Is.Null);
            Assert.That(w.Province("harrow").Owner, Is.EqualTo("kesh"));
            Assert.That(DevEdits.Apply(g, "war kesh valen"), Is.Null);
            Assert.That(w.AtWar("kesh", "valen"), Is.True);
            Assert.That(DevEdits.Apply(g, "eliminate ilvar"), Is.Null);
            Assert.That(w.Country("ilvar").Alive, Is.False);
            Assert.That(w.ProvinceCount("ilvar"), Is.EqualTo(0));
            Assert.That(DevEdits.Apply(g, "gold ilvar +5"), Does.Contain("out of the game"));
            Assert.That(DevEdits.Apply(g, "gold atlantis +5"), Does.StartWith("usage"));
            Assert.That(DevEdits.Apply(g, "teleport kesh"), Does.StartWith("unknown edit"));
        }

        [Test]
        public void An_event_fires_at_the_next_upkeep_and_the_table_comes_back()
        {
            var g = new Game(11, MapDefinition.Standard());
            for (int i = 0; i < 2; i++) { g.BeginTurn(); g.SubmitCouncil(null); g.Resolve(); } // the rules allow no event before turn 3
            var t = EventRules.Templates.First(x => x.Id == EventRules.Windfall);
            Assert.That(DevEdits.Apply(g, $"event {t.Id} kesh"), Is.Null);
            Assert.That(DevEdits.Apply(g, $"event {t.Id} valen"), Is.Not.Null, "one event a turn, refused rather than thrown");
            g.BeginTurn();
            DevEdits.AfterBeginTurn(g);
            Assert.That(g.Events.Told.Any(e => e.Template == t.Id && e.Target == "kesh"), Is.True);
            Assert.That(g.EventTable, Is.True, "the random table is on again for the turns after");
        }

        [Test]
        public async Task A_dev_edit_in_a_rule_game_replays_exactly_and_marks_the_game()
        {
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide("none"));
            var s = new RuleSession("kesh", "qwen3.5:4b", "default", 11, Path.Combine(folder, "decisions.jsonl"), (m, w) => provider);
            var run = s.RunAsync();
            for (int i = 0; i < 2000 && s.WaitingFor != RuleSession.Waiting.Orders; i++) await Task.Delay(5);
            Assert.That(s.QueueDev("gold kesh +500"), Is.Null);
            Assert.That(s.QueueDev("owner harrow kesh"), Is.Null);
            Assert.That(s.QueueDev("gold nobody +1"), Is.Not.Null, "checked when queued");
            s.Order(Move.Hold);
            for (int i = 0; i < 2000 && !(s.Resolved == 1 && s.WaitingFor == RuleSession.Waiting.Orders); i++) await Task.Delay(5);
            Assert.That(s.Game.World.Country("kesh").Gold, Is.GreaterThan(500), "turn 2 began with the edits");
            s.Order(Move.Hold);
            for (int i = 0; i < 2000 && s.Resolved < 2; i++) await Task.Delay(5);

            Assert.That(s.Dev, Is.True);
            Assert.That(s.DevApplied.Select(e => e.Turn), Is.EqualTo(new[] { 2, 2 }), "applied at the start of the next turn");
            var g = s.BuildReplay();
            Assert.That(g.Dev, Is.True, "the replay knows it is a dev game");
            Assert.That(g.Turns[1].Events.Count(e => e.Kind == ReplayEventKind.Dev), Is.EqualTo(2), "each edit is a line in the chronicle");
            Assert.That(g.Turns[1].End.Owners[g.IndexOf("harrow")], Is.EqualTo("kesh"), "the replay lands where the game did");

            // Saved and loaded, it replays the same.
            var rec = s.BuildRecording();
            var back = ReplayBuilder.Build(Recording.Parse(rec.ToJson()), partial: true);
            Assert.That(back.Dev, Is.True);
            Assert.That(back.Turns.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(back.Turns[1].End.Owners, Is.EqualTo(g.Turns[1].End.Owners));
            s.Stop();
            await run;
        }

        [Test]
        public void The_command_line_answers_in_text_never_throws()
        {
            var c = new DevCommands()
                .Add("speed", "speed <x>", "speed", a => float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture).ToString())
                .Add("need", "need <a>", "throws a usage error", a => throw new ArgumentException("Give one."));
            Assert.That(c.Run("speed 4"), Is.EqualTo("4"));
            Assert.That(c.Run("warp 9"), Does.StartWith("Unknown command 'warp'").And.Contain("speed"));
            Assert.That(c.Run("speed fast"), Is.EqualTo("Usage: speed <x>"));
            Assert.That(c.Run("need"), Is.EqualTo("Give one. Usage: need <a>"));
            Assert.That(c.Run(""), Does.StartWith("Commands:"));
        }

        [Test]
        public void A_pretended_lock_never_touches_a_real_one()
        {
            string held = Path.Combine(folder, "held");
            DevFaults.FakeLockHolder = "another session";
            Assert.That(Gateway.TakeLock(held, "test", "test", null), Is.False, "the game waits as if someone held it");
            Assert.That(Directory.Exists(held), Is.False, "and nothing is written");
            Assert.That(Gateway.ReadLock(held).Owner, Is.EqualTo("another session"));
            DevFaults.Clear();
            Assert.That(Gateway.ReadLock(held), Is.Null);
        }

        [Test]
        public async Task Pretended_replies_are_unreadable_or_hang_until_cancelled()
        {
            var inner = new ScriptedProvider().Otherwise(ScriptedReply.Decide("none"));
            var p = DevProvider.Wrap(inner);
            DevFaults.Unreadable = true;
            var r = await p.RequestAsync(null, CancellationToken.None);
            Assert.That(r.Text, Does.Contain("unreadable"));
            Assert.That(inner.RequestCount, Is.EqualTo(0), "the model is never asked");
            DevFaults.Unreadable = false;

            DevFaults.Hang = true;
            var cts = new CancellationTokenSource();
            var hung = p.RequestAsync(null, cts.Token);
            await Task.Delay(100);
            Assert.That(DevProvider.InFlight, Is.EqualTo(1));
            cts.Cancel();
            bool cancelled = false;
            try { await hung; } catch (OperationCanceledException) { cancelled = true; }
            Assert.That(cancelled, Is.True, "a hung request ends when its round is cancelled");
            Assert.That(DevProvider.InFlight, Is.EqualTo(0));
        }
    }
}
