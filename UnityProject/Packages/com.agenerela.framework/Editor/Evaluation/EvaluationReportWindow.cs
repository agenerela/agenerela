using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Agenerela.Editor.UI;
using Agenerela.Evaluation;
using Agenerela.Providers;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// <b>Window → Agenerela → Evaluation Report</b>: runs an A/B over a labelled prompt set and
    /// reads its report in the Editor (#146, screen 4). The report is never one percentage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One scrolling column, numbered as screen 4 is: ① the run set-up, ② the five outcomes per arm,
    /// ③ the noise check, ④ accuracy per category, ⑤ the answers that were not correct, ⑥ the
    /// labellers' agreement and export. Until a set, a profile and two arms are chosen it shows the
    /// steps to start, and <b>Use the project's set, profile and arms</b> fills all of them at once.
    /// </para>
    /// <para>
    /// <b>Run A/B</b> runs <see cref="EvalRunner"/> without blocking the Editor, and can be
    /// cancelled. Each run is saved to <c>Logs/Evaluation/</c>, a cancelled one marked so, and
    /// <b>Open report…</b> reads any saved one back: what is shown comes only from what the report
    /// saves (<see cref="EvaluationReportView"/>). The choices are remembered on this machine, per
    /// project (<see cref="EvaluationReportPrefs"/>).
    /// </para>
    /// <para>
    /// Built in UI Toolkit on the #167 kit (#179): the tree is <see cref="EvaluationReportUi"/>, from
    /// <c>EvaluationReportWindow.uxml</c> and <c>.uss</c>. This class holds the set-up, the run and
    /// the report, and hands the tree all of it whenever one changes.
    /// </para>
    /// </remarks>
    public sealed class EvaluationReportWindow : EditorWindow
    {
        private const string ProjectSettingsPath = "Project/Agenerela";

        [SerializeField] private TextAsset setAsset;
        [SerializeField] private AgentProfile profile;
        [SerializeField] private ProviderConfig configA, configB;
        [SerializeField] private string nameA = "", nameB = "";
        [SerializeField] private bool checkTargetA = true, checkTargetB = true;
        [SerializeField] private EvalArmOrder order = EvalArmOrder.Interleaved;
        [SerializeField] private string shownReport = "";

        private EvaluationReportPrefs prefs;
        private readonly EvaluationRunSetup setup = new EvaluationRunSetup();
        private EvaluationReportUi ui;
        private EvaluationReportView view;
        private AgreementReport agreement;
        private string agreementNote;

        // The run in progress, if any.
        private CancellationTokenSource running;
        private EvalProgress progress;
        private double runStarted;
        private double lastTick;
        private string runMessage;
        private ProblemSeverity? runSeverity;

        /// <summary>The size the window opens at when it is not already open: wide enough for a report's tables without wrapping.</summary>
        internal static readonly Vector2 DefaultSize = new Vector2(780, 900);

        [MenuItem("Window/Agenerela/Evaluation Report")]
        public static void Open()
        {
            bool fresh = !HasOpenInstances<EvaluationReportWindow>();
            var window = GetWindow<EvaluationReportWindow>();
            window.titleContent = new GUIContent("Evaluation Report");
            window.minSize = new Vector2(560, 420);
            if (fresh)
            {
                // Unity opens a new window small; one already open keeps the size it was given.
                float height = Mathf.Min(DefaultSize.y, Screen.currentResolution.height - 120);
                window.position = new Rect(window.position.x, window.position.y, DefaultSize.x, Mathf.Max(420, height));
            }

            window.Show();
        }

        /// <summary>Where each run is saved: <c>Logs/Evaluation/</c> in the project, which git ignores.</summary>
        internal static string ReportsFolder => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "Evaluation");

        private bool Running => running != null;

        private void OnEnable()
        {
            prefs = EvaluationReportPrefs.ForThisProject();
            if (setAsset == null) setAsset = LoadByGuid<TextAsset>(prefs.SetGuid);
            if (profile == null) profile = LoadByGuid<AgentProfile>(prefs.ProfileGuid);
            if (configA == null) configA = LoadByGuid<ProviderConfig>(prefs.ConfigGuid(0));
            if (configB == null) configB = LoadByGuid<ProviderConfig>(prefs.ConfigGuid(1));
            if (string.IsNullOrEmpty(nameA)) nameA = prefs.Name(0);
            if (string.IsNullOrEmpty(nameB)) nameB = prefs.Name(1);
            checkTargetA = prefs.CheckTarget(0) ?? checkTargetA;
            checkTargetB = prefs.CheckTarget(1) ?? checkTargetB;
            if (Enum.TryParse(prefs.Order, out EvalArmOrder remembered)) order = remembered;

            ToSetup();
            if (string.IsNullOrEmpty(nameA) && string.IsNullOrEmpty(nameB) && (configA != null || configB != null))
            {
                setup.NameArmsFromConfigs();
                FromSetup();
            }

            LoadSet();

            if (string.IsNullOrEmpty(shownReport)) shownReport = prefs.LastReport;
            if (!string.IsNullOrEmpty(shownReport) && File.Exists(shownReport))
            {
                TryShow(shownReport, false);
            }

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void CreateGUI()
        {
            ui = new EvaluationReportUi(EvaluationReportUi.Load());
            rootVisualElement.Add(ui.Root);

            ui.UseDefaults += UseProjectDefaults;
            ui.OpenReport += OpenReport;
            ui.EditSet += PromptSetWindow.Open;
            ui.ReviewDisagreements += PromptSetWindow.Open;
            ui.OpenProjectSettings += () => SettingsService.OpenProjectSettings(ProjectSettingsPath);
            ui.Run += Run;
            ui.Cancel += () => running?.Cancel();
            ui.CopySummary += CopySummary;
            ui.ShowFiles += () => EditorUtility.RevealInFinder(Path.Combine(ShownFolder, EvalReport.MarkdownFile));
            ui.SaveCopy += SaveCopy;

            ui.SetChanged += chosen =>
            {
                setAsset = chosen;
                LoadSet();
                if (profile == null) profile = ProfileFor(setAsset, setup.Set);
                Remember();
            };
            ui.ProfileChanged += chosen =>
            {
                profile = chosen;
                Remember();
            };
            ui.ConfigChanged += (arm, chosen) =>
            {
                ToSetup();
                setup.ChangeConfig(arm, chosen);
                FromSetup();
            };
            ui.NameChanged += (arm, typed) =>
            {
                if (arm == 0) nameA = typed; else nameB = typed;
                Remember();
            };
            ui.GuardChanged += (arm, on) =>
            {
                if (arm == 0) checkTargetA = on; else checkTargetB = on;
                Remember();
            };
            ui.OrderChanged += chosen =>
            {
                order = chosen;
                Remember();
            };

            Refresh();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= Tick;

            // A closed window cannot show its run, so the run stops; its partial report is still saved.
            running?.Cancel();
        }

        // The set's prompts, and its label files, may have changed in the Prompt Set window meanwhile.
        private void OnFocus()
        {
            if (Running)
            {
                return;
            }

            if (setAsset != null)
            {
                LoadSet();
            }

            if (view != null)
            {
                ReadAgreement(view.Report);
            }

            Refresh();
        }

        // A set file added, moved or deleted, or a config changed, can change what the steps say.
        private void OnProjectChange() => Refresh();

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && Running)
            {
                running.Cancel();
            }

            Refresh();
        }

        // ---- State ------------------------------------------------------------------------------

        private void ToSetup()
        {
            setup.Profile = profile;
            setup.A.Config = configA;
            setup.B.Config = configB;
            setup.A.Name = nameA ?? "";
            setup.B.Name = nameB ?? "";
            setup.A.CheckTarget = checkTargetA;
            setup.B.CheckTarget = checkTargetB;
            setup.ChosenOrder = order;
        }

        private void FromSetup()
        {
            configA = setup.A.Config;
            configB = setup.B.Config;
            nameA = setup.A.Name;
            nameB = setup.B.Name;
            checkTargetA = setup.A.CheckTarget;
            checkTargetB = setup.B.CheckTarget;
            Remember();
        }

        // Every change of choice is remembered at once, and shown.
        private void Remember()
        {
            prefs.SetGuid = GuidOf(setAsset);
            prefs.ProfileGuid = GuidOf(profile);
            prefs.SetConfigGuid(0, GuidOf(configA));
            prefs.SetConfigGuid(1, GuidOf(configB));
            prefs.SetName(0, nameA);
            prefs.SetName(1, nameB);
            prefs.SetCheckTarget(0, checkTargetA);
            prefs.SetCheckTarget(1, checkTargetB);
            prefs.Order = order.ToString();
            Refresh();
        }

        private void LoadSet()
        {
            setup.Set = null;
            setup.SetError = null;
            if (setAsset != null)
            {
                try
                {
                    setup.Set = PromptSet.Load(Path.GetFullPath(AssetDatabase.GetAssetPath(setAsset)));
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
                {
                    setup.SetError = e.Message;
                }
            }

            Refresh();
        }

        /// <summary>Hands the tree everything it shows. Before <c>CreateGUI</c>, nothing to do.</summary>
        private void Refresh()
        {
            if (ui == null)
            {
                return;
            }

            ToSetup();
            var settings = AgenerelaSettingsPage.Find();
            ui.Show(new EvaluationReportUi.State
            {
                SetAsset = setAsset,
                Setup = setup,
                WhyNotRunnable = Running ? null
                    : EditorApplication.isPlayingOrWillChangePlaymode ? "Leave Play mode first: entering or leaving it would stop the run."
                    : setup.WhyNotRunnable(),
                ArmsInSettings = settings != null && settings.EvaluationArmA != null && settings.EvaluationArmB != null,
                Running = Running,
                Progress = progress,
                Seconds = Running ? EditorApplication.timeSinceStartup - runStarted : 0,
                Message = runMessage,
                MessageSeverity = runSeverity,
                View = view,
                ShownPath = shownReport,
                Agreement = agreement,
                AgreementNote = agreementNote,
            });
        }

        /// <summary>The progress bar's words, such as "Asking · guards-on · 37 of 152 answers".</summary>
        internal static string ProgressText(EvalProgress p)
        {
            if (p == null)
            {
                return "Starting…";
            }

            string what;
            switch (p.Phase)
            {
                case EvalPhase.Checking: what = p.Arm == null ? "Checking what is loaded" : $"Checking {p.Arm}'s server"; break;
                case EvalPhase.WarmUp: what = $"Warming up {p.Arm}"; break;
                case EvalPhase.Unloading: what = $"Unloading {p.Arm}'s model"; break;
                default: what = $"Asking · {p.Arm}"; break;
            }

            return $"{what} · {p.Answered} of {p.Total} answers";
        }

        private string ShownFolder => string.IsNullOrEmpty(shownReport) ? null : Path.GetDirectoryName(shownReport);

        // ---- Actions ----------------------------------------------------------------------------

        private void UseProjectDefaults()
        {
            var sets = PromptSetStart.FindSets();
            if (sets.Count > 1)
            {
                // A division slash in place of each '/': GenericMenu would make each one a submenu.
                var menu = new GenericMenu();
                foreach (string path in sets)
                {
                    menu.AddItem(new GUIContent(path.Replace('/', '∕')), path == AssetDatabase.GetAssetPath(setAsset), () => UseProjectDefaults(path));
                }

                menu.ShowAsContext();
                return;
            }

            UseProjectDefaults(sets.Count == 1 ? sets[0] : null);
        }

        private void UseProjectDefaults(string setPath)
        {
            var missing = new List<string>();
            if (setPath != null)
            {
                setAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(setPath);
                LoadSet();
                profile = ProfileFor(setAsset, setup.Set) ?? profile;
            }
            else
            {
                missing.Add("no agent.json under Assets/ (write one in the Prompt Set window)");
            }

            if (profile == null)
            {
                missing.Add("no profile beside the set (drag it into Profile)");
            }

            var settings = AgenerelaSettingsPage.Find();
            if (settings != null && settings.EvaluationArmA != null) configA = settings.EvaluationArmA;
            if (settings != null && settings.EvaluationArmB != null) configB = settings.EvaluationArmB;
            if (settings == null || settings.EvaluationArmA == null || settings.EvaluationArmB == null)
            {
                missing.Add("no Arm A and Arm B in Project Settings → Agenerela");
            }

            ToSetup();
            setup.NameArmsFromConfigs();
            runMessage = missing.Count == 0 ? null : "The project has " + string.Join("; ", missing) + ".";
            runSeverity = ProblemSeverity.Warning;
            FromSetup();
        }

        // The profile the Prompt Set window last used for this set, else the one beside it.
        private static AgentProfile ProfileFor(TextAsset set, PromptSet loaded)
        {
            if (set == null)
            {
                return null;
            }

            var remembered = LoadByGuid<AgentProfile>(PromptSetPrefs.ForThisProject().ProfileGuidFor(GuidOf(set)));
            return remembered != null ? remembered : PromptSetStart.FindProfileBeside(AssetDatabase.GetAssetPath(set), loaded);
        }

        private async void Run()
        {
            LoadSet();
            ToSetup();
            EvalRunner runner;
            try
            {
                runner = setup.Build();
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                SetRunMessage(e.Message, ProblemSeverity.Error);
                return;
            }

            var cancel = new CancellationTokenSource();
            running = cancel;
            runStarted = EditorApplication.timeSinceStartup;
            runMessage = null;
            progress = null;
            runner.Progress = p => progress = p;
            EditorApplication.update += Tick;
            Refresh();

            // A recompile mid-run would drop the run with the old domain.
            EditorApplication.LockReloadAssemblies();
            try
            {
                var report = await runner.RunAsync(cancel.Token);
                string folder = report.Save(ReportsFolder);
                if (this != null)
                {
                    Show(report, Path.Combine(folder, EvalReport.JsonFile));
                    SetRunMessage($"Finished and saved to {folder}.", null);
                }
            }
            catch (EvalCancelledException e)
            {
                // The partial report is kept and shown, marked cancelled, so what was asked is not lost.
                string folder;
                try
                {
                    folder = e.Partial.Save(ReportsFolder);
                }
                catch (Exception saving) when (saving is IOException || saving is UnauthorizedAccessException)
                {
                    SetRunMessage("Cancelled, and the partial report could not be saved: " + saving.Message, ProblemSeverity.Warning);
                    folder = null;
                }

                if (folder != null && this != null)
                {
                    Show(e.Partial, Path.Combine(folder, EvalReport.JsonFile));
                    SetRunMessage($"Cancelled after {e.Partial.Answers.Count} of {e.Partial.Conditions.Prompts * 2} answers. "
                                + $"The partial report below is saved to {folder}, marked cancelled: it is no result.", ProblemSeverity.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                SetRunMessage("Cancelled before the first request. Nothing was saved.", ProblemSeverity.Warning);
            }
            catch (Exception e) when (e is InvalidOperationException || e is ProviderException)
            {
                // The runner's refusals say what to fix.
                SetRunMessage("The run stopped: " + e.Message, ProblemSeverity.Error);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                SetRunMessage("The run finished but its report could not be saved: " + e.Message, ProblemSeverity.Error);
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
                EditorApplication.update -= Tick;
                if (running == cancel)
                {
                    running = null;
                }

                cancel.Dispose();
                if (this != null)
                {
                    Refresh();
                }
            }
        }

        private void SetRunMessage(string message, ProblemSeverity? severity)
        {
            if (this == null)
            {
                return;
            }

            runMessage = message;
            runSeverity = severity;
            Refresh();
        }

        // Moves the progress bar and the time while a run goes on.
        private void Tick()
        {
            if (EditorApplication.timeSinceStartup - lastTick > 0.5)
            {
                lastTick = EditorApplication.timeSinceStartup;
                ui?.ShowProgress(progress, EditorApplication.timeSinceStartup - runStarted);
            }
        }

        private void OpenReport()
        {
            string start = Directory.Exists(ReportsFolder) ? ReportsFolder : Path.GetDirectoryName(Application.dataPath);
            string path = EditorUtility.OpenFilePanel("Open an evaluation report", start, "json");
            if (!string.IsNullOrEmpty(path))
            {
                TryShow(path, true);
            }
        }

        private void TryShow(string path, bool complain)
        {
            try
            {
                Show(EvalReport.FromJson(File.ReadAllText(path)), path);
                runMessage = null;
                Refresh();
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                if (complain)
                {
                    EditorUtility.DisplayDialog("Not an evaluation report", $"{path} could not be read as a report: {e.Message}", "OK");
                }
            }
        }

        private void Show(EvalReport report, string jsonPath)
        {
            view = new EvaluationReportView(report);
            shownReport = jsonPath ?? "";
            prefs.LastReport = shownReport;
            ReadAgreement(report);
            Refresh();
        }

        // The agreement on the report's set as its label files are now: labels are not part of a run.
        private void ReadAgreement(EvalReport report)
        {
            agreement = null;
            agreementNote = null;
            string path = report.Conditions.SetPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                agreementNote = $"The set this report ran on ({path}) is not on this machine, so its labellers' agreement cannot be read.";
                return;
            }

            try
            {
                var set = PromptSet.Load(path);
                agreement = AgreementReport.Compute(set, set.LoadLabels());
                agreementNote = "From the set's label files as they are now, which may have changed since the run.";
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                agreementNote = "The labellers' agreement could not be read: " + e.Message;
            }
        }

        private void CopySummary()
        {
            if (view == null)
            {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = view.FindingsSummary(
                ShownFolder == null ? null : Path.Combine(ShownFolder, EvalReport.MarkdownFile), agreement, agreementNote);
            ShowNotification(new GUIContent("Summary copied"));
        }

        private void SaveCopy()
        {
            if (view == null)
            {
                return;
            }

            string parent = EditorUtility.SaveFolderPanel("Save a copy of the report", Path.GetDirectoryName(Application.dataPath), "");
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            try
            {
                string folder = view.Report.Save(parent);
                EditorUtility.RevealInFinder(Path.Combine(folder, EvalReport.MarkdownFile));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                EditorUtility.DisplayDialog("The report was not saved", e.Message, "OK");
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
