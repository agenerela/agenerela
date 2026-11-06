using System;
using Agenerela.Editor.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Agenerela.Tests.UI
{
    /// <summary>
    /// <b>Window → Agenerela → Tests → UI Kit Gallery</b>: every element of the editor UI kit on
    /// one page (#167), for the hand check in both editor skins and its screenshots. It lives in
    /// the test assembly, so it never ships in the package.
    /// </summary>
    /// <remarks>
    /// The first title's "dark skin" or "light skin" is written when the page is built, so reopen
    /// the page after switching skins to check the other one. Add a piece here when the kit gains
    /// one.
    /// </remarks>
    internal sealed class UiKitGallery : EditorWindow
    {
        [MenuItem("Window/Agenerela/Tests/UI Kit Gallery")]
        private static void Open()
        {
            var window = GetWindow<UiKitGallery>();
            window.titleContent = new GUIContent("UI Kit Gallery");
            window.minSize = new Vector2(480, 520);
        }

        private void CreateGUI()
        {
            var root = new ScrollView();
            AgenerelaStyles.Apply(root);
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 4;
            rootVisualElement.Add(root);

            root.Add(new SectionTitle($"Chips · {(EditorGUIUtility.isProSkin ? "dark" : "light")} skin"));
            var chips = Row(root);
            chips.Add(new Chip("follow_player"));
            chips.Add(new Chip("move_to"));
            chips.Add(new Chip("none"));
            chips.Add(new Chip("attack_target", offered: false));
            chips.Add(new Chip("isFollowing = false"));

            root.Add(new SectionTitle("Outcomes"));
            var outcomes = Row(root);
            foreach (Outcome outcome in Enum.GetValues(typeof(Outcome)))
            {
                outcomes.Add(new OutcomeIcon(outcome));
                var name = new Label(OutcomeIcon.DisplayName(outcome));
                name.style.marginRight = 12;
                outcomes.Add(name);
            }

            root.Add(new SectionTitle("Problems"));
            var duplicate = new ProblemBox("Two actions are called move_to. The model can only tell them apart by id.");
            duplicate.SetFix("Rename the second", () => Debug.Log("UI Kit Gallery: fix clicked"));
            root.Add(duplicate);
            root.Add(new ProblemBox("The profile has no actions, so nothing can be offered.", ProblemSeverity.Error));

            root.Add(new SectionTitle("Buttons"));
            var buttons = Row(root);
            buttons.Add(new Button { text = "Skip" });
            buttons.Add(AgenerelaButtons.Primary("Save answer", () => Debug.Log("UI Kit Gallery: primary clicked")));
            buttons.Add(AgenerelaButtons.Dashed("Open choice", () => Debug.Log("UI Kit Gallery: dashed clicked")));
            buttons.Add(new Button { text = "Disabled primary" });
            AgenerelaButtons.MakePrimary((Button)buttons[buttons.childCount - 1]).SetEnabled(false);

            root.Add(new SectionTitle("Card and type"));
            var card = new VisualElement();
            card.AddToClassList("ag-card");
            card.Add(new SectionTitle("Player"));
            card.Add(new Label("Go to the bridge."));
            card.Add(new SectionTitle("State"));
            var state = new Label("isFollowing = false");
            state.AddToClassList("ag-mono");
            card.Add(state);
            var muted = new Label("A muted hint: saved to labels/Yevhen/command_with_target.json.");
            muted.AddToClassList("ag-muted");
            muted.style.whiteSpace = WhiteSpace.Normal;
            muted.style.marginTop = 8;
            card.Add(muted);
            root.Add(card);
        }

        private static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.AddToClassList("ag-chips");
            row.style.marginBottom = 4;
            parent.Add(row);
            return row;
        }
    }
}
