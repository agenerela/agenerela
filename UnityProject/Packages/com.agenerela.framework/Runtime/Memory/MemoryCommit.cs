namespace Agenerela
{
    /// <summary>
    /// When an agent's memory records a decision (DR-018), set on <see cref="Agent.MemoryCommit"/>.
    /// Either way, what is remembered is the decision as the guards left it (build plan §2.9,
    /// rule 1), into the memory the decision recalled from.
    /// </summary>
    public enum MemoryCommit
    {
        /// <summary>
        /// As <see cref="Agent.DecideAsync"/> returns it: every decision is remembered, whether or not
        /// the game ever acts on it. The default, and how memory always worked.
        /// </summary>
        OnDecide,

        /// <summary>
        /// When the game settles it: <see cref="Agent.Execute(DecisionResult)"/> remembers a decision
        /// it carried out, and <see cref="Agent.Commit"/> or <see cref="Agent.Discard"/> settle one by
        /// hand. A decision the game drops as stale, and never settles, is never remembered.
        /// </summary>
        OnExecute,
    }
}
