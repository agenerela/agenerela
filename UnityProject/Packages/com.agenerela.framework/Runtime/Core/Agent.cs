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
        // The only guard Execute runs again: legality is what can change between deciding and
        // acting. Grounding judged the reply, which Execute does not have.
        private static readonly SchemaLegalityGuard Legality = new SchemaLegalityGuard();

        // The built-in guards, in the order §2.5 fixes. They run before Guards and are never in it.
        private static readonly IDecisionGuard[] BuiltInGuards = { Legality, new TargetGroundingGuard() };
        private static readonly IDecisionGuard[] ExecuteGuards = { Legality };

        private readonly ILLMProvider provider;

        // The provider's queue, found by the provider instance, so every agent given that
        // instance shares it (DR-013, DR-015).
        private readonly RequestScheduler scheduler;

        // Where Bind finds definitions: the profile, read when Bind is called, or the list a
        // code-only agent was given. Exactly one is set.
        private readonly AgentProfile profile;
        private readonly List<ActionDefinition> definitions;

        /// <summary>
        /// An agent built from a profile, the asset a developer creates first (#15). It keeps the
        /// profile's identity object, so editing that identity's fields changes every agent built
        /// from the profile, but assigning the profile a different identity later does not. The
        /// idle example is read from the profile on every call. Its actions are registered with
        /// <see cref="Bind"/>.
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
        /// them. It has no idle example unless a call passes one in
        /// <see cref="DecideOptions.IdleExampleStimulus"/>.
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
            scheduler = RequestScheduler.For(provider);
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
        /// An id offered twice, by <see cref="Targets"/> and a source or by two sources, makes the
        /// decision a <see cref="DecisionOutcome.PipelineError"/>: two objects answering to one id
        /// cannot both be offered (DR-014).
        /// </remarks>
        public List<ITargetSource> TargetSources { get; }

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
        /// Guards that run after the built-in ones, in list order, inside <see cref="DecideAsync"/>
        /// only. Empty by default. Internal until #108 makes it the developer's list.
        /// </summary>
        internal List<IDecisionGuard> Guards { get; } = new List<IDecisionGuard>();

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
        /// availability check that throws; a provider that throws; and a reply that is empty, is not one JSON object, or
        /// lacks a field the schema asked for. A reply naming an action or target the request did
        /// not offer is still a decision, which the guards reject (see <c>DecisionParser</c> and
        /// <see cref="SchemaLegalityGuard"/>). The telemetry says which of these it was in
        /// <see cref="DecisionTelemetry.ErrorKind"/>, from the <see cref="ProviderException.Kind"/>
        /// when a provider threw one, and holds the warning in
        /// <see cref="DecisionTelemetry.ErrorMessage"/> (#87).
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <see cref="DecideOptions.StimulusLabel"/> is null, empty or whitespace. Thrown by the call
        /// itself, before anything is sent: it is a mistake in the calling code, not an event.
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
        /// <see cref="SchemaLegalityGuard"/>, then the grounding guard (#105), then the developer's
        /// (#108). The first that does not pass stops them, and the result's
        /// <see cref="DecisionResult.Decision"/> becomes <see cref="ActionRegistry.None"/> with
        /// <see cref="TargetRegistry.NoTarget"/>, keeping the model's statement until #109 decides
        /// what to say instead. The model's own answer stays on
        /// <see cref="DecisionResult.ModelDecision"/>, the verdict on
        /// <see cref="DecisionResult.Verdict"/>, and the guard's name, the verdict's kind and its
        /// reason in <see cref="DecisionTelemetry.GuardsFired"/>,
        /// <see cref="DecisionTelemetry.Verdict"/> and <see cref="DecisionTelemetry.GuardReason"/>.
        /// A <see cref="GuardVerdict.Reject"/>, the provider breaking the contract, is logged as a
        /// warning; a <see cref="GuardVerdict.RewriteToNone"/>, a contained refusal, is not.
        /// </para>
        /// <para>
        /// <see cref="Memory"/> is recalled while the request is prepared, into
        /// <see cref="DecisionRequest.History"/>, and so fails as preparing it does, including on a
        /// history that is not whole exchanges. The decision is remembered just before it is
        /// returned, as the guards left it, never as the model answered: whitespace or extra fields
        /// in the reply are not remembered, and neither is an action a guard stopped. A pipeline
        /// error or a cancelled call remembers nothing; a <see cref="ActionRegistry.None"/>
        /// decision is remembered, because it happened. If remembering throws, the warning is
        /// logged and the decision still returned. Remembering happens on the main thread, even
        /// when the provider answered on another.
        /// </para>
        /// <para>
        /// Every agent given the same provider instance shares one queue, which sends at most one
        /// request at a time: player-facing ones first, then background ones, each in the order
        /// they were made (<see cref="DecideOptions.Priority"/>, DR-015). Agents on different
        /// provider instances never wait on each other. A failed request frees its place like an
        /// answered one, and nothing is retried.
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

            // A list of the context's own, apart from the options' copy a guard may read. Blank
            // lines are not something the agent knows, so they take no place under the cap.
            var observations = new ReadOnlyCollection<string>(NotBlank(options.Observations));
            string idleExample = options.IdleExampleStimulus ?? (profile != null ? profile.IdleExampleStimulus : null);

            return Decide(stimulus, options, idleExample, observations, ct);
        }

        /// <summary>
        /// Carries out a decision: checks it again against the agent as it is now, then runs the
        /// bound handler. Returns whether the decision was carried out.
        /// </summary>
        /// <returns>
        /// True when the handler ran, or when the decision was <see cref="ActionRegistry.None"/>,
        /// which runs no handler: a decision a guard rewrote to <c>none</c> included. False, with no
        /// handler run, for a result with no decision (a pipeline error, already logged), for one a
        /// guard rejected inside <see cref="DecideAsync"/> (already logged), and for a decision the
        /// check refuses, logged as a warning saying why.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The check (hard rule 5) is <see cref="SchemaLegalityGuard"/>, run again against the agent
        /// as it is right now: the action is registered; the target is registered, or is
        /// <see cref="TargetRegistry.NoTarget"/>; an action that needs a target got a real one; and
        /// the action is available, recomputed here rather than taken from the request it was
        /// chosen from. The targets are resolved again for it, so one a source no longer offers is
        /// refused. No other guard runs again: grounding judged the reply, which this call does not
        /// have. A refusal is added to the result's telemetry, as
        /// <c>"SchemaLegalityGuard@Execute"</c> in <see cref="DecisionTelemetry.GuardsFired"/>, with
        /// <see cref="DecisionTelemetry.Verdict"/> and <see cref="DecisionTelemetry.GuardReason"/>;
        /// <see cref="DecisionResult.Verdict"/> stays what <see cref="DecideAsync"/> made it.
        /// </para>
        /// <para>
        /// A refusal returns false rather than throwing: the provider is untrusted, so an answer to
        /// refuse is an expected event, not a mistake in the calling code. Exceptions thrown by the
        /// developer's own code, a handler, an availability check or a target source, propagate.
        /// The handler gets a context built now, holding the current state and targets; it has no
        /// stimulus or observations, which were the inputs to deciding, not to acting.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        public bool Execute(DecisionResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var decision = result.Decision;
            if (decision == null || result.Telemetry.Outcome == DecisionOutcome.PipelineError)
            {
                return false;
            }

            // Its Decision is none, but the provider broke the contract, and DecideAsync said so.
            if (result.Verdict.HasValue && result.Verdict.Value.Kind == GuardVerdictKind.Reject)
            {
                return false;
            }

            if (decision.ActionId == ActionRegistry.None)
            {
                return true;
            }

            // No dropped targets to fill: this Collect overwrites a source's Dropped, but the result
            // already holds its own copy from the decision.
            var ctx = Snapshot("", Array.Empty<string>(), dropped: null);

            // No request and no reply: only the agent as it is now. The options are the defaults,
            // and no guard that runs here reads them.
            var input = new GuardInput(ctx, null, null, decision, new DecideOptions(), result.Telemetry);
            var verdict = GuardPipeline.Run(ExecuteGuards, input, "@Execute");
            if (verdict.Kind != GuardVerdictKind.Pass)
            {
                return Refuse(decision, verdict.Reason);
            }

            ctx.Actions.TryGet(decision.ActionId, out _, out var handler);
            handler.Execute(ctx, decision);
            return true;
        }

        private async Awaitable<DecisionResult> Decide(
            string stimulus, DecideOptions call, string idleExample, IReadOnlyList<string> observations,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string label = call.StimulusLabel;
            var priority = call.Priority;
            var memory = Memory;
            var telemetry = new DecisionTelemetry
            {
                ProviderName = provider.Name ?? "",
                ProviderConfig = ProviderConfig.NameOf(provider),
                FrameworkVersion = AgenerelaInfo.Version,
                Priority = priority,
            };

            AgentContext ctx;
            DecisionRequest request;
            try
            {
                ctx = Observe(Snapshot(stimulus, observations, telemetry.TargetsDropped), telemetry.ObservationsDropped);
                var options = new PromptOptions { StimulusLabel = label, IdleExampleStimulus = idleExample };

                // Asked once, so the action enum and the examples come from one answer even when an
                // availability check is not a pure function of the context, such as a cooldown.
                var available = ActionAvailability.For(ctx);
                var schema = DecisionSchema.Build(ctx, available);
                string fewShot = FewShotBuilder.Build(available, ctx.Targets, label, idleExample);

                var history = Recall(memory, ctx, telemetry);
                request = PromptBuilder.Build(ctx, schema, fewShot, history, options);
            }
            catch (Exception e)
            {
                return Failed(telemetry, PipelineErrorKind.RequestPreparation,
                    $"Preparing the request failed. {e.GetType().Name}: {e.Message}");
            }

            ProviderResult reply;

            // The provider's queue, shared with every agent on this provider instance (DR-015).
            // Disposing the ticket frees the slot for the next request, whether the provider
            // answered, failed or was cancelled.
            var waited = System.Diagnostics.Stopwatch.StartNew();
            using (var ticket = scheduler.Enqueue(priority, ct))
            {
                telemetry.QueuedAhead = ticket.QueuedAhead;

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
                // send, so the two cover the whole call between them: LatencySeconds is the
                // provider's time alone and a long queue shows in QueueWaitSeconds instead.
                telemetry.QueueWaitSeconds = (float)waited.Elapsed.TotalSeconds;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    reply = await provider.RequestAsync(request, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception) when (ct.IsCancellationRequested)
                {
                    // A provider that reports the caller's cancellation as some other exception, as an
                    // aborted web request tends to, still ends the call cancelled rather than counting
                    // as a pipeline error.
                    throw new OperationCanceledException(ct);
                }
                catch (Exception e)
                {
                    telemetry.LatencySeconds = (float)clock.Elapsed.TotalSeconds;
                    return Failed(telemetry, KindOf(e), $"The provider failed. {e.GetType().Name}: {e.Message}");
                }

                telemetry.LatencySeconds = (float)clock.Elapsed.TotalSeconds;
            }

            // Back on the main thread, as for the send: a provider that answered on a worker thread
            // resumed this code there, and the memory below is no more thread-safe than the rest of
            // the agent. Free when already there.
            await Awaitable.MainThreadAsync();

            // Also for a provider that finished without noticing the cancellation.
            ct.ThrowIfCancellationRequested();

            if (reply == null)
            {
                return Failed(telemetry, PipelineErrorKind.ProviderFailed, "The provider returned no result.");
            }

            telemetry.Model = reply.Model ?? "";
            telemetry.PromptTokens = reply.PromptTokens;
            telemetry.CompletionTokens = reply.CompletionTokens;

            if (!DecisionParser.TryParse(reply.Text, request.Schema, out var decision, out string problem))
            {
                return Failed(telemetry, PipelineErrorKind.UnreadableReply, $"{problem} The reply was: {Excerpt(reply.Text)}");
            }

            var result = Guard(new GuardInput(ctx, request, reply, decision, call, telemetry));

            // After the guards, so what is remembered is what the agent will do (§2.9, rule 1).
            if (memory != null)
            {
                try
                {
                    memory.Remember(ctx, request, result);
                }
                catch (Exception e)
                {
                    // The decision was made; a memory that cannot record it does not undo it.
                    Debug.LogWarning($"{Who()} decided, but its memory could not remember it. {e.GetType().Name}: {e.Message}");
                }
            }

            return result;
        }

        // The guards over one parsed decision: the built-in ones, then Guards. A decision none of
        // them stopped is returned as it is; one they stopped becomes none, beside the model's own.
        private DecisionResult Guard(GuardInput input)
        {
            var verdict = GuardPipeline.Run(AllGuards(), input);
            var decision = input.Decision;

            if (verdict.Kind == GuardVerdictKind.Pass)
            {
                return new DecisionResult(decision, input.Telemetry);
            }

            if (verdict.Kind == GuardVerdictKind.Reject)
            {
                // Once, here: Execute returns false for it without saying so again.
                Debug.LogWarning($"{Who()} will do nothing: {verdict.Guard} rejected '{decision.ActionId}' on '{decision.TargetId}': {verdict.Reason}");
            }

            // The statement is the model's until #109 decides what an agent says after a refusal.
            var none = new AgentDecision(ActionRegistry.None, TargetRegistry.NoTarget, decision.Statement);
            return new DecisionResult(none, input.Telemetry, decision, verdict);
        }

        private IEnumerable<IDecisionGuard> AllGuards()
        {
            foreach (var guard in BuiltInGuards)
            {
                yield return guard;
            }

            // A copy, so a guard that edits the list while it runs cannot break the loop.
            foreach (var guard in Guards.ToArray())
            {
                yield return guard;
            }
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
        private AgentContext Snapshot(string stimulus, IReadOnlyList<string> observations, List<string> dropped)
        {
            var targets = new TargetRegistry();
            var state = new Dictionary<string, object>(State, State.Comparer);
            var ctx = new AgentContext(Identity, stimulus, observations, state, targets, Actions);

            foreach (string id in Targets.Ids)
            {
                Targets.TryGet(id, out object target);
                targets.Register(id, target);
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

            return ctx;
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
                ctx.State, ctx.Targets, ctx.Actions);
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

        private DecisionResult Failed(DecisionTelemetry telemetry, PipelineErrorKind kind, string reason)
        {
            string message = $"{Who()} could not decide. {reason}";
            telemetry.Outcome = DecisionOutcome.PipelineError;
            telemetry.ErrorKind = kind;
            telemetry.ErrorMessage = message;
            Debug.LogWarning(message);
            return new DecisionResult(null, telemetry);
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

        private bool Refuse(AgentDecision decision, string reason)
        {
            Debug.LogWarning($"{Who()} did not execute '{decision.ActionId}' on '{decision.TargetId}': {reason}");
            return false;
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
