using System;
using UnityEngine.UIElements;

namespace Agenerela.Editor.UI
{
    /// <summary>
    /// The two button styles the wireframes add to Unity's own (#167). <b>Primary</b>, filled, is
    /// the one action a screen is for: <b>Save answer</b>, <b>Save · 1 file changed</b>. <b>Dashed</b>
    /// is a choice that is open but not suggested: <b>Open choice</b>. Every other button is
    /// Unity's plain one.
    /// </summary>
    /// <remarks>
    /// Each works on a button built in code or loaded from UXML. A primary button can also be
    /// styled from UXML alone with the <c>ag-button--primary</c> class; a dashed one needs
    /// <see cref="MakeDashed"/>, which attaches the outline.
    /// </remarks>
    internal static class AgenerelaButtons
    {
        internal const string PrimaryClass = "ag-button--primary";
        internal const string DashedClass = "ag-button--dashed";

        /// <summary>A new primary button.</summary>
        internal static Button Primary(string text, Action clicked) => MakePrimary(new Button(clicked) { text = text });

        /// <summary>A new dashed button.</summary>
        internal static Button Dashed(string text, Action clicked) => MakeDashed(new Button(clicked) { text = text });

        /// <summary>Styles <paramref name="button"/> as the screen's primary action.</summary>
        internal static Button MakePrimary(Button button)
        {
            AgenerelaStyles.Apply(button);
            button.AddToClassList(PrimaryClass);
            return button;
        }

        /// <summary>Styles <paramref name="button"/> as an open choice, with a dashed outline.</summary>
        internal static Button MakeDashed(Button button)
        {
            AgenerelaStyles.Apply(button);
            button.AddToClassList(DashedClass);
            DashedOutline.Attach(button);
            DashedOutline.Show(button, true);
            return button;
        }
    }
}
