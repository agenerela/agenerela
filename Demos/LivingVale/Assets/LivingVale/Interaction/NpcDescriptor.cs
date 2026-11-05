using System;

namespace LivingVale.Interaction
{
    /// <summary>
    /// A speaking NPC as the conversation layer knows it: a stable ID (saves and drivers key on
    /// it, never on a GameObject), the name the chat header shows, and a one-line role.
    /// </summary>
    public sealed class NpcDescriptor
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Role { get; }

        public NpcDescriptor(string id, string displayName, string role)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("An NPC needs a stable ID.", nameof(id));
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Role = role ?? "";
        }
    }
}
