using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using Newtonsoft.Json.Linq;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The writs turn's decisions as the game asked them (rules v2, R1): a model ruler's, with what
    /// <c>Agent.Execute</c> made of it and its decision log record, or a person's. Shared by the live
    /// watch game and the rule game.
    /// </summary>
    /// <remarks>
    /// <see cref="WritRuler"/>'s agents decide outside any <c>DecisionRound</c>, so their log lines carry no
    /// chain id to read a turn from. Each decision here is paired with its agent's log record in order,
    /// checked by the stimulus the ruler sent; a pair that does not match keeps no prompt rather than another's.
    /// </remarks>
    public sealed class WritsRecorder
    {
        private sealed class Asked
        {
            public CountryRequest Request;
            public string AgentId;
            public string Stimulus;
            public Move Choice;
            public ModelDecision Record;
            public string Model;
            public long Sequence;
            public List<string> Offered;
        }

        private readonly List<Asked> asked = new List<Asked>();
        private readonly object gate = new object();
        private long sequence;

        public int Seed { get; }
        public WritsRecorder(int seed) { Seed = seed; }

        /// <summary>A model ruler's decision, just made: <paramref name="record"/> is its <see cref="ModelDecision"/>, which <c>Admit</c> later completes.</summary>
        public void Model(CountryRequest r, CountryAnswer answer, string stimulus, ModelDecision record, string model)
        {
            lock (gate)
                asked.Add(new Asked
                {
                    Request = r, Choice = answer.Choice, Stimulus = stimulus, Record = record, Model = model, Sequence = sequence++,
                    AgentId = r.Moment == Moment.Army ? r.CountryId + RecordedDecision.MarshalSuffix : r.CountryId,
                    Offered = r.Legal.Select(m => m.ToString()).ToList(),
                });
        }

        /// <summary>A person's order.</summary>
        public void Person(CountryRequest r, Move move, string model)
        {
            lock (gate)
                asked.Add(new Asked
                {
                    Request = r, Choice = move, Model = model, Sequence = sequence++, AgentId = r.CountryId,
                    Offered = r.Legal.Select(m => m.ToString()).ToList(),
                });
        }

        /// <summary>Every decision so far, with its log record where it pairs.</summary>
        public List<RecordedDecision> Decisions(IEnumerable<string> logLines)
        {
            List<Asked> snapshot;
            lock (gate) snapshot = asked.ToList();
            var records = LogRecords(logLines);
            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            var list = new List<RecordedDecision>();
            foreach (var a in snapshot)
            {
                JObject raw = null;
                if (a.Stimulus != null)
                {
                    // The k-th decision of an agent is the k-th record of that agent in the log, if its stimulus says so.
                    used.TryGetValue(a.AgentId, out int k);
                    if (records.TryGetValue(a.AgentId, out var recs) && k < recs.Count)
                    {
                        if ((string)recs[k]["stimulus"] == a.Stimulus) raw = recs[k];
                        used[a.AgentId] = k + 1;
                    }
                }
                list.Add(Decision(a, raw));
            }
            return list;
        }

        private RecordedDecision Decision(Asked a, JObject raw)
        {
            var r = a.Request;
            string moment = r.Moment == Moment.Envoy ? "envoys" : r.Moment == Moment.Army ? RecordedDecision.ArmyMoment
                : r.Moment == Moment.Choice ? RecordedDecision.ChoiceMoment : "council";
            RecordedDecision d;
            if (raw != null)
            {
                var o = (JObject)raw.DeepClone();
                o["turn"] = r.Turn;
                o["moment"] = moment;
                d = Recording.FromLogRecord(o);
            }
            else d = new RecordedDecision
            {
                Turn = r.Turn, Moment = moment, Stimulus = a.Stimulus, Statement = "",
                Observations = string.Join("\n", r.StandingFacts), StimulusTurn = string.Join("\n", r.TurnFacts),
            };
            // What the game asked and took is the truth; the log record adds how the model got there.
            d.AgentId = a.AgentId;
            d.Turn = r.Turn;
            d.Moment = moment;
            d.ArmyId = r.ArmyId;
            d.Sequence = a.Sequence;
            d.ChainId = d.ChainId ?? $"seed-{Seed}-turn-{r.Turn}-{moment}{(r.ArmyId != null ? "-" + r.ArmyId : "")}";
            d.Action = a.Choice?.Action ?? Actions.None;
            d.Target = a.Choice?.Target ?? RecordedDecision.NoTarget;
            d.ModelName = a.Model;
            var m = a.Record;
            if (m != null)
            {
                d.Statement = m.Statement ?? d.Statement ?? "";
                d.ModelAction = m.ModelAction ?? d.ModelAction;
                d.ModelTarget = m.ModelTarget ?? d.ModelTarget;
                d.LatencySeconds = d.LatencySeconds > 0 ? d.LatencySeconds : m.LatencySeconds;
                // How it ended when it landed: refused or failed at Execute, as the replay's gate must refuse it too.
                if (m.Execution.HasValue) { d.ExecutionStatus = m.Execution.Value.ToString(); d.ExecutionReason = m.ExecutionReason; }
            }
            else if (a.Stimulus == null) { d.ModelAction = d.Action; d.ModelTarget = d.Target; }
            d.OfferedMoves = a.Offered;
            d.OfferedActions = r.Legal.Select(x => x.Action).Distinct().ToList();
            return d;
        }

        // The log's "Decided" records by agent, in the order written.
        private static Dictionary<string, List<JObject>> LogRecords(IEnumerable<string> lines)
        {
            var byAgent = new Dictionary<string, List<JObject>>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                JObject o;
                try { o = JObject.Parse(line); } catch (Newtonsoft.Json.JsonReaderException) { continue; } // a line still being written
                if ((string)o["stage"] != "Decided") continue;
                string agent = (string)o["agentId"];
                if (agent == null) continue;
                if (!byAgent.TryGetValue(agent, out var list)) byAgent[agent] = list = new List<JObject>();
                list.Add(o);
            }
            return byAgent;
        }
    }
}
