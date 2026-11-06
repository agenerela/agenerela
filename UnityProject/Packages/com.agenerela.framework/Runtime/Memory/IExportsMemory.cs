namespace Agenerela
{
    /// <summary>
    /// A memory strategy that can be saved with the game and loaded back (DR-018). Optional, beside
    /// <see cref="IMemoryStrategy"/>, as <see cref="IReportsDroppedTurns"/> is, so a strategy that
    /// does not implement it still works and simply is not saved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Extension point: the Memory research area (#119).</b> A new strategy implements this to be
    /// saved. What it holds beyond plain exchanges, such as facts or a summary, goes in
    /// <see cref="MemorySnapshot.Data"/>, in its own encoding, named by
    /// <see cref="MemorySnapshot.Format"/> and <see cref="MemorySnapshot.Version"/>.
    /// </para>
    /// <para>
    /// A game saves it beside its own state:
    /// </para>
    /// <code>
    /// if (agent.Memory is IExportsMemory saved) save.guardMemory = saved.Export();
    /// // ...and on load:
    /// if (agent.Memory is IExportsMemory loaded) loaded.Import(save.guardMemory);
    /// </code>
    /// <para>
    /// Called on the main thread, like the rest of a strategy, and never by <c>Agent</c> itself. A
    /// decision still waiting under <see cref="MemoryCommit.OnExecute"/> is not in the memory yet, so
    /// it is not in the snapshot.
    /// </para>
    /// </remarks>
    public interface IExportsMemory
    {
        /// <summary>
        /// Everything this memory holds, as plain data: a new snapshot each call, which changing
        /// afterwards does not change the memory. Never null.
        /// </summary>
        MemorySnapshot Export();

        /// <summary>
        /// Replaces what this memory holds with <paramref name="snapshot"/>'s. A snapshot it cannot
        /// read, another strategy's format or a version it does not know included, throws and leaves
        /// the memory as it was. A strategy may also offer a <c>CanImport</c> check that changes
        /// nothing, as <see cref="RollingHistory.CanImport"/> does.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
        /// <exception cref="System.ArgumentException">The snapshot is not one this strategy can read.</exception>
        void Import(MemorySnapshot snapshot);
    }
}
