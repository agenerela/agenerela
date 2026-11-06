using System;
using System.Collections.Generic;

namespace LivingVale.Game
{
    /// <summary>The three level-up perks (design §6). Names and effects are provisional.</summary>
    public enum Perk
    {
        /// <summary>More survivability.</summary>
        Hardy,

        /// <summary>Reduced dodge stamina cost.</summary>
        Skirmisher,

        /// <summary>Unlocks one bounded bargaining or favor condition; never a promise that the model is more persuadable.</summary>
        TrustedFace
    }

    public enum ProgressionRejection
    {
        None,

        /// <summary>The event ID was already awarded; nothing was given twice.</summary>
        DuplicateEvent,

        InvalidAmount,

        /// <summary>No level-up is waiting for a perk choice.</summary>
        NoPerkChoice,

        /// <summary>That perk was already chosen.</summary>
        AlreadyChosen
    }

    /// <summary>
    /// XP, levels and perks (design §6). XP comes only from recorded game events, each with a unique
    /// event ID applied once, in the same style as <see cref="PlayerInventory.TryGiveLoot"/>: replayed
    /// dialogue, a repeated reward or a reloaded save cannot award it twice. Levels 1–3 at cumulative
    /// thresholds 100 and 250; each level-up grants one perk choice, made whenever the player likes.
    /// </summary>
    /// <remarks>Plain C#, no scene. Main thread only.</remarks>
    public sealed class Progression
    {
        public const int MaxLevel = 3;

        /// <summary>Health added per level above 1 ("a small health increase", design §6). Provisional.</summary>
        public const int HealthPerLevel = 10;

        /// <summary>Cumulative XP needed for each level, indexed by level − 1.</summary>
        public static readonly IReadOnlyList<int> Thresholds = new[] { 0, 100, 250 };

        private readonly HashSet<string> awarded = new HashSet<string>();
        private readonly List<Perk> perks = new List<Perk>();

        public int Xp { get; private set; }

        public int Level => LevelFor(Xp);

        public IReadOnlyList<Perk> Perks => perks;

        /// <summary>Level-ups whose perk has not been chosen yet.</summary>
        public int PendingPerkChoices => Level - 1 - perks.Count;

        public int MaxHealthBonus => (Level - 1) * HealthPerLevel;

        /// <summary>Event IDs already awarded, for the save.</summary>
        public IReadOnlyCollection<string> AwardedEvents => awarded;

        /// <summary>XP still needed for the next level, or 0 at the cap.</summary>
        public int XpToNextLevel => Level >= MaxLevel ? 0 : Thresholds[Level] - Xp;

        /// <summary>Raised after every committed change.</summary>
        public event Action Changed;

        /// <summary>Raised with the new level when an award crosses a threshold, after <see cref="Changed"/>.</summary>
        public event Action<int> LeveledUp;

        public static int LevelFor(int xp)
        {
            int level = 1;
            for (int i = 1; i < Thresholds.Count; i++)
            {
                if (xp >= Thresholds[i])
                {
                    level = i + 1;
                }
            }

            return level;
        }

        public bool HasAwarded(string eventId) => eventId != null && awarded.Contains(eventId);

        public bool Has(Perk perk) => perks.Contains(perk);

        /// <summary>Awards XP for one recorded event. A repeated ID is refused and changes nothing.</summary>
        public ProgressionRejection TryAward(string eventId, int amount)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new ArgumentException("XP needs a unique event ID.", nameof(eventId));
            }

            if (awarded.Contains(eventId))
            {
                return ProgressionRejection.DuplicateEvent;
            }

            if (amount <= 0 || (long)Xp + amount > int.MaxValue)
            {
                return ProgressionRejection.InvalidAmount;
            }

            int before = Level;
            awarded.Add(eventId);
            Xp += amount;
            GameEvents.Raise(Changed);
            for (int level = before + 1; level <= Level; level++)
            {
                GameEvents.Raise(LeveledUp, level);
            }

            return ProgressionRejection.None;
        }

        public ProgressionRejection TryChoosePerk(Perk perk)
        {
            if (!Enum.IsDefined(typeof(Perk), perk))
            {
                return ProgressionRejection.InvalidAmount;
            }

            if (PendingPerkChoices <= 0)
            {
                return ProgressionRejection.NoPerkChoice;
            }

            if (perks.Contains(perk))
            {
                return ProgressionRejection.AlreadyChosen;
            }

            perks.Add(perk);
            GameEvents.Raise(Changed);
            return ProgressionRejection.None;
        }

        /// <summary>
        /// Whether a saved state is one this class could have reached: XP not negative, perks known,
        /// distinct and no more than the level allows, event IDs non-empty.
        /// </summary>
        public static bool IsValidState(int xp, IReadOnlyList<Perk> savedPerks, IEnumerable<string> events)
        {
            if (xp < 0 || savedPerks == null || events == null || savedPerks.Count > LevelFor(xp) - 1)
            {
                return false;
            }

            var seen = new HashSet<Perk>();
            foreach (var perk in savedPerks)
            {
                if (!Enum.IsDefined(typeof(Perk), perk) || !seen.Add(perk))
                {
                    return false;
                }
            }

            foreach (var id in events)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Replaces the whole state with a validated save. Call <see cref="IsValidState"/> first.</summary>
        internal void Restore(int xp, IReadOnlyList<Perk> savedPerks, IEnumerable<string> events)
        {
            if (!IsValidState(xp, savedPerks, events))
            {
                throw new ArgumentException("Invalid progression state; validate before restoring.");
            }

            Xp = xp;
            perks.Clear();
            perks.AddRange(savedPerks);
            awarded.Clear();
            awarded.UnionWith(events);
            GameEvents.Raise(Changed);
        }
    }
}
