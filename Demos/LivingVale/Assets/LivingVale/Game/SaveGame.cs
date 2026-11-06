using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using UnityEngine;

namespace LivingVale.Game
{
    /// <summary>
    /// Saves durable state and validates every service before replacing any of it. A bad file must
    /// not cost the player their current progress, and loading must never revive a pending card.
    /// </summary>
    /// <remarks>Plain C#, no scene. Main thread only, including capture and JSON operations.</remarks>
    public sealed class SaveGame
    {
        private readonly PlayerInventory inventory;
        private readonly TradeService trade;
        private readonly QuestLog quests;
        private readonly Progression progression;
        private readonly string directory;

        /// <summary>Uses one shared set of services so a load cannot restore an unrelated inventory or XP ledger.</summary>
        public SaveGame(PlayerInventory inventory, TradeService trade, QuestLog quests, Progression progression,
            string directory)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.trade = trade ?? throw new ArgumentNullException(nameof(trade));
            this.quests = quests ?? throw new ArgumentNullException(nameof(quests));
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
            if (trade.Inventory != inventory || quests.Inventory != inventory || quests.Progression != progression)
            {
                throw new ArgumentException("Save services must share their inventory and progression.");
            }

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A save needs a directory.", nameof(directory));
            }

            this.directory = Path.GetFullPath(directory);
        }

        /// <summary>The NPCs' conversation memories, saved and loaded with the rest (G5h); null saves none and leaves them alone on load.</summary>
        public INpcMemoryArchive Memories { get; set; }

        public static string DefaultDirectory => Application.persistentDataPath;
        public string SavePath => Path.Combine(directory, "livingvale_save.json");
        public string BackupPath => Path.Combine(directory, "livingvale_save.bak.json");

        /// <summary>Copies holdings and event ledgers, so later gameplay cannot change the captured save.</summary>
        public SaveSnapshot Capture()
        {
            var snapshot = new SaveSnapshot
            {
                version = SaveSnapshot.CurrentVersion,
                gold = inventory.Gold,
                slots = inventory.Slots.Select(s => new SavedItem { id = s.ItemId, count = s.Quantity }).ToList(),
                questItems = inventory.QuestItems.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => new SavedItem { id = p.Key, count = p.Value }).ToList(),
                weapon = inventory.Weapon,
                armor = inventory.Armor,
                quickUse = inventory.QuickUse.ToList(),
                appliedEvents = inventory.AppliedEvents.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                vendors = new List<SavedVendor>(),
                mainState = quests.MainState.ToString(),
                route = quests.Route.ToString(),
                disposition = quests.Disposition.ToString(),
                herbsState = quests.HerbsState.ToString(),
                badgeState = quests.BadgeState.ToString(),
                clues = quests.Clues.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                xp = progression.Xp,
                perks = progression.Perks.Select(PerkId).ToList(),
                awardedEvents = progression.AwardedEvents.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                npcMemories = Memories?.Capture() ?? new List<SavedNpcMemory>()
            };

            foreach (var vendor in trade.Vendors.OrderBy(v => v.VendorId, StringComparer.Ordinal))
            {
                snapshot.vendors.Add(new SavedVendor
                {
                    id = vendor.VendorId,
                    entries = vendor.Selling.Select(e => new SavedStock { id = e.Id, remaining = e.Remaining }).ToList()
                });
            }

            return snapshot;
        }

        /// <summary>Serializes the data alone, so no quote, offer or conversation object can leak into a save.</summary>
        public string ToJson(SaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return JsonUtility.ToJson(snapshot, true);
        }

        /// <summary>Reads data without applying it; callers can inspect it or ask <see cref="Validate"/> why it is unsafe.</summary>
        public bool TryParse(string json, out SaveSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            string trimmed = json?.Trim();
            if (!IsCompleteObject(trimmed))
            {
                error = "Expected a complete save JSON object.";
                return false;
            }

            try
            {
                var parsed = JsonUtility.FromJson<SaveSnapshot>(trimmed);
                if (parsed == null)
                {
                    error = "Missing save data.";
                    return false;
                }

                // JsonUtility writes null string fields as empty strings. Only nullable item IDs
                // have that meaning; enum names and event IDs must still be present and valid.
                parsed.weapon = NullIfEmpty(parsed.weapon);
                parsed.armor = NullIfEmpty(parsed.armor);
                if (parsed.quickUse != null)
                {
                    for (int i = 0; i < parsed.quickUse.Count; i++)
                    {
                        parsed.quickUse[i] = NullIfEmpty(parsed.quickUse[i]);
                    }
                }

                if (parsed.slots != null)
                {
                    foreach (var slot in parsed.slots)
                    {
                        if (slot != null)
                        {
                            slot.id = NullIfEmpty(slot.id);
                        }
                    }
                }

                snapshot = parsed;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = "Invalid save JSON: " + exception.Message;
                return false;
            }
        }

        /// <summary>Checks all services on detached data; even pending cards and revisions stay untouched.</summary>
        public string Validate(SaveSnapshot snapshot) => Prepare(snapshot, out _);

        /// <summary>
        /// Restores only after every check passes. Pending cards are invalidated before holdings move,
        /// so a pre-load button can never spend restored gold or accept an old offer.
        /// </summary>
        public bool TryApply(SaveSnapshot snapshot, out string error)
        {
            error = Prepare(snapshot, out var state);
            if (error != null)
            {
                return false;
            }

            trade.InvalidatePending();
            quests.InvalidatePending();
            using (inventory.DeferNotifications())
            {
                if (!inventory.TryRestore(state.Gold, state.Slots, state.QuestItems, state.Weapon, state.Armor,
                    state.QuickUse, state.AppliedEvents, out error))
                {
                    return false;
                }

                foreach (var vendor in state.Vendors)
                {
                    if (!vendor.Vendor.TryRestoreRemaining(vendor.Remaining, out error))
                    {
                        return false;
                    }
                }

                if (!quests.TryRestore(state.Main, state.Route, state.Disposition, state.Herbs, state.Badge,
                    state.Clues, out error))
                {
                    return false;
                }

                progression.Restore(state.Xp, state.Perks, state.AwardedEvents);
            }

            // Last, after everything else restored, and already validated in Prepare: a load that
            // failed above leaves every NPC's memory as it was.
            Memories?.Restore(state.Memories);
            return true;
        }

        /// <summary>Flushes a complete temporary file before replacing the save, retaining the previous file as backup.</summary>
        public bool TrySave(out string error)
        {
            var snapshot = Capture();
            error = Validate(snapshot);
            if (error != null)
            {
                return false;
            }

            try
            {
                string json = ToJson(snapshot);
                Directory.CreateDirectory(directory);
                string temporary = SavePath + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                    {
                        writer.Write(json);
                        writer.Flush();
                    }

                    stream.Flush(true);
                }

                if (File.Exists(SavePath))
                {
                    File.Replace(temporary, SavePath, BackupPath);
                }
                else
                {
                    File.Move(temporary, SavePath);
                }

                return true;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                error = "Could not save: " + exception.Message;
                return false;
            }
        }

        /// <summary>Loads the primary file explicitly; a failure preserves current progress rather than silently rolling it back.</summary>
        public bool TryLoad(out string error) => TryLoadFile(SavePath, out error);

        /// <summary>Lets the player deliberately recover the previous save when the primary file is unusable.</summary>
        public bool TryLoadBackup(out string error) => TryLoadFile(BackupPath, out error);

        private bool TryLoadFile(string path, out string error)
        {
            try
            {
                if (!TryParse(File.ReadAllText(path), out var snapshot, out error))
                {
                    return false;
                }

                return TryApply(snapshot, out error);
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                error = "Could not load: " + exception.Message;
                return false;
            }
        }

        private string Prepare(SaveSnapshot snapshot, out RestoreState state)
        {
            state = null;
            if (snapshot == null)
            {
                return "Missing save data.";
            }

            if (snapshot.version < SaveSnapshot.OldestVersion || snapshot.version > SaveSnapshot.CurrentVersion)
            {
                return $"Unsupported save version {snapshot.version}.";
            }

            // Version 2 must carry its memories, even an empty list; version 1 has none to carry.
            if (snapshot.version >= 2 && snapshot.npcMemories == null)
            {
                return "Missing save collection.";
            }

            var memories = snapshot.version >= 2 ? snapshot.npcMemories : null;
            string memoryError = Memories?.Validate(memories);
            if (memoryError != null)
            {
                return memoryError;
            }

            if (snapshot.slots == null || snapshot.questItems == null || snapshot.quickUse == null ||
                snapshot.appliedEvents == null || snapshot.vendors == null || snapshot.clues == null ||
                snapshot.perks == null || snapshot.awardedEvents == null)
            {
                return "Missing save collection.";
            }

            var ready = new RestoreState
            {
                Gold = snapshot.gold,
                Slots = new ItemStack[snapshot.slots.Count],
                QuestItems = new Dictionary<string, int>(StringComparer.Ordinal),
                Weapon = snapshot.weapon,
                Armor = snapshot.armor,
                QuickUse = snapshot.quickUse.ToArray(),
                AppliedEvents = snapshot.appliedEvents.ToArray(),
                Vendors = new List<VendorRestore>(),
                Clues = snapshot.clues.ToArray(),
                Xp = snapshot.xp,
                Perks = new List<Perk>(),
                AwardedEvents = snapshot.awardedEvents.ToArray(),
                Memories = memories
            };

            for (int i = 0; i < snapshot.slots.Count; i++)
            {
                var slot = snapshot.slots[i];
                // ItemStack normalizes bad quantities into empty slots; reject before converting.
                if (slot == null || slot.count < 0 || (slot.id == null) != (slot.count == 0))
                {
                    return $"Invalid inventory slot {i}.";
                }

                ready.Slots[i] = new ItemStack(slot.id, slot.count);
            }

            foreach (var item in snapshot.questItems)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id) || ready.QuestItems.ContainsKey(item.id))
                {
                    return "Missing or duplicate quest item ID.";
                }

                ready.QuestItems.Add(item.id, item.count);
            }

            string error = inventory.ValidateRestore(ready.Gold, ready.Slots, ready.QuestItems, ready.Weapon,
                ready.Armor, ready.QuickUse, ready.AppliedEvents);
            if (error != null)
            {
                return error;
            }

            var vendorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in snapshot.vendors)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.id) || saved.entries == null || !vendorIds.Add(saved.id))
                {
                    return "Missing or duplicate vendor data.";
                }

                var vendor = trade.VendorFor(saved.id);
                if (vendor == null)
                {
                    return $"Unknown vendor {saved.id}.";
                }

                var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var entry in saved.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.id) || remaining.ContainsKey(entry.id))
                    {
                        return $"{saved.id}: missing or duplicate stock entry.";
                    }

                    remaining.Add(entry.id, entry.remaining);
                }

                error = vendor.ValidateRemaining(remaining);
                if (error != null)
                {
                    return error;
                }

                // Every entry, or the load would keep this session's stock for the missing ones.
                if (remaining.Count != vendor.Selling.Count)
                {
                    return $"{saved.id}: the save is missing stock entries.";
                }

                ready.Vendors.Add(new VendorRestore { Vendor = vendor, Remaining = remaining });
            }

            foreach (var vendor in trade.Vendors)
            {
                if (!vendorIds.Contains(vendor.VendorId))
                {
                    return $"The save is missing vendor {vendor.VendorId}.";
                }
            }

            if (!TryStateName(snapshot.mainState, out ready.Main) || !TryStateName(snapshot.route, out ready.Route) ||
                !TryStateName(snapshot.disposition, out ready.Disposition) || !TryStateName(snapshot.herbsState, out ready.Herbs) ||
                !TryStateName(snapshot.badgeState, out ready.Badge))
            {
                return "Unknown quest state name.";
            }

            error = QuestLog.ValidateState(ready.Main, ready.Route, ready.Disposition, ready.Herbs, ready.Badge, ready.Clues);
            if (error != null)
            {
                return error;
            }

            foreach (var id in snapshot.perks)
            {
                switch (id)
                {
                    case "perk_hardy": ready.Perks.Add(Perk.Hardy); break;
                    case "perk_skirmisher": ready.Perks.Add(Perk.Skirmisher); break;
                    case "perk_trusted_face": ready.Perks.Add(Perk.TrustedFace); break;
                    default: return $"Unknown perk {id}.";
                }
            }

            if (!Progression.IsValidState(ready.Xp, ready.Perks, ready.AwardedEvents))
            {
                return "Invalid progression state.";
            }

            error = QuestLog.ValidateConsistency(ready.Main, ready.Herbs, ready.Badge, ready.QuestItems,
                new HashSet<string>(ready.AppliedEvents, StringComparer.Ordinal),
                new HashSet<string>(ready.AwardedEvents, StringComparer.Ordinal));
            if (error != null)
            {
                return error;
            }

            state = ready;
            return null;
        }

        private static bool TryStateName<T>(string name, out T value) where T : struct =>
            Enum.TryParse(name, out value) && Enum.IsDefined(typeof(T), value) && value.ToString() == name;

        private static bool IsCompleteObject(string json)
        {
            if (string.IsNullOrEmpty(json) || json[0] != '{')
            {
                return false;
            }

            // JsonUtility may stop after the first object. Reject trailing data and truncation
            // even when the final character happens to close an inner object in the save.
            var brackets = new Stack<char>();
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') inString = true;
                else if (c == '{' || c == '[') brackets.Push(c);
                else if (c == '}' || c == ']')
                {
                    if (brackets.Count == 0 || brackets.Pop() != (c == '}' ? '{' : '['))
                    {
                        return false;
                    }

                    if (brackets.Count == 0)
                    {
                        return i == json.Length - 1;
                    }
                }
            }

            return false;
        }

        private static string NullIfEmpty(string value) => value == "" ? null : value;

        private static bool IsFileError(Exception exception) => exception is IOException ||
            exception is UnauthorizedAccessException || exception is SecurityException || exception is NotSupportedException;

        private static string PerkId(Perk perk)
        {
            switch (perk)
            {
                case Perk.Hardy: return "perk_hardy";
                case Perk.Skirmisher: return "perk_skirmisher";
                case Perk.TrustedFace: return "perk_trusted_face";
                default: throw new ArgumentOutOfRangeException(nameof(perk));
            }
        }

        private sealed class VendorRestore
        {
            public VendorStock Vendor;
            public Dictionary<string, int> Remaining;
        }

        private sealed class RestoreState
        {
            public List<SavedNpcMemory> Memories;
            public int Gold;
            public ItemStack[] Slots;
            public Dictionary<string, int> QuestItems;
            public string Weapon;
            public string Armor;
            public string[] QuickUse;
            public string[] AppliedEvents;
            public List<VendorRestore> Vendors;
            public MainQuestState Main;
            public RecoveryRoute Route;
            public ParcelDisposition Disposition;
            public SideQuestState Herbs;
            public SideQuestState Badge;
            public string[] Clues;
            public int Xp;
            public List<Perk> Perks;
            public string[] AwardedEvents;
        }
    }
}
