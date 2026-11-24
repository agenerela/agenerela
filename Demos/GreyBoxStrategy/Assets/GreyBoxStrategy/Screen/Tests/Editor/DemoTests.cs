using System;
using System.IO;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The demo director's timeline, its selectors and its callouts' placement, with no scene: the format a demo video
    /// is written in, and the lookup that finds an element by name rather than by pixel.
    /// </summary>
    public class DemoTests
    {
        private static string ProjectFolder => Path.GetDirectoryName(Application.dataPath);
        private static string Sample => File.ReadAllText(Path.Combine(ProjectFolder, DemoDirector.TimelineFolder, "sample.demo.txt"));

        // ---- the timeline ----

        [Test]
        public void The_sample_timeline_parses_starts_with_its_recording_and_every_selector_in_it_is_valid()
        {
            var s = DemoScript.Parse(Sample);
            Assert.That(s.Steps[0].Verb, Is.EqualTo("load"));
            Assert.That(File.Exists(Path.Combine(ProjectFolder, s.Steps[0].Arg(0))), "the recording it loads is in the project");
            Assert.That(s.Steps.Any(x => x.Verb == "click"), Is.True);
            foreach (var st in s.Steps.Where(x => x.Verb == "click" || x.Verb == "callout" || x.Verb == "dim" || x.Verb == "cursor"))
                Assert.DoesNotThrow(() => DemoSelector.Parse(st.Arg(0)), st.ToString());
            // 45 to 60 s in all: the waits and titles alone leave room for the replay's own pace.
            Assert.That(s.KnownSeconds, Is.InRange(30f, 55f));
        }

        [Test]
        public void A_line_is_a_verb_its_arguments_and_its_options_and_quotes_keep_words_together()
        {
            var s = DemoScript.Parse("load a.json\ncallout \".inspector .code-box\" \"The reply\" \"Action, then \\\"target\\\"\" side=left width=400\n# a comment\n\nwait 1.5");
            Assert.That(s.Steps.Count, Is.EqualTo(3));
            var c = s.Steps[1];
            Assert.That(c.Line, Is.EqualTo(2));
            Assert.That(c.Args, Is.EqualTo(new[] { ".inspector .code-box", "The reply", "Action, then \"target\"" }));
            Assert.That(c.Options["side"], Is.EqualTo("left"));
            Assert.That(c.Number("width", 0), Is.EqualTo(400));
            Assert.That(s.Steps[2].KnownSeconds, Is.EqualTo(1.5f));
        }

        [Test]
        public void A_quoted_word_with_an_equals_sign_is_an_argument_not_an_option()
        {
            var s = DemoScript.Parse("load a.json\nchapter \"side=left\" \"x=1\"");
            Assert.That(s.Steps[1].Args, Is.EqualTo(new[] { "side=left", "x=1" }));
            Assert.That(s.Steps[1].Options, Is.Empty);
        }

        [TestCase("load a.json\nzoom 3", 2, "not a step")]
        [TestCase("load a.json\nwait", 2, "takes 1 argument")]
        [TestCase("load a.json\nwait soon", 2, "seconds")]
        [TestCase("load a.json\ncallout .x \"t\" \"l\" side=up", 2, "left, right")]
        [TestCase("load a.json\ncallout .x \"t\" \"l\" colour=red", 2, "no option")]
        [TestCase("load a.json\nchapter \"not closed", 2, "not closed")]
        [TestCase("load a.json\nuntil Conquest", 2, "not an event kind")]
        [TestCase("load a.json\nclick \".x:sparkle(1)\"", 2, "not known")]
        [TestCase("load a.json\nmap stonereach", 2, "@province")]
        [TestCase("wait 1\nload a.json", 1, "starts by loading")]
        public void A_mistake_is_named_with_its_line(string text, int line, string says)
        {
            var e = Assert.Throws<DemoScriptException>(() => DemoScript.Parse(text));
            Assert.That(e.Line, Is.EqualTo(line));
            Assert.That(e.Message, Does.Contain(says));
            Assert.That(e.Message, Does.StartWith($"Line {line}:"));
        }

        [Test]
        public void Every_event_kind_until_and_after_take_is_a_replay_event_kind()
        {
            foreach (var k in Enum.GetNames(typeof(ReplayEventKind))) Assert.That(DemoScript.IsEventKind(k), k);
            Assert.That(DemoScript.IsEventKind("3"), Is.False);
            Assert.That(DemoScript.IsEventKind("capture"), Is.False, "names are as the enum spells them");
        }

        [Test]
        public void The_glide_and_the_ease_stay_in_their_bounds()
        {
            Assert.That(DemoDefaults.Ease(0), Is.EqualTo(0));
            Assert.That(DemoDefaults.Ease(1), Is.EqualTo(1));
            Assert.That(DemoDefaults.Ease(0.5f), Is.EqualTo(0.5f).Within(1e-5));
            Assert.That(DemoDefaults.Ease(0.1f), Is.LessThan(0.1f), "it eases in");
            Assert.That(DemoDefaults.GlideSeconds(0), Is.EqualTo(0.55f));
            Assert.That(DemoDefaults.GlideSeconds(5000), Is.EqualTo(1.15f));
        }

        // ---- the selector, on UI elements built here (no panel, so the test says what is shown) ----

        private static VisualElement Screen(out VisualElement guardLink, out VisualElement hiddenTab)
        {
            var root = new VisualElement();
            var chronicle = new VisualElement(); chronicle.AddToClassList("chronicle"); root.Add(chronicle);
            VisualElement Row(string text, string tag, out VisualElement link)
            {
                var row = new VisualElement(); row.AddToClassList("lg");
                row.Add(new Label(text) { name = "text" });
                link = new Label("Find it on the map · open the decision ›"); link.AddToClassList("lg-link"); row.Add(link);
                var t = new Label(tag.ToUpperInvariant()); t.AddToClassList("tag"); t.AddToClassList(tag); row.Add(t);
                chronicle.Add(row);
                return row;
            }
            Row("Valen took Stonereach", "land", out _);
            Row("Merrow holds: a guard stopped its move", "guard", out guardLink);
            Row("Kesh took Harrow", "land", out _);
            var inspector = new VisualElement(); inspector.AddToClassList("inspector"); root.Add(inspector);
            foreach (var tab in new[] { "Answer", "Offered", "Guards" })
            {
                var t = new VisualElement(); t.AddToClassList("ktab"); t.Add(new Label(tab)); inspector.Add(t);
            }
            hiddenTab = new VisualElement(); hiddenTab.AddToClassList("ktab"); hiddenTab.Add(new Label("Secret"));
            hiddenTab.style.display = DisplayStyle.None;
            inspector.Add(hiddenTab);
            return root;
        }

        private static readonly DemoTree Tree = new DemoTree { VisibleTest = e => e.style.display != DisplayStyle.None };

        [Test]
        public void A_selector_finds_by_class_descendant_has_and_text()
        {
            var root = Screen(out var guardLink, out _);
            Assert.That(DemoSelector.Parse(".chronicle .lg:has(.tag.guard) .lg-link").First(root, Tree), Is.SameAs(guardLink));
            var valen = DemoSelector.Parse(".lg'valen'").First(root, Tree);
            Assert.That(valen.Q<Label>("text").text, Is.EqualTo("Valen took Stonereach"), "text is matched without case, anywhere inside");
            Assert.That(DemoSelector.Parse(".lg:has(.tag.land)").All(root, Tree).Count, Is.EqualTo(2));
            Assert.That(DemoSelector.Parse(".lg:has(.tag.land):nth(2) #text").First(root, Tree).text(), Is.EqualTo("Kesh took Harrow"));
            Assert.That(DemoSelector.Parse(".inspector .ktab:has('Offered')").First(root, Tree).Q<Label>().text, Is.EqualTo("Offered"));
            Assert.That(DemoSelector.Parse(".chronicle .ktab").First(root, Tree), Is.Null, "a part must be inside the one before");
        }

        [Test]
        public void A_hidden_element_never_matches_and_hides_what_is_inside_it()
        {
            var root = Screen(out _, out var hidden);
            Assert.That(DemoSelector.Parse(".ktab:has('Secret')").First(root, Tree), Is.Null);
            Assert.That(DemoSelector.Parse("'Secret'").First(root, Tree), Is.Null);
            hidden.style.display = DisplayStyle.Flex;
            Assert.That(DemoSelector.Parse(".ktab:has('Secret')").First(root, Tree), Is.SameAs(hidden));
        }

        [Test]
        public void A_click_on_an_inspector_tab_shows_its_page()
        {
            // A pointer's click event needs a panel to travel through, so only the tab's own path is checked here;
            // the rows' clicks are checked in the running game (the demo's contact sheet).
            var root = Screen(out _, out _);
            bool shown = false;
            var tab = DemoSelector.Parse(".ktab:has('Guards')").First(root, Tree);
            tab.userData = (Action)(() => shown = true);
            DemoTree.Click(tab);
            Assert.That(shown, Is.True);
        }

        [TestCase(".")]
        [TestCase(".lg:has(")]
        [TestCase(".lg'open")]
        [TestCase(".lg:nth(0)")]
        [TestCase("  ")]
        public void A_malformed_selector_is_refused(string s) => Assert.Throws<FormatException>(() => DemoSelector.Parse(s));

        // ---- where a callout goes ----

        private static readonly DemoRect Panel = new DemoRect(0, 0, 1920, 1080);

        [Test]
        public void A_callout_goes_left_of_an_element_on_the_right_and_never_over_it()
        {
            var target = new DemoRect(1420, 300, 480, 120);
            var p = DemoLayout.Place(target, 380, 110, Panel);
            Assert.That(p.Side, Is.EqualTo("left"));
            Assert.That(p.Box.Overlaps(target), Is.False);
            Assert.That(p.Box.Right, Is.EqualTo(target.X - DemoLayout.Gap));
            Assert.That(p.Box.CY, Is.EqualTo(target.CY).Within(0.01), "level with what it points at");
            Assert.That(p.ToX, Is.EqualTo(target.X), "the leader line ends on the element's edge");
        }

        [Test]
        public void With_no_room_on_the_asked_side_a_callout_takes_the_next_that_fits_and_stays_on_screen()
        {
            var target = new DemoRect(20, 1000, 300, 60); // bottom left: no room left or below
            var p = DemoLayout.Place(target, 380, 110, Panel, "left");
            Assert.That(p.Side, Is.EqualTo("right"));
            Assert.That(p.Box.Inside(new DemoRect(DemoLayout.Margin, DemoLayout.Margin, 1920 - 2 * DemoLayout.Margin, 1080 - 2 * DemoLayout.Margin)), Is.True, "slid up to stay on screen");
            Assert.That(p.Box.Overlaps(target), Is.False);
        }

        [Test]
        public void A_callout_keeps_clear_of_another_on_the_screen()
        {
            var target = new DemoRect(1420, 300, 480, 120);
            var first = DemoLayout.Place(target, 380, 110, Panel);
            var second = DemoLayout.Place(new DemoRect(1420, 330, 480, 60), 380, 110, Panel, null, new[] { first.Box.Grow(12) });
            Assert.That(second.Box.Overlaps(first.Box), Is.False);
        }

        // ---- the sample's game has the moments it shows ----

        [Test]
        public void The_sample_recording_has_turn_4s_capture_by_Valen_and_Merrows_answer_held_by_the_grounding_guard()
        {
            var load = DemoScript.Parse(Sample).Steps[0].Arg(0);
            var game = ReplayBuilder.Build(Recording.Parse(File.ReadAllText(Path.Combine(ProjectFolder, load))));
            var turn4 = game.Turns.First(t => t.Turn == 4).Events;
            Assert.That(turn4.Any(e => e.Kind == ReplayEventKind.Capture && e.Country == "valen"), "Valen takes a province on turn 4");
            var held = turn4.Single(e => e.Kind == ReplayEventKind.Hold && e.Country == "merrow");
            Assert.That(held.Decision.GuardsFired, Does.Contain("TargetGroundingGuard"));
            Assert.That(held.Decision.ModelTarget ?? held.Decision.Raw?["reply"]?.ToString(), Does.Contain("stonereach"));
            Assert.That(turn4.Any(e => e.Kind == ReplayEventKind.Taxes && e.Country == "kesh"), "the timeline's first hold is there");
        }
    }

    internal static class DemoTestText
    {
        public static string text(this VisualElement e) => (e as TextElement)?.text;
    }
}
