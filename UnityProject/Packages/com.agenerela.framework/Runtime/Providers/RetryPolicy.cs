using System;
using System.Globalization;
using System.Net.Http.Headers;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// When a provider sends a request again after the server said it is busy, and how long it
    /// waits in all: the attempts, the pauses between them, and a deadline for the whole call
    /// (DR-026). A plain serializable class, so <see cref="OllamaSettings.Retry"/> carries it in
    /// code, <see cref="OllamaProviderConfig"/> shows it in the Inspector, and another provider
    /// reuses it as it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is retried.</b> Only an answer that says "busy, try later": HTTP 429 (too many
    /// requests) and HTTP 503 (unavailable), the two <see cref="IsBusy"/> names. A queueing gateway
    /// in front of a shared card, such as the team's home server, answers 503 with
    /// <c>Retry-After</c> when a request waited its turn too long. Never another 4xx, which sending
    /// again cannot change; never another 5xx; never a timeout, a refused connection or the
    /// caller's cancellation. A decision request is safe to send twice: it changes nothing on the
    /// server, so the second answer is as good as the first.
    /// </para>
    /// <para>
    /// <b>How long between attempts.</b> What the server's <c>Retry-After</c> says, seconds or an
    /// HTTP date (<see cref="RetryAfterSeconds"/>). Without one, an exponential back-off from
    /// <see cref="BackoffSeconds"/>, doubling each time up to <see cref="MaxBackoffSeconds"/>, with
    /// jitter (<see cref="BackoffFor"/>), so clients turned away together do not all come back in the
    /// same instant.
    /// </para>
    /// <para>
    /// <b>When it gives up.</b> After <see cref="MaxAttempts"/> sends; or when the next pause would
    /// take the pauses past <see cref="MaxWaitSeconds"/>; or when it would end past
    /// <see cref="DeadlineSeconds"/> from the first send. The provider then throws
    /// <see cref="ProviderErrorKind.RateLimited"/>, at once rather than after a pause it knows is
    /// too long. Each attempt still has its own timeout, the provider's (for Ollama,
    /// <see cref="OllamaSettings.TimeoutSeconds"/>), cut short to what the deadline leaves.
    /// </para>
    /// <para>
    /// The time before the attempt that answered, failed attempts and pauses together, is
    /// recorded as <see cref="ProviderResult.RetryWaitSeconds"/> and kept out of
    /// <see cref="DecisionTelemetry.LatencySeconds"/>, so a request that waited for a busy server
    /// is never read as a slow model.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class RetryPolicy
    {
        /// <summary>
        /// The most sends of one request, the first included: 1 never retries. 3 unless set.
        /// </summary>
        [Min(1)]
        [Tooltip("The most times one request is sent when the server answers busy (HTTP 429 or 503), the first " +
                 "included. 1 never retries.")]
        public int MaxAttempts = 3;

        /// <summary>
        /// The most seconds spent pausing between attempts, all pauses together. A pause that would
        /// pass it is not taken: the request fails as <see cref="ProviderErrorKind.RateLimited"/>.
        /// 10 unless set.
        /// </summary>
        [Min(0f)]
        [Tooltip("The most seconds spent pausing between attempts, all together. A Retry-After that would pass " +
                 "it ends the request at once, as RateLimited.")]
        public float MaxWaitSeconds = 10f;

        /// <summary>
        /// The most seconds the whole call may take, from the first send: every attempt and every
        /// pause. 0, the default, sets none, so only <see cref="MaxAttempts"/>,
        /// <see cref="MaxWaitSeconds"/> and each attempt's own timeout bound it.
        /// </summary>
        [Min(0f)]
        [Tooltip("The most seconds the whole call may take, every attempt and pause included, counted from the " +
                 "first send. 0 sets none. Each attempt's own timeout is cut short to what is left.")]
        public float DeadlineSeconds = 0f;

        /// <summary>
        /// The first pause, in seconds, when the server gave no <c>Retry-After</c>; each later one
        /// doubles, up to <see cref="MaxBackoffSeconds"/>. 0.5 unless set.
        /// </summary>
        [Min(0f)]
        [Tooltip("The first pause when the server says no Retry-After. Each later one doubles, with jitter.")]
        public float BackoffSeconds = 0.5f;

        /// <summary>The longest one back-off pause may be, before jitter. 8 unless set.</summary>
        [Min(0f)]
        [Tooltip("The longest one back-off pause may be.")]
        public float MaxBackoffSeconds = 8f;

        /// <summary>A policy that never retries: one send, as every provider did before DR-026.</summary>
        public static RetryPolicy None => new RetryPolicy { MaxAttempts = 1, MaxWaitSeconds = 0f };

        /// <summary>
        /// For a shipped game: one more try within 3 s, then the game's own fallback runs.
        /// </summary>
        public static RetryPolicy FailFast => new RetryPolicy
        {
            MaxAttempts = 2, MaxWaitSeconds = 3f, BackoffSeconds = 0.5f, MaxBackoffSeconds = 2f,
        };

        /// <summary>
        /// For development against a shared, queueing server: up to 30 sends within ten minutes,
        /// pauses of up to 30 s. Agents running tests and a developer at the keyboard wait their
        /// turn rather than fail.
        /// </summary>
        public static RetryPolicy Patient => new RetryPolicy
        {
            MaxAttempts = 30, MaxWaitSeconds = 600f, DeadlineSeconds = 600f, BackoffSeconds = 1f, MaxBackoffSeconds = 30f,
        };

        /// <summary>A copy, so a provider keeps the values it was built with.</summary>
        public RetryPolicy Copy()
        {
            return (RetryPolicy)MemberwiseClone();
        }

        /// <summary>
        /// True for the answers this policy retries: HTTP 429 and HTTP 503, a server saying it is
        /// busy. Every other status is final.
        /// </summary>
        public static bool IsBusy(int status)
        {
            return status == 429 || status == 503;
        }

        /// <summary>
        /// The pause before retry number <paramref name="retry"/> (1 for the first retry) when the
        /// server gave no <c>Retry-After</c>: <see cref="BackoffSeconds"/> doubled each retry, at most
        /// <see cref="MaxBackoffSeconds"/>, then jittered to between half and all of that
        /// ("equal jitter"), from <paramref name="unit"/>, a number from 0 to 1.
        /// </summary>
        public float BackoffFor(int retry, double unit)
        {
            double full = BackoffSeconds * Math.Pow(2, Math.Max(0, Math.Min(retry - 1, 30)));
            full = Math.Min(full, MaxBackoffSeconds);
            unit = Math.Max(0d, Math.Min(1d, unit));
            return (float)(full / 2d + full / 2d * unit);
        }

        /// <summary>
        /// The seconds a <c>Retry-After</c> header asks for: its delay, or the time from
        /// <paramref name="now"/> until its HTTP date, 0 for a date already past. Null when there
        /// is no header or it says neither.
        /// </summary>
        public static float? RetryAfterSeconds(RetryConditionHeaderValue header, DateTimeOffset now)
        {
            if (header == null)
            {
                return null;
            }

            if (header.Delta.HasValue)
            {
                return (float)Math.Max(0d, header.Delta.Value.TotalSeconds);
            }

            if (header.Date.HasValue)
            {
                return (float)Math.Max(0d, (header.Date.Value - now).TotalSeconds);
            }

            return null;
        }

        /// <summary>
        /// The same for the header's raw text, for a transport that does not parse it: whole
        /// seconds, or an HTTP date. Null when it is neither.
        /// </summary>
        public static float? RetryAfterSeconds(string value, DateTimeOffset now)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0)
            {
                return null;
            }

            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
            {
                return seconds;
            }

            if (DateTimeOffset.TryParseExact(value, "r", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var date))
            {
                return (float)Math.Max(0d, (date - now).TotalSeconds);
            }

            return null;
        }

        /// <summary>What is wrong with the values, or null when they are usable.</summary>
        internal string Problem()
        {
            if (MaxAttempts < 1)
            {
                return $"RetryPolicy.MaxAttempts is {MaxAttempts}. It must be 1 or more; 1 never retries.";
            }

            if (!Finite(MaxWaitSeconds))
            {
                return $"RetryPolicy.MaxWaitSeconds is {MaxWaitSeconds}. It must be 0 or more.";
            }

            if (!Finite(DeadlineSeconds) || DeadlineSeconds > int.MaxValue / 1000f)
            {
                return $"RetryPolicy.DeadlineSeconds is {DeadlineSeconds}. It must be 0, for none, or more.";
            }

            if (!Finite(BackoffSeconds) || !Finite(MaxBackoffSeconds))
            {
                return "RetryPolicy.BackoffSeconds and MaxBackoffSeconds must be 0 or more.";
            }

            return null;
        }

        private static bool Finite(float value)
        {
            return value >= 0f && !float.IsInfinity(value);
        }
    }
}
