using System.Collections;
using Agenerela.Editor.UI;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Agenerela.Tests.UI
{
    /// <summary>The chip: an action or target id, struck through when it is not offered (#167).</summary>
    public sealed class ChipTests
    {
        [Test]
        public void AnOfferedChipShowsItsIdAsWritten()
        {
            var chip = new Chip("follow_player");

            Assert.That(chip.text, Is.EqualTo("follow_player"));
            Assert.That(chip.enableRichText, Is.False);
            Assert.That(chip.ClassListContains(Chip.UssClassName), Is.True);
            Assert.That(chip.ClassListContains(Chip.OffUssClassName), Is.False);
        }

        [Test]
        public void AChipNotOfferedIsStruckThroughAndDashed()
        {
            var chip = new Chip("attack_target", offered: false);

            Assert.That(chip.Id, Is.EqualTo("attack_target"));
            Assert.That(chip.text, Is.EqualTo("<s><noparse>attack_target</noparse></s>"));
            Assert.That(chip.enableRichText, Is.True);
            Assert.That(chip.ClassListContains(Chip.OffUssClassName), Is.True);
        }

        [Test]
        public void OfferingItAgainTakesTheStrikeOff()
        {
            var chip = new Chip("attack_target", offered: false) { Offered = true };

            Assert.That(chip.text, Is.EqualTo("attack_target"));
            Assert.That(chip.enableRichText, Is.False);
            Assert.That(chip.ClassListContains(Chip.OffUssClassName), Is.False);
        }

        [Test]
        public void AnIdIsNeverReadAsRichText()
        {
            Assert.That(new Chip("<b>gate</b>").enableRichText, Is.False);
            Assert.That(new Chip("<b>gate</b>", offered: false).text, Is.EqualTo("<s><noparse><b>gate</b></noparse></s>"));
        }

        [Test]
        public void AChipCarriesTheStylesheet()
        {
            var chip = new Chip("tower");

            Assert.That(chip.ClassListContains(AgenerelaStyles.RootClass), Is.True);
            Assert.That(chip.styleSheets.Contains(AgenerelaStyles.Sheet), Is.True);
        }

        [UnityTest]
        public IEnumerator AShownChipIsMonospaceThroughTheStylesheetsVariables()
        {
            var chip = new Chip("training_dummy");
            using (EditorUiFixtures.Host(chip))
            {
                yield return EditorUiFixtures.Until(
                    () => chip.resolvedStyle.unityFontDefinition.fontAsset != null,
                    "the chip's font resolves");

                Assert.That(chip.resolvedStyle.unityFontDefinition.fontAsset.name, Does.Contain("Mono"));
            }
        }

        // A variable that does not resolve is not an error in USS: the property silently keeps
        // Unity's default. This is how --ag-text-muted once pointed at a theme colour that never
        // reaches an editor window, and both chips came out the same.
        [UnityTest]
        public IEnumerator AShownChipNotOfferedIsMutedAndBothAreTheSmallSize()
        {
            var offered = new Chip("move_to");
            var notOffered = new Chip("attack_target", offered: false);
            var row = new UnityEngine.UIElements.VisualElement();
            row.Add(offered);
            row.Add(notOffered);
            using (EditorUiFixtures.Host(row))
            {
                yield return EditorUiFixtures.Until(
                    () => offered.resolvedStyle.unityFontDefinition.fontAsset != null,
                    "the chips' styles resolve");

                Assert.That(notOffered.resolvedStyle.color, Is.Not.EqualTo(offered.resolvedStyle.color));
                Assert.That(offered.resolvedStyle.fontSize, Is.EqualTo(11f));
                Assert.That(notOffered.resolvedStyle.fontSize, Is.EqualTo(11f));
            }
        }
    }
}
