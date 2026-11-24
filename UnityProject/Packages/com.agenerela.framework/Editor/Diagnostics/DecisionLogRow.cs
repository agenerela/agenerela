using System;
using System.Collections.Generic;
using System.Globalization;
using Agenerela.Editor.UI;

namespace Agenerela.Editor.Diagnostics
{
    /// <summary>
    /// One row of the Decision Log window (#172, screen 3 ①): one decision, as the
    /// <see cref="DecisionLogLine"/> the feed's last entry for it made, and the name the window shows
    /// for its agent.
    /// </summary>
    /// <remarks>
    /// A decision raises two feed entries, one when <c>DecideAsync</c> returns and one when
    /// <c>Execute</c> does. The second replaces the first's line on the same row, so a row always
    /// says the latest thing known about its decision, and the window never shows one decision
    /// twice. The line is plain data, the same shape <see cref="DecisionLog"/> writes, so a row keeps
    /// no agent or scene object alive and exports as one line of a log file.
    /// </remarks>
    internal sealed class DecisionLogRow
    {
        internal DecisionLogRow(DecisionLogLine line, string agent, int gameObject = 0, string providerSource = null)
        {
            Line = line ?? throw new ArgumentNullException(nameof(line));
            Agent = string.IsNullOrEmpty(agent) ? NameOf(line) : agent;
            GameObject = gameObject;
            ProviderSource = providerSource;
        }

        /// <summary>The latest line for this decision: the decided one, or its execution once there is one.</summary>
        internal DecisionLogLine Line { get; set; }

        /// <summary>
        /// The agent as the list shows it: its GameObject's name, or its identity's name for an agent
        /// with no GameObject.
        /// </summary>
        internal string Agent { get; }

        /// <summary>The instance id of the agent's GameObject when it had one, else 0: what <b>Select agent in scene</b> selects.</summary>
        internal int GameObject { get; }

        /// <summary>
        /// Which of DR-013's three places the provider config came from, in words, when the window
        /// could tell; null when it could not, such as for a row read back from a file.
        /// </summary>
        internal string ProviderSource { get; }

        /// <summary>Whether the cap or <b>Clear</b> has taken the row out of the log.</summary>
        internal bool Removed { get; set; }

        internal Outcome Outcome => OutcomeOf(Line);

        /// <summary>
        /// What happened to the decision, from the line alone (#172 ③). Not
        /// <see cref="DecisionTelemetry.Outcome"/>, which only the evaluation runner sets.
        /// </summary>
        /// <remarks>
        /// A pipeline error first, since it has no decision. Then a guard: one that stopped the
        /// model's answer inside <c>DecideAsync</c>, or <c>Execute</c>'s re-check refusing it. Then
        /// <c>none</c>, which the model chose, and which needs no executing. Anything else either
        /// ran, a handler that reported a failure included, since it did run, or has not run yet.
        /// </remarks>
        internal static Outcome OutcomeOf(DecisionLogLine line)
        {
            if (line.Error != null || line.Decision == null)
            {
                return Outcome.Error;
            }

            if (line.Verdict != null || line.Execution?.Status == ExecutionStatus.Refused)
            {
                return Outcome.Contained;
            }

            if (line.Decision.Action == ActionRegistry.None)
            {
                return Outcome.NoAction;
            }

            return line.Execution == null ? Outcome.NotExecuted : Outcome.Executed;
        }

        /// <summary>What the agent was asked, as the list shows it: <c>Player: “Go to the tower.”</c>.</summary>
        internal static string AskedText(DecisionLogLine line)
        {
            var stimulus = "“" + (line.Stimulus ?? "") + "”";
            return string.IsNullOrEmpty(line.Label) ? stimulus : line.Label + ": " + stimulus;
        }

        /// <summary>
        /// What came of it, as the list shows it: <c>move_to · tower</c>; with both values when a guard
        /// changed the move, <c>attack_target · training_dummy ⇒ none</c>; <c>error: ProviderFailed</c>
        /// for a pipeline error.
        /// </summary>
        internal static string ResultText(DecisionLogLine line)
        {
            if (line.Decision == null)
            {
                return "error: " + (line.Error != null ? line.Error.Kind.ToString() : "no decision");
            }

            var text = Move(line.Decision);
            var model = line.ModelDecision;
            if (model != null && (model.Action != line.Decision.Action || model.Target != line.Decision.Target))
            {
                text = Move(model) + " ⇒ " + text;
            }

            if (line.Execution?.Status == ExecutionStatus.Failed)
            {
                text += " (handler failed)";
            }
            else if (line.Execution?.Status == ExecutionStatus.Refused && line.Verdict == null)
            {
                text += " (refused at Execute)";
            }

            return text;
        }

        /// <summary>An action and its target, the target left out when it is <c>no_target</c>.</summary>
        internal static string Move(DecisionLogDecision decision)
        {
            if (decision == null)
            {
                return "";
            }

            var action = string.IsNullOrEmpty(decision.Action) ? "?" : decision.Action;
            return string.IsNullOrEmpty(decision.Target) || decision.Target == TargetRegistry.NoTarget
                ? action
                : action + " · " + decision.Target;
        }

        /// <summary>
        /// The provider's time, <c>1.31 s</c>, or <c>n/a</c> for an error, which has no decision to
        /// time: section ⑥ still shows what was measured.
        /// </summary>
        internal static string LatencyText(DecisionLogLine line)
        {
            return line.Error != null
                ? "n/a"
                : line.LatencySeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s";
        }

        /// <summary>The time in the Editor's own clock, <c>14:02:31</c>.</summary>
        internal static string TimeText(DecisionLogLine line)
        {
            return line.Time.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>The agent's name from the line alone: its identity's, then its id.</summary>
        internal static string NameOf(DecisionLogLine line)
        {
            if (!string.IsNullOrEmpty(line.AgentName)) return line.AgentName;
            if (!string.IsNullOrEmpty(line.AgentId)) return line.AgentId;
            return "(unnamed agent)";
        }

        /// <summary>Rows read back from lines, such as an export: each named from its line.</summary>
        internal static List<DecisionLogRow> FromLines(IEnumerable<DecisionLogLine> lines)
        {
            var rows = new List<DecisionLogRow>();
            foreach (var line in lines)
            {
                rows.Add(new DecisionLogRow(line, null));
            }

            return rows;
        }
    }
}
