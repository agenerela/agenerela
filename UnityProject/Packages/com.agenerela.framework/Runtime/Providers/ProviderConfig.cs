using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// A provider chosen once, as an asset, rather than built in every agent's code (DR-013): which
    /// backend, which model, where, and how many requests it takes at once. Several can coexist in
    /// a project, such as <c>OllamaLocal</c> and <c>GeminiFlash</c>, and Project Settings → Agenerela
    /// names the project default. <see cref="OllamaProviderConfig"/> is the one the framework ships.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One provider instance per config.</b> <see cref="Provider"/> builds it the first time it
    /// is read and hands back that same instance afterwards, so every agent on one config shares one
    /// provider, and so one queue (#89). <see cref="MaxInFlight"/> is that queue's limit.
    /// </para>
    /// <para>
    /// <b>Resolution</b> is the agent's own config, then the project default:
    /// <see cref="Resolve"/>. DR-013's middle level, a scene override, is left out until a demo
    /// needs one. Telemetry records which config resolved, in
    /// <see cref="DecisionTelemetry.ProviderConfig"/>, because a value that can come from several
    /// places must say where it came from.
    /// </para>
    /// <para>
    /// <b>Edits.</b> In the Editor outside Play mode, changing a field in the Inspector drops the
    /// built provider, so the next read of <see cref="Provider"/> builds one with the new values.
    /// During Play, a new <see cref="MaxInFlight"/> reaches the live queue at once, and every other
    /// field waits for the next time Play mode is entered: rebuilding mid-Play would leave agents
    /// built earlier on the old instance and agents built later on a new one, two queues for one
    /// config. The provider is dropped as Play mode starts and as it ends, so a project that enters
    /// Play mode without reloading scripts never keeps an old one. In a built player, the config's
    /// values are fixed and the provider is built once.
    /// </para>
    /// <para>
    /// <b>Adding a provider config</b> for another backend: derive from this class, add the
    /// backend's settings as serialized fields with <c>[Tooltip]</c>s and a
    /// <c>[CreateAssetMenu(menuName = "Agenerela/... Provider")]</c>, and override
    /// <see cref="CreateProvider"/> to build its <see cref="ILLMProvider"/> from them. The base
    /// does the rest: it caches the instance, applies <see cref="MaxInFlight"/>, and records the
    /// config's name for telemetry. A subclass that needs <c>OnValidate</c> overrides
    /// <see cref="OnValidate"/> and calls the base.
    /// </para>
    /// <para>
    /// <b>API keys are never serialized</b> into a config (hard rule 1): an asset is committed and
    /// shipped inside the build. A config for a backend that needs a key serializes where to read
    /// it from, such as the name of an environment variable, and reads the key itself in
    /// <see cref="CreateProvider"/>. Nor may a key appear in an exception message.
    /// </para>
    /// <para>
    /// Read <see cref="Provider"/> and call <see cref="Resolve"/> from the main thread: both read
    /// Unity objects.
    /// </para>
    /// </remarks>
    public abstract class ProviderConfig : ScriptableObject
    {
        /// <summary>
        /// What <see cref="Resolve"/> throws when there is nothing to resolve: what is missing, and
        /// how to make it.
        /// </summary>
        internal const string NoConfigMessage =
            "No provider config: the agent has none of its own, and the project has no default. " +
            "Create one with Create → Agenerela → Ollama Provider, and set it in Project Settings → Agenerela.";

        // The config each built provider came from, by name, so Agent can record it however the
        // provider reached it. Keyed by the instance and held weakly, as the scheduler's table is.
        private static readonly ConditionalWeakTable<ILLMProvider, string> names =
            new ConditionalWeakTable<ILLMProvider, string>();

        [SerializeField, Min(1)]
        [Tooltip("How many requests may be with the backend at once. Every agent on this config " +
                 "shares one queue, which sends player-facing requests first. 1 is what the " +
                 "prototype measured safe on one GPU; during Play, a change applies at once.")]
        private int maxInFlight = 1;

        [NonSerialized]
        private ILLMProvider provider;

        /// <summary>
        /// How many requests the queue in front of this config's provider lets through at once (#89).
        /// 1 by default. Set in the Inspector.
        /// </summary>
        public int MaxInFlight => maxInFlight;

        /// <summary>
        /// The provider this config describes, built the first time it is read and the same instance
        /// every time after, so every agent given it shares one queue.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <see cref="MaxInFlight"/> is below 1, the config's settings are not valid (the message
        /// names the config and the setting), or <see cref="CreateProvider"/> returned null.
        /// </exception>
        public ILLMProvider Provider
        {
            get
            {
                if (provider != null)
                {
                    return provider;
                }

                if (maxInFlight < 1)
                {
                    throw new InvalidOperationException(
                        $"Provider config '{name}' has MaxInFlight {maxInFlight}. It must be 1 or more.");
                }

                ILLMProvider built;
                try
                {
                    built = CreateProvider();
                }
                catch (ArgumentException e)
                {
                    throw new InvalidOperationException($"Provider config '{name}' is not valid. {e.Message}", e);
                }

                if (built == null)
                {
                    throw new InvalidOperationException($"{GetType().Name}.CreateProvider returned null for '{name}'.");
                }

                RequestScheduler.For(built).MaxInFlight = maxInFlight;
                names.Remove(built);
                names.Add(built, name);
                provider = built;
#if UNITY_EDITOR
                Built.Add(this);
#endif
                return provider;
            }
        }

        /// <summary>
        /// The config an agent uses: <paramref name="own"/> when it is set, otherwise the project
        /// default named in Project Settings → Agenerela. A code-built agent uses it as
        /// <c>new Agent(profile, ProviderConfig.Resolve().Provider)</c>.
        /// </summary>
        /// <param name="own">The agent's own config, or null for none.</param>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="own"/> is null and there is no project default. The message says how to
        /// make one.
        /// </exception>
        public static ProviderConfig Resolve(ProviderConfig own = null)
        {
            // Unity's null: a destroyed config counts as none.
            if (own != null)
            {
                return own;
            }

            var settings = AgenerelaSettings.Load();
            var fallback = settings != null ? settings.DefaultProvider : null;
            if (fallback != null)
            {
                return fallback;
            }

            throw new InvalidOperationException(NoConfigMessage);
        }

        /// <summary>
        /// Builds the provider from this config's fields. Called once, the first time
        /// <see cref="Provider"/> is read, and again only after an edit outside Play mode has
        /// dropped the last one. Return a new instance each time, never one shared with anything
        /// else: its queue is this config's.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// A setting is not valid. <see cref="Provider"/> reports it with the config's name.
        /// </exception>
        protected abstract ILLMProvider CreateProvider();

        /// <summary>
        /// Called by Unity when a field changes in the Inspector. Outside Play mode it drops the built
        /// provider; during Play it sends a new <see cref="MaxInFlight"/> to the live queue. An
        /// override calls this.
        /// </summary>
        protected virtual void OnValidate()
        {
            Changed(Application.isPlaying);
        }

        // What an edit does, with Play mode passed in so EditMode tests can take both branches.
        internal void Changed(bool playing)
        {
            if (provider == null)
            {
                return;
            }

            if (!playing)
            {
                Forget();
            }
            else if (maxInFlight >= 1)
            {
                RequestScheduler.For(provider).MaxInFlight = maxInFlight;
            }
        }

        /// <summary>
        /// The name of the config <paramref name="provider"/> was built by, or empty when it was built
        /// in code. What <see cref="DecisionTelemetry.ProviderConfig"/> records.
        /// </summary>
        internal static string NameOf(ILLMProvider provider)
        {
            return provider != null && names.TryGetValue(provider, out string configName) ? configName : "";
        }

        // Lets the next read of Provider build a new one. Agents holding the old one keep it.
        internal void Forget()
        {
            provider = null;
        }

#if UNITY_EDITOR
        // Every config holding a built provider, so a change of Play mode can drop them all.
        private static readonly HashSet<ProviderConfig> Built = new HashSet<ProviderConfig>();

        [UnityEditor.InitializeOnLoadMethod]
        private static void WatchPlayMode()
        {
            UnityEditor.EditorApplication.playModeStateChanged += change =>
            {
                if (change == UnityEditor.PlayModeStateChange.ExitingEditMode
                    || change == UnityEditor.PlayModeStateChange.EnteredEditMode)
                {
                    ForgetAll();
                }
            };
        }

        // Drops every built provider, as Play mode starts and as it ends.
        internal static void ForgetAll()
        {
            foreach (var config in Built)
            {
                // A destroyed config is still a C# object, and its field is still there.
                config.provider = null;
            }

            Built.Clear();
        }
#endif
    }
}
