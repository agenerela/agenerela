using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// An arm's own target legality, for measuring whether a model picks a legal action and target
    /// pair (#151): categories for the set's targets and the profile's actions, and extra targets
    /// registered after the set's, all without changing the prompt set, its labels or the profile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For evaluation only. A game declares its legality with
    /// <see cref="ActionDefinition.TargetCategories"/>, categories on its targets and per-target
    /// checks; this lets a runner arm give the evaluation set's agent the same kind of rules, so that
    /// a set whose targets declare none can still tell a legal pair from an illegal one.
    /// </para>
    /// <para>
    /// Only honest when no label expects a pair these rules make illegal. <see cref="EvalRunner"/>
    /// checks that before the first prompt and refuses the run otherwise.
    /// </para>
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EvalLegality
    {
        /// <summary>A short name for the report, such as <c>places-and-creatures</c>.</summary>
        [JsonProperty("name")] public string Name;

        /// <summary>The category each of the set's targets is registered in, by id. A target not named has none.</summary>
        [JsonProperty("targetCategories")] public Dictionary<string, string> TargetCategories = new Dictionary<string, string>();

        /// <summary>
        /// The <see cref="ActionDefinition.TargetCategories"/> an action is given, by id, on a copy of
        /// the profile's definition. An action not named keeps the profile's.
        /// </summary>
        [JsonProperty("actionCategories")] public Dictionary<string, string[]> ActionCategories = new Dictionary<string, string[]>();

        /// <summary>Targets registered after the set's, in order, each told as a standing line as the set's are.</summary>
        [JsonProperty("extraTargets")] public List<EvalExtraTarget> ExtraTargets = new List<EvalExtraTarget>();

        // A copy, so an arm's legality cannot change after the arm is made.
        internal EvalLegality Copy()
        {
            var copy = new EvalLegality
            {
                Name = Name,
                TargetCategories = new Dictionary<string, string>(TargetCategories ?? new Dictionary<string, string>()),
                ActionCategories = new Dictionary<string, string[]>(),
                ExtraTargets = new List<EvalExtraTarget>(),
            };

            foreach (var entry in ActionCategories ?? new Dictionary<string, string[]>())
            {
                copy.ActionCategories[entry.Key] = entry.Value == null ? null : (string[])entry.Value.Clone();
            }

            foreach (var extra in ExtraTargets ?? new List<EvalExtraTarget>())
            {
                if (extra == null)
                {
                    throw new ArgumentException("An extra target is null.");
                }

                copy.ExtraTargets.Add(new EvalExtraTarget { Id = extra.Id, Description = extra.Description, Category = extra.Category });
            }

            return copy;
        }
    }

    /// <summary>One target an <see cref="EvalLegality"/> registers beside the set's.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EvalExtraTarget
    {
        /// <summary>Its id, which no label of the set may name.</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>Its standing line's text, as a set target's description is.</summary>
        [JsonProperty("description")] public string Description;

        /// <summary>Its category, or null for none.</summary>
        [JsonProperty("category")] public string Category;
    }
}
