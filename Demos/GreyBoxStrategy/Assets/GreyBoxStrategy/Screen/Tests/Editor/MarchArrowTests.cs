using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Realm;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>A march's arrow: a body of even width along the path, a head whose tip is the destination.</summary>
    public class MarchArrowTests
    {
        [Test]
        public void An_arrow_ends_on_its_destination_with_a_head_wider_than_its_body()
        {
            var path = new List<Vector2> { new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100) };
            var outline = ScreenUI.MarchLines.ArrowOutline(path, 7, 20);
            Assert.That(outline.Count, Is.GreaterThan(8));
            Assert.That(outline.Any(o => Vector2.Distance(o, path[2]) < 0.01f), Is.True, "the tip is the destination");
            // The head: two points 20 apart across the path, just before the tip.
            var headPts = outline.Where(o => o.y > 70 && o.y < 90).ToList();
            Assert.That(headPts.Max(o => o.x) - headPts.Min(o => o.x), Is.EqualTo(20).Within(1.5f), "the head is 20 wide");
            var mid = outline.Where(o => o.x > 40 && o.x < 70).ToList();
            Assert.That(mid.Max(o => o.y) - mid.Min(o => o.y), Is.EqualTo(7).Within(0.5f), "the body is 7 wide along the way");
            // Every outline point stays within half a head of the path.
            Assert.That(outline.All(o => Mathf.Min(Mathf.Abs(o.y), Mathf.Abs(o.x - 100)) <= 10.5f || (o.x <= 100 && o.y <= 100)), Is.True);
        }

        [Test]
        public void A_path_with_no_length_draws_nothing()
        {
            Assert.That(ScreenUI.MarchLines.ArrowOutline(new List<Vector2> { Vector2.one, Vector2.one }, 7, 20), Is.Empty);
        }
    }
}
