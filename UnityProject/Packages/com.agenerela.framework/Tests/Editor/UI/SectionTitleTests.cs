using System.Collections;
using Agenerela.Editor.UI;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Agenerela.Tests.UI
{
    /// <summary>The section title: small, spaced, muted capitals (#167).</summary>
    public sealed class SectionTitleTests
    {
        [TearDown]
        public void CloseWindows() => EditorUiFixtures.CloseAll();

        [Test]
        public void ItIsInCapitalsHoweverItsTextIsSet()
        {
            var title = new SectionTitle("Expected answer");
            Assert.That(title.text, Is.EqualTo("EXPECTED ANSWER"));

            title.text = "23 left to label · command_with_target";
            Assert.That(title.text, Is.EqualTo("23 LEFT TO LABEL · COMMAND_WITH_TARGET"));

            title.text = null;
            Assert.That(title.text, Is.Empty);
        }

        [UnityTest]
        public IEnumerator AShownTitleIsSpacedMutedAndSmallerThanItsText()
        {
            var title = new SectionTitle("Checks");
            var body = new Label("the state allows move_to");
            var section = new VisualElement();
            AgenerelaStyles.Apply(section);
            section.Add(title);
            section.Add(body);
            using (EditorUiFixtures.Host(section))
            {
                yield return EditorUiFixtures.Until(() => title.resolvedStyle.letterSpacing > 0f, "the title's letter spacing resolves");

                Assert.That(title.resolvedStyle.color, Is.Not.EqualTo(body.resolvedStyle.color));
                Assert.That(title.resolvedStyle.fontSize, Is.LessThan(body.resolvedStyle.fontSize));
            }
        }
    }
}
