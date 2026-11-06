using System.IO;
using Agenerela.Providers;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor
{
    /// <summary>
    /// <b>Project Settings → Agenerela</b>: names the provider config every agent without one of its
    /// own uses, and the two an evaluation run compares, arm A and arm B (DR-013, #145). It holds
    /// nothing else.
    /// </summary>
    /// <remarks>
    /// The value is kept in <see cref="AgenerelaSettings"/>, which this page creates at
    /// <see cref="AssetPath"/> the first time a default is set, so a project that never opens the
    /// page gets no asset.
    /// </remarks>
    internal static class AgenerelaSettingsPage
    {
        internal const string SettingsPath = "Project/Agenerela";
        internal const string AssetPath = "Assets/Agenerela/Resources/" + AgenerelaSettings.ResourceName + ".asset";

        private static readonly GUIContent DefaultLabel = new GUIContent("Default Provider",
            "The provider config every agent without one of its own uses.");

        private static readonly GUIContent ArmALabel = new GUIContent("Arm A",
            "The provider config an evaluation run asks first, such as OllamaLocal. Agents never read it.");

        private static readonly GUIContent ArmBLabel = new GUIContent("Arm B",
            "The provider config an evaluation run compares against arm A, such as OllamaLocal-noGuards " +
            "or a second model. Agents never read it.");

        /// <summary>Which of the settings' provider configs a change sets.</summary>
        internal enum Slot
        {
            Default,
            ArmA,
            ArmB,
        }

        [SettingsProvider]
        private static SettingsProvider Create()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Agenerela",
                guiHandler = _ => Draw(),
                keywords = new[] { "Agenerela", "Provider", "Default Provider", "Model", "Ollama", "Evaluation", "Arm A", "Arm B" },
            };
        }

        private static void Draw()
        {
            var settings = Find();
            var current = settings != null ? settings.DefaultProvider : null;

            bool readOnly = settings != null && IsReadOnly(settings);

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            ProviderConfig chosen;
            using (new EditorGUI.DisabledScope(readOnly))
            {
                chosen = (ProviderConfig)EditorGUILayout.ObjectField(DefaultLabel, current, typeof(ProviderConfig), false);
            }

            if (EditorGUI.EndChangeCheck() && !readOnly)
            {
                settings = SetDefault(chosen, AssetPath);
            }

            if (readOnly)
            {
                EditorGUILayout.HelpBox(
                    "These settings are in a read-only package, so they cannot be changed here. Move " +
                    AgenerelaSettings.ResourceName + ".asset into a Resources folder under Assets, and " +
                    "remove the package's copy: a project keeps exactly one.",
                    MessageType.Warning);
            }

            if (chosen == null)
            {
                EditorGUILayout.HelpBox(
                    "No default: an agent without a provider config of its own cannot decide. Create one with " +
                    "Create → Agenerela → Ollama Provider, and choose it here.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Evaluation", EditorStyles.boldLabel);
            settings = ArmField(settings, ArmALabel, Slot.ArmA, readOnly);
            settings = ArmField(settings, ArmBLabel, Slot.ArmB, readOnly);
            EditorGUILayout.HelpBox(
                "An A/B run asks every prompt of a set in arm A and in arm B, in one session: two configs, " +
                "or one config twice with different options, such as the grounding guard off. Arms on two " +
                "models must run one after the other (hard rule 3).",
                MessageType.None);

            if (settings != null)
            {
                EditorGUILayout.LabelField("Saved in", AssetDatabase.GetAssetPath(settings), EditorStyles.miniLabel);
            }
        }

        private static AgenerelaSettings ArmField(AgenerelaSettings settings, GUIContent label, Slot slot, bool readOnly)
        {
            var current = settings == null ? null : slot == Slot.ArmA ? settings.EvaluationArmA : settings.EvaluationArmB;
            EditorGUI.BeginChangeCheck();
            ProviderConfig chosen;
            using (new EditorGUI.DisabledScope(readOnly))
            {
                chosen = (ProviderConfig)EditorGUILayout.ObjectField(label, current, typeof(ProviderConfig), false);
            }

            return EditorGUI.EndChangeCheck() && !readOnly ? Set(slot, chosen, AssetPath) ?? settings : settings;
        }

        /// <summary>The project's settings, found the way a player finds them; null when there are none.</summary>
        internal static AgenerelaSettings Find()
        {
            return Resources.Load<AgenerelaSettings>(AgenerelaSettings.ResourceName);
        }

        /// <summary>
        /// Makes <paramref name="config"/> the project default and saves it, creating the settings at
        /// <paramref name="createAt"/> when the project has none. Clearing a default that was never
        /// set creates nothing.
        /// </summary>
        /// <returns>The settings, or null when there were none and nothing needed saving.</returns>
        /// <exception cref="System.InvalidOperationException">The settings found are in a read-only package.</exception>
        internal static AgenerelaSettings SetDefault(ProviderConfig config, string createAt) => Set(Slot.Default, config, createAt);

        /// <summary>
        /// Sets <paramref name="slot"/> to <paramref name="config"/> and saves it, as
        /// <see cref="SetDefault"/> does: creating the settings at <paramref name="createAt"/> when the
        /// project has none, and nothing when a slot that was never set is cleared.
        /// </summary>
        /// <returns>The settings, or null when there were none and nothing needed saving.</returns>
        /// <exception cref="System.InvalidOperationException">The settings found are in a read-only package.</exception>
        internal static AgenerelaSettings Set(Slot slot, ProviderConfig config, string createAt)
        {
            var settings = Find();
            if (settings != null && IsReadOnly(settings))
            {
                // Changed in memory only, it would look saved until the next reload.
                throw new System.InvalidOperationException(
                    $"The Agenerela settings at {AssetDatabase.GetAssetPath(settings)} are in a read-only package.");
            }

            if (settings == null)
            {
                if (config == null)
                {
                    return null;
                }

                CreateFolders(Path.GetDirectoryName(createAt).Replace('\\', '/'));
                settings = ScriptableObject.CreateInstance<AgenerelaSettings>();
                AssetDatabase.CreateAsset(settings, createAt);
            }

            switch (slot)
            {
                case Slot.Default:
                    Undo.RecordObject(settings, "Set Agenerela Default Provider");
                    settings.DefaultProvider = config;
                    break;
                case Slot.ArmA:
                    Undo.RecordObject(settings, "Set Agenerela Evaluation Arm A");
                    settings.EvaluationArmA = config;
                    break;
                case Slot.ArmB:
                    Undo.RecordObject(settings, "Set Agenerela Evaluation Arm B");
                    settings.EvaluationArmB = config;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(slot), slot, null);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            return settings;
        }

        // True for settings inside an immutable package, such as one installed from a registry,
        // where a change would never reach the disk.
        internal static bool IsReadOnly(AgenerelaSettings settings)
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(settings));
            return !string.IsNullOrEmpty(folder)
                   && AssetDatabase.TryGetAssetFolderInfo(folder.Replace('\\', '/'), out _, out bool immutable)
                   && immutable;
        }

        // AssetDatabase.CreateFolder makes one level at a time.
        private static void CreateFolders(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            CreateFolders(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
