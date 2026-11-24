using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Editor.UI;
using Agenerela.Evaluation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// Label mode: one annotator answers the prompts they have not answered yet, one at a time,
    /// without seeing anyone's answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Blind by construction rather than by care. The view holds <see cref="BlindPrompt"/>s and
    /// <see cref="LabelChoices"/>, never the <see cref="PromptSet"/>, so no expected answer, note or
    /// other person's label is within its reach; <c>BlindLabellingTests</c> checks its fields, and
    /// <c>PromptSetWindowTests</c> reads every word its tree shows. It never shows a prompt's
    /// category either, since <c>impossible_request</c> gives the answer away, and it asks in an
    /// order of its own for each annotator, so prompts of one category do not arrive together.
    /// Its one way back to the set is <c>record</c>, which files an answer and returns nothing of
    /// the prompt.
    /// </para>
    /// <para>
    /// Keys, while the view has focus: <b>1</b> to <b>9</b> pick an action, <b>T</b> the next
    /// target, <b>Enter</b> saves the answer and <b>S</b> skips.
    /// </para>
    /// </remarks>
    internal sealed class PromptSetLabelView
    {
        internal const string LayoutPath = PromptSetUi.Folder + "PromptSetLabelView.uxml";

        // Names a test reaches. The rest are in the UXML.
        internal const string ProgressName = "label-progress", DoneName = "label-done", PromptName = "label-prompt", ActionName = "label-action",
            TargetName = "label-target", SkipName = "skip", SaveName = "save-answer";

        private readonly List<BlindPrompt> queue;
        private readonly LabelChoices choices;
        private readonly Func<BlindPrompt, LabelAnswer, bool> record;
        private readonly int total;

        private int current;
        private int answeredNow;
        private string action;
        private string target;

        private VisualElement root;
        private DropdownField actionField, targetField;

        /// <param name="total">How many prompts <paramref name="annotator"/> is asked to label in all, <see cref="LabelProgress.TotalFor"/>: a count, not the set.</param>
        internal PromptSetLabelView(string annotator, IReadOnlyList<BlindPrompt> queue, LabelChoices choices, Func<BlindPrompt, LabelAnswer, bool> record, int total)
        {
            Annotator = annotator;
            this.queue = Shuffled(annotator, queue);
            this.choices = choices;
            this.record = record;
            this.total = total;
        }

        internal string Annotator { get; }

        /// <summary>Answered ones are no longer left; skipped ones still are.</summary>
        internal LabelProgress Progress => new LabelProgress(total, queue.Count - answeredNow, current - answeredNow);

        /// <summary>The view's tree, made the first time it is asked for.</summary>
        internal VisualElement Root => root ?? Build();

        /// <summary>The action picked for the prompt shown.</summary>
        internal string Action => action;

        /// <summary>The target picked for the prompt shown.</summary>
        internal string Target => target;

        private VisualElement Build()
        {
            root = PromptSetUi.Load(LayoutPath);
            root.focusable = true;

            actionField = root.Q<DropdownField>(ActionName);
            actionField.tooltip = "The actions the prompt's state allows, and none. Keys 1 to 9 pick one.";
            actionField.RegisterValueChangedCallback(e =>
            {
                action = e.newValue;
                ShowTargets();
            });

            targetField = root.Q<DropdownField>(TargetName);
            targetField.tooltip = "T picks the next one.";
            targetField.RegisterValueChangedCallback(e => target = e.newValue);

            root.Q<Button>(SkipName).clicked += Skip;
            var saveButton = root.Q<Button>(SaveName);
            saveButton.clicked += Save;
            AgenerelaButtons.MakePrimary(saveButton);

            // Trickles down, so the keys work whichever control has focus; a dropdown's own menu
            // still opens with a click.
            root.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            root.RegisterCallback<AttachToPanelEvent>(_ => root.schedule.Execute(() => root.Focus()));

            Show();
            return root;
        }

        /// <summary>Picks the <paramref name="n"/>th action offered, from 1, as key <paramref name="n"/> does.</summary>
        internal bool PickAction(int n)
        {
            var actions = Actions();
            if (current >= queue.Count || n < 1 || n > actions.Count)
            {
                return false;
            }

            action = actions[n - 1];
            ShowAnswer();
            return true;
        }

        /// <summary>Moves to the next target offered, as key T does.</summary>
        internal void NextTarget()
        {
            Normalize();
            var targets = choices.TargetsFor(action);
            if (current >= queue.Count || targets.Count < 2)
            {
                return;
            }

            int at = Math.Max(0, IndexOf(targets, target));
            target = targets[(at + 1) % targets.Count];
            ShowAnswer();
        }

        /// <summary>Files the answer shown and moves on, as <b>Save answer</b> and Enter do.</summary>
        internal void Save()
        {
            if (current >= queue.Count)
            {
                return;
            }

            Normalize();
            if (choices.TargetsFor(action).Count == 0)
            {
                return;
            }

            record(queue[current], new LabelAnswer(action, target));
            answeredNow++;
            Next();
        }

        /// <summary>Leaves the prompt shown for later, as <b>Skip</b> and S do.</summary>
        internal void Skip()
        {
            if (current < queue.Count)
            {
                Next();
            }
        }

        private void OnKey(KeyDownEvent e)
        {
            if (e.ctrlKey || e.commandKey || e.altKey)
            {
                return;
            }

            bool handled = true;
            if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9)
            {
                PickAction(e.keyCode - KeyCode.Alpha0);
            }
            else if (e.keyCode >= KeyCode.Keypad1 && e.keyCode <= KeyCode.Keypad9)
            {
                PickAction(e.keyCode - KeyCode.Keypad0);
            }
            else if (e.keyCode == KeyCode.T)
            {
                NextTarget();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                Save();
            }
            else if (e.keyCode == KeyCode.S)
            {
                Skip();
            }
            else
            {
                handled = false;
            }

            if (handled)
            {
                e.StopPropagation();
            }
        }

        private void Show()
        {
            if (root == null)
            {
                return;
            }

            var progress = Progress;
            var bar = root.Q<ProgressBar>(ProgressName);
            bar.title = progress.Heading;
            bar.highValue = Math.Max(1, progress.Total);
            bar.value = progress.Total - progress.Left;

            bool done = current >= queue.Count;
            var doneLabel = root.Q<Label>(DoneName);
            doneLabel.text = done ? progress.NothingLeft(Annotator) : "";
            PromptSetUi.Display(doneLabel, done);
            PromptSetUi.Display(root.Q(PromptName), !done);
            if (done)
            {
                return;
            }

            var prompt = queue[current];
            root.Q<Label>("label-speaker").text = prompt.Label ?? "";
            root.Q<Label>("label-stimulus").text = prompt.Stimulus ?? "";

            var observed = root.Q("label-observations");
            observed.Clear();
            if (prompt.Observations.Count == 0)
            {
                observed.Add(Line("none", true));
            }

            foreach (string observation in prompt.Observations)
            {
                observed.Add(Line("• " + observation, false));
            }

            var state = root.Q("label-state");
            state.Clear();
            var properties = prompt.PreconditionState.Properties().ToList();
            if (properties.Count == 0)
            {
                state.Add(Line("none", true));
            }

            foreach (var property in properties)
            {
                var line = Line($"{property.Name} = {property.Value.ToString(Newtonsoft.Json.Formatting.None)}", false);
                line.AddToClassList("ag-mono");
                state.Add(line);
            }

            root.Q<Label>("label-saved-note").text = $"Saved under labels/{Annotator}/ beside the set. An answer once saved is not changed.";
            ShowAnswer();
        }

        // The answer stays one the prompt offers: an action its state allows, else none, and a
        // target that action takes, else its first.
        private void Normalize()
        {
            var actions = Actions();
            if (action == null || !actions.Contains(action))
            {
                action = ActionRegistry.None;
            }

            var targets = choices.TargetsFor(action);
            if (targets.Count > 0 && (target == null || IndexOf(targets, target) < 0))
            {
                target = targets[0];
            }
        }

        private void ShowAnswer()
        {
            Normalize();
            if (root == null)
            {
                return;
            }

            actionField.choices = Actions();
            actionField.SetValueWithoutNotify(action);
            ShowTargets();
        }

        private void ShowTargets()
        {
            Normalize();
            if (root == null)
            {
                return;
            }

            var targets = choices.TargetsFor(action).ToList();
            var problem = root.Q("label-no-target");
            problem.Clear();
            bool none = targets.Count == 0;
            if (none)
            {
                problem.Add(new ProblemBox($"{action} needs a target, and the set declares none."));
            }

            PromptSetUi.Display(targetField, !none);
            root.Q<Button>(SaveName).SetEnabled(!none);
            if (none)
            {
                return;
            }

            targetField.choices = targets;
            targetField.SetValueWithoutNotify(target);
            targetField.SetEnabled(targets.Count > 1);
        }

        private List<string> Actions() => current < queue.Count
            ? choices.ActionsAvailableIn(queue[current].PreconditionState).ToList()
            : new List<string>();

        private static int IndexOf(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value) return i;
            }

            return -1;
        }

        private static Label Line(string text, bool muted)
        {
            var line = new Label(text);
            line.AddToClassList("ps-label__line");
            if (muted)
            {
                line.AddToClassList("ag-muted");
            }

            return line;
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
            Show();
            root?.Focus();
        }
    }
}
