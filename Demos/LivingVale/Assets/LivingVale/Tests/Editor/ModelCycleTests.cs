using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Agenerela;
using LivingVale.Game;
using LivingVale.Interaction;
using NUnit.Framework;

namespace LivingVale.Tests
{
    public sealed class ModelCycleTests
    {
        private sealed class FakeCatalog : IModelCatalog
        {
            public IReadOnlyCollection<string> Installed;
            public TaskCompletionSource<IReadOnlyCollection<string>> Pending;
            public int Calls;

            public Task<IReadOnlyCollection<string>> InstalledAsync(CancellationToken ct)
            {
                Calls++;
                return Pending != null ? Pending.Task : Task.FromResult(Installed);
            }
        }

        private static readonly string[] List = { "qwen3.5:2b", "gemma4:e2b-it-qat", "llama3.2:3b", "granite4.1:3b" };
        private static readonly NpcDescriptor Mara = new NpcDescriptor(VendorStock.MaraId, "Mara", "General trader");

        [Test]
        public void PickNext_SkipsWhatIsNotInstalledAndWrapsRound()
        {
            var installed = new[] { "qwen3.5:2b", "llama3.2:3b" };

            Assert.AreEqual("llama3.2:3b", ModelSelector.PickNext(List, "qwen3.5:2b", installed, out var skipped));
            CollectionAssert.AreEqual(new[] { "gemma4:e2b-it-qat" }, skipped);

            Assert.AreEqual("qwen3.5:2b", ModelSelector.PickNext(List, "llama3.2:3b", installed, out skipped), "Wraps past the end.");
            CollectionAssert.AreEqual(new[] { "granite4.1:3b" }, skipped);
        }

        [Test]
        public void PickNext_IsNullWhenNothingElseIsInstalled()
        {
            Assert.IsNull(ModelSelector.PickNext(List, "qwen3.5:2b", new[] { "qwen3.5:2b" }, out var skipped));
            Assert.AreEqual(3, skipped.Count);
            Assert.IsNull(ModelSelector.PickNext(List, "qwen3.5:2b", Array.Empty<string>(), out _));
        }

        [Test]
        public void PickNext_AnUntaggedNameMatchesLatest()
        {
            Assert.AreEqual("mistral", ModelSelector.PickNext(new[] { "a:1", "mistral" }, "a:1", new[] { "mistral:latest" }, out _));
        }

        [Test]
        public void Selector_PutsAnUnlistedStartingModelFirstAndDropsRepeats()
        {
            var selector = new ModelSelector(new[] { "b", "b", " ", "c" }, "a", new FakeCatalog());
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, selector.Models);
            Assert.AreEqual("a", selector.Current);
        }

        [Test]
        public async Task Selector_ServerDown_StaysAndSaysSo()
        {
            var selector = new ModelSelector(List, "qwen3.5:2b", new FakeCatalog { Installed = null });

            var next = selector.NextAsync(CancellationToken.None);
            await Run.NotCancelled(next);

            Assert.IsFalse(next.Result.Changed);
            Assert.AreEqual("qwen3.5:2b", selector.Current);
            StringAssert.Contains("not answering", next.Result.Notice);
        }

        [Test]
        public async Task Selector_SkippedModelsAreNamedInTheNotice()
        {
            var selector = new ModelSelector(List, "qwen3.5:2b", new FakeCatalog { Installed = new[] { "granite4.1:3b" } });

            var next = selector.NextAsync(CancellationToken.None);
            await Run.NotCancelled(next);

            Assert.IsTrue(next.Result.Changed);
            Assert.AreEqual("granite4.1:3b", selector.Current);
            CollectionAssert.AreEqual(new[] { "gemma4:e2b-it-qat", "llama3.2:3b" }, next.Result.Skipped);
            StringAssert.Contains("2 models not installed", next.Result.Notice);
            Assert.Less(next.Result.Notice.Length, 40, "The notice shares one line with the badge.");
        }

        private sealed class Rig
        {
            public readonly FakeLlmProvider First = new FakeLlmProvider();
            public readonly List<(string model, FakeLlmProvider provider)> Built = new List<(string, FakeLlmProvider)>();
            public readonly FakeCatalog Catalog = new FakeCatalog { Installed = List };
            public readonly TradeService Trade;
            public readonly AgenerelaNpcDecisionDriver Driver;
            public readonly SwitchableNpcDecisionDriver Drivers;
            public readonly ConversationCoordinator Coordinator;
            public readonly ModelCycle Cycle;

            public Rig()
            {
                var catalog = ItemCatalog.CreateDefault();
                Trade = new TradeService(new PlayerInventory(catalog), new[] { VendorStock.CreateMara(catalog) }, new FakeTradeSession());
                Driver = new AgenerelaNpcDecisionDriver(First, "qwen3.5:2b", executor: new NpcActionExecutor(Trade));
                Drivers = new SwitchableNpcDecisionDriver(new FakeNpcDriver(), Driver, NpcDriverChoice.Agenerela);
                Coordinator = new ConversationCoordinator(Drivers);
                Cycle = new ModelCycle(Coordinator, Drivers, new ModelSelector(List, "qwen3.5:2b", Catalog), Driver,
                    (driver, model) =>
                    {
                        var provider = new FakeLlmProvider();
                        Built.Add((model, provider));
                        return driver.WithProvider(provider, model);
                    });
            }
        }

        [Test]
        public async Task Next_ClosesTheConversationAndPutsTheNewModelInTheSwitch()
        {
            var rig = new Rig();
            rig.Coordinator.Open(Mara);

            var press = rig.Cycle.NextAsync();
            await Run.NotCancelled(press);

            Assert.IsFalse(rig.Coordinator.IsOpen, "No turn may cross models.");
            Assert.AreEqual("Agenerela · gemma4:e2b-it-qat", rig.Drivers.Label);
            Assert.AreSame(rig.Cycle.Driver, rig.Drivers.Current);
            Assert.AreEqual(1, rig.Built.Count);
        }

        [Test]
        public async Task Next_KeepsEachNpcsMemoryAndTheExecutor()
        {
            var rig = new Rig();
            rig.First.Reply = FakeLlmProvider.Json(ActionRegistry.None, TargetRegistry.NoTarget, "Ten gold a potion.");
            var turn = new NpcTurn(1, Mara, "How much is a potion?", Array.Empty<ChatLine>(), 1);
            var decide = rig.Driver.DecideAsync(turn, CancellationToken.None);
            await Run.NotCancelled(decide);
            Assert.IsTrue(rig.Driver.Execute(turn, decide.Result));

            await Run.NotCancelled(rig.Cycle.NextAsync());
            var next = rig.Cycle.Driver.DecideAsync(new NpcTurn(2, Mara, "And bread?", Array.Empty<ChatLine>(), 1), CancellationToken.None);
            await Run.NotCancelled(next);

            var request = rig.Built[0].provider.LastRequest;
            Assert.AreEqual(2, request.History.Count, "Mara remembers the exchange from the previous model.");
            StringAssert.Contains("Ten gold a potion.", request.History[1]);
            StringAssert.Contains(GameActions.QuotePurchase, request.Observations, "The trade actions come with the rebuilt driver.");
            Assert.AreEqual(1, rig.First.Requests.Count, "The old model is asked nothing more.");
        }

        [Test]
        public async Task Next_TheNewDriverFollowsReceipts()
        {
            var rig = new Rig();
            await Run.NotCancelled(rig.Cycle.NextAsync());

            Assert.AreEqual(TradeRejection.None, rig.Trade.TryQuotePurchase(VendorStock.MaraId, "stock_mara_potion_one", 1, out var quote));
            Assert.IsTrue(rig.Trade.Confirm(quote.Id).Applied);

            var next = rig.Cycle.Driver.DecideAsync(new NpcTurn(1, Mara, "Hello.", Array.Empty<ChatLine>(), 1), CancellationToken.None);
            await Run.NotCancelled(next);
            StringAssert.Contains("Last trade with the traveler", rig.Built[0].provider.LastRequest.Observations, "The new driver follows receipts.");
        }

        [Test]
        public async Task Next_AChatOpenedWhileTheServerIsAskedIsClosedBeforeTheSwap()
        {
            var rig = new Rig();
            rig.Catalog.Pending = new TaskCompletionSource<IReadOnlyCollection<string>>();
            rig.First.Hold = true;

            var press = rig.Cycle.NextAsync();
            rig.Coordinator.Open(Mara);
            Assert.IsTrue(rig.Coordinator.Submit("Quick, before the switch."));
            Assert.IsTrue(rig.Coordinator.IsAwaitingReply);

            rig.Catalog.Pending.SetResult(List);
            await Run.NotCancelled(press);

            Assert.IsFalse(rig.Coordinator.IsOpen, "The chat opened mid-press is closed before the new model is in.");
            Assert.IsFalse(rig.Coordinator.IsAwaitingReply);
            Assert.IsTrue(rig.First.LastToken.IsCancellationRequested, "The old model's turn is cancelled.");
            Assert.AreEqual(0, rig.Driver.ExecutedCount);
        }

        [Test]
        public async Task Next_ACancelledPressChangesNothingAndRaisesNothing()
        {
            var rig = new Rig();
            rig.Catalog.Pending = new TaskCompletionSource<IReadOnlyCollection<string>>();
            int finished = 0;
            rig.Cycle.Finished += () => finished++;

            using (var owner = new CancellationTokenSource())
            {
                var press = rig.Cycle.NextAsync(owner.Token);
                owner.Cancel();
                rig.Catalog.Pending.SetResult(List);
                await Run.NotCancelled(press);
                Assert.IsFalse(press.Result);
            }

            Assert.AreEqual(0, finished);
            Assert.AreEqual(0, rig.Built.Count);
            Assert.AreSame(rig.Driver, rig.Drivers.Current);
            Assert.IsFalse(rig.Cycle.Busy);
            Assert.AreEqual("qwen3.5:2b", rig.Cycle.Selector.Current, "The selection stays with the driver in use.");

            rig.Catalog.Pending = null;
            await Run.NotCancelled(rig.Cycle.NextAsync());
            Assert.AreEqual("gemma4:e2b-it-qat", rig.Cycle.Selector.Current, "The next press picks the next model, none skipped.");
        }

        [Test]
        public async Task Next_ServerDown_ChangesNothingButTheNotice()
        {
            var rig = new Rig();
            rig.Catalog.Installed = null;

            await Run.NotCancelled(rig.Cycle.NextAsync());

            Assert.AreSame(rig.Driver, rig.Cycle.Driver);
            Assert.AreEqual("Agenerela · qwen3.5:2b", rig.Drivers.Label);
            Assert.AreEqual(0, rig.Built.Count);
            StringAssert.Contains("not answering", rig.Cycle.Notice);
        }

        [Test]
        public async Task Next_ASecondPressWhileAskingDoesNothing()
        {
            var rig = new Rig();
            rig.Catalog.Pending = new TaskCompletionSource<IReadOnlyCollection<string>>();
            int finished = 0;
            rig.Cycle.Finished += () => finished++;

            var first = rig.Cycle.NextAsync();
            var second = rig.Cycle.NextAsync();
            await Run.NotCancelled(second);
            Assert.IsFalse(second.Result);
            Assert.AreEqual(1, rig.Catalog.Calls);

            rig.Catalog.Pending.SetResult(List);
            await Run.NotCancelled(first);
            Assert.IsTrue(first.Result);
            Assert.AreEqual(1, rig.Built.Count);
            Assert.AreEqual(1, finished);
        }
    }
}
