using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
    /// cancelled. Each finished run is saved to <c>Logs/Evaluation/</c>, and <b>Open report…</b>
    /// reads any saved one back: what is shown comes only from what the report saves
    /// (<see cref="EvaluationReportView"/>). The choices are remembered on this machine, per
    /// project (<see cref="EvaluationReportPrefs"/>).
    /// </para>
    /// </remarks>
    public sealed class EvaluationReportWindow : EditorWindow
    {
        private const string ProjectSettingsPath = "Project/Agenerela";

        private const string SetTip = "The set file: the agent.json beside a folder of prompts. The Prompt Set window writes and labels it.";
        private const string ProfileTip = "The AgentProfile the set is measured against: every prompt is asked of an agent built from it.";
        private const string ConfigTip = "The provider config this arm's decisions go through. Project Settings → Agenerela names a default arm A and arm B.";
        private const string NameTip = "How the report names this arm, such as control or guards-on. The two must differ.";
        private const string CheckTargetTip = "The grounding guard: on, a target the request never named is stopped. Off is the usual control arm.";
        private const string OrderTip = "Interleaved asks each prompt in A, then in B, so drift hits both alike. One after the other runs all of A, unloads its model, then all of B: the only choice for arms on two models.";
        private const string DefaultsTip = "Opens the project's agent.json, the profile beside it, and the arm A and arm B configs from Project Settings → Agenerela.";

        private static readonly GUIContent[] OrderNames =
        {
            new GUIContent("Interleaved", OrderTip),
            new GUIContent("One after the other", OrderTip),
        };

        private static readonly Color[] OutcomeColors =
        {
            new Color(0.30f, 0.62f, 0.36f), // correct
            new Color(0.82f, 0.33f, 0.28f), // wrong but legal
            new Color(0.33f, 0.53f, 0.83f), // contained by a guard
            new Color(0.88f, 0.64f, 0.22f), // rejected when an action was expected
            new Color(0.52f, 0.52f, 0.52f), // pipeline error
        };

        [SerializeField] private TextAsset setAsset;
        [SerializeField] private AgentProfile profile;
        [SerializeField] private ProviderConfig configA, configB;
        [SerializeField] private string nameA = "", nameB = "";
        [SerializeField] private bool checkTargetA = true, checkTargetB = true;
        [SerializeField] private EvalArmOrder order = EvalArmOrder.Interleaved;
        [SerializeField] private string shownReport = "";
        [SerializeField] private int failureArm = 1;
        [SerializeField] private int failureFilter;
        [SerializeField] private bool showChecks = true, showConditions;

        private EvaluationReportPrefs prefs;
        private readonly EvaluationRunSetup setup = new EvaluationRunSetup();
        private EvaluationReportView view;
        private AgreementReport agreement;
        private string agreementNote;
        private readonly HashSet<string> opened = new HashSet<string>();
        private Vector2 scroll;

        // The run in progress, if any.
        private CancellationTokenSource running;
        private EvalProgress progress;
        private double runStarted;
        private double lastTick;
        private string runMessage;
        private MessageType runMessageType;

        [MenuItem("Window/Agenerela/Evaluation Report")]
        public static void Open()
        {
            var window = GetWindow<EvaluationReportWindow>();
            window.titleContent = new GUIContent("Evaluation Report");
            window.minSize = new Vector2(560, 420);
            window.Show();
        }

        /// <summary>Where each finished run is saved: <c>Logs/Evaluation/</c> in the project, which git ignores.</summary>
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

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= Tick;

            // A closed window cannot show its run, so the run stops.
            running?.Cancel();
        }

        // The set's prompts may have changed in the Prompt Set window meanwhile.
        private void OnFocus()
        {
            if (!Running && setAsset != null)
            {
                LoadSet();
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && Running)
            {
                running.Cancel();
            }
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

            Repaint();
        }

        // ---- Drawing ----------------------------------------------------------------------------

        private void OnGUI()
        {
            ToSetup();
            DrawToolbar();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (view == null && setup.WhyNotRunnable() != null && !Running)
            {
                DrawStart();
            }

            DrawSetup();

            if (view == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("No report yet. Press Run A/B above, or Open report… in the toolbar to read a saved one.", MessageType.None);
            }
            else
            {
                DrawReport();
            }

            EditorGUILayout.Space();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(Running))
                {
                    if (GUILayout.Button(new GUIContent("Use the project's set, profile and arms", DefaultsTip), EditorStyles.toolbarButton))
                    {
                        UseProjectDefaults();
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button(new GUIContent("Open report…", "Read a saved report.json back, such as one from Logs/Evaluation."), EditorStyles.toolbarButton))
                    {
                        OpenReport();
                        GUIUtility.ExitGUI();
                    }
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Edit prompt set", "Opens the Prompt Set window, which writes and labels prompts."), EditorStyles.toolbarButton))
                {
                    PromptSetWindow.Open();
                }

                if (GUILayout.Button(new GUIContent("Project Settings", "Project Settings → Agenerela names the default arm A and arm B."), EditorStyles.toolbarButton))
                {
                    SettingsService.OpenProjectSettings(ProjectSettingsPath);
                }
            }
        }

        /// <summary>The window before it can run: four numbered steps, and the button that does the first three.</summary>
        private void DrawStart()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Start here", EditorStyles.largeLabel);
            EditorGUILayout.LabelField(
                "The window measures how often an agent chooses correctly: the same labelled prompts asked in two set-ups, arm A and arm B, in one session. " +
                "It remembers your choices on this machine.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Use the project's set, profile and arms", DefaultsTip), GUILayout.Width(280), GUILayout.Height(24)))
                {
                    UseProjectDefaults();
                    GUIUtility.ExitGUI();
                }

                GUILayout.Label("Does steps 1 to 3 at once, where the project has them.", EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space();
            Step(1, setup.Set != null, "Choose the prompt set",
                setup.Set != null ? $"{AssetDatabase.GetAssetPath(setAsset)}: {setup.Set.Prompts.Count()} prompts." : "The agent.json the Prompt Set window writes. Drag it into Prompt set below.");
            Step(2, profile != null, "Choose the profile",
                profile != null ? profile.name + "." : "The AgentProfile the set was written for, usually beside agent.json.");

            var settings = AgenerelaSettingsPage.Find();
            bool armsInSettings = settings != null && settings.EvaluationArmA != null && settings.EvaluationArmB != null;
            Step(3, configA != null && configB != null, "Choose the two arms",
                configA != null && configB != null
                    ? $"A: {configA.name}, B: {configB.name}."
                    : armsInSettings
                        ? "Project Settings → Agenerela names them; the button above takes them from there."
                        : "Name an Arm A and an Arm B in Project Settings → Agenerela, or drag a provider config into each arm below. One config twice compares the grounding guard off and on.");
            if (configA == null || configB == null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(26);
                    if (GUILayout.Button("Open Project Settings → Agenerela", GUILayout.Width(240)))
                    {
                        SettingsService.OpenProjectSettings(ProjectSettingsPath);
                    }
                }
            }

            Step(4, false, "Run A/B",
                "Uses the GPU for a few minutes. Make sure nobody else is running a model: the run refuses to start while another one is loaded (hard rule 3).");
        }

        private static void Step(int number, bool done, string title, string detail)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(done ? "✓" : number + ".", EditorStyles.boldLabel, GUILayout.Width(22));
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(detail, EditorStyles.wordWrappedLabel);
                }
            }
        }

        private static void Heading(string number, string text)
        {
            EditorGUILayout.Space();
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            EditorGUILayout.LabelField($"{number}  {text}", EditorStyles.boldLabel);
        }

        // ① The set, the profile, the arms, the order, the checks, and Run.
        private void DrawSetup()
        {
            Heading("①", "Run set-up");
            using (new EditorGUI.DisabledScope(Running))
            {
                var chosenSet = (TextAsset)EditorGUILayout.ObjectField(new GUIContent("Prompt set", SetTip), setAsset, typeof(TextAsset), false);
                if (chosenSet != setAsset)
                {
                    setAsset = chosenSet;
                    LoadSet();
                    if (profile == null) profile = ProfileFor(setAsset, setup.Set);
                    Remember();
                    GUIUtility.ExitGUI();
                }

                if (setup.SetError != null)
                {
                    EditorGUILayout.HelpBox("The set did not load: " + setup.SetError, MessageType.Error);
                }
                else if (setup.Set != null)
                {
                    EditorGUILayout.LabelField(" ", $"{setup.Set.Agent}: {setup.Set.Prompts.Count()} prompts in {setup.Set.Categories.Count} categories.", EditorStyles.miniLabel);
                }

                var chosenProfile = (AgentProfile)EditorGUILayout.ObjectField(new GUIContent("Profile", ProfileTip), profile, typeof(AgentProfile), false);
                if (chosenProfile != profile)
                {
                    profile = chosenProfile;
                    Remember();
                }

                EditorGUILayout.Space(4);
                DrawArm(0, "Arm A");
                DrawArm(1, "Arm B");

                using (new EditorGUI.DisabledScope(setup.TwoModels))
                {
                    int chosen = EditorGUILayout.Popup(new GUIContent("Order", OrderTip), (int)setup.Order, OrderNames);
                    if (!setup.TwoModels && chosen != (int)order)
                    {
                        order = (EvalArmOrder)chosen;
                        Remember();
                    }
                }

                if (setup.TwoModels)
                {
                    EditorGUILayout.LabelField(" ", "The arms are on two models, so they run one after the other: both loaded at once would break hard rule 3.", EditorStyles.wordWrappedMiniLabel);
                }
            }

            ToSetup();
            if (setup.ArmsAreTheSame)
            {
                EditorGUILayout.HelpBox("Both arms are the same config with the same guard setting: a run of one set-up against itself. That measures the noise, not a change.", MessageType.Info);
            }

            var problems = setup.SetProblems();
            if (problems.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"The runner refuses this set until {(problems.Count == 1 ? "this is" : "these are")} fixed:\n• " + string.Join("\n• ", problems.Take(8))
                    + (problems.Count > 8 ? $"\n… and {problems.Count - 8} more." : ""),
                    MessageType.Warning);
            }

            showChecks = EditorGUILayout.Foldout(showChecks, "What the run checks", true);
            if (showChecks)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (string line in setup.Checks(3))
                    {
                        EditorGUILayout.LabelField("• " + line, EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }

            EditorGUILayout.Space(4);
            DrawRunButton();
        }

        private void DrawArm(int arm, string label)
        {
            var current = arm == 0 ? configA : configB;
            using (new EditorGUILayout.HorizontalScope())
            {
                var chosen = (ProviderConfig)EditorGUILayout.ObjectField(new GUIContent(label, ConfigTip), current, typeof(ProviderConfig), false);
                if (chosen != current)
                {
                    setup.ChangeConfig(arm, chosen);
                    FromSetup();
                }
            }

            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string name = arm == 0 ? nameA : nameB;
                    string typed = EditorGUILayout.TextField(new GUIContent("Name", NameTip), name);
                    bool check = arm == 0 ? checkTargetA : checkTargetB;
                    bool toggled = EditorGUILayout.ToggleLeft(new GUIContent("Grounding guard", CheckTargetTip), check, GUILayout.Width(130));
                    if (typed != name || toggled != check)
                    {
                        if (arm == 0) { nameA = typed; checkTargetA = toggled; }
                        else { nameB = typed; checkTargetB = toggled; }
                        Remember();
                    }
                }

                if (current != null)
                {
                    string model = EvaluationRunSetup.ModelOf(current);
                    EditorGUILayout.LabelField(" ", model != null ? $"{model} on Ollama." : $"A {current.GetType().Name}: which model it runs is not checked.", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawRunButton()
        {
            string why = Running ? null
                : EditorApplication.isPlayingOrWillChangePlaymode ? "Leave Play mode first: entering or leaving it would stop the run."
                : setup.WhyNotRunnable();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (Running)
                {
                    if (GUILayout.Button(new GUIContent("Cancel", "Stops the run between or during decisions."), GUILayout.Width(120), GUILayout.Height(26)))
                    {
                        running.Cancel();
                    }

                    double seconds = EditorApplication.timeSinceStartup - runStarted;
                    using (new EditorGUILayout.VerticalScope())
                    {
                        var bar = EditorGUILayout.GetControlRect(false, 18);
                        float done = progress == null || progress.Total == 0 ? 0 : (float)progress.Answered / progress.Total;
                        EditorGUI.ProgressBar(bar, done, ProgressText(progress));
                        GUILayout.Label($"{(int)(seconds / 60)} min {(int)(seconds % 60)} s so far. The Editor stays usable; scripts do not recompile until the run ends.",
                            EditorStyles.wordWrappedMiniLabel);
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(why != null))
                    {
                        if (GUILayout.Button(new GUIContent("▶ Run A/B", "Asks every prompt in both arms and saves the report to Logs/Evaluation."), GUILayout.Width(120), GUILayout.Height(26)))
                        {
                            Run();
                            GUIUtility.ExitGUI();
                        }
                    }

                    GUILayout.Label(why ?? "Ready. Make sure nobody else is using the GPU.", EditorStyles.wordWrappedMiniLabel);
                }
            }

            if (runMessage != null)
            {
                EditorGUILayout.HelpBox(runMessage, runMessageType);
            }
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

        private void DrawReport()
        {
            var report = view.Report;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(report.Name ?? "Report", EditorStyles.largeLabel);
            EditorGUILayout.LabelField(view.RunLine, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(shownReport))
            {
                EditorGUILayout.LabelField(shownReport, EditorStyles.miniLabel);
            }

            if (view.CancelledLine != null)
            {
                EditorGUILayout.HelpBox(view.CancelledLine, MessageType.Warning);
            }

            foreach (string warning in view.Warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            showConditions = EditorGUILayout.Foldout(showConditions, "Conditions this run was measured under", true);
            if (showConditions)
            {
                foreach (string line in view.ConditionLines())
                {
                    EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
                }
            }

            DrawOutcomes();
            DrawNoise();
            DrawCategories();
            DrawFailures();
            DrawLabellingAndExport();
        }

        // ② The five outcomes per arm: a bar each, a legend, and the table.
        private void DrawOutcomes()
        {
            Heading("②", $"Outcomes · {view.Report.Conditions.Prompts} prompts per arm");
            foreach (var arm in new[] { view.ArmA, view.ArmB })
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(150)))
                    {
                        EditorGUILayout.LabelField(arm.Name, EditorStyles.boldLabel, GUILayout.Width(150));
                        EditorGUILayout.LabelField(arm.Subtitle, EditorStyles.miniLabel, GUILayout.Width(150));
                    }

                    var rect = GUILayoutUtility.GetRect(100, 22, GUILayout.ExpandWidth(true));
                    rect.y += 6;
                    DrawBar(rect, arm.Counts);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(154);
                for (int i = 0; i < EvaluationReportView.Outcomes.Length; i++)
                {
                    var swatch = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10));
                    swatch.y += 4;
                    EditorGUI.DrawRect(swatch, OutcomeColors[i]);
                    GUILayout.Label(EvaluationReportView.NameOf(EvaluationReportView.Outcomes[i]), EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(4);
            Row("Outcome", view.ArmA.Name, view.ArmB.Name, "Difference", EditorStyles.miniBoldLabel);
            foreach (var row in view.OutcomeRows())
            {
                string name = row.Meaning.Length > 0 ? $"{row.Name} — {row.Meaning}" : row.Name;
                string difference = EvaluationReportView.Signed(row.DifferencePoints) + " pts" + (row.Noise != null ? " · " + row.Noise : "");
                Row(name, $"{EvaluationReportView.Percent(row.ShareA)} ({row.CountA})", $"{EvaluationReportView.Percent(row.ShareB)} ({row.CountB})", difference,
                    row.Outcome == DecisionOutcome.Correct ? EditorStyles.boldLabel : EditorStyles.label);
            }
        }

        private static void Row(string name, string a, string b, string difference, GUIStyle style)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(name, style, GUILayout.MinWidth(160), GUILayout.ExpandWidth(true));
                GUILayout.Label(a, style, GUILayout.Width(80));
                GUILayout.Label(b, style, GUILayout.Width(80));
                GUILayout.Label(difference, style, GUILayout.Width(150));
            }
        }

        private static void DrawBar(Rect rect, OutcomeCounts counts)
        {
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.15f));
            if (counts.Total == 0)
            {
                return;
            }

            float x = rect.x;
            for (int i = 0; i < EvaluationReportView.Outcomes.Length; i++)
            {
                double share = EvaluationReportView.Share(counts, EvaluationReportView.Outcomes[i]);
                float width = (float)(rect.width * share);
                if (width <= 0)
                {
                    continue;
                }

                var part = new Rect(x, rect.y, width, rect.height);
                EditorGUI.DrawRect(part, OutcomeColors[i]);
                if (width > 34)
                {
                    GUI.Label(part, EvaluationReportView.Percent(share), BarText);
                }

                x += width;
            }
        }

        private static GUIStyle barText;

        private static GUIStyle BarText => barText ?? (barText = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
        });

        // ③ The noise check, in one sentence and its formula.
        private void DrawNoise()
        {
            Heading("③", "Noise check");
            var all = view.Overall;
            EditorGUILayout.HelpBox(view.NoiseLine, all != null && !all.WithinNoise ? MessageType.None : MessageType.Info);
            EditorGUILayout.LabelField($"Noise floor: {NoiseCheck.Formula}. Checked on accuracy, over the whole set and in each category with its own n.",
                EditorStyles.wordWrappedMiniLabel);
        }

        // ④ Accuracy per category, with each one's own noise check.
        private void DrawCategories()
        {
            Heading("④", "Correct answers by prompt category");
            var rows = view.CategoryRows();
            if (rows.Count == 0)
            {
                EditorGUILayout.LabelField("The report has no categories.", EditorStyles.miniLabel);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Category", EditorStyles.miniBoldLabel, GUILayout.MinWidth(140), GUILayout.ExpandWidth(true));
                GUILayout.Label("n", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                GUILayout.Label(view.ArmA.Name, EditorStyles.miniBoldLabel, GUILayout.Width(120));
                GUILayout.Label(view.ArmB.Name, EditorStyles.miniBoldLabel, GUILayout.Width(120));
                GUILayout.Label("Difference", EditorStyles.miniBoldLabel, GUILayout.Width(150));
            }

            foreach (var row in rows)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(row.Id, GUILayout.MinWidth(140), GUILayout.ExpandWidth(true));
                    GUILayout.Label(row.N.ToString(), GUILayout.Width(36));
                    AccuracyCell(row.AccuracyA);
                    AccuracyCell(row.AccuracyB);
                    GUILayout.Label(EvaluationReportView.Signed(row.DifferencePoints) + " pts · " + (row.WithinNoise ? "within noise" : "beyond noise"),
                        row.WithinNoise ? EditorStyles.miniLabel : EditorStyles.miniBoldLabel, GUILayout.Width(150));
                }
            }

            EditorGUILayout.LabelField(
                "Categories exist so the headline number is never measured on player chat alone: agents are also asked by timers and game events. " +
                "A small category needs a large difference before it says anything.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private static void AccuracyCell(double points)
        {
            var rect = GUILayoutUtility.GetRect(120, 18, GUILayout.Width(120));
            var bar = new Rect(rect.x, rect.y + 5, 70, 8);
            EditorGUI.DrawRect(bar, new Color(0.5f, 0.5f, 0.5f, 0.2f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, (float)(bar.width * Math.Max(0, Math.Min(100, points)) / 100), bar.height), OutcomeColors[0]);
            GUI.Label(new Rect(rect.x + 74, rect.y, 46, rect.height), EvaluationReportView.Percent(points / 100), EditorStyles.miniLabel);
        }

        // ⑤ The answers that were not correct, per arm, each opening its whole record.
        private void DrawFailures()
        {
            string[] arms = { view.ArmA.Name, view.ArmB.Name };
            string arm = arms[Mathf.Clamp(failureArm, 0, 1)];
            var outcomes = new[] { (DecisionOutcome?)null }.Concat(EvaluationReportView.Outcomes.Skip(1).Select(o => (DecisionOutcome?)o)).ToArray();
            var filter = outcomes[Mathf.Clamp(failureFilter, 0, outcomes.Length - 1)];
            var failures = view.Failures(arm, filter);

            Heading("⑤", $"Not correct in arm {(failureArm == 0 ? "A" : "B")} · {failures.Count} prompt{(failures.Count == 1 ? "" : "s")}");
            using (new EditorGUILayout.HorizontalScope())
            {
                failureArm = GUILayout.Toolbar(Mathf.Clamp(failureArm, 0, 1), new[] { "A · " + arms[0], "B · " + arms[1] }, GUILayout.Width(260));
                var names = outcomes.Select(o => o == null ? "Every outcome but correct" : EvaluationReportView.NameOf(o.Value)).ToArray();
                failureFilter = EditorGUILayout.Popup(Mathf.Clamp(failureFilter, 0, outcomes.Length - 1), names, GUILayout.Width(240));
                GUILayout.FlexibleSpace();
            }

            if (failures.Count == 0)
            {
                EditorGUILayout.LabelField("None.", EditorStyles.miniLabel);
                return;
            }

            foreach (var answer in failures)
            {
                string key = answer.Arm + "\n" + answer.PromptId;
                bool open = opened.Contains(key);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(answer.PromptId, EditorStyles.miniBoldLabel, GUILayout.Width(110));
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(EvaluationReportView.Asked(answer), EditorStyles.wordWrappedLabel);
                        EditorGUILayout.LabelField(EvaluationReportView.ExpectedAndGot(answer) + " · " + EvaluationReportView.NameOf(answer.Outcome).ToLowerInvariant(),
                            EditorStyles.wordWrappedMiniLabel);
                    }

                    if (GUILayout.Button(open ? "Close ▾" : "Open ▸", GUILayout.Width(70)))
                    {
                        if (open) opened.Remove(key); else opened.Add(key);
                    }
                }

                if (open)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        foreach (var detail in EvaluationReportView.Details(answer))
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label(detail.Key, EditorStyles.miniBoldLabel, GUILayout.Width(150));
                                EditorGUILayout.SelectableLabel(detail.Value, EditorStyles.wordWrappedMiniLabel,
                                    GUILayout.Height(EditorStyles.wordWrappedMiniLabel.CalcHeight(new GUIContent(detail.Value), Mathf.Max(100, position.width - 210))));
                            }
                        }
                    }
                }
            }
        }

        // ⑥ The labellers' agreement on the set now, and the report's export.
        private void DrawLabellingAndExport()
        {
            Heading("⑥", "Labelling and export");
            if (agreement != null)
            {
                EditorGUILayout.LabelField(EvaluationReportView.AgreementHeadline(agreement), new GUIStyle(EditorStyles.boldLabel) { wordWrap = true });
                EditorGUILayout.LabelField(EvaluationReportView.AgreementDetail(agreement), EditorStyles.wordWrappedMiniLabel);
            }

            if (agreementNote != null)
            {
                EditorGUILayout.LabelField(agreementNote, EditorStyles.wordWrappedMiniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Review disagreements", "Opens the Prompt Set window: its Agreement tab lists and settles the splits."), GUILayout.Width(170)))
                {
                    PromptSetWindow.Open();
                }

                if (GUILayout.Button(new GUIContent("Copy summary for findings.md", "Copies a short Markdown summary: what ran, its conditions, the five outcomes and the noise check."), GUILayout.Width(210)))
                {
                    EditorGUIUtility.systemCopyBuffer = view.FindingsSummary(ShownFolder == null ? null : Path.Combine(ShownFolder, EvalReport.MarkdownFile));
                    ShowNotification(new GUIContent("Summary copied"));
                }

                using (new EditorGUI.DisabledScope(ShownFolder == null || !Directory.Exists(ShownFolder)))
                {
                    if (GUILayout.Button(new GUIContent("Show files", "Shows report.json and report.md in the file browser."), GUILayout.Width(90)))
                    {
                        EditorUtility.RevealInFinder(Path.Combine(ShownFolder, EvalReport.MarkdownFile));
                    }
                }

                if (GUILayout.Button(new GUIContent("Save a copy…", "Writes report.json and report.md into a folder of your choice."), GUILayout.Width(100)))
                {
                    SaveCopy();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.LabelField(
                "Every accuracy claim has to come from a run like this one: a control arm, the same model, the same machine, the same session (hard rule 2). " +
                "Read docs/llm-wiki/measuring.md before writing a number down.",
                EditorStyles.wordWrappedMiniLabel);
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
            FromSetup();

            runMessage = missing.Count == 0 ? null : "The project has " + string.Join("; ", missing) + ".";
            runMessageType = MessageType.Info;
            Repaint();
        }

        // The profile the Prompt Set window last used for this set, else the one beside it.
        private static AgentProfile ProfileFor(TextAsset set, PromptSet loaded)
        {
            if (set == null)
            {
                return null;
            }

            string guid = GuidOf(set);
            var remembered = LoadByGuid<AgentProfile>(PromptSetPrefs.ForThisProject().ProfileGuidFor(guid));
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
                runMessage = e.Message;
                runMessageType = MessageType.Error;
                return;
            }

            var cancel = new CancellationTokenSource();
            running = cancel;
            runStarted = EditorApplication.timeSinceStartup;
            runMessage = null;
            progress = null;
            runner.Progress = p => progress = p;
            EditorApplication.update += Tick;

            // A recompile mid-run would drop the run with the old domain.
            EditorApplication.LockReloadAssemblies();
            try
            {
                var report = await runner.RunAsync(cancel.Token);
                string folder = report.Save(ReportsFolder);
                string json = Path.Combine(folder, EvalReport.JsonFile);
                if (this != null)
                {
                    Show(report, json);
                    runMessage = $"Finished and saved to {folder}.";
                    runMessageType = MessageType.Info;
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
                    SetRunMessage("Cancelled, and the partial report could not be saved: " + saving.Message, MessageType.Warning);
                    folder = null;
                }

                if (folder != null && this != null)
                {
                    Show(e.Partial, Path.Combine(folder, EvalReport.JsonFile));
                    SetRunMessage($"Cancelled after {e.Partial.Answers.Count} of {e.Partial.Conditions.Prompts * 2} answers. "
                                + $"The partial report below is saved to {folder}, marked cancelled: it is no result.", MessageType.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                SetRunMessage("Cancelled before the first request. Nothing was saved.", MessageType.Warning);
            }
            catch (Exception e) when (e is InvalidOperationException || e is ProviderException)
            {
                // The runner's refusals say what to fix.
                SetRunMessage("The run stopped: " + e.Message, MessageType.Error);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                SetRunMessage("The run finished but its report could not be saved: " + e.Message, MessageType.Error);
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
                    Repaint();
                }
            }
        }

        private void SetRunMessage(string message, MessageType type)
        {
            if (this == null)
            {
                return;
            }

            runMessage = message;
            runMessageType = type;
        }

        // Redraws the elapsed time while a run goes on.
        private void Tick()
        {
            if (EditorApplication.timeSinceStartup - lastTick > 0.5)
            {
                lastTick = EditorApplication.timeSinceStartup;
                Repaint();
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
            opened.Clear();
            failureArm = 1;
            failureFilter = 0;
            ReadAgreement(report);
            Repaint();
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

        private void SaveCopy()
        {
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
