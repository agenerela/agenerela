using System;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// The storyteller's part of a table arm, read without the Agents assembly: whether the game has a
    /// storyteller, and its <see cref="StoryRules"/>. It reads the same modifiers as
    /// <c>ModelSeatOptions.Parse</c> (a test holds the two together), so a replay plays a recorded
    /// storyteller's events under the rules the game had.
    /// </summary>
    public static class StoryArms
    {
        /// <summary>
        /// The arm the screen's New game uses for a preset: the storyteller with every stage S2 piece,
        /// the title call (the tech lead's decision from S2's measurements) and planned tales, which turn
        /// on the ruler's choices and so read as a story.
        /// </summary>
        public static string For(string preset) =>
            string.IsNullOrEmpty(preset) || preset == "off" ? "default" : $"default+storyteller+s2+chains-skeleton+preset-{preset}";

        /// <summary>True when the arm has a storyteller.</summary>
        public static bool HasStoryteller(string arm) => Rules(arm) != null;

        /// <summary>The game's story rules for an arm, or null when it has no storyteller.</summary>
        public static StoryRules Rules(string arm)
        {
            if (string.IsNullOrEmpty(arm)) return null;
            var parts = arm.Split('+');
            bool storyteller = false;
            var r = new StoryRules();
            string preset = null;
            for (int i = 1; i < parts.Length; i++)
            {
                string m = parts[i];
                switch (m)
                {
                    case "storyteller": storyteller = true; break;
                    case "choices": r.Choices = true; break;
                    case "multi": r.Multi = true; break;
                    case "history": r.History = true; break;
                    case "traits": r.Traits = true; break;
                    case "title-call": r.TitleCall = true; break;
                    case "chains-free": r.Chains = ChainMode.Free; break;
                    case "chains-skeleton": r.Chains = ChainMode.Skeleton; break;
                    case "s2":
                        r.Choices = r.Multi = r.History = r.Traits = r.TitleCall = true;
                        preset = preset ?? StorytellerSettings.EvenHand.Name;
                        break;
                    default:
                        if (m.StartsWith("preset-", StringComparison.Ordinal) && StorytellerSettings.Named(m.Substring(7)) != null) preset = m.Substring(7);
                        break;
                }
            }
            if (!storyteller) return null;
            r.Settings = preset == null ? new StorytellerSettings() : StorytellerSettings.Named(preset);
            return r;
        }
    }
}
