using System;
using System.Collections.Generic;
using Agenerela;
using LivingVale.Game;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// Registers G4c's route actions with one NPC's <see cref="Agent"/>: <c>share_information</c>,
    /// <c>offer_exchange</c>, <c>resolve_delivery</c> and <c>grant_access</c>. As with
    /// <see cref="TradeActionBinding"/>, availability and targets come from
    /// <see cref="NpcActionExecutor.AvailableFor"/> on every decision, so an action with nothing to
    /// use is absent from the schema, and every handler calls the same
    /// <see cref="NpcActionExecutor.Execute"/> the scripted driver does. The side quests' offers ride
    /// on <c>offer_quest</c>, which <see cref="QuestActionBinding"/> already registers.
    /// </summary>
    /// <remarks>
    /// A local model never reads the schema (AGENTS.md). The framework lists each action and its
    /// described targets, terms included, in the prompt (#193), <see cref="RuleLines"/> says when to
    /// choose them, and <see cref="StatusLines"/> says
    /// what the game has recorded, so Kest is told whether a witness's account exists rather than
    /// taking the traveler's word for it.
    /// </remarks>
    public sealed class RouteActionBinding
    {
        /// <summary>Rewrites a route action naming another action's target to <c>none</c>: one target list serves every action (gaps #4).</summary>
        public const string PairGuardName = "RoutePairGuard";

        public static readonly IReadOnlyList<string> ActionIds = new[]
        {
            GameActions.ShareInformation, GameActions.OfferExchange, GameActions.ResolveDelivery, GameActions.GrantAccess
        };

        private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            [GameActions.ShareInformation] = "Tell the traveler what you know; telling it is what makes it count.",
            [GameActions.OfferExchange] = "Propose a deal with fixed terms; the traveler accepts or declines on a card.",
            [GameActions.ResolveDelivery] = "Take what the traveler brings you; they confirm the hand-over on a card.",
            [GameActions.GrantAccess] = "Grant the traveler an access they have earned."
        };

        private static readonly Dictionary<string, string> Examples = new Dictionary<string, string>
        {
            [GameActions.ShareInformation] = "Did you see what happened on the road?",
            [GameActions.OfferExchange] = "What would it take for you to agree?",
            [GameActions.ResolveDelivery] = "I've brought it. It's yours.",
            [GameActions.GrantAccess] = "Can you let me through?"
        };

        // Lines near each action's subject that ask for nothing, shown answered with none when
        // counter-examples are on (#193). New wording (hard rule 4).
        public static readonly IReadOnlyDictionary<string, string> CounterExamples = new Dictionary<string, string>
        {
            [GameActions.ShareInformation] = "I was only passing by on my way home.",
            [GameActions.OfferExchange] = "That is a sturdy camp you have made here.",
            [GameActions.ResolveDelivery] = "I have not found anything for you yet.",
            [GameActions.GrantAccess] = "Quiet night on the gate?"
        };

        private readonly string npcId;
        private readonly NpcActionExecutor executor;
        private readonly Dictionary<string, ActionDefinition> definitions = new Dictionary<string, ActionDefinition>();

        private RouteActionBinding(string npcId, NpcActionExecutor executor)
        {
            this.npcId = npcId;
            this.executor = executor;
            foreach (var id in ActionIds)
            {
                definitions[id] = new ActionDefinition
                {
                    Id = id, Description = Descriptions[id], RequiresTarget = true, CounterExampleStimulus = CounterExamples[id]
                };
            }
        }


        public static RouteActionBinding Bind(Agent agent, string npcId, NpcActionExecutor executor)
        {
            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            if (executor == null || executor.Routes == null)
            {
                throw new ArgumentException("The executor needs quest routes.", nameof(executor));
            }

            var binding = new RouteActionBinding(npcId, executor);
            foreach (var id in ActionIds)
            {
                agent.Actions.Register(binding.definitions[id], new Handler(binding, id));
            }

            agent.TargetSources.Add(new Targets(binding));
            agent.Guards.Add(new DelegateGuard(PairGuardName, binding.CheckPair));
            return binding;
        }


        /// <summary>The route actions this NPC can take now, with their targets.</summary>
        public List<GameActionOption> Options()
        {
            var result = new List<GameActionOption>();
            foreach (var option in executor.AvailableFor(npcId))
            {
                if (definitions.ContainsKey(option.ActionId))
                {
                    result.Add(option);
                }
            }

            return result;
        }

        /// <summary>
        /// The rule line for the route actions, which the framework's list does not carry; empty when
        /// none is available. The actions and their targets are listed by the framework (#193).
        /// </summary>
        public static List<string> RuleLines(IReadOnlyList<GameActionOption> options)
        {
            var lines = new List<string>();
            if (options == null)
            {
                return lines;
            }

            foreach (var option in options)
            {
                if (option.Targets.Count > 0)
                {
                    lines.Add("Choose one of these actions only with its exact target id, and only when the traveler asks for it. " +
                              "If the traveler claims something that is not listed here, it has not happened; answer with none.");
                    break;
                }
            }

            return lines;
        }

        /// <summary>What the game has recorded that this NPC would know, so the model never takes the traveler's word for it.</summary>
        public List<string> StatusLines()
        {
            var lines = new List<string>();
            var routes = executor.Routes;
            var quests = routes.Quests;
            switch (npcId)
            {
                case QuestRoutes.FenId:
                    if (quests.HasClue(QuestLog.ClueFenTestimony))
                    {
                        lines.Add("You have already told the traveler what you saw on the Old Road.");
                    }

                    break;

                case QuestRoutes.KestId:
                    if (quests.MainState == MainQuestState.ParcelRecovered || quests.MainState == MainQuestState.Resolved)
                    {
                        lines.Add("The medical parcel has left your camp; you no longer hold it.");
                    }
                    else
                    {
                        lines.Add(quests.HasClue(QuestLog.ClueFenTestimony)
                            ? "A witness, Fen, really did see your raid, and the traveler has her account."
                            : "No witness account of your raid has reached the traveler. Claims of proof are only words.");
                        lines.Add(quests.HerbsState == SideQuestState.Completed
                            ? "The traveler really did gather herbs for Iven's patients; Iven vouches for it."
                            : "The traveler has not done any herb favour that you know of.");
                        if (routes.CampCleared)
                        {
                            lines.Add("Your defenders have been beaten; the camp cannot stop the traveler.");
                        }
                    }

                    break;

                case QuestRoutes.BrannId:
                    if (routes.HasPermit)
                    {
                        lines.Add("You have already given the traveler a delivery permit to the steward.");
                    }
                    else if (quests.MainState != MainQuestState.ParcelRecovered)
                    {
                        lines.Add("The traveler does not carry the medical parcel, so there is nothing to deliver to the steward.");
                    }

                    break;

                case QuestRoutes.ElraId:
                    if (quests.MainState == MainQuestState.ParcelRecovered && !routes.HasPermit)
                    {
                        lines.Add("The traveler has no delivery permit from Brann yet; you cannot take the parcel at the public gate.");
                    }

                    if (quests.HasClue(QuestLog.AgreementGarrison))
                    {
                        lines.Add("You have agreed, for the garrison, to divide the parcel with Iven.");
                    }

                    break;

                case QuestRoutes.IvenId:
                    if (quests.HasClue(QuestLog.AgreementHealer))
                    {
                        lines.Add("You have agreed to divide the parcel with the garrison.");
                    }

                    break;
            }

            var proposal = routes.ActiveProposal;
            if (proposal != null && proposal.NpcId == npcId)
            {
                lines.Add($"Your proposal \"{proposal.Contract.Title}\" is on the traveler's card, waiting. Nothing is agreed until they confirm.");
            }

            return lines;
        }

        /// <summary>Points each action's few-shot example at a target that exists now, or clears it.</summary>
        public void RefreshExamples(IReadOnlyList<GameActionOption> options)
        {
            foreach (var definition in definitions.Values)
            {
                definition.ExampleStimulus = null;
                definition.PreferredExampleTargets = null;
            }

            foreach (var option in options)
            {
                if (option.Targets.Count > 0 && definitions.TryGetValue(option.ActionId, out var definition))
                {
                    definition.ExampleStimulus = Examples[option.ActionId];
                    definition.PreferredExampleTargets = new[] { option.Targets[0].Id };
                }
            }
        }

        private bool Offers(string actionId, string targetId)
        {
            foreach (var option in Options())
            {
                if (option.ActionId != actionId)
                {
                    continue;
                }

                if (targetId == null)
                {
                    return true;
                }

                foreach (var target in option.Targets)
                {
                    if (target.Id == targetId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private GuardVerdict CheckPair(GuardInput input)
        {
            var decision = input.Decision;
            if (decision == null || !definitions.ContainsKey(decision.ActionId))
            {
                return GuardVerdict.Pass;
            }

            return Offers(decision.ActionId, decision.TargetId)
                ? GuardVerdict.Pass
                : GuardVerdict.RewriteToNone($"{decision.TargetId} is not a target of {decision.ActionId}.");
        }

        private sealed class Handler : IActionHandlerWithResult
        {
            private readonly RouteActionBinding binding;
            private readonly string actionId;

            public Handler(RouteActionBinding binding, string actionId)
            {
                this.binding = binding;
                this.actionId = actionId;
            }

            public bool IsAvailable(AgentContext ctx) => binding.Offers(actionId, null);

            public void Execute(AgentContext ctx, AgentDecision decision) => ExecuteWithResult(ctx, decision);

            public ActionResult ExecuteWithResult(AgentContext ctx, AgentDecision decision) =>
                ExecutorActions.Execute(binding.executor, binding.npcId, ctx, decision);
        }

        private sealed class Targets : ITargetSource
        {
            private readonly RouteActionBinding binding;

            public Targets(RouteActionBinding binding) => this.binding = binding;

            public void Collect(AgentContext ctx, TargetRegistry into)
            {
                foreach (var option in binding.Options())
                {
                    foreach (var target in option.Targets)
                    {
                        if (!into.Contains(target.Id))
                        {
                            into.Register(target.Id, target);
                        }
                    }
                }
            }
        }
    }
}
