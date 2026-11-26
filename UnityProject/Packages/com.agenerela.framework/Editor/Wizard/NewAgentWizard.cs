using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Agenerela.Providers;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agenerela.Editor
{
    /// <summary>
    /// Creates a new agent from the New Agent window's answers: a script with one method per action,
    /// a profile, and optionally an Action asset per action. Also sets up a default AI model if the
    /// project has none, and can add the agent to a GameObject.
    /// </summary>
    internal static class NewAgentWizard
    {
        /// <summary>Whether an action needs a target, can take one, or never takes one.</summary>
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
            /// An example of what a player might say to trigger this action, with <c>{0}</c> for the target.
            /// Leave it empty to write the <c>[Example]</c> line as a commented-out TODO, so no made-up example
            /// is sent to the AI.
            /// </summary>
            public string Example;
        }

        internal sealed class Answers
        {
            public string Name;
            public string Role;

            /// <summary>The folder the files are written to: <c>Assets</c> or a folder inside it.</summary>
            public string Folder;

            public List<ActionChoice> Actions = new List<ActionChoice>();
            public bool AsAssets;

            /// <summary>
            /// When true, adds the script and Agent Behaviour to <see cref="AddTo"/>, or to a new GameObject
            /// when it is null.
            /// </summary>
            public bool AddToScene;

            public GameObject AddTo;
        }

        /// <summary>
        /// The folder the default AI model asset is written to. It sits next to the settings' Resources
        /// folder rather than inside it, because everything in Resources is included in builds.
        /// </summary>
        internal const string ProviderFolder = "Assets/Agenerela";

        /// <summary>
        /// The four ready-made actions shown in the window. Their examples must not match any evaluation
        /// prompt.
        /// </summary>
        internal static readonly ActionChoice[] Starters =
        {
            new ActionChoice { Id = "move_to", Description = "Walk to a named place.", Target = TargetKind.Needs, Example = "Wait by the {0}." },
            new ActionChoice { Id = "follow_player", Description = "Walk with whoever asked.", Target = TargetKind.Never, Example = "Stay close, I'm heading out." },
            new ActionChoice { Id = "speak_only", Description = "Answer without moving.", Target = TargetKind.Never, Example = "What's the news today?" },
            new ActionChoice { Id = "attack_target", Description = "Strike a named thing.", Target = TargetKind.Needs, Example = "Drive off the {0}!" },
        };

        /// <summary>
        /// Checks the answers, then writes the script, the profile, any Action assets and, if needed, the
        /// default AI model. Returns the script's path. Adding the agent to a GameObject happens later, in
        /// <see cref="FinishAfterReload"/>, because the script has to compile first.
        /// </summary>
        internal static string Create(Answers answers)
        {
            Check(answers);
            string scriptPath = Paths(answers)[0];

            AgenerelaSettingsPage.CreateFolders(answers.Folder.TrimEnd('/'));
            File.WriteAllText(scriptPath, Script(answers));
            AssetDatabase.ImportAsset(scriptPath);

            AgentProfile profile = WriteAssets(answers);
            EnsureDefaultProvider(ProviderFolder, AgenerelaSettingsPage.AssetPath);

            if (answers.AddToScene)
            {
                NewAgentPending pending = NewAgentPending.instance;
                pending.ScriptPath = scriptPath;
                pending.Profile = profile;
                pending.Target = answers.AddTo != null ? answers.AddTo : NewGameObject(answers.Name.Trim());
            }

            return scriptPath;
        }

        /// <summary>
        /// Runs after Unity recompiles. If <see cref="Create"/> left a GameObject waiting, adds the new
        /// script and Agent Behaviour to it and sets the profile.
        /// </summary>
        [DidReloadScripts]
        private static void FinishAfterReload()
        {
            NewAgentPending pending = NewAgentPending.instance;
            if (string.IsNullOrEmpty(pending.ScriptPath))
            {
                return;
            }

            string scriptPath = pending.ScriptPath;
            AgentProfile profile = pending.Profile;
            GameObject target = pending.Target;

            // Cleared before anything can fail, so an error is shown once, not after every recompile.
            pending.Clear();

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script == null || script.GetClass() == null)
            {
                throw new InvalidOperationException($"{scriptPath} did not compile into a class, so the agent was not added to a GameObject. " +
                                                    "Fix the errors above, then add AgentBehaviour and the script by hand.");
            }

            if (target == null)
            {
                throw new InvalidOperationException($"The GameObject for {script.name} is gone. Add AgentBehaviour and {script.name} to one by hand.");
            }

            Undo.AddComponent(target, script.GetClass());
            AgentBehaviour behaviour = Undo.AddComponent<AgentBehaviour>(target);
            behaviour.Profile = profile;
            EditorSceneManager.MarkSceneDirty(target.scene);
            Selection.activeGameObject = target;
        }

        /// <summary>
        /// Writes the profile and, if <see cref="Answers.AsAssets"/> is set, one Action asset per action,
        /// listed on the profile. Returns the profile. Separate from <see cref="Create"/> so tests can call
        /// it without writing a script.
        /// </summary>
        internal static AgentProfile WriteAssets(Answers answers)
        {
            List<string> paths = Paths(answers);
            string folder = answers.Folder.TrimEnd('/');
            AgenerelaSettingsPage.CreateFolders(folder);

            AgentProfile profile = ScriptableObject.CreateInstance<AgentProfile>();
            profile.Identity.Name = answers.Name.Trim();
            profile.Identity.Role = answers.Role?.Trim() ?? "";

            if (answers.AsAssets)
            {
                AgenerelaSettingsPage.CreateFolders(folder + "/Actions");
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
            return profile;
        }

        /// <summary>
        /// If the project has no AI model set, makes the <c>OllamaLocal</c> asset in
        /// <paramref name="folder"/> the project default, creating it if it is not there.
        /// <paramref name="settingsAt"/> is where the settings are created if the project has none.
        /// </summary>
        /// <returns>The asset made the default, or null if a model was already set.</returns>
        internal static OllamaProviderConfig EnsureDefaultProvider(string folder, string settingsAt)
        {
            if (AgenerelaSettings.ActiveProvider != null)
            {
                return null;
            }

            OllamaProviderConfig provider = AssetDatabase.LoadAssetAtPath<OllamaProviderConfig>(ProviderPath(folder));
            if (provider == null)
            {
                provider = ScriptableObject.CreateInstance<OllamaProviderConfig>();
                AgenerelaSettingsPage.CreateFolders(folder);
                AssetDatabase.CreateAsset(provider, ProviderPath(folder));
            }

            AgenerelaSettingsPage.SetDefault(provider, settingsAt);
            return provider;
        }

        /// <summary>Throws with a message if the answers are invalid or a file the wizard would write already exists.</summary>
        internal static void Check(Answers answers)
        {
            string folder = answers.Folder?.TrimEnd('/');
            if (folder != "Assets" && (folder == null || !folder.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                throw new ArgumentException($"The folder '{answers.Folder}' is not inside Assets.");
            }

            if (folder.Contains(".."))
            {
                throw new ArgumentException($"The folder '{answers.Folder}' uses '..'. Give a path inside Assets without it.");
            }

            if (answers.Actions.Count == 0)
            {
                throw new ArgumentException("Pick at least one action.");
            }

            // Every class and method name in the script must differ, or the script does not compile.
            string className = ClassName(answers.Name);
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal) { className };
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ActionChoice action in answers.Actions)
            {
                if (!ActionDefinition.ValidateId(action.Id, out string problem))
                {
                    throw new ArgumentException($"The action '{action.Id}': {problem}.");
                }

                // The id becomes a method name, so it must start with a letter and hold nothing Pascal drops.
                if (!Regex.IsMatch(action.Id, "^[a-z][a-z0-9_]*$"))
                {
                    throw new ArgumentException($"The action '{action.Id}' must start with a letter and use only a-z, 0-9 and _.");
                }

                if (action.Id == ActionRegistry.None)
                {
                    throw new ArgumentException($"'{ActionRegistry.None}' is always added for you, so it cannot be one of your actions.");
                }

                if (!seen.Add(action.Id))
                {
                    throw new ArgumentException($"The action '{action.Id}' is listed twice.");
                }

                string method = Pascal(action.Id);
                if (!names.Add(method) || !names.Add("Can" + method))
                {
                    throw new ArgumentException($"The action '{action.Id}' writes the method {method} and Can{method}, " +
                                                "but the class or another action already uses one of those names. Rename the action.");
                }

                if (string.IsNullOrWhiteSpace(action.Description))
                {
                    throw new ArgumentException($"The action '{action.Id}' has no description.");
                }
            }

            if (answers.AddToScene && answers.AddTo != null && answers.AddTo.GetComponent<AgentBehaviour>() != null)
            {
                throw new InvalidOperationException($"{answers.AddTo.name} already has an agent. Select another GameObject, or none for a new one.");
            }

            foreach (string path in Paths(answers))
            {
                if (File.Exists(path))
                {
                    throw new InvalidOperationException($"{path} already exists. The wizard never overwrites a file: move it, or pick another name or folder.");
                }
            }

            // The script's class has no namespace, so a class of the same name anywhere breaks compiling.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType(className, false) != null)
                {
                    throw new InvalidOperationException($"A class called {className} already exists, in {WhereIs(className, assembly)}. Pick another agent name.");
                }
            }
        }

        /// <summary>The paths of every file <see cref="Create"/> writes: the script first, then the profile, any Action assets and any default AI model.</summary>
        internal static List<string> Paths(Answers answers)
        {
            string folder = answers.Folder.TrimEnd('/');
            string className = ClassName(answers.Name);
            List<string> paths = new List<string> { $"{folder}/{className}.cs", $"{folder}/{className}Profile.asset" };
            if (answers.AsAssets)
            {
                paths.AddRange(answers.Actions.Select(a => $"{folder}/Actions/{a.Id}.asset"));
            }

            // Only a project with no AI model gets one, and an OllamaLocal already there is reused,
            // so only then is its path listed.
            if (AgenerelaSettings.ActiveProvider == null &&
                AssetDatabase.LoadAssetAtPath<OllamaProviderConfig>(ProviderPath(ProviderFolder)) == null)
            {
                paths.Add(ProviderPath(ProviderFolder));
            }

            return paths;
        }

        /// <summary>
        /// Returns the text of the agent's script. Lines always end in <c>\n</c>, so it matches the test
        /// fixture on every machine.
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

        /// <summary>Turns the agent's name into the script's class name: "Gate guard" becomes <c>GateGuard</c>.</summary>
        internal static string ClassName(string agentName)
        {
            if (Regex.IsMatch(agentName ?? "", "[^\\x00-\\x7F]"))
            {
                throw new ArgumentException($"The agent name '{agentName}' has a letter outside A to Z, which a class name cannot keep. Use only A to Z, 0 to 9 and spaces.");
            }

            string name = Pascal(agentName ?? "");
            if (name.Length == 0 || !char.IsLetter(name[0]))
            {
                throw new ArgumentException($"The agent name '{agentName}' does not make a C# class name: start it with a letter.");
            }

            return name;
        }

        // Creates an empty GameObject in the open scene, registered with Undo.
        private static GameObject NewGameObject(string name)
        {
            GameObject made = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(made, "New Agent");
            return made;
        }

        // Joins words into one capitalised name: move_to becomes MoveTo.
        private static string Pascal(string text)
        {
            return string.Concat(Regex.Split(text, "[^A-Za-z0-9]+")
                .Where(word => word.Length > 0)
                .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
        }

        private static string ProviderPath(string folder)
        {
            return folder + "/OllamaLocal.asset";
        }

        // The script file of a class, or the assembly it is in when no script file is found.
        private static string WhereIs(string className, Assembly assembly)
        {
            string script = AssetDatabase.FindAssets($"{className} t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => Path.GetFileNameWithoutExtension(path) == className);
            return script ?? assembly.GetName().Name;
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

        // Returns text as a C# string literal: quotes and backslashes escaped, line breaks made spaces.
        private static string Quote(string text)
        {
            string oneLine = Regex.Replace(text ?? "", @"\s*\r?\n\s*", " ");
            return "\"" + oneLine.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
