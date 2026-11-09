using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using LivingVale.Game;
using LivingVale.Interaction;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// The scripted replies, decided through the framework's <c>Agent</c> like the model's (#194):
    /// an <see cref="AgenerelaNpcDecisionDriver"/> over a <see cref="ScriptedProvider"/>, whose
    /// callback asks <see cref="ScriptedNpcDecisionDriver.Choose"/> what to say and do. The scripted
    /// and model paths now share one pipeline: the same bindings, guards, executor, receipts and
    /// committed memory. Only the reply's source differs.
    /// </summary>
    /// <remarks>
    /// The F1–F5 fixtures become scripted replies: Slow waits <see cref="ScriptedNpcDecisionDriver.SlowDelay"/>,
    /// NeverAnswers never answers, Malformed answers with text that is not a decision, and Throws
    /// fails as a server error. The coordinator sees them as it did before: a timeout, a malformed
    /// reply, a provider error. The label is the script's, so a scripted reply is never mistaken
    /// for a model's.
    /// </remarks>
    public sealed class ScriptedAgentDriver : INpcDecisionDriver
    {
        /// <summary>What the Malformed fixture answers with: text that is not one JSON decision.</summary>
        public const string MalformedText = "Scripted reply that is not a decision (fixture).";

        private readonly ScriptedNpcDecisionDriver script;
        private readonly AgenerelaNpcDecisionDriver inner;

        // ScriptedProvider names the asking agent by its identity's name; the script keys on NPC ids.
        private readonly Dictionary<string, string> npcByAgentName = new Dictionary<string, string>(StringComparer.Ordinal);

        public ScriptedAgentDriver(ScriptedNpcDecisionDriver script, NpcActionExecutor executor, IEnumerable<NpcPersona> personas = null)
        {
            this.script = script ?? throw new ArgumentNullException(nameof(script));
            Provider = new ScriptedProvider { Callback = Answer };
            inner = new AgenerelaNpcDecisionDriver(Provider, ScriptedProvider.ProviderName, personas, null, executor);
        }

        /// <summary>The framework provider the replies come from; tests read its requests.</summary>
        public ScriptedProvider Provider { get; }

        /// <summary>The driver underneath, for its outcome and memory.</summary>
        public AgenerelaNpcDecisionDriver Inner => inner;

        public string Label => script.Label;

        public Task<NpcReply> DecideAsync(NpcTurn turn, CancellationToken ct)
        {
            if (turn == null)
            {
                throw new ArgumentNullException(nameof(turn));
            }

            npcByAgentName[inner.PersonaFor(turn.Npc).Name] = turn.Npc.Id;
            return inner.DecideAsync(turn, ct);
        }

        public bool Execute(NpcTurn turn, NpcReply reply) => inner.Execute(turn, reply);

        private ScriptedReply Answer(ScriptedRequest request)
        {
            if (request.IsRefusalLine)
            {
                return null;
            }

            var fixture = script.Fixture;
            if (fixture == ScriptedFixture.NeverAnswers)
            {
                return ScriptedReply.Never();
            }

            float wait = (float)script.NextDelay().TotalSeconds;
            switch (fixture)
            {
                case ScriptedFixture.Malformed:
                    return ScriptedReply.Raw(MalformedText).After(wait);
                case ScriptedFixture.Throws:
                    return ScriptedReply.Fail(ProviderErrorKind.ServerError, "Scripted backend failure (fixture).").After(wait);
            }

            string npcId = request.Agent != null && npcByAgentName.TryGetValue(request.Agent, out var id) ? id : request.Agent;
            var decision = script.Choose(npcId, request.Stimulus);
            return ScriptedReply.Decide(decision.ActionId, decision.TargetId, decision.Statement).After(wait);
        }
    }
}
