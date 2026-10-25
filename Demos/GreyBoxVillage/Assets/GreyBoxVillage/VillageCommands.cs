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
    /// The guard's brain: every moment the game asks the agent something comes through here. The
    /// player's typed line, a trespass the guard noticed, a wolf it spotted, a quiet moment at its
    /// post. Each becomes a stimulus with its own label and priority, the decision is executed
    /// through the framework's own check, and the whole exchange is recorded for the History panel.
    /// </summary>
    /// <remarks>
    /// The agent is built in code (DR-011), so the scene waits for neither the provider config asset
    /// (#92) nor <c>AgentBehaviour</c> (#93). One decision runs at a time: a player line sent while
    /// the guard is deciding is turned away, and an event waits, the latest replacing any older one.
    /// </remarks>
    public sealed class VillageCommands : MonoBehaviour
    {
        public DemoCommandInput Input;
        public DecisionDebugOverlay Overlay;
        public Transform Player;
        public VillageGuard Guard;
        public GuardSenses Senses;
        public float SpeakingDistance = 6f;

        [Header("Targets")]
        [Tooltip("The guard can name any Targetable within this distance.")]
        public float TargetRadius = 90f;
        public int TargetCap = 16;

        [Header("Ollama")]
        public string Model = "qwen3.5:2b";
        public string Endpoint = "http://127.0.0.1:11434";
        [Tooltip("Append every decision to Logs/agenerela-decisions-<time>.jsonl.")]
        public bool WriteLogFile = true;

        public Agent Agent { get; private set; }
        public DecisionRecorder Recorder { get; private set; }
        public bool IsDeciding { get; private set; }
        /// <summary>When the last decision finished, in game time. The quiet timer counts from here.</summary>
        public float LastActivityTime { get; private set; }

        private ILLMProvider provider;
        private bool writeLog = true;
        private PendingEvent pending;

        private sealed class PendingEvent
        {
            public string Source, Label, Text;
            public DecisionPriority Priority;
        }

        private void OnEnable() => Input.Submitted += OnSubmitted;
        private void OnDisable() => Input.Submitted -= OnSubmitted;

        /// <summary>Replaces the provider and builds a fresh agent, memory and log included. Tests pass a fake.</summary>
        public void UseProvider(ILLMProvider replacement, bool writeLogFile = false)
        {
            provider = replacement ?? throw new ArgumentNullException(nameof(replacement));
            writeLog = writeLogFile;
            Agent = null;
            pending = null;
        }

        public Agent EnsureAgent()
        {
            if (Agent != null) return Agent;
            provider ??= new OllamaProvider(new OllamaSettings { Model = Model, Endpoint = Endpoint });
            Guard.Player = Player;
            Recorder = new DecisionRecorder(provider, WriteLogFile && writeLog ? DecisionRecorder.DefaultFilePath() : null);

            var identity = new AgentIdentity
            {
                Name = "Bram",
                Role = "the guard of the village",
                Personality = "Brief and dutiful. Speaks in one short sentence.",
                Goals = "Keep the village safe, keep people out of places that are off limits, and help the player."
            };
            Agent = new Agent(identity, Array.Empty<ActionDefinition>(), Recorder);
            Agent.Actions.RegisterMethods(Guard);
            Agent.TargetSources.Add(new ProximityTargetSource { Origin = Guard.transform, Radius = TargetRadius, Cap = TargetCap });
            LastActivityTime = Time.time;
            return Agent;
        }

        public string ProviderLabel => provider == null || provider is OllamaProvider
            ? "ollama / " + Model
            : provider.Name;

        // ---- What the guard knows ------------------------------------------------------------

        /// <summary>
        /// The facts sent with every request, all derived from the scene and the guard's state. A
        /// local model never reads the schema, so the places it can name are listed here by id.
        /// </summary>
        public List<string> Observations()
        {
            var facts = new List<string>
            {
                "It is daytime.",
                "You are " + Guard.Activity + ".",
                Guard.IsFollowing ? "You are following the player." : "You are not following the player."
            };

            float toPlayer = Vector3.Distance(Player.position, Guard.transform.position);
            facts.Add("The player is " + Metres(toPlayer) + " away.");
            if (Senses != null && !Senses.CanSee(Player)) facts.Add("You cannot see the player right now.");

            var zones = FindObjectsByType<RestrictedZone>(FindObjectsSortMode.None).OrderBy(z => z.Id).ToList();
            foreach (var zone in zones.Where(z => z.PlayerInside))
                facts.Add("The player is inside the " + zone.DisplayName + ", which is off limits.");
            if (zones.Count > 0)
                facts.Add("Off limits to the player: " + string.Join(", ", zones.Select(z => z.Id)) + ".");

            if (Guard.Alarm != null && Guard.Alarm.IsSounding) facts.Add("The alarm bell is ringing.");

            var wolf = FindFirstObjectByType<VillageWolf>();
            if (wolf != null && !wolf.IsHidden && Senses != null && Senses.CanSee(wolf.transform))
                facts.Add("A wolf is " + Metres(Vector3.Distance(wolf.transform.position, Guard.transform.position)) + " from you.");

            facts.AddRange(Places());
            return facts;
        }

        // The same Targetables the proximity source registers, nearest first, as "id: description".
        private IEnumerable<string> Places()
        {
            var origin = Guard.transform.position;
            return FindObjectsByType<Targetable>(FindObjectsSortMode.None)
                .Where(t => t.enabled && t.IsCurrentlyTargetable && !t.transform.IsChildOf(Guard.transform))
                .Select(t => (t, d: Vector3.Distance(origin, t.transform.position)))
                .Where(x => x.d <= TargetRadius)
                .OrderBy(x => x.d).ThenBy(x => x.t.Id, StringComparer.Ordinal)
                .Take(TargetCap)
                .Select(x => x.t.Id + ", " + Metres(x.d) + " away: " + x.t.Description);
        }

        private static string Metres(float d) => d.ToString("0", CultureInfo.InvariantCulture) + " m";

        // ---- The moments the game asks -------------------------------------------------------

        private async void OnSubmitted(string text) => await SubmitAsync(text);

        /// <summary>The player's typed line. Completes when the decision has been executed.</summary>
        public async Awaitable SubmitAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            if (Vector3.Distance(Player.position, Guard.transform.position) > SpeakingDistance)
            {
                Overlay.ShowPlaceholder("Player: " + text, "none", "no_target",
                    "Move within " + SpeakingDistance.ToString("0", CultureInfo.InvariantCulture) + " m of the guard to speak.");
                return;
            }
            if (IsDeciding)
            {
                Overlay.ShowNote("The guard is still answering. Say it again in a moment.");
                return;
            }
            await DecideAsync("player", "Player", text, DecisionPriority.PlayerFacing);
        }

        /// <summary>Something the guard noticed. Waits its turn when a decision is already running.</summary>
        public async Awaitable ReportEventAsync(string source, string label, string text, DecisionPriority priority)
        {
            if (IsDeciding)
            {
                pending = new PendingEvent { Source = source, Label = label, Text = text, Priority = priority };
                return;
            }
            await DecideAsync(source, label, text, priority);
        }

        private async Awaitable DecideAsync(string source, string label, string text, DecisionPriority priority)
        {
            var agent = EnsureAgent();
            IsDeciding = true;
            Overlay.ShowThinking(label + ": " + text, ProviderLabel);
            var record = Recorder.Begin(source, label, priority, text);
            try
            {
                var options = new DecideOptions
                {
                    StimulusLabel = label,
                    IdleExampleStimulus = "Fine day for it.",
                    Observations = Observations(),
                    Priority = priority
                };
                var token = Application.isPlaying ? destroyCancellationToken : CancellationToken.None;
                var result = await agent.DecideAsync(text, options, token);
                var t = result.Telemetry;

                if (result.Decision == null)
                {
                    string error = "No decision: " + t.ErrorKind + ". " + t.ErrorMessage;
                    Recorder.Complete(record, result, "error", error);
                    Overlay.ShowDecision(label + ": " + text, "none", "no_target", "", error,
                        t.LatencySeconds, t.GuardsFired, ProviderLabel);
                    return;
                }

                var d = result.Decision;
                bool executed = agent.Execute(result);
                string outcome = d.ActionId == ActionRegistry.None ? "none" : executed ? "executed" : "refused";
                string refusal = executed ? "" : RefusalReason(record, d);
                if (executed) Guard.Say(d.Statement);
                Recorder.Complete(record, result, outcome, refusal);

                string details = string.Format(CultureInfo.InvariantCulture,
                    "{0} / {1}  |  {2:0.00} s, queued {3:0.00} s  |  tokens {4} in, {5} out  |  history sent {6}{7}",
                    t.ProviderName, t.Model, t.LatencySeconds, t.QueueWaitSeconds, t.PromptTokens,
                    t.CompletionTokens, t.HistoryTurnsSent, executed ? "" : "  |  REFUSED by Execute: " + refusal);
                Overlay.ShowDecision(label + ": " + text, d.ActionId, d.TargetId, d.Statement, details,
                    t.LatencySeconds, t.GuardsFired, ProviderLabel);
            }
            catch (OperationCanceledException)
            {
                // The scene is closing; nothing to show.
            }
            finally
            {
                IsDeciding = false;
                LastActivityTime = Time.time;
            }

            if (pending != null)
            {
                var next = pending;
                pending = null;
                await DecideAsync(next.Source, next.Label, next.Text, next.Priority);
            }
        }

        private static string RefusalReason(DecisionRecord record, AgentDecision d)
        {
            if (!record.OfferedActions.Contains(d.ActionId)) return "the action was not offered";
            if (d.TargetId != TargetRegistry.NoTarget && !record.OfferedTargets.Contains(d.TargetId)) return "the target was not offered";
            return "the action or target stopped being available before it ran";
        }

        private void OnGUI()
        {
            float scale = Mathf.Max(0.5f, Mathf.Min(Screen.width / 1000f, Screen.height / 720f));
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(16, 16, 660, 122), GUI.skin.box);
            GUILayout.Label("GREYBOX VILLAGE / live guard", new GUIStyle(GUI.skin.label) { fontSize = 22 });
            GUILayout.Label("Blue: you   |   Gold: guard   |   Green: villagers   |   Grey: wolf   |   Red zones: off limits");
            GUILayout.Label("Guard distance: " + Vector3.Distance(Player.position, Guard.transform.position).ToString("0.0") +
                " m   |   Speak within " + SpeakingDistance.ToString("0") + " m   |   " + ProviderLabel +
                (IsDeciding ? "   |   thinking..." : ""));
            GUILayout.Label((Senses != null ? Senses.Status : "") + "   |   H: request history");
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }
    }
}
