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
            // come from that action's own PreferredExampleTargets. The unsuitable target is
            // registered first, so rotation through the registry reaches it before any
            // preferred one and this test fails.
            var targets = Registry("blacksmith", "tower", "sword", "lantern");
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

        // ---- Counter-examples (#190, Living Vale #9) ----------------------------------------------

        [Test]
        public void ACounterExampleFollowsItsActionsOwnExampleAnsweredWithNone()
        {
            var targets = Registry("bread", "tower");
            var actions = new List<ActionDefinition>
            {
                Countered(Action("quote_purchase", "How much for the {0}?", true, "bread"), "That {0} smells good."),
                Countered(Action("offer_quest", "Got any work for me?", false), "Any rumours about the wagon?"),
                Action("move_to", "Proceed to the {0}.", true, "tower"),
            };

            string block = FewShotBuilder.Build(actions, targets, "Player", "Nothing has changed.");

            Assert.That(block, Is.EqualTo(string.Join("\n",
                "Examples of correct decisions:",
                "Player: \"How much for the Bread?\" -> action: quote_purchase, target: bread",
                "Player: \"That Bread smells good.\" -> action: none, target: no_target",
                "Player: \"Got any work for me?\" -> action: offer_quest, target: no_target",
                "Player: \"Any rumours about the wagon?\" -> action: none, target: no_target",
                "Player: \"Proceed to the Tower.\" -> action: move_to, target: tower",
                "Player: \"Nothing has changed.\" -> action: none, target: no_target",
                "Player: \"How much for the Atlantis?\" -> action: none, target: no_target (not an available target)")));
        }

        [Test]
        public void WithoutCounterExamplesTheBlockIsWhatItWas()
        {
            var targets = Registry("bread");
            var plain = new List<ActionDefinition> { Action("quote_purchase", "How much for the {0}?", true, "bread") };
            var blank = new List<ActionDefinition> { Countered(Action("quote_purchase", "How much for the {0}?", true, "bread"), "  ") };
            var countered = new List<ActionDefinition> { Countered(Action("quote_purchase", "How much for the {0}?", true, "bread"), "That {0} smells good.") };

            string without = FewShotBuilder.Build(plain, targets, "Player", "Nothing has changed.");

            Assert.That(FewShotBuilder.Build(blank, targets, "Player", "Nothing has changed."), Is.EqualTo(without),
                "A blank counter-example adds nothing.");
            Assert.That(FewShotBuilder.Build(countered, targets, "Player", "Nothing has changed.", counterExamples: false), Is.EqualTo(without),
                "Off leaves every counter-example out, byte for byte.");
            Assert.That(Lines(FewShotBuilder.Build(countered, targets, "Player", "Nothing has changed.")), Is.EqualTo(Lines(without) + 1),
                "On by default: what a developer wrote is shown.");
        }

        [Test]
        public void ACounterExampleWithAPlaceholderAndNothingToFillItIsLeftOut()
        {
            var targets = Registry("tower");
            var actions = new List<ActionDefinition>
            {
                Countered(Action("give_item", "Hand over the {0}.", true, "sword"), "Nice {0} you have."),   // sword not registered
                Countered(Action("wave", "Wave at me.", false), "I saw you near the {0} earlier."),        // no preferred target at all
            };

            string block = FewShotBuilder.Build(actions, targets);

            Assert.That(block, Does.Not.Contain("{0}"), "Never sent with the placeholder in it.");
            Assert.That(block, Does.Not.Contain("Nice"));
            Assert.That(block, Does.Not.Contain("I saw you"));
            Assert.That(block, Does.Contain("\"Wave at me.\" -> action: wave"), "The action's own example is unaffected.");
        }

        // ---- Statements (#190, G14) ----------------------------------------------------------------

        [Test]
        public void AWrittenStatementFollowsItsExampleAndTheRefusalNeverHasOne()
        {
            var targets = Registry("bread", "tower");
            var quote = Countered(Action("quote_purchase", "How much for the {0}?", true, "bread"), "That {0} smells good.");
            quote.ExampleStatement = "The {0} is three gold.";
            quote.CounterExampleStatement = "Baked it this morning.";
            var offer = Action("offer_quest", "Got any work for me?", false);
            offer.ExampleStatement = "Rats in the cellar, if you're brave.";
            var actions = new List<ActionDefinition> { quote, offer, Action("move_to", "Proceed to the {0}.", true, "tower") };

            string block = FewShotBuilder.Build(actions, targets, new PromptOptions
            {
                IdleExampleStimulus = "Nothing has changed.",
                IdleExampleStatement = "Quiet as ever.",
            });

            Assert.That(block, Is.EqualTo(string.Join("\n",
                "Examples of correct decisions:",
                "Player: \"How much for the Bread?\" -> action: quote_purchase, target: bread, statement: \"The Bread is three gold.\"",
                "Player: \"That Bread smells good.\" -> action: none, target: no_target, statement: \"Baked it this morning.\"",
                "Player: \"Got any work for me?\" -> action: offer_quest, target: no_target, statement: \"Rats in the cellar, if you're brave.\"",
                "Player: \"Proceed to the Tower.\" -> action: move_to, target: tower",
                "Player: \"Nothing has changed.\" -> action: none, target: no_target, statement: \"Quiet as ever.\"",
                "Player: \"How much for the Atlantis?\" -> action: none, target: no_target (not an available target)")),
                "The statement comes last, as the reply's field does; the refusal line has none.");
        }

        [Test]
        public void AQuoteInsideAStimulusOrAStatementCannotEndItEarly()
        {
            var targets = Registry("gate");
            var shout = Action("shout_warning", "Shout \"halt\" at the {0}.", true, "gate");
            shout.ExampleStatement = "\"Halt!\" I cried, at the C:\\gate.";
            var counter = Countered(Action("wave", "Wave at me.", false), "He said \"wave\", didn't he?");
            counter.CounterExampleStatement = "He did say \"wave\".";

            string block = FewShotBuilder.Build(new List<ActionDefinition> { shout, counter }, targets, new PromptOptions
            {
                IdleExampleStimulus = "The sign says \"closed\".",
                IdleExampleStatement = "So it does.",
            });

            Assert.That(block.Split('\n'), Is.SupersetOf(new[]
            {
                "Player: \"Shout \\\"halt\\\" at the Gate.\" -> action: shout_warning, target: gate, statement: \"\\\"Halt!\\\" I cried, at the C:\\\\gate.\"",
                "Player: \"He said \\\"wave\\\", didn't he?\" -> action: none, target: no_target, statement: \"He did say \\\"wave\\\".\"",
                "Player: \"The sign says \\\"closed\\\".\" -> action: none, target: no_target, statement: \"So it does.\"",
            }), "Quotes and backslashes are escaped as JSON escapes them, so each quoted text ends where it should.");
        }

        [Test]
        public void WithStatementsOffOrUnwrittenTheBlockIsWhatItWas()
        {
            var targets = Registry("bread");
            var plain = new List<ActionDefinition> { Countered(Action("quote_purchase", "How much for the {0}?", true, "bread"), "That {0} smells good.") };
            var stated = Countered(Action("quote_purchase", "How much for the {0}?", true, "bread"), "That {0} smells good.");
            stated.ExampleStatement = "Three gold.";
            stated.CounterExampleStatement = "Fresh today.";
            var withStatements = new List<ActionDefinition> { stated };

            string before = FewShotBuilder.Build(plain, targets, "Player", "Nothing has changed.");

            Assert.That(FewShotBuilder.Build(plain, targets, new PromptOptions { IdleExampleStimulus = "Nothing has changed." }), Is.EqualTo(before),
                "Both overloads give the same block when nothing new is set.");
            Assert.That(FewShotBuilder.Build(withStatements, targets, new PromptOptions
            {
                IdleExampleStimulus = "Nothing has changed.",
                IdleExampleStatement = "Quiet as ever.",
                ExampleStatements = false,
            }), Is.EqualTo(before), "Off leaves every statement out, byte for byte.");
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

        private static ActionDefinition Countered(ActionDefinition action, string counterExample)
        {
            action.CounterExampleStimulus = counterExample;
            return action;
        }

        private static int Lines(string block) => block.Split('\n').Length;
    }
}
