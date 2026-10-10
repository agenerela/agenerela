using System.Collections.Generic;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests
{
    /// <summary>
    /// Shared test data, so the test classes that run an agent describe the same one. A test class
    /// that calls a method creating assets calls <see cref="DestroyCreated"/> in its <c>[TearDown]</c>.
    /// </summary>
    public static class Fixtures
    {
        private static readonly List<Object> created = new List<Object>();

        /// <summary>
        /// The village guard of #16's golden file, with the goals written as the golden shows them.
        /// <c>PromptBuilderTests</c> reads it too, so the golden and the agent's tests describe one guard.
        /// </summary>
        public static AgentIdentity VillageGuardIdentity()
        {
            return new AgentIdentity
            {
                Name = "Village Guard",
                Role = "a guard at the town gate",
                Personality = "Loyal, brief, a little suspicious of strangers.",
                Goals = "keep the gate safe; help the player when asked.",
            };
        }

        /// <summary>
        /// The village guard's profile: its identity, four actions and the golden's idle example.
        /// Bound with <c>follow_player</c>, <c>move_to</c> and <c>attack_target</c>, with
        /// <c>tower</c> and <c>training_dummy</c> registered and <c>stop_following</c> masked, it
        /// produces #16's golden few-shot block. Creates assets: see <see cref="DestroyCreated"/>.
        /// </summary>
        /// <remarks>
        /// No example stimulus here may be an evaluation prompt (hard rule 4), even in a test.
        /// </remarks>
        public static AgentProfile VillageGuardProfile()
        {
            var profile = Create<AgentProfile>();
            profile.name = "VillageGuard";
            profile.Identity = VillageGuardIdentity();
            profile.IdleExampleStimulus = "You seem well rested today.";
            profile.Actions.Add(Action("follow_player", "Walk alongside the player.", "Accompany me on my rounds.", false));
            profile.Actions.Add(Action("stop_following", "Stop walking alongside the player.", "You can return to your post now.", false));
            profile.Actions.Add(Action("move_to", "Walk to a named place.", "Proceed to the {0}.", true, "tower", "bridge"));
            profile.Actions.Add(Action("attack_target", "Attack a named target.", "Engage the {0}.", true, "training_dummy"));
            return profile;
        }

        /// <summary>
        /// A village guard ready to decide: <see cref="VillageGuardProfile"/> with all four actions
        /// bound, each to its own <see cref="FakeHandler"/>, <c>tower</c> and <c>training_dummy</c>
        /// registered, and <c>State["isFollowing"]</c> set. <c>follow_player</c> is available only
        /// while it is false and <c>stop_following</c> only while it is true. Creates assets: see
        /// <see cref="DestroyCreated"/>.
        /// </summary>
        public static Agent VillageGuard(ILLMProvider provider, bool isFollowing = false)
        {
            var agent = new Agent(VillageGuardProfile(), provider);
            agent.Bind("follow_player", new FakeHandler(ctx => !IsFollowing(ctx)));
            agent.Bind("stop_following", new FakeHandler(IsFollowing));
            agent.Bind("move_to", new FakeHandler());
            agent.Bind("attack_target", new FakeHandler());
            agent.Targets.Register("tower", new object());
            agent.Targets.Register("training_dummy", new object());
            agent.State["isFollowing"] = isFollowing;
            return agent;
        }

        /// <summary>The <see cref="FakeHandler"/> bound to <paramref name="actionId"/> on <paramref name="agent"/>.</summary>
        public static FakeHandler Handler(Agent agent, string actionId)
        {
            Assert.That(agent.Actions.TryGet(actionId, out _, out var handler), Is.True, $"'{actionId}' is not bound.");
            return (FakeHandler)handler;
        }

        /// <summary>Destroys every asset the fixtures created since the last call.</summary>
        public static void DestroyCreated()
        {
            foreach (var obj in created)
            {
                Object.DestroyImmediate(obj);
            }

            created.Clear();
        }

        private static bool IsFollowing(AgentContext ctx)
        {
            return (bool)ctx.State["isFollowing"];
        }

        private static ActionDefinitionAsset Action(string id, string description, string exampleStimulus,
                                                    bool requiresTarget, params string[] preferredTargets)
        {
            var asset = Create<ActionDefinitionAsset>();
            asset.name = id;
            asset.Action = new ActionDefinition
            {
                Id = id,
                Description = description,
                RequiresTarget = requiresTarget,
                ExampleStimulus = exampleStimulus,
                PreferredExampleTargets = preferredTargets,
            };
            return asset;
        }

        private static T Create<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            created.Add(obj);
            return obj;
        }
    }
}
