using System;
using Agenerela.Providers;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// One side of an A/B run: a name, the provider config its agents decide through (DR-013), and
    /// the options each of its calls starts from, such as <see cref="DecideOptions.CheckTarget"/>
    /// off for a control arm.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each prompt sets the call's <see cref="DecideOptions.StimulusLabel"/> from its <c>label</c>.
    /// <see cref="DecideOptions.CheckTarget"/> is on for a prompt only when both the arm and the
    /// prompt's <c>checkTarget</c> leave it on. The arm's own <see cref="DecideOptions.Observations"/>
    /// are sent first on every prompt, before the state, the targets and the prompt's own lines, so
    /// an arm can test one extra line. Everything else is the arm's, unchanged.
    /// </para>
    /// <para>The options are copied when the arm is made: changing them afterwards changes nothing.</para>
    /// </remarks>
    public sealed class EvalArm
    {
        /// <param name="name">How the report names the arm, such as <c>guards-on</c>. Any non-empty text.</param>
        /// <param name="config">The provider config its decisions go through.</param>
        /// <param name="options">The options its calls start from; null for the defaults.</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public EvalArm(string name, ProviderConfig config, DecideOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("An arm needs a name, such as guards-on.", nameof(name));
            }

            // Unity's null, so a destroyed config counts as none.
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), $"Arm '{name}' has no provider config.");
            }

            Name = name.Trim();
            Config = config;
            Options = options == null ? new DecideOptions() : options.Copy();
        }

        /// <summary>The arm's name in the report.</summary>
        public string Name { get; }

        /// <summary>The provider config the arm's agents decide through.</summary>
        public ProviderConfig Config { get; }

        // The arm's own copy; each prompt copies it again before changing it.
        internal DecideOptions Options { get; }

        /// <summary>Whether the arm checks targets, before a prompt's own <c>checkTarget</c>.</summary>
        public bool CheckTarget => Options.CheckTarget;
    }
}
