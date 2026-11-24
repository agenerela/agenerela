using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Editor.UI;
using Agenerela.Evaluation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// Write mode: the prompts grouped by category on the left, one prompt's editor on the right.
    /// The expected answer is picked from dropdowns, and the rules a prompt breaks show under it as
    /// you type. Changes stay in memory until saved, one category file at a time.
    /// </summary>
    /// <remarks>
    /// Its tree is a fresh copy of <c>PromptSetWriteView.uxml</c>, made for this view alone, so a
    /// view made after a reload never shares a callback with the one before it. Every field is
    /// written back only when a person edits it, so selecting a prompt never tidies it: what is
    /// not touched saves exactly as it was read.
    /// </remarks>
    internal sealed class PromptSetWriteView
    {
        internal const string LayoutPath = PromptSetUi.Folder + "PromptSetWriteView.uxml";

        // Names a test reaches. The rest are in the UXML.
        internal const string PromptListName = "prompt-list", EditorName = "editor", NothingSelectedName = "nothing-selected",
            SearchName = "search", ChecksName = "checks", SaveName = "save", SaveStateName = "save-state";

        /// <summary>A prompt row in the list; the selected one also carries <see cref="SelectedClass"/>.</summary>
        internal const string PromptClass = "ps-prompt", SelectedClass = "ps-prompt--selected", BadgeClass = "ps-prompt__badge";

        private readonly PromptSet set;
        private readonly Func<AgentProfile> profile;
        private readonly Func<string> you;
        private readonly Action onFilesWritten;
        private readonly Action onEdited;
        private readonly HashSet<PromptCategory> changed = new HashSet<PromptCategory>();

        // Each category file's fingerprint as this view last read or wrote it, so a draft kept
        // through a domain reload can tell whether someone changed the file since. Taken when the
        // view is made, right after the set loads, not at the first edit: by then the file may
        // already hold a teammate's newer prompts that the window never read.
        private readonly Dictionary<PromptCategory, string> baselines = new Dictionary<PromptCategory, string>();
        private readonly HashSet<string> open = new HashSet<string>();

        private IReadOnlyList<AnnotatorLabels> labels = Array.Empty<AnnotatorLabels>();
        private EvalPrompt selected;
        private PromptCategory selectedCategory;

        private VisualElement root;
        private ScrollView list;
        private ToolbarSearchField search;
        private TextField newCategory, idField, labelField, stimulus, observations, newStateKey, note, labelledBy;
        private ToolbarMenu knownLabels;
        private DropdownField action;
        private Toggle checkTarget;
        private Button addCategory, openChoice, save;
        private IVisualElementScheduledItem listRefresh;

        internal PromptSetWriteView(PromptSet set, Func<AgentProfile> profile, Func<string> you, Action onFilesWritten, Action onEdited = null)
        {
            this.set = set;
            this.profile = profile;
            this.you = you;
            this.onFilesWritten = onFilesWritten;
            this.onEdited = onEdited ?? (() => { });
            foreach (var category in set.Categories)
            {
                baselines[category] = PromptSetWindow.Fingerprint(set, category);
            }
        }

        internal bool HasUnsavedChanges => changed.Count > 0;

        /// <summary>The categories edited since they were last saved.</summary>
        internal IEnumerable<PromptCategory> Changed => changed;

        /// <summary>The prompt open in the editor, or null.</summary>
        internal EvalPrompt Selected => selected;

        /// <summary>The fingerprint of <paramref name="category"/>'s file from before its unsaved edits.</summary>
        internal string BaselineOf(PromptCategory category) =>
            baselines.TryGetValue(category, out string baseline) ? baseline : PromptSetWindow.Fingerprint(set, category);

        /// <summary>
        /// Marks <paramref name="category"/> as edited. <paramref name="baseline"/> is the
        /// fingerprint of the file the edits were made from; null keeps the one this view read.
        /// </summary>
        internal void MarkChanged(PromptCategory category, string baseline = null)
        {
            changed.Add(category);
            if (baseline != null)
            {
                baselines[category] = baseline;
            }
            else if (!baselines.ContainsKey(category))
            {
                // A category added in this view: its file does not exist yet.
                baselines[category] = PromptSetWindow.Fingerprint(set, category);
            }

            if (root != null)
            {
                ShowSaveBar();
                ScheduleList();
            }
        }

        internal void SetLabels(IReadOnlyList<AnnotatorLabels> files)
        {
            labels = files;
            if (root != null && selected != null)
            {
                ShowLabelledWarning();
            }
        }

        internal void SaveAll()
        {
            foreach (var category in changed)
            {
                set.SaveCategory(category);
                baselines[category] = PromptSetWindow.Fingerprint(set, category);
            }

            changed.Clear();
            if (root != null)
            {
                ShowSaveBar();
                ShowList();
            }

            onFilesWritten();
        }

        /// <summary>The view's tree, made the first time it is asked for.</summary>
        internal VisualElement Root => root ?? Build();

        /// <summary>Shows everything again: after the profile or your name changes, say.</summary>
        internal void Refresh()
        {
            if (root == null)
            {
                return;
            }

            ShowList();
            ShowEditor();
            ShowSaveBar();
        }

        /// <summary>Opens <paramref name="prompt"/> in the editor, as a click on its row does.</summary>
        internal void Select(EvalPrompt prompt, PromptCategory category)
        {
            selected = prompt;
            selectedCategory = category;
            if (category != null)
            {
                open.Add(category.Id);
            }

            if (root != null)
            {
                newStateKey.SetValueWithoutNotify("");
                ShowList();
                ShowEditor();
            }
        }

        private VisualElement Build()
        {
            root = PromptSetUi.Load(LayoutPath);
            list = root.Q<ScrollView>(PromptListName);

            search = root.Q<ToolbarSearchField>(SearchName);
            search.RegisterValueChangedCallback(_ => ShowList());

            newCategory = root.Q<TextField>("new-category");
            newCategory.RegisterValueChangedCallback(_ => ShowNewCategory());
            addCategory = root.Q<Button>("add-category");
            addCategory.clicked += AddCategory;

            idField = root.Q<TextField>("prompt-id");
            idField.isDelayed = true;
            idField.RegisterValueChangedCallback(e => Edit(p => p.Id = e.newValue, true));

            labelField = root.Q<TextField>("prompt-label");
            labelField.RegisterValueChangedCallback(e => Edit(p => p.Label = e.newValue));
            knownLabels = root.Q<ToolbarMenu>("known-labels");

            stimulus = root.Q<TextField>("stimulus");
            stimulus.RegisterValueChangedCallback(e => Edit(p => p.Stimulus = e.newValue, true));

            observations = root.Q<TextField>("observations");
            observations.RegisterValueChangedCallback(e =>
                Edit(p => p.Observations = e.newValue.Split('\n').Select(o => o.Trim()).Where(o => o.Length > 0).ToList()));

            newStateKey = root.Q<TextField>("new-state-key");
            newStateKey.RegisterValueChangedCallback(_ => ShowAddState());
            root.Q<Button>("add-state").clicked += AddState;

            action = root.Q<DropdownField>("expected-action");
            action.RegisterValueChangedCallback(e =>
            {
                Edit(p => p.ExpectedAction = e.newValue);
                ShowTargets();
            });

            openChoice = root.Q<Button>("open-choice");
            openChoice.clicked += () =>
            {
                var ids = TargetIds();
                ids.Add(set.Targets.Select(t => t.Id).FirstOrDefault(t => !ids.Contains(t)) ?? TargetRegistry.NoTarget);
                Edit(p => p.ExpectedTarget = new ExpectedTarget(ids.ToArray()));
                ShowTargets();
            };

            checkTarget = root.Q<Toggle>("check-target");
            checkTarget.RegisterValueChangedCallback(e => Edit(p => p.CheckTarget = e.newValue ? (bool?)null : false));

            note = root.Q<TextField>("note");
            note.RegisterValueChangedCallback(e => Edit(p => p.Note = string.IsNullOrWhiteSpace(e.newValue) ? null : e.newValue));

            labelledBy = root.Q<TextField>("labelled-by");
            labelledBy.RegisterValueChangedCallback(e =>
                Edit(p => p.LabelledBy = e.newValue.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToList()));

            root.Q<Button>("remove-prompt").clicked += RemoveSelected;
            save = root.Q<ToolbarButton>(SaveName);
            save.clicked += SaveAll;

            ShowList();
            ShowEditor();
            ShowSaveBar();
            return root;
        }

        // ---- The list ----------------------------------------------------------------------------

        // Typing in the editor redraws the list at most a few times a second: checking every
        // prompt on each keystroke would lag in a set of hundreds.
        private void ScheduleList()
        {
            if (listRefresh == null)
            {
                listRefresh = root.schedule.Execute(ShowList);
            }

            listRefresh.ExecuteLater(250);
        }

        private void ShowList()
        {
            if (root == null)
            {
                return;
            }

            list.Clear();
            var problems = PromptSetChecks.Check(set, profile())
                .Where(p => p.PromptId != null)
                .GroupBy(p => p.PromptId)
                .ToDictionary(g => g.Key, g => g.Count());
            string find = (search.value ?? "").Trim();
            bool finding = find.Length > 0;
            bool named = PromptSet.ValidateAnnotator(you(), out _);

            foreach (var category in set.Categories)
            {
                var prompts = finding ? category.Prompts.Where(p => Matches(p, find)).ToList() : category.Prompts;
                if (finding && prompts.Count == 0)
                {
                    continue;
                }

                bool isOpen = finding || open.Contains(category.Id);
                list.Add(CategoryRow(category, isOpen, finding ? $"{prompts.Count} of {category.Prompts.Count}" : category.Prompts.Count.ToString()));
                if (!isOpen)
                {
                    continue;
                }

                foreach (var prompt in prompts)
                {
                    problems.TryGetValue(prompt.Id ?? "", out int count);
                    list.Add(PromptRow(prompt, category, count));
                }

                if (!finding)
                {
                    var add = new Button(() => Add(category)) { text = "+ Add prompt", tooltip = "Your name goes on it as its writer." };
                    add.AddToClassList("ps-add-prompt");
                    add.SetEnabled(named);
                    list.Add(add);
                }
            }

            if (finding && list.childCount == 0)
            {
                var none = new Label($"No prompt matches '{find}'.");
                none.AddToClassList("ag-muted");
                none.AddToClassList("ps-pad-s");
                list.Add(none);
            }

            PromptSetUi.Display(root.Q("write-name-hint"), !named);
            ShowNewCategory();
        }

        private static bool Matches(EvalPrompt prompt, string find)
        {
            return Contains(prompt.Id, find) || Contains(prompt.Label, find) || Contains(prompt.Stimulus, find);
        }

        private static bool Contains(string text, string find) =>
            text != null && text.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0;

        private VisualElement CategoryRow(PromptCategory category, bool isOpen, string count)
        {
            var row = new VisualElement { name = "category-" + category.Id };
            row.AddToClassList("ps-category");
            row.Add(Classed(new Label(isOpen ? "▾" : "▸"), "ps-category__arrow"));
            row.Add(new Label(category.Id));
            row.Add(Classed(new Label(count), "ps-category__count"));
            if (changed.Contains(category))
            {
                row.Add(Classed(new Label("●") { tooltip = "Changed, not saved yet." }, "ps-category__changed"));
            }

            row.RegisterCallback<ClickEvent>(_ =>
            {
                if (!open.Remove(category.Id))
                {
                    open.Add(category.Id);
                }

                ShowList();
            });
            return row;
        }

        private VisualElement PromptRow(EvalPrompt prompt, PromptCategory category, int problems)
        {
            var row = new VisualElement { name = "prompt-" + prompt.Id };
            row.AddToClassList(PromptClass);
            row.EnableInClassList(SelectedClass, prompt == selected);
            row.Add(Classed(new Label(prompt.Id), "ps-prompt__id"));
            var badge = Classed(new Label(problems > 0 ? "⚠" : ""), BadgeClass);
            if (problems > 0)
            {
                badge.tooltip = problems == 1 ? "Breaks a rule: open it to see which." : $"Breaks {problems} rules: open it to see which.";
            }

            row.Add(badge);
            row.Add(Classed(new Label(prompt.Stimulus ?? ""), "ps-prompt__stimulus"));
            row.RegisterCallback<ClickEvent>(_ => Select(prompt, category));
            return row;
        }

        private void ShowNewCategory()
        {
            string id = newCategory.value ?? "";
            var candidate = new PromptCategory { Id = id, File = $"prompts/{id}.json" };
            addCategory.SetEnabled(PromptSet.ValidateCategoryId(id, out _) && set.FindCategory(id) == null && set.ValidateCategory(candidate, out _));
        }

        private void AddCategory()
        {
            string id = newCategory.value ?? "";
            set.Categories.Add(new PromptCategory { Id = id, File = $"prompts/{id}.json" });
            set.Save();
            open.Add(id);
            newCategory.SetValueWithoutNotify("");
            ShowList();
            onFilesWritten();
        }

        private void Add(PromptCategory category)
        {
            var previous = category.Prompts.LastOrDefault();
            var prompt = new EvalPrompt
            {
                Id = set.NextId(category),
                Label = previous != null ? previous.Label : "Player",
                Stimulus = "",
                ExpectedAction = ActionRegistry.None,
                ExpectedTarget = ExpectedTarget.NoTarget,
                PreconditionState = previous != null && previous.PreconditionState != null
                    ? (JObject)previous.PreconditionState.DeepClone()
                    : new JObject(),
                LabelledBy = new List<string> { you() },
            };

            category.Prompts.Add(prompt);
            MarkChanged(category);
            onEdited();
            Select(prompt, category);
            stimulus.Focus();
        }

        // ---- The editor --------------------------------------------------------------------------

        // One edit to the prompt open: the field's new value goes into it, and the rest follows.
        private void Edit(Action<EvalPrompt> apply, bool listShowsIt = false)
        {
            if (selected == null)
            {
                return;
            }

            apply(selected);
            MarkChanged(selectedCategory);
            onEdited();
            ShowChecks();
            if (listShowsIt)
            {
                root.Q<Label>("editor-title").text = $"{selected.Id} · {selectedCategory.Id}";
            }
        }

        private void ShowEditor()
        {
            bool any = selected != null;
            PromptSetUi.Display(root.Q(EditorName), any);
            PromptSetUi.Display(root.Q(NothingSelectedName), !any);
            if (!any)
            {
                return;
            }

            var prompt = selected;
            root.Q<Label>("editor-title").text = $"{prompt.Id} · {selectedCategory.Id}";
            idField.SetValueWithoutNotify(prompt.Id ?? "");
            labelField.SetValueWithoutNotify(prompt.Label ?? "");
            stimulus.SetValueWithoutNotify(prompt.Stimulus ?? "");
            observations.SetValueWithoutNotify(string.Join("\n", prompt.Observations ?? new List<string>()));
            checkTarget.SetValueWithoutNotify(prompt.ChecksTarget);
            note.SetValueWithoutNotify(prompt.Note ?? "");
            labelledBy.SetValueWithoutNotify(string.Join(", ", prompt.LabelledBy ?? new List<string>()));

            var settled = root.Q<Label>("settled-from");
            settled.text = prompt.Labels != null ? "Settled from " + string.Join(", ", prompt.Labels.Select(l => $"{l.Key}: {l.Value}")) : "";
            PromptSetUi.Display(settled, prompt.Labels != null);

            knownLabels.menu.ClearItems();
            foreach (string known in KnownLabels())
            {
                string picked = known;
                knownLabels.menu.AppendAction(known, _ =>
                {
                    labelField.value = picked;
                });
            }

            var actions = ActionChoices(prompt.ExpectedAction);
            action.choices = actions;
            action.SetValueWithoutNotify(prompt.ExpectedAction ?? ActionRegistry.None);

            ShowState();
            ShowAddState();
            ShowLabelledWarning();
            ShowTargets();
            ShowChecks();
        }

        private void ShowState()
        {
            var rows = root.Q("state-rows");
            rows.Clear();
            var prompt = selected;
            if (prompt.PreconditionState == null)
            {
                // Shown as empty; written back only if a key is added.
                return;
            }

            foreach (var property in prompt.PreconditionState.Properties().ToList())
            {
                var row = new VisualElement();
                row.AddToClassList("ps-state-row");
                row.Add(new Label(property.Name) { tooltip = property.Name });
                if (property.Value.Type == JTokenType.Boolean)
                {
                    var flag = new Toggle { value = (bool)property.Value };
                    flag.RegisterValueChangedCallback(e => Edit(_ => property.Value = e.newValue));
                    row.Add(flag);
                }
                else
                {
                    var text = new TextField { isDelayed = true, value = property.Value.ToString(Newtonsoft.Json.Formatting.None) };
                    text.RegisterValueChangedCallback(e => Edit(_ => property.Value = ParseValue(e.newValue)));
                    row.Add(text);
                }

                var remove = new Button(() =>
                {
                    Edit(_ => property.Remove());
                    ShowState();
                    ShowAddState();
                })
                { text = "−", tooltip = "Remove " + property.Name + "." };
                remove.AddToClassList("ps-editor__small");
                row.Add(remove);
                rows.Add(row);
            }
        }

        private void ShowAddState()
        {
            string key = (newStateKey.value ?? "").Trim();
            bool taken = selected?.PreconditionState != null && selected.PreconditionState.ContainsKey(key);
            root.Q<Button>("add-state").SetEnabled(selected != null && key.Length > 0 && !taken);
        }

        private void AddState()
        {
            string key = (newStateKey.value ?? "").Trim();
            Edit(p =>
            {
                p.PreconditionState = p.PreconditionState ?? new JObject();
                p.PreconditionState[key] = false;
            });
            newStateKey.SetValueWithoutNotify("");
            ShowState();
            ShowAddState();
        }

        private List<string> TargetIds() => (selected.ExpectedTarget ?? ExpectedTarget.NoTarget).Ids.ToList();

        private void ShowTargets()
        {
            var rows = root.Q("target-rows");
            rows.Clear();
            var declared = set.FindAction(selected.ExpectedAction);
            bool needsTarget = declared != null && declared.NeedsTarget;
            var choices = new List<string> { TargetRegistry.NoTarget };
            choices.AddRange(set.Targets.Select(t => t.Id));

            var ids = TargetIds();
            for (int i = 0; i < ids.Count; i++)
            {
                if (!choices.Contains(ids[i]))
                {
                    // A target the set does not declare stays shown as it is, and a check names it.
                    choices.Add(ids[i]);
                }
            }

            for (int i = 0; i < ids.Count; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.AddToClassList("ps-target-row");
                var target = new DropdownField(i == 0 ? "Target" : " ", choices, ids[i]);
                target.RegisterValueChangedCallback(e =>
                {
                    var now = TargetIds();
                    now[index] = e.newValue;
                    Edit(p => p.ExpectedTarget = new ExpectedTarget(now.ToArray()));
                });
                row.Add(target);
                if (ids.Count > 1)
                {
                    var remove = new Button(() =>
                    {
                        var now = TargetIds();
                        now.RemoveAt(index);
                        Edit(p => p.ExpectedTarget = new ExpectedTarget(now.ToArray()));
                        ShowTargets();
                    })
                    { text = "−", tooltip = "Remove this choice." };
                    remove.AddToClassList("ps-editor__small");
                    row.Add(remove);
                }

                rows.Add(row);
            }

            openChoice.SetEnabled(needsTarget);
        }

        // Changing the answer of a prompt others have labelled hides the split they made.
        private void ShowLabelledWarning()
        {
            var host = root.Q("labelled-warning");
            host.Clear();
            var prompt = selected;
            int others = AgreementReport.AnnotatorsOf(prompt, labels).Count(a => prompt.LabelledBy == null || !prompt.LabelledBy.Contains(a));
            if (others > 0)
            {
                host.Add(new ProblemBox(
                    $"{others} other {(others == 1 ? "person has" : "people have")} answered this prompt blind. " +
                    "Change its answer by settling it in Agreement, so both answers are kept."));
            }
        }

        private void ShowChecks()
        {
            var host = root.Q(ChecksName);
            host.Clear();
            var problems = PromptSetChecks.CheckPrompt(set, selectedCategory, selected, profile());
            if (problems.Count == 0)
            {
                var ok = new VisualElement();
                ok.AddToClassList("ps-check");
                ok.Add(Classed(new Label("✓"), "ps-check__mark"));
                ok.Add(Classed(new Label("No rule broken" + (profile() == null ? " (choose a profile to check its few-shot examples too)" : "")), "ag-muted"));
                host.Add(ok);
            }

            foreach (var problem in problems)
            {
                host.Add(new ProblemBox(problem.Message));
            }
        }

        private void ShowSaveBar()
        {
            root.Q<Label>(SaveStateName).text = HasUnsavedChanges ? $"{changed.Count} file(s) changed" : "Saved";
            save.SetEnabled(HasUnsavedChanges);
        }

        private void RemoveSelected()
        {
            if (selected == null
                || !EditorUtility.DisplayDialog($"Remove {selected.Id}?",
                    "A prompt that has been labelled or scored is better rewritten as a new id than removed. Remove it anyway?", "Remove", "Cancel"))
            {
                return;
            }

            selectedCategory.Prompts.Remove(selected);
            MarkChanged(selectedCategory);
            onEdited();
            selected = null;
            ShowList();
            ShowEditor();
        }

        // The profile's actions when one is chosen, since actions come from it; the set's otherwise.
        // The current value stays in the list even when neither has it, so a broken prompt shows
        // what it says rather than silently becoming another action.
        private List<string> ActionChoices(string current)
        {
            var chosenProfile = profile();
            var ids = chosenProfile != null
                ? chosenProfile.Actions.Where(a => a != null && a.Action != null).Select(a => a.Action.Id).ToList()
                : set.Actions.Select(a => a.Id).ToList();

            ids.Remove(ActionRegistry.None);
            ids.Add(ActionRegistry.None);
            if (!string.IsNullOrEmpty(current) && !ids.Contains(current))
            {
                ids.Add(current);
            }

            return ids;
        }

        private List<string> KnownLabels()
        {
            var known = set.Prompts.Select(p => p.Label).Where(l => !string.IsNullOrEmpty(l)).Distinct().ToList();
            foreach (string label in new[] { "Player", "Event", "Situation" })
            {
                if (!known.Contains(label)) known.Add(label);
            }

            return known;
        }

        private static T Classed<T>(T element, string className) where T : VisualElement
        {
            element.AddToClassList(className);
            return element;
        }

        // A state value is JSON: true, 3, "night". Anything that does not parse is kept as text.
        private static JToken ParseValue(string text)
        {
            try
            {
                return JToken.Parse(text);
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return new JValue(text);
            }
        }
    }
}
