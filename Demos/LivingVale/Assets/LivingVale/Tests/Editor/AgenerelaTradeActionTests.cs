using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Agenerela;
using LivingVale.Game;
using LivingVale.Interaction;
using NUnit.Framework;

namespace LivingVale.Tests
{
    /// <summary>G3's trade actions through the Agenerela driver, with a scripted model and the real game executor.</summary>
    public sealed class AgenerelaTradeActionTests
    {
        private const string Potion = "stock_mara_potion_one";
        private const string PeltSale = "sale_mara_wolf_pelt_one";
        private static readonly NpcDescriptor Mara = new NpcDescriptor(VendorStock.MaraId, "Mara", "General trader");

        private FakeLlmProvider provider;
        private PlayerInventory inventory;
        private FakeTradeSession session;
        private TradeService trade;
        private AgenerelaNpcDecisionDriver driver;
        private long nextId;

        [SetUp]
        public void SetUp()
        {
            var catalog = ItemCatalog.CreateDefault();
            inventory = new PlayerInventory(catalog);
            session = new FakeTradeSession { Generation = 7 };
            trade = new TradeService(inventory, new[] { VendorStock.CreateMara(catalog) }, session,
                () => new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));
            provider = new FakeLlmProvider();
            driver = new AgenerelaNpcDecisionDriver(provider, "fake", executor: new NpcActionExecutor(trade));
        }

        private NpcTurn Turn(string line) => new NpcTurn(++nextId, Mara, line, Array.Empty<ChatLine>(), session.Generation);

        private async Task<NpcReply> Decide(NpcTurn turn)
        {
            var decide = driver.DecideAsync(turn, CancellationToken.None);
            await Run.NotCancelled(decide);
            return decide.Result;
        }

        private void GivePelt(int count) =>
            Assert.IsTrue(inventory.TryApply(0, new[] { new ItemChange(ItemCatalog.WolfPelt, count) }, out _));

        private static string[] Allowed(NpcReply reply, string field) =>
            ((DecisionResult)reply.DriverState).Request.Schema.Fields.First(f => f.Name == field).AllowedValues.ToArray();

        [Test]
        public async Task AQuoteOnAListedTargetMakesExactlyOneQuoteAndMovesNothing()
        {
            int gold = inventory.Gold;
            session.Generation = 12;
            provider.Reply = FakeLlmProvider.Json(GameActions.QuotePurchase, Potion, "Ten gold for a potion.");

            var turn = Turn("A potion, please.");
            var reply = await Decide(turn);
            Assert.AreEqual(GameActions.QuotePurchase, reply.Decision.ActionId);

            int quotesIssued = 0;
            trade.QuotesChanged += () => quotesIssued++;
            Assert.IsTrue(driver.Execute(turn, reply));
            Assert.AreEqual(1, quotesIssued, "Exactly one quote change: the one quote issued.");
            Assert.IsTrue(driver.LastOutcome.Executed);
            Assert.IsNotNull(trade.ActiveQuote);
            Assert.AreSame(driver.LastOutcome.Quote, trade.ActiveQuote);
            Assert.AreEqual(Potion, trade.ActiveQuote.TargetId);
            Assert.AreEqual(12, trade.ActiveQuote.ConversationGeneration, "The handler passes the turn's generation.");
            Assert.AreEqual(gold, inventory.Gold, "A quote moves no gold.");
            Assert.AreEqual(0, inventory.CountOf(ItemCatalog.HealingPotion));
        }

        [Test]
        public async Task TheActionsAndTargetsReachThePromptAndTheSchema()
        {
            var reply = await Decide(Turn("Hello."));

            var request = ((DecisionResult)reply.DriverState).Request;
            StringAssert.Contains(GameActions.QuotePurchase, request.Observations);
            StringAssert.Contains(Potion, request.Observations);
            StringAssert.Contains(Potion, request.FewShotBlock, "The example names a real target.");
            CollectionAssert.Contains(Allowed(reply, DecisionSchema.ActionFieldName), GameActions.QuotePurchase);
            CollectionAssert.Contains(Allowed(reply, DecisionSchema.TargetFieldName), Potion);
        }

        [Test]
        public async Task WithNothingToSell_QuoteSaleIsAbsent()
        {
            var reply = await Decide(Turn("Hello."));

            CollectionAssert.DoesNotContain(Allowed(reply, DecisionSchema.ActionFieldName), GameActions.QuoteSale);
            CollectionAssert.DoesNotContain(Allowed(reply, DecisionSchema.TargetFieldName), PeltSale);
            StringAssert.DoesNotContain(GameActions.QuoteSale, provider.LastRequest.Observations);

            GivePelt(1);
            reply = await Decide(Turn("Hello again."));
            CollectionAssert.Contains(Allowed(reply, DecisionSchema.ActionFieldName), GameActions.QuoteSale);
            CollectionAssert.Contains(Allowed(reply, DecisionSchema.TargetFieldName), PeltSale);
        }

        [Test]
        public async Task AnUnlistedTargetIsRefusedAndChangesNothing()
        {
            provider.Reply = FakeLlmProvider.Json(GameActions.QuotePurchase, "stock_mara_dragon", "One dragon, coming up.");

            var turn = Turn("Sell me a dragon.");
            var reply = await Decide(turn);

            Assert.AreEqual(ActionRegistry.None, reply.Decision.ActionId, "The guards stop a target the request never offered.");
            Assert.IsFalse(driver.Execute(turn, reply));
            Assert.IsNull(trade.ActiveQuote);
        }

        [Test]
        public async Task ATargetOfTheOtherActionIsContainedAsNone()
        {
            GivePelt(1);
            provider.Reply = FakeLlmProvider.Json(GameActions.QuotePurchase, PeltSale, "I'll sell you your own pelt.");

            var turn = Turn("Sell me a pelt.");
            var reply = await Decide(turn);

            Assert.AreEqual(ActionRegistry.None, reply.Decision.ActionId);
            Assert.AreEqual(AgenerelaNpcDecisionDriver.RefusalLine, reply.Decision.Statement);
            CollectionAssert.Contains(((DecisionResult)reply.DriverState).Telemetry.GuardsFired, TradeActionBinding.PairGuardName);
            Assert.IsTrue(driver.Execute(turn, reply), "A contained refusal executes as none.");
            Assert.IsNull(trade.ActiveQuote);
        }

        [Test]
        public async Task ATargetThatWentStaleBeforeExecuteIsRefusedAndChangesNothing()
        {
            GivePelt(1);
            provider.Reply = FakeLlmProvider.Json(GameActions.QuoteSale, PeltSale, "Three gold for that pelt.");

            var turn = Turn("Want my pelt?");
            var reply = await Decide(turn);
            Assert.AreEqual(GameActions.QuoteSale, reply.Decision.ActionId);

            GivePelt(-1);
            int gold = inventory.Gold;

            Assert.IsFalse(driver.Execute(turn, reply), "Agent.Execute re-resolves the targets and the pelt is gone.");
            Assert.IsNull(trade.ActiveQuote);
            Assert.AreEqual(gold, inventory.Gold);
        }

        [Test]
        public async Task AQuoteTheExecutorRefusesIsNotExecuted()
        {
            provider.Reply = FakeLlmProvider.Json(GameActions.QuotePurchase, Potion, "Ten gold.");

            var turn = Turn("A potion, please.");
            var reply = await Decide(turn);

            // The conversation moved on: the executor refuses a quote for an old generation.
            session.Generation = 8;
            Assert.IsFalse(driver.Execute(turn, reply));
            Assert.IsFalse(driver.LastOutcome.Executed);
            Assert.IsNull(trade.ActiveQuote);
        }

        [Test]
        public async Task TheNextTurnSeesTheOpenOfferAndThenTheReceipt()
        {
            provider.Reply = FakeLlmProvider.Json(GameActions.QuotePurchase, Potion, "Ten gold.");
            var turn = Turn("A potion, please.");
            Assert.IsTrue(driver.Execute(turn, await Decide(turn)));

            provider.Reply = FakeLlmProvider.Json(ActionRegistry.None, "no_target", "Well?");
            await Decide(Turn("Did I buy it?"));
            StringAssert.Contains("waiting for the traveler to confirm", provider.LastRequest.Observations);
            StringAssert.DoesNotContain("Last trade", provider.LastRequest.Observations);

            Assert.IsTrue(trade.Confirm(trade.ActiveQuote.Id).Applied);
            await Decide(Turn("And now?"));
            StringAssert.DoesNotContain("waiting for the traveler to confirm", provider.LastRequest.Observations);
            StringAssert.Contains("Last trade with the traveler: Bought Healing potion", provider.LastRequest.Observations);
        }

        [Test]
        public async Task ATradeMadeBeforeMarasFirstModelTurnStillReachesIt()
        {
            // As the scripted driver would, before an F6 switch: quote and confirm with no agent built yet.
            Assert.AreEqual(TradeRejection.None, trade.TryQuotePurchase(VendorStock.MaraId, Potion, session.Generation, out var quote));
            Assert.IsTrue(trade.Confirm(quote.Id).Applied);

            await Decide(Turn("Did that go through?"));
            StringAssert.Contains("Last trade with the traveler: Bought Healing potion", provider.LastRequest.Observations);
        }

        [Test]
        public async Task OnlyExecutedDecisionsAreRemembered()
        {
            provider.Reply = FakeLlmProvider.Json(ActionRegistry.None, "no_target", "A line the player never saw.");
            await Decide(Turn("This reply goes stale."));

            provider.Reply = FakeLlmProvider.Json(ActionRegistry.None, "no_target", "Morning.");
            var turn = Turn("Hello.");
            var reply = await Decide(turn);
            Assert.AreEqual(0, provider.LastRequest.History.Count, "A reply never executed is not remembered.");

            Assert.IsTrue(driver.Execute(turn, reply));
            await Decide(Turn("Still there?"));
            Assert.AreEqual(2, provider.LastRequest.History.Count);
            StringAssert.Contains("Morning.", provider.LastRequest.History[1]);
        }
    }
}
