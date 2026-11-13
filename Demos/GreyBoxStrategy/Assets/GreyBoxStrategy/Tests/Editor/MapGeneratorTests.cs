using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace GreyBoxStrategy.Tests
{
    /// <summary>Rules v2 slice R2: the map generator, its checks, and today's rules on its worlds.</summary>
    public sealed class MapGeneratorTests
    {
        private static readonly LandShape[] Shapes = (LandShape[])Enum.GetValues(typeof(LandShape));
        private static readonly MapSize[] Sizes = (MapSize[])Enum.GetValues(typeof(MapSize));

        private static GeneratedWorld Make(int seed, MapSize size, LandShape shape = LandShape.Continent) =>
            MapGenerator.Generate(seed, MapSettings.DefaultCountries(size), new MapSettings { Size = size, Shape = shape });

        private static WorldMap Copy(WorldMap map) => WorldMap.FromJson(map.ToJson());

        private static string Sha(string s)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(s)).Select(b => b.ToString("x2")));
        }

        // --- Determinism -------------------------------------------------------------------------

        [Test]
        public void Determinism_TheSameSeedAndSettingsMakeTheSameWorld()
        {
            foreach (var size in new[] { MapSize.Small, MapSize.Medium })
            {
                string a = Make(11, size, LandShape.TwoLands).Map.ToJson();
                string b = Make(11, size, LandShape.TwoLands).Map.ToJson();
                Assert.That(b, Is.EqualTo(a), $"{size}");
                Assert.That(Make(12, size, LandShape.TwoLands).Map.ToJson(), Is.Not.EqualTo(a), $"{size}: another seed, another world");
            }
        }

        // Hashes measured under .NET 9 outside Unity; the same world under Unity's Mono shows the
        // generator does not depend on the runtime. A changed rule changes them: re-pin, and say in the
        // pull request that every seed now makes a different world. Re-pinned on 13 November when the
        // land shapes became irregular (warped shapes, scenery islands, land kept off the frame).
        [TestCase(MapSize.Small, 7, "d767564849cadd88d2e210c2baa5562af9a3c98aaae09c1f74abbd40006b8937")]
        [TestCase(MapSize.Medium, 7, "07221e93a71dadf1c5b8b8823fb33fcf50b282f64edf06ef2f1a77e22d202d82")]
        [TestCase(MapSize.Large, 7, "b80e80addca97d8bb695ffd4d596b3336a982b28a845c66df105872256dbe80f")]
        [TestCase(MapSize.Huge, 28, "4f8bab21058ed9f5a4674753269c8ce6d0b091ae2c5e3e96271dcc9a6fe0f9f4")]
        public void Determinism_ASeedsWorldIsPinned(MapSize size, int seed, string hash)
        {
            var made = Make(seed, size);
            Assert.That(made.Passed, Is.True);
            Assert.That(Sha(made.Map.ToJson(false)), Is.EqualTo(hash));
        }

        // --- Every check, on worlds of every size and shape ---------------------------------------

        private static IEnumerable<TestCaseData> Worlds()
        {
            foreach (var shape in Shapes)
            {
                yield return new TestCaseData(MapSize.Small, shape, 21);
                yield return new TestCaseData(MapSize.Small, shape, 22);
                yield return new TestCaseData(MapSize.Medium, shape, 21);
            }
            yield return new TestCaseData(MapSize.Large, LandShape.Continent, 21);
            yield return new TestCaseData(MapSize.Large, LandShape.InlandSea, 22);
            yield return new TestCaseData(MapSize.Huge, LandShape.TwoLands, 21);
        }

        [TestCaseSource(nameof(Worlds))]
        public void Generated_PassesEveryCheck(MapSize size, LandShape shape, int seed)
        {
            var made = Make(seed, size, shape);
            Assert.That(made.Passed, Is.True, string.Join("\n", made.Log));
            AssertSound(made.Map, MapSettings.DefaultCountries(size), new MapSettings().StartingRealm);
        }

        /// <summary>The page's 500 seeds a size, every shape: about twenty minutes. Run by hand; the pull request records the result.</summary>
        [Test, Explicit("long: thousands of worlds")]
        public void Generated_ManySeeds_AllPass([Values(MapSize.Small, MapSize.Medium, MapSize.Large, MapSize.Huge)] MapSize size)
        {
            var failed = new List<string>();
            foreach (var shape in Shapes)
                for (int seed = 1; seed <= 100; seed++)
                {
                    var made = Make(seed, size, shape);
                    if (!made.Passed) failed.Add($"{shape} {seed}: {string.Join("; ", made.Problems.Take(2))}");
                    else AssertSound(made.Map, MapSettings.DefaultCountries(size), 3);
                }
            Assert.That(failed, Is.Empty);
        }

        private static void AssertSound(WorldMap map, int countries, int realm)
        {
            Assert.That(MapChecks.Check(map), Is.Empty, "the world as made");
            var back = Copy(map);
            Assert.That(MapChecks.Check(back), Is.Empty, "the world read back from its JSON");

            var def = map.ToDefinition();
            Assert.That(def.Countries.Count, Is.EqualTo(countries));
            foreach (var p in def.Provinces)
                Assert.That(def.Neighbours(p.Id), Is.Not.Empty, $"{p.Id} has no neighbour");
            foreach (var c in def.Countries)
            {
                Assert.That(def.Provinces.Count(p => p.IsCapital && p.StartOwner == c.Id), Is.EqualTo(1), $"{c.Id}'s capital");
                Assert.That(def.Provinces.Count(p => p.StartOwner == c.Id), Is.EqualTo(realm), $"{c.Id}'s starting realm");
            }

            var f = MapChecks.Fairness(map);
            Assert.That(f.Max(x => x.Reach60) * 10, Is.LessThanOrEqualTo(f.Min(x => x.Reach60) * MapChecks.ReachSpreadTenths), "reach");
            Assert.That(f.Max(x => x.Value60) * 100, Is.LessThanOrEqualTo(f.Min(x => x.Value60) * MapChecks.ValueSpreadPercent), "value");
            Assert.That(f.Min(x => x.Neutral), Is.GreaterThan(0), "every start borders neutral land");
            Assert.That(f.Max(x => x.Neutral), Is.LessThanOrEqualTo(f.Min(x => x.Neutral) * MapChecks.NeutralSpread), "neutral");
            Assert.That(f.Max(x => x.Rivals) - f.Min(x => x.Rivals), Is.LessThanOrEqualTo(MapChecks.RivalSpread(countries)), "rivals");

            var ids = map.Countries.Select(c => c.Id).Concat(map.Provinces.Select(p => p.Id)).Concat(map.Regions.Select(r => r.Id)).ToList();
            Assert.That(ids, Is.Unique);
            foreach (string id in ids) Assert.That(id, Does.Match("^[a-z]{4,11}$"));
            foreach (var t in TerrainTable.All) Assert.That(map.Provinces.Any(p => p.Terrain == t), Is.True, $"no {t}");

            // The land never meets the frame, and scenery islands never touch a province.
            var geo = map.Geometry;
            var grid = geo.DecodeCells();
            for (int y = 0; y < geo.Height; y++)
            for (int x = 0; x < geo.Width; x++)
            {
                int v = grid[y * geo.Width + x];
                if (v >= 0) Assert.That(x > 0 && y > 0 && x < geo.Width - 1 && y < geo.Height - 1, Is.True, $"province cell on the frame at {x},{y}");
                if (v != MapGeometry.Island) continue;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < geo.Width && ny < geo.Height)
                        Assert.That(grid[ny * geo.Width + nx], Is.LessThan(0), $"island touches a province at {x},{y}");
                }
            }

            // The outlines are the cells: each province's loops enclose exactly its area.
            foreach (var shape in map.Geometry.Provinces)
            {
                long twice = 0;
                foreach (var loop in shape.Outline)
                    for (int k = 0; k < loop.Length; k += 2)
                    {
                        int j = (k + 2) % loop.Length;
                        twice += (long)loop[k] * loop[j + 1] - (long)loop[j] * loop[k + 1];
                    }
                Assert.That(twice, Is.EqualTo(2L * shape.Area), $"{shape.Id}'s outline");
            }
        }

        // --- The checks catch broken worlds --------------------------------------------------------

        private static WorldMap Sample()
        {
            var made = Make(7, MapSize.Small);
            Assert.That(made.Passed, Is.True);
            return made.Map;
        }

        private static IReadOnlyList<string> Neighbours(WorldMap m, string id) =>
            m.Borders.Where(b => b[0] == id || b[1] == id).Select(b => b[0] == id ? b[1] : b[0]).ToList();

        private static int[] Steps(WorldMap m, string from)
        {
            var def = m.ToDefinition();
            var d = m.Provinces.ToDictionary(p => p.Id, _ => -1);
            var q = new Queue<string>();
            d[from] = 0;
            q.Enqueue(from);
            while (q.Count > 0)
            {
                string p = q.Dequeue();
                foreach (string n in def.Neighbours(p))
                    if (d[n] < 0) { d[n] = d[p] + 1; q.Enqueue(n); }
            }
            return m.Provinces.Select(p => d[p.Id]).ToArray();
        }

        private static void Rename(WorldMap m, string from, string to)
        {
            string R(string x) => x == from ? to : x;
            foreach (var p in m.Provinces) { p.Id = R(p.Id); p.Name = NameRules.Display(p.Id); }
            foreach (var b in m.Borders) { b[0] = R(b[0]); b[1] = R(b[1]); }
            foreach (var r in m.Regions) r.Provinces = r.Provinces.Select(R).ToList();
            foreach (var s in m.Specials) s.Province = R(s.Province);
            foreach (var s in m.Geometry.Provinces) s.Id = R(s.Id);
            foreach (var b in m.Geometry.Borders) { b.A = R(b.A); b.B = R(b.B); }
            foreach (var r in m.Geometry.Ridges)
            {
                foreach (var x in r.Passes) { x[0] = R(x[0]); x[1] = R(x[1]); }
                foreach (var x in r.Cuts) { x[0] = R(x[0]); x[1] = R(x[1]); }
            }
        }

        private static void SetCells(WorldMap m, Func<int[], int[]> change) => m.Geometry.Cells = MapGeometry.EncodeCells(change(m.Geometry.DecodeCells()));

        [TestCase("isolated", "isolated")]
        [TestCase("isolated-capital", "capitals-reach")]
        [TestCase("phantom-border", "cell-adjacency")]
        [TestCase("lost-border", "cell-adjacency")]
        [TestCase("ridge-crossed", "ridge-pass")]
        [TestCase("pass-forgotten", "ridge-pass")]
        [TestCase("split", "one-piece")]
        [TestCase("merged", "area")]
        [TestCase("realm-grown", "realms")]
        [TestCase("capital-on-mountain", "capital")]
        [TestCase("capitals-close", "capital-distance")]
        [TestCase("rich-start", "value")]
        [TestCase("scarce-hoard", "scarce-goods")]
        [TestCase("real-name", "names")]
        [TestCase("special-on-capital", "specials")]
        [TestCase("special-wrong-terrain", "specials")]
        [TestCase("island-ashore", "island")]
        public void Checks_CatchABrokenWorld(string breakage, string check)
        {
            var m = Copy(Sample());
            Assert.That(MapChecks.Check(m), Is.Empty, "the sample passes before it is broken");
            var cap0 = m.Provinces.First(p => p.Capital && p.Owner == m.Countries[0].Id);
            var cells = m.Geometry.DecodeCells();
            int Index(string id) => m.Provinces.FindIndex(p => p.Id == id);

            switch (breakage)
            {
                case "isolated":
                {
                    var p = m.Provinces.First(x => x.Owner == null);
                    m.Borders.RemoveAll(b => b[0] == p.Id || b[1] == p.Id);
                    break;
                }
                case "isolated-capital":
                    m.Borders.RemoveAll(b => b[0] == cap0.Id || b[1] == cap0.Id);
                    break;
                case "phantom-border":
                {
                    var d = Steps(m, m.Provinces[0].Id);
                    int far = Array.IndexOf(d, d.Max());
                    m.Borders.Add(new[] { m.Provinces[0].Id, m.Provinces[far].Id });
                    break;
                }
                case "lost-border":
                {
                    // Two provinces that touch on the map stop being neighbours in the rules.
                    m.Borders.RemoveAt(0);
                    break;
                }
                case "ridge-crossed":
                {
                    var cut = m.Geometry.Ridges.SelectMany(r => r.Cuts).FirstOrDefault();
                    Assert.That(cut, Is.Not.Null, "the sample has a ridge with a cut");
                    m.Borders.Add(new[] { cut[0], cut[1] });
                    break;
                }
                case "pass-forgotten":
                {
                    var ridge = m.Geometry.Ridges.First(r => r.Passes.Count > 0);
                    ridge.Passes.RemoveAt(0);
                    break;
                }
                case "split":
                {
                    // A cell deep inside one province is given to another, far away.
                    var shape = m.Geometry.Provinces[0];
                    int far = Array.IndexOf(Steps(m, m.Provinces[0].Id), Steps(m, m.Provinces[0].Id).Max());
                    SetCells(m, c => { c[shape.Label[1] * m.Geometry.Width + shape.Label[0]] = far; return c; });
                    break;
                }
                case "merged":
                {
                    // The two largest neighbours become one: about twice the mean, and one with no cells at all.
                    var big = m.Borders.Select(b => (a: Index(b[0]), b: Index(b[1])))
                        .OrderByDescending(x => m.Geometry.Provinces[x.a].Area + m.Geometry.Provinces[x.b].Area).First();
                    SetCells(m, c => c.Select(v => v == big.b ? big.a : v).ToArray());
                    break;
                }
                case "realm-grown":
                {
                    var p = m.Provinces.First(x => x.Owner == null && Neighbours(m, x.Id).Any(n => m.Provinces[Index(n)].Owner == cap0.Owner));
                    p.Owner = cap0.Owner;
                    break;
                }
                case "capital-on-mountain":
                    cap0.Terrain = Terrain.Mountains;
                    break;
                case "capitals-close":
                {
                    // The second country's capital moves two steps from the first's.
                    var d = Steps(m, cap0.Id);
                    var near = m.Provinces.Where((x, i) => d[i] == 2 && x.Owner == null).First();
                    var cap1 = m.Provinces.First(x => x.Capital && x.Owner == m.Countries[1].Id);
                    cap1.Capital = false;
                    near.Owner = m.Countries[1].Id;
                    near.Capital = true;
                    break;
                }
                case "rich-start":
                {
                    var d = Steps(m, cap0.Id);
                    for (int i = 0; i < m.Provinces.Count; i++) if (d[i] <= 2) m.Provinces[i].Population = 12;
                    break;
                }
                case "scarce-hoard":
                    foreach (var p in m.Provinces.Where(x => x.Owner == cap0.Owner)) p.Good = "iron";
                    break;
                case "real-name":
                    Rename(m, m.Provinces[3].Id, "london");
                    break;
                case "special-on-capital":
                    m.Specials.Add(new SpecialPlace { Province = cap0.Id, Template = "healing_spring", Name = "The Wells", Story = "Warm water.", MadeBy = "test" });
                    cap0.Special = "healing_spring";
                    break;
                case "special-wrong-terrain":
                {
                    var s = m.Specials.First();
                    var p = m.Provinces[Index(s.Province)];
                    s.Template = p.Terrain == Terrain.Forest ? "horse_plains" : "great_wood";
                    p.Special = s.Template;
                    break;
                }
                case "island-ashore":
                {
                    // The sea cell beside a coastal province becomes a scenery island.
                    int w = m.Geometry.Width;
                    SetCells(m, c =>
                    {
                        for (int k = w; k < c.Length - w; k++)
                            if (c[k] == MapGeometry.Sea && (c[k - 1] >= 0 || c[k + 1] >= 0)) { c[k] = MapGeometry.Island; break; }
                        return c;
                    });
                    break;
                }
                default:
                    Assert.Fail("unknown breakage");
                    break;
            }
            var problems = MapChecks.Check(m);
            Assert.That(problems.Select(p => p.Check), Does.Contain(check), string.Join("\n", problems));
        }

        // Two shapes no generated world has, drawn by hand: a province one cell wide, and four
        // provinces meeting at one corner.
        [TestCase("thin", "width")]
        [TestCase("corner", "corner-contact")]
        public void Checks_CatchABadShape(string kind, string check)
        {
            var m = new WorldMap { Name = "Test" };
            m.Countries.Add(new CountryData { Id = "aaaa", Name = "Aaaa" });
            m.Countries.Add(new CountryData { Id = "bbbb", Name = "Bbbb" });
            int[] cells;
            int w, h;
            if (kind == "thin")
            {
                w = 12; h = 3;
                cells = Enumerable.Range(0, w * h).Select(c => c < w ? 0 : 1).ToArray();
                m.Provinces.Add(new ProvinceData { Id = "pone", Name = "Pone", Owner = "aaaa", Capital = true });
                m.Provinces.Add(new ProvinceData { Id = "ptwo", Name = "Ptwo", Owner = "bbbb", Capital = true });
                m.Borders.Add(new[] { "pone", "ptwo" });
            }
            else
            {
                w = 2; h = 2;
                cells = new[] { 0, 1, 2, 3 };
                foreach (var (id, owner) in new[] { ("pone", "aaaa"), ("ptwo", "bbbb"), ("pthree", (string)null), ("pfour", null) })
                    m.Provinces.Add(new ProvinceData { Id = id, Name = NameRules.Display(id), Owner = owner, Capital = owner != null });
                m.Borders.Add(new[] { "pone", "ptwo" });
                m.Borders.Add(new[] { "pone", "pthree" });
                m.Borders.Add(new[] { "ptwo", "pfour" });
                m.Borders.Add(new[] { "pthree", "pfour" });
            }
            m.Geometry = MapGeometry.Build(cells, w, h, m.Provinces.Select(p => p.Id).ToList());
            var problems = MapChecks.Check(m);
            Assert.That(problems.Select(p => p.Check), Does.Contain(check), string.Join("\n", problems));
        }

        // --- Retries, and reporting failure ------------------------------------------------------------

        [Test]
        public void Generator_RetriesUntilAWorldPasses_AndReportsWhenNoneDid()
        {
            var settings = new MapSettings { Size = MapSize.Small, Shape = LandShape.LongCoast };
            var once = MapGenerator.Generate(1, 4, settings, maxAttempts: 1);
            Assert.That(once.Passed, Is.False, "seed 1's first try fails (found by search, 13 November)");
            Assert.That(once.Attempts, Is.EqualTo(1));
            Assert.That(once.Problems, Is.Not.Empty);
            Assert.That(once.Log.Single(), Does.StartWith("try 0: "));
            Assert.That(once.Map, Is.Not.Null, "the best world found is still returned");
            Assert.That(once.Playable, Is.True, "it fails fairness, not connectivity");

            var retried = MapGenerator.Generate(1, 4, settings);
            Assert.That(retried.Passed, Is.True);
            Assert.That(retried.Attempts, Is.GreaterThan(1));
            Assert.That(retried.Map.Generated.Attempt, Is.EqualTo(retried.Attempts - 1));
            Assert.That(retried.Log.Count, Is.EqualTo(retried.Attempts - 1));
        }

        [Test]
        public void Generator_RefusesSettingsItCannotMeet()
        {
            Assert.Throws<ArgumentException>(() => MapGenerator.Generate(1, 9, new MapSettings { Size = MapSize.Medium }));
            Assert.Throws<ArgumentException>(() => MapGenerator.Generate(1, 4, new MapSettings { StartingRealm = 6 }));
            Assert.Throws<ArgumentException>(() => MapGenerator.Generate(1, 2, new MapSettings()));
            Assert.That(new MapSettings { Size = MapSize.Small, StartingRealm = 5 }.WhyInvalid(5), Does.Contain("over 60%"));
        }

        // --- The hooks the model's slices plug into ------------------------------------------------------

        private sealed class Maker : ISpecialPlaceMaker
        {
            private readonly bool valid;
            public int Calls;
            public Maker(bool valid) { this.valid = valid; }

            public SpecialPlace Make(SpecialSlot slot, WorldMap world)
            {
                Calls++;
                Assert.That(slot.Allowed, Is.Not.Empty);
                return new SpecialPlace
                {
                    Province = slot.Province,
                    Template = valid ? slot.Allowed[slot.Allowed.Count - 1] : "dragon_lair",
                    Name = "The Test Stone",
                    Story = "A stone that hums when strangers pass.",
                    MadeBy = "model",
                };
            }
        }

        [Test]
        public void Specials_AMakersChoiceIsUsed_AndABadOneFallsBackToTheTables()
        {
            var good = new Maker(true);
            var made = MapGenerator.Generate(7, 4, new MapSettings { Size = MapSize.Small, Specials = SpecialPlaces.Many }, good);
            Assert.That(made.Passed, Is.True, string.Join("\n", made.Problems));
            Assert.That(good.Calls, Is.EqualTo(made.Slots.Count).And.GreaterThan(0));
            Assert.That(made.Map.Specials.All(s => s.MadeBy == "model" && s.Name == "The Test Stone"), Is.True);

            var bad = new Maker(false);
            var fallback = MapGenerator.Generate(7, 4, new MapSettings { Size = MapSize.Small, Specials = SpecialPlaces.Many }, bad);
            Assert.That(fallback.Passed, Is.True);
            Assert.That(fallback.Map.Specials, Is.Not.Empty);
            Assert.That(fallback.Map.Specials.All(s => s.MadeBy == "table" && s.Template != "dragon_lair"), Is.True);
        }

        [Test]
        public void Specials_FollowTheirRules()
        {
            var made = MapGenerator.Generate(5, 6, new MapSettings { Size = MapSize.Medium, Specials = SpecialPlaces.Many });
            Assert.That(made.Passed, Is.True);
            var map = made.Map;
            Assert.That(map.Specials.Count, Is.GreaterThan(2));
            foreach (var s in map.Specials)
            {
                var p = map.Provinces.First(x => x.Id == s.Province);
                Assert.That(p.Capital || p.Owner != null, Is.False, $"{p.Id}: never a capital or a starting realm");
                Assert.That(SpecialTemplates.Get(s.Template).Terrains, Does.Contain(p.Terrain));
                Assert.That(s.Story, Is.Not.Empty);
            }
            Assert.That(map.Specials.Select(s => map.Provinces.First(x => x.Id == s.Province).Region), Is.Unique);
            Assert.That(MapGenerator.Generate(5, 6, new MapSettings { Size = MapSize.Medium, Specials = SpecialPlaces.None }).Map.Specials, Is.Empty);
        }

        private sealed class Namer : INameMaker
        {
            public IReadOnlyList<string> ProvinceNames(RegionData region, string style, WorldMap world) =>
                region.Provinces.Select((p, i) => i == 0 ? "Quillowmere" : i == 1 ? "London" : null).ToList();
        }

        [Test]
        public void Names_AMakersNameIsUsed_AndOneThatFailsItsChecksIsNot()
        {
            var made = MapGenerator.Generate(7, 4, new MapSettings { Size = MapSize.Small }, names: new Namer());
            Assert.That(made.Passed, Is.True, string.Join("\n", made.Problems));
            Assert.That(made.Map.Provinces.Count(p => p.Id == "quillowmere"), Is.EqualTo(1), "the first proposal passes once; later regions repeat it and fall back");
            Assert.That(made.Map.Provinces.Any(p => p.Id == "london"), Is.False, "a real place is refused");
            Assert.That(made.Map.Provinces.First(p => p.Id == "quillowmere").Name, Is.EqualTo("Quillowmere"));
        }

        [TestCase("london", "real place")]
        [TestCase("abc", "4 to 11")]
        [TestCase("north-x", "not a lowercase letter")]
        [TestCase("army", "reserved")]
        [TestCase("oakhurst", "already used")]
        [TestCase("oakhursts", "prefix")]
        [TestCase("oakhorst", "one letter")]
        [TestCase("fenwick", "same three letters")]
        public void Names_TheRulesRefuseWithAReason(string name, string reason)
        {
            Assert.That(NameRules.WhyNot(name, new[] { "oakhurst" }, new[] { "fenmore" }), Does.Contain(reason));
        }

        // --- Speed --------------------------------------------------------------------------------------

        [Test]
        public void Speed_OneTryAtTheLargestSize_IsUnderTwoSeconds()
        {
            MapGenerator.Generate(1, 4, new MapSettings { Size = MapSize.Small }, maxAttempts: 1); // warm the code
            var watch = Stopwatch.StartNew();
            for (int seed = 31; seed < 34; seed++)
                MapGenerator.Generate(seed, 10, new MapSettings { Size = MapSize.Huge }, maxAttempts: 1);
            watch.Stop();
            TestContext.WriteLine($"huge, one try: {watch.ElapsedMilliseconds / 3} ms on average");
            Assert.That(watch.ElapsedMilliseconds / 3, Is.LessThan(2000));
        }

        // --- Today's rules on generated worlds --------------------------------------------------------

        private sealed class Legal : IPlayer
        {
            private readonly IPlayer inner;
            public int Requests;
            public Legal(IPlayer inner) { this.inner = inner; }
            public string Kind => inner.Kind;

            public Move Decide(CountryRequest r)
            {
                Requests++;
                if (r.Moment == Moment.Council)
                    foreach (var m in r.Legal)
                        Assert.That(Legality.IsLegal(r.World, r.CountryId, m), Is.True, $"turn {r.Turn} {r.CountryId}: offered {m}");
                var choice = inner.Decide(r);
                Assert.That(r.Legal, Does.Contain(choice));
                return choice;
            }
        }

        [TestCase(MapSize.Small, LandShape.Peninsula)]
        [TestCase(MapSize.Medium, LandShape.Continent)]
        [TestCase(MapSize.Large, LandShape.InlandSea)]
        [TestCase(MapSize.Huge, LandShape.TwoLands)]
        public void TodaysRules_PlayOnAGeneratedWorld(MapSize size, LandShape shape)
        {
            foreach (string lineup in new[] { "mixed", "random" })
            {
                var setup = new GameSetup
                {
                    Seed = 3,
                    Map = GameSetup.GeneratedMapId,
                    Countries = MapSettings.DefaultCountries(size),
                    Generator = new MapSettings { Size = size, Shape = shape },
                };
                var map = setup.BuildMap();
                Assert.That(map.Source.Geometry, Is.Not.Null, "the rules' map keeps the world's geometry for the screen");
                var seats = Lineups.Seat(lineup, 0, map).ToDictionary(kv => kv.Key, kv => (IPlayer)new Legal(kv.Value));
                var g = GameRunner.Play(setup.Seed, seats, map, setup.BuildRules(map));
                Assert.That(g.Game.IsOver, Is.True);
                Assert.That(g.Result.Turns, Is.InRange(1, setup.TurnLimit));
                Assert.That(g.Game.World.Country(g.Result.Winner).Alive, Is.True);
                Assert.That(g.Reports.Last().OwnerGrid.Count(char.IsLetter), Is.EqualTo(map.Provinces.Count(p => g.Game.World.Province(p.Id).Owner != null)),
                    "the log's owner grid shows every owned province once");
            }
        }

        [Test]
        public void Runner_PlaysASeriesOnOneGeneratedWorld()
        {
            var setup = new GameSetup
            {
                Map = GameSetup.GeneratedMapId,
                MapSeed = 9,
                Countries = 6,
                Generator = new MapSettings { Size = MapSize.Medium, Shape = LandShape.Peninsula },
            };
            var map = setup.BuildMap();
            Assert.That(new GameSetup { Map = GameSetup.GeneratedMapId, MapSeed = 9, Seed = 77, Countries = 6, Generator = setup.Generator }
                .BuildWorld().ToJson(), Is.EqualTo(map.Source.ToJson()), "the map seed, not the game seed, makes the world");

            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gbs-world-runner-" + Guid.NewGuid().ToString("N"));
            try
            {
                var summary = StrategyRuns.Run(folder, "mixed", 6, 1, false, map, setup.BuildRules(map));
                Assert.That(summary.Kinds.Sum(k => k.Wins), Is.EqualTo(6));
                Assert.That(summary.Kinds.Sum(k => k.Seats), Is.EqualTo(36), "six games of six seats");
                Assert.That(summary.WinsByCountry.Keys, Is.EquivalentTo(map.Countries.Select(c => c.Id)));
                Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "summary.md")), Does.Contain("| expansionist |"));
            }
            finally
            {
                if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
            }
        }

        [Test]
        public void Setup_AGeneratedMap_RoundTripsAndRebuildsTheSameWorld()
        {
            var setup = new GameSetup
            {
                Seed = 44,
                Map = GameSetup.GeneratedMapId,
                Countries = 4,
                Generator = new MapSettings { Size = MapSize.Small, Shape = LandShape.InlandSea, StartingRealm = 2, Specials = SpecialPlaces.Many },
            };
            var back = GameSetup.FromJson(setup.ToJson());
            Assert.That(back.ToJson(), Is.EqualTo(setup.ToJson()));
            Assert.That(back.ToJson(), Does.Contain("\"shape\": \"inland_sea\""));
            Assert.That(back.BuildWorld().ToJson(), Is.EqualTo(setup.BuildWorld().ToJson()));
            Assert.That(back.BuildMap().Provinces.Count(p => p.StartOwner != null), Is.EqualTo(8));
        }

        [TestCase("{ \"map\": \"generated\", \"countries\": 9, \"generator\": { \"size\": \"medium\" } }", "at most 8 countries")]
        [TestCase("{ \"map\": \"generated\", \"generator\": { \"startingRealm\": 6 } }", "outside 2 to 5")]
        [TestCase("{ \"map\": \"generated\", \"generator\": { \"shape\": \"donut\" } }", "cannot be read")]
        [TestCase("{ \"map\": \"classic\", \"generator\": { } }", "only to a generated map")]
        public void Setup_InvalidGeneratorSettings_AreRefused(string json, string message)
        {
            var e = Assert.Throws<FormatException>(() => GameSetup.FromJson(json));
            Assert.That(e.Message, Does.Contain(message));
        }
    }
}
