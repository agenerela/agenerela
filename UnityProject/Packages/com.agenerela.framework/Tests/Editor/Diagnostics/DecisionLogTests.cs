using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // The runtime decision log (#190, G7): one JSON line per decision feed entry, written off the
    // main thread, read back by DecisionLogReader.
    public sealed class DecisionLogTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

        private string folder;
        private string path;
        private DecisionLog log;

        [SetUp]
        public void MakeFolder()
        {
            folder = Path.Combine(Path.GetTempPath(), "agenerela-log-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(folder, "decisions.jsonl");
        }

        [TearDown]
        public void CleanUp()
        {
            log?.Stop();
            log = null;
            Fixtures.DestroyCreated();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        private List<DecisionLogLine> Lines()
        {
            Assert.That(log.Flush(Wait), Is.True, "The log wrote everything queued.");
            return DecisionLogReader.Read(path);
        }

        private string[] RawLines()
        {
            Assert.That(log.Flush(Wait), Is.True);
            return File.ReadAllLines(path);
        }

        // ---- One line per entry --------------------------------------------------------------

        [Test]
        public async Task ADecisionIsOneLineWithWhatTheGameGot()
        {
            var fake = new FakeProvider(GoToTheTower) { PromptTokens = 120, CompletionTokens = 14 };
            var agent = Fixtures.VillageGuard(fake);
            agent.Id = "guard_north";
            log = DecisionLog.Start(path);

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { StimulusLabel = "Captain" }));

            var raw = RawLines();
            Assert.That(raw, Has.Length.EqualTo(1), "One entry, one line.");
            var json = JObject.Parse(raw[0]);
            Assert.That((int)json["version"], Is.EqualTo(DecisionLogLine.CurrentVersion));
            Assert.That((string)json["stage"], Is.EqualTo("Decided"), "Enums are written by name.");

            var line = DecisionLogReader.Read(path).Single();
            Assert.That(line.Sequence, Is.GreaterThan(0));
            Assert.That(line.Time.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(line.AgentId, Is.EqualTo("guard_north"));
            Assert.That(line.AgentName, Is.EqualTo("Village Guard"));
            Assert.That(line.Label, Is.EqualTo("Captain"));
            Assert.That(line.Stimulus, Is.EqualTo("Go to the tower."), "The stimulus without its label.");
            Assert.That(line.Decision.Action, Is.EqualTo("move_to"));
            Assert.That(line.Decision.Target, Is.EqualTo("tower"));
            Assert.That(line.Decision.Statement, Is.EqualTo("On my way."));
            Assert.That(line.ModelDecision, Is.Null, "No guard changed it.");
            Assert.That(line.Verdict, Is.Null);
            Assert.That(line.Execution, Is.Null, "Nothing executed it yet.");
            Assert.That(line.Error, Is.Null);
            Assert.That(line.Provider, Is.EqualTo("fake"));
            Assert.That(line.PromptTokens, Is.EqualTo(120));
            Assert.That(line.CompletionTokens, Is.EqualTo(14));
            Assert.That(line.Reply, Is.EqualTo(GoToTheTower), "The reply as the provider sent it, for a replay.");
            Assert.That(line.FrameworkVersion, Is.EqualTo(AgenerelaInfo.Version));
        }

        [Test]
        public async Task AnExecutionIsOneMoreLineSayingWhatItDid()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            log = DecisionLog.Start(path);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            Assert.That(agent.Execute(result), Is.True);

            var lines = Lines();
            Assert.That(lines, Has.Count.EqualTo(2));
            Assert.That(lines[1].Stage, Is.EqualTo(DecisionStage.Executed));
            Assert.That(lines[1].Sequence, Is.GreaterThan(lines[0].Sequence));
            Assert.That(lines[1].Execution, Is.Not.Null);
            Assert.That(lines[1].Execution.Status, Is.EqualTo(ExecutionStatus.Done));
            Assert.That(lines[1].Decision.Action, Is.EqualTo("move_to"));
            Assert.That(lines[0].Execution, Is.Null,
                "The decision's line was copied when it arrived, before Execute changed the telemetry.");
        }

        [Test]
        public async Task AGuardsRewriteShowsTheModelsAnswerAndTheFinalOne()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Guards.Add(FakeGuard.Rewriting("NoTowers", "towers are closed."));
            log = DecisionLog.Start(path);

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            var line = Lines().Single();
            Assert.That(line.Decision.Action, Is.EqualTo("none"), "What the game got.");
            Assert.That(line.Decision.Target, Is.EqualTo("no_target"));
            Assert.That(line.ModelDecision, Is.Not.Null, "What the model answered, since a guard changed it.");
            Assert.That(line.ModelDecision.Action, Is.EqualTo("move_to"));
            Assert.That(line.ModelDecision.Target, Is.EqualTo("tower"));
            Assert.That(line.Verdict.Guard, Is.EqualTo("NoTowers"));
            Assert.That(line.Verdict.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(line.Verdict.Reason, Is.EqualTo("towers are closed."));
            Assert.That(line.GuardsFired, Is.EqualTo(new[] { "NoTowers" }));
        }

        [Test]
        public async Task APipelineErrorHasItsKindAndNoDecision()
        {
            var fake = new FakeProvider(GoToTheTower) { Failure = new InvalidOperationException("the backend at https://user:secret@example.com/ is gone") };
            var agent = Fixtures.VillageGuard(fake);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide"));
            log = DecisionLog.Start(path);

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            var line = Lines().Single();
            Assert.That(line.Decision, Is.Null);
            Assert.That(line.Error, Is.Not.Null);
            Assert.That(line.Error.Kind, Is.EqualTo(PipelineErrorKind.ProviderFailed));
            Assert.That(File.ReadAllText(path), Does.Not.Contain("secret"), "Credentials in a URL are masked.");
        }

        // ---- The prompt ----------------------------------------------------------------------

        [Test]
        public async Task ThePromptIsAbsentUnlessSwitchedOn()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            log = DecisionLog.Start(path);

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            log.IncludePrompt = true;
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower again."));

            var raw = RawLines();
            Assert.That(raw[0], Does.Not.Contain("\"request\""), "Off by default.");
            Assert.That(raw[0], Does.Not.Contain("Village Guard, a guard"), "No part of the system prompt either.");

            var on = DecisionLogReader.Read(path)[1].Request;
            Assert.That(on, Is.Not.Null, "Switched on, from the next entry.");
            Assert.That(on.SystemPrompt, Does.Contain("Village Guard"));
            Assert.That(on.StimulusTurn, Does.Contain("Go to the tower again."));
            Assert.That(on.Actions, Does.Contain("move_to"));
            Assert.That(on.Targets, Does.Contain("tower").And.Contain("no_target"));
        }

        // ---- Failures ------------------------------------------------------------------------

        [Test]
        public async Task AWriteFailureNeverReachesTheCallerAndIsReportedOnce()
        {
            // A file where the log's folder should be: creating the folder fails on every write.
            Directory.CreateDirectory(folder);
            var blocker = Path.Combine(folder, "blocker");
            File.WriteAllText(blocker, "not a folder");
            var warnings = 0;
            void Count(string message, string stack, LogType type)
            {
                if (type == LogType.Warning && message.Contains("decision log could not write"))
                {
                    Interlocked.Increment(ref warnings);
                }
            }

            Application.logMessageReceivedThreaded += Count;
            try
            {
                var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
                log = DecisionLog.Start(Path.Combine(blocker, "decisions.jsonl"));

                var first = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
                Assert.That(log.Flush(Wait), Is.True);
                var second = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower again."));
                Assert.That(log.Flush(Wait), Is.True);

                Assert.That(first.Decision.ActionId, Is.EqualTo("move_to"), "The game got its decisions.");
                Assert.That(second.Decision.ActionId, Is.EqualTo("move_to"));
                Assert.That(log.LinesDropped, Is.EqualTo(2));
                Assert.That(log.LinesWritten, Is.EqualTo(0));
                Assert.That(log.LastError, Is.Not.Null);
                Assert.That(Volatile.Read(ref warnings), Is.EqualTo(1), "Reported once, not per line.");
            }
            finally
            {
                Application.logMessageReceivedThreaded -= Count;
            }
        }

        // ---- Reading back --------------------------------------------------------------------

        [Test]
        public async Task TheReaderRoundTripsWhatTheWriterWrote()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Guards.Add(FakeGuard.Rewriting("NoTowers"));
            log = DecisionLog.Start(path, includePrompt: true);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            agent.Execute(result);

            var lines = Lines();
            var again = lines.Select(l => Newtonsoft.Json.JsonConvert.SerializeObject(l, DecisionLogFormat.Settings)).ToArray();
            Assert.That(again, Is.EqualTo(File.ReadAllLines(path)), "Reading a line and writing it again gives the same line.");
        }

        [Test]
        public async Task APartialLastLineIsSkippedAndTheNextLogStartsOnANewLine()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, "{\"version\":1,\"sequence\":7,\"stage\":\"Deci");
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            log = DecisionLog.Start(path);

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            var lines = Lines();
            DecisionLogReader.Read(path, out var skipped);
            Assert.That(skipped, Is.EqualTo(1), "The game that died mid-line left one line nobody can read.");
            Assert.That(lines, Has.Count.EqualTo(1), "The new line did not run into it.");
            Assert.That(lines[0].Decision.Action, Is.EqualTo("move_to"));
        }

        [Test]
        public void AJsonObjectWithoutAVersionIsNotALine()
        {
            Assert.That(DecisionLogReader.TryParse("{\"sequence\":3}", out var line), Is.False);
            Assert.That(line, Is.Null);
            Assert.That(DecisionLogReader.TryParse("{\"version\":1,\"sequence\":3,\"future\":true}", out line), Is.True,
                "A field this version does not know is ignored.");
            Assert.That(line.Sequence, Is.EqualTo(3));
        }

        // ---- Several agents, growth, stopping ------------------------------------------------

        [Test]
        public async Task LinesFromTwoAgentsInterleaveWithoutCorruption()
        {
            var north = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            north.Id = "north";
            var south = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            south.Id = "south";
            var northResult = await Fixtures.NotCancelled(north.DecideAsync("Go to the tower."));
            var southResult = await Fixtures.NotCancelled(south.DecideAsync("Go to the tower."));
            log = DecisionLog.Start(path, includePrompt: true);

            // Entries arriving from many threads at once, as Execute may raise them.
            const int each = 200;
            var raised = 0L;
            Parallel.For(0, each * 2, i =>
            {
                var agent = i % 2 == 0 ? north : south;
                var result = i % 2 == 0 ? northResult : southResult;
                log.Write(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, Interlocked.Increment(ref raised)));
            });

            var raw = RawLines();
            Assert.That(raw, Has.Length.EqualTo(each * 2), "One line per entry.");
            var lines = DecisionLogReader.Parse(raw, out var skipped);
            Assert.That(skipped, Is.Zero, "Every line whole.");
            Assert.That(lines.Count(l => l.AgentId == "north"), Is.EqualTo(each));
            Assert.That(lines.Count(l => l.AgentId == "south"), Is.EqualTo(each));
            Assert.That(lines.Select(l => l.Sequence).Distinct().Count(), Is.EqualTo(each * 2));
        }

        [Test]
        public async Task AFullFileRollsOverOnce()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            log = DecisionLog.Start(path);
            log.MaxFileBytes = 1;

            for (var i = 0; i < 3; i++)
            {
                await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower, " + i + "."));
                Assert.That(log.Flush(Wait), Is.True);
            }

            var rolled = DecisionLog.RolloverPath(path);
            Assert.That(rolled, Is.EqualTo(Path.Combine(folder, "decisions.1.jsonl")));
            Assert.That(DecisionLogReader.Read(path).Single().Stimulus, Is.EqualTo("Go to the tower, 2."), "The newest line, alone.");
            Assert.That(DecisionLogReader.Read(rolled).Single().Stimulus, Is.EqualTo("Go to the tower, 1."),
                "One rollover file, holding the line before; the oldest is gone.");
            Assert.That(Directory.GetFiles(folder), Has.Length.EqualTo(2));
        }

        [Test]
        public async Task StoppingTheLogUnsubscribesIt()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            log = DecisionLog.Start(path);

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            log.Stop();
            Assert.That(log.IsRunning, Is.False);
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower again."));

            Assert.That(DecisionLogReader.Read(path), Has.Count.EqualTo(1), "Stop wrote what was queued, and nothing after.");
            Assert.That(log.LinesWritten, Is.EqualTo(1));
            Assert.DoesNotThrow(() => log.Stop(), "Stopping twice is harmless.");
        }
    }
}
