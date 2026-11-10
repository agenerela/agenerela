using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Tests
{
    /// <summary>Helpers that set up a game in a known state and play one turn of chosen moves.</summary>
    internal static class TestGames
    {
        /// <summary>The standard rules with no events and no revolts, so upkeep is arithmetic.</summary>
        public static GameRules Calm()
        {
            var r = GameRules.Standard;
            r.GoodHarvestChance = 0;
            r.PoorHarvestChance = 0;
            r.BanditsChance = 0;
            r.UnrestChance = 0;
            r.RevoltChance = 0;
            return r;
        }

        public static Game NewGame(int seed = 1, GameRules rules = null) => new Game(seed, MapDefinition.Standard(), rules ?? Calm());

        /// <summary>Hands a province to a country (or to nobody), as if it had been taken.</summary>
        public static void Give(World w, string provinceId, string owner)
        {
            var p = w.Province(provinceId);
            p.Owner = owner;
            if (owner != null) p.Garrison = 0;
        }

        /// <summary>
        /// Plays one turn: each country named makes its move, every other one holds, and each
        /// offer of peace is answered by <paramref name="answerPeace"/> (refused when null).
        /// </summary>
        public static TurnReport PlayTurn(Game game, Func<CountryRequest, bool> answerPeace, params (string country, string action, string target)[] moves)
        {
            var council = game.BeginTurn();
            return Finish(game, council, answerPeace, moves);
        }

        public static TurnReport PlayTurn(Game game, params (string country, string action, string target)[] moves) =>
            PlayTurn(game, null, moves);

        /// <summary>Submits moves for an already begun turn and resolves it.</summary>
        public static TurnReport Finish(Game game, IReadOnlyList<CountryRequest> council, Func<CountryRequest, bool> answerPeace,
            params (string country, string action, string target)[] moves)
        {
            var answers = council.Select(r =>
            {
                var m = moves.FirstOrDefault(x => x.country == r.CountryId);
                return new CountryAnswer(r, m.country == null ? Move.Hold : new Move(m.action, m.target));
            }).ToList();
            var envoys = game.SubmitCouncil(answers);
            var replies = envoys.Select(r => new CountryAnswer(r,
                new Move(answerPeace != null && answerPeace(r) ? Actions.AcceptPeace : Actions.RefusePeace))).ToList();
            return game.Resolve(replies);
        }

        public static CountryRequest For(IReadOnlyList<CountryRequest> requests, string country) =>
            requests.First(r => r.CountryId == country);

        public static bool Offers(CountryRequest request, string action, string target = null) =>
            request.Legal.Any(m => m.Action == action && m.Target == target);

        public static MoveOutcome Outcome(TurnReport report, string country, Moment moment = Moment.Council) =>
            report.Outcomes.First(o => o.Request.CountryId == country && o.Request.Moment == moment);

        /// <summary>Plays a whole game with these kinds per seat, in seat order.</summary>
        public static GameRecord PlayWhole(int seed, params string[] kinds)
        {
            var map = MapDefinition.Standard();
            var seats = new Dictionary<string, IPlayer>();
            for (int i = 0; i < map.Countries.Count; i++) seats[map.Countries[i].Id] = Lineups.Make(kinds[i % kinds.Length]);
            return GameRunner.Play(seed, seats, map);
        }
    }
}
