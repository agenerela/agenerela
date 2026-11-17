using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace GreyBoxStrategy
{
    /// <summary>One province's shape, in grid units (a cell is 1 × 1, the origin at the top left).</summary>
    public sealed class ProvinceShape
    {
        public string Id;
        /// <summary>Cells it covers.</summary>
        public int Area;
        /// <summary>The mean of its cells' centres, rounded to a cell: where a piece or a marker stands.</summary>
        public int[] Centre;
        /// <summary>
        /// The cell farthest from its border (the pole of inaccessibility on the grid): where its
        /// name goes (v2 section 2.6).
        /// </summary>
        public int[] Label;
        /// <summary>How many cells from <see cref="Label"/> to the nearest border: the largest label that fits.</summary>
        public int LabelRadius;
        /// <summary>
        /// Its outline as closed loops of grid corners, each <c>[x0, y0, x1, y1, ...]</c>, the first
        /// loop the outer one, clockwise on screen. Straight runs are merged, so a corner of the
        /// staircase is a point. A province has no holes (inland water is a province's neighbour,
        /// never inside it).
        /// </summary>
        public List<int[]> Outline = new List<int[]>();
    }

    /// <summary>A border between two provinces, or a stretch of coast, as polylines of grid corners.</summary>
    public sealed class BorderLine
    {
        public const string Land = "land";
        /// <summary>A border a ridge cuts: the provinces touch on the map but are not neighbours in the rules.</summary>
        public const string Ridge = "ridge";
        /// <summary>A border a ridge crosses at a pass: neighbours in the rules.</summary>
        public const string Pass = "pass";
        /// <summary>Land against the sea; <see cref="B"/> is null.</summary>
        public const string Coast = "coast";
        /// <summary>Land against inland water; <see cref="B"/> is null.</summary>
        public const string Shore = "shore";

        public string A;
        public string B;
        public string Kind;
        /// <summary>Cell edges along it: a border's length.</summary>
        public int Length;
        /// <summary>Polylines, each <c>[x0, y0, x1, y1, ...]</c>.</summary>
        public List<int[]> Lines = new List<int[]>();
    }

    /// <summary>A mountain ridge drawn across the land, from one end to the other, with the borders it cuts.</summary>
    public sealed class RidgeLine
    {
        /// <summary>The ridge's line, <c>[x0, y0, x1, y1]</c>, in grid units.</summary>
        public int[] Line;
        /// <summary>Borders it crosses that stay open, each <c>[a, b]</c>.</summary>
        public List<string[]> Passes = new List<string[]>();
        /// <summary>Borders it cuts, each <c>[a, b]</c>.</summary>
        public List<string[]> Cuts = new List<string[]>();
    }

    /// <summary>
    /// A generated world's geometry, as plain data a screen rasterises at any size (v2 section 2.6):
    /// the cell grid it was made on, each province's outline, label point and centre, every border and
    /// coast as polylines, and the ridges. The rules never read it; the adjacency the rules use is
    /// derived from these same cells, so the drawing and the rules cannot disagree.
    /// </summary>
    public sealed class MapGeometry
    {
        public const int Sea = -1;
        public const int Water = -2;
        /// <summary>A small island off the coast: scenery to draw, never a province.</summary>
        public const int Island = -4;

        /// <summary>The grid's size in cells.</summary>
        public int Width;
        public int Height;
        /// <summary>
        /// The grid, row by row from the top, run-length coded: <c>"v*n v*n ..."</c>, where v is the
        /// province's index in <see cref="WorldMap.Provinces"/>, -1 the sea, -2 inland water and -4 a scenery island. Decode
        /// with <see cref="DecodeCells"/>.
        /// </summary>
        public string Cells;
        /// <summary>In the order of <see cref="WorldMap.Provinces"/>.</summary>
        public List<ProvinceShape> Provinces = new List<ProvinceShape>();
        public List<BorderLine> Borders = new List<BorderLine>();
        public List<RidgeLine> Ridges = new List<RidgeLine>();
        /// <summary>Scenery islands' outlines, closed polylines of grid corners: land to draw, never played.</summary>
        public List<int[]> Islands = new List<int[]>();
        /// <summary>Rivers, source to mouth, along grid corners. No rule reads them yet.</summary>
        public List<RiverLine> Rivers = new List<RiverLine>();
        /// <summary>Roads through chains of neighbouring provinces, with their bridges. No rule reads them yet.</summary>
        public List<RoadLine> Roads = new List<RoadLine>();

        public static string EncodeCells(int[] cells)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < cells.Length)
            {
                int v = cells[i], n = 1;
                while (i + n < cells.Length && cells[i + n] == v) n++;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(v.ToString(CultureInfo.InvariantCulture)).Append('*').Append(n.ToString(CultureInfo.InvariantCulture));
                i += n;
            }
            return sb.ToString();
        }

        public int[] DecodeCells()
        {
            var cells = new int[Width * Height];
            int i = 0;
            foreach (string run in Cells.Split(' '))
            {
                int star = run.IndexOf('*');
                int v = int.Parse(run.Substring(0, star), CultureInfo.InvariantCulture);
                int n = int.Parse(run.Substring(star + 1), CultureInfo.InvariantCulture);
                for (int k = 0; k < n; k++) cells[i++] = v;
            }
            if (i != cells.Length) throw new FormatException($"The cell grid has {i} cells, not {cells.Length}.");
            return cells;
        }

        // ------------------------------------------------------------------------------------
        // Building it from a grid
        // ------------------------------------------------------------------------------------

        /// <summary>The geometry of a grid of province indices (<see cref="Sea"/>, <see cref="Water"/> for water).</summary>
        public static MapGeometry Build(int[] cells, int width, int height, IReadOnlyList<string> ids)
        {
            var g = new MapGeometry { Width = width, Height = height, Cells = EncodeCells(cells) };
            int n = ids.Count;
            var area = new int[n];
            var sumX = new long[n];
            var sumY = new long[n];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int v = cells[y * width + x];
                if (v < 0) continue;
                area[v]++;
                sumX[v] += x;
                sumY[v] += y;
            }

            var dist = DistanceToBorder(cells, width, height);
            var label = new int[n];
            var best = new int[n];
            for (int i = 0; i < n; i++) label[i] = -1;
            for (int c = 0; c < cells.Length; c++)
            {
                int v = cells[c];
                if (v < 0) continue;
                if (dist[c] > best[v]) { best[v] = dist[c]; label[v] = c; }
            }

            var outlines = Outlines(cells, width, height, n);
            for (int i = 0; i < n; i++)
            {
                int cx = area[i] == 0 ? 0 : (int)((sumX[i] * 2 + area[i]) / (2 * area[i]));
                int cy = area[i] == 0 ? 0 : (int)((sumY[i] * 2 + area[i]) / (2 * area[i]));
                g.Provinces.Add(new ProvinceShape
                {
                    Id = ids[i],
                    Area = area[i],
                    Centre = new[] { cx, cy },
                    Label = label[i] < 0 ? new[] { cx, cy } : new[] { label[i] % width, label[i] / width },
                    LabelRadius = best[i],
                    Outline = outlines[i],
                });
            }

            var islandEdges = new List<(int x0, int y0, int x1, int y1)>();
            int Cell(int ax, int ay) => ax < 0 || ay < 0 || ax >= width || ay >= height ? Sea : cells[ay * width + ax];
            for (int y = 0; y <= height; y++)
            for (int x = 0; x <= width; x++)
            {
                if (x < width && (Cell(x, y - 1) == Island) != (Cell(x, y) == Island)) islandEdges.Add((x, y, x + 1, y));
                if (y < height && (Cell(x - 1, y) == Island) != (Cell(x, y) == Island)) islandEdges.Add((x, y, x, y + 1));
            }
            if (islandEdges.Count > 0) g.Islands = Chain(islandEdges, width + 1);

            foreach (var line in EdgeLines(cells, width, height))
            {
                bool water = line.b < 0;
                g.Borders.Add(new BorderLine
                {
                    A = ids[line.a],
                    B = water ? null : ids[line.b],
                    Kind = water ? (line.b == Water ? BorderLine.Shore : BorderLine.Coast) : BorderLine.Land,
                    Length = line.length,
                    Lines = line.lines,
                });
            }
            return g;
        }

        /// <summary>
        /// Per cell, its distance in cells (4-neighbour steps) to the nearest cell of another province
        /// or water; 0 for water. A cell on the border is 1.
        /// </summary>
        public static int[] DistanceToBorder(int[] cells, int width, int height)
        {
            var d = new int[cells.Length];
            var queue = new int[cells.Length];
            int head = 0, tail = 0;
            for (int c = 0; c < cells.Length; c++)
            {
                int v = cells[c];
                if (v < 0) continue;
                int x = c % width, y = c / width;
                bool edge = x == 0 || y == 0 || x == width - 1 || y == height - 1
                    || cells[c - 1] != v || cells[c + 1] != v || cells[c - width] != v || cells[c + width] != v;
                if (edge) { d[c] = 1; queue[tail++] = c; }
            }
            while (head < tail)
            {
                int c = queue[head++];
                int x = c % width, y = c / width;
                int v = cells[c];
                void Try(int o)
                {
                    if (cells[o] == v && d[o] == 0) { d[o] = d[c] + 1; queue[tail++] = o; }
                }
                if (x > 0) Try(c - 1);
                if (x < width - 1) Try(c + 1);
                if (y > 0) Try(c - width);
                if (y < height - 1) Try(c + width);
            }
            return d;
        }

        // A directed boundary edge from corner (x0, y0) to (x1, y1) with the province on its right
        // as drawn on screen (y down), so outer loops run clockwise.
        private static List<int[]>[] Outlines(int[] cells, int width, int height, int n)
        {
            int At(int x, int y) => x < 0 || y < 0 || x >= width || y >= height ? Sea : cells[y * width + x];
            int stride = width + 1;
            var next = new Dictionary<long, List<int>>[n];
            for (int i = 0; i < n; i++) next[i] = new Dictionary<long, List<int>>();
            void Add(int v, int x0, int y0, int x1, int y1)
            {
                long from = (long)y0 * stride + x0;
                if (!next[v].TryGetValue(from, out var list)) next[v][from] = list = new List<int>(1);
                list.Add(y1 * stride + x1);
            }
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int v = cells[y * width + x];
                if (v < 0) continue;
                if (At(x, y - 1) != v) Add(v, x, y, x + 1, y);             // top, left to right
                if (At(x + 1, y) != v) Add(v, x + 1, y, x + 1, y + 1);     // right, downwards
                if (At(x, y + 1) != v) Add(v, x + 1, y + 1, x, y + 1);     // bottom, right to left
                if (At(x - 1, y) != v) Add(v, x, y + 1, x, y);             // left, upwards
            }

            var result = new List<int[]>[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = new List<int[]>();
                var edges = next[i];
                // Start each loop at its lowest corner index, so the order never depends on a dictionary.
                var starts = edges.Keys.OrderBy(k => k).ToList();
                foreach (long s in starts)
                {
                    while (edges.TryGetValue(s, out var outs) && outs.Count > 0)
                    {
                        var loop = new List<int>();
                        long at = s;
                        int prevDx = 0, prevDy = 0;
                        int guard = 4 * width * height + 8;
                        while (guard-- > 0)
                        {
                            if (!edges.TryGetValue(at, out var options) || options.Count == 0) break;
                            int pick = 0;
                            if (options.Count > 1)
                            {
                                // At a corner the province touches itself diagonally: turn right first,
                                // which keeps the loop tight around the cells it came along.
                                int bestScore = int.MaxValue;
                                for (int k = 0; k < options.Count; k++)
                                {
                                    int dx = (int)(options[k] % stride - at % stride), dy = (int)(options[k] / stride - at / stride);
                                    int turn = prevDx * dy - prevDy * dx; // > 0 is a right turn with y down
                                    int score = turn > 0 ? 0 : turn == 0 ? 1 : 2;
                                    if (score < bestScore) { bestScore = score; pick = k; }
                                }
                            }
                            int to = options[pick];
                            options.RemoveAt(pick);
                            int ax = (int)(at % stride), ay = (int)(at / stride);
                            loop.Add(ax);
                            loop.Add(ay);
                            prevDx = to % stride - ax;
                            prevDy = to / stride - ay;
                            at = to;
                            if (at == s) break;
                        }
                        result[i].Add(Simplify(loop, closed: true));
                    }
                }
                // The outer loop first: the one with the largest area.
                result[i] = result[i].OrderByDescending(l => Math.Abs(SignedArea(l))).ToList();
            }
            return result;
        }

        private static long SignedArea(int[] l)
        {
            long a = 0;
            for (int k = 0; k < l.Length; k += 2)
            {
                int j = (k + 2) % l.Length;
                a += (long)l[k] * l[j + 1] - (long)l[j] * l[k + 1];
            }
            return a;
        }

        /// <summary>An open polyline <c>[x0, y0, ...]</c> with the corners in the middle of straight runs dropped.</summary>
        internal static int[] SimplifyOpen(List<int> pts) => Simplify(pts, closed: false);

        // Drops corners in the middle of a straight run.
        private static int[] Simplify(List<int> pts, bool closed)
        {
            int count = pts.Count / 2;
            if (count < 3) return pts.ToArray();
            var keep = new List<int>();
            for (int k = 0; k < count; k++)
            {
                bool ends = !closed && (k == 0 || k == count - 1);
                int p = (k - 1 + count) % count, q = (k + 1) % count;
                int dx1 = pts[2 * k] - pts[2 * p], dy1 = pts[2 * k + 1] - pts[2 * p + 1];
                int dx2 = pts[2 * q] - pts[2 * k], dy2 = pts[2 * q + 1] - pts[2 * k + 1];
                bool straight = dx1 * dy2 - dy1 * dx2 == 0 && dx1 * dx2 + dy1 * dy2 > 0;
                if (ends || !straight)
                {
                    keep.Add(pts[2 * k]);
                    keep.Add(pts[2 * k + 1]);
                }
            }
            return keep.ToArray();
        }

        // Every stretch where province a meets province b (b > a) or water (b < 0), chained into polylines.
        private static IEnumerable<(int a, int b, int length, List<int[]> lines)> EdgeLines(int[] cells, int width, int height)
        {
            int At(int x, int y) => x < 0 || y < 0 || x >= width || y >= height ? Sea : cells[y * width + x];
            var groups = new Dictionary<long, List<(int x0, int y0, int x1, int y1)>>();
            void Add(int a, int b, int x0, int y0, int x1, int y1)
            {
                if (a < 0 && b < 0) return;
                if (a < 0 || (b >= 0 && b < a)) { int t = a; a = b; b = t; }
                long key = ((long)a << 32) | (uint)(b + 2);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<(int, int, int, int)>();
                list.Add((x0, y0, x1, y1));
            }
            for (int y = 0; y <= height; y++)
            for (int x = 0; x <= width; x++)
            {
                if (x < width)
                {
                    int up = At(x, y - 1), down = At(x, y);
                    if (up != down) Add(up, down, x, y, x + 1, y);
                }
                if (y < height)
                {
                    int left = At(x - 1, y), right = At(x, y);
                    if (left != right) Add(left, right, x, y, x, y + 1);
                }
            }
            foreach (var kv in groups.OrderBy(k => k.Key))
            {
                int a = (int)(kv.Key >> 32);
                int b = (int)(kv.Key & 0xFFFFFFFF) - 2;
                yield return (a, b, kv.Value.Count, Chain(kv.Value, width + 1));
            }
        }

        // Chains unit edges into polylines, starting from ends (corners with one edge) so open lines
        // come out whole; what is left are closed loops.
        private static List<int[]> Chain(List<(int x0, int y0, int x1, int y1)> edges, int stride)
        {
            var at = new Dictionary<int, List<int>>();
            for (int e = 0; e < edges.Count; e++)
            {
                int p = edges[e].y0 * stride + edges[e].x0, q = edges[e].y1 * stride + edges[e].x1;
                if (!at.TryGetValue(p, out var lp)) at[p] = lp = new List<int>();
                if (!at.TryGetValue(q, out var lq)) at[q] = lq = new List<int>();
                lp.Add(e);
                lq.Add(e);
            }
            var used = new bool[edges.Count];
            var lines = new List<int[]>();
            var order = at.Keys.OrderBy(k => at[k].Count == 1 ? 0 : 1).ThenBy(k => k).ToList();
            foreach (int start in order)
            {
                foreach (int first in at[start].ToList())
                {
                    if (used[first]) continue;
                    var pts = new List<int> { start % stride, start / stride };
                    int corner = start, e = first;
                    while (e >= 0)
                    {
                        used[e] = true;
                        int p = edges[e].y0 * stride + edges[e].x0, q = edges[e].y1 * stride + edges[e].x1;
                        corner = corner == p ? q : p;
                        pts.Add(corner % stride);
                        pts.Add(corner / stride);
                        e = -1;
                        foreach (int f in at[corner])
                            if (!used[f]) { e = f; break; }
                        if (corner == start) break;
                    }
                    lines.Add(Simplify(pts, closed: false));
                }
            }
            return lines;
        }
    }
}
