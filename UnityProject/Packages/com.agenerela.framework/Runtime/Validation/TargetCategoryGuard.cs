using System;
using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>
    /// The third built-in guard (build plan §2.5, DR-020): the target is a kind the action takes. An
    /// action that names its <see cref="ActionDefinition.TargetCategories"/> is held to them, so
    /// <c>complete_quest</c> aimed at a place, or <c>quote_sale</c> at a stock entry, becomes
    /// <c>none</c> before any handler sees it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It acts only for an action that declares categories, and only on a real target: an action
    /// that names none, <see cref="ActionRegistry.None"/> and <see cref="TargetRegistry.NoTarget"/>
    /// always pass. So it is on by default and changes nothing until a developer declares a
    /// category. A target registered with no category is refused by an action that declares one.
    /// </para>
    /// <para>
    /// Inside <see cref="Agent.DecideAsync"/> it runs after the grounding guard, so adopting it never
    /// changes what that guard reads or records, and its verdict is
    /// <see cref="GuardVerdict.RewriteToNone"/>: the model chose an action and a target that were
    /// both offered, so it broke no contract, but the two do not go together. That is the contained
    /// refusal the demos' own pair guards gave. <see cref="Agent.Execute"/> runs it again after
    /// <see cref="SchemaLegalityGuard"/>, against the targets as they are then, and refuses a mismatch.
    /// </para>
    /// <para>
    /// The schema still offers every target with every action; this guard catches the pair after
    /// the model chose it. Making the pair unexpressible is the paired schema of DR-020, an option
    /// that is off until it is A/B'd.
    /// </para>
    /// </remarks>
    public sealed class TargetCategoryGuard : IDecisionGuard
    {
        // What a refusal line's request says instead of Reason, which names categories for a
        // developer (#109). Unmeasured, and it changes only what is said.
        private const string NotThat = "that is not something this action can be done to.";

        /// <summary>Always <c>"TargetCategoryGuard"</c>; <see cref="Agent.Execute"/>'s re-check records it with <c>"@Execute"</c> after it.</summary>
        public string Name => nameof(TargetCategoryGuard);

        /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
        public GuardVerdict Inspect(GuardInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var decision = input.Decision;
            var ctx = input.Context;

            if (decision.ActionId == ActionRegistry.None || decision.TargetId == TargetRegistry.NoTarget)
            {
                return GuardVerdict.Pass;
            }

            // An unregistered action, or a target that is not registered, is SchemaLegalityGuard's
            // to refuse, and it runs first.
            if (!ctx.Actions.TryGet(decision.ActionId, out var definition, out _) || !ctx.Targets.Contains(decision.TargetId))
            {
                return GuardVerdict.Pass;
            }

            string category = ctx.Targets.CategoryOf(decision.TargetId);
            if (definition.AcceptsCategory(category))
            {
                return GuardVerdict.Pass;
            }

            return GuardVerdict.RewriteToNone(Reason(definition, decision.TargetId, category), NotThat);
        }

        private static string Reason(ActionDefinition definition, string targetId, string category)
        {
            var accepted = new List<string>();
            foreach (string name in definition.TargetCategories)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    accepted.Add("'" + name + "'");
                }
            }

            string kind = category == null ? "has no category" : $"is in the category '{category}'";
            return $"'{definition.Id}' takes only targets in the {(accepted.Count == 1 ? "category" : "categories")} " +
                   $"{string.Join(", ", accepted)}, and '{targetId}' {kind}.";
        }
    }
}
