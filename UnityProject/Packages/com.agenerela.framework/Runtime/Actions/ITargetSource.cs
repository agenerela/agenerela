namespace Agenerela
{
    /// <summary>
    /// Where an agent's targets come from (DR-014). An agent holds an ordered list of sources
    /// and asks each one, per decision, to add what the agent may currently refer to.
    /// </summary>
    public interface ITargetSource
    {
        /// <summary>
        /// Adds this source's targets to <paramref name="into"/>, which may already hold
        /// targets from earlier sources. Finding nothing adds nothing: the registry stays
        /// empty rather than null, and an empty registry is what removes <c>target</c> from
        /// the schema (#8). An id already in <paramref name="into"/> for another object is an
        /// error, because two objects answering to one id cannot both be offered. The same object
        /// again is one target, as the earlier source registered it (DR-020):
        /// <see cref="TargetRegistry.Offer"/> registers by that rule, and
        /// <see cref="TargetRegistry.Register(string, object, string)"/> with a category, which an
        /// action's <see cref="ActionDefinition.TargetCategories"/> reads.
        /// </summary>
        /// <remarks>
        /// <c>Agent.Preview</c> calls it too, with no decision after it (#166), so a source should be
        /// safe to call when nothing follows: it finds targets, and changes nothing in the game.
        /// </remarks>
        void Collect(AgentContext ctx, TargetRegistry into);
    }
}
