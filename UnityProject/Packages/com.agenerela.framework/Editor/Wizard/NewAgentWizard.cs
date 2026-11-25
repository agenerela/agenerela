using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor
{
    /// <summary>
    /// What Create → Agenerela → New Agent… writes (#175, figure 12 of workflow.html): a script with
    /// an <c>[AgentAction]</c> method for each action, and the profile. With
    /// <see cref="Answers.AsAssets"/>, also an action asset for each, listed on the profile; the
    /// script still runs them, because an asset wins on its id (DR-011). Every file is ordinary and
    /// editable afterwards.
    /// </summary>
    internal static class NewAgentWizard
    {
        /// <summary>The three target states of DR-020.</summary>
        internal enum TargetKind
        {
            Needs,
            Optional,
            Never,
        }

        internal sealed class ActionChoice
        {
            public string Id;
            public string Description;
            public TargetKind Target;

            /// <summary>
            /// Something a player might say that leads to this action, <c>{0}</c> for the target.
            /// Empty writes the <c>[Example]</c> line commented out with a TODO: an invented sentence
            /// would reach the model as a real example.
            /// </summary>
            public string Example;
        }

        internal sealed class Answers
        {
            public string Name;
            public string Role;

            /// <summary>Where the files go: <c>Assets</c> or a folder inside it.</summary>
            public string Folder;

            public List<ActionChoice> Actions = new List<ActionChoice>();
            public bool AsAssets;
        }

        /// <summary>
        /// Figure 12's starters. Their examples are not evaluation prompts (hard rule 4): grep
        /// <c>Assets/Evaluation</c> before changing one.
        /// </summary>
        internal static readonly ActionChoice[] Starters =
        {
            new ActionChoice { Id = "move_to", Description = "Walk to a named place.", Target = TargetKind.Needs, Example = "Keep watch over by the {0}." },
            new ActionChoice { Id = "follow_player", Description = "Walk with whoever asked.", Target = TargetKind.Never, Example = "Stay close, I'm heading out." },
            new ActionChoice { Id = "speak_only", Description = "Answer without moving.", Target = TargetKind.Never, Example = "What's the news today?" },
            new ActionChoice { Id = "attack_target", Description = "Strike a named thing.", Target = TargetKind.Needs, Example = "Drive off the {0}!" },
        };

        /// <summary>
        /// Writes every file in <see cref="Paths"/>. Returns the script's path: Unity compiles it
        /// after this returns, so adding it to a GameObject has to wait for that domain reload.
        /// </summary>
        internal static string Create(Answers answers)
        {
            Check(answers);
            List<string> paths = Paths(answers);
            string folder = answers.Folder.TrimEnd('/');

            Directory.CreateDirectory(answers.AsAssets ? folder + "/Actions" : folder);
            File.WriteAllText(paths[0], Script(answers));

            // Imports the new folders and the script, so the assets below have somewhere to go.
            AssetDatabase.Refresh();

            AgentProfile profile = ScriptableObject.CreateInstance<AgentProfile>();
            profile.Identity.Name = answers.Name.Trim();
            profile.Identity.Role = answers.Role?.Trim() ?? "";

            if (answers.AsAssets)
            {
                foreach (ActionChoice action in answers.Actions)
                {
                    ActionDefinitionAsset asset = ScriptableObject.CreateInstance<ActionDefinitionAsset>();
                    asset.Action.Id = action.Id;
                    asset.Action.Description = action.Description;
                    asset.Action.RequiresTarget = action.Target == TargetKind.Needs;
                    asset.Action.TakesNoTarget = action.Target == TargetKind.Never;
                    asset.Action.ExampleStimulus = string.IsNullOrWhiteSpace(action.Example) ? null : action.Example;
                    AssetDatabase.CreateAsset(asset, $"{folder}/Actions/{action.Id}.asset");
                    profile.Actions.Add(asset);
                }
            }

            AssetDatabase.CreateAsset(profile, paths[1]);
            AssetDatabase.SaveAssets();
            return paths[0];
        }

        /// <summary>Throws, saying what is wrong, before anything is written. An existing file is wrong: the wizard never overwrites.</summary>
        internal static void Check(Answers answers)
        {
            string folder = answers.Folder?.TrimEnd('/');
            if (folder != "Assets" && (folder == null || !folder.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                throw new ArgumentException($"The folder '{answers.Folder}' is not inside Assets.");
            }

            if (answers.Actions.Count == 0)
            {
                throw new ArgumentException("Pick at least one action.");
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ActionChoice action in answers.Actions)
            {
                if (!ActionDefinition.ValidateId(action.Id, out string problem))
                {
                    throw new ArgumentException($"The action '{action.Id}': {problem}.");
                }

                if (action.Id == ActionRegistry.None)
                {
                    throw new ArgumentException($"'{ActionRegistry.None}' is always added for you, so it cannot be one of your actions.");
                }

                if (!seen.Add(action.Id))
                {
                    throw new ArgumentException($"The action '{action.Id}' is listed twice.");
                }

                if (string.IsNullOrWhiteSpace(action.Description))
                {
                    throw new ArgumentException($"The action '{action.Id}' has no description.");
                }
            }

            foreach (string path in Paths(answers))
            {
                if (File.Exists(path))
                {
                    throw new InvalidOperationException($"{path} already exists. The wizard never overwrites a file: move it, or pick another name or folder.");
                }
            }
        }

        /// <summary>Every file <see cref="Create"/> writes: the script, the profile, then any action assets.</summary>
        internal static List<string> Paths(Answers answers)
        {
            string folder = answers.Folder.TrimEnd('/');
            string className = ClassName(answers.Name);
            List<string> paths = new List<string> { $"{folder}/{className}.cs", $"{folder}/{className}Profile.asset" };
            if (answers.AsAssets)
            {
                paths.AddRange(answers.Actions.Select(a => $"{folder}/Actions/{a.Id}.asset"));
            }

            return paths;
        }

        /// <summary>
        /// The script, as text. Lines end in <c>\n</c> on every machine, so the compiled fixture in
        /// the tests matches it whoever runs them.
        /// </summary>
        internal static string Script(Answers answers)
        {
            StringBuilder text = new StringBuilder();
            void Line(string line = "") => text.Append(line).Append('\n');

            Line("using Agenerela;");
            Line("using UnityEngine;");
            Line();
            Line("// Written by Create > Agenerela > New Agent. It is yours to edit: fill in each TODO.");
            Line($"public class {ClassName(answers.Name)} : MonoBehaviour");
            Line("{");

            for (int i = 0; i < answers.Actions.Count; i++)
            {
                ActionChoice action = answers.Actions[i];
                string method = Pascal(action.Id);
                if (i > 0)
                {
                    Line();
                }

                Line($"    [AgentAction({Quote(action.Id)}, {Quote(action.Description)}{TargetArgument(action.Target)})]");
                Line(string.IsNullOrWhiteSpace(action.Example)
                    ? "    // TODO: [Example(\"something a player might say that leads to this action\")]"
                    : $"    [Example({Quote(action.Example)})]");
                Line($"    void {method}(AgentDecision decision)");
                Line("    {");
                Line($"        // TODO: what {action.Id} does in your game.");
                Line($"        Debug.Log($\"{{name}}: {action.Id}, target {{decision.TargetId}}\", this);");
                Line("    }");
                Line();
                Line("    // TODO: return false while this action makes no sense, so the agent is not offered it.");
                Line($"    [Available({Quote(action.Id)})]");
                Line($"    bool Can{method}() => true;");
            }

            Line("}");
            return text.ToString();
        }

        /// <summary>"Gate guard" → <c>GateGuard</c>, the script's class and file name.</summary>
        internal static string ClassName(string agentName)
        {
            string name = Pascal(agentName ?? "");
            if (name.Length == 0 || !char.IsLetter(name[0]))
            {
                throw new ArgumentException($"The agent name '{agentName}' does not make a C# class name: start it with a letter.");
            }

            return name;
        }

        // move_to → MoveTo, "gate guard" → GateGuard: words split on anything but a letter or digit.
        private static string Pascal(string text)
        {
            return string.Concat(Regex.Split(text, "[^A-Za-z0-9]+")
                .Where(word => word.Length > 0)
                .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
        }

        private static string TargetArgument(TargetKind target)
        {
            switch (target)
            {
                case TargetKind.Needs: return ", RequiresTarget = true";
                case TargetKind.Never: return ", TakesNoTarget = true";
                default: return "";
            }
        }

        // A C# string literal. A line break becomes a space: descriptions and examples are one line.
        private static string Quote(string text)
        {
            string oneLine = Regex.Replace(text ?? "", @"\s*\r?\n\s*", " ");
            return "\"" + oneLine.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
