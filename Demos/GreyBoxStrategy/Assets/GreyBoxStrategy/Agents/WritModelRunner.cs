using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace GreyBoxStrategy
{
    /// <summary>
    /// Plays the writs turn (rules v2, R1b) with model seats (<see cref="WritRuler"/>) beside scripted and
    /// random players. Each round of requests is answered on one snapshot: the players at once, the model
    /// rulers one after another, so one ruler's decisions run back to back (v2 section 1.3), where the
    /// server can reuse the prompt's shared beginning. Every move lands through its seat's gate.
    /// </summary>
    public static class WritModelRunner
    {
        /// <summary>One game from a setup; <paramref name="kinds"/> per country in seat order, <see cref="ModelGameRunner.Model"/> for a model seat.</summary>
        public static async Awaitable<WritModelRecord> PlayAsync(GameSetup setup, IReadOnlyList<string> kinds,
            Func<World, string, WritRuler> makeSeat, string modelKind, CancellationToken ct = default, Storyteller storyteller = null)
        {
            var map = setup.BuildMap();
            var game = new Game(setup.Seed, map, setup.BuildRules(map));
            var players = new Dictionary<string, IPlayer>(StringComparer.Ordinal);
            var seats = new Dictionary<string, WritRuler>(StringComparer.Ordinal);
            var kindByCountry = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < map.Countries.Count; i++)
            {
                string id = map.Countries[i].Id;
                string kind = kinds[i % kinds.Count];
                if (kind == ModelGameRunner.Model)
                {
                    var seat = makeSeat(game.World, id);
                    seat.Seed = setup.Seed;
                    seats[id] = seat;
                    game.Gates[id] = seat;
                    kindByCountry[id] = modelKind;
                }
                else
                {
                    players[id] = Lineups.Make(kind);
                    kindByCountry[id] = kind;
                }
            }
            int guard = game.Rules.MaxTurns + 1;
            while (!game.IsOver)
            {
                ct.ThrowIfCancellationRequested();
                if (guard-- <= 0) throw new InvalidOperationException("The game did not end within its turn limit.");
                await PlayTurnAsync(game, players, seats, ct, storyteller);
            }
            return new WritModelRecord(new GameRecord(game, kindByCountry), seats.Values.SelectMany(s => s.Decisions).ToList(), seats.Keys.ToList());
        }

        /// <summary>One turn: the storyteller and its dilemmas, then rounds of orders, then treaty answers, then the turn lands.</summary>
        public static async Awaitable<TurnReport> PlayTurnAsync(Game game, IReadOnlyDictionary<string, IPlayer> players,
            IReadOnlyDictionary<string, WritRuler> seats, CancellationToken ct = default, Storyteller storyteller = null)
        {
            if (storyteller != null) await storyteller.TellAsync(ct);
            if (game.PendingChoices.Count > 0)
                game.SubmitChoices(await AnswerAll(game.ChoiceRequests(), players, seats, ct));
            var requests = game.BeginTurn();
            while (requests.Count > 0)
                requests = game.Submit(await AnswerAll(requests, players, seats, ct));
            var report = game.Resolve();
            foreach (var seat in seats.Values) seat.Learn(report);
            foreach (var seat in seats.Values) seat.EndTurn();
            return report;
        }

        private static async Awaitable<List<CountryAnswer>> AnswerAll(IReadOnlyList<CountryRequest> requests,
            IReadOnlyDictionary<string, IPlayer> players, IReadOnlyDictionary<string, WritRuler> seats, CancellationToken ct)
        {
            var answers = new List<CountryAnswer>();
            foreach (var r in requests)
            {
                if (players.TryGetValue(r.CountryId, out var p)) answers.Add(new CountryAnswer(r, p.Decide(r)));
                else if (seats.TryGetValue(r.CountryId, out var seat)) answers.Add(await seat.DecideAsync(r, ct));
            }
            return answers;
        }
    }

    /// <summary>A writs game with model seats in it, and what each model decision came to.</summary>
    public sealed class WritModelRecord
    {
        public WritModelRecord(GameRecord record, List<ModelDecision> decisions, IReadOnlyList<string> modelCountries)
        {
            Record = record;
            Decisions = decisions;
            ModelCountries = modelCountries;
        }

        public GameRecord Record { get; }
        public List<ModelDecision> Decisions { get; }
        public IReadOnlyList<string> ModelCountries { get; }
    }
}
