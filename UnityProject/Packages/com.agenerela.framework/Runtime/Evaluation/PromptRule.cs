namespace Agenerela.Evaluation
{
    /// <summary>The rule a <see cref="PromptProblem"/> breaks, from the rules for writing a prompt.</summary>
    public enum PromptRule
    {
        /// <summary>An id, label, stimulus, expected action or expected target is missing.</summary>
        Incomplete,

        /// <summary>The id does not follow <c>&lt;category prefix&gt;_&lt;nnn&gt;</c>.</summary>
        IdMalformed,

        /// <summary>Another prompt in the set has the same id.</summary>
        IdDuplicate,

        /// <summary>The expected action is not one the set declares.</summary>
        ActionNotDeclared,

        /// <summary>An expected target is neither a declared target nor <c>no_target</c>.</summary>
        TargetNotRegistered,

        /// <summary><c>none</c> expects a target other than <c>no_target</c>.</summary>
        NoneTakesNoTarget,

        /// <summary>An action that needs a target expects <c>no_target</c>.</summary>
        ActionNeedsTarget,

        /// <summary>An action that takes no target expects one.</summary>
        ActionTakesNoTarget,

        /// <summary>The precondition state does not match the expected action's <c>onlyWhen</c>.</summary>
        PreconditionMismatch,

        /// <summary>
        /// The stimulus is a few-shot example the agent is shown: an action's <c>ExampleStimulus</c>
        /// or the profile's <c>IdleExampleStimulus</c> (hard rule 4).
        /// </summary>
        StimulusIsExample,
    }
}
