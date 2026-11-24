using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The army controls on the screen (#20): with several armies chosen the group view offers Merge, enabled only when the
    /// rules allow it and with their reason when not; with one chosen, Split opens a slider at half its soldiers whose
    /// button says what it does and is disabled with the rules' reason. The rules themselves are the build session's
    /// (RuleSession.CanMerge, CanSplit); here they are stubbed, so only the screen is tested.
    /// </summary>
    public class ArmyControlsTests
    {
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
            ui.BuildPanels();
            return ui;
        }

        // A moment where one realm has two armies or more.
        private static (ReplayState s, string owner, List<string> armies) TwoArmies(ReplayGame g)
        {
            foreach (var s in g.AllEvents.Select(e => e.After).Where(s => s != null))
                foreach (var grp in s.Armies.GroupBy(a => a.Owner))
                    if (grp.Count() >= 2) return (s, grp.Key, grp.Select(a => a.Id).ToList());
            Assert.Fail("no realm in the scripted game has two armies at once");
            return default;
        }

        [Test]
        public void Merge_is_offered_with_several_armies_chosen_and_disabled_with_the_rules_reason()
        {
            var g = Game(); var ui = Ui(g);
            var (s, _, armies) = TwoArmies(g);
            ui.ChooseArmies(armies.Take(2));

            ui.CanMergeArmies = ids => ids.Count >= 2 ? null : "choose two or more";
            ui.ShowGroup(s);
            var merge = ui.Root.Q<Button>("merge-armies");
            Assert.That(merge, Is.Not.Null, "the group view offers Merge");
            Assert.That(merge.enabledSelf, Is.True);

            ui.CanMergeArmies = _ => "together they would be 130 soldiers, above 100";
            ui.ShowGroup(s);
            merge = ui.Root.Q<Button>("merge-armies");
            Assert.That(merge.enabledSelf, Is.False, "the rules say no");
            Assert.That(merge.tooltip, Does.Contain("130 soldiers"), "and say why");

            // Outside your council there is nothing to merge with.
            ui.CanMergeArmies = null;
            ui.ShowGroup(s);
            Assert.That(ui.Root.Q<Button>("merge-armies").enabledSelf, Is.False);
        }

        [Test]
        public void Split_opens_a_slider_at_half_and_its_button_follows_the_rules()
        {
            var g = Game(); var ui = Ui(g);
            var (s, _, armies) = TwoArmies(g);
            var a = s.Army(armies[0]);
            ui.ChooseArmies(new[] { a.Id });
            ui.CanSplitArmy = (id, n) => n <= a.Soldiers / 2 ? null : "the rest would be too few to hold";
            ui.ShowArmy(a.Id, s, g.AllEvents);
            ui.AddSplitControl(a.Id, s);
            Assert.That(ui.SplitValue, Is.EqualTo(-1), "closed until opened");

            ui.ToggleSplit(s);
            Assert.That(ui.SplitValue, Is.EqualTo(System.Math.Max(1, a.Soldiers / 2)), "half by default");
            var go = ui.Root.Q<Button>("split-army");
            Assert.That(go.text, Does.StartWith($"Split off {a.Soldiers / 2}"));
            Assert.That(go.enabledSelf, Is.True);

            // The rules refuse this many: the button says why and cannot be pressed.
            ui.CanSplitArmy = (id, n) => "the rest would be too few to hold";
            ui.ToggleSplit(s); ui.ToggleSplit(s);
            go = ui.Root.Q<Button>("split-army");
            Assert.That(go.enabledSelf, Is.False);
            Assert.That(go.tooltip, Does.Contain("too few"));
        }
    }
}
