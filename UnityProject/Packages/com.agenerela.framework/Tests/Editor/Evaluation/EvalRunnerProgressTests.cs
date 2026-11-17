using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Evaluation;
using NUnit.Framework;
using static Agenerela.Tests.Evaluation.RunnerFixtures;

namespace Agenerela.Tests.Evaluation
{
    // What the runner tells a window while it runs, and what a cancel leaves (#146): progress in
    // order with its counts, and a partial report marked cancelled, carried by an exception every
    // existing catch of OperationCanceledException still catches.
    public sealed class EvalRunnerProgressTests
    {
        private RunnerFixtures fixtures;

        private static readonly string Bridge = Prompt("target_001", "Meet me over at the bridge.", "move_to", "bridge", false);
        private static readonly string Escort = Prompt("target_002", "Walk with me a while.", "follow_player", "no_target", false);

        [SetUp]
        public void SetUp() => fixtures = new RunnerFixtures();

        [TearDown]
        public void TearDown()
        {
            fixtures.Dispose();
            Fixtures.DestroyCreated();
        }

        // ---- Progress ---------------------------------------------------------------------------

        [Test]
        public async Task AnInterleavedRunReportsEachPhaseInOrderAndEveryAnswer()
        {
            var runner = Runner(new FakeProvider(Reply("move_to", "bridge")));
            var seen = new List<string>();
            runner.Progress = p => seen.Add(p.ToString());

            await Fixtures.NotCancelled(runner.RunAsync());

            Assert.That(seen, Is.EqualTo(new[]
            {
                "Checking both arms 0/4",
                "Checking A 0/4", "WarmUp A 0/4",
                "Checking B 0/4", "WarmUp B 0/4",
                "Asking A 1/4", "Asking B 2/4", "Asking A 3/4", "Asking B 4/4",
            }));
        }

        [Test]
        public async Task ASequentialRunReportsTheUnloadBetweenItsArms()
        {
            var server = new FakeOllama();
            var runner = new EvalRunner(fixtures.Set(new[] { Bridge, Escort }), Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Ollama(server, Model2B, "OllamaA")),
                new EvalArm("B", fixtures.Ollama(server, Model4B, "OllamaB")))
            {
                Order = EvalArmOrder.Sequential,
            };
            var seen = new List<string>();
            runner.Progress = p => seen.Add(p.ToString());

            await Fixtures.NotCancelled(runner.RunAsync());

            Assert.That(seen, Is.EqualTo(new[]
            {
                "Checking both arms 0/4",
                "Checking A 0/4", "WarmUp A 0/4", "Asking A 1/4", "Asking A 2/4",
                "Unloading A 2/4",
                "Checking B 2/4", "WarmUp B 2/4", "Asking B 3/4", "Asking B 4/4",
            }));
        }

        // ---- Cancel -----------------------------------------------------------------------------

        [Test]
        public async Task ACancelMidRunLeavesAPartialReportMarkedCancelled()
        {
            var (runner, cancel) = CancelledAfter(3);

            EvalCancelledException e = null;
            try
            {
                await runner.RunAsync(cancel.Token);
            }
            catch (EvalCancelledException thrown)
            {
                e = thrown;
            }

            Assert.That(e, Is.Not.Null, "The run ended without a cancel.");
            var partial = e.Partial;
            Assert.That(partial.Cancelled, Is.True);
            Assert.That(partial.Answers.Select(a => a.Arm + " " + a.PromptId), Is.EqualTo(new[] { "A target_001", "B target_001", "A target_002" }));
            Assert.That(partial.Arms[0].Overall.Total, Is.EqualTo(2));
            Assert.That(partial.Arms[1].Overall.Total, Is.EqualTo(1));
            Assert.That(partial.Noise, Is.Empty, "The arms were not asked the same prompts, so there is nothing to compare.");
            Assert.That(partial.Conditions.FinishedUtc, Is.Not.Null);
            Assert.That(partial.ToMarkdown(), Does.Contain("**Cancelled after 3 of 4 answers.**"));
            Assert.That(e.Message, Does.Contain("3 of 4"));

            var back = EvalReport.FromJson(partial.ToJson());
            Assert.That(back.Cancelled, Is.True, "A saved partial report still says it was cancelled.");
        }

        [Test]
        public async Task ACancelBetweenTwoPromptsComparesTheArmsOnWhatBothWereAsked()
        {
            var (runner, cancel) = CancelledAfter(2);

            try
            {
                await runner.RunAsync(cancel.Token);
                Assert.Fail("The run ended without a cancel.");
            }
            catch (EvalCancelledException e)
            {
                Assert.That(e.Partial.Noise.Select(n => n.Scope), Is.EqualTo(new[] { "all", "command_with_target" }));
                Assert.That(e.Partial.Noise[0].N, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task AFinishedRunIsNotCancelledAndSaysNothingAboutIt()
        {
            var report = await Fixtures.NotCancelled(Runner(new FakeProvider(Reply("move_to", "bridge"))).RunAsync());

            Assert.That(report.Cancelled, Is.False);
            Assert.That(report.ToJson(), Does.Not.Contain("cancelled"), "A finished report's JSON is as it was before #146.");
            Assert.That(report.ToMarkdown(), Does.Not.Contain("Cancelled"));
        }

        [Test]
        public async Task AnExistingCatchOfOperationCanceledExceptionStillCatchesIt()
        {
            var (runner, cancel) = CancelledAfter(1);

            OperationCanceledException caught = null;
            try
            {
                await runner.RunAsync(cancel.Token);
            }
            catch (OperationCanceledException e)
            {
                caught = e;
            }

            Assert.That(caught, Is.InstanceOf<EvalCancelledException>());
            Assert.That(caught.CancellationToken, Is.EqualTo(cancel.Token));
        }

        [Test]
        public async Task FixturesNotCancelledStillSeesACancel()
        {
            var (runner, cancel) = CancelledAfter(1);

            string failure = null;
            try
            {
                await Fixtures.NotCancelled(runner.RunAsync(cancel.Token));
            }
            catch (AssertionException e)
            {
                failure = e.Message;
            }

            Assert.That(failure, Does.Contain("ended cancelled"));
        }

        // A two-prompt interleaved run whose token is cancelled once `answers` answers are in.
        private (EvalRunner, CancellationTokenSource) CancelledAfter(int answers)
        {
            var runner = Runner(new FakeProvider(Reply("move_to", "bridge")));
            var cancel = new CancellationTokenSource();
            runner.Progress = p =>
            {
                if (p.Phase == EvalPhase.Asking && p.Answered == answers)
                {
                    cancel.Cancel();
                }
            };

            return (runner, cancel);
        }

        private EvalRunner Runner(FakeProvider provider)
        {
            return new EvalRunner(fixtures.Set(new[] { Bridge, Escort }), Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Config(provider, "FakeA")),
                new EvalArm("B", fixtures.Config(provider, "FakeB"), new DecideOptions { CheckTarget = false }));
        }
    }
}
