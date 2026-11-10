using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Agenerela;
using LivingVale.Game;
using LivingVale.Interaction;
using NUnit.Framework;
using UnityEngine;

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

        // Continuations resume on the editor's context, a frame later; wait for them, boundedly.
        private static async Task WaitUntil(Func<bool> condition)
        {
            for (int i = 0; i < 200 && !condition(); i++)
            {
                await Task.Yield();
            }

            Assert.IsTrue(condition(), "Timed out waiting for the condition.");
        }

        private sealed class Rig
        {
            public readonly FakeLlmProvider First;

            // The models unloaded, in order, whichever provider asked; with HoldUnloads, each
            // unload stays out until the test releases it.
            public readonly List<string> Unloaded = new List<string>();
            public bool HoldUnloads;
            public readonly List<AwaitableCompletionSource<bool>> PendingUnloads = new List<AwaitableCompletionSource<bool>>();

            // Each settle delay, released by hand; one cancelled by a later press ends cancelled.
            public readonly List<TaskCompletionSource<bool>> Delays = new List<TaskCompletionSource<bool>>();

            /// <summary>Whether the providers built from now on hold their warm-ups until cancelled.</summary>
            public bool NewProvidersHold;
            public readonly List<(string model, FakeLlmProvider provider)> Built = new List<(string, FakeLlmProvider)>();
            public readonly FakeCatalog Catalog = new FakeCatalog { Installed = List };
            public readonly TradeService Trade;
            public readonly AgenerelaNpcDecisionDriver Driver;
            public readonly SwitchableNpcDecisionDriver Drivers;
            public readonly ConversationCoordinator Coordinator;
            public readonly ModelCycle Cycle;

            private FakeLlmProvider Server(string model)
            {
                var provider = new FakeLlmProvider { HoldWarmUp = NewProvidersHold };
                provider.OnUnload = () =>
                {
                    Unloaded.Add(model);
                    var unload = new AwaitableCompletionSource<bool>();
                    if (HoldUnloads)
                    {
                        PendingUnloads.Add(unload);
                    }
                    else
                    {
                        unload.TrySetResult(true);
                    }

                    return unload.Awaitable;
                };
                return provider;
            }

            public Rig()
            {
                First = Server("qwen3.5:2b");
                var catalog = ItemCatalog.CreateDefault();
                Trade = new TradeService(new PlayerInventory(catalog), new[] { VendorStock.CreateMara(catalog) }, new FakeTradeSession());
                Driver = new AgenerelaNpcDecisionDriver(First, "qwen3.5:2b", executor: new NpcActionExecutor(Trade));
                Drivers = new SwitchableNpcDecisionDriver(new FakeNpcDriver(), Driver, NpcDriverChoice.Agenerela);
                Coordinator = new ConversationCoordinator(Drivers);
                Cycle = new ModelCycle(Coordinator, Drivers, new ModelSelector(List, "qwen3.5:2b", Catalog), Driver,
                    model =>
                    {
                        var provider = Server(model);
                        Built.Add((model, provider));
                        return provider;
                    },
                    (wait, ct) =>
                    {
                        var delay = new TaskCompletionSource<bool>();
                        ct.Register(() => delay.TrySetCanceled());
                        Delays.Add(delay);
                        return delay.Task;
                    });
            }

            public async Task Press()
            {
                var press = Cycle.NextAsync();
                await Run.NotCancelled(press);
                Assert.IsTrue(press.Result);
            }

            public async Task Settle(int delay)
            {
                Delays[delay].SetResult(true);
                await Run.NotCancelled(Cycle.Loading);
            }
        }

        [Test]
        public async Task RapidPresses_OnlyTheModelThePlayerSettlesOnIsLoaded()
        {
            var rig = new Rig();
            await rig.Press();
            await rig.Press();
            await rig.Press();

            Assert.IsTrue(rig.Delays[0].Task.IsCanceled && rig.Delays[1].Task.IsCanceled, "Each press cancels the one before it.");
            Assert.AreEqual(0, rig.Built[2].provider.WarmUps, "Nothing is loaded until the key rests.");

            await rig.Settle(2);

            Assert.AreEqual(0, rig.Built[0].provider.WarmUps);
            Assert.AreEqual(0, rig.Built[1].provider.WarmUps);
            Assert.AreEqual(1, rig.Built[2].provider.WarmUps, "One warm-up, for the model the player stopped on.");
        }

        [Test]
        public async Task ANewPressCancelsAWarmUpAlreadySent()
        {
            var rig = new Rig { NewProvidersHold = true };
            await rig.Press();
            rig.Delays[0].SetResult(true);
            await WaitUntil(() => rig.Built[0].provider.WarmUps == 1);
            Assert.IsFalse(rig.Built[0].provider.LastWarmUpToken.IsCancellationRequested);

            await rig.Press();

            Assert.IsTrue(rig.Built[0].provider.LastWarmUpToken.IsCancellationRequested, "A superseded warm-up is aborted.");
        }

        [Test]
        public async Task APressAbortsTheStartingModelsWarmUp()
        {
            var rig = new Rig();
            rig.First.HoldWarmUp = true;
            rig.Cycle.LoadCurrent();
            rig.Delays[0].SetResult(true);
            await WaitUntil(() => rig.First.WarmUps == 1);

            await rig.Press();

            Assert.IsTrue(rig.First.LastWarmUpToken.IsCancellationRequested, "The replaced model's warm-up is aborted.");
        }

        [Test]
        public async Task ASettleReplacedDuringItsUnloadDoesNotRewriteWhatIsLoaded()
        {
            var rig = new Rig();
            rig.Catalog.Installed = new[] { "qwen3.5:2b", "gemma4:e2b-it-qat" };
            rig.HoldUnloads = true;

            await rig.Press();                       // to gemma
            rig.Delays[0].SetResult(true);
            await WaitUntil(() => rig.PendingUnloads.Count == 1);   // unloading qwen, held

            await rig.Press();                       // back to qwen
            rig.Delays[1].SetResult(true);
            rig.PendingUnloads[0].SetResult(true);   // the stale unload lands
            await WaitUntil(() => rig.Built[1].provider.WarmUps == 1);   // qwen warmed

            rig.HoldUnloads = false;
            await rig.Press();                       // to gemma again
            await rig.Settle(2);

            CollectionAssert.AreEqual(new[] { "qwen3.5:2b", "qwen3.5:2b" }, rig.Unloaded,
                "qwen was loaded again, so leaving it unloads it again.");
        }

        [Test]
        public async Task AModelLoadedThroughF6IsUnloadedByTheNextSwitch()
        {
            var rig = new Rig();
            rig.Drivers.Select(NpcDriverChoice.Scripted);
            await rig.Press();                       // to gemma while scripted replies are on
            await rig.Settle(0);
            CollectionAssert.AreEqual(new[] { "qwen3.5:2b" }, rig.Unloaded);
            Assert.AreEqual(0, rig.Built[0].provider.WarmUps, "Nothing is loaded while the scripted driver answers.");

            rig.Drivers.Select(NpcDriverChoice.Agenerela);   // F6
            rig.Cycle.LoadCurrent();
            await rig.Settle(1);
            Assert.AreEqual(1, rig.Built[0].provider.WarmUps, "F6 loads gemma.");

            await rig.Press();                       // to llama
            await rig.Settle(2);
            CollectionAssert.AreEqual(new[] { "qwen3.5:2b", rig.Built[0].model }, rig.Unloaded, "gemma leaves the card.");
        }

        [Test]
        public async Task F6OffAndOnDuringAWarmUp_SendsItAgain()
        {
            var rig = new Rig { NewProvidersHold = true };
            await rig.Press();
            rig.Delays[0].SetResult(true);
            await WaitUntil(() => rig.Built[0].provider.WarmUps == 1);

            rig.Drivers.Select(NpcDriverChoice.Scripted);    // F6 off
            rig.Drivers.Select(NpcDriverChoice.Agenerela);   // and on
            rig.Cycle.LoadCurrent();
            rig.Built[0].provider.HoldWarmUp = false;
            await rig.Settle(1);

            Assert.AreEqual(2, rig.Built[0].provider.WarmUps, "The cancelled warm-up is sent again, not reused.");
            Assert.AreEqual("", rig.Cycle.Notice, "The model loaded.");
        }

        [Test]
        public async Task TheStartingModelLoadsThroughTheSettleAndSaysWhenItCannot()
        {
            // As at startup, with the default model missing from the server.
            var rig = new Rig();
            rig.First.WarmUpFailure = new global::Agenerela.Providers.ProviderException(
                global::Agenerela.Providers.ProviderErrorKind.ModelNotFound, "fake", "Not pulled.");

            rig.Cycle.LoadCurrent();
            await rig.Settle(0);

            Assert.AreEqual(1, rig.First.WarmUps, "One warm-up for the starting model.");
            CollectionAssert.IsEmpty(rig.Unloaded, "Nothing else was loaded, so nothing is unloaded.");
            StringAssert.Contains("did not load", rig.Cycle.Notice);
            StringAssert.Contains("F7", rig.Cycle.Notice, "The notice says how to keep playing.");
        }

        [Test]
        public void TheGamesDefaultModelIsFirstAndTheFrameworksIsStillReachable()
        {
            Assert.AreEqual("gemma4:e2b-it-qat", ModelCycleDefaults.Models[0]);
            Assert.AreEqual("qwen3.5:2b", ModelCycleDefaults.Models[1]);
        }

        [Test]
        public async Task Settling_UnloadsTheModelLoadedBeforeAndNeverAPassedOne()
        {
            var rig = new Rig();
            await rig.Press();
            await rig.Settle(0);
            CollectionAssert.AreEqual(new[] { "qwen3.5:2b" }, rig.Unloaded, "The starting model leaves the card.");

            await rig.Press();
            await rig.Press();
            await rig.Settle(2);

            CollectionAssert.AreEqual(new[] { "qwen3.5:2b", rig.Built[0].model }, rig.Unloaded,
                "Only the model actually loaded is unloaded; the one pressed past never was.");
        }

        [Test]
        public async Task TheNoticeSaysWhileTheModelLoads()
        {
            var rig = new Rig { NewProvidersHold = true };
            int changes = 0;
            rig.Cycle.NoticeChanged += () => changes++;
            await rig.Press();
            rig.Delays[0].SetResult(true);
            await WaitUntil(() => changes == 1);

            StringAssert.Contains("loading " + rig.Built[0].model, rig.Cycle.Notice);

            var firstLoad = rig.Cycle.Loading;
            await rig.Press();
            await Run.NotCancelled(firstLoad);
            Assert.AreEqual(1, changes, "A superseded load says nothing more.");
            Assert.IsFalse(rig.Cycle.Notice.Contains(rig.Built[0].model), "The old model's loading line is gone.");
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
            StringAssert.Contains(GameActions.QuotePurchase, request.SystemPrompt, "The agent keeps its trade actions on the new provider.");
            Assert.AreEqual(1, rig.First.Requests.Count, "The old model is asked nothing more.");
            Assert.AreSame(rig.Driver, rig.Cycle.Driver, "F7 keeps the driver and its agents; only the provider changes (#198).");
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
