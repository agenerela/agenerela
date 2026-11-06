using UnityEngine.UIElements;

namespace Agenerela.Editor.UI
{
    /// <summary>
    /// An action or target id, drawn as a small pill in monospace (#167). A chip that is not
    /// offered, an action the model cannot choose right now, has its text struck through and a
    /// dashed outline instead of a fill: illegal options are absent from the schema, and the
    /// screens show which ones were left out.
    /// </summary>
    /// <remarks>
    /// USS has neither a dashed border nor <c>text-decoration</c>, so the strike is a rich-text
    /// <c>&lt;s&gt;</c> tag and the outline is <see cref="DashedOutline"/>. The id is never parsed
    /// as rich text.
    /// </remarks>
    [UxmlElement]
    internal partial class Chip : Label
    {
        internal const string UssClassName = "ag-chip";
        internal const string OffUssClassName = UssClassName + "--off";

        private string id = "";
        private bool offered = true;

        public Chip() : this("")
        {
        }

        internal Chip(string id, bool offered = true)
        {
            AgenerelaStyles.Apply(this);
            AddToClassList(UssClassName);
            DashedOutline.Attach(this);
            this.id = id ?? "";
            this.offered = offered;
            Refresh();
        }

        /// <summary>The action or target id the chip shows.</summary>
        [UxmlAttribute]
        internal string Id
        {
            get => id;
            set
            {
                id = value ?? "";
                Refresh();
            }
        }

        /// <summary>False for an action that is not offered: struck through, outline dashed.</summary>
        [UxmlAttribute]
        internal bool Offered
        {
            get => offered;
            set
            {
                offered = value;
                Refresh();
            }
        }

        private void Refresh()
        {
            EnableInClassList(OffUssClassName, !offered);
            DashedOutline.Show(this, !offered);

            // Rich text only for the strike, with the id inside <noparse>, so an id is always shown
            // as written.
            enableRichText = !offered;
            text = offered ? id : "<s><noparse>" + id + "</noparse></s>";
        }
    }
}
