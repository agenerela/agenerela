using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>What the generator made: the world, whether it passed every check, and how it got there.</summary>
    public sealed class GeneratedWorld
    {
        /// <summary>The world that passed, or the best one found when none did (null if no try made a world at all).</summary>
        public WorldMap Map;
        public bool Passed;
        /// <summary>Tries made, the passing one included.</summary>
        public int Attempts;
        /// <summary>What the returned world fails; empty when it passed.</summary>
        public List<MapProblem> Problems = new List<MapProblem>();
        /// <summary>One line per failed try: its first problems.</summary>
        public List<string> Log = new List<string>();
        /// <summary>Every country's start, measured.</summary>
        public List<CountryFairness> Fairness = new List<CountryFairness>();
        /// <summary>The slots the special places were made for.</summary>
        public List<SpecialSlot> Slots = new List<SpecialSlot>();

        /// <summary>Whether the world can be played: it exists and nothing in its rules graph is wrong.</summary>
        public bool Playable => Map != null && Problems.All(p => p.Kind != ProblemKind.Connectivity);
    }

    /// <summary>
    /// The map generator of rules v2 (section 2.3): from a seed, a number of countries and
    /// <see cref="MapSettings"/> to a world with provinces, borders, terrain, coasts and ports,
    /// regions, fair capitals and starting realms, goods, population, special places and names,
    /// and the geometry to draw it. Integer arithmetic throughout, so a seed's world is the same on
    /// every machine. Each try is checked (<see cref="MapChecks"/>); a world that fails is made again
    /// from the next sub-seed, up to <see cref="MaxAttempts"/> times.
    /// </summary>
    /// <remarks>
    /// The model's parts plug in through <see cref="ISpecialPlaceMaker"/> (special places: R2's
    /// world-maker agent, R9's Cartographer) and <see cref="INameMaker"/> (names: R10). Both are
    /// checked, and fall back to the tables on anything they get wrong.
    /// </remarks>
    public static class MapGenerator
    {
        public const int MaxAttempts = 50;
        /// <summary>Land cells per province on the generator's grid: the drawing's resolution.</summary>
        public const int CellsPerProvince = 400;

        public static GeneratedWorld Generate(int seed, int countries, MapSettings settings = null,
            ISpecialPlaceMaker specials = null, INameMaker names = null, int maxAttempts = MaxAttempts)
        {
            settings = (settings ?? new MapSettings()).Clone();
            if (countries < GameSetup.MinCountries || countries > GameSetup.MaxCountries)
                throw new ArgumentException($"{countries} countries is outside {GameSetup.MinCountries} to {GameSetup.MaxCountries}.");
            string why = settings.WhyInvalid(countries);
            if (why != null) throw new ArgumentException($"These map settings cannot make a world: {why}.");

            var result = new GeneratedWorld();
            (WorldMap map, List<MapProblem> problems, List<SpecialSlot> slots, List<CountryFairness> fairness) best = default;
            int bestScore = int.MaxValue;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                result.Attempts = attempt + 1;
                int sub = (int)MapRandom.Hash(seed, "attempt", attempt);
                var draft = new Draft(sub, countries, settings, specials, names);
                var map = draft.Build();
                var problems = new List<MapProblem>(draft.Problems);
                if (map != null)
                {
                    map.Generated.Seed = seed;
                    map.Generated.Attempt = attempt;
                    problems.AddRange(MapChecks.Check(map));
                }
                if (map != null && problems.Count == 0)
                {
                    result.Map = map;
                    result.Passed = true;
                    result.Slots = draft.Slots;
                    result.Fairness = MapChecks.Fairness(map);
                    return result;
                }
                result.Log.Add($"try {attempt}: " + string.Join("; ", problems.Take(3)) + (problems.Count > 3 ? $"; and {problems.Count - 3} more" : ""));
                int score = map == null ? int.MaxValue - 1
                    : problems.Count(p => p.Kind == ProblemKind.Connectivity) * 10000
                      + problems.Count(p => p.Kind == ProblemKind.Shape) * 100
                      + problems.Count;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = (map, problems, draft.Slots, map == null ? null : MapChecks.Fairness(map));
                }
            }
            result.Map = best.map;
            result.Problems = best.problems ?? new List<MapProblem>();
            result.Slots = best.slots ?? new List<SpecialSlot>();
            result.Fairness = best.fairness ?? new List<CountryFairness>();
            return result;
        }

        /// <summary>One try: every step of section 2.3 on one sub-seed.</summary>
        private sealed class Draft
        {
            private readonly int seed;
            private readonly int countries;
            private readonly MapSettings settings;
            private readonly ISpecialPlaceMaker specialMaker;
            private readonly INameMaker nameMaker;

            public readonly List<MapProblem> Problems = new List<MapProblem>();
            public List<SpecialSlot> Slots = new List<SpecialSlot>();

            private int n, w, h;
            private int[] cells;      // province index, Sea, Water; Land (-3) while unassigned
            private int[] height;     // the land's height field, 0..65535
            private double meanArea;
            private int[] cx, cy;     // province centres
            private List<int>[] adj;  // the rules graph
            private Dictionary<long, int> shared;
            private Dictionary<long, List<(int x0, int y0, int x1, int y1)>> edges;
            private readonly List<RidgeLine> ridges = new List<RidgeLine>();
            private readonly List<(int a, int b)> cutPairs = new List<(int, int)>();
            private readonly HashSet<long> passPairs = new HashSet<long>();
            private Terrain[] terrain;
            private int[] coastLen, shoreLen, population, region, owner;
            private bool[] port, onChartEdge, isCapital;
            private string[] good;
            private int[] capitals; // per country, in seat order
            private int regionCount;

            private const int Land = -3;

            public Draft(int seed, int countries, MapSettings settings, ISpecialPlaceMaker specials, INameMaker names)
            {
                this.seed = seed;
                this.countries = countries;
                this.settings = settings;
                specialMaker = specials;
                nameMaker = names;
            }

            public WorldMap Build()
            {
                n = MapSettings.Provinces(settings.Size);
                if (!MakeLand()) return null;
                if (!MakeProvinces()) return null;
                MakeAdjacency();
                MakeRidges();
                MakeTerrain();
                MakeCoasts();
                MakeRegions();
                MakeGoods();
                if (!PlaceCapitals()) return null;
                FixScarceGoods();
                var map = Assemble();
                NameEverything(map);
                PlaceSpecials(map);
                return map;
            }

            // ------------------------------------------------------------------------------
            // 1. Land
            // ------------------------------------------------------------------------------

            private int LandPercent()
            {
                switch (settings.Shape)
                {
                    case LandShape.TwoLands: return 40;
                    case LandShape.InlandSea: return 46;
                    case LandShape.Peninsula: return 46;
                    case LandShape.LongCoast: return 44;
                    default: return 42;
                }
            }

            private bool MakeLand()
            {
                int percent = LandPercent();
                long grid = (long)n * CellsPerProvince * 100 / percent;
                w = (int)MapRandom.Isqrt(grid * 16 / 9);
                h = (int)(grid / w);
                cells = new int[w * h];
                height = new int[w * h];
                int period = Math.Max(4, w / 4);
                bool flip = (MapRandom.Hash(seed, "flip", 0) & 1) == 1;
                var hist = new int[65536];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int noise = MapRandom.Fractal(seed, "land", x, y, period, 5);
                    long u = (long)(flip ? w - 1 - x : x) * 65536 / w, v = (long)y * 65536 / h;
                    int shape = Shape(u, v);
                    int value = (int)(((long)noise * 50 + (long)shape * 50) / 100);
                    if (ForcedWater(x, y, flip)) value = 0;
                    height[y * w + x] = value;
                    hist[value]++;
                }
                long target = grid * percent / 100;
                int threshold = 65535;
                long count = 0;
                while (threshold > 0 && count + hist[threshold] < target) count += hist[threshold--];
                for (int c = 0; c < cells.Length; c++) cells[c] = height[c] >= threshold && height[c] > 0 ? Land : MapGeometry.Sea;

                // Only the largest land stays playable; small enclosed water is filled, large stays as inland water.
                var landParts = Components(c => cells[c] == Land);
                var biggest = landParts.OrderByDescending(p => p.Count).ThenBy(p => p[0]).FirstOrDefault();
                if (biggest == null) { Problems.Add(new MapProblem(ProblemKind.Shape, "land", "no land at all")); return false; }
                var keep = new bool[cells.Length];
                foreach (int c in biggest) keep[c] = true;
                for (int c = 0; c < cells.Length; c++) if (cells[c] == Land && !keep[c]) cells[c] = MapGeometry.Sea;
                int landCount = biggest.Count;
                meanArea = (double)landCount / n;
                foreach (var water in Components(c => cells[c] == MapGeometry.Sea))
                {
                    bool open = water.Any(c => { int x = c % w, y = c / w; return x == 0 || y == 0 || x == w - 1 || y == h - 1; });
                    if (open) continue;
                    if (water.Count < meanArea * 0.6) { foreach (int c in water) cells[c] = Land; landCount += water.Count; }
                    else foreach (int c in water) cells[c] = MapGeometry.Water;
                }
                meanArea = (double)landCount / n;
                if (landCount < n * CellsPerProvince / 3)
                {
                    Problems.Add(new MapProblem(ProblemKind.Shape, "land", $"only {landCount} land cells for {n} provinces"));
                    return false;
                }
                return true;
            }

            // The shape's falloff, 0..65535: high where the shape wants land. u and v run 0..65535 across the chart.
            private int Shape(long u, long v)
            {
                switch (settings.Shape)
                {
                    case LandShape.TwoLands:
                        return Math.Max(Ellipse(u, v, 0.28, 0.5, 0.25, 0.42),
                               Math.Max(Ellipse(u, v, 0.72, 0.5, 0.25, 0.42), Ellipse(u, v, 0.5, 0.5, 0.22, 0.09)));
                    case LandShape.InlandSea:
                    {
                        long e = EllipseDistance(u, v, 0.5, 0.5, 0.47, 0.47);
                        long s = 1024 - Math.Abs(e - 700) * 1024 / 300;
                        return (int)Math.Min(65535, Math.Max(0, s) * 64);
                    }
                    case LandShape.Peninsula:
                        return Ellipse(u, v, 0.0, 0.5, 0.92, 0.44);
                    case LandShape.LongCoast:
                    {
                        long sv = Clamp((v - 26214) * 65536 / 14418);       // 0 at 40% down, full at 62%
                        long edge = Math.Min(u, 65535 - u);
                        long sx = Clamp(edge * 65536 / 5243);               // tapers within 8% of each side
                        return (int)(sv * sx / 65536);
                    }
                    default:
                        return Ellipse(u, v, 0.5, 0.5, 0.46, 0.46);
                }
            }

            private static long Clamp(long v) => Math.Max(0, Math.Min(65535, v));

            // 1024 at the ellipse's rim, 0 at its centre, in fixed point.
            private static long EllipseDistance(long u, long v, double cu, double cv, double ru, double rv)
            {
                long du = (u - (long)(cu * 65536)) * 1024 / (long)(ru * 65536);
                long dv = (v - (long)(cv * 65536)) * 1024 / (long)(rv * 65536);
                return MapRandom.Isqrt(du * du + dv * dv);
            }

            private static int Ellipse(long u, long v, double cu, double cv, double ru, double rv) =>
                (int)Math.Min(65535, Math.Max(0, 1024 - EllipseDistance(u, v, cu, cv, ru, rv)) * 64);

            // The chart's edges are sea, except the side a peninsula or a long coast runs off.
            private bool ForcedWater(int x, int y, bool flip)
            {
                bool left = x == 0, right = x == w - 1, top = y == 0, bottom = y == h - 1;
                switch (settings.Shape)
                {
                    case LandShape.Peninsula: return top || bottom || (flip ? left : right);
                    case LandShape.LongCoast: return top || left || right;
                    default: return left || right || top || bottom;
                }
            }

            private List<List<int>> Components(Func<int, bool> member)
            {
                var seen = new bool[cells.Length];
                var parts = new List<List<int>>();
                var stack = new Stack<int>();
                for (int c = 0; c < cells.Length; c++)
                {
                    if (seen[c] || !member(c)) continue;
                    var part = new List<int>();
                    seen[c] = true;
                    stack.Push(c);
                    while (stack.Count > 0)
                    {
                        int k = stack.Pop();
                        part.Add(k);
                        int x = k % w, y = k / w;
                        if (x > 0 && !seen[k - 1] && member(k - 1)) { seen[k - 1] = true; stack.Push(k - 1); }
                        if (x + 1 < w && !seen[k + 1] && member(k + 1)) { seen[k + 1] = true; stack.Push(k + 1); }
                        if (y > 0 && !seen[k - w] && member(k - w)) { seen[k - w] = true; stack.Push(k - w); }
                        if (y + 1 < h && !seen[k + w] && member(k + w)) { seen[k + w] = true; stack.Push(k + w); }
                    }
                    part.Sort();
                    parts.Add(part);
                }
                return parts;
            }

            // ------------------------------------------------------------------------------
            // 2-3. Seeds, provinces, and their repair
            // ------------------------------------------------------------------------------

            private bool MakeProvinces()
            {
                var land = new List<int>();
                for (int c = 0; c < cells.Length; c++) if (cells[c] == Land) land.Add(c);
                var keys = land.Select(c => MapRandom.Hash(seed, "seeds", c)).ToArray();
                var order = land.ToArray();
                Array.Sort(keys, order);

                // Poisson-disc darts: the widest spacing that still places n seeds.
                long lo = (long)(meanArea * 0.15), hi = (long)(meanArea * 2.5);
                while (lo < hi)
                {
                    long mid = (lo + hi + 1) / 2;
                    if (Darts(order, mid, n).Count >= n) lo = mid;
                    else hi = mid - 1;
                }
                var seeds = Darts(order, lo, n);
                if (seeds.Count < n)
                {
                    Problems.Add(new MapProblem(ProblemKind.Shape, "seeds", $"only {seeds.Count} province seeds fit"));
                    return false;
                }
                var sx = seeds.Take(n).Select(c => c % w).ToArray();
                var sy = seeds.Take(n).Select(c => c / w).ToArray();

                // Lloyd relaxation: three times toward the centre of each seed's cells, so provinces are even.
                for (int round = 0; round < 3; round++)
                {
                    var sumX = new long[n];
                    var sumY = new long[n];
                    var count = new int[n];
                    BuildBuckets(sx, sy);
                    foreach (int c in land)
                    {
                        int s = Nearest(c % w, c / w, sx, sy);
                        sumX[s] += c % w;
                        sumY[s] += c / w;
                        count[s]++;
                    }
                    for (int s = 0; s < n; s++)
                        if (count[s] > 0)
                        {
                            sx[s] = (int)((sumX[s] * 2 + count[s]) / (2 * count[s]));
                            sy[s] = (int)((sumY[s] * 2 + count[s]) / (2 * count[s]));
                        }
                }

                // Borders that wander: each cell joins the seed nearest to it after a low-frequency warp.
                int side = (int)Math.Sqrt(meanArea);
                int amp = Math.Max(1, side * 2 / 5), period = Math.Max(4, side * 2);
                BuildBuckets(sx, sy);
                foreach (int c in land)
                {
                    int x = c % w, y = c / w;
                    int dx = (MapRandom.Fractal(seed, "warp-x", x, y, period, 2) - 32768) * amp / 32768;
                    int dy = (MapRandom.Fractal(seed, "warp-y", x, y, period, 2) - 32768) * amp / 32768;
                    cells[c] = Nearest(x + dx, y + dy, sx, sy);
                }

                // Provinces well under the mean area take a layer of cells from larger neighbours.
                for (int pass = 0; pass < 6; pass++) if (!GrowSmall()) break;

                // Smooth the borders first: a cell joins the province holding at least five of its eight
                // neighbours. Most jagged contacts at a junction go here.
                for (int pass = 0; pass < 2; pass++) Smooth();

                // Each repair can disturb another's work, so they run in turn until none changes anything.
                for (int round = 0; round < 12; round++)
                {
                    bool changed = KeepLargestPieces();
                    changed |= FixCorners();
                    changed |= FixWeakContacts();
                    if (!changed) break;
                }

                var area = new int[n];
                foreach (int v in cells) if (v >= 0) area[v]++;
                int empty = area.Count(a => a == 0);
                if (empty > 0)
                {
                    Problems.Add(new MapProblem(ProblemKind.Shape, "one-piece", $"{empty} provinces lost every cell"));
                    return false;
                }
                Reindex();
                return true;
            }

            private List<int> Darts(int[] order, long r2, int enough)
            {
                int r = (int)MapRandom.Isqrt(r2) + 1;
                int bw = w / r + 1, bh = h / r + 1;
                var buckets = new List<int>[bw * bh];
                var accepted = new List<int>();
                foreach (int c in order)
                {
                    int x = c % w, y = c / w, bx = x / r, by = y / r;
                    bool ok = true;
                    for (int j = Math.Max(0, by - 1); ok && j <= Math.Min(bh - 1, by + 1); j++)
                    for (int i = Math.Max(0, bx - 1); ok && i <= Math.Min(bw - 1, bx + 1); i++)
                    {
                        var b = buckets[j * bw + i];
                        if (b == null) continue;
                        foreach (int o in b)
                        {
                            long ddx = o % w - x, ddy = o / w - y;
                            if (ddx * ddx + ddy * ddy < r2) { ok = false; break; }
                        }
                    }
                    if (!ok) continue;
                    accepted.Add(c);
                    (buckets[by * bw + bx] ?? (buckets[by * bw + bx] = new List<int>())).Add(c);
                }
                return accepted;
            }

            // The seed nearest a point, ties to the lowest index, searched ring by ring over a bucket grid.
            private int[] bucketSeeds, bucketStart;
            private int bucketSize, bucketW, bucketH;
            private int[] bucketOfX, bucketOfY;

            private void BuildBuckets(int[] sx, int[] sy)
            {
                bucketSize = Math.Max(4, (int)Math.Sqrt(meanArea));
                bucketW = w / bucketSize + 1;
                bucketH = h / bucketSize + 1;
                var count = new int[bucketW * bucketH + 1];
                for (int s = 0; s < sx.Length; s++) count[Bucket(sx[s], sy[s]) + 1]++;
                for (int b = 0; b < bucketW * bucketH; b++) count[b + 1] += count[b];
                bucketStart = (int[])count.Clone();
                bucketSeeds = new int[sx.Length];
                var fill = (int[])count.Clone();
                for (int s = 0; s < sx.Length; s++) bucketSeeds[fill[Bucket(sx[s], sy[s])]++] = s;
            }

            private int Bucket(int x, int y) =>
                Math.Max(0, Math.Min(bucketH - 1, y / bucketSize)) * bucketW + Math.Max(0, Math.Min(bucketW - 1, x / bucketSize));

            private int Nearest(int x, int y, int[] sx, int[] sy)
            {
                long best = long.MaxValue;
                int who = -1;
                int bx = Math.Max(0, Math.Min(bucketW - 1, x / bucketSize)), by = Math.Max(0, Math.Min(bucketH - 1, y / bucketSize));
                for (int ring = 0; ring < bucketW + bucketH; ring++)
                {
                    // Every cell of ring r is at least (r - 1) buckets away; stop once that cannot beat the best.
                    if (who >= 0)
                    {
                        long reach = (long)(ring - 1) * bucketSize;
                        if (reach > 0 && reach * reach > best) break;
                    }
                    for (int j = by - ring; j <= by + ring; j++)
                    for (int i = bx - ring; i <= bx + ring; i++)
                    {
                        if (i < 0 || j < 0 || i >= bucketW || j >= bucketH) continue;
                        if (Math.Max(Math.Abs(i - bx), Math.Abs(j - by)) != ring) continue;
                        int b = j * bucketW + i;
                        for (int k = bucketStart[b]; k < bucketStart[b + 1]; k++)
                        {
                            int s = bucketSeeds[k];
                            long dx = sx[s] - x, dy = sy[s] - y, d = dx * dx + dy * dy;
                            if (d < best || (d == best && s < who)) { best = d; who = s; }
                        }
                    }
                }
                return who;
            }

            // A province in pieces keeps its largest; the rest goes to whichever neighbour it touches most.
            private bool KeepLargestPieces()
            {
                bool changed = false;
                var seen = new bool[cells.Length];
                var byProvince = new Dictionary<int, List<List<int>>>();
                var stack = new Stack<int>();
                for (int c = 0; c < cells.Length; c++)
                {
                    int v = cells[c];
                    if (v < 0 || seen[c]) continue;
                    var part = new List<int>();
                    seen[c] = true;
                    stack.Push(c);
                    while (stack.Count > 0)
                    {
                        int k = stack.Pop();
                        part.Add(k);
                        int x = k % w, y = k / w;
                        if (x > 0 && !seen[k - 1] && cells[k - 1] == v) { seen[k - 1] = true; stack.Push(k - 1); }
                        if (x + 1 < w && !seen[k + 1] && cells[k + 1] == v) { seen[k + 1] = true; stack.Push(k + 1); }
                        if (y > 0 && !seen[k - w] && cells[k - w] == v) { seen[k - w] = true; stack.Push(k - w); }
                        if (y + 1 < h && !seen[k + w] && cells[k + w] == v) { seen[k + w] = true; stack.Push(k + w); }
                    }
                    if (!byProvince.TryGetValue(v, out var list)) byProvince[v] = list = new List<List<int>>();
                    list.Add(part);
                }
                var loose = new List<int>();
                foreach (var kv in byProvince)
                {
                    if (kv.Value.Count < 2) continue;
                    var keep = kv.Value.OrderByDescending(p => p.Count).ThenBy(p => p.Min()).First();
                    foreach (var p in kv.Value)
                        if (p != keep) { foreach (int c in p) { cells[c] = Land; loose.Add(c); } }
                    changed = true;
                }
                // Hand loose cells to their neighbours, from the outside in.
                loose.Sort();
                while (loose.Count > 0)
                {
                    var still = new List<int>();
                    var assign = new List<(int c, int v)>();
                    foreach (int c in loose)
                    {
                        int v = MostCommonNeighbour(c, -1, -1);
                        if (v < 0) still.Add(c);
                        else assign.Add((c, v));
                    }
                    if (assign.Count == 0) { foreach (int c in still) cells[c] = MapGeometry.Sea; break; }
                    foreach (var (c, v) in assign) cells[c] = v;
                    loose = still;
                }
                return changed;
            }

            private int MostCommonNeighbour(int c, int not1, int not2)
            {
                int x = c % w, y = c / w;
                Span4 s = default;
                if (x > 0) s.Add(cells[c - 1]);
                if (x + 1 < w) s.Add(cells[c + 1]);
                if (y > 0) s.Add(cells[c - w]);
                if (y + 1 < h) s.Add(cells[c + w]);
                return s.Most(not1, not2);
            }

            private struct Span4
            {
                private int a, b, c, d, count;

                public void Add(int v)
                {
                    switch (count++)
                    {
                        case 0: a = v; break;
                        case 1: b = v; break;
                        case 2: c = v; break;
                        default: d = v; break;
                    }
                }

                public int Most(int not1, int not2)
                {
                    int best = -1, bestCount = 0;
                    for (int i = 0; i < count; i++)
                    {
                        int v = i == 0 ? a : i == 1 ? b : i == 2 ? c : d;
                        if (v < 0 || v == not1 || v == not2) continue;
                        int k = (a == v ? 1 : 0) + (count > 1 && b == v ? 1 : 0) + (count > 2 && c == v ? 1 : 0) + (count > 3 && d == v ? 1 : 0);
                        if (k > bestCount || (k == bestCount && v < best)) { best = v; bestCount = k; }
                    }
                    return best;
                }
            }

            private bool GrowSmall()
            {
                var area = new int[n];
                foreach (int v in cells) if (v >= 0) area[v]++;
                var take = new List<(int c, int to)>();
                for (int c = 0; c < cells.Length; c++)
                {
                    int v = cells[c];
                    if (v < 0 || area[v] <= meanArea * 0.85) continue;
                    int best = -1;
                    foreach (int o in Around(c))
                        if (o >= 0 && o != v && area[o] < meanArea * 0.7 && (best < 0 || area[o] < area[best])) best = o;
                    if (best >= 0) take.Add((c, best));
                }
                foreach (var (c, to) in take) cells[c] = to;
                return take.Count > 0;
            }

            private void Smooth()
            {
                var next = (int[])cells.Clone();
                var counts = new Dictionary<int, int>();
                for (int y = 1; y + 1 < h; y++)
                for (int x = 1; x + 1 < w; x++)
                {
                    int c = y * w + x, v = cells[c];
                    if (v < 0) continue;
                    counts.Clear();
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int o = cells[c + dy * w + dx];
                        if (o >= 0) counts[o] = counts.TryGetValue(o, out int k) ? k + 1 : 1;
                    }
                    foreach (var kv in counts)
                        if (kv.Value >= 5 && kv.Key != v) { next[c] = kv.Key; break; }
                }
                cells = next;
            }

            // Two provinces meeting only at a corner, across two others. The corner cell joins a side
            // neighbour; when the two side provinces were not neighbours, that makes them touch along one
            // edge, which the next repair grows into a real border.
            private bool FixCorners()
            {
                var counts = MapChecks.SharedEdges(cells, w, h);
                bool Real(int a, int b) => counts.TryGetValue(MapChecks.Key(a, b), out int e) && e >= 3;
                bool changed = false;
                for (int y = 0; y + 1 < h; y++)
                for (int x = 0; x + 1 < w; x++)
                {
                    int i = y * w + x;
                    int p = cells[i], q = cells[i + 1], r = cells[i + w], s = cells[i + w + 1];
                    if (p < 0 || q < 0 || r < 0 || s < 0) continue;
                    if (p != q && p != r && s != q && s != r && p != s && !Real(p, s)) { cells[i + w + 1] = q; changed = true; }
                    else if (q != p && q != s && r != p && r != s && q != r && !Real(q, r)) { cells[i + w] = p; changed = true; }
                }
                return changed;
            }

            // Two provinces sharing one or two cell edges would look like neighbours but not be. The contact
            // grows into a real border: a contact cell of one side joins the other, when everything that
            // cell touches already borders the side it joins, so no new short contact appears. Where the
            // contact is on the coast and cannot grow, its cells become water.
            private bool FixWeakContacts()
            {
                var counts = MapChecks.SharedEdges(cells, w, h);
                bool Real(int a, int b) => a == b || (counts.TryGetValue(MapChecks.Key(a, b), out int e) && e >= 3);
                bool changed = false;
                var touched = new HashSet<int>();
                foreach (var kv in counts.Where(k => k.Value < 3).OrderBy(k => k.Key))
                {
                    var (a, b) = MapChecks.Unkey(kv.Key);
                    if (touched.Contains(a) || touched.Contains(b)) continue;
                    int flip = -1, to = -1;
                    foreach (var (from, other) in new[] { (a, b), (b, a) })
                    {
                        foreach (int c in Contact(from, other))
                        {
                            if (Around(c).All(v => v < 0 || v == from || Real(other, v))) { flip = c; to = other; break; }
                        }
                        if (flip >= 0) break;
                    }
                    if (flip < 0)
                    {
                        // On the coast: the side with fewer contact cells gives them to the water beside them.
                        var side = Contact(a, b).Count <= Contact(b, a).Count ? Contact(a, b) : Contact(b, a);
                        foreach (int c in side)
                        {
                            int water = Around(c).Where(v => v < 0 && v != Land).DefaultIfEmpty(0).Max();
                            if (water < 0) { cells[c] = water; changed = true; }
                        }
                    }
                    else
                    {
                        cells[flip] = to;
                        changed = true;
                    }
                    touched.Add(a);
                    touched.Add(b);
                }
                return changed;
            }

            private List<int> Contact(int from, int other)
            {
                var list = new List<int>();
                for (int c = 0; c < cells.Length; c++)
                    if (cells[c] == from && Around(c).Contains(other)) list.Add(c);
                return list;
            }

            private IEnumerable<int> Around(int c)
            {
                int x = c % w, y = c / w;
                if (x > 0) yield return cells[c - 1];
                if (x + 1 < w) yield return cells[c + 1];
                if (y > 0) yield return cells[c - w];
                if (y + 1 < h) yield return cells[c + w];
            }

            // Province indices in reading order of their centres, so a world lists north-west first.
            private void Reindex()
            {
                var sumX = new long[n];
                var sumY = new long[n];
                var count = new int[n];
                for (int c = 0; c < cells.Length; c++)
                {
                    int v = cells[c];
                    if (v < 0) continue;
                    sumX[v] += c % w;
                    sumY[v] += c / w;
                    count[v]++;
                }
                var ccx = Enumerable.Range(0, n).Select(i => (int)(sumX[i] / count[i])).ToArray();
                var ccy = Enumerable.Range(0, n).Select(i => (int)(sumY[i] / count[i])).ToArray();
                int band = Math.Max(1, (int)Math.Sqrt(meanArea));
                var order = Enumerable.Range(0, n).OrderBy(i => ccy[i] / band).ThenBy(i => ccx[i]).ThenBy(i => i).ToArray();
                var newIndex = new int[n];
                for (int k = 0; k < n; k++) newIndex[order[k]] = k;
                for (int c = 0; c < cells.Length; c++) if (cells[c] >= 0) cells[c] = newIndex[cells[c]];
                cx = order.Select(i => ccx[i]).ToArray();
                cy = order.Select(i => ccy[i]).ToArray();
            }

            // ------------------------------------------------------------------------------
            // 4-5. Adjacency and ridges
            // ------------------------------------------------------------------------------

            private void MakeAdjacency()
            {
                shared = MapChecks.SharedEdges(cells, w, h);
                edges = MapChecks.EdgesByPair(cells, w, h);
                adj = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
                foreach (var kv in shared.OrderBy(k => k.Key))
                {
                    if (kv.Value < 3) continue;
                    var (a, b) = MapChecks.Unkey(kv.Key);
                    adj[a].Add(b);
                    adj[b].Add(a);
                }
                foreach (var l in adj) l.Sort();
            }

            private void MakeRidges()
            {
                int wanted = settings.Size == MapSize.Small ? 1 : 1 + MapRandom.Int(seed, "ridges", 0, settings.Size == MapSize.Medium ? 2 : 3);
                long minD = (long)w * 30 / 100, maxD = (long)w * 60 / 100;
                for (int r = 0; r < wanted; r++)
                {
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        int p = MapRandom.Int(seed, "ridge-a", r * 31 + attempt, n);
                        var far = Enumerable.Range(0, n).Where(q =>
                        {
                            long dx = cx[q] - cx[p], dy = cy[q] - cy[p], d2 = dx * dx + dy * dy;
                            return d2 >= minD * minD && d2 <= maxD * maxD;
                        }).ToList();
                        if (far.Count == 0) continue;
                        int qq = far[MapRandom.Int(seed, "ridge-b", r * 31 + attempt, far.Count)];
                        var line = new[] { cx[p], cy[p], cx[qq], cy[qq] };
                        var crossed = edges
                            .Where(kv => { var (a, b) = MapChecks.Unkey(kv.Key); return adj[a].Contains(b); })
                            .Where(kv => kv.Value.Any(e => MapChecks.Crosses(line, e)))
                            .Select(kv => (key: kv.Key, t: Along(line, kv.Value)))
                            .OrderBy(x => x.t).ThenBy(x => x.key)
                            .Select(x => x.key).ToList();
                        if (crossed.Count < 3) continue;

                        var ridge = new RidgeLine { Line = line };
                        int passes = Math.Min(crossed.Count - 1, 1 + MapRandom.Int(seed, "passes", r, 2));
                        var passAt = passes == 1
                            ? new HashSet<int> { crossed.Count / 2 }
                            : new HashSet<int> { crossed.Count / 3, crossed.Count * 2 / 3 };
                        for (int k = 0; k < crossed.Count; k++)
                        {
                            var (a, b) = MapChecks.Unkey(crossed[k]);
                            bool pass = passAt.Contains(k) || passPairs.Contains(crossed[k]);
                            if (!pass)
                            {
                                adj[a].Remove(b);
                                adj[b].Remove(a);
                                if (adj[a].Count < 2 || adj[b].Count < 2 || MapChecks.Bfs(n, adj, a)[b] < 0)
                                {
                                    adj[a].Add(b); adj[a].Sort();
                                    adj[b].Add(a); adj[b].Sort();
                                    pass = true;
                                }
                            }
                            if (pass)
                            {
                                passPairs.Add(crossed[k]);
                                ridge.Passes.Add(new[] { a.ToString(), b.ToString() });
                            }
                            else
                            {
                                cutPairs.Add((a, b));
                                ridge.Cuts.Add(new[] { a.ToString(), b.ToString() });
                            }
                        }
                        ridges.Add(ridge);
                        break;
                    }
                }
            }

            // Where along the ridge a border sits: the projection of its first edge's midpoint.
            private static long Along(int[] line, List<(int x0, int y0, int x1, int y1)> e)
            {
                long mx = e[0].x0 + e[0].x1, my = e[0].y0 + e[0].y1;
                long dx = line[2] - line[0], dy = line[3] - line[1];
                return (mx - 2L * line[0]) * dx + (my - 2L * line[1]) * dy;
            }

            // ------------------------------------------------------------------------------
            // 6-7. Terrain, coasts and ports
            // ------------------------------------------------------------------------------

            private void MakeTerrain()
            {
                var hSum = new long[n];
                var mSum = new long[n];
                var count = new int[n];
                int mPeriod = Math.Max(4, w / 4);
                for (int c = 0; c < cells.Length; c++)
                {
                    int v = cells[c];
                    if (v < 0) continue;
                    // Relief is its own noise, not the land's height: the land's height carries the
                    // shape's falloff, which would put every mountain in the middle of a continent.
                    hSum[v] += MapRandom.Fractal(seed, "relief", c % w, c / w, Math.Max(4, w / 5), 3);
                    mSum[v] += MapRandom.Fractal(seed, "moisture", c % w, c / w, mPeriod, 3);
                    count[v]++;
                }
                var rough = new HashSet<int>();
                foreach (var (a, b) in cutPairs) { rough.Add(a); rough.Add(b); }
                foreach (long k in passPairs) { var (a, b) = MapChecks.Unkey(k); rough.Add(a); rough.Add(b); }
                var hgt = Enumerable.Range(0, n).Select(i => hSum[i] / count[i] + (rough.Contains(i) ? 12000 : 0)).ToArray();
                var wet = Enumerable.Range(0, n).Select(i => mSum[i] / count[i]).ToArray();

                // Wet near the water.
                for (int c = 0; c < cells.Length; c++)
                {
                    int v = cells[c];
                    if (v < 0) continue;
                    int x = c % w, y = c / w;
                    if ((x > 0 && cells[c - 1] == MapGeometry.Water) || (x + 1 < w && cells[c + 1] == MapGeometry.Water)
                        || (y > 0 && cells[c - w] == MapGeometry.Water) || (y + 1 < h && cells[c + w] == MapGeometry.Water))
                        wet[v] += 400;
                }

                int mountains = (n * 12 + 50) / 100, hills = (n * 18 + 50) / 100, marsh = (n * 10 + 50) / 100, forest = (n * 25 + 50) / 100;
                terrain = Enumerable.Repeat(Terrain.Plains, n).ToArray();
                var byHeight = Enumerable.Range(0, n).OrderByDescending(i => hgt[i]).ThenBy(i => i).ToList();
                for (int k = 0; k < mountains; k++) terrain[byHeight[k]] = Terrain.Mountains;
                for (int k = mountains; k < mountains + hills; k++) terrain[byHeight[k]] = Terrain.Hills;
                var rest = byHeight.Skip(mountains + hills).ToList();
                var lowHalf = new HashSet<int>(rest.Skip(rest.Count / 2));
                var byWet = rest.OrderByDescending(i => wet[i]).ThenBy(i => i).ToList();
                int placed = 0;
                foreach (int i in byWet)
                    if (placed < marsh && lowHalf.Contains(i)) { terrain[i] = Terrain.Marsh; placed++; }
                placed = 0;
                foreach (int i in byWet)
                    if (placed < forest && terrain[i] == Terrain.Plains) { terrain[i] = Terrain.Forest; placed++; }
            }

            private void MakeCoasts()
            {
                coastLen = new int[n];
                shoreLen = new int[n];
                onChartEdge = new bool[n];
                for (int c = 0; c < cells.Length; c++)
                {
                    int v = cells[c];
                    if (v < 0) continue;
                    int x = c % w, y = c / w;
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1) onChartEdge[v] = true;
                    void Look(int o)
                    {
                        if (cells[o] == MapGeometry.Sea) coastLen[v]++;
                        else if (cells[o] == MapGeometry.Water) shoreLen[v]++;
                    }
                    if (x > 0) Look(c - 1);
                    if (x + 1 < w) Look(c + 1);
                    if (y > 0) Look(c - w);
                    if (y + 1 < h) Look(c + w);
                }
                port = new bool[n];
                var coastal = Enumerable.Range(0, n).Where(Coastal).ToList();
                if (coastal.Count == 0) return;
                var lengths = coastal.Select(i => coastLen[i] + shoreLen[i]).OrderBy(x => x).ToList();
                int median = lengths[lengths.Count / 2];
                var eligible = coastal.Where(i => coastLen[i] + shoreLen[i] >= median).ToList();
                int ports = Math.Max(1, (coastal.Count + 1) / 3);
                var chosen = new List<int> { eligible.OrderByDescending(i => coastLen[i] + shoreLen[i]).ThenBy(i => i).First() };
                var near = MapChecks.Bfs(n, adj, chosen[0]);
                while (chosen.Count < Math.Min(ports, eligible.Count))
                {
                    int next = eligible.Where(i => !chosen.Contains(i))
                        .OrderByDescending(i => near[i] < 0 ? int.MaxValue : near[i]).ThenByDescending(i => coastLen[i]).ThenBy(i => i).First();
                    chosen.Add(next);
                    var d = MapChecks.Bfs(n, adj, next);
                    for (int i = 0; i < n; i++) if (d[i] >= 0 && (near[i] < 0 || d[i] < near[i])) near[i] = d[i];
                }
                foreach (int i in chosen) port[i] = true;
            }

            private bool Coastal(int i) => coastLen[i] + shoreLen[i] > 0;

            // ------------------------------------------------------------------------------
            // Regions, goods, capitals and realms
            // ------------------------------------------------------------------------------

            private void MakeRegions()
            {
                regionCount = Math.Max(2, (n + 4) / 8);
                var centres = new List<int> { MapRandom.Int(seed, "region-first", 0, n) };
                var near = MapChecks.Bfs(n, adj, centres[0]);
                while (centres.Count < regionCount)
                {
                    int next = Enumerable.Range(0, n).Where(i => !centres.Contains(i))
                        .OrderByDescending(i => near[i]).ThenBy(i => MapRandom.Hash(seed, "region-tie", i)).First();
                    centres.Add(next);
                    var d = MapChecks.Bfs(n, adj, next);
                    for (int i = 0; i < n; i++) if (d[i] >= 0 && d[i] < near[i]) near[i] = d[i];
                }
                region = Enumerable.Repeat(-1, n).ToArray();
                var queue = new Queue<int>();
                for (int r = 0; r < centres.Count; r++) { region[centres[r]] = r; queue.Enqueue(centres[r]); }
                while (queue.Count > 0)
                {
                    int p = queue.Dequeue();
                    foreach (int q in adj[p])
                        if (region[q] < 0) { region[q] = region[p]; queue.Enqueue(q); }
                }
            }

            private void MakeGoods()
            {
                good = new string[n];
                population = new int[n];
                for (int i = 0; i < n; i++)
                {
                    var options = TerrainTable.Goods(terrain[i], Coastal(i));
                    good[i] = options[MapRandom.Int(seed, "good", i, options.Length)];
                    population[i] = TerrainTable.StartPopulation(terrain[i]) + (Coastal(i) ? 1 : 0);
                }
            }

            private bool PlaceCapitals()
            {
                int spacing = MapChecks.CapitalSpacing(n, countries);
                var dist = Enumerable.Range(0, n).Select(i => MapChecks.Bfs(n, adj, i)).ToArray();
                var candidates = Enumerable.Range(0, n)
                    .Where(i => terrain[i] != Terrain.Mountains && terrain[i] != Terrain.Marsh && adj[i].Count >= 3 && !onChartEdge[i])
                    .ToList();
                if (candidates.Count < countries)
                {
                    Problems.Add(new MapProblem(ProblemKind.Fairness, "capital", $"only {candidates.Count} provinces may hold a capital"));
                    return false;
                }
                var baseValue = Enumerable.Range(0, n).Select(i => MapChecks.ProvinceValue(terrain[i], population[i], false)).ToArray();
                var capitalValue = Enumerable.Range(0, n).Select(i => MapChecks.ProvinceValue(terrain[i], population[i] + 2, true)).ToArray();
                var rough = Enumerable.Range(0, n).Select(i => terrain[i] == Terrain.Hills || terrain[i] == Terrain.Mountains || terrain[i] == Terrain.Marsh).ToArray();

                double Score(List<int> chosen, out int[] owners)
                {
                    double penalty = 0;
                    for (int a = 0; a < chosen.Count; a++)
                    for (int b = a + 1; b < chosen.Count; b++)
                    {
                        int d = dist[chosen[a]][chosen[b]];
                        if (d < 0) penalty += 1000;
                        else if (d < spacing) penalty += 50 * (spacing - d);
                    }
                    var seats = chosen.OrderBy(i => i).ToArray();
                    owners = GrowRealms(seats, dist);
                    if (owners == null) return penalty + 500;
                    var value = (int[])baseValue.Clone();
                    foreach (int sc in seats) value[sc] = capitalValue[sc];
                    var m = MapChecks.MeasureRaw(n, adj, seats, owners, value, good, rough, dist);
                    double excess = MapChecks.Excess(m);
                    double reachSpread = (double)m.Max(x => x.Reach60) / Math.Max(1, m.Min(x => x.Reach60));
                    double valueSpread = (double)m.Max(x => x.Value60) / Math.Max(1, m.Min(x => x.Value60));
                    return penalty + excess * 10 + (reachSpread - 1) + (valueSpread - 1);
                }

                var nearCandidates = new List<int>[n];
                for (int i = 0; i < n; i++)
                    nearCandidates[i] = candidates.Where(q => dist[i][q] >= 1 && dist[i][q] <= 3).ToList();

                List<int> best = null;
                int[] bestOwners = null;
                double bestScore = double.MaxValue;
                int trials = 4;
                for (int t = 0; t < trials && bestScore >= 1; t++)
                {
                    var chosen = new List<int> { candidates[MapRandom.Int(seed, "capital-first", t, candidates.Count)] };
                    while (chosen.Count < countries)
                    {
                        int next = candidates.Where(i => !chosen.Contains(i))
                            .OrderByDescending(i => chosen.Min(c => dist[c][i] < 0 ? int.MaxValue : dist[c][i]))
                            .ThenBy(i => MapRandom.Hash(seed, "capital-tie", t, i)).First();
                        chosen.Add(next);
                    }
                    // Graph Lloyd: each capital moves to the candidate nearest, in total steps, to the
                    // provinces it reaches, so the reaches even out before the fine search.
                    for (int round = 0; round < 6; round++)
                    {
                        bool moved = false;
                        for (int k = 0; k < chosen.Count; k++)
                        {
                            var mine = Enumerable.Range(0, n).Where(p =>
                            {
                                int dk = dist[chosen[k]][p];
                                return dk >= 0 && chosen.All(o => o == chosen[k] || dist[o][p] < 0 || dist[o][p] > dk);
                            }).ToList();
                            if (mine.Count == 0) continue;
                            int bestC = chosen[k];
                            long bestSum = mine.Sum(p => (long)dist[bestC][p]);
                            foreach (int q in candidates)
                            {
                                if (chosen.Contains(q) || dist[chosen[k]][q] > 3) continue;
                                long sum = mine.Sum(p => (long)dist[q][p]);
                                if (sum < bestSum) { bestSum = sum; bestC = q; }
                            }
                            if (bestC != chosen[k]) { chosen[k] = bestC; moved = true; }
                        }
                        if (!moved) break;
                    }

                    // Annealing: move one capital to a candidate up to three steps away; take every move that
                    // helps, and early on some that do not, so the search leaves local optima. Seeded, so the
                    // same seed always finds the same capitals.
                    double score = Score(chosen, out var owners);
                    var bestHere = chosen.ToList();
                    double bestHereScore = score;
                    int[] bestHereOwners = owners;
                    var rng = new MapRng(seed, "anneal-" + t);
                    const int steps = 1000;
                    for (int step = 0; step < steps && bestHereScore >= 1; step++)
                    {
                        int k = rng.Int(chosen.Count);
                        var near = nearCandidates[chosen[k]];
                        if (near.Count == 0) continue;
                        int q = near[rng.Int(near.Count)];
                        if (chosen.Contains(q)) continue;
                        int old = chosen[k];
                        chosen[k] = q;
                        double s = Score(chosen, out var o);
                        double temperature = 3.0 * (1.0 - (double)step / steps) + 0.01;
                        // The acceptance odds fall with how much worse the move is, as 1 / (1 + x + x^2 / 2), a
                        // stand-in for e^-x built from + - * / only: Math.Exp may differ in its last bit between
                        // runtimes, and one flipped draw would make a different world.
                        double x = (s - score) / temperature;
                        bool accept = s <= score || rng.Int(1 << 16) < (int)(65536 / (1 + x + x * x / 2));
                        if (accept)
                        {
                            score = s;
                            owners = o;
                            if (s < bestHereScore) { bestHereScore = s; bestHere = chosen.ToList(); bestHereOwners = o; }
                        }
                        else chosen[k] = old;
                    }
                    chosen = bestHere;
                    score = bestHereScore;
                    owners = bestHereOwners;
                    if (score < bestScore) { bestScore = score; best = chosen.ToList(); bestOwners = owners; }
                }

                capitals = best.OrderBy(i => i).ToArray();
                owner = bestOwners;
                if (owner == null)
                {
                    Problems.Add(new MapProblem(ProblemKind.Fairness, "realms", "no placement of capitals let every realm grow"));
                    return false;
                }
                isCapital = new bool[n];
                foreach (int c in capitals) { isCapital[c] = true; population[c] += 2; }
                return true;
            }

            // Each country in turn takes the next nearest province to its capital, never one bordering another's start.
            private int[] GrowRealms(int[] seats, int[][] dist)
            {
                var own = Enumerable.Repeat(-1, n).ToArray();
                for (int c = 0; c < seats.Length; c++)
                {
                    if (own[seats[c]] >= 0) return null;
                    own[seats[c]] = c;
                }
                for (int c = 0; c < seats.Length; c++)
                    foreach (int q in adj[seats[c]])
                        if (own[q] >= 0 && own[q] != c) return null;
                for (int size = 1; size < settings.StartingRealm; size++)
                {
                    for (int c = 0; c < seats.Length; c++)
                    {
                        int pick = -1, bestD = int.MaxValue;
                        for (int p = 0; p < n; p++)
                        {
                            if (own[p] >= 0) continue;
                            bool touchesMine = false, touchesOther = false;
                            foreach (int q in adj[p])
                            {
                                if (own[q] == c) touchesMine = true;
                                else if (own[q] >= 0) touchesOther = true;
                            }
                            if (!touchesMine || touchesOther) continue;
                            // Nearest to its own capital first, then farthest from the others', so realms
                            // grow away from each other.
                            int others = int.MaxValue;
                            for (int o = 0; o < seats.Length; o++)
                                if (o != c && dist[seats[o]][p] >= 0 && dist[seats[o]][p] < others) others = dist[seats[o]][p];
                            int d = dist[seats[c]][p] * 1000 - Math.Min(others, 999);
                            if (d < bestD) { bestD = d; pick = p; }
                        }
                        if (pick < 0) return null;
                        own[pick] = c;
                    }
                }
                return own;
            }

            // At most one scarce good per realm, and each scarce good somewhere in neutral land.
            private void FixScarceGoods()
            {
                for (int c = 0; c < capitals.Length; c++)
                {
                    bool seen = false;
                    for (int p = 0; p < n; p++)
                    {
                        if (owner[p] != c || !TerrainTable.Scarce.Contains(good[p])) continue;
                        if (!seen) { seen = true; continue; }
                        good[p] = TerrainTable.Goods(terrain[p], Coastal(p)).First(g => !TerrainTable.Scarce.Contains(g));
                    }
                }
                foreach (string scarce in TerrainTable.Scarce)
                {
                    if (Enumerable.Range(0, n).Any(p => owner[p] < 0 && good[p] == scarce)) continue;
                    var options = Enumerable.Range(0, n)
                        .Where(p => owner[p] < 0 && TerrainTable.Goods(terrain[p], Coastal(p)).Contains(scarce) && !TerrainTable.Scarce.Contains(good[p]))
                        .ToList();
                    if (options.Count > 0) good[options[MapRandom.Int(seed, "scarce-" + scarce, 0, options.Count)]] = scarce;
                }
            }

            // ------------------------------------------------------------------------------
            // The world, with placeholder ids until it is named
            // ------------------------------------------------------------------------------

            private WorldMap Assemble()
            {
                var map = new WorldMap
                {
                    Name = "Generated",
                    Generated = new GenerationInfo
                    {
                        Countries = countries,
                        Settings = settings.Clone(),
                        NeutralPercent = owner.Count(o => o < 0) * 100 / n,
                    },
                };
                var (col, row) = Layout();
                for (int c = 0; c < capitals.Length; c++)
                    map.Countries.Add(new CountryData { Id = "c" + c, Name = "C" + c, StartGold = 40, StartArmy = 30 });
                for (int i = 0; i < n; i++)
                    map.Provinces.Add(new ProvinceData
                    {
                        Id = "p" + i,
                        Name = "P" + i,
                        Column = col[i],
                        Row = row[i],
                        Owner = owner[i] >= 0 ? "c" + owner[i] : null,
                        Capital = isCapital[i],
                        Forts = isCapital[i] ? 1 : 0,
                        Terrain = terrain[i],
                        Coastal = Coastal(i),
                        Port = port[i],
                        Population = population[i],
                        Good = good[i],
                        Region = "r" + region[i],
                    });
                for (int a = 0; a < n; a++)
                    foreach (int b in adj[a])
                        if (a < b) map.Borders.Add(new[] { "p" + a, "p" + b });
                for (int r = 0; r < regionCount; r++)
                    map.Regions.Add(new RegionData { Id = "r" + r, Name = "R" + r, Provinces = Enumerable.Range(0, n).Where(i => region[i] == r).Select(i => "p" + i).ToList() });

                var geo = MapGeometry.Build(cells, w, h, Enumerable.Range(0, n).Select(i => "p" + i).ToList());
                var cuts = new HashSet<long>(cutPairs.Select(x => MapChecks.Key(x.a, x.b)));
                foreach (var b in geo.Borders)
                {
                    if (b.B == null) continue;
                    long key = MapChecks.Key(int.Parse(b.A.Substring(1)), int.Parse(b.B.Substring(1)));
                    if (cuts.Contains(key)) b.Kind = BorderLine.Ridge;
                    else if (passPairs.Contains(key)) b.Kind = BorderLine.Pass;
                }
                foreach (var r in ridges)
                {
                    geo.Ridges.Add(new RidgeLine
                    {
                        Line = r.Line,
                        Passes = r.Passes.Select(x => new[] { "p" + x[0], "p" + x[1] }).ToList(),
                        Cuts = r.Cuts.Select(x => new[] { "p" + x[0], "p" + x[1] }).ToList(),
                    });
                }
                map.Geometry = geo;
                return map;
            }

            // A coarse grid position per province for the logs' owner grid: each province in its own cell.
            private (int[] col, int[] row) Layout()
            {
                int cols = (int)MapRandom.Isqrt((long)n * 2 * 16 / 9) + 1;
                int rows = (2 * n + cols - 1) / cols + 1;
                var taken = new bool[cols * rows];
                var col = new int[n];
                var row = new int[n];
                for (int i = 0; i < n; i++)
                {
                    int c0 = Math.Min(cols - 1, cx[i] * cols / w), r0 = Math.Min(rows - 1, cy[i] * rows / h);
                    bool done = false;
                    for (int ring = 0; !done && ring < cols + rows; ring++)
                    for (int r = r0 - ring; !done && r <= r0 + ring; r++)
                    for (int c = c0 - ring; !done && c <= c0 + ring; c++)
                    {
                        if (r < 0 || c < 0 || r >= rows || c >= cols) continue;
                        if (Math.Max(Math.Abs(r - r0), Math.Abs(c - c0)) != ring || taken[r * cols + c]) continue;
                        taken[r * cols + c] = true;
                        col[i] = c;
                        row[i] = r;
                        done = true;
                    }
                }
                return (col, row);
            }

            // ------------------------------------------------------------------------------
            // 12. Names
            // ------------------------------------------------------------------------------

            private void NameEverything(WorldMap map)
            {
                var tables = new TableNames(seed);
                var taken = new List<string>();
                var rename = new Dictionary<string, string>(StringComparer.Ordinal);
                bool failed = false;

                for (int c = 0; c < map.Countries.Count; c++)
                {
                    string id = tables.CountryName(c, taken);
                    if (id == null) { failed = true; continue; }
                    taken.Add(id);
                    rename[map.Countries[c].Id] = id;
                }
                var styles = new string[regionCount];
                for (int r = 0; r < regionCount; r++)
                {
                    styles[r] = tables.StyleFor(r);
                    string id = tables.Name(styles[r], "region", r, taken, null);
                    if (id == null) { failed = true; continue; }
                    taken.Add(id);
                    rename["r" + r] = id;
                }
                var named = new string[n];
                for (int r = 0; r < regionCount; r++)
                {
                    var members = Enumerable.Range(0, n).Where(i => region[i] == r).ToList();
                    IReadOnlyList<string> proposals = null;
                    if (nameMaker != null)
                    {
                        try { proposals = nameMaker.ProvinceNames(map.Regions[r], styles[r], map); }
                        catch (Exception) { proposals = null; } // a failing maker leaves the region to the tables
                    }
                    for (int k = 0; k < members.Count; k++)
                    {
                        int i = members[k];
                        var neighbourNames = adj[i].Select(j => named[j]).Where(x => x != null).ToList();
                        string id = null;
                        string proposal = proposals != null && k < proposals.Count ? proposals[k]?.Trim().ToLowerInvariant() : null;
                        if (proposal != null && NameRules.WhyNot(proposal, taken, neighbourNames) == null) id = proposal;
                        id = id ?? tables.Name(styles[r], "province", i, taken, neighbourNames);
                        if (id == null) { failed = true; continue; }
                        taken.Add(id);
                        named[i] = id;
                        rename["p" + i] = id;
                    }
                }
                if (failed) Problems.Add(new MapProblem(ProblemKind.Content, "names", "the tables ran out of valid names"));

                string R(string id) => id != null && rename.TryGetValue(id, out var to) ? to : id;
                for (int c = 0; c < map.Countries.Count; c++)
                {
                    var country = map.Countries[c];
                    country.Id = R(country.Id);
                    country.Name = NameRules.Display(country.Id);
                    country.Personality = TableNames.Personalities[(c + MapRandom.Int(seed, "personality", 0, TableNames.Personalities.Length)) % TableNames.Personalities.Length];
                }
                foreach (var p in map.Provinces)
                {
                    p.Id = R(p.Id);
                    p.Name = NameRules.Display(p.Id);
                    p.Owner = R(p.Owner);
                    p.Region = R(p.Region);
                }
                foreach (var b in map.Borders) { b[0] = R(b[0]); b[1] = R(b[1]); }
                for (int r = 0; r < map.Regions.Count; r++)
                {
                    var reg = map.Regions[r];
                    reg.Id = R(reg.Id);
                    reg.Name = NameRules.Display(reg.Id);
                    reg.Provinces = reg.Provinces.Select(R).ToList();
                    map.Generated.Styles[reg.Id] = styles[r];
                }
                foreach (var s in map.Geometry.Provinces) s.Id = R(s.Id);
                foreach (var b in map.Geometry.Borders) { b.A = R(b.A); b.B = R(b.B); }
                foreach (var r in map.Geometry.Ridges)
                {
                    foreach (var x in r.Passes) { x[0] = R(x[0]); x[1] = R(x[1]); }
                    foreach (var x in r.Cuts) { x[0] = R(x[0]); x[1] = R(x[1]); }
                }
                map.Name = NameRules.Display(R("r0"));
            }

            // ------------------------------------------------------------------------------
            // 11. Special places
            // ------------------------------------------------------------------------------

            private void PlaceSpecials(WorldMap map)
            {
                int wanted;
                switch (settings.Specials)
                {
                    case SpecialPlaces.None: wanted = 0; break;
                    case SpecialPlaces.Many: wanted = (n + 4) / 8; break;
                    default: wanted = Math.Max(1, (n + 7) / 15); break;
                }
                var order = Enumerable.Range(0, n).OrderBy(i => MapRandom.Hash(seed, "special-slot", i)).ThenBy(i => i).ToList();
                var chosen = new List<int>();
                var usedRegions = new HashSet<int>();
                foreach (int i in order)
                {
                    if (chosen.Count >= wanted) break;
                    if (owner[i] >= 0 || isCapital[i] || usedRegions.Contains(region[i])) continue;
                    var d = MapChecks.Bfs(n, adj, i);
                    if (chosen.Any(o => d[o] >= 0 && d[o] < MapChecks.SpecialSpacing)) continue;
                    chosen.Add(i);
                    usedRegions.Add(region[i]);
                }

                var tables = new TableSpecialPlaces(seed);
                foreach (int i in chosen.OrderBy(i => i))
                {
                    var p = map.Provinces[i];
                    var slot = new SpecialSlot
                    {
                        Province = p.Id,
                        Terrain = p.Terrain,
                        Region = p.Region,
                        Neighbours = adj[i].Select(j => map.Provinces[j].Id).ToList(),
                        Allowed = SpecialTemplates.AllowedOn(p.Terrain),
                    };
                    Slots.Add(slot);
                    SpecialPlace made = null;
                    if (specialMaker != null)
                    {
                        try { made = specialMaker.Make(slot, map); }
                        catch (Exception) { made = null; } // a failing maker leaves the slot to the tables
                    }
                    var otherNames = map.Provinces.Select(x => x.Name).Concat(map.Countries.Select(c => c.Name));
                    if (made == null || made.Province != slot.Province || !slot.Allowed.Contains(made.Template)
                        || string.IsNullOrWhiteSpace(made.Name) || made.Name.Length > 40
                        || SpecialTemplates.WhyNotStory(made.Story, otherNames) != null)
                        made = tables.Make(slot, map);
                    map.Specials.Add(made);
                    p.Special = made.Template;
                }
            }
        }
    }
}
