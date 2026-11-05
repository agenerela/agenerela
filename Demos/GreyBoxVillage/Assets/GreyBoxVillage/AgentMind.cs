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
    /// Not <c>[RequireComponent(typeof(AgentBehaviour))]</c>: Unity refuses to remove a required
    /// component, and <see cref="UseProvider"/> has to replace it. The scene builder adds both.
    /// </remarks>
    public sealed class AgentMind : MonoBehaviour
    {
        /// <summary>State key: the source of the stimulus being decided, such as "player", "zone", "wolf", "bell" or "quiet".</summary>
        public const string AskedBy = "askedBy";
        /// <summary>State key: how far away a wolf the agent can see is, in metres. Absent when it sees none.</summary>
        public const string WolfMetres = "wolfMetres";

        public VillageCommands Village;
        public VillageBody Body;
        public GuardSenses Senses;
        [Tooltip("The example the framework shows for the none action.")]
        public string IdleExampleStimulus = "Fine day for it.";

        public AgentBehaviour Behaviour => behaviour != null ? behaviour : behaviour = GetComponent<AgentBehaviour>();
        public string Name => Behaviour.Profile != null && !string.IsNullOrEmpty(Behaviour.Profile.Identity.Name) ? Behaviour.Profile.Identity.Name : name;
        public string Role => Behaviour.Profile != null ? Behaviour.Profile.Identity.Role : "";
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
            LastActivityTime = Time.time;
        }

        /// <summary>
        /// Gives the agent a new provider config, and so a fresh agent with an empty memory. An
        /// <see cref="AgentBehaviour"/> builds its agent once, so this replaces the component with one
        /// that has the same settings. Tests pass a config with a fake provider.
        /// </summary>
        public void UseProvider(ProviderConfig config)
        {
            var old = Behaviour;
            var profile = old.Profile;
            bool findTargets = old.FindTargetsNearby, notice = old.NoticeNearby;
            var targets = old.Targets;
            var observations = old.Observations;
            DestroyImmediate(old);
            behaviour = gameObject.AddComponent<AgentBehaviour>();
            behaviour.Provider = config;
            behaviour.FindTargetsNearby = findTargets;
            behaviour.Targets = targets;
            behaviour.NoticeNearby = notice;
            behaviour.Observations = observations;
            // Last: an AgentBehaviour with a profile builds its agent on the first read.
            behaviour.Profile = profile;
            configured = null;
            pending = null;
            Conversation.Clear();
            IsDeciding = false;
            Last = null;
        }

        // ---- What the agent knows ------------------------------------------------------------

        /// <summary>
        /// The facts sent with every request, all derived from the scene and the body's state. A
        /// local model never reads the schema, so the places it can name are listed here by id.
        /// </summary>
        public List<string> Observations()
        {
            var facts = new List<string> { "It is daytime.", "You are " + Body.Activity + "." };
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

            facts.AddRange(Places());
            return facts;
        }

        // The Targetables the agent's own target source offers, nearest first, as "id, distance: description".
        private IEnumerable<string> Places()
        {
            var origin = transform.position;
            var source = Behaviour.Targets;
            return FindObjectsByType<Targetable>(FindObjectsSortMode.None)
                .Where(t => t.enabled && t.IsCurrentlyTargetable && !t.transform.IsChildOf(transform))
                .Where(t => source.Categories == null || source.Categories.Length == 0 || source.Categories.Contains(t.Category))
                .Select(t => (t, d: Vector3.Distance(origin, t.transform.position)))
                .Where(x => x.d <= source.Radius)
                .OrderBy(x => x.d).ThenBy(x => x.t.Id, StringComparer.Ordinal)
                .Take(source.Cap)
                .Select(x => x.t.Id + ", " + Metres(x.d) + " away: " + x.t.Description);
        }

        public float? VisibleWolfDistance()
        {
            var wolf = FindFirstObjectByType<VillageWolf>();
            if (wolf == null || wolf.IsHidden || Senses == null || !Senses.CanSee(wolf.transform)) return null;
            return Vector3.Distance(wolf.transform.position, transform.position);
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
                Village.ReportModel("No agent could be built: " + e.Message);
                Body.Say("No answer: there is no model to ask.", SpeechKind.Error);
                LastActivityTime = Time.time;
                pending = null;
                return;
            }
            IsDeciding = true;
            // Who is asking decides what the agent is offered (its body's availability checks). It
            // stays set until the next decision, because Execute checks availability again.
            Body.Attend(source);
            agent.State[AskedBy] = source;
            float? wolf = VisibleWolfDistance();
            if (wolf != null) agent.State[WolfMetres] = wolf.Value;
            else agent.State.Remove(WolfMetres);
            string asked = Name + " ← " + label + ": " + text;
            Village.Overlay.ShowThinking(asked, Village.ProviderLabel, Name + " is thinking...");
            var record = Village.Recorder.Begin(Name, source, label, priority, text);
            Last = record;
            bool gone = false;
            try
            {
                var options = new DecideOptions
                {
                    StimulusLabel = label,
                    IdleExampleStimulus = IdleExampleStimulus,
                    Observations = Observations(),
                    Priority = priority
                };
                var token = Application.isPlaying ? destroyCancellationToken : CancellationToken.None;
                var result = await agent.DecideAsync(text, options, token);
                // Destroyed while the model thought (in Edit mode nothing cancels the call).
                if (this == null) { gone = true; return; }
                Village.Recorder.Attach(record, result);
                Village.ReportModel(result);
                // A decision that says nothing, or fails, must not leave the last one's line standing.
                Body.Hush();
                if (result.Decision == null) ShowError(record, result, asked);
                else Settle(agent, record, result, asked);
            }
            catch (OperationCanceledException)
            {
                // Destroyed in Play mode, which cancels the call: nothing to show, and nothing more to ask.
                gone = true;
            }
            finally
            {
                IsDeciding = false;
                LastActivityTime = Time.time;
                if (gone) pending = null;
            }

            // An event that came in meanwhile is asked now, whatever became of this decision, unless
            // the model is down and nobody could answer it.
            if (!gone && pending != null)
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
            Log(Name, "(no answer: " + t.ErrorKind + ")", DialogueLine.Kinds.Note);
            // Shown where the player is looking, so a failure never looks like an agent thinking forever.
            Body.Say(Village.ModelProblem != null ? "No answer: no model is answering."
                : t.ErrorKind == PipelineErrorKind.UnreadableReply ? "No answer: the reply could not be read."
                : "No answer (" + t.ErrorKind + ").", SpeechKind.Error);
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
