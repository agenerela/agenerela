using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>
    /// The settings for one call to <see cref="Agent.DecideAsync"/>. Every field has a default, and
    /// a null <c>DecideOptions</c> means all of them.
    /// </summary>
    /// <remarks>
    /// Open on purpose (DR-008): free strings and switches the developer sets per call, never an
    /// enum of situations. Whether this call is a player's line, a timer or a report is the
    /// developer's knowledge, and the framework never asks. What comes later goes here as more
    /// switches of the same kind, such as Phase 3's per-call guard settings, starting with
    /// <see cref="CheckTarget"/> (DR-016). <see cref="Priority"/> is one too: it says whether anyone
    /// is waiting, never why the agent is asked (DR-015).
    /// </remarks>
    public sealed class DecideOptions
    {
        /// <summary>
        /// How the stimulus is labelled in the prompt, and in the examples: "Player", "Advisor",
        /// "Report", "Situation"... Any non-empty string; an empty one is a mistake the call
        /// throws for.
        /// </summary>
        public string StimulusLabel = "Player";

        /// <summary>
        /// The stimulus of the idle example, the one answered with <c>none</c>, for this call only.
        /// Null uses the agent's <see cref="Agent.IdleExampleStimulus"/>, and when that is null too,
        /// the profile's <see cref="AgentProfile.IdleExampleStimulus"/>; empty leaves the example out
        /// of this call.
        /// </summary>
        public string IdleExampleStimulus;

        /// <summary>
        /// Whether this call's system prompt lists the actions offered with their descriptions, and
        /// the targets offered with their own lines (#190). Off by default: unmeasured, and owed an
        /// A/B run before it becomes the default (#151). <see cref="PromptOptions.ListActionsAndTargets"/>
        /// says what it adds.
        /// </summary>
        /// <remarks>
        /// For a provider that never reads the schema, such as Ollama, where a model otherwise learns
        /// the actions only from the few-shot examples. A target object says what it is by
        /// implementing <see cref="IDescribedTarget"/>.
        /// </remarks>
        public bool ListActionsAndTargets;

        /// <summary>
        /// The most targets <see cref="ListActionsAndTargets"/> lists, 12 by default
        /// (<see cref="PromptOptions.MaxListedTargets"/>). A negative value is a mistake the call
        /// throws for.
        /// </summary>
        public int MaxListedTargets = PromptOptions.DefaultMaxListedTargets;

        /// <summary>
        /// Whether this call's examples show each action's
        /// <see cref="ActionDefinition.CounterExampleStimulus"/>, answered with <c>none</c> (#190). On
        /// by default, which shows only what a developer wrote. False leaves them out of this call, as
        /// the control arm of an A/B run does.
        /// </summary>
        public bool CounterExamples = true;

        /// <summary>
        /// What the game wants the agent to know right now, one fact per entry, sent verbatim. The
        /// list is copied when the call starts, so changing it afterwards changes nothing.
        /// </summary>
        /// <remarks>
        /// Observations are what the model reads about the world. <see cref="Agent.State"/> is
        /// not: it stays with the developer's handlers and never reaches the prompt.
        /// </remarks>
        public IList<string> Observations = new List<string>();

        /// <summary>
        /// Whether anyone is waiting on this decision (DR-015). Every agent on one provider shares
        /// its queue, which sends a <see cref="DecisionPriority.PlayerFacing"/> request before any
        /// <see cref="DecisionPriority.Background"/> one. Player-facing by default; set it to
        /// background for ambient behaviour, a timer or anything else nobody is waiting for.
        /// </summary>
        /// <remarks>
        /// Going first means jumping the queue: a background request already sent is not aborted.
        /// The decision's telemetry records the priority, how long it waited and behind how many.
        /// </remarks>
        public DecisionPriority Priority = DecisionPriority.PlayerFacing;

        /// <summary>
        /// Whether <see cref="TargetGroundingGuard"/> checks this call's target: whether the model
        /// wanted something that is not on the list, and so would act on the nearest listed target
        /// instead (DR-016). On by default, whoever calls. Turn it off for a call where the agent
        /// should act on its best guess rather than do nothing.
        /// </summary>
        /// <remarks>
        /// Off, the guard does not run on that call, and the decision's telemetry records
        /// <see cref="GroundingStatus.Off"/>. It also turns off the guard's fallback: on a provider
        /// that reports no token probabilities, the prompt carries the one-line target rule only
        /// while this is on (<see cref="PromptOptions.TargetRule"/>, #107).
        /// </remarks>
        public bool CheckTarget = true;

        /// <summary>
        /// Whether to ask the model what to say when a guard stops this decision and
        /// <see cref="Agent.RefusalStatement"/> gave no line (#109). Off by default, so a stopped
        /// decision's statement is empty. Per call, because only the caller knows whether anyone is
        /// listening (DR-008).
        /// </summary>
        /// <remarks>
        /// It sends one more short request through the same provider and queue, at this call's
        /// <see cref="Priority"/>, and only when a guard fired. It never changes which action runs,
        /// and if it fails the decision stands with an empty statement. Its cost is recorded apart
        /// from the decision's, in <see cref="DecisionTelemetry.RefusalLineSeconds"/> and the fields
        /// beside it. The wording of that request is unmeasured.
        /// </remarks>
        public bool AskForRefusalLine;

        /// <summary>
        /// The caller's own object for this call, which the framework carries and never reads
        /// (DR-018): the conversation a reply belongs to, a turn number, a request id. Null by
        /// default.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It is set on <see cref="AgentContext.Correlation"/> of every context made for this
        /// decision, so availability checks, target and observation sources, guards and memory see
        /// it, and on <see cref="DecisionResult.Correlation"/>, pipeline errors included. When the
        /// result is executed, the context <see cref="Agent.Execute(DecisionResult)"/> hands the
        /// handler carries it too, so a handler knows which call it is acting for without anything
        /// written to <see cref="Agent.State"/>, which every call on the agent shares.
        /// </para>
        /// <para>
        /// The object itself is kept, never copied, and never reaches the model or the telemetry.
        /// It says which call this is, never why the agent is asked (DR-008).
        /// </para>
        /// </remarks>
        public object Correlation;

        // The options one call keeps, taken when it is made, so changing the caller's object
        // afterwards changes nothing, the guards' view included (#106). A memberwise copy, so a
        // switch added later is copied without being listed here; only a field holding a mutable
        // object needs copying by hand.
        internal DecideOptions Copy()
        {
            var copy = (DecideOptions)MemberwiseClone();
            copy.Observations = Observations == null ? null : new List<string>(Observations);
            return copy;
        }
    }
}
