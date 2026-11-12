namespace Agenerela
{
    /// <summary>
    /// A memory strategy that can forget everything it holds, which is how <see cref="Agent.Reset"/>
    /// clears an agent's memory (#190, G12). Optional, beside <see cref="IMemoryStrategy"/>, as
    /// <see cref="IExportsMemory"/> is, so a strategy written before it still works. A reset then
    /// leaves that strategy's memory as it is, with a warning.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Extension point: the Memory research area (#119).</b> A strategy that holds more than
    /// exchanges, such as facts or a summary, forgets those too: after <see cref="Clear"/>, the next
    /// <see cref="IMemoryStrategy.Recall"/> returns what a new instance's first one would.
    /// </para>
    /// <para>
    /// Called on the main thread, like the rest of a strategy. A decision still waiting under
    /// <see cref="MemoryCommit.OnExecute"/> is not in the memory yet. <c>Agent.Reset</c> drops it
    /// itself, so a strategy never has to.
    /// </para>
    /// </remarks>
    public interface IClearableMemory
    {
        /// <summary>
        /// Forgets everything this memory holds, leaving it as a new instance with the same
        /// settings would be.
        /// </summary>
        void Clear();
    }
}
