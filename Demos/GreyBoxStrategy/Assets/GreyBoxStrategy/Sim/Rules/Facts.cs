using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>One thing that happened to a country, and how much it matters next decision.</summary>
    public readonly struct TurnFact
    {
        public TurnFact(int priority, string line)
        {
            Priority = priority;
            Line = line;
        }

        /// <summary>0 matters most: land lost, war declared on us. 3 matters least.</summary>
        public int Priority { get; }
        public string Line { get; }
    }

    /// <summary>
    /// The two kinds of fact a country decides on, kept apart from the start (#239's
    /// <c>ObservationPlacement.WithStimulus</c> pays off only when they are).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Standing facts</b> hold on every call: the turn and the score, the treasury, the army,
    /// and each rival's relation and strength. Stage 2 returns them from an <c>IObservationSource</c>.
    /// </para>
    /// <para>
    /// <b>Turn facts</b> are the one to three things that just happened to this country: land
    /// taken, a war declared on it, an attack that failed, a harvest. Stage 2 sends them with the
    /// call, after the stimulus.
    /// </para>
    /// Every line names a country or province by its id, the id a move's target uses.
    /// </remarks>
    public static class Facts
    {
        /// <summary>Most turn facts that go with one call.</summary>
        public const int MaxTurnFacts = 3;

        /// <summary>The standing facts for one country: at most eight lines.</summary>
        public static List<string> Standing(World world, string countryId)
        {
            var me = world.Country(countryId);
            var rules = world.Rules;
            int held = world.ProvinceCount(countryId);
            var lines = new List<string>
            {
                $"Turn {world.Turn} of {rules.MaxTurns}. We hold {held} of {world.ProvinceList.Count} provinces; {rules.ProvincesToWin} wins.",
                $"Treasury: {me.Gold} gold; income {world.Income(me)} and upkeep {world.Upkeep(me)} a turn.",
                me.Stability <= 1
                    ? $"Army: {me.Army} soldiers. Stability: {me.Stability} of {rules.MaxStability}, so a province may revolt."
                    : $"Army: {me.Army} soldiers. Stability: {me.Stability} of {rules.MaxStability}.",
            };

            foreach (var other in world.Alive.Where(c => c.Id != countryId))
            {
                var rel = world.RelationBetween(countryId, other.Id);
                string relation = rel == Relation.War ? "at war with us"
                    : rel == Relation.Truce ? (world.TruceTurnsLeft(countryId, other.Id) == 1 ? "truce with us for 1 more turn" : $"truce with us for {world.TruceTurnsLeft(countryId, other.Id)} more turns")
                    : "at peace with us";
                string border = world.Borders(countryId, other.Id) ? "; borders us" : "";
                lines.Add($"{other.Id}: {relation}; army {other.Army}, {Provinces(world.ProvinceCount(other.Id))}{border}.");
            }

            int neutralNear = world.ProvinceList.Count(p => p.IsNeutral && world.Touches(countryId, p));
            if (neutralNear > 0 && lines.Count < 8)
                lines.Add(neutralNear == 1 ? "1 neutral province borders us." : $"{neutralNear} neutral provinces border us.");

            return lines;
        }

        /// <summary>
        /// One line naming every target each targeted move can take right now, for a model that
        /// cannot read the schema: "Moves open to us: attack fenmark or westmarch; fortify ashby;
        /// declare war on orsk; offer peace to kesh." Null when no targeted move is legal.
        /// </summary>
        /// <remarks>
        /// The game-side workaround for stage 1's gap N1: an Ollama model never reads the schema,
        /// and the framework's target list is every registered target, whatever per-target
        /// availability says, so a legal pair reaches it nowhere else.
        /// </remarks>
        public static string MovesLine(World world, string countryId)
        {
            var legal = Legality.CouncilMoves(world, countryId);
            var parts = new List<string>();
            void Add(string action, string verb)
            {
                var targets = legal.Where(m => m.Action == action).Select(m => m.Target).ToList();
                if (targets.Count == 0) return;
                string list = targets.Count == 1 ? targets[0]
                    : string.Join(", ", targets.Take(targets.Count - 1)) + " or " + targets[targets.Count - 1];
                parts.Add(verb + " " + list);
            }
            Add(Actions.Attack, "attack");
            Add(Actions.Fortify, "fortify");
            Add(Actions.DeclareWar, "declare war on");
            Add(Actions.OfferPeace, "offer peace to");
            return parts.Count == 0 ? null : "Moves open to us: " + string.Join("; ", parts) + ".";
        }

        /// <summary>"1 province", "6 provinces".</summary>
        public static string Provinces(int n) => n == 1 ? "1 province" : $"{n} provinces";

        /// <summary>The turn facts that go with one call: the most important first, at most three.</summary>
        public static List<string> Select(IEnumerable<TurnFact> facts, int max = MaxTurnFacts) =>
            facts.Select((f, i) => (f, i))
                .OrderBy(x => x.f.Priority).ThenBy(x => x.i)
                .Take(max)
                .Select(x => x.f.Line)
                .ToList();
    }
}
