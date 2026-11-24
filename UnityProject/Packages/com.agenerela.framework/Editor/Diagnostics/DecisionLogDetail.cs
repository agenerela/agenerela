using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Agenerela.Editor.Diagnostics
{
    /// <summary>What one guard did to a decision, as section ⑤ shows it.</summary>
    internal enum GuardStep
    {
        /// <summary>It ran and let the decision through.</summary>
        Passed,

        /// <summary>It ran and replaced the statement, keeping the move (#266 item 4).</summary>
        ReplacedStatement,

        /// <summary>It stopped the decision: a rewrite to <c>none</c> or a reject.</summary>
        Stopped,

        /// <summary>It threw, which made the decision a pipeline error.</summary>
        Failed,

        /// <summary>A guard before it stopped the decision, so it never ran.</summary>
        NotRun,

        /// <summary><c>Execute</c>'s re-check refused the decision afterwards.</summary>
        RefusedAtExecute,
    }

    /// <summary>One row of section ⑤: a guard, what it did, and why.</summary>
    internal readonly struct GuardRow
    {
        internal GuardRow(string guard, GuardStep step, string reason = null)
        {
            Guard = guard;
            Step = step;
            Reason = reason;
        }

        internal string Guard { get; }
        internal GuardStep Step { get; }

        /// <summary>The guard's reason, word for word, or a note; null for a plain pass.</summary>
        internal string Reason { get; }
    }

    /// <summary>
    /// What the detail panel shows for one line (#172 ④ to ⑥), worked out from the line alone so a
    /// test reads it without a window, and a line read back from a file shows the same.
    /// </summary>
    internal static class DecisionLogDetail
    {
        private const string GroundingGuard = nameof(TargetGroundingGuard);
        private const string AtExecute = "@Execute";

        /// <summary>
        /// Every guard in the order it ran or would have (#172 ⑤): those before the one that stopped
        /// the decision passed, that one stopped it, the rest never ran, then any refusal by
        /// <c>Execute</c>'s re-check. Built-in and the developer's own alike, by the name each
        /// reports, so a new guard needs no change here.
        /// </summary>
        /// <remarks>
        /// A line from before version 4 has no guard list, only the guards that fired, so it shows
        /// those. A pipeline error before the guards shows none.
        /// </remarks>
        internal static List<GuardRow> Guards(DecisionLogLine line)
        {
            var rows = new List<GuardRow>();
            var order = line.Guards ?? (line.GuardsFired ?? new List<string>()).Where(g => !g.EndsWith(AtExecute, StringComparison.Ordinal)).ToList();
            var stopper = line.Verdict?.Guard;
            var failed = line.Error?.Kind == PipelineErrorKind.GuardFailed ? FailedGuard(order, line.Error.Message) : -1;

            var replaced = new Dictionary<string, string>();
            if (line.StatementsReplaced != null)
            {
                foreach (var r in line.StatementsReplaced)
                {
                    if (r.Guard != null && !replaced.ContainsKey(r.Guard))
                    {
                        replaced[r.Guard] = r.Reason;
                    }
                }
            }

            var over = false;
            for (int i = 0; i < order.Count; i++)
            {
                var guard = order[i];
                if (over)
                {
                    rows.Add(new GuardRow(guard, GuardStep.NotRun));
                }
                else if (i == failed)
                {
                    rows.Add(new GuardRow(guard, GuardStep.Failed, line.Error.Message));
                    over = true;
                }
                else if (stopper != null && guard == stopper)
                {
                    rows.Add(new GuardRow(guard, GuardStep.Stopped, StopReason(line)));
                    over = true;
                }
                else if (replaced.TryGetValue(guard, out var reason))
                {
                    rows.Add(new GuardRow(guard, GuardStep.ReplacedStatement, reason));
                }
                else
                {
                    rows.Add(new GuardRow(guard, GuardStep.Passed, guard == GroundingGuard ? GroundingNote(line) : null));
                }
            }

            // A stop by a guard the list does not hold, as on a line from before version 4.
            if (stopper != null && !order.Contains(stopper))
            {
                rows.Add(new GuardRow(stopper, GuardStep.Stopped, StopReason(line)));
            }

            if (line.GuardsFired != null)
            {
                foreach (var fired in line.GuardsFired.Where(g => g.EndsWith(AtExecute, StringComparison.Ordinal)))
                {
                    rows.Add(new GuardRow(fired, GuardStep.RefusedAtExecute, line.Execution?.Reason));
                }
            }

            return rows;
        }

        /// <summary>The grounding share and why it was or was not read (DR-016), or null when the guard never saw the decision.</summary>
        internal static string GroundingNote(DecisionLogLine line)
        {
            if (line.TargetGroundingStatus == null)
            {
                return null;
            }

            if (line.TargetGroundingStatus == GroundingStatus.Scored && line.TargetGrounding.HasValue)
            {
                return $"{Percent(line.TargetGrounding.Value)} of the model's choice of target stayed on the list";
            }

            return line.TargetGroundingStatus switch
            {
                GroundingStatus.NoProbabilities => "not scored: the reply carried no token probabilities",
                GroundingStatus.NoTargetField => "not scored: no target was offered",
                GroundingStatus.Unreadable => "not scored: the target could not be found in the reply's tokens",
                GroundingStatus.Off => "off for this call (CheckTarget)",
                _ => line.TargetGroundingStatus.ToString(),
            };
        }

        /// <summary>The verdict's kind and reason, and the grounding share when the grounding guard stopped it.</summary>
        private static string StopReason(DecisionLogLine line)
        {
            var kind = line.Verdict.Kind == GuardVerdictKind.RewriteToNone ? "Rewrite to none" : line.Verdict.Kind.ToString();
            var text = kind + ": " + line.Verdict.Reason;
            if (line.Verdict.Guard == GroundingGuard && line.TargetGroundingStatus == GroundingStatus.Scored && line.TargetGrounding.HasValue)
            {
                text += $" (share {Percent(line.TargetGrounding.Value)})";
            }

            return text;
        }

        // The guard a GuardFailed error names: Agent writes "The guard 'Name' failed" or, for a null
        // entry, "The guard Guards[i] failed".
        private static int FailedGuard(List<string> order, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return -1;
            }

            for (int i = 0; i < order.Count; i++)
            {
                if (message.Contains("'" + order[i] + "'") || (order[i].StartsWith("Guards[", StringComparison.Ordinal) && message.Contains(order[i])))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// The line the agent said because a guard stopped it (#109): the decision's statement once a
        /// guard rewrote it, which is empty unless the game's <c>RefusalStatement</c> gave one.
        /// </summary>
        internal static string RefusalLine(DecisionLogLine line)
        {
            return line.Verdict != null && !string.IsNullOrEmpty(line.Decision?.Statement) ? line.Decision.Statement : null;
        }

        /// <summary>The observations sent, one per line, or none when the prompt was not kept.</summary>
        internal static List<string> Observations(DecisionLogLine line)
        {
            var block = line.Request?.Observations;
            if (string.IsNullOrWhiteSpace(block))
            {
                return new List<string>();
            }

            return block.Split('\n').Select(l => l.Trim().TrimStart('-', '•', '*').Trim()).Where(l => l.Length > 0).ToList();
        }

        /// <summary>The reply as the model sent it, indented when it is one JSON object; null when there was none.</summary>
        internal static string ReplyText(DecisionLogLine line)
        {
            if (string.IsNullOrEmpty(line.Reply))
            {
                return null;
            }

            try
            {
                return Newtonsoft.Json.Linq.JToken.Parse(line.Reply).ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return line.Reply;
            }
        }

        /// <summary>
        /// Section ⑥, one entry per field (#172 ⑥): a new <see cref="DecisionTelemetry"/> field the
        /// log carries needs one line here. A field that gives null is left out for that decision.
        /// </summary>
        internal static readonly (string Label, Func<DecisionLogRow, string> Value)[] TelemetryFields =
        {
            ("Latency", r => Seconds(r.Line.LatencySeconds)),
            ("Queue wait", r => Seconds(r.Line.QueueWaitSeconds) + (r.Line.QueuedAhead > 0 ? $" · {r.Line.QueuedAhead} ahead" : "")),
            ("Backlog wait", r => r.Line.BacklogWaitSeconds > 0 ? Seconds(r.Line.BacklogWaitSeconds) : null),
            ("Tokens", r => $"{r.Line.PromptTokens} in · {r.Line.CompletionTokens} out"),
            ("Prompt size", r => r.Line.PromptCharacters > 0 ? $"{r.Line.PromptCharacters} characters · about {r.Line.PromptTokensEstimated} tokens" : null),
            ("Prompt budget", r => r.Line.PromptBudget.HasValue ? $"{r.Line.PromptBudget} tokens{(r.Line.OverBudget == true ? " · over" : "")}" : null),
            ("Prompt trims", r => Join(r.Line.PromptTrims)),
            ("Provider", r => r.Line.Provider),
            ("Provider config", r => (string.IsNullOrEmpty(r.Line.ProviderConfig) ? "none" : r.Line.ProviderConfig) + (r.ProviderSource != null ? " · " + r.ProviderSource : "")),
            ("Model", r => r.Line.ModelName),
            ("Framework", r => r.Line.FrameworkVersion),
            ("Priority", r => r.Line.Priority.ToString()),
            ("Memory turns", r => $"{r.Line.HistoryTurnsSent} sent · {r.Line.HistoryTurnsDropped} dropped"),
            ("Targets dropped", r => Join(r.Line.TargetsDropped)),
            ("Targets unlisted", r => Join(r.Line.TargetsUnlisted)),
            ("Targets unavailable", r => r.Line.TargetsUnavailable != null && r.Line.TargetsUnavailable.Count > 0 ? string.Join(", ", r.Line.TargetsUnavailable.Select(t => $"{t.Action} · {t.Target}")) : null),
            ("Observations dropped", r => r.Line.ObservationsDropped > 0 ? r.Line.ObservationsDropped.ToString(CultureInfo.InvariantCulture) : "0"),
            ("Target ignored", r => r.Line.TargetIgnored),
            ("Arguments defaulted", r => Join(r.Line.ArgumentsDefaulted)),
            ("Field order", r => r.Line.OrderLost == true ? "lost: the reply's fields came out of the schema's order" : null),
            ("Chain", r => r.Line.ChainId != null ? $"{r.Line.ChainId} · hop {r.Line.ChainHop}{(r.Line.ChainSource != null ? " from " + r.Line.ChainSource : "")}" : null),
            ("Sequence", r => r.Line.Sequence.ToString(CultureInfo.InvariantCulture)),
        };

        /// <summary>Section ⑥'s label and value pairs for <paramref name="row"/>, empty values left out.</summary>
        internal static List<(string Label, string Value)> Telemetry(DecisionLogRow row)
        {
            var shown = new List<(string, string)>();
            foreach (var (label, value) in TelemetryFields)
            {
                var text = value(row);
                if (!string.IsNullOrEmpty(text))
                {
                    shown.Add((label, text));
                }
            }

            return shown;
        }

        /// <summary>One sentence on what came of the decision, for section ⑤'s result.</summary>
        internal static string ResultSentence(DecisionLogLine line)
        {
            if (line.Error != null)
            {
                return $"No decision: {line.Error.Kind}. {line.Error.Message}";
            }

            var move = DecisionLogRow.Move(line.Decision);
            if (line.Verdict != null)
            {
                return line.Verdict.Kind == GuardVerdictKind.Reject
                    ? $"Nothing ran: {line.Verdict.Guard} rejected the model's choice. Rejecting means the model broke the contract, so check the provider."
                    : $"Nothing ran: {line.Verdict.Guard} rewrote the model's choice to none. This is the safety layer working, not a bug.";
            }

            switch (line.Execution?.Status)
            {
                case ExecutionStatus.Done: return $"{move} ran.";
                case ExecutionStatus.Failed: return $"{move} ran, and its handler reported it could not: {line.Execution.Reason}";
                case ExecutionStatus.NoAction: return "none: the model chose to do nothing.";
                case ExecutionStatus.Refused: return $"{move} was refused when executed: {line.Execution.Reason}";
                default:
                    return line.Decision.Action == ActionRegistry.None
                        ? "none: the model chose to do nothing."
                        : $"{move} was decided, but nothing has executed it yet. The game calls Agent.Execute to run it.";
            }
        }

        private static string Seconds(float seconds) => seconds.ToString("0.00", CultureInfo.InvariantCulture) + " s";

        private static string Percent(float share) => Math.Round(share * 100f).ToString(CultureInfo.InvariantCulture) + "%";

        private static string Join(List<string> items) => items != null && items.Count > 0 ? string.Join(", ", items) : null;
    }
}
