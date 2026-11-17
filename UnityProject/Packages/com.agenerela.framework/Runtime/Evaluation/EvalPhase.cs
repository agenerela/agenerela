namespace Agenerela.Evaluation
{
    /// <summary>What an <see cref="EvalRunner"/> is doing when it reports <see cref="EvalRunner.Progress"/>.</summary>
    public enum EvalPhase
    {
        /// <summary>Reading what an Ollama server holds and its version, before an arm's first request.</summary>
        Checking,

        /// <summary>Sending an arm's throwaway warm-up requests, which no count includes.</summary>
        WarmUp,

        /// <summary>Asking the prompts: reported after each answer.</summary>
        Asking,

        /// <summary>Unloading arm A's model between sequential arms, and waiting until it has left memory.</summary>
        Unloading,
    }
}
