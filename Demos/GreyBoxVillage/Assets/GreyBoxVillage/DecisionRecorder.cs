using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// One decision as the History panel and the log file show it: what was sent, what came back,
    /// and what the game did with it. Strings only, so <c>JsonUtility</c> writes it as one line.
    /// </summary>
    [Serializable]
    public sealed class DecisionRecord
    {
        public int Number;
        public string Time;
        public string Agent;
        public string Source;
        public string Label;
        public string Priority;
        public string Stimulus;

        // What was sent: the DecisionRequest the agent handed its provider.
        public bool Sent;
        public string SystemPrompt;
        public string FewShotBlock;
        public string Observations;
        public List<string> History = new List<string>();
        public string StimulusTurn;
        public List<string> OfferedActions = new List<string>();
        public List<string> OfferedTargets = new List<string>();
        public string SchemaJson;

        // What came back.
        public string RawReply;
        public string ProviderError;

        // What the model chose, and what the game did with it: executed, none, refused or contained.
        public string Action;
        public string Target;
        public string Statement;
        // What the agent said: the statement when the action ran, the refusal line after a guard.
        public string Said;
        public string Outcome;
        public string Detail;
        public string Guard;
        public string GuardReason;
        public string Model;
        public float LatencySeconds;
        public float QueueWaitSeconds;
        public int QueuedAhead;
        public int PromptTokens;
        public int CompletionTokens;
        public int HistoryTurnsSent;
        public List<string> TargetsDropped = new List<string>();
        public bool Done;
    }

    /// <summary>
    /// Keeps every decision the village's agents make, what was sent and what came back, and writes
    /// each finished one to a JSON-lines file. Game-side, through the public API alone: a prototype of
    /// what the framework's Decision Log (build plan §2.7, Phase 5) needs to see.
    /// </summary>
    /// <remarks>
    /// The request and reply come from the result itself (<see cref="DecisionResult.Request"/> and
    /// <see cref="DecisionResult.Reply"/>, #142), never from wrapping the provider. Several agents
    /// share one provider and its queue, which reorders their requests by priority, so a wrapper could
    /// not tell whose request it was handed. Main thread only.
    /// </remarks>
    public sealed class DecisionRecorder
    {
        private readonly List<DecisionRecord> records = new List<DecisionRecord>();
        private readonly JsonSchemaSerializer serializer = new JsonSchemaSerializer();
        private int count;

        public DecisionRecorder(string filePath) => FilePath = filePath;

        /// <summary>Where each finished record is appended as one JSON line; null writes no file.</summary>
        public string FilePath { get; }

        /// <summary>Every record, oldest first.</summary>
        public IReadOnlyList<DecisionRecord> Records => records;

        /// <summary>Opens the record for the decision an agent is about to make.</summary>
        public DecisionRecord Begin(string agent, string source, string label, DecisionPriority priority, string stimulus)
        {
            var record = new DecisionRecord
            {
                Number = ++count,
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Agent = agent,
                Source = source,
                Label = label,
                Priority = priority.ToString(),
                Stimulus = stimulus
            };
            records.Add(record);
            return record;
        }

        /// <summary>Copies what was sent and what came back from the result into the record.</summary>
        public void Attach(DecisionRecord record, DecisionResult result)
        {
            var req = result?.Request;
            if (req != null)
            {
                record.Sent = true;
                record.SystemPrompt = req.SystemPrompt;
                record.FewShotBlock = req.FewShotBlock;
                record.Observations = req.Observations;
                record.History = req.History?.ToList() ?? new List<string>();
                record.StimulusTurn = req.Stimulus;
                if (req.Schema != null)
                {
                    record.OfferedActions = Allowed(req.Schema, DecisionSchema.ActionFieldName);
                    record.OfferedTargets = Allowed(req.Schema, DecisionSchema.TargetFieldName);
                    try { record.SchemaJson = serializer.Serialize(req.Schema); }
                    catch (Exception e) { record.SchemaJson = "(could not serialize: " + e.Message + ")"; }
                }
            }
            record.RawReply = result?.Reply?.Text;
            var t = result?.Telemetry;
            if (t != null && t.ErrorKind != null) record.ProviderError = t.ErrorKind + ": " + t.ErrorMessage;
        }

        /// <summary>Fills in what the game did with the decision, and appends the record to the file.</summary>
        public void Complete(DecisionRecord record, DecisionResult result, string outcome, string detail, string said = "")
        {
            if (result != null && !record.Sent) Attach(record, result);
            var t = result?.Telemetry;
            // The model's answer, so a refused or contained one still shows what it asked for;
            // the outcome says whether it ran.
            var d = result?.ModelDecision;
            record.Action = d?.ActionId ?? "";
            record.Target = d?.TargetId ?? "";
            record.Statement = d?.Statement ?? "";
            record.Said = said ?? "";
            record.Outcome = outcome;
            record.Detail = detail ?? "";
            if (t != null)
            {
                record.Model = t.Model;
                record.LatencySeconds = t.LatencySeconds;
                record.QueueWaitSeconds = t.QueueWaitSeconds;
                record.QueuedAhead = t.QueuedAhead;
                record.PromptTokens = t.PromptTokens;
                record.CompletionTokens = t.CompletionTokens;
                record.HistoryTurnsSent = t.HistoryTurnsSent;
                record.TargetsDropped = t.TargetsDropped?.ToList() ?? new List<string>();
                record.Guard = t.GuardsFired.Count > 0 ? t.GuardsFired[t.GuardsFired.Count - 1] : "";
                record.GuardReason = t.GuardReason ?? "";
            }
            record.Done = true;
            Append(record);
        }

        private void Append(DecisionRecord record)
        {
            if (string.IsNullOrEmpty(FilePath)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.AppendAllText(FilePath, JsonUtility.ToJson(record) + "\n");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not write the decision log: " + e.Message);
            }
        }

        private static List<string> Allowed(DecisionSchema schema, string field)
        {
            var match = schema.Fields.FirstOrDefault(f => f.Name == field);
            return match?.AllowedValues?.ToList() ?? new List<string>();
        }

        /// <summary>A new log file for this play session: the project's Logs folder in the Editor.</summary>
        public static string DefaultFilePath()
        {
            string folder = Application.isEditor
                ? Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs")
                : Application.persistentDataPath;
            return Path.Combine(folder, "agenerela-decisions-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jsonl");
        }
    }
}
