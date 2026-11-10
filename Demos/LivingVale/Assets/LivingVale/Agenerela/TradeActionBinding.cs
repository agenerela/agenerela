using System;
using System.Collections.Generic;
using Agenerela;
using LivingVale.Game;
using LivingVale.Interaction;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// Registers G3's trade actions, <c>quote_purchase</c> and <c>quote_sale</c>, with one NPC's
    /// <see cref="Agent"/>, all of it through the framework's public API. What the NPC may offer
    /// comes from <see cref="NpcActionExecutor.AvailableFor"/>, asked afresh on every decision:
    /// <list type="bullet">
    /// <item><description>Availability: each action's handler is available only while the executor lists it, so an action with nothing to offer is absent from the schema.</description></item>
    /// <item><description>Targets: a target source registers the listed target IDs on each decision, and again in <see cref="Agent.Execute"/>, so a target that went stale is refused there.</description></item>
    /// <item><description>Pairs: stock entries and the traveler's sale bundles are registered in their own target categories, which each action declares (#208), so the framework's <c>TargetCategoryGuard</c> rewrites a quote naming the other action's target to <c>none</c>.</description></item>
    /// <item><description>Execution: the handlers call the same <see cref="NpcActionExecutor.Execute"/> the scripted driver does, with the turn's conversation generation, so both drivers share one path.</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// A local model never reads the schema (AGENTS.md). The framework lists the actions and their
    /// described targets in the prompt (#193), <see cref="RuleLines"/> says when to choose them, and <see cref="RefreshExamples"/> gives each
    /// available action a one-line example on a real target, which the few-shot block shows.
    /// </remarks>
    public sealed class TradeActionBinding
    {
        /// <summary>The target category of this NPC's stock entries, which <c>quote_purchase</c> takes.</summary>
        public const string StockCategory = "stock";

        /// <summary>The target category of the traveler's sale bundles, which <c>quote_sale</c> takes.</summary>
        public const string PackCategory = "pack";

        private readonly string npcId;
        private readonly NpcActionExecutor executor;
        private readonly ActionDefinition purchase;
        private readonly ActionDefinition sale;

        private TradeActionBinding(string npcId, NpcActionExecutor executor)
        {
            this.npcId = npcId;
            this.executor = executor;
            purchase = new ActionDefinition
            {
                Id = GameActions.QuotePurchase,
                Description = "Offer the traveler an exact price for one of your stock entries.",
                RequiresTarget = true,
                TargetCategories = new[] { StockCategory },
                CounterExampleStimulus = PurchaseCounterExample
            };
            sale = new ActionDefinition
            {
                Id = GameActions.QuoteSale,
                Description = "Offer to buy something the traveler carries, at an exact price.",
                RequiresTarget = true,
                TargetCategories = new[] { PackCategory },
                CounterExampleStimulus = SaleCounterExample
            };
        }

        /// <summary>Binds both actions and the target source to <paramref name="agent"/>.</summary>
        public static TradeActionBinding Bind(Agent agent, string npcId, NpcActionExecutor executor)
        {
            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            if (executor == null)
            {
                throw new ArgumentNullException(nameof(executor));
            }

            var binding = new TradeActionBinding(npcId, executor);
            agent.Actions.Register(binding.purchase, new Handler(binding, GameActions.QuotePurchase));
            agent.Actions.Register(binding.sale, new Handler(binding, GameActions.QuoteSale));
            agent.TargetSources.Add(new Targets(binding));
            return binding;
        }

        /// <summary>
        /// What really happened to this NPC's trades, from game state (design §11): its offer still
        /// waiting for the player, and the receipt of the last one the player confirmed or that was
        /// refused. A decision only asks for a quote, so without these lines the model could not tell
        /// a sale that went through from an offer nobody accepted.
        /// </summary>
        /// <param name="lastReceipt">This NPC's last receipt, which the driver keeps from its start, so a trade made through the scripted driver counts too.</param>
        public List<string> StatusLines(TradeReceipt lastReceipt)
        {
            var lines = new List<string>();
            var trade = executor.Trade;
            var quote = trade.ActiveQuote;
            if (quote != null && quote.NpcId == npcId && quote.State == TradeQuoteState.Pending)
            {
                string item = trade.Inventory.Catalog.TryGet(quote.ItemId, out var definition) ? definition.DisplayName : quote.ItemId;
                lines.Add(quote.Direction == TradeDirection.Purchase
                    ? $"Your offer is waiting for the traveler to confirm: they buy {item} ×{quote.Quantity} for {quote.Total} gold. Nothing has changed hands yet."
                    : $"Your offer is waiting for the traveler to confirm: you buy their {item} ×{quote.Quantity} for {quote.Total} gold. Nothing has changed hands yet.");
            }

            if (lastReceipt != null)
            {
                lines.Add("Last trade with the traveler: " + ConversationTradeLink.ReceiptLine(lastReceipt, trade.Inventory.Catalog));
            }

            return lines;
        }

        /// <summary>The trade actions the executor offers this NPC now; other actions, such as <c>offer_quest</c>, have their own binding.</summary>
        public IReadOnlyList<GameActionOption> Options()
        {
            var options = new List<GameActionOption>();
            foreach (var option in executor.AvailableFor(npcId))
            {
                if (option.ActionId == GameActions.QuotePurchase || option.ActionId == GameActions.QuoteSale)
                {
                    options.Add(option);
                }
            }

            return options;
        }

        /// <summary>The rule line written before the trade actions, as G5b wrote it: the control arm of G5f's A/B.</summary>
        public const string DefaultRule =
            "When the traveler asks to buy or sell, choose the matching action with one of its exact target ids below; otherwise choose none. " +
            "Nothing changes hands until the traveler confirms your offer.";

        /// <summary>G5f's stricter rule line: an offer only when asked, and what "not asked" looks like.</summary>
        public const string OnlyWhenAskedRule =
            "Make an offer (quote_purchase or quote_sale) only when the traveler asks to buy something from you or to sell you something. " +
            "For greetings, questions, rumours and small talk, choose none and just answer. " +
            "Nothing changes hands until the traveler confirms your offer.";

        /// <summary>Lines that mention trade and ask for nothing, shown answered with <c>none</c> when counter-examples are on (#193).</summary>
        public const string PurchaseCounterExample = "Your stall looks well stocked today.";
        public const string SaleCounterExample = "Iron and pelts must be hard to come by these days.";

        /// <summary>
        /// The rule line that goes before the choices: game policy, which the framework's own list of
        /// actions and described targets (<c>DecideOptions.ListActionsAndTargets</c>, #193) does not
        /// carry. With nothing to offer, one line says so.
        /// </summary>
        /// <param name="rule">The rule line; null uses <see cref="DefaultRule"/>.</param>
        public static List<string> RuleLines(IReadOnlyList<GameActionOption> options, string rule = null)
        {
            if (options == null || options.Count == 0)
            {
                return new List<string> { "You cannot make a trade offer right now." };
            }

            return new List<string> { string.IsNullOrWhiteSpace(rule) ? DefaultRule : rule };
        }

        /// <summary>
        /// Points each action's few-shot example at its first target now, so the example the
        /// framework shows names a real id. Only an action with targets is offered, so only its
        /// example appears.
        /// </summary>
        public void RefreshExamples(IReadOnlyList<GameActionOption> options)
        {
            purchase.ExampleStimulus = null;
            purchase.PreferredExampleTargets = null;
            sale.ExampleStimulus = null;
            sale.PreferredExampleTargets = null;

            foreach (var option in options ?? Array.Empty<GameActionOption>())
            {
                if (option.Targets.Count == 0)
                {
                    continue;
                }

                var first = option.Targets[0];
                if (option.ActionId == GameActions.QuotePurchase)
                {
                    purchase.ExampleStimulus = $"I'll take {Lower(first.Name)}, please.";
                    purchase.PreferredExampleTargets = new[] { first.Id };
                }
                else if (option.ActionId == GameActions.QuoteSale)
                {
                    sale.ExampleStimulus = $"Would you buy my {Lower(StripOwner(first.Name))}?";
                    sale.PreferredExampleTargets = new[] { first.Id };
                }
            }
        }

        private static string Lower(string text) => string.IsNullOrEmpty(text) ? "" : char.ToLowerInvariant(text[0]) + text.Substring(1);

        private static string StripOwner(string name) =>
            name != null && name.StartsWith("Player's ", StringComparison.Ordinal) ? name.Substring("Player's ".Length) : name;

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

        private sealed class Handler : IActionHandlerWithResult
        {
            private readonly TradeActionBinding binding;
            private readonly string actionId;

            public Handler(TradeActionBinding binding, string actionId)
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
            private readonly TradeActionBinding binding;

            public Targets(TradeActionBinding binding) => this.binding = binding;

            public void Collect(AgentContext ctx, TargetRegistry into)
            {
                foreach (var option in binding.Options())
                {
                    string category = option.ActionId == GameActions.QuotePurchase ? StockCategory : PackCategory;
                    foreach (var target in option.Targets)
                    {
                        if (!into.Contains(target.Id))
                        {
                            into.Register(target.Id, target, category);
                        }
                    }
                }
            }
        }
    }
}
