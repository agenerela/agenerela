using System;
using System.Collections.Generic;
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

        private List<string> knowledge = new List<string>();

        /// <summary>
        /// Lines added to the profile's <see cref="AgentIdentity.Knowledge"/> for this arm's agents,
        /// after the profile's own; empty unless set, which leaves the profile's identity as it is.
        /// The profile asset itself is never changed. So an arm can test a few lines as knowledge
        /// against an arm that sends the same lines as <see cref="DecideOptions.Observations"/>
        /// (#190, Living Vale #14).
        /// </summary>
        /// <remarks>Copied when set: changing the list afterwards changes nothing.</remarks>
        public IReadOnlyList<string> Knowledge
        {
            get => knowledge;
            set => knowledge = value == null ? new List<string>() : new List<string>(value);
        }

        /// <summary>The arm's name in the report.</summary>
        public string Name { get; }

        /// <summary>The provider config the arm's agents decide through.</summary>
        public ProviderConfig Config { get; }

        // The arm's own copy; each prompt copies it again before changing it.
        internal DecideOptions Options { get; }

        /// <summary>Whether the arm checks targets, before a prompt's own <c>checkTarget</c>.</summary>
        public bool CheckTarget => Options.CheckTarget;

        /// <summary>
        /// <see cref="MaxStatementLength"/>'s default, 200 characters. Unmeasured: a guess at where a
        /// spoken line turns into a speech, well above the longest of 632 statements from
        /// <c>qwen3.5:2b</c> on 9 November 2026 (81).
        /// </summary>
        public const int DefaultMaxStatementLength = 200;

        private int maxStatementLength = DefaultMaxStatementLength;

        /// <summary>
        /// The longest statement, in characters once trimmed, that is not
        /// <see cref="ReplyFlag.TooLong"/>; <see cref="DefaultMaxStatementLength"/> unless set. A
        /// reply check only: it never changes a decision, its outcome or the accuracy.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Set to less than 1.</exception>
        public int MaxStatementLength
        {
            get => maxStatementLength;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        $"Arm '{Name}': MaxStatementLength must be at least 1; int.MaxValue never flags a statement as too long.");
                }

                maxStatementLength = value;
            }
        }
    }
}
