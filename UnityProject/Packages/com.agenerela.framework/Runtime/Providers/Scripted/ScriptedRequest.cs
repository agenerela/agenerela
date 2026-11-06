using System;
using System.Collections.Generic;

namespace Agenerela.Providers
{
    /// <summary>
    /// One request as a <see cref="ScriptedProvider"/> sees it: which agent asked, what it was
    /// asked, what it may answer with, and the <see cref="DecisionRequest"/> itself. What its
    /// <see cref="ScriptedProvider.Callback"/> is given, and what <see cref="ScriptedProvider.LastRequest"/> holds.
    /// </summary>
    public sealed class ScriptedRequest
    {
        private static readonly IReadOnlyList<string> None = Array.Empty<string>();

        internal ScriptedRequest(DecisionRequest request, int index)
        {
            Request = request;
            Index = index;
            Agent = request.AgentName;
            Stimulus = request.StimulusText;
            Label = request.StimulusLabel;

            var action = FieldOf(request.Schema, DecisionSchema.ActionFieldName);
            var target = FieldOf(request.Schema, DecisionSchema.TargetFieldName);
            IsRefusalLine = request.Schema != null && action == null;
            OfferedActions = action?.AllowedValues ?? None;
            OfferedTargets = target?.AllowedValues ?? None;
        }

        /// <summary>The request exactly as the agent sent it.</summary>
        public DecisionRequest Request { get; }

        /// <summary>Its place among the requests this provider has received, from 0.</summary>
        public int Index { get; }

        /// <summary>
        /// The asking agent's name, <see cref="AgentIdentity.Name"/>. Null when the request was not
        /// built by <see cref="PromptBuilder"/>, as a test may build one by hand.
        /// </summary>
        public string Agent { get; }

        /// <summary>
        /// The stimulus exactly as it was passed to <see cref="Agenerela.Agent.DecideAsync"/>, without its
        /// label: <c>Follow me.</c>, not <c>Player: "Follow me."</c>. Null, as for <see cref="Agent"/>.
        /// </summary>
        public string Stimulus { get; }

        /// <summary>The stimulus's label, <see cref="DecideOptions.StimulusLabel"/>, such as <c>Player</c>. Null, as for <see cref="Agent"/>.</summary>
        public string Label { get; }

        /// <summary>
        /// True for the short second request that asks only for a line about what a guard stopped
        /// (<see cref="DecideOptions.AskForRefusalLine"/>). Its <see cref="Stimulus"/> is still the
        /// decision's, and it is answered with <see cref="ScriptedReply.Statement"/>.
        /// </summary>
        public bool IsRefusalLine { get; }

        /// <summary>The action ids the schema allows, in its order; empty for a refusal line.</summary>
        public IReadOnlyList<string> OfferedActions { get; }

        /// <summary>The target ids the schema allows, <see cref="TargetRegistry.NoTarget"/> included; empty for a refusal line.</summary>
        public IReadOnlyList<string> OfferedTargets { get; }

        private static SchemaField FieldOf(DecisionSchema schema, string name)
        {
            if (schema?.Fields == null)
            {
                return null;
            }

            foreach (var field in schema.Fields)
            {
                if (field != null && field.Name == name)
                {
                    return field;
                }
            }

            return null;
        }
    }
}
