namespace Agenerela
{
    /// <summary>
    /// A target object that says what to call it, as a player would ("healing potion"), for the
    /// few-shot examples. Optional: implement it on the object a game registers as a target.
    /// <c>FewShotBuilder</c> fills an example's <c>{0}</c> with it in place of the name it makes from
    /// the id ("StockMaraPotionOne", or "stock mara potion one" spaced) (#190, Living Vale #20).
    /// </summary>
    /// <remarks>
    /// Implementing it is the opt-in: no target has one by default, so no example changes until a
    /// game writes one. It is separate from <see cref="IDescribedTarget"/>, whose line says what the
    /// target is and goes in the action and target list; a name has to fit inside the example's
    /// sentence. Unmeasured, like the rest of #190's prompt changes (#151).
    /// </remarks>
    public interface INamedTarget
    {
        /// <summary>
        /// A short name that fits where the example writes <c>{0}</c>, such as "healing potion".
        /// Used as written, whatever <see cref="DecideOptions.SpacedExampleNames"/> says. Null or blank
        /// uses the name made from the id. Read on every decision that names the target.
        /// </summary>
        string Name { get; }
    }
}
