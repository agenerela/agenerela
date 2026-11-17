using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // ProviderHealth on its own (#190, G1): what a report does to the state, when Changed is
    // raised, and when the back-off holds a request back. Its clock is replaced, so a retry time
    // passes without waiting. AgentHealthTests checks that agents report through it.
    public sealed class ProviderHealthTests
    {
        private static readonly DateTime Start = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

        private DateTime now;

        private ProviderHealth Health()
        {
            now = Start;
            var health = ProviderHealth.For(new FakeProvider());
            health.Clock = () => now;
            return health;
        }

        [Test]
        public void OneProviderHasOneHealthAndEachProviderItsOwn()
        {
            var provider = new FakeProvider();

            Assert.That(ProviderHealth.For(provider), Is.SameAs(ProviderHealth.For(provider)));
            Assert.That(ProviderHealth.For(new FakeProvider()), Is.Not.SameAs(ProviderHealth.For(provider)));
            Assert.Throws<ArgumentNullException>(() => ProviderHealth.For(null));
        }

        [Test]
        public void ANewProviderIsUpWithTheBackOffOff()
        {
            var health = Health();

            Assert.That(health.IsDown, Is.False);
            Assert.That(health.Reason, Is.Null);
            Assert.That(health.Message, Is.Null);
            Assert.That(health.DownSinceUtc, Is.Null);
            Assert.That(health.RetryAtUtc, Is.Null);
            Assert.That(health.ConsecutiveFailures, Is.Zero);
            Assert.That(health.BackOff, Is.False, "Opt-in: today's behaviour by default (DR-015, as amended).");
            Assert.That(health.FirstRetrySeconds, Is.EqualTo(5f));
            Assert.That(health.LongestRetrySeconds, Is.EqualTo(60f));
            Assert.That(health.ToString(), Is.EqualTo("up"));
        }

        [Test]
        public void AProviderFailureMarksItDownWithTheReasonTheWordsAndARetryTime()
        {
            var health = Health();

            health.ReportFailure(PipelineErrorKind.ProviderUnreachable, "Nothing answered at http://127.0.0.1:11434.");

            Assert.That(health.IsDown, Is.True);
            Assert.That(health.Reason, Is.EqualTo(PipelineErrorKind.ProviderUnreachable));
            Assert.That(health.Message, Is.EqualTo("Nothing answered at http://127.0.0.1:11434."));
            Assert.That(health.DownSinceUtc, Is.EqualTo(Start));
            Assert.That(health.RetryAtUtc, Is.EqualTo(Start.AddSeconds(5)));
            Assert.That(health.ConsecutiveFailures, Is.EqualTo(1));
            Assert.That(health.ToString(), Does.StartWith("down (ProviderUnreachable) since 12:00:00 UTC, 1 failure in a row, retry at 12:00:05"));
        }

        [Test]
        public void EachFailureInARowDoublesTheWaitUpToTheLongest()
        {
            var health = Health();
            var waits = new List<double>();

            for (int i = 0; i < 6; i++)
            {
                health.ReportFailure(PipelineErrorKind.ProviderTimeout, "No reply.");
                waits.Add((health.RetryAtUtc.Value - now).TotalSeconds);
                now = now.AddSeconds(100);
            }

            Assert.That(waits, Is.EqualTo(new double[] { 5, 10, 20, 40, 60, 60 }));
            Assert.That(health.DownSinceUtc, Is.EqualTo(Start), "Down since the first failure of the run.");
            Assert.That(health.ConsecutiveFailures, Is.EqualTo(6));
        }

        [Test]
        public void TheRetrySettingsApplyFromTheNextFailure()
        {
            var health = Health();
            health.LongestRetrySeconds = 4f;
            health.FirstRetrySeconds = 1f;

            health.ReportFailure(PipelineErrorKind.ProviderServerError, "500.");
            Assert.That(health.RetryAtUtc, Is.EqualTo(Start.AddSeconds(1)));
            health.ReportFailure(PipelineErrorKind.ProviderServerError, "500.");
            health.ReportFailure(PipelineErrorKind.ProviderServerError, "500.");
            health.ReportFailure(PipelineErrorKind.ProviderServerError, "500.");
            Assert.That(health.RetryAtUtc, Is.EqualTo(Start.AddSeconds(4)), "1, 2, 4, then capped at 4.");

            health.Reset();
            health.FirstRetrySeconds = 10f;
            health.ReportFailure(PipelineErrorKind.ProviderServerError, "500.");
            Assert.That(health.RetryAtUtc, Is.EqualTo(Start.AddSeconds(4)), "No wait is longer than the longest, the first included.");
        }

        [Test]
        public void ARetrySettingOutOfRangeIsRefusedAndChangesNothing()
        {
            var health = Health();

            Assert.Throws<ArgumentOutOfRangeException>(() => health.FirstRetrySeconds = 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => health.FirstRetrySeconds = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => health.FirstRetrySeconds = float.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => health.LongestRetrySeconds = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => health.LongestRetrySeconds = float.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => health.FirstRetrySeconds = 1e12f, "Finite, but past what a DateTime can hold (Codex review, F3).");
            Assert.Throws<ArgumentOutOfRangeException>(() => health.LongestRetrySeconds = 86401f, "More than a day.");
            Assert.That(health.FirstRetrySeconds, Is.EqualTo(5f));
            Assert.That(health.LongestRetrySeconds, Is.EqualTo(60f));
        }

        [Test]
        public void TheLongestRetrySettingsStillGiveARetryTimeAndChangeTheStateWhole()
        {
            var health = Health();
            health.LongestRetrySeconds = 86400f;
            health.FirstRetrySeconds = 86400f;

            Assert.DoesNotThrow(() => health.ReportFailure(PipelineErrorKind.ProviderTimeout, "No reply."));
            Assert.That(health.RetryAtUtc, Is.EqualTo(Start.AddDays(1)));
            health.BackOff = true;
            Assert.That(health.HoldsBack(DecisionPriority.Background, out _), Is.True);

            now = DateTime.MaxValue.AddHours(-1);
            Assert.DoesNotThrow(() => health.ReportFailure(PipelineErrorKind.ProviderTimeout, "No reply."),
                "A clock near the end of time saturates rather than throwing half way through a change.");
            Assert.That(health.RetryAtUtc, Is.EqualTo(DateTime.MaxValue));
            Assert.That(health.ConsecutiveFailures, Is.EqualTo(2));
        }

        [Test]
        public void AReplyMarksItUpAgainAndTheNextRunStartsFromTheFirstWait()
        {
            var health = Health();
            health.ReportFailure(PipelineErrorKind.ModelNotFound, "Run `ollama pull qwen3.5:2b`.");
            health.ReportFailure(PipelineErrorKind.ModelNotFound, "Run `ollama pull qwen3.5:2b`.");

            health.ReportReply();

            Assert.That(health.IsDown, Is.False);
            Assert.That(health.Reason, Is.Null);
            Assert.That(health.Message, Is.Null);
            Assert.That(health.DownSinceUtc, Is.Null);
            Assert.That(health.RetryAtUtc, Is.Null);
            Assert.That(health.ConsecutiveFailures, Is.Zero);

            now = Start.AddSeconds(30);
            health.ReportFailure(PipelineErrorKind.ProviderUnreachable, "Nothing answered.");
            Assert.That(health.RetryAtUtc, Is.EqualTo(now.AddSeconds(5)));
            Assert.That(health.DownSinceUtc, Is.EqualTo(now));
        }

        [Test]
        public void ResetMarksItUpAsAReplyWould()
        {
            var health = Health();
            health.ReportFailure(PipelineErrorKind.ProviderUnreachable, "Nothing answered.");

            health.Reset();

            Assert.That(health.IsDown, Is.False);
            Assert.That(health.ConsecutiveFailures, Is.Zero);
        }

        [TestCase(PipelineErrorKind.RequestPreparation)]
        [TestCase(PipelineErrorKind.UnreadableReply)]
        [TestCase(PipelineErrorKind.GuardFailed)]
        [TestCase(PipelineErrorKind.Superseded)]
        [TestCase(PipelineErrorKind.BacklogFull)]
        [TestCase(PipelineErrorKind.ChainTooLong)]
        [TestCase(PipelineErrorKind.ProviderDown)]
        public void AFailureThatIsNotTheProvidersChangesNothing(PipelineErrorKind kind)
        {
            var health = Health();
            int changes = 0;
            health.Changed += _ => changes++;

            health.ReportFailure(kind, "Not the provider's.");

            Assert.That(health.IsDown, Is.False);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void ChangedIsRaisedOnGoingDownOnEachFailureWhileDownAndOnComingBackNeverOtherwise()
        {
            var health = Health();
            var seen = new List<string>();
            health.Changed += h => seen.Add(h.IsDown ? $"down {h.Reason} {h.ConsecutiveFailures}" : "up");

            health.ReportReply();
            health.Reset();
            health.ReportFailure(PipelineErrorKind.ProviderUnreachable, "Nothing answered.");
            health.ReportFailure(PipelineErrorKind.ModelNotFound, "Not pulled.");
            health.ReportReply();
            health.ReportReply();

            Assert.That(seen, Is.EqualTo(new[] { "down ProviderUnreachable 1", "down ModelNotFound 2", "up" }),
                "Nothing while it was up already; the reason follows the last failure.");
        }

        [Test]
        public void AHandlerThatThrowsIsLoggedAndTheOthersStillRun()
        {
            var health = Health();
            bool second = false;
            health.Changed += _ => throw new InvalidOperationException("The HUD broke.");
            health.Changed += _ => second = true;
            LogAssert.Expect(LogType.Warning, new Regex("A ProviderHealth\\.Changed handler failed\\. InvalidOperationException: The HUD broke\\."));

            Assert.DoesNotThrow(() => health.ReportFailure(PipelineErrorKind.ProviderUnreachable, "Nothing answered."));

            Assert.That(second, Is.True);
            Assert.That(health.IsDown, Is.True);
        }

        [Test]
        public void WithTheBackOffOffNothingIsHeldBack()
        {
            var health = Health();
            health.ReportFailure(PipelineErrorKind.ProviderUnreachable, "Nothing answered.");

            Assert.That(health.HoldsBack(DecisionPriority.Background, out string why), Is.False);
            Assert.That(why, Is.Null);
        }

        [Test]
        public void WithTheBackOffOnABackgroundRequestIsHeldBackUntilTheRetryTime()
        {
            var health = Health();
            health.BackOff = true;
            Assert.That(health.HoldsBack(DecisionPriority.Background, out _), Is.False, "Up: everything is sent.");

            health.ReportFailure(PipelineErrorKind.ProviderUnreachable, "Nothing answered at http://127.0.0.1:11434.");

            now = Start.AddSeconds(2);
            Assert.That(health.HoldsBack(DecisionPriority.Background, out string why), Is.True);
            Assert.That(why, Is.EqualTo("Its provider is down (ProviderUnreachable), so background requests are not sent "
                                      + "for another 3 s. Nothing answered at http://127.0.0.1:11434."));
            Assert.That(health.HoldsBack(DecisionPriority.PlayerFacing, out why), Is.False, "Someone is waiting: always sent.");
            Assert.That(why, Is.Null);

            now = Start.AddSeconds(5);
            Assert.That(health.HoldsBack(DecisionPriority.Background, out _), Is.False, "At the retry time, the next one is sent and finds out.");
            Assert.That(health.IsDown, Is.True, "Still down until a reply says otherwise.");
        }

        [TestCase(PipelineErrorKind.ProviderUnreachable, true)]
        [TestCase(PipelineErrorKind.ModelNotFound, true)]
        [TestCase(PipelineErrorKind.ProviderTimeout, true)]
        [TestCase(PipelineErrorKind.ProviderRejected, true)]
        [TestCase(PipelineErrorKind.ProviderServerError, true)]
        [TestCase(PipelineErrorKind.ProviderMalformedResponse, true)]
        [TestCase(PipelineErrorKind.ProviderFailed, true)]
        [TestCase(PipelineErrorKind.ProviderDown, true)]
        [TestCase(PipelineErrorKind.RequestPreparation, false)]
        [TestCase(PipelineErrorKind.UnreadableReply, false)]
        [TestCase(PipelineErrorKind.GuardFailed, false)]
        [TestCase(PipelineErrorKind.Superseded, false)]
        [TestCase(PipelineErrorKind.BacklogFull, false)]
        [TestCase(PipelineErrorKind.ChainTooLong, false)]
        public void IsProviderFaultSaysWhetherTheFailureWasTheProviders(PipelineErrorKind kind, bool expected)
        {
            Assert.That(kind.IsProviderFault(), Is.EqualTo(expected));
        }

        [Test]
        public void EveryErrorKindIsClassified()
        {
            // A kind added later must be put on one side on purpose: this lists them all.
            var classified = new[]
            {
                PipelineErrorKind.RequestPreparation, PipelineErrorKind.ProviderUnreachable, PipelineErrorKind.ModelNotFound,
                PipelineErrorKind.ProviderTimeout, PipelineErrorKind.ProviderRejected, PipelineErrorKind.ProviderServerError,
                PipelineErrorKind.ProviderMalformedResponse, PipelineErrorKind.ProviderFailed, PipelineErrorKind.UnreadableReply,
                PipelineErrorKind.GuardFailed, PipelineErrorKind.ProviderDown, PipelineErrorKind.Superseded,
                PipelineErrorKind.BacklogFull, PipelineErrorKind.ChainTooLong,
            };

            Assert.That(Enum.GetValues(typeof(PipelineErrorKind)), Is.EquivalentTo(classified),
                "Add the new kind to IsProviderFault's switch, or leave it out on purpose, and to the cases above.");
        }
    }
}
