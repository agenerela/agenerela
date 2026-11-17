using System;
using System.Collections.Generic;
using Agenerela.Providers;
using LivingVale.Game;
using LivingVale.Interaction;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// The scripted replies as a framework provider (#194): a <see cref="ScriptedProvider"/> whose
    /// callback asks <see cref="ScriptedNpcDecisionDriver.Choose"/> what to say and do. F6 gives every
    /// NPC's agent this provider instead of the model's (<see cref="NpcProviderSwitch"/>), so the
    /// scripted and model paths share one agent per NPC: the same bindings, guards, executor,
    /// receipts and memory (LV #23). Only the reply's source differs.
    /// </summary>
    /// <remarks>
    /// The F1–F5 fixtures become scripted replies: Slow waits <see cref="ScriptedNpcDecisionDriver.SlowDelay"/>,
    /// NeverAnswers never answers, Malformed answers with text that is not a decision, and Throws
    /// fails as a server error. The coordinator sees them as a timeout, a malformed reply and a
    /// provider error. The label is the script's, so a scripted reply is never mistaken for a model's.
    /// </remarks>
    public sealed class ScriptedReplies
    {
        /// <summary>What the Malformed fixture answers with: text that is not one JSON decision.</summary>
        public const string MalformedText = "Scripted reply that is not a decision (fixture).";

        public ScriptedReplies(ScriptedNpcDecisionDriver script)
        {
            Script = script ?? throw new ArgumentNullException(nameof(script));
            Provider = new ScriptedProvider { Callback = Answer };
        }

        /// <summary>The keyword lines, trade, quest and route rules, and the fixture in use.</summary>
        public ScriptedNpcDecisionDriver Script { get; }

        /// <summary>The framework provider the replies come from; tests read its requests.</summary>
        public ScriptedProvider Provider { get; }

        /// <summary>"Scripted replies", with the fixture when one is on.</summary>
        public string Label => Script.Label;

        /// <summary>A driver whose NPCs answer from the script alone, for tests and tools.</summary>
        public AgenerelaNpcDecisionDriver Driver(NpcActionExecutor executor = null, IEnumerable<NpcPersona> personas = null)
        {
            var driver = new AgenerelaNpcDecisionDriver(Provider, ScriptedProvider.ProviderName, personas, null, executor);
            driver.UseProvider(Provider, ScriptedProvider.ProviderName, () => Label);
            return driver;
        }

        private ScriptedReply Answer(ScriptedRequest request)
        {
            if (request.IsRefusalLine)
            {
                return null;
            }

            var fixture = Script.Fixture;
            if (fixture == ScriptedFixture.NeverAnswers)
            {
                return ScriptedReply.Never();
            }

            float wait = (float)Script.NextDelay().TotalSeconds;
            switch (fixture)
            {
                case ScriptedFixture.Malformed:
                    return ScriptedReply.Raw(MalformedText).After(wait);
                case ScriptedFixture.Throws:
                    return ScriptedReply.Fail(ProviderErrorKind.ServerError, "Scripted backend failure (fixture).").After(wait);
            }

            // Each NPC's agent carries its stable id (#217), so the script keys on it, not the display name.
            string npcId = request.AgentId ?? request.Agent;
            var decision = Script.Choose(npcId, request.Stimulus);
            return ScriptedReply.Decide(decision.ActionId, decision.TargetId, decision.Statement).After(wait);
        }
    }
}
