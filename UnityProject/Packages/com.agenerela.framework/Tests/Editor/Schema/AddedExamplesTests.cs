using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // DecideOptions.AddedExamples (#151): examples a call adds to its actions' and profile's, for an
    // A/B run of one example in one arm, such as a free choice answered with an action. Off by
    // default, and never shows an answer the call could not give. Every await of DecideAsync goes
    // through Fixtures.NotCancelled.
    public sealed class AddedExamplesTests
    {
        private const string FreeChoice = "Do whatever you think best.";
        private const string Shown = "Player: \"Do whatever you think best.\" -> action: move_to, target: tower";

        private static DecideOptions Adding(params FewShotExample[] examples) =>
            new DecideOptions { AddedExamples = new List<FewShotExample>(examples) };

        private static FewShotExample ToTheTower(string statement = "I'll keep watch from the tower.") =>
            new FewShotExample(FreeChoice, "move_to", "tower", statement);

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        private static async Task<string> Block(DecideOptions options)
        {
            var fake = new FakeProvider("{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}");
            var result = await Fixtures.NotCancelled(Fixtures.VillageGuard(fake).DecideAsync("Report.", options));
            return result.Request.FewShotBlock;
        }

        [Test]
        public async Task NoneByDefaultAndAnEmptyListChangesNothing()
        {
            Assert.That(new DecideOptions().AddedExamples, Is.Null);

            string plain = await Block(null);
            Assert.That(await Block(Adding()), Is.EqualTo(plain));
            Assert.That(plain, Does.Not.Contain(FreeChoice));
        }

        [Test]
        public async Task AnAddedExampleIsShownAfterTheActionsAndBeforeTheIdleExample()
        {
            string block = await Block(Adding(ToTheTower()));

            Assert.That(block, Does.Contain(Shown + ", statement: \"I'll keep watch from the tower.\""));
            int added = block.IndexOf(FreeChoice, System.StringComparison.Ordinal);
            Assert.That(added, Is.GreaterThan(block.IndexOf("Engage the", System.StringComparison.Ordinal)), "After the actions' own examples.");
            Assert.That(added, Is.LessThan(block.IndexOf("You seem well rested today.", System.StringComparison.Ordinal)), "Before the idle example.");
        }

        [Test]
        public async Task ItsStatementFollowsTheCallsNameAndItsExampleStatementsSwitch()
        {
            var dialogue = Adding(ToTheTower());
            dialogue.StatementFieldName = DecisionSchema.DialogueFieldName;
            Assert.That(await Block(dialogue), Does.Contain(Shown + ", dialogue: \"I'll keep watch from the tower.\""));

            var bare = Adding(ToTheTower());
            bare.ExampleStatements = false;
            string block = await Block(bare);
            Assert.That(block, Does.Contain(Shown + "\n").Or.EndWith(Shown));
            Assert.That(block, Does.Not.Contain("keep watch"));
        }

        [TestCase("stop_following", null, TestName = "An action not available now")]
        [TestCase("dance", null, TestName = "An action not registered")]
        [TestCase("move_to", null, TestName = "No target for an action that needs one")]
        [TestCase("move_to", "bridge", TestName = "A target not registered")]
        [TestCase("none", "tower", TestName = "None with a target")]
        public async Task AnAnswerTheCallCouldNotGiveIsLeftOut(string action, string target)
        {
            string block = await Block(Adding(new FewShotExample(FreeChoice, action, target)));

            Assert.That(block, Does.Not.Contain(FreeChoice));
        }

        [Test]
        public async Task NoneWithNoTargetIsShown()
        {
            Assert.That(await Block(Adding(new FewShotExample(FreeChoice, "none"))),
                Does.Contain("Player: \"Do whatever you think best.\" -> action: none, target: no_target"));
        }

        [Test]
        public void AnExampleNeedsAStimulusAndAnAction()
        {
            Assert.That(() => new FewShotExample(" ", "move_to"), Throws.ArgumentException);
            Assert.That(() => new FewShotExample(FreeChoice, ""), Throws.ArgumentException);
            Assert.That(new FewShotExample(FreeChoice, "none", " ").TargetId, Is.EqualTo(TargetRegistry.NoTarget));
        }
    }
}
