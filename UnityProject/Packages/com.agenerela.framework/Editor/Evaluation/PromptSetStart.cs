using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agenerela.Evaluation;
using UnityEditor;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// How the Prompt Set window's <b>Open the project's evaluation set</b> finds a set and the
    /// profile beside it (#158). Kept apart from the drawing so it can be tested.
    /// </summary>
    internal static class PromptSetStart
    {
        /// <summary>The file name a set has unless someone renamed it: what <b>New set from this profile…</b> suggests.</summary>
        internal const string SetFileName = "agent.json";

        /// <summary>Where a project keeps its set by convention, as agenerela's own <c>UnityProject</c> does.</summary>
        internal const string ConventionalSetPath = "Assets/Evaluation/" + SetFileName;

        /// <summary>
        /// Every <c>agent.json</c> among <paramref name="assetPaths"/>, the conventional one first
        /// and the rest in path order, so the button opens the same one on every machine.
        /// </summary>
        internal static IReadOnlyList<string> OrderSets(IEnumerable<string> assetPaths)
        {
            return assetPaths
                .Where(p => !string.IsNullOrEmpty(p) && string.Equals(Path.GetFileName(p), SetFileName, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Replace('\\', '/'))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => string.Equals(p, ConventionalSetPath, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Every set file under <c>Assets/</c>; see <see cref="OrderSets"/>.</summary>
        internal static IReadOnlyList<string> FindSets()
        {
            return OrderSets(AssetDatabase.FindAssets("agent t:TextAsset", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath));
        }

        /// <summary>
        /// The profile beside the set at <paramref name="setAssetPath"/>, among
        /// <paramref name="profiles"/> (asset path, profile): the one in the same folder, not a
        /// subfolder. When there are several, the one whose actions agree with the set, if only one
        /// does; otherwise null, and a person chooses.
        /// </summary>
        /// <param name="set">The loaded set, to break a tie; null when it did not load.</param>
        internal static AgentProfile ProfileBeside(string setAssetPath, PromptSet set, IEnumerable<KeyValuePair<string, AgentProfile>> profiles)
        {
            string folder = FolderOf(setAssetPath);
            var beside = profiles
                .Where(p => p.Value != null && string.Equals(FolderOf(p.Key), folder, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Value)
                .ToList();

            if (beside.Count == 1)
            {
                return beside[0];
            }

            if (set == null)
            {
                return null;
            }

            var agreeing = beside.Where(p => PromptSetChecks.CompareWithProfile(set, p).Count == 0).ToList();
            return agreeing.Count == 1 ? agreeing[0] : null;
        }

        /// <summary>
        /// The profile the window uses once a set is opened. For another set: the one remembered
        /// for it on this machine, else the one beside it, else the one already chosen. For the set
        /// already open, the one chosen stays, and only a missing one is filled the same way, so
        /// opening a set whose profile was cleared still makes the window ready.
        /// </summary>
        /// <param name="beside">Looked up only when needed: it searches the project.</param>
        internal static AgentProfile ProfileWhenOpening(bool sameSet, AgentProfile current, AgentProfile remembered, Func<AgentProfile> beside)
        {
            if (sameSet && current != null)
            {
                return current;
            }

            return remembered ?? beside() ?? current;
        }

        /// <summary><see cref="ProfileBeside(string, PromptSet, IEnumerable{KeyValuePair{string, AgentProfile}})"/> over the project's assets.</summary>
        internal static AgentProfile FindProfileBeside(string setAssetPath, PromptSet set)
        {
            string folder = FolderOf(setAssetPath);
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                return null;
            }

            var profiles = AssetDatabase.FindAssets("t:" + nameof(AgentProfile), new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => new KeyValuePair<string, AgentProfile>(path, AssetDatabase.LoadAssetAtPath<AgentProfile>(path)));
            return ProfileBeside(setAssetPath, set, profiles);
        }

        private static string FolderOf(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) ? "" : (Path.GetDirectoryName(assetPath) ?? "").Replace('\\', '/');
        }
    }
}
