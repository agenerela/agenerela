using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Editor.UI;
using Agenerela.Providers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Agenerela.Editor
{
    /// <summary>
    /// Create → Agenerela → New Agent (#175, figure 12 of workflow.html): the form. It only collects
    /// the answers; <see cref="NewAgentWizard.Create"/> checks them and writes every file. Structure
    /// is in NewAgentWizard.uxml, layout in NewAgentWizard.uss.
    /// </summary>
    internal sealed class NewAgentWindow : EditorWindow
    {
        private const string LayoutPath = "Packages/com.agenerela.framework/Editor/Wizard/NewAgentWizard.uxml";
        private const string SheetPath = "Packages/com.agenerela.framework/Editor/Wizard/NewAgentWizard.uss";

        // Figure 12 ticks these starters to begin with; attack_target starts unticked.
        private static readonly string[] TickedAtFirst = { "move_to", "follow_player", "speak_only" };

        // The target dropdown's words, in NewAgentWizard.TargetKind's order.
        private static readonly List<string> TargetWords = new List<string> { "needs a target", "target optional", "never a target" };

        // The starters ticked now, and the developer's own actions in the order their rows show.
        private readonly HashSet<string> ticked = new HashSet<string>(TickedAtFirst);
        private readonly List<NewAgentWizard.ActionChoice> ownActions = new List<NewAgentWizard.ActionChoice>();

        [MenuItem("Assets/Create/Agenerela/New Agent", false, 0)]
        private static void Open()
        {
            NewAgentWindow window = GetWindow<NewAgentWindow>(true, "New Agent");
            window.minSize = new Vector2(460, 540);
        }

        private void CreateGUI()
        {
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath)
                                     ?? throw new InvalidOperationException($"The New Agent window's layout is missing: {LayoutPath}.");
            VisualElement tree = layout.Instantiate();
            tree.AddToClassList("na-root");
            AgenerelaStyles.Apply(tree);
            tree.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath));
            rootVisualElement.Add(tree);

            VisualElement starters = tree.Q("starters");
            foreach (NewAgentWizard.ActionChoice starter in NewAgentWizard.Starters)
            {
                Toggle toggle = new Toggle
                {
                    text = $"{starter.Id} — {starter.Description.TrimEnd('.')} ({TargetWords[(int)starter.Target]})",
                    tooltip = $"Something a player might say that leads to it: \"{starter.Example}\"",
                    value = ticked.Contains(starter.Id),
                };
                toggle.RegisterValueChangedCallback(change =>
                {
                    if (change.newValue)
                    {
                        ticked.Add(starter.Id);
                    }
                    else
                    {
                        ticked.Remove(starter.Id);
                    }
                });
                starters.Add(toggle);
            }

            tree.Q<Label>("model").text = ModelLine();

            Button addAction = AgenerelaButtons.MakeDashed(tree.Q<Button>("addAction"));
            addAction.clicked += () => AddRow(tree.Q("ownActions"));
            tree.Q<Button>("browse").clicked += () => Browse(tree.Q<TextField>("folder"));
            AgenerelaButtons.MakePrimary(tree.Q<Button>("create")).clicked += () => Create(tree);
        }

        // Hands the answers to the wizard. A problem it finds is shown in the form, and nothing has
        // been written: Check runs before the first file.
        private void Create(VisualElement tree)
        {
            NewAgentWizard.Answers answers = new NewAgentWizard.Answers
            {
                Name = tree.Q<TextField>("agentName").value,
                Role = tree.Q<TextField>("role").value,
                Folder = tree.Q<TextField>("folder").value.Trim(),
                AsAssets = tree.Q<Toggle>("asAssets").value,
            };
            answers.Actions.AddRange(NewAgentWizard.Starters.Where(starter => ticked.Contains(starter.Id)));
            answers.Actions.AddRange(ownActions);

            try
            {
                string scriptPath = NewAgentWizard.Create(answers);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath));
                Close();
            }
            catch (Exception problem) when (problem is ArgumentException || problem is InvalidOperationException)
            {
                HelpBox box = tree.Q<HelpBox>("problem");
                box.text = problem.Message;
                box.RemoveFromClassList("na-hidden");
            }
        }

        // One action of the developer's own: a card of fields that write into its ActionChoice.
        private void AddRow(VisualElement rows)
        {
            NewAgentWizard.ActionChoice action = new NewAgentWizard.ActionChoice { Target = NewAgentWizard.TargetKind.Needs };
            ownActions.Add(action);

            VisualElement card = new VisualElement();
            card.AddToClassList("ag-card");

            TextField id = new TextField("Action name") { tooltip = "The name the AI picks: lower case and underscores, such as open_gate." };
            TextField description = new TextField("What it does") { tooltip = "One line the AI reads to know when to pick it, such as: Open the village gate." };
            DropdownField target = new DropdownField("Acts on a target?", TargetWords, 0)
            {
                tooltip = "Needs one: always about something, as move_to is about a place. Optional: either. Never: about nothing, as speak_only is.",
            };
            TextField example = new TextField("Example line")
            {
                tooltip = "Optional. Something a player might say that leads to this action; {0} stands for the target. " +
                          "Never one of the evaluation prompts.",
            };
            Button remove = new Button { text = "Remove" };
            remove.AddToClassList("na-remove");

            id.RegisterValueChangedCallback(change => action.Id = change.newValue.Trim());
            description.RegisterValueChangedCallback(change => action.Description = change.newValue);
            target.RegisterValueChangedCallback(_ => action.Target = (NewAgentWizard.TargetKind)target.index);
            example.RegisterValueChangedCallback(change => action.Example = change.newValue);
            remove.clicked += () =>
            {
                ownActions.Remove(action);
                card.RemoveFromHierarchy();
            };

            card.Add(id);
            card.Add(description);
            card.Add(target);
            card.Add(example);
            card.Add(remove);
            rows.Add(card);
        }

        // What the agents will talk to, said before Create so that a new asset and a changed project
        // setting are no surprise. Read when the window opens, as NewAgentWizard.Create reads it.
        private static string ModelLine()
        {
            ProviderConfig active = AgenerelaSettings.ActiveProvider;
            if (active != null)
            {
                return $"Uses the project's AI model: {active.name}.";
            }

            OllamaSettings defaults = new OllamaSettings();
            return $"No AI model set up yet: this also adds OllamaLocal ({defaults.Model}) as the project default.";
        }

        // The folder picker gives a full path; the wizard wants one that starts at Assets.
        private static void Browse(TextField folder)
        {
            string chosen = EditorUtility.OpenFolderPanel("Folder for the new agent", folder.value, "");
            if (chosen.Length == 0)
            {
                return;
            }

            string assets = Application.dataPath;
            if (chosen == assets || chosen.StartsWith(assets + "/", StringComparison.Ordinal))
            {
                folder.value = "Assets" + chosen.Substring(assets.Length);
            }
            else
            {
                EditorUtility.DisplayDialog("New Agent", "Pick a folder inside this project's Assets folder.", "OK");
            }
        }
    }
}
