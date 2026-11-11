using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;

namespace GreyBoxStrategy
{
    /// <summary>What one model decision came to, for the run's tables.</summary>
    public sealed class ModelDecision
    {
        public int Seed;
        public int Turn;
        public string Country;
        /// <summary>The seat's persona, or null for stage 2's identity.</summary>
        public string Persona;
        /// <summary>The <see cref="DecisionRound"/> it was decided in, by the round's id (its chain id); null when decided alone.</summary>
        public string Round;
        public Moment Moment;
        /// <summary>What the model answered, before any guard.</summary>
        public string ModelAction;
        public string ModelTarget;
        /// <summary>What the agent decided, after the guards: the model's answer, or none.</summary>
        public string Action;
        public string Target;
        public string Statement;
        public float LatencySeconds;
        public int PromptTokens;
        /// <summary>The guard that stopped the decision, or null.</summary>
        public string Guard;
        public GuardVerdictKind? Verdict;
        public string GuardReason;
        /// <summary>Set when there was no decision at all.</summary>
        public PipelineErrorKind? Error;
        /// <summary>The move as the game received it.</summary>
        public Move Move;
        /// <summary>Whether that move was one the game offered.</summary>
        public bool Offered;
        /// <summary>What <c>Agent.Execute</c> did when the move landed, or null when it never got that far.</summary>
        public ExecutionStatus? Execution;
        public string ExecutionReason;
        /// <summary>Whether its round refused it as stale when it landed (<see cref="RoundEntry.Stale"/>): legal when decided, broken by an earlier move. False outside a round.</summary>
        public bool Stale;

        /// <summary>The model named a target for a move that takes none; the framework dropped it (<c>DecisionTelemetry.TargetIgnored</c>), or the seat did.</summary>
        public bool TargetIgnored;

        /// <summary>
        /// Whether the model's own answer, before any guard, was a move the game offered on that
        /// snapshot (a target on a move that takes none is ignored; for an envoy, accept, refuse or
        /// none). False when there was no answer.
        /// </summary>
        public bool ModelAnswerLegal;

        public bool Contained => Verdict == GuardVerdictKind.RewriteToNone;
        public bool Rejected => Verdict == GuardVerdictKind.Reject;
    }

    /// <summary>
    /// A target the agent can name: a province or a country, described as it stands now, which
    /// the framework lists by id with this line after it.
    /// </summary>
    internal sealed class StrategyTarget : IDescribedTarget
    {
        private readonly World world;
        private readonly string self;

        public StrategyTarget(World world, string self, string id)
        {
            this.world = world;
            this.self = self;
            Id = id;
        }

        public string Id { get; }
        public string Description => Legality.Describe(world, self, Id);
    }

    /// <summary>
    /// The targets of one country's agent, collected on every decision: provinces in category
    /// <c>ours</c>, <c>neutral</c> or <c>foreign</c>, and every other living country in
    /// <c>at_war</c>, <c>at_peace</c> or <c>truce</c>. Which of them a move can take now is the
    /// per-target check's job; the category is only what kind of thing the target is.
    /// </summary>
    internal sealed class StrategyTargetSource : ITargetSource
    {
        private readonly World world;
        private readonly string self;

        public StrategyTargetSource(World world, string self)
        {
            this.world = world;
            this.self = self;
        }

        public void Collect(AgentContext ctx, TargetRegistry into)
        {
            foreach (var p in world.ProvinceList)
                into.Register(p.Id, new StrategyTarget(world, self, p.Id),
                    p.Owner == self ? CountryAgent.Ours : p.Owner == null ? CountryAgent.Neutral : CountryAgent.Foreign);
            foreach (var c in world.Alive.Where(c => c.Id != self))
            {
                var rel = world.RelationBetween(self, c.Id);
                into.Register(c.Id, new StrategyTarget(world, self, c.Id),
                    rel == Relation.War ? CountryAgent.AtWar : rel == Relation.Truce ? CountryAgent.Truce : CountryAgent.AtPeace);
            }
        }
    }

    /// <summary>The standing facts, read from the game at decision time (stage 1's split).</summary>
    internal sealed class StandingFactsSource : IObservationSource
    {
        private readonly World world;
        private readonly string self;
        private readonly bool movesLine;

        public StandingFactsSource(World world, string self, bool movesLine)
        {
            this.world = world;
            this.self = self;
            this.movesLine = movesLine;
        }

        public IEnumerable<string> Observe(AgentContext ctx)
        {
            foreach (string line in Facts.Standing(world, self)) yield return line;
            // An envoy call offers no targeted move, so the line would only mislead it.
            if (movesLine && !(ctx.Correlation is EnvoyOffer))
            {
                string moves = Facts.MovesLine(world, self);
                if (moves != null) yield return moves;
            }
        }
    }

    /// <summary>The correlation an envoy call carries: which offer it answers.</summary>
    public sealed class EnvoyOffer
    {
        public EnvoyOffer(string id, string from)
        {
            Id = id;
            From = from;
        }

        public string Id { get; }
        public string From { get; }
        public override string ToString() => Id;
    }

    /// <summary>
    /// One country played by a framework <see cref="Agent"/>, built in code with no scene (DR-011).
    /// Its actions are the game's moves, registered once; whether each is offered comes from
    /// <see cref="Legality.WhyNot"/>, action-wide through the handler's availability and per target
    /// through <see cref="DelegateActionHandler.IsAvailableFor"/> (<see cref="ITargetAvailability"/>).
    /// The handlers change nothing: the game lands every move, and asks this seat first, through
    /// <see cref="IMoveGate"/>, so <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> is
    /// the re-check at the moment of landing and memory keeps only what landed.
    /// </summary>
    public sealed class CountryAgent : IMoveGate
    {
        public const string Ours = "ours";
        public const string Neutral = "neutral";
        public const string Foreign = "foreign";
        public const string AtWar = "at_war";
        public const string AtPeace = "at_peace";
        public const string Truce = "truce";

        private readonly World world;
        // Keyed by the request object itself: the game hands the same one back when the move lands.
        // A decision from a round is executed through that round, so its entry says what came of it.
        private readonly Dictionary<CountryRequest, (DecisionResult result, ModelDecision record, DecisionRound round)> pending =
            new Dictionary<CountryRequest, (DecisionResult, ModelDecision, DecisionRound)>();

        /// <param name="persona">A character for the ruler (<see cref="Personas"/>), in place of the country's one-line personality; null keeps stage 2's identity exactly.</param>
        public CountryAgent(World world, string countryId, ILLMProvider provider, ModelSeatOptions options = null, Persona persona = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            CountryId = countryId;
            Options = options ?? ModelSeatOptions.Default;
            var info = world.Country(countryId).Info;

            var identity = new AgentIdentity
            {
                Name = info.Name,
                Role = $"the ruler of {info.Name} ({countryId}), one of five countries at war over a map of provinces",
                Personality = info.Personality,
                Goals = $"Hold {world.Rules.ProvincesToWin} provinces to win; if nobody has by turn {world.Rules.MaxTurns}, hold the most.",
            };
            Persona = persona;
            persona?.ApplyTo(identity);
            Agent = new Agent(identity, Array.Empty<ActionDefinition>(), provider)
            {
                Id = countryId,
                MaxObservations = 12,
                MemoryCommit = MemoryCommit.OnExecute,
                Memory = new RollingHistory(6),
            };
            Agent.TargetSources.Add(new StrategyTargetSource(world, countryId));
            Agent.ObservationSources.Add(new StandingFactsSource(world, countryId, Options.MovesLine));
            Register();
        }

        public string CountryId { get; }
        public Agent Agent { get; }
        public ModelSeatOptions Options { get; }
        /// <summary>The ruler's persona, or null.</summary>
        public Persona Persona { get; }
        /// <summary>Every decision this seat made, in order.</summary>
        public List<ModelDecision> Decisions { get; } = new List<ModelDecision>();
        /// <summary>The seed of the game it plays, for the records.</summary>
        public int Seed { get; set; }

        // --- Actions --------------------------------------------------------------------------

        private void Register()
        {
            Council(Actions.Recruit, "Raise soldiers with gold.", null);
            Council(Actions.Fortify, "Build a fort in one of our border provinces.", new[] { Ours });
            Council(Actions.RaiseTaxes, "Take gold from our people, at a cost in stability.", null);
            Council(Actions.DeclareWar, "Go to war with a neighbouring country.", new[] { AtPeace });
            Council(Actions.Attack, "Send our army to take a province next to ours.", new[] { Neutral, Foreign });
            Council(Actions.OfferPeace, "Ask a country we are at war with for a truce.", new[] { AtWar });
            Envoy(Actions.AcceptPeace, "Accept the offered truce.");
            Envoy(Actions.RefusePeace, "Refuse the offered truce and fight on.");
        }

        private void Council(string action, string description, string[] categories)
        {
            bool targeted = categories != null;
            var definition = new ActionDefinition
            {
                Id = action,
                Description = description,
                RequiresTarget = targeted,
                // recruit and raise_taxes take no target in the rules (Legality.WhyNot), so the
                // framework drops one the model names anyway and says so (#244 item 2).
                TakesNoTarget = !targeted,
                TargetCategories = categories,
            };
            var handler = DelegateActionHandler.WithResult(
                ctx => !(ctx.Correlation is EnvoyOffer) && (targeted ? AnyTarget(action) : Legality.IsLegal(world, CountryId, new Move(action))),
                (ctx, d) => ActionResult.Done());
            if (targeted)
                handler.IsAvailableFor = (ctx, target) =>
                {
                    string why = Legality.WhyNot(world, CountryId, new Move(action, target));
                    return why == null ? Availability.Yes : Availability.No(why);
                };
            Agent.Actions.Register(definition, handler);
        }

        private void Envoy(string action, string description)
        {
            Agent.Actions.Register(new ActionDefinition { Id = action, Description = description, TakesNoTarget = true },
                DelegateActionHandler.WithResult(ctx => ctx.Correlation is EnvoyOffer, (ctx, d) => ActionResult.Done()));
        }

        private bool AnyTarget(string action)
        {
            IEnumerable<string> ids = Actions.TargetsProvince(action)
                ? world.ProvinceList.Select(p => p.Id)
                : world.CountryList.Select(c => c.Id);
            return ids.Any(id => Legality.IsLegal(world, CountryId, new Move(action, id)));
        }

        // --- Deciding -------------------------------------------------------------------------

        /// <summary>The stimulus for a request, by the arm's choice.</summary>
        public string StimulusFor(CountryRequest request)
        {
            if (request.Moment == Moment.Envoy) return $"{request.OfferedBy} offers us peace.";
            if (Options.Stimulus == CouncilStimulus.Plain) return "Your move.";
            string head = request.TurnFacts.Count > 0 ? request.TurnFacts[0] : "Nothing has changed.";
            return $"Turn {request.Turn} of {world.Rules.MaxTurns}. {head}";
        }

        public DecideOptions OptionsFor(CountryRequest request)
        {
            var facts = request.Moment == Moment.Council && Options.Stimulus == CouncilStimulus.Headline
                ? request.TurnFacts.Skip(1).ToList() // the headline already says the first
                : request.TurnFacts.ToList();
            return new DecideOptions
            {
                StimulusLabel = request.Moment == Moment.Envoy ? "Envoy" : "Council",
                Observations = facts,
                ObservationPlacement = Options.Placement,
                ListActionsAndTargets = true,
                MaxListedTargets = 40,
                Priority = DecisionPriority.Background,
                CheckTarget = Options.CheckTarget,
                TargetGroundingThreshold = Options.GroundingThreshold,
                LegalTargets = Options.LegalTargets,
                PairTargetsWithActions = Options.PairTargets,
                Correlation = request.Moment == Moment.Envoy ? new EnvoyOffer(request.OfferId, request.OfferedBy) : null,
            };
        }

        /// <summary>
        /// Asks the agent for this request's move. A decision that fails, or that a guard turned
        /// into <c>none</c>, comes back as hold (or, for an envoy, a refusal); either way it is
        /// recorded in <see cref="Decisions"/>, and the answer's note says what happened.
        /// </summary>
        public async Awaitable<CountryAnswer> DecideAsync(CountryRequest request, CancellationToken ct = default)
        {
            var result = await Agent.DecideAsync(StimulusFor(request), OptionsFor(request), ct);
            return Take(request, result, null);
        }

        /// <summary>This request as one call of a <see cref="DecisionRound"/>: what <see cref="DecideAsync"/> would ask.</summary>
        public RoundCall CallFor(CountryRequest request) => new RoundCall(Agent, StimulusFor(request), OptionsFor(request));

        /// <summary>
        /// The answer to <paramref name="request"/> from a decided round this seat was in. When the
        /// game lands the move, <see cref="Admit"/> executes it through the round, so the round's entry
        /// records the outcome and whether it went stale. A call that threw is a decision with no
        /// result: the game holds, as for a pipeline error.
        /// </summary>
        public CountryAnswer Take(CountryRequest request, DecisionRound round)
        {
            if (round == null) throw new ArgumentNullException(nameof(round));
            return Take(request, round[Agent].Result, round);
        }

        private CountryAnswer Take(CountryRequest request, DecisionResult result, DecisionRound round)
        {
            var t = result?.Telemetry;
            var record = new ModelDecision
            {
                Seed = Seed,
                Turn = request.Turn,
                Country = CountryId,
                Persona = Persona?.Id,
                Round = t?.ChainId,
                Moment = request.Moment,
                ModelAction = result?.ModelDecision?.ActionId,
                ModelTarget = result?.ModelDecision?.TargetId,
                Action = result?.Decision?.ActionId,
                Target = result?.Decision?.TargetId,
                Statement = result?.Decision?.Statement ?? result?.ModelDecision?.Statement,
                LatencySeconds = t?.LatencySeconds ?? 0,
                PromptTokens = t?.PromptTokens ?? 0,
                Verdict = t?.Verdict,
                GuardReason = t?.GuardReason,
                Guard = t != null && t.GuardsFired.Count > 0 ? t.GuardsFired[0] : null,
                // A round entry whose call threw has no result at all: counted as the provider failing.
                Error = result == null ? PipelineErrorKind.ProviderFailed : t?.ErrorKind,
            };

            var move = ToMove(request, result?.Decision);
            // The framework drops a target on a move that takes none and records it (#244 item 2);
            // the seat's own drop in ToMove stays as a second check.
            record.TargetIgnored = t?.TargetIgnored != null || (result?.Decision != null && move.Target == null
                && result.Decision.TargetId != null && result.Decision.TargetId != TargetRegistry.NoTarget);
            record.Move = move;
            record.Offered = request.Legal.Contains(move);
            // An envoy's ToMove turns any answer into accept or refuse, so check its action itself.
            string asked = result?.ModelDecision?.ActionId;
            record.ModelAnswerLegal = asked != null && (request.Moment == Moment.Envoy
                ? asked == Actions.AcceptPeace || asked == Actions.RefusePeace || asked == ActionRegistry.None
                : request.Legal.Contains(ToMove(request, result.ModelDecision)));
            Decisions.Add(record);
            if (result?.Decision != null) pending[request] = (result, record, round);
            return new CountryAnswer(request, move, Note(record));
        }

        private static Move ToMove(CountryRequest request, AgentDecision d)
        {
            if (request.Moment == Moment.Envoy)
                return d != null && d.ActionId == Actions.AcceptPeace ? new Move(Actions.AcceptPeace) : new Move(Actions.RefusePeace);
            if (d == null || d.ActionId == ActionRegistry.None) return Move.Hold;
            // A move that takes no target ignores one the model named anyway ("recruit corwick").
            // The framework drops it first since #244 item 2 (gap N5); this stays as a second check.
            bool takesTarget = Actions.TargetsProvince(d.ActionId) || Actions.TargetsCountry(d.ActionId);
            string target = !takesTarget || d.TargetId == TargetRegistry.NoTarget ? null : d.TargetId;
            return new Move(d.ActionId, target);
        }

        private static string Note(ModelDecision r)
        {
            string answer = r.ModelAction == null ? "no decision" : r.ModelTarget == null || r.ModelTarget == TargetRegistry.NoTarget
                ? r.ModelAction : $"{r.ModelAction} {r.ModelTarget}";
            string line = $"model: `{answer}`, {r.LatencySeconds:0.00} s";
            if (r.Error != null) line += $", pipeline error {r.Error}";
            if (r.Verdict != null) line += $", {r.Guard}: {r.Verdict} ({r.GuardReason})";
            if (!string.IsNullOrWhiteSpace(r.Statement)) line += $"\n\"{r.Statement.Trim()}\"";
            return line;
        }

        // --- Landing --------------------------------------------------------------------------

        /// <summary>
        /// The game is about to land this seat's move: carry the decision out through the agent,
        /// whose re-check either admits it or refuses it with the framework's reason.
        /// </summary>
        public string Admit(CountryRequest request, Move move)
        {
            if (!pending.TryGetValue(request, out var entry)) return null; // no decision: the game holds
            pending.Remove(request);
            ActionResult outcome;
            if (entry.round != null)
            {
                // Through the round: the same Agent.Execute and re-check, and its entry keeps the outcome and Stale.
                var executed = entry.round.Execute(Agent)[0];
                outcome = executed.Outcome ?? ActionResult.Failed(executed.Error?.Message ?? "the round did not execute it");
                entry.record.Stale = executed.Stale;
            }
            else Agent.Execute(entry.result, out outcome);
            entry.record.Execution = outcome.Status;
            entry.record.ExecutionReason = outcome.Reason;
            return outcome.Succeeded ? null : outcome.Reason ?? "the agent refused it";
        }

        /// <summary>
        /// Drops what never landed: a decision the game refused before asking, which memory must
        /// not keep as made (MemoryCommit.OnExecute).
        /// </summary>
        public void EndTurn()
        {
            // A round's own End discards its waiting decisions; one decided alone is discarded here.
            foreach (var e in pending.Values)
                if (e.round == null) Agent.Discard(e.result);
            pending.Clear();
        }
    }
}
