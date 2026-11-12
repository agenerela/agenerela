using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using GreyBoxStrategy.Replay;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Rule mode without a scene: you rule one country, four model rulers with personas the
    /// others. Each turn the model seats decide in one <c>DecisionRound</c> on the council's
    /// snapshot while the game waits for your order; a peace offer to you waits for your answer;
    /// then the turn resolves. The screen plays the resolution back from <see cref="BuildReplay"/>, as
    /// it plays a recorded game.
    /// </summary>
    /// <remarks>
    /// <para>Your seat is offered exactly the moves a model seat is offered on the same state:
    /// <see cref="Legality.CouncilMoves"/>, the same function the game re-checks every move with.
    /// An order that is not on the list is refused by <see cref="Order"/>.</para>
    /// <para>The model seats' decisions go to the framework's <c>DecisionLog</c>, yours into the
    /// recording beside them, so the replay is built from both by the same code as any game.</para>
    /// </remarks>
    public sealed class RuleSession
    {
        /// <summary>The card the model seats use, taken for each round of model calls only.</summary>
        public interface ICardLock
        {
            /// <summary>Who holds the card while a round waits for it, or null.</summary>
            string WaitingFor { get; }
            Awaitable Acquire(CancellationToken ct);
            Awaitable Release();
        }

        public enum Waiting { Nothing, Orders, Envoy, Models }

        public const string YourModel = "you";

        private readonly TableDefinition table;
        private readonly Func<string, World, ILLMProvider> providerFor;
        private readonly ICardLock cardLock;
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
        private readonly List<RecordedDecision> yours = new List<RecordedDecision>();
        private readonly Dictionary<string, CountryAgent> seats = new Dictionary<string, CountryAgent>(StringComparer.Ordinal);
        private AwaitableCompletionSource<Move> pending;
        private DecisionLog log;

        public RuleSession(string you, string model, string arm, int seed, string logPath, Func<string, World, ILLMProvider> providerFor, ICardLock cardLock = null)
        {
            You = you;
            Model = model;
            Seed = seed;
            LogPath = logPath;
            this.providerFor = providerFor;
            this.cardLock = cardLock;
            table = Tables.Named("personas", model, null, arm, true);
            var map = MapDefinition.Standard();
            if (map.Countries.All(c => c.Id != you)) throw new ArgumentException($"No country '{you}'.", nameof(you));
            Game = new Game(seed, map);
            for (int i = 0; i < map.Countries.Count; i++)
            {
                string id = map.Countries[i].Id;
                Players[id] = id == you
                    ? new RecordedPlayer { Model = YourModel, Persona = null }
                    : new RecordedPlayer { Model = table.SeatFor(i, 0).Model, Persona = table.PersonaFor(i, 0)?.Id };
            }
        }

        public string You { get; }
        public string Model { get; }
        public int Seed { get; }
        public string LogPath { get; }
        public Game Game { get; }
        public Dictionary<string, RecordedPlayer> Players { get; } = new Dictionary<string, RecordedPlayer>(StringComparer.Ordinal);

        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public bool Stopped { get; private set; }
        public string Error { get; private set; }
        public GameResult Result => Game.Result;

        /// <summary>What the game waits on now.</summary>
        public Waiting WaitingFor { get; private set; }
        /// <summary>Your council request while the game waits for your order: its <c>Legal</c> moves are your choices.</summary>
        public CountryRequest Orders { get; private set; }
        /// <summary>Your envoy request while a peace offer waits for your answer.</summary>
        public CountryRequest Offer { get; private set; }
        /// <summary>What the offering ruler said with its offer.</summary>
        public string OfferStatement { get; private set; }
        /// <summary>True while the model seats are deciding; they decide while you choose, so it can turn false before your order is in.</summary>
        public bool ModelsThinking { get; private set; }
        /// <summary>Who holds the RTX 5080 while a round waits for it, or null.</summary>
        public string WaitingForCard => cardLock?.WaitingFor;
        /// <summary>Turns resolved so far: the screen plays a turn only once it has.</summary>
        public int Resolved { get; private set; }

        public string Status => !Started ? "not started" : Error != null ? "error: " + Error : Finished ? (Stopped ? "stopped" : "finished") : WaitingFor.ToString();

        /// <summary>Your order for this turn. It must be one of <see cref="Orders"/>' legal moves.</summary>
        public void Order(Move move)
        {
            if (WaitingFor != Waiting.Orders || Orders == null) throw new InvalidOperationException("The game is not waiting for your order.");
            if (move == null || !Orders.Legal.Contains(move))
                throw new ArgumentException($"{move} is not offered to {You} this turn: {Legality.WhyNot(Orders.World, You, move ?? Move.Hold) ?? "not on the list"}.");
            var p = pending; pending = null;
            p.SetResult(Orders.Legal.First(m => m.Equals(move)));
        }

        /// <summary>Your answer to the peace offer.</summary>
        public void Answer(bool accept)
        {
            if (WaitingFor != Waiting.Envoy || Offer == null) throw new InvalidOperationException("No peace offer waits for your answer.");
            var p = pending; pending = null;
            p.SetResult(new Move(accept ? Actions.AcceptPeace : Actions.RefusePeace));
        }

        public void Stop()
        {
            cancel.Cancel();
            var p = pending; pending = null;
            p?.SetCanceled();
        }

        /// <summary>Plays the game to its end, or until <see cref="Stop"/>.</summary>
        public async Awaitable RunAsync()
        {
            Started = true;
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            log = DecisionLog.Start(LogPath, includePrompt: true);
            try
            {
                var map = MapDefinition.Standard();
                var providers = new Dictionary<string, ILLMProvider>(StringComparer.Ordinal);
                for (int i = 0; i < map.Countries.Count; i++)
                {
                    string id = map.Countries[i].Id;
                    if (id == You) continue;
                    string model = table.SeatFor(i, 0).Model;
                    if (!providers.TryGetValue(model, out var provider)) providers[model] = provider = providerFor(model, Game.World);
                    var seat = new CountryAgent(Game.World, id, provider, table.Options, table.PersonaFor(i, 0)) { Seed = Seed };
                    seats[id] = seat;
                    Game.Gates[id] = seat;
                }

                while (!Game.IsOver)
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    await PlayTurn();
                }
            }
            catch (OperationCanceledException) { Stopped = true; }
            catch (Exception e) { Error = e.Message; Debug.LogException(e); }
            finally
            {
                WaitingFor = Waiting.Nothing;
                ModelsThinking = false;
                log.Flush(TimeSpan.FromSeconds(5));
                log.Dispose();
                Finished = true;
            }
        }

        private async Awaitable PlayTurn()
        {
            var rounds = new List<DecisionRound>();
            var council = Game.BeginTurn();
            int turn = Game.World.Turn;
            var mine = council.FirstOrDefault(r => r.CountryId == You);

            // The model seats decide at once, on the council's snapshot, while you choose.
            ModelsThinking = true;
            var models = Decide(council.Where(r => seats.ContainsKey(r.CountryId)).ToList(), $"seed-{Seed}-turn-{turn}-council", rounds);
            var answers = new List<CountryAnswer>();
            if (mine != null)
            {
                var move = await Ask(Waiting.Orders, () => Orders = mine);
                Orders = null;
                answers.Add(new CountryAnswer(mine, move));
                Record(mine, move);
            }
            WaitingFor = Waiting.Models;
            answers.AddRange(await models);

            var envoys = Game.SubmitCouncil(answers);
            var myOffer = envoys.FirstOrDefault(r => r.CountryId == You);
            var replies = new List<CountryAnswer>();
            ModelsThinking = envoys.Any(r => seats.ContainsKey(r.CountryId));
            var modelReplies = Decide(envoys.Where(r => seats.ContainsKey(r.CountryId)).ToList(), $"seed-{Seed}-turn-{turn}-envoys", rounds);
            if (myOffer != null)
            {
                OfferStatement = seats.TryGetValue(myOffer.OfferedBy, out var by)
                    ? by.Decisions.LastOrDefault(d => d.Turn == turn && d.Moment == Moment.Council)?.Statement : null;
                var move = await Ask(Waiting.Envoy, () => Offer = myOffer);
                Offer = null; OfferStatement = null;
                replies.Add(new CountryAnswer(myOffer, move));
                Record(myOffer, move);
            }
            WaitingFor = Waiting.Models;
            replies.AddRange(await modelReplies);

            var report = Game.Resolve(replies);
            foreach (var round in rounds) round.End();
            foreach (var seat in seats.Values) seat.Learn(report);
            foreach (var seat in seats.Values) seat.EndTurn();
            WaitingFor = Waiting.Nothing;
            Resolved++;
        }

        private async Awaitable<Move> Ask(Waiting what, Action show)
        {
            pending = new AwaitableCompletionSource<Move>();
            var awaitable = pending.Awaitable;
            show();
            WaitingFor = what;
            return await awaitable;
        }

        // The model seats' requests, in rounds: a seat asked twice (two offers of peace) goes in a
        // second round, as ModelGameRunner does. Each round holds the card only while it decides.
        private async Awaitable<List<CountryAnswer>> Decide(List<CountryRequest> requests, string id, List<DecisionRound> rounds)
        {
            var answers = new List<CountryAnswer>();
            var layers = requests.GroupBy(r => r.CountryId)
                .SelectMany(g => g.Select((r, k) => (r, k)))
                .GroupBy(x => x.k).OrderBy(g => g.Key)
                .Select(g => g.Select(x => x.r).ToList()).ToList();
            for (int k = 0; k < layers.Count; k++)
            {
                var layer = layers[k];
                if (cardLock != null) await cardLock.Acquire(cancel.Token);
                DecisionRound round;
                try
                {
                    round = await DecisionRound.DecideAsync(layer.Select(r => seats[r.CountryId].CallFor(r)),
                        layers.Count == 1 ? id : $"{id}-{k + 1}", cancel.Token);
                }
                finally
                {
                    if (cardLock != null) await cardLock.Release();
                }
                rounds.Add(round);
                foreach (var r in layer) answers.Add(seats[r.CountryId].Take(r, round));
            }
            ModelsThinking = false; // they have decided, whether or not you have
            return answers;
        }

        private void Record(CountryRequest request, Move move)
        {
            bool envoy = request.Moment == Moment.Envoy;
            yours.Add(new RecordedDecision
            {
                AgentId = You,
                Turn = request.Turn,
                Moment = envoy ? "envoys" : "council",
                ChainId = $"seed-{Seed}-turn-{request.Turn}-{(envoy ? "envoys" : "council")}",
                Action = move.Action,
                Target = move.Target ?? RecordedDecision.NoTarget,
                ModelAction = move.Action,
                ModelTarget = move.Target ?? RecordedDecision.NoTarget,
                ModelName = YourModel,
                Statement = "",
                Observations = string.Join("\n", request.StandingFacts),
                StimulusTurn = string.Join("\n", request.TurnFacts),
            });
        }

        /// <summary>Every line the log has written so far; read while it is still being written.</summary>
        public IEnumerable<string> Lines()
        {
            if (LogPath == null || !File.Exists(LogPath)) return Array.Empty<string>();
            if (!Finished) log?.Flush(TimeSpan.FromMilliseconds(200));
            using (var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var r = new StreamReader(fs))
                return r.ReadToEnd().Split('\n');
        }

        /// <summary>The game so far: the model seats' decisions from the log, yours beside them.</summary>
        public GreyBoxStrategy.Replay.Recording BuildRecording()
        {
            var rec = GreyBoxStrategy.Replay.Recording.FromDecisionLog(Lines(), Seed, Players, $"You rule {Cap(You)}, seed {Seed}");
            rec.Source = "rule";
            rec.Decisions.AddRange(yours);
            return rec;
        }

        /// <summary>The turns that have resolved, as the screen plays them.</summary>
        public ReplayGame BuildReplay()
        {
            var g = ReplayBuilder.Build(BuildRecording(), partial: true);
            // A turn whose decisions are all in but which has not resolved yet would play without
            // the refusals Agent.Execute writes when the moves land: keep only resolved turns.
            if (g.Turns.Count > Resolved) g.Turns.RemoveRange(Resolved, g.Turns.Count - Resolved);
            if (Resolved < g.Turns.Count || !Game.IsOver) g.Result = null;
            return g;
        }

        private static string Cap(string id) => string.IsNullOrEmpty(id) ? "" : char.ToUpperInvariant(id[0]) + id.Substring(1);
    }
}
