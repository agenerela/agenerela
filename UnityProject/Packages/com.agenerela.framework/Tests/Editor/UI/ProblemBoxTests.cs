using Agenerela.Editor.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Agenerela.Tests.UI
{
    /// <summary>The problem box: a message and an optional fix button (screen 1 ⑥, #167).</summary>
    public sealed class ProblemBoxTests
    {
        [TearDown]
        public void CloseWindows() => EditorUiFixtures.CloseAll();

        [Test]
        public void ItShowsItsMessage()
        {
            var box = new ProblemBox("Two actions are called move_to.");

            Assert.That(box.Find<Label>(ProblemBox.MessageName).text, Is.EqualTo("Two actions are called move_to."));
        }

        [Test]
        public void WithoutAFixThereIsNoButton()
        {
            var box = new ProblemBox("The name is empty.");

            Assert.That(box.HasFix, Is.False);
            Assert.That(box.Q<Button>(ProblemBox.FixName), Is.Null);
        }

        [Test]
        public void TheFixButtonCallsBackOncePerClick()
        {
            int fixes = 0;
            var box = new ProblemBox("Two actions are called move_to.");
            box.SetFix("Rename the second", () => fixes++);

            using (var tree = EditorUiFixtures.Host(box))
            {
                var fix = tree.Find<Button>(ProblemBox.FixName);
                Assert.That(fix.text, Is.EqualTo("Rename the second"));

                EditorUiFixtures.Click(fix);

                Assert.That(fixes, Is.EqualTo(1));
            }
        }

        [Test]
        public void ClearingTheFixRemovesTheButton()
        {
            var box = new ProblemBox("The name is empty.");
            box.SetFix("Use the asset's name", () => { });

            box.ClearFix();

            Assert.That(box.HasFix, Is.False);
            Assert.That(box.Q<Button>(ProblemBox.FixName), Is.Null);
        }

        [Test]
        public void AnErrorIsMarkedAsOne()
        {
            var box = new ProblemBox("No actions.") { Severity = ProblemSeverity.Error };

            Assert.That(box.ClassListContains(ProblemBox.UssClassName + "--error"), Is.True);
            Assert.That(box.ClassListContains(ProblemBox.UssClassName + "--warning"), Is.False);
            Assert.That(box.Find<Image>(ProblemBox.IconName).image, Is.Not.Null);
        }
    }
}
