using System.Collections;
using Agenerela.Editor.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Agenerela.Tests.UI
{
    /// <summary>The stylesheet, and the fixtures every Phase 5 UI test builds its tree with (#167).</summary>
    public sealed class EditorUiFixturesTests
    {
        [TearDown]
        public void CloseWindows() => EditorUiFixtures.CloseAll();

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

        // A screen left open across a skin switch must not keep the other skin's muted colour. The
        // test stands in for the switch by giving the element the wrong skin's class, then checks the
        // restyle that follows puts it right.
        [UnityTest]
        public IEnumerator TheSkinClassIsPutRightWheneverStylesResolve()
        {
            bool light = !EditorGUIUtility.isProSkin;
            var box = new VisualElement();
            AgenerelaStyles.Apply(box);
            int resolved = 0;
            box.RegisterCallback<CustomStyleResolvedEvent>(_ => resolved++);
            using (EditorUiFixtures.Host(box))
            {
                yield return EditorUiFixtures.Until(() => resolved > 0, "the box's styles resolve");

                box.EnableInClassList(AgenerelaStyles.LightClass, !light);

                yield return EditorUiFixtures.Until(
                    () => box.ClassListContains(AgenerelaStyles.LightClass) == light,
                    "the light-skin class matches the editor's skin again");
            }
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
