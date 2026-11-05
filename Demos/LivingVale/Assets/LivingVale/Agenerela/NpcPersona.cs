using System;
using System.Collections.Generic;
using Agenerela;
using LivingVale.Interaction;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// Who one speaking NPC is to the model: the framework identity, plus the few facts the NPC
    /// knows, sent each turn as observations (design §7, §11). Facts are the NPC's own, never the
    /// whole quest database or another NPC's motives.
    /// </summary>
    public sealed class NpcPersona
    {
        public string NpcId { get; }
        public string Name { get; }
        public string Role { get; }
        public string Personality { get; }
        public string Goals { get; }

        /// <summary>What the NPC knows, one short line each. Sent before the turn's own observations.</summary>
        public IReadOnlyList<string> Knowledge { get; }

        public NpcPersona(string npcId, string name, string role, string personality, string goals, IReadOnlyList<string> knowledge)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                throw new ArgumentException("A persona needs the NPC's stable ID.", nameof(npcId));
            }

            NpcId = npcId;
            Name = name ?? "";
            Role = role ?? "";
            Personality = personality ?? "";
            Goals = goals ?? "";
            Knowledge = knowledge ?? Array.Empty<string>();
        }

        /// <summary>A fresh framework identity. Each agent gets its own, so editing one never changes another.</summary>
        public AgentIdentity ToIdentity()
        {
            return new AgentIdentity { Name = Name, Role = Role, Personality = Personality, Goals = Goals };
        }

        /// <summary>For an NPC with no authored persona: its descriptor's name and role, and nothing it claims to know.</summary>
        public static NpcPersona FromDescriptor(NpcDescriptor npc)
        {
            if (npc == null)
            {
                throw new ArgumentNullException(nameof(npc));
            }

            return new NpcPersona(npc.Id, npc.DisplayName, npc.Role,
                ValeNpcPersonas.SpeakingRules,
                "Help travelers where you can.",
                Array.Empty<string>());
        }
    }

    /// <summary>
    /// The authored personas of the first slice's speaking NPCs, from design §7 and the frozen
    /// content list (docs/demos/living-vale-content.md). Mara and Iven so far; the other six join
    /// as their scenes are built.
    /// </summary>
    public static class ValeNpcPersonas
    {
        /// <summary>
        /// How every NPC talks. A local model never reads the response schema (AGENTS.md), so what
        /// it must know about the free-text field has to be in the prompt, and here is where.
        /// </summary>
        public const string SpeakingRules =
            "Answer the traveler in your own voice, in one to three short sentences. " +
            "Stay in character: you live in Briar Vale and know nothing of games, models or the world outside it. " +
            "If you do not know something, say so plainly or name someone who might. " +
            "Never claim to have given, sold or done something you only talked about.";

        public static readonly NpcPersona Mara = new NpcPersona(
            "npc_mara", "Mara", "the general trader at the market stall in Briar Glen",
            "Practical, plain-spoken and protective of your stock; you like a fair price and a bit of gossip. " + SpeakingRules,
            "Keep your stall stocked and paying, and see the roads busy with trade again.",
            new[]
            {
                "You are at your market stall in Briar Glen, talking with a traveler.",
                "You sell healing potions at 10 gold each, and you only have a few left.",
                "The medicine wagon from Ashwatch Castle never reached the vale, and trade has been slow since the Old Road went quiet.",
                "Iven, the healer on the porch nearby, was waiting on that wagon more than anyone.",
                "Sella at the inn hears travelers' news; you hear the rest.",
                "Your stall is not taking sales through talk yet; if asked to sell, say the traveler must wait until it opens properly."
            });

        public static readonly NpcPersona Iven = new NpcPersona(
            "npc_iven", "Iven", "the healer of Briar Glen",
            "Quiet, tired and kind; your patients come first and small talk can wait. " + SpeakingRules,
            "Get medicine to your patients, and learn what happened to the wagon that carried it.",
            new[]
            {
                "You are on your porch in Briar Glen, talking with a traveler.",
                "Three patients are waiting on your porch, and you have no medicine left to give them.",
                "The medicine wagon left Ashwatch Castle two days ago and should have arrived by now; it would have come along the Old Road.",
                "Herbs grow in Herb Hollow; three bundles would help a little, but they are no substitute for the real supplies.",
                "Mara at the market stall hears every rumor in the village."
            });

        public static IReadOnlyList<NpcPersona> All { get; } = new[] { Mara, Iven };
    }
}
