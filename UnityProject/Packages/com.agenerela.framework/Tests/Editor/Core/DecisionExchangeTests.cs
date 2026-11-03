using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // DecisionResult.Request and DecisionResult.Reply (#142): each result carries the request its
    // decision sent and the reply it got, so a game can tell its agents' exchanges apart on one
    // provider's queue without wrapping the provider. Through Agent.DecideAsync and FakeProvider.
    // Every await of DecideAsync goes through Fixtures.NotCancelled (see AgentTests) unless
    // cancellation is what the test is about.
    public sealed class DecisionExchangeTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";
        private const string NoTowerLine = "{\"statement\":\"I will not go there.\"}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public async Task ADecisionThatPassedCarriesTheRequestSentAndTheReply()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var watcher = new FakeGuard("Watcher");
            agent.Guards.Add(watcher);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Verdict, Is.Null);
            Assert.That(result.Request, Is.SameAs(fake.LastRequest));
            Assert.That(result.Reply, Is.SameAs(watcher.Inputs[0].Reply), "The provider's own result, not a copy.");
            Assert.That(result.Reply.Text, Is.EqualTo(GoToTheTower));
        }

        [Test]
        public async Task AGuardStoppedDecisionCarriesBothAndTheReplyIsTheModelsOriginalAnswer()
        {
            // The guard scripts the refusal line's reply, so the second request gets a different one.
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var stopper = new FakeGuard("Stopper", _ =>
            {
                fake.Reply = NoTowerLine;
                return GuardVerdict.RewriteToNone("the tower was the wrong one.");
            });
            agent.Guards.Add(stopper);

            var result = await Fixtures.NotCancelled(
                agent.DecideAsync("Go to the tower.", new DecideOptions { AskForRefusalLine = true }));

            Assert.That(fake.Requests, Has.Count.EqualTo(2), "The refusal line was asked for.");
            Assert.That(result.Decision.Statement, Is.EqualTo("I will not go there."));
            Assert.That(result.Request, Is.SameAs(fake.Requests[0]), "The decision's request, not the refusal line's.");
            Assert.That(result.Reply, Is.SameAs(stopper.Inputs[0].Reply));
            Assert.That(result.Reply.Text, Is.EqualTo(GoToTheTower));
        }

        [TestCase(false, TestName = "AProviderThatThrowsCarriesTheRequestAndNoReply")]
        [TestCase(true, TestName = "AProviderThatReturnsNoResultCarriesTheRequestAndNoReply")]
        public async Task AProviderFailureCarriesTheRequestAndNoReply(bool returnsNoResult)
        {
            var fake = new FakeProvider(GoToTheTower);
            if (returnsNoResult)
            {
                fake.ReturnsNoResult = true;
            }
            else
            {
                fake.Failure = new InvalidOperationException("The server is down.");
            }

            var agent = Fixtures.VillageGuard(fake);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. The provider"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(result.Request, Is.SameAs(fake.LastRequest));
            Assert.That(result.Reply, Is.Null);
        }

        [Test]
        public async Task AReplyThatCannotBeReadIsCarriedSoTheGameCanShowWhy()
        {
            var fake = new FakeProvider("I think I will go to the tower.");
            var agent = Fixtures.VillageGuard(fake);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\."));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.UnreadableReply));
            Assert.That(result.Request, Is.SameAs(fake.LastRequest));
            Assert.That(result.Reply.Text, Is.EqualTo("I think I will go to the tower."));
        }

        [Test]
        public async Task AGuardThatThrowsLeavesTheExchangeOnTheResult()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(new FakeGuard("Broken", _ => throw new InvalidOperationException("The guard broke.")));
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. The guard 'Broken'"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.GuardFailed));
            Assert.That(result.Request, Is.SameAs(fake.LastRequest));
            Assert.That(result.Reply.Text, Is.EqualTo(GoToTheTower));
        }

        [Test]
        public async Task ARequestThatCouldNotBePreparedCarriesNeither()
        {
            // The availability check reads a state key the game never set.
            var fake = new FakeProvider(GoToTheTower);
            var agent = new Agent(Fixtures.VillageGuardProfile(), fake);
            agent.Bind("follow_player", new FakeHandler(ctx => !(bool)ctx.State["isFollowing"]));
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. Preparing the request failed\\."));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(fake.Requests, Is.Empty);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Reply, Is.Null);
        }

        [Test]
        public async Task ARequestCancelledWhileQueuedHasNoResultAndTheNextCallerGetsItsOwn()
        {
            // Cancelling ends the call with OperationCanceledException, so there is no result to carry
            // anything: the cancelled request's place goes to the one behind it, with nothing of the
            // cancelled one's on its result.
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            var inFlight = agent.DecideAsync("The bell rings once.");

            using (var cts = new CancellationTokenSource())
            {
                var cancelled = agent.DecideAsync("Walk away from me.", null, cts.Token);
                var behind = agent.DecideAsync("Go to the tower.");

                cts.Cancel();
                try
                {
                    await cancelled;
                    Assert.Fail("A cancelled decision has no result.");
                }
                catch (OperationCanceledException)
                {
                }

                fake.Release();
                fake.Release();
                await Fixtures.NotCancelled(inFlight);
                var result = await Fixtures.NotCancelled(behind);

                Assert.That(fake.Requests, Has.Count.EqualTo(2), "The cancelled request was never sent.");
                Assert.That(result.Request, Is.SameAs(fake.Requests[1]));
                Assert.That(result.Request.Stimulus, Does.Contain("Go to the tower."));
                Assert.That(result.Request.Stimulus, Does.Not.Contain("Walk away from me."));
            }
        }

        [Test]
        public async Task TwoAgentsOnOneProviderWithMixedPrioritiesEachGetTheirOwnExchange()
        {
            // The queue sends player-facing requests first (DR-015), so they reach the provider in
            // an order different from the one they were asked in. Each is answered with a reply of
            // its own, so a mix-up shows in the replies too.
            var background = new DecideOptions { Priority = DecisionPriority.Background };
            var fake = new FakeProvider { Hold = true };
            var guard = Fixtures.VillageGuard(fake);
            var sentry = Fixtures.VillageGuard(fake);

            var guardBell = guard.DecideAsync("The bell rings once.", background);
            var sentryCart = sentry.DecideAsync("A cart rolls past.", background);
            var guardTower = guard.DecideAsync("Go to the tower.");
            var sentryDummy = sentry.DecideAsync("Attack the training dummy.");

            // Sent in this order: the one in flight, then the player-facing two, then the cart.
            string[] stimuli = { "The bell rings once.", "Go to the tower.", "Attack the training dummy.", "A cart rolls past." };
            string[] replies = new string[stimuli.Length];
            for (int i = 0; i < stimuli.Length; i++)
            {
                Assert.That(fake.LastRequest.Stimulus, Does.Contain(stimuli[i]), $"Request {i} was sent out of the expected order.");
                replies[i] = $"{{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"Reply {i}.\"}}";
                fake.Reply = replies[i];
                fake.Release();
            }

            var results = new[]
            {
                await Fixtures.NotCancelled(guardBell),
                await Fixtures.NotCancelled(guardTower),
                await Fixtures.NotCancelled(sentryDummy),
                await Fixtures.NotCancelled(sentryCart),
            };

            for (int i = 0; i < results.Length; i++)
            {
                Assert.That(results[i].Request, Is.SameAs(fake.Requests[i]), $"'{stimuli[i]}' got another request.");
                Assert.That(results[i].Request.Stimulus, Does.Contain(stimuli[i]));
                Assert.That(results[i].Reply.Text, Is.EqualTo(replies[i]), $"'{stimuli[i]}' got another reply.");
                Assert.That(results[i].Decision.Statement, Is.EqualTo($"Reply {i}."));
            }
        }
    }
}
