using System.Collections.Generic;
using System.Text;
using Agenerela.Providers;

namespace Agenerela.Tests
{
    /// <summary>
    /// Builds token probabilities by hand, so a grounding test reads like the probe's data: each
    /// token's text, its log probability, and the alternatives the model weighed there, likeliest
    /// first. <c>T("training", -2.95, ("sc", -0.06), ("training", -2.95))</c> is the token
    /// "training", chosen at -2.95 while the model wanted "sc".
    /// </summary>
    public static class Tokens
    {
        /// <summary>The statement every <see cref="Reply"/> ends with.</summary>
        public const string Statement = "On my way.";

        /// <summary>One token, with the alternatives at its position.</summary>
        public static TokenProbability T(string text, double logProbability, params (string Text, double LogProbability)[] alternatives)
        {
            var list = new List<TokenAlternative>();
            foreach (var (altText, altLogProbability) in alternatives)
            {
                list.Add(new TokenAlternative { Text = altText, LogProbability = (float)altLogProbability });
            }

            return new TokenProbability { Text = text, LogProbability = (float)logProbability, Alternatives = list };
        }

        /// <summary>A token the model was sure of, with no alternatives reported.</summary>
        public static TokenProbability T(string text)
        {
            return T(text, 0);
        }

        /// <summary>
        /// A whole reply: <paramref name="action"/>, then the target's tokens, then <see cref="Statement"/>.
        /// Everything outside the target is one sure token on each side, so the target's first token
        /// starts right after its opening quote.
        /// </summary>
        public static IReadOnlyList<TokenProbability> Reply(string action, params TokenProbability[] target)
        {
            var tokens = new List<TokenProbability> { T("{\"action\": \"" + action + "\", \"target\": \"") };
            tokens.AddRange(target);
            tokens.Add(T("\", \"statement\": \"" + Statement + "\"}"));
            return tokens;
        }

        /// <summary>The tokens' texts joined, which is the reply's text when no character was split.</summary>
        public static string Join(IEnumerable<TokenProbability> tokens)
        {
            var text = new StringBuilder();
            foreach (var token in tokens)
            {
                text.Append(token.Text);
            }

            return text.ToString();
        }
    }
}
