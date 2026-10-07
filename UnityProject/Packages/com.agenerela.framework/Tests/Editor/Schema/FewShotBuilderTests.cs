using System.Collections.Generic;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public sealed class FewShotBuilderTests
    {
        [Test]
        public void ExamplesNeverPairAVerbWithAnUnsuitableTarget()
        {
            // Regression: blind rotation through the registry once produced the
            // demonstration "Pick up the Blacksmith". Every target-requiring example must
            // come from that action's own PreferredExampleTargets.
            var targets = Registry("sword", "lantern", "blacksmith", "tower");
            var actions = new List<ActionDefinition>
            {
                Action("pick_up", "Pick up the {0}.", true, "sword", "lantern")
            };

            string block = FewShotBuilder.Build(actions, targets);

            Assert.That(block, Does.Not.Contain("Blacksmith"));
            Assert.That(block, Does.Contain("Pick up the Sword."));
        }

        [Test]
        public void BuildsTheVillageGuardBlock()
        {
            var targets = Registry("tower", "training_dummy");
            var actions = new List<ActionDefinition>
            {
                Action("follow_player", "Accompany me on my rounds.", false),
                Action("move_to", "Proceed to the {0}.", true, "tower", "bridge"),
                Action("attack_target", "Engage the {0}.", true, "training_dummy")
            };

            string block = FewShotBuilder.Build(actions, targets, "Player", "You seem well rested today.");

            Assert.That(block, Is.EqualTo(string.Join("\n",
                "Examples of correct decisions:",
                "Player: \"Accompany me on my rounds.\" -> action: follow_player, target: no_target",
                "Player: \"Proceed to the Tower.\" -> action: move_to, target: tower",
                "Player: \"Engage the TrainingDummy.\" -> action: attack_target, target: training_dummy",
                "Player: \"You seem well rested today.\" -> action: none, target: no_target",
                "Player: \"Proceed to the Atlantis.\" -> action: none, target: no_target (not an available target)")));
        }

        [Test]
        public void BuildsTheColonyBlockWithTheSameCode()
        {
            // The test of the concept: no speaking character, a different label, and the
            // builder cannot tell this apart from the guard above.
            var targets = Registry("house", "mine");
            var actions = new List<ActionDefinition>
            {
                Action("build", "Housing is short; we need a {0}.", true, "house"),
                Action("assign_worker", "The {0} is unstaffed.", true, "mine"),
                Action("celebrate", "The harvest came in above target.", false)
            };

            string block = FewShotBuilder.Build(actions, targets, "Report",
                                                "Nothing has changed since last time.");

            Assert.That(block, Is.EqualTo(string.Join("\n",
                "Examples of correct decisions:",
                "Report: \"Housing is short; we need a House.\" -> action: build, target: house",
                "Report: \"The Mine is unstaffed.\" -> action: assign_worker, target: mine",
                "Report: \"The harvest came in above target.\" -> action: celebrate, target: no_target",
                "Report: \"Nothing has changed since last time.\" -> action: none, target: no_target",
                "Report: \"Housing is short; we need a Atlantis.\" -> action: none, target: no_target (not an available target)")));
        }

        [Test]
        public void AnActionWithNoRegisteredPreferredTargetIsOmitted()
        {
            var targets = Registry("tower");
            var actions = new List<ActionDefinition>
            {
                Action("move_to", "Proceed to the {0}.", true, "tower"),
                Action("give_item", "Hand over the {0}.", true, "sword")   // sword not registered
            };

            string block = FewShotBuilder.Build(actions, targets);

            Assert.That(block, Does.Contain("move_to"));
            Assert.That(block, Does.Not.Contain("give_item"));
            Assert.That(block, Does.Not.Contain("Sword"));
        }

        [Test]
        public void AnEmptyIdleStimulusOmitsTheIdleExample()
        {
            var targets = Registry("tower");
            var actions = new List<ActionDefinition> { Action("move_to", "Proceed to the {0}.", true, "tower") };

            string withIdle = FewShotBuilder.Build(actions, targets, "Player", "Nothing has changed.");
            string without = FewShotBuilder.Build(actions, targets, "Player", null);

            Assert.That(withIdle, Does.Contain("\"Nothing has changed.\" -> action: none"));
            Assert.That(Lines(without), Is.EqualTo(Lines(withIdle) - 1));
        }

        [Test]
        public void TheRefusalExampleIsOmittedWhenNoActionTakesATarget()
        {
            var targets = Registry("tower");
            var actions = new List<ActionDefinition>
            {
                Action("celebrate", "The harvest came in above target.", false)
            };

            string block = FewShotBuilder.Build(actions, targets);

            Assert.That(block, Does.Not.Contain("Atlantis"));
            Assert.That(block, Does.Not.Contain("not an available target"));
        }

        [Test]
        public void ThePlaceholderFallsBackWhenTheGameReallyHasAnAtlantis()
        {
            // Atlantis is registered but is not move_to's preferred target, so the only
            // line it could reach is the refusal one - which must pick another word.
            var targets = Registry("tower", "atlantis");
            var actions = new List<ActionDefinition> { Action("move_to", "Proceed to the {0}.", true, "tower") };

            string block = FewShotBuilder.Build(actions, targets);

            Assert.That(block, Does.Contain("Proceed to the Avalon."));
            Assert.That(block, Does.Not.Contain("Atlantis"));
        }

        [Test]
        public void AnActionWithNoExampleStimulusIsOmitted()
        {
            var targets = Registry("tower");
            var actions = new List<ActionDefinition>
            {
                Action("move_to", "Proceed to the {0}.", true, "tower"),
                Action("wait", "", false)
            };

            string block = FewShotBuilder.Build(actions, targets);

            Assert.That(block, Does.Not.Contain("wait"));
        }

        [Test]
        public void TheBlockAlwaysOpensWithTheHeader()
        {
            string block = FewShotBuilder.Build(new List<ActionDefinition>(), new TargetRegistry());

            Assert.That(block, Is.EqualTo("Examples of correct decisions:"));
        }

        private static TargetRegistry Registry(params string[] ids)
        {
            var registry = new TargetRegistry();
            foreach (string id in ids)
            {
                registry.Register(id, new object());
            }

            return registry;
        }

        private static ActionDefinition Action(string id, string stimulus, bool requiresTarget,
                                               params string[] preferredTargets)
            => new ActionDefinition
            {
                Id = id,
                ExampleStimulus = stimulus,
                RequiresTarget = requiresTarget,
                PreferredExampleTargets = preferredTargets
            };

        private static int Lines(string block) => block.Split('\n').Length;
    }
}
