using System.Collections.Generic;
using System.Linq;
using Agenerela;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// Odile's body: a merchant behind her stall at the market. What she can sell is what lies on her
    /// counter, each a <see cref="Targetable"/> of category "item"; a sold item leaves the counter,
    /// and so leaves the schema, until it is restocked.
    /// </summary>
    /// <remarks>
    /// Her game guards depend on the item the model picks, so they are guards rather than availability:
    /// <c>cannot_afford</c> contains a sale the player cannot pay for, and <c>no_free_gifts</c> a gift
    /// of anything worth more than two coins.
    /// </remarks>
    public sealed class VillageMerchant : VillageBody
    {
        public const string CannotAfford = "cannot_afford";
        public const string NoFreeGifts = "no_free_gifts";
        /// <summary>The most a gift may be worth, in coins.</summary>
        public const int GiftLimit = 2;

        [Tooltip("The items on her counter, each a Targetable of category item.")]
        public Targetable[] Stock = new Targetable[0];
        public float RestockSeconds = 90f;

        private readonly Dictionary<Targetable, float> restockAt = new Dictionary<Targetable, float>();
        private string activity;

        public override string Activity => activity ??= "minding your stall at the market";
        public override bool IsBusy => false;

        private static readonly string[] SuggestedLines =
            { "What are you selling?", "Got any work for me?", "I'll take some bread.", "Could you spare an apple?", "I found your moonpetals." };

        public override IReadOnlyList<string> Suggestions => SuggestedLines;

        public override IEnumerable<string> Facts()
        {
            foreach (var fact in base.Facts()) yield return fact;
            var onCounter = Stock.Where(t => t != null && t.gameObject.activeInHierarchy)
                .Select(t => t.Id + " (" + Price(t) + (Price(t) == 1 ? " coin)" : " coins)")).ToList();
            yield return onCounter.Count == 0 ? "Your counter is empty." : "On your counter: " + string.Join(", ", onCounter) + ".";
            if (Inventory != null)
                yield return "The player has " + Inventory.Coins + " coins and carries " + Inventory.Describe() + ".";
        }

        public override void ResetState()
        {
            activity = null;
            restockAt.Clear();
            foreach (var t in Stock)
                if (t != null) t.gameObject.SetActive(true);
        }

        private static int Price(Targetable item) => ItemCatalog.Get(item.Id)?.Price ?? 0;

        // ---- Actions ---------------------------------------------------------------------------

        [AgentAction("sell", "Sell the player an item from your counter, for its price.", RequiresTarget = true)]
        [Example("How much for the {0}? I'll have it.", "bread", "apple")]
        private void Sell(Transform target)
        {
            var item = target.GetComponent<Targetable>();
            if (!Inventory.Pay(Price(item))) return;
            Hand(item);
            activity = "counting the coins for the " + item.Id;
            Report("Sold the " + item.Id + ".");
        }

        [AgentAction("give", "Give the player an item from your counter for nothing.", RequiresTarget = true)]
        [Example("I'm starving and I haven't a coin.", "apple", "bread")]
        private void Give(Transform target)
        {
            var item = target.GetComponent<Targetable>();
            Hand(item);
            activity = "handing over the " + item.Id;
            Report("Gave away the " + item.Id + ".");
        }

        // Selling and giving are for the player's own asking, and need something on the counter.
        [Available("sell")]
        private bool CanSell(AgentContext ctx) => Asking(ctx) == "player" && Stock.Any(t => t != null && t.gameObject.activeInHierarchy);

        [Available("give")]
        private bool CanGive(AgentContext ctx) => CanSell(ctx);

        private void Hand(Targetable item)
        {
            Inventory.Add(item.Id);
            item.gameObject.SetActive(false);
            restockAt[item] = Time.time + RestockSeconds;
        }

        // ---- Rules -----------------------------------------------------------------------------

        protected override bool TargetFits(string actionId, object target) =>
            actionId == "sell" || actionId == "give"
                ? target is Component c && Stock.Contains(c.GetComponent<Targetable>())
                : base.TargetFits(actionId, target);

        public override IEnumerable<IDecisionGuard> Guards()
        {
            foreach (var guard in base.Guards()) yield return guard;
            yield return new DelegateGuard(CannotAfford, input =>
            {
                var d = input.Decision;
                if (d.ActionId != "sell" || Inventory == null || !Item(input, out var item)) return GuardVerdict.Pass;
                return Inventory.Coins >= Price(item) ? GuardVerdict.Pass
                    : GuardVerdict.RewriteToNone("The " + item.Id + " costs " + Price(item) + " coins and the player has " + Inventory.Coins + ".");
            });
            yield return new DelegateGuard(NoFreeGifts, input =>
            {
                var d = input.Decision;
                if (d.ActionId != "give" || !Item(input, out var item)) return GuardVerdict.Pass;
                return Price(item) <= GiftLimit ? GuardVerdict.Pass
                    : GuardVerdict.RewriteToNone("The " + item.Id + " is worth " + Price(item) + " coins; Odile gives nothing worth more than " + GiftLimit + ".");
            });
        }

        private static bool Item(GuardInput input, out Targetable item)
        {
            item = null;
            if (!input.Context.Targets.TryGet(input.Decision.TargetId, out var target)) return false;
            item = (target as Component)?.GetComponent<Targetable>();
            return item != null;
        }

        public override string RefusalLine(GuardVerdict verdict) => base.RefusalLine(verdict) ??
            (verdict.Guard == CannotAfford ? "That costs more than you've got, love."
            : verdict.Guard == NoFreeGifts ? "I can't give that away. An apple, maybe."
            : verdict.Guard == "TargetGroundingGuard" ? "I don't sell that."
            : null);

        protected override void Update()
        {
            base.Update();
            foreach (var item in restockAt.Where(kv => Time.time > kv.Value).Select(kv => kv.Key).ToList())
            {
                restockAt.Remove(item);
                item.gameObject.SetActive(true);
            }
        }
    }
}
