using System.Linq;
using System.Text.RegularExpressions;
using GreyBoxStrategy.Replay;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>The first-turn help: at most six notes, in the screen's words, about what this game shows.</summary>
    public class HelpCalloutsTests
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void The_help_says_what_this_game_shows_in_six_notes_at_most(bool advisor, bool bigWorld)
        {
            var notes = HelpCallouts.ForRule(advisor, bigWorld);
            Assert.That(notes.Count, Is.LessThanOrEqualTo(HelpCallouts.Max));
            Assert.That(HelpCallouts.Max, Is.LessThanOrEqualTo(6), "the lead's brief: no more than six callouts");
            var keys = notes.Select(n => n.Key).ToList();
            Assert.That(keys, Is.Unique);
            foreach (var always in new[] { "writs", "army", "hatching", "endturn" })
                Assert.That(keys, Does.Contain(always), $"every rule game explains {always}");
            Assert.That(keys.Contains("edge"), Is.EqualTo(bigWorld), "the edge markers where they show: a big world");
            Assert.That(keys.Contains("advisor"), Is.EqualTo(advisor), "the advisor when it is on");
            Assert.That(keys.Last(), Is.EqualTo("endturn"), "the last note is the last thing you do");
            foreach (var n in notes)
            {
                Assert.That(n.Title, Is.Not.Empty);
                Assert.That(n.Text.Length, Is.InRange(40, 220), $"{n.Key}: a sentence or two");
                // A player's words: no file, rule, class or id names.
                Assert.That(Regex.IsMatch(n.Text + n.Title, @"\.cs\b|`|_|[a-z][A-Z]|rules v2|R\d\b"), Is.False, $"{n.Key}: \"{n.Text}\"");
            }
        }
    }
}
