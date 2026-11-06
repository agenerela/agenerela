using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Editor.UI;
using Agenerela.Evaluation;
using Agenerela.Providers;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// What the Evaluation Report window shows (#179, screen 4), as a UI Toolkit tree on the #167
    /// kit: the start-here card, ① the run set-up, the run's progress, and ② to ⑥ of a report.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It holds no run and decides nothing: <see cref="EvaluationReportWindow"/> owns the set-up,
    /// the run and the report, and calls <see cref="Show"/> with all of it. What a person does here
    /// comes back as the events below. Only what is purely a matter of looking is kept here: whether
    /// ① is open, which arm and outcome the failure list shows, and which rows are open.
    /// </para>
    /// <para>
    /// It needs no window, so a test builds it and reads its tree directly; every part a test or
    /// the window reaches is named (the constants below).
    /// </para>
    /// </remarks>
    internal sealed class EvaluationReportUi
    {
        internal const string LayoutPath = "Packages/com.agenerela.framework/Editor/Evaluation/EvaluationReportWindow.uxml";
        internal const string SheetPath = "Packages/com.agenerela.framework/Editor/Evaluation/EvaluationReportWindow.uss";

        // Names a test or the window reaches. The rest are in the UXML.
        internal const string StartName = "start", StepsName = "steps", SetupFieldsName = "setup-fields", SetupToggleName = "setup-toggle",
            SetupSummaryName = "setup-summary", RunName = "run", RunReasonName = "run-reason", FoldedReasonName = "setup-folded-reason",
            ProgressRowName = "progress-row", ProgressName = "progress", RunMessageName = "run-message", NoReportName = "no-report",
            ReportName = "report", CancelledName = "cancelled", WarningsName = "warnings", OutcomesName = "outcomes", NoiseName = "noise",
            CategoriesName = "categories", FailuresName = "failures", FailureListName = "failure-list", FailureFilterName = "failure-filter",
            FailureArmsName = "failure-arms", AgreementHeadlineName = "agreement-headline",
            SetProblemsName = "set-problems", UseDefaultsName = "use-defaults", StartDefaultsName = "start-defaults";

        /// <summary>Everything the window shows, handed over whole on each change.</summary>
        internal sealed class State
        {
            internal TextAsset SetAsset;
            internal EvaluationRunSetup Setup = new EvaluationRunSetup();

            /// <summary>Why <b>Run A/B</b> is greyed out, in words; null when it can run.</summary>
            internal string WhyNotRunnable;

            /// <summary>True when Project Settings names both arms, so <b>Use the project's…</b> can take them.</summary>
            internal bool ArmsInSettings;

            internal bool Running;
            internal EvalProgress Progress;
            internal double Seconds;

            /// <summary>What the last run or action said; null for nothing.</summary>
            internal string Message;

            /// <summary>The message's severity; null for a plain note, such as "Finished and saved to …".</summary>
            internal ProblemSeverity? MessageSeverity;

            internal EvaluationReportView View;

            /// <summary>The report.json shown, or "".</summary>
            internal string ShownPath = "";

            internal AgreementReport Agreement;
            internal string AgreementNote;
            internal int WarmUpRequests = 3;
        }

        // The outcomes the failure filter offers: every one but correct, then each.
        private static readonly DecisionOutcome?[] Filters =
            new DecisionOutcome?[] { null }.Concat(EvaluationReportView.Outcomes.Skip(1).Select(o => (DecisionOutcome?)o)).ToArray();

        private static readonly string[] OutcomeClasses = { "er-outcome--correct", "er-outcome--wrong", "er-outcome--contained", "er-outcome--rejected", "er-outcome--error" };

        private readonly VisualElement root;
        private readonly ObjectField setField, profileField, configA, configB;
        private readonly TextField nameA, nameB;
        private readonly Toggle guardA, guardB;
        private readonly DropdownField order, failureFilter;
        private readonly Button run;
        private readonly HashSet<string> opened = new HashSet<string>();
        private State state = new State();
        private EvaluationReportView lastView;

        internal event Action UseDefaults, OpenReport, EditSet, OpenProjectSettings, Run, Cancel, CopySummary, ShowFiles, SaveCopy, ReviewDisagreements;
        internal event Action<TextAsset> SetChanged;
        internal event Action<AgentProfile> ProfileChanged;
        internal event Action<int, ProviderConfig> ConfigChanged;
        internal event Action<int, string> NameChanged;
        internal event Action<int, bool> GuardChanged;
        internal event Action<EvalArmOrder> OrderChanged;

        /// <param name="root">The tree built from <see cref="LayoutPath"/>; see <see cref="Load"/>.</param>
        internal EvaluationReportUi(VisualElement root)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));

            Wire(UseDefaultsName, () => UseDefaults?.Invoke());
            Wire(StartDefaultsName, () => UseDefaults?.Invoke());
            AgenerelaButtons.MakePrimary(root.Q<Button>(StartDefaultsName));
            Wire("open-report", () => OpenReport?.Invoke());
            Wire("edit-set", () => EditSet?.Invoke());
            Wire("project-settings", () => OpenProjectSettings?.Invoke());
            Wire("cancel", () => Cancel?.Invoke());
            Wire("copy-summary", () => CopySummary?.Invoke());
            Wire("show-files", () => ShowFiles?.Invoke());
            Wire("save-copy", () => SaveCopy?.Invoke());
            Wire("review-disagreements", () => ReviewDisagreements?.Invoke());

            run = root.Q<Button>(RunName);
            run.clicked += () => Run?.Invoke();
            AgenerelaButtons.MakePrimary(run);

            setField = Field<TextAsset>("set-field", v => SetChanged?.Invoke(v as TextAsset));
            profileField = Field<AgentProfile>("profile-field", v => ProfileChanged?.Invoke(v as AgentProfile));
            configA = Field<ProviderConfig>("arm-a-config", v => ConfigChanged?.Invoke(0, v as ProviderConfig));
            configB = Field<ProviderConfig>("arm-b-config", v => ConfigChanged?.Invoke(1, v as ProviderConfig));
            configA.tooltip = configB.tooltip = "The provider config this arm's decisions go through. Project Settings → Agenerela names a default arm A and arm B.";

            nameA = Text("arm-a-name", 0);
            nameB = Text("arm-b-name", 1);
            guardA = Guard("arm-a-guard", 0);
            guardB = Guard("arm-b-guard", 1);

            order = root.Q<DropdownField>("order");
            order.choices = new List<string> { "Interleaved", "One after the other" };
            order.tooltip = "Interleaved asks each prompt in A, then in B, so drift hits both alike. One after the other runs all of A, unloads its model, then all of B: the only choice for arms on two models.";
            order.RegisterValueChangedCallback(e => OrderChanged?.Invoke(order.index == 1 ? EvalArmOrder.Sequential : EvalArmOrder.Interleaved));

            failureFilter = root.Q<DropdownField>(FailureFilterName);
            failureFilter.choices = Filters.Select(f => f == null ? "Every outcome but correct" : EvaluationReportView.NameOf(f.Value)).ToList();
            failureFilter.index = 0;
            failureFilter.RegisterValueChangedCallback(_ => ShowFailures());

            var toggle = root.Q<Label>(SetupToggleName);
            toggle.AddManipulator(new Clickable(() =>
            {
                SetupOpen = !SetupOpen;
                ShowSetup();
            }));
            root.Q<VisualElement>("setup-summary").AddManipulator(new Clickable(() =>
            {
                SetupOpen = !SetupOpen;
                ShowSetup();
            }));

            Show(state);
        }

        /// <summary>A new tree from the window's UXML, with the kit's stylesheet and the window's own.</summary>
        internal static VisualElement Load()
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath)
                         ?? throw new InvalidOperationException($"The Evaluation Report window's layout is missing: {LayoutPath}.");
            var tree = layout.Instantiate();
            tree.style.flexGrow = 1;
            AgenerelaStyles.Apply(tree);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath);
            if (sheet != null)
            {
                tree.styleSheets.Add(sheet);
            }

            return tree;
        }

        internal VisualElement Root => root;

        /// <summary>Whether ① shows its fields. Open until a report is shown, then folded to one line.</summary>
        internal bool SetupOpen { get; set; } = true;

        /// <summary>Which arm the failure list shows: 0 for A, 1 for B. B when a report is shown.</summary>
        internal int FailureArm { get; set; } = 1;

        /// <summary>The outcome the failure list shows; null for every one but correct.</summary>
        internal DecisionOutcome? FailureOutcome
        {
            get => Filters[Mathf.Clamp(failureFilter.index, 0, Filters.Length - 1)];
            set => failureFilter.index = Array.IndexOf(Filters, value) < 0 ? 0 : Array.IndexOf(Filters, value);
        }

        /// <summary>Shows <paramref name="next"/>: everything is drawn again from it.</summary>
        internal void Show(State next)
        {
            state = next ?? new State();
            if (!ReferenceEquals(state.View, lastView))
            {
                // A new report: ① folds out of the way, and its failures start at arm B, all outcomes.
                lastView = state.View;
                SetupOpen = state.View == null;
                FailureArm = 1;
                failureFilter.SetValueWithoutNotify(failureFilter.choices[0]);
                opened.Clear();
            }

            ShowStart();
            ShowSetup();
            ShowRun();
            ShowReport();
            Literal(root);
        }

        // Report and set text is shown as written: a statement or a stimulus with "<b>" in it is
        // not formatting. Unity parses rich text by default, so every label is told not to, but a
        // chip, which marks one not offered with a strike tag of its own and never parses its id.
        private static void Literal(VisualElement under)
        {
            under.Query<Label>().ForEach(label =>
            {
                if (!(label is Chip))
                {
                    label.enableRichText = false;
                }
            });
        }

        /// <summary>Only the progress and the time: called every half second while a run goes on.</summary>
        internal void ShowProgress(EvalProgress progress, double seconds)
        {
            state.Progress = progress;
            state.Seconds = seconds;
            var bar = root.Q<ProgressBar>(ProgressName);
            bar.value = progress == null || progress.Total == 0 ? 0 : (float)progress.Answered / progress.Total;
            bar.title = EvaluationReportWindow.ProgressText(progress);
            root.Q<Label>("elapsed").text =
                $"{(int)(seconds / 60)} min {(int)(seconds % 60)} s so far. The Editor stays usable; scripts do not recompile until the run ends.";
        }

        // ---- A · First open ------------------------------------------------------------------------

        private void ShowStart()
        {
            var setup = state.Setup;
            bool start = state.View == null && state.WhyNotRunnable != null && !state.Running;
            Display(root.Q(StartName), start);
            // The card has the one primary button; the toolbar's copy would be a second.
            Display(root.Q(UseDefaultsName), !start);
            if (!start)
            {
                return;
            }

            var steps = root.Q(StepsName);
            steps.Clear();
            steps.Add(Step(1, setup.Set != null, "Choose the prompt set",
                setup.Set != null
                    ? $"{AssetDatabase.GetAssetPath(state.SetAsset)} · {setup.Set.Prompts.Count()} prompts."
                    : "The agent.json the Prompt Set window writes. Drag it into Prompt set below."));
            steps.Add(Step(2, setup.Profile != null, "Choose the profile",
                setup.Profile != null ? setup.Profile.name + "." : "The AgentProfile the set was written for, usually beside agent.json."));

            bool arms = setup.A.Config != null && setup.B.Config != null;
            var three = Step(3, arms, "Choose the two arms",
                arms ? $"A: {setup.A.Config.name}, B: {setup.B.Config.name}."
                : state.ArmsInSettings ? "Project Settings → Agenerela names them; the button above takes them from there."
                : null);
            if (!arms && !state.ArmsInSettings)
            {
                var problem = new ProblemBox("The project has no Arm A and Arm B in Project Settings → Agenerela. Name them there, or drag a provider config into each arm below; one config twice compares the grounding guard off and on.");
                problem.name = "arms-problem";
                problem.SetFix("Open Project Settings", () => OpenProjectSettings?.Invoke());
                three.Q("step-text").Add(problem);
            }

            steps.Add(three);
            steps.Add(Step(4, false, "Run A/B", "Uses the GPU for a few minutes. It refuses to start while another model is loaded (hard rule 3)."));
        }

        private static VisualElement Step(int number, bool done, string title, string detail)
        {
            var step = new VisualElement { name = "step-" + number };
            step.AddToClassList("er-step");
            var mark = new Label(done ? "✓" : number.ToString()) { name = "step-mark" };
            mark.AddToClassList("er-step__mark");
            mark.EnableInClassList("er-step__mark--done", done);
            step.Add(mark);
            var text = new VisualElement { name = "step-text" };
            text.AddToClassList("er-grow");
            var heading = new Label(title);
            heading.AddToClassList("er-step__title");
            text.Add(heading);
            if (detail != null)
            {
                var line = new Label(detail);
                line.AddToClassList("er-wrap");
                text.Add(line);
            }

            step.Add(text);
            return step;
        }

        // ---- ① Run set-up -------------------------------------------------------------------------

        private void ShowSetup()
        {
            var setup = state.Setup;
            bool canEdit = !state.Running;

            root.Q<Label>(SetupToggleName).text = (SetupOpen ? "▾" : "▸") + "  ① Run set-up";
            root.Q<Label>(SetupSummaryName).text = SetupOpen ? "" : Summary(setup);
            Display(root.Q(SetupFieldsName), SetupOpen);

            setField.SetValueWithoutNotify(state.SetAsset);
            profileField.SetValueWithoutNotify(setup.Profile);
            configA.SetValueWithoutNotify(setup.A.Config);
            configB.SetValueWithoutNotify(setup.B.Config);
            nameA.SetValueWithoutNotify(setup.A.Name ?? "");
            nameB.SetValueWithoutNotify(setup.B.Name ?? "");
            guardA.SetValueWithoutNotify(setup.A.CheckTarget);
            guardB.SetValueWithoutNotify(setup.B.CheckTarget);
            foreach (var field in new VisualElement[] { setField, profileField, configA, configB, nameA, nameB, guardA, guardB })
            {
                field.SetEnabled(canEdit);
            }

            var info = root.Q<Label>("set-info");
            info.text = setup.SetError != null ? "The set did not load: " + setup.SetError
                : setup.Set != null ? $"{setup.Set.Agent}: {setup.Set.Prompts.Count()} prompts in {setup.Set.Categories.Count} categories."
                : "";
            Display(info, info.text.Length > 0);

            ModelNote(root.Q<Label>("arm-a-model"), setup.A.Config);
            ModelNote(root.Q<Label>("arm-b-model"), setup.B.Config);

            order.SetValueWithoutNotify(order.choices[setup.Order == EvalArmOrder.Sequential ? 1 : 0]);
            order.SetEnabled(canEdit && !setup.TwoModels);
            var orderNote = root.Q<Label>("order-note");
            orderNote.text = setup.TwoModels ? "The arms are on two models, so they run one after the other: both loaded at once would break hard rule 3." : "";
            Display(orderNote, setup.TwoModels);

            var same = root.Q<Label>("same-arms");
            same.text = "Both arms are the same config with the same guard setting: a run of one set-up against itself. That measures the noise, not a change.";
            Display(same, setup.ArmsAreTheSame);

            var problems = root.Q(SetProblemsName);
            problems.Clear();
            var found = setup.SetProblems();
            if (found.Count > 0)
            {
                problems.Add(new ProblemBox(
                    $"The runner refuses this set until {(found.Count == 1 ? "this is" : "these are")} fixed:\n• " + string.Join("\n• ", found.Take(8))
                    + (found.Count > 8 ? $"\n… and {found.Count - 8} more." : ""), ProblemSeverity.Warning) { name = "set-problem" });
            }

            var checks = root.Q<Foldout>("checks");
            checks.Clear();
            foreach (string line in setup.Checks(state.WarmUpRequests))
            {
                var label = new Label("• " + line);
                label.AddToClassList("er-check");
                checks.Add(label);
            }

            // Run sits on ①'s folded line, or at the end of its fields.
            run.SetEnabled(state.WhyNotRunnable == null && !state.Running);
            Display(run, !state.Running);
            var slot = SetupOpen ? root.Q("run-row") : root.Q("setup-header-run");
            if (run.parent != slot)
            {
                slot.Insert(0, run);
            }

            var reason = root.Q<Label>(RunReasonName);
            reason.text = state.WhyNotRunnable ?? "Ready. Make sure nobody else is using the GPU.";
            var folded = root.Q<Label>(FoldedReasonName);
            folded.text = state.WhyNotRunnable ?? "";
            Display(folded, !SetupOpen && state.WhyNotRunnable != null && !state.Running);
        }

        private static string Summary(EvaluationRunSetup setup)
        {
            var parts = new List<string>
            {
                setup.Set?.FilePath != null ? System.IO.Path.GetFileName(setup.Set.FilePath) : "no set",
                setup.Profile != null ? setup.Profile.name : "no profile",
                $"{(string.IsNullOrWhiteSpace(setup.A.Name) ? "A" : setup.A.Name)} vs {(string.IsNullOrWhiteSpace(setup.B.Name) ? "B" : setup.B.Name)}",
                setup.Order == EvalArmOrder.Interleaved ? "interleaved" : "one after the other",
            };
            return string.Join(" · ", parts);
        }

        private static void ModelNote(Label note, ProviderConfig config)
        {
            string model = EvaluationRunSetup.ModelOf(config);
            note.text = config == null ? "" : model != null ? $"{model} on Ollama." : $"A {config.GetType().Name}: which model it runs is not checked.";
            Display(note, config != null);
        }

        // ---- B · A run ----------------------------------------------------------------------------

        private void ShowRun()
        {
            Display(root.Q(ProgressRowName), state.Running);
            if (state.Running)
            {
                ShowProgress(state.Progress, state.Seconds);
            }

            var message = root.Q(RunMessageName);
            message.Clear();
            if (state.Message == null)
            {
                return;
            }

            if (state.MessageSeverity.HasValue)
            {
                message.Add(new ProblemBox(state.Message, state.MessageSeverity.Value) { name = "run-problem" });
            }
            else
            {
                var note = new Label(state.Message) { name = "run-note" };
                note.AddToClassList("ag-muted");
                note.AddToClassList("er-wrap");
                note.AddToClassList("er-note");
                message.Add(note);
            }
        }

        // ---- C, D · The report --------------------------------------------------------------------

        private void ShowReport()
        {
            var view = state.View;
            Display(root.Q(NoReportName), view == null && !state.Running);
            Display(root.Q(ReportName), view != null);
            if (view == null)
            {
                return;
            }

            root.Q<Label>("report-name").text = view.Report.Name ?? "Report";
            root.Q<Label>("report-run-line").text = view.RunLine;
            var path = root.Q<Label>("report-path");
            path.text = state.ShownPath ?? "";
            Display(path, path.text.Length > 0);

            var cancelled = root.Q(CancelledName);
            cancelled.Clear();
            if (view.CancelledLine != null)
            {
                cancelled.Add(new ProblemBox(view.CancelledLine) { name = "cancelled-problem" });
            }

            var warnings = root.Q(WarningsName);
            warnings.Clear();
            foreach (string warning in view.Warnings)
            {
                warnings.Add(new ProblemBox(warning));
            }

            var conditions = root.Q<Foldout>("conditions");
            conditions.Clear();
            foreach (string line in view.ConditionLines())
            {
                var label = new Label(line);
                label.AddToClassList("er-wrap");
                label.AddToClassList("er-small");
                conditions.Add(label);
            }

            ShowOutcomes(view);
            ShowNoise(view);
            ShowCategories(view);
            ShowFailures();
            ShowLabelling();
        }

        // ② The five outcomes per arm: a bar each, a legend, and the table.
        private void ShowOutcomes(EvaluationReportView view)
        {
            var section = root.Q(OutcomesName);
            section.Clear();
            section.Add(new SectionTitle($"② Outcomes · {view.Report.Conditions.Prompts} prompts per arm"));
            foreach (var (arm, name) in new[] { (view.ArmA, "bar-a"), (view.ArmB, "bar-b") })
            {
                var row = Row("er-arm-bar");
                var label = new VisualElement();
                label.AddToClassList("er-arm-bar__name");
                label.Add(new Label(arm.Name) { name = name + "-name" });
                label[0].AddToClassList("er-arm-bar__title");
                var subtitle = new Label(arm.Subtitle);
                subtitle.AddToClassList("ag-muted");
                subtitle.AddToClassList("er-small");
                label.Add(subtitle);
                row.Add(label);
                row.Add(Bar(arm.Counts, name));
                section.Add(row);
            }

            var legend = Row("er-legend");
            for (int i = 0; i < EvaluationReportView.Outcomes.Length; i++)
            {
                var item = new VisualElement();
                item.AddToClassList("er-legend__item");
                var swatch = new VisualElement();
                swatch.AddToClassList("er-swatch");
                swatch.AddToClassList(OutcomeClasses[i]);
                item.Add(swatch);
                var name = new Label(EvaluationReportView.NameOf(EvaluationReportView.Outcomes[i]));
                name.AddToClassList("er-small");
                item.Add(name);
                legend.Add(item);
            }

            section.Add(legend);

            var table = new VisualElement { name = "outcome-table" };
            table.Add(TableRow(true, ("Outcome", null), (view.ArmA.Name, "er-cell--number"), (view.ArmB.Name, "er-cell--number"), ("Difference", "er-cell--difference")));
            foreach (var row in view.OutcomeRows())
            {
                string name = row.Meaning.Length > 0 ? $"{row.Name} — {row.Meaning}" : row.Name;
                string difference = EvaluationReportView.Signed(row.DifferencePoints) + " pts" + (row.Noise != null ? " · " + row.Noise : "");
                var line = TableRow(false, (name, null),
                    ($"{EvaluationReportView.Percent(row.ShareA)} ({row.CountA})", "er-cell--number"),
                    ($"{EvaluationReportView.Percent(row.ShareB)} ({row.CountB})", "er-cell--number"),
                    (difference, "er-cell--difference"));
                line.name = "outcome-" + row.Outcome;
                if (row.Outcome == DecisionOutcome.Correct)
                {
                    foreach (var cell in line.Children())
                    {
                        cell.AddToClassList("er-bold");
                    }
                }

                table.Add(line);
            }

            section.Add(table);
        }

        private static VisualElement Bar(OutcomeCounts counts, string name)
        {
            var bar = new VisualElement { name = name };
            bar.AddToClassList("er-bar");
            for (int i = 0; i < EvaluationReportView.Outcomes.Length; i++)
            {
                double share = EvaluationReportView.Share(counts, EvaluationReportView.Outcomes[i]);
                if (share <= 0)
                {
                    continue;
                }

                var part = new Label(share >= 0.08 ? EvaluationReportView.Percent(share) : "")
                {
                    name = "part-" + EvaluationReportView.Outcomes[i],
                    tooltip = $"{EvaluationReportView.NameOf(EvaluationReportView.Outcomes[i])}: {counts[EvaluationReportView.Outcomes[i]]} of {counts.Total}",
                };
                part.AddToClassList("er-bar__part");
                part.AddToClassList(OutcomeClasses[i]);
                part.style.width = Length.Percent((float)(share * 100));
                bar.Add(part);
            }

            return bar;
        }

        // ③ The noise check, in one sentence and its formula.
        private static void ShowNoise(EvaluationReportView view, VisualElement section)
        {
            section.Clear();
            section.Add(new SectionTitle("③ Noise check"));
            var card = new VisualElement();
            card.AddToClassList("ag-card");
            var line = new Label(view.NoiseLine) { name = "noise-line" };
            line.AddToClassList("er-wrap");
            card.Add(line);
            var formula = new Label($"Floor: {NoiseCheck.Formula}. Checked on accuracy, over the whole set and in each category with its own n.");
            formula.AddToClassList("ag-muted");
            formula.AddToClassList("er-wrap");
            formula.AddToClassList("er-small");
            card.Add(formula);
            section.Add(card);
        }

        private void ShowNoise(EvaluationReportView view) => ShowNoise(view, root.Q(NoiseName));

        // ④ Accuracy per category, with each one's own noise check.
        private void ShowCategories(EvaluationReportView view)
        {
            var section = root.Q(CategoriesName);
            section.Clear();
            section.Add(new SectionTitle("④ Correct answers by prompt category"));
            var rows = view.CategoryRows();
            if (rows.Count == 0)
            {
                section.Add(Muted("The report has no categories."));
                return;
            }

            var head = Row("er-table-row");
            head.AddToClassList("er-table-row--head");
            head.Add(Cell("Category", null));
            head.Add(Cell("n", "er-cell--n"));
            head.Add(Cell(view.ArmA.Name, "er-cell--accuracy"));
            head.Add(Cell(view.ArmB.Name, "er-cell--accuracy"));
            head.Add(Cell("Difference", "er-cell--difference"));
            section.Add(head);

            foreach (var row in rows)
            {
                var line = Row("er-table-row");
                line.name = "category-" + row.Id;
                var id = new VisualElement();
                id.AddToClassList("er-cell");
                id.AddToClassList("er-cell--chip");
                id.Add(new Chip(row.Id));
                line.Add(id);
                line.Add(Cell(row.N.ToString(), "er-cell--n"));
                line.Add(Accuracy(row.AccuracyA));
                line.Add(Accuracy(row.AccuracyB));
                var difference = Cell(EvaluationReportView.Signed(row.DifferencePoints) + " pts · " + (row.WithinNoise ? "within noise" : "beyond noise"), "er-cell--difference");
                difference.EnableInClassList("er-bold", !row.WithinNoise);
                line.Add(difference);
                section.Add(line);
            }

            var note = Muted("Categories exist so the headline number is never measured on player chat alone: agents are also asked by timers and game events. A small category needs a large difference before it says anything.");
            note.AddToClassList("er-small");
            section.Add(note);
        }

        private static VisualElement Accuracy(double points)
        {
            var cell = new VisualElement();
            cell.AddToClassList("er-cell");
            cell.AddToClassList("er-cell--accuracy");
            var bar = new VisualElement();
            bar.AddToClassList("er-mini-bar");
            var fill = new VisualElement();
            fill.AddToClassList("er-mini-bar__fill");
            fill.style.width = Length.Percent((float)Math.Max(0, Math.Min(100, points)));
            bar.Add(fill);
            cell.Add(bar);
            cell.Add(new Label(EvaluationReportView.Percent(points / 100)));
            return cell;
        }

        // ⑤ The answers that were not correct, per arm and outcome, each opening its whole record.
        private void ShowFailures()
        {
            var view = state.View;
            if (view == null)
            {
                return;
            }

            string[] arms = { view.ArmA.Name, view.ArmB.Name };
            string arm = arms[Mathf.Clamp(FailureArm, 0, 1)];
            var failures = view.Failures(arm, FailureOutcome);

            var title = root.Q("failures-title");
            title.Clear();
            title.Add(new SectionTitle($"⑤ Not correct in arm {(FailureArm == 0 ? "A" : "B")} · {failures.Count} prompt{(failures.Count == 1 ? "" : "s")}") { name = "failures-heading" });

            var choice = root.Q(FailureArmsName);
            choice.Clear();
            for (int i = 0; i < 2; i++)
            {
                int which = i;
                var button = new Button(() =>
                {
                    FailureArm = which;
                    ShowFailures();
                })
                {
                    name = which == 0 ? "failure-arm-a" : "failure-arm-b",
                    text = (which == 0 ? "A · " : "B · ") + arms[which],
                };
                button.EnableInClassList("er-selected", which == FailureArm);
                choice.Add(button);
            }

            var list = root.Q(FailureListName);
            list.Clear();
            if (failures.Count == 0)
            {
                list.Add(Muted("None."));
                return;
            }

            foreach (var answer in failures)
            {
                string key = answer.Arm + "\n" + answer.PromptId;
                bool open = opened.Contains(key);
                var row = new VisualElement { name = "failure-" + answer.PromptId };
                row.AddToClassList("er-failure");
                var top = new VisualElement();
                top.AddToClassList("er-failure__top");
                var id = new Label(answer.PromptId);
                id.AddToClassList("ag-mono");
                id.AddToClassList("er-failure__id");
                top.Add(id);
                var text = new VisualElement();
                text.AddToClassList("er-grow");
                var asked = new Label(EvaluationReportView.Asked(answer));
                asked.AddToClassList("er-wrap");
                text.Add(asked);
                text.Add(Muted(EvaluationReportView.ExpectedAndGot(answer) + " · " + EvaluationReportView.NameOf(answer.Outcome).ToLowerInvariant()));
                top.Add(text);
                var toggle = new Button(() =>
                {
                    if (!opened.Remove(key))
                    {
                        opened.Add(key);
                    }

                    ShowFailures();
                })
                {
                    name = "open",
                    text = open ? "Close ▾" : "Open ▸",
                };
                toggle.AddToClassList("er-failure__open");
                top.Add(toggle);
                row.Add(top);

                if (open)
                {
                    var details = new VisualElement { name = "details" };
                    details.AddToClassList("ag-card");
                    details.AddToClassList("er-details");
                    foreach (var detail in EvaluationReportView.Details(answer))
                    {
                        var line = new VisualElement();
                        line.AddToClassList("er-detail");
                        var k = new Label(detail.Key);
                        k.AddToClassList("er-detail__key");
                        line.Add(k);
                        var v = new Label(detail.Value);
                        v.AddToClassList("er-detail__value");
                        v.selection.isSelectable = true;
                        line.Add(v);
                        details.Add(line);
                    }

                    row.Add(details);
                }

                list.Add(row);
            }

            Literal(list);
        }

        // ⑥ The labellers' agreement on the set now, and the report's export.
        private void ShowLabelling()
        {
            var title = root.Q("labelling-title");
            title.Clear();
            title.Add(new SectionTitle("⑥ Labelling and export"));

            var agreement = state.Agreement;
            root.Q<Label>("agreement-percent").text = agreement?.PercentAgreement == null ? "" : $"{agreement.PercentAgreement:0}%";
            Display(root.Q("agreement-percent"), agreement?.PercentAgreement != null);
            root.Q<Label>(AgreementHeadlineName).text = agreement == null ? "" : EvaluationReportView.AgreementHeadline(agreement);
            root.Q<Label>("agreement-detail").text = agreement == null ? "" : EvaluationReportView.AgreementDetail(agreement);
            root.Q<Label>("agreement-note").text = state.AgreementNote ?? "";

            bool folder = !string.IsNullOrEmpty(state.ShownPath) && System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(state.ShownPath));
            root.Q<Button>("show-files").SetEnabled(folder);
        }

        // ---- Plumbing -----------------------------------------------------------------------------

        private void Wire(string name, Action clicked)
        {
            root.Q<Button>(name).clicked += clicked;
        }

        private ObjectField Field<T>(string name, Action<UnityEngine.Object> changed) where T : UnityEngine.Object
        {
            var field = root.Q<ObjectField>(name);
            field.objectType = typeof(T);
            field.allowSceneObjects = false;
            field.RegisterValueChangedCallback(e => changed(e.newValue));
            return field;
        }

        private TextField Text(string name, int arm)
        {
            var field = root.Q<TextField>(name);
            field.tooltip = "How the report names this arm, such as control or guards-on. The two must differ.";
            field.RegisterValueChangedCallback(e => NameChanged?.Invoke(arm, e.newValue));
            return field;
        }

        private Toggle Guard(string name, int arm)
        {
            var toggle = root.Q<Toggle>(name);
            toggle.tooltip = "The target-grounding guard (DecideOptions.CheckTarget). On, a decision whose target the model's own token "
                           + "probabilities show it swapped in for one not on the list becomes none. Off is the usual control arm.";
            toggle.RegisterValueChangedCallback(e => GuardChanged?.Invoke(arm, e.newValue));
            return toggle;
        }

        private static VisualElement Row(string cls)
        {
            var row = new VisualElement();
            row.AddToClassList("er-row");
            row.AddToClassList(cls);
            return row;
        }

        private static VisualElement TableRow(bool head, params (string Text, string Class)[] cells)
        {
            var row = Row("er-table-row");
            row.EnableInClassList("er-table-row--head", head);
            foreach (var (text, cls) in cells)
            {
                row.Add(Cell(text, cls));
            }

            return row;
        }

        private static Label Cell(string text, string cls)
        {
            var cell = new Label(text);
            cell.AddToClassList("er-cell");
            if (cls != null)
            {
                cell.AddToClassList(cls);
            }

            return cell;
        }

        private static Label Muted(string text)
        {
            var label = new Label(text);
            label.AddToClassList("ag-muted");
            label.AddToClassList("er-wrap");
            return label;
        }

        private static void Display(VisualElement element, bool shown)
        {
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
