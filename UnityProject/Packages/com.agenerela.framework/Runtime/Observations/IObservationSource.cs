using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>
    /// Where an agent's observations come from besides the call itself (DR-012, build plan
    /// §2.8): a source the agent asks at decision time, the way it asks an
    /// <see cref="ITargetSource"/> for targets, but for facts. An agent holds an ordered list of
    /// them in <see cref="Agent.ObservationSources"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pulled, never pushed. <see cref="Agent.DecideAsync"/> asks every source on every decision,
    /// in list order, after the targets are resolved, and keeps nothing between decisions: what a
    /// source returns is what the agent knows from it at that instant. A source that wants to
    /// remember, or to do something costly such as calling a model to summarise, keeps its own
    /// cache and refreshes it on the game's events, never once per decision (§2.8).
    /// </para>
    /// <para>
    /// The lines are sent verbatim, with nothing added, before the call's own
    /// <see cref="DecideOptions.Observations"/>, and within <see cref="Agent.MaxObservations"/>.
    /// Observations are the last thing the model reads before the question, so keep each line a
    /// short fact, and use a target's registered id when a line names one.
    /// </para>
    /// <para>
    /// A source that throws makes the decision a <see cref="DecisionOutcome.PipelineError"/>, and
    /// nothing is sent. A source that also wants to say what its own cap left out implements
    /// <see cref="IReportsDroppedObservations"/>. Only <c>Runtime/Unity/</c> touches a scene, so a
    /// source that reads one, such as <c>ProximityObservationSource</c>, lives there; one that
    /// reads the game's own data needs no scene at all.
    /// </para>
    /// </remarks>
    /// <example>
    /// A source with no scene: a country's treasury, read from the game's own data.
    /// <code>
    /// public sealed class TreasurySource : IObservationSource
    /// {
    ///     private readonly Country country;
    ///
    ///     public TreasurySource(Country country) { this.country = country; }
    ///
    ///     public IEnumerable&lt;string&gt; Observe(AgentContext ctx)
    ///     {
    ///         yield return $"Your treasury holds {country.Gold} gold.";
    ///         if (country.AtWar) yield return "You are at war.";
    ///     }
    /// }
    ///
    /// agent.ObservationSources.Add(new TreasurySource(france));
    /// </code>
    /// </example>
    public interface IObservationSource
    {
        /// <summary>
        /// This source's lines for one decision, most important first: when the agent's cap cuts,
        /// it cuts from the end. Return nothing, or null, when there is nothing to say; blank lines
        /// are skipped and take no place under the cap.
        /// </summary>
        /// <param name="ctx">
        /// The decision's context, with its targets already resolved, so a source may describe
        /// what <c>ctx.Targets</c> offers. Its <c>Observations</c> are the call's own, never
        /// another source's lines, so no source depends on the order of the others.
        /// </param>
        /// <remarks>
        /// Read in full straight after the call, so a lazy <c>yield return</c> body runs then,
        /// and an exception it throws fails the decision as one thrown by the call does.
        /// </remarks>
        IEnumerable<string> Observe(AgentContext ctx);
    }
}
