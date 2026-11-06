namespace Agenerela.Evaluation
{
    /// <summary>How an <see cref="EvalRunner"/> takes turns between its two arms.</summary>
    public enum EvalArmOrder
    {
        /// <summary>
        /// Each prompt in arm A, then in arm B, then the next prompt, so drift over the session hits
        /// both arms alike. Both arms must be on one model: two would both be resident (hard rule 3).
        /// </summary>
        Interleaved,

        /// <summary>
        /// All of arm A, then the model is unloaded, then all of arm B: for arms on two models, such
        /// as a 2B against a 4B, so only one is ever resident. Drift over the session is not shared
        /// between the arms, and the report says they ran one after the other.
        /// </summary>
        Sequential,
    }
}
