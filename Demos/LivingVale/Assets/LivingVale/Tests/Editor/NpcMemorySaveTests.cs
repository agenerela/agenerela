using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Agenerela;
using LivingVale.Game;
using LivingVale.Interaction;
using NUnit.Framework;

namespace LivingVale.Tests
{
    /// <summary>
    /// G5h, DR-018 (#195): the model driver on the framework's own paths. The generation travels in
    /// the call's correlation, handlers return ActionResult, memory commits on execute, and NPC
    /// memory is saved and loaded with the game.
    /// </summary>
    public sealed class NpcMemorySaveTests
    {
        private static readonly NpcDescriptor Mara = new NpcDescriptor(VendorStock.MaraId, "Mara", "General trader");
        private const string Potion = "stock_mara_potion_one";

        private sealed class Game
        {
            public readonly FakeTradeSession Session = new FakeTradeSession { Generation = 7 };
            public readonly PlayerInventory Inventory;
            public readonly TradeService Trade;
            public readonly SaveGame Save;
            public readonly FakeLlmProvider Provider = new FakeLlmProvider();
            public readonly AgenerelaNpcDecisionDriver Driver;

            public Game(string directory)
            {
                var catalog = ItemCatalog.CreateDefault();
                Inventory = PlayerInventory.CreateStarting(catalog);
                Trade = new TradeService(Inventory, new[] { VendorStock.CreateMara(catalog) }, Session,
                    () => new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));
                var progression = new Progression();
                var quests = new QuestLog(Inventory, progression, Session);
                Driver = new AgenerelaNpcDecisionDriver(Provider, "fake", executor: new NpcActionExecutor(Trade, quests));
                Save = new SaveGame(Inventory, Trade, quests, progression, directory) { Memories = new NpcMemoryArchive(() => Driver) };
            }

            public async Task<(NpcTurn, NpcReply)> Say(string line, string reply)
            {
                Provider.Reply = reply;
                var turn = new NpcTurn(Environment.TickCount, Mara, line, Array.Empty<ChatLine>(), Session.Generation);
                var decide = Driver.DecideAsync(turn, CancellationToken.None);
                await Run.NotCancelled(decide);
                return (turn, decide.Result);
            }
        }

        private string directory;

        [SetUp]
        public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "LivingValeMemoryTests_" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private static string Talk(string statement) => FakeLlmProvider.Json(ActionRegistry.None, TargetRegistry.NoTarget, statement);

        [Test]
        public async Task TheGenerationTravelsInTheCorrelationNotInAgentState()
        {
            var game = new Game(directory);
            game.Session.Generation = 42;
            var (turn, reply) = await game.Say("A potion, please.", FakeLlmProvider.Json(GameActions.QuotePurchase, Potion, "Ten gold."));

            var result = (DecisionResult)reply.DriverState;
            Assert.AreSame(turn, result.Correlation, "The turn is the call's correlation.");
            Assert.IsTrue(game.Driver.Execute(turn, reply));
            Assert.AreEqual(42, game.Trade.ActiveQuote.ConversationGeneration);
            Assert.IsFalse(game.Driver.AgentFor(Mara).State.ContainsKey("livingvale.generation"), "Nothing is written to the agent's state.");
        }

        [Test]
        public async Task ExecuteReturnsTheExecutorsOutcomeAsAnActionResult()
        {
            var game = new Game(directory);
            var (turn, reply) = await game.Say("A potion, please.", FakeLlmProvider.Json(GameActions.QuotePurchase, Potion, "Ten gold."));

            game.Session.Generation = 8;   // the conversation moved on: the executor refuses
            Assert.IsFalse(game.Driver.Execute(turn, reply));

            var telemetry = ((DecisionResult)reply.DriverState).Telemetry;
            Assert.AreEqual(ExecutionStatus.Failed, telemetry.Execution);
            Assert.IsFalse(string.IsNullOrEmpty(telemetry.ExecutionReason), "The executor's rejection is the reason.");
            Assert.IsFalse(game.Driver.LastOutcome.Executed, "The payload is the executor's outcome.");
            Assert.IsNull(game.Trade.ActiveQuote);
        }

        [Test]
        public async Task ARefusedQuestOfferReportsTheQuestsOwnReason()
        {
            var game = new Game(directory);
            var iven = new NpcDescriptor(QuestLog.IvenId, "Iven", "Healer");
            game.Provider.Reply = FakeLlmProvider.Json(GameActions.OfferQuest, QuestLog.IvenOfferId, "Will you help?");
            var turn = new NpcTurn(1, iven, "Can I help?", Array.Empty<ChatLine>(), game.Session.Generation);
            var decide = game.Driver.DecideAsync(turn, CancellationToken.None);
            await Run.NotCancelled(decide);
            Assert.AreEqual(GameActions.OfferQuest, decide.Result.Decision.ActionId);

            game.Session.Generation = 8;
            Assert.IsFalse(game.Driver.Execute(turn, decide.Result));

            string reason = ((DecisionResult)decide.Result.DriverState).Telemetry.ExecutionReason;
            StringAssert.Contains(game.Driver.LastOutcome.QuestReason.ToString(), reason);
            Assert.AreNotEqual(QuestRejection.None, game.Driver.LastOutcome.QuestReason);
        }

        [Test]
        public async Task ADecisionTheExecutorRefusesIsNotRemembered()
        {
            var game = new Game(directory);
            var (turn, reply) = await game.Say("A potion, please.", FakeLlmProvider.Json(GameActions.QuotePurchase, Potion, "Ten gold."));
            game.Session.Generation = 8;
            Assert.IsFalse(game.Driver.Execute(turn, reply));

            game.Session.Generation = 7;
            await game.Say("Hello?", Talk("Yes?"));
            Assert.AreEqual(0, game.Provider.LastRequest.History.Count, "The refused quote stays out of memory.");
        }

        [Test]
        public async Task MemorySavesAndLoadsWithTheGame()
        {
            var game = new Game(directory);
            var (turn, reply) = await game.Say("How is trade?", Talk("Slow since the road went quiet."));
            Assert.IsTrue(game.Driver.Execute(turn, reply));
            Assert.IsTrue(game.Save.TrySave(out string error), error);

            var loaded = new Game(directory);
            Assert.IsTrue(loaded.Save.TryLoad(out error), error);

            await loaded.Say("And now?", Talk("Still slow."));
            var history = loaded.Provider.LastRequest.History;
            Assert.AreEqual(2, history.Count, "Mara remembers the exchange from before the save.");
            StringAssert.Contains("How is trade?", history[0]);
            StringAssert.Contains("Slow since the road went quiet.", history[1]);
        }

        [Test]
        public async Task LoadingReplacesMemoryAndClearsNpcsTheSaveDoesNotMention()
        {
            var game = new Game(directory);
            Assert.IsTrue(game.Save.TrySave(out string error), error);   // saved before Mara said anything

            var (turn, reply) = await game.Say("How is trade?", Talk("Slow."));
            Assert.IsTrue(game.Driver.Execute(turn, reply));
            Assert.IsTrue(game.Save.TryLoad(out error), error);

            await game.Say("Hello.", Talk("Hello."));
            Assert.AreEqual(0, game.Provider.LastRequest.History.Count, "In the loaded game that conversation never happened.");
        }

        [Test]
        public async Task AVersionOneSaveLoadsWithEveryMemoryCleared()
        {
            var game = new Game(directory);
            var (turn, reply) = await game.Say("How is trade?", Talk("Slow."));
            Assert.IsTrue(game.Driver.Execute(turn, reply));

            var old = game.Save.Capture();
            old.version = 1;
            old.npcMemories = null;
            Assert.IsTrue(game.Save.TryApply(old, out string error), error);

            await game.Say("Hello.", Talk("Hello."));
            Assert.AreEqual(0, game.Provider.LastRequest.History.Count);
        }

        [Test]
        public async Task AnUnreadableMemoryFailsTheLoadAndChangesNothing()
        {
            var game = new Game(directory);
            var (turn, reply) = await game.Say("How is trade?", Talk("Slow."));
            Assert.IsTrue(game.Driver.Execute(turn, reply));

            var broken = game.Save.Capture();
            broken.gold = game.Inventory.Gold + 99;
            broken.npcMemories[0].format = "someone.elses.memory";
            int gold = game.Inventory.Gold;

            Assert.IsFalse(game.Save.TryApply(broken, out string error));
            StringAssert.Contains(VendorStock.MaraId, error);
            Assert.AreEqual(gold, game.Inventory.Gold, "Nothing else was restored either.");

            await game.Say("Still there?", Talk("Yes."));
            Assert.AreEqual(2, game.Provider.LastRequest.History.Count, "Mara's memory is as it was.");
        }

        [Test]
        public async Task AVersionTwoSaveWithoutItsMemorySectionIsRefused()
        {
            var game = new Game(directory);
            var snapshot = game.Save.Capture();
            snapshot.npcMemories = null;

            Assert.IsFalse(game.Save.TryApply(snapshot, out string error));
            Assert.AreEqual("Missing save collection.", error);
            await Task.CompletedTask;
        }
    }
}
