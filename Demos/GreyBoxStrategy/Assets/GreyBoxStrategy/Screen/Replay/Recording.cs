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
        /// <summary>The moves the game offered, "attack harrow", filled in by the replay from the request it answered.</summary>
        public List<string> OfferedMoves = new List<string>();
        /// <summary>The exchanges of memory sent, as the request carried them.</summary>
        public int HistoryEntries;
        /// <summary>The whole record, for anything the screen shows later.</summary>
        public JObject Raw;

        public bool IsEnvoy => Moment == "envoys";
        /// <summary>The writs turn: the army a marshal's order is for (the decision's correlation), or null for a court decision.</summary>
        public string ArmyId;
        /// <summary>The country this decision is for: a marshal's agent id is the country's plus <c>_marshal</c>.</summary>
        public string Country => AgentId != null && AgentId.EndsWith(MarshalSuffix, StringComparison.Ordinal) ? AgentId.Substring(0, AgentId.Length - MarshalSuffix.Length) : AgentId;
        public const string MarshalSuffix = "_marshal";
        /// <summary>The moment of an army order in the writs turn.</summary>
        public const string ArmyMoment = "army";
        /// <summary>The storyteller's own decision: the event it chose for <see cref="Turn"/>'s upkeep.</summary>
        public bool IsStory => Moment == StoryMoment;
        /// <summary>The scribe's decision: the title of the turn's event (<c>+title-call</c>).</summary>
        public bool IsTitle => Moment == TitleMoment;
        /// <summary>A ruler's answer to a dilemma, between turns, before <see cref="Turn"/>'s upkeep.</summary>
        public bool IsChoice => Moment == ChoiceMoment;
        /// <summary>Taken by the rules as a council or envoy move (the others are the storyteller's).</summary>
        public bool IsMove => !IsStory && !IsTitle && !IsChoice;
        public const string StoryMoment = "story";
        public const string TitleMoment = "title";
        public const string ChoiceMoment = "choices";
        /// <summary>The framework's execution status of a storyteller decision whose event was queued.</summary>
        public const string Done = "Done";
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
        /// <summary>The storyteller's agent id, and its scribe's (<c>Agents/Storyteller.cs</c>).</summary>
        public const string StorytellerId = "storyteller", ScribeId = "scribe";

        public int Seed;
        public string Title;
        public string Source;
        public Dictionary<string, RecordedPlayer> Players = new Dictionary<string, RecordedPlayer>(StringComparer.Ordinal);
        public List<RecordedDecision> Decisions = new List<RecordedDecision>();
        /// <summary>Changes made through the dev menu, each applied at the start of its turn.</summary>
        public List<DevEdit> DevEdits = new List<DevEdit>();
        /// <summary>True for a game the dev menu touched, or played with stand-in rulers: never a measured game.</summary>
        public bool Dev;
        /// <summary>
        /// The table arm when the game had a storyteller ("default+storyteller+s2+chains-skeleton"), or
        /// null. The replay plays the storyteller's recorded events under the same rules (<see cref="StoryArms"/>).
        /// </summary>
        public string StoryArm;

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
            rec.Dev = (bool?)root["dev"] ?? false;
            rec.StoryArm = (string)root["storyArm"];
            if (root["devEdits"] is JArray edits)
                foreach (var e in edits) rec.DevEdits.Add(new DevEdit { Turn = (int)e["turn"], Command = (string)e["command"] });
            return rec;
        }

        /// <summary>
        /// The recording as <see cref="Parse"/> reads it, so a game played on the screen can be
        /// saved and replayed. A decision from the log keeps its own record; one made by a person
        /// is written from its fields.
        /// </summary>
        public string ToJson()
        {
            var root = new JObject
            {
                ["seed"] = Seed,
                ["title"] = Title,
                ["source"] = Source,
                ["dev"] = Dev,
            };
            if (StoryArm != null) root["storyArm"] = StoryArm;
            var players = new JObject();
            foreach (var kv in Players) players[kv.Key] = new JObject { ["model"] = kv.Value.Model, ["persona"] = kv.Value.Persona };
            root["players"] = players;
            var decisions = new JArray();
            foreach (var d in Decisions)
            {
                JObject o;
                if (d.Raw != null) o = (JObject)d.Raw.DeepClone();
                else
                {
                    o = new JObject
                    {
                        ["agentId"] = d.AgentId,
                        ["chainId"] = d.ChainId,
                        ["sequence"] = d.Sequence,
                        ["modelName"] = d.ModelName,
                        ["decision"] = new JObject { ["action"] = d.Action, ["target"] = d.Target, ["statement"] = d.Statement },
                        ["modelDecision"] = new JObject { ["action"] = d.ModelAction, ["target"] = d.ModelTarget },
                        ["request"] = new JObject { ["observations"] = d.Observations, ["stimulusTurn"] = d.StimulusTurn },
                    };
                    if (d.ExecutionStatus != null) o["execution"] = new JObject { ["status"] = d.ExecutionStatus, ["reason"] = d.ExecutionReason };
                }
                o["turn"] = d.Turn;
                o["moment"] = d.Moment;
                if (d.ArmyId != null) o["army"] = d.ArmyId;
                decisions.Add(o);
            }
            root["decisions"] = decisions;
            root["devEdits"] = new JArray(DevEdits.Select(e => new JObject { ["turn"] = e.Turn, ["command"] = e.Command }));
            return root.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>
        /// A recording from the framework's decision log as it is being written: the lines of a
        /// <c>decisions.jsonl</c> (<c>DecisionLog</c>), the same conversion as
        /// <c>ArtSource/export_recording.py</c>. Each decision keeps its "Decided" record with the
        /// execution of its "Executed" one.
        /// </summary>
        public static Recording FromDecisionLog(IEnumerable<string> lines, int seed, Dictionary<string, RecordedPlayer> players, string title = null, string storyArm = null)
        {
            // The storyteller and its scribe decide outside the rulers' rounds, so their lines carry no
            // chain id: the storyteller's stimulus names the turn ("Turn 7. What happens?"), and the
            // scribe titles the storyteller's last event.
            var told = new System.Text.RegularExpressions.Regex(@"^Turn (\d+)\.");
            int storyTurn = 0;
            var rx = new System.Text.RegularExpressions.Regex(@"^seed-(\d+)-turn-(\d+)-(\w+)");
            var decided = new List<JObject>();
            var executed = new Dictionary<string, JToken>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                JObject o;
                try { o = JObject.Parse(line); } catch (Newtonsoft.Json.JsonReaderException) { continue; } // a line still being written
                string agent = (string)o["agentId"];
                if (string.IsNullOrEmpty((string)o["chainId"]) && (agent == StorytellerId || agent == ScribeId))
                {
                    if (agent == StorytellerId)
                    {
                        var t = told.Match((string)o["stimulus"] ?? "");
                        if (!t.Success) continue;
                        storyTurn = int.Parse(t.Groups[1].Value);
                    }
                    if (storyTurn == 0) continue;
                    o["turn"] = storyTurn;
                    o["moment"] = agent == StorytellerId ? RecordedDecision.StoryMoment : RecordedDecision.TitleMoment;
                    o["chainId"] = $"seed-{seed}-turn-{storyTurn}-{o["moment"]}";
                    if ((string)o["stage"] == "Executed") executed[(string)o["chainId"] + "|" + agent] = o["execution"];
                    else if ((string)o["stage"] == "Decided") decided.Add(o);
                    continue;
                }
                var m = rx.Match((string)o["chainId"] ?? "");
                if (!m.Success || int.Parse(m.Groups[1].Value) != seed) continue;
                o["turn"] = int.Parse(m.Groups[2].Value);
                o["moment"] = m.Groups[3].Value;
                string stage = (string)o["stage"];
                if (stage == "Executed") executed[(string)o["chainId"] + "|" + (string)o["agentId"]] = o["execution"];
                else if (stage == "Decided") decided.Add(o);
            }
            var rec = new Recording { Seed = seed, Title = title ?? $"Live game, seed {seed}", Source = "live", Players = players, StoryArm = storyArm };
            foreach (var o in decided.OrderBy(o => (int)o["turn"]).ThenBy(o => (string)o["moment"] != "council").ThenBy(o => (long?)o["sequence"] ?? 0))
            {
                if (executed.TryGetValue((string)o["chainId"] + "|" + (string)o["agentId"], out var ex)) o["execution"] = ex;
                rec.Decisions.Add(ParseDecision(o));
            }
            return rec;
        }

        /// <summary>One decision from a decision log's "Decided" record, with <c>turn</c> and <c>moment</c> set on it.</summary>
        public static RecordedDecision FromLogRecord(JObject o) => ParseDecision(o);

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
                ArmyId = (string)o["army"],
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
            if (req?["history"] is JArray hist) d.HistoryEntries = hist.Count;
            return d;
        }
    }
}
