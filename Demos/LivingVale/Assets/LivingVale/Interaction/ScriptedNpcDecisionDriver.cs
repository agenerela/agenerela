using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Game;

namespace LivingVale.Interaction
{
    /// <summary>How the scripted driver answers the next turns: the failure cases G1 must survive, injected through the same boundary a real driver uses.</summary>
    public enum ScriptedFixture
    {
        /// <summary>A short, slightly variable delay, then a line from the NPC's script.</summary>
        Normal,

        /// <summary>A reply after <see cref="ScriptedNpcDecisionDriver.SlowDelay"/>: long enough to switch speakers or cancel first.</summary>
        Slow,

        /// <summary>Never answers on its own; only cancellation or the coordinator's timeout ends the turn.</summary>
        NeverAnswers,

        /// <summary>Answers with something that is not a decision.</summary>
        Malformed,

        /// <summary>The backend throws.</summary>
        Throws
    }

    /// <summary>
    /// A scripted trade request: when the player's line contains any of <see cref="Verbs"/> and any
    /// of <see cref="Nouns"/>, the NPC takes <see cref="ActionId"/> on <see cref="TargetId"/> if the
    /// executor offers that pair now; otherwise it says <see cref="UnavailableLine"/>.
    /// </summary>
    public sealed class ScriptedTradeRule
    {
        public string[] Verbs { get; }
        public string[] Nouns { get; }
        public string ActionId { get; }
        public string TargetId { get; }
        public string Line { get; }
        public string UnavailableLine { get; }

        /// <summary>When the pair is not available now, try the next rule instead of saying <see cref="UnavailableLine"/> (G4c).</summary>
        public bool SkipWhenUnavailable { get; }

        public ScriptedTradeRule(string[] verbs, string[] nouns, string actionId, string targetId, string line, string unavailableLine,
            bool skipWhenUnavailable = false)
        {
            SkipWhenUnavailable = skipWhenUnavailable;
            Verbs = verbs;
            Nouns = nouns;
            ActionId = actionId;
            TargetId = targetId;
            Line = line;
            UnavailableLine = unavailableLine;
        }
    }

    /// <summary>
    /// The G1–G4 development fixture: keyword-matched lines per NPC, after a delay. Without an
    /// executor every decision is <c>none / no_target</c>, as in G1; with one, trade rules turn buy
    /// and sell requests into <c>quote_purchase</c> and <c>quote_sale</c> decisions, carried out by
    /// the same <see cref="NpcActionExecutor"/> a model-backed driver uses. It is not a model and
    /// must never be presented as one, hence <see cref="Label"/>.
    /// </summary>
    public sealed partial class ScriptedNpcDecisionDriver
    {
        private readonly Dictionary<string, NpcScript> scripts = new Dictionary<string, NpcScript>();
        private readonly Dictionary<string, List<ScriptedTradeRule>> tradeRules = new Dictionary<string, List<ScriptedTradeRule>>();
        private readonly Random random;
        private readonly NpcActionExecutor executor;

        /// <param name="seed">Fixes the delays' jitter and the fallback line order, for tests.</param>
        /// <param name="executor">Carries out decisions; null keeps G1's behaviour, where only <c>none</c> executes.</param>
        public ScriptedNpcDecisionDriver(int seed = 0, NpcActionExecutor executor = null)
        {
            random = new Random(seed);
            this.executor = executor;
            AddG1Scripts();
            RegionNpcScripts.AddTo(this); // G4b: the six region NPCs, general conversation only.
            AddG3TradeRules();
            AddG4cRouteRules(); // Before G4's offer rule, so "here is the parcel" is a delivery, not a new offer.
            AddG4QuestRules();
        }

        public string Label => Fixture == ScriptedFixture.Normal ? "Scripted replies" : $"Scripted replies ({Fixture})";

        /// <summary>Applies to every turn started after it is set.</summary>
        public ScriptedFixture Fixture { get; set; } = ScriptedFixture.Normal;

        /// <summary>Delay before a <see cref="ScriptedFixture.Normal"/> reply, before jitter of up to half as much again.</summary>
        public TimeSpan NormalDelay { get; set; } = TimeSpan.FromMilliseconds(600);

        public TimeSpan SlowDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>Adds or replaces an NPC's script. A keyword matches anywhere in the player's line, ignoring case; the first match wins.</summary>
        public void SetScript(string npcId, IEnumerable<(string keyword, string line)> rules, IEnumerable<string> fallbacks)
        {
            var script = new NpcScript();
            foreach (var rule in rules)
            {
                script.Rules.Add(rule);
            }

            script.Fallbacks.AddRange(fallbacks);
            if (script.Fallbacks.Count == 0)
            {
                script.Fallbacks.Add("Hm.");
            }

            scripts[npcId] = script;
        }

        /// <summary>
        /// What the script decides for this line, with no delay or fixture: a trade, quest or route
        /// rule when one matches and game state offers it, otherwise the NPC's keyword line.
        /// <c>ScriptedReplies</c> answers through the framework's <c>ScriptedProvider</c> with it.
        /// </summary>
        public AgentDecision Choose(string npcId, string playerLine)
        {
            playerLine = playerLine ?? "";
            if (TryTrade(npcId, playerLine, out var trade))
            {
                return trade;
            }

            return new AgentDecision(ActionRegistry.None, TargetRegistry.NoTarget, LineFor(npcId, playerLine));
        }

        /// <summary>The wait before the next reply for the current <see cref="Fixture"/>: <see cref="SlowDelay"/>, or <see cref="NormalDelay"/> with jitter.</summary>
        public TimeSpan NextDelay()
        {
            return Fixture == ScriptedFixture.Slow ? SlowDelay : Jittered(NormalDelay);
        }

        /// <summary>Adds trade rules for an NPC, checked in order before its keyword lines. Ignored without an executor.</summary>
        public void AddTradeRules(string npcId, IEnumerable<ScriptedTradeRule> rules)
        {
            if (!tradeRules.TryGetValue(npcId, out var list))
            {
                list = new List<ScriptedTradeRule>();
                tradeRules[npcId] = list;
            }

            list.AddRange(rules);
        }

        private bool TryTrade(string npcId, string playerLine, out AgentDecision decision)
        {
            decision = null;
            if (executor == null || !tradeRules.TryGetValue(npcId, out var rules))
            {
                return false;
            }

            foreach (var rule in rules)
            {
                if (!ContainsAny(playerLine, rule.Verbs) || !ContainsAny(playerLine, rule.Nouns))
                {
                    continue;
                }

                // Without a quest log, quest rules do not apply, and the NPC keeps its G1 lines.
                if (rule.ActionId == GameActions.OfferQuest && executor.Quests == null)
                {
                    continue;
                }

                // Likewise G4c's route rules without the routes.
                if (IsRouteRule(rule) && executor.Routes == null)
                {
                    continue;
                }

                // Only what game state offers now, as a model sees only what is registered.
                bool available = IsAvailable(npcId, rule.ActionId, rule.TargetId);
                if (!available && rule.SkipWhenUnavailable)
                {
                    continue;
                }

                decision = available
                    ? new AgentDecision(rule.ActionId, rule.TargetId, rule.Line)
                    : new AgentDecision(ActionRegistry.None, TargetRegistry.NoTarget, rule.UnavailableLine);
                return true;
            }

            return false;
        }

        private bool IsAvailable(string npcId, string actionId, string targetId)
        {
            foreach (var option in executor.AvailableFor(npcId))
            {
                if (option.ActionId != actionId)
                {
                    continue;
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

        private static bool ContainsAny(string text, string[] words)
        {
            foreach (var word in words)
            {
                if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private string LineFor(string npcId, string playerLine)
        {
            if (!scripts.TryGetValue(npcId, out var script))
            {
                return "I don't think we've met.";
            }

            foreach (var (keyword, line) in script.Rules)
            {
                if (playerLine.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return line;
                }
            }

            return script.Fallbacks[random.Next(script.Fallbacks.Count)];
        }

        private TimeSpan Jittered(TimeSpan delay)
        {
            return delay + TimeSpan.FromMilliseconds(random.NextDouble() * delay.TotalMilliseconds * 0.5);
        }

        // Placeholder lines for the two G1 NPCs; real dialogue comes from the model at G5.
        private void AddG1Scripts()
        {
            SetScript("npc_mara",
                new[]
                {
                    ("potion", "Healing potions are ten gold each. I'll have stock again once the wagon turns up."),
                    ("wagon", "The medicine wagon never reached us. Ask Iven, he was waiting on it more than anyone."),
                    ("buy", "Potions, bread and herb bundles. Say which and I'll name a price."),
                    ("sell", "I'll take wolf pelts, iron scrap and herbs off your hands."),
                    ("hello", "Morning, traveler. Mind the mud."),
                    ("bye", "Safe roads.")
                },
                new[]
                {
                    "Can't say I know much about that.",
                    "You're not from the vale, are you?",
                    "Trade's slow since the road went quiet."
                });

            SetScript("npc_iven",
                new[]
                {
                    ("medicine", "Without that wagon I have three patients and nothing to give them."),
                    ("wagon", "It left the castle two days ago. It should have been here by now."),
                    ("herb", "Herbs grow in Herb Hollow, west of the village, but they're no substitute for the real supplies."),
                    ("hello", "Hello. Forgive me, I'm distracted."),
                    ("bye", "Go carefully.")
                },
                new[]
                {
                    "I'm sorry, I don't follow.",
                    "Talk to Mara at the stall, she hears everything.",
                    "Not now, please. Unless it's about the wagon."
                });
        }

        // Mara's trades. Sale rules come first, so "buy my pelts" is a sale, not a purchase.
        private void AddG3TradeRules()
        {
            var sellVerbs = new[] { "sell my", "sell you", "sell some", "sell these", "sell the", "sell a ", "sell this", "to sell", "i'll sell", "buy my" };
            var buyVerbs = new[] { "buy", "purchase", "i'll take", "i need", "i want", "give me" };
            AddTradeRules(VendorStock.MaraId, new[]
            {
                new ScriptedTradeRule(sellVerbs, new[] { "pelt" }, GameActions.QuoteSale, "sale_mara_wolf_pelt_one",
                    "A wolf pelt? I can use that.", "You don't have a pelt on you."),
                new ScriptedTradeRule(sellVerbs, new[] { "scrap", "iron" }, GameActions.QuoteSale, "sale_mara_iron_scrap_one",
                    "Iron scrap. The smith will want it.", "You've no scrap to sell."),
                new ScriptedTradeRule(sellVerbs, new[] { "herb" }, GameActions.QuoteSale, "sale_mara_herb_one",
                    "Herbs, good. Iven always needs more.", "You aren't carrying any herbs."),
                new ScriptedTradeRule(buyVerbs, new[] { "potion", "heal" }, GameActions.QuotePurchase, "stock_mara_potion_one",
                    "One healing potion. Here's my price.", "I'm out of potions until the wagon comes."),
                new ScriptedTradeRule(buyVerbs, new[] { "bread", "food" }, GameActions.QuotePurchase, "stock_mara_bread_one",
                    "Fresh this morning. One loaf?", "No bread left today."),
                new ScriptedTradeRule(buyVerbs, new[] { "herb" }, GameActions.QuotePurchase, "stock_mara_herb_one",
                    "A bundle of herbs, then.", "I've sold the last of my herbs.")
            });
        }

        // Iven offers the main quest on any wagon or medicine line while it can still be offered (G4).
        // Once it is accepted the offer is no longer available, and he answers with a status line.
        private void AddG4QuestRules()
        {
            var topics = new[] { "wagon", "medicine", "parcel", "help" };
            AddTradeRules(QuestLog.IvenId, new[]
            {
                new ScriptedTradeRule(topics, topics, GameActions.OfferQuest, QuestLog.IvenOfferId,
                    "The medicine wagon never arrived. Will you find it for me?",
                    "The wagon is still out there. Bring back whatever you find of it.")
            });
        }

        private sealed class NpcScript
        {
            public readonly List<(string keyword, string line)> Rules = new List<(string, string)>();
            public readonly List<string> Fallbacks = new List<string>();
        }
    }
}
