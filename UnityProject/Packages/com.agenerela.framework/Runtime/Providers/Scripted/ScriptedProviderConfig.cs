using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// A <see cref="ScriptedProvider"/> as an asset, so a scene, an <c>AgentBehaviour</c> or the
    /// project default can select it like any provider config: for presenting a game with the
    /// model off, or a headless capture. Made with <b>Create → Agenerela → Scripted Provider</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each entry of <see cref="Replies"/> answers one agent and one stimulus with one decision.
    /// A blank agent answers every agent and a blank stimulus every stimulus, so an entry with
    /// both blank is the fallback; a new asset has one, which does nothing and says nothing.
    /// Entries for the same agent and stimulus answer in list order, the last repeating.
    /// <see cref="ScriptedProvider"/> says which entry wins when several fit.
    /// </para>
    /// <para>
    /// For anything an entry cannot say, such as a failure, a reply that does not parse or a
    /// callback, script the built provider in code through <see cref="Scripted"/>. Edits reach
    /// the provider as <see cref="ProviderConfig"/> says: outside Play mode at once, during Play at
    /// the next Play. Code scripting the provider during Play reaches it at once.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Agenerela/Scripted Provider", fileName = "Scripted")]
    public sealed class ScriptedProviderConfig : ProviderConfig
    {
        /// <summary>One scripted decision: who it answers, what it answers, and the reply.</summary>
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("The agent this answers, by its Agent.Id or its identity's Name; an id is matched first. " +
                     "Blank answers every agent.")]
            public string Agent = "";

            [Tooltip("The stimulus this answers, exactly as passed to DecideAsync, without its label. " +
                     "Blank answers every stimulus.")]
            public string Stimulus = "";

            [Tooltip("The action's id, or none.")]
            public string Action = ActionRegistry.None;

            [Tooltip("The target's id, or no_target.")]
            public string Target = TargetRegistry.NoTarget;

            [TextArea]
            [Tooltip("What the agent says.")]
            public string Statement = "";

            [Min(0f)]
            [Tooltip("Seconds before the reply arrives, as a model would take. Telemetry records it as " +
                     "the decision's latency.")]
            public float LatencySeconds;
        }

        [Tooltip("The scripted decisions. A blank Agent or Stimulus matches any; replies for the same " +
                 "agent and stimulus answer in order, the last repeating.")]
        public List<Entry> Replies = new List<Entry> { new Entry() };

        [TextArea]
        [Tooltip("What an agent says when a guard stops its decision and the call asked for a refusal " +
                 "line. Blank gives none, and the decision stands without a line.")]
        public string RefusalLine = "";

        /// <summary>
        /// The provider this config built, as a <see cref="ScriptedProvider"/>, for scripting it
        /// further in code. The same instance as <see cref="ProviderConfig.Provider"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">An entry is not valid; the message names it.</exception>
        public ScriptedProvider Scripted => (ScriptedProvider)Provider;

        /// <summary>A new <see cref="ScriptedProvider"/> answering with <see cref="Replies"/>.</summary>
        /// <exception cref="ArgumentException">An entry has no action or no target, or a latency out of range.</exception>
        protected override ILLMProvider CreateProvider()
        {
            var provider = new ScriptedProvider();

            // In the order each pair first appears, each pair's replies in list order.
            var order = new List<(string Agent, string Stimulus)>();
            var byPair = new Dictionary<(string, string), List<ScriptedReply>>();
            for (int i = 0; i < (Replies?.Count ?? 0); i++)
            {
                var entry = Replies[i];
                if (entry == null)
                {
                    continue;
                }

                var pair = (entry.Agent ?? "", entry.Stimulus ?? "");
                if (!byPair.TryGetValue(pair, out var replies))
                {
                    replies = new List<ScriptedReply>();
                    byPair.Add(pair, replies);
                    order.Add(pair);
                }

                replies.Add(ReplyOf(entry, i));
            }

            foreach (var pair in order)
            {
                provider.When(pair.Agent, pair.Stimulus, byPair[pair].ToArray());
            }

            if (!string.IsNullOrEmpty(RefusalLine))
            {
                provider.RefusalLine = ScriptedReply.Statement(RefusalLine);
            }

            return provider;
        }

        private static ScriptedReply ReplyOf(Entry entry, int index)
        {
            if (string.IsNullOrWhiteSpace(entry.Action))
            {
                throw new ArgumentException($"Replies[{index}] has no Action. Write {ActionRegistry.None} for no action.");
            }

            if (string.IsNullOrWhiteSpace(entry.Target))
            {
                throw new ArgumentException($"Replies[{index}] has no Target. Write {TargetRegistry.NoTarget} for none.");
            }

            if (!(entry.LatencySeconds >= 0f && entry.LatencySeconds <= ScriptedReply.MaxLatencySeconds))
            {
                throw new ArgumentException(
                    $"Replies[{index}] has a LatencySeconds of {entry.LatencySeconds}. It must be 0 to {ScriptedReply.MaxLatencySeconds}.");
            }

            return ScriptedReply.Decide(entry.Action, entry.Target, entry.Statement).After(entry.LatencySeconds);
        }
    }
}
