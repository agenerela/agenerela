using System;

namespace Agenerela
{
    /// <summary>
    /// One few-shot example a call adds to the ones its actions and profile give
    /// (<see cref="DecideOptions.AddedExamples"/>, #151): a stimulus and the answer it shows. Immutable,
    /// so a list handed to a call cannot change under it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rendered as every other example is, under the call's stimulus label and with the statement under
    /// the name the schema gives it: <c>Player: "Do whatever you think best." -&gt; action: move_to,
    /// target: tower, statement: "I'll watch from the tower."</c>. It comes after the actions' own
    /// examples and counter-examples, and before the idle example.
    /// </para>
    /// <para>
    /// An example never shows an answer the call could not give, so it is left out of a call that does
    /// not offer its action, or its target, or where its action cannot take that target now; a target
    /// of <c>no_target</c> is left out for an action that requires one. Its text is sent as written: a
    /// <c>{0}</c> is not filled.
    /// </para>
    /// <para>
    /// For an A/B run of one example in one arm, such as a free choice the model is shown answering
    /// with an action rather than <c>none</c>. The evaluation runner refuses a run in which an arm's
    /// example matches a prompt's stimulus (hard rule 4).
    /// </para>
    /// </remarks>
    public sealed class FewShotExample
    {
        /// <param name="stimulus">What the example is asked, such as "Do whatever you think best."</param>
        /// <param name="actionId">The action it answers with; <see cref="ActionRegistry.None"/> is allowed.</param>
        /// <param name="targetId">The target it names; null is <see cref="TargetRegistry.NoTarget"/>.</param>
        /// <param name="statement">What the agent says in it; null or empty shows none.</param>
        /// <exception cref="ArgumentException"><paramref name="stimulus"/> or <paramref name="actionId"/> is empty.</exception>
        public FewShotExample(string stimulus, string actionId, string targetId = null, string statement = null)
        {
            if (string.IsNullOrWhiteSpace(stimulus))
            {
                throw new ArgumentException("A few-shot example needs a stimulus.", nameof(stimulus));
            }

            if (string.IsNullOrWhiteSpace(actionId))
            {
                throw new ArgumentException($"The example \"{stimulus}\" needs an action; use none for no action.", nameof(actionId));
            }

            Stimulus = stimulus;
            ActionId = actionId.Trim();
            TargetId = string.IsNullOrWhiteSpace(targetId) ? TargetRegistry.NoTarget : targetId.Trim();
            Statement = string.IsNullOrWhiteSpace(statement) ? null : statement;
        }

        /// <summary>What the example is asked.</summary>
        public string Stimulus { get; }

        /// <summary>The action it answers with.</summary>
        public string ActionId { get; }

        /// <summary>The target it names, <see cref="TargetRegistry.NoTarget"/> when none.</summary>
        public string TargetId { get; }

        /// <summary>What the agent says in it; null when nothing.</summary>
        public string Statement { get; }

        /// <summary>"stimulus -&gt; action, target", as the evaluation report lists it.</summary>
        public override string ToString()
        {
            return $"\"{Stimulus}\" -> {ActionId}, {TargetId}";
        }
    }
}
