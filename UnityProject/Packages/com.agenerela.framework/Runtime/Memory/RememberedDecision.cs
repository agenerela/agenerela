namespace Agenerela
{
    /// <summary>
    /// What a remembered decision holds when <see cref="RollingHistory"/> sends it back to the model
    /// (#244 item 4, #216 item 3), set on <see cref="RollingHistory.Remembers"/>. The stimulus of each
    /// exchange is sent as it was in every setting; only the decision's side changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The memory keeps everything whichever is set, and writes the decision out at each recall, so
    /// changing the setting changes the next request and loses nothing.
    /// </para>
    /// <para>
    /// <b>Extension point: the Memory research area (#119).</b> Unmeasured as a default: a turn-based
    /// game's small model copied its own remembered statements for turns on end, and invented
    /// grievances its history never held (GreyBoxStrategy, gap N7). Leaving the statement out, or
    /// replacing it with what came of the move, is the experiment this setting makes possible.
    /// </para>
    /// </remarks>
    public enum RememberedDecision
    {
        /// <summary>
        /// The decision as the model gave it, statement included, in the schema's field order. The
        /// default, and how memory always worked: byte for byte the same history as before.
        /// </summary>
        Whole,

        /// <summary>
        /// The move alone: the decision's JSON without its statement field, so the model reads what
        /// it did and never what it said.
        /// </summary>
        Move,

        /// <summary>
        /// The move and what came of it: the decision's JSON without its statement, with an
        /// <see cref="RollingHistory.OutcomeFieldName"/> field after the rest holding the line the
        /// game attached (<see cref="Agent.SetOutcome"/>, <see cref="IRemembersOutcomes"/>). A
        /// decision with no outcome attached is sent as <see cref="Move"/> sends it.
        /// </summary>
        MoveAndOutcome,
    }
}
