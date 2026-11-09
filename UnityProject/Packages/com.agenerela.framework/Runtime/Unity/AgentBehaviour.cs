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
    /// and cancels the agent's decisions when the GameObject is destroyed. <see cref="ResetAgent"/>
    /// starts the agent afresh without destroying anything. An agent that is not a
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

        /// <summary>
        /// The only actions this agent registers, by id (#190 G10): of the <c>[AgentAction]</c>
        /// methods on this GameObject and the rows of <see cref="Handlers"/>, the rest are left out,
        /// so a body class several agents share can carry actions only some of them take. Empty, the
        /// default, registers every one, as before.
        /// </summary>
        [Tooltip("Only these actions are registered, by id. Leave empty for every [AgentAction] on " +
                 "this GameObject and every row of Handlers.")]
        public string[] OnlyActions;

        /// <summary>
        /// The only target categories this agent is ever offered, whichever source finds them
        /// (<see cref="Agenerela.Agent.TargetCategories"/>, #190 G3). Empty, the default, offers every
        /// target. When <see cref="Targets"/> names no categories of its own, it is given these, so
        /// its cap counts only what this agent may be offered.
        /// </summary>
        [Tooltip("Only targets in these categories are ever offered, from any source. Leave empty " +
                 "for every target. A Targets source with no categories of its own gets these.")]
        public string[] TargetCategories;

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
        /// <see cref="Agenerela.Agent.Reset"/> on this component's agent (#190, G12): starts it afresh,
        /// keeping this GameObject, this component and the agent itself. Its decisions under way or
        /// waiting end cancelled, the decisions it holds for memory are dropped, and its memory is
        /// cleared. Before the agent is built there is nothing to reset, so nothing happens.
        /// </summary>
        /// <remarks>
        /// Not named <c>Reset</c>: Unity calls a component's <c>Reset</c> in the editor, when the
        /// component is added or reset from the Inspector. Replaces destroying and re-adding this
        /// component, which a game had to do before, and which also dropped the guards, sources and
        /// state it had added.
        /// </remarks>
        public void ResetAgent()
        {
            agent?.Reset();
        }

        /// <summary>
        /// Checks what can be seen before Play mode: a missing profile, a row of
        /// <see cref="Handlers"/> that cannot be bound, a malformed <c>[AgentAction]</c> method, an
        /// id in <see cref="OnlyActions"/> that nothing provides or a row of <see cref="Handlers"/> it
        /// leaves out, and an action in the profile with no handler, which is never offered, unless
        /// <see cref="OnlyActions"/> leaves it out. The provider is not
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
            var only = Only();
            var provided = new HashSet<string>(StringComparer.Ordinal);

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
                        provided.Add(definition.Id);
                        if (only != null && !only.Contains(definition.Id))
                        {
                            continue;
                        }

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

                provided.Add(id);
                if (only != null && !only.Contains(id))
                {
                    problem = $"Handlers[{i}] binds the action '{id}', which OnlyActions leaves out, so it is never registered";
                    return false;
                }

                if (!bound.Add(id))
                {
                    problem = $"Handlers[{i}] binds the action '{id}', which already has a handler";
                    return false;
                }
            }

            if (only != null)
            {
                foreach (string id in only)
                {
                    if (!provided.Contains(id))
                    {
                        problem = $"OnlyActions lists '{id}', which no [AgentAction] method on this GameObject and no row of Handlers provides";
                        return false;
                    }
                }
            }

            // An action OnlyActions leaves out is meant to be absent, so its missing handler is not a problem.
            var unbound = new List<string>();
            foreach (string id in listed)
            {
                if (!bound.Contains(id) && (only == null || only.Contains(id)))
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

        // OnlyActions as a set, or null when it lists nothing, which registers every action.
        private HashSet<string> Only()
        {
            var ids = NotBlank(OnlyActions);
            return ids.Count > 0 ? new HashSet<string>(ids, StringComparer.Ordinal) : null;
        }

        private static List<string> NotBlank(string[] values)
        {
            var kept = new List<string>();
            if (values != null)
            {
                foreach (string value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        kept.Add(value);
                    }
                }
            }

            return kept;
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
                var only = Only();

                foreach (var component in GetComponents<MonoBehaviour>())
                {
                    // RegisterMethods refuses an owner with no actions, which most components are.
                    if (component != null && AgentActionReader.Read(component).Count > 0)
                    {
                        built.Actions.RegisterMethods(component, Profile.Actions, only);
                    }
                }

                foreach (var binding in Handlers)
                {
                    if (only == null || (binding.ActionId != null && only.Contains(binding.ActionId)))
                    {
                        built.Bind(binding.ActionId, binding.Handler);
                    }
                }

                var categories = NotBlank(TargetCategories);
                built.TargetCategories.AddRange(categories);

                if (FindTargetsNearby)
                {
                    Targets ??= new ProximityTargetSource();
                    Targets.Origin = Targets.Origin != null ? Targets.Origin : transform;

                    // The agent's filter runs after the source's cap, so the source is given it first.
                    if (categories.Count > 0 && NotBlank(Targets.Categories).Count == 0)
                    {
                        Targets.Categories = categories.ToArray();
                    }

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
