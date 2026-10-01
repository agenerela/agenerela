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
        /// the schema (#8). An id already in <paramref name="into"/> is an error, because two
        /// objects answering to one id cannot both be offered.
        /// </summary>
        void Collect(AgentContext ctx, TargetRegistry into);
    }
}
