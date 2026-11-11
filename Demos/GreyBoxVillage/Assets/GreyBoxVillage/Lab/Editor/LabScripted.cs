using System.Linq;
using Agenerela.Providers;

namespace Demos.GreyBoxVillage.Lab
{
    /// <summary>
    /// Two scripted models for the bench, with no Ollama: <see cref="Oracle"/> answers every turn
    /// with its first acceptable answer the request offers, and <see cref="Contrary"/> with an
    /// offered answer that is not acceptable. The tests run the whole bench on them, and
    /// <c>-labServer scripted</c> runs it on the oracle, a dry run of the machinery.
    /// </summary>
    /// <remarks>
    /// The oracle checks each scenario against the game: an acceptable answer the setup does not
    /// offer is one no model could give, and the oracle then answers none, which a scenario that
    /// does not accept none scores as wrong.
    /// </remarks>
    public static class LabScripted
    {
        public static ScriptedReply Oracle(ScriptedRequest request)
        {
            var turn = LabBench.CurrentTurn;
            if (turn != null)
                foreach (var text in turn.accept)
                    if (LabPair.TryParse(text, out var pair) && Pick(pair, request, out string target))
                        return ScriptedReply.Decide(pair.Action, target, "As you say.");
            return ScriptedReply.Decide("none", "no_target", "Hm.");
        }

        public static ScriptedReply Contrary(ScriptedRequest request)
        {
            var turn = LabBench.CurrentTurn;
            if (turn == null) return ScriptedReply.Decide("none", "no_target", "Hm.");
            bool Good(string action, string target) =>
                turn.accept.Concat(turn.weaker).Any(t => LabPair.TryParse(t, out var p) && p.Matches(action, target));
            if (!Good("none", "no_target")) return ScriptedReply.Decide("none", "no_target", "No.");
            foreach (var action in request.OfferedActions.Where(a => a != "none"))
                foreach (var target in request.OfferedTargets.DefaultIfEmpty("no_target"))
                    if (!Good(action, target)) return ScriptedReply.Decide(action, target, "No.");
            return ScriptedReply.Decide("none", "no_target", "No.");
        }

        // The first of the pair's targets the request offers; no_target for a pair that names it.
        private static bool Pick(LabPair pair, ScriptedRequest request, out string target)
        {
            target = null;
            if (!request.OfferedActions.Contains(pair.Action)) return false;
            if (pair.AnyTarget)
            {
                target = request.OfferedTargets.FirstOrDefault(t => t != "no_target") ?? "no_target";
                return true;
            }
            target = pair.Targets.FirstOrDefault(t => t == "no_target" || request.OfferedTargets.Contains(t));
            return target != null;
        }
    }
}
