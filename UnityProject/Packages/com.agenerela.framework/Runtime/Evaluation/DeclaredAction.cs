using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// An action the set's agent can take, as the set declares it. The <see cref="AgentProfile"/>
    /// the set is measured against should list the same ones; a difference is reported by
    /// <see cref="PromptSetChecks.CompareWithProfile"/>, never fixed silently.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class DeclaredAction
    {
        /// <summary>The action id. <c>none</c> is declared like any other.</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>
        /// True when the action requires a registered target. False leaves the target optional
        /// (DR-020): either a registered target or <c>no_target</c> is a correct answer, unless
        /// <see cref="TakesNoTarget"/> says it never takes one.
        /// </summary>
        [JsonProperty("needsTarget")] public bool NeedsTarget;

        /// <summary>
        /// True when the action never takes a target, so <c>no_target</c> is its only correct
        /// answer (<see cref="ActionDefinition.TakesNoTarget"/>). Absent, like every other
        /// unset field, means false: the target is optional.
        /// </summary>
        [JsonProperty("takesNoTarget", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool TakesNoTarget;

        /// <summary>
        /// Optional. The state in which the action is available, in the same shape as a prompt's
        /// <c>preconditionState</c>. Declared here because availability is the game's code, which a
        /// set cannot read.
        /// </summary>
        [JsonProperty("onlyWhen", NullValueHandling = NullValueHandling.Ignore)] public JObject OnlyWhen;

        [JsonExtensionData] public IDictionary<string, JToken> Extra = new Dictionary<string, JToken>();

        /// <summary>True when <paramref name="state"/> holds every value <see cref="OnlyWhen"/> asks for.</summary>
        public bool IsAvailableIn(JObject state)
        {
            if (OnlyWhen == null)
            {
                return true;
            }

            foreach (var required in OnlyWhen.Properties())
            {
                if (state == null || !state.TryGetValue(required.Name, out var actual)
                                  || !JToken.DeepEquals(actual, required.Value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
