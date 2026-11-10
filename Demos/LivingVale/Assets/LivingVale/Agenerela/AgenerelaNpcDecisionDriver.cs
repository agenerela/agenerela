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
    /// defines (design §11). A change of model keeps it, since <see cref="UseProvider"/> only gives
    /// each agent another provider (#198). Each agent runs under <see cref="MemoryCommit.OnExecute"/> (DR-018), so a
    /// decision is remembered only once <see cref="Execute"/> carried it out: a reply the coordinator
    /// dropped as stale, or one the game refused, never enters it. The memories are saved with the
    /// game (<see cref="NpcMemoryArchive"/>). The cost: lines the scripted driver gave before an F6
    /// switch are not in it.
    /// </para>
    /// <para>
    /// <b>Actions.</b> <see cref="ActionRegistry.None"/>, which every agent has, and, given an
    /// <see cref="NpcActionExecutor"/>, the trade actions through <see cref="TradeActionBinding"/>
    /// and, when the executor has a quest log, <c>offer_quest</c> through <see cref="QuestActionBinding"/>,
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

        private ILLMProvider provider;
        private readonly Dictionary<string, NpcPersona> personas = new Dictionary<string, NpcPersona>();
        private readonly Dictionary<string, Agent> agents = new Dictionary<string, Agent>();
        private readonly Dictionary<string, Conversation> conversations = new Dictionary<string, Conversation>();
        private readonly Action<NpcDescriptor, Agent> configureAgent;
        private readonly NpcActionExecutor executor;
        private readonly Dictionary<string, TradeActionBinding> trade = new Dictionary<string, TradeActionBinding>();
        private readonly Dictionary<string, QuestActionBinding> questBindings = new Dictionary<string, QuestActionBinding>();
        private readonly Dictionary<string, RouteActionBinding> routeBindings = new Dictionary<string, RouteActionBinding>();
        private readonly Dictionary<string, TradeReceipt> lastReceipts = new Dictionary<string, TradeReceipt>();

        // Each NPC's memory, by NPC id, so a load can fill one before the NPC's agent is built.
        private readonly Dictionary<string, RollingHistory> memories = new Dictionary<string, RollingHistory>();

        /// <param name="provider">Shared by every NPC, so their requests share one queue (DR-015).</param>
        /// <param name="modelLabel">What <see cref="Label"/> names, such as <c>qwen3.5:2b</c>.</param>
        /// <param name="personas">The authored NPCs; null uses <see cref="ValeNpcPersonas.All"/>. An NPC without one is built from its descriptor.</param>
        /// <param name="configureAgent">Runs once per NPC, right after its agent is built: register its actions, guards or observation sources here.</param>
        /// <param name="executor">The game's executor, shared with the scripted driver; null registers no trade actions.</param>
        public AgenerelaNpcDecisionDriver(ILLMProvider provider, string modelLabel,
            IEnumerable<NpcPersona> personas = null, Action<NpcDescriptor, Agent> configureAgent = null,
            NpcActionExecutor executor = null)
        {
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

        /// <summary>A driver over Ollama: <paramref name="server"/>'s endpoint and key, or a local Ollama at <c>127.0.0.1:11434</c>.</summary>
        public static AgenerelaNpcDecisionDriver ForOllama(string model, NpcActionExecutor executor = null,
            Action<NpcDescriptor, Agent> configureAgent = null, OllamaProviderConfig server = null)
        {
            var settings = SettingsFor(model, server);
            return new AgenerelaNpcDecisionDriver(new OllamaProvider(settings), settings.Model, null, configureAgent, executor);
        }

        /// <summary>
        /// A provider for <paramref name="model"/> on <paramref name="server"/>, an
        /// <see cref="OllamaProviderConfig"/> whose API Key Variable names the key; null is a local
        /// Ollama at <c>127.0.0.1:11434</c> (never <c>localhost</c>, AGENTS.md).
        /// </summary>
        public static OllamaProvider OllamaFor(string model, OllamaProviderConfig server = null) =>
            new OllamaProvider(SettingsFor(model, server));

        private static OllamaSettings SettingsFor(string model, OllamaProviderConfig server)
        {
            var settings = server != null ? server.ToSettings() : new OllamaSettings();
            if (!string.IsNullOrWhiteSpace(model))
            {
                settings.Model = model.Trim();
            }

            return settings;
        }

        /// <summary>The provider every NPC's agent asks now.</summary>
        public ILLMProvider Provider => provider;

        /// <summary>
        /// Gives every NPC's agent <paramref name="next"/>, for a change of model, and every agent
        /// built later too (<see cref="Agent.Provider"/>, #198). Each keeps its memory, actions,
        /// targets and guards; a decision already under way finishes on the provider it started on.
        /// </summary>
        /// <param name="label">What <see cref="Label"/> says instead of "Agenerela · model", read each time: the scripted replies name their fixture.</param>
        public void UseProvider(ILLMProvider next, string modelLabel, Func<string> label = null)
        {
            provider = next ?? throw new ArgumentNullException(nameof(next));
            ModelLabel = string.IsNullOrWhiteSpace(modelLabel) ? next.Name : modelLabel.Trim();
            labelSource = label;
            foreach (var agent in agents.Values)
            {
                agent.Provider = next;
            }
        }

        public string ModelLabel { get; private set; }

        public string Label => labelSource != null ? labelSource() : "Agenerela · " + ModelLabel;

        private Func<string> labelSource;

        /// <summary>The game's own observations for a turn, sent after the bindings' lines: quest stage, a visible quote, the last receipt.</summary>
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
                // The NPC's stable id (#217): a scripted provider and the decision log tell NPCs
                // apart by it, never by the display name two NPCs could share. The model never sees it.
                Id = npc.Id,
                RefusalStatement = (verdict, context) => RefusalLine,
                Memory = MemoryFor(npc.Id),

                // A decision is remembered only once Execute carries it out (DR-018): a reply the
                // coordinator drops as stale, or one the game refuses, never enters memory.
                MemoryCommit = MemoryCommit.OnExecute
            };

            if (executor != null)
            {
                trade[npc.Id] = TradeActionBinding.Bind(agent, npc.Id, executor);
                if (executor.Quests != null)
                {
                    questBindings[npc.Id] = QuestActionBinding.Bind(agent, npc.Id, executor);
                }

                if (executor.Routes != null)
                {
                    routeBindings[npc.Id] = RouteActionBinding.Bind(agent, npc.Id, executor);
                }
            }

            configureAgent?.Invoke(npc, agent);
            agents[npc.Id] = agent;
            return agent;
        }

        /// <summary>This NPC's conversation with its agent (#209), which the coordinator asks and executes through.</summary>
        public Conversation ConversationFor(NpcDescriptor npc)
        {
            var agent = AgentFor(npc);
            if (!conversations.TryGetValue(npc.Id, out var chat))
            {
                chat = new Conversation(agent);
                conversations[npc.Id] = chat;
            }

            return chat;
        }

        private RollingHistory MemoryFor(string npcId)
        {
            if (!memories.TryGetValue(npcId, out var memory))
            {
                memory = new RollingHistory();
                memories[npcId] = memory;
            }

            return memory;
        }

        /// <summary>Every NPC's memory, by NPC id, as saved with the game.</summary>
        public IReadOnlyDictionary<string, RollingHistory> Memories => memories;

        /// <summary>This NPC's memory, made empty if it has none yet, so a load can fill it before the NPC first speaks.</summary>
        public RollingHistory MemoryOf(string npcId) => MemoryFor(npcId);

        /// <summary>The persona the NPC's agent is built from: the authored one, or one made from its descriptor.</summary>
        public NpcPersona PersonaFor(NpcDescriptor npc)
        {
            return personas.TryGetValue(npc.Id, out var persona) ? persona : NpcPersona.FromDescriptor(npc);
        }

        /// <summary>The rule line before the trade actions; null uses <see cref="TradeActionBinding.DefaultRule"/>.</summary>
        public string TradeRule { get; set; }

        /// <summary>
        /// The negative example the driver shows by default. In G5f's A/B it cut unasked trade
        /// offers to none on both models measured without costing an asked one (findings,
        /// 5 November). New wording: it repeats no evaluation prompt and no measured line (hard rule 4).
        /// </summary>
        public const string DefaultIdleExample = "Good to see a friendly face again.";

        /// <summary>
        /// A line the few-shot block shows answered with <c>none</c> (<see cref="DecideOptions.IdleExampleStimulus"/>):
        /// a negative example, so a model sees that not every line is a request. Null shows none.
        /// </summary>
        public string IdleExampleStimulus { get; set; } = DefaultIdleExample;

        /// <summary>
        /// The observations a turn sends: what it can offer now and where things stand, then
        /// <see cref="TurnObservations"/>. The persona's facts are not among them: they are the
        /// identity's knowledge (#231), in the system prompt, outside the observation cap.
        /// </summary>
        public List<string> ObservationsFor(NpcTurn turn)
        {
            var lines = new List<string>();
            if (trade.TryGetValue(turn.Npc.Id, out var binding))
            {
                var options = binding.Options();
                lines.AddRange(TradeActionBinding.RuleLines(options, TradeRule));
                lastReceipts.TryGetValue(turn.Npc.Id, out var receipt);
                lines.AddRange(binding.StatusLines(receipt));
            }

            if (questBindings.TryGetValue(turn.Npc.Id, out var quest))
            {
                var option = quest.Option();
                lines.AddRange(QuestActionBinding.RuleLines(option));
                lines.AddRange(quest.StatusLines());
            }

            if (routeBindings.TryGetValue(turn.Npc.Id, out var route))
            {
                var routeOptions = route.Options();
                lines.AddRange(RouteActionBinding.RuleLines(routeOptions));
                lines.AddRange(route.StatusLines());
            }

            var extra = TurnObservations?.Invoke(turn);
            if (extra != null)
            {
                lines.AddRange(extra);
            }

            return lines;
        }

        /// <summary>
        /// Forgets the last trade per NPC, for a loaded save: the load may have reversed it, and the
        /// prompt must not report a trade the game no longer holds (#206 review).
        /// </summary>
        public void ForgetReceipts()
        {
            lastReceipts.Clear();
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

            // A cancelled decision throws OperationCanceledException here; the caller owns
            // timeouts and cancellations and tells them apart itself.
            DecisionResult result = await agent.DecideAsync(turn.PlayerLine, OptionsFor(turn), ct);
            return ToReply(result);
        }

        /// <summary>What a turn sends with the player's line, through <see cref="DecideAsync"/> or the NPC's conversation.</summary>
        public DecideOptions OptionsFor(NpcTurn turn)
        {
            // The framework writes the choices into the prompt (#193, adopted in G5g after an A/B):
            // its list of actions and described targets, each action's counter-example, and the
            // agent's idle example. A local model never reads the schema, so this is what it reads.
            AgentFor(turn.Npc).IdleExampleStimulus = IdleExampleStimulus;
            return new DecideOptions
            {
                StimulusLabel = StimulusLabel,
                Observations = ObservationsFor(turn),
                Priority = DecisionPriority.PlayerFacing,
                ListActionsAndTargets = true,
                CounterExamples = true,

                // Every action declares its target category, so an example with no preferred target
                // names the first offered target its action takes, never one the line mentions (#234).
                ExampleTargetFallback = true,

                // Which conversation the decision belongs to (DR-018): the handlers read its
                // generation, so the executor refuses a reply that outlived its conversation.
                Correlation = turn
            };
        }

        /// <summary>What a framework result is to the game (<see cref="NpcReply.From"/>).</summary>
        public static NpcReply ToReply(DecisionResult result) => NpcReply.From(result);

        /// <summary>
        /// Carries out the NPC's conversation's current answer (#209): an answer the conversation no
        /// longer holds as current, a stale one, is refused and runs nothing.
        /// </summary>
        public bool Execute(NpcTurn turn, DecisionResult result)
        {
            if (turn == null || result == null || !conversations.TryGetValue(turn.Npc.Id, out var chat))
            {
                return false;
            }

            ExecutedCount++;
            bool executed = chat.Execute(result, out var actionResult);
            LastOutcome = actionResult.TryGetPayload<ActionOutcome>(out var outcome) ? outcome : null;
            return executed;
        }

        /// <summary>Carries out a decision from <see cref="DecideAsync"/>, which no conversation asked for: for tests and tools.</summary>
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

            // The handlers return what the executor did (DR-018): Done with the outcome, or Failed
            // with the outcome when the executor refused the quote or offer. Under
            // MemoryCommit.OnExecute the agent remembers the decision only when it succeeds here.
            bool executed = agent.Execute(result, out var actionResult);
            LastOutcome = actionResult.TryGetPayload<ActionOutcome>(out var outcome) ? outcome : null;
            return executed;
        }
    }
}
