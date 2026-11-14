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
        /// <summary>
        /// Your orders for one writs turn (rules v2, R1), given at once: the court's order, an order for each
        /// army that gets a writ, and a second court order when a writ is left. The rules ask for them round by
        /// round; the session answers each round from this plan, and holds where it has nothing.
        /// </summary>
        public sealed class WritsPlan
        {
            public Move Court = Move.Hold;
            public readonly Dictionary<string, Move> Armies = new Dictionary<string, Move>(StringComparer.Ordinal);
            public Move Court2 = Move.Hold;
            internal bool CourtTaken;

            /// <summary>The advisor's whole advice as a plan: its court order and every army's, as "Take the advice" fills it.</summary>
            public static WritsPlan From(Advice advice)
            {
                var plan = new WritsPlan { Court = advice?.Court ?? Move.Hold };
                if (advice != null) foreach (var kv in advice.Armies) plan.Armies[kv.Key] = kv.Value;
                return plan;
            }

            /// <summary>The order for a request of yours: the first court request takes <see cref="Court"/>, the next <see cref="Court2"/>.</summary>
            public Move For(CountryRequest r)
            {
                Move m;
                if (r.Moment == Moment.Army) m = r.ArmyId != null && Armies.TryGetValue(r.ArmyId, out var a) ? a : Move.Hold;
                else if (!CourtTaken) { CourtTaken = true; m = Court ?? Move.Hold; }
                else m = Court2 ?? Move.Hold;
                // Only what the rules offer on this request lands; anything else holds, as for a model.
                return r.Legal.FirstOrDefault(x => x.Equals(m)) ?? r.Legal.FirstOrDefault(x => x.Action == Actions.None) ?? Move.Hold;
            }
        }

        /// <summary>
        /// The card the model seats use. A game a person plays takes it before the first council and
        /// holds it until the game ends or is left (the lead's decision, 12 November).
        /// </summary>
        public interface ICardLock
        {
            /// <summary>Who holds the card while the game waits for it, or null.</summary>
            string WaitingFor { get; }
            Awaitable Acquire(CancellationToken ct);
            Awaitable Release();
        }

        public enum Waiting { Nothing, Card, Orders, Envoy, Models, Storyteller, Choice }

        /// <summary>How long one round of the rulers' decisions may take before the game stops and says so.</summary>
        public TimeSpan RoundTimeout { get; set; } = TimeSpan.FromSeconds(150);

        public const string YourModel = "you";

        private readonly TableDefinition table;
        private readonly Func<string, World, ILLMProvider> providerFor;
        private readonly ICardLock cardLock;
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
        private readonly List<RecordedDecision> yours = new List<RecordedDecision>();
        private readonly Dictionary<string, CountryAgent> seats = new Dictionary<string, CountryAgent>(StringComparer.Ordinal);
        private AwaitableCompletionSource<Move> pending;
        private Exception failure;
        private readonly List<string> devQueue = new List<string>();
        private readonly List<DevEdit> devApplied = new List<DevEdit>();
        private DecisionLog log;
        private readonly string arm;
        private readonly StoryRules story;
        private Storyteller teller;
        // The writs turn: a WritRuler for each model seat, every decision recorded as the game asked it.
        private readonly Dictionary<string, WritRuler> writSeats = new Dictionary<string, WritRuler>(StringComparer.Ordinal);
        private readonly WritsRecorder recorder;
        private AwaitableCompletionSource<WritsPlan> pendingPlan;
        private readonly MapDefinition map;

        /// <summary>True for the writs turn: armies, a court and a marshal for each model ruler, your orders as one plan a turn.</summary>
        public bool Writs => Game.Rules.Turn == TurnStyle.Writs;

        // ---- the advisor (the lead's answer 29: optional, on by default, turned off or on in game) ----

        private Advisor advisor;
        private CancellationTokenSource adviceCancel;

        /// <summary>
        /// Whether your advisor proposes each turn's plan, on the writs turn. Set before the game or during it; the
        /// next turn follows it. Off: no call is made.
        /// </summary>
        public bool AdvisorOn { get; set; }
        /// <summary>This turn's advice, once it is in; null while the advisor thinks, when it is off, or when it failed.</summary>
        public Advice Advice { get; private set; }
        /// <summary>True while the advisor is deciding this turn's advice.</summary>
        public bool AdvisorThinking { get; private set; }
        /// <summary>Why the advisor gave no advice this turn, or null.</summary>
        public string AdvisorError { get; private set; }
        /// <summary>How many decisions the advisor asked for over the game: 0 when it was never on.</summary>
        public int AdvisorCalls => advisor?.Calls ?? 0;
        /// <summary>The advisor's agents, as the decision log names them; null before the game starts.</summary>
        public string AdvisorCourtId => advisor?.CourtAgentId;

        /// <summary>
        /// An advice decision as the inspector shows any ruler's: its log record where one pairs, marked as advice and
        /// never a move of yours.
        /// </summary>
        public RecordedDecision AdviceDecision(AdviceItem item)
        {
            if (item == null) return null;
            var r = item.Request;
            string moment = r.Moment == Moment.Army ? RecordedDecision.ArmyMoment : "council";
            Newtonsoft.Json.Linq.JObject raw = null;
            foreach (string line in Lines())
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains(item.AgentId)) continue;
                try
                {
                    var o = Newtonsoft.Json.Linq.JObject.Parse(line);
                    if ((string)o["stage"] == "Decided" && (string)o["agentId"] == item.AgentId && (string)o["stimulus"] == item.Stimulus) raw = o;
                }
                catch (Newtonsoft.Json.JsonException) { }
            }
            RecordedDecision d;
            if (raw != null) { raw["turn"] = r.Turn; raw["moment"] = moment; d = GreyBoxStrategy.Replay.Recording.FromLogRecord(raw); }
            else d = new RecordedDecision { Observations = string.Join("\n", r.StandingFacts), StimulusTurn = string.Join("\n", r.TurnFacts), Stimulus = item.Stimulus };
            d.AgentId = item.AgentId; d.Turn = r.Turn; d.Moment = moment; d.ArmyId = r.ArmyId;
            d.Action = item.Move.Action; d.Target = item.Move.Target ?? RecordedDecision.NoTarget;
            d.Statement = item.Record?.Statement ?? d.Statement ?? "";
            d.ModelAction = item.Record?.ModelAction ?? d.ModelAction;
            d.ModelTarget = item.Record?.ModelTarget ?? d.ModelTarget;
            d.ModelName = Model + " · advice";
            d.OfferedMoves = r.Legal.Select(x => x.ToString()).ToList();
            d.OfferedActions = r.Legal.Select(x => x.Action).Distinct().ToList();
            return d;
        }

        // Starts this turn's advice beside the rulers' decisions; it lands in Advice when done. Never awaited by the turn.
        private async void Advise(CountryRequest court)
        {
            if (advisor == null || !AdvisorOn) return;
            adviceCancel?.Cancel();
            var mine = adviceCancel = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
            Advice = null; AdvisorError = null; AdvisorThinking = true;
            try
            {
                var advice = await advisor.AdviseAsync(court, mine.Token);
                if (!mine.IsCancellationRequested) Advice = advice;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!mine.IsCancellationRequested) AdvisorError = e.Message; Debug.LogWarning("Painted Realm: the advisor: " + e.Message); }
            finally { if (adviceCancel == mine) AdvisorThinking = false; }
        }

        private void EndAdvice()
        {
            adviceCancel?.Cancel();
            adviceCancel = null;
            AdvisorThinking = false;
            Advice = null;
            AdvisorError = null;
        }

        /// <param name="map">The map, or null for the Classic board.</param>
        /// <param name="rules">The rules, or null for today's one-move game; the writs turn's (<see cref="GameSetup.Writs"/>) for rule mode on it.</param>
        public RuleSession(string you, string model, string arm, int seed, string logPath, Func<string, World, ILLMProvider> providerFor, ICardLock cardLock = null,
            MapDefinition map = null, GameRules rules = null)
        {
            You = you;
            Model = model;
            Seed = seed;
            LogPath = logPath;
            this.providerFor = providerFor;
            this.cardLock = cardLock;
            table = Tables.Named("personas", model, null, arm, true);
            map = map ?? MapDefinition.Standard();
            this.map = map;
            if (map.Countries.All(c => c.Id != you)) throw new ArgumentException($"No country '{you}'.", nameof(you));
            Game = new Game(seed, map, rules);
            recorder = new WritsRecorder(seed);
            this.arm = arm;
            // A storyteller game: its rules from the arm, and no random table beside it.
            if (table.Options.Storyteller)
            {
                story = table.Options.StoryRules();
                Game.EventTable = false;
                Game.Story = story;
            }
            for (int i = 0; i < map.Countries.Count; i++)
            {
                string id = map.Countries[i].Id;
                Players[id] = id == you
                    ? new RecordedPlayer { Model = YourModel, Persona = null }
                    : new RecordedPlayer { Model = table.SeatFor(i % 5, 0).Model, Persona = table.PersonaFor(i % 5, 0)?.Id };
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
        /// <summary>The storyteller's dilemma waiting for your choice, before the upkeep it lands at; null otherwise.</summary>
        public EventChoice Choice { get; private set; }
        /// <summary>The request behind <see cref="Choice"/>: its legal moves are the options.</summary>
        public CountryRequest ChoiceRequest { get; private set; }
        /// <summary>True when the game has a storyteller.</summary>
        public bool HasStoryteller => story != null;
        /// <summary>The storyteller, once the game runs; null without one.</summary>
        public Storyteller Teller => teller;
        /// <summary>What the offering ruler said with its offer.</summary>
        public string OfferStatement { get; private set; }
        /// <summary>True while the model seats are deciding; they decide while you choose, so it can turn false before your order is in.</summary>
        public bool ModelsThinking { get; private set; }
        /// <summary>Who holds the RTX 5080 while the game waits for it, or null.</summary>
        public string WaitingForCard => cardLock?.WaitingFor;
        /// <summary>True from when this game has the card until it lets it go.</summary>
        public bool HoldsCard { get; private set; }
        /// <summary>When the current wait began: for the card, or for the rulers' round (UTC).</summary>
        public DateTime WaitingSince { get; private set; } = DateTime.UtcNow;
        /// <summary>True once the dev menu has touched this game, or it was played with stand-in rulers: never a measured game.</summary>
        public bool Dev { get; set; }
        /// <summary>Dev edits waiting for the start of the next turn.</summary>
        public IReadOnlyList<string> DevQueued => devQueue;
        /// <summary>Dev edits applied so far, each with the turn it started.</summary>
        public IReadOnlyList<DevEdit> DevApplied => devApplied;

        /// <summary>
        /// Queues a dev edit (<see cref="DevEdits"/>) for the start of the next turn, so the game and
        /// its replay change at the same point. Null when queued, else why not. Marks the game dev.
        /// </summary>
        public string QueueDev(string command)
        {
            string why = DevEdits.WhyNot(Game, command);
            if (why != null) return why;
            devQueue.Add(command);
            Dev = true;
            return null;
        }

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

        /// <summary>
        /// The writs turn: your orders for this turn, while <see cref="WaitingFor"/> is <see cref="Waiting.Orders"/>.
        /// Every order must be one the rules offer now: the court's on <see cref="Orders"/>, an army's from
        /// <see cref="WritLegality.ArmyMoves"/> for an army awaiting orders.
        /// </summary>
        public void Plan(WritsPlan plan)
        {
            if (!Writs) throw new InvalidOperationException("Plan is the writs turn's; the one-move game takes Order.");
            if (WaitingFor != Waiting.Orders || pendingPlan == null || Orders == null) throw new InvalidOperationException("The game is not waiting for your orders.");
            var w = Orders.World;
            if (plan.Court != null && !Orders.Legal.Contains(plan.Court))
                throw new ArgumentException($"{plan.Court} is not offered to your court: {WritLegality.WhyNotCourt(w, You, plan.Court) ?? "not on the list"}.");
            foreach (var kv in plan.Armies)
                if (!WritLegality.ArmyMoves(w, You, kv.Key).Contains(kv.Value))
                    throw new ArgumentException($"{kv.Key} cannot {kv.Value}: {WritLegality.WhyNotArmy(w, You, kv.Key, kv.Value) ?? "not on the list"}.");
            var p = pendingPlan; pendingPlan = null;
            p.SetResult(plan);
        }

        /// <summary>Your answer to the peace offer.</summary>
        public void Answer(bool accept)
        {
            if (WaitingFor != Waiting.Envoy || Offer == null) throw new InvalidOperationException("No peace offer waits for your answer.");
            var p = pending; pending = null;
            p.SetResult(new Move(accept ? Actions.AcceptPeace : Actions.RefusePeace));
        }

        /// <summary>Your answer to the storyteller's dilemma: one of <see cref="Choice"/>'s options.</summary>
        public void Choose(string optionId)
        {
            if (WaitingFor != Waiting.Choice || ChoiceRequest == null) throw new InvalidOperationException("No choice waits for your answer.");
            var move = ChoiceRequest.Legal.FirstOrDefault(m => m.Action == optionId)
                       ?? throw new ArgumentException($"{optionId} is not one of the options: {string.Join(", ", ChoiceRequest.Legal.Select(m => m.Action))}.");
            var p = pending; pending = null;
            p.SetResult(move);
        }

        public void Stop()
        {
            cancel.Cancel();
            var p = pending; pending = null;
            p?.SetCanceled();
            var q = pendingPlan; pendingPlan = null;
            q?.SetCanceled();
        }

        /// <summary>Plays the game to its end, or until <see cref="Stop"/>.</summary>
        public async Awaitable RunAsync()
        {
            Started = true;
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            log = DecisionLog.Start(LogPath, includePrompt: true);
            try
            {
                var providers = new Dictionary<string, ILLMProvider>(StringComparer.Ordinal);
                for (int i = 0; i < map.Countries.Count; i++)
                {
                    string id = map.Countries[i].Id;
                    if (id == You) continue;
                    string model = table.SeatFor(i % 5, 0).Model;
                    if (!providers.TryGetValue(model, out var provider)) providers[model] = provider = providerFor(model, Game.World);
                    if (Writs)
                    {
                        // A court and a marshal for each model ruler, as the writs turn's measured runs have them.
                        var ruler = new WritRuler(Game.World, id, provider, table.Options, table.PersonaFor(i % 5, 0)) { Seed = Seed };
                        writSeats[id] = ruler;
                        Game.Gates[id] = ruler;
                        continue;
                    }
                    var seat = new CountryAgent(Game.World, id, provider, table.Options, table.PersonaFor(i, 0)) { Seed = Seed };
                    seats[id] = seat;
                    Game.Gates[id] = seat;
                }
                // The advisor sits on your seat with the session's model, through the same provider and queue.
                if (Writs)
                {
                    if (!providers.TryGetValue(Model, out var advisorProvider)) providers[Model] = advisorProvider = providerFor(Model, Game.World);
                    advisor = new Advisor(Game.World, You, advisorProvider, table.Options);
                }
                // The storyteller tells on the session's model, through the same provider and queue.
                if (story != null)
                {
                    if (!providers.TryGetValue(Model, out var tellerProvider)) providers[Model] = tellerProvider = providerFor(Model, Game.World);
                    teller = new Storyteller(Game, tellerProvider, table.Options);
                }

                if (cardLock != null)
                {
                    WaitingFor = Waiting.Card;
                    WaitingSince = DateTime.UtcNow;
                    await cardLock.Acquire(cancel.Token);
                    HoldsCard = true;
                    WaitingFor = Waiting.Nothing;
                }

                while (!Game.IsOver)
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    if (Writs) await PlayWritsTurn();
                    else await PlayTurn();
                }
            }
            catch (OperationCanceledException) { Stopped = true; }
            catch (TimeoutException e) { Error = e.Message; Debug.LogWarning("Painted Realm: " + e.Message); }
            catch (Exception e) { Error = e.Message; Debug.LogException(e); }
            finally
            {
                WaitingFor = Waiting.Nothing;
                ModelsThinking = false;
                log.Flush(TimeSpan.FromSeconds(5));
                log.Dispose();
                // Every way out lets the card go: the end, Stop, a timeout, an exception.
                if (HoldsCard)
                {
                    HoldsCard = false;
                    try { await cardLock.Release(); }
                    catch (Exception e) { Debug.LogWarning("Painted Realm: releasing the RTX 5080: " + e.Message); }
                }
                Finished = true;
            }
        }

        private async Awaitable PlayTurn()
        {
            var rounds = new List<DecisionRound>();
            foreach (string command in devQueue)
            {
                string why = DevEdits.Apply(Game, command);
                if (why == null) devApplied.Add(new DevEdit { Turn = Game.NextTurn, Command = command });
                else Debug.LogWarning($"Painted Realm: dev edit '{command}' did not apply: {why}");
            }
            devQueue.Clear();
            if (story != null)
            {
                // The storyteller decides on the state the last turn left, unless the dev menu set this upkeep's event.
                if (!devApplied.Any(d => d.Turn == Game.NextTurn && DevEdits.QueuesEvent(d.Command))) await Tell();
                if (Game.PendingChoices.Count > 0) await Choices(rounds);
            }
            var council = Game.BeginTurn();
            if (story == null) DevEdits.AfterBeginTurn(Game);
            int turn = Game.World.Turn;
            var mine = council.FirstOrDefault(r => r.CountryId == You);

            // The model seats decide at once, on the council's snapshot, while you choose.
            ModelsThinking = true;
            WaitingSince = DateTime.UtcNow;
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
            // Several rulers may offer you peace in one turn: each envoy waits for its own answer.
            var myOffers = envoys.Where(r => r.CountryId == You).ToList();
            var replies = new List<CountryAnswer>();
            ModelsThinking = envoys.Any(r => seats.ContainsKey(r.CountryId));
            if (ModelsThinking) WaitingSince = DateTime.UtcNow;
            var modelReplies = Decide(envoys.Where(r => seats.ContainsKey(r.CountryId)).ToList(), $"seed-{Seed}-turn-{turn}-envoys", rounds);
            foreach (var myOffer in myOffers)
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

        // ---- the writs turn ----

        // Rounds of requests until every order is in, then the treaty answers, then the turn lands. Your requests
        // are answered from one plan you give at the first, while the model rulers decide theirs.
        private async Awaitable PlayWritsTurn()
        {
            ApplyDev();
            if (story != null)
            {
                if (!devApplied.Any(d => d.Turn == Game.NextTurn && DevEdits.QueuesEvent(d.Command))) await Tell();
                if (Game.PendingChoices.Count > 0) await WritsChoices();
            }
            var requests = Game.BeginTurn();
            if (story == null) DevEdits.AfterBeginTurn(Game);
            WritsPlan plan = null;
            while (requests.Count > 0)
            {
                var theirs = requests.Where(r => writSeats.ContainsKey(r.CountryId)).ToList();
                ModelsThinking = theirs.Count > 0;
                if (ModelsThinking) WaitingSince = DateTime.UtcNow;
                var models = DecideWrits(theirs);
                var answers = new List<CountryAnswer>();
                foreach (var r in requests.Where(r => r.CountryId == You))
                {
                    Move move;
                    if (r.Moment == Moment.Envoy)
                    {
                        // A treaty term put to you: its card waits for your answer.
                        OfferStatement = writSeats.TryGetValue(r.OfferedBy, out var by)
                            ? by.Decisions.LastOrDefault(d => d.Turn == r.Turn && Actions.IsTerm(d.Action))?.Statement : null;
                        move = await Ask(Waiting.Envoy, () => Offer = r);
                        Offer = null; OfferStatement = null;
                    }
                    else
                    {
                        if (plan == null)
                        {
                            // The advisor is asked as your turn opens, beside the rulers, never before them.
                            Advise(r);
                            plan = await AskPlan(r);
                            EndAdvice();
                        }
                        move = plan.For(r);
                    }
                    answers.Add(new CountryAnswer(r, move));
                    recorder.Person(r, move, YourModel);
                }
                WaitingFor = Waiting.Models;
                answers.AddRange(await models);
                requests = Game.Submit(answers);
            }
            var report = Game.Resolve();
            foreach (var seat in writSeats.Values) seat.Learn(report);
            foreach (var seat in writSeats.Values) seat.EndTurn();
            WaitingFor = Waiting.Nothing;
            Resolved++;
        }

        private async Awaitable<WritsPlan> AskPlan(CountryRequest court)
        {
            if (failure != null) throw failure;
            pendingPlan = new AwaitableCompletionSource<WritsPlan>();
            var awaitable = pendingPlan.Awaitable;
            Orders = court;
            WaitingSince = DateTime.UtcNow;
            WaitingFor = Waiting.Orders;
            var plan = await awaitable;
            Orders = null;
            return plan;
        }

        // A dilemma on the writs turn: the model rulers' courts choose while you choose.
        private async Awaitable WritsChoices()
        {
            var asks = Game.ChoiceRequests();
            var models = DecideWrits(asks.Where(r => writSeats.ContainsKey(r.CountryId)).ToList());
            var mine = asks.FirstOrDefault(r => r.CountryId == You);
            if (mine != null)
            {
                var choice = Game.PendingChoices.First(c => c.Country == You && c.Chosen == null);
                var move = await Ask(Waiting.Choice, () => { Choice = choice; ChoiceRequest = mine; });
                Choice = null; ChoiceRequest = null;
                string why = Game.Choose(You, move.Action, "person");
                if (why != null) Debug.LogWarning("Painted Realm: your choice did not land: " + why);
                recorder.Person(mine, move, YourModel);
            }
            WaitingFor = Waiting.Models;
            Game.SubmitChoices(await models);
            WaitingFor = Waiting.Nothing;
        }

        // The model rulers' requests, one ruler's back to back, each within the round timeout.
        private async Awaitable<List<CountryAnswer>> DecideWrits(List<CountryRequest> requests)
        {
            try
            {
                var answers = new List<CountryAnswer>();
                foreach (var r in requests.OrderBy(r => r.CountryId, StringComparer.Ordinal))
                {
                    var seat = writSeats[r.CountryId];
                    string stimulus = seat.StimulusFor(r);
                    int before = seat.Decisions.Count;
                    CountryAnswer answer;
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token))
                    using (OnMainThreadAfter(RoundTimeout, timeout))
                    {
                        try { answer = await seat.DecideAsync(r, timeout.Token); }
                        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
                        {
                            throw new TimeoutException($"{r.CountryId}'s {(r.Moment == Moment.Army ? "marshal" : "court")} did not answer within {RoundTimeout.TotalSeconds:0} seconds on turn {r.Turn}. " +
                                "The server may be busy with another client's requests, or unreachable. Go back to the menu and try again when the card is free.");
                        }
                    }
                    answers.Add(answer);
                    recorder.Model(r, answer, stimulus, seat.Decisions.Count > before ? seat.Decisions[seat.Decisions.Count - 1] : null, Players[r.CountryId].Model);
                }
                ModelsThinking = false;
                return answers;
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                Fail(e);
                throw;
            }
        }

        private void ApplyDev()
        {
            foreach (string command in devQueue)
            {
                string why = DevEdits.Apply(Game, command);
                if (why == null) devApplied.Add(new DevEdit { Turn = Game.NextTurn, Command = command });
                else Debug.LogWarning($"Painted Realm: dev edit '{command}' did not apply: {why}");
            }
            devQueue.Clear();
        }

        // The storyteller's call, with the rulers' timeout: it never sits silent either.
        private async Awaitable Tell()
        {
            WaitingFor = Waiting.Storyteller;
            WaitingSince = DateTime.UtcNow;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token))
            using (OnMainThreadAfter(RoundTimeout, timeout))
            {
                try { await teller.TellAsync(timeout.Token); }
                catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
                {
                    throw new TimeoutException($"The storyteller did not answer within {RoundTimeout.TotalSeconds:0} seconds before turn {Game.NextTurn}. " +
                        "The server may be busy with another client's requests, or unreachable. Go back to the menu and try again when the card is free.");
                }
            }
            WaitingFor = Waiting.Nothing;
        }

        // A dilemma: the model seats it reaches choose in a round of their own while you choose, before the upkeep.
        private async Awaitable Choices(List<DecisionRound> rounds)
        {
            int turn = Game.NextTurn;
            var asks = Game.ChoiceRequests();
            var theirs = asks.Where(r => seats.ContainsKey(r.CountryId)).ToList();
            ModelsThinking = theirs.Count > 0;
            if (ModelsThinking) WaitingSince = DateTime.UtcNow;
            var models = Decide(theirs, $"seed-{Seed}-turn-{turn}-choices", rounds);
            var mine = asks.FirstOrDefault(r => r.CountryId == You);
            if (mine != null)
            {
                var choice = Game.PendingChoices.First(c => c.Country == You && c.Chosen == null);
                var move = await Ask(Waiting.Choice, () => { Choice = choice; ChoiceRequest = mine; });
                Choice = null; ChoiceRequest = null;
                string why = Game.Choose(You, move.Action, "person");
                if (why != null) Debug.LogWarning("Painted Realm: your choice did not land: " + why);
                Record(mine, move);
            }
            WaitingFor = Waiting.Models;
            Game.SubmitChoices(await models);
            WaitingFor = Waiting.Nothing;
        }

        private async Awaitable<Move> Ask(Waiting what, Action show)
        {
            if (failure != null) throw failure;
            pending = new AwaitableCompletionSource<Move>();
            var awaitable = pending.Awaitable;
            show();
            WaitingFor = what;
            return await awaitable;
        }

        // The model seats' requests, in rounds: a seat asked twice (two offers of peace) goes in a
        // second round, as ModelGameRunner does. A round that takes longer than RoundTimeout stops
        // the game with a message, even while it waits for your order, so it never sits silent.
        private async Awaitable<List<CountryAnswer>> Decide(List<CountryRequest> requests, string id, List<DecisionRound> rounds)
        {
            try
            {
                var answers = new List<CountryAnswer>();
                var layers = requests.GroupBy(r => r.CountryId)
                    .SelectMany(g => g.Select((r, k) => (r, k)))
                    .GroupBy(x => x.k).OrderBy(g => g.Key)
                    .Select(g => g.Select(x => x.r).ToList()).ToList();
                for (int k = 0; k < layers.Count; k++)
                {
                    var layer = layers[k];
                    DecisionRound round;
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token))
                    using (OnMainThreadAfter(RoundTimeout, timeout))
                    {
                        try
                        {
                            round = await DecisionRound.DecideAsync(layer.Select(r => seats[r.CountryId].CallFor(r)),
                                layers.Count == 1 ? id : $"{id}-{k + 1}", timeout.Token);
                        }
                        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
                        {
                            throw new TimeoutException($"The rulers did not answer within {RoundTimeout.TotalSeconds:0} seconds on turn {requests[0].Turn}. " +
                                "The server may be busy with another client's requests, or unreachable. Go back to the menu and try again when the card is free.");
                        }
                    }
                    rounds.Add(round);
                    foreach (var r in layer) answers.Add(seats[r.CountryId].Take(r, round));
                }
                ModelsThinking = false; // they have decided, whether or not you have
                return answers;
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                Fail(e);
                throw;
            }
        }

        // A failure while the game waits for you ends the wait, so the game stops and says why.
        private void Fail(Exception e)
        {
            failure = e;
            var p = pending; pending = null;
            p?.SetException(e);
            var q = pendingPlan; pendingPlan = null;
            q?.SetException(e);
        }

        // Cancels after a delay, on the main thread: a timer's own thread would resume the round's
        // awaits off the main thread.
        private static IDisposable OnMainThreadAfter(TimeSpan delay, CancellationTokenSource cts)
        {
            var context = SynchronizationContext.Current;
            if (context == null) { cts.CancelAfter(delay); return new CancellationTokenSource(); }
            return new Timer(_ => context.Post(__ => { try { cts.Cancel(); } catch (ObjectDisposedException) { } }, null),
                null, delay, Timeout.InfiniteTimeSpan);
        }

        private void Record(CountryRequest request, Move move)
        {
            string moment = request.Moment == Moment.Envoy ? "envoys" : request.Moment == Moment.Choice ? RecordedDecision.ChoiceMoment : "council";
            yours.Add(new RecordedDecision
            {
                AgentId = You,
                Turn = request.Turn,
                Moment = moment,
                ChainId = $"seed-{Seed}-turn-{request.Turn}-{moment}",
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
            if (Writs)
            {
                // The writs turn: every ruler's decision as the game asked it, yours beside them; the storyteller's from the log.
                var lines = Lines().ToList();
                var w = new GreyBoxStrategy.Replay.Recording { Seed = Seed, Title = $"You rule {Cap(You)}, the writs turn, seed {Seed}", Source = "rule", Players = Players };
                if (story != null)
                    w.Decisions.AddRange(GreyBoxStrategy.Replay.Recording.FromDecisionLog(lines, Seed, Players).Decisions.Where(d => d.IsStory || d.IsTitle));
                w.Decisions.AddRange(recorder.Decisions(lines));
                w.DevEdits.AddRange(devApplied);
                w.Dev = Dev;
                if (story != null) w.StoryArm = arm;
                return w;
            }
            var rec = GreyBoxStrategy.Replay.Recording.FromDecisionLog(Lines(), Seed, Players, $"You rule {Cap(You)}, seed {Seed}");
            rec.Source = "rule";
            rec.Decisions.AddRange(yours);
            rec.DevEdits.AddRange(devApplied);
            rec.Dev = Dev;
            if (story != null) rec.StoryArm = arm;
            return rec;
        }

        /// <summary>The turns that have resolved, as the screen plays them.</summary>
        public ReplayGame BuildReplay()
        {
            var g = ReplayBuilder.Build(BuildRecording(), map, Game.Rules, partial: true);
            // A turn whose decisions are all in but which has not resolved yet would play without
            // the refusals Agent.Execute writes when the moves land: keep only resolved turns.
            if (g.Turns.Count > Resolved) g.Turns.RemoveRange(Resolved, g.Turns.Count - Resolved);
            if (Resolved < g.Turns.Count || !Game.IsOver) g.Result = null;
            return g;
        }

        private static string Cap(string id) => string.IsNullOrEmpty(id) ? "" : char.ToUpperInvariant(id[0]) + id.Substring(1);
    }
}
