using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agenerela.Providers.SdCpp.Native;
using UnityEngine;

namespace Agenerela.Providers.SdCpp
{
    /// <summary>
    /// Finds and loads stable-diffusion.cpp's native library (DR-028, Q6). Each build of the pinned
    /// release lives in its own folder, <c>StreamingAssets/Agenerela/sdcpp/&lt;build&gt;/</c>, so a
    /// game build carries it. They are tried fastest first (CUDA, then Vulkan, then CPU), and the first
    /// that loads is used for the rest of the process.
    /// </summary>
    /// <remarks>
    /// The library is never unloaded: ggml's backends cannot be unloaded safely once registered. A
    /// provider gives the card's memory back by freeing its model (<see cref="SdCppImageProvider.UnloadAsync"/>).
    /// </remarks>
    public static class SdCppLibrary
    {
        /// <summary>The folder under <c>StreamingAssets</c> every build sits in.</summary>
        public const string StreamingFolder = "Agenerela/sdcpp";

        private static readonly object Gate = new object();
        private static SdCppApi loaded;

        /// <summary>
        /// The folders to try, in order: <paramref name="folder"/> alone when it is given, otherwise
        /// each installed build for this platform under <paramref name="streamingAssets"/>, fastest first.
        /// </summary>
        public static IReadOnlyList<string> Candidates(string folder, string streamingAssets)
        {
            if (!string.IsNullOrWhiteSpace(folder))
            {
                return new[] { Path.GetFullPath(folder.Trim()) };
            }

            return SdCppRelease.Builds.Where(b => b.ForThisPlatform)
                .Select(b => Path.Combine(streamingAssets ?? "", StreamingFolder, b.Id))
                .Where(Directory.Exists)
                .ToList();
        }

        /// <summary>The installed build folder for <paramref name="build"/> in this project or game.</summary>
        public static string FolderFor(SdCppBuild build) => Path.Combine(Application.streamingAssetsPath, StreamingFolder, build.Id);

        /// <summary>The folder the library was loaded from, or null when it is not loaded yet.</summary>
        public static string LoadedFrom
        {
            get
            {
                lock (Gate)
                {
                    return loaded?.Folder;
                }
            }
        }

        // Before a domain reload: stop sd.cpp calling a log delegate that is about to disappear.
        internal static void BeforeReload()
        {
            lock (Gate)
            {
                SdCppNativeEngine.StopLogging(loaded);
            }
        }

        // Loads the first candidate that loads, once per process. Throws DllNotFoundException
        // naming every folder tried and why each failed.
        internal static SdCppApi Load(IReadOnlyList<string> candidates)
        {
            lock (Gate)
            {
                if (loaded != null)
                {
                    return loaded;
                }

                if (candidates == null || candidates.Count == 0)
                {
                    throw new DllNotFoundException(
                        "stable-diffusion.cpp is not installed in this project: no build folder under StreamingAssets/" + StreamingFolder +
                        ". Open Window → Agenerela → Local Image Models and install one, or set SdCppSettings.LibraryFolder.");
                }

                var why = new List<string>();
                foreach (string folder in candidates)
                {
                    try
                    {
                        loaded = SdCppApi.Load(folder);
                        Debug.Log($"[Agenerela] stable-diffusion.cpp {SdCppApi.Text(loaded.Version())} ({SdCppApi.Text(loaded.Commit())}) loaded from {folder}.");
                        return loaded;
                    }
                    catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException)
                    {
                        why.Add($"{folder}: {e.Message}");
                    }
                }

                throw new DllNotFoundException("No stable-diffusion.cpp build would load. " + string.Join(" ", why));
            }
        }
    }
}
