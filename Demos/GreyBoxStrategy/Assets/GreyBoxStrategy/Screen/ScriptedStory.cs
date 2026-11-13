using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Providers;
using GreyBoxStrategy.Replay;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// A stand-in storyteller and scribe for games with scripted rulers: no model, no server. It
    /// answers the storyteller's call with one of the pairs the paired schema offers, so every
    /// answer is legal, leaning toward dilemmas and tales so a short game shows every card; the
    /// scribe gets a title from a list. A ruler asked to choose takes one of its options.
    /// </summary>
    public static class ScriptedStory
    {
        private static readonly string[] Stories =
        {
            "A grey wind came down from the hills, and for a week the people spoke of nothing else.",
            "A bell rang at dusk where no bell hangs, and the old women said it was a sign.",
            "Travellers on the salt road brought a rumour that grew with every inn they passed.",
            "The river ran red with clay after the storm, and the market buzzed with strange talk.",
            "A hermit walked into the square and would not leave until the lords had heard him.",
            "The winter came early and hard, and the granaries were counted twice over.",
            "A caravan of strangers camped by the walls, offering what they had for what they needed.",
            "The saints' day passed without the usual rain, and the priests read it as an omen.",
        };

        private static readonly string[] Titles =
        {
            "The Grey Wind", "A Bell at Dusk", "The Salt Road Rumour", "The Red River", "The Hermit's Word",
            "An Early Winter", "Strangers at the Gate", "The Dry Saints' Day", "The Weeping Well", "A Crown of Frost",
        };

        /// <summary>The storyteller's answer: a legal pair, a dilemma or a tale about half the time when one is open.</summary>
        public static ScriptedReply Tell(ScriptedRequest r, Random rng)
        {
            var pairs = new List<(string action, string target)>();
            foreach (var p in r.Request?.Schema?.Pairs ?? (IReadOnlyList<Agenerela.ActionTargets>)Array.Empty<Agenerela.ActionTargets>())
                foreach (string a in p.Actions)
                    if (a != "none")
                        foreach (string t in p.Targets) pairs.Add((a, t));
            if (pairs.Count == 0) return ScriptedReply.Decide("none", statement: "Nothing stirs.");
            bool Dilemma((string action, string target) x) => EventRules.Template(x.action)?.IsChoice == true;
            bool Tale((string action, string target) x) => x.action.StartsWith("begin_", StringComparison.Ordinal) || x.target.StartsWith("tale_", StringComparison.Ordinal);
            var tales = pairs.Where(Tale).ToList();
            var dilemmas = pairs.Where(Dilemma).ToList();
            double roll = rng.NextDouble();
            var pick = tales.Count > 0 && roll < 0.35 ? tales[rng.Next(tales.Count)]
                     : dilemmas.Count > 0 && roll < 0.7 ? dilemmas[rng.Next(dilemmas.Count)]
                     : pairs[rng.Next(pairs.Count)];
            return ScriptedReply.Decide(pick.action, pick.target, Stories[rng.Next(Stories.Length)]);
        }

        /// <summary>The scribe's answer: a title.</summary>
        public static ScriptedReply Title(Random rng) => ScriptedReply.Decide("none", statement: Titles[rng.Next(Titles.Length)]);

        /// <summary>A ruler asked to choose: one of its options, at random.</summary>
        public static ScriptedReply Choose(ScriptedRequest r, Random rng)
        {
            var options = r.OfferedActions.Where(a => a != "none").ToList();
            return options.Count == 0 ? ScriptedReply.Decide("none", statement: "We wait.")
                : ScriptedReply.Decide(options[rng.Next(options.Count)], statement: "So be it; we have decided.");
        }

        /// <summary>Whether a ruler's request is a dilemma's choice call: its offered actions are a dilemma's options.</summary>
        public static bool IsChoiceCall(ScriptedRequest r) =>
            r.OfferedActions.Any(a => EventRules.AllTemplates.Any(t => t.Options != null && t.Options.Any(o => o.Id == a)));
    }
}
