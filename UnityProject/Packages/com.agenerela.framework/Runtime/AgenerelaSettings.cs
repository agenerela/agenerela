using System;
using Agenerela.Providers;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// The project's Agenerela settings: the provider config an agent uses when it has none of its
    /// own, and the two an evaluation run compares, arm A and arm B (DR-013). Edited in <b>Project Settings → Agenerela</b>, which creates it at
    /// <c>Assets/Agenerela/Resources/AgenerelaSettings.asset</c> the first time a default is set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Profiles</b> (DR-026). <see cref="Development"/> and <see cref="Production"/> each name a
    /// provider config, so one project runs patient retries against a shared server while it is
    /// developed and fails fast once it ships. <see cref="ActiveProfileKind"/> picks one: the
    /// <c>AGENERELA_PROFILE</c> environment variable first, then, in the Editor only,
    /// <see cref="EditorProfile"/>, then the build: Development in the Editor and in a development
    /// build, Production in a release build. A project with no profiles resolves exactly as before.
    /// </para>
    /// <para>
    /// It lives in a <c>Resources</c> folder and is read with <see cref="Resources.Load(string)"/>,
    /// so a built player finds it with no scene and no <c>AssetDatabase</c>, and the build carries the
    /// default config it names. Any <c>Resources</c> folder works if it is moved, as long as it keeps
    /// the name <see cref="ResourceName"/>; keep exactly one.
    /// </para>
    /// </remarks>
    public sealed class AgenerelaSettings : ScriptableObject
    {
        /// <summary>The name it is loaded by, inside a <c>Resources</c> folder.</summary>
        public const string ResourceName = "AgenerelaSettings";

        [Tooltip("The provider config every agent without one of its own uses.")]
        public ProviderConfig DefaultProvider;

        [Tooltip("Arm A of an evaluation run: the provider config an A/B run (EvalRunner) asks first, " +
                 "such as OllamaLocal. Only evaluation reads it; agents never do.")]
        public ProviderConfig EvaluationArmA;

        [Tooltip("Arm B of an evaluation run, compared against arm A on the same prompts, such as " +
                 "OllamaLocal-noGuards or a second model. Only evaluation reads it; agents never do.")]
        public ProviderConfig EvaluationArmB;

        [Tooltip("The profile for the Editor and development builds: a provider config that waits patiently for a " +
                 "shared server, say. Empty uses the Default Provider.")]
        public AgenerelaProfile Development;

        [Tooltip("The profile for a release build: a provider config that fails fast, so the game's own fallback " +
                 "runs. Empty uses the Default Provider.")]
        public AgenerelaProfile Production;

        [Tooltip("Which profile the Editor uses: Automatic is Development. Never read by a build, which goes by " +
                 "its type; the AGENERELA_PROFILE environment variable overrides both.")]
        public ProfileChoice EditorProfile = ProfileChoice.Automatic;

        /// <summary>
        /// The environment variable that chooses the profile everywhere, a build included:
        /// <c>Development</c> or <c>Production</c> (or <c>dev</c>, <c>prod</c>), any case.
        /// </summary>
        public const string ProfileVariable = "AGENERELA_PROFILE";

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

        /// <summary>
        /// The profile in use now: <see cref="ProfileVariable"/> when it names one, else, in the
        /// Editor, <see cref="EditorProfile"/> when it is not Automatic, else Development in the Editor
        /// and in a development build and Production in a release build. Read each time, so a change
        /// in Project Settings applies to the next provider resolved.
        /// </summary>
        public static ProfileKind ActiveProfileKind => Choose(
            Environment.GetEnvironmentVariable(ProfileVariable), Load()?.EditorProfile ?? ProfileChoice.Automatic,
            Application.isEditor, Debug.isDebugBuild, out _);

        /// <summary>
        /// The asset of <see cref="ActiveProfileKind"/>, or null when the project has no settings or
        /// names no profile of that kind.
        /// </summary>
        public static AgenerelaProfile ActiveProfile
        {
            get
            {
                var settings = Load();
                if (settings == null)
                {
                    return null;
                }

                var profile = ActiveProfileKind == ProfileKind.Production ? settings.Production : settings.Development;
                return profile != null ? profile : null;
            }
        }

        /// <summary>
        /// The provider config an agent without one of its own uses now: the active profile's, else
        /// <see cref="DefaultProvider"/>; null when there is neither. What
        /// <see cref="ProviderConfig.Resolve"/> falls back to, and what a game that builds its
        /// providers in code reads, as a template, to follow the profile.
        /// </summary>
        public static ProviderConfig ActiveProvider
        {
            get
            {
                var profile = ActiveProfile;
                if (profile != null && profile.DefaultProvider != null)
                {
                    return profile.DefaultProvider;
                }

                var settings = Load();
                return settings != null && settings.DefaultProvider != null ? settings.DefaultProvider : null;
            }
        }

        /// <summary>Why <see cref="ActiveProfileKind"/> is what it is, in words, for a status line or a log.</summary>
        public static string ActiveProfileReason
        {
            get
            {
                Choose(Environment.GetEnvironmentVariable(ProfileVariable), Load()?.EditorProfile ?? ProfileChoice.Automatic,
                    Application.isEditor, Debug.isDebugBuild, out string why);
                return why;
            }
        }

        // The choice, with everything it reads passed in, so EditMode tests take every branch.
        internal static ProfileKind Choose(string variable, ProfileChoice editorChoice, bool isEditor, bool isDebugBuild, out string why)
        {
            string value = (variable ?? "").Trim();
            if (value.Length > 0)
            {
                if (Matches(value, "development", "dev"))
                {
                    why = $"{ProfileVariable} is {value}.";
                    return ProfileKind.Development;
                }

                if (Matches(value, "production", "prod"))
                {
                    why = $"{ProfileVariable} is {value}.";
                    return ProfileKind.Production;
                }
            }

            string ignored = value.Length > 0 ? $" ({ProfileVariable} is {value}, which names no profile, so it is ignored.)" : "";
            if (isEditor && editorChoice != ProfileChoice.Automatic)
            {
                why = "Project Settings → Agenerela chooses it for the Editor." + ignored;
                return editorChoice == ProfileChoice.Production ? ProfileKind.Production : ProfileKind.Development;
            }

            if (isDebugBuild)
            {
                why = (isEditor ? "The Editor runs Development." : "A development build runs Development.") + ignored;
                return ProfileKind.Development;
            }

            why = "A release build runs Production." + ignored;
            return ProfileKind.Production;
        }

        private static bool Matches(string value, string name, string shortName)
        {
            return string.Equals(value, name, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, shortName, StringComparison.OrdinalIgnoreCase);
        }

        // Makes the next Load look the asset up again.
        internal static void Forget()
        {
            loaded = null;
        }

        // Before anything else as Play mode starts. Without a domain reload (Enter Play Mode Options)
        // statics keep their values from the session before, so a test's Override, or the asset
        // found last time, would otherwise carry into this one (#190, G8).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            loaded = null;
            Override = null;
        }
    }
}
