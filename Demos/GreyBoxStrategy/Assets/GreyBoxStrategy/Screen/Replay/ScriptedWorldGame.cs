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

        public static Recording Play(GameSetup setup, MapDefinition map, GameRules rules)
        {
            var game = new Game(setup.Seed, map, rules);
            var seats = Lineups.Seat(Lineup, 0, map);
            var rec = new Recording
            {
                Seed = setup.Seed,
                Title = $"A generated world, seed {setup.MapSeed ?? setup.Seed}: {setup.Generator?.Size ?? MapSize.Medium}, {setup.Generator?.Shape ?? LandShape.Continent}",
                Source = "scripted",
                Dev = true,
            };
            foreach (var kv in seats) rec.Players[kv.Key] = new RecordedPlayer { Model = kv.Value.Kind };
            long sequence = 0;
            Move Decide(CountryRequest r)
            {
                var move = seats[r.CountryId].Decide(r);
                rec.Decisions.Add(new RecordedDecision
                {
                    AgentId = r.CountryId,
                    Turn = r.Turn,
                    Moment = r.Moment == Moment.Envoy ? "envoys" : "council",
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
                var council = game.BeginTurn();
                var envoys = game.SubmitCouncil(council.Select(r => new CountryAnswer(r, Decide(r))).ToList());
                game.Resolve(envoys.Select(r => new CountryAnswer(r, Decide(r))).ToList());
            }
            return rec;
        }
    }
}
