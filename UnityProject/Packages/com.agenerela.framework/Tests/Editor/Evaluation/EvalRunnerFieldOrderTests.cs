using System.Linq;
using System.Threading.Tasks;
using Agenerela.Evaluation;
using Agenerela.Providers;
using NUnit.Framework;
using static Agenerela.Tests.Evaluation.RunnerFixtures;

namespace Agenerela.Tests.Evaluation
{
    // The runner's field-order check (#180): each reply's order is counted per arm, an arm with any
    // out of order is warned about, and warm-ups out of order refuse the run unless AllowOrderLost
    // is on. The stand-in Ollama answers as the model would through Ollama's native template path,
    // whose grammar has the schema's keys sorted (findings, 5 November 2026). Every run is awaited
    // through Fixtures.NotCancelled.
    public sealed class EvalRunnerFieldOrderTests
    {
        private RunnerFixtures fixtures;
        private FakeOllama server;

        private static readonly string Bridge = Prompt("target_001", "Meet me over at the bridge.", "move_to", "bridge", false);
        private static readonly string Escort = Prompt("target_002", "Walk with me a while.", "follow_player", "no_target", false);

        // What the native path writes: the keys sorted, the statement before the target.
        private const string Sorted = "{\"action\":\"move_to\",\"statement\":\"On my way.\",\"target\":\"bridge\"}";

        [SetUp]
        public void SetUp()
        {
            fixtures = new RunnerFixtures();
            server = new FakeOllama();
        }

        [TearDown]
        public void TearDown()
        {
            fixtures.Dispose();
            Fixtures.DestroyCreated();
        }

        [Test]
        public async Task WarmUpsOutOfOrderRefuseTheRunNamingTheArmAndTheModel()
        {
            server.Content = Sorted;

            var e = await EvalRunnerTests.Refused(Ollama());

            Assert.That(e.Message, Does.Contain("Arm 'A'").And.Contain(Model2B).And.Contain("AllowOrderLost"));
            Assert.That(server.Chats.Count(), Is.EqualTo(3), "only arm A's warm-ups were sent, no prompt");
        }

        [Test]
        public async Task WithAllowOrderLostTheRunGoesOnAndTheReportSaysSo()
        {
            server.Content = Sorted;
            var runner = Ollama();
            runner.AllowOrderLost = true;

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            Assert.That(report.Conditions.AllowOrderLost, Is.True);
            foreach (var arm in report.Arms)
            {
                Assert.That((arm.OrderKept, arm.OrderLost, arm.OrderNotChecked), Is.EqualTo((0, 2, 0)), arm.Name);
                Assert.That(arm.OrderFlagged, Is.True);
            }

            Assert.That(report.Conditions.Arms.Select(a => a.WarmUpOrderLost), Is.All.EqualTo(3));
            Assert.That(report.Answers.Select(a => a.OrderLost), Is.All.EqualTo(true));
            Assert.That(report.Conditions.Warnings.Count(w => w.Contains("out of the schema's order")), Is.EqualTo(2));

            string md = report.ToMarkdown();
            Assert.That(md, Does.Contain("## Field order").And.Contain("AllowOrderLost was on").And.Contain("**order lost**"));

            var back = EvalReport.FromJson(report.ToJson());
            Assert.That(back.Conditions.AllowOrderLost, Is.True);
            Assert.That(back.Arms[0].OrderLost, Is.EqualTo(2));
            Assert.That(back.Conditions.Arms[0].WarmUpOrderLost, Is.EqualTo(3));
            Assert.That(back.Answers[0].OrderLost, Is.True);
        }

        [Test]
        public async Task RepliesInTheSchemasOrderAreCountedAsKept()
        {
            // FakeOllama's own reply is written action, target, statement.
            var report = await Fixtures.NotCancelled(Ollama().RunAsync());

            Assert.That(report.Conditions.AllowOrderLost, Is.False);
            foreach (var arm in report.Arms)
            {
                Assert.That((arm.OrderKept, arm.OrderLost, arm.OrderNotChecked), Is.EqualTo((2, 0, 0)), arm.Name);
                Assert.That(arm.OrderFlagged, Is.False);
            }

            Assert.That(report.Answers.Select(a => a.OrderLost), Is.All.EqualTo(false));
            Assert.That(report.Conditions.Warnings, Has.None.Contains("schema's order"));
            Assert.That(report.ToMarkdown(), Does.Contain("order kept").And.Not.Contain("**order lost**"));
        }

        [Test]
        public async Task AReplyThatWasNotReadIsNotCheckedRatherThanKept()
        {
            var provider = new FakeProvider("this is not JSON");
            var runner = Fake(provider, provider);
            runner.WarmUpRequests = 0;

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            Assert.That((report.Arms[0].OrderKept, report.Arms[0].OrderLost, report.Arms[0].OrderNotChecked), Is.EqualTo((0, 0, 1)));
            Assert.That(report.Answers[0].OrderLost, Is.Null, "the telemetry's false is not a kept order");
            Assert.That(report.ToMarkdown(), Does.Contain("not checked"));
        }

        [Test]
        public async Task OnlyTheArmOutOfOrderIsFlagged()
        {
            var kept = new FakeProvider(Reply("move_to", "bridge", "On my way."));
            var lost = new FakeProvider(Sorted);
            var runner = Fake(kept, lost);
            runner.WarmUpRequests = 0;

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            Assert.That(report.Arms[0].OrderFlagged, Is.False);
            Assert.That(report.Arms[1].OrderFlagged, Is.True);
            var warnings = report.Conditions.Warnings.Where(w => w.Contains("schema's order")).ToList();
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0], Does.StartWith("Arm 'B'"));
            Assert.That(report.Arms[0].Overall.Correct, Is.EqualTo(1), "flagged, never rescored");
            Assert.That(report.Arms[1].Overall.Correct, Is.EqualTo(1));
        }

        private EvalRunner Ollama()
        {
            var set = fixtures.Set(new[] { Bridge, Escort });
            return new EvalRunner(set, Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Ollama(server, Model2B, "OllamaA")),
                new EvalArm("B", fixtures.Ollama(server, Model2B, "OllamaB"), new DecideOptions { CheckTarget = false }));
        }

        private EvalRunner Fake(ILLMProvider a, ILLMProvider b)
        {
            var set = fixtures.Set(new[] { Bridge });
            return new EvalRunner(set, Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Config(a, "FakeA")),
                new EvalArm("B", fixtures.Config(b, "FakeB")));
        }
    }
}
