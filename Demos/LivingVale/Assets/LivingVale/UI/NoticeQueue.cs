using System.Collections.Generic;

namespace LivingVale.UI
{
    /// <summary>One line in the notice area. A keyed notice replaces the last one with its key.</summary>
    public sealed class Notice
    {
        public string Key { get; }
        public string Text { get; }

        /// <summary>Seconds on screen once shown; 0 or less stays until replaced or cleared.</summary>
        public float Seconds { get; }

        /// <summary>When it went on screen, or −1 while it waits for a slot.</summary>
        public float ShownAt { get; internal set; } = -1f;

        public Notice(string key, string text, float seconds)
        {
            Key = key;
            Text = text;
            Seconds = seconds;
        }

        public bool Sticky => Seconds <= 0f;

        /// <summary>Seconds left on screen, for the fade; infinite while sticky or waiting.</summary>
        public float Left(float now) => Sticky || ShownAt < 0f ? float.PositiveInfinity : ShownAt + Seconds - now;
    }

    /// <summary>
    /// G8a: the one notice area under the compass. At most <see cref="MaxVisible"/> notices show,
    /// oldest on top; more wait their turn in arrival order, and a waiting notice's time starts only
    /// when it shows, so a burst (a discovery, its XP and a quest receipt) is read rather than lost.
    /// A keyed notice ("the model is loading") updates in place instead of stacking. Plain C#: the
    /// caller passes the clock.
    /// </summary>
    public sealed class NoticeQueue
    {
        private readonly List<Notice> visible = new List<Notice>();
        private readonly List<Notice> waiting = new List<Notice>();

        public NoticeQueue(int maxVisible = 3)
        {
            MaxVisible = maxVisible < 1 ? 1 : maxVisible;
        }

        public int MaxVisible { get; }

        /// <summary>What shows now, oldest first.</summary>
        public IReadOnlyList<Notice> Visible => visible;

        public int WaitingCount => waiting.Count;

        /// <summary>Adds a notice, or replaces the one with the same non-null key in place.</summary>
        public void Push(string text, float seconds, float now, string key = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (key != null)
                {
                    Clear(key, now);
                }

                return;
            }

            var notice = new Notice(key, text, seconds);
            if (key != null)
            {
                int shown = visible.FindIndex(n => n.Key == key);
                if (shown >= 0)
                {
                    notice.ShownAt = now;
                    visible[shown] = notice;
                    return;
                }

                int queued = waiting.FindIndex(n => n.Key == key);
                if (queued >= 0)
                {
                    waiting[queued] = notice;
                    Tick(now);
                    return;
                }
            }

            waiting.Add(notice);
            Tick(now);
        }

        /// <summary>Removes the notice with this key, shown or waiting.</summary>
        public void Clear(string key, float now)
        {
            visible.RemoveAll(n => n.Key == key);
            waiting.RemoveAll(n => n.Key == key);
            Tick(now);
        }

        /// <summary>Drops expired notices and shows waiting ones in the freed slots.</summary>
        public void Tick(float now)
        {
            visible.RemoveAll(n => n.Left(now) <= 0f);
            while (visible.Count < MaxVisible && waiting.Count > 0)
            {
                Notice next = waiting[0];
                waiting.RemoveAt(0);
                next.ShownAt = now;
                visible.Add(next);
            }
        }
    }
}
