using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // Switching an agent's provider at runtime (DR-019, #190 workstream D): the next decision goes
    // to the new one, everything else about the agent stays, and a decision under way finishes
    // where it started. Every await of DecideAsync goes through Fixtures.NotCancelled.
    public sealed class AgentProviderTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public void AnAgentHasTheProviderItWasBuiltWith()
        {
            var fake = new FakeProvider();
            var agent = Fixtures.VillageGuard(fake);

            Assert.That(agent.Provider, Is.SameAs(fake));
        }

        [Test]
        public void SettingNoProviderIsRefusedAndKeepsTheOne()
        {
            var fake = new FakeProvider();
            var agent = Fixtures.VillageGuard(fake);

            Assert.Throws<ArgumentNullException>(() => agent.Provider = null);
            Assert.That(agent.Provider, Is.SameAs(fake));
        }

        [Test]
        public async Task TheNextDecisionGoesToTheNewProviderAndTheAgentKeepsEverythingElse()
        {
            var before = new FakeProvider(GoToTheTower, "before");
            var after = new FakeProvider(GoToTheTower, "after");
            var agent = Fixtures.VillageGuard(before);
            var guard = new FakeGuard("Watching");
            agent.Guards.Add(guard);
            agent.State["shift"] = "night";
            var memory = agent.Memory;
            await Fixtures.NotCancelled(agent.DecideAsync("The bell rings once."));

            agent.Provider = after;
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(before.Requests, Has.Count.EqualTo(1), "The old provider is asked nothing more.");
            Assert.That(after.Requests, Has.Count.EqualTo(1));
            Assert.That(result.Telemetry.ProviderName, Is.EqualTo("after"));
            Assert.That(agent.Memory, Is.SameAs(memory));
            Assert.That(after.LastRequest.History, Has.Count.EqualTo(2), "It remembers what it decided on the old provider.");
            Assert.That(after.LastRequest.History[0], Does.Contain("The bell rings once."));
            Assert.That(after.LastRequest.Schema.Fields[0].AllowedValues, Is.EqualTo(before.LastRequest.Schema.Fields[0].AllowedValues),
                "The same actions.");
            Assert.That(after.LastRequest.Schema.Fields[1].AllowedValues, Is.EqualTo(before.LastRequest.Schema.Fields[1].AllowedValues),
                "The same targets.");
            Assert.That(guard.Inputs, Has.Count.EqualTo(2), "The same guards.");
            Assert.That(agent.State["shift"], Is.EqualTo("night"));
            Assert.That(agent.Execute(result), Is.True, "The same handlers.");
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ADecisionUnderWayFinishesOnTheProviderItStartedWith()
        {
            var before = new FakeProvider(GoToTheTower, "before") { Hold = true };
            var after = new FakeProvider(GoToTheTower, "after");
            var agent = Fixtures.VillageGuard(before);
            var underWay = agent.DecideAsync("Go to the tower.");

            agent.Provider = after;
            before.Release();
            var result = await Fixtures.NotCancelled(underWay);

            Assert.That(result.Telemetry.ProviderName, Is.EqualTo("before"));
            Assert.That(after.Requests, Is.Empty);
        }

        [Test]
        public async Task ARefusalLineGoesToTheProviderItsDecisionWasMadeOn()
        {
            // The second request of a stopped decision (#109) is part of that decision.
            var before = new FakeProvider("{\"action\":\"attack_target\",\"target\":\"tower\",\"statement\":\"Charge!\"}", "before") { Hold = true };
            var after = new FakeProvider("{\"statement\":\"Not the tower.\"}", "after");
            var agent = Fixtures.VillageGuard(before);
            agent.Guards.Add(new DelegateGuard("NoTowers", input => input.Decision.TargetId == "tower"
                ? GuardVerdict.RewriteToNone("Never attack the tower.")
                : GuardVerdict.Pass));
            var underWay = agent.DecideAsync("Attack the tower.", new DecideOptions { AskForRefusalLine = true });

            agent.Provider = after;
            before.Release();
            Assert.That(before.HeldCount, Is.EqualTo(1), "Its refusal line went where the decision was made.");
            before.Reply = "{\"statement\":\"I will not.\"}";
            before.Release();

            var result = await Fixtures.NotCancelled(underWay);

            Assert.That(before.Requests, Has.Count.EqualTo(2), "The decision, then its refusal line.");
            Assert.That(after.Requests, Is.Empty);
            Assert.That(result.Decision.Statement, Is.EqualTo("I will not."));
        }

        [Test]
        public async Task TheNewProvidersQueueIsTheOneTheAgentWaitsIn()
        {
            var before = new FakeProvider(GoToTheTower, "before") { Hold = true };
            var after = new FakeProvider(GoToTheTower, "after");
            var agent = Fixtures.VillageGuard(before);
            var neighbour = Fixtures.VillageGuard(before);
            var busy = neighbour.DecideAsync("The bell rings once.");

            agent.Provider = after;
            var deciding = agent.DecideAsync("Go to the tower.");

            Assert.That(deciding.GetAwaiter().IsCompleted, Is.True, "It did not wait behind the old provider's request.");
            var result = await Fixtures.NotCancelled(deciding);
            Assert.That(result.Telemetry.QueuedAhead, Is.Zero, "Nothing waits for the old provider's queue.");
            before.Release();
            await Fixtures.NotCancelled(busy);
        }

        [Test]
        public async Task TheNewProvidersHealthIsTheOneItReportsTo()
        {
            var before = new FakeProvider(GoToTheTower, "before");
            var after = new FakeProvider(GoToTheTower, "after");
            after.FailWith(ProviderErrorKind.ModelNotFound, "Run `ollama pull gemma4:e2b-it-qat`.");
            var agent = Fixtures.VillageGuard(before);
            agent.Provider = after;
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. The provider failed\\."));

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(ProviderHealth.For(after).IsDown, Is.True);
            Assert.That(ProviderHealth.For(before).IsDown, Is.False);
        }
    }
}
