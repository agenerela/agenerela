using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Agenerela.Providers;

namespace Agenerela
{
    /// <summary>
    /// What an agent would be offered right now, and the request <see cref="Agent.DecideAsync"/>
    /// would send, worked out without asking the model (#166). <see cref="Agent.Preview"/> makes
    /// one. Read-only: nothing here changes the agent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built by the same code as a decision, so it cannot drift from what the model is really sent.
    /// Its lists are its own, but what they hold is the agent's: the action definitions, the
    /// identity and the sources are the agent's own objects, as <see cref="Agent.Actions"/> and
    /// <see cref="Agent.Identity"/> give them, not copies.
    /// That also means every target source, observation source and availability check runs, and
    /// <see cref="IMemoryStrategy.Recall"/> is called once, as a decision would call them. Nothing
    /// is sent and nothing is remembered.
    /// </para>
    /// <para>
    /// A source shows up under its type name. <b>Extension points:</b> a source of the Targets area
    /// (#120) shows its dropped targets when it implements <see cref="IReportsDroppedTargets"/>, one
    /// of the Observations area (#122) its dropped lines through
    /// <see cref="IReportsDroppedObservations"/>, and a strategy of the Memory area (#119) what it
    /// left out through <see cref="IReportsDroppedTurns"/>.
    /// </para>
    /// </remarks>
    public sealed class AgentPreview
    {
        private readonly List<ActionDefinition> available = new List<ActionDefinition>();
        private readonly List<LeftOutAction> leftOut = new List<LeftOutAction>();
        private readonly List<string> registeredTargets = new List<string>();
        private readonly List<PreviewSource> targetSources = new List<PreviewSource>();
        private readonly List<string> leftOutByCategory = new List<string>();
        private readonly List<PreviewSource> observationSources = new List<PreviewSource>();

        internal AgentPreview(string stimulus, IReadOnlyList<string> callObservations)
        {
            Stimulus = stimulus ?? "";
            CallObservations = callObservations;
            Available = available.AsReadOnly();
            LeftOut = leftOut.AsReadOnly();
            RegisteredTargets = registeredTargets.AsReadOnly();
            TargetSources = targetSources.AsReadOnly();
            TargetsLeftOutByCategory = leftOutByCategory.AsReadOnly();
            ObservationSources = observationSources.AsReadOnly();
        }

        /// <summary>The stimulus previewed, empty for none.</summary>
        public string Stimulus { get; }

        /// <summary>
        /// The request <see cref="Agent.DecideAsync"/> would send with the same stimulus and options:
        /// the schema, the system prompt, the examples, the observations and the history. Null when
        /// preparing it failed: <see cref="Error"/> says why.
        /// </summary>
        public DecisionRequest Request { get; internal set; }

        /// <summary>
        /// The context the request is built from: a copy of the agent's state, the targets resolved
        /// now, and every observation sent. Null when a target or observation source failed.
        /// </summary>
        public AgentContext Context { get; internal set; }

        /// <summary>The actions offered, in the order the schema lists them.</summary>
        public IReadOnlyList<ActionDefinition> Available { get; }

        /// <summary>
        /// The actions not offered, each with why: the profile, or a code-built agent's action list,
        /// names it and nothing binds it; it needs a target and none is offered; its handler's
        /// <see cref="IActionHandler.IsAvailable"/> says no; or, with
        /// <see cref="DecideOptions.PairTargetsWithActions"/>, no offered target is one it takes.
        /// </summary>
        public IReadOnlyList<LeftOutAction> LeftOut { get; }

        /// <summary>The targets registered by hand in <see cref="Agent.Targets"/>, by id, before any source.</summary>
        public IReadOnlyList<string> RegisteredTargets { get; }

        /// <summary>
        /// Each of <see cref="Agent.TargetSources"/>, in order: the ids it added, and the ids its own
        /// cap left out when it implements <see cref="IReportsDroppedTargets"/>.
        /// </summary>
        public IReadOnlyList<PreviewSource> TargetSources { get; }

        /// <summary>
        /// The ids found but not offered because <see cref="Agent.TargetCategories"/> does not list
        /// their category. Empty when it lists none.
        /// </summary>
        public IReadOnlyList<string> TargetsLeftOutByCategory { get; }

        /// <summary>
        /// Each of <see cref="Agent.ObservationSources"/>, in order: the lines it gave that are sent,
        /// and those left out, by <see cref="Agent.MaxObservations"/> and then by its own cap through
        /// <see cref="IReportsDroppedObservations"/>.
        /// </summary>
        public IReadOnlyList<PreviewSource> ObservationSources { get; }

        /// <summary>The call's own <see cref="DecideOptions.Observations"/>, blank lines left out. Always sent whole.</summary>
        public IReadOnlyList<string> CallObservations { get; }

        /// <summary>The memory recalled from: <see cref="Agent.Memory"/> when the preview was taken. Null for none.</summary>
        public IMemoryStrategy Memory { get; internal set; }

        /// <summary>
        /// The history that would be sent, as <see cref="DecisionRequest.History"/> carries it: two
        /// entries per exchange, oldest first. Null with no memory, or when preparing failed before it.
        /// </summary>
        public IReadOnlyList<string> History { get; internal set; }

        /// <summary>The exchanges the memory held but left out, when it implements <see cref="IReportsDroppedTurns"/>; 0 otherwise.</summary>
        public int HistoryTurnsDropped { get; internal set; }

        /// <summary>The exchanges the memory has let go of, a rising total, from <see cref="IReportsDroppedTurns"/>; 0 otherwise.</summary>
        public int HistoryTurnsForgotten { get; internal set; }

        /// <summary>
        /// <see cref="PipelineErrorKind.RequestPreparation"/> when preparing the request failed, as
        /// <see cref="Agent.DecideAsync"/> would record it; null otherwise.
        /// </summary>
        public PipelineErrorKind? ErrorKind { get; private set; }

        /// <summary>
        /// The message a decision would log for that failure, word for word. A preview logs nothing.
        /// Null when the request was prepared.
        /// </summary>
        public string Error { get; private set; }

        internal void Fail(PipelineErrorKind kind, string message)
        {
            ErrorKind = kind;
            Error = message;
        }

        internal void AddRegisteredTargets(IReadOnlyList<string> ids)
        {
            registeredTargets.AddRange(ids);
        }

        internal void AddTargetSource(ITargetSource source, List<string> added, IReadOnlyList<string> dropped)
        {
            targetSources.Add(new PreviewSource(source, added, dropped != null ? new List<string>(dropped) : new List<string>()));
        }

        internal void AddLeftOutByCategory(string id)
        {
            leftOutByCategory.Add(id);
        }

        internal void AddObservationSource(IObservationSource source, List<string> sent, List<string> dropped)
        {
            observationSources.Add(new PreviewSource(source, sent, dropped));
        }

        // From what the decision asked once: listed are every action the agent was given, offered
        // what ActionAvailability allowed, and takeable what pairing left of it. Nothing is asked
        // again, so an availability check that is not a pure function, a cooldown say, runs once.
        internal void SetActions(AgentContext ctx, IEnumerable<ActionDefinition> listed, IReadOnlyList<ActionDefinition> offered,
            IReadOnlyList<ActionDefinition> takeable)
        {
            available.AddRange(takeable);
            var kept = new HashSet<ActionDefinition>(takeable);
            var allowed = new HashSet<ActionDefinition>(offered);

            foreach (var definition in listed)
            {
                if (definition != null && !ctx.Actions.TryGet(definition.Id, out _, out _))
                {
                    leftOut.Add(new LeftOutAction(definition, "Nothing binds it, so nothing could run it."));
                }
            }

            foreach (var definition in ctx.Actions.Definitions)
            {
                if (kept.Contains(definition))
                {
                    continue;
                }

                string reason;
                if (allowed.Contains(definition))
                {
                    reason = "No target offered is one it takes.";
                }
                else if (definition.RequiresTarget && ctx.Targets.Count == 0)
                {
                    reason = "It needs a target, and none is offered.";
                }
                else
                {
                    reason = "Its handler's IsAvailable says no.";
                }

                leftOut.Add(new LeftOutAction(definition, reason));
            }
        }
    }

    /// <summary>One source in an <see cref="AgentPreview"/>: what it gave, and what was left out.</summary>
    public sealed class PreviewSource
    {
        internal PreviewSource(object source, List<string> kept, List<string> dropped)
        {
            Source = source;
            Name = source.GetType().Name;
            Kept = new ReadOnlyCollection<string>(kept);
            Dropped = new ReadOnlyCollection<string>(dropped);
        }

        /// <summary>The source's type name.</summary>
        public string Name { get; }

        /// <summary>The source itself: an <see cref="ITargetSource"/> or an <see cref="IObservationSource"/>.</summary>
        public object Source { get; }

        /// <summary>For a target source, the ids it added; for an observation source, its lines that are sent.</summary>
        public IReadOnlyList<string> Kept { get; }

        /// <summary>
        /// For a target source, the ids its cap left out, from <see cref="IReportsDroppedTargets"/>; for
        /// an observation source, its lines past <see cref="Agent.MaxObservations"/>, then those its
        /// own cap left out. Empty when it left nothing out, or cannot say.
        /// </summary>
        public IReadOnlyList<string> Dropped { get; }
    }

    /// <summary>An action an <see cref="AgentPreview"/> shows as not offered, and why.</summary>
    public readonly struct LeftOutAction
    {
        internal LeftOutAction(ActionDefinition action, string reason)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            Reason = reason;
        }

        /// <summary>The action.</summary>
        public ActionDefinition Action { get; }

        /// <summary>Why it is not offered, in a sentence.</summary>
        public string Reason { get; }
    }
}
