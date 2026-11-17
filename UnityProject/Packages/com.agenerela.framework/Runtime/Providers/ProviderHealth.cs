using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// Whether a provider is answering, as the requests agents send it show (#190, G1): up, or down
    /// with the reason and a retry time. One per provider instance, like its queue, and found the
    /// same way: <c>ProviderHealth.For(agent.Provider)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What moves it.</b> <see cref="Agent"/> reports the outcome of every request it sends.
    /// Any reply that arrives marks the provider up, even one that cannot be read, because a model
    /// answered. A failure that is the provider's (<see cref="PipelineErrorKindExtensions.IsProviderFault"/>)
    /// marks it down, with that kind as <see cref="Reason"/>, the provider's own words as
    /// <see cref="Message"/>, and a retry time: <see cref="FirstRetrySeconds"/> after the first
    /// failure, doubling with each one after it, up to <see cref="LongestRetrySeconds"/>. A
    /// cancelled request reports nothing. Model lifecycle calls (<see cref="IModelLifecycle"/>) are
    /// not decisions and report nothing either.
    /// </para>
    /// <para>
    /// <b>What it changes</b> is nothing, until <see cref="BackOff"/> is switched on (DR-015, as
    /// amended). Then a <see cref="DecisionPriority.Background"/> request whose turn in the queue
    /// comes while the provider is down, before the retry time, is not sent: its decision ends with
    /// no result, as <see cref="PipelineErrorKind.ProviderDown"/>, and logs nothing, since the
    /// failure that put the provider down logged already. The first background request after the
    /// retry time is sent, and finds out. A <see cref="DecisionPriority.PlayerFacing"/> request is
    /// always sent: someone is waiting for its answer, or for its error.
    /// </para>
    /// <para>
    /// <b>Threads.</b> <see cref="Agent"/> reports on the main thread, so <see cref="Changed"/> is
    /// raised there, and a game reads the state there. Its members are safe to read from any
    /// thread, one at a time.
    /// </para>
    /// <para>
    /// Plain C# with no scene, like the queue. It is held weakly against its provider, so it goes
    /// when the provider does, and a new provider, such as one for another model (DR-019), starts
    /// up.
    /// </para>
    /// </remarks>
    public sealed class ProviderHealth
    {
        private static readonly ConditionalWeakTable<ILLMProvider, ProviderHealth> table =
            new ConditionalWeakTable<ILLMProvider, ProviderHealth>();

        // Guards the state below. Reports come from the main thread, reads from anywhere.
        private readonly object gate = new object();
        private bool isDown;
        private PipelineErrorKind? reason;
        private string message;
        private DateTime? downSince;
        private DateTime? retryAt;
        private int failures;
        private bool backOff;
        private float firstRetrySeconds = 5f;
        private float longestRetrySeconds = 60f;

        // The longest either retry setting may be, one day: long enough for any game, and short
        // enough that a retry time can always be written as a DateTime.
        private const float MostRetrySeconds = 86400f;

        // The time now, in UTC. Replaced by tests, so a retry time can pass without waiting for it.
        internal Func<DateTime> Clock = () => DateTime.UtcNow;

        internal ProviderHealth()
        {
        }

        /// <summary>The health of <paramref name="provider"/>, the same object every time.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
        public static ProviderHealth For(ILLMProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            // By the instance, never by Equals, as the queue is.
            return table.GetValue(provider, _ => new ProviderHealth());
        }

        /// <summary>
        /// Raised after every report that changes the state: the provider going down, coming back
        /// up, or failing again while down, which moves the retry time and may change the reason.
        /// <see cref="Agent"/> raises it on the main thread, once the decision that reported is back
        /// there, so a handler reads the state as it is when it runs. A handler that throws is
        /// logged as a warning and does not stop the decision that reported.
        /// </summary>
        public event Action<ProviderHealth> Changed;

        /// <summary>True from a provider failure until the next reply, or <see cref="Reset"/>.</summary>
        public bool IsDown
        {
            get
            {
                lock (gate)
                {
                    return isDown;
                }
            }
        }

        /// <summary>The kind of the last failure while down, such as <see cref="PipelineErrorKind.ProviderUnreachable"/>; null while up.</summary>
        public PipelineErrorKind? Reason
        {
            get
            {
                lock (gate)
                {
                    return reason;
                }
            }
        }

        /// <summary>
        /// The last failure's own words while down, such as "Nothing answered at
        /// http://127.0.0.1:11434. Is Ollama running there?"; null while up.
        /// </summary>
        public string Message
        {
            get
            {
                lock (gate)
                {
                    return message;
                }
            }
        }

        /// <summary>When the first of the failures in a row happened, in UTC; null while up.</summary>
        public DateTime? DownSinceUtc
        {
            get
            {
                lock (gate)
                {
                    return downSince;
                }
            }
        }

        /// <summary>
        /// When a background request may be sent again under <see cref="BackOff"/>, in UTC; null
        /// while up. Set whether or not <see cref="BackOff"/> is on, so a game can show it.
        /// </summary>
        public DateTime? RetryAtUtc
        {
            get
            {
                lock (gate)
                {
                    return retryAt;
                }
            }
        }

        /// <summary>Provider failures since the last reply; 0 while up.</summary>
        public int ConsecutiveFailures
        {
            get
            {
                lock (gate)
                {
                    return failures;
                }
            }
        }

        /// <summary>
        /// Whether background requests are held back while the provider is down, until the retry
        /// time. Off by default, so every request is sent, as before (DR-015, as amended). Applies
        /// to requests whose turn comes after it is set.
        /// </summary>
        public bool BackOff
        {
            get
            {
                lock (gate)
                {
                    return backOff;
                }
            }
            set
            {
                lock (gate)
                {
                    backOff = value;
                }
            }
        }

        /// <summary>
        /// Seconds from the first failure to the first retry, 5 by default. Each failure after it
        /// doubles the wait, and no wait is longer than <see cref="LongestRetrySeconds"/>, the first
        /// included. Applies from the next failure.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Not above 0, or above a day (86,400 s).</exception>
        public float FirstRetrySeconds
        {
            get
            {
                lock (gate)
                {
                    return firstRetrySeconds;
                }
            }
            set
            {
                // Not checked against LongestRetrySeconds, so the two can be set in either order.
                if (!(value > 0f) || value > MostRetrySeconds)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "FirstRetrySeconds must be above 0 and at most a day (86400).");
                }

                lock (gate)
                {
                    firstRetrySeconds = value;
                }
            }
        }

        /// <summary>The longest wait between retries, 60 s by default. Applies from the next failure.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Not above 0, or above a day (86,400 s).</exception>
        public float LongestRetrySeconds
        {
            get
            {
                lock (gate)
                {
                    return longestRetrySeconds;
                }
            }
            set
            {
                if (!(value > 0f) || value > MostRetrySeconds)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "LongestRetrySeconds must be above 0 and at most a day (86400).");
                }

                lock (gate)
                {
                    longestRetrySeconds = value;
                }
            }
        }

        /// <summary>
        /// Marks the provider up, as a reply would, such as when the player says the server is
        /// running again. Raises <see cref="Changed"/> if it was down.
        /// </summary>
        public void Reset()
        {
            ReportReply();
        }

        /// <summary>The state in one line, for a log or a HUD: "up", or "down (ProviderUnreachable) since ..., retry at ...".</summary>
        public override string ToString()
        {
            lock (gate)
            {
                if (!isDown)
                {
                    return "up";
                }

                return $"down ({reason}) since {downSince.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} UTC, "
                     + $"{failures} failure{(failures == 1 ? "" : "s")} in a row, retry at "
                     + $"{retryAt.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} UTC";
            }
        }

        // A reply arrived: the provider is up. Raises Changed if that changed anything.
        internal void ReportReply()
        {
            if (RecordReply())
            {
                RaiseChanged();
            }
        }

        // A request failed for a reason of the provider's. Raises Changed if that changed anything.
        internal void ReportFailure(PipelineErrorKind kind, string why)
        {
            if (RecordFailure(kind, why))
            {
                RaiseChanged();
            }
        }

        // The state half of ReportReply, safe on any thread. Agent records an outcome before it
        // frees the request's slot, so the request that slot goes to is judged by it, and raises
        // Changed afterwards, on the main thread. True when the state changed.
        internal bool RecordReply()
        {
            lock (gate)
            {
                if (!isDown)
                {
                    return false;
                }

                isDown = false;
                reason = null;
                message = null;
                downSince = null;
                retryAt = null;
                failures = 0;
                return true;
            }
        }

        // The state half of ReportFailure. Any kind that is not the provider's changes nothing, and
        // neither does ProviderDown, which is the back-off itself. True when the state changed.
        internal bool RecordFailure(PipelineErrorKind kind, string why)
        {
            if (!kind.IsProviderFault() || kind == PipelineErrorKind.ProviderDown)
            {
                return false;
            }

            lock (gate)
            {
                // Everything worked out first, so nothing below can throw half way through a change.
                var now = Clock();
                int count = failures < int.MaxValue ? failures + 1 : failures;

                // First * 2^(count - 1), capped, with the exponent kept small enough not to overflow.
                double wait = Math.Min(longestRetrySeconds, firstRetrySeconds * Math.Pow(2, Math.Min(count - 1, 30)));

                // At most a day, so only a clock within a day of DateTime.MaxValue could pass the end.
                var retry = wait >= (DateTime.MaxValue - now).TotalSeconds ? DateTime.MaxValue : now.AddSeconds(wait);

                if (!isDown)
                {
                    downSince = now;
                }

                failures = count;
                isDown = true;
                reason = kind;
                message = why ?? "";
                retryAt = retry;
                return true;
            }
        }

        // Whether a request of this priority, whose turn has come, is held back now. Says why when
        // it is, in words for the decision's ErrorMessage.
        internal bool HoldsBack(DecisionPriority priority, out string why)
        {
            lock (gate)
            {
                if (!backOff || !isDown || priority != DecisionPriority.Background)
                {
                    why = null;
                    return false;
                }

                var now = Clock();
                if (now >= retryAt.Value)
                {
                    why = null;
                    return false;
                }

                double left = (retryAt.Value - now).TotalSeconds;
                why = $"Its provider is down ({reason}), so background requests are not sent for another "
                    + $"{left.ToString("0.#", CultureInfo.InvariantCulture)} s. {message}";
                return true;
            }
        }

        // Each handler in turn, on the calling thread.
        internal void RaiseChanged()
        {
            var changed = Changed;
            if (changed == null)
            {
                return;
            }

            // One at a time, so a handler that throws costs neither the others nor the decision
            // that reported.
            foreach (var handler in changed.GetInvocationList())
            {
                try
                {
                    ((Action<ProviderHealth>)handler)(this);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"A ProviderHealth.Changed handler failed. {e.GetType().Name}: {e.Message}");
                }
            }
        }
    }
}
