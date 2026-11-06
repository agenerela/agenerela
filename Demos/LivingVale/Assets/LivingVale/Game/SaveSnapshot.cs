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
        /// <summary>2 adds <see cref="npcMemories"/> (G5h). A version 1 save still loads, with every NPC's memory cleared.</summary>
        public const int CurrentVersion = 2;

        /// <summary>The oldest version a load accepts.</summary>
        public const int OldestVersion = 1;

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

        /// <summary>What each NPC remembers of its conversations, from version 2; null in a version 1 save.</summary>
        public List<SavedNpcMemory> npcMemories;
    }

    /// <summary>
    /// One NPC's conversation memory, as plain data: the framework's memory snapshot (DR-018) under
    /// the NPC's stable id. Game code reads no framework type; the archive that fills it does.
    /// </summary>
    [Serializable]
    public sealed class SavedNpcMemory
    {
        public string npcId;
        public string format;
        public int version;
        public List<SavedExchange> exchanges;
        public string data;
    }

    /// <summary>One remembered exchange: what the NPC was told, and the decision it made, as one line of JSON.</summary>
    [Serializable]
    public sealed class SavedExchange
    {
        public string stimulus;
        public string decision;
    }

    /// <summary>
    /// Where NPC memories come from and go to on save and load. <see cref="SaveGame"/> calls it, so a
    /// load stays atomic: <see cref="Validate"/> on the detached data first, <see cref="Restore"/>
    /// only after every other part of the load has succeeded.
    /// </summary>
    public interface INpcMemoryArchive
    {
        /// <summary>Every NPC's memory now, in a stable order. Never null.</summary>
        List<SavedNpcMemory> Capture();

        /// <summary>Why these memories cannot be restored, or null. Null memories (a version 1 save) are valid and mean "none".</summary>
        string Validate(List<SavedNpcMemory> memories);

        /// <summary>Replaces every NPC's memory with these; an NPC not listed, or every NPC for null, is left remembering nothing.</summary>
        void Restore(List<SavedNpcMemory> memories);
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
