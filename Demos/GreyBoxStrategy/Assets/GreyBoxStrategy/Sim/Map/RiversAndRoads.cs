using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>A river, from its source to its mouth, along cell corners (the outlines' units).</summary>
    public sealed class RiverLine
    {
        public string Id;
        /// <summary>1 a stream, 2 a river, 3 a great river: what the screen draws as its width.</summary>
        public int Rank;
        /// <summary>Grid corners <c>[x0, y0, x1, y1, ...]</c>, source first, straight runs merged.</summary>
        public int[] Line;
        /// <summary><c>sea</c>, <c>water</c> (an inland sea or lake), or <c>river:&lt;id&gt;</c> for a tributary.</summary>
        public string Mouth;
        /// <summary>The provinces it touches, source to mouth.</summary>
        public List<string> Provinces = new List<string>();
        /// <summary>Province pairs, each <c>[a, b]</c>, whose shared border it runs along.</summary>
        public List<string[]> Borders = new List<string[]>();
    }

    /// <summary>Where a road crosses a river on a province border.</summary>
    public sealed class Bridge
    {
        public string River;
        public string A;
        public string B;
        /// <summary>The grid corner at the middle of the river's run along that border.</summary>
        public int[] At;
    }

    /// <summary>A road through a chain of neighbouring provinces.</summary>
    public sealed class RoadLine
    {
        public const string Highway = "highway";
        public const string Track = "track";

        public string Id;
        /// <summary><c>highway</c> (joining the capitals) or <c>track</c>.</summary>
        public string Kind;
        /// <summary>The provinces in order; each step is a border the rules know (ridge cuts never, passes yes).</summary>
        public List<string> Provinces = new List<string>();
        /// <summary>The provinces' centres in order, <c>[x0, y0, ...]</c>, in <see cref="ProvinceShape.Centre"/> units.</summary>
        public int[] Line;
        public List<Bridge> Bridges = new List<Bridge>();
    }

    /// <summary>
    /// How developed a province looks, 0 to 4: the one number the screen reads to draw farms and
    /// buildings. Today it comes from population, capital and port; slice R6's buildings add to it.
    /// </summary>
    public static class Development
    {
        public const int Max = 4;

        public static int Of(int population, bool capital, bool port, int buildingLevels = 0) =>
            Math.Max(0, Math.Min(Max, (population - 1) / 2 + (capital ? 1 : 0) + (port ? 1 : 0) + buildingLevels));
    }

    /// <summary>
    /// Rivers and roads for a generated world. No rule reads them yet (see the rules page, R2).
    /// Deterministic: integer heights and fixed orders throughout.
    /// </summary>
    internal static class RiversAndRoads
    {
        // --- Rivers --------------------------------------------------------------------------

        /// <summary>
        /// Rivers from high land to water. Every land corner first learns where its water goes by a
        /// priority flood from the coasts (so no river ever ends in a pit), then rivers are traced down
        /// from the highest well-spaced sources; one reaching an earlier river joins it.
        /// </summary>
        public static List<RiverLine> Rivers(int seed, int[] cells, int w, int h, int[] height, int wanted, double meanArea, Func<int, string> id)
        {
            int cw = w + 1, ch = h + 1;
            int Cell(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? MapGeometry.Sea : cells[y * w + x];
            var cornerHeight = new long[cw * ch];
            var land = new bool[cw * ch];
            var mouth = new string[cw * ch];
            for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
            {
                int c = y * cw + x;
                int a = Cell(x - 1, y - 1), b = Cell(x, y - 1), d = Cell(x - 1, y), e = Cell(x, y);
                int lands = 0;
                long sum = 0;
                foreach (var (v, cx, cy) in new[] { (a, x - 1, y - 1), (b, x, y - 1), (d, x - 1, y), (e, x, y) })
                    if (v >= 0) { lands++; sum += height[cy * w + cx]; }
                bool sea = a == MapGeometry.Sea || b == MapGeometry.Sea || d == MapGeometry.Sea || e == MapGeometry.Sea;
                bool water = a == MapGeometry.Water || b == MapGeometry.Water || d == MapGeometry.Water || e == MapGeometry.Water;
                if (lands > 0 && (sea || water)) mouth[c] = water ? "water" : "sea";
                land[c] = lands == 4;
                // Fine noise on the height makes the water wander instead of running straight down a smooth slope.
                cornerHeight[c] = lands == 0 ? 0 : sum / lands + (MapRandom.Fractal(seed, "river-wander", x, y, 6, 2) - 32768) / 6;
            }

            // Priority flood: from every mouth corner, reach land corners lowest first; each remembers the
            // corner it was reached from, which is where its water flows.
            var down = Enumerable.Repeat(-1, cw * ch).ToArray();
            var level = new long[cw * ch];
            var done = new bool[cw * ch];
            var queue = new SortedSet<(long level, int c)>();
            for (int c = 0; c < cw * ch; c++)
                if (mouth[c] != null) { level[c] = cornerHeight[c]; queue.Add((level[c], c)); done[c] = true; }
            while (queue.Count > 0)
            {
                var top = queue.Min;
                queue.Remove(top);
                int c = top.c, x = c % cw, y = c / cw;
                foreach (int n in new[] { y > 0 ? c - cw : -1, x + 1 < cw ? c + 1 : -1, y + 1 < ch ? c + cw : -1, x > 0 ? c - 1 : -1 })
                {
                    if (n < 0 || done[n] || !land[n]) continue;
                    done[n] = true;
                    down[n] = c;
                    level[n] = Math.Max(cornerHeight[n], top.level);
                    queue.Add((level[n], n));
                }
            }

            int spacing = Math.Max(4, (int)(Math.Sqrt(meanArea) * 2.2));
            int minLength = Math.Max(6, (int)(Math.Sqrt(meanArea) * 1.1));
            var onRiver = new Dictionary<int, int>(); // corner -> river index
            var sources = new List<int>();
            var rivers = new List<(List<int> path, string mouth)>();
            var candidates = Enumerable.Range(0, cw * ch).Where(c => land[c] && down[c] >= 0)
                .OrderByDescending(c => cornerHeight[c]).ThenBy(c => c).ToList();
            foreach (int s in candidates)
            {
                if (rivers.Count >= wanted) break;
                int sx = s % cw, sy = s / cw;
                if (sources.Any(o => Math.Abs(o % cw - sx) + Math.Abs(o / cw - sy) < spacing)) continue;
                if (onRiver.ContainsKey(s)) continue;
                var path = new List<int> { s };
                int at = s;
                string end = null;
                for (int guard = 0; guard < cw * ch; guard++)
                {
                    if (mouth[at] != null) { end = mouth[at]; break; }
                    if (onRiver.TryGetValue(at, out int joins) && at != s) { end = "river:" + joins; break; }
                    at = down[at];
                    if (at < 0) break;
                    path.Add(at);
                }
                if (end == null || path.Count < minLength) continue;
                sources.Add(s);
                foreach (int c in path)
                    if (!onRiver.ContainsKey(c)) onRiver[c] = rivers.Count;
                rivers.Add((path, end));
            }

            var result = new List<RiverLine>();
            var joined = new int[rivers.Count];
            foreach (var r in rivers)
                if (r.mouth.StartsWith("river:", StringComparison.Ordinal)) joined[int.Parse(r.mouth.Substring(6))]++;
            for (int i = 0; i < rivers.Count; i++)
            {
                var (path, end) = rivers[i];
                var line = new List<int>();
                foreach (int c in path) { line.Add(c % cw); line.Add(c / cw); }
                var river = new RiverLine
                {
                    Id = "river" + (i + 1),
                    Rank = Math.Min(3, 1 + (path.Count >= minLength * 2 ? 1 : 0) + (joined[i] > 0 ? 1 : 0)),
                    Line = MapGeometry.SimplifyOpen(line),
                    Mouth = end.StartsWith("river:", StringComparison.Ordinal) ? "river:river" + (int.Parse(end.Substring(6)) + 1) : end,
                };
                var seenProvince = new HashSet<int>();
                var seenBorder = new HashSet<long>();
                for (int k = 0; k + 1 < path.Count; k++)
                {
                    var (p, q) = Beside(path[k], path[k + 1], cw, Cell);
                    foreach (int v in new[] { p, q })
                        if (v >= 0 && seenProvince.Add(v)) river.Provinces.Add(id(v));
                    if (p >= 0 && q >= 0 && p != q && seenBorder.Add(MapChecks.Key(p, q)))
                        river.Borders.Add(new[] { id(Math.Min(p, q)), id(Math.Max(p, q)) });
                }
                result.Add(river);
            }
            return result;
        }

        /// <summary>The two cells either side of a corner-to-corner edge.</summary>
        internal static (int, int) Beside(int c1, int c2, int cw, Func<int, int, int> cell)
        {
            int x1 = c1 % cw, y1 = c1 / cw, x2 = c2 % cw, y2 = c2 / cw;
            if (y1 == y2)
            {
                int x = Math.Min(x1, x2);
                return (cell(x, y1 - 1), cell(x, y1));
            }
            int y = Math.Min(y1, y2);
            return (cell(x1 - 1, y), cell(x1, y));
        }

        // --- Roads ---------------------------------------------------------------------------

        /// <summary>
        /// Roads: highways along the cheapest paths joining every capital (a spanning tree), then
        /// tracks joining each port and populous province to the nearest road. A path prefers plains,
        /// crosses ridges only at passes (cut borders are not in <paramref name="adj"/>), and pays to
        /// cross a river; each crossing gets a bridge.
        /// </summary>
        public static List<RoadLine> Roads(List<int>[] adj, Terrain[] terrain, bool[] capital, bool[] port, int[] population,
            int[] cx, int[] cy, List<RiverLine> rivers, int[] cells, int w, Func<int, string> id)
        {
            int n = adj.Length;
            int[] index = Enumerable.Range(0, n).ToArray();
            var riverOf = new Dictionary<long, (RiverLine river, int[] at)>();
            int cw = w + 1;
            int Cell(int x, int y) => x < 0 || y < 0 || x >= w || y >= cells.Length / w ? MapGeometry.Sea : cells[y * w + x];
            foreach (var r in rivers)
            {
                // Expand the river's corners back to unit steps, to find the middle of each border it forms.
                var steps = new List<(int, int)>();
                for (int k = 0; k + 3 < r.Line.Length; k += 2)
                {
                    int x0 = r.Line[k], y0 = r.Line[k + 1], x1 = r.Line[k + 2], y1 = r.Line[k + 3];
                    int dx = Math.Sign(x1 - x0), dy = Math.Sign(y1 - y0);
                    for (int x = x0, y = y0; x != x1 || y != y1; x += dx, y += dy)
                        steps.Add((y * cw + x, (y + dy) * cw + x + dx));
                }
                var byPair = new Dictionary<long, List<int>>();
                foreach (var (a, b) in steps)
                {
                    var (p, q) = Beside(a, b, cw, Cell);
                    if (p < 0 || q < 0 || p == q) continue;
                    long key = MapChecks.Key(p, q);
                    if (!byPair.TryGetValue(key, out var list)) byPair[key] = list = new List<int>();
                    list.Add(a);
                }
                foreach (var kv in byPair)
                    if (!riverOf.ContainsKey(kv.Key))
                    {
                        int mid = kv.Value[kv.Value.Count / 2];
                        riverOf[kv.Key] = (r, new[] { mid % cw, mid / cw });
                    }
            }

            int Cost(int t) => t == (int)Terrain.Plains ? 2 : t == (int)Terrain.Forest ? 3 : t == (int)Terrain.Hills ? 5 : t == (int)Terrain.Marsh ? 6 : 8;
            int Step(int a, int b) => Cost((int)terrain[a]) + Cost((int)terrain[b]) + (riverOf.ContainsKey(MapChecks.Key(a, b)) ? 8 : 0);

            // Cheapest paths from a set of starts: distances and the province each was reached from.
            (long[] dist, int[] prev) Dijkstra(IEnumerable<int> starts)
            {
                var dist = Enumerable.Repeat(long.MaxValue, n).ToArray();
                var prev = Enumerable.Repeat(-1, n).ToArray();
                var q = new SortedSet<(long, int)>();
                foreach (int s in starts) { dist[s] = 0; q.Add((0, s)); }
                while (q.Count > 0)
                {
                    var (d, a) = q.Min;
                    q.Remove(q.Min);
                    foreach (int b in adj[a])
                    {
                        long nd = d + Step(a, b);
                        if (nd < dist[b] || (nd == dist[b] && a < prev[b]))
                        {
                            if (dist[b] != long.MaxValue) q.Remove((dist[b], b));
                            dist[b] = nd;
                            prev[b] = a;
                            q.Add((nd, b));
                        }
                    }
                }
                return (dist, prev);
            }

            List<int> PathTo(int[] prev, int to)
            {
                var path = new List<int>();
                for (int at = to; at >= 0; at = prev[at]) path.Add(at);
                path.Reverse();
                return path;
            }

            var roads = new List<RoadLine>();
            var onRoad = new HashSet<int>();
            var paved = new HashSet<long>();
            void Add(List<int> path, string kind)
            {
                // Only the part not already paved becomes a new road.
                var fresh = new List<List<int>>();
                var run = new List<int>();
                for (int k = 0; k + 1 < path.Count; k++)
                {
                    long key = MapChecks.Key(path[k], path[k + 1]);
                    if (paved.Contains(key))
                    {
                        if (run.Count > 1) fresh.Add(run);
                        run = new List<int>();
                        continue;
                    }
                    if (run.Count == 0) run.Add(path[k]);
                    run.Add(path[k + 1]);
                    paved.Add(key);
                }
                if (run.Count > 1) fresh.Add(run);
                foreach (var leg in fresh)
                {
                    var road = new RoadLine { Id = "road" + (roads.Count + 1), Kind = kind };
                    var line = new List<int>();
                    foreach (int p in leg) { road.Provinces.Add(id(p)); line.Add(cx[p]); line.Add(cy[p]); }
                    road.Line = line.ToArray();
                    for (int k = 0; k + 1 < leg.Count; k++)
                        if (riverOf.TryGetValue(MapChecks.Key(leg[k], leg[k + 1]), out var crossing))
                            road.Bridges.Add(new Bridge { River = crossing.river.Id, A = id(leg[k]), B = id(leg[k + 1]), At = crossing.at });
                    roads.Add(road);
                }
                foreach (int p in path) onRoad.Add(p);
            }

            // Highways: a spanning tree over the capitals, each edge the cheapest path (Prim, from the first capital).
            var caps = index.Where(i => capital[i]).ToList();
            if (caps.Count > 0)
            {
                var joinedCaps = new List<int> { caps[0] };
                onRoad.Add(caps[0]);
                while (joinedCaps.Count < caps.Count)
                {
                    var (dist, prev) = Dijkstra(onRoad.OrderBy(i => i));
                    int next = caps.Where(c => !joinedCaps.Contains(c) && dist[c] != long.MaxValue)
                        .OrderBy(c => dist[c]).ThenBy(c => c).DefaultIfEmpty(-1).First();
                    if (next < 0) break;
                    Add(PathTo(prev, next), RoadLine.Highway);
                    joinedCaps.Add(next);
                }
            }

            // Tracks: ports and the most populous provinces, each to the nearest road.
            int populous = population.OrderByDescending(p => p).ElementAt(Math.Min(n - 1, n / 4));
            foreach (int p in index.Where(i => !onRoad.Contains(i) && (port[i] || population[i] >= populous)))
            {
                if (onRoad.Contains(p)) continue;
                var (dist, prev) = Dijkstra(onRoad.OrderBy(i => i));
                if (dist[p] == long.MaxValue) continue;
                Add(PathTo(prev, p), RoadLine.Track);
            }
            return roads;
        }
    }
}
