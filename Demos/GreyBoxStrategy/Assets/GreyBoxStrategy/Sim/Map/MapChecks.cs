using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>What kind of thing a failed check says about a world.</summary>
    public enum ProblemKind
    {
        /// <summary>The rules graph itself is wrong. A bug in the generator, never a matter of luck.</summary>
        Connectivity,
        /// <summary>A province's shape: too big, too small, too thin, in pieces, or touching another only at a corner.</summary>
        Shape,
        /// <summary>The starts are not fair (v2 section 2.4).</summary>
        Fairness,
        /// <summary>Names and special places.</summary>
        Content,
    }

    /// <summary>One failed check, in words.</summary>
    public sealed class MapProblem
    {
        public MapProblem(ProblemKind kind, string check, string message)
        {
            Kind = kind;
            Check = check;
            Message = message;
        }

        public ProblemKind Kind { get; }
        /// <summary>The check's id: <c>connected</c>, <c>reach</c>, <c>corner-contact</c>...</summary>
        public string Check { get; }
        public string Message { get; }

        public override string ToString() => $"{Check}: {Message}";
    }

    /// <summary>One country's start, measured (v2 section 2.4).</summary>
    public sealed class CountryFairness
    {
        public string Country;
        public string Capital;
        /// <summary>
        /// Provinces nearer its capital, in steps, than any other capital. A province equally near
        /// several capitals counts as shared among them equally (a half for two), in sixtieths:
        /// see <see cref="Reach"/>.
        /// </summary>
        public int Reach60;
        /// <summary>Food + gold + one per distinct good, over its reach, shares counted the same way, in sixtieths.</summary>
        public int Value60;
        public double Reach => Reach60 / 60.0;
        public double Value => Value60 / 60.0;
        /// <summary>Neutral provinces bordering its starting realm.</summary>
        public int Neutral;
        /// <summary>Other capitals within six steps.</summary>
        public int Rivals;
        /// <summary>Hills, mountains and marsh on its reach's edge, and ridge cuts there. Reported, not checked.</summary>
        public int Defensible;
    }

    /// <summary>
    /// The checks a world must pass (v2 section 2.4), as code: each either passes or says why not.
    /// They read only the map (its rules graph, and its geometry when it has one), so they judge any
    /// world, generated or written by hand, and a test can break a world and see the right check fail.
    /// </summary>
    public static class MapChecks
    {
        // Fairness bounds, every one *to tune* (v2 section 2.4).
        public const int ReachSpreadTenths = 13;     // best : worst at most 1.3 : 1
        public const int ValueSpreadPercent = 125;   // at most 1.25 : 1
        public const int NeutralSpread = 2;          // at most 2 : 1, and never 0
        /// <summary>
        /// How far apart the counts of rival capitals within <see cref="RivalSteps"/> may be: 2, as the
        /// page asks, up to six countries; then half the countries less one (3 for eight, 4 for ten).
        /// Measured on 12 November (R2): with eight or ten countries some realm must sit inland, with
        /// six or seven rivals near against three on the coast, and no placement passed 2.
        /// </summary>
        public static int RivalSpread(int countries) => countries <= 6 ? 2 : countries / 2 - 1;
        public const int RivalSteps = 6;
        public const int SpecialSpacing = 4;

        /// <summary>
        /// The steps capitals must stand apart: 4 when each country has 20 or more provinces to itself,
        /// otherwise 3. The page asked for 4 on medium and up. Measured on 12 November (R2): a reach of
        /// 10 to 15 provinces puts neighbouring capitals about 3.5 to 4.5 steps apart, so 4 made the
        /// capitals of a compact world pack to its edges and failed the reach check on nearly every try.
        /// </summary>
        public static int CapitalSpacing(int provinces, int countries) => provinces >= 20 * countries ? 4 : 3;

        /// <summary>Every check. An empty list means the world passes.</summary>
        public static List<MapProblem> Check(WorldMap map)
        {
            var problems = new List<MapProblem>();
            string invalid = map.WhyInvalid();
            if (invalid != null)
            {
                problems.Add(new MapProblem(ProblemKind.Connectivity, "valid", invalid));
                return problems;
            }
            var g = new Graph(map);
            CheckConnectivity(map, g, problems);
            if (map.Geometry != null) CheckGeometry(map, g, problems);
            CheckStarts(map, g, problems);
            CheckFairness(map, g, problems);
            CheckNames(map, g, problems);
            CheckSpecials(map, g, problems);
            return problems;
        }

        /// <summary>The fairness measures of every country, in seat order.</summary>
        public static List<CountryFairness> Fairness(WorldMap map) => Measure(map, new Graph(map));

        // ------------------------------------------------------------------------------------

        private static void CheckConnectivity(WorldMap map, Graph g, List<MapProblem> problems)
        {
            var dist = g.Distances(0);
            int unreached = dist.Count(d => d < 0);
            if (unreached > 0)
                problems.Add(new MapProblem(ProblemKind.Connectivity, "connected",
                    $"{unreached} provinces cannot be reached from {map.Provinces[0].Id}, e.g. {map.Provinces[Array.FindIndex(dist, d => d < 0)].Id}"));

            var capitals = map.Provinces.Where(p => p.Capital).Select(p => g.Index[p.Id]).ToList();
            foreach (int c in capitals)
            {
                var d = g.Distances(c);
                foreach (int o in capitals)
                    if (d[o] < 0)
                    {
                        problems.Add(new MapProblem(ProblemKind.Connectivity, "capitals-reach",
                            $"capital {map.Provinces[c].Id} cannot reach capital {map.Provinces[o].Id}"));
                        break;
                    }
            }

            var lonely = map.Provinces.Where((p, i) => g.Adj[i].Count < 2).Select(p => p.Id).ToList();
            int allowed = map.Provinces.Count / 20;
            if (lonely.Count > allowed)
                problems.Add(new MapProblem(ProblemKind.Connectivity, "degree",
                    $"{lonely.Count} provinces have fewer than 2 borders, more than {allowed} allowed: {string.Join(", ", lonely.Take(5))}"));
            var none = map.Provinces.Where((p, i) => g.Adj[i].Count == 0).Select(p => p.Id).ToList();
            if (none.Count > 0)
                problems.Add(new MapProblem(ProblemKind.Connectivity, "isolated", $"provinces without a neighbour: {string.Join(", ", none)}"));
        }

        private static void CheckGeometry(WorldMap map, Graph g, List<MapProblem> problems)
        {
            var geo = map.Geometry;
            int[] cells;
            try { cells = geo.DecodeCells(); }
            catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException)
            {
                problems.Add(new MapProblem(ProblemKind.Connectivity, "cells", e.Message));
                return;
            }
            int n = map.Provinces.Count, w = geo.Width, h = geo.Height;
            if (cells.Any(v => v >= n || v < MapGeometry.Island || v == -3))
            {
                problems.Add(new MapProblem(ProblemKind.Connectivity, "cells", "a cell names a province that does not exist"));
                return;
            }

            var shared = SharedEdges(cells, w, h);
            var cuts = new HashSet<long>(geo.Ridges.SelectMany(r => r.Cuts).Select(c => Key(g.Index[c[0]], g.Index[c[1]])));
            var passes = new HashSet<long>(geo.Ridges.SelectMany(r => r.Passes).Select(c => Key(g.Index[c[0]], g.Index[c[1]])));

            // Adjacency is derived from the drawing: a border is three or more shared cell edges, less ridge cuts.
            foreach (var kv in shared.OrderBy(k => k.Key))
            {
                var (a, b) = Unkey(kv.Key);
                bool border = g.Has(a, b);
                if (kv.Value >= 3 && !border && !cuts.Contains(kv.Key))
                    problems.Add(new MapProblem(ProblemKind.Connectivity, "cell-adjacency",
                        $"{map.Provinces[a].Id} and {map.Provinces[b].Id} share {kv.Value} cell edges but are not neighbours, and no ridge cuts them"));
                if (kv.Value >= 1 && kv.Value < 3)
                    problems.Add(new MapProblem(ProblemKind.Shape, "corner-contact",
                        $"{map.Provinces[a].Id} and {map.Provinces[b].Id} touch along only {kv.Value} cell edges"));
            }
            for (int a = 0; a < n; a++)
                foreach (int b in g.Adj[a])
                    if (a < b && (!shared.TryGetValue(Key(a, b), out int e) || e < 3))
                        problems.Add(new MapProblem(ProblemKind.Connectivity, "cell-adjacency",
                            $"{map.Provinces[a].Id} and {map.Provinces[b].Id} are neighbours but do not share a border on the map"));
            foreach (long c in cuts)
            {
                var (a, b) = Unkey(c);
                if (g.Has(a, b))
                    problems.Add(new MapProblem(ProblemKind.Connectivity, "ridge-pass",
                        $"a ridge cuts {map.Provinces[a].Id}-{map.Provinces[b].Id}, but they are neighbours without a pass"));
            }

            // A ridge may cross a border only at a pass.
            var edges = EdgesByPair(cells, w, h);
            foreach (var ridge in geo.Ridges)
                foreach (var kv in edges)
                {
                    var (a, b) = Unkey(kv.Key);
                    if (!g.Has(a, b) || passes.Contains(kv.Key)) continue;
                    if (kv.Value.Any(e => Crosses(ridge.Line, e)))
                        problems.Add(new MapProblem(ProblemKind.Connectivity, "ridge-pass",
                            $"the border {map.Provinces[a].Id}-{map.Provinces[b].Id} crosses a ridge away from a pass"));
                }

            // A scenery island touching a province would read as part of it.
            int touching = 0;
            for (int c = 0; c < cells.Length; c++)
            {
                if (cells[c] != MapGeometry.Island) continue;
                int x = c % w, y = c / w;
                if ((x > 0 && cells[c - 1] >= 0) || (x + 1 < w && cells[c + 1] >= 0) || (y > 0 && cells[c - w] >= 0) || (y + 1 < h && cells[c + w] >= 0))
                    touching++;
            }
            if (touching > 0)
                problems.Add(new MapProblem(ProblemKind.Shape, "island", $"{touching} island cells touch a province"));

            // Corners: two provinces meeting only diagonally, across two others.
            int diagonals = 0;
            for (int y = 0; y + 1 < h; y++)
            for (int x = 0; x + 1 < w; x++)
            {
                int p = cells[y * w + x], q = cells[y * w + x + 1], r = cells[(y + 1) * w + x], s = cells[(y + 1) * w + x + 1];
                if (p < 0 || q < 0 || r < 0 || s < 0) continue;
                if ((p != q && p != r && s != q && s != r && p != s && !g.Has(p, s))
                    || (q != p && q != s && r != p && r != s && q != r && !g.Has(q, r)))
                    diagonals++;
            }
            if (diagonals > 0)
                problems.Add(new MapProblem(ProblemKind.Shape, "corner-contact", $"{diagonals} places where two provinces touch only at a corner"));

            // Shapes: one piece, area within bounds, not too thin.
            var area = new int[n];
            foreach (int v in cells) if (v >= 0) area[v]++;
            double mean = area.Average();
            var pieces = Pieces(cells, w, h, n);
            int minWidth = MinWidth(mean);
            for (int i = 0; i < n; i++)
            {
                string id = map.Provinces[i].Id;
                if (area[i] == 0) { problems.Add(new MapProblem(ProblemKind.Shape, "one-piece", $"{id} has no cells")); continue; }
                if (pieces[i] != 1) problems.Add(new MapProblem(ProblemKind.Shape, "one-piece", $"{id} is in {pieces[i]} pieces"));
                if (area[i] * 2 < mean || area[i] > mean * 1.8)
                    problems.Add(new MapProblem(ProblemKind.Shape, "area",
                        $"{id} covers {area[i]} cells, outside 0.5 to 1.8 times the mean of {mean.ToString("0", CultureInfo.InvariantCulture)}"));
            }
            var depth = MapGeometry.DistanceToBorder(cells, w, h);
            var widest = new int[n];
            for (int c = 0; c < cells.Length; c++) if (cells[c] >= 0) widest[cells[c]] = Math.Max(widest[cells[c]], depth[c]);
            for (int i = 0; i < n; i++)
                if (area[i] > 0 && widest[i] < minWidth)
                    problems.Add(new MapProblem(ProblemKind.Shape, "width", $"{map.Provinces[i].Id} is nowhere wider than {2 * widest[i] - 1} cells"));
        }

        /// <summary>The narrowest a province may be, as cells from its middle to its border.</summary>
        public static int MinWidth(double meanArea) => Math.Max(2, (int)(Math.Sqrt(meanArea) * 0.15));

        private static void CheckStarts(WorldMap map, Graph g, List<MapProblem> problems)
        {
            var sizes = map.Countries.Select(c => map.Provinces.Count(p => p.Owner == c.Id)).ToList();
            if (sizes.Distinct().Count() > 1)
                problems.Add(new MapProblem(ProblemKind.Fairness, "realms", $"starting realms differ in size: {string.Join(", ", sizes)}"));
            for (int a = 0; a < map.Provinces.Count; a++)
                foreach (int b in g.Adj[a])
                {
                    string oa = map.Provinces[a].Owner, ob = map.Provinces[b].Owner;
                    if (a < b && oa != null && ob != null && oa != ob)
                        problems.Add(new MapProblem(ProblemKind.Fairness, "realms", $"{oa} and {ob} start bordering each other ({map.Provinces[a].Id}-{map.Provinces[b].Id})"));
                }
            var caps = map.Provinces.Select((p, i) => (p, i)).Where(x => x.p.Capital).ToList();
            foreach (var c in caps)
                if (c.p.Terrain == Terrain.Mountains || c.p.Terrain == Terrain.Marsh)
                    problems.Add(new MapProblem(ProblemKind.Fairness, "capital", $"capital {c.p.Id} is on {TerrainTable.Id(c.p.Terrain)}"));
            if (map.Geometry == null) return; // the spacing rule is the generator's; a hand-made board sets its own
            int spacing = CapitalSpacing(map.Provinces.Count, map.Countries.Count);
            foreach (var c in caps)
            {
                var d = g.Distances(c.i);
                foreach (var o in caps)
                    if (o.i > c.i && d[o.i] >= 0 && d[o.i] < spacing)
                        problems.Add(new MapProblem(ProblemKind.Fairness, "capital-distance",
                            $"capitals {c.p.Id} and {o.p.Id} are {d[o.i]} steps apart, under {spacing}"));
            }
        }

        private static void CheckFairness(WorldMap map, Graph g, List<MapProblem> problems)
        {
            var m = Measure(map, g);
            if (m.Count < 2) return;
            int minReach = m.Min(x => x.Reach60), maxReach = m.Max(x => x.Reach60);
            if ((long)maxReach * 10 > (long)minReach * ReachSpreadTenths)
                problems.Add(new MapProblem(ProblemKind.Fairness, "reach", $"reach from {F(minReach)} to {F(maxReach)} provinces, over 1.3 : 1"));
            int minValue = m.Min(x => x.Value60), maxValue = m.Max(x => x.Value60);
            if ((long)maxValue * 100 > (long)minValue * ValueSpreadPercent)
                problems.Add(new MapProblem(ProblemKind.Fairness, "value", $"value of reach from {F(minValue)} to {F(maxValue)}, over 1.25 : 1"));
            int minN = m.Min(x => x.Neutral), maxN = m.Max(x => x.Neutral);
            if (minN == 0) problems.Add(new MapProblem(ProblemKind.Fairness, "neutral", $"{m.First(x => x.Neutral == 0).Country} borders no neutral province"));
            else if (maxN > minN * NeutralSpread) problems.Add(new MapProblem(ProblemKind.Fairness, "neutral", $"neutral provinces bordering a start from {minN} to {maxN}, over 2 : 1"));
            int minR = m.Min(x => x.Rivals), maxR = m.Max(x => x.Rivals);
            int rivalSpread = RivalSpread(m.Count);
            if (maxR - minR > rivalSpread)
                problems.Add(new MapProblem(ProblemKind.Fairness, "rivals", $"rival capitals within {RivalSteps} steps from {minR} to {maxR}, over {rivalSpread} apart"));

            // Scarce goods: no realm starts with more than one, and each lies somewhere in neutral land.
            foreach (var c in map.Countries)
            {
                var scarce = map.Provinces.Where(p => p.Owner == c.Id && TerrainTable.Scarce.Contains(p.Good)).Select(p => p.Good).ToList();
                if (scarce.Count > 1) problems.Add(new MapProblem(ProblemKind.Fairness, "scarce-goods", $"{c.Id} starts with {scarce.Count} scarce goods"));
            }
            if (map.Geometry != null)
                foreach (string good in TerrainTable.Scarce)
                    if (!map.Provinces.Any(p => p.Owner == null && p.Good == good))
                        problems.Add(new MapProblem(ProblemKind.Fairness, "scarce-goods", $"no neutral province has {good}"));
        }

        private static string F(int sixtieths) => (sixtieths / 60.0).ToString("0.#", CultureInfo.InvariantCulture);

        private static void CheckNames(WorldMap map, Graph g, List<MapProblem> problems)
        {
            if (map.Geometry == null) return; // Classic's names are its own, and predate the rules
            var taken = new List<string>();
            var all = map.Countries.Select(c => c.Id).Concat(map.Provinces.Select(p => p.Id)).Concat(map.Regions.Select(r => r.Id)).ToList();
            foreach (string id in all)
            {
                string why = NameRules.WhyNot(id, taken);
                if (why != null) problems.Add(new MapProblem(ProblemKind.Content, "names", why));
                taken.Add(id);
            }
            for (int a = 0; a < map.Provinces.Count; a++)
            {
                var p = map.Provinces[a];
                if (p.Name != NameRules.Display(p.Id)) problems.Add(new MapProblem(ProblemKind.Content, "names", $"{p.Id} is shown as '{p.Name}'"));
                foreach (int b in g.Adj[a])
                    if (a < b && string.CompareOrdinal(p.Id, 0, map.Provinces[b].Id, 0, 3) == 0)
                        problems.Add(new MapProblem(ProblemKind.Content, "names", $"neighbours {p.Id} and {map.Provinces[b].Id} start with the same three letters"));
            }
        }

        private static void CheckSpecials(WorldMap map, Graph g, List<MapProblem> problems)
        {
            var regions = new HashSet<string>(StringComparer.Ordinal);
            var placed = new List<int>();
            foreach (var s in map.Specials)
            {
                if (!g.Index.TryGetValue(s.Province ?? "", out int i))
                {
                    problems.Add(new MapProblem(ProblemKind.Content, "specials", $"special place '{s.Name}' stands on unknown province {s.Province}"));
                    continue;
                }
                var p = map.Provinces[i];
                var t = SpecialTemplates.Get(s.Template);
                if (t == null) problems.Add(new MapProblem(ProblemKind.Content, "specials", $"{p.Id} carries unknown template {s.Template}"));
                else if (!t.Terrains.Contains(p.Terrain)) problems.Add(new MapProblem(ProblemKind.Content, "specials", $"{s.Template} cannot stand on {TerrainTable.Id(p.Terrain)} ({p.Id})"));
                if (p.Special != s.Template) problems.Add(new MapProblem(ProblemKind.Content, "specials", $"{p.Id}'s special is '{p.Special}', not {s.Template}"));
                if (p.Capital) problems.Add(new MapProblem(ProblemKind.Content, "specials", $"special place on capital {p.Id}"));
                else if (p.Owner != null) problems.Add(new MapProblem(ProblemKind.Content, "specials", $"special place in {p.Owner}'s starting realm ({p.Id})"));
                if (p.Region != null && !regions.Add(p.Region)) problems.Add(new MapProblem(ProblemKind.Content, "specials", $"two special places in region {p.Region}"));
                var d = g.Distances(i);
                foreach (int o in placed)
                    if (d[o] >= 0 && d[o] < SpecialSpacing)
                        problems.Add(new MapProblem(ProblemKind.Content, "specials", $"special places at {p.Id} and {map.Provinces[o].Id} are {d[o]} steps apart, under {SpecialSpacing}"));
                placed.Add(i);
                string story = SpecialTemplates.WhyNotStory(s.Story, map.Provinces.Select(x => x.Name).Concat(map.Countries.Select(c => c.Name)));
                if (story != null) problems.Add(new MapProblem(ProblemKind.Content, "specials", $"{p.Id}: {story}"));
            }
            foreach (var p in map.Provinces)
                if (p.Special != null && !map.Specials.Any(s => s.Province == p.Id))
                    problems.Add(new MapProblem(ProblemKind.Content, "specials", $"{p.Id} is marked {p.Special} but has no special place"));
        }

        // ------------------------------------------------------------------------------------
        // Measures, shared with the generator
        // ------------------------------------------------------------------------------------

        private static List<CountryFairness> Measure(WorldMap map, Graph g)
        {
            int n = map.Provinces.Count;
            var countryIndex = map.Countries.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
            var capitals = map.Countries.Select(c => map.Provinces.FindIndex(p => p.Capital && p.Owner == c.Id)).ToArray();
            var owner = map.Provinces.Select(p => p.Owner == null ? -1 : countryIndex[p.Owner]).ToArray();
            var value = map.Provinces.Select(p => ProvinceValue(p.Terrain, p.Population, p.Capital)).ToArray();
            var goods = map.Provinces.Select(p => p.Good).ToArray();
            var rough = map.Provinces.Select(p => p.Terrain == Terrain.Hills || p.Terrain == Terrain.Mountains || p.Terrain == Terrain.Marsh).ToArray();
            var raw = MeasureRaw(n, g.Adj, capitals, owner, value, goods, rough);
            for (int c = 0; c < raw.Length; c++)
            {
                raw[c].Country = map.Countries[c].Id;
                raw[c].Capital = capitals[c] >= 0 ? map.Provinces[capitals[c]].Id : null;
            }
            return raw.ToList();
        }

        /// <summary>Food + gold of one province by v2's tables (sections 3.2, 4.1, 4.3).</summary>
        public static int ProvinceValue(Terrain t, int population, bool capital) =>
            TerrainTable.Food(t) + population + (capital ? 5 : 0);

        /// <summary>The fairness measures on plain arrays, for the generator's search and for <see cref="Check"/>.</summary>
        internal static CountryFairness[] MeasureRaw(int n, List<int>[] adj, int[] capitals, int[] owner, int[] value, string[] goods, bool[] rough, int[][] allDistances = null)
        {
            int countries = capitals.Length;
            var dist = new int[countries][];
            for (int c = 0; c < countries; c++)
                dist[c] = capitals[c] < 0 ? Enumerable.Repeat(-1, n).ToArray()
                    : allDistances != null ? allDistances[capitals[c]] : Bfs(n, adj, capitals[c]);
            // Who is nearest each province: one capital, or several tied, who then share it.
            var reachOf = new int[n];
            var nearest = new int[n];
            var tied = new int[n];
            for (int p = 0; p < n; p++)
            {
                int best = int.MaxValue, who = -1, count = 0;
                for (int c = 0; c < countries; c++)
                {
                    int d = dist[c][p];
                    if (d < 0) continue;
                    if (d < best) { best = d; who = c; count = 1; }
                    else if (d == best) { who = -2; count++; }
                }
                reachOf[p] = who;
                nearest[p] = best;
                tied[p] = count;
            }
            var result = new CountryFairness[countries];
            for (int c = 0; c < countries; c++)
            {
                var f = new CountryFairness();
                var distinct = new HashSet<string>(StringComparer.Ordinal);
                for (int p = 0; p < n; p++)
                {
                    if (tied[p] == 0 || dist[c][p] != nearest[p]) continue;
                    int share = 60 / Math.Min(tied[p], 60);
                    f.Reach60 += share;
                    f.Value60 += value[p] * share;
                    if (reachOf[p] != c) continue; // goods and defensible borders count only what it alone reaches
                    if (goods[p] != null) distinct.Add(goods[p]);
                    if (rough[p])
                        foreach (int q in adj[p])
                            if (reachOf[q] != c) { f.Defensible++; break; }
                }
                f.Value60 += distinct.Count * 60;
                var neutral = new HashSet<int>();
                for (int p = 0; p < n; p++)
                    if (owner[p] == c)
                        foreach (int q in adj[p])
                            if (owner[q] < 0) neutral.Add(q);
                f.Neutral = neutral.Count;
                for (int o = 0; o < countries; o++)
                    if (o != c && capitals[o] >= 0 && dist[c][capitals[o]] >= 0 && dist[c][capitals[o]] <= RivalSteps) f.Rivals++;
                result[c] = f;
            }
            return result;
        }

        /// <summary>
        /// A single number for how far the measures are outside their bounds: 0 when every fairness
        /// bound holds. The generator's search minimises it.
        /// </summary>
        internal static double Excess(CountryFairness[] m)
        {
            long minReach = m.Min(x => x.Reach60), maxReach = m.Max(x => x.Reach60);
            long minValue = m.Min(x => x.Value60), maxValue = m.Max(x => x.Value60);
            int minN = m.Min(x => x.Neutral), maxN = m.Max(x => x.Neutral);
            int minR = m.Min(x => x.Rivals), maxR = m.Max(x => x.Rivals);
            double e = 0;
            e += Math.Max(0, maxReach * 10 - minReach * ReachSpreadTenths) / 600.0;
            e += Math.Max(0, maxValue * 100 - minValue * ValueSpreadPercent) / 6000.0;
            e += minN == 0 ? 10 : Math.Max(0, maxN - minN * NeutralSpread);
            e += Math.Max(0, maxR - minR - RivalSpread(m.Length));
            return e;
        }

        internal static int[] Bfs(int n, List<int>[] adj, int from)
        {
            var d = new int[n];
            for (int i = 0; i < n; i++) d[i] = -1;
            var q = new int[n];
            int head = 0, tail = 0;
            d[from] = 0;
            q[tail++] = from;
            while (head < tail)
            {
                int p = q[head++];
                foreach (int r in adj[p])
                    if (d[r] < 0) { d[r] = d[p] + 1; q[tail++] = r; }
            }
            return d;
        }

        // ------------------------------------------------------------------------------------
        // Cell helpers
        // ------------------------------------------------------------------------------------

        internal static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        internal static (int, int) Unkey(long k) => ((int)(k >> 32), (int)(k & 0xFFFFFFFF));

        /// <summary>Cell edges shared by each pair of provinces.</summary>
        internal static Dictionary<long, int> SharedEdges(int[] cells, int w, int h)
        {
            var shared = new Dictionary<long, int>();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int v = cells[y * w + x];
                if (v < 0) continue;
                if (x + 1 < w) { int r = cells[y * w + x + 1]; if (r >= 0 && r != v) Bump(shared, Key(v, r)); }
                if (y + 1 < h) { int d = cells[(y + 1) * w + x]; if (d >= 0 && d != v) Bump(shared, Key(v, d)); }
            }
            return shared;
        }

        /// <summary>The unit edges each pair of provinces shares, as <c>(x0, y0, x1, y1)</c> in grid corners.</summary>
        internal static Dictionary<long, List<(int x0, int y0, int x1, int y1)>> EdgesByPair(int[] cells, int w, int h)
        {
            var edges = new Dictionary<long, List<(int, int, int, int)>>();
            void Add(long k, (int, int, int, int) e)
            {
                if (!edges.TryGetValue(k, out var l)) edges[k] = l = new List<(int, int, int, int)>();
                l.Add(e);
            }
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int v = cells[y * w + x];
                if (v < 0) continue;
                if (x + 1 < w) { int r = cells[y * w + x + 1]; if (r >= 0 && r != v) Add(Key(v, r), (x + 1, y, x + 1, y + 1)); }
                if (y + 1 < h) { int d = cells[(y + 1) * w + x]; if (d >= 0 && d != v) Add(Key(v, d), (x, y + 1, x + 1, y + 1)); }
            }
            return edges;
        }

        /// <summary>Whether a ridge line crosses a unit edge (touching an end counts).</summary>
        internal static bool Crosses(int[] line, (int x0, int y0, int x1, int y1) e)
        {
            long Orient(long ax, long ay, long bx, long by, long cx, long cy) => Math.Sign((bx - ax) * (cy - ay) - (by - ay) * (cx - ax));
            // The ridge is in grid units, the edge in corners: compare both doubled so a ridge through cell centres is exact.
            long px = line[0] * 2L + 1, py = line[1] * 2L + 1, qx = line[2] * 2L + 1, qy = line[3] * 2L + 1;
            long ax = e.x0 * 2L, ay = e.y0 * 2L, bx = e.x1 * 2L, by = e.y1 * 2L;
            long o1 = Orient(px, py, qx, qy, ax, ay), o2 = Orient(px, py, qx, qy, bx, by);
            long o3 = Orient(ax, ay, bx, by, px, py), o4 = Orient(ax, ay, bx, by, qx, qy);
            return o1 != o2 && o3 != o4;
        }

        private static void Bump(Dictionary<long, int> d, long k) => d[k] = d.TryGetValue(k, out int v) ? v + 1 : 1;

        private static int[] Pieces(int[] cells, int w, int h, int n)
        {
            var seen = new bool[cells.Length];
            var pieces = new int[n];
            var stack = new Stack<int>();
            for (int c = 0; c < cells.Length; c++)
            {
                int v = cells[c];
                if (v < 0 || seen[c]) continue;
                pieces[v]++;
                seen[c] = true;
                stack.Push(c);
                while (stack.Count > 0)
                {
                    int k = stack.Pop();
                    int x = k % w, y = k / w;
                    if (x > 0 && !seen[k - 1] && cells[k - 1] == v) { seen[k - 1] = true; stack.Push(k - 1); }
                    if (x + 1 < w && !seen[k + 1] && cells[k + 1] == v) { seen[k + 1] = true; stack.Push(k + 1); }
                    if (y > 0 && !seen[k - w] && cells[k - w] == v) { seen[k - w] = true; stack.Push(k - w); }
                    if (y + 1 < h && !seen[k + w] && cells[k + w] == v) { seen[k + w] = true; stack.Push(k + w); }
                }
            }
            return pieces;
        }

        /// <summary>A map's rules graph by province index.</summary>
        private sealed class Graph
        {
            public readonly Dictionary<string, int> Index;
            public readonly List<int>[] Adj;
            private readonly HashSet<long> pairs = new HashSet<long>();

            public Graph(WorldMap map)
            {
                Index = map.Provinces.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
                Adj = map.Provinces.Select(_ => new List<int>()).ToArray();
                foreach (var b in map.Borders)
                {
                    int a = Index[b[0]], c = Index[b[1]];
                    if (!pairs.Add(Key(a, c))) continue;
                    Adj[a].Add(c);
                    Adj[c].Add(a);
                }
                foreach (var l in Adj) l.Sort();
            }

            public bool Has(int a, int b) => pairs.Contains(Key(a, b));
            public int[] Distances(int from) => Bfs(Adj.Length, Adj, from);
        }
    }
}
