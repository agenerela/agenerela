using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;

namespace GreyBoxStrategy
{
    /// <summary>One order the advisor proposes: the request it answered, the legal move it maps to, and its record.</summary>
    public sealed class AdviceItem
    {
        public CountryRequest Request;
        /// <summary>Always a move the request offered: what the model said, or a hold where that was not offered.</summary>
        public Move Move;
        /// <summary>False when the model named a move the rules did not offer, and the advice holds instead.</summary>
        public bool AsAsked;
        public ModelDecision Record;
        public string Stimulus;
        /// <summary>The advisor agent that decided it, as the decision log names it.</summary>
        public string AgentId;
    }

    /// <summary>A turn's advice: a court order, an order for each army a writ reaches, and the advisor's counsel.</summary>
    public sealed class Advice
    {
        public int Turn;
        public Move Court = Move.Hold;
        public readonly Dictionary<string, Move> Armies = new Dictionary<string, Move>(StringComparer.Ordinal);
        /// <summary>What the advisor says, in a sentence: the court decision's statement.</summary>
        public string Counsel;
        public readonly List<AdviceItem> Items = new List<AdviceItem>();
    }

    /// <summary>
    /// The advisor in rule mode (the lead's answer 29): a model sitting on the person's own seat, which proposes the
    /// turn's plan and never plays it. It is the same court and marshal a model ruler has, a <see cref="WritRuler"/>
    /// used as it is, asked with the person's own requests and facts. What it says is only a suggestion: every
    /// order maps to a move the rules offered, nothing it decides is carried out, and its memory keeps nothing.
    /// </summary>
    public sealed class Advisor
    {
        /// <summary>Appended to the country's id for the advisor's court; its marshal adds <c>_marshal</c>, as a ruler's does.</summary>
        public const string AgentSuffix = "_advisor";

        private readonly World world;
        private readonly WritRuler ruler;

        public Advisor(World world, string countryId, ILLMProvider provider, ModelSeatOptions options = null, Persona persona = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            CountryId = countryId;
            ruler = new WritRuler(world, countryId, provider, options, persona);
            // Its own names in the decision log and the feed, never the person's or a ruler's.
            ruler.Court.Id = countryId + AgentSuffix;
            ruler.Marshal.Id = countryId + AgentSuffix + "_marshal";
        }

        public string CountryId { get; }
        /// <summary>How many decisions it has asked for, over the game.</summary>
        public int Calls { get; private set; }
        public string CourtAgentId => ruler.Court.Id;
        public string MarshalAgentId => ruler.Marshal.Id;

        /// <summary>
        /// The advice for the turn <paramref name="court"/> opens: the court's order, then one order for each army
        /// awaiting orders that a writ reaches (all but one writ, as the rules ask them), each told the orders
        /// before it, as a ruler's are.
        /// </summary>
        public async Awaitable<Advice> AdviseAsync(CountryRequest court, CancellationToken ct = default)
        {
            if (court == null || court.Moment != Moment.Council) throw new ArgumentException("The advisor answers the court's request of a turn.", nameof(court));
            var advice = new Advice { Turn = court.Turn };
            var ordered = new List<string>(court.Ordered ?? Array.Empty<string>());
            try
            {
                var c = await Ask(court, ct);
                advice.Items.Add(c);
                advice.Court = c.Move;
                ordered.Add(c.Move.ToString());

                int slots = court.Writs >= GameRules.UnlimitedWrits ? int.MaxValue : Math.Max(0, court.Writs - 1);
                foreach (var army in WritLegality.Awaiting(world, CountryId).Take(slots))
                {
                    ct.ThrowIfCancellationRequested();
                    var facts = new List<string> { $"{army.Id}: {army.Soldiers} soldiers at {army.Location}; " + (army.Wake.Count > 0 ? "awaiting orders: " + string.Join(", ", army.Wake) : "") + "." };
                    facts.AddRange(court.TurnFacts.Take(Facts.MaxTurnFacts - 1));
                    var r = new CountryRequest(Moment.Army, court.Turn, CountryId, world, court.StandingFacts, facts, WritLegality.ArmyMoves(world, CountryId, army.Id))
                    {
                        ArmyId = army.Id,
                        Ordered = ordered.ToList(),
                        Writ = court.Writ + advice.Items.Count,
                        Writs = court.Writs,
                    };
                    var a = await Ask(r, ct);
                    advice.Items.Add(a);
                    advice.Armies[army.Id] = a.Move;
                    ordered.Add(a.Move.Action == Actions.March ? $"{army.Id} to march to {a.Move.Target}" : $"{army.Id} to hold");
                }
            }
            finally
            {
                // The counsel: the first line any of the turn's decisions kept (a refused answer keeps none).
                advice.Counsel = advice.Items.Select(i => i.Record?.Statement?.Trim()).FirstOrDefault(x => !string.IsNullOrEmpty(x));
                // Nothing the advisor decided is carried out: its memory drops the turn's decisions.
                ruler.EndTurn();
            }
            return advice;
        }

        private async Awaitable<AdviceItem> Ask(CountryRequest r, CancellationToken ct)
        {
            string stimulus = ruler.StimulusFor(r);
            int before = ruler.Decisions.Count;
            Calls++;
            var answer = await ruler.DecideAsync(r, ct);
            var record = ruler.Decisions.Count > before ? ruler.Decisions[ruler.Decisions.Count - 1] : null;
            var move = answer?.Choice ?? Move.Hold;
            bool offered = move.Action == Actions.None || r.Legal.Contains(move);
            return new AdviceItem
            {
                Request = r,
                Move = offered ? move : Move.Hold,
                AsAsked = offered,
                Record = record,
                Stimulus = stimulus,
                AgentId = r.Moment == Moment.Army ? ruler.Marshal.Id : ruler.Court.Id,
            };
        }
    }
}
