using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // When memory records a decision (DR-018, #190 C, Living Vale gap 3): OnDecide, the default,
    // as DecideAsync returns it; OnExecute, when the game settles it through Execute, Commit or
    // Discard, so a reply dropped as stale is never remembered.
    public sealed class MemoryCommitTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public async Task ByDefaultADecisionIsRememberedAsItIsReturned()
        {
            var (agent, _, spy) = Guard();

            Assert.That(agent.MemoryCommit, Is.EqualTo(MemoryCommit.OnDecide));
            var result = await Decide(agent);

            Assert.That(spy.Remembered.Select(r => r.Result), Is.EqualTo(new[] { result }));
            Assert.That(agent.Commit(result), Is.False, "Nothing is held to commit.");
            Assert.That(agent.Discard(result), Is.False);
            agent.Execute(result);
            Assert.That(spy.Remembered, Has.Count.EqualTo(1), "Executing it does not remember it again.");
        }

        [Test]
        public async Task OnExecuteADecisionIsRememberedOnlyOnceExecuted()
        {
            var (agent, fake, _) = Guard();
            agent.Memory = new RollingHistory();
            agent.MemoryCommit = MemoryCommit.OnExecute;

            var result = await Decide(agent);
            await Decide(agent, "Still there?");
            Assert.That(fake.LastRequest.History, Is.Empty, "Neither decision is remembered yet.");

            Assert.That(agent.Execute(result), Is.True);
            await Decide(agent, "Anything else?");

            Assert.That(fake.LastRequest.History, Is.EqualTo(new[] { "Player: \"Go to the tower.\"", GoToTheTower }),
                "Only the executed one, as it would have been remembered on deciding.");
        }

        [Test]
        public async Task TheHeldDecisionIsRememberedWithItsOwnContextAndRequest()
        {
            var (agent, fake, spy) = Guard();
            agent.MemoryCommit = MemoryCommit.OnExecute;

            var result = await Decide(agent);
            var request = fake.LastRequest;
            agent.Execute(result);

            Assert.That(spy.Remembered, Has.Count.EqualTo(1));
            Assert.That(spy.Remembered[0].Ctx, Is.SameAs(spy.Recalled[0]), "The context it was decided in, not the one Execute made.");
            Assert.That(spy.Remembered[0].Request, Is.SameAs(request));
            Assert.That(spy.Remembered[0].Result, Is.SameAs(result));
        }

        [Test]
        public async Task ADecisionNeverSettledIsNeverRemembered()
        {
            var (agent, _, spy) = Guard();
            agent.MemoryCommit = MemoryCommit.OnExecute;

            await Decide(agent, "Go to the tower.");
            var kept = await Decide(agent, "Go to the tower, now.");
            agent.Execute(kept);

            Assert.That(spy.Remembered.Select(r => r.Result), Is.EqualTo(new[] { kept }), "The dropped reply is not remembered.");
        }

        [Test]
        public async Task ExecutingTwiceRemembersOnce()
        {
            var (agent, _, spy) = Guard();
            agent.MemoryCommit = MemoryCommit.OnExecute;
            var result = await Decide(agent);

            agent.Execute(result);
            agent.Execute(result);
            Assert.That(agent.Commit(result), Is.False, "Already settled.");

            Assert.That(spy.Remembered, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ADecisionTheRecheckRefusesWaitsForTheGame()
        {
            var fake = new FakeProvider("{\"action\":\"follow_player\",\"target\":\"no_target\",\"statement\":\"Lead the way.\"}");
            var agent = Fixtures.VillageGuard(fake);
            var spy = new SpyMemory();
            agent.Memory = spy;
            agent.MemoryCommit = MemoryCommit.OnExecute;
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Keep me company."));
            agent.State["isFollowing"] = true;
            LogAssert.Expect(LogType.Warning, new Regex("the action is not available right now"));

            Assert.That(agent.Execute(result), Is.False);
            Assert.That(spy.Remembered, Is.Empty, "It was not carried out.");

            // The game showed the line anyway, so it keeps it.
            Assert.That(agent.Commit(result), Is.True);
            Assert.That(spy.Remembered.Select(r => r.Result), Is.EqualTo(new[] { result }));
            Assert.That(agent.Commit(result), Is.False, "Once only.");
        }

        [Test]
        public async Task ADecisionWhoseHandlerFailedWaitsForTheGame()
        {
            bool stale = true;
            var fake = new FakeProvider(GoToTheTower);
            var agent = new Agent(Fixtures.VillageGuardProfile(), fake);
            agent.Bind("move_to", DelegateActionHandler.WithResult(ctx => true,
                (ctx, d) => stale ? ActionResult.Failed("The quote went stale.") : ActionResult.Done()));
            agent.Targets.Register("tower", new object());
            var spy = new SpyMemory();
            agent.Memory = spy;
            agent.MemoryCommit = MemoryCommit.OnExecute;
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(agent.Execute(result), Is.False);
            Assert.That(spy.Remembered, Is.Empty);

            Assert.That(agent.Discard(result), Is.True);
            stale = false;
            Assert.That(agent.Execute(result), Is.True, "Executing still works once discarded.");
            Assert.That(spy.Remembered, Is.Empty, "A discarded decision is never remembered.");
            Assert.That(agent.Discard(result), Is.False, "Once only.");
        }

        [Test]
        public async Task ANoneDecisionIsRememberedWhenExecuted()
        {
            var (agent, fake, spy) = Guard();
            fake.Reply = "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}";
            agent.MemoryCommit = MemoryCommit.OnExecute;
            var result = await Decide(agent, "Nice weather.");

            Assert.That(agent.Execute(result), Is.True);

            Assert.That(spy.Remembered.Select(r => r.Result), Is.EqualTo(new[] { result }));
        }

        [Test]
        public async Task TheModeIsReadWhenTheDecisionStarts()
        {
            var (agent, fake, spy) = Guard();
            fake.Hold = true;
            agent.MemoryCommit = MemoryCommit.OnExecute;

            var pending = agent.DecideAsync("Go to the tower.");
            agent.MemoryCommit = MemoryCommit.OnDecide;
            fake.Release();
            var result = await Fixtures.NotCancelled(pending);

            Assert.That(spy.Remembered, Is.Empty, "It started under OnExecute, so it waits.");
            Assert.That(agent.Commit(result), Is.True);
        }

        [Test]
        public async Task TheHeldDecisionGoesIntoTheMemoryItRecalledFrom()
        {
            var (agent, _, spy) = Guard();
            agent.MemoryCommit = MemoryCommit.OnExecute;
            var result = await Decide(agent);
            var replacement = new SpyMemory();
            agent.Memory = replacement;

            agent.Execute(result);

            Assert.That(spy.Remembered, Has.Count.EqualTo(1));
            Assert.That(replacement.Remembered, Is.Empty);
        }

        [Test]
        public async Task OnlyTheAgentThatDecidedSettlesIt()
        {
            var (agent, _, spy) = Guard();
            agent.MemoryCommit = MemoryCommit.OnExecute;
            var result = await Decide(agent);
            var (other, _, otherSpy) = Guard();
            other.MemoryCommit = MemoryCommit.OnExecute;

            Assert.Throws<ArgumentException>(() => other.Commit(result));
            Assert.Throws<ArgumentException>(() => other.Discard(result));
            Assert.That(other.Execute(result), Is.True, "Another agent may still carry it out...");
            Assert.That(spy.Remembered, Is.Empty, "...but not settle this agent's memory.");
            Assert.That(otherSpy.Remembered, Is.Empty);

            Assert.That(agent.Commit(result), Is.True, "Still waiting for its own agent.");
            Assert.That(spy.Remembered, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task APipelineErrorHoldsNothing()
        {
            var (agent, fake, spy) = Guard();
            fake.ReturnsNoResult = true;
            agent.MemoryCommit = MemoryCommit.OnExecute;
            LogAssert.Expect(LogType.Warning, new Regex("could not decide"));
            var failed = await Decide(agent);

            Assert.That(agent.Commit(failed), Is.False);
            Assert.That(spy.Remembered, Is.Empty);
        }

        [Test]
        public async Task WithNoMemoryNothingIsHeld()
        {
            var (agent, _, _) = Guard();
            agent.Memory = null;
            agent.MemoryCommit = MemoryCommit.OnExecute;
            var result = await Decide(agent);

            Assert.That(agent.Commit(result), Is.False, "Nothing is held without a memory to hold it for.");
            Assert.That(agent.Execute(result), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task AMemoryThatFailsOnSettlingIsLoggedAndTheDecisionStillCarriedOut()
        {
            var (agent, _, spy) = Guard();
            agent.MemoryCommit = MemoryCommit.OnExecute;
            var result = await Decide(agent);
            spy.FailsToRemember = true;
            LogAssert.Expect(LogType.Warning, new Regex("its memory could not remember it\\. InvalidOperationException: The spy refuses\\."));

            Assert.That(agent.Execute(result), Is.True);
            Assert.That(agent.Commit(result), Is.False, "Settled all the same, so never tried twice.");
        }

        [Test]
        public async Task ASettledDecisionLetsGoOfWhatItHeld()
        {
            var (agent, _, _) = Guard();
            agent.MemoryCommit = MemoryCommit.OnExecute;
            var kept = await Decide(agent);
            var dropped = await Decide(agent);
            Assert.That(kept.Pending.Context, Is.Not.Null, "Held until settled.");

            agent.Execute(kept);
            agent.Discard(dropped);

            // Its targets may be scene objects, which a result kept in a log must not keep alive.
            Assert.That(kept.Pending.Context, Is.Null);
            Assert.That(kept.Pending.Memory, Is.Null);
            Assert.That(dropped.Pending.Context, Is.Null);
        }

        [Test]
        public async Task AHeldDecisionKeepsNoReferenceToItsAgent()
        {
            // So a result kept in a log, settled or not, does not keep the agent and all it
            // reaches alive. Only the agent's token and name are held.
            var (agent, _, _) = Guard();
            agent.MemoryCommit = MemoryCommit.OnExecute;
            var result = await Decide(agent);
            var fields = typeof(PendingMemory).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(fields, Is.Not.Empty);

            foreach (var field in fields)
            {
                Assert.That(field.GetValue(result.Pending), Is.Not.SameAs(agent), $"{field.Name}, while held");
            }

            agent.Execute(result);

            foreach (var field in fields)
            {
                Assert.That(field.GetValue(result.Pending), Is.Not.SameAs(agent), $"{field.Name}, once settled");
            }
        }

        [Test]
        public void CommitAndDiscardRefuseNoResult()
        {
            var (agent, _, _) = Guard();

            Assert.Throws<ArgumentNullException>(() => agent.Commit(null));
            Assert.Throws<ArgumentNullException>(() => agent.Discard(null));
        }

        [Test]
        public void TheModeIsOneTheEnumDefines()
        {
            var (agent, _, _) = Guard();

            Assert.Throws<ArgumentOutOfRangeException>(() => agent.MemoryCommit = (MemoryCommit)7);
            Assert.That(agent.MemoryCommit, Is.EqualTo(MemoryCommit.OnDecide));
        }

        // A village guard with a spy for memory.
        private static (Agent Agent, FakeProvider Fake, SpyMemory Spy) Guard()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var spy = new SpyMemory();
            agent.Memory = spy;
            return (agent, fake, spy);
        }

        private static Task<DecisionResult> Decide(Agent agent, string stimulus = "Go to the tower.")
        {
            return Fixtures.NotCancelled(agent.DecideAsync(stimulus));
        }

        private sealed class SpyMemory : IMemoryStrategy
        {
            public bool FailsToRemember;
            public readonly List<AgentContext> Recalled = new List<AgentContext>();
            public readonly List<(AgentContext Ctx, DecisionRequest Request, DecisionResult Result)> Remembered =
                new List<(AgentContext, DecisionRequest, DecisionResult)>();

            public IReadOnlyList<string> Recall(AgentContext ctx)
            {
                Recalled.Add(ctx);
                return Array.Empty<string>();
            }

            public void Remember(AgentContext ctx, DecisionRequest request, DecisionResult result)
            {
                if (FailsToRemember)
                {
                    throw new InvalidOperationException("The spy refuses.");
                }

                Remembered.Add((ctx, request, result));
            }
        }
    }
}
