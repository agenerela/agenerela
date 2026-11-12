namespace Agenerela
{
    /// <summary>
    /// How the system prompt introduces <see cref="AgentIdentity.StatementRules"/> (#244 item 8). An
    /// enum rather than free text, so each wording is one the framework can name in a report and an
    /// A/B run can compare.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="YourStatement"/>, the default, is the line every request has carried since the
    /// rules existed (#190). Its heading names the field the model fills, so a rule that opens with a
    /// noun phrase (<c>Your statement: A trader's voice: profit, price or a deal.</c>) can read as a
    /// statement already given. In GreyBoxStrategy's persona tables (#249) <c>qwen3.5:2b</c> copied
    /// its merchant's rule out as the statement 373 times in 582; <c>qwen3.5:4b</c> did it twice in
    /// 2,318.
    /// </para>
    /// <para>
    /// The other two values are the candidates measured on 11 November 2026 (findings, "How a
    /// statement rule is introduced"). The rules themselves go in as the developer wrote them, on one
    /// line and ended as a sentence, in every wording.
    /// </para>
    /// </remarks>
    public enum StatementRulesWording
    {
        /// <summary>The default: <c>Your statement: &lt;rules&gt;</c>.</summary>
        YourStatement = 0,

        /// <summary>
        /// A heading that describes the statement rather than naming it:
        /// <c>How your statement should read: &lt;rules&gt;</c>.
        /// </summary>
        HowItShouldRead = 1,

        /// <summary>
        /// An instruction sentence with the rules after it:
        /// <c>Write your statement in your own words, following these rules: &lt;rules&gt;</c>.
        /// </summary>
        InYourOwnWords = 2,
    }
}
