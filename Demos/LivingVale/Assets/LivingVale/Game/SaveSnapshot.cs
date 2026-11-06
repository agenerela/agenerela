using System;
using System.Collections.Generic;

namespace LivingVale.Game
{
    /// <summary>
    /// Versioned, durable game state only: cards and conversations belong to the session that made
    /// them. Public fields and lists let JsonUtility serialize it without depending on live services.
    /// </summary>
    [Serializable]
    public sealed class SaveSnapshot
    {
        public const int CurrentVersion = 1;

        // Leave fields uninitialized so a missing version or collection cannot become a valid save.
        public int version;
        public int gold;
        public List<SavedItem> slots;
        public List<SavedItem> questItems;
        public string weapon;
        public string armor;
        public List<string> quickUse;
        public List<string> appliedEvents;
        public List<SavedVendor> vendors;
        public string mainState;
        public string route;
        public string disposition;
        public string herbsState;
        public string badgeState;
        public List<string> clues;
        public int xp;
        public List<string> perks;
        public List<string> awardedEvents;
    }

    /// <summary>A stack or protected holding by stable ID, preserving even empty slot positions.</summary>
    [Serializable]
    public sealed class SavedItem
    {
        public string id;
        public int count;
    }

    /// <summary>Stock belongs to its vendor, so entry IDs cannot restore a different shop's goods.</summary>
    [Serializable]
    public sealed class SavedVendor
    {
        public string id;
        public List<SavedStock> entries;
    }

    /// <summary>Only remaining units persist; prices and bundle definitions remain authored content.</summary>
    [Serializable]
    public sealed class SavedStock
    {
        public string id;
        public int remaining;
    }
}
