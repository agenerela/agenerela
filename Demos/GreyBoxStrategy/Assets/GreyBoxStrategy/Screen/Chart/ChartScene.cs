using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Chart
{
    public enum GlyphKind { Conifer, RoundTree, Mountain, Hill, Marsh, Settlement, Fort, Anchor, Pass, Bridge, Special, Rock, Rose }

    /// <summary>
    /// One drawn mark on the chart, in grid units: its kind, where its foot stands, its size, and
    /// small integers that pick its variant. The CPU renderer draws it straight onto the chart; the
    /// GPU renderer draws each distinct one once into an atlas and places quads.
    /// </summary>
    public struct GlyphInstance
    {
        public GlyphKind Kind;
        public float U, V;
        /// <summary>In grid cells.</summary>
        public float Size;
        /// <summary>0 to 7: which of the kind's variants.</summary>
        public int Variant;
        /// <summary>Kind-specific: a settlement's <see cref="SettlementLook"/>, a bridge's angle in degrees, a special place's template index.</summary>
        public int Param;
        /// <summary>A ridge's peak: larger and darker than a mountain province's.</summary>
        public bool Ridge;

        /// <summary>The key two identical glyphs share, for an atlas: the size kept to a tenth of a cell.</summary>
        public long AtlasKey => ((long)Kind << 48) | ((long)Variant << 40) | ((long)(Ridge ? 1 : 0) << 39) | ((long)(Param & 0xFFFF) << 16) | (long)Math.Round(Size * 10);
    }

    /// <summary>A settlement's look, packed into <see cref="GlyphInstance.Param"/>: its tier, a capital's walls and banner, forts, a port's pier.</summary>
    public static class SettlementLook
    {
        public static int Pack(int tier, bool capital, int forts, bool port) => tier | (capital ? 8 : 0) | (Math.Min(3, forts) << 4) | (port ? 64 : 0);
        public static int Tier(int p) => p & 7;
        public static bool Capital(int p) => (p & 8) != 0;
        public static int Forts(int p) => (p >> 4) & 3;
        public static bool Port(int p) => (p & 64) != 0;

        /// <summary>The settlement a province's chart shows, 0 to 4: its development, a capital's city above all.</summary>
        public static int TierOf(ProvinceData p) => p.Capital ? 4 : Math.Min(3, ChartLayout.DevelopmentOf(p));

        /// <summary>What a tier is called in the province panel.</summary>
        public static string Name(int tier) => tier switch { 0 => "a hamlet", 1 => "a village", 2 => "a market town", 3 => "a walled town", _ => "a capital city" };
    }

    public enum StrokeKind { River, Highway, Track }

    /// <summary>A line on the chart, in grid units: a river or a road.</summary>
    public sealed class Stroke
    {
        public StrokeKind Kind;
        public List<(float u, float v)> Points = new List<(float, float)>();
        /// <summary>In grid cells.</summary>
        public float Width;
    }

    /// <summary>Everything drawn over the ground, in grid units, and the fields the ground is toned by.</summary>
    public sealed class ChartScene
    {
        public readonly List<GlyphInstance> Glyphs = new List<GlyphInstance>();
        public readonly List<Stroke> Strokes = new List<Stroke>();
        /// <summary>Per grid cell, 0 to 1: how much of the land is under the plough. Development puts it there.</summary>
        public float[] Farmland;
        /// <summary>Per grid cell: the terrain's index (<see cref="Terrain"/>), -1 water, -2 a scenery island.</summary>
        public int[] TerrainOf;
        /// <summary>Per province: where its settlement stands, in grid units.</summary>
        public (float u, float v)[] Settlements;
        /// <summary>Per province: its development reading, 0 to <see cref="Development.Max"/>.</summary>
        public int[] DevelopmentOf;
        /// <summary>The compass rose's centre and radius, in grid units; radius 0 for none.</summary>
        public float RoseU, RoseV, RoseRadius;
        /// <summary>Rhumb lines' centres, in grid units.</summary>
        public readonly List<(float u, float v)> Rhumbs = new List<(float, float)>();
    }

    /// <summary>
    /// Where everything goes on a generated world's chart, from its data alone: settlements by
    /// development, farmland around them, terrain glyphs massed by province, ridges with their
    /// passes, rivers, roads with their bridges, special places, scenery islands, the compass rose.
    /// Plain C#, no drawing: the CPU and GPU renderers draw the same scene.
    /// </summary>
    public static class ChartLayout
    {
        /// <summary>
        /// What a province's development reads as on the chart, 0 to <see cref="Development.Max"/>. Today
        /// the map's own value (population, capital, port); when slice R6 adds buildings, they feed
        /// <see cref="Development.Of"/> and nothing here changes.
        /// </summary>
        public static int DevelopmentOf(ProvinceData p) => Math.Max(0, Math.Min(Development.Max, p.Development));

        /// <summary>
        /// How far the plough reaches from a settlement of this development, in cells: a hamlet's patch of a cell or
        /// so, a developed town's belt of fields six or seven cells out, so the difference reads on the chart.
        /// </summary>
        public static float FarmRadius(int development, bool capital) => 1.4f + 1.9f * development + (capital ? 1.5f : 0);

        /// <summary>How much of the land near the settlement is fields, 0 to 1.</summary>
        public static float FarmShare(int development) => Math.Min(1f, 0.18f + 0.2f * development);

        public static float TerrainFarming(Terrain t)
        {
            switch (t)
            {
                case Terrain.Plains: return 1f;
                case Terrain.Hills: return 0.55f;
                case Terrain.Forest: return 0.25f;
                default: return 0f;
            }
        }

        public static ChartScene Build(WorldMap map, ChartRaster raster, int seed)
        {
            var b = new Builder(map, raster, seed);
            b.Run();
            return b.Scene;
        }

        private sealed class Builder
        {
            private readonly WorldMap map;
            private readonly ChartRaster raster;
            private readonly int seed;
            private readonly MapGeometry geo;
            private readonly int gw, gh;
            private readonly int[] cells;
            public readonly ChartScene Scene = new ChartScene();
            private readonly List<(float u, float v, float rx, float ry)> keepClear = new List<(float, float, float, float)>();
            // Coarse buckets of placed glyph footprints, so spacing checks stay local.
            private readonly Dictionary<long, List<(float u, float v, float r)>> placed = new Dictionary<long, List<(float, float, float)>>();

            public Builder(WorldMap map, ChartRaster raster, int seed)
            {
                this.map = map; this.raster = raster; this.seed = seed;
                geo = map.Geometry; gw = geo.Width; gh = geo.Height;
                cells = raster.Cells;
            }

            public void Run()
            {
                int n = map.Provinces.Count;
                Scene.TerrainOf = new int[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                    Scene.TerrainOf[i] = cells[i] >= 0 ? (int)map.Provinces[cells[i]].Terrain : cells[i] == MapGeometry.Island ? -2 : -1;
                Scene.DevelopmentOf = map.Provinces.Select(DevelopmentOf).ToArray();

                Settlements();
                Farmland();
                Specials();
                var passes = Ridges();
                Rivers();
                Roads();
                Terrain(passes);
                Islands();
                Rose();
                // Back to front: a lower glyph overlaps the one above it.
                Scene.Glyphs.Sort((a, c) => a.V.CompareTo(c.V));
            }

            // ---- settlements and development ----

            private void Settlements()
            {
                int n = map.Provinces.Count;
                Scene.Settlements = new (float, float)[n];
                for (int p = 0; p < n; p++)
                {
                    var pr = map.Provinces[p];
                    var shape = geo.Provinces[p];
                    float lu = shape.Label[0] + 0.5f, lv = shape.Label[1] + 0.5f;
                    string name = pr.Name ?? pr.Id;
                    keepClear.Add((lu, lv, Math.Max(1.4f, 0.36f * name.Length), 0.95f));
                    // The settlement stands above the name, or at the centre when there is no room there.
                    (float u, float v) at = (lu, lv - 1.8f);
                    if (!Inside(at.u, at.v, p, 0.8f)) at = (shape.Centre[0] + 0.5f, shape.Centre[1] + 0.5f);
                    Scene.Settlements[p] = at;
                    int tier = SettlementLook.TierOf(pr);
                    // Each tier visibly larger, so a town reads from a village at the middle zoom.
                    float size = 1.05f + 0.3f * tier;
                    Scene.Glyphs.Add(new GlyphInstance { Kind = GlyphKind.Settlement, U = at.u, V = at.v, Size = size, Variant = Hash8(p, 1),
                        Param = SettlementLook.Pack(tier, pr.Capital, pr.Forts, pr.Port) });
                    float r = 1.4f + 0.55f * tier;
                    keepClear.Add((at.u, at.v - 0.7f, r + 0.6f, r * 0.75f + 0.4f));
                    if (pr.Port && FindShore(p, out float hu, out float hv))
                    {
                        Scene.Glyphs.Add(new GlyphInstance { Kind = GlyphKind.Anchor, U = hu, V = hv, Size = 1, Variant = 0 });
                        keepClear.Add((hu, hv - 0.4f, 1.0f, 1.0f));
                    }
                }
            }

            private void Farmland()
            {
                var farm = new float[cells.Length];
                // Plains are farmed a little everywhere; more, and further, around a developed settlement.
                for (int i = 0; i < cells.Length; i++)
                    if (cells[i] >= 0 && map.Provinces[cells[i]].Terrain == GreyBoxStrategy.Terrain.Plains) farm[i] = 0.08f;
                for (int p = 0; p < map.Provinces.Count; p++)
                {
                    var (su, sv) = Scene.Settlements[p];
                    int dev = Scene.DevelopmentOf[p];
                    bool capital = map.Provinces[p].Capital;
                    float radius = FarmRadius(dev, capital), share = FarmShare(dev);
                    int x0 = Math.Max(0, (int)(su - radius - 1)), x1 = Math.Min(gw - 1, (int)(su + radius + 1));
                    int y0 = Math.Max(0, (int)(sv - radius - 1)), y1 = Math.Min(gh - 1, (int)(sv + radius + 1));
                    for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int i = y * gw + x;
                        int q = cells[i];
                        if (q < 0) continue;
                        // Its own land, and a little over the border into a neighbour's.
                        float own = q == p ? 1f : 0.35f;
                        float d = Dist(x + 0.5f, y + 0.5f, su, sv) / radius;
                        if (d >= 1) continue;
                        float f = share * own * TerrainFarming(map.Provinces[q].Terrain) * (float)Math.Pow(1 - d, 0.6);
                        if (f > farm[i]) farm[i] = f;
                    }
                }
                Scene.Farmland = farm;
            }

            // ---- special places ----

            private void Specials()
            {
                foreach (var sp in map.Specials)
                {
                    int p = map.Provinces.FindIndex(x => x.Id == sp.Province);
                    if (p < 0) continue;
                    var shape = geo.Provinces[p];
                    float lu = shape.Label[0] + 0.5f, lv = shape.Label[1] + 0.5f;
                    (float u, float v) at = (lu + 3.4f, lv + 0.4f);
                    foreach (var (du, dv) in new[] { (3.4f, 0.4f), (-3.4f, 0.4f), (0f, 2.6f), (3.6f, -2.0f), (-3.6f, -2.0f), (0f, -4.4f), (4.6f, 1.8f), (-4.6f, 1.8f) })
                        if (Inside(lu + du, lv + dv, p, 1.0f) && !InKeepClear(lu + du, lv + dv)) { at = (lu + du, lv + dv); break; }
                    int template = SpecialIndex(sp.Template);
                    Scene.Glyphs.Add(new GlyphInstance { Kind = GlyphKind.Special, U = at.u, V = at.v, Size = 1.9f, Param = template, Variant = Hash8(p, 7) });
                    keepClear.Add((at.u, at.v - 0.9f, 2.0f, 1.9f));
                }
            }

            public static readonly string[] SpecialTemplates =
                { "old_castle", "silver_mine", "iron_deep", "sacred_lake", "healing_spring", "horse_plains", "great_wood", "old_battlefield", "crown_seat" };

            private static int SpecialIndex(string t) { int i = Array.IndexOf(SpecialTemplates, t); return i < 0 ? SpecialTemplates.Length - 1 : i; }

            // ---- ridges, rivers, roads ----

            private List<(float u, float v)> Ridges()
            {
                var passes = new List<(float u, float v)>();
                foreach (var b in geo.Borders.Where(b => b.Kind == BorderLine.Pass))
                {
                    var line = b.Lines.OrderByDescending(l => l.Length).First();
                    int mid = (line.Length / 4) * 2;
                    passes.Add((line[mid], line[mid + 1]));
                    Scene.Glyphs.Add(new GlyphInstance { Kind = GlyphKind.Pass, U = line[mid], V = line[mid + 1] + 0.01f, Size = 1 });
                }
                foreach (var ridge in geo.Ridges)
                {
                    float ax = ridge.Line[0], ay = ridge.Line[1], bx = ridge.Line[2], by = ridge.Line[3];
                    float len = (float)Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                    float nx = -(by - ay) / Math.Max(1e-3f, len), ny = (bx - ax) / Math.Max(1e-3f, len);
                    int salt = (int)(ax * 31 + ay * 17);
                    // Two staggered rows of overlapping peaks along a bending line: a range, not a row of tents.
                    int count = (int)(len / 1.15f);
                    for (int k = 0; k <= count; k++)
                    {
                        float t = count == 0 ? 0 : k / (float)count;
                        float bend = (ChartNoise.Value(t * len * 0.09f, 3.7f, seed + 82 + salt) - 0.5f) * 5f;
                        float across = bend + (k % 2 == 0 ? -0.7f : 0.7f) + (ChartNoise.Hash01(k, 1, seed + 81 + salt) - 0.5f) * 1.2f;
                        float u = ax + (bx - ax) * t + nx * across + (ChartNoise.Hash01(k, 2, seed + 81 + salt) - 0.5f) * 0.6f;
                        float v = ay + (by - ay) * t + ny * across;
                        Reserve(u, v, 1.2f);
                        if (IsWater(u, v) || IsWater(u, v + 0.8f) || Id(u, v) < 0) continue;
                        if (passes.Any(pp => Dist(pp.u, pp.v, u, v) < 2.4f)) continue;
                        float size = (float)Math.Round((2.8f + 1.8f * ChartNoise.Hash01(k, 3, seed + 81 + salt)) * 4) / 4;
                        Scene.Glyphs.Add(new GlyphInstance { Kind = GlyphKind.Mountain, U = u, V = v, Size = size, Variant = Hash8(k, salt), Ridge = true });
                    }
                }
                return passes;
            }

            private void Rivers()
            {
                foreach (var r in geo.Rivers ?? new List<RiverLine>())
                {
                    var pts = new List<(float, float)>();
                    for (int k = 0; k + 1 < r.Line.Length; k += 2) pts.Add((r.Line[k], r.Line[k + 1]));
                    if (pts.Count < 2) continue;
                    // The grid's staircase simplified away, then rounded, then a meander that fades at the ends.
                    pts = Densify(Chaikin(Simplify(pts, 3f), 4), 0.8f);
                    for (int k = 1; k < pts.Count - 1; k++)
                    {
                        var (u, v) = pts[k];
                        float w = 0.45f * (float)Math.Sin(Math.PI * k / (pts.Count - 1));
                        pts[k] = (u + w * (ChartNoise.Value(u * 0.7f, v * 0.7f, seed + 301) - 0.5f) * 2, v + w * (ChartNoise.Value(u * 0.7f + 9, v * 0.7f, seed + 302) - 0.5f) * 2);
                    }
                    Scene.Strokes.Add(new Stroke { Kind = StrokeKind.River, Points = pts, Width = 0.1f + 0.09f * Math.Max(1, Math.Min(3, r.Rank)) });
                    foreach (var p in pts) Reserve(p.Item1, p.Item2, 0.6f);
                }
            }

            private void Roads()
            {
                var index = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int p = 0; p < map.Provinces.Count; p++) index[map.Provinces[p].Id] = p;
                // Where two provinces' border is crossed: its middle, the way a road would.
                (float u, float v)? Crossing(string a, string c)
                {
                    var bl = geo.Borders.FirstOrDefault(x => (x.A == a && x.B == c) || (x.A == c && x.B == a));
                    if (bl == null || bl.Lines.Count == 0) return null;
                    var line = bl.Lines.OrderByDescending(l => l.Length).First();
                    int mid = (line.Length / 4) * 2;
                    return (line[mid], line[mid + 1]);
                }
                foreach (var road in geo.Roads ?? new List<RoadLine>())
                {
                    var pts = new List<(float, float)>();
                    for (int k = 0; k < road.Provinces.Count; k++)
                    {
                        if (!index.TryGetValue(road.Provinces[k], out int p)) continue;
                        if (k > 0)
                        {
                            string a = road.Provinces[k - 1], c = road.Provinces[k];
                            var bridge = road.Bridges.FirstOrDefault(x => (x.A == a && x.B == c) || (x.A == c && x.B == a));
                            if (bridge != null)
                            {
                                pts.Add((bridge.At[0], bridge.At[1]));
                                Scene.Glyphs.Add(new GlyphInstance { Kind = GlyphKind.Bridge, U = bridge.At[0], V = bridge.At[1] + 0.02f, Size = 1, Param = RiverAngle(bridge) });
                            }
                            else { var cross = Crossing(a, c); if (cross.HasValue) pts.Add(cross.Value); }
                        }
                        pts.Add(Scene.Settlements[p]);
                    }
                    if (pts.Count < 2) continue;
                    pts = Wander(Chaikin(Densify(pts, 1.2f), 2), road.Id);
                    bool highway = road.Kind == RoadLine.Highway;
                    Scene.Strokes.Add(new Stroke { Kind = highway ? StrokeKind.Highway : StrokeKind.Track, Points = pts, Width = highway ? 0.2f : 0.13f });
                    foreach (var q in pts) Reserve(q.Item1, q.Item2, 0.55f);
                }
            }

            // A bridge spans the river: its angle, from the river's run at that corner.
            private int RiverAngle(Bridge b)
            {
                var r = geo.Rivers.FirstOrDefault(x => x.Id == b.River);
                if (r == null) return 0;
                for (int k = 0; k + 1 < r.Line.Length; k += 2)
                    if (r.Line[k] == b.At[0] && r.Line[k + 1] == b.At[1])
                    {
                        int j = k + 2 < r.Line.Length ? k + 2 : k - 2;
                        if (j < 0) break;
                        return (int)Math.Round(Math.Atan2(r.Line[j + 1] - r.Line[k + 1], r.Line[j] - r.Line[k]) * 180 / Math.PI);
                    }
                return 0;
            }

            private List<(float, float)> Wander(List<(float u, float v)> pts, string id)
            {
                int salt = id.GetHashCode() & 0xFFFF;
                for (int k = 1; k < pts.Count - 1; k++)
                {
                    var (u, v) = pts[k];
                    pts[k] = (u + 0.35f * (ChartNoise.Value(u * 0.5f, v * 0.5f, salt) - 0.5f), v + 0.35f * (ChartNoise.Value(u * 0.5f + 5, v * 0.5f, salt + 1) - 0.5f));
                }
                return pts;
            }

            // Douglas-Peucker: drops the points within tolerance of the line between their neighbours.
            private static List<(float, float)> Simplify(List<(float u, float v)> pts, float tolerance)
            {
                if (pts.Count < 3) return pts;
                var keep = new bool[pts.Count];
                keep[0] = keep[pts.Count - 1] = true;
                var stack = new Stack<(int, int)>();
                stack.Push((0, pts.Count - 1));
                while (stack.Count > 0)
                {
                    var (a, b) = stack.Pop();
                    float best = 0; int at = -1;
                    for (int k = a + 1; k < b; k++)
                    {
                        float d = SegDist(pts[k], pts[a], pts[b]);
                        if (d > best) { best = d; at = k; }
                    }
                    if (at >= 0 && best > tolerance) { keep[at] = true; stack.Push((a, at)); stack.Push((at, b)); }
                }
                var o = new List<(float, float)>();
                for (int k = 0; k < pts.Count; k++) if (keep[k]) o.Add(pts[k]);
                return o;
            }

            private static float SegDist((float u, float v) p, (float u, float v) a, (float u, float v) b)
            {
                float vx = b.u - a.u, vy = b.v - a.v, l2 = vx * vx + vy * vy;
                float t = l2 > 0 ? Math.Max(0, Math.Min(1, ((p.u - a.u) * vx + (p.v - a.v) * vy) / l2)) : 0;
                return Dist(p.u, p.v, a.u + vx * t, a.v + vy * t);
            }

            private static List<(float, float)> Densify(List<(float u, float v)> pts, float step)
            {
                var o = new List<(float, float)> { pts[0] };
                for (int k = 1; k < pts.Count; k++)
                {
                    var (au, av) = pts[k - 1]; var (bu, bv) = pts[k];
                    int n = Math.Max(1, (int)Math.Ceiling(Dist(au, av, bu, bv) / step));
                    for (int j = 1; j <= n; j++) o.Add((au + (bu - au) * j / n, av + (bv - av) * j / n));
                }
                return o;
            }

            private static List<(float, float)> Chaikin(List<(float u, float v)> pts, int rounds)
            {
                for (int r = 0; r < rounds; r++)
                {
                    var o = new List<(float, float)> { pts[0] };
                    for (int k = 0; k + 1 < pts.Count; k++)
                    {
                        var (au, av) = pts[k]; var (bu, bv) = pts[k + 1];
                        o.Add((0.75f * au + 0.25f * bu, 0.75f * av + 0.25f * bv));
                        o.Add((0.25f * au + 0.75f * bu, 0.25f * av + 0.75f * bv));
                    }
                    o.Add(pts[pts.Count - 1]);
                    pts = o;
                }
                return pts;
            }

            // ---- terrain ----

            private void Terrain(List<(float u, float v)> passes)
            {
                var provinces = map.Provinces;
                for (int i = 0; i < cells.Length; i++)
                {
                    int p = cells[i];
                    if (p < 0) continue;
                    var t = provinces[p].Terrain;
                    int gx = i % gw, gy = i / gw;
                    // Forest and mountains are massed: many glyphs, overlapping, so a wood reads as a wood
                    // and a range as a range; the ground under them carries the canopy and the rock.
                    float density, spacing, margin;
                    switch (t)
                    {
                        case GreyBoxStrategy.Terrain.Forest: density = 1f; spacing = 0.72f; margin = 0.5f; break;
                        case GreyBoxStrategy.Terrain.Mountains: density = 0.42f; spacing = 1.55f; margin = 1.1f; break;
                        case GreyBoxStrategy.Terrain.Hills: density = 0.32f; spacing = 2.0f; margin = 1.2f; break;
                        case GreyBoxStrategy.Terrain.Marsh: density = 0.35f; spacing = 1.5f; margin = 0.8f; break;
                        default: density = 0.07f; spacing = 2.6f; margin = 1.0f; break;
                    }
                    // Fields take the place of wood near a settlement.
                    float farm = Scene.Farmland[i];
                    if (t == GreyBoxStrategy.Terrain.Forest && farm > 0.15f) density *= 0.35f;
                    if (ChartNoise.Hash01(gx, gy, seed + 90) > density) continue;
                    float u = gx + 0.1f + 0.8f * ChartNoise.Hash01(gx, gy, seed + 91);
                    float v = gy + 0.1f + 0.8f * ChartNoise.Hash01(gx, gy, seed + 92);
                    if (Crowded(u, v, spacing)) continue;
                    if (InKeepClear(u, v)) continue;
                    if (!Inside(u, v, p, margin)) continue;
                    float roll = ChartNoise.Hash01(gx, gy, seed + 93);
                    int variant = (int)(roll * 8) & 7;
                    switch (t)
                    {
                        case GreyBoxStrategy.Terrain.Forest:
                            // Edge trees round and tall, the mass mostly conifers.
                            Add(GlyphKind.Conifer, u, v, 1.15f + 0.6f * roll, variant, spacing, roll < 0.72f ? GlyphKind.Conifer : GlyphKind.RoundTree);
                            break;
                        case GreyBoxStrategy.Terrain.Mountains:
                            Add(GlyphKind.Mountain, u, v, 2.4f + 1.6f * roll, variant, spacing);
                            break;
                        case GreyBoxStrategy.Terrain.Hills:
                            Add(GlyphKind.Hill, u, v, 2.0f + 0.9f * roll, variant, spacing);
                            break;
                        case GreyBoxStrategy.Terrain.Marsh:
                            Add(GlyphKind.Marsh, u, v, 1.4f + 0.5f * roll, variant, spacing);
                            break;
                        default:
                            // Plains: copses and lone trees, more where the fields end.
                            if (farm < 0.35f) Add(GlyphKind.RoundTree, u, v, 1.0f + 0.5f * roll, variant, spacing);
                            break;
                    }
                }
                // Hedgerow trees along the fields' edges near settlements.
                for (int i = 0; i < cells.Length; i++)
                {
                    float farm = Scene.Farmland[i];
                    if (farm < 0.25f || cells[i] < 0) continue;
                    int gx = i % gw, gy = i / gw;
                    if (ChartNoise.Hash01(gx, gy, seed + 95) > 0.08f) continue;
                    float u = gx + ChartNoise.Hash01(gx, gy, seed + 96), v = gy + ChartNoise.Hash01(gx, gy, seed + 97);
                    if (Crowded(u, v, 1.4f) || InKeepClear(u, v) || !Inside(u, v, cells[i], 0.6f)) continue;
                    Add(GlyphKind.RoundTree, u, v, 0.8f + 0.3f * ChartNoise.Hash01(gx, gy, seed + 98), gx & 7, 1.4f);
                }
            }

            private void Add(GlyphKind kind, float u, float v, float size, int variant, float spacing, GlyphKind? as_ = null)
            {
                // Sizes in quarter cells, so an atlas draws each look once.
                Scene.Glyphs.Add(new GlyphInstance { Kind = as_ ?? kind, U = u, V = v, Size = (float)Math.Round(size * 4) / 4, Variant = variant });
                Reserve(u, v, spacing);
            }

            private void Islands()
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i] != MapGeometry.Island) continue;
                    int gx = i % gw, gy = i / gw;
                    float roll = ChartNoise.Hash01(gx, gy, seed + 110);
                    if (roll > 0.3f) continue;
                    float u = gx + 0.5f, v = gy + 0.6f;
                    if (!IslandAt(u, v) || !IslandAt(u, v + 0.5f) || Crowded(u, v, 1.2f)) continue;
                    Add(roll < 0.12f ? GlyphKind.Rock : GlyphKind.Conifer, u, v, roll < 0.12f ? 1.1f : 1.0f, (int)(roll * 80) & 7, 1.2f);
                }
            }

            private void Rose()
            {
                int w = raster.Width, h = raster.Height;
                float s = raster.Scale;
                var dist = ChartRaster.DistanceToOther(raster.Ids, w, h, (a, c) => ChartRaster.IsWater(a) != ChartRaster.IsWater(c));
                (float, float, float) Best(int x0, int y0, int x1, int y1)
                {
                    float best = -1; (float, float, float) at = ((x0 + x1) / 2f, (y0 + y1) / 2f, 0);
                    int step = Math.Max(1, (int)(s / 2));
                    for (int y = y0; y < y1; y += step)
                    for (int x = x0; x < x1; x += step)
                    {
                        int i = y * w + x;
                        if (raster.Ids[i] != ChartRaster.Sea) continue;
                        float edge = Math.Min(Math.Min(x, w - x), Math.Min(y, h - y));
                        float score = Math.Min(dist[i], edge * 0.8f);
                        if (score > best) { best = score; at = (x, y, dist[i]); }
                    }
                    return at;
                }
                var (rx, ry, rd) = Best(w / 2, h / 2, w, h);
                Scene.RoseU = rx / s; Scene.RoseV = ry / s;
                Scene.RoseRadius = Math.Min(5.5f, rd / s * 0.6f);
                if (Scene.RoseRadius < 2) Scene.RoseRadius = 0;
                else Scene.Glyphs.Add(new GlyphInstance { Kind = GlyphKind.Rose, U = Scene.RoseU, V = Scene.RoseV, Size = (float)Math.Round(Scene.RoseRadius * 4) / 4 });
                Scene.Rhumbs.Add((Scene.RoseU, Scene.RoseV));
                var (tx, ty, _) = Best(0, 0, w / 2, h / 2);
                Scene.Rhumbs.Add((tx / s, ty / s));
            }

            // ---- helpers ----

            private static long Bucket(int bx, int by) => ((long)bx << 32) | (uint)by;

            private void Reserve(float u, float v, float r)
            {
                long k = Bucket((int)(u / 3), (int)(v / 3));
                if (!placed.TryGetValue(k, out var list)) placed[k] = list = new List<(float, float, float)>();
                list.Add((u, v, r));
            }

            private bool Crowded(float u, float v, float spacing)
            {
                int bx = (int)(u / 3), by = (int)(v / 3);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (placed.TryGetValue(Bucket(bx + dx, by + dy), out var list))
                        foreach (var (pu, pv, pr) in list)
                            if (Dist(pu, pv, u, v) < Math.Max(spacing, pr) * 0.98f) return true;
                return false;
            }

            private bool InKeepClear(float u, float v)
            {
                foreach (var (cu, cv, rx, ry) in keepClear)
                {
                    float dx = (u - cu) / rx, dy = (v - cv) / ry;
                    if (dx * dx + dy * dy < 1) return true;
                }
                return false;
            }

            private int Id(float u, float v)
            {
                int x = (int)(u * raster.Scale), y = (int)(v * raster.Scale);
                if (x < 0 || y < 0 || x >= raster.Width || y >= raster.Height) return -1;
                ushort id = raster.Ids[y * raster.Width + x];
                return ChartRaster.IsProvince(id) ? id : -1;
            }

            private bool IsWater(float u, float v)
            {
                int x = (int)(u * raster.Scale), y = (int)(v * raster.Scale);
                if (x < 0 || y < 0 || x >= raster.Width || y >= raster.Height) return true;
                return ChartRaster.IsWater(raster.Ids[y * raster.Width + x]);
            }

            private bool IslandAt(float u, float v)
            {
                int x = (int)(u * raster.Scale), y = (int)(v * raster.Scale);
                if (x < 0 || y < 0 || x >= raster.Width || y >= raster.Height) return false;
                return raster.Ids[y * raster.Width + x] == ChartRaster.Island;
            }

            /// <summary>Whether a point and a ring around it all lie in province <paramref name="p"/>, as drawn.</summary>
            public bool Inside(float u, float v, int p, float margin)
            {
                if (Id(u, v) != p) return false;
                for (int k = 0; k < 8; k++)
                {
                    double a = k * Math.PI / 4;
                    if (Id(u + margin * (float)Math.Cos(a), v + margin * (float)Math.Sin(a)) != p) return false;
                }
                return true;
            }

            private bool FindShore(int p, out float u, out float v)
            {
                var c = geo.Provinces[p].Centre;
                u = v = 0;
                float best = float.MaxValue;
                for (int y = 1; y < gh - 1; y++)
                for (int x = 1; x < gw - 1; x++)
                {
                    if (cells[y * gw + x] != p) continue;
                    bool shore = cells[y * gw + x - 1] == MapGeometry.Sea || cells[y * gw + x + 1] == MapGeometry.Sea || cells[(y - 1) * gw + x] == MapGeometry.Sea || cells[(y + 1) * gw + x] == MapGeometry.Sea;
                    if (!shore) continue;
                    float d = Dist(x, y, c[0], c[1]);
                    if (d < best) { best = d; u = x + 0.5f; v = y + 0.5f; }
                }
                if (best == float.MaxValue) return false;
                float du = c[0] + 0.5f - u, dv = c[1] + 0.5f - v, len = (float)Math.Sqrt(du * du + dv * dv);
                if (len > 0) { u += du / len * 0.9f; v += dv / len * 0.9f; }
                return true;
            }

            private int Hash8(int a, int b) => (int)(ChartNoise.Hash(a, b, seed + 120) & 7);
        }

        public static float Dist(float au, float av, float bu, float bv) => (float)Math.Sqrt((au - bu) * (au - bu) + (av - bv) * (av - bv));
    }
}
