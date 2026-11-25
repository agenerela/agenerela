using System.IO;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Providers.SdCpp.Editor
{
    /// <summary>
    /// Window → Agenerela → Local Image Models: installs a build of the pinned stable-diffusion.cpp
    /// release into <c>StreamingAssets/Agenerela/sdcpp/</c>, from GitHub or from a zip already on disk,
    /// each checked against the hash the package lists (DR-028, Q6).
    /// </summary>
    internal sealed class SdCppSetupWindow : EditorWindow
    {
        private Vector2 scroll;

        [MenuItem("Window/Agenerela/Local Image Models")]
        private static void Open() => GetWindow<SdCppSetupWindow>("Local Image Models");

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("stable-diffusion.cpp " + SdCppRelease.Tag, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "A build is the native library for one platform and GPU backend. Install the ones your game needs; the fastest " +
                "installed one is used (CUDA, then Vulkan, then CPU). Builds and models go under StreamingAssets so a game build " +
                "carries them. Keep them out of version control.", MessageType.Info);
            string loaded = SdCppLibrary.LoadedFrom;
            EditorGUILayout.LabelField("Loaded now", loaded ?? "nothing (it loads with the first picture)");
            EditorGUILayout.Space();

            foreach (var build in SdCppRelease.Builds)
            {
                if (!build.ForThisPlatform)
                {
                    continue;
                }

                string folder = SdCppLibrary.FolderFor(build);
                bool installed = Directory.Exists(folder);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(build.Label, installed ? "installed" : "not installed", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(build.File + ", " + SdCppDownloads.Mb(build.Size), EditorStyles.miniLabel);
                    if (build.Note.Length > 0)
                    {
                        EditorGUILayout.LabelField(build.Note, EditorStyles.wordWrappedMiniLabel);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Download and install"))
                        {
                            string zip = Path.Combine(SdCppDownloads.Cache, build.File);
                            bool have = File.Exists(zip) && SdCppDownloads.Check(zip, build.Size, build.Sha256) == null;
                            if (have || SdCppDownloads.Download(SdCppRelease.Url(build), zip, build.Size, build.Sha256, "stable-diffusion.cpp"))
                            {
                                Install(zip, build, folder);
                            }
                        }

                        if (GUILayout.Button("Install from zip…"))
                        {
                            string zip = EditorUtility.OpenFilePanel("The release zip " + build.File, "", "zip");
                            if (!string.IsNullOrEmpty(zip))
                            {
                                string wrong = SdCppDownloads.Check(zip, build.Size, build.Sha256);
                                if (wrong != null)
                                {
                                    EditorUtility.DisplayDialog("Not the pinned build", wrong, "OK");
                                }
                                else
                                {
                                    Install(zip, build, folder);
                                }
                            }
                        }

                        using (new EditorGUI.DisabledScope(!installed || loaded == folder))
                        {
                            if (GUILayout.Button("Remove") && EditorUtility.DisplayDialog("Remove", "Delete " + folder + "?", "Delete", "Cancel"))
                            {
                                FileUtil.DeleteFileOrDirectory(folder);
                                FileUtil.DeleteFileOrDirectory(folder + ".meta");
                                AssetDatabase.Refresh();
                            }
                        }
                    }
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("New model recipe (Z-Image Turbo)"))
            {
                var model = CreateInstance<SdCppModel>();
                model.UseZImageTurbo();
                string path = AssetDatabase.GenerateUniqueAssetPath("Assets/ZImageTurbo.asset");
                AssetDatabase.CreateAsset(model, path);
                Selection.activeObject = model;
            }

            EditorGUILayout.EndScrollView();
        }

        private static void Install(string zip, SdCppBuild build, string folder)
        {
            if (SdCppLibrary.LoadedFrom == folder)
            {
                EditorUtility.DisplayDialog("In use", "This build is loaded in the editor and cannot be replaced until it restarts.", "OK");
                return;
            }

            int files = SdCppDownloads.Unpack(zip, folder);
            AssetDatabase.Refresh();
            Debug.Log($"[Agenerela] Installed {build.Label} ({files} files) into {folder}.");
        }
    }
}
