using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>How a decision ended, as the inspector's badge says it.</summary>
    public enum CardOutcome
    {
        /// <summary>Carried out.</summary>
        Done,
        /// <summary>The agent held: it chose <c>none</c>, or answered an envoy.</summary>
        Held,
        /// <summary>A guard stopped what the model wrote, and the agent ran <c>none</c>.</summary>
        Contained,
        /// <summary>The game's re-check refused it when it landed: an earlier move made it stale.</summary>
        RefusedAtResolution,
        /// <summary><c>Agent.Execute</c> refused it.</summary>
        RefusedByAgent,
    }

    /// <summary>One guard's line on the card.</summary>
    public sealed class CardGuard
    {
        public string Name;
        /// <summary>"passed", "contained", "not scored", "refused", "done"...</summary>
        public string State;
        public bool Good;
        public string Detail;
    }

    /// <summary>
    /// The decision inspector's content for one decision: the framework's window on this screen.
    /// Every value comes from the recorded decision (the decision feed's record) and the game's
    /// own event; the card adds nothing but wording. Plain C#, so it is tested without a screen.
    /// </summary>
    public sealed class DecisionCard
    {
        public const float GroundingThreshold = 0.6f;

        public string Country;
        public int Turn;
        public string Moment;
        public string ChainId;
        public int ChainHop;
        public CardOutcome Outcome;
        public string OutcomeText;

        /// <summary>1. The model's answer, as it wrote it: action, then target, then statement.</summary>
        public string AnswerJson;
        public string Action, Target, Statement;
        /// <summary>What the model wrote before a guard changed it, or null.</summary>
        public string ModelWrote;

        /// <summary>2. The moves offered, and those not offered with the reason for each.</summary>
        public List<string> Offered = new List<string>();
        public int Picked = -1;
        public List<(string move, string why)> NotOffered = new List<(string, string)>();

        /// <summary>3. What it was told: standing facts, the stimulus and this turn's facts.</summary>
        public List<string> Standing = new List<string>();
        public List<string> ThisTurn = new List<string>();
        public int ObservationsDropped;

        /// <summary>4. Guards and the re-check when the move landed.</summary>
        public List<CardGuard> Guards = new List<CardGuard>();
        public float? Grounding;

        /// <summary>5. Memory.</summary>
        public int MemorySent, MemoryDropped;

        /// <summary>6. Model and time.</summary>
        public string Model;
        public float LatencySeconds, QueueWaitSeconds;
        public int QueuedAhead, PromptTokens, CompletionTokens;

        public static DecisionCard From(ReplayEvent e)
        {
            var d = e.Decision ?? throw new ArgumentException("This event has no decision behind it.");
            var c = new DecisionCard
            {
                Country = d.IsMove ? d.Country : d.AgentId, Turn = d.Turn,
                // The writs turn has two agents a country: the marshal orders an army, the court the rest.
                Moment = d.ArmyId != null ? $"marshal, ordering {Chronicle.Host(d.ArmyId)}" : d.IsEnvoy ? "envoy" : d.IsStory ? "the storyteller's call"
                    : d.IsTitle ? "the title call" : d.IsChoice ? "a dilemma's choice" : "council",
                ChainId = d.ChainId, ChainHop = d.ChainHop,
                Action = d.Action, Target = d.Target ?? RecordedDecision.NoTarget, Statement = d.Statement ?? "",
                Model = d.ModelName, LatencySeconds = d.LatencySeconds, QueueWaitSeconds = d.QueueWaitSeconds,
                QueuedAhead = d.QueuedAhead, PromptTokens = d.PromptTokens, CompletionTokens = d.CompletionTokens,
                MemorySent = d.HistoryTurnsSent, MemoryDropped = d.HistoryTurnsDropped, ObservationsDropped = d.ObservationsDropped,
                Grounding = d.TargetGrounding,
            };
            string reply = (string)d.Raw?["reply"];
            c.AnswerJson = !string.IsNullOrWhiteSpace(reply) ? reply.Trim() : "{\n  \"action\": \"" + c.Action + "\",\n  \"target\": \"" + c.Target + "\",\n  \"statement\": \"" + c.Statement.Replace("\"", "\\\"") + "\"\n}";
            if (d.ModelAction != null && (d.ModelAction != d.Action || d.ModelTarget != d.Target))
                c.ModelWrote = d.ModelAction + (string.IsNullOrEmpty(d.ModelTarget) || d.ModelTarget == RecordedDecision.NoTarget ? "" : " " + d.ModelTarget);

            // 2. the menu
            c.Offered = d.OfferedMoves.Count > 0 ? d.OfferedMoves.ToList() : d.OfferedActions.ToList();
            string chosen = d.HasTarget ? d.Action + " " + d.Target : d.Action;
            c.Picked = c.Offered.IndexOf(chosen);
            if (c.Picked < 0 && (d.IsEnvoy || !d.IsMove)) c.Picked = c.Offered.IndexOf(d.Action);
            c.NotOffered = d.TargetsUnavailable.Select(u => ((u.Action + " " + u.Target).Trim(), u.Reason)).ToList();

            // 3. what it was told
            foreach (string line in (d.Observations ?? "").Split('\n'))
            {
                string t = line.Trim();
                if (t.StartsWith("- ")) c.Standing.Add(t.Substring(2));
            }
            foreach (string line in (d.StimulusTurn ?? "").Split('\n'))
                if (!string.IsNullOrWhiteSpace(line)) c.ThisTurn.Add(line.Trim());

            // 4. guards
            bool contained = d.GuardsFired.Count > 0 || d.Verdict != null;
            c.Guards.Add(new CardGuard
            {
                Name = "SchemaLegalityGuard",
                State = d.Verdict?.Guard == "SchemaLegalityGuard" ? "stopped it" : "passed",
                Good = d.Verdict?.Guard != "SchemaLegalityGuard",
                Detail = d.Verdict?.Guard == "SchemaLegalityGuard" ? d.Verdict.Reason : "The action and target are on the offered list.",
            });
            if (d.TargetGroundingStatus == "Scored" && d.TargetGrounding.HasValue)
            {
                bool stopped = d.Verdict?.Guard == "TargetGroundingGuard";
                c.Guards.Add(new CardGuard
                {
                    Name = "TargetGroundingGuard",
                    State = stopped ? "contained" : "passed",
                    Good = !stopped,
                    Detail = string.Format(CultureInfo.InvariantCulture,
                        "{0:0}% of the model's probability at the target stayed on the target list (threshold {1:0}%).",
                        d.TargetGrounding.Value * 100, GroundingThreshold * 100) + (stopped ? " " + d.Verdict.Reason : ""),
                });
            }
            else
                c.Guards.Add(new CardGuard { Name = "TargetGroundingGuard", State = "not scored", Good = true,
                    Detail = d.TargetGroundingStatus == null ? "Not reached." : "No target to ground (" + d.TargetGroundingStatus + ")." });

            if (e.Kind == ReplayEventKind.Refused && d.ExecutionStatus != "Refused")
            {
                c.Outcome = CardOutcome.RefusedAtResolution;
                c.OutcomeText = "Refused at resolution";
                c.Guards.Add(new CardGuard { Name = "The game's re-check", State = "refused", Good = false,
                    Detail = "When the move came to land, an earlier move had made it stale: “" + e.Text.Replace("refused: ", "") + "”. It never reached Agent.Execute." });
            }
            else if (d.ExecutionStatus == "Refused")
            {
                c.Outcome = CardOutcome.RefusedByAgent;
                c.OutcomeText = "Refused by the agent";
                c.Guards.Add(new CardGuard { Name = "Agent.Execute", State = "refused", Good = false, Detail = "“" + d.ExecutionReason + "”" });
            }
            else
            {
                c.Outcome = contained ? CardOutcome.Contained : d.ExecutionStatus == "NoAction" || d.IsEnvoy || d.IsTitle || d.IsChoice ? CardOutcome.Held : CardOutcome.Done;
                c.OutcomeText = contained ? "Contained by a guard" : c.Outcome == CardOutcome.Held ? (d.IsEnvoy || d.IsChoice ? "Answered" : d.IsTitle ? "Titled" : "Held") : d.IsStory ? "Queued" : "Done";
                c.Guards.Add(new CardGuard { Name = "Agent.Execute", State = (d.ExecutionStatus ?? "not reached").ToLowerInvariant(), Good = true,
                    Detail = d.ExecutionStatus == null ? "The game did not call Execute for it." : "Re-checked with the same rule that offered it, then carried out." });
            }
            return c;
        }
    }
}
