namespace LivingVale.Interaction
{
    /// <summary>Who said a line in a conversation's history.</summary>
    public enum ChatSpeaker
    {
        Player,
        Npc,

        /// <summary>A game-authored line, such as "No reply in time". Never sent to a driver as something the NPC said.</summary>
        System
    }

    /// <summary>One line of a conversation, as the chat overlay shows it.</summary>
    public readonly struct ChatLine
    {
        public ChatSpeaker Speaker { get; }
        public string Text { get; }

        public ChatLine(ChatSpeaker speaker, string text)
        {
            Speaker = speaker;
            Text = text ?? "";
        }

        public override string ToString() => $"{Speaker}: {Text}";
    }
}
