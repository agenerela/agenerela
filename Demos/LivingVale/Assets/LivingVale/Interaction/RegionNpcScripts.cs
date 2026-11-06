using LivingVale.World;

namespace LivingVale.Interaction
{
    /// <summary>
    /// Scripted-driver lines for the six NPCs the region brings (G4b). Placeholders in each NPC's voice,
    /// keyword-matched like G1's, and all general conversation (<c>none</c>): their quest actions come
    /// with the quest routes. They say only what the NPC's persona knows.
    /// </summary>
    public static class RegionNpcScripts
    {
        public static void AddTo(ScriptedNpcDecisionDriver driver)
        {
            driver.SetScript(RegionNpcs.SellaId,
                new[]
                {
                    ("room", "Beds are all empty, if you ever need one."),
                    ("road", "Nobody's come up the Old Road in days. A carter saw torchlight on the bend, two nights back."),
                    ("wagon", "The medicine wagon? Iven's been watching the road for it. Mara might know more."),
                    ("rumor", "They say Broken Watch isn't as empty as it was. I didn't say who said it."),
                    ("hello", "Come in out of the road, love. What's the news where you're from?"),
                    ("bye", "Don't be a stranger.")
                },
                new[]
                {
                    "Couldn't tell you, but I'll ask around.",
                    "Inn's been quiet since the road went bad.",
                    "Reedbank's east past the Old Road, if you're heading on."
                });

            driver.SetScript(RegionNpcs.TomaId,
                new[]
                {
                    ("sword", "I make them. Can't sell you one yet, there's no iron coming up the road."),
                    ("armor", "Armor wants iron too. Bring me scrap and we'll talk."),
                    ("cart", "Every cart off that road lately came in with a broken axle. Somebody stopped them hard."),
                    ("wagon", "Didn't see it. Fen did, I think. Ask Fen in the fields."),
                    ("castle", "Over the bridge, north. Gate's shut to most."),
                    ("hello", "Mind the sparks."),
                    ("bye", "Hm.")
                },
                new[]
                {
                    "I hammer iron. I don't do riddles.",
                    "Ask someone who talks more.",
                    "Your gear's seen better days."
                });

            driver.SetScript(RegionNpcs.FenId,
                new[]
                {
                    ("wagon", "I... I saw it stopped on the bend. People in grey lifted a crate off it."),
                    ("crate", "They carried it south. Towards the old watchtower, Broken Watch."),
                    ("saw", "I don't want trouble. I only saw what I saw."),
                    ("road", "I don't go on that road now. Not after that night."),
                    ("hello", "Oh. Hello. Can I help you?"),
                    ("bye", "Be careful out there.")
                },
                new[]
                {
                    "I just work the fields.",
                    "I'd rather not say.",
                    "Toma might know, he's at the forge."
                });

            driver.SetScript(RegionNpcs.BrannId,
                new[]
                {
                    ("gate", "The gate stays closed. No entry without business the steward approves."),
                    ("badge", "...I lost my badge on the Old Road. Don't spread it around."),
                    ("wagon", "The castle sent medicine down the Old Road. We've heard nothing since."),
                    ("steward", "Elra deals with travelers. She's here in the forecourt."),
                    ("hello", "State your business."),
                    ("bye", "Move along.")
                },
                new[]
                {
                    "That's not for me to say.",
                    "Orders are orders.",
                    "Talk to the steward."
                });

            driver.SetScript(RegionNpcs.ElraId,
                new[]
                {
                    ("medicine", "Part of that wagon was for our garrison's sick, part for the healer in the Glen."),
                    ("wagon", "It never arrived. The garrison is short, and I have no answers yet."),
                    ("share", "Shared fairly between the garrison and the village? If both sides agreed, perhaps."),
                    ("gate", "The courtyard is closed to travelers for now. Brann has his orders."),
                    ("hello", "Good day. I have a moment, if it's brief."),
                    ("bye", "Go well.")
                },
                new[]
                {
                    "I'm afraid I can't help with that.",
                    "There are a dozen duties waiting on me.",
                    "If it concerns the castle, speak plainly."
                });

            driver.SetScript(RegionNpcs.KestId,
                new[]
                {
                    ("medicine", "My people are sick. The castle never once helped this camp."),
                    ("crate", "We took it. Nobody was hurt. Don't expect me to say sorry."),
                    ("castle", "The castle can come and ask itself, if it dares."),
                    ("help", "Help my people and I might listen. Threats get you nothing."),
                    ("hello", "You're far from the road. Who sent you?"),
                    ("bye", "Watch your step on the way out.")
                },
                new[]
                {
                    "We're watching you, stranger.",
                    "Say what you came to say.",
                    "We don't want a fight."
                });
        }
    }
}
