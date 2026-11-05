using Agenerela.Editor.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Agenerela.Tests.UI
{
    /// <summary>The stylesheet, and the fixtures every Phase 5 UI test builds its tree with (#167).</summary>
    public sealed class EditorUiFixturesTests
    {
        [Test]
        public void TheStylesheetImportsCleanly()
        {
            var sheet = AgenerelaStyles.Sheet;

            Assert.That(sheet, Is.Not.Null, $"No stylesheet at {AgenerelaStyles.SheetPath}");
            Assert.That(sheet.importedWithErrors, Is.False);
            Assert.That(sheet.importedWithWarnings, Is.False);
        }

        [Test]
        public void ApplyingTheStylesTwiceAddsTheSheetOnce()
        {
            var root = new VisualElement();

            AgenerelaStyles.Apply(root);
            AgenerelaStyles.Apply(root);

            Assert.That(root.styleSheets.count, Is.EqualTo(1));
            Assert.That(root.ClassListContains(AgenerelaStyles.RootClass), Is.True);
        }

        [Test]
        public void AWindowIsBuiltAndQueriedByName()
        {
            using (var tree = EditorUiFixtures.Window<ProbeWindow>())
            {
                Assert.That(tree.Find<Label>(ProbeWindow.LabelName).text, Is.EqualTo("built"));
                Assert.That(tree.Window.position.width, Is.EqualTo(EditorUiFixtures.DefaultSize.x));
            }
        }

        [Test]
        public void FindingAMissingElementNamesTheOnesThere()
        {
            var root = new VisualElement();
            root.Add(new Label { name = "progress" });

            var failure = Assert.Throws<AssertionException>(() => root.Find<Label>("progres"));

            Assert.That(failure.Message, Does.Contain("progress (Label)"));
        }

        /// <summary>A window built the way a screen is, so the fixture's window path is tested on its own.</summary>
        internal sealed class ProbeWindow : EditorWindow
        {
            internal const string LabelName = "probe";

            private void CreateGUI()
            {
                rootVisualElement.Add(new Label("built") { name = LabelName });
            }
        }
    }
}
