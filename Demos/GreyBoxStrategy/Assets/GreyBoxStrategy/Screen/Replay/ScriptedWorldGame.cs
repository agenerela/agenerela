using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// A whole game on any map, played at once by the rules' own scripted rulers and written as a
    /// recording the screen replays: how the drawn-chart spike shows a generated world without a
    /// model or a server (rules v2, R2).
    /// </summary>
    /// <remarks>
    /// The model seats' runner (<c>TableRunner</c>, in Agents) plays the Classic board only, and
    /// Agents belongs to the rules sessions; the Sim's players (<see cref="Lineups"/>) already play
    /// any map. Every move is recorded as a decision with no statement, so the replay rebuilds the
    /// same game turn by turn, as it does for a model's game.
    /// </remarks>
    public static class ScriptedWorldGame
    {
        /// <summary>The line-up: expansionists and cautious rulers in turn, one random.</summary>
        public const string Lineup = "mixed";

        /// <param name="edits">Dev-menu edits, each applied at the start of its turn as a recording's are; the game is then a dev game.</param>
        public static Recording Play(GameSetup setup, MapDefinition map, GameRules rules, IReadOnlyList<DevEdit> edits = null)
        {
            var game = new Game(setup.Seed, map, rules);
            var seats = Lineups.Seat(Lineup, 0, map);
            var rec = new Recording
            {
                Seed = setup.Seed,
                Title = (setup.Map == GameSetup.ClassicMapId ? "The Classic board" : $"A generated world, seed {setup.MapSeed ?? setup.Seed}: {setup.Generator?.Size ?? MapSize.Medium}, {setup.Generator?.Shape ?? LandShape.Continent}")
                        + (setup.Turn == TurnStyle.Writs ? $", the writs turn (seed {setup.Seed})" : ""),
                Source = "scripted",
                Dev = true,
            };
            foreach (var kv in seats) rec.Players[kv.Key] = new RecordedPlayer { Model = kv.Value.Kind };
            long sequence = 0;
            Move Decide(CountryRequest r)
            {
                var move = seats[r.CountryId].Decide(r);
                if (game.Rules.EconomyOn) move = EconomyHand(r, move);
                rec.Decisions.Add(new RecordedDecision
                {
                    AgentId = r.CountryId,
                    Turn = r.Turn,
                    Moment = r.Moment == Moment.Envoy ? "envoys" : r.Moment == Moment.Army ? RecordedDecision.ArmyMoment : "council",
                    ArmyId = r.ArmyId,
                    Sequence = sequence++,
                    Action = move.Action,
                    Target = move.Target ?? RecordedDecision.NoTarget,
                    ModelAction = move.Action,
                    ModelTarget = move.Target ?? RecordedDecision.NoTarget,
                    Statement = "",
                    ModelName = seats[r.CountryId].Kind,
                    OfferedActions = r.Legal.Select(m => m.Action).Distinct().ToList(),
                });
                return move;
            }
            int guard = game.Rules.MaxTurns + 1;
            while (!game.IsOver)
            {
                if (guard-- <= 0) throw new InvalidOperationException("The game did not end within its turn limit.");
                int next = game.World.Turn + 1;
                bool edited = edits != null && edits.Any(e => e.Turn == next);
                if (edited) DevEdits.ApplyFor(game, edits, next);
                if (game.Rules.Turn == TurnStyle.Writs)
                {
                    // The writs turn: rounds of orders (court, armies, a second court), then treaty answers.
                    var requests = game.BeginTurn();
                    while (requests.Count > 0) requests = game.Submit(requests.Select(r => new CountryAnswer(r, Decide(r))).ToList());
                    game.Resolve();
                    if (edited) DevEdits.AfterBeginTurn(game);
                    continue;
                }
                var council = game.BeginTurn();
                var envoys = game.SubmitCouncil(council.Select(r => new CountryAnswer(r, Decide(r))).ToList());
                game.Resolve(envoys.Select(r => new CountryAnswer(r, Decide(r))).ToList());
                if (edited) DevEdits.AfterBeginTurn(game);
            }
            if (edits != null) rec.DevEdits.AddRange(edits);
            return rec;
        }

        /// <summary>Gold a scripted court keeps after buying a building.</summary>
        public const int BuildReserve = 20;

        /// <summary>
        /// The economy (#372, #374): the Sim's scripted rulers know no buildings or tax levels, so a watched scripted game
        /// would show none. Where a court would stop giving orders, this hand sets a tax level by its stability (high when
        /// steady, back to normal or low when shaky), or buys the building that adds most where the rules offer it while
        /// <see cref="BuildReserve"/> gold stays. It only picks among the orders offered; its other choices are the ruler's.
        /// </summary>
        public static Move EconomyHand(CountryRequest r, Move chosen)
        {
            if (r.Moment != Moment.Council || chosen == null || chosen.Action != Actions.None) return chosen;
            var me = r.World.Country(r.CountryId);
            if (me == null) return chosen;
            Move Offered(string action) => r.Legal.FirstOrDefault(m => m.Action == action);
            if (me.Stability >= 4 && me.Taxes == TaxLevel.Normal && Offered(Actions.TaxesHigh) is Move high) return high;
            if (me.Stability <= 2 && me.Taxes == TaxLevel.High && Offered(Actions.TaxesNormal) is Move normal) return normal;
            if (me.Stability <= 1 && me.Taxes == TaxLevel.Normal && Offered(Actions.TaxesLow) is Move low) return low;
            var best = r.Legal.Where(m => Actions.BuildsWhat(m.Action) != null && r.World.Province(m.Target) != null)
                .Where(m => me.Spendable - WritLegality.BuildingCost(r.World.Rules, Actions.BuildsWhat(m.Action).Value) >= BuildReserve)
                .OrderByDescending(m => WritLegality.BuildingGain(r.World, r.World.Province(m.Target), Actions.BuildsWhat(m.Action).Value))
                .ThenBy(m => m.Action, StringComparer.Ordinal).ThenBy(m => m.Target, StringComparer.Ordinal).FirstOrDefault();
            return best ?? chosen;
        }
    }
}
