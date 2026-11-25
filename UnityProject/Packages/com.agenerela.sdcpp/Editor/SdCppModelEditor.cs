using System.IO;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Providers.SdCpp.Editor
{
    /// <summary>
    /// The model recipe's Inspector (DR-028, Q6): a <b>Choose…</b> button for each file, and a
    /// <b>Download</b> button for each file the recipe knows, asking first with its source, size and
    /// licence.
    /// </summary>
    [CustomEditor(typeof(SdCppModel))]
    internal sealed class SdCppModelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var model = (SdCppModel)target;
            EditorGUILayout.HelpBox("Files are absolute paths, or relative to StreamingAssets so a game build carries them.", MessageType.None);
            File(model, "Diffusion model", ref model.DiffusionModel, "diffusion");
            File(model, "Text encoder", ref model.TextEncoder, "text_encoder");
            File(model, "VAE", ref model.Vae, "vae");
            File(model, "Checkpoint", ref model.Checkpoint, "checkpoint");
            EditorGUILayout.Space();

            if (GUILayout.Button("Use Z-Image Turbo (8 GB card)"))
            {
                Undo.RecordObject(model, "Use Z-Image Turbo");
                model.UseZImageTurbo();
                EditorUtility.SetDirty(model);
            }

            EditorGUILayout.Space();
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "DiffusionModel", "TextEncoder", "Vae", "Checkpoint");
            serializedObject.ApplyModifiedProperties();
        }

        private static void File(SdCppModel model, string label, ref string path, string slot)
        {
            string resolved = SdCppModel.Resolve(path);
            bool present = resolved.Length > 0 && System.IO.File.Exists(resolved);
            using (new EditorGUILayout.HorizontalScope())
            {
                string edited = EditorGUILayout.TextField(label, path);
                if (edited != path)
                {
                    Undo.RecordObject(model, "Set " + label);
                    path = edited;
                    EditorUtility.SetDirty(model);
                }

                if (GUILayout.Button("Choose…", GUILayout.Width(70)))
                {
                    string chosen = EditorUtility.OpenFilePanel(label, present ? Path.GetDirectoryName(resolved) : Application.streamingAssetsPath, "gguf,safetensors,sft,ckpt");
                    if (!string.IsNullOrEmpty(chosen))
                    {
                        Undo.RecordObject(model, "Choose " + label);
                        path = Relative(chosen);
                        EditorUtility.SetDirty(model);
                    }
                }
            }

            foreach (var known in model.KnownFiles)
            {
                if (known.Slot != slot)
                {
                    continue;
                }

                string file = Path.GetFileName(new System.Uri(known.Url).LocalPath);
                string destination = Path.Combine(Application.streamingAssetsPath, model.DownloadFolder ?? "", file);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(" ", $"{file}, {SdCppDownloads.Mb(known.Size)}, {known.Licence}", EditorStyles.miniLabel);
                    bool there = System.IO.File.Exists(destination);
                    using (new EditorGUI.DisabledScope(there))
                    {
                        if (GUILayout.Button(there ? "Downloaded" : "Download", GUILayout.Width(90))
                            && SdCppDownloads.Download(known.Url, destination, known.Size, known.Sha256, label + " (" + known.Licence + ")"))
                        {
                            Undo.RecordObject(model, "Download " + label);
                            path = Relative(destination);
                            EditorUtility.SetDirty(model);
                        }
                    }
                }
            }

            if (resolved.Length > 0 && !present)
            {
                EditorGUILayout.HelpBox(label + " is not at " + resolved, MessageType.Warning);
            }
        }

        // A path under StreamingAssets is stored relative to it, so it works in a build too.
        private static string Relative(string path)
        {
            string root = Path.GetFullPath(Application.streamingAssetsPath).Replace('\\', '/').TrimEnd('/') + "/";
            string full = Path.GetFullPath(path).Replace('\\', '/');
            return full.StartsWith(root, System.StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : full;
        }
    }
}
