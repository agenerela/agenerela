namespace Agenerela.Evaluation
{
    /// <summary>
    /// What a reply check found wrong with an answer's <c>statement</c>, the line the agent says.
    /// A second score beside the five outcomes, which it never changes: the outcomes score the
    /// decision, these the words (<see cref="ReplyChecks"/>).
    /// </summary>
    public enum ReplyFlag
    {
        /// <summary>
        /// The statement is a few-shot line, by <see cref="PromptSetChecks.MatchesExample"/>: an
        /// action's example or counter-example stimulus or statement, the profile's idle example or
        /// its statement, or the arm's own.
        /// </summary>
        Echo,

        /// <summary>The schema asked for a statement and it is empty or only whitespace.</summary>
        Empty,

        /// <summary>The statement is longer than the arm's <see cref="EvalArm.MaxStatementLength"/>.</summary>
        TooLong,

        /// <summary>
        /// The statement shows the machinery: an action or target id as written (<c>move_to</c>,
        /// <c>no_target</c>), a schema field used as a label (<c>action:</c>), a JSON brace, or one
        /// of <see cref="ReplyChecks.AssistantPhrases"/>.
        /// </summary>
        Leak,
    }
}
