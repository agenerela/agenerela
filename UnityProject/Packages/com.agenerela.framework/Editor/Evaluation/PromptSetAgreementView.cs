using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Editor.UI;
using Agenerela.Evaluation;
using UnityEditor;
using UnityEngine.UIElements;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// Agreement mode: how many people answered each prompt and how many agreed, overall and by
    /// category, and the prompts where they split. A split is settled only when a person clicks
    /// to settle it, as findings.md asks; nothing here settles one on its own.
    /// </summary>
    internal sealed class PromptSetAgreementView
    {
        internal const string LayoutPath = PromptSetUi.Folder + "PromptSetAgreementView.uxml";

        // Names a test reaches. The rest are in the UXML.
        internal const string CategoriesName = "categories", SplitsName = "splits", HeadlineName = "agreement-headline",
            PercentName = "agreement-percent";

        /// <summary>The table's columns, by name, in order.</summary>
        internal static readonly string[] Columns = { "category", "prompts", "twice", "agreed", "split", "agreement" };

        /// <summary>One category's row in the table.</summary>
        internal sealed class CategoryRow
        {
            internal string Id;
            internal int Prompts;
            internal int Twice;
            internal int Split;
            internal int Agreed => Twice - Split;

            /// <summary>The share of prompts answered twice or more that all agreed on; null with none.</summary>
            internal double? Percent => Twice == 0 ? (double?)null : 100.0 * Agreed / Twice;
        }

        private readonly PromptSet set;
        private readonly AgreementReport report;
        private readonly Action afterSettle;
        private readonly Dictionary<string, string> notes = new Dictionary<string, string>();
        private readonly List<CategoryRow> rows;
        private VisualElement root;
        private MultiColumnListView table;

        internal PromptSetAgreementView(PromptSet set, IReadOnlyList<AnnotatorLabels> labels, Action afterSettle)
        {
            this.set = set;
            this.afterSettle = afterSettle;
            report = AgreementReport.Compute(set, labels);
            rows = set.Categories.Select(category =>
            {
                var prompts = report.Prompts.Where(p => p.CategoryId == category.Id).ToList();
                return new CategoryRow
                {
                    Id = category.Id,
                    Prompts = category.Prompts.Count,
                    Twice = prompts.Count(p => p.Answered >= 2),
                    Split = prompts.Count(p => p.IsSplit),
                };
            }).ToList();
        }

        /// <summary>The table's rows in the order shown.</summary>
        internal IReadOnlyList<CategoryRow> Rows => rows;

        /// <summary>The view's tree, made the first time it is asked for.</summary>
        internal VisualElement Root => root ?? Build();

        private VisualElement Build()
        {
            root = PromptSetUi.Load(LayoutPath);

            int twice = report.AnsweredByTwoOrMore;
            root.Q<Label>(PercentName).text = report.PercentAgreement is double percent ? $"{percent:0}%" : "–";
            root.Q<Label>(HeadlineName).text = twice == 0
                ? "No prompt has two answers yet."
                : $"{report.Unanimous} of {twice} prompts answered by two or more people got the same answer from all.";

            BuildTable();
            BuildSplits();
            BuildEveryPrompt();
            return root;
        }

        // ---- By category ---------------------------------------------------------------------------

        private void BuildTable()
        {
            table = root.Q<MultiColumnListView>(CategoriesName);
            table.selectionType = SelectionType.None;
            table.sortingMode = ColumnSortingMode.Custom;
            table.columns.Clear();
            Column(Columns[0], "Category", 160, r => r.Id, false);
            Column(Columns[1], "Prompts", 64, r => r.Prompts.ToString(), true);
            Column(Columns[2], "Twice+", 64, r => r.Twice.ToString(), true, "Answered by two or more people.");
            Column(Columns[3], "Agreed", 64, r => r.Agreed.ToString(), true, "Answered twice or more, and everyone gave the same answer.");
            Column(Columns[4], "Split", 56, r => r.Split.ToString(), true, "Answered twice or more, and the answers differ.");
            table.columns.Add(new Column
            {
                name = Columns[5],
                title = "Agreement",
                stretchable = true,
                minWidth = 110,
                makeCell = () =>
                {
                    var cell = new VisualElement();
                    cell.AddToClassList("ps-row");
                    var bar = new VisualElement();
                    bar.AddToClassList("ps-bar");
                    var fill = new VisualElement { name = "fill" };
                    fill.AddToClassList("ps-bar__fill");
                    bar.Add(fill);
                    cell.Add(bar);
                    var label = new Label { name = "percent" };
                    label.AddToClassList("ps-cell");
                    label.AddToClassList("ps-cell--number");
                    label.style.width = 44;
                    cell.Add(label);
                    return cell;
                },
                bindCell = (cell, i) =>
                {
                    var row = rows[i];
                    cell.Q("fill").style.width = Length.Percent((float)(row.Percent ?? 0));
                    cell.Q<Label>("percent").text = row.Percent is double p ? $"{p:0}%" : "–";
                },
            });

            table.columnSortingChanged += () =>
            {
                Sort(table.sortedColumns.FirstOrDefault());
                table.RefreshItems();
            };

            table.itemsSource = rows;
            table.style.height = Math.Max(1, rows.Count) * table.fixedItemHeight + 28;
        }

        private void Column(string name, string title, float width, Func<CategoryRow, string> text, bool number, string tooltip = null)
        {
            table.columns.Add(new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = 40,
                makeHeader = tooltip == null ? null : (Func<VisualElement>)(() => new Label(title) { tooltip = tooltip }),
                makeCell = () =>
                {
                    var label = new Label();
                    label.AddToClassList("ps-cell");
                    if (number) label.AddToClassList("ps-cell--number");
                    return label;
                },
                bindCell = (cell, i) => ((Label)cell).text = text(rows[i]),
            });
        }

        /// <summary>Orders the table by <paramref name="by"/>; the set's own order when there is none.</summary>
        internal void Sort(SortColumnDescription by)
        {
            var order = set.Categories.Select(c => c.Id).ToList();
            IEnumerable<CategoryRow> sorted = rows.OrderBy(r => order.IndexOf(r.Id));
            if (by != null)
            {
                Func<CategoryRow, IComparable> key = by.columnName switch
                {
                    "prompts" => r => r.Prompts,
                    "twice" => r => r.Twice,
                    "agreed" => r => r.Agreed,
                    "split" => r => r.Split,
                    "agreement" => r => r.Percent ?? -1,
                    _ => r => r.Id,
                };
                sorted = by.direction == SortDirection.Descending ? sorted.OrderByDescending(key) : sorted.OrderBy(key);
            }

            var next = sorted.ToList();
            rows.Clear();
            rows.AddRange(next);
        }

        // ---- Splits --------------------------------------------------------------------------------

        private void BuildSplits()
        {
            root.Q<Label>("splits-title").text = $"Splits · {report.Splits.Count}";
            var host = root.Q(SplitsName);
            if (report.Splits.Count == 0)
            {
                var none = new Label("None.");
                none.AddToClassList("ag-muted");
                host.Add(none);
                return;
            }

            foreach (var split in report.Splits)
            {
                if (set.TryFind(split.PromptId, out var prompt, out _))
                {
                    host.Add(Split(split, prompt));
                }
            }
        }

        private VisualElement Split(PromptAgreement split, EvalPrompt prompt)
        {
            var card = new VisualElement { name = "split-" + prompt.Id };
            card.AddToClassList("ag-card");
            card.AddToClassList("ps-split");

            var title = new Label($"{prompt.Id} · {prompt.Label}: {prompt.Stimulus}");
            title.AddToClassList("ps-wrap");
            title.AddToClassList("ps-bold");
            card.Add(title);

            foreach (var answer in split.Answers)
            {
                card.Add(AnswerLine(answer.Key, answer.Value.ToString()));
            }

            card.Add(AnswerLine("Expected now", $"{prompt.ExpectedAction}/{prompt.ExpectedTarget}"));

            if (prompt.Labels != null)
            {
                // Settled already: the split still counts against agreement, but the decision is
                // made. Changing it is an edit in Write mode, never a click here.
                card.Add(AnswerLine("Settled", "The answers above are kept in the prompt's labels."));
                return card;
            }

            notes.TryGetValue(prompt.Id, out string note);
            var noteField = new TextField("Note (why)") { value = note ?? "" };
            noteField.RegisterValueChangedCallback(e => notes[prompt.Id] = e.newValue);
            card.Add(noteField);

            var buttons = new VisualElement();
            buttons.AddToClassList("ps-row");
            buttons.AddToClassList("ps-split__buttons");
            foreach (var answer in split.DistinctAnswers)
            {
                var chosen = answer;
                buttons.Add(new Button(() => Settle(split, chosen.Action, new ExpectedTarget(chosen.Target))) { text = $"Settle as {chosen}" });
            }

            // Both right, same action and different targets: an open choice (findings.md).
            var actions = split.DistinctAnswers.Select(a => a.Action).Distinct().ToList();
            bool sameAction = actions.Count == 1 && split.DistinctAnswers.All(a => a.Target != TargetRegistry.NoTarget);
            var openChoice = new Button(() => Settle(split, actions[0], new ExpectedTarget(split.DistinctAnswers.Select(a => a.Target).ToArray())))
            {
                text = "Open choice",
                tooltip = "Either target is right: for answers with the same action and different targets.",
            };
            openChoice.SetEnabled(sameAction);
            buttons.Add(openChoice);
            card.Add(buttons);
            return card;
        }

        private static VisualElement AnswerLine(string who, string what)
        {
            var line = new VisualElement();
            line.AddToClassList("ps-split__answer");
            var name = new Label(who);
            name.AddToClassList("ps-split__who");
            line.Add(name);
            var answer = new Label(what);
            answer.AddToClassList("ag-mono");
            answer.AddToClassList("ps-wrap");
            answer.AddToClassList("ps-shrink");
            line.Add(answer);
            return line;
        }

        private void BuildEveryPrompt()
        {
            var every = root.Q<Foldout>("every-prompt");
            every.text = $"Every answered prompt ({report.Prompts.Count})";
            foreach (var prompt in report.Prompts)
            {
                every.Add(AnswerLine(prompt.PromptId, $"{prompt.Answered} answered · {prompt.Agreeing} agree" + (prompt.IsSplit ? " · split" : "")));
            }
        }

        private void Settle(PromptAgreement split, string action, ExpectedTarget target)
        {
            notes.TryGetValue(split.PromptId, out string note);
            string answer = $"{action}/{target}";
            if (!EditorUtility.DisplayDialog($"Settle {split.PromptId}?",
                    $"The expected answer becomes {answer}, and each person's own answer is kept in the prompt's labels. " +
                    "Never settle to match a model's reply.",
                    "Settle", "Cancel"))
            {
                return;
            }

            set.Settle(split, action, target, note);
            afterSettle();
        }
    }
}
