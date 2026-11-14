using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;

namespace GreyBoxStrategy
{
    /// <summary>The correlation of an army order: which army the marshal is ordering.</summary>
    public sealed class ArmyOrder
    {
        public ArmyOrder(string armyId)
        {
            ArmyId = armyId;
        }

        public string ArmyId { get; }
        public override string ToString() => ArmyId;
    }

    /// <summary>The correlation of a treaty answer: the proposal, who made it and its term.</summary>
    public sealed class TreatyOffer
    {
        public TreatyOffer(string id, string from)
        {
            Id = id;
            From = from;
        }

        public string Id { get; }
        public string From { get; }
        public override string ToString() => Id;
    }

    /// <summary>
    /// A model ruler for the writs turn (rules v2, R1b; section 1.3): two framework agents with one identity
    /// and persona. The <b>court</b> gives court orders, answers treaties and the storyteller's dilemmas;
    /// the <b>marshal</b> orders armies, the army named in the decision's correlation. Each keeps its own
    /// memory of four decisions, so army orders do not push the court's out. The one-move game's
    /// <see cref="CountryAgent"/> is unchanged.
    /// </summary>
    /// <remarks>
    /// Every decision is one action and one target from a closed list: court orders with availability
    /// from <see cref="WritLegality.WhyNotCourt"/>, a march with the army's own targets from
    /// <see cref="WritLegality.WhyNotArmy"/>. What a local model must read is in the prompt (it never
    /// reads the schema): the local standing facts (<see cref="WritFacts"/>), the moves line written from
    /// the request's offered moves, and this turn's earlier orders.
    /// </remarks>
    public sealed class WritRuler : IMoveGate
    {
        public const int MemoryDecisions = 4;

        private readonly World world;
        private readonly Dictionary<CountryRequest, (Agent agent, DecisionResult result, ModelDecision record)> pending =
            new Dictionary<CountryRequest, (Agent, DecisionResult, ModelDecision)>();
        private readonly Dictionary<CountryRequest, (Agent agent, DecisionResult result)> landed =
            new Dictionary<CountryRequest, (Agent, DecisionResult)>();

        public WritRuler(World world, string countryId, ILLMProvider provider, ModelSeatOptions options = null, Persona persona = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (world.Rules.Turn != TurnStyle.Writs) throw new ArgumentException("A WritRuler plays the writs turn; the one-move game has CountryAgent.");
            CountryId = countryId;
            Options = options ?? ModelSeatOptions.Default;
            Persona = persona;
            Court = NewAgent(provider, countryId, "the court");
            Marshal = NewAgent(provider, countryId + "_marshal", "the marshal of the armies");
            Court.TargetSources.Add(new StrategyTargetSource(world, countryId));
            Marshal.TargetSources.Add(new ProvinceTargets(world, countryId));
            Court.ObservationSources.Add(new WritFactsSource(this));
            Marshal.ObservationSources.Add(new WritFactsSource(this));
            RegisterCourt();
            RegisterMarshal();
        }

        public string CountryId { get; }
        public ModelSeatOptions Options { get; }
        public Persona Persona { get; }
        public Agent Court { get; }
        public Agent Marshal { get; }
        /// <summary>Every decision either agent made, in order.</summary>
        public List<ModelDecision> Decisions { get; } = new List<ModelDecision>();
        public int Seed { get; set; }
        /// <summary>The request being decided now: what the facts source and a test's fake model read.</summary>
        public CountryRequest Asking { get; private set; }

        private Agent NewAgent(ILLMProvider provider, string id, string office)
        {
            var info = world.Country(CountryId).Info;
            var identity = new AgentIdentity
            {
                Name = info.Name,
                Role = $"{office} of {info.Name} ({CountryId}), one of {CountryAgent.CountWord(world.CountryList.Count)} countries at war over a map of provinces",
                Personality = info.Personality,
                Goals = CountryAgent.GoalsFor(world.Rules),
            };
            Persona?.ApplyTo(identity);
            return new Agent(identity, Array.Empty<ActionDefinition>(), provider)
            {
                Id = id,
                // The call's own lines count first and the sources fill what is left, so the moves line and the
                // ordered line, the sources' last, are the ones cut. The own-lines line gets a place of its own:
                // without it 43% of later orders lost "Ordered this turn" in a scripted probe (14 November).
                MaxObservations = Options.OwnLines ? 13 : 12,
                MemoryCommit = MemoryCommit.OnExecute,
                Memory = new RollingHistory(MemoryDecisions) { Remembers = Options.Remembers },
            };
        }

        // --- Actions --------------------------------------------------------------------------

        private void RegisterCourt()
        {
            CourtOrder(Actions.Recruit, "Raise soldiers with gold, at our capital.", null);
            CourtOrder(Actions.Fortify, "Build a fort in one of our border provinces.", new[] { CountryAgent.Ours });
            CourtOrder(Actions.RaiseTaxes, "Take gold from our people, at a cost in stability.", null);
            CourtOrder(Actions.DeclareWar, "Go to war with a country our land borders.", new[] { CountryAgent.AtPeace });
            CourtOrder(Actions.OfferWhitePeace, "Offer an enemy peace: all occupied land returns.", new[] { CountryAgent.AtWar });
            CourtOrder(Actions.DemandOccupied, "Demand from an enemy the provinces of theirs we occupy, for peace.", new[] { CountryAgent.AtWar });
            CourtOrder(Actions.CedeOccupied, "Give an enemy the provinces of ours it occupies, for peace.", new[] { CountryAgent.AtWar });
            foreach (var (action, description) in new[] { (Actions.AcceptPeace, "Accept the proposed treaty."), (Actions.RefusePeace, "Refuse the proposed treaty and fight on.") })
                Court.Actions.Register(new ActionDefinition { Id = action, Description = description, TakesNoTarget = true },
                    DelegateActionHandler.WithResult(ctx => ctx.Correlation is TreatyOffer, (ctx, d) => ActionResult.Done()));
            if (Options.Choices)
                foreach (var t in EventRules.S2Templates.Where(t => t.IsChoice))
                    foreach (var o in t.Options)
                        Court.Actions.Register(new ActionDefinition { Id = o.Id, Description = $"Choose to {o.Label}.", TakesNoTarget = true },
                            DelegateActionHandler.WithResult(ctx => ctx.Correlation is ChoiceCall c && c.Options.Contains(o.Id), (ctx, d) => ActionResult.Done()));
        }

        private void CourtOrder(string action, string description, string[] categories)
        {
            bool targeted = categories != null;
            var handler = DelegateActionHandler.WithResult(
                ctx => ctx.Correlation == null && (targeted
                    ? (Actions.TargetsProvince(action) ? world.ProvinceList.Select(p => p.Id) : world.CountryList.Select(c => c.Id))
                        .Any(id => WritLegality.WhyNotCourt(world, CountryId, new Move(action, id)) == null)
                    : WritLegality.WhyNotCourt(world, CountryId, new Move(action)) == null),
                (ctx, d) => ActionResult.Done());
            if (targeted)
                handler.IsAvailableFor = (ctx, target) =>
                {
                    string why = WritLegality.WhyNotCourt(world, CountryId, new Move(action, target)) ?? NotOffered(new Move(action, target));
                    return why == null ? Availability.Yes : Availability.No(why);
                };
            Court.Actions.Register(new ActionDefinition
            {
                Id = action,
                Description = description,
                RequiresTarget = targeted,
                TakesNoTarget = !targeted,
                TargetCategories = categories,
            }, handler);
        }

        // The rules allow more targets than a decision offers: fortify lists at most six provinces and a march
        // at most twelve. A target the rules allow but the list left out is not offered either, or the game
        // holds the order as "not offered" (R1c: 56 of 3,408 army orders before this check).
        private string NotOffered(Move move) =>
            Asking != null && !Asking.Legal.Contains(move) ? "not among the moves offered in this decision" : null;

        private void RegisterMarshal()
        {
            var handler = DelegateActionHandler.WithResult(
                ctx => ctx.Correlation is ArmyOrder order && WritLegality.ArmyMoves(world, CountryId, order.ArmyId).Any(m => m.Action == Actions.March),
                (ctx, d) => ActionResult.Done());
            handler.IsAvailableFor = (ctx, target) =>
            {
                if (!(ctx.Correlation is ArmyOrder order)) return Availability.No("no army is being ordered");
                string why = WritLegality.WhyNotArmy(world, CountryId, order.ArmyId, new Move(Actions.March, target)) ?? NotOffered(new Move(Actions.March, target));
                return why == null ? Availability.Yes : Availability.No(why);
            };
            Marshal.Actions.Register(new ActionDefinition
            {
                Id = Actions.March,
                Description = "March the army to a province: the shortest path, a step or two a turn.",
                RequiresTarget = true,
                TargetCategories = new[] { CountryAgent.Ours, CountryAgent.Neutral, CountryAgent.Foreign },
            }, handler);
        }

        // --- Deciding -------------------------------------------------------------------------

        private Agent AgentFor(CountryRequest r) => r.Moment == Moment.Army ? Marshal : Court;

        public string StimulusFor(CountryRequest r)
        {
            switch (r.Moment)
            {
                case Moment.Army: return $"Orders for {r.ArmyId}.";
                case Moment.Envoy: return $"{r.OfferedBy} proposes a treaty.";
                case Moment.Choice: return "An event asks for our decision.";
                default:
                    string head = r.TurnFacts.Count > 0 ? r.TurnFacts[0] : "Nothing has changed.";
                    return $"Turn {r.Turn}. {head}";
            }
        }

        public DecideOptions OptionsFor(CountryRequest r)
        {
            // The court's headline already says its first fact; an army's first fact is its own line.
            var facts = r.Moment == Moment.Council ? r.TurnFacts.Skip(1).ToList() : r.TurnFacts.ToList();
            bool speaks = Speaks(r);
            bool asked = AsksForStatement(r);
            if (!speaks && asked) facts.Add(SayNothingLine);
            else if (asked && Options.OwnLines && OwnLinesLine() is string own) facts.Add(own);
            return new DecideOptions
            {
                StimulusLabel = r.Moment == Moment.Army ? "Army" : r.Moment == Moment.Envoy ? "Treaty" : r.Moment == Moment.Choice ? "Choice" : "Court",
                Observations = facts,
                ObservationPlacement = Options.Placement,
                ListActionsAndTargets = true,
                MaxListedTargets = 40,
                Priority = DecisionPriority.Background,
                CheckTarget = Options.CheckTarget,
                TargetGroundingThreshold = Options.GroundingThreshold,
                LegalTargets = Options.LegalTargets,
                StatementRulesWording = Options.RulesWording,
                PairTargetsWithActions = Options.PairTargets,
                ExampleStatements = speaks && asked,
                AskForStatement = asked,
                Correlation = r.Moment == Moment.Army ? new ArmyOrder(r.ArmyId)
                    : r.Moment == Moment.Envoy ? new TreatyOffer(r.OfferId, r.OfferedBy)
                    : r.Moment == Moment.Choice ? new ChoiceCall(r.OfferId, r.Legal.Select(m => m.Action).ToList())
                    : (object)null,
            };
        }

        /// <summary>The line a decision that should say nothing reads (<see cref="ModelSeatOptions.OneVoice"/>).</summary>
        public const string SayNothingLine = "Say nothing this time: leave the statement empty.";

        /// <summary>Whether this decision carries a statement: always, unless <see cref="ModelSeatOptions.OneVoice"/>
        /// leaves the turn's voice to the ruler's first court decision.</summary>
        public bool Speaks(CountryRequest r) => !Options.OneVoice || (r.Moment == Moment.Council && r.Ordered.Count == 0);

        /// <summary>Whether the call asks for a statement at all: not for an army order under
        /// <see cref="ModelSeatOptions.SilentArmies"/>, which leaves the field out of the request (#266).</summary>
        public bool AsksForStatement(CountryRequest r) => !(Options.SilentArmies && r.Moment == Moment.Army);

        /// <summary>
        /// <see cref="ModelSeatOptions.OwnLines"/>: the ruler's last few distinct lines, newest last, with a plain
        /// ask for something new; null before the ruler has said anything.
        /// </summary>
        public string OwnLinesLine()
        {
            var lines = new List<string>();
            for (int i = Decisions.Count - 1; i >= 0 && lines.Count < ModelSeatOptions.OwnLinesShown; i--)
            {
                string s = Decisions[i].Statement?.Trim();
                if (!string.IsNullOrEmpty(s) && !lines.Contains(s)) lines.Add(s);
            }
            if (lines.Count == 0) return null;
            lines.Reverse();
            return "Lines we said lately: " + string.Join(" ", lines.Select(l => "\"" + l + "\"")) + " Say something new, not one of these.";
        }

        /// <summary>Asks the right agent for this request's move; a failed decision holds (or refuses a treaty).</summary>
        public async Awaitable<CountryAnswer> DecideAsync(CountryRequest r, CancellationToken ct = default)
        {
            Asking = r;
            var agent = AgentFor(r);
            var result = await agent.DecideAsync(StimulusFor(r), OptionsFor(r), ct);
            Asking = null;
            return Take(r, agent, result);
        }

        private CountryAnswer Take(CountryRequest r, Agent agent, DecisionResult result)
        {
            var t = result?.Telemetry;
            var record = new ModelDecision
            {
                Seed = Seed,
                Turn = r.Turn,
                Country = CountryId,
                Persona = Persona?.Id,
                Round = r.ArmyId,
                Moment = r.Moment,
                OfferedBy = r.OfferedBy,
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
                Error = result == null ? PipelineErrorKind.ProviderFailed : t?.ErrorKind,
            };
            if (!Speaks(r) && !string.IsNullOrWhiteSpace(record.Statement))
            {
                record.Unspoken = record.Statement;
                record.Statement = "";
            }
            var move = ToMove(r, result?.Decision);
            record.Move = move;
            record.Offered = r.Legal.Contains(move);
            string asked = result?.ModelDecision?.ActionId;
            record.ModelAnswerLegal = asked != null && (asked == ActionRegistry.None || r.Legal.Contains(ToMove(r, result.ModelDecision)));
            Decisions.Add(record);
            if (result?.Decision != null) pending[r] = (agent, result, record);
            return new CountryAnswer(r, move, $"model: `{record.ModelAction} {record.ModelTarget}`, {record.LatencySeconds:0.00} s");
        }

        private static Move ToMove(CountryRequest r, AgentDecision d)
        {
            if (r.Moment == Moment.Envoy)
                return d != null && d.ActionId == Actions.AcceptPeace ? new Move(Actions.AcceptPeace) : new Move(Actions.RefusePeace);
            if (r.Moment == Moment.Choice)
                return d != null && r.Legal.Any(m => m.Action == d.ActionId) ? new Move(d.ActionId) : Move.Hold;
            if (d == null || d.ActionId == ActionRegistry.None) return Move.Hold;
            bool takesTarget = Actions.TargetsProvince(d.ActionId) || Actions.TargetsCountry(d.ActionId);
            return new Move(d.ActionId, !takesTarget || d.TargetId == TargetRegistry.NoTarget ? null : d.TargetId);
        }

        // --- Landing --------------------------------------------------------------------------

        /// <summary>The game is about to land this move: carry it out through the agent that decided it, whose re-check may refuse it.</summary>
        public string Admit(CountryRequest request, Move move)
        {
            if (!pending.TryGetValue(request, out var entry)) return null;
            pending.Remove(request);
            entry.agent.Execute(entry.result, out var outcome);
            entry.record.Execution = outcome.Status;
            entry.record.ExecutionReason = outcome.Reason;
            if (outcome.Succeeded) landed[request] = (entry.agent, entry.result);
            return outcome.Succeeded ? null : outcome.Reason ?? "the agent refused it";
        }

        /// <summary>Attaches what came of each landed order to the memory that made it. Call after <see cref="Game.Resolve"/>.</summary>
        public void Learn(TurnReport report)
        {
            foreach (var o in report.Outcomes)
                if (o.Request != null && !o.Refused && landed.TryGetValue(o.Request, out var e))
                    e.agent.SetOutcome(e.result, o.Text);
            landed.Clear();
        }

        /// <summary>Drops decisions the game never landed, so memory does not keep them as made.</summary>
        public void EndTurn()
        {
            foreach (var e in pending.Values) e.agent.Discard(e.result);
            pending.Clear();
            landed.Clear();
        }

        // --- What the agents read ------------------------------------------------------------

        private sealed class WritFactsSource : IObservationSource
        {
            private readonly WritRuler ruler;
            public WritFactsSource(WritRuler ruler) { this.ruler = ruler; }

            public IEnumerable<string> Observe(AgentContext ctx)
            {
                var r = ruler.Asking;
                foreach (string line in WritFacts.Standing(ruler.world, ruler.CountryId)) yield return line;
                if (r == null || !ruler.Options.MovesLine) yield break;
                if (r.Moment == Moment.Council && WritFacts.CourtMovesLine(r.Legal) is string court) yield return court;
                if (r.Moment == Moment.Army) yield return WritFacts.ArmyMovesLine(r.ArmyId, r.Legal);
                if ((r.Moment == Moment.Council || r.Moment == Moment.Army) && WritFacts.OrderedLine(r.Ordered) is string ordered) yield return ordered;
            }
        }

        /// <summary>The marshal's targets: provinces, as ours, neutral or foreign.</summary>
        private sealed class ProvinceTargets : ITargetSource
        {
            private readonly World world;
            private readonly string self;
            public ProvinceTargets(World world, string self) { this.world = world; this.self = self; }

            public void Collect(AgentContext ctx, TargetRegistry into)
            {
                foreach (var p in world.ProvinceList)
                    into.Register(p.Id, new StrategyTarget(world, self, p.Id),
                        p.Controller == self ? CountryAgent.Ours : p.Owner == null ? CountryAgent.Neutral : CountryAgent.Foreign);
            }
        }
    }
}
