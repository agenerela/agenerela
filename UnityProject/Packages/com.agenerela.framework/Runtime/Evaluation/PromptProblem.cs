namespace Agenerela.Evaluation
{
    /// <summary>One rule one prompt breaks, found by <see cref="PromptSetChecks"/>.</summary>
    public sealed class PromptProblem
    {
        public PromptProblem(string promptId, PromptRule rule, string message)
        {
            PromptId = promptId;
            Rule = rule;
            Message = message;
        }

        public string PromptId { get; }

        public PromptRule Rule { get; }

        /// <summary>One sentence a person can act on.</summary>
        public string Message { get; }

        public override string ToString() => $"{PromptId}: {Message}";
    }
}
