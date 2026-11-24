using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Agenerela.Editor.UI;
using Agenerela.Evaluation;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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
    /// <para>
    /// Built in UI Toolkit from <c>PromptSetWindow.uxml</c> and each view's own UXML, on the #167
    /// kit (#160). <see cref="PromptSetUi"/> shows the toolbar, the tabs and the start page; this
    /// class holds the set, the profile and the drafts, and hands them over in <see cref="Refresh"/>.
    /// </para>
    /// </remarks>
    public sealed class PromptSetWindow : EditorWindow
    {
        private PromptSetPrefs prefs;
        private IReadOnlyList<string> foundSets;

        [SerializeField] private TextAsset setAsset;
        [SerializeField] private AgentProfile profile;
        [SerializeField] private string you = "";
        [SerializeField] private PromptSetMode mode;

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
        private PromptSetUi ui;

        // The agreement cohort beside the set (#148), null when there is none; why it could not be
        // read, when it could not; and whether Label mode asks the whole set instead.
        private AgreementCohort cohort;
        private string cohortError;
        private bool labelWholeSet;

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
            Refresh();
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
            Refresh();
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
        private IReadOnlyList<PromptSetUi.WaitingDraft> WaitingDrafts()
        {
            var waiting = new List<PromptSetUi.WaitingDraft>();
            for (int i = 0; i < draftCategories.Count; i++)
            {
                var category = set != null ? set.FindCategory(draftCategories[i]) : null;
                string why = set == null ? "the set did not load"
                    : category == null ? "the set no longer has this category"
                    : "its file changed on disk since you edited it";
                waiting.Add(new PromptSetUi.WaitingDraft(draftCategories[i], why, category != null && writeView != null));
            }

            return waiting;
        }

        private void KeepDraft(int i)
        {
            var category = set?.FindCategory(draftCategories[i]);
            if (category != null && writeView != null && ApplyDraft(i, category, Fingerprint(set, category)))
            {
                RemoveDraft(i);
            }

            Refresh();
        }

        private void DiscardDraft(int i)
        {
            if (EditorUtility.DisplayDialog("Discard the draft?", $"Your unsaved prompts for '{draftCategories[i]}' are lost.", "Discard", "Cancel"))
            {
                RemoveDraft(i);
            }

            Refresh();
        }

        private void CreateGUI()
        {
            ui = new PromptSetUi(PromptSetUi.Load());
            rootVisualElement.Add(ui.Root);

            ui.SetChanged += chosen =>
            {
                OpenSet(chosen);
                Refresh();
            };
            ui.ProfileChanged += chosen =>
            {
                // Only the checks depend on the profile, so nothing is reloaded and nothing unsaved is lost.
                profile = chosen;
                RememberProfile();
                writeView?.Refresh();
                Refresh();
            };
            ui.NameChanged += typed =>
            {
                // Not a reload: that would drop what is being written. Label mode rebuilds its
                // queue for the new name on its own.
                you = typed ?? "";
                prefs.Annotator = you;
                writeView?.Refresh();
                Refresh();
            };
            ui.ModeChanged += chosen =>
            {
                mode = chosen;
                RefreshLabels();
                Refresh();
            };
            ui.Reload += () =>
            {
                if (!hasUnsavedChanges || EditorUtility.DisplayDialog("Reload the prompt set?", "Your unsaved changes are lost.", "Reload", "Cancel"))
                {
                    // Drafts still waiting from a reload stay offered: a reload is how a fixed
                    // file is read again.
                    Reload();
                }

                Refresh();
            };
            ui.SaveFirst += () =>
            {
                SaveChanges();
                Reload();
                Refresh();
            };
            ui.OpenProjectSet += anchor => OpenProjectSet(PromptSetStart.FindSets(), anchor);
            ui.NewSet += CreateSet;
            ui.KeepDraft += KeepDraft;
            ui.WholeSetChanged += whole =>
            {
                labelWholeSet = whole;
                labelView = null;
                Refresh();
            };
            ui.DiscardDraft += DiscardDraft;

            Refresh();
        }

        /// <summary>
        /// Hands the tree everything it shows, and places the view for the tab chosen. Before
        /// <c>CreateGUI</c>, nothing to do.
        /// </summary>
        private void Refresh()
        {
            // A waiting draft cannot be saved, only kept or discarded, so closing the window says so.
            saveChangesMessage = draftCategories.Count == 0 ? SaveMessage
                : SaveMessage + $" {draftCategories.Count} draft(s) from before a reload are still waiting to be kept or discarded, and are lost when the window closes.";
            hasUnsavedChanges = Unsaved;

            if (ui == null)
            {
                return;
            }

            var state = new PromptSetUi.State
            {
                SetAsset = setAsset,
                Profile = profile,
                You = you,
                SetLoaded = set != null,
                LoadError = loadError,
                SetSummary = set != null ? $"{AssetDatabase.GetAssetPath(setAsset)}: {set.Prompts.Count()} prompts in {set.Categories.Count} categories." : null,
                FoundSets = foundSets ?? (foundSets = PromptSetStart.FindSets()),
                KnownNames = KnownNames(),
                Mode = mode,
                WriteUnsaved = writeView != null && writeView.HasUnsavedChanges,
                ProfileDifferences = set != null && profile != null ? PromptSetChecks.CompareWithProfile(set, profile) : Array.Empty<string>(),
                Drafts = WaitingDrafts(),
                CohortLine = CohortLine(),
                WholeSet = labelWholeSet,
                CohortError = cohortError != null
                    ? $"{AgreementCohort.FileName} could not be read, so Label mode asks the whole set: {cohortError}"
                    : null,
            };

            ui.Show(state);
            if (!state.Ready)
            {
                return;
            }

            switch (mode)
            {
                case PromptSetMode.Write:
                    ui.Place(PromptSetMode.Write, writeView.Root);
                    break;
                case PromptSetMode.Label:
                    ui.Place(PromptSetMode.Label, LabelRoot());
                    break;
                case PromptSetMode.Agreement:
                    ui.Place(PromptSetMode.Agreement, agreementView.Root);
                    break;
            }
        }

        private void OpenProjectSet(IReadOnlyList<string> sets, VisualElement anchor)
        {
            if (sets.Count == 1)
            {
                OpenSet(AssetDatabase.LoadAssetAtPath<TextAsset>(sets[0]));
                Refresh();
                return;
            }

            // A division slash in place of each '/', which a menu would make a submenu.
            var menu = new GenericDropdownMenu();
            foreach (string path in sets)
            {
                menu.AddItem(path.Replace('/', '∕'), false, () =>
                {
                    OpenSet(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                    Refresh();
                });
            }

            menu.DropDown(anchor.worldBound, anchor, DropdownMenuSizeMode.Auto);
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

        /// <summary>
        /// Label mode's view, made for <see cref="you"/> when the name changes or the labels are read
        /// again, or a note saying why there is none.
        /// </summary>
        private VisualElement LabelRoot()
        {
            if (!PromptSet.ValidateAnnotator(you, out string problem))
            {
                labelView = null;
                var box = new ProblemBox(problem + ". Type it into Your name in the toolbar, or pick it from ▾.");
                box.style.marginTop = 8;
                return box;
            }

            if (labelView == null || labelView.Annotator != you)
            {
                // Everything Label mode is given: prompts with no answer in them, and the choices.
                var asked = Asked();
                labelView = new PromptSetLabelView(you, BlindPrompt.Queue(set, labels, you, cohort: asked), LabelChoices.From(set), Record,
                    LabelProgress.TotalFor(set, you, asked));
            }

            return labelView.Root;
        }

        // Whose prompts Label mode asks: the agreement cohort's when one sits beside the set (#148),
        // so a second annotator is not asked the whole grown set, unless Whole set is chosen.
        // Null for the whole set. The cohort holds ids only, no answers.
        private AgreementCohort Asked() => labelWholeSet || cohortError != null ? null : cohort;

        // The line above Label saying which prompts it asks; null with no cohort beside the set.
        private string CohortLine()
        {
            if (cohort == null || set == null)
            {
                return null;
            }

            int inSet = set.Prompts.Count(p => cohort.Contains(p.Id));
            string line = labelWholeSet
                ? $"Labelling the whole set, {set.Prompts.Count()} prompts, not only the {inSet} of the agreement cohort."
                : $"Labelling the {inSet} prompts of the agreement cohort{(cohort.Issue != null ? $" (#{cohort.Issue})" : "")}, from {AgreementCohort.FileName}.";
            if (inSet < cohort.Count)
            {
                line += $" {cohort.Count - inSet} of its ids are not in the set.";
            }

            return line;
        }

        private bool Record(BlindPrompt prompt, LabelAnswer answer)
        {
            bool recorded = set.RecordLabel(you, prompt.Id, answer);
            AssetDatabase.Refresh();
            labels = set.LoadLabels();
            agreementView = new PromptSetAgreementView(set, labels, AfterSettle);
            writeView?.SetLabels(labels);
            return recorded;
        }

        private void AfterSettle()
        {
            AssetDatabase.Refresh();
            Reload();
            Refresh();
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

            cohort = null;
            cohortError = null;
            if (set != null)
            {
                try
                {
                    cohort = AgreementCohort.FindBeside(set);
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
                {
                    cohortError = e.Message;
                }

                writeView = new PromptSetWriteView(set, () => profile, () => you, OnFilesWritten, OnEdited);
                RefreshLabels();
            }
            else
            {
                writeView = null;
                agreementView = null;
            }

            Refresh();
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
            Refresh();
        }

        // An edit in Write mode: the unsaved flag, and the tabs' gate, follow it.
        private void OnEdited()
        {
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
            Refresh();
        }

        /// <summary>Everyone who wrote or labelled a prompt in the set, for the ▾ beside Your name.</summary>
        private IReadOnlyList<string> KnownNames()
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

            return names.ToList();
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
