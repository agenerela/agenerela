using LivingVale.Game;

namespace LivingVale.Interaction
{
    // G4c: the scripted driver's rules for the quest routes, endings and side quests. Kept in their own
    // file so the region and persona sessions can edit the driver's other rules without conflicts.
    // Each rule asks the shared executor whether its pair is available now, exactly as a model only
    // sees what is registered: "I have proof" reaches Kest's testimony deal only once Fen has told it.
    public sealed partial class ScriptedNpcDecisionDriver
    {
        private static readonly string[] HandOver =
        {
            "here is", "here's", "here are", "here you", "give", "deliver", "hand", "brought", "bring", "take it", "for you", "found"
        };

        private static readonly string[] Favour = { "herb", "favour", "favor", "iven", "healer" };

        private static readonly string[] Division = { "split", "divide", "division", "share it", "share the", "agreed" };

        private static bool IsRouteRule(ScriptedTradeRule rule)
        {
            switch (rule.ActionId)
            {
                case GameActions.ShareInformation:
                case GameActions.OfferExchange:
                case GameActions.ResolveDelivery:
                case GameActions.GrantAccess:
                    return true;
                case GameActions.OfferQuest:
                    return rule.TargetId == QuestRoutes.OfferHerbsIven || rule.TargetId == QuestRoutes.OfferBadgeBrann;
                default:
                    return false;
            }
        }

        private void AddG4cRouteRules()
        {
            AddTradeRules(QuestRoutes.IvenId, new[]
            {
                new ScriptedTradeRule(HandOver, Division, GameActions.ResolveDelivery, QuestRoutes.DeliveryDivisionIven,
                    "As we agreed, then. Half for my patients, half for the castle.", "", skipWhenUnavailable: true),
                new ScriptedTradeRule(Division, Division, GameActions.OfferExchange, QuestRoutes.ExchangeDivisionIven,
                    "Divide it? If the garrison agrees too, I can live with that.", "", skipWhenUnavailable: true),
                new ScriptedTradeRule(HandOver, new[] { "parcel", "medicine" }, GameActions.ResolveDelivery, QuestRoutes.DeliveryParcelIven,
                    "The medicine! Give it here, my patients can't wait.", "", skipWhenUnavailable: true),
                new ScriptedTradeRule(Words("herb", "errand", "anything else", "another task", "more work"), Words("herb", "errand", "anything else", "another task", "more work"),
                    GameActions.OfferQuest, QuestRoutes.OfferHerbsIven,
                    "Would you gather herbs in the Hollow for me? Three bundles. There's no deadline, whatever the name says.", "", skipWhenUnavailable: true),
                new ScriptedTradeRule(HandOver, Words("herb"), GameActions.ResolveDelivery, QuestRoutes.DeliveryHerbsIven,
                    "Three bundles, good ones. Thank you.", "", skipWhenUnavailable: true)
            });

            AddTradeRules(QuestRoutes.FenId, new[]
            {
                new ScriptedTradeRule(Words("wagon", "saw", "see", "road", "raid", "happened", "witness", "know"),
                    Words("wagon", "saw", "see", "road", "raid", "happened", "witness", "know"),
                    GameActions.ShareInformation, QuestRoutes.KnowledgeFenRaid,
                    "I saw it. Raiders from Broken Watch took the wagon, and Kest kept the medicine for their own sick.",
                    "I've told you all I saw: Kest's lot, at Broken Watch.")
            });

            AddTradeRules(QuestRoutes.KestId, new[]
            {
                new ScriptedTradeRule(Words("fen", "witness", "saw you", "proof", "evidence", "seen"), Words("fen", "witness", "saw you", "proof", "evidence", "seen"),
                    GameActions.OfferExchange, QuestRoutes.ExchangeKestTestimony,
                    "Fen saw us, did she. Then holding it only brings the garrison down on us. Take it.",
                    "Proof? Anyone can claim that. Bring me a witness, not a story."),
                // The favour falls through when it is not earned, so "how much for Iven's parcel?" still
                // reaches the payment; its refusal line comes after the payment rule.
                new ScriptedTradeRule(Favour, Favour, GameActions.OfferExchange, QuestRoutes.ExchangeKestFavour,
                    "You gathered for the healer's sick. Then you understand ours. Take the parcel.", "", skipWhenUnavailable: true),
                new ScriptedTradeRule(Words("pay", "gold", "coin", "buy", "price", "how much"), Words("pay", "gold", "coin", "buy", "price", "how much"),
                    GameActions.OfferExchange, QuestRoutes.ExchangeKestPayment,
                    "Twenty gold for the camp's supplies, and the parcel is yours.",
                    "The parcel's gone. Nothing left to sell."),
                new ScriptedTradeRule(Favour, Favour, GameActions.OfferExchange, QuestRoutes.ExchangeKestFavour,
                    "", "Herbs for the sick would earn some trust. Talk doesn't."),
                new ScriptedTradeRule(Words("parcel", "medicine", "wagon", "release", "give"), Words("parcel", "medicine", "wagon", "release", "give"),
                    GameActions.OfferExchange, QuestRoutes.ExchangeKestPayment,
                    "It's ours now. Twenty gold buys it back.", "", skipWhenUnavailable: true)
            });

            AddTradeRules(QuestRoutes.BrannId, new[]
            {
                new ScriptedTradeRule(HandOver, Words("badge"), GameActions.ResolveDelivery, QuestRoutes.DeliveryBadgeBrann,
                    "My badge! Dented, but mine. Thank you.", "", skipWhenUnavailable: true),
                new ScriptedTradeRule(Words("badge", "lost", "task", "anything"), Words("badge", "lost", "task", "anything"),
                    GameActions.OfferQuest, QuestRoutes.OfferBadgeBrann,
                    "I lost my badge near that wreck on the Old Road. Find it and I'll owe you.", "", skipWhenUnavailable: true),
                new ScriptedTradeRule(Words("permit", "let me through", "pass", "steward", "elra", "deliver", "enter"),
                    Words("permit", "let me through", "pass", "steward", "elra", "deliver", "enter"),
                    GameActions.GrantAccess, QuestLog.AccessDeliveryPermit,
                    "You've got the medicine. Here's a delivery permit; the steward will see you.",
                    "No permit without cause. Come back with the medicine and I'll pass you to the steward.")
            });

            AddTradeRules(QuestRoutes.ElraId, new[]
            {
                new ScriptedTradeRule(HandOver, Division, GameActions.ResolveDelivery, QuestRoutes.DeliveryDivisionElra,
                    "As agreed. The garrison takes its share, the healer the rest.", "", skipWhenUnavailable: true),
                new ScriptedTradeRule(Division, Division, GameActions.OfferExchange, QuestRoutes.ExchangeDivisionElra,
                    "A division? If the healer agrees, the garrison will.", "", skipWhenUnavailable: true),
                new ScriptedTradeRule(HandOver, Words("parcel", "medicine"), GameActions.ResolveDelivery, QuestRoutes.DeliveryParcelElra,
                    "The garrison's medicine. The castle thanks you.",
                    "I can only take a parcel you carry, with Brann's delivery permit.")
            });
        }

        private static string[] Words(params string[] words) => words;
    }
}
