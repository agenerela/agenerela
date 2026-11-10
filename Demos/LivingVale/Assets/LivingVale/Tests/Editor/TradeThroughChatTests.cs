using System;
using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Agenerela;
using LivingVale.Game;
using LivingVale.Interaction;
using NUnit.Framework;

namespace LivingVale.Tests
{
    // The whole G3 path without a scene: scripted Mara decides, the shared executor quotes, the
    // card's button would call Confirm, and the receipt lands in the chat.
    public sealed class TradeThroughChatTests
    {
        private static readonly NpcDescriptor Mara = new NpcDescriptor(VendorStock.MaraId, "Mara", "General trader");
        private static readonly NpcDescriptor Iven = new NpcDescriptor("npc_iven", "Iven", "Healer");

        private PlayerInventory inventory;
        private TradeService trade;
        private NpcActionExecutor executor;
        private ScriptedNpcDecisionDriver driver;
        private AgenerelaNpcDecisionDriver npcs;

        // The action the agent last carried out: a none runs no handler, so it leaves no outcome.
        private string LastAction => npcs.LastOutcome?.ActionId ?? GameActions.None;
        private ConversationCoordinator coordinator;
        private ConversationTradeLink link;

        [SetUp]
        public void SetUp()
        {
            var catalog = ItemCatalog.CreateDefault();
            inventory = PlayerInventory.CreateStarting(catalog);
            link = new ConversationTradeLink();
            trade = new TradeService(inventory, new[] { VendorStock.CreateMara(catalog) }, link);
            executor = new NpcActionExecutor(trade);
            driver = new ScriptedNpcDecisionDriver(seed: 1, executor: executor) { NormalDelay = TimeSpan.FromMilliseconds(5) };
            npcs = new ScriptedReplies(driver).Driver(executor);
            coordinator = new ConversationCoordinator(npcs, new ConversationSettings { TurnTimeout = TimeSpan.FromSeconds(2) });
            link.Attach(coordinator, trade);
        }

        [TearDown]
        public void TearDown() => link.Dispose();

        private async Task Say(string line)
        {
            Assert.That(coordinator.Submit(line), Is.True);
            await Run.NotCancelled(coordinator.LastTurn);
        }

        private ChatLine LastLine => coordinator.HistoryFor(coordinator.ActiveNpc.Id).Last();

        [Test]
        public void GameSentinelsMatchTheFramework()
        {
            Assert.That(GameActions.None, Is.EqualTo(ActionRegistry.None));
            Assert.That(GameActions.NoTarget, Is.EqualTo(TargetRegistry.NoTarget));
        }

        [Test]
        public async Task BuyAPotionThroughChat_QuoteThenConfirm()
        {
            coordinator.Open(Mara);
            await Say("I'd like to buy a healing potion");

            var quote = trade.ActiveQuote;
            Assert.That(quote, Is.Not.Null, string.Join("\n", coordinator.HistoryFor(Mara.Id)));
            Assert.That(quote.TargetId, Is.EqualTo("stock_mara_potion_one"));
            Assert.That(quote.Direction, Is.EqualTo(TradeDirection.Purchase));
            Assert.That(quote.ConversationGeneration, Is.EqualTo(coordinator.Generation));
            Assert.That(LastAction, Is.EqualTo(GameActions.QuotePurchase));
            Assert.That(inventory.Gold, Is.EqualTo(40), "the decision only quoted");
            Assert.That(LastLine.Speaker, Is.EqualTo(ChatSpeaker.Npc));

            var receipt = trade.Confirm(quote.Id);
            Assert.That(receipt.Applied, Is.True);
            Assert.That(inventory.Gold, Is.EqualTo(30));
            Assert.That(inventory.CountOf(ItemCatalog.HealingPotion), Is.EqualTo(2));
            Assert.That(LastLine.Speaker, Is.EqualTo(ChatSpeaker.System));
            StringAssert.Contains("Bought Healing potion ×1 for 10 gold", LastLine.Text);
        }

        [Test]
        public async Task SellAPelt_OnlyWhenThePlayerHasOne()
        {
            coordinator.Open(Mara);
            await Say("Can I sell my wolf pelt?");
            Assert.That(trade.ActiveQuote, Is.Null);
            Assert.That(LastAction, Is.EqualTo(GameActions.None), "no pelt, so quote_sale was not available");
            StringAssert.Contains("pelt", LastLine.Text);

            inventory.TryGiveLoot("wolf_1", new[] { new ItemChange(ItemCatalog.WolfPelt, 1) }, 0);
            await Say("Can I sell my wolf pelt?");
            Assert.That(trade.ActiveQuote?.TargetId, Is.EqualTo("sale_mara_wolf_pelt_one"));
            Assert.That(trade.Confirm(trade.ActiveQuote.Id).Applied, Is.True);
            Assert.That(inventory.Gold, Is.EqualTo(44));
        }

        [Test]
        public async Task ClosingTheChat_InvalidatesTheQuote()
        {
            coordinator.Open(Mara);
            await Say("buy bread");
            var quote = trade.ActiveQuote;
            Assert.That(quote, Is.Not.Null);

            coordinator.Close(ConversationCloseReason.PlayerClosed);
            Assert.That(quote.State, Is.EqualTo(TradeQuoteState.Invalidated));
            Assert.That(trade.ActiveQuote, Is.Null);

            coordinator.Open(Mara);
            Assert.That(trade.Confirm(quote.Id).Applied, Is.False, "reopening does not revive it");
            Assert.That(inventory.Gold, Is.EqualTo(40));
            Assert.That(inventory.CountOf(ItemCatalog.Bread), Is.EqualTo(2));
        }

        [TestCase(ConversationCloseReason.SwitchedSpeaker)]
        [TestCase(ConversationCloseReason.LeftRange)]
        public async Task SwitchingOrLeaving_InvalidatesTheQuote(ConversationCloseReason reason)
        {
            coordinator.Open(Mara);
            await Say("buy a potion");
            var quote = trade.ActiveQuote;

            if (reason == ConversationCloseReason.SwitchedSpeaker)
            {
                coordinator.Open(Iven);
            }
            else
            {
                coordinator.Close(reason);
            }

            Assert.That(trade.Confirm(quote.Id).Applied, Is.False);
            Assert.That(inventory.Gold, Is.EqualTo(40));
        }

        [Test]
        public async Task ExecutorRefusesAQuoteFromAStaleTurn()
        {
            coordinator.Open(Mara);
            await Say("hello");
            var outcome = executor.Execute(Mara.Id, GameActions.QuotePurchase, "stock_mara_potion_one", coordinator.Generation - 1);
            Assert.That(outcome.Executed, Is.False);
            Assert.That(outcome.Reason, Is.EqualTo(TradeRejection.StaleConversation));
            Assert.That(executor.Execute(Mara.Id, "build_castle", GameActions.NoTarget, coordinator.Generation).Executed, Is.False);
        }

        [Test]
        public void AvailableActions_ComeFromGameState()
        {
            var actions = executor.AvailableFor(Mara.Id);
            Assert.That(actions.Select(a => a.ActionId), Is.EqualTo(new[] { GameActions.QuotePurchase }), "no sellable goods yet");
            Assert.That(actions[0].Targets.Select(t => t.Id),
                Is.EqualTo(new[] { "stock_mara_potion_one", "stock_mara_bread_one", "stock_mara_herb_one" }));

            inventory.TryGiveLoot("loot", new[] { new ItemChange(ItemCatalog.IronScrap, 2) }, 0);
            actions = executor.AvailableFor(Mara.Id);
            Assert.That(actions.Select(a => a.ActionId), Is.EqualTo(new[] { GameActions.QuotePurchase, GameActions.QuoteSale }));
            Assert.That(actions[1].Targets.Select(t => t.Id), Is.EqualTo(new[] { "sale_mara_iron_scrap_one" }));

            Assert.That(executor.AvailableFor(Iven.Id), Is.Empty, "Iven trades nothing in G3");
        }

        [Test]
        public async Task G1Behaviour_WithoutAnExecutor_IsUnchanged()
        {
            var plain = new ScriptedNpcDecisionDriver(seed: 1) { NormalDelay = TimeSpan.FromMilliseconds(5) };
            var plainNpcs = new ScriptedReplies(plain).Driver();
            var conversations = new ConversationCoordinator(plainNpcs);
            conversations.Open(Mara);
            Assert.That(conversations.Submit("buy a potion"), Is.True);
            await Run.NotCancelled(conversations.LastTurn);
            Assert.That(conversations.HistoryFor(Mara.Id).Last().Speaker, Is.EqualTo(ChatSpeaker.Npc));
            Assert.That(plainNpcs.LastOutcome?.Executed ?? true, Is.True, "Without an executor only none is registered.");
        }
    }
}
