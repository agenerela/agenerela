using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        public string Source;
        public string Label;
        public string Priority;
        public string Stimulus;

        // What was sent: the DecisionRequest as the provider received it.
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
        public string Outcome;
        public string Detail;
        public string Model;
        public float LatencySeconds;
        public float QueueWaitSeconds;
        public int PromptTokens;
        public int CompletionTokens;
        public int HistoryTurnsSent;
        public List<string> TargetsDropped = new List<string>();
        public bool Done;
    }

    /// <summary>
    /// Wraps the real provider and keeps every request it is handed and every reply it returns,
    /// then writes each finished decision to a JSON-lines file. Game-side, through the public
    /// <see cref="ILLMProvider"/> interface alone: a prototype of what the framework's Decision Log
    /// (build plan §2.7, Phase 5) needs to see.
    /// </summary>
    /// <remarks>
    /// <see cref="VillageCommands"/> sends one decision at a time, so the request the provider
    /// receives always belongs to the record opened last by <see cref="Begin"/>.
    /// </remarks>
    public sealed class DecisionRecorder : ILLMProvider
    {
        private readonly ILLMProvider inner;
        private readonly List<DecisionRecord> records = new List<DecisionRecord>();
        private readonly JsonSchemaSerializer serializer = new JsonSchemaSerializer();
        private DecisionRecord open;
        private int count;

        public DecisionRecorder(ILLMProvider inner, string filePath)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            FilePath = filePath;
        }

        /// <summary>Where each finished record is appended as one JSON line; null writes no file.</summary>
        public string FilePath { get; }

        /// <summary>Every record, oldest first. Read on the main thread.</summary>
        public IReadOnlyList<DecisionRecord> Records
        {
            get { lock (records) return records.ToList(); }
        }

        public string Name => inner.Name;
        public ProviderCapabilities Capabilities => inner.Capabilities;

        /// <summary>Opens the record for the decision about to be made.</summary>
        public DecisionRecord Begin(string source, string label, DecisionPriority priority, string stimulus)
        {
            var record = new DecisionRecord
            {
                Number = ++count,
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Source = source,
                Label = label,
                Priority = priority.ToString(),
                Stimulus = stimulus
            };
            lock (records) records.Add(record);
            open = record;
            return record;
        }

        public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
        {
            var record = open ?? Begin("unknown", "?", DecisionPriority.PlayerFacing, req.Stimulus);
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
            return Forward(record, req, ct);
        }

        // The reply may arrive on a worker thread, so nothing here touches a Unity API.
        private async Awaitable<ProviderResult> Forward(DecisionRecord record, DecisionRequest req, CancellationToken ct)
        {
            try
            {
                var result = await inner.RequestAsync(req, ct);
                record.RawReply = result?.Text;
                return result;
            }
            catch (Exception e)
            {
                record.ProviderError = e.GetType().Name + ": " + e.Message;
                throw;
            }
        }

        /// <summary>Fills in what the game did with the decision, and appends the record to the file.</summary>
        public void Complete(DecisionRecord record, DecisionResult result, string outcome, string detail)
        {
            var t = result?.Telemetry;
            // The model's answer, so a refused or contained one still shows what it asked for;
            // the outcome says whether it ran.
            var d = result?.ModelDecision;
            record.Action = d?.ActionId ?? "";
            record.Target = d?.TargetId ?? "";
            record.Statement = d?.Statement ?? "";
            record.Outcome = outcome;
            record.Detail = detail ?? "";
            if (t != null)
            {
                record.Model = t.Model;
                record.LatencySeconds = t.LatencySeconds;
                record.QueueWaitSeconds = t.QueueWaitSeconds;
                record.PromptTokens = t.PromptTokens;
                record.CompletionTokens = t.CompletionTokens;
                record.HistoryTurnsSent = t.HistoryTurnsSent;
                record.TargetsDropped = t.TargetsDropped?.ToList() ?? new List<string>();
            }
            record.Done = true;
            if (open == record) open = null;
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
