using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // One agent's decision relayed as another's stimulus, in one chain (#190, G4): the chain id, hop
    // and source ride on DecideOptions.Chain into the telemetry, the feed and the decision log, and
    // never into the prompt.
    public sealed class DecisionRelayTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

        private readonly List<DecisionFeedEntry> entries = new List<DecisionFeedEntry>();
        private string folder;
        private DecisionLog log;

        [SetUp]
        public void Subscribe()
        {
            entries.Clear();
            DecisionFeed.Recorded += entries.Add;
            folder = Path.Combine(Path.GetTempPath(), "agenerela-relay-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void CleanUp()
        {
            DecisionFeed.Recorded -= entries.Add;
            log?.Stop();
            log = null;
            Fixtures.DestroyCreated();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        private static Agent Named(string id, FakeProvider provider = null)
        {
            var agent = Fixtures.VillageGuard(provider ?? new FakeProvider(GoToTheTower));
            agent.Id = id;
            return agent;
        }

        // ---- Carried across hops -------------------------------------------------------------

        [Test]
        public async Task ARelayStartsAChainAtHopOneFromTheSourceAgent()
        {
            var odile = Named("odile");
            var wren = Named("wren");

            var call = await Fixtures.NotCancelled(odile.DecideAsync("A thief took the bread."));
            var chase = await Fixtures.NotCancelled(odile.RelayAsync(call, wren, "Odile shouts: stop that thief!", "Merchant"));

            Assert.That(call.Telemetry.ChainId, Is.Null, "Odile was asked by the game, in no chain.");
            Assert.That(chase.Decision, Is.Not.Null);
            Assert.That(chase.Telemetry.ChainId, Is.Not.Null.And.Not.Empty, "The relay started a chain.");
            Assert.That(chase.Telemetry.ChainHop, Is.EqualTo(1));
            Assert.That(chase.Telemetry.ChainSource, Is.EqualTo("odile"));
            Assert.That(DecisionChain.Of(chase).Id, Is.EqualTo(chase.Telemetry.ChainId));

            var wrens = entries.Single(e => e.Agent == wren);
            Assert.That(wrens.Result.Telemetry.ChainId, Is.EqualTo(chase.Telemetry.ChainId), "The feed carries it.");
        }

        [Test]
        public async Task AChainKeepsItsIdAcrossThreeHopsEachFromTheAgentBefore()
        {
            var thief = Named("thief");
            var odile = Named("odile");
            var wren = Named("wren");

            var seen = await Fixtures.NotCancelled(thief.DecideAsync("You grab the bread.",
                new DecideOptions { StimulusLabel = "Event", Chain = DecisionChain.Start("theft-1") }));
            var call = await Fixtures.NotCancelled(thief.RelayAsync(seen, odile, "The thief runs off with your bread.", "Event"));
            var chase = await Fixtures.NotCancelled(odile.RelayAsync(call, wren, "Odile shouts: stop that thief!", "Merchant"));

            var chain = new[] { seen, call, chase };
            Assert.That(chain.Select(r => r.Telemetry.ChainId), Is.All.EqualTo("theft-1"));
            Assert.That(chain.Select(r => r.Telemetry.ChainHop), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(chain.Select(r => r.Telemetry.ChainSource), Is.EqualTo(new[] { null, "thief", "odile" }));
        }

        [Test]
        public async Task TheReceivingModelReadsTheStimulusAndLabelAsTheyWerePassed()
        {
            var relayed = new FakeProvider(GoToTheTower);
            var plain = new FakeProvider(GoToTheTower);
            var odile = Named("odile");
            var correlation = new object();

            var call = await Fixtures.NotCancelled(odile.DecideAsync("A thief took the bread."));
            var chase = await Fixtures.NotCancelled(odile.RelayAsync(call, Named("wren", relayed), "Stop that thief!", "Merchant",
                new DecideOptions { StimulusLabel = "Player", Correlation = correlation }));
            await Fixtures.NotCancelled(Named("wren", plain).DecideAsync("Stop that thief!",
                new DecideOptions { StimulusLabel = "Merchant", Correlation = correlation }));

            var sent = relayed.LastRequest;
            var control = plain.LastRequest;
            Assert.That(sent.StimulusLabel, Is.EqualTo("Merchant"), "The relay's label replaces the options'.");
            Assert.That(sent.StimulusText, Is.EqualTo("Stop that thief!"));
            Assert.That(sent.SystemPrompt, Is.EqualTo(control.SystemPrompt), "No chain text in the prompt.");
            Assert.That(sent.FewShotBlock, Is.EqualTo(control.FewShotBlock));
            Assert.That(sent.Stimulus, Is.EqualTo(control.Stimulus));
            Assert.That(sent.Observations, Is.EqualTo(control.Observations));
            Assert.That(chase.Correlation, Is.SameAs(correlation), "The caller's correlation is kept.");
        }

        // ---- The hop cap ---------------------------------------------------------------------

        [Test]
        public async Task ARelayPastTheCapIsRefusedAndNeverSent()
        {
            var fake = new FakeProvider(GoToTheTower);
            var hale = Named("hale", fake);
            var wren = Named("wren", fake);

            var order = await Fixtures.NotCancelled(hale.DecideAsync("Send a guard."));
            var first = await Fixtures.NotCancelled(hale.RelayAsync(order, wren, "Go.", "Captain", maxHops: 2));
            var back = await Fixtures.NotCancelled(wren.RelayAsync(first, hale, "Done.", "Guard", maxHops: 2));
            int sent = fake.Requests.Count;
            entries.Clear();

            var refused = await Fixtures.NotCancelled(hale.RelayAsync(back, wren, "Go again.", "Captain", maxHops: 2));

            Assert.That(back.Telemetry.ChainHop, Is.EqualTo(2), "Up to the cap, relays are sent.");
            Assert.That(refused.Decision, Is.Null);
            Assert.That(refused.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(refused.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ChainTooLong));
            Assert.That(refused.Telemetry.ErrorMessage, Does.Contain("hop 3").And.Contain("cap of 2"));
            Assert.That(refused.Telemetry.ChainHop, Is.EqualTo(3));
            Assert.That(refused.Telemetry.ChainId, Is.EqualTo(first.Telemetry.ChainId));
            Assert.That(fake.Requests.Count, Is.EqualTo(sent), "Nothing was sent.");
            Assert.That(entries, Has.Count.EqualTo(1), "The refusal is on the feed, so the log shows where the chain stopped.");
            Assert.That(entries[0].Agent, Is.SameAs(wren));
            Assert.That(entries[0].Result, Is.SameAs(refused));
        }

        [Test]
        public async Task TwoAgentsRelayingToEachOtherStopAtTheDefaultCap()
        {
            var a = Named("a");
            var b = Named("b");

            var last = await Fixtures.NotCancelled(a.DecideAsync("Begin."));
            var from = a;
            int relays = 0;
            while (relays < 50)
            {
                var to = from == a ? b : a;
                last = await Fixtures.NotCancelled(from.RelayAsync(last, to, "Your turn.", "Friend"));
                if (last.Decision == null) break;
                relays++;
                from = to;
            }

            Assert.That(relays, Is.EqualTo(DecisionRelay.DefaultMaxHops));
            Assert.That(last.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ChainTooLong));
        }

        [Test]
        public void ACapBelowOneIsAMistake()
        {
            var a = Named("a");
            var result = new DecisionResult(null, new DecisionTelemetry { Outcome = DecisionOutcome.PipelineError });
            Assert.That(() => a.RelayAsync(result, a, "Hi.", "Friend", maxHops: 0).GetAwaiter().GetResult(),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        // ---- Unrelayed -----------------------------------------------------------------------

        [Test]
        public async Task ADecisionWithNoRelayHasNoChain()
        {
            var agent = Named("wren");

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.ChainId, Is.Null);
            Assert.That(result.Telemetry.ChainHop, Is.Zero);
            Assert.That(result.Telemetry.ChainSource, Is.Null);
            Assert.That(DecisionChain.Of(result), Is.Null);
        }

        // ---- The decision log ----------------------------------------------------------------

        [Test]
        public async Task TheLogWritesAndReadsBackTheChainAndLeavesItOutWithoutOne()
        {
            var path = Path.Combine(folder, "decisions.jsonl");
            var odile = Named("odile");
            var wren = Named("wren");
            log = DecisionLog.Start(path);

            var call = await Fixtures.NotCancelled(odile.DecideAsync("A thief took the bread."));
            var chase = await Fixtures.NotCancelled(odile.RelayAsync(call, wren, "Stop that thief!", "Merchant"));
            Assert.That(wren.Execute(chase), Is.True);
            Assert.That(log.Flush(Wait), Is.True);

            var raw = File.ReadAllLines(path);
            Assert.That(raw, Has.Length.EqualTo(3));
            var plain = JObject.Parse(raw[0]);
            Assert.That(plain.ContainsKey("chainId"), Is.False, "No chain, no field.");
            Assert.That(plain.ContainsKey("chainHop"), Is.False);
            Assert.That(plain.ContainsKey("chainSource"), Is.False);

            var lines = DecisionLogReader.Read(path);
            Assert.That(lines[0].ChainId, Is.Null);
            Assert.That(lines[0].ChainHop, Is.Null);
            Assert.That(lines[1].ChainId, Is.EqualTo(chase.Telemetry.ChainId));
            Assert.That(lines[1].ChainHop, Is.EqualTo(1));
            Assert.That(lines[1].ChainSource, Is.EqualTo("odile"));
            Assert.That(lines[1].Label, Is.EqualTo("Merchant"));
            Assert.That(lines[2].Stage, Is.EqualTo(DecisionStage.Executed));
            Assert.That(lines[2].ChainId, Is.EqualTo(chase.Telemetry.ChainId), "The chase is in the chain too.");

            var groups = lines.Where(l => l.ChainId != null).GroupBy(l => l.ChainId).ToList();
            Assert.That(groups, Has.Count.EqualTo(1), "A log groups by chain.");
        }

        [Test]
        public void AVersionOneLineWithoutTheChainStillReads()
        {
            Assert.That(DecisionLogReader.TryParse(
                "{\"version\":1,\"sequence\":3,\"stage\":\"Decided\",\"agentId\":\"wren\",\"label\":\"Merchant\"}", out var line), Is.True);
            Assert.That(line.Version, Is.EqualTo(1));
            Assert.That(line.AgentId, Is.EqualTo("wren"));
            Assert.That(line.ChainId, Is.Null);
            Assert.That(line.ChainHop, Is.Null);
            Assert.That(line.ChainSource, Is.Null);
        }
    }
}
