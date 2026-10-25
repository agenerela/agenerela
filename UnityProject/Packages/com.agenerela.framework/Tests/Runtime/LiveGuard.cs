using System.Collections.Generic;

namespace Agenerela.Tests
{
    /// <summary>
    /// The agent the Phase 2 gate decides with (#94): the village guard built in code, with five
    /// registered actions plus <c>none</c>, and a target for each action that needs one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The four actions of the EditMode fixtures' guard, plus <c>pick_up_item</c>, the
    /// prototype's fifth. Handlers change only <c>State["isFollowing"]</c>: no test runs game code,
    /// and none checks whether the model chose well, which is Phase 4's to measure with a control
    /// arm.
    /// </para>
    /// <para>
    /// No example stimulus here may be an evaluation prompt (hard rule 4).
    /// </para>
    /// </remarks>
    internal static class LiveGuard
    {
        public const string IsFollowing = "isFollowing";

        public static Agent Build(Agenerela.Providers.ILLMProvider provider)
        {
            var identity = new AgentIdentity
            {
                Name = "Village Guard",
                Role = "a guard at the town gate",
                Personality = "Loyal, brief, a little suspicious of strangers.",
                Goals = "keep the gate safe; help the player when asked.",
            };

            var actions = new List<ActionDefinition>
            {
                Action("follow_player", "Walk alongside the player.", "Accompany me on my rounds.", false),
                Action("stop_following", "Stop walking alongside the player.", "You can return to your post now.", false),
                Action("move_to", "Walk to a named place.", "Proceed to the {0}.", true, "tower"),
                Action("attack_target", "Attack a named target.", "Engage the {0}.", true, "training_dummy"),
                Action("pick_up_item", "Pick up a named item.", "Fetch the {0} for me.", true, "sword"),
            };

            var agent = new Agent(identity, actions, provider);
            agent.State[IsFollowing] = false;

            agent.Bind("follow_player", new DelegateActionHandler(
                ctx => !Following(ctx), (ctx, decision) => agent.State[IsFollowing] = true));
            agent.Bind("stop_following", new DelegateActionHandler(
                Following, (ctx, decision) => agent.State[IsFollowing] = false));
            agent.Bind("move_to", new DelegateActionHandler(ctx => true, (ctx, decision) => { }));
            agent.Bind("attack_target", new DelegateActionHandler(ctx => true, (ctx, decision) => { }));
            agent.Bind("pick_up_item", new DelegateActionHandler(ctx => true, (ctx, decision) => { }));

            agent.Targets.Register("tower", "tower");
            agent.Targets.Register("training_dummy", "training_dummy");
            agent.Targets.Register("sword", "sword");
            return agent;
        }

        /// <summary>The options every live decision uses, with the guard's idle example.</summary>
        public static DecideOptions Options(DecisionPriority priority = DecisionPriority.PlayerFacing)
        {
            return new DecideOptions { IdleExampleStimulus = "You seem well rested today.", Priority = priority };
        }

        private static bool Following(AgentContext ctx)
        {
            return (bool)ctx.State[IsFollowing];
        }

        private static ActionDefinition Action(string id, string description, string exampleStimulus,
                                               bool requiresTarget, params string[] preferredTargets)
        {
            return new ActionDefinition
            {
                Id = id,
                Description = description,
                RequiresTarget = requiresTarget,
                ExampleStimulus = exampleStimulus,
                PreferredExampleTargets = preferredTargets,
            };
        }
    }
}
