using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// How large the screen's UI is drawn. The layout is designed at 1920 × 1080 in panel units; a
    /// larger screen draws it larger, so text and icons stay sharp at the screen's own pixels
    /// rather than being scaled up as an image.
    /// </summary>
    /// <remarks>
    /// Never below 1: at 1366 × 768 the layout switches to its compact form instead (decision 28
    /// says 1366 × 768 must work). The smaller of the two ratios keeps a wide screen from pushing
    /// the panels off its top and bottom.
    /// </remarks>
    public static class UiScale
    {
        public const float ReferenceWidth = 1920, ReferenceHeight = 1080;

        /// <summary>Panel pixels per layout unit for a screen of this size: 1 at 1080p, 1.33 at 1440p, 2 at 2160p.</summary>
        public static float For(int width, int height) =>
            Mathf.Max(1f, Mathf.Min(width / ReferenceWidth, height / ReferenceHeight));

        /// <summary>Whether the layout needs its compact form: narrower than 1600 layout units.</summary>
        public static bool Compact(int width, int height) => width / For(width, height) < 1600;
    }
}
