using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using LivingVale.Game;
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
    /// coordinator's chat log, so the model reads its earlier answers in the encoding the framework
    /// defines (design §11). It is wrapped in <see cref="CommittedHistory"/>, so a decision is
    /// remembered only once <see cref="Execute"/> carried it out: a reply the coordinator dropped
    /// as stale, or one the game refused, never enters it. The cost: lines the scripted driver gave
    /// before an F6 switch are not in it.
    /// </para>
    /// <para>
    /// <b>Actions.</b> <see cref="ActionRegistry.None"/>, which every agent has, and, given an
    /// <see cref="NpcActionExecutor"/>, the trade actions through <see cref="TradeActionBinding"/>,
    /// recomputed from the executor on every turn. The <c>configureAgent</c> callback runs once
    /// per NPC when its agent is built, and is where the game registers anything else.
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
        private readonly NpcActionExecutor executor;
        private readonly Dictionary<string, TradeActionBinding> trade = new Dictionary<string, TradeActionBinding>();
        private readonly Dictionary<string, TradeReceipt> lastReceipts;

        // Each NPC's memory, by NPC id. Kept apart from the agents so a driver rebuilt for another
        // model (WithProvider) carries every NPC's conversation over.
        private readonly Dictionary<string, CommittedHistory> memories;
        private Task warmUp;
        private bool detached;

        /// <param name="provider">Shared by every NPC, so their requests share one queue (DR-015).</param>
        /// <param name="modelLabel">What <see cref="Label"/> names, such as <c>qwen3.5:2b</c>.</param>
        /// <param name="personas">The authored NPCs; null uses <see cref="ValeNpcPersonas.All"/>. An NPC without one is built from its descriptor.</param>
        /// <param name="configureAgent">Runs once per NPC, right after its agent is built: register its actions, guards or observation sources here.</param>
        /// <param name="executor">The game's executor, shared with the scripted driver; null registers no trade actions.</param>
        public AgenerelaNpcDecisionDriver(ILLMProvider provider, string modelLabel,
            IEnumerable<NpcPersona> personas = null, Action<NpcDescriptor, Agent> configureAgent = null,
            NpcActionExecutor executor = null)
            : this(provider, modelLabel, personas, configureAgent, executor,
                new Dictionary<string, CommittedHistory>(), new Dictionary<string, TradeReceipt>())
        {
        }

        private AgenerelaNpcDecisionDriver(ILLMProvider provider, string modelLabel,
            IEnumerable<NpcPersona> personas, Action<NpcDescriptor, Agent> configureAgent,
            NpcActionExecutor executor, Dictionary<string, CommittedHistory> memories,
            Dictionary<string, TradeReceipt> lastReceipts)
        {
            this.memories = memories;
            this.lastReceipts = lastReceipts;
            this.executor = executor;
            if (executor != null)
            {
                // From the start, not when an NPC's agent is first built: a trade the scripted
                // driver made before an F6 switch must still reach the model's next turn.
                executor.Trade.Confirmed += OnConfirmed;
            }

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
        public static AgenerelaNpcDecisionDriver ForOllama(string model, NpcActionExecutor executor = null, Action<NpcDescriptor, Agent> configureAgent = null)
        {
            var settings = new OllamaSettings();
            if (!string.IsNullOrWhiteSpace(model))
            {
                settings.Model = model.Trim();
            }

            return new AgenerelaNpcDecisionDriver(new OllamaProvider(settings), settings.Model, null, configureAgent, executor);
        }

        /// <summary>
        /// This driver for another model: the same personas, executor, callbacks and every NPC's
        /// memory and last receipt, with new agents on <paramref name="newProvider"/>. Detaches this
        /// one, which stops following trade receipts; use the new one from now on.
        /// </summary>
        public AgenerelaNpcDecisionDriver WithProvider(ILLMProvider newProvider, string modelLabel)
        {
            var next = new AgenerelaNpcDecisionDriver(newProvider, modelLabel, personas.Values, configureAgent, executor, memories, lastReceipts)
            {
                TurnObservations = TurnObservations
            };
            Detach();
            return next;
        }

        /// <summary><see cref="WithProvider"/> with a new Ollama provider at <c>127.0.0.1:11434</c>.</summary>
        public AgenerelaNpcDecisionDriver WithOllamaModel(string model)
        {
            var settings = new OllamaSettings();
            if (!string.IsNullOrWhiteSpace(model))
            {
                settings.Model = model.Trim();
            }

            return WithProvider(new OllamaProvider(settings), settings.Model);
        }

        /// <summary>Stops following trade receipts. Called by <see cref="WithProvider"/>; safe to call twice.</summary>
        public void Detach()
        {
            if (detached)
            {
                return;
            }

            detached = true;
            if (executor != null)
            {
                executor.Trade.Confirmed -= OnConfirmed;
            }
        }

        public string ModelLabel { get; }

        public string Label => "Agenerela · " + ModelLabel;

        /// <summary>The game's own observations for a turn, sent after the persona's knowledge: quest stage, a visible quote, the last receipt.</summary>
        public Func<NpcTurn, IEnumerable<string>> TurnObservations { get; set; }

        /// <summary>How many decisions <see cref="Execute"/> was asked to carry out. Tests read it.</summary>
        public int ExecutedCount { get; private set; }

        /// <summary>The executor's outcome for the last trade action executed, or null. Tests and tools read it.</summary>
        public ActionOutcome LastOutcome { get; private set; }

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
                RefusalStatement = (verdict, context) => RefusalLine,
                Memory = MemoryFor(npc.Id)
            };

            if (executor != null)
            {
                trade[npc.Id] = TradeActionBinding.Bind(agent, npc.Id, executor);
            }

            configureAgent?.Invoke(npc, agent);
            agents[npc.Id] = agent;
            return agent;
        }

        private CommittedHistory MemoryFor(string npcId)
        {
            if (!memories.TryGetValue(npcId, out var memory))
            {
                memory = new CommittedHistory();
                memories[npcId] = memory;
            }

            return memory;
        }

        /// <summary>The persona the NPC's agent is built from: the authored one, or one made from its descriptor.</summary>
        public NpcPersona PersonaFor(NpcDescriptor npc)
        {
            return personas.TryGetValue(npc.Id, out var persona) ? persona : NpcPersona.FromDescriptor(npc);
        }

        /// <summary>The observations a turn sends: the persona's knowledge, what it can offer now, then <see cref="TurnObservations"/>.</summary>
        public List<string> ObservationsFor(NpcTurn turn)
        {
            var lines = new List<string>(PersonaFor(turn.Npc).Knowledge);
            if (trade.TryGetValue(turn.Npc.Id, out var binding))
            {
                var options = binding.Options();
                binding.RefreshExamples(options);
                lines.AddRange(TradeActionBinding.PromptLines(options));
                lastReceipts.TryGetValue(turn.Npc.Id, out var receipt);
                lines.AddRange(binding.StatusLines(receipt));
            }

            var extra = TurnObservations?.Invoke(turn);
            if (extra != null)
            {
                lines.AddRange(extra);
            }

            return lines;
        }

        private void OnConfirmed(TradeReceipt receipt)
        {
            var quote = receipt == null ? null : executor.Trade.FindQuote(receipt.QuoteId);
            if (quote?.NpcId != null)
            {
                lastReceipts[quote.NpcId] = receipt;
            }
        }

        public async Task<NpcReply> DecideAsync(NpcTurn turn, CancellationToken ct)
        {
            if (turn == null)
            {
                throw new ArgumentNullException(nameof(turn));
            }

            ct.ThrowIfCancellationRequested();

            var agent = AgentFor(turn.Npc);
            agent.State[TradeActionBinding.GenerationKey] = turn.Generation;
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
            LastOutcome = null;
            trade.TryGetValue(turn.Npc.Id, out var binding);
            binding?.ClearOutcome();

            // The handlers read the generation from the state Agent.Execute copies into their context.
            agent.State[TradeActionBinding.GenerationKey] = turn.Generation;
            bool executed = agent.Execute(result);

            // Agent.Execute is true once the handler ran; the executor may still have refused the quote.
            if (executed && binding?.LastOutcome != null)
            {
                LastOutcome = binding.LastOutcome;
                executed = LastOutcome.Executed;
            }

            if (executed && agent.Memory is CommittedHistory memory)
            {
                memory.Commit(result);
            }

            return executed;
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
