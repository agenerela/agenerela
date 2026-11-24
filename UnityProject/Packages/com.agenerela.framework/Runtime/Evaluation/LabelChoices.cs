using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// The answers an annotator can pick from: the set's actions and targets, and nothing about
    /// any prompt. With <see cref="BlindPrompt"/>, everything Label mode is given.
    /// </summary>
    public sealed class LabelChoices
    {
        private readonly List<DeclaredAction> actions;

        private LabelChoices(List<DeclaredAction> actions, List<string> targets)
        {
            this.actions = actions;
            Targets = targets;
        }

        /// <summary>Every declared target id, in the set's order. <c>no_target</c> is not one.</summary>
        public IReadOnlyList<string> Targets { get; }

        /// <summary>The choices <paramref name="set"/> declares, copied so the set can change without them.</summary>
        public static LabelChoices From(PromptSet set)
        {
            var actions = set.Actions.Select(a => new DeclaredAction
            {
                Id = a.Id,
                NeedsTarget = a.NeedsTarget,
                TakesNoTarget = a.TakesNoTarget,
                OnlyWhen = (JObject)a.OnlyWhen?.DeepClone(),
            }).ToList();

            if (actions.All(a => a.Id != ActionRegistry.None))
            {
                actions.Add(new DeclaredAction { Id = ActionRegistry.None });
            }

            return new LabelChoices(actions, set.Targets.Select(t => t.Id).ToList());
        }

        /// <summary>
        /// The action ids available in <paramref name="state"/>, <c>none</c> last, the way an agent in
        /// that state is offered them: an action whose <c>onlyWhen</c> does not hold is absent.
        /// </summary>
        public IReadOnlyList<string> ActionsAvailableIn(JObject state)
        {
            return actions.Where(a => a.Id != ActionRegistry.None && a.IsAvailableIn(state))
                .Select(a => a.Id)
                .Concat(new[] { ActionRegistry.None })
                .ToList();
        }

        /// <summary>
        /// The targets an annotator may give <paramref name="actionId"/>, the three states of DR-020
        /// that <see cref="PromptSetChecks"/> holds a prompt to: every declared target when it requires
        /// one, <c>no_target</c> alone when it never takes one, and <c>no_target</c> then every
        /// declared target when it is optional. <c>none</c>, and an id the set does not declare,
        /// take <c>no_target</c> alone.
        /// </summary>
        public IReadOnlyList<string> TargetsFor(string actionId)
        {
            var action = actions.FirstOrDefault(a => a.Id == actionId);
            if (action == null || action.Id == ActionRegistry.None || action.TakesNoTarget)
            {
                return new[] { TargetRegistry.NoTarget };
            }

            return action.NeedsTarget ? Targets : new[] { TargetRegistry.NoTarget }.Concat(Targets).ToList();
        }
    }
}
