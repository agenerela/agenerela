using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Evaluation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// Write mode: the prompts grouped by category on the left, one prompt's editor on the right.
    /// The expected answer is picked from dropdowns, and the rules a prompt breaks show under it as
    /// you type. Changes stay in memory until saved, one category file at a time.
    /// </summary>
    internal sealed class PromptSetWriteView
    {
        private const float ListWidth = 260;

        private readonly PromptSet set;
        private readonly Func<AgentProfile> profile;
        private readonly Func<string> you;
        private readonly Action onFilesWritten;
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
        private Vector2 listScroll;
        private Vector2 editorScroll;
        private string newCategory = "";
        private string newStateKey = "";

        internal PromptSetWriteView(PromptSet set, Func<AgentProfile> profile, Func<string> you, Action onFilesWritten)
        {
            this.set = set;
            this.profile = profile;
            this.you = you;
            this.onFilesWritten = onFilesWritten;
            foreach (var category in set.Categories)
            {
                baselines[category] = PromptSetWindow.Fingerprint(set, category);
            }
        }

        internal bool HasUnsavedChanges => changed.Count > 0;

        /// <summary>The categories edited since they were last saved.</summary>
        internal IEnumerable<PromptCategory> Changed => changed;

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
        }

        internal void SetLabels(IReadOnlyList<AnnotatorLabels> files) => labels = files;

        internal void SaveAll()
        {
            foreach (var category in changed)
            {
                set.SaveCategory(category);
                baselines[category] = PromptSetWindow.Fingerprint(set, category);
            }

            changed.Clear();
            onFilesWritten();
        }

        internal void Draw(Rect window)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
                {
                    DrawList();
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    editorScroll = EditorGUILayout.BeginScrollView(editorScroll);
                    if (selected != null)
                    {
                        DrawEditor();
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Pick a prompt on the left, or add one to a category.", MessageType.Info);
                    }

                    EditorGUILayout.EndScrollView();
                    DrawSaveBar();
                }
            }
        }

        private void DrawList()
        {
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            var problemIds = new HashSet<string>(PromptSetChecks.Check(set, profile()).Select(p => p.PromptId));

            foreach (var category in set.Categories)
            {
                bool isOpen = open.Contains(category.Id);
                string title = $"{category.Id} ({category.Prompts.Count})" + (changed.Contains(category) ? " *" : "");
                bool nowOpen = EditorGUILayout.Foldout(isOpen, title, true, EditorStyles.foldoutHeader);
                if (nowOpen != isOpen)
                {
                    if (nowOpen) open.Add(category.Id); else open.Remove(category.Id);
                }

                if (!nowOpen)
                {
                    continue;
                }

                foreach (var prompt in category.Prompts)
                {
                    string mark = problemIds.Contains(prompt.Id) ? " ⚠" : "";
                    var style = prompt == selected ? EditorStyles.selectionRect : EditorStyles.label;
                    if (GUILayout.Button($"  {prompt.Id}{mark}   {prompt.Stimulus}", style, GUILayout.Width(ListWidth - 20)))
                    {
                        Select(prompt, category);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(!PromptSet.ValidateAnnotator(you(), out _)))
                    {
                        if (GUILayout.Button(new GUIContent("+ Add prompt", "Your name goes on it as its writer."), GUILayout.Width(110)))
                        {
                            Add(category);
                        }
                    }
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                newCategory = EditorGUILayout.TextField(newCategory);
                var candidate = new PromptCategory { Id = newCategory, File = $"prompts/{newCategory}.json" };
                bool valid = PromptSet.ValidateCategoryId(newCategory, out _) && set.FindCategory(newCategory) == null
                             && set.ValidateCategory(candidate, out _);
                using (new EditorGUI.DisabledScope(!valid))
                {
                    if (GUILayout.Button("+ Category", GUILayout.Width(90)))
                    {
                        set.Categories.Add(candidate);
                        set.Save();
                        open.Add(newCategory);
                        newCategory = "";
                        onFilesWritten();
                    }
                }
            }

            if (!PromptSet.ValidateAnnotator(you(), out _))
            {
                EditorGUILayout.HelpBox("Type your name into Your name in the toolbar to add prompts.", MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEditor()
        {
            var prompt = selected;
            EditorGUILayout.LabelField($"{prompt.Id} · {selectedCategory.Id}", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            prompt.Id = EditorGUILayout.DelayedTextField("Id", prompt.Id);
            prompt.Label = TextWithChoices("Label", prompt.Label, KnownLabels());
            prompt.Stimulus = EditorGUILayout.TextField("Stimulus", prompt.Stimulus);

            EditorGUILayout.LabelField("Observations", "One per line. Empty for most player commands.", EditorStyles.miniLabel);
            // Each field is written back only when it is edited, so selecting a prompt never
            // tidies it: what is not touched saves exactly as it was read.
            EditorGUI.BeginChangeCheck();
            string observations = EditorGUILayout.TextArea(string.Join("\n", prompt.Observations ?? new List<string>()), GUILayout.MinHeight(48));
            if (EditorGUI.EndChangeCheck())
            {
                prompt.Observations = observations.Split('\n').Select(o => o.Trim()).Where(o => o.Length > 0).ToList();
            }

            DrawState(prompt);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Expected answer", EditorStyles.miniBoldLabel);
            WarnIfLabelled(prompt);
            DrawExpected(prompt);

            bool check = EditorGUILayout.Toggle(new GUIContent("Check target", "Off only for a case that tests a decision with the grounding guard off (DR-016)."), prompt.ChecksTarget);
            if (check != prompt.ChecksTarget)
            {
                prompt.CheckTarget = check ? (bool?)null : false;
            }

            EditorGUILayout.LabelField("Note", "Why the answer is what it is.", EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            string note = EditorGUILayout.TextArea(prompt.Note ?? "", GUILayout.MinHeight(36));
            if (EditorGUI.EndChangeCheck())
            {
                prompt.Note = string.IsNullOrWhiteSpace(note) ? null : note;
            }

            EditorGUI.BeginChangeCheck();
            string labelledBy = EditorGUILayout.TextField(new GUIContent("Labelled by", "Comma-separated."), string.Join(", ", prompt.LabelledBy ?? new List<string>()));
            if (EditorGUI.EndChangeCheck())
            {
                prompt.LabelledBy = labelledBy.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
            }

            if (prompt.Labels != null)
            {
                EditorGUILayout.LabelField("Settled from", string.Join(", ", prompt.Labels.Select(l => $"{l.Key}: {l.Value}")), EditorStyles.wordWrappedMiniLabel);
            }

            if (EditorGUI.EndChangeCheck())
            {
                MarkChanged(selectedCategory);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Checks", EditorStyles.miniBoldLabel);
            var problems = PromptSetChecks.CheckPrompt(set, selectedCategory, prompt, profile());
            if (problems.Count == 0)
            {
                EditorGUILayout.LabelField("✓ No rule broken" + (profile() == null ? " (choose a profile to check its few-shot examples too)" : ""), EditorStyles.wordWrappedMiniLabel);
            }

            foreach (var problem in problems)
            {
                EditorGUILayout.HelpBox(problem.Message, MessageType.Warning);
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(80))
                    && EditorUtility.DisplayDialog($"Remove {prompt.Id}?",
                        "A prompt that has been labelled or scored is better rewritten as a new id than removed. Remove it anyway?", "Remove", "Cancel"))
                {
                    selectedCategory.Prompts.Remove(prompt);
                    MarkChanged(selectedCategory);
                    selected = null;
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawState(EvalPrompt prompt)
        {
            prompt.PreconditionState = prompt.PreconditionState ?? new JObject();
            EditorGUILayout.LabelField("State", "The agent's state the prompt assumes.", EditorStyles.miniLabel);
            foreach (var property in prompt.PreconditionState.Properties().ToList())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("    " + property.Name, GUILayout.Width(EditorGUIUtility.labelWidth));
                    EditorGUI.BeginChangeCheck();
                    if (property.Value.Type == JTokenType.Boolean)
                    {
                        bool flag = EditorGUILayout.Toggle((bool)property.Value);
                        if (EditorGUI.EndChangeCheck()) property.Value = flag;
                    }
                    else
                    {
                        string text = EditorGUILayout.DelayedTextField(property.Value.ToString(Newtonsoft.Json.Formatting.None));
                        if (EditorGUI.EndChangeCheck()) property.Value = ParseValue(text);
                    }

                    if (GUILayout.Button("−", GUILayout.Width(22)))
                    {
                        property.Remove();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("    add", GUILayout.Width(EditorGUIUtility.labelWidth));
                newStateKey = EditorGUILayout.TextField(newStateKey);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newStateKey) || prompt.PreconditionState.ContainsKey(newStateKey)))
                {
                    if (GUILayout.Button("+", GUILayout.Width(22)))
                    {
                        prompt.PreconditionState[newStateKey.Trim()] = false;
                        newStateKey = "";
                    }
                }
            }
        }

        private void DrawExpected(EvalPrompt prompt)
        {
            var actions = ActionChoices(prompt.ExpectedAction);
            EditorGUI.BeginChangeCheck();
            int index = EditorGUILayout.Popup("Action", actions.IndexOf(prompt.ExpectedAction), actions.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                prompt.ExpectedAction = actions[index];
            }

            var declared = set.FindAction(prompt.ExpectedAction);
            bool needsTarget = declared != null && declared.NeedsTarget;
            var targets = new List<string> { TargetRegistry.NoTarget };
            targets.AddRange(set.Targets.Select(t => t.Id));

            EditorGUI.BeginChangeCheck();
            var ids = (prompt.ExpectedTarget ?? ExpectedTarget.NoTarget).Ids.ToList();
            for (int i = 0; i < ids.Count; i++)
            {
                if (!targets.Contains(ids[i]))
                {
                    targets.Add(ids[i]);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    ids[i] = targets[EditorGUILayout.Popup(i == 0 ? "Target" : " ", targets.IndexOf(ids[i]), targets.ToArray())];
                    if (ids.Count > 1 && GUILayout.Button("−", GUILayout.Width(22)))
                    {
                        ids.RemoveAt(i);
                        break;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!needsTarget))
                {
                    if (GUILayout.Button(new GUIContent("+ open choice", "Either target is right."), GUILayout.Width(110)))
                    {
                        ids.Add(set.Targets.Select(t => t.Id).FirstOrDefault(t => !ids.Contains(t)) ?? TargetRegistry.NoTarget);
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                prompt.ExpectedTarget = new ExpectedTarget(ids.ToArray());
            }
        }

        // Changing the answer of a prompt others have labelled hides the split they made.
        private void WarnIfLabelled(EvalPrompt prompt)
        {
            int others = AgreementReport.AnnotatorsOf(prompt, labels).Count(a => prompt.LabelledBy == null || !prompt.LabelledBy.Contains(a));
            if (others > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{others} other {(others == 1 ? "person has" : "people have")} answered this prompt blind. " +
                    "Change its answer by settling it in Agreement, so both answers are kept.",
                    MessageType.Info);
            }
        }

        private void DrawSaveBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(HasUnsavedChanges ? $"{changed.Count} file(s) changed" : "Saved", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!HasUnsavedChanges))
                {
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        SaveAll();
                    }
                }
            }
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

            MarkChanged(category);
            category.Prompts.Add(prompt);
            Select(prompt, category);
        }

        private void Select(EvalPrompt prompt, PromptCategory category)
        {
            selected = prompt;
            selectedCategory = category;
            newStateKey = "";
            GUI.FocusControl(null);
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

        private static string TextWithChoices(string label, string value, List<string> choices)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                int picked = EditorGUILayout.Popup(-1, choices.ToArray(), GUILayout.Width(18));
                if (picked >= 0)
                {
                    GUI.FocusControl(null);
                    return choices[picked];
                }
            }

            return value;
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
