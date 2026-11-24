using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Editor.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>The Prompt Set window's three tabs.</summary>
    internal enum PromptSetMode
    {
        Write,
        Label,
        Agreement,
    }

    /// <summary>
    /// What the Prompt Set window shows around its three views (#160, screen 8), as a UI Toolkit
    /// tree on the #167 kit: the toolbar, the tabs, the messages above every view and the start
    /// page. The views themselves are <see cref="PromptSetWriteView"/>, <see cref="PromptSetLabelView"/>
    /// and <see cref="PromptSetAgreementView"/>, each placed into its host here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It holds no set and decides nothing: <see cref="PromptSetWindow"/> owns the set, the
    /// profile and the drafts, and calls <see cref="Show"/> with all of it. What a person does here
    /// comes back as the events below.
    /// </para>
    /// <para>
    /// It needs no window, so a test builds it and reads its tree directly; every part a test or
    /// the window reaches is named (the constants below).
    /// </para>
    /// </remarks>
    internal sealed class PromptSetUi
    {
        internal const string Folder = "Packages/com.agenerela.framework/Editor/Evaluation/";
        internal const string LayoutPath = Folder + "PromptSetWindow.uxml";
        internal const string SheetPath = Folder + "PromptSetWindow.uss";

        // Names a test or the window reaches. The rest are in the UXML.
        internal const string ToolbarName = "toolbar", SetFieldName = "set-field", ProfileFieldName = "profile-field", NameFieldName = "name-field",
            NamesName = "names", ReloadName = "reload", ModeLineName = "mode-line", DraftsName = "drafts", ProfileProblemsName = "profile-problems",
            StartName = "start", LoadErrorName = "load-error", OpenProjectSetName = "open-project-set", FoundSetsName = "found-sets",
            NewSetName = "new-set", SaveFirstName = "save-first", WriteHostName = "write-host", LabelHostName = "label-host",
            AgreementHostName = "agreement-host";

        internal static readonly string[] TabNames = { "tab-write", "tab-label", "tab-agreement" };

        /// <summary>The line under the tabs, in #158's words.</summary>
        internal static readonly string[] ModeDescriptions =
        {
            "add prompts, each with the answer it should get",
            "answer other people's prompts without seeing their answers",
            "compare everyone's answers and settle the splits",
        };

        /// <summary>A step on the start page that is done carries this class, and its number turns to a tick.</summary>
        internal const string StepDoneClass = "ps-step--done";

        /// <summary>A draft from before a reload that was not applied, as the window shows it.</summary>
        internal readonly struct WaitingDraft
        {
            internal WaitingDraft(string categoryId, string why, bool canKeep)
            {
                CategoryId = categoryId;
                Why = why;
                CanKeep = canKeep;
            }

            internal string CategoryId { get; }
            internal string Why { get; }
            internal bool CanKeep { get; }
        }

        /// <summary>Everything the window shows around its views, handed over whole on each change.</summary>
        internal sealed class State
        {
            internal TextAsset SetAsset;
            internal AgentProfile Profile;
            internal string You = "";

            /// <summary>True once the set file has loaded.</summary>
            internal bool SetLoaded;

            /// <summary>Why the set did not load; null when it did or none is chosen.</summary>
            internal string LoadError;

            /// <summary>Step 1's line once a set is open: its path and how many prompts it has.</summary>
            internal string SetSummary;

            internal IReadOnlyList<string> FoundSets = Array.Empty<string>();
            internal IReadOnlyList<string> KnownNames = Array.Empty<string>();
            internal PromptSetMode Mode;

            /// <summary>Write has edits not yet saved: Label and Agreement wait for them.</summary>
            internal bool WriteUnsaved;

            /// <summary>Where the profile and the set disagree, each in words.</summary>
            internal IReadOnlyList<string> ProfileDifferences = Array.Empty<string>();

            internal IReadOnlyList<WaitingDraft> Drafts = Array.Empty<WaitingDraft>();

            /// <summary>The tabs open once a set has loaded and a profile is chosen.</summary>
            internal bool Ready => SetLoaded && Profile != null;
        }

        private readonly VisualElement root;
        private readonly ObjectField setField, profileField;
        private readonly TextField nameField;
        private readonly ToolbarMenu names;
        private readonly ToolbarToggle[] tabs;
        private readonly VisualElement[] hosts;
        private State state = new State();

        internal event Action<TextAsset> SetChanged;
        internal event Action<AgentProfile> ProfileChanged;
        internal event Action<string> NameChanged;
        internal event Action<PromptSetMode> ModeChanged;
        internal event Action Reload, NewSet, SaveFirst;

        /// <summary>Raised with the button, so a menu of several sets opens under it.</summary>
        internal event Action<VisualElement> OpenProjectSet;

        internal event Action<int> KeepDraft, DiscardDraft;

        /// <param name="root">The tree built from <see cref="LayoutPath"/>; see <see cref="Load"/>.</param>
        internal PromptSetUi(VisualElement root)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));

            setField = root.Q<ObjectField>(SetFieldName);
            setField.objectType = typeof(TextAsset);
            setField.allowSceneObjects = false;
            setField.RegisterValueChangedCallback(e => SetChanged?.Invoke(e.newValue as TextAsset));

            profileField = root.Q<ObjectField>(ProfileFieldName);
            profileField.objectType = typeof(AgentProfile);
            profileField.allowSceneObjects = false;
            profileField.RegisterValueChangedCallback(e => ProfileChanged?.Invoke(e.newValue as AgentProfile));

            nameField = root.Q<TextField>(NameFieldName);
            nameField.RegisterValueChangedCallback(e => NameChanged?.Invoke(e.newValue));

            names = root.Q<ToolbarMenu>(NamesName);
            root.Q<ToolbarButton>(ReloadName).clicked += () => Reload?.Invoke();

            tabs = TabNames.Select(n => root.Q<ToolbarToggle>(n)).ToArray();
            for (int i = 0; i < tabs.Length; i++)
            {
                var mode = (PromptSetMode)i;
                tabs[i].RegisterValueChangedCallback(e =>
                {
                    // A toggle can be clicked off; the tab stays chosen, as a tab does.
                    if (e.newValue && mode != state.Mode)
                    {
                        ModeChanged?.Invoke(mode);
                    }

                    tabs[(int)mode].SetValueWithoutNotify(mode == state.Mode);
                });
            }

            hosts = new[] { root.Q(WriteHostName), root.Q(LabelHostName), root.Q(AgreementHostName) };

            var open = root.Q<Button>(OpenProjectSetName);
            open.clicked += () => OpenProjectSet?.Invoke(open);
            AgenerelaButtons.MakePrimary(open);
            root.Q<Button>(NewSetName).clicked += () => NewSet?.Invoke();

            Show(state);
        }

        /// <summary>A new tree from <paramref name="layoutPath"/>, with the kit's stylesheet and the window's own.</summary>
        internal static VisualElement Load(string layoutPath = LayoutPath)
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(layoutPath)
                         ?? throw new InvalidOperationException($"The Prompt Set window's layout is missing: {layoutPath}.");
            var tree = layout.Instantiate();
            tree.style.flexGrow = 1;
            AgenerelaStyles.Apply(tree);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath);
            if (sheet != null && !tree.styleSheets.Contains(sheet))
            {
                tree.styleSheets.Add(sheet);
            }

            return tree;
        }

        internal VisualElement Root => root;

        /// <summary>Where <paramref name="mode"/>'s view goes.</summary>
        internal VisualElement HostOf(PromptSetMode mode) => hosts[(int)mode];

        /// <summary>Puts <paramref name="view"/> into <paramref name="mode"/>'s host, in place of what was there.</summary>
        internal void Place(PromptSetMode mode, VisualElement view)
        {
            var host = hosts[(int)mode];
            if (view != null && view.parent == host)
            {
                return;
            }

            host.Clear();
            if (view != null)
            {
                host.Add(view);
            }
        }

        /// <summary>Shows <paramref name="next"/>: everything around the views is set again from it.</summary>
        internal void Show(State next)
        {
            state = next ?? new State();

            setField.SetValueWithoutNotify(state.SetAsset);
            profileField.SetValueWithoutNotify(state.Profile);
            if (nameField.value != (state.You ?? ""))
            {
                nameField.SetValueWithoutNotify(state.You ?? "");
            }

            ShowNames();
            ShowTabs();
            ShowDrafts();
            ShowProfileProblems();
            ShowStart();
            ShowBody();
        }

        private void ShowNames()
        {
            names.menu.ClearItems();
            foreach (string name in state.KnownNames)
            {
                string picked = name;
                names.menu.AppendAction(name, _ => NameChanged?.Invoke(picked),
                    _ => picked == state.You ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }

            if (state.KnownNames.Count == 0)
            {
                names.menu.AppendAction("Nobody has written or labelled a prompt yet", _ => { }, DropdownMenuAction.Status.Disabled);
            }
        }

        private void ShowTabs()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].SetEnabled(state.Ready);
                tabs[i].SetValueWithoutNotify(state.Ready && i == (int)state.Mode);
            }

            root.Q<Label>(ModeLineName).text = state.Ready
                ? $"{state.Mode}: {ModeDescriptions[(int)state.Mode]}."
                : NotReadyReason(state);
        }

        /// <summary>The line under the greyed-out tabs: what is still missing.</summary>
        internal static string NotReadyReason(State state)
        {
            string missing = !state.SetLoaded && state.Profile == null ? "a set and a profile are"
                : !state.SetLoaded ? "a set is"
                : "a profile is";
            return $"The tabs open once {missing} chosen: the steps are below.";
        }

        private void ShowDrafts()
        {
            var host = root.Q(DraftsName);
            host.Clear();
            for (int i = 0; i < state.Drafts.Count; i++)
            {
                var draft = state.Drafts[i];
                int index = i;
                var box = new ProblemBox(
                    $"Unsaved prompts for '{draft.CategoryId}' from before a reload were not applied: {draft.Why}. " +
                    "Keeping them replaces that category's prompts in the window with your draft; nothing is written until you save.",
                    ProblemSeverity.Warning);
                if (draft.CanKeep)
                {
                    box.SetFix("Keep my draft", () => KeepDraft?.Invoke(index));
                }

                var discard = new Button(() => DiscardDraft?.Invoke(index)) { text = "Discard it" };
                discard.AddToClassList(ProblemBox.UssClassName + "__fix");
                box.Add(discard);
                host.Add(box);
            }
        }

        private void ShowProfileProblems()
        {
            var host = root.Q(ProfileProblemsName);
            host.Clear();
            if (!state.Ready)
            {
                return;
            }

            foreach (string difference in state.ProfileDifferences)
            {
                host.Add(new ProblemBox(difference + ". Nothing is changed here: fix the profile or the set file.", ProblemSeverity.Warning));
            }
        }

        private void ShowStart()
        {
            var start = root.Q(StartName);
            Display(start, !state.Ready);
            if (state.Ready)
            {
                return;
            }

            var loadError = root.Q(LoadErrorName);
            loadError.Clear();
            if (state.LoadError != null)
            {
                loadError.Add(new ProblemBox(state.LoadError, ProblemSeverity.Error));
            }

            // Step 1: the set.
            Step(1, state.SetLoaded, state.SetLoaded
                ? state.SetSummary
                : "The set file, the agent.json beside a folder of prompts. It names the agent's actions and targets, and the categories.");
            var sets = state.FoundSets;
            var open = root.Q<Button>(OpenProjectSetName);
            open.SetEnabled(sets.Count > 0);
            open.tooltip = sets.Count == 0 ? "There is no agent.json under Assets/ yet." : "Opens the set and the profile beside it, if there is one.";
            root.Q<Label>(FoundSetsName).text = sets.Count == 0 ? "No agent.json under Assets/ yet."
                : sets.Count == 1 ? sets[0]
                : $"{sets.Count} found: you choose which.";
            root.Q<Button>(NewSetName).SetEnabled(state.Profile != null);
            Display(root.Q("new-set-note"), state.Profile == null);

            // Step 2: the profile.
            Step(2, state.Profile != null, state.Profile != null
                ? $"{state.Profile.name}: {state.Profile.Actions.Count(a => a != null)} actions."
                : "The AgentProfile the set is measured against. Write offers its actions, and every prompt is checked against its few-shot examples (hard rule 4).");
            var hint = root.Q<Label>("step-2-hint");
            hint.text = state.SetLoaded
                ? "Drag the agent's AgentProfile into Profile above."
                : "Opening the set picks the profile beside it, if there is one. Otherwise, drag yours into Profile above.";
            Display(hint, state.Profile == null);

            // Step 3: your name. Not needed to open the tabs, but to write and to label.
            bool named = Agenerela.Evaluation.PromptSet.ValidateAnnotator(state.You, out string nameProblem);
            Step(3, named, named
                ? state.You + "."
                : "In Your name above. It goes on the prompts you write, and your answers in Label are saved under it.");
            var problem = root.Q<Label>("step-3-problem");
            problem.text = named || string.IsNullOrEmpty(state.You) ? "" : nameProblem + ".";
            Display(problem, problem.text.Length > 0);
        }

        private void Step(int number, bool done, string detail)
        {
            var step = root.Q("step-" + number);
            step.EnableInClassList(StepDoneClass, done);
            root.Q<Label>($"step-{number}-mark").text = done ? "✓" : number.ToString();
            root.Q<Label>($"step-{number}-detail").text = detail;
        }

        private void ShowBody()
        {
            // Label and Agreement wait for Write's edits to be saved: a waiting draft cannot be
            // saved, and stays offered above.
            bool gated = state.Ready && state.Mode != PromptSetMode.Write && state.WriteUnsaved;
            var saveFirst = root.Q(SaveFirstName);
            saveFirst.Clear();
            if (gated)
            {
                var box = new ProblemBox("Save the prompts you changed in Write mode first.", ProblemSeverity.Warning);
                box.SetFix("Save", () => SaveFirst?.Invoke());
                saveFirst.Add(box);
            }

            Display(saveFirst, gated);
            for (int i = 0; i < hosts.Length; i++)
            {
                Display(hosts[i], state.Ready && !gated && i == (int)state.Mode);
            }
        }

        internal static void Display(VisualElement element, bool shown)
        {
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
