using System.Collections.Generic;

namespace Agenerela.Providers.SdCpp
{
    /// <summary>
    /// The one stable-diffusion.cpp release this package is bound to (DR-028, Q6): its tag, and for
    /// each build the file on GitHub, its size and its SHA-256, so a download is checked before it is
    /// used. sd.cpp publishes several builds a day with no semantic versions; this moves by hand,
    /// with the binding's tests.
    /// </summary>
    public static class SdCppRelease
    {
        /// <summary>The release tag, as GitHub names it.</summary>
        public const string Tag = "master-1127-79dc7ca";

        /// <summary>The commit it was built from.</summary>
        public const string Commit = "79dc7ca";

        /// <summary>The builds this package knows, fastest first within a platform: the order <see cref="SdCppLibrary"/> tries them in.</summary>
        public static readonly IReadOnlyList<SdCppBuild> Builds = new[]
        {
            new SdCppBuild("win-x64-cuda12", "Windows, NVIDIA CUDA 12", "sd-master-79dc7ca-bin-win-cuda12-x64.zip", 413084402,
                "984b6e22803df7cf8ddff968c6e7abbb026b06f326e1d00471b484e59f891f2b",
                "Also needs cudart-sd-bin-win-cu12-x64.zip (563 MB) unpacked into the same folder."),
            new SdCppBuild("win-x64-vulkan", "Windows, Vulkan (NVIDIA, AMD, Intel)", "sd-master-79dc7ca-bin-win-vulkan-x64.zip", 30956796,
                "cc504eabda2001367ab21d752b27da60a884e7c5c61f84d79e006b5ea4e5dc9f", ""),
            new SdCppBuild("win-x64-cpu", "Windows, CPU only", "sd-master-79dc7ca-bin-win-cpu-x64.zip", 19004333,
                "345d3ab72eec70c23f29cd3414a4403f8ee4d706811cd04b92e3b3ac9c75edbb", "Slow: minutes a picture."),
            new SdCppBuild("linux-x64-vulkan", "Linux, Vulkan (not yet tested)", "sd-master-79dc7ca-bin-Linux-Ubuntu-26.04-x86_64-vulkan.zip", 43890285,
                "b00638b1ce9b5ec6e96169260a4cb40d67ac1d9284c95e71200b0cea9b2dcb86", ""),
            new SdCppBuild("linux-x64-cpu", "Linux, CPU only (not yet tested)", "sd-master-79dc7ca-bin-Linux-Ubuntu-26.04-x86_64.zip", 30800118,
                "b1afb65b7c9712cad5b59c1020ea90b6c482fc42c4498db7f9a2fbb2e5e0ab94", ""),
            new SdCppBuild("macos-arm64", "macOS, Apple silicon (not yet tested)", "sd-master-79dc7ca-bin-Darwin-macOS-26.7.2-arm64.zip", 39649910,
                "3813e319158289a23e512992cf2a8429664c9af808305d2d9e5cdd150f803447", ""),
        };

        /// <summary>Where a build's file is downloaded from.</summary>
        public static string Url(SdCppBuild build) =>
            "https://github.com/leejet/stable-diffusion.cpp/releases/download/" + Tag + "/" + build.File;
    }

    /// <summary>One build of the pinned release: a platform and a GPU backend.</summary>
    public sealed class SdCppBuild
    {
        internal SdCppBuild(string id, string label, string file, long size, string sha256, string note)
        {
            Id = id;
            Label = label;
            File = file;
            Size = size;
            Sha256 = sha256;
            Note = note;
        }

        /// <summary>The folder name under <c>StreamingAssets/Agenerela/sdcpp/</c>, such as <c>win-x64-vulkan</c>.</summary>
        public string Id { get; }

        /// <summary>What it is, for a menu.</summary>
        public string Label { get; }

        /// <summary>The zip on the release page.</summary>
        public string File { get; }

        /// <summary>Its size in bytes.</summary>
        public long Size { get; }

        /// <summary>Its SHA-256, as GitHub publishes it, lower-case hex.</summary>
        public string Sha256 { get; }

        /// <summary>Anything else it needs, or an empty string.</summary>
        public string Note { get; }

        /// <summary>True for a build of the platform this runs on.</summary>
        public bool ForThisPlatform
        {
            get
            {
                var p = UnityEngine.Application.platform;
                if (p == UnityEngine.RuntimePlatform.WindowsEditor || p == UnityEngine.RuntimePlatform.WindowsPlayer)
                {
                    return Id.StartsWith("win-", System.StringComparison.Ordinal);
                }

                if (p == UnityEngine.RuntimePlatform.LinuxEditor || p == UnityEngine.RuntimePlatform.LinuxPlayer)
                {
                    return Id.StartsWith("linux-", System.StringComparison.Ordinal);
                }

                if (p == UnityEngine.RuntimePlatform.OSXEditor || p == UnityEngine.RuntimePlatform.OSXPlayer)
                {
                    return Id.StartsWith("macos-", System.StringComparison.Ordinal);
                }

                return false;
            }
        }
    }
}
