using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Agenerela.Editor.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Agenerela.Editor.Diagnostics
{
    /// <summary>
    /// What the Decision Log window shows (#172, screen 3), as a UI Toolkit tree on the #167 kit:
    /// the toolbar with ② its filters, ① the list, and ④ to ⑥ the decision selected.
    /// </summary>
    /// <remarks>
    /// It holds no rows and decides nothing: <see cref="DecisionLogWindow"/> owns the store and the
    /// filter, and calls <see cref="Show"/> with all of it. What a person does here comes back as the
    /// events below. It needs no window, so a test builds it and reads its tree directly; every part a
    /// test or the window reaches is named (the constants below).
    /// </remarks>
    internal sealed class DecisionLogUi
    {
        internal const string LayoutPath = "Packages/com.agenerela.framework/Editor/Diagnostics/DecisionLogWindow.uxml";
        internal const string SheetPath = "Packages/com.agenerela.framework/Editor/Diagnostics/DecisionLogWindow.uss";

        internal const string RecordingName = "recording", ClearName = "clear", ClearOnPlayName = "clear-on-play", AgentMenuName = "agent-menu",
            FiltersName = "outcome-filters", SearchName = "search", ExportName = "export", ListName = "list", EmptyName = "empty",
            StatusName = "status", NoSelectionName = "no-selection", DetailName = "detail", TitleName = "detail-title",
            SubtitleName = "detail-subtitle", SelectInSceneName = "select-in-scene", CopyJsonName = "copy-json", GuardsName = "guards",
            ResultName = "result", TelemetryName = "telemetry", OfferedActionsName = "offered-actions", OfferedTargetsName = "offered-targets",
            ReplyName = "reply", AnsweredName = "answered", AskedName = "asked";

        // The parts of one list row.
        internal const string RowIconName = "row-icon", RowTimeName = "row-time", RowAgentName = "row-agent", RowAskedName = "row-asked",
            RowResultName = "row-result", RowLatencyName = "row-latency";

        internal const string AllAgents = "All agents";

        /// <summary>Everything the window shows, handed over whole on each change.</summary>
        internal sealed class State
        {
            /// <summary>The rows the filters let through, newest first.</summary>
            internal List<DecisionLogRow> Shown = new List<DecisionLogRow>();

            /// <summary>Every row in the log, oldest first, for the status line.</summary>
            internal IReadOnlyList<DecisionLogRow> All = Array.Empty<DecisionLogRow>();

            internal DecisionLogFilter Filter = new DecisionLogFilter();
            internal Dictionary<Outcome, int> Counts = new Dictionary<Outcome, int>();
            internal List<string> Agents = new List<string>();
            internal bool Recording = true;
            internal bool ClearOnPlay;
            internal bool Playing;

            /// <summary>The row whose story the detail panel tells, or null.</summary>
            internal DecisionLogRow Selected;
        }

        private readonly Dictionary<Outcome, ToolbarToggle> filterToggles = new Dictionary<Outcome, ToolbarToggle>();
        private readonly ListView list;
        private readonly VisualElement detail;
        private State state = new State();
        private DecisionLogRow detailShown;
        private string agentsShown = "";

        internal event Action<bool> RecordingChanged;
        internal event Action<bool> ClearOnPlayChanged;
        internal event Action ClearClicked;
        internal event Action ExportClicked;
        internal event Action<string> AgentChanged;
        internal event Action<Outcome, bool> OutcomeToggled;
        internal event Action<string> SearchChanged;
        internal event Action<DecisionLogRow> SelectionChanged;
        internal event Action<DecisionLogRow> SelectInScene;
        internal event Action<DecisionLogRow> CopyJson;

        internal DecisionLogUi(VisualElement root)
        {
            Root = root;

            Find<ToolbarToggle>(RecordingName).RegisterValueChangedCallback(e => RecordingChanged?.Invoke(e.newValue));
            Find<ToolbarToggle>(ClearOnPlayName).RegisterValueChangedCallback(e => ClearOnPlayChanged?.Invoke(e.newValue));
            Find<ToolbarButton>(ClearName).clicked += () => ClearClicked?.Invoke();
            Find<ToolbarButton>(ExportName).clicked += () => ExportClicked?.Invoke();
            Find<ToolbarSearchField>(SearchName).RegisterValueChangedCallback(e => SearchChanged?.Invoke(e.newValue));

            var filters = Find<VisualElement>(FiltersName);
            foreach (var outcome in DecisionLogFilter.Outcomes)
            {
                var toggle = new ToolbarToggle { name = "filter-" + OutcomeIcon.ModifierClass(outcome).Substring(OutcomeIcon.UssClassName.Length + 2), tooltip = OutcomeIcon.Describe(outcome) };
                toggle.AddToClassList("dl-filter");
                var icon = new OutcomeIcon(outcome);
                icon.pickingMode = PickingMode.Ignore;
                toggle.Insert(0, icon);
                toggle.SetValueWithoutNotify(true);
                var captured = outcome;
                toggle.RegisterValueChangedCallback(e => OutcomeToggled?.Invoke(captured, e.newValue));
                filters.Add(toggle);
                filterToggles[outcome] = toggle;
            }

            list = Find<ListView>(ListName);
            list.makeItem = MakeRow;
            list.bindItem = (element, index) => BindRow(element, state.Shown[index]);
            list.itemsSource = state.Shown;
            list.selectionChanged += _ =>
            {
                var row = list.selectedItem as DecisionLogRow;
                if (row != state.Selected)
                {
                    SelectionChanged?.Invoke(row);
                }
            };

            detail = Find<VisualElement>(DetailName);
        }

        /// <summary>The tree's root, which the window adds to its own.</summary>
        internal VisualElement Root { get; }

        /// <summary>The list's tree from the package's UXML and stylesheets.</summary>
        internal static VisualElement Load()
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath)
                         ?? throw new InvalidOperationException($"The Decision Log window's layout is missing: {LayoutPath}.");
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

        /// <summary>Shows <paramref name="shown"/>: the toolbar, the list and the selected decision.</summary>
        internal void Show(State shown)
        {
            state = shown ?? new State();

            Find<ToolbarToggle>(RecordingName).SetValueWithoutNotify(state.Recording);
            Find<ToolbarToggle>(RecordingName).text = state.Recording ? "● Recording" : "Recording off";
            Find<ToolbarToggle>(ClearOnPlayName).SetValueWithoutNotify(state.ClearOnPlay);

            foreach (var outcome in DecisionLogFilter.Outcomes)
            {
                var toggle = filterToggles[outcome];
                toggle.SetValueWithoutNotify(!state.Filter.Hidden.Contains(outcome));
                state.Counts.TryGetValue(outcome, out var count);
                toggle.text = count.ToString(CultureInfo.InvariantCulture);
            }

            ShowAgents();

            var search = Find<ToolbarSearchField>(SearchName);
            if (search.value != (state.Filter.Search ?? ""))
            {
                search.SetValueWithoutNotify(state.Filter.Search ?? "");
            }

            list.itemsSource = state.Shown;
            list.RefreshItems();
            var index = state.Selected == null ? -1 : state.Shown.IndexOf(state.Selected);
            if (index >= 0)
            {
                list.SetSelectionWithoutNotify(new[] { index });
            }
            else
            {
                list.ClearSelection();
            }

            var empty = Find<Label>(EmptyName);
            empty.text = EmptyText();
            empty.style.display = state.Shown.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            list.style.display = state.Shown.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            Find<Label>(StatusName).text = StatusText(state);

            Find<Label>(NoSelectionName).style.display = state.Selected == null ? DisplayStyle.Flex : DisplayStyle.None;
            detail.style.display = state.Selected == null ? DisplayStyle.None : DisplayStyle.Flex;

            // Rebuilt when the selection changes, or when its row gained its execution.
            if (state.Selected != detailShown || (state.Selected != null && detail.userData != state.Selected.Line))
            {
                ShowDetail(state.Selected);
            }
        }

        // ---- ① The list ---------------------------------------------------------------------------

        internal static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("dl-row");

            row.Add(new OutcomeIcon { name = RowIconName });

            var who = new VisualElement();
            who.AddToClassList("dl-col-who");
            who.Add(Text(RowTimeName, "ag-muted", "dl-row__time"));
            who.Add(Text(RowAgentName, "dl-row__agent"));
            row.Add(who);

            var what = new VisualElement();
            what.AddToClassList("dl-col-what");
            what.Add(Text(RowAskedName, "dl-row__asked"));
            what.Add(Text(RowResultName, "dl-row__result"));
            row.Add(what);

            row.Add(Text(RowLatencyName, "ag-muted", "dl-col-latency"));
            return row;
        }

        /// <summary>Fills a row made by <see cref="MakeRow"/> with <paramref name="row"/>.</summary>
        internal static void BindRow(VisualElement element, DecisionLogRow row)
        {
            var line = row.Line;
            element.Q<OutcomeIcon>(RowIconName).Outcome = row.Outcome;
            element.Q<Label>(RowTimeName).text = DecisionLogRow.TimeText(line);
            element.Q<Label>(RowAgentName).text = row.Agent;
            element.Q<Label>(RowAskedName).text = DecisionLogRow.AskedText(line);
            var result = element.Q<Label>(RowResultName);
            result.text = DecisionLogRow.ResultText(line);
            result.tooltip = result.text;
            element.Q<Label>(RowLatencyName).text = DecisionLogRow.LatencyText(line);
            element.tooltip = OutcomeIcon.Describe(row.Outcome);
        }

        private string EmptyText()
        {
            if (state.All.Count > 0)
            {
                return "No decision matches the filters.";
            }

            if (!state.Recording)
            {
                return "Recording is off. Switch it on to see each decision your agents make.";
            }

            return state.Playing
                ? "Waiting for the first decision. Each one appears here as an agent makes it."
                : "Enter Play mode: each decision any agent makes appears here, newest first.";
        }

        internal static string StatusText(State state)
        {
            var all = state.All;
            var agents = all.Select(r => r.Agent).Distinct().Count();
            var timed = all.Where(r => r.Line.Error == null).ToList();
            var text = $"{all.Count} decision{(all.Count == 1 ? "" : "s")} · {agents} agent{(agents == 1 ? "" : "s")}";
            if (timed.Count > 0)
            {
                text += " · average " + timed.Average(r => r.Line.LatencySeconds).ToString("0.00", CultureInfo.InvariantCulture) + " s";
            }

            if (state.Shown.Count != all.Count)
            {
                text += $" · {state.Shown.Count} shown";
            }

            return text;
        }

        private void ShowAgents()
        {
            var menu = Find<ToolbarMenu>(AgentMenuName);
            var selected = string.IsNullOrEmpty(state.Filter.Agent) ? AllAgents : state.Filter.Agent;
            menu.text = selected;

            var key = selected + "\n" + string.Join("\n", state.Agents);
            if (key == agentsShown)
            {
                return;
            }

            agentsShown = key;
            menu.menu.ClearItems();
            menu.menu.AppendAction(AllAgents, _ => AgentChanged?.Invoke(null),
                _ => string.IsNullOrEmpty(state.Filter.Agent) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            foreach (var agent in state.Agents)
            {
                var name = agent;
                menu.menu.AppendAction(name.Replace("/", "∕"), _ => AgentChanged?.Invoke(name),
                    _ => state.Filter.Agent == name ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
        }

        // ---- ④ to ⑥ The decision selected ----------------------------------------------------------

        private void ShowDetail(DecisionLogRow row)
        {
            detailShown = row;
            detail.Clear();
            detail.userData = row?.Line;
            if (row == null)
            {
                return;
            }

            var line = row.Line;
            var outcome = row.Outcome;

            var header = new VisualElement();
            header.AddToClassList("dl-header");
            header.Add(new OutcomeIcon(outcome));
            header.Add(Text(TitleName, "dl-header__title").Set(Title(outcome)));
            detail.Add(header);
            detail.Add(Text(SubtitleName, "ag-muted", "dl-wrap").Set($"{row.Agent} · {DecisionLogRow.TimeText(line)} · {Subtitle(outcome)}"));

            var actions = new VisualElement();
            actions.AddToClassList("dl-actions");
            var select = new Button(() => SelectInScene?.Invoke(row)) { name = SelectInSceneName, text = "Select agent in scene" };
            select.SetEnabled(row.GameObject != 0);
            select.tooltip = row.GameObject != 0 ? "Select the agent's GameObject in the Hierarchy." : "This agent has no GameObject, or the row was read from a file.";
            actions.Add(select);
            actions.Add(new Button(() => CopyJson?.Invoke(row)) { name = CopyJsonName, text = "Copy as JSON", tooltip = "Copy this decision's log line, the same JSON Export writes." });
            detail.Add(actions);

            // 1 · Asked
            detail.Add(new SectionTitle("1 · Asked"));
            var asked = new VisualElement { name = AskedName };
            if (!string.IsNullOrEmpty(line.Label)) asked.Add(Field("Label", line.Label));
            asked.Add(Field("Stimulus", "“" + (line.Stimulus ?? "") + "”"));
            var observations = DecisionLogDetail.Observations(line);
            asked.Add(Field("Observations", observations.Count > 0 ? string.Join("\n", observations) : (line.Request == null ? "not recorded" : "none")));
            if (line.Request?.History != null && line.Request.History.Count > 0)
            {
                asked.Add(Field("Memory", $"{line.Request.History.Count / 2} earlier exchange{(line.Request.History.Count == 2 ? "" : "s")} sent"));
            }

            if (line.ChoicesOffered != null) asked.Add(Field("Choices", string.Join(", ", line.ChoicesOffered)));
            detail.Add(asked);

            // 2 · Offered to the model
            detail.Add(new SectionTitle("2 · Offered to the model"));
            detail.Add(ChipField("Actions", OfferedActionsName, line.Request?.Actions, line.ModelDecision?.Action ?? line.Decision?.Action));
            detail.Add(ChipField("Targets", OfferedTargetsName, line.Request?.Targets, line.ModelDecision?.Target ?? line.Decision?.Target));
            if (line.TargetsUnavailable != null && line.TargetsUnavailable.Count > 0)
            {
                detail.Add(Field("Not offered", string.Join("\n", line.TargetsUnavailable.Select(t => $"{t.Action} · {t.Target}: {t.Reason}"))));
            }

            // 3 · Model answered
            detail.Add(new SectionTitle("3 · Model answered"));
            var reply = DecisionLogDetail.ReplyText(line);
            var replyLabel = Text(ReplyName, "dl-mono", "dl-reply").Set(reply ?? (line.Error != null ? "No reply: the request failed." : "No reply recorded."));
            replyLabel.selection.isSelectable = true;
            detail.Add(replyLabel);
            if (line.Decision != null)
            {
                detail.Add(Text(AnsweredName, "ag-muted", "dl-wrap").Set("The game got: " + DecisionLogRow.ResultText(line)));
            }

            // 4 · Guards, in order
            detail.Add(new SectionTitle("4 · Guards, in order"));
            detail.Add(GuardList(line));

            // 5 · Result
            detail.Add(new SectionTitle("5 · Result"));
            var result = Text(ResultName, "ag-card", "dl-wrap").Set(DecisionLogDetail.ResultSentence(line));
            result.EnableInClassList("dl-result--contained", outcome == Outcome.Contained);
            result.EnableInClassList("dl-result--error", outcome == Outcome.Error);
            detail.Add(result);
            var refusal = DecisionLogDetail.RefusalLine(line);
            if (refusal != null)
            {
                detail.Add(Field("Said instead", "“" + refusal + "”"));
            }

            // 6 · Telemetry
            detail.Add(new SectionTitle("6 · Telemetry"));
            var telemetry = new VisualElement { name = TelemetryName };
            foreach (var (label, value) in DecisionLogDetail.Telemetry(row))
            {
                telemetry.Add(Field(label, value));
            }

            detail.Add(telemetry);
        }

        internal static VisualElement GuardList(DecisionLogLine line)
        {
            var guards = new VisualElement { name = GuardsName };
            var rows = DecisionLogDetail.Guards(line);
            if (rows.Count == 0)
            {
                guards.Add(Text(null, "ag-muted", "dl-wrap").Set(line.Error != null
                    ? "None ran: there was no decision to check."
                    : "None recorded."));
                return guards;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var g = rows[i];
                var item = new VisualElement { name = "guard-" + i };
                item.AddToClassList("dl-guard");
                item.AddToClassList(GuardClass(g.Step));
                item.Add(Text(null, "ag-muted", "dl-guard__index").Set((i + 1).ToString(CultureInfo.InvariantCulture)));
                item.Add(Text("guard-name", "dl-guard__name").Set(g.Guard));
                item.Add(Text("guard-step", "dl-guard__step").Set(StepText(g.Step)));
                item.Add(Text("guard-reason", "dl-guard__reason").Set(g.Reason ?? ""));
                guards.Add(item);
            }

            return guards;
        }

        /// <summary>The class a guard row carries for its step, such as <c>dl-guard--not-run</c>.</summary>
        internal static string GuardClass(GuardStep step) => step switch
        {
            GuardStep.Passed => "dl-guard--passed",
            GuardStep.ReplacedStatement => "dl-guard--replaced",
            GuardStep.Stopped => "dl-guard--stopped",
            GuardStep.Failed => "dl-guard--failed",
            GuardStep.NotRun => "dl-guard--not-run",
            GuardStep.RefusedAtExecute => "dl-guard--refused",
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
        };

        internal static string StepText(GuardStep step) => step switch
        {
            GuardStep.Passed => "✓ Passed",
            GuardStep.ReplacedStatement => "✎ New statement",
            GuardStep.Stopped => "⚠ Stopped",
            GuardStep.Failed => "✕ Threw",
            GuardStep.NotRun => "Not run",
            GuardStep.RefusedAtExecute => "⚠ Refused at Execute",
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
        };

        private static string Title(Outcome outcome) => outcome switch
        {
            Outcome.Executed => "Executed",
            Outcome.NoAction => "No action",
            Outcome.Contained => "Contained by a guard",
            Outcome.Error => "Error",
            Outcome.NotExecuted => "Decided, not executed yet",
            _ => outcome.ToString(),
        };

        private static string Subtitle(Outcome outcome) => outcome switch
        {
            Outcome.Executed => "the chosen action ran",
            Outcome.NoAction => "the model chose to do nothing",
            Outcome.Contained => "the model's choice was blocked before it ran",
            Outcome.Error => "the request failed, so nothing was decided",
            Outcome.NotExecuted => "nothing has run it yet",
            _ => "",
        };

        private static VisualElement Field(string label, string value)
        {
            var field = new VisualElement();
            field.AddToClassList("dl-field");
            field.Add(Text(null, "ag-muted", "dl-field__label").Set(label));
            var text = Text(null, "dl-field__value").Set(value ?? "");
            text.selection.isSelectable = true;
            field.Add(text);
            return field;
        }

        private static VisualElement ChipField(string label, string name, List<string> ids, string chosen)
        {
            var field = new VisualElement();
            field.AddToClassList("dl-field");
            field.Add(Text(null, "ag-muted", "dl-field__label").Set(label));
            var chips = new VisualElement { name = name };
            chips.AddToClassList("ag-chips");
            chips.AddToClassList("dl-field__value");
            if (ids == null || ids.Count == 0)
            {
                chips.Add(Text(null, "ag-muted").Set(ids == null ? "not recorded" : "none"));
            }
            else
            {
                foreach (var id in ids)
                {
                    var chip = new Chip(id);
                    if (id == chosen)
                    {
                        chip.tooltip = "The model's choice.";
                        chip.style.unityFontStyleAndWeight = FontStyle.Bold;
                    }

                    chips.Add(chip);
                }
            }

            field.Add(chips);
            return field;
        }

        private static Label Text(string name, params string[] classes)
        {
            var label = new Label { name = name };
            foreach (var c in classes)
            {
                label.AddToClassList(c);
            }

            return label;
        }

        private T Find<T>(string name) where T : VisualElement
        {
            return Root.Q<T>(name) ?? throw new InvalidOperationException($"The Decision Log's layout has no {typeof(T).Name} named \"{name}\".");
        }
    }

    internal static class DecisionLogLabels
    {
        /// <summary>Sets a label's text and returns it, for building a tree in one expression.</summary>
        internal static Label Set(this Label label, string text)
        {
            label.text = text;
            return label;
        }
    }
}
