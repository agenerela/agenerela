using System;

namespace LivingVale.Game
{
    /// <summary>
    /// Raises game-state events one subscriber at a time, so a broken view can neither stop the
    /// others from hearing the change nor interrupt the code that made it. Raise only after state
    /// is fully committed.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Where a subscriber's exception goes. The bootstrap points it at Unity's console; tests may capture it.</summary>
        public static Action<Exception> ReportSubscriberError { get; set; } = e => Console.Error.WriteLine(e);

        public static void Raise(Action handler)
        {
            if (handler == null)
            {
                return;
            }

            foreach (Action subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber();
                }
                catch (Exception e)
                {
                    ReportSubscriberError?.Invoke(e);
                }
            }
        }

        public static void Raise<T>(Action<T> handler, T value)
        {
            if (handler == null)
            {
                return;
            }

            foreach (Action<T> subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber(value);
                }
                catch (Exception e)
                {
                    ReportSubscriberError?.Invoke(e);
                }
            }
        }
    }
}
