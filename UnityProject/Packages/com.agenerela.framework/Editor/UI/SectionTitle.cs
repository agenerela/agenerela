using UnityEngine.UIElements;

namespace Agenerela.Editor.UI
{
    /// <summary>
    /// A section's title, in small, spaced, muted capitals (#167): "EXPECTED ANSWER", "CHECKS",
    /// "23 LEFT TO LABEL · COMMAND_WITH_TARGET". Every Phase 5 screen heads its sections this way.
    /// </summary>
    /// <remarks>
    /// USS has no <c>text-transform</c>, so the title upper-cases its own text, however it is set:
    /// in code, from UXML's <c>text</c> attribute, or by a binding.
    /// </remarks>
    [UxmlElement]
    internal partial class SectionTitle : Label
    {
        internal const string UssClassName = "ag-section-title";

        public SectionTitle() : this("")
        {
        }

        internal SectionTitle(string text)
        {
            AgenerelaStyles.Apply(this);
            AddToClassList(UssClassName);
            this.text = text;
        }

        public override string text
        {
            get => base.text;
            set => base.text = (value ?? "").ToUpperInvariant();
        }
    }
}
