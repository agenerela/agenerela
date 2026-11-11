using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;

namespace GreyBoxStrategy
{
    /// <summary>A game with model seats in it, and what each model decision came to.</summary>
    public sealed class ModelGameRecord
    {
        public ModelGameRecord(GameRecord record, List<ModelDecision> decisions, IReadOnlyList<string> modelCountries)
        {
            Record = record;
            Decisions = decisions;
            ModelCountries = modelCountries;
        }

        public GameRecord Record { get; }
        public List<ModelDecision> Decisions { get; }
        public IReadOnlyList<string> ModelCountries { get; }
    }

    /// <summary>
    /// Plays games where some seats are framework agents: it drives <see cref="Game"/>'s three
    /// calls itself, starts every agent's decision on the same snapshot, and awaits them before
    /// the turn moves on. The other seats are ordinary <see cref="IPlayer"/>s, so any line-up mixes.
    /// </summary>
    public static class ModelGameRunner
    {
        /// <summary>The kind that marks a model seat in a line-up.</summary>
        public const string Model = "model";

        /// <summary>
        /// The headline line-up: one seat for the player being measured, four scripted opponents
        /// (two of each temperament). Seats rotate game by game, as in <see cref="Lineups"/>.
        /// </summary>
        public static string[] SeatAgainstScripted(string seat) =>
            new[] { seat, Lineups.Expansionist, Lineups.Cautious, Lineups.Expansionist, Lineups.Cautious };

        /// <summary>Plays one game. <paramref name="kinds"/> are per country in seat order; <see cref="Model"/> seats use <paramref name="makeSeat"/>.</summary>
        public static async Awaitable<ModelGameRecord> PlayAsync(int seed, IReadOnlyList<string> kinds,
            Func<World, string, CountryAgent> makeSeat, string modelKind, CancellationToken ct = default)
        {
            var map = MapDefinition.Standard();
            var game = new Game(seed, map);
            var players = new Dictionary<string, IPlayer>(StringComparer.Ordinal);
            var seats = new Dictionary<string, CountryAgent>(StringComparer.Ordinal);
            var kindByCountry = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < map.Countries.Count; i++)
            {
                string id = map.Countries[i].Id;
                if (kinds[i] == Model)
                {
                    var seat = makeSeat(game.World, id);
                    seat.Seed = seed;
                    seats[id] = seat;
                    game.Gates[id] = seat;
                    kindByCountry[id] = modelKind;
                }
                else
                {
                    players[id] = Lineups.Make(kinds[i]);
                    kindByCountry[id] = kinds[i];
                }
            }

            await PlayToTheEndAsync(game, players, seats, ct);
            var record = new GameRecord(game, kindByCountry);
            return new ModelGameRecord(record, seats.Values.SelectMany(s => s.Decisions).ToList(), seats.Keys.ToList());
        }

        /// <summary>
        /// Plays <paramref name="game"/> to its end. Each turn, every model seat's council move is
        /// decided in one <see cref="DecisionRound"/> on the turn's snapshot, while the scripted and
        /// random players answer at once; the offers of peace that council made are a second round
        /// (or more: a country offered peace twice answers in two, since a round holds an agent once).
        /// Nothing is executed until <see cref="Game.Resolve"/> lands the turn in the game's own order,
        /// and each model move then runs through its round (<see cref="CountryAgent.Admit"/>), which
        /// re-checks it and marks it stale if an earlier move broke it. Whatever never landed is
        /// dropped when the rounds end.
        /// </summary>
        public static async Awaitable PlayToTheEndAsync(Game game, IReadOnlyDictionary<string, IPlayer> players,
            IReadOnlyDictionary<string, CountryAgent> seats, CancellationToken ct = default)
        {
            int guard = game.Rules.MaxTurns + 1;
            while (!game.IsOver)
            {
                ct.ThrowIfCancellationRequested();
                if (guard-- <= 0) throw new InvalidOperationException("The game did not end within its turn limit.");
                await PlayTurnAsync(game, players, seats, ct);
            }
        }

        /// <summary>One turn of <see cref="PlayToTheEndAsync"/>: the council round, the envoy rounds, the resolution.</summary>
        public static async Awaitable<TurnReport> PlayTurnAsync(Game game, IReadOnlyDictionary<string, IPlayer> players,
            IReadOnlyDictionary<string, CountryAgent> seats, CancellationToken ct = default)
        {
            var rounds = new List<DecisionRound>();
            var council = game.BeginTurn();
            var answers = await AnswerAll(council, players, seats, $"seed-{game.Seed}-turn-{game.World.Turn}-council", rounds, ct);
            var envoys = game.SubmitCouncil(answers);
            var replies = await AnswerAll(envoys, players, seats, $"seed-{game.Seed}-turn-{game.World.Turn}-envoys", rounds, ct);
            var report = game.Resolve(replies);
            foreach (var round in rounds) round.End();
            foreach (var seat in seats.Values) seat.EndTurn();
            return report;
        }

        // Model seats decide in rounds, all on the same snapshot; the others answer at once.
        private static async Awaitable<List<CountryAnswer>> AnswerAll(IReadOnlyList<CountryRequest> requests,
            IReadOnlyDictionary<string, IPlayer> players, IReadOnlyDictionary<string, CountryAgent> seats, string id,
            List<DecisionRound> rounds, CancellationToken ct)
        {
            var answers = new List<CountryAnswer>();
            foreach (var r in requests)
                if (players.TryGetValue(r.CountryId, out var p)) answers.Add(new CountryAnswer(r, p.Decide(r)));

            // A round holds each agent once, so the k-th request of each seat goes in round k.
            var layers = requests.Where(r => seats.ContainsKey(r.CountryId))
                .GroupBy(r => r.CountryId)
                .SelectMany(g => g.Select((r, k) => (r, k)))
                .GroupBy(x => x.k)
                .OrderBy(g => g.Key)
                .Select(g => g.Select(x => x.r).ToList())
                .ToList();
            for (int k = 0; k < layers.Count; k++)
            {
                var layer = layers[k];
                var round = await DecisionRound.DecideAsync(layer.Select(r => seats[r.CountryId].CallFor(r)),
                    layers.Count == 1 ? id : $"{id}-{k + 1}", ct);
                rounds.Add(round);
                foreach (var r in layer) answers.Add(seats[r.CountryId].Take(r, round));
            }
            return answers;
        }

        /// <summary>Plays one game per seed with these kinds, seats rotating game by game.</summary>
        public static async Awaitable<List<ModelGameRecord>> PlaySeriesAsync(string[] kinds, IReadOnlyList<int> seeds,
            Func<World, string, CountryAgent> makeSeat, string modelKind, Action<int, ModelGameRecord> progress = null,
            CancellationToken ct = default)
        {
            var records = new List<ModelGameRecord>(seeds.Count);
            for (int g = 0; g < seeds.Count; g++)
            {
                var rotated = Enumerable.Range(0, kinds.Length).Select(i => kinds[(i + g) % kinds.Length]).ToList();
                var record = await PlayAsync(seeds[g], rotated, makeSeat, modelKind, ct);
                records.Add(record);
                progress?.Invoke(g, record);
            }
            return records;
        }
    }

    /// <summary>The numbers a series says about its model seats, beside the game's own summary.</summary>
    public sealed class ModelReport
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public ModelReport(string kind, IReadOnlyList<ModelGameRecord> games)
        {
            Kind = kind;
            Games = games;
            var all = games.SelectMany(g => g.Decisions).ToList();
            Decisions = all.Count;
            Council = all.Count(d => d.Moment == Moment.Council);
            Envoy = all.Count(d => d.Moment == Moment.Envoy);
            Contained = all.Count(d => d.Contained);
            Rejected = all.Count(d => d.Rejected);
            PipelineErrors = all.Count(d => d.Error != null);
            NotOffered = all.Count(d => !d.Offered);
            TargetsIgnored = all.Count(d => d.TargetIgnored);
            RefusedOnExecute = all.Count(d => d.Execution == ExecutionStatus.Refused);
            IllegalAnswers = all.Count(d => d.Moment == Moment.Council && d.ModelAction != null && !d.ModelAnswerLegal);
            ContainedLegal = all.Count(d => d.Contained && d.ModelAnswerLegal);
            ContainedLegalAttacks = all.Count(d => d.Contained && d.ModelAnswerLegal && d.ModelAction == Actions.Attack);
            var attacks = games.SelectMany(g => g.Record.Reports.SelectMany(r => r.Outcomes)
                .Where(o => g.ModelCountries.Contains(o.Request.CountryId) && !o.Refused && o.Applied.Action == Actions.Attack)).ToList();
            AttacksLanded = attacks.Count;
            AttacksWon = attacks.Count(o => o.Text != null && o.Text.StartsWith("took ", StringComparison.Ordinal));
            var latencies = all.Where(d => d.Error == null).Select(d => (double)d.LatencySeconds).OrderBy(x => x).ToList();
            MedianLatency = latencies.Count == 0 ? 0 : latencies[latencies.Count / 2];
            P90Latency = latencies.Count == 0 ? 0 : latencies[(int)(latencies.Count * 0.9)];
            MeanPromptTokens = all.Count == 0 ? 0 : all.Average(d => (double)d.PromptTokens);
            DecisionsPerGame = games.Count == 0 ? 0 : (double)Decisions / games.Count;
            GuardCounts = all.Where(d => d.Guard != null).GroupBy(d => d.Guard).ToDictionary(g => g.Key, g => g.Count());
            CouncilActions = all.Where(d => d.Moment == Moment.Council)
                .GroupBy(d => d.Move?.Action ?? Actions.None).ToDictionary(g => g.Key, g => g.Count());
            AcceptedPeace = all.Count(d => d.Moment == Moment.Envoy && d.Move?.Action == Actions.AcceptPeace);
            StaleAtResolution = games.Sum(g => g.Record.Reports.SelectMany(r => r.Outcomes)
                .Count(o => g.ModelCountries.Contains(o.Request.CountryId) && o.Refused));
        }

        public string Kind { get; }
        public IReadOnlyList<ModelGameRecord> Games { get; }
        public int Decisions { get; }
        public int Council { get; }
        public int Envoy { get; }
        public int Contained { get; }
        public int Rejected { get; }
        public int PipelineErrors { get; }
        public int NotOffered { get; }
        public int TargetsIgnored { get; }
        public int RefusedOnExecute { get; }
        /// <summary>Council answers, before any guard, that were not a move the game offered: an illegal pair or an unavailable move.</summary>
        public int IllegalAnswers { get; }
        /// <summary>Decisions a guard turned into none although the model's answer was legal.</summary>
        public int ContainedLegal { get; }
        /// <summary>Of <see cref="ContainedLegal"/>, those that were attacks (gap N4).</summary>
        public int ContainedLegalAttacks { get; }
        /// <summary>The model's attacks that reached a battle, and those it won.</summary>
        public int AttacksLanded { get; }
        public int AttacksWon { get; }
        /// <summary>The model's moves the game refused at resolution, stale ones and the framework's refusals both.</summary>
        public int StaleAtResolution { get; }
        public double MedianLatency { get; }
        public double P90Latency { get; }
        public double MeanPromptTokens { get; }
        public double DecisionsPerGame { get; }
        public int AcceptedPeace { get; }
        public Dictionary<string, int> GuardCounts { get; }
        public Dictionary<string, int> CouncilActions { get; }

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## The model seat: {Kind}");
            sb.AppendLine();
            sb.AppendLine("| Measure | Value |");
            sb.AppendLine("|---|---:|");
            sb.AppendLine($"| Decisions (council / envoy) | {Decisions} ({Council} / {Envoy}) |");
            sb.AppendLine($"| Decisions per game | {F(DecisionsPerGame)} |");
            sb.AppendLine($"| Median / 90th percentile latency per decision, s | {F2(MedianLatency)} / {F2(P90Latency)} |");
            sb.AppendLine($"| Mean prompt tokens | {MeanPromptTokens.ToString("0", Inv)} |");
            sb.AppendLine($"| Council answers not offered, before the guards | {IllegalAnswers} |");
            sb.AppendLine($"| Contained by a guard (rewritten to none) | {Contained} |");
            sb.AppendLine($"| Of those, on a legal answer / a legal attack | {ContainedLegal} / {ContainedLegalAttacks} |");
            sb.AppendLine($"| Attacks landed / won | {AttacksLanded} / {AttacksWon} |");
            sb.AppendLine($"| Rejected by a guard | {Rejected} |");
            sb.AppendLine($"| Pipeline errors (no decision) | {PipelineErrors} |");
            sb.AppendLine($"| Move not among those offered | {NotOffered} |");
            sb.AppendLine($"| Target named for a move that takes none (dropped) | {TargetsIgnored} |");
            sb.AppendLine($"| Refused by Agent.Execute at landing | {RefusedOnExecute} |");
            sb.AppendLine($"| Refused at resolution, all causes | {StaleAtResolution} |");
            sb.AppendLine($"| Peace accepted / envoys answered | {AcceptedPeace} / {Envoy} |");
            sb.AppendLine();
            sb.AppendLine("| Guard | Firings |");
            sb.AppendLine("|---|---:|");
            if (GuardCounts.Count == 0) sb.AppendLine("| none | 0 |");
            foreach (var kv in GuardCounts.OrderByDescending(kv => kv.Value)) sb.AppendLine($"| {kv.Key} | {kv.Value} |");
            sb.AppendLine();
            sb.AppendLine("| Council move | Share |");
            sb.AppendLine("|---|---:|");
            foreach (string a in Actions.Council)
            {
                int n = CouncilActions.TryGetValue(a, out int c) ? c : 0;
                sb.AppendLine($"| {a} | {(Council == 0 ? 0 : 100.0 * n / Council).ToString("0", Inv)}% ({n}) |");
            }
            return sb.ToString();
        }

        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("seed,turn,country,moment,model_action,model_target,action,target,move,offered,model_answer_legal,target_ignored,latency_s,prompt_tokens,guard,verdict,error,execution,statement");
            foreach (var d in Games.SelectMany(g => g.Decisions))
                sb.AppendLine(string.Join(",", d.Seed, d.Turn, d.Country, d.Moment, d.ModelAction, d.ModelTarget, d.Action, d.Target,
                    d.Move, d.Offered, d.ModelAnswerLegal, d.TargetIgnored, d.LatencySeconds.ToString("0.000", Inv), d.PromptTokens, d.Guard, d.Verdict, d.Error, d.Execution,
                    Csv(d.Statement)));
            return sb.ToString();
        }

        private static string Csv(string s) => string.IsNullOrEmpty(s) ? "" : "\"" + s.Replace("\"", "\"\"").Replace("\n", " ") + "\"";
        private static string F(double v) => v.ToString("0.0", Inv);
        private static string F2(double v) => v.ToString("0.00", Inv);
    }

    /// <summary>
    /// Plays a series with a model seat and writes it to a folder, as <see cref="StrategyRuns"/>
    /// does, plus <c>model.csv</c> (every model decision) and <c>decisions.jsonl</c> (the
    /// framework's <see cref="DecisionLog"/>, prompts included).
    /// </summary>
    public static class StrategyModelRuns
    {
        public static async Awaitable<(SeriesSummary summary, ModelReport model)> RunAsync(string folder, string seatKind,
            ILLMProvider provider, string modelKind, ModelSeatOptions options, int games, int firstSeed,
            Action<string> progress = null, CancellationToken ct = default)
        {
            Directory.CreateDirectory(folder);
            var kinds = ModelGameRunner.SeatAgainstScripted(seatKind);
            var seeds = GameRunner.Seeds(games, firstSeed);
            var started = DateTime.Now;
            List<ModelGameRecord> records;
            using (var log = provider != null ? DecisionLog.Start(Path.Combine(folder, "decisions.jsonl"), includePrompt: true) : null)
            {
                records = await ModelGameRunner.PlaySeriesAsync(kinds, seeds,
                    (world, id) => new CountryAgent(world, id, provider, options), modelKind,
                    (i, r) => progress?.Invoke($"game {i + 1}/{games} (seed {r.Record.Seed}): {r.Record.Result.Winner} won on turn {r.Record.Result.Turns}; " +
                                               $"{string.Join(", ", r.ModelCountries.Select(c => $"{c} held {r.Record.Result.Provinces[c]}"))}; " +
                                               $"{(DateTime.Now - started).TotalSeconds:0} s so far"),
                    ct);
            }

            var summary = new SeriesSummary($"{modelKind} against scripted", records.Select(r => r.Record).ToList());
            var report = new ModelReport(modelKind, records);
            File.WriteAllText(Path.Combine(folder, "summary.md"), summary.ToMarkdown() + "\n" + report.ToMarkdown() +
                $"\nWall time: {(DateTime.Now - started).TotalSeconds:0} s. Arm: {options?.Arm ?? "default"}.\n");
            File.WriteAllText(Path.Combine(folder, "games.csv"), summary.ToCsv());
            File.WriteAllText(Path.Combine(folder, "model.csv"), report.ToCsv());
            foreach (var r in records)
                File.WriteAllText(Path.Combine(folder, $"game-{r.Record.Seed:D4}.md"), GameLog.Write(r.Record));
            return (summary, report);
        }
    }
}
