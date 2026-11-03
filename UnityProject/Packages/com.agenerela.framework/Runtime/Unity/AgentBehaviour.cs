using System;
using System.Collections.Generic;
using System.Threading;
using Agenerela.Providers;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// An agent on a GameObject, with no manager object in the scene (pattern A in the workflow
    /// page): drop it on the guard and assign a <see cref="Profile"/>. It builds the
    /// <see cref="Agenerela.Agent"/>, registers the <c>[AgentAction]</c> methods on this GameObject,
    /// binds the listed <see cref="Handlers"/>, finds targets and observations around this transform,
    /// and cancels the agent's decisions when the GameObject is destroyed. An agent that is not a
    /// thing in the world owns a plain <see cref="Agenerela.Agent"/> instead (DR-011).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fields are read once, when the agent is built: changing them afterwards changes nothing.
    /// Change a built agent through <see cref="Agent"/>, which also has <see cref="Agenerela.Agent.Execute"/>,
    /// <see cref="Agenerela.Agent.State"/> and <see cref="Agenerela.Agent.Guards"/>.
    /// </para>
    /// <para>
    /// <b>When it is built.</b> In <c>Awake</c>, or by the first read of <see cref="Agent"/> or call
    /// of <see cref="DecideAsync"/> if that comes first, so a sibling's <c>Awake</c> can use it
    /// whatever the script order, and code can add this component and then set <see cref="Profile"/>.
    /// That is also why <c>Awake</c> stays silent about a missing profile, which the Inspector warns
    /// about instead. Anything else wrong is logged as an error from <c>Awake</c>.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AgentBehaviour : MonoBehaviour
    {
        /// <summary>One row of <see cref="Handlers"/>: the scene component that runs an action of the profile's.</summary>
        [Serializable]
        public struct ActionHandlerBinding
        {
            [Tooltip("The id of an action in the profile.")]
            public string ActionId;

            [Tooltip("The component that runs it. It does not have to be on this GameObject.")]
            public ActionHandlerBehaviour Handler;
        }

        [Tooltip("Who this agent is and what it can do. Ten guards can share one profile.")]
        public AgentProfile Profile;

        [Tooltip("The provider this agent asks. Leave empty for the default in Project Settings → Agenerela.")]
        public ProviderConfig Provider;

        [Tooltip("Offer the Targetables around this agent as targets, using the settings below.")]
        public bool FindTargetsNearby = true;

        [Tooltip("Which Targetables are offered. An empty Origin means this transform.")]
        public ProximityTargetSource Targets = new ProximityTargetSource();

        [Tooltip("Tell the agent the descriptions of the Targetables around it, using the settings below.")]
        public bool NoticeNearby = true;

        [Tooltip("Which Targetables are described. An empty Origin means this transform.")]
        public ProximityObservationSource Observations = new ProximityObservationSource();

        [Tooltip("Handlers for actions the profile lists that no [AgentAction] method on this GameObject runs.")]
        public List<ActionHandlerBinding> Handlers = new List<ActionHandlerBinding>();

        private Agent agent;
        private bool warningQueued;

        /// <summary>
        /// The agent this component built, the same one every time. Built by this read if
        /// <c>Awake</c> has not run yet.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The agent cannot be built: the message says why. Nothing is kept, so the next read tries
        /// again.
        /// </exception>
        public Agent Agent
        {
            get
            {
                if (agent == null)
                {
                    agent = Build();
                }

                return agent;
            }
        }

        /// <summary>
        /// <see cref="Agenerela.Agent.DecideAsync"/>, with the token that this GameObject's
        /// destruction cancels: a destroyed guard's decision ends cancelled, and the provider stops
        /// its request, rather than running on. So does a call made after the destruction.
        /// </summary>
        /// <remarks>
        /// Unity raises that token only for a component that woke, so a decision asked of a guard
        /// whose GameObject has never been active is not cancelled by destroying it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The agent cannot be built, as for <see cref="Agent"/>.</exception>
        public Awaitable<DecisionResult> DecideAsync(string stimulus, DecideOptions options = null)
        {
            // Unity's null: destroyCancellationToken itself throws once the component is destroyed.
            var ct = this != null ? destroyCancellationToken : new CancellationToken(true);
            return Agent.DecideAsync(stimulus, options, ct);
        }

        /// <summary>
        /// Checks what can be seen before Play mode: a missing profile, a row of
        /// <see cref="Handlers"/> that cannot be bound, a malformed <c>[AgentAction]</c> method, and
        /// an action in the profile with no handler, which is never offered. The provider is not
        /// checked: an empty one is the normal case, and its default is read when the agent is built.
        /// </summary>
        public bool Validate(out string problem)
        {
            problem = null;

            if (Profile == null)
            {
                problem = "Profile is empty, so there is no agent to build";
                return false;
            }

            var bound = new HashSet<string>(StringComparer.Ordinal);

            foreach (var component in GetComponents<MonoBehaviour>())
            {
                // A component whose script is missing comes back as null.
                if (component == null)
                {
                    continue;
                }

                try
                {
                    foreach (var (definition, _) in AgentActionReader.Read(component))
                    {
                        if (!bound.Add(definition.Id))
                        {
                            problem = $"{component.GetType().Name} declares the action '{definition.Id}', which another component on this GameObject also declares";
                            return false;
                        }
                    }
                }
                catch (ArgumentException e)
                {
                    problem = $"{component.GetType().Name}: {e.Message}";
                    return false;
                }
            }

            var listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in Profile.Actions)
            {
                if (asset != null && asset.Action != null && !string.IsNullOrWhiteSpace(asset.Action.Id))
                {
                    listed.Add(asset.Action.Id);
                }
            }

            for (int i = 0; i < Handlers.Count; i++)
            {
                string id = Handlers[i].ActionId;

                if (Handlers[i].Handler == null)
                {
                    problem = $"Handlers[{i}] ('{id}') has no handler";
                    return false;
                }

                if (id == null || !listed.Contains(id))
                {
                    problem = $"Handlers[{i}] names the action '{id}', which profile '{Profile.name}' does not list";
                    return false;
                }

                if (!bound.Add(id))
                {
                    problem = $"Handlers[{i}] binds the action '{id}', which already has a handler";
                    return false;
                }
            }

            var unbound = new List<string>();
            foreach (string id in listed)
            {
                if (!bound.Contains(id))
                {
                    unbound.Add(id);
                }
            }

            if (unbound.Count > 0)
            {
                problem = $"No handler for {string.Join(", ", unbound)}: an action nobody binds is never offered";
                return false;
            }

            return true;
        }

        private void Awake()
        {
            if (Profile == null)
            {
                return;
            }

            try
            {
                _ = Agent;
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError(e.Message, this);
            }
        }

        // Unity may call this on its loading thread, where GetComponents is not allowed, so the
        // check waits for the editor's main thread. Several calls before then make one check.
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (warningQueued)
            {
                return;
            }

            warningQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                warningQueued = false;
                WarnIfInvalid();
            };
#endif
        }

        // Not during Play, where code adding this component would hear about the profile it is
        // about to set. Destroyed is Unity's null: the object can go before the check comes.
        internal void WarnIfInvalid()
        {
            if (this != null && !Application.isPlaying && !Validate(out string problem))
            {
                Debug.LogWarning($"{name}: {problem}", this);
            }
        }

        // The whole agent or an exception, never half of one: it is only kept once this returns.
        private Agent Build()
        {
            if (this == null)
            {
                throw new InvalidOperationException("This AgentBehaviour was destroyed before it built its agent.");
            }

            if (Profile == null)
            {
                throw new InvalidOperationException($"AgentBehaviour on '{name}' has no Profile. Assign one before using its agent.");
            }

            try
            {
                var built = new Agent(Profile, ProviderConfig.Resolve(Provider).Provider);

                foreach (var component in GetComponents<MonoBehaviour>())
                {
                    // RegisterMethods refuses an owner with no actions, which most components are.
                    if (component != null && AgentActionReader.Read(component).Count > 0)
                    {
                        built.Actions.RegisterMethods(component, Profile.Actions);
                    }
                }

                foreach (var binding in Handlers)
                {
                    built.Bind(binding.ActionId, binding.Handler);
                }

                if (FindTargetsNearby)
                {
                    Targets ??= new ProximityTargetSource();
                    Targets.Origin = Targets.Origin != null ? Targets.Origin : transform;
                    built.TargetSources.Add(Targets);
                }

                if (NoticeNearby)
                {
                    Observations ??= new ProximityObservationSource();
                    Observations.Origin = Observations.Origin != null ? Observations.Origin : transform;
                    built.ObservationSources.Add(Observations);
                }

                return built;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"AgentBehaviour on '{name}' could not build its agent. {e.Message}", e);
            }
        }
    }
}
