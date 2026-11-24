using Agenerela.Providers;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// One way of running the project's agents, such as for development or for a shipped game: the
    /// provider config they use (DR-026). Made with <b>Create → Agenerela → Settings Profile</b> and
    /// named in Project Settings → Agenerela as the <see cref="AgenerelaSettings.Development"/> or the
    /// <see cref="AgenerelaSettings.Production"/> profile; <see cref="AgenerelaSettings.ActiveProfile"/>
    /// says which one is in use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What differs between the two lives in the provider configs a profile names: the server, its
    /// key's variable, the timeout and the <see cref="RetryPolicy"/>. A development config, for a
    /// shared server that queues, waits patiently and reads its address from an environment
    /// variable; a production one fails fast, so the game's own fallback runs.
    /// </para>
    /// <para>
    /// Nothing here is secret, and nothing may be (hard rule 1): a profile is an asset, committed
    /// and shipped. Keys stay in environment variables, which a provider config names.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Agenerela/Settings Profile", fileName = "AgenerelaDevelopment")]
    public sealed class AgenerelaProfile : ScriptableObject
    {
        [Tooltip("The provider config every agent without one of its own uses while this profile is active. " +
                 "Empty falls back to the Default Provider in Project Settings → Agenerela.")]
        public ProviderConfig DefaultProvider;
    }

    /// <summary>Which of the project's two profiles is in use (DR-026).</summary>
    public enum ProfileKind
    {
        /// <summary>The Editor and development builds, unless chosen otherwise.</summary>
        Development,

        /// <summary>A release build, unless chosen otherwise.</summary>
        Production,
    }

    /// <summary>
    /// The profile Project Settings → Agenerela chooses for the Editor: as the build type says, or
    /// one of the two (DR-026).
    /// </summary>
    public enum ProfileChoice
    {
        /// <summary>Development in the Editor and in a development build, Production in a release build.</summary>
        Automatic,

        /// <summary>Development in the Editor.</summary>
        Development,

        /// <summary>Production in the Editor, to try what players get.</summary>
        Production,
    }
}
