using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Chart;
using GreyBoxStrategy.Realm;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// Rules v2 R4's test "no label collides at three zoom levels": every name, realm name, piece and march tag a
    /// generated world puts on the map, settled by <see cref="MapDeclutter"/>, and the rules it settles them by.
    /// </summary>
    public class MapLabelTests
    {
        private static readonly Dictionary<MapSize, WorldMap> worlds = new Dictionary<MapSize, WorldMap>();

        private static WorldMap World(MapSize size)
        {
            if (!worlds.TryGetValue(size, out var w))
            {
                var made = MapGenerator.Generate(7, MapSettings.DefaultCountries(size), new MapSettings { Size = size });
                Assert.That(made.Playable, Is.True, $"seed 7 makes a playable {size} world");
                worlds[size] = w = made.Map;
            }
            return w;
        }

        // Far (realms and regions named), the fitted view of a medium world (provinces named), and zoomed in.
        private static readonly float[] Zooms = { 3f, 6f, 14f };

        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Huge)]
        public void Nothing_on_the_map_covers_anything_else_at_three_zooms(MapSize size)
        {
            var world = World(size);
            var owners = world.Provinces.Select(p => p.Owner).ToList();
            var names = ChartLabels.For(world);
            var realms = RealmLabels.For(world, owners, id => world.Countries.First(c => c.Id == id).Name ?? id);
            foreach (float zoom in Zooms)
            {
                ChartLabels.Fit(names, zoom);
                RealmLabels.Fit(realms, zoom);
                var items = new List<MapItem>();
                // A piece on every owned province, set apart as the screen sets them (ScreenUI.Unclutter), and a march
                // tag under every third one's neighbour: crowded on purpose.
                var owned = Enumerable.Range(0, world.Provinces.Count).Where(p => owners[p] != null).ToList();
                // One to three armies a province, side by side over its name as ScreenUI.HostPoint stands them.
                var feet = new List<UnityEngine.Vector2>();
                foreach (int p in owned)
                {
                    int n = 1 + p % 3;
                    for (int c = 0; c < n; c++)
                        feet.Add(new UnityEngine.Vector2(names[p].U * zoom + (c - (n - 1) / 2f) * 58, names[p].V * zoom - 24));
                }
                var sizes = feet.Select(_ => new UnityEngine.Vector2(52, 20)).ToList();
                ScreenUI.Unclutter(feet, sizes, feet.Select(_ => false).ToList());
                foreach (var f in feet) items.Add(new MapItem(MapItemKind.Piece, f.x - 26, f.y - 20, f.x + 26, f.y));
                for (int k = 0; k < owned.Count; k++)
                {
                    if (k % 3 == 0 && owned[k] + 1 < world.Provinces.Count)
                    {
                        var d = names[owned[k] + 1];
                        float tx = d.U * zoom, ty = d.V * zoom + 12;
                        items.Add(new MapItem(MapItemKind.Tag, tx - 20, ty, tx + 20, ty + 14));
                    }
                }
                foreach (var l in names.Where(l => l.Visible))
                {
                    float w = ChartLabels.Width(l), h = l.Size * 1.25f, x = l.U * zoom, y = l.V * zoom;
                    var kind = l.Region ? MapItemKind.Region : l.Capital ? MapItemKind.Capital : MapItemKind.Name;
                    float y0 = kind == MapItemKind.Capital ? y + 4 : y - h / 2;
                    items.Add(new MapItem(kind, x - w / 2, y0, x + w / 2, y0 + h, l.Size));
                }
                foreach (var r in realms.Where(r => r.Visible))
                {
                    float w = RealmLabels.Width(r), h = r.Size * 1.25f, a = r.Angle * UnityEngine.Mathf.Deg2Rad;
                    float bw = System.Math.Abs(w * UnityEngine.Mathf.Cos(a)) + System.Math.Abs(h * UnityEngine.Mathf.Sin(a));
                    float bh = System.Math.Abs(w * UnityEngine.Mathf.Sin(a)) + System.Math.Abs(h * UnityEngine.Mathf.Cos(a));
                    items.Add(new MapItem(MapItemKind.Realm, r.U * zoom - bw / 2, r.V * zoom - bh / 2, r.U * zoom + bw / 2, r.V * zoom + bh / 2, r.Size));
                }

                var result = MapDeclutter.Resolve(items);
                var shown = items.Select((m, i) => (m, r: result[i])).Where(x => x.r.shown).Select(x => x.m.Down(x.r.dy)).ToList();
                var hits = MapDeclutter.Collisions(shown);
                Assert.That(hits, Is.Empty, $"{size} at {zoom} px a cell: " + string.Join(", ", hits.Select(kv => $"{kv.Key} {kv.Value}")));
                Assert.That(items.Select((m, i) => (m, r: result[i])).Where(x => x.m.Kind == MapItemKind.Piece || x.m.Kind == MapItemKind.Tag).All(x => x.r.shown), Is.True,
                    "pieces and march tags always show");
                bool far = zoom < ChartLabels.RegionZoom;
                Assert.That(shown.Any(m => m.Kind == (far ? MapItemKind.Realm : MapItemKind.Name)), Is.True,
                    far ? "zoomed out, realms are named" : "zoomed in, provinces are named");
                if (!far) Assert.That(shown.Count(m => m.Kind == MapItemKind.Name || m.Kind == MapItemKind.Capital), Is.GreaterThan(names.Count(l => l.Visible && !l.Region) / 2),
                    "most names that fit their province still show among the pieces");
            }
        }

        [Test]
        public void A_name_a_piece_would_cover_steps_down_a_little_or_hides()
        {
            var piece = new MapItem(MapItemKind.Piece, 0, 0, 50, 20);
            var grazed = new MapItem(MapItemKind.Name, 10, 16, 60, 30);      // covered by 4 pixels: steps down 5
            var buried = new MapItem(MapItemKind.Name, 10, 2, 40, 14);       // covered whole: 19 down is more than its height
            var other = new MapItem(MapItemKind.Piece, 200, 0, 250, 20);
            var tag = new MapItem(MapItemKind.Tag, 210, 10, 240, 24);
            var r = MapDeclutter.Resolve(new[] { grazed, buried, tag, piece, other });
            Assert.That(r[3], Is.EqualTo((true, 0f)), "the piece stays");
            Assert.That(r[2].shown && r[2].dy >= 10, Is.True, "the tag moves below the piece");
            Assert.That(r[0].shown, Is.True, "a grazed name stays");
            Assert.That(r[0].dy, Is.GreaterThan(0), "and steps down");
            Assert.That(r[1].shown, Is.False, "a buried name hides");
        }

        [Test]
        public void A_piece_caught_between_two_others_gets_clear_of_both()
        {
            // Pushed up off the lower one it meets the upper one: it must go on up, not back down into the first.
            var at = new List<UnityEngine.Vector2> { new UnityEngine.Vector2(0, 0), new UnityEngine.Vector2(4, -40), new UnityEngine.Vector2(2, -18) };
            var size = at.Select(_ => new UnityEngine.Vector2(52, 20)).ToList();
            ScreenUI.Unclutter(at, size, new List<bool> { false, false, false });
            var boxes = at.Select((p, i) => new MapItem(MapItemKind.Piece, p.x - 26, p.y - 20, p.x + 26, p.y)).ToList();
            Assert.That(MapDeclutter.Collisions(boxes), Is.Empty, string.Join(" ", at));
        }

        [Test]
        public void A_realm_or_region_name_that_would_cover_something_hides()
        {
            var name = new MapItem(MapItemKind.Capital, 0, 0, 80, 16, 16);
            var region = new MapItem(MapItemKind.Region, 40, 8, 140, 22, 13);
            var realm = new MapItem(MapItemKind.Realm, 200, 0, 400, 30, 24);
            var r = MapDeclutter.Resolve(new[] { region, realm, name });
            Assert.That(r.Select(x => x.shown), Is.EqualTo(new[] { false, true, true }));
        }

        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Huge)]
        public void The_province_panel_names_the_settlement_the_chart_draws(MapSize size)
        {
            var world = World(size);
            var chart = ChartImage.Make(world, world.Geometry.Width * 3, 7, gpu: true);
            var glyphs = chart.Scene.Glyphs.Where(g => g.Kind == GlyphKind.Settlement).ToList();
            Assert.That(glyphs.Count, Is.EqualTo(world.Provinces.Count), "one settlement a province");
            // The glyphs are sorted for drawing: each province's is the one standing where its settlement does.
            var drawn = Enumerable.Range(0, world.Provinces.Count).Select(p =>
            {
                var (u, v) = chart.Scene.Settlements[p];
                return SettlementLook.Tier(glyphs.Single(g => System.Math.Abs(g.U - u) < 1e-4f && System.Math.Abs(g.V - v) < 1e-4f).Param);
            }).ToList();
            Assert.That(drawn, Is.EqualTo(world.Provinces.Select(SettlementLook.TierOf).ToList()), "each province's settlement is the tier the panel reads");
            Assert.That(drawn.Distinct().Count(), Is.GreaterThanOrEqualTo(3), "the tiers vary across the world");
        }

        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Huge)]
        public void Each_realm_with_joined_land_is_named_on_it(MapSize size)
        {
            var world = World(size);
            var owners = world.Provinces.Select(p => p.Owner).ToList();
            var realms = RealmLabels.For(world, owners, id => id);
            // Named: every realm with two provinces that touch, on that joined land.
            var ownerOf = world.Provinces.ToDictionary(p => p.Id, p => p.Owner);
            var joined = world.Borders.Where(b => ownerOf[b[0]] != null && ownerOf[b[0]] == ownerOf[b[1]]).Select(b => ownerOf[b[0]]).Distinct().OrderBy(x => x).ToList();
            Assert.That(realms.Select(r => r.Country).OrderBy(x => x), Is.EqualTo(joined));
            var geo = world.Geometry;
            foreach (var r in realms)
            {
                // The province whose label point is nearest the name's centre is the realm's own.
                int nearest = Enumerable.Range(0, world.Provinces.Count)
                    .OrderBy(p => System.Math.Pow(geo.Provinces[p].Label[0] + 0.5 - r.U, 2) + System.Math.Pow(geo.Provinces[p].Label[1] + 0.5 - r.V, 2)).First();
                Assert.That(owners[nearest], Is.EqualTo(r.Country), $"{r.Country}'s name stands on its land");
                Assert.That(System.Math.Abs(r.Angle), Is.LessThanOrEqualTo(RealmLabels.MaxAngle), "readable: never steeper than the limit");
                Assert.That(r.Span, Is.GreaterThan(0));
            }
        }
    }
}
