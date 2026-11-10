using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using Demos.Shared;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// One agent's mind: every moment the game asks it something comes through here. The player's
    /// typed line, a trespass it noticed, a wolf it spotted, the bell it heard, a quiet moment. Each
    /// becomes a stimulus with its own label and priority, the decision is executed through the
    /// framework's own check, and the whole exchange is recorded for the board and the History panel.
    /// </summary>
    /// <remarks>
    /// The agent itself is the <see cref="AgentBehaviour"/> on the same GameObject, built from its
    /// profile with the village's provider config; every agent on that config shares one provider, so
    /// one queue (DR-013, DR-015). This mind adds what the game knows: the body's guards and refusal
    /// lines, who is asking, and the observations. One decision per agent runs at a time: a player line
    /// sent while it is deciding is turned away, and an event waits, the latest replacing any older one.
    /// Other agents are not held up; their requests wait in the provider's queue instead.
    ///
    /// The scene builder adds both components. F1 keeps them, and the agent too: it resets the agent
    /// (<see cref="UseProvider"/>, #190 G12) rather than replacing the component as it did before.
    /// </remarks>
    public sealed class AgentMind : MonoBehaviour
    {
        /// <summary>
        /// What the game knows about one call, which only its own checks read: who is asking, and how
        /// far a wolf or a running thief the agent can see is. Sent as the call's
        /// <see cref="DecideOptions.Correlation"/>, so the bodies' availability checks see it while
        /// the call decides, and <see cref="Agent.Execute(DecisionResult)"/>'s re-check sees the same
        /// call's values, whatever another call on the agent set since (DR-018). It never reaches the
        /// model, which reads the observations instead.
        /// </summary>
        public sealed class Moment
        {
            /// <summary>The source of the stimulus, such as "player", "zone", "wolf", "bell", "quiet", "captain" or "merchant".</summary>
            public readonly string AskedBy;
            /// <summary>How far away a wolf the agent can see is, in metres; null when it sees none.</summary>
            public readonly float? WolfMetres;
            /// <summary>How far away a running thief the agent can see is, in metres; null when it sees none.</summary>
            public readonly float? ThiefMetres;

            public Moment(string askedBy, float? wolfMetres = null, float? thiefMetres = null)
            {
                AskedBy = askedBy;
                WolfMetres = wolfMetres;
                ThiefMetres = thiefMetres;
            }

            /// <summary>The moment a context was made for; null for a call the game did not make through a mind.</summary>
            public static Moment Of(AgentContext ctx) => ctx?.Correlation as Moment;
        }

        public VillageCommands Village;
        public VillageBody Body;
        public GuardSenses Senses;
        [Tooltip("The example the framework shows for the none action.")]
        public string IdleExampleStimulus = "Fine day for it.";

        public AgentBehaviour Behaviour => behaviour != null ? behaviour : behaviour = GetComponent<AgentBehaviour>();
        public string Name => Behaviour.Profile != null && !string.IsNullOrEmpty(Behaviour.Profile.Identity.Name) ? Behaviour.Profile.Identity.Name : name;
        public string Role => Behaviour.Profile != null ? Behaviour.Profile.Identity.Role : "";
        /// <summary>The role the board and the dialogue panel show: the body's duty while it has one (the night watch), else <see cref="Role"/>.</summary>
        public string RoleNow => Body != null && Body.Duty != null ? Body.Duty : Role;
        public bool IsDeciding { get; private set; }
        /// <summary>When the last decision finished, in game time. The quiet timer counts from here.</summary>
        public float LastActivityTime { get; private set; }
        /// <summary>This agent's latest decision, finished or not; null before the first.</summary>
        public DecisionRecord Last { get; private set; }
        /// <summary>True while an event waits for this agent's running decision to finish.</summary>
        public bool HasPending => pending != null;
        /// <summary>The conversation the dialogue panel shows: the player's lines, the agent's, and what it did.</summary>
        public readonly List<DialogueLine> Conversation = new List<DialogueLine>();

        private AgentBehaviour behaviour;
        private Agent configured;
        private PendingEvent pending;

        private sealed class PendingEvent
        {
            public string Source, Label, Text;
            public DecisionPriority Priority;
        }

        /// <summary>
        /// The agent its <see cref="AgentBehaviour"/> built, with this body's guards and refusal lines
        /// added the first time it is read.
        /// </summary>
        public Agent Agent
        {
            get
            {
                var agent = Behaviour.Agent;
                if (agent != configured) Configure(agent);
                return agent;
            }
        }

        private void Configure(Agent agent)
        {
            configured = agent;
            Body.Player = Village.Player;
            Body.Quests = Village.Quests;
            Body.Inventory = Village.Inventory;
            foreach (var guard in Body.Guards()) agent.Guards.Add(guard);
            foreach (var source in Body.TargetSources()) agent.TargetSources.Add(source);
            agent.RefusalStatement = (verdict, ctx) => Body.RefusalLine(verdict);
            Village.Watch(agent.Provider);
            LastActivityTime = Time.time;
        }

        /// <summary>
        /// Gives the agent a new provider config and starts it afresh, with an empty memory: the
        /// presenter's F1, and how tests give it a scripted provider. The agent itself is kept, with
        /// its guards, sources and settings; <see cref="AgentBehaviour.ResetAgent"/> (#190, G12)
        /// cancels what it still has queued or with the model, so the shared slot is free at once, and
        /// clears its memory. Before the agent is built there is nothing to reset: it is built from
        /// this config on its first read.
        /// </summary>
        public void UseProvider(ProviderConfig config)
        {
            var b = Behaviour;
            b.Provider = config;
            pending = null;
            if (b.HasAgent)
            {
                var agent = b.Agent;
                // Resolved as the component resolves it when it builds the agent.
                agent.Provider = ProviderConfig.Resolve(config).Provider;
                b.ResetAgent();
                // The new provider's health, from the next decision on.
                if (configured == agent) Village.Watch(agent.Provider);
            }
            Conversation.Clear();
            IsDeciding = false;
            Last = null;
            // A fresh start begins its quiet timer now, not from the last decision: a guard that was
            // busy or idle past its quiet time is not asked about a quiet moment at once.
            LastActivityTime = Time.time;
        }

        // ---- What the agent knows ------------------------------------------------------------

        /// <summary>
        /// The facts sent with every request, all derived from the scene and the body's state. A
        /// local model never reads the schema, so the places it can name are listed here by id.
        /// </summary>
        public List<string> Observations()
        {
            bool night = Village.Day != null && Village.Day.IsNight;
            var facts = new List<string> { night ? "It is night." : "It is daytime." };
            // Nothing in clear weather; in rain and fog, how far anyone can see.
            string weather = Village.Weather != null ? Village.Weather.Observation : null;
            if (weather != null) facts.Add(weather);
            facts.Add("You are " + Body.Activity + ".");
            facts.AddRange(Body.Facts());

            var player = Village.Player;
            float toPlayer = Vector3.Distance(player.position, transform.position);
            facts.Add("The player is " + Metres(toPlayer) + " away.");
            if (Senses != null && !Senses.CanSee(player)) facts.Add("You cannot see the player right now.");

            var zones = FindObjectsByType<RestrictedZone>(FindObjectsSortMode.None).OrderBy(z => z.Id).ToList();
            foreach (var zone in zones.Where(z => z.PlayerInside))
                facts.Add("The player is inside the " + zone.DisplayName + ", which is off limits.");
            if (zones.Count > 0)
                facts.Add("Off limits to the player: " + string.Join(", ", zones.Select(z => z.Id)) + ".");

            float? wolf = VisibleWolfDistance();
            if (wolf != null) facts.Add("A wolf is " + Metres(wolf.Value) + " from you.");
            float? thief = VisibleThiefDistance();
            if (thief != null) facts.Add("A thief is running for the eastern_road, " + Metres(thief.Value) + " from you.");
            // Who is where in the village, so the model sees the day go by.
            string crowd = Village.Routine != null ? Village.Routine.Describe() : null;
            if (crowd != null) facts.Add(crowd);

            facts.AddRange(Places());
            return facts;
        }

        // The Targetables the agent's own target source offers, nearest first, as "id, distance: description".
        // As the framework offers them: the source's own categories, or the agent's when it names none,
        // before its cap; then the agent's, which hold for every source.
        private IEnumerable<string> Places()
        {
            var origin = transform.position;
            var source = Behaviour.Targets;
            var agent = Agent.TargetCategories;
            var own = source.Categories != null && source.Categories.Any(c => !string.IsNullOrWhiteSpace(c))
                ? (IReadOnlyCollection<string>)source.Categories
                : agent;
            return FindObjectsByType<Targetable>(FindObjectsSortMode.None)
                .Where(t => t.enabled && t.IsCurrentlyTargetable && !t.transform.IsChildOf(transform))
                .Where(t => own.Count == 0 || own.Contains(t.Category))
                .Select(t => (t, d: Vector3.Distance(origin, t.transform.position)))
                .Where(x => x.d <= source.Radius)
                .OrderBy(x => x.d).ThenBy(x => x.t.Id, StringComparer.Ordinal)
                .Take(source.Cap)
                .Where(x => agent.Count == 0 || agent.Contains(x.t.Category))
                .Select(x => x.t.Id + ", " + Metres(x.d) + " away: " + x.t.Description);
        }

        public float? VisibleWolfDistance()
        {
            var wolf = FindFirstObjectByType<VillageWolf>();
            if (wolf == null || wolf.IsHidden || wolf.IsFleeing || Senses == null || !Senses.CanSee(wolf.transform)) return null;
            return Vector3.Distance(wolf.transform.position, transform.position);
        }

        /// <summary>How far away a thief running with stolen goods is, if this agent can see it; null otherwise.</summary>
        public float? VisibleThiefDistance()
        {
            var thief = FindFirstObjectByType<VillageThief>();
            if (thief == null || !thief.IsRunning || Senses == null || !Senses.CanSee(thief.transform)) return null;
            return Vector3.Distance(thief.transform.position, transform.position);
        }

        private static string Metres(float d) => d.ToString("0", CultureInfo.InvariantCulture) + " m";

        // ---- The moments the game asks -------------------------------------------------------

        /// <summary>The player's typed line. Completes when the decision has been executed.</summary>
        public async Awaitable HearPlayerAsync(string text)
        {
            if (IsDeciding)
            {
                Village.Overlay.ShowNote(Name + " is still answering. Say it again in a moment.");
                return;
            }
            Log("You", text, DialogueLine.Kinds.Player);
            await DecideAsync("player", "Player", text, DecisionPriority.PlayerFacing);
        }

        private void Log(string speaker, string text, DialogueLine.Kinds kind)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Conversation.Add(new DialogueLine(speaker, text, kind));
            if (Conversation.Count > 30) Conversation.RemoveAt(0);
        }

        /// <summary>
        /// Something the agent noticed. Waits its turn when a decision is already running, and is
        /// dropped while the model is down (<see cref="VillageCommands.MayAskOnItsOwn"/>).
        /// </summary>
        public async Awaitable ReportEventAsync(string source, string label, string text, DecisionPriority priority)
        {
            if (!Village.MayAskOnItsOwn)
            {
                // Nobody can answer it. The quiet timer counts from now, so it does not ask at once.
                LastActivityTime = Time.time;
                return;
            }
            if (IsDeciding)
            {
                pending = new PendingEvent { Source = source, Label = label, Text = text, Priority = priority };
                return;
            }
            await DecideAsync(source, label, text, priority);
        }

        private async Awaitable DecideAsync(string source, string label, string text, DecisionPriority priority)
        {
            // A mind destroyed while an event waited for it asks nothing more.
            if (this == null) return;
            Agent agent;
            try { agent = Agent; }
            catch (InvalidOperationException e)
            {
                // No agent can be built, such as from a provider config that is not valid: say so, and stay playable.
                Village.ReportAgentProblem("No agent could be built: " + e.Message);
                Body.Say("No answer: there is no model to ask.", SpeechKind.Error);
                LastActivityTime = Time.time;
                pending = null;
                return;
            }
            IsDeciding = true;
            // Who is asking decides what the agent is offered (its body's availability checks). The
            // moment rides with the call, so Execute's re-check reads this call's values.
            Body.Attend(source);
            var moment = new Moment(source, VisibleWolfDistance(), VisibleThiefDistance());
            string asked = Name + " ← " + label + ": " + text;
            Village.Overlay.ShowThinking(asked, Village.ProviderLabel, Name + " is thinking...");
            var record = Village.Recorder.Begin(Name, source, label, priority, text);
            Last = record;
            bool gone = false, reset = false;
            // Destroying the mind cancels the call in Play mode; in Edit mode nothing does.
            var own = Application.isPlaying ? destroyCancellationToken : CancellationToken.None;
            try
            {
                var options = new DecideOptions
                {
                    StimulusLabel = label,
                    IdleExampleStimulus = IdleExampleStimulus,
                    Observations = Observations(),
                    Priority = priority,
                    Correlation = moment
                };
                var result = await agent.DecideAsync(text, options, own);
                // Destroyed while the model thought, in Edit mode, where nothing cancels the call.
                if (this == null) { gone = true; return; }
                Village.Recorder.Attach(record, result);
                // A decision that says nothing, or fails, must not leave the last one's line standing.
                Body.Hush();
                if (result.Decision == null) ShowError(record, result, asked);
                else Settle(agent, record, result, asked);
            }
            catch (OperationCanceledException) when (!own.IsCancellationRequested)
            {
                // The agent was reset (F1, UseProvider), which cancels what it had under way, whether
                // or not a provider still answers it. The reset set this mind's state afresh, and a
                // decision may already have begun after it, so this one leaves the state alone.
                reset = true;
            }
            catch (OperationCanceledException)
            {
                // Destroyed in Play mode, which cancels the call: nothing to show, and nothing more to ask.
                gone = true;
            }
            finally
            {
                if (!reset)
                {
                    IsDeciding = false;
                    LastActivityTime = Time.time;
                    if (gone) pending = null;
                }
            }

            // An event that came in meanwhile is asked now, whatever became of this decision, unless
            // the model is down and nobody could answer it.
            if (!gone && !reset && pending != null)
            {
                var next = pending;
                pending = null;
                if (Village.MayAskOnItsOwn) await DecideAsync(next.Source, next.Label, next.Text, next.Priority);
            }
        }

        private void ShowError(DecisionRecord record, DecisionResult result, string asked)
        {
            var t = result.Telemetry;
            string error = "No decision: " + t.ErrorKind + ". " + t.ErrorMessage;
            Village.Recorder.Complete(record, result, "error", error);
            // Said where the player is looking, so a failure never looks like an agent thinking
            // forever, and the same words in the dialogue log. The error kind is in the recorder.
            string why = Village.ModelProblem != null ? "no model is answering"
                : t.ErrorKind == PipelineErrorKind.UnreadableReply ? "the reply could not be read"
                : "something went wrong (" + t.ErrorKind + ")";
            Log(Name, "(no answer: " + why + ")", DialogueLine.Kinds.Note);
            Body.Say("No answer: " + why + ".", SpeechKind.Error);
            Village.Overlay.ShowDecision(asked, "none", "no_target", "", error,
                t.LatencySeconds, t.GuardsFired, Village.ProviderLabel);
        }

        private void Settle(Agent agent, DecisionRecord record, DecisionResult result, string asked)
        {
            var t = result.Telemetry;
            // What the model answered. A guard that stopped it inside DecideAsync left
            // result.Decision as none, with the refusal line as its statement, so the outcome
            // comes from the verdict, not from that.
            var d = result.ModelDecision;
            bool executed = agent.Execute(result);
            string outcome = Outcome(result, executed);
            string reason = outcome == "refused" || outcome == "contained" ? StopReason(record, result, outcome) : "";
            string guard = t.GuardsFired.Count > 0 ? t.GuardsFired[t.GuardsFired.Count - 1] : null;
            string said = "";
            if (outcome == "executed" || outcome == "none")
            {
                said = d.Statement;
                Body.Say(said);
            }
            else if (result.Verdict != null && !string.IsNullOrWhiteSpace(result.Decision.Statement))
            {
                // Only a guard inside DecideAsync supplies a line; Execute's re-check never does.
                said = result.Decision.Statement;
                Body.Say(said, SpeechKind.Refusal, guard);
            }
            Village.Recorder.Complete(record, result, outcome, reason, said);
            if (record.Source != "player") Log(record.Label, record.Stimulus, DialogueLine.Kinds.Note);
            Log(Name, said, outcome == "executed" || outcome == "none" ? DialogueLine.Kinds.Agent : DialogueLine.Kinds.Refusal);
            if (outcome == "executed")
            {
                Log(Name, "(" + Body.Activity + ")", DialogueLine.Kinds.Note);
                Body.AfterAction(d.ActionId, d.Statement);
            }

            string details = string.Format(CultureInfo.InvariantCulture,
                "{0} / {1}  |  {2:0.00} s, queued {3:0.00} s  |  tokens {4} in, {5} out  |  history sent {6}{7}",
                t.ProviderName, t.Model, t.LatencySeconds, t.QueueWaitSeconds, t.PromptTokens,
                t.CompletionTokens, t.HistoryTurnsSent, reason == "" ? "" : "  |  " + outcome.ToUpperInvariant() + ": " + reason);
            Village.Overlay.ShowDecision(asked, d.ActionId, d.TargetId, said, details,
                t.LatencySeconds, t.GuardsFired, Village.ProviderLabel);
        }

        // refused: a guard rejected the answer inside DecideAsync (the provider broke the contract),
        // or Execute's re-check did. contained: a guard judged a legal answer wrong and made it none.
        private static string Outcome(DecisionResult result, bool executed)
        {
            if (!executed) return "refused";
            if (result.Verdict != null) return "contained";
            return result.ModelDecision.ActionId == ActionRegistry.None ? "none" : "executed";
        }

        // The game's own reading of a refusal, from what the request offered, then the words of the
        // guard that stopped it: the last in GuardsFired, which is Execute's re-check when it refused.
        private static string StopReason(DecisionRecord record, DecisionResult result, string outcome)
        {
            var d = result.ModelDecision;
            var t = result.Telemetry;
            string guard = t.GuardsFired.Count > 0 ? t.GuardsFired[t.GuardsFired.Count - 1] : "a guard";
            string said = guard + " said: " + t.GuardReason;
            if (outcome == "contained") return said;
            if (!record.OfferedActions.Contains(d.ActionId)) return "the action was not offered; " + said;
            if (d.TargetId != TargetRegistry.NoTarget && !record.OfferedTargets.Contains(d.TargetId)) return "the target was not offered; " + said;
            return "the action or target stopped being available before it ran; " + said;
        }
    }
}
