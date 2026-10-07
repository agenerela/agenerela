using System.Collections.Generic;

namespace Agenerela.Providers
{
    /// <summary>
    /// One generated token, with the model's log probability for it and the likeliest tokens
    /// it weighed at the same position. <see cref="ProviderResult.TokenProbabilities"/> holds
    /// one per token of the reply, and the grounding guard (Phase 3, DR-016) reads them.
    /// </summary>
    /// <remarks>
    /// Every number here comes from before the schema's mask: what the model wanted to write,
    /// not what it was allowed to. That is the rule on
    /// <see cref="ProviderCapabilities.ReportsTokenProbabilities"/>, and it is what makes the
    /// data worth having. "Attack the scarecrow." ends in an attack on the training dummy,
    /// but only 0.05 of the model's probability at the target stayed on the list, because it
    /// wanted to write "sc…".
    /// </remarks>
    public sealed class TokenProbability
    {
        /// <summary>
        /// The token as text, as the backend rendered it. A character split across two tokens
        /// cannot be written by either token alone, so its bytes are missing from both texts:
        /// Ollama, through llama.cpp's server, cuts them off.
        /// </summary>
        public string Text;

        /// <summary>
        /// The natural log of the probability the model gave this token, so 0 or below;
        /// <c>Math.Exp</c> of it is the probability.
        /// </summary>
        public float LogProbability;

        /// <summary>
        /// The likeliest tokens at this position, likeliest first, as many as the provider
        /// asked for (five was enough in the probe). Empty when the backend reported none.
        /// The token actually chosen is among them only if it was one of the likeliest. After
        /// the mask it often is not, and that is the case the guard looks for.
        /// </summary>
        public IReadOnlyList<TokenAlternative> Alternatives = new List<TokenAlternative>();
    }

    /// <summary>
    /// A token the model weighed at one position of its reply, read from the same
    /// distribution as <see cref="TokenProbability.LogProbability"/>, before the mask.
    /// </summary>
    public sealed class TokenAlternative
    {
        /// <summary>The token as text, rendered as <see cref="TokenProbability.Text"/> is.</summary>
        public string Text;

        /// <summary>The natural log of its probability, so 0 or below.</summary>
        public float LogProbability;
    }
}
