using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Editor.Evaluation;
using Agenerela.Editor.UI;
using Agenerela.Evaluation;
using Agenerela.Tests.UI;
using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// The Prompt Set window's trees (#160): built from their UXML on the #167 kit and read
    /// directly, with no window, so what #158 and #159 could only check by hand is a test. The one
    /// test that needs layout opens a window, and is ignored in a run with no graphics device.
    /// </summary>
    public sealed class PromptSetWindowTests
    {
        private EvalFixtures fixture;
        private PromptSetUi ui;
        private AgentProfile profile;

        [SetUp]
        public void SetUp()
        {
            fixture = new EvalFixtures();
            ui = new PromptSetUi(PromptSetUi.Load());
            profile = Fixtures.VillageGuardProfile();
        }

        [TearDown]
        public void TearDown()
        {
            EditorUiFixtures.CloseAll();
            fixture.Dispose();
            Fixtures.DestroyCreated();
        }

        // ---- The gate: tabs greyed out until a set and a profile are chosen ------------------------

        [Test]
        public void TheTabsAreGreyedOutAndTheStepsShownUntilASetAndAProfileAreChosen()
        {
            ui.Show(new PromptSetUi.State());

            Assert.That(Tabs().Select(t => t.enabledSelf), Is.All.False);
            Assert.That(Shown(PromptSetUi.StartName), Is.True);
            Assert.That(ModeLine(), Is.EqualTo("The tabs open once a set and a profile are chosen: the steps are below."));
            Assert.That(Hosts().Select(Shown), Is.All.False, "No view shows before the window is ready.");

            ui.Show(new PromptSetUi.State { SetLoaded = true, SetSummary = "Assets/Evaluation/agent.json: 5 prompts in 2 categories." });

            Assert.That(Tabs().Select(t => t.enabledSelf), Is.All.False, "A set alone is not enough.");
            Assert.That(ModeLine(), Does.Contain("a profile is"));
            Assert.That(ui.Root.Find<VisualElement>("step-1").ClassListContains(PromptSetUi.StepDoneClass), Is.True);
            Assert.That(ui.Root.Find<Label>("step-1-mark").text, Is.EqualTo("✓"));
            Assert.That(ui.Root.Find<VisualElement>("step-2").ClassListContains(PromptSetUi.StepDoneClass), Is.False);

            ui.Show(new PromptSetUi.State { SetLoaded = true, Profile = profile });

            Assert.That(Tabs().Select(t => t.enabledSelf), Is.All.True);
            Assert.That(Shown(PromptSetUi.StartName), Is.False);
        }

        [Test]
        public void TheStartButtonIsThePrimaryOneAndGreyedOutWithNoSetToOpen()
        {
            ui.Show(new PromptSetUi.State());

            var open = ui.Root.Find<Button>(PromptSetUi.OpenProjectSetName);
            Assert.That(open.ClassListContains(AgenerelaButtons.PrimaryClass), Is.True);
            Assert.That(open.enabledSelf, Is.False);
            Assert.That(ui.Root.Find<Label>(PromptSetUi.FoundSetsName).text, Is.EqualTo("No agent.json under Assets/ yet."));
            Assert.That(ui.Root.Find<Button>(PromptSetUi.NewSetName).enabledSelf, Is.False, "A new set needs the profile first.");

            ui.Show(new PromptSetUi.State { FoundSets = new[] { "Assets/Evaluation/agent.json" }, Profile = profile });

            Assert.That(open.enabledSelf, Is.True);
            Assert.That(ui.Root.Find<Label>(PromptSetUi.FoundSetsName).text, Is.EqualTo("Assets/Evaluation/agent.json"));
            Assert.That(ui.Root.Find<Button>(PromptSetUi.NewSetName).enabledSelf, Is.True);
        }

        // ---- Tab states ----------------------------------------------------------------------------

        [Test]
        public void TheChosenTabIsTheOneOnAndItsViewTheOneShown()
        {
            foreach (PromptSetMode mode in System.Enum.GetValues(typeof(PromptSetMode)))
            {
                ui.Show(Ready(mode));

                Assert.That(Tabs().Select(t => t.value), Is.EqualTo(Enumerable.Range(0, 3).Select(i => i == (int)mode)), $"Tabs with {mode} chosen.");
                Assert.That(Hosts().Select(Shown), Is.EqualTo(Enumerable.Range(0, 3).Select(i => i == (int)mode)), $"Views with {mode} chosen.");
                Assert.That(ModeLine(), Is.EqualTo($"{mode}: {PromptSetUi.ModeDescriptions[(int)mode]}."));
            }
        }

        [Test]
        public void ClickingTheTabOnDoesNotTurnItOff()
        {
            ui.Show(Ready(PromptSetMode.Label));
            PromptSetMode? asked = null;
            ui.ModeChanged += m => asked = m;
            var label = Tabs()[(int)PromptSetMode.Label];

            // A change event reaches an element only on a panel.
            using (EditorUiFixtures.Host(ui.Root))
            {
                label.value = false;

                Assert.That(label.value, Is.True);
                Assert.That(asked, Is.Null);

                Tabs()[(int)PromptSetMode.Agreement].value = true;

                Assert.That(asked, Is.EqualTo(PromptSetMode.Agreement));
            }
        }

        [Test]
        public void LabelAndAgreementWaitForWritesUnsavedEdits()
        {
            var state = Ready(PromptSetMode.Label);
            state.WriteUnsaved = true;
            ui.Show(state);

            Assert.That(Shown(PromptSetUi.SaveFirstName), Is.True);
            Assert.That(ui.Root.Find<VisualElement>(PromptSetUi.SaveFirstName).Q<ProblemBox>().HasFix, Is.True, "The message carries the Save button.");
            Assert.That(Hosts().Select(Shown), Is.All.False);

            state.Mode = PromptSetMode.Write;
            ui.Show(state);

            Assert.That(Shown(PromptSetUi.SaveFirstName), Is.False);
            Assert.That(Shown(PromptSetUi.WriteHostName), Is.True, "Write is where the edits are saved, so it stays open.");
        }

        [Test]
        public void AProfileThatDisagreesWithTheSetIsShownAndNeverFixed()
        {
            var state = Ready(PromptSetMode.Write);
            state.ProfileDifferences = new[] { "The profile has guard_post, which the set does not declare" };
            ui.Show(state);

            var problem = ui.Root.Find<VisualElement>(PromptSetUi.ProfileProblemsName).Q<ProblemBox>();
            Assert.That(problem.Message, Does.StartWith("The profile has guard_post").And.Contain("Nothing is changed here"));
            Assert.That(problem.HasFix, Is.False);
        }

        [Test]
        public void ADraftThatCannotBeAppliedOffersOnlyDiscard()
        {
            var state = Ready(PromptSetMode.Write);
            state.Drafts = new[]
            {
                new PromptSetUi.WaitingDraft("situation", "its file changed on disk since you edited it", true),
                new PromptSetUi.WaitingDraft("gone", "the set no longer has this category", false),
            };
            ui.Show(state);

            var boxes = ui.Root.Find<VisualElement>(PromptSetUi.DraftsName).Query<ProblemBox>().ToList();
            Assert.That(boxes.Count, Is.EqualTo(2));
            Assert.That(boxes[0].HasFix, Is.True, "Keep my draft");
            Assert.That(boxes[1].HasFix, Is.False);
            Assert.That(boxes.Select(b => b.Query<Button>().ToList().Last().text), Is.All.EqualTo("Discard it"));
        }

        [Test]
        public void TheCohortLineShowsOnlyAboveLabelAndItsToggleAsksForTheWholeSet()
        {
            var state = Ready(PromptSetMode.Label);
            state.CohortLine = "Labelling the 3 prompts of the agreement cohort (#148), from agreement-cohort.json.";
            ui.Show(state);
            bool? whole = null;
            ui.WholeSetChanged += w => whole = w;

            Assert.That(Shown(PromptSetUi.CohortName), Is.True);
            Assert.That(ui.Root.Find<Label>(PromptSetUi.CohortLineName).text, Is.EqualTo(state.CohortLine));

            using (EditorUiFixtures.Host(ui.Root))
            {
                ui.Root.Find<Toggle>(PromptSetUi.WholeSetName).value = true;
            }

            Assert.That(whole, Is.True);

            state.Mode = PromptSetMode.Write;
            ui.Show(state);
            Assert.That(Shown(PromptSetUi.CohortName), Is.False, "Only Label asks the cohort.");

            state.Mode = PromptSetMode.Label;
            state.CohortLine = null;
            ui.Show(state);
            Assert.That(Shown(PromptSetUi.CohortName), Is.False, "No cohort beside the set, no line.");
        }

        // ---- Label: progress, keys and the blind guarantee -------------------------------------------

        [Test]
        public void TheProgressBarSaysHowManyAreLeftAndFillsAsAnswersAreSaved()
        {
            var set = fixture.Load();
            var recorded = new List<LabelAnswer>();
            var view = LabelView(set, "Bo", recorded);
            var bar = view.Root.Find<ProgressBar>(PromptSetLabelView.ProgressName);

            Assert.That(bar.title, Is.EqualTo("5 of 5 left to label"));
            Assert.That(bar.value, Is.EqualTo(0));
            Assert.That(bar.highValue, Is.EqualTo(5));

            view.Save();
            view.Skip();

            Assert.That(bar.title, Is.EqualTo("4 of 5 left to label"), "A skipped prompt is still left.");
            Assert.That(bar.value, Is.EqualTo(1));
            Assert.That(recorded.Count, Is.EqualTo(1));

            for (int i = 0; i < 3; i++) view.Save();

            Assert.That(Shown(view.Root, PromptSetLabelView.DoneName), Is.True);
            Assert.That(Shown(view.Root, PromptSetLabelView.PromptName), Is.False);
            Assert.That(view.Root.Find<Label>(PromptSetLabelView.DoneName).text, Is.EqualTo(view.Progress.NothingLeft("Bo")));
        }

        [Test]
        public void NumberKeysPickAnActionAndTPicksTheNextTarget()
        {
            var set = fixture.Load();
            var recorded = new List<LabelAnswer>();
            var view = LabelView(set, "Bo", recorded);
            using var host = EditorUiFixtures.Host(view.Root);
            var actions = view.Root.Find<DropdownField>(PromptSetLabelView.ActionName);
            int moveTo = actions.choices.IndexOf("move_to") + 1;
            Assume.That(moveTo, Is.GreaterThan(0), "Every fixture prompt's state allows move_to.");

            Key(view, (KeyCode)((int)KeyCode.Alpha0 + moveTo));

            Assert.That(view.Action, Is.EqualTo("move_to"));
            Assert.That(actions.value, Is.EqualTo("move_to"));
            string first = view.Target;

            Key(view, KeyCode.T);

            Assert.That(view.Target, Is.Not.EqualTo(first));
            Assert.That(view.Root.Find<DropdownField>(PromptSetLabelView.TargetName).value, Is.EqualTo(view.Target));

            string chosen = view.Target;
            Key(view, KeyCode.Return);

            Assert.That(recorded, Is.EqualTo(new[] { new LabelAnswer("move_to", chosen) }));

            Key(view, KeyCode.S);

            Assert.That(recorded.Count, Is.EqualTo(1), "S skips: nothing is recorded.");
            Assert.That(view.Progress.Skipped, Is.EqualTo(1));
        }

        [Test]
        public void TheLabelTreeShowsNoWordOfAnyAnswerNoteOrOtherPerson()
        {
            // The fixture's set: Maxim wrote every prompt, Ana settled one with him, and a note
            // names the answer. Bo labels; nothing Bo can read in the tree may give any of it away.
            var set = fixture.Load();
            fixture.WriteLabels("Ana", "command_with_target", "{ \"target_001\": \"move_to/bridge\" }");
            var view = LabelView(set, "Bo", new List<LabelAnswer>(), set.LoadLabels());
            var secrets = new[] { "SECRET_NOTE", "Maxim", "Ana", "command_with_target", "situation", "expected", "settled" };

            int shown = 0;
            while (Shown(view.Root, PromptSetLabelView.PromptName))
            {
                string seen = Everything(view.Root);
                foreach (string secret in secrets)
                {
                    Assert.That(seen, Does.Not.Contain(secret), $"The Label view shows '{secret}':\n{seen}");
                }

                shown++;
                view.Skip();
            }

            Assert.That(shown, Is.EqualTo(5), "Every prompt Bo is asked was read.");
        }

        [Test]
        public void AMultiTurnPromptShowsItsEarlierTurnsAboveNow()
        {
            var set = fixture.Load();
            foreach (var prompt in set.Prompts)
            {
                prompt.History = new List<HistoryTurn>
                {
                    new HistoryTurn { Stimulus = "Follow me.", Action = "follow_player", Target = TargetRegistry.NoTarget, Statement = "Right behind you." },
                };
            }

            var view = LabelView(set, "Bo", new List<LabelAnswer>());
            var history = view.Root.Find<VisualElement>("label-history");
            string shown = Everything(history);

            Assert.That(Shown(history), Is.True);
            Assert.That(shown, Does.Contain("EARLIER TURNS").And.Contain("Player: Follow me.").And.Contain("→ follow_player/no_target — \"Right behind you.\"").And.Contain("NOW"));
            Assert.That(shown.IndexOf("NOW"), Is.GreaterThan(shown.IndexOf("Follow me.")), "Now comes after the earlier turns.");
        }

        [Test]
        public void ASinglePromptShowsNoEarlierTurns()
        {
            var set = fixture.Load();
            var view = LabelView(set, "Bo", new List<LabelAnswer>());

            Assert.That(Shown(view.Root, "label-history"), Is.False);
        }

        // ---- Agreement -------------------------------------------------------------------------------

        [Test]
        public void TheCategoryTableSortsByAnyColumnAndBack()
        {
            var set = fixture.Load();
            fixture.WriteLabels("Ana", "command_with_target", "{ \"target_001\": \"move_to/bridge\", \"target_002\": \"none/no_target\" }");
            var view = new PromptSetAgreementView(set, set.LoadLabels(), () => { });
            Assert.That(view.Root.Find<MultiColumnListView>(PromptSetAgreementView.CategoriesName).columns.Select(c => c.name),
                Is.EqualTo(PromptSetAgreementView.Columns));
            Assert.That(view.Rows.Select(r => r.Id), Is.EqualTo(new[] { "command_with_target", "situation" }), "The set's order first.");

            view.Sort(new SortColumnDescription("prompts", SortDirection.Ascending));

            Assert.That(view.Rows.Select(r => r.Id), Is.EqualTo(new[] { "situation", "command_with_target" }));

            view.Sort(new SortColumnDescription("split", SortDirection.Descending));

            Assert.That(view.Rows.First().Id, Is.EqualTo("command_with_target"));
            Assert.That(view.Rows.First().Split, Is.EqualTo(1), "target_002: attack_target from Maxim, none from Ana.");

            view.Sort(null);

            Assert.That(view.Rows.Select(r => r.Id), Is.EqualTo(new[] { "command_with_target", "situation" }));
        }

        [Test]
        public void RecordAgreedAnswersShowsOnlyWhenAUnanimousPromptIsUnrecorded()
        {
            var set = fixture.Load();
            var none = new PromptSetAgreementView(set, set.LoadLabels(), () => { });
            Assert.That(Shown(none.Root, PromptSetAgreementView.RecordAgreedName), Is.False);

            fixture.WriteLabels("Ana", "command_with_target", "{ \"target_001\": \"move_to/bridge\" }");
            var view = new PromptSetAgreementView(set, set.LoadLabels(), () => { });
            var button = view.Root.Find<Button>(PromptSetAgreementView.RecordAgreedName);

            Assert.That(Shown(button), Is.True);
            Assert.That(button.text, Is.EqualTo("Record agreed answers (1)"));
            Assert.That(view.Root.Find<Label>(PromptSetAgreementView.PairwiseName).text, Is.Not.Empty);
        }

        [Test]
        public void ASplitOffersEachAnswerAndAnOpenChoiceOnlyForOneActionWithTwoTargets()
        {
            var set = fixture.Load();
            fixture.WriteLabels("Ana", "command_with_target", "{ \"target_001\": \"move_to/docks\", \"target_002\": \"none/no_target\" }");
            var view = new PromptSetAgreementView(set, set.LoadLabels(), () => { });

            var sameAction = view.Root.Find<VisualElement>("split-target_001").Query<Button>().ToList();
            var twoActions = view.Root.Find<VisualElement>("split-target_002").Query<Button>().ToList();

            Assert.That(sameAction.Select(b => b.text), Is.EquivalentTo(new[] { "Settle as move_to/bridge", "Settle as move_to/docks", "Open choice" }));
            Assert.That(sameAction.Single(b => b.text == "Open choice").enabledSelf, Is.True);
            Assert.That(twoActions.Single(b => b.text == "Open choice").enabledSelf, Is.False);
        }

        // ---- 640 px: nothing clips (#159) ------------------------------------------------------------

        [UnityTest]
        public IEnumerator NothingInTheToolbarClipsAtTheWindowsMinimumWidth()
        {
            ui.Show(Ready(PromptSetMode.Write));
            using (EditorUiFixtures.Host(ui.Root, EditorUiFixtures.DefaultSize))
            {
                yield return EditorUiFixtures.Until(() => ui.Root.layout.width > 0, "the window to lay out");

                var toolbar = ui.Root.Find<Toolbar>(PromptSetUi.ToolbarName);
                float right = toolbar.worldBound.xMax + 0.5f;
                var reload = ui.Root.Find<ToolbarButton>(PromptSetUi.ReloadName);
                foreach (var child in toolbar.Children())
                {
                    Assert.That(child.worldBound.xMax, Is.LessThanOrEqualTo(right), $"{(string.IsNullOrEmpty(child.name) ? string.Join(" ", child.Query<TextElement>().ToList().Select(t => t.text)) : child.name)} runs past the toolbar's edge at 640 px.");
                }

                Assert.That(reload.worldBound.width, Is.GreaterThan(30f), "Reload keeps its whole width.");
                foreach (var label in toolbar.Query<Label>().ToList().Where(l => l.ClassListContains("ps-toolbar__label")))
                {
                    var field = label.parent.ElementAt(1);
                    Assert.That(label.worldBound.center.y, Is.EqualTo(field.worldBound.center.y).Within(3f), $"'{label.text}' stays on its field's line.");
                }
            }
        }

        // ---- Helpers ----------------------------------------------------------------------------------

        private PromptSetUi.State Ready(PromptSetMode mode) => new PromptSetUi.State { SetLoaded = true, Profile = profile, Mode = mode };

        private ToolbarToggle[] Tabs() => PromptSetUi.TabNames.Select(n => ui.Root.Find<ToolbarToggle>(n)).ToArray();

        private VisualElement[] Hosts() => new[] { PromptSetUi.WriteHostName, PromptSetUi.LabelHostName, PromptSetUi.AgreementHostName }
            .Select(n => ui.Root.Find<VisualElement>(n)).ToArray();

        private string ModeLine() => ui.Root.Find<Label>(PromptSetUi.ModeLineName).text;

        private bool Shown(string name) => Shown(ui.Root, name);

        private static bool Shown(VisualElement element) => element.style.display != DisplayStyle.None;

        private static bool Shown(VisualElement root, string name) => Shown(root.Find<VisualElement>(name));

        private static PromptSetLabelView LabelView(PromptSet set, string annotator, List<LabelAnswer> recorded, IReadOnlyList<AnnotatorLabels> labels = null)
        {
            return new PromptSetLabelView(annotator, BlindPrompt.Queue(set, labels ?? set.LoadLabels(), annotator), LabelChoices.From(set),
                (prompt, answer) =>
                {
                    recorded.Add(answer);
                    return true;
                },
                LabelProgress.TotalFor(set, annotator));
        }

        private static void Key(PromptSetLabelView view, KeyCode key)
        {
            using (var down = KeyDownEvent.GetPooled('\0', key, EventModifiers.None))
            {
                down.target = view.Root;
                view.Root.SendEvent(down);
            }
        }

        // Every word the tree shows or would show: text, tooltips, and each dropdown's choices.
        private static string Everything(VisualElement root)
        {
            var words = new List<string>();
            root.Query<VisualElement>().ForEach(e =>
            {
                if (e.style.display == DisplayStyle.None) return;
                if (e is TextElement text) words.Add(text.text);
                if (!string.IsNullOrEmpty(e.tooltip)) words.Add(e.tooltip);
                if (e is DropdownField dropdown) words.AddRange(dropdown.choices);
            });
            return string.Join("\n", words);
        }
    }
}
