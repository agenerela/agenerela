using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using LivingVale.Interaction;
using UnityEngine;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// The G5 driver: each speaking NPC is a framework <see cref="Agent"/>, built in code the first
    /// time the player talks to it, and the player's line is its stimulus. Deciding and executing
    /// stay two calls (hard rule 5): <see cref="DecideAsync"/> returns the framework's
    /// <see cref="DecisionResult"/> in <see cref="NpcReply.DriverState"/>, and <see cref="Execute"/>
    /// hands it back to <see cref="Agent.Execute"/>, which checks it again before anything runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Memory.</b> Each agent keeps the framework's own <see cref="RollingHistory"/>, not the
    /// coordinator's chat log. The framework remembers what the agent actually decided, as the
    /// guards left it, in the encoding its prompt builder expects, and a cancelled or failed turn
    /// remembers nothing, which matches the coordinator dropping it. Design §11 asks for exactly
    /// this: keep the framework's memory contract rather than invent another history encoding. The
    /// cost: lines the scripted driver gave before an F6 switch are not in it.
    /// </para>
    /// <para>
    /// <b>Actions.</b> Only <see cref="ActionRegistry.None"/>, which every agent has, until the
    /// trade session's quote actions merge. <see cref="ConfigureAgent"/> runs once per NPC when
    /// its agent is built, and is where the game registers more: <c>agent.Actions.Register(...)</c>.
    /// </para>
    /// <para>
    /// Call from the main thread, as the coordinator does: the framework's <c>Awaitable</c>
    /// resumes there.
    /// </para>
    /// </remarks>
    public sealed class AgenerelaNpcDecisionDriver : INpcDecisionDriver
    {
        /// <summary>The label the stimulus carries in the prompt: <c>Player: "Hello."</c></summary>
        public const string StimulusLabel = "Player";

        /// <summary>What an NPC says when a guard stops its decision; the model's own line is never used then (design §12).</summary>
        public const string RefusalLine = "I can't help you with that.";

        private readonly ILLMProvider provider;
        private readonly Dictionary<string, NpcPersona> personas = new Dictionary<string, NpcPersona>();
        private readonly Dictionary<string, Agent> agents = new Dictionary<string, Agent>();
        private readonly Action<NpcDescriptor, Agent> configureAgent;
        private Task warmUp;

        /// <param name="provider">Shared by every NPC, so their requests share one queue (DR-015).</param>
        /// <param name="modelLabel">What <see cref="Label"/> names, such as <c>qwen3.5:2b</c>.</param>
        /// <param name="personas">The authored NPCs; null uses <see cref="ValeNpcPersonas.All"/>. An NPC without one is built from its descriptor.</param>
        /// <param name="configureAgent">Runs once per NPC, right after its agent is built: register its actions, guards or observation sources here.</param>
        public AgenerelaNpcDecisionDriver(ILLMProvider provider, string modelLabel,
            IEnumerable<NpcPersona> personas = null, Action<NpcDescriptor, Agent> configureAgent = null)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            ModelLabel = string.IsNullOrWhiteSpace(modelLabel) ? provider.Name : modelLabel.Trim();
            this.configureAgent = configureAgent;

            foreach (var persona in personas ?? ValeNpcPersonas.All)
            {
                if (persona != null)
                {
                    this.personas[persona.NpcId] = persona;
                }
            }
        }

        /// <summary>A driver over a local Ollama at <c>127.0.0.1:11434</c> (never <c>localhost</c>, AGENTS.md).</summary>
        public static AgenerelaNpcDecisionDriver ForOllama(string model, Action<NpcDescriptor, Agent> configureAgent = null)
        {
            var settings = new OllamaSettings();
            if (!string.IsNullOrWhiteSpace(model))
            {
                settings.Model = model.Trim();
            }

            return new AgenerelaNpcDecisionDriver(new OllamaProvider(settings), settings.Model, null, configureAgent);
        }

        public string ModelLabel { get; }

        public string Label => "Agenerela · " + ModelLabel;

        /// <summary>The game's own observations for a turn, sent after the persona's knowledge: quest stage, a visible quote, the last receipt.</summary>
        public Func<NpcTurn, IEnumerable<string>> TurnObservations { get; set; }

        /// <summary>How many decisions <see cref="Execute"/> was asked to carry out. Tests read it.</summary>
        public int ExecutedCount { get; private set; }

        /// <summary>This NPC's agent, built on first use.</summary>
        public Agent AgentFor(NpcDescriptor npc)
        {
            if (npc == null)
            {
                throw new ArgumentNullException(nameof(npc));
            }

            if (agents.TryGetValue(npc.Id, out var agent))
            {
                return agent;
            }

            var persona = PersonaFor(npc);
            agent = new Agent(persona.ToIdentity(), Array.Empty<ActionDefinition>(), provider)
            {
                RefusalStatement = (verdict, context) => RefusalLine
            };

            configureAgent?.Invoke(npc, agent);
            agents[npc.Id] = agent;
            return agent;
        }

        /// <summary>The persona the NPC's agent is built from: the authored one, or one made from its descriptor.</summary>
        public NpcPersona PersonaFor(NpcDescriptor npc)
        {
            return personas.TryGetValue(npc.Id, out var persona) ? persona : NpcPersona.FromDescriptor(npc);
        }

        /// <summary>The observations a turn sends: the persona's knowledge, then <see cref="TurnObservations"/>.</summary>
        public List<string> ObservationsFor(NpcTurn turn)
        {
            var lines = new List<string>(PersonaFor(turn.Npc).Knowledge);
            var extra = TurnObservations?.Invoke(turn);
            if (extra != null)
            {
                lines.AddRange(extra);
            }

            return lines;
        }

        public async Task<NpcReply> DecideAsync(NpcTurn turn, CancellationToken ct)
        {
            if (turn == null)
            {
                throw new ArgumentNullException(nameof(turn));
            }

            ct.ThrowIfCancellationRequested();

            var agent = AgentFor(turn.Npc);
            var options = new DecideOptions
            {
                StimulusLabel = StimulusLabel,
                Observations = ObservationsFor(turn),
                Priority = DecisionPriority.PlayerFacing
            };

            // A cancelled decision throws OperationCanceledException here; the coordinator owns
            // timeouts and cancellations and tells them apart itself.
            DecisionResult result = await agent.DecideAsync(turn.PlayerLine, options, ct);
            return ToReply(result);
        }

        /// <summary>
        /// What the coordinator gets for a framework result. An unreadable reply is
        /// <see cref="NpcReplyFailure.Malformed"/>; every other pipeline error, the server being
        /// unreachable included, is <see cref="NpcReplyFailure.ProviderError"/>.
        /// </summary>
        public static NpcReply ToReply(DecisionResult result)
        {
            if (result == null)
            {
                return NpcReply.Failed(NpcReplyFailure.ProviderError);
            }

            var telemetry = result.Telemetry;
            if (result.Decision == null || (telemetry != null && telemetry.Outcome == DecisionOutcome.PipelineError))
            {
                var kind = telemetry?.ErrorKind;
                bool unreadable = kind == PipelineErrorKind.UnreadableReply || kind == PipelineErrorKind.ProviderMalformedResponse;
                return NpcReply.Failed(unreadable ? NpcReplyFailure.Malformed : NpcReplyFailure.ProviderError);
            }

            return NpcReply.Decided(result.Decision, result);
        }

        public bool Execute(NpcTurn turn, NpcReply reply)
        {
            if (turn == null || reply == null || !(reply.DriverState is DecisionResult result))
            {
                return false;
            }

            // Only the agent that decided it: one this driver never built has nothing to check against.
            if (!agents.TryGetValue(turn.Npc.Id, out var agent))
            {
                return false;
            }

            ExecutedCount++;
            return agent.Execute(result);
        }

        /// <summary>
        /// Loads the model without waiting for it, so the first real turn does not spend the turn
        /// timeout on loading (about 8 s cold for qwen3.5:2b on an 8 GB laptop GPU). Sent by a
        /// throwaway agent with no memory, at background priority, so no NPC remembers it. Never throws.
        /// </summary>
        public void WarmUp()
        {
            _ = WarmUpAsync();
        }

        /// <summary>The one warm-up request, started on the first call. Never faults: a failure is logged.</summary>
        public Task WarmUpAsync()
        {
            return warmUp ?? (warmUp = SendWarmUp());
        }

        private async Task SendWarmUp()
        {
            try
            {
                var agent = new Agent(new AgentIdentity { Name = "Warm-up" }, Array.Empty<ActionDefinition>(), provider) { Memory = null };
                var result = await agent.DecideAsync("Hello.", new DecideOptions { Priority = DecisionPriority.Background });
                if (result?.Decision == null)
                {
                    Debug.LogWarning($"[LivingVale] {Label} warm-up got no decision ({result?.Telemetry?.ErrorKind}); NPC turns will fail until the model answers.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LivingVale] {Label} warm-up failed: {e.Message}");
            }
        }
    }
}
