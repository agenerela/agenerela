using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Agenerela.Providers;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// The brain: one agent's identity, actions, targets and state, and the pipeline that asks a
    /// model for its next decision. Plain C#, not a <c>MonoBehaviour</c>, so a faction or a colony
    /// owns one in code with no GameObject involved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deciding and executing are two calls on purpose (hard rule 5). <see cref="DecideAsync"/> asks
    /// the provider, passes its answer through the guards (build plan §2.5), and returns what is
    /// left. <see cref="Execute"/> checks that choice again, on its own, against the agent as it is
    /// at that moment, and only then runs the game's code. The provider is untrusted (§2.4), and
    /// the world can change while the model thinks.
    /// </para>
    /// <para>
    /// The developer decides when to call <see cref="DecideAsync"/> and what to pass: a player's
    /// line, a report each turn, a timer, a perception event. The framework never classifies the
    /// call (DR-008).
    /// </para>
    /// </remarks>
    public sealed class Agent
    {
        // The guards Execute runs again: legality, and whether the target is a kind the action
        // takes, are what can change between deciding and acting. Grounding judged the reply, which
        // Execute does not have.
        private static readonly SchemaLegalityGuard Legality = new SchemaLegalityGuard();
        private static readonly TargetCategoryGuard Categories = new TargetCategoryGuard();

        // The built-in guards, in the order §2.5 fixes. They run before Guards and are never in it.
        // The category guard comes after grounding, so declaring categories never changes what
        // grounding reads or records (DR-020).
        private static readonly IDecisionGuard[] BuiltInGuards = { Legality, new TargetGroundingGuard(), Categories };
        private static readonly IDecisionGuard[] ExecuteGuards = { Legality, Categories };

        // Read once by each decision, which keeps it to the end. Its queue and health are found by
        // the instance, so every agent given it shares them (DR-013, DR-015).
        private ILLMProvider provider;

        // This agent's own turns, one at a time, used only by calls made while Backlog is set (G5).
        private readonly RequestScheduler turns = new RequestScheduler();

        // Decisions under way: from preparing the request until the result is returned.
        private int deciding;

        // Where Bind finds definitions: the profile, read when Bind is called, or the list a
        // code-only agent was given. Exactly one is set.
        private readonly AgentProfile profile;
        private readonly List<ActionDefinition> definitions;

        /// <summary>
        /// An agent built from a profile, the asset a developer creates first (#15). It keeps the
        /// profile's identity object, so editing that identity's fields changes every agent built
        /// from the profile, but assigning the profile a different identity later does not. The
        /// idle example is read from the profile on every call, unless <see cref="IdleExampleStimulus"/>
        /// is set. Its actions are registered with <see cref="Bind"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="profile"/> or <paramref name="provider"/> is null.</exception>
        /// <exception cref="ArgumentException">The profile has no identity.</exception>
        public Agent(AgentProfile profile, ILLMProvider provider)
            : this(provider)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (profile.Identity == null)
            {
                throw new ArgumentException($"Profile '{profile.name}' has no identity.", nameof(profile));
            }

            this.profile = profile;
            Identity = profile.Identity;
        }

        /// <summary>
        /// An agent built in code, with no asset (DR-011). <paramref name="actions"/> are the actions
        /// it can ever take, the code equivalent of a profile's list; <see cref="Bind"/> registers
        /// them. It has no idle example unless <see cref="IdleExampleStimulus"/> is set or a call
        /// passes one in <see cref="DecideOptions.IdleExampleStimulus"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">An entry of <paramref name="actions"/> is null.</exception>
        public Agent(AgentIdentity identity, IReadOnlyList<ActionDefinition> actions, ILLMProvider provider)
            : this(provider)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            // The list is copied, the definitions are not: like ActionRegistry, the agent reads the
            // developer's own objects.
            definitions = new List<ActionDefinition>(actions.Count);
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] == null)
                {
                    throw new ArgumentException($"actions[{i}] is null.", nameof(actions));
                }

                definitions.Add(actions[i]);
            }

            Identity = identity;
        }

        private Agent(ILLMProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            this.provider = provider;
            Actions = new ActionRegistry();
            Targets = new TargetRegistry();
            TargetSources = new List<ITargetSource>();
            ObservationSources = new List<IObservationSource>();
            State = new Dictionary<string, object>();
            Memory = new RollingHistory();
        }

        /// <summary>Who this agent is. The model reads it on every call.</summary>
        public AgentIdentity Identity { get; }

        /// <summary>
        /// The stimulus of this agent's idle example, the one answered with <c>none</c>, for every
        /// call that does not pass its own in <see cref="DecideOptions.IdleExampleStimulus"/>. Null by
        /// default, which uses the profile's <see cref="AgentProfile.IdleExampleStimulus"/>, so an agent
        /// built in code has none (#190, Living Vale #13). Empty leaves the example out. Read on every
        /// call, as the profile's is.
        /// </summary>
        /// <remarks>
        /// <see cref="AgentProfile.DefaultIdleExampleStimulus"/> gives a code-built agent the example a
        /// new profile has. It stays off by default because turning it on moves what a code-built
        /// agent sends, and whether an idle example helps is still unmeasured (build plan §2.3, #151).
        /// </remarks>
        public string IdleExampleStimulus { get; set; }

        /// <summary>
        /// The provider this agent asks. Setting it switches the backend or the model the agent
        /// decides with (DR-019), and keeps everything else: its memory, actions, targets, sources,
        /// guards and state. Each decision reads it once, when it starts, so one under way finishes
        /// on the provider it began with. Set it on the main thread.
        /// </summary>
        /// <remarks>
        /// Its queue and <see cref="ProviderHealth"/> are the new provider's, shared with every agent
        /// given that instance. To switch models on Ollama, build an <see cref="OllamaProvider"/> for
        /// the new model once and give it to every agent moving, warm it up first through
        /// <see cref="IModelLifecycle.WarmUpAsync"/>, and unload the old one after.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Set to null.</exception>
        public ILLMProvider Provider
        {
            get => provider;
            set => provider = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// What happens to a call made while this agent is already deciding (#190, G5). Null, the
        /// default, starts every call at once, and they decide side by side. A
        /// <see cref="DecisionBacklog"/> makes the agent decide one call at a time, the others
        /// waiting within its capacity: <see cref="DecisionBacklog.LatestWins"/>,
        /// <see cref="DecisionBacklog.DropOldest"/> or <see cref="DecisionBacklog.QueueWithCap"/>.
        /// Read when each call is made.
        /// </summary>
        /// <remarks>
        /// A call the backlog drops or refuses returns a result with no decision, whose telemetry's
        /// <see cref="DecisionTelemetry.ErrorKind"/> is <see cref="PipelineErrorKind.Superseded"/>
        /// or <see cref="PipelineErrorKind.BacklogFull"/>. Nothing is sent and nothing is logged.
        /// The time a call waited for its turn is in <see cref="DecisionTelemetry.BacklogWaitSeconds"/>.
        /// </remarks>
        public DecisionBacklog Backlog { get; set; }

        /// <summary>
        /// Whether a decision of this agent is under way: being prepared, waiting in the provider's
        /// queue, with the provider, or being judged. Calls waiting in its <see cref="Backlog"/> are
        /// not under way yet; <see cref="Waiting"/> counts them.
        /// </summary>
        public bool IsDeciding => Volatile.Read(ref deciding) > 0;

        /// <summary>How many calls wait in this agent's <see cref="Backlog"/> for their turn.</summary>
        public int Waiting => turns.Queued;

        /// <summary>
        /// The actions this agent can take, each bound to the code that runs it (#6). Filled by
        /// <see cref="Bind"/>, or directly through <see cref="ActionRegistry.Register"/> and
        /// <see cref="ActionRegistry.RegisterMethods"/>.
        /// </summary>
        public ActionRegistry Actions { get; }

        /// <summary>
        /// Targets registered by hand. Every decision starts from these, then adds what each of
        /// <see cref="TargetSources"/> finds, so this registry itself is never changed by a source.
        /// </summary>
        public TargetRegistry Targets { get; }

        /// <summary>
        /// Where the rest of the targets come from, asked in order on every decision (DR-014): a
        /// <c>ProximityTargetSource</c> for a scene agent, an <see cref="ExplicitTargetSource"/> or the
        /// developer's own for one with no scene. Empty by default.
        /// </summary>
        /// <remarks>
        /// An id offered for two different objects, by <see cref="Targets"/> and a source or by two
        /// sources, makes the decision a <see cref="DecisionOutcome.PipelineError"/>: two objects
        /// answering to one id cannot both be offered (DR-014). The same object offered twice is one
        /// target, as the first offer registered it (DR-020): the built-in sources skip it, and a
        /// source of the developer's does the same through <see cref="TargetRegistry.Offer"/>.
        /// </remarks>
        public List<ITargetSource> TargetSources { get; }

        /// <summary>
        /// The only target categories this agent is ever offered, whichever source finds them (#190
        /// G3, DR-020): a guard given <c>place</c>, <c>person</c> and <c>creature</c> is never offered
        /// the market's wares. Empty by default, which offers every target, as before.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Applied on every decision, and at <see cref="Execute"/>'s re-check, after
        /// <see cref="Targets"/> and every one of <see cref="TargetSources"/> have added theirs: a
        /// target whose <see cref="TargetRegistry.CategoryOf"/> is not listed, or which has no
        /// category, is left out, as a <c>Targetable</c> outside a proximity source's radius is. It is
        /// not recorded as dropped. Categories compare ordinally, and a blank entry is ignored.
        /// </para>
        /// <para>
        /// A source's own cap is applied before this, so a proximity source capped at 8 that finds 3
        /// wares leaves 5. Give the source the same categories, or let <c>AgentBehaviour</c> copy them
        /// to its proximity source, which it does when that source names none.
        /// </para>
        /// </remarks>
        public List<string> TargetCategories { get; } = new List<string>();

        /// <summary>
        /// Where the agent's own observations come from, asked in order on every decision after
        /// the targets are resolved (DR-012, §2.8): a <c>ProximityObservationSource</c> for a scene
        /// agent, or the developer's own <see cref="IObservationSource"/>. Empty by default, so an
        /// agent knows only what each call passes in <see cref="DecideOptions.Observations"/>.
        /// </summary>
        /// <remarks>
        /// Their lines are sent before the call's own, in list order, within
        /// <see cref="MaxObservations"/>. Nothing is kept between decisions. A source that throws,
        /// or a null entry, makes the decision a <see cref="DecisionOutcome.PipelineError"/>.
        /// <see cref="Execute"/> never asks them: observations are an input to deciding, not to
        /// acting.
        /// </remarks>
        public List<IObservationSource> ObservationSources { get; }

        /// <summary>
        /// The most observation lines one request carries, 8 by default (build plan §2.8). The
        /// call's own <see cref="DecideOptions.Observations"/> are always sent whole, and count
        /// against it; <see cref="ObservationSources"/> fill what is left, in order, and their lines
        /// past it are recorded in <see cref="DecisionTelemetry.ObservationsDropped"/>.
        /// </summary>
        /// <remarks>
        /// Observations are the last thing the model reads, and the measured good prompt was 143
        /// tokens against a 332-token baseline (Appendix A): an unbounded gatherer is the easiest
        /// way to lose that while it looks like a model problem. Blank lines take no place. 0 means
        /// the sources add nothing. Read once per decision, when it starts.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Set below 0.</exception>
        public int MaxObservations
        {
            get => maxObservations;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MaxObservations cannot be negative.");
                }

                maxObservations = value;
            }
        }

        private int maxObservations = 8;

        /// <summary>
        /// The game's own facts about this agent, such as <c>State["isFollowing"] = true</c>. Copied
        /// into <see cref="AgentContext.State"/> for each decision, where the handlers' availability
        /// checks read it. It never reaches the model, which reads the observations instead:
        /// <see cref="DecideOptions.Observations"/> and the lines of <see cref="ObservationSources"/>.
        /// </summary>
        public Dictionary<string, object> State { get; }

        /// <summary>
        /// What this agent remembers between decisions (build plan §2.9): a new
        /// <see cref="RollingHistory"/> of its own by default, the last six exchanges within its
        /// token budget. Null means no memory: no history is sent and nothing is remembered.
        /// </summary>
        /// <remarks>
        /// Each agent has its own, never one on <see cref="AgentProfile"/>, because agents built from
        /// one profile never share their memories (§2.9, rule 2). Read once per decision, when it
        /// starts, so replacing it while a decision is in flight leaves that decision with the one
        /// it recalled from. Giving two agents the same instance makes them share it.
        /// </remarks>
        public IMemoryStrategy Memory { get; set; }

        /// <summary>
        /// When <see cref="Memory"/> records a decision (DR-018). <see cref="Agenerela.MemoryCommit.OnDecide"/>,
        /// the default, remembers it as <see cref="DecideAsync"/> returns it, as memory always has.
        /// <see cref="Agenerela.MemoryCommit.OnExecute"/> holds it on the result until the game settles it:
        /// <see cref="Execute(DecisionResult)"/> remembers a decision it carries out, and
        /// <see cref="Commit"/> and <see cref="Discard"/> settle one by hand.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For a game that may drop a reply after it arrives: a conversation that timed out, was
        /// cancelled or moved to another speaker. Under <see cref="Agenerela.MemoryCommit.OnDecide"/> the
        /// agent would remember a line nobody saw. A decision never settled is never remembered, and
        /// needs no clean-up: it is held on its own result, not on the agent.
        /// </para>
        /// <para>
        /// Read once per decision, when it starts, as <see cref="Memory"/> is, and the held decision
        /// goes into the memory it recalled from. It is remembered as it would have been on deciding,
        /// with the same context and request, but in the order decisions are settled, not the order
        /// they were made. A decision <see cref="Execute(DecisionResult)"/> refuses, or whose handler
        /// reports <see cref="ExecutionStatus.Failed"/>, is left waiting, for the game to
        /// <see cref="Commit"/>, say because the agent's line was still shown, or to
        /// <see cref="Discard"/>.
        /// </para>
        /// <para>
        /// <b>Extension point: the Memory research area (#119).</b> This works with any
        /// <see cref="IMemoryStrategy"/>: a strategy only ever sees <see cref="IMemoryStrategy.Remember"/>,
        /// called later.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Set to a value the enum does not define.</exception>
        public MemoryCommit MemoryCommit
        {
            get => memoryCommit;
            set
            {
                if (value != MemoryCommit.OnDecide && value != MemoryCommit.OnExecute)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MemoryCommit is OnDecide or OnExecute.");
                }

                memoryCommit = value;
            }
        }

        private MemoryCommit memoryCommit = MemoryCommit.OnDecide;

        // What a held decision names as the agent that made it: a token, never the agent itself,
        // so a result the game keeps after settling it keeps no agent alive.
        private readonly object holder = new object();

        /// <summary>
        /// The game's own rules over every decision (build plan §2.5, item 3), such as a cooldown
        /// or line of sight: guards that run after the built-in ones, in list order, inside
        /// <see cref="DecideAsync"/>. Empty by default. A <see cref="DelegateGuard"/> makes one
        /// from a lambda.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The built-in guards, <see cref="SchemaLegalityGuard"/>, the grounding guard and
        /// <see cref="TargetCategoryGuard"/>, are never in this list and cannot be removed; <see cref="DecideOptions.CheckTarget"/> turns
        /// grounding off for one call. Once a guard has stopped a decision, the rest are skipped.
        /// </para>
        /// <para>
        /// <see cref="Execute"/> never runs these again. A rule that must still hold at the moment
        /// of acting belongs in the handler or its <see cref="IActionHandler.IsAvailable"/>.
        /// </para>
        /// <para>
        /// A guard that throws, or a null entry, does not refuse the decision: it makes it a
        /// <see cref="DecisionOutcome.PipelineError"/> of kind
        /// <see cref="PipelineErrorKind.GuardFailed"/>, naming the guard. Read when the guards run,
        /// after the reply.
        /// </para>
        /// </remarks>
        public List<IDecisionGuard> Guards { get; } = new List<IDecisionGuard>();

        /// <summary>
        /// What the agent says when a guard stops a decision (build plan §2.5): the developer's own
        /// line, made from the verdict, which names the guard and its reason, and the decision's
        /// context. Null by default. Runs on the main thread, read each time a guard fires.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Its return value becomes the statement of the <see cref="ActionRegistry.None"/> decision.
        /// A null return gives no line: the statement stays empty, or the model is asked for one
        /// when the call set <see cref="DecideOptions.AskForRefusalLine"/>. An empty string is a
        /// line, saying nothing, so no request follows it.
        /// </para>
        /// <para>
        /// A callback that throws is logged as a warning and treated as a null return. The decision
        /// still stands.
        /// </para>
        /// </remarks>
        public Func<GuardVerdict, AgentContext, string> RefusalStatement { get; set; }

        /// <summary>
        /// Registers the action <paramref name="actionId"/>, as the profile defines it, with the game
        /// code that runs it, so the developer never fetches the definition from the asset by hand.
        /// An action the profile lists but nobody binds is never offered: nothing could run it.
        /// </summary>
        /// <remarks>
        /// Throws as <see cref="ActionRegistry.Register"/> does for a null handler, an id already
        /// registered, or the reserved <see cref="ActionRegistry.None"/>. An action the profile does
        /// not list can still be registered through <see cref="Actions"/>.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// The profile, or a code-only agent's action list, has no action with this id, or has two.
        /// </exception>
        public void Bind(string actionId, IActionHandler handler)
        {
            ActionDefinition match = null;

            foreach (var definition in Definitions())
            {
                if (definition == null || !string.Equals(definition.Id, actionId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                {
                    throw new ArgumentException($"{Source()} defines action '{actionId}' twice.", nameof(actionId));
                }

                match = definition;
            }

            if (match == null)
            {
                throw new ArgumentException(
                    $"{Source()} has no action '{actionId}'. Add it there, or register it through Actions.",
                    nameof(actionId));
            }

            Actions.Register(match, handler);
        }

        /// <summary>
        /// Asks the provider what this agent should do about <paramref name="stimulus"/>, and returns
        /// its answer with the telemetry of the request. Decides only: nothing runs until
        /// <see cref="Execute"/>.
        /// </summary>
        /// <param name="stimulus">
        /// What the agent is responding to, sent verbatim and labelled with
        /// <see cref="DecideOptions.StimulusLabel"/>. Null is sent as empty, as
        /// <see cref="AgentContext"/> treats it.
        /// </param>
        /// <param name="options">Null uses the defaults.</param>
        /// <param name="ct">
        /// Cancels the decision. While it waits in the provider's queue, it leaves the queue and is
        /// never sent; once sent, the token is passed to the provider, which stops its request. A
        /// decision cancelled at any point has no result.
        /// </param>
        /// <returns>
        /// The decision, or, when there is none to be had, a result whose
        /// <see cref="DecisionResult.Decision"/> is null and whose telemetry's
        /// <see cref="DecisionTelemetry.Outcome"/> is <see cref="DecisionOutcome.PipelineError"/>,
        /// with the reason logged as a warning. That covers anything failing while the request is
        /// prepared, such as a target source offering an id twice, an observation source or an
        /// availability check that throws; a provider that throws; a reply that is empty, is not
        /// one JSON object, or lacks a field the schema asked for; and a guard that throws, or a
        /// null entry in <see cref="Guards"/>. A reply naming an action or target the request did
        /// not offer is still a decision, which the guards reject (see <c>DecisionParser</c> and
        /// <see cref="SchemaLegalityGuard"/>). The telemetry says which of these it was in
        /// <see cref="DecisionTelemetry.ErrorKind"/>, from the <see cref="ProviderException.Kind"/>
        /// when a provider threw one, and holds the warning in
        /// <see cref="DecisionTelemetry.ErrorMessage"/> (#87). Every result whose request was sent
        /// carries it in <see cref="DecisionResult.Request"/>, and the model's reply, when one
        /// arrived, in <see cref="DecisionResult.Reply"/>, pipeline errors included (#142).
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <see cref="DecideOptions.StimulusLabel"/> is null, empty or whitespace, or
        /// <see cref="DecideOptions.MaxListedTargets"/> is negative. Thrown by the call itself, before
        /// anything is sent: it is a mistake in the calling code, not an event.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the result is awaited, if <paramref name="ct"/> was cancelled before the reply
        /// was read.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The pipeline, in order: a context from the identity, a copy of <see cref="State"/>, the
        /// targets resolved now, the stimulus and the observations, those of
        /// <see cref="ObservationSources"/> first and the call's own last, within
        /// <see cref="MaxObservations"/> (#91); the schema from the actions
        /// available now (#7, #8); the examples (#10); the request (#16); the provider's queue
        /// (#89); the provider (#12); the parse; the guards (#106). Telemetry records the latency of
        /// the provider's request on a monotonic clock, from the send and not the enqueue; the
        /// priority, the time spent queued and how many requests were ahead; the provider's name,
        /// the framework version, the token counts the provider reported, and the target ids each
        /// <see cref="IReportsDroppedTargets"/> source left out, in source order (#81); the
        /// observation lines left out, by the cap or by a source's own (#91); and the exchanges of
        /// history sent, left out and forgotten (#90).
        /// </para>
        /// <para>
        /// The guards (build plan §2.5) run on every decision, never on a pipeline error: first
        /// <see cref="SchemaLegalityGuard"/>, then the grounding guard (#105), then
        /// <see cref="TargetCategoryGuard"/> (DR-020), then <see cref="Guards"/>, the developer's (#108). The first that does not pass stops them,
        /// and the result's <see cref="DecisionResult.Decision"/> becomes <see cref="ActionRegistry.None"/> with
        /// <see cref="TargetRegistry.NoTarget"/>. The model's own answer stays on
        /// <see cref="DecisionResult.ModelDecision"/>, the verdict on
        /// <see cref="DecisionResult.Verdict"/>, and the guard's name, the verdict's kind and its
        /// reason in <see cref="DecisionTelemetry.GuardsFired"/>,
        /// <see cref="DecisionTelemetry.Verdict"/> and <see cref="DecisionTelemetry.GuardReason"/>.
        /// A <see cref="GuardVerdict.Reject"/>, the provider breaking the contract, is logged as a
        /// warning; a <see cref="GuardVerdict.RewriteToNone"/>, a contained refusal, is not.
        /// </para>
        /// <para>
        /// A stopped decision's statement is never the model's (#109). It is
        /// <see cref="RefusalStatement"/>'s line when that gives one; otherwise, with
        /// <see cref="DecideOptions.AskForRefusalLine"/>, what the model answers to one more short
        /// request, sent through the same queue at the call's priority; otherwise empty. That request
        /// runs no guard and cannot change the action. If it fails, a warning is logged, the kind goes
        /// in <see cref="DecisionTelemetry.RefusalLineError"/>, and the decision stands with an empty
        /// statement. Cancelling during it cancels the decision, as at any other point.
        /// </para>
        /// <para>
        /// <see cref="Memory"/> is recalled while the request is prepared, into
        /// <see cref="DecisionRequest.History"/>, and so fails as preparing it does, including on a
        /// history that is not whole exchanges. The decision is remembered just before it is
        /// returned, as the guards left it, never as the model answered: whitespace or extra fields
        /// in the reply are not remembered, and neither is an action a guard stopped, nor its
        /// statement: the history holds the line actually said, or an empty one. A pipeline
        /// error or a cancelled call remembers nothing; a <see cref="ActionRegistry.None"/>
        /// decision is remembered, because it happened. If remembering throws, the warning is
        /// logged and the decision still returned. Remembering happens on the main thread, even
        /// when the provider answered on another. All of this is under
        /// <see cref="Agenerela.MemoryCommit.OnDecide"/>, the default; under
        /// <see cref="Agenerela.MemoryCommit.OnExecute"/> the same decision waits on its result until the game
        /// settles it (<see cref="MemoryCommit"/>, DR-018).
        /// </para>
        /// <para>
        /// <see cref="DecideOptions.Correlation"/> is on every context this call makes and on the
        /// result, pipeline errors included, and never reaches the provider.
        /// </para>
        /// <para>
        /// Every agent given the same provider instance shares one queue, which sends at most one
        /// request at a time: player-facing ones first, then background ones, each in the order
        /// they were made (<see cref="DecideOptions.Priority"/>, DR-015). Agents on different
        /// provider instances never wait on each other. A failed request frees its place like an
        /// answered one, and nothing is retried.
        /// </para>
        /// <para>
        /// Every request's outcome is reported to the provider's <see cref="ProviderHealth"/>: a
        /// reply marks it up, a provider failure down. With its <see cref="ProviderHealth.BackOff"/>
        /// on, a background request whose turn comes while it is down, before the retry time, is not
        /// sent, and its result has no decision, as <see cref="PipelineErrorKind.ProviderDown"/>.
        /// With a <see cref="Backlog"/>, a call made while the agent is deciding waits for its turn
        /// before any of this, or is dropped or refused, as <see cref="PipelineErrorKind.Superseded"/>
        /// or <see cref="PipelineErrorKind.BacklogFull"/>. None of the three is logged (#190).
        /// </para>
        /// </remarks>
        public Awaitable<DecisionResult> DecideAsync(string stimulus, DecideOptions options = null, CancellationToken ct = default)
        {
            // Copied now, so changing the options after the call changes nothing, including what
            // the guards read from them.
            options = options == null ? new DecideOptions() : options.Copy();

            // Checked here, outside the async body, so the mistake throws at the call rather than
            // becoming a PipelineError.
            if (string.IsNullOrWhiteSpace(options.StimulusLabel))
            {
                throw new ArgumentException(
                    "DecideOptions.StimulusLabel is empty. It labels the stimulus; the default is \"Player\".",
                    nameof(options));
            }

            if (options.MaxListedTargets < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxListedTargets,
                    "DecideOptions.MaxListedTargets cannot be negative. 0 lists no targets.");
            }

            // A list of the context's own, apart from the options' copy a guard may read. Blank
            // lines are not something the agent knows, so they take no place under the cap.
            var observations = new ReadOnlyCollection<string>(NotBlank(options.Observations));
            string idleExample = options.IdleExampleStimulus ?? IdleExampleStimulus ?? (profile != null ? profile.IdleExampleStimulus : null);

            return InTurn(Backlog, stimulus, options, idleExample, observations, ct);
        }

        /// <summary>
        /// Carries out a decision: checks it again against the agent as it is now, then runs the
        /// bound handler. Returns whether the decision was carried out. The overload with an
        /// <see cref="ActionResult"/> also says what that did.
        /// </summary>
        /// <returns>
        /// True when the handler ran, or when the decision was <see cref="ActionRegistry.None"/>,
        /// which runs no handler: a decision a guard rewrote to <c>none</c> included. False, with no
        /// handler run, for a result with no decision (a pipeline error, already logged), for one a
        /// guard rejected inside <see cref="DecideAsync"/> (already logged), and for a decision the
        /// check refuses, logged as a warning saying why. False too when the handler ran and
        /// reported <see cref="ExecutionStatus.Failed"/> (DR-018), which only a handler that reports
        /// a result can, so a handler written before it never makes this false.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The check (hard rule 5) is <see cref="SchemaLegalityGuard"/>, run again against the agent
        /// as it is right now: the action is registered; the target is registered, or is
        /// <see cref="TargetRegistry.NoTarget"/>; an action that needs a target got a real one; and
        /// the action is available, recomputed here rather than taken from the request it was
        /// chosen from. The targets are resolved again for it, so one a source no longer offers is
        /// refused. <see cref="TargetCategoryGuard"/> runs again after it (DR-020). No other guard
        /// runs again: grounding judged the reply, which this call does not have. A refusal is added to the result's telemetry, as
        /// <c>"SchemaLegalityGuard@Execute"</c> in <see cref="DecisionTelemetry.GuardsFired"/>, with
        /// <see cref="DecisionTelemetry.Verdict"/> and <see cref="DecisionTelemetry.GuardReason"/>;
        /// <see cref="DecisionResult.Verdict"/> stays what <see cref="DecideAsync"/> made it.
        /// </para>
        /// <para>
        /// A refusal returns false rather than throwing: the provider is untrusted, so an answer to
        /// refuse is an expected event, not a mistake in the calling code. Exceptions thrown by the
        /// developer's own code, a handler, an availability check or a target source, propagate.
        /// The handler gets a context built now, holding the current state and targets and the
        /// decision's <see cref="DecisionResult.Correlation"/>; it has no stimulus or observations,
        /// which were the inputs to deciding, not to acting.
        /// </para>
        /// <para>
        /// <b>Extension point: the Guards research area (#138).</b> The re-check here is the one place
        /// a rule is checked at the moment of acting. A guard that must hold then, rather than when
        /// the model answered, would run beside it; until one does, such a rule belongs in the
        /// handler, which can now say so with <see cref="ActionResult.Failed"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        public bool Execute(DecisionResult result)
        {
            return Execute(result, out _);
        }

        /// <summary>
        /// Carries out a decision as <see cref="Execute(DecisionResult)"/> does, and says what that did
        /// (DR-018): whether a handler ran, whether it did what was asked, why not, and what it made.
        /// </summary>
        /// <param name="result">The decision, as <see cref="DecideAsync"/> returned it.</param>
        /// <param name="outcome">
        /// <see cref="ExecutionStatus.Done"/> or <see cref="ExecutionStatus.Failed"/> as the handler
        /// reported it, through <see cref="IActionHandlerWithResult"/>, a
        /// <see cref="DelegateActionHandler.WithResult"/> function or an <c>[AgentAction]</c> method
        /// returning <see cref="ActionResult"/>; <see cref="ExecutionStatus.Done"/> for any other
        /// handler that returns. <see cref="ExecutionStatus.NoAction"/> for <c>none</c>, and
        /// <see cref="ExecutionStatus.Refused"/>, with the reason, whenever
        /// <see cref="Execute(DecisionResult)"/> would return false without running a handler.
        /// </param>
        /// <returns><paramref name="outcome"/>'s <see cref="ActionResult.Succeeded"/>, which is what <see cref="Execute(DecisionResult)"/> returns.</returns>
        /// <remarks>
        /// <para>
        /// Everything <see cref="Execute(DecisionResult)"/> says holds here: the re-check, what is
        /// logged, the context the handler gets, and that exceptions propagate. A handler that
        /// throws leaves no outcome, since the call throws, and records nothing.
        /// </para>
        /// <para>
        /// The status and reason are written to the result's telemetry, in
        /// <see cref="DecisionTelemetry.Execution"/> and <see cref="DecisionTelemetry.ExecutionReason"/>,
        /// each call replacing the last's; the payload is not, being the game's own object. A
        /// <see cref="ExecutionStatus.Failed"/> is the game's own answer, so it is not logged.
        /// </para>
        /// <para>
        /// Under <see cref="Agenerela.MemoryCommit.OnExecute"/>, a decision this agent made and this call carries
        /// out is remembered now, once, however often it is executed. One it does not carry out
        /// stays waiting for <see cref="Commit"/> or <see cref="Discard"/>. A memory that throws is
        /// logged as a warning, and the decision still counts as carried out.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        public bool Execute(DecisionResult result, out ActionResult outcome)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            outcome = CarryOut(result);
            result.Telemetry.Execution = outcome.Status;
            result.Telemetry.ExecutionReason = outcome.Reason;

            if (outcome.Succeeded)
            {
                Settle(result, remember: true);
            }

            return outcome.Succeeded;
        }

        /// <summary>
        /// Remembers a decision held back by <see cref="Agenerela.MemoryCommit.OnExecute"/>, as it would have
        /// been remembered on deciding. For a decision the game acted on without
        /// <see cref="Execute(DecisionResult)"/> carrying it out: a line shown although the handler
        /// failed, say. Returns whether it was waiting; false when it was already settled, or was
        /// never held, because it was made under <see cref="Agenerela.MemoryCommit.OnDecide"/>, with no memory,
        /// or is a pipeline error.
        /// </summary>
        /// <remarks>
        /// A memory that throws is logged as a warning, and the decision is settled all the same, so
        /// it is never remembered twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="result"/> was decided by another agent.</exception>
        public bool Commit(DecisionResult result)
        {
            CheckOwnHeld(result);
            return Settle(result, remember: true);
        }

        /// <summary>
        /// Settles a decision held back by <see cref="Agenerela.MemoryCommit.OnExecute"/> without remembering it,
        /// so that a later <see cref="Execute(DecisionResult)"/> or <see cref="Commit"/> does not
        /// either: a reply the game drops as stale. Returns whether it was waiting, as
        /// <see cref="Commit"/> does.
        /// </summary>
        /// <remarks>
        /// Not needed for a result the game simply drops: one never settled is never remembered.
        /// This is for one that might still reach <see cref="Execute(DecisionResult)"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="result"/> was decided by another agent.</exception>
        public bool Discard(DecisionResult result)
        {
            CheckOwnHeld(result);
            return Settle(result, remember: false);
        }

        // What Execute does with one result, without the bookkeeping around it.
        private ActionResult CarryOut(DecisionResult result)
        {
            var decision = result.Decision;
            if (decision == null || result.Telemetry.Outcome == DecisionOutcome.PipelineError)
            {
                return ActionResult.Refused("There is no decision to carry out: deciding failed with a pipeline error.");
            }

            // Its Decision is none, but the provider broke the contract, and DecideAsync said so.
            if (result.Verdict.HasValue && result.Verdict.Value.Kind == GuardVerdictKind.Reject)
            {
                var rejected = result.Verdict.Value;
                return ActionResult.Refused($"{rejected.Guard} rejected the model's answer inside DecideAsync: {rejected.Reason}");
            }

            if (decision.ActionId == ActionRegistry.None)
            {
                return ActionResult.NoAction();
            }

            // No dropped targets to fill: this Collect overwrites a source's Dropped, but the result
            // already holds its own copy from the decision.
            var ctx = Snapshot("", Array.Empty<string>(), dropped: null, result.Correlation);

            // No request and no reply: only the agent as it is now. The options are the defaults
            // with the call's correlation, and no guard that runs here reads them.
            var input = new GuardInput(ctx, null, null, decision, new DecideOptions { Correlation = result.Correlation }, result.Telemetry);
            var verdict = GuardPipeline.Run(ExecuteGuards, input, "@Execute");
            if (verdict.Kind != GuardVerdictKind.Pass)
            {
                Refuse(decision, verdict.Reason);
                return ActionResult.Refused(verdict.Reason);
            }

            ctx.Actions.TryGet(decision.ActionId, out _, out var handler);
            if (handler is IActionHandlerWithResult reporting)
            {
                return ActionResult.FromHandler(reporting.ExecuteWithResult(ctx, decision));
            }

            handler.Execute(ctx, decision);
            return ActionResult.Done();
        }

        private void CheckOwnHeld(DecisionResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Pending != null && result.Pending.Owner != holder)
            {
                throw new ArgumentException(
                    $"This decision was made by {result.Pending.OwnerName}, so only that agent can settle it.",
                    nameof(result));
            }
        }

        // The decision a result holds back from this agent's memory, remembered or dropped, once.
        // Nothing for a result held for another agent: Execute may carry out another agent's
        // decision, but its memory is that agent's to settle.
        private bool Settle(DecisionResult result, bool remember)
        {
            var pending = result.Pending;
            if (pending == null || pending.Settled || pending.Owner != holder)
            {
                return false;
            }

            var memory = pending.Memory;
            var ctx = pending.Context;
            var request = pending.Request;

            // Before remembering, so a memory that settles it again from inside Remember cannot
            // remember it twice.
            pending.Settle();

            if (remember)
            {
                Remember(memory, ctx, request, result);
            }

            return true;
        }

        // The decision was made; a memory that cannot record it does not undo it.
        private void Remember(IMemoryStrategy memory, AgentContext ctx, DecisionRequest request, DecisionResult result)
        {
            try
            {
                memory.Remember(ctx, request, result);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Who()} decided, but its memory could not remember it. {e.GetType().Name}: {e.Message}");
            }
        }

        // One call: with a backlog, its turn among this agent's calls first (G5), then the decision.
        // Counted as under way from the start of the decision to its result.
        private async Awaitable<DecisionResult> InTurn(
            DecisionBacklog backlog, string stimulus, DecideOptions call, string idleExample,
            IReadOnlyList<string> observations, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            RequestScheduler.Ticket turn = null;
            float waitedSeconds = 0f;
            if (backlog != null)
            {
                var waited = System.Diagnostics.Stopwatch.StartNew();
                turn = turns.Enqueue(call.Priority, ct, backlog.Capacity, backlog.WhenFull);
                if (turn == null)
                {
                    return NotSent(Telemetry(provider, call.Priority), PipelineErrorKind.BacklogFull,
                        $"{Who()} did not decide: it was deciding already, and its backlog ({backlog}) was full.", call.Correlation);
                }

                // A call whose turn came at once never waited, and records 0, not the time to queue it.
                bool waits = turn.IsQueued;

                try
                {
                    // Cancelled while waiting, the call ends here, as in the provider's queue.
                    await turn.Ready;
                }
                catch (SupersededException)
                {
                    turn.Dispose();
                    var dropped = Telemetry(provider, call.Priority);
                    dropped.BacklogWaitSeconds = (float)waited.Elapsed.TotalSeconds;
                    return NotSent(dropped, PipelineErrorKind.Superseded,
                        $"{Who()} did not decide: a newer call took this one's place in its backlog ({backlog}).", call.Correlation);
                }
                catch
                {
                    turn.Dispose();
                    throw;
                }

                // The turn comes on whichever thread ended the decision before it.
                await Awaitable.MainThreadAsync();
                waitedSeconds = waits ? (float)waited.Elapsed.TotalSeconds : 0f;
            }

            Interlocked.Increment(ref deciding);
            try
            {
                // Its turn came just as the caller cancelled: still never prepared.
                ct.ThrowIfCancellationRequested();
                return await Decide(stimulus, call, idleExample, observations, waitedSeconds, ct);
            }
            finally
            {
                Interlocked.Decrement(ref deciding);

                // The next call's turn, which may run it right here.
                turn?.Dispose();
            }
        }

        private async Awaitable<DecisionResult> Decide(
            string stimulus, DecideOptions call, string idleExample, IReadOnlyList<string> observations,
            float backlogWaitSeconds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // Once, so the whole decision, its refusal line included, is on one provider even if
            // Provider is set meanwhile (DR-019).
            var provider = this.provider;
            string label = call.StimulusLabel;
            var priority = call.Priority;
            var memory = Memory;
            var commit = MemoryCommit;
            var telemetry = Telemetry(provider, priority);
            telemetry.BacklogWaitSeconds = backlogWaitSeconds;

            AgentContext ctx;
            DecisionRequest request;
            try
            {
                ctx = Observe(Snapshot(stimulus, observations, telemetry.TargetsDropped, call.Correlation), telemetry.ObservationsDropped);
                var options = new PromptOptions
                {
                    StimulusLabel = label,
                    IdleExampleStimulus = idleExample,
                    TargetRule = call.CheckTarget && provider.Capabilities?.ReportsTokenProbabilities != true,
                    ListActionsAndTargets = call.ListActionsAndTargets,
                    MaxListedTargets = call.MaxListedTargets,
                    CounterExamples = call.CounterExamples,
                };

                // Asked once, so the action enum and the examples come from one answer even when an
                // availability check is not a pure function of the context, such as a cooldown.
                var available = ActionAvailability.For(ctx);
                var schema = DecisionSchema.Build(ctx, available);
                string fewShot = FewShotBuilder.Build(available, ctx.Targets, label, idleExample, options.CounterExamples);

                var history = Recall(memory, ctx, telemetry);
                request = PromptBuilder.Build(ctx, schema, fewShot, history, options);
                telemetry.TargetRuleSent = PromptBuilder.SendsTargetRule(schema, options);
            }
            catch (Exception e)
            {
                return Failed(telemetry, PipelineErrorKind.RequestPreparation,
                    $"Preparing the request failed. {e.GetType().Name}: {e.Message}", call.Correlation);
            }

            var sent = await Send(provider, request, priority, ct);
            telemetry.QueuedAhead = sent.QueuedAhead;
            telemetry.QueueWaitSeconds = sent.QueueWaitSeconds;
            telemetry.LatencySeconds = sent.Seconds;

            if (sent.HeldBack != null)
            {
                return NotSent(telemetry, PipelineErrorKind.ProviderDown, $"{Who()} did not decide. {sent.HeldBack}", call.Correlation);
            }

            if (sent.Failure != null)
            {
                return Failed(telemetry, KindOf(sent.Failure), $"The provider failed. {sent.Failure.GetType().Name}: {sent.Failure.Message}", call.Correlation, request);
            }

            var reply = sent.Reply;
            if (reply == null)
            {
                return Failed(telemetry, PipelineErrorKind.ProviderFailed, "The provider returned no result.", call.Correlation, request);
            }

            telemetry.Model = reply.Model ?? "";
            telemetry.PromptTokens = reply.PromptTokens;
            telemetry.CompletionTokens = reply.CompletionTokens;

            if (!DecisionParser.TryParse(reply.Text, request.Schema, out var decision, out string problem))
            {
                return Failed(telemetry, PipelineErrorKind.UnreadableReply, $"{problem} The reply was: {Excerpt(reply.Text)}", call.Correlation, request, reply);
            }

            var result = Guard(new GuardInput(ctx, request, reply, decision, call, telemetry));
            if (result.Verdict.HasValue)
            {
                result = await SayWhatWasStopped(provider, result, ctx, request, call, ct);
            }

            // After the guards and the line, so what is remembered is what the agent will do and
            // say (§2.9, rule 1). A guard that failed left no decision, and a pipeline error
            // remembers nothing. Under OnExecute it waits on the result for the game to settle it
            // (DR-018), with what remembering it now would have used.
            if (memory != null && result.Decision != null)
            {
                if (commit == MemoryCommit.OnExecute)
                {
                    result.Pending = new PendingMemory(holder, Who(), memory, ctx, request);
                }
                else
                {
                    Remember(memory, ctx, request, result);
                }
            }

            return result;
        }

        // The guards over one parsed decision: the built-in ones, then Guards. A decision none of
        // them stopped is returned as it is; one they stopped becomes none, beside the model's own.
        // A guard that throws is the developer's code failing, not a refusal: a pipeline error.
        private DecisionResult Guard(GuardInput input)
        {
            string running = null;
            GuardVerdict verdict;
            try
            {
                verdict = GuardPipeline.Run(AllGuards(name => running = name), input);
            }
            catch (Exception e)
            {
                return Failed(input.Telemetry, PipelineErrorKind.GuardFailed,
                    $"The guard {running} failed. {e.GetType().Name}: {e.Message}", input.Context.Correlation, input.Request, input.Reply);
            }

            var decision = input.Decision;

            if (verdict.Kind == GuardVerdictKind.Pass)
            {
                return new DecisionResult(decision, input.Telemetry, decision, null, input.Request, input.Reply, input.Context.Correlation);
            }

            if (verdict.Kind == GuardVerdictKind.Reject)
            {
                // Once, here: Execute returns false for it without saying so again.
                Debug.LogWarning($"{Who()} will do nothing: {verdict.Guard} rejected '{decision.ActionId}' on '{decision.TargetId}': {verdict.Reason}");
            }

            // Empty, never the model's: it no longer matches what the agent does (#109).
            var none = new AgentDecision(ActionRegistry.None, TargetRegistry.NoTarget, "");
            return new DecisionResult(none, input.Telemetry, decision, verdict, input.Request, input.Reply, input.Context.Correlation);
        }

        // The stopped decision with what the agent says about it: the developer's line, else the
        // model's when the call asked for one, else as it came, empty. Its request and reply stay
        // the decision's own, never the refusal line's.
        private async Awaitable<DecisionResult> SayWhatWasStopped(
            ILLMProvider provider, DecisionResult stopped, AgentContext ctx, DecisionRequest request, DecideOptions call,
            CancellationToken ct)
        {
            var verdict = stopped.Verdict.Value;

            string line = DevelopersLine(verdict, ctx);
            if (line == null && call.AskForRefusalLine)
            {
                line = await ModelsLine(provider, verdict, ctx, request, call.Priority, stopped.Telemetry, ct);
            }

            if (line == null)
            {
                return stopped;
            }

            var none = new AgentDecision(ActionRegistry.None, TargetRegistry.NoTarget, line);
            return new DecisionResult(none, stopped.Telemetry, stopped.ModelDecision, verdict, stopped.Request, stopped.Reply, stopped.Correlation);
        }

        // RefusalStatement's answer, or null. One that throws is the developer's code failing, but
        // the decision has already been made, so it costs only the line.
        private string DevelopersLine(GuardVerdict verdict, AgentContext ctx)
        {
            var refusalStatement = RefusalStatement;
            if (refusalStatement == null)
            {
                return null;
            }

            try
            {
                return refusalStatement(verdict, ctx);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Who()} has no line for what {verdict.Guard} stopped: RefusalStatement failed. {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        // The second request (#109), or null when it gives no line. Its numbers go in fields of
        // their own, so the decision's still mean the decision alone.
        private async Awaitable<string> ModelsLine(
            ILLMProvider provider, GuardVerdict verdict, AgentContext ctx, DecisionRequest decided,
            DecisionPriority priority, DecisionTelemetry telemetry, CancellationToken ct)
        {
            var sent = await Send(provider, PromptBuilder.BuildRefusalLine(decided, ctx, verdict), priority, ct);
            telemetry.RefusalLineSeconds = sent.Seconds;

            if (sent.HeldBack != null)
            {
                // Held back, as the decision would have been: nothing to warn about (#190).
                telemetry.RefusalLineError = PipelineErrorKind.ProviderDown;
                return null;
            }

            if (sent.Failure != null)
            {
                return NoLine(telemetry, KindOf(sent.Failure), $"The provider failed. {sent.Failure.GetType().Name}: {sent.Failure.Message}");
            }

            var reply = sent.Reply;
            if (reply == null)
            {
                return NoLine(telemetry, PipelineErrorKind.ProviderFailed, "The provider returned no result.");
            }

            telemetry.RefusalLinePromptTokens = reply.PromptTokens;
            telemetry.RefusalLineCompletionTokens = reply.CompletionTokens;

            if (!DecisionParser.TryParseStatement(reply.Text, out string line, out string problem))
            {
                return NoLine(telemetry, PipelineErrorKind.UnreadableReply, $"{problem} The reply was: {Excerpt(reply.Text)}");
            }

            return line;
        }

        private string NoLine(DecisionTelemetry telemetry, PipelineErrorKind kind, string reason)
        {
            telemetry.RefusalLineError = kind;
            Debug.LogWarning($"{Who()} still decided, but has no refusal line. {reason}");
            return null;
        }

        // One request through the provider's queue, shared with every agent on this provider
        // instance (DR-015), ending back on the main thread. A provider's failure is returned for
        // the caller to judge; only cancellation throws. Disposing the ticket frees the slot for the
        // next request, whether the provider answered, failed, was cancelled or was held back. The
        // outcome is reported to the provider's health, which may hold a background request back.
        private async Awaitable<SentRequest> Send(
            ILLMProvider provider, DecisionRequest request, DecisionPriority priority, CancellationToken ct)
        {
            var sent = new SentRequest();
            var health = ProviderHealth.For(provider);

            var waited = System.Diagnostics.Stopwatch.StartNew();
            using (var ticket = RequestScheduler.For(provider).Enqueue(priority, ct))
            {
                sent.QueuedAhead = ticket.QueuedAhead;

                // Cancelled while queued, the decision ends here and never reaches the provider.
                await ticket.Ready;

                // The turn comes on whichever thread freed the slot, such as the worker thread a
                // provider of the developer's own answered on, and this code resumes there. Every
                // provider call starts on the main thread, where one built on UnityWebRequest or
                // LLMUnity has to be called. Free when already there. The cancelled path needs no
                // hop: Unity brings the caller of an Awaitable started on the main thread back to it.
                await Awaitable.MainThreadAsync();

                // Its turn came just as the caller cancelled: still never sent.
                ct.ThrowIfCancellationRequested();

                // Enqueue to send, the wait for the main thread included, and the latency from the
                // send, so the two cover the whole call between them: Seconds is the provider's time
                // alone and a long queue shows in QueueWaitSeconds instead.
                sent.QueueWaitSeconds = (float)waited.Elapsed.TotalSeconds;

                // Checked at its turn, not when it joined, so a request queued before the provider
                // went down is held back too. Its slot goes straight to the next request.
                if (health.HoldsBack(priority, out string heldBack))
                {
                    sent.HeldBack = heldBack;
                    return sent;
                }

                var clock = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    sent.Reply = await provider.RequestAsync(request, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception) when (ct.IsCancellationRequested)
                {
                    // A provider that reports the caller's cancellation as some other exception, as an
                    // aborted web request tends to, still ends the call cancelled rather than counting
                    // as a failure.
                    throw new OperationCanceledException(ct);
                }
                catch (Exception e)
                {
                    sent.Failure = e;
                }

                sent.Seconds = (float)clock.Elapsed.TotalSeconds;

                // Recorded before the slot is freed, so the request it goes to is judged by this
                // outcome, on whichever thread the provider answered. Changed waits for the main
                // thread, below.
                sent.HealthChanged = Record(health, sent);
            }

            // Back on the main thread, as for the send: a provider that answered on a worker thread
            // resumed this code there, and the guards and the memory after it are no more
            // thread-safe than the rest of the agent. Free when already there.
            await Awaitable.MainThreadAsync();

            // On the main thread, where a game's handler can touch the scene. Before the check
            // below: a reply arrived, or the provider failed, whatever the caller did meanwhile.
            if (sent.HealthChanged)
            {
                health.RaiseChanged();
            }

            // Also for a provider that finished without noticing the cancellation.
            ct.ThrowIfCancellationRequested();

            return sent;
        }

        // What one sent request says about its provider: any reply means it answered, and a
        // failure is the provider's, since nothing but the provider ran (#190, G1). True when that
        // changed the provider's health.
        private static bool Record(ProviderHealth health, SentRequest sent)
        {
            if (sent.Reply != null)
            {
                return health.RecordReply();
            }

            if (sent.Failure is ProviderException failure)
            {
                return health.RecordFailure(KindOf(failure), failure.Message);
            }

            if (sent.Failure != null)
            {
                return health.RecordFailure(PipelineErrorKind.ProviderFailed, $"{sent.Failure.GetType().Name}: {sent.Failure.Message}");
            }

            return health.RecordFailure(PipelineErrorKind.ProviderFailed, "The provider returned no result.");
        }

        private sealed class SentRequest
        {
            public int QueuedAhead;
            public float QueueWaitSeconds;
            public float Seconds;
            public ProviderResult Reply;
            public Exception Failure;

            // Why ProviderHealth held it back unsent, or null when it was sent.
            public string HeldBack;

            // Whether its outcome changed the provider's health, so Changed is owed.
            public bool HealthChanged;
        }

        // Each guard in the order they run, telling running how to name it just before it does, so
        // a guard that throws is named in the warning.
        private IEnumerable<IDecisionGuard> AllGuards(Action<string> running)
        {
            foreach (var guard in BuiltInGuards)
            {
                running($"'{NameOf(guard)}'");
                yield return guard;
            }

            // A copy, so a guard that edits the list while it runs cannot break the loop.
            var guards = Guards.ToArray();
            for (int i = 0; i < guards.Length; i++)
            {
                var guard = guards[i];
                if (guard == null)
                {
                    running($"Guards[{i}]");
                    throw new InvalidOperationException($"Guards[{i}] is null.");
                }

                running($"'{NameOf(guard)}' (Guards[{i}])");
                yield return guard;
            }
        }

        // As telemetry records it, or by its type when its Name is blank or itself throws.
        private static string NameOf(IDecisionGuard guard)
        {
            string name;
            try
            {
                name = guard.Name;
            }
            catch (Exception)
            {
                name = null;
            }

            return string.IsNullOrWhiteSpace(name) ? guard.GetType().Name : name;
        }

        // The history for one request, as DecisionRequest.History encodes it: whole exchanges of
        // two entries, none null, or the request is not sent. Counted into the telemetry here.
        private static IReadOnlyList<string> Recall(IMemoryStrategy memory, AgentContext ctx, DecisionTelemetry telemetry)
        {
            if (memory == null)
            {
                return null;
            }

            var history = memory.Recall(ctx) ?? Array.Empty<string>();

            if (history.Count % 2 != 0)
            {
                throw new InvalidOperationException(
                    $"Memory recalled {history.Count} entries. History is whole exchanges, a stimulus then a decision, so the count must be even.");
            }

            for (int i = 0; i < history.Count; i++)
            {
                if (history[i] == null)
                {
                    throw new InvalidOperationException($"Memory recalled a null entry at {i}.");
                }
            }

            telemetry.HistoryTurnsSent = history.Count / 2;
            if (memory is IReportsDroppedTurns reports)
            {
                telemetry.HistoryTurnsDropped = reports.Dropped;
                telemetry.HistoryTurnsForgotten = reports.Forgotten;
            }

            return history;
        }

        // One decision's view of the agent. State is copied, so a context is what the agent knew at
        // one instant. Targets are resolved afresh: the hand-registered ones, then each source's, in
        // order (DR-014). What a source reports dropping is copied into dropped, when given, right
        // after its Collect and before the next source runs, since its list lasts only until then.
        private AgentContext Snapshot(string stimulus, IReadOnlyList<string> observations, List<string> dropped, object correlation)
        {
            var targets = new TargetRegistry();
            var state = new Dictionary<string, object>(State, State.Comparer);
            var ctx = new AgentContext(Identity, stimulus, observations, state, targets, Actions, correlation);

            foreach (string id in Targets.Ids)
            {
                Targets.TryGet(id, out object target);
                targets.Register(id, target, Targets.CategoryOf(id));
            }

            for (int i = 0; i < TargetSources.Count; i++)
            {
                var source = TargetSources[i];
                if (source == null)
                {
                    throw new InvalidOperationException($"TargetSources[{i}] is null.");
                }

                // Handed the context whose registry it is filling, so it sees what came before it.
                source.Collect(ctx, targets);

                if (dropped != null && source is IReportsDroppedTargets reports && reports.Dropped != null)
                {
                    dropped.AddRange(reports.Dropped);
                }
            }

            KeepAllowedCategories(targets);
            return ctx;
        }

        // TargetCategories over the resolved set: with any listed, only targets in one of them stay.
        private void KeepAllowedCategories(TargetRegistry targets)
        {
            HashSet<string> allowed = null;
            foreach (string category in TargetCategories)
            {
                if (!string.IsNullOrWhiteSpace(category))
                {
                    (allowed ??= new HashSet<string>(StringComparer.Ordinal)).Add(category);
                }
            }

            if (allowed == null)
            {
                return;
            }

            // A copy: Ids is a live view of the registry being changed.
            foreach (string id in new List<string>(targets.Ids))
            {
                string category = targets.CategoryOf(id);
                if (category == null || !allowed.Contains(category))
                {
                    targets.Unregister(id);
                }
            }
        }

        // The decision's context with its observations complete: each source's lines, in source
        // order, then the call's own, which ctx already holds. The call's lines are always sent and
        // count against the cap, and the sources fill what is left. What a source returns past it
        // goes into dropped, then what the source reports its own cap left out, so the list is
        // every unsent line in the order the sources ranked them. Each source is read in full and
        // its report copied before the next one runs, since its list lasts only until then.
        private AgentContext Observe(AgentContext ctx, List<string> dropped)
        {
            int room = Math.Max(0, MaxObservations - ctx.Observations.Count);
            var lines = new List<string>();

            for (int i = 0; i < ObservationSources.Count; i++)
            {
                var source = ObservationSources[i];
                if (source == null)
                {
                    throw new InvalidOperationException($"ObservationSources[{i}] is null.");
                }

                // Handed ctx, whose observations are the call's own: a source never sees another's
                // lines, so none depends on the order of the others.
                foreach (string line in NotBlank(source.Observe(ctx)))
                {
                    if (lines.Count < room)
                    {
                        lines.Add(line);
                    }
                    else
                    {
                        dropped.Add(line);
                    }
                }

                if (source is IReportsDroppedObservations reports)
                {
                    dropped.AddRange(NotBlank(reports.Dropped));
                }
            }

            if (lines.Count == 0)
            {
                return ctx;
            }

            lines.AddRange(ctx.Observations);

            // The same state and targets, so everything after this reads one snapshot.
            return new AgentContext(ctx.Identity, ctx.Stimulus, new ReadOnlyCollection<string>(lines),
                ctx.State, ctx.Targets, ctx.Actions, ctx.Correlation);
        }

        // The lines that say something, in order. Enumerated here, in full, so a lazy source runs,
        // and throws, while its decision is being prepared.
        private static List<string> NotBlank(IEnumerable<string> lines)
        {
            var kept = new List<string>();

            if (lines == null)
            {
                return kept;
            }

            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    kept.Add(line);
                }
            }

            return kept;
        }

        private IEnumerable<ActionDefinition> Definitions()
        {
            if (profile == null)
            {
                foreach (var definition in definitions)
                {
                    yield return definition;
                }

                yield break;
            }

            // An empty slot in the profile defines nothing; the profile warns about it already.
            foreach (var asset in profile.Actions)
            {
                if (asset != null)
                {
                    yield return asset.Action;
                }
            }
        }

        // A new decision's telemetry, with what is known before anything else happens.
        private static DecisionTelemetry Telemetry(ILLMProvider provider, DecisionPriority priority)
        {
            return new DecisionTelemetry
            {
                ProviderName = provider.Name ?? "",
                ProviderConfig = ProviderConfig.NameOf(provider),
                FrameworkVersion = AgenerelaInfo.Version,
                Priority = priority,
            };
        }

        // A decision that ended before its request was sent, by a rule the game switched on: the
        // provider's back-off or the agent's backlog (#190). Recorded as a pipeline error, so a game
        // reads it where it reads any decision that did not happen, but logged nowhere: it is what
        // the rule is for, and the telemetry holds the reason. It carries the call's correlation, as
        // every result DecideAsync returns does (DR-018).
        private static DecisionResult NotSent(DecisionTelemetry telemetry, PipelineErrorKind kind, string message, object correlation)
        {
            telemetry.Outcome = DecisionOutcome.PipelineError;
            telemetry.ErrorKind = kind;
            telemetry.ErrorMessage = message;
            return new DecisionResult(null, telemetry, null, null, null, null, correlation);
        }

        // With the request when it was sent, and the reply when one arrived (#142).
        private DecisionResult Failed(
            DecisionTelemetry telemetry, PipelineErrorKind kind, string reason, object correlation,
            DecisionRequest request = null, ProviderResult reply = null)
        {
            string message = $"{Who()} could not decide. {reason}";
            telemetry.Outcome = DecisionOutcome.PipelineError;
            telemetry.ErrorKind = kind;
            telemetry.ErrorMessage = message;
            Debug.LogWarning(message);
            return new DecisionResult(null, telemetry, null, null, request, reply, correlation);
        }

        // A provider says why it failed by throwing a ProviderException, whose kind maps one to
        // one. Any other exception says only that the provider failed.
        private static PipelineErrorKind KindOf(Exception e)
        {
            if (!(e is ProviderException failure))
            {
                return PipelineErrorKind.ProviderFailed;
            }

            switch (failure.Kind)
            {
                case ProviderErrorKind.Unreachable:
                    return PipelineErrorKind.ProviderUnreachable;
                case ProviderErrorKind.ModelNotFound:
                    return PipelineErrorKind.ModelNotFound;
                case ProviderErrorKind.Timeout:
                    return PipelineErrorKind.ProviderTimeout;
                case ProviderErrorKind.Rejected:
                    return PipelineErrorKind.ProviderRejected;
                case ProviderErrorKind.ServerError:
                    return PipelineErrorKind.ProviderServerError;
                case ProviderErrorKind.MalformedResponse:
                    return PipelineErrorKind.ProviderMalformedResponse;
                default:
                    // A kind added to ProviderErrorKind before it has one here.
                    return PipelineErrorKind.ProviderFailed;
            }
        }

        private void Refuse(AgentDecision decision, string reason)
        {
            Debug.LogWarning($"{Who()} did not execute '{decision.ActionId}' on '{decision.TargetId}': {reason}");
        }

        private string Who()
        {
            return string.IsNullOrWhiteSpace(Identity.Name) ? "Agent" : $"Agent '{Identity.Name}'";
        }

        private string Source()
        {
            return profile != null ? $"Profile '{profile.name}'" : "This agent's action list";
        }

        // The reply as the warning quotes it: enough to see what went wrong, never a whole essay.
        private static string Excerpt(string text)
        {
            const int Longest = 200;

            if (text == null)
            {
                return "null";
            }

            return text.Length <= Longest ? $"\"{text}\"" : $"\"{text.Substring(0, Longest)}...\"";
        }
    }
}
