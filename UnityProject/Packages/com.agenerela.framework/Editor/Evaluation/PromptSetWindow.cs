using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Agenerela.Evaluation;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// <b>Window → Agenerela → Prompt Set</b>: writes the labelled prompts an agent is measured
    /// against, and collects other people's answers to them blind (#134, DR-017).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three modes. <b>Write</b> edits prompts, with the expected answer picked from the agent's
    /// actions and the set's targets, and shows the rules each one breaks. <b>Label</b> asks one
    /// annotator the prompts they have not answered, and is given nothing that shows an answer:
    /// see <see cref="PromptSetLabelView"/>. <b>Agreement</b> compares everyone's answers and
    /// settles a split only when a person clicks to.
    /// </para>
    /// <para>
    /// The set file is the <c>agent.json</c> a folder of prompts sits beside. Until a set and a
    /// profile are chosen the tabs are greyed out and the window shows the steps to start (#158);
    /// the set, the profile and your name are then remembered on this machine, per project, never
    /// in the set (<see cref="PromptSetPrefs"/>).
    /// </para>
    /// </remarks>
    public sealed class PromptSetWindow : EditorWindow
    {
        private enum Mode { Write, Label, Agreement }

        private static readonly string[] ModeNames = { "Write", "Label", "Agreement" };

        // The line under the tabs, in #158's words.
        private static readonly string[] ModeDescriptions =
        {
            "add prompts, each with the answer it should get",
            "answer other people's prompts without seeing their answers",
            "compare everyone's answers and settle the splits",
        };

        private static readonly GUIContent[] ModeTabs = ModeNames
            .Select((name, i) => new GUIContent(name, char.ToUpperInvariant(ModeDescriptions[i][0]) + ModeDescriptions[i].Substring(1) + "."))
            .ToArray();

        private const string SetTip = "The set file: the agent.json beside a folder of prompts. Drag one in, or click Open the project's evaluation set while no set is open.";
        private const string ProfileTip = "The AgentProfile the set is measured against. Write offers its actions, and every prompt is checked against its few-shot examples (hard rule 4).";
        private const string NameTip = "Your name. It goes on the prompts you write, and your answers in Label are saved under it.";
        private const string NamesTip = "Pick a name already in the set.";
        private const string ReloadTip = "Read the set and everyone's label files again, say after pulling a teammate's commit.";

        private PromptSetPrefs prefs;
        private IReadOnlyList<string> foundSets;
        private Vector2 startScroll;

        [SerializeField] private TextAsset setAsset;
        [SerializeField] private AgentProfile profile;
        [SerializeField] private string you = "";
        [SerializeField] private Mode mode;

        // Unsaved prompts, one category's list as JSON per entry, kept through a domain reload: a
        // script recompiling would otherwise drop them, since the set itself is not serialized.
        // Each keeps its file's fingerprint from before the edits. A draft is applied only onto the
        // file it was made from; one whose file changed, or that could not be applied, stays here
        // until a person keeps or discards it, through any number of reloads.
        [SerializeField] private List<string> draftCategories = new List<string>();
        [SerializeField] private List<string> draftPrompts = new List<string>();
        [SerializeField] private List<string> draftBaselines = new List<string>();

        private static readonly JsonSerializerSettings DraftSettings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
        };

        private PromptSet set;
        private string loadError;
        private IReadOnlyList<AnnotatorLabels> labels = Array.Empty<AnnotatorLabels>();
        private PromptSetWriteView writeView;
        private PromptSetLabelView labelView;
        private PromptSetAgreementView agreementView;

        [MenuItem("Window/Agenerela/Prompt Set")]
        public static void Open()
        {
            var window = GetWindow<PromptSetWindow>();
            window.titleContent = new GUIContent("Prompt Set");
            window.minSize = new Vector2(640, 420);
            window.Show();
        }

        private void OnEnable()
        {
            saveChangesMessage = SaveMessage;
            prefs = PromptSetPrefs.ForThisProject();
            if (setAsset == null)
            {
                setAsset = LoadByGuid<TextAsset>(prefs.SetGuid);
            }

            if (string.IsNullOrEmpty(you))
            {
                you = prefs.Annotator;
            }

            AssemblyReloadEvents.beforeAssemblyReload += StashDraft;
            Reload();

            // A remembered set with no profile remembered for it takes the one beside it, then
            // the last one chosen.
            FillMissingProfile();
            if (profile == null)
            {
                profile = LoadByGuid<AgentProfile>(prefs.LastProfileGuid);
            }

            RestoreDraft();
        }

        // A set file added, moved or deleted changes what the start button opens.
        private void OnProjectChange()
        {
            foundSets = null;
            Repaint();
        }

        /// <summary>The tabs open once a set has loaded and a profile is chosen.</summary>
        private bool Ready => set != null && profile != null;

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= StashDraft;
        }

        public override void SaveChanges()
        {
            // Also how the close dialog saves, so it lets the window close; a draft still waiting
            // is named in that dialog's message. Saving from inside the window recomputes the flag.
            writeView?.SaveAll();
            hasUnsavedChanges = false;
            base.SaveChanges();
        }

        /// <summary>A category file's content hash, or "" when it does not exist yet.</summary>
        internal static string Fingerprint(PromptSet set, PromptCategory category)
        {
            string path = set.CategoryPath(category);
            if (!File.Exists(path))
            {
                return "";
            }

            using (var sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
            }
        }

        /// <summary>Unsaved edits, or drafts from before a reload still waiting for a decision.</summary>
        private bool Unsaved => (writeView != null && writeView.HasUnsavedChanges) || draftCategories.Count > 0;

        private const string SaveMessage = "The prompt set has unsaved changes. Save them?";

        private void StashDraft()
        {
            // Drafts still waiting for a decision stay; the edits made since are added to them.
            if (set == null || writeView == null)
            {
                return;
            }

            foreach (var category in writeView.Changed)
            {
                draftCategories.Add(category.Id);
                draftPrompts.Add(JsonConvert.SerializeObject(category.Prompts, DraftSettings));
                draftBaselines.Add(writeView.BaselineOf(category));
            }
        }

        private void RestoreDraft()
        {
            if (set == null || writeView == null)
            {
                return;
            }

            for (int i = draftCategories.Count - 1; i >= 0; i--)
            {
                var category = set.FindCategory(draftCategories[i]);
                if (category != null && Fingerprint(set, category) == draftBaselines[i] && ApplyDraft(i, category, draftBaselines[i]))
                {
                    RemoveDraft(i);
                }
            }

            hasUnsavedChanges = Unsaved;
        }

        private bool ApplyDraft(int i, PromptCategory category, string baseline)
        {
            List<EvalPrompt> prompts;
            try
            {
                prompts = JsonConvert.DeserializeObject<List<EvalPrompt>>(draftPrompts[i], DraftSettings);
            }
            catch (JsonException)
            {
                return false;
            }

            if (prompts == null)
            {
                return false;
            }

            category.Prompts.Clear();
            category.Prompts.AddRange(prompts.Where(p => p != null));
            writeView.MarkChanged(category, baseline);
            return true;
        }

        private void RemoveDraft(int i)
        {
            draftCategories.RemoveAt(i);
            draftPrompts.RemoveAt(i);
            draftBaselines.RemoveAt(i);
        }

        private void ClearDraft()
        {
            draftCategories.Clear();
            draftPrompts.Clear();
            draftBaselines.Clear();
        }

        /// <summary>
        /// Drafts from before a reload that were not applied: their file changed on disk since the
        /// edits, their category is gone, or the set did not load. Never applied without a click.
        /// </summary>
        private void DrawWaitingDrafts()
        {
            for (int i = draftCategories.Count - 1; i >= 0; i--)
            {
                var category = set != null ? set.FindCategory(draftCategories[i]) : null;
                string why = set == null ? "the set did not load"
                    : category == null ? "the set no longer has this category"
                    : "its file changed on disk since you edited it";
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        $"Unsaved prompts for '{draftCategories[i]}' from before a reload were not applied: {why}. " +
                        "Keeping them replaces that category's prompts in the window with your draft; nothing is written until you save.",
                        EditorStyles.wordWrappedLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(category == null || writeView == null))
                        {
                            if (GUILayout.Button("Keep my draft", GUILayout.Width(120)) && ApplyDraft(i, category, Fingerprint(set, category)))
                            {
                                RemoveDraft(i);
                                hasUnsavedChanges = Unsaved;
                                GUIUtility.ExitGUI();
                            }
                        }

                        if (GUILayout.Button("Discard it", GUILayout.Width(100))
                            && EditorUtility.DisplayDialog("Discard the draft?", $"Your unsaved prompts for '{draftCategories[i]}' are lost.", "Discard", "Cancel"))
                        {
                            RemoveDraft(i);
                            hasUnsavedChanges = Unsaved;
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
        }

        private void OnGUI()
        {
            // A waiting draft cannot be saved, only kept or discarded, so closing the window says so.
            saveChangesMessage = draftCategories.Count == 0 ? SaveMessage
                : SaveMessage + $" {draftCategories.Count} draft(s) from before a reload are still waiting to be kept or discarded, and are lost when the window closes.";

            DrawToolbar();
            DrawWaitingDrafts();

            if (!Ready)
            {
                DrawStart();
                return;
            }

            foreach (string difference in PromptSetChecks.CompareWithProfile(set, profile))
            {
                EditorGUILayout.HelpBox(difference + ". Nothing is changed here: fix the profile or the set file.", MessageType.Warning);
            }

            // Only edits block the other modes: a waiting draft cannot be saved, and stays offered above.
            if (mode != Mode.Write && writeView != null && writeView.HasUnsavedChanges)
            {
                EditorGUILayout.HelpBox("Save the prompts you changed in Write mode first.", MessageType.Info);
                if (GUILayout.Button("Save", GUILayout.Width(80)))
                {
                    SaveChanges();
                    Reload();
                    hasUnsavedChanges = Unsaved;
                }

                return;
            }

            switch (mode)
            {
                case Mode.Write:
                    writeView.Draw(position);
                    hasUnsavedChanges = Unsaved;
                    break;
                case Mode.Label:
                    DrawLabel();
                    break;
                case Mode.Agreement:
                    agreementView.Draw();
                    break;
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                ToolbarLabel("Set", SetTip, 26);
                var chosenSet = (TextAsset)EditorGUILayout.ObjectField(setAsset, typeof(TextAsset), false, GUILayout.Width(140));
                Tooltip(SetTip);
                ToolbarLabel("Profile", ProfileTip, 44);
                var chosenProfile = (AgentProfile)EditorGUILayout.ObjectField(profile, typeof(AgentProfile), false, GUILayout.Width(140));
                Tooltip(ProfileTip);

                if (chosenSet != setAsset)
                {
                    // What is drawn below the toolbar changes with the set, so this pass ends here. The
                    // fields' widths leave Reload room at the window's 640 px minimum.
                    OpenSet(chosenSet);
                    GUIUtility.ExitGUI();
                }
                else if (chosenProfile != profile)
                {
                    // Only the checks depend on the profile, so nothing is reloaded and nothing unsaved is lost.
                    profile = chosenProfile;
                    RememberProfile();
                }

                ToolbarLabel("Your name", NameTip, 64);
                EditorGUI.BeginChangeCheck();
                you = EditorGUILayout.TextField(you, EditorStyles.toolbarTextField, GUILayout.Width(90));
                Tooltip(NameTip);
                if (GUILayout.Button(new GUIContent("▾", NamesTip), EditorStyles.toolbarDropDown, GUILayout.Width(18)))
                {
                    ShowKnownNames();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    // Not a reload: that would drop what is being written. Label mode rebuilds its
                    // queue for the new name on its own.
                    prefs.Annotator = you;
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Reload", ReloadTip), EditorStyles.toolbarButton))
                {
                    if (!hasUnsavedChanges || EditorUtility.DisplayDialog("Reload the prompt set?", "Your unsaved changes are lost.", "Reload", "Cancel"))
                    {
                        // Drafts still waiting from a reload stay offered: a reload is how a
                        // fixed file is read again.
                        Reload();
                        hasUnsavedChanges = Unsaved;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(!Ready))
                {
                    var chosen = (Mode)GUILayout.Toolbar((int)mode, ModeTabs, EditorStyles.toolbarButton, GUILayout.Width(220));
                    if (chosen != mode)
                    {
                        mode = chosen;
                        GUI.FocusControl(null);
                        RefreshLabels();
                    }
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.LabelField(Ready ? $"{ModeNames[(int)mode]}: {ModeDescriptions[(int)mode]}." : NotReadyReason(), EditorStyles.wordWrappedMiniLabel);
        }

        private string NotReadyReason()
        {
            string missing = set == null && profile == null ? "a set and a profile are"
                : set == null ? "a set is"
                : "a profile is";
            return $"The tabs open once {missing} chosen: the steps are below.";
        }

        private static void ToolbarLabel(string text, string tooltip, float width)
        {
            GUILayout.Label(new GUIContent(text, tooltip), GUILayout.Width(width));
        }

        // An object or text field takes no tooltip of its own, so one is laid over the field just
        // drawn. A label uses no mouse events, so clicks and drags still reach the field.
        private static void Tooltip(string tooltip)
        {
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", tooltip));
        }

        /// <summary>
        /// The window before a set and a profile are chosen: three numbered steps, the first with
        /// a button that opens the project's own set and the profile beside it (#158).
        /// </summary>
        private void DrawStart()
        {
            startScroll = EditorGUILayout.BeginScrollView(startScroll);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Start here", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("Three steps, once: the window remembers all three on this machine.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();

            using (Step(1, set != null, "Choose the set",
                       set != null
                           ? $"{AssetDatabase.GetAssetPath(setAsset)}: {set.Prompts.Count()} prompts in {set.Categories.Count} categories."
                           : "The set file, the agent.json beside a folder of prompts. It names the agent's actions and targets, and the categories."))
            {
                if (loadError != null)
                {
                    EditorGUILayout.HelpBox(loadError, MessageType.Error);
                }

                var sets = foundSets ?? (foundSets = PromptSetStart.FindSets());
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(sets.Count == 0))
                    {
                        var open = new GUIContent("Open the project's evaluation set",
                            sets.Count == 0 ? "There is no agent.json under Assets/ yet." : "Opens the set and the profile beside it, if there is one.");
                        if (GUILayout.Button(open, GUILayout.Width(240)))
                        {
                            OpenProjectSet(sets);
                            GUIUtility.ExitGUI();
                        }
                    }

                    GUILayout.Label(
                        sets.Count == 0 ? "No agent.json under Assets/ yet."
                        : sets.Count == 1 ? sets[0]
                        : $"{sets.Count} found: you choose which.",
                        EditorStyles.miniLabel);
                }

                EditorGUILayout.LabelField("Or drag a set file into Set above, or start a new one from the profile:", EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(profile == null))
                    {
                        if (GUILayout.Button(new GUIContent("New set from this profile…", "Writes a set file with the profile's actions and none. Add targets by hand."), GUILayout.Width(240)))
                        {
                            CreateSet();
                            GUIUtility.ExitGUI();
                        }
                    }

                    if (profile == null)
                    {
                        GUILayout.Label("Choose the profile first (step 2).", EditorStyles.miniLabel);
                    }
                }
            }

            using (Step(2, profile != null, "Choose the profile",
                       profile != null
                           ? $"{profile.name}: {profile.Actions.Count(a => a != null)} actions."
                           : "The AgentProfile the set is measured against. Write offers its actions, and every prompt is checked against its few-shot examples (hard rule 4)."))
            {
                if (profile == null)
                {
                    EditorGUILayout.LabelField(
                        set != null ? "Drag the agent's AgentProfile into Profile above." : "Opening the set picks the profile beside it, if there is one. Otherwise, drag yours into Profile above.",
                        EditorStyles.wordWrappedMiniLabel);
                }
            }

            bool named = PromptSet.ValidateAnnotator(you, out string nameProblem);
            using (Step(3, named, "Type your name",
                       named ? you + "." : "In Your name above. It goes on the prompts you write, and your answers in Label are saved under it."))
            {
                if (!named && !string.IsNullOrEmpty(you))
                {
                    EditorGUILayout.LabelField(nameProblem + ".", EditorStyles.wordWrappedMiniLabel);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Then pick a tab", EditorStyles.boldLabel);
            for (int i = 0; i < ModeNames.Length; i++)
            {
                EditorGUILayout.LabelField($"{ModeNames[i]}: {ModeDescriptions[i]}.", EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("More in Documentation~/evaluation.md, in the Agenerela package.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        private static GUI.Scope Step(int number, bool done, string title, string detail)
        {
            var scope = new StepScope();
            GUILayout.Label(done ? "✓" : number + ".", EditorStyles.boldLabel, GUILayout.Width(22));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(detail, EditorStyles.wordWrappedLabel);
            return scope;
        }

        // One step: a number, then its text and controls in a column beside it. A GUI.Scope, so
        // GUIUtility.ExitGUI inside a step leaves the layout groups alone.
        private sealed class StepScope : GUI.Scope
        {
            internal StepScope()
            {
                EditorGUILayout.BeginHorizontal();
            }

            protected override void CloseScope()
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }
        }

        private void OpenProjectSet(IReadOnlyList<string> sets)
        {
            if (sets.Count == 1)
            {
                OpenSet(AssetDatabase.LoadAssetAtPath<TextAsset>(sets[0]));
                return;
            }

            // A division slash in place of each '/': GenericMenu would make each one a submenu.
            var menu = new GenericMenu();
            foreach (string path in sets)
            {
                menu.AddItem(new GUIContent(path.Replace('/', '∕')), false, () => OpenSet(AssetDatabase.LoadAssetAtPath<TextAsset>(path)));
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// Switches to <paramref name="chosen"/>, with the profile <see cref="PromptSetStart.ProfileWhenOpening"/>
        /// picks. Nothing changes until a switch that loses unsaved changes is confirmed: a
        /// cancelled one leaves the window showing, and saving to, the set it was editing. The set
        /// already open is not reloaded, so nothing unsaved is lost; only a missing profile is filled.
        /// </summary>
        private void OpenSet(TextAsset chosen)
        {
            if (chosen == setAsset)
            {
                FillMissingProfile();
                return;
            }

            if ((hasUnsavedChanges || draftCategories.Count > 0)
                && !EditorUtility.DisplayDialog("Open another set?", "Your unsaved changes are lost.", "Open", "Cancel"))
            {
                return;
            }

            setAsset = chosen;
            hasUnsavedChanges = false;
            ClearDraft();
            prefs.SetGuid = GuidOf(setAsset);

            var remembered = LoadByGuid<AgentProfile>(prefs.ProfileGuidFor(GuidOf(setAsset)));
            Reload();
            if (setAsset != null)
            {
                profile = PromptSetStart.ProfileWhenOpening(false, profile, remembered, ProfileBesideSet);
            }

            RememberProfile();
            GUI.FocusControl(null);
        }

        /// <summary>For the set already open and no profile: the remembered one, or the one beside it.</summary>
        private void FillMissingProfile()
        {
            if (setAsset == null || profile != null)
            {
                return;
            }

            profile = PromptSetStart.ProfileWhenOpening(true, null, LoadByGuid<AgentProfile>(prefs.ProfileGuidFor(GuidOf(setAsset))), ProfileBesideSet);
            if (profile != null)
            {
                RememberProfile();
            }
        }

        private AgentProfile ProfileBesideSet()
        {
            return setAsset == null ? null : PromptSetStart.FindProfileBeside(AssetDatabase.GetAssetPath(setAsset), set);
        }

        private void DrawLabel()
        {
            if (!PromptSet.ValidateAnnotator(you, out string problem))
            {
                EditorGUILayout.HelpBox(problem + ". Type it into Your name in the toolbar, or pick it from ▾.", MessageType.Info);
                return;
            }

            if (labelView == null || labelView.Annotator != you)
            {
                // Everything Label mode is given: prompts with no answer in them, and the choices.
                labelView = new PromptSetLabelView(you, BlindPrompt.Queue(set, labels, you), LabelChoices.From(set), Record,
                    LabelProgress.TotalFor(set, you));
            }

            labelView.Draw();
        }

        private bool Record(BlindPrompt prompt, LabelAnswer answer)
        {
            bool recorded = set.RecordLabel(you, prompt.Id, answer);
            AssetDatabase.Refresh();
            labels = set.LoadLabels();
            agreementView = new PromptSetAgreementView(set, labels, AfterSettle);
            return recorded;
        }

        private void AfterSettle()
        {
            AssetDatabase.Refresh();
            Reload();
        }

        private void Reload()
        {
            set = null;
            loadError = null;
            labelView = null;
            if (setAsset != null)
            {
                string path = AssetDatabase.GetAssetPath(setAsset);
                try
                {
                    set = PromptSet.Load(Path.GetFullPath(path));
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
                {
                    loadError = e.Message;
                }

                if (profile == null)
                {
                    profile = LoadByGuid<AgentProfile>(prefs.ProfileGuidFor(GuidOf(setAsset)));
                }
            }

            if (set != null)
            {
                writeView = new PromptSetWriteView(set, () => profile, () => you, OnFilesWritten);
                RefreshLabels();
            }

            Repaint();
        }

        private void RefreshLabels()
        {
            if (set == null)
            {
                return;
            }

            labels = set.LoadLabels();
            labelView = null;
            agreementView = new PromptSetAgreementView(set, labels, AfterSettle);
            writeView?.SetLabels(labels);
        }

        private void OnFilesWritten()
        {
            AssetDatabase.Refresh();
            hasUnsavedChanges = Unsaved;
        }

        private void CreateSet()
        {
            string path = EditorUtility.SaveFilePanelInProject("New prompt set", "agent", "json",
                "The set file. Its prompts and labels go in folders beside it.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            PromptSet.CreateFor(profile, Path.GetFullPath(path)).Save();
            AssetDatabase.ImportAsset(path);
            setAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            prefs.SetGuid = GuidOf(setAsset);
            foundSets = null;
            RememberProfile();
            Reload();
        }

        private void ShowKnownNames()
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            if (set != null)
            {
                foreach (var prompt in set.Prompts)
                {
                    names.UnionWith(prompt.LabelledBy ?? new List<string>());
                }

                names.UnionWith(labels.Select(l => l.Annotator));
            }

            var menu = new GenericMenu();
            foreach (string name in names)
            {
                menu.AddItem(new GUIContent(name), name == you, () =>
                {
                    you = name;
                    prefs.Annotator = you;
                    GUI.FocusControl(null);
                });
            }

            if (names.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("Nobody has written or labelled a prompt yet"));
            }

            menu.ShowAsContext();
        }

        private void RememberProfile()
        {
            prefs.RememberProfile(GuidOf(setAsset), GuidOf(profile));
        }

        private static string GuidOf(UnityEngine.Object asset)
        {
            return asset == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        }

        private static T LoadByGuid<T>(string guid) where T : UnityEngine.Object
        {
            return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
