using System;
using System.Collections.Generic;
using Agenerela;
using LivingVale.Game;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// Registers G4's <c>offer_quest</c> with one NPC's <see cref="Agent"/>, the way
    /// <see cref="TradeActionBinding"/> registers trades: availability and targets come from
    /// <see cref="NpcActionExecutor.AvailableFor"/> on every decision, so the action is absent from the
    /// schema once the quest can no longer be offered, and the handler calls the same
    /// <see cref="NpcActionExecutor.Execute"/> the scripted driver does. Executing only puts the offer
    /// on a card; the player's Accept button moves the quest, never the model.
    /// </summary>
    /// <remarks>
    /// A local model never reads the schema (AGENTS.md). The framework lists the action and its
    /// described target in the prompt (#193), <see cref="RuleLines"/> says when to choose it, and <see cref="StatusLines"/> tells the model where the quest stands, from
    /// game state, so it does not offer what the traveler already accepted or claim an agreement
    /// nobody made.
    /// </remarks>
    public sealed class QuestActionBinding
    {
        /// <summary>
        /// The target category of the quests an NPC can offer, which <c>offer_quest</c> takes (#208):
        /// the framework's <c>TargetCategoryGuard</c> rewrites an <c>offer_quest</c> naming a trade or
        /// route target to <c>none</c>.
        /// </summary>
        public const string QuestCategory = "quest";

        private readonly string npcId;
        private readonly NpcActionExecutor executor;
        private readonly ActionDefinition offer;

        private QuestActionBinding(string npcId, NpcActionExecutor executor)
        {
            this.npcId = npcId;
            this.executor = executor;
            offer = new ActionDefinition
            {
                Id = GameActions.OfferQuest,
                Description = "Ask the traveler to take on your task; they accept or decline on a card.",
                RequiresTarget = true,
                TargetCategories = new[] { QuestCategory },
                CounterExampleStimulus = CounterExample
            };
        }


        public static QuestActionBinding Bind(Agent agent, string npcId, NpcActionExecutor executor)
        {
            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            if (executor == null)
            {
                throw new ArgumentNullException(nameof(executor));
            }

            var binding = new QuestActionBinding(npcId, executor);
            agent.Actions.Register(binding.offer, new ExecutorHandler(executor, npcId, GameActions.OfferQuest));
            agent.TargetSources.Add(new Targets(binding));
            return binding;
        }


        /// <summary>The <c>offer_quest</c> option for this NPC now, or null.</summary>
        public GameActionOption Option()
        {
            foreach (var option in executor.AvailableFor(npcId))
            {
                if (option.ActionId == GameActions.OfferQuest)
                {
                    return option;
                }
            }

            return null;
        }

        /// <summary>A line near the quest's subject that asks for nothing, shown answered with <c>none</c> when counter-examples are on (#193).</summary>
        public const string CounterExample = "Your patients look like they are in good hands.";

        /// <summary>
        /// The rule line for <c>offer_quest</c>, which the framework's list does not carry; empty when
        /// it is not available. The action and its targets are listed by the framework (#193).
        /// </summary>
        public static List<string> RuleLines(GameActionOption option)
        {
            var lines = new List<string>();
            if (option == null || option.Targets.Count == 0)
            {
                return lines;
            }

            lines.Add($"When the traveler asks about your trouble or offers to help, choose {option.ActionId} with its exact target id below. " +
                "The traveler accepts or declines on a card; nothing is agreed until they do.");
            return lines;
        }

        /// <summary>Where this NPC's quest stands, from the quest log. Only Iven has one so far.</summary>
        public List<string> StatusLines()
        {
            var lines = new List<string>();
            var quests = executor.Quests;
            if (quests == null || npcId != QuestLog.IvenId)
            {
                return lines;
            }

            if (quests.ActiveOffer != null && quests.ActiveOffer.NpcId == npcId)
            {
                lines.Add("Your request for help is on the traveler's card, waiting for them to accept. They have not agreed yet.");
            }

            switch (quests.MainState)
            {
                case MainQuestState.Active:
                    lines.Add("The traveler has agreed to look for the medicine wagon. They have not found it yet.");
                    break;
                case MainQuestState.ParcelRecovered:
                    lines.Add("The traveler has recovered the medical parcel from the wagon, but has not decided where it goes.");
                    break;
                case MainQuestState.Resolved:
                    lines.Add(quests.Disposition == ParcelDisposition.Healer
                        ? "The traveler brought you the medical parcel; your patients are being treated."
                        : quests.Disposition == ParcelDisposition.Garrison
                            ? "The traveler gave the medical parcel to the castle garrison, not to you."
                            : "The medical parcel was divided between you and the garrison, as both agreed.");
                    break;
            }

            return lines;
        }

        /// <summary>Points the few-shot example at the real offer id, or clears it when nothing can be offered.</summary>
        public void RefreshExample(GameActionOption option)
        {
            if (option == null || option.Targets.Count == 0)
            {
                offer.ExampleStimulus = null;
                offer.PreferredExampleTargets = null;
                return;
            }

            offer.ExampleStimulus = "Is there anything I can do for you?";
            offer.PreferredExampleTargets = new[] { option.Targets[0].Id };
        }

        private sealed class Targets : ITargetSource
        {
            private readonly QuestActionBinding binding;

            public Targets(QuestActionBinding binding) => this.binding = binding;

            public void Collect(AgentContext ctx, TargetRegistry into)
            {
                var option = binding.Option();
                if (option == null)
                {
                    return;
                }

                foreach (var target in option.Targets)
                {
                    if (!into.Contains(target.Id))
                    {
                        into.Register(target.Id, target, QuestCategory);
                    }
                }
            }
        }
    }
}
