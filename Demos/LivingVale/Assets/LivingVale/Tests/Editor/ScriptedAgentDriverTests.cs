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
    /// <summary>The scripted replies decided through Agent with the framework's ScriptedProvider (#194, G5g).</summary>
    public sealed class ScriptedAgentDriverTests
    {
        private static readonly NpcDescriptor Mara = new NpcDescriptor(VendorStock.MaraId, "Mara", "General trader");

        private ScriptedNpcDecisionDriver script;
        private TradeService trade;
        private ScriptedAgentDriver driver;
        private long nextId;

        [SetUp]
        public void SetUp()
        {
            var catalog = ItemCatalog.CreateDefault();
            trade = new TradeService(new PlayerInventory(catalog), new[] { VendorStock.CreateMara(catalog) }, new FakeTradeSession());
            var executor = new NpcActionExecutor(trade);
            script = new ScriptedNpcDecisionDriver(executor: executor) { NormalDelay = TimeSpan.Zero };
            driver = new ScriptedAgentDriver(script, executor);
        }

        private NpcTurn Turn(string line) => new NpcTurn(++nextId, Mara, line, Array.Empty<ChatLine>(), 1);

        private async Task<NpcReply> Decide(NpcTurn turn)
        {
            var decide = driver.DecideAsync(turn, CancellationToken.None);
            await Run.NotCancelled(decide);
            return decide.Result;
        }

        [Test]
        public async Task AScriptedTradeGoesThroughTheAgentAndTheExecutor()
        {
            var turn = Turn("I want to buy a healing potion.");
            var reply = await Decide(turn);

            Assert.AreEqual(GameActions.QuotePurchase, reply.Decision.ActionId);
            Assert.AreEqual("stock_mara_potion_one", reply.Decision.TargetId);
            Assert.IsInstanceOf<DecisionResult>(reply.DriverState, "The framework's pipeline decided it.");
            Assert.AreEqual(1, driver.Provider.RequestCount, "Through ScriptedProvider.");
            Assert.AreEqual("Mara", driver.Provider.LastRequest.Agent);

            Assert.IsTrue(driver.Execute(turn, reply), "Agent.Execute runs the bound handler, then the executor.");
            Assert.IsNotNull(trade.ActiveQuote);
            Assert.AreEqual("stock_mara_potion_one", trade.ActiveQuote.TargetId);
        }

        [Test]
        public async Task AKeywordLineIsNoneWithTheScriptsStatement()
        {
            var reply = await Decide(Turn("Hello there."));

            Assert.AreEqual(ActionRegistry.None, reply.Decision.ActionId);
            Assert.AreEqual("Morning, traveler. Mind the mud.", reply.Decision.Statement);
        }

        [Test]
        public async Task TheMalformedFixtureIsMalformedAndThrowsIsAProviderError()
        {
            script.Fixture = ScriptedFixture.Malformed;
            Assert.AreEqual(NpcReplyFailure.Malformed, (await Decide(Turn("Hello."))).Failure);

            script.Fixture = ScriptedFixture.Throws;
            Assert.AreEqual(NpcReplyFailure.ProviderError, (await Decide(Turn("Hello."))).Failure);
        }

        [Test]
        public async Task TheNeverAnswersFixtureEndsOnlyByCancellation()
        {
            script.Fixture = ScriptedFixture.NeverAnswers;
            using (var cts = new CancellationTokenSource())
            {
                var decide = driver.DecideAsync(Turn("Hello."), cts.Token);
                await Task.Yield();
                Assert.IsFalse(decide.IsCompleted, "No reply on its own.");

                cts.Cancel();
                bool cancelled = false;
                try
                {
                    await decide;
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }

                Assert.IsTrue(cancelled);
            }
        }

        [Test]
        public void TheLabelIsTheScriptsSoItIsNeverMistakenForAModel()
        {
            Assert.AreEqual("Scripted replies", driver.Label);
            script.Fixture = ScriptedFixture.Slow;
            Assert.AreEqual("Scripted replies (Slow)", driver.Label);
        }

        [Test]
        public async Task ThroughTheCoordinator_AScriptedReplyIsShown()
        {
            var coordinator = new ConversationCoordinator(driver);
            coordinator.Open(Mara);

            Assert.IsTrue(coordinator.Submit("Hello."));
            await Run.NotCancelled(coordinator.LastTurn);

            var history = coordinator.HistoryFor(Mara.Id);
            Assert.AreEqual("Morning, traveler. Mind the mud.", history[history.Count - 1].Text);
        }
    }
}
