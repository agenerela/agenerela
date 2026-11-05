using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LivingVale.Editor
{
    public static class LivingValeBuild
    {
        [MenuItem("Living Vale/Build Windows Player")]
        public static void BuildWindows()
        {
            try
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(VillageSceneBuilder.ScenePath) == null)
                {
                    throw new InvalidOperationException("Run VillageSceneBuilder.Build before building the player.");
                }

                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows/LivingVale.exe"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { VillageSceneBuilder.ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException($"Windows build {report.summary.result}, {report.summary.totalErrors} errors.");
                }

                Debug.Log($"[LivingVale] Windows player built at {output}.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
