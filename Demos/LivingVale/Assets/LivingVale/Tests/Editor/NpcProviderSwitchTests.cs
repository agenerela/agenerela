using System;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Agenerela;
using LivingVale.Game;
using LivingVale.Interaction;
using NUnit.Framework;

namespace LivingVale.Tests
{
    /// <summary>F6 as a provider switch on the same agents (#198, LV #23).</summary>
    public sealed class NpcProviderSwitchTests
    {
        private static readonly NpcDescriptor Mara = new NpcDescriptor(VendorStock.MaraId, "Mara", "General trader");

        private FakeLlmProvider model;
        private ScriptedReplies scripted;
        private AgenerelaNpcDecisionDriver driver;
        private NpcProviderSwitch drivers;
        private long nextId;

        [SetUp]
        public void SetUp()
        {
            model = new FakeLlmProvider { Reply = FakeLlmProvider.Json(ActionRegistry.None, TargetRegistry.NoTarget, "Model line.") };
            scripted = new ScriptedReplies(new ScriptedNpcDecisionDriver(seed: 1) { NormalDelay = TimeSpan.Zero });
            driver = new AgenerelaNpcDecisionDriver(model, "m");
            drivers = new NpcProviderSwitch(driver, scripted, NpcDriverChoice.Agenerela);
        }

        private NpcTurn Turn(string line) => new NpcTurn(++nextId, Mara, line, Array.Empty<ChatLine>(), 1);

        private async Task<(NpcTurn turn, NpcReply reply)> Decide(string line)
        {
            var turn = Turn(line);
            var decide = driver.DecideAsync(turn, CancellationToken.None);
            await Run.NotCancelled(decide);
            return (turn, decide.Result);
        }

        [Test]
        public void TheLabelAndTheProviderFollowTheSelection()
        {
            Assert.AreEqual("Agenerela · m", drivers.Label);
            Assert.AreSame(model, driver.Provider);

            drivers.Next();
            Assert.AreEqual(NpcDriverChoice.Scripted, drivers.Choice);
            Assert.AreEqual("Scripted replies", drivers.Label);
            Assert.AreSame(scripted.Provider, driver.Provider);
            scripted.Script.Fixture = ScriptedFixture.Slow;
            Assert.AreEqual("Scripted replies (Slow)", drivers.Label, "The label is read live, so a fixture shows at once.");

            drivers.Next();
            Assert.AreEqual("Agenerela · m", drivers.Label);
            Assert.AreSame(model, driver.Provider);
        }

        [Test]
        public void TheConstructorHonoursTheInitialSelection()
        {
            var other = new AgenerelaNpcDecisionDriver(new FakeLlmProvider(), "m");
            var switched = new NpcProviderSwitch(other, scripted, NpcDriverChoice.Scripted);

            Assert.AreSame(scripted.Provider, other.Provider);
            Assert.AreEqual("m", switched.ModelLabel, "The model is kept for the next F6.");
        }

        [Test]
        public async Task EachNpcHasOneMemoryWhicheverAnswers()
        {
            drivers.Select(NpcDriverChoice.Scripted);
            var (turn, reply) = await Decide("Hello there.");
            Assert.IsTrue(driver.Execute(turn, reply));

            drivers.Select(NpcDriverChoice.Agenerela);
            await Decide("And now?");

            Assert.AreEqual(2, model.LastRequest.History.Count, "The model reads what the scripted reply said (LV #23).");
            StringAssert.Contains("Morning, traveler.", model.LastRequest.History[1]);
        }

        [Test]
        public async Task AReplyDecidedBeforeASwitchIsStillCarriedOut()
        {
            var (turn, reply) = await Decide("Hello.");
            drivers.Next();

            Assert.IsTrue(driver.Execute(turn, reply), "The same agent decided it and carries it out.");
            Assert.AreEqual(1, model.Requests.Count);
        }

        [Test]
        public void ANewModelWhileScriptedWaitsForTheNextF6()
        {
            drivers.Select(NpcDriverChoice.Scripted);
            var next = new FakeLlmProvider();

            drivers.UseModel(next, "n");
            Assert.AreSame(scripted.Provider, driver.Provider, "The script keeps answering.");

            drivers.Select(NpcDriverChoice.Agenerela);
            Assert.AreSame(next, driver.Provider);
            Assert.AreEqual("Agenerela · n", drivers.Label);
        }
    }
}
