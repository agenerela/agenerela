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
    /// the provider and returns what it chose. <see cref="Execute"/> checks that choice again, on its
    /// own, against the agent as it is at that moment, and only then runs the game's code. The
    /// provider is untrusted (build plan §2.4), and the world can change while the model thinks.
    /// </para>
    /// <para>
    /// The developer decides when to call <see cref="DecideAsync"/> and what to pass: a player's
    /// line, a report each turn, a timer, a perception event. The framework never classifies the
    /// call (DR-008).
    /// </para>
    /// </remarks>
    public sealed class Agent
    {
        private readonly ILLMProvider provider;

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
            Actions = new ActionRegistry();
            Targets = new TargetRegistry();
            TargetSources = new List<ITargetSource>();
            State = new Dictionary<string, object>();
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
        /// The game's own facts about this agent, such as <c>State["isFollowing"] = true</c>. Copied
        /// into <see cref="AgentContext.State"/> for each decision, where the handlers' availability
        /// checks read it. It never reaches the model, which reads
        /// <see cref="DecideOptions.Observations"/> instead.
        /// </summary>
        public Dictionary<string, object> State { get; }

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
        /// Cancels the decision. Passed to the provider, which stops its request; a decision
        /// cancelled at any point has no result.
        /// </param>
        /// <returns>
        /// The decision, or, when there is none to be had, a result whose
        /// <see cref="DecisionResult.Decision"/> is null and whose telemetry's
        /// <see cref="DecisionTelemetry.Outcome"/> is <see cref="DecisionOutcome.PipelineError"/>,
        /// with the reason logged as a warning. That covers anything failing while the request is
        /// prepared, such as a target source offering an id twice or an availability check that
        /// throws; a provider that throws; and a reply that is empty, is not one JSON object, or
        /// lacks a field the schema asked for. A reply naming an action or target the request did
        /// not offer is still a decision, for <see cref="Execute"/> to refuse (see
        /// <c>DecisionParser</c>).
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
        /// The pipeline, in order: a context from the identity, a copy of <see cref="State"/>, the
        /// targets resolved now, the stimulus and the observations; the schema from the actions
        /// available now (#7, #8); the examples (#10); the request (#16); the provider (#12); the
        /// parse. Telemetry records the latency of the provider's request on a monotonic clock, the
        /// provider's name, the framework version and the token counts the provider reported.
        /// </remarks>
        public Awaitable<DecisionResult> DecideAsync(string stimulus, DecideOptions options = null, CancellationToken ct = default)
        {
            options = options ?? new DecideOptions();

            // Checked here, outside the async body, so the mistake throws at the call rather than
            // becoming a PipelineError.
            if (string.IsNullOrWhiteSpace(options.StimulusLabel))
            {
                throw new ArgumentException(
                    "DecideOptions.StimulusLabel is empty. It labels the stimulus; the default is \"Player\".",
                    nameof(options));
            }

            // Copied now, so changing the options after the call changes nothing.
            var observations = new ReadOnlyCollection<string>(
                options.Observations == null ? new List<string>() : new List<string>(options.Observations));
            string idleExample = options.IdleExampleStimulus ?? (profile != null ? profile.IdleExampleStimulus : null);

            return Decide(stimulus, options.StimulusLabel, idleExample, observations, ct);
        }

        /// <summary>
        /// Carries out a decision: checks it again against the agent as it is now, then runs the
        /// bound handler. Returns whether the decision was carried out.
        /// </summary>
        /// <returns>
        /// True when the handler ran, or when the decision was <see cref="ActionRegistry.None"/>,
        /// which runs no handler. False, with no handler run, for a result with no decision (a
        /// pipeline error, already logged), and for a decision the check refuses, logged as a
        /// warning saying why.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The check (hard rule 5): the action is registered, and available right now, recomputed
        /// here rather than taken from the request it was chosen from; the target is registered
        /// right now, or is <see cref="TargetRegistry.NoTarget"/>. The targets are resolved again
        /// for it, so one a source no longer offers is refused. Phase 3 makes this check the first
        /// guard of a pipeline that runs inside this call, and adds the rule that an action needing
        /// a target gets a real one.
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

            if (decision.ActionId == ActionRegistry.None)
            {
                return true;
            }

            if (!Actions.TryGet(decision.ActionId, out var definition, out var handler))
            {
                return Refuse(decision, "the action is not registered.");
            }

            var ctx = Snapshot("", Array.Empty<string>());

            if (decision.TargetId != TargetRegistry.NoTarget && !ctx.Targets.Contains(decision.TargetId))
            {
                return Refuse(decision, "the target is not registered.");
            }

            if (!IsAvailable(definition, ctx))
            {
                return Refuse(decision, "the action is not available right now.");
            }

            handler.Execute(ctx, decision);
            return true;
        }

        private async Awaitable<DecisionResult> Decide(
            string stimulus, string label, string idleExample, IReadOnlyList<string> observations, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var telemetry = new DecisionTelemetry
            {
                ProviderName = provider.Name ?? "",
                FrameworkVersion = AgenerelaInfo.Version,
            };

            // TargetsDropped is not filled: ITargetSource has no way to say what a cap left out.
            // ProximityTargetSource.Dropped has it, for a developer to copy in.
            DecisionRequest request;
            try
            {
                var ctx = Snapshot(stimulus, observations);
                var options = new PromptOptions { StimulusLabel = label, IdleExampleStimulus = idleExample };

                // Asked once, so the action enum and the examples come from one answer even when an
                // availability check is not a pure function of the context, such as a cooldown.
                var available = ActionAvailability.For(ctx);
                var schema = DecisionSchema.Build(ctx, available);
                string fewShot = FewShotBuilder.Build(available, ctx.Targets, label, idleExample);

                // The memory slot (build plan §2.9): Phase 2's IMemoryStrategy recalls the turns
                // that go here, oldest first. Until then there are none.
                request = PromptBuilder.Build(ctx, schema, fewShot, history: null, options);
            }
            catch (Exception e)
            {
                return Failed(telemetry, $"Preparing the request failed. {e.GetType().Name}: {e.Message}");
            }

            ProviderResult reply;
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
                return Failed(telemetry, $"The provider failed. {e.GetType().Name}: {e.Message}");
            }

            telemetry.LatencySeconds = (float)clock.Elapsed.TotalSeconds;

            // Also for a provider that finished without noticing the cancellation.
            ct.ThrowIfCancellationRequested();

            if (reply == null)
            {
                return Failed(telemetry, "The provider returned no result.");
            }

            telemetry.PromptTokens = reply.PromptTokens;
            telemetry.CompletionTokens = reply.CompletionTokens;

            if (!DecisionParser.TryParse(reply.Text, request.Schema, out var decision, out string problem))
            {
                return Failed(telemetry, $"{problem} The reply was: {Excerpt(reply.Text)}");
            }

            return new DecisionResult(decision, telemetry);
        }

        // One decision's view of the agent. State is copied, so a context is what the agent knew at
        // one instant. Targets are resolved afresh: the hand-registered ones, then each source's, in
        // order (DR-014).
        private AgentContext Snapshot(string stimulus, IReadOnlyList<string> observations)
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
            }

            return ctx;
        }

        // Asks ActionAvailability, the same rule the schema was built by, rather than the handler
        // alone, so the two can never disagree about what "available" means.
        private static bool IsAvailable(ActionDefinition definition, AgentContext ctx)
        {
            foreach (var available in ActionAvailability.For(ctx))
            {
                if (available == definition)
                {
                    return true;
                }
            }

            return false;
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

        private DecisionResult Failed(DecisionTelemetry telemetry, string reason)
        {
            telemetry.Outcome = DecisionOutcome.PipelineError;
            Debug.LogWarning($"{Who()} could not decide. {reason}");
            return new DecisionResult(null, telemetry);
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
