using System.Linq;
using System.Threading.Tasks;
using Agenerela.Evaluation;
using Agenerela.Providers;
using NUnit.Framework;
using static Agenerela.Tests.Evaluation.RunnerFixtures;

namespace Agenerela.Tests.Evaluation
{
    // The runner's checks before and between arms (#145, build plan §4), against a stand-in Ollama
    // server reached through OllamaProvider's own internal methods: one model resident, the
    // server's version, how much of the model is in VRAM, and the unload between sequential arms.
    public sealed class EvalRunnerOllamaTests
    {
        private RunnerFixtures fixtures;
        private FakeOllama server;

        private static readonly string Bridge = Prompt("target_001", "Meet me over at the bridge.", "move_to", "bridge", false);
        private static readonly string Escort = Prompt("target_002", "Walk with me a while.", "follow_player", "no_target", false);

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
        public async Task ASecondModelLoadedRefusesTheRunBeforeAnythingIsAsked()
        {
            server.Loaded.Add("llama3.1:8b");

            var e = await EvalRunnerTests.Refused(Runner(Model2B, Model2B));

            Assert.That(e.Message, Does.Contain("Hard rule 3").And.Contain("llama3.1:8b"));
            Assert.That(server.Chats, Is.Empty);
        }

        [Test]
        public async Task WithNoOtherModelLoadedTheReportRecordsSizeVram()
        {
            var report = await Fixtures.NotCancelled(Runner(Model2B, Model2B).RunAsync());

            var arm = report.Conditions.Arms[0];
            var before = arm.Residency.Single(r => r.When == EvalRunner.BeforeWarmUp);
            var after = arm.Residency.Single(r => r.When == EvalRunner.AfterWarmUp);
            Assert.That(before.Checked, Is.True);
            Assert.That(before.Models, Is.Empty);
            Assert.That(after.Find(Model2B), Is.Not.Null);
            Assert.That(after.Find(Model2B).SizeVram, Is.EqualTo(server.SizeVram));
            Assert.That(after.Find(Model2B).FullyOnGpu, Is.True);
            Assert.That(arm.ProviderVersion, Is.EqualTo("0.40.14"));
            Assert.That(arm.Model, Is.EqualTo(Model2B));
            Assert.That(report.Conditions.Warnings, Is.Empty);
        }

        [Test]
        public async Task AModelNotFullyOnTheGpuIsFlagged()
        {
            server.SizeVram = server.Size / 2;

            var report = await Fixtures.NotCancelled(Runner(Model2B, Model2B).RunAsync());

            Assert.That(report.Conditions.Warnings, Has.Some.Contains("not fully on the GPU"));
            Assert.That(report.ToMarkdown(), Does.Contain("not fully on the GPU"));
        }

        [Test]
        public async Task InterleavedArmsOnTwoModelsAreRefused()
        {
            var e = await EvalRunnerTests.Refused(Runner(Model2B, Model4B));

            Assert.That(e.Message, Does.Contain("Sequential"));
            Assert.That(server.Log, Is.Empty);
        }

        [Test]
        public async Task SequentialArmsUnloadTheFirstModelBeforeTheSecondRuns()
        {
            var runner = Runner(Model2B, Model4B);
            runner.Order = EvalArmOrder.Sequential;

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            var log = server.Log;
            int lastOf2B = log.FindLastIndex(l => l == "POST /api/chat " + Model2B);
            int unload = log.IndexOf($"POST /api/generate {Model2B} keep_alive=0");
            int firstOf4B = log.IndexOf("POST /api/chat " + Model4B);
            Assert.That(lastOf2B, Is.GreaterThanOrEqualTo(0));
            Assert.That(unload, Is.GreaterThan(lastOf2B), string.Join("\n", log));
            Assert.That(firstOf4B, Is.GreaterThan(unload), string.Join("\n", log));
            Assert.That(server.MostLoaded, Is.EqualTo(1), "Only one model may ever be resident (hard rule 3).");
            Assert.That(report.Conditions.Arms[0].Unloaded, Is.True);
            Assert.That(report.Conditions.Arms[1].Unloaded, Is.Null);
            Assert.That(report.ToMarkdown(), Does.Contain("one after the other"));
        }

        [Test]
        public async Task ASequentialRunRefusesArmBsModelResidentWhileArmARuns()
        {
            // Arm B's model left loaded from an earlier run would sit beside arm A's for all of A.
            server.Loaded.Add(Model4B);
            var runner = Runner(Model2B, Model4B);
            runner.Order = EvalArmOrder.Sequential;

            var e = await EvalRunnerTests.Refused(runner);

            Assert.That(e.Message, Does.Contain("Hard rule 3").And.Contain(Model4B));
            Assert.That(server.Chats, Is.Empty);
        }

        [Test]
        public async Task AStrangerOnArmBsOwnServerIsRefusedBeforeArmARuns()
        {
            var serverB = new FakeOllama();
            serverB.Loaded.Add("llama3.1:8b");
            var runner = new EvalRunner(fixtures.Set(new[] { Bridge, Escort }), Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Ollama(server, Model2B, "OllamaA")),
                new EvalArm("B", fixtures.Ollama(serverB, Model4B, "OllamaB", "http://127.0.0.2:11434")))
            {
                Order = EvalArmOrder.Sequential,
            };

            var e = await EvalRunnerTests.Refused(runner);

            Assert.That(e.Message, Does.Contain("llama3.1:8b"));
            Assert.That(server.Chats, Is.Empty, "Arm A must not run when arm B could not.");
        }

        [Test]
        public async Task AModelThatStaysLoadedStopsASequentialRun()
        {
            server.IgnoresUnload = true;
            var runner = Runner(Model2B, Model4B);
            runner.Order = EvalArmOrder.Sequential;
            runner.UnloadWaitSeconds = 0;

            var e = await EvalRunnerTests.Refused(runner);

            Assert.That(e.Message, Does.Contain("still resident"));
            Assert.That(server.Chats, Has.None.Contains(Model4B));
        }

        [Test]
        public async Task AProviderThatCannotSayIsRecordedAsNotChecked()
        {
            var fake = new FakeProvider(Reply("move_to", "bridge"));
            var runner = new EvalRunner(fixtures.Set(new[] { Bridge }), Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Config(fake, "FakeA")), new EvalArm("B", fixtures.Config(fake, "FakeB")));

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            var arm = report.Conditions.Arms[0];
            Assert.That(arm.ProviderVersion, Is.EqualTo("not checked"));
            Assert.That(arm.Residency.Select(r => r.Checked), Is.All.False);
            Assert.That(report.Conditions.Warnings, Has.Some.Contains("not checked"));
        }

        [TestCase("qwen3.5", "qwen3.5:latest", true)]
        [TestCase("Qwen3.5:2B", "qwen3.5:2b", true)]
        [TestCase("qwen3.5:2b", "qwen3.5:4b", false)]
        public void ModelNamesCompareAsOllamaListsThem(string a, string b, bool same)
        {
            Assert.That(OllamaProvider.SameModel(a, b), Is.EqualTo(same));
        }

        private EvalRunner Runner(string modelA, string modelB)
        {
            var set = fixtures.Set(new[] { Bridge, Escort });
            return new EvalRunner(set, Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Ollama(server, modelA, "OllamaA")),
                new EvalArm("B", fixtures.Ollama(server, modelB, "OllamaB"), new DecideOptions { CheckTarget = false }));
        }
    }
}
