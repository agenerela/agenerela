using System;
using LivingVale.Game;

namespace LivingVale.World
{
    /// <summary>
    /// Landmark discovery: the first time the player enters a place's radius it is discovered, worth
    /// <see cref="Xp"/> through <see cref="Progression"/> under the fixed event ID
    /// <c>discover_&lt;place id&gt;</c>. The progression's awarded events are the only record, so a
    /// discovery is saved and loaded with the XP it paid, and can never pay twice. Briar Glen is where
    /// the player starts, so it is known from the start and pays nothing.
    /// </summary>
    /// <remarks>Plain C#, no scene. A Unity component calls <see cref="Visit"/> with the player's position.</remarks>
    public sealed class PlaceDiscovery
    {
        /// <summary>Per landmark (content list, XP budget).</summary>
        public const int Xp = 10;

        public const string EventPrefix = "discover_";

        private readonly Progression progression;

        public PlaceDiscovery(Progression progression)
        {
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
        }

        /// <summary>The place the player was in at the last <see cref="Visit"/>, or null between places.</summary>
        public ValePlace Current { get; private set; }

        /// <summary>Raised once per place, when its discovery is first awarded.</summary>
        public event Action<ValePlace> Discovered;

        public static string EventIdFor(string placeId) => EventPrefix + placeId;

        /// <summary>Whether entering this place is a discovery at all (every place but the starting village).</summary>
        public static bool IsLandmark(string placeId) => placeId != ValePlaces.BriarGlen;

        public bool IsDiscovered(string placeId) =>
            !IsLandmark(placeId) || progression.HasAwarded(EventIdFor(placeId));

        /// <summary>
        /// Records where the player is. Returns the place if this visit discovered it, otherwise null.
        /// </summary>
        public ValePlace Visit(float x, float z)
        {
            Current = ValePlaces.At(x, z);
            if (Current == null || IsDiscovered(Current.Id))
            {
                return null;
            }

            if (progression.TryAward(EventIdFor(Current.Id), Xp) != ProgressionRejection.None)
            {
                return null;
            }

            GameEvents.Raise(Discovered, Current);
            return Current;
        }
    }
}
