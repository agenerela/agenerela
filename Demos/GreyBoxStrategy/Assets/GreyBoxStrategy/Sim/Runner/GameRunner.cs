using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>A played game and who played it.</summary>
    public sealed class GameRecord
    {
        public GameRecord(Game game, IReadOnlyDictionary<string, string> kinds)
        {
            Game = game;
            Kinds = kinds;
        }

        public Game Game { get; }
        public int Seed => Game.Seed;
        public GameResult Result => Game.Result;
        public IReadOnlyList<TurnReport> Reports => Game.Reports;
        /// <summary>Player kind per country id.</summary>
        public IReadOnlyDictionary<string, string> Kinds { get; }
        public string WinnerKind => Result == null ? null : Kinds[Result.Winner];
    }

    /// <summary>The player kinds, and the line-ups a series plays.</summary>
    public static class Lineups
    {
        public const string Expansionist = "expansionist";
        public const string Cautious = "cautious";
        public const string Random = "random";

        /// <summary>
        /// The line-ups by name. Seat i plays kind <c>(i + game index) mod 5</c>, so over every five
        /// games each kind sits at every country once and no corner's luck favours a kind.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string[]> Named = new Dictionary<string, string[]>
        {
            ["mixed"] = new[] { Expansionist, Cautious, Expansionist, Cautious, Random },
            ["expansionist-vs-random"] = new[] { Expansionist, Random, Random, Random, Random },
            ["cautious-vs-random"] = new[] { Cautious, Random, Random, Random, Random },
            ["scripted"] = new[] { Expansionist, Cautious, Expansionist, Cautious, Expansionist },
            ["random"] = new[] { Random, Random, Random, Random, Random },
            // Stage 2's baselines: the seat a model plays, against the same four opponents.
            ["seat-expansionist"] = new[] { Expansionist, Expansionist, Cautious, Expansionist, Cautious },
            ["seat-cautious"] = new[] { Cautious, Expansionist, Cautious, Expansionist, Cautious },
            ["seat-random"] = new[] { Random, Expansionist, Cautious, Expansionist, Cautious },
        };

        public static IPlayer Make(string kind)
        {
            switch (kind)
            {
                case Expansionist: return new ScriptedPlayer(Temperament.Expansionist);
                case Cautious: return new ScriptedPlayer(Temperament.Cautious);
                case Random: return new RandomPlayer();
                default: throw new ArgumentException($"Unknown player kind '{kind}'.");
            }
        }

        /// <summary>Who plays each country in a series' <paramref name="gameIndex"/>th game.</summary>
        public static Dictionary<string, IPlayer> Seat(string lineup, int gameIndex, MapDefinition map)
        {
            if (!Named.TryGetValue(lineup, out var kinds)) throw new ArgumentException($"Unknown line-up '{lineup}'.");
            var seats = new Dictionary<string, IPlayer>(StringComparer.Ordinal);
            for (int i = 0; i < map.Countries.Count; i++)
                seats[map.Countries[i].Id] = Make(kinds[(i + gameIndex) % kinds.Length]);
            return seats;
        }
    }

    /// <summary>Plays whole games headless, as fast as the rules run: no scene, no model, no waiting.</summary>
    public static class GameRunner
    {
        /// <summary>Plays one game to the end with one player per country id.</summary>
        public static GameRecord Play(int seed, IReadOnlyDictionary<string, IPlayer> players, MapDefinition map = null, GameRules rules = null)
        {
            var game = new Game(seed, map, rules);
            foreach (var c in game.World.CountryList)
                if (!players.ContainsKey(c.Id)) throw new ArgumentException($"No player for {c.Id}.");

            int guard = game.Rules.MaxTurns + 1;
            while (!game.IsOver)
            {
                if (guard-- <= 0) throw new InvalidOperationException("The game did not end within its turn limit.");
                var council = game.BeginTurn();
                var envoys = game.SubmitCouncil(council.Select(r => new CountryAnswer(r, players[r.CountryId].Decide(r))).ToList());
                game.Resolve(envoys.Select(r => new CountryAnswer(r, players[r.CountryId].Decide(r))).ToList());
            }
            return new GameRecord(game, players.ToDictionary(kv => kv.Key, kv => kv.Value.Kind, StringComparer.Ordinal));
        }

        /// <summary>Plays one game from a setup (<see cref="GameSetup"/>) with a named line-up.</summary>
        public static GameRecord Play(GameSetup setup, string lineup, int gameIndex = 0)
        {
            var map = setup.BuildMap();
            return Play(setup.Seed, Lineups.Seat(lineup, gameIndex, map), map, setup.BuildRules(map));
        }

        /// <summary>Plays one game per seed with a named line-up, seats rotating game by game.</summary>
        /// <param name="map">The map; the Classic board when null.</param>
        public static List<GameRecord> PlaySeries(string lineup, IReadOnlyList<int> seeds, GameRules rules = null, MapDefinition map = null)
        {
            map = map ?? MapDefinition.Standard();
            var records = new List<GameRecord>(seeds.Count);
            for (int i = 0; i < seeds.Count; i++)
                records.Add(Play(seeds[i], Lineups.Seat(lineup, i, map), map, rules));
            return records;
        }

        /// <summary>Seeds <c>first, first + 1, …</c>: the list every series uses unless told otherwise.</summary>
        public static List<int> Seeds(int count, int first = 1) => Enumerable.Range(first, count).ToList();
    }
}
