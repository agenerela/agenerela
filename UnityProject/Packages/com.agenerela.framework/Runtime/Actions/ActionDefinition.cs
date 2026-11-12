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

        /// <summary>
        /// Whether this action never takes a target, such as <c>recruit</c> or <c>raise_taxes</c>
        /// (#244 item 2). False, the default, leaves the action as it always was: with
        /// <see cref="RequiresTarget"/> off, its target is optional and may be any target in its
        /// <see cref="TargetCategories"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// On, a target the model names anyway is dropped, not refused: inside
        /// <see cref="Agent.DecideAsync"/> the decision's target becomes
        /// <see cref="TargetRegistry.NoTarget"/> before any guard or handler sees it,
        /// <see cref="DecisionResult.ModelDecision"/> keeps the model's, and
        /// <see cref="DecisionTelemetry.TargetIgnored"/> records the target dropped. With
        /// <see cref="DecideOptions.PairTargetsWithActions"/> the schema pairs the action with
        /// <c>no_target</c> alone, so the grammar never lets a target through, and one that comes
        /// anyway is rejected by <see cref="SchemaLegalityGuard"/>, as any pair the grammar forbade is.
        /// <see cref="Agent.Execute(DecisionResult)"/> refuses such a decision too.
        /// </para>
        /// <para>
        /// It changes nothing the model reads in the prompt. Registering an action with it and
        /// <see cref="RequiresTarget"/>, or with <see cref="TargetCategories"/>, throws.
        /// </para>
        /// </remarks>
        [Tooltip("This action never takes a target. A target the model names anyway is dropped. " +
                 "Cannot be combined with 'Requires Target' or 'Target Categories'.")]
        public bool TakesNoTarget;

        [Tooltip("A stimulus that should lead to this action. A player's input, an event, " +
         "or behavior that triggers this action. Use {0} for the target and never reuse an evaluation prompt")]
        public string ExampleStimulus;

        [Tooltip("Target ids this verb sensibly applies to. Prevents nonsense examples " +
                 "like 'Pick up the Blacksmith'.")]
        public string[] PreferredExampleTargets;

        /// <summary>
        /// What the agent says in this action's example, shown after its target as
        /// <c>statement: "On my way to the Tower."</c> (#190, GreyBoxVillage G14). Empty by default:
        /// the example then shows no statement, as before.
        /// </summary>
        /// <remarks>
        /// An example with no statement leaves the model only the stimulus to copy: GreyBoxVillage's
        /// guard answered with its idle example's stimulus in 4 of 4 live tries (findings,
        /// 5 November). A <c>{0}</c> is filled with the same target as the example's stimulus.
        /// <see cref="DecideOptions.ExampleStatements"/> leaves every statement out of one call.
        /// Unmeasured: the A/B is owed (#151). Never an evaluation prompt (hard rule 4).
        /// </remarks>
        [Tooltip("Optional. What the agent says in this action's example. Use {0} for the target, " +
                 "as in the example, and never reuse an evaluation prompt.")]
        public string ExampleStatement;

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
        /// What the agent says in this action's counter-example, the line answered with <c>none</c>
        /// (#190, G14). Empty by default. Shown and filled like <see cref="ExampleStatement"/>.
        /// </summary>
        [Tooltip("Optional. What the agent says in the counter-example. Use {0} for the target, and " +
                 "never reuse an evaluation prompt.")]
        public string CounterExampleStatement;

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
