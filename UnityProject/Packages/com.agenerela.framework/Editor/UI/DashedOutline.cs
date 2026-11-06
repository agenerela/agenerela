using UnityEngine;
using UnityEngine.UIElements;

namespace Agenerela.Editor.UI
{
    /// <summary>
    /// A dashed outline, which USS cannot draw (#167). The wireframes use one for something that
    /// is there but not in play: a chip that is not offered, a button for an open choice.
    /// </summary>
    /// <remarks>
    /// <see cref="Attach"/> once; then the <c>ag-dashed</c> class turns the outline on and off,
    /// so a stylesheet and a test can both see whether it is drawn. It follows the element's
    /// rounded corners and is stroked in its border colour, so the USS for a dashed state sets
    /// <c>border-width: 0</c> and keeps <c>border-color</c> and <c>border-radius</c>.
    /// </remarks>
    internal static class DashedOutline
    {
        internal const string UssClassName = "ag-dashed";

        private const float DashLength = 3f;
        private const float DashGap = 2f;

        /// <summary>Lets <paramref name="element"/> draw the outline while it has the <c>ag-dashed</c> class.</summary>
        internal static void Attach(VisualElement element)
        {
            element.generateVisualContent += context => Draw(element, context);
        }

        /// <summary>Turns the outline on or off.</summary>
        internal static void Show(VisualElement element, bool dashed)
        {
            element.EnableInClassList(UssClassName, dashed);
            element.MarkDirtyRepaint();
        }

        private static void Draw(VisualElement element, MeshGenerationContext context)
        {
            if (!element.ClassListContains(UssClassName))
            {
                return;
            }

            const float inset = 0.5f;
            float left = inset;
            float top = inset;
            float right = element.layout.width - inset;
            float bottom = element.layout.height - inset;
            if (right <= left || bottom <= top)
            {
                return;
            }

            // One radius for all four corners, no more than half the shorter side: a chip's 9 px
            // on an 18 px height makes a pill, a button's 3 px a rounded rectangle.
            var style = element.resolvedStyle;
            float radius = Mathf.Clamp(style.borderTopLeftRadius, 0f, Mathf.Min(right - left, bottom - top) / 2f);

            // Clockwise from the top edge; angles run clockwise from +x, with y pointing down.
            var painter = context.painter2D;
            painter.lineWidth = 1f;
            painter.strokeColor = style.borderTopColor;
            painter.SetDashPattern(DashLength, DashGap);
            painter.BeginPath();
            painter.MoveTo(new Vector2(left + radius, top));
            painter.LineTo(new Vector2(right - radius, top));
            Corner(painter, right - radius, top + radius, radius, 270f);
            painter.LineTo(new Vector2(right, bottom - radius));
            Corner(painter, right - radius, bottom - radius, radius, 0f);
            painter.LineTo(new Vector2(left + radius, bottom));
            Corner(painter, left + radius, bottom - radius, radius, 90f);
            painter.LineTo(new Vector2(left, top + radius));
            Corner(painter, left + radius, top + radius, radius, 180f);
            painter.ClosePath();
            painter.Stroke();
        }

        private static void Corner(Painter2D painter, float x, float y, float radius, float from)
        {
            if (radius > 0f)
            {
                painter.Arc(new Vector2(x, y), radius, Angle.Degrees(from), Angle.Degrees(from + 90f), ArcDirection.Clockwise);
            }
        }
    }
}
