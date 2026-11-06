using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela;
using LivingVale.Game;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// The NPCs' conversation memories in the save (gap 11, DR-018): each NPC's
    /// <see cref="RollingHistory"/>, through the framework's <see cref="IExportsMemory"/>, under its
    /// stable id. The memories are the model driver's, which every F7 switch shares.
    /// </summary>
    public sealed class NpcMemoryArchive : INpcMemoryArchive
    {
        private readonly Func<AgenerelaNpcDecisionDriver> driver;

        /// <param name="driver">The model driver in use now; read on every save and load, since F7 replaces it.</param>
        public NpcMemoryArchive(Func<AgenerelaNpcDecisionDriver> driver)
        {
            this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public List<SavedNpcMemory> Capture()
        {
            var saved = new List<SavedNpcMemory>();
            foreach (var pair in driver().Memories.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var snapshot = pair.Value.Export();
                saved.Add(new SavedNpcMemory
                {
                    npcId = pair.Key,
                    format = snapshot.Format,
                    version = snapshot.Version,
                    exchanges = snapshot.Exchanges.Select(e => new SavedExchange { stimulus = e.Stimulus, decision = e.Decision }).ToList(),
                    data = snapshot.Data ?? ""
                });
            }

            return saved;
        }

        public string Validate(List<SavedNpcMemory> memories)
        {
            if (memories == null)
            {
                return null;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var memory in memories)
            {
                if (memory == null || string.IsNullOrWhiteSpace(memory.npcId) || !ids.Add(memory.npcId))
                {
                    return "Missing or duplicate NPC memory ID.";
                }

                // RollingHistory reads any strategy's exchanges and ignores the format, by design. The
                // game saved its own, so anything else, or a newer version, is a save it cannot trust.
                if (memory.format != RollingHistory.SnapshotFormat || memory.version < 1 || memory.version > RollingHistory.SnapshotVersion)
                {
                    return $"Unreadable memory for {memory.npcId}: format {memory.format} version {memory.version}.";
                }

                try
                {
                    // Into a throwaway history: what Restore will do, without touching a real one.
                    new RollingHistory().Import(ToSnapshot(memory));
                }
                catch (Exception e) when (e is ArgumentException || e is FormatException || e is InvalidOperationException)
                {
                    return $"Unreadable memory for {memory.npcId}: {e.Message}";
                }
            }

            return null;
        }

        public void Restore(List<SavedNpcMemory> memories)
        {
            var current = driver();
            var restored = new HashSet<string>(StringComparer.Ordinal);
            foreach (var memory in memories ?? new List<SavedNpcMemory>())
            {
                current.MemoryOf(memory.npcId).Import(ToSnapshot(memory));
                restored.Add(memory.npcId);
            }

            // An NPC the save does not mention remembers nothing of a conversation that, in the
            // loaded game, never happened.
            foreach (var pair in current.Memories.ToList())
            {
                if (!restored.Contains(pair.Key))
                {
                    pair.Value.Import(Empty());
                }
            }
        }

        private static MemorySnapshot ToSnapshot(SavedNpcMemory memory)
        {
            var snapshot = new MemorySnapshot
            {
                Format = memory.format,
                Version = memory.version,
                Data = string.IsNullOrEmpty(memory.data) ? null : memory.data
            };

            foreach (var exchange in memory.exchanges ?? new List<SavedExchange>())
            {
                snapshot.Exchanges.Add(new MemoryExchange(exchange?.stimulus, exchange?.decision));
            }

            return snapshot;
        }

        private static MemorySnapshot Empty() =>
            new MemorySnapshot { Format = RollingHistory.SnapshotFormat, Version = RollingHistory.SnapshotVersion };
    }
}
