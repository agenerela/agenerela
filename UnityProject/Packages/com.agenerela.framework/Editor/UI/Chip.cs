using UnityEngine;
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
    /// <c>&lt;s&gt;</c> tag and the outline is stroked here. The id is never parsed as rich text.
    /// </remarks>
    [UxmlElement]
    internal partial class Chip : Label
    {
        internal const string UssClassName = "ag-chip";
        internal const string OffUssClassName = UssClassName + "--off";

        private const float DashLength = 3f;
        private const float DashGap = 2f;

        private string id = "";
        private bool offered = true;

        public Chip() : this("")
        {
        }

        internal Chip(string id, bool offered = true)
        {
            AgenerelaStyles.Apply(this);
            AddToClassList(UssClassName);
            generateVisualContent += DrawDashedOutline;
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

            // Rich text only for the strike, with the id inside <noparse>, so an id is always shown
            // as written.
            enableRichText = !offered;
            text = offered ? id : "<s><noparse>" + id + "</noparse></s>";
            MarkDirtyRepaint();
        }

        private void DrawDashedOutline(MeshGenerationContext context)
        {
            if (offered)
            {
                return;
            }

            const float inset = 0.5f;
            float left = inset;
            float top = inset;
            float right = layout.width - inset;
            float bottom = layout.height - inset;
            float radius = Mathf.Min(right - left, bottom - top) / 2f;
            if (radius <= 0f)
            {
                return;
            }

            // The same pill the USS gives an offered chip: straight top and bottom, round ends.
            // Angles run clockwise from +x, with y pointing down.
            var painter = context.painter2D;
            painter.lineWidth = 1f;
            painter.strokeColor = resolvedStyle.borderTopColor;
            painter.SetDashPattern(DashLength, DashGap);
            painter.BeginPath();
            painter.MoveTo(new Vector2(left + radius, top));
            painter.LineTo(new Vector2(right - radius, top));
            painter.Arc(new Vector2(right - radius, top + radius), radius, Angle.Degrees(270f), Angle.Degrees(90f), ArcDirection.Clockwise);
            painter.LineTo(new Vector2(left + radius, bottom));
            painter.Arc(new Vector2(left + radius, top + radius), radius, Angle.Degrees(90f), Angle.Degrees(270f), ArcDirection.Clockwise);
            painter.ClosePath();
            painter.Stroke();
        }
    }
}
