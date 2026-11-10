using System.Collections.Generic;
using System.Linq;
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

        // ---- The example's target (#190, Living Vale #20) -----------------------------------------

        [Test]
        public void WithTheFallbackAnExampleNamesTheFirstTargetItsActionTakes()
        {
            var targets = new TargetRegistry();
            targets.Register("north_tower", new object(), "place");
            targets.Register("stock_bread_one", new Named("loaf of bread"), "stock");
            targets.Register("stock_potion_one", new Named("healing potion"), "stock");
            var buy = Countered(Action("quote_purchase", "How much for the {0}?", true), "That {0} smells good.");
            buy.TargetCategories = new[] { "stock" };
            var stale = Action("move_to", "Proceed to the {0}.", true, "old_mill");   // not registered
            var actions = new List<ActionDefinition> { buy, stale };

            string off = FewShotBuilder.Build(actions, targets);
            string on = FewShotBuilder.Build(actions, targets, new PromptOptions { ExampleTargetFallback = true });

            Assert.That(off, Does.Not.Contain("quote_purchase").And.Not.Contain("move_to"),
                "Off, the default: no registered preferred target leaves the example out, as before.");
            Assert.That(on.Split('\n'), Is.SupersetOf(new[]
            {
                "Player: \"How much for the loaf of bread?\" -> action: quote_purchase, target: stock_bread_one",
                "Player: \"That loaf of bread smells good.\" -> action: none, target: no_target",
                "Player: \"Proceed to the NorthTower.\" -> action: move_to, target: north_tower",
            }), "The first target each action takes, called by its INamedTarget name when it has one.");
        }

        [Test]
        public void ANamedTargetIsCalledByItsNameAndABlankNameFallsBackToTheId()
        {
            var targets = new TargetRegistry();
            targets.Register("stock_bread_one", new Named("  loaf of bread "));
            targets.Register("stock_potion_one", new Named("  "));
            var actions = new List<ActionDefinition>
            {
                Action("quote_purchase", "How much for the {0}?", true, "stock_bread_one"),
                Action("sell_item", "I'd like to sell the {0}.", true, "stock_potion_one"),
            };

            var lines = FewShotBuilder.Build(actions, targets).Split('\n');

            Assert.That(lines[1], Is.EqualTo("Player: \"How much for the loaf of bread?\" -> action: quote_purchase, target: stock_bread_one"));
            Assert.That(lines[2], Is.EqualTo("Player: \"I'd like to sell the StockPotionOne.\" -> action: sell_item, target: stock_potion_one"));
        }

        [Test]
        public void TheFallbackNeverNamesATargetTheStimulusMentions()
        {
            var targets = new TargetRegistry();
            targets.Register("north_tower", new object());
            targets.Register("docks", new object());
            targets.Register("inn", new object());
            targets.Register("stock_potion_one", new Named("healing potion"));
            targets.Register("well", new object());
            var move = Countered(Action("move_to", "Go and stand by the {0}.", true), "The {0} looks quiet.");
            var actions = new List<ActionDefinition> { move };

            string Block(string stimulus) => FewShotBuilder.Build(actions, targets,
                new PromptOptions { ExampleTargetFallback = true, SpacedExampleNames = true, Stimulus = stimulus });

            Assert.That(Block("Meet me at the towers by the dock, near the inn. Bring a potion!"), Does.Contain(
                "Player: \"Go and stand by the well.\" -> action: move_to, target: well\n" +
                "Player: \"The well looks quiet.\" -> action: none, target: no_target"),
                "towers mentions north_tower, dock docks, inn inn, and potion the target named healing potion.");
            Assert.That(Block("Well, the north tower, docks, inn and potions."), Does.Not.Contain("action: move_to")
                .And.Not.Contain("looks quiet"), "Every target mentioned leaves the example and its counter-example out.");

            var few = new TargetRegistry();
            few.Register("the_old_mill", new object());
            few.Register("inn", new object());
            string First(string stimulus) => FewShotBuilder.Build(actions, few,
                new PromptOptions { ExampleTargetFallback = true, Stimulus = stimulus }).Split('\n')[1];

            Assert.That(First("Look at the cart."), Does.EndWith("target: the_old_mill"), "'the' is in every name: it mentions nothing.");
            Assert.That(First("The old cart."), Does.EndWith("target: inn"), "'old', three letters, is said.");
            Assert.That(First("Oldest carts first."), Does.EndWith("target: inn"), "'oldest' starts with 'old'.");
            Assert.That(First("Stand in a line, mil."), Does.EndWith("target: the_old_mill"),
                "'in' is too short to stand for 'inn', and 'mil' for 'mill'.");
            Assert.That(First("Innkeepers and millers."), Does.Not.Contain("action: move_to"),
                "'innkeepers' starts with 'inn' and 'millers' with 'mill': both mentioned, the example goes.");
            Assert.That(First("The dock workers."), Does.EndWith("target: the_old_mill"));
        }

        [Test]
        public void ShortNamesNamesOfLittleWordsAndAcronymsAreMentionsToo()
        {
            var actions = new List<ActionDefinition> { Action("move_to", "Go and stand by the {0}.", true) };
            string First(string stimulus, params (string id, object target)[] registered)
            {
                var targets = new TargetRegistry();
                foreach (var (id, target) in registered) targets.Register(id, target);
                targets.Register("well", new object());
                return FewShotBuilder.Build(actions, targets, new PromptOptions
                {
                    ExampleTargetFallback = true, SpacedExampleNames = true, Stimulus = stimulus,
                }).Split('\n')[1];
            }

            Assert.That(First("Attack the ox!", ("ox", new object())), Does.EndWith("target: well"), "'ox', said whole.");
            Assert.That(First("Fetch Bo.", ("x9", new Named("Bo"))), Does.EndWith("target: well"), "A two-letter INamedTarget name, said whole.");
            Assert.That(First("Go to stall 7.", ("stall_7", new object())), Does.EndWith("target: well"));
            Assert.That(First("Box seven.", ("ox", new object())), Does.EndWith("target: ox"), "'ox' only starts nothing under three letters.");
            Assert.That(First("Look to the end.", ("to_the", new object())), Does.EndWith("target: well"),
                "A name of nothing but little words is a mention when all of them are said.");
            Assert.That(First("Go to it.", ("to_the", new object())), Does.EndWith("target: to_the"));
            Assert.That(First("Attack the guard.", ("NPCGuard", new object())), Does.EndWith("target: well"),
                "NPCGuard's words are 'npc' and 'guard'.");
            Assert.That(First("Hello.", ("NPCGuard", new object())), Does.StartWith("Player: \"Go and stand by the npc guard.\""));
        }

        [Test]
        public void TheRefusalNeverNamesAPlaceAnOfferedTargetIsCalled()
        {
            string Refusal(params string[] names)
            {
                var targets = new TargetRegistry();
                for (int i = 0; i < names.Length; i++) targets.Register("city_" + i, new Named(names[i]));
                var actions = new List<ActionDefinition> { Action("travel", "Travel to {0}.", true, "city_0") };
                return FewShotBuilder.Build(actions, targets).Split('\n').Last();
            }

            Assert.That(Refusal("Rome"), Does.StartWith("Player: \"Travel to Atlantis.\" -> action: none"));
            Assert.That(Refusal("atlantis"), Does.StartWith("Player: \"Travel to Avalon.\" -> action: none"),
                "A target an example calls Atlantis, by any case, is offered: the refusal names Avalon.");
            Assert.That(Refusal("Atlantis", "Avalon"), Does.StartWith("Player: \"Travel to Atlantis.\" -> action: travel"),
                "Both taken: no refusal at all, rather than one contradicting the example.");
        }

        [Test]
        public void SpacedNamesSeparateTheIdsWordsAndTheRefusalDropsItsArticle()
        {
            var targets = new TargetRegistry();
            targets.Register("training_dummy", new object());
            targets.Register("village-gate", new object());
            targets.Register("NorthTower", new object());
            targets.Register("stock_one", new Named("Mara's Potion"));
            var actions = new List<ActionDefinition>
            {
                Action("attack_target", "Engage the {0}.", true, "training_dummy"),
                Action("open_gate", "Open the {0}.", true, "village-gate"),
                Action("move_to", "Proceed to the {0}.", true, "NorthTower"),
                Action("buy", "I'll take {0}.", true, "stock_one"),
            };

            string glued = FewShotBuilder.Build(actions, targets);
            string spaced = FewShotBuilder.Build(actions, targets, new PromptOptions { SpacedExampleNames = true });

            Assert.That(glued, Does.Contain("\"Engage the TrainingDummy.\"").And.Contain("\"Proceed to the NorthTower.\"")
                .And.Contain("\"Engage the Atlantis.\""),
                "Off, the default, is what every example said before.");
            Assert.That(spaced.Split('\n'), Is.EqualTo(new[]
            {
                "Examples of correct decisions:",
                "Player: \"Engage the training dummy.\" -> action: attack_target, target: training_dummy",
                "Player: \"Open the village gate.\" -> action: open_gate, target: village-gate",
                "Player: \"Proceed to the north tower.\" -> action: move_to, target: NorthTower",
                "Player: \"I'll take Mara's Potion.\" -> action: buy, target: stock_one",
                "Player: \"Engage Atlantis.\" -> action: none, target: no_target (not an available target)",
            }), "Words separated and lowercase; an INamedTarget's name as written; no 'the Atlantis'.");
        }

        [Test]
        public void TheSpacedRefusalReadsAsEnglishForAnyArticle()
        {
            string Refusal(string stimulus) => FewShotBuilder.Build(
                new List<ActionDefinition> { Action("ask", stimulus, true) }, new TargetRegistry(),
                new PromptOptions { SpacedExampleNames = true }).Split('\n')[1];

            Assert.That(Refusal("The {0}, please."), Does.StartWith("Player: \"Atlantis, please.\""));
            Assert.That(Refusal("Housing is short; we need a {0}."), Does.StartWith("Player: \"Housing is short; we need Atlantis.\""));
            Assert.That(Refusal("Fetch an {0} for me."), Does.StartWith("Player: \"Fetch Atlantis for me.\""));
            Assert.That(Refusal("Take {0} home."), Does.StartWith("Player: \"Take Atlantis home.\""), "Nothing to drop is left alone.");
            Assert.That(Refusal("Bathe {0}."), Does.StartWith("Player: \"Bathe Atlantis.\""), "Only a whole-word article goes.");
            Assert.That(Refusal("Open the {0} gate."), Does.StartWith("Player: \"Open the Atlantis gate.\""),
                "A {0} that goes on into a longer phrase keeps its article.");
            Assert.That(Refusal("Bring a {0}-shaped token."), Does.StartWith("Player: \"Bring an Atlantis-shaped token.\""),
                "...as 'an', for the vowel.");
            Assert.That(Refusal("Go to the {0} now."), Does.StartWith("Player: \"Go to Atlantis now.\""));
        }

        private sealed class Named : INamedTarget
        {
            public Named(string name)
            {
                Name = name;
            }

            public string Name { get; }
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
