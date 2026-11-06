using System;
using LivingVale.Game;

namespace LivingVale.World
{
    /// <summary>
    /// G8a: the sums behind the compass strip, the map and the NPC name tags, in plain C# so they are
    /// tested without a scene. World axes: +z is north, +x is east; bearings are degrees clockwise from
    /// north, as on a real compass.
    /// </summary>
    public static class NavigationMath
    {
        /// <summary>The bearing from one ground point to another, in [0, 360).</summary>
        public static float Bearing(float fromX, float fromZ, float toX, float toZ)
        {
            return Normalise((float)(Math.Atan2(toX - fromX, toZ - fromZ) * 180.0 / Math.PI));
        }

        /// <summary>The bearing a ground direction points along, in [0, 360).</summary>
        public static float Heading(float forwardX, float forwardZ) => Bearing(0f, 0f, forwardX, forwardZ);

        /// <summary>An angle wrapped into [0, 360).</summary>
        public static float Normalise(float degrees)
        {
            float wrapped = degrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }

        /// <summary>How far <paramref name="bearing"/> is to the right of <paramref name="heading"/>, in [−180, 180).</summary>
        public static float Relative(float bearing, float heading)
        {
            float delta = Normalise(bearing - heading);
            return delta >= 180f ? delta - 360f : delta;
        }

        /// <summary>
        /// Where a bearing sits on a compass strip <paramref name="width"/> wide that shows
        /// <paramref name="span"/> degrees centred on <paramref name="heading"/>: the x offset from the
        /// strip's centre, or false when the bearing is off the strip.
        /// </summary>
        public static bool StripOffset(float bearing, float heading, float span, float width, out float x)
        {
            float relative = Relative(bearing, heading);
            float half = span * 0.5f;
            x = relative / half * (width * 0.5f);
            return relative >= -half && relative <= half;
        }

        /// <summary>
        /// A world ground point on a square map <paramref name="size"/> wide drawn over the whole
        /// valley, as offsets from the map's bottom-left corner: west to east is left to right, south
        /// to north is bottom to top. Points outside the valley are clamped to the edge.
        /// </summary>
        public static (float u, float v) ToMap(float x, float z, float size,
            float halfExtent = ValePlaces.GroundHalfExtent)
        {
            float u = (x + halfExtent) / (2f * halfExtent);
            float v = (z + halfExtent) / (2f * halfExtent);
            return (Clamp01(u) * size, Clamp01(v) * size);
        }

        /// <summary>A whole-metre distance for the HUD: "12 m", or "1.2 km" past a kilometre.</summary>
        public static string Distance(float metres)
        {
            if (metres >= 1000f)
            {
                return $"{metres / 1000f:0.0} km";
            }

            return $"{Math.Max(0, (int)Math.Round(metres))} m";
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    /// <summary>
    /// G8a: when an NPC's name tag shows. Within <see cref="Range"/> metres, with a clear line from the
    /// camera, and not while the player talks to that NPC (the chat header already names them). Fully
    /// opaque up to <see cref="FadeStart"/>, then fading to nothing at the range.
    /// </summary>
    public static class NameTagRules
    {
        public const float Range = 15f;
        public const float FadeStart = 10f;

        public static float Alpha(float distance, bool lineOfSight, bool talkingToThisNpc)
        {
            if (talkingToThisNpc || !lineOfSight || distance > Range || float.IsNaN(distance))
            {
                return 0f;
            }

            if (distance <= FadeStart)
            {
                return 1f;
            }

            return (Range - distance) / (Range - FadeStart);
        }
    }
}
