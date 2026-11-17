using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// What the Prompt Set window remembers between sessions (#158): the set, the profile and your
    /// name. Kept on this machine in <see cref="EditorPrefs"/>, never in a committed file, and keyed
    /// per project, so two projects open on one machine each keep their own.
    /// </summary>
    /// <remarks>
    /// The set and profile are stored as asset GUIDs, so a renamed or moved asset is still found.
    /// The profile is remembered per set, the one that set was last measured against, and also as
    /// the last one chosen, for a window that has no set yet.
    /// </remarks>
    internal sealed class PromptSetPrefs
    {
        private readonly Func<string, string> read;
        private readonly Action<string, string> write;
        private readonly string prefix;

        /// <param name="get">Reads a key, "" when it is not there.</param>
        /// <param name="set">Writes a key.</param>
        /// <param name="projectFolder">The project's folder, the one that holds <c>Assets/</c>.</param>
        internal PromptSetPrefs(Func<string, string> get, Action<string, string> set, string projectFolder)
        {
            read = get;
            write = set;
            prefix = "Agenerela.PromptSet." + ProjectKey(projectFolder) + ".";
        }

        /// <summary>The window's own: <see cref="EditorPrefs"/>, for the project open now.</summary>
        internal static PromptSetPrefs ForThisProject()
        {
            return new PromptSetPrefs(key => EditorPrefs.GetString(key, ""), EditorPrefs.SetString,
                Path.GetDirectoryName(Application.dataPath));
        }

        /// <summary>The set file's GUID, or "".</summary>
        internal string SetGuid
        {
            get => read(prefix + "Set") ?? "";
            set => write(prefix + "Set", value ?? "");
        }

        /// <summary>The name typed into <b>Your name</b>.</summary>
        internal string Annotator
        {
            get => read(prefix + "Annotator") ?? "";
            set => write(prefix + "Annotator", value ?? "");
        }

        /// <summary>The profile chosen last, with or without a set: a GUID, or "".</summary>
        internal string LastProfileGuid
        {
            get => read(prefix + "Profile") ?? "";
            set => write(prefix + "Profile", value ?? "");
        }

        /// <summary>The profile the set <paramref name="setGuid"/> was last measured against: a GUID, or "".</summary>
        internal string ProfileGuidFor(string setGuid)
        {
            return string.IsNullOrEmpty(setGuid) ? "" : read(prefix + "Profile." + setGuid) ?? "";
        }

        /// <summary>Remembers <paramref name="profileGuid"/> for the set <paramref name="setGuid"/>, and as the last one chosen.</summary>
        internal void RememberProfile(string setGuid, string profileGuid)
        {
            if (!string.IsNullOrEmpty(setGuid))
            {
                write(prefix + "Profile." + setGuid, profileGuid ?? "");
            }

            LastProfileGuid = profileGuid;
        }

        // A hash rather than the path itself: the key stays short and holds no separators. The
        // path is normalised first, since Windows does not care about case or slash direction.
        internal static string ProjectKey(string projectFolder)
        {
            string normal = Path.GetFullPath(projectFolder ?? ".").Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normal));
                var hex = new StringBuilder();
                for (int i = 0; i < 8; i++)
                {
                    hex.Append(hash[i].ToString("x2"));
                }

                return hex.ToString();
            }
        }
    }
}
