using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Providers;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests
{
    // Saving memory with the game (DR-018, #190 C, Living Vale gap 11): IExportsMemory, the plain
    // MemorySnapshot it trades in, and RollingHistory's export and import.
    public sealed class MemorySnapshotTests
    {
        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public void ExportHoldsEveryExchangeOldestFirstThoseTheTokenCapLeavesOutIncluded()
        {
            var memory = new RollingHistory(turns: 6, maxTokens: 20);
            Remember(memory, "Player: \"One.\"", "none");
            Remember(memory, "Player: \"Two.\"", "none");
            Remember(memory, "Player: \"Three.\"", "none");
            Assert.That(memory.Recall(null).Count, Is.LessThan(6), "The budget leaves some out of a request.");

            var snapshot = memory.Export();

            Assert.That(snapshot.Format, Is.EqualTo(RollingHistory.SnapshotFormat));
            Assert.That(snapshot.Version, Is.EqualTo(RollingHistory.SnapshotVersion));
            Assert.That(snapshot.Exchanges.Select(e => e.Stimulus), Is.EqualTo(new[] { "Player: \"One.\"", "Player: \"Two.\"", "Player: \"Three.\"" }));
            Assert.That(snapshot.Exchanges.Select(e => e.Decision), Has.All.EqualTo(Written("none")));
            Assert.That(snapshot.Data, Is.Null);
        }

        [Test]
        public void ChangingAnExportChangesNothingInTheMemory()
        {
            var memory = new RollingHistory();
            Remember(memory, "Player: \"One.\"", "none");

            var snapshot = memory.Export();
            snapshot.Exchanges[0].Stimulus = "Player: \"Changed.\"";
            snapshot.Exchanges.Add(new MemoryExchange("Player: \"Two.\"", "{}"));

            Assert.That(memory.Count, Is.EqualTo(1));
            Assert.That(memory.Recall(null)[0], Is.EqualTo("Player: \"One.\""));
            Assert.That(memory.Export(), Is.Not.SameAs(snapshot), "Each export is a new snapshot.");
        }

        [Test]
        public void ImportReplacesWhatWasHeld()
        {
            var memory = new RollingHistory();
            Remember(memory, "Player: \"Before the save.\"", "none");
            var snapshot = Own();
            snapshot.Exchanges.Add(new MemoryExchange("Player: \"From the save.\"", "{\"action\":\"follow_player\"}"));

            memory.Import(snapshot);

            Assert.That(memory.Recall(null), Is.EqualTo(new[] { "Player: \"From the save.\"", "{\"action\":\"follow_player\"}" }));
        }

        [Test]
        public void ImportKeepsTheNewestTurnsAndCountsTheRestForgotten()
        {
            var memory = new RollingHistory(turns: 2);
            var snapshot = Own();
            for (int i = 1; i <= 5; i++)
            {
                snapshot.Exchanges.Add(new MemoryExchange($"Player: \"Order {i}.\"", "{}"));
            }

            memory.Import(snapshot);

            Assert.That(memory.Count, Is.EqualTo(2));
            Assert.That(memory.Export().Exchanges.Select(e => e.Stimulus), Is.EqualTo(new[] { "Player: \"Order 4.\"", "Player: \"Order 5.\"" }));
            Assert.That(memory.Forgotten, Is.EqualTo(3));
        }

        // LV #22: a save that names another strategy, or a version this one cannot read, is refused
        // outright, as Living Vale's own check refused it, and CanImport says so first.
        [TestCase("someone.facts", 1, "format 'someone.facts'")]
        [TestCase(null, 1, "no format")]
        [TestCase("", 1, "no format")]
        [TestCase(RollingHistory.SnapshotFormat, 0, "version 0")]
        [TestCase(RollingHistory.SnapshotFormat, RollingHistory.SnapshotVersion + 1, "reads versions 1 to")]
        public void ImportRefusesAnotherFormatOrAVersionItCannotRead(string format, int version, string said)
        {
            var memory = new RollingHistory();
            Remember(memory, "Player: \"Kept.\"", "none");
            var snapshot = new MemorySnapshot { Format = format, Version = version };
            snapshot.Exchanges.Add(new MemoryExchange("Player: \"Hello.\"", "{}"));

            Assert.That(memory.CanImport(snapshot, out string reason), Is.False);
            Assert.That(reason, Does.Contain(said));
            var thrown = Assert.Throws<ArgumentException>(() => memory.Import(snapshot));
            Assert.That(thrown.Message, Does.StartWith(reason), "Import refuses it for the reason CanImport gives.");
            Assert.That(memory.Recall(null), Is.EqualTo(new[] { "Player: \"Kept.\"", Written("none") }), "Nothing was imported.");
        }

        [Test]
        public void CanImportSaysYesWithoutChangingAnything()
        {
            var memory = new RollingHistory();
            Remember(memory, "Player: \"Kept.\"", "none");
            var snapshot = Own(new MemoryExchange("Player: \"From the save.\"", "{}"));
            snapshot.Data = "{\"anything\":true}";

            Assert.That(memory.CanImport(snapshot, out string reason), Is.True);
            Assert.That(reason, Is.Null);
            Assert.That(memory.Recall(null), Is.EqualTo(new[] { "Player: \"Kept.\"", Written("none") }), "Checking imports nothing.");

            memory.Import(snapshot);
            Assert.That(memory.Recall(null), Is.EqualTo(new[] { "Player: \"From the save.\"", "{}" }));
            Assert.That(memory.Export().Data, Is.Null, "Data is not read.");
        }

        [Test]
        public void CanImportRefusesNoSnapshotWithoutThrowing()
        {
            Assert.That(new RollingHistory().CanImport(null, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("There is no snapshot."));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ImportRefusesAMissingPartAndKeepsWhatWasHeld(int broken)
        {
            var memory = new RollingHistory();
            Remember(memory, "Player: \"Kept.\"", "none");
            var snapshot = Own();
            snapshot.Exchanges.Add(new MemoryExchange("Player: \"Fine.\"", "{}"));
            snapshot.Exchanges.Add(broken == 0 ? null
                : broken == 1 ? new MemoryExchange(null, "{}")
                : new MemoryExchange("Player: \"Half.\"", null));

            Assert.That(memory.CanImport(snapshot, out string reason), Is.False);
            var thrown = Assert.Throws<ArgumentException>(() => memory.Import(snapshot));

            Assert.That(reason, Does.Contain("Exchanges[1]"));
            Assert.That(thrown.Message, Does.StartWith(reason));
            Assert.That(memory.Recall(null), Is.EqualTo(new[] { "Player: \"Kept.\"", Written("none") }), "Nothing was imported.");
        }

        [Test]
        public void ImportOfNothing()
        {
            var memory = new RollingHistory();
            Remember(memory, "Player: \"Kept.\"", "none");

            Assert.Throws<ArgumentNullException>(() => memory.Import(null));
            Assert.That(memory.Count, Is.EqualTo(1));

            var none = Own();
            none.Exchanges = null;
            memory.Import(none);
            Assert.That(memory.Count, Is.Zero, "No exchanges is an empty memory.");
        }

        [Test]
        public void ASnapshotInsideASaveSurvivesJsonUtility()
        {
            // How Living Vale saves: a [Serializable] object of public fields, through JsonUtility.
            var memory = new RollingHistory();
            Remember(memory, "Player: \"Bread, please.\"", "quote_sale");
            var save = new GameSave { gold = 12, guardMemory = memory.Export() };

            var loaded = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));
            var restored = new RollingHistory();
            restored.Import(loaded.guardMemory);

            Assert.That(loaded.gold, Is.EqualTo(12));
            Assert.That(loaded.guardMemory.Format, Is.EqualTo(RollingHistory.SnapshotFormat));
            Assert.That(restored.Recall(null), Is.EqualTo(memory.Recall(null)));
        }

        [Test]
        public void ASnapshotSurvivesNewtonsoft()
        {
            var memory = new RollingHistory();
            Remember(memory, "Report: \"Grain stores fell.\"", "none");

            var back = JsonConvert.DeserializeObject<MemorySnapshot>(JsonConvert.SerializeObject(memory.Export()));
            var restored = new RollingHistory();
            restored.Import(back);

            Assert.That(restored.Recall(null), Is.EqualTo(memory.Recall(null)));
        }

        [Test]
        public async Task AnAgentLoadedFromASaveSendsTheHistoryItWasSavedWith()
        {
            const string Reply = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";
            var before = new FakeProvider(Reply);
            var saved = Fixtures.VillageGuard(before);
            await Fixtures.NotCancelled(saved.DecideAsync("Go to the tower."));
            var snapshot = ((IExportsMemory)saved.Memory).Export();

            var after = new FakeProvider(Reply);
            var loaded = Fixtures.VillageGuard(after);
            ((IExportsMemory)loaded.Memory).Import(snapshot);
            await Fixtures.NotCancelled(loaded.DecideAsync("Now the bridge."));

            Assert.That(after.LastRequest.History, Is.EqualTo(new[] { "Player: \"Go to the tower.\"", Reply }));
        }

        // Remembers one exchange the way Agent would: the stimulus as sent, then the decision,
        // which RollingHistory writes as Written shows, there being no schema.
        private static void Remember(RollingHistory memory, string stimulus, string action)
        {
            var request = new DecisionRequest { Stimulus = stimulus };
            var result = new DecisionResult(new AgentDecision(action, TargetRegistry.NoTarget, ""), new DecisionTelemetry());
            memory.Remember(null, request, result);
        }

        // An empty snapshot in RollingHistory's own format, the one Import takes, with these exchanges.
        private static MemorySnapshot Own(params MemoryExchange[] exchanges)
        {
            var snapshot = new MemorySnapshot { Format = RollingHistory.SnapshotFormat, Version = RollingHistory.SnapshotVersion };
            snapshot.Exchanges.AddRange(exchanges);
            return snapshot;
        }

        private static string Written(string action)
        {
            return "{\"action\":\"" + action + "\",\"target\":\"no_target\",\"statement\":\"\"}";
        }

        [Serializable]
        private sealed class GameSave
        {
            public int gold;
            public MemorySnapshot guardMemory;
        }
    }
}
