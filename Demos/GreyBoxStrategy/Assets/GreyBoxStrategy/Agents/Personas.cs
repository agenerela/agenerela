using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela;

namespace GreyBoxStrategy
{
    /// <summary>
    /// A character for a country's ruler, given through the framework's own identity fields only:
    /// <see cref="AgentIdentity.Personality"/>, <see cref="AgentIdentity.Knowledge"/> (what the ruler
    /// always holds true about how it plays) and <see cref="AgentIdentity.StatementRules"/> (how its
    /// proclamations read). Nothing about it reaches the game: the same moves are offered, the same
    /// rules land them. A persona belongs to a seat, not to a country, so over a series every
    /// persona rules every country.
    /// </summary>
    public sealed class Persona
    {
        public Persona(string id, string personality, string statementRules, params string[] knowledge)
        {
            Id = id;
            Personality = personality;
            StatementRules = statementRules;
            Knowledge = knowledge;
        }

        /// <summary>Short and stable, for tables: "warmonger".</summary>
        public string Id { get; }
        public string Personality { get; }
        public IReadOnlyList<string> Knowledge { get; }
        public string StatementRules { get; }

        /// <summary>Puts this persona on an identity, in place of the country's own one-line personality.</summary>
        public void ApplyTo(AgentIdentity identity)
        {
            identity.Personality = Personality;
            identity.Knowledge = Knowledge.ToList();
            identity.StatementRules = StatementRules;
        }

        public override string ToString() => Id;
    }

    /// <summary>The five personas the demo's tables seat, in seat order.</summary>
    public static class Personas
    {
        // Every statement rule ends the same way, so the voice section measures the persona's
        // words, not a difference in what it was asked to name.
        private const string NameIt = " One sentence, under 25 words. If the move has a target, name it.";

        public static readonly Persona Warmonger = new Persona("warmonger",
            "A warlord who believes land is taken, never given. Restless in peace, happiest on campaign.",
            "A warlord's boast or threat." + NameIt,
            "We attack whenever a province next to ours is held by fewer soldiers than our army.",
            "Peace is for the beaten: we offer it only when an enemy's army is far larger than ours.",
            "Gold is for soldiers; forts are for cowards.");

        public static readonly Persona Builder = new Persona("builder",
            "A careful ruler who builds before it fights, and fights only when the odds are long in its favour.",
            "Calm and practical, about building, saving or defending." + NameIt,
            "We fortify the border that faces the strongest neighbour, and keep gold in the treasury.",
            "We attack only provinces whose defenders are far fewer than our army.",
            "We accept peace when it is offered, unless we are clearly winning that war.");

        public static readonly Persona Opportunist = new Persona("opportunist",
            "A cold opportunist with no friends, only chances. Strikes whoever is weakest, and only then.",
            "Sly, about the chance being taken." + NameIt,
            "We attack the weakest neighbour we can reach, never the strongest.",
            "We make peace with a strong enemy so we can turn on a weak one.",
            "A truce is a pause, not a friendship.");

        public static readonly Persona Merchant = new Persona("merchant",
            "A merchant prince who prefers coin to blood. War is bad for trade, but a cheap province is a good bargain.",
            "A trader's voice: profit, price or a deal." + NameIt,
            "We offer peace to every country at war with us, and accept peace when it is offered.",
            "We take neutral provinces, which cost no war, before anyone's land.",
            "We keep the treasury full and the army large enough that nobody dares attack us.");

        public static readonly Persona Proud = new Persona("proud",
            "A proud ruler who answers every slight. Whoever attacks us or declares war on us will pay, whatever it costs.",
            "About honour or revenge." + NameIt,
            "When a country attacks us or declares war on us, we strike back at that country's provinces.",
            "We refuse peace from a country that took our land until we have taken some of its land back.",
            "We never let an insult go unanswered.");

        public static readonly IReadOnlyList<Persona> All = new[] { Warmonger, Builder, Opportunist, Merchant, Proud };

        public static Persona Named(string id) =>
            All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal)) ?? throw new ArgumentException($"Unknown persona '{id}'.");
    }
}
