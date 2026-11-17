using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Realm
{
    /// <summary>What a box on the map is, in the order collisions are settled: earlier kinds win.</summary>
    public enum MapItemKind { Piece, Tag, Realm, Capital, Region, Name }

    /// <summary>A box on the map in panel pixels (y down), and its rank among its kind: larger first.</summary>
    public struct MapItem
    {
        public MapItemKind Kind;
        public float X0, Y0, X1, Y1;
        public float Rank;

        public MapItem(MapItemKind kind, float x0, float y0, float x1, float y1, float rank = 0)
        {
            Kind = kind; X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; Rank = rank;
        }

        public float Height => Y1 - Y0;
        public MapItem Down(float dy) => new MapItem(Kind, X0, Y0 + dy, X1, Y1 + dy, Rank);
    }

    /// <summary>
    /// Nothing on the map covers anything else (rules v2 section 2.6, "collisions are resolved greedily"):
    /// one pass over everything placed for the frame, in a fixed order of importance.
    /// <list type="bullet">
    /// <item>Pieces stay where they stand: they were set apart from each other already, and they are what a
    /// player clicks.</item>
    /// <item>A march's turns tag moves down, clear of what it would cover; it never hides.</item>
    /// <item>A realm's name lies under the pieces and tags, as a printed map's area names do, so only other names
    /// can hide it; every name after it keeps clear of it.</item>
    /// <item>Region names hide where they would cover something.</item>
    /// <item>Capital and province names step down a little, at most about their own height, to get clear,
    /// and hide where that is not enough.</item>
    /// </list>
    /// </summary>
    public static class MapDeclutter
    {
        /// <summary>Boxes may touch by this much (panel pixels) and still count as clear: a shadow's width.</summary>
        public const float Slack = 1;

        /// <summary>For each item, whether it shows and how far down it moved.</summary>
        public static (bool shown, float dy)[] Resolve(IReadOnlyList<MapItem> items)
        {
            var result = new (bool, float)[items.Count];
            var kept = new List<MapItem>(items.Count);
            var order = Enumerable.Range(0, items.Count).OrderBy(i => items[i].Kind).ThenByDescending(i => items[i].Rank).ThenBy(i => i);
            foreach (int i in order)
            {
                var it = items[i];
                switch (it.Kind)
                {
                    case MapItemKind.Piece:
                        kept.Add(it); result[i] = (true, 0);
                        break;
                    case MapItemKind.Tag:
                    {
                        float dy = 0;
                        for (int tries = 0; tries < 8; tries++)
                        {
                            int k = FirstHit(kept, it.Down(dy));
                            if (k < 0) break;
                            dy = kept[k].Y1 - it.Y0 + Slack;
                        }
                        kept.Add(it.Down(dy)); result[i] = (true, dy);
                        break;
                    }
                    case MapItemKind.Capital:
                    case MapItemKind.Name:
                    {
                        float dy = 0, most = it.Height * 1.1f;
                        bool clear = false;
                        for (int tries = 0; tries < 3; tries++)
                        {
                            int k = FirstHit(kept, it.Down(dy));
                            if (k < 0) { clear = true; break; }
                            dy = kept[k].Y1 - it.Y0 + Slack;
                            if (dy > most) break;
                        }
                        if (clear) { kept.Add(it.Down(dy)); result[i] = (true, dy); }
                        else result[i] = (false, 0);
                        break;
                    }
                    case MapItemKind.Realm:
                        if (FirstHit(kept, it, under: true) < 0) { kept.Add(it); result[i] = (true, 0); }
                        else result[i] = (false, 0);
                        break;
                    default:
                        if (FirstHit(kept, it) < 0) { kept.Add(it); result[i] = (true, 0); }
                        else result[i] = (false, 0);
                        break;
                }
            }
            return result;
        }

        public static bool Overlaps(MapItem a, MapItem b) =>
            Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0) > Slack && Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0) > Slack;

        // under: a realm's name, which pieces and tags may cover.
        private static int FirstHit(List<MapItem> kept, MapItem it, bool under = false)
        {
            for (int k = 0; k < kept.Count; k++)
                if (!(under && Below(kept[k].Kind)) && Overlaps(kept[k], it)) return k;
            return -1;
        }

        private static bool Below(MapItemKind k) => k == MapItemKind.Piece || k == MapItemKind.Tag;

        /// <summary>A pair that may overlap: a piece or tag over a realm's name.</summary>
        public static bool Allowed(MapItemKind a, MapItemKind b) => (a == MapItemKind.Realm && Below(b)) || (b == MapItemKind.Realm && Below(a));

        /// <summary>The pairs of shown items that overlap, by kind, but for pieces and tags over realm names: the readout's measure, and the tests'.</summary>
        public static Dictionary<string, int> Collisions(IReadOnlyList<MapItem> shown)
        {
            var count = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < shown.Count; i++)
            for (int j = i + 1; j < shown.Count; j++)
            {
                if (!Overlaps(shown[i], shown[j]) || Allowed(shown[i].Kind, shown[j].Kind)) continue;
                var a = shown[i].Kind; var b = shown[j].Kind;
                string key = a <= b ? $"{a}-{b}" : $"{b}-{a}";
                count[key] = count.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            return count;
        }
    }
}
