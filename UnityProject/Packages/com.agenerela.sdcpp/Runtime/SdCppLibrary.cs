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

        private static readonly HashSet<SdCppImageProvider> Live = new HashSet<SdCppImageProvider>();
        private static bool hooked;

        // Every provider alive, so a domain reload can stop them all before the domain goes.
        internal static void Track(SdCppImageProvider provider)
        {
            lock (Gate)
            {
                Live.Add(provider);
                Hook();
            }
        }

        internal static void Untrack(SdCppImageProvider provider)
        {
            lock (Gate)
            {
                Live.Remove(provider);
            }
        }

        // Registered once per domain, as soon as a provider exists or the library is loaded: a
        // native callback or a worker still in native code must not outlive the domain.
        private static void Hook()
        {
            if (hooked)
            {
                return;
            }

            hooked = true;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
#endif
            AppDomain.CurrentDomain.DomainUnload += (_, __) => BeforeReload();
        }

        // Before a domain reload, or as the process ends: stop every provider and wait until its
        // worker is out of native code (blocking the reload until then), then stop sd.cpp calling
        // a log delegate that is about to disappear. Safe to call twice.
        internal static void BeforeReload()
        {
            List<SdCppImageProvider> providers;
            lock (Gate)
            {
                providers = new List<SdCppImageProvider>(Live);
            }

            var clock = System.Diagnostics.Stopwatch.StartNew();
            foreach (var provider in providers)
            {
                provider.Shutdown();
            }

            if (providers.Count > 0)
            {
                Debug.Log($"[Agenerela] Stopped {providers.Count} image model worker(s) before the domain went, in {clock.Elapsed.TotalSeconds:0.0} s.");
            }

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
                        var api = SdCppApi.Load(folder);
                        string devices = api.ListDevices().Replace("\n", "; ").Trim();
                        bool cpuBuild = Path.GetFileName(folder.TrimEnd('/', '\\')).IndexOf("cpu", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!cpuBuild && !SdCppApi.HasGpu(api.ListDevices()))
                        {
                            throw new DllNotFoundException(
                                $"The build in {folder} loaded but found no GPU, so it would run on the CPU only (devices: {devices}). " +
                                "Its GPU backend did not load: for CUDA, the CUDA runtime zip must be unpacked beside it; for Vulkan, " +
                                "the graphics driver must provide vulkan-1.dll. Install the CPU build if the CPU is meant.");
                        }

                        loaded = api;
                        Hook();
                        Debug.Log($"[Agenerela] stable-diffusion.cpp {SdCppApi.Text(loaded.Version())} ({SdCppApi.Text(loaded.Commit())}) loaded from {folder}. Devices: {devices}");
                        return loaded;
                    }
                    catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException)
                    {
                        why.Add($"{folder}: {e.Message}");
                        if (NativeLoader.IsResident(Path.Combine(folder, NativeLoader.LibraryFile("ggml"))))
                        {
                            // Its ggml is in the process now, and another build would load against it, so no
                            // other build is tried until Unity restarts.
                            throw new DllNotFoundException(string.Join(" ", why) + " Fix or remove that build and restart Unity: no other " +
                                                           "build is tried in this process, because this one's ggml is already loaded.");
                        }
                    }
                }

                throw new DllNotFoundException("No stable-diffusion.cpp build would load. " + string.Join(" ", why));
            }
        }
    }
}
