using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// Nothing is cut off at the bottom (#20): a window over the map is never taller than the room below its top
    /// edge at any of the sizes the game is played at, every such window is fitted and scrolls inside, and a
    /// scrolling list knows when rows lie below what it shows.
    /// </summary>
    public class LayoutTests
    {
        // The game-view sizes of the audit: 1080p, 16:10, 1440p, a 1080p laptop at 125%, the two small ones, and the lead's
        // own 16:10 laptop panel at about 150% (1707 x 1067 from 2560 x 1600; 2560 x 1600 itself).
        private static readonly (int w, int h)[] Sizes = { (1920, 1080), (1920, 1200), (2560, 1440), (1536, 864), (1366, 768), (1280, 720), (1707, 1067), (2560, 1600) };
        // Where a window starts: under the top bar (84), and a card a fifth of the way down the screen.
        private const float UnderTopBar = 84;

        [Test]
        public void A_window_under_the_top_bar_ends_above_the_screens_bottom_margin_at_every_size()
        {
            foreach (var (w, h) in Sizes)
            {
                float screen = h / UiScale.For(w, h); // the layout's height: the panel is drawn at the UI scale
                foreach (float top in new[] { UnderTopBar, screen * 0.18f, screen * 0.22f })
                {
                    float max = ScreenUI.WindowMaxHeight(screen, top);
                    Assert.That(top + max, Is.LessThanOrEqualTo(screen - ScreenUI.BottomMargin + 0.01f), $"{w}x{h}: a window at {top:0} must end {ScreenUI.BottomMargin} above the bottom");
                    Assert.That(max, Is.GreaterThanOrEqualTo(ScreenUI.WindowFloor), $"{w}x{h}: and still be a window");
                }
            }
        }

        [Test]
        public void A_share_of_the_screen_is_not_the_room_below_the_top_bar()
        {
            // What the stylesheet said before: 90% of the screen for the inspector below 1600 wide. At 1366 x 768
            // that is 691 from y 84, 7 past the bottom; the room is 676.
            Assert.That(UnderTopBar + 0.90f * 768, Is.GreaterThan(768));
            Assert.That(ScreenUI.WindowMaxHeight(768, UnderTopBar), Is.EqualTo(676).Within(0.01));
            Assert.That(ScreenUI.WindowMaxHeight(720, UnderTopBar), Is.EqualTo(628).Within(0.01));
            Assert.That(ScreenUI.WindowMaxHeight(1080, UnderTopBar), Is.EqualTo(988).Within(0.01));
        }

        [Test]
        public void A_tiny_screen_keeps_a_window_tall_enough_to_scroll_in()
        {
            Assert.That(ScreenUI.WindowMaxHeight(200, UnderTopBar), Is.EqualTo(ScreenUI.WindowFloor));
        }

        [TestCase(600, 240, 0, true, TestName = "More below: a long list at its top")]
        [TestCase(600, 240, 200, true, TestName = "More below: scrolled part of the way")]
        [TestCase(600, 240, 360, false, TestName = "More below: not at the list's end")]
        [TestCase(600, 240, 359.5f, false, TestName = "More below: not within a pixel of the end")]
        [TestCase(200, 240, 0, false, TestName = "More below: not when everything fits")]
        [TestCase(240, 240, 0, false, TestName = "More below: not when it fits exactly")]
        public void A_list_knows_when_rows_lie_below(float content, float viewport, float offset, bool more)
        {
            Assert.That(ScreenUI.MoreBelow(content, viewport, offset), Is.EqualTo(more));
        }

        [Test]
        public void A_long_counsel_folds_to_three_whole_lines()
        {
            // 652 characters ran to ten lines of 16 px in the live game (the tech lead, 17 November).
            Assert.That(ScreenUI.FoldHeight(160, 16), Is.EqualTo(48), "three lines, each whole");
            Assert.That(ScreenUI.FoldHeight(160, 16).Value % 16, Is.EqualTo(0), "never half a line");
        }

        [TestCase(48, 16, TestName = "Fold: not three lines")]
        [TestCase(54, 16, TestName = "Fold: not three lines and a sliver")]
        [TestCase(20, 16, TestName = "Fold: not one line")]
        public void A_short_text_never_folds(float height, float line)
        {
            Assert.That(ScreenUI.FoldHeight(height, line), Is.Null, "a \"more\" that hides half a line or nothing is worse than none");
        }

        [Test]
        public void A_fold_folds_four_lines_and_not_before_layout()
        {
            Assert.That(ScreenUI.FoldHeight(64, 16), Is.EqualTo(48));
            Assert.That(ScreenUI.FoldHeight(float.NaN, 16), Is.Null, "before the label is laid out");
            Assert.That(ScreenUI.FoldHeight(64, 0), Is.Null);
        }

        [TestCase(0, 1800, 1700, 0, TestName = "Top bar: full form while the stats fit")]
        [TestCase(0, 1700, 1700, 1, TestName = "Top bar: sub-labels go before anything touches")]
        [TestCase(1, 1400, 1500, 2, TestName = "Top bar: still too wide bare, the squeezed step")]
        [TestCase(2, 1300, 1500, 3, TestName = "Top bar: still too wide squeezed, the icons go")]
        [TestCase(3, 800, 1500, 3, TestName = "Top bar: never past the last step")]
        [TestCase(1, 1800, 1600, 1, TestName = "Top bar: a step is kept while the width stays")]
        public void The_top_bar_steps_down_before_anything_touches(int step, float room, float need, int next)
        {
            Assert.That(ScreenUI.StatsStep(step, room, need), Is.EqualTo(next));
        }

        [Test]
        public void An_opened_feed_entry_is_scrolled_into_view_whole()
        {
            // A 200 px feed scrolled to 100: an entry at 250 to 330 ends below its foot (300), so the feed scrolls 30.
            Assert.That(ScreenUI.EntryScroll(250, 80, 200, 100), Is.EqualTo(130));
            Assert.That(ScreenUI.EntryScroll(150, 80, 200, 100), Is.EqualTo(100), "already whole: the feed stays put");
            Assert.That(ScreenUI.EntryScroll(60, 80, 200, 100), Is.EqualTo(60), "above the top: its top at the top");
            Assert.That(ScreenUI.EntryScroll(250, 320, 200, 100), Is.EqualTo(250), "taller than the feed: from its top, the rest by scrolling");
        }

        private static ReplayGame game;
        private static ReplayGame Game()
        {
            if (game != null) return game;
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = ReplayDirector.WithArmySettings(setup.BuildRules(map), true, true, true, true);
            return game = ReplayBuilder.Build(Recording.Parse(ScriptedWorldGame.Play(setup, map, rules).ToJson()), map, rules);
        }

        private static ScreenUI Ui(ReplayGame g)
        {
            var ui = new ScreenUI(new VisualElement(), UnityEngine.ScriptableObject.CreateInstance<StyleSheet>(), null,
                g.CountryIds, g.ProvinceIds, new Dictionary<string, RecordedPlayer>(), "test");
            ui.Queries = new RealmQueries(g);
            ui.BuildPanels();
            return ui;
        }

        [Test]
        public void Every_window_over_the_map_is_fitted_to_the_screen_and_scrolls_inside()
        {
            var g = Game(); var ui = Ui(g);
            ui.ShowCard(DecisionCard.From(g.AllEvents.First(x => x.Decision != null && !x.Decision.IsEnvoy)));
            ui.ShowDiplomacy(g.Initial, 1, Enumerable.Empty<ReplayEvent>());
            var battle = g.AllEvents.FirstOrDefault(x => x.Kind == ReplayEventKind.Battle && x.Battle != null);
            Assert.That(battle, Is.Not.Null, "the scripted game fights a battle");
            ui.ShowBattle(battle);
            ui.KeepLayout(); // the pass the screen runs every 250 ms, whichever code opened the window
            var windows = new List<VisualElement>();
            foreach (string cls in new[] { "inspector", "diplomacy", "event-card" }) ui.Root.Query(className: cls).ForEach(windows.Add);
            Assert.That(windows.Count, Is.EqualTo(3));
            foreach (var w in windows)
            {
                string name = string.Join(".", w.GetClasses());
                Assert.That(w.ClassListContains("window"), Is.True, $"{name} is kept inside the screen");
                Assert.That(w.Q<ScrollView>(), Is.Not.Null, $"{name} scrolls inside when it is too tall");
            }
        }

        [Test]
        public void Every_scrolling_list_gets_its_shade_once()
        {
            var g = Game(); var ui = Ui(g);
            ui.ShowState(g.Initial);
            ui.KeepLayout();
            ui.ShowState(g.Initial);
            ui.KeepLayout(); // twice: still one shade each
            int lists = 0;
            ui.Root.Query<ScrollView>().ForEach(v =>
            {
                lists++;
                Assert.That(v.contentViewport.Query(className: "scroll-shade").ToList().Count, Is.EqualTo(1), "one shade a list");
            });
            Assert.That(lists, Is.GreaterThanOrEqualTo(4), "the ledger, the chronicle, the outliner, the inspector");
        }
    }
}
