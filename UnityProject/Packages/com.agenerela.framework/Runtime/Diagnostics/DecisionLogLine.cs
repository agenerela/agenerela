using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Agenerela
{
    /// <summary>
    /// One line of a decision log file (#190, G7): one <see cref="DecisionFeedEntry"/>, copied as it
    /// arrived. <see cref="DecisionLog"/> writes these and <see cref="DecisionLogReader"/> reads them
    /// back, so the writer, the tests and the Decision Log window (#172) share one definition of the
    /// format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each field is written under the name in its <c>[JsonProperty]</c>, in camelCase, and a field
    /// that is null is left out of the line. Enums are written by name. <see cref="Version"/> is
    /// <see cref="CurrentVersion"/> for every line this package writes; a later version only adds
    /// fields, so a reader keeps reading the ones it knows and ignores the rest.
    /// </para>
    /// <para>
    /// Plain data on purpose: nothing here refers to an <see cref="Agent"/>, a result or a scene
    /// object, so a line kept in a list keeps nothing alive.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DecisionLogLine
    {
        /// <summary>
        /// The format version this package writes. Raised only when a field is added: 2 added
        /// <see cref="ChainId"/>, <see cref="ChainHop"/> and <see cref="ChainSource"/> (#190, G4).
        /// </summary>
        public const int CurrentVersion = 2;

        /// <summary>
        /// The format version of this line, <see cref="CurrentVersion"/> when this package wrote it. Every
        /// line has one; a JSON object without it is not a decision log line.
        /// </summary>
        [JsonProperty("version", Required = Required.Always)] public int Version = CurrentVersion;

        /// <summary>When the feed raised the entry, in UTC (<see cref="DecisionFeedEntry.Time"/>).</summary>
        [JsonProperty("time")] public DateTime Time;

        /// <summary>
        /// <see cref="DecisionFeedEntry.Sequence"/>: rising across every agent, from 1 at the start of
        /// each Play session, so it orders one session's lines and starts again in the next.
        /// </summary>
        [JsonProperty("sequence")] public long Sequence;

        /// <summary>Whether the line is the decision or its execution.</summary>
        [JsonProperty("stage")] public DecisionStage Stage;

        /// <summary>The agent's <see cref="Agent.Id"/>; left out when it has none.</summary>
        [JsonProperty("agentId")] public string AgentId;

        /// <summary>The agent's <see cref="AgentIdentity.Name"/>.</summary>
        [JsonProperty("agentName")] public string AgentName;

        /// <summary>
        /// The chain of relayed decisions this one is in (<see cref="DecisionTelemetry.ChainId"/>),
        /// for grouping a log by chain; left out for a decision in none, and on every version 1 line.
        /// </summary>
        [JsonProperty("chainId")] public string ChainId;

        /// <summary>That chain's hop, 0 for its first decision; left out with no <see cref="ChainId"/>.</summary>
        [JsonProperty("chainHop")] public int? ChainHop;

        /// <summary>
        /// The <see cref="Agent.Id"/> of the agent whose decision was relayed to this one
        /// (<see cref="DecisionTelemetry.ChainSource"/>); left out at a chain's start, with no chain,
        /// and when that agent has no id.
        /// </summary>
        [JsonProperty("chainSource")] public string ChainSource;

        /// <summary>The call's stimulus label, such as <c>Player</c>, as the game passed it.</summary>
        [JsonProperty("label")] public string Label;

        /// <summary>The call's stimulus, as the game passed it, without its label.</summary>
        [JsonProperty("stimulus")] public string Stimulus;

        /// <summary>The call's declared priority (<see cref="DecisionTelemetry.Priority"/>).</summary>
        [JsonProperty("priority")] public DecisionPriority Priority;

        /// <summary>
        /// What the game got, after the guards (<see cref="DecisionResult.Decision"/>); left out for a
        /// pipeline error, which has none.
        /// </summary>
        [JsonProperty("decision")] public DecisionLogDecision Decision;

        /// <summary>
        /// What the model itself answered (<see cref="DecisionResult.ModelDecision"/>), only when a
        /// guard changed it; left out when it is the same as <see cref="Decision"/>.
        /// </summary>
        [JsonProperty("modelDecision")] public DecisionLogDecision ModelDecision;

        /// <summary>
        /// The verdict that stopped the model's answer inside <c>DecideAsync</c>
        /// (<see cref="DecisionResult.Verdict"/>); left out when every guard passed.
        /// </summary>
        [JsonProperty("verdict")] public DecisionLogVerdict Verdict;

        /// <summary>
        /// Each guard that did not pass the decision, in order, a refusal by <c>Execute</c>'s re-check
        /// included (<see cref="DecisionTelemetry.GuardsFired"/>); left out when none did.
        /// </summary>
        [JsonProperty("guardsFired")] public List<string> GuardsFired;

        /// <summary>
        /// For <see cref="DecisionStage.Executed"/>, what that <c>Execute</c> did
        /// (<see cref="DecisionFeedEntry.Execution"/>); left out for <see cref="DecisionStage.Decided"/>.
        /// </summary>
        [JsonProperty("execution")] public DecisionLogExecution Execution;

        /// <summary>Why there is no decision, for a pipeline error; left out otherwise.</summary>
        [JsonProperty("error")] public DecisionLogError Error;

        /// <summary>The provider's name, such as <c>Ollama</c>.</summary>
        [JsonProperty("provider")] public string Provider;

        /// <summary>The provider config asset that answered, such as <c>OllamaLocal</c>; left out when built in code.</summary>
        [JsonProperty("providerConfig")] public string ProviderConfig;

        /// <summary>The model that answered, as the provider reported it; left out when it did not say.</summary>
        [JsonProperty("modelName")] public string ModelName;

        /// <summary>The framework version that made the decision.</summary>
        [JsonProperty("frameworkVersion")] public string FrameworkVersion;

        /// <summary>The provider's own time for the request, in seconds (<see cref="DecisionTelemetry.LatencySeconds"/>).</summary>
        [JsonProperty("latencySeconds")] public float LatencySeconds;

        /// <summary>The wait in the provider's queue, in seconds (<see cref="DecisionTelemetry.QueueWaitSeconds"/>).</summary>
        [JsonProperty("queueWaitSeconds")] public float QueueWaitSeconds;

        /// <summary>The wait in the agent's own backlog, in seconds (<see cref="DecisionTelemetry.BacklogWaitSeconds"/>).</summary>
        [JsonProperty("backlogWaitSeconds")] public float BacklogWaitSeconds;

        /// <summary>Requests ahead in the queue when it joined (<see cref="DecisionTelemetry.QueuedAhead"/>).</summary>
        [JsonProperty("queuedAhead")] public int QueuedAhead;

        /// <summary>Prompt tokens, as the provider reported them; 0 when it did not.</summary>
        [JsonProperty("promptTokens")] public int PromptTokens;

        /// <summary>Completion tokens, as the provider reported them; 0 when it did not.</summary>
        [JsonProperty("completionTokens")] public int CompletionTokens;

        /// <summary>The target grounding share (<see cref="DecisionTelemetry.TargetGrounding"/>); left out unless scored.</summary>
        [JsonProperty("targetGrounding")] public float? TargetGrounding;

        /// <summary>Whether the grounding share was read, or why not; left out when the decision never reached the guard.</summary>
        [JsonProperty("targetGroundingStatus")] public GroundingStatus? TargetGroundingStatus;

        /// <summary>Exchanges of memory the request carried (<see cref="DecisionTelemetry.HistoryTurnsSent"/>).</summary>
        [JsonProperty("historyTurnsSent")] public int HistoryTurnsSent;

        /// <summary>Exchanges the memory held but left out (<see cref="DecisionTelemetry.HistoryTurnsDropped"/>).</summary>
        [JsonProperty("historyTurnsDropped")] public int HistoryTurnsDropped;

        /// <summary>Target ids a source left out for its cap (<see cref="DecisionTelemetry.TargetsDropped"/>); left out when none were.</summary>
        [JsonProperty("targetsDropped")] public List<string> TargetsDropped;

        /// <summary>
        /// How many observation lines the request does not carry
        /// (<see cref="DecisionTelemetry.ObservationsDropped"/>). The count, not the lines: they are
        /// prompt text, written only with the prompt.
        /// </summary>
        [JsonProperty("observationsDropped")] public int ObservationsDropped;

        /// <summary>
        /// The model's reply, exactly as the provider returned it (<see cref="DecisionResult.Reply"/>):
        /// what a replay scripts with <c>ScriptedReply.Raw</c>. Left out when there was none.
        /// </summary>
        [JsonProperty("reply")] public string Reply;

        /// <summary>
        /// What was sent, only when <see cref="DecisionLog.IncludePrompt"/> is on and the request was
        /// sent; left out otherwise.
        /// </summary>
        [JsonProperty("request")] public DecisionLogRequest Request;

        /// <summary>
        /// The line a <see cref="DecisionLog"/> writes for <paramref name="entry"/>, without writing it:
        /// for a game or tool that keeps its own list of decisions, such as a history panel, in the
        /// same shape as the file. Everything is copied out of the result now, so call it when the
        /// entry arrives: the result's telemetry changes when it is executed.
        /// </summary>
        /// <param name="entry">A <see cref="DecisionFeed"/> entry.</param>
        /// <param name="withPrompt">Whether to fill <see cref="Request"/>, as <see cref="DecisionLog.IncludePrompt"/> does.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
        public static DecisionLogLine From(DecisionFeedEntry entry, bool withPrompt = false)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var result = entry.Result;
            var telemetry = result.Telemetry;
            var request = result.Request;
            var agent = entry.Agent;

            var line = new DecisionLogLine
            {
                Time = entry.Time,
                Sequence = entry.Sequence,
                Stage = entry.Stage,
                AgentId = agent?.Id,
                AgentName = agent?.Identity?.Name,
                Label = Mask(request?.StimulusLabel),
                Stimulus = Mask(request?.StimulusText),
                Priority = telemetry.Priority,
                ChainId = Empty(telemetry.ChainId),
                ChainHop = string.IsNullOrEmpty(telemetry.ChainId) ? (int?)null : telemetry.ChainHop,
                ChainSource = string.IsNullOrEmpty(telemetry.ChainId) ? null : Empty(telemetry.ChainSource),
                Decision = DecisionLogDecision.From(result.Decision),
                Provider = Empty(telemetry.ProviderName),
                ProviderConfig = Empty(telemetry.ProviderConfig),
                ModelName = Empty(telemetry.Model),
                FrameworkVersion = Empty(telemetry.FrameworkVersion),
                LatencySeconds = telemetry.LatencySeconds,
                QueueWaitSeconds = telemetry.QueueWaitSeconds,
                BacklogWaitSeconds = telemetry.BacklogWaitSeconds,
                QueuedAhead = telemetry.QueuedAhead,
                PromptTokens = telemetry.PromptTokens,
                CompletionTokens = telemetry.CompletionTokens,
                TargetGrounding = telemetry.TargetGrounding,
                TargetGroundingStatus = telemetry.TargetGroundingStatus,
                HistoryTurnsSent = telemetry.HistoryTurnsSent,
                HistoryTurnsDropped = telemetry.HistoryTurnsDropped,
                ObservationsDropped = telemetry.ObservationsDropped?.Count ?? 0,
                Reply = MaskReply(result.Reply?.Text),
            };

            var model = DecisionLogDecision.From(result.ModelDecision);
            if (model != null && !model.SameAs(line.Decision))
            {
                line.ModelDecision = model;
            }

            if (result.Verdict.HasValue)
            {
                var verdict = result.Verdict.Value;
                line.Verdict = new DecisionLogVerdict { Guard = verdict.Guard, Kind = verdict.Kind, Reason = Mask(verdict.Reason) };
            }

            if (telemetry.GuardsFired != null && telemetry.GuardsFired.Count > 0)
            {
                line.GuardsFired = new List<string>(telemetry.GuardsFired);
            }

            if (telemetry.TargetsDropped != null && telemetry.TargetsDropped.Count > 0)
            {
                line.TargetsDropped = new List<string>(telemetry.TargetsDropped);
            }

            if (entry.Execution.HasValue)
            {
                line.Execution = new DecisionLogExecution { Status = entry.Execution.Value.Status, Reason = Mask(entry.Execution.Value.Reason) };
            }

            if (telemetry.ErrorKind.HasValue)
            {
                line.Error = new DecisionLogError { Kind = telemetry.ErrorKind.Value, Message = Mask(telemetry.ErrorMessage) };
            }

            if (withPrompt && request != null)
            {
                line.Request = new DecisionLogRequest
                {
                    SystemPrompt = Mask(request.SystemPrompt),
                    FewShotBlock = Mask(Empty(request.FewShotBlock)),
                    Observations = Mask(Empty(request.Observations)),
                    History = request.History != null ? request.History.Select(Mask).ToList() : new List<string>(),
                    StimulusTurn = Mask(request.Stimulus),
                    Actions = Allowed(request.Schema, DecisionSchema.ActionFieldName),
                    Targets = Allowed(request.Schema, DecisionSchema.TargetFieldName),
                };
            }

            return line;
        }

        // Any text the line copies may quote a URL with a user and password in it, a provider's
        // reply or a handler's reason as much as an error, so every free-text field is masked. Text
        // may also quote JSON, as an error message quotes the reply it could not read, where JSON
        // escapes can hide the URL: when decoding them reveals one, the decoded text is masked and
        // written instead.
        internal static string Mask(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var masked = DecisionLog.MaskCredentials(text);
            var decoded = DecodeJsonEscapes(masked);
            var maskedDecoded = DecisionLog.MaskCredentials(decoded);
            return maskedDecoded != decoded ? maskedDecoded : masked;
        }

        // A reply is usually JSON, where a URL can be written with escapes (an escaped slash, or a
        // Unicode escape for any character) that a pattern on the raw text would have to chase one
        // by one. So a reply that is JSON is read token by token, every string, property name and
        // comment
        // decoded and masked, and written again, duplicate properties included, since reading it
        // into an object would keep only the last of each. It is written again only when a token
        // changed, so the masked text differs from what the model sent only where it had to. A
        // reply that is not one JSON value is plain text, and masked as such.
        internal static string MaskReply(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var changed = false;
            var output = new StringWriter();
            try
            {
                using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Decimal })
                using (var writer = new JsonTextWriter(output) { Formatting = Formatting.None })
                {
                    while (reader.Read())
                    {
                        switch (reader.TokenType)
                        {
                            case JsonToken.PropertyName:
                            case JsonToken.String:
                            case JsonToken.Comment:
                                var original = (string)reader.Value ?? string.Empty;
                                var masked = Mask(original);
                                changed |= masked != original;
                                if (reader.TokenType == JsonToken.PropertyName)
                                {
                                    writer.WritePropertyName(masked);
                                }
                                else if (reader.TokenType == JsonToken.Comment)
                                {
                                    writer.WriteComment(masked);
                                }
                                else
                                {
                                    writer.WriteValue(masked);
                                }

                                break;
                            default:
                                writer.WriteToken(reader, false);
                                break;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON, or JSON cut short: masked as text, which also sees through its escapes.
                return Mask(text);
            }

            // Masked once more as text, whichever it returns: a safety net for any token kind this
            // rewrite copies as it is.
            return Mask(changed ? output.ToString() : text);
        }

        // Decodes the escapes JSON allows in a string (an escaped slash or quote, a Unicode escape),
        // for masking text that may be or may quote JSON.
        private static readonly System.Text.RegularExpressions.Regex JsonEscape = new System.Text.RegularExpressions.Regex(
            @"\\u([0-9a-fA-F]{4})|\\([/\\""])");

        private static string DecodeJsonEscapes(string text)
        {
            return JsonEscape.Replace(text, m => m.Groups[1].Success
                ? ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString()
                : m.Groups[2].Value);
        }

        private static string Empty(string text)
        {
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static List<string> Allowed(DecisionSchema schema, string field)
        {
            if (schema?.Fields == null)
            {
                return null;
            }

            foreach (var f in schema.Fields)
            {
                if (f != null && f.Name == field)
                {
                    return f.AllowedValues != null ? new List<string>(f.AllowedValues) : null;
                }
            }

            return null;
        }
    }

    /// <summary>An action, a target and a statement, as a <see cref="DecisionLogLine"/> holds a decision.</summary>
    [Serializable]
    public sealed class DecisionLogDecision
    {
        /// <summary>The action id, such as <c>move_to</c> or <c>none</c>.</summary>
        [JsonProperty("action")] public string Action;

        /// <summary>The target id, or <c>no_target</c>.</summary>
        [JsonProperty("target")] public string Target;

        /// <summary>The statement, possibly empty.</summary>
        [JsonProperty("statement")] public string Statement;

        internal static DecisionLogDecision From(AgentDecision decision)
        {
            return decision == null
                ? null
                : new DecisionLogDecision { Action = decision.ActionId, Target = decision.TargetId, Statement = DecisionLogLine.Mask(decision.Statement) };
        }

        internal bool SameAs(DecisionLogDecision other)
        {
            return other != null && Action == other.Action && Target == other.Target && Statement == other.Statement;
        }
    }

    /// <summary>A guard's verdict, as a <see cref="DecisionLogLine"/> holds it.</summary>
    [Serializable]
    public sealed class DecisionLogVerdict
    {
        /// <summary>The guard's name, as <see cref="DecisionTelemetry.GuardsFired"/> records it.</summary>
        [JsonProperty("guard")] public string Guard;

        /// <summary>Rewrite to none, or reject.</summary>
        [JsonProperty("kind")] public GuardVerdictKind Kind;

        /// <summary>The guard's reason, word for word.</summary>
        [JsonProperty("reason")] public string Reason;
    }

    /// <summary>What one <c>Execute</c> did, as a <see cref="DecisionLogLine"/> holds it.</summary>
    [Serializable]
    public sealed class DecisionLogExecution
    {
        /// <summary>Done, failed, no action, or refused.</summary>
        [JsonProperty("status")] public ExecutionStatus Status;

        /// <summary>The handler's or the agent's reason; left out for done and no action.</summary>
        [JsonProperty("reason")] public string Reason;
    }

    /// <summary>A pipeline error, as a <see cref="DecisionLogLine"/> holds it.</summary>
    [Serializable]
    public sealed class DecisionLogError
    {
        /// <summary>Why there is no decision.</summary>
        [JsonProperty("kind")] public PipelineErrorKind Kind;

        /// <summary>The warning the agent logged, with any credentials in a URL masked.</summary>
        [JsonProperty("message")] public string Message;
    }

    /// <summary>
    /// What a decision sent, as a <see cref="DecisionLogLine"/> holds it when
    /// <see cref="DecisionLog.IncludePrompt"/> is on: each block of the <c>DecisionRequest</c>, and
    /// the actions and targets its schema offered.
    /// </summary>
    [Serializable]
    public sealed class DecisionLogRequest
    {
        /// <summary>The system prompt.</summary>
        [JsonProperty("systemPrompt")] public string SystemPrompt;

        /// <summary>The few-shot block; left out when there was none.</summary>
        [JsonProperty("fewShotBlock")] public string FewShotBlock;

        /// <summary>The observations block; left out when there was none.</summary>
        [JsonProperty("observations")] public string Observations;

        /// <summary>The memory sent, two entries per exchange, oldest first.</summary>
        [JsonProperty("history")] public List<string> History;

        /// <summary>The final user turn, label included, as it was sent.</summary>
        [JsonProperty("stimulusTurn")] public string StimulusTurn;

        /// <summary>The action ids the schema allowed.</summary>
        [JsonProperty("actions")] public List<string> Actions;

        /// <summary>The target ids the schema allowed, <c>no_target</c> included.</summary>
        [JsonProperty("targets")] public List<string> Targets;
    }

    // The one set of serializer settings the writer and the reader share, so the format has a
    // single definition.
    internal static class DecisionLogFormat
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateParseHandling = DateParseHandling.DateTime,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = { new StringEnumConverter() },
        };
    }
}
