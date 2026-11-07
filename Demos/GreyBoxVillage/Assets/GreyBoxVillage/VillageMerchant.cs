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
    ///
    /// When there is trouble at the market, a thief running off with a ware or a fire, she can
    /// <c>call_guard</c>. Its target is a guard, offered by <see cref="GuardTargetSource"/>, and the call
    /// becomes that guard's stimulus, with her words, labelled Merchant: the same way the captain's
    /// <c>send_guard</c> works (DR-008).
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
        [Tooltip("The thief who robs her counter; call_guard is offered while it runs.")]
        public VillageThief Thief;
        [Tooltip("The fire at the market; call_guard is offered while it burns.")]
        public VillageFire Fire;

        private readonly Dictionary<Targetable, float> restockAt = new Dictionary<Targetable, float>();
        private string activity;
        private Transform calling;

        public override string Activity => activity ??= "minding your stall at the market";
        public override bool IsBusy => false;

        private static readonly IReadOnlyList<string> SuggestedLines = System.Array.AsReadOnly(new[]
        { "What are you selling?", "Got any work for me?", "I'll take some bread.", "Could you spare an apple?", "I found your moonpetals." });

        public override IReadOnlyList<string> Suggestions => SuggestedLines;

        public override IEnumerable<string> Facts()
        {
            foreach (var fact in base.Facts()) yield return fact;
            var onCounter = Stock.Where(t => t != null && t.gameObject.activeInHierarchy)
                .Select(t => t.Id + " (" + Price(t) + (Price(t) == 1 ? " coin)" : " coins)")).ToList();
            yield return onCounter.Count == 0 ? "Your counter is empty." : "On your counter: " + string.Join(", ", onCounter) + ".";
            if (Inventory != null)
                yield return "The player has " + Inventory.Coins + " coins and carries " + Inventory.Describe() + ".";
            if (Thief != null && Thief.IsRunning && Thief.Loot != null)
                yield return "A thief is running for the eastern_road with your " + Thief.Loot.Id + ".";
            if (Fire != null && Fire.IsBurning) yield return "There is a fire at " + Fire.Where + ".";
            // The guards are offered by their own source, so the local model needs their ids here.
            var guards = CallableGuards()
                .Select(g => IdOf(g.transform) + " (" + Vector3.Distance(g.transform.position, transform.position).ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                             " m away, " + g.Activity + ")").ToList();
            if (guards.Count > 0) yield return "Guards you can call: " + string.Join(", ", guards) + ".";
        }

        public override IEnumerable<ITargetSource> TargetSources()
        {
            foreach (var source in base.TargetSources()) yield return source;
            yield return new GuardTargetSource(this);
        }

        /// <summary>
        /// The guards <c>call_guard</c> may name: every guard while a thief runs (Bram catches one
        /// with <c>attack_target</c>), but for a fire alone only those who can carry water (a
        /// guard whose agent has <c>fetch_water</c>), so the call never goes to a guard with nothing to do about it.
        /// </summary>
        public IEnumerable<VillageGuard> CallableGuards()
        {
            var commands = Village();
            if (commands == null) yield break;
            bool fireOnly = Fire != null && Fire.IsBurning && !(Thief != null && Thief.IsRunning);
            foreach (var mind in commands.Minds)
                if (mind != null && mind.Body is VillageGuard guard && (!fireOnly || mind.Agent.Actions.TryGet("fetch_water", out _, out _)))
                    yield return guard;
        }

        private VillageCommands Village()
        {
            var mind = GetComponent<AgentMind>();
            return mind != null ? mind.Village : null;
        }

        public override void ResetState()
        {
            activity = null;
            calling = null;
            restockAt.Clear();
            foreach (var t in Stock)
                if (t != null) t.gameObject.SetActive(true);
        }

        private static int Price(Targetable item) => ItemCatalog.Get(item.Id)?.Price ?? 0;

        // ---- Actions ---------------------------------------------------------------------------

        [AgentAction("sell", "Sell the player an item from your counter, for its price.", RequiresTarget = true, TargetCategories = new[] { "item" })]
        [Example("How much for the {0}? I'll have it.", "bread", "apple")]
        private void Sell(Transform target)
        {
            var item = target.GetComponent<Targetable>();
            if (!Inventory.Pay(Price(item))) return;
            Hand(item);
            activity = "counting the coins for the " + item.Id;
            Report("Sold the " + item.Id + ".");
        }

        [AgentAction("give", "Give the player an item from your counter for nothing.", RequiresTarget = true, TargetCategories = new[] { "item" })]
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
            Lose(item);
        }

        [AgentAction("call_guard", "Shout for one of the guards to deal with trouble at the market.", RequiresTarget = true,
            TargetCategories = new[] { GuardTargetSource.Category })]
        [Example("Someone just ran off with a pie from your counter!", "wren", "bram")]
        private void CallGuard(Transform guard)
        {
            calling = guard;
            activity = "shouting for " + IdOf(guard);
            Report("Calling " + IdOf(guard) + ".");
        }

        // Trouble the game can see: a thief making off with a ware, or a fire at the market.
        [Available("call_guard")]
        private bool CanCallGuard(AgentContext ctx) => Thief != null && Thief.IsRunning || Fire != null && Fire.IsBurning;

        /// <summary>Passes her call on, with her words, to the guard she named: that guard's next stimulus.</summary>
        public override void AfterAction(string actionId, string statement)
        {
            if (actionId != "call_guard" || calling == null) return;
            var mind = calling.GetComponent<AgentMind>();
            calling = null;
            if (mind == null) return;
            string trouble = Thief != null && Thief.IsRunning && Thief.Loot != null
                ? "a thief has taken the " + Thief.Loot.Id + " from her counter and is running for the eastern_road"
                : Fire != null && Fire.IsBurning ? "there is a fire at " + Fire.Where
                : "there is trouble at her stall";
            string words = string.IsNullOrWhiteSpace(statement) ? "" : " She says: \"" + statement.Trim() + "\"";
            _ = mind.ReportEventAsync("merchant", "Merchant", "Odile shouts for you from the market: " + trouble + "." + words,
                DecisionPriority.Background);
        }

        // ---- The counter -------------------------------------------------------------------------

        /// <summary>The first ware still on the counter, or null when it is bare.</summary>
        public Targetable FirstOnCounter() => Stock.FirstOrDefault(t => t != null && t.gameObject.activeInHierarchy);

        /// <summary>A ware leaves the counter, sold, given or stolen, until it is restocked.</summary>
        public void Lose(Targetable item)
        {
            item.gameObject.SetActive(false);
            restockAt[item] = Time.time + RestockSeconds;
        }

        /// <summary>A ware comes back, such as one a guard took off the thief: on the counter at once.</summary>
        public void Recover(Targetable item)
        {
            restockAt.Remove(item);
            item.gameObject.SetActive(true);
        }

        // ---- Rules -----------------------------------------------------------------------------

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
