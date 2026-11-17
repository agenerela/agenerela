using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Agenerela.Editor.UI
{
    /// <summary>How serious a <see cref="ProblemBox"/>'s problem is.</summary>
    internal enum ProblemSeverity
    {
        /// <summary>Works, but probably not as meant.</summary>
        Warning,

        /// <summary>Breaks something until it is fixed.</summary>
        Error,
    }

    /// <summary>
    /// A problem shown where the developer is looking rather than only in the Console (#167,
    /// screen 1 ⑥): a message and, when the screen knows the fix, a button that applies it.
    /// </summary>
    /// <remarks>
    /// Its parts are named for tests and stylesheets: <c>icon</c>, <c>message</c> and, only while
    /// there is a fix, <c>fix</c>.
    /// </remarks>
    [UxmlElement]
    internal partial class ProblemBox : VisualElement
    {
        internal const string UssClassName = "ag-problem";
        internal const string IconName = "icon";
        internal const string MessageName = "message";
        internal const string FixName = "fix";

        private readonly Image icon;
        private readonly Label message;
        private ProblemSeverity severity;
        private Button fix;
        private Action onFix;

        public ProblemBox() : this("")
        {
        }

        internal ProblemBox(string message, ProblemSeverity severity = ProblemSeverity.Warning)
        {
            AgenerelaStyles.Apply(this);
            AddToClassList(UssClassName);

            icon = new Image { name = IconName, scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList(UssClassName + "__icon");
            Add(icon);

            this.message = new Label(message ?? "") { name = MessageName };
            this.message.AddToClassList(UssClassName + "__message");
            Add(this.message);

            this.severity = severity;
            RefreshSeverity();
        }

        /// <summary>What is wrong, in a sentence the developer can act on.</summary>
        [UxmlAttribute]
        internal string Message
        {
            get => message.text;
            set => message.text = value ?? "";
        }

        [UxmlAttribute]
        internal ProblemSeverity Severity
        {
            get => severity;
            set
            {
                severity = value;
                RefreshSeverity();
            }
        }

        /// <summary>True while the box has a fix button.</summary>
        internal bool HasFix => fix != null;

        /// <summary>
        /// Adds a fix button labelled <paramref name="label"/>, or relabels the one there, that
        /// calls <paramref name="apply"/>. A null <paramref name="apply"/> removes the button.
        /// </summary>
        internal void SetFix(string label, Action apply)
        {
            if (apply == null)
            {
                ClearFix();
                return;
            }

            onFix = apply;
            if (fix == null)
            {
                fix = new Button(() => onFix?.Invoke()) { name = FixName };
                fix.AddToClassList(UssClassName + "__fix");
                Add(fix);
            }

            fix.text = string.IsNullOrEmpty(label) ? "Fix" : label;
        }

        /// <summary>Removes the fix button, for a problem the screen cannot fix by itself.</summary>
        internal void ClearFix()
        {
            onFix = null;
            fix?.RemoveFromHierarchy();
            fix = null;
        }

        private void RefreshSeverity()
        {
            EnableInClassList(UssClassName + "--warning", severity == ProblemSeverity.Warning);
            EnableInClassList(UssClassName + "--error", severity == ProblemSeverity.Error);
            icon.image = EditorGUIUtility.IconContent(severity == ProblemSeverity.Error ? "console.erroricon.sml" : "console.warnicon.sml").image;
        }
    }
}
