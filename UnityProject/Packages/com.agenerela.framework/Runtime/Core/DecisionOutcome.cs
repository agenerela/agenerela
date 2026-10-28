namespace Agenerela
{
    public enum DecisionOutcome
    {
        // Chose the labelled action and target
        Correct,

        // Chose a different legal action; the agent visibly misbehaves
        WrongLegalAction,

        // Chose something invalid; a guard caught it
        ContainedByGuard,

        // A guard refused a request that should have produced an action
        RejectedWhenActionExpected,

        // Transport or parse failure; not a semantic result
        PipelineError
    }
}