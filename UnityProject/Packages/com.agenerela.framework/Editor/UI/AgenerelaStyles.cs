using UnityEditor;
using UnityEngine.UIElements;

namespace Agenerela.Editor.UI
{
    /// <summary>
    /// Finds <c>Agenerela.uss</c>, the one stylesheet every Agenerela editor screen uses (#167),
    /// and attaches it.
    /// </summary>
    internal static class AgenerelaStyles
    {
        /// <summary>Where the stylesheet is, by the package's name, however the package is installed.</summary>
        internal const string SheetPath = "Packages/com.agenerela.framework/Editor/UI/Agenerela.uss";

        /// <summary>The class that carries the <c>--ag-*</c> variables to an element and its children.</summary>
        internal const string RootClass = "ag";

        /// <summary>
        /// Added beside <see cref="RootClass"/> in the light skin, for the few colours Unity's
        /// theme does not provide (the muted text).
        /// </summary>
        internal const string LightClass = "ag--light";

        private static StyleSheet sheet;

        /// <summary>The stylesheet, or null if it is missing from the package.</summary>
        internal static StyleSheet Sheet
        {
            get
            {
                if (sheet == null)
                {
                    sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath);
                }

                return sheet;
            }
        }

        /// <summary>
        /// Gives <paramref name="element"/> the stylesheet and the <c>ag</c> class, so it and its
        /// children can use the <c>--ag-*</c> variables. A screen calls it on its root; each kit
        /// element calls it on itself, so it looks right wherever it is put.
        /// </summary>
        internal static void Apply(VisualElement element)
        {
            element.AddToClassList(RootClass);
            element.EnableInClassList(LightClass, !EditorGUIUtility.isProSkin);
            var found = Sheet;
            if (found != null && !element.styleSheets.Contains(found))
            {
                element.styleSheets.Add(found);
            }
        }
    }
}
