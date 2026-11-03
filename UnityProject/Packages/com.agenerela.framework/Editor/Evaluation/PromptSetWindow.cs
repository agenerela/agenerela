using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agenerela.Evaluation;
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
            saveChangesMessage = "The prompt set has unsaved changes. Save them?";
            if (setAsset == null)
            {
                setAsset = LoadByGuid<TextAsset>(EditorPrefs.GetString(SetPref, ""));
            }

            if (string.IsNullOrEmpty(you))
            {
                you = EditorPrefs.GetString(YouPref, "");
            }

            Reload();
        }

        public override void SaveChanges()
        {
            writeView?.SaveAll();
            hasUnsavedChanges = false;
            base.SaveChanges();
        }

        private void OnGUI()
        {
            DrawToolbar();

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

            if (mode != Mode.Write && hasUnsavedChanges)
            {
                EditorGUILayout.HelpBox("Save the prompts you changed in Write mode first.", MessageType.Info);
                if (GUILayout.Button("Save", GUILayout.Width(80)))
                {
                    SaveChanges();
                    Reload();
                }

                return;
            }

            switch (mode)
            {
                case Mode.Write:
                    writeView.Draw(position);
                    hasUnsavedChanges = writeView.HasUnsavedChanges;
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
                EditorGUI.BeginChangeCheck();
                GUILayout.Label("Set", GUILayout.Width(26));
                setAsset = (TextAsset)EditorGUILayout.ObjectField(setAsset, typeof(TextAsset), false, GUILayout.Width(150));
                GUILayout.Label("Profile", GUILayout.Width(44));
                var chosenProfile = (AgentProfile)EditorGUILayout.ObjectField(profile, typeof(AgentProfile), false, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                {
                    if (chosenProfile != profile)
                    {
                        profile = chosenProfile;
                        RememberProfile();
                    }
                    else
                    {
                        // A set remembers the profile it was last measured against on this machine.
                        EditorPrefs.SetString(SetPref, GuidOf(setAsset));
                        profile = LoadByGuid<AgentProfile>(EditorPrefs.GetString(ProfilePref + GuidOf(setAsset), "")) ?? profile;
                        RememberProfile();
                    }

                    if (!hasUnsavedChanges || EditorUtility.DisplayDialog("Open another set?", "Your unsaved changes are lost.", "Open", "Cancel"))
                    {
                        hasUnsavedChanges = false;
                        Reload();
                    }
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
                        hasUnsavedChanges = false;
                        Reload();
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
            hasUnsavedChanges = writeView.HasUnsavedChanges;
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
