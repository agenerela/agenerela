using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Agenerela.Editor.UI
{
    /// <summary>
    /// What happened to one decision, as the Decision Log shows it (screen 3 ③). Four outcomes
    /// that look identical in the game, "the agent did nothing", but need different fixes.
    /// </summary>
    /// <remarks>
    /// Not <see cref="DecisionOutcome"/>: that grades an answer against a label for the
    /// evaluation. This is only what the game saw.
    /// </remarks>
    internal enum Outcome
    {
        /// <summary>The chosen action ran.</summary>
        Executed,

        /// <summary>The model chose to do nothing.</summary>
        NoAction,

        /// <summary>A guard blocked the choice.</summary>
        Contained,

        /// <summary>The request failed, so nothing was decided.</summary>
        Error,
    }

    /// <summary>
    /// The icon for one <see cref="Outcome"/> (#167), from Unity's built-in icons only, with a
    /// tooltip saying what it means. The Decision Log, its filters and the Evaluation Report all
    /// draw the same four.
    /// </summary>
    [UxmlElement]
    internal partial class OutcomeIcon : Image
    {
        internal const string UssClassName = "ag-outcome";

        private Outcome outcome;

        public OutcomeIcon() : this(Outcome.Executed)
        {
        }

        internal OutcomeIcon(Outcome outcome)
        {
            AgenerelaStyles.Apply(this);
            AddToClassList(UssClassName);
            scaleMode = ScaleMode.ScaleToFit;
            this.outcome = outcome;
            Refresh();
        }

        /// <summary>The outcome shown. Setting it swaps the icon, tooltip and modifier class.</summary>
        [UxmlAttribute]
        internal Outcome Outcome
        {
            get => outcome;
            set
            {
                RemoveFromClassList(ModifierClass(outcome));
                outcome = value;
                Refresh();
            }
        }

        /// <summary>The modifier class for an outcome, such as <c>ag-outcome--no-action</c>.</summary>
        internal static string ModifierClass(Outcome outcome) => UssClassName + "--" + outcome switch
        {
            Outcome.Executed => "executed",
            Outcome.NoAction => "no-action",
            Outcome.Contained => "contained",
            Outcome.Error => "error",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

        /// <summary>The outcome's name as a screen labels it, such as a filter chip's "No action".</summary>
        internal static string DisplayName(Outcome outcome) => outcome switch
        {
            Outcome.Executed => "Executed",
            Outcome.NoAction => "No action",
            Outcome.Contained => "Contained",
            Outcome.Error => "Error",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

        /// <summary>What the outcome means, for the icon's tooltip.</summary>
        internal static string Describe(Outcome outcome) => outcome switch
        {
            Outcome.Executed => "Executed: the action ran.",
            Outcome.NoAction => "No action: the model chose to do nothing.",
            Outcome.Contained => "Contained: a guard blocked the choice.",
            Outcome.Error => "Error: the request failed, so nothing was decided.",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

        /// <summary>
        /// The built-in icon's name. These four match the wireframe's ✓ ○ ⚠ ✕ and the Test Runner
        /// and Console, so a developer reads them without a legend.
        /// </summary>
        internal static string IconName(Outcome outcome) => outcome switch
        {
            Outcome.Executed => "TestPassed",
            Outcome.NoAction => "TestIgnored",
            Outcome.Contained => "console.warnicon.sml",
            Outcome.Error => "console.erroricon.sml",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

        /// <summary>The icon itself; <c>IconContent</c> picks the dark skin's version when it has one.</summary>
        internal static Texture Icon(Outcome outcome) => EditorGUIUtility.IconContent(IconName(outcome)).image;

        private void Refresh()
        {
            AddToClassList(ModifierClass(outcome));
            image = Icon(outcome);
            tooltip = Describe(outcome);
        }
    }
}
