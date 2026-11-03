using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Evaluation;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// Agreement mode: how many people answered each prompt and how many agreed, overall and by
    /// category, and the prompts where they split. A split is settled only when a person clicks
    /// to settle it, as findings.md asks; nothing here settles one on its own.
    /// </summary>
    internal sealed class PromptSetAgreementView
    {
        private readonly PromptSet set;
        private readonly AgreementReport report;
        private readonly Action afterSettle;
        private readonly Dictionary<string, string> notes = new Dictionary<string, string>();
        private Vector2 scroll;
        private bool showEveryPrompt;

        internal PromptSetAgreementView(PromptSet set, IReadOnlyList<AnnotatorLabels> labels, Action afterSettle)
        {
            this.set = set;
            this.afterSettle = afterSettle;
            report = AgreementReport.Compute(set, labels);
        }

        internal void Draw()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space();

            int twice = report.AnsweredByTwoOrMore;
            string headline = twice == 0
                ? "No prompt has two answers yet."
                : $"{report.Unanimous} of {twice} prompts answered by two or more people got the same answer from all ({report.PercentAgreement:0}%).";
            EditorGUILayout.LabelField(headline, new GUIStyle(EditorStyles.boldLabel) { wordWrap = true, fontSize = 14 });
            EditorGUILayout.LabelField(
                "An unsettled prompt counts its expected answer as its one writer's. Settling a split writes everyone's answer into the prompt's labels.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space();
            DrawCategories();
            EditorGUILayout.Space();
            DrawSplits();
            EditorGUILayout.Space();

            showEveryPrompt = EditorGUILayout.Foldout(showEveryPrompt, $"Every answered prompt ({report.Prompts.Count})", true);
            if (showEveryPrompt)
            {
                foreach (var prompt in report.Prompts)
                {
                    EditorGUILayout.LabelField(prompt.PromptId,
                        $"{prompt.Answered} answered · {prompt.Agreeing} agree" + (prompt.IsSplit ? " · split" : ""));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCategories()
        {
            EditorGUILayout.LabelField("By category", EditorStyles.miniBoldLabel);
            foreach (var category in set.Categories)
            {
                var prompts = report.Prompts.Where(p => p.CategoryId == category.Id).ToList();
                int twice = prompts.Count(p => p.Answered >= 2);
                int splits = prompts.Count(p => p.IsSplit);
                EditorGUILayout.LabelField(category.Id,
                    $"{category.Prompts.Count} prompts · {twice} answered twice or more · {twice - splits} agreed · {splits} split");
            }
        }

        private void DrawSplits()
        {
            EditorGUILayout.LabelField($"Splits · {report.Splits.Count}", EditorStyles.miniBoldLabel);
            if (report.Splits.Count == 0)
            {
                EditorGUILayout.LabelField("None.", EditorStyles.miniLabel);
                return;
            }

            foreach (var split in report.Splits)
            {
                if (!set.TryFind(split.PromptId, out var prompt, out _))
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{prompt.Id} · {prompt.Label}: {prompt.Stimulus}", EditorStyles.wordWrappedLabel);
                    foreach (var answer in split.Answers)
                    {
                        EditorGUILayout.LabelField("    " + answer.Key, answer.Value.ToString());
                    }

                    EditorGUILayout.LabelField("    Expected now", $"{prompt.ExpectedAction}/{prompt.ExpectedTarget}");

                    notes.TryGetValue(prompt.Id, out string note);
                    notes[prompt.Id] = EditorGUILayout.TextField("    Note (why)", note ?? "");

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16);
                        foreach (var answer in split.DistinctAnswers)
                        {
                            if (GUILayout.Button($"Settle as {answer}"))
                            {
                                Settle(split, answer.Action, new ExpectedTarget(answer.Target));
                            }
                        }

                        // Both right, same action and different targets: an open choice (findings.md).
                        var actions = split.DistinctAnswers.Select(a => a.Action).Distinct().ToList();
                        bool sameAction = actions.Count == 1 && split.DistinctAnswers.All(a => a.Target != TargetRegistry.NoTarget);
                        using (new EditorGUI.DisabledScope(!sameAction))
                        {
                            if (GUILayout.Button("Open choice"))
                            {
                                Settle(split, actions[0], new ExpectedTarget(split.DistinctAnswers.Select(a => a.Target).ToArray()));
                            }
                        }
                    }
                }
            }

            EditorGUILayout.LabelField(
                "If neither side convinces the other, the prompt is ambiguous: rewrite it as a new prompt in Write mode, as findings.md describes.",
                EditorStyles.wordWrappedMiniLabel);
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
            GUIUtility.ExitGUI();
        }
    }
}
