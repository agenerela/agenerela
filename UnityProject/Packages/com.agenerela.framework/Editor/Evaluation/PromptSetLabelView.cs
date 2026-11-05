using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Evaluation;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// Label mode: one annotator answers the prompts they have not answered yet, one at a time,
    /// without seeing anyone's answer.
    /// </summary>
    /// <remarks>
    /// Blind by construction rather than by care. The view holds <see cref="BlindPrompt"/>s and
    /// <see cref="LabelChoices"/>, never the <see cref="PromptSet"/>, so no expected answer, note or
    /// other person's label is within its reach; <c>BlindLabellingTests</c> checks its fields. It
    /// never shows a prompt's category either, since <c>impossible_request</c> gives the answer
    /// away, and it asks in an order of its own for each annotator, so prompts of one category do
    /// not arrive together.
    /// Its one way back to the set is <c>record</c>, which files an answer and returns nothing of
    /// the prompt.
    /// </remarks>
    internal sealed class PromptSetLabelView
    {
        private readonly List<BlindPrompt> queue;
        private readonly LabelChoices choices;
        private readonly Func<BlindPrompt, LabelAnswer, bool> record;

        private int current;
        private int answeredNow;
        private string action;
        private string target;
        private Vector2 scroll;

        internal PromptSetLabelView(string annotator, IReadOnlyList<BlindPrompt> queue, LabelChoices choices, Func<BlindPrompt, LabelAnswer, bool> record)
        {
            Annotator = annotator;
            this.queue = Shuffled(annotator, queue);
            this.choices = choices;
            this.record = record;
        }

        internal string Annotator { get; }

        internal void Draw()
        {
            if (current >= queue.Count)
            {
                EditorGUILayout.HelpBox(
                    answeredNow > 0
                        ? $"Done: {answeredNow} answered. Nothing left for {Annotator} to label."
                        : $"Nothing left for {Annotator} to label: every prompt is one you wrote or have answered.",
                    MessageType.Info);
                return;
            }

            var prompt = queue[current];
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{queue.Count - current} left to label", EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(prompt.Label ?? "", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(prompt.Stimulus ?? "", Wrapped(EditorStyles.label, 14));
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Observations", EditorStyles.miniBoldLabel);
                if (prompt.Observations.Count == 0)
                {
                    EditorGUILayout.LabelField("none", EditorStyles.miniLabel);
                }

                foreach (string observation in prompt.Observations)
                {
                    EditorGUILayout.LabelField("• " + observation, Wrapped(EditorStyles.label, 0));
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("State", EditorStyles.miniBoldLabel);
                foreach (var property in prompt.PreconditionState.Properties())
                {
                    EditorGUILayout.LabelField($"{property.Name} = {property.Value.ToString(Newtonsoft.Json.Formatting.None)}");
                }
            }

            EditorGUILayout.Space();
            DrawAnswer(prompt);
            EditorGUILayout.EndScrollView();

            // #92: a "Try it" reply from a live model joins here once the Editor has a provider. It
            // stays hidden until this annotator's answer is saved, so it cannot steer the label.
        }

        private void DrawAnswer(BlindPrompt prompt)
        {
            var actions = choices.ActionsAvailableIn(prompt.PreconditionState).ToArray();
            if (action == null || Array.IndexOf(actions, action) < 0)
            {
                action = ActionRegistry.None;
            }

            action = actions[EditorGUILayout.Popup("Action", Array.IndexOf(actions, action), actions)];

            bool needsTarget = choices.NeedsTarget(action);
            var targets = needsTarget ? choices.Targets.ToArray() : new[] { TargetRegistry.NoTarget };
            if (targets.Length == 0)
            {
                EditorGUILayout.HelpBox($"{action} needs a target, and the set declares none.", MessageType.Warning);
                return;
            }

            if (target == null || Array.IndexOf(targets, target) < 0)
            {
                target = targets[0];
            }

            using (new EditorGUI.DisabledScope(!needsTarget))
            {
                target = targets[EditorGUILayout.Popup("Target", Array.IndexOf(targets, target), targets)];
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Skip", GUILayout.Width(80)))
                {
                    Next();
                }

                if (GUILayout.Button("Save answer", GUILayout.Width(110)))
                {
                    record(prompt, new LabelAnswer(action, target));
                    answeredNow++;
                    Next();
                }
            }

            EditorGUILayout.LabelField(
                $"Saved under labels/{Annotator}/ beside the set. An answer once saved is not changed.",
                EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>
        /// The queue in an order fixed for each annotator: the same on every reopen, so skipping
        /// stays predictable, and different from the set's, which groups prompts by category.
        /// </summary>
        internal static List<BlindPrompt> Shuffled(string annotator, IReadOnlyList<BlindPrompt> queue)
        {
            return queue.OrderBy(p => Fnv1a(annotator + "\n" + p.Id)).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();
        }

        // string.GetHashCode is randomised per process, so it would reorder the queue on every reload.
        private static uint Fnv1a(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash = (hash ^ c) * 16777619;
            }

            return hash;
        }

        private void Next()
        {
            current++;
            action = null;
            target = null;
            GUI.FocusControl(null);
        }

        private static GUIStyle Wrapped(GUIStyle style, int fontSize)
        {
            var wrapped = new GUIStyle(style) { wordWrap = true };
            if (fontSize > 0)
            {
                wrapped.fontSize = fontSize;
            }

            return wrapped;
        }
    }
}
