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
    /// The set file is the <c>agent.json</c> a folder of prompts sits beside; the profile and your
    /// name are remembered on this machine, not in the set.
    /// </para>
    /// </remarks>
    public sealed class PromptSetWindow : EditorWindow
    {
        private enum Mode { Write, Label, Agreement }

        private static readonly string[] ModeNames = { "Write", "Label", "Agreement" };

        private const string SetPref = "Agenerela.PromptSet.Set";
        private const string YouPref = "Agenerela.PromptSet.You";
        private const string ProfilePref = "Agenerela.PromptSet.Profile.";

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
            if (setAsset == null)
            {
                setAsset = LoadByGuid<TextAsset>(EditorPrefs.GetString(SetPref, ""));
            }

            if (string.IsNullOrEmpty(you))
            {
                you = EditorPrefs.GetString(YouPref, "");
            }

            AssemblyReloadEvents.beforeAssemblyReload += StashDraft;
            Reload();
            RestoreDraft();
        }

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

            if (set == null)
            {
                DrawNoSet();
                return;
            }

            if (profile != null)
            {
                foreach (string difference in PromptSetChecks.CompareWithProfile(set, profile))
                {
                    EditorGUILayout.HelpBox(difference + ". Nothing is changed here: fix the profile or the set file.", MessageType.Warning);
                }
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
                GUILayout.Label("Set", GUILayout.Width(26));
                var chosenSet = (TextAsset)EditorGUILayout.ObjectField(setAsset, typeof(TextAsset), false, GUILayout.Width(150));
                GUILayout.Label("Profile", GUILayout.Width(44));
                var chosenProfile = (AgentProfile)EditorGUILayout.ObjectField(profile, typeof(AgentProfile), false, GUILayout.Width(150));

                // Nothing changes until the switch is confirmed: a cancelled switch leaves the
                // window showing, and saving to, the set it was editing.
                if (chosenSet != setAsset
                    && (!hasUnsavedChanges && draftCategories.Count == 0
                        || EditorUtility.DisplayDialog("Open another set?", "Your unsaved changes are lost.", "Open", "Cancel")))
                {
                    setAsset = chosenSet;
                    hasUnsavedChanges = false;
                    ClearDraft();

                    // A set remembers the profile it was last measured against on this machine.
                    EditorPrefs.SetString(SetPref, GuidOf(setAsset));
                    profile = LoadByGuid<AgentProfile>(EditorPrefs.GetString(ProfilePref + GuidOf(setAsset), "")) ?? profile;
                    RememberProfile();
                    Reload();
                }
                else if (chosenProfile != profile)
                {
                    // Only the checks depend on the profile, so nothing is reloaded and nothing unsaved is lost.
                    profile = chosenProfile;
                    RememberProfile();
                }

                GUILayout.Label("You", GUILayout.Width(28));
                EditorGUI.BeginChangeCheck();
                you = EditorGUILayout.TextField(you, EditorStyles.toolbarTextField, GUILayout.Width(90));
                if (GUILayout.Button("▾", EditorStyles.toolbarDropDown, GUILayout.Width(18)))
                {
                    ShowKnownNames();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    // Not a reload: that would drop what is being written. Label mode rebuilds its
                    // queue for the new name on its own.
                    EditorPrefs.SetString(YouPref, you);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
                {
                    if (!hasUnsavedChanges || EditorUtility.DisplayDialog("Reload the prompt set?", "Your unsaved changes are lost.", "Reload", "Cancel"))
                    {
                        // Drafts still waiting from a reload stay offered: a reload is how a
                        // fixed file is read again.
                        Reload();
                        hasUnsavedChanges = Unsaved;
                    }
                }

                var chosen = (Mode)GUILayout.Toolbar((int)mode, ModeNames, EditorStyles.toolbarButton, GUILayout.Width(220));
                if (chosen != mode)
                {
                    mode = chosen;
                    GUI.FocusControl(null);
                    RefreshLabels();
                }
            }
        }

        private void DrawNoSet()
        {
            if (loadError != null)
            {
                EditorGUILayout.HelpBox(loadError, MessageType.Error);
            }

            EditorGUILayout.HelpBox(
                "Choose a set file, the agent.json beside a folder of prompts, or make a new one from the profile chosen above. " +
                "Documentation~/evaluation.md in the package says how to write a set.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(profile == null))
            {
                if (GUILayout.Button("New set from this profile…", GUILayout.Width(200)))
                {
                    CreateSet();
                }
            }
        }

        private void DrawLabel()
        {
            if (!PromptSet.ValidateAnnotator(you, out string problem))
            {
                EditorGUILayout.HelpBox(problem + ". Type your name in the toolbar, or pick it from ▾.", MessageType.Info);
                return;
            }

            if (labelView == null || labelView.Annotator != you)
            {
                // Everything Label mode is given: prompts with no answer in them, and the choices.
                labelView = new PromptSetLabelView(you, BlindPrompt.Queue(set, labels, you), LabelChoices.From(set), Record);
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
                    profile = LoadByGuid<AgentProfile>(EditorPrefs.GetString(ProfilePref + GuidOf(setAsset), ""));
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
            EditorPrefs.SetString(SetPref, GuidOf(setAsset));
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
                    EditorPrefs.SetString(YouPref, you);
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
            if (setAsset != null)
            {
                EditorPrefs.SetString(ProfilePref + GuidOf(setAsset), GuidOf(profile));
            }
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
