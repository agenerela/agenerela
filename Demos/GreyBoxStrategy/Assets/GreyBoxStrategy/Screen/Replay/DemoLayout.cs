using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>A rectangle in panel units (y down), for the demo's layout without UnityEngine.</summary>
    public struct DemoRect
    {
        public float X, Y, W, H;
        public DemoRect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        public float Right => X + W;
        public float Bottom => Y + H;
        public float CX => X + W / 2;
        public float CY => Y + H / 2;
        public bool Overlaps(DemoRect o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;
        public bool Inside(DemoRect o) => X >= o.X && Y >= o.Y && Right <= o.Right && Bottom <= o.Bottom;
        public DemoRect Grow(float by) => new DemoRect(X - by, Y - by, W + 2 * by, H + 2 * by);
        public float OverlapArea(DemoRect o) => Math.Max(0, Math.Min(Right, o.Right) - Math.Max(X, o.X)) * Math.Max(0, Math.Min(Bottom, o.Bottom) - Math.Max(Y, o.Y));
        public override string ToString() => $"({X:0},{Y:0} {W:0}×{H:0})";
    }

    /// <summary>Where a callout goes: beside what it points at, never over it, inside the screen.</summary>
    public static class DemoLayout
    {
        /// <summary>The gap between a callout and what it points at, which the leader line crosses.</summary>
        public const float Gap = 56, Margin = 18;

        public struct Placement
        {
            public DemoRect Box;
            public string Side;
            /// <summary>The leader line: from the callout's edge to the target's.</summary>
            public float FromX, FromY, ToX, ToY;
        }

        /// <summary>
        /// Tries the sides in order (the asked one first, then left, right, above, below) and takes the first that
        /// fits the screen without touching the target or anything in <paramref name="avoid"/>; failing that, the one
        /// covering the least. The box slides along its side to stay on screen, kept level with the target where it can.
        /// </summary>
        public static Placement Place(DemoRect target, float boxW, float boxH, DemoRect screen, string side = null, IList<DemoRect> avoid = null)
        {
            var order = new List<string> { "left", "right", "above", "below" };
            if (side != null && order.Remove(side)) order.Insert(0, side);
            var inner = new DemoRect(screen.X + Margin, screen.Y + Margin, screen.W - 2 * Margin, screen.H - 2 * Margin);
            Placement best = default; float bestCost = float.MaxValue;
            foreach (var s in order)
            {
                var box = At(target, boxW, boxH, inner, s);
                float cost = 0;
                if (!box.Inside(inner)) cost += 1e6f + OutsideArea(box, inner);
                cost += box.OverlapArea(target.Grow(8)) * 100;
                if (avoid != null) foreach (var a in avoid) cost += box.OverlapArea(a);
                if (cost == 0) return Finish(box, target, s);
                if (cost < bestCost) { bestCost = cost; best = Finish(box, target, s); }
            }
            return best;
        }

        private static float OutsideArea(DemoRect r, DemoRect inner) => r.W * r.H - r.OverlapArea(inner);

        private static DemoRect At(DemoRect t, float w, float h, DemoRect inner, string side)
        {
            float x, y;
            switch (side)
            {
                case "left": x = t.X - Gap - w; y = t.CY - h / 2; break;
                case "right": x = t.Right + Gap; y = t.CY - h / 2; break;
                case "above": x = t.CX - w / 2; y = t.Y - Gap - h; break;
                default: x = t.CX - w / 2; y = t.Bottom + Gap; break;
            }
            // Slide along the side to stay on screen; the side's own direction is left as it is, so a box that does not
            // fit there shows as not fitting rather than being pushed onto the target.
            if (side == "left" || side == "right") y = Clamp(y, inner.Y, inner.Bottom - h);
            else x = Clamp(x, inner.X, inner.Right - w);
            return new DemoRect(x, y, w, h);
        }

        private static float Clamp(float v, float lo, float hi) => hi < lo ? lo : Math.Max(lo, Math.Min(hi, v));

        private static Placement Finish(DemoRect box, DemoRect t, string side)
        {
            var p = new Placement { Box = box, Side = side };
            switch (side)
            {
                case "left": p.FromX = box.Right; p.FromY = Clamp(t.CY, box.Y + 14, box.Bottom - 14); p.ToX = t.X; p.ToY = Clamp(p.FromY, t.Y + 2, t.Bottom - 2); break;
                case "right": p.FromX = box.X; p.FromY = Clamp(t.CY, box.Y + 14, box.Bottom - 14); p.ToX = t.Right; p.ToY = Clamp(p.FromY, t.Y + 2, t.Bottom - 2); break;
                case "above": p.FromY = box.Bottom; p.FromX = Clamp(t.CX, box.X + 14, box.Right - 14); p.ToY = t.Y; p.ToX = Clamp(p.FromX, t.X + 2, t.Right - 2); break;
                default: p.FromY = box.Y; p.FromX = Clamp(t.CX, box.X + 14, box.Right - 14); p.ToY = t.Bottom; p.ToX = Clamp(p.FromX, t.X + 2, t.Right - 2); break;
            }
            return p;
        }
    }
}
