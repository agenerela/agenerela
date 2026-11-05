using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Agenerela.Tests.UI
{
    /// <summary>
    /// Builds Agenerela's UI Toolkit editor screens in EditMode tests and queries them by name
    /// (#167), so what IMGUI could only check by hand becomes a test. Every Phase 5 UI issue
    /// uses it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An element only gets a panel, and so only receives events and resolves its styles, inside
    /// a window that is shown. <see cref="Host"/>, <see cref="Inspector"/> and
    /// <see cref="Window{TWindow}"/> each open one; dispose the <see cref="UiTree"/> they return
    /// (<c>using var tree = ...</c>) to close it.
    /// </para>
    /// <para>
    /// Events are dispatched at once, but styles resolve on a later editor tick: a test that
    /// reads <c>resolvedStyle</c> is a <c>[UnityTest]</c> that yields <see cref="Until"/> first.
    /// Classes, text and children can be checked straight away, with no window at all.
    /// </para>
    /// <para>
    /// A <c>[UnityTest]</c> that fails while it yields is dropped without its <c>using</c> block
    /// ending, so its window stays open. Every test class that opens one calls
    /// <see cref="CloseAll"/> in its <c>[TearDown]</c>, which runs however the test ends.
    /// </para>
    /// </remarks>
    public static class EditorUiFixtures
    {
        /// <summary>The size a window opens at unless a test asks for another: the Prompt Set window's minimum.</summary>
        public static readonly Vector2 DefaultSize = new Vector2(640, 420);

        private static readonly List<EditorWindow> opened = new List<EditorWindow>();

        /// <summary>
        /// Closes every window these fixtures opened, including any a failed test left behind.
        /// Call it in <c>[TearDown]</c>.
        /// </summary>
        public static void CloseAll()
        {
            // Host windows are also found by type, so ones left from before a domain reload close too.
            foreach (var window in opened.Concat(Resources.FindObjectsOfTypeAll<HostWindow>()).Distinct().ToList())
            {
                if (window != null)
                {
                    window.Close();
                }
            }

            opened.Clear();
        }

        /// <summary>Puts <paramref name="element"/> in a shown window, so it receives events and resolves styles.</summary>
        public static UiTree Host(VisualElement element, Vector2? size = null)
        {
            var window = ScriptableObject.CreateInstance<HostWindow>();
            Show(window, size);
            window.rootVisualElement.Add(element);
            return new UiTree(element, window, null);
        }

        /// <summary>
        /// Builds the inspector Unity would show for <paramref name="target"/> and hosts it. Fails
        /// if that inspector draws with IMGUI, which has no tree to query.
        /// </summary>
        public static UiTree Inspector(UnityEngine.Object target, Vector2? size = null)
        {
            Assert.That(target, Is.Not.Null, "No object to build an inspector for.");
            var editor = UnityEditor.Editor.CreateEditor(target);
            var root = editor.CreateInspectorGUI();
            if (root == null)
            {
                UnityEngine.Object.DestroyImmediate(editor);
                Assert.Fail($"{target.GetType().Name}'s inspector draws with IMGUI: it returns no tree from CreateInspectorGUI.");
            }

            var window = ScriptableObject.CreateInstance<HostWindow>();
            Show(window, size);
            window.rootVisualElement.Add(root);
            return new UiTree(root, window, editor);
        }

        /// <summary>
        /// Opens a new <typeparamref name="TWindow"/> at <paramref name="size"/> (640 × 420 unless
        /// given) and returns its root once its <c>CreateGUI</c> has run.
        /// </summary>
        public static UiTree Window<TWindow>(Vector2? size = null) where TWindow : EditorWindow
        {
            var window = ScriptableObject.CreateInstance<TWindow>();
            Show(window, size);
            return new UiTree(window.rootVisualElement, window, null);
        }

        /// <summary>
        /// The descendant of <paramref name="root"/> named <paramref name="name"/>. Fails, listing
        /// the names that are there, if there is none of that type.
        /// </summary>
        public static T Find<T>(this VisualElement root, string name) where T : VisualElement
        {
            var found = root.Q<T>(name);
            if (found == null)
            {
                var named = root.Query<VisualElement>().Where(e => !string.IsNullOrEmpty(e.name))
                    .ToList().Select(e => $"{e.name} ({e.GetType().Name})");
                Assert.Fail($"No {typeof(T).Name} named \"{name}\". Named elements: {string.Join(", ", named)}.");
            }

            return found;
        }

        /// <summary>Clicks <paramref name="button"/> as Enter on a focused button does. It must be hosted.</summary>
        public static void Click(Button button)
        {
            Assert.That(button.panel, Is.Not.Null, "The button has no panel: build it with Host, Inspector or Window first.");
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        /// <summary>
        /// Yields editor ticks until <paramref name="condition"/> holds, failing after
        /// <paramref name="ticks"/>. Use it before reading <c>resolvedStyle</c>.
        /// </summary>
        public static IEnumerator Until(Func<bool> condition, string what, int ticks = 30)
        {
            for (int i = 0; i < ticks; i++)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.That(condition(), Is.True, $"Still not true after {ticks} editor ticks: {what}.");
        }

        private static void Show(EditorWindow window, Vector2? size)
        {
            var wanted = size ?? DefaultSize;
            opened.Add(window);
            window.ShowUtility();
            window.minSize = wanted;
            window.maxSize = wanted;
            window.position = new Rect(80, 80, wanted.x, wanted.y);
        }

        /// <summary>An empty window that only hosts what a test puts in it.</summary>
        internal sealed class HostWindow : EditorWindow
        {
        }
    }

    /// <summary>A built tree and the window holding it. Dispose it to close the window.</summary>
    public sealed class UiTree : IDisposable
    {
        private readonly UnityEditor.Editor editor;

        internal UiTree(VisualElement root, EditorWindow window, UnityEditor.Editor editor)
        {
            Root = root;
            Window = window;
            this.editor = editor;
        }

        /// <summary>The element built: the hosted element, the inspector's root or the window's root.</summary>
        public VisualElement Root { get; }

        /// <summary>The window holding <see cref="Root"/>.</summary>
        public EditorWindow Window { get; }

        /// <summary>The descendant of <see cref="Root"/> named <paramref name="name"/>; see <see cref="EditorUiFixtures.Find{T}"/>.</summary>
        public T Find<T>(string name) where T : VisualElement => Root.Find<T>(name);

        public void Dispose()
        {
            if (Window != null)
            {
                Window.Close();
            }

            if (editor != null)
            {
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }
    }
}
