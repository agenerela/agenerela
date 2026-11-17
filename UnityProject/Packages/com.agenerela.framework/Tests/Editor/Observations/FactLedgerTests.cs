using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // The fact ledger (#216 item 4, #266 item 5): keyed facts in a stable order, worded per reader,
    // expiring by the game's Tick, under a budget reported through IReportsDroppedObservations,
    // saved and loaded, and read by several agents at once. Every await of DecideAsync goes through
    // Fixtures.NotCancelled.
    public sealed class FactLedgerTests
    {
        private static string[] Read(IObservationSource source) => source.Observe(null).ToArray();

        // ---- Keys and order ---------------------------------------------------------------------

        [Test]
        public void LinesComeInTheOrderFactsWereSet()
        {
            var ledger = new FactLedger();
            ledger.Set("a", "The well is dry.");
            ledger.Set("b", "Orsk broke a truce with us on turn 6.");
            ledger.Set("c", "Plague in kesh.");

            Assert.That(Read(ledger), Is.EqualTo(new[] { "The well is dry.", "Orsk broke a truce with us on turn 6.", "Plague in kesh." }));
            Assert.That(Read(ledger), Is.EqualTo(Read(ledger)), "The same on every call.");
            Assert.That(ledger.Keys, Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void SettingAKeyAgainReplacesTheLineAndMakesItTheNewest()
        {
            var ledger = new FactLedger();
            ledger.Set("well", "The well is low.");
            ledger.Set("truce", "We hold a truce with orsk.");
            ledger.Word("well", "kesh", "Our well is low.");

            ledger.Set("well", "The well is dry.", expiresAfter: 2);

            Assert.That(Read(ledger), Is.EqualTo(new[] { "We hold a truce with orsk.", "The well is dry." }));
            Assert.That(ledger.Count, Is.EqualTo(2));
            Assert.That(Read(ledger.For("kesh")).Last(), Is.EqualTo("The well is dry."), "Setting again clears the wordings.");
            Assert.That(ledger.Remaining("well"), Is.EqualTo(2));
        }

        [Test]
        public void RemoveForgetsAFact()
        {
            var ledger = new FactLedger();
            ledger.Set("a", "One.");

            Assert.That(ledger.Remove("a"), Is.True);
            Assert.That(ledger.Remove("a"), Is.False);
            Assert.That(ledger.Contains("a"), Is.False);
            Assert.That(Read(ledger), Is.Empty);
            Assert.That(ledger.Remaining("a"), Is.EqualTo(-1));
        }

        // ---- Each in its own words ----------------------------------------------------------------

        [Test]
        public void AReaderReadsItsOwnWordingAndEveryoneElseTheSharedLine()
        {
            var ledger = new FactLedger();
            ledger.Set("plague_kesh", "A plague spreads in kesh.");
            Assert.That(ledger.Word("plague_kesh", "kesh", "Plague is killing our people."), Is.True);
            ledger.Set("secret", null);
            ledger.Word("secret", "orsk", "Our spies stand ready in fenmark.");

            Assert.That(Read(ledger.For("kesh")), Is.EqualTo(new[] { "Plague is killing our people." }));
            Assert.That(Read(ledger.For("orsk")), Is.EqualTo(new[] { "A plague spreads in kesh.", "Our spies stand ready in fenmark." }));
            Assert.That(Read(ledger.For("fenmark")), Is.EqualTo(new[] { "A plague spreads in kesh." }), "A fact with no shared line is only its readers'.");
            Assert.That(Read(ledger), Is.EqualTo(new[] { "A plague spreads in kesh." }));
            Assert.That(ledger.Word("missing", "kesh", "x"), Is.False);
        }

        [Test]
        public void ABlankWordingHidesTheFactFromThatReader()
        {
            var ledger = new FactLedger();
            ledger.Set("rumour", "They say orsk is weak.");
            ledger.Word("rumour", "orsk", null);

            Assert.That(Read(ledger.For("orsk")), Is.Empty);
            Assert.That(Read(ledger.For("kesh")), Is.EqualTo(new[] { "They say orsk is weak." }));
        }

        // ---- Expiry -------------------------------------------------------------------------------

        [Test]
        public void AFactIsReadUntilItsLastTickThenRemoved()
        {
            var ledger = new FactLedger();
            ledger.Set("storm", "A storm rages.", expiresAfter: 2);
            ledger.Set("well", "The well is dry.");

            Assert.That(ledger.Tick(), Is.Empty);
            Assert.That(Read(ledger), Is.EqualTo(new[] { "A storm rages.", "The well is dry." }));
            Assert.That(ledger.Remaining("storm"), Is.EqualTo(1));

            Assert.That(ledger.Tick(), Is.EqualTo(new[] { "storm" }));
            Assert.That(Read(ledger), Is.EqualTo(new[] { "The well is dry." }));

            for (int i = 0; i < 10; i++) ledger.Tick();
            Assert.That(ledger.Remaining("well"), Is.EqualTo(FactLedger.Forever), "Forever never expires.");
        }

        // ---- The budget ---------------------------------------------------------------------------

        [Test]
        public void TheBudgetKeepsTheNewestAndReportsTheOldest()
        {
            var ledger = new FactLedger { MaxLines = 2 };
            ledger.Set("a", "One.");
            ledger.Set("b", "Two.");
            ledger.Set("c", "Three.");
            var view = ledger.For("kesh");

            Assert.That(Read(view), Is.EqualTo(new[] { "Two.", "Three." }));
            Assert.That(view.Dropped, Is.EqualTo(new[] { "One." }));

            ledger.Remove("a");
            Read(view);
            Assert.That(view.Dropped, Is.Empty, "Each Observe replaces the report.");
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.MaxLines = -1);
        }

        [Test]
        public async Task TheAgentRecordsWhatTheBudgetDropped()
        {
            var ledger = new FactLedger { MaxLines = 1 };
            ledger.Set("a", "The well is dry.");
            ledger.Set("b", "Plague in kesh.");
            var fake = new FakeProvider("{\"action\":\"none\",\"statement\":\"\"}");
            var agent = new Agent(new AgentIdentity { Name = "Kesh" }, Array.Empty<ActionDefinition>(), fake);
            agent.ObservationSources.Add(ledger.For("kesh"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your move."));

            Assert.That(result.Telemetry.ObservationsDropped, Is.EqualTo(new[] { "The well is dry." }));
            Assert.That(fake.LastRequest.Observations, Does.Contain("Plague in kesh.").And.Not.Contain("The well is dry."));
        }

        // ---- Many readers ---------------------------------------------------------------------------

        [Test]
        public async Task OneLedgerIsReadByTwoAgentsEachInItsOwnWords()
        {
            var ledger = new FactLedger();
            ledger.Set("famine_kesh", "Famine strikes kesh.", expiresAfter: 3);
            ledger.Word("famine_kesh", "kesh", "Famine strikes our lands.");

            var keshProvider = new FakeProvider("{\"action\":\"none\",\"statement\":\"\"}");
            var orskProvider = new FakeProvider("{\"action\":\"none\",\"statement\":\"\"}");
            var kesh = new Agent(new AgentIdentity { Name = "Kesh" }, Array.Empty<ActionDefinition>(), keshProvider);
            var orsk = new Agent(new AgentIdentity { Name = "Orsk" }, Array.Empty<ActionDefinition>(), orskProvider);
            kesh.ObservationSources.Add(ledger.For("kesh"));
            orsk.ObservationSources.Add(ledger.For("orsk"));

            await Fixtures.NotCancelled(kesh.DecideAsync("Your move."));
            await Fixtures.NotCancelled(orsk.DecideAsync("Your move."));

            Assert.That(keshProvider.LastRequest.Observations, Does.Contain("Famine strikes our lands.").And.Not.Contain("Famine strikes kesh."));
            Assert.That(orskProvider.LastRequest.Observations, Does.Contain("Famine strikes kesh."));

            for (int i = 0; i < 3; i++) ledger.Tick();
            await Fixtures.NotCancelled(orsk.DecideAsync("Your move."));
            Assert.That(orskProvider.LastRequest.Observations ?? "", Does.Not.Contain("Famine"), "Expired for every reader at once.");
        }

        // ---- Saving -------------------------------------------------------------------------------

        [Test]
        public void ExportAndImportRoundTripFactsWordingsAndExpiry()
        {
            var ledger = new FactLedger();
            ledger.Set("a", "The well is dry.");
            ledger.Set("b", "Plague in kesh.", expiresAfter: 2);
            ledger.Word("b", "kesh", "Plague in our lands.");
            ledger.Tick();

            var snapshot = ledger.Export();
            var json = UnityEngine.JsonUtility.FromJson<MemorySnapshot>(UnityEngine.JsonUtility.ToJson(snapshot));
            var loaded = new FactLedger();
            Assert.That(loaded.CanImport(json), Is.True);
            loaded.Import(json);

            Assert.That(snapshot.Format, Is.EqualTo(FactLedger.SnapshotFormat));
            Assert.That(snapshot.Exchanges, Is.Empty);
            Assert.That(loaded.Keys, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(Read(loaded.For("kesh")), Is.EqualTo(new[] { "The well is dry.", "Plague in our lands." }));
            Assert.That(loaded.Remaining("b"), Is.EqualTo(1));

            ledger.Set("c", "Later.");
            Assert.That(loaded.Contains("c"), Is.False, "The snapshot is a copy.");
        }

        [Test]
        public void ASnapshotItCannotReadThrowsAndChangesNothing()
        {
            var ledger = new FactLedger();
            ledger.Set("a", "Kept.");
            var history = new RollingHistory().Export();
            var newer = new FactLedger().Export();
            newer.Version = FactLedger.SnapshotVersion + 1;
            var broken = new FactLedger().Export();
            broken.Data = "[{\"key\":\"a\"},{\"key\":\"a\"}]";

            Assert.That(ledger.CanImport(history), Is.False);
            Assert.Throws<ArgumentException>(() => ledger.Import(history));
            Assert.Throws<ArgumentException>(() => ledger.Import(newer));
            Assert.Throws<ArgumentException>(() => ledger.Import(broken));
            Assert.Throws<ArgumentNullException>(() => ledger.Import(null));
            Assert.That(Read(ledger), Is.EqualTo(new[] { "Kept." }));
        }

        // ---- Mistakes -------------------------------------------------------------------------------

        [Test]
        public void MistakesThrow()
        {
            var ledger = new FactLedger();

            Assert.Throws<ArgumentException>(() => ledger.Set(" ", "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Set("a", "x", -1));
            Assert.Throws<ArgumentException>(() => ledger.For(""));
            ledger.Set("a", "x");
            Assert.Throws<ArgumentException>(() => ledger.Word("a", null, "y"));
        }
    }
}
