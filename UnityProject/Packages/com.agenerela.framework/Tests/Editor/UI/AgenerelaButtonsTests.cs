using System.Collections;
using Agenerela.Editor.UI;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Agenerela.Tests.UI
{
    /// <summary>The primary and dashed buttons the wireframes add to Unity's own (#167).</summary>
    public sealed class AgenerelaButtonsTests
    {
        [TearDown]
        public void CloseWindows() => EditorUiFixtures.CloseAll();

        [Test]
        public void APrimaryButtonIsMarkedAndStillClicks()
        {
            int saves = 0;
            var save = AgenerelaButtons.Primary("Save answer", () => saves++);

            Assert.That(save.ClassListContains(AgenerelaButtons.PrimaryClass), Is.True);
            Assert.That(save.ClassListContains(DashedOutline.UssClassName), Is.False);
            using (EditorUiFixtures.Host(save))
            {
                EditorUiFixtures.Click(save);
            }

            Assert.That(saves, Is.EqualTo(1));
        }

        [Test]
        public void ADashedButtonDrawsItsOutlineAndStillClicks()
        {
            int opened = 0;
            var open = AgenerelaButtons.Dashed("Open choice", () => opened++);

            Assert.That(open.ClassListContains(AgenerelaButtons.DashedClass), Is.True);
            Assert.That(open.ClassListContains(DashedOutline.UssClassName), Is.True);
            using (EditorUiFixtures.Host(open))
            {
                EditorUiFixtures.Click(open);
            }

            Assert.That(opened, Is.EqualTo(1));
        }

        [Test]
        public void ADashedOutlineIsAttachedOnceHoweverOftenItIsAsked()
        {
            var open = AgenerelaButtons.Dashed("Open choice", () => { });
            AgenerelaButtons.MakeDashed(open);

            Assert.That(DashedOutline.Attach(open), Is.False, "the button's outline was attached again");
            Assert.That(DashedOutline.Attach(new Chip("attack_target")), Is.False, "a chip attaches its own outline");
            Assert.That(DashedOutline.Attach(new Button()), Is.True);
        }

        [Test]
        public void AButtonFromUxmlCanBeMadeEither()
        {
            var loaded = new Button { name = "settle" };

            Assert.That(AgenerelaButtons.MakePrimary(loaded), Is.SameAs(loaded));
            Assert.That(loaded.ClassListContains(AgenerelaStyles.RootClass), Is.True);
        }

        // Unity's own :hover:enabled rules outrank one class, so the fill is checked on a shown
        // button rather than trusted to the stylesheet.
        [UnityTest]
        public IEnumerator AShownPrimaryButtonIsFilledInTheTextColour()
        {
            // The text colour is a label's: Unity gives a plain button a lighter text of its own.
            var text = new Label("Go to the bridge.");
            var plain = new Button { text = "Skip" };
            var primary = AgenerelaButtons.Primary("Save answer", () => { });
            var row = new VisualElement();
            AgenerelaStyles.Apply(row);
            row.Add(text);
            row.Add(plain);
            row.Add(primary);
            using (EditorUiFixtures.Host(row))
            {
                yield return EditorUiFixtures.Until(
                    () => primary.resolvedStyle.backgroundColor == text.resolvedStyle.color,
                    "the primary button's fill is the text colour");

                Assert.That(primary.resolvedStyle.backgroundColor, Is.Not.EqualTo(plain.resolvedStyle.backgroundColor));
                Assert.That(primary.resolvedStyle.color, Is.Not.EqualTo(text.resolvedStyle.color));
            }
        }
    }
}
