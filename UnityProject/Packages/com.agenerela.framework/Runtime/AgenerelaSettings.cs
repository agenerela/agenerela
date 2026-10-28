using Agenerela.Providers;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// The project's Agenerela settings: for now, the provider config an agent uses when it has none
    /// of its own (DR-013). Edited in <b>Project Settings → Agenerela</b>, which creates it at
    /// <c>Assets/Agenerela/Resources/AgenerelaSettings.asset</c> the first time a default is set.
    /// </summary>
    /// <remarks>
    /// It lives in a <c>Resources</c> folder and is read with <see cref="Resources.Load(string)"/>,
    /// so a built player finds it with no scene and no <c>AssetDatabase</c>, and the build carries the
    /// default config it names. Any <c>Resources</c> folder works if it is moved, as long as it keeps
    /// the name <see cref="ResourceName"/>; keep exactly one.
    /// </remarks>
    public sealed class AgenerelaSettings : ScriptableObject
    {
        /// <summary>The name it is loaded by, inside a <c>Resources</c> folder.</summary>
        public const string ResourceName = "AgenerelaSettings";

        [Tooltip("The provider config every agent without one of its own uses.")]
        public ProviderConfig DefaultProvider;

        private static AgenerelaSettings loaded;

        // Set by tests to stand in for the project's asset; null otherwise.
        internal static AgenerelaSettings Override;

        /// <summary>The project's settings, or null when it has none.</summary>
        internal static AgenerelaSettings Load()
        {
            if (Override != null)
            {
                return Override;
            }

            // Unity's null, so an asset deleted in the Editor is looked up again. A missing one is
            // not remembered, so one created later is found.
            if (loaded == null)
            {
                loaded = Resources.Load<AgenerelaSettings>(ResourceName);
            }

            return loaded;
        }

        // Makes the next Load look the asset up again.
        internal static void Forget()
        {
            loaded = null;
        }
    }
}
