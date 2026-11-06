using System;
using UnityEngine;

namespace Agenerela
{
    [Serializable]
    public sealed class ActionDefinition
    {
        [Tooltip("snake_case. Becomes the value the model picks from.")]
        public string Id;

        [Tooltip("One SHORT clause telling the model WHEN to use this action. Keep it " +
         "comparable in length to your other actions. A long emphatic description " +
         "measurably biases small models toward picking it.")]
        [TextArea(1, 3)] public string Description;

        [Tooltip("Does this action need something to act on?")]
        public bool RequiresTarget;

        [Tooltip("A stimulus that should lead to this action. A player's input, an event, " +
         "or behavior that triggers this action. Use {0} for the target and never reuse an evaluation prompt")]
        public string ExampleStimulus;

        [Tooltip("Target ids this verb sensibly applies to. Prevents nonsense examples " +
                 "like 'Pick up the Blacksmith'.")]
        public string[] PreferredExampleTargets;

        /// <summary>
        /// A stimulus that touches this action's subject but asks for nothing, shown to the model
        /// answered with <c>none</c> beside the action's own example (#190, Living Vale #9). Empty
        /// by default, which adds nothing to the prompt.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="FewShotBuilder.Build"/> puts it right after this action's example, as
        /// <c>Player: "Any rumours about the wagon?" -&gt; action: none, target: no_target</c>, while
        /// the action is available. A <c>{0}</c> is filled like the example's, with the first
        /// registered target of <see cref="PreferredExampleTargets"/>, and with none registered the
        /// line is left out. <see cref="DecideOptions.CounterExamples"/> leaves every action's out
        /// of one call.
        /// </para>
        /// <para>
        /// <b>Unmeasured.</b> In Living Vale one line answered with <c>none</c>, sent as the idle
        /// example, cut offers nobody asked for from 6/13 to 0/13 and from 10/13 to 0/13 on two small
        /// models (its findings on branch <c>69-living-vale</c>, 5 November). A counter-example per
        /// action has not been measured, and a <c>none</c> example can also make a model miss real
        /// commands. The evaluation A/B is owed (#151). Like every example, it must never be an
        /// evaluation prompt (hard rule 4), which <c>PromptSetChecks</c> checks.
        /// </para>
        /// </remarks>
        [Tooltip("Optional. A stimulus that mentions this action's subject but asks for nothing, " +
                 "shown answered with 'none'. Use {0} for the target, as in the example, and never " +
                 "reuse an evaluation prompt.")]
        public string CounterExampleStimulus;

        /// <summary>
        /// The kinds of target this action takes, matched ordinally against the category each
        /// target was registered in (<see cref="TargetRegistry.CategoryOf"/>), such as <c>quest</c>
        /// for <c>complete_quest</c> (#190, DR-020). Null or empty, the default, takes any target,
        /// as every action did before.
        /// </summary>
        /// <remarks>
        /// Declaring them changes nothing the model is sent. <see cref="TargetCategoryGuard"/>, a
        /// built-in guard, turns a decision that pairs this action with a target of another
        /// category, or with no category, into <c>none</c>, and <see cref="Agent.Execute"/> refuses
        /// one. A blank entry is ignored. <see cref="AcceptsCategory"/> is the rule.
        /// </remarks>
        [Tooltip("Optional. The target categories this action takes, such as 'quest'. A target in " +
                 "another category, or in none, is refused for it. Leave empty for any target.")]
        public string[] TargetCategories;

        /// <summary>
        /// Whether this action takes a target registered in <paramref name="category"/>: always when
        /// <see cref="TargetCategories"/> names none, otherwise only for one of them. A null or blank
        /// category, a target registered without one, is taken only by an action that names none.
        /// </summary>
        public bool AcceptsCategory(string category)
        {
            if (!DeclaresTargetCategories)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            foreach (string accepted in TargetCategories)
            {
                if (string.Equals(accepted, category, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether <see cref="TargetCategories"/> names at least one category.</summary>
        public bool DeclaresTargetCategories
        {
            get
            {
                if (TargetCategories == null)
                {
                    return false;
                }

                foreach (string accepted in TargetCategories)
                {
                    if (!string.IsNullOrWhiteSpace(accepted))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static bool ValidateId(string id, out string problem)
        {
            problem = null;

            if (string.IsNullOrWhiteSpace(id))
            {
                problem = "Id is empty";
                return false;
            }

            // Invariant, not the current culture: under a Turkish locale 'I'.ToLower()
            // is the dotless i (U+0131), so a culture-sensitive compare classifies ids by
            // whichever machine happens to open the asset.
            if (id != id.ToLowerInvariant())
            {
                problem = $"Id '{id}' cannot have uppercase characters";
                return false;
            }

            // Any whitespace, not just ' '. A tab is invisible in the Inspector but the
            // id goes verbatim into the schema enum, so it has to be caught here.
            foreach (char c in id)
            {
                if (char.IsWhiteSpace(c))
                {
                    problem = $"Id '{id}' cannot have whitespace";
                    return false;
                }
            }

            return true;
        }
    }
}
