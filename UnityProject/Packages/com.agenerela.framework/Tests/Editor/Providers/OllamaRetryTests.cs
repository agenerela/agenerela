using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // OllamaProvider's retries (DR-026): a server that answers busy, HTTP 503 or 429, gets the
    // request again after the pause its Retry-After asks for, or a jittered back-off, until the
    // RetryPolicy runs out. A scripted handler answers each send in turn, and the provider's Pause
    // seam records the pauses rather than waiting them out, except where a test needs a real wait.
    public sealed class OllamaRetryTests
    {
        private const string Reply = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public async Task BusyWithRetryAfterThenARepliesOnRetryAndRecordsIt()
        {
            var handler = new SequenceHandler(Busy(503, "7"), Ok());
            var pauses = new List<TimeSpan>();
            var provider = Provider(handler, new OllamaSettings(), pauses);

            var result = await NotCancelled(provider.RequestAsync(Request(), CancellationToken.None));

            Assert.That(result.Text, Is.EqualTo(Reply));
            Assert.That(handler.Bodies.Count, Is.EqualTo(2), "Sent once, then again after the busy answer.");
            Assert.That(handler.Bodies[1], Is.EqualTo(handler.Bodies[0]), "The same request, sent again unchanged.");
            Assert.That(pauses.Select(p => p.TotalSeconds), Is.EqualTo(new[] { 7d }), "The pause Retry-After asked for.");
            Assert.That(result.Retries, Is.EqualTo(1));
            Assert.That(result.RetryWaitSeconds, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public async Task ARetryAfterGivenAsAnHttpDateIsHonoured()
        {
            var handler = new SequenceHandler(Busy(429, date: DateTimeOffset.UtcNow.AddSeconds(6)), Ok());
            var pauses = new List<TimeSpan>();

            await NotCancelled(Provider(handler, new OllamaSettings(), pauses).RequestAsync(Request(), CancellationToken.None));

            Assert.That(pauses.Count, Is.EqualTo(1));
            Assert.That(pauses[0].TotalSeconds, Is.InRange(4d, 6d), "The time until the date, to the second it was written in.");
        }

        [Test]
        public async Task WithoutRetryAfterItBacksOffExponentiallyWithJitter()
        {
            var handler = new SequenceHandler(Busy(503), Busy(503), Busy(503), Ok());
            var pauses = new List<TimeSpan>();
            var settings = new OllamaSettings
            {
                Retry = new RetryPolicy { MaxAttempts = 4, MaxWaitSeconds = 60f, BackoffSeconds = 1f, MaxBackoffSeconds = 3f },
            };

            var result = await NotCancelled(Provider(handler, settings, pauses).RequestAsync(Request(), CancellationToken.None));

            Assert.That(result.Retries, Is.EqualTo(3));
            var seconds = pauses.Select(p => p.TotalSeconds).ToArray();
            Assert.That(seconds.Length, Is.EqualTo(3));
            Assert.That(seconds[0], Is.InRange(0.5d, 1d), "Half to all of the first back-off.");
            Assert.That(seconds[1], Is.InRange(1d, 2d), "Doubled.");
            Assert.That(seconds[2], Is.InRange(1.5d, 3d), "Doubled again, then held to MaxBackoffSeconds.");
        }

        [Test]
        public async Task RetriesRunningOutIsRateLimitedWithTheCount()
        {
            var handler = new SequenceHandler(Busy(503, "1", "{\"error\":\"queue wait exceeded\"}"));
            var pauses = new List<TimeSpan>();
            var settings = new OllamaSettings { Retry = new RetryPolicy { MaxAttempts = 3, MaxWaitSeconds = 60f } };

            var failure = await Fails(Provider(handler, settings, pauses).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.RateLimited));
            Assert.That(handler.Bodies.Count, Is.EqualTo(3), "MaxAttempts sends, the first included.");
            Assert.That(pauses.Count, Is.EqualTo(2), "No pause after the last.");
            Assert.That(failure.Retries, Is.EqualTo(2));
            Assert.That(failure.Message, Does.Contain("HTTP 503"));
            Assert.That(failure.Message, Does.Contain("3 attempts"));
            Assert.That(failure.Message, Does.Contain("RetryPolicy.MaxAttempts"));
            Assert.That(failure.Message, Does.Contain("queue wait exceeded"), "What the server said.");
        }

        [Test]
        public async Task ARetryAfterPastMaxWaitSecondsFailsAtOnce()
        {
            var handler = new SequenceHandler(Busy(503, "120"), Ok());
            var pauses = new List<TimeSpan>();
            var settings = new OllamaSettings { Retry = new RetryPolicy { MaxAttempts = 5, MaxWaitSeconds = 10f } };

            var failure = await Fails(Provider(handler, settings, pauses).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.RateLimited));
            Assert.That(pauses, Is.Empty, "A pause it knows is too long is never begun.");
            Assert.That(handler.Bodies.Count, Is.EqualTo(1));
            Assert.That(failure.Message, Does.Contain("RetryPolicy.MaxWaitSeconds"));
            Assert.That(failure.Message, Does.Contain("120 s"));
        }

        [Test]
        public async Task APausePastTheDeadlineFailsAtOnce()
        {
            var handler = new SequenceHandler(Busy(503, "5"), Ok());
            var pauses = new List<TimeSpan>();
            var settings = new OllamaSettings { Retry = new RetryPolicy { MaxAttempts = 5, MaxWaitSeconds = 60f, DeadlineSeconds = 3f } };

            var failure = await Fails(Provider(handler, settings, pauses).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.RateLimited));
            Assert.That(pauses, Is.Empty);
            Assert.That(failure.Message, Does.Contain("RetryPolicy.DeadlineSeconds"));
        }

        [Test]
        public async Task WithRetriesOffABusyAnswerIsRateLimitedAtOnce()
        {
            var handler = new SequenceHandler(Busy(429, "1"), Ok());
            var pauses = new List<TimeSpan>();

            var failure = await Fails(Provider(handler, new OllamaSettings { Retry = RetryPolicy.None }, pauses)
                .RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.RateLimited));
            Assert.That(handler.Bodies.Count, Is.EqualTo(1));
            Assert.That(failure.Retries, Is.Zero);
            Assert.That(failure.Message, Does.Contain("MaxAttempts is 1"));
        }

        [TestCase(HttpStatusCode.BadRequest, ProviderErrorKind.Rejected)]
        [TestCase(HttpStatusCode.Unauthorized, ProviderErrorKind.Rejected)]
        [TestCase(HttpStatusCode.Forbidden, ProviderErrorKind.Rejected)]
        [TestCase(HttpStatusCode.InternalServerError, ProviderErrorKind.ServerError)]
        [TestCase(HttpStatusCode.BadGateway, ProviderErrorKind.ServerError)]
        public async Task AnyOtherRefusalIsNeverRetried(HttpStatusCode status, ProviderErrorKind kind)
        {
            // A Retry-After on it changes nothing: only a busy answer is sent again.
            var handler = new SequenceHandler(Answer(status, "{\"error\":\"no\"}", "1"), Ok());
            var pauses = new List<TimeSpan>();

            var failure = await Fails(Provider(handler, new OllamaSettings(), pauses).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(kind));
            Assert.That(handler.Bodies.Count, Is.EqualTo(1), "Sent once.");
            Assert.That(pauses, Is.Empty);
            Assert.That(failure.Retries, Is.Zero);
        }

        [Test]
        public async Task TheCallersCancellationDuringAPauseEndsItAtOnce()
        {
            // The provider's own pause, a real Task.Delay, cancelled 0.1 s into 30 s.
            var handler = new SequenceHandler(Busy(503, "30"), Ok());
            var provider = new OllamaProvider(new OllamaSettings { Retry = new RetryPolicy { MaxAttempts = 3, MaxWaitSeconds = 60f } }, handler);
            var clock = Stopwatch.StartNew();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(100);
                try
                {
                    await provider.RequestAsync(Request(), cts.Token);
                    Assert.Fail("A cancelled request has no result.");
                }
                catch (OperationCanceledException e)
                {
                    Assert.That(e.CancellationToken, Is.EqualTo(cts.Token), "The caller's cancellation, never a provider failure.");
                }
            }

            Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(5d), "The pause stopped, rather than ran out.");
            Assert.That(handler.Bodies.Count, Is.EqualTo(1), "Nothing sent after the cancellation.");
        }

        [Test]
        public async Task AnAttemptThatTimesOutAfterARetryCarriesTheRetry()
        {
            var handler = new SequenceHandler(Busy(503, "0"), SequenceHandler.Hang);
            var pauses = new List<TimeSpan>();
            var settings = new OllamaSettings { TimeoutSeconds = 0.2f };

            var failure = await Fails(Provider(handler, settings, pauses).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.Timeout), "A timeout is never retried.");
            Assert.That(failure.Retries, Is.EqualTo(1));
            Assert.That(handler.Bodies.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task TheDeadlineCutsAnAttemptShort()
        {
            // Each attempt may wait 30 s, but the whole call 0.3 s.
            var handler = new SequenceHandler(SequenceHandler.Hang);
            var settings = new OllamaSettings { TimeoutSeconds = 30f, Retry = new RetryPolicy { DeadlineSeconds = 0.3f } };
            var clock = Stopwatch.StartNew();

            var failure = await Fails(Provider(handler, settings, new List<TimeSpan>()).RequestAsync(Request(), CancellationToken.None));

            Assert.That(failure.Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(failure.Message, Does.Contain("RetryPolicy.DeadlineSeconds"));
            Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(5d));
        }

        [Test]
        public async Task TheModelListIsRetriedToo()
        {
            var handler = new SequenceHandler(Busy(503, "2"),
                Answer(HttpStatusCode.OK, "{\"models\":[{\"name\":\"qwen3.5:4b\",\"size\":3400000000}]}"));
            var pauses = new List<TimeSpan>();

            var models = await NotCancelled(Provider(handler, new OllamaSettings(), pauses).ListModelsAsync(CancellationToken.None));

            Assert.That(models.Select(m => m.Name), Is.EqualTo(new[] { "qwen3.5:4b" }));
            Assert.That(pauses.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task AgentRecordsTheRetriesAndKeepsTheirWaitOutOfTheLatency()
        {
            var handler = new SequenceHandler(Busy(503, "1"), Ok());
            var provider = new OllamaProvider(new OllamaSettings(), handler)
            {
                // A real pause of 0.3 s, whatever was asked.
                Pause = (wait, ct) => Task.Delay(300, ct),
            };
            var agent = Fixtures.VillageGuard(provider);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision, Is.Not.Null);
            Assert.That(result.Telemetry.Retries, Is.EqualTo(1));
            Assert.That(result.Telemetry.RetryWaitSeconds, Is.GreaterThanOrEqualTo(0.29f));
            Assert.That(result.Telemetry.LatencySeconds, Is.LessThan(0.29f), "The pause is not the model's time.");

            var line = DecisionLogLine.From(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1));
            Assert.That(line.Retries, Is.EqualTo(1));
            Assert.That(line.RetryWaitSeconds, Is.EqualTo(result.Telemetry.RetryWaitSeconds));
        }

        [Test]
        public async Task AgentRecordsRetriesThatRanOutAsProviderRateLimited()
        {
            var handler = new SequenceHandler(Busy(503, "1"));
            var provider = Provider(handler, new OllamaSettings { Retry = new RetryPolicy { MaxAttempts = 2 } }, new List<TimeSpan>());
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. The provider failed\\. ProviderException: .*busy"));

            var result = await Fixtures.NotCancelled(Fixtures.VillageGuard(provider).DecideAsync("Go to the tower."));

            Assert.That(result.Decision, Is.Null);
            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderRateLimited));
            Assert.That(result.Telemetry.Retries, Is.EqualTo(1));
            Assert.That(PipelineErrorKind.ProviderRateLimited.IsProviderFault(), Is.True);
        }

        [Test]
        public void ADecisionLogLineLeavesRetriesOutWhenThereWereNone()
        {
            var line = new DecisionLogLine();
            string json = JsonConvert.SerializeObject(line, DecisionLogFormat.Settings);

            Assert.That(json, Does.Not.Contain("retries"));
        }

        [TestCase("12", 12f)]
        [TestCase(" 0 ", 0f)]
        [TestCase("soon", null)]
        [TestCase("-3", null)]
        [TestCase("", null)]
        public void RetryAfterTextIsSecondsOrNothing(string value, float? expected)
        {
            Assert.That(RetryPolicy.RetryAfterSeconds(value, DateTimeOffset.UtcNow), Is.EqualTo(expected));
        }

        [Test]
        public void RetryAfterTextCanBeAnHttpDate()
        {
            var now = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

            Assert.That(RetryPolicy.RetryAfterSeconds("Tue, 24 Nov 2026 12:00:09 GMT", now), Is.EqualTo(9f));
            Assert.That(RetryPolicy.RetryAfterSeconds("Tue, 24 Nov 2026 11:00:00 GMT", now), Is.EqualTo(0f), "A date already past is now.");
        }

        [Test]
        public void APolicyThatCannotWorkFailsWhenTheProviderIsBuilt()
        {
            var thrown = Assert.Throws<ArgumentException>(() =>
                new OllamaProvider(new OllamaSettings { Retry = new RetryPolicy { MaxAttempts = 0 } }, new SequenceHandler(Ok())));

            Assert.That(thrown.Message, Does.Contain("RetryPolicy.MaxAttempts"));
        }

        [Test]
        public void TheSharedPoliciesAreWhatTheProfilesNeed()
        {
            Assert.That(RetryPolicy.None.MaxAttempts, Is.EqualTo(1));
            Assert.That(RetryPolicy.FailFast.MaxWaitSeconds, Is.LessThanOrEqualTo(5f), "A player waits seconds, not minutes.");
            Assert.That(RetryPolicy.Patient.DeadlineSeconds, Is.EqualTo(600f), "Ten minutes for a shared, queued server.");
            Assert.That(RetryPolicy.IsBusy(503) && RetryPolicy.IsBusy(429), Is.True);
            Assert.That(RetryPolicy.IsBusy(500) || RetryPolicy.IsBusy(400) || RetryPolicy.IsBusy(502), Is.False);
        }

        // ---- helpers ----------------------------------------------------------------------------

        private static OllamaProvider Provider(SequenceHandler handler, OllamaSettings settings, List<TimeSpan> pauses)
        {
            return new OllamaProvider(settings, handler)
            {
                Pause = (wait, ct) =>
                {
                    pauses.Add(wait);
                    return Task.CompletedTask;
                },
            };
        }

        private static DecisionRequest Request()
        {
            return new DecisionRequest
            {
                SystemPrompt = "You are the guard.",
                Stimulus = "Player: \"Go to the tower.\"",
                Schema = new DecisionSchema(new[]
                {
                    new SchemaField { Name = "action", Description = DecisionSchema.ActionDescription, AllowedValues = new[] { "move_to", "none" }, Required = true },
                    new SchemaField { Name = "statement", Description = DecisionSchema.StatementDescription, Required = true },
                }),
            };
        }

        private static Func<CancellationToken, Task<HttpResponseMessage>> Ok()
        {
            string envelope = "{\"model\":\"qwen3.5:4b\",\"message\":{\"role\":\"assistant\",\"content\":"
                              + JsonConvert.ToString(Reply) + "},\"done\":true,\"prompt_eval_count\":143,\"eval_count\":21}";
            return Answer(HttpStatusCode.OK, envelope);
        }

        private static Func<CancellationToken, Task<HttpResponseMessage>> Busy(int status, string retryAfter = null,
                                                                               string body = "{\"error\":\"busy\"}",
                                                                               DateTimeOffset? date = null)
        {
            return ct =>
            {
                var response = Response((HttpStatusCode)status, body);
                if (retryAfter != null)
                {
                    response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
                }

                if (date.HasValue)
                {
                    response.Headers.RetryAfter = new RetryConditionHeaderValue(date.Value);
                }

                return Task.FromResult(response);
            };
        }

        private static Func<CancellationToken, Task<HttpResponseMessage>> Answer(HttpStatusCode status, string body, string retryAfter = null)
        {
            return ct =>
            {
                var response = Response(status, body);
                if (retryAfter != null)
                {
                    response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
                }

                return Task.FromResult(response);
            };
        }

        private static HttpResponseMessage Response(HttpStatusCode status, string body)
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, new UTF8Encoding(false)) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            return response;
        }

        private static async Task<T> NotCancelled<T>(Awaitable<T> pending)
        {
            try
            {
                return await pending;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"The request ended cancelled, though nothing cancelled it: {e.Message}");
                return default;
            }
        }

        private static async Task<ProviderException> Fails(Awaitable<ProviderResult> pending)
        {
            try
            {
                await pending;
            }
            catch (ProviderException e)
            {
                return e;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"The request ended cancelled instead of failing: {e.Message}");
            }

            Assert.Fail("The request succeeded instead of failing.");
            return null;
        }

        // Answers each send with the next scripted answer, the last one again once they run out.
        private sealed class SequenceHandler : HttpMessageHandler
        {
            private readonly Func<CancellationToken, Task<HttpResponseMessage>>[] answers;

            public readonly List<string> Bodies = new List<string>();

            public SequenceHandler(params Func<CancellationToken, Task<HttpResponseMessage>>[] answers)
            {
                this.answers = answers;
            }

            // Never answers; ends cancelled when the request's token fires.
            public static Task<HttpResponseMessage> Hang(CancellationToken ct)
            {
                var never = new TaskCompletionSource<HttpResponseMessage>();
                ct.Register(() => never.TrySetCanceled(ct));
                return never.Task;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Bodies.Add(request.Content == null ? "" : request.Content.ReadAsStringAsync().Result);
                var answer = answers[Math.Min(Bodies.Count - 1, answers.Length - 1)];
                return answer(ct);
            }
        }
    }
}
