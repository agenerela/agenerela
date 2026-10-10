using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>
    /// One field of the answer the model must give: its name, the description the model reads,
    /// and the values it may take. Provider-neutral, like <see cref="DecisionSchema"/>, which
    /// holds these in generation order.
    /// </summary>
    public sealed class SchemaField
    {
        /// <summary>The key the model writes the value under, such as <c>action</c>.</summary>
        public string Name;

        /// <summary>What the field means. The model reads it on every call.</summary>
        public string Description;

        /// <summary>
        /// The only values the field may take, in the order the model is shown them, or null for
        /// a free-form string.
        /// </summary>
        /// <remarks>
        /// Never <c>""</c> among them: Gemini rejects an empty enum string with HTTP 400, which is
        /// why an answer with no target says <see cref="TargetRegistry.NoTarget"/> instead.
        /// </remarks>
        public IReadOnlyList<string> AllowedValues;

        /// <summary>Whether every answer must contain this field. A free-form one may still be empty.</summary>
        public bool Required;
    }
}
