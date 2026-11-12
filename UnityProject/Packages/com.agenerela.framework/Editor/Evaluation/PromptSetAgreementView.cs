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

            string headline = EvaluationReportView.AgreementHeadline(report);
            EditorGUILayout.LabelField(headline, new GUIStyle(EditorStyles.boldLabel) { wordWrap = true, fontSize = 14 });
            EditorGUILayout.LabelField(EvaluationReportView.PairwiseAgreement(report), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                "An unsettled prompt counts its expected answer as its one writer's. Settling a split writes everyone's answer into the prompt's labels.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space();
            DrawCategories();
            DrawAgreedAnswers();
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

                    if (prompt.Labels != null)
                    {
                        // Settled already: the split still counts against agreement, but the
                        // decision is made. Changing it is an edit in Write mode, never a click here.
                        EditorGUILayout.LabelField("    Settled", "The answers above are kept in the prompt's labels.");
                        continue;
                    }

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

        private void DrawAgreedAnswers()
        {
            int count = UnrecordedAgreements(set, report).Count();
            if (count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button($"Record agreed answers ({count})") && EditorUtility.DisplayDialog(
                    "Record agreed answers?",
                    $"Keep each person's own answer in labels for these {count} unanimous prompts. Expected answers stay as they are; splits still need discussion.",
                    "Record", "Cancel"))
            {
                RecordAgreedAnswers(set, report);
                afterSettle();
                GUIUtility.ExitGUI();
            }
        }

        // Unanimous prompts need both original answers in labels too (#148), without making a
        // person settle a disagreement that never existed or replacing the expected answer.
        internal static int RecordAgreedAnswers(PromptSet set, AgreementReport report)
        {
            var agreed = UnrecordedAgreements(set, report).ToList();
            foreach (var agreement in agreed)
            {
                set.TryFind(agreement.PromptId, out var prompt, out _);
                set.Settle(agreement, prompt.ExpectedAction, prompt.ExpectedTarget);
            }

            return agreed.Count;
        }

        private static IEnumerable<PromptAgreement> UnrecordedAgreements(PromptSet set, AgreementReport report)
        {
            return report.Prompts.Where(p => p.Answered >= 2 && !p.IsSplit
                && set.TryFind(p.PromptId, out var prompt, out _)
                && p.Answers.Keys.Any(name => prompt.Labels == null || !prompt.Labels.ContainsKey(name)));
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
