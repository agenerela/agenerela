using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// What the Evaluation Report window remembers between sessions (#146): the set, the profile,
    /// each arm's config, name and <c>Check targets</c> switch, the order, and the report shown
    /// last. Kept on this machine in <see cref="EditorPrefs"/>, never in a committed file, keyed per
    /// project as <see cref="PromptSetPrefs"/> is.
    /// </summary>
    /// <remarks>Assets are stored as GUIDs, so a renamed or moved asset is still found.</remarks>
    internal sealed class EvaluationReportPrefs
    {
        private readonly Func<string, string> read;
        private readonly Action<string, string> write;
        private readonly string prefix;

        /// <param name="get">Reads a key, "" when it is not there.</param>
        /// <param name="set">Writes a key.</param>
        /// <param name="projectFolder">The project's folder, the one that holds <c>Assets/</c>.</param>
        internal EvaluationReportPrefs(Func<string, string> get, Action<string, string> set, string projectFolder)
        {
            read = get;
            write = set;
            prefix = "Agenerela.EvaluationReport." + PromptSetPrefs.ProjectKey(projectFolder) + ".";
        }

        /// <summary>The window's own: <see cref="EditorPrefs"/>, for the project open now.</summary>
        internal static EvaluationReportPrefs ForThisProject()
        {
            return new EvaluationReportPrefs(key => EditorPrefs.GetString(key, ""), EditorPrefs.SetString,
                Path.GetDirectoryName(Application.dataPath));
        }

        /// <summary>The set file's GUID, or "".</summary>
        internal string SetGuid
        {
            get => Get("Set");
            set => Put("Set", value);
        }

        /// <summary>The profile's GUID, or "".</summary>
        internal string ProfileGuid
        {
            get => Get("Profile");
            set => Put("Profile", value);
        }

        /// <summary>Interleaved or Sequential, as the person last chose it; "" for the default.</summary>
        internal string Order
        {
            get => Get("Order");
            set => Put("Order", value);
        }

        /// <summary>The <c>report.json</c> shown last, a full path, or "".</summary>
        internal string LastReport
        {
            get => Get("LastReport");
            set => Put("LastReport", value);
        }

        /// <summary>Arm A's (<paramref name="arm"/> 0) or B's (1) provider config GUID, or "".</summary>
        internal string ConfigGuid(int arm) => Get(Arm(arm) + "Config");

        internal void SetConfigGuid(int arm, string guid) => Put(Arm(arm) + "Config", guid);

        /// <summary>The arm's name as typed, or "" when none was.</summary>
        internal string Name(int arm) => Get(Arm(arm) + "Name");

        internal void SetName(int arm, string name) => Put(Arm(arm) + "Name", name);

        /// <summary>The arm's <c>Check targets</c> switch, or null when it was never set.</summary>
        internal bool? CheckTarget(int arm)
        {
            string value = Get(Arm(arm) + "CheckTarget");
            return value == "1" ? true : value == "0" ? false : (bool?)null;
        }

        internal void SetCheckTarget(int arm, bool on) => Put(Arm(arm) + "CheckTarget", on ? "1" : "0");

        private static string Arm(int arm)
        {
            if (arm != 0 && arm != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(arm), arm, "0 for arm A, 1 for arm B.");
            }

            return arm == 0 ? "ArmA." : "ArmB.";
        }

        private string Get(string key) => read(prefix + key) ?? "";

        private void Put(string key, string value) => write(prefix + key, value ?? "");
    }
}
