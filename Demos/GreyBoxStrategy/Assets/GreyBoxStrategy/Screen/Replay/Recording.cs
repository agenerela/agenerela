using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>A guard that stopped a decision, as the decision feed recorded it.</summary>
    public sealed class RecordedVerdict
    {
        public string Guard;
        public string Kind;
        public string Reason;
    }

    /// <summary>One action and target a per-target check left out, with its reason.</summary>
    public sealed class RecordedUnavailable
    {
        public string Action;
        public string Target;
        public string Reason;
    }

    /// <summary>
    /// One framework decision from a table run's <c>decisions.jsonl</c>: what the game received,
    /// what became of it in <c>Agent.Execute</c>, and the telemetry the decision feed recorded.
    /// </summary>
    public sealed class RecordedDecision
    {
        public string AgentId;
        public int Turn;
        /// <summary>"council" or "envoys", from the chain id.</summary>
        public string Moment;
        public string ChainId;
        public int ChainHop;
        public long Sequence;
        public string Action;
        /// <summary>The target id, or "no_target".</summary>
        public string Target;
        public string Statement;
        /// <summary>What the model wrote before any guard, when a guard changed it; else null.</summary>
        public string ModelAction;
        public string ModelTarget;
        /// <summary>"Done", "Failed", "NoAction" or "Refused"; null when the move never reached Execute.</summary>
        public string ExecutionStatus;
        public string ExecutionReason;
        public string ModelName;
        public string Stimulus;
        public float LatencySeconds;
        public float QueueWaitSeconds;
        public int QueuedAhead;
        public int PromptTokens;
        public int CompletionTokens;
        public float? TargetGrounding;
        public string TargetGroundingStatus;
        public int HistoryTurnsSent;
        public int HistoryTurnsDropped;
        public int ObservationsDropped;
        public List<string> GuardsFired = new List<string>();
        public RecordedVerdict Verdict;
        public List<RecordedUnavailable> TargetsUnavailable = new List<RecordedUnavailable>();
        /// <summary>The facts sent, word for word, as one block.</summary>
        public string Observations;
        public string StimulusTurn;
        public List<string> OfferedActions = new List<string>();
        /// <summary>The whole record, for anything the screen shows later.</summary>
        public JObject Raw;

        public bool IsEnvoy => Moment == "envoys";
        public bool HasTarget => !string.IsNullOrEmpty(Target) && Target != NoTarget;
        public const string NoTarget = "no_target";
    }

    /// <summary>Who played a country in the recorded game.</summary>
    public sealed class RecordedPlayer
    {
        public string Model;
        public string Persona;
    }

    /// <summary>
    /// One game from a table run, as <c>ArtSource/export_recording.py</c> writes it: the seed, who
    /// played each country, and every decision in the order the game asked for them.
    /// </summary>
    public sealed class Recording
    {
        public int Seed;
        public string Title;
        public string Source;
        public Dictionary<string, RecordedPlayer> Players = new Dictionary<string, RecordedPlayer>(StringComparer.Ordinal);
        public List<RecordedDecision> Decisions = new List<RecordedDecision>();

        public static Recording Parse(string json)
        {
            var root = JObject.Parse(json);
            var rec = new Recording
            {
                Seed = (int)root["seed"],
                Title = (string)root["title"],
                Source = (string)root["source"],
            };
            if (root["players"] is JObject players)
                foreach (var p in players.Properties())
                    rec.Players[p.Name] = new RecordedPlayer { Model = (string)p.Value["model"], Persona = (string)p.Value["persona"] };
            foreach (var d in (JArray)root["decisions"])
                rec.Decisions.Add(ParseDecision((JObject)d));
            return rec;
        }

        private static RecordedDecision ParseDecision(JObject o)
        {
            var decision = o["decision"] as JObject;
            var model = o["modelDecision"] as JObject;
            var exec = o["execution"] as JObject;
            var req = o["request"] as JObject;
            var d = new RecordedDecision
            {
                AgentId = (string)o["agentId"],
                Turn = (int)o["turn"],
                Moment = (string)o["moment"],
                ChainId = (string)o["chainId"],
                ChainHop = (int?)o["chainHop"] ?? 0,
                Sequence = (long?)o["sequence"] ?? 0,
                Action = (string)decision?["action"],
                Target = (string)decision?["target"],
                Statement = (string)decision?["statement"] ?? "",
                ModelAction = (string)model?["action"],
                ModelTarget = (string)model?["target"],
                ExecutionStatus = (string)exec?["status"],
                ExecutionReason = (string)exec?["reason"],
                ModelName = (string)o["modelName"],
                Stimulus = (string)o["stimulus"],
                LatencySeconds = (float?)o["latencySeconds"] ?? 0,
                QueueWaitSeconds = (float?)o["queueWaitSeconds"] ?? 0,
                QueuedAhead = (int?)o["queuedAhead"] ?? 0,
                PromptTokens = (int?)o["promptTokens"] ?? 0,
                CompletionTokens = (int?)o["completionTokens"] ?? 0,
                TargetGrounding = (float?)o["targetGrounding"],
                TargetGroundingStatus = (string)o["targetGroundingStatus"],
                HistoryTurnsSent = (int?)o["historyTurnsSent"] ?? 0,
                HistoryTurnsDropped = (int?)o["historyTurnsDropped"] ?? 0,
                ObservationsDropped = (int?)o["observationsDropped"] ?? 0,
                Observations = (string)req?["observations"],
                StimulusTurn = (string)req?["stimulusTurn"],
                Raw = o,
            };
            if (o["guardsFired"] is JArray g) d.GuardsFired = g.Select(x => (string)x).ToList();
            if (o["verdict"] is JObject v)
                d.Verdict = new RecordedVerdict { Guard = (string)v["guard"], Kind = (string)v["kind"], Reason = (string)v["reason"] };
            if (o["targetsUnavailable"] is JArray tu)
                d.TargetsUnavailable = tu.Select(x => new RecordedUnavailable
                    { Action = (string)x["action"], Target = (string)x["target"], Reason = (string)x["reason"] }).ToList();
            if (req?["actions"] is JArray acts) d.OfferedActions = acts.Select(x => (string)x).ToList();
            return d;
        }
    }
}
